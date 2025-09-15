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