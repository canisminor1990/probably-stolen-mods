using System;

namespace AutoStorage;

internal static class Mover
{
    private static bool warned;

    internal static bool CanAccept(GameInventory inventory, GameItem item)
    {
        if (inventory == null || item == null)
        {
            return false;
        }

        try
        {
            return GraphUtils.CanAccept(inventory, item);
        }
        catch (Exception ex)
        {
            if (!warned)
            {
                warned = true;
                ModEntry.Log?.Warning("Capacity check failed: " + ex.Message);
            }

            return false;
        }
    }

    internal static bool Move(GameItem item, GameInventory target)
    {
        if (item == null || target == null)
        {
            return false;
        }

        GameInventory source = null;
        try
        {
            source = item.parentInventory;
        }
        catch
        {
        }

        if (source == null || source == target)
        {
            return false;
        }

        try
        {
            if (source.IsRemoveLocked() || target.IsInsertLocked())
            {
                return false;
            }
        }
        catch
        {
            return false;
        }

        if (!Catalog.Attached(item))
        {
            return false;
        }

        int result = -1;
        try
        {
            result = GraphUtils.TryAcceptAll(target, item, -1);
        }
        catch (Exception ex)
        {
            ModEntry.Log?.Warning("Move failed: " + ex.Message);
        }

        if (result == 0)
        {
            return true;
        }

        try
        {
            if (item.parentInventory == null)
            {
                GraphUtils.TryAcceptAll(source, item, -1);
            }
        }
        catch (Exception ex)
        {
            ModEntry.Log?.Warning("Move rollback failed: " + ex.Message);
        }

        return false;
    }
}
