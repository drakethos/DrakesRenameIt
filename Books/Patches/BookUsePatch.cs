using HarmonyLib;

namespace DrakeRenameit.Books;

/// <summary>Use on a book (right-click in the inventory, or its hotbar key) reads it. Placing is a hammer piece, not Use.</summary>
[HarmonyPatch]
internal static class BookUsePatch
{
    [HarmonyPrefix]
    [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.UseItem))]
    static bool UseItem_Prefix(Humanoid __instance, ItemDrop.ItemData item)
    {
        if (!RenameitConfig.BooksEnabled || __instance != Player.m_localPlayer || item == null || !BookItem.IsBook(item))
            return true;

        BookReaderPanel.Show(item, BookAccess.CanEditItem(item));
        return false;
    }
}

/// <summary>Who may write in a book: its maker (or nobody, for one from a trader), and admins / VIPs.</summary>
internal static class BookAccess
{
    internal static bool CanEditItem(ItemDrop.ItemData item)
    {
        var player = Player.m_localPlayer;
        if (player == null)
            return false;
        return item.m_crafterID == 0L || item.m_crafterID == player.GetPlayerID() || API.RenameitPermission.IsAdminOrVIP(player);
    }
}
