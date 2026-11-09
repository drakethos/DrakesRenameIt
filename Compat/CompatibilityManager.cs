using System;
using DrakeModsLibs.Compat;
using DrakeRenameit.Compat.ArcaneWard;
using DrakeRenameit.Compat.ExtraSlots;
using DrakeRenameit.Compat.ItemLibs;
using DrakeRenameit.Compat.ProtectiveWards;
using DrakeRenameit.Compat.SeneaL;
using DrakeRenameit.Compat.Vanilla;
using DrakeRenameit.Compat.WardIsLove;
using HarmonyLib;
using UnityEngine;

namespace DrakeRenameit.Compat;

/// <summary>
/// RenameIt compatibility facade over a per-plugin <see cref="CompatHost"/>.
/// Paper take and item-stand names stay here; register/priority/patches are Libs.
/// </summary>
internal static class CompatibilityManager
{
    private static CompatHost? _host;

    /// <summary>
    /// SoftDependency GUIDs. SoftDependency never blocks load when the mod is missing.
    /// </summary>
    internal static class SoftGuids
    {
        public const string WardIsLove = WardIsLoveModule.PluginGuid;
        public const string ProtectiveWards = ProtectiveWardsModule.PluginGuid;
        public const string ArcaneWard = ArcaneWardModule.PluginGuid;
        public const string LockSmith = WardHoverAccess.LockSmithGuid;
        public const string ExtraSlots = ExtraSlotsTooltipModule.ExtraSlotsGuid;
        public const string SeneaL = SeneaLItemNameModule.SeneaLGuid;
    }

    private static CompatHost Host =>
        _host ??= CompatHosts.GetOrCreate(DrakeRenameit.GUID, RenameitConfig.Log);

    /// <summary>
    /// Register built-in modules, then initialize the Libs host.
    /// Call once from plugin Awake after <c>PatchAll</c>.
    /// </summary>
    public static void Initialize(Harmony harmony)
    {
        if (harmony == null)
            return;

        // Soft dependencies load first (BepInDependency), so an absent mod is simply not in PluginInfos. Skipping it avoids
        // each module's AccessTools.TypeByName probe, which scans every type of every loaded assembly (~100 ms each).
        void Reg(ICompatModule module)
        {
            var guid = module.SoftDependencyGuid;
            if (guid != null && !BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey(guid))
                return;
            Host.Register(module);
        }

        Reg(new VanillaPrivateAreaModule());
        Reg(new WardIsLoveModule());
        Reg(new ProtectiveWardsModule());
        Reg(new ArcaneWardModule());
        Reg(new ItemStandHoverModule());
        Reg(new ExtraSlotsTooltipModule());
        Reg(new SeneaLItemNameModule());
        Host.Initialize(harmony);
    }

    public static bool HasActiveModule(string id) => Host.HasActiveModule(id);

    /// <summary>OR across active area modules — any enabled covering ward.</summary>
    public static bool IsInsideEnabledWard(Vector3 position)
    {
        foreach (var module in Host.GetActiveModulesOfType<IAreaCompatModule>())
        {
            try
            {
                if (module.IsInsideEnabledWard(position))
                    return true;
            }
            catch (Exception ex)
            {
                RenameitConfig.Log?.LogDebug($"Compat {module.Id} IsInsideEnabledWard: {ex.Message}");
            }
        }

        return false;
    }

    /// <summary>
    /// Highest-priority covering module wins. Nothing covering → vanilla
    /// <see cref="PrivateArea.CheckAccess"/> (true when no vanilla ward exists).
    /// </summary>
    public static bool CheckAccess(Vector3 position, bool flash = false)
    {
        var modules = Host.GetActiveModulesOfType<IAreaCompatModule>();
        modules.Sort((a, b) => b.Priority.CompareTo(a.Priority));

        foreach (var module in modules)
        {
            try
            {
                var kind = module.QueryLocalAccess(position, flash);
                if (kind == AreaCoverageKind.Unrelated)
                    continue;

                return kind == AreaCoverageKind.Allowed;
            }
            catch (Exception ex)
            {
                RenameitConfig.Log?.LogDebug($"Compat {module.Id} access query: {ex.Message}");
            }
        }

        try
        {
            return PrivateArea.CheckAccess(position, 0f, flash, wardCheck: false);
        }
        catch (Exception ex)
        {
            RenameitConfig.Log?.LogDebug($"Compat vanilla CheckAccess fallback: {ex.Message}");
            return true;
        }
    }
}
