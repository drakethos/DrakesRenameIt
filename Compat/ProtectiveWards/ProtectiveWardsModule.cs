using System;
using System.Reflection;
using DrakeModsLibs.Compat;
using HarmonyLib;
using UnityEngine;

namespace DrakeRenameit.Compat.ProtectiveWards;

/// <summary>
/// Soft ProtectiveWards stack. Coverage for paper take/edit; ItemStand Interact
/// gated so take-public Written Pages remain removable by visitors.
/// </summary>
internal sealed class ProtectiveWardsModule : IAreaCompatModule
{
    public const string ModuleId = "ProtectiveWards";
    public const string PluginGuid = "shudnal.ProtectiveWards";

    private const string PwTypeName = "ProtectiveWards.ProtectiveWards";
    private const string FullProtectionTypeName = "ProtectiveWards.FullProtection";

    private MethodInfo? _insideEnabledPlayersArea;
    private MethodInfo? _hasAccessPlayer;
    private MethodInfo? _findProtectedWard;
    private bool _typesOk;
    private bool _patchesApplied;

    private static MethodInfo? _pwItemStandBlock;
    private static MethodInfo? _getAttachedItem;
    private static MethodInfo? _loadFromZdo;
    private static bool _attachApisResolved;
    private static bool _loggedTakePublicAllow;
    private static bool _loggedAllowFault;

    public string Id => ModuleId;
    public string? SoftDependencyGuid => PluginGuid;
    public int Priority => CompatPriority.BuiltIn;
    public bool IsActive { get; private set; }

    public bool TryActivate()
    {
        ResolveTypes();
        IsActive = _typesOk;
        return IsActive;
    }

    public void ApplyHarmonyPatches(Harmony harmony)
    {
        if (!IsActive || harmony == null || _patchesApplied)
            return;

        _patchesApplied = true;

        _pwItemStandBlock = FindFullProtectionNestedPrefix("ItemStand_Interact_PreventUnauthorizedAccess");
        SwapPrefix(
            harmony,
            AccessTools.Method(typeof(ItemStand), nameof(ItemStand.Interact), new[] { typeof(Humanoid), typeof(bool), typeof(bool) }),
            _pwItemStandBlock,
            nameof(ItemStandInteractGate));
    }

    public bool IsInsideEnabledWard(Vector3 position)
    {
        if (!IsActive || _insideEnabledPlayersArea == null)
            return false;

        try
        {
            return _insideEnabledPlayersArea.Invoke(null, new object[] { position, false }) is true;
        }
        catch (Exception ex)
        {
            RenameitConfig.Log?.LogDebug($"Compat ProtectiveWards IsInsideWard: {ex.Message}");
            return false;
        }
    }

    public AreaCoverageKind QueryLocalAccess(Vector3 position, bool flash)
    {
        if (!IsActive || !IsInsideEnabledWard(position))
            return AreaCoverageKind.Unrelated;

        try
        {
            PrivateArea? area = null;
            if (_findProtectedWard != null
                && _findProtectedWard.Invoke(null, new object[] { position }) is PrivateArea found
                && found)
                area = found;

            var player = Player.m_localPlayer;
            if (area && player && _hasAccessPlayer != null
                && _hasAccessPlayer.Invoke(null, new object[] { area, player }) is bool ok)
                return ok ? AreaCoverageKind.Allowed : AreaCoverageKind.Denied;

            return PrivateArea.CheckAccess(position, 0f, flash, wardCheck: false)
                ? AreaCoverageKind.Allowed
                : AreaCoverageKind.Denied;
        }
        catch (Exception ex)
        {
            RenameitConfig.Log?.LogDebug($"Compat ProtectiveWards CheckAccess: {ex.Message}");
            return AreaCoverageKind.Denied;
        }
    }

    /// <summary>
    /// Take-public Written Page on a stand skips PW's ItemStand block.
    /// Propagates <c>__state</c> so PW's Finalizer still clears CheckAccess bypass.
    /// </summary>
    private static bool ItemStandInteractGate(
        ItemStand __instance,
        Humanoid user,
        bool hold,
        bool alt,
        ref bool __result,
        ref bool __state)
    {
        if (SafeIsTakePublicPaper(__instance))
        {
            if (!_loggedTakePublicAllow)
            {
                _loggedTakePublicAllow = true;
                RenameitConfig.Log?.LogInfo(
                    "Compat ProtectiveWards: take-public paper bypassed PW ItemStand interact block.");
            }

            return true;
        }

        return CallPwInteractPrefix(_pwItemStandBlock, __instance, user, hold, alt, ref __result, ref __state);
    }

    private static bool CallPwInteractPrefix(
        MethodInfo? pw,
        ItemStand stand,
        Humanoid user,
        bool hold,
        bool alt,
        ref bool __result,
        ref bool __state)
    {
        if (pw == null || !stand)
            return true;

        try
        {
            // PW signature: (ItemStand, Humanoid, bool hold, bool alt, ref bool __result, ref bool __state)
            var args = new object[] { stand, user, hold, alt, __result, __state };
            var cont = pw.Invoke(null, args) is not false;
            __result = args[4] is true;
            __state = args[5] is true;
            return cont;
        }
        catch (Exception ex)
        {
            RenameitConfig.Log?.LogDebug($"Compat ProtectiveWards ItemStand invoke: {ex.Message}");
            return true;
        }
    }

    private static bool SafeIsTakePublicPaper(ItemStand stand)
    {
        try
        {
            return IsTakePublicPaperStand(stand);
        }
        catch (Exception ex)
        {
            if (!_loggedAllowFault)
            {
                _loggedAllowFault = true;
                RenameitConfig.Log?.LogWarning($"Compat ProtectiveWards take-public check failed: {ex.Message}");
            }

            return false;
        }
    }

    private static bool IsTakePublicPaperStand(ItemStand stand)
    {
        if (!stand || !RenameitConfig.PaperTakePublicEnabled)
            return false;

        if (!stand.HaveAttachment() || !stand.m_canBeRemoved)
            return false;

        var item = TryLoadAttached(stand);
        if (!PaperItem.IsWrittenLike(item))
            return false;

        PaperItemStyle.Read(item, out _, out _, out var takePublic);
        return takePublic;
    }

    /// <summary>Same resolve path as <see cref="Patches.PaperItemStandPatches"/> — keep defensive.</summary>
    private static ItemDrop.ItemData? TryLoadAttached(ItemStand stand)
    {
        if (!stand || ObjectDB.instance == null)
            return null;

        EnsureAttachApis();

        try
        {
            if (_getAttachedItem != null)
            {
                object? result = _getAttachedItem.Invoke(stand, null);
                GameObject? prefab = result switch
                {
                    int hash when hash != 0 => ObjectDB.instance.GetItemPrefab(hash),
                    string name when !string.IsNullOrEmpty(name) => ObjectDB.instance.GetItemPrefab(name),
                    _ => null,
                };

                if (prefab != null)
                {
                    var proto = prefab.GetComponent<ItemDrop>()?.m_itemData;
                    if (proto != null)
                    {
                        var clone = proto.Clone();
                        var zdo = stand.GetComponent<ZNetView>()?.GetZDO();
                        if (zdo != null)
                            TryLoadItemDataFromZdo(clone, zdo);
                        return clone;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            RenameitConfig.Log?.LogDebug($"Compat ProtectiveWards attach load: {ex.Message}");
        }

        var container = stand.GetComponent<Container>();
        var items = container?.GetInventory()?.GetAllItems();
        if (items == null || items.Count == 0)
            return null;
        return items[0];
    }

    private static void TryLoadItemDataFromZdo(ItemDrop.ItemData item, ZDO zdo)
    {
        if (_loadFromZdo == null)
            return;

        try
        {
            var parms = _loadFromZdo.GetParameters();
            if (parms.Length == 2)
                _loadFromZdo.Invoke(null, new object[] { item, zdo });
            else if (parms.Length == 3)
                _loadFromZdo.Invoke(null, new object[] { item, zdo, -1 });
        }
        catch
        {
            /* keep prefab defaults */
        }
    }

    /// <summary>
    /// Resolve attach helpers without <c>AccessTools.Method</c> so missing
    /// signatures do not spam HarmonyX warnings on every Interact/hover.
    /// </summary>
    private static void EnsureAttachApis()
    {
        if (_attachApisResolved)
            return;

        _attachApisResolved = true;
        try
        {
            _getAttachedItem = typeof(ItemStand).GetMethod(
                "GetAttachedItem",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            foreach (var method in typeof(ItemDrop).GetMethods(
                         BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (method.Name != "LoadFromZDO")
                    continue;

                var parms = method.GetParameters();
                if (parms.Length == 2
                    && parms[0].ParameterType == typeof(ItemDrop.ItemData)
                    && parms[1].ParameterType == typeof(ZDO))
                {
                    _loadFromZdo = method;
                    break;
                }

                if (parms.Length == 3
                    && parms[0].ParameterType == typeof(ItemDrop.ItemData)
                    && parms[1].ParameterType == typeof(ZDO)
                    && parms[2].ParameterType == typeof(int)
                    && _loadFromZdo == null)
                {
                    _loadFromZdo = method;
                }
            }
        }
        catch (Exception ex)
        {
            RenameitConfig.Log?.LogDebug($"Compat ProtectiveWards attach API resolve: {ex.Message}");
        }
    }

    private static MethodInfo? FindFullProtectionNestedPrefix(string nestedTypeName)
    {
        var outer = AccessTools.TypeByName(FullProtectionTypeName);
        if (outer == null)
        {
            RenameitConfig.Log?.LogWarning($"Compat ProtectiveWards: {FullProtectionTypeName} not found.");
            return null;
        }

        var nested = outer.GetNestedType(nestedTypeName, BindingFlags.Public | BindingFlags.NonPublic);
        var method = nested == null ? null : AccessTools.Method(nested, "Prefix");
        if (method == null)
            RenameitConfig.Log?.LogWarning($"Compat ProtectiveWards: {nestedTypeName}.Prefix not found.");
        return method;
    }

    private static void SwapPrefix(Harmony harmony, MethodInfo? original, MethodInfo? pwPatch, string ourName)
    {
        if (!TryRemovePw(harmony, original, pwPatch, ourName))
            return;

        var ours = AccessTools.Method(typeof(ProtectiveWardsModule), ourName);
        if (ours == null || original == null)
            return;

        harmony.Patch(original, prefix: new HarmonyMethod(ours) { priority = HarmonyLib.Priority.First });
    }

    private static bool TryRemovePw(Harmony harmony, MethodInfo? original, MethodInfo? pwPatch, string ourName)
    {
        if (original == null || pwPatch == null)
        {
            RenameitConfig.Log?.LogWarning($"Compat ProtectiveWards: cannot gate {ourName} (missing method).");
            return false;
        }

        var present = ListsPw(original, pwPatch);
        try
        {
            if (present)
                harmony.Unpatch(original, pwPatch);
        }
        catch (Exception ex)
        {
            RenameitConfig.Log?.LogWarning($"Compat ProtectiveWards unpatch {ourName}: {ex.Message}");
        }

        var still = ListsPw(original, pwPatch);
        RenameitConfig.Log?.LogInfo(
            $"Compat ProtectiveWards {ourName}: PW patch {(present ? "found" : "absent")}, still attached={still}.");
        return true;
    }

    private static bool ListsPw(MethodBase original, MethodInfo pwPatch)
    {
        var info = Harmony.GetPatchInfo(original);
        if (info == null)
            return false;

        foreach (var entry in info.Prefixes)
        {
            if (SameMethod(entry.PatchMethod, pwPatch))
                return true;
        }

        return false;
    }

    private static bool SameMethod(MethodInfo? candidate, MethodInfo expected)
    {
        if (candidate == null)
            return false;
        if (candidate == expected)
            return true;
        return candidate.Name == expected.Name
               && candidate.DeclaringType?.FullName == expected.DeclaringType?.FullName;
    }

    private void ResolveTypes()
    {
        if (_typesOk || _insideEnabledPlayersArea != null)
            return;

        try
        {
            var pwType = AccessTools.TypeByName(PwTypeName);
            if (pwType == null)
                return;

            _insideEnabledPlayersArea = AccessTools.Method(
                pwType,
                "InsideEnabledPlayersArea",
                new[] { typeof(Vector3), typeof(bool) });
            _hasAccessPlayer = AccessTools.Method(
                pwType,
                "HasAccessToWardOrConnectedWard",
                new[] { typeof(PrivateArea), typeof(Player) });
            _findProtectedWard = AccessTools.Method(pwType, "FindProtectedWard", new[] { typeof(Vector3) });

            _typesOk = _insideEnabledPlayersArea != null
                       && (_hasAccessPlayer != null || _findProtectedWard != null);
        }
        catch (Exception ex)
        {
            _typesOk = false;
            RenameitConfig.Log?.LogDebug($"Compat ProtectiveWards resolve: {ex.Message}");
        }
    }
}
