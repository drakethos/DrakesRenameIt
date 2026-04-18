using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace DrakeRenameit;

/// <summary>Shared hover text rename logic (ItemDrop + ItemStand / container stands).</summary>
internal static class HoverRenameHelper
{
    internal static void ApplyRenameToHoverResult(ref string __result, ItemDrop.ItemData item)
    {
        if (item?.m_shared == null || string.IsNullOrEmpty(__result))
            return;

        if (!DrakeRenameit.hasNewName(item))
            return;

        string customName = DrakeRenameit.GetPropperName(item);
        if (customName == null || item.m_shared.m_name == null)
            return;

        // Replace the default name in the hover text with our rename
        if (Localization.instance == null)
        {
            __result = __result.Replace(item.m_shared.m_name, customName);
            return;
        }

        string localizedOriginalName = Localization.instance.Localize(item.m_shared.m_name);
        string localizedCustomName = Localization.instance.Localize(customName);

        if (__result.Contains(localizedOriginalName))
            __result = __result.Replace(localizedOriginalName, localizedCustomName);
    }
}

public static class Patches
{
    [HarmonyPatch(typeof(ItemDrop))]
    public static class HoverTextPatch
    {
        [HarmonyPatch(nameof(ItemDrop.GetHoverText))]
        [HarmonyPostfix]
        static void FixHoverText(ItemDrop __instance, ref string __result)
        {
            var item = __instance.m_itemData;
            if (item == null) return;
            if (__instance?.m_itemData?.m_shared == null || string.IsNullOrEmpty(__result))
                return;

            HoverRenameHelper.ApplyRenameToHoverResult(ref __result, item);
        }

        [HarmonyPatch(typeof(ItemDrop), nameof(ItemDrop.GetHoverName))]
        [HarmonyPostfix]
        static void FixHoverName(ItemDrop __instance, ref string __result)
        {
            var item = __instance?.m_itemData;
            if (item == null)
                return;

            if (DrakeRenameit.hasNewName(item))
            {
                var newName = DrakeRenameit.GetPropperName(item);
                if (!string.IsNullOrEmpty(newName))
                    __result = newName;
            }
        }
    }

    [HarmonyPatch(typeof(InventoryGui))]
    public static class PatchInventoryGuiAwake
    {
        [HarmonyPatch(nameof(InventoryGui.Awake))]
        [HarmonyPostfix]
        static void RenameGrab(InventoryGui __instance)
        {
            // grab the original delegate
            var original = __instance.m_playerGrid.m_onRightClick;

            // wrap it with our own
            __instance.m_playerGrid.m_onRightClick = (grid, item, pos) =>
            {
                if (item != null && (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)))
                {
                    if (DrakeRenameit.CanChangeName(item, true))
                    {
                        DrakeRenameit.OpenRename(item);
                    }

                    return; // stop here, skip vanilla handler
                }

                if (item != null && (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)))
                {
                    if (DrakeRenameit.CanChangeDesc(item, true))
                    {
                        DrakeRenameit.OpenRewriteDesc(item);
                    }

                    return; // stop here, skip vanilla handler
                }

                // otherwise, let vanilla delegate run
                original?.Invoke(grid, item, pos);
            };
        }


        [HarmonyPatch(nameof(InventoryGui.DoCrafting))]
        [HarmonyPostfix]
        static void FixCrafting(InventoryGui __instance, Player player)
        {
            var craftRecipe = AccessTools.Field(typeof(InventoryGui), "m_craftRecipe")
    ?.GetValue(__instance) as Recipe;
            var oldItem = (ItemDrop.ItemData)AccessTools.Field(typeof(InventoryGui), "m_craftUpgradeItem")
                .GetValue(__instance);

            if (craftRecipe == null)
                return;
            if (oldItem == null)
                return;

            var inv = player.GetInventory();
            var newItem = inv.GetItemAt(oldItem.m_gridPos.x, oldItem.m_gridPos.y);
            if (newItem == null)
            {
                // fallback: grab last-added item of that name
                newItem = inv.GetAllItems().LastOrDefault(i =>
                    i.m_shared.m_name == oldItem.m_shared.m_name && i.m_quality > oldItem.m_quality);
            }

            if (newItem != null)
            {
                // carry over rename/desc customData
                foreach (var kv in oldItem.m_customData)
                {
                    // overwrite or add
                    newItem.m_customData[kv.Key] = kv.Value;
                }
            }
        }
    }
}

[HarmonyPatch(typeof(InventoryGrid), nameof(InventoryGrid.CreateItemTooltip))]
public static class InventoryGridTooltipPatch
{
    [HarmonyPostfix]
    static void UpdateToolTip(InventoryGrid __instance, ItemDrop.ItemData? item, UITooltip tooltip)
    {
        if (item?.m_shared == null || tooltip == null)
            return;

        var topic = DrakeRenameit.GetPropperName(item) ?? item.m_shared.m_name;
        string currentText = item.GetTooltip();

        // Handle custom description replacement
        currentText = UpdateDescription(item, currentText);

        // Build tooltip extensions
        var sb = new System.Text.StringBuilder();

        // Config: rename enabled?
        if (RenameitConfig.RenameEnabled)
        {
            sb.AppendLine("\n");
            if (DrakeRenameit.CanChangeName(item, false))
            {
                sb.AppendLine($"<color={RenameitConfig.ShiftColor}><b>Shift + Right Click to rename</b></color>");
            }
            else if (Player.m_localPlayer != null &&
                     API.RenameitPermission.IsElevatedForOverrides(Player.m_localPlayer))
            {
                sb.AppendLine(
                    $"<color={RenameitConfig.ShiftColor}><b>Shift + Right Click to rename</b></color><color=blue> Elevated override</color>");
            }
            else
            {
                var hint = RenamePermissionManager.GetTooltipDisabledHint(RenamePermissionOperation.RenameItemName, item);
                sb.AppendLine(
                    $"<color=red><s>Shift + Right Click to rename</s><br><b>{hint}</b></color>");
            }
        }
        else if (Player.m_localPlayer != null &&
                 API.RenameitPermission.IsElevatedForOverrides(Player.m_localPlayer))
        {
            sb.AppendLine(
                $"<color={RenameitConfig.ShiftColor}><b>Shift + Right Click to rename</b></color><color=blue> Elevated override (rename disabled globally)</color>");
        }

        // Config: rewrite desc enabled?
        if (RenameitConfig.RewriteDescriptionsEnabled)
        {
            if (!RenameitConfig.RenameEnabled)
                sb.AppendLine("\n");

            if (DrakeRenameit.CanChangeDesc(item, false))
            {
                sb.AppendLine(
                    $"<color={RenameitConfig.CtrlColor}><b>Ctrl + Right Click to rewrite description</b></color>");
            }
            else if (Player.m_localPlayer != null &&
                     API.RenameitPermission.IsElevatedForOverrides(Player.m_localPlayer))
            {
                sb.AppendLine(
                    $"<color={RenameitConfig.CtrlColor}><b>Ctrl + Right Click to rewrite description</b></color><color=blue> Elevated override</color>");
            }
            else
            {
                var hint = RenamePermissionManager.GetTooltipDisabledHint(RenamePermissionOperation.RewriteDescription, item);
                sb.AppendLine(
                    $"<color=red><s>Ctrl + Right Click to rewrite description</s><br><b>{hint}</b></color>");
            }
        }
        else if (Player.m_localPlayer != null &&
                 API.RenameitPermission.IsElevatedForOverrides(Player.m_localPlayer))
        {
            if (!RenameitConfig.RenameEnabled)
                sb.AppendLine("\n");
            sb.AppendLine(
                $"<color={RenameitConfig.CtrlColor}><b>Ctrl + Right Click to rewrite description</b></color><br><b><color=blue> Elevated override (descriptions disabled globally)</color></b>");
        }

        // Final set
        tooltip.Set(topic, currentText + sb, __instance.m_tooltipAnchor);
    }

    private static string UpdateDescription(ItemDrop.ItemData? item, string currentText)
    {
        if (item?.m_shared == null)
            return currentText;
        if (DrakeRenameit.hasNewDesc(item))
        {
            string customDesc = DrakeRenameit.getPropperDesc(item, item.m_shared.m_description);
            string originalDesc = item.m_shared.m_description;

            if (!string.IsNullOrEmpty(originalDesc) && currentText.Contains(originalDesc))
            {
                currentText = currentText.Replace(originalDesc, customDesc);
            }
        }

        return currentText;
    }
}

[HarmonyPatch(typeof(ItemStand))]
public static class ItemStandPatch
{
    /// <summary>ZenDragon-style and other container stands: first item in attached <see cref="Container"/>.</summary>
    private static ItemDrop.ItemData? TryGetFirstContainerItem(ItemStand stand)
    {
        var c = stand.GetComponent<Container>();
        if (c == null)
            return null;
        var inv = c.GetInventory();
        if (inv == null)
            return null;
        var items = inv.GetAllItems();
        if (items == null || items.Count == 0)
            return null;
        return items[0];
    }

    [HarmonyPatch(nameof(ItemStand.GetHoverText))]
    [HarmonyPostfix]
    [HarmonyPriority(Priority.Last)]
    static void FixItemStandHoverText(ItemStand __instance, ref string __result)
    {
        if (__instance == null || string.IsNullOrEmpty(__result))
            return;

        var item = TryGetFirstContainerItem(__instance);
        if (item?.m_shared == null)
            return;

        if (!DrakeRenameit.hasNewName(item))
            return;

        HoverRenameHelper.ApplyRenameToHoverResult(ref __result, item!);
    }

    [HarmonyPatch(nameof(ItemStand.UseItem))]
    [HarmonyPostfix]
    [HarmonyPriority(Priority.Last)]
    static void GrabItem(ItemStand __instance, Humanoid user, ItemDrop.ItemData? item)
    {
        if (item?.m_shared == null)
            return;

        string customName = DrakeRenameit.getPropperName(item);
        if (customName != item.m_shared.m_name)
        {
            var zdo = ((ZNetView)AccessTools.Field(typeof(ItemStand), "m_nview").GetValue(__instance)).GetZDO();
            zdo.Set("DrakeRenameIt_CustomName", customName);
        }
    }

    [HarmonyPatch(nameof(ItemStand.SetVisualItem))]
    [HarmonyPostfix]
    [HarmonyPriority(Priority.Last)]
    static void FixStandText(ItemStand __instance, string itemName, int variant, int quality)
    {
        var mNviewField = AccessTools.Field(typeof(ItemStand), "m_nview");
        object? nviewObj = __instance != null ? mNviewField?.GetValue(__instance) : null;
        var nview = nviewObj as ZNetView;

        if (nview == null)
            return;

        var zdo = nview.GetZDO();

        if (zdo == null) return;

        string customName = zdo.GetString("DrakeRenameIt_CustomName", "");
        if (!string.IsNullOrEmpty(customName))
        {
            var currentItemField = AccessTools.Field(typeof(ItemStand), "m_currentItemName");
            currentItemField?.SetValue(__instance, customName);
        }
    }
}