using System;

namespace StorageThemes;

internal enum ContainerKind
{
    None,
    Storage,
    Smuggler,
    Hypertech,
    Machine,
}

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

    internal static bool IsMachineBayFrame(string name)
    {
        const string Prefix = "machine_bay";
        if (name == null || name.Length != Prefix.Length + 1 || !name.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return false;
        }

        char digit = name[Prefix.Length];
        return digit >= '0' && digit <= '4';
    }

    internal static ContainerKind KindOf(string spriteName)
    {
        if (string.IsNullOrEmpty(spriteName))
        {
            return ContainerKind.None;
        }

        if (TryParse(spriteName, out string baseName, out _))
        {
            spriteName = baseName;
        }

        if (spriteName.StartsWith("storage_bay", StringComparison.Ordinal))
        {
            return ContainerKind.Storage;
        }

        if (spriteName.StartsWith("smuggler_bay", StringComparison.Ordinal))
        {
            return ContainerKind.Smuggler;
        }

        if (spriteName == "hypertech_bay")
        {
            return ContainerKind.Hypertech;
        }

        if (spriteName == "machine_bay_ext" || spriteName == "machine_bay" || IsMachineBayFrame(spriteName))
        {
            return ContainerKind.Machine;
        }

        return ContainerKind.None;
    }
}
