using DrakeModsLibs.UI.Toolkit;
using System;
using System.Text.RegularExpressions;
using DrakeRenameit.Patches;
using UnityEngine;
using UnityEngine.UIElements;

namespace DrakeRenameit.UI.Toolkit;

/// <summary>
/// The world hover text (looking at an item on the ground or on a stand), drawn quietly: no panel, just clean text with a soft
/// shadow, fading in and out, optionally on a light see-through backdrop. Vanilla builds the string (other mods change it too),
/// so this reads that final string, blanks vanilla's copy, and draws it in the chosen style. "Vanilla" leaves everything alone.
/// All of it is per-player settings (see <c>RenameitConfig.Hover*</c>).
/// </summary>
internal static class WorldHover
{
    /// <summary>A bracketed key group like <c>[E]</c> or <c>[&lt;color=yellow&gt;&lt;b&gt;Shift+E&lt;/b&gt;&lt;/color&gt;]</c>; group 1 is the inside, tags and all.</summary>
    static readonly Regex Bracket = new(@"\[((?:<[^>]*>|[^\]<])+)\]", RegexOptions.Compiled);

    static readonly Regex Tags = new(@"<[^>]*>", RegexOptions.Compiled);

    static readonly Color Soft = new(0.94f, 0.90f, 0.82f, 0.78f);
    static readonly Color Chip = new(0.94f, 0.90f, 0.82f, 0.5f);

    static UkScreen? _screen;
    static VisualElement? _box;
    static float _alpha;
    static string _signature = "";
    static string _shownText = "";
    static float _sortCheckedAt = -100f;
    static bool _failed;
    static GameObject? _lastHover;
    static string _lastHoverText = "";
    static ItemDrop.ItemData? _hoverItem;

    /// <summary>Called every frame after the HUD updated its crosshair and hover text.</summary>
    public static void Tick(Hud hud, Player? player)
    {
        if (_failed)
            return;
        try
        {
            TickInner(hud, player);
        }
        catch (Exception ex)
        {
            // Never spam an error per frame or leave the player without hover text: give up and let vanilla draw it.
            _failed = true;
            RenameitConfig.Log?.LogError($"[Hover] Disabled after an error, vanilla hover text is back: {ex}");
        }
    }

    static void TickInner(Hud hud, Player? player)
    {
        var style = RenameitConfig.HoverStyle;
        var vanillaText = hud.m_hoverName;
        if (style == "Vanilla" || vanillaText == null)
        {
            if (_box != null)
            {
                _alpha = 0f;
                _box.Show(false);
            }

            return;
        }

        EnsureCreated();
        var text = vanillaText.text ?? "";
        var target = 0f;
        if (!string.IsNullOrEmpty(text))
        {
            // Take over: vanilla's copy is blanked so only ours shows.
            vanillaText.text = "";
            _shownText = text;
            target = 1f;
        }

        _alpha = Mathf.MoveTowards(_alpha, target, Time.unscaledDeltaTime * 9f);
        if (_alpha <= 0.001f && target <= 0f)
        {
            _box!.Show(false);
            return;
        }

        if (Time.unscaledTime - _sortCheckedAt > 5f)
        {
            // New canvases appear over time (Jotunn, other mods); stay just above them.
            _sortCheckedAt = Time.unscaledTime;
            _screen!.RaiseAboveCanvases(2);
        }

        ResolveHoveredItem(player, style);

        var signature = style + "|" + _shownText + "|" + RenameitConfig.HoverKeyHint + RenameitConfig.HoverCrafter +
                        RenameitConfig.HoverDescription + RenameitConfig.HoverBackdrop + "|" + RenameitConfig.HoverSize +
                        "|" + RenameitConfig.HoverPosition + "|" + (_hoverItem != null ? _hoverItem.m_customData?.Count ?? 0 : -1);
        if (signature != _signature)
        {
            _signature = signature;
            Rebuild(style);
        }

        Place();
    }

    static void EnsureCreated()
    {
        if (_screen != null)
            return;
        _screen = UkScreen.Create("drakes_world_hover");
        _screen.RaiseAboveCanvases(2);
        _box = new VisualElement { pickingMode = PickingMode.Ignore };
        _box.style.position = Position.Absolute;
        _box.style.flexDirection = FlexDirection.Column;
        _box.Show(false);
        _screen.Root.Add(_box);
    }

    /// <summary>The item being looked at, for the description and crafter lines (Detailed only). Re-read when the target or its text changes.</summary>
    static void ResolveHoveredItem(Player? player, string style)
    {
        if (style != "Detailed" || player == null || !(RenameitConfig.HoverCrafter || RenameitConfig.HoverDescription))
        {
            _hoverItem = null;
            _lastHover = null;
            return;
        }

        // GetHoverObject is public in the real game; the m_hovering field it reads is not.
        var hovered = player.GetHoverObject();
        if (hovered == _lastHover && _shownText == _lastHoverText)
            return;
        _lastHover = hovered;
        _lastHoverText = _shownText;
        _hoverItem = null;
        if (hovered == null)
            return;

        var drop = hovered.GetComponentInParent<ItemDrop>();
        if (drop != null)
        {
            _hoverItem = drop.m_itemData;
            return;
        }

        var stand = hovered.GetComponentInParent<ItemStand>();
        if (stand != null)
            _hoverItem = PaperItemStandPatches.TryLoadAttached(stand);
    }

    static void Rebuild(string style)
    {
        var box = _box!;
        box.Clear();

        var nameSize = RenameitConfig.HoverSize switch { "S" => 20, "L" => 31, _ => 25 };
        var subSize = Mathf.RoundToInt(nameSize * 0.72f);
        var under = RenameitConfig.HoverPosition == "Under";
        var align = under ? TextAnchor.UpperCenter : TextAnchor.UpperLeft;
        box.style.alignItems = under ? Align.Center : Align.FlexStart;

        // Backdrop: a light see-through dark box. Off = just text.
        if (RenameitConfig.HoverBackdrop)
        {
            box.style.backgroundColor = new Color(0f, 0f, 0f, 0.42f);
            box.style.paddingTop = 6;
            box.style.paddingBottom = 8;
            box.style.paddingLeft = box.style.paddingRight = 14;
            box.Round(10);
        }
        else
        {
            box.style.backgroundColor = Color.clear;
            box.style.paddingTop = box.style.paddingBottom = box.style.paddingLeft = box.style.paddingRight = 0;
            box.Round(0);
        }

        var lines = _shownText.Split('\n');
        var name = TooltipRichText.EnsureRichTextTagsClosedForTooltip(lines[0].Trim());
        box.Add(Shadowed(UkControls.Text(name, nameSize, UkTheme.Gold, heading: true, wrap: false), align));

        if (style == "Detailed")
            AddDetails(box, subSize, align);

        for (var i = 1; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (line.Length == 0)
                continue;

            // Key hints from vanilla and from other mods ("[E] Use", "[Shift+E] Take", two on one line...) all become chips.
            var hints = ParseHints(line, out var before);
            if (hints != null)
            {
                if (before.Length > 0)
                    box.Add(Info(before, subSize, align));
                if (RenameitConfig.HoverKeyHint)
                    box.Add(HintsRow(hints, subSize, under));
                continue;
            }

            // Anything else vanilla or another mod added (stand label, "no access", page text): kept, just quieter.
            box.Add(Info(TooltipRichText.EnsureRichTextTagsClosedForTooltip(line), subSize, align));
        }
    }

    static Label Info(string text, int size, TextAnchor align)
    {
        var info = Shadowed(UkControls.Text(text, size, Soft, wrap: true), align);
        info.style.maxWidth = 420;
        return info;
    }

    /// <summary>
    /// Finds the key hints on a line: every <c>[key]</c> group that looks like a key (<c>E</c>, <c>Shift+E</c>, <c>F1</c>), each with the
    /// words after it up to the next one. Null when the line has none. Text before the first hint comes back in <paramref name="before"/>.
    /// </summary>
    static System.Collections.Generic.List<(string Key, string Action)>? ParseHints(string line, out string before)
    {
        before = "";
        var found = new System.Collections.Generic.List<(int Start, int End, string Key)>();
        foreach (Match match in Bracket.Matches(line))
        {
            var key = Tags.Replace(match.Groups[1].Value, "").Trim();
            if (LooksLikeKey(key))
                found.Add((match.Index, match.Index + match.Length, key));
        }

        if (found.Count == 0)
            return null;

        before = Tags.Replace(line.Substring(0, found[0].Start), "").Trim();
        var hints = new System.Collections.Generic.List<(string, string)>();
        for (var i = 0; i < found.Count; i++)
        {
            var end = found[i].End;
            var next = i + 1 < found.Count ? found[i + 1].Start : line.Length;
            var action = Tags.Replace(line.Substring(end, next - end), "").Trim().Trim('|', '/', ',', '-', ':').Trim();
            hints.Add((found[i].Key, action));
        }

        return hints;
    }

    /// <summary>A short key name or a chord of up to four of them: <c>E</c>, <c>Shift+E</c>, <c>Left Shift + F1</c> is too wordy and is left as text.</summary>
    static bool LooksLikeKey(string key)
    {
        if (key.Length == 0 || key.Length > 28)
            return false;
        var parts = key.Split('+');
        if (parts.Length > 4)
            return false;
        foreach (var part in parts)
        {
            var p = part.Trim();
            if (p.Length == 0 || p.Length > 12 || p.IndexOf(' ') >= 0)
                return false;
        }

        return true;
    }

    /// <summary>"Pick up [E]   Take [Shift][+][E]": each action, then its key as outlined chips; several hints sit side by side.</summary>
    static VisualElement HintsRow(System.Collections.Generic.List<(string Key, string Action)> hints, int size, bool centered)
    {
        var row = new VisualElement { pickingMode = PickingMode.Ignore }.Row();
        row.style.marginTop = 3;
        row.style.justifyContent = centered ? Justify.Center : Justify.FlexStart;
        row.style.flexWrap = Wrap.Wrap;
        for (var i = 0; i < hints.Count; i++)
        {
            var (key, action) = hints[i];
            if (!string.IsNullOrEmpty(action))
            {
                var label = Shadowed(UkControls.Text(action, size, Soft, wrap: false), TextAnchor.MiddleLeft);
                label.style.marginRight = 8;
                if (i > 0)
                    label.style.marginLeft = 14;
                row.Add(label);
            }
            else if (i > 0)
            {
                row.Add(new VisualElement { pickingMode = PickingMode.Ignore }.Size(14, 1));
            }

            var parts = key.Split('+');
            for (var p = 0; p < parts.Length; p++)
            {
                if (p > 0)
                {
                    var plus = Shadowed(UkControls.Text("+", size, Soft, wrap: false), TextAnchor.MiddleCenter);
                    plus.style.marginLeft = plus.style.marginRight = 3;
                    row.Add(plus);
                }

                var chip = Shadowed(UkControls.Text(parts[p].Trim(), Mathf.RoundToInt(size * 0.9f), UkTheme.Text, wrap: false), TextAnchor.MiddleCenter);
                chip.Border(Chip).Round(4).Pad(0, 6);
                row.Add(chip);
            }
        }

        return row;
    }

    static void AddDetails(VisualElement box, int subSize, TextAnchor align)
    {
        var item = _hoverItem;
        if (item == null)
            return;

        if (RenameitConfig.HoverDescription)
        {
            var description = Loc(DrakeRenameit.getPropperDesc(item));
            description = TooltipRichText.EnsureRichTextTagsClosedForTooltip(Shorten(description, 110));
            if (!string.IsNullOrEmpty(description))
            {
                var label = Shadowed(UkControls.Text(description, subSize, new Color(0.94f, 0.90f, 0.82f, 0.9f), wrap: true), align);
                label.style.maxWidth = 360;
                label.style.marginTop = 2;
                box.Add(label);
            }
        }

        if (RenameitConfig.HoverCrafter)
        {
            var crafter = DrakeRenameit.getCraftedByDisplay(item);
            if (!string.IsNullOrEmpty(crafter))
            {
                var caption = DrakeRenameit.GetStoredCraftedByLineLabel(item);
                if (string.IsNullOrEmpty(caption))
                    caption = Loc("$item_crafter");
                var label = Shadowed(UkControls.Text((string.IsNullOrEmpty(caption) ? "" : caption + ": ") + crafter, subSize, Soft, wrap: false), align);
                label.style.marginTop = 2;
                box.Add(label);
            }
        }
    }

    static Label Shadowed(Label label, TextAnchor align)
    {
        label.style.unityTextAlign = align;
        label.style.textShadow = new TextShadow { offset = new Vector2(0f, 1f), blurRadius = 3f, color = new Color(0f, 0f, 0f, 0.95f) };
        return label;
    }

    /// <summary>Sits at the crosshair (screen centre): under it, or beside it. Fades with the shown/hidden state.</summary>
    static void Place()
    {
        var box = _box!;
        var root = _screen!.Root;
        var width = root.resolvedStyle.width;
        var height = root.resolvedStyle.height;
        if (float.IsNaN(width) || width <= 0f || float.IsNaN(height))
            return;

        var strength = RenameitConfig.HoverStrength switch { "Light" => 0.6f, "Bold" => 1f, _ => 0.88f };
        box.style.opacity = _alpha * strength;
        box.Show(true);

        if (RenameitConfig.HoverPosition == "Under")
        {
            box.style.left = width * 0.5f;
            box.style.top = height * 0.5f + 30f;
            box.style.translate = new Translate(Length.Percent(-50f), 0f);
        }
        else
        {
            box.style.left = width * 0.5f + 34f;
            box.style.top = height * 0.5f;
            box.style.translate = new Translate(0f, Length.Percent(-50f));
        }
    }

    static string Loc(string? text) =>
        string.IsNullOrEmpty(text) ? "" : Localization.instance != null ? Localization.instance.Localize(text) : text!;

    /// <summary>Cuts at a word boundary near <paramref name="max"/> characters (plain text length; tags are ignored by the cut point finder only roughly).</summary>
    static string Shorten(string text, int max)
    {
        if (RichText.VisibleLength(text) <= max)
            return text;
        var cut = RichText.Truncate(text, max, 0);
        var space = cut.LastIndexOf(' ');
        if (space > max / 2 && !cut.Contains("<"))
            cut = cut.Substring(0, space);
        return cut + "...";
    }
}
