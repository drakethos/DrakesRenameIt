using System;
using DrakeModsLibs.UI;
using DrakeModsLibs.UI.Toolkit;
using DrakeRenameit.API;
using DrakeRenameit.ModText;
using DrakeRenameit.Paper.Functionality;
using DrakeRenameit.Paper.Items;
using DrakeRenameit.Paper.Pieces;
using UnityEngine;
using UnityEngine.UIElements;
using static DrakeRenameit.ModText.RenameItLocalization;

namespace DrakeRenameit.UI.Toolkit;

/// <summary>
/// The Paper tab in the suite's new look (a <see cref="UkWindow"/>): the same options and rules as the classic
/// <see cref="ClassicPaperTabPanel"/>. Public page, landscape text, font size, immutable stack, make copies (with the cost
/// shown), recycle. Controls you can't use stay visible but dim and say why on hover.
/// </summary>
internal sealed class PaperWorkshopPanel
{
    static PaperWorkshopPanel? _instance;

    readonly UkWindow _window;
    readonly UkSwitch _public;
    readonly UkSwitch _landscape;
    readonly UkSwitch _immutable;
    readonly VisualElement _fontCard;
    readonly Label _fontHint;
    readonly UkStepper _fontStepper;
    readonly UkStepper _copyStepper;
    readonly Label _costLabel;
    readonly Button _copyButton;
    readonly VisualElement _copyRow;
    readonly VisualElement _recycleCard;
    readonly UkConfirm _recycle;

    // Same state the classic panel keeps (copies is remembered between openings).
    float _fontSize = PaperFontScale.LevelToSize(3);
    bool _landscapeOn;
    bool _takePublic;
    bool _immutableStack;
    int _copies = 1;
    string? _publicReason;
    string? _styleReason;
    string? _copyReason;

    public static bool IsOpen => _instance != null && _instance._window.IsOpen;

    /// <summary>The tab host opened the Paper tab.</summary>
    public static void Show(DrakeTabPageContext ctx)
    {
        _instance ??= new PaperWorkshopPanel();
        _instance.Open(ctx.Item);
    }

    /// <summary>Hide without ending the host session (tab switch, wall session over).</summary>
    public static void Hide() => _instance?._window.CloseSilent();

    PaperWorkshopPanel()
    {
        _window = new UkWindow("drakes_paper_workshop", 540f);
        // Closing the window ends the tab-host / wall session, like the classic panel's Close button.
        _window.Closed = () => DrakeTabHost.Close();

        var body = _window.Body;
        body.style.paddingTop = 16;

        _public = new UkSwitch("Public page", "Anyone can take a copy when it is pinned");
        _public.Changed = OnPublicChanged;
        _public.Root.style.marginBottom = 8;
        body.Add(_public.Root);
        _window.Tips.Attach(_public.Root, () => _publicReason);

        _landscape = new UkSwitch("Landscape text", "Lay the text on its side");
        _landscape.Changed = on =>
        {
            _landscapeOn = on;
            ApplyStyle();
        };
        _landscape.Root.style.marginBottom = 8;
        body.Add(_landscape.Root);
        _window.Tips.Attach(_landscape.Root, () => _styleReason);

        _fontCard = new VisualElement().Row().Fill(UkTheme.PanelDeep).Round(UkTheme.Radius + 2).Border(UkTheme.LineSoft).Pad(12, 16);
        _fontCard.style.marginBottom = 8;
        var fontText = new VisualElement().Column().Grow();
        fontText.Add(UkControls.Text("Font size", 16, UkTheme.Text, heading: true, wrap: false));
        _fontHint = UkControls.Text("", 14, UkTheme.TextMuted, wrap: false);
        _fontHint.style.marginTop = 2;
        fontText.Add(_fontHint);
        _fontCard.Add(fontText);
        _fontStepper = new UkStepper(PaperFontScale.LevelMin, PaperFontScale.LevelMax, PaperFontScale.StoredToLevel(_fontSize));
        _fontStepper.Changed = level =>
        {
            _fontSize = PaperFontScale.LevelToSize(level);
            ApplyStyle();
            RefreshFontHint();
        };
        _fontCard.Add(_fontStepper.Root);
        body.Add(_fontCard);
        _window.Tips.Attach(_fontCard, () => _styleReason);

        _immutable = new UkSwitch("Immutable stack", "Print copies that can't be rewritten and stack together");
        _immutable.Changed = OnImmutableChanged;
        _immutable.Root.style.marginBottom = 8;
        body.Add(_immutable.Root);

        // Make copies: how many, what it costs, and the button.
        var copyCard = UkControls.Card().Pad(14, 16);
        copyCard.style.marginBottom = 8;
        var copyHead = new VisualElement().Row();
        copyHead.Add(UkControls.Text("Make copies", 16, UkTheme.Text, heading: true, wrap: false));
        copyCard.Add(copyHead);
        _costLabel = UkControls.Text("", 14, UkTheme.TextMuted);
        _costLabel.style.marginTop = 4;
        copyCard.Add(_costLabel);
        _copyRow = new VisualElement().Row();
        _copyRow.style.marginTop = 12;
        _copyStepper = new UkStepper(PaperCopyService.CopiesMin, PaperCopyService.CopiesMax, _copies);
        _copyStepper.Changed = n =>
        {
            _copies = n;
            RefreshCopyCost();
        };
        _copyRow.Add(_copyStepper.Root);
        _copyRow.Add(UkControls.Spacer());
        _copyButton = UkControls.MakeButton(T(LKeys.CopyPageBtn), OnCopyClicked, UkButtonKind.Primary, 160f);
        _copyRow.Add(_copyButton);
        copyCard.Add(_copyRow);
        body.Add(copyCard);
        _window.Tips.Attach(_copyRow, () => _copyReason);

        // Recycle: asks inline, no popup.
        _recycleCard = UkControls.Card().Row().Pad(12, 16);
        var recycleText = new VisualElement().Column().Grow();
        recycleText.Add(UkControls.Text("Recycle", 16, UkTheme.Text, heading: true, wrap: false));
        var recycleHint = UkControls.Text(T(LKeys.RecycleBtnTip), 14, UkTheme.TextMuted);
        recycleHint.style.marginTop = 2;
        recycleText.Add(recycleHint);
        _recycleCard.Add(recycleText);
        _recycle = new UkConfirm(T(LKeys.RecycleBtn), "Wipe the writing and get a blank sheet back?", "Recycle", OnRecycleConfirmed);
        _recycleCard.Add(_recycle.Root);
        body.Add(_recycleCard);

        _window.Footer.Add(UkControls.Spacer());
        _window.Footer.Add(UkControls.MakeButton("Close", () => _window.Close(), UkButtonKind.Secondary));
        _window.ShowFooter(true);

        // Esc cancels a pending "Recycle?" before it closes the window.
        _window.EscapeHandler = () =>
        {
            if (!_recycle.IsAsking)
                return false;
            _recycle.Cancel();
            return true;
        };
    }

    void Open(ItemDrop.ItemData? item)
    {
        DrakeRenameit.CurrentItem = item;
        _recycle.Cancel();
        LoadFromContext(item);
        Refresh();
        _window.ShowTabs(PaperTabPanel.TabId);
        _window.Open();
    }

    // ---------------------------------------------------------------- state (same rules as the classic panel)

    void LoadFromContext(ItemDrop.ItemData? item)
    {
        var vessel = PaperWallSession.Vessel;
        if (vessel != null)
        {
            _fontSize = PaperFontScale.NormalizeStored(vessel.ReadFontSize());
            _landscapeOn = vessel.ReadLandscape();
            _takePublic = vessel.ReadTakePublicPublic();
            PaperItemStyle.WriteAll(item, _fontSize, _landscapeOn, _takePublic);
        }
        else
        {
            PaperItemStyle.Read(item, out _fontSize, out _landscapeOn, out _takePublic);
            _fontSize = PaperFontScale.NormalizeStored(_fontSize);
        }

        // Printed is always immutable print mode; Written defaults to writable copies.
        _immutableStack = IsCopyContext(item);
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

    bool PrintModeActive(ItemDrop.ItemData? item) => IsCopyContext(item) || _immutableStack;

    static int CopyMax() => Mathf.Min(PaperCopyService.PrintMax, RenameitConfig.PrintedPaperStackSize);

    // ---------------------------------------------------------------- view

    void Refresh()
    {
        var vessel = PaperWallSession.Vessel;
        var item = DrakeRenameit.CurrentItem;
        var copyCtx = IsCopyContext(item);
        var styleLocked = StyleLocked(item);
        var printMode = PrintModeActive(item);

        var name = item != null ? DrakeRenameit.GetPropperName(item) : "Written Page";
        _window.SetHeader(Loc(name), "Paper", item?.GetIcon());

        _fontStepper.SetValueWithoutNotify(PaperFontScale.StoredToLevel(_fontSize));
        RefreshFontHint();

        var copyMin = printMode ? PaperCopyService.PrintMin : PaperCopyService.CopiesMin;
        var copyMax = printMode ? CopyMax() : PaperCopyService.CopiesMax;
        _copyStepper.SetRange(copyMin, copyMax);
        _copies = Mathf.Clamp(_copies, copyMin, copyMax);
        _copyStepper.SetValueWithoutNotify(_copies);

        // Public page: hidden when the feature is off; dim (with the reason) when this player may not flip it.
        var showPublic = TakePublicFeatureOn();
        _public.Root.Show(showPublic);
        if (showPublic)
        {
            var may = vessel == null || vessel.LocalMayToggleTakePublicPublic();
            if (vessel != null)
                _takePublic = vessel.ReadTakePublicPublic();
            _public.SetValue(_takePublic);
            _public.SetEnabled(may);
            _publicReason = may ? null : "Only the page's owner can change this.";
        }

        _styleReason = styleLocked ? "This is a printed copy, so its style is fixed." : null;
        _landscape.SetValue(_landscapeOn);
        _landscape.SetEnabled(!styleLocked);
        _fontStepper.SetEnabled(!styleLocked);
        _fontCard.style.opacity = styleLocked ? 0.6f : 1f;

        // Immutable checkbox: templates only. Copies are always print mode.
        _immutable.Root.Show(!copyCtx);
        _immutable.SetValue(_immutableStack);

        RefreshCopyCost();
    }

    void RefreshFontHint() =>
        _fontHint.text = $"Level {PaperFontScale.StoredToLevel(_fontSize)} of {PaperFontScale.LevelMax}";

    void RefreshCopyCost()
    {
        var player = Player.m_localPlayer;
        var item = DrakeRenameit.CurrentItem;
        var canAfford = PaperCopyService.CanAfford(player, _copies);
        var written = item != null && PaperItem.IsWrittenLike(item);
        var canCopy = written && canAfford;

        _costLabel.text = PaperCopyService.FormatCostLine(player, _copies);
        UkControls.SetButtonEnabled(_copyButton, canCopy);
        _copyReason = canCopy ? null : !written ? "Only written pages can be copied." : "Not enough blank paper.";

        _recycleCard.Show(PaperRecycleService.CanRecycle(item, player));
    }

    // ---------------------------------------------------------------- actions

    void OnPublicChanged(bool on)
    {
        _takePublic = on;
        PaperItemStyle.WriteTakePublic(DrakeRenameit.CurrentItem, on);
        PaperWallSession.Vessel?.RequestSetTakePublic(on);
    }

    void OnImmutableChanged(bool on)
    {
        _immutableStack = on;
        var item = DrakeRenameit.CurrentItem;
        var printMode = PrintModeActive(item);
        var copyMin = printMode ? PaperCopyService.PrintMin : PaperCopyService.CopiesMin;
        var copyMax = printMode ? CopyMax() : PaperCopyService.CopiesMax;
        _copyStepper.SetRange(copyMin, copyMax, notifyIfClamped: true);
        _copies = Mathf.Clamp(_copies, copyMin, copyMax);
        RefreshCopyCost();
    }

    void ApplyStyle()
    {
        var item = DrakeRenameit.CurrentItem;
        if (StyleLocked(item))
            return;

        PaperItemStyle.WriteStyle(item, _fontSize, _landscapeOn);
        PaperWallSession.Vessel?.ApplyPaperStyle(_fontSize, _landscapeOn);
    }

    void OnCopyClicked()
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

        if (!ok && !string.IsNullOrEmpty(err))
            _window.Toast.Show(err, null, 4f);
        else if (ok)
            _window.Toast.Show(_copies == 1 ? "Made a copy" : $"Made {_copies} copies", null, 3f);

        RefreshCopyCost();
    }

    void OnRecycleConfirmed()
    {
        var current = DrakeRenameit.CurrentItem;
        var player = Player.m_localPlayer;
        if (!PaperRecycleService.CanRecycle(current, player))
            return;

        if (!PaperRecycleService.TryRecycleToBlank(current, player, out var err))
        {
            if (!string.IsNullOrEmpty(err))
                _window.Toast.Show(err, null, 4f);
            return;
        }

        DrakeRenameit.CurrentItem = null;
        _window.Close();
    }

    /// <summary>Tokens like <c>$item_x</c> become what the tooltip shows.</summary>
    static string Loc(string? text) =>
        string.IsNullOrEmpty(text) ? "" : Localization.instance != null ? Localization.instance.Localize(text) : text!;
}
