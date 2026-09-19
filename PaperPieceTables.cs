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
/// <para>
/// Field access is via <see cref="AccessTools"/> so CI (Pfhoenix stubs) can compile without
/// the Valheim 1.0 field names while the live game still hardens correctly.
/// </para>
/// </summary>
internal static class PaperPieceTables
{
    private const int VanillaCategoryBucketCount = 9;

    private static readonly MethodInfo? UpdateAvailablePiecesListMethod =
        AccessTools.DeclaredMethod(typeof(Player), "UpdateAvailablePiecesList")
        ?? AccessTools.Method(typeof(Player), "UpdateAvailablePiecesList");

    /// <summary>Valheim 1.0 name; older stubs had <c>m_availablePieces</c> as List&lt;List&lt;Piece&gt;&gt;.</summary>
    private static readonly FieldInfo? AvailableByCategoryField =
        AccessTools.Field(typeof(PieceTable), "m_availablePiecesByCategory")
        ?? AccessTools.Field(typeof(PieceTable), "m_availablePieces");

    private static readonly FieldInfo? HideAdvancedMenuField =
        AccessTools.Field(typeof(PieceTable), "m_hideAdvancedMenu");

    /// <summary>Prepare a custom paper place table for Valheim 1.0 build HUD.</summary>
    internal static void Harden(PieceTable? table)
    {
        if (table == null)
            return;

        EnsureCategoryBuckets(table);

        // Simplified menu: paper tables only have wall/flat sheets (1.0 field; no-op on older stubs).
        if (HideAdvancedMenuField != null)
        {
            try
            {
                HideAdvancedMenuField.SetValue(table, true);
            }
            catch
            {
                /* ignore */
            }
        }

        table.m_canRemovePieces = true;

        ClampSelectedCategory(table);
        EnsureSelectionArrays(table);

        var go = table.gameObject;
        if (go != null)
            UnityEngine.Object.DontDestroyOnLoad(go);
    }

    /// <summary>
    /// Grow the per-category bucket list to at least the vanilla Max count so indexing never uses -1.
    /// </summary>
    internal static void EnsureCategoryBuckets(PieceTable table)
    {
        if (table == null)
            return;

        var buckets = GetAvailableByCategory(table);
        if (buckets == null)
            return;

        var need = Math.Max(VanillaCategoryBucketCount, (int)Piece.PieceCategory.Max);
        while (buckets.Count < need)
            buckets.Add(new List<Piece>());
    }

    internal static void ClampSelectedCategory(PieceTable table)
    {
        if (table == null)
            return;

        var buckets = GetAvailableByCategory(table);
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
        var buckets = GetAvailableByCategory(table);
        var n = Math.Max(buckets?.Count ?? 0, VanillaCategoryBucketCount);
        if (n <= 0)
            n = VanillaCategoryBucketCount;

        if (table.m_selectedPiece == null || table.m_selectedPiece.Length < n)
            Array.Resize(ref table.m_selectedPiece, n);
        if (table.m_lastSelectedPiece == null || table.m_lastSelectedPiece.Length < n)
            Array.Resize(ref table.m_lastSelectedPiece, n);
    }

    private static List<List<Piece>>? GetAvailableByCategory(PieceTable table)
    {
        if (AvailableByCategoryField == null || table == null)
            return null;

        try
        {
            // Valheim 1.0: List<List<Piece>>. Pre-1.0 ComfyGizmo-era: also List<List<Piece>> under old name.
            // If the live field is HashSet&lt;Piece&gt; (1.0 m_availablePieces), skip — wrong shape.
            return AvailableByCategoryField.GetValue(table) as List<List<Piece>>;
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
        /// </summary>
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        [HarmonyPatch(typeof(PieceTable), nameof(PieceTable.GetAvailablePiecesInCategory))]
        private static void GetAvailablePiecesInCategory_Prefix(PieceTable __instance)
        {
            if (__instance == null)
                return;

            var buckets = GetAvailableByCategory(__instance);
            if (buckets == null || buckets.Count == 0)
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

            var buckets = GetAvailableByCategory(__instance);
            if (buckets == null || buckets.Count == 0)
            {
                // Field missing or wrong shape (stub / unexpected build) — let vanilla run.
                return true;
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
