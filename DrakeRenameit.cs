using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
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
        public const string Version = "1.0.0";
        public const string GUID = "com." + CompanyName + "." + ModName;
        public const string DrakeNewName = "Drake_Rename";
        public const string DrakeNewDesc = "Drake_Rename_Desc";
        public const string DrakeCraftedByDisplay = "Drake_CraftedByDisplay";
        /// <summary>When set on a stack, <see cref="RenameitConfig.UnlockCost"/> has been paid and rename/description/crafted-by edits are allowed (if other rules pass).</summary>
        public const string DrakeRenameUnlocked = "Drake_RenameUnlocked";
        public static ItemDrop.ItemData? CurrentItem { get; set; }
        private readonly Harmony harmony = new Harmony("drakesmod.DrakeRenameit");

        private Texture2D TestTex;
        private Sprite TestSprite;


        private void Awake()
        {
            Bind(Config);
            ExcludedCategoryReferenceWriter.EnsureGenerated();
            AddVip();
            RenamePermissionManager.Init(Logger);
            RenameUnlockCost.Init(Logger);

            InventoryStackPatches.Apply(harmony, Logger);
            ItemTooltipPatches.Apply(harmony, Logger);
            harmony.PatchAll();
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
            return getPropperName(item, item.m_shared.m_name);
        }

        public static bool hasNewDesc(ItemDrop.ItemData? item)
        {
            if (item.m_customData == null)
                return false;
            return item.m_customData.TryGetValue(DrakeNewDesc, out _);
        }

        public static bool hasNewName(ItemDrop.ItemData? item)
        {
            if (item == null || item.m_customData == null)
                return false;
            return item.m_customData.TryGetValue(DrakeNewName, out _);
        }

        public static bool IsRenameUnlocked(ItemDrop.ItemData? item)
        {
            if (item?.m_customData == null)
                return false;
            if (!item.m_customData.TryGetValue(DrakeRenameUnlocked, out var v))
                return false;
            return v == "1" || string.Equals(v, "true", StringComparison.OrdinalIgnoreCase);
        }

        internal static void SetRenameUnlocked(ItemDrop.ItemData item)
        {
            if (item.m_customData == null)
                item.m_customData = new Dictionary<string, string>();
            item.m_customData[DrakeRenameUnlocked] = "1";
        }

        /// <summary>Shows the Unlock button when unlock cost applies, the stack is not unlocked yet, and the player is not elevated.</summary>
        public static bool ShowUnlockButton(ItemDrop.ItemData? item)
        {
            if (item == null || Player.m_localPlayer == null)
                return false;
            if (!RenameUnlockCost.UnlockCostApplies())
                return false;
            if (IsRenameUnlocked(item))
                return false;
            return !RenameitPermission.IsElevatedForOverrides(Player.m_localPlayer);
        }

        /// <summary>Pays <see cref="RenameitConfig.UnlockCost"/> from inventory and marks the stack unlocked.</summary>
        public static bool TryPayRenameUnlock(ItemDrop.ItemData? item)
        {
            if (item == null || Player.m_localPlayer == null)
                return false;
            if (!RenameUnlockCost.UnlockCostApplies() || IsRenameUnlocked(item))
                return false;
            if (!RenameUnlockCost.TryConsumeUnlockCost(Player.m_localPlayer, out var err))
            {
                Player.m_localPlayer.Message(MessageHud.MessageType.Center, err);
                return false;
            }

            SetRenameUnlocked(item);
            Player.m_localPlayer.Message(MessageHud.MessageType.Center, "Stack unlocked for editing.");
            return true;
        }

        /// <summary>True when <see cref="DrakeCraftedByDisplay"/> overrides the visible crafted-by line.</summary>
        public static bool HasCraftedByDisplayOverride(ItemDrop.ItemData? item)
        {
            if (item?.m_customData == null)
                return false;
            return item.m_customData.TryGetValue(DrakeCraftedByDisplay, out var s) && !string.IsNullOrEmpty(s);
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
            if (CanChangeCraftedByLabel(item, false) && HasCraftedByDisplayOverride(item))
                return true;
            return false;
        }

        /// <summary>Clears custom name, description, and crafted-by display according to permissions (same as each sub-dialog reset).</summary>
        public static void ResetAllCustomizations(ItemDrop.ItemData? item)
        {
            if (item == null)
                return;
            if (CanChangeName(item, false) && hasNewName(item))
                resetName(item);
            if (CanChangeDesc(item, false) && hasNewDesc(item))
                resetDesc(item);
            if (!CanChangeCraftedByLabel(item, false) || !HasCraftedByDisplayOverride(item))
                return;
            if (item.m_customData == null)
                return;
            string oldDisplay = getCraftedByDisplay(item);
            item.m_customData.Remove(DrakeCraftedByDisplay);
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
            return getPropperName(item, item.m_shared.m_name);
        }

        public static string getPropperName(ItemDrop.ItemData? item, String defaultName)
        {
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
            return getPropperDesc(item, item.m_shared.m_description);
        }

        public static string getPropperDesc(ItemDrop.ItemData? item, String defaultDesc)
        {
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

            UIPanels.InputNamePanel.SetActive(true);
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

            RewriteItemDesc(newDesc);
            CurrentItem = null;

            UIPanels.InputDescPanel!.SetActive(false);
            UIPanels.EnsureInputUnblocked();
        }

        public static void ApplyRename(string newName)
        {
            if (CurrentItem == null) return;

            RenameItem(newName);
            CurrentItem = null;

            UIPanels.InputNamePanel!.SetActive(false);
            UIPanels.EnsureInputUnblocked();
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

            // For a locked stack: show the menu so the player can see the Unlock button and cost,
            // regardless of whether they can currently afford it. The actual affordability check
            // happens when they click Unlock (or in the confirmation panel).
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

        public static bool IsMenuOpenModifierHeld()
        {
            return MenuModifierIsShift
                ? Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)
                : Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
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
            UIPanels.InputCraftedByPanel!.SetActive(true);
            UIPanels.EnsureInputBlocked();
        }

        public static void ApplyCraftedByLabel(string display)
        {
            if (CurrentItem == null) return;

            if (CurrentItem.m_customData == null)
                CurrentItem.m_customData = new Dictionary<string, string>();

            string oldDisplay = getCraftedByDisplay(CurrentItem);
            if (string.IsNullOrEmpty(display))
                CurrentItem.m_customData.Remove(DrakeCraftedByDisplay);
            else
                CurrentItem.m_customData[DrakeCraftedByDisplay] = display;

            string newDisplay = string.IsNullOrEmpty(display) ? (CurrentItem.m_crafterName ?? "") : display;
            RenameEvents.RaiseCraftedByDisplayChanged(
                Player.m_localPlayer,
                CurrentItem,
                CurrentItem.m_shared.m_name,
                oldDisplay,
                newDisplay);

            CurrentItem = null;
            UIPanels.InputCraftedByPanel!.SetActive(false);
            UIPanels.EnsureInputUnblocked();
        }

        /// <summary>True if the item is blocked by <see cref="RenameitConfig.ExcludedNames"/> or <see cref="RenameitConfig.ExcludedCategory"/>.</summary>
        public static bool IsExcluded(ItemDrop.ItemData? item)
        {
            return RenameExclusionRules.IsExcludedFromConfig(item);
        }

        /// <summary>True if the item appears on <see cref="RenameitConfig.RenameAllowlist"/>.</summary>
        public static bool IsRenameAllowlisted(ItemDrop.ItemData? item)
        {
            return RenameExclusionRules.MatchesRenameAllowlist(item);
        }
    }
}