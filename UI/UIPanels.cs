using System;
using System.Collections.Generic;
using DrakeRenameit.API;
using DrakeRenameit.Ext.UI;
using DrakeRenameit.ModText;
using DrakeModsLibs.Data;
using DrakeModsLibs.UI;
using static DrakeRenameit.ModText.RenameItLocalization;
using Jotunn.Managers;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Text = UnityEngine.UI.Text;
using Image = UnityEngine.UI.Image;

namespace DrakeRenameit.UI;

public static class UIPanels
{
    public static GameObject? InputNamePanel { get; private set; }
    public static GameObject? InputDescPanel { get; private set; }
    public static InputField? RenameNameInput { get; private set; }
    public static InputField? RenameDescInput { get; private set; }
    private static Button _buttonOkName = default!;
    private static Button _buttonOkDesc = default!;
    private static Button _buttonCancelName = default!;
    private static Button _buttonCancelDesc = default!;
    private static Button? _buttonResetName = default!;
    private static Button? _buttonResetDesc = default!;
    private static GameObject? _publicRow;
    private static Toggle? _publicToggle;
    private static Text? _publicTipText;
    private static GameObject? _publicHoverTip;

    public static GameObject? ActionMenuPanel { get; private set; }
    private static Button? _buttonMenuRename;
    private static Button? _buttonMenuDesc;
    private static Button? _buttonMenuCraftedBy;
    private static Button? _buttonMenuResetAll;
    private static Button? _buttonMenuUnlock;
    private static Button? _buttonMenuCancel;
    private static Text? _actionMenuTitleText;
    private static Text? _craftedByPanelTitleText;
    private static Text? _craftedByTooltipLineLabelText;

    public static GameObject? InputCraftedByPanel { get; private set; }
    public static InputField? RenameCraftedByInput { get; private set; }
    private static Button? _buttonOkCraftedBy;
    private static Button? _buttonCancelCraftedBy;
    private static Button? _buttonResetCraftedBy;
    private static Button? _buttonCraftedByLineLabelPick;
    private static GameObject? _craftedByLineLabelPopover;
    private static string? _craftedByLineLabelPendingToken;

    const float ActionMenuButtonWidth = 200f;
    const float ActionMenuOkButtonWidth = 64f;
    const float ActionMenuBottomButtonGap = 8f;
    const float ActionMenuResetButtonWidth =
        ActionMenuButtonWidth - ActionMenuOkButtonWidth - ActionMenuBottomButtonGap;

    const float CraftedByPanelWidth = 400f;
    const float CraftedByPanelHeight = 258f;
    const float CraftedByContentWidth = 340f;
    const float CraftedByDropdownWidth = 280f;
    const float CraftedByDropdownHeight = 32f;
    const float CraftedByPopoverWidth = 290f;
    const float CraftedByPopoverRowHeight = 32f;
    const float CraftedByFooterButtonWidth = 72f;
    const float CraftedByInputAnchorY = -56f;
    const float CraftedByFooterButtonY = 32f;

    /// <summary>Line label applied on crafted-by OK when allowed; null clears <see cref="DrakeCustomDataKeys.CraftedByLineLabel"/>.</summary>
    internal static string? CraftedByLineLabelPendingToken => _craftedByLineLabelPendingToken;

    // Unlock confirmation sub-panel
    private static GameObject? _unlockConfirmPanel;
    private static RectTransform? _unlockCostListRoot;
    private static Text? _unlockAffordWarning;
    private static Button? _buttonConfirmUnlock;
    private static Button? _buttonConfirmCancel;
    private static Text? _unlockPanelTitleText;
    private static Text? _unlockCostLabelText;

    static readonly DrakeConfirmPanel ResetAllConfirm = new DrakeConfirmPanel("renameit_reset_all_confirm");

    /// <summary>Forwards to <see cref="DrakeGuiInput"/> (shared with LockSmith wood panels).</summary>
    internal static void EnsureInputBlocked() => DrakeGuiInput.EnsureBlocked();

    /// <summary>Forwards to <see cref="DrakeGuiInput"/>.</summary>
    internal static void EnsureInputUnblocked() => DrakeGuiInput.EnsureUnblocked();

    static void SetButtonLabel(Button? button, string label)
    {
        if (button == null)
            return;
        var text = button.GetComponentInChildren<Text>(true);
        if (text != null)
            text.text = label;
    }

    static void RefreshActionMenuLabels()
    {
        if (_actionMenuTitleText != null)
            _actionMenuTitleText.text = T(LKeys.MenuTitle);
        SetButtonLabel(_buttonMenuRename, T(LKeys.MenuRename));
        SetButtonLabel(_buttonMenuDesc, T(LKeys.MenuDescription));
        SetButtonLabel(_buttonMenuCraftedBy, T(LKeys.MenuCraftedBy));
        SetButtonLabel(_buttonMenuResetAll, T(LKeys.MenuResetAll));
        SetButtonLabel(_buttonMenuCancel, T(LKeys.MenuOk));
    }

    static void RefreshUnlockPanelStaticLabels()
    {
        if (_unlockPanelTitleText != null)
            _unlockPanelTitleText.text = T(LKeys.UnlockPanelTitle);
        if (_unlockCostLabelText != null)
            _unlockCostLabelText.text = T(LKeys.UnlockCostLabel);
        SetButtonLabel(_buttonConfirmUnlock, T(LKeys.UnlockPayBtn));
        SetButtonLabel(_buttonConfirmCancel, T(LKeys.BtnCancel));
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
        ResetAllConfirm.Close();
        DrakeRenameit.CurrentItem = null;
        EnsureInputUnblocked();
        DrakeTabHost.NotifyFeatureClosed(DrakeTabRegistration.RenameItTabId);
    }

    /// <summary>Tab host switching away from Rename — hide chrome without clearing host session twice.</summary>
    internal static void HideForTabHost()
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
        ResetAllConfirm.Close();
        DrakeRenameit.CurrentItem = null;
        EnsureInputUnblocked();
    }

    /// <summary>Discards unsaved edits and returns to the action menu (does not write to the item).</summary>
    public static void CancelNameEditor() =>
        CancelEditor(
            () =>
            {
                if (DrakeRenameit.CurrentItem != null && RenameNameInput != null)
                    RenameNameInput.text = DrakeRenameit.GetPropperName(DrakeRenameit.CurrentItem);
            },
            () => InputNamePanel?.SetActive(false));

    public static void CancelDescEditor() =>
        CancelEditor(
            () =>
            {
                if (DrakeRenameit.CurrentItem != null && RenameDescInput != null)
                    RenameDescInput.text = DrakeRenameit.getPropperDesc(DrakeRenameit.CurrentItem);
            },
            () => InputDescPanel?.SetActive(false));

    public static void CancelCraftedByEditor() =>
        CancelEditor(
            () =>
            {
                if (DrakeRenameit.CurrentItem == null)
                    return;
                if (RenameCraftedByInput != null)
                    RenameCraftedByInput.text = DrakeRenameit.getCraftedByDisplay(DrakeRenameit.CurrentItem);
                RefreshCraftedByLineLabelPicker(DrakeRenameit.CurrentItem);
                CloseCraftedByLineLabelPopover();
            },
            () => InputCraftedByPanel?.SetActive(false));

    private static void CancelEditor(Action revertFields, Action hidePanel)
    {
        revertFields();
        hidePanel();
        var item = DrakeRenameit.CurrentItem;
        if (item != null && DrakeRenameit.IsItemInLocalPlayerInventory(item))
            OpenActionMenu(item);
        else
            CloseAllRenameEditingUi();
    }

    public static void OpenActionMenu(ItemDrop.ItemData item)
    {
        if (GUIManager.Instance == null || !GUIManager.CustomGUIFront)
            return;

        if (!DrakeRenameit.IsItemInLocalPlayerInventory(item))
        {
            CloseAllRenameEditingUi();
            ValheimHudMessage.Show(Player.m_localPlayer, MessageHud.MessageType.Center,
                T(LKeys.MsgItemNotInInventory));
            return;
        }

        EnsureActionMenu();
        if (ActionMenuPanel == null || _buttonMenuRename == null || _buttonMenuDesc == null || _buttonMenuCraftedBy == null ||
            _buttonMenuResetAll == null || _buttonMenuUnlock == null)
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
                    ? T(LKeys.MenuUnlock)
                    : T(LKeys.MenuUnlockCost, cost);
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
            _buttonMenuCraftedBy.interactable =
                !PaperItem.IsBlankPaper(item) && DrakeRenameit.CanChangeCraftedByLabel(item, false);
            _buttonMenuResetAll.interactable = DrakeRenameit.CanResetAnyCustomization(item);
        }

        RefreshActionMenuLabels();
        ApplyActionMenuLayout();
        SyncPublicRewriteToggle(item);
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
            height: 320,
            draggable: false);

        _actionMenuTitleText = GUIManager.Instance.CreateText(
            text: T(LKeys.MenuTitle),
            parent: ActionMenuPanel.transform,
            anchorMin: new Vector2(0.5f, 1f),
            anchorMax: new Vector2(0.5f, 1f),
            position: new Vector2(0f, -48f),
            font: GUIManager.Instance.AveriaSerifBold,
            fontSize: 22,
            color: GUIManager.Instance.ValheimOrange,
            outline: true,
            outlineColor: Color.black,
            width: ActionMenuButtonWidth,
            height: 40,
            addContentSizeFitter: false).GetComponent<Text>();
        _actionMenuTitleText.alignment = TextAnchor.MiddleCenter;

        _buttonMenuUnlock = SoftenButton(GUIManager.Instance.CreateButton(
            text: T(LKeys.MenuUnlock),
            parent: ActionMenuPanel.transform,
            anchorMin: new Vector2(0.5f, 0.5f),
            anchorMax: new Vector2(0.5f, 0.5f),
            position: new Vector2(0f, 72f),
            width: 220f,
            height: 30f));
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

        _buttonMenuRename = SoftenButton(GUIManager.Instance.CreateButton(
            text: T(LKeys.MenuRename),
            parent: ActionMenuPanel.transform,
            anchorMin: new Vector2(0.5f, 0.5f),
            anchorMax: new Vector2(0.5f, 0.5f),
            position: new Vector2(0f, 40f),
            width: ActionMenuButtonWidth,
            height: 32f));
        _buttonMenuRename.AddUniqueListener(() =>
        {
            var item = DrakeRenameit.CurrentItem;
            HideActionMenuForSubmenu();
            if (item != null)
                DrakeRenameit.OpenRename(item);
        });

        _buttonMenuDesc = SoftenButton(GUIManager.Instance.CreateButton(
            text: T(LKeys.MenuDescription),
            parent: ActionMenuPanel.transform,
            anchorMin: new Vector2(0.5f, 0.5f),
            anchorMax: new Vector2(0.5f, 0.5f),
            position: new Vector2(0f, 0f),
            width: ActionMenuButtonWidth,
            height: 32f));
        _buttonMenuDesc.AddUniqueListener(() =>
        {
            var item = DrakeRenameit.CurrentItem;
            HideActionMenuForSubmenu();
            if (item != null)
                DrakeRenameit.OpenRewriteDesc(item);
        });

        _buttonMenuCraftedBy = SoftenButton(GUIManager.Instance.CreateButton(
            text: T(LKeys.MenuCraftedBy),
            parent: ActionMenuPanel.transform,
            anchorMin: new Vector2(0.5f, 0.5f),
            anchorMax: new Vector2(0.5f, 0.5f),
            position: new Vector2(0f, -40f),
            width: ActionMenuButtonWidth,
            height: 32f));
        _buttonMenuCraftedBy.AddUniqueListener(() =>
        {
            var item = DrakeRenameit.CurrentItem;
            HideActionMenuForSubmenu();
            if (item != null)
                DrakeRenameit.OpenCraftedByEditor(item);
        });

        _buttonMenuResetAll = SoftenButton(GUIManager.Instance.CreateButton(
            text: T(LKeys.MenuResetAll),
            parent: ActionMenuPanel.transform,
            anchorMin: new Vector2(0.5f, 0.5f),
            anchorMax: new Vector2(0.5f, 0.5f),
            position: new Vector2(-40f, -80f),
            width: ActionMenuResetButtonWidth,
            height: 28f));
        _buttonMenuResetAll.AddUniqueListener(() =>
        {
            var item = DrakeRenameit.CurrentItem;
            if (item != null)
                OpenResetAllConfirmPanel(item);
        });

        _buttonMenuCancel = SoftenButton(GUIManager.Instance.CreateButton(
            text: T(LKeys.MenuOk),
            parent: ActionMenuPanel.transform,
            anchorMin: new Vector2(0.5f, 0.5f),
            anchorMax: new Vector2(0.5f, 0.5f),
            position: new Vector2(40f, -80f),
            width: ActionMenuOkButtonWidth,
            height: 28f));
        _buttonMenuCancel.AddUniqueListener(CloseActionMenuOnly);

        EnsurePublicCheckRow();
        ApplyActionMenuLayout();
        SoftenDrakeButtonSfx(ActionMenuPanel);
    }

    static void EnsurePublicCheckRow()
    {
        if (ActionMenuPanel == null || GUIManager.Instance == null || _publicRow != null)
            return;

        _publicRow = new GameObject("public_rewrite", typeof(RectTransform));
        _publicRow.transform.SetParent(ActionMenuPanel.transform, false);
        var hit = _publicRow.AddComponent<Image>();
        hit.color = new Color(0f, 0f, 0f, 0f);
        hit.raycastTarget = true;
        var rowRt = _publicRow.GetComponent<RectTransform>();
        rowRt.anchorMin = rowRt.anchorMax = new Vector2(0.5f, 0.5f);
        rowRt.pivot = new Vector2(0.5f, 0.5f);
        rowRt.anchoredPosition = new Vector2(0f, -72f);
        rowRt.sizeDelta = new Vector2(ActionMenuButtonWidth, 28f);

        // Jotunn checkbox (checkbox + checkbox_marker). CreateToggle sizes the box to width/height.
        const float box = 24f;
        var toggleGo = GUIManager.Instance.CreateToggle(_publicRow.transform, box, box);
        toggleGo.transform.SetParent(_publicRow.transform, false);
        _publicToggle = toggleGo.GetComponent<Toggle>();
        if (_publicToggle != null)
        {
            _publicToggle.navigation = new Navigation { mode = Navigation.Mode.None };
            _publicToggle.onValueChanged.AddListener(on =>
            {
                var item = DrakeRenameit.CurrentItem;
                if (item == null ||
                    !Permissions.RenamePermissionManager.CanChangePublicFlag(item, Player.m_localPlayer))
                {
                    SyncPublicRewriteToggle(item);
                    return;
                }
                Permissions.RenamePermissionManager.SetPublicRewriteFlag(item, on);
            });
        }

        var toggleRt = toggleGo.GetComponent<RectTransform>();
        if (toggleRt != null)
        {
            toggleRt.anchorMin = toggleRt.anchorMax = new Vector2(0.5f, 0.5f);
            toggleRt.pivot = new Vector2(0.5f, 0.5f);
            toggleRt.anchoredPosition = Vector2.zero;
            toggleRt.sizeDelta = new Vector2(ActionMenuButtonWidth, 28f);
        }

        if (toggleGo.transform.Find("Background") is RectTransform bg)
        {
            bg.anchorMin = bg.anchorMax = new Vector2(0.5f, 0.5f);
            bg.pivot = new Vector2(1f, 0.5f);
            bg.anchoredPosition = new Vector2(-6f, 0f);
            bg.sizeDelta = new Vector2(box, box);
            if (bg.Find("Checkmark") is RectTransform mark)
            {
                mark.anchorMin = mark.anchorMax = new Vector2(0.5f, 0.5f);
                mark.pivot = new Vector2(0.5f, 0.5f);
                mark.anchoredPosition = Vector2.zero;
                mark.sizeDelta = new Vector2(16f, 16f);
            }
        }

        var label = toggleGo.GetComponentInChildren<Text>();
        if (label != null)
        {
            label.text = "Public";
            label.font = GUIManager.Instance.AveriaSerifBold;
            label.fontSize = 16;
            label.color = Color.white;
            label.alignment = TextAnchor.MiddleLeft;
            label.raycastTarget = false;
            var labelRt = label.rectTransform;
            labelRt.anchorMin = new Vector2(0.5f, 0f);
            labelRt.anchorMax = new Vector2(0.5f, 1f);
            labelRt.pivot = new Vector2(0f, 0.5f);
            labelRt.anchoredPosition = new Vector2(2f, 0f);
            labelRt.sizeDelta = new Vector2(90f, 28f);
        }

        // Selectable (the toggle) consumes pointer-enter, so the tip has to live on the
        // checkbox itself — a parent handler never runs while the box is under the cursor.
        var tip = EnsurePublicHoverTip();
        _publicRow.AddComponent<PublicRewriteHover>().Tip = tip;
        toggleGo.AddComponent<PublicRewriteHover>().Tip = tip;
    }

    static GameObject EnsurePublicHoverTip()
    {
        if (_publicHoverTip != null)
            return _publicHoverTip;

        _publicHoverTip = new GameObject("public_tip", typeof(RectTransform));
        _publicHoverTip.transform.SetParent(_publicRow!.transform, false);
        var tipRt = _publicHoverTip.GetComponent<RectTransform>();
        tipRt.anchorMin = tipRt.anchorMax = new Vector2(0.5f, 0.5f);
        tipRt.pivot = new Vector2(0.5f, 0f);
        tipRt.anchoredPosition = new Vector2(0f, 18f);
        tipRt.sizeDelta = new Vector2(260f, 52f);

        var bg = _publicHoverTip.AddComponent<Image>();
        bg.color = new Color(0.06f, 0.05f, 0.04f, 0.94f);
        bg.raycastTarget = false;

        var tipTextGo = GUIManager.Instance.CreateText(
            text: "Anyone can rewrite this item's name and description.",
            parent: _publicHoverTip.transform,
            anchorMin: new Vector2(0.5f, 0.5f),
            anchorMax: new Vector2(0.5f, 0.5f),
            position: Vector2.zero,
            font: GUIManager.Instance.AveriaSerifBold,
            fontSize: 13,
            color: Color.white,
            outline: true,
            outlineColor: Color.black,
            width: 244f,
            height: 44f,
            addContentSizeFitter: false);
        _publicTipText = tipTextGo.GetComponent<Text>();
        if (_publicTipText != null)
        {
            _publicTipText.alignment = TextAnchor.MiddleCenter;
            _publicTipText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _publicTipText.verticalOverflow = VerticalWrapMode.Overflow;
            _publicTipText.raycastTarget = false;
        }

        _publicHoverTip.SetActive(false);
        return _publicHoverTip;
    }

    /// <summary>
    /// Soften helpers forward to <see cref="DrakeButtonSfx"/> (kept as local names for call sites).
    /// </summary>
    static void SoftenDrakeButtonSfx(GameObject? root) => DrakeButtonSfx.Soften(root);

    static Button SoftenButton(GameObject buttonGo) => DrakeButtonSfx.SoftenButton(buttonGo);

    static void ApplyActionMenuLayout()
    {
        if (_actionMenuTitleText != null)
        {
            _actionMenuTitleText.alignment = TextAnchor.MiddleCenter;
            var titleRt = _actionMenuTitleText.rectTransform;
            titleRt.anchoredPosition = new Vector2(0f, -48f);
            titleRt.sizeDelta = new Vector2(ActionMenuButtonWidth, 40f);
        }

        float menuHalf = ActionMenuButtonWidth * 0.5f;
        float resetHalf = ActionMenuResetButtonWidth * 0.5f;
        float okHalf = ActionMenuOkButtonWidth * 0.5f;
        float resetCenterX = -menuHalf + resetHalf;
        float okCenterX = menuHalf - okHalf;

        SetButtonLayout(_buttonMenuRename, 0f, 56f, ActionMenuButtonWidth, 32f);
        SetButtonLayout(_buttonMenuDesc, 0f, 16f, ActionMenuButtonWidth, 32f);
        SetButtonLayout(_buttonMenuCraftedBy, 0f, -24f, ActionMenuButtonWidth, 32f);
        SetButtonLayout(_buttonMenuResetAll, resetCenterX, -120f, ActionMenuResetButtonWidth, 28f);
        SetButtonLayout(_buttonMenuCancel, okCenterX, -120f, ActionMenuOkButtonWidth, 28f);
        SetButtonLayout(_buttonMenuUnlock, 0f, 96f, 220f, 30f);

        if (_publicRow != null)
        {
            var rowRt = _publicRow.GetComponent<RectTransform>();
            if (rowRt != null)
            {
                rowRt.anchorMin = rowRt.anchorMax = new Vector2(0.5f, 0.5f);
                rowRt.pivot = new Vector2(0.5f, 0.5f);
                rowRt.anchoredPosition = new Vector2(0f, -72f);
                rowRt.sizeDelta = new Vector2(ActionMenuButtonWidth, 28f);
            }
        }
    }

    static void SetButtonLayout(Button? button, float x, float y, float width, float height)
    {
        if (button == null)
            return;
        var rt = button.GetComponent<RectTransform>();
        if (rt == null)
            return;
        rt.anchoredPosition = new Vector2(x, y);
        rt.sizeDelta = new Vector2(width, height);
    }

    /// <summary>
    /// Hide the action menu while opening a submenu (rename / desc / crafted-by).
    /// Does not end the DrakeTabHost session — otherwise Lock|Rename tabs vanish until reopen.
    /// </summary>
    private static void HideActionMenuForSubmenu()
    {
        if (_publicHoverTip != null)
            _publicHoverTip.SetActive(false);
        if (ActionMenuPanel != null)
            ActionMenuPanel.SetActive(false);
        // Leave input blocked; the editor panel takes over.
    }

    /// <summary>User dismissed the action menu (OK) — end tab-host session.</summary>
    private static void CloseActionMenuOnly()
    {
        if (_publicHoverTip != null)
            _publicHoverTip.SetActive(false);
        if (ActionMenuPanel != null)
            ActionMenuPanel.SetActive(false);
        EnsureInputUnblocked();
        DrakeTabHost.NotifyFeatureClosed(DrakeTabRegistration.RenameItTabId);
    }

    // -------------------------------------------------------------------------
    // Reset all confirmation (DrakeConfirmPanel — RenameIt-standard chrome)
    // -------------------------------------------------------------------------

    private static void OpenResetAllConfirmPanel(ItemDrop.ItemData item)
    {
        if (GUIManager.Instance == null || !GUIManager.CustomGUIFront)
            return;

        if (!DrakeRenameit.IsItemInLocalPlayerInventory(item))
        {
            CloseAllRenameEditingUi();
            ValheimHudMessage.Show(Player.m_localPlayer, MessageHud.MessageType.Center,
                T(LKeys.MsgItemNotInInventory));
            return;
        }

        DrakeRenameit.CurrentItem = item;
        ResetAllConfirm.Show(
            T(LKeys.ResetAllTitle),
            T(LKeys.ResetAllBody),
            onYes: () =>
            {
                var current = DrakeRenameit.CurrentItem;
                if (current != null)
                    DrakeRenameit.ResetAllCustomizations(current);
                if (ActionMenuPanel != null)
                    ActionMenuPanel.SetActive(false);
                DrakeRenameit.CurrentItem = null;
                DrakeTabHost.NotifyFeatureClosed(DrakeTabRegistration.RenameItTabId);
                // DrakeConfirmPanel.Close already unblocked.
            },
            onNo: () =>
            {
                var current = DrakeRenameit.CurrentItem;
                if (current != null)
                {
                    ActionMenuPanel?.SetActive(false);
                    OpenActionMenu(current);
                }
            },
            yesLabel: T(LKeys.BtnYes),
            noLabel: T(LKeys.BtnNo));
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
            ValheimHudMessage.Show(Player.m_localPlayer, MessageHud.MessageType.Center,
                T(LKeys.MsgItemNotInInventory));
            return;
        }

        EnsureUnlockConfirmPanel();
        if (_unlockConfirmPanel == null || _unlockCostListRoot == null || _buttonConfirmUnlock == null)
            return;

        RefreshUnlockPanelStaticLabels();
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
            AddUnlockPlainLine(_unlockCostListRoot, T(LKeys.UnlockPrompt));
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
            string line = T(LKeys.UnlockCostLine, amount, localizedName, haveColor, have);
            var sprite = RenameUnlockCost.GetItemIconSprite(prefabName);
            CreateUnlockCostRow(_unlockCostListRoot, sprite, line);
        }

        if (_unlockAffordWarning != null)
        {
            if (!canAfford)
            {
                _unlockAffordWarning.text = T(LKeys.UnlockCostAffordWarning);
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

        _unlockPanelTitleText = GUIManager.Instance.CreateText(
            text: T(LKeys.UnlockPanelTitle),
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
            addContentSizeFitter: false).GetComponent<Text>();

        _unlockCostLabelText = GUIManager.Instance.CreateText(
            text: T(LKeys.UnlockCostLabel),
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
            addContentSizeFitter: false).GetComponent<Text>();

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

        _buttonConfirmUnlock = SoftenButton(GUIManager.Instance.CreateButton(
            text: T(LKeys.UnlockPayBtn),
            parent: _unlockConfirmPanel.transform,
            anchorMin: new Vector2(0.5f, 0f),
            anchorMax: new Vector2(0.5f, 0f),
            position: new Vector2(-62f, 35f),
            width: 120f,
            height: 30f));
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

        _buttonConfirmCancel = SoftenButton(GUIManager.Instance.CreateButton(
            text: T(LKeys.BtnCancel),
            parent: _unlockConfirmPanel.transform,
            anchorMin: new Vector2(0.5f, 0f),
            anchorMax: new Vector2(0.5f, 0f),
            position: new Vector2(55f, 35f),
            width: 110f,
            height: 30f));
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
            DrakeTabHost.NotifyFeatureClosed(DrakeTabRegistration.RenameItTabId);
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
                width: CraftedByPanelWidth,
                height: CraftedByPanelHeight,
                draggable: false);

            _craftedByPanelTitleText = GUIManager.Instance.CreateText(
                text: T(LKeys.PanelCraftedByTitle),
                parent: InputCraftedByPanel.transform,
                anchorMin: new Vector2(0.5f, 1f),
                anchorMax: new Vector2(0.5f, 1f),
                position: new Vector2(0f, -44f),
                font: GUIManager.Instance.AveriaSerifBold,
                fontSize: 22,
                color: GUIManager.Instance.ValheimOrange,
                outline: true,
                outlineColor: Color.black,
                width: CraftedByContentWidth,
                height: 40,
                addContentSizeFitter: false).GetComponent<Text>();
            _craftedByPanelTitleText.alignment = TextAnchor.MiddleCenter;
        }

        InputCraftedByPanel.SetActive(true);
        InputCraftedByPanel.transform.SetAsLastSibling();
        EnsureCraftedByLineLabelControls();
        ApplyCraftedByPanelLayout();

        if (RenameCraftedByInput == null)
        {
            RenameCraftedByInput = GUIManager.Instance.CreateInputField(
                parent: InputCraftedByPanel.transform,
                anchorMin: new Vector2(0.5f, 0.5f),
                anchorMax: new Vector2(0.5f, 0.5f),
                position: new Vector2(0f, CraftedByInputAnchorY),
                contentType: InputField.ContentType.Standard,
                placeholderText: T(LKeys.PlaceholderCraftedBy),
                fontSize: 18,
                width: CraftedByContentWidth,
                height: 34f).GetComponent<InputField>();
            CenterInputFieldText(RenameCraftedByInput);
        }

        RenameCraftedByInput!.characterLimit = RenameitConfig.CraftedByCharLimit;
        RenameCraftedByInput.text = DrakeRenameit.getCraftedByDisplay(DrakeRenameit.CurrentItem);
        if (DrakeRenameit.CurrentItem != null)
            RefreshCraftedByLineLabelPicker(DrakeRenameit.CurrentItem);

        if (_buttonCancelCraftedBy == null)
        {
            _buttonCancelCraftedBy = SoftenButton(GUIManager.Instance.CreateButton(
                text: T(LKeys.BtnCancel),
                parent: InputCraftedByPanel.transform,
                anchorMin: new Vector2(0.5f, 0f),
                anchorMax: new Vector2(0.5f, 0f),
                position: new Vector2(-134f, CraftedByFooterButtonY),
                width: CraftedByFooterButtonWidth,
                height: 30f));
            _buttonCancelCraftedBy.AddUniqueListener(CancelCraftedByEditor);
        }

        if (_buttonOkCraftedBy == null)
        {
            _buttonOkCraftedBy = SoftenButton(GUIManager.Instance.CreateButton(
                text: T(LKeys.BtnOk),
                parent: InputCraftedByPanel.transform,
                anchorMin: new Vector2(0.5f, 0f),
                anchorMax: new Vector2(0.5f, 0f),
                position: new Vector2(0f, CraftedByFooterButtonY),
                width: CraftedByFooterButtonWidth,
                height: 30f));
            _buttonOkCraftedBy.AddUniqueListener(() =>
            {
                DrakeRenameit.ApplyCraftedByLabel(RenameCraftedByInput.text.Trim());
            });
        }

        if (_buttonResetCraftedBy == null)
        {
            _buttonResetCraftedBy = SoftenButton(GUIManager.Instance.CreateButton(
                text: T(LKeys.BtnReset),
                parent: InputCraftedByPanel.transform,
                anchorMin: new Vector2(0.5f, 0f),
                anchorMax: new Vector2(0.5f, 0f),
                position: new Vector2(134f, CraftedByFooterButtonY),
                width: CraftedByFooterButtonWidth,
                height: 30f));
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

    static void ApplyCraftedByPanelLayout()
    {
        if (InputCraftedByPanel == null)
            return;

        var panelRt = InputCraftedByPanel.GetComponent<RectTransform>();
        if (panelRt != null)
            panelRt.sizeDelta = new Vector2(CraftedByPanelWidth, CraftedByPanelHeight);

        if (_craftedByPanelTitleText != null)
        {
            _craftedByPanelTitleText.alignment = TextAnchor.MiddleCenter;
            var titleRt = _craftedByPanelTitleText.rectTransform;
            titleRt.anchoredPosition = new Vector2(0f, -44f);
            titleRt.sizeDelta = new Vector2(CraftedByContentWidth, 40f);
        }

        const float dropdownY = -98f;
        const float labelAboveY = -72f;

        if (_craftedByTooltipLineLabelText != null)
        {
            _craftedByTooltipLineLabelText.alignment = TextAnchor.MiddleCenter;
            var labelRt = _craftedByTooltipLineLabelText.rectTransform;
            labelRt.anchoredPosition = new Vector2(0f, labelAboveY);
            labelRt.sizeDelta = new Vector2(CraftedByDropdownWidth, 22f);
        }

        if (_buttonCraftedByLineLabelPick != null)
        {
            var pickRt = _buttonCraftedByLineLabelPick.GetComponent<RectTransform>();
            if (pickRt != null)
            {
                pickRt.sizeDelta = new Vector2(CraftedByDropdownWidth, CraftedByDropdownHeight);
                pickRt.anchoredPosition = new Vector2(0f, dropdownY);
            }
        }

        if (_craftedByLineLabelPopover != null)
        {
            var popRt = _craftedByLineLabelPopover.GetComponent<RectTransform>();
            if (popRt != null)
                popRt.anchoredPosition = new Vector2(0f, dropdownY - CraftedByDropdownHeight - 6f);
        }

        if (RenameCraftedByInput != null)
        {
            var inputRt = RenameCraftedByInput.GetComponent<RectTransform>();
            if (inputRt != null)
            {
                inputRt.sizeDelta = new Vector2(CraftedByContentWidth, 34f);
                inputRt.anchoredPosition = new Vector2(0f, CraftedByInputAnchorY);
            }

            CenterInputFieldText(RenameCraftedByInput);
        }

        float contentHalf = CraftedByContentWidth * 0.5f;
        float btnHalf = CraftedByFooterButtonWidth * 0.5f;
        SetButtonLayout(_buttonCancelCraftedBy, -contentHalf + btnHalf, CraftedByFooterButtonY, CraftedByFooterButtonWidth, 30f);
        SetButtonLayout(_buttonOkCraftedBy, 0f, CraftedByFooterButtonY, CraftedByFooterButtonWidth, 30f);
        SetButtonLayout(_buttonResetCraftedBy, contentHalf - btnHalf, CraftedByFooterButtonY, CraftedByFooterButtonWidth, 30f);
    }

    static void CenterInputFieldText(InputField? field)
    {
        if (field?.textComponent == null)
            return;
        field.textComponent.alignment = TextAnchor.MiddleCenter;
        field.textComponent.horizontalOverflow = HorizontalWrapMode.Overflow;
        if (field.placeholder != null)
        {
            var ph = field.placeholder.GetComponent<Text>();
            if (ph != null)
                ph.alignment = TextAnchor.MiddleCenter;
        }
    }

    static void EnsureCraftedByLineLabelControls()
    {
        if (InputCraftedByPanel == null || GUIManager.Instance == null)
            return;

        ApplyCraftedByPanelLayout();

        if (_buttonCraftedByLineLabelPick != null)
            return;

        _craftedByTooltipLineLabelText = GUIManager.Instance.CreateText(
            text: T(LKeys.TooltipLineLabel),
            parent: InputCraftedByPanel.transform,
            anchorMin: new Vector2(0.5f, 1f),
            anchorMax: new Vector2(0.5f, 1f),
            position: new Vector2(0f, -72f),
            font: GUIManager.Instance.AveriaSerifBold,
            fontSize: 16,
            color: Color.white,
            outline: true,
            outlineColor: Color.black,
            width: CraftedByDropdownWidth,
            height: 22f,
            addContentSizeFitter: false).GetComponent<Text>();
        _craftedByTooltipLineLabelText.alignment = TextAnchor.MiddleCenter;

        _buttonCraftedByLineLabelPick = SoftenButton(GUIManager.Instance.CreateButton(
            text: T(LKeys.CraftedByLinePick),
            parent: InputCraftedByPanel.transform,
            anchorMin: new Vector2(0.5f, 1f),
            anchorMax: new Vector2(0.5f, 1f),
            position: new Vector2(0f, -98f),
            width: CraftedByDropdownWidth,
            height: CraftedByDropdownHeight));
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
            position: new Vector2(0f, -98f - CraftedByDropdownHeight - 6f),
            width: CraftedByPopoverWidth,
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
                         item.m_customData.TryGetValue(DrakeCustomDataKeys.CraftedByLineLabel, out var ls) &&
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
        return list.Count > 0 ? list[0] : T(LKeys.CraftedByFallback);
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
        const float pad = 8f;
        float rowH = CraftedByPopoverRowHeight;
        float h = pad * 2f + options.Count * rowH;
        var rt = _craftedByLineLabelPopover.GetComponent<RectTransform>();
        if (rt != null)
            rt.sizeDelta = new Vector2(CraftedByPopoverWidth, h);

        float rowBtnWidth = CraftedByPopoverWidth - 16f;
        for (int i = 0; i < options.Count; i++)
        {
            int capture = i;
            string rowText = i == 0 ? LocalizedDefaultCraftedByCaption() : options[i];
            var btn = SoftenButton(GUIManager.Instance.CreateButton(
                text: rowText,
                parent: _craftedByLineLabelPopover.transform,
                anchorMin: new Vector2(0.5f, 1f),
                anchorMax: new Vector2(0.5f, 1f),
                position: new Vector2(0f, -pad - rowH * (i + 0.5f)),
                width: rowBtnWidth,
                height: rowH - 4f));
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
            text: T(LKeys.PanelRenameTitle),
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
                placeholderText: T(LKeys.PlaceholderRename),
                fontSize: 18,
                width: 300,
                height: 30f).GetComponent<InputField>();
        }

        RenameNameInput!.characterLimit = RenameitConfig.NameCharLimit;
        RenameNameInput.text = DrakeRenameit.GetPropperName(DrakeRenameit.CurrentItem);

        if (_buttonCancelName == null)
        {
            _buttonCancelName = SoftenButton(GUIManager.Instance.CreateButton(
                text: T(LKeys.BtnCancel),
                parent: InputNamePanel.transform,
                anchorMin: new Vector2(0.5f, 0f),
                anchorMax: new Vector2(0.5f, 0f),
                position: new Vector2(-100f, 35f),
                width: 72f,
                height: 30f));
            _buttonCancelName.gameObject.SetActive(true);
            _buttonCancelName.AddUniqueListener(CancelNameEditor);
        }

        if (_buttonOkName == null)
        {
            _buttonOkName = SoftenButton(GUIManager.Instance.CreateButton(
                text: T(LKeys.BtnOk),
                parent: InputNamePanel.transform,
                anchorMin: new Vector2(0.5f, 0f),
                anchorMax: new Vector2(0.5f, 0f),
                position: new Vector2(0f, 35f),
                width: 72f,
                height: 30f));

            _buttonOkName.gameObject.SetActive(true);
            
            _buttonOkName.GetComponent<Button>().AddUniqueListener(() =>
            {
                DrakeRenameit.ApplyRename(RenameNameInput.text.Trim());
            });
        }

        if (_buttonResetName == null)
        {
            _buttonResetName = SoftenButton(GUIManager.Instance.CreateButton(
                text: T(LKeys.BtnReset),
                parent: InputNamePanel.transform,
                anchorMin: new Vector2(0.5f, 0f),
                anchorMax: new Vector2(0.5f, 0f),
                position: new Vector2(100f, 35f),
                width: 72f,
                height: 30f));
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
            text: T(LKeys.PanelDescTitle),
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
                placeholderText: T(LKeys.PlaceholderDesc),
                fontSize: 16,
                width: 225,
                height: 240f).GetComponent<InputField>();
            RenameDescInput.contentType = InputField.ContentType.Standard;
            RenameDescInput.lineType = InputField.LineType.MultiLineNewline;
            RenameDescInput.text = DrakeRenameit.getPropperDesc(DrakeRenameit.CurrentItem);
        }

        RenameDescInput!.characterLimit = RenameitConfig.DescCharLimit;


        if (_buttonCancelDesc == null)
        {
            _buttonCancelDesc = SoftenButton(GUIManager.Instance.CreateButton(
                text: T(LKeys.BtnCancel),
                parent: InputDescPanel.transform,
                anchorMin: new Vector2(0.5f, 0f),
                anchorMax: new Vector2(0.5f, 0f),
                position: new Vector2(-80f, 35f),
                width: 68f,
                height: 30f));
            _buttonCancelDesc.gameObject.SetActive(true);
            _buttonCancelDesc.AddUniqueListener(CancelDescEditor);
        }

        if (_buttonOkDesc == null)
        {
            _buttonOkDesc = SoftenButton(GUIManager.Instance.CreateButton(
                text: T(LKeys.BtnOk),
                parent: InputDescPanel.transform,
                anchorMin: new Vector2(0.5f, 0f),
                anchorMax: new Vector2(0.5f, 0f),
                position: new Vector2(0f, 35f),
                width: 68f,
                height: 30f));
            _buttonOkDesc.gameObject.SetActive(true);
            _buttonOkDesc.AddUniqueListener(() =>
            {
                if (String.IsNullOrEmpty(RenameDescInput.text))
                {
                    GetPlayerAndSendError(T(LKeys.MsgDescEmpty));
                    return;
                }

                DrakeRenameit.ApplyRewriteDesc(RenameDescInput.text.Trim());
            });
        }

        if (_buttonResetDesc == null)
        {
            _buttonResetDesc = SoftenButton(GUIManager.Instance.CreateButton(
                text: T(LKeys.BtnReset),
                parent: InputDescPanel.transform,
                anchorMin: new Vector2(0.5f, 0f),
                anchorMax: new Vector2(0.5f, 0f),
                position: new Vector2(80f, 35f),
                width: 68f,
                height: 30f));
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
                ValheimHudMessage.Show(local, 
                    MessageHud.MessageType.Center, // or TopLeft, depending where you want it
                    msg
                );
            }
        }
    }

    internal static void SyncPublicRewriteToggle(ItemDrop.ItemData? item)
    {
        if (_publicRow == null)
            return;

        bool show = RenameitConfig.PublicRewriteEnabled && item != null && Player.m_localPlayer != null;
        _publicRow.SetActive(show);
        if (!show)
        {
            if (_publicHoverTip != null)
                _publicHoverTip.SetActive(false);
            return;
        }

        if (_publicToggle == null || item == null)
            return;

        bool canChange = Permissions.RenamePermissionManager.CanChangePublicFlag(item, Player.m_localPlayer);
        _publicToggle.interactable = canChange;
        _publicToggle.SetIsOnWithoutNotify(Permissions.RenamePermissionManager.HasPublicRewriteFlag(item));

        var label = _publicToggle.GetComponentInChildren<Text>();
        if (label != null)
            label.color = canChange ? Color.white : new Color(0.62f, 0.62f, 0.62f, 1f);

        if (_publicTipText != null)
        {
            _publicTipText.text = canChange
                ? "Anyone can rewrite this item's name and description."
                : "Only the original creator or an admin can change this.";
        }
    }
}

/// <summary>Hover tip for the main-menu Public checkbox.</summary>
internal sealed class PublicRewriteHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    internal GameObject? Tip;

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (Tip == null)
            return;
        Tip.SetActive(true);
        Tip.transform.SetAsLastSibling();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (Tip != null)
            Tip.SetActive(false);
    }
}