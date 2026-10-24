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
    static readonly int ZdoHash = ZdoKey.GetStableHashCode();
    static readonly DrakeTextPromptPanel Prompt = new("drakes_renameit_piece_prompt");

    static string _excludedRaw = "\0";
    static HashSet<string> _excluded = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The piece behind a hovered object when it can carry a custom name (feature on, has a hover name, not skipped).</summary>
    internal static Piece? NameablePiece(GameObject? hovered)
    {
        if (hovered == null || !RenameitConfig.PieceRenameEnabled)
            return null;
        var piece = hovered.GetComponentInParent<Piece>();
        if (piece == null)
            return null;
        // Only pieces that show a name on hover, and only when that hover belongs to this piece.
        if (hovered.GetComponentInParent<Hoverable>() is not Component hoverable || hoverable.GetComponentInParent<Piece>() != piece)
            return null;
        // Vanilla already uses Shift+Use on these; paper and signs have their own text.
        if (piece.GetComponent<Fireplace>() != null || piece.GetComponent<ItemStand>() != null
            || piece.GetComponent<Sign>() != null || piece.GetComponent<PaperWrittenVessel>() != null)
            return null;
        var nview = piece.GetComponent<ZNetView>();
        return nview != null && nview.IsValid() ? piece : null;
    }

    internal static bool IsExcluded(Piece piece)
    {
        var raw = RenameitConfig.PieceRenameExcluded ?? "";
        if (raw != _excludedRaw)
        {
            _excludedRaw = raw;
            _excluded = new HashSet<string>(
                raw.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).Where(s => s.Length > 0),
                StringComparer.OrdinalIgnoreCase);
        }
        return _excluded.Count > 0 && _excluded.Contains(Utils.GetPrefabName(piece.gameObject));
    }

    /// <summary>Builder with ward access, or an admin/VIP. <paramref name="flash"/> flashes the ward like vanilla when denied.</summary>
    internal static bool CanRename(Piece piece, Player? player, bool flash)
    {
        if (player == null)
            return false;
        if (RenameitPermission.IsElevatedForOverrides(player))
            return true;
        if (IsExcluded(piece))
            return false;
        if (piece.GetCreator() == 0 || piece.GetCreator() != player.GetPlayerID())
            return false;
        return PrivateArea.CheckAccess(piece.transform.position, 0f, flash, false);
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
        var limit = Math.Max(1, RenameitConfig.NameCharLimit);
        var value = (name ?? "").Trim();
        if (value.Length > limit)
            value = value.Substring(0, limit);
        // Same as vanilla signs: take ownership, then write.
        nview.ClaimOwnership();
        nview.GetZDO().Set(ZdoHash, value);
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

    static string DefaultName(Piece piece) => Localize(piece.m_name);

    internal static void OpenPrompt(Piece piece)
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
            charLimit: Math.Max(1, RenameitConfig.NameCharLimit),
            okLabel: "Save");
    }

    internal static bool PromptOpen => Prompt.IsOpen;

    internal static string HintLine() =>
        Localize("\n[<color=yellow><b>$KEY_AltPlace + $KEY_Use</b></color>] Rename");

    /// <summary>Hover text as the HUD shows it: the normal name swapped for the custom one, plus the Rename hint when allowed.</summary>
    internal static string ApplyToHoverText(Piece piece, string text, Player? player)
    {
        var custom = GetCustomName(piece);
        if (custom.Length > 0)
        {
            // Hover text starts with the piece's own name; chests use Container.m_name, which can differ.
            var original = Localize(piece.m_name);
            var at = original.Length > 0 ? text.IndexOf(original, StringComparison.Ordinal) : -1;
            if (at < 0 && piece.GetComponent<Container>() is Container box && box != null)
            {
                original = Localize(box.m_name);
                at = original.Length > 0 ? text.IndexOf(original, StringComparison.Ordinal) : -1;
            }
            text = at >= 0 ? text.Substring(0, at) + custom + text.Substring(at + original.Length) : custom + "\n" + text;
        }
        if (CanRename(piece, player, flash: false))
            text += HintLine();
        return text;
    }

    static string Localize(string? text) =>
        string.IsNullOrEmpty(text) ? "" : Localization.instance != null ? Localization.instance.Localize(text) : text!;
}

[HarmonyPatch]
internal static class PieceRenamePatches
{
    // Never ldfld Hud.m_hoverName directly — the compile ref can disagree with the live game (see PaperWrittenPlace).
    static readonly System.Reflection.FieldInfo? HoverNameField = AccessTools.Field(typeof(Hud), "m_hoverName");

    /// <summary>Swap the piece's normal name for its custom one in the hover text, and add the Rename hint.</summary>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(Hud), "UpdateCrosshair")]
    static void UpdateCrosshair_Postfix(Hud __instance, Player player)
    {
        try
        {
            if (__instance == null || player == null)
                return;
            var piece = PieceRename.NameablePiece(player.GetHoverObject());
            if (piece == null)
                return;
            if (HoverNameField?.GetValue(__instance) is not TMPro.TMP_Text hoverName || hoverName == null)
                return;
            var text = hoverName.text;
            if (string.IsNullOrEmpty(text))
                return;

            hoverName.text = PieceRename.ApplyToHoverText(piece, text, player);
        }
        catch
        {
            /* never break the HUD, never log per frame */
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
            PieceRename.OpenPrompt(piece);
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
