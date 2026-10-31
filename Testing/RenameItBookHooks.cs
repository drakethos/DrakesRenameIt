using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DrakeRenameit.Books;
using UnityEngine;

namespace DrakeRenameit.Testing;

/// <summary>Test hooks for books, shelves and the Book hammer piece (same convention as <see cref="RenameItTestHooks"/>).</summary>
internal static class RenameItBookHooks
{
    static Player P => Player.m_localPlayer ?? throw new InvalidOperationException("no local player");

    static string S(Dictionary<string, object?> a, string key, string fallback = "") =>
        a.TryGetValue(key, out var v) && v != null ? Convert.ToString(v, CultureInfo.InvariantCulture) ?? fallback : fallback;

    static bool B(Dictionary<string, object?> a, string key, bool fallback = false)
    {
        if (!a.TryGetValue(key, out var v) || v == null)
            return fallback;
        if (v is bool b)
            return b;
        var s = Convert.ToString(v, CultureInfo.InvariantCulture) ?? "";
        return s == "1" || s.Equals("true", StringComparison.OrdinalIgnoreCase);
    }

    static ItemDrop.ItemData FirstBook() =>
        P.GetInventory().GetAllItems().FirstOrDefault(BookItem.IsBook) ?? throw new InvalidOperationException("no book in the inventory");

    /// <summary>Title, colour (0-5), size (0-2) and pages ("|" between pages) onto the first book in the inventory.</summary>
    [TestHook("renameit.bookWrite")]
    static bool Write(Dictionary<string, object?> args)
    {
        var book = FirstBook();
        BookData.SetTitle(book, S(args, "title", "Test Book"));
        BookData.SetPages(book, S(args, "pages", "One|Two|Three").Split('|'));
        BookData.SetColor(book, BookData.CoverColors[Mathf.Clamp(int.Parse(S(args, "color", "0")), 0, BookData.CoverColors.Length - 1)]);
        BookData.SetSize(book, (BookSize)Mathf.Clamp(int.Parse(S(args, "size", "1")), 0, 2));
        return true;
    }

    /// <summary>"title|pageCount|size|firstPage" of the first book in the inventory.</summary>
    [TestHook("renameit.bookInfo")]
    static string Info(Dictionary<string, object?> args)
    {
        var book = FirstBook();
        var pages = BookData.GetPages(book);
        return $"{BookData.DisplayTitle(book)}|{pages.Count}|{(int)BookData.GetSize(book)}|{pages[0]}";
    }

    /// <summary>The hover text a book item shows (title only, no description).</summary>
    [TestHook("renameit.bookTooltip")]
    static string Tooltip(Dictionary<string, object?> args)
    {
        var book = FirstBook();
        return DrakeModsLibs.API.CustomizeLibsAPI.GetProperDescription(book, "") ?? "";
    }

    /// <summary>Pick the first inventory book (or a blank one with blank:true) as the hammer piece's next book.</summary>
    [TestHook("renameit.bookPick")]
    static bool Pick(Dictionary<string, object?> args)
    {
        if (B(args, "blank"))
            BookPlaceState.ChooseBlank();
        else
            BookPlaceState.Choose(FirstBook());
        return BookPlaceState.HasChoice;
    }

    /// <summary>The packed book data on a placed Book piece (empty until it has been placed with a pick).</summary>
    [TestHook("renameit.pieceBookData")]
    static string PieceBookData(Dictionary<string, object?> args, GameObject? target = null) =>
        target != null && target.GetComponent<ZNetView>() is { } nview && nview.IsValid() ? nview.GetZDO().GetString("DrakeBookData", "") : "";

    /// <summary>How many book spines a shelf is drawing right now.</summary>
    [TestHook("renameit.shelfSpines")]
    static int Spines(Dictionary<string, object?> args, GameObject? target = null) =>
        target == null ? -1 : target.GetComponentsInChildren<BookSpineTag>(true).Length;

    [TestHook("renameit.shelfAllowsNonBooks")]
    static bool AllowsNonBooks(Dictionary<string, object?> args, GameObject? target = null) =>
        target != null && target.GetComponent<BookShelfBehaviour>() is { } shelf && shelf.AllowsNonBooks();

    [TestHook("renameit.shelfSetNonBooks")]
    static bool SetNonBooks(Dictionary<string, object?> args, GameObject? target = null)
    {
        if (target == null || target.GetComponent<BookShelfBehaviour>() is not { } shelf)
            return false;
        shelf.SetNonBooks(B(args, "on", true));
        return true;
    }

    /// <summary>Stores an item the way the container UI does (so the shelf filter applies). True when it fit.</summary>
    [TestHook("renameit.shelfTryStore")]
    static bool TryStore(Dictionary<string, object?> args, GameObject? target = null)
    {
        var container = target != null ? target.GetComponent<Container>() : null;
        if (container == null)
            return false;
        var prefab = S(args, "prefab");
        var item = P.GetInventory().GetAllItems().FirstOrDefault(i => i.m_dropPrefab != null && i.m_dropPrefab.name.Equals(prefab, StringComparison.OrdinalIgnoreCase));
        if (item == null)
            return false;
        var moved = item.Clone();
        moved.m_stack = 1;
        if (!container.GetInventory().AddItem(moved))
            return false;
        P.GetInventory().RemoveItem(item, 1);
        return true;
    }

    /// <summary>"true" when a registered prefab with this name exists (pieces and items).</summary>
    [TestHook("renameit.hasPrefab")]
    static bool HasPrefab(Dictionary<string, object?> args) =>
        ZNetScene.instance != null && ZNetScene.instance.GetPrefab(S(args, "name")) != null
        || ObjectDB.instance != null && ObjectDB.instance.GetItemPrefab(S(args, "name")) != null;
}
