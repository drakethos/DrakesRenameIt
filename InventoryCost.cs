using System.Collections.Generic;
using DrakeRenameit.ModText;
using static DrakeRenameit.ModText.RenameItLocalization;

namespace DrakeRenameit;

/// <summary>
/// Shared inventory pay: afford / consume / have-color for UnlockCost and Paper copy.
/// Vanilla DevCommands <c>nocost</c> (<see cref="Player.NoCostCheat"/>) skips material pay.
/// </summary>
internal static class InventoryCost
{
    public static bool IsNoCostCheat(Player? player) =>
        player != null && player.NoCostCheat();

    /// <summary>
    /// True when nocost is on, lines are empty, or the player has every line in inventory.
    /// </summary>
    public static bool CanAfford(Player? player, IReadOnlyList<(string SharedName, int Amount)>? lines)
    {
        if (player == null)
            return false;
        if (IsNoCostCheat(player))
            return true;
        if (lines == null || lines.Count == 0)
            return true;

        var inv = player.GetInventory();
        if (inv == null)
            return false;

        foreach (var (sharedName, amount) in lines)
        {
            if (string.IsNullOrEmpty(sharedName) || amount <= 0)
                continue;
            if (inv.CountItems(sharedName) < amount)
                return false;
        }

        return true;
    }

    /// <summary>
    /// Removes cost lines unless nocost. Caller must have already validated lines exist when required.
    /// </summary>
    public static bool TryConsume(
        Player? player,
        IReadOnlyList<(string SharedName, int Amount)> lines,
        out string errorMessage,
        string? notEnoughMessage = null)
    {
        errorMessage = "";
        if (player == null)
        {
            errorMessage = T(LKeys.UnlockErrNoPlayer);
            return false;
        }

        if (IsNoCostCheat(player))
            return true;

        if (lines == null || lines.Count == 0)
        {
            errorMessage = T(LKeys.UnlockErrEmpty);
            return false;
        }

        var inv = player.GetInventory();
        if (inv == null)
        {
            errorMessage = T(LKeys.UnlockErrNoInventory);
            return false;
        }

        foreach (var (sharedName, amount) in lines)
        {
            if (string.IsNullOrEmpty(sharedName) || amount <= 0)
                continue;
            if (inv.CountItems(sharedName) < amount)
            {
                errorMessage = notEnoughMessage ?? T(LKeys.UnlockErrNotEnough);
                return false;
            }
        }

        foreach (var (sharedName, amount) in lines)
        {
            if (string.IsNullOrEmpty(sharedName) || amount <= 0)
                continue;
            inv.RemoveItem(sharedName, amount, -1, true);
        }

        return true;
    }

    public static int CountHave(Player? player, string sharedName)
    {
        if (player == null || string.IsNullOrEmpty(sharedName))
            return 0;
        return player.GetInventory()?.CountItems(sharedName) ?? 0;
    }

    /// <summary>lime when enough (or nocost); red when short.</summary>
    public static string HaveColor(int have, int need, bool noCostCheat) =>
        noCostCheat || have >= need ? "lime" : "red";

    /// <summary>Rich line matching unlock cost: "{n}x Name  &lt;color=…&gt;(have in inv)&lt;/color&gt;".</summary>
    public static string FormatCostLine(
        int amount,
        string localizedName,
        int have,
        bool noCostCheat)
    {
        var color = HaveColor(have, amount, noCostCheat);
        return T(LKeys.UnlockCostLine, amount, localizedName, color, have);
    }
}
