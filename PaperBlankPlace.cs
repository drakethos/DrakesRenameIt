using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Logging;
using HarmonyLib;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;

namespace DrakeRenameit;

/// <summary>
/// Blank Piece of Paper → hotbar place (vertical / flat). Orientation comes from
/// <see cref="RenameitConfig.BlankPaperPlaceOrientation"/>. Use-again toggles only on Both.
/// Cost is one sheet from the triggering stack; hammer-remove refunds it.
/// </summary>
internal static class PaperBlankPlace
{
    internal const string PieceTableName = "DrakesBlankPaper";
    internal const string PlaceVertical = "Drakes_PaperBlank_UseVertical";
    internal const string PlaceFlat = "Drakes_PaperBlank_UseFlat";

    private static ManualLogSource? _log;
    private static ItemDrop.ItemData? _triggerPaper;
    private static bool _placeArmed;
    private static ItemDrop.ItemData? _consumeAfterPlacement;
    private static bool _endPlaceAfterPlacement;
    private static int _orientIndex;
    private static Piece.Requirement[] _refund = Array.Empty<Piece.Requirement>();
    private static Piece? _swapPiece;
    private static Piece.Requirement[]? _swapSaved;

    private static readonly FieldInfo? BuildPiecesField =
        AccessTools.Field(typeof(Player), "m_buildPieces");

    private static readonly MethodInfo? SetPlaceModeMethod =
        AccessTools.Method(typeof(Player), "SetPlaceMode", new[] { typeof(PieceTable) });

    private static readonly MethodInfo? GetRightItemMethod =
        AccessTools.Method(typeof(Humanoid), "GetRightItem");

    private static readonly MethodInfo? EquipItemMethod =
        AccessTools.Method(typeof(Humanoid), "EquipItem", new[] { typeof(ItemDrop.ItemData), typeof(bool) })
        ?? AccessTools.Method(typeof(Humanoid), "EquipItem");

    private static readonly MethodInfo? UnequipItemMethod =
        AccessTools.Method(typeof(Humanoid), "UnequipItem", new[] { typeof(ItemDrop.ItemData), typeof(bool) })
        ?? AccessTools.Method(typeof(Humanoid), "UnequipItem");

    private static readonly FieldInfo? PieceTablePiecesField =
        AccessTools.Field(typeof(PieceTable), "m_pieces");

    private static readonly MethodInfo? SetSelectedPieceMethod =
        AccessTools.Method(typeof(Player), "SetSelectedPiece", new[] { typeof(Piece) });

    private static readonly MethodInfo? HideHandItemsMethod =
        AccessTools.Method(typeof(Humanoid), "HideHandItems");

    internal static bool IsInPlaceMode(Player? player)
    {
        var table = PieceManager.Instance?.GetPieceTable(PieceTableName);
        if (table == null || player == null || BuildPiecesField == null)
            return false;
        return ReferenceEquals(BuildPiecesField.GetValue(player) as PieceTable, table);
    }

    internal static bool IsBlankPlacePiece(Component? c)
    {
        if (c == null)
            return false;
        var n = c.gameObject.name;
        return n.StartsWith(PlaceVertical, StringComparison.OrdinalIgnoreCase) ||
               n.StartsWith(PlaceFlat, StringComparison.OrdinalIgnoreCase);
    }

    internal static void Register(ManualLogSource log)
    {
        _log = log;
        PrefabManager.OnVanillaPrefabsAvailable -= Add;
        PrefabManager.OnVanillaPrefabsAvailable += Add;
    }

    private static void Add()
    {
        PrefabManager.OnVanillaPrefabsAvailable -= Add;
        try
        {
            if (!RenameitConfig.PaperEnabled || !RenameitConfig.PaperPlaceEnabled)
                return;

            _refund = BuildRefund();

            var table = new CustomPieceTable(PieceTableName, new PieceTableConfig
            {
                UseCategories = false,
                CanRemovePieces = true,
            });
            PieceManager.Instance.AddPieceTable(table);

            // Both sheets always exist. The dropdown filters selection at place time so a
            // server sync of Vertical Only / Horizontal Only / Both applies without a restart.
            var icon = PaperItem.GetBlankIconSprite();
            RegisterSheet(PlaceVertical, wall: true, icon);
            RegisterSheet(PlaceFlat, wall: false, icon);

            AttachTableToBlankItem();
            _log?.LogInfo(
                $"[Paper] Blank hotbar place ready ({RenameitConfig.BlankPaperPlaceOrientation}).");
        }
        catch (Exception ex)
        {
            _log?.LogError($"[Paper] Blank place register failed: {ex}");
        }
    }

    private static void RegisterSheet(string prefab, bool wall, Sprite? icon)
    {
        var nameTok = wall ? "$piece_drakes_paper_blank_u" : "$piece_drakes_paper_blank_l";
        var descTok = wall ? "$piece_drakes_paper_blank_u_desc" : "$piece_drakes_paper_blank_l_desc";
        var config = new PieceConfig
        {
            Name = nameTok,
            Description = descTok,
            Enabled = true,
            PieceTable = PieceTableName,
            Category = "Misc",
            Requirements = Array.Empty<RequirementConfig>(),
        };
        if (icon != null)
            config.Icon = icon;

        var clone = wall ? PaperPlace.BlankUpright : PaperPlace.BlankLaying;
        if (PrefabManager.Instance.GetPrefab(clone) == null)
            clone = "sign";

        var piece = new CustomPiece(prefab, clone, config);
        PieceManager.Instance.AddPiece(piece);
        var go = piece.PiecePrefab;
        if (go == null)
            return;

        var p = go.GetComponent<Piece>();
        if (p != null)
            p.m_resources = _refund;

        if (go.GetComponent<PaperBlankRefund>() == null)
            go.AddComponent<PaperBlankRefund>();
    }

    private static Piece.Requirement[] BuildRefund()
    {
        var prefab = PrefabManager.Instance.GetPrefab(PaperItem.PrefabName)
                     ?? ObjectDB.instance?.GetItemPrefab(PaperItem.PrefabName);
        var drop = prefab?.GetComponent<ItemDrop>();
        if (drop == null)
        {
            _log?.LogWarning("[Paper] Blank place refund missing paper prefab.");
            return Array.Empty<Piece.Requirement>();
        }

        return new[]
        {
            new Piece.Requirement
            {
                m_resItem = drop,
                m_amount = 1,
                m_amountPerLevel = 0,
                m_recover = true,
            },
        };
    }

    private static void AttachTableToBlankItem()
    {
        var prefab = PrefabManager.Instance.GetPrefab(PaperItem.PrefabName)
                     ?? ObjectDB.instance?.GetItemPrefab(PaperItem.PrefabName);
        var shared = prefab?.GetComponent<ItemDrop>()?.m_itemData?.m_shared;
        if (shared == null)
            return;

        var table = PieceManager.Instance.GetPieceTable(PieceTableName);
        if (table == null)
            return;

        shared.m_buildPieces = table;
        // Same as Written Page: UpdatePlacement NREs if the right-hand item has no build table.
        shared.m_itemType = ItemDrop.ItemData.ItemType.Tool;
        if (shared.m_attack == null)
            shared.m_attack = new Attack();
        shared.m_attack.m_attackStamina = 0f;
    }

    private static void BeginPlace(ItemDrop.ItemData item)
    {
        if (!PaperItem.IsBlankPaper(item) || !RenameitConfig.PaperPlaceEnabled)
            return;
        var player = Player.m_localPlayer;
        if (player == null || !DrakeRenameit.IsItemInLocalPlayerInventory(item))
            return;

        AttachTableToBlankItem();
        var table = item.m_shared?.m_buildPieces
                    ?? PieceManager.Instance.GetPieceTable(PieceTableName);
        if (table == null)
        {
            _log?.LogWarning("[Paper] Blank place table missing.");
            return;
        }

        if (!EnsurePaperIsBuildTool(player, item))
        {
            player.Message(
                MessageHud.MessageType.Center,
                "Put the Piece of Paper on your hotbar, then Use it to place.");
            return;
        }

        _triggerPaper = item;
        _orientIndex = AllowedOrientIndex();
        InventoryGui.instance?.Hide();

        if (SetPlaceModeMethod != null)
            SetPlaceModeMethod.Invoke(player, new object[] { table });
        else
            player.SetPlaceMode(table);

        SelectOrientation(player, _orientIndex);
        player.Message(MessageHud.MessageType.TopLeft, PlaceStatus(_orientIndex));
    }

    private static int AllowedOrientIndex() =>
        RenameitConfig.BlankPaperPlaceHorizontal && !RenameitConfig.BlankPaperPlaceVertical ? 1 : 0;

    private static string PlaceStatus(int orient)
    {
        var label = orient == 0 ? "Wall (vertical)" : "Flat";
        return RenameitConfig.BlankPaperPlaceCanToggle
            ? $"Place: {label} — Use again to switch Wall/Flat."
            : $"Place: {label}";
    }

    private static void CycleOrientation(Player player)
    {
        if (!RenameitConfig.BlankPaperPlaceCanToggle)
            return;

        _orientIndex = _orientIndex == 0 ? 1 : 0;
        SelectOrientation(player, _orientIndex);
        player.Message(MessageHud.MessageType.TopLeft, PlaceStatus(_orientIndex));
    }

    private static void SelectOrientation(Player player, int orient)
    {
        var table = PieceManager.Instance.GetPieceTable(PieceTableName);
        var pieces = GetTablePieces(table);
        if (pieces == null || pieces.Count == 0)
            return;

        var want = orient == 0 ? PlaceVertical : PlaceFlat;
        Piece? fallback = null;
        foreach (var go in pieces)
        {
            if (go == null)
                continue;
            var p = go.GetComponent<Piece>();
            if (p == null)
                continue;
            fallback ??= p;
            if (go.name.StartsWith(want, StringComparison.OrdinalIgnoreCase))
            {
                InvSetSelectedPiece(player, p);
                return;
            }
        }

        if (fallback != null)
            InvSetSelectedPiece(player, fallback);
    }

    private static List<GameObject>? GetTablePieces(PieceTable? table)
    {
        if (table == null)
            return null;
        try
        {
            if (PieceTablePiecesField != null)
                return PieceTablePiecesField.GetValue(table) as List<GameObject>;
        }
        catch (Exception ex)
        {
            _log?.LogWarning($"[Paper] Blank PieceTable.m_pieces access failed: {ex.Message}");
        }

        try
        {
            return table.m_pieces;
        }
        catch
        {
            return null;
        }
    }

    private static void InvSetSelectedPiece(Player player, Piece piece)
    {
        if (player == null || piece == null)
            return;
        try
        {
            if (SetSelectedPieceMethod != null)
                SetSelectedPieceMethod.Invoke(player, new object[] { piece });
            else
                player.SetSelectedPiece(piece);
        }
        catch (Exception ex)
        {
            _log?.LogWarning($"[Paper] Blank SetSelectedPiece failed: {ex.InnerException?.Message ?? ex.Message}");
        }
    }

    private static ItemDrop.ItemData? InvGetRightItem(Humanoid humanoid)
    {
        if (humanoid == null || GetRightItemMethod == null)
            return null;
        try
        {
            return GetRightItemMethod.Invoke(humanoid, null) as ItemDrop.ItemData;
        }
        catch
        {
            return null;
        }
    }

    private static bool InvEquipItem(Humanoid humanoid, ItemDrop.ItemData item)
    {
        if (humanoid == null || item == null || EquipItemMethod == null)
            return false;
        try
        {
            var result = EquipItemMethod.Invoke(humanoid, EquipItemMethod.GetParameters().Length >= 2
                ? new object[] { item, false }
                : new object[] { item });
            return result is not bool b || b;
        }
        catch (Exception ex)
        {
            _log?.LogWarning($"[Paper] Blank EquipItem failed: {ex.InnerException?.Message ?? ex.Message}");
            return false;
        }
    }

    private static void InvUnequipItem(Humanoid humanoid, ItemDrop.ItemData item)
    {
        if (humanoid == null || item == null || UnequipItemMethod == null)
            return;
        try
        {
            UnequipItemMethod.Invoke(humanoid, UnequipItemMethod.GetParameters().Length >= 2
                ? new object[] { item, false }
                : new object[] { item });
        }
        catch
        {
            /* ignore */
        }
    }

    private static bool EnsurePaperIsBuildTool(Player player, ItemDrop.ItemData item)
    {
        if (item?.m_shared == null)
            return false;

        var table = PieceManager.Instance.GetPieceTable(PieceTableName);
        if (table != null)
            item.m_shared.m_buildPieces = table;

        var right = InvGetRightItem(player);
        if (ReferenceEquals(right, item) && right.m_shared?.m_buildPieces != null)
            return true;

        try
        {
            if (right != null)
                InvUnequipItem(player, right);
            InvEquipItem(player, item);
            right = InvGetRightItem(player);
            return right != null &&
                   PaperItem.IsBlankPaper(right) &&
                   right.m_shared?.m_buildPieces != null;
        }
        catch (Exception ex)
        {
            _log?.LogWarning($"[Paper] Equip blank for place failed: {ex.Message}");
            return false;
        }
    }

    private static void EndPlaceMode(Player player)
    {
        try
        {
            if (SetPlaceModeMethod != null)
                SetPlaceModeMethod.Invoke(player, new object?[] { null });
            else
                player.SetPlaceMode(null!);
        }
        catch
        {
            /* ignore */
        }
    }

    private static void ConsumeTrigger(Player player, ItemDrop.ItemData? source)
    {
        if (source == null || player == null)
            return;

        InvUnequipItem(player, source);
        var inv = player.GetInventory();
        if (inv != null && inv.ContainsItem(source))
        {
            if (source.m_stack > 1)
                source.m_stack -= 1;
            else
                inv.RemoveItem(source);
        }

        try
        {
            HideHandItemsMethod?.Invoke(player, null);
        }
        catch
        {
            /* ignore */
        }
    }

    private static void SwapOutRequirements(Piece piece)
    {
        _swapPiece = piece;
        _swapSaved = piece.m_resources;
        piece.m_resources = Array.Empty<Piece.Requirement>();
    }

    private static void RestoreRequirements()
    {
        if (_swapPiece != null && _swapSaved != null)
            _swapPiece.m_resources = _swapSaved;
        _swapPiece = null;
        _swapSaved = null;
    }

    private static bool IsFreeBuild(Piece? piece)
    {
        var zs = ZoneSystem.instance;
        if (zs == null || piece == null)
            return false;
        try
        {
            return zs.GetGlobalKey(piece.FreeBuildKey());
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Copies refund requirements onto the placed instance (prefab is emptied during PlacePiece).</summary>
    internal sealed class PaperBlankRefund : MonoBehaviour
    {
        private void Awake()
        {
            var piece = GetComponent<Piece>();
            if (piece == null || _refund.Length == 0)
                return;
            if (piece.m_resources != null && piece.m_resources.Length > 0)
                return;
            piece.m_resources = _refund;
        }
    }

    [HarmonyPatch]
    private static class Patches
    {
        [HarmonyPrefix]
        [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.UseItem))]
        private static bool UseItem_Prefix(Humanoid __instance, ItemDrop.ItemData item)
        {
            if (__instance != Player.m_localPlayer || item == null)
                return true;
            if (!RenameitConfig.PaperPlaceEnabled || !PaperItem.IsBlankPaper(item))
                return true;

            var player = Player.m_localPlayer;
            if (player != null && IsInPlaceMode(player))
            {
                // Toggle only when both orientations are allowed. One-or-the-other: Use does nothing.
                CycleOrientation(player);
                return false;
            }

            BeginPlace(item);
            return false;
        }

        [HarmonyPrefix]
        [HarmonyPriority(Priority.High)]
        [HarmonyPatch(typeof(Player), "UpdatePlacement", new[] { typeof(bool), typeof(float) })]
        private static bool UpdatePlacement_Prefix(Player __instance)
        {
            if (__instance != Player.m_localPlayer || !IsInPlaceMode(__instance))
                return true;

            if (!RenameitConfig.BlankPaperPlaceCanToggle)
                SelectOrientation(__instance, AllowedOrientIndex());

            var right = InvGetRightItem(__instance);
            if (right?.m_shared?.m_buildPieces != null)
                return true;

            if (_triggerPaper != null &&
                DrakeRenameit.IsItemInLocalPlayerInventory(_triggerPaper) &&
                EnsurePaperIsBuildTool(__instance, _triggerPaper) &&
                InvGetRightItem(__instance)?.m_shared?.m_buildPieces != null)
                return true;

            EndPlaceMode(__instance);
            _triggerPaper = null;
            return false;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Player), "UpdatePlacement", new[] { typeof(bool), typeof(float) })]
        private static void UpdatePlacement_Postfix(Player __instance)
        {
            if (__instance != Player.m_localPlayer)
                return;
            if (!_endPlaceAfterPlacement && _consumeAfterPlacement == null)
                return;

            var toConsume = _consumeAfterPlacement;
            _consumeAfterPlacement = null;
            _endPlaceAfterPlacement = false;

            EndPlaceMode(__instance);
            ConsumeTrigger(__instance, toConsume);
            _triggerPaper = null;
        }

        private static int _blanksBefore;

        private static int CountPlacedBlanks()
        {
            var n = 0;
            var found = UnityEngine.Object.FindObjectsOfType<PaperBlankRefund>();
            for (var i = 0; i < found.Length; i++)
            {
                if (found[i] != null && found[i].gameObject.activeInHierarchy)
                    n++;
            }
            return n;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Player), "PlacePiece", new[]
        {
            typeof(Piece), typeof(Vector3), typeof(Quaternion), typeof(bool), typeof(bool)
        })]
        private static bool PlacePiece_Prefix(Player __instance, Piece piece)
        {
            _placeArmed = false;
            if (__instance != Player.m_localPlayer || piece == null || !IsBlankPlacePiece(piece))
                return true;
            if (_triggerPaper == null || !DrakeRenameit.IsItemInLocalPlayerInventory(_triggerPaper))
            {
                __instance.Message(MessageHud.MessageType.Center, "Use a Piece of Paper to place this.");
                return false;
            }

            // Empty cost for this call so vanilla doesn't eat the equipped stack mid-UpdatePlacement.
            // PlacePiece is void — a bool __result postfix never binds and aborts other place patches.
            SwapOutRequirements(piece);
            _blanksBefore = CountPlacedBlanks();
            _placeArmed = true;
            return true;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Player), "PlacePiece", new[]
        {
            typeof(Piece), typeof(Vector3), typeof(Quaternion), typeof(bool), typeof(bool)
        })]
        private static void PlacePiece_Postfix(Player __instance, Piece piece)
        {
            RestoreRequirements();
            if (__instance != Player.m_localPlayer || !_placeArmed)
                return;

            _placeArmed = false;
            if (piece == null || !IsBlankPlacePiece(piece))
                return;
            if (IsFreeBuild(piece))
                return;
            if (CountPlacedBlanks() <= _blanksBefore)
                return;

            _consumeAfterPlacement = _triggerPaper;
            _endPlaceAfterPlacement = true;
        }

        [HarmonyFinalizer]
        [HarmonyPatch(typeof(Player), "PlacePiece", new[]
        {
            typeof(Piece), typeof(Vector3), typeof(Quaternion), typeof(bool), typeof(bool)
        })]
        private static Exception? PlacePiece_Finalizer(Exception? __exception)
        {
            RestoreRequirements();
            return __exception;
        }
    }
}
