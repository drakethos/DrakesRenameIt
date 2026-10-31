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
        BookData.Set(book, BookData.KeyCover, S(args, "cover"));
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
        BookPlacementPatches.KeepPick(10f);
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

    /// <summary>Opens the reader on the first inventory book as its maker: tab "pages" or "book", edit true/false.</summary>
    [TestHook("renameit.bookOpenReader")]
    static bool OpenReader(Dictionary<string, object?> args)
    {
        BookReaderPanel.Show(FirstBook(), canEdit: true);
        BookReaderPanel.TestSelect(S(args, "tab", "pages") == "book", B(args, "edit"));
        return BookReaderPanel.IsOpen;
    }

    /// <summary>Closes the reader and the picker (the test step closeUi only closes the game's own windows).</summary>
    [TestHook("renameit.closeBookWindows")]
    static bool CloseWindows(Dictionary<string, object?> args)
    {
        BookReaderPanel.CloseWindow();
        BookPickerPanel.CloseWindow();
        ShelfSettingsPanel.CloseWindow();
        PaperReaderPanel.CloseWindow();
        return true;
    }

    [TestHook("renameit.bookOpenPicker")]
    static bool OpenPicker(Dictionary<string, object?> args)
    {
        BookPickerPanel.Show(P);
        return BookPickerPanel.IsOpen;
    }

    /// <summary>How far the placed book's collider floats above the piece origin (a ground-touching collider blocks placement).</summary>
    [TestHook("renameit.pieceColliderLift")]
    static float ColliderLift(Dictionary<string, object?> args, GameObject? target = null)
    {
        var box = target != null ? target.GetComponent<BoxCollider>() : null;
        return box == null ? -1f : box.center.y - box.size.y / 2f;
    }

    /// <summary>The hover text a placed book shows (its title and Read), without needing to aim at it.</summary>
    [TestHook("renameit.bookHoverText")]
    static string BookHoverText(Dictionary<string, object?> args, GameObject? target = null) =>
        target != null && target.GetComponent<BookPieceBehaviour>() is { } book ? book.GetHoverText() : "";

    /// <summary>Sets a shelf row's mode: row (0 = top), mode books / slots / free.</summary>
    [TestHook("renameit.shelfSetMode")]
    static bool SetMode(Dictionary<string, object?> args, GameObject? target = null)
    {
        if (target == null || target.GetComponent<BookShelfBehaviour>() is not { } shelf)
            return false;
        var mode = S(args, "mode", "books").ToLowerInvariant() switch { "slots" => ShelfRowMode.Slots, "free" => ShelfRowMode.Free, _ => ShelfRowMode.Books };
        shelf.SetMode(int.Parse(S(args, "row", "0")), mode);
        return true;
    }

    /// <summary>Gives the nth item of a shelf row a Free pose: x (metres from the middle), lift, yaw, scale.</summary>
    [TestHook("renameit.shelfSetPose")]
    static bool SetPose(Dictionary<string, object?> args, GameObject? target = null)
    {
        if (target == null || target.GetComponent<BookShelfBehaviour>() is not { } shelf)
            return false;
        var items = shelf.RowItems(int.Parse(S(args, "row", "0")));
        var item = items[int.Parse(S(args, "index", "0"))];
        float F(string key, float fallback) => float.Parse(S(args, key, fallback.ToString(CultureInfo.InvariantCulture)), CultureInfo.InvariantCulture);
        shelf.SetPose(item, new ShelfPose { X = F("x", 0f), Lift = F("lift", 0f), Yaw = F("yaw", 0f), Scale = F("scale", 1f) });
        return true;
    }

    /// <summary>The drawn items' local X, left to right, as "x1,x2,..." (3 decimals): what each row mode produced.</summary>
    [TestHook("renameit.shelfLayout")]
    static string Layout(Dictionary<string, object?> args, GameObject? target = null)
    {
        var holder = target != null ? target.transform.Find(BookShelfPiece.FrameName + "/drake_shelf_items") : null;
        if (holder == null)
            return "";
        var xs = new List<float>();
        foreach (Transform child in holder)
            xs.Add(child.localPosition.x);
        xs.Sort();
        return string.Join(",", xs.ConvertAll(x => x.ToString("0.000", CultureInfo.InvariantCulture)));
    }

    /// <summary>Opens the shelf settings window on a shelf, for pictures.</summary>
    [TestHook("renameit.shelfOpenSettings")]
    static bool OpenSettings(Dictionary<string, object?> args, GameObject? target = null)
    {
        if (target == null || target.GetComponent<BookShelfBehaviour>() is not { } shelf)
            return false;
        ShelfSettingsPanel.Show(shelf, P);
        return true;
    }

    /// <summary>What a shelf's container holds: "prefab@x,y;prefab@x,y".</summary>
    [TestHook("renameit.shelfItems")]
    static string ShelfItems(Dictionary<string, object?> args, GameObject? target = null)
    {
        if (target == null || target.GetComponent<Container>() is not { } c)
            return "";
        var parts = new List<string>();
        foreach (var item in c.GetInventory().GetAllItems())
            parts.Add($"{(item.m_dropPrefab != null ? item.m_dropPrefab.name : item.m_shared.m_name)}@{item.m_gridPos.x},{item.m_gridPos.y}");
        return string.Join(";", parts);
    }

    /// <summary>Opens the paper reader on the first written page in the inventory (prefab Drakes_Paper_Written).</summary>
    [TestHook("renameit.paperOpenReader")]
    static bool PaperOpenReader(Dictionary<string, object?> args)
    {
        var item = P.GetInventory().GetAllItems().FirstOrDefault(i => i.m_dropPrefab != null && i.m_dropPrefab.name == "Drakes_Paper_Written")
                   ?? throw new InvalidOperationException("no written page in the inventory");
        PaperReaderPanel.Show(item);
        return PaperReaderPanel.IsOpen;
    }

    /// <summary>True when Use on this written page would open the reader instead of placing it (what the patch decides).</summary>
    [TestHook("renameit.paperUseReads")]
    static bool PaperUseReads(Dictionary<string, object?> args) => RenameitConfig.PaperUseReads;

    /// <summary>True when the placed book has a cover picture quad under it.</summary>
    [TestHook("renameit.coverPictureShown")]
    static bool CoverPictureShown(Dictionary<string, object?> args, GameObject? target = null) =>
        target != null && target.transform.Find("attach/drakes_book_mesh/cover_picture") != null;

    /// <summary>"WxH" of a shelf's container (inventory columns x rows).</summary>
    [TestHook("renameit.containerSize")]
    static string ContainerSize(Dictionary<string, object?> args, GameObject? target = null) =>
        target != null && target.GetComponent<Container>() is { } c ? $"{c.m_width}x{c.m_height}" : "none";

    /// <summary>"true" when a registered prefab with this name exists (pieces and items).</summary>
    [TestHook("renameit.hasPrefab")]
    static bool HasPrefab(Dictionary<string, object?> args) =>
        ZNetScene.instance != null && ZNetScene.instance.GetPrefab(S(args, "name")) != null
        || ObjectDB.instance != null && ObjectDB.instance.GetItemPrefab(S(args, "name")) != null;
}
