using System;
using System.Reflection;
using System.Text.RegularExpressions;
using BepInEx.Logging;
using DrakeRenameit.UI;
using HarmonyLib;

namespace DrakeRenameit.Patches;

/// <summary>
/// Replaces visible crafted-by text when <see cref="DrakeRenameit.DrakeCraftedByDisplay"/> and/or
/// <see cref="DrakeRenameit.DrakeCraftedByLineLabel"/> is set.
/// </summary>
/// <remarks>
/// Vanilla builds the line as <c>\n$item_crafter: {m_crafterName}</c> then localizes; the UI often wraps the name in
/// <c>&lt;color&gt;</c> tags, so a plain <see cref="string.Replace(string, string)"/> on <see cref="ItemDrop.ItemData.m_crafterName"/> misses.
/// </remarks>
internal static class ItemTooltipPatches
{
    internal static void Apply(Harmony harmony, ManualLogSource log)
    {
        var t = typeof(ItemDrop.ItemData);
        var sig = new[] { typeof(ItemDrop.ItemData), typeof(int), typeof(bool) };

        var target = AccessTools.DeclaredMethod(t, "GetTooltip", sig)
                     ?? AccessTools.Method(t, "GetTooltip", sig);

        if (target == null)
        {
            foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
            {
                if (m.Name != "GetTooltip" || !m.IsStatic)
                    continue;
                var p = m.GetParameters();
                if (p.Length == 3 &&
                    p[0].ParameterType == typeof(ItemDrop.ItemData) &&
                    p[1].ParameterType == typeof(int) &&
                    p[2].ParameterType == typeof(bool))
                {
                    target = m;
                    break;
                }
            }
        }

        if (target == null)
        {
            log.LogWarning(
                "[DrakesRenameIt] Crafted-by display: static GetTooltip(ItemData,int,bool) not found — tooltip override disabled.");
            return;
        }

        harmony.Patch(
            target,
            postfix: new HarmonyMethod(typeof(ItemTooltipPatches), nameof(CraftedByDisplayPostfix)));
    }

    internal static void CraftedByDisplayPostfix(
        ItemDrop.ItemData item,
        int qualityLevel,
        bool crafting,
        ref string __result)
    {
        __result = ApplyCraftedByDisplayToTooltipText(__result, item);
    }

    /// <summary>Inventory / shared: rewrite the crafted-by segment like description replacement (handles color tags + localization).</summary>
    internal static string ApplyCraftedByDisplayToTooltipText(string text, ItemDrop.ItemData item)
    {
        if (string.IsNullOrEmpty(text) || item?.m_customData == null)
            return text;
        if (item.m_crafterID == 0L)
            return text;
        var oldName = item.m_crafterName ?? "";
        if (string.IsNullOrEmpty(oldName))
            return text;

        bool hasDisp = item.m_customData.TryGetValue(DrakeRenameit.DrakeCraftedByDisplay, out var dispRaw) &&
                       !string.IsNullOrEmpty(dispRaw);
        bool hasLine = item.m_customData.TryGetValue(DrakeRenameit.DrakeCraftedByLineLabel, out var lineRaw) &&
                       !string.IsNullOrEmpty(lineRaw);
        if (!hasDisp && !hasLine)
            return text;

        string displayPart = hasDisp ? dispRaw! : oldName;
        if (hasDisp)
        {
            displayPart = TooltipRichText.EnsureRichTextTagsClosedForTooltip(displayPart);
            displayPart = TooltipRichText.WrapCraftedByDisplayWithDefaultStatColorIfNeeded(displayPart);
        }

        string? lineOverride = hasLine ? lineRaw : null;
        return ReplaceCraftedBySegment(text, oldName, displayPart, lineOverride);
    }

    /// <summary>Match vanilla <c>\n$item_crafter: name</c>, localized "Crafted by: …", and common &lt;color&gt; wrapping around the name.</summary>
    internal static string ReplaceCraftedBySegment(string text, string oldName, string newDisplay, string? lineLabelOverride = null)
    {
        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(oldName))
            return text;

        if (!string.IsNullOrEmpty(lineLabelOverride))
        {
            var custom = TryReplaceCraftedByWithCustomLineLabel(text, oldName, newDisplay, lineLabelOverride);
            if (custom != null)
                return custom;
        }

        // Exact fragment from ItemData.GetTooltip StringBuilder (often before full localization pass)
        var tokenSpaced = "\n$item_crafter: " + oldName + " ";
        if (text.Contains(tokenSpaced))
            return text.Replace(tokenSpaced, "\n$item_crafter: " + newDisplay + " ");
        var tokenTight = "\n$item_crafter: " + oldName;
        if (text.Contains(tokenTight))
            return text.Replace(tokenTight, "\n$item_crafter: " + newDisplay);

        string label = "Crafted by";
        if (Localization.instance != null)
        {
            var loc = Localization.instance.Localize("$item_crafter");
            if (!string.IsNullOrEmpty(loc))
                label = loc;
        }

        // Plain localized line (no color tags)
        foreach (var suffix in new[] { " ", "" })
        {
            var needle = "\n" + label + ": " + oldName + suffix;
            if (text.Contains(needle))
                return text.Replace(needle, "\n" + label + ": " + newDisplay + (suffix == " " ? " " : ""));
        }

        // Name wrapped in <color=…>…</color> after "Label:"
        try
        {
            string escLabel = Regex.Escape(label);
            string escOld = Regex.Escape(oldName);
            var rxColor = new Regex(
                @"(?<prefix>\n[^\n]*?" + escLabel + @"\s*:\s*)(?:<color[^>]*>)?" + escOld + @"(?:</color>)?",
                RegexOptions.None,
                TimeSpan.FromMilliseconds(250));
            if (rxColor.IsMatch(text))
                return rxColor.Replace(text, m => m.Groups["prefix"].Value + newDisplay);
        }
        catch (RegexMatchTimeoutException)
        {
            /* ignore */
        }

        // Same line, $item_crafter key still present but localized label unknown
        try
        {
            var rxTok = new Regex(
                @"(?<prefix>\n[^\n]*?\$item_crafter\s*:\s*)(?:<color[^>]*>)?" + Regex.Escape(oldName) + @"(?:</color>)?",
                RegexOptions.None,
                TimeSpan.FromMilliseconds(250));
            if (rxTok.IsMatch(text))
                return rxTok.Replace(text, m => m.Groups["prefix"].Value + newDisplay);
        }
        catch (RegexMatchTimeoutException)
        {
            /* ignore */
        }

        // First line that looks like crafted-by: replace only that occurrence of oldName
        var lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            if (line.IndexOf("$item_crafter", StringComparison.OrdinalIgnoreCase) < 0 &&
                line.IndexOf(label, StringComparison.OrdinalIgnoreCase) < 0)
                continue;
            if (line.IndexOf(oldName, StringComparison.Ordinal) < 0)
                continue;
            int idx = line.IndexOf(oldName, StringComparison.Ordinal);
            lines[i] = line.Remove(idx, oldName.Length).Insert(idx, newDisplay);
            return string.Join("\n", lines);
        }

        return text;
    }

    static string? TryReplaceCraftedByWithCustomLineLabel(string text, string oldName, string newDisplay, string lineLabelOverride)
    {
        var tokenSpaced = "\n$item_crafter: " + oldName + " ";
        if (text.Contains(tokenSpaced))
            return text.Replace(tokenSpaced, "\n" + lineLabelOverride + ": " + newDisplay + " ");
        var tokenTight = "\n$item_crafter: " + oldName;
        if (text.Contains(tokenTight))
            return text.Replace(tokenTight, "\n" + lineLabelOverride + ": " + newDisplay);

        string label = "Crafted by";
        if (Localization.instance != null)
        {
            var loc = Localization.instance.Localize("$item_crafter");
            if (!string.IsNullOrEmpty(loc))
                label = loc;
        }

        foreach (var suffix in new[] { " ", "" })
        {
            var needle = "\n" + label + ": " + oldName + suffix;
            if (text.Contains(needle))
                return text.Replace(needle, "\n" + lineLabelOverride + ": " + newDisplay + (suffix == " " ? " " : ""));
        }

        try
        {
            string escLabel = Regex.Escape(label);
            string escOld = Regex.Escape(oldName);
            var rxColor = new Regex(
                @"(?<whole>\n[^\n]*?" + escLabel + @"\s*:\s*)(?:<color[^>]*>)?" + escOld + @"(?:</color>)?",
                RegexOptions.None,
                TimeSpan.FromMilliseconds(250));
            if (rxColor.IsMatch(text))
                return rxColor.Replace(text, _ => "\n" + lineLabelOverride + ": " + newDisplay);
        }
        catch (RegexMatchTimeoutException)
        {
            /* ignore */
        }

        try
        {
            var rxTok = new Regex(
                @"(?<whole>\n[^\n]*?\$item_crafter\s*:\s*)(?:<color[^>]*>)?" + Regex.Escape(oldName) + @"(?:</color>)?",
                RegexOptions.None,
                TimeSpan.FromMilliseconds(250));
            if (rxTok.IsMatch(text))
                return rxTok.Replace(text, _ => "\n" + lineLabelOverride + ": " + newDisplay);
        }
        catch (RegexMatchTimeoutException)
        {
            /* ignore */
        }

        var lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            if (line.IndexOf("$item_crafter", StringComparison.OrdinalIgnoreCase) < 0 &&
                line.IndexOf(label, StringComparison.OrdinalIgnoreCase) < 0)
                continue;
            if (line.IndexOf(oldName, StringComparison.Ordinal) < 0)
                continue;
            lines[i] = lineLabelOverride + ": " + newDisplay;
            return string.Join("\n", lines);
        }

        return null;
    }
}
