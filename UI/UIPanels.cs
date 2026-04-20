using System;
using System.Collections.Generic;
using DrakeRenameit.API;
using DrakeRenameit.Ext.UI;
using Jotunn.Managers;
using UnityEngine;
using UnityEngine.UI;
using Text = UnityEngine.UI.Text;
using Image = UnityEngine.UI.Image;

namespace DrakeRenameit.UI;

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
    private static Button? _buttonMenuUnlock;
    private static Button? _buttonMenuCancel;

    public static GameObject? InputCraftedByPanel { get; private set; }
    public static InputField? RenameCraftedByInput { get; private set; }
    private static Button? _buttonOkCraftedBy;
    private static Button? _buttonResetCraftedBy;
    private static Button? _buttonCraftedByLineLabelPick;
    private static GameObject? _craftedByLineLabelPopover;
    private static string? _craftedByLineLabelPendingToken;

    /// <summary>Line label applied on crafted-by OK when allowed; null clears <see cref="DrakeRenameit.DrakeCraftedByLineLabel"/>.</summary>
    internal static string? CraftedByLineLabelPendingToken => _craftedByLineLabelPendingToken;

    // Unlock confirmation sub-panel
    private static GameObject? _unlockConfirmPanel;
    private static RectTransform? _unlockCostListRoot;
    private static Text? _unlockAffordWarning;
    private static Button? _buttonConfirmUnlock;
    private static Button? _buttonConfirmCancel;

    // Track whether we currently hold a BlockInput(true) so we never double-block or double-unblock
    private static bool _inputBlocked;

    internal static void EnsureInputBlocked()
    {
        if (_inputBlocked) return;
        GUIManager.BlockInput(true);
        _inputBlocked = true;
    }

    internal static void EnsureInputUnblocked()
    {
        if (!_inputBlocked) return;
        GUIManager.BlockInput(false);
        _inputBlocked = false;
    }

    /// <summary>Hides rename / unlock UI and clears <see cref="DrakeRenameit.CurrentItem"/> (e.g. stale stack after drop).</summary>
    public static void CloseAllRenameEditingUi()
    {
        if (InputNamePanel != null)
            InputNamePanel.SetActive(false);
        if (InputDescPanel != null)
            InputDescPanel.SetActive(false);
        if (InputCraftedByPanel != null)
            InputCraftedByPanel.SetActive(false);
        CloseCraftedByLineLabelPopover();
        if (ActionMenuPanel != null)
            ActionMenuPanel.SetActive(false);
        if (_unlockConfirmPanel != null)
            _unlockConfirmPanel.SetActive(false);
        DrakeRenameit.CurrentItem = null;
        EnsureInputUnblocked();
    }

    public static void OpenActionMenu(ItemDrop.ItemData item)
    {
        if (GUIManager.Instance == null || !GUIManager.CustomGUIFront)
            return;

        if (!DrakeRenameit.IsItemInLocalPlayerInventory(item))
        {
            CloseAllRenameEditingUi();
            Player.m_localPlayer?.Message(MessageHud.MessageType.Center,
                "That item is no longer in your inventory.");
            return;
        }

        EnsureActionMenu();
        if (ActionMenuPanel == null || _buttonMenuRename == null || _buttonMenuResetAll == null || _buttonMenuUnlock == null)
            return;

        DrakeRenameit.CurrentItem = item;

        bool showUnlock = DrakeRenameit.ShowUnlockButton(item);
        _buttonMenuUnlock.gameObject.SetActive(showUnlock);
        if (showUnlock)
        {
            var unlockLabel = _buttonMenuUnlock.GetComponentInChildren<Text>();
            if (unlockLabel != null)
            {
                string cost = RenameUnlockCost.GetCostDisplayShort();
                unlockLabel.text = string.IsNullOrEmpty(cost)
                    ? $"{DrakeRenameit.TooltipUnlockCostLockedEmoji} Unlock"
                    : $"{DrakeRenameit.TooltipUnlockCostLockedEmoji} Unlock ({cost})";
            }

            _buttonMenuRename.interactable = false;
            _buttonMenuDesc.interactable = false;
            _buttonMenuCraftedBy.interactable = false;
            _buttonMenuResetAll.interactable = false;
            // Unlock button is always clickable — affordability is checked in the confirm panel
            _buttonMenuUnlock.interactable = true;
        }
        else
        {
            _buttonMenuRename.interactable = DrakeRenameit.CanChangeName(item, false);
            _buttonMenuDesc.interactable = DrakeRenameit.CanChangeDesc(item, false);
            _buttonMenuCraftedBy.interactable = DrakeRenameit.CanChangeCraftedByLabel(item, false);
            _buttonMenuResetAll.interactable = DrakeRenameit.CanResetAnyCustomization(item);
        }

        ActionMenuPanel.SetActive(true);
        ActionMenuPanel.transform.SetAsLastSibling();
        EnsureInputBlocked();
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
            height: 280,
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

        _buttonMenuUnlock = GUIManager.Instance.CreateButton(
            text: $"{DrakeRenameit.TooltipUnlockCostLockedEmoji} Unlock",
            parent: ActionMenuPanel.transform,
            anchorMin: new Vector2(0.5f, 0.5f),
            anchorMax: new Vector2(0.5f, 0.5f),
            position: new Vector2(0f, 72f),
            width: 220f,
            height: 30f).GetComponent<Button>();
        _buttonMenuUnlock.gameObject.SetActive(false);
        _buttonMenuUnlock.AddUniqueListener(() =>
        {
            var item = DrakeRenameit.CurrentItem;
            if (item == null)
                return;
            // Hide the action menu and show the confirmation panel instead
            ActionMenuPanel!.SetActive(false);
            OpenUnlockConfirmPanel(item);
        });

        _buttonMenuRename = GUIManager.Instance.CreateButton(
            text: "Rename",
            parent: ActionMenuPanel.transform,
            anchorMin: new Vector2(0.5f, 0.5f),
            anchorMax: new Vector2(0.5f, 0.5f),
            position: new Vector2(0f, 40f),
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
            position: new Vector2(0f, 0f),
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
            position: new Vector2(0f, -40f),
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
            position: new Vector2(-66f, -80f),
            width: 120f,
            height: 28f).GetComponent<Button>();
        _buttonMenuResetAll.AddUniqueListener(() =>
        {
            var item = DrakeRenameit.CurrentItem;
            if (item != null)
                DrakeRenameit.ResetAllCustomizations(item);
            CloseActionMenuOnly();
        });

        _buttonMenuCancel = GUIManager.Instance.CreateButton(
            text: "OK",
            parent: ActionMenuPanel.transform,
            anchorMin: new Vector2(0.5f, 0.5f),
            anchorMax: new Vector2(0.5f, 0.5f),
            position: new Vector2(66f, -80f),
            width: 120f,
            height: 28f).GetComponent<Button>();
        _buttonMenuCancel.AddUniqueListener(CloseActionMenuOnly);
    }

    private static void CloseActionMenuOnly()
    {
        if (ActionMenuPanel != null)
            ActionMenuPanel.SetActive(false);
        EnsureInputUnblocked();
    }

    // -------------------------------------------------------------------------
    // Unlock Confirmation Panel
    // -------------------------------------------------------------------------

    /// <summary>Opens the unlock cost panel directly (shift+click when stack is still locked).</summary>
    public static void OpenUnlockMenuFromInventory(ItemDrop.ItemData item)
    {
        DrakeRenameit.CurrentItem = item;
        OpenUnlockConfirmPanel(item);
    }

    /// <summary>Opens the unlock confirmation panel, hiding the action menu behind it.</summary>
    private static void OpenUnlockConfirmPanel(ItemDrop.ItemData item)
    {
        if (GUIManager.Instance == null || !GUIManager.CustomGUIFront)
            return;

        if (!DrakeRenameit.IsItemInLocalPlayerInventory(item))
        {
            CloseAllRenameEditingUi();
            Player.m_localPlayer?.Message(MessageHud.MessageType.Center,
                "That item is no longer in your inventory.");
            return;
        }

        EnsureUnlockConfirmPanel();
        if (_unlockConfirmPanel == null || _unlockCostListRoot == null || _buttonConfirmUnlock == null)
            return;

        // Refresh body text with current cost info
        RefreshUnlockConfirmBody();

        // Enable/disable the pay button based on current affordability
        bool canAfford = RenameUnlockCost.CanPlayerAfford(Player.m_localPlayer);
        _buttonConfirmUnlock.interactable = canAfford;

        _unlockConfirmPanel.SetActive(true);
        _unlockConfirmPanel.transform.SetAsLastSibling();
        // Input remains blocked from the action menu open — no extra block needed
        EnsureInputBlocked();
    }

    private static void RefreshUnlockConfirmBody()
    {
        if (_unlockCostListRoot == null)
            return;

        ClearUnlockCostListChildren();

        var costEntries = RenameUnlockCost.GetCostDisplayEntries();
        bool canAfford = RenameUnlockCost.CanPlayerAfford(Player.m_localPlayer);

        if (costEntries.Count == 0)
        {
            AddUnlockPlainLine(_unlockCostListRoot, "Unlock this item stack for editing?");
            if (_unlockAffordWarning != null)
            {
                _unlockAffordWarning.text = "";
                _unlockAffordWarning.gameObject.SetActive(false);
            }

            return;
        }

        foreach (var (localizedName, amount, prefabName) in costEntries)
        {
            string token = RenameUnlockCost.GetItemTokenPublic(prefabName);
            int have = Player.m_localPlayer != null
                ? Player.m_localPlayer.GetInventory()?.CountItems(token) ?? 0
                : 0;
            string haveColor = have >= amount ? "lime" : "red";
            string line =
                $"{amount}x {localizedName}  <color={haveColor}>({have} in inv)</color>";
            var sprite = RenameUnlockCost.GetItemIconSprite(prefabName);
            CreateUnlockCostRow(_unlockCostListRoot, sprite, line);
        }

        if (_unlockAffordWarning != null)
        {
            if (!canAfford)
            {
                _unlockAffordWarning.text = "<color=red>You don't have enough items to unlock.</color>";
                _unlockAffordWarning.gameObject.SetActive(true);
            }
            else
            {
                _unlockAffordWarning.text = "";
                _unlockAffordWarning.gameObject.SetActive(false);
            }
        }
    }

    private static void ClearUnlockCostListChildren()
    {
        if (_unlockCostListRoot == null)
            return;
        for (int i = _unlockCostListRoot.childCount - 1; i >= 0; i--)
            UnityEngine.Object.Destroy(_unlockCostListRoot.GetChild(i).gameObject);
    }

    private static void AddUnlockPlainLine(RectTransform parent, string message)
    {
        var textGo = new GameObject("PlainLine", typeof(RectTransform), typeof(Text));
        textGo.transform.SetParent(parent, false);
        var te = textGo.GetComponent<Text>();
        te.text = message;
        if (GUIManager.Instance != null)
            te.font = GUIManager.Instance.AveriaSerifBold;
        te.fontSize = 14;
        te.color = Color.white;
        te.alignment = TextAnchor.UpperLeft;
        te.horizontalOverflow = HorizontalWrapMode.Wrap;
        var rt = textGo.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(300, 40);
    }

    private static void CreateUnlockCostRow(RectTransform parent, Sprite? icon, string richLine)
    {
        var row = new GameObject("CostRow", typeof(RectTransform));
        row.transform.SetParent(parent, false);
        var rowRt = row.GetComponent<RectTransform>();
        rowRt.sizeDelta = new Vector2(300, 38);
        var h = row.AddComponent<HorizontalLayoutGroup>();
        h.spacing = 10;
        h.childAlignment = TextAnchor.MiddleLeft;
        h.childForceExpandWidth = false;
        h.childForceExpandHeight = true;
        h.padding = new RectOffset(2, 2, 2, 2);

        var iconGo = new GameObject("icon", typeof(RectTransform), typeof(Image));
        iconGo.transform.SetParent(row.transform, false);
        var img = iconGo.GetComponent<Image>();
        img.sprite = icon;
        img.preserveAspect = true;
        img.color = Color.white;
        img.enabled = icon != null;
        var iconLe = iconGo.AddComponent<LayoutElement>();
        iconLe.preferredWidth = 32;
        iconLe.preferredHeight = 32;
        iconLe.minWidth = 32;

        var textGo = new GameObject("line", typeof(RectTransform), typeof(Text));
        textGo.transform.SetParent(row.transform, false);
        var te = textGo.GetComponent<Text>();
        te.text = richLine;
        if (GUIManager.Instance != null)
            te.font = GUIManager.Instance.AveriaSerifBold;
        te.fontSize = 14;
        te.color = Color.white;
        te.alignment = TextAnchor.MiddleLeft;
        te.supportRichText = true;
        te.horizontalOverflow = HorizontalWrapMode.Wrap;
        var teLe = textGo.AddComponent<LayoutElement>();
        teLe.flexibleWidth = 1;
        teLe.minWidth = 180;
    }

    private static void EnsureUnlockConfirmPanel()
    {
        if (_unlockConfirmPanel != null)
            return;

        if (GUIManager.Instance == null || !GUIManager.CustomGUIFront)
            return;

        _unlockConfirmPanel = GUIManager.Instance.CreateWoodpanel(
            parent: GUIManager.CustomGUIFront.transform,
            anchorMin: new Vector2(0.5f, 0.5f),
            anchorMax: new Vector2(0.5f, 0.5f),
            position: new Vector2(0f, 0f),
            width: 360,
            height: 280,
            draggable: false);

        GUIManager.Instance.CreateText(
            text: $"{DrakeRenameit.TooltipUnlockCostLockedEmoji} Unlock Item",
            parent: _unlockConfirmPanel.transform,
            anchorMin: new Vector2(0.5f, 1f),
            anchorMax: new Vector2(0.5f, 1f),
            position: new Vector2(0f, -44f),
            font: GUIManager.Instance.AveriaSerifBold,
            fontSize: 20,
            color: GUIManager.Instance.ValheimOrange,
            outline: true,
            outlineColor: Color.black,
            width: 300,
            height: 36,
            addContentSizeFitter: false);

        GUIManager.Instance.CreateText(
            text: "Unlock cost:",
            parent: _unlockConfirmPanel.transform,
            anchorMin: new Vector2(0.5f, 1f),
            anchorMax: new Vector2(0.5f, 1f),
            position: new Vector2(0f, -78f),
            font: GUIManager.Instance.AveriaSerifBold,
            fontSize: 14,
            color: Color.white,
            outline: true,
            outlineColor: Color.black,
            width: 300,
            height: 22,
            addContentSizeFitter: false);

        var listGo = new GameObject("UnlockCostList", typeof(RectTransform), typeof(VerticalLayoutGroup));
        listGo.transform.SetParent(_unlockConfirmPanel.transform, false);
        _unlockCostListRoot = listGo.GetComponent<RectTransform>();
        _unlockCostListRoot.anchorMin = new Vector2(0.5f, 1f);
        _unlockCostListRoot.anchorMax = new Vector2(0.5f, 1f);
        _unlockCostListRoot.pivot = new Vector2(0.5f, 1f);
        _unlockCostListRoot.sizeDelta = new Vector2(320, 150);
        _unlockCostListRoot.anchoredPosition = new Vector2(0f, -102f);
        var vlg = listGo.GetComponent<VerticalLayoutGroup>();
        vlg.spacing = 6;
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.childControlHeight = true;
        vlg.childControlWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.childForceExpandWidth = true;

        var warnGo = GUIManager.Instance.CreateText(
            text: "",
            parent: _unlockConfirmPanel.transform,
            anchorMin: new Vector2(0.5f, 0f),
            anchorMax: new Vector2(0.5f, 0f),
            position: new Vector2(0f, 78f),
            font: GUIManager.Instance.AveriaSerifBold,
            fontSize: 13,
            color: Color.red,
            outline: true,
            outlineColor: Color.black,
            width: 310,
            height: 36,
            addContentSizeFitter: false);
        _unlockAffordWarning = warnGo.GetComponent<Text>();
        _unlockAffordWarning.supportRichText = true;
        _unlockAffordWarning.gameObject.SetActive(false);

        _buttonConfirmUnlock = GUIManager.Instance.CreateButton(
            text: $"{DrakeRenameit.TooltipUnlockCostLockedEmoji} Pay",
            parent: _unlockConfirmPanel.transform,
            anchorMin: new Vector2(0.5f, 0f),
            anchorMax: new Vector2(0.5f, 0f),
            position: new Vector2(-55f, 35f),
            width: 110f,
            height: 30f).GetComponent<Button>();
        _buttonConfirmUnlock.AddUniqueListener(() =>
        {
            var item = DrakeRenameit.CurrentItem;
            if (item == null)
            {
                CloseUnlockConfirmPanel(reopenActionMenu: false);
                return;
            }

            if (!DrakeRenameit.TryPayRenameUnlock(item))
            {
                // Stack dropped / moved, or cost changed — don't leave a dead confirm panel open
                if (DrakeRenameit.CurrentItem == null ||
                    !DrakeRenameit.IsItemInLocalPlayerInventory(DrakeRenameit.CurrentItem))
                {
                    CloseAllRenameEditingUi();
                    return;
                }

                RefreshUnlockConfirmBody();
                _buttonConfirmUnlock!.interactable = RenameUnlockCost.CanPlayerAfford(Player.m_localPlayer);
                return;
            }

            // Paid successfully — close confirm panel and reopen action menu (now unlocked)
            CloseUnlockConfirmPanel(reopenActionMenu: true);
        });

        _buttonConfirmCancel = GUIManager.Instance.CreateButton(
            text: "Cancel",
            parent: _unlockConfirmPanel.transform,
            anchorMin: new Vector2(0.5f, 0f),
            anchorMax: new Vector2(0.5f, 0f),
            position: new Vector2(55f, 35f),
            width: 110f,
            height: 30f).GetComponent<Button>();
        _buttonConfirmCancel.AddUniqueListener(() => CloseUnlockConfirmPanel(reopenActionMenu: false));
    }

    private static void CloseUnlockConfirmPanel(bool reopenActionMenu)
    {
        if (_unlockConfirmPanel != null)
            _unlockConfirmPanel.SetActive(false);

        if (reopenActionMenu && DrakeRenameit.CurrentItem != null)
        {
            // Re-show action menu (now unlocked) without releasing the input block
            var item = DrakeRenameit.CurrentItem;
            ActionMenuPanel?.SetActive(false);
            OpenActionMenu(item);
        }
        else
        {
            // Cancel: fully close everything and release input
            if (ActionMenuPanel != null)
                ActionMenuPanel.SetActive(false);
            DrakeRenameit.CurrentItem = null;
            EnsureInputUnblocked();
        }
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
                height: 230,
                draggable: false);

            GUIManager.Instance.CreateText(
                text: "Crafted by (display)",
                parent: InputCraftedByPanel.transform,
                anchorMin: new Vector2(0.5f, 1f),
                anchorMax: new Vector2(0.5f, 1f),
                position: new Vector2(15f, -56f),
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
        EnsureCraftedByLineLabelControls();

        if (RenameCraftedByInput == null)
        {
            RenameCraftedByInput = GUIManager.Instance.CreateInputField(
                parent: InputCraftedByPanel.transform,
                anchorMin: new Vector2(0.5f, 0.5f),
                anchorMax: new Vector2(0.5f, 0.5f),
                position: new Vector2(0f, -28f),
                contentType: InputField.ContentType.Standard,
                placeholderText: "Display name on tooltip…",
                fontSize: 18,
                width: 300,
                height: 30f).GetComponent<InputField>();
        }

        RenameCraftedByInput!.characterLimit = RenameitConfig.CraftedByCharLimit;
        RenameCraftedByInput.text = DrakeRenameit.getCraftedByDisplay(DrakeRenameit.CurrentItem);
        if (DrakeRenameit.CurrentItem != null)
            RefreshCraftedByLineLabelPicker(DrakeRenameit.CurrentItem);

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
                var opts = RenameitConfig.GetCraftedByAllowedLabelsList();
                _craftedByLineLabelPendingToken = null;
                SetCraftedByLineLabelPickButtonText(opts, 0);
                CloseCraftedByLineLabelPopover();
            });
        }
    }

    static void EnsureCraftedByLineLabelControls()
    {
        if (InputCraftedByPanel == null || GUIManager.Instance == null)
            return;

        var panelRt = InputCraftedByPanel.GetComponent<RectTransform>();
        if (panelRt != null && panelRt.sizeDelta.y < 220f)
            panelRt.sizeDelta = new Vector2(350f, 230f);

        if (_buttonCraftedByLineLabelPick != null)
            return;

        GUIManager.Instance.CreateText(
            text: "Tooltip line",
            parent: InputCraftedByPanel.transform,
            anchorMin: new Vector2(0.5f, 1f),
            anchorMax: new Vector2(0.5f, 1f),
            position: new Vector2(-118f, -90f),
            font: GUIManager.Instance.AveriaSerifBold,
            fontSize: 16,
            color: Color.white,
            outline: true,
            outlineColor: Color.black,
            width: 100f,
            height: 28f,
            addContentSizeFitter: false);

        _buttonCraftedByLineLabelPick = GUIManager.Instance.CreateButton(
            text: "…",
            parent: InputCraftedByPanel.transform,
            anchorMin: new Vector2(0.5f, 1f),
            anchorMax: new Vector2(0.5f, 1f),
            position: new Vector2(32f, -90f),
            width: 210f,
            height: 28f).GetComponent<Button>();
        _buttonCraftedByLineLabelPick.AddUniqueListener(() =>
        {
            if (_buttonCraftedByLineLabelPick == null || !_buttonCraftedByLineLabelPick.interactable)
                return;
            ToggleCraftedByLineLabelPopover();
        });

        _craftedByLineLabelPopover = GUIManager.Instance.CreateWoodpanel(
            parent: InputCraftedByPanel.transform,
            anchorMin: new Vector2(0.5f, 1f),
            anchorMax: new Vector2(0.5f, 1f),
            position: new Vector2(24f, -118f),
            width: 226,
            height: 48,
            draggable: false);
        _craftedByLineLabelPopover.SetActive(false);
    }

    internal static void RefreshCraftedByLineLabelPicker(ItemDrop.ItemData item)
    {
        EnsureCraftedByLineLabelControls();
        if (_buttonCraftedByLineLabelPick == null)
            return;

        bool mayCustomize = RenameitConfig.CraftedByLabelCustomizable ||
                            RenameitPermission.IsElevatedForOverrides(Player.m_localPlayer);
        var options = RenameitConfig.GetCraftedByAllowedLabelsList();

        string? stored = item.m_customData != null &&
                         item.m_customData.TryGetValue(DrakeRenameit.DrakeCraftedByLineLabel, out var ls) &&
                         !string.IsNullOrEmpty(ls)
            ? ls
            : null;

        int idx = 0;
        string? pending = null;
        if (mayCustomize && stored != null)
        {
            for (int i = 1; i < options.Count; i++)
            {
                if (!string.Equals(options[i], stored, StringComparison.Ordinal))
                    continue;
                idx = i;
                pending = stored;
                break;
            }
        }

        _craftedByLineLabelPendingToken = pending;
        SetCraftedByLineLabelPickButtonText(options, idx);
        _buttonCraftedByLineLabelPick.interactable = mayCustomize;
        CloseCraftedByLineLabelPopover();
    }

    static void SetCraftedByLineLabelPickButtonText(List<string> options, int idx)
    {
        if (_buttonCraftedByLineLabelPick == null)
            return;
        string label = idx <= 0 || idx >= options.Count
            ? LocalizedDefaultCraftedByCaption()
            : options[idx];
        var t = _buttonCraftedByLineLabelPick.GetComponentInChildren<Text>();
        if (t != null)
            t.text = label + "  \u25BC";
    }

    static string LocalizedDefaultCraftedByCaption()
    {
        if (Localization.instance != null)
        {
            var s = Localization.instance.Localize("$item_crafter");
            if (!string.IsNullOrEmpty(s))
                return s;
        }

        var list = RenameitConfig.GetCraftedByAllowedLabelsList();
        return list.Count > 0 ? list[0] : "Crafted by";
    }

    static void ToggleCraftedByLineLabelPopover()
    {
        if (_craftedByLineLabelPopover == null)
            return;
        if (_craftedByLineLabelPopover.activeSelf)
        {
            CloseCraftedByLineLabelPopover();
            return;
        }

        RebuildCraftedByLineLabelPopoverContent();
        _craftedByLineLabelPopover.SetActive(true);
        _craftedByLineLabelPopover.transform.SetAsLastSibling();
    }

    static void CloseCraftedByLineLabelPopover()
    {
        if (_craftedByLineLabelPopover != null)
            _craftedByLineLabelPopover.SetActive(false);
    }

    static void RebuildCraftedByLineLabelPopoverContent()
    {
        if (_craftedByLineLabelPopover == null || GUIManager.Instance == null)
            return;

        for (int i = _craftedByLineLabelPopover.transform.childCount - 1; i >= 0; i--)
            UnityEngine.Object.Destroy(_craftedByLineLabelPopover.transform.GetChild(i).gameObject);

        var options = RenameitConfig.GetCraftedByAllowedLabelsList();
        const float rowH = 28f;
        const float pad = 6f;
        float h = pad * 2f + options.Count * rowH;
        var rt = _craftedByLineLabelPopover.GetComponent<RectTransform>();
        if (rt != null)
            rt.sizeDelta = new Vector2(220f, h);

        for (int i = 0; i < options.Count; i++)
        {
            int capture = i;
            string rowText = i == 0 ? LocalizedDefaultCraftedByCaption() : options[i];
            var btn = GUIManager.Instance.CreateButton(
                text: rowText,
                parent: _craftedByLineLabelPopover.transform,
                anchorMin: new Vector2(0.5f, 1f),
                anchorMax: new Vector2(0.5f, 1f),
                position: new Vector2(0f, -pad - rowH * (i + 0.5f)),
                width: 200f,
                height: rowH - 2f).GetComponent<Button>();
            btn.AddUniqueListener(() =>
            {
                _craftedByLineLabelPendingToken = capture == 0 ? null : options[capture];
                SetCraftedByLineLabelPickButtonText(options, capture);
                CloseCraftedByLineLabelPopover();
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