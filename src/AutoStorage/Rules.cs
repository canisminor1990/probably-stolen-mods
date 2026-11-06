using System;
using System.Collections.Generic;
using System.Globalization;

namespace AutoStorage;

internal sealed class Rule
{
    public bool Enabled;
    public string Allow = "";
    public string Block = "";
    public bool AcceptStolen;
    public bool StolenOnly;
    public bool AcceptContraband;
    public int Priority;
    public bool? Receive;
    public bool? Supply;

    public Rule Copy()
    {
        return new Rule
        {
            Enabled = Enabled,
            Allow = Allow ?? "",
            Block = Block ?? "",
            AcceptStolen = AcceptStolen,
            StolenOnly = StolenOnly,
            AcceptContraband = AcceptContraband,
            Priority = Priority,
            Receive = Receive,
            Supply = Supply
        };
    }
}

internal static class Rules
{
    private const string Prefix = "rule.";

    internal static bool GlobalEnabled = true;

    private static readonly Dictionary<string, Rule> memory = new Dictionary<string, Rule>(StringComparer.Ordinal);
    private static object store;
    private static int slot = int.MinValue;

    internal static void Load()
    {
        try
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
            memory.Clear();
            GlobalEnabled = true;
            Catalog.Clear();
            RulesWindow.Close();
            string global = ModHelper.GetModData(ModEntry.Id, "global", null);
            List<string> keys = ModHelper.GetModDataKeys(ModEntry.Id);
            for (int i = 0; i < keys.Count; i++)
            {
                string key = keys[i];
                if (key != null && key.StartsWith(Prefix, StringComparison.Ordinal))
                {
                    Remember(key.Substring(Prefix.Length), ModHelper.GetModData(ModEntry.Id, key, null));
                }
            }

            if (global != null)
            {
                GlobalEnabled = global != "0";
            }

            Catalog.IncludeAll = ModHelper.GetModData(ModEntry.Id, "include_all", "0") == "1";
        }
        catch (Exception ex)
        {
            store = null;
            slot = int.MinValue;
            memory.Clear();
            GlobalEnabled = true;
            ModEntry.Log?.Warning("Could not read storage rules: " + ex.Message);
        }
    }

    internal static void SetGlobal(bool enabled)
    {
        Load();
        GlobalEnabled = enabled;
        Write();
    }

    internal static Rule Get(GameItem item, string slotId)
    {
        Load();
        if (item == null)
        {
            return new Rule();
        }

        if (memory.TryGetValue(Key(item, slotId), out Rule rule))
        {
            return rule.Copy();
        }

        return Fresh(item);
    }

    internal static Rule Fresh(GameItem item)
    {
        bool bay = Catalog.IsSmuggler(item);
        return new Rule
        {
            AcceptStolen = bay,
            AcceptContraband = bay
        };
    }

    internal static void Save(GameItem item, string slotId, Rule rule)
    {
        if (item == null || rule == null)
        {
            return;
        }

        Load();
        Rule previous = Get(item, slotId);
        memory[Key(item, slotId)] = new Rule
        {
            Enabled = rule.Enabled,
            Allow = rule.Allow ?? "",
            Block = rule.Block ?? "",
            AcceptStolen = rule.AcceptStolen,
            StolenOnly = rule.StolenOnly,
            AcceptContraband = rule.AcceptContraband,
            Priority = rule.Priority,
            Receive = previous.Receive,
            Supply = previous.Supply
        };
        Write();
    }

    internal static void SetRoles(GameItem item, string slotId, bool? receive, bool? supply)
    {
        if (item == null)
        {
            return;
        }

        Load();
        Rule rule = Get(item, slotId);
        rule.Receive = receive;
        rule.Supply = supply;
        memory[Key(item, slotId)] = rule;
        Write();
    }

    internal static string Key(GameItem item, string slotId)
    {
        string id = Catalog.IdOf(item);
        if (string.IsNullOrEmpty(id))
        {
            id = "container";
        }

        string key = id + "#" + Catalog.UidOf(item);
        if (!string.IsNullOrEmpty(slotId))
        {
            key += "@" + slotId;
        }

        return key;
    }

    internal static List<string> Parts(string csv)
    {
        List<string> list = new List<string>();
        if (string.IsNullOrEmpty(csv))
        {
            return list;
        }

        string[] bits = csv.Split(',');
        for (int i = 0; i < bits.Length; i++)
        {
            string text = bits[i].Trim();
            if (text.Length > 0)
            {
                list.Add(text);
            }
        }

        return list;
    }

    internal static bool Has(string csv, string part)
    {
        List<string> list = Parts(csv);
        for (int i = 0; i < list.Count; i++)
        {
            if (string.Equals(list[i], part, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    internal static string Add(string csv, string part)
    {
        List<string> list = Parts(csv);
        for (int i = 0; i < list.Count; i++)
        {
            if (string.Equals(list[i], part, StringComparison.OrdinalIgnoreCase))
            {
                return Join(list);
            }
        }

        list.Add(part);
        return Join(list);
    }

    internal static string Remove(string csv, string part)
    {
        List<string> list = Parts(csv);
        list.RemoveAll(text => string.Equals(text, part, StringComparison.OrdinalIgnoreCase));
        return Join(list);
    }

    internal static string Join(List<string> parts)
    {
        if (parts == null || parts.Count == 0)
        {
            return "";
        }

        return string.Join(",", parts);
    }

    private static void Remember(string key, string value)
    {
        if (!Valid(key) || memory.ContainsKey(key))
        {
            return;
        }

        Rule rule = Decode(value);
        if (rule != null)
        {
            memory[key] = rule;
        }
    }

    private static bool Valid(string key)
    {
        if (string.IsNullOrEmpty(key))
        {
            return false;
        }

        int hash = key.IndexOf('#');
        if (hash <= 0 || key.IndexOf('#', hash + 1) >= 0)
        {
            return false;
        }

        int at = key.IndexOf('@', hash + 1);
        string uid = at < 0 ? key.Substring(hash + 1) : key.Substring(hash + 1, at - hash - 1);
        return int.TryParse(uid, out _);
    }

    private static Rule Decode(string value)
    {
        if (value == null)
        {
            return null;
        }

        string[] parts = value.Split('|');
        if (parts.Length != 4 || (parts[0] != "0" && parts[0] != "1"))
        {
            return null;
        }

        Rule rule = new Rule
        {
            Enabled = parts[0] == "1",
            Allow = parts[1] ?? "",
            Block = parts[2] ?? ""
        };
        ParseFlags(rule, parts[3]);
        return rule;
    }

    private static string Encode(Rule rule)
    {
        return (rule.Enabled ? "1" : "0") + "|" + (rule.Allow ?? "") + "|" + (rule.Block ?? "") + "|" + Flags(rule);
    }

    private static string Flags(Rule rule)
    {
        string contraband = rule.AcceptContraband ? "1" : "0";
        return "a" + (rule.AcceptStolen ? "1" : "0")
            + "p" + (rule.StolenOnly ? "1" : "0")
            + (rule.Receive.HasValue ? "t" + (rule.Receive.Value ? "1" : "0") : "")
            + (rule.Supply.HasValue ? "s" + (rule.Supply.Value ? "1" : "0") : "")
            + "l" + contraband
            + "m" + contraband
            + "h" + contraband
            + "c" + contraband
            + "r" + rule.Priority.ToString(CultureInfo.InvariantCulture);
    }

    private static void ParseFlags(Rule rule, string text)
    {
        if (string.IsNullOrEmpty(text) || text.Length < 4 || text[0] != 'a' || text[2] != 'p')
        {
            return;
        }

        rule.AcceptStolen = text[1] != '0';
        rule.StolenOnly = text[3] == '1';
        bool sawContraband = false;
        for (int i = 4; i + 1 < text.Length; i += 2)
        {
            if (text[i] == 'r')
            {
                if (int.TryParse(text.Substring(i + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out int priority))
                {
                    rule.Priority = priority;
                }

                break;
            }

            if (text[i + 1] != '0' && text[i + 1] != '1')
            {
                continue;
            }

            bool on = text[i + 1] == '1';
            if (text[i] == 't')
            {
                rule.Receive = on;
            }

            if (text[i] == 's')
            {
                rule.Supply = on;
            }

            if (text[i] == 'l' || text[i] == 'm' || text[i] == 'h' || text[i] == 'c')
            {
                if (!sawContraband)
                {
                    rule.AcceptContraband = false;
                    sawContraband = true;
                }

                if (on)
                {
                    rule.AcceptContraband = true;
                }
            }
        }
    }

    private static void Write()
    {
        try
        {
            if (!PlayerStore.HasInstance)
            {
                return;
            }

            ModHelper.SetModData(ModEntry.Id, "global", GlobalEnabled ? "1" : "0");
            foreach (KeyValuePair<string, Rule> pair in memory)
            {
                ModHelper.SetModData(ModEntry.Id, Prefix + pair.Key, Encode(pair.Value));
            }
        }
        catch (Exception ex)
        {
            ModEntry.Log?.Warning("Could not save storage rules: " + ex.Message);
        }
    }
}
