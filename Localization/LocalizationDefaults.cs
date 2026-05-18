using System.Collections.Generic;

namespace DrakeRenameit.ModText;

/// <summary>Built-in English strings (used when JSON is missing or fails to parse).</summary>
internal static class LocalizationDefaults
{
    internal static void Populate(Dictionary<string, string> target)
    {
        void Add(string key, string value) => target[key] = value;

        Add(LKeys.MenuTitle, "DrakesRenameit");
        Add(LKeys.MenuRename, "Rename");
        Add(LKeys.MenuDescription, "Description");
        Add(LKeys.MenuCraftedBy, "Crafted by");
        Add(LKeys.MenuResetAll, "Reset all");
        Add(LKeys.MenuOk, "OK");
        Add(LKeys.MenuUnlock, "🔒 Unlock");
        Add(LKeys.MenuUnlockCost, "🔒 Unlock ({0})");

        Add(LKeys.BtnCancel, "Cancel");
        Add(LKeys.BtnOk, "OK");
        Add(LKeys.BtnReset, "Reset");
        Add(LKeys.BtnYes, "Yes");
        Add(LKeys.BtnNo, "No");

        Add(LKeys.ResetAllTitle, "Reset all customizations?");
        Add(LKeys.ResetAllBody, "Clears custom name, description, and crafted-by on this stack.");

        Add(LKeys.PanelRenameTitle, "Rename Item");
        Add(LKeys.PanelDescTitle, "Rewrite Item Desc");
        Add(LKeys.PanelCraftedByTitle, "Crafted by (display)");
        Add(LKeys.PlaceholderRename, "Enter new name...");
        Add(LKeys.PlaceholderDesc, "Enter new desc");
        Add(LKeys.PlaceholderCraftedBy, "Display name on tooltip…");
        Add(LKeys.TooltipLineLabel, "Tooltip line");
        Add(LKeys.CraftedByLinePick, "…");

        Add(LKeys.UnlockPanelTitle, "🔒 Unlock Item");
        Add(LKeys.UnlockPayBtn, "🔒 Pay");
        Add(LKeys.UnlockCostLabel, "Unlock cost:");
        Add(LKeys.CraftedByFallback, "Crafted by");
        Add(LKeys.UnlockPrompt, "Unlock this item stack for editing?");
        Add(LKeys.UnlockCostAffordWarning, "<color=red>You don't have enough items to unlock.</color>");
        Add(LKeys.UnlockCostLine, "{0}x {1}  <color={2}>({3} in inv)</color>");

        Add(LKeys.MsgItemNotInInventory, "That item is no longer in your inventory.");
        Add(LKeys.MsgItemNotInInventoryUnlock, "That item is no longer in your inventory. Put it back, then unlock again.");
        Add(LKeys.MsgItemNotInInventoryApply, "That item is no longer in your inventory. Put it back, then try again.");
        Add(LKeys.MsgItemUnlocked, "Item unlocked for editing.");
        Add(LKeys.MsgCannotEditItem, "This item cannot be edited.");
        Add(LKeys.MsgDescEmpty, "Description must not be empty!");

        Add(LKeys.TooltipMenuHint, "{0} + Right Click for options");
        Add(LKeys.TooltipMenuHintRightClick, "Right Click for options");
        Add(LKeys.TooltipElevatedOverride, " Elevated override");
        Add(LKeys.MenuKeyFallback, "Key");

        Add(LKeys.DenialRenameDisabled, "Renaming is disabled.");
        Add(LKeys.DenialDescDisabled, "Description editing is disabled.");
        Add(LKeys.DenialCraftedByDisabled, "Crafted-by label editing is disabled.");
        Add(LKeys.DenialNotEnoughUnlock, "Not enough items to unlock.");
        Add(LKeys.DenialUnlockFirst, "Unlock this stack first (action menu).");

        Add(LKeys.DenialRenameDisabledConfig, "Renaming is disabled for this world (config).");
        Add(LKeys.DenialDescDisabledConfig, "Description editing is disabled for this world (config).");
        Add(LKeys.DenialCraftedByDisabledConfig, "Crafted-by label editing is disabled for this world (config).");
        Add(LKeys.DenialNotEnoughUnlockInventory, "Not enough items in inventory to unlock this stack for editing.");
        Add(LKeys.DenialPayUnlockFirst, "Pay the unlock cost in the action menu (Unlock) before editing this stack.");
        Add(LKeys.DenialExcludedName, "Excluded by name. ");
        Add(LKeys.DenialExcludedCategory, "Excluded by category. ");
        Add(LKeys.DenialExcludedStacks, "Stackable items cannot be customized (config). ");
        Add(LKeys.DenialUncrafted, "Unowned items cannot be changed (config). ");
        Add(LKeys.DenialNotOwner, "You don't own this item. ");

        Add(LKeys.DenialCannotRename, "This item cannot be renamed.");
        Add(LKeys.DenialCannotDesc, "This item's description cannot be changed.");
        Add(LKeys.DenialCannotCraftedBy, "This item's crafted-by label cannot be changed.");
        Add(LKeys.DenialActionNotAllowed, "Action not allowed.");

        Add(LKeys.TooltipCannotRename, "Cannot rename this item.");
        Add(LKeys.TooltipCannotDesc, "Cannot edit description.");
        Add(LKeys.TooltipCannotCraftedBy, "Cannot edit crafted-by label.");
        Add(LKeys.TooltipNotAllowed, "Not allowed.");
        Add(LKeys.TooltipNotEnoughResourcesUnlock, "Not enough resources to unlock.");

        Add(LKeys.UnlockErrNoPlayer, "No local player.");
        Add(LKeys.UnlockErrNotConfigured, "Unlock cost is not configured.");
        Add(LKeys.UnlockErrEmpty, "Unlock cost is empty.");
        Add(LKeys.UnlockErrNoInventory, "No inventory.");
        Add(LKeys.UnlockErrNotEnough, "Not enough items to unlock (see tooltip / config).");
    }
}
