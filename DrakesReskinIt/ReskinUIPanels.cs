using System.Collections.Generic;
using System.Linq;
using Jotunn.Managers;
using UnityEngine;
using UnityEngine.UI;
using DrakesReskinIt.Ext.UI;
using Text = UnityEngine.UI.Text;

namespace DrakesReskinIt;

/// <summary>
/// Jotunn GUIManager UI panels for the DrakesReskinIt action menu.
/// Three panels: action menu (hub), icon picker, tint picker.
/// </summary>
public static class ReskinUIPanels
{
    // ─── Action menu ──────────────────────────────────────────────────────────
    public static GameObject? ActionMenuPanel { get; private set; }
    private static Button? _buttonMenuIcon;
    private static Button? _buttonMenuTint;
    private static Button? _buttonMenuResetAll;
    private static Button? _buttonMenuCancel;

    // ─── Icon picker ──────────────────────────────────────────────────────────
    public static GameObject? IconPickerPanel { get; private set; }
    private static GameObject? _iconScrollContent;
    private static Button? _buttonIconCancel;
    private static Button? _buttonIconClear;

    // ─── Tint picker ──────────────────────────────────────────────────────────
    public static GameObject? TintPickerPanel { get; private set; }
    private static InputField? _tintHexInput;
    private static Button? _buttonTintOk;
    private static Button? _buttonTintReset;
    private static Button? _buttonTintCancel;

    // ─── Preset tint colors offered in the picker ─────────────────────────────
    private static readonly (string label, string hex)[] TintPresets =
    {
        ("White", "#ffffff"),
        ("Red", "#ff3333"),
        ("Orange", "#ff8800"),
        ("Yellow", "#ffee00"),
        ("Green", "#44dd44"),
        ("Cyan", "#00ccff"),
        ("Blue", "#3366ff"),
        ("Purple", "#bb44ff"),
        ("Pink", "#ff77bb"),
        ("Brown", "#996633"),
        ("Grey", "#888888"),
        ("Black", "#111111"),
    };

    // ─── Open action menu ─────────────────────────────────────────────────────

    public static void OpenActionMenu(ItemDrop.ItemData item)
    {
        if (GUIManager.Instance == null || !GUIManager.CustomGUIFront)
            return;

        EnsureActionMenu();
        if (ActionMenuPanel == null)
            return;

        DrakesReskinIt.CurrentItem = item;

        _buttonMenuIcon!.interactable = DrakesReskinIt.CanChangeIcon(item, false);
        _buttonMenuTint!.interactable = DrakesReskinIt.CanChangeTint(item, false);
        _buttonMenuResetAll!.interactable = DrakesReskinIt.CanResetAnyCustomization(item);

        ActionMenuPanel.SetActive(true);
        ActionMenuPanel.transform.SetAsLastSibling();
        GUIManager.BlockInput(true);
    }

    private static void CloseActionMenuOnly()
    {
        ActionMenuPanel?.SetActive(false);
        GUIManager.BlockInput(false);
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
            width: 300,
            height: 240,
            draggable: false);

        GUIManager.Instance.CreateText(
            text: "DrakesReskinIt",
            parent: ActionMenuPanel.transform,
            anchorMin: new Vector2(0.5f, 1f),
            anchorMax: new Vector2(0.5f, 1f),
            position: new Vector2(0f, -44f),
            font: GUIManager.Instance.AveriaSerifBold,
            fontSize: 22,
            color: GUIManager.Instance.ValheimOrange,
            outline: true,
            outlineColor: Color.black,
            width: 260,
            height: 40,
            addContentSizeFitter: false);

        _buttonMenuIcon = GUIManager.Instance.CreateButton(
            text: "Change Icon",
            parent: ActionMenuPanel.transform,
            anchorMin: new Vector2(0.5f, 0.5f),
            anchorMax: new Vector2(0.5f, 0.5f),
            position: new Vector2(0f, 50f),
            width: 200f,
            height: 32f).GetComponent<Button>();
        _buttonMenuIcon.AddUniqueListener(() =>
        {
            var item = DrakesReskinIt.CurrentItem;
            CloseActionMenuOnly();
            if (item != null) DrakesReskinIt.OpenIconPicker(item);
        });

        _buttonMenuTint = GUIManager.Instance.CreateButton(
            text: "Recolor / Tint",
            parent: ActionMenuPanel.transform,
            anchorMin: new Vector2(0.5f, 0.5f),
            anchorMax: new Vector2(0.5f, 0.5f),
            position: new Vector2(0f, 10f),
            width: 200f,
            height: 32f).GetComponent<Button>();
        _buttonMenuTint.AddUniqueListener(() =>
        {
            var item = DrakesReskinIt.CurrentItem;
            CloseActionMenuOnly();
            if (item != null) DrakesReskinIt.OpenTintPicker(item);
        });

        _buttonMenuResetAll = GUIManager.Instance.CreateButton(
            text: "Reset all",
            parent: ActionMenuPanel.transform,
            anchorMin: new Vector2(0.5f, 0.5f),
            anchorMax: new Vector2(0.5f, 0.5f),
            position: new Vector2(0f, -30f),
            width: 120f,
            height: 28f).GetComponent<Button>();
        _buttonMenuResetAll.AddUniqueListener(() =>
        {
            var item = DrakesReskinIt.CurrentItem;
            if (item != null) DrakesReskinIt.ResetAllCustomizations(item);
            CloseActionMenuOnly();
        });

        _buttonMenuCancel = GUIManager.Instance.CreateButton(
            text: "Cancel",
            parent: ActionMenuPanel.transform,
            anchorMin: new Vector2(0.5f, 0.5f),
            anchorMax: new Vector2(0.5f, 0.5f),
            position: new Vector2(0f, -68f),
            width: 120f,
            height: 28f).GetComponent<Button>();
        _buttonMenuCancel.AddUniqueListener(CloseActionMenuOnly);
    }

    // ─── Icon picker ──────────────────────────────────────────────────────────

    public static void CreateIconPicker()
    {
        if (GUIManager.Instance == null || !GUIManager.CustomGUIFront)
            return;

        IconPickerPanel = GUIManager.Instance.CreateWoodpanel(
            parent: GUIManager.CustomGUIFront.transform,
            anchorMin: new Vector2(0.5f, 0.5f),
            anchorMax: new Vector2(0.5f, 0.5f),
            position: new Vector2(0f, 0f),
            width: 400,
            height: 380,
            draggable: false);

        GUIManager.Instance.CreateText(
            text: "Choose Icon",
            parent: IconPickerPanel.transform,
            anchorMin: new Vector2(0.5f, 1f),
            anchorMax: new Vector2(0.5f, 1f),
            position: new Vector2(0f, -44f),
            font: GUIManager.Instance.AveriaSerifBold,
            fontSize: 22,
            color: GUIManager.Instance.ValheimOrange,
            outline: true,
            outlineColor: Color.black,
            width: 360,
            height: 40,
            addContentSizeFitter: false);

        // Scrollable icon grid area — built inline without a ScrollRect helper
        var scrollRoot = new GameObject("IconScrollRoot");
        scrollRoot.transform.SetParent(IconPickerPanel.transform, false);
        var scrollRootRect = scrollRoot.AddComponent<RectTransform>();
        scrollRootRect.anchorMin = new Vector2(0.05f, 0.12f);
        scrollRootRect.anchorMax = new Vector2(0.95f, 0.82f);
        scrollRootRect.offsetMin = Vector2.zero;
        scrollRootRect.offsetMax = Vector2.zero;
        scrollRoot.AddComponent<Image>().color = new Color(0, 0, 0, 0.2f);

        var scrollRect = scrollRoot.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;

        var viewport = new GameObject("Viewport");
        viewport.transform.SetParent(scrollRoot.transform, false);
        var vpRect = viewport.AddComponent<RectTransform>();
        vpRect.anchorMin = Vector2.zero;
        vpRect.anchorMax = Vector2.one;
        vpRect.offsetMin = Vector2.zero;
        vpRect.offsetMax = Vector2.zero;
        viewport.AddComponent<Image>().color = new Color(0, 0, 0, 0);
        viewport.AddComponent<Mask>().showMaskGraphic = false;
        scrollRect.viewport = vpRect;

        var content = new GameObject("Content");
        content.transform.SetParent(viewport.transform, false);
        _iconScrollContent = content;
        var contentRect = content.AddComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.offsetMin = Vector2.zero;
        contentRect.offsetMax = Vector2.zero;
        var layout = content.AddComponent<GridLayoutGroup>();
        layout.cellSize = new Vector2(64, 64);
        layout.spacing = new Vector2(4, 4);
        layout.padding = new RectOffset(4, 4, 4, 4);
        layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        layout.constraintCount = 5;
        content.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scrollRect.content = contentRect;

        // Buttons at the bottom
        _buttonIconClear = GUIManager.Instance.CreateButton(
            text: "Clear Icon",
            parent: IconPickerPanel.transform,
            anchorMin: new Vector2(0.5f, 0f),
            anchorMax: new Vector2(0.5f, 0f),
            position: new Vector2(-55f, 28f),
            width: 100f,
            height: 28f).GetComponent<Button>();
        _buttonIconClear.AddUniqueListener(() =>
        {
            if (DrakesReskinIt.CurrentItem != null)
                DrakesReskinIt.ClearCustomIcon(DrakesReskinIt.CurrentItem);
            IconPickerPanel?.SetActive(false);
            DrakesReskinIt.CurrentItem = null;
            GUIManager.BlockInput(false);
        });

        _buttonIconCancel = GUIManager.Instance.CreateButton(
            text: "Cancel",
            parent: IconPickerPanel.transform,
            anchorMin: new Vector2(0.5f, 0f),
            anchorMax: new Vector2(0.5f, 0f),
            position: new Vector2(55f, 28f),
            width: 100f,
            height: 28f).GetComponent<Button>();
        _buttonIconCancel.AddUniqueListener(() =>
        {
            IconPickerPanel?.SetActive(false);
            DrakesReskinIt.CurrentItem = null;
            GUIManager.BlockInput(false);
        });
    }

    /// <summary>Rebuild the scroll-view content with one button per registered icon.</summary>
    public static void RefreshIconPicker(ItemDrop.ItemData item)
    {
        if (_iconScrollContent == null) return;

        // Clear old buttons
        foreach (Transform child in _iconScrollContent.transform)
            Object.Destroy(child.gameObject);

        foreach (var kvp in IconRegistry.GetAll())
        {
            string iconName = kvp.Key;
            Sprite sprite = kvp.Value;

            var btnGo = new GameObject("IconBtn_" + iconName);
            btnGo.transform.SetParent(_iconScrollContent.transform, false);
            var img = btnGo.AddComponent<Image>();
            img.sprite = sprite;
            img.preserveAspect = true;
            var btn = btnGo.AddComponent<Button>();

            // Capture for lambda
            string capturedName = iconName;
            btn.onClick.AddListener(() =>
            {
                DrakesReskinIt.ApplyIcon(capturedName);
            });
        }
    }

    // ─── Tint picker ──────────────────────────────────────────────────────────

    public static void CreateTintPicker()
    {
        if (GUIManager.Instance == null || !GUIManager.CustomGUIFront)
            return;

        TintPickerPanel = GUIManager.Instance.CreateWoodpanel(
            parent: GUIManager.CustomGUIFront.transform,
            anchorMin: new Vector2(0.5f, 0.5f),
            anchorMax: new Vector2(0.5f, 0.5f),
            position: new Vector2(0f, 0f),
            width: 380,
            height: 340,
            draggable: false);

        GUIManager.Instance.CreateText(
            text: "Recolor Icon",
            parent: TintPickerPanel.transform,
            anchorMin: new Vector2(0.5f, 1f),
            anchorMax: new Vector2(0.5f, 1f),
            position: new Vector2(0f, -44f),
            font: GUIManager.Instance.AveriaSerifBold,
            fontSize: 22,
            color: GUIManager.Instance.ValheimOrange,
            outline: true,
            outlineColor: Color.black,
            width: 340,
            height: 40,
            addContentSizeFitter: false);

        // Preset color buttons (3 per row)
        float startY = 90f;
        float rowH = 32f;
        float colW = 110f;
        for (int i = 0; i < TintPresets.Length; i++)
        {
            var (label, hex) = TintPresets[i];
            int col = i % 3;
            int row = i / 3;
            float x = (col - 1) * colW;
            float y = startY - row * rowH;

            string capturedHex = hex;
            var btn = GUIManager.Instance.CreateButton(
                text: label,
                parent: TintPickerPanel.transform,
                anchorMin: new Vector2(0.5f, 0.5f),
                anchorMax: new Vector2(0.5f, 0.5f),
                position: new Vector2(x, y),
                width: 100f,
                height: 26f).GetComponent<Button>();

            // Apply color tint to the button background so players can see the color
            if (ColorUtility.TryParseHtmlString(capturedHex, out Color c))
            {
                var colors = btn.colors;
                colors.normalColor = c;
                colors.highlightedColor = c * 1.2f;
                btn.colors = colors;
            }

            btn.AddUniqueListener(() => DrakesReskinIt.ApplyTint(capturedHex));
        }

        // Hex input field
        GUIManager.Instance.CreateText(
            text: "or enter hex:",
            parent: TintPickerPanel.transform,
            anchorMin: new Vector2(0.5f, 0f),
            anchorMax: new Vector2(0.5f, 0f),
            position: new Vector2(-90f, 95f),
            font: GUIManager.Instance.AveriaSerifBold,
            fontSize: 14,
            color: Color.white,
            outline: false,
            outlineColor: Color.black,
            width: 90,
            height: 24,
            addContentSizeFitter: false);

        _tintHexInput = GUIManager.Instance.CreateInputField(
            parent: TintPickerPanel.transform,
            anchorMin: new Vector2(0.5f, 0f),
            anchorMax: new Vector2(0.5f, 0f),
            position: new Vector2(30f, 95f),
            contentType: InputField.ContentType.Standard,
            placeholderText: "#rrggbb",
            fontSize: 16,
            width: 120,
            height: 28f).GetComponent<InputField>();

        _buttonTintOk = GUIManager.Instance.CreateButton(
            text: "OK",
            parent: TintPickerPanel.transform,
            anchorMin: new Vector2(0.5f, 0f),
            anchorMax: new Vector2(0.5f, 0f),
            position: new Vector2(-55f, 62f),
            width: 80f,
            height: 28f).GetComponent<Button>();
        _buttonTintOk.AddUniqueListener(() =>
        {
            string hex = _tintHexInput?.text?.Trim() ?? "";
            if (!string.IsNullOrEmpty(hex))
                DrakesReskinIt.ApplyTint(hex);
        });

        _buttonTintReset = GUIManager.Instance.CreateButton(
            text: "Reset",
            parent: TintPickerPanel.transform,
            anchorMin: new Vector2(0.5f, 0f),
            anchorMax: new Vector2(0.5f, 0f),
            position: new Vector2(0f, 62f),
            width: 80f,
            height: 28f).GetComponent<Button>();
        _buttonTintReset.AddUniqueListener(() =>
        {
            if (DrakesReskinIt.CurrentItem != null)
                DrakesReskinIt.ClearIconTint(DrakesReskinIt.CurrentItem);
            TintPickerPanel?.SetActive(false);
            DrakesReskinIt.CurrentItem = null;
            GUIManager.BlockInput(false);
        });

        _buttonTintCancel = GUIManager.Instance.CreateButton(
            text: "Cancel",
            parent: TintPickerPanel.transform,
            anchorMin: new Vector2(0.5f, 0f),
            anchorMax: new Vector2(0.5f, 0f),
            position: new Vector2(55f, 62f),
            width: 80f,
            height: 28f).GetComponent<Button>();
        _buttonTintCancel.AddUniqueListener(() =>
        {
            TintPickerPanel?.SetActive(false);
            DrakesReskinIt.CurrentItem = null;
            GUIManager.BlockInput(false);
        });
    }

    public static void RefreshTintPicker(ItemDrop.ItemData item)
    {
        if (_tintHexInput == null) return;
        string? current = DrakesReskinIt.GetIconTint(item);
        _tintHexInput.text = current ?? "";
    }
}
