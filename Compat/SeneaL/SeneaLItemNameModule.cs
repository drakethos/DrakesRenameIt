using System;
using System.Reflection;
using BepInEx.Bootstrap;
using DrakeModsLibs.API;
using DrakeModsLibs.Compat;
using HarmonyLib;

namespace DrakeRenameit.Compat.SeneaL;

/// <summary>
/// SeneaL UI draws its own inventory, equipment slots and tooltips and writes the item's vanilla name into every one of them.
/// It does ask one question first, <c>Runeforging.CustomName(item)</c> (another mod's custom name), and uses the answer instead
/// of the vanilla name. This postfix answers with RenameIt's display name when the item has one, so SeneaL's tooltips and
/// crafting views show the new name.
/// </summary>
internal sealed class SeneaLItemNameModule : ICompatModule
{
    public const string ModuleId = "SeneaLItemName";
    public const string SeneaLGuid = "seneaL.valheim.ui";

    private const string TypeName = "SeneaLUI.Mods.Runeforging";
    private const string MethodName = "CustomName";

    public string Id => ModuleId;

    public string? SoftDependencyGuid => SeneaLGuid;

    public int Priority => CompatPriority.BuiltIn;

    public bool IsActive { get; private set; }

    public bool TryActivate()
    {
        try
        {
            IsActive = Chainloader.PluginInfos.ContainsKey(SeneaLGuid) && ResolveTarget() != null;
        }
        catch (Exception)
        {
            IsActive = false;
        }

        return IsActive;
    }

    public void ApplyHarmonyPatches(Harmony harmony)
    {
        if (harmony == null || !IsActive)
            return;

        var target = ResolveTarget();
        if (target == null)
            return;

        harmony.Patch(target, postfix: new HarmonyMethod(typeof(SeneaLItemNameModule), nameof(Postfix)));
    }

    private static MethodBase? ResolveTarget()
    {
        var type = AccessTools.TypeByName(TypeName);
        return type == null ? null : AccessTools.Method(type, MethodName, new[] { typeof(ItemDrop.ItemData) });
    }

    // never replaces an answer SeneaL already has (the Runeforging mod's own name)
    private static void Postfix(ItemDrop.ItemData item, ref string __result)
    {
        try
        {
            if (__result != null || item?.m_shared == null || !CustomizeLibsAPI.HasCustomName(item))
                return;
            var name = CustomizeLibsAPI.GetDisplayNameForUi(item, localize: false);
            if (!string.IsNullOrEmpty(name))
                __result = name;
        }
        catch (Exception)
        {
            // never break the inventory
        }
    }
}
