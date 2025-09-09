namespace DrakeRenameit;

using BepInEx.Configuration;
using ServerSync;

public static class RenameitConfig
{
    private const string SectionGeneral = "General";

    // The sync object ties everything to server authority
    public static ConfigSync configSync = new ConfigSync(DrakeRenameit.ModName)
    {
        DisplayName = DrakeRenameit.ModName,
        CurrentVersion = DrakeRenameit.Version,
        MinimumRequiredVersion = DrakeRenameit.Version,
    };

    private static ConfigEntry<bool> _lockToOwner;
    private static ConfigEntry<bool> _editDescriptionsEnable;
    private static ConfigEntry<bool> _nameClaimsOwner;
    private static ConfigEntry<bool> _seperateStacks;
    private static ConfigEntry<int> _nameCharLimit;
    private static ConfigEntry<int> _descCharLimit;

    public static bool LockToOwner => _lockToOwner.Value;
    public static int DescCharLimit => _descCharLimit.Value;
    public static bool NameClaimsOwner => _nameClaimsOwner.Value;
    public static bool EditDescriptionsEnabled => _editDescriptionsEnable.Value;
    public static int NameCharLimit => _nameCharLimit.Value;

    /*public static bool SeperateStacks => _seperateStacks.Value;*/

    public static void Bind(ConfigFile config)
    {
        // Example: Lock renames to item owner
        _lockToOwner = config.BindSynced(
            SectionGeneral,
            "LockToOwner",
            true,
            "If true, only the crafter can rename the item.",
            sync: true
        );
        
        // Example: First rename attempt claims ownership
        _nameClaimsOwner = config.BindSynced(
            SectionGeneral,
            "NameClaimsOwner",
            true,
            "If true, renaming an unowned item assigns ownership to the renamer. Used in conjunction with LockToOwner, when you rename an unclaimed item, you will have laid claim to it.",
            sync: true
        );

        _editDescriptionsEnable = config.BindSynced(
            SectionGeneral,
            "EditDescriptionsEnabled",
            true,
            "If enabled, allows players to also edit descriptions of items. Could be turned off preplace items with descriptions.",
            sync: true
        );

        
        // Example: Lock renames to item owner
        _nameCharLimit = config.BindSynced(
            SectionGeneral,
            "NameCharacterLimit",
            50,
            "Defines the limit for max characters in rename, be sure to account for <color=> tag codes etc.",
            sync: true
        );
        
        _descCharLimit = config.BindSynced(
            SectionGeneral,
            "DescriptionCharacterLimit",
            1000,
            "Defines the limit for max characters description, be sure to account for <color=> tag codes etc.",
            sync: true
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
