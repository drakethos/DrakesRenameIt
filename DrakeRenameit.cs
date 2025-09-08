using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using Jotunn.Configs;
using UnityEngine;
using Jotunn.Entities;
using Jotunn.Managers;
using Jotunn.Utils;
using UnityEngine.UI;

namespace DrakeRenameit
{
    [BepInPlugin(DrakeRenameit.GUID, DrakeRenameit.ModName, DrakeRenameit.Version)]
    [BepInDependency(Jotunn.Main.ModGuid)]
    [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Minor)]
    public class DrakeRenameit : BaseUnityPlugin
    {
        public const string CompanyName = "DrakeMods";
        public const string ModName = "DrakesRenameit";
        public const string Version = "0.1.2";
        public const string GUID = "com." + CompanyName + "." + ModName;
        public const string DrakeRename = "Drake_Rename";
        public const string DrakeRenameDesc = "Drake_Rename_Desc";
        public static ItemDrop.ItemData currentItem;
        private readonly Harmony harmony = new Harmony("drakesmod.DrakeRenameit");

        private Texture2D TestTex;
        private Sprite TestSprite;
 


        private void Awake()
        {
            RenameitConfig.Bind(Config);
            harmony.PatchAll();
        }


        public static string getPropperName(ItemDrop.ItemData item)
        {
            return getPropperName(item, item.m_shared.m_name);
        }

        public static string getPropperName(ItemDrop.ItemData item, String defaultName)
        {
            string name;
            if (item.m_customData == null)
                item.m_customData = new Dictionary<string, string>();

            name = item.m_customData.TryGetValue(DrakeRename, out var existing)
                ? existing
                : defaultName;
            return name;
        }

        public static string getPropperDesc(ItemDrop.ItemData item)
        {
            return getPropperDesc(item, item.m_shared.m_description);
            
        }

        public static string getPropperDesc(ItemDrop.ItemData item, String defaultDesc)
        {
            string name;
            if (item.m_customData == null)
                item.m_customData = new Dictionary<string, string>();

            name = item.m_customData.TryGetValue(DrakeRenameDesc, out var existing)
                ? existing
                : defaultDesc;
            return name;
        }

        public static void OpenRename(ItemDrop.ItemData item)
        {
            currentItem = item;
            if (InventoryGui.instance == null) return;

            // Ensure panel exists
            if (UIPanels.inputNamePanel == null)
            {
                UIPanels.CreateRenameInput();
            }

            // Pre-fill with current name (renamed OR vanilla)
            string startName = getPropperName(item);

            UIPanels.renameNameInput.text = startName;

            // Bring it up
            UIPanels.inputNamePanel.SetActive(true);
            GUIManager.BlockInput(true);
        }
        
        public static void OpenRewriteDesc(ItemDrop.ItemData item)
        {
            currentItem = item;
            if (InventoryGui.instance == null) return;

            // Ensure panel exists
            if (UIPanels.inputDescPanel == null)
            {
                UIPanels.CreateRenameDescInput();
            }

            // Pre-fill with current name (renamed OR vanilla)
            string startDesc = getPropperDesc(item);

            UIPanels.renameDescInput.text = startDesc;

            // Bring it up
            UIPanels.inputDescPanel.SetActive(true);
            GUIManager.BlockInput(true);
        }

        public static void renameItem(String name)
        {
            if (currentItem == null) return;

            if (currentItem.m_customData == null)
                currentItem.m_customData = new Dictionary<string, string>();

            if (string.IsNullOrEmpty(name))
                currentItem.m_customData.Remove(DrakeRename);
            else
                currentItem.m_customData[DrakeRename] = name;

            if (RenameitConfig.NameClaimsOwner)
            {
                if (String.IsNullOrEmpty(currentItem.m_crafterName))
                {
                    Player localPlayer = Player.m_localPlayer;
                    if (localPlayer != null)
                    {
                        currentItem.m_crafterName = localPlayer.GetPlayerName();
                    }
                }
            }
        }
        
        public static void rewriteItemDesc(String name)
        {
            if (currentItem == null) return;

            if (currentItem.m_customData == null)
                currentItem.m_customData = new Dictionary<string, string>();

            if (string.IsNullOrEmpty(name))
                currentItem.m_customData.Remove(DrakeRenameDesc);
            else
                currentItem.m_customData[DrakeRenameDesc] = name;

            if (RenameitConfig.NameClaimsOwner)
            {
                if (String.IsNullOrEmpty(currentItem.m_crafterName))
                {
                    Player localPlayer = Player.m_localPlayer;
                    if (localPlayer != null)
                    {
                        currentItem.m_crafterName = localPlayer.GetPlayerName();
                    }
                }
            }
        }


        public static void ApplyRewriteDesc(string newDesc)
        {
            if (currentItem == null) return;

            rewriteItemDesc(newDesc);
            currentItem = null;

            // Close panel + unblock
            UIPanels.inputNamePanel.SetActive(false);
            GUIManager.BlockInput(false);
        }
        
        public static void ApplyRename(string newName)
        {
            if (currentItem == null) return;

            renameItem(newName);
            currentItem = null;

            // Close panel + unblock
            UIPanels.inputNamePanel.SetActive(false);
            GUIManager.BlockInput(false);
        }

        public static bool canChangeName(ItemDrop.ItemData item, bool showError = false,
            bool giveOwnershipOnEmpty = true)
        {
            if (RenameitConfig.LockToOwner)
            {
                Player local = Player.m_localPlayer;
                if (local != null)
                {
                    //first check if it even has an owner.
                    if (String.IsNullOrEmpty(item.m_crafterName))
                    {
                        if (giveOwnershipOnEmpty)
                        {
                            Debug.Log($"Crafter: '{item.m_crafterName}' vs Local: '{local.GetPlayerName()}'");

                            item.m_crafterName = local.GetPlayerName();
                            Debug.Log($" New Crafter: '{item.m_crafterName}' vs Local: '{local.GetPlayerName()}'");
                        }

                        return true;
                    }

                    if (item.m_crafterName != local.GetPlayerName())
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
            }
            return true;
        }
    }
}