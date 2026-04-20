using System.Linq;
using HarmonyLib;
using UnityEngine;
using RenameitPermission = global::DrakeRenameit.API.RenameitPermission;

namespace DrakeRenameit;

/// <summary>Shared hover text rename logic (ItemDrop + ItemStand / container stands).</summary>
internal static class HoverRenameHelper
{
    internal static void ApplyRenameToHoverResult(ref string __result, ItemDrop.ItemData item)
    {
        if (item?.m_shared == null || string.IsNullOrEmpty(__result))
            return;

        if (!DrakeRenameit.hasNewName(item))
            return;

        string customName = TooltipRichText.EnsureColorTagsClosedForTooltip(DrakeRenameit.GetPropperName(item));
        if (customName == null || item.m_shared.m_name == null)
            return;

        // Replace the default name in the hover text with our rename
        if (Localization.instance == null)
        {
            __result = __result.Replace(item.m_shared.m_name, customName);
            return;
        }

        string localizedOriginalName = Localization.instance.Localize(item.m_shared.m_name);
        string localizedCustomName = Localization.instance.Localize(customName);

        if (__result.Contains(localizedOriginalName))
            __result = __result.Replace(localizedOriginalName, localizedCustomName);
    }
}

/// <summary>Top-left pickup / removed messages use <see cref="ItemDrop.ItemData.m_shared"/>.<see cref="ItemDrop.SharedData.m_name"/>; swap in our custom name when set.</summary>
internal static class PickupHudMessageHelper
{
    internal static bool TryGetLocalizedCustomNameForHud(ItemDrop.ItemData? item, out string localizedName)
    {
        localizedName = "";
        if (item?.m_shared == null || !DrakeRenameit.hasNewName(item))
            return false;

        string customName = TooltipRichText.EnsureColorTagsClosedForTooltip(DrakeRenameit.GetPropperName(item));
        if (string.IsNullOrEmpty(customName))
            return false;

        localizedName = Localization.instance != null
            ? Localization.instance.Localize(customName)
            : customName;
        return true;
    }
}

[HarmonyPatch(typeof(Character), nameof(Character.ShowPickupMessage))]
internal static class CharacterShowPickupMessagePatch
{
    [HarmonyPrefix]
    static bool Prefix(Character __instance, ItemDrop.ItemData item, int amount)
    {
        if (!PickupHudMessageHelper.TryGetLocalizedCustomNameForHud(item, out var nameFragment))
            return true;

        __instance.Message(MessageHud.MessageType.TopLeft, "$msg_added " + nameFragment, amount, item.GetIcon());
        return false;
    }
}

[HarmonyPatch(typeof(Character), nameof(Character.ShowRemovedMessage))]
internal static class CharacterShowRemovedMessagePatch
{
    [HarmonyPrefix]
    static bool Prefix(Character __instance, ItemDrop.ItemData item, int amount)
    {
        if (!PickupHudMessageHelper.TryGetLocalizedCustomNameForHud(item, out var nameFragment))
            return true;

        __instance.Message(MessageHud.MessageType.TopLeft, "$msg_removed " + nameFragment, amount, item.GetIcon());
        return false;
    }
}

public static class Patches
{
    [HarmonyPatch(typeof(ItemDrop))]
    public static class HoverTextPatch
    {
        [HarmonyPatch(nameof(ItemDrop.GetHoverText))]
        [HarmonyPostfix]
        static void FixHoverText(ItemDrop __instance, ref string __result)
        {
            var item = __instance.m_itemData;
            if (item == null) return;
            if (__instance?.m_itemData?.m_shared == null || string.IsNullOrEmpty(__result))
                return;

            HoverRenameHelper.ApplyRenameToHoverResult(ref __result, item);
        }

        [HarmonyPatch(typeof(ItemDrop), nameof(ItemDrop.GetHoverName))]
        [HarmonyPostfix]
        static void FixHoverName(ItemDrop __instance, ref string __result)
        {
            var item = __instance?.m_itemData;
            if (item == null)
                return;

            if (DrakeRenameit.hasNewName(item))
            {
                var newName = DrakeRenameit.GetPropperName(item);
                if (!string.IsNullOrEmpty(newName))
                    __result = newName;
            }
        }
    }

    [HarmonyPatch(typeof(InventoryGui))]
    public static class PatchInventoryGuiAwake
    {
        [HarmonyPatch(nameof(InventoryGui.Awake))]
        [HarmonyPostfix]
        static void RenameGrab(InventoryGui __instance)
        {
            // grab the original delegate
            var original = __instance.m_playerGrid.m_onRightClick;

            // wrap it with our own
            __instance.m_playerGrid.m_onRightClick = (grid, item, pos) =>
            {
                if (item != null && DrakeRenameit.IsMenuOpenModifierHeld())
                {
                    if (DrakeRenameit.AnyInventoryActionAvailable(item))
                    {
                        UIPanels.OpenActionMenu(item);
                        return;
                    }

                    // Modifier is held but nothing is available — show the reason instead of silently doing nothing
                    string reason = DrakeRenameit.GetMenuBlockedReason(item);
                    if (!string.IsNullOrEmpty(reason))
                        Player.m_localPlayer?.Message(MessageHud.MessageType.Center, reason);
                    return;
                }

                original?.Invoke(grid, item, pos);
            };
        }


        [HarmonyPatch(nameof(InventoryGui.DoCrafting))]
        [HarmonyPostfix]
        static void FixCrafting(InventoryGui __instance, Player player)
        {
            var craftRecipe = AccessTools.Field(typeof(InventoryGui), "m_craftRecipe")
    ?.GetValue(__instance) as Recipe;
            var oldItem = (ItemDrop.ItemData)AccessTools.Field(typeof(InventoryGui), "m_craftUpgradeItem")
                .GetValue(__instance);

            if (craftRecipe == null)
                return;
            if (oldItem == null)
                return;

            var inv = player.GetInventory();
            var newItem = inv.GetItemAt(oldItem.m_gridPos.x, oldItem.m_gridPos.y);
            if (newItem == null)
            {
                // fallback: grab last-added item of that name
                newItem = inv.GetAllItems().LastOrDefault(i =>
                    i.m_shared.m_name == oldItem.m_shared.m_name && i.m_quality > oldItem.m_quality);
            }

            if (newItem != null)
            {
                // carry over rename/desc customData
                foreach (var kv in oldItem.m_customData)
                {
                    // overwrite or add
                    newItem.m_customData[kv.Key] = kv.Value;
                }
            }
        }
    }
}

[HarmonyPatch(typeof(InventoryGrid), nameof(InventoryGrid.CreateItemTooltip))]
public static class InventoryGridTooltipPatch
{
    [HarmonyPostfix]
    static void UpdateToolTip(InventoryGrid __instance, ItemDrop.ItemData? item, UITooltip tooltip)
    {
        if (item?.m_shared == null || tooltip == null)
            return;

        var topic = TooltipRichText.EnsureColorTagsClosedForTooltip(DrakeRenameit.GetPropperName(item) ?? item.m_shared.m_name);
        string currentText = item.GetTooltip();
        currentText = ItemTooltipPatches.ApplyCraftedByDisplayToTooltipText(currentText, item);

        // Handle custom description replacement
        currentText = UpdateDescription(item, currentText);

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("\n");

        string menuColor = RenameitConfig.MenuHintColor;
        string menuMod = RenameitConfig.MenuModifierIsShift ? "Shift" : "Ctrl";
        bool anyAction = DrakeRenameit.AnyInventoryActionAvailable(item);
        bool elevated = Player.m_localPlayer != null &&
                          RenameitPermission.IsElevatedForOverrides(Player.m_localPlayer);

        if (anyAction)
        {
            sb.AppendLine($"<color={menuColor}><b>{menuMod} + Right Click for options</b></color>");
        }
        else if (elevated)
        {
            sb.AppendLine(
                $"<color={menuColor}><b>{menuMod} + Right Click for options</b></color><color=blue> Elevated override</color>");
        }
        else
        {
            string detail = BuildNoDrakeMenuHint(item);
            sb.AppendLine($"<color=red><s>{menuMod} + Right Click for options</s></color>");
            if (!string.IsNullOrEmpty(detail))
                sb.AppendLine($"<color=red>{detail}</color>");
        }

        // Final set
        tooltip.Set(topic, currentText + sb, __instance.m_tooltipAnchor);
    }

    private static string BuildNoDrakeMenuHint(ItemDrop.ItemData item)
    {
        var parts = new System.Collections.Generic.List<string>();
        if (!DrakeRenameit.CanChangeName(item, false))
            parts.Add(RenamePermissionManager.GetTooltipDisabledHint(RenamePermissionOperation.RenameItemName, item));
        if (!DrakeRenameit.CanChangeDesc(item, false))
            parts.Add(RenamePermissionManager.GetTooltipDisabledHint(RenamePermissionOperation.RewriteDescription, item));
        if (!DrakeRenameit.CanChangeCraftedByLabel(item, false))
            parts.Add(RenamePermissionManager.GetTooltipDisabledHint(RenamePermissionOperation.EditCraftedByLabel, item));
        return string.Join("<br>", parts.Distinct());
    }

    private static string UpdateDescription(ItemDrop.ItemData? item, string currentText)
    {
        if (item?.m_shared == null)
            return currentText;
        if (DrakeRenameit.hasNewDesc(item))
        {
            string customDesc = TooltipRichText.EnsureColorTagsClosedForTooltip(
                DrakeRenameit.getPropperDesc(item, item.m_shared.m_description));
            string originalDesc = item.m_shared.m_description;

            if (!string.IsNullOrEmpty(originalDesc) && currentText.Contains(originalDesc))
            {
                currentText = currentText.Replace(originalDesc, customDesc);
            }
        }

        return currentText;
    }
}

[HarmonyPatch(typeof(ItemStand))]
public static class ItemStandPatch
{
    /// <summary>ZenDragon-style and other container stands: first item in attached <see cref="Container"/>.</summary>
    private static ItemDrop.ItemData? TryGetFirstContainerItem(ItemStand stand)
    {
        var c = stand.GetComponent<Container>();
        if (c == null)
            return null;
        var inv = c.GetInventory();
        if (inv == null)
            return null;
        var items = inv.GetAllItems();
        if (items == null || items.Count == 0)
            return null;
        return items[0];
    }

    [HarmonyPatch(nameof(ItemStand.GetHoverText))]
    [HarmonyPostfix]
    [HarmonyPriority(Priority.Last)]
    static void FixItemStandHoverText(ItemStand __instance, ref string __result)
    {
        if (__instance == null || string.IsNullOrEmpty(__result))
            return;

        var item = TryGetFirstContainerItem(__instance);
        if (item?.m_shared == null)
            return;

        if (!DrakeRenameit.hasNewName(item))
            return;

        HoverRenameHelper.ApplyRenameToHoverResult(ref __result, item!);
    }

    [HarmonyPatch(nameof(ItemStand.UseItem))]
    [HarmonyPostfix]
    [HarmonyPriority(Priority.Last)]
    static void GrabItem(ItemStand __instance, Humanoid user, ItemDrop.ItemData? item)
    {
        if (item?.m_shared == null)
            return;

        string customName = DrakeRenameit.getPropperName(item);
        if (customName != item.m_shared.m_name)
        {
            var zdo = ((ZNetView)AccessTools.Field(typeof(ItemStand), "m_nview").GetValue(__instance)).GetZDO();
            zdo.Set("DrakeRenameIt_CustomName", customName);
        }
    }

    [HarmonyPatch(nameof(ItemStand.SetVisualItem))]
    [HarmonyPostfix]
    [HarmonyPriority(Priority.Last)]
    static void FixStandText(ItemStand __instance, string itemName, int variant, int quality)
    {
        var mNviewField = AccessTools.Field(typeof(ItemStand), "m_nview");
        object? nviewObj = __instance != null ? mNviewField?.GetValue(__instance) : null;
        var nview = nviewObj as ZNetView;

        if (nview == null)
            return;

        var zdo = nview.GetZDO();

        if (zdo == null) return;

        string customName = zdo.GetString("DrakeRenameIt_CustomName", "");
        if (!string.IsNullOrEmpty(customName))
        {
            var currentItemField = AccessTools.Field(typeof(ItemStand), "m_currentItemName");
            currentItemField?.SetValue(__instance, customName);
        }
    }
}