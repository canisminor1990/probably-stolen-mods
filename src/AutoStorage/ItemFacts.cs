using System;
using System.Collections.Generic;

namespace AutoStorage;

internal sealed class ItemFacts
{
    public string Id = "";
    public readonly HashSet<string> Types = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    public bool Contraband;

    public static ItemFacts Of(GameItem item)
    {
        ItemFacts facts = new ItemFacts();
        if (item == null)
        {
            return facts;
        }

        facts.Id = Catalog.IdOf(item);
        try
        {
            facts.Contraband = ContrabandHelper.IsContraband(item);
        }
        catch
        {
        }

        Collect(item, facts.Types);
        return facts;
    }

    public static void Collect(GameItem item, HashSet<string> types)
    {
        if (item == null || types == null)
        {
            return;
        }

        try
        {
            List<string> listed = item.GetGameItemType();
            int count = listed == null ? 0 : listed.Count;
            if (count > 0 && count <= 64)
            {
                for (int i = 0; i < count; i++)
                {
                    if (!string.IsNullOrEmpty(listed[i]))
                    {
                        types.Add(listed[i]);
                    }
                }
            }
        }
        catch
        {
        }

        try
        {
            TagSystem state = item.modifiedState ?? item.state;
            Dictionary<string, TagState> dict = state == null ? null : state.dict;
            if (dict != null)
            {
                foreach (KeyValuePair<string, TagState> pair in dict)
                {
                    if (pair.Value != null && pair.Value.IsEnabled() && !string.IsNullOrEmpty(pair.Key))
                    {
                        types.Add(pair.Key);
                    }
                }
            }
        }
        catch
        {
        }

        try
        {
            if (PlantGeneticsHelper.IsBlankDisk(item))
            {
                types.Add("DNA_DISK_BLANK");
            }
            else if (PlantGeneticsHelper.IsWrittenDisk(item))
            {
                types.Add("DNA_DISK_WRITTEN");
            }
        }
        catch
        {
        }

        if (!string.Equals(Catalog.IdOf(item), "wine_bottle", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        try
        {
            if (WineHelper.IsFinishedWine(item))
            {
                types.Add("WINE_BOTTLE_FINISHED");
            }
            else if (WineHelper.CanModifyLiquid(item))
            {
                types.Add("WINE_BOTTLE_EMPTY");
            }
        }
        catch
        {
        }
    }

    public static bool HasTag(GameItem item, string tag)
    {
        if (item == null || string.IsNullOrEmpty(tag))
        {
            return false;
        }

        try
        {
            if (item.IsTag(tag))
            {
                return true;
            }
        }
        catch
        {
        }

        try
        {
            List<string> listed = item.GetGameItemType();
            if (listed != null)
            {
                for (int i = 0; i < listed.Count && i < 64; i++)
                {
                    if (string.Equals(listed[i], tag, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }
        }
        catch
        {
        }

        return false;
    }

    public static string TypeName(string key)
    {
        if (string.IsNullOrEmpty(key))
        {
            return "";
        }

        if (string.Equals(key, "DNA_DISK_BLANK", StringComparison.OrdinalIgnoreCase))
        {
            return Text.Get("tag_dna_blank");
        }

        if (string.Equals(key, "DNA_DISK_WRITTEN", StringComparison.OrdinalIgnoreCase))
        {
            return Text.Get("tag_dna_written");
        }

        if (string.Equals(key, "WINE_BOTTLE_EMPTY", StringComparison.OrdinalIgnoreCase))
        {
            return Text.Get("tag_wine_empty");
        }

        if (string.Equals(key, "WINE_BOTTLE_FINISHED", StringComparison.OrdinalIgnoreCase))
        {
            return Text.Get("tag_wine_full");
        }

        try
        {
            string value = TypeHelper.GetTypeDisplayName(key);
            if (string.IsNullOrWhiteSpace(value))
            {
                return "";
            }

            string fallback = "type_" + key.ToLowerInvariant();
            if (string.Equals(value, fallback, StringComparison.OrdinalIgnoreCase) || value.StartsWith("Translation Error", StringComparison.OrdinalIgnoreCase))
            {
                return "";
            }

            return value.Trim();
        }
        catch
        {
            return "";
        }
    }

    public static string ItemName(string id)
    {
        if (string.IsNullOrEmpty(id) || id.IndexOf(' ') >= 0)
        {
            return "";
        }

        try
        {
            string key = "item_" + id + "_name";
            string value = LocHelper.GetLocalizedItem(key, null);
            if (!string.IsNullOrEmpty(value) && !string.Equals(value, key, StringComparison.OrdinalIgnoreCase) && !value.StartsWith("Translation Error", StringComparison.OrdinalIgnoreCase))
            {
                return value.Trim();
            }
        }
        catch
        {
        }

        return "";
    }
}
