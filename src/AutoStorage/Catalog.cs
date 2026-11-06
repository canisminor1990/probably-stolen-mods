using System;
using System.Collections.Generic;
using UnityEngine;

namespace AutoStorage;

internal sealed class Bin
{
    public GameItem Item;
    public GameInventory Inventory;
    public string Slot;
    public string Label;
    public bool StoragePage;
    public bool DefaultTarget;
    public bool DefaultSource;
    public bool Missing;
    public bool Trash;

    public string Title
    {
        get
        {
            if (Trash)
            {
                return Text.Get("menu_trash");
            }

            string name = Catalog.NameOf(Item);
            if (string.IsNullOrEmpty(name))
            {
                name = Text.Get("window_title");
            }

            if (!string.IsNullOrEmpty(Label))
            {
                return name + "  ·  " + Label;
            }

            return name;
        }
    }
}

internal static class Catalog
{
    private static readonly string[] Blocked =
    {
        "display_case", "displaycase", "showcase", "counter", "sold", "trashcan", "trash", "drain", "faucet", "vending",
        "cassette", "swapbuffer", "swap_buffer", "response", "afterhour", "docinv", "bazar", "hidden", "dossier", "toolbox"
    };

    private static readonly List<Bin> bins = new List<Bin>();
    private static readonly List<GameItem> allItems = new List<GameItem>();
    private static readonly List<GameInventory> shielded = new List<GameInventory>();
    private static readonly HashSet<GameItem> retiredItems = new HashSet<GameItem>();
    private static readonly HashSet<string> retiredKeys = new HashSet<string>(StringComparer.Ordinal);
    private static float scannedAt = -1000f;

    internal static bool IncludeAll;

    internal static IReadOnlyList<Bin> Bins
    {
        get { return bins; }
    }

    internal static void Clear()
    {
        bins.Clear();
        allItems.Clear();
        shielded.Clear();
        retiredItems.Clear();
        retiredKeys.Clear();
        scannedAt = -1000f;
    }

    internal static void SetIncludeAll(bool value)
    {
        IncludeAll = value;
        try
        {
            ModHelper.SetModData(ModEntry.Id, "include_all", value ? "1" : "0");
        }
        catch
        {
        }

        scannedAt = -1000f;
    }

    internal static void Collect(bool force)
    {
        if (!force && Time.realtimeSinceStartup - scannedAt < 1.5f)
        {
            return;
        }

        scannedAt = Time.realtimeSinceStartup;
        bins.Clear();
        allItems.Clear();
        RebuildShield();
        HashSet<GameItem> seen = new HashSet<GameItem>();
        try
        {
            EmporiumEntry shop = EmporiumEntry.Instance;
            if (shop != null)
            {
                Walk(shop.invElement as GameInventory, seen);
                Walk(shop.frontInvinvElement as GameInventory, seen);
                Walk(shop.backInvinvElement as GameInventory, seen);
                Walk(shop.backInvinvElementCounter as GameInventory, seen);
                Walk(shop.showcaseElement as GameInventory, seen);
                Walk(shop.soldElement as GameInventory, seen);
                Walk(shop.hiddenElement as GameInventory, seen);
                Walk(shop.bazarLeftinvElement as GameInventory, seen);
                Walk(shop.afterhourInventory, seen);
                Walk(shop.hirelingInv, seen);
                Walk(shop.responseInventory, seen);
                Consider(shop.selectedBackpack, seen);
            }
        }
        catch (Exception ex)
        {
            ModEntry.Log?.Warning("Shop scan failed: " + ex.Message);
        }

        try
        {
            PlayerStore player = PlayerStore.HasInstance ? PlayerStore.Instance : null;
            List<GameItem> found = player == null ? null : player.FindAllItem(false);
            if (found != null)
            {
                for (int i = 0; i < found.Count; i++)
                {
                    Consider(found[i], seen);
                }
            }

            if (player != null)
            {
                Walk(player.GetStoreInventory() as GameInventory, seen);
            }
        }
        catch (Exception ex)
        {
            ModEntry.Log?.Warning("Item scan failed: " + ex.Message);
        }

        bins.Sort(CompareBins);
        Bin trash = TrashBin();
        if (trash != null)
        {
            bins.Insert(0, trash);
        }
    }

    internal static Bin Prefer(GameItem item)
    {
        if (item == null)
        {
            return bins.Count > 0 ? bins[0] : null;
        }

        if (IsTrash(item))
        {
            for (int i = 0; i < bins.Count; i++)
            {
                if (bins[i].Trash)
                {
                    return bins[i];
                }
            }
        }

        Bin first = null;
        for (int i = 0; i < bins.Count; i++)
        {
            Bin bin = bins[i];
            if (bin.Item != item)
            {
                continue;
            }

            if (first == null)
            {
                first = bin;
            }

            if (CanReceive(bin))
            {
                return bin;
            }
        }

        return first;
    }

    internal static bool CanReceive(Bin bin)
    {
        if (bin == null || bin.Trash || bin.Missing || bin.DefaultSource || bin.Inventory == null)
        {
            return false;
        }

        Rule rule = Rules.Get(bin.Item, bin.Slot);
        return rule.Receive ?? bin.DefaultTarget;
    }

    internal static bool CanSupply(Bin bin)
    {
        if (bin == null || bin.Trash || bin.Missing || bin.Inventory == null)
        {
            return false;
        }

        Rule rule = Rules.Get(bin.Item, bin.Slot);
        return rule.Supply ?? bin.DefaultSource;
    }

    internal static void Retire(GameItem item)
    {
        if (item == null)
        {
            return;
        }

        retiredItems.Add(item);
        string key = RetiredKey(item);
        if (!string.IsNullOrEmpty(key))
        {
            retiredKeys.Add(key);
        }
    }

    internal static bool IsTrash(GameItem item)
    {
        return item != null && string.Equals(IdOf(item), "trashcan", StringComparison.OrdinalIgnoreCase);
    }

    internal static bool IsFridge(GameItem item)
    {
        return item != null && string.Equals(IdOf(item), "fridge", StringComparison.OrdinalIgnoreCase);
    }

    internal static bool CanConfigure(GameItem item)
    {
        if (!Owned(item) || Retired(item))
        {
            return false;
        }

        if (IsTrash(item))
        {
            return true;
        }

        if (IsBlocked(IdOf(item)))
        {
            return false;
        }

        return IsMachine(item) || IsCage(item) || HasStorage(item) || IdHasStorage(item);
    }

    internal static bool Owned(GameItem item)
    {
        if (item == null)
        {
            return false;
        }

        try
        {
            return GeneralHelper.IsItemEffectivelyOwned(item);
        }
        catch
        {
            return false;
        }
    }

    internal static bool Stolen(GameItem item)
    {
        if (item == null)
        {
            return false;
        }

        try
        {
            return StolenHelper.IsStolenItem(item);
        }
        catch
        {
            return false;
        }
    }

    internal static bool IsSmuggler(GameItem item)
    {
        string id = IdOf(item);
        return id.StartsWith("smuggler_bay", StringComparison.OrdinalIgnoreCase);
    }

    internal static string IdOf(GameItem item)
    {
        if (item == null)
        {
            return "";
        }

        try
        {
            return item.identifier ?? "";
        }
        catch
        {
            return "";
        }
    }

    internal static int UidOf(GameItem item)
    {
        if (item == null)
        {
            return 0;
        }

        try
        {
            return item.GetUniqueID();
        }
        catch
        {
            return 0;
        }
    }

    internal static string NameOf(GameItem item)
    {
        if (item == null)
        {
            return "";
        }

        try
        {
            string name = item.GetDisplayName(true);
            if (!string.IsNullOrEmpty(name))
            {
                return name;
            }
        }
        catch
        {
        }

        return IdOf(item);
    }

    internal static bool Attached(GameItem item)
    {
        if (item == null)
        {
            return false;
        }

        try
        {
            GameInventory parent = item.parentInventory;
            List<GameItem> children = parent == null ? null : parent.childItems;
            if (children == null || children.Count > 4000)
            {
                return false;
            }

            for (int i = 0; i < children.Count; i++)
            {
                if (children[i] == item)
                {
                    return true;
                }
            }
        }
        catch
        {
        }

        return false;
    }

    internal static bool IsBag(GameItem item)
    {
        string id = IdOf(item).ToLowerInvariant();
        if (id.Length == 0 || IsBlocked(id))
        {
            return false;
        }

        if (id.Contains("backpack") || id.Contains("satchel") || id.Contains("fanny_pack") || id.Contains("fannypack"))
        {
            return true;
        }

        return ItemFacts.HasTag(item, "backpack");
    }

    internal static bool IsMachine(GameItem item)
    {
        return item != null && !Retired(item) && ItemFacts.HasTag(item, "MACHINE");
    }

    internal static bool IsCage(GameItem item)
    {
        return item != null && (string.Equals(IdOf(item), "animal_cage", StringComparison.OrdinalIgnoreCase) || ItemFacts.HasTag(item, "CAGE_TAG"));
    }

    internal static bool IsContainerItem(GameItem item)
    {
        if (!Owned(item) || Retired(item) || IsBlocked(IdOf(item)))
        {
            return false;
        }

        if (!IncludeAll && !HasStorage(item) && !IdHasStorage(item) && !IsCage(item) && !IsMachine(item))
        {
            return false;
        }

        return Resolve(item) != null;
    }

    internal static GameInventory TrashInventory()
    {
        try
        {
            EmporiumEntry shop = EmporiumEntry.Instance;
            return shop == null ? null : Resolve(shop.trashcan);
        }
        catch
        {
            return null;
        }
    }

    internal static List<GameItem> SourceItems()
    {
        Collect(false);
        List<GameItem> result = new List<GameItem>();
        HashSet<GameItem> seen = new HashSet<GameItem>();
        try
        {
            EmporiumEntry shop = EmporiumEntry.Instance;
            if (shop != null)
            {
                Take(shop.invElement as GameInventory, result, seen);
                Take(shop.frontInvinvElement as GameInventory, result, seen);
                Take(shop.backInvinvElement as GameInventory, result, seen);
                Take(shop.hiddenElement as GameInventory, result, seen);
                Take(shop.backInvinvElementCounter as GameInventory, result, seen);
                List<GameItem> counters = shop.GetCountersItems();
                if (counters != null)
                {
                    for (int i = 0; i < counters.Count; i++)
                    {
                        Push(counters[i], result, seen);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            ModEntry.Log?.Warning("Counter scan failed: " + ex.Message);
        }

        MineBags(result, seen);
        TakeMachineOutputs(result, seen);
        TakeSupply(result, seen);
        TakeFeces(result, seen);
        return result;
    }

    private static void RebuildShield()
    {
        shielded.Clear();
        try
        {
            EmporiumEntry shop = EmporiumEntry.Instance;
            if (shop != null)
            {
                Shield(shop.invElement as GameInventory);
                Shield(shop.frontInvinvElement as GameInventory);
                Shield(shop.backInvinvElement as GameInventory);
                Shield(shop.backInvinvElementCounter as GameInventory);
                Shield(shop.showcaseElement as GameInventory);
                Shield(shop.soldElement as GameInventory);
                Shield(shop.hiddenElement as GameInventory);
                Shield(shop.bazarLeftinvElement as GameInventory);
                Shield(Resolve(shop.trashcan));
                Shield(shop.afterhourInventory);
                Shield(shop.hirelingInv);
                Shield(shop.responseInventory);
            }
        }
        catch
        {
        }

        try
        {
            if (PlayerStore.HasInstance)
            {
                Shield(PlayerStore.Instance.GetStoreInventory() as GameInventory);
            }
        }
        catch
        {
        }
    }

    private static void Shield(GameInventory inventory)
    {
        if (inventory != null && !shielded.Contains(inventory))
        {
            shielded.Add(inventory);
        }
    }

    private static bool Shielded(GameInventory inventory)
    {
        return inventory != null && shielded.Contains(inventory);
    }

    private static void Walk(GameInventory inventory, HashSet<GameItem> seen)
    {
        if (inventory == null)
        {
            return;
        }

        List<GameItem> children = null;
        try
        {
            children = inventory.childItems;
        }
        catch
        {
        }

        if (children == null || children.Count > 4000)
        {
            return;
        }

        for (int i = 0; i < children.Count; i++)
        {
            Consider(children[i], seen);
        }
    }

    private static void Consider(GameItem item, HashSet<GameItem> seen)
    {
        if (item == null || !seen.Add(item) || Retired(item))
        {
            return;
        }

        allItems.Add(item);
        if (IsBag(item) || IsBlocked(IdOf(item)))
        {
            return;
        }

        Register(item);
    }

    private static void Register(GameItem item)
    {
        if (!Owned(item) || Retired(item) || IsBlocked(IdOf(item)))
        {
            return;
        }

        bool storageLike = IsMachine(item) || IsCage(item) || HasStorage(item) || IdHasStorage(item) || HasTabs(item);
        if (!Attached(item))
        {
            if (storageLike)
            {
                Retire(item);
            }

            return;
        }

        if (IsMachine(item) || HasTabs(item))
        {
            List<Bin> slots = SlotsOf(item);
            if (slots.Count == 0)
            {
                bins.Add(new Bin { Item = item, Missing = true });
            }
            else
            {
                bins.AddRange(slots);
            }

            return;
        }

        if (!HasStorage(item) && !IdHasStorage(item) && !IsCage(item) && !IncludeAll)
        {
            return;
        }

        GameInventory inventory = Resolve(item);
        if (inventory == null || Shielded(inventory) || !Owns(item, inventory))
        {
            return;
        }

        bins.Add(new Bin
        {
            Item = item,
            Inventory = inventory,
            DefaultTarget = true
        });
    }

    private static bool HasStorage(GameItem item)
    {
        return ItemFacts.HasTag(item, "STORAGE");
    }

    private static bool IdHasStorage(string id)
    {
        return !string.IsNullOrEmpty(id) && id.IndexOf("storage", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool IdHasStorage(GameItem item)
    {
        return IdHasStorage(IdOf(item));
    }

    private static bool IsBlocked(string id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return false;
        }

        string text = id.ToLowerInvariant();
        if (text == "save_bag" || text.EndsWith("_inv_save_bag", StringComparison.Ordinal))
        {
            return true;
        }

        for (int i = 0; i < Blocked.Length; i++)
        {
            if (text.Contains(Blocked[i]))
            {
                return true;
            }
        }

        return false;
    }

    private static bool Retired(GameItem item)
    {
        if (item == null)
        {
            return false;
        }

        if (retiredItems.Contains(item))
        {
            return true;
        }

        string key = RetiredKey(item);
        return !string.IsNullOrEmpty(key) && retiredKeys.Contains(key);
    }

    private static string RetiredKey(GameItem item)
    {
        string id = IdOf(item);
        int uid = UidOf(item);
        if (string.IsNullOrEmpty(id) || uid == 0)
        {
            return "";
        }

        return id + "#" + uid;
    }

    private static bool Owns(GameItem item, GameInventory inventory)
    {
        if (inventory == null)
        {
            return false;
        }

        try
        {
            GameItem parent = inventory.GetParentItem();
            if (parent == item)
            {
                return true;
            }
        }
        catch
        {
        }

        try
        {
            List<GameInventory> children = GraphUtils.FindChildrenType<GameInventory>(item, null, null);
            if (children == null)
            {
                return false;
            }

            for (int i = 0; i < children.Count; i++)
            {
                if (children[i] == inventory)
                {
                    return true;
                }
            }
        }
        catch
        {
        }

        return false;
    }

    internal static GameInventory Resolve(GameItem item)
    {
        if (item == null)
        {
            return null;
        }

        try
        {
            PixelWindow window = item.contentWindow;
            GameInventory fromWindow = FromWindow(window);
            if (fromWindow != null)
            {
                return fromWindow;
            }
        }
        catch
        {
        }

        try
        {
            List<GraphNodeStorage> children = item.children;
            if (children != null && children.Count > 0 && children.Count <= 256)
            {
                for (int i = 0; i < children.Count; i++)
                {
                    if (children[i] is GameInventory inventory)
                    {
                        return inventory;
                    }
                }
            }
        }
        catch
        {
        }

        try
        {
            GraphNodeWindow child = item.child;
            if (child != null)
            {
                return Dig(child.child, 0);
            }
        }
        catch
        {
        }

        return null;
    }

    private static GameInventory Dig(GraphNodeStorage node, int depth)
    {
        if (node == null || depth > 4)
        {
            return null;
        }

        if (node is GameInventory inventory)
        {
            return inventory;
        }

        try
        {
            List<GraphNodeStorage> children = node.children;
            if (children == null || children.Count <= 0 || children.Count > 256)
            {
                return null;
            }

            for (int i = 0; i < children.Count; i++)
            {
                GameInventory found = Dig(children[i], depth + 1);
                if (found != null)
                {
                    return found;
                }
            }
        }
        catch
        {
        }

        return null;
    }

    private static GameInventory FromWindow(PixelWindow window)
    {
        if (window == null)
        {
            return null;
        }

        if (window.childElement is GameInventory direct)
        {
            return direct;
        }

        if (window.child is GameInventory child)
        {
            return child;
        }

        if (window.childElement is GridPixelElement grid)
        {
            for (int y = 0; y < grid.gridHeight && y < 64; y++)
            {
                for (int x = 0; x < grid.gridWidth && x < 64; x++)
                {
                    if (grid.GetElement(x, y) is GameInventory inventory)
                    {
                        return inventory;
                    }
                }
            }
        }

        return null;
    }

    private static bool HasTabs(GameItem item)
    {
        if (IsFridge(item) || Retired(item))
        {
            return false;
        }

        List<Bin> slots = SlotsOf(item);
        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i].StoragePage)
            {
                return true;
            }
        }

        return false;
    }

    private static List<Bin> SlotsOf(GameItem item)
    {
        List<Bin> slots = new List<Bin>();
        if (item == null || IsBlocked(IdOf(item)))
        {
            return slots;
        }

        if (IsFridge(item))
        {
            AddSlot(slots, item, "chill", Mechanic("mech_fridge_label_chill", "冷藏区"), Zone(() => MachineFridge.GetChillZone(item)), true, false, false);
            AddSlot(slots, item, "freeze", Mechanic("mech_fridge_label_freeze", "冷冻区"), Zone(() => MachineFridge.GetFreezeZone(item)), true, false, false);
            return slots;
        }

        MachineryPanelRow row = null;
        try
        {
            if (item.contentWindow != null && item.contentWindow.childElement is MachineryPanelRow panelRow)
            {
                row = panelRow;
            }
        }
        catch
        {
        }

        if (row == null || row.Panels == null)
        {
            return slots;
        }

        for (int i = 0; i < row.Panels.Count; i++)
        {
            MachineryPanel panel = row.Panels[i];
            if (panel == null || panel.isHidden)
            {
                continue;
            }

            string role = Role(panel.role);
            if (role == "battery" || role == "module" || role == "note")
            {
                continue;
            }

            if (panel.content is MachineryTabs tabs && tabs.count > 0)
            {
                AddTabs(slots, item, panel, tabs, role);
                continue;
            }

            List<GraphNodeStorage> found = new List<GraphNodeStorage>();
            try
            {
                panel.CollectStorage(found);
            }
            catch
            {
            }

            for (int j = 0; j < found.Count; j++)
            {
                if (!(found[j] is GameInventory inventory) || Shielded(inventory))
                {
                    continue;
                }

                string id = role ?? ("slot" + i);
                if (HasSlot(slots, id))
                {
                    id = id + "_" + i + "_" + j;
                }

                string label = PanelLabel(panel);
                if (string.IsNullOrWhiteSpace(label))
                {
                    label = FallbackLabel(role, i);
                }

                AddSlot(slots, item, id, label, inventory, role == "input" || role == "container", role == "output", false);
            }
        }

        slots.Sort(CompareBins);
        return slots;
    }

    private static void AddTabs(List<Bin> slots, GameItem item, MachineryPanel panel, MachineryTabs tabs, string role)
    {
        for (int i = 0; i < tabs.count; i++)
        {
            PixelElement page = tabs.GetPage(i);
            if (page == null)
            {
                continue;
            }

            string caption = tabs.GetCaption(i);
            if (string.IsNullOrWhiteSpace(caption))
            {
                caption = (i + 1).ToString();
            }

            List<GraphNodeStorage> found = new List<GraphNodeStorage>();
            try
            {
                MachineryContainerUtils.Collect(page, found);
            }
            catch
            {
            }

            for (int j = 0; j < found.Count; j++)
            {
                if (!(found[j] is GameInventory inventory) || Shielded(inventory))
                {
                    continue;
                }

                string id = (string.IsNullOrEmpty(role) ? "tab" : role) + ".tab" + i;
                if (HasSlot(slots, id))
                {
                    id = id + "_" + j;
                }

                AddSlot(slots, item, id, caption, inventory, true, false, true);
            }
        }
    }

    private static void AddSlot(List<Bin> slots, GameItem item, string id, string label, GameInventory inventory, bool target, bool source, bool page)
    {
        if (inventory == null || Shielded(inventory))
        {
            return;
        }

        slots.Add(new Bin
        {
            Item = item,
            Inventory = inventory,
            Slot = id,
            Label = label,
            DefaultTarget = target,
            DefaultSource = source,
            StoragePage = page
        });
    }

    private static GameInventory Zone(Func<GameInventory> read)
    {
        try
        {
            return read();
        }
        catch
        {
            return null;
        }
    }

    private static string Mechanic(string key, string fallback)
    {
        try
        {
            string value = LocHelper.GetLocalizedMechanic(key, Array.Empty<object>());
            if (!string.IsNullOrWhiteSpace(value) && !value.StartsWith("Translation Error", StringComparison.OrdinalIgnoreCase))
            {
                return value;
            }
        }
        catch
        {
        }

        return fallback;
    }

    private static string Role(string role)
    {
        if (string.IsNullOrWhiteSpace(role))
        {
            return null;
        }

        switch (role.Trim().ToLowerInvariant())
        {
            case "input": return "input";
            case "output": return "output";
            case "battery":
            case "power": return "battery";
            case "module": return "module";
            case "note": return "note";
            case "container": return "container";
            case "storage": return "storage";
            case "chip": return "chip";
            default: return null;
        }
    }

    private static string PanelLabel(MachineryPanel panel)
    {
        try
        {
            if (panel.label != null)
            {
                return panel.label.GetText();
            }
        }
        catch
        {
        }

        return "";
    }

    private static string FallbackLabel(string role, int index)
    {
        switch (role)
        {
            case "input": return Text.Get("slot_input");
            case "output": return Text.Get("slot_output");
            case "container": return Text.Get("slot_container");
            case "storage": return Text.Get("slot_storage");
            case "chip": return Text.Get("slot_chip");
            default: return Text.Get("slot_unknown") + " " + (index + 1);
        }
    }

    private static bool HasSlot(List<Bin> slots, string id)
    {
        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i].Slot == id)
            {
                return true;
            }
        }

        return false;
    }

    private static int CompareBins(Bin left, Bin right)
    {
        int rank = Rank(left).CompareTo(Rank(right));
        if (rank != 0)
        {
            return rank;
        }

        return string.Compare(left.Title, right.Title, StringComparison.OrdinalIgnoreCase);
    }

    private static int Rank(Bin bin)
    {
        if (bin == null)
        {
            return 50;
        }

        if (bin.Trash)
        {
            return -1;
        }

        string id = bin.Slot ?? "";
        switch (id)
        {
            case "input": return 0;
            case "container": return 1;
            case "chill": return 2;
            case "freeze": return 3;
            case "output": return 30;
        }

        int tab = id.LastIndexOf(".tab", StringComparison.Ordinal);
        if (tab >= 0 && int.TryParse(id.Substring(tab + 4), out int page))
        {
            return 10 + page;
        }

        return 20;
    }

    private static Bin TrashBin()
    {
        try
        {
            EmporiumEntry shop = EmporiumEntry.Instance;
            if (shop == null || shop.trashcan == null)
            {
                return null;
            }

            return new Bin
            {
                Item = shop.trashcan,
                Inventory = Resolve(shop.trashcan),
                Trash = true
            };
        }
        catch
        {
            return null;
        }
    }

    private static void Take(GameInventory inventory, List<GameItem> result, HashSet<GameItem> seen)
    {
        if (inventory == null)
        {
            return;
        }

        List<GameItem> children = null;
        try
        {
            children = inventory.childItems;
        }
        catch
        {
        }

        if (children == null || children.Count > 4000)
        {
            return;
        }

        for (int i = 0; i < children.Count; i++)
        {
            Push(children[i], result, seen);
        }
    }

    private static void Push(GameItem item, List<GameItem> result, HashSet<GameItem> seen)
    {
        if (item != null && seen.Add(item))
        {
            result.Add(item);
        }
    }

    private static void MineBags(List<GameItem> result, HashSet<GameItem> seen)
    {
        List<GameItem> bags = new List<GameItem>();
        try
        {
            EmporiumEntry shop = EmporiumEntry.Instance;
            if (shop != null)
            {
                if (shop.selectedBackpack != null)
                {
                    bags.Add(shop.selectedBackpack);
                }

                Pocket(shop.afterhourPocketSlotInvLeft, bags);
                Pocket(shop.afterhourPocketSlotInvBackpack, bags);
                Pocket(shop.afterhourPocketSlotInvRight, bags);
            }
        }
        catch
        {
        }

        for (int i = 0; i < result.Count; i++)
        {
            if (IsBag(result[i]))
            {
                bags.Add(result[i]);
            }
        }

        for (int i = 0; i < allItems.Count; i++)
        {
            if (Owned(allItems[i]) && IsBag(allItems[i]))
            {
                bags.Add(allItems[i]);
            }
        }

        HashSet<GameItem> walked = new HashSet<GameItem>();
        for (int i = 0; i < bags.Count; i++)
        {
            Mine(bags[i], result, seen, walked, 0);
        }
    }

    private static void Pocket(GameSlotInventory slot, List<GameItem> bags)
    {
        try
        {
            if (slot != null && slot.childItem != null)
            {
                bags.Add(slot.childItem);
            }
        }
        catch
        {
        }
    }

    private static void Mine(GameItem bag, List<GameItem> result, HashSet<GameItem> seen, HashSet<GameItem> walked, int depth)
    {
        if (bag == null || depth > 2 || !walked.Add(bag) || !IsBag(bag))
        {
            return;
        }

        GameInventory inventory = Resolve(bag);
        if (inventory == null || Shielded(inventory))
        {
            return;
        }

        List<GameItem> children = null;
        try
        {
            children = inventory.childItems;
        }
        catch
        {
        }

        if (children == null || children.Count > 2000)
        {
            return;
        }

        for (int i = 0; i < children.Count; i++)
        {
            GameItem child = children[i];
            if (child == null)
            {
                continue;
            }

            Push(child, result, seen);
            if (IsBag(child))
            {
                Mine(child, result, seen, walked, depth + 1);
            }
        }
    }

    private static void TakeMachineOutputs(List<GameItem> result, HashSet<GameItem> seen)
    {
        for (int i = 0; i < allItems.Count; i++)
        {
            GameItem item = allItems[i];
            if (!Owned(item) || Retired(item) || (!IsMachine(item) && !HasTabs(item)))
            {
                continue;
            }

            List<Bin> slots = SlotsOf(item);
            for (int s = 0; s < slots.Count; s++)
            {
                Bin bin = slots[s];
                if (!CanSupply(bin))
                {
                    continue;
                }

                try
                {
                    if (bin.Inventory.IsRemoveLocked())
                    {
                        continue;
                    }
                }
                catch
                {
                    continue;
                }

                Take(bin.Inventory, result, seen);
            }
        }
    }

    private static void TakeSupply(List<GameItem> result, HashSet<GameItem> seen)
    {
        for (int i = 0; i < bins.Count; i++)
        {
            Bin bin = bins[i];
            if (bin.Trash || bin.Slot != null || bin.Inventory == null || !Owned(bin.Item))
            {
                continue;
            }

            if (!HasStorage(bin.Item) && !IdHasStorage(bin.Item) && !IsCage(bin.Item))
            {
                continue;
            }

            if (!CanSupply(bin))
            {
                continue;
            }

            try
            {
                if (!bin.Inventory.IsRemoveLocked())
                {
                    Take(bin.Inventory, result, seen);
                }
            }
            catch
            {
            }
        }
    }

    private static void TakeFeces(List<GameItem> result, HashSet<GameItem> seen)
    {
        if (!Trash.Feces)
        {
            return;
        }

        GameInventory trash = TrashInventory();
        for (int i = 0; i < allItems.Count; i++)
        {
            GameItem item = allItems[i];
            if (item == null || Retired(item) || !Owned(item) || !ItemFacts.HasTag(item, "ANIMAL_FECES_TAG") || InTrash(item, trash))
            {
                continue;
            }

            Push(item, result, seen);
        }
    }

    private static bool InTrash(GameItem item, GameInventory trash)
    {
        if (item == null || trash == null)
        {
            return false;
        }

        GameInventory inventory = null;
        try
        {
            inventory = item.parentInventory;
        }
        catch
        {
        }

        for (int i = 0; inventory != null && i < 16; i++)
        {
            if (inventory == trash)
            {
                return true;
            }

            GameItem parent = null;
            try
            {
                parent = inventory.GetParentItem();
            }
            catch
            {
            }

            if (parent == null)
            {
                break;
            }

            try
            {
                inventory = parent.parentInventory;
            }
            catch
            {
                inventory = null;
            }
        }

        return false;
    }

    internal static void VisitContents(GameItem owner, Action<GameItem> visit)
    {
        if (owner == null || visit == null)
        {
            return;
        }

        for (int i = 0; i < bins.Count; i++)
        {
            Bin bin = bins[i];
            if (bin.Item != owner || bin.Trash || bin.Inventory == null)
            {
                continue;
            }

            List<GameItem> children = null;
            try
            {
                children = bin.Inventory.childItems;
            }
            catch
            {
            }

            if (children == null)
            {
                continue;
            }

            int count = children.Count < 400 ? children.Count : 400;
            for (int c = 0; c < count; c++)
            {
                if (children[c] != null)
                {
                    visit(children[c]);
                }
            }
        }
    }

    internal static void VisitKnown(Action<GameItem> visit)
    {
        if (visit == null)
        {
            return;
        }

        for (int i = 0; i < allItems.Count; i++)
        {
            if (allItems[i] != null)
            {
                visit(allItems[i]);
            }
        }

        for (int i = 0; i < bins.Count; i++)
        {
            List<GameItem> children = null;
            try
            {
                children = bins[i].Inventory == null ? null : bins[i].Inventory.childItems;
            }
            catch
            {
            }

            if (children == null)
            {
                continue;
            }

            for (int c = 0; c < children.Count && c < 400; c++)
            {
                if (children[c] != null)
                {
                    visit(children[c]);
                }
            }
        }
    }
}
