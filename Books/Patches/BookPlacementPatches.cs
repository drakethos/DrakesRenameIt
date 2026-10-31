using HarmonyLib;
using UnityEngine;

namespace DrakeRenameit.Books;

/// <summary>
/// Selecting the hammer's Book piece opens the picker once. A pick is kept until you choose a different piece or put the hammer
/// away for a few seconds, so a blip in the placement state (the picker window itself takes input) never throws it away.
/// </summary>
[HarmonyPatch]
internal static class BookPlacementPatches
{
    static bool _wasBook;
    static float _lastBookTime;
    static float _keepPickUntil;

    /// <summary>A fresh pick survives this long whatever the placement state does (the picker window itself takes input).</summary>
    internal static void KeepPick(float seconds) => _keepPickUntil = Time.time + seconds;

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Player), "Update")]
    static void Update_Postfix(Player __instance)
    {
        if (__instance != Player.m_localPlayer || !RenameitConfig.BooksEnabled)
            return;

        var inPlace = __instance.InPlaceMode();
        var selected = inPlace ? Selected(__instance) : null;
        var isBook = BookPiece.IsBookPiece(selected);
        if (isBook)
        {
            _lastBookTime = Time.time;
            if (!_wasBook && !BookPlaceState.HasChoice && !BookPickerPanel.IsOpen)
                BookPickerPanel.Show(__instance);
        }
        else if (BookPlaceState.HasChoice && !BookPickerPanel.IsOpen)
        {
            // A different piece, or the hammer away for a while: forget the pick.
            if (Time.time > _keepPickUntil && (selected != null || Time.time - _lastBookTime > 4f))
                BookPlaceState.Clear();
        }

        _wasBook = isBook;
    }

    static readonly System.Reflection.FieldInfo? BuildPieces = AccessTools.Field(typeof(Player), "m_buildPieces");

    static Piece? Selected(Player player) =>
        (BuildPieces?.GetValue(player) as PieceTable)?.GetSelectedPiece();
}
