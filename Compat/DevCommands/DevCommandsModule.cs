using System;
using BepInEx.Bootstrap;
using DrakeModsLibs.Compat;
using HarmonyLib;

namespace DrakeRenameit.Compat.DevCommands;

/// <summary>
/// Soft DevCommands detection. Material costs already follow vanilla
/// <c>Player.NoCostCheat</c> (<see cref="InventoryCost"/>); this module is the
/// hook if a DevCommands command starts fighting rename or paper interact.
/// </summary>
internal sealed class DevCommandsModule : ICompatModule
{
    public const string ModuleId = "DevCommands";

    /// <summary>JereKuusela DevCommands (Thunderstore). SoftDependency only.</summary>
    public const string PluginGuid = "devcommands";

    public string Id => ModuleId;
    public string? SoftDependencyGuid => PluginGuid;
    public int Priority => CompatPriority.BuiltIn;
    public bool IsActive { get; private set; }

    public bool TryActivate()
    {
        IsActive = IsPluginLoaded(PluginGuid)
                   || AccessTools.TypeByName("DevCommands.DevCommands") != null
                   || AccessTools.TypeByName("DevCommands") != null;
        return IsActive;
    }

    public void ApplyHarmonyPatches(Harmony harmony)
    {
        if (IsActive)
            RenameitConfig.Log?.LogDebug("Compat DevCommands: present (nocost stays on Player.NoCostCheat).");
    }

    private static bool IsPluginLoaded(string guid)
    {
        if (string.IsNullOrEmpty(guid))
            return false;

        try
        {
            return Chainloader.PluginInfos != null
                   && Chainloader.PluginInfos.ContainsKey(guid);
        }
        catch (Exception ex)
        {
            RenameitConfig.Log?.LogDebug($"Compat DevCommands Chainloader probe: {ex.Message}");
            return false;
        }
    }
}
