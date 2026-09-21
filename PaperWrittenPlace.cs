using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Logging;
using DrakeModsLibs.API;
using DrakeRenameit.UI;
using HarmonyLib;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;

namespace DrakeRenameit;

/// <summary>
/// Written Page → Feaster-style piece table (vertical / flat). Consumes the triggering written stack.
/// [E] or break returns the original Written Page with customData.
/// </summary>
internal static class PaperWrittenPlace
{
    internal const string PieceTableName = "DrakesWrittenPaper";
    internal const string NoteVertical = "Drakes_PaperNote_Vertical";
    internal const string NoteFlat = "Drakes_PaperNote_Flat";

    private static ManualLogSource? _log;
    private static ItemDrop.ItemData? _triggerPaper;
    private static PaperSnapshot? _pendingSnapshot;
    private static bool _vesselAppliedSnapshot;
    /// <summary>Prefix armed a note place — Postfix must consume the Written Page.</summary>
    private static bool _notePlaceArmed;
    /// <summary>Consume/exit after UpdatePlacement returns — never mid-method (causes NRE).</summary>
    private static ItemDrop.ItemData? _consumeAfterPlacement;
    private static bool _endPlaceAfterPlacement;

    private static readonly FieldInfo? PlacementGhostField =
        AccessTools.Field(typeof(Player), "m_placementGhost");

    private static readonly MethodInfo? SetPlaceModeMethod =
        AccessTools.DeclaredMethod(typeof(Player), "SetPlaceMode", new[] { typeof(PieceTable) })
        ?? AccessTools.Method(typeof(Player), "SetPlaceMode", new[] { typeof(PieceTable) });

    private static readonly FieldInfo? BuildPiecesField =
        AccessTools.Field(typeof(Player), "m_buildPieces");

    // Publicized at compile-time, private at runtime — never call these directly.
    private static readonly MethodInfo? GetRightItemMethod =
        AccessTools.Method(typeof(Humanoid), "GetRightItem");

    private static readonly MethodInfo? EquipItemMethod =
        AccessTools.Method(typeof(Humanoid), "EquipItem", new[] { typeof(ItemDrop.ItemData), typeof(bool) })
        ?? AccessTools.Method(typeof(Humanoid), "EquipItem");

    private static readonly MethodInfo? UnequipItemMethod =
        AccessTools.Method(typeof(Humanoid), "UnequipItem", new[] { typeof(ItemDrop.ItemData), typeof(bool) })
        ?? AccessTools.Method(typeof(Humanoid), "UnequipItem");

    static readonly FieldInfo? QueuedAttackTimerField =
        AccessTools.Field(typeof(Player), "m_queuedAttackTimer");
    static readonly FieldInfo? QueuedSecondAttackTimerField =
        AccessTools.Field(typeof(Player), "m_queuedSecondAttackTimer");
    static readonly FieldInfo? AttackFlagField =
        AccessTools.Field(typeof(Character), "m_attack");
    static readonly FieldInfo? SecondaryAttackFlagField =
        AccessTools.Field(typeof(Character), "m_secondaryAttack");

    /// <summary>Block StartAttack until deferred consume finishes (cultivator-break punch).</summary>
    static float _suppressAttackUntil;
    /// <summary>Written page removed once Attack is released after a successful place.</summary>
    static ItemDrop.ItemData? _deferredConsume;
    static bool _deferConsumeArmed;

    static bool AttackStillHeld()
    {
        try
        {
            return ZInput.GetButton("Attack") || ZInput.GetButton("JoyPlace");
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Place click also queues StartAttack. Unequipping the page while Attack is still held
    /// is the cultivator-break case → unarmed punch. Clear the queue and suppress until settled.
    /// </summary>
    static void ClearPendingAttacksAfterPaperPlace(Player player)
    {
        try
        {
            QueuedAttackTimerField?.SetValue(player, 0f);
            QueuedSecondAttackTimerField?.SetValue(player, 0f);
            AttackFlagField?.SetValue(player, false);
            SecondaryAttackFlagField?.SetValue(player, false);
            _suppressAttackUntil = Math.Max(_suppressAttackUntil, Time.time + 0.5f);
        }
        catch
        {
            /* ignore */
        }
    }

    static void ArmDeferredConsume(Player player, ItemDrop.ItemData? source)
    {
        _deferredConsume = source;
        _deferConsumeArmed = source != null;
        ClearPendingAttacksAfterPaperPlace(player);
        _suppressAttackUntil = Time.time + 30f;
    }

    static void TryFinishDeferredConsume(Player player)
    {
        if (!_deferConsumeArmed || player == null)
            return;
        if (AttackStillHeld())
            return;

        var toConsume = _deferredConsume;
        _deferredConsume = null;
        _deferConsumeArmed = false;
        ClearPendingAttacksAfterPaperPlace(player);
        ConsumeTrigger(player, toConsume);
        _suppressAttackUntil = Time.time + 0.2f;
    }

    private static readonly FieldInfo? PieceTablePiecesField =
        AccessTools.Field(typeof(PieceTable), "m_pieces");

    private static readonly MethodInfo? SetSelectedPieceMethod =
        AccessTools.Method(typeof(Player), "SetSelectedPiece", new[] { typeof(Piece) });

    private static readonly MethodInfo? HideHandItemsMethod =
        AccessTools.Method(typeof(Humanoid), "HideHandItems");

    private static readonly HashSet<int> NotesBeforePlace = new();
    private static Vector3 _placePos;

    /// <summary>0 = wall/vertical, 1 = flat. Toggled with Use while placing.</summary>
    private static int _orientIndex;

    internal sealed class PaperSnapshot
    {
        public string Rename = "";
        public string Desc = "";
        public bool Public;
        public bool Unlock;
        public long CrafterId;
        public string CrafterName = "";
        public float FontSize;
        public bool Landscape;
        public bool TakePublic;
        public ItemDrop.ItemData? SourceItem;

        public static PaperSnapshot From(ItemDrop.ItemData item)
        {
            PaperItemStyle.Read(item, out var fontSize, out var landscape, out var takePublic);
            return new()
            {
                // Keep rich-text color tags — Plain() was stripping <color> from name/desc.
                Rename = CustomizeLibsAPI.HasCustomName(item)
                    ? (CustomizeLibsAPI.GetProperName(item) ?? "")
                    : "",
                Desc = CustomizeLibsAPI.HasCustomDescription(item)
                    ? (CustomizeLibsAPI.GetProperDescription(item) ?? "")
                    : "",
                Public = Permissions.RenamePermissionManager.HasPublicRewriteFlag(item),
                Unlock = DrakeRenameit.IsRenameUnlocked(item),
                CrafterId = item.m_crafterID,
                CrafterName = item.m_crafterName ?? "",
                FontSize = fontSize,
                Landscape = landscape,
                TakePublic = takePublic,
                SourceItem = item,
            };
        }
    }

    internal static PaperSnapshot? TakePendingSnapshot()
    {
        var s = _pendingSnapshot;
        _pendingSnapshot = null;
        return s;
    }

    internal static bool IsPlacementGhost(GameObject? go)
    {
        if (go == null || PlacementGhostField == null || Player.m_localPlayer == null)
            return false;
        if (PlacementGhostField.GetValue(Player.m_localPlayer) is not GameObject ghost || ghost == null)
            return false;
        return go == ghost || go.transform.IsChildOf(ghost.transform);
    }

    internal static void MarkVesselApplied() => _vesselAppliedSnapshot = true;

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

            AddLocalization();

            var table = new CustomPieceTable(PieceTableName, new PieceTableConfig
            {
                // Valheim 1.0: categories live on pieces; UseCategories is obsolete in Jotunn.
                CanRemovePieces = true,
            });
            PieceManager.Instance.AddPieceTable(table);
            PaperPieceTables.Harden(table.PieceTable);

            var icon = PaperItem.GetWrittenIconSprite() ?? PaperItem.GetBlankIconSprite();
            RegisterNote(NoteVertical, wall: true, icon);
            RegisterNote(NoteFlat, wall: false, icon);
            AttachTableToWrittenItem();

            _log?.LogInfo("[Paper] Written place table ready (forge art on sign donors).");
        }
        catch (Exception ex)
        {
            _log?.LogError($"[Paper] Written place register failed: {ex}");
        }
    }

    private static void RegisterNote(string prefab, bool wall, Sprite? icon)
    {
        var nameTok = wall ? "$piece_drakes_paper_note_v" : "$piece_drakes_paper_note_f";
        var descTok = wall ? "$piece_drakes_paper_note_v_desc" : "$piece_drakes_paper_note_f_desc";
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

        var piece = new CustomPiece(prefab, "sign", config);
        PieceManager.Instance.AddPiece(piece);
        var go = piece.PiecePrefab;
        if (go == null)
            return;

        ClearPieceBuildCost(go);
        PaperNotePageText.PreserveSignText(go);
        PaperItem.BuildDecorSheetVisual(go, wall, written: !RenameitConfig.PaperShowPageText);
        PaperNotePageText.AttachToPrefab(go, wall);
        PaperPieceTables.ForceMiscCategory(go);
        PaperAssets.EnsurePersistentZNetView(go);

        if (go.GetComponent<PaperWrittenVessel>() == null)
            go.AddComponent<PaperWrittenVessel>();
    }

    /// <summary>Strip donor recipe so HUD doesn't show Wood/Coal; placement consumes the Written Page.</summary>
    private static void ClearPieceBuildCost(GameObject go)
    {
        var p = go.GetComponent<Piece>();
        if (p == null)
            return;

        p.m_resources = Array.Empty<Piece.Requirement>();
        p.m_craftingStation = null;
        // Vanilla PlacePiece calls m_placeEffect.Create with no null check.
        if (p.m_placeEffect == null)
            p.m_placeEffect = new EffectList();
    }

    private static void AttachTableToWrittenItem()
    {
        var writtenDrop = GetWrittenItemDrop();
        var shared = writtenDrop?.m_itemData?.m_shared;
        if (shared == null || writtenDrop?.m_itemData == null)
            return;

        var table = PieceManager.Instance.GetPieceTable(PieceTableName);
        if (table == null)
            return;

        // Blank and written are both cloned from the same donor. Mutating m_shared in place
        // turns blank paper into a place-tool too (Use enters place-mode, attack punches).
        var blankShared = GetBlankItemDrop()?.m_itemData?.m_shared;
        if (ReferenceEquals(shared, blankShared))
        {
            shared = DetachShared(shared);
            writtenDrop.m_itemData.m_shared = shared;
        }

        PaperPieceTables.Harden(table);
        shared.m_buildPieces = table;
        // Materials can't be GetRightItem() for build mode — UpdatePlacement NREs without
        // right-hand.m_buildPieces. Written Page only; blank stays a material.
        shared.m_itemType = ItemDrop.ItemData.ItemType.Tool;
        // Root cause of place-punch: CustomItem clones LeatherScraps, whose Attack is the
        // unarmed punch. PlacePiece always SetTriggers that string. Install a place-tool
        // Attack instead of scrubbing the donor punch after the fact.
        InstallPlaceToolAttack(shared);

        var blankItem = GetBlankItemDrop()?.m_itemData;
        PaperItem.ClearBlankPlaceTool(blankItem);
    }

    /// <summary>Point this stack at the written-only shared data (never the blank paper shared).</summary>
    private static void BindWrittenShared(ItemDrop.ItemData item)
    {
        var proto = GetWrittenItemDrop()?.m_itemData?.m_shared;
        if (proto == null || item == null)
            return;
        if (!ReferenceEquals(item.m_shared, proto))
            item.m_shared = proto;
    }

    private static ItemDrop? GetWrittenItemDrop()
    {
        var prefab = PrefabManager.Instance.GetPrefab(PaperItem.WrittenPrefabName)
                     ?? ObjectDB.instance?.GetItemPrefab(PaperItem.WrittenPrefabName);
        return prefab?.GetComponent<ItemDrop>();
    }

    private static ItemDrop? GetBlankItemDrop()
    {
        var prefab = PrefabManager.Instance.GetPrefab(PaperItem.PrefabName)
                     ?? ObjectDB.instance?.GetItemPrefab(PaperItem.PrefabName);
        return prefab?.GetComponent<ItemDrop>();
    }

    private static ItemDrop.ItemData.SharedData DetachShared(ItemDrop.ItemData.SharedData src)
    {
        var flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        var clone = (ItemDrop.ItemData.SharedData)typeof(object)
            .GetMethod("MemberwiseClone", flags)!
            .Invoke(src, null);
        // Do not copy LeatherScraps' unarmed Attack — written place-tool gets its own.
        InstallPlaceToolAttack(clone);
        return clone;
    }

    /// <summary>Blank paper must not stay in the written place table. Never mutate the written shared instance.</summary>
    private static void StripBlankPlaceTool(ItemDrop.ItemData? item)
    {
        if (!PaperItem.IsBlankPaper(item) || item?.m_shared == null)
            return;

        var writtenShared = GetWrittenItemDrop()?.m_itemData?.m_shared;
        if (writtenShared != null && ReferenceEquals(item.m_shared, writtenShared))
        {
            var blankShared = GetBlankItemDrop()?.m_itemData?.m_shared;
            if (blankShared != null && !ReferenceEquals(blankShared, writtenShared))
                item.m_shared = blankShared;
            else
                return;
        }

        PaperItem.ClearBlankPlaceTool(item);
    }

    /// <summary>
    /// Written Page must be a Tool for <see cref="Player.UpdatePlacement"/>, and
    /// <see cref="Player.GetBuildStamina"/> always reads <c>m_attack.m_attackStamina</c>.
    /// Attack anim does not matter for notes: <c>PlacePiece</c> is called with
    /// <c>doAttack=false</c> so <c>SetTrigger</c> is skipped (LeatherScraps punch / hammer swing).
    /// </summary>
    private static void InstallPlaceToolAttack(ItemDrop.ItemData.SharedData shared)
    {
        if (shared == null)
            return;
        shared.m_attack = NewInertAttack();
        shared.m_secondaryAttack = NewInertAttack();
    }

    /// <summary>Non-null Attack for GetBuildStamina; empty anim as belt-and-suspenders.</summary>
    private static Attack NewInertAttack()
    {
        return new Attack
        {
            m_attackType = Attack.AttackType.None,
            m_attackStamina = 0f,
            m_attackRange = 0f,
            m_attackAnimation = "",
            m_attackHitNoise = 0f,
        };
    }

    internal static bool IsNotePiece(Component? c)
    {
        if (c == null) return false;
        var n = c.gameObject.name;
        return n.StartsWith(NoteVertical, StringComparison.OrdinalIgnoreCase) ||
               n.StartsWith(NoteFlat, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Open written place mode. Use again while placing cycles Wall ↔ Flat.</summary>
    internal static void BeginPlace(ItemDrop.ItemData item)
    {
        if (!PaperItem.IsWrittenPaper(item) || PaperItem.IsBlankPaper(item) || !RenameitConfig.PaperPlaceEnabled)
            return;
        var player = Player.m_localPlayer;
        if (player == null || !DrakeRenameit.IsItemInLocalPlayerInventory(item))
            return;

        AttachTableToWrittenItem();
        BindWrittenShared(item);
        if (item.m_shared != null)
            InstallPlaceToolAttack(item.m_shared);
        var table = item.m_shared?.m_buildPieces
                    ?? PieceManager.Instance.GetPieceTable(PieceTableName);
        if (table == null)
        {
            _log?.LogWarning("[Paper] Written place table missing.");
            return;
        }

        // Player.UpdatePlacement reads GetRightItem().m_shared.m_buildPieces with no null check.
        // If the Written Page isn't the right-hand item, it NREs every frame (can't rotate/place).
        if (!EnsurePaperIsBuildTool(player, item))
        {
            ValheimHudMessage.Show(player, 
                MessageHud.MessageType.Center,
                "Put the Written Page on your hotbar, then Use it to place.");
            return;
        }

        _triggerPaper = item;
        _orientIndex = AllowedOrientIndex();
        InventoryGui.instance?.Hide();

        PaperPieceTables.Harden(table);

        if (SetPlaceModeMethod != null)
            SetPlaceModeMethod.Invoke(player, new object[] { table });
        else
            player.SetPlaceMode(table);

        PaperPieceTables.RefreshPlayerAvailable(player);
        SelectOrientation(player, _orientIndex);
        ValheimHudMessage.Show(player, MessageHud.MessageType.TopLeft, PlaceStatus(_orientIndex));
    }

    private static ItemDrop.ItemData? InvGetRightItem(Humanoid humanoid)
    {
        if (humanoid == null || GetRightItemMethod == null)
            return null;
        try
        {
            return GetRightItemMethod.Invoke(humanoid, null) as ItemDrop.ItemData;
        }
        catch (Exception ex)
        {
            _log?.LogWarning($"[Paper] GetRightItem failed: {ex.InnerException?.Message ?? ex.Message}");
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
            _log?.LogWarning($"[Paper] EquipItem failed: {ex.InnerException?.Message ?? ex.Message}");
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
        catch (Exception ex)
        {
            _log?.LogWarning($"[Paper] UnequipItem failed: {ex.InnerException?.Message ?? ex.Message}");
        }
    }

    /// <summary>Equip written page so UpdatePlacement's GetRightItem() has m_buildPieces.</summary>
    private static bool EnsurePaperIsBuildTool(Player player, ItemDrop.ItemData item)
    {
        if (item?.m_shared == null)
            return false;

        // Keep table on this shared data.
        var table = PieceManager.Instance.GetPieceTable(PieceTableName);
        if (table != null)
            item.m_shared.m_buildPieces = table;

        var right = InvGetRightItem(player);
        if (ReferenceEquals(right, item) && right.m_shared?.m_buildPieces != null)
            return true;

        try
        {
            // Unequip current so EquipItem can take the paper (materials/tools).
            if (right != null)
                InvUnequipItem(player, right);
            InvEquipItem(player, item);

            right = InvGetRightItem(player);
            return right != null &&
                   PaperItem.IsWrittenPaper(right) &&
                   right.m_shared?.m_buildPieces != null;
        }
        catch (Exception ex)
        {
            _log?.LogWarning($"[Paper] Equip written for place failed: {ex.Message}");
            return false;
        }
    }

    private static int AllowedOrientIndex() =>
        RenameitConfig.BlankPaperPlaceHorizontal && !RenameitConfig.BlankPaperPlaceVertical ? 1 : 0;

    private static string PlaceStatus(int orient)
    {
        var label = orient == 0 ? "Wall (vertical)" : "Flat";
        return $"Place: {label} — Use again to switch Wall/Flat.";
    }

    private static bool InOurPlaceMode(Player player)
    {
        var table = PieceManager.Instance.GetPieceTable(PieceTableName);
        if (table == null || player == null)
            return false;
        if (BuildPiecesField == null)
            return false;
        var current = BuildPiecesField.GetValue(player) as PieceTable;
        return ReferenceEquals(current, table);
    }

    private static void CycleOrientation(Player player)
    {
        _orientIndex = _orientIndex == 0 ? 1 : 0;
        SelectOrientation(player, _orientIndex);
        ValheimHudMessage.Show(player, MessageHud.MessageType.TopLeft, PlaceStatus(_orientIndex));
    }

    private static void SelectOrientation(Player player, int orient)
    {
        var table = PieceManager.Instance.GetPieceTable(PieceTableName);
        var pieces = GetTablePieces(table);
        if (pieces == null || pieces.Count == 0)
            return;

        var want = orient == 0 ? NoteVertical : NoteFlat;
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
            _log?.LogWarning($"[Paper] PieceTable.m_pieces access failed: {ex.Message}");
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
            _log?.LogWarning($"[Paper] SetSelectedPiece failed: {ex.InnerException?.Message ?? ex.Message}");
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

    private static ItemDrop.ItemData? ResolveTrigger(Player player)
    {
        if (_triggerPaper != null && DrakeRenameit.IsItemInLocalPlayerInventory(_triggerPaper))
            return _triggerPaper;

        // Only the stack that opened place mode — never "first written page in bag."
        return null;
    }

    private static void ConsumeTrigger(Player player, ItemDrop.ItemData? source)
    {
        if (source == null || player == null)
            return;

        // Unequip first — RemoveItem alone can leave the paper mesh in-hand.
        // triggerEquipEffects=false (InvUnequipItem already passes false).
        InvUnequipItem(player, source);

        var inv = player.GetInventory();
        if (inv == null)
            return;

        var item = inv.ContainsItem(source) ? source : FindMatchingWritten(inv, source);
        if (item == null)
        {
            _log?.LogWarning("[Paper] Placed a note but could not find the Written Page to consume.");
            return;
        }

        if (item.m_stack > 1)
            item.m_stack -= 1;
        else
            inv.RemoveItem(item);

        // HideHandItems defaults animation=true → SetTrigger("equip_hip"). Silent hide.
        try
        {
            var ps = HideHandItemsMethod?.GetParameters();
            if (HideHandItemsMethod != null && ps != null)
            {
                object?[] args = ps.Length >= 2
                    ? new object[] { false, false } // onlyRightHand, animation
                    : ps.Length == 1
                        ? new object[] { false }
                        : Array.Empty<object>();
                HideHandItemsMethod.Invoke(player, args);
            }
        }
        catch
        {
            /* ignore */
        }
    }

    private static ItemDrop.ItemData? FindMatchingWritten(Inventory inv, ItemDrop.ItemData source)
    {
        var snap = PaperSnapshot.From(source);
        var items = inv.GetAllItems();
        if (items == null)
            return null;

        ItemDrop.ItemData? fallback = null;
        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            if (!PaperItem.IsWrittenPaper(item))
                continue;
            fallback ??= item;
            var other = PaperSnapshot.From(item);
            if (other.Rename == snap.Rename &&
                other.Desc == snap.Desc &&
                other.CrafterId == snap.CrafterId)
                return item;
        }

        return fallback;
    }

    private static void AddLocalization()
    {
        var loc = LocalizationManager.Instance.GetLocalization();
        loc.AddTranslation("English", "piece_drakes_paper_note_v", "Written Page (wall)");
        loc.AddTranslation("English", "piece_drakes_paper_note_v_desc", "Pin your Written Page on a wall.");
        loc.AddTranslation("English", "piece_drakes_paper_note_f", "Written Page (flat)");
        loc.AddTranslation("English", "piece_drakes_paper_note_f_desc", "Lay your Written Page flat.");
        loc.AddTranslation("English", "piece_drakes_paper_make_public", "Make public");
        loc.AddTranslation("English", "piece_drakes_paper_make_private", "Make private");
        loc.AddTranslation("English", "piece_drakes_paper_now_public", "Public — anyone can take this.");
        loc.AddTranslation("English", "piece_drakes_paper_now_private", "Private.");
        loc.AddTranslation("English", "piece_drakes_paper_edit", "Edit");
        loc.AddTranslation("English", "piece_drakes_paper_edit_options", "Edit page");
    }

    [HarmonyPatch]
    private static class Patches
    {
        [HarmonyPrefix]
        [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.UseItem))]
        private static bool UseItem_Prefix(
            Humanoid __instance,
            ItemDrop.ItemData item,
            bool fromInventoryGui)
        {
            if (__instance != Player.m_localPlayer || item == null)
                return true;

            // Blank paper is hammer décor only — Use must not open the written place table.
            if (PaperItem.IsBlankPaper(item))
            {
                StripBlankPlaceTool(item);
                if (__instance is Player blankPlayer && InOurPlaceMode(blankPlayer))
                    EndPlaceMode(blankPlayer);
                return true;
            }

            if (!RenameitConfig.PaperPlaceEnabled || !PaperItem.IsWrittenPaper(item))
                return true;

            _ = fromInventoryGui;
            var player = Player.m_localPlayer;
            if (player != null && InOurPlaceMode(player))
            {
                // Use again switches only when orientation is Both. Vertical/Horizontal Only stays put.
                CycleOrientation(player);
                return false;
            }

            BeginPlace(item);
            return false;
        }

        /// <summary>
        /// Written Page is a place-tool. Left-click opens place mode; while placing, Attack is for the ghost.
        /// </summary>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.StartAttack))]
        private static bool StartAttack_Prefix(Humanoid __instance, bool secondaryAttack, ref bool __result)
        {
            if (__instance != Player.m_localPlayer)
                return true;

            if (Time.time < _suppressAttackUntil || _deferConsumeArmed)
            {
                __result = false;
                return false;
            }

            if (__instance is Player placing && InOurPlaceMode(placing))
            {
                __result = false;
                return false;
            }

            var right = InvGetRightItem(__instance);
            if (PaperItem.IsBlankPaper(right))
            {
                StripBlankPlaceTool(right);
                __result = false;
                return false;
            }

            if (!PaperItem.IsWrittenPaper(right))
                return true;

            __result = false;
            if (!secondaryAttack && __instance is Player player)
                BeginPlace(right!);
            return false;
        }

        /// <summary>If blank paper still carries the written place table, drop out of place mode.</summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.SetupEquipment))]
        private static void SetupEquipment_Postfix(Humanoid __instance)
        {
            if (__instance != Player.m_localPlayer)
                return;
            var right = InvGetRightItem(__instance);
            if (!PaperItem.IsBlankPaper(right))
                return;

            StripBlankPlaceTool(right);
            if (__instance is Player player && InOurPlaceMode(player))
                EndPlaceMode(player);
        }

        static readonly HashSet<ZDOID> ClaimedWrittenDrops = new();

        /// <summary>
        /// A non-owner Instantiate of the written prefab leaves a drop that auto-pickup
        /// adds to inventory every frame and never destroys. Swallow those ghosts.
        /// </summary>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.Pickup), new[] { typeof(GameObject), typeof(bool), typeof(bool) })]
        private static bool Pickup_Prefix(GameObject go, ref bool __result)
        {
            var drop = go != null ? go.GetComponent<ItemDrop>() : null;
            if (drop == null || !PaperItem.IsWrittenPaper(drop.m_itemData))
                return true;

            // m_nview is publicized at compile time but private at runtime.
            var nv = go.GetComponent<ZNetView>();
            var zdo = nv != null && nv.IsValid() ? nv.GetZDO() : null;
            if (zdo == null)
            {
                UnityEngine.Object.Destroy(go);
                __result = false;
                return false;
            }

            if (ClaimedWrittenDrops.Contains(zdo.m_uid))
            {
                if (ZNetScene.instance != null)
                    ZNetScene.instance.Destroy(go);
                else
                    UnityEngine.Object.Destroy(go);
                __result = false;
                return false;
            }

            return true;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.Pickup), new[] { typeof(GameObject), typeof(bool), typeof(bool) })]
        private static void Pickup_Postfix(GameObject go, bool __result)
        {
            if (!__result || go == null)
                return;
            var drop = go.GetComponent<ItemDrop>();
            if (drop == null || !PaperItem.IsWrittenPaper(drop.m_itemData))
                return;
            // m_nview is publicized at compile time but private at runtime.
            var nv = go.GetComponent<ZNetView>();
            var zdo = nv != null && nv.IsValid() ? nv.GetZDO() : null;
            if (zdo != null)
                ClaimedWrittenDrops.Add(zdo.m_uid);
        }

        /// <summary>
        /// Guard: vanilla UpdatePlacement does GetRightItem().m_shared with no null check.
        /// </summary>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(Player), "UpdatePlacement", new[] { typeof(bool), typeof(float) })]
        private static bool UpdatePlacement_Prefix(Player __instance, bool takeInput)
        {
            if (__instance != Player.m_localPlayer)
                return true;
            if (PaperBlankPlace.IsInPlaceMode(__instance))
                return true;
            if (!__instance.InPlaceMode())
                return true;

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

        /// <summary>
        /// After a successful note place: exit place mode immediately, but defer consuming the
        /// Written Page until Attack/JoyPlace is released — unequipping mid-click punches
        /// (same class of bug as cultivator breaking on last plant).
        /// </summary>
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

            ClearPendingAttacksAfterPaperPlace(__instance);
            EndPlaceMode(__instance);
            ArmDeferredConsume(__instance, toConsume);
            _triggerPaper = null;
            if (_vesselAppliedSnapshot)
                _pendingSnapshot = null;
            _vesselAppliedSnapshot = false;
        }

        /// <summary>Finish deferred paper consume once the place-click Attack is released.</summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(Player), "Update", new Type[] { })]
        private static void PlayerUpdate_DeferredConsume(Player __instance)
        {
            if (__instance != Player.m_localPlayer || !_deferConsumeArmed)
                return;
            TryFinishDeferredConsume(__instance);
        }

        /// <summary>
        /// Seat blank upright + written vertical sheets on the wall face, not in the wall volume.
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(Player), nameof(Player.UpdatePlacementGhost))]
        private static void UpdatePlacementGhost_Postfix(Player __instance)
        {
            if (__instance != Player.m_localPlayer)
                return;
            SeatGhost(__instance);
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Player), "PlacePiece", new[]
        {
            typeof(Piece), typeof(Vector3), typeof(Quaternion), typeof(bool), typeof(bool)
        })]
        private static void PlacePiece_SeatPrefix(
            Player __instance,
            Piece piece,
            ref Vector3 pos,
            ref bool doAttack,
            object[] __args)
        {
            if (__instance != Player.m_localPlayer || piece == null)
                return;
            if (PaperItem.IsWallSheetPiece(piece))
                pos = PaperItem.SeatWallSheet(pos, piece.transform);
            // TryPlacePiece always passes doAttack=true → PlacePiece SetTriggers the held
            // tool anim. Skip that for paper notes / blank place pieces.
            if (IsNotePiece(piece) || PaperBlankPlace.IsBlankPlacePiece(piece))
            {
                doAttack = false;
                if (__args != null && __args.Length > 3)
                    __args[3] = false;
            }
        }

        static void SeatGhost(Player player)
        {
            if (PlacementGhostField?.GetValue(player) is not GameObject ghost || ghost == null)
                return;
            var piece = ghost.GetComponent<Piece>();
            if (!PaperItem.IsWallSheetPiece(piece))
                return;
            ghost.transform.position = PaperItem.SeatWallSheet(ghost.transform.position, ghost.transform);
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Player), "PlacePiece", new[]
        {
            typeof(Piece), typeof(Vector3), typeof(Quaternion), typeof(bool), typeof(bool)
        })]
        private static bool PlacePiece_Prefix(Player __instance, Piece piece, Vector3 pos)
        {
            _vesselAppliedSnapshot = false;
            _notePlaceArmed = false;
            if (__instance != Player.m_localPlayer || piece == null || !IsNotePiece(piece))
                return true;

            var source = ResolveTrigger(__instance);
            if (source == null)
            {
                ValheimHudMessage.Show(__instance, MessageHud.MessageType.Center, "Use a Written Page to place this.");
                return false;
            }

            _placePos = pos;
            RememberNotes();
            _pendingSnapshot = PaperSnapshot.From(source);
            _triggerPaper = source;
            _notePlaceArmed = true;
            return true;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Player), "PlacePiece", new[]
        {
            typeof(Piece), typeof(Vector3), typeof(Quaternion), typeof(bool), typeof(bool)
        })]
        private static void PlacePiece_Postfix(Player __instance, Piece piece)
        {
            if (__instance != Player.m_localPlayer || !_notePlaceArmed)
                return;

            _notePlaceArmed = false;
            if (piece == null || !IsNotePiece(piece))
                return;

            // `piece` is the ghost / table prefab, not the spawned sheet. Vanilla also
            // does not consume the tool — empty requirements — so stamp and consume here.
            var spawned = FindSpawnedNote(__instance);
            // Awake on the spawned sheet already took the snapshot if place succeeded.
            var takenBySpawn = _pendingSnapshot == null;
            if (spawned == null && !takenBySpawn)
            {
                _log?.LogWarning("[Paper] Place did not spawn a note; kept the Written Page.");
                _pendingSnapshot = null;
                return;
            }

            spawned?.AcceptSnapshot(_pendingSnapshot);
            if (_vesselAppliedSnapshot)
                _pendingSnapshot = null;

            // Defer exit+consume until UpdatePlacement postfix (same-frame NRE otherwise).
            _consumeAfterPlacement = _triggerPaper;
            _endPlaceAfterPlacement = true;
        }

        private static void RememberNotes()
        {
            NotesBeforePlace.Clear();
            var found = UnityEngine.Object.FindObjectsOfType<PaperWrittenVessel>();
            for (var i = 0; i < found.Length; i++)
            {
                var v = found[i];
                if (v != null && !IsPlacementGhost(v.gameObject))
                    NotesBeforePlace.Add(v.GetInstanceID());
            }
        }

        private static PaperWrittenVessel? FindSpawnedNote(Player player)
        {
            PaperWrittenVessel? best = null;
            var bestDist = 2.5f;
            var found = UnityEngine.Object.FindObjectsOfType<PaperWrittenVessel>();
            for (var i = 0; i < found.Length; i++)
            {
                var v = found[i];
                if (v == null || IsPlacementGhost(v.gameObject))
                    continue;
                if (NotesBeforePlace.Contains(v.GetInstanceID()))
                    continue;
                var dist = Vector3.Distance(v.transform.position, _placePos);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = v;
                }
            }

            _ = player;
            return best;
        }

        /// <summary>
        /// Vanilla GetBuildStamina: GetRightItem().m_shared.m_attack — NRE with no null check.
        /// After we consume/unequip the page, UpdatePlacement still calls this for a frame.
        /// </summary>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(Player), nameof(Player.GetBuildStamina))]
        private static bool GetBuildStamina_Prefix(Player __instance, ref float __result)
        {
            if (__instance != Player.m_localPlayer)
                return true;

            var right = InvGetRightItem(__instance);
            if (right?.m_shared?.m_attack != null)
                return true;

            __result = 0f;
            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Sign), nameof(Sign.Interact))]
        private static bool Sign_Interact_Prefix(Sign __instance, Humanoid character, bool hold, bool alt)
        {
            // Legacy: notes no longer use Sign; keep guard if a donor Sign remains.
            if (!IsNotePiece(__instance))
                return true;
            __instance.GetComponent<PaperWrittenVessel>()?.TryReclaim(character);
            return false;
        }

        /// <summary>
        /// Skip vanilla Sign hover on notes. Sign.GetHoverText hits MuteList/UGC and
        /// spams errors every frame if a donor Sign survived the strip.
        /// </summary>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(Sign), nameof(Sign.GetHoverText))]
        private static bool Sign_Hover_Prefix(Sign __instance, ref string __result)
        {
            if (__instance == null || !IsNotePiece(__instance))
                return true;
            var vessel = __instance.GetComponent<PaperWrittenVessel>();
            if (vessel == null)
                return true;
            __result = vessel.GetHoverText();
            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(WearNTear), nameof(WearNTear.Destroy))]
        private static void WearNTear_Destroy_Prefix(WearNTear __instance)
        {
            if (__instance == null || !IsNotePiece(__instance))
                return;
            try
            {
                __instance.GetComponent<PaperWrittenVessel>()?.DropIntoWorld();
            }
            catch (Exception ex)
            {
                _log?.LogWarning($"[Paper] Break drop skipped so the piece can still be removed: {ex.Message}");
            }
        }

        // Never ldfld Hud.m_hoverName / m_crosshair — JIT FieldAccessException spams the HUD
        // every frame while hovering if the compile ref disagrees with the live game.
        static readonly FieldInfo? HoverNameField = AccessTools.Field(typeof(Hud), "m_hoverName");
        static readonly FieldInfo? CrosshairField = AccessTools.Field(typeof(Hud), "m_crosshair");
        /// <summary>Vanilla hold-Use gate (ItemStand take). Private at runtime.</summary>
        static readonly FieldInfo? LastHoverInteractTimeField =
            AccessTools.Field(typeof(Player), "m_lastHoverInteractTime");

        /// <summary>
        /// Store/CI builds cannot put <see cref="Hoverable"/> on the vessel (vtable). Fill hover
        /// text when vanilla found no Hoverable. Always overwrite leftover Sign text.
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(Hud), "UpdateCrosshair")]
        private static void UpdateCrosshair_Postfix(Hud __instance, Player player)
        {
            try
            {
                if (__instance == null || player == null)
                    return;
                if (TextViewer.instance != null && TextViewer.instance.IsVisible())
                    return;

                var hover = player.GetHoverObject();
                if (hover == null)
                    return;
                var vessel = hover.GetComponentInParent<PaperWrittenVessel>();
                if (vessel == null)
                    return;

                if (HoverNameField?.GetValue(__instance) is not TMPro.TextMeshProUGUI hoverName || hoverName == null)
                    return;

                hoverName.text = vessel.GetHoverText();
                if (hoverName.text.Length == 0 || CrosshairField == null)
                    return;
                if (CrosshairField.GetValue(__instance) is UnityEngine.UI.Image cross && cross != null)
                    cross.color = Color.yellow;
            }
            catch
            {
                /* never break HUD, never log per-frame */
            }
        }

        /// <summary>
        /// Route [E] to the vessel without requiring <see cref="Interactable"/> on the type.
        /// Must mirror Player.Interact's hold throttle: ButtonDown (hold=false) arms the timer;
        /// hold=true is ignored until 0.2s later — otherwise tap-E reclaims on the next frame.
        /// </summary>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(Player), "Interact", new[] { typeof(GameObject), typeof(bool), typeof(bool) })]
        private static bool Interact_Prefix(Player __instance, GameObject go, bool hold, bool alt)
        {
            try
            {
                if (go == null || __instance == null)
                    return true;
                var vessel = go.GetComponentInParent<PaperWrittenVessel>();
                if (vessel == null)
                    return true;
                if (__instance.InAttack() || __instance.InDodge())
                    return false;

                // Same gate as vanilla Player.Interact before calling Interactable.
                if (hold && LastHoverInteractTimeField != null)
                {
                    var last = (float)LastHoverInteractTimeField.GetValue(__instance)!;
                    if (Time.time - last < 0.2f)
                        return false;
                }

                LastHoverInteractTimeField?.SetValue(__instance, Time.time);
                vessel.Interact(__instance, hold, alt);
                return false;
            }
            catch
            {
                return true;
            }
        }
    }
}

/// <summary>
/// ZDO vessel for a placed Written Page — hover/reclaim; mesh is parchment only.
/// Does <b>not</b> implement <c>Hoverable</c>/<c>Interactable</c>: Pfhoenix CI stubs lack
/// <c>Hoverable.GetHoverOffset</c> that Valheim 1.0 added, so a store build throws
/// <c>TypeLoadException: VTable setup of type PaperWrittenVessel failed</c> and the
/// script never instantiates. Hover/Use are Harmony-routed from <see cref="PaperWrittenPlace"/>.
/// </summary>
internal sealed class PaperWrittenVessel : MonoBehaviour
{
    const string ZdoRename = "DrakePaper_Rename";
    const string ZdoDesc = "DrakePaper_Desc";
    const string ZdoPublic = "DrakePaper_Public";
    const string ZdoUnlock = "DrakePaper_Unlock";
    const string ZdoCrafterId = "DrakePaper_CrafterId";
    const string ZdoCrafterName = "DrakePaper_CrafterName";
    /// <summary>Take-off-wall bypass. Not the rewrite flag (<see cref="ZdoPublic"/>).</summary>
    internal const string ZdoTakePublic = PaperItemStyle.TakePublicKey;
    const string RpcSetTakePublic = "DrakePaper_SetTakePublic";
    const string ZdoHandled = "DrakePaper_Handled";
    internal const string ZdoFontSize = PaperItemStyle.FontSizeKey;
    internal const string ZdoLandscape = PaperItemStyle.LandscapeKey;

    PaperWrittenPlace.PaperSnapshot? _pending;
    bool _rpcRegistered;
    bool _handled;
    bool _reclaimQueued;

    void Awake()
    {
        // Notes are pieces, not world pickups. A leftover ItemDrop makes nearby players
        // auto-grab the page (and, if the ZDO isn't owned, copy it forever).
        StripPickupComponents();

        // Donor Sign strips rich text in its GetHoverText — never leave one on notes.
        foreach (var sign in GetComponentsInChildren<Sign>(true))
            DestroyImmediate(sign);

        RegisterTakePublicRpc();
        // Ghost Awake runs when place mode starts — it must not eat the page snapshot.
        if (PaperWrittenPlace.IsPlacementGhost(gameObject))
            return;
        _pending = PaperWrittenPlace.TakePendingSnapshot();
    }

    internal void AcceptSnapshot(PaperWrittenPlace.PaperSnapshot? snap)
    {
        if (snap != null)
            _pending = snap;
        FlushPendingSnapshot();
    }

    void StripPickupComponents()
    {
        foreach (var drop in GetComponentsInChildren<ItemDrop>(true))
        {
            if (drop == null)
                continue;
            drop.m_autoPickup = false;
            DestroyImmediate(drop);
        }
    }

    void Start()
    {
        RegisterTakePublicRpc();
        FlushPendingSnapshot();
        RefreshPageVisual();
    }

    void LateUpdate()
    {
        if (_pending != null)
            FlushPendingSnapshot();
        if (_reclaimQueued && Player.m_localPlayer != null)
            TryReclaim(Player.m_localPlayer);
    }

    /// <summary>Write pending snapshot to ZDO when available.</summary>
    internal void FlushPendingSnapshot()
    {
        if (_pending == null)
            return;

        var zdo = GetComponent<ZNetView>()?.GetZDO();
        if (zdo == null)
            return;

        ApplySnapshot(_pending);
        PaperWrittenPlace.MarkVesselApplied();
        _pending = null;
        RefreshPageVisual();
    }

    void RefreshPageVisual()
    {
        if (PaperWrittenPlace.IsPlacementGhost(gameObject))
            return;
        PaperNotePageText.Sync(gameObject, ReadPageDescription());
    }

    /// <summary>Ward permit, or original creator / elevated (public-take guests stay out).</summary>
    bool LocalMayEditWallPaper() =>
        PrivateArea.CheckAccess(transform.position, 0f, flash: false) || LocalMayToggleTakePublic();

    string ReadPageDescription()
    {
        var zdo = GetComponent<ZNetView>()?.GetZDO();
        return zdo?.GetString(ZdoDesc, "") ?? "";
    }

    void ApplySnapshot(PaperWrittenPlace.PaperSnapshot snap)
    {
        var zdo = GetComponent<ZNetView>()?.GetZDO();
        if (zdo == null)
            return;

        zdo.Set(ZdoRename, snap.Rename ?? "");
        zdo.Set(ZdoDesc, snap.Desc ?? "");
        zdo.Set(ZdoPublic, snap.Public ? 1 : 0);
        zdo.Set(ZdoUnlock, snap.Unlock ? 1 : 0);
        zdo.Set(ZdoCrafterId, snap.CrafterId);
        zdo.Set(ZdoCrafterName, snap.CrafterName ?? "");
        // Style before Sync so on-mesh ink reads font/landscape on first paint.
        zdo.Set(ZdoFontSize, PaperFontScale.NormalizeStored(snap.FontSize));
        zdo.Set(ZdoLandscape, snap.Landscape ? 1 : 0);
        zdo.Set(ZdoTakePublic, snap.TakePublic ? 1 : 0);
        // Pass desc directly — don't rely on a ZDO round-trip for the first paint.
        if (!PaperWrittenPlace.IsPlacementGhost(gameObject))
            PaperNotePageText.Sync(gameObject, snap.Desc);
    }

    public string GetHoverName()
    {
        var zdo = GetComponent<ZNetView>()?.GetZDO();
        var name = zdo?.GetString(ZdoRename, "") ?? "";
        return string.IsNullOrEmpty(name) ? "Written Page" : name;
    }

    public string GetHoverText()
    {
        // Hover runs every frame from Hud.Update. A throw here aborts interact, so take dies.
        try
        {
            return BuildHoverText();
        }
        catch (Exception)
        {
            return "Written Page";
        }
    }

    public float GetHoverOffset() => 0f;

    public bool Interact(Humanoid character, bool hold, bool alt)
    {
        if (alt)
        {
            // Shift+Use is press-only (same as before) — ignore hold repeats.
            if (hold)
                return false;

            if (RenameitConfig.PaperWallRenameEnabled)
            {
                if (character == Player.m_localPlayer && LocalMayEditWallPaper())
                    PaperWallSession.TryOpen(this);
                return true;
            }

            // Flag off: Shift+Use stays Make public.
            if (RenameitConfig.PaperTakePublicEnabled)
            {
                if (CanOfferTakePublicToggle())
                    RequestToggleTakePublic(character);
                return true;
            }

            return false;
        }

        if (!MayTake(flash: !hold))
            return true;

        // Match vanilla ItemStand: take only on hold=true. Player (via our Interact prefix)
        // arms m_lastHoverInteractTime on ButtonDown and suppresses hold=true for 0.2s —
        // so a tap never reaches reclaim even though GetButton is true the next frame.
        if (RenameitConfig.PaperHoldToTake)
        {
            if (!hold)
                return false;
        }
        else if (hold)
        {
            return false;
        }

        _reclaimQueued = true;
        TryReclaim(character);
        return true;
    }

    public bool UseItem(Humanoid user, ItemDrop.ItemData item) => false;

    void RegisterTakePublicRpc()
    {
        if (_rpcRegistered)
            return;
        var nv = GetComponent<ZNetView>();
        if (nv == null)
            return;
        try
        {
            nv.Register<int>(RpcSetTakePublic, RPC_SetTakePublic);
            _rpcRegistered = true;
        }
        catch (Exception)
        {
            // Placement ghost, or ZNetView not networked yet. Start retries.
        }
    }

    void RPC_SetTakePublic(long sender, int value)
    {
        _ = sender;
        var nv = GetComponent<ZNetView>();
        if (nv == null || !nv.IsOwner())
            return;
        var zdo = nv.GetZDO();
        if (zdo == null)
            return;
        zdo.Set(ZdoTakePublic, value != 0 ? 1 : 0);
    }

    void RequestToggleTakePublic(Humanoid character)
    {
        if (character != Player.m_localPlayer)
            return;
        var nv = GetComponent<ZNetView>();
        if (nv == null || !nv.IsValid())
            return;

        if (!LocalMayToggleTakePublic())
            return;

        var next = ReadTakePublic() ? 0 : 1;
        nv.InvokeRPC(RpcSetTakePublic, next);
        var msg = next == 1 ? "$piece_drakes_paper_now_public" : "$piece_drakes_paper_now_private";
        ValheimHudMessage.Show(character, MessageHud.MessageType.Center, Localize(msg));
    }

    bool ReadTakePublic()
    {
        var zdo = GetComponent<ZNetView>()?.GetZDO();
        return zdo != null && zdo.GetInt(ZdoTakePublic, 0) == 1;
    }

    /// <summary>Feature on and this page was marked public — strangers may [E] Take.</summary>
    bool TakeIgnoresWard() => RenameitConfig.PaperTakePublicEnabled && ReadTakePublic();

    bool MayTake(bool flash)
    {
        if (TakeIgnoresWard())
            return true;
        return PrivateArea.CheckAccess(transform.position, 0f, flash);
    }

    /// <summary>Ward-permitted player, and the page is actually inside a ward. Otherwise the line is noise.</summary>
    bool CanOfferTakePublicToggle()
    {
        if (!RenameitConfig.PaperTakePublicEnabled)
            return false;
        if (!LocalMayToggleTakePublic())
            return false;
        if (!InsideEnabledWard(transform.position))
            return false;
        return PrivateArea.CheckAccess(transform.position, 0f, flash: false);
    }

    /// <summary>Original creator, or the admin/VIP override. Public editors cannot flip this.</summary>
    bool LocalMayToggleTakePublic()
    {
        var zdo = GetComponent<ZNetView>()?.GetZDO();
        if (zdo == null)
            return false;
        return Permissions.RenamePermissionManager.CanChangePublicFlag(
            zdo.GetLong(ZdoCrafterId, 0L),
            zdo.GetString(ZdoCrafterName, ""),
            Player.m_localPlayer);
    }

    static string Localize(string text) =>
        Localization.instance != null ? Localization.instance.Localize(text) : text;

    /// <summary>
    /// Valheim 1.0 join-key hover: non-classic gamepad uses <c>$KEY_AltKeys</c> (same
    /// modifier as <c>Player.Interact</c> alt). Keyboard / classic pad keeps Shift
    /// <c>$KEY_AltPlace + $KEY_Use</c>. Direct <c>IsNonClassicFunctionality</c> is
    /// missing from Pfhoenix CI stubs — bind at runtime.
    /// </summary>
    static readonly MethodInfo? NonClassicFunctionalityMethod =
        AccessTools.DeclaredMethod(typeof(ZInput), "IsNonClassicFunctionality")
        ?? AccessTools.Method(typeof(ZInput), "IsNonClassicFunctionality");

    static bool UseGamepadAltKeysPrompt()
    {
        try
        {
            if (!ZInput.IsGamepadActive())
                return false;
            return NonClassicFunctionalityMethod?.Invoke(null, null) is true;
        }
        catch
        {
            return false;
        }
    }

    static string HoverUseLine(string action) =>
        Localize("\n[<color=yellow><b>$KEY_Use</b></color>] " + action);

    /// <summary>Same prompt shape as vanilla item stands: hold Use to take.</summary>
    static string HoverHoldUseLine() =>
        Localize("\n[<color=yellow><b>$ui_hold $KEY_Use</b></color>] $piece_itemstand_take");

    static string HoverAltUseLine(string actionToken)
    {
        var keys = UseGamepadAltKeysPrompt()
            ? "$KEY_AltKeys + $KEY_Use"
            : "$KEY_AltPlace + $KEY_Use";
        return Localize("\n[<color=yellow><b>" + keys + "</b></color>] " + actionToken);
    }

    // Publicized at compile time, private at runtime. Direct access throws FieldAccessException in the HUD.
    static readonly FieldInfo? AllAreasField = AccessTools.Field(typeof(PrivateArea), "m_allAreas");
    static readonly MethodInfo? IsEnabledMethod = AccessTools.Method(typeof(PrivateArea), "IsEnabled");
    static readonly MethodInfo? IsInsideMethod =
        AccessTools.Method(typeof(PrivateArea), "IsInside", new[] { typeof(Vector3), typeof(float) });

    static bool InsideEnabledWard(Vector3 point)
    {
        try
        {
            if (AllAreasField == null || IsEnabledMethod == null || IsInsideMethod == null)
                return false;
            if (AllAreasField.GetValue(null) is not System.Collections.IList areas)
                return false;
            for (var i = 0; i < areas.Count; i++)
            {
                if (areas[i] is not PrivateArea area)
                    continue;
                if (IsEnabledMethod.Invoke(area, null) is not true)
                    continue;
                if (IsInsideMethod.Invoke(area, new object[] { point, 0f }) is true)
                    return true;
            }
        }
        catch (Exception)
        {
            return false;
        }
        return false;
    }

    internal string BuildHoverText()
    {
        var zdo = GetComponent<ZNetView>()?.GetZDO();
        string name = zdo?.GetString(ZdoRename, "") ?? "";
        string desc = zdo?.GetString(ZdoDesc, "") ?? "";
        if (string.IsNullOrEmpty(name))
            name = "Written Page";

        // Close unclosed <#RRGGBB>/<color> tags — same as inventory tooltips.
        name = TooltipRichText.EnsureRichTextTagsClosedForTooltip(name);
        desc = TooltipRichText.EnsureRichTextTagsClosedForTooltip(desc);

        var sb = name;
        if (!string.IsNullOrEmpty(desc))
        {
            // Keep hover short so wrapped desc does not sit on top of [E] Take.
            // Full letter lives on the parchment when PaperShowPageText is on.
            if (RenameitConfig.PaperShowPageText && desc.Length > 72)
                desc = desc.Substring(0, 69).TrimEnd() + "...";
            sb += "\n" + desc;
        }

        // Ward blocks Take, but the page stays readable (same idea as item-stand "no access" labels).
        // Public pages skip that gate so a TAKE ONE board works inside the ward.
        if (!MayTake(flash: false))
        {
            var denied = "$piece_noaccess";
            denied = Localize(denied);
            return sb + "\n\n" + denied;
        }

        // Blank line before prompts — Valheim HUD stacks lines tightly otherwise.
        sb += "\n";
        if (RenameitConfig.PaperHoldToTake)
            sb += HoverHoldUseLine();
        else
            sb += HoverUseLine("Take");
        if (RenameitConfig.PaperWallRenameEnabled && LocalMayEditWallPaper())
        {
            sb += HoverAltUseLine("$piece_drakes_paper_edit");
        }
        else if (CanOfferTakePublicToggle())
        {
            var token = ReadTakePublic()
                ? "$piece_drakes_paper_make_private"
                : "$piece_drakes_paper_make_public";
            sb += HoverAltUseLine(token);
        }

        return sb;
    }

    internal void TryReclaim(Humanoid character)
    {
        if (character != Player.m_localPlayer)
            return;
        if (!MayTake(flash: true))
        {
            _reclaimQueued = false;
            return;
        }

        // Ownership is async. Queue and retry once we own the ZDO so two clients
        // cannot both AddItem, and a non-owner destroy doesn't spawn a ghost drop.
        var nv = GetComponent<ZNetView>();
        if (nv != null && nv.IsValid() && !nv.IsOwner())
        {
            nv.ClaimOwnership();
            _reclaimQueued = true;
            return;
        }

        if (!TryClaimHandle())
        {
            _reclaimQueued = false;
            return;
        }

        var inv = character.GetInventory();
        var item = BuildItemFromZdo();
        if (inv == null || item == null)
        {
            ReleaseHandle();
            _reclaimQueued = false;
            return;
        }
        if (!inv.AddItem(item))
        {
            ReleaseHandle();
            _reclaimQueued = false;
            ValheimHudMessage.Show(character, MessageHud.MessageType.Center, "Inventory full.");
            return;
        }

        _reclaimQueued = false;
        DestroyPiece();
    }

    internal void DropIntoWorld()
    {
        // Only the ZDO owner may spawn the page, and only once. Instantiating on every
        // client that sees WearNTear.Destroy leaves a drop with no owner: nearby players
        // auto-pickup it every frame (infinite copies) and the piece never finishes breaking.
        if (!TryClaimHandle())
            return;

        try
        {
            var item = BuildItemFromZdo();
            if (item?.m_dropPrefab == null)
                return;

            var drop = ItemDrop.DropItem(item, 1, transform.position + Vector3.up * 0.35f, Quaternion.identity);
            if (drop != null)
                drop.m_autoPickup = false;
        }
        catch (Exception ex)
        {
            // Never throw out of WearNTear.Destroy — that aborts the break and drops again next hit.
            UnityEngine.Debug.LogWarning($"[Paper] Written drop failed: {ex.Message}");
        }
    }

    bool TryClaimHandle()
    {
        if (_handled)
            return false;

        var nv = GetComponent<ZNetView>();
        if (nv == null || !nv.IsValid() || !nv.IsOwner())
            return false;

        var zdo = nv.GetZDO();
        if (zdo == null)
            return false;
        if (zdo.GetInt(ZdoHandled, 0) == 1)
        {
            _handled = true;
            return false;
        }

        zdo.Set(ZdoHandled, 1);
        _handled = true;
        return true;
    }

    void ReleaseHandle()
    {
        _handled = false;
        var nv = GetComponent<ZNetView>();
        if (nv == null || !nv.IsValid() || !nv.IsOwner())
            return;
        nv.GetZDO()?.Set(ZdoHandled, 0);
    }

    ItemDrop.ItemData? BuildItemFromZdo()
    {
        var zdo = GetComponent<ZNetView>()?.GetZDO();
        var prefab = ObjectDB.instance?.GetItemPrefab(PaperItem.WrittenPrefabName);
        var drop = prefab?.GetComponent<ItemDrop>();
        if (zdo == null || drop?.m_itemData == null)
            return null;

        var item = drop.m_itemData.Clone();
        item.m_stack = 1;
        item.m_dropPrefab = prefab;
        item.m_customData = new Dictionary<string, string>();

        var rename = zdo.GetString(ZdoRename, "");
        var desc = zdo.GetString(ZdoDesc, "");
        if (!string.IsNullOrEmpty(rename))
            CustomizeLibsAPI.SetCustomName(item, rename);
        if (!string.IsNullOrEmpty(desc))
            CustomizeLibsAPI.SetCustomDescription(item, desc);
        if (zdo.GetInt(ZdoPublic, 0) == 1)
            Permissions.RenamePermissionManager.SetPublicRewriteFlag(item, true);
        if (zdo.GetInt(ZdoUnlock, 0) == 1)
            DrakeRenameit.SetRenameUnlocked(item);
        item.m_crafterID = zdo.GetLong(ZdoCrafterId, 0L);
        item.m_crafterName = zdo.GetString(ZdoCrafterName, "");
        PaperItemStyle.WriteAll(
            item,
            PaperFontScale.NormalizeStored(zdo.GetFloat(ZdoFontSize, RenameitConfig.PaperDefaultFontSize)),
            zdo.GetInt(ZdoLandscape, RenameitConfig.PaperDefaultLandscape ? 1 : 0) == 1,
            zdo.GetInt(ZdoTakePublic, 0) == 1);
        return item;
    }

    /// <summary>Wall-session synthetic item from this piece's ZDO.</summary>
    internal ItemDrop.ItemData? BuildItemFromZdoPublic() => BuildItemFromZdo();

    /// <summary>Push rename/desc/crafter/flags from a session item back onto the piece ZDO.</summary>
    internal void ApplyCustomizationFromItem(ItemDrop.ItemData item)
    {
        if (item == null)
            return;
        var zdo = EnsureOwnedZdo();
        if (zdo == null)
            return;

        var rename = DrakeRenameit.hasNewName(item) ? DrakeRenameit.GetPropperName(item) : "";
        var desc = DrakeRenameit.hasNewDesc(item) ? DrakeRenameit.getPropperDesc(item) : "";
        zdo.Set(ZdoRename, rename ?? "");
        zdo.Set(ZdoDesc, desc ?? "");
        zdo.Set(ZdoPublic, Permissions.RenamePermissionManager.HasPublicRewriteFlag(item) ? 1 : 0);
        zdo.Set(ZdoUnlock, DrakeRenameit.IsRenameUnlocked(item) ? 1 : 0);
        zdo.Set(ZdoCrafterId, item.m_crafterID);
        zdo.Set(ZdoCrafterName, item.m_crafterName ?? "");
        PaperItemStyle.Read(item, out var fontSize, out var landscape, out var takePublic);
        zdo.Set(ZdoFontSize, fontSize);
        zdo.Set(ZdoLandscape, landscape ? 1 : 0);
        zdo.Set(ZdoTakePublic, takePublic ? 1 : 0);
        RefreshPageVisual();
    }

    internal void ApplyPaperStyle(float fontSize, bool landscape)
    {
        var zdo = EnsureOwnedZdo();
        if (zdo == null)
            return;

        var size = PaperFontScale.NormalizeStored(fontSize);
        zdo.Set(ZdoFontSize, size);
        zdo.Set(ZdoLandscape, landscape ? 1 : 0);
        // Keep wall-session / inventory item in sync so take/place keeps Paper tab choices.
        PaperItemStyle.WriteStyle(DrakeRenameit.CurrentItem, size, landscape);
        RefreshPageVisual();
    }

    internal float ReadFontSize()
    {
        var zdo = GetComponent<ZNetView>()?.GetZDO();
        if (zdo == null)
            return PaperFontScale.NormalizeStored(RenameitConfig.PaperDefaultFontSize);
        return PaperFontScale.NormalizeStored(
            zdo.GetFloat(ZdoFontSize, RenameitConfig.PaperDefaultFontSize));
    }

    internal bool ReadLandscape()
    {
        var zdo = GetComponent<ZNetView>()?.GetZDO();
        if (zdo == null)
            return RenameitConfig.PaperDefaultLandscape;
        return zdo.GetInt(ZdoLandscape, RenameitConfig.PaperDefaultLandscape ? 1 : 0) == 1;
    }

    /// <summary>Set take-public to an absolute value (Paper tab), with HUD feedback.</summary>
    internal void RequestSetTakePublic(bool on)
    {
        if (Player.m_localPlayer == null)
            return;
        var nv = GetComponent<ZNetView>();
        if (nv == null || !nv.IsValid())
            return;
        if (!LocalMayToggleTakePublic())
            return;

        var next = on ? 1 : 0;
        if (ReadTakePublic() == on)
            return;
        nv.InvokeRPC(RpcSetTakePublic, next);
        var msg = next == 1 ? "$piece_drakes_paper_now_public" : "$piece_drakes_paper_now_private";
        ValheimHudMessage.Show(Player.m_localPlayer, MessageHud.MessageType.Center, Localize(msg));
    }

    internal bool CanOfferTakePublicTogglePublic() => CanOfferTakePublicToggle();
    internal bool LocalMayToggleTakePublicPublic() => LocalMayToggleTakePublic();
    internal bool ReadTakePublicPublic() => ReadTakePublic();

    ZDO? EnsureOwnedZdo()
    {
        var nv = GetComponent<ZNetView>();
        if (nv == null || !nv.IsValid())
            return null;
        if (!nv.IsOwner())
            nv.ClaimOwnership();
        return nv.GetZDO();
    }

    void DestroyPiece()
    {
        var nv = GetComponent<ZNetView>();
        if (nv != null && nv.IsValid())
            nv.Destroy();
        else
            Destroy(gameObject);
    }
}
