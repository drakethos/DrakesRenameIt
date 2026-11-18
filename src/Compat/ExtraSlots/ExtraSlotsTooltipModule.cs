using System;
using System.Reflection;
using BepInEx.Bootstrap;
using DrakeModsLibs.Compat;
using HarmonyLib;

namespace DrakeRenameit.Compat.ExtraSlots;

/// <summary>
/// ExtraSlots (shudnal) writes its equipment and extra slot tooltips itself, using the vanilla item name. Each update it
/// overwrites a renamed item's tooltip with the vanilla name. This postfix re-runs the game's own grid tooltip on the
/// same element, so the Libs display name and Crafted-by line apply to those slots too.
/// </summary>
internal sealed class ExtraSlotsTooltipModule : ICompatModule
{
    public const string ModuleId = "ExtraSlotsTooltip";
    public const string ExtraSlotsGuid = "shudnal.ExtraSlots";

    private const string PanelTypeName = "ExtraSlots.EquipmentPanel";
    private const string PanelMethodName = "SetSlotBackgroundImage";

    private static PropertyInfo? _slotItem;
    private static PropertyInfo? _slotIsFree;
    private static FieldInfo? _elementTooltip;
    private static MethodInfo? _createTooltip;

    public string Id => ModuleId;

    public string? SoftDependencyGuid => ExtraSlotsGuid;

    public int Priority => CompatPriority.BuiltIn;

    public bool IsActive { get; private set; }

    public bool TryActivate()
    {
        try
        {
            IsActive = Chainloader.PluginInfos.ContainsKey(ExtraSlotsGuid) && ResolveTarget() != null;
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

        harmony.Patch(target, postfix: new HarmonyMethod(typeof(ExtraSlotsTooltipModule), nameof(Postfix)));
    }

    private static MethodInfo? ResolveTarget()
    {
        var panel = AccessTools.TypeByName(PanelTypeName);
        return panel == null ? null : AccessTools.Method(panel, PanelMethodName);
    }

    // Runs after ExtraSlots has set its slot background; slot and element are typed as object, so this never depends
    // on ExtraSlots' own types being loadable.
    private static void Postfix(object element, object slot)
    {
        try
        {
            if (element == null || slot == null)
                return;

            _slotIsFree ??= AccessTools.Property(slot.GetType(), "IsFree");
            if (_slotIsFree?.GetValue(slot) is not false)
                return;

            _slotItem ??= AccessTools.Property(slot.GetType(), "Item");
            if (_slotItem?.GetValue(slot) is not ItemDrop.ItemData item || item.m_shared == null)
                return;

            _elementTooltip ??= AccessTools.Field(element.GetType(), "m_tooltip");
            if (_elementTooltip?.GetValue(element) is not UITooltip tooltip)
                return;

            var grid = InventoryGui.instance?.m_playerGrid;
            if (grid == null)
                return;

            _createTooltip ??= AccessTools.Method(typeof(InventoryGrid), "CreateItemTooltip");
            _createTooltip?.Invoke(grid, new object[] { item, tooltip });
        }
        catch (Exception)
        {
            // never break the inventory
        }
    }
}
