using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace DrakeRenameit.Books;

/// <summary>Marks one drawn spine so the crosshair can tell which book it is aimed at.</summary>
internal sealed class BookSpineTag : MonoBehaviour
{
    internal ItemDrop.ItemData? Item;
}

/// <summary>
/// A shelf's contents drawn on the shelf: books stand edge to edge with their spines out, other items (when the shelf allows them)
/// stand in their own gap. Everything comes from the container's synced inventory, so every client draws its own copy. Rows are
/// the inventory's rows, top row first; slot order is left to right.
/// </summary>
internal sealed class BookShelfBehaviour : MonoBehaviour
{
    internal const string ZNonBooks = "DrakeShelfNonBooks";

    /// <summary>Wall shelf: one board, books stand on y = 0. Bookcase: three rows on a 0.50 pitch.</summary>
    internal bool IsWall;

    const float Gap = 0.004f;
    const float RefreshSeconds = 0.5f;

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

    internal void SetNonBooks(bool on)
    {
        var nview = GetComponent<ZNetView>();
        if (nview == null || !nview.IsValid())
            return;
        nview.ClaimOwnership();
        nview.GetZDO().Set(ZNonBooks, on);
    }

    /// <summary>The book spine under the crosshair on this shelf, if any.</summary>
    internal ItemDrop.ItemData? AimedBook()
    {
        var cam = Camera.main;
        if (cam == null)
            return null;
        var hits = Physics.RaycastAll(new Ray(cam.transform.position, cam.transform.forward), 8f);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
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

    internal bool AllowsNonBooks()
    {
        if (!RenameitConfig.ShelfAllowNonBooks)
            return false;
        var nview = GetComponent<ZNetView>();
        return nview != null && nview.IsValid() && nview.GetZDO().GetBool(ZNonBooks, false);
    }

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

        var signature = Describe(inventory);
        if (signature == _shown)
            return;
        _shown = signature;

        if (_items != null)
            Destroy(_items.gameObject);
        _items = new GameObject("drake_shelf_items").transform;
        _items.SetParent(transform.Find(BookShelfPiece.FrameName) ?? transform, false);

        var rows = Mathf.Max(1, inventory.GetHeight());
        var depth = IsWall ? BookMesh.ShelfD : BookMesh.CaseD;
        var usable = IsWall ? BookMesh.ShelfW - 0.12f : BookMesh.CaseW - 2f * BookMesh.CaseTh - 0.06f;
        for (var y = 0; y < rows; y++)
        {
            var row = new List<ItemDrop.ItemData>();
            foreach (var item in inventory.GetAllItems())
                if (item != null && item.m_gridPos.y == y)
                    row.Add(item);
            row.Sort((a, b) => a.m_gridPos.x.CompareTo(b.m_gridPos.x));

            var floor = IsWall ? 0f : BookMesh.RowY(rows - 1 - y);
            var cursor = -usable / 2f;
            foreach (var item in row)
            {
                var width = BookItem.IsBook(item) ? PlaceBook(item, cursor, floor, depth) : PlaceOther(item, cursor, floor, depth);
                cursor += width + Gap;
                if (cursor > usable / 2f)
                    break;
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

    /// <summary>Stands a book spine-out at <paramref name="x"/> (its left edge). Returns the width it used.</summary>
    float PlaceBook(ItemDrop.ItemData item, float x, float floor, float shelfDepth)
    {
        var size = BookData.GetSize(item);
        var d = BookMesh.Dimensions(size);
        var holder = new GameObject("book").transform;
        holder.SetParent(_items, false);
        holder.localPosition = new Vector3(x + d.z / 2f, floor, -shelfDepth / 2f + 0.015f + d.x / 2f);
        BookAssets.Build(holder, size, BookData.GetColor(item));

        var box = holder.gameObject.AddComponent<BoxCollider>();
        box.center = new Vector3(0f, d.y / 2f, 0f);
        box.size = new Vector3(d.z, d.y, d.x);
        var layer = LayerMask.NameToLayer("piece");
        if (layer >= 0)
            holder.gameObject.layer = layer;
        holder.gameObject.AddComponent<BookSpineTag>().Item = item;
        return d.z;
    }

    /// <summary>A non-book standing in its own gap (only drawn when the shelf allows them). Returns the width it used.</summary>
    float PlaceOther(ItemDrop.ItemData item, float x, float floor, float shelfDepth)
    {
        const float maxWidth = 0.16f;
        var maxHeight = IsWall ? 0.30f : BookMesh.CasePitch - BookMesh.CaseTh - 0.05f;
        var model = item.m_dropPrefab != null ? ShelfItemModel.StillCopy(item.m_dropPrefab) : null;
        if (model == null || !ShelfItemModel.LocalBounds(model.transform, out var bounds))
        {
            if (model != null)
                Destroy(model);
            return 0.04f;
        }

        var scale = Mathf.Min(1f, maxWidth / Mathf.Max(bounds.size.x, 0.001f), maxHeight / Mathf.Max(bounds.size.y, 0.001f),
            (shelfDepth - 0.05f) / Mathf.Max(bounds.size.z, 0.001f));
        var width = bounds.size.x * scale;
        model.transform.SetParent(_items, false);
        model.transform.localScale = Vector3.one * scale;
        model.transform.localPosition = new Vector3(x + width / 2f, floor, -shelfDepth / 2f + 0.03f + bounds.size.z * scale / 2f)
                                        - bounds.center * scale + new Vector3(0f, bounds.size.y * scale / 2f, 0f);
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
