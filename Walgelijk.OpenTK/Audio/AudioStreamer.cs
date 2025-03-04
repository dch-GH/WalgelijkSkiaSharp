﻿using OpenTK.Audio.OpenAL;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace Walgelijk.OpenTK;

public class AudioStreamer : IDisposable
{
    public const int BufferSize = 2048;
    public const int MaxBufferCount = 4;

    public readonly SourceHandle Source;
    public readonly Sound Sound;
    public readonly StreamAudioData AudioData;
    public readonly Dictionary<BufferHandle, BufferEntry> Buffers = [];

    public ALFormat Format => AudioData.ChannelCount == 1 ? ALFormat.Mono16 : ALFormat.Stereo16;

    public TimeSpan CurrentTime
    {
        get
        {
            AL.GetSource(Source, ALGetSourcei.SampleOffset, out int samplesPlayedInThisBuffer);
            AlCheck();
            var total = samplesPlayedInThisBuffer + processedSamples;
            return TimeSpan.FromSeconds((total / (float)AudioData.SampleRate / AudioData.ChannelCount) %
                                        (float)Sound.Data.Duration.TotalSeconds);
        }

        set =>
            stream.TimePosition = value > TimeSpan.Zero
                ? (value < AudioData.Duration ? value : AudioData.Duration)
                : TimeSpan.Zero;
    }

    private readonly short[] shortDataBuffer = new short[BufferSize];
    private readonly float[] rawBuffer = new float[BufferSize];
    private readonly int[] bufferHandles = new int[MaxBufferCount];
    private readonly Thread thread;
    private readonly CircularBuffer<float> recentlyPlayed = new(1024);

    private int processedSamples = 0;

    private IAudioStream stream;
    private bool endReached;
    private volatile bool monitorFlag = true;

    public AudioStreamer(SourceHandle sourceHandle, Sound sound, StreamAudioData raw)
    {
        AudioData = raw;
        Source = sourceHandle;
        Sound = sound;

        stream = AudioData.InputSourceFactory();

        for (int i = 0; i < MaxBufferCount; i++)
        {
            var a = new BufferEntry(AL.GenBuffer());
            AlCheck();
            Buffers.Add(a.Handle, a);
            bufferHandles[i] = a.Handle;
        }

        thread = new Thread(ThreadLoop);
        thread.IsBackground = true;
        thread.Start();
    }

    private void FillQueue()
    {
        foreach (var b in Buffers.Values)
            FillBuffer(b);
    }

    private bool FillBuffer(BufferEntry buffer)
    {
        if (ReadSamples(out var readAmount))
        {
            Array.Copy(rawBuffer, buffer.Data, BufferSize);

            for (int i = 0; i < readAmount; i++)
            {
                var v = (short)Utilities.MapRange(-1, 1, short.MinValue, short.MaxValue, buffer.Data[i]);
                shortDataBuffer[i] = v;
            }

            AL.BufferData<short>(buffer.Handle, Format, shortDataBuffer.AsSpan(0, readAmount), AudioData.SampleRate);
            AlCheck();

            AL.SourceQueueBuffer(Source, buffer.Handle);
            AlCheck();

            lock (recentlyPlayed)
            {
                for (int i = 0; i < readAmount; i++)
                    recentlyPlayed.Write(rawBuffer[i]);
            }

            return true;
        }

        return false;
    }

    private void ThreadLoop()
    {
        FillQueue();

        if (Sound.State == SoundState.Playing)
            AL.SourcePlay(Source);
        AlCheck();

        while (monitorFlag)
        {
            Thread.Sleep(8);

            if (!AL.IsSource(Source))
            {
                this.Dispose();
                break;
            }

            var state = Source.GetALState();
            AlCheck();
            var processed = AL.GetSource(Source, ALGetSourcei.BuffersProcessed);
            AlCheck();

            switch (Sound.State)
            {
                case SoundState.Playing:
                    // if we are requested to play, didnt reach the end yet, and the source isnt playing: play the source
                    if (!endReached && state is ALSourceState.Stopped or ALSourceState.Initial or ALSourceState.Paused)
                        AL.SourcePlay(Source);
                    AlCheck();
                    processed = AL.GetSource(Source, ALGetSourcei.BuffersProcessed);
                    AlCheck();
                    break;
                case SoundState.Paused:
                    // if we are requested to pause and the source is still playing, pause
                    if (state == ALSourceState.Playing)
                        AL.SourcePause(Source);
                    AlCheck();
                    continue;
                case SoundState.Stopped:
                    // if we are requested to stop and the source isnt stopped, stop
                    if (state is ALSourceState.Playing or ALSourceState.Paused)
                    {
                        AL.SourceStop(Source);
                        AlCheck();
                        processed = AL.GetSource(Source, ALGetSourcei.BuffersProcessed);
                        AlCheck();

                        for (int i = 0; i < processed; i++)
                        {
                            AL.SourceUnqueueBuffer(Source);
                            AlCheck();
                        }

                        //AL.SourceRewind(Source);
                        AlCheck();

                        stream?.Dispose();

                        endReached = false;
                        processedSamples = 0;
                        stream = AudioData.InputSourceFactory();

                        FillQueue();
                    }
                    continue;
            }

            while (processed > 0)
            {
                int bufferHandle = AL.SourceUnqueueBuffer(Source);
                AlCheck();

                if (Sound.State is SoundState.Playing && Buffers.TryGetValue(bufferHandle, out var buffer))
                    if (!FillBuffer(buffer))
                    {
                        endReached = true;
                        break;
                    }

                processed--;
                processedSamples += BufferSize;
            }

            if (endReached && !Sound.Looping)
                Sound.State = SoundState.Stopped;
        }
    }

    private bool ReadSamples(out int read)
    {
        read = stream.ReadSamples(rawBuffer);
        if (Sound.Looping)
        {
            // if the file is set to loop, just start from the beginning again
            if (read == 0)
            {
                stream.Position = 0;
                return ReadSamples(out read);
            }
        }

        return read > 0;
    }

    public void Dispose()
    {
        monitorFlag = false;
        GC.SuppressFinalize(this);

        AL.SourceStop(Source);
        AL.Source(Source, ALSourcei.Buffer, 0);

        foreach (var b in Buffers.Keys)
            AL.DeleteBuffer(b.Handle);

        stream.Dispose();
    }

    private static void AlCheck()
    {
#if DEBUG
        while (true)
        {
            var err = AL.GetError();
            if (err == ALError.NoError)
                break;
            Console.Error.WriteLine(err);
        }
#endif
    }

    public IEnumerable<float> TakeLastPlayed(int count = 1024)
    {
        lock (recentlyPlayed)
        {
            for (int i = 0; i < count; i++)
                yield return recentlyPlayed.Read();
        }
    }

    public class BufferEntry(BufferHandle bufferHandle)
    {
        public readonly BufferHandle Handle = bufferHandle;
        public float[] Data = new float[BufferSize];
        public override string ToString() => Handle.ToString();
    }
}