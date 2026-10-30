using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace StorageThemes;

[HarmonyPatch(typeof(ToggleAppearanceHelper), nameof(ToggleAppearanceHelper.MakeSpriteCyclable))]
internal static class Patch_MakeSpriteCyclable
{
    private static readonly HashSet<string> Seen = new HashSet<string>();

    private static void Prefix(GameItem item, ref string[] spriteVariants)
    {
        if (spriteVariants == null || spriteVariants.Length == 0)
        {
            return;
        }

        for (int i = 0; i < spriteVariants.Length; i++)
        {
            string existing = spriteVariants[i];
            if (existing != null && existing.Contains(Palette.Marker))
            {
                return;
            }
        }

        string baseName = spriteVariants[0];
        Persist.WriteBase(item, baseName);
        LogOnce(item, baseName);

        if (!ModEntry.IsKindEnabled(baseName) || Palette.Themes.Length == 0)
        {
            return;
        }

        List<string> painted = new List<string>();
        for (int i = 0; i < Palette.Themes.Length; i++)
        {
            string theme = Palette.Themes[i];
            if (ThemeArt.Has(baseName, theme))
            {
                painted.Add(Palette.Name(baseName, "t" + theme));
            }
        }

        if (painted.Count == 0)
        {
            return;
        }

        string[] expanded = new string[spriteVariants.Length + painted.Count];
        int index = 0;
        for (int i = 0; i < spriteVariants.Length; i++)
        {
            expanded[index++] = spriteVariants[i];
        }

        for (int i = 0; i < painted.Count; i++)
        {
            expanded[index++] = painted[i];
        }

        spriteVariants = expanded;
    }

    private static void LogOnce(GameItem item, string baseName)
    {
        string identifier = item != null ? item.identifier : null;
        if (string.IsNullOrEmpty(identifier))
        {
            identifier = "(null)";
        }

        if (Seen.Add(identifier))
        {
            ModEntry.Log?.Log($"Cyclable sprite '{baseName}' on '{identifier}'.");
        }
    }

    private static void Postfix(GameItem item, string atlasPath, string[] spriteVariants)
    {
        if (item == null || spriteVariants == null || spriteVariants.Length == 0)
        {
            return;
        }

        if (!ModEntry.IsKindEnabled(spriteVariants[0]))
        {
            return;
        }

        bool hasTheme = false;
        for (int i = 0; i < spriteVariants.Length; i++)
        {
            string variant = spriteVariants[i];
            if (variant != null && variant.Contains(Palette.Marker))
            {
                hasTheme = true;
                break;
            }
        }

        if (!hasTheme)
        {
            return;
        }

        string[] options = (string[])spriteVariants.Clone();
        item.onToggleSlotItemFunc = (toggled, _, _) => ThemePicker.Open(toggled, atlasPath, options);
    }
}

[HarmonyPatch(typeof(ShipSystemDirectory), "SmugglerBay")]
internal static class Patch_SmugglerBay
{
    private static void Postfix(GameItem __result)
    {
        Enable(__result, "smuggler_bay");
    }

    internal static void Enable(GameItem item, string sprite)
    {
        Enable(item, "Items/items_ship", sprite);
    }

    internal static void Enable(GameItem item, string atlas, string sprite)
    {
        if (item == null || string.IsNullOrEmpty(atlas) || string.IsNullOrEmpty(sprite) || !ModEntry.IsKindEnabled(sprite))
        {
            return;
        }

        ToggleAppearanceHelper.MakeSpriteCyclable(item, atlas, sprite);
    }
}

[HarmonyPatch(typeof(ShipSystemDirectory), "HyperTechBay")]
internal static class Patch_HyperTechBay
{
    private static void Postfix(GameItem __result)
    {
        Patch_SmugglerBay.Enable(__result, "Items/items_tool2", "hypertech_bay");
    }
}

[HarmonyPatch(typeof(ShipSystemDirectory), "MachineBay")]
internal static class Patch_MachineBay
{
    private static void Postfix(GameItem __result)
    {
        Patch_SmugglerBay.Enable(__result, "Items/items_tool2", "machine_bay0");
    }
}

[HarmonyPatch(typeof(ShipSystemDirectory), "MachineBayExt")]
internal static class Patch_MachineBayExt
{
    private static void Postfix(GameItem __result)
    {
        Patch_SmugglerBay.Enable(__result, "Items/items_tool2", "machine_bay_ext");
    }
}

[HarmonyPatch(typeof(GameItem), nameof(GameItem.SetSpriteAndShape))]
internal static class Patch_SetSpriteAndShape
{
    private static void Postfix(GameItem __instance, string spriteAtlasPath, string spritePath)
    {
        if (!Util.IsMachineBayFrame(spritePath) || !ModEntry.IsKindEnabled(spritePath) || !Persist.TryRead(__instance, out string suffix))
        {
            return;
        }

        string variant = Palette.Name(spritePath, suffix);
        __instance.SetSprite(spriteAtlasPath, variant);
        if (__instance is GameItemElement element)
        {
            Sprite sprite = element.ResolveSpriteByName(variant);
            if (sprite != null)
            {
                element.ApplyAnimationFrame(sprite);
            }
        }
    }
}

[HarmonyPatch(typeof(ShipSystemDirectory), "SmugglerBayMod")]
internal static class Patch_SmugglerBayMod
{
    private static void Postfix(GameItem __result)
    {
        Patch_SmugglerBay.Enable(__result, "smuggler_bay_large");
    }
}

[HarmonyPatch(typeof(GameItemElement), nameof(GameItemElement.ResolveSpriteByName))]
internal static class Patch_ResolveSpriteByName
{
    private static bool Prefix(GameItemElement __instance, string name, ref Sprite __result)
    {
        if (name == null || name.IndexOf(Palette.Marker, System.StringComparison.Ordinal) < 0)
        {
            return true;
        }

        __result = SpriteFactory.Resolve(__instance, name);
        return __result == null;
    }
}

[HarmonyPatch(typeof(GameItem), nameof(GameItem.SetSprite))]
internal static class Patch_SetSprite
{
    private static void Postfix(GameItem __instance, string spritePath)
    {
        Remember(__instance, spritePath);
    }

    internal static void Remember(GameItem item, string spritePath)
    {
        if (Util.TryParse(spritePath, out _, out string suffix))
        {
            Persist.Write(item, suffix);
        }
        else if (ModEntry.IsKindEnabled(spritePath) || !Persist.TryRead(item, out _))
        {
            Persist.Clear(item);
        }
    }
}

[HarmonyPatch(typeof(GameItem), nameof(GameItem.SetSpriteQuick))]
internal static class Patch_SetSpriteQuick
{
    private static void Postfix(GameItem __instance, string spritePath)
    {
        Patch_SetSprite.Remember(__instance, spritePath);
    }
}

[HarmonyPatch(typeof(GameItemElement), nameof(GameItemElement.Validate))]
internal static class Patch_Validate
{
    [System.ThreadStatic]
    private static bool busy;

    private static void Postfix(GameItemElement __instance)
    {
        if (busy)
        {
            return;
        }

        string spritePath = __instance.spritePath;
        if (Util.TryParse(spritePath, out string themedBase, out _))
        {
            if (ModEntry.IsKindEnabled(themedBase))
            {
                return;
            }

            ShowPlain(__instance, themedBase);
            return;
        }

        if (!Persist.TryRead(__instance, out string suffix))
        {
            return;
        }

        string baseName = Persist.ReadBase(__instance);
        string frame = spritePath;
        if (baseName != null && spritePath == baseName)
        {
            frame = baseName;
        }
        else if (!Util.IsMachineBayFrame(spritePath))
        {
            return;
        }

        if (!ModEntry.IsKindEnabled(frame))
        {
            return;
        }

        string variant = Palette.Name(frame, suffix);
        busy = true;
        try
        {
            __instance.SetSprite(__instance.spriteAtlasPath, variant);
            Sprite sprite = __instance.ResolveSpriteByName(variant);
            if (sprite != null)
            {
                __instance.ApplyAnimationFrame(sprite);
            }
        }
        finally
        {
            busy = false;
        }
    }

    private static void ShowPlain(GameItemElement item, string spriteName)
    {
        busy = true;
        try
        {
            item.SetSprite(item.spriteAtlasPath, spriteName);
            Sprite sprite = item.ResolveSpriteByName(spriteName);
            if (sprite != null)
            {
                item.ApplyAnimationFrame(sprite);
            }
        }
        finally
        {
            busy = false;
        }
    }
}
