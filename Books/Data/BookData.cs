using System;
using System.Collections.Generic;
using System.Globalization;
using DrakeModsLibs.API;
using UnityEngine;

namespace DrakeRenameit.Books;

/// <summary>
/// What a book remembers, stored in the item's <c>m_customData</c> (same idea as <c>DrakePaper_*</c>). A placed book piece will
/// use the same keys on its ZDO so taking and placing round-trips. The title is the item's custom name (CustomizeLibsAPI), so
/// hover shows only the title.
/// </summary>
internal static class BookData
{
    internal const string KeyPages = "DrakeBook_Pages";
    internal const string KeyColor = "DrakeBook_Color";
    internal const string KeySize = "DrakeBook_Size";
    internal const string KeyCover = "DrakeBook_Cover";
    internal const string KeyReadable = "DrakeBook_Readable";
    internal const string KeySpineOnly = "DrakeBook_SpineOnly";
    internal const string KeyCopy = "DrakeBook_Copy";
    internal const string KeyTake = "DrakeBook_Take";
    internal const string KeyAuthor = "DrakeBook_Author";

    /// <summary>Pages are one string joined by this control character (never typed into a text box).</summary>
    const char PageBreak = '\u001f';
    internal const int MaxPages = 40;
    internal const int MaxPageChars = 900;
    internal const int MaxTitleChars = 40;

    /// <summary>Cover colours the editor offers (also the default pick).</summary>
    internal static readonly Color[] CoverColors =
    {
        new(0.48f, 0.16f, 0.16f), new(0.16f, 0.29f, 0.48f), new(0.16f, 0.42f, 0.23f),
        new(0.35f, 0.23f, 0.10f), new(0.35f, 0.16f, 0.42f), new(0.78f, 0.64f, 0.23f),
    };

    internal static string Get(ItemDrop.ItemData item, string key, string fallback = "") =>
        item.m_customData != null && item.m_customData.TryGetValue(key, out var v) ? v : fallback;

    internal static void Set(ItemDrop.ItemData item, string key, string? value)
    {
        if (string.IsNullOrEmpty(value))
            item.m_customData.Remove(key);
        else
            item.m_customData[key] = value!;
    }

    internal static bool GetFlag(ItemDrop.ItemData item, string key, bool fallback)
    {
        var raw = Get(item, key);
        return raw.Length == 0 ? fallback : raw == "1";
    }

    internal static void SetFlag(ItemDrop.ItemData item, string key, bool on) => Set(item, key, on ? "1" : "0");

    internal static List<string> GetPages(ItemDrop.ItemData item)
    {
        var raw = Get(item, KeyPages);
        var pages = raw.Length == 0 ? new List<string>() : new List<string>(raw.Split(PageBreak));
        if (pages.Count == 0)
            pages.Add("");
        return pages;
    }

    internal static void SetPages(ItemDrop.ItemData item, IList<string> pages)
    {
        var clean = new List<string>();
        foreach (var p in pages)
        {
            var t = (p ?? "").Replace(PageBreak.ToString(), "");
            clean.Add(t.Length > MaxPageChars ? t.Substring(0, MaxPageChars) : t);
            if (clean.Count >= MaxPages)
                break;
        }

        Set(item, KeyPages, string.Join(PageBreak.ToString(), clean));
    }

    internal static BookSize GetSize(ItemDrop.ItemData item)
    {
        if (int.TryParse(Get(item, KeySize), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) && n >= 0 && n <= 2)
            return (BookSize)n;
        return BookSize.Medium;
    }

    internal static void SetSize(ItemDrop.ItemData item, BookSize size) =>
        Set(item, KeySize, ((int)size).ToString(CultureInfo.InvariantCulture));

    internal static Color GetColor(ItemDrop.ItemData item)
    {
        var raw = Get(item, KeyColor);
        if (raw.Length > 0 && ColorUtility.TryParseHtmlString("#" + raw, out var c))
            return c;
        return CoverColors[0];
    }

    internal static void SetColor(ItemDrop.ItemData item, Color color) => Set(item, KeyColor, ColorUtility.ToHtmlStringRGB(color));

    /// <summary>Title the way hover shows it (custom name, else the item's own name).</summary>
    internal static string DisplayTitle(ItemDrop.ItemData item)
    {
        var name = CustomizeLibsAPI.GetDisplayNameForUi(item, localize: true);
        return string.IsNullOrWhiteSpace(name) ? "Untitled book" : name;
    }

    internal static void SetTitle(ItemDrop.ItemData item, string title)
    {
        var t = (title ?? "").Trim();
        if (t.Length > MaxTitleChars)
            t = t.Substring(0, MaxTitleChars);
        CustomizeLibsAPI.SetCustomName(item, t.Length == 0 ? null : t);
    }

    const char PairBreak = '\u001e';
    const char KeyBreak = '\u001d';

    /// <summary>The whole custom-data dictionary as one string, for a placed book's ZDO.</summary>
    internal static string Pack(IDictionary<string, string> data)
    {
        var parts = new List<string>();
        foreach (var kv in data)
            parts.Add(kv.Key + KeyBreak + kv.Value);
        return string.Join(PairBreak.ToString(), parts);
    }

    internal static Dictionary<string, string> Unpack(string packed)
    {
        var data = new Dictionary<string, string>();
        if (string.IsNullOrEmpty(packed))
            return data;
        foreach (var pair in packed.Split(PairBreak))
        {
            var i = pair.IndexOf(KeyBreak);
            if (i > 0)
                data[pair.Substring(0, i)] = pair.Substring(i + 1);
        }

        return data;
    }

    /// <summary>A brand-new book's data: untitled, first cover colour, medium, one empty page.</summary>
    internal static Dictionary<string, string> NewBlank()
    {
        var data = new Dictionary<string, string>
        {
            [DrakeModsLibs.Data.DrakeCustomDataKeys.Rename] = "New book",
            [KeyColor] = ColorUtility.ToHtmlStringRGB(CoverColors[0]),
            [KeySize] = ((int)BookSize.Medium).ToString(CultureInfo.InvariantCulture),
            [KeyPages] = "",
        };
        return data;
    }

    /// <summary>Readers other than the owner may open it. Default yes.</summary>
    internal static bool Readable(ItemDrop.ItemData item) => GetFlag(item, KeyReadable, true);
}
