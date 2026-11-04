namespace SmugglerBayTabs;

public class ModEntry : IMod
{
    internal static ModLog Log { get; private set; }

    public void Init(ModManifest manifest)
    {
        Log = new ModLog(manifest);
        Log.Log("Smuggler bays accept the storage expansion kit.");
    }

    public void OnEnable()
    {
        Log?.Log("Smuggler Bay Tabs enabled.");
    }

    public void OnDisable()
    {
        Log?.Log("Smuggler Bay Tabs disabled.");
    }
}
