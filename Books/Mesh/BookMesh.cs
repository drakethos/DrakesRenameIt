using System.Collections.Generic;
using UnityEngine;

namespace DrakeRenameit.Books;

public enum BookSize { Small, Medium, Large }

/// <summary>
/// Low-poly book, wall shelf and bookcase meshes, built in code so no asset bundle is needed. Mirrors
/// <c>Forge/Books/mesh_mockup/make_book_meshes.py</c> number for number (metres, Y up here; Blender is Z up).
/// Books stand upright with the spine facing -Z, width (thickness) along X. Shelves face -Z, wall at +Z.
/// </summary>
internal static class BookMesh
{
    // Cover depth, height, thickness. Paper sheet is 0.216 x 0.279, so medium is about 1.25x.
    static readonly Vector3[] Sizes = { new(0.21f, 0.29f, 0.040f), new(0.26f, 0.36f, 0.050f), new(0.33f, 0.45f, 0.065f) };
    const float Board = 0.005f;
    const float PageInset = 0.006f;

    // Bookcase: depth, board thickness, plinth. Width, height and rows come from each bookcase (whole metres, to fit the build grid).
    // Footprints are whole half-metres (like the shop's crates), so they tile floors: 1 x 0.5 m, 2 x 0.5 m; wall shelves 0.25 m deep.
    internal const float CaseD = 0.50f, CaseTh = 0.03f, CasePlinth = 0.08f;
    internal const float ShelfW = 1.00f, ShelfD = 0.25f, ShelfTh = 0.03f;

    /// <summary>Room above a wall shelf's board that books are sized to.</summary>
    internal const float WallClear = 0.36f;

    static readonly Dictionary<BookSize, Mesh> Books = new();
    static readonly Dictionary<string, Mesh> Shelves = new();

    static readonly Dictionary<BookSize, Mesh> CoverQuads = new();

    /// <summary>A flat picture on the front cover (+X side), spine on its left; just proud of the board so it never z-fights.</summary>
    internal static Mesh CoverQuad(BookSize size)
    {
        if (CoverQuads.TryGetValue(size, out var cached) && cached != null)
            return cached;
        var s = Sizes[(int)size];
        float d = s.x, h = s.y, t = s.z;
        var x = t / 2 + 0.0008f;
        float z0 = -d / 2 + Board, z1 = d / 2 - PageInset / 2f;
        var mesh = new Mesh { name = "drakes_book_cover_picture" };
        mesh.SetVertices(new List<Vector3> { new(x, 0.01f, z0), new(x, h - 0.01f, z0), new(x, h - 0.01f, z1), new(x, 0.01f, z1) });
        mesh.SetUVs(0, new List<Vector2> { new(0, 0), new(0, 1), new(1, 1), new(1, 0) });
        mesh.SetTriangles(new List<int> { 0, 1, 2, 0, 2, 3 }, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return CoverQuads[size] = mesh;
    }

    static readonly Dictionary<string, Mesh> CustomQuads = new();

    /// <summary><see cref="CoverQuad"/> for a book of any size.</summary>
    internal static Mesh CoverQuadFor(float d, float h, float t)
    {
        var key = d.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture) + "x" + h.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture)
                  + "x" + t.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture);
        if (CustomQuads.TryGetValue(key, out var cached) && cached != null)
            return cached;
        var x = t / 2 + 0.0008f;
        float z0 = -d / 2 + Board * 2f, z1 = d / 2 - PageInset / 2f;
        var mesh = new Mesh { name = "drakes_book_cover_picture" };
        mesh.SetVertices(new List<Vector3> { new(x, 0.01f, z0), new(x, h - 0.01f, z0), new(x, h - 0.01f, z1), new(x, 0.01f, z1) });
        mesh.SetUVs(0, new List<Vector2> { new(0, 0), new(0, 1), new(1, 1), new(1, 0) });
        mesh.SetTriangles(new List<int> { 0, 1, 2, 0, 2, 3 }, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return CustomQuads[key] = mesh;
    }

    internal static Vector3 Dimensions(BookSize size) => Sizes[(int)size];

    /// <summary>Three submeshes: 0 = covers (cover texture), 1 = page block (paper), 2 = spine (spine texture). Origin at the foot of the spine's centre.</summary>
    internal static Mesh Book(BookSize size)
    {
        var s = Sizes[(int)size];
        return Custom(s.x, s.y, s.z);
    }

    static readonly Dictionary<string, Mesh> CustomBooks = new();

    /// <summary>
    /// A book of any depth <paramref name="d"/>, height <paramref name="h"/> and thickness <paramref name="t"/> (shelves size books to
    /// their row). Sizes are rounded to millimetres so books of the same shape share one mesh.
    /// </summary>
    internal static Mesh Custom(float d, float h, float t)
    {
        d = Mathf.Round(d * 1000f) / 1000f;
        h = Mathf.Round(h * 1000f) / 1000f;
        t = Mathf.Max(0.012f, Mathf.Round(t * 1000f) / 1000f);
        var key = d.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture) + "x" + h.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture)
                  + "x" + t.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture);
        if (CustomBooks.TryGetValue(key, out var cached) && cached != null)
            return cached;
        var covers = new Boxes();
        covers.Add(-t / 2, -t / 2 + Board, 0, h, -d / 2 + Board, d / 2);
        covers.Add(t / 2 - Board, t / 2, 0, h, -d / 2 + Board, d / 2);
        var pages = new Boxes();
        pages.Add(-t / 2 + Board, t / 2 - Board, PageInset, h - PageInset, -d / 2 + Board, d / 2 - PageInset);
        var spine = new Boxes();
        spine.Add(-t / 2, t / 2, 0, h, -d / 2, -d / 2 + Board);   // spine faces -Z
        return CustomBooks[key] = Boxes.Combine("drakes_book_" + key, covers, pages, spine);
    }

    /// <summary>One board on brackets with a back rail, <paramref name="w"/> wide. Origin at the top of the board, centred; wall at +Z.</summary>
    internal static Mesh WallShelf(float w = ShelfW)
    {
        var key = "wall" + w.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
        if (Shelves.TryGetValue(key, out var cached) && cached != null)
            return cached;
        float d = ShelfD, th = ShelfTh;
        var b = new Boxes { TileUv = true };
        b.Add(-w / 2, w / 2, -th, 0, -d / 2, d / 2);
        b.Add(-w / 2, w / 2, -th, 0.09f, d / 2 - 0.02f, d / 2);
        var xs = w > 1.4f ? new[] { -w / 2 + 0.10f, 0f, w / 2 - 0.10f } : new[] { -w / 2 + 0.10f, w / 2 - 0.10f };
        foreach (var x in xs)
        {
            b.Add(x - 0.015f, x + 0.015f, -th - 0.03f, -th, -d / 2 + 0.03f, d / 2 - 0.02f);
            b.Add(x - 0.015f, x + 0.015f, -th - 0.14f, -th - 0.03f, d / 2 - 0.05f, d / 2 - 0.02f);
        }

        return Shelves[key] = Boxes.Combine("drakes_wall_shelf", b);
    }

    /// <summary>Thickness of the cap on top of a bookcase.</summary>
    internal const float CapTh = 0.03f;

    /// <summary>Distance between the standing surfaces of two rows, for a bookcase <paramref name="height"/> tall with <paramref name="rows"/> rows.</summary>
    internal static float Pitch(int rows, float height) => (height - CapTh - CasePlinth) / Mathf.Max(1, rows);

    /// <summary>
    /// Plinth, open shelves, sides, cap and back panel: exactly <paramref name="w"/> wide, <paramref name="height"/> tall and
    /// <see cref="CaseD"/> deep, so bookcases line up with floors and walls and with each other. Origin at the floor, centred; wall at +Z.
    /// </summary>
    internal static Mesh Bookcase(float w, int rows, float height)
    {
        var key = "case" + w.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + "x" + rows + "x"
                  + height.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
        if (Shelves.TryGetValue(key, out var cached) && cached != null)
            return cached;
        float d = CaseD, th = CaseTh, plinth = CasePlinth, pitch = Pitch(rows, height);
        var b = new Boxes { TileUv = true };
        b.Add(-w / 2 + th, w / 2 - th, plinth - th, plinth, -d / 2, d / 2);
        for (var r = 1; r < rows; r++)
        {
            var y = plinth + pitch * r;
            b.Add(-w / 2 + th, w / 2 - th, y - th, y, -d / 2, d / 2);
        }

        b.Add(-w / 2, w / 2, height - CapTh, height, -d / 2, d / 2);
        b.Add(-w / 2, -w / 2 + th, 0, height - CapTh, -d / 2, d / 2);
        b.Add(w / 2 - th, w / 2, 0, height - CapTh, -d / 2, d / 2);
        b.Add(-w / 2 + th, w / 2 - th, 0, height - CapTh, d / 2 - 0.015f, d / 2);
        b.Add(-w / 2 + th, w / 2 - th, 0, plinth - th, -d / 2, -d / 2 + 0.02f); // kick board under the bottom row
        return Shelves[key] = Boxes.Combine("drakes_bookcase", b);
    }

    /// <summary>Axis-aligned boxes, 24 verts each, UVs 0..1 per face (same as the Blender cubes).</summary>
    sealed class Boxes
    {
        internal readonly List<Vector3> V = new();
        internal readonly List<Vector2> Uv = new();
        internal readonly List<int> Tri = new();
        /// <summary>Shelves: UVs run in metres so the wood grain repeats at its natural size instead of stretching over each board.</summary>
        internal bool TileUv;

        internal void Add(float x0, float x1, float y0, float y1, float z0, float z1)
        {
            var p = new[]
            {
                new Vector3(x0, y0, z0), new Vector3(x1, y0, z0), new Vector3(x1, y1, z0), new Vector3(x0, y1, z0),
                new Vector3(x0, y0, z1), new Vector3(x1, y0, z1), new Vector3(x1, y1, z1), new Vector3(x0, y1, z1),
            };
            // Corner indices per face, wound so the normal points out (Unity is clockwise-front).
            Face(p, 0, 3, 2, 1);   // -Z
            Face(p, 5, 6, 7, 4);   // +Z
            Face(p, 4, 7, 3, 0);   // -X
            Face(p, 1, 2, 6, 5);   // +X
            Face(p, 3, 7, 6, 2);   // +Y
            Face(p, 4, 0, 1, 5);   // -Y
        }

        void Face(Vector3[] p, int a, int b, int c, int d)
        {
            var i = V.Count;
            V.Add(p[a]); V.Add(p[b]); V.Add(p[c]); V.Add(p[d]);
            // Corner order a,b,c,d: a-b runs along V, b-c along U.
            var u = TileUv ? Vector3.Distance(p[c], p[b]) : 1f;
            var v = TileUv ? Vector3.Distance(p[b], p[a]) : 1f;
            Uv.Add(new Vector2(0, 0)); Uv.Add(new Vector2(0, v)); Uv.Add(new Vector2(u, v)); Uv.Add(new Vector2(u, 0));
            Tri.Add(i); Tri.Add(i + 1); Tri.Add(i + 2);
            Tri.Add(i); Tri.Add(i + 2); Tri.Add(i + 3);
        }

        /// <summary>One submesh per box set, in order.</summary>
        internal static Mesh Combine(string name, params Boxes[] sets)
        {
            var mesh = new Mesh { name = name, subMeshCount = sets.Length };
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            foreach (var s in sets)
            {
                verts.AddRange(s.V);
                uvs.AddRange(s.Uv);
            }

            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            var offset = 0;
            for (var i = 0; i < sets.Length; i++)
            {
                var tri = new List<int>(sets[i].Tri.Count);
                foreach (var t in sets[i].Tri)
                    tri.Add(t + offset);
                mesh.SetTriangles(tri, i);
                offset += sets[i].V.Count;
            }

            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
