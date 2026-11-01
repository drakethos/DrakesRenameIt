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
    public float Width = 1.1f;
    public int ShelfRows = 1;
    /// <summary>Inventory rows per shelf row (a long shelf takes two).</summary>
    public int InvPerRow = 1;

    const float Gap = 0.004f;
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
    internal float Usable => IsWall ? Width - 0.12f : Width - 2f * BookMesh.CaseTh - 0.06f;

    internal int SlotsPerShelfRow => Columns * Mathf.Max(1, InvPerRow);

    /// <summary>The row's mode: Slots unless the owner chose Free. (Books is an old value, read as Slots.)</summary>
    internal ShelfRowMode GetMode(int shelfRow)
    {
        var parts = (Zdo?.GetString(ZModes, "") ?? "").Split(',');
        return shelfRow < parts.Length && int.TryParse(parts[shelfRow], NumberStyles.Integer, CultureInfo.InvariantCulture, out var m) && m >= 0 && m <= 2
            ? (m == (int)ShelfRowMode.Free ? ShelfRowMode.Free : ShelfRowMode.Slots)
            : ShelfRowMode.Slots;
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

        var depth = IsWall ? BookMesh.ShelfD : BookMesh.CaseD;
        var usable = Usable;
        for (var shelfRow = 0; shelfRow < ShelfRows; shelfRow++)
        {
            var row = RowItems(shelfRow);
            var floor = IsWall ? 0f : BookMesh.RowY(ShelfRows - 1 - shelfRow);
            switch (GetMode(shelfRow))
            {
                case ShelfRowMode.Slots:
                {
                    var slotWidth = usable / SlotsPerShelfRow;
                    foreach (var item in row)
                        PlaceAt(item, SlotCentreX(item), floor, depth, 0f, 0f, 1f, Mathf.Min(0.16f, slotWidth * 0.95f));
                    break;
                }
                case ShelfRowMode.Free:
                    foreach (var item in row)
                    {
                        var pose = GetPose(item);
                        PlaceAt(item, pose.X, floor, depth, pose.Lift, pose.Yaw, pose.Scale, 0.16f);
                    }

                    break;
                default:
                {
                    var cursor = -usable / 2f;
                    foreach (var item in row)
                    {
                        var thick = BookItem.IsBook(item) ? BookMesh.Dimensions(BookData.GetSize(item)).z : 0.12f;
                        var width = PlaceAt(item, cursor + thick / 2f, floor, depth, 0f, 0f, 1f, 0.16f, packed: true);
                        cursor += width + Gap;
                        if (cursor > usable / 2f)
                            break;
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
    /// Puts one item on the shelf with its middle at <paramref name="centreX"/>. Returns the width it takes (for packing rows).
    /// <paramref name="packed"/> lets a non-book in a Books row take the width of its own model.
    /// </summary>
    float PlaceAt(ItemDrop.ItemData item, float centreX, float floor, float shelfDepth, float lift, float yaw, float scale, float maxWidth, bool packed = false)
    {
        return BookItem.IsBook(item)
            ? PlaceBook(item, centreX, floor, shelfDepth, lift, yaw, scale)
            : PlaceOther(item, centreX, floor, shelfDepth, lift, yaw, scale, maxWidth, packed);
    }

    float PlaceBook(ItemDrop.ItemData item, float centreX, float floor, float shelfDepth, float lift, float yaw, float scale)
    {
        var size = BookData.GetSize(item);
        var d = BookMesh.Dimensions(size);
        var holder = new GameObject("book").transform;
        holder.SetParent(_items, false);
        holder.localScale = Vector3.one * scale;
        holder.localRotation = Quaternion.Euler(0f, yaw, 0f);
        holder.localPosition = new Vector3(centreX, floor + lift, -shelfDepth / 2f + 0.015f + d.x * scale / 2f);
        BookAssets.Build(holder, size, BookData.GetColor(item), BookData.Get(item, BookData.KeyCover));

        var box = holder.gameObject.AddComponent<BoxCollider>();
        box.center = new Vector3(0f, d.y / 2f, 0f);
        box.size = new Vector3(d.z, d.y, d.x);
        var layer = LayerMask.NameToLayer("piece");
        if (layer >= 0)
            holder.gameObject.layer = layer;
        holder.gameObject.AddComponent<BookSpineTag>().Item = item;
        return d.z * scale;
    }

    float PlaceOther(ItemDrop.ItemData item, float centreX, float floor, float shelfDepth, float lift, float yaw, float scale, float maxWidth, bool packed)
    {
        var maxHeight = IsWall ? 0.30f : BookMesh.CasePitch - BookMesh.CaseTh - 0.05f;
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
