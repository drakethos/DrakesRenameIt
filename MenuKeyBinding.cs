using System;
using System.Collections.Generic;
using System.Linq;
using DrakeRenameit.ModText;
using UnityEngine;
using static DrakeRenameit.ModText.RenameItLocalization;

namespace DrakeRenameit;

/// <summary>Parses <see cref="RenameitConfig.MenuOpenModifier"/> and tests held keys (supports combos like Shift+Alt).</summary>
public static class MenuKeyBinding
{
    /// <summary>When the binding is empty or <c>None</c>, only right-click is required (no modifier).</summary>
    public static bool IsHeld(string? binding)
    {
        if (string.IsNullOrWhiteSpace(binding))
            return false;

        var trimmed = binding!.Trim();
        if (string.Equals(trimmed, "None", StringComparison.OrdinalIgnoreCase))
            return true;

        foreach (var token in SplitTokens(trimmed))
        {
            if (!IsTokenHeld(token))
                return false;
        }

        return true;
    }

    /// <summary>Human-readable label for tooltips (e.g. <c>Shift+Ctrl</c> → <c>Shift + Ctrl</c>).</summary>
    public static string FormatForDisplay(string? binding)
    {
        if (string.IsNullOrWhiteSpace(binding))
            return T(LKeys.MenuKeyFallback);

        var trimmed = binding!.Trim();
        if (string.Equals(trimmed, "None", StringComparison.OrdinalIgnoreCase))
            return "";

        var tokens = SplitTokens(trimmed).ToList();
        if (tokens.Count == 0)
            return trimmed;

        return string.Join(" + ", tokens.Select(FormatToken));
    }

    private static IEnumerable<string> SplitTokens(string binding) =>
        binding.Split(new[] { '+', ',', '&', ';' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(t => t.Trim())
            .Where(t => t.Length > 0);

    private static bool IsTokenHeld(string token)
    {
        switch (token.ToLowerInvariant())
        {
            case "shift":
                return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            case "ctrl":
            case "control":
                return Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            case "alt":
                return Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
            default:
                if (Enum.TryParse(token, true, out KeyCode key))
                    return Input.GetKey(key);
                return false;
        }
    }

    private static string FormatToken(string token)
    {
        switch (token.ToLowerInvariant())
        {
            case "shift": return "Shift";
            case "ctrl":
            case "control": return "Ctrl";
            case "alt": return "Alt";
            default:
                if (Enum.TryParse(token, true, out KeyCode key))
                    return key.ToString();
                return token;
        }
    }
}
