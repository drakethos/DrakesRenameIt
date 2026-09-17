using System;
using BepInEx.Logging;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;

namespace DrakeRenameit;

/// <summary>
/// Hammer d├⌐cor only: blank sheet upright/flat (1 paper) + paper stack (50 paper).
/// Pure build pieces ΓÇö no Sign interaction.
/// </summary>
internal static class PaperPlace
{
    internal const string BlankUpright = "Drakes_PaperBlank_Upright";
    internal const string BlankLaying = "Drakes_PaperBlank_Laying";
    internal const string PaperStack = "Drakes_PaperStack";

    private static ManualLogSource? _log;
    private static Piece? _uprightPiece;
    private static Piece? _flatPiece;
    private static Piece? _stackPiece;

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

            AddLocalization();
            var icon = PaperItem.GetBlankIconSprite();

            // Sign clone for all sheets ΓÇö wood_stack acts like a terrain floor pile.
            RegisterSheet(BlankUpright, "sign", "$piece_drakes_paper_blank_u", "$piece_drakes_paper_blank_u_desc",
                wall: true, cost: 1, icon, RenameitConfig.VerticalPaperPlaceable);

            RegisterSheet(BlankLaying, "sign", "$piece_drakes_paper_blank_l", "$piece_drakes_paper_blank_l_desc",
                wall: false, cost: 1, icon, RenameitConfig.HorizontalPaperPlaceable);

            RegisterStack(icon, RenameitConfig.StackPaperPlaceable);

            RenameitConfig.HammerPaperPlaceableChanged -= ApplyHammerPlaceableFlags;
            RenameitConfig.HammerPaperPlaceableChanged += ApplyHammerPlaceableFlags;
            ApplyHammerPlaceableFlags();

            _log?.LogInfo(
                "[Paper] Hammer d├⌐cor: " +
                $"upright={(RenameitConfig.VerticalPaperPlaceable ? "on" : "off")}, " +
                $"flat={(RenameitConfig.HorizontalPaperPlaceable ? "on" : "off")}, " +
                $"stack={(RenameitConfig.StackPaperPlaceable ? "on" : "off")}.");
        }
        catch (Exception ex)
        {
            _log?.LogError($"[Paper] Place register failed: {ex}");
        }
    }

    private static void RegisterSheet(
        string prefab,
        string clone,
        string nameTok,
        string descTok,
        bool wall,
        int cost,
        Sprite? icon,
        bool placeable)
    {
        var config = new PieceConfig
        {
            Name = nameTok,
            Description = descTok,
            // Always register the prefab (written notes clone it). Hammer visibility is m_enabled.
            Enabled = true,
            PieceTable = "Hammer",
            Category = "Furniture",
            // No crafting station — place with hammer anywhere (paper cost still applies).
            CraftingStation = "",
            Requirements = new[]
            {
                new RequirementConfig(PaperItem.PrefabName, cost, 0, true),
            },
        };
        if (icon != null)
            config.Icon = icon;

        var piece = new CustomPiece(prefab, clone, config);
        PieceManager.Instance.AddPiece(piece);
        var go = piece.PiecePrefab;
        if (go == null)
            return;

        PaperItem.BuildDecorSheetVisual(go, wall);
        SanitizeDecorPiece(go, nameTok);
        ClearPieceCraftingStation(go);
        var placed = go.GetComponent<Piece>();
        if (wall)
            _uprightPiece = placed;
        else
            _flatPiece = placed;
        SetHammerPlaceable(go, placeable);
    }

    private static void RegisterStack(Sprite? icon, bool placeable)
    {
        var stackCost = Math.Max(1, RenameitConfig.BlankPaperStackSize);
        var config = new PieceConfig
        {
            Name = "$piece_drakes_paper_stack",
            Description = "$piece_drakes_paper_stack_desc",
            Enabled = true,
            PieceTable = "Hammer",
            Category = "Furniture",
            // No crafting station — place with hammer anywhere (paper cost still applies).
            CraftingStation = "",
            Requirements = new[]
            {
                new RequirementConfig(PaperItem.PrefabName, stackCost, 0, true),
            },
        };
        if (icon != null)
            config.Icon = icon;

        var piece = new CustomPiece(PaperStack, "sign", config);
        PieceManager.Instance.AddPiece(piece);
        var go = piece.PiecePrefab;
        if (go == null)
            return;

        PaperItem.BuildPaperStackVisual(go);
        SanitizeDecorPiece(go, "$piece_drakes_paper_stack");
        ClearPieceCraftingStation(go);
        _stackPiece = go.GetComponent<Piece>();
        SetHammerPlaceable(go, placeable);
    }

    /// <summary>Sign donor can keep a workbench link; blank paper décor must not require one.</summary>
    private static void ClearPieceCraftingStation(GameObject go)
    {
        var piece = go != null ? go.GetComponent<Piece>() : null;
        if (piece != null)
            piece.m_craftingStation = null;
    }

    private static void ApplyHammerPlaceableFlags()
    {
        SetHammerPlaceablePiece(_uprightPiece, RenameitConfig.VerticalPaperPlaceable);
        SetHammerPlaceablePiece(_flatPiece, RenameitConfig.HorizontalPaperPlaceable);
        SetHammerPlaceablePiece(_stackPiece, RenameitConfig.StackPaperPlaceable);
    }

    private static void SetHammerPlaceablePiece(Piece? piece, bool placeable)
    {
        if (piece != null)
            piece.m_enabled = placeable;
    }

    /// <summary>Keep the prefab; hide it from the hammer when the placeable flag is off.</summary>
    private static void SetHammerPlaceable(GameObject go, bool placeable)
    {
        var piece = go.GetComponent<Piece>();
        if (piece != null)
            piece.m_enabled = placeable;
    }

    /// <summary>Remove leftover interactables / containers from donor so it is bric-a-brac only.</summary>
    private static void SanitizeDecorPiece(GameObject go, string nameTok)
    {
        foreach (var sign in go.GetComponentsInChildren<Sign>(true))
            UnityEngine.Object.DestroyImmediate(sign);

        foreach (var c in go.GetComponentsInChildren<Container>(true))
            UnityEngine.Object.DestroyImmediate(c);

        // Hoverable/Interactable on donor (if any) ΓÇö destroy common components carefully.
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

        loc.AddTranslation("English", "piece_drakes_paper_blank_u", "Blank Paper (wall)");
        loc.AddTranslation("English", "piece_drakes_paper_blank_u_desc", "Hang a blank sheet on a wall. Costs 1 Piece of Paper.");
        loc.AddTranslation("English", "piece_drakes_paper_blank_l", "Blank Paper (flat)");
        loc.AddTranslation("English", "piece_drakes_paper_blank_l_desc", "Lay a blank sheet flat on a table or floor. Costs 1 Piece of Paper.");
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
