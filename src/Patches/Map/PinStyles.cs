using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using DrakeModsLibs.UI.Toolkit;
using UnityEngine;

namespace DrakeRenameit.Map;

/// <summary>
/// What the map saves about a pin is its name, place, icon and checked mark; there is no colour and no room for a styled name
/// (the game strips tags from typed names). This keeps the rest on the character, per world: a coloured / formatted name and a
/// tint for the pin. Looked up by place and icon, applied after every pin refresh (see <see cref="PinEdit"/>).
/// </summary>
internal static class PinStyles
{
    internal readonly struct Style
    {
        public Style(string name, string colour)
        {
            Name = name;
            Colour = colour;
        }

        /// <summary>The name with its rich text tags (may be empty: then the pin's own name is used).</summary>
        public string Name { get; }

        /// <summary>The tint as <c>#rrggbb</c>, or empty for the normal look.</summary>
        public string Colour { get; }
    }

    const string KeyPrefix = "DrakePins_";
    const int MaxEntries = 400;

    static readonly Dictionary<string, Style> Map = new();
    static string? _loadedFor;

    static string StorageKey => KeyPrefix + (ZNet.instance != null ? ZNet.instance.GetWorldName() : "");

    /// <summary>The same pin always gets the same key: place to a tenth of a metre, and its icon.</summary>
    internal static string KeyOf(Vector3 pos, int type) =>
        Mathf.RoundToInt(pos.x * 10f).ToString(CultureInfo.InvariantCulture) + "," +
        Mathf.RoundToInt(pos.z * 10f).ToString(CultureInfo.InvariantCulture) + "," + type.ToString(CultureInfo.InvariantCulture);

    static void EnsureLoaded()
    {
        var player = Player.m_localPlayer;
        if (player == null)
            return;
        var storage = StorageKey;
        if (_loadedFor == storage)
            return;
        _loadedFor = storage;
        Map.Clear();
        if (player.m_customData == null || !player.m_customData.TryGetValue(storage, out var text) || string.IsNullOrEmpty(text))
            return;
        foreach (var line in text.Split(';'))
        {
            var parts = line.Split(new[] { '|' }, 3);
            if (parts.Length < 3 || parts[0].Length == 0)
                continue;
            Map[parts[0]] = new Style(parts[2], parts[1]);
        }
    }

    internal static bool TryGet(Vector3 pos, int type, out Style style)
    {
        EnsureLoaded();
        return Map.TryGetValue(KeyOf(pos, type), out style);
    }

    /// <summary>Remember a pin's styling; a pin with neither name styling nor colour is forgotten.</summary>
    internal static void Set(Vector3 pos, int type, string? richName, string? colour)
    {
        EnsureLoaded();
        var key = KeyOf(pos, type);
        var name = Clean(richName);
        var tint = (colour ?? "").Trim();
        if (name.Length == 0 && tint.Length == 0)
            Map.Remove(key);
        else
            Map[key] = new Style(name, tint);
        Save();
    }

    internal static void Remove(Vector3 pos, int type)
    {
        EnsureLoaded();
        if (Map.Remove(KeyOf(pos, type)))
            Save();
    }

    /// <summary>A pin changed icon: its styling follows it.</summary>
    internal static void Move(Vector3 pos, int fromType, int toType)
    {
        EnsureLoaded();
        if (fromType == toType || !Map.TryGetValue(KeyOf(pos, fromType), out var style))
            return;
        Map.Remove(KeyOf(pos, fromType));
        Map[KeyOf(pos, toType)] = style;
        Save();
    }

    static string Clean(string? raw) => (raw ?? "").Replace(';', ' ').Replace('|', ' ').Trim();

    static void Save()
    {
        var player = Player.m_localPlayer;
        if (player == null || player.m_customData == null)
            return;
        var sb = new StringBuilder();
        var count = 0;
        foreach (var pair in Map)
        {
            if (count++ >= MaxEntries)
                break;
            if (sb.Length > 0)
                sb.Append(';');
            sb.Append(pair.Key).Append('|').Append(pair.Value.Colour).Append('|').Append(pair.Value.Name);
        }

        var storage = StorageKey;
        if (sb.Length == 0)
            player.m_customData.Remove(storage);
        else
            player.m_customData[storage] = sb.ToString();
    }

    /// <summary>The label text for a pin: the styled name when one is saved, else vanilla's plain one.</summary>
    internal static string LabelText(Style style, string plainName) => style.Name.Length > 0 ? style.Name : plainName;

    internal static bool TryParseColour(string? hex, out Color colour)
    {
        colour = Color.white;
        return !string.IsNullOrEmpty(hex) && UkColorPicker.TryParse(hex, out colour);
    }
}
