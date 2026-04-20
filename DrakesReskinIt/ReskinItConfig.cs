using BepInEx.Configuration;
using ServerSync;

namespace DrakesReskinIt;

public static class ReskinItConfig
{
    private const string SectionGeneral = "General";
    private const string SectionUI = "UI-NotSynced";
    private const string SectionAdmin = "Admin";
    private const string SectionExclusions = "Exclusions";

    private static ConfigSync configSync = new ConfigSync(DrakesReskinIt.ModName)
    {
        DisplayName = DrakesReskinIt.ModName,
        CurrentVersion = DrakesReskinIt.Version,
        MinimumRequiredVersion = DrakesReskinIt.Version,
    };

    private static ConfigEntry<bool> _reskinEnabled;
    private static ConfigEntry<bool> _tintEnabled;
    private static ConfigEntry<bool> _allowAdminOverride;
    private static ConfigEntry<bool> _lockToOwner;
    private static ConfigEntry<bool> _nameClaimsOwner;
    private static ConfigEntry<bool> _vipOnlyOverride;
    private static ConfigEntry<bool> _showReason;
    private static ConfigEntry<string> _vipList;
    private static ConfigEntry<string> _excludedNames;
    private static ConfigEntry<string> _excludedCategory;
    private static ConfigEntry<string> _reskinAllowlist;
    private static ConfigEntry<string> _menuOpenModifier;
    private static ConfigEntry<string> _menuHintColor;

    // ─── Public accessors ─────────────────────────────────────────────────────

    /// <summary>Master toggle: if false, no icon changes are allowed for non-elevated players.</summary>
    public static bool ReskinEnabled => _reskinEnabled.Value;

    /// <summary>If false, the tint/recolor sub-feature is globally disabled for non-elevated players.</summary>
    public static bool TintEnabled => _tintEnabled.Value;

    /// <summary>When true, admin or VIP can override ownership and exclusion rules.</summary>
    public static bool AllowAdminOverride => _allowAdminOverride.Value;

    /// <summary>If true, only the crafter/owner may reskin the item.</summary>
    public static bool LockToOwner => _lockToOwner.Value;

    /// <summary>If true, reskinning an unowned item claims ownership for the player.</summary>
    public static bool NameClaimsOwner => _nameClaimsOwner.Value;

    /// <summary>If true, only VIP-list / API-added users are treated as elevated (Valheim server admin ignored).</summary>
    public static bool VipOnlyOverride => _vipOnlyOverride.Value;

    /// <summary>If true, denial messages include the specific reason; if false, generic messages only.</summary>
    public static bool ShowReason => _showReason.Value;

    /// <summary>Comma-separated player names or player IDs treated as VIPs.</summary>
    public static string VipList => _vipList.Value;

    /// <summary>Comma-separated item token/prefab/display-name ids blocked for non-admins.</summary>
    public static string ExcludedNames => _excludedNames.Value;

    /// <summary>Comma-separated ItemType or SkillType names blocked for non-admins.</summary>
    public static string ExcludedCategory => _excludedCategory.Value;

    /// <summary>Comma-separated item ids that always bypass exclusions (but not ownership or global disable).</summary>
    public static string ReskinAllowlist => _reskinAllowlist.Value;

    /// <summary>Shift or Ctrl — which modifier + right-click opens the Drake Reskin action menu.</summary>
    public static string MenuOpenModifier => _menuOpenModifier.Value;

    /// <summary>HTML color name or hex used for the menu-hint line in item tooltips.</summary>
    public static string MenuHintColor => _menuHintColor.Value;

    public static bool MenuModifierIsShift =>
        string.Equals(_menuOpenModifier.Value, "Shift", System.StringComparison.OrdinalIgnoreCase);

    // ─── Bind ─────────────────────────────────────────────────────────────────

    public static void Bind(ConfigFile config)
    {
        _reskinEnabled = config.BindSynced(
            SectionGeneral,
            "ReskinEnabled",
            true,
            "If true, players can change item icons via the action menu.",
            true);

        _tintEnabled = config.BindSynced(
            SectionGeneral,
            "TintEnabled",
            true,
            "If true, players can apply a recolor/tint to item icons via the action menu.",
            true);

        _lockToOwner = config.BindSynced(
            SectionGeneral,
            "LockToOwner",
            true,
            "If true, only the crafter/owner of the item can reskin it. Items with no crafter (m_crafterID 0) are not locked until someone claims them.",
            true);

        _nameClaimsOwner = config.BindSynced(
            SectionGeneral,
            "ReskinClaimsOwner",
            true,
            "If true, reskinning an unowned item assigns ownership to the player who reskins it. Works with LockToOwner.",
            true);

        _showReason = config.BindSynced(
            SectionGeneral,
            "ShowReason",
            false,
            "If true, denied reskin actions show specific reasons (ownership, exclusion). If false, generic messages only. Server-synced.",
            true);

        _allowAdminOverride = config.BindSynced(
            SectionAdmin,
            "AllowAdminOverride",
            true,
            "If true, admins and VIPs can change item icons regardless of ownership or enabled state.",
            true);

        _vipList = config.BindSynced(
            SectionAdmin,
            "VipList",
            "",
            "Comma-separated player names or IDs. When AdminOverride is on, VIPs can bypass restrictions.",
            true);

        _vipOnlyOverride = config.BindSynced(
            SectionAdmin,
            "VipOnlyOverride",
            false,
            "If true (and AdminOverride is on), only VIP list / AddVIP API users are elevated — Valheim server admin is NOT.",
            true);

        _excludedNames = config.BindSynced(
            SectionExclusions,
            "ExcludedNames",
            "",
            "Comma-separated item token ($item_...), prefab name, or English display name that non-elevated players cannot reskin. See ExcludedCategory and ReskinAllowlist.",
            true);

        _excludedCategory = config.BindSynced(
            SectionExclusions,
            "ExcludedCategory",
            "",
            "Comma-separated category tokens (non-elevated players): e.g. Swords,Armor,Material. Same token set as DrakeRenameIt ExcludedCategory.",
            true);

        _reskinAllowlist = config.BindSynced(
            SectionExclusions,
            "ReskinAllowlist",
            "",
            "Comma-separated item tokens/prefab names/display names. These items bypass ExcludedNames and ExcludedCategory. Does not bypass LockToOwner or global disable.",
            true);

        _menuHintColor = config.BindSynced(
            SectionUI,
            "MenuHintColor",
            "yellow",
            "Color used for the modifier+right-click hint line in item tooltips. Accepts Unity color names or hex (#rrggbb).",
            false);

        _menuOpenModifier = config.Bind(
            SectionUI,
            "MenuOpenModifier",
            "Shift",
            new ConfigDescription(
                "Which key + right-click opens the Drake Reskin menu: Shift or Ctrl.",
                new AcceptableValueList<string>(new[] { "Shift", "Ctrl" })));
        configSync.AddConfigEntry(_menuOpenModifier).SynchronizedConfig = false;
    }

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
