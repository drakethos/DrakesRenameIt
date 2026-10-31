using HarmonyLib;

namespace DrakeRenameit.Books;

/// <summary>Selecting the hammer's Book piece opens the picker once; leaving it forgets an unused pick.</summary>
[HarmonyPatch]
internal static class BookPlacementPatches
{
    static bool _wasBook;

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Player), "Update")]
    static void Update_Postfix(Player __instance)
    {
        if (__instance != Player.m_localPlayer || !RenameitConfig.BooksEnabled)
            return;

        var selected = __instance.InPlaceMode() ? Selected(__instance) : null;
        var isBook = BookPiece.IsBookPiece(selected);
        if (isBook && !_wasBook && !BookPlaceState.HasChoice && !BookPickerPanel.IsOpen)
            BookPickerPanel.Show(__instance);
        if (!isBook && _wasBook && !BookPickerPanel.IsOpen)
            BookPlaceState.Clear();
        _wasBook = isBook;
    }

    static readonly System.Reflection.FieldInfo? BuildPieces = AccessTools.Field(typeof(Player), "m_buildPieces");

    static Piece? Selected(Player player) =>
        (BuildPieces?.GetValue(player) as PieceTable)?.GetSelectedPiece();
}
