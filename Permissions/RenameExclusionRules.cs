using System;
using System.Collections.Generic;
using System.Linq;

namespace DrakeRenameit.Permissions;

/// <summary>Parses exclusion / allowlist config and evaluates <see cref="ItemDrop.ItemData"/> against it.</summary>
internal static class RenameExclusionRules
{
    /// <summary>
    /// Allow rename for this item even if it matches ExcludedNames / ExcludedCategory or would be blocked by AllowRenameResources (uncrafted).
    /// Does not bypass RenameEnabled, LockToOwner rules, or ownership (non-admins still need to own the item when ownership applies).
    /// </summary>
    public static bool MatchesRenameAllowlist(ItemDrop.ItemData? item)
    {
        if (item?.m_shared == null)
            return false;
        return SplitList(RenameitConfig.RenameAllowlist)
            .Any(token => TokenMatchesItemName(item, token));
    }

    public static bool MatchesExcludedName(ItemDrop.ItemData? item)
    {
        if (item?.m_shared == null)
            return false;
        if (string.IsNullOrWhiteSpace(RenameitConfig.ExcludedNames))
            return false;
        return SplitList(RenameitConfig.ExcludedNames)
            .Any(token => TokenMatchesItemName(item, token));
    }

    /// <summary>
    /// Matches Jotunn item-list <b>Token</b> column (<c>m_shared.m_name</c>, e.g. <c>$item_axe_stone</c>),
    /// <b>Item</b> column (spawn / prefab name, e.g. <c>AxeStone</c> via <c>m_dropPrefab.name</c>), or the localized English display name.
    /// See https://valheim-modding.github.io/Jotunn/data/objects/item-list.html
    /// </summary>
    private static bool TokenMatchesItemName(ItemDrop.ItemData item, string token)
    {
        if (item.m_shared == null)
            return false;
        string internalName = item.m_shared.m_name;
        if (string.IsNullOrEmpty(internalName))
            return false;
        if (internalName.Equals(token, StringComparison.OrdinalIgnoreCase))
            return true;
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

    public static bool MatchesExcludedCategory(ItemDrop.ItemData? item)
    {
        if (item?.m_shared == null)
            return false;
        if (string.IsNullOrWhiteSpace(RenameitConfig.ExcludedCategory))
            return false;

        foreach (var raw in RenameitConfig.ExcludedCategory.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var token = raw.Trim();
            if (token.Length == 0)
                continue;
            if (MatchesCategoryToken(item, token))
                return true;
        }

        return false;
    }

    /// <summary>True when <see cref="MatchesExcludedName"/>, <see cref="MatchesExcludedCategory"/>, or (when <see cref="RenameitConfig.ExcludeStackable"/>) the item is stackable. Allowlist is not applied here.</summary>
    public static bool IsExcludedFromConfig(ItemDrop.ItemData? item)
    {
        return MatchesExcludedName(item) || MatchesExcludedCategory(item) || MatchesExcludeStackable(item);
    }

    /// <summary>When <see cref="RenameitConfig.ExcludeStackable"/> is on: items with <c>m_maxStackSize &gt; 1</c> (vanilla stackable).</summary>
    public static bool MatchesExcludeStackable(ItemDrop.ItemData? item)
    {
        if (!RenameitConfig.ExcludeStackable)
            return false;
        if (item?.m_shared == null)
            return false;
        return item.m_shared.m_maxStackSize > 1;
    }

    private static IEnumerable<string> SplitList(string? csv)
    {
        if (string.IsNullOrWhiteSpace(csv))
            yield break;
        foreach (var part in csv.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var t = part.Trim();
            if (t.Length > 0)
                yield return t;
        }
    }

    private static bool MatchesCategoryToken(ItemDrop.ItemData item, string token)
    {
        if (item.m_shared == null)
            return false;

        if (TryAlias(item, token))
            return true;

        if (Enum.TryParse(token, ignoreCase: true, out Skills.SkillType skill) &&
            Enum.IsDefined(typeof(Skills.SkillType), skill) &&
            item.m_shared.m_skillType == skill)
            return true;

        if (Enum.TryParse(token, ignoreCase: true, out ItemDrop.ItemData.ItemType itemType) &&
            Enum.IsDefined(typeof(ItemDrop.ItemData.ItemType), itemType) &&
            item.m_shared.m_itemType == itemType)
            return true;

        return false;
    }

    private static bool TryAlias(ItemDrop.ItemData item, string token)
    {
        if (item.m_shared == null)
            return false;

        switch (token.Trim().ToLowerInvariant())
        {
            case "armor":
                return IsArmor(item);
            case "weapons":
                return IsWeapon(item);
            case "tools":
                return IsTools(item);
            case "ranged":
                return IsRanged(item);
            case "melee":
                return IsMelee(item);
            case "shields":
                return item.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Shield;
            case "ammo":
                return item.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Ammo;
            case "fish":
                return item.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Fish;
            default:
                return false;
        }
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
        return s.m_itemType is ItemDrop.ItemData.ItemType.Bow
            or ItemDrop.ItemData.ItemType.Ammo
            || s.m_skillType is Skills.SkillType.Bows or Skills.SkillType.Crossbows;
    }

    private static bool IsMelee(ItemDrop.ItemData item)
    {
        var sk = item.m_shared!.m_skillType;
        return sk is Skills.SkillType.Swords
            or Skills.SkillType.Axes
            or Skills.SkillType.Clubs
            or Skills.SkillType.Polearms
            or Skills.SkillType.Spears
            or Skills.SkillType.Knives
            or Skills.SkillType.Unarmed
            or Skills.SkillType.Pickaxes;
    }

    private static bool IsTools(ItemDrop.ItemData item)
    {
        var sk = item.m_shared!.m_skillType;
        return sk is Skills.SkillType.Pickaxes
            or Skills.SkillType.WoodCutting;
    }
}
