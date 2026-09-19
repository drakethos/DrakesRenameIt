using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace DrakeRenameit;

/// <summary>
/// Valheim 1.0 <see cref="PieceTable"/> keeps pieces in private
/// <c>m_availablePiecesByCategory</c>. <see cref="PieceTable.GetAvailablePiecesInCategory"/>
/// does <c>buckets[Min(Count - 1, cat)]</c> — when Count is 0 that index is -1 and
/// Hud.UpdateBuild spams ArgumentOutOfRangeException every frame (custom paper place
/// tables before the first UpdateAvailable, especially after multiplayer scene loads).
/// <para>
/// Private fields are accessed only via Harmony <see cref="AccessTools.FieldRefAccess{T,F}"/>
/// / <see cref="AccessTools"/> — never via publicized compile refs (those compile as
/// direct ldfld and throw <see cref="FieldAccessException"/> against the live game asm,
/// which corrupts the entire build HUD).
/// </para>
/// </summary>
internal static class PaperPieceTables
{
    private const int VanillaCategoryBucketCount = 9;

    private static readonly MethodInfo? UpdateAvailablePiecesListMethod =
        AccessTools.DeclaredMethod(typeof(Player), "UpdateAvailablePiecesList")
        ?? AccessTools.Method(typeof(Player), "UpdateAvailablePiecesList");

    // FieldRefAccess skips visibility; FieldInfo.GetValue can still FieldAccessException on Mono.
    private static readonly AccessTools.FieldRef<PieceTable, List<List<Piece>>>? AvailableByCategoryRef =
        TryFieldRef<PieceTable, List<List<Piece>>>("m_availablePiecesByCategory");

    private static readonly AccessTools.FieldRef<PieceTable, Piece.PieceCategory>? SelectedCategoryRef =
        TryFieldRef<PieceTable, Piece.PieceCategory>("m_selectedCategory");

    private static AccessTools.FieldRef<T, F>? TryFieldRef<T, F>(string name)
    {
        try
        {
            if (AccessTools.Field(typeof(T), name) == null)
                return null;
            return AccessTools.FieldRefAccess<T, F>(name);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Prepare a custom paper place table for Valheim 1.0 build HUD.</summary>
    internal static void Harden(PieceTable? table)
    {
        if (table == null)
            return;

        try
        {
            EnsureCategoryBuckets(table);
            table.m_hideAdvancedMenu = true;
            table.m_canRemovePieces = true;
            ClampSelectedCategory(table);
            EnsureSelectionArrays(table);

            var go = table.gameObject;
            if (go != null)
                UnityEngine.Object.DontDestroyOnLoad(go);
        }
        catch (Exception)
        {
            /* never break place-mode arming */
        }
    }

    /// <summary>
    /// Grow the per-category bucket list to at least the vanilla Max count so indexing never uses -1.
    /// </summary>
    internal static void EnsureCategoryBuckets(PieceTable table)
    {
        if (table == null || AvailableByCategoryRef == null)
            return;

        List<List<Piece>> buckets;
        try
        {
            buckets = AvailableByCategoryRef(table);
        }
        catch
        {
            return;
        }

        if (buckets == null)
            return;

        var need = Math.Max(VanillaCategoryBucketCount, (int)Piece.PieceCategory.Max);
        while (buckets.Count < need)
            buckets.Add(new List<Piece>());
    }

    internal static void ClampSelectedCategory(PieceTable table)
    {
        if (table == null || SelectedCategoryRef == null)
            return;

        try
        {
            var buckets = TryGetBuckets(table);
            var cat = SelectedCategoryRef(table);
            var idx = (int)cat;
            if (cat == Piece.PieceCategory.Max ||
                cat == Piece.PieceCategory.All ||
                idx < 0 ||
                (buckets != null && buckets.Count > 0 && idx >= buckets.Count))
            {
                SelectedCategoryRef(table) = Piece.PieceCategory.Misc;
            }
        }
        catch
        {
            /* ignore */
        }
    }

    private static void EnsureSelectionArrays(PieceTable table)
    {
        try
        {
            var buckets = TryGetBuckets(table);
            var n = Math.Max(buckets?.Count ?? 0, VanillaCategoryBucketCount);
            if (n <= 0)
                n = VanillaCategoryBucketCount;

            // public on 1.0
            if (table.m_selectedPiece == null || table.m_selectedPiece.Length < n)
                Array.Resize(ref table.m_selectedPiece, n);
            if (table.m_lastSelectedPiece == null || table.m_lastSelectedPiece.Length < n)
                Array.Resize(ref table.m_lastSelectedPiece, n);
        }
        catch
        {
            /* ignore */
        }
    }

    private static List<List<Piece>>? TryGetBuckets(PieceTable table)
    {
        if (AvailableByCategoryRef == null || table == null)
            return null;
        try
        {
            return AvailableByCategoryRef(table);
        }
        catch
        {
            return null;
        }
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
        /// Must never throw — exceptions here abort Hud.UpdateBuild mid-frame and scramble the HUD.
        /// </summary>
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        [HarmonyPatch(typeof(PieceTable), nameof(PieceTable.GetAvailablePiecesInCategory))]
        private static void GetAvailablePiecesInCategory_Prefix(PieceTable __instance)
        {
            try
            {
                if (__instance == null)
                    return;

                var buckets = TryGetBuckets(__instance);
                if (buckets == null || buckets.Count == 0)
                {
                    EnsureCategoryBuckets(__instance);
                    ClampSelectedCategory(__instance);
                }
            }
            catch
            {
                /* swallow — prefer vanilla crash over HUD corruption from our patch */
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
            try
            {
                if (__instance == null)
                {
                    __result = new List<Piece>();
                    return false;
                }

                EnsureCategoryBuckets(__instance);
                ClampSelectedCategory(__instance);

                var buckets = TryGetBuckets(__instance);
                if (buckets == null || buckets.Count == 0)
                    return true;

                var idx = (int)__instance.GetSelectedCategory();
                if (idx < 0 || idx >= buckets.Count)
                {
                    __result = buckets[0] ?? new List<Piece>();
                    return false;
                }

                return true;
            }
            catch
            {
                return true;
            }
        }
    }
}
