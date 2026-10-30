using System;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace StorageThemes;

internal static class ThemePicker
{
    private const int Columns = 2;

    private static PixelWindow window;

    private static List<AdvButtonElement> openButtons;

    private static readonly string[] TextColorFields =
    {
        "buttonTextColorNormal",
        "buttonTextColorHighlight",
        "buttonTextColorClicked",
        "buttonTextColorDisabled",
    };

    internal static void Open(GameItem item, string atlasPath, string[] options)
    {
        if (item == null || string.IsNullOrEmpty(atlasPath) || options == null || options.Length == 0)
        {
            return;
        }

        try
        {
            Close();
            window = Build(item, atlasPath, options);
            window.Show(Input.mousePosition, true);
            CenterOpenLabels();
        }
        catch (Exception ex)
        {
            ModEntry.Log?.Warning("Theme panel failed to open: " + ex.Message);
            Close();
        }
    }

    private static PixelWindow Build(GameItem item, string atlasPath, string[] options)
    {
        string current = item.spritePath;
        string title = Text("picker_title");
        PixelWindow panel = new PixelWindow(true, title, WindowStyle.Machinery);
        panel.xPadding = 8;
        panel.yPadding = 6;

        int rows = (options.Length + Columns - 1) / Columns;
        GridPixelElement grid = new GridPixelElement(Columns * 2, rows, true);
        grid.SetSpacing(6, 4);

        GameItemElement element = item as GameItemElement;
        openButtons = new List<AdvButtonElement>(options.Length);
        for (int i = 0; i < options.Length; i++)
        {
            string option = options[i];
            int column = i % Columns;
            int row = i / Columns;
            string label = Label(option, options[0]);
            bool selected = IsSelected(option, current);

            SpritePixelElement icon = new SpritePixelElement(atlasPath, PreviewName(option));
            if (element != null)
            {
                Sprite sprite = element.ResolveSpriteByName(option);
                if (sprite != null)
                {
                    icon.SetSprite(sprite);
                }
            }

            Sprite idle = selected ? MachineryStyle.ButtonPressed : MachineryStyle.ButtonNormal;
            AdvButtonElement button = new AdvButtonElement(
                idle,
                label,
                72,
                MachineryStyle.BUTTON_H,
                () => Choose(item, atlasPath, option),
                "interface_click");
            button.SetSpriteHighlight(MachineryStyle.ButtonHover);
            button.SetSpriteClicked(MachineryStyle.ButtonPressed);
            button.SetSpriteDisabled(MachineryStyle.ButtonNormal);
            button.SetSpriteNormal(idle);
            ApplyMachineryText(button);
            button.description = label;
            openButtons.Add(button);

            grid.AttachPos(icon, column * 2, row);
            grid.AttachPos(button, column * 2 + 1, row);
        }

        panel.Attach(grid);
        return panel;
    }

    private static void Choose(GameItem item, string atlasPath, string option)
    {
        try
        {
            if (item != null && !string.IsNullOrEmpty(option))
            {
                item.SetSprite(atlasPath, ApplyFrame(item, option));
                if (item is GameItemElement element)
                {
                    Sprite sprite = element.ResolveSpriteByName(option);
                    if (sprite != null)
                    {
                        element.ApplyAnimationFrame(sprite);
                    }

                    element.Validate();
                }
            }
        }
        catch (Exception ex)
        {
            ModEntry.Log?.Warning("Theme apply failed: " + ex.Message);
        }
        finally
        {
            Close();
        }
    }

    private static bool IsSelected(string option, string current)
    {
        if (option == current)
        {
            return true;
        }

        bool optionThemed = Util.TryParse(option, out string optionBase, out string optionSuffix);
        bool currentThemed = Util.TryParse(current, out string currentBase, out string currentSuffix);
        if (optionThemed && currentThemed)
        {
            return optionSuffix == currentSuffix
                && Util.IsMachineBayFrame(optionBase)
                && Util.IsMachineBayFrame(currentBase);
        }

        return !optionThemed
            && !currentThemed
            && Util.IsMachineBayFrame(option)
            && Util.IsMachineBayFrame(current);
    }

    private static string ApplyFrame(GameItem item, string option)
    {
        string current = item != null ? item.spritePath : null;
        string frame = null;
        if (Util.TryParse(current, out string shown, out _) && Util.IsMachineBayFrame(shown))
        {
            frame = shown;
        }
        else if (Util.IsMachineBayFrame(current))
        {
            frame = current;
        }

        if (frame == null)
        {
            return option;
        }

        if (Util.TryParse(option, out string optionBase, out string suffix) && Util.IsMachineBayFrame(optionBase))
        {
            return Palette.Name(frame, suffix);
        }

        if (Util.IsMachineBayFrame(option))
        {
            return frame;
        }

        return option;
    }

    private static string PreviewName(string option)
    {
        if (Util.TryParse(option, out string baseName, out _))
        {
            return baseName;
        }

        return option;
    }

    private static string Label(string option, string baseSprite)
    {
        if (!Util.TryParse(option, out _, out string suffix))
        {
            if (option == baseSprite)
            {
                return Text("theme_original");
            }

            if (baseSprite != null && option != null && option.StartsWith(baseSprite, StringComparison.Ordinal))
            {
                string frame = option.Substring(baseSprite.Length);
                if (frame == "1")
                {
                    return Text("theme_frame_warm");
                }

                if (frame == "2")
                {
                    return Text("theme_frame_dark");
                }
            }

            return option;
        }

        if (suffix.Length > 1 && suffix[0] == 't')
        {
            return Text("theme_" + suffix.Substring(1));
        }

        return option;
    }

    private static void CenterOpenLabels()
    {
        if (openButtons == null)
        {
            return;
        }

        for (int i = 0; i < openButtons.Count; i++)
        {
            CenterLabel(openButtons[i]);
        }
    }

    private static void ApplyMachineryText(AdvButtonElement button)
    {
        Color32 color = MachineryStyle.BUTTON_TEXT;
        BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        for (int i = 0; i < TextColorFields.Length; i++)
        {
            typeof(AdvButtonElement).GetField(TextColorFields[i], flags)?.SetValue(button, color);
        }
    }

    private static void CenterLabel(AdvButtonElement button)
    {
        if (button == null || button.handler == null)
        {
            return;
        }

        RectTransform plate = null;
        RectTransform label = null;
        Transform root = button.handler.transform;
        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child.name == "Node (AdvButton)" || child.name == "Node (Button)")
            {
                plate = child as RectTransform;
            }
            else if (child.name == "Node (Text)")
            {
                label = child as RectTransform;
            }
        }

        if (plate == null || label == null)
        {
            return;
        }

        Image plateImage = plate.GetComponent<Image>();
        if (plateImage != null)
        {
            plateImage.type = Image.Type.Sliced;
            plateImage.color = Color.white;
        }

        label.anchorMin = plate.anchorMin;
        label.anchorMax = plate.anchorMax;
        label.pivot = plate.pivot;
        label.anchoredPosition = plate.anchoredPosition;
        label.sizeDelta = plate.sizeDelta;

        TextMeshProUGUI text = label.GetComponent<TextMeshProUGUI>();
        if (text == null)
        {
            return;
        }

        text.alignment = TextAlignmentOptions.Center;
        text.enableWordWrapping = false;
        text.margin = Vector4.zero;
        text.color = MachineryStyle.BUTTON_TEXT;
        text.ForceMeshUpdate();
    }

    private static string Text(string key)
    {
        return ModHelper.GetLocalized(ModEntry.ModId, key);
    }

    private static void Close()
    {
        PixelWindow closing = window;
        window = null;
        openButtons = null;
        if (closing == null)
        {
            return;
        }

        try
        {
            TreeNodeRender handler = closing.handler;
            closing.Hide();
            if (handler != null)
            {
                UnityEngine.Object.Destroy(handler.gameObject);
            }
        }
        catch (Exception ex)
        {
            ModEntry.Log?.Warning("Theme panel close failed: " + ex.Message);
        }
    }
}
