using System.Reflection;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using ReskinItPermission = global::DrakesReskinIt.API.ReskinItPermission;

namespace DrakesReskinIt;

/// <summary>Harmony patches for InventoryGrid icon/tint rendering and right-click menu entry.</summary>
internal static class ReskinPatches
{
    internal static void Apply(Harmony harmony, ManualLogSource log)
    {
        // The InventoryGrid image-set method varies by Valheim version; bind it by reflection.
        MethodInfo? updateItem = null;
        foreach (var m in typeof(InventoryGrid).GetMethods(
                     BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
        {
            var p = m.GetParameters();
            if (m.Name == "UpdateItem" && p.Length >= 2 &&
                p[0].ParameterType == typeof(InventoryGrid.Element) &&
                p[1].ParameterType == typeof(ItemDrop.ItemData))
            {
                updateItem = m;
                break;
            }
        }

        if (updateItem != null)
        {
            harmony.Patch(
                updateItem,
                postfix: new HarmonyMethod(typeof(ReskinPatches), nameof(UpdateItem_Postfix)));
        }
        else
        {
            log.LogWarning("[DrakesReskinIt] InventoryGrid.UpdateItem not found — icon/tint replacement in grid disabled.");
        }
    }

    /// <summary>
    /// After the grid cell is populated with the vanilla icon, swap in the custom sprite and/or apply the tint color.
    /// </summary>
    static void UpdateItem_Postfix(InventoryGrid.Element element, ItemDrop.ItemData item)
    {
        if (item == null || element?.m_icon == null)
            return;

        // Custom sprite
        if (DrakesReskinIt.HasCustomIcon(item))
        {
            var sprite = DrakesReskinIt.GetDisplayIcon(item);
            if (sprite != null)
                element.m_icon.sprite = sprite;
        }

        // Tint / recolor
        element.m_icon.color = DrakesReskinIt.GetDisplayTint(item);
    }
}

// ─── InventoryGui right-click → open action menu ──────────────────────────────

[HarmonyPatch(typeof(InventoryGui))]
public static class ReskinInventoryGuiPatch
{
    [HarmonyPatch(nameof(InventoryGui.Awake))]
    [HarmonyPostfix]
    static void HookRightClick(InventoryGui __instance)
    {
        var original = __instance.m_playerGrid.m_onRightClick;
        __instance.m_playerGrid.m_onRightClick = (grid, item, pos) =>
        {
            if (item != null && DrakesReskinIt.IsMenuOpenModifierHeld())
            {
                if (DrakesReskinIt.AnyInventoryActionAvailable(item))
                {
                    ReskinUIPanels.OpenActionMenu(item);
                    return;
                }
            }
            original?.Invoke(grid, item, pos);
        };
    }
}

// ─── InventoryGrid tooltip hint ───────────────────────────────────────────────

[HarmonyPatch(typeof(InventoryGrid), nameof(InventoryGrid.CreateItemTooltip))]
public static class ReskinTooltipPatch
{
    [HarmonyPostfix]
    static void AddHint(InventoryGrid __instance, ItemDrop.ItemData? item, UITooltip tooltip)
    {
        if (item?.m_shared == null || tooltip == null)
            return;

        string topic = item.m_shared.m_name;
        if (Localization.instance != null)
            topic = Localization.instance.Localize(topic);

        string currentText = item.GetTooltip();
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("\n");

        string color = ReskinItConfig.MenuHintColor;
        string mod = ReskinItConfig.MenuModifierIsShift ? "Shift" : "Ctrl";
        bool anyAction = DrakesReskinIt.AnyInventoryActionAvailable(item);
        bool elevated = Player.m_localPlayer != null &&
                        ReskinItPermission.IsElevatedForOverrides(Player.m_localPlayer);

        if (anyAction)
        {
            sb.AppendLine($"<color={color}><b>{mod} + Right Click for reskin options</b></color>");
        }
        else if (elevated)
        {
            sb.AppendLine(
                $"<color={color}><b>{mod} + Right Click for reskin options</b></color><color=blue> Elevated</color>");
        }
        else
        {
            sb.AppendLine(
                $"<color=red><s>{mod} + Right Click for reskin options</s></color>");
        }

        tooltip.Set(topic, currentText + sb, __instance.m_tooltipAnchor);
    }
}
