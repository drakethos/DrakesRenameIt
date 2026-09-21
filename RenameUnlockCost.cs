using System;
using System.Collections.Generic;
using System.Globalization;
using DrakeRenameit.ModText;
using UnityEngine;
using static DrakeRenameit.ModText.RenameItLocalization;

namespace DrakeRenameit;

/// <summary>Parses <see cref="RenameitConfig.UnlockCost"/>, checks affordability, and consumes cost for one-time stack unlock.</summary>
internal static class RenameUnlockCost
{
    private static string? _cachedUnlockCostRaw;
    private static bool _cacheValid;
    private static bool _cacheOk;
    private static List<(string SharedName, int Amount, string ConfigKey)>? _cachedLines;
    private static string? _cachedError;

    internal static bool HasValidCostConfigured()
    {
        return TryBuildResolvedCost(out _, out _);
    }

    internal static bool UnlockCostApplies()
    {
        return RenameitConfig.UnlockCostEnabled && HasValidCostConfigured();
    }

    internal static bool CanPlayerAfford(Player? player)
    {
        if (player == null)
            return false;
        if (!TryBuildResolvedCost(out var lines, out _))
            return true;

        var payLines = new List<(string SharedName, int Amount)>(lines.Count);
        foreach (var (sharedName, amount, _) in lines)
            payLines.Add((sharedName, amount));
        return InventoryCost.CanAfford(player, payLines);
    }

    internal static bool TryConsumeUnlockCost(Player? player, out string errorMessage)
    {
        errorMessage = "";
        if (player == null)
        {
            errorMessage = T(LKeys.UnlockErrNoPlayer);
            return false;
        }

        if (!TryBuildResolvedCost(out var lines, out var parseError))
        {
            errorMessage = parseError ?? T(LKeys.UnlockErrNotConfigured);
            return false;
        }

        if (lines.Count == 0)
        {
            errorMessage = T(LKeys.UnlockErrEmpty);
            return false;
        }

        var payLines = new List<(string SharedName, int Amount)>(lines.Count);
        foreach (var (sharedName, amount, _) in lines)
            payLines.Add((sharedName, amount));
        return InventoryCost.TryConsume(player, payLines, out errorMessage, T(LKeys.UnlockErrNotEnough));
    }

    internal static string GetCostDisplayShort()
    {
        if (!TryBuildResolvedCost(out var lines, out _) || lines.Count == 0)
            return "";
        var parts = new List<string>();
        foreach (var (sharedName, amount, _) in lines)
        {
            string label = sharedName;
            if (Localization.instance != null)
                label = Localization.instance.Localize(sharedName);
            parts.Add($"{amount}x {label}");
        }

        return string.Join(", ", parts);
    }

    /// <summary>Lines for the unlock confirmation panel: localized name, amount, and config prefab key (for <see cref="GetItemTokenPublic"/>).</summary>
    internal static List<(string LocalizedName, int Amount, string PrefabName)> GetCostDisplayEntries()
    {
        var list = new List<(string LocalizedName, int Amount, string PrefabName)>();
        if (!TryBuildResolvedCost(out var lines, out _) || lines.Count == 0)
            return list;

        foreach (var (sharedName, amount, configKey) in lines)
        {
            string loc = sharedName;
            if (Localization.instance != null)
                loc = Localization.instance.Localize(sharedName);
            list.Add((loc, amount, configKey));
        }

        return list;
    }

    /// <summary>Resolves a config prefab name or <c>$item_</c> token to <c>m_shared.m_name</c> for inventory ops.</summary>
    internal static string GetItemTokenPublic(string prefabName) => ResolveItemSharedName(prefabName);

    /// <summary>Icon for a cost line (prefab spawn name from config, e.g. Coins).</summary>
    internal static Sprite? GetItemIconSprite(string configPrefabName)
    {
        if (string.IsNullOrWhiteSpace(configPrefabName) || ObjectDB.instance == null)
            return null;
        var go = ObjectDB.instance.GetItemPrefab(configPrefabName);
        if (go == null)
            return null;
        var drop = go.GetComponent<ItemDrop>();
        return drop?.m_itemData?.GetIcon();
    }

    private static bool TryBuildResolvedCost(
        out List<(string SharedName, int Amount, string ConfigKey)> lines,
        out string? error)
    {
        var raw = RenameitConfig.UnlockCost?.Trim() ?? "";
        if (_cacheValid && string.Equals(_cachedUnlockCostRaw, raw, StringComparison.Ordinal))
        {
            lines = _cachedLines ?? new List<(string SharedName, int Amount, string ConfigKey)>();
            error = _cachedError;
            return _cacheOk;
        }

        lines = new List<(string SharedName, int Amount, string ConfigKey)>();
        error = null;

        if (string.IsNullOrEmpty(raw))
        {
            error = "UnlockCost is empty.";
            StoreParseCache(raw, false, lines, error);
            return false;
        }

        var segments = raw.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var seg in segments)
        {
            var s = seg.Trim();
            if (string.IsNullOrEmpty(s))
                continue;
            var idx = s.LastIndexOf(':');
            if (idx <= 0 || idx >= s.Length - 1)
            {
                RenameitConfig.VerboseWarning($"[UnlockCost] Ignoring invalid segment (need Name:Amount): \"{s}\"");
                continue;
            }

            var namePart = s.Substring(0, idx).Trim();
            var amtPart = s.Substring(idx + 1).Trim();
            if (!int.TryParse(amtPart, NumberStyles.Integer, CultureInfo.InvariantCulture, out var amount) ||
                amount <= 0)
            {
                RenameitConfig.VerboseWarning($"[UnlockCost] Ignoring invalid amount in: \"{s}\"");
                continue;
            }

            var resolved = ResolveItemSharedName(namePart);
            if (string.IsNullOrEmpty(resolved))
            {
                RenameitConfig.VerboseWarning($"[UnlockCost] Unknown item or token: \"{namePart}\"");
                continue;
            }

            lines.Add((resolved, amount, namePart));
        }

        if (lines.Count == 0)
        {
            error = "No valid UnlockCost entries (use Item prefab name or $item_ token, e.g. Coins:4).";
            StoreParseCache(raw, false, lines, error);
            return false;
        }

        StoreParseCache(raw, true, lines, null);
        return true;
    }

    static void StoreParseCache(
        string raw,
        bool ok,
        List<(string SharedName, int Amount, string ConfigKey)> lines,
        string? error)
    {
        _cachedUnlockCostRaw = raw;
        _cacheValid = true;
        _cacheOk = ok;
        _cachedLines = lines;
        _cachedError = error;
    }

    private static string ResolveItemSharedName(string tokenOrPrefab)
    {
        if (string.IsNullOrWhiteSpace(tokenOrPrefab))
            return "";

        if (ObjectDB.instance == null)
            return tokenOrPrefab.StartsWith("$", StringComparison.Ordinal) ? tokenOrPrefab : "";

        var prefab = ObjectDB.instance.GetItemPrefab(tokenOrPrefab);
        if (prefab != null)
        {
            var drop = prefab.GetComponent<ItemDrop>();
            var sn = drop?.m_itemData?.m_shared?.m_name ?? "";
            if (!string.IsNullOrEmpty(sn))
                return sn;
        }

        if (tokenOrPrefab.StartsWith("$", StringComparison.Ordinal))
            return tokenOrPrefab;

        return "";
    }
}
