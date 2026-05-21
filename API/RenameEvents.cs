using System;
using DrakesWorkshopLibs.API;

namespace DrakeRenameit.API;

/// <summary>RenameIt-facing events; raises mirror <see cref="CustomizationEvents"/> for suite-wide subscribers.</summary>
public static class RenameEvents
{
    public static event Action<Player, ItemDrop.ItemData, string, string>? OnItemNameChanged;
    public static event Action<Player, ItemDrop.ItemData, string, string>? OnItemDescriptionChanged;
    public static event Action<Player, ItemDrop.ItemData, string, string, string>? OnCraftedByDisplayChanged;

    internal static void RaiseNameChanged(Player player, ItemDrop.ItemData item, string oldName, string newName)
    {
        CustomizationEvents.RaiseNameChanged(player, item, oldName, newName);
        OnItemNameChanged?.Invoke(player, item, oldName, newName);
    }

    internal static void RaiseDescriptionChanged(Player player, ItemDrop.ItemData item, string oldDesc, string newDesc)
    {
        CustomizationEvents.RaiseDescriptionChanged(player, item, oldDesc, newDesc);
        OnItemDescriptionChanged?.Invoke(player, item, oldDesc, newDesc);
    }

    internal static void RaiseCraftedByDisplayChanged(
        Player player, ItemDrop.ItemData item, string itemPrefabName, string oldDisplay, string newDisplay)
    {
        CustomizationEvents.RaiseCraftedByDisplayChanged(player, item, itemPrefabName, oldDisplay, newDisplay);
        OnCraftedByDisplayChanged?.Invoke(player, item, itemPrefabName, oldDisplay, newDisplay);
    }
}
