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

        // The maker (or a book nobody made, such as one from a trader) may edit; everyone else reads.
        var player = Player.m_localPlayer;
        var canEdit = item.m_crafterID == 0L || item.m_crafterID == player.GetPlayerID();
        BookReaderPanel.Show(item, canEdit);
        return false;
    }
}
