﻿using Newtonsoft.Json;
using System.Globalization;

namespace Walgelijk.Localisation;

public static class Localisation
{
    /// <summary>
    /// Currently selected language
    /// </summary>
    public static Language? CurrentLanguage;

    /// <summary>
    /// Language to display
    /// </summary>
    public static Language? FallbackLanguage;

    public static string Get(in string key, string? fallback = null)
    {
        var lang = CurrentLanguage ?? FallbackLanguage;
        if (lang == null || (!lang.Table.TryGetValue(key, out var value) && !(FallbackLanguage?.Table.TryGetValue(key, out value) ?? false)))
            return fallback ?? key;
        return value;
    }
}

public class Language
{
    public readonly string DisplayName;
    public readonly CultureInfo Culture;
    public IReadableTexture Flag = Flags.Unknown;

    public Dictionary<string, string> Table = new();

    public Language(string displayName, CultureInfo culture, IReadableTexture? flag = null)
    {
        DisplayName = displayName;
        Culture = culture;
        Flag = flag ?? Flags.Unknown;
    }

    public Language(string displayName, CultureInfo culture, IReadableTexture flag, Dictionary<string, string> table)
    {
        DisplayName = displayName;
        Culture = culture;
        Flag = flag;
        Table = table;
    }

    public Language(string displayName, CultureInfo culture, Dictionary<string, string> table)
    {
        DisplayName = displayName;
        Culture = culture;
        Flag = Flags.Unknown;
        Table = table;
    }

    public override string ToString() => DisplayName;

    public static Language Load(string filePath)
    {
        var data = File.ReadAllText(filePath);
        var s = JsonConvert.DeserializeObject<Serialisable>(data);
        return new Language(
            s.DisplayName ?? Path.GetFileNameWithoutExtension(filePath),
            string.IsNullOrWhiteSpace(s.Culture) ? CultureInfo.InvariantCulture : new CultureInfo(s.Culture),
            string.IsNullOrWhiteSpace(s.Flag) ? Flags.Unknown : Resources.Load<Texture>(Path.GetFullPath(s.Flag, Path.GetDirectoryName(filePath) ?? Environment.CurrentDirectory), true),
            s.Table ?? new Dictionary<string, string>());
    }

    private struct Serialisable
    {
        public string? DisplayName;
        public string? Culture;
        public string? Flag;
        public Dictionary<string, string>? Table;
    }
}

public readonly struct Flags
{
    public static readonly Texture Unknown = TextureLoader.FromBytes(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10, 0, 0, 0, 13, 73, 72, 68, 82, 0, 0, 0, 64, 0, 0, 0, 48, 8, 0, 0, 0, 0, 132, 32, 35, 195, 0, 0, 0, 1, 115, 82, 71, 66, 0, 174, 206, 28, 233, 0, 0, 0, 4, 103, 65, 77, 65, 0, 0, 177, 143, 11, 252, 97, 5, 0, 0, 0, 9, 112, 72, 89, 115, 0, 0, 14, 195, 0, 0, 14, 195, 1, 199, 111, 168, 100, 0, 0, 0, 222, 73, 68, 65, 84, 72, 199, 99, 208, 160, 16, 48, 140, 26, 48, 106, 192, 72, 48, 64, 47, 175, 119, 201, 228, 26, 39, 114, 13, 48, 95, 244, 231, 63, 24, 236, 243, 36, 203, 0, 251, 251, 255, 225, 32, 131, 28, 3, 206, 130, 181, 222, 132, 152, 224, 64, 186, 1, 177, 32, 125, 19, 129, 140, 22, 16, 99, 2, 233, 6, 204, 0, 106, 187, 15, 102, 93, 0, 178, 14, 144, 110, 192, 117, 160, 182, 86, 48, 171, 24, 228, 19, 74, 210, 65, 23, 208, 128, 147, 20, 24, 160, 15, 138, 141, 21, 228, 27, 224, 124, 17, 20, 136, 65, 100, 27, 16, 251, 4, 164, 127, 50, 217, 121, 161, 14, 156, 10, 38, 145, 159, 153, 94, 0, 181, 223, 75, 32, 63, 55, 246, 2, 245, 63, 52, 167, 32, 59, 31, 1, 26, 80, 64, 73, 121, 240, 244, 255, 255, 159, 20, 21, 40, 159, 254, 255, 127, 73, 145, 1, 254, 64, 48, 176, 101, 162, 239, 178, 53, 17, 148, 24, 224, 245, 251, 255, 255, 127, 190, 20, 24, 208, 1, 74, 134, 189, 20, 24, 144, 15, 50, 160, 132, 146, 48, 216, 141, 167, 44, 34, 46, 22, 252, 195, 70, 43, 215, 81, 3, 70, 134, 1, 0, 194, 214, 75, 209, 33, 211, 82, 142, 0, 0, 0, 0, 73, 69, 78, 68, 174, 66, 96, 130 });
    public static readonly Texture Netherlands = TextureLoader.FromBytes(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10, 0, 0, 0, 13, 73, 72, 68, 82, 0, 0, 0, 80, 0, 0, 0, 53, 4, 3, 0, 0, 0, 36, 6, 113, 119, 0, 0, 0, 1, 115, 82, 71, 66, 0, 174, 206, 28, 233, 0, 0, 0, 4, 103, 65, 77, 65, 0, 0, 177, 143, 11, 252, 97, 5, 0, 0, 0, 9, 112, 72, 89, 115, 0, 0, 14, 195, 0, 0, 14, 195, 1, 199, 111, 168, 100, 0, 0, 0, 15, 80, 76, 84, 69, 33, 70, 139, 107, 132, 178, 174, 28, 40, 200, 102, 110, 255, 255, 255, 95, 189, 67, 224, 0, 0, 0, 43, 73, 68, 65, 84, 72, 199, 99, 84, 98, 32, 14, 48, 49, 140, 42, 28, 85, 56, 120, 20, 178, 8, 82, 91, 225, 104, 128, 143, 42, 28, 76, 10, 25, 137, 77, 184, 163, 96, 20, 12, 73, 0, 0, 191, 251, 0, 160, 198, 164, 77, 146, 0, 0, 0, 0, 73, 69, 78, 68, 174, 66, 96, 130 });
    public static readonly Texture UnitedKingdom = TextureLoader.FromBytes(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10, 0, 0, 0, 13, 73, 72, 68, 82, 0, 0, 0, 80, 0, 0, 0, 40, 4, 3, 0, 0, 0, 155, 78, 96, 50, 0, 0, 0, 1, 115, 82, 71, 66, 0, 174, 206, 28, 233, 0, 0, 0, 4, 103, 65, 77, 65, 0, 0, 177, 143, 11, 252, 97, 5, 0, 0, 0, 9, 112, 72, 89, 115, 0, 0, 14, 195, 0, 0, 14, 195, 1, 199, 111, 168, 100, 0, 0, 0, 48, 80, 76, 84, 69, 1, 33, 105, 53, 78, 136, 57, 82, 138, 65, 89, 143, 170, 181, 205, 171, 181, 205, 185, 194, 214, 192, 200, 218, 200, 16, 46, 206, 212, 226, 214, 76, 98, 215, 79, 101, 241, 196, 203, 243, 240, 243, 244, 205, 211, 255, 255, 255, 186, 92, 184, 181, 0, 0, 1, 0, 73, 68, 65, 84, 56, 203, 237, 213, 61, 18, 1, 65, 16, 5, 224, 70, 201, 37, 114, 71, 193, 133, 54, 84, 34, 233, 220, 192, 37, 148, 35, 56, 128, 114, 6, 114, 74, 201, 86, 68, 155, 53, 127, 221, 175, 167, 148, 64, 168, 179, 217, 249, 146, 173, 125, 253, 150, 54, 204, 220, 142, 40, 207, 144, 157, 31, 158, 150, 39, 189, 230, 226, 220, 134, 28, 72, 3, 131, 99, 114, 32, 17, 70, 199, 180, 7, 9, 48, 57, 166, 230, 172, 165, 134, 217, 181, 212, 95, 105, 169, 96, 118, 247, 9, 161, 148, 80, 57, 148, 2, 130, 3, 89, 160, 113, 90, 102, 216, 75, 175, 89, 156, 146, 9, 86, 157, 148, 227, 0, 103, 117, 39, 228, 35, 192, 147, 113, 139, 56, 203, 211, 241, 112, 184, 250, 171, 0, 159, 225, 240, 88, 167, 123, 98, 51, 111, 104, 230, 123, 232, 190, 156, 63, 252, 12, 127, 255, 101, 74, 40, 252, 201, 231, 226, 150, 97, 23, 18, 17, 138, 28, 51, 127, 229, 119, 124, 151, 98, 22, 142, 91, 27, 199, 232, 82, 112, 231, 77, 85, 22, 39, 86, 161, 34, 133, 147, 203, 101, 228, 64, 56, 181, 174, 32, 149, 211, 5, 160, 164, 118, 80, 41, 66, 130, 195, 146, 42, 18, 156, 169, 189, 40, 91, 2, 103, 139, 52, 74, 2, 87, 169, 230, 32, 9, 92, 173, 236, 223, 146, 192, 85, 127, 31, 157, 124, 1, 71, 211, 60, 60, 171, 81, 214, 57, 0, 0, 0, 0, 73, 69, 78, 68, 174, 66, 96, 130 });
}