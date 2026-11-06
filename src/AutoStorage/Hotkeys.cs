using System;
using UnityEngine;

namespace AutoStorage;

internal static class Hotkeys
{
    internal static ConfigEntry<string> Sort;
    internal static ConfigEntry<string> Toggle;

    internal static void Init()
    {
        ModConfig config = ModHelper.GetConfig(ModEntry.Id);
        const string Section = "快捷键 / Hotkeys";
        Sort = config.Define("sort_hotkey", "Ctrl+R", Section, () => Text.Get("sort_hotkey"), null, () => Text.Get("sort_hotkey_tip"), false);
        Toggle = config.Define("toggle_hotkey", "Ctrl+Shift+O", Section, () => Text.Get("toggle_hotkey"), null, () => Text.Get("toggle_hotkey_tip"), false);
    }

    internal static bool Pressed(ConfigEntry<string> entry)
    {
        if (entry == null || !Bind(entry.Value, out KeyCode key, out bool ctrl, out bool alt, out bool shift) || key == KeyCode.None)
        {
            return false;
        }

        return Input.GetKeyDown(key) && Mods(ctrl, alt, shift);
    }

    internal static string Current(ConfigEntry<string> entry)
    {
        if (entry == null || string.IsNullOrWhiteSpace(entry.Value))
        {
            return "";
        }

        return entry.Value.Trim();
    }

    private static bool Bind(string raw, out KeyCode key, out bool ctrl, out bool alt, out bool shift)
    {
        key = KeyCode.None;
        ctrl = false;
        alt = false;
        shift = false;
        if (string.IsNullOrWhiteSpace(raw) || raw.Equals("None", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        string[] parts = raw.Split('+');
        for (int i = 0; i < parts.Length; i++)
        {
            string token = parts[i].Trim();
            string lower = token.ToLowerInvariant();
            if (lower == "ctrl" || lower == "control" || lower == "leftcontrol" || lower == "rightcontrol")
            {
                ctrl = true;
                continue;
            }

            if (lower == "alt" || lower == "leftalt" || lower == "rightalt")
            {
                alt = true;
                continue;
            }

            if (lower == "shift" || lower == "leftshift" || lower == "rightshift")
            {
                shift = true;
                continue;
            }

            if (key != KeyCode.None || !Enum.TryParse((token == "`") ? "BackQuote" : token, true, out KeyCode parsed) || !Enum.IsDefined(typeof(KeyCode), parsed) || parsed == KeyCode.None)
            {
                return false;
            }

            key = parsed;
        }

        return key != KeyCode.None;
    }

    private static bool Mods(bool ctrl, bool alt, bool shift)
    {
        bool ctrlDown = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
        bool altDown = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
        bool shiftDown = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        return ctrl == ctrlDown && alt == altDown && shift == shiftDown;
    }
}
