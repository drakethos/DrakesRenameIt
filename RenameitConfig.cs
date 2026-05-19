using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using ServerSync;

namespace DrakeRenameit;

public static class RenameitConfig
{
    // Config file sections use numeric prefixes so Configuration Manager's alphabetical sort matches tab order.
    private const string SectionAdmin = "01 Admin";
    private const string SectionFeatures = "02 Features";
    private const string SectionExclusions = "03 Exclusions";
    private const string SectionCraftedBy = "04 CraftedBy";
    private const string SectionGeneral = "05 General";
    private const string SectionStacks = "06 Stacks";
    private const string SectionUnlockCost = "07 UnlockCost";
    private const string SectionLimits = "08 Limits";
    private const string SectionModifiers = "09 Modifiers";
    private const string SectionUI = "10 UI-NotSynced";

    private const string DisplayAdmin = "Admin";
    private const string DisplayFeatures = "Features";
    private const string DisplayExclusions = "Exclusions";
    private const string DisplayCraftedBy = "Crafted by";
    private const string DisplayGeneral = "General";
    private const string DisplayStacks = "Stacks";
    private const string DisplayUnlockCost = "Unlock cost";
    private const string DisplayLimits = "Limits";
    private const string DisplayModifiers = "Modifiers";
    private const string DisplayUI = "UI-NotSynced";

    private static ConfigSync configSync = new ConfigSync(DrakeRenameit.ModName)
    {
        DisplayName = DrakeRenameit.ModName,
        CurrentVersion = DrakeRenameit.Version,
        MinimumRequiredVersion = DrakeRenameit.Version,
    };

    private static ConfigEntry<bool> _lockToOwner = default!;
    private static ConfigEntry<bool> _rewriteDescriptionsEnable = default!;
    private static ConfigEntry<bool> _RenameEnable = default!;
    private static ConfigEntry<bool> _nameClaimsOwner = default!;
    private static ConfigEntry<bool> _allowAdminOverride = default!;
    private static ConfigEntry<bool> _allowRenameUnownedItems = default!;
    private static ConfigEntry<int> _nameCharLimit = default!;
    private static ConfigEntry<int> _descCharLimit = default!;
    private static ConfigEntry<int> _craftedByCharLimit = default!;
    private static ConfigEntry<string> _vipList = default!;
    private static ConfigEntry<string> _menuHintColor = default!;
    private static ConfigEntry<string> _excludedNames = default!;
    private static ConfigEntry<string> _excludedCategory = default!;
    private static ConfigEntry<string> _renameAllowlist = default!;
    private static ConfigEntry<bool> _excludeStacks = default!;
    private static ConfigEntry<bool> _vipOnlyOverride = default!;
    private static ConfigEntry<bool> _showDenialUi = default!;
    private static ConfigEntry<bool> _showReason = default!;
    private static ConfigEntry<bool> _separateStacks = default!;
    private static ConfigEntry<bool> _separateStacksHardLock = default!;
    private static ConfigEntry<bool> _craftedByLabelEnabled = default!;
    private static ConfigEntry<bool> _craftedByLabelCustomizable = default!;
    private static ConfigEntry<string> _craftedByAllowedLabels = default!;
    private static ConfigEntry<string> _menuOpenModifier = default!;
    private static ConfigEntry<bool> _unlockCostEnabled = default!;
    private static ConfigEntry<string> _unlockCost = default!;
    private static ConfigEntry<bool> _showItemStandItemNameWhenNoAccess = default!;
    private static ConfigEntry<bool> _durabilityModifierEnabled = default!;
    private static ConfigEntry<string> _durabilityUnbrokenLabel = default!;
    private static ConfigEntry<string> _durabilityBrokenLabel = default!;
    private static ConfigEntry<string> _durabilityTierModifiers = default!;

    public static bool LockToOwner => _lockToOwner.Value;
    public static int DescCharLimit => _descCharLimit.Value;
    public static bool NameClaimsOwner => _nameClaimsOwner.Value;
    public static bool RewriteDescriptionsEnabled => _rewriteDescriptionsEnable.Value;
    public static bool RenameEnabled => _RenameEnable.Value;
    public static bool CraftedByLabelEnabled => _craftedByLabelEnabled.Value;
    /// <summary>When true, any player who may open the crafted-by editor can pick a tooltip line label from <see cref="CraftedByAllowedLabels"/>. When false, the picker is disabled for non-elevated players (admins/VIPs still can).</summary>
    public static bool CraftedByLabelCustomizable => _craftedByLabelCustomizable.Value;
    /// <summary>Comma- or semicolon-separated tooltip prefixes (text before “: name”). The first entry is the default (vanilla localized line); additional entries are stored on the item when chosen.</summary>
    public static string CraftedByAllowedLabels => _craftedByAllowedLabels.Value;
    /// <summary>When true, stacks with no crafter/owner may be edited (picked up, spawned, loot, raw resources).</summary>
    public static bool AllowRenameUnownedItems => _allowRenameUnownedItems.Value;
    public static bool AllowAdminOverride => _allowAdminOverride.Value;
    public static int NameCharLimit => _nameCharLimit.Value;
    public static int CraftedByCharLimit => _craftedByCharLimit.Value;
    public static string VipList => _vipList.Value;
    public static string MenuHintColor => _menuHintColor.Value;
    public static string ExcludedNames => _excludedNames.Value;
    public static string ExcludedCategory => _excludedCategory.Value;
    /// <summary>Comma-separated internal item ids (<c>m_shared.m_name</c>) that may always be renamed.</summary>
    public static string RenameAllowlist => _renameAllowlist.Value;
    /// <summary>When true, non-elevated players cannot rename, edit description, or crafted-by for items with vanilla stack limits &gt; 1 (<c>m_maxStackSize &gt; 1</c>). Overrides by admins/VIPs apply when <see cref="AllowAdminOverride"/> is on. Separate from <see cref="SeparateStacks"/> / <see cref="SeparateStacksHardLock"/>.</summary>
    public static bool ExcludeStacks => _excludeStacks.Value;
    public static bool VipOnlyOverride => _vipOnlyOverride.Value;
    /// <summary>
    /// When true, access denials (not owner, excluded, feature off, etc.) show red menu hints, tooltip lines, and center messages.
    /// Does not affect unlock-cost prompts, inventory placement errors, or validation messages.
    /// </summary>
    public static bool ShowDenialUi => _showDenialUi.Value;
    public static bool ShowReason => _showReason.Value;
    public static bool SeparateStacks => _separateStacks.Value;

    /// <summary>
    /// When <see cref="SeparateStacks"/> is on: if true, mismatched Drake identities never merge (pickup or drag).
    /// If false, auto pickup still keeps stacks separate, but you can drag-merge mismatched stacks in one step (no dialog).
    /// </summary>
    public static bool SeparateStacksHardLock => _separateStacksHardLock.Value;

    /// <summary>Keys held + right-click open the Drake action menu (e.g. Shift, Ctrl, Alt, Shift+Alt, F1). Use None for right-click only.</summary>
    public static string MenuOpenModifier => _menuOpenModifier.Value;

    /// <summary>When true (and <see cref="UnlockCost"/> parses to at least one item), a stack must be unlocked once before rename/description/crafted-by edits. Admins/VIPs still bypass when <see cref="AllowAdminOverride"/> applies.</summary>
    public static bool UnlockCostEnabled => _unlockCostEnabled.Value;

    /// <summary>Comma- or semicolon-separated list: <c>ItemPrefabName:amount</c> (e.g. <c>Coins:4</c>, <c>Coal:10</c>) or <c>$item_token:amount</c>. Resolved via the game ObjectDB when running.</summary>
    public static string UnlockCost => _unlockCost.Value;

    /// <summary>
    /// When true, item stands in warded/private areas keep the "no access" line, but also show the stand's item name on the hover label.
    /// This is purely a display/label convenience and does not change permissions.
    /// </summary>
    public static bool ShowItemStandItemNameWhenNoAccess => _showItemStandItemNameWhenNoAccess.Value;

    /// <summary>When true, a durability tier label is prepended to item display names (inventory, hover, HUD) for items with max durability &gt; 0.</summary>
    public static bool DurabilityModifierEnabled => _durabilityModifierEnabled.Value;

    /// <summary>Label at full durability (~100%). May include rich-text color tags.</summary>
    public static string DurabilityUnbrokenLabel => _durabilityUnbrokenLabel.Value;

    /// <summary>Label only when the item is at 0 durability (broken in-game, unusable until repaired). Not used for merely worn items.</summary>
    public static string DurabilityBrokenLabel => _durabilityBrokenLabel.Value;

    /// <summary>
    /// In-between wear tiers only: <c>{Rusty,0.2},{Tarnished,0.6}</c> — fraction 0–1 or percent &gt; 1. When current/max is at or below a threshold, the first matching tier (lowest threshold first) wins.
    /// Durability above the highest tier but below full gets no extra label.
    /// </summary>
    public static string DurabilityTierModifiers => _durabilityTierModifiers.Value;

    /// <summary>Parsed <see cref="CraftedByAllowedLabels"/>; first entry is always the “use game default line” option in the UI.</summary>
    public static List<string> GetCraftedByAllowedLabelsList()
    {
        var raw = _craftedByAllowedLabels.Value;
        if (string.IsNullOrWhiteSpace(raw))
            return new List<string> { "Crafted By", "Belongs To", "Return to" };

        var parts = raw.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .Where(s => s.Length > 0)
            .ToList();
        if (parts.Count == 0)
            return new List<string> { "Crafted By", "Belongs To", "Return to" };

        var deduped = new List<string>();
        foreach (var p in parts)
        {
            if (deduped.Exists(x => string.Equals(x, p, StringComparison.Ordinal)))
                continue;
            deduped.Add(p);
        }

        return deduped;
    }

    public static void Bind(ConfigFile config)
    {
        // --- Admin ---
        _allowAdminOverride = config.BindSynced(
            SectionAdmin, DisplayAdmin,
            "AllowAdminOverride",
            true,
            "If enabled, Valheim server admins and VIP list / API users may bypass ownership, exclusions, and most per-item blocks (still respects Features toggles unless elevated logic applies).",
            true);

        _vipList = config.BindSynced(
            SectionAdmin, DisplayAdmin,
            "VipList",
            "",
            "Comma-separated player names or player IDs (same as API AddVIP). When AdminOverride is on, VIPs can bypass restrictions. If VipOnlyOverride is on, ONLY VIP/API users count as elevated (Valheim server admin is ignored for overrides).",
            true);

        _vipOnlyOverride = config.BindSynced(
            SectionAdmin, DisplayAdmin,
            "VipOnlyOverride",
            false,
            "If true (and AdminOverride is on), only VIP list / AddVIP API users are treated as elevated for bypassing rules—Valheim server admin is NOT. Useful for testing VIP behavior locally. If false, Valheim admin OR VIP is elevated.",
            true);

        // --- Features (rename / description / crafted-by toggles) ---
        _RenameEnable = config.BindSynced(
            SectionFeatures, DisplayFeatures,
            "RenameEnabled",
            true,
            "If enabled, players may edit item display names (subject to all other rules). Turn off to freeze names on new edits while leaving descriptions/crafted-by alone.",
            true);

        _rewriteDescriptionsEnable = config.BindSynced(
            SectionFeatures, DisplayFeatures,
            "RewriteDescriptionsEnabled",
            true,
            "If enabled, players may edit item descriptions. Turn off to pre-place lore text and block further description changes.",
            true);

        _craftedByLabelEnabled = config.BindSynced(
            SectionFeatures, DisplayFeatures,
            "CraftedByLabelEnabled",
            true,
            "If true, players may set a display-only override for the crafted-by line (real crafter id/name unchanged).",
            true);

        // --- Exclusions ---
        _excludedNames = config.BindSynced(
            SectionExclusions, DisplayExclusions,
            "ExcludedNames",
            "",
            "Comma-separated entries: Jotunn Token ($item_...) OR Item spawn name (AxeStone) OR English display name. Admins/VIP ignore when AdminOverride is on. See ExcludedCategory and RenameAllowlist.",
            true);

        _excludedCategory = config.BindSynced(
            SectionExclusions, DisplayExclusions,
            "ExcludedCategory",
            "",
            "Comma-separated category tokens (non-elevated players). Examples: Swords,Armor,Material,Bows. Reference file: BepInEx/config/<mod GUID>/ExcludedCategoryReference.txt on first run or version change.",
            true);

        _renameAllowlist = config.BindSynced(
            SectionExclusions, DisplayExclusions,
            "RenameAllowlist",
            "",
            "Comma-separated entries (same format as ExcludedNames). When RenameEnabled / RewriteDescriptionsEnabled are ON, these items bypass ExcludedNames, ExcludedCategory, ExcludeStacks, and the unowned (AllowRenameUnownedItems) rule. Does NOT bypass global Features toggles when those are off. Does not bypass LockToOwner.",
            true);

        // --- Crafted by labels ---
        _craftedByLabelCustomizable = config.BindSynced(
            SectionCraftedBy, DisplayCraftedBy,
            "LabelCustomizable",
            true,
            "If true, players who can edit crafted-by may choose a tooltip line label from AllowedLabels. If false, the label picker is greyed out for normal players (default line only) while admins/VIPs can still change it.",
            true);

        _craftedByAllowedLabels = config.BindSynced(
            SectionCraftedBy, DisplayCraftedBy,
            "AllowedLabels",
            "Crafted By, Belongs To, Return to",
            "Comma- or semicolon-separated labels shown before the crafter name (e.g. “Belongs To: Name”). The first entry is the default (vanilla localized crafted-by line when that option is selected).",
            true);

        // --- General (ownership & behavior) ---
        _lockToOwner = config.BindSynced(
            SectionGeneral, DisplayGeneral,
            "LockToOwner",
            true,
            "If true, only the crafter/owner can rename the item, edit its description, or crafted-by display. Items with no crafter (unowned stacks) are not locked until someone claims them (NameClaimsOwner) or the item is crafted.",
            true);

        _nameClaimsOwner = config.BindSynced(
            SectionGeneral, DisplayGeneral,
            "NameClaimsOwner",
            true,
            "If true, renaming an unowned item assigns ownership to the renamer. Used with LockToOwner: the first successful rename on an unclaimed stack makes you the owner.",
            true);

        _allowRenameUnownedItems = config.BindSynced(
            SectionGeneral, DisplayGeneral,
            "AllowRenameUnownedItems",
            true,
            "If true, unowned items may be renamed or given a description/crafted-by display. Unowned means no crafter id and no crafter name — e.g. picked up from the world, spawned (console/admin), loot drops, and uncrafted resource stacks. When false, those stacks are blocked unless on RenameAllowlist. Not the same as ExcludedCategory Material (that blocks by item type even when crafted). When false, NameClaimsOwner cannot claim those stacks.",
            true);

        _showDenialUi = config.BindSynced(
            SectionGeneral, DisplayGeneral,
            "ShowDenialUi",
            true,
            "If true, access denials (not your item, excluded, rename disabled, etc.) show a red strikethrough menu hint, denial lines in tooltips, and a center message when modifier+right-click cannot open the menu. If false, those access cues are hidden. Unlock-cost, not-in-inventory, and empty-field errors are unchanged. Server-synced.",
            true);

        _showReason = config.BindSynced(
            SectionGeneral, DisplayGeneral,
            "ShowReason",
            false,
            "If true, denial text (when ShowDenialUi is on) uses specific reasons (ownership, exclusion, unowned, etc.). If false, generic messages only. Has no effect when ShowDenialUi is off. Server-synced.",
            true);

        _showItemStandItemNameWhenNoAccess = config.BindSynced(
            SectionGeneral, DisplayGeneral,
            "ShowItemStandItemNameWhenNoAccess",
            true,
            "If true, item stands inside warded/private areas still show 'no access' but also append the stand's current item name to the hover label (display only; permissions unchanged).",
            true);

        // --- Stacks (merge rules, stackable edit block) ---
        _separateStacks = config.BindSynced(
            SectionStacks, DisplayStacks,
            "SeparateStacks",
            true,
            "If true, stacks only combine when Drake custom name, description, and crafted-by display match (same identity). Renamed or customized stacks no longer absorb mismatched pickups automatically.",
            true);

        _separateStacksHardLock = config.BindSynced(
            SectionStacks, DisplayStacks,
            "SeparateStacksHardLock",
            false,
            "Only applies when SeparateStacks is on. If true, mismatched stacks never merge — including manual drags. If false, auto pickup still will not combine mismatched stacks, but you can drag one stack onto another to merge immediately (target stack keeps its custom data).",
            true);

        _excludeStacks = config.BindSynced(
            SectionStacks, DisplayStacks,
            "ExcludeStacks",
            false,
            "When true, non-elevated players cannot rename, change descriptions, or edit crafted-by on stackable vanilla items (m_maxStackSize > 1). Admins/VIPs override when AllowAdminOverride is on. Items on RenameAllowlist bypass this. Does not change SeparateStacks merge behavior.",
            true);

        // --- Unlock cost (per-stack pay gate) ---
        _unlockCostEnabled = config.BindSynced(
            SectionUnlockCost, DisplayUnlockCost,
            "UnlockCostEnabled",
            false,
            "If true, each item stack must be unlocked once (pay UnlockCost from your inventory) before rename, description, or crafted-by edits apply. Invalid/empty UnlockCost is ignored. Elevated players skip the cost when AdminOverride applies.",
            true);

        _unlockCost = config.BindSynced(
            SectionUnlockCost, DisplayUnlockCost,
            "UnlockCost",
            "Coins:5",
            "Comma or semicolon separated: PrefabName:amount (e.g. Coins:4, Coal:10) or $item_token:amount. Uses player inventory. Paid once per stack via the Unlock button in the action menu.",
            true);

        // --- Limits ---
        _nameCharLimit = config.BindSynced(
            SectionLimits, DisplayLimits,
            "NameCharacterLimit",
            50,
            "Max characters for rename text (count <color=> tag codes toward the limit).",
            true);

        _craftedByCharLimit = config.BindSynced(
            SectionLimits, DisplayLimits,
            "CraftedByCharLimit",
            50,
            "Max characters for crafted-by display name (count rich-text tags toward the limit).",
            true);

        _descCharLimit = config.BindSynced(
            SectionLimits, DisplayLimits,
            "DescriptionCharacterLimit",
            1000,
            "Max characters for item description (count rich-text tags toward the limit).",
            true);

        // --- Modifiers (durability display) ---
        _durabilityModifierEnabled = config.BindSynced(
            SectionModifiers, DisplayModifiers,
            "DurabilityModifierEnabled",
            false,
            "If true, prepends a durability wear label (Pristine / tiers / Broken) in front of the item name everywhere Drake builds display names — only for forge-repair gear and tools (m_useDurability), not spoilage timers. Does not change stored rename text.",
            true);

        _durabilityUnbrokenLabel = config.BindSynced(
            SectionModifiers, DisplayModifiers,
            "DurabilityUnbrokenLabel",
            "Pristine",
            "Prepended at full durability (100%). Empty = no label when pristine. Rich-text color tags allowed.",
            true);

        _durabilityBrokenLabel = config.BindSynced(
            SectionModifiers, DisplayModifiers,
            "DurabilityBrokenLabel",
            "<#f00>Broken</color>",
            "Prepended only when durability is 0 (item is broken in-game and must be repaired). Not used for worn but usable gear. Empty = no label when broken.",
            true);

        _durabilityTierModifiers = config.BindSynced(
            SectionModifiers, DisplayModifiers,
            "DurabilityTierModifiers",
            "{<#c50>Rusty</color>,0.4},{<#888>Worn</color>,0.6},{<#aaa>Tarnished</color>,0.8}",
            "Wear bands between broken and pristine: {Name,fraction} or {Name,percent}. Each applies when current/max is at or below that value; lowest matching band wins. Supports <color> tags; terminate with </color> unless you add a high threshold near 1.",
            true);

        // --- UI (not server-synced) ---
        _menuHintColor = config.BindSynced(
            SectionUI, DisplayUI,
            "MenuHintColor",
            "yellow",
            "Color for the inventory tooltip hint (Modifier + Right Click). Unity color names or hex (#fff / #ffffff).",
            false);

        _menuOpenModifier = config.Bind(
            SectionUI,
            "MenuOpenModifier",
            "Shift",
            new ConfigDescription(
                "Keys held while right-clicking an inventory item to open the Drake menu. Examples: Shift, Ctrl, Alt, Shift+Alt, F1. Combine with + , or &. Use None for right-click only. Not server-synced (per-client).",
                null,
                new ConfigurationManagerAttributes { Category = DisplayUI }));
        configSync.AddConfigEntry(_menuOpenModifier).SynchronizedConfig = false;

        MigrateLegacyConfigSections(config);
    }

    private static ConfigEntry<T> BindSynced<T>(
        this ConfigFile config,
        string section,
        string displayCategory,
        string key,
        T defaultValue,
        string description,
        bool sync = true)
    {
        var entry = config.Bind(
            section,
            key,
            defaultValue,
            new ConfigDescription(description, null, new ConfigurationManagerAttributes { Category = displayCategory }));
        var syncedEntry = configSync.AddConfigEntry(entry);
        syncedEntry.SynchronizedConfig = sync;
        return entry;
    }

    /// <summary>Copies values from pre-reorder section names so existing cfg files keep working.</summary>
    private static void MigrateLegacyConfigSections(ConfigFile config)
    {
        MigrateSectionKeys(config, "Admin", SectionAdmin,
            "AllowAdminOverride", "VipList", "VipOnlyOverride");
        MigrateSectionKeys(config, "Features", SectionFeatures,
            "RenameEnabled", "RewriteDescriptionsEnabled", "CraftedByLabelEnabled");
        MigrateSectionKeys(config, "Exclusions", SectionExclusions,
            "ExcludedNames", "ExcludedCategory", "RenameAllowlist");
        MigrateSectionKeys(config, "CraftedBy", SectionCraftedBy,
            "LabelCustomizable", "AllowedLabels");
        MigrateSectionKeys(config, "General", SectionGeneral,
            "LockToOwner", "NameClaimsOwner", "AllowRenameUnownedItems", "ShowDenialUi", "ShowReason", "ShowItemStandItemNameWhenNoAccess");

        MigrateHideDisabledDenialUiToShowDenialUi(config);
        MigrateSectionKeys(config, "Stacks", SectionStacks,
            "SeparateStacks", "SeparateStacksHardLock", "ExcludeStacks");
        MigrateSectionKeys(config, "UnlockCost", SectionUnlockCost,
            "UnlockCostEnabled", "UnlockCost");
        MigrateSectionKeys(config, "Limits", SectionLimits,
            "NameCharacterLimit", "CraftedByCharLimit", "DescriptionCharacterLimit");
        MigrateSectionKeys(config, "Modifiers", SectionModifiers,
            "DurabilityModifierEnabled", "DurabilityUnbrokenLabel", "DurabilityBrokenLabel", "DurabilityTierModifiers");
        MigrateSectionKeys(config, "UI-NotSynced", SectionUI,
            "MenuHintColor", "MenuOpenModifier");

        // Unlock cost keys used to live under Stacks.
        MigrateSectionKeys(config, "Stacks", SectionUnlockCost, "UnlockCostEnabled", "UnlockCost");
    }

    private static void MigrateSectionKeys(ConfigFile config, string legacySection, string newSection, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (TryMigrate<bool>(config, legacySection, key, newSection)) continue;
            if (TryMigrate<int>(config, legacySection, key, newSection)) continue;
            TryMigrate<string>(config, legacySection, key, newSection);
        }
    }

    private static bool TryMigrate<T>(ConfigFile config, string legacySection, string key, string newSection)
    {
        if (!config.TryGetEntry(legacySection, key, out ConfigEntry<T> legacy))
            return false;
        if (!config.TryGetEntry(newSection, key, out ConfigEntry<T> target))
            return false;
        target.Value = legacy.Value;
        return true;
    }

    /// <summary>Renamed <c>HideDisabledDenialUi</c> → <c>ShowDenialUi</c> (inverted).</summary>
    private static void MigrateHideDisabledDenialUiToShowDenialUi(ConfigFile config)
    {
        foreach (var section in new[] { SectionGeneral, "General" })
        {
            if (!config.TryGetEntry(section, "HideDisabledDenialUi", out ConfigEntry<bool> legacy))
                continue;
            if (!config.TryGetEntry(SectionGeneral, "ShowDenialUi", out ConfigEntry<bool> target))
                return;
            target.Value = !legacy.Value;
            return;
        }
    }
}
