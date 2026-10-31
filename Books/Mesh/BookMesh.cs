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

    // Bookcase: width, depth, board thickness, plinth, row pitch, rows. Pitch clears the tallest (large) book.
    internal const float CaseW = 1.10f, CaseD = 0.36f, CaseTh = 0.03f, CasePlinth = 0.08f, CasePitch = 0.50f;
    internal const int CaseRows = 3;
    internal const float ShelfW = 0.90f, ShelfD = 0.26f, ShelfTh = 0.03f;

    static readonly Dictionary<BookSize, Mesh> Books = new();
    static Mesh? _wallShelf, _bookcase;

    internal static Vector3 Dimensions(BookSize size) => Sizes[(int)size];

    /// <summary>Height of row <paramref name="row"/>'s standing surface above the bookcase origin.</summary>
    internal static float RowY(int row) => CasePlinth + CasePitch * row;

    /// <summary>Two submeshes: 0 = cover (tinted leather), 1 = page block (paper texture). Origin at the foot of the spine's centre.</summary>
    internal static Mesh Book(BookSize size)
    {
        if (Books.TryGetValue(size, out var cached) && cached != null)
            return cached;
        var s = Sizes[(int)size];
        float d = s.x, h = s.y, t = s.z;
        var cover = new Boxes();
        cover.Add(-t / 2, -t / 2 + Board, 0, h, -d / 2, d / 2);
        cover.Add(t / 2 - Board, t / 2, 0, h, -d / 2, d / 2);
        cover.Add(-t / 2, t / 2, 0, h, -d / 2, -d / 2 + Board);   // spine faces -Z
        var pages = new Boxes();
        pages.Add(-t / 2 + Board, t / 2 - Board, PageInset, h - PageInset, -d / 2 + Board, d / 2 - PageInset);
        var mesh = Boxes.Combine("drakes_book_" + size, cover, pages);
        Books[size] = mesh;
        return mesh;
    }

    /// <summary>One board on two brackets with a back rail. Origin at the top of the board, centred; wall at +Z.</summary>
    internal static Mesh WallShelf()
    {
        if (_wallShelf != null)
            return _wallShelf;
        float w = ShelfW, d = ShelfD, th = ShelfTh;
        var b = new Boxes();
        b.Add(-w / 2, w / 2, -th, 0, -d / 2, d / 2);
        b.Add(-w / 2, w / 2, -th, 0.09f, d / 2 - 0.02f, d / 2);
        foreach (var x in new[] { -w / 2 + 0.10f, w / 2 - 0.10f })
        {
            b.Add(x - 0.015f, x + 0.015f, -th - 0.03f, -th, -d / 2 + 0.03f, d / 2 - 0.02f);
            b.Add(x - 0.015f, x + 0.015f, -th - 0.14f, -th - 0.03f, d / 2 - 0.05f, d / 2 - 0.02f);
        }

        return _wallShelf = Boxes.Combine("drakes_wall_shelf", b);
    }

    /// <summary>Plinth, three shelves, sides, overhanging cap and back panel. Origin at the floor, centred; wall at +Z.</summary>
    internal static Mesh Bookcase()
    {
        if (_bookcase != null)
            return _bookcase;
        float w = CaseW, d = CaseD, th = CaseTh, plinth = CasePlinth, pitch = CasePitch;
        float height = plinth + pitch * CaseRows + th;
        var b = new Boxes();
        b.Add(-w / 2, w / 2, plinth - th, plinth, -d / 2, d / 2);
        for (var r = 1; r <= CaseRows; r++)
        {
            var y = plinth + pitch * r;
            b.Add(-w / 2, w / 2, y - th, y, -d / 2, d / 2);
        }

        b.Add(-w / 2 - 0.02f, w / 2 + 0.02f, height, height + 0.03f, -d / 2 - 0.02f, d / 2);
        b.Add(-w / 2, -w / 2 + th, 0, height, -d / 2, d / 2);
        b.Add(w / 2 - th, w / 2, 0, height, -d / 2, d / 2);
        b.Add(-w / 2, w / 2, 0, height, d / 2 - 0.015f, d / 2);
        return _bookcase = Boxes.Combine("drakes_bookcase", b);
    }

    /// <summary>Axis-aligned boxes, 24 verts each, UVs 0..1 per face (same as the Blender cubes).</summary>
    sealed class Boxes
    {
        internal readonly List<Vector3> V = new();
        internal readonly List<Vector2> Uv = new();
        internal readonly List<int> Tri = new();

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
            Uv.Add(new Vector2(0, 0)); Uv.Add(new Vector2(0, 1)); Uv.Add(new Vector2(1, 1)); Uv.Add(new Vector2(1, 0));
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
