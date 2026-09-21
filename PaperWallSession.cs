using DrakeModsLibs.API;
using DrakeModsLibs.UI;
using DrakeRenameit.Integration;
using DrakeRenameit.UI;
using UnityEngine;

namespace DrakeRenameit;

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
            if (DrakeTabHost.OpenForItem(item, RenameItLibsBridge.TabId, onClosed: End))
                return true;

            // Tab host empty / libs mismatch — still open Rename action menu.
            UIPanels.OpenActionMenu(item);
            return true;
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
