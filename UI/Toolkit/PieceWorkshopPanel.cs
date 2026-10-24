using System;
using DrakeModsLibs.UI.Toolkit;
using DrakeRenameit.Pieces;
using UnityEngine;
using UnityEngine.UIElements;

namespace DrakeRenameit.UI.Toolkit;

/// <summary>
/// The Workshop for a placed piece: same look and controls as the item Rename tab, trimmed to what a piece has
/// (a name with the formatting toolbar, Shared, Reset, Revert / Save with Undo, quick settings).
/// Permission checks and writes go through <see cref="PieceRename"/>.
/// </summary>
internal sealed class PieceWorkshopPanel
{
    // Strings are English for now, like WorkshopPanel; move to RenameItLocalization with it.
    const string TitleName = "Name";
    const string SharedHint = "Anyone with access to the ward here can rename this piece. Applies immediately.";
    const string SharedDenied = "Only the builder or an admin can change this.";

    static PieceWorkshopPanel? _instance;

    /// <summary>Where the player dragged the panel to (kept for the session).</summary>
    static Vector2 _offset;

    readonly UkScreen _screen;
    readonly VisualElement _scrim;
    readonly VisualElement _card;
    readonly Button _cogButton;
    readonly UkPopover _cogMenu;
    readonly QuickSettingsContent _settings;
    readonly VisualElement _iconSlot;
    readonly Label _title;
    readonly Label _subtitle;
    readonly UkField _name;
    readonly UkRichToolbar _richBar;
    readonly Label _preview;
    readonly UkSwitch _shared;
    readonly UkConfirm _reset;
    readonly Button _revert;
    readonly Button _save;
    readonly UkToast _toast;
    readonly UkTooltip _tips;

    Piece? _piece;
    bool _open;
    bool _dragging;
    string _savedName = "";
    string? _sharedReason;
    string? _resetReason;
    string? _undoName;

    public static bool IsOpen => _instance != null && _instance._open;

    public static void Show(Piece piece)
    {
        _instance ??= new PieceWorkshopPanel();
        _instance.Open(piece);
    }

    public static void Hide() => _instance?.Close();

    /// <summary>Esc: closes the colour picker / settings / inline confirm first, then the window. True when it handled the key.</summary>
    public static bool TryHandleEscape()
    {
        var panel = _instance;
        if (panel == null || !panel._open)
            return false;
        if (panel._richBar.PickerOpen)
        {
            panel._richBar.ClosePicker();
            return true;
        }

        if (panel._cogMenu.IsOpen)
        {
            panel._cogMenu.Close();
            return true;
        }

        if (panel._reset.IsAsking)
        {
            panel._reset.Cancel();
            return true;
        }

        panel.Close();
        return true;
    }

    PieceWorkshopPanel()
    {
        _screen = UkScreen.Create("drakes_piece_workshop_ui");

        _scrim = new VisualElement().Fill(UkTheme.Scrim);
        _scrim.style.position = Position.Absolute;
        _scrim.style.left = _scrim.style.right = _scrim.style.top = _scrim.style.bottom = 0;
        _scrim.style.alignItems = Align.Center;
        _scrim.style.justifyContent = Justify.Center;
        _scrim.pickingMode = PickingMode.Position;
        _scrim.Show(false);
        _screen.Root.Add(_scrim);
        _tips = new UkTooltip(_screen.Root);

        _card = new VisualElement().Column().Fill(UkTheme.Panel).Round(UkTheme.RadiusLarge).Border(UkTheme.Line);
        _card.style.width = 560;
        _card.style.overflow = Overflow.Hidden;
        _scrim.Add(_card);

        // Header: piece icon, its normal name, state line, quick settings, close.
        var header = new VisualElement().Row().Pad(18, 22).BorderBottom(UkTheme.LineSoft);
        _iconSlot = new VisualElement().Fixed();
        header.Add(_iconSlot);
        var titles = new VisualElement().Column().Grow();
        titles.style.marginLeft = 14;
        _title = UkControls.Text("", 24, UkTheme.Text, heading: true, wrap: false);
        _subtitle = UkControls.Text("", 14, UkTheme.TextMuted, wrap: false);
        titles.Add(_title);
        titles.Add(_subtitle);
        header.Add(titles);
        _cogButton = UkControls.MakeIconButton("", () => _cogMenu!.Toggle(_cogButton!), "Quick settings", UkButtonKind.Secondary);
        _cogButton.style.alignItems = Align.Center;
        _cogButton.style.justifyContent = Justify.Center;
        _cogButton.style.marginRight = 8;
        _cogButton.Add(UkIcons.Gear(22f, UkTheme.TextMuted));
        header.Add(_cogButton);
        header.Add(UkControls.MakeIconButton("✕", Close, "Close (Esc)", UkButtonKind.Secondary));
        _card.Add(header);
        MakeDraggable(header);

        _cogMenu = new UkPopover(_card, 430f);
        _settings = new QuickSettingsContent(withHeader: true);
        _cogMenu.Content.Add(_settings.Root);

        var body = new VisualElement().Column().Pad(20, 22);
        _card.Add(body);

        _name = new UkField(TitleName, multiline: false, resetTooltip: "Back to the original name", tips: _tips);
        _name.EnableRich();
        _name.Changed = _ => OnEdited();
        _name.ResetClicked = () =>
        {
            if (_piece != null)
                _name.SetValue(PieceRename.DefaultName(_piece));
            OnEdited();
        };
        _name.SubmitPressed = Save;
        _name.SetSuggestions(_scrim, typed => _piece == null ? System.Linq.Enumerable.Empty<(string, string)>() : NameLibrary.Suggest(NameLibrary.PieceKind(_piece), typed));
        _name.Root.style.marginBottom = 10;
        body.Add(_name.Root);

        _richBar = new UkRichToolbar(_tips, _card, () => RenameitConfig.RecentColors, value => RenameitConfig.RecentColors = value,
            (TitleName, _name));
        _richBar.Root.style.marginBottom = 14;
        body.Add(_richBar.Root);

        // What the hover will say, drawn with the formatting applied.
        var previewRow = new VisualElement().Row().Fill(UkTheme.Raised).Round(UkTheme.Radius).Pad(10, 14);
        previewRow.style.marginBottom = 16;
        var previewCaption = UkControls.Text("Hover shows", 13, UkTheme.TextMuted, wrap: false);
        previewCaption.style.marginRight = 12;
        previewRow.Add(previewCaption);
        _preview = UkControls.Text("", 18, UkTheme.Text, wrap: false);
        _preview.enableRichText = true;
        previewRow.Add(_preview);
        body.Add(previewRow);

        _shared = new UkSwitch("Shared", SharedHint);
        _shared.Changed = OnSharedChanged;
        _tips.Attach(_shared.Root, () => _sharedReason);
        body.Add(_shared.Root);

        _scrim.RegisterCallback<PointerDownEvent>(e =>
        {
            var target = e.target as VisualElement;
            if (_cogMenu.IsOpen && !_cogMenu.Contains(target) && !_cogButton.Contains(target))
                _cogMenu.Close();
            _richBar.CloseIfOutside(target);
        }, TrickleDown.TrickleDown);

        // Footer: reset (inline confirm), hint, revert, save.
        var footer = new VisualElement().Row().Pad(14, 22).BorderTop(UkTheme.LineSoft).Fill(UkTheme.PanelDeep);
        _reset = new UkConfirm("Reset name", "Back to the original name?", "Reset", OnReset);
        footer.Add(_reset.Root);
        _tips.Attach(_reset.Root, () => _resetReason);
        footer.Add(UkControls.Spacer());
        var hint = UkControls.Text("Enter save · Esc close", 13, UkTheme.TextMuted, wrap: false);
        hint.style.marginRight = 14;
        footer.Add(hint);
        _revert = UkControls.MakeButton("Revert", RevertFields, UkButtonKind.Secondary);
        _revert.style.marginRight = 8;
        footer.Add(_revert);
        _save = UkControls.MakeButton("Save", Save, UkButtonKind.Primary, width: 110);
        footer.Add(_save);
        _card.Add(footer);

        _toast = new UkToast();
        _toast.Root.style.marginLeft = _toast.Root.style.marginRight = 22;
        _toast.Root.style.marginBottom = 14;
        _toast.Root.style.marginTop = 0;
        _card.Add(_toast.Root);
    }

    // ---------------------------------------------------------------- open / close

    void Open(Piece piece)
    {
        _piece = piece;
        _undoName = null;
        DrakeModsLibs.UI.DrakeGuiInput.EnsureBlocked();
        _toast.Hide();
        _reset.Cancel();
        _screen.RaiseAboveCanvases();
        _cogMenu.Close();
        _settings.Sync();
        _scrim.Show(true);
        _open = true;
        ApplyOffset();
        Refresh();
        _name.Focus();
    }

    void Close()
    {
        if (!_open)
            return;
        _open = false;
        _scrim.Show(false);
        _toast.Hide();
        _reset.Cancel();
        _cogMenu.Close();
        _piece = null;
        _undoName = null;
        DrakeModsLibs.UI.DrakeGuiInput.EnsureUnblocked();
    }

    /// <summary>The piece was destroyed or the player lost the right to edit it while the window was open.</summary>
    bool StillEditable()
    {
        var piece = _piece;
        if (piece != null && PieceRename.CanRename(piece, Player.m_localPlayer, flash: false))
            return true;
        Close();
        return false;
    }

    // ---------------------------------------------------------------- state -> view

    void Refresh()
    {
        var piece = _piece;
        if (piece == null)
            return;

        var player = Player.m_localPlayer;
        var custom = PieceRename.GetCustomName(piece);
        var original = PieceRename.DefaultName(piece);
        _savedName = custom.Length > 0 ? custom : original;

        _iconSlot.Clear();
        _iconSlot.Add(UkControls.IconTile(piece.m_icon, 56));
        _title.text = original;

        _name.SetMax(RenameitConfig.PieceNameCharLimit);
        _name.SetEditable(true);
        _name.SetValue(_savedName);

        var showShared = RenameitConfig.PieceSharedEnabled && RenameitConfig.PieceLockToBuilder;
        _shared.Root.Show(showShared);
        if (showShared)
        {
            var may = PieceRename.CanChangeShared(piece, player);
            _sharedReason = may ? null : SharedDenied;
            _shared.SetValue(PieceRename.IsShared(piece));
            _shared.SetEnabled(may, may ? SharedHint : SharedDenied);
        }

        var canReset = custom.Length > 0;
        _reset.SetEnabled(canReset);
        _resetReason = canReset ? null : "Nothing to reset: this piece has its original name.";
        OnEdited();
    }

    void RevertFields()
    {
        _name.SetValue(_savedName);
        OnEdited();
    }

    void OnEdited()
    {
        var piece = _piece;
        if (piece == null)
            return;
        var original = PieceRename.DefaultName(piece);
        var dirty = _name.Value != _savedName;
        _name.SetChanged(dirty);
        _name.SetResetVisible(_name.Value != original);
        UkControls.SetButtonEnabled(_save, dirty);
        UkControls.SetButtonEnabled(_revert, dirty);
        _subtitle.text = dirty ? "Unsaved changes" : SubtitleForSaved(piece);
        _preview.text = string.IsNullOrEmpty(_name.Value) ? original : _name.Value;
    }

    static string SubtitleForSaved(Piece piece)
    {
        var who = PieceRename.IsBuilder(piece, Player.m_localPlayer) ? "Yours" : "Built by another player";
        return RenameitConfig.PieceSharedEnabled && PieceRename.IsShared(piece) ? who + " · Shared" : who;
    }

    // ---------------------------------------------------------------- actions

    void Save()
    {
        var piece = _piece;
        if (piece == null || !_save.enabledSelf || !StillEditable())
            return;
        var before = PieceRename.GetCustomName(piece);
        // A value equal to the original name means "no custom name".
        var value = _name.Value == PieceRename.DefaultName(piece) ? "" : _name.Value;
        PieceRename.SetCustomName(piece, value);
        NameLibrary.Add(NameLibrary.PieceKind(piece), value);
        _undoName = before;
        Refresh();
        _toast.Show("Saved", Undo);
    }

    void Undo()
    {
        var piece = _piece;
        if (piece == null || _undoName == null || !StillEditable())
            return;
        PieceRename.SetCustomName(piece, _undoName);
        _undoName = null;
        Refresh();
        _toast.Show("Reverted", null, 3f);
    }

    void OnReset()
    {
        var piece = _piece;
        if (piece == null || !StillEditable())
            return;
        _undoName = PieceRename.GetCustomName(piece);
        PieceRename.SetCustomName(piece, "");
        Refresh();
        _toast.Show("Reset to original", Undo);
    }

    void OnSharedChanged(bool on)
    {
        var piece = _piece;
        if (piece == null)
            return;
        if (!PieceRename.CanChangeShared(piece, Player.m_localPlayer))
        {
            _shared.SetValue(PieceRename.IsShared(piece));
            return;
        }
        PieceRename.SetShared(piece, on);
        _subtitle.text = SubtitleForSaved(piece);
    }

    // ---------------------------------------------------------------- drag

    void MakeDraggable(VisualElement handle)
    {
        handle.RegisterCallback<PointerDownEvent>(e =>
        {
            if (e.button != 0 || e.target is Button)
                return;
            _dragging = true;
            handle.CapturePointer(e.pointerId);
            e.StopPropagation();
        });
        handle.RegisterCallback<PointerMoveEvent>(e =>
        {
            if (!_dragging)
                return;
            _offset += new Vector2(e.deltaPosition.x, e.deltaPosition.y);
            ApplyOffset();
        });
        handle.RegisterCallback<PointerUpEvent>(e =>
        {
            if (!_dragging)
                return;
            _dragging = false;
            handle.ReleasePointer(e.pointerId);
        });
        handle.RegisterCallback<PointerCaptureOutEvent>(_ => _dragging = false);
    }

    void ApplyOffset()
    {
        var rootW = _scrim.resolvedStyle.width;
        var rootH = _scrim.resolvedStyle.height;
        var cardW = _card.resolvedStyle.width;
        var cardH = _card.resolvedStyle.height;
        if (rootW > 0f && cardW > 0f)
        {
            var left = (rootW - cardW) * 0.5f;
            var top = (rootH - cardH) * 0.5f;
            _offset.x = Mathf.Clamp(_offset.x, -(left + cardW - 140f), rootW - 140f - left);
            _offset.y = Mathf.Clamp(_offset.y, -top, rootH - 70f - top);
        }

        _card.style.translate = new Translate(_offset.x, _offset.y);
    }
}
