using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace DrakeRenameit.Compat;

/// <summary>
/// Shared local ward-deny probe for hover text. Prefers LockSmith when present
/// (covers WardIsLove / ProtectiveWards / vanilla via LockSmith's modules);
/// otherwise uses RenameIt's <see cref="CompatibilityManager"/>.
/// </summary>
internal static class WardHoverAccess
{
    public const string LockSmithGuid = "com.drakesworkshop.locksmith";

    private static bool _resolved;
    private static MethodInfo? _isReady;
    private static MethodInfo? _hasLocalWardAccess;
    private static bool _loggedLockSmithFault;

    /// <summary>True when the local player is denied by a covering ward.</summary>
    public static bool IsLocalDenied(Vector3 position)
    {
        if (TryLockSmithDenied(position, out var denied))
            return denied;

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

    private static bool TryLockSmithDenied(Vector3 position, out bool denied)
    {
        denied = false;
        EnsureResolved();
        if (_isReady == null || _hasLocalWardAccess == null)
            return false;

        try
        {
            if (_isReady.Invoke(null, null) is not true)
                return false;

            denied = _hasLocalWardAccess.Invoke(null, new object[] { position }) is not true;
            return true;
        }
        catch (Exception ex)
        {
            if (!_loggedLockSmithFault)
            {
                _loggedLockSmithFault = true;
                RenameitConfig.Log?.LogDebug($"Compat WardHover LockSmith: {ex.Message}");
            }

            return false;
        }
    }

    private static void EnsureResolved()
    {
        if (_resolved)
            return;

        _resolved = true;
        try
        {
            var api = AccessTools.TypeByName("LockSmith.API.LockSmithCompatApi");
            if (api == null)
                return;

            _isReady = AccessTools.PropertyGetter(api, "IsReady")
                       ?? AccessTools.Method(api, "get_IsReady");
            _hasLocalWardAccess = AccessTools.Method(api, "HasLocalWardAccess", new[] { typeof(Vector3) });
        }
        catch (Exception ex)
        {
            RenameitConfig.Log?.LogDebug($"Compat WardHover resolve: {ex.Message}");
        }
    }
}
