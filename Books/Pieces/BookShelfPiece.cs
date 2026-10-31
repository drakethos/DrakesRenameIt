using System;
using BepInEx.Logging;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DrakeRenameit.Books;

/// <summary>
/// The wall shelf (one row, hangs on a wall) and the bookcase (three rows, stands on the floor). Both are real containers, so
/// the contents save and sync like a chest; <see cref="BookShelfBehaviour"/> draws them as spines. Hammer, Furniture.
/// </summary>
internal static class BookShelfPiece
{
    internal const string WallName = "Drakes_WallShelf";
    internal const string CaseName = "Drakes_Bookcase";
    internal const string WallToken = "$piece_drakes_wall_shelf";
    internal const string CaseToken = "$piece_drakes_bookcase";

    /// <summary>Vanilla pieces face +Z and the meshes are modelled facing -Z, so the mesh sits in a frame turned half way round.</summary>
    internal const string FrameName = "drakes_frame";

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
                return;

            var loc = LocalizationManager.Instance.GetLocalization();
            loc.AddTranslation("English", "piece_drakes_wall_shelf", "Wall Shelf");
            loc.AddTranslation("English", "piece_drakes_wall_shelf_desc", "A small shelf for books on a wall. Put books in it and they stand on the shelf; use one to read it in place.");
            loc.AddTranslation("English", "piece_drakes_bookcase", "Bookcase");
            loc.AddTranslation("English", "piece_drakes_bookcase_desc", "A tall bookcase with three rows. Books stand on the shelves; use a spine to read it in place.");
            loc.AddTranslation("Spanish", "piece_drakes_wall_shelf", "Estante de pared");
            loc.AddTranslation("Spanish", "piece_drakes_wall_shelf_desc", "Un estante pequeño para libros en la pared.");
            loc.AddTranslation("Spanish", "piece_drakes_bookcase", "Librería");
            loc.AddTranslation("Spanish", "piece_drakes_bookcase_desc", "Una librería alta con tres filas.");

            var chest = PrefabManager.Instance.GetPrefab("piece_chest_wood");
            var chestContainer = chest != null ? chest.GetComponent<Container>() : null;
            var wood = WoodMaterial(chest);
            if (chestContainer == null)
            {
                _log?.LogWarning("[Books] Shelves skipped: piece_chest_wood has no Container to copy from.");
                return;
            }

            One(WallName, "sign", WallToken, wall: true, "FineWood:4", chestContainer, wood, BookMesh.WallShelf(), cols: 8, rows: 1);
            One(CaseName, "piece_chest_wood", CaseToken, wall: false, "FineWood:14", chestContainer, wood, BookMesh.Bookcase(), cols: 10, rows: 3);
            _log?.LogInfo("[Books] Registered the wall shelf and bookcase.");
        }
        catch (Exception ex)
        {
            _log?.LogError($"[Books] Failed to register shelves: {ex}");
        }
    }

    /// <summary>
    /// The chest's own wood material when one can be told apart by name (the first renderer can be a glow or ghost material, which
    /// drew the shelves as white slabs); otherwise a plain brown one. What was chosen is logged.
    /// </summary>
    static Material WoodMaterial(GameObject? chest)
    {
        Material? pick = null;
        var names = new System.Collections.Generic.List<string>();
        if (chest != null)
        {
            foreach (var r in chest.GetComponentsInChildren<MeshRenderer>(true))
            {
                var m = r.sharedMaterial;
                if (m == null)
                    continue;
                names.Add($"{r.name}:{m.name}:{(m.shader != null ? m.shader.name : "?")}");
                var n = m.name.ToLowerInvariant();
                if (pick == null && n.Contains("wood") && !n.Contains("ghost") && !n.Contains("glow") && !n.Contains("highlight"))
                    pick = m;
            }
        }

        _log?.LogInfo($"[Books] Chest renderers: {string.Join(", ", names)}; wood = {(pick != null ? pick.name : "plain brown")}.");
        if (pick != null)
            return pick;
        var brown = new Material(Shader.Find("Standard")) { name = "drakes_shelf_wood", color = new Color(0.36f, 0.22f, 0.11f) };
        brown.SetFloat("_Glossiness", 0.1f);
        return brown;
    }

    static void One(string prefab, string clone, string token, bool wall, string cost, Container chest, Material? wood, Mesh mesh, int cols, int rows)
    {
        var config = new PieceConfig
        {
            Name = token,
            Description = token + "_desc",
            Enabled = true,
            PieceTable = "Hammer",
            Category = "Furniture",
            CraftingStation = "",
        };
        foreach (var (item, amount) in Paper.Items.PaperItem.ParseCost(cost))
            config.AddRequirement(new RequirementConfig(item, amount, 0, true));
        if (BookAssets.Icon is { } icon)
            config.Icon = icon;

        var piece = new CustomPiece(prefab, clone, config);
        PieceManager.Instance.AddPiece(piece);
        var go = piece.PiecePrefab;
        if (go == null)
            return;

        // Keep the donor's Piece, ZNetView and (chest) Container; drop everything it looks like and can break on.
        for (var i = go.transform.childCount - 1; i >= 0; i--)
            Object.DestroyImmediate(go.transform.GetChild(i).gameObject);
        foreach (var col in go.GetComponents<Collider>())
            Object.DestroyImmediate(col);
        foreach (var sign in go.GetComponents<Sign>())
            Object.DestroyImmediate(sign);
        foreach (var wear in go.GetComponents<WearNTear>())
            Object.DestroyImmediate(wear);

        var container = go.GetComponent<Container>();
        if (container == null)
        {
            container = go.AddComponent<Container>();
            container.m_destroyedLootPrefab = chest.m_destroyedLootPrefab;
            container.m_openEffects = chest.m_openEffects;
            container.m_closeEffects = chest.m_closeEffects;
            container.m_checkGuardStone = true;
        }

        container.m_name = token;
        container.m_width = cols;
        container.m_height = rows;

        var frame = new GameObject(FrameName).transform;
        frame.SetParent(go.transform, false);
        frame.localRotation = Quaternion.Euler(0f, 180f, 0f);
        var look = new GameObject("drakes_shelf_mesh");
        look.transform.SetParent(frame, false);
        look.AddComponent<MeshFilter>().sharedMesh = mesh;
        look.AddComponent<MeshRenderer>().sharedMaterial = wood;

        Colliders(frame.gameObject, wall);
        var p = go.GetComponent<Piece>();
        if (p != null)
        {
            p.m_name = token;
            p.m_category = Piece.PieceCategory.Furniture;
            p.m_craftingStation = null;
        }

        go.AddComponent<BookShelfBehaviour>().IsWall = wall;
    }

    /// <summary>Wood parts only: the middle stays open so the crosshair can reach the spines.</summary>
    static void Colliders(GameObject frame, bool wall)
    {
        void Box(float cx, float cy, float cz, float sx, float sy, float sz)
        {
            var box = frame.AddComponent<BoxCollider>();
            box.center = new Vector3(cx, cy, cz);
            box.size = new Vector3(sx, sy, sz);
        }

        if (wall)
        {
            var w = BookMesh.ShelfW;
            var d = BookMesh.ShelfD;
            Box(0f, -BookMesh.ShelfTh / 2f, 0f, w, BookMesh.ShelfTh, d);
            Box(0f, 0.045f - BookMesh.ShelfTh / 2f, d / 2f - 0.01f, w, 0.12f, 0.02f);
            return;
        }

        var cw = BookMesh.CaseW;
        var cd = BookMesh.CaseD;
        var th = BookMesh.CaseTh;
        var height = BookMesh.CasePlinth + BookMesh.CasePitch * BookMesh.CaseRows + th;
        Box(-cw / 2f + th / 2f, height / 2f, 0f, th, height, cd);
        Box(cw / 2f - th / 2f, height / 2f, 0f, th, height, cd);
        Box(0f, height / 2f, cd / 2f - 0.0075f, cw, height, 0.015f);
        Box(0f, height + 0.015f, 0f, cw + 0.04f, 0.03f, cd + 0.02f);
        for (var r = 0; r <= BookMesh.CaseRows; r++)
            Box(0f, BookMesh.CasePlinth + BookMesh.CasePitch * r - th / 2f, 0f, cw, th, cd);
    }
}
