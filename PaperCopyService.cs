using System.Collections.Generic;
using DrakeModsLibs.API;
using DrakeRenameit.ModText;
using UnityEngine;
using static DrakeRenameit.ModText.RenameItLocalization;

namespace DrakeRenameit;

/// <summary>
/// Paper-tab Copy page: N blank sheets → N Written Page clones (shared <see cref="InventoryCost"/>).
/// </summary>
internal static class PaperCopyService
{
    public const int CopiesMin = 1;
    public const int CopiesMax = 9;

    /// <summary>Cost lines: N × blank paper shared name.</summary>
    internal static List<(string SharedName, int Amount)> BuildBlankCostLines(int copies)
    {
        var list = new List<(string SharedName, int Amount)>();
        copies = Mathf.Clamp(copies, CopiesMin, CopiesMax);
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
        copies = Mathf.Clamp(copies, CopiesMin, CopiesMax);
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

        if (source == null || !PaperItem.IsWrittenPaper(source))
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

        // Need N empty slots (written pages do not stack with unique custom data).
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
                // Rollback clones + blanks (best effort).
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

    static ItemDrop.ItemData? CloneWritten(
        ItemDrop.ItemData source,
        ItemDrop.ItemData template,
        GameObject writtenPrefab)
    {
        var written = template.Clone();
        written.m_stack = 1;
        written.m_dropPrefab = writtenPrefab;
        written.m_customData = new Dictionary<string, string>();
        if (source.m_customData != null)
        {
            foreach (var kv in source.m_customData)
                written.m_customData[kv.Key] = kv.Value;
        }

        written.m_crafterID = source.m_crafterID;
        written.m_crafterName = source.m_crafterName ?? "";

        // Ensure style keys exist even if source predates persistence.
        PaperItemStyle.Read(source, out var font, out var land, out var takePub);
        PaperItemStyle.WriteAll(written, font, land, takePub);

        // Re-apply rename/desc via API so Libs caches stay consistent.
        if (CustomizeLibsAPI.HasCustomName(source))
        {
            var n = CustomizeLibsAPI.GetProperName(source);
            if (!string.IsNullOrEmpty(n))
                CustomizeLibsAPI.SetCustomName(written, n);
        }

        if (CustomizeLibsAPI.HasCustomDescription(source))
        {
            var d = CustomizeLibsAPI.GetProperDescription(source);
            if (!string.IsNullOrEmpty(d))
                CustomizeLibsAPI.SetCustomDescription(written, d);
        }

        return written;
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
