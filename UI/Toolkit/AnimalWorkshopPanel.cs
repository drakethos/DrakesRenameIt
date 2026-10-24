using DrakeModsLibs.UI.Toolkit;
using DrakeRenameit.Animals;
using UnityEngine;
using UnityEngine.UIElements;

namespace DrakeRenameit.UI.Toolkit;

/// <summary>
/// The Workshop for a tamed animal: same look and controls as <see cref="PieceWorkshopPanel"/> (name with the formatting toolbar and
/// saved-name suggestions, Reset, Revert / Save with Undo, quick settings), with a Lock switch in place of Shared and a preview of
/// the label drawn over the animal. Permission checks and writes go through <see cref="AnimalRename"/>.
/// </summary>
internal sealed class AnimalWorkshopPanel
{
    const string TitleName = "Name";
    const string LockHint = "Only you (and admins) can change this name. Applies immediately.";
    const string LockDenied = "Only the player who named this animal can change this.";

    static AnimalWorkshopPanel? _instance;

    /// <summary>Where the player dragged the panel to (kept for the session).</summary>
    static Vector2 _offset;

    readonly UkScreen _screen;
    readonly VisualElement _scrim;
    readonly VisualElement _card;
    readonly Button _cogButton;
    readonly UkPopover _cogMenu;
    readonly QuickSettingsContent _settings;
    readonly Label _title;
    readonly Label _subtitle;
    readonly UkField _name;
    readonly UkRichToolbar _richBar;
    readonly Label _preview;
    readonly UkSwitch _lock;
    readonly UkConfirm _reset;
    readonly Button _revert;
    readonly Button _save;
    readonly UkToast _toast;
    readonly UkTooltip _tips;

    Tameable? _animal;
    bool _open;
    bool _dragging;
    string _savedName = "";
    string? _lockReason;
    string? _resetReason;
    string? _undoName;

    public static bool IsOpen => _instance != null && _instance._open;

    public static void Show(Tameable animal)
    {
        _instance ??= new AnimalWorkshopPanel();
        _instance.Open(animal);
    }

    public static void Hide() => _instance?.Close();

    /// <summary>Esc: closes the colour picker / settings / inline confirm first, then the window. True when it handled the key.</summary>
    public static bool TryHandleEscape()
    {
        var panel = _instance;
        if (panel == null || !panel._open)
            return false;
        if (panel._name.SuggestionsOpen)
        {
            panel._name.CloseSuggestions();
            return true;
        }

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

    AnimalWorkshopPanel()
    {
        _screen = UkScreen.Create("drakes_animal_workshop_ui");

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

        // Header: species name, state line, quick settings, close.
        var header = new VisualElement().Row().Pad(18, 22).BorderBottom(UkTheme.LineSoft);
        var titles = new VisualElement().Column().Grow();
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

        _name = new UkField(TitleName, multiline: false, resetTooltip: "Back to the species name", tips: _tips);
        _name.EnableRich();
        _name.Changed = _ => OnEdited();
        _name.ResetClicked = () =>
        {
            if (_animal != null)
                _name.SetValue(AnimalRename.DefaultName(_animal));
            OnEdited();
        };
        _name.SubmitPressed = Save;
        _name.SetSuggestions(_scrim, typed => NameLibrary.Suggest(NameLibrary.Animal, typed));
        _name.Root.style.marginBottom = 10;
        body.Add(_name.Root);

        _richBar = new UkRichToolbar(_tips, _card, () => RenameitConfig.RecentColors, value => RenameitConfig.RecentColors = value,
            (TitleName, _name));
        _richBar.Root.style.marginBottom = 14;
        body.Add(_richBar.Root);

        // The label over the animal, drawn with the formatting applied, at the size the player picked.
        var previewRow = new VisualElement().Row().Fill(UkTheme.Raised).Round(UkTheme.Radius).Pad(10, 14);
        previewRow.style.marginBottom = 16;
        var previewCaption = UkControls.Text("Label shows", 13, UkTheme.TextMuted, wrap: false);
        previewCaption.style.marginRight = 12;
        previewRow.Add(previewCaption);
        _preview = UkControls.Text("", 18, UkTheme.Text, wrap: false);
        _preview.enableRichText = true;
        previewRow.Add(_preview);
        body.Add(previewRow);

        _lock = new UkSwitch("Lock name", LockHint);
        _lock.Changed = OnLockChanged;
        _tips.Attach(_lock.Root, () => _lockReason);
        body.Add(_lock.Root);

        _scrim.RegisterCallback<PointerDownEvent>(e =>
        {
            var target = e.target as VisualElement;
            if (_cogMenu.IsOpen && !_cogMenu.Contains(target) && !_cogButton.Contains(target))
                _cogMenu.Close();
            _richBar.CloseIfOutside(target);
        }, TrickleDown.TrickleDown);

        // Footer: reset (inline confirm), hint, revert, save.
        var footer = new VisualElement().Row().Pad(14, 22).BorderTop(UkTheme.LineSoft).Fill(UkTheme.PanelDeep);
        _reset = new UkConfirm("Reset name", "Back to the species name?", "Reset", OnReset);
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

    void Open(Tameable animal)
    {
        _animal = animal;
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
        _name.CloseSuggestions();
        _scrim.Show(false);
        _toast.Hide();
        _reset.Cancel();
        _cogMenu.Close();
        _animal = null;
        _undoName = null;
        DrakeModsLibs.UI.DrakeGuiInput.EnsureUnblocked();
    }

    /// <summary>The animal is gone or the player lost the right to rename it while the window was open.</summary>
    bool StillEditable()
    {
        var animal = _animal;
        if (animal != null && AnimalRename.CanRename(animal, Player.m_localPlayer, flash: false))
            return true;
        Close();
        return false;
    }

    // ---------------------------------------------------------------- state -> view

    void Refresh()
    {
        var animal = _animal;
        if (animal == null)
            return;

        var player = Player.m_localPlayer;
        var custom = AnimalRename.GetCustomName(animal);
        var original = AnimalRename.DefaultName(animal);
        _savedName = custom.Length > 0 ? custom : original;
        _title.text = original;

        _name.SetMax(RenameitConfig.AnimalNameCharLimit);
        _name.SetEditable(true);
        _name.SetValue(_savedName);
        _richBar.Root.Show(RenameitConfig.AnimalAllowColour);

        var showLock = RenameitConfig.AnimalAllowLock;
        _lock.Root.Show(showLock);
        if (showLock)
        {
            var may = AnimalRename.CanChangeLock(animal, player);
            _lockReason = may ? null : LockDenied;
            _lock.SetValue(AnimalRename.IsLocked(animal));
            _lock.SetEnabled(may, may ? LockHint : LockDenied);
        }

        var canReset = custom.Length > 0;
        _reset.SetEnabled(canReset);
        _resetReason = canReset ? null : "Nothing to reset: this animal has its species name.";
        OnEdited();
    }

    void RevertFields()
    {
        _name.SetValue(_savedName);
        OnEdited();
    }

    void OnEdited()
    {
        var animal = _animal;
        if (animal == null)
            return;
        var original = AnimalRename.DefaultName(animal);
        var dirty = _name.Value != _savedName;
        _name.SetChanged(dirty);
        _name.SetResetVisible(_name.Value != original);
        UkControls.SetButtonEnabled(_save, dirty);
        UkControls.SetButtonEnabled(_revert, dirty);
        _subtitle.text = dirty ? "Unsaved changes" : SubtitleForSaved(animal);
        _preview.text = string.IsNullOrEmpty(_name.Value) ? original : _name.Value;
        var scale = string.Equals(RenameitConfig.AnimalLabelStyle, "Improved", System.StringComparison.OrdinalIgnoreCase)
            ? RenameitConfig.AnimalLabelScale
            : 1f;
        _preview.style.fontSize = Mathf.RoundToInt(18f * Mathf.Clamp(scale, 0.8f, 1.6f));
    }

    static string SubtitleForSaved(Tameable animal)
    {
        var namer = AnimalRename.GetNamer(animal);
        var who = namer == 0L ? "Not named yet" : namer == Player.m_localPlayer?.GetPlayerID() ? "Named by you" : "Named by another player";
        return AnimalRename.IsLocked(animal) ? who + " · Locked" : who;
    }

    // ---------------------------------------------------------------- actions

    void Save()
    {
        var animal = _animal;
        if (animal == null || !_save.enabledSelf || !StillEditable())
            return;
        var before = AnimalRename.GetCustomName(animal);
        // A value equal to the species name means "no custom name".
        var value = _name.Value == AnimalRename.DefaultName(animal) ? "" : _name.Value;
        AnimalRename.SetName(animal, value);
        NameLibrary.Add(NameLibrary.Animal, value);
        _undoName = before;
        _savedName = value.Length > 0 ? AnimalRename.Sanitize(value) : AnimalRename.DefaultName(animal);
        _name.SetValue(_savedName);
        _reset.SetEnabled(value.Length > 0);
        OnEdited();
        _toast.Show("Saved", Undo);
    }

    void Undo()
    {
        var animal = _animal;
        if (animal == null || _undoName == null || !StillEditable())
            return;
        AnimalRename.SetName(animal, _undoName);
        _savedName = _undoName.Length > 0 ? _undoName : AnimalRename.DefaultName(animal);
        _name.SetValue(_savedName);
        _undoName = null;
        OnEdited();
        _toast.Show("Reverted", null, 3f);
    }

    void OnReset()
    {
        var animal = _animal;
        if (animal == null || !StillEditable())
            return;
        _undoName = AnimalRename.GetCustomName(animal);
        AnimalRename.SetName(animal, "");
        _savedName = AnimalRename.DefaultName(animal);
        _name.SetValue(_savedName);
        _reset.SetEnabled(false);
        OnEdited();
        _toast.Show("Reset to the species name", Undo);
    }

    void OnLockChanged(bool on)
    {
        var animal = _animal;
        if (animal == null)
            return;
        if (!AnimalRename.CanChangeLock(animal, Player.m_localPlayer))
        {
            _lock.SetValue(AnimalRename.IsLocked(animal));
            return;
        }

        AnimalRename.SetLocked(animal, on);
        _subtitle.text = SubtitleForSaved(animal);
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
