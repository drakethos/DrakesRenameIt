using System;
using HarmonyLib;
using UnityEngine;

namespace DrakeRenameit.Patches;

/// <summary>
/// Ensures Piece of Paper can mount on vanilla item stands (Material type is often filtered out).
/// </summary>
[HarmonyPatch(typeof(ItemStand))]
internal static class PaperItemStandPatches
{
    [HarmonyPostfix]
    [HarmonyPatch(nameof(ItemStand.CanAttach))]
    private static void CanAttach_Postfix(ItemDrop.ItemData item, ref bool __result)
    {
        if (PaperItem.IsPaperItem(item))
            __result = true;
    }

    [HarmonyPostfix]
    [HarmonyPatch(nameof(ItemStand.GetAttachPrefab))]
    private static void GetAttachPrefab_Postfix(GameObject item, ref GameObject __result)
    {
        if (__result != null || item == null)
            return;

        var isPaper = item.name.Equals(PaperItem.PrefabName, StringComparison.OrdinalIgnoreCase) ||
                      item.name.StartsWith(PaperItem.PrefabName, StringComparison.OrdinalIgnoreCase) ||
                      item.name.Equals(PaperItem.WrittenPrefabName, StringComparison.OrdinalIgnoreCase) ||
                      item.name.StartsWith(PaperItem.WrittenPrefabName, StringComparison.OrdinalIgnoreCase);
        if (!isPaper)
            return;

        var attach = item.transform.Find("attach");
        __result = attach != null ? attach.gameObject : item;
    }
}
