using DrakeModsLibs;
using DrakeModsLibs.API;
using DrakeModsLibs.Data;
using DrakeRenameit.ModText;
using DrakeRenameit.Paper.Functionality;
using DrakeRenameit.Paper.Items;
using DrakeRenameit.Paper.Pieces;
using static DrakeRenameit.ModText.RenameItLocalization;

namespace DrakeRenameit
{
    /// <summary>
    /// UI-free edit commits for the Item Workshop panel. The legacy popups apply edits and then reopen their own menu;
    /// the Workshop panel stays open, so it calls these instead. Rules (peel blank paper, wall-session flush, events)
    /// match the Apply* methods in <c>DrakeRenameit.cs</c>.
    /// </summary>
    public partial class DrakeRenameit
    {
        /// <summary>What an item looked like before an edit, so the panel can offer Undo.</summary>
        internal readonly struct WorkshopSnapshot
        {
            public WorkshopSnapshot(bool hadName, string name, bool hadDesc, string desc, bool hadCrafted, string crafted, string? lineLabel)
            {
                HadName = hadName;
                Name = name;
                HadDesc = hadDesc;
                Desc = desc;
                HadCrafted = hadCrafted;
                Crafted = crafted;
                LineLabel = lineLabel;
            }

            public bool HadName { get; }
            public string Name { get; }
            public bool HadDesc { get; }
            public string Desc { get; }
            public bool HadCrafted { get; }
            public string Crafted { get; }
            /// <summary>The stored tooltip prefix ("Forged by"...), or null for the default line.</summary>
            public string? LineLabel { get; }
        }

        /// <summary>Config allows custom "Crafted by" labels, or the local player is elevated.</summary>
        internal static bool MayEditCraftedByLineLabel() =>
            RenameitConfig.CraftedByLabelCustomizable ||
            global::DrakeRenameit.API.RenameitPermission.IsElevatedForOverrides(Player.m_localPlayer);

        /// <summary>The "Crafted by" tooltip prefix stored on the item, or null when it uses the default line.</summary>
        internal static string? GetStoredCraftedByLineLabel(ItemDrop.ItemData? item) =>
            item?.m_customData != null &&
            item.m_customData.TryGetValue(DrakeCustomDataKeys.CraftedByLineLabel, out var stored) &&
            !string.IsNullOrEmpty(stored)
                ? stored
                : null;

        internal static WorkshopSnapshot CaptureWorkshopSnapshot(ItemDrop.ItemData item) =>
            new WorkshopSnapshot(
                hasNewName(item), GetPropperName(item),
                hasNewDesc(item), getPropperDesc(item),
                HasCraftedByDisplayOverride(item), getCraftedByDisplay(item),
                GetStoredCraftedByLineLabel(item));

        /// <summary>
        /// Applies the fields that are not null (an empty string clears that customization, like the old OK button).
        /// <paramref name="setLineLabel"/> says the label picker changed: <paramref name="lineLabel"/> null = back to the default line.
        /// Returns the item the panel should keep editing: blank paper turns into a Written Page on its first edit, so
        /// that is a different instance. Null when the edit could not be applied (item left the inventory).
        /// </summary>
        internal static ItemDrop.ItemData? CommitWorkshopEdits(
            ItemDrop.ItemData item, string? newName, string? newDesc, string? newCraftedBy, string? lineLabel = null, bool setLineLabel = false)
        {
            CurrentItem = item;
            if (!IsEditableItemContext(item))
            {
                ValheimHudMessage.Show(Player.m_localPlayer, MessageHud.MessageType.Center, T(LKeys.MsgItemNotInInventoryApply));
                return null;
            }

            var player = Player.m_localPlayer;
            if (player == null)
                return null;

            if (PaperItem.IsBlankPaper(item))
            {
                var name = string.IsNullOrWhiteSpace(newName) ? null : newName;
                var desc = string.IsNullOrWhiteSpace(newDesc) ? null : newDesc;
                if (name == null && desc == null)
                    return item;
                var written = PaperWriteConverter.PeelBlankToWritten(item, customName: name, customDesc: desc, player);
                if (written == null)
                    return item;
                CurrentItem = written;
                return written;
            }

            // Only touch what the player is allowed to change; the panel already greys out the rest.
            if (newName != null && CanChangeName(item, false))
                RenameItem(newName);
            if (newDesc != null && CanChangeDesc(item, false))
                RewriteItemDesc(newDesc);
            if ((newCraftedBy != null || setLineLabel) && CanChangeCraftedByLabel(item, false))
            {
                if (newCraftedBy != null)
                    ApplyCraftedByDisplayWithoutUi(item, newCraftedBy);
                if (setLineLabel && MayEditCraftedByLineLabel())
                    ApplyCraftedByLineLabelWithoutUi(item, lineLabel, raiseEvent: newCraftedBy == null);
            }

            FlushWallSessionIfNeeded();
            return item;
        }

        /// <summary>Puts name / description / crafted-by (and its label) back to a snapshot (Undo).</summary>
        internal static void RestoreWorkshopSnapshot(ItemDrop.ItemData item, WorkshopSnapshot snapshot)
        {
            CurrentItem = item;
            if (!IsEditableItemContext(item))
                return;

            if (hasNewName(item) != snapshot.HadName || (snapshot.HadName && GetPropperName(item) != snapshot.Name))
            {
                if (snapshot.HadName)
                    RenameItem(snapshot.Name);
                else
                    resetName(item);
            }

            if (hasNewDesc(item) != snapshot.HadDesc || (snapshot.HadDesc && getPropperDesc(item) != snapshot.Desc))
            {
                if (snapshot.HadDesc)
                    RewriteItemDesc(snapshot.Desc);
                else
                    resetDesc(item);
            }

            if (snapshot.HadCrafted)
            {
                if (getCraftedByDisplay(item) != snapshot.Crafted)
                    ApplyCraftedByDisplayWithoutUi(item, snapshot.Crafted);
            }
            else if (HasCraftedByDisplayOverride(item))
            {
                ClearCraftedByWithoutUi(item);
            }

            if (GetStoredCraftedByLineLabel(item) != snapshot.LineLabel)
                ApplyCraftedByLineLabelWithoutUi(item, snapshot.LineLabel, raiseEvent: false);

            FlushWallSessionIfNeeded();
        }

        static void ApplyCraftedByDisplayWithoutUi(ItemDrop.ItemData item, string display)
        {
            EnsureLocalPlayerCrafterIfAbsent(item);
            var oldDisplay = getCraftedByDisplay(item);
            CustomizeLibsAPI.SetCraftedByDisplay(item, display);
            var newDisplay = string.IsNullOrEmpty(display) ? (item.m_crafterName ?? "") : display;
            global::DrakeRenameit.API.RenameEvents.RaiseCraftedByDisplayChanged(
                Player.m_localPlayer, item, item.m_shared.m_name, oldDisplay, newDisplay);
        }

        /// <summary>Sets (or, with null/empty, clears) the tooltip prefix. Only values from the allowed list are stored.</summary>
        static void ApplyCraftedByLineLabelWithoutUi(ItemDrop.ItemData item, string? lineLabel, bool raiseEvent)
        {
            if (string.IsNullOrEmpty(lineLabel))
                CustomizeLibsAPI.SetCraftedByLineLabel(item, null);
            else if (IsAllowedCustomCraftedByLineLabel(lineLabel!))
                CustomizeLibsAPI.SetCraftedByLineLabel(item, lineLabel);
            else
                return;

            if (raiseEvent)
            {
                var display = getCraftedByDisplay(item);
                global::DrakeRenameit.API.RenameEvents.RaiseCraftedByDisplayChanged(
                    Player.m_localPlayer, item, item.m_shared.m_name, display, display);
            }
        }

        static void ClearCraftedByWithoutUi(ItemDrop.ItemData item)
        {
            var oldDisplay = getCraftedByDisplay(item);
            CustomizeLibsAPI.ClearCraftedByOverrides(item);
            var newDisplay = item.m_crafterName ?? "";
            global::DrakeRenameit.API.RenameEvents.RaiseCraftedByDisplayChanged(
                Player.m_localPlayer, item, item.m_shared.m_name, oldDisplay, newDisplay);
        }

        /// <summary>The "Shared" flag, with the same wall-session flush the old checkbox did.</summary>
        internal static void SetWorkshopSharedFlag(ItemDrop.ItemData item, bool on)
        {
            Permissions.RenamePermissionManager.SetPublicRewriteFlag(item, on);
            if (PaperWallSession.Matches(item))
                PaperWallSession.FlushCurrentItemToVessel();
        }
    }
}
