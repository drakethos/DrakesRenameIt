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
    const string NameToken = "$piece_drakes_book";
    const string DescToken = "$piece_drakes_book_desc";

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

            var config = new PieceConfig
            {
                Name = NameToken,
                Description = DescToken,
                Enabled = true,
                PieceTable = "Hammer",
                Category = "Furniture",
                CraftingStation = "",
                Requirements = Array.Empty<RequirementConfig>(),
            };
            if (BookAssets.Icon is { } icon)
                config.Icon = icon;

            var piece = new CustomPiece(PrefabName, "sign", config);
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

            foreach (var sign in go.GetComponentsInChildren<Sign>(true))
                Object.DestroyImmediate(sign);

            var p = go.GetComponent<Piece>();
            if (p != null)
            {
                p.m_name = NameToken;
                p.m_category = Piece.PieceCategory.Furniture;
                p.m_craftingStation = null;
                // Hammer removal would lose the book's text. It comes back through Take on the book window.
                p.m_canBeRemoved = false;
            }

            BookPieceBehaviour.FitCollider(go, BookSize.Medium);
            // The ghost (and the prefab) show a medium book floating at the same height as a placed one.
            var ghost = new GameObject("attach").transform;
            ghost.SetParent(go.transform, false);
            var d = BookMesh.Dimensions(BookSize.Medium);
            ghost.localRotation = Quaternion.Euler(0f, 0f, 90f);
            ghost.localPosition = new Vector3(d.y / 2f, BookPieceBehaviour.Lift + d.z / 2f, 0f);
            BookAssets.Build(ghost, BookSize.Medium, BookData.CoverColors[0]);
            go.AddComponent<BookPieceBehaviour>();
            _log?.LogInfo("[Books] Registered the Book hammer piece.");
        }
        catch (Exception ex)
        {
            _log?.LogError($"[Books] Failed to register the book piece: {ex}");
        }
    }

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

    void Redraw() => Dress(transform, _data);

    /// <summary>
    /// Draws a book lying on its back, front cover up, under <paramref name="root"/> (replacing any earlier drawing), and fits the
    /// collider. Used by the placed piece and by the placement preview.
    /// </summary>
    internal static void Dress(Transform root, Dictionary<string, string> data)
    {
        for (var i = root.childCount - 1; i >= 0; i--)
            if (root.GetChild(i).name == "attach")
                Object.DestroyImmediate(root.GetChild(i).gameObject);

        var size = BookSizeOf(data);
        var d = BookMesh.Dimensions(size);
        var attach = new GameObject("attach").transform;
        attach.SetParent(root, false);
        // Lying on its back, front cover up (the mesh stands, spine -Z, up +Y).
        attach.localRotation = Quaternion.Euler(0f, 0f, 90f);
        attach.localPosition = new Vector3(d.y / 2f, Lift + d.z / 2f, 0f);
        BookAssets.Build(attach, size, ColorOf(data), data.TryGetValue(BookData.KeyCover, out var cover) ? cover : null);
        FitCollider(root.gameObject, size);
    }

    /// <summary>
    /// The book rests this far above the aimed surface. A collider that touches the ground or the table top reads as "blocked" to
    /// the placement ghost (red, cannot place); paper floats the same way for the same reason.
    /// </summary>
    internal const float Lift = 0.03f;

    internal static void FitCollider(GameObject go, BookSize size)
    {
        var d = BookMesh.Dimensions(size);
        var box = go.GetComponent<BoxCollider>() ?? go.AddComponent<BoxCollider>();
        box.center = new Vector3(0f, Lift + d.z / 2f, 0f);
        box.size = new Vector3(d.y, d.z, d.x);
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
        return Title() + (canRead ? "\n[<color=yellow><b>$KEY_Use</b></color>] Read" : "");
    }

    public bool Interact(Humanoid user, bool hold, bool alt)
    {
        if (hold || user != Player.m_localPlayer || _nview == null || !_nview.IsValid())
            return false;
        var player = (Player)user;
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
