namespace StorageThemes;

internal static class Persist
{
    internal static void Write(GameItem item, string suffix)
    {
        TagSystem state = item?.state;
        if (state == null || string.IsNullOrEmpty(suffix))
        {
            return;
        }

        state.EnableTag(Palette.TagKey);
        TagState tag = state.GetTag(Palette.TagKey);
        if (tag == null)
        {
            return;
        }

        tag.valueString = suffix;
        tag.valueEnabled = true;
    }

    internal static void Clear(GameItem item)
    {
        if (item == null)
        {
            return;
        }

        TagState tag = item.GetTagReadonly(Palette.TagKey);
        if (tag != null && tag.valueEnabled)
        {
            item.DisableTag(Palette.TagKey, true);
        }
    }

    internal static bool TryRead(GameItem item, out string suffix)
    {
        suffix = null;
        TagState tag = item?.GetTagReadonly(Palette.TagKey);
        if (tag == null || !tag.valueEnabled || string.IsNullOrEmpty(tag.valueString))
        {
            return false;
        }

        suffix = tag.valueString;
        return true;
    }

    internal static void WriteBase(GameItem item, string baseName)
    {
        TagSystem state = item?.state;
        if (state == null || string.IsNullOrEmpty(baseName))
        {
            return;
        }

        state.EnableTag(Palette.BaseTagKey);
        TagState tag = state.GetTag(Palette.BaseTagKey);
        if (tag == null)
        {
            return;
        }

        tag.valueString = baseName;
        tag.valueEnabled = true;
    }

    internal static string ReadBase(GameItem item)
    {
        TagState tag = item?.GetTagReadonly(Palette.BaseTagKey);
        if (tag == null || !tag.valueEnabled)
        {
            return null;
        }

        return tag.valueString;
    }
}
