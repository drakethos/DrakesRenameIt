using DrakeModsLibs.API;
using DrakeModsLibs.UI;
using DrakeRenameit.API;
using DrakeRenameit.Integration;
using DrakeRenameit.Paper.Functionality;
using DrakeRenameit.UI;
using UnityEngine;

namespace DrakeRenameit.Paper.Pieces;

/// <summary>
/// Short-lived wall-edit session: synthetic <see cref="ItemDrop.ItemData"/> from a pinned
/// Written Page ZDO so DrakeTabHost / Rename UI can run without picking the page up.
/// </summary>
internal static class PaperWallSession
{
    static PaperWrittenVessel? _vessel;
    static ItemDrop.ItemData? _item;

    internal static bool IsActive =>
        _vessel != null && _item != null && _vessel;

    internal static PaperWrittenVessel? Vessel => IsActive ? _vessel : null;
    internal static ItemDrop.ItemData? Item => IsActive ? _item : null;

    internal static bool Matches(ItemDrop.ItemData? item) =>
        IsActive && item != null && ReferenceEquals(item, _item);

    internal static void Begin(PaperWrittenVessel vessel, ItemDrop.ItemData item)
    {
        _vessel = vessel;
        _item = item;
        DrakeRenameit.CurrentItem = item;
    }

    internal static void End()
    {
        var sessionItem = _item;
        _vessel = null;
        _item = null;
        PaperTabPanel.Hide();
        if (DrakeRenameit.CurrentItem != null &&
            (ReferenceEquals(DrakeRenameit.CurrentItem, sessionItem) || sessionItem == null))
            DrakeRenameit.CurrentItem = null;
        // Wall Shift+E always blocks via Paper or Rename chrome — clear on session end.
        DrakeGuiInput.EnsureUnblocked();
    }

    /// <summary>Open Rename|Paper for a pinned note. Returns false if host/UI cannot open.</summary>
    internal static bool TryOpen(PaperWrittenVessel vessel)
    {
        if (vessel == null)
            return false;

        var item = vessel.BuildItemFromZdoPublic();
        if (item == null)
            return false;

        Begin(vessel, item);

        try
        {
            // Copies claim Paper; prefer Paper when Rename is hidden (non-override).
            var preferred = PaperCopyMark.IsCopy(item)
                ? RenameItLibsBridge.PaperTabId
                : RenameItLibsBridge.TabId;
            if (DrakeTabHost.OpenForItem(item, preferred, onClosed: End))
                return true;

            // Retry with Paper if Rename preferred failed (libs mismatch / empty strip).
            if (preferred != RenameItLibsBridge.PaperTabId &&
                DrakeTabHost.OpenForItem(item, RenameItLibsBridge.PaperTabId, onClosed: End))
                return true;

            // Tab host empty — Rename action menu for templates / elevated only.
            if (!PaperCopyMark.IsCopy(item) ||
                RenameitPermission.IsElevatedForOverrides(Player.m_localPlayer))
            {
                UIPanels.OpenActionMenu(item);
                return true;
            }

            End();
            return false;
        }
        catch (System.Exception ex)
        {
            End();
            Debug.LogWarning($"[DrakesRenameit] Wall paper menu failed: {ex.GetType().Name}: {ex.Message}");
            return false;
        }
    }

    /// <summary>After inventory-style rename/desc apply, push custom data back onto the piece ZDO.</summary>
    internal static void FlushCurrentItemToVessel()
    {
        if (!IsActive || _item == null || _vessel == null)
            return;
        _vessel.ApplyCustomizationFromItem(_item);
    }
}
