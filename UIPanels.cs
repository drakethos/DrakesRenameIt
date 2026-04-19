using System;
using Jotunn.Managers;
using UnityEngine;
using UnityEngine.UI;
using DrakeRenameit.Ext.UI;

namespace DrakeRenameit;

public static class UIPanels
{
    private const string RenameItemDescription = "Rewrite Item Desc";
    public static GameObject? InputNamePanel { get; private set; }
    public static GameObject? InputDescPanel { get; private set; }
    public static InputField? RenameNameInput { get; private set; }
    public static InputField? RenameDescInput { get; private set; }
    private static Button _buttonOkName;
    private static Button _buttonOkDesc;
    private static Button _buttonResetName;
    private static Button _buttonResetDesc;

    public static GameObject? ActionMenuPanel { get; private set; }
    private static Button? _buttonMenuRename;
    private static Button? _buttonMenuDesc;
    private static Button? _buttonMenuCraftedBy;
    private static Button? _buttonMenuResetAll;
    private static Button? _buttonMenuCancel;

    public static GameObject? InputCraftedByPanel { get; private set; }
    public static InputField? RenameCraftedByInput { get; private set; }
    private static Button? _buttonOkCraftedBy;
    private static Button? _buttonResetCraftedBy;

    public static void OpenActionMenu(ItemDrop.ItemData item)
    {
        if (GUIManager.Instance == null || !GUIManager.CustomGUIFront)
            return;

        EnsureActionMenu();
        if (ActionMenuPanel == null || _buttonMenuRename == null || _buttonMenuResetAll == null)
            return;

        DrakeRenameit.CurrentItem = item;
        _buttonMenuRename.interactable = DrakeRenameit.CanChangeName(item, false);
        _buttonMenuDesc.interactable = DrakeRenameit.CanChangeDesc(item, false);
        _buttonMenuCraftedBy.interactable = DrakeRenameit.CanChangeCraftedByLabel(item, false);
        if (_buttonMenuResetAll != null)
            _buttonMenuResetAll.interactable = DrakeRenameit.CanResetAnyCustomization(item);

        ActionMenuPanel.SetActive(true);
        ActionMenuPanel.transform.SetAsLastSibling();
        GUIManager.BlockInput(true);
    }

    private static void EnsureActionMenu()
    {
        if (ActionMenuPanel != null)
            return;

        if (GUIManager.Instance == null || !GUIManager.CustomGUIFront)
            return;

        ActionMenuPanel = GUIManager.Instance.CreateWoodpanel(
            parent: GUIManager.CustomGUIFront.transform,
            anchorMin: new Vector2(0.5f, 0.5f),
            anchorMax: new Vector2(0.5f, 0.5f),
            position: new Vector2(0f, 0f),
            width: 320,
            height: 260,
            draggable: false);

        GUIManager.Instance.CreateText(
            text: "DrakesRenameIt",
            parent: ActionMenuPanel.transform,
            anchorMin: new Vector2(0.5f, 1f),
            anchorMax: new Vector2(0.5f, 1f),
            position: new Vector2(0f, -48f),
            font: GUIManager.Instance.AveriaSerifBold,
            fontSize: 22,
            color: GUIManager.Instance.ValheimOrange,
            outline: true,
            outlineColor: Color.black,
            width: 280,
            height: 40,
            addContentSizeFitter: false);

        _buttonMenuRename = GUIManager.Instance.CreateButton(
            text: "Rename",
            parent: ActionMenuPanel.transform,
            anchorMin: new Vector2(0.5f, 0.5f),
            anchorMax: new Vector2(0.5f, 0.5f),
            position: new Vector2(0f, 52f),
            width: 200f,
            height: 32f).GetComponent<Button>();
        _buttonMenuRename.AddUniqueListener(() =>
        {
            var item = DrakeRenameit.CurrentItem;
            CloseActionMenuOnly();
            if (item != null)
                DrakeRenameit.OpenRename(item);
        });

        _buttonMenuDesc = GUIManager.Instance.CreateButton(
            text: "Description",
            parent: ActionMenuPanel.transform,
            anchorMin: new Vector2(0.5f, 0.5f),
            anchorMax: new Vector2(0.5f, 0.5f),
            position: new Vector2(0f, 12f),
            width: 200f,
            height: 32f).GetComponent<Button>();
        _buttonMenuDesc.AddUniqueListener(() =>
        {
            var item = DrakeRenameit.CurrentItem;
            CloseActionMenuOnly();
            if (item != null)
                DrakeRenameit.OpenRewriteDesc(item);
        });

        _buttonMenuCraftedBy = GUIManager.Instance.CreateButton(
            text: "Crafted by",
            parent: ActionMenuPanel.transform,
            anchorMin: new Vector2(0.5f, 0.5f),
            anchorMax: new Vector2(0.5f, 0.5f),
            position: new Vector2(0f, -28f),
            width: 200f,
            height: 32f).GetComponent<Button>();
        _buttonMenuCraftedBy.AddUniqueListener(() =>
        {
            var item = DrakeRenameit.CurrentItem;
            CloseActionMenuOnly();
            if (item != null)
                DrakeRenameit.OpenCraftedByEditor(item);
        });

        _buttonMenuResetAll = GUIManager.Instance.CreateButton(
            text: "Reset all",
            parent: ActionMenuPanel.transform,
            anchorMin: new Vector2(0.5f, 0.5f),
            anchorMax: new Vector2(0.5f, 0.5f),
            position: new Vector2(0f, -60f),
            width: 100f,
            height: 22f).GetComponent<Button>();
        _buttonMenuResetAll.AddUniqueListener(() =>
        {
            var item = DrakeRenameit.CurrentItem;
            if (item != null)
                DrakeRenameit.ResetAllCustomizations(item);
            CloseActionMenuOnly();
        });

        _buttonMenuCancel = GUIManager.Instance.CreateButton(
            text: "Cancel",
            parent: ActionMenuPanel.transform,
            anchorMin: new Vector2(0.5f, 0.5f),
            anchorMax: new Vector2(0.5f, 0.5f),
            position: new Vector2(0f, -94f),
            width: 120f,
            height: 28f).GetComponent<Button>();
        _buttonMenuCancel.AddUniqueListener(CloseActionMenuOnly);
    }

    private static void CloseActionMenuOnly()
    {
        if (ActionMenuPanel != null)
            ActionMenuPanel.SetActive(false);
        GUIManager.BlockInput(false);
    }

    public static void CreateCraftedByInput()
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

        if (DrakeRenameit.CurrentItem == null)
        {
            Debug.LogError("Current Item null");
            return;
        }

        if (InputCraftedByPanel == null)
        {
            InputCraftedByPanel = GUIManager.Instance.CreateWoodpanel(
                parent: GUIManager.CustomGUIFront.transform,
                anchorMin: new Vector2(0.5f, 0.5f),
                anchorMax: new Vector2(0.5f, 0.5f),
                position: new Vector2(0f, 0f),
                width: 350,
                height: 170,
                draggable: false);

            GUIManager.Instance.CreateText(
                text: "Crafted by (display)",
                parent: InputCraftedByPanel.transform,
                anchorMin: new Vector2(0.5f, 1f),
                anchorMax: new Vector2(0.5f, 1f),
                position: new Vector2(15f, -65f),
                font: GUIManager.Instance.AveriaSerifBold,
                fontSize: 22,
                color: GUIManager.Instance.ValheimOrange,
                outline: true,
                outlineColor: Color.black,
                width: 300,
                height: 60,
                addContentSizeFitter: false);
        }

        InputCraftedByPanel.SetActive(true);
        InputCraftedByPanel.transform.SetAsLastSibling();

        if (RenameCraftedByInput == null)
        {
            RenameCraftedByInput = GUIManager.Instance.CreateInputField(
                parent: InputCraftedByPanel.transform,
                anchorMin: new Vector2(0.5f, 0.5f),
                anchorMax: new Vector2(0.5f, 0.5f),
                position: new Vector2(0f, 8f),
                contentType: InputField.ContentType.Standard,
                placeholderText: "Display name on tooltip…",
                fontSize: 18,
                width: 300,
                height: 30f).GetComponent<InputField>();
        }

        RenameCraftedByInput!.characterLimit = RenameitConfig.NameCharLimit;
        RenameCraftedByInput.text = DrakeRenameit.getCraftedByDisplay(DrakeRenameit.CurrentItem);

        if (_buttonOkCraftedBy == null)
        {
            _buttonOkCraftedBy = GUIManager.Instance.CreateButton(
                text: "OK",
                parent: InputCraftedByPanel.transform,
                anchorMin: new Vector2(0.5f, 0f),
                anchorMax: new Vector2(0.5f, 0f),
                position: new Vector2(-42f, 35f),
                width: 80f,
                height: 30f).GetComponent<Button>();
            _buttonOkCraftedBy.AddUniqueListener(() =>
            {
                DrakeRenameit.ApplyCraftedByLabel(RenameCraftedByInput.text.Trim());
            });
        }

        if (_buttonResetCraftedBy == null)
        {
            _buttonResetCraftedBy = GUIManager.Instance.CreateButton(
                text: "Reset",
                parent: InputCraftedByPanel.transform,
                anchorMin: new Vector2(0.5f, 0f),
                anchorMax: new Vector2(0.5f, 0f),
                position: new Vector2(42f, 35f),
                width: 80f,
                height: 30f).GetComponent<Button>();
            _buttonResetCraftedBy.AddUniqueListener(() =>
            {
                if (DrakeRenameit.CurrentItem != null)
                    RenameCraftedByInput.text = DrakeRenameit.CurrentItem.m_crafterName ?? "";
            });
        }
    }

    public static void CreateRenameInput()
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

        if (DrakeRenameit.CurrentItem == null)
        {
            Debug.LogError("Current Item null");
            return;
        }

        if (!InputNamePanel)
        {
            // Create main panel
            InputNamePanel = GUIManager.Instance.CreateWoodpanel(
                parent: GUIManager.CustomGUIFront.transform,
                anchorMin: new Vector2(0.5f, 0.5f),
                anchorMax: new Vector2(0.5f, 0.5f),
                position: new Vector2(0f, 0),
                width: 350,
                height: 150, // bigger for button
                draggable: false
            );
        }

        InputNamePanel.SetActive(true);
        InputNamePanel.transform.SetAsLastSibling();

        // Title text
        GUIManager.Instance.CreateText(
            text: "Rename Item",
            parent: InputNamePanel.transform,
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

        if (!RenameNameInput)
        {
            // Input field
            RenameNameInput = GUIManager.Instance.CreateInputField(
                parent: InputNamePanel.transform,
                anchorMin: new Vector2(0.5f, 0.5f),
                anchorMax: new Vector2(0.5f, 0.5f),
                position: new Vector2(0f, 0f), // slightly above center
                contentType: InputField.ContentType.Standard,
                placeholderText: "Enter new name...",
                fontSize: 18,
                width: 300,
                height: 30f).GetComponent<InputField>();
        }

        RenameNameInput!.characterLimit = RenameitConfig.NameCharLimit;
        RenameNameInput.text = DrakeRenameit.GetPropperName(DrakeRenameit.CurrentItem);

        // OK Button
        if (_buttonOkName == null)
        {
            _buttonOkName = GUIManager.Instance.CreateButton(
                text: "OK",
                parent: InputNamePanel.transform,
                anchorMin: new Vector2(0.5f, 0f),
                anchorMax: new Vector2(0.5f, 0f),
                position: new Vector2(-42f, 35f), // 20px above bottom
                width: 80f,
                height: 30f).GetComponent<Button>();

            _buttonOkName.gameObject.SetActive(true);
            
            _buttonOkName.GetComponent<Button>().AddUniqueListener(() =>
            {
                DrakeRenameit.ApplyRename(RenameNameInput.text.Trim());

                InputNamePanel.SetActive(false); // hide panel on OK
                GUIManager.BlockInput(false);
            });
        }

        if (_buttonResetName == null)
        {
            _buttonResetName = GUIManager.Instance.CreateButton(
                text: "Reset",
                parent: InputNamePanel.transform,
                anchorMin: new Vector2(0.5f, 0f),
                anchorMax: new Vector2(0.5f, 0f),
                position: new Vector2(42f, 35f), // 20px above bottom
                width: 80,
                height: 30f).GetComponent<Button>();
            _buttonResetName.gameObject.SetActive(true);
            _buttonResetName.GetComponent<Button>().AddUniqueListener(() =>
            {
                if (DrakeRenameit.CurrentItem != null)
                {
                    RenameNameInput.text = DrakeRenameit.resetName(DrakeRenameit.CurrentItem);
                }
            });
        }
    }

    public static void CreateRenameDescInput()
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

        if (DrakeRenameit.CurrentItem == null)
        {
            Debug.LogError("Current Item null");
            return;
        }

        // Create main panel
        if (!InputDescPanel)
        {
            InputDescPanel = GUIManager.Instance.CreateWoodpanel(
                parent: GUIManager.CustomGUIFront.transform,
                anchorMin: new Vector2(0.5f, 0.5f),
                anchorMax: new Vector2(0.5f, 0.5f),
                position: new Vector2(0f, 0),
                width: 275,
                height: 375,
                draggable: false
            );
        }

        InputDescPanel!.SetActive(true);
        InputDescPanel.transform.SetAsLastSibling();

        // Title text
        GUIManager.Instance.CreateText(
            text: RenameItemDescription,
            parent: InputDescPanel.transform,
            anchorMin: new Vector2(0.5f, 1f),
            anchorMax: new Vector2(0.5f, 1f),
            position: new Vector2(15f, -65f),
            font: GUIManager.Instance.AveriaSerifBold,
            fontSize: 24,
            color: GUIManager.Instance.ValheimOrange,
            outline: true,
            outlineColor: Color.black,
            width: 250,
            height: 80,
            addContentSizeFitter: false);

        // Input field
        if (!RenameDescInput)
        {
            RenameDescInput = GUIManager.Instance.CreateInputField(
                parent: InputDescPanel.transform,
                anchorMin: new Vector2(0.5f, 0.5f),
                anchorMax: new Vector2(0.5f, 0.5f),
                position: new Vector2(0f, 0f), // slightly above center
                contentType: InputField.ContentType.Standard,
                placeholderText: "Enter new desc",
                fontSize: 16,
                width: 225,
                height: 240f).GetComponent<InputField>();
            RenameDescInput.contentType = InputField.ContentType.Standard;
            RenameDescInput.lineType = InputField.LineType.MultiLineNewline;
            RenameDescInput.text = DrakeRenameit.getPropperDesc(DrakeRenameit.CurrentItem);
        }

        RenameDescInput!.characterLimit = RenameitConfig.DescCharLimit;


        // OK Button
        if (_buttonOkDesc == null)
        {
            _buttonOkDesc = GUIManager.Instance.CreateButton(
                text: "OK",
                parent: InputDescPanel.transform,
                anchorMin: new Vector2(0.5f, 0f),
                anchorMax: new Vector2(0.5f, 0f),
                position: new Vector2(-42f, 35f), // 20px above bottom
                width: 80f,
                height: 30f).GetComponent<Button>();
            _buttonOkDesc.gameObject.SetActive(true);
            _buttonOkDesc.AddUniqueListener(() =>
            {
                if (String.IsNullOrEmpty(RenameDescInput.text))
                {
                    GetPlayerAndSendError("Description must not be empty!");
                    return;
                }

                DrakeRenameit.ApplyRewriteDesc(RenameDescInput.text.Trim());

                InputDescPanel.SetActive(false); // hide panel on OK
                GUIManager.BlockInput(false);
            });
        }

        if (_buttonResetDesc == null)
        {
            _buttonResetDesc = GUIManager.Instance.CreateButton(
                text: "Reset",
                parent: InputDescPanel.transform,
                anchorMin: new Vector2(0.5f, 0f),
                anchorMax: new Vector2(0.5f, 0f),
                position: new Vector2(42f, 35f), // 20px above bottom
                width: 80,
                height: 30f).GetComponent<Button>();
            _buttonResetDesc.gameObject.SetActive(true);
            _buttonResetDesc.GetComponent<Button>().AddUniqueListener(() =>
            {
                RenameDescInput.text = DrakeRenameit.resetDesc(DrakeRenameit.CurrentItem);
            });
        }

        void GetPlayerAndSendError(string msg)
        {
            Player local = Player.m_localPlayer;
            if (local != null)
            {
                local.Message(
                    MessageHud.MessageType.Center, // or TopLeft, depending where you want it
                    msg
                );
            }
        }
    }
}