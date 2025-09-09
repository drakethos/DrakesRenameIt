using HarmonyLib;
using UnityEngine;

namespace DrakeRenameit;

public static class Patches
{
    [HarmonyPatch(typeof(ItemDrop))]
    public class HoverTextPatch
    {
        [HarmonyPatch(nameof(ItemDrop.GetHoverText))]
        [HarmonyPostfix]
        static void FixHoverText(ItemDrop __instance, ref string __result)
        {
            var item = __instance.m_itemData;
            if (item == null) return;

            if (DrakeRenameit.hasNewName(item))
            {
                string customName = DrakeRenameit.getPropperName(item);
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
        static void FixHoverName(ItemDrop.ItemData __instance, ref string __result)
        {
            if (DrakeRenameit.hasNewName(__instance))
            {
                string newName = DrakeRenameit.getPropperName(__instance);
                __result = newName;
            }
        }
    }

    [HarmonyPatch(typeof(InventoryGui))]
    public static class Patch_InventoryGui_Awake
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
                    if (DrakeRenameit.canChangeName(item, true))
                    {
                        DrakeRenameit.OpenRename(item);
                    }

                    return; // stop here, skip vanilla handler
                }

                if (item != null && (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.LeftControl)))
                {
                    if (DrakeRenameit.canChangeDesc(item, true))
                    {
                        DrakeRenameit.OpenRewriteDesc(item);
                    }

                    return; // stop here, skip vanilla handler
                }

                // otherwise, let vanilla delegate run
                original?.Invoke(grid, item, pos);
            };
        }
    }

    /*[HarmonyPatch(nameof(InventoryGui.SetupUpgradeItem))]
    [HarmonyPostfix]
    public static void FixCrafting(Player __instance, ref Recipe ___m_craftRecipe,
        ref ItemDrop.ItemData ___m_craftUpgradeItem)
    {
        if (___m_craftRecipe?.m_item?.m_itemData == null)
            return;
        Debug.Log($"[DrakeNewName] Attempting rename:");
        var upgradedItem = ___m_craftRecipe.m_item.m_itemData;

        // If we had a custom rename on the base item, preserve it
        var customName = DrakeRenameit.getPropperName(___m_craftUpgradeItem, null);
        if (customName != null)
        {
            DrakeRenameit.currentItem = upgradedItem;
            DrakeRenameit.renameItem(customName);
        }
    }
}*/

    [HarmonyPatch(typeof(InventoryGrid), nameof(InventoryGrid.CreateItemTooltip))]
    public static class InventoryGridTooltipPatch
    {
        [HarmonyPostfix]
        static void UpdateToolTip(InventoryGrid __instance, ItemDrop.ItemData item, UITooltip tooltip)
        {
            if (item == null || tooltip == null || Player.m_localPlayer == null)
                return;

            string topic = DrakeRenameit.getPropperName(item);
            string currentText = item.GetTooltip();
         
            // Handle custom description replacement
            if (DrakeRenameit.hasNewDesc(item))
            {
                string customDesc = DrakeRenameit.getPropperDesc(item, item.m_shared.m_description);
                string originalDesc = item.m_shared.m_description;

                if (!string.IsNullOrEmpty(originalDesc) && currentText.Contains(originalDesc))
                {
                    currentText = currentText.Replace(originalDesc, customDesc);
                }
            }

            // Build tooltip extensions
            var sb = new System.Text.StringBuilder();
           
            // Config: rename enabled?
            if (RenameitConfig.RenameEnabled)
            {
                // keeps from mushin with the previous tooltip
                sb.AppendLine("\n");
                if (DrakeRenameit.canChangeName(item, false))
                    sb.AppendLine($"<color={RenameitConfig.ShiftColor}><b>Shift + Right Click to rename</b></color>");
                else
                    sb.AppendLine(
                        "<color=red><s>Shift + Right Click to rename</s><br><b>Must be owner to rename</b></color>");
            }
            else if (API.RenameitPermission.IsAdminOrVIP())
            {
                sb.AppendLine($"<color={RenameitConfig.ShiftColor}><b>Shift + Right Click to rename</b></color><color=blue>Disabled: Admin Override</color>");
            }

            // Config: rewrite desc enabled?
            if (RenameitConfig.RewriteDescriptionsEnabled)
            {
                // we need an extra line to not mush with the other if its disabled
                if (!RenameitConfig.RenameEnabled)
                {
                    sb.AppendLine("\n");
                }
                if (DrakeRenameit.canChangeName(item, false))
                    sb.AppendLine("<color=yellow><b>Ctrl + Right Click to rewrite description</b></color>");
                if (API.RenameitPermission.IsAdminOrVIP())
                {
                    sb.Append("<color=blue>not owner- Admin Override</color>");
                }
                else
                    sb.AppendLine(
                        "<color=red><s>Ctrl + Right Click to rewrite description</s><br><b>Must be owner to rewrite</b></color>");
            }
            else if (API.RenameitPermission.IsAdminOrVIP())
            {
                sb.AppendLine("<color=yellow><b>Ctrl + Right Click to rewrite description</b></color><color=blue>Disabled: Admin Override</color>");
            }

            // Final set
            tooltip.Set(topic, currentText + sb, __instance.m_tooltipAnchor);
        }
    }


    /*[HarmonyPatch(typeof(Inventory))]
    public static class Patch_Inventory_StackLookup
    {
        // Override FindFreeStackSpace
        [HarmonyPatch(nameof(Inventory.FindFreeStackSpace))]
        [HarmonyPrefix]
        static bool FindFreeStackSpacePrefix(Inventory __instance, string name, float worldLevel, ref int __result)
        {
            int freeStackSpace = 0;

            foreach (var itemData in __instance.m_inventory)
            {
                // Use your custom lookup instead of m_shared.m_name
                string itemName = DrakeRenameit.getPropperName(itemData);

                if (itemName == name && itemData.m_stack < itemData.m_shared.m_maxStackSize &&
                    itemData.m_worldLevel == worldLevel)
                {
                    freeStackSpace += itemData.m_shared.m_maxStackSize - itemData.m_stack;
                }
            }

            __result = freeStackSpace;
            return false; // skip original
        }

        // Override FindFreeStackItem
        [HarmonyPatch(nameof(Inventory.FindFreeStackItem))]
        [HarmonyPrefix]
        static bool FindFreeStackItemPrefix(Inventory __instance, string name, int quality, float worldLevel,
            ref ItemDrop.ItemData __result)
        {
            if (RenameitConfig.SeperateStacks)
            {
                foreach (var itemData in __instance.m_inventory)
                {
                    // Use your custom lookup instead of m_shared.m_name
                    string itemName = DrakeRenameit.getPropperName(itemData);

                    if (itemName == name && itemData.m_quality == quality &&
                        itemData.m_stack < itemData.m_shared.m_maxStackSize && itemData.m_worldLevel == worldLevel)
                    {
                        __result = itemData;
                        return false; // skip original
                    }
                }

                __result = null;
                return false; // skip original
            }

            return true;
        }
    }*/
}