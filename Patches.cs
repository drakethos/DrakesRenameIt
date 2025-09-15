using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace DrakeRenameit;

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

            if (DrakeRenameit.hasNewName(item))
            {
                string customName = DrakeRenameit.GetPropperName(item);
                if (customName != null)
                {
                    // Replace the default name in the hover text with our rename
                    // Find the original name to swap

                    string localizedOriginalName = Localization.instance.Localize(item.m_shared.m_name);
                    string localizedCustomName = Localization.instance.Localize(customName);

                    // Replace only the first instance of originalName with customName
                    if (__result.Contains(localizedOriginalName))
                    {
                        __result = __result.Replace(localizedOriginalName, localizedCustomName);
                    }
                }
            }
        }

        [HarmonyPatch(nameof(ItemDrop.GetHoverName))]
        [HarmonyPostfix]
        static void FixHoverName(ItemDrop.ItemData? __instance, ref string __result)
        {
            if (DrakeRenameit.hasNewName(__instance))
            {
                string newName = DrakeRenameit.GetPropperName(__instance);
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
            var craftRecipe = (Recipe)AccessTools.Field(typeof(InventoryGui), "m_craftRecipe").GetValue(__instance);
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
        string topic = DrakeRenameit.GetPropperName(item);
        string currentText = item.GetTooltip();

        // Handle custom description replacement
        currentText = UpdateDescription(item, currentText);

        // Build tooltip extensions
        var sb = new System.Text.StringBuilder();

        // Config: rename enabled?
        if (RenameitConfig.RenameEnabled)
        {
            // keeps from mushin with the previous tooltip
            sb.AppendLine("\n");
            if (DrakeRenameit.CanChangeName(item, false))
            {
                sb.AppendLine($"<color={RenameitConfig.ShiftColor}><b>Shift + Right Click to rename</b></color>");
            }
            else
            {
                sb.AppendLine(
                    "<color=red><s>Shift + Right Click to rename</s><br><b>Must be owner to rename</b></color>");
            }
        }
        else if (API.RenameitPermission.IsAdminOrVIP())
        {
            sb.AppendLine(
                $"<color={RenameitConfig.ShiftColor}><b>Shift + Right Click to rename</b></color><color=blue> Disabled: Admin Override</color>");
        }

        // Config: rewrite desc enabled?
        if (RenameitConfig.RewriteDescriptionsEnabled)
        {
            // we need an extra line to not mush with the other if its disabled
            if (!RenameitConfig.RenameEnabled)
            {
                sb.AppendLine("\n");
            }

            if (DrakeRenameit.CanChangeName(item, false))
            {
                sb.AppendLine(
                    $"<color={RenameitConfig.CtrlColor}><b>Ctrl + Right Click to rewrite description</b></color>");
            }
            else
            {
                sb.AppendLine(
                    "<color=red><s>Ctrl + Right Click to rewrite description</s><br><b>Must be owner to rewrite</b></color>");
            }
        }
        else if (API.RenameitPermission.IsAdminOrVIP())
        {
            sb.AppendLine(
                $"<color={RenameitConfig.CtrlColor}><b>Ctrl + Right Click to rewrite description</b></color><br><b><color=blue> Disabled: Admin Override</color></b>");
        }

        // Final set
        tooltip.Set(topic, currentText + sb, __instance.m_tooltipAnchor);
    }

    private static string UpdateDescription(ItemDrop.ItemData? item, string currentText)
    {
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
    [HarmonyPatch(nameof(ItemStand.UseItem))]
    [HarmonyPostfix]
    static void GrabItem(ItemStand __instance, Humanoid user, ItemDrop.ItemData? item)
    {
        string customName = DrakeRenameit.getPropperName(item);
        if (customName != item.m_shared.m_name)
        {
            var zdo = ((ZNetView)AccessTools.Field(typeof(ItemStand), "m_nview").GetValue(__instance)).GetZDO();
            zdo.Set("DrakeRenameIt_CustomName", customName);
        }
    }

    [HarmonyPatch(nameof(ItemStand.SetVisualItem))]
    [HarmonyPostfix]
    static void FixStandText(ItemStand __instance, string itemName, int variant, int quality)
    {
        
        var zdo = ((ZNetView)AccessTools.Field(typeof(ItemStand), "m_nview").GetValue(__instance)).GetZDO();
        
        if (zdo == null) return;

        string customName = zdo.GetString("DrakeRenameIt_CustomName", "");
        if (!string.IsNullOrEmpty(customName))
        {
            var currentItemField = AccessTools.Field(typeof(ItemStand), "m_currentItemName");
            currentItemField.SetValue(__instance, customName);
        }
    }
}