using System;
using System.Collections;
using System.Collections.Generic;
using DrakeModsLibs.UI.Toolkit;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DrakeRenameit.Map;

/// <summary>
/// Editing a map pin after it is placed: Shift + click opens the pin editor (name with colours, pin tint, icon, checked, remove).
/// Vanilla only lets you type a name once, when the pin is created. The game's own members used here are private or changed in
/// Valheim 1.0, so they are reached by reflection and anything missing just turns the feature off with one log line.
/// </summary>
internal static class PinEdit
{
    /// <summary>The icons the map's pin buttons offer, in the order they appear (the game calls them Icon0..Icon4).</summary>
    internal static readonly (Minimap.PinType Type, string Label)[] Icons =
    {
        (Minimap.PinType.Icon0, "Fire"),
        (Minimap.PinType.Icon1, "House"),
        (Minimap.PinType.Icon2, "Hammer"),
        (Minimap.PinType.Icon3, "Pin"),
        (Minimap.PinType.Icon4, "Portal"),
    };

    static System.Reflection.FieldInfo? _pins;
    static System.Reflection.FieldInfo? _nameRootLarge;
    static System.Reflection.MethodInfo? _closestToCursor;
    static System.Reflection.MethodInfo? _createNamePin;
    static System.Reflection.MethodInfo? _getSprite;
    static bool _broken;

    static void Resolve()
    {
        _pins ??= AccessTools.Field(typeof(Minimap), "m_pins");
        _nameRootLarge ??= AccessTools.Field(typeof(Minimap), "m_pinNameRootLarge");
        _closestToCursor ??= AccessTools.Method(typeof(Minimap), "GetClosestPinToCursor");
        _createNamePin ??= AccessTools.Method(typeof(Minimap), "CreateMapNamePin");
        _getSprite ??= AccessTools.Method(typeof(Minimap), "GetSprite");
        if (_pins == null || _closestToCursor == null || _createNamePin == null || _getSprite == null)
        {
            _broken = true;
            RenameitConfig.Log?.LogWarning("[MapPins] The map's pin members were not found in this Valheim version; pin editing is off.");
        }
    }

    internal static bool Active => RenameitConfig.MapPinEditEnabled && !_broken && Minimap.instance != null && Minimap.IsOpen();

    internal static IEnumerable<Minimap.PinData> AllPins()
    {
        if (_pins == null)
            Resolve();
        if (_broken || Minimap.instance == null || _pins!.GetValue(Minimap.instance) is not IList pins)
            yield break;
        foreach (var pin in pins)
        {
            if (pin is Minimap.PinData data)
                yield return data;
        }
    }

    /// <summary>The pin under the mouse pointer, or null.</summary>
    internal static Minimap.PinData? PinUnderCursor()
    {
        if (_closestToCursor == null)
            Resolve();
        return _broken || Minimap.instance == null ? null : _closestToCursor!.Invoke(Minimap.instance, null) as Minimap.PinData;
    }

    /// <summary>The modifier key set for editing is held (None = a plain click).</summary>
    internal static bool ModifierHeld()
    {
        switch (RenameitConfig.MapPinEditKey)
        {
            case "Ctrl":
                return Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            case "Alt":
                return Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
            case "None":
                return true;
            default:
                return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        }
    }

    internal static bool CanEdit(Minimap.PinData pin) => pin.m_save && pin.m_ownerID == 0L;

    /// <summary>What the pin shows as its name right now, tags and all.</summary>
    internal static string DisplayName(Minimap.PinData pin) =>
        PinStyles.TryGet(pin.m_pos, (int)pin.m_type, out var style) ? PinStyles.LabelText(style, pin.m_name ?? "") : pin.m_name ?? "";

    internal static string Colour(Minimap.PinData pin) =>
        PinStyles.TryGet(pin.m_pos, (int)pin.m_type, out var style) ? style.Colour : "";

    internal static bool IsIconType(Minimap.PinType type)
    {
        foreach (var (t, _) in Icons)
        {
            if (t == type)
                return true;
        }

        return false;
    }

    /// <summary>Save an edit: the name (plain for the game's own save, styled for ours), tint, icon and checked mark.</summary>
    internal static void Apply(Minimap.PinData pin, string richName, string colour, Minimap.PinType type, bool isChecked)
    {
        if (!CanEdit(pin) || Minimap.instance == null)
            return;
        var oldType = pin.m_type;
        if (type != oldType && IsIconType(oldType) && IsIconType(type))
        {
            PinStyles.Move(pin.m_pos, (int)oldType, (int)type);
            pin.m_type = type;
            if (_getSprite?.Invoke(Minimap.instance, new object[] { type }) is Sprite sprite)
            {
                pin.m_icon = sprite;
                if (pin.m_iconElement != null)
                    pin.m_iconElement.sprite = sprite;
            }
        }

        // What the game saves is the plain name (it strips < > and $ from typed names too).
        var plain = RichModel.FromRaw(richName).Plain.Replace('$', ' ').Replace('<', ' ').Replace('>', ' ').Trim();
        pin.m_name = plain;
        pin.m_checked = isChecked;
        PinStyles.Set(pin.m_pos, (int)pin.m_type, RichText.HasTags(richName) ? richName : "", colour);
        EnsureLabel(pin);
        Refresh();
    }

    /// <summary>The pin has a name now but never had a label on the map: make one the way the game does.</summary>
    static void EnsureLabel(Minimap.PinData pin)
    {
        if (string.IsNullOrEmpty(pin.m_name) || pin.m_NamePinData != null || _createNamePin == null || _nameRootLarge == null)
            return;
        pin.m_NamePinData = new Minimap.PinNameData(pin);
        if (_nameRootLarge.GetValue(Minimap.instance) is RectTransform root)
            _createNamePin.Invoke(Minimap.instance, new object[] { pin, root });
    }

    internal static void Remove(Minimap.PinData pin)
    {
        if (!CanEdit(pin) || Minimap.instance == null)
            return;
        PinStyles.Remove(pin.m_pos, (int)pin.m_type);
        Minimap.instance.RemovePin(pin);
    }

    /// <summary>Redraw the pins soon (the game only does it when something told it to).</summary>
    internal static void Refresh()
    {
        if (Minimap.instance == null)
            return;
        AccessTools.Field(typeof(Minimap), "m_pinUpdateRequired")?.SetValue(Minimap.instance, true);
    }

    /// <summary>After the game redrew its pins: put our tint and styled names back on top.</summary>
    internal static void Restyle()
    {
        foreach (var pin in AllPins())
        {
            if (pin.m_ownerID != 0L || !PinStyles.TryGet(pin.m_pos, (int)pin.m_type, out var style))
                continue;
            var tint = PinStyles.TryParseColour(style.Colour, out var colour);
            if (tint && pin.m_iconElement != null)
                pin.m_iconElement.color = colour;
            var label = pin.m_NamePinData?.PinNameText;
            if (label == null)
                continue;
            if (tint)
                label.color = colour;
            if (style.Name.Length > 0)
            {
                var text = Localization.instance != null ? Localization.instance.Localize(style.Name) : style.Name;
                if (label.text != text)
                    label.text = text;
            }
        }
    }
}

[HarmonyPatch]
internal static class PinEditPatches
{
    /// <summary>Modifier + click on a pin: our editor instead of ticking the pin off.</summary>
    [HarmonyPatch(typeof(Minimap), nameof(Minimap.OnMapLeftClick))]
    [HarmonyPrefix]
    static bool LeftClickPrefix()
    {
        try
        {
            if (!PinEdit.Active || !PinEdit.ModifierHeld())
                return true;
            var pin = PinEdit.PinUnderCursor();
            if (pin == null)
                return true;
            if (!PinEdit.CanEdit(pin))
            {
                DrakeModsLibs.UI.DrakeMessage.Show("Pins shared by other players can't be edited.");
                return false;
            }

            UI.Toolkit.PinEditorPanel.Show(pin);
            return false;
        }
        catch (Exception ex)
        {
            RenameitConfig.Log?.LogWarning($"[MapPins] Pin editor failed: {ex.Message}");
            return true;
        }
    }

    [HarmonyPatch(typeof(Minimap), "UpdatePins")]
    [HarmonyPostfix]
    static void UpdatePinsPostfix()
    {
        try
        {
            PinEdit.Restyle();
        }
        catch (Exception ex)
        {
            RenameitConfig.Log?.LogWarning($"[MapPins] Restyle failed: {ex.Message}");
        }
    }

    /// <summary>The hint that follows the pointer while it is over a pin you may edit.</summary>
    [HarmonyPatch(typeof(Minimap), "Update")]
    [HarmonyPostfix]
    static void UpdatePostfix()
    {
        try
        {
            var show = PinEdit.Active && !UI.Toolkit.PinEditorPanel.IsOpen && !PinEdit.ModifierHeld() ? PinEdit.PinUnderCursor() : null;
            UI.Toolkit.PinHint.Tick(show != null && PinEdit.CanEdit(show) ? RenameitConfig.MapPinEditKey : null);
        }
        catch (Exception)
        {
            UI.Toolkit.PinHint.Tick(null);
        }
    }
}
