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

    /// <summary>Opens the reader on the first inventory book as its maker: tab "pages" (read view), "full" (full editor) or "book", edit true/false.</summary>
    [TestHook("renameit.bookOpenReader")]
    static bool OpenReader(Dictionary<string, object?> args)
    {
        var tab = S(args, "tab", "pages");
        if (tab == "full")
        {
            BookReaderPanel.ShowFull(FirstBook(), canEdit: true);
            return BookReaderPanel.IsOpen;
        }

        BookReaderPanel.Show(FirstBook(), canEdit: true);
        BookReaderPanel.TestSelect(tab == "book", B(args, "edit"));
        return BookReaderPanel.IsOpen;
    }

    /// <summary>Closes the reader and the picker (the test step closeUi only closes the game's own windows).</summary>
    [TestHook("renameit.closeBookWindows")]
    static bool CloseWindows(Dictionary<string, object?> args)
    {
        BookReaderPanel.CloseWindow();
        DrakeModsLibs.UI.DrakeTabHost.Close();
        BookPickerPanel.CloseWindow();
        ShelfSettingsPanel.CloseWindow();
        SecretDoorSettings.CloseWindow();
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

    static System.Type? LockMenuType => HarmonyLib.AccessTools.TypeByName("LockSmith.UI.PieceAccessMenu");

    /// <summary>Opens LockSmith's Shift+E menu on the target chest (as its key would), through reflection: release LockSmith has no hooks.</summary>
    [TestHook("renameit.lockMenuOpen")]
    static bool LockMenuOpen(Dictionary<string, object?> args, GameObject? target = null)
    {
        var container = target != null ? target.GetComponentInChildren<Container>() : null;
        var open = LockMenuType?.GetMethod("TryOpenLockMenu", new[] { typeof(Container), typeof(Humanoid), HarmonyLib.AccessTools.TypeByName("LockSmith.UI.LockTab") });
        if (container == null || open == null)
            return false;
        var tab = open.GetParameters()[2];
        return (bool)open.Invoke(null, new[] { container, P, tab.HasDefaultValue ? tab.DefaultValue : System.Activator.CreateInstance(tab.ParameterType) })!;
    }

    /// <summary>The open LockSmith menu's tab labels ("" when closed).</summary>
    [TestHook("renameit.lockMenuTabs")]
    static string LockMenuTabs() => LockMenuType?.GetProperty("TabOrder")?.GetValue(null) as string ?? "";

    [TestHook("renameit.lockMenuClose")]
    static bool LockMenuClose()
    {
        LockMenuType?.GetMethod("Close", System.Type.EmptyTypes)?.Invoke(null, null);
        return true;
    }

    /// <summary>
    /// Puts "count" made-up books straight into the target shelf (seeded, so every run looks the same): varied titles, cover colours,
    /// patterns, accents, spine inks, sizes (mostly medium) and page counts (1 to the limit). Returns how many went in.
    /// </summary>
    [TestHook("renameit.fillShelf")]
    static int FillShelf(Dictionary<string, object?> args, GameObject? target = null)
    {
        var container = target != null ? target.GetComponent<Container>() : null;
        var proto = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab(BookItem.PrefabName) : null;
        if (container == null || proto == null || proto.GetComponent<ItemDrop>() is not { } drop)
            return 0;
        string[] titles =
        {
            "Old Ledger", "Ravens Notes", "Smithing Primer", "Sagas of the North", "Herbs and Roots", "Tides", "Map Book", "Trolls",
            "Bronze and Tin", "Songs", "The Black Forest", "Recipes", "Runes", "Letters Home", "Swamp Journal", "Stars",
        };
        var rng = new System.Random(int.Parse(S(args, "seed", "7"), CultureInfo.InvariantCulture));
        var count = int.Parse(S(args, "count", "16"), CultureInfo.InvariantCulture);
        var added = 0;
        for (var i = 0; i < count; i++)
        {
            var item = drop.m_itemData.Clone();
            item.m_dropPrefab = proto;
            item.m_stack = 1;
            item.m_customData = new Dictionary<string, string>();
            BookData.SetTitle(item, titles[rng.Next(titles.Length)]);
            BookData.SetColor(item, BookLook.Covers[rng.Next(BookLook.Covers.Length)]);
            BookLook.SetPattern(item, (BookPattern)rng.Next((int)BookPattern.Studs + 1));
            BookLook.SetAccent(item, BookLook.Accents[rng.Next(BookLook.Accents.Length)]);
            BookLook.SetInk(item, BookLook.Inks[rng.Next(2)]);
            var roll = rng.Next(10);
            BookData.SetSize(item, roll < 2 ? BookSize.Small : roll > 7 ? BookSize.Large : BookSize.Medium);
            var pages = new List<string>();
            for (var p = rng.Next(1, BookData.MaxPages + 1); p > 0; p--)
                pages.Add("Page.");
            BookData.SetPages(item, pages);
            if (!container.GetInventory().AddItem(item))
                break;
            added++;
        }

        target!.GetComponent<BookShelfBehaviour>()?.SaveContents();
        return added;
    }

    /// <summary>Opens the inventory tab host (Shift+right-click) on the first book. True when the book editor is what opened.</summary>
    [TestHook("renameit.bookOpenHost")]
    static bool OpenHost(Dictionary<string, object?> args) =>
        DrakeModsLibs.UI.DrakeTabHost.OpenForItem(FirstBook()) && BookReaderPanel.IsOpen;

    /// <summary>
    /// Sets the target shelf onto the nearest wood floor the way snapping would: same turn as the floor, the shelf's corner snap point on
    /// the floor's top corner snap point, then "along" metres along the floor's X (and "across" along its Z). Reports whether the whole
    /// footprint lies on the floor and every footprint corner sits on the floor's half-metre grid, e.g. "inside=True grid=True".
    /// </summary>
    [TestHook("renameit.snapToFloor")]
    static string SnapToFloor(Dictionary<string, object?> args, GameObject? target = null)
    {
        if (target == null || target.GetComponent<BookShelfBehaviour>() is not { } shelf)
            return "no shelf";
        Piece? floor = null;
        var best = 6f;
        foreach (var piece in UnityEngine.Object.FindObjectsOfType<Piece>())
        {
            if (!Utils.GetPrefabName(piece.gameObject).StartsWith("wood_floor", StringComparison.OrdinalIgnoreCase))
                continue;
            var d = Vector3.Distance(piece.transform.position, target.transform.position);
            if (d < best)
            {
                best = d;
                floor = piece;
            }
        }

        if (floor == null)
            return "no floor";
        var floorPoints = new List<Vector3>();
        foreach (Transform child in floor.transform)
            if (child.CompareTag("snappoint"))
                floorPoints.Add(child.localPosition);
        if (floorPoints.Count == 0)
            return "floor has no snap points";
        var top = floorPoints.Max(p => p.y);
        var minX = floorPoints.Min(p => p.x);
        var maxX = floorPoints.Max(p => p.x);
        var minZ = floorPoints.Min(p => p.z);
        var maxZ = floorPoints.Max(p => p.z);

        var shelfPoints = new List<Vector3>();
        foreach (Transform child in target.transform)
            if (child.CompareTag("snappoint"))
                shelfPoints.Add(child.localPosition);
        var sMinX = shelfPoints.Min(p => p.x);
        var sMaxX = shelfPoints.Max(p => p.x);
        var sMinZ = shelfPoints.Min(p => p.z);
        var sMaxZ = shelfPoints.Max(p => p.z);
        var along = float.Parse(S(args, "along", "0"), CultureInfo.InvariantCulture);
        var across = float.Parse(S(args, "across", "0"), CultureInfo.InvariantCulture);

        // Shelf corner (sMinX, 0, sMinZ) onto floor corner (minX, top, minZ) + offset, both in the floor's frame.
        var local = new Vector3(minX + along - sMinX, top, minZ + across - sMinZ);
        target.transform.rotation = floor.transform.rotation;
        target.transform.position = floor.transform.TransformPoint(local);
        if (target.GetComponent<ZNetView>() is { } nview && nview.IsValid())
        {
            nview.ClaimOwnership();
            nview.GetZDO().SetPosition(target.transform.position);
            nview.GetZDO().SetRotation(target.transform.rotation);
        }

        const float eps = 0.01f;
        float[] xs = { local.x + sMinX, local.x + sMaxX };
        float[] zs = { local.z + sMinZ, local.z + sMaxZ };
        var inside = xs.All(x => x >= minX - eps && x <= maxX + eps) && zs.All(z => z >= minZ - eps && z <= maxZ + eps);
        bool OnHalf(float v, float origin) => Mathf.Abs((v - origin) / 0.5f - Mathf.Round((v - origin) / 0.5f)) < 0.02f;
        var grid = xs.All(x => OnHalf(x, minX)) && zs.All(z => OnHalf(z, minZ));
        return $"inside={inside} grid={grid} footprint={(sMaxX - sMinX).ToString("0.00", CultureInfo.InvariantCulture)}x{(sMaxZ - sMinZ).ToString("0.00", CultureInfo.InvariantCulture)} floor={(maxX - minX).ToString("0.00", CultureInfo.InvariantCulture)}x{(maxZ - minZ).ToString("0.00", CultureInfo.InvariantCulture)}";
    }

    /// <summary>
    /// Removes trees, logs, bushes and rocks within "radius" metres of the player, for clean pictures (stand-in until TestLab's own
    /// clearArea step is in the deployed TestLab). Returns how many went.
    /// </summary>
    [TestHook("renameit.clearAround")]
    static int ClearAround(Dictionary<string, object?> args)
    {
        var radius = float.Parse(S(args, "radius", "15"), CultureInfo.InvariantCulture);
        var centre = P.transform.position;
        var doomed = new List<ZNetView>();
        foreach (var nview in UnityEngine.Object.FindObjectsOfType<ZNetView>())
        {
            if (nview == null || !nview.IsValid() || Vector3.Distance(nview.transform.position, centre) > radius)
                continue;
            var go = nview.gameObject;
            if (go.GetComponent<TreeBase>() != null || go.GetComponent<TreeLog>() != null || go.GetComponent<MineRock>() != null
                || go.GetComponent<MineRock5>() != null || go.GetComponent<Destructible>() != null && go.GetComponent<Piece>() == null)
                doomed.Add(nview);
        }

        foreach (var nview in doomed)
        {
            nview.ClaimOwnership();
            ZNetScene.instance.Destroy(nview.gameObject);
        }

        return doomed.Count;
    }

    static SecretBookcaseBehaviour? Secret(GameObject? target) => target != null ? target.GetComponent<SecretBookcaseBehaviour>() : null;

    /// <summary>Aim slot for the secret-door hooks: "handle", a slot number, or "case" (the bookcase, no book).</summary>
    static int AimSlot(Dictionary<string, object?> args, SecretBookcaseBehaviour secret)
    {
        var at = S(args, "at", "handle");
        return at == "handle" ? secret.Handle : at == "case" ? -1 : int.Parse(at, CultureInfo.InvariantCulture);
    }

    /// <summary>The secret door's hover text while aiming at "at" (handle / a slot / case).</summary>
    [TestHook("renameit.secretHover")]
    static string SecretHover(Dictionary<string, object?> args, GameObject? target = null)
    {
        if (Secret(target) is not { } secret)
            return "no secret door";
        SecretBookcaseBehaviour.TestAim = AimSlot(args, secret);
        try
        {
            return Localization.instance.Localize(target!.GetComponent<Door>().GetHoverText());
        }
        finally
        {
            SecretBookcaseBehaviour.TestAim = null;
        }
    }

    /// <summary>Presses Use (E) on the secret door while aiming at "at"; returns what Interact answered.</summary>
    [TestHook("renameit.secretUse")]
    static bool SecretUse(Dictionary<string, object?> args, GameObject? target = null)
    {
        if (Secret(target) is not { } secret)
            return false;
        SecretBookcaseBehaviour.TestAim = AimSlot(args, secret);
        try
        {
            return target!.GetComponent<Door>().Interact(P, false, false);
        }
        finally
        {
            SecretBookcaseBehaviour.TestAim = null;
        }
    }

    /// <summary>True while the secret door stands open (its shared door state is not 0).</summary>
    [TestHook("renameit.secretIsOpen")]
    static bool SecretIsOpen(Dictionary<string, object?> args, GameObject? target = null) =>
        target != null && target.GetComponent<ZNetView>() is { } nview && nview.IsValid() && nview.GetZDO().GetInt(ZDOVars.s_state) != 0;

    /// <summary>Sets the handle slot and its title, as the Secret door tab does.</summary>
    [TestHook("renameit.secretConfigure")]
    static bool SecretConfigure(Dictionary<string, object?> args, GameObject? target = null)
    {
        if (Secret(target) is not { } secret)
            return false;
        secret.Configure(int.Parse(S(args, "slot", secret.Handle.ToString(CultureInfo.InvariantCulture)), CultureInfo.InvariantCulture), S(args, "label", secret.Label));
        return true;
    }

    [TestHook("renameit.secretOpenSettings")]
    static bool SecretOpenSettings(Dictionary<string, object?> args, GameObject? target = null)
    {
        if (Secret(target) is not { } secret)
            return false;
        SecretDoorSettings.Show(secret);
        return true;
    }

    /// <summary>Puts "text" (rich tags allowed) on the open editor's left page, caret at "cursor", selection from "anchor".</summary>
    [TestHook("renameit.bookCaret")]
    static bool BookCaret(Dictionary<string, object?> args)
    {
        var cursor = int.Parse(S(args, "cursor", "0"), CultureInfo.InvariantCulture);
        var anchorAt = int.Parse(S(args, "anchor", cursor.ToString(CultureInfo.InvariantCulture)), CultureInfo.InvariantCulture);
        return BookReaderPanel.TestCaret(S(args, "text", ""), cursor, anchorAt);
    }

    /// <summary>The icon the game shows for the first inventory book (its sprite name).</summary>
    [TestHook("renameit.bookIcon")]
    static string BookIcon(Dictionary<string, object?> args) => FirstBook().GetIcon() is { } sprite ? sprite.name : "none";

    /// <summary>Hold Use on the target placed book (hold=true), as the game sends it after the hold delay. True when it acted.</summary>
    [TestHook("renameit.bookHoldUse")]
    static bool BookHoldUse(Dictionary<string, object?> args, GameObject? target = null) =>
        target != null && target.GetComponent<BookPieceBehaviour>() is { } book && book.Interact(P, true, false);

    /// <summary>Height above the piece origin of the placed book's lowest drawn point (0 = flush), and whether it has titles.</summary>
    [TestHook("renameit.bookRest")]
    static string BookRest(Dictionary<string, object?> args, GameObject? target = null)
    {
        if (target == null)
            return "none";
        var low = float.MaxValue;
        foreach (var r in target.GetComponentsInChildren<MeshRenderer>())
            if (r.GetComponent<TMPro.TextMeshPro>() == null)
                low = Mathf.Min(low, r.bounds.min.y);
        var rest = low - target.transform.position.y;
        var titles = target.GetComponentsInChildren<TMPro.TextMeshPro>().Length;
        var where = Mathf.Abs(rest) < 0.006f ? "flush" : "rest=" + rest.ToString("0.000", CultureInfo.InvariantCulture);
        return $"{where} titles={titles}";
    }

    /// <summary>Moves the nth item of a shelf row to slot "to" of that row (swapping), as dragging on the layout board does.</summary>
    [TestHook("renameit.shelfMove")]
    static bool ShelfMove(Dictionary<string, object?> args, GameObject? target = null)
    {
        if (target == null || target.GetComponent<BookShelfBehaviour>() is not { } shelf)
            return false;
        var row = int.Parse(S(args, "row", "0"));
        var items = shelf.RowItems(row);
        var index = int.Parse(S(args, "index", "0"));
        return index < items.Count && shelf.MoveTo(items[index], row, int.Parse(S(args, "to", "0")));
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

    /// <summary>Where each drawn shelf item is, in the piece's own space (x right, y up, z forward): "x,y,z;..." plus the piece's mesh bounds.</summary>
    [TestHook("renameit.shelfWhere")]
    static string ShelfWhere(Dictionary<string, object?> args, GameObject? target = null)
    {
        if (target == null)
            return "";
        var holder = target.transform.Find(BookShelfPiece.FrameName + "/drake_shelf_items");
        var parts = new List<string>();
        if (holder != null)
            foreach (Transform child in holder)
            {
                var b = new Bounds(); var any = false;
                foreach (var r in child.GetComponentsInChildren<Renderer>())
                {
                    if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
                }
                if (!any) continue;
                var c = target.transform.InverseTransformPoint(b.center);
                parts.Add($"{c.x:0.00},{c.y:0.00},{c.z:0.00}");
            }
        var box = new Bounds(); var has = false;
        foreach (var r in target.GetComponentsInChildren<MeshRenderer>())
        {
            if (holder != null && r.transform.IsChildOf(holder)) continue;
            var lc = target.transform.InverseTransformPoint(r.bounds.center);
            var e = r.bounds.extents;
            var local = new Bounds(lc, e * 2f);
            if (!has) { box = local; has = true; } else box.Encapsulate(local);
        }
        return string.Join(";", parts) + " | case " + (has ? $"x{box.min.x:0.00}..{box.max.x:0.00} y{box.min.y:0.00}..{box.max.y:0.00} z{box.min.z:0.00}..{box.max.z:0.00}" : "?");
    }

    /// <summary>True when the shelf's Shift+Use is claimed (so RenameIt's piece rename leaves it alone).</summary>
    [TestHook("renameit.shelfClaimed")]
    static bool ShelfClaimed(Dictionary<string, object?> args, GameObject? target = null) =>
        target != null && DrakeModsLibs.API.PieceInteractionClaims.IsClaimed(target.GetComponent<Piece>());

    /// <summary>The hover text of a shelf as the player sees it.</summary>
    [TestHook("renameit.shelfHover")]
    static string ShelfHover(Dictionary<string, object?> args, GameObject? target = null)
    {
        var container = target != null ? target.GetComponent<Container>() : null;
        return container != null ? Localization.instance.Localize(container.GetHoverText()) : "";
    }

    /// <summary>Pull the third-person camera in close (metres behind the head; 0 = first person).</summary>
    [TestHook("renameit.cameraZoom")]
    static bool CameraZoom(Dictionary<string, object?> args)
    {
        var cam = GameCamera.instance;
        if (cam == null)
            return false;
        var d = float.Parse(S(args, "distance", "0.3"), CultureInfo.InvariantCulture);
        foreach (var name in new[] { "m_distance", "m_minDistance", "m_maxDistance" })
            HarmonyLib.AccessTools.Field(typeof(GameCamera), name)?.SetValue(cam, name == "m_maxDistance" ? Mathf.Max(d, 0.1f) : d);
        return true;
    }

    /// <summary>Hide or show the player's body (so a screenshot shows the piece, not the player standing in front of it).</summary>
    [TestHook("renameit.hidePlayer")]
    static bool HidePlayer(Dictionary<string, object?> args)
    {
        var hide = B(args, "on", true);
        foreach (var r in P.GetComponentsInChildren<Renderer>(true))
            r.enabled = !hide;
        return true;
    }

    /// <summary>Stand "distance" metres in front of a piece (the side its front faces), at "side" metres to its left, looking at it.</summary>
    [TestHook("renameit.frontView")]
    static bool FrontView(Dictionary<string, object?> args, GameObject? target = null)
    {
        if (target == null)
            return false;
        var distance = float.Parse(S(args, "distance", "2.4"), CultureInfo.InvariantCulture);
        var side = float.Parse(S(args, "side", "0"), CultureInfo.InvariantCulture);
        var t = target.transform;
        var spot = t.position + t.forward * distance + t.right * side;
        var p = P;
        spot.y = p.transform.position.y;
        p.transform.position = spot;
        if (p.GetComponent<Rigidbody>() is { } body)
        {
            body.position = spot;
            body.velocity = Vector3.zero;
        }

        var look = (t.position + Vector3.up * 0.9f - p.GetEyePoint()).normalized;
        var flat = new Vector3(look.x, 0f, look.z);
        var yaw = Quaternion.LookRotation(flat.normalized);
        p.transform.rotation = yaw;
        HarmonyLib.AccessTools.Field(typeof(Player), "m_lookYaw")?.SetValue(p, yaw);
        HarmonyLib.AccessTools.Field(typeof(Player), "m_lookPitch")?.SetValue(p, Mathf.Clamp(-Mathf.Asin(look.y) * Mathf.Rad2Deg, -89f, 89f));
        p.SetMouseLook(Vector2.zero);
        return true;
    }

    /// <summary>Put a copy of the first book in the inventory into the shelf at grid x,y (with its title kept).</summary>
    [TestHook("renameit.shelfStoreAt")]
    static bool StoreAt(Dictionary<string, object?> args, GameObject? target = null)
    {
        var container = target != null ? target.GetComponent<Container>() : null;
        if (container == null)
            return false;
        var item = FirstBook().Clone();
        item.m_stack = 1;
        item.m_gridPos = new Vector2i(int.Parse(S(args, "x", "0")), int.Parse(S(args, "y", "0")));
        var inventory = container.GetInventory();
        if (!inventory.AddItem(item))
            return false;
        // the game picks its own slot; put the new item where the test asked
        var live = inventory.GetAllItems().Last();
        var wanted = new Vector2i(int.Parse(S(args, "x", "0")), int.Parse(S(args, "y", "0")));
        var other = inventory.GetItemAt(wanted.x, wanted.y);
        if (other != null && other != live)
            other.m_gridPos = live.m_gridPos;
        live.m_gridPos = wanted;
        HarmonyLib.AccessTools.Method(typeof(Container), "Save")?.Invoke(container, null);
        return true;
    }

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
