using System;
using System.Collections;
using System.Reflection;
using DrakeModsLibs.Compat;
using HarmonyLib;
using UnityEngine;

namespace DrakeRenameit.Compat.Vanilla;

/// <summary>Baseline vanilla <see cref="PrivateArea"/> stack. Always active when members resolve.</summary>
internal sealed class VanillaPrivateAreaModule : IAreaCompatModule
{
    public const string ModuleId = "VanillaPrivateArea";

    private FieldInfo? _allAreasField;
    private MethodInfo? _isEnabled;
    private MethodInfo? _isInside;
    private bool _resolved;

    public string Id => ModuleId;
    public string? SoftDependencyGuid => null;
    public int Priority => CompatPriority.Vanilla;
    public bool IsActive { get; private set; }

    public bool TryActivate()
    {
        EnsureResolved();
        IsActive = _allAreasField != null && _isEnabled != null && _isInside != null;
        if (!IsActive)
            RenameitConfig.Log?.LogError("Compat VanillaPrivateArea: failed to resolve PrivateArea members.");
        return IsActive;
    }

    public void ApplyHarmonyPatches(Harmony harmony)
    {
    }

    public bool IsInsideEnabledWard(Vector3 position)
    {
        var areas = GetAllAreas();
        if (areas == null || areas.Count == 0)
            return false;

        for (var i = 0; i < areas.Count; i++)
        {
            if (areas[i] is not PrivateArea area)
                continue;
            if (!InvokeBool(_isEnabled, area))
                continue;
            if (InvokeBool(_isInside, area, position, 0f))
                return true;
        }

        return false;
    }

    public AreaCoverageKind QueryLocalAccess(Vector3 position, bool flash)
    {
        if (!IsInsideEnabledWard(position))
            return AreaCoverageKind.Unrelated;

        try
        {
            return PrivateArea.CheckAccess(position, 0f, flash, wardCheck: false)
                ? AreaCoverageKind.Allowed
                : AreaCoverageKind.Denied;
        }
        catch (Exception ex)
        {
            RenameitConfig.Log?.LogDebug($"Compat VanillaPrivateArea CheckAccess: {ex.Message}");
            return AreaCoverageKind.Denied;
        }
    }

    private void EnsureResolved()
    {
        if (_resolved)
            return;

        _resolved = true;
        var t = typeof(PrivateArea);
        _allAreasField = AccessTools.Field(t, "m_allAreas");
        _isEnabled = AccessTools.Method(t, "IsEnabled");
        _isInside = AccessTools.Method(t, "IsInside", new[] { typeof(Vector3), typeof(float) });
    }

    private IList? GetAllAreas()
    {
        if (_allAreasField == null)
            return null;

        try
        {
            return _allAreasField.GetValue(null) as IList;
        }
        catch (Exception ex)
        {
            RenameitConfig.Log?.LogDebug($"Compat VanillaPrivateArea m_allAreas: {ex.Message}");
            return null;
        }
    }

    private static bool InvokeBool(MethodInfo? method, object target, params object[] args)
    {
        if (method == null || target == null)
            return false;

        try
        {
            return method.Invoke(target, args) is true;
        }
        catch (Exception ex)
        {
            RenameitConfig.Log?.LogDebug($"Compat VanillaPrivateArea {method.Name}: {ex.Message}");
            return false;
        }
    }
}
