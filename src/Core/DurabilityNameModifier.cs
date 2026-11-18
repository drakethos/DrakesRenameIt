using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace DrakeRenameit;

/// <summary>Builds a durability prefix from <see cref="RenameitConfig"/> (unbroken / broken-at-zero / tier list).</summary>
internal static class DurabilityNameModifier
{
    private const float FullEpsilon = 0.0001f;

    private static readonly Regex TierRegex = new Regex(@"\{([^}]*)\}", RegexOptions.Compiled);

    /// <summary>True when this item should use a durability prefix (feature on, item uses equipment-style wear, label non-empty).</summary>
    internal static bool AffectsDisplay(ItemDrop.ItemData? item) =>
        !string.IsNullOrEmpty(GetPrefixRaw(item));

    /// <summary>Label only (may include &lt;color=…&gt; / &lt;#RRGGBB&gt; markup). Empty when not applicable.</summary>
    internal static string GetPrefixRaw(ItemDrop.ItemData? item)
    {
        if (!RenameitConfig.DurabilityModifierEnabled || item?.m_shared == null)
            return "";
        if (!TryGetDurabilityRatio(item, out var ratio))
            return "";

        var rules = BuildRules();
        var label = ResolveLabel(rules, item, ratio);
        return string.IsNullOrEmpty(label) ? "" : label.Trim();
    }

    /// <summary>Broken = 0 durability in-game; full = ~100%; tiers fill the middle; gap between top tier and full has no label.</summary>
    private static string ResolveLabel(DurabilityModifierRules rules, ItemDrop.ItemData item, float ratio)
    {
        if (item.m_durability <= 0f)
            return rules.BrokenLabel;

        if (ratio >= 1f - FullEpsilon)
            return rules.UnbrokenLabel;

        foreach (var tier in rules.Tiers)
        {
            if (ratio <= tier.Threshold + FullEpsilon)
                return tier.Label;
        }

        return "";
    }

    private static DurabilityModifierRules BuildRules()
    {
        var unbroken = (RenameitConfig.DurabilityUnbrokenLabel ?? "").Trim();
        var broken = (RenameitConfig.DurabilityBrokenLabel ?? "").Trim();
        var tiers = ParseTiers(RenameitConfig.DurabilityTierModifiers ?? "");
        return new DurabilityModifierRules(unbroken, broken, tiers);
    }

    private static List<DurabilityTier> ParseTiers(string raw)
    {
        var tiers = new List<DurabilityTier>();
        if (string.IsNullOrWhiteSpace(raw))
            return tiers;

        foreach (Match m in TierRegex.Matches(raw))
        {
            if (TryParseTierInner(m.Groups[1].Value, out var tier))
                tiers.Add(tier);
        }

        tiers.Sort((a, b) => a.Threshold.CompareTo(b.Threshold));
        return tiers;
    }

    /// <summary>
    /// True for forge-repair gear/tools: vanilla <c>m_useDurability</c>, not spoilage timers on food/seeds/etc.
    /// Excludes <see cref="ItemDrop.ItemData.ItemType"/> buckets that never use tier labels (Materials, Consumables, Fish…).
    /// </summary>
    private static bool UsesWearTierDurabilityItem(ItemDrop.ItemData item)
    {
        var s = item.m_shared;
        if (!s.m_useDurability || s.m_maxDurability <= 0f)
            return false;

        return s.m_itemType switch
        {
            ItemDrop.ItemData.ItemType.None => false,
            ItemDrop.ItemData.ItemType.Material => false,
            ItemDrop.ItemData.ItemType.Consumable => false,
            ItemDrop.ItemData.ItemType.Fish => false,
            ItemDrop.ItemData.ItemType.Misc => false,
            ItemDrop.ItemData.ItemType.Ammo => false,
            ItemDrop.ItemData.ItemType.Customization => false,
            ItemDrop.ItemData.ItemType.Trophy => false,
            _ => true,
        };
    }

    private static bool TryGetDurabilityRatio(ItemDrop.ItemData item, out float ratio)
    {
        ratio = 1f;
        if (!UsesWearTierDurabilityItem(item))
            return false;

        float max;
        try
        {
            max = item.GetMaxDurability();
        }
        catch
        {
            return false;
        }

        if (max <= 0f)
            return false;

        ratio = item.m_durability / max;
        if (ratio < 0f) ratio = 0f;
        if (ratio > 1f) ratio = 1f;
        return true;
    }

    private static bool TryParseTierInner(string inner, out DurabilityTier tier)
    {
        tier = default;
        inner = inner.Trim();
        if (inner.Length == 0)
            return false;

        var lastComma = inner.LastIndexOf(',');
        if (lastComma <= 0 || lastComma >= inner.Length - 1)
            return false;

        var labelPart = inner.Substring(0, lastComma).Trim();
        var numPart = inner.Substring(lastComma + 1).Trim();
        if (labelPart.Length == 0 || numPart.Length == 0)
            return false;

        if (!float.TryParse(numPart, NumberStyles.Float, CultureInfo.InvariantCulture, out var threshold))
            return false;

        if (threshold > 1f + FullEpsilon)
            threshold /= 100f;

        if (threshold < 0f) threshold = 0f;
        if (threshold > 1f) threshold = 1f;

        tier = new DurabilityTier(labelPart, threshold);
        return true;
    }

    private readonly struct DurabilityModifierRules
    {
        internal DurabilityModifierRules(string unbrokenLabel, string brokenLabel, List<DurabilityTier> tiers)
        {
            UnbrokenLabel = unbrokenLabel;
            BrokenLabel = brokenLabel;
            Tiers = tiers;
        }

        internal string UnbrokenLabel { get; }
        internal string BrokenLabel { get; }
        internal List<DurabilityTier> Tiers { get; }
    }

    private readonly struct DurabilityTier
    {
        internal DurabilityTier(string label, float threshold)
        {
            Label = label.Trim();
            Threshold = threshold;
        }

        internal string Label { get; }
        internal float Threshold { get; }
    }
}
