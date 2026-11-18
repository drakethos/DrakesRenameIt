using System;
using DrakeModsLibs.API;
using DrakeModsLibs.Compat;
using HarmonyLib;
using UnityEngine;

namespace DrakeRenameit.Compat.ItemLibs;

/// <summary>
/// Keeps DrakeModsLibs item-stand labels when WardIsLove replaces hover text.
/// WardIsLove 4.0.4: <c>WardIsLove.PatchClasses.ShowWardLockOnItemStandHover.Postfix(string, ItemStand)</c>
/// returns <c>m_name</c> (the piece, "Item stand") plus a red <c>$piece_noaccess</c> line.
/// WardIsLove 3.5.x used <c>ItemStandGetHoverTextPatch</c> with the same shape.
/// The target method is static, so the stand is an argument — not Harmony's <c>__instance</c>.
/// Interact stays blocked — this module never patches stand Interact.
/// </summary>
internal sealed class ItemStandHoverModule : ICompatModule
{
    public const string ModuleId = "ItemStandHover";

    /// <summary>
    /// Newest first. Probed without <c>AccessTools.TypeByName</c> so a missing older name does not log a Harmony warning.
    /// </summary>
    private static readonly string[] HoverPatchTypeNames =
    {
        "WardIsLove.PatchClasses.ShowWardLockOnItemStandHover",
        "WardIsLove.PatchClasses.ItemStandGetHoverTextPatch",
    };

    private bool _patchesApplied;
    private Type? _hoverPatchType;

    public string Id => ModuleId;
    public string? SoftDependencyGuid => WardIsLove.WardIsLoveModule.PluginGuid;
    public int Priority => CompatPriority.BuiltIn;
    public bool IsActive { get; private set; }

    public bool TryActivate()
    {
        _hoverPatchType = FindHoverPatchType();
        var wilLoaded = WilApiReportsLoaded();
        if (_hoverPatchType == null && wilLoaded)
        {
            RenameitConfig.Log?.LogWarning(
                "Compat ItemStand: WardIsLove is loaded but no item-stand hover patch type was found.");
        }

        IsActive = _hoverPatchType != null && wilLoaded;
        return IsActive;
    }

    public void ApplyHarmonyPatches(Harmony harmony)
    {
        if (!IsActive || harmony == null || _patchesApplied || _hoverPatchType == null)
            return;

        _patchesApplied = true;
        if (TryPatchPostfix(harmony, _hoverPatchType, "Postfix", nameof(KeepStandNamePostfix)))
        {
            RenameitConfig.Log?.LogInfo(
                "Compat ItemStand: WardIsLove hover patch applied (" + _hoverPatchType.Name + ").");
        }
    }

    /// <summary>
    /// Runs after WardIsLove builds the denied line. WIL still decides access
    /// (including per-ward item-stand interact). When show-name-on-wards is on,
    /// replace that piece-name line with the item label plus No access.
    /// <c>__result</c> is WIL's return value. The stand is <c>__args</c>, because
    /// a <c>ItemStand __instance</c> parameter on this static method is always null.
    /// </summary>
    private static void KeepStandNamePostfix(object[] __args, ref string __result)
    {
        try
        {
            if (!RenameitConfig.ShowItemStandItemNameWhenNoAccess)
                return;

            if (!HoverHasNoAccess(__result))
                return;

            var stand = StandFromArgs(__args);
            if (!stand)
                return;

            var composed = ComposeDeniedHover(stand);
            if (string.IsNullOrEmpty(composed))
                return;

            __result = composed;
        }
        catch (Exception ex)
        {
            RenameitConfig.Log?.LogDebug($"Compat ItemStand hover: {ex.Message}");
        }
    }

    /// <summary>
    /// Item label plus WIL's red No access line. Empty when there is no item label,
    /// so an empty stand keeps the piece name WardIsLove already wrote.
    /// </summary>
    private static string ComposeDeniedHover(ItemStand stand)
    {
        string label;
        try
        {
            label = CustomizeLibsAPI.GetItemStandHoverLabel(stand);
        }
        catch (Exception ex)
        {
            RenameitConfig.Log?.LogDebug($"Compat ItemStand label: {ex.Message}");
            return "";
        }

        if (string.IsNullOrWhiteSpace(label) || LabelIsPieceName(stand, label))
            return "";

        var denied = Localize("\n<color=#FF0000>$piece_noaccess</color>");
        return label + denied;
    }

    private static bool LabelIsPieceName(ItemStand stand, string label)
    {
        var piece = stand.m_name;
        if (string.IsNullOrEmpty(piece))
            return false;

        if (string.Equals(label, piece, StringComparison.OrdinalIgnoreCase))
            return true;

        var localized = Localize(piece);
        return !string.IsNullOrEmpty(localized)
               && string.Equals(label, localized, StringComparison.OrdinalIgnoreCase);
    }

    private static bool HoverHasNoAccess(string? hoverText)
    {
        if (string.IsNullOrEmpty(hoverText))
            return false;

        const string token = "$piece_noaccess";
        if (hoverText!.Contains(token))
            return true;

        if (Localization.instance == null)
            return false;

        var localized = Localization.instance.Localize(token);
        return !string.IsNullOrEmpty(localized)
               && hoverText.IndexOf(localized, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static ItemStand? StandFromArgs(object[]? args)
    {
        if (args == null)
            return null;

        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] is ItemStand stand)
                return stand;
        }

        return null;
    }

    private static Type? FindHoverPatchType()
    {
        for (var i = 0; i < HoverPatchTypeNames.Length; i++)
        {
            var type = FindTypeSilent(HoverPatchTypeNames[i]);
            if (type != null)
                return type;
        }

        return null;
    }

    private static Type? FindTypeSilent(string fullName)
    {
        var assemblies = AppDomain.CurrentDomain.GetAssemblies();
        for (var i = 0; i < assemblies.Length; i++)
        {
            try
            {
                var type = assemblies[i].GetType(fullName, throwOnError: false);
                if (type != null)
                    return type;
            }
            catch (Exception)
            {
                /* dynamic or reflection-only assemblies */
            }
        }

        return null;
    }

    private static string Localize(string text) =>
        Localization.instance != null ? Localization.instance.Localize(text) : text;

    private static bool WilApiReportsLoaded()
    {
        try
        {
            var apiType = AccessTools.TypeByName("WardIsLove.API.API")
                           ?? AccessTools.TypeByName("WardIsLove.API");
            var isLoaded = apiType == null ? null : AccessTools.Method(apiType, "IsLoaded", Type.EmptyTypes);
            return isLoaded?.Invoke(null, null) is true;
        }
        catch (Exception ex)
        {
            RenameitConfig.Log?.LogDebug($"Compat ItemStand WIL probe: {ex.Message}");
            return false;
        }
    }

    private static bool TryPatchPostfix(Harmony harmony, Type type, string methodName, string patchMethodName)
    {
        try
        {
            var target = AccessTools.Method(type, methodName);
            if (target == null)
            {
                RenameitConfig.Log?.LogWarning($"Compat ItemStand: {type.FullName}.{methodName} not found.");
                return false;
            }

            var postfix = AccessTools.Method(typeof(ItemStandHoverModule), patchMethodName);
            if (postfix == null)
                return false;

            harmony.Patch(target, postfix: new HarmonyMethod(postfix));
            return true;
        }
        catch (Exception ex)
        {
            RenameitConfig.Log?.LogWarning($"Compat ItemStand patch {type.FullName}.{methodName}: {ex.Message}");
            return false;
        }
    }
}
