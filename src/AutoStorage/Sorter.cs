using System;
using System.Collections.Generic;

namespace AutoStorage;

internal static class Sorter
{
    private sealed class Filter
    {
        internal readonly HashSet<string> tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        internal readonly List<string> ids = new List<string>();

        public int Count
        {
            get { return tags.Count + ids.Count; }
        }

        public static Filter Parse(string csv)
        {
            Filter filter = new Filter();
            List<string> parts = Rules.Parts(csv);
            for (int i = 0; i < parts.Count; i++)
            {
                string part = parts[i];
                if (part.StartsWith("id:", StringComparison.OrdinalIgnoreCase))
                {
                    string id = part.Substring(3).Trim();
                    if (id.Length > 0)
                    {
                        filter.ids.Add(id);
                    }
                }
                else
                {
                    filter.tags.Add(part);
                }
            }

            return filter;
        }

        public bool Matches(ItemFacts facts)
        {
            foreach (string tag in tags)
            {
                if (facts.Types.Contains(tag))
                {
                    return true;
                }
            }

            for (int i = 0; i < ids.Count; i++)
            {
                if (string.Equals(facts.Id, ids[i], StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }

    private sealed class Target
    {
        public Bin Bin;
        public Rule Rule;
        public Filter Allow;
        public Filter Block;
        public bool AllowEmpty = true;
    }

    private sealed class Index
    {
        private readonly List<Target> targets;
        private readonly Dictionary<string, List<int>> byTag = new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, List<int>> byId = new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);
        private readonly List<int> emptyAllow = new List<int>();
        private readonly List<int> candidates = new List<int>();
        private readonly int[] seen;
        private int stamp;

        public bool SawFull;

        public Index(List<Target> targets)
        {
            this.targets = targets;
            seen = new int[targets.Count];
            for (int i = 0; i < targets.Count; i++)
            {
                Filter allow = targets[i].Allow;
                if (allow.Count == 0 && targets[i].AllowEmpty)
                {
                    emptyAllow.Add(i);
                }

                foreach (string tag in allow.tags)
                {
                    Add(byTag, tag, i);
                }

                for (int id = 0; id < allow.ids.Count; id++)
                {
                    Add(byId, allow.ids[id], i);
                }
            }
        }

        public Bin Pick(GameItem item, ItemFacts facts, bool stolen)
        {
            SawFull = false;
            stamp++;
            candidates.Clear();
            AddRange(emptyAllow);
            foreach (string type in facts.Types)
            {
                if (byTag.TryGetValue(type, out List<int> tags))
                {
                    AddRange(tags);
                }
            }

            if (byId.TryGetValue(facts.Id, out List<int> ids))
            {
                AddRange(ids);
            }

            candidates.Sort(Compare);
            for (int i = 0; i < candidates.Count; i++)
            {
                Target target = targets[candidates[i]];
                if (target.Bin.Inventory == null || Same(target.Bin.Inventory, Parent(item)))
                {
                    continue;
                }

                if (stolen && !target.Rule.AcceptStolen)
                {
                    continue;
                }

                if (facts.Contraband && !target.Rule.AcceptContraband)
                {
                    continue;
                }

                if (target.Block.Matches(facts))
                {
                    continue;
                }

                if (Mover.CanAccept(target.Bin.Inventory, item))
                {
                    return target.Bin;
                }

                SawFull = true;
            }

            return null;
        }

        private int Compare(int left, int right)
        {
            int priority = targets[right].Rule.Priority.CompareTo(targets[left].Rule.Priority);
            return priority != 0 ? priority : left.CompareTo(right);
        }

        private void Add(Dictionary<string, List<int>> map, string key, int index)
        {
            if (!map.TryGetValue(key, out List<int> list))
            {
                list = new List<int>();
                map[key] = list;
            }

            list.Add(index);
        }

        private void AddRange(List<int> indices)
        {
            for (int i = 0; i < indices.Count; i++)
            {
                int index = indices[i];
                if (seen[index] != stamp)
                {
                    seen[index] = stamp;
                    candidates.Add(index);
                }
            }
        }
    }

    private static bool running;

    internal static void Run()
    {
        if (running)
        {
            return;
        }

        running = true;
        try
        {
            Rules.Load();
            if (!Rules.GlobalEnabled)
            {
                string key = Hotkeys.Current(Hotkeys.Toggle);
                Text.Notify(string.IsNullOrEmpty(key) ? Text.Get("status_off") : Text.Get("status_off") + "  " + key);
                return;
            }

            Catalog.Collect(true);
            List<Target> targets = BuildTargets();
            if (targets.Count == 0 && !Trash.Feces)
            {
                Text.Notify(Text.Get("status_none"));
                return;
            }

            List<Target> ordinary = new List<Target>();
            List<Target> machines = new List<Target>();
            for (int i = 0; i < targets.Count; i++)
            {
                ordinary.Add(targets[i]);
                if (!string.IsNullOrEmpty(targets[i].Bin.Slot) && !targets[i].Bin.StoragePage)
                {
                    machines.Add(targets[i]);
                }
            }

            Index machineIndex = new Index(machines);
            Index ordinaryIndex = new Index(ordinary);
            GameInventory trash = Trash.Feces ? Catalog.TrashInventory() : null;
            HashSet<GameInventory> storageSources = new HashSet<GameInventory>();
            HashSet<GameInventory> cageSources = new HashSet<GameInventory>();
            IReadOnlyList<Bin> bins = Catalog.Bins;
            for (int i = 0; i < bins.Count; i++)
            {
                Bin bin = bins[i];
                if (bin.Inventory == null || bin.Trash)
                {
                    continue;
                }

                if (Catalog.CanSupply(bin) && bin.Slot == null && Catalog.IsCage(bin.Item))
                {
                    cageSources.Add(bin.Inventory);
                }

                if (Catalog.CanSupply(bin) && bin.Slot == null && (ItemFacts.HasTag(bin.Item, "STORAGE") || Catalog.IdOf(bin.Item).IndexOf("storage", StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    storageSources.Add(bin.Inventory);
                }

                if (bin.StoragePage && Catalog.CanSupply(bin))
                {
                    storageSources.Add(bin.Inventory);
                }
            }

            List<GameItem> sources = Catalog.SourceItems();
            int moved = 0;
            int full = 0;
            for (int i = 0; i < sources.Count; i++)
            {
                GameItem item = sources[i];
                if (item == null || ItemPinHelper.IsPinned(item) || DebrisHelper.IsDebris(item))
                {
                    continue;
                }

                if (Catalog.IsBag(item) || Catalog.IsMachine(item) || Catalog.IsContainerItem(item) || !Catalog.Owned(item))
                {
                    continue;
                }

                ItemFacts facts = ItemFacts.Of(item);
                if (trash != null && ItemFacts.HasTag(item, "ANIMAL_FECES_TAG"))
                {
                    if (Mover.Move(item, trash))
                    {
                        moved++;
                    }

                    continue;
                }

                bool stolen = Catalog.Stolen(item);
                GameInventory parent = Parent(item);
                bool fromStorage = parent != null && storageSources.Contains(parent);
                bool fromCage = parent != null && cageSources.Contains(parent);
                Bin machine = fromCage ? null : machineIndex.Pick(item, facts, stolen);
                if (machine != null && Mover.Move(item, machine.Inventory))
                {
                    moved++;
                    continue;
                }

                if (fromStorage)
                {
                    if (machine == null && machineIndex.SawFull)
                    {
                        full++;
                    }

                    continue;
                }

                Bin any = ordinaryIndex.Pick(item, facts, stolen);
                if (any != null && Mover.Move(item, any.Inventory))
                {
                    moved++;
                }
                else if (any == null && ordinaryIndex.SawFull)
                {
                    full++;
                }
            }

            for (int i = 0; i < targets.Count; i++)
            {
                try
                {
                    if (targets[i].Bin.Inventory != null)
                    {
                        targets[i].Bin.Inventory.Validate();
                    }
                }
                catch
                {
                }
            }

            if (moved > 0)
            {
                Sound.Play();
            }

            string message = moved > 0
                ? Text.Get("status_moved") + " " + moved
                : Text.Get("status_idle");
            if (moved > 0 && full > 0)
            {
                message += "    " + Text.Get("status_full") + " " + full;
            }

            Text.Notify(message);
            ModEntry.Log?.Log(message);
        }
        catch (Exception ex)
        {
            ModEntry.Log?.Warning("Sort failed: " + ex.Message);
        }
        finally
        {
            running = false;
        }
    }

    internal static void DeliverRewards()
    {
        GameInventory trash = Catalog.TrashInventory();
        if (trash == null || trash.childItems == null)
        {
            return;
        }

        Catalog.Collect(true);
        Index index = new Index(BuildTargets());
        GameInventory back = null;
        GameInventory front = null;
        try
        {
            EmporiumEntry shop = EmporiumEntry.Instance;
            if (shop != null)
            {
                back = shop.backInvinvElement as GameInventory;
                front = shop.invElement as GameInventory;
            }
        }
        catch
        {
        }

        List<GameItem> items = new List<GameItem>(trash.childItems);
        int moved = 0;
        for (int i = 0; i < items.Count; i++)
        {
            GameItem item = items[i];
            if (item == null || !Catalog.Owned(item) || !Trash.IsReward(item))
            {
                continue;
            }

            Trash.Forget(item);
            if (ItemPinHelper.IsPinned(item) || DebrisHelper.IsDebris(item) || Catalog.IsBag(item) || Catalog.IsMachine(item) || Catalog.IsContainerItem(item))
            {
                continue;
            }

            try
            {
                GeneralHelper.SetItemOwned(item, true);
            }
            catch
            {
            }

            ItemFacts facts = ItemFacts.Of(item);
            bool stolen = Catalog.Stolen(item);
            if (back != null && Mover.CanAccept(back, item) && Mover.Move(item, back))
            {
                moved++;
                continue;
            }

            if (front != null && Mover.CanAccept(front, item) && Mover.Move(item, front))
            {
                moved++;
                continue;
            }

            Bin bin = index.Pick(item, facts, stolen);
            if (bin != null && Mover.Move(item, bin.Inventory))
            {
                moved++;
            }
        }

        if (moved > 0)
        {
            ModEntry.Log?.Log("Nightly pickup moved " + moved + ".");
        }
    }

    private static List<Target> BuildTargets()
    {
        List<Target> list = new List<Target>();
        IReadOnlyList<Bin> bins = Catalog.Bins;
        for (int i = 0; i < bins.Count; i++)
        {
            Bin bin = bins[i];
            if (bin.Trash || bin.Missing || bin.Inventory == null || !Catalog.CanReceive(bin))
            {
                continue;
            }

            Rule rule = Rules.Get(bin.Item, bin.Slot);
            if (!rule.Enabled)
            {
                continue;
            }

            list.Add(new Target
            {
                Bin = bin,
                Rule = rule,
                Allow = Filter.Parse(rule.Allow),
                Block = Filter.Parse(rule.Block),
                AllowEmpty = !Catalog.IsFridge(bin.Item)
            });
        }

        return list;
    }

    private static GameInventory Parent(GameItem item)
    {
        try
        {
            return item == null ? null : item.parentInventory;
        }
        catch
        {
            return null;
        }
    }

    private static bool Same(GameInventory left, GameInventory right)
    {
        return left != null && left == right;
    }
}
