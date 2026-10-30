using UnityEngine;

namespace StorageThemes;

internal static class SpriteFactory
{
    internal static Sprite Resolve(GameItemElement element, string name)
    {
        if (element == null || !Util.TryParse(name, out string baseName, out string suffix) || suffix.Length < 2)
        {
            return null;
        }

        if (suffix[0] != 't')
        {
            return null;
        }

        return ThemeArt.Get(element, baseName, suffix.Substring(1));
    }

    internal static Sprite BuildSpriteFromTexture(Texture2D texture, Sprite reference)
    {
        int width = texture.width;
        int height = texture.height;
        Rect textureRect = reference.textureRect;
        float referenceWidth = Mathf.Max(textureRect.width, 1f);
        float referenceHeight = Mathf.Max(textureRect.height, 1f);
        Vector2 pivot = new Vector2(reference.pivot.x / referenceWidth, reference.pivot.y / referenceHeight);
        Sprite sprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, width, height),
            pivot,
            reference.pixelsPerUnit,
            0,
            SpriteMeshType.FullRect);
        sprite.name = reference.name + "_msc";
        sprite.hideFlags = HideFlags.HideAndDontSave;
        return sprite;
    }
}
