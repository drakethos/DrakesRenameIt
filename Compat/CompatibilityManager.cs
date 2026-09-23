using System;
using DrakeModsLibs.Compat;
using DrakeRenameit.Compat.ArcaneWard;
using DrakeRenameit.Compat.DevCommands;
using DrakeRenameit.Compat.ItemLibs;
using DrakeRenameit.Compat.ProtectiveWards;
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
        public const string DevCommands = DevCommandsModule.PluginGuid;
        public const string LockSmith = WardHoverAccess.LockSmithGuid;
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

        Host.Register(new VanillaPrivateAreaModule());
        Host.Register(new WardIsLoveModule());
        Host.Register(new ProtectiveWardsModule());
        Host.Register(new ArcaneWardModule());
        Host.Register(new ItemStandHoverModule());
        Host.Register(new DevCommandsModule());
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
