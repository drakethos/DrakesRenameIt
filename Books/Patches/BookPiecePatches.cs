using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace DrakeRenameit.Books;

/// <summary>Hover text and Use for a placed book, routed by Harmony (the behaviour cannot implement the game's interfaces; see its note).</summary>
[HarmonyPatch]
internal static class BookPiecePatches
{
    static readonly FieldInfo? HoverNameField = AccessTools.Field(typeof(Hud), "m_hoverName");
    static readonly FieldInfo? CrosshairField = AccessTools.Field(typeof(Hud), "m_crosshair");
    static readonly FieldInfo? LastHoverInteractTimeField = AccessTools.Field(typeof(Player), "m_lastHoverInteractTime");

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Hud), "UpdateCrosshair")]
    static void UpdateCrosshair_Postfix(Hud __instance, Player player)
    {
        try
        {
            if (__instance == null || player == null)
                return;
            var hover = player.GetHoverObject();
            var book = hover != null ? hover.GetComponentInParent<BookPieceBehaviour>() : null;
            if (book == null || HoverNameField?.GetValue(__instance) is not TMPro.TextMeshProUGUI hoverName || hoverName == null)
                return;
            hoverName.text = Localization.instance != null ? Localization.instance.Localize(book.GetHoverText()) : book.GetHoverText();
            if (CrosshairField?.GetValue(__instance) is UnityEngine.UI.Image cross && cross != null)
                cross.color = Color.yellow;
        }
        catch
        {
            /* never break the HUD */
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Player), "Interact", new[] { typeof(GameObject), typeof(bool), typeof(bool) })]
    static bool Interact_Prefix(Player __instance, GameObject go, bool hold, bool alt)
    {
        try
        {
            var book = go != null ? go.GetComponentInParent<BookPieceBehaviour>() : null;
            if (book == null || __instance == null)
                return true;
            if (__instance.InAttack() || __instance.InDodge())
                return false;
            if (hold && LastHoverInteractTimeField != null && Time.time - (float)LastHoverInteractTimeField.GetValue(__instance)! < 0.2f)
                return false;
            LastHoverInteractTimeField?.SetValue(__instance, Time.time);
            book.Interact(__instance, hold, alt);
            return false;
        }
        catch
        {
            return true;
        }
    }
}
