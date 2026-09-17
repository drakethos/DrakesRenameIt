using System;
using DrakeModsLibs.API;
using DrakeRenameit.UI;
using HarmonyLib;
using UnityEngine;

namespace DrakeRenameit.Patches;

/// <summary>
/// Ensures Piece of Paper can mount on vanilla item stands (Material type is often filtered out),
/// and that a warded stand still shows the page (name + writing) instead of only "No access".
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

    /// <summary>
    /// Runs after DrakeModsLibs so a warded stand keeps the page text, not only the item name.
    /// </summary>
    [HarmonyPostfix]
    [HarmonyPatch(nameof(ItemStand.GetHoverText))]
    [HarmonyAfter("drakemods.DrakeModsLibs")]
    [HarmonyPriority(Priority.Last)]
    private static void GetHoverText_Postfix(ItemStand __instance, ref string __result)
    {
        if (__instance == null)
            return;

        var item = TryLoadAttached(__instance);
        if (!PaperItem.IsPaperItem(item))
            return;

        string readable = BuildReadable(item!);
        if (string.IsNullOrEmpty(readable))
            return;

        bool blocked = !PrivateArea.CheckAccess(__instance.transform.position, 0f, flash: false);
        if (blocked || HoverTextContainsNoAccess(__result))
        {
            var denied = "$piece_noaccess";
            denied = Localization.instance != null ? Localization.instance.Localize(denied) : denied;
            __result = readable + "\n" + denied;
            return;
        }

        // Access allowed: still show the writing under the stand name so the page is readable in place.
        string desc = CustomDescription(item!);
        if (string.IsNullOrEmpty(desc) ||
            (!string.IsNullOrEmpty(__result) && __result.IndexOf(desc, StringComparison.Ordinal) >= 0))
            return;

        if (string.IsNullOrEmpty(__result))
        {
            __result = readable;
            return;
        }

        int nl = __result.IndexOf('\n');
        __result = nl >= 0
            ? __result.Insert(nl, "\n" + desc)
            : __result + "\n" + desc;
    }

    private static string BuildReadable(ItemDrop.ItemData item)
    {
        string name = CustomizeLibsAPI.GetDisplayNameForUi(item, localize: true);
        if (string.IsNullOrEmpty(name))
            name = item.m_shared?.m_name ?? "";
        name = TooltipRichText.EnsureRichTextTagsClosedForTooltip(name);
        if (Localization.instance != null && !string.IsNullOrEmpty(name))
            name = Localization.instance.Localize(name);

        string desc = CustomDescription(item);
        return string.IsNullOrEmpty(desc) ? name : name + "\n" + desc;
    }

    private static string CustomDescription(ItemDrop.ItemData item)
    {
        if (!CustomizeLibsAPI.HasCustomDescription(item))
            return "";

        string desc = CustomizeLibsAPI.GetProperDescription(item) ?? "";
        desc = TooltipRichText.EnsureRichTextTagsClosedForTooltip(desc);
        if (Localization.instance != null && !string.IsNullOrEmpty(desc))
            desc = Localization.instance.Localize(desc);
        return desc;
    }

    private static ItemDrop.ItemData? TryLoadAttached(ItemStand stand)
    {
        // Valheim 1.0 / Pfhoenix: GetAttachedItem() returns prefab name (string).
        // Older publicized refs: returns prefab hash (int). Resolve via reflection so both CI and local builds compile.
        GameObject? prefab = ResolveAttachedPrefab(stand);
        if (prefab != null)
        {
            var proto = prefab.GetComponent<ItemDrop>()?.m_itemData;
            if (proto != null)
            {
                var clone = proto.Clone();
                var zdo = stand.GetComponent<ZNetView>()?.GetZDO();
                if (zdo != null)
                    TryLoadItemDataFromZdo(clone, zdo);
                return clone;
            }
        }

        var container = stand.GetComponent<Container>();
        var items = container?.GetInventory()?.GetAllItems();
        if (items == null || items.Count == 0)
            return null;
        return items[0];
    }

    private static GameObject? ResolveAttachedPrefab(ItemStand stand)
    {
        if (stand == null || ObjectDB.instance == null)
            return null;

        try
        {
            var mi = AccessTools.Method(typeof(ItemStand), "GetAttachedItem");
            if (mi == null)
                return null;

            object? result = mi.Invoke(stand, null);
            switch (result)
            {
                case int hash when hash != 0:
                    return ObjectDB.instance.GetItemPrefab(hash);
                case string name when !string.IsNullOrEmpty(name):
                    return ObjectDB.instance.GetItemPrefab(name);
                default:
                    return null;
            }
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Valheim 1.0: <c>LoadFromZDO(ItemData, ZDO)</c>. Older: <c>LoadFromZDO(ItemData, ZDO, int)</c>.
    /// </summary>
    private static void TryLoadItemDataFromZdo(ItemDrop.ItemData item, ZDO zdo)
    {
        try
        {
            var load2 = AccessTools.Method(
                typeof(ItemDrop),
                "LoadFromZDO",
                new[] { typeof(ItemDrop.ItemData), typeof(ZDO) });
            if (load2 != null)
            {
                load2.Invoke(null, new object[] { item, zdo });
                return;
            }

            var load3 = AccessTools.Method(
                typeof(ItemDrop),
                "LoadFromZDO",
                new[] { typeof(ItemDrop.ItemData), typeof(ZDO), typeof(int) });
            load3?.Invoke(null, new object[] { item, zdo, -1 });
        }
        catch
        {
            /* keep prefab defaults */
        }
    }

    private static bool HoverTextContainsNoAccess(string? hoverText)
    {
        if (string.IsNullOrEmpty(hoverText))
            return false;

        const string token = "$piece_noaccess";
        if (hoverText!.Contains(token))
            return true;

        if (Localization.instance == null)
            return false;

        string localized = Localization.instance.Localize(token);
        return !string.IsNullOrEmpty(localized) &&
               hoverText.IndexOf(localized, StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
