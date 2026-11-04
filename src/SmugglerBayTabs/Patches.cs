using HarmonyLib;

namespace SmugglerBayTabs;

[HarmonyPatch(typeof(StorageExpansionHelper), "GetKitTabs")]
internal static class Patch_GetKitTabs
{
    private static void Postfix(GameItem bay, ref int __result)
    {
        if (__result != 0 || bay == null)
        {
            return;
        }

        if (bay.identifier == "smuggler_bay")
        {
            __result = StorageExpansionHelper.STORAGE_BAY_KIT_TABS;
        }
        else if (bay.identifier == "smuggler_bay_mod")
        {
            __result = StorageExpansionHelper.STORAGE_BAY_LARGE_KIT_TABS;
        }
    }
}

[HarmonyPatch(typeof(StorageExpansionHelper), "AddTabs")]
internal static class Patch_AddTabs
{
    private const string InventoryId = "smuggler_bay";

    private static void Postfix(GameItem bay)
    {
        if (!IsSmuggler(bay))
        {
            return;
        }

        MachineryTabs tabs = (bay.contentWindow?.childElement as MachineryPanelRow)?.GetPanel("storage")?.content as MachineryTabs;
        if (tabs == null)
        {
            return;
        }

        for (int i = 0; i < tabs.count; i++)
        {
            GameInventory inventory = tabs.GetTabInventory(i);
            if (inventory != null)
            {
                inventory.identifier = InventoryId;
            }
        }
    }

    private static bool IsSmuggler(GameItem bay)
    {
        if (bay == null)
        {
            return false;
        }

        return bay.identifier == "smuggler_bay" || bay.identifier == "smuggler_bay_mod";
    }
}
