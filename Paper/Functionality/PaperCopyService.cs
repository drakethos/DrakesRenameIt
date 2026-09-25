using System.Collections.Generic;
using DrakeModsLibs.API;
using DrakeRenameit.ModText;
using DrakeRenameit.Paper.Items;
using UnityEngine;
using static DrakeRenameit.ModText.RenameItLocalization;

namespace DrakeRenameit.Paper.Functionality;

/// <summary>
/// Paper-tab Copy: writable Written clones (1–9) or immutable Printed stack (1–50).
/// </summary>
internal static class PaperCopyService
{
    public const int CopiesMin = 1;
    public const int CopiesMax = 9;
    public const int PrintMin = 1;
    public const int PrintMax = 50;

    /// <summary>Cost lines: N × blank paper shared name.</summary>
    internal static List<(string SharedName, int Amount)> BuildBlankCostLines(int copies)
    {
        var list = new List<(string SharedName, int Amount)>();
        copies = Mathf.Max(1, copies);
        var shared = ResolveBlankSharedName();
        if (string.IsNullOrEmpty(shared))
            return list;
        list.Add((shared, copies));
        return list;
    }

    internal static string ResolveBlankSharedName()
    {
        if (ObjectDB.instance == null)
            return "";
        var go = ObjectDB.instance.GetItemPrefab(PaperItem.PrefabName);
        var sn = go?.GetComponent<ItemDrop>()?.m_itemData?.m_shared?.m_name ?? "";
        return sn;
    }

    internal static string LocalizedBlankName()
    {
        var sn = ResolveBlankSharedName();
        if (string.IsNullOrEmpty(sn))
            return "Piece of Paper";
        return Localization.instance != null ? Localization.instance.Localize(sn) : sn;
    }

    internal static bool CanAfford(Player? player, int copies) =>
        InventoryCost.CanAfford(player, BuildBlankCostLines(copies));

    /// <summary>Cost display for the Paper tab (one blank line).</summary>
    internal static string FormatCostLine(Player? player, int copies)
    {
        copies = Mathf.Max(1, copies);
        var shared = ResolveBlankSharedName();
        var have = InventoryCost.CountHave(player, shared);
        var noCost = InventoryCost.IsNoCostCheat(player);
        return InventoryCost.FormatCostLine(copies, LocalizedBlankName(), have, noCost);
    }

    /// <summary>
    /// Consume N blanks (unless nocost) and add N Written clones of <paramref name="source"/>.
    /// </summary>
    internal static bool TryCopyPages(ItemDrop.ItemData? source, int copies, Player? player, out string errorMessage)
    {
        errorMessage = "";
        if (player == null)
        {
            errorMessage = T(LKeys.UnlockErrNoPlayer);
            return false;
        }

        if (source == null || !PaperItem.IsWrittenPaper(source) || PaperItem.IsPrintedPaper(source))
        {
            errorMessage = T(LKeys.CopyPageErrNotWritten);
            return false;
        }

        copies = Mathf.Clamp(copies, CopiesMin, CopiesMax);
        var lines = BuildBlankCostLines(copies);
        if (lines.Count == 0)
        {
            errorMessage = T(LKeys.CopyPageErrNoBlank);
            return false;
        }

        var inv = player.GetInventory();
        if (inv == null)
        {
            errorMessage = T(LKeys.UnlockErrNoInventory);
            return false;
        }

        if (!InventoryCost.CanAfford(player, lines))
        {
            errorMessage = T(LKeys.CopyPageErrNotEnough);
            return false;
        }

        var writtenPrefab = ObjectDB.instance?.GetItemPrefab(PaperItem.WrittenPrefabName);
        var drop = writtenPrefab?.GetComponent<ItemDrop>();
        if (drop?.m_itemData == null)
        {
            errorMessage = T(LKeys.CopyPageErrNoPrefab);
            return false;
        }

        if (CountEmptySlots(inv) < copies)
        {
            errorMessage = T(LKeys.CopyPageErrInventoryFull);
            return false;
        }

        if (!InventoryCost.TryConsume(player, lines, out errorMessage, T(LKeys.CopyPageErrNotEnough)))
            return false;

        var added = new List<ItemDrop.ItemData>();
        for (var i = 0; i < copies; i++)
        {
            var clone = CloneWritten(source, drop.m_itemData, writtenPrefab!);
            if (clone == null || !inv.AddItem(clone))
            {
                foreach (var a in added)
                    inv.RemoveItem(a);
                if (!InventoryCost.IsNoCostCheat(player))
                    RefundBlanks(inv, copies);
                errorMessage = T(LKeys.CopyPageErrInventoryFull);
                return false;
            }

            added.Add(clone);
        }

        ValheimHudMessage.Show(player, MessageHud.MessageType.Center, T(LKeys.CopyPageDone, copies));
        return true;
    }

    /// <summary>
    /// Blank-for-blank print: N blanks → one Printed stack of N (or merge onto matching stack).
    /// Source may be Written template or an existing Printed copy.
    /// </summary>
    internal static bool TryPrintStack(ItemDrop.ItemData? source, int count, Player? player, out string errorMessage)
    {
        errorMessage = "";
        if (player == null)
        {
            errorMessage = T(LKeys.UnlockErrNoPlayer);
            return false;
        }

        if (source == null || !PaperItem.IsWrittenLike(source))
        {
            errorMessage = T(LKeys.CopyPageErrNotWritten);
            return false;
        }

        count = Mathf.Clamp(count, PrintMin, Mathf.Min(PrintMax, RenameitConfig.PrintedPaperStackSize));
        var lines = BuildBlankCostLines(count);
        if (lines.Count == 0)
        {
            errorMessage = T(LKeys.CopyPageErrNoBlank);
            return false;
        }

        var inv = player.GetInventory();
        if (inv == null)
        {
            errorMessage = T(LKeys.UnlockErrNoInventory);
            return false;
        }

        if (!InventoryCost.CanAfford(player, lines))
        {
            errorMessage = T(LKeys.CopyPageErrNotEnough);
            return false;
        }

        var printedPrefab = ObjectDB.instance?.GetItemPrefab(PaperItem.PrintedPrefabName);
        var drop = printedPrefab?.GetComponent<ItemDrop>();
        if (drop?.m_itemData == null)
        {
            errorMessage = T(LKeys.CopyPageErrNoPrefab);
            return false;
        }

        if (!InventoryCost.TryConsume(player, lines, out errorMessage, T(LKeys.CopyPageErrNotEnough)))
            return false;

        var print = ClonePrinted(source, drop.m_itemData, printedPrefab!, count);
        if (print == null || !inv.AddItem(print))
        {
            if (!InventoryCost.IsNoCostCheat(player))
                RefundBlanks(inv, count);
            errorMessage = T(LKeys.CopyPageErrInventoryFull);
            return false;
        }

        ValheimHudMessage.Show(player, MessageHud.MessageType.Center, T(LKeys.CopyPageDone, count));
        return true;
    }

    static ItemDrop.ItemData? CloneWritten(
        ItemDrop.ItemData source,
        ItemDrop.ItemData template,
        GameObject writtenPrefab)
    {
        var written = template.Clone();
        written.m_stack = 1;
        written.m_dropPrefab = writtenPrefab;
        written.m_customData = new Dictionary<string, string>();
        CopyCustomData(source, written);
        written.m_crafterID = source.m_crafterID;
        written.m_crafterName = source.m_crafterName ?? "";

        PaperItemStyle.Read(source, out var font, out var land, out var takePub);
        PaperItemStyle.WriteAll(written, font, land, takePub);
        ReapplyRenameDesc(source, written);
        return written;
    }

    static ItemDrop.ItemData? ClonePrinted(
        ItemDrop.ItemData source,
        ItemDrop.ItemData template,
        GameObject printedPrefab,
        int stack)
    {
        var printed = template.Clone();
        printed.m_stack = Mathf.Clamp(stack, 1, RenameitConfig.PrintedPaperStackSize);
        printed.m_dropPrefab = printedPrefab;
        printed.m_customData = new Dictionary<string, string>();
        CopyCustomData(source, printed);
        printed.m_crafterID = source.m_crafterID;
        printed.m_crafterName = source.m_crafterName ?? "";

        PaperItemStyle.Read(source, out var font, out var land, out var takePub);
        PaperItemStyle.WriteAll(printed, font, land, takePub);
        ReapplyRenameDesc(source, printed);
        PaperCopyMark.Stamp(printed);
        return printed;
    }

    static void CopyCustomData(ItemDrop.ItemData source, ItemDrop.ItemData dest)
    {
        if (source.m_customData == null)
            return;
        foreach (var kv in source.m_customData)
            dest.m_customData![kv.Key] = kv.Value;
    }

    static void ReapplyRenameDesc(ItemDrop.ItemData source, ItemDrop.ItemData dest)
    {
        if (CustomizeLibsAPI.HasCustomName(source))
        {
            var n = CustomizeLibsAPI.GetProperName(source);
            if (!string.IsNullOrEmpty(n))
                CustomizeLibsAPI.SetCustomName(dest, n);
        }

        if (CustomizeLibsAPI.HasCustomDescription(source))
        {
            var d = CustomizeLibsAPI.GetProperDescription(source);
            if (!string.IsNullOrEmpty(d))
                CustomizeLibsAPI.SetCustomDescription(dest, d);
        }
    }

    static int CountEmptySlots(Inventory inv)
    {
        try
        {
            var w = inv.GetWidth();
            var h = inv.GetHeight();
            var n = 0;
            for (var y = 0; y < h; y++)
            {
                for (var x = 0; x < w; x++)
                {
                    if (inv.GetItemAt(x, y) == null)
                        n++;
                }
            }

            return n;
        }
        catch
        {
            return inv.HaveEmptySlot() ? 1 : 0;
        }
    }

    static void RefundBlanks(Inventory inv, int count)
    {
        if (count <= 0 || ObjectDB.instance == null)
            return;
        var blankPrefab = ObjectDB.instance.GetItemPrefab(PaperItem.PrefabName);
        var blankDrop = blankPrefab?.GetComponent<ItemDrop>();
        if (blankDrop?.m_itemData == null)
            return;

        for (var i = 0; i < count; i++)
        {
            var restored = blankDrop.m_itemData.Clone();
            restored.m_stack = 1;
            restored.m_dropPrefab = blankPrefab;
            inv.AddItem(restored);
        }
    }
}
