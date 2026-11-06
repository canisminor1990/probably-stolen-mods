using System;
using System.Collections;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace AutoStorage;

internal static class ContextMenu
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

    private static AccessTools.FieldRef<ItemContextHandler, GameItem> currentItem;
    private static AccessTools.FieldRef<ItemContextHandler, PixelWindow> contextWindow;
    private static AccessTools.FieldRef<ItemContextHandler, RenderHandler.ColorPalette> textColor;
    private static AccessTools.FieldRef<ItemContextHandler, RenderHandler.ColorPalette> textSelectColor;
    private static FieldInfo elementsField;
    private static FieldInfo innersField;
    private static FieldInfo minimumField;
    private static FieldInfo heightField;
    private static bool fieldsReady;
    private static bool fieldsFailed;
    private static bool dangpu;
    private static bool dangpuTried;
    private static Type dangpuEntry;
    private static RichTextElement row;
    private static GridPixelElement grid;
    private static ItemContextHandler owner;
    private static bool shown;
    private static bool menuFailed;

    internal static void Hide()
    {
        if (row != null && row.handler != null)
        {
            row.handler.gameObject.SetActive(false);
        }

        shown = false;
    }

    private static void Refresh(ItemContextHandler handler)
    {
        if (handler == null || menuFailed)
        {
            return;
        }

        try
        {
            if (!dangpu && !dangpuTried)
            {
                dangpuTried = true;
                TryDangpu();
            }

            if (dangpu || !Bind())
            {
                return;
            }

            PixelWindow window = contextWindow(handler);
            if (window == null || window.handler == null || !(window.childElement is GridPixelElement host))
            {
                return;
            }

            GameItem item = currentItem(handler);
            bool show = Catalog.CanConfigure(item);
            if (owner != handler || row == null || row.handler == null || Find(host, row) == null)
            {
                if (!show)
                {
                    return;
                }

                row = new RichTextElement(false, -1, -1, true);
                row.SetText(Label(item), 10000, textColor(handler));
                row.handler.gameObject.name = "Storage Rules";
                row.handler.gameObject.tag = "ContextMenuNode";
                int index = host.gridHeight;
                Grow(host);
                if (!host.AttachPos(row, 0, index))
                {
                    throw new InvalidOperationException("Could not add the storage menu row.");
                }

                owner = handler;
                grid = host;
                shown = true;
            }

            if (shown != show)
            {
                shown = show;
                row.handler.gameObject.SetActive(show);
                grid.Validate();
                window.Validate();
            }

            if (show)
            {
                row.SetText(Label(item), 10000, textColor(handler));
            }
        }
        catch (Exception ex)
        {
            menuFailed = true;
            ModEntry.Log?.Warning("Context menu row failed: " + ex.Message);
        }
    }

    private static void Layout(ItemContextHandler handler)
    {
        if (menuFailed || dangpu || handler == null || owner != handler || !shown || row == null || grid == null)
        {
            return;
        }

        try
        {
            PixelElement[] elements = (PixelElement[])elementsField.GetValue(grid);
            int widest = 0;
            for (int i = 0; i < elements.Length; i++)
            {
                if (elements[i] is RichTextElement text && text.handler != null && text.handler.gameObject.activeSelf)
                {
                    text.SetFixedSize(-1, -1);
                    if (text.widthPixels > widest)
                    {
                        widest = text.widthPixels;
                    }
                }
            }

            for (int i = 0; i < elements.Length; i++)
            {
                if (elements[i] is RichTextElement text && text.handler != null && text.handler.gameObject.activeSelf)
                {
                    text.SetFixedSize(widest, -1);
                }
            }

            grid.Validate();
            PixelWindow window = contextWindow(handler);
            if (window != null)
            {
                window.Validate();
            }
        }
        catch (Exception ex)
        {
            menuFailed = true;
            ModEntry.Log?.Warning("Context menu layout failed: " + ex.Message);
        }
    }

    private static void Highlight(ItemContextHandler handler, RichTextElement selected)
    {
        if (menuFailed || dangpu || handler == null || owner != handler || !shown || row == null || row.handler == null)
        {
            return;
        }

        try
        {
            RenderHandler.ColorPalette color = selected == row ? textSelectColor(handler) : textColor(handler);
            row.SetText(Label(currentItem(handler)), 10000, color);
        }
        catch
        {
        }
    }

    private static bool Trigger(ItemContextHandler handler, RichTextElement button)
    {
        if (dangpu || handler == null || owner != handler || button == null || button != row)
        {
            return true;
        }

        try
        {
            if (shown)
            {
                RulesWindow.Open(currentItem(handler));
            }
        }
        catch (Exception ex)
        {
            ModEntry.Log?.Warning("Could not open storage rules: " + ex.Message);
        }

        handler.Stop(false);
        return false;
    }

    private static string Label(GameItem item)
    {
        return Catalog.IsTrash(item) ? Text.Get("menu_trash") : Text.Get("menu_rules");
    }

    private static bool Bind()
    {
        if (fieldsReady)
        {
            return true;
        }

        if (fieldsFailed)
        {
            return false;
        }

        try
        {
            currentItem = AccessTools.FieldRefAccess<ItemContextHandler, GameItem>("currentItem");
            contextWindow = AccessTools.FieldRefAccess<ItemContextHandler, PixelWindow>("contextWindow");
            textColor = AccessTools.FieldRefAccess<ItemContextHandler, RenderHandler.ColorPalette>("textColor");
            textSelectColor = AccessTools.FieldRefAccess<ItemContextHandler, RenderHandler.ColorPalette>("textSelectColor");
            elementsField = typeof(GridPixelElement).GetField("childElements", Flags);
            innersField = typeof(GridPixelElement).GetField("childInners", Flags);
            minimumField = typeof(GridPixelElement).GetField("childMinimumSize", Flags);
            heightField = typeof(GridPixelElement).GetField("gridHeight", Flags);
            if (elementsField == null || innersField == null || minimumField == null || heightField == null)
            {
                throw new MissingFieldException("GridPixelElement");
            }

            fieldsReady = true;
            return true;
        }
        catch (Exception ex)
        {
            fieldsFailed = true;
            ModEntry.Log?.Warning("Context menu is unavailable: " + ex.Message);
            return false;
        }
    }

    private static GridPixelElement Find(GridPixelElement host, RichTextElement needle)
    {
        if (host == null || needle == null || elementsField == null)
        {
            return null;
        }

        PixelElement[] elements = (PixelElement[])elementsField.GetValue(host);
        if (elements == null)
        {
            return null;
        }

        for (int i = 0; i < elements.Length; i++)
        {
            if (elements[i] == needle)
            {
                return host;
            }

            if (elements[i] is GridPixelElement child)
            {
                GridPixelElement found = Find(child, needle);
                if (found != null)
                {
                    return found;
                }
            }
        }

        return null;
    }

    private static void Grow(GridPixelElement host)
    {
        if (host.gridWidth < 1)
        {
            throw new InvalidOperationException("Menu grid has no columns.");
        }

        PixelElement[] elements = (PixelElement[])elementsField.GetValue(host);
        TreeNodeRender[] inners = (TreeNodeRender[])innersField.GetValue(host);
        (int, int)[] minimums = ((int, int)[])minimumField.GetValue(host);
        int count = host.gridWidth * host.gridHeight;
        if (elements == null || inners == null || minimums == null || elements.Length != count || inners.Length != count || minimums.Length != count)
        {
            throw new InvalidOperationException("Menu grid is inconsistent.");
        }

        int next = count + 1;
        PixelElement[] grownElements = new PixelElement[next];
        TreeNodeRender[] grownInners = new TreeNodeRender[next];
        (int, int)[] grownMinimums = new (int, int)[next];
        Array.Copy(elements, grownElements, count);
        Array.Copy(inners, grownInners, count);
        Array.Copy(minimums, grownMinimums, count);
        for (int i = count; i < next; i++)
        {
            grownInners[i] = TreeNodeRender.Instantiate().SetParent(host.inner);
        }

        elementsField.SetValue(host, grownElements);
        innersField.SetValue(host, grownInners);
        minimumField.SetValue(host, grownMinimums);
        heightField.SetValue(host, host.gridHeight + 1);
    }

    private static void TryDangpu()
    {
        try
        {
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                Type extras = assemblies[i].GetType("DangpuPlus.Features.ContextMenuExtras", false);
                if (extras == null)
                {
                    continue;
                }

                Type entry = assemblies[i].GetType("DangpuPlus.Features.MenuEntry", false);
                MethodInfo register = extras.GetMethod("Register", Flags);
                if (entry == null || register == null || register.GetParameters().Length != 1)
                {
                    continue;
                }

                dangpuEntry = entry;
                Delegate provider = Delegate.CreateDelegate(register.GetParameters()[0].ParameterType, typeof(ContextMenu).GetMethod(nameof(Provide), Flags));
                register.Invoke(null, new object[] { provider });
                dangpu = true;
                return;
            }
        }
        catch (Exception ex)
        {
            ModEntry.Log?.Warning("Dangpu menu hook skipped: " + ex.Message);
        }
    }

    private static void Provide(GameItem item, object entries)
    {
        if (!Catalog.CanConfigure(item) || dangpuEntry == null || !(entries is IList list))
        {
            return;
        }

        object entry = Activator.CreateInstance(dangpuEntry, true);
        dangpuEntry.GetField("Key", Flags)?.SetValue(entry, "canisminor.StorageRules.Rules");
        dangpuEntry.GetField("ElementName", Flags)?.SetValue(entry, "Storage Rules");
        dangpuEntry.GetField("Text", Flags)?.SetValue(entry, Label(item));
        dangpuEntry.GetField("Enabled", Flags)?.SetValue(entry, true);
        dangpuEntry.GetField("Run", Flags)?.SetValue(entry, new Action<GameItem>(RulesWindow.Open));
        list.Add(entry);
    }

    [HarmonyPatch(typeof(ItemContextHandler), "UpdateConditions")]
    private static class Patch_Update
    {
        private static void Postfix(ItemContextHandler __instance)
        {
            Refresh(__instance);
        }
    }

    [HarmonyPatch(typeof(ItemContextHandler), "EqualizeButtonWidths")]
    [HarmonyPriority(Priority.Last)]
    private static class Patch_Layout
    {
        private static void Postfix(ItemContextHandler __instance)
        {
            Layout(__instance);
        }
    }

    [HarmonyPatch(typeof(ItemContextHandler), "UpdateSelectButtonColor")]
    private static class Patch_Highlight
    {
        private static void Postfix(ItemContextHandler __instance, RichTextElement selectedButton)
        {
            Highlight(__instance, selectedButton);
        }
    }

    [HarmonyPatch(typeof(ItemContextHandler), "ResetButtonColor")]
    private static class Patch_Reset
    {
        private static void Postfix(ItemContextHandler __instance)
        {
            Highlight(__instance, null);
        }
    }

    [HarmonyPatch(typeof(ItemContextHandler), "TriggerButton")]
    private static class Patch_Trigger
    {
        private static bool Prefix(ItemContextHandler __instance, RichTextElement button)
        {
            return Trigger(__instance, button);
        }
    }
}
