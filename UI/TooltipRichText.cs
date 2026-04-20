using System;
using System.Text;
using System.Text.RegularExpressions;

namespace DrakeRenameit.UI;

/// <summary>Guards injected tooltip strings so unclosed rich-text color tags do not bleed into the rest of the tooltip.</summary>
internal static class TooltipRichText
{
    /// <summary>
    /// Appends <c>&lt;/color&gt;</c> for each unmatched color open (Valheim / TMP-style <c>&lt;color=…&gt;</c> and common <c>&lt;#RRGGBB&gt;</c> shorthand).
    /// </summary>
    internal static string EnsureColorTagsClosedForTooltip(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return text ?? "";

        int depth = 0;
        for (int i = 0; i < text.Length;)
        {
            if (text[i] != '<')
            {
                i++;
                continue;
            }

            int end = text.IndexOf('>', i);
            if (end < 0)
                break;

            string tag = text.Substring(i, end - i + 1);
            if (tag.Length >= 2 && tag[1] == '/')
            {
                if (tag.StartsWith("</color", StringComparison.OrdinalIgnoreCase))
                    depth = Math.Max(0, depth - 1);
            }
            else if (tag.StartsWith("<color", StringComparison.OrdinalIgnoreCase))
            {
                depth++;
            }
            else if (IsHashColorOpenTag(tag))
            {
                depth++;
            }

            i = end + 1;
        }

        if (depth == 0)
            return text;

        var sb = new StringBuilder(text, text.Length + depth * 8);
        for (int d = 0; d < depth; d++)
            sb.Append("</color>");
        return sb.ToString();
    }

    private static bool IsHashColorOpenTag(string tag)
    {
        // e.g. <#ff0> <#ffffff> (user / game shorthand)
        if (tag.Length < 5 || tag[0] != '<' || tag[1] != '#' || tag[tag.Length - 1] != '>')
            return false;
        string hex = tag.Substring(2, tag.Length - 3);
        return HexColorRegex.IsMatch(hex);
    }

    private static readonly Regex HexColorRegex = new Regex("^[0-9a-fA-F]{3,8}$", RegexOptions.Compiled);
}
