namespace AutoStorage;

internal static class Text
{
    internal static string Get(string key)
    {
        try
        {
            string value = ModHelper.GetLocalized(ModEntry.Id, key);
            if (!string.IsNullOrEmpty(value)
                && !string.Equals(value, key, System.StringComparison.Ordinal)
                && !value.StartsWith("Translation Error", System.StringComparison.OrdinalIgnoreCase))
            {
                return value;
            }
        }
        catch
        {
        }

        return key;
    }

    internal static void Notify(string message)
    {
        if (string.IsNullOrEmpty(message))
        {
            return;
        }

        try
        {
            if (StoreUIManager.Instance != null)
            {
                StoreUIManager.Instance.Notify(message, "default");
            }
        }
        catch
        {
        }

        RulesWindow.SetStatus(message);
    }
}
