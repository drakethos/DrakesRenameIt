using DrakeModsLibs.UI.Toolkit;
using System;
using UnityEngine.UIElements;

namespace DrakeRenameit.UI.Toolkit;

/// <summary>
/// The Workshop's settings tab. Legacy has no world-hover text and no standalone Quick Settings
/// shortcut (both removed), so this is just the preview-card toggle. Call <see cref="Sync"/>
/// whenever it is about to be shown: the value lives in config, which can change in between.
/// </summary>
internal sealed class QuickSettingsContent
{
    readonly UkSwitch _previewSwitch;

    public VisualElement Root { get; }

    /// <summary>Raised after the preview-card switch changes (the Workshop shows or hides its card).</summary>
    public Action? PreviewChanged;

    /// <param name="withHeader">A "Settings / This device only" title row. The standalone window draws its own header instead.</param>
    public QuickSettingsContent(bool withHeader)
    {
        Root = new VisualElement().Column();

        if (withHeader)
        {
            var head = new VisualElement().Row();
            head.Add(UkControls.Text("Settings", 18, UkTheme.Text, heading: true, wrap: false));
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
    }

    /// <summary>Loads the current config values into the controls.</summary>
    public void Sync()
    {
        _previewSwitch.SetValue(RenameitConfig.ShowPreviewCard);
    }
}
