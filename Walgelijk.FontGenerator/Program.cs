﻿using Newtonsoft.Json;
using System.Diagnostics;
using System.IO.Compression;
using System.Linq;
using System.Numerics;
using System.Reflection;

namespace Walgelijk.FontGenerator;

public class Program
{
    public const int FontSize = 72;

    static void Main(string[] args)
    {
        if (args.Length != 1)
            throw new Exception("Expected 1 argument: the path to the .ttf file");

        if (!args[0].EndsWith(".ttf"))
            throw new Exception("Input is not a ttf file because it doesn't end with .ttf");

        var pathToTtf = Path.GetFullPath(args[0]);
        var fontName = Path.GetFileNameWithoutExtension(pathToTtf).Replace(' ', '_');

        foreach (var invalid in Path.GetInvalidFileNameChars().Append('_'))
            fontName = fontName.Replace(invalid, '-');

        const string intermediatePrefix = "wf-intermediate-";

        var intermediateImageOut = Path.GetFullPath($"{intermediatePrefix}{fontName}.png");
        var intermediateMetadataOut = Path.GetFullPath($"{intermediatePrefix}{fontName}.json");
        var finalOut = new FileInfo(pathToTtf).DirectoryName + Path.DirectorySeparatorChar + fontName + ".wf";

        var packageImageName = "atlas.png";
        var packageMetadataName = "meta.json";

        MsdfGen(pathToTtf, intermediateImageOut, intermediateMetadataOut);

        var metadata = JsonConvert.DeserializeObject<MsdfGenFont>(File.ReadAllText(intermediateMetadataOut)) ?? throw new Exception("Exported metadata does not exist...");

        AddLegacyKernings(pathToTtf, metadata);

        using var archiveStream = new FileStream(finalOut, FileMode.Create, FileAccess.Write);
        using var archive = new ZipArchive(archiveStream, ZipArchiveMode.Create, false);

        //var xheight = FontSize * Math.Abs(metadata.Metrics.Ascender - metadata.Metrics.Ascender * metadata.Metrics.LineHeight);
        var xheight = FontSize * (metadata.Glyphs!.Where(g => char.IsLower(g.Unicode)).Max(g => MathF.Abs(g.PlaneBounds.Top - MathF.Abs(metadata.Metrics.Descender))));

        var final = new FontFormat(
            name: fontName,
            style: FontStyle.Regular,
            size: FontSize,
            xheight: xheight,
            atlas: null!,
            lineHeight: metadata.Metrics.LineHeight * FontSize,
            kernings: (metadata.Kerning?.Select(a => new Kerning { Amount = a.Advance, FirstChar = a.Unicode1, SecondChar = a.Unicode2 }).ToArray())!,
            glyphs: (metadata.Glyphs?.Select(g => new Glyph(
                character: g.Unicode,
                advance: g.Advance * FontSize,
                textureRect: AbsoluteToTexcoords(g.AtlasBounds.GetRect(), new Vector2(metadata.Atlas.Width, metadata.Atlas.Height)),
                geometryRect: TransformGeometryRect(g.PlaneBounds.GetRect()).Translate(0, xheight)
            )).ToArray())!);

        using var metadataEntry = new StreamWriter(archive.CreateEntry(packageMetadataName, CompressionLevel.Fastest).Open());
        metadataEntry.Write(JsonConvert.SerializeObject(final));
        metadataEntry.Dispose();

        archive.CreateEntryFromFile(intermediateImageOut, packageImageName, CompressionLevel.Fastest);

        archive.Dispose();
        archiveStream.Dispose();

        File.Delete(intermediateImageOut);
        File.Delete(intermediateMetadataOut);
    }

    private static Rect AbsoluteToTexcoords(Rect rect, Vector2 size)
    {
        rect.MinX /= size.X;
        rect.MinY /= size.Y;
        rect.MaxX /= size.X;
        rect.MaxY /= size.Y;

        return rect;
    }

    private static Rect TransformGeometryRect(Rect rect)
    {
        rect.MinX *= FontSize;
        rect.MinY *= -FontSize;
        rect.MaxX *= FontSize;
        rect.MaxY *= -FontSize;

        return rect;
    }

    private static void MsdfGen(string pathToTtf, string intermediateImageOut, string intermediateMetadataOut)
    {
        using var process = new Process();
        var execDir = Path.GetDirectoryName(Assembly.GetEntryAssembly()!.Location)!;
        var processPath = Path.Combine(execDir, "msdf-atlas-gen");
        var charsetPath = Path.Combine(execDir, "charset.txt");
        process.StartInfo = new ProcessStartInfo(processPath,
            $"-font \"{pathToTtf}\" -size {FontSize} -charset \"{charsetPath}\" -format png -pots -imageout \"{intermediateImageOut}\" -json \"{intermediateMetadataOut}\"")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        Console.WriteLine("Starting msdf-atlas-gen...");
        process.Start();
        process.WaitForExit();
        while (!process.StandardError.EndOfStream)
            Console.WriteLine(process.StandardError.ReadLine());
        while (!process.StandardOutput.EndOfStream)
            Console.WriteLine(process.StandardOutput.ReadLine());
        Console.WriteLine("msdf-atlas-gen complete");
    }

    private static void AddLegacyKernings(string ttfPath, MsdfGenFont msdfGenFont)
    {
        using var process = new Process();
        var execDir = Path.GetDirectoryName(Assembly.GetEntryAssembly()!.Location)!;
        var processPath = Path.Combine(execDir, "ConvertGpos/", "index.js");
        process.StartInfo = new ProcessStartInfo("node", processPath + " " + ttfPath)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = Path.GetDirectoryName(processPath)
        };
        Console.WriteLine("Starting ConvertGpos...");
        process.ErrorDataReceived += (o, e) =>
        {
            throw new Exception(e.Data);
        };
        process.Start();
        process.WaitForExit();

        Console.WriteLine(process.StandardError.ReadToEnd());
        Console.WriteLine(process.StandardOutput.ReadToEnd());

        var kerningIntermediate = Path.Combine(execDir, "ConvertGpos/", "kerning_intermediate.json");

        var json = File.ReadAllText(kerningIntermediate);
        File.Delete(kerningIntermediate);
        var arr = JsonConvert.DeserializeObject<MsdfKerning[]>(json);
        if (arr == null)
            return;

        for (int i = 0; i < arr.Length; i++)
            arr[i].Advance /= FontSize;

        if (msdfGenFont.Kerning == null)
            msdfGenFont.Kerning = arr;
        else
            msdfGenFont.Kerning = arr.Concat(msdfGenFont.Kerning).Distinct().ToArray();

        Console.WriteLine("ConvertGpos complete");
    }

    // all of these are here for the deserialisation of the metadata that msdf-atlas-gen outputs.
    // DO NOT CHANGE ANY MEMBER NAMES

    public class MsdfGenFont
    {
        public MsdfAtlas Atlas;
        public MsdfMetrics Metrics;
        public MsdfGlyph[]? Glyphs;
        public MsdfKerning[]? Kerning;
    }

    public struct MsdfAtlas
    {
        public string Type;
        public float DistanceRange;
        public float Size;
        public int Width;
        public int Height;
        public string YOrigin;
    }

    public struct MsdfMetrics
    {
        public int EmSize;
        public float LineHeight;
        public float Ascender;
        public float Descender;
        public float UnderlineY;
        public float UnderlineThickness;
    }

    public struct MsdfGlyph
    {
        public char Unicode;
        public float Advance;
        public MsdfRect PlaneBounds, AtlasBounds;
    }

    public struct MsdfKerning
    {
        public char Unicode1, Unicode2;
        public float Advance;
    }

    public struct MsdfRect
    {
        public float Left, Bottom, Right, Top;

        public Rect GetRect() => new(Left, Bottom, Right, Top);
    }
}