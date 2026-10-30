namespace StorageThemes;

internal static class Palette
{
    internal const string Marker = "::msc::";

    internal const string TagKey = "MSC_COLOR";

    internal const string BaseTagKey = "MSC_BASE";

    internal static readonly string[] Themes =
    {
        "blank",
        "chemistry",
        "chip",
        "fish",
        "food",
        "grape",
        "household",
        "material",
        "meat",
        "medical",
        "plant",
        "substance",
        "water",
        "weapon"
    };

    internal static string Name(string baseSpriteName, string suffix)
    {
        return baseSpriteName + Marker + suffix;
    }
}
