using System;
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
    private static ConfigEntry<string> _vipList;
    private static ConfigEntry<string> _shiftColor;
    private static ConfigEntry<string> _ctrlColor;
    private static ConfigEntry<string> _excludedNames;
    private static ConfigEntry<string> _excludedCategory;
    private static ConfigEntry<string> _renameAllowlist;
    private static ConfigEntry<bool> _vipOnlyOverride;
    private static ConfigEntry<bool> _showReason;
    private static ConfigEntry<bool> _separateStacks;
    private static ConfigEntry<bool> _craftedByLabelEnabled;
    private static ConfigEntry<string> _menuOpenModifier;

    
    public static bool LockToOwner => _lockToOwner.Value;
    public static int DescCharLimit => _descCharLimit.Value;
    public static bool NameClaimsOwner => _nameClaimsOwner.Value;
    public static bool RewriteDescriptionsEnabled => _rewriteDescriptionsEnable.Value;
    public static bool RenameEnabled => _RenameEnable.Value;
    public static bool CraftedByLabelEnabled => _craftedByLabelEnabled.Value;
    public static bool AllowRenameResources => _allowRenameResources.Value;
    public static bool AllowAdminOverride => _allowAdminOverride.Value;
    public static int NameCharLimit => _nameCharLimit.Value;
    public static string VipList => _vipList.Value;
    public static string ShiftColor => _shiftColor.Value;
    public static string CtrlColor => _ctrlColor.Value;
    public static string ExcludedNames => _excludedNames.Value;
    public static string ExcludedCategory => _excludedCategory.Value;
    /// <summary>Comma-separated internal item ids (<c>m_shared.m_name</c>) that may always be renamed.</summary>
    public static string RenameAllowlist => _renameAllowlist.Value;
    public static bool VipOnlyOverride => _vipOnlyOverride.Value;
    public static bool ShowReason => _showReason.Value;
    public static bool SeparateStacks => _separateStacks.Value;

    /// <summary>Shift or Ctrl — which modifier + right-click opens the Drake action menu.</summary>
    public static string MenuOpenModifier => _menuOpenModifier.Value;

    public static bool MenuModifierIsShift =>
        string.Equals(_menuOpenModifier.Value, "Shift", StringComparison.OrdinalIgnoreCase);

    
    public static void Bind(ConfigFile config)
    {
        // Example: Lock renames to item owner
        _lockToOwner = config.BindSynced(
            SectionGeneral,
            "LockToOwner",
            true,
            "If true, only the crafter/owner can rename or edit description. Items with no crafter (raw resources, m_crafterID 0 and no crafter name) are not locked until someone claims them (NameClaimsOwner) or they are crafted.",
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
            "If true, items with no crafter (picked-up / uncrafted stacks) can be renamed. This is NOT the same as ExcludedCategory Material: Material blocks by item type even for crafted items. If disabled, NameClaimsOwner cannot apply to those stacks.",
           true
        );

        _RenameEnable = config.BindSynced(
            SectionGeneral,
            "RenameEnabled",
            true,
            "If enabled, allows players to edit item names. Could be cycled to pre change some items in a world then block others from adding new ones.",
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
            false,
            "If true, denied rename/description actions show specific reasons (ownership, exclusion, resources). If false, generic messages only. Server-synced so clients cannot override the server's disclosure policy.",
            true
        );

        _separateStacks = config.BindSynced(
            SectionGeneral,
            "SeparateStacks",
            false,
            "If true, stacks only combine when Drake custom name, description, and crafted-by display match (same identity). Renamed or customized stacks no longer absorb mismatched pickups automatically.",
            true
        );


        
        // Example: Lock renames to item owner
        _nameCharLimit = config.BindSynced(
            SectionLimits,
            "NameCharacterLimit",
            50,
            "Defines the limit for max characters in rename, be sure to account for <color=> tag codes etc.",
           true
        );
        
        _descCharLimit = config.BindSynced(
            SectionLimits,
            "DescriptionCharacterLimit",
            1000,
            "Defines the limit for max characters description, be sure to account for <color=> tag codes etc.",
           true
        );

        _allowAdminOverride = config.BindSynced(
            SectionAdmin,
            "AllowAdminOverride",
            true,
            "If enabled anyone designated as admin or added to VIP list with api hook, will be able to edit names and descriptions regardless of ownership or enabled.",
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
        
        _shiftColor = config.BindSynced(
            SectionUI,
            "ShiftColor",
            "yellow",
            "Color to display press shift + right click to ... Acceptable values anything that will be recognized by unit color engine such as a few colors yellow green red or hex: #fff or #ffffff based",
           false
        );
        _ctrlColor = config.BindSynced(
            SectionUI,
            "CtrlColor",
            "yellow",
            "Color for inventory hint when MenuOpenModifier is Ctrl (see MenuOpenModifier).",
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
            "Comma-separated category tokens (non-elevated players). Examples: Swords,Armor,Material,Bows. Full lists of Skills.SkillType and ItemDrop.ItemData.ItemType names plus aliases are written to BepInEx/config/<this mod GUID>/ExcludedCategoryReference.txt on first run (or when the mod version changes). Does not bypass RenameEnabled when that is off. Elevated users ignore when AdminOverride is on.",
            true
        );

        _renameAllowlist = config.BindSynced(
            SectionExclusions,
            "RenameAllowlist",
            "",
            "Comma-separated entries: Jotunn Token ($item_...) or Item (spawn name) or English display name — same rules as ExcludedNames. When RenameEnabled / RewriteDescriptionsEnabled are ON, these items bypass ExcludedNames, ExcludedCategory, and the uncrafted (AllowRenameResources) rule. Does NOT bypass global RenameEnabled/RewriteDescriptionsEnabled when those are off (only elevated users can). Does not bypass LockToOwner ownership.",
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
