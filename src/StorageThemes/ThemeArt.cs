using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace StorageThemes;

internal static class ThemeArt
{
    private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();
    private static readonly HashSet<string> SizeLogged = new HashSet<string>();
    private static readonly HashSet<string> MissingLogged = new HashSet<string>();

    internal static Sprite Get(GameItemElement element, string baseName, string themeKey)
    {
        if (element == null || string.IsNullOrEmpty(baseName) || string.IsNullOrEmpty(themeKey))
        {
            return null;
        }

        string shape = SanitizeShape(baseName);
        string cacheKey = shape + "/" + themeKey;
        if (Cache.TryGetValue(cacheKey, out Sprite cached) && cached != null)
        {
            return cached;
        }

        Sprite baseSprite = element.ResolveSpriteByName(baseName);
        if (baseSprite == null)
        {
            return null;
        }

        Rect textureRect = baseSprite.textureRect;
        int width = Mathf.RoundToInt(textureRect.width);
        int height = Mathf.RoundToInt(textureRect.height);
        if (SizeLogged.Add(shape))
        {
            ModEntry.Log?.Log($"Storage shape '{shape}' is {width}x{height}. Art belongs in Textures/<theme>/{shape}.png.");
        }

        string relativePath = "Textures/" + themeKey + "/" + shape + ".png";
        byte[] png = ModHelper.ReadModFileBytes(ModEntry.ModId, relativePath);
        if (png == null)
        {
            if (MissingLogged.Add(cacheKey))
            {
                ModEntry.Log?.Warning("Missing theme art " + relativePath + ". That toggle step keeps the original sprite.");
            }

            return baseSprite;
        }

        Texture2D texture = LoadPng(png, relativePath);
        if (texture == null)
        {
            return baseSprite;
        }

        if (texture.width != width || texture.height != height)
        {
            ModEntry.Log?.Warning($"Theme '{themeKey}' art for '{shape}' is {texture.width}x{texture.height}, base is {width}x{height}.");
        }

        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.hideFlags = HideFlags.HideAndDontSave;
        Sprite sprite = SpriteFactory.BuildSpriteFromTexture(texture, baseSprite);
        Cache[cacheKey] = sprite;
        return sprite;
    }

    private static string SanitizeShape(string baseName)
    {
        StringBuilder builder = new StringBuilder(baseName.Length);
        foreach (char c in baseName)
        {
            builder.Append(char.IsLetterOrDigit(c) || c == '_' || c == '-' ? c : '_');
        }

        return builder.ToString();
    }

    private static Texture2D LoadPng(byte[] png, string relativePath)
    {
        Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false)
        {
            hideFlags = HideFlags.HideAndDontSave
        };
        if (!ImageConversion.LoadImage(texture, png))
        {
            ModEntry.Log?.Warning("Failed to decode " + relativePath + ".");
            return null;
        }

        return texture;
    }
}
