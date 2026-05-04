using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using DrakeRenameit.Patches;
using DrakeRenameit.Permissions;
using DrakeRenameit.UI;
using HarmonyLib;
using Jotunn;
using Jotunn.Managers;
using Jotunn.Utils;
using UnityEngine;
using RenameEvents = global::DrakeRenameit.API.RenameEvents;
using RenameitPermission = global::DrakeRenameit.API.RenameitPermission;
using static DrakeRenameit.RenameitConfig;

namespace DrakeRenameit
{
    [BepInPlugin(GUID, ModName, Version)]
    [BepInDependency(Main.ModGuid)]
    [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Minor)]
    public class DrakeRenameit : BaseUnityPlugin
    {
        public const string CompanyName = "DrakeMods";
        public const string ModName = "DrakesRenameit";
        public const string Version = "0.9.10";
        public const string GUID = "com." + CompanyName + "." + ModName;
        public const string DrakeNewName = "Drake_Rename";
        public const string DrakeNewDesc = "Drake_Rename_Desc";
        public const string DrakeCraftedByDisplay = "Drake_CraftedByDisplay";
        /// <summary>When set, tooltip line uses this prefix instead of the localized <c>$item_crafter</c> label (text before “: name”).</summary>
        public const string DrakeCraftedByLineLabel = "Drake_CraftedByLineLabel";
        /// <summary>When set on a stack, <see cref="RenameitConfig.UnlockCost"/> has been paid and rename/description/crafted-by edits are allowed (if other rules pass).</summary>
        public const string DrakeRenameUnlocked = "Drake_RenameUnlocked";

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
            Bind(Config);
            ExcludedCategoryReferenceWriter.EnsureGenerated();
            AddVip();
            RenamePermissionManager.Init(Logger);
            RenameUnlockCost.Init(Logger);
            InventoryStackPatches.Apply(harmony, Logger);
            ItemTooltipPatches.Apply(harmony, Logger);
            DropHudMessagePatches.ApplyDropItemPendingCapture(harmony, Logger);
            harmony.PatchAll();
            DropHudMessagePatches.ApplyMessageHudShowMessage(harmony, Logger);
        }

        private static void AddVip()
        {
            List<string> vipList = VipList.Split(',')
                .Select(s => s.Trim().ToLowerInvariant())
                .Where(s => !string.IsNullOrEmpty(s))
                .ToList();
            RenameitPermission.AddVIP(vipList);
        }


        public static string GetPropperName(ItemDrop.ItemData? item)
        {
            if (item?.m_shared == null)
                return "";
            return getPropperName(item, item.m_shared.m_name);
        }

        /// <summary>
        /// Returns the item name for UI display, applying Drake custom name (if any) and ensuring rich-text tags are closed.
        /// Optionally localizes the result (useful for HUD messages).
        /// </summary>
        public static string GetDisplayNameForUi(ItemDrop.ItemData? item, bool localize)
        {
            if (item?.m_shared == null)
                return "";

            string raw = GetPropperName(item) ?? item.m_shared.m_name;
            string prefix = DurabilityNameModifier.GetPrefixRaw(item);
            string combined = string.IsNullOrEmpty(prefix) ? raw : prefix + " " + raw;
            string safe = UI.TooltipRichText.EnsureRichTextTagsClosedForTooltip(combined);
            if (!localize || Localization.instance == null)
                return safe;
            return Localization.instance.Localize(safe);
        }

        public static bool hasNewDesc(ItemDrop.ItemData? item)
        {
            if (item?.m_customData == null)
                return false;
            return item.m_customData.TryGetValue(DrakeNewDesc, out _);
        }

        public static bool hasNewName(ItemDrop.ItemData? item)
        {
            if (item == null || item.m_customData == null)
                return false;
            return item.m_customData.TryGetValue(DrakeNewName, out _);
        }

        /// <summary>True when the stack has any Drake rename / description / crafted-by customization (keys present with content where applicable).</summary>
        public static bool HasAnyDrakeRenameCustomization(ItemDrop.ItemData? item)
        {
            if (item?.m_customData == null)
                return false;
            if (hasNewName(item))
                return true;
            if (hasNewDesc(item))
                return true;
            if (HasCraftedByDisplayOverride(item))
                return true;
            return HasCraftedByLineLabelOverride(item);
        }

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
                    "That item is no longer in your inventory. Put it back, then unlock again.");
                return false;
            }

            if (!RenameUnlockCost.UnlockCostApplies() || IsRenameUnlocked(item))
                return false;
            if (!WouldHaveAnyDrakeEditIfUnlockIgnored(item))
            {
                string why = GetMenuBlockedReason(item);
                Player.m_localPlayer.Message(MessageHud.MessageType.Center,
                    string.IsNullOrEmpty(why) ? "This item cannot be edited." : why);
                return false;
            }

            if (!RenameUnlockCost.TryConsumeUnlockCost(Player.m_localPlayer, out var err))
            {
                Player.m_localPlayer.Message(MessageHud.MessageType.Center, err);
                return false;
            }

            SetRenameUnlocked(item);
            Player.m_localPlayer.Message(MessageHud.MessageType.Center, "Item unlocked for editing.");
            return true;
        }

        /// <summary>True when <see cref="DrakeCraftedByDisplay"/> overrides the visible crafted-by line.</summary>
        public static bool HasCraftedByDisplayOverride(ItemDrop.ItemData? item)
        {
            if (item?.m_customData == null)
                return false;
            return item.m_customData.TryGetValue(DrakeCraftedByDisplay, out var s) && !string.IsNullOrEmpty(s);
        }

        public static bool HasCraftedByLineLabelOverride(ItemDrop.ItemData? item)
        {
            if (item?.m_customData == null)
                return false;
            return item.m_customData.TryGetValue(DrakeCraftedByLineLabel, out var s) && !string.IsNullOrEmpty(s);
        }

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
                    "That item is no longer in your inventory.");
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
            item.m_customData.Remove(DrakeCraftedByDisplay);
            item.m_customData.Remove(DrakeCraftedByLineLabel);
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
            item.m_customData.Remove(DrakeNewName);
            return item.m_shared.m_name;
        }

        public static string resetDesc(ItemDrop.ItemData? item)
        {
            if (item == null)
                return "";

            item.m_customData.Remove(DrakeNewDesc);
            return item.m_shared.m_name;
        }

        public static string getPropperName(ItemDrop.ItemData? item)
        {
            if (item?.m_shared == null)
                return "";
            return getPropperName(item, item.m_shared.m_name);
        }

        public static string getPropperName(ItemDrop.ItemData? item, String defaultName)
        {
            if (item == null)
                return defaultName;
            string name;
            if (item.m_customData == null)
                item.m_customData = new Dictionary<string, string>();

            name = item.m_customData.TryGetValue(DrakeNewName, out var existing)
                ? existing
                : defaultName;
            return name;
        }

        public static string getPropperDesc(ItemDrop.ItemData? item)
        {
            if (item?.m_shared == null)
                return "";
            return getPropperDesc(item, item.m_shared.m_description);
        }

        public static string getPropperDesc(ItemDrop.ItemData? item, String defaultDesc)
        {
            if (item == null)
                return defaultDesc;
            string name;
            if (item.m_customData == null)
                item.m_customData = new Dictionary<string, string>();

            name = item.m_customData.TryGetValue(DrakeNewDesc, out var existing)
                ? existing
                : defaultDesc;
            return name;
        }

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

            if (CurrentItem.m_customData == null)
                CurrentItem.m_customData = new Dictionary<string, string>();

            if (string.IsNullOrEmpty(name))
                CurrentItem.m_customData.Remove(DrakeNewName);
            else
            {
                CurrentItem.m_customData[DrakeNewName] = name;
                RenameEvents.RaiseNameChanged(
                    Player.m_localPlayer,
                    CurrentItem,
                    CurrentItem.m_shared.m_name,
                    name);
            }

            if (NameClaimsOwner && (AllowRenameResources || RenameExclusionRules.MatchesRenameAllowlist(CurrentItem)) &&
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

            if (CurrentItem.m_customData == null)
                CurrentItem.m_customData = new Dictionary<string, string>();

            if (string.IsNullOrEmpty(name))
                CurrentItem.m_customData.Remove(DrakeNewDesc);
            else
            {
                CurrentItem.m_customData[DrakeNewDesc] = name;
                RenameEvents.RaiseDescriptionChanged(
               Player.m_localPlayer,
               CurrentItem,
               CurrentItem.m_shared.m_name,
               name);
            }
            

            if (NameClaimsOwner && (AllowRenameResources || RenameExclusionRules.MatchesRenameAllowlist(CurrentItem)) &&
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
                    "That item is no longer in your inventory. Put it back, then try again.");
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
                    "That item is no longer in your inventory. Put it back, then try again.");
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
            return RenamePermissionManager
                .Evaluate(RenamePermissionOperation.RenameItemName, item, Player.m_localPlayer, showError).Allowed;
        }

        public static bool CanChangeDesc(ItemDrop.ItemData item, bool showError = false)
        {
            return RenamePermissionManager
                .Evaluate(RenamePermissionOperation.RewriteDescription, item, Player.m_localPlayer, showError).Allowed;
        }

        public static bool CanChangeCraftedByLabel(ItemDrop.ItemData? item, bool showError = false)
        {
            if (item == null)
                return false;
            if (item.m_crafterID == 0L && string.IsNullOrEmpty(item.m_crafterName))
                return false;
            return RenamePermissionManager
                .Evaluate(RenamePermissionOperation.EditCraftedByLabel, item, Player.m_localPlayer, showError).Allowed;
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

        public static bool IsMenuOpenModifierHeld()
        {
            return MenuModifierIsShift
                ? Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)
                : Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
        }

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
            if (item == null || Player.m_localPlayer == null)
                return "";

            var parts = new System.Collections.Generic.List<string>();

            var nameResult = RenamePermissionManager.TryGetDenial(
                RenamePermissionOperation.RenameItemName, item, Player.m_localPlayer);
            if (!nameResult.Allowed)
                parts.Add(RenamePermissionManager.FormatDenialForPlayer(
                    RenamePermissionOperation.RenameItemName, nameResult.Reasons));

            var descResult = RenamePermissionManager.TryGetDenial(
                RenamePermissionOperation.RewriteDescription, item, Player.m_localPlayer);
            if (!descResult.Allowed && (parts.Count == 0 ||
                descResult.Reasons != nameResult.Reasons))
                parts.Add(RenamePermissionManager.FormatDenialForPlayer(
                    RenamePermissionOperation.RewriteDescription, descResult.Reasons));

            var craftedResult = RenamePermissionManager.TryGetDenial(
                RenamePermissionOperation.EditCraftedByLabel, item, Player.m_localPlayer);
            if (!craftedResult.Allowed &&
                (parts.Count == 0 ||
                 (craftedResult.Reasons != nameResult.Reasons && craftedResult.Reasons != descResult.Reasons)))
                parts.Add(RenamePermissionManager.FormatDenialForPlayer(
                    RenamePermissionOperation.EditCraftedByLabel, craftedResult.Reasons));

            if (parts.Count == 0)
                return "This item cannot be edited.";

            // Deduplicate and join
            var seen = new System.Collections.Generic.HashSet<string>();
            var deduped = new System.Collections.Generic.List<string>();
            foreach (var p in parts)
                if (seen.Add(p))
                    deduped.Add(p);

            return string.Join(" | ", deduped);
        }

        public static string getCraftedByDisplay(ItemDrop.ItemData? item)
        {
            if (item?.m_customData == null)
                return item?.m_crafterName ?? "";
            if (item.m_customData.TryGetValue(DrakeCraftedByDisplay, out var s) && !string.IsNullOrEmpty(s))
                return s;
            return item.m_crafterName ?? "";
        }

        public static void OpenCraftedByEditor(ItemDrop.ItemData? item)
        {
            if (InventoryGui.instance == null) return;
            if (item == null) return;
            CurrentItem = item;
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
                    "That item is no longer in your inventory. Put it back, then try again.");
                UIPanels.CloseAllRenameEditingUi();
                return;
            }

            if (CurrentItem.m_customData == null)
                CurrentItem.m_customData = new Dictionary<string, string>();

            string oldDisplay = getCraftedByDisplay(CurrentItem);
            if (string.IsNullOrEmpty(display))
                CurrentItem.m_customData.Remove(DrakeCraftedByDisplay);
            else
                CurrentItem.m_customData[DrakeCraftedByDisplay] = display;

            bool mayEditLineLabel = CraftedByLabelCustomizable ||
                                    RenameitPermission.IsElevatedForOverrides(Player.m_localPlayer);
            if (mayEditLineLabel)
            {
                var pending = UIPanels.CraftedByLineLabelPendingToken;
                if (string.IsNullOrEmpty(pending))
                    CurrentItem.m_customData.Remove(DrakeCraftedByLineLabel);
                else if (IsAllowedCustomCraftedByLineLabel(pending!))
                    CurrentItem.m_customData[DrakeCraftedByLineLabel] = pending;
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