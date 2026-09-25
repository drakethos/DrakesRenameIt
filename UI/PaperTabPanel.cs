using DrakeModsLibs.UI;
using DrakeRenameit.API;
using DrakeRenameit.ModText;
using DrakeRenameit.Paper.Functionality;
using DrakeRenameit.Paper.Items;
using DrakeRenameit.Paper.Pieces;
using Jotunn.Managers;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Text = UnityEngine.UI.Text;
using Image = UnityEngine.UI.Image;
using static DrakeRenameit.ModText.RenameItLocalization;

namespace DrakeRenameit.UI;

/// <summary>
/// DrakeTabHost "Paper" page: Make public, landscape, font, Immutable stack, Make Copy, Recycle.
/// Grows downward from the original 330 sheet so Rename|Paper tabs do not move.
/// </summary>
internal static class PaperTabPanel
{
    public const string TabId = "renameit.paper";

    const float PanelWidth = 320f;
    /// <summary>Old 330 sheet top (tabs sit at 178). Height shrinks to content so Close sits tight under actions.</summary>
    const float TabSafeTopY = 165f;
    const float ContentWidth = 280f;
    const float PanelBottomPad = 18f;

    const float StepBtn = 40f;
    const float StepField = 64f;
    const float StepGap = 12f;
    const float StepH = 32f;
    const float CopyBtnW = 140f;
    const float RecycleBtnW = 44f;
    const float CloseBtnW = 100f;

    /// <summary>Original rhythm — do not crush row spacing; only trim dead space under Close.</summary>
    const float TopPublic = 87f;
    const float GapToggle = 34f;
    const float GapFontLabel = 36f;
    const float GapFontStepper = 32f;
    const float GapImmutable = 40f;
    const float GapToCopyBlock = 44f;
    const float GapCost = 28f;
    const float GapCopyBtn = 36f;
    const float GapRecycleBtn = 36f;
    const float GapToClose = 36f;

    static readonly Color LabelLive = Color.white;
    static readonly Color LabelGray = new Color(0.55f, 0.55f, 0.55f, 1f);
    static readonly Color RecycleIconGreen = new Color(0.45f, 0.82f, 0.40f, 1f);

    static float _panelHeight = 460f;
    static float PanelPosY => TabSafeTopY - _panelHeight * 0.5f;

    static GameObject? _panel;
    static Text? _titleText;
    static Text? _subtitleText;
    static Text? _copyCostText;
    static Text? _fontLabelText;
    static GameObject? _fontLabelGo;
    static GameObject? _landscapeRow;
    static GameObject? _immutableRow;
    static DrakeNumericStepper? _fontStepper;
    static DrakeNumericStepper? _copyStepper;
    static Button? _copyButton;
    static Button? _recycleButton;
    static Button? _closeButton;
    static Toggle? _publicToggle;
    static Toggle? _landscapeToggle;
    static Toggle? _immutableToggle;
    static GameObject? _publicRow;
    static readonly DrakeConfirmPanel RecycleConfirm = new DrakeConfirmPanel("renameit_paper_recycle_confirm");
    static float _fontSize = PaperFontScale.LevelToSize(3);
    static bool _landscape;
    static bool _takePublic;
    static bool _immutableStack;
    static bool _suppressToggle;
    static int _copies = 1;

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
        RecycleConfirm.Close();
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
            _takePublic = vessel.ReadTakePublicPublic();
            PaperItemStyle.WriteAll(
                item,
                _fontSize,
                _landscape,
                _takePublic);
        }
        else
        {
            PaperItemStyle.Read(item, out _fontSize, out _landscape, out _takePublic);
            _fontSize = PaperFontScale.NormalizeStored(_fontSize);
        }

        // Printed is always immutable print mode; Written defaults to writable copies.
        _immutableStack = PaperCopyMark.IsCopy(item) || PaperItem.IsPrintedPaper(item);
        if (_immutableStack)
            _copies = Mathf.Clamp(_copies, PaperCopyService.PrintMin, CopyMax());
        else
            _copies = Mathf.Clamp(_copies, PaperCopyService.CopiesMin, PaperCopyService.CopiesMax);
    }

    static bool TakePublicFeatureOn()
    {
        try
        {
            return RenameitConfig.PaperTakePublicEnabled;
        }
        catch
        {
            return true;
        }
    }

    static bool IsCopyContext(ItemDrop.ItemData? item) =>
        PaperCopyMark.IsCopy(item) || PaperItem.IsPrintedPaper(item);

    static bool StyleLocked(ItemDrop.ItemData? item) =>
        IsCopyContext(item) && !RenameitPermission.IsElevatedForOverrides(Player.m_localPlayer);

    static bool PrintModeActive(ItemDrop.ItemData? item) =>
        IsCopyContext(item) || _immutableStack;

    static int CopyMax() =>
        Mathf.Min(PaperCopyService.PrintMax, RenameitConfig.PrintedPaperStackSize);

    static void RefreshUi()
    {
        var vessel = PaperWallSession.Vessel;
        var item = DrakeRenameit.CurrentItem;
        var copyCtx = IsCopyContext(item);
        var styleLocked = StyleLocked(item);
        var printMode = PrintModeActive(item);

        if (_titleText != null)
            _titleText.text = "Paper";
        if (_subtitleText != null)
        {
            var name = item != null ? DrakeRenameit.GetPropperName(item) : "Written Page";
            _subtitleText.text = name;
        }

        var level = PaperFontScale.StoredToLevel(_fontSize);
        _fontStepper?.SetValueWithoutNotify(level);

        var copyMin = printMode ? PaperCopyService.PrintMin : PaperCopyService.CopiesMin;
        var copyMax = printMode ? CopyMax() : PaperCopyService.CopiesMax;
        _copyStepper?.SetRange(copyMin, copyMax);
        _copies = Mathf.Clamp(_copies, copyMin, copyMax);
        _copyStepper?.SetValueWithoutNotify(_copies);

        _suppressToggle = true;
        try
        {
            if (_publicRow != null)
            {
                var showPublic = TakePublicFeatureOn();
                var mayToggle = vessel == null || vessel.LocalMayToggleTakePublicPublic();
                if (vessel != null)
                    _takePublic = vessel.ReadTakePublicPublic();

                _publicRow.SetActive(showPublic);
                if (_publicToggle != null && showPublic)
                {
                    _publicToggle.interactable = mayToggle;
                    _publicToggle.SetIsOnWithoutNotify(_takePublic);
                }
            }

            if (_landscapeToggle != null)
            {
                _landscapeToggle.interactable = !styleLocked;
                _landscapeToggle.SetIsOnWithoutNotify(_landscape);
            }

            if (_fontLabelText != null)
                _fontLabelText.color = styleLocked ? LabelGray : LabelLive;
            _fontStepper?.SetInteractable(!styleLocked);

            // Immutable checkbox: templates only. Copies are always print mode.
            if (_immutableRow != null)
            {
                _immutableRow.SetActive(!copyCtx);
                if (_immutableToggle != null && !copyCtx)
                {
                    _immutableToggle.interactable = true;
                    _immutableToggle.SetIsOnWithoutNotify(_immutableStack);
                }
            }
        }
        finally
        {
            _suppressToggle = false;
        }

        RefreshCopyCostUi();
        Relayout();
    }

    static void RefreshCopyCostUi()
    {
        var player = Player.m_localPlayer;
        var item = DrakeRenameit.CurrentItem;
        var canAfford = PaperCopyService.CanAfford(player, _copies);
        var canCopy = item != null && PaperItem.IsWrittenLike(item) && canAfford;

        if (_copyCostText != null)
        {
            _copyCostText.supportRichText = true;
            _copyCostText.text = PaperCopyService.FormatCostLine(player, _copies);
        }

        if (_copyButton != null)
            _copyButton.interactable = canCopy;

        var showRecycle = PaperRecycleService.CanRecycle(item, player);
        if (_recycleButton != null)
        {
            _recycleButton.gameObject.SetActive(showRecycle);
            _recycleButton.interactable = showRecycle;
        }
    }

    static void Relayout()
    {
        var y = TopPublic;
        PinFromTop(_publicRow, y);
        if (_publicRow == null || _publicRow.activeSelf)
            y += GapToggle;

        PinFromTop(_landscapeRow, y);
        y += GapFontLabel;
        PinFromTop(_fontLabelGo, y);
        y += GapFontStepper;
        PinFromTop(_fontStepper != null ? _fontStepper.Root : null, y);
        y += GapImmutable;
        PinFromTop(_immutableRow, y);
        if (_immutableRow == null || _immutableRow.activeSelf)
            y += GapToCopyBlock;
        else
            y += GapToCopyBlock - GapToggle * 0.35f;

        PinFromTop(_copyStepper != null ? _copyStepper.Root : null, y);
        y += GapCost;
        PinFromTop(_copyCostText != null ? _copyCostText.gameObject : null, y);
        y += GapCopyBtn;
        PinFromTop(_copyButton != null ? _copyButton.gameObject : null, y);
        if (_recycleButton != null && _recycleButton.gameObject.activeSelf)
        {
            y += GapRecycleBtn;
            PinFromTop(_recycleButton.gameObject, y);
        }

        y += GapToClose;
        PinFromTop(_closeButton != null ? _closeButton.gameObject : null, y);
        FitPanelHeight(y + StepH * 0.5f + PanelBottomPad);
    }

    /// <summary>Keep panel top fixed; shrink bottom so Close is not floating in empty parchment.</summary>
    static void FitPanelHeight(float contentBottomFromTop)
    {
        if (_panel == null)
            return;
        _panelHeight = Mathf.Max(360f, contentBottomFromTop);
        var rt = _panel.GetComponent<RectTransform>();
        if (rt == null)
            return;
        rt.sizeDelta = new Vector2(PanelWidth, _panelHeight);
        rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, PanelPosY);
    }

    /// <summary>Keep X; pin to the panel top so extra height only grows downward.</summary>
    static void PinFromTop(GameObject? go, float yFromTop)
    {
        if (go == null)
            return;
        var rt = go.GetComponent<RectTransform>();
        if (rt == null)
            return;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, -yFromTop);
    }

    static void EnsurePanel()
    {
        if (_panel != null || GUIManager.Instance == null || !GUIManager.CustomGUIFront)
            return;

        _panel = GUIManager.Instance.CreateWoodpanel(
            parent: GUIManager.CustomGUIFront.transform,
            anchorMin: new Vector2(0.5f, 0.5f),
            anchorMax: new Vector2(0.5f, 0.5f),
            position: new Vector2(0f, PanelPosY),
            width: PanelWidth,
            height: _panelHeight,
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

        _publicRow = DrakeToggleLayout.CreateRow(_panel.transform, "public_take", Vector2.zero, ContentWidth);
        var pubToggleGo = GUIManager.Instance.CreateToggle(_publicRow.transform, 24f, 24f);
        _publicToggle = pubToggleGo.GetComponent<Toggle>();
        if (_publicToggle != null)
        {
            _publicToggle.navigation = new Navigation { mode = Navigation.Mode.None };
            _publicToggle.onValueChanged.AddListener(OnPublicChanged);
        }
        DrakeToggleLayout.ApplyLabelLeftBoxRight(pubToggleGo, "Make public", ContentWidth);

        _landscapeRow = DrakeToggleLayout.CreateRow(_panel.transform, "landscape", Vector2.zero, ContentWidth);
        var landToggleGo = GUIManager.Instance.CreateToggle(_landscapeRow.transform, 24f, 24f);
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

        _fontLabelGo = GUIManager.Instance.CreateText(
            text: "Font size",
            parent: _panel.transform,
            anchorMin: new Vector2(0.5f, 1f),
            anchorMax: new Vector2(0.5f, 1f),
            position: Vector2.zero,
            font: GUIManager.Instance.AveriaSerifBold,
            fontSize: 16,
            color: Color.white,
            outline: true,
            outlineColor: Color.black,
            width: ContentWidth,
            height: 22f,
            addContentSizeFitter: false);
        _fontLabelText = _fontLabelGo != null ? _fontLabelGo.GetComponent<Text>() : null;
        if (_fontLabelText != null)
            _fontLabelText.alignment = TextAnchor.MiddleCenter;

        _fontStepper = DrakeNumericStepper.Create(
            parent: _panel.transform,
            anchoredPosition: Vector2.zero,
            min: PaperFontScale.LevelMin,
            max: PaperFontScale.LevelMax,
            initial: PaperFontScale.StoredToLevel(_fontSize),
            onChanged: level =>
            {
                _fontSize = PaperFontScale.LevelToSize(level);
                ApplyStyle();
            },
            buttonSize: StepBtn,
            fieldWidth: StepField,
            height: StepH,
            gap: StepGap);

        _immutableRow = DrakeToggleLayout.CreateRow(_panel.transform, "immutable_stack", Vector2.zero, ContentWidth);
        var immToggleGo = GUIManager.Instance.CreateToggle(_immutableRow.transform, 24f, 24f);
        _immutableToggle = immToggleGo.GetComponent<Toggle>();
        if (_immutableToggle != null)
        {
            _immutableToggle.navigation = new Navigation { mode = Navigation.Mode.None };
            _immutableToggle.onValueChanged.AddListener(OnImmutableChanged);
        }
        DrakeToggleLayout.ApplyLabelLeftBoxRight(immToggleGo, "Immutable stack", ContentWidth);

        _copyStepper = DrakeNumericStepper.Create(
            parent: _panel.transform,
            anchoredPosition: Vector2.zero,
            min: PaperCopyService.CopiesMin,
            max: PaperCopyService.CopiesMax,
            initial: _copies,
            onChanged: n =>
            {
                _copies = n;
                RefreshCopyCostUi();
            },
            buttonSize: StepBtn,
            fieldWidth: StepField,
            height: StepH,
            gap: StepGap);

        _copyCostText = GUIManager.Instance.CreateText(
            text: "",
            parent: _panel.transform,
            anchorMin: new Vector2(0.5f, 1f),
            anchorMax: new Vector2(0.5f, 1f),
            position: Vector2.zero,
            font: GUIManager.Instance.AveriaSerifBold,
            fontSize: 13,
            color: Color.white,
            outline: true,
            outlineColor: Color.black,
            width: ContentWidth,
            height: 20f,
            addContentSizeFitter: false).GetComponent<Text>();
        if (_copyCostText != null)
        {
            _copyCostText.alignment = TextAnchor.MiddleCenter;
            _copyCostText.supportRichText = true;
            _copyCostText.horizontalOverflow = HorizontalWrapMode.Overflow;
            _copyCostText.verticalOverflow = VerticalWrapMode.Truncate;
        }

        _copyButton = DrakeButtonSfx.SoftenButton(GUIManager.Instance.CreateButton(
            text: T(LKeys.CopyPageBtn),
            parent: _panel.transform,
            anchorMin: new Vector2(0.5f, 1f),
            anchorMax: new Vector2(0.5f, 1f),
            position: Vector2.zero,
            width: CopyBtnW,
            height: StepH));
        if (_copyButton != null)
        {
            _copyButton.onClick.AddListener(OnCopyClicked);
            AttachButtonHoverTip(_copyButton, T(LKeys.CopyPageBtnTip));
        }

        _recycleButton = DrakeButtonSfx.SoftenButton(GUIManager.Instance.CreateButton(
            text: T(LKeys.RecycleBtn),
            parent: _panel.transform,
            anchorMin: new Vector2(0.5f, 1f),
            anchorMax: new Vector2(0.5f, 1f),
            position: Vector2.zero,
            width: RecycleBtnW,
            height: StepH));
        if (_recycleButton != null)
        {
            _recycleButton.onClick.AddListener(OnRecycleClicked);
            _recycleButton.gameObject.SetActive(false);
            ApplyRecycleIconStyle(_recycleButton);
            AttachButtonHoverTip(_recycleButton, T(LKeys.RecycleBtnTip));
        }

        _closeButton = DrakeButtonSfx.SoftenButton(GUIManager.Instance.CreateButton(
            text: "Close",
            parent: _panel.transform,
            anchorMin: new Vector2(0.5f, 1f),
            anchorMax: new Vector2(0.5f, 1f),
            position: Vector2.zero,
            width: CloseBtnW,
            height: 28f));
        if (_closeButton != null)
            _closeButton.onClick.AddListener(CloseAndEndHost);

        DrakeButtonSfx.Soften(_panel);
        Relayout();
    }

    static void ApplyRecycleIconStyle(Button button)
    {
        var label = button.GetComponentInChildren<Text>(true);
        if (label == null)
            return;
        label.text = T(LKeys.RecycleBtn);
        label.color = RecycleIconGreen;
        label.fontSize = 22;
        label.alignment = TextAnchor.MiddleCenter;
    }

    static void OnImmutableChanged(bool on)
    {
        if (_suppressToggle)
            return;
        _immutableStack = on;
        var item = DrakeRenameit.CurrentItem;
        var printMode = PrintModeActive(item);
        var copyMin = printMode ? PaperCopyService.PrintMin : PaperCopyService.CopiesMin;
        var copyMax = printMode ? CopyMax() : PaperCopyService.CopiesMax;
        _copyStepper?.SetRange(copyMin, copyMax, notifyIfClamped: true);
        _copies = Mathf.Clamp(_copies, copyMin, copyMax);
        RefreshCopyCostUi();
        Relayout();
    }

    static void OnCopyClicked()
    {
        var player = Player.m_localPlayer;
        var item = DrakeRenameit.CurrentItem;
        if (!StyleLocked(item))
            ApplyStyle();

        bool ok;
        string err;
        if (PrintModeActive(item))
            ok = PaperCopyService.TryPrintStack(item, _copies, player, out err);
        else
            ok = PaperCopyService.TryCopyPages(item, _copies, player, out err);

        if (!ok && !string.IsNullOrEmpty(err) && player != null)
            ValheimHudMessage.Show(player, MessageHud.MessageType.Center, err);

        RefreshCopyCostUi();
    }

    static void OnRecycleClicked()
    {
        var item = DrakeRenameit.CurrentItem;
        var player = Player.m_localPlayer;
        if (!PaperRecycleService.CanRecycle(item, player))
            return;

        RecycleConfirm.Show(
            T(LKeys.RecycleTitle),
            T(LKeys.RecycleBody),
            onYes: () =>
            {
                var current = DrakeRenameit.CurrentItem;
                if (!PaperRecycleService.TryRecycleToBlank(current, Player.m_localPlayer, out var err))
                {
                    if (!string.IsNullOrEmpty(err) && Player.m_localPlayer != null)
                        ValheimHudMessage.Show(Player.m_localPlayer, MessageHud.MessageType.Center, err);
                    return;
                }

                DrakeRenameit.CurrentItem = null;
                CloseAndEndHost();
            },
            onNo: null,
            yesLabel: T(LKeys.BtnYes),
            noLabel: T(LKeys.BtnNo));
    }

    static void OnPublicChanged(bool on)
    {
        if (_suppressToggle)
            return;
        _takePublic = on;
        PaperItemStyle.WriteTakePublic(DrakeRenameit.CurrentItem, on);
        PaperWallSession.Vessel?.RequestSetTakePublic(on);
    }

    static void ApplyStyle()
    {
        var item = DrakeRenameit.CurrentItem;
        if (StyleLocked(item))
            return;

        PaperItemStyle.WriteStyle(item, _fontSize, _landscape);

        var vessel = PaperWallSession.Vessel;
        if (vessel != null)
            vessel.ApplyPaperStyle(_fontSize, _landscape);
    }

    static void AttachButtonHoverTip(Button button, string tipText)
    {
        if (button == null || _panel == null || GUIManager.Instance == null)
            return;

        var tip = new GameObject("btn_tip", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        tip.transform.SetParent(_panel.transform, false);
        tip.SetActive(false);

        var tipRt = tip.GetComponent<RectTransform>();
        tipRt.anchorMin = tipRt.anchorMax = new Vector2(0.5f, 1f);
        tipRt.pivot = new Vector2(0.5f, 0f);
        tipRt.sizeDelta = new Vector2(ContentWidth, 52f);

        var bg = tip.GetComponent<Image>();
        bg.color = new Color(0.08f, 0.07f, 0.06f, 0.92f);
        bg.raycastTarget = false;

        var labelGo = GUIManager.Instance.CreateText(
            text: tipText ?? "",
            parent: tip.transform,
            anchorMin: new Vector2(0.5f, 0.5f),
            anchorMax: new Vector2(0.5f, 0.5f),
            position: Vector2.zero,
            font: GUIManager.Instance.AveriaSerifBold,
            fontSize: 13,
            color: Color.white,
            outline: true,
            outlineColor: Color.black,
            width: ContentWidth - 16f,
            height: 48f,
            addContentSizeFitter: false);
        var label = labelGo != null ? labelGo.GetComponent<Text>() : null;
        if (label != null)
        {
            label.alignment = TextAnchor.MiddleCenter;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            label.raycastTarget = false;
        }

        var hover = button.gameObject.GetComponent<PaperButtonHoverTip>()
                    ?? button.gameObject.AddComponent<PaperButtonHoverTip>();
        hover.Tip = tip;
        hover.HostButton = button;
    }
}

/// <summary>Floating tip above a Paper-tab button while hovered.</summary>
internal sealed class PaperButtonHoverTip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    internal GameObject? Tip;
    internal Button? HostButton;

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (Tip == null || HostButton == null)
            return;
        var btnRt = HostButton.GetComponent<RectTransform>();
        var tipRt = Tip.GetComponent<RectTransform>();
        if (btnRt != null && tipRt != null)
            tipRt.anchoredPosition = new Vector2(0f, btnRt.anchoredPosition.y + btnRt.sizeDelta.y * 0.5f + 6f);

        Tip.SetActive(true);
        Tip.transform.SetAsLastSibling();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (Tip != null)
            Tip.SetActive(false);
    }
}
