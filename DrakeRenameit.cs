using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using HarmonyLib;
using Jotunn;
using Jotunn.Managers;
using Jotunn.Utils;
using UnityEngine;

namespace DrakeRenameit
{
    [BepInPlugin(GUID, ModName, Version)]
    [BepInDependency(Main.ModGuid)]
    [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Minor)]
    public class DrakeRenameit : BaseUnityPlugin
    {
        public const string CompanyName = "DrakeMods";
        public const string ModName = "DrakesRenameit";
        public const string Version = "0.3.1";
        public const string GUID = "com." + CompanyName + "." + ModName;
        public const string DrakeNewName = "Drake_Rename";
        public const string DrakeNewDesc = "Drake_Rename_Desc";
        public static ItemDrop.ItemData? CurrentItem { get; private set; }
        private readonly Harmony harmony = new Harmony("drakesmod.DrakeRenameit");

        private Texture2D TestTex;
        private Sprite TestSprite;


        private void Awake()
        {
            RenameitConfig.Bind(Config);
            AddVip();

            harmony.PatchAll();
        }

        private static void AddVip()
        {
            List<string> vipList = RenameitConfig.VipList.Split(',')
                .Select(s => s.Trim().ToLowerInvariant())
                .Where(s => !string.IsNullOrEmpty(s))
                .ToList();
            API.RenameitPermission.AddVIP(vipList);
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
            if (item.m_customData == null)
                return false;
            return item.m_customData.TryGetValue(DrakeNewName, out _);
        }

        public static string resetName(ItemDrop.ItemData? item)
        {
            item.m_customData.Remove(DrakeNewName);
            return item.m_shared.m_name;
        }

        public static string resetDesc(ItemDrop.ItemData? item)
        {
            item.m_customData.Remove(DrakeNewDesc);
            return item.m_shared.m_name;
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
            // Ensure panel exists
            if (UIPanels.InputNamePanel == null)
            {
                UIPanels.CreateRenameInput();
            }

            // Pre-fill with current name (renamed OR vanilla)
            string startName = GetPropperName(item);

            UIPanels.RenameNameInput!.text = startName;

            // Bring it up
            UIPanels.InputNamePanel.SetActive(true);
            GUIManager.BlockInput(true);
        }

        public static void OpenRewriteDesc(ItemDrop.ItemData? item)
        {
            if (InventoryGui.instance == null) return;
            if (item == null) return;
            CurrentItem = item;
            // Ensure panel exists
            if (UIPanels.InputDescPanel == null)
            {
                UIPanels.CreateRenameDescInput();
            }

            // Pre-fill with current name (renamed OR vanilla)
            string startDesc = getPropperDesc(item);

            UIPanels.RenameDescInput!.text = startDesc;

            // Bring it up
            UIPanels.InputDescPanel!.SetActive(true);
            GUIManager.BlockInput(true);
        }

        public static void RenameItem(String name)
        {
            if (CurrentItem == null) return;

            if (CurrentItem.m_customData == null)
                CurrentItem.m_customData = new Dictionary<string, string>();

            if (string.IsNullOrEmpty(name))
                CurrentItem.m_customData.Remove(DrakeNewName);
            else
                CurrentItem.m_customData[DrakeNewName] = name;

            if (RenameitConfig.NameClaimsOwner && String.IsNullOrEmpty(CurrentItem.m_crafterName))
            {
                Player localPlayer = Player.m_localPlayer;
                if (localPlayer != null)
                {
                    CurrentItem.m_crafterID = localPlayer.GetPlayerID();
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
                CurrentItem.m_customData[DrakeNewDesc] = name;

            if (RenameitConfig.NameClaimsOwner && String.IsNullOrEmpty(CurrentItem.m_crafterName))
            {
                Player localPlayer = Player.m_localPlayer;
                if (localPlayer != null)
                {
                    CurrentItem.m_crafterID = localPlayer.GetPlayerID();
                }
            }
        }


        public static void ApplyRewriteDesc(string newDesc)
        {
            if (CurrentItem == null) return;

            RewriteItemDesc(newDesc);
            CurrentItem = null;

            // Close panel + unblock
            UIPanels.InputDescPanel!.SetActive(false);
            GUIManager.BlockInput(false);
        }

        public static void ApplyRename(string newName)
        {
            if (CurrentItem == null) return;

            RenameItem(newName);
            CurrentItem = null;

            // Close panel + unblock
            UIPanels.InputNamePanel!.SetActive(false);
            GUIManager.BlockInput(false);
        }

        public static bool CanChangeName(ItemDrop.ItemData? item, bool showError = false)
        {
            Player local = Player.m_localPlayer;
            if ((RenameitConfig.RenameEnabled || API.RenameitPermission.IsAdminOrVIP(local)) &&
                RenameitConfig.LockToOwner)
            {
                //first check if it even has an owner.
                if (String.IsNullOrEmpty(item!.m_crafterName))
                {
                    return true;
                }

                if (item.m_crafterID != local.GetPlayerID() && !API.RenameitPermission.IsAdminOrVIP(local))
                {
                    if (showError)
                    {
                        local.Message(
                            MessageHud.MessageType.Center, // or TopLeft, depending where you want it
                            "You cannot change this — it’s owned!"
                        );
                    }

                    return false;
                }
            }
            else
            {
                local.Message(
                    MessageHud.MessageType.Center, // or TopLeft, depending where you want it
                    "You cannot change this — Renames have been disabled");
                return false;
            }
            return true;
        }

        public static bool CanChangeDesc(ItemDrop.ItemData item, bool showError = false)
        {
            Player local = Player.m_localPlayer;
            if ((RenameitConfig.RewriteDescriptionsEnabled || API.RenameitPermission.IsAdminOrVIP(local)) &&
                RenameitConfig.LockToOwner)
            {
                //first check if it even has an owner.
                if (String.IsNullOrEmpty(item.m_crafterName))
                {
                    return true;
                }

                if (item.m_crafterID != local.GetPlayerID() && !API.RenameitPermission.IsAdminOrVIP(local))
                {
                    if (showError)
                    {
                        local.Message(
                            MessageHud.MessageType.Center, // or TopLeft, depending where you want it
                            "You cannot change this — it’s owned!"
                        );
                    }
                    return false;
                }
            }
            else
            {
                local.Message(
                    MessageHud.MessageType.Center, // or TopLeft, depending where you want it
                    "You cannot change this — Desc Rewrites have been disabled");
                return false;
            }
            return true;
        }
    }
}