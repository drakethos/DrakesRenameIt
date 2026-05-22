using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using DrakeRenameit.Integration;
using DrakesWorkshopLibs;
using DrakesWorkshopLibs.API;
using DrakesWorkshopLibs.Data;
using DrakeRenameit.Permissions;
using DrakeRenameit.UI;
using HarmonyLib;
using Jotunn;
using Jotunn.Managers;
using Jotunn.Utils;
using UnityEngine;
using DrakeRenameit.ModText;
using RenameEvents = global::DrakeRenameit.API.RenameEvents;
using RenameitPermission = global::DrakeRenameit.API.RenameitPermission;
using static DrakeRenameit.ModText.RenameItLocalization;
using static DrakeRenameit.RenameitConfig;

namespace DrakeRenameit
{
    [BepInPlugin(GUID, ModName, Version)]
    [BepInDependency(Main.ModGuid)]
    [BepInDependency(CustomizeLibsPlugin.GUID)]
    [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Minor)]
    public partial class DrakeRenameit : BaseUnityPlugin
    {
        public const string DrakeNewName = DrakeCustomDataKeys.Rename;
        public const string DrakeNewDesc = DrakeCustomDataKeys.RenameDescription;
        public const string DrakeCraftedByDisplay = DrakeCustomDataKeys.CraftedByDisplay;
        /// <summary>When set, tooltip line uses this prefix instead of the localized <c>$item_crafter</c> label (text before “: name”).</summary>
        public const string DrakeCraftedByLineLabel = DrakeCustomDataKeys.CraftedByLineLabel;
        /// <summary>When set on a stack, <see cref="RenameitConfig.UnlockCost"/> has been paid and rename/description/crafted-by edits are allowed (if other rules pass).</summary>
        public const string DrakeRenameUnlocked = DrakeCustomDataKeys.RenameUnlocked;

        /// <summary>Inventory tooltip suffix when unlock cost applies but this item is not paid yet.</summary>
        public const string TooltipUnlockCostLockedEmoji = "\uD83D\uDD12";

        /// <summary>Inventory tooltip suffix when the stack is paid-unlocked or the local player is elevated — pen/nib reads clearly; Valheim fonts often render open-lock like closed-lock.</summary>
        public const string TooltipDrakeEditableEmoji = "\U0001F58A\uFE0F";
        public static ItemDrop.ItemData? CurrentItem { get; set; }
        private readonly Harmony harmony = new Harmony("drakesmod.DrakeRenameit");

        /// <summary>
        /// True when <paramref name="item"/> is the same instance currently held in the local player's inventory.
        /// Used to avoid paying or writing custom data to a stale <see cref="ItemDrop.ItemData"/> after the stack was dropped or moved.
        /// </summary>
        public static bool IsItemInLocalPlayerInventory(ItemDrop.ItemData? item)
        {
            if (item == null || Player.m_localPlayer == null)
                return false;
            var inv = Player.m_localPlayer.GetInventory();
            return inv != null && inv.ContainsItem(item);
        }



        private void Awake()
        {
            RenameItLocalization.Init(this, Logger);
            RenameitConfig.Log = Logger;
            Bind(Config);
            ExcludedCategoryReferenceWriter.EnsureGenerated();
            RenamePermissionManager.Init(Logger);
            RenameUnlockCost.Init(Logger);
            RenameItLibsBridge.Register();
            harmony.PatchAll();
        }

        public static string GetPropperName(ItemDrop.ItemData? item) => CustomizeLibsAPI.GetProperName(item);

        /// <summary>
        /// Returns the item name for UI display, applying Drake custom name (if any) and ensuring rich-text tags are closed.
        /// Optionally localizes the result (useful for HUD messages).
        /// </summary>
        public static string GetDisplayNameForUi(ItemDrop.ItemData? item, bool localize) =>
            CustomizeLibsAPI.GetDisplayNameForUi(item, localize);

        public static bool hasNewDesc(ItemDrop.ItemData? item) =>
            CustomizeLibsAPI.HasCustomDescription(item);

        public static bool hasNewName(ItemDrop.ItemData? item) =>
            CustomizeLibsAPI.HasCustomName(item);

        /// <summary>True when the stack has any Drake rename / description / crafted-by customization (keys present with content where applicable).</summary>
        public static bool HasAnyDrakeRenameCustomization(ItemDrop.ItemData? item) =>
            CustomizeLibsAPI.HasAnyCustomization(item);

        static bool ReadRenameUnlockedFlag(ItemDrop.ItemData item)
        {
            if (!item.m_customData.TryGetValue(DrakeRenameUnlocked, out var v))
                return false;
            return v == "1" || string.Equals(v, "true", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// When <see cref="RenameitConfig.UnlockCostEnabled"/> applies: stacks that already carry Drake custom data but no unlock flag
        /// are treated as unlocked and the flag is written once (pre-unlock-cost worlds, config toggles, or imports).
        /// When unlock cost is off, only the explicit flag counts (legacy behavior).
        /// </summary>
        public static bool IsRenameUnlocked(ItemDrop.ItemData? item)
        {
            if (item?.m_customData == null)
                return false;

            bool hasFlag = ReadRenameUnlockedFlag(item);

            if (RenameUnlockCost.UnlockCostApplies())
            {
                if (!hasFlag && HasAnyDrakeRenameCustomization(item))
                {
                    SetRenameUnlocked(item);
                    return true;
                }

                return hasFlag;
            }

            return hasFlag;
        }

        internal static void SetRenameUnlocked(ItemDrop.ItemData item)
        {
            if (item.m_customData == null)
                item.m_customData = new Dictionary<string, string>();
            item.m_customData[DrakeRenameUnlocked] = "1";
        }

        /// <summary>
        /// True if at least one rename / description / crafted-by edit would be allowed when the unlock-cost gate is ignored
        /// (same test used to decide whether a locked stack may open the action menu).
        /// </summary>
        public static bool WouldHaveAnyDrakeEditIfUnlockIgnored(ItemDrop.ItemData? item)
        {
            if (item == null || Player.m_localPlayer == null)
                return false;
            RenamePermissionManager.BeginIgnoreUnlockRequirement();
            try
            {
                return CanChangeName(item, false) || CanChangeDesc(item, false) || CanChangeCraftedByLabel(item, false);
            }
            finally
            {
                RenamePermissionManager.EndIgnoreUnlockRequirement();
            }
        }

        /// <summary>
        /// Shows the Unlock affordance when unlock cost applies, the stack is not unlocked yet, the player is not elevated,
        /// and paying would actually enable at least one edit (otherwise players pay and still see all actions denied).
        /// </summary>
        public static bool ShowUnlockButton(ItemDrop.ItemData? item)
        {
            if (item == null || Player.m_localPlayer == null)
                return false;
            if (!RenameUnlockCost.UnlockCostApplies())
                return false;
            if (IsRenameUnlocked(item))
                return false;
            if (RenameitPermission.IsElevatedForOverrides(Player.m_localPlayer))
                return false;
            return WouldHaveAnyDrakeEditIfUnlockIgnored(item);
        }

        /// <summary>Pays <see cref="RenameitConfig.UnlockCost"/> from inventory and marks the stack unlocked.</summary>
        public static bool TryPayRenameUnlock(ItemDrop.ItemData? item)
        {
            if (item == null || Player.m_localPlayer == null)
                return false;
            if (!IsItemInLocalPlayerInventory(item))
            {
                Player.m_localPlayer.Message(MessageHud.MessageType.Center,
                    T(LKeys.MsgItemNotInInventoryUnlock));
                return false;
            }

            if (!RenameUnlockCost.UnlockCostApplies() || IsRenameUnlocked(item))
                return false;
            if (!WouldHaveAnyDrakeEditIfUnlockIgnored(item))
            {
                if (RenameitConfig.ShowDenialUi)
                {
                    string why = GetMenuBlockedReason(item);
                    if (!string.IsNullOrEmpty(why))
                        Player.m_localPlayer.Message(MessageHud.MessageType.Center, why);
                }

                return false;
            }

            if (!RenameUnlockCost.TryConsumeUnlockCost(Player.m_localPlayer, out var err))
            {
                Player.m_localPlayer.Message(MessageHud.MessageType.Center, err);
                return false;
            }

            SetRenameUnlocked(item);
            Player.m_localPlayer.Message(MessageHud.MessageType.Center, T(LKeys.MsgItemUnlocked));
            return true;
        }

        /// <summary>True when <see cref="DrakeCraftedByDisplay"/> overrides the visible crafted-by line.</summary>
        public static bool HasCraftedByDisplayOverride(ItemDrop.ItemData? item) =>
            CustomizeLibsAPI.HasCraftedByDisplayOverride(item);

        public static bool HasCraftedByLineLabelOverride(ItemDrop.ItemData? item) =>
            CustomizeLibsAPI.HasCraftedByLineLabelOverride(item);

        /// <summary>True if the player may clear at least one Drake customization that is currently set on the item.</summary>
        public static bool CanResetAnyCustomization(ItemDrop.ItemData? item)
        {
            if (item == null)
                return false;
            if (CanChangeName(item, false) && hasNewName(item))
                return true;
            if (CanChangeDesc(item, false) && hasNewDesc(item))
                return true;
            if (CanChangeCraftedByLabel(item, false) &&
                (HasCraftedByDisplayOverride(item) || HasCraftedByLineLabelOverride(item)))
                return true;
            return false;
        }

        /// <summary>Clears custom name, description, and crafted-by display according to permissions (same as each sub-dialog reset).</summary>
        public static void ResetAllCustomizations(ItemDrop.ItemData? item)
        {
            if (item == null)
                return;
            if (!IsItemInLocalPlayerInventory(item))
            {
                Player.m_localPlayer?.Message(MessageHud.MessageType.Center,
                    T(LKeys.MsgItemNotInInventory));
                return;
            }

            if (CanChangeName(item, false) && hasNewName(item))
                resetName(item);
            if (CanChangeDesc(item, false) && hasNewDesc(item))
                resetDesc(item);
            if (!CanChangeCraftedByLabel(item, false) ||
                (!HasCraftedByDisplayOverride(item) && !HasCraftedByLineLabelOverride(item)))
                return;
            if (item.m_customData == null)
                return;
            string oldDisplay = getCraftedByDisplay(item);
            CustomizeLibsAPI.ClearCraftedByOverrides(item);
            string newDisplay = item.m_crafterName ?? "";
            RenameEvents.RaiseCraftedByDisplayChanged(
                Player.m_localPlayer,
                item,
                item.m_shared.m_name,
                oldDisplay,
                newDisplay);
        }

        public static string resetName(ItemDrop.ItemData? item)
        {
            if (item == null)
                return "";

            string oldDisplay = GetPropperName(item);
            CustomizeLibsAPI.SetCustomName(item, null);
            string newDisplay = GetPropperName(item);
            if (!string.Equals(oldDisplay, newDisplay, StringComparison.Ordinal))
            {
                RenameEvents.RaiseNameChanged(Player.m_localPlayer, item, oldDisplay, newDisplay);
            }

            return newDisplay;
        }

        public static string resetDesc(ItemDrop.ItemData? item)
        {
            if (item == null)
                return "";

            string oldDisplay = getPropperDesc(item);
            CustomizeLibsAPI.SetCustomDescription(item, null);
            string newDisplay = getPropperDesc(item);
            if (!string.Equals(oldDisplay, newDisplay, StringComparison.Ordinal))
            {
                RenameEvents.RaiseDescriptionChanged(Player.m_localPlayer, item, oldDisplay, newDisplay);
            }

            return newDisplay;
        }

        public static string getPropperName(ItemDrop.ItemData? item)
        {
            if (item?.m_shared == null)
                return "";
            return getPropperName(item, item.m_shared.m_name);
        }

        public static string getPropperName(ItemDrop.ItemData? item, String defaultName) =>
            CustomizeLibsAPI.GetProperName(item, defaultName);

        public static string getPropperDesc(ItemDrop.ItemData? item)
        {
            if (item?.m_shared == null)
                return "";
            return getPropperDesc(item, item.m_shared.m_description);
        }

        public static string getPropperDesc(ItemDrop.ItemData? item, String defaultDesc) =>
            CustomizeLibsAPI.GetProperDescription(item, defaultDesc);

        public static void OpenRename(ItemDrop.ItemData? item)
        {
            if (InventoryGui.instance == null) return;
            if (item == null) return;
            CurrentItem = item;
            if (UIPanels.InputNamePanel == null)
            {
                UIPanels.CreateRenameInput();
            }

            string startName = GetPropperName(item);
            UIPanels.RenameNameInput!.text = startName;

            UIPanels.InputNamePanel!.SetActive(true);
            UIPanels.EnsureInputBlocked();
        }

        public static void OpenRewriteDesc(ItemDrop.ItemData? item)
        {
            if (InventoryGui.instance == null) return;
            if (item == null) return;
            CurrentItem = item;
            if (UIPanels.InputDescPanel == null)
            {
                UIPanels.CreateRenameDescInput();
            }

            string startDesc = getPropperDesc(item);
            UIPanels.RenameDescInput!.text = startDesc;

            UIPanels.InputDescPanel!.SetActive(true);
            UIPanels.EnsureInputBlocked();
        }

        public static void RenameItem(String name)
        {
            if (CurrentItem == null) return;

            Player? player = Player.m_localPlayer;
            string oldDisplay = GetPropperName(CurrentItem);

            if (string.IsNullOrEmpty(name))
            {
                if (!hasNewName(CurrentItem))
                    return;

                CustomizeLibsAPI.SetCustomName(CurrentItem, null);
                string newDisplay = GetPropperName(CurrentItem);
                if (!string.Equals(oldDisplay, newDisplay, StringComparison.Ordinal))
                {
                    RenameEvents.RaiseNameChanged(player, CurrentItem, oldDisplay, newDisplay);
                }
            }
            else if (!string.Equals(oldDisplay, name, StringComparison.Ordinal))
            {
                CustomizeLibsAPI.SetCustomName(CurrentItem, name);
                RenameEvents.RaiseNameChanged(player, CurrentItem, oldDisplay, name);
            }

            if (NameClaimsOwner && (AllowRenameUnownedItems || RenameExclusionRules.MatchesRenameAllowlist(CurrentItem)) &&
                String.IsNullOrEmpty(CurrentItem.m_crafterName))
            {
                Player localPlayer = Player.m_localPlayer;
                if (localPlayer != null)
                {
                    CurrentItem.m_crafterID = localPlayer.GetPlayerID();
                    CurrentItem.m_crafterName = localPlayer.GetPlayerName();
                }
            }
        }

        public static void RewriteItemDesc(String name)
        {
            if (CurrentItem == null) return;

            Player? player = Player.m_localPlayer;
            string oldDisplay = getPropperDesc(CurrentItem);

            if (string.IsNullOrEmpty(name))
            {
                if (!hasNewDesc(CurrentItem))
                    return;

                CustomizeLibsAPI.SetCustomDescription(CurrentItem, null);
                string newDisplay = getPropperDesc(CurrentItem);
                if (!string.Equals(oldDisplay, newDisplay, StringComparison.Ordinal))
                {
                    RenameEvents.RaiseDescriptionChanged(player, CurrentItem, oldDisplay, newDisplay);
                }
            }
            else if (!string.Equals(oldDisplay, name, StringComparison.Ordinal))
            {
                CustomizeLibsAPI.SetCustomDescription(CurrentItem, name);
                RenameEvents.RaiseDescriptionChanged(player, CurrentItem, oldDisplay, name);
            }
            

            if (NameClaimsOwner && (AllowRenameUnownedItems || RenameExclusionRules.MatchesRenameAllowlist(CurrentItem)) &&
                String.IsNullOrEmpty(CurrentItem.m_crafterName))
            {
                Player localPlayer = Player.m_localPlayer;
                if (localPlayer != null)
                {
                    CurrentItem.m_crafterID = localPlayer.GetPlayerID();
                    CurrentItem.m_crafterName = localPlayer.GetPlayerName();
                }
            }
        }


        public static void ApplyRewriteDesc(string newDesc)
        {
            if (CurrentItem == null) return;
            if (!IsItemInLocalPlayerInventory(CurrentItem))
            {
                Player.m_localPlayer?.Message(MessageHud.MessageType.Center,
                    T(LKeys.MsgItemNotInInventoryApply));
                UIPanels.CloseAllRenameEditingUi();
                return;
            }

            RewriteItemDesc(newDesc);
            var item = CurrentItem;
            UIPanels.InputDescPanel!.SetActive(false);
            UIPanels.OpenActionMenu(item);
        }

        public static void ApplyRename(string newName)
        {
            if (CurrentItem == null) return;
            if (!IsItemInLocalPlayerInventory(CurrentItem))
            {
                Player.m_localPlayer?.Message(MessageHud.MessageType.Center,
                    T(LKeys.MsgItemNotInInventoryApply));
                UIPanels.CloseAllRenameEditingUi();
                return;
            }

            RenameItem(newName);
            var item = CurrentItem;
            UIPanels.InputNamePanel!.SetActive(false);
            UIPanels.OpenActionMenu(item);
        }

        public static bool CanChangeName(ItemDrop.ItemData? item, bool showError = false)
        {
            if (!CustomizeLibsAPI.CanPerform(CustomizeOperation.RenameName, item, Player.m_localPlayer))
            {
                if (showError)
                    RenamePermissionManager.Evaluate(RenamePermissionOperation.RenameItemName, item, Player.m_localPlayer, true);
                return false;
            }
            return true;
        }

        public static bool CanChangeDesc(ItemDrop.ItemData item, bool showError = false)
        {
            if (!CustomizeLibsAPI.CanPerform(CustomizeOperation.RenameDescription, item, Player.m_localPlayer))
            {
                if (showError)
                    RenamePermissionManager.Evaluate(RenamePermissionOperation.RewriteDescription, item, Player.m_localPlayer, true);
                return false;
            }
            return true;
        }

        public static bool CanChangeCraftedByLabel(ItemDrop.ItemData? item, bool showError = false)
        {
            if (item == null)
                return false;
            if (!CustomizeLibsAPI.CanPerform(CustomizeOperation.EditCraftedBy, item, Player.m_localPlayer))
            {
                if (showError)
                    RenamePermissionManager.Evaluate(RenamePermissionOperation.EditCraftedByLabel, item, Player.m_localPlayer, true);
                return false;
            }
            return true;
        }

        /// <summary>
        /// True if the action menu should open for this item.
        /// When an unlock cost applies and the stack is not yet unlocked, the menu still opens
        /// (to show the Unlock button and its cost) regardless of whether the player can afford it.
        /// </summary>
        public static bool AnyInventoryActionAvailable(ItemDrop.ItemData? item)
        {
            if (item == null || Player.m_localPlayer == null)
                return false;

            // If no unlock gate, or already unlocked, check normal permissions
            if (!RenameUnlockCost.UnlockCostApplies() || IsRenameUnlocked(item))
                return CanChangeName(item, false) || CanChangeDesc(item, false) || CanChangeCraftedByLabel(item, false);

            // Elevated users skip the gate entirely
            if (RenameitPermission.IsElevatedForOverrides(Player.m_localPlayer))
                return CanChangeName(item, false) || CanChangeDesc(item, false) || CanChangeCraftedByLabel(item, false);

            // For a locked stack: show the menu only if at least one edit would be allowed after paying unlock
            // (same rule as ShowUnlockButton — never trap players into paying for zero usable actions).
            return WouldHaveAnyDrakeEditIfUnlockIgnored(item);
        }

        public static bool IsMenuOpenModifierHeld() =>
            DrakesWorkshopLibs.Input.MenuBindingRegistry.IsHeld(
                DrakesWorkshopLibs.Input.MenuBindingRegistry.InventoryContextScope,
                Integration.RenameItLibsBridge.InventoryMenuBindingId);

        /// <summary>Suffix for inventory tooltip when <see cref="RenameitConfig.UnlockCost"/> applies: 🔒 until paid, then 🖊️ (open-lock glyphs are easy to confuse with locked in Valheim fonts).</summary>
        public static string GetMenuTooltipLockSuffix(ItemDrop.ItemData? item)
        {
            if (item == null || Player.m_localPlayer == null)
                return "";
            if (!RenameUnlockCost.UnlockCostApplies())
                return "";
            if (RenameitPermission.IsElevatedForOverrides(Player.m_localPlayer))
                return " " + TooltipDrakeEditableEmoji;
            return IsRenameUnlocked(item) ? " " + TooltipDrakeEditableEmoji : " " + TooltipUnlockCostLockedEmoji;
        }

        /// <summary>Returns a player-facing reason string for why the action menu cannot open for this item.
        /// Used to show a <see cref="MessageHud"/> message when the modifier is held but the menu is blocked.</summary>
        public static string GetMenuBlockedReason(ItemDrop.ItemData? item)
        {
            if (!RenameitConfig.ShowDenialUi)
                return "";
            if (item == null || Player.m_localPlayer == null)
                return "";

            var parts = new System.Collections.Generic.List<string>();

            var nameResult = RenamePermissionManager.TryGetDenial(
                RenamePermissionOperation.RenameItemName, item, Player.m_localPlayer);
            if (!nameResult.Allowed && RenamePermissionManager.HasAccessDenial(nameResult.Reasons))
                parts.Add(RenamePermissionManager.FormatDenialForPlayer(
                    RenamePermissionOperation.RenameItemName, nameResult.Reasons));

            var descResult = RenamePermissionManager.TryGetDenial(
                RenamePermissionOperation.RewriteDescription, item, Player.m_localPlayer);
            if (!descResult.Allowed && RenamePermissionManager.HasAccessDenial(descResult.Reasons) &&
                (parts.Count == 0 || descResult.Reasons != nameResult.Reasons))
                parts.Add(RenamePermissionManager.FormatDenialForPlayer(
                    RenamePermissionOperation.RewriteDescription, descResult.Reasons));

            var craftedResult = RenamePermissionManager.TryGetDenial(
                RenamePermissionOperation.EditCraftedByLabel, item, Player.m_localPlayer);
            if (!craftedResult.Allowed && RenamePermissionManager.HasAccessDenial(craftedResult.Reasons) &&
                (parts.Count == 0 ||
                 (craftedResult.Reasons != nameResult.Reasons && craftedResult.Reasons != descResult.Reasons)))
                parts.Add(RenamePermissionManager.FormatDenialForPlayer(
                    RenamePermissionOperation.EditCraftedByLabel, craftedResult.Reasons));

            if (parts.Count == 0)
                return T(LKeys.MsgCannotEditItem);

            // Deduplicate and join
            var seen = new System.Collections.Generic.HashSet<string>();
            var deduped = new System.Collections.Generic.List<string>();
            foreach (var p in parts)
                if (seen.Add(p))
                    deduped.Add(p);

            return string.Join(" | ", deduped);
        }

        public static string getCraftedByDisplay(ItemDrop.ItemData? item) =>
            CustomizeLibsAPI.GetCraftedByDisplay(item);

        /// <summary>
        /// Stacks with no crafter yet cannot show a crafted-by line until someone is assigned — claim for the local
        /// player when they open the editor (permission already validated).
        /// </summary>
        private static void EnsureLocalPlayerCrafterIfAbsent(ItemDrop.ItemData item)
        {
            var local = Player.m_localPlayer;
            if (local == null)
                return;
            if (item.m_crafterID != 0L || !string.IsNullOrEmpty(item.m_crafterName))
                return;
            item.m_crafterID = local.GetPlayerID();
            item.m_crafterName = local.GetPlayerName();
        }

        public static void OpenCraftedByEditor(ItemDrop.ItemData? item)
        {
            if (InventoryGui.instance == null) return;
            if (item == null) return;
            CurrentItem = item;
            EnsureLocalPlayerCrafterIfAbsent(item);
            if (UIPanels.InputCraftedByPanel == null)
                UIPanels.CreateCraftedByInput();
            UIPanels.RenameCraftedByInput!.text = getCraftedByDisplay(item);
            UIPanels.RefreshCraftedByLineLabelPicker(item);
            UIPanels.InputCraftedByPanel!.SetActive(true);
            UIPanels.EnsureInputBlocked();
        }

        public static void ApplyCraftedByLabel(string display)
        {
            if (CurrentItem == null) return;
            if (!IsItemInLocalPlayerInventory(CurrentItem))
            {
                Player.m_localPlayer?.Message(MessageHud.MessageType.Center,
                    T(LKeys.MsgItemNotInInventoryApply));
                UIPanels.CloseAllRenameEditingUi();
                return;
            }

            EnsureLocalPlayerCrafterIfAbsent(CurrentItem);

            string oldDisplay = getCraftedByDisplay(CurrentItem);
            CustomizeLibsAPI.SetCraftedByDisplay(CurrentItem, display);

            bool mayEditLineLabel = CraftedByLabelCustomizable ||
                                    RenameitPermission.IsElevatedForOverrides(Player.m_localPlayer);
            if (mayEditLineLabel)
            {
                var pending = UIPanels.CraftedByLineLabelPendingToken;
                if (string.IsNullOrEmpty(pending))
                    CustomizeLibsAPI.SetCraftedByLineLabel(CurrentItem, null);
                else if (IsAllowedCustomCraftedByLineLabel(pending!))
                    CustomizeLibsAPI.SetCraftedByLineLabel(CurrentItem, pending);
            }

            string newDisplay = string.IsNullOrEmpty(display) ? (CurrentItem.m_crafterName ?? "") : display;
            RenameEvents.RaiseCraftedByDisplayChanged(
                Player.m_localPlayer,
                CurrentItem,
                CurrentItem.m_shared.m_name,
                oldDisplay,
                newDisplay);

            var item = CurrentItem;
            UIPanels.InputCraftedByPanel!.SetActive(false);
            UIPanels.OpenActionMenu(item);
        }

        /// <summary>True if the item is blocked by <see cref="RenameitConfig.ExcludedNames"/>, <see cref="RenameitConfig.ExcludedCategory"/>, or (when <see cref="RenameitConfig.ExcludeStacks"/>) stackable items. Does not consider <see cref="RenameitConfig.RenameAllowlist"/>.</summary>
        public static bool IsExcluded(ItemDrop.ItemData? item)
        {
            return RenameExclusionRules.IsExcludedFromConfig(item);
        }

        /// <summary>True if the item appears on <see cref="RenameitConfig.RenameAllowlist"/>.</summary>
        public static bool IsRenameAllowlisted(ItemDrop.ItemData? item)
        {
            return RenameExclusionRules.MatchesRenameAllowlist(item);
        }

        static bool IsAllowedCustomCraftedByLineLabel(string value)
        {
            var options = GetCraftedByAllowedLabelsList();
            for (int i = 1; i < options.Count; i++)
            {
                if (string.Equals(options[i], value, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }
    }
}