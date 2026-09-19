using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace DrakeRenameit;

/// <summary>
/// Valheim 1.0 <see cref="PieceTable"/> keeps pieces in <c>m_availablePiecesByCategory</c>.
/// <see cref="PieceTable.GetAvailablePiecesInCategory"/> does
/// <c>buckets[Min(Count - 1, cat)]</c> — when Count is 0 that index is -1 and Hud.UpdateBuild
/// spams ArgumentOutOfRangeException every frame (common with custom paper place tables
/// before the first UpdateAvailable, especially after multiplayer scene loads).
/// </summary>
internal static class PaperPieceTables
{
    private static readonly MethodInfo? UpdateAvailablePiecesListMethod =
        AccessTools.DeclaredMethod(typeof(Player), "UpdateAvailablePiecesList")
        ?? AccessTools.Method(typeof(Player), "UpdateAvailablePiecesList");

    /// <summary>Prepare a custom paper place table for Valheim 1.0 build HUD.</summary>
    internal static void Harden(PieceTable? table)
    {
        if (table == null)
            return;

        EnsureCategoryBuckets(table);

        // Simplified menu: paper tables only have wall/flat sheets.
        table.m_hideAdvancedMenu = true;
        table.m_canRemovePieces = true;

        ClampSelectedCategory(table);
        EnsureSelectionArrays(table);

        var go = table.gameObject;
        if (go != null)
            UnityEngine.Object.DontDestroyOnLoad(go);
    }

    /// <summary>
    /// Grow <see cref="PieceTable.m_availablePiecesByCategory"/> to at least
    /// <see cref="Piece.PieceCategory.Max"/> empty lists so category indexing never uses -1.
    /// </summary>
    internal static void EnsureCategoryBuckets(PieceTable table)
    {
        if (table == null)
            return;

        var buckets = table.m_availablePiecesByCategory;
        if (buckets == null)
            return;

        var need = Math.Max((int)Piece.PieceCategory.Max, 9);
        while (buckets.Count < need)
            buckets.Add(new List<Piece>());
    }

    internal static void ClampSelectedCategory(PieceTable table)
    {
        if (table == null)
            return;

        var buckets = table.m_availablePiecesByCategory;
        var cat = table.m_selectedCategory;
        var idx = (int)cat;
        if (cat == Piece.PieceCategory.Max ||
            cat == Piece.PieceCategory.All ||
            idx < 0 ||
            (buckets != null && buckets.Count > 0 && idx >= buckets.Count))
        {
            table.m_selectedCategory = Piece.PieceCategory.Misc;
        }
    }

    private static void EnsureSelectionArrays(PieceTable table)
    {
        var n = Math.Max(
            table.m_availablePiecesByCategory?.Count ?? 0,
            (int)Piece.PieceCategory.Max);
        if (n <= 0)
            n = 9;

        if (table.m_selectedPiece == null || table.m_selectedPiece.Length < n)
            Array.Resize(ref table.m_selectedPiece, n);
        if (table.m_lastSelectedPiece == null || table.m_lastSelectedPiece.Length < n)
            Array.Resize(ref table.m_lastSelectedPiece, n);
    }

    /// <summary>Force vanilla Misc so indexing stays inside the default 0..Max-1 buckets.</summary>
    internal static void ForceMiscCategory(GameObject? go)
    {
        var piece = go != null ? go.GetComponent<Piece>() : null;
        if (piece == null)
            return;
        piece.m_category = Piece.PieceCategory.Misc;
    }

    /// <summary>Re-run Player.UpdateAvailablePiecesList after SetPlaceMode.</summary>
    internal static void RefreshPlayerAvailable(Player? player)
    {
        if (player == null || UpdateAvailablePiecesListMethod == null)
            return;
        try
        {
            UpdateAvailablePiecesListMethod.Invoke(player, null);
        }
        catch (Exception)
        {
            /* Hud guard still prevents crash if refresh fails */
        }
    }

    [HarmonyPatch]
    private static class Patches
    {
        /// <summary>
        /// Before vanilla indexes Count-1, ensure buckets exist (Count==0 → index -1 crash).
        /// </summary>
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        [HarmonyPatch(typeof(PieceTable), nameof(PieceTable.GetAvailablePiecesInCategory))]
        private static void GetAvailablePiecesInCategory_Prefix(PieceTable __instance)
        {
            if (__instance == null)
                return;
            if (__instance.m_availablePiecesByCategory == null ||
                __instance.m_availablePiecesByCategory.Count == 0)
            {
                EnsureCategoryBuckets(__instance);
                ClampSelectedCategory(__instance);
            }
        }

        /// <summary>
        /// No Min() clamp in vanilla — custom / stale selected categories throw here.
        /// </summary>
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        [HarmonyPatch(typeof(PieceTable), nameof(PieceTable.GetPiecesInSelectedCategory))]
        private static bool GetPiecesInSelectedCategory_Prefix(
            PieceTable __instance,
            ref List<Piece> __result)
        {
            if (__instance == null)
            {
                __result = new List<Piece>();
                return false;
            }

            EnsureCategoryBuckets(__instance);
            ClampSelectedCategory(__instance);

            var buckets = __instance.m_availablePiecesByCategory;
            if (buckets == null || buckets.Count == 0)
            {
                __result = new List<Piece>();
                return false;
            }

            var idx = (int)__instance.GetSelectedCategory();
            if (idx < 0 || idx >= buckets.Count)
            {
                __result = buckets[0];
                return false;
            }

            return true;
        }
    }
}
