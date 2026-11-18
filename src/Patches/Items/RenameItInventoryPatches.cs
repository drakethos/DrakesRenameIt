using System.Linq;
using System.Reflection;
using DrakeRenameit;
using DrakeRenameit.ModText;
using DrakeRenameit.Paper.Functionality;
using DrakeRenameit.Paper.Items;
using DrakeRenameit.Permissions;
using DrakeRenameit.UI;
using HarmonyLib;
using static DrakeRenameit.ModText.RenameItLocalization;
using static DrakeRenameit.RenameitConfig;
using RenameitPermission = global::DrakeRenameit.API.RenameitPermission;
using DrakeRenameit.Integration;
using DrakeModsLibs.API;
using DrakeModsLibs.Input;
using DrakeModsLibs.UI;

namespace DrakeRenameit.Patches;

[HarmonyPatch]
public static class RenameItInventoryPatches
{
    [HarmonyPatch(typeof(InventoryGui))]
    public static class PatchInventoryGuiAwake
    {
        [HarmonyPatch(nameof(InventoryGui.Awake))]
        [HarmonyPostfix]
        static void RenameGrab(InventoryGui __instance)
        {
            var original = __instance.m_playerGrid.m_onRightClick;
            __instance.m_playerGrid.m_onRightClick = (grid, item, pos) =>
            {
                // Libs tag rules with suppressRenameInventoryUi (e.g. NoRename, QuestItem, or mod-registered).
                if (item != null && CustomizeLibsAPI.IsRenameInventorySuppressed(item))
                {
                    original?.Invoke(grid, item, pos);
                    return;
                }

                if (item != null && MenuBindingRegistry.IsHeld(MenuBindingRegistry.InventoryContextScope, RenameItLibsBridge.InventoryMenuBindingId))
                {
                    if (DrakeTabHost.OpenForItem(item))
                        return;

                    // Fallback if tab host has no registration (should not happen after bridge Register).
                    if (DrakeRenameit.ShowUnlockButton(item))
                    {
                        UIPanels.OpenUnlockMenuFromInventory(item);
                        return;
                    }

                    if (DrakeRenameit.AnyInventoryActionAvailable(item))
                    {
                        UIPanels.OpenActionMenu(item);
                        return;
                    }

                    if (RenameitConfig.ShowDenialUi)
                    {
                        string reason = DrakeRenameit.GetMenuBlockedReason(item);
                        if (!string.IsNullOrEmpty(reason))
                            ValheimHudMessage.Show(Player.m_localPlayer, MessageHud.MessageType.Center, reason);
                    }
                    return;
                }

                original?.Invoke(grid, item, pos);
            };
        }

        [HarmonyPatch(nameof(InventoryGui.DoCrafting))]
        [HarmonyPostfix]
        static void FixCrafting(InventoryGui __instance, Player player)
        {
            var craftRecipe = AccessTools.Field(typeof(InventoryGui), "m_craftRecipe")?.GetValue(__instance) as Recipe;
            var oldItem = (ItemDrop.ItemData)AccessTools.Field(typeof(InventoryGui), "m_craftUpgradeItem").GetValue(__instance);
            if (craftRecipe == null || oldItem == null) return;

            var inv = player.GetInventory();
            var newItem = inv.GetItemAt(oldItem.m_gridPos.x, oldItem.m_gridPos.y);
            if (newItem == null)
            {
                newItem = inv.GetAllItems().LastOrDefault(i =>
                    i.m_shared.m_name == oldItem.m_shared.m_name && i.m_quality > oldItem.m_quality);
            }

            if (newItem != null)
            {
                foreach (var kv in oldItem.m_customData)
                    newItem.m_customData[kv.Key] = kv.Value;
            }
        }
    }

    [HarmonyPatch(typeof(InventoryGrid), nameof(InventoryGrid.CreateItemTooltip))]
    [HarmonyPriority(Priority.Last)]
    public static class InventoryGridMenuTooltipPatch
    {
        [HarmonyPostfix]
        static void AppendMenuHints(InventoryGrid __instance, ItemDrop.ItemData? item, UITooltip tooltip)
        {
            if (item?.m_shared == null || tooltip == null)
                return;

            // Libs handoff: no yellow/red RenameIt lines when inventory UI is suppressed.
            if (CustomizeLibsAPI.IsRenameInventorySuppressed(item))
                return;

            var topicField = AccessTools.Field(typeof(UITooltip), "m_topic");
            var textField = AccessTools.Field(typeof(UITooltip), "m_text");
            if (topicField == null || textField == null)
                return;

            string topic = topicField.GetValue(tooltip) as string ?? "";
            string currentText = textField.GetValue(tooltip) as string ?? "";

            // Legacy prints may still have [Copy] baked into description — scrub tooltip body.
            if (PaperCopyMark.IsCopy(item))
            {
                PaperCopyMark.StripCopyFromDescription(item);
                currentText = PaperCopyMark.StripCopyLine(currentText);
                if (topic.IndexOf(PaperCopyMark.TooltipLabel, System.StringComparison.OrdinalIgnoreCase) < 0)
                    topic = string.IsNullOrEmpty(topic)
                        ? PaperCopyMark.TooltipLabel
                        : topic.TrimEnd() + " " + PaperCopyMark.TooltipLabel;
            }

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("\n");
            AppendPaperHint(sb, item);
            string menuColor = RenameitConfig.MenuHintColor;
            string menuHint = RenameItLocalization.GetMenuTooltipHint(RenameitConfig.MenuOpenModifier);
            string lockSuffix = DrakeRenameit.GetMenuTooltipLockSuffix(item);
            bool anyAction = DrakeRenameit.AnyInventoryActionAvailable(item);
            bool elevated = Player.m_localPlayer != null &&
                              RenameitPermission.IsElevatedForOverrides(Player.m_localPlayer);

            if (anyAction)
                sb.AppendLine($"<color={menuColor}><b>{menuHint}{lockSuffix}</b></color>");
            else if (elevated)
                sb.AppendLine($"<color={menuColor}><b>{menuHint}{lockSuffix}</b></color><color=blue>{T(LKeys.TooltipElevatedOverride)}</color>");
            else if (RenameitConfig.ShowDenialUi)
            {
                string detail = BuildNoDrakeMenuHint(item);
                sb.AppendLine($"<color=red><s>{menuHint}{lockSuffix}</s></color>");
                if (!string.IsNullOrEmpty(detail))
                    sb.AppendLine($"<color=red>{detail}</color>");
            }

            tooltip.Set(topic, currentText + sb, __instance.m_tooltipAnchor);
        }

        /// <summary>Place / write instructions, above the yellow Shift + Right Click line.</summary>
        private static void AppendPaperHint(System.Text.StringBuilder sb, ItemDrop.ItemData item)
        {
            if (!PaperItem.IsAnyPaper(item))
                return;

            // [Copy] goes on the inventory tooltip title (topic), not here — and never on world hover.

            string hint = PaperItem.IsBlankPaper(item)
                ? BlankPaperHint()
                : WrittenPaperPlaceHint();
            if (string.IsNullOrWhiteSpace(hint))
                return;

            foreach (var line in hint.Split('\n'))
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;
                sb.AppendLine(TooltipRichText.OrangeBold(line.Trim()));
            }
        }

        /// <summary>
        /// Blank paper is not placeable from Use — hint Use → rename menu + write tip.
        /// Menu modifier + Right Click tip is appended separately below.
        /// </summary>
        private static string BlankPaperHint()
        {
            var use = T(LKeys.TooltipPaperBlankUse);
            var write = T(LKeys.TooltipPaperBlankWrite);
            if (string.IsNullOrWhiteSpace(use))
                return write;
            if (string.IsNullOrWhiteSpace(write))
                return use;
            return use + "\n" + write;
        }

        /// <summary>Written pages use the same hotbar orientation flag as blank paper.</summary>
        private static string WrittenPaperPlaceHint()
        {
            if (!PaperPlaceEnabled)
                return "";
            return PlaceHint();
        }

        private static string PlaceHint() =>
            BlankPaperPlaceCanToggle
                ? T(LKeys.TooltipPaperPlace)
                : T(LKeys.TooltipPaperPlaceOnly);

        private static string BuildNoDrakeMenuHint(ItemDrop.ItemData item)
        {
            var parts = new System.Collections.Generic.List<string>();
            if (!DrakeRenameit.CanChangeName(item, false))
                parts.Add(RenamePermissionManager.GetTooltipDisabledHint(RenamePermissionOperation.RenameItemName, item));
            if (!DrakeRenameit.CanChangeDesc(item, false))
                parts.Add(RenamePermissionManager.GetTooltipDisabledHint(RenamePermissionOperation.RewriteDescription, item));
            if (!DrakeRenameit.CanChangeCraftedByLabel(item, false))
                parts.Add(RenamePermissionManager.GetTooltipDisabledHint(RenamePermissionOperation.EditCraftedByLabel, item));
            return string.Join("<br>", parts.Distinct());
        }
    }
}
