using System;
using System.Collections.Generic;
using DrakeModsLibs.API;
using DrakeRenameit.ModText;
using static DrakeRenameit.ModText.RenameItLocalization;

namespace DrakeRenameit;

/// <summary>Blank → Written peel-one conversion on rename/desc apply.</summary>
internal static class PaperWriteConverter
{
    /// <summary>
    /// Consumes one blank from <paramref name="blank"/> and adds a Written Page with optional name/desc.
    /// Returns the new written stack, or null on failure (inventory full / missing prefab).
    /// </summary>
    internal static ItemDrop.ItemData? PeelBlankToWritten(
        ItemDrop.ItemData blank,
        string? customName,
        string? customDesc,
        Player player)
    {
        if (!PaperItem.IsBlankPaper(blank) || blank.m_shared == null)
            return null;

        var inv = player.GetInventory();
        if (inv == null)
            return null;

        if (!inv.ContainsItem(blank))
        {
            player.Message(MessageHud.MessageType.Center, T(LKeys.MsgItemNotInInventoryApply));
            return null;
        }

        // Need room for the written page unless blank stack becomes empty and frees the slot.
        bool blankWillVanish = blank.m_stack <= 1;
        if (!blankWillVanish && !inv.HaveEmptySlot())
        {
            // May still stack into empty? Written is stack 1 and unique custom data — need empty slot.
            player.Message(MessageHud.MessageType.Center, "Inventory full — cannot create a Written Page.");
            return null;
        }

        var writtenPrefab = ObjectDB.instance?.GetItemPrefab(PaperItem.WrittenPrefabName);
        if (writtenPrefab == null)
        {
            RenameitConfig.Log?.LogError("[Paper] Written prefab missing; cannot peel blank.");
            player.Message(MessageHud.MessageType.Center, "Written Page is not available.");
            return null;
        }

        var drop = writtenPrefab.GetComponent<ItemDrop>();
        if (drop?.m_itemData == null)
            return null;

        var written = drop.m_itemData.Clone();
        written.m_stack = 1;
        written.m_dropPrefab = writtenPrefab;
        written.m_customData = new Dictionary<string, string>();

        if (!string.IsNullOrWhiteSpace(customName))
            CustomizeLibsAPI.SetCustomName(written, customName.Trim());
        if (!string.IsNullOrWhiteSpace(customDesc))
            CustomizeLibsAPI.SetCustomDescription(written, customDesc.Trim());
        if (Permissions.RenamePermissionManager.HasPublicRewriteFlag(blank))
            Permissions.RenamePermissionManager.SetPublicRewriteFlag(written, true);

        // Writer owns the page.
        written.m_crafterID = player.GetPlayerID();
        written.m_crafterName = player.GetPlayerName();
        DrakeRenameit.SetRenameUnlocked(written);

        // Consume one blank first, then add written (so a vanishing blank frees its slot).
        if (blank.m_stack > 1)
            blank.m_stack -= 1;
        else
            inv.RemoveItem(blank);

        if (!inv.AddItem(written))
        {
            // Rollback: restore blank if add failed.
            if (blankWillVanish)
            {
                // blank was removed — try give blank back
                var blankPrefab = ObjectDB.instance?.GetItemPrefab(PaperItem.PrefabName);
                var blankDrop = blankPrefab?.GetComponent<ItemDrop>();
                if (blankDrop?.m_itemData != null)
                {
                    var restored = blankDrop.m_itemData.Clone();
                    restored.m_stack = 1;
                    restored.m_dropPrefab = blankPrefab;
                    inv.AddItem(restored);
                }
            }
            else
                blank.m_stack += 1;

            player.Message(MessageHud.MessageType.Center, "Inventory full — cannot create a Written Page.");
            return null;
        }

        RenameitConfig.VerboseInfo("[Paper] Peeled 1 blank → Written Page.");
        return written;
    }
}
