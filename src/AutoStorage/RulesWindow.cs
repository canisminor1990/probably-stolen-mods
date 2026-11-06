using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AutoStorage;

internal static class RulesWindow
{
    private const string WindowId = "storage_rules.rules";

    private const float CardWidth = 112f;
    private const float CardHeight = 128f;
    private const float IconBox = 56f;

    private static readonly Color Idle = new Color(0.72f, 0.70f, 0.66f, 1f);
    private static readonly Color Allow = new Color(0.45f, 0.72f, 0.40f, 1f);
    private static readonly Color Block = new Color(0.82f, 0.38f, 0.34f, 1f);
    private static readonly Color Ink = new Color(0.16f, 0.14f, 0.12f, 1f);
    private static readonly Color TitleInk = new Color(0.95f, 0.92f, 0.86f, 1f);
    private static readonly Color QuietInk = new Color(0.95f, 0.92f, 0.86f, 0.62f);
    private static readonly Color FaintInk = new Color(0.95f, 0.92f, 0.86f, 0.4f);

    private sealed class Chip
    {
        public string Part;
        public string Label;
        public bool Item;
        public Sprite Sprite;
        public CustomUIElement Button;
        public GameObject Root;
        public Image Plate;
        public TextMeshProUGUI Caption;
    }

    private static CustomUIWindow window;
    private static RectTransform detailHost;
    private static CustomUIElement status;
    private static CustomUIElement summary;
    private static CustomUIElement hint;
    private static CustomUIElement priorityLabel;
    private static RectTransform chosenHost;
    private static TMP_FontAsset font;
    private static Sprite plateSprite;
    private static readonly List<Chip> chips = new List<Chip>();
    private static Rule editing;
    private static Rule clipboard;
    private static Bin selected;
    private static string ruleQuery = "";
    private static bool onlyHere = true;
    private static bool building;
    private static float windowWidth = 1040f;
    private static float windowHeight = 760f;

    internal static void Open(GameItem item)
    {
        try
        {
            if (item == null || CustomUIManager.Instance == null)
            {
                return;
            }

            Catalog.Collect(true);
            Bin bin = Catalog.Prefer(item);
            if (bin == null)
            {
                Text.Notify(Text.Get("no_bins"));
                return;
            }

            if (window != null && window.IsAlive && selected != null && selected.Item == bin.Item && selected.Trash == bin.Trash)
            {
                window.BringToFront();
                window.Show();
                if (!Same(selected, bin))
                {
                    Select(bin);
                }

                return;
            }

            Create(bin);
        }
        catch (Exception ex)
        {
            ModEntry.Log?.Warning("Storage window failed: " + ex.Message);
        }
    }

    internal static void Close()
    {
        if (window != null && window.IsAlive)
        {
            window.Close();
        }

        window = null;
        detailHost = null;
        chips.Clear();
        editing = null;
        selected = null;
    }

    internal static void SetStatus(string message)
    {
        if (status != null && status.IsAlive)
        {
            status.SetText(message ?? "");
        }
    }

    private static void Create(Bin bin)
    {
        Close();
        windowWidth = Mathf.Clamp(Screen.width - 48f, 1180f, 1560f);
        windowHeight = Mathf.Clamp(Screen.height - 64f, 680f, 940f);
        CustomUIBuilder opener = CustomUIManager.Window(WindowId, bin.Title, "paper");
        if (opener == null)
        {
            return;
        }

        opener.SetSize(windowWidth, windowHeight).SetDraggable(true).SetCloseOnEscape(true).OnClose(Closed);
        window = opener.Handle;
        if (window == null || !window.IsAlive)
        {
            return;
        }

        window.SetTitle(bin.Title);
        window.Center();
        window.Show();
        detailHost = window.ContentRoot;
        selected = bin;
        Fill();
    }

    private static void Closed()
    {
        window = null;
        detailHost = null;
        status = null;
        summary = null;
        hint = null;
        priorityLabel = null;
        chosenHost = null;
        chips.Clear();
        editing = null;
        selected = null;
    }

    private static void Fill()
    {
        if (detailHost == null || window == null || !window.IsAlive)
        {
            return;
        }

        building = true;
        editing = LoadEditing(selected);
        chips.Clear();
        summary = null;
        hint = null;
        priorityLabel = null;
        chosenHost = null;
        status = null;
        CustomUIBuilder.ClearContainer(detailHost);
        PrepareHost(detailHost);
        CustomUIBuilder builder = new CustomUIBuilder(CustomUIManager.Instance, window, detailHost);
        builder.BeginColumn(0f, TextAnchor.UpperLeft);
        Stretch(builder.CurrentContainer);
        if (selected == null)
        {
            builder.AddLabel(Text.Get("no_bins"), null);
        }
        else if (selected.Trash)
        {
            FillTrash(builder);
        }
        else if (selected.Missing)
        {
            builder.AddLabel(Text.Get("open_machine"), null);
        }
        else
        {
            FillRules(builder);
        }

        builder.End();
        building = false;
        ApplyFilter();
        if (detailHost != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(detailHost);
        }
    }

    private static void FillTrash(CustomUIBuilder builder)
    {
        builder.AddLabel(Text.Get("trash_hint"), null);
        builder.AddToggle(Text.Get("trash_feces"), Trash.Feces, value => Trash.SetFeces(value), "feces").WithTooltip(Text.Get("tip_feces"));
        builder.AddToggle(Text.Get("trash_nightly"), Trash.Nightly, value => Trash.SetNightly(value), "nightly").WithTooltip(Text.Get("tip_nightly"));
    }

    private static void FillRules(CustomUIBuilder builder)
    {
        bool outputOnly = selected.DefaultSource && !selected.StoragePage;
        builder.BeginRow(16f, TextAnchor.UpperLeft);
        Stretch(builder.CurrentContainer);
        builder.BeginColumn(10f, TextAnchor.UpperLeft);
        Pane(builder.CurrentContainer, 400f, false);
        WriteControls(builder, outputOnly);
        if (!outputOnly)
        {
            builder.AddSpace(4f);
            builder.BeginRow(8f, TextAnchor.MiddleLeft);
            Section(builder, Text.Get("chosen_header"));
            builder.AddFlexibleSpace();
            builder.AddLabel(Summary(), "summary");
            summary = builder.Last;
            Dress(summary, 13f, FontStyles.Normal, QuietInk, 0.4f);
            builder.End();
            BeginFillScroll(builder, 120f);
            chosenHost = builder.CurrentContainer;
            builder.End();
            builder.AddSeparator(null, 2);
            builder.BeginRow(8f, TextAnchor.MiddleLeft);
            builder.AddButton(Text.Get("clear_rules"), ClearRules, "clear");
            builder.AddButton(Text.Get("copy_rules"), CopyRules, "copy");
            builder.AddButton(Text.Get("paste_rules"), PasteRules, "paste");
            builder.End();
            builder.AddButton(Text.Get("sort_now") + "  " + Hotkeys.Current(Hotkeys.Sort), () => Sorter.Run(), "sort_now");
            Size(builder.Last, 0f, 36f);
            builder.Last.SetFlexible(1f, -1f);
            builder.AddLabel("", "status");
            status = builder.Last;
            Dress(status, 12f, FontStyles.Normal, QuietInk, 0f);
        }

        builder.End();
        if (!outputOnly)
        {
            builder.BeginColumn(8f, TextAnchor.UpperLeft);
            Pane(builder.CurrentContainer, 640f, true);
            builder.AddInput(Text.Get("search_rules"), ruleQuery, OnSearch, "rule_search", OnSearch);
            Size(builder.Last, 0f, 34f);
            builder.Last.SetFlexible(1f, -1f);
            builder.AddToggle(Text.Get("only_here"), onlyHere, OnOnlyHere, "only_here").WithTooltip(Text.Get("tip_only_here"));
            Note(builder, Text.Get("hint_cycle"), "hint");
            hint = builder.Last;
            BuildChoices(builder);
            builder.End();
        }

        builder.End();
        if (!outputOnly)
        {
            RefreshChosen();
        }
    }

    private static void WriteControls(CustomUIBuilder builder, bool outputOnly)
    {
        List<Bin> siblings = Siblings(selected);
        if (siblings.Count > 1)
        {
            builder.BeginRow(6f, TextAnchor.MiddleLeft);
            for (int i = 0; i < siblings.Count; i++)
            {
                Bin sibling = siblings[i];
                string caption = string.IsNullOrEmpty(sibling.Label) ? sibling.Title : sibling.Label;
                builder.AddButton(caption, () => Select(sibling), null);
                Size(builder.Last, 0f, 30f);
                if (Same(sibling, selected))
                {
                    builder.Last.SetImageColor(new Color(0.86f, 0.78f, 0.62f, 1f));
                    builder.Last.SetTextColor(Ink);
                }
            }

            builder.End();
            builder.AddSpace(6f);
        }

        Section(builder, Text.Get("section_slot"));
        builder.AddToggle(Text.Get("enable"), editing.Enabled, OnEnabled, "enable").WithTooltip(Text.Get("tip_enable"));
        builder.AddSpace(8f);
        Section(builder, Text.Get("section_flow"));
        if (!outputOnly)
        {
            builder.AddToggle(Text.Get("receive"), Catalog.CanReceive(selected), OnReceive, "receive").WithTooltip(Text.Get("tip_receive"));
        }
        else
        {
            Note(builder, Text.Get("output_note"), null);
        }

        builder.AddToggle(Text.Get("supply"), Catalog.CanSupply(selected), OnSupply, "supply").WithTooltip(Text.Get("tip_supply"));
        if (outputOnly)
        {
            return;
        }

        builder.AddSpace(8f);
        Section(builder, Text.Get("section_limit"));
        builder.BeginRow(8f, TextAnchor.MiddleLeft);
        builder.AddToggle(Text.Get("accept_stolen"), editing.AcceptStolen, value => SaveFlag(() => editing.AcceptStolen = value), "stolen").WithTooltip(Text.Get("tip_stolen"));
        builder.AddToggle(Text.Get("accept_contraband"), editing.AcceptContraband, value => SaveFlag(() => editing.AcceptContraband = value), "contraband").WithTooltip(Text.Get("tip_contraband"));
        builder.End();
        builder.BeginRow(8f, TextAnchor.MiddleLeft);
        builder.AddLabel(Text.Get("priority"), null);
        Dress(builder.Last, 13f, FontStyles.Normal, QuietInk, 0.4f);
        builder.AddButton("−", () => Step(-1), "priority_down");
        Remember(builder);
        Size(builder.Last, 36f, 30f);
        builder.AddLabel(editing.Priority.ToString(), "priority_value");
        priorityLabel = builder.Last;
        Dress(priorityLabel, 16f, FontStyles.Bold, TitleInk, 0f);
        builder.AddButton("+", () => Step(1), "priority_up");
        Size(builder.Last, 36f, 30f);
        builder.End();
        Note(builder, Hint(), null);
    }

    private static void BuildChoices(CustomUIBuilder builder)
    {
        List<Chip> types = new List<Chip>();
        List<Chip> items = new List<Chip>();
        Dictionary<string, GameItem> samples = new Dictionary<string, GameItem>(StringComparer.OrdinalIgnoreCase);
        HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        Action<GameItem> take = item =>
        {
            ItemFacts facts = ItemFacts.Of(item);
            foreach (string type in facts.Types)
            {
                AddType(types, seen, type);
            }

            if (!string.IsNullOrEmpty(facts.Id) && samples.ContainsKey(facts.Id) == false)
            {
                samples[facts.Id] = item;
            }

            AddItem(items, seen, facts.Id, NameOf(item, facts.Id), IconOf(item));
        };
        if (onlyHere)
        {
            Catalog.VisitContents(selected.Item, take);
        }
        else
        {
            Catalog.VisitKnown(take);
            RememberTypes(types, seen, editing.Allow);
            RememberTypes(types, seen, editing.Block);
            RememberItems(items, seen, samples, editing.Allow);
            RememberItems(items, seen, samples, editing.Block);
        }
        types.Sort((left, right) => string.Compare(left.Label, right.Label, StringComparison.OrdinalIgnoreCase));
        items.Sort((left, right) => string.Compare(left.Label, right.Label, StringComparison.OrdinalIgnoreCase));
        if (items.Count > 240)
        {
            items.RemoveRange(240, items.Count - 240);
        }

        BeginFillScroll(builder, 160f);
        builder.BeginColumn(12f, TextAnchor.UpperLeft);
        if (types.Count == 0 && items.Count == 0)
        {
            Note(builder, Text.Get(onlyHere ? "picker_empty" : "chosen_empty"), null);
        }
        else
        {
            if (types.Count > 0)
            {
                Kicker(builder, Text.Get("types_header"));
                FlowTags(builder, types);
            }

            if (items.Count > 0)
            {
                Kicker(builder, Text.Get("items_header"));
                FlowItems(builder, items);
            }
        }

        builder.End();
        builder.End();
    }

    private static void FlowTags(CustomUIBuilder builder, List<Chip> types)
    {
        if (types.Count == 0)
        {
            return;
        }

        float limit = Mathf.Max(280f, windowWidth - 500f);
        float used = limit;
        for (int i = 0; i < types.Count; i++)
        {
            Chip chip = types[i];
            float width = TagWidth(chip.Label);
            if (used + width > limit)
            {
                if (i > 0)
                {
                    builder.End();
                }

                builder.BeginRow(6f, TextAnchor.MiddleLeft);
                used = 0f;
            }

            builder.AddButton(chip.Label, () => Cycle(chip), null).WithTooltip(chip.Label);
            chip.Button = builder.Last;
            Size(chip.Button, width, 28f);
            Quiet(chip.Button);
            if (plateSprite == null && chip.Button.image != null)
            {
                plateSprite = chip.Button.image.sprite;
            }

            if (font == null && chip.Button.text != null)
            {
                font = chip.Button.text.font;
            }

            Paint(chip);
            chips.Add(chip);
            used += width + 6f;
        }

        builder.End();
    }

    private static void FlowItems(CustomUIBuilder builder, List<Chip> items)
    {
        PlaceItems(builder, items, CardColumns(windowWidth - 460f), CardWidth, CardHeight, true);
    }

    private static void PlaceItems(CustomUIBuilder builder, List<Chip> items, int columns, float cellWidth, float cellHeight, bool store)
    {
        if (items.Count == 0)
        {
            return;
        }

        int rows = (items.Count + columns - 1) / columns;
        builder.BeginGrid(columns, cellWidth, cellHeight, 8f);
        LayoutElement grid = builder.CurrentContainer.GetComponent<LayoutElement>();
        if (grid == null)
        {
            grid = builder.CurrentContainer.gameObject.AddComponent<LayoutElement>();
        }

        grid.minHeight = rows * (cellHeight + 8f);
        grid.preferredHeight = rows * (cellHeight + 8f);
        RectTransform parent = builder.CurrentContainer;
        for (int i = 0; i < items.Count; i++)
        {
            MountCell(parent, items[i], store);
        }

        builder.End();
    }

    private static void MountCell(RectTransform parent, Chip chip, bool store)
    {
        GameObject root = new GameObject("ItemCell", typeof(RectTransform), typeof(Image), typeof(Button), typeof(VerticalLayoutGroup));
        root.transform.SetParent(parent, false);
        Image plate = root.GetComponent<Image>();
        plate.sprite = plateSprite;
        plate.type = plateSprite == null ? Image.Type.Simple : Image.Type.Sliced;
        plate.color = Idle;
        Button button = root.GetComponent<Button>();
        button.targetGraphic = plate;
        button.transition = Selectable.Transition.None;
        Chip captured = chip;
        button.onClick.AddListener(() => Cycle(captured));
        VerticalLayoutGroup column = root.GetComponent<VerticalLayoutGroup>();
        column.padding = new RectOffset(6, 6, 8, 6);
        column.spacing = 4f;
        column.childAlignment = TextAnchor.UpperCenter;
        column.childControlWidth = true;
        column.childControlHeight = true;
        column.childForceExpandWidth = false;
        column.childForceExpandHeight = false;
        GameObject iconFrame = new GameObject("IconFrame", typeof(RectTransform), typeof(LayoutElement));
        iconFrame.transform.SetParent(root.transform, false);
        LayoutElement iconLayout = iconFrame.GetComponent<LayoutElement>();
        iconLayout.minWidth = IconBox;
        iconLayout.minHeight = IconBox;
        iconLayout.preferredWidth = IconBox;
        iconLayout.preferredHeight = IconBox;
        iconLayout.flexibleWidth = 0f;
        iconLayout.flexibleHeight = 0f;
        GameObject iconObject = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        iconObject.transform.SetParent(iconFrame.transform, false);
        RectTransform iconRect = iconObject.GetComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0.5f, 0.5f);
        iconRect.anchorMax = new Vector2(0.5f, 0.5f);
        iconRect.pivot = new Vector2(0.5f, 0.5f);
        iconRect.sizeDelta = new Vector2(IconBox, IconBox);
        Image icon = iconObject.GetComponent<Image>();
        icon.sprite = chip.Sprite;
        icon.preserveAspect = true;
        icon.raycastTarget = false;
        icon.color = Color.white;
        icon.enabled = chip.Sprite != null;
        GameObject textObject = new GameObject("Name", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
        textObject.transform.SetParent(root.transform, false);
        LayoutElement textLayout = textObject.GetComponent<LayoutElement>();
        textLayout.minHeight = 32f;
        textLayout.preferredHeight = 36f;
        textLayout.flexibleWidth = 1f;
        textLayout.flexibleHeight = 0f;
        TextMeshProUGUI caption = textObject.GetComponent<TextMeshProUGUI>();
        caption.text = chip.Label;
        if (font != null)
        {
            caption.font = font;
        }

        caption.fontSize = 12f;
        caption.maxVisibleLines = 2;
        caption.alignment = TextAlignmentOptions.Center;
        caption.enableWordWrapping = true;
        caption.overflowMode = TextOverflowModes.Ellipsis;
        caption.raycastTarget = false;
        caption.color = Ink;
        CustomUITooltipTrigger tooltip = root.AddComponent<CustomUITooltipTrigger>();
        tooltip.tooltipText = chip.Label;
        if (store)
        {
            chip.Root = root;
            chip.Plate = plate;
            chip.Caption = caption;
            Paint(chip);
            chips.Add(chip);
        }
        else
        {
            PaintPlate(plate, caption, chip);
        }
    }

    private static void AddType(List<Chip> group, HashSet<string> seen, string key)
    {
        if (string.IsNullOrEmpty(key) || !seen.Add(key))
        {
            return;
        }

        string label = ItemFacts.TypeName(key);
        if (string.IsNullOrEmpty(label))
        {
            seen.Remove(key);
            return;
        }

        group.Add(new Chip { Part = key, Label = label });
    }

    private static void AddItem(List<Chip> group, HashSet<string> seen, string id, string label, Sprite sprite)
    {
        if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(label))
        {
            return;
        }

        string part = "id:" + id;
        if (!seen.Add(part))
        {
            return;
        }

        group.Add(new Chip { Part = part, Label = label, Item = true, Sprite = sprite });
    }

    private static void RememberTypes(List<Chip> group, HashSet<string> seen, string csv)
    {
        List<string> parts = Rules.Parts(csv);
        for (int i = 0; i < parts.Count; i++)
        {
            if (!parts[i].StartsWith("id:", StringComparison.OrdinalIgnoreCase))
            {
                AddType(group, seen, parts[i]);
            }
        }
    }

    private static void RememberItems(List<Chip> group, HashSet<string> seen, Dictionary<string, GameItem> samples, string csv)
    {
        List<string> parts = Rules.Parts(csv);
        for (int i = 0; i < parts.Count; i++)
        {
            string part = parts[i];
            if (!part.StartsWith("id:", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string id = part.Substring(3).Trim();
            if (seen.Contains(part))
            {
                continue;
            }

            GameItem sample;
            samples.TryGetValue(id, out sample);
            Sprite sprite = sample != null ? IconOf(sample) : IconForId(id);
            string label = sample != null ? NameOf(sample, id) : ItemFacts.ItemName(id);
            if (string.IsNullOrEmpty(label))
            {
                label = id;
            }

            AddItem(group, seen, id, label, sprite);
        }
    }

    private static void Select(Bin bin)
    {
        if (building || bin == null)
        {
            return;
        }

        selected = bin;
        if (window != null && window.IsAlive)
        {
            window.SetTitle(bin.Title);
        }

        Fill();
    }

    private static void OnEnabled(bool value)
    {
        if (building || editing == null || selected == null)
        {
            return;
        }

        editing.Enabled = value;
        Rules.Save(selected.Item, selected.Slot, editing);
    }

    private static void OnReceive(bool value)
    {
        if (building || selected == null)
        {
            return;
        }

        bool supply = Catalog.CanSupply(selected);
        if (value && !selected.StoragePage)
        {
            supply = false;
        }

        Rules.SetRoles(selected.Item, selected.Slot, value, supply);
        Fill();
    }

    private static void OnSupply(bool value)
    {
        if (building || selected == null)
        {
            return;
        }

        bool receive = Catalog.CanReceive(selected);
        if (value && !selected.StoragePage && !selected.DefaultSource)
        {
            receive = false;
        }

        Rules.SetRoles(selected.Item, selected.Slot, selected.DefaultSource && !selected.StoragePage ? (bool?)null : receive, value);
        Fill();
    }

    private static void SaveFlag(Action change)
    {
        if (building || editing == null || selected == null)
        {
            return;
        }

        change();
        Rules.Save(selected.Item, selected.Slot, editing);
        if (summary != null && summary.IsAlive)
        {
            summary.SetText(Summary());
        }
    }

    private static void Step(int delta)
    {
        if (building || editing == null || selected == null)
        {
            return;
        }

        editing.Priority += delta;
        Rules.Save(selected.Item, selected.Slot, editing);
        if (priorityLabel != null && priorityLabel.IsAlive)
        {
            priorityLabel.SetText(editing.Priority.ToString());
        }

        if (summary != null && summary.IsAlive)
        {
            summary.SetText(Summary());
        }
    }

    private static void OnOnlyHere(bool value)
    {
        if (building || value == onlyHere)
        {
            return;
        }

        onlyHere = value;
        Fill();
    }

    private static void OnSearch(string value)
    {
        if (building)
        {
            return;
        }

        ruleQuery = value ?? "";
        ApplyFilter();
    }

    private static void Cycle(Chip chip)
    {
        if (building || editing == null || selected == null || chip == null)
        {
            return;
        }

        if (Rules.Has(editing.Block, chip.Part))
        {
            editing.Block = Rules.Remove(editing.Block, chip.Part);
        }
        else if (Rules.Has(editing.Allow, chip.Part))
        {
            editing.Allow = Rules.Remove(editing.Allow, chip.Part);
            editing.Block = Rules.Add(editing.Block, chip.Part);
        }
        else
        {
            editing.Block = Rules.Remove(editing.Block, chip.Part);
            editing.Allow = Rules.Add(editing.Allow, chip.Part);
        }

        Rules.Save(selected.Item, selected.Slot, editing);
        Paint(chip);
        RefreshChosen();
        if (summary != null && summary.IsAlive)
        {
            summary.SetText(Summary());
        }
    }

    private static void ClearRules()
    {
        if (editing == null || selected == null)
        {
            return;
        }

        editing.Allow = "";
        editing.Block = "";
        Rules.Save(selected.Item, selected.Slot, editing);
        PaintAll();
        RefreshChosen();
        if (summary != null && summary.IsAlive)
        {
            summary.SetText(Summary());
        }

        SetStatus(Text.Get("status_cleared"));
    }

    private static void CopyRules()
    {
        if (editing == null)
        {
            return;
        }

        clipboard = editing.Copy();
        SetStatus(Text.Get("status_copied"));
    }

    private static void PasteRules()
    {
        if (clipboard == null || selected == null || selected.Trash || selected.Missing)
        {
            return;
        }

        Rule pasted = clipboard.Copy();
        pasted.Receive = editing == null ? null : editing.Receive;
        pasted.Supply = editing == null ? null : editing.Supply;
        Rules.Save(selected.Item, selected.Slot, pasted);
        Fill();
        SetStatus(Text.Get("status_pasted"));
    }

    private static void ApplyFilter()
    {
        string query = ruleQuery.Trim();
        for (int i = 0; i < chips.Count; i++)
        {
            Chip chip = chips[i];
            bool alive = (chip.Button != null && chip.Button.IsAlive) || chip.Root != null;
            if (!alive)
            {
                continue;
            }

            bool match = string.IsNullOrEmpty(query) || (chip.Label != null && chip.Label.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0);
            if (chip.Button != null && chip.Button.IsAlive)
            {
                chip.Button.SetVisible(match);
            }

            if (chip.Root != null)
            {
                chip.Root.SetActive(match);
            }
        }
    }

    private static void PaintAll()
    {
        for (int i = 0; i < chips.Count; i++)
        {
            Paint(chips[i]);
        }
    }

    private static void Paint(Chip chip)
    {
        if (chip == null || editing == null)
        {
            return;
        }

        Color wash = Tone(chip);
        if (chip.Button != null && chip.Button.IsAlive)
        {
            chip.Button.SetText(chip.Label);
            chip.Button.SetTextColor(Ink);
            chip.Button.SetImageColor(wash);
            chip.Button.SetTooltip(Tip(chip));
        }

        if (chip.Plate != null)
        {
            chip.Plate.color = wash;
            if (chip.Caption != null)
            {
                chip.Caption.color = Ink;
            }
        }
    }

    private static void PaintPlate(Image plate, TextMeshProUGUI caption, Chip chip)
    {
        if (plate != null)
        {
            plate.color = Tone(chip);
        }

        if (caption != null)
        {
            caption.color = Ink;
        }
    }

    private static Color Tone(Chip chip)
    {
        if (chip == null || editing == null)
        {
            return Idle;
        }

        if (Rules.Has(editing.Block, chip.Part))
        {
            return Block;
        }

        if (Rules.Has(editing.Allow, chip.Part))
        {
            return Allow;
        }

        return Idle;
    }

    private static string Tip(Chip chip)
    {
        if (chip == null)
        {
            return "";
        }

        string state = "";
        if (editing != null && Rules.Has(editing.Block, chip.Part))
        {
            state = Text.Get("word_block");
        }
        else if (editing != null && Rules.Has(editing.Allow, chip.Part))
        {
            state = Text.Get("word_allow");
        }

        return string.IsNullOrEmpty(state) ? chip.Label : chip.Label + "\n" + state;
    }

    private static void RefreshChosen()
    {
        if (chosenHost == null || window == null || !window.IsAlive)
        {
            return;
        }

        while (chosenHost.childCount > 0)
        {
            UnityEngine.Object.DestroyImmediate(chosenHost.GetChild(0).gameObject);
        }

        if (editing == null)
        {
            return;
        }

        Dictionary<string, Chip> known = new Dictionary<string, Chip>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < chips.Count; i++)
        {
            Chip chip = chips[i];
            if (chip != null && !string.IsNullOrEmpty(chip.Part))
            {
                known[chip.Part] = chip;
            }
        }

        List<Chip> tags = new List<Chip>();
        List<Chip> items = new List<Chip>();
        HashSet<string> marked = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        CollectChosen(known, marked, tags, items, editing.Allow);
        CollectChosen(known, marked, tags, items, editing.Block);

        CustomUIBuilder builder = new CustomUIBuilder(CustomUIManager.Instance, window, chosenHost);
        if (tags.Count == 0 && items.Count == 0)
        {
            Note(builder, Text.Get("chosen_empty"), null);
            return;
        }

        if (tags.Count > 0)
        {
            float used = 0f;
            float limit = 360f;
            bool row = false;
            for (int i = 0; i < tags.Count; i++)
            {
                Chip chip = tags[i];
                float width = TagWidth(chip.Label);
                if (!row || used + width > limit)
                {
                    if (row)
                    {
                        builder.End();
                    }

                    builder.BeginRow(6f, TextAnchor.MiddleLeft);
                    row = true;
                    used = 0f;
                }

                Chip captured = chip;
                builder.AddButton(chip.Label, () => Cycle(captured), null).WithTooltip(Tip(chip));
                Size(builder.Last, width, 28f);
                Quiet(builder.Last);
                builder.Last.SetTextColor(Ink);
                builder.Last.SetImageColor(Tone(chip));
                used += width + 6f;
            }

            if (row)
            {
                builder.End();
            }
        }

        PlaceItems(builder, items, CardColumns(360f), CardWidth, CardHeight, false);
        LayoutRebuilder.ForceRebuildLayoutImmediate(chosenHost);
    }

    private static void Remember(CustomUIBuilder builder)
    {
        if (builder == null || builder.Last == null)
        {
            return;
        }

        if (plateSprite == null && builder.Last.image != null && builder.Last.image.sprite != null)
        {
            plateSprite = builder.Last.image.sprite;
        }

        if (font == null && builder.Last.text != null)
        {
            font = builder.Last.text.font;
        }
    }

    private static string Summary()
    {
        if (editing == null)
        {
            return "";
        }

        int allow = Rules.Parts(editing.Allow).Count;
        int block = Rules.Parts(editing.Block).Count;
        return Text.Get("word_allow") + " " + allow + "  ·  " + Text.Get("word_block") + " " + block;
    }

    private static string Hint()
    {
        if (selected != null && Catalog.IsFridge(selected.Item))
        {
            return Text.Get("hint_fridge");
        }

        return Text.Get("hint_empty");
    }

    private static Rule LoadEditing(Bin bin)
    {
        if (bin == null || bin.Trash || bin.Missing)
        {
            return null;
        }

        return Rules.Get(bin.Item, bin.Slot);
    }

    private static List<Bin> Siblings(Bin bin)
    {
        List<Bin> list = new List<Bin>();
        if (bin == null)
        {
            return list;
        }

        IReadOnlyList<Bin> bins = Catalog.Bins;
        for (int i = 0; i < bins.Count; i++)
        {
            if (bins[i].Item == bin.Item && !bins[i].Trash)
            {
                list.Add(bins[i]);
            }
        }

        return list;
    }

    private static bool Same(Bin left, Bin right)
    {
        if (left == null || right == null)
        {
            return left == right;
        }

        return left.Item == right.Item && string.Equals(left.Slot ?? "", right.Slot ?? "", StringComparison.OrdinalIgnoreCase) && left.Trash == right.Trash;
    }

    private static string NameOf(GameItem item, string id)
    {
        try
        {
            string name = item == null ? "" : item.GetDisplayName(false);
            if (!string.IsNullOrEmpty(name))
            {
                return name;
            }
        }
        catch
        {
        }

        return ItemFacts.ItemName(id);
    }

    private static Sprite IconOf(GameItem item)
    {
        if (item == null)
        {
            return null;
        }

        try
        {
            GameItemElement element = item as GameItemElement;
            if (element != null)
            {
                if (item.useSpriteFromMod)
                {
                    return element.ResolveSpriteByName(item.spritePath);
                }

                if (element.background != null && element.background.image != null && element.background.image.sprite != null)
                {
                    return element.background.image.sprite;
                }
            }
        }
        catch
        {
        }

        try
        {
            if (!string.IsNullOrEmpty(item.spriteAtlasPath) && !string.IsNullOrEmpty(item.spritePath))
            {
                return RenderHandler.TryLoadFromAtlas(item.spriteAtlasPath, item.spritePath);
            }
        }
        catch
        {
        }

        return null;
    }

    private static Sprite IconForId(string id)
    {
        GameItem created = null;
        try
        {
            created = DirectoryMaster.Create<GameItem>(id);
            return IconOf(created);
        }
        catch
        {
            return null;
        }
        finally
        {
            if (created != null)
            {
                try
                {
                    created.Destroy();
                }
                catch
                {
                }
            }
        }
    }

    private static float TagWidth(string label)
    {
        int length = string.IsNullOrEmpty(label) ? 1 : label.Length;
        return Mathf.Clamp(18f + length * 14f, 52f, 240f);
    }

    private static void Quiet(CustomUIElement element)
    {
        if (element != null && element.button != null)
        {
            element.button.transition = Selectable.Transition.None;
        }
    }

    private static void Pane(RectTransform rect, float width, bool grow)
    {
        if (rect == null)
        {
            return;
        }

        LayoutElement element = rect.GetComponent<LayoutElement>();
        if (element == null)
        {
            element = rect.gameObject.AddComponent<LayoutElement>();
        }

        element.minWidth = width;
        element.preferredWidth = grow ? width : width;
        element.flexibleWidth = grow ? 1f : 0f;
        element.flexibleHeight = 1f;
    }

    private static void Stretch(RectTransform rect)
    {
        if (rect == null)
        {
            return;
        }

        LayoutElement element = rect.GetComponent<LayoutElement>();
        if (element == null)
        {
            element = rect.gameObject.AddComponent<LayoutElement>();
        }

        element.flexibleWidth = 1f;
        element.flexibleHeight = 1f;
    }

    private static void PrepareHost(RectTransform host)
    {
        if (host == null || host.GetComponent<HorizontalLayoutGroup>() != null || host.GetComponent<GridLayoutGroup>() != null)
        {
            return;
        }

        VerticalLayoutGroup group = host.GetComponent<VerticalLayoutGroup>();
        if (group == null)
        {
            group = host.gameObject.AddComponent<VerticalLayoutGroup>();
        }

        group.spacing = 0f;
        group.childAlignment = TextAnchor.UpperLeft;
        group.childControlWidth = true;
        group.childControlHeight = true;
        group.childForceExpandWidth = true;
        group.childForceExpandHeight = true;
    }

    private static void BeginFillScroll(CustomUIBuilder builder, float minHeight)
    {
        builder.BeginScrollFlexible(minHeight);
        RectTransform content = builder.CurrentContainer;
        if (content == null)
        {
            return;
        }

        ScrollRect scroll = content.GetComponentInParent<ScrollRect>();
        if (scroll == null)
        {
            return;
        }

        LayoutElement layout = scroll.GetComponent<LayoutElement>();
        if (layout == null && scroll.transform.parent != null)
        {
            layout = scroll.transform.parent.GetComponent<LayoutElement>();
        }

        if (layout == null)
        {
            return;
        }

        layout.minHeight = minHeight;
        layout.preferredHeight = minHeight;
        layout.flexibleHeight = 1f;
        layout.flexibleWidth = 1f;
    }

    private static int CardColumns(float width)
    {
        return Mathf.Max(2, Mathf.FloorToInt((width + 8f) / (CardWidth + 8f)));
    }

    private static void Section(CustomUIBuilder builder, string text)
    {
        builder.AddLabel(text, null);
        Dress(builder.Last, 15f, FontStyles.Bold, TitleInk, 1.2f);
    }

    private static void Kicker(CustomUIBuilder builder, string text)
    {
        builder.AddLabel(text, null);
        Dress(builder.Last, 12f, FontStyles.Bold, QuietInk, 1.4f);
    }

    private static void Note(CustomUIBuilder builder, string text, string id)
    {
        builder.AddLabel(text, id);
        Dress(builder.Last, 12f, FontStyles.Normal, FaintInk, 0.2f);
    }

    private static void Dress(CustomUIElement element, float size, FontStyles style, Color color, float spacing)
    {
        if (element == null || element.text == null)
        {
            return;
        }

        element.text.fontSize = size;
        element.text.fontStyle = style;
        element.text.color = color;
        element.text.characterSpacing = spacing;
        if (font == null)
        {
            font = element.text.font;
        }
    }

    private static void CollectChosen(Dictionary<string, Chip> known, HashSet<string> marked, List<Chip> tags, List<Chip> items, string csv)
    {
        List<string> parts = Rules.Parts(csv);
        for (int i = 0; i < parts.Count; i++)
        {
            string part = parts[i];
            if (string.IsNullOrEmpty(part) || !marked.Add(part))
            {
                continue;
            }

            Chip chip;
            if (!known.TryGetValue(part, out chip))
            {
                chip = MakeChip(part);
            }

            if (chip == null || string.IsNullOrEmpty(chip.Label))
            {
                continue;
            }

            if (chip.Item || part.StartsWith("id:", StringComparison.OrdinalIgnoreCase))
            {
                items.Add(chip);
            }
            else
            {
                tags.Add(chip);
            }
        }
    }

    private static Chip MakeChip(string part)
    {
        if (part.StartsWith("id:", StringComparison.OrdinalIgnoreCase))
        {
            string id = part.Substring(3);
            string label = ItemFacts.ItemName(id);
            if (string.IsNullOrEmpty(label))
            {
                label = id;
            }

            return new Chip { Part = part, Label = label, Item = true, Sprite = IconForId(id) };
        }

        string typeLabel = ItemFacts.TypeName(part);
        if (string.IsNullOrEmpty(typeLabel))
        {
            typeLabel = part;
        }

        return new Chip { Part = part, Label = typeLabel };
    }

    private static void Size(CustomUIElement element, float width, float height)
    {
        if (element == null)
        {
            return;
        }

        element.SetPreferredSize(width, height);
        if (width > 0f)
        {
            element.SetFixedWidth(width);
        }
    }
}
