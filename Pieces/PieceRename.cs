using System;
using System.Collections.Generic;
using System.Linq;
using DrakeModsLibs.UI;
using DrakeRenameit.API;
using DrakeRenameit.Paper.Pieces;
using HarmonyLib;
using UnityEngine;

namespace DrakeRenameit.Pieces;

/// <summary>
/// Custom hover names for placed pieces (chests, stations, doors, beds, portals, …). The name lives on the piece's ZDO,
/// so every client sees it. The builder renames with Shift+Use (vanilla alt-interact) when a ward covering the piece
/// lets them in; admins/VIP always can. Empty text restores the normal name.
/// </summary>
internal static class PieceRename
{
    internal const string ZdoKey = "DrakePiece_Name";
    internal const string ZdoSharedKey = "DrakePiece_Shared";
    static readonly int ZdoHash = ZdoKey.GetStableHashCode();
    static readonly int ZdoSharedHash = ZdoSharedKey.GetStableHashCode();
    /// <summary>Raw length cap (tags included); the window limits the visible characters to NameCharacterLimit.</summary>
    const int MaxRawLength = 512;
    static readonly DrakeTextPromptPanel Prompt = new("drakes_renameit_piece_prompt");

    /// <summary>The piece behind a hovered object when it can carry a custom name (feature on, has a hover name, not skipped).</summary>
    internal static Piece? NameablePiece(GameObject? hovered)
    {
        if (hovered == null || !RenameitConfig.PieceRenameEnabled)
            return null;
        var piece = hovered.GetComponentInParent<Piece>();
        if (piece == null)
            return null;
        // Another mod owns Shift+Use here (a Team Ward's settings, or LockSmith's Lock menu on a chest or door).
        // Leave it alone, admins included. Rename for those pieces is a tab in that menu, not this workshop.
        if (DrakeModsLibs.API.PieceInteractionClaims.IsClaimed(piece))
            return null;
        // Only pieces that show a name on hover, and only when that hover belongs to this piece.
        if (hovered.GetComponentInParent<Hoverable>() is not Component hoverable || !IsNameableHover(piece, hoverable))
            return null;
        var nview = piece.GetComponent<ZNetView>();
        return nview != null && nview.IsValid() ? piece : null;
    }

    /// <summary>
    /// Vanilla already uses Shift+Use on fires (a hot tub's heater, a campfire) and item stands; signs and paper have
    /// their own text. Judged by the part being looked at, so a hot tub's seat is renamable and its heater is not.
    /// </summary>
    static bool IsNameableHover(Piece piece, Component hoverable) =>
        OwningPiece(hoverable) == piece
        && hoverable is not Fireplace && hoverable is not ItemStand && hoverable is not Sign
        && piece.GetComponent<PaperWrittenVessel>() == null;

    /// <summary>
    /// The piece whose custom name a hovered part should show: any part of a renamed piece (a hot tub's fuel hatch reads
    /// "Hot Tubyy (Wood 0/10)"), except signs and paper, which show their own text. Null when the feature is off.
    /// </summary>
    internal static Piece? NamedPieceForDisplay(GameObject? hovered)
    {
        if (hovered == null || !RenameitConfig.PieceRenameEnabled)
            return null;
        var piece = hovered.GetComponentInParent<Piece>();
        if (piece == null || piece.GetComponent<PaperWrittenVessel>() != null || hovered.GetComponentInParent<Sign>() != null)
            return null;
        if (hovered.GetComponentInParent<Hoverable>() is not Component part || part.GetComponentInParent<Piece>() != piece)
            return null;
        return GetCustomName(piece).Length > 0 ? piece : null;
    }

    /// <summary>The nearest Piece above a part, also on inactive prefabs (GetComponentInParent skips those).</summary>
    static Piece? OwningPiece(Component part)
    {
        for (var t = part.transform; t != null; t = t.parent)
        {
            if (t.GetComponent<Piece>() is Piece piece)
                return piece;
        }
        return null;
    }

    /// <summary>The first part of the piece that shows a nameable hover (tests and tools that have no crosshair).</summary>
    /// <summary>
    /// Prefers the piece's own part (a hot tub's seat) over a Switch (its wood hatch, a smelter's ore and coal slots);
    /// a smelter only has switches, so one of those is used then.
    /// </summary>
    internal static Component? FirstNameableHover(Piece piece)
    {
        Component? fallback = null;
        // includeInactive: prefabs (the reference file lists them) are inactive objects.
        foreach (var part in piece.GetComponentsInChildren<Component>(true))
        {
            if (part is not Hoverable || !IsNameableHover(piece, part))
                continue;
            if (part is not Switch)
                return part;
            fallback ??= part;
        }
        return fallback;
    }

    // ---------------------------------------------------------------- exclusions (12 Pieces)

    static readonly ParsedList Excluded = new();
    static readonly ParsedList Categories = new();
    static readonly ParsedList Allowed = new();

    /// <summary>A comma/semicolon list from config, re-parsed only when the text changes.</summary>
    sealed class ParsedList
    {
        string _raw = "\0";
        HashSet<string> _set = new(StringComparer.OrdinalIgnoreCase);

        public HashSet<string> Get(string? raw)
        {
            raw ??= "";
            if (raw == _raw)
                return _set;
            _raw = raw;
            _set = new HashSet<string>(
                raw.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).Where(s => s.Length > 0),
                StringComparer.OrdinalIgnoreCase);
            return _set;
        }
    }

    /// <summary>Prefab name (<c>piece_chest_wood</c>), token (<c>$piece_chestwood</c>) or the name players see (<c>Chest</c>).</summary>
    static bool MatchesName(Piece piece, HashSet<string> set)
    {
        if (set.Count == 0)
            return false;
        var token = piece.m_name ?? "";
        return set.Contains(Utils.GetPrefabName(piece.gameObject)) || (token.Length > 0 && set.Contains(token))
               || set.Contains(Localize(token));
    }

    /// <summary>
    /// The piece's build-menu category (Misc, Crafting, BuildingWorkbench, BuildingStonecutter, Furniture, …) plus what it does,
    /// which is how players think of them: Chest, Door, Bed, Station, Smelter, Cooking, Portal, Ward, Seat.
    /// </summary>
    internal static IEnumerable<string> CategoriesOf(Piece piece)
    {
        yield return CategoryName(piece.m_category);
        if (piece.GetComponentInChildren<Container>(true) != null) yield return "Chest";
        if (piece.GetComponentInChildren<Door>(true) != null) yield return "Door";
        if (piece.GetComponentInChildren<Bed>(true) != null) yield return "Bed";
        if (piece.GetComponentInChildren<CraftingStation>(true) != null || piece.GetComponentInChildren<StationExtension>(true) != null) yield return "Station";
        if (piece.GetComponentInChildren<Smelter>(true) != null) yield return "Smelter";
        if (piece.GetComponentInChildren<CookingStation>(true) != null || piece.GetComponentInChildren<Fermenter>(true) != null) yield return "Cooking";
        if (piece.GetComponentInChildren<TeleportWorld>(true) != null) yield return "Portal";
        if (piece.GetComponentInChildren<PrivateArea>(true) != null) yield return "Ward";
        if (piece.GetComponentInChildren<Chair>(true) != null) yield return "Seat";
    }

    static readonly Dictionary<int, string> CustomCategoryNames = new();

    /// <summary>Vanilla categories by enum name; ones mods add through Jotunn (ItemShop's and so on) by their Jotunn name, not a number.</summary>
    internal static string CategoryName(Piece.PieceCategory category)
    {
        if (Enum.IsDefined(typeof(Piece.PieceCategory), category))
            return category.ToString();
        if (CustomCategoryNames.TryGetValue((int)category, out var known))
            return known;
        var name = category.ToString();
        try
        {
            if (Jotunn.Managers.PieceManager.Instance.GetPieceCategoriesMap().TryGetValue(category, out var jotunn) && !string.IsNullOrEmpty(jotunn))
                name = jotunn;
        }
        catch
        {
            // Jotunn API drift: keep the number
        }
        CustomCategoryNames[(int)category] = name;
        return name;
    }

    /// <summary>PieceExcludedNames / PieceExcludedCategory, unless PieceRenameAllowlist names it.</summary>
    internal static bool IsExcluded(Piece piece)
    {
        if (MatchesName(piece, Allowed.Get(RenameitConfig.PieceRenameAllowlist)))
            return false;
        if (MatchesName(piece, Excluded.Get(RenameitConfig.PieceExcludedNames)))
            return true;
        var categories = Categories.Get(RenameitConfig.PieceExcludedCategory);
        return categories.Count > 0 && CategoriesOf(piece).Any(categories.Contains);
    }

    // ---------------------------------------------------------------- who may rename

    internal static bool IsBuilder(Piece piece, Player? player) =>
        player != null && piece.GetCreator() != 0 && piece.GetCreator() == player.GetPlayerID();

    /// <summary>False for pieces the world generated (no builder): dungeon chests, crypt doors, village props.</summary>
    internal static bool IsPlayerBuilt(Piece piece) => piece.GetCreator() != 0;

    static bool HasWardAccess(Piece piece, bool flash) =>
        !RenameitConfig.PieceRequireWardAccess || PrivateArea.CheckAccess(piece.transform.position, 0f, flash, false);

    /// <summary>
    /// Admins/VIP (AllowAdminOverride) always can while the feature is on. Everyone else: not admin-only, not excluded,
    /// the builder (or anyone when PieceLockToBuilder is off or the piece is Shared), with ward access when required.
    /// <paramref name="flash"/> flashes the ward like vanilla when denied.
    /// </summary>
    internal static bool CanRename(Piece piece, Player? player, bool flash)
    {
        if (player == null || !RenameitConfig.PieceRenameEnabled)
            return false;
        if (RenameitPermission.IsElevatedForOverrides(player))
            return true;
        if (RenameitConfig.PieceRenameAdminOnly || IsExcluded(piece))
            return false;
        // Built by the world (dungeon chests, crypt doors): admins only, whatever the builder / Shared settings say.
        if (!IsPlayerBuilt(piece))
            return false;
        var mayEdit = !RenameitConfig.PieceLockToBuilder || IsBuilder(piece, player)
                      || (RenameitConfig.PieceSharedEnabled && IsShared(piece));
        return mayEdit && HasWardAccess(piece, flash);
    }

    /// <summary>Only the builder (with ward access when required) or an admin/VIP may turn Shared on or off.</summary>
    internal static bool CanChangeShared(Piece piece, Player? player)
    {
        if (player == null || !RenameitConfig.PieceRenameEnabled || !RenameitConfig.PieceSharedEnabled)
            return false;
        if (RenameitPermission.IsElevatedForOverrides(player))
            return true;
        return !RenameitConfig.PieceRenameAdminOnly && !IsExcluded(piece) && IsBuilder(piece, player)
               && HasWardAccess(piece, false);
    }

    internal static bool IsShared(Piece piece)
    {
        var zdo = piece.GetComponent<ZNetView>()?.GetZDO();
        return zdo != null && zdo.GetBool(ZdoSharedHash, false);
    }

    internal static void SetShared(Piece piece, bool on)
    {
        var nview = piece.GetComponent<ZNetView>();
        if (nview == null || !nview.IsValid())
            return;
        Write(nview, RpcSetShared, on, zdo => zdo.Set(ZdoSharedHash, on));
    }

    internal static string GetCustomName(Piece piece)
    {
        var zdo = piece.GetComponent<ZNetView>()?.GetZDO();
        return zdo != null ? zdo.GetString(ZdoHash, "") : "";
    }

    internal static void SetCustomName(Piece piece, string? name)
    {
        var nview = piece.GetComponent<ZNetView>();
        if (nview == null || !nview.IsValid())
            return;
        var value = (name ?? "").Trim();
        if (value.Length > MaxRawLength)
            value = value.Substring(0, MaxRawLength);
        Write(nview, RpcSetName, value, zdo => zdo.Set(ZdoHash, value));
    }

    // ---------------------------------------------------------------- writing through the owner

    const string RpcSetName = "DrakePiece_RPC_SetName";
    const string RpcSetShared = "DrakePiece_RPC_SetShared";

    /// <summary>
    /// Write on the piece's ZDO. When another player's client owns it (they have the chest open, or simply stand nearer),
    /// ask that client to write it instead of claiming the piece: taking ownership closes an open chest on their screen.
    /// </summary>
    static void Write(ZNetView nview, string rpc, object value, Action<ZDO> apply)
    {
        var zdo = nview.GetZDO();
        if (zdo.HasOwner() && !nview.IsOwner())
        {
            nview.InvokeRPC(rpc, value);
            return;
        }

        nview.ClaimOwnership();
        apply(zdo);
    }

    /// <summary>The owner side of <see cref="Write"/>: registered on every piece's ZNetView when it wakes.</summary>
    internal static void RegisterRpcs(ZNetView? nview)
    {
        if (nview == null || !nview.IsValid())
            return;
        nview.Unregister(RpcSetName);
        nview.Register<string>(RpcSetName, (sender, name) =>
        {
            if (nview.IsValid() && nview.IsOwner())
                nview.GetZDO().Set(ZdoHash, name ?? "");
        });
        nview.Unregister(RpcSetShared);
        nview.Register<bool>(RpcSetShared, (sender, on) =>
        {
            if (nview.IsValid() && nview.IsOwner())
                nview.GetZDO().Set(ZdoSharedHash, on);
        });
    }

    /// <summary>Custom name if set (and the feature is on), else null.</summary>
    internal static string? DisplayName(Component? anyPart)
    {
        if (anyPart == null || !RenameitConfig.PieceRenameEnabled)
            return null;
        var piece = anyPart.GetComponentInParent<Piece>();
        if (piece == null)
            return null;
        var name = GetCustomName(piece);
        return name.Length > 0 ? name : null;
    }

    internal static string DefaultName(Piece piece) => Localize(piece.m_name);

    /// <summary>The piece Workshop (UI Toolkit), or the plain name prompt when the old UI is selected.</summary>
    internal static void OpenEditor(Piece piece)
    {
        if (RenameitConfig.UseToolkitUi)
        {
            try
            {
                UI.Toolkit.PieceWorkshopPanel.Show(piece);
                return;
            }
            catch (Exception ex)
            {
                RenameitConfig.Log?.LogWarning($"[PieceRename] Workshop window failed, using the plain prompt: {ex.Message}");
            }
        }
        OpenPrompt(piece);
    }

    static void OpenPrompt(Piece piece)
    {
        var current = GetCustomName(piece);
        Prompt.Show(
            "Rename " + DefaultName(piece),
            current,
            text =>
            {
                if (piece == null || !CanRename(piece, Player.m_localPlayer, flash: true))
                    return;
                SetCustomName(piece, text);
            },
            charLimit: Math.Max(1, RenameitConfig.PieceNameCharLimit),
            okLabel: "Save");
    }

    internal static bool PromptOpen => Prompt.IsOpen || UI.Toolkit.PieceWorkshopPanel.IsOpen;

    internal static string HintLine() =>
        Localize("\n[<color=yellow><b>$KEY_AltPlace + $KEY_Use</b></color>] Rename");

    /// <summary>
    /// Hover text as the HUD shows it: the title line's name swapped for the custom one, plus the Rename hint when allowed.
    /// <paramref name="hoverable"/> is the part being looked at; its own name is what the title shows (a door's, a chest's,
    /// or the seat inside a hot tub, which vanilla calls "Wood Bench"), so that is replaced first.
    /// </summary>
    internal static string ApplyToHoverText(Piece piece, string text, Player? player, Hoverable? hoverable = null, bool offerRename = true)
    {
        // A portal's vanilla title is one long line (name, Tag:"x" and the connection state): name on the first line, the tag on its own.
        if (piece.GetComponent<TeleportWorld>() != null)
            text = SplitPortalTitle(text);
        var custom = GetCustomName(piece);
        if (custom.Length > 0)
            text = ReplaceTitleName(piece, text, custom, hoverable ?? FirstNameableHover(piece) as Hoverable);
        if (custom.Length > 0 && RenameitConfig.PieceCrafterLineShown)
        {
            var builder = CreatorName(piece);
            if (builder.Length > 0)
                text += "\n<color=#f2e6c0>" + RenameitConfig.PieceCrafterLineStyle + ": " + builder + "</color>";
        }

        if (offerRename && CanRename(piece, player, flash: false))
            text += HintLine();
        return text;
    }

    /// <summary>"Portal Tag:\"x\" [CONNECTED]" becomes "Portal" and, on the next line, "Tag: x [CONNECTED]". Anything else is left alone.</summary>
    static string SplitPortalTitle(string text)
    {
        var nl = text.IndexOf('\n');
        var title = nl >= 0 ? text.Substring(0, nl) : text;
        var rest = nl >= 0 ? text.Substring(nl) : "";
        var quote = title.IndexOf(":\"", StringComparison.Ordinal);
        if (quote <= 0)
            return text;
        var space = title.LastIndexOf(' ', quote);
        if (space <= 0)
            return text;
        var tag = title.Substring(space + 1).Replace(":\"", ": ");
        var close = tag.IndexOf('"');
        if (close >= 0)
            tag = tag.Remove(close, 1);
        return title.Substring(0, space) + "\n" + tag + rest;
    }

    static readonly Dictionary<long, string> CreatorNames = new();

    /// <summary>
    /// Who built the piece. Valheim 1.0 keeps a world player history and the piece stores an index into it; that API is not in the
    /// stub assemblies this builds against, so it is read by reflection. Falls back to the local player and to players who are online.
    /// Empty when unknown (a piece from an older world, or a builder not seen yet).
    /// </summary>
    internal static string CreatorName(Piece piece)
    {
        var creator = piece.GetCreator();
        if (creator == 0L)
            return "";
        if (CreatorNames.TryGetValue(creator, out var known))
            return known;

        var name = "";
        if (Player.m_localPlayer != null && Player.m_localPlayer.GetPlayerID() == creator)
        {
            name = Player.m_localPlayer.GetPlayerName();
        }
        else
        {
            try
            {
                var index = AccessTools.Method(typeof(Piece), "GetCreatorPlatformUserIdIndex")?.Invoke(piece, null) is int i ? i : -1;
                var world = AccessTools.Property(typeof(ZNet), "World")?.GetValue(null) ?? AccessTools.Field(typeof(ZNet), "World")?.GetValue(null);
                var history = world == null ? null : AccessTools.Field(world.GetType(), "m_playerHistory")?.GetValue(world) as System.Collections.IList;
                if (history != null && index >= 0 && index < history.Count)
                {
                    var info = history[index];
                    name = AccessTools.Field(info.GetType(), "m_displayName")?.GetValue(info) as string ?? "";
                }
            }
            catch
            {
                /* older game version without the history: use the online-player fallback below */
            }

            if (name.Length == 0)
            {
                foreach (var other in Player.GetAllPlayers())
                {
                    if (other.GetPlayerID() == creator)
                    {
                        name = other.GetPlayerName();
                        break;
                    }
                }
            }
        }

        // Only remember a real answer: a builder who is offline now may resolve later.
        if (name.Length > 0)
            CreatorNames[creator] = name;
        return name;
    }

    /// <summary>Only the first line is the title; keep whatever follows the name on it (e.g. a chest's "( Empty )").</summary>
    static string ReplaceTitleName(Piece piece, string text, string custom, Hoverable? hoverable)
    {
        var nl = text.IndexOf('\n');
        var title = nl >= 0 ? text.Substring(0, nl) : text;
        var rest = nl >= 0 ? text.Substring(nl) : "";

        string? hoverName = null;
        try
        {
            hoverName = hoverable?.GetHoverName();
        }
        catch
        {
            /* some mods' hoverables throw here; fall through to the other names */
        }

        var candidates = new[] { Localize(hoverName), Localize(piece.m_name), Localize(piece.GetComponent<Container>()?.m_name) };
        foreach (var name in candidates)
        {
            if (name.Length == 0)
                continue;
            var at = title.IndexOf(name, StringComparison.OrdinalIgnoreCase);
            if (at >= 0)
                return title.Substring(0, at) + custom + title.Substring(at + name.Length) + rest;
        }

        // Unknown shape: the whole title line is the name.
        return custom + rest;
    }

    static string Localize(string? text) =>
        string.IsNullOrEmpty(text) ? "" : Localization.instance != null ? Localization.instance.Localize(text) : text!;
}

[HarmonyPatch]
internal static class PieceRenamePatches
{
    // Never ldfld Hud.m_hoverName directly — the compile ref can disagree with the live game (see PaperWrittenPlace).
    static readonly System.Reflection.FieldInfo? HoverNameField = AccessTools.Field(typeof(Hud), "m_hoverName");

    /// <summary>Owner side of a rename sent by another client (see PieceRename.Write).</summary>
    [HarmonyPatch(typeof(Piece), "Awake")]
    [HarmonyPostfix]
    static void PieceAwake(Piece __instance)
    {
        try
        {
            // only pieces with a hover can carry a custom name (walls and floors never get these RPCs); one cheap lookup per piece
            if (__instance.GetComponentInChildren<Hoverable>(true) != null)
                PieceRename.RegisterRpcs(__instance.GetComponent<ZNetView>());
        }
        catch (Exception ex)
        {
            RenameitConfig.Log?.LogWarning($"Piece rename RPCs not registered on {__instance.name}: {ex.Message}");
        }
    }

    /// <summary>Swap the piece's normal name for its custom one in the hover text, and add the Rename hint.</summary>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(Hud), "UpdateCrosshair")]
    static void UpdateCrosshair_Postfix(Hud __instance, Player player)
    {
        try
        {
            if (__instance == null || player == null)
                return;
            var hovered = player.GetHoverObject();
            // Renamable part: name + Rename hint. Any other part of a renamed piece: just the name.
            var nameable = PieceRename.NameablePiece(hovered);
            var piece = nameable ?? PieceRename.NamedPieceForDisplay(hovered);
            if (piece == null)
                return;
            if (HoverNameField?.GetValue(__instance) is not TMPro.TMP_Text hoverName || hoverName == null)
                return;
            var text = hoverName.text;
            if (string.IsNullOrEmpty(text))
            {
                // A piece that is a creature (the training dummy) has no hover text of its own unless it is tame: start from its name.
                var creature = piece.GetComponent<Character>();
                if (creature == null || piece.GetComponent<Tameable>() != null)
                    return;
                text = creature.GetHoverName();
                if (string.IsNullOrEmpty(text))
                    return;
            }

            hoverName.text = PieceRename.ApplyToHoverText(piece, text, player, hovered.GetComponentInParent<Hoverable>(), offerRename: nameable != null);
        }
        catch
        {
            /* never break the HUD, never log per frame */
        }
    }

    /// <summary>
    /// Pieces that are creatures (the training dummy) show their name in the label over their head and in the hover; both read
    /// Character.GetHoverName, so a custom name on the piece goes there too. Tame animals have their own rename (AnimalRename).
    /// </summary>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(Character), nameof(Character.GetHoverName))]
    static void CharacterHoverName_Postfix(Character __instance, ref string __result)
    {
        try
        {
            if (!RenameitConfig.PieceRenameEnabled || __instance.GetComponent<Tameable>() != null)
                return;
            var piece = __instance.GetComponent<Piece>();
            if (piece == null)
                return;
            var custom = PieceRename.GetCustomName(piece);
            if (custom.Length > 0)
                __result = custom;
        }
        catch
        {
            /* a name must never throw */
        }
    }

    /// <summary>Shift+Use opens the name prompt instead of the piece's own alt action (none for the pieces we accept).</summary>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(Player), "Interact", new[] { typeof(GameObject), typeof(bool), typeof(bool) })]
    static bool Interact_Prefix(Player __instance, GameObject go, bool hold, bool alt)
    {
        try
        {
            if (!alt || hold || __instance == null || __instance != Player.m_localPlayer || PieceRename.PromptOpen)
                return true;
            var piece = PieceRename.NameablePiece(go);
            if (piece == null)
                return true;
            if (!PieceRename.CanRename(piece, __instance, flash: true))
                return true;
            PieceRename.OpenEditor(piece);
            return false;
        }
        catch
        {
            return true;
        }
    }

    /// <summary>Chest windows (and anything else that asks a container for its name) show the custom name.</summary>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(Container), nameof(Container.GetHoverName))]
    static void Container_GetHoverName_Postfix(Container __instance, ref string __result)
    {
        try
        {
            var custom = PieceRename.DisplayName(__instance);
            if (custom != null)
                __result = custom;
        }
        catch
        {
            /* keep vanilla name */
        }
    }
}
