using System;
using System.Collections.Generic;
using HarmonyLib;

namespace AutoStorage;

internal static class Trash
{
    private const string Prefix = "trash.";
    private const string RewardPrefix = "trash.reward.";

    private static object store;
    private static int slot = int.MinValue;
    private static bool feces;
    private static bool nightly;
    private static readonly HashSet<int> rewards = new HashSet<int>();

    internal static bool Feces
    {
        get
        {
            Load();
            return feces;
        }
    }

    internal static bool Nightly
    {
        get
        {
            Load();
            return nightly;
        }
    }

    internal static void Load()
    {
        if (!PlayerStore.HasInstance)
        {
            return;
        }

        PlayerStore player = PlayerStore.Instance;
        if (ReferenceEquals(store, player) && slot == player.saveSlotId)
        {
            return;
        }

        store = player;
        slot = player.saveSlotId;
        feces = ModHelper.GetModData(ModEntry.Id, Prefix + "feces", "0") == "1";
        nightly = ModHelper.GetModData(ModEntry.Id, Prefix + "nightly", "0") == "1";
        rewards.Clear();
        List<string> keys = ModHelper.GetModDataKeys(ModEntry.Id);
        for (int i = 0; i < keys.Count; i++)
        {
            string key = keys[i];
            if (key != null && key.StartsWith(RewardPrefix, StringComparison.Ordinal) && int.TryParse(key.Substring(RewardPrefix.Length), out int id))
            {
                rewards.Add(id);
            }
        }
    }

    internal static void SetFeces(bool value)
    {
        Load();
        feces = value;
        ModHelper.SetModData(ModEntry.Id, Prefix + "feces", value ? "1" : "0");
    }

    internal static void SetNightly(bool value)
    {
        Load();
        nightly = value;
        ModHelper.SetModData(ModEntry.Id, Prefix + "nightly", value ? "1" : "0");
    }

    internal static void MarkReward(GameItem item)
    {
        if (item == null)
        {
            return;
        }

        Load();
        int id = item.uniqueId;
        rewards.Add(id);
        ModHelper.SetModData(ModEntry.Id, RewardPrefix + id, "1");
    }

    internal static bool IsReward(GameItem item)
    {
        return item != null && rewards.Contains(item.uniqueId);
    }

    internal static void Forget(GameItem item)
    {
        if (item == null)
        {
            return;
        }

        int id = item.uniqueId;
        rewards.Remove(id);
        ModHelper.SetModData(ModEntry.Id, RewardPrefix + id, null);
    }

    internal static void AfterSleep()
    {
        try
        {
            Load();
            if (!nightly || !Rules.GlobalEnabled)
            {
                return;
            }

            Sorter.DeliverRewards();
        }
        catch (Exception ex)
        {
            ModEntry.Log?.Warning("Nightly trash pickup failed: " + ex.Message);
        }
    }
}

[HarmonyPatch(typeof(ExchangeBarter), "Accept")]
internal static class Patch_ExchangeBarter
{
    private static void Prefix(ExchangeBarter __instance)
    {
        try
        {
            if (__instance == null || !string.Equals(__instance.identifier, "smuggler_barter", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            List<GameItem> required = __instance.GetRequiredItems();
            if (required == null)
            {
                return;
            }

            for (int i = 0; i < required.Count; i++)
            {
                GameItem item = required[i];
                if (item != null && string.Equals(Catalog.IdOf(item), "smuggler_bay", StringComparison.OrdinalIgnoreCase))
                {
                    Catalog.Retire(item);
                }
            }
        }
        catch (Exception ex)
        {
            ModEntry.Log?.Warning("Could not remember the smuggler bay upgrade: " + ex.Message);
        }
    }
}

[HarmonyPatch(typeof(ExchangeManager), "TransferBuffer")]
internal static class Patch_ExchangeDelivery
{
    private static void Prefix(ExchangeManager __instance)
    {
        try
        {
            List<GameItem> buffer = __instance == null ? null : __instance.itemsBuffer;
            if (buffer == null)
            {
                return;
            }

            for (int i = 0; i < buffer.Count; i++)
            {
                Trash.MarkReward(buffer[i]);
            }
        }
        catch (Exception ex)
        {
            ModEntry.Log?.Warning("Could not remember exchange rewards: " + ex.Message);
        }
    }
}
