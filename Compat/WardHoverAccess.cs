using System;
using UnityEngine;

namespace DrakeRenameit.Compat;

/// <summary>
/// Shared local ward-deny probe for hover text, via RenameIt's own <see cref="CompatibilityManager"/>
/// (WardIsLove / ProtectiveWards / ArcaneWard / vanilla). Legacy has no other-Drakes-mod integration,
/// so this does not probe LockSmith.
/// </summary>
internal static class WardHoverAccess
{
    /// <summary>True when the local player is denied by a covering ward.</summary>
    public static bool IsLocalDenied(Vector3 position)
    {
        try
        {
            return !CompatibilityManager.CheckAccess(position, flash: false);
        }
        catch (Exception ex)
        {
            RenameitConfig.Log?.LogDebug($"Compat WardHover CheckAccess: {ex.Message}");
            return false;
        }
    }
}
