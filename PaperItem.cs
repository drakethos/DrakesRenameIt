using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using BepInEx.Logging;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using Jotunn.Utils;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DrakeRenameit;

/// <summary>
/// Blank Piece of Paper (stackable) + Written Page (stack 1), loaded from the paper asset bundle.
/// Placement variants reuse those meshes; vertical sheets are rotated in code.
/// </summary>
internal static class PaperItem
{
    /// <summary>Blank paper — legacy prefab name kept for existing worlds/recipes.</summary>
    internal const string PrefabName = "Drakes_PieceOfPaper";
    internal const string WrittenPrefabName = "Drakes_Paper_Written";
    private const string TokenName = "$item_drakes_pieceofpaper";
    private const string TokenDesc = "$item_drakes_pieceofpaper_desc";
    private const string TokenWrittenName = "$item_drakes_paper_written";
    private const string TokenWrittenDesc = "$item_drakes_paper_written_desc";

    /// <summary>
    /// Flat sheets float this far above the placement hit so plank gaps / thick tabletops
    /// do not swallow the parchment (blank laying + written flat + stack).
    /// </summary>
    private const float FlatSurfaceLift = 0.04f;

    /// <summary>
    /// Vertical sheets sit this far off the aimed wall face (toward the player).
    /// Placement hits often land in the wall volume; without this the sheet sinks into the planks.
    /// </summary>
    internal const float WallFaceGap = 0.05f;

    private static ManualLogSource? _log;
    private static string _pluginDir = "";
    private static Sprite? _blankIconSprite;
    private static Sprite? _writtenIconSprite;

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
            if (!PaperAssets.TryEnsureLoaded())
                return;

            AddLocalization();
            RegisterBlank();
            RegisterWritten();
        }
        catch (Exception ex)
        {
            _log?.LogError($"[Paper] Failed to register paper items: {ex}");
        }
    }

    private static void RegisterBlank()
    {
        var go = PaperAssets.CreatePrefab(PaperAssets.BlankItem, PrefabName);
        if (go == null)
            return;

        SanitizeLoadedItem(go);

        var itemConfig = new ItemConfig
        {
            Name = TokenName,
            Description = TokenDesc,
            Enabled = RenameitConfig.PaperEnabled,
            Amount = 1,
        };
        var icon = ReadPrefabIcon(go);
        if (icon != null)
            itemConfig.Icon = icon;

        ApplyRecipeFromConfig(itemConfig);

        var paper = new CustomItem(go, false, itemConfig);
        ItemManager.Instance.AddItem(paper);

        SanitizeItemDrop(paper, stackable: true);
        CacheIcon(ref _blankIconSprite, paper, icon);

        if (paper.ItemDrop?.m_itemData != null)
            paper.ItemDrop.m_itemData.m_dropPrefab = paper.ItemPrefab;

        _log?.LogInfo(
            $"[Paper] Registered blank {PrefabName} from bundle (stack={RenameitConfig.BlankPaperStackSize}, " +
            $"enabled={RenameitConfig.PaperEnabled}, cost='{RenameitConfig.PaperCost}').");
    }

    private static void RegisterWritten()
    {
        var go = PaperAssets.CreatePrefab(PaperAssets.WrittenItem, WrittenPrefabName);
        if (go == null)
            return;

        SanitizeLoadedItem(go);

        var itemConfig = new ItemConfig
        {
            Name = TokenWrittenName,
            Description = TokenWrittenDesc,
            Enabled = RenameitConfig.PaperEnabled,
            Amount = 1,
        };
        var icon = ReadPrefabIcon(go);
        if (icon != null)
            itemConfig.Icon = icon;

        var paper = new CustomItem(go, false, itemConfig);
        ItemManager.Instance.AddItem(paper);

        SanitizeItemDrop(paper, stackable: false);
        CacheIcon(ref _writtenIconSprite, paper, icon);

        if (paper.ItemDrop?.m_itemData != null)
            paper.ItemDrop.m_itemData.m_dropPrefab = paper.ItemPrefab;

        _log?.LogInfo($"[Paper] Registered written {WrittenPrefabName} from bundle (stack=1).");
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

        var loc = LocalizationManager.Instance.GetLocalization();
        loc.AddTranslation("English", "item_drakes_pieceofpaper", name);
        loc.AddTranslation("English", "item_drakes_pieceofpaper_desc", desc);
        loc.AddTranslation("Spanish", "item_drakes_pieceofpaper", name);
        loc.AddTranslation("Spanish", "item_drakes_pieceofpaper_desc", desc);
        loc.AddTranslation("English", "item_drakes_paper_written", wName);
        loc.AddTranslation("English", "item_drakes_paper_written_desc", wDesc);
        loc.AddTranslation("Spanish", "item_drakes_paper_written", wName);
        loc.AddTranslation("Spanish", "item_drakes_paper_written_desc", wDesc);
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

    private static void SanitizeItemDrop(CustomItem paper, bool stackable)
    {
        var drop = paper.ItemDrop;
        if (drop?.m_itemData?.m_shared == null)
            return;

        var shared = drop.m_itemData.m_shared;
        shared.m_name = stackable ? TokenName : TokenWrittenName;
        shared.m_description = stackable ? TokenDesc : TokenWrittenDesc;
        shared.m_maxStackSize = stackable ? RenameitConfig.BlankPaperStackSize : 1;
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
        drop.m_itemData.m_stack = 1;
        drop.m_autoPickup = true;
        drop.m_autoDestroy = true;
    }

    /// <summary>
    /// Blank paper is hammer décor only. Never leave a piece table or tool attack on it
    /// (that lets Use enter place-mode and play the unarmed punch).
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

    internal static bool IsBlankPaper(ItemDrop.ItemData? item)
    {
        if (item?.m_shared == null)
            return false;
        if (PrefabsMatch(item, PrefabName) || PrefabsMatch(item, PaperAssets.BlankItem))
            return true;
        return item.m_shared.m_name.Equals(TokenName, StringComparison.OrdinalIgnoreCase);
    }

    internal static bool IsWrittenPaper(ItemDrop.ItemData? item)
    {
        if (item?.m_shared == null)
            return false;
        if (PrefabsMatch(item, WrittenPrefabName) || PrefabsMatch(item, PaperAssets.WrittenItem))
            return true;
        return item.m_shared.m_name.Equals(TokenWrittenName, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Blank or Written.</summary>
    internal static bool IsAnyPaper(ItemDrop.ItemData? item) => IsBlankPaper(item) || IsWrittenPaper(item);

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

    /// <summary>Hammer / UI icon for blank parchment (cached).</summary>
    internal static Sprite? GetBlankIconSprite()
    {
        if (_blankIconSprite != null)
            return _blankIconSprite;
        _blankIconSprite = ReadRegisteredIcon(PrefabName) ?? LoadPngSprite("paper.png");
        return _blankIconSprite;
    }

    /// <summary>Piece-table / UI icon for Written Page (cached).</summary>
    internal static Sprite? GetWrittenIconSprite()
    {
        if (_writtenIconSprite != null)
            return _writtenIconSprite;
        _writtenIconSprite = ReadRegisteredIcon(WrittenPrefabName) ??
                             LoadPngSprite("paper_written.png") ??
                             GetBlankIconSprite();
        return _writtenIconSprite;
    }

    /// <summary>
    /// Keep the authored mesh/snaps and fill Piece fields. <paramref name="wall"/> rotates the sheet upright.
    /// </summary>
    internal static void PrepareSheetPiece(GameObject? root, bool wall)
    {
        if (root == null)
            return;

        try
        {
            StripItemLeftoversFromPiece(root);
            ApplySheetOrientation(root, wall);
            PromoteSnapPointsToRoot(root);
            ConfigurePaperPlacement(root, wall);
            EnsurePieceLayer(root);
            SanitizePaperWearNTear(root);
            _log?.LogInfo($"[Paper] Prepared sheet '{root.name}' wall={wall} from bundle mesh.");
        }
        catch (Exception ex)
        {
            _log?.LogWarning($"[Paper] Prepare sheet failed: {ex.Message}");
        }
    }

    /// <summary>Keep the authored paper-stack mesh; only fill placement / snap / WearNTear fields.</summary>
    internal static void PrepareStackPiece(GameObject? root)
    {
        if (root == null)
            return;

        try
        {
            StripItemLeftoversFromPiece(root);
            PromoteSnapPointsToRoot(root);
            ConfigurePaperPlacement(root, wall: false);
            EnsurePieceLayer(root);
            SanitizePaperWearNTear(root);
            _log?.LogInfo($"[Paper] Prepared stack '{root.name}' from bundle mesh.");
        }
        catch (Exception ex)
        {
            _log?.LogWarning($"[Paper] Prepare stack failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Bundle sheets are authored flat. Vertical variants rotate <c>attach</c> to face +Z.
    /// </summary>
    internal static void ApplySheetOrientation(GameObject root, bool wall)
    {
        var attach = FindAttach(root.transform);
        if (attach == null)
        {
            _log?.LogWarning($"[Paper] No 'attach' child on '{root.name}'; cannot rotate sheet.");
            return;
        }

        if (wall)
        {
            attach.localRotation = Quaternion.identity;
            attach.localPosition = Vector3.zero;
        }
        else
        {
            attach.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var pos = attach.localPosition;
            attach.localPosition = new Vector3(pos.x, FlatSurfaceLift, pos.z);
        }
    }

    internal static bool IsWallSheetPiece(Piece? piece)
    {
        if (piece == null)
            return false;
        var n = piece.gameObject.name;
        return n.StartsWith(PaperPlace.BlankUpright, StringComparison.Ordinal)
               || n.StartsWith(PaperWrittenPlace.NoteVertical, StringComparison.Ordinal);
    }

    /// <summary>
    /// Move a vertical sheet onto the near face of the aimed wall (toward the camera).
    /// Idempotent: calling again on an already-seated point keeps it there.
    /// Keeps the point's position in the wall plane so snaps are not undone — only depth changes.
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
        piece.m_canRotate = true;
        piece.m_allowRotatedOverlap = true;

        if (wall)
        {
            piece.m_clipGround = true;
            piece.m_clipEverything = true;
            piece.m_allowAltGroundPlacement = true;
        }
        else
        {
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

    /// <summary>Valheim only recognizes snappoints as direct children of the piece root.</summary>
    internal static void PromoteSnapPointsToRoot(GameObject root)
    {
        if (root == null)
            return;

        var snaps = new List<Transform>();
        CollectSnapPoints(root.transform, snaps);
        foreach (var snap in snaps)
        {
            if (snap == null || snap.parent == root.transform)
                continue;
            snap.SetParent(root.transform, true);
        }
    }

    /// <summary>Kill leftover item physics / loot so breaks don't spray logs or drop the donor item.</summary>
    internal static void SanitizePaperWearNTear(GameObject? root)
    {
        if (root == null)
            return;

        var wnt = root.GetComponent<WearNTear>();
        if (wnt == null)
            return;

        wnt.m_autoCreateFragments = false;
        wnt.m_fragmentRoots = Array.Empty<GameObject>();
        wnt.m_destroyedEffect = new EffectList();
        wnt.m_hitEffect = new EffectList();
        wnt.m_noSupportWear = true;
        wnt.m_noRoofWear = true;

        for (var i = root.transform.childCount - 1; i >= 0; i--)
        {
            var child = root.transform.GetChild(i);
            var n = child.name;
            if (n.IndexOf("fragment", StringComparison.OrdinalIgnoreCase) >= 0 ||
                n.IndexOf("debris", StringComparison.OrdinalIgnoreCase) >= 0)
                Object.DestroyImmediate(child.gameObject);
        }
    }

    private static void SanitizeLoadedItem(GameObject go)
    {
        foreach (var piece in go.GetComponentsInChildren<Piece>(true))
            Object.DestroyImmediate(piece);
        foreach (var wnt in go.GetComponentsInChildren<WearNTear>(true))
            Object.DestroyImmediate(wnt);

        foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(true))
        {
            if (ps == null)
                continue;
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ps.gameObject.SetActive(false);
        }

        foreach (var renderer in go.GetComponentsInChildren<ParticleSystemRenderer>(true))
        {
            if (renderer != null)
                renderer.enabled = false;
        }

        var znet = go.GetComponent<ZNetView>();
        if (znet != null)
            znet.m_syncInitialScale = true;
    }

    private static void StripItemLeftoversFromPiece(GameObject root)
    {
        foreach (var drop in root.GetComponentsInChildren<ItemDrop>(true))
            Object.DestroyImmediate(drop);
        foreach (var body in root.GetComponentsInChildren<Rigidbody>(true))
            Object.DestroyImmediate(body);
        foreach (var sync in root.GetComponentsInChildren<ZSyncTransform>(true))
            Object.DestroyImmediate(sync);
        foreach (var lod in root.GetComponentsInChildren<LODGroup>(true))
            Object.DestroyImmediate(lod);
        foreach (var ps in root.GetComponentsInChildren<ParticleSystem>(true))
            Object.DestroyImmediate(ps);
        foreach (var psr in root.GetComponentsInChildren<ParticleSystemRenderer>(true))
            Object.DestroyImmediate(psr);
    }

    private static Transform? FindAttach(Transform root)
    {
        var named = root.Find("attach");
        if (named != null)
            return named;

        for (var i = 0; i < root.childCount; i++)
        {
            var child = root.GetChild(i);
            if (child.name.StartsWith("attach", StringComparison.OrdinalIgnoreCase))
                return child;
        }

        return null;
    }

    private static void CollectSnapPoints(Transform current, List<Transform> dest)
    {
        for (var i = 0; i < current.childCount; i++)
        {
            var child = current.GetChild(i);
            if (IsSnapPoint(child))
                dest.Add(child);
            CollectSnapPoints(child, dest);
        }
    }

    private static bool IsSnapPoint(Transform child)
    {
        if (child.name.IndexOf("snap", StringComparison.OrdinalIgnoreCase) >= 0)
            return true;
        try
        {
            return child.CompareTag("snappoint");
        }
        catch
        {
            return false;
        }
    }

    private static void CacheIcon(ref Sprite? cache, CustomItem paper, Sprite? fallback)
    {
        cache = ReadPrefabIcon(paper.ItemPrefab) ?? fallback ?? cache;
    }

    private static Sprite? ReadRegisteredIcon(string prefabName)
    {
        var go = PrefabManager.Instance.GetPrefab(prefabName)
                 ?? ObjectDB.instance?.GetItemPrefab(prefabName);
        return ReadPrefabIcon(go);
    }

    private static Sprite? ReadPrefabIcon(GameObject? go)
    {
        var icons = go?.GetComponent<ItemDrop>()?.m_itemData?.m_shared?.m_icons;
        if (icons == null || icons.Length == 0)
            return null;
        return icons[0];
    }

    private static Sprite? LoadPngSprite(string fileName)
    {
        var path = Path.Combine(_pluginDir, "Assets", "Icons", fileName);
        if (!File.Exists(path))
            return null;

        try
        {
            var tex = AssetUtils.LoadTexture(path, relativePath: false);
            if (tex == null)
                return null;
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
        }
        catch (Exception ex)
        {
            _log?.LogWarning($"[Paper] Failed to load fallback icon '{fileName}': {ex.Message}");
            return null;
        }
    }
}
