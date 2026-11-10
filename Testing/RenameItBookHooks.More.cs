using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using DrakeRenameit.Books;
using UnityEngine;
using UnityEngine.UIElements;

namespace DrakeRenameit.Testing;

/// <summary>Hooks for the 1.5 book round: windows' text and scroll bars, the picker, names file, standing books, owners.</summary>
internal static partial class RenameItBookHooks
{
    static VisualElement? Card(string window) => window switch
    {
        "shelf" => ShelfSettingsPanel.TestCard,
        "picker" => BookPickerPanel.TestCard,
        "secret" => SecretDoorSettings.TestCard,
        _ => BookReaderPanel.TestCard,
    };

    /// <summary>All the words in an open window ("window": reader / shelf / picker / secret): labels, buttons and field values.</summary>
    [TestHook("renameit.uiText")]
    static string UiText(Dictionary<string, object?> args)
    {
        var card = Card(S(args, "window", "reader"));
        if (card == null)
            return "window closed";
        var text = new StringBuilder();
        foreach (var label in card.Query<TextElement>().ToList())
            if (label.resolvedStyle.display != DisplayStyle.None && !string.IsNullOrEmpty(label.text))
                text.Append(label.text).Append(" | ");
        foreach (var field in card.Query<TextField>().ToList())
            text.Append(field.value).Append(" | ");
        return text.ToString();
    }

    /// <summary>
    /// Whether the scroll area "name" in a window has more inside than it shows and its bar is drawn:
    /// "scrolls=True bar=True content=1234 view=620".
    /// </summary>
    [TestHook("renameit.uiScroll")]
    static string UiScroll(Dictionary<string, object?> args)
    {
        var card = Card(S(args, "window", "reader"));
        if (card == null)
            return "window closed";
        var name = S(args, "name");
        var scroll = card.Query<ScrollView>().ToList().FirstOrDefault(v => name.Length == 0 || v.name == name);
        if (scroll == null)
            return "no scroll view";
        var content = scroll.contentContainer.layout.height;
        var view = scroll.contentViewport.layout.height;
        // the drawn bar (BookScroll) sits beside the scroll view in its frame
        var track = scroll.parent?.Q("drakes_scroll_track");
        var bar = track != null && track.resolvedStyle.display == DisplayStyle.Flex && track.layout.height > 10f;
        return $"scrolls={content > view + 1f} bar={bar} content={content.ToString("0", CultureInfo.InvariantCulture)} view={view.ToString("0", CultureInfo.InvariantCulture)}";
    }

    /// <summary>Title, pages, colour, size onto inventory book number "index" (0 = the first).</summary>
    [TestHook("renameit.bookWriteAt")]
    static bool WriteAt(Dictionary<string, object?> args)
    {
        var books = P.GetInventory().GetAllItems().Where(BookItem.IsBook).ToList();
        var index = int.Parse(S(args, "index", "0"), CultureInfo.InvariantCulture);
        if (index < 0 || index >= books.Count)
            return false;
        var book = books[index];
        BookData.SetTitle(book, S(args, "title", "Test Book"));
        BookData.SetPages(book, S(args, "pages", "One").Split('|'));
        BookData.SetColor(book, BookData.CoverColors[Mathf.Clamp(int.Parse(S(args, "color", "0")), 0, BookData.CoverColors.Length - 1)]);
        BookData.SetSize(book, (BookSize)Mathf.Clamp(int.Parse(S(args, "size", "1")), 0, 2));
        return true;
    }

    [TestHook("renameit.bookCount")]
    static int BookCount(Dictionary<string, object?> args) => P.GetInventory().GetAllItems().Count(BookItem.IsBook);

    [TestHook("renameit.pickerCount")]
    static int PickerCount(Dictionary<string, object?> args) => BookPickerPanel.TestCount;

    /// <summary>Chooses dropdown entry "index" and presses Place it.</summary>
    [TestHook("renameit.pickerPick")]
    static bool PickerPick(Dictionary<string, object?> args)
    {
        var ok = BookPickerPanel.TestPick(int.Parse(S(args, "index", "0"), CultureInfo.InvariantCulture));
        BookPlacementPatches.KeepPick(10f);
        return ok;
    }

    /// <summary>Opens (or closes) the picker's list of carried books.</summary>
    [TestHook("renameit.pickerList")]
    static bool PickerList(Dictionary<string, object?> args)
    {
        BookPickerPanel.TestToggleList();
        return BookPickerPanel.IsOpen;
    }

    /// <summary>Presses New book.</summary>
    [TestHook("renameit.pickerPickNew")]
    static bool PickerPickNew(Dictionary<string, object?> args)
    {
        var ok = BookPickerPanel.TestPickNew();
        BookPlacementPatches.KeepPick(10f);
        return ok;
    }

    /// <summary>What the Book piece will place next: "item:Title", "blank" or "none".</summary>
    [TestHook("renameit.pickState")]
    static string PickState(Dictionary<string, object?> args) => BookPlaceState.Describe();

    /// <summary>On the open full editor: picks "hex" on row "row" (cover / accent / ink), and opens that row's colour picker with open:true.</summary>
    [TestHook("renameit.bookColour")]
    static bool BookColour(Dictionary<string, object?> args) =>
        BookReaderPanel.TestColour(S(args, "row", "cover"), S(args, "hex", "#336699"), B(args, "open"));

    /// <summary>One custom-data value of the first inventory book ("key": color / accent / ink, or a raw key).</summary>
    [TestHook("renameit.bookKey")]
    static string BookKey(Dictionary<string, object?> args)
    {
        var key = S(args, "key", "color");
        key = key switch { "color" => BookData.KeyColor, "accent" => BookLook.KeyAccent, "ink" => BookLook.KeyInk, _ => key };
        return BookData.Get(FirstBook(), key) ?? "";
    }

    // ---------------------------------------------------------------- names file

    static string Backup => BookNames.FilePath + ".testbak";

    /// <summary>
    /// Writes "values" ({ key: name }) into BookNames.yml (backing the real file up first) and reloads it. Undo with renameit.namesRestore.
    /// </summary>
    [TestHook("renameit.namesSet")]
    static bool NamesSet(Dictionary<string, object?> args)
    {
        if (File.Exists(BookNames.FilePath) && !File.Exists(Backup))
            File.Copy(BookNames.FilePath, Backup);
        var lines = new List<string> { "# written by a RenameIt test (renameit.namesSet); the real file is in BookNames.yml.testbak" };
        if (args.TryGetValue("values", out var raw) && raw is Dictionary<string, object?> values)
            foreach (var pair in values)
                lines.Add(pair.Key + ": " + Convert.ToString(pair.Value, CultureInfo.InvariantCulture));
        File.WriteAllLines(BookNames.FilePath, lines, new UTF8Encoding(false));
        BookNames.Reload();
        return true;
    }

    /// <summary>Puts the real BookNames.yml back (or removes the test one) and reloads; the names go back to the defaults.</summary>
    [TestHook("renameit.namesRestore")]
    static bool NamesRestore(Dictionary<string, object?> args)
    {
        if (File.Exists(Backup))
        {
            File.Copy(Backup, BookNames.FilePath, overwrite: true);
            File.Delete(Backup);
        }
        else if (File.Exists(BookNames.FilePath))
        {
            File.Delete(BookNames.FilePath);
        }

        // Reloading puts every name the test file changed back to its default.
        BookNames.Reload();
        return true;
    }

    /// <summary>Takes every "prefab" (comma-separated item prefabs) out of the inventory; returns how many went.</summary>
    [TestHook("renameit.removeItems")]
    static int RemoveItems(Dictionary<string, object?> args)
    {
        var inv = P.GetInventory();
        var gone = 0;
        foreach (var prefab in S(args, "prefab").Split(','))
        {
            var drop = ObjectDB.instance?.GetItemPrefab(prefab.Trim())?.GetComponent<ItemDrop>();
            if (drop == null)
                continue;
            var name = drop.m_itemData.m_shared.m_name;
            var count = inv.CountItems(name);
            if (count > 0)
                inv.RemoveItem(name, count);
            gone += count;
        }

        return gone;
    }

    [TestHook("renameit.namesFileHas")]
    static string NamesFileHas(Dictionary<string, object?> args) => File.Exists(BookNames.FilePath) ? File.ReadAllText(BookNames.FilePath) : "no file";

    [TestHook("renameit.localize")]
    static string LocalizeHook(Dictionary<string, object?> args) => Localization.instance.Localize(S(args, "text"));

    // ---------------------------------------------------------------- owners and settings

    /// <summary>Gives a shelf (or any piece) its own name, as the Name field does.</summary>
    [TestHook("renameit.shelfRename")]
    static bool ShelfRename(Dictionary<string, object?> args, GameObject? target = null)
    {
        var piece = target != null ? target.GetComponent<Piece>() : null;
        if (piece == null)
            return false;
        Pieces.PieceRename.SetCustomName(piece, S(args, "name"));
        return true;
    }

    /// <summary>Makes a placed book someone else's ("owner": a player id, or "other"), or mine again ("me").</summary>
    [TestHook("renameit.bookSetOwner")]
    static bool BookSetOwner(Dictionary<string, object?> args, GameObject? target = null)
    {
        var nview = target != null && target.GetComponent<BookPieceBehaviour>() != null ? target.GetComponent<ZNetView>() : null;
        if (nview == null || !nview.IsValid())
            return false;
        var who = S(args, "owner", "other");
        var id = who == "me" ? P.GetPlayerID() : who == "other" ? 424242L : long.Parse(who, CultureInfo.InvariantCulture);
        nview.ClaimOwnership();
        nview.GetZDO().Set("DrakeBookOwner", id);
        return true;
    }

    /// <summary>Turns a placed book's own switch on or off ("flag": readable / take).</summary>
    [TestHook("renameit.bookSetFlag")]
    static bool BookSetFlag(Dictionary<string, object?> args, GameObject? target = null)
    {
        var nview = target != null && target.GetComponent<BookPieceBehaviour>() != null ? target.GetComponent<ZNetView>() : null;
        if (nview == null || !nview.IsValid())
            return false;
        var data = BookData.Unpack(nview.GetZDO().GetString("DrakeBookData", ""));
        var key = S(args, "flag", "take") == "readable" ? BookData.KeyReadable : BookData.KeyTake;
        data[key] = B(args, "on") ? "1" : "0";
        nview.ClaimOwnership();
        nview.GetZDO().Set("DrakeBookData", BookData.Pack(data));
        return true;
    }

    /// <summary>The packed data a placed book would send to another client (for peerSet), with a new title.</summary>
    [TestHook("renameit.bookPackedWithTitle")]
    static string BookPackedWithTitle(Dictionary<string, object?> args, GameObject? target = null)
    {
        var nview = target != null ? target.GetComponent<ZNetView>() : null;
        if (nview == null || !nview.IsValid())
            return "";
        var data = BookData.Unpack(nview.GetZDO().GetString("DrakeBookData", ""));
        data[DrakeModsLibs.Data.DrakeCustomDataKeys.Rename] = S(args, "title", "Edited elsewhere");
        return BookData.Pack(data);
    }

    // ---------------------------------------------------------------- pieces

    /// <summary>How a placed or spawned book stands: "upright=True" when it is taller than it is wide, plus its size in metres.</summary>
    [TestHook("renameit.bookPose")]
    static string BookPose(Dictionary<string, object?> args, GameObject? target = null)
    {
        if (target == null)
            return "none";
        var bounds = new Bounds(target.transform.position, Vector3.zero);
        var any = false;
        foreach (var r in target.GetComponentsInChildren<MeshRenderer>())
        {
            if (r.GetComponent<TMPro.TextMeshPro>() != null)
                continue;
            if (!any)
                bounds = r.bounds;
            else
                bounds.Encapsulate(r.bounds);
            any = true;
        }

        var size = bounds.size;
        var upright = size.y > Mathf.Max(size.x, size.z) * 0.9f;
        var low = bounds.min.y - target.transform.position.y;
        return $"upright={upright} flush={Mathf.Abs(low) < 0.006f} size={size.x.ToString("0.00", CultureInfo.InvariantCulture)}x{size.y.ToString("0.00", CultureInfo.InvariantCulture)}x{size.z.ToString("0.00", CultureInfo.InvariantCulture)}";
    }

    /// <summary>The piece's snap points: how many, and whether every one is a corner of its footprint (floor or top).</summary>
    [TestHook("renameit.snapPoints")]
    static string SnapPoints(Dictionary<string, object?> args, GameObject? target = null)
    {
        if (target == null)
            return "none";
        var points = new List<Vector3>();
        foreach (Transform child in target.transform)
            if (child.CompareTag("snappoint"))
                points.Add(child.localPosition);
        if (points.Count == 0)
            return "count=0";
        var minX = points.Min(p => p.x);
        var maxX = points.Max(p => p.x);
        var minZ = points.Min(p => p.z);
        var maxZ = points.Max(p => p.z);
        bool Near(float a, float b) => Mathf.Abs(a - b) < 0.001f;
        var corners = points.All(p => (Near(p.x, minX) || Near(p.x, maxX)) && (Near(p.z, minZ) || Near(p.z, maxZ)));
        return $"count={points.Count} corners={corners}";
    }
}
