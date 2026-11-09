using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DrakeRenameit.Books;

/// <summary>Marks one drawn spine so the crosshair can tell which book it is aimed at.</summary>
internal sealed class BookSpineTag : MonoBehaviour
{
    internal ItemDrop.ItemData? Item;
}

/// <summary>How one shelf row lays its things out. Slots and Free only apply when the shelf allows more than books.</summary>
internal enum ShelfRowMode
{
    /// <summary>Books stand edge to edge, spines out, in slot order (the default).</summary>
    Books = 0,
    /// <summary>Each item stands centred in the gap that matches its inventory slot.</summary>
    Slots = 1,
    /// <summary>Each item has its own spot along the shelf, plus turn, lift and size.</summary>
    Free = 2,
}

/// <summary>Where one item stands in a Free row: X is metres from the row's middle.</summary>
internal struct ShelfPose
{
    internal float X, Lift, Yaw, Scale;

    internal static ShelfPose At(float x) => new() { X = x, Lift = 0f, Yaw = 0f, Scale = 1f };
}

/// <summary>
/// A shelf's contents drawn on the shelf. Each row has a mode: Books packs spines edge to edge, Slots centres every item in the
/// gap of its inventory slot, Free uses a saved spot per item. Everything comes from the container's synced inventory and the
/// piece's ZDO, so every client draws its own copy. Rows run top first; slot order is left to right.
/// </summary>
internal sealed class BookShelfBehaviour : MonoBehaviour
{
    internal const string ZNonBooks = "DrakeShelfNonBooks";
    internal const string ZModes = "DrakeShelfModes";
    internal const string ZPoses = "DrakeShelfPoses";

    /// <summary>Wall shelf: one board, books stand on y = 0. Bookcase: rows on a 0.50 pitch.</summary>
    public bool IsWall;
    public float Width = 1f;
    /// <summary>Outer height of a bookcase (1 or 2 m); rows share it evenly.</summary>
    public float Height = 2f;
    public int ShelfRows = 1;
    /// <summary>Inventory rows per shelf row (a long shelf takes two).</summary>
    public int InvPerRow = 1;

    const float RefreshSeconds = 0.5f;
    const int Columns = 8;
    internal const float MaxLift = 0.30f;
    internal const float MinScale = 0.5f, MaxScale = 1.5f;

    Container? _container;
    Transform? _items;
    string _shown = "";
    float _next;

    void Awake() => _container = GetComponent<Container>();

    void Update()
    {
        if (Time.time < _next || _container == null || ZNet.instance == null || ZNet.instance.IsDedicated())
            return;
        _next = Time.time + RefreshSeconds;
        Refresh();
    }

    /// <summary>Every live shelf by its inventory, so the add filter can tell a shelf's inventory from a chest's.</summary>
    internal static readonly Dictionary<Inventory, BookShelfBehaviour> ByInventory = new();

    Inventory? _registered;

    ZDO? Zdo
    {
        get
        {
            var nview = GetComponent<ZNetView>();
            return nview != null && nview.IsValid() ? nview.GetZDO() : null;
        }
    }

    void Write(Action<ZDO> change)
    {
        var nview = GetComponent<ZNetView>();
        if (nview == null || !nview.IsValid())
            return;
        nview.ClaimOwnership();
        change(nview.GetZDO());
        _shown = "";
    }

    internal void SetNonBooks(bool on) => Write(z => z.Set(ZNonBooks, on));

    internal bool AllowsNonBooks()
    {
        if (!RenameitConfig.ShelfAllowNonBooks)
            return false;
        return Zdo?.GetBool(ZNonBooks, false) ?? false;
    }

    // ---- row modes and poses

    /// <summary>Usable length of a row (inside the sides, or between the wall shelf's ends).</summary>
    internal float Usable => IsWall ? Width - 0.06f : Width - 2f * BookMesh.CaseTh - 0.02f;

    /// <summary>Room above a row's board for books to stand in.</summary>
    internal float Clear => IsWall ? BookMesh.WallClear : BookMesh.Pitch(ShelfRows, Height) - BookMesh.CaseTh;

    internal float Depth => IsWall ? BookMesh.ShelfD : BookMesh.CaseD;

    /// <summary>Height of a shelf row's standing surface (row 0 is the top row).</summary>
    internal float RowFloor(int shelfRow) => IsWall ? 0f : BookMesh.CasePlinth + BookMesh.Pitch(ShelfRows, Height) * (ShelfRows - 1 - shelfRow);

    internal float SlotWidth => Usable / SlotsPerShelfRow;

    /// <summary>
    /// A shelved book's size, fitted to this shelf: height from the book's size (small / medium / large) as a share of the row's room,
    /// thickness from its page count (a one-page book is a little thinner than its slot, a full one a little fatter; packed rows are
    /// squeezed to fit). Depth follows the height, within the shelf.
    /// </summary>
    internal (float Depth, float Height, float Thickness) ShelfDims(ItemDrop.ItemData book)
    {
        var share = BookData.GetSize(book) switch { BookSize.Small => 0.74f, BookSize.Large => 0.94f, _ => 0.84f };
        var h = (Clear - 0.01f) * share;
        var d = Mathf.Min(h * 0.74f, Depth - 0.06f);
        var pages = BookData.GetPages(book).Count;
        var fill = Mathf.Clamp01((pages - 1f) / Mathf.Max(1f, BookData.MaxPages - 1f));
        return (d, h, SlotWidth * Mathf.Lerp(0.82f, 1.15f, fill));
    }

    internal int SlotsPerShelfRow => Columns * Mathf.Max(1, InvPerRow);

    /// <summary>The row's mode: Books (spines side by side, like a real shelf) unless the owner chose Slots or Free.</summary>
    internal ShelfRowMode GetMode(int shelfRow)
    {
        var parts = (Zdo?.GetString(ZModes, "") ?? "").Split(',');
        return shelfRow < parts.Length && int.TryParse(parts[shelfRow], NumberStyles.Integer, CultureInfo.InvariantCulture, out var m) && m >= 0 && m <= 2
            ? (ShelfRowMode)m
            : ShelfRowMode.Books;
    }

    /// <summary>Private in the live game: tells the container its contents changed (it saves to the ZDO when we own it).</summary>
    static readonly System.Reflection.MethodInfo? InventoryChangedMethod = HarmonyLib.AccessTools.Method(typeof(Inventory), "Changed");

    /// <summary>
    /// Calls the game's Inventory.Changed with its own defaults. Valheim 1.0 has Changed(bool success = false, bool
    /// cheatedStateChanged = false); the stubs have no parameters, so the argument list comes from the live method.
    /// </summary>
    internal static void NotifyInventoryChanged(Inventory inventory)
    {
        var method = InventoryChangedMethod;
        if (method == null)
            return;
        var parameters = method.GetParameters();
        var args = new object?[parameters.Length];
        for (var i = 0; i < parameters.Length; i++)
            args[i] = parameters[i].HasDefaultValue ? parameters[i].DefaultValue
                : parameters[i].ParameterType.IsValueType ? Activator.CreateInstance(parameters[i].ParameterType) : null;
        method.Invoke(inventory, args);
    }

    /// <summary>
    /// Move <paramref name="item"/> to slot <paramref name="slot"/> of shelf row <paramref name="shelfRow"/>, swapping with whatever is
    /// there, and save. False while someone else has the shelf open as a chest.
    /// </summary>
    internal bool MoveTo(ItemDrop.ItemData item, int shelfRow, int slot)
    {
        if (_container?.GetInventory() is not { } inventory || slot < 0 || slot >= SlotsPerShelfRow)
            return false;
        if (_container.IsInUse() && !InventoryGui.IsVisible())
            return false;
        var perRow = Mathf.Max(1, InvPerRow);
        var to = new Vector2i(slot % Columns, shelfRow * perRow + slot / Columns);
        var other = inventory.GetItemAt(to.x, to.y);
        var from = item.m_gridPos;
        item.m_gridPos = to;
        if (other != null && other != item)
            other.m_gridPos = from;
        SaveContents();
        return true;
    }

    /// <summary>Take the shelf's ownership and save its inventory, so every client draws the new order.</summary>
    internal void SaveContents()
    {
        var nview = GetComponent<ZNetView>();
        if (nview == null || !nview.IsValid() || _container?.GetInventory() is not { } inventory)
            return;
        nview.ClaimOwnership();
        NotifyInventoryChanged(inventory);
        _shown = "";
    }

    internal void SetMode(int shelfRow, ShelfRowMode mode) => Write(z =>
    {
        var parts = new string[ShelfRows];
        var current = z.GetString(ZModes, "").Split(',');
        for (var i = 0; i < ShelfRows; i++)
            parts[i] = i < current.Length && current[i].Length > 0 ? current[i] : "0";
        parts[shelfRow] = ((int)mode).ToString(CultureInfo.InvariantCulture);
        z.Set(ZModes, string.Join(",", parts));
    });

    /// <summary>The items in one shelf row, left to right (inventory row by row).</summary>
    internal List<ItemDrop.ItemData> RowItems(int shelfRow)
    {
        var row = new List<ItemDrop.ItemData>();
        if (_container?.GetInventory() is not { } inventory)
            return row;
        var perRow = Mathf.Max(1, InvPerRow);
        foreach (var item in inventory.GetAllItems())
            if (item != null && item.m_gridPos.y / perRow == shelfRow)
                row.Add(item);
        row.Sort((a, b) => (a.m_gridPos.y, a.m_gridPos.x).CompareTo((b.m_gridPos.y, b.m_gridPos.x)));
        return row;
    }

    /// <summary>The slot's own number inside its shelf row (0 .. SlotsPerShelfRow - 1).</summary>
    internal int SlotIndex(ItemDrop.ItemData item) => item.m_gridPos.x + Columns * (item.m_gridPos.y % Mathf.Max(1, InvPerRow));

    /// <summary>X (metres from the row's middle) of the centre of the item's slot.</summary>
    internal float SlotCentreX(ItemDrop.ItemData item)
    {
        var width = Usable / SlotsPerShelfRow;
        return -Usable / 2f + (SlotIndex(item) + 0.5f) * width;
    }

    internal static string PoseKey(ItemDrop.ItemData item) => item.m_gridPos.x + "_" + item.m_gridPos.y;

    /// <summary>The item's saved spot, or its slot's centre while it has none.</summary>
    internal ShelfPose GetPose(ItemDrop.ItemData item)
    {
        var key = PoseKey(item);
        foreach (var entry in (Zdo?.GetString(ZPoses, "") ?? "").Split('|'))
        {
            var eq = entry.IndexOf('=');
            if (eq <= 0 || entry.Substring(0, eq) != key)
                continue;
            var f = entry.Substring(eq + 1).Split(';');
            if (f.Length == 4 && TryF(f[0], out var x) && TryF(f[1], out var lift) && TryF(f[2], out var yaw) && TryF(f[3], out var scale))
                return Clamp(new ShelfPose { X = x, Lift = lift, Yaw = yaw, Scale = scale });
        }

        return ShelfPose.At(SlotCentreX(item));
    }

    static bool TryF(string s, out float v) => float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v);

    ShelfPose Clamp(ShelfPose p) => new()
    {
        X = Mathf.Clamp(p.X, -Usable / 2f, Usable / 2f),
        Lift = Mathf.Clamp(p.Lift, 0f, MaxLift),
        Yaw = p.Yaw,
        Scale = Mathf.Clamp(p.Scale, MinScale, MaxScale),
    };

    internal void SetPose(ItemDrop.ItemData item, ShelfPose pose)
    {
        var key = PoseKey(item);
        pose = Clamp(pose);
        Write(z =>
        {
            var entries = new List<string>();
            foreach (var entry in z.GetString(ZPoses, "").Split('|'))
            {
                var eq = entry.IndexOf('=');
                if (eq > 0 && entry.Substring(0, eq) != key)
                    entries.Add(entry);
            }

            string F(float v) => v.ToString("0.###", CultureInfo.InvariantCulture);
            entries.Add($"{key}={F(pose.X)};{F(pose.Lift)};{F(pose.Yaw)};{F(pose.Scale)}");
            z.Set(ZPoses, string.Join("|", entries));
        });
    }

    // ---- aiming

    /// <summary>The book spine under the crosshair on this shelf, if any.</summary>
    internal ItemDrop.ItemData? AimedBook()
    {
        var cam = Camera.main;
        if (cam == null)
            return null;
        var hits = Physics.RaycastAll(new Ray(cam.transform.position, cam.transform.forward), 8f);
        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (var hit in hits)
        {
            if (hit.collider.GetComponentInParent<Player>() != null)
                continue;
            if (hit.collider.GetComponentInParent<BookShelfBehaviour>() != this)
                return null;
            return hit.collider.GetComponent<BookSpineTag>()?.Item;
        }

        return null;
    }

    // ---- drawing

    void Refresh()
    {
        if (_container == null || _container.GetInventory() is not { } inventory)
            return;
        if (_registered != inventory)
        {
            if (_registered != null)
                ByInventory.Remove(_registered);
            _registered = inventory;
            ByInventory[inventory] = this;
        }

        var signature = Describe(inventory) + "#" + (Zdo?.GetString(ZModes, "") ?? "") + "#" + (Zdo?.GetString(ZPoses, "") ?? "") + "#" + AllowsNonBooks();
        if (signature == _shown)
            return;
        _shown = signature;

        if (_items != null)
            Destroy(_items.gameObject);
        _items = new GameObject("drake_shelf_items").transform;
        _items.SetParent(transform.Find(BookShelfPiece.FrameName) ?? transform, false);

        var depth = Depth;
        var usable = Usable;
        var slot = SlotWidth;
        for (var shelfRow = 0; shelfRow < ShelfRows; shelfRow++)
        {
            var row = RowItems(shelfRow);
            var floor = RowFloor(shelfRow);
            switch (GetMode(shelfRow))
            {
                case ShelfRowMode.Slots:
                    foreach (var item in row)
                        PlaceAt(item, SlotCentreX(item), floor, depth, 0f, 0f, 1f, slot * 0.92f, slot * 0.92f);
                    break;
                case ShelfRowMode.Free:
                    foreach (var item in row)
                    {
                        var pose = GetPose(item);
                        PlaceAt(item, pose.X, floor, depth, pose.Lift, pose.Yaw, pose.Scale, slot * 1.5f, float.MaxValue);
                    }

                    break;
                default:
                {
                    // Side by side from the left. When the row would overflow, every book and gap is squeezed by the same share.
                    var widths = new List<float>();
                    foreach (var item in row)
                        widths.Add(BookItem.IsBook(item) ? ShelfDims(item).Thickness : slot);
                    var gap = slot * 0.05f;
                    var total = gap * Mathf.Max(0, row.Count - 1);
                    foreach (var w in widths)
                        total += w;
                    var squeeze = total > usable ? usable / total : 1f;
                    var cursor = -usable / 2f;
                    for (var i = 0; i < row.Count; i++)
                    {
                        var w = widths[i] * squeeze;
                        PlaceAt(row[i], cursor + w / 2f, floor, depth, 0f, 0f, 1f, w, w);
                        cursor += w + gap * squeeze;
                    }

                    break;
                }
            }
        }
    }

    static string Describe(Inventory inventory)
    {
        var sb = new StringBuilder();
        foreach (var item in inventory.GetAllItems())
        {
            sb.Append(item.m_dropPrefab != null ? item.m_dropPrefab.name : "?").Append('|').Append(item.m_gridPos.x).Append(',').Append(item.m_gridPos.y);
            if (item.m_customData != null)
                sb.Append('|').Append(BookData.Pack(item.m_customData).GetHashCode());
            sb.Append(';');
        }

        return sb.ToString();
    }

    /// <summary>
    /// Puts one item on the shelf with its middle at <paramref name="centreX"/>. A book is at most <paramref name="bookWidth"/> thick;
    /// anything else is shrunk to <paramref name="maxWidth"/> across.
    /// </summary>
    void PlaceAt(ItemDrop.ItemData item, float centreX, float floor, float shelfDepth, float lift, float yaw, float scale, float maxWidth, float bookWidth)
    {
        if (BookItem.IsBook(item))
            PlaceBook(item, centreX, floor, shelfDepth, lift, yaw, scale, bookWidth);
        else
            PlaceOther(item, centreX, floor, shelfDepth, lift, yaw, scale, maxWidth, false);
    }

    void PlaceBook(ItemDrop.ItemData item, float centreX, float floor, float shelfDepth, float lift, float yaw, float scale, float maxThickness)
    {
        var (d, h, t) = ShelfDims(item);
        t = Mathf.Min(t, maxThickness);
        var holder = new GameObject("book").transform;
        holder.SetParent(_items, false);
        holder.localScale = Vector3.one * scale;
        holder.localRotation = Quaternion.Euler(0f, yaw, 0f);
        // Spines line up near the front edge of the shelf.
        holder.localPosition = new Vector3(centreX, floor + lift, -shelfDepth / 2f + 0.02f + d * scale / 2f);
        BookAssets.Build(holder, d, h, t, item.m_customData, BookData.DisplayTitle(item));

        var box = holder.gameObject.AddComponent<BoxCollider>();
        box.center = new Vector3(0f, h / 2f, 0f);
        box.size = new Vector3(t, h, d);
        var layer = LayerMask.NameToLayer("piece");
        if (layer >= 0)
            holder.gameObject.layer = layer;
        holder.gameObject.AddComponent<BookSpineTag>().Item = item;
    }

    float PlaceOther(ItemDrop.ItemData item, float centreX, float floor, float shelfDepth, float lift, float yaw, float scale, float maxWidth, bool packed)
    {
        var maxHeight = Clear - 0.03f;
        var model = item.m_dropPrefab != null ? ShelfItemModel.StillCopy(item.m_dropPrefab) : null;
        if (model == null || !ShelfItemModel.LocalBounds(model.transform, out var bounds))
        {
            if (model != null)
                Destroy(model);
            return 0.04f;
        }

        var fit = Mathf.Min(1f, maxWidth / Mathf.Max(bounds.size.x, 0.001f), maxHeight / Mathf.Max(bounds.size.y, 0.001f),
            (shelfDepth - 0.05f) / Mathf.Max(bounds.size.z, 0.001f));
        var total = fit * scale;
        var width = bounds.size.x * total;
        model.transform.SetParent(_items, false);
        model.transform.localScale = Vector3.one * total;
        model.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        var centred = new Vector3(centreX, floor + lift, -shelfDepth / 2f + 0.03f + bounds.size.z * total / 2f);
        model.transform.localPosition = centred - model.transform.localRotation * (bounds.center * total) + new Vector3(0f, bounds.size.y * total / 2f, 0f);
        model.SetActive(true);
        return width;
    }

    void OnDestroy()
    {
        if (_registered != null)
            ByInventory.Remove(_registered);
        if (_items != null)
            Destroy(_items.gameObject);
    }
}

/// <summary>A harmless copy of an item's dropped model for display: no scripts, physics or sound. Same idea as the shop's display case.</summary>
internal static class ShelfItemModel
{
    internal static GameObject? StillCopy(GameObject prefab)
    {
        var wasActive = prefab.activeSelf;
        prefab.SetActive(false);
        GameObject copy;
        try
        {
            copy = Object.Instantiate(prefab);
        }
        finally
        {
            prefab.SetActive(wasActive);
        }

        copy.name = prefab.name + "_shelf";
        foreach (var mb in copy.GetComponentsInChildren<MonoBehaviour>(true))
            if (mb != null)
                Object.DestroyImmediate(mb);
        foreach (var nview in copy.GetComponentsInChildren<ZNetView>(true))
            Object.DestroyImmediate(nview);
        foreach (var lods in copy.GetComponentsInChildren<LODGroup>(true))
            Object.DestroyImmediate(lods);
        foreach (var joint in copy.GetComponentsInChildren<Joint>(true))
            Object.DestroyImmediate(joint);
        foreach (var body in copy.GetComponentsInChildren<Rigidbody>(true))
            Object.DestroyImmediate(body);
        foreach (var collider in copy.GetComponentsInChildren<Collider>(true))
            Object.DestroyImmediate(collider);
        foreach (var audio in copy.GetComponentsInChildren<AudioSource>(true))
            Object.DestroyImmediate(audio);
        foreach (var particles in copy.GetComponentsInChildren<ParticleSystem>(true))
        {
            if (particles.GetComponent<ParticleSystemRenderer>() is { } r)
                Object.DestroyImmediate(r);
            Object.DestroyImmediate(particles);
        }

        return copy;
    }

    /// <summary>Bounds of the copy's meshes in its own root space (renderer bounds are empty while it is inactive).</summary>
    internal static bool LocalBounds(Transform root, out Bounds bounds)
    {
        bounds = default;
        var any = false;
        var toRoot = root.worldToLocalMatrix;
        foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
        {
            var mesh = renderer.GetComponent<MeshFilter>()?.sharedMesh;
            if (mesh == null || !renderer.enabled)
                continue;
            var matrix = toRoot * renderer.transform.localToWorldMatrix;
            var b = mesh.bounds;
            for (var corner = 0; corner < 8; corner++)
            {
                var p = matrix.MultiplyPoint3x4(b.center + Vector3.Scale(b.extents,
                    new Vector3((corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1)));
                if (!any)
                {
                    bounds = new Bounds(p, Vector3.zero);
                    any = true;
                }
                else
                {
                    bounds.Encapsulate(p);
                }
            }
        }

        return any;
    }
}
