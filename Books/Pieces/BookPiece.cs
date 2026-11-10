using System;
using System.Collections.Generic;
using BepInEx.Logging;
using DrakeModsLibs.API;
using DrakeRenameit.Paper.Items;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DrakeRenameit.Books;

/// <summary>
/// The hammer's Book piece (Furniture). Placing it is free in the build menu: the real cost is the book you pick in
/// <see cref="BookPickerPanel"/> first, taken from your inventory when the piece appears (see <see cref="BookPieceBehaviour"/>).
/// </summary>
internal static class BookPiece
{
    internal const string PrefabName = "Drakes_BookPiece";
    internal const string UprightPrefab = "Drakes_BookPieceUpright";
    const string NameToken = "$piece_drakes_book";
    const string DescToken = "$piece_drakes_book_desc";
    internal const string UprightToken = "$piece_drakes_book_upright";

    /// <summary>
    /// A Gizmo mod (ComfyGizmo, Gizmo Reloaded...) turns pieces on every axis, so one Book piece is enough. Without one there is
    /// a second piece that stands the book up, since the hammer alone only turns it flat on the table.
    /// </summary>
    internal static bool GizmoInstalled()
    {
        foreach (var info in BepInEx.Bootstrap.Chainloader.PluginInfos.Values)
        {
            var guid = info?.Metadata?.GUID ?? "";
            var name = info?.Metadata?.Name ?? "";
            if (guid.IndexOf("gizmo", StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf("gizmo", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
        }

        return false;
    }

    static ManualLogSource? _log;

    internal static void Register(ManualLogSource log)
    {
        _log = log;
        PrefabManager.OnVanillaPrefabsAvailable -= Add;
        PrefabManager.OnVanillaPrefabsAvailable += Add;
    }

    static void Add()
    {
        PrefabManager.OnVanillaPrefabsAvailable -= Add;
        try
        {
            if (!RenameitConfig.BooksEnabled)
            {
                _log?.LogInfo("[Books] Book piece skipped (BooksEnabled off).");
                return;
            }

            var loc = LocalizationManager.Instance.GetLocalization();
            loc.AddTranslation("English", "piece_drakes_book", "Book");
            loc.AddTranslation("English", "piece_drakes_book_desc", "Set a book down to read. Pick which book (or a new one) before you place it.");
            loc.AddTranslation("Spanish", "piece_drakes_book", "Libro");
            loc.AddTranslation("Spanish", "piece_drakes_book_desc", "Deja un libro para leerlo. Elige cuál antes de colocarlo.");
            loc.AddTranslation("English", UprightToken.TrimStart('$'), "Book, standing");
            loc.AddTranslation("English", UprightToken.TrimStart('$') + "_desc", "Stand a book up on a table or floor, cover to the front. Pick which book (or a new one) before you place it.");
            loc.AddTranslation("Spanish", UprightToken.TrimStart('$'), "Libro de pie");
            loc.AddTranslation("Spanish", UprightToken.TrimStart('$') + "_desc", "Pon un libro de pie. Elige cuál antes de colocarlo.");

            One(PrefabName, NameToken, DescToken, upright: false);
            if (GizmoInstalled())
                _log?.LogInfo("[Books] A Gizmo mod is loaded: one Book piece (turn it with Gizmo to stand it up).");
            else
                One(UprightPrefab, UprightToken, UprightToken + "_desc", upright: true);
            BookNames.Apply();
        }
        catch (Exception ex)
        {
            _log?.LogError($"[Books] Failed to register the book piece: {ex}");
        }
    }

    static void One(string prefabName, string nameToken, string descToken, bool upright)
    {

            var config = new PieceConfig
            {
                Name = nameToken,
                Description = descToken,
                Enabled = true,
                PieceTable = "Hammer",
                Category = "Furniture",
                CraftingStation = "",
                Requirements = Array.Empty<RequirementConfig>(),
            };
            if (BookAssets.Icon is { } icon)
                config.Icon = icon;

            var piece = new CustomPiece(prefabName, "sign", config);
            PieceManager.Instance.AddPiece(piece);
            var go = piece.PiecePrefab;
            if (go == null)
                return;

            // The sheet-décor builder gives snap points, layer and placement rules; swap its paper for the book.
            PaperItem.BuildDecorSheetVisual(go, wall: false);
            foreach (Transform child in go.transform)
            {
                if (child.name == "drakes_paper_decor")
                {
                    Object.DestroyImmediate(child.gameObject);
                    break;
                }
            }

            // No snap points: the paper's were sheet-sized, and any point pulls the ghost onto a table's or floor's own points, so
            // it jumped about under the crosshair. Books go where you aim.
            for (var i = go.transform.childCount - 1; i >= 0; i--)
                if (go.transform.GetChild(i).CompareTag("snappoint"))
                    Object.DestroyImmediate(go.transform.GetChild(i).gameObject);

            foreach (var sign in go.GetComponentsInChildren<Sign>(true))
                Object.DestroyImmediate(sign);
            // The sign's wear keeps a list of its renderers; the book swaps its model after placing (Dress), and wear then read
            // bounds off a destroyed renderer (NullReferenceException in WearNTear.UpdateBiome). A book needs no wear or damage:
            // it can't be hammer-removed either, and breaking it would lose its text.
            foreach (var wear in go.GetComponentsInChildren<WearNTear>(true))
                Object.DestroyImmediate(wear);

            var p = go.GetComponent<Piece>();
            if (p != null)
            {
                p.m_name = nameToken;
                p.m_category = Piece.PieceCategory.Furniture;
                p.m_craftingStation = null;
                // Hammer removal would lose the book's text. It comes back through Take on the book window.
                p.m_canBeRemoved = false;
            }

            // The prefab (and the ghost until a book is picked) shows a medium book in the same pose as a placed one.
            BookPieceBehaviour.Dress(go.transform, new Dictionary<string, string>(), upright);
            go.AddComponent<BookPieceBehaviour>().Upright = upright;
            _log?.LogInfo($"[Books] Registered the Book hammer piece{(upright ? " (standing)" : "")}.");
    }

    internal static bool IsUpright(GameObject? go) => go != null && go.GetComponent<BookPieceBehaviour>() is { Upright: true };

    internal static bool IsBookPiece(Piece? piece) =>
        piece != null && piece.gameObject.name.StartsWith(PrefabName, StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// A placed book: remembers its data in the ZDO (<c>DrakeBookData</c>), draws its own colour and size, reads on Use, and can be taken
/// back. Does not implement Hoverable/Interactable (the CI stubs lack members Valheim 1.0 added, which breaks the type load);
/// <see cref="BookPiecePatches"/> routes hover and Use here. A piece that appears with no picked book is removed again, so the hammer cannot make blank free books.
/// </summary>
internal sealed class BookPieceBehaviour : MonoBehaviour
{
    const string ZData = "DrakeBookData";
    const string ZInit = "DrakeBookInit";
    const string ZOwner = "DrakeBookOwner";

    /// <summary>The standing piece: the book stands on its bottom edge, front cover forward, instead of lying on its back.</summary>
    public bool Upright;

    ZNetView? _nview;
    string _shown = "";
    Dictionary<string, string> _data = new();

    void Awake() => _nview = GetComponent<ZNetView>();

    void Start()
    {
        if (_nview == null || !_nview.IsValid())
            return;
        var zdo = _nview.GetZDO();
        if (!zdo.GetBool(ZInit, false))
        {
            if (!_nview.IsOwner())
                return;
            var player = Player.m_localPlayer;
            if (player == null || !BookPlaceState.TryTake(player, out var data, out var owner))
            {
                // No pick (or it no longer holds): take the piece back and ask again, so the next click places.
                ZNetScene.instance.Destroy(gameObject);
                if (player != null)
                {
                    ValheimHudMessage.Show(player, MessageHud.MessageType.Center, "Pick a book, then place it.");
                    BookPickerPanel.Show(player);
                }

                return;
            }

            zdo.Set(ZData, BookData.Pack(data));
            zdo.Set(ZOwner, owner);
            zdo.Set(ZInit, true);
        }

        Refresh();
        InvokeRepeating(nameof(Refresh), 2f, 2f);
    }

    /// <summary>Re-reads the ZDO (an edit by someone else) and redraws when it changed.</summary>
    void Refresh()
    {
        if (_nview == null || !_nview.IsValid())
            return;
        var packed = _nview.GetZDO().GetString(ZData, "");
        if (packed == _shown)
            return;
        _shown = packed;
        _data = BookData.Unpack(packed);
        Redraw();
    }

    void Redraw() => Dress(transform, _data, Upright);

    /// <summary>
    /// Draws a book lying on its back, front cover up, under <paramref name="root"/> (replacing any earlier drawing), and fits the
    /// collider. Used by the placed piece and by the placement preview.
    /// </summary>
    internal static void Dress(Transform root, Dictionary<string, string> data, bool upright = false)
    {
        for (var i = root.childCount - 1; i >= 0; i--)
            if (root.GetChild(i).name == "attach")
                Object.DestroyImmediate(root.GetChild(i).gameObject);

        var size = BookSizeOf(data);
        var d = BookMesh.Dimensions(size);
        var attach = new GameObject("attach").transform;
        attach.SetParent(root, false);
        if (upright)
        {
            // Standing on its bottom edge, front cover to the piece's front (+Z), spine to the right.
            attach.localRotation = Quaternion.Euler(0f, -90f, 0f);
            attach.localPosition = Vector3.zero;
        }
        else
        {
            // Lying on its back, front cover up (the mesh stands, spine -Z, up +Y).
            attach.localRotation = Quaternion.Euler(0f, 0f, 90f);
            // The book itself lies flush on the surface; only the collider floats (see Lift), so the build ghost isn't "blocked".
            attach.localPosition = new Vector3(d.y / 2f, d.z / 2f, 0f);
        }

        var title = data.TryGetValue(DrakeModsLibs.Data.DrakeCustomDataKeys.Rename, out var t) && t.Length > 0 ? t : null;
        BookAssets.Build(attach, size, data, title, frontTitle: true);
        // The build ghost sits on the ghost layer, which the placement ray skips. A fresh drawing on another layer was hit by
        // that ray, so the ghost climbed onto itself and jittered.
        SetLayer(attach, root.gameObject.layer);
        FitCollider(root.gameObject, size, upright);
    }

    /// <summary>
    /// The book rests this far above the aimed surface. A collider that touches the ground or the table top reads as "blocked" to
    /// the placement ghost (red, cannot place); paper floats the same way for the same reason.
    /// </summary>
    internal const float Lift = 0.03f;

    internal static void FitCollider(GameObject go, BookSize size, bool upright = false)
    {
        var d = BookMesh.Dimensions(size);
        var box = go.GetComponent<BoxCollider>() ?? go.AddComponent<BoxCollider>();
        if (upright)
        {
            box.center = new Vector3(0f, Lift + d.y / 2f, 0f);
            box.size = new Vector3(d.x, d.y, d.z);
            return;
        }

        box.center = new Vector3(0f, Lift + d.z / 2f, 0f);
        box.size = new Vector3(d.y, d.z, d.x);
    }

    static void SetLayer(Transform t, int layer)
    {
        t.gameObject.layer = layer;
        foreach (Transform child in t)
            SetLayer(child, layer);
    }

    static BookSize BookSizeOf(Dictionary<string, string> data) =>
        data.TryGetValue(BookData.KeySize, out var s) && int.TryParse(s, out var n) && n >= 0 && n <= 2 ? (BookSize)n : BookSize.Medium;

    static Color ColorOf(Dictionary<string, string> data) =>
        data.TryGetValue(BookData.KeyColor, out var c) && ColorUtility.TryParseHtmlString("#" + c, out var col) ? col : BookData.CoverColors[0];

    long OwnerId() => _nview != null && _nview.IsValid() ? _nview.GetZDO().GetLong(ZOwner, 0L) : 0L;

    bool IsOwner(Player? player) => player != null && (OwnerId() == 0L || OwnerId() == player.GetPlayerID());

    string Title() => _data.TryGetValue(DrakeModsLibs.Data.DrakeCustomDataKeys.Rename, out var t) && t.Length > 0 ? t : "Untitled book";

    bool Flag(string key, bool fallback) => _data.TryGetValue(key, out var v) && v.Length > 0 ? v == "1" : fallback;

    public string GetHoverName() => Title();

    public string GetHoverText()
    {
        var player = Player.m_localPlayer;
        var canRead = IsOwner(player) || Flag(BookData.KeyReadable, true);
        var text = Title() + (canRead ? "\n[<color=yellow><b>$KEY_Use</b></color>] Read" : "");
        if (MayTake(player))
            text += "\n[<color=yellow><b>$ui_hold $KEY_Use</b></color>] $piece_itemstand_take";
        if (IsOwner(player))
            text += "\n[<color=yellow><b>Shift</b></color>+<color=yellow><b>$KEY_Use</b></color>] Edit";
        return text;
    }

    /// <summary>
    /// Who may pick the book up: whoever placed it, anyone when its owner allows take, and (server option BookTakeWardAccess)
    /// anyone the ward around it lets in. Outside any ward that last rule doesn't apply.
    /// </summary>
    bool MayTake(Player? player)
    {
        if (player == null)
            return false;
        if (IsOwner(player) || Flag(BookData.KeyTake, false))
            return true;
        return RenameitConfig.BookTakeWardAccess && Compat.CompatibilityManager.IsInsideEnabledWard(transform.position)
               && Compat.CompatibilityManager.CheckAccess(transform.position, flash: false);
    }

    /// <summary>Use was pressed: read when it is let go quickly, take when it is held (see <see cref="Update"/>).</summary>
    float _readPendingSince = -1f;

    void Update()
    {
        if (_readPendingSince < 0f)
            return;
        // Still held: the hold (Interact with hold=true) decides. Let go before it came: it was a tap, so read.
        if (ZInput.GetButton("Use") || ZInput.GetButton("JoyUse"))
        {
            if (Time.time - _readPendingSince > 1.5f)
                _readPendingSince = -1f;
            return;
        }

        _readPendingSince = -1f;
        if (Player.m_localPlayer is { } player)
            Read(player, alt: false);
    }

    public bool Interact(Humanoid user, bool hold, bool alt)
    {
        if (user != Player.m_localPlayer || _nview == null || !_nview.IsValid())
            return false;
        var player = (Player)user;
        if (hold)
        {
            // Hold Use: take it, like a page off a wall.
            if (alt || !MayTake(player))
                return false;
            _readPendingSince = -1f;
            Take(player);
            return true;
        }

        if (!alt && MayTake(player))
        {
            // Wait for the key to come up: a tap reads, a hold takes.
            _readPendingSince = Time.time;
            return true;
        }

        return Read(player, alt);
    }

    bool Read(Player player, bool alt)
    {
        var owner = IsOwner(player);
        if (!owner && !Flag(BookData.KeyReadable, true))
        {
            ValheimHudMessage.Show(player, MessageHud.MessageType.Center, "This book is closed to readers.");
            return true;
        }

        var proto = ObjectDB.instance?.GetItemPrefab(BookItem.PrefabName)?.GetComponent<ItemDrop>()?.m_itemData;
        if (proto == null)
            return false;
        var temp = proto.Clone();
        temp.m_customData = new Dictionary<string, string>(_data);
        temp.m_crafterID = OwnerId();
        var canTake = owner || Flag(BookData.KeyTake, false);
        // E reads (with Edit on the strip for the owner); Shift+E opens the full editor.
        if (alt && owner)
            BookReaderPanel.ShowFull(temp, canEdit: true, onSave: Save, owner: true, onTake: canTake ? () => Take(player) : null);
        else
            BookReaderPanel.Show(temp, canEdit: owner, onSave: Save, owner: owner, onTake: canTake ? () => Take(player) : null);
        return true;
    }

    void Save(ItemDrop.ItemData edited)
    {
        if (_nview == null || !_nview.IsValid())
            return;
        _nview.ClaimOwnership();
        _nview.GetZDO().Set(ZData, BookData.Pack(edited.m_customData));
        _shown = "";
        Refresh();
    }

    void Take(Player player)
    {
        if (_nview == null || !_nview.IsValid())
            return;
        var proto = ObjectDB.instance?.GetItemPrefab(BookItem.PrefabName);
        var drop = proto != null ? proto.GetComponent<ItemDrop>() : null;
        if (drop == null)
            return;
        var item = drop.m_itemData.Clone();
        item.m_dropPrefab = proto;
        item.m_stack = 1;
        item.m_customData = new Dictionary<string, string>(_data);
        item.m_crafterID = OwnerId();
        if (!player.GetInventory().AddItem(item))
            ItemDrop.DropItem(item, 1, player.transform.position + player.transform.forward + Vector3.up, Quaternion.identity);
        _nview.ClaimOwnership();
        ZNetScene.instance.Destroy(gameObject);
    }
}
