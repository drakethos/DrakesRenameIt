using System;
using System.IO;
using BepInEx.Logging;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DrakeRenameit;

/// <summary>
/// Loads forge-exported paper art (<c>Assets/Items/paper_*/art.bundle</c>, prefab name <c>art</c>).
/// Used as the visual base; donors (LeatherScraps / sign) still provide ItemDrop / Piece / WearNTear.
/// </summary>
internal static class PaperAssets
{
    internal const string ArtAssetName = "art";

    private static ManualLogSource? _log;
    private static string _pluginDir = "";
    private static AssetBundle? _pieceBundle;
    private static AssetBundle? _itemBundle;

    internal static void Init(ManualLogSource log, string pluginDirectory)
    {
        _log = log;
        _pluginDir = pluginDirectory ?? "";
    }

    /// <summary>
    /// Parent an <c>art</c> instance under <paramref name="parent"/>. Returns the visual root, or null to fall back.
    /// </summary>
    internal static GameObject? AttachForgeArt(Transform parent, bool forItem, bool written, Material? material = null)
    {
        if (parent == null)
            return null;

        var source = LoadArtPrefab(forItem);
        if (source == null)
            return null;

        var visual = Object.Instantiate(source, parent, false);
        visual.name = written ? "art_written" : "art";
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.identity;
        visual.transform.localScale = Vector3.one;

        // Strip any networked/piece leftovers from the exported mesh prefab.
        foreach (var nv in visual.GetComponentsInChildren<ZNetView>(true))
            Object.DestroyImmediate(nv);
        foreach (var wnt in visual.GetComponentsInChildren<WearNTear>(true))
            Object.DestroyImmediate(wnt);
        foreach (var piece in visual.GetComponentsInChildren<Piece>(true))
            Object.DestroyImmediate(piece);
        foreach (var drop in visual.GetComponentsInChildren<ItemDrop>(true))
            Object.DestroyImmediate(drop);
        foreach (var col in visual.GetComponentsInChildren<Collider>(true))
            Object.DestroyImmediate(col);

        if (material != null)
        {
            foreach (var renderer in visual.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer != null)
                    renderer.sharedMaterial = material;
            }
        }
        else
            ApplyDonorMaterials(visual, parent.gameObject);

        if (written)
            TryTintWritten(visual);

        FitToPaperSize(visual);
        return visual;
    }

    /// <summary>
    /// Uniform-scale forge mesh so its footprint matches <see cref="PaperItem.PaperSize"/> (US Letter).
    /// </summary>
    private static void FitToPaperSize(GameObject visual)
    {
        if (visual == null)
            return;

        var filters = visual.GetComponentsInChildren<MeshFilter>(true);
        if (filters == null || filters.Length == 0)
            return;

        var min = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
        var max = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
        var any = false;

        foreach (var filter in filters)
        {
            var mesh = filter != null ? filter.sharedMesh : null;
            if (mesh == null)
                continue;

            var b = mesh.bounds;
            var corners = new[]
            {
                new Vector3(b.min.x, b.min.y, b.min.z),
                new Vector3(b.min.x, b.min.y, b.max.z),
                new Vector3(b.min.x, b.max.y, b.min.z),
                new Vector3(b.min.x, b.max.y, b.max.z),
                new Vector3(b.max.x, b.min.y, b.min.z),
                new Vector3(b.max.x, b.min.y, b.max.z),
                new Vector3(b.max.x, b.max.y, b.min.z),
                new Vector3(b.max.x, b.max.y, b.max.z),
            };

            foreach (var corner in corners)
            {
                var world = filter.transform.TransformPoint(corner);
                var local = visual.transform.InverseTransformPoint(world);
                min = Vector3.Min(min, local);
                max = Vector3.Max(max, local);
                any = true;
            }
        }

        if (!any)
            return;

        var size = max - min;
        // Flat-authored mesh: width/height live on XZ (or XY if already upright). Use the two largest axes.
        float a = size.x;
        float b2 = size.y;
        float c2 = size.z;
        // Sort two largest as mesh width/height footprint
        float meshW;
        float meshH;
        if (a >= b2 && a >= c2)
        {
            meshW = a;
            meshH = b2 >= c2 ? b2 : c2;
        }
        else if (b2 >= a && b2 >= c2)
        {
            meshW = b2;
            meshH = a >= c2 ? a : c2;
        }
        else
        {
            meshW = c2;
            meshH = a >= b2 ? a : b2;
        }

        meshW = Mathf.Max(meshW, 0.0001f);
        meshH = Mathf.Max(meshH, 0.0001f);

        var target = PaperItem.PaperSize;
        // Uniform scale: fit letter rectangle (prefer matching long edge = 11").
        float scale = Mathf.Min(target.x / meshW, target.y / meshH);
        // If mesh aspect is portrait-swapped vs our (width,height), still fit inside letter bounds.
        float scaleAlt = Mathf.Min(target.x / meshH, target.y / meshW);
        // Pick the scale that lands closer to letter area without exceeding either edge much
        if (scaleAlt > scale && scaleAlt * meshW <= target.y * 1.05f && scaleAlt * meshH <= target.x * 1.05f)
            scale = scaleAlt;

        visual.transform.localScale = Vector3.one * scale;
        // Authored pivot is often on the table face — recenter so wall rotation does not float the sheet.
        var center = (min + max) * 0.5f;
        visual.transform.localPosition = -center * scale;
    }

    private static GameObject? LoadArtPrefab(bool forItem)
    {
        // paper_item and paper_piece currently ship identical bundles. Unity refuses to
        // LoadFromFile the same AssetBundle content twice — share one handle for both.
        _ = forItem;
        var bundle = EnsureArtBundle();
        if (bundle == null)
            return null;

        var direct = bundle.LoadAsset<GameObject>(ArtAssetName);
        if (direct != null)
            return direct;

        foreach (var name in bundle.GetAllAssetNames())
        {
            if (string.IsNullOrEmpty(name))
                continue;
            if (Path.GetFileNameWithoutExtension(name).Equals(ArtAssetName, StringComparison.OrdinalIgnoreCase))
                return bundle.LoadAsset<GameObject>(name);
        }

        _log?.LogError("[Paper] Forge art.bundle has no prefab named 'art'.");
        return null;
    }

    private static AssetBundle? EnsureArtBundle()
    {
        if (_itemBundle != null)
            return _itemBundle;
        if (_pieceBundle != null)
            return _pieceBundle;

        var itemPath = Path.Combine(_pluginDir, "Assets", "Items", "paper_item", "art.bundle");
        var piecePath = Path.Combine(_pluginDir, "Assets", "Items", "paper_piece", "art.bundle");
        var flatPath = Path.Combine(_pluginDir, "art.bundle");

        _itemBundle = LoadBundle(itemPath);
        if (_itemBundle != null)
        {
            _pieceBundle = _itemBundle;
            return _itemBundle;
        }

        _pieceBundle = LoadBundle(piecePath);
        if (_pieceBundle != null)
        {
            _itemBundle = _pieceBundle;
            return _pieceBundle;
        }

        var flat = LoadBundle(flatPath);
        if (flat != null)
        {
            _itemBundle = flat;
            _pieceBundle = flat;
            return flat;
        }

        _log?.LogWarning("[Paper] Forge art.bundle not found under Assets/Items/paper_* or plugin root.");
        return null;
    }

    private static AssetBundle? EnsurePieceBundle() => EnsureArtBundle();

    private static AssetBundle? EnsureItemBundle() => EnsureArtBundle();

    private static AssetBundle? LoadBundle(string path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
            return null;

        try
        {
            var bundle = AssetBundle.LoadFromFile(path);
            if (bundle == null)
                _log?.LogError($"[Paper] Failed to load forge bundle: {path} (identical bundle may already be loaded).");
            else
                _log?.LogInfo($"[Paper] Loaded forge art '{path}'.");
            return bundle;
        }
        catch (Exception ex)
        {
            _log?.LogError($"[Paper] Forge bundle load error: {ex.Message}");
            return null;
        }
    }

    private static void ApplyDonorMaterials(GameObject visual, GameObject donorRoot)
    {
        Material? mat = null;
        foreach (var renderer in donorRoot.GetComponentsInChildren<MeshRenderer>(true))
        {
            if (renderer == null || renderer.transform.IsChildOf(visual.transform))
                continue;
            if (renderer.sharedMaterial != null)
            {
                mat = renderer.sharedMaterial;
                break;
            }
        }

        if (mat == null)
            return;

        foreach (var renderer in visual.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer == null)
                continue;
            renderer.sharedMaterial = mat;
        }
    }

    private static void TryTintWritten(GameObject visual)
    {
        // Soft ink wash so written pages read differently without a second mesh export.
        foreach (var renderer in visual.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer == null || renderer.sharedMaterial == null)
                continue;
            var mat = new Material(renderer.sharedMaterial);
            // Keep matte finish after tint — cloning alone can revive donor gloss.
            if (mat.HasProperty("_Color"))
                mat.SetColor("_Color", new Color(0.86f, 0.82f, 0.72f, 1f));
            mat.color = new Color(0.86f, 0.82f, 0.72f, 1f);
            if (mat.HasProperty("_Glossiness"))
                mat.SetFloat("_Glossiness", 0f);
            if (mat.HasProperty("_Smoothness"))
                mat.SetFloat("_Smoothness", 0.02f);
            if (mat.HasProperty("_Metallic"))
                mat.SetFloat("_Metallic", 0f);
            if (mat.HasProperty("_SpecColor"))
                mat.SetColor("_SpecColor", Color.black);
            renderer.sharedMaterial = mat;
        }
    }

    /// <summary>
    /// Piece prefabs must keep a root ZNetView or world load retries ZDOs forever.
    /// </summary>
    internal static void EnsurePersistentZNetView(GameObject root)
    {
        if (root == null)
            return;

        if (!root.activeSelf)
            root.SetActive(true);

        var nested = root.GetComponentsInChildren<ZNetView>(true);
        ZNetView? rootNv = null;
        for (var i = 0; i < nested.Length; i++)
        {
            var nv = nested[i];
            if (nv == null)
                continue;
            if (nv.gameObject == root)
            {
                rootNv = nv;
                continue;
            }

            Object.DestroyImmediate(nv);
        }

        if (rootNv == null)
            rootNv = root.AddComponent<ZNetView>();

        rootNv.m_persistent = true;
        rootNv.m_distant = false;
        if (!rootNv.enabled)
            rootNv.enabled = true;
    }
}
