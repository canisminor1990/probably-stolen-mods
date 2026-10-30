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

        bool themes = ModEntry.Themes == null || ModEntry.Themes.Value;
        if (!themes || Palette.Themes.Length == 0)
        {
            return;
        }

        string[] expanded = new string[spriteVariants.Length + Palette.Themes.Length];
        int index = 0;
        for (int i = 0; i < spriteVariants.Length; i++)
        {
            expanded[index++] = spriteVariants[i];
        }

        for (int i = 0; i < Palette.Themes.Length; i++)
        {
            expanded[index++] = Palette.Name(baseName, "t" + Palette.Themes[i]);
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

        if (ModEntry.Themes != null && !ModEntry.Themes.Value)
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
        else
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
        if (Util.TryParse(spritePath, out _, out _))
        {
            return;
        }

        string baseName = Persist.ReadBase(__instance);
        if (baseName == null || spritePath != baseName || !Persist.TryRead(__instance, out string suffix))
        {
            return;
        }

        string variant = Palette.Name(baseName, suffix);
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
}
