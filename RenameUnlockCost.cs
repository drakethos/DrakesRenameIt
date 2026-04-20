using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using BepInEx.Logging;
using UnityEngine;

namespace DrakeRenameit;

/// <summary>Parses <see cref="RenameitConfig.UnlockCost"/> and removes items from the player inventory for one-time rename unlock.</summary>
internal static class RenameUnlockCost
{
    private static ManualLogSource? _log;

    internal static void Init(ManualLogSource log) => _log = log;

    /// <summary>Unlock cost is on, the string parses, and at least one positive cost line resolves in <see cref="ObjectDB"/>.</summary>
    internal static bool HasValidCostConfigured()
    {
        return TryBuildResolvedCost(out _, out _);
    }

    /// <summary>True when <see cref="RenameitConfig.UnlockCostEnabled"/> is on and <see cref="HasValidCostConfigured"/>.</summary>
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
        foreach (var (sharedName, amount) in lines)
        {
            if (inv.CountItems(sharedName) < amount)
                return false;
        }

        return true;
    }

    /// <summary>Removes cost from inventory. Fails with a player-facing message if something is missing.</summary>
    internal static bool TryConsumeUnlockCost(Player player, out string errorMessage)
    {
        errorMessage = "";
        if (!TryBuildResolvedCost(out var lines, out var parseError))
        {
            errorMessage = parseError ?? "Unlock cost is not configured.";
            return false;
        }

        if (lines.Count == 0)
        {
            errorMessage = "Unlock cost is empty.";
            return false;
        }

        var inv = player.GetInventory();
        if (inv == null)
        {
            errorMessage = "No inventory.";
            return false;
        }

        foreach (var (sharedName, amount) in lines)
        {
            if (inv.CountItems(sharedName) < amount)
            {
                errorMessage = "Not enough items to unlock (see tooltip / config).";
                return false;
            }
        }

        foreach (var (sharedName, amount) in lines)
            inv.RemoveItem(sharedName, amount, -1, true);

        return true;
    }

    /// <summary>Short text for the Unlock button, e.g. "4 Coins, 2 Coal".</summary>
    internal static string GetCostDisplayShort()
    {
        if (!TryBuildResolvedCost(out var lines, out _) || lines.Count == 0)
            return "";
        var parts = new List<string>();
        foreach (var (sharedName, amount) in lines)
        {
            string label = sharedName;
            if (Localization.instance != null)
                label = Localization.instance.Localize(sharedName);
            parts.Add($"{amount} {label}");
        }

        return string.Join(", ", parts);
    }

    private static bool TryBuildResolvedCost(
        out List<(string SharedName, int Amount)> lines,
        out string? error)
    {
        lines = new List<(string SharedName, int Amount)>();
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

            lines.Add((resolved, amount));
        }

        if (lines.Count == 0)
        {
            error = "No valid UnlockCost entries (use Item prefab name or $item_ token, e.g. Coins:4).";
            return false;
        }

        return true;
    }

    /// <summary>Prefab spawn name (e.g. Coins) or localization token (e.g. $item_coins).</summary>
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
            var sn = drop?.m_itemData?.m_shared?.m_name;
            if (!string.IsNullOrEmpty(sn))
                return sn;
        }

        // Assume already a shared m_name token
        if (tokenOrPrefab.StartsWith("$", StringComparison.Ordinal))
            return tokenOrPrefab;

        return "";
    }
}
