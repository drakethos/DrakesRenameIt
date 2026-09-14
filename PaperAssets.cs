using System;
using System.IO;
using BepInEx.Logging;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DrakeRenameit;

/// <summary>Loads the <c>paper</c> asset bundle shipped next to the plugin.</summary>
internal static class PaperAssets
{
    internal const string BundleFileName = "paper";
    internal const string BlankItem = "paper_blank_item";
    internal const string WrittenItem = "paper_written_item";
    internal const string BlankPiece = "paper_blank_piece";
    internal const string WrittenPiece = "paper_written_piece";
    internal const string BlankStackPiece = "stack_paper_blank_piece";

    private static ManualLogSource? _log;
    private static string _pluginDir = "";
    private static AssetBundle? _bundle;

    internal static bool IsLoaded => _bundle != null;

    internal static void Init(ManualLogSource log, string pluginDirectory)
    {
        _log = log;
        _pluginDir = pluginDirectory ?? "";
    }

    internal static bool TryEnsureLoaded()
    {
        if (_bundle != null)
            return true;

        var path = ResolveBundlePath();
        if (string.IsNullOrEmpty(path))
        {
            _log?.LogError("[Paper] Asset bundle 'paper' was not found under Assets/ next to the plugin.");
            return false;
        }

        try
        {
            _bundle = AssetBundle.LoadFromFile(path);
        }
        catch (Exception ex)
        {
            _log?.LogError($"[Paper] Failed to load asset bundle at '{path}': {ex.Message}");
            return false;
        }

        if (_bundle == null)
        {
            _log?.LogError($"[Paper] AssetBundle.LoadFromFile returned null for '{path}'.");
            return false;
        }

        _log?.LogInfo($"[Paper] Loaded asset bundle '{path}'.");
        return true;
    }

    /// <summary>Instantiate a bundle prefab under a stable registered name (inactive, persist across scenes).</summary>
    internal static GameObject? CreateInstance(string assetName, string registeredName) =>
        CreatePrefab(assetName, registeredName);

    internal static GameObject? CreatePrefab(string assetName, string registeredName)
    {
        if (!TryEnsureLoaded() || _bundle == null)
            return null;

        var source = LoadPrefabAsset(assetName);
        if (source == null)
        {
            _log?.LogError($"[Paper] Prefab '{assetName}' is missing from the paper bundle.");
            return null;
        }

        var go = Object.Instantiate(source);
        go.name = registeredName;
        go.SetActive(false);
        Object.DontDestroyOnLoad(go);
        return go;
    }

    private static GameObject? LoadPrefabAsset(string assetName)
    {
        if (_bundle == null)
            return null;

        var direct = _bundle.LoadAsset<GameObject>(assetName);
        if (direct != null)
            return direct;

        foreach (var name in _bundle.GetAllAssetNames())
        {
            if (string.IsNullOrEmpty(name))
                continue;
            var file = Path.GetFileNameWithoutExtension(name);
            if (file.Equals(assetName, StringComparison.OrdinalIgnoreCase))
                return _bundle.LoadAsset<GameObject>(name);
        }

        return null;
    }

    private static string ResolveBundlePath()
    {
        string[] candidates =
        {
            Path.Combine(_pluginDir, "Assets", BundleFileName),
            Path.Combine(_pluginDir, BundleFileName),
            Path.Combine(Directory.GetCurrentDirectory(), "Assets", BundleFileName),
        };

        foreach (var path in candidates)
        {
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
                return path;
        }

        return "";
    }
}
