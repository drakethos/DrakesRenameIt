using HarmonyLib;

namespace DrakeRenameit.Books;

/// <summary>
/// Every book that lands in an inventory (crafted, picked up, bought, loaded from an old save) gets the default red cover stamped on it,
/// colour and icon tint both, so no book shows the plain light icon. Books that already have a cover are left alone.
/// </summary>
[HarmonyPatch(typeof(Inventory), "Changed")]
internal static class BookDefaultsPatch
{
    static void Postfix(Inventory __instance)
    {
        if (!RenameitConfig.BooksEnabled || __instance == null)
            return;
        foreach (var item in __instance.GetAllItems())
        {
            if (item == null || !BookItem.IsBook(item))
                continue;
            if (item.m_customData != null && item.m_customData.ContainsKey(BookData.KeyColor))
            {
                // Books stamped by the first round carry an icon tint; the drawn icon replaces it.
                if (DrakeModsLibs.API.CustomizeLibsAPI.GetIconTint(item) != null)
                    DrakeModsLibs.API.CustomizeLibsAPI.SetIconTint(item, null);
                continue;
            }

            BookData.SetColor(item, BookData.CoverColors[0]);
        }
    }
}
