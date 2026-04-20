namespace DrakeRenameit.Permissions;

/// <summary>Fixed alias tokens for <see cref="RenameitConfig.ExcludedCategory"/> (lowercase keys match <see cref="RenameExclusionRules"/>).</summary>
public static class ExcludedCategoryAliases
{
    /// <summary>All alias keys accepted in addition to enum names. Keep in sync with <see cref="RenameExclusionRules"/>.</summary>
    public static readonly string[] AllKeys =
    {
        "armor",
        "weapons",
        "tools",
        "ranged",
        "melee",
        "shields",
        "ammo",
        "fish"
    };
}
