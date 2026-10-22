using System;
using System.Collections.Generic;
using DrakeModsLibs.Forge;
using UnityEngine;

namespace DrakeRenameit.Paper.Items;

/// <summary>
/// Low-poly 3D paper sheet (Valheim look), built in code so no asset bundle is needed. Mirrors
/// <c>Forge/Paper/mesh_mockup/make_paper_mesh.py</c>: an 8×10 grid with a slight edge lift and ripple,
/// wobbly (deckled) edges, a thin rim, ~390 tris. Textures are 128×166, point filtered.
/// Placement matches the old sprites: flat = face up, centred; wall = standing, facing +Z, centred.
/// </summary>
internal static class PaperMesh
{
    // US Letter at scale 1 (same as the sprite recipes).
    const float W = 0.2159f, H = 0.2794f;
    const float Thick = 0.0007f;
    const int Nx = 8, Ny = 10;
    const float FaceLift = 0.002f;          // flat sheet's top face height, same as the sprite

    static readonly Dictionary<float, UnityEngine.Mesh> Meshes = new();
    static readonly Material?[] Materials = new Material?[2];

    /// <summary>Icons rendered from this mesh by the Blender script (top-down, transparent).</summary>
    internal static Sprite? BlankIcon => ForgeTextures.Sprite(typeof(PaperMesh).Assembly, "textures/paper_mesh_icon.png");
    internal static Sprite? WrittenIcon => ForgeTextures.Sprite(typeof(PaperMesh).Assembly, "textures/paper_mesh_written_icon.png");

    /// <summary>Adds the sheet under <paramref name="parent"/>.</summary>
    internal static GameObject? Build(Transform parent, float scale, bool wall, bool written)
    {
        var material = SheetMaterial(written);
        if (material == null)
            return null;
        var go = new GameObject("drakes_paper_mesh");
        go.transform.SetParent(parent, false);
        if (wall)
        {
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
        }
        else
        {
            go.transform.localPosition = new Vector3(0f, FaceLift * scale, 0f);
            go.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);   // sheet +Z (face) → +Y (up)
        }

        go.AddComponent<MeshFilter>().sharedMesh = SheetMesh(scale);
        var renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        return go;
    }

    /// <summary>Shared blank/written sheet material: Standard, albedo + normal map, Point filtering. Built once each.</summary>
    internal static Material? SheetMaterial(bool written)
    {
        var slot = written ? 1 : 0;
        if (Materials[slot] != null)
            return Materials[slot];
        var owner = typeof(PaperMesh).Assembly;
        var albedo = ForgeTextures.Load(owner, written ? "textures/paper_mesh_sheet_written.png" : "textures/paper_mesh_sheet.png");
        if (albedo == null)
            return null;
        albedo.filterMode = FilterMode.Point;
        // ForgeSprites gives a vanilla Standard material whose shader variant is surely in the build; copy it.
        var material = new Material(ForgeSprites.Material(albedo)) { name = "drakes_paper_mesh" + (written ? "_written" : "") };
        var normal = ForgeTextures.Load(owner, "textures/paper_mesh_normal.png", normalMap: true);
        if (normal != null && material.HasProperty("_BumpMap"))
        {
            normal.filterMode = FilterMode.Point;
            material.SetTexture("_BumpMap", normal);
            material.SetFloat("_BumpScale", 0.5f);
            material.EnableKeyword("_NORMALMAP");
        }

        Materials[slot] = material;
        return material;
    }

    /// <summary>The sheet in its own space: width on X, length on Y, front face toward +Z, centred.</summary>
    static UnityEngine.Mesh SheetMesh(float scale)
    {
        if (Meshes.TryGetValue(scale, out var cached) && cached != null)
            return cached;

        var rng = new System.Random(7);
        var grid = new Vector3[Nx + 1, Ny + 1];
        for (var j = 0; j <= Ny; j++)
        for (var i = 0; i <= Nx; i++)
        {
            var x = ((float)i / Nx - 0.5f) * W;
            var y = ((float)j / Ny - 0.5f) * H;
            // Deckled edge: nudge border points in-plane.
            var n = (float)(rng.NextDouble() * 2 - 1) * 0.0015f;
            if (i == 0) x -= n;
            if (i == Nx) x += n;
            if (j == 0) y -= n;
            if (j == Ny) y += n;
            // Ends lift a touch and a faint ripple runs across, so it never reads as a flat card.
            // Kept small: page text clears the mesh bounds, so a big bow would float the ink.
            var ends = 2f * y / H;
            var z = 0.0012f * ends * ends + 0.0004f * Mathf.Sin(x * 22f + y * 9f);
            grid[i, j] = new Vector3(x, y, z) * scale;
        }

        var verts = new List<Vector3>();
        var uvs = new List<Vector2>();
        var tris = new List<int>();
        var back = new Vector3(0f, 0f, -Thick * scale);

        // UVs as the sprite quad: u runs toward -X so the page reads correctly from the front; back is mirrored.
        Vector2 Uv(int i, int j, bool mirror) => new(mirror ? (float)i / Nx : 1f - (float)i / Nx, (float)j / Ny);

        int AddVert(Vector3 p, Vector2 uv)
        {
            verts.Add(p);
            uvs.Add(uv);
            return verts.Count - 1;
        }

        // Unity's front face: Cross(b - a, c - a) points toward the viewer. Flip to face `outward`.
        void AddTri(int a, int b, int c, Vector3 outward)
        {
            if (Vector3.Dot(Vector3.Cross(verts[b] - verts[a], verts[c] - verts[a]), outward) < 0f)
                (b, c) = (c, b);
            tris.Add(a);
            tris.Add(b);
            tris.Add(c);
        }

        void AddFace(Vector3 offset, bool mirror, Vector3 outward)
        {
            var idx = new int[Nx + 1, Ny + 1];
            for (var j = 0; j <= Ny; j++)
            for (var i = 0; i <= Nx; i++)
                idx[i, j] = AddVert(grid[i, j] + offset, Uv(i, j, mirror));
            for (var j = 0; j < Ny; j++)
            for (var i = 0; i < Nx; i++)
            {
                AddTri(idx[i, j], idx[i, j + 1], idx[i + 1, j + 1], outward);
                AddTri(idx[i, j], idx[i + 1, j + 1], idx[i + 1, j], outward);
            }
        }

        AddFace(Vector3.zero, mirror: false, Vector3.forward);
        AddFace(back, mirror: true, Vector3.back);

        // Rim: one quad per border segment, its own verts so normals stay crisp.
        var border = new List<(int i, int j)>();
        for (var i = 0; i < Nx; i++) border.Add((i, 0));
        for (var j = 0; j < Ny; j++) border.Add((Nx, j));
        for (var i = Nx; i > 0; i--) border.Add((i, Ny));
        for (var j = Ny; j > 0; j--) border.Add((0, j));
        for (var k = 0; k < border.Count; k++)
        {
            var (i0, j0) = border[k];
            var (i1, j1) = border[(k + 1) % border.Count];
            var p0 = grid[i0, j0];
            var p1 = grid[i1, j1];
            var mid = (p0 + p1) * 0.5f;
            var outward = new Vector3(mid.x, mid.y, 0f).normalized;
            var a = AddVert(p0, Uv(i0, j0, false));
            var b = AddVert(p1, Uv(i1, j1, false));
            var c = AddVert(p1 + back, Uv(i1, j1, false));
            var d = AddVert(p0 + back, Uv(i0, j0, false));
            AddTri(a, b, c, outward);
            AddTri(a, c, d, outward);
        }

        var mesh = new UnityEngine.Mesh { name = "drakes_paper_mesh" };
        mesh.SetVertices(verts);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        mesh.RecalculateTangents();
        Meshes[scale] = mesh;
        return mesh;
    }
}
