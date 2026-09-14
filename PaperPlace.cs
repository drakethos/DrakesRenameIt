using System;
using BepInEx.Logging;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;

namespace DrakeRenameit;

/// <summary>
/// Hammer décor: bundle blank sheet (flat + coded vertical) + bundle paper stack.
/// Pure build pieces — no Sign interaction.
/// </summary>
internal static class PaperPlace
{
    internal const string BlankUpright = "Drakes_PaperBlank_Upright";
    internal const string BlankLaying = "Drakes_PaperBlank_Laying";
    internal const string PaperStack = "Drakes_PaperStack";

    private static ManualLogSource? _log;

    internal static void Register(ManualLogSource log)
    {
        _log = log;
        PrefabManager.OnVanillaPrefabsAvailable -= AddPieces;
        PrefabManager.OnVanillaPrefabsAvailable += AddPieces;
    }

    private static void AddPieces()
    {
        PrefabManager.OnVanillaPrefabsAvailable -= AddPieces;

        try
        {
            if (!RenameitConfig.PaperEnabled || !RenameitConfig.PaperPlaceEnabled)
            {
                _log?.LogInfo("[Paper] Place pieces skipped (PaperEnabled/PaperPlaceEnabled off).");
                return;
            }

            if (!PaperAssets.TryEnsureLoaded())
                return;

            AddLocalization();
            var icon = PaperItem.GetBlankIconSprite();

            RegisterSheet(BlankUpright, PaperAssets.BlankPiece, "$piece_drakes_paper_blank_u",
                "$piece_drakes_paper_blank_u_desc", wall: true, cost: 1, icon);

            RegisterSheet(BlankLaying, PaperAssets.BlankPiece, "$piece_drakes_paper_blank_l",
                "$piece_drakes_paper_blank_l_desc", wall: false, cost: 1, icon);

            RegisterStack(icon);

            _log?.LogInfo("[Paper] Hammer décor from bundle: upright (rotated), flat, stack.");
        }
        catch (Exception ex)
        {
            _log?.LogError($"[Paper] Place register failed: {ex}");
        }
    }

    private static void RegisterSheet(
        string prefab,
        string assetName,
        string nameTok,
        string descTok,
        bool wall,
        int cost,
        Sprite? icon)
    {
        var go = PaperAssets.CreatePrefab(assetName, prefab);
        if (go == null)
            return;

        var config = new PieceConfig
        {
            Name = nameTok,
            Description = descTok,
            Enabled = true,
            PieceTable = "Hammer",
            Category = "Furniture",
            CraftingStation = "piece_workbench",
            Requirements = new[]
            {
                new RequirementConfig(PaperItem.PrefabName, cost, 0, true),
            },
        };
        if (icon != null)
            config.Icon = icon;

        var piece = new CustomPiece(go, false, config);
        PieceManager.Instance.AddPiece(piece);
        var pieceGo = piece.PiecePrefab;
        if (pieceGo == null)
            return;

        PaperItem.PrepareSheetPiece(pieceGo, wall);
        SanitizeDecorPiece(pieceGo, nameTok);
    }

    private static void RegisterStack(Sprite? icon)
    {
        var go = PaperAssets.CreatePrefab(PaperAssets.BlankStackPiece, PaperStack);
        if (go == null)
            return;

        var stackCost = Math.Max(1, RenameitConfig.BlankPaperStackSize);
        var config = new PieceConfig
        {
            Name = "$piece_drakes_paper_stack",
            Description = "$piece_drakes_paper_stack_desc",
            Enabled = true,
            PieceTable = "Hammer",
            Category = "Furniture",
            CraftingStation = "piece_workbench",
            Requirements = new[]
            {
                new RequirementConfig(PaperItem.PrefabName, stackCost, 0, true),
            },
        };
        if (icon != null)
            config.Icon = icon;

        var piece = new CustomPiece(go, false, config);
        PieceManager.Instance.AddPiece(piece);
        var pieceGo = piece.PiecePrefab;
        if (pieceGo == null)
            return;

        PaperItem.PrepareStackPiece(pieceGo);
        SanitizeDecorPiece(pieceGo, "$piece_drakes_paper_stack");
    }

    /// <summary>Remove leftover interactables / containers from donor so it is bric-a-brac only.</summary>
    private static void SanitizeDecorPiece(GameObject go, string nameTok)
    {
        foreach (var sign in go.GetComponentsInChildren<Sign>(true))
            UnityEngine.Object.DestroyImmediate(sign);

        foreach (var c in go.GetComponentsInChildren<Container>(true))
            UnityEngine.Object.DestroyImmediate(c);

        foreach (var mb in go.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (mb == null)
                continue;
            var n = mb.GetType().Name;
            if (n is "Sign" or "Container" or "ItemDrop" or "Smelter" or "Fireplace")
                UnityEngine.Object.DestroyImmediate(mb);
        }

        var piece = go.GetComponent<Piece>();
        if (piece != null)
            piece.m_name = nameTok;
    }

    private static void AddLocalization()
    {
        var loc = LocalizationManager.Instance.GetLocalization();
        var stack = Math.Max(1, RenameitConfig.BlankPaperStackSize);

        loc.AddTranslation("English", "piece_drakes_paper_blank_u", "Blank Paper (upright)");
        loc.AddTranslation("English", "piece_drakes_paper_blank_u_desc", "Decorative blank sheet for a wall. Costs 1 Piece of Paper.");
        loc.AddTranslation("English", "piece_drakes_paper_blank_l", "Blank Paper (flat)");
        loc.AddTranslation("English", "piece_drakes_paper_blank_l_desc", "Decorative blank sheet laid flat. Costs 1 Piece of Paper.");
        loc.AddTranslation("English", "piece_drakes_paper_stack", "Paper Stack");
        loc.AddTranslation("English", "piece_drakes_paper_stack_desc",
            $"A tidy pile of blank paper. Costs {stack} Piece of Paper.");

        loc.AddTranslation("Spanish", "piece_drakes_paper_blank_u", "Papel en blanco (vertical)");
        loc.AddTranslation("Spanish", "piece_drakes_paper_blank_u_desc", "Hoja decorativa en la pared. Cuesta 1 Piece of Paper.");
        loc.AddTranslation("Spanish", "piece_drakes_paper_blank_l", "Papel en blanco (plano)");
        loc.AddTranslation("Spanish", "piece_drakes_paper_blank_l_desc", "Hoja decorativa plana. Cuesta 1 Piece of Paper.");
        loc.AddTranslation("Spanish", "piece_drakes_paper_stack", "Pila de papel");
        loc.AddTranslation("Spanish", "piece_drakes_paper_stack_desc",
            $"Una pila de papel en blanco. Cuesta {stack} Piece of Paper.");
    }
}
