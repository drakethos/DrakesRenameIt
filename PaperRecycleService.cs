using DrakeRenameit.API;
using DrakeRenameit.ModText;
using DrakeRenameit.Permissions;
using static DrakeRenameit.ModText.RenameItLocalization;

namespace DrakeRenameit;

/// <summary>
/// Free recycle: Written Page (editable template) → fresh blank Piece of Paper.
/// Printed / immutable copies cannot be recycled.
/// Requires owner, Shared rewrite, or admin/VIP — not a random holder.
/// </summary>
internal static class PaperRecycleService
{
    /// <summary>
    /// True when the Paper tab may offer Recycle: config on, inventory Written template,
    /// and local player is owner / Shared / elevated.
    /// </summary>
    internal static bool CanRecycle(ItemDrop.ItemData? item, Player? player)
    {
        if (!RenameitConfig.PaperRecycleEnabled)
            return false;
        if (player == null || item == null)
            return false;
        if (PaperWallSession.IsActive)
            return false;
        if (!PaperItem.IsWrittenPaper(item) || PaperItem.IsPrintedPaper(item) || PaperCopyMark.IsCopy(item))
            return false;
        if (!DrakeRenameit.IsItemInLocalPlayerInventory(item))
            return false;
        if (!MayRecycle(item, player))
            return false;
        return ObjectDB.instance?.GetItemPrefab(PaperItem.PrefabName) != null;
    }

    /// <summary>
    /// Owner, Shared (public rewrite), or admin/VIP. Always required — ignores LockToOwner off.
    /// </summary>
    internal static bool MayRecycle(ItemDrop.ItemData? item, Player? player)
    {
        if (item == null || player == null)
            return false;
        if (RenameitPermission.IsElevatedForOverrides(player))
            return true;
        if (IsLocalOwner(item, player))
            return true;
        if (RenameitConfig.PublicRewriteEnabled && RenamePermissionManager.HasPublicRewriteFlag(item))
            return true;
        return false;
    }

    static bool IsLocalOwner(ItemDrop.ItemData item, Player local)
    {
        if (item.m_crafterID != 0L)
            return item.m_crafterID == local.GetPlayerID();
        if (!string.IsNullOrEmpty(item.m_crafterName))
            return item.m_crafterName.Equals(local.GetPlayerName(), System.StringComparison.OrdinalIgnoreCase);
        return false;
    }

    /// <summary>
    /// Remove the Written page and add one clean blank (no cost). Wipes name/desc/tags/style by replacement.
    /// </summary>
    internal static bool TryRecycleToBlank(ItemDrop.ItemData? written, Player? player, out string errorMessage)
    {
        errorMessage = "";
        if (player == null)
        {
            errorMessage = T(LKeys.UnlockErrNoPlayer);
            return false;
        }

        if (!RenameitConfig.PaperRecycleEnabled)
        {
            errorMessage = T(LKeys.RecycleErrDisabled);
            return false;
        }

        if (written == null || !PaperItem.IsWrittenPaper(written) ||
            PaperItem.IsPrintedPaper(written) || PaperCopyMark.IsCopy(written))
        {
            errorMessage = T(LKeys.RecycleErrNotWritten);
            return false;
        }

        if (!MayRecycle(written, player))
        {
            errorMessage = T(LKeys.RecycleErrNoPermission);
            return false;
        }

        if (PaperWallSession.IsActive)
        {
            errorMessage = T(LKeys.RecycleErrWall);
            return false;
        }

        var inv = player.GetInventory();
        if (inv == null)
        {
            errorMessage = T(LKeys.UnlockErrNoInventory);
            return false;
        }

        if (!inv.ContainsItem(written))
        {
            errorMessage = T(LKeys.MsgItemNotInInventory);
            return false;
        }

        var blankPrefab = ObjectDB.instance?.GetItemPrefab(PaperItem.PrefabName);
        var blankDrop = blankPrefab?.GetComponent<ItemDrop>();
        if (blankDrop?.m_itemData == null)
        {
            errorMessage = T(LKeys.RecycleErrNoBlank);
            return false;
        }

        var blank = blankDrop.m_itemData.Clone();
        blank.m_stack = 1;
        blank.m_dropPrefab = blankPrefab;
        blank.m_customData = new System.Collections.Generic.Dictionary<string, string>();
        blank.m_crafterID = 0L;
        blank.m_crafterName = "";
        PaperItem.ClearBlankPlaceTool(blank);

        inv.RemoveItem(written);

        if (!inv.AddItem(blank))
        {
            if (!inv.AddItem(written))
                RenameitConfig.Log?.LogError("[Paper] Recycle rollback failed: could not restore Written Page.");
            errorMessage = T(LKeys.RecycleErrInventoryFull);
            return false;
        }

        ValheimHudMessage.Show(player, MessageHud.MessageType.Center, T(LKeys.RecycleDone));
        RenameitConfig.VerboseInfo("[Paper] Recycled Written Page → blank.");
        return true;
    }
}
