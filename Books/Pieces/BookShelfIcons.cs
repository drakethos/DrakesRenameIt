using System.Collections.Generic;
using System.Globalization;
using Jotunn.Managers;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DrakeRenameit.Books;

/// <summary>
/// Hammer icons for the shelves and bookcases, rendered when they register from the piece itself (Jotunn's RenderManager, as the
/// shop's crates do) with sample books packed on every row. They follow the real case: the old pictures came from the first
/// mock-up, which had dividers and other row counts.
/// </summary>
internal static class BookShelfIcons
{
    /// <summary>The rendered icons by prefab name (tests save them to look at).</summary>
    internal static readonly Dictionary<string, Sprite> Made = new();

    /// <summary>
    /// Renders <paramref name="prefab"/> (looked at from the front, turned by <paramref name="yaw"/> and tipped so the top shows)
    /// with books on it. Null when the render failed; the caller keeps the PNG icon then.
    /// </summary>
    internal static Sprite? Render(GameObject prefab, BookShelfPiece.Variant v, float yaw = 28f)
    {
        GameObject? target = null;
        try
        {
            target = MeshOnlyCopy(prefab);
            var frame = Find(target.transform, BookShelfPiece.FrameName);
            if (frame != null)
                AddBooks(frame, v);
            var request = new RenderManager.RenderRequest(target)
            {
                Width = 128,
                Height = 128,
                Rotation = Quaternion.Euler(14f, 0f, 0f) * Quaternion.Euler(0f, yaw, 0f),
            };
            var sprite = RenderManager.Instance.Render(request);
            if (sprite != null)
            {
                sprite.name = "drakes_shelf_icon_" + prefab.name;
                Made[prefab.name] = sprite;
            }

            return sprite;
        }
        catch (System.Exception ex)
        {
            RenameitConfig.Log?.LogWarning($"[Books] Icon render for {prefab.name} skipped: {ex.Message}");
            return null;
        }
        finally
        {
            if (target != null)
                Object.DestroyImmediate(target);
        }
    }

    /// <summary>Rows of made-up books, packed like a full shelf (a little room left at the end), seeded so the icon never changes.</summary>
    static void AddBooks(Transform frame, BookShelfPiece.Variant v)
    {
        var rng = new System.Random(v.Prefab.GetHashCode() & 0x7fff);
        var usable = v.Wall ? v.Width - 0.06f : v.Width - 2f * BookMesh.CaseTh - 0.02f;
        var clear = v.Wall ? BookMesh.WallClear : BookMesh.Pitch(v.ShelfRows, v.Height) - BookMesh.CaseTh;
        var depth = v.Wall ? BookMesh.ShelfD : BookMesh.CaseD;
        var slot = usable / (8 * Mathf.Max(1, v.InvPerRow));
        var books = new GameObject("drakes_icon_books").transform;
        books.SetParent(frame, false);
        for (var row = 0; row < v.ShelfRows; row++)
        {
            var floor = v.Wall ? 0f : BookMesh.CasePlinth + BookMesh.Pitch(v.ShelfRows, v.Height) * (v.ShelfRows - 1 - row);
            var cursor = -usable / 2f;
            var end = usable / 2f - slot * (0.4f + (float)rng.NextDouble() * 1.2f);
            while (true)
            {
                var roll = rng.Next(10);
                var share = roll < 2 ? 0.74f : roll > 7 ? 0.94f : 0.84f;
                var h = (clear - 0.01f) * share;
                var d = Mathf.Min(h * 0.74f, depth - 0.06f);
                var t = slot * Mathf.Lerp(0.82f, 1.15f, (float)rng.NextDouble());
                if (cursor + t > end)
                    break;
                var data = new Dictionary<string, string>
                {
                    [BookData.KeyColor] = ColorUtility.ToHtmlStringRGB(BookLook.Covers[rng.Next(BookLook.Covers.Length)]),
                    [BookLook.KeyPattern] = rng.Next((int)BookPattern.Studs + 1).ToString(CultureInfo.InvariantCulture),
                    [BookLook.KeyAccent] = ColorUtility.ToHtmlStringRGB(BookLook.Accents[rng.Next(BookLook.Accents.Length)]),
                };
                var holder = new GameObject("book").transform;
                holder.SetParent(books, false);
                holder.localPosition = new Vector3(cursor + t / 2f, floor, -depth / 2f + 0.02f + d / 2f);
                // No titles: at 128 px lettering is only noise.
                BookAssets.Build(holder, d, h, t, data, null);
                cursor += t + slot * 0.05f;
            }
        }
    }

    static Transform? Find(Transform root, string name)
    {
        if (root.name == name)
            return root;
        foreach (Transform child in root)
            if (Find(child, name) is { } hit)
                return hit;
        return null;
    }

    /// <summary>
    /// RenderManager skips inactive targets, and Jotunn keeps its prefabs under an inactive container: a mesh-only copy, made under
    /// an inactive parent so nothing on it wakes up (same as the shop's PieceIcons).
    /// </summary>
    static GameObject MeshOnlyCopy(GameObject source)
    {
        var parent = new GameObject("drakes_icon_parent");
        parent.SetActive(false);
        var copy = Object.Instantiate(source, parent.transform);
        foreach (var behaviour in copy.GetComponentsInChildren<MonoBehaviour>(true))
            Object.DestroyImmediate(behaviour);
        foreach (var body in copy.GetComponentsInChildren<Rigidbody>(true))
            Object.DestroyImmediate(body);
        foreach (var component in copy.GetComponentsInChildren<Component>(true))
            if (component is not (Transform or MeshFilter or MeshRenderer))
                Object.DestroyImmediate(component);
        copy.transform.SetParent(null, false);
        copy.transform.position = new Vector3(0f, -5000f, 0f);
        Object.DestroyImmediate(parent);
        copy.SetActive(true);
        return copy;
    }
}
