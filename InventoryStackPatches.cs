using System;
using System.Reflection;
using BepInEx.Logging;
using HarmonyLib;

namespace DrakeRenameit;

/// <summary>When <see cref="RenameitConfig.SeparateStacks"/> is on, only stacks with matching Drake fingerprints merge.</summary>
/// <remarks>Applied manually from <see cref="Apply"/> so missing game methods do not break <see cref="Harmony.PatchAll"/>.</remarks>
internal static class InventoryStackPatches
{
    internal static ItemDrop.ItemData? IncomingStackItem;

    internal static void Apply(Harmony harmony, ManualLogSource log)
    {
        var inv = typeof(Inventory);
        var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        var addItemOne = AccessTools.DeclaredMethod(inv, nameof(Inventory.AddItem), new[] { typeof(ItemDrop.ItemData) })
                         ?? AccessTools.Method(inv, nameof(Inventory.AddItem), new[] { typeof(ItemDrop.ItemData) });
        if (addItemOne != null)
        {
            harmony.Patch(
                addItemOne,
                prefix: new HarmonyMethod(typeof(InventoryStackPatches), nameof(AddItem_IncomingPrefix)),
                finalizer: new HarmonyMethod(typeof(InventoryStackPatches), nameof(AddItem_IncomingCleanup)));
        }
        else
            log.LogWarning("[DrakesRenameIt] SeparateStacks: AddItem(ItemData) not found — incoming stack tracking disabled.");

        var findStack = AccessTools.DeclaredMethod(inv, "FindFreeStackItem", new[] { typeof(string), typeof(int) })
                        ?? AccessTools.Method(inv, "FindFreeStackItem", new[] { typeof(string), typeof(int) });
        if (findStack == null)
        {
            foreach (var m in inv.GetMethods(flags))
            {
                if (m.Name != "FindFreeStackItem" || m.GetParameters().Length != 2)
                    continue;
                findStack = m;
                log.LogInfo("[DrakesRenameIt] SeparateStacks: using scanned FindFreeStackItem: " + m);
                break;
            }
        }

        if (findStack != null)
        {
            harmony.Patch(
                findStack,
                postfix: new HarmonyMethod(typeof(InventoryStackPatches), nameof(FindFreeStackItem_Postfix)));
        }
        else
            log.LogWarning("[DrakesRenameIt] SeparateStacks: FindFreeStackItem not found — merge-from-pickup may ignore identity.");

        var addAtCell = AccessTools.DeclaredMethod(
                           inv,
                           "AddItem",
                           new[] { typeof(ItemDrop.ItemData), typeof(int), typeof(int), typeof(int) })
                        ?? AccessTools.Method(
                            inv,
                            "AddItem",
                            new[] { typeof(ItemDrop.ItemData), typeof(int), typeof(int), typeof(int) });
        if (addAtCell != null)
        {
            harmony.Patch(
                addAtCell,
                prefix: new HarmonyMethod(typeof(InventoryStackPatches), nameof(AddItemAtCell_Prefix)));
        }
        else
            log.LogWarning("[DrakesRenameIt] SeparateStacks: AddItem(ItemData,int,int,int) not found — cell merge guard disabled.");
    }

    static void AddItem_IncomingPrefix(ItemDrop.ItemData item)
    {
        IncomingStackItem = item;
    }

    static void AddItem_IncomingCleanup(Exception? __exception)
    {
        IncomingStackItem = null;
    }

    static void FindFreeStackItem_Postfix(ref ItemDrop.ItemData? __result)
    {
        if (!RenameitConfig.SeparateStacks || __result == null || IncomingStackItem == null)
            return;
        if (!StackIdentity.SameDrakeStackIdentity(IncomingStackItem, __result))
            __result = null;
    }

    static bool AddItemAtCell_Prefix(
        ItemDrop.ItemData item,
        int amount,
        int x,
        int y,
        Inventory __instance,
        ref bool __result)
    {
        if (!RenameitConfig.SeparateStacks)
            return true;

        ItemDrop.ItemData? itemAt = __instance.GetItemAt(x, y);
        if (itemAt == null)
            return true;

        if (itemAt.m_shared.m_name != item.m_shared.m_name)
            return true;
        if (itemAt.m_shared.m_maxQuality > 1 && itemAt.m_quality != item.m_quality)
            return true;

        if (!StackIdentity.SameDrakeStackIdentity(item, itemAt))
        {
            __result = false;
            return false;
        }

        return true;
    }
}
