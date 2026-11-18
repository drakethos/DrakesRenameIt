using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using DrakeModsLibs.Compat;
using DrakeRenameit.Paper.Items;
using HarmonyLib;
using UnityEngine;

namespace DrakeRenameit.Compat.ArcaneWard;

/// <summary>
/// Soft KG Arcane Ward. Coverage so paper / stand hover deny is correct
/// (Arcane does not patch PrivateArea.CheckAccess). ItemStand Interact gated
/// for take-public Written Pages.
/// </summary>
internal sealed class ArcaneWardModule : IAreaCompatModule
{
    public const string ModuleId = "ArcaneWard";
    public const string PluginGuid = "kg.ArcaneWard";

    private const string ComponentTypeName = "kg_ArcaneWard.ArcaneWardComponent";
    private const string PatchesTypeName = "kg_ArcaneWard.WardProtectionPatches";
    private const string ProtectionTypeName = "kg_ArcaneWard.Protection";

    private FieldInfo? _instancesField;
    private MethodInfo? _checkFlag;
    private PropertyInfo? _isEnabled;
    private PropertyInfo? _radius;
    private object? _protectionItemStand;
    private object? _protectionDoor;
    private bool _typesOk;
    private bool _patchesApplied;

    private static MethodInfo? _awItemStandBlock;
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

        _awItemStandBlock = FindNestedPrefix("ItemStand_Interact_Patch");
        SwapPrefix(
            harmony,
            AccessTools.Method(typeof(ItemStand), nameof(ItemStand.Interact), new[] { typeof(Humanoid), typeof(bool), typeof(bool) }),
            _awItemStandBlock,
            nameof(ItemStandInteractGate));
    }

    public bool IsInsideEnabledWard(Vector3 position)
    {
        if (!IsActive)
            return false;

        try
        {
            foreach (var instance in EnumerateInstances())
            {
                if (!IsEnabledInstance(instance))
                    continue;
                if (IsPointInside(instance, position))
                    return true;
            }
        }
        catch (Exception ex)
        {
            RenameitConfig.Log?.LogDebug($"Compat ArcaneWard IsInsideWard: {ex.Message}");
        }

        return false;
    }

    public AreaCoverageKind QueryLocalAccess(Vector3 position, bool flash)
    {
        if (!IsActive || !IsInsideEnabledWard(position))
            return AreaCoverageKind.Unrelated;

        try
        {
            // Prefer Door flag for generic access; ItemStand flag if Door enum missing.
            var flag = _protectionDoor ?? _protectionItemStand;
            if (_checkFlag != null && flag != null
                && _checkFlag.Invoke(null, new object[] { position, true, flag, flash }) is true)
                return AreaCoverageKind.Denied;

            return AreaCoverageKind.Allowed;
        }
        catch (Exception ex)
        {
            RenameitConfig.Log?.LogDebug($"Compat ArcaneWard CheckFlag: {ex.Message}");
            return AreaCoverageKind.Denied;
        }
    }

    private static bool ItemStandInteractGate(ItemStand __instance, Humanoid user, bool hold, bool alt)
    {
        _ = user;
        _ = hold;
        _ = alt;

        if (SafeIsTakePublicPaper(__instance))
        {
            if (!_loggedTakePublicAllow)
            {
                _loggedTakePublicAllow = true;
                RenameitConfig.Log?.LogInfo(
                    "Compat ArcaneWard: take-public paper bypassed Arcane ItemStand interact block.");
            }

            return true;
        }

        return CallAwBool(_awItemStandBlock, __instance);
    }

    private static bool CallAwBool(MethodInfo? aw, ItemStand stand)
    {
        if (aw == null || !stand)
            return true;

        try
        {
            return aw.Invoke(null, new object[] { stand }) is not false;
        }
        catch (Exception ex)
        {
            RenameitConfig.Log?.LogDebug($"Compat ArcaneWard ItemStand invoke: {ex.Message}");
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
                RenameitConfig.Log?.LogWarning($"Compat ArcaneWard take-public check failed: {ex.Message}");
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
            RenameitConfig.Log?.LogDebug($"Compat ArcaneWard attach load: {ex.Message}");
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
            RenameitConfig.Log?.LogDebug($"Compat ArcaneWard attach API resolve: {ex.Message}");
        }
    }

    private IEnumerable<object> EnumerateInstances()
    {
        if (_instancesField == null)
            yield break;

        object? raw;
        try
        {
            raw = _instancesField.GetValue(null);
        }
        catch
        {
            yield break;
        }

        if (raw is not IEnumerable list)
            yield break;

        foreach (var entry in list)
        {
            if (entry == null)
                continue;
            if (entry is UnityEngine.Object uo && !uo)
                continue;
            yield return entry;
        }
    }

    private bool IsEnabledInstance(object instance)
    {
        try
        {
            return _isEnabled?.GetValue(instance) is true;
        }
        catch
        {
            return false;
        }
    }

    private bool IsPointInside(object instance, Vector3 point)
    {
        try
        {
            if (instance is not Component component || !component)
                return false;

            var radius = 0;
            if (_radius?.GetValue(instance) is int r)
                radius = r;
            else if (_radius?.GetValue(instance) is float f)
                radius = Mathf.RoundToInt(f);

            return Vector3.Distance(point, component.transform.position) <= radius;
        }
        catch
        {
            return false;
        }
    }

    private static MethodInfo? FindNestedPrefix(string nestedTypeName)
    {
        var outer = AccessTools.TypeByName(PatchesTypeName);
        if (outer == null)
        {
            RenameitConfig.Log?.LogWarning($"Compat ArcaneWard: {PatchesTypeName} not found.");
            return null;
        }

        var nested = outer.GetNestedType(nestedTypeName, BindingFlags.Public | BindingFlags.NonPublic);
        var method = nested == null ? null : AccessTools.Method(nested, "Prefix");
        if (method == null)
            RenameitConfig.Log?.LogWarning($"Compat ArcaneWard: {nestedTypeName}.Prefix not found.");
        return method;
    }

    private static void SwapPrefix(Harmony harmony, MethodInfo? original, MethodInfo? awPatch, string ourName)
    {
        if (!TryRemoveAw(harmony, original, awPatch, ourName))
            return;

        var ours = AccessTools.Method(typeof(ArcaneWardModule), ourName);
        if (ours == null || original == null)
            return;

        harmony.Patch(original, prefix: new HarmonyMethod(ours) { priority = HarmonyLib.Priority.First });
    }

    private static bool TryRemoveAw(Harmony harmony, MethodInfo? original, MethodInfo? awPatch, string ourName)
    {
        if (original == null || awPatch == null)
        {
            RenameitConfig.Log?.LogWarning($"Compat ArcaneWard: cannot gate {ourName} (missing method).");
            return false;
        }

        var present = ListsAw(original, awPatch);
        try
        {
            if (present)
                harmony.Unpatch(original, awPatch);
        }
        catch (Exception ex)
        {
            RenameitConfig.Log?.LogWarning($"Compat ArcaneWard unpatch {ourName}: {ex.Message}");
        }

        var still = ListsAw(original, awPatch);
        RenameitConfig.Log?.LogInfo(
            $"Compat ArcaneWard {ourName}: Arcane patch {(present ? "found" : "absent")}, still attached={still}.");
        return true;
    }

    private static bool ListsAw(MethodBase original, MethodInfo awPatch)
    {
        var info = Harmony.GetPatchInfo(original);
        if (info == null)
            return false;

        foreach (var entry in info.Prefixes)
        {
            if (SameMethod(entry.PatchMethod, awPatch))
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
        if (_typesOk || _instancesField != null)
            return;

        try
        {
            var componentType = AccessTools.TypeByName(ComponentTypeName);
            var protectionType = AccessTools.TypeByName(ProtectionTypeName);
            if (componentType == null || protectionType == null)
                return;

            _instancesField = AccessTools.Field(componentType, "_instances");
            _checkFlag = AccessTools.Method(
                componentType,
                "CheckFlag",
                new[] { typeof(Vector3), typeof(bool), protectionType, typeof(bool) });
            _isEnabled = AccessTools.Property(componentType, "IsEnabled");
            _radius = AccessTools.Property(componentType, "Radius");

            try
            {
                _protectionItemStand = Enum.Parse(protectionType, "Item_Stand");
                _protectionDoor = Enum.Parse(protectionType, "Door");
            }
            catch
            {
                _protectionItemStand = null;
                _protectionDoor = null;
            }

            _typesOk = _instancesField != null
                       && _checkFlag != null
                       && _isEnabled != null
                       && _radius != null
                       && (_protectionDoor != null || _protectionItemStand != null);
        }
        catch (Exception ex)
        {
            _typesOk = false;
            RenameitConfig.Log?.LogDebug($"Compat ArcaneWard resolve: {ex.Message}");
        }
    }
}
