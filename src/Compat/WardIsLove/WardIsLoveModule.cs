using System;
using System.Reflection;
using DrakeModsLibs.Compat;
using HarmonyLib;
using UnityEngine;

namespace DrakeRenameit.Compat.WardIsLove;

/// <summary>
/// Soft WardIsLove stack (<c>WardMonoscript</c>). Absent → module not registered.
/// Item-stand hover is <see cref="ItemStand.ItemStandHoverModule"/>, not this module.
/// </summary>
internal sealed class WardIsLoveModule : IAreaCompatModule
{
    public const string ModuleId = "WardIsLove";
    public const string PluginGuid = "Azumatt.WardIsLove";

    private MethodInfo? _apiIsLoaded;
    private MethodInfo? _apiIsInsideWard;
    private MethodInfo? _wardCheckAccess;
    private MethodInfo? _checkInWardMonoscript;
    private bool _typesOk;

    public string Id => ModuleId;
    public string? SoftDependencyGuid => PluginGuid;
    public int Priority => CompatPriority.BuiltIn;
    public bool IsActive { get; private set; }

    public bool TryActivate()
    {
        ResolveTypes();
        if (!_typesOk)
            return false;

        try
        {
            IsActive = _apiIsLoaded?.Invoke(null, null) is true;
        }
        catch (Exception ex)
        {
            IsActive = false;
            RenameitConfig.Log?.LogDebug($"Compat WardIsLove IsLoaded: {ex.Message}");
        }

        return IsActive;
    }

    public void ApplyHarmonyPatches(Harmony harmony)
    {
    }

    public bool IsInsideEnabledWard(Vector3 position)
    {
        if (!IsActive)
            return false;

        try
        {
            if (_apiIsInsideWard?.Invoke(null, new object[] { position }) is true)
                return true;
            return _checkInWardMonoscript?.Invoke(null, new object[] { position, false }) is true;
        }
        catch (Exception ex)
        {
            RenameitConfig.Log?.LogDebug($"Compat WardIsLove IsInsideWard: {ex.Message}");
            return false;
        }
    }

    public AreaCoverageKind QueryLocalAccess(Vector3 position, bool flash)
    {
        if (!IsActive || !IsInsideEnabledWard(position))
            return AreaCoverageKind.Unrelated;

        try
        {
            if (_wardCheckAccess != null
                && _wardCheckAccess.Invoke(null, new object[] { position, 0f, flash, false }) is bool ok)
                return ok ? AreaCoverageKind.Allowed : AreaCoverageKind.Denied;
        }
        catch (Exception ex)
        {
            RenameitConfig.Log?.LogDebug($"Compat WardIsLove CheckAccess: {ex.Message}");
        }

        return AreaCoverageKind.Denied;
    }

    private void ResolveTypes()
    {
        if (_typesOk || _apiIsLoaded != null)
            return;

        try
        {
            var apiType = AccessTools.TypeByName("WardIsLove.API.API")
                           ?? AccessTools.TypeByName("WardIsLove.API");
            var wardType = AccessTools.TypeByName("WardIsLove.Util.WardMonoscript");
            if (apiType == null || wardType == null)
                return;

            _apiIsLoaded = AccessTools.Method(apiType, "IsLoaded", Type.EmptyTypes);
            _apiIsInsideWard = AccessTools.Method(apiType, "IsInsideWard", new[] { typeof(Vector3) });
            _wardCheckAccess = AccessTools.Method(
                wardType,
                "CheckAccess",
                new[] { typeof(Vector3), typeof(float), typeof(bool), typeof(bool) });
            _checkInWardMonoscript = AccessTools.Method(
                wardType,
                "CheckInWardMonoscript",
                new[] { typeof(Vector3), typeof(bool) });

            _typesOk = _apiIsLoaded != null
                       && (_apiIsInsideWard != null || _checkInWardMonoscript != null)
                       && _wardCheckAccess != null;

            if (_typesOk)
                RenameitConfig.Log?.LogInfo("Compat WardIsLove: soft access API resolved.");
        }
        catch (Exception ex)
        {
            _typesOk = false;
            RenameitConfig.Log?.LogDebug($"Compat WardIsLove resolve: {ex.Message}");
        }
    }
}
