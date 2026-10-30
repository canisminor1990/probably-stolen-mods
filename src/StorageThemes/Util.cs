using System;

namespace StorageThemes;

internal static class Util
{
    internal static bool TryParse(string name, out string baseName, out string suffix)
    {
        baseName = null;
        suffix = null;
        if (name == null)
        {
            return false;
        }

        int split = name.LastIndexOf(Palette.Marker, StringComparison.Ordinal);
        if (split < 0)
        {
            return false;
        }

        baseName = name.Substring(0, split);
        suffix = name.Substring(split + Palette.Marker.Length);
        return suffix.Length > 0;
    }
}
