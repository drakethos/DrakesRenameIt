using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Logging;
using UnityEngine;

namespace DrakeRenameit;

/// <summary>Parses <see cref="RenameitConfig.UnlockCost"/>, checks player inventory affordability, and consumes the cost.</summary>
public static class RenameUnlockCost
{
    private static ManualLogSource? _log;

    public static void Init(ManualLogSource log)
    {
        _log = log;
    }

    /// <summary>True when the unlock cost feature is enabled and at least one valid cost entry is configured.</summary>
    public static bool UnlockCostApplies()
    {
        if (!RenameitConfig.UnlockCostEnabled)
            return false;
        var entries = ParseCostEntries();
        return entries.Count > 0;
    }

    /// <summary>True if the player has all required items in their inventory.</summary>
    public static bool CanPlayerAfford(Player? player)
    {
        if (player == null)
            return false;
        var entries = ParseCostEntries();
        if (entries.Count == 0)
            return true;

        var inv = player.GetInventory();
        if (inv == null)
            return false;

        foreach (var (prefab, amount) in entries)
        {
            string token = GetItemToken(prefab);
            int have = inv.CountItems(token);
            if (have < amount)
                return false;
        }

        return true;
    }

    /// <summary>Attempts to consume the unlock cost. Returns true on success; sets <paramref name="errorMessage"/> on failure.</summary>
    public static bool TryConsumeUnlockCost(Player? player, out string errorMessage)
    {
        errorMessage = "";
        if (player == null)
        {
            errorMessage = "No local player.";
            return false;
        }

        var entries = ParseCostEntries();
        if (entries.Count == 0)
            return true;

        var inv = player.GetInventory();
        if (inv == null)
        {
            errorMessage = "Cannot access inventory.";
            return false;
        }

        var missing = new List<string>();
        foreach (var (prefab, amount) in entries)
        {
            string token = GetItemToken(prefab);
            int have = inv.CountItems(token);
            if (have < amount)
                missing.Add($"{amount}x {GetLocalizedName(prefab)} (have {have})");
        }

        if (missing.Count > 0)
        {
            errorMessage = "Not enough items: " + string.Join(", ", missing);
            return false;
        }

        foreach (var (prefab, amount) in entries)
        {
            string token = GetItemToken(prefab);
            inv.RemoveItem(token, amount);
        }

        return true;
    }

    /// <summary>Short human-readable cost string, e.g. "4x Coins" or "4x Coins, 2x Coal". Uses localized item name when available.</summary>
    public static string GetCostDisplayShort()
    {
        var entries = ParseCostEntries();
        if (entries.Count == 0)
            return "";

        var parts = new List<string>();
        foreach (var (prefab, amount) in entries)
        {
            string displayName = GetLocalizedName(prefab);
            parts.Add($"{amount}x {displayName}");
        }

        return string.Join(", ", parts);
    }

    /// <summary>Returns per-item cost entries with their localized display names and amounts, for use in UI panels.</summary>
    public static List<(string LocalizedName, int Amount, string PrefabName)> GetCostDisplayEntries()
    {
        var entries = ParseCostEntries();
        return entries.Select(e => (GetLocalizedName(e.prefab), e.amount, e.prefab)).ToList();
    }

    private static string GetLocalizedName(string prefabName)
    {
        if (ObjectDB.instance != null)
        {
            var prefab = ObjectDB.instance.GetItemPrefab(prefabName);
            if (prefab != null)
            {
                var drop = prefab.GetComponent<ItemDrop>();
                if (drop?.m_itemData?.m_shared?.m_name != null)
                {
                    return Localization.instance != null
                        ? Localization.instance.Localize(drop.m_itemData.m_shared.m_name)
                        : drop.m_itemData.m_shared.m_name;
                }
            }
        }

        return prefabName;
    }

    /// <summary>Returns the <c>m_shared.m_name</c> token for a prefab name (e.g. "Coins" → "$item_coins"),
    /// for use with <c>Inventory.CountItems</c> and <c>Inventory.RemoveItem</c>.</summary>
    public static string GetItemTokenPublic(string prefabName) => GetItemToken(prefabName);

    private static string GetItemToken(string prefabName)
    {
        if (ObjectDB.instance != null)
        {
            var prefab = ObjectDB.instance.GetItemPrefab(prefabName);
            if (prefab != null)
            {
                var drop = prefab.GetComponent<ItemDrop>();
                if (!string.IsNullOrEmpty(drop?.m_itemData?.m_shared?.m_name))
                    return drop.m_itemData.m_shared.m_name;
            }
        }

        return prefabName;
    }

    private static List<(string prefab, int amount)> ParseCostEntries()
    {
        var result = new List<(string, int)>();
        string raw = RenameitConfig.UnlockCost ?? "";
        if (string.IsNullOrWhiteSpace(raw))
            return result;

        var tokens = raw.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var token in tokens)
        {
            var trimmed = token.Trim();
            if (string.IsNullOrEmpty(trimmed))
                continue;

            var parts = trimmed.Split(':');
            if (parts.Length != 2)
            {
                _log?.LogWarning($"[UnlockCost] Invalid entry '{trimmed}' — expected PrefabName:amount");
                continue;
            }

            string prefabName = parts[0].Trim();
            if (!int.TryParse(parts[1].Trim(), out int amount) || amount <= 0)
            {
                _log?.LogWarning($"[UnlockCost] Invalid amount in '{trimmed}'");
                continue;
            }

            if (string.IsNullOrEmpty(prefabName))
                continue;

            result.Add((prefabName, amount));
        }

        return result;
    }
}
