using System;

namespace DrakesReskinIt.API;

/// <summary>Events raised when icon or tint customizations change. Other mods can subscribe to be notified.</summary>
public static class ReskinEvents
{
    /// <summary>Raised when a player sets or clears a custom icon on an item stack.</summary>
    public static event Action<Player, ItemDrop.ItemData, string, string>? OnItemIconChanged;

    /// <summary>Raised when a player sets or clears a tint on an item stack.</summary>
    public static event Action<Player, ItemDrop.ItemData, string, string>? OnItemTintChanged;

    internal static void RaiseIconChanged(
        Player? player,
        ItemDrop.ItemData item,
        string itemPrefabName,
        string newIconName)
    {
        if (player == null) return;
        OnItemIconChanged?.Invoke(player, item, itemPrefabName, newIconName);
    }

    internal static void RaiseTintChanged(
        Player? player,
        ItemDrop.ItemData item,
        string itemPrefabName,
        string newHtmlColor)
    {
        if (player == null) return;
        OnItemTintChanged?.Invoke(player, item, itemPrefabName, newHtmlColor);
    }
}
