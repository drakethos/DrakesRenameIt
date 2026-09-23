namespace DrakeRenameit.Compat;

/// <summary>How a ward module relates to one world position.</summary>
internal enum AreaCoverageKind
{
    /// <summary>This module's wards do not cover the position.</summary>
    Unrelated = 0,

    /// <summary>A covering ward allows the local player.</summary>
    Allowed = 1,

    /// <summary>A covering ward denies the local player.</summary>
    Denied = 2
}
