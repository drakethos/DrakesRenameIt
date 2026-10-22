using DrakeModsLibs.UI.Toolkit;
using UnityEngine;
using UnityEngine.UIElements;

namespace DrakeRenameit.UI.Toolkit;

/// <summary>
/// The floating preview beside the Workshop window: your item as it will read in its tooltip, updated on every keystroke.
/// Name and description are rich text, so colour / size / bold tags show up as they will in game.
/// </summary>
internal sealed class WorkshopPreview
{
    readonly VisualElement _iconSlot;
    readonly Label _name;
    readonly Label _subtitle;
    readonly Label _desc;
    readonly Label _crafted;

    public VisualElement Root { get; }

    public WorkshopPreview()
    {
        Root = new VisualElement().Column().Fill(UkTheme.Panel).Round(UkTheme.RadiusLarge - 2).Border(UkTheme.Line);
        Root.style.width = 360;
        Root.style.overflow = Overflow.Hidden;
        // The card is for looking, not clicking: let pointer events pass through to whatever is behind it.
        Root.pickingMode = PickingMode.Ignore;

        var header = new VisualElement().Row().Pad(12, 14).BorderBottom(UkTheme.LineSoft);
        _iconSlot = new VisualElement { pickingMode = PickingMode.Ignore }.Fixed();
        header.Add(_iconSlot);
        var titles = new VisualElement { pickingMode = PickingMode.Ignore }.Column().Grow();
        titles.style.marginLeft = 12;
        _name = UkControls.Text("", 21, UkTheme.Gold, heading: true);
        _subtitle = UkControls.Text("", 13, UkTheme.TextMuted, wrap: false);
        titles.Add(_name);
        titles.Add(_subtitle);
        header.Add(titles);
        Root.Add(header);

        var body = new VisualElement { pickingMode = PickingMode.Ignore }.Column().Pad(12, 14);
        _desc = UkControls.Text("", 15, UkTheme.TextBody);
        body.Add(_desc);
        _crafted = UkControls.Text("", 14, UkTheme.Text);
        _crafted.style.marginTop = 10;
        body.Add(_crafted);
        var note = UkControls.Text("Preview · updates as you type", 12, UkTheme.TextMuted, wrap: false);
        note.style.marginTop = 10;
        body.Add(note);
        Root.Add(body);
    }

    /// <summary>Icon and the item's original name (the line under the title). Called when the panel opens or the item changes.</summary>
    public void SetItem(Sprite? icon, string originalName)
    {
        _iconSlot.Clear();
        _iconSlot.Add(UkControls.IconTile(icon, 48));
        _subtitle.text = originalName;
    }

    /// <summary>
    /// What the tooltip would show right now. <paramref name="name"/> and <paramref name="description"/> may contain rich text tags;
    /// the crafted line is "<paramref name="label"/>: <paramref name="crafter"/>" like the real tooltip.
    /// </summary>
    public void Update(string name, string description, string label, string crafter)
    {
        _name.text = name;
        _desc.text = description;
        _desc.Show(!string.IsNullOrEmpty(description));
        var hasCrafter = !string.IsNullOrEmpty(crafter);
        _crafted.Show(hasCrafter);
        if (hasCrafter)
            _crafted.text = (string.IsNullOrEmpty(label) ? "" : label + ": ") + "<b>" + crafter + "</b>";
    }
}
