using Jotunn.Managers;
using UnityEngine;
using UnityEngine.UI;

namespace DrakeRenameit;

public class UIPanels
{
    private const string RenameItemDescription = "Rewrite Item Desc";
    public static GameObject inputNamePanel;
    public static GameObject inputDescPanel;
    public static InputField renameNameInput;
    public static InputField renameDescInput;

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

        if (DrakeRenameit.currentItem == null)
        {
            Debug.LogError("Current Item null");
            return;
        }

        // Create main panel
        inputNamePanel = GUIManager.Instance.CreateWoodpanel(
            parent: GUIManager.CustomGUIFront.transform,
            anchorMin: new Vector2(0.5f, 0.5f),
            anchorMax: new Vector2(0.5f, 0.5f),
            position: new Vector2(0f, 0),
            width: 350,
            height: 150, // bigger for button
            draggable: false
        );
        inputNamePanel.SetActive(true);
        inputNamePanel.transform.SetAsLastSibling();

        // Title text
        GUIManager.Instance.CreateText(
            text: "Rename Item",
            parent: inputNamePanel.transform,
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
        renameNameInput = GUIManager.Instance.CreateInputField(
            parent: inputNamePanel.transform,
            anchorMin: new Vector2(0.5f, 0.5f),
            anchorMax: new Vector2(0.5f, 0.5f),
            position: new Vector2(0f, 0f), // slightly above center
            contentType: InputField.ContentType.Standard,
            placeholderText: "Enter new name...",
            fontSize: 18,
            width: 300,
            height: 30f).GetComponent<InputField>();
        renameNameInput.characterLimit = RenameitConfig.NameCharLimit;
        renameNameInput.text = DrakeRenameit.getPropperName(DrakeRenameit.currentItem);

        // OK Button
        var okButton = GUIManager.Instance.CreateButton(
            text: "OK",
            parent: inputNamePanel.transform,
            anchorMin: new Vector2(0.5f, 0f),
            anchorMax: new Vector2(0.5f, 0f),
            position: new Vector2(-42f, 35f), // 20px above bottom
            width: 80f,
            height: 30f);
        okButton.gameObject.SetActive(true);
        okButton.GetComponent<Button>().onClick.AddListener(() =>
        {
            if (DrakeRenameit.currentItem != null)
            {
                DrakeRenameit.ApplyRename(renameNameInput.text.Trim());
                
            }

            inputNamePanel.SetActive(false); // hide panel on OK
            GUIManager.BlockInput(false);
        });

        var resetButton = GUIManager.Instance.CreateButton(
            text: "Reset",
            parent: inputNamePanel.transform,
            anchorMin: new Vector2(0.5f, 0f),
            anchorMax: new Vector2(0.5f, 0f),
            position: new Vector2(42f, 35f), // 20px above bottom
            width: 80,
            height: 30f);
        resetButton.gameObject.SetActive(true);
        resetButton.GetComponent<Button>().onClick.AddListener(() =>
        {
            if (DrakeRenameit.currentItem != null)
            {
                renameNameInput.text = DrakeRenameit.resetName(DrakeRenameit.currentItem);
            }
        });
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
        
        if (DrakeRenameit.currentItem == null)
        {
            Debug.LogError("Current Item null");
            return;
        }

        // Create main panel
        inputDescPanel = GUIManager.Instance.CreateWoodpanel(
            parent: GUIManager.CustomGUIFront.transform,
            anchorMin: new Vector2(0.5f, 0.5f),
            anchorMax: new Vector2(0.5f, 0.5f),
            position: new Vector2(0f, 0),
            width: 325,
            height: 425, 
            draggable: false
        );
        inputDescPanel.SetActive(true);
        inputDescPanel.transform.SetAsLastSibling();

        // Title text
        GUIManager.Instance.CreateText(
            text: RenameItemDescription,
            parent: inputDescPanel.transform,
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
        renameDescInput = GUIManager.Instance.CreateInputField(
            parent: inputDescPanel.transform,
            anchorMin: new Vector2(0.5f, 0.5f),
            anchorMax: new Vector2(0.5f, 0.5f),
            position: new Vector2(0f, 0f), // slightly above center
            contentType: InputField.ContentType.Standard,
            placeholderText: "Enter new desc",
            fontSize: 18,
            width: 200,
            height: 250f).GetComponent<InputField>();
        renameDescInput.contentType = InputField.ContentType.Standard;
        renameDescInput.lineType = InputField.LineType.MultiLineNewline;
        renameDescInput.characterLimit = RenameitConfig.DescCharLimit;
        renameDescInput.text = DrakeRenameit.getPropperDesc(DrakeRenameit.currentItem);

        // OK Button
        var okButton2 = GUIManager.Instance.CreateButton(
            text: "OK",
            parent: inputDescPanel.transform,
            anchorMin: new Vector2(0.5f, 0f),
            anchorMax: new Vector2(0.5f, 0f),
            position: new Vector2(-42f, 35f), // 20px above bottom
            width: 80f,
            height: 30f);
        okButton2.gameObject.SetActive(true);
        okButton2.GetComponent<Button>().onClick.AddListener(() =>
        {
            if (DrakeRenameit.currentItem != null)
            {
                DrakeRenameit.ApplyRewriteDesc(renameDescInput.text.Trim());
            }

            inputDescPanel.SetActive(false); // hide panel on OK
            GUIManager.BlockInput(false);
        });

        var resetButton2 = GUIManager.Instance.CreateButton(
            text: "Reset",
            parent: inputDescPanel.transform,
            anchorMin: new Vector2(0.5f, 0f),
            anchorMax: new Vector2(0.5f, 0f),
            position: new Vector2(42f, 35f), // 20px above bottom
            width: 80,
            height: 30f);
        resetButton2.gameObject.SetActive(true);
        resetButton2.GetComponent<Button>().onClick.AddListener(() =>
        {
            if (DrakeRenameit.currentItem != null)
            {
                renameDescInput.text = DrakeRenameit.resetDesc(DrakeRenameit.currentItem);
            }
        });
    }
}