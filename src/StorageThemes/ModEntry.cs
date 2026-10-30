using UnityEngine;

namespace StorageThemes;

public class ModEntry : IMod
{
    internal const string ModId = "storage_themes";

    internal static ModLog Log { get; private set; }

    internal static ConfigEntry<bool> Themes { get; private set; }

    public void Init(ModManifest manifest)
    {
        Log = new ModLog(manifest);

        ModConfig config = ModHelper.GetConfig(manifest.ID);
        Themes = config.Define(
            "themes",
            true,
            "Appearance",
            () => ModHelper.GetLocalized(manifest.ID, "themes_label"),
            () => ModHelper.GetLocalized(manifest.ID, "themes_tip"),
            true);

        Log.Log("Storage Themes ready. Toggle appearance opens the theme panel.");
    }

    public void OnEnable()
    {
        Log.Log("Storage Themes enabled.");
    }

    public void OnDisable()
    {
        Log.Log("Storage Themes disabled.");
    }
}
