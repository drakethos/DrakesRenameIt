using DrakeModsLibs.UI;
using Jotunn.Managers;
using UnityEngine;
using UnityEngine.UI;
using Text = UnityEngine.UI.Text;

namespace DrakeRenameit.UI;

/// <summary>
/// DrakeTabHost "Paper" page: Make public, font size, landscape — writes the pinned
/// note's ZDO (or inventory item custom data when opened from bag).
/// </summary>
internal static class PaperTabPanel
{
    public const string TabId = "renameit.paper";

    const float PanelWidth = 320f;
    const float PanelHeight = 330f;
    const float ContentWidth = 260f;

    static GameObject? _panel;
    static Text? _titleText;
    static Text? _subtitleText;
    static Text? _fontPreview;
    static DrakeNumericStepper? _fontStepper;
    static Toggle? _publicToggle;
    static Toggle? _landscapeToggle;
    static GameObject? _publicRow;
    static float _fontSize = PaperFontScale.LevelToSize(3);
    static bool _landscape;
    static bool _suppressToggle;

    internal static void Show(DrakeTabPageContext ctx)
    {
        if (GUIManager.Instance == null || !GUIManager.CustomGUIFront)
            return;

        EnsurePanel();
        if (_panel == null)
            return;

        DrakeRenameit.CurrentItem = ctx.Item;
        LoadFromContext(ctx.Item);
        RefreshUi();
        _panel.SetActive(true);
        _panel.transform.SetAsLastSibling();
        DrakeGuiInput.EnsureBlocked();
    }

    /// <summary>Tab host switching away — hide chrome without ending the host session.</summary>
    internal static void Hide()
    {
        if (_panel != null)
            _panel.SetActive(false);
        DrakeGuiInput.EnsureUnblocked();
    }

    /// <summary>User dismissed the Paper panel — end tab-host / wall session.</summary>
    internal static void CloseAndEndHost()
    {
        Hide();
        DrakeTabHost.Close();
        DrakeGuiInput.EnsureUnblocked();
    }

    static void LoadFromContext(ItemDrop.ItemData? item)
    {
        var vessel = PaperWallSession.Vessel;
        if (vessel != null)
        {
            _fontSize = PaperFontScale.NormalizeStored(vessel.ReadFontSize());
            _landscape = vessel.ReadLandscape();
            // Keep session item customData aligned with the piece (take/place persistence).
            PaperItemStyle.WriteAll(
                item,
                _fontSize,
                _landscape,
                vessel.ReadTakePublicPublic());
            return;
        }

        PaperItemStyle.Read(item, out _fontSize, out _landscape, out _);
        _fontSize = PaperFontScale.NormalizeStored(_fontSize);
    }

    static void RefreshUi()
    {
        var vessel = PaperWallSession.Vessel;
        var item = DrakeRenameit.CurrentItem;

        if (_titleText != null)
            _titleText.text = "Paper";
        if (_subtitleText != null)
        {
            var name = item != null ? DrakeRenameit.GetPropperName(item) : "Written Page";
            _subtitleText.text = name;
        }

        var level = PaperFontScale.StoredToLevel(_fontSize);
        _fontStepper?.SetValueWithoutNotify(level);
        RefreshFontPreview(level);

        _suppressToggle = true;
        try
        {
            if (_publicRow != null)
            {
                var showPublic = RenameitConfig.PaperTakePublicEnabled &&
                    vessel != null &&
                    vessel.CanOfferTakePublicTogglePublic();
                _publicRow.SetActive(showPublic);
                if (_publicToggle != null && showPublic)
                {
                    _publicToggle.interactable = vessel!.LocalMayToggleTakePublicPublic();
                    _publicToggle.SetIsOnWithoutNotify(vessel.ReadTakePublicPublic());
                }
            }

            if (_landscapeToggle != null)
                _landscapeToggle.SetIsOnWithoutNotify(_landscape);
        }
        finally
        {
            _suppressToggle = false;
        }
    }

    static void RefreshFontPreview(int level)
    {
        if (_fontPreview == null)
            return;
        _fontPreview.text = "FONT";
        _fontPreview.fontSize = LevelToPreviewPx(level);
    }

    /// <summary>HUD preview px — proportional stand-in for parchment levels (not world TMP units).</summary>
    static int LevelToPreviewPx(int level)
    {
        var t = (Mathf.Clamp(level, PaperFontScale.LevelMin, PaperFontScale.LevelMax) - PaperFontScale.LevelMin)
                / (float)(PaperFontScale.LevelMax - PaperFontScale.LevelMin);
        return Mathf.RoundToInt(Mathf.Lerp(12f, 30f, t));
    }

    static void EnsurePanel()
    {
        if (_panel != null || GUIManager.Instance == null || !GUIManager.CustomGUIFront)
            return;

        _panel = GUIManager.Instance.CreateWoodpanel(
            parent: GUIManager.CustomGUIFront.transform,
            anchorMin: new Vector2(0.5f, 0.5f),
            anchorMax: new Vector2(0.5f, 0.5f),
            position: Vector2.zero,
            width: PanelWidth,
            height: PanelHeight,
            draggable: false);
        _panel.name = "drakes_paper_tab_panel";

        _titleText = GUIManager.Instance.CreateText(
            text: "Paper",
            parent: _panel.transform,
            anchorMin: new Vector2(0.5f, 1f),
            anchorMax: new Vector2(0.5f, 1f),
            position: new Vector2(0f, -26f),
            font: GUIManager.Instance.AveriaSerifBold,
            fontSize: 22,
            color: GUIManager.Instance.ValheimOrange,
            outline: true,
            outlineColor: Color.black,
            width: ContentWidth,
            height: 28f,
            addContentSizeFitter: false).GetComponent<Text>();
        if (_titleText != null)
            _titleText.alignment = TextAnchor.MiddleCenter;

        _subtitleText = GUIManager.Instance.CreateText(
            text: "",
            parent: _panel.transform,
            anchorMin: new Vector2(0.5f, 1f),
            anchorMax: new Vector2(0.5f, 1f),
            position: new Vector2(0f, -48f),
            font: GUIManager.Instance.AveriaSerifBold,
            fontSize: 14,
            color: Color.white,
            outline: true,
            outlineColor: Color.black,
            width: ContentWidth,
            height: 20f,
            addContentSizeFitter: false).GetComponent<Text>();
        if (_subtitleText != null)
            _subtitleText.alignment = TextAnchor.MiddleCenter;

        _publicRow = DrakeToggleLayout.CreateRow(_panel.transform, "public_take", new Vector2(0f, 78f), ContentWidth);
        var pubToggleGo = GUIManager.Instance.CreateToggle(_publicRow.transform, 24f, 24f);
        _publicToggle = pubToggleGo.GetComponent<Toggle>();
        if (_publicToggle != null)
        {
            _publicToggle.navigation = new Navigation { mode = Navigation.Mode.None };
            _publicToggle.onValueChanged.AddListener(OnPublicChanged);
        }
        DrakeToggleLayout.ApplyLabelLeftBoxRight(pubToggleGo, "Make public", ContentWidth);

        var landRow = DrakeToggleLayout.CreateRow(_panel.transform, "landscape", new Vector2(0f, 44f), ContentWidth);
        var landToggleGo = GUIManager.Instance.CreateToggle(landRow.transform, 24f, 24f);
        _landscapeToggle = landToggleGo.GetComponent<Toggle>();
        if (_landscapeToggle != null)
        {
            _landscapeToggle.navigation = new Navigation { mode = Navigation.Mode.None };
            _landscapeToggle.onValueChanged.AddListener(on =>
            {
                if (_suppressToggle)
                    return;
                _landscape = on;
                ApplyStyle();
            });
        }
        DrakeToggleLayout.ApplyLabelLeftBoxRight(landToggleGo, "Landscape text", ContentWidth);

        var fontLabel = GUIManager.Instance.CreateText(
            text: "Font size",
            parent: _panel.transform,
            anchorMin: new Vector2(0.5f, 0.5f),
            anchorMax: new Vector2(0.5f, 0.5f),
            position: new Vector2(0f, 8f),
            font: GUIManager.Instance.AveriaSerifBold,
            fontSize: 16,
            color: Color.white,
            outline: true,
            outlineColor: Color.black,
            width: ContentWidth,
            height: 22f,
            addContentSizeFitter: false).GetComponent<Text>();
        if (fontLabel != null)
            fontLabel.alignment = TextAnchor.MiddleCenter;

        // Live size preview — HUD px stand-in for parchment level (word FONT).
        _fontPreview = GUIManager.Instance.CreateText(
            text: "FONT",
            parent: _panel.transform,
            anchorMin: new Vector2(0.5f, 0.5f),
            anchorMax: new Vector2(0.5f, 0.5f),
            position: new Vector2(0f, -22f),
            font: GUIManager.Instance.AveriaSerifBold,
            fontSize: 18,
            color: new Color(0.92f, 0.86f, 0.72f, 1f),
            outline: true,
            outlineColor: Color.black,
            width: ContentWidth,
            height: 36f,
            addContentSizeFitter: false).GetComponent<Text>();
        if (_fontPreview != null)
        {
            _fontPreview.alignment = TextAnchor.MiddleCenter;
            _fontPreview.horizontalOverflow = HorizontalWrapMode.Overflow;
            _fontPreview.verticalOverflow = VerticalWrapMode.Overflow;
        }

        var hint = GUIManager.Instance.CreateText(
            text: "1 small  ·  7 large",
            parent: _panel.transform,
            anchorMin: new Vector2(0.5f, 0.5f),
            anchorMax: new Vector2(0.5f, 0.5f),
            position: new Vector2(0f, -48f),
            font: GUIManager.Instance.AveriaSerifBold,
            fontSize: 13,
            color: new Color(0.85f, 0.85f, 0.85f, 1f),
            outline: true,
            outlineColor: Color.black,
            width: ContentWidth,
            height: 18f,
            addContentSizeFitter: false).GetComponent<Text>();
        if (hint != null)
            hint.alignment = TextAnchor.MiddleCenter;

        _fontStepper = DrakeNumericStepper.Create(
            parent: _panel.transform,
            anchoredPosition: new Vector2(0f, -78f),
            min: PaperFontScale.LevelMin,
            max: PaperFontScale.LevelMax,
            initial: PaperFontScale.StoredToLevel(_fontSize),
            onChanged: level =>
            {
                _fontSize = PaperFontScale.LevelToSize(level);
                RefreshFontPreview(level);
                ApplyStyle();
            });

        var closeGo = DrakeButtonSfx.SoftenButton(GUIManager.Instance.CreateButton(
            text: "Close",
            parent: _panel.transform,
            anchorMin: new Vector2(0.5f, 0f),
            anchorMax: new Vector2(0.5f, 0f),
            position: new Vector2(0f, 34f),
            width: 100f,
            height: 28f));
        if (closeGo != null)
            closeGo.onClick.AddListener(CloseAndEndHost);

        DrakeButtonSfx.Soften(_panel);
    }

    static void OnPublicChanged(bool on)
    {
        if (_suppressToggle)
            return;
        var vessel = PaperWallSession.Vessel;
        if (vessel == null)
            return;
        vessel.RequestSetTakePublic(on);
        PaperItemStyle.WriteTakePublic(DrakeRenameit.CurrentItem, on);
        RefreshUi();
    }

    static void ApplyStyle()
    {
        // Always stamp the item — take/place persistence lives on customData.
        PaperItemStyle.WriteStyle(DrakeRenameit.CurrentItem, _fontSize, _landscape);

        var vessel = PaperWallSession.Vessel;
        if (vessel != null)
            vessel.ApplyPaperStyle(_fontSize, _landscape);
    }
}
