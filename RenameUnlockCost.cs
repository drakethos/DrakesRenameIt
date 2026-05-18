using System;
using System.Collections.Generic;
using System.Globalization;
using BepInEx.Logging;
using DrakeRenameit.ModText;
using UnityEngine;
using static DrakeRenameit.ModText.RenameItLocalization;

namespace DrakeRenameit;

/// <summary>Parses <see cref="RenameitConfig.UnlockCost"/>, checks affordability, and consumes cost for one-time stack unlock.</summary>
internal static class RenameUnlockCost
{
    private static ManualLogSource? _log;

    internal static void Init(ManualLogSource log) => _log = log;

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
        var inv = player.GetInventory();
        if (inv == null)
            return false;
        foreach (var (sharedName, amount, _) in lines)
        {
            if (inv.CountItems(sharedName) < amount)
                return false;
        }

        return true;
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

        var inv = player.GetInventory();
        if (inv == null)
        {
            errorMessage = T(LKeys.UnlockErrNoInventory);
            return false;
        }

        foreach (var (sharedName, amount, _) in lines)
        {
            if (inv.CountItems(sharedName) < amount)
            {
                errorMessage = T(LKeys.UnlockErrNotEnough);
                return false;
            }
        }

        foreach (var (sharedName, amount, _) in lines)
            inv.RemoveItem(sharedName, amount, -1, true);

        return true;
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
        lines = new List<(string SharedName, int Amount, string ConfigKey)>();
        error = null;
        var raw = RenameitConfig.UnlockCost?.Trim() ?? "";
        if (string.IsNullOrEmpty(raw))
        {
            error = "UnlockCost is empty.";
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
                _log?.LogWarning($"[UnlockCost] Ignoring invalid segment (need Name:Amount): \"{s}\"");
                continue;
            }

            var namePart = s.Substring(0, idx).Trim();
            var amtPart = s.Substring(idx + 1).Trim();
            if (!int.TryParse(amtPart, NumberStyles.Integer, CultureInfo.InvariantCulture, out var amount) ||
                amount <= 0)
            {
                _log?.LogWarning($"[UnlockCost] Ignoring invalid amount in: \"{s}\"");
                continue;
            }

            var resolved = ResolveItemSharedName(namePart);
            if (string.IsNullOrEmpty(resolved))
            {
                _log?.LogWarning($"[UnlockCost] Unknown item or token: \"{namePart}\"");
                continue;
            }

            lines.Add((resolved, amount, namePart));
        }

        if (lines.Count == 0)
        {
            error = "No valid UnlockCost entries (use Item prefab name or $item_ token, e.g. Coins:4).";
            return false;
        }

        return true;
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
