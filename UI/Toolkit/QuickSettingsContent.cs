using DrakeModsLibs.UI.Toolkit;
using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace DrakeRenameit.UI.Toolkit;

/// <summary>
/// The quick settings controls (preview card, world hover text, shortcut hint), all per-player. One piece of UI shared by the
/// Workshop's cog menu and the standalone Quick Settings window, so they can never drift apart. Call <see cref="Sync"/> whenever it is
/// about to be shown: the values live in config, which can change in between.
/// </summary>
internal sealed class QuickSettingsContent
{
    static readonly string[] HoverStyles = { "Vanilla", "Minimal", "Detailed" };
    static readonly string[] HoverSizes = { "S", "M", "L" };
    static readonly string[] HoverStrengths = { "Light", "Normal", "Bold" };
    static readonly string[] HoverPositions = { "Under", "Beside" };

    readonly UkSwitch _previewSwitch;
    readonly UkSegmented _hoverStyle;
    readonly UkSegmented _hoverSize;
    readonly UkSegmented _hoverStrength;
    readonly UkSegmented _hoverPosition;
    readonly UkChip _chipKey;
    readonly UkChip _chipCrafter;
    readonly UkChip _chipDescription;
    readonly UkChip _chipBackdrop;
    readonly Label _shortcutKey;
    readonly Button _shortcutChange;
    readonly Label _shortcutHint;
    string? _captureStatus;

    public VisualElement Root { get; }

    /// <summary>Raised after the preview-card switch changes (the Workshop shows or hides its card).</summary>
    public Action? PreviewChanged;

    /// <param name="withHeader">A "Quick settings / This device only" title row. The standalone window draws its own header instead.</param>
    public QuickSettingsContent(bool withHeader)
    {
        Root = new VisualElement().Column();

        if (withHeader)
        {
            var head = new VisualElement().Row();
            head.Add(UkControls.Text("Quick settings", 18, UkTheme.Text, heading: true, wrap: false));
            head.Add(UkControls.Spacer());
            head.Add(UkControls.Text("This device only", 12, UkTheme.TextMuted, wrap: false));
            Root.Add(head);
        }

        _previewSwitch = new UkSwitch("Preview card", "A floating card beside the item window that shows your item as you edit.");
        _previewSwitch.Root.style.marginTop = withHeader ? 12 : 0;
        _previewSwitch.Changed = on =>
        {
            RenameitConfig.ShowPreviewCard = on;
            PreviewChanged?.Invoke();
        };
        Root.Add(_previewSwitch.Root);

        // World hover text: what you see looking at an item on the ground or on a stand.
        var hoverTitle = UkControls.Text("HOVER TEXT", 12, UkTheme.TextMuted, wrap: false);
        hoverTitle.style.marginTop = 16;
        Root.Add(hoverTitle);
        var hoverNote = UkControls.Text("What you see looking at an item on the ground or on a stand.", 13, UkTheme.TextMuted);
        hoverNote.style.marginTop = 2;
        hoverNote.style.marginBottom = 8;
        Root.Add(hoverNote);

        _hoverStyle = new UkSegmented(HoverStyles);
        _hoverStyle.Changed = i => RenameitConfig.HoverStyle = HoverStyles[i];
        Root.Add(_hoverStyle.Root);

        var chips = new VisualElement().Row(Align.FlexStart);
        chips.style.flexWrap = Wrap.Wrap;
        chips.style.marginTop = 10;
        _chipKey = new UkChip("Key hint");
        _chipKey.Changed = on => RenameitConfig.HoverKeyHint = on;
        _chipCrafter = new UkChip("Crafter");
        _chipCrafter.Changed = on => RenameitConfig.HoverCrafter = on;
        _chipDescription = new UkChip("Description");
        _chipDescription.Changed = on => RenameitConfig.HoverDescription = on;
        _chipBackdrop = new UkChip("Backdrop");
        _chipBackdrop.Changed = on => RenameitConfig.HoverBackdrop = on;
        chips.Add(_chipKey.Root);
        chips.Add(_chipCrafter.Root);
        chips.Add(_chipDescription.Root);
        chips.Add(_chipBackdrop.Root);
        Root.Add(chips);

        _hoverSize = new UkSegmented(HoverSizes);
        _hoverSize.Changed = i => RenameitConfig.HoverSize = HoverSizes[i];
        _hoverStrength = new UkSegmented(HoverStrengths);
        _hoverStrength.Changed = i => RenameitConfig.HoverStrength = HoverStrengths[i];
        _hoverPosition = new UkSegmented("Under crosshair", "Beside it");
        _hoverPosition.Changed = i => RenameitConfig.HoverPosition = HoverPositions[i];

        var twin = new VisualElement().Row(Align.FlexStart);
        twin.style.marginTop = 6;
        twin.Add(Captioned("SIZE", _hoverSize.Root, 0, 8));
        twin.Add(Captioned("STRENGTH", _hoverStrength.Root, 8, 0));
        Root.Add(twin);
        var positionBlock = Captioned("POSITION", _hoverPosition.Root, 0, 0);
        positionBlock.style.marginTop = 10;
        Root.Add(positionBlock);

        var shortcutTitle = UkControls.Text("SHORTCUT", 12, UkTheme.TextMuted, wrap: false);
        shortcutTitle.style.marginTop = 16;
        Root.Add(shortcutTitle);
        var shortcutRow = new VisualElement().Row();
        shortcutRow.style.marginTop = 6;
        shortcutRow.Add(UkControls.Text("Open Quick Settings", 15, UkTheme.Text, wrap: false));
        shortcutRow.Add(UkControls.Spacer());
        _shortcutKey = UkControls.Text("", 14, UkTheme.Text, wrap: false);
        _shortcutKey.style.marginRight = 8;
        _shortcutKey.Border(UkTheme.Line).Round(6).Pad(4, 10);
        shortcutRow.Add(_shortcutKey);
        _shortcutChange = UkControls.MakeButton("Change", OnChangeShortcut, UkButtonKind.Secondary);
        _shortcutChange.style.height = 38;
        shortcutRow.Add(_shortcutChange);
        Root.Add(shortcutRow);
        _shortcutHint = UkControls.Text("", 13, UkTheme.TextMuted);
        _shortcutHint.style.marginTop = 6;
        Root.Add(_shortcutHint);
    }

    /// <summary>Loads the current config values into the controls.</summary>
    public void Sync()
    {
        _previewSwitch.SetValue(RenameitConfig.ShowPreviewCard);
        _hoverStyle.SetIndex(Mathf.Max(0, Array.IndexOf(HoverStyles, RenameitConfig.HoverStyle)));
        _hoverSize.SetIndex(Mathf.Max(0, Array.IndexOf(HoverSizes, RenameitConfig.HoverSize)));
        _hoverStrength.SetIndex(Mathf.Max(0, Array.IndexOf(HoverStrengths, RenameitConfig.HoverStrength)));
        _hoverPosition.SetIndex(Mathf.Max(0, Array.IndexOf(HoverPositions, RenameitConfig.HoverPosition)));
        _chipKey.SetOn(RenameitConfig.HoverKeyHint);
        _chipCrafter.SetOn(RenameitConfig.HoverCrafter);
        _chipDescription.SetOn(RenameitConfig.HoverDescription);
        _chipBackdrop.SetOn(RenameitConfig.HoverBackdrop);

        RefreshShortcut();
    }

    void OnChangeShortcut()
    {
        QuickSettingsShortcut.BeginCapture(status =>
        {
            _captureStatus = status;
            RefreshShortcut();
        });
    }

    void RefreshShortcut()
    {
        var binding = RenameitConfig.QuickSettingsKey;
        _shortcutKey.text = QuickSettingsShortcut.Capturing
            ? "..."
            : string.IsNullOrWhiteSpace(binding) ? "Off" : global::DrakeRenameit.MenuKeyBinding.FormatForDisplay(binding);
        _shortcutHint.text = !string.IsNullOrEmpty(_captureStatus)
            ? _captureStatus!
            : "Works anywhere you're not looking at an item, and while standing still. Press Change, then the keys you want.";
    }

    /// <summary>A small caption above a control, as one column that shares a row evenly.</summary>
    static VisualElement Captioned(string caption, VisualElement control, float marginLeft, float marginRight)
    {
        var column = new VisualElement().Column().Grow();
        column.style.marginLeft = marginLeft;
        column.style.marginRight = marginRight;
        var label = UkControls.Text(caption, 12, UkTheme.TextMuted, wrap: false);
        label.style.marginBottom = 4;
        column.Add(label);
        column.Add(control);
        return column;
    }
}
