using System;
using System.Text;

namespace DrakeRenameit;

/// <summary>Compares Drake-specific custom data so stacks with different rename/desc/crafted display do not merge when <see cref="RenameitConfig.SeparateStacks"/> is on.</summary>
internal static class StackIdentity
{
    internal static string GetFingerprint(ItemDrop.ItemData? item)
    {
        if (item?.m_customData == null)
            return "";

        var sb = new StringBuilder();
        Append(sb, item, DrakeRenameit.DrakeNewName);
        Append(sb, item, DrakeRenameit.DrakeNewDesc);
        Append(sb, item, DrakeRenameit.DrakeCraftedByDisplay);
        Append(sb, item, DrakeRenameit.DrakeCraftedByLineLabel);
        return sb.ToString();
    }

    private static void Append(StringBuilder sb, ItemDrop.ItemData item, string key)
    {
        if (item.m_customData!.TryGetValue(key, out var v) && !string.IsNullOrEmpty(v))
            sb.Append('|').Append(v);
        else
            sb.Append('|');
    }

    internal static bool SameDrakeStackIdentity(ItemDrop.ItemData? a, ItemDrop.ItemData? b)
    {
        if (a == null || b == null)
            return false;
        return string.Equals(GetFingerprint(a), GetFingerprint(b), StringComparison.Ordinal);
    }
}
