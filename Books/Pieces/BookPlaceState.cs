using System.Collections.Generic;
using DrakeModsLibs.Data;

namespace DrakeRenameit.Books;

/// <summary>
/// Which book the player picked for the hammer's Book piece. Choosing happens in <see cref="BookPickerPanel"/> before the
/// placement preview; the placed piece takes it (and removes the book, or the blank book's cost, from the inventory).
/// </summary>
internal static class BookPlaceState
{
    internal static ItemDrop.ItemData? Item { get; private set; }
    internal static bool Blank { get; private set; }
    internal static bool HasChoice => Item != null || Blank;

    /// <summary>The picked book's data (a copy), for the placement preview; a new blank book's defaults when that was picked.</summary>
    internal static Dictionary<string, string> PreviewData() =>
        Item != null ? new Dictionary<string, string>(Item.m_customData ?? new Dictionary<string, string>()) : BookData.NewBlank();

    internal static void Choose(ItemDrop.ItemData item)
    {
        Item = item;
        Blank = false;
    }

    internal static void ChooseBlank()
    {
        Item = null;
        Blank = true;
    }

    internal static void Clear()
    {
        Item = null;
        Blank = false;
    }

    /// <summary>The book's recipe as (item prefab, shared name, amount) rows, for cost checks and removal.</summary>
    internal static List<(string Prefab, string SharedName, int Amount)> Cost()
    {
        var list = new List<(string, string, int)>();
        foreach (var (prefab, amount) in Paper.Items.PaperItem.ParseCost(RenameitConfig.BookCost))
        {
            var drop = ObjectDB.instance?.GetItemPrefab(prefab)?.GetComponent<ItemDrop>();
            if (drop?.m_itemData?.m_shared != null)
                list.Add((prefab, drop.m_itemData.m_shared.m_name, amount));
        }

        return list;
    }

    internal static bool CanAffordBlank(Player player)
    {
        var inv = player.GetInventory();
        foreach (var (_, shared, amount) in Cost())
            if (inv.CountItems(shared) < amount)
                return false;
        return true;
    }

    /// <summary>
    /// Takes the chosen book out of the player's inventory (or pays for a blank one) and returns its data and maker.
    /// False when the choice no longer holds (the book moved, or the player can't pay now).
    /// </summary>
    internal static bool TryTake(Player player, out Dictionary<string, string> data, out long owner)
    {
        data = new Dictionary<string, string>();
        owner = player.GetPlayerID();
        var inv = player.GetInventory();

        if (Item != null)
        {
            if (!inv.ContainsItem(Item))
                return false;
            data = new Dictionary<string, string>(Item.m_customData ?? new Dictionary<string, string>());
            if (Item.m_crafterID != 0L)
                owner = Item.m_crafterID;
            inv.RemoveItem(Item);
            Clear();
            return true;
        }

        if (Blank)
        {
            if (!CanAffordBlank(player))
                return false;
            foreach (var (_, shared, amount) in Cost())
                inv.RemoveItem(shared, amount);
            data = BookData.NewBlank();
            Clear();
            return true;
        }

        return false;
    }
}
