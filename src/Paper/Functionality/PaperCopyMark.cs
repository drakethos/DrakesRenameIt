using DrakeModsLibs.API;
using DrakeModsLibs.Tags;
using DrakeRenameit.Paper.Items;

namespace DrakeRenameit.Paper.Functionality;

/// <summary>
/// Soft stamp on Printed Page copies: non-owners cannot rewrite name/desc/crafted-by
/// (TagBypass / admin override still can). Does not suppress inventory UI — tabs own that.
/// <see cref="TooltipLabel"/> is shown on the inventory (brown) tooltip <b>name/title</b> only —
/// never baked into description, Drake_NewName, or world/stand hover.
/// </summary>
internal static class PaperCopyMark
{
    /// <summary>Soft tag consumed by Libs gatekeeper.</summary>
    public const string SoftTagKey = "Drake_PaperCopy";

    /// <summary>Stable customData flag (also stamped for fingerprint/tooltips).</summary>
    public const string IsCopyKey = "DrakePaper_IsCopy";

    /// <summary>Inventory tooltip title suffix only (not description / world hover).</summary>
    public const string TooltipLabel = "[Copy]";

    static bool _ruleRegistered;

    internal static void RegisterSoftEditRule()
    {
        if (_ruleRegistered)
            return;
        _ruleRegistered = true;
        CustomizeLibsAPI.RegisterTagBlockRule(
            SoftTagKey,
            CustomizeOperation.AllEdits,
            suppressRenameInventoryUi: false,
            hardLock: false);
    }

    internal static bool IsCopy(ItemDrop.ItemData? item)
    {
        if (item == null)
            return false;
        if (PaperItem.IsPrintedPaper(item))
            return true;
        if (DrakeTagManager.HasTag(item, SoftTagKey))
            return true;
        return item.m_customData != null &&
               item.m_customData.TryGetValue(IsCopyKey, out var v) &&
               (v == "1" || v.Equals("true", System.StringComparison.OrdinalIgnoreCase));
    }

    internal static void Stamp(ItemDrop.ItemData? item)
    {
        if (item == null)
            return;
        item.m_customData ??= new System.Collections.Generic.Dictionary<string, string>();
        item.m_customData[IsCopyKey] = "1";
        DrakeTagManager.SetTag(item, SoftTagKey);
        // Legacy prints may have [Copy] baked into description — scrub it.
        StripCopyFromDescription(item);
    }

    /// <summary>Remove legacy <c>[Copy]</c> lines from custom description (marker is tooltip-only now).</summary>
    internal static void StripCopyFromDescription(ItemDrop.ItemData? item)
    {
        if (item == null || !CustomizeLibsAPI.HasCustomDescription(item))
            return;

        var desc = CustomizeLibsAPI.GetProperDescription(item) ?? "";
        var stripped = StripCopyLine(desc);
        if (stripped == desc)
            return;

        if (string.IsNullOrWhiteSpace(stripped))
            CustomizeLibsAPI.SetCustomDescription(item, null);
        else
            CustomizeLibsAPI.SetCustomDescription(item, stripped);
    }

    /// <summary>Drop whole lines that are only the Copy marker (for hover / parchment / ZDO text).</summary>
    internal static string StripCopyLine(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return text ?? "";

        var lines = text!.Split('\n');
        var kept = new System.Collections.Generic.List<string>(lines.Length);
        foreach (var line in lines)
        {
            if (line.Trim().Equals(TooltipLabel, System.StringComparison.OrdinalIgnoreCase))
                continue;
            kept.Add(line);
        }

        return string.Join("\n", kept).TrimEnd();
    }
}
