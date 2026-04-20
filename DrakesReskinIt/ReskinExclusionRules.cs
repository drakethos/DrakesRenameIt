using System;
using System.Collections.Generic;
using System.Linq;

namespace DrakesReskinIt;

/// <summary>Parses exclusion / allowlist config and evaluates <see cref="ItemDrop.ItemData"/> against it.</summary>
internal static class ReskinExclusionRules
{
    public static bool MatchesReskinAllowlist(ItemDrop.ItemData? item)
    {
        if (item?.m_shared == null) return false;
        return SplitList(ReskinItConfig.ReskinAllowlist).Any(token => TokenMatchesItemName(item, token));
    }

    public static bool MatchesExcludedName(ItemDrop.ItemData? item)
    {
        if (item?.m_shared == null) return false;
        if (string.IsNullOrWhiteSpace(ReskinItConfig.ExcludedNames)) return false;
        return SplitList(ReskinItConfig.ExcludedNames).Any(token => TokenMatchesItemName(item, token));
    }

    public static bool MatchesExcludedCategory(ItemDrop.ItemData? item)
    {
        if (item?.m_shared == null) return false;
        if (string.IsNullOrWhiteSpace(ReskinItConfig.ExcludedCategory)) return false;
        foreach (var raw in ReskinItConfig.ExcludedCategory.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var token = raw.Trim();
            if (token.Length == 0) continue;
            if (MatchesCategoryToken(item, token)) return true;
        }
        return false;
    }

    public static bool IsExcludedFromConfig(ItemDrop.ItemData? item)
        => MatchesExcludedName(item) || MatchesExcludedCategory(item);

    // ─── Token matching (same logic as DrakeRenameIt) ─────────────────────────

    private static bool TokenMatchesItemName(ItemDrop.ItemData item, string token)
    {
        if (item.m_shared == null) return false;
        string internalName = item.m_shared.m_name;
        if (string.IsNullOrEmpty(internalName)) return false;
        if (internalName.Equals(token, StringComparison.OrdinalIgnoreCase)) return true;
        if (item.m_dropPrefab != null &&
            !string.IsNullOrEmpty(item.m_dropPrefab.name) &&
            item.m_dropPrefab.name.Equals(token, StringComparison.OrdinalIgnoreCase))
            return true;
        if (Localization.instance != null)
        {
            string localized = Localization.instance.Localize(internalName);
            if (!string.IsNullOrEmpty(localized) && localized.Equals(token, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private static bool MatchesCategoryToken(ItemDrop.ItemData item, string token)
    {
        if (item.m_shared == null) return false;
        if (TryAlias(item, token)) return true;
        if (Enum.TryParse(token, ignoreCase: true, out Skills.SkillType skill) &&
            Enum.IsDefined(typeof(Skills.SkillType), skill) &&
            item.m_shared.m_skillType == skill) return true;
        if (Enum.TryParse(token, ignoreCase: true, out ItemDrop.ItemData.ItemType itemType) &&
            Enum.IsDefined(typeof(ItemDrop.ItemData.ItemType), itemType) &&
            item.m_shared.m_itemType == itemType) return true;
        return false;
    }

    private static bool TryAlias(ItemDrop.ItemData item, string token)
    {
        if (item.m_shared == null) return false;
        return token.Trim().ToLowerInvariant() switch
        {
            "armor" => IsArmor(item),
            "weapons" => IsWeapon(item),
            "tools" => IsTools(item),
            "ranged" => IsRanged(item),
            "melee" => IsMelee(item),
            "shields" => item.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Shield,
            "ammo" => item.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Ammo,
            "fish" => item.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Fish,
            _ => false
        };
    }

    private static bool IsArmor(ItemDrop.ItemData item)
    {
        var t = item.m_shared!.m_itemType;
        return t is ItemDrop.ItemData.ItemType.Helmet
            or ItemDrop.ItemData.ItemType.Chest
            or ItemDrop.ItemData.ItemType.Legs
            or ItemDrop.ItemData.ItemType.Shoulder;
    }

    private static bool IsWeapon(ItemDrop.ItemData item)
    {
        var t = item.m_shared!.m_itemType;
        return t is ItemDrop.ItemData.ItemType.OneHandedWeapon
            or ItemDrop.ItemData.ItemType.TwoHandedWeapon
            or ItemDrop.ItemData.ItemType.TwoHandedWeaponLeft
            or ItemDrop.ItemData.ItemType.Bow;
    }

    private static bool IsRanged(ItemDrop.ItemData item)
    {
        var s = item.m_shared!;
        return s.m_itemType is ItemDrop.ItemData.ItemType.Bow or ItemDrop.ItemData.ItemType.Ammo
            || s.m_skillType is Skills.SkillType.Bows or Skills.SkillType.Crossbows;
    }

    private static bool IsMelee(ItemDrop.ItemData item)
    {
        var sk = item.m_shared!.m_skillType;
        return sk is Skills.SkillType.Swords or Skills.SkillType.Axes or Skills.SkillType.Clubs
            or Skills.SkillType.Polearms or Skills.SkillType.Spears or Skills.SkillType.Knives
            or Skills.SkillType.Unarmed or Skills.SkillType.Pickaxes;
    }

    private static bool IsTools(ItemDrop.ItemData item)
    {
        var sk = item.m_shared!.m_skillType;
        return sk is Skills.SkillType.Pickaxes or Skills.SkillType.WoodCutting;
    }

    private static IEnumerable<string> SplitList(string? csv)
    {
        if (string.IsNullOrWhiteSpace(csv)) yield break;
        foreach (var part in csv.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var t = part.Trim();
            if (t.Length > 0) yield return t;
        }
    }
}
