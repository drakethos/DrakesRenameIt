using DrakeRenameit.UI.Toolkit;
using HarmonyLib;

namespace DrakeRenameit.Patches;

/// <summary>
/// Hooks the HUD after it has set its crosshair and hover text for the frame, so <see cref="WorldHover"/> sees the final string
/// (including what other mods added) and can draw it in the player's chosen style. Does nothing in "Vanilla" style.
/// </summary>
[HarmonyPatch(typeof(Hud), "UpdateCrosshair")]
internal static class WorldHoverPatches
{
    [HarmonyPostfix]
    [HarmonyPriority(Priority.Last)] // after every other mod (and Paper's own wall-page hover) has written its text
    private static void UpdateCrosshair_Postfix(Hud __instance, Player player) => WorldHover.Tick(__instance, player);
}
