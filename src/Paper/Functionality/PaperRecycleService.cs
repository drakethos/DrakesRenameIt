using DrakeRenameit.API;
using DrakeRenameit.ModText;
using DrakeRenameit.Paper.Items;
using DrakeRenameit.Paper.Pieces;
using DrakeRenameit.Permissions;
using static DrakeRenameit.ModText.RenameItLocalization;

namespace DrakeRenameit.Paper.Functionality;

/// <summary>
/// Free recycle: Written Page (editable template) → blank Piece of Paper.
/// Printed / immutable copies only when <see cref="RenameitConfig.PaperRecycleImmutableEnabled"/>.
/// Requires owner, Shared rewrite, or admin/VIP — not a random holder.
/// </summary>
internal static class PaperRecycleService
{
    static bool IsImmutableCopy(ItemDrop.ItemData? item) =>
        PaperItem.IsPrintedPaper(item) || PaperCopyMark.IsCopy(item);

    /// <summary>
    /// True when the Paper tab may offer Recycle: config on, inventory Written template
    /// (or Printed when immutable recycle is enabled), and local player is owner / Shared / elevated.
    /// </summary>
    internal static bool CanRecycle(ItemDrop.ItemData? item, Player? player)
    {
        if (!RenameitConfig.PaperRecycleEnabled)
            return false;
        if (player == null || item == null)
            return false;
        if (PaperWallSession.IsActive)
            return false;
        if (!PaperItem.IsWrittenLike(item))
            return false;
        if (IsImmutableCopy(item) && !RenameitConfig.PaperRecycleImmutableEnabled)
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
    /// Remove the page stack and add the same count of clean blanks (no cost).
    /// Wipes name/desc/tags/style by replacement.
    /// </summary>
    internal static bool TryRecycleToBlank(ItemDrop.ItemData? page, Player? player, out string errorMessage)
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

        if (page == null || !PaperItem.IsWrittenLike(page))
        {
            errorMessage = T(LKeys.RecycleErrNotWritten);
            return false;
        }

        if (IsImmutableCopy(page) && !RenameitConfig.PaperRecycleImmutableEnabled)
        {
            errorMessage = T(LKeys.RecycleErrNotWritten);
            return false;
        }

        if (!MayRecycle(page, player))
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

        if (!inv.ContainsItem(page))
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

        var count = System.Math.Max(1, page.m_stack);
        var blanks = new System.Collections.Generic.List<ItemDrop.ItemData>(count);
        for (var i = 0; i < count; i++)
        {
            var blank = blankDrop.m_itemData.Clone();
            blank.m_stack = 1;
            blank.m_dropPrefab = blankPrefab;
            blank.m_customData = new System.Collections.Generic.Dictionary<string, string>();
            blank.m_crafterID = 0L;
            blank.m_crafterName = "";
            PaperItem.ClearBlankPlaceTool(blank);
            blanks.Add(blank);
        }

        // Remove first so freed slots can hold blanks (important for full bags recycling 1→1).
        inv.RemoveItem(page);

        var added = new System.Collections.Generic.List<ItemDrop.ItemData>();
        foreach (var blank in blanks)
        {
            if (!inv.AddItem(blank))
            {
                foreach (var a in added)
                    inv.RemoveItem(a);
                if (!inv.AddItem(page))
                    RenameitConfig.Log?.LogError("[Paper] Recycle rollback failed: could not restore page stack.");
                errorMessage = T(LKeys.RecycleErrInventoryFull);
                return false;
            }

            added.Add(blank);
        }

        ValheimHudMessage.Show(player, MessageHud.MessageType.Center, T(LKeys.RecycleDone));
        RenameitConfig.VerboseInfo($"[Paper] Recycled {count} page(s) → blank.");
        return true;
    }
}
