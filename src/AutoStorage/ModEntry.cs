using UnityEngine;

namespace AutoStorage;

public class ModEntry : IMod
{
    internal const string Id = "storage_rules";

    internal static ModLog Log { get; private set; }

    private GameObject updater;

    public void Init(ModManifest manifest)
    {
        Log = new ModLog(manifest);
        Hotkeys.Init();
        Log.Log("Storage Rules ready.");
    }

    public void OnEnable()
    {
        Sound.Ensure();
        ModHook.OnGameLoadedNormal += OnLoaded;
        ModHook.OnGoingSleepLate += Trash.AfterSleep;
        updater = new GameObject("StorageRules.Updater");
        Object.DontDestroyOnLoad(updater);
        updater.AddComponent<Ticker>();
        Log.Log("Storage Rules enabled.");
    }

    public void OnDisable()
    {
        ModHook.OnGameLoadedNormal -= OnLoaded;
        ModHook.OnGoingSleepLate -= Trash.AfterSleep;
        RulesWindow.Close();
        ContextMenu.Hide();
        if (updater != null)
        {
            Object.Destroy(updater);
        }

        updater = null;
        Log.Log("Storage Rules disabled.");
    }

    private static void OnLoaded()
    {
        Rules.Load();
        Trash.Load();
        Catalog.Clear();
    }

    private sealed class Ticker : MonoBehaviour
    {
        private void Update()
        {
            try
            {
                if (!PlayerStore.HasInstance)
                {
                    return;
                }

                Rules.Load();
                Trash.Load();
                if (Hotkeys.Pressed(Hotkeys.Sort))
                {
                    Sorter.Run();
                }
                else if (Hotkeys.Pressed(Hotkeys.Toggle))
                {
                    Rules.SetGlobal(!Rules.GlobalEnabled);
                    Text.Notify(Rules.GlobalEnabled ? Text.Get("status_on") : Text.Get("status_off"));
                }
            }
            catch (System.Exception ex)
            {
                Log.Warning("Update failed: " + ex.Message);
            }
        }
    }
}
