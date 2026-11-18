using System;
using System.Collections.Generic;
using DrakeModsLibs.UI.Toolkit;
using DrakeRenameit.Map;
using UnityEngine;
using UnityEngine.UIElements;

namespace DrakeRenameit.UI.Toolkit;

/// <summary>
/// The map pin editor (Shift + click a pin): name with the formatting toolbar and saved-name suggestions, the pin's tint, its icon,
/// the checked mark and Remove. Same look and keys as the piece and animal windows; the writes go through <see cref="PinEdit"/>.
/// </summary>
internal sealed class PinEditorPanel
{
    const string TitleName = "Name";

    /// <summary>Quick tints for the pin and its label; the picker covers everything else.</summary>
    static readonly string[] Presets =
    {
        "#f3c873", "#e8903f", "#d8584a", "#e58fb6", "#b992e6", "#6aa8e0", "#5cc6b8", "#7fbf6a", "#ffffff", "#9a9a9a",
    };

    static PinEditorPanel? _instance;
    static Vector2 _offset;

    readonly UkScreen _screen;
    readonly VisualElement _scrim;
    readonly VisualElement _card;
    readonly Label _title;
    readonly Label _subtitle;
    readonly UkField _name;
    readonly UkRichToolbar _richBar;
    readonly VisualElement _swatchRow;
    readonly List<(VisualElement Dot, string Hex)> _dots = new();
    readonly UkPopover _pickerPopover;
    readonly UkColorPicker _picker;
    readonly Button _customButton;
    readonly VisualElement _iconRow;
    readonly List<(UkChip Chip, Minimap.PinType Type)> _chips = new();
    readonly UkSwitch _checked;
    readonly UkConfirm _remove;
    readonly Button _revert;
    readonly Button _save;
    readonly UkToast _toast;
    readonly UkTooltip _tips;

    Minimap.PinData? _pin;
    bool _open;
    bool _dragging;
    string _savedName = "";
    string _savedColour = "";
    Minimap.PinType _savedType;
    bool _savedChecked;
    bool _checkedNow;
    string _colour = "";
    Minimap.PinType _type;

    public static bool IsOpen => _instance != null && _instance._open;

    public static void Show(Minimap.PinData pin)
    {
        _instance ??= new PinEditorPanel();
        _instance.Open(pin);
    }

    public static void Hide() => _instance?.Close();

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

        if (panel._pickerPopover.IsOpen)
        {
            panel._pickerPopover.Close();
            return true;
        }

        if (panel._remove.IsAsking)
        {
            panel._remove.Cancel();
            return true;
        }

        panel.Close();
        return true;
    }

    PinEditorPanel()
    {
        _screen = UkScreen.Create("drakes_pin_editor_ui");

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

        var header = new VisualElement().Row().Pad(18, 22).BorderBottom(UkTheme.LineSoft);
        var titles = new VisualElement().Column().Grow();
        _title = UkControls.Text("Edit pin", 24, UkTheme.Text, heading: true, wrap: false);
        _subtitle = UkControls.Text("", 14, UkTheme.TextMuted, wrap: false);
        titles.Add(_title);
        titles.Add(_subtitle);
        header.Add(titles);
        header.Add(UkControls.MakeIconButton("✕", Close, "Close (Esc)", UkButtonKind.Secondary));
        _card.Add(header);
        MakeDraggable(header);

        var body = new VisualElement().Column().Pad(20, 22);
        _card.Add(body);

        _name = new UkField(TitleName, multiline: false, resetTooltip: "Clear the name", tips: _tips);
        _name.EnableRich();
        _name.SetMax(40);
        _name.Changed = _ => OnEdited();
        _name.ResetClicked = () =>
        {
            _name.SetValue("");
            OnEdited();
        };
        _name.SubmitPressed = Save;
        _name.SetSuggestions(_scrim, typed => NameLibrary.Suggest(NameLibrary.Pin, typed));
        _name.Root.style.marginBottom = 10;
        body.Add(_name.Root);

        _richBar = new UkRichToolbar(_tips, _card, () => RenameitConfig.RecentColors, value => RenameitConfig.RecentColors = value,
            (TitleName, _name));
        _richBar.Root.style.marginBottom = 14;
        body.Add(_richBar.Root);

        // The tint for the pin and its label.
        body.Add(Caption("Pin colour"));
        _swatchRow = new VisualElement().Row();
        _swatchRow.style.marginBottom = 14;
        _swatchRow.style.flexWrap = Wrap.Wrap;
        _swatchRow.Add(Swatch("", "Normal look"));
        foreach (var hex in Presets)
            _swatchRow.Add(Swatch(hex, hex));
        _customButton = UkControls.MakeButton("Custom", () => _pickerPopover!.Toggle(_customButton!), UkButtonKind.Secondary);
        _customButton.style.height = 28;
        _customButton.style.marginLeft = 6;
        _swatchRow.Add(_customButton);
        body.Add(_swatchRow);
        _picker = new UkColorPicker();
        _picker.Picked = hex => SetColour(hex);
        _pickerPopover = new UkPopover(_card, 310f);
        _pickerPopover.Content.Add(_picker.Root);

        body.Add(Caption("Icon"));
        _iconRow = new VisualElement().Row();
        _iconRow.style.flexWrap = Wrap.Wrap;
        _iconRow.style.marginBottom = 8;
        foreach (var (type, label) in PinEdit.Icons)
        {
            var chip = new UkChip(label);
            var captured = type;
            chip.Changed = _ => SetType(captured);
            _chips.Add((chip, type));
            _iconRow.Add(chip.Root);
        }

        body.Add(_iconRow);

        _checked = new UkSwitch("Crossed off", "The pin shows a cross (same as clicking it).");
        _checked.Changed = on =>
        {
            _checkedNow = on;
            OnEdited();
        };
        body.Add(_checked.Root);

        _scrim.RegisterCallback<PointerDownEvent>(e =>
        {
            var target = e.target as VisualElement;
            if (_pickerPopover.IsOpen && !_pickerPopover.Contains(target) && !_customButton.Contains(target))
                _pickerPopover.Close();
            _richBar.CloseIfOutside(target);
        }, TrickleDown.TrickleDown);

        var footer = new VisualElement().Row().Pad(14, 22).BorderTop(UkTheme.LineSoft).Fill(UkTheme.PanelDeep);
        _remove = new UkConfirm("Remove pin", "Remove this pin from the map?", "Remove", OnRemove);
        footer.Add(_remove.Root);
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

    static Label Caption(string text)
    {
        var caption = UkControls.Text(text, 15, UkTheme.Text, heading: true, wrap: false);
        caption.style.marginBottom = 6;
        return caption;
    }

    VisualElement Swatch(string hex, string tip)
    {
        var dot = new VisualElement();
        dot.style.width = dot.style.height = 28;
        dot.style.marginRight = 6;
        dot.style.marginBottom = 4;
        dot.Round(14);
        if (hex.Length == 0)
        {
            dot.Fill(UkTheme.Field);
            dot.Add(UkControls.Text("×", 16, UkTheme.TextMuted, wrap: false));
            dot.style.alignItems = Align.Center;
            dot.style.justifyContent = Justify.Center;
        }
        else
        {
            UkColorPicker.TryParse(hex, out var colour);
            dot.Fill(colour);
        }

        _tips.Attach(dot, () => tip);
        dot.RegisterCallback<PointerDownEvent>(e =>
        {
            if (e.button != 0)
                return;
            SetColour(hex);
            e.StopPropagation();
        });
        _dots.Add((dot, hex));
        return dot;
    }

    // ---------------------------------------------------------------- open / close

    void Open(Minimap.PinData pin)
    {
        _pin = pin;
        DrakeModsLibs.UI.DrakeGuiInput.EnsureBlocked();
        _toast.Hide();
        _remove.Cancel();
        _screen.RaiseAboveCanvases();
        _pickerPopover.Close();
        _scrim.Show(true);
        _open = true;
        ApplyOffset();
        _savedName = PinEdit.DisplayName(pin);
        _savedColour = PinEdit.Colour(pin);
        _savedType = pin.m_type;
        _savedChecked = pin.m_checked;
        _colour = _savedColour;
        _type = _savedType;
        _name.SetValue(_savedName);
        _checkedNow = _savedChecked;
        _checked.SetValue(_savedChecked);
        PaintSwatches();
        PaintChips();
        OnEdited();
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
        _remove.Cancel();
        _pickerPopover.Close();
        _pin = null;
        DrakeModsLibs.UI.DrakeGuiInput.EnsureUnblocked();
    }

    // ---------------------------------------------------------------- state

    void SetColour(string hex)
    {
        _colour = hex ?? "";
        PaintSwatches();
        OnEdited();
    }

    void SetType(Minimap.PinType type)
    {
        _type = type;
        PaintChips();
        OnEdited();
    }

    void PaintSwatches()
    {
        foreach (var (dot, hex) in _dots)
        {
            var on = string.Equals(hex, _colour, StringComparison.OrdinalIgnoreCase);
            dot.style.borderTopWidth = dot.style.borderBottomWidth = dot.style.borderLeftWidth = dot.style.borderRightWidth = on ? 2 : 1;
            var line = on ? UkTheme.Gold : UkTheme.Line;
            dot.style.borderTopColor = dot.style.borderBottomColor = dot.style.borderLeftColor = dot.style.borderRightColor = line;
        }
    }

    void PaintChips()
    {
        var editable = PinEdit.IsIconType(_savedType);
        foreach (var (chip, type) in _chips)
        {
            chip.SetOn(type == _type);
            chip.Root.SetEnabled(editable);
        }

        _iconRow.style.opacity = editable ? 1f : 0.5f;
    }

    void RevertFields()
    {
        _name.SetValue(_savedName);
        _colour = _savedColour;
        _type = _savedType;
        _checkedNow = _savedChecked;
        _checked.SetValue(_savedChecked);
        PaintSwatches();
        PaintChips();
        OnEdited();
    }

    void OnEdited()
    {
        var pin = _pin;
        if (pin == null)
            return;
        var dirty = _name.Value != _savedName || _colour != _savedColour || _type != _savedType || _checkedNow != _savedChecked;
        _name.SetChanged(_name.Value != _savedName);
        _name.SetResetVisible(_name.Value.Length > 0);
        UkControls.SetButtonEnabled(_save, dirty);
        UkControls.SetButtonEnabled(_revert, dirty);
        _subtitle.text = dirty ? "Unsaved changes" : "Placed by you";
    }

    // ---------------------------------------------------------------- actions

    void Save()
    {
        var pin = _pin;
        if (pin == null || !_save.enabledSelf)
            return;
        if (!PinEdit.CanEdit(pin))
        {
            Close();
            return;
        }

        PinEdit.Apply(pin, _name.Value, _colour, _type, _checkedNow);
        NameLibrary.Add(NameLibrary.Pin, _name.Value);
        _savedName = PinEdit.DisplayName(pin);
        _savedColour = PinEdit.Colour(pin);
        _savedType = pin.m_type;
        _savedChecked = pin.m_checked;
        OnEdited();
        _toast.Show("Saved", null, 3f);
    }

    void OnRemove()
    {
        var pin = _pin;
        if (pin == null)
            return;
        PinEdit.Remove(pin);
        Close();
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

/// <summary>"Shift + click to edit this pin", drawn beside the pointer while it is over a pin you may edit.</summary>
internal static class PinHint
{
    static UkScreen? _screen;
    static Label? _label;

    public static void Tick(string? key)
    {
        if (key == null)
        {
            _label?.Show(false);
            return;
        }

        if (_screen == null)
        {
            _screen = UkScreen.Create("drakes_pin_hint");
            _screen.RaiseAboveCanvases(5);
            _label = UkControls.Text("", 15, UkTheme.Text, wrap: false);
            _label.pickingMode = PickingMode.Ignore;
            _label.style.position = Position.Absolute;
            _label.Fill(new Color(0.06f, 0.05f, 0.04f, 0.92f)).Round(6).Pad(5, 10);
            _screen.Root.Add(_label);
        }

        var label = _label!;
        label.text = (key == "None" ? "Click" : key + " + click") + " to edit this pin";
        var width = _screen.Root.resolvedStyle.width;
        var height = _screen.Root.resolvedStyle.height;
        if (float.IsNaN(width) || width <= 0f || float.IsNaN(height) || height <= 0f)
            return;
        var pointer = (Vector2)Input.mousePosition;
        label.style.left = pointer.x / Screen.width * width + 18f;
        label.style.top = (Screen.height - pointer.y) / Screen.height * height + 18f;
        label.Show(true);
    }
}
