namespace DrakeRenameit;

using BepInEx.Configuration;
using ServerSync;

public static class RenameitConfig
{
    private const string SectionGeneral = "General";
    private const string SectionUI = "UI-NotSynced";
    private const string SectionLimits = "Limits";
    private const string SectionAdmin = "Admin";

    // The sync object ties everything to server authority
    public static ConfigSync configSync = new ConfigSync(DrakeRenameit.ModName)
    {
        DisplayName = DrakeRenameit.ModName,
        CurrentVersion = DrakeRenameit.Version,
        MinimumRequiredVersion = DrakeRenameit.Version,
    };

    private static ConfigEntry<bool> _lockToOwner;
    private static ConfigEntry<bool> _rewriteDescriptionsEnable;
    private static ConfigEntry<bool> _RenameEnable;
    private static ConfigEntry<bool> _nameClaimsOwner;
    private static ConfigEntry<bool> _seperateStacks;
    private static ConfigEntry<bool> _allowAdminOverride;
    private static ConfigEntry<int> _nameCharLimit;
    private static ConfigEntry<int> _descCharLimit;
    private static ConfigEntry<string> _vipList;
    private static ConfigEntry<bool> _serverSync;
    private static ConfigEntry<string> _shiftColor;
    private static ConfigEntry<string> _ctrlColor;

    public static bool LockToOwner => _lockToOwner.Value;
    public static int DescCharLimit => _descCharLimit.Value;
    public static bool NameClaimsOwner => _nameClaimsOwner.Value;
    public static bool RewriteDescriptionsEnabled => _rewriteDescriptionsEnable.Value;
    public static bool RenameEnabled => _RenameEnable.Value;
    public static bool AllowAdminOverride => _allowAdminOverride.Value;
    public static int NameCharLimit => _nameCharLimit.Value;
    public static string VipList => _vipList.Value;
    public static string ShiftColor => _shiftColor.Value;
    public static string CtrlColor => _ctrlColor.Value;
    

    /*public static bool SeperateStacks => _seperateStacks.Value;*/

    public static void Bind(ConfigFile config)
    {
        // Example: Lock renames to item owner
        _lockToOwner = config.BindSynced(
            SectionGeneral,
            "LockToOwner",
            true,
            "If true, only the crafter can rename the item.",
           _serverSync.Value
        );
        
        // Example: First rename attempt claims ownership
        _nameClaimsOwner = config.BindSynced(
            SectionGeneral,
            "NameClaimsOwner",
            true,
            "If true, renaming an unowned item assigns ownership to the renamer. Used in conjunction with LockToOwner, when you rename an unclaimed item, you will have laid claim to it.",
           _serverSync.Value
        );
        _RenameEnable = config.BindSynced(
            SectionGeneral,
            "RenameEnabled",
            true,
            "If enabled, allows players to edit item names. Could be cycled to pre change some items in a world then block others from adding new ones.",
           _serverSync.Value
        );
        
        _rewriteDescriptionsEnable = config.BindSynced(
            SectionGeneral,
            "RewriteDescriptionsEnabled",
            true,
            "If enabled, allows players to also edit descriptions of items. Could be turned off preplace items with descriptions.",
           _serverSync.Value
        );   
      

        
        // Example: Lock renames to item owner
        _nameCharLimit = config.BindSynced(
            SectionLimits,
            "NameCharacterLimit",
            50,
            "Defines the limit for max characters in rename, be sure to account for <color=> tag codes etc.",
           _serverSync.Value
        );
        
        _descCharLimit = config.BindSynced(
            SectionLimits,
            "DescriptionCharacterLimit",
            1000,
            "Defines the limit for max characters description, be sure to account for <color=> tag codes etc.",
           _serverSync.Value
        );
        
        _serverSync = config.BindSynced(
            SectionAdmin,
            "ServerSync",
            true,
            "When enabled all settings will be synced to server",
            sync: true
        ); 
        
        _allowAdminOverride = config.BindSynced(
            SectionAdmin,
            "AllowAdminOverride",
            true,
            "If enabled anyone designated as admin or added to VIP list with api hook, will be able to edit names and descriptions regardless of ownership or enabled.",
           _serverSync.Value
        );        
        
        _vipList = config.BindSynced(
            SectionAdmin,
            "VipList",
            "",
            "When AdminOverride is set: this list can specify those who can ignore restrictions in additional to actual admins, and any mod that uses the API hook.",
           _serverSync.Value
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
            "ShiftColor",
            "yellow",
            "Color to display press shift + right click to ... Acceptable values anything that will be recognized by unit color engine such as a few colors yellow green red or hex: #fff or #ffffff based",
           false
        );

        
        /*_seperateStacks = config.BindSynced(
            SectionGeneral,
            "SeperateStacks",
            true,
            "If true, prevents stacks with different names from fusing.",
            sync: true
        );*/
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
