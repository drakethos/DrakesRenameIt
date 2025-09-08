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
    [BepInPlugin(GUID, ModName, Version)]
    [BepInDependency(Jotunn.Main.ModGuid)]
    [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Minor)]
    public class DrakeRenameit : BaseUnityPlugin
    {
        public const string CompanyName = "DrakeMods";
        public const string ModName = "DrakesRenameit";
        public const string Version = "0.0.1";
        public const string GUID = "com." + CompanyName + "." + ModName;
        public const string DrakeRename = "Drake_Rename";
 
        private readonly Harmony harmony = new Harmony("drakesmod.DrakeRenameit");

        private Texture2D TestTex;
        private Sprite TestSprite;
        private static GameObject inputPanel;

        public DrakeRenameit()
        {
        }


        private void Awake()
        {
            RenameitConfig.Bind(Config);
            harmony.PatchAll();
        }


        private static InputField renameInput;

        public static void CreateRenameInput(ItemDrop.ItemData item)
        {
            if (GUIManager.Instance == null)
            {
                Debug.LogError("GUIManager instance is null");
                return;
            }

            if (!GUIManager.CustomGUIFront)
            {
                Debug.LogError("GUIManager CustomGUI is null");
                return;
            }

            // Create main panel
            inputPanel = GUIManager.Instance.CreateWoodpanel(
                parent: GUIManager.CustomGUIFront.transform,
                anchorMin: new Vector2(0.5f, 0.5f),
                anchorMax: new Vector2(0.5f, 0.5f),
                position: new Vector2(0f, 0),
                width: 350,
                height: 150, // bigger for button
                draggable: false
            );
            inputPanel.SetActive(true);
            inputPanel.transform.SetAsLastSibling();

            // Title text
            GUIManager.Instance.CreateText(
                text: "Rename Item",
                parent: inputPanel.transform,
                anchorMin: new Vector2(0.5f, 1f),
                anchorMax: new Vector2(0.5f, 1f),
                position: new Vector2(15f, -65f),
                font: GUIManager.Instance.AveriaSerifBold,
                fontSize: 24,
                color: GUIManager.Instance.ValheimOrange,
                outline: true,
                outlineColor: Color.black,
                width: 200,
                height: 100,
                addContentSizeFitter: false);

            // Input field
            renameInput = GUIManager.Instance.CreateInputField(
                parent: inputPanel.transform,
                anchorMin: new Vector2(0.5f, 0.5f),
                anchorMax: new Vector2(0.5f, 0.5f),
                position: new Vector2(0f, 0f), // slightly above center
                contentType: InputField.ContentType.Standard,
                placeholderText: "Enter new name...",
                fontSize: 18,
                width: 300,
                height: 30f).GetComponent<InputField>();

            renameInput.text = getPropperName(item);

            // OK Button
            var okButton = GUIManager.Instance.CreateButton(
                text: "OK",
                parent: inputPanel.transform,
                anchorMin: new Vector2(0.5f, 0f),
                anchorMax: new Vector2(0.5f, 0f),
                position: new Vector2(-42f, 35f), // 20px above bottom
                width: 80f,
                height: 30f);
            okButton.gameObject.SetActive(true);
            okButton.GetComponent<Button>().onClick.AddListener(() =>
            {
                ApplyRename(renameInput.text.Trim(), item);
                inputPanel.SetActive(false); // hide panel on OK
                GUIManager.BlockInput(false);
            });

            var resetButton = GUIManager.Instance.CreateButton(
                text: "Reset",
                parent: inputPanel.transform,
                anchorMin: new Vector2(0.5f, 0f),
                anchorMax: new Vector2(0.5f, 0f),
                position: new Vector2(42f, 35f), // 20px above bottom
                width: 80,
                height: 30f);
            resetButton.gameObject.SetActive(true);
            resetButton.GetComponent<Button>().onClick.AddListener(() => { renameInput.text = item.m_shared.m_name; });
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

        public static void OpenRename(ItemDrop.ItemData item)
        {
            if (InventoryGui.instance == null) return;

            // Ensure panel exists
            if (inputPanel == null)
            {
                CreateRenameInput(item);
            }

            // Pre-fill with current name (renamed OR vanilla)
            string startName = getPropperName(item);

            renameInput.text = startName;

            // Bring it up
            inputPanel.SetActive(true);
            GUIManager.BlockInput(true);
        }

        public static void renameItem(ItemDrop.ItemData item, String name)
        {
            if (item.m_customData == null)
                item.m_customData = new Dictionary<string, string>();

            if (string.IsNullOrEmpty(name))
                item.m_customData.Remove(DrakeRename);
            else
                item.m_customData[DrakeRename] = name;

            if (RenameitConfig.NameClaimsOwner)
            {
                if (String.IsNullOrEmpty(item.m_crafterName))
                {
                    Player localPlayer = Player.m_localPlayer;
                    if (localPlayer != null)
                    {
                        item.m_crafterName = localPlayer.GetPlayerName();
                    }
                }
            }
        }

        public static void ApplyRename(string newName, ItemDrop.ItemData item)
        {
            if (item == null) return;

            renameItem(item, newName);

            // Close panel + unblock
            inputPanel.SetActive(false);
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