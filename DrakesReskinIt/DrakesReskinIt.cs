using System.Collections.Generic;
using System.Linq;
using BepInEx;
using HarmonyLib;
using Jotunn;
using Jotunn.Managers;
using Jotunn.Utils;
using UnityEngine;
using ReskinEvents = global::DrakesReskinIt.API.ReskinEvents;
using ReskinItPermission = global::DrakesReskinIt.API.ReskinItPermission;
using static DrakesReskinIt.ReskinItConfig;

namespace DrakesReskinIt
{
    [BepInPlugin(GUID, ModName, Version)]
    [BepInDependency(Main.ModGuid)]
    [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Minor)]
    public class DrakesReskinIt : BaseUnityPlugin
    {
        public const string CompanyName = "DrakeMods";
        public const string ModName = "DrakesReskinIt";
        public const string Version = "1.0.0";
        public const string GUID = "com." + CompanyName + "." + ModName;

        /// <summary>Custom data key: path/name of the icon asset to use for this item stack.</summary>
        public const string DrakeCustomIcon = "Drake_CustomIcon";

        /// <summary>Custom data key: a Unity-style HTML color string applied as a tint to the icon (e.g. "#ff4400" or "red").</summary>
        public const string DrakeIconTint = "Drake_IconTint";

        public static ItemDrop.ItemData? CurrentItem { get; set; }

        private readonly Harmony harmony = new Harmony("drakesmod.DrakesReskinIt");

        private void Awake()
        {
            Bind(Config);
            AddVip();
            ReskinPermissionManager.Init(Logger);
            IconRegistry.Init(Logger);

            ReskinPatches.Apply(harmony, Logger);
            harmony.PatchAll();
        }

        private static void AddVip()
        {
            List<string> vipList = VipList.Split(',')
                .Select(s => s.Trim().ToLowerInvariant())
                .Where(s => !string.IsNullOrEmpty(s))
                .ToList();
            ReskinItPermission.AddVIP(vipList);
        }

        // ─── Icon custom data helpers ─────────────────────────────────────────

        public static bool HasCustomIcon(ItemDrop.ItemData? item)
        {
            if (item?.m_customData == null) return false;
            return item.m_customData.TryGetValue(DrakeCustomIcon, out var v) && !string.IsNullOrEmpty(v);
        }

        public static string? GetCustomIconName(ItemDrop.ItemData? item)
        {
            if (item?.m_customData == null) return null;
            item.m_customData.TryGetValue(DrakeCustomIcon, out var v);
            return string.IsNullOrEmpty(v) ? null : v;
        }

        public static bool HasIconTint(ItemDrop.ItemData? item)
        {
            if (item?.m_customData == null) return false;
            return item.m_customData.TryGetValue(DrakeIconTint, out var v) && !string.IsNullOrEmpty(v);
        }

        public static string? GetIconTint(ItemDrop.ItemData? item)
        {
            if (item?.m_customData == null) return null;
            item.m_customData.TryGetValue(DrakeIconTint, out var v);
            return string.IsNullOrEmpty(v) ? null : v;
        }

        // ─── Sprite / tint resolution ─────────────────────────────────────────

        /// <summary>
        /// Returns the sprite that should be displayed for this item stack.
        /// If a custom icon is registered, returns it; otherwise the vanilla <c>m_shared.m_icons[0]</c>.
        /// </summary>
        public static Sprite GetDisplayIcon(ItemDrop.ItemData? item)
        {
            if (item?.m_shared == null)
                return null!;

            if (HasCustomIcon(item))
            {
                string iconName = GetCustomIconName(item)!;
                var sprite = IconRegistry.Get(iconName);
                if (sprite != null)
                    return sprite;
            }

            var icons = item.m_shared.m_icons;
            return (icons != null && icons.Length > 0) ? icons[0] : null!;
        }

        /// <summary>
        /// Returns the Color that should tint the item icon.
        /// Parses the stored HTML color string; falls back to <see cref="Color.white"/> on failure.
        /// </summary>
        public static Color GetDisplayTint(ItemDrop.ItemData? item)
        {
            if (!HasIconTint(item))
                return Color.white;

            string? hex = GetIconTint(item);
            if (string.IsNullOrEmpty(hex))
                return Color.white;

            if (ColorUtility.TryParseHtmlString(hex, out Color c))
                return c;

            return Color.white;
        }

        // ─── Apply / Clear helpers ────────────────────────────────────────────

        public static void SetCustomIcon(ItemDrop.ItemData item, string iconName)
        {
            if (item.m_customData == null)
                item.m_customData = new Dictionary<string, string>();

            if (string.IsNullOrEmpty(iconName))
                item.m_customData.Remove(DrakeCustomIcon);
            else
            {
                item.m_customData[DrakeCustomIcon] = iconName;
                ReskinEvents.RaiseIconChanged(Player.m_localPlayer, item, item.m_shared.m_name, iconName);
            }
        }

        public static void SetIconTint(ItemDrop.ItemData item, string htmlColor)
        {
            if (item.m_customData == null)
                item.m_customData = new Dictionary<string, string>();

            if (string.IsNullOrEmpty(htmlColor))
                item.m_customData.Remove(DrakeIconTint);
            else
            {
                item.m_customData[DrakeIconTint] = htmlColor;
                ReskinEvents.RaiseTintChanged(Player.m_localPlayer, item, item.m_shared.m_name, htmlColor);
            }
        }

        public static void ClearCustomIcon(ItemDrop.ItemData item)
        {
            item.m_customData?.Remove(DrakeCustomIcon);
        }

        public static void ClearIconTint(ItemDrop.ItemData item)
        {
            item.m_customData?.Remove(DrakeIconTint);
        }

        public static void ResetAllCustomizations(ItemDrop.ItemData? item)
        {
            if (item == null) return;
            if (CanChangeIcon(item, false) && HasCustomIcon(item))
                ClearCustomIcon(item);
            if (CanChangeTint(item, false) && HasIconTint(item))
                ClearIconTint(item);
        }

        public static bool CanResetAnyCustomization(ItemDrop.ItemData? item)
        {
            if (item == null) return false;
            if (CanChangeIcon(item, false) && HasCustomIcon(item)) return true;
            if (CanChangeTint(item, false) && HasIconTint(item)) return true;
            return false;
        }

        // ─── Permission helpers ───────────────────────────────────────────────

        public static bool CanChangeIcon(ItemDrop.ItemData? item, bool showError = false)
        {
            return ReskinPermissionManager
                .Evaluate(ReskinPermissionOperation.ChangeIcon, item, Player.m_localPlayer, showError).Allowed;
        }

        public static bool CanChangeTint(ItemDrop.ItemData? item, bool showError = false)
        {
            return ReskinPermissionManager
                .Evaluate(ReskinPermissionOperation.ChangeTint, item, Player.m_localPlayer, showError).Allowed;
        }

        public static bool AnyInventoryActionAvailable(ItemDrop.ItemData? item)
        {
            if (item == null || Player.m_localPlayer == null) return false;
            return CanChangeIcon(item, false) || CanChangeTint(item, false);
        }

        // ─── UI helpers ───────────────────────────────────────────────────────

        public static bool IsMenuOpenModifierHeld()
        {
            return MenuModifierIsShift
                ? Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)
                : Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
        }

        public static void OpenIconPicker(ItemDrop.ItemData? item)
        {
            if (InventoryGui.instance == null || item == null) return;
            CurrentItem = item;
            if (ReskinUIPanels.IconPickerPanel == null)
                ReskinUIPanels.CreateIconPicker();
            ReskinUIPanels.RefreshIconPicker(item);
            ReskinUIPanels.IconPickerPanel!.SetActive(true);
            GUIManager.BlockInput(true);
        }

        public static void OpenTintPicker(ItemDrop.ItemData? item)
        {
            if (InventoryGui.instance == null || item == null) return;
            CurrentItem = item;
            if (ReskinUIPanels.TintPickerPanel == null)
                ReskinUIPanels.CreateTintPicker();
            ReskinUIPanels.RefreshTintPicker(item);
            ReskinUIPanels.TintPickerPanel!.SetActive(true);
            GUIManager.BlockInput(true);
        }

        public static void ApplyIcon(string iconName)
        {
            if (CurrentItem == null) return;
            SetCustomIcon(CurrentItem, iconName);
            CurrentItem = null;
            ReskinUIPanels.IconPickerPanel?.SetActive(false);
            GUIManager.BlockInput(false);
        }

        public static void ApplyTint(string htmlColor)
        {
            if (CurrentItem == null) return;
            SetIconTint(CurrentItem, htmlColor);
            CurrentItem = null;
            ReskinUIPanels.TintPickerPanel?.SetActive(false);
            GUIManager.BlockInput(false);
        }
    }
}
