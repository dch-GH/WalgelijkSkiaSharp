﻿using NVorbis;
using Walgelijk.AssetManager;
using Walgelijk.AssetManager.Deserialisers;

namespace Walgelijk.CommonAssetDeserialisers.Audio;

public class OggStreamAudioDeserialiser : IAssetDeserialiser<StreamAudioData>
{
    public bool IsCandidate(in AssetMetadata assetMetadata)
    {
        return
            assetMetadata.MimeType.Equals("audio/vorbis", StringComparison.InvariantCultureIgnoreCase) ||
            assetMetadata.MimeType.Equals("audio/ogg", StringComparison.InvariantCultureIgnoreCase);
    }

    public StreamAudioData Deserialise(Func<Stream> stream, in AssetMetadata assetMetadata)
    {
        using var reader = new VorbisReader(stream(), true);
        return new StreamAudioData(() => new OggAudioStream(stream()), reader.SampleRate, reader.Channels, reader.TotalSamples);
    }

    public class OggAudioStream : IAudioStream
    {
        private readonly VorbisReader reader;

        public OggAudioStream(string path)
        {
            reader = new VorbisReader(path);
        }

        public OggAudioStream(Stream source)
        {
            reader = new VorbisReader(source, true);
        }

        public long Position
        {
            get => reader.SamplePosition;
            set => reader.SamplePosition = value;
        }

        public TimeSpan TimePosition
        {
            get => reader.TimePosition;
            set => reader.TimePosition = value;
        }

        public int ReadSamples(Span<float> b) => reader.ReadSamples(b);

        public void Dispose()
        {
            reader?.Dispose();
        }
    }
}
