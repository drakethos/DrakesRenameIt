using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using BepInEx.Logging;
using DrakeRenameit.Paper.Pieces;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using Jotunn.Utils;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DrakeRenameit.Paper.Items;

/// <summary>
/// Blank Piece of Paper (stackable) + Written Page (stack 1). Paper2 parchment visuals.
/// </summary>
internal static class PaperItem
{
    /// <summary>Blank paper ΓÇö legacy prefab name kept for existing worlds/recipes.</summary>
    internal const string PrefabName = "Drakes_PieceOfPaper";
    internal const string WrittenPrefabName = "Drakes_Paper_Written";
    /// <summary>Immutable stackable print (Manifesto / flyer copies).</summary>
    internal const string PrintedPrefabName = "Drakes_Paper_Print";
    /// <summary>Donor for ItemDrop / physics / networking only ΓÇö visuals are discarded.</summary>
    private const string CloneSource = "LeatherScraps";
    private const string TokenName = "$item_drakes_pieceofpaper";
    private const string TokenDesc = "$item_drakes_pieceofpaper_desc";
    private const string TokenWrittenName = "$item_drakes_paper_written";
    private const string TokenWrittenDesc = "$item_drakes_paper_written_desc";
    private const string TokenPrintedName = "$item_drakes_paper_print";
    private const string TokenPrintedDesc = "$item_drakes_paper_print_desc";

    /// <summary>US Letter in Valheim meters (1 unit = 1 m): 8.5" × 11", before <see cref="RenameitConfig.PaperScale"/>.</summary>
    private static readonly Vector2 BasePaperSize = new Vector2(8.5f * 0.0254f, 11f * 0.0254f);

    /// <summary>World size of the paper sheet (meters), X = width, Z = height when laid flat.</summary>
    internal static Vector2 PaperSize => BasePaperSize * RenameitConfig.PaperScale;

    /// <summary>Lift above terrain so grass / ground decals do not z-fight through the sheet.</summary>
    private const float GroundClearance = 0.045f;

    /// <summary>
    /// Flat sheets float this far above the placement hit so plank gaps / thick tabletops
    /// do not swallow the parchment (blank laying + written flat + stack).
    /// </summary>
    private const float FlatSurfaceLift = 0.04f;

    /// <summary>
    /// Vertical sheets sit this far off the aimed wall face (toward the player).
    /// Placement hits often land in the wall volume; without this the sheet sinks into the planks.
    /// </summary>
    internal const float WallFaceGap = 0.008f;

    private static readonly Color OffWhite = new Color(0.93f, 0.89f, 0.80f, 1f);

    private static ManualLogSource? _log;
    private static string _pluginDir = "";

    internal static void Register(ManualLogSource log, string pluginDirectory)
    {
        _log = log;
        _pluginDir = pluginDirectory ?? "";
        PaperAssets.Init(log, _pluginDir);
        PrefabManager.OnVanillaPrefabsAvailable -= AddPaperItems;
        PrefabManager.OnVanillaPrefabsAvailable += AddPaperItems;
    }

    private static void AddPaperItems()
    {
        PrefabManager.OnVanillaPrefabsAvailable -= AddPaperItems;

        try
        {
            AddLocalization();

            var parchment = LoadParchmentTexture();
            var meshTex = LoadMeshTexture() ?? (parchment != null ? CreateCutoutMeshTexture(parchment) : null);
            var writtenParchment = LoadWrittenParchmentTexture() ?? parchment;
            // Mesh must use blank silhouette as alpha mask so ink never expands the border.
            var writtenMeshTex = CreateWrittenMeshTexture(writtenParchment, parchment, meshTex);

            RegisterBlank(parchment, meshTex);
            RegisterWritten(writtenParchment, writtenMeshTex);
            RegisterPrinted(writtenParchment, writtenMeshTex);
        }
        catch (Exception ex)
        {
            _log?.LogError($"[Paper] Failed to register paper items: {ex}");
        }
    }

    private static void RegisterBlank(Texture2D? parchment, Texture2D? meshTex)
    {
        Sprite? icon = null;
        if (parchment != null)
        {
            icon = Sprite.Create(
                parchment,
                new Rect(0, 0, parchment.width, parchment.height),
                new Vector2(0.5f, 0.5f));
        }

        var itemConfig = new ItemConfig
        {
            Name = TokenName,
            Description = TokenDesc,
            Enabled = RenameitConfig.PaperEnabled,
            Amount = 1,
        };
        if (icon != null)
            itemConfig.Icon = icon;

        ApplyRecipeFromConfig(itemConfig);

        var paper = new CustomItem(PrefabName, CloneSource, itemConfig);
        SanitizeItemDrop(paper, TokenName, TokenDesc, maxStack: RenameitConfig.BlankPaperStackSize);
        // Attach mesh before AddItem — Jotunn/ObjectDB must register the dressed prefab.
        BuildPaper2Visual(paper.ItemPrefab, meshTex, written: false);
        ItemManager.Instance.AddItem(paper);

        if (paper.ItemDrop?.m_itemData != null)
            paper.ItemDrop.m_itemData.m_dropPrefab = paper.ItemPrefab;

        _log?.LogInfo(
            $"[Paper] Registered blank {PrefabName} (stack={RenameitConfig.BlankPaperStackSize}, " +
            $"enabled={RenameitConfig.PaperEnabled}, cost='{RenameitConfig.PaperCost}').");
    }

    private static void RegisterWritten(Texture2D? parchment, Texture2D? meshTex)
    {
        Sprite? icon = null;
        if (parchment != null)
        {
            icon = Sprite.Create(
                parchment,
                new Rect(0, 0, parchment.width, parchment.height),
                new Vector2(0.5f, 0.5f));
        }

        var itemConfig = new ItemConfig
        {
            Name = TokenWrittenName,
            Description = TokenWrittenDesc,
            Enabled = RenameitConfig.PaperEnabled,
            Amount = 1,
        };
        if (icon != null)
            itemConfig.Icon = icon;

        // Not craftable from stations ΓÇö created by peel-write only.
        // Jotunn still needs an item; leave requirements empty and Enabled only controls visibility in some UIs.

        var paper = new CustomItem(WrittenPrefabName, CloneSource, itemConfig);
        SanitizeItemDrop(paper, TokenWrittenName, TokenWrittenDesc, maxStack: 1);
        BuildPaper2Visual(paper.ItemPrefab, meshTex, written: true);
        ItemManager.Instance.AddItem(paper);

        if (paper.ItemDrop?.m_itemData != null)
            paper.ItemDrop.m_itemData.m_dropPrefab = paper.ItemPrefab;

        _log?.LogInfo($"[Paper] Registered written {WrittenPrefabName} (stack=1).");
    }

    private static void RegisterPrinted(Texture2D? parchment, Texture2D? meshTex)
    {
        Sprite? icon = null;
        if (parchment != null)
        {
            icon = Sprite.Create(
                parchment,
                new Rect(0, 0, parchment.width, parchment.height),
                new Vector2(0.5f, 0.5f));
        }

        var itemConfig = new ItemConfig
        {
            Name = TokenPrintedName,
            Description = TokenPrintedDesc,
            Enabled = RenameitConfig.PaperEnabled,
            Amount = 1,
        };
        if (icon != null)
            itemConfig.Icon = icon;

        var paper = new CustomItem(PrintedPrefabName, CloneSource, itemConfig);
        SanitizeItemDrop(paper, TokenPrintedName, TokenPrintedDesc, maxStack: RenameitConfig.PrintedPaperStackSize);
        BuildPaper2Visual(paper.ItemPrefab, meshTex, written: true);
        ItemManager.Instance.AddItem(paper);

        if (paper.ItemDrop?.m_itemData != null)
            paper.ItemDrop.m_itemData.m_dropPrefab = paper.ItemPrefab;

        _log?.LogInfo($"[Paper] Registered printed {PrintedPrefabName} (stack={RenameitConfig.PrintedPaperStackSize}).");
    }

    internal static void ApplyLocalizationFromConfig()
    {
        try
        {
            AddLocalization();
        }
        catch (Exception ex)
        {
            _log?.LogWarning($"[Paper] Localization refresh failed: {ex.Message}");
        }
    }

    private static void AddLocalization()
    {
        var name = string.IsNullOrWhiteSpace(RenameitConfig.PaperName)
            ? "Piece of Paper"
            : RenameitConfig.PaperName.Trim();
        var desc = string.IsNullOrWhiteSpace(RenameitConfig.PaperDescription)
            ? "A blank piece of paper you can write on. Renaming uses one sheet and creates a Written Page."
            : RenameitConfig.PaperDescription.Trim();
        var wName = string.IsNullOrWhiteSpace(RenameitConfig.WrittenPaperName)
            ? "Written Page"
            : RenameitConfig.WrittenPaperName.Trim();
        var wDesc = string.IsNullOrWhiteSpace(RenameitConfig.WrittenPaperDescription)
            ? "A page with writing on it."
            : RenameitConfig.WrittenPaperDescription.Trim();
        var pName = string.IsNullOrWhiteSpace(RenameitConfig.PrintedPaperName)
            ? "Printed Page"
            : RenameitConfig.PrintedPaperName.Trim();
        var pDesc = string.IsNullOrWhiteSpace(RenameitConfig.PrintedPaperDescription)
            ? "A printed copy of a written page. Stackable; text is locked unless you have override."
            : RenameitConfig.PrintedPaperDescription.Trim();

        var loc = LocalizationManager.Instance.GetLocalization();
        loc.AddTranslation("English", "item_drakes_pieceofpaper", name);
        loc.AddTranslation("English", "item_drakes_pieceofpaper_desc", desc);
        loc.AddTranslation("Spanish", "item_drakes_pieceofpaper", name);
        loc.AddTranslation("Spanish", "item_drakes_pieceofpaper_desc", desc);
        loc.AddTranslation("English", "item_drakes_paper_written", wName);
        loc.AddTranslation("English", "item_drakes_paper_written_desc", wDesc);
        loc.AddTranslation("Spanish", "item_drakes_paper_written", wName);
        loc.AddTranslation("Spanish", "item_drakes_paper_written_desc", wDesc);
        loc.AddTranslation("English", "item_drakes_paper_print", pName);
        loc.AddTranslation("English", "item_drakes_paper_print_desc", pDesc);
        loc.AddTranslation("Spanish", "item_drakes_paper_print", pName);
        loc.AddTranslation("Spanish", "item_drakes_paper_print_desc", pDesc);
    }

    private static Texture2D? LoadParchmentTexture() => LoadTextureFile("paper.png", "icon");

    private static Texture2D? LoadWrittenParchmentTexture() => LoadTextureFile("paper_written.png", "written");

    private static Texture2D? LoadMeshTexture() => LoadTextureFile("paper_mesh.png", "mesh");


    private static Texture2D? LoadTextureFile(string fileName, string label)
    {
        var path = Path.IsPathRooted(fileName)
            ? fileName
            : Path.Combine(_pluginDir, "Assets", fileName);
        // Back-compat: Icons\<file> when caller passed a bare filename.
        if (!File.Exists(path) && fileName.IndexOf(Path.DirectorySeparatorChar) < 0 &&
            fileName.IndexOf(Path.AltDirectorySeparatorChar) < 0)
            path = Path.Combine(_pluginDir, "Assets", "Icons", fileName);

        var leaf = Path.GetFileName(fileName);
        // Gale flattens zip folders. Never treat Thunderstore's package icon.png as item art.
        if (!File.Exists(path) &&
            !leaf.Equals("icon.png", StringComparison.OrdinalIgnoreCase))
            path = Path.Combine(_pluginDir, leaf);

        if (!File.Exists(path))
        {
            _log?.LogWarning($"[Paper] {label} texture not found at {path}.");
            return null;
        }

        try
        {
            return AssetUtils.LoadTexture(path, relativePath: false);
        }
        catch (Exception ex)
        {
            _log?.LogWarning($"[Paper] Failed to load {label} texture: {ex.Message}");
            return null;
        }
    }

    private static void ApplyRecipeFromConfig(ItemConfig itemConfig)
    {
        var station = RenameitConfig.PaperCraftingStation?.Trim() ?? "";
        itemConfig.CraftingStation = string.IsNullOrEmpty(station) ? null : station;

        foreach (var (prefab, amount) in ParseCost(RenameitConfig.PaperCost))
            itemConfig.AddRequirement(prefab, amount);
    }

    internal static List<(string PrefabName, int Amount)> ParseCost(string? raw)
    {
        var list = new List<(string PrefabName, int Amount)>();
        if (string.IsNullOrWhiteSpace(raw))
            return list;

        var costRaw = raw!;
        var segments = costRaw.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var seg in segments)
        {
            var s = seg.Trim();
            if (string.IsNullOrEmpty(s))
                continue;
            var idx = s.LastIndexOf(':');
            if (idx <= 0 || idx >= s.Length - 1)
            {
                RenameitConfig.VerboseWarning($"[Paper] Ignoring invalid cost segment: \"{s}\"");
                continue;
            }

            var namePart = s.Substring(0, idx).Trim();
            var amtPart = s.Substring(idx + 1).Trim();
            if (!int.TryParse(amtPart, NumberStyles.Integer, CultureInfo.InvariantCulture, out var amount) ||
                amount <= 0)
            {
                RenameitConfig.VerboseWarning($"[Paper] Ignoring invalid amount in: \"{s}\"");
                continue;
            }

            list.Add((namePart, amount));
        }

        if (list.Count == 0)
        {
            list.Add(("Wood", 1));
            list.Add(("Coal", 1));
            _log?.LogWarning("[Paper] PaperCost had no valid entries; using Wood:1,Coal:1.");
        }

        return list;
    }

    private static void SanitizeItemDrop(CustomItem paper, string tokenName, string tokenDesc, int maxStack)
    {
        var drop = paper.ItemDrop;
        if (drop?.m_itemData?.m_shared == null)
            return;

        var shared = drop.m_itemData.m_shared;
        shared.m_name = tokenName;
        shared.m_description = tokenDesc;
        shared.m_maxStackSize = Math.Max(1, maxStack);
        shared.m_itemType = ResolveItemType();
        shared.m_weight = 0.1f;
        shared.m_value = 0;
        shared.m_teleportable = true;
        shared.m_questItem = false;
        shared.m_maxQuality = 1;
        shared.m_useDurability = false;
        shared.m_food = 0f;
        shared.m_foodStamina = 0f;
        shared.m_foodEitr = 0f;
    }

    private static ItemDrop.ItemData.ItemType ResolveItemType()
    {
        var raw = RenameitConfig.PaperItemType?.Trim() ?? "";
        if (Enum.TryParse(raw, ignoreCase: true, out ItemDrop.ItemData.ItemType parsed) &&
            (parsed == ItemDrop.ItemData.ItemType.Material || parsed == ItemDrop.ItemData.ItemType.Misc))
            return parsed;

        if (!string.IsNullOrEmpty(raw) &&
            !raw.Equals("Material", StringComparison.OrdinalIgnoreCase))
        {
            _log?.LogWarning($"[Paper] PaperItemType '{raw}' invalid; use Material or Misc. Falling back to Material.");
        }

        return ItemDrop.ItemData.ItemType.Material;
    }

    /// <summary>
    /// Blank paper must not keep a build piece table (that lets Use enter place-mode and play the unarmed punch).
    /// </summary>
    internal static void ClearBlankPlaceTool(ItemDrop.ItemData? item)
    {
        if (!IsBlankPaper(item) || item!.m_shared == null)
            return;
        if (IsWrittenPaper(item))
            return;

        item.m_shared.m_buildPieces = null;
        if (item.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Tool)
            item.m_shared.m_itemType = ResolveItemType();
    }

    internal static bool IsBlankPaper(ItemDrop.ItemData? item)
    {
        if (item?.m_shared == null)
            return false;
        if (PrefabsMatch(item, PrefabName))
            return true;
        return item.m_shared.m_name.Equals(TokenName, StringComparison.OrdinalIgnoreCase);
    }

    internal static bool IsWrittenPaper(ItemDrop.ItemData? item)
    {
        if (item?.m_shared == null)
            return false;
        if (PrefabsMatch(item, WrittenPrefabName))
            return true;
        return item.m_shared.m_name.Equals(TokenWrittenName, StringComparison.OrdinalIgnoreCase);
    }

    internal static bool IsPrintedPaper(ItemDrop.ItemData? item)
    {
        if (item?.m_shared == null)
            return false;
        if (PrefabsMatch(item, PrintedPrefabName))
            return true;
        return item.m_shared.m_name.Equals(TokenPrintedName, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Written template or Printed copy — place / ink / Paper tab.</summary>
    internal static bool IsWrittenLike(ItemDrop.ItemData? item) =>
        IsWrittenPaper(item) || IsPrintedPaper(item);

    /// <summary>Blank, Written, or Printed.</summary>
    internal static bool IsAnyPaper(ItemDrop.ItemData? item) =>
        IsBlankPaper(item) || IsWrittenLike(item);

    /// <summary>Alias for <see cref="IsAnyPaper"/> (exclusion alias + stand patches).</summary>
    internal static bool IsPaperItem(ItemDrop.ItemData? item) => IsAnyPaper(item);

    private static bool PrefabsMatch(ItemDrop.ItemData item, string prefabName)
    {
        if (item.m_dropPrefab == null)
            return false;
        var n = item.m_dropPrefab.name;
        return n.Equals(prefabName, StringComparison.OrdinalIgnoreCase) ||
               n.StartsWith(prefabName, StringComparison.OrdinalIgnoreCase);
    }

    private static Sprite? _blankIconSprite;
    private static Sprite? _writtenIconSprite;

    /// <summary>Hammer / UI icon for blank parchment (cached).</summary>
    internal static Sprite? GetBlankIconSprite()
    {
        if (_blankIconSprite != null)
            return _blankIconSprite;
        var parchment = LoadParchmentTexture()
                        ?? LoadTextureFile(Path.Combine("Items", "paper_item", "icon.png"), "forge-item-icon")
                        ?? LoadTextureFile(Path.Combine("Items", "paper_piece", "icon.png"), "forge-piece-icon");
        if (parchment == null)
            return null;
        _blankIconSprite = Sprite.Create(
            parchment,
            new Rect(0, 0, parchment.width, parchment.height),
            new Vector2(0.5f, 0.5f));
        return _blankIconSprite;
    }

    /// <summary>Piece-table / UI icon for Written Page (cached).</summary>
    internal static Sprite? GetWrittenIconSprite()
    {
        if (_writtenIconSprite != null)
            return _writtenIconSprite;
        var parchment = LoadWrittenParchmentTexture() ?? LoadParchmentTexture();
        if (parchment == null)
            return null;
        _writtenIconSprite = Sprite.Create(
            parchment,
            new Rect(0, 0, parchment.width, parchment.height),
            new Vector2(0.5f, 0.5f));
        return _writtenIconSprite;
    }

    /// <summary>
    /// Strip donor mesh/UI and build a single parchment sheet on a build piece (no Sign / no interact).
    /// <paramref name="wall"/> true = vertical facing +Z; false = flat on XZ.
    /// <paramref name="written"/> true = paper_written.png scribbles (same cutout as blank).
    /// </summary>
    internal static void BuildDecorSheetVisual(GameObject? root, bool wall, bool written = false)
    {
        if (root == null)
            return;

        try
        {
            // Capture a usable Valheim material before donor meshes are gone.
            var donorMat = CaptureDonorMaterial(root) ?? FindFallbackWorldMaterial();
            StripPieceDonorVisuals(root);
            var attachGo = new GameObject("drakes_paper_decor");
            var attach = attachGo.transform;
            attach.SetParent(root.transform, false);
            // Flat: sit above the hit surface (tables with gaps / uneven tops).
            attach.localPosition = wall ? new Vector3(0f, 0f, -0.002f) : new Vector3(0f, FlatSurfaceLift, 0f);
            attach.localScale = Vector3.one;

            var paperMat = CreateDecorPaperMaterial(written);
            var usedForge = PaperAssets.AttachForgeArt(attach, forItem: false, written, paperMat) != null;
            if (usedForge)
            {
                // Forge paper_piece is authored lying on XZ (face +Y). Wall tips it up to face +Z.
                attach.localRotation = wall ? Quaternion.Euler(90f, 0f, 0f) : Quaternion.identity;
            }
            else
            {
                // Procedural quads face +Z; flat tips them onto the table.
                attach.localRotation = wall ? Quaternion.identity : Quaternion.Euler(90f, 0f, 0f);
                CreatePaperFace(attach, "paper_front", paperMat, Quaternion.identity, new Vector3(0f, 0f, 0.003f));
                CreatePaperFace(attach, "paper_back", paperMat, Quaternion.Euler(0f, 180f, 0f), new Vector3(0f, 0f, -0.003f));
            }

            EnsureThinBoxCollider(root, wall);
            EnsurePaperSnapPoints(root, wall ? PaperSnapKind.Wall : PaperSnapKind.Flat);
            ConfigurePaperPlacement(root, wall);
            EnsurePieceLayer(root);
            SanitizePaperWearNTear(root);
            PaperAssets.EnsurePersistentZNetView(root);
            EnsureVisualDepthBias(root, wall);
            _log?.LogInfo($"[Paper] Decor sheet on '{root.name}' wall={wall} written={written} forge={(usedForge ? "yes" : "quad-fallback")}.");
        }
        catch (Exception ex)
        {
            _log?.LogWarning($"[Paper] Decor sheet failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Swap a placed note's parchment albedo between blank mesh and scribble texture.
    /// Does not touch inventory / floor item prefabs.
    /// </summary>
    internal static void SetNotePieceScribbles(GameObject? root, bool scribbles)
    {
        if (root == null)
            return;
        try
        {
            var decor = root.transform.Find("drakes_paper_decor");
            if (decor == null)
                return;
            var mat = CreateDecorPaperMaterial(scribbles);
            if (mat == null)
                return;
            var renderers = decor.GetComponentsInChildren<Renderer>(true);
            for (var i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (r == null || r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer)
                    continue;
                // Never stomp the on-page text mesh with parchment albedo.
                if (r.GetComponent<TMPro.TMP_Text>() != null || r.GetComponentInParent<TMPro.TMP_Text>() != null)
                    continue;
                r.sharedMaterial = mat;
                r.enabled = true;
            }
        }
        catch (Exception ex)
        {
            _log?.LogWarning($"[Paper] Note albedo swap skipped: {ex.Message}");
        }
    }

    /// <summary>Stacked parchment pile (wood-stack style d├⌐cor).</summary>
    internal static void BuildPaperStackVisual(GameObject? root, int sheets = 12)
    {
        if (root == null)
            return;

        try
        {
            StripPieceDonorVisuals(root);
            var donorMat = FindFallbackWorldMaterial();
            var attachGo = new GameObject("drakes_paper_stack");
            var attach = attachGo.transform;
            attach.SetParent(root.transform, false);
            attach.localPosition = new Vector3(0f, FlatSurfaceLift, 0f);
            attach.localRotation = Quaternion.identity;
            attach.localScale = Vector3.one;

            sheets = Math.Max(4, Math.Min(sheets, 20));
            float thickness = 0.008f;
            var mat = CreateDecorPaperMaterial(written: false);
            for (var i = 0; i < sheets; i++)
            {
                float y = i * thickness + thickness * 0.5f;
                float yaw = (i % 3 - 1) * 4f + (i % 5) * 0.5f;
                float scaleJitter = 1f - (i % 4) * 0.015f;
                var sheet = new GameObject($"sheet_{i}");
                var st = sheet.transform;
                st.SetParent(attach, false);
                st.localPosition = new Vector3(0f, y, 0f);
                st.localScale = new Vector3(scaleJitter, scaleJitter, 1f);
                if (PaperAssets.AttachForgeArt(st, forItem: false, written: false, mat) != null)
                {
                    // Forge mesh is already flat on XZ.
                    st.localRotation = Quaternion.Euler(0f, yaw, 0f);
                    continue;
                }

                // Procedural quads face +Z — tip onto the pile.
                st.localRotation = Quaternion.Euler(90f, yaw, 0f);
                CreatePaperFace(st, "front", mat, Quaternion.identity, new Vector3(0f, 0f, 0.0005f));
                CreatePaperFace(st, "back", mat, Quaternion.Euler(0f, 180f, 0f), new Vector3(0f, 0f, -0.0005f));
            }

            foreach (var col in root.GetComponentsInChildren<Collider>(true))
                Object.DestroyImmediate(col);
            var box = root.AddComponent<BoxCollider>();
            float stackHeight = sheets * thickness;
            box.center = new Vector3(0f, FlatSurfaceLift + stackHeight * 0.5f, 0f);
            box.size = new Vector3(PaperSize.x * 1.05f, Math.Max(0.02f, stackHeight), PaperSize.y * 1.05f);

            EnsurePaperSnapPoints(root, PaperSnapKind.Stack, stackHeight);
            ConfigurePaperPlacement(root, wall: false);
            EnsurePieceLayer(root);
            SanitizePaperWearNTear(root);
            PaperAssets.EnsurePersistentZNetView(root);
            _log?.LogInfo($"[Paper] Paper stack visual on '{root.name}' sheets={sheets}.");
        }
        catch (Exception ex)
        {
            _log?.LogWarning($"[Paper] Paper stack visual failed: {ex.Message}");
        }
    }

    /// <summary>Valheim only recognizes direct children tagged <c>snappoint</c>.</summary>
    internal enum PaperSnapKind
    {
        Wall,
        Flat,
        Stack,
    }

    /// <summary>
    /// Replace donor snap points. Direct children with tag <c>snappoint</c>.
    /// GameObject name is the HUD label (Extra Snap Points / manual snap UI).
    /// </summary>
    internal static void EnsurePaperSnapPoints(GameObject root, PaperSnapKind kind, float stackHeight = 0.12f)
    {
        if (root == null)
            return;

        ClearSnapPointChildren(root.transform);

        float hx = PaperSize.x * 0.5f;
        float hy = PaperSize.y * 0.5f;

        switch (kind)
        {
            case PaperSnapKind.Wall:
                // Local +X = right, +Y = up (piece +Z faces the player).
                AddSnap(root, "Top Left", new Vector3(-hx, hy, 0f));
                AddSnap(root, "Top", new Vector3(0f, hy, 0f));
                AddSnap(root, "Top Right", new Vector3(hx, hy, 0f));
                AddSnap(root, "Bottom Left", new Vector3(-hx, -hy, 0f));
                AddSnap(root, "Bottom", new Vector3(0f, -hy, 0f));
                AddSnap(root, "Bottom Right", new Vector3(hx, -hy, 0f));
                break;

            case PaperSnapKind.Flat:
                // Match flat collider underside (FlatSurfaceLift).
                // Local -Z = toward player (Front), +X = right.
                float flatY = FlatSurfaceLift;
                AddSnap(root, "Front Left", new Vector3(hx, flatY, -hy));
                AddSnap(root, "Front Right", new Vector3(-hx, flatY, -hy));
                AddSnap(root, "Back Left", new Vector3(hx, flatY, hy));
                AddSnap(root, "Back Right", new Vector3(-hx, flatY, hy));
                // No Center snap — stacking on Center left sheets coplanar and z-fighting.
                break;

            case PaperSnapKind.Stack:
                float yBot = FlatSurfaceLift;
                float yTop = yBot + Math.Max(0.02f, stackHeight);
                AddSnap(root, "Bottom Front Left", new Vector3(hx, yBot, -hy));
                AddSnap(root, "Bottom Front Right", new Vector3(-hx, yBot, -hy));
                AddSnap(root, "Bottom Back Left", new Vector3(hx, yBot, hy));
                AddSnap(root, "Bottom Back Right", new Vector3(-hx, yBot, hy));
                AddSnap(root, "Top Front Left", new Vector3(hx, yTop, -hy));
                AddSnap(root, "Top Front Right", new Vector3(-hx, yTop, -hy));
                AddSnap(root, "Top Back Left", new Vector3(hx, yTop, hy));
                AddSnap(root, "Top Back Right", new Vector3(-hx, yTop, hy));
                AddSnap(root, "Top Center", new Vector3(0f, yTop, 0f));
                break;
        }
    }

    private static void ClearSnapPointChildren(Transform root)
    {
        for (var i = root.childCount - 1; i >= 0; i--)
        {
            var child = root.GetChild(i);
            var named = child.name.IndexOf("snap", StringComparison.OrdinalIgnoreCase) >= 0;
            var tagged = false;
            try
            {
                tagged = child.CompareTag("snappoint");
            }
            catch
            {
                /* tag may be missing in some contexts */
            }

            if (tagged || named)
                Object.DestroyImmediate(child.gameObject);
        }
    }

    private static void AddSnap(GameObject root, string label, Vector3 localPos)
    {
        // Tag drives vanilla snap; name is the "Snapping: ΓÇª" HUD label.
        var go = new GameObject(string.IsNullOrWhiteSpace(label) ? "Center" : label);
        var t = go.transform;
        t.SetParent(root.transform, false);
        t.localPosition = localPos;
        t.localRotation = Quaternion.identity;
        t.localScale = Vector3.one;
        try
        {
            go.tag = "snappoint";
        }
        catch (Exception ex)
        {
            _log?.LogWarning($"[Paper] Failed to tag snappoint '{label}': {ex.Message}");
        }
    }


    /// <summary>
    /// Nudge the parchment visual a hair off the shared placement plane so two sheets
    /// on the same table/wall do not z-fight (flicker which is on top).
    /// </summary>
    internal static void EnsureVisualDepthBias(GameObject root, bool wall)
    {
        if (root == null)
            return;
        var bias = root.GetComponent<PaperVisualDepthBias>();
        if (bias == null)
            bias = root.AddComponent<PaperVisualDepthBias>();
        bias.Configure(wall);
    }
    internal static bool IsWallSheetPiece(Piece? piece)
    {
        if (piece == null)
            return false;
        var n = piece.gameObject.name;
        return n.StartsWith(PaperPlace.BlankUpright, StringComparison.Ordinal)
               || n.StartsWith(PaperWrittenPlace.NoteVertical, StringComparison.Ordinal)
               || n.StartsWith(PaperBlankPlace.PlaceVertical, StringComparison.Ordinal);
    }

    /// <summary>
    /// Move a vertical sheet onto the near face of the aimed wall (toward the camera).
    /// Idempotent: calling again on an already-seated point keeps it there.
    /// Keeps the point's position in the wall plane so snaps are not undone ΓÇö only depth changes.
    /// </summary>
    internal static Vector3 SeatWallSheet(Vector3 proposed, Transform ghost)
    {
        if (ghost == null)
            return proposed;

        var cam = Utils.GetMainCamera();
        if (cam == null)
            return proposed;

        var origin = cam.transform.position;
        var delta = proposed - origin;
        var dist = delta.magnitude;
        if (dist < 0.2f)
            return proposed;

        var dir = delta / dist;
        var hits = Physics.RaycastAll(origin, dir, dist + 1.25f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        var found = false;
        var facePoint = Vector3.zero;
        var faceNormal = Vector3.zero;
        var bestAbs = float.MaxValue;
        foreach (var hit in hits)
        {
            if (hit.collider == null)
                continue;
            var t = hit.collider.transform;
            if (t == ghost || t.IsChildOf(ghost) || ghost.IsChildOf(t))
                continue;

            // Face toward the camera, or the face we're already sitting on (small positive = already gapped out).
            var along = Vector3.Dot(hit.point - proposed, dir);
            if (along > WallFaceGap + 0.03f || along < -1.15f)
                continue;

            var abs = Mathf.Abs(along);
            if (abs >= bestAbs)
                continue;
            bestAbs = abs;
            facePoint = hit.point;
            faceNormal = hit.normal;
            found = true;
        }

        if (!found)
            return proposed;

        if (Vector3.Dot(faceNormal, origin - facePoint) < 0f)
            faceNormal = -faceNormal;
        if (faceNormal.sqrMagnitude < 0.0001f)
            return proposed;

        var seated = facePoint + faceNormal.normalized * WallFaceGap;
        return proposed + Vector3.Project(seated - proposed, faceNormal);
    }

    /// <summary>
    /// Free placement on walls, tables, floors. Flat/stack sit ON TOP of aimed surfaces
    /// (not terrain-floor / wood_stack heightmap behavior).
    /// </summary>
    internal static void ConfigurePaperPlacement(GameObject? root, bool wall)
    {
        var piece = root != null ? root.GetComponent<Piece>() : null;
        if (piece == null)
            return;

        piece.m_groundPiece = false;
        piece.m_groundOnly = false;
        piece.m_waterPiece = false;
        piece.m_noInWater = true;
        piece.m_noClipping = false;
        piece.m_notOnTiltingSurface = false;
        piece.m_randomInitBuildRotation = false;
        piece.m_canBeRemoved = true;

        if (wall)
        {
            // Vertical: same as working upright ΓÇö clip to walls/structures.
            piece.m_clipGround = true;
            piece.m_clipEverything = true;
            piece.m_allowAltGroundPlacement = true;
        }
        else
        {
            // Flat / stack: sit ON TOP of tables/floors/books ΓÇö not wood_floor heightmap pieces.
            // clipGround=true makes Valheim prefer terrain like floorboards; keep piece-to-piece only.
            piece.m_clipGround = false;
            piece.m_clipEverything = true;
            piece.m_allowAltGroundPlacement = true;
        }
    }

    /// <summary>Snappoints require the piece collider on layer piece / piece_nonsolid.</summary>
    internal static void EnsurePieceLayer(GameObject? root)
    {
        if (root == null)
            return;

        var layer = LayerMask.NameToLayer("piece");
        if (layer < 0)
            layer = 10;

        root.layer = layer;
        foreach (var col in root.GetComponentsInChildren<Collider>(true))
        {
            if (col == null)
                continue;
            if (col.GetComponentInParent<Canvas>() != null)
                continue;
            col.gameObject.layer = layer;
        }
    }

    /// <summary>
    /// Remove donor wood meshes/colliders but keep Sign + Canvas/TMP for written notes.
    /// </summary>
    internal static void StripDonorGeometryKeepSignUi(GameObject root)
    {
        if (root == null)
            return;

        var doomed = new List<GameObject>();
        foreach (Transform child in root.transform)
        {
            if (child.name.StartsWith("drakes_paper", StringComparison.OrdinalIgnoreCase))
                continue;
            if (child.GetComponentInChildren<Canvas>(true) != null)
                continue;
            if (child.GetComponentInChildren<TMPro.TMP_Text>(true) != null)
                continue;
            if (child.name.IndexOf("snap", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                doomed.Add(child.gameObject);
                continue;
            }

            if (child.GetComponent<MeshFilter>() != null ||
                child.GetComponentInChildren<MeshFilter>(true) != null)
                doomed.Add(child.gameObject);
        }

        foreach (var go in doomed)
            Object.DestroyImmediate(go);

        foreach (var col in root.GetComponentsInChildren<Collider>(true))
        {
            if (col == null)
                continue;
            if (col.GetComponentInParent<Canvas>() != null)
                continue;
            Object.DestroyImmediate(col);
        }
    }

    /// <summary>Kill wood_stack / sign wood debris so breaks don't spray logs; keep a soft fabric SFX.</summary>
    internal static void SanitizePaperWearNTear(GameObject? root)
    {
        if (root == null)
            return;

        var wnt = root.GetComponent<WearNTear>();
        if (wnt == null)
            return;

        wnt.m_autoCreateFragments = false;
        wnt.m_fragmentRoots = Array.Empty<GameObject>();
        ApplySoftPaperBreakEffects(wnt);

        // Donor fragment mesh roots sometimes live as children.
        for (var i = root.transform.childCount - 1; i >= 0; i--)
        {
            var child = root.transform.GetChild(i);
            var n = child.name;
            if (n.IndexOf("fragment", StringComparison.OrdinalIgnoreCase) >= 0 ||
                n.IndexOf("debris", StringComparison.OrdinalIgnoreCase) >= 0)
                Object.DestroyImmediate(child.gameObject);
        }
    }

    /// <summary>
    /// Sign/wood donors are wiped above; copy hit/destroy SFX from a soft cloth/rug piece instead.
    /// </summary>
    private static void ApplySoftPaperBreakEffects(WearNTear wnt)
    {
        // Soft fabric donors — paper tearing should sound like cloth, not planks.
        string[] donors =
        {
            "rug_deer",
            "piece_banner01",
            "piece_cloth_hanging",
            "piece_clothdoor",
            "Banner",
        };

        foreach (var name in donors)
        {
            var go = PrefabManager.Instance?.GetPrefab(name);
            if (go == null && ZNetScene.instance != null)
            {
                try { go = ZNetScene.instance.GetPrefab(name); }
                catch { /* prefab hash miss */ }
            }

            var donor = go != null ? go.GetComponent<WearNTear>() : null;
            if (donor == null)
                continue;

            var destroyed = donor.m_destroyedEffect;
            var hit = donor.m_hitEffect;
            var hasDestroyed = destroyed?.m_effectPrefabs != null && destroyed.m_effectPrefabs.Length > 0;
            var hasHit = hit?.m_effectPrefabs != null && hit.m_effectPrefabs.Length > 0;
            if (!hasDestroyed && !hasHit)
                continue;

            if (hasDestroyed)
                wnt.m_destroyedEffect = destroyed;
            else
                wnt.m_destroyedEffect = new EffectList();

            if (hasHit)
                wnt.m_hitEffect = hit;
            else
                wnt.m_hitEffect = new EffectList();

            _log?.LogInfo($"[Paper] Break/hit SFX copied from '{name}'.");
            return;
        }

        wnt.m_destroyedEffect = new EffectList();
        wnt.m_hitEffect = new EffectList();
        _log?.LogWarning("[Paper] No soft WearNTear donor found for break SFX; destroy stays silent.");
    }

    private static Material CreateDecorPaperMaterial(bool written)
    {
        var blank = LoadParchmentTexture();
        var blankMesh = LoadMeshTexture() ?? (blank != null ? CreateCutoutMeshTexture(blank) : null);
        Texture2D? meshTex;
        if (written)
        {
            var writtenParchment = LoadWrittenParchmentTexture() ?? blank;
            meshTex = CreateWrittenMeshTexture(writtenParchment, blank, blankMesh);
        }
        else
        {
            meshTex = blankMesh ?? (blank != null ? CreateCutoutMeshTexture(blank) : null);
        }

        var donorMat = FindFallbackWorldMaterial() ?? FindCutoutWorldMaterialFromObjectDb();
        var tex = meshTex != null ? meshTex : CreateBlankParchmentTexture(64);
        var mat = CreatePaperMaterial(tex, donorMat);
        if (!MaterialSupportsCutout(mat))
        {
            tex = MakeOpaqueInpainted(tex);
            if (mat.HasProperty("_MainTex"))
                mat.SetTexture("_MainTex", tex);
            mat.mainTexture = tex;
        }

        return mat;
    }

    /// <summary>
    /// True when the transform lives under a <c>drakes_paper*</c> holder we added.
    /// Stops below <paramref name="root"/>: the piece prefab itself is named
    /// <c>Drakes_PaperNote_*</c>, so walking past it would spare every donor visual.
    /// </summary>
    private static bool IsOurs(Transform? t, Transform root)
    {
        while (t != null && t != root)
        {
            if (t.name.StartsWith("drakes_paper", StringComparison.OrdinalIgnoreCase))
                return true;
            t = t.parent;
        }

        return false;
    }

    private static void StripPieceDonorVisuals(GameObject root)
    {
        var rootT = root.transform;
        // Remove Sign / UI so the piece is bric-a-brac only (no [E] write).
        // The note's preserved Sign text widget lives under drakes_paper_signtext — keep it.
        foreach (var sign in root.GetComponentsInChildren<Sign>(true))
            Object.DestroyImmediate(sign);
        foreach (var canvas in root.GetComponentsInChildren<Canvas>(true))
        {
            if (canvas != null && !IsOurs(canvas.transform, rootT))
                Object.DestroyImmediate(canvas.gameObject);
        }
        foreach (var tmp in root.GetComponentsInChildren<TMPro.TMP_Text>(true))
        {
            if (tmp != null && !IsOurs(tmp.transform, rootT))
                Object.DestroyImmediate(tmp.gameObject);
        }

        // wood_stack / floor donors force terrain height ΓÇö strip those.
        foreach (var tm in root.GetComponentsInChildren<TerrainModifier>(true))
            Object.DestroyImmediate(tm);

        foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer == null)
                continue;
            // Preserved on-page text renders through its own mesh — leave it on.
            if (IsOurs(renderer.transform, rootT))
                continue;
            if (renderer is ParticleSystemRenderer || renderer is TrailRenderer || renderer is LineRenderer)
            {
                renderer.enabled = false;
                continue;
            }

            renderer.enabled = false;
        }

        // Prefer destroying disabled mesh children that are not our decor.
        var doomed = new List<GameObject>();
        foreach (Transform child in root.transform)
        {
            if (IsOurs(child, rootT))
                continue;
            // Keep structural empty roots; kill obvious mesh holders
            if (child.GetComponent<MeshFilter>() != null || child.GetComponentInChildren<MeshFilter>(true) != null)
                doomed.Add(child.gameObject);
        }

        foreach (var go in doomed)
            Object.DestroyImmediate(go);

        // Root-level mesh (some donors) ΓÇö destroy filter/renderer, keep Piece/ZNetView.
        var rootFilter = root.GetComponent<MeshFilter>();
        if (rootFilter != null)
            Object.DestroyImmediate(rootFilter);
        var rootRenderer = root.GetComponent<MeshRenderer>();
        if (rootRenderer != null)
            Object.DestroyImmediate(rootRenderer);
    }

    internal static void EnsureThinBoxCollider(GameObject root, bool wall)
    {
        // Donor Sign leaves a large board collider on children ΓÇö that skews hover far off the sheet.
        foreach (var col in root.GetComponentsInChildren<Collider>(true))
            Object.DestroyImmediate(col);

        var box = root.AddComponent<BoxCollider>();
        if (wall)
        {
            // Thin slab flush to the wall plane (local Z toward player).
            box.center = new Vector3(0f, 0f, 0.004f);
            box.size = new Vector3(PaperSize.x, PaperSize.y, 0.01f);
        }
        else
        {
            // Collider bottom at FlatSurfaceLift so the piece sits on top of uneven tables.
            const float half = 0.01f;
            box.center = new Vector3(0f, FlatSurfaceLift + half, 0f);
            box.size = new Vector3(PaperSize.x, half * 2f, PaperSize.y);
        }
    }

    /// <summary>
    /// SPIKE helper: vertical double-sided parchment on a wall piece (quads face +Z like a sign).
    /// Does not strip existing children ΓÇö caller should hide wood meshes first.
    /// </summary>
    internal static void ApplySpikeWallPaperVisual(GameObject? root) =>
        ApplySpikeWallPaperVisual(root, written: false);

    internal static void ApplySpikeWallPaperVisual(GameObject? root, bool written)
    {
        if (root == null)
            return;

        try
        {
            var blank = LoadParchmentTexture();
            var blankMesh = LoadMeshTexture() ?? (blank != null ? CreateCutoutMeshTexture(blank) : null);
            Texture2D? meshTex;
            if (written)
            {
                var writtenParchment = LoadWrittenParchmentTexture() ?? blank;
                meshTex = CreateWrittenMeshTexture(writtenParchment, blank, blankMesh);
            }
            else
            {
                meshTex = blankMesh ?? (blank != null ? CreateCutoutMeshTexture(blank) : null);
            }

            var donorMat = CaptureDonorMaterial(root) ?? FindFallbackWorldMaterial() ?? FindCutoutWorldMaterialFromObjectDb();
            var tex = meshTex != null ? meshTex : CreateBlankParchmentTexture(64);
            var mat = CreatePaperMaterial(tex, donorMat);
            if (!MaterialSupportsCutout(mat))
            {
                tex = MakeOpaqueInpainted(tex);
                if (mat.HasProperty("_MainTex"))
                    mat.SetTexture("_MainTex", tex);
                mat.mainTexture = tex;
            }

            var attachGo = new GameObject("drakes_paper_spike");
            var attach = attachGo.transform;
            attach.SetParent(root.transform, false);
            attach.localPosition = Vector3.zero;
            attach.localRotation = Quaternion.identity;
            attach.localScale = Vector3.one;

            CreatePaperFace(attach, "paper_front", mat, Quaternion.identity, new Vector3(0f, 0f, 0.001f));
            CreatePaperFace(attach, "paper_back", mat, Quaternion.Euler(0f, 180f, 0f), new Vector3(0f, 0f, -0.001f));

            // Caller may rotate the visual for flat notes; snaps are set by the place registrar.
            _log?.LogInfo($"[Paper] Wall visual attached on '{root.name}' written={written}.");
        }
        catch (Exception ex)
        {
            _log?.LogWarning($"[Paper] Wall visual failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Strip donor visuals and build a flat double-sided parchment under <c>attach</c>.
    /// </summary>
    private static void BuildPaper2Visual(GameObject? prefab, Texture2D? meshTexture, bool written)
    {
        if (prefab == null)
            return;

        var znet = prefab.GetComponent<ZNetView>();
        if (znet != null)
            znet.m_syncInitialScale = true;

        prefab.transform.localScale = Vector3.one;

        // Grab a real Valheim shader BEFORE stripping — Shader.Find often returns null in Valheim.
        var donorMat = CaptureDonorMaterial(prefab);

        // LeatherScraps (and similar) often keep MeshFilter/Renderer on the root, not only children.
        StripRootAndChildDonorMeshes(prefab);

        // Remove remaining donor children (VFX / LOD / attach leftovers).
        var doomed = new List<GameObject>();
        foreach (Transform child in prefab.transform)
            doomed.Add(child.gameObject);
        foreach (var go in doomed)
            Object.DestroyImmediate(go);

        // Collider matches a thin sheet lifted above grass / terrain.
        foreach (var col in prefab.GetComponents<Collider>())
            Object.DestroyImmediate(col);
        var box = prefab.AddComponent<BoxCollider>();
        box.center = new Vector3(0f, GroundClearance, 0f);
        box.size = new Vector3(PaperSize.x, 0.02f, PaperSize.y);

        var attachGo = new GameObject("attach");
        var attach = attachGo.transform;
        attach.SetParent(prefab.transform, false);
        attach.localPosition = new Vector3(0f, GroundClearance, 0f);
        attach.localRotation = Quaternion.identity;
        attach.localScale = Vector3.one;

        // World mesh uses hard-cutout paper_mesh.png (icon keeps transparent paper.png).
        var tex = meshTexture != null ? meshTexture : CreateBlankParchmentTexture(64);
        var mat = CreatePaperMaterial(tex, donorMat);

        // Prefer forge art with parchment material (never leave LeatherScraps material on the mesh).
        if (PaperAssets.AttachForgeArt(attach, forItem: true, written, mat) != null)
        {
            DisableNonArtRenderers(prefab, attach);
            _log?.LogInfo($"[Paper] Item visual from forge art on '{prefab.name}' written={written}.");
            return;
        }

        // If the Valheim shader ignores alpha, bake a fringe-free opaque skin (nearest parchment, not white).
        if (!MaterialSupportsCutout(mat))
        {
            tex = MakeOpaqueInpainted(tex);
            if (mat.HasProperty("_MainTex"))
                mat.SetTexture("_MainTex", tex);
            mat.mainTexture = tex;
            _log?.LogInfo($"[Paper] Donor shader '{mat.shader?.name}' has no cutout; using opaque inpainted mesh skin.");
        }

        // Quads face +Z by default. Lay flat on XZ (face up) for ground / horizontal stands.
        // Tiny separation so the two faces never z-fight each other.
        CreatePaperFace(attach, "paper_front", mat, Quaternion.Euler(90f, 0f, 0f), new Vector3(0f, 0.001f, 0f));
        CreatePaperFace(attach, "paper_back", mat, Quaternion.Euler(-90f, 0f, 0f), new Vector3(0f, -0.001f, 0f));

        _log?.LogInfo(
            $"[Paper] Paper2 visual built ({PaperSize.x:0.##}x{PaperSize.y:0.##}m, clearance {GroundClearance:0.###}m, " +
            $"shader='{mat.shader?.name}', tex={tex.width}x{tex.height}, cutout={MaterialSupportsCutout(mat)}).");
    }

    /// <summary>
    /// Donor clones often keep MeshFilter / MeshRenderer on the root GameObject.
    /// Destroying only children leaves LeatherScraps (etc.) visible forever.
    /// </summary>
    private static void StripRootAndChildDonorMeshes(GameObject prefab)
    {
        if (prefab == null)
            return;

        foreach (var lod in prefab.GetComponentsInChildren<LODGroup>(true))
        {
            if (lod != null)
                Object.DestroyImmediate(lod);
        }

        foreach (var skinned in prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (skinned != null)
                Object.DestroyImmediate(skinned);
        }

        foreach (var filter in prefab.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter == null)
                continue;
            var renderer = filter.GetComponent<MeshRenderer>();
            if (renderer != null)
                Object.DestroyImmediate(renderer);
            Object.DestroyImmediate(filter);
        }

        foreach (var renderer in prefab.GetComponentsInChildren<MeshRenderer>(true))
        {
            if (renderer != null)
                Object.DestroyImmediate(renderer);
        }

        foreach (var renderer in prefab.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer == null)
                continue;
            if (renderer is ParticleSystemRenderer || renderer is TrailRenderer || renderer is LineRenderer)
                renderer.enabled = false;
        }
    }

    /// <summary>Belt-and-suspenders: hide any leftover donor renderers outside the art attach.</summary>
    private static void DisableNonArtRenderers(GameObject root, Transform artAttach)
    {
        if (root == null || artAttach == null)
            return;

        foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer == null)
                continue;
            if (renderer.transform == artAttach || renderer.transform.IsChildOf(artAttach))
                continue;
            renderer.enabled = false;
        }
    }

    private static Material? CaptureDonorMaterial(GameObject prefab)
    {
        // Prefer a cutout-capable mesh material ΓÇö opaque fills cause white alpha fringes on torn edges.
        var cutout = FindCutoutWorldMaterial(prefab) ?? FindCutoutWorldMaterialFromObjectDb();
        if (cutout != null)
            return cutout;

        // Prefer MeshRenderer ΓÇö LeatherScraps also has particle renderers; those shaders blow out white on stands.
        foreach (var renderer in prefab.GetComponentsInChildren<MeshRenderer>(true))
        {
            if (IsUsableWorldMaterial(renderer.sharedMaterial))
                return renderer.sharedMaterial;
        }

        foreach (var renderer in prefab.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer is ParticleSystemRenderer || renderer is TrailRenderer || renderer is LineRenderer)
                continue;
            if (IsUsableWorldMaterial(renderer.sharedMaterial))
                return renderer.sharedMaterial;
        }

        return FindFallbackWorldMaterial();
    }

    private static Material? FindCutoutWorldMaterial(GameObject prefab)
    {
        foreach (var renderer in prefab.GetComponentsInChildren<MeshRenderer>(true))
        {
            var mat = renderer.sharedMaterial;
            if (IsUsableWorldMaterial(mat) && MaterialSupportsCutout(mat))
                return mat;
        }

        return null;
    }

    private static Material? FindCutoutWorldMaterialFromObjectDb()
    {
        if (ObjectDB.instance == null)
            return null;

        // Plants / trophies often ship with real alpha-cutout materials.
        string[] donors =
        {
            "Dandelion", "Mushroom", "Raspberry", "Blueberries", "Thistle", "Cloudberry",
            "WitheredBone", "TrophyDeer",
        };
        foreach (var name in donors)
        {
            var go = ObjectDB.instance.GetItemPrefab(name);
            if (go == null)
                continue;
            var mat = FindCutoutWorldMaterial(go);
            if (mat != null)
                return mat;
        }

        return null;
    }

    private static bool MaterialSupportsCutout(Material? mat)
    {
        if (mat?.shader == null)
            return false;

        // Only trust shaders that actually alpha-test. Custom/Creature often exposes _Cutoff unused.
        var n = mat.shader.name;
        return n.IndexOf("Cutout", StringComparison.OrdinalIgnoreCase) >= 0
               || n.IndexOf("Vegetation", StringComparison.OrdinalIgnoreCase) >= 0
               || n.IndexOf("Leaf", StringComparison.OrdinalIgnoreCase) >= 0
               || n.IndexOf("Grass", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static Material? FindFallbackWorldMaterial()
    {
        if (ObjectDB.instance == null)
            return null;

        // Simple opaque item meshes ΓÇö never particles.
        string[] donors = { "Wood", "FineWood", "Stone", "Coal", "Resin", "Flint", "LeatherScraps" };
        foreach (var name in donors)
        {
            var go = ObjectDB.instance.GetItemPrefab(name);
            if (go == null)
                continue;
            foreach (var renderer in go.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (IsUsableWorldMaterial(renderer.sharedMaterial))
                    return renderer.sharedMaterial;
            }
        }

        return null;
    }

    private static bool IsUsableWorldMaterial(Material? mat)
    {
        var shader = mat?.shader;
        if (shader == null)
            return false;

        var n = shader.name;
        if (n.IndexOf("Particle", StringComparison.OrdinalIgnoreCase) >= 0)
            return false;
        if (n.IndexOf("Additive", StringComparison.OrdinalIgnoreCase) >= 0)
            return false;
        if (n.IndexOf("UI", StringComparison.OrdinalIgnoreCase) >= 0)
            return false;
        if (n.IndexOf("Sprite", StringComparison.OrdinalIgnoreCase) >= 0)
            return false;
        if (n.IndexOf("Internal", StringComparison.OrdinalIgnoreCase) >= 0)
            return false;

        return true;
    }

    private static void CreatePaperFace(
        Transform parent,
        string name,
        Material mat,
        Quaternion localRot,
        Vector3 localPos)
    {
        var face = GameObject.CreatePrimitive(PrimitiveType.Quad);
        face.name = name;
        Object.DestroyImmediate(face.GetComponent<Collider>());

        var t = face.transform;
        t.SetParent(parent, false);
        t.localPosition = localPos;
        t.localRotation = localRot;
        t.localScale = new Vector3(PaperSize.x, PaperSize.y, 1f);

        var renderer = face.GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = mat;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = true;
            face.layer = parent.gameObject.layer;
        }
    }

    private static Texture2D CreateBlankParchmentTexture(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "Drakes_PaperBlank",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
        };
        var pixels = new Color[size * size];
        for (var i = 0; i < pixels.Length; i++)
            pixels[i] = OffWhite;
        tex.SetPixels(pixels);
        tex.Apply(false, false);
        return tex;
    }

    /// <summary>
    /// Crops parchment and hardens alpha so cutout materials clip torn edges without white fringe.
    /// </summary>
    private static Texture2D CreateCutoutMeshTexture(Texture2D source)
    {
        var readable = EnsureReadableTexture(source);
        var w = readable.width;
        var h = readable.height;
        var pixels = readable.GetPixels();
        const float keep = 0.55f;

        var minX = w;
        var minY = h;
        var maxX = -1;
        var maxY = -1;
        for (var y = 0; y < h; y++)
        {
            for (var x = 0; x < w; x++)
            {
                if (pixels[y * w + x].a < keep)
                    continue;
                if (x < minX) minX = x;
                if (y < minY) minY = y;
                if (x > maxX) maxX = x;
                if (y > maxY) maxY = y;
            }
        }

        if (maxX < minX || maxY < minY)
            return CreateBlankParchmentTexture(64);

        var cw = maxX - minX + 1;
        var ch = maxY - minY + 1;
        var cropped = new Color[cw * ch];
        for (var y = 0; y < ch; y++)
        {
            for (var x = 0; x < cw; x++)
            {
                var c = pixels[(minY + y) * w + (minX + x)];
                // Hard cutout: discard soft fringe (those bright low-alpha rim pixels).
                cropped[y * cw + x] = c.a < keep
                    ? new Color(0f, 0f, 0f, 0f)
                    : new Color(c.r, c.g, c.b, 1f);
            }
        }

        var tex = new Texture2D(cw, ch, TextureFormat.RGBA32, false)
        {
            name = "Drakes_PaperMesh",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
        };
        tex.SetPixels(cropped);
        tex.Apply(false, false);
        return tex;
    }

    /// <summary>
    /// Written world mesh: same cutout crop/harden as blank, sourced from paper_written.png.
    /// Do not UV-map the full square (black margins) onto a cropped mask ΓÇö that paints a solid black border.
    /// </summary>
    private static Texture2D? CreateWrittenMeshTexture(
        Texture2D? writtenParchment,
        Texture2D? blankParchment,
        Texture2D? blankMeshTex)
    {
        if (writtenParchment == null)
            return blankMeshTex ?? (blankParchment != null ? CreateCutoutMeshTexture(blankParchment) : null);

        // paper_written.png is paper.png + scribbles ΓÇö identical layout; crop like blank.
        return CreateCutoutMeshTexture(writtenParchment);
    }

    /// <summary>
    /// Fills transparent texels with the nearest solid parchment color (never bright white OffWhite).
    /// Used when the world shader cannot alpha-test.
    /// </summary>
    private static Texture2D MakeOpaqueInpainted(Texture2D source)
    {
        var readable = EnsureReadableTexture(source);
        var w = readable.width;
        var h = readable.height;
        var pixels = readable.GetPixels();
        const float solid = 0.55f;

        var opaque = new List<Vector2Int>();
        for (var y = 0; y < h; y++)
        {
            for (var x = 0; x < w; x++)
            {
                if (pixels[y * w + x].a >= solid)
                    opaque.Add(new Vector2Int(x, y));
            }
        }

        if (opaque.Count == 0)
            return CreateBlankParchmentTexture(64);

        // Average solid parchment as a coarse fill when a hole is huge.
        var avg = Color.black;
        foreach (var p in opaque)
            avg += pixels[p.y * w + p.x];
        avg /= opaque.Count;
        avg.a = 1f;

        var outPixels = new Color[pixels.Length];
        for (var y = 0; y < h; y++)
        {
            for (var x = 0; x < w; x++)
            {
                var i = y * w + x;
                var c = pixels[i];
                if (c.a >= solid)
                {
                    outPixels[i] = new Color(c.r, c.g, c.b, 1f);
                    continue;
                }

                // Local search for nearest solid pixel (small radius first).
                Color? found = null;
                for (var r = 1; r <= 12 && found == null; r++)
                {
                    for (var dy = -r; dy <= r && found == null; dy++)
                    {
                        for (var dx = -r; dx <= r; dx++)
                        {
                            if (Math.Abs(dx) != r && Math.Abs(dy) != r)
                                continue;
                            var nx = x + dx;
                            var ny = y + dy;
                            if (nx < 0 || ny < 0 || nx >= w || ny >= h)
                                continue;
                            var n = pixels[ny * w + nx];
                            if (n.a < solid)
                                continue;
                            found = new Color(n.r, n.g, n.b, 1f);
                            break;
                        }
                    }
                }

                outPixels[i] = found ?? avg;
            }
        }

        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
        {
            name = "Drakes_PaperMeshOpaque",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
        };
        tex.SetPixels(outPixels);
        tex.Apply(false, false);
        return tex;
    }

    private static Texture2D EnsureReadableTexture(Texture2D source)
    {
        try
        {
            _ = source.GetPixels(0, 0, 1, 1);
            return source;
        }
        catch
        {
            // Non-readable GPU texture.
        }

        var rt = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32);
        var prev = RenderTexture.active;
        Graphics.Blit(source, rt);
        RenderTexture.active = rt;
        var copy = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
        copy.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        copy.Apply(false, false);
        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(rt);
        return copy;
    }

    private static Material CreatePaperMaterial(Texture2D tex, Material? donorMat)
    {
        Material mat;
        if (donorMat?.shader != null)
        {
            mat = new Material(donorMat)
            {
                name = "Drakes_Paper2Mat",
            };
        }
        else
        {
            var shader = ResolveWorldShader();
            if (shader == null)
                throw new InvalidOperationException(
                    "No usable Valheim shader found for paper mesh (Shader.Find and donor both failed).");

            mat = new Material(shader)
            {
                name = "Drakes_Paper2Mat",
            };
        }

        mat.mainTexture = tex;
        if (mat.HasProperty("_MainTex"))
            mat.SetTexture("_MainTex", tex);

        ApplyMattePaperFinish(mat);

        // Clip transparent mesh texels — stops white fringe from soft AA edges.
        if (mat.HasProperty("_Cutoff"))
            mat.SetFloat("_Cutoff", 0.5f);
        mat.EnableKeyword("_ALPHATEST_ON");
        mat.DisableKeyword("_ALPHABLEND_ON");
        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        if (mat.HasProperty("_Cull"))
            mat.SetInt("_Cull", 0);

        return mat;
    }

    /// <summary>
    /// Paper should read as dry parchment, not polished wood/leather from the donor
    /// material clone. Clear gloss maps and force near-zero specular so torch light
    /// does not bloom the sheet into a white flare.
    /// </summary>
    private static void ApplyMattePaperFinish(Material mat)
    {
        if (mat == null)
            return;

        // Soft parchment — pure white albedo + Valheim bloom = blinding.
        var parchment = new Color(0.88f, 0.84f, 0.74f, 1f);
        mat.color = parchment;
        if (mat.HasProperty("_Color"))
            mat.SetColor("_Color", parchment);
        if (mat.HasProperty("_TintColor"))
            mat.SetColor("_TintColor", parchment);

        // Drop shine / metal maps inherited from leather, wood, creature skins, etc.
        ClearTex(mat, "_BumpMap");
        ClearTex(mat, "_MetallicGlossMap");
        ClearTex(mat, "_SpecGlossMap");
        ClearTex(mat, "_GlossMap");
        ClearTex(mat, "_OcclusionMap");
        ClearTex(mat, "_DetailMask");
        ClearTex(mat, "_DetailAlbedoMap");
        ClearTex(mat, "_DetailNormalMap");
        ClearTex(mat, "_EmissionMap");
        ClearTex(mat, "_ParallaxMap");

        if (mat.HasProperty("_EmissionColor"))
            mat.SetColor("_EmissionColor", Color.black);
        if (mat.HasProperty("_SpecColor"))
            mat.SetColor("_SpecColor", Color.black);

        SetFloatIf(mat, "_Glossiness", 0f);
        SetFloatIf(mat, "_Smoothness", 0.02f); // tiny residual — fully 0 can look plastic on some shaders
        SetFloatIf(mat, "_GlossMapScale", 0f);
        SetFloatIf(mat, "_Metallic", 0f);
        SetFloatIf(mat, "_Specular", 0f);
        SetFloatIf(mat, "_SpecularHighlights", 0f);
        SetFloatIf(mat, "_GlossyReflections", 0f);

        mat.DisableKeyword("_NORMALMAP");
        mat.DisableKeyword("_METALLICGLOSSMAP");
        mat.DisableKeyword("_SPECGLOSSMAP");
        mat.DisableKeyword("_EMISSION");
        mat.DisableKeyword("_DETAIL_MULX2");
        mat.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
        mat.EnableKeyword("_GLOSSYREFLECTIONS_OFF");
        mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
    }

    private static void ClearTex(Material mat, string property)
    {
        if (mat.HasProperty(property))
            mat.SetTexture(property, null);
    }

    private static void SetFloatIf(Material mat, string property, float value)
    {
        if (mat.HasProperty(property))
            mat.SetFloat(property, value);
    }

    private static Shader? ResolveWorldShader()
    {
        string[] candidates =
        {
            "Legacy Shaders/Transparent/Cutout/Diffuse",
            "Transparent/Cutout/Diffuse",
            "Legacy Shaders/Diffuse",
            "Diffuse",
            "Custom/Creature",
            "Standard",
            "Unlit/Texture",
        };

        foreach (var name in candidates)
        {
            var shader = Shader.Find(name);
            if (shader != null)
                return shader;
        }

        // Last resort: any loaded non-particle material shader already in memory.
        foreach (var mat in Resources.FindObjectsOfTypeAll<Material>())
        {
            if (IsUsableWorldMaterial(mat))
                return mat.shader;
        }

        return null;
    }
}


/// <summary>Per-instance visual offset so coplanar paper sheets do not z-fight.</summary>
internal sealed class PaperVisualDepthBias : MonoBehaviour
{
    bool _wall;
    bool _applied;

    internal void Configure(bool wallSheet)
    {
        // Prefab registration only stores the flag — never bake bias into the shared prefab.
        _wall = wallSheet;
        _applied = false;
    }

    void Start() => StartCoroutine(ApplyWhenZdoReady());

    System.Collections.IEnumerator ApplyWhenZdoReady()
    {
        // Wait until this is a real placed instance with a ZDO (skip shared prefabs).
        for (var i = 0; i < 60; i++)
        {
            if (_applied)
                yield break;

            var nv = GetComponent<ZNetView>();
            var zdo = nv != null && nv.IsValid() ? nv.GetZDO() : null;
            if (zdo != null)
            {
                ApplyBias(zdo.m_uid.ID);
                yield break;
            }

            yield return null;
        }
    }

    void ApplyBias(uint seed)
    {
        if (_applied)
            return;

        var attach = transform.Find("drakes_paper_decor");
        if (attach == null)
            return;

        // ~0.5mm .. 3.5mm — breaks depth fighting without obvious floating.
        float bias = 0.0002f + (seed % 21u) * 0.00005f;

        var lp = attach.localPosition;
        if (_wall)
            lp.z += bias;
        else
            lp.y += bias;
        attach.localPosition = lp;
        _applied = true;
    }
}
