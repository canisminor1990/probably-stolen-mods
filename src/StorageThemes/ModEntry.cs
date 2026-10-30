using UnityEngine;

namespace StorageThemes;

public class ModEntry : IMod
{
    internal const string ModId = "storage_themes";

    internal static ModLog Log { get; private set; }

    internal static ConfigEntry<bool> Storage { get; private set; }

    internal static ConfigEntry<bool> Smuggler { get; private set; }

    internal static ConfigEntry<bool> Hypertech { get; private set; }

    internal static ConfigEntry<bool> Machine { get; private set; }

    internal static bool IsKindEnabled(string spriteName)
    {
        switch (Util.KindOf(spriteName))
        {
            case ContainerKind.Storage:
                return Storage == null || Storage.Value;
            case ContainerKind.Smuggler:
                return Smuggler == null || Smuggler.Value;
            case ContainerKind.Hypertech:
                return Hypertech == null || Hypertech.Value;
            case ContainerKind.Machine:
                return Machine == null || Machine.Value;
            default:
                return true;
        }
    }

    public void Init(ModManifest manifest)
    {
        Log = new ModLog(manifest);

        ModConfig config = ModHelper.GetConfig(manifest.ID);
        const string Section = "容器 / Containers";
        Storage = DefineKind(config, manifest.ID, "storage", "kind_storage", "kind_storage_tip", Section);
        Smuggler = DefineKind(config, manifest.ID, "smuggler", "kind_smuggler", "kind_smuggler_tip", Section);
        Hypertech = DefineKind(config, manifest.ID, "hypertech", "kind_hypertech", "kind_hypertech_tip", Section);
        Machine = DefineKind(config, manifest.ID, "machine", "kind_machine", "kind_machine_tip", Section);

        Log.Log("Storage Themes ready. Toggle appearance opens the theme panel.");
    }

    private static ConfigEntry<bool> DefineKind(ModConfig config, string modId, string key, string labelKey, string tipKey, string section)
    {
        return config.Define(
            key,
            true,
            section,
            () => ModHelper.GetLocalized(modId, labelKey),
            () => ModHelper.GetLocalized(modId, tipKey),
            true);
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
