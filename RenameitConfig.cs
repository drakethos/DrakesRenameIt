using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using ServerSync;

namespace DrakeRenameit;

public static class RenameitConfig
{
    private const string SectionGeneral = "General";
    private const string SectionUI = "UI-NotSynced";
    private const string SectionExclusions ="Exclusions";
    private const string SectionLimits = "Limits";
    private const string SectionAdmin = "Admin";
    private const string SectionUnlock = "UnlockCost";
    private const string SectionCraftedBy = "CraftedBy";
    private const string SectionModifiers = "Modifiers";

    // The sync object ties everything to server authority
    private static ConfigSync configSync = new ConfigSync(DrakeRenameit.ModName)
    {
        DisplayName = DrakeRenameit.ModName,
        CurrentVersion = DrakeRenameit.Version,
        MinimumRequiredVersion = DrakeRenameit.Version,
    };

    private static ConfigEntry<bool> _lockToOwner;
    private static ConfigEntry<bool> _rewriteDescriptionsEnable;
    private static ConfigEntry<bool> _RenameEnable;
    private static ConfigEntry<bool> _nameClaimsOwner;
    private static ConfigEntry<bool> _allowAdminOverride;
    private static ConfigEntry<bool> _allowRenameResources;
    private static ConfigEntry<int> _nameCharLimit;
    private static ConfigEntry<int> _descCharLimit;
    private static ConfigEntry<int> _craftedByCharLimit;
    private static ConfigEntry<string> _vipList;
    private static ConfigEntry<string> _menuHintColor;
    private static ConfigEntry<string> _excludedNames;
    private static ConfigEntry<string> _excludedCategory;
    private static ConfigEntry<string> _renameAllowlist;
    private static ConfigEntry<bool> _excludeStackable;
    private static ConfigEntry<bool> _vipOnlyOverride;
    private static ConfigEntry<bool> _showReason;
    private static ConfigEntry<bool> _separateStacks;
    private static ConfigEntry<bool> _separateStacksHardLock;
    private static ConfigEntry<bool> _craftedByLabelEnabled;
    private static ConfigEntry<bool> _craftedByLabelCustomizable;
    private static ConfigEntry<string> _craftedByAllowedLabels;
    private static ConfigEntry<string> _menuOpenModifier;
    private static ConfigEntry<bool> _unlockCostEnabled;
    private static ConfigEntry<string> _unlockCost;
    private static ConfigEntry<bool> _showItemStandItemNameWhenNoAccess;
    private static ConfigEntry<bool> _durabilityModifierEnabled;
    private static ConfigEntry<string> _durabilityUnbrokenLabel;
    private static ConfigEntry<string> _durabilityBrokenLabel;
    private static ConfigEntry<string> _durabilityTierModifiers;

    
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
    public static bool AllowRenameResources => _allowRenameResources.Value;
    public static bool AllowAdminOverride => _allowAdminOverride.Value;
    public static int NameCharLimit => _nameCharLimit.Value;
    public static int CraftedByCharLimit => _craftedByCharLimit.Value;
    public static string VipList => _vipList.Value;
    public static string MenuHintColor => _menuHintColor.Value;
    public static string ExcludedNames => _excludedNames.Value;
    public static string ExcludedCategory => _excludedCategory.Value;
    /// <summary>Comma-separated internal item ids (<c>m_shared.m_name</c>) that may always be renamed.</summary>
    public static string RenameAllowlist => _renameAllowlist.Value;
    /// <summary>When true, non-elevated players cannot rename, edit description, or crafted-by for items with <c>m_maxStackSize &gt; 1</c>. Does not affect <see cref="SeparateStacks"/> or other General stack settings. Elevated users ignore this when <see cref="AllowAdminOverride"/> applies.</summary>
    public static bool ExcludeStackable => _excludeStackable.Value;
    public static bool VipOnlyOverride => _vipOnlyOverride.Value;
    public static bool ShowReason => _showReason.Value;
    public static bool SeparateStacks => _separateStacks.Value;

    /// <summary>
    /// When <see cref="SeparateStacks"/> is on: if true, mismatched Drake identities never merge (pickup or drag).
    /// If false, auto pickup still keeps stacks separate, but you can drag-merge mismatched stacks in one step (no dialog).
    /// </summary>
    public static bool SeparateStacksHardLock => _separateStacksHardLock.Value;

    /// <summary>Shift or Ctrl — which modifier + right-click opens the Drake action menu.</summary>
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

    public static bool MenuModifierIsShift =>
        string.Equals(_menuOpenModifier.Value, "Shift", StringComparison.OrdinalIgnoreCase);

    /// <summary>Parsed <see cref="CraftedByAllowedLabels"/>; first entry is always the “use game default line” option in the UI.</summary>
    public static List<string> GetCraftedByAllowedLabelsList()
    {
        var raw = _craftedByAllowedLabels?.Value;
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
        // Example: Lock renames to item owner
        _lockToOwner = config.BindSynced(
            SectionGeneral,
            "LockToOwner",
            true,
            "If true, only the crafter/owner can rename the item, edit its description, or 'Crafted By' : name. Items with no crafter (raw resources, m_crafterID and no crafter name) are not locked until someone claims them (NameClaimsOwner) or they are crafted.",
           true
        );
        
        // Example: First rename attempt claims ownership
        _nameClaimsOwner = config.BindSynced(
            SectionGeneral,
            "NameClaimsOwner",
            true,
            "If true, renaming an unowned item assigns ownership to the renamer. Used in conjunction with LockToOwner, when you rename an unclaimed item, you will have laid claim to it.",
           true
        );

        _allowRenameResources = config.BindSynced(
            SectionGeneral,
            "AllowRenameResources",
            true,
            "If true, items with no crafter (picked-up / uncrafted stacks) can be renamed. This is NOT the same as ExcludedCategory Material: Material blocks by item type even for crafted items. If disabled, NameClaimsOwner cannot apply to those items.",
           true
        );

        _RenameEnable = config.BindSynced(
            SectionGeneral,
            "RenameEnabled",
            true,
            "If enabled, allows players to edit item names. Could be used to leave renamed items in a world then block others from changing new ones.",
           true
        );
        
        _rewriteDescriptionsEnable = config.BindSynced(
            SectionGeneral,
            "RewriteDescriptionsEnabled",
            true,
            "If enabled, allows players to also edit descriptions of items. Could be turned off preplace items with descriptions.",
           true
        );
        
        _craftedByLabelEnabled = config.BindSynced(
            SectionGeneral,
            "CraftedByLabelEnabled",
            true,
            "If true, players may set a display-only override for the Crafted by line (real crafter id/name unchanged). Server-synced.",
            true
        );

        _showReason = config.BindSynced(
            SectionGeneral,
            "ShowReason",
            true,
            "If true, denied edit item actions show specific reasons (ownership, exclusion, resources). If false, generic messages only. Server-synced so clients cannot override the server's disclosure policy.",
            true
        );

        _showItemStandItemNameWhenNoAccess = config.BindSynced(
            SectionGeneral,
            "ShowItemStandItemNameWhenNoAccess",
            true,
            "If true, item stands inside warded/private areas still show 'no access' but also append the stand's current item name to the hover label (display only; permissions unchanged).",
            true
        );

        _separateStacks = config.BindSynced(
            SectionGeneral,
            "SeparateStacks",
            true,
            "If true, stacks only combine when Drake custom name, description, and crafted-by display match (same identity). Renamed or customized stacks no longer absorb mismatched pickups automatically.",
            true
        );

        _separateStacksHardLock = config.BindSynced(
            SectionGeneral,
            "SeparateStacksHardLock",
            false,
            "Only applies when SeparateStacks is on. If true, mismatched stacks never merge — including manual drags. If false, auto pickup still will not combine mismatched stacks, but you can drag one stack onto another to merge immediately (target stack keeps its custom name, description, and crafted-by display).",
            true
        );

        _unlockCostEnabled = config.BindSynced(
            SectionUnlock,
            "UnlockCostEnabled",
            false,
            "If true, each item stack must be unlocked once (pay UnlockCost from your inventory) before rename, description, or crafted-by edits apply. After unlock, edits are free for that stack. Invalid/empty UnlockCost is ignored (no gate). Elevated players (AdminOverride) skip the cost.",
            true
        );

        _unlockCost = config.BindSynced(
            SectionUnlock,
            "UnlockCost",
            "Coins:5",
            "Comma or semicolon separated: PrefabName:amount (e.g. Coins:4, Coal:10) or $item_token:amount. Uses player inventory. Paid once per stack via the Unlock button in the action menu.",
            true
        );

        _nameCharLimit = config.BindSynced(
            SectionLimits,
            "NameCharacterLimit",
            50,
            "Defines the limit for max characters in rename, be sure to account for <color=> tag codes etc.",
           true
        );

        _craftedByCharLimit = config.BindSynced(
            SectionLimits,
            "CraftedByCharLimit",
            50,
            "Defines the limit for max characters in crafted by: edit, be sure to account for <color=> tag codes etc.",
            true
        );

        _descCharLimit = config.BindSynced(
            SectionLimits,
            "DescriptionCharacterLimit",
            1000,
            "Defines the limit for max characters description, be sure to account for <color=> tag codes etc.",
            true
        );

        _craftedByLabelCustomizable = config.BindSynced(
            SectionCraftedBy,
            "LabelCustomizable",
            true,
            "If true, players who can edit crafted-by may choose a tooltip line label from AllowedLabels. If false, the label picker is greyed out for normal players (shows the default line only) while admins/VIPs can still change it. Separate from CraftedByLabelEnabled in General.",
            true
        );

        _craftedByAllowedLabels = config.BindSynced(
            SectionCraftedBy,
            "AllowedLabels",
            "Crafted By, Belongs To, Return to",
            "Comma- or semicolon-separated labels shown before the crafter name (e.g. “Belongs To: Name”). The first entry is the default (vanilla localized “crafted by” line is used on the tooltip when that option is selected).",
            true
        );

        _allowAdminOverride = config.BindSynced(
            SectionAdmin,
            "AllowAdminOverride",
            true,
            "If enabled anyone designated as admin or added to VIP list with api hook, will be able to edit names, descriptions, and crafted-by regardless of ownership or enabled.",
           true
        );        
        
        _vipList = config.BindSynced(
            SectionAdmin,
            "VipList",
            "",
            "Comma-separated player names or player IDs (same as API AddVIP). When AdminOverride is on, VIPs can bypass restrictions. If VipOnlyOverride is on, ONLY VIP/API users count as elevated (Valheim server admin is ignored for overrides).",
           true
        );

        _vipOnlyOverride = config.BindSynced(
            SectionAdmin,
            "VipOnlyOverride",
            false,
            "If true (and AdminOverride is on), only VIP list / AddVIP API users are treated as elevated for bypassing rules—Valheim server admin is NOT. Useful for testing VIP behavior locally. If false, Valheim admin OR VIP is elevated.",
            true
        );
        
        _menuHintColor = config.BindSynced(
            SectionUI,
            "MenuHintColor",
            "yellow",
            "Color for the 'Modifier + Right Click for options' inventory tooltip hint. Accepts Unity color names (yellow, green, red, white) or hex values (#fff or #ffffff).",
           false
        );

        _menuOpenModifier = config.Bind(
            SectionUI,
            "MenuOpenModifier",
            "Shift",
            new ConfigDescription(
                "Which key + right-click opens the Drake menu (Rename / Description / Crafted by): Shift or Ctrl. The other modifier + right-click uses vanilla behavior.",
                new AcceptableValueList<string>(new[] { "Shift", "Ctrl" })));
        configSync.AddConfigEntry(_menuOpenModifier).SynchronizedConfig = false;
        
        _excludedNames = config.BindSynced(
            SectionExclusions,
            "ExcludedNames",
            "",
            "Comma-separated entries: Jotunn item list Token column (m_shared.m_name, e.g. $item_axe_stone) OR Item column (spawn/prefab name, e.g. AxeStone, ShieldBronzeBuckler), OR localized display name (English Name column). List: https://valheim-modding.github.io/Jotunn/data/objects/item-list.html — Admins/VIP (when AdminOverride is on) ignore this. See also ExcludedCategory and RenameAllowlist.",
            true
        );

        _excludedCategory = config.BindSynced(
            SectionExclusions,
            "ExcludedCategory",
            "",
            "Comma-separated category tokens (non-elevated players). Examples: Swords,Armor,Material,Bows. Full lists of Skills.SkillType and ItemDrop.ItemData.ItemType names plus aliases are written to BepInEx/config/<this mod GUID>/ExcludedCategoryReference.txt on first run (or when the mod version changes). Does not bypass RenameEnabled when that is off. Elevated users ignore when AdminOverride is on. See generated file: ExcludedCategoryReference.txt for a list of what can go in.",
            true
        );

        _excludeStackable = config.BindSynced(
            SectionExclusions,
            "ExcludeStackable",
            false,
            "If true, non-elevated players cannot change names, descriptions, or crafted-by on items that stack in vanilla (m_maxStackSize > 1). Does not change SeparateStacks / SeparateStacksHardLock or any other General stacking options. Admins and VIPs (when AllowAdminOverride is on) are unaffected. Items on RenameAllowlist bypass this, same as other exclusion rules.",
            true
        );

        _renameAllowlist = config.BindSynced(
            SectionExclusions,
            "RenameAllowlist",
            "",
            "Comma-separated entries: Jotunn Token ($item_...) or Item (spawn name) or English display name — same rules as ExcludedNames. When RenameEnabled / RewriteDescriptionsEnabled are ON, these items bypass ExcludedNames, ExcludedCategory, ExcludeStackable, and the uncrafted (AllowRenameResources) rule. Does NOT bypass global RenameEnabled/RewriteDescriptionsEnabled when those are off (only elevated users can). Does not bypass LockToOwner ownership.",
            true
        );

        _durabilityModifierEnabled = config.BindSynced(
            SectionModifiers,
            "DurabilityModifierEnabled",
            false,
            "If true, prepends a durability label in front of the item name everywhere Drake builds display names (custom name or vanilla). Does not change stored rename text. Only applies to items that use durability (m_maxDurability > 0); resources like wood or coins are unchanged. Server-synced.",
            true
        );

        _durabilityUnbrokenLabel = config.BindSynced(
            SectionModifiers,
            "DurabilityUnbrokenLabel",
            "Pristine",
            "Prepended at full durability (100%). Empty = no label when pristine. Rich-text color tags allowed.",
            true
        );

        _durabilityBrokenLabel = config.BindSynced(
            SectionModifiers,
            "DurabilityBrokenLabel",
            "Broken",
            "Prepended only when durability is 0 (item is broken in-game and must be repaired). Not used for worn but usable gear. Empty = no label when broken.",
            true
        );

        _durabilityTierModifiers = config.BindSynced(
            SectionModifiers,
            "DurabilityTierModifiers",
            "{Rusty,0.2},{Tarnished,0.6}",
            "Wear bands between broken and pristine: {Name,fraction} or {Name,percent}. Each applies when current/max is at or below that value; lowest matching band wins. Between the top band and full durability there is no label (unless you add a high threshold near 1).",
            true
        );
    }

    // Helper extension for easier ServerSync binding
    private static ConfigEntry<T> BindSynced<T>(
        this ConfigFile config,
        string section,
        string key,
        T defaultValue,
        string description,
        bool sync = true)
    {
        var entry = config.Bind(section, key, defaultValue, description);
        var syncedEntry = configSync.AddConfigEntry(entry);
        syncedEntry.SynchronizedConfig = sync;
        return entry;
    }
}
