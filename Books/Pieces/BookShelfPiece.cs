using System;
using System.Collections.Generic;
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

    /// <summary>
    /// Every shelf size. Inventory rows run 8 wide (the container window's width); a shelf row takes <c>InvPerRow</c> inventory rows,
    /// so a long shelf holds 16 books a row. Meshes and icons share the ids (see Forge/Books/mesh_mockup/make_book_meshes.py).
    /// </summary>
    internal sealed class Variant
    {
        public string Prefab = "";
        public string Token = "";
        public string English = "";
        public string Spanish = "";
        public string Description = "";
        public bool Wall;
        public float Width;
        /// <summary>Bookcases: outer height in metres (1 or 2, like a half or full wall).</summary>
        public float Height = 2f;
        public int ShelfRows = 1;
        public int InvPerRow = 1;
        public string Cost = "";
        public Func<Sprite?> Icon = () => null;
    }

    /// <summary>
    /// Sized on the build grid so they sit flush with floors and walls and with each other: wall shelves 1 m and 2 m long, bookcases
    /// 1 x 1 m (small), 1 x 2 m (tall, the standard) and 2 x 1 m (wide), all 0.4 m deep. 8 books per metre of row.
    /// </summary>
    internal static readonly Variant[] Variants =
    {
        new() { Prefab = WallName, Token = WallToken, English = "Wall Shelf", Spanish = "Estante de pared", Wall = true, Width = 1.00f, Cost = "FineWood:4",
                Description = "A 1 m shelf for books on a wall. Use a spine to read it in place.", Icon = () => BookAssets.WallShelfIcon },
        new() { Prefab = "Drakes_WallShelfLong", Token = "$piece_drakes_wall_shelf_long", English = "Long Wall Shelf", Spanish = "Estante de pared largo", Wall = true, Width = 2.00f,
                InvPerRow = 2, Cost = "FineWood:8", Description = "A 2 m wall shelf that holds twice as many books.", Icon = () => BookAssets.PieceIcon("wall_shelf_long") },
        new() { Prefab = "Drakes_BookcaseSmall", Token = "$piece_drakes_bookcase_small", English = "Small Bookcase", Spanish = "Librería pequeña", Width = 1.00f, Height = 1.00f,
                ShelfRows = 2, Cost = "FineWood:8", Description = "A 1 x 1 m bookcase with two rows, as tall as a half wall.", Icon = () => BookAssets.PieceIcon("bookcase_small") },
        new() { Prefab = CaseName, Token = CaseToken, English = "Tall Bookcase", Spanish = "Librería alta", Width = 1.00f, Height = 2.00f, ShelfRows = 4, Cost = "FineWood:14",
                Description = "A 1 x 2 m bookcase with four rows, as tall as a wall. Use a spine to read it in place.", Icon = () => BookAssets.BookcaseIcon },
        new() { Prefab = "Drakes_BookcaseWide", Token = "$piece_drakes_bookcase_wide", English = "Wide Bookcase", Spanish = "Librería ancha", Width = 2.00f, Height = 1.00f,
                ShelfRows = 2, InvPerRow = 2, Cost = "FineWood:14", Description = "A 2 x 1 m bookcase with two long rows, under a window or along a wall.",
                Icon = () => BookAssets.PieceIcon("bookcase_wide") },
    };

    static ManualLogSource? _log;

    internal static void Register(ManualLogSource log)
    {
        _log = log;
        // Shift+Use on a shelf is the shelf's own settings window: RenameIt's piece workshop must not take it over.
        DrakeModsLibs.API.PieceInteractionClaims.Register("DrakesRenameIt.shelves", piece => piece != null && piece.GetComponent<BookShelfBehaviour>() != null);
        // The layout shows as a Shelf tab in LockSmith's Shift+E menu (Libs keeps the list; nothing happens without LockSmith).
        ShelfLayout.RegisterTab();
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
            foreach (var v in Variants)
            {
                var key = v.Token.TrimStart('$');
                loc.AddTranslation("English", key, v.English);
                loc.AddTranslation("English", key + "_desc", v.Description);
                loc.AddTranslation("Spanish", key, v.Spanish);
                loc.AddTranslation("Spanish", key + "_desc", v.Description);
            }

            var chest = PrefabManager.Instance.GetPrefab("piece_chest_wood");
            var chestContainer = chest != null ? chest.GetComponent<Container>() : null;
            var wood = WoodMaterial(chest);
            if (chestContainer == null)
            {
                _log?.LogWarning("[Books] Shelves skipped: piece_chest_wood has no Container to copy from.");
                return;
            }

            foreach (var v in Variants)
                One(v, chestContainer, wood);
            _log?.LogInfo($"[Books] Registered {Variants.Length} shelf pieces (wall shelves and bookcases).");
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
        string from = "";
        var seen = new System.Collections.Generic.List<string>();

        Material? Find(GameObject? go, string label)
        {
            if (go == null)
                return null;
            foreach (var r in go.GetComponentsInChildren<MeshRenderer>(true))
            {
                var m = r.sharedMaterial;
                if (m == null)
                    continue;
                seen.Add($"{label}/{r.name}:{m.name}:{(m.shader != null ? m.shader.name : "?")}");
                var n = m.name.ToLowerInvariant();
                if (m.shader != null && m.shader.name == "Custom/Piece" && n.Contains("wood") && !n.Contains("ghost") && !n.Contains("glow") && !n.Contains("highlight"))
                    return m;
            }

            return null;
        }

        // The wooden wall pieces carry the plank look the shelves should match; the chest's own wood is the fallback.
        foreach (var name in new[] { "wood_wall_half", "wood_wall_log", "wood_floor", "wood_wall_roof" })
        {
            pick = Find(PrefabManager.Instance.GetPrefab(name), name);
            if (pick != null)
            {
                from = name;
                break;
            }
        }

        if (pick == null)
        {
            pick = Find(chest, "piece_chest_wood");
            from = "piece_chest_wood";
        }

        _log?.LogInfo($"[Books] Wood candidates: {string.Join(", ", seen)}; wood = {(pick != null ? pick.name + " from " + from : "plain brown")}.");
        if (pick != null)
            return pick;
        var brown = new Material(Shader.Find("Standard")) { name = "drakes_shelf_wood", color = new Color(0.36f, 0.22f, 0.11f) };
        brown.SetFloat("_Glossiness", 0.1f);
        return brown;
    }

    static void One(Variant v, Container chest, Material? wood)
    {
        var prefab = v.Prefab;
        var clone = v.Wall ? "sign" : "piece_chest_wood";
        var token = v.Token;
        var wall = v.Wall;
        var cost = v.Cost;
        var cols = 8;
        var rows = v.ShelfRows * v.InvPerRow;
        var mesh = wall ? BookMesh.WallShelf(v.Width) : BookMesh.Bookcase(v.Width, v.ShelfRows, v.Height);
        var pieceIcon = v.Icon();
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
        if (pieceIcon != null)
            config.Icon = pieceIcon;

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

        Colliders(frame.gameObject, v);
        SnapPoints(go, v);
        var p = go.GetComponent<Piece>();
        if (p != null)
        {
            p.m_name = token;
            p.m_category = Piece.PieceCategory.Furniture;
            p.m_craftingStation = null;
        }

        var behaviour = go.AddComponent<BookShelfBehaviour>();
        behaviour.IsWall = wall;
        behaviour.Width = v.Width;
        behaviour.Height = v.Height;
        behaviour.ShelfRows = v.ShelfRows;
        behaviour.InvPerRow = v.InvPerRow;
    }

    /// <summary>Wood parts only: the middle stays open so the crosshair can reach the spines.</summary>
    static void Colliders(GameObject frame, Variant v)
    {
        void Box(float cx, float cy, float cz, float sx, float sy, float sz)
        {
            var box = frame.AddComponent<BoxCollider>();
            box.center = new Vector3(cx, cy, cz);
            box.size = new Vector3(sx, sy, sz);
        }

        var w = v.Width;
        if (v.Wall)
        {
            var d = BookMesh.ShelfD;
            Box(0f, -BookMesh.ShelfTh / 2f, 0f, w, BookMesh.ShelfTh, d);
            Box(0f, 0.045f - BookMesh.ShelfTh / 2f, d / 2f - 0.01f, w, 0.12f, 0.02f);
            return;
        }

        var cd = BookMesh.CaseD;
        var th = BookMesh.CaseTh;
        var height = v.Height;
        var pitch = BookMesh.Pitch(v.ShelfRows, height);
        Box(-w / 2f + th / 2f, height / 2f, 0f, th, height, cd);
        Box(w / 2f - th / 2f, height / 2f, 0f, th, height, cd);
        Box(0f, height / 2f, cd / 2f - 0.0075f, w, height, 0.015f);
        Box(0f, height - BookMesh.CapTh / 2f, 0f, w, BookMesh.CapTh, cd);
        for (var r = 0; r < v.ShelfRows; r++)
            Box(0f, BookMesh.CasePlinth + pitch * r - th / 2f, 0f, w, th, cd);
    }

    /// <summary>
    /// Snap points on the half-metre grid, the same way the shop's crates tile: along every footprint edge every 0.5 m (so a 1 m case
    /// lines up with half a floor, a 2 m one with a whole floor, and cases sit side by side or back to back), repeated every 0.5 m up
    /// the height (stacking a small case on another, lining up with a wall). Wall shelves: along the back edge, top and underside.
    /// </summary>
    static void SnapPoints(GameObject root, Variant v)
    {
        var points = new System.Collections.Generic.List<Vector3>();
        var hw = v.Width / 2f;
        IEnumerable<float> Steps(float from, float to)
        {
            for (var x = from; x <= to + 0.001f; x += 0.5f)
                yield return x;
        }

        // The mesh is turned half way round in its frame, so the back (wall) side is -Z here.
        if (v.Wall)
        {
            var back = -BookMesh.ShelfD / 2f;
            foreach (var x in Steps(-hw, hw))
            {
                points.Add(new Vector3(x, 0f, back));
                points.Add(new Vector3(x, -BookMesh.ShelfTh, back));
            }
        }
        else
        {
            var hd = BookMesh.CaseD / 2f;
            foreach (var y in Steps(0f, v.Height))
            {
                foreach (var x in Steps(-hw, hw))
                {
                    points.Add(new Vector3(x, y, -hd));
                    points.Add(new Vector3(x, y, hd));
                }

                foreach (var z in Steps(-hd, hd))
                {
                    points.Add(new Vector3(-hw, y, z));
                    points.Add(new Vector3(hw, y, z));
                }
            }
        }

        // Libs makes them like the game's own (tagged, inactive until the build ghost wants them) and drops duplicates' clutter.
        var unique = new System.Collections.Generic.List<Vector3>();
        foreach (var p in points)
            if (!unique.Exists(u => (u - p).sqrMagnitude < 0.0001f))
                unique.Add(p);
        DrakeModsLibs.Forge.ForgeLook.SnapPoints(root, true, unique.ToArray());
    }
}
