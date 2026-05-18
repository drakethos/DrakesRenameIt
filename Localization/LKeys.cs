namespace DrakeRenameit.ModText;

/// <summary>Localization keys. Edit <c>Assets/Localization/English.json</c> or language files such as <c>Spanish.json</c> (name must match Valheim's language id).</summary>
public static class LKeys
{
    // Action menu
    public const string MenuTitle = "menu_title";
    public const string MenuRename = "menu_rename";
    public const string MenuDescription = "menu_description";
    public const string MenuCraftedBy = "menu_crafted_by";
    public const string MenuResetAll = "menu_reset_all";
    public const string MenuOk = "menu_ok";
    public const string MenuUnlock = "menu_unlock";
    public const string MenuUnlockCost = "menu_unlock_cost";

    // Common buttons
    public const string BtnCancel = "btn_cancel";
    public const string BtnOk = "btn_ok";
    public const string BtnReset = "btn_reset";
    public const string BtnYes = "btn_yes";
    public const string BtnNo = "btn_no";

    // Reset-all confirm
    public const string ResetAllTitle = "reset_all_title";
    public const string ResetAllBody = "reset_all_body";

    // Editor panels
    public const string PanelRenameTitle = "panel_rename_title";
    public const string PanelDescTitle = "panel_desc_title";
    public const string PanelCraftedByTitle = "panel_crafted_by_title";
    public const string PlaceholderRename = "placeholder_rename";
    public const string PlaceholderDesc = "placeholder_desc";
    public const string PlaceholderCraftedBy = "placeholder_crafted_by";
    public const string TooltipLineLabel = "tooltip_line_label";
    public const string CraftedByLinePick = "crafted_by_line_pick";

    // Unlock panel
    public const string UnlockPanelTitle = "unlock_panel_title";
    public const string UnlockPayBtn = "unlock_pay_btn";
    public const string UnlockCostLabel = "unlock_cost_label";
    public const string UnlockPrompt = "unlock_prompt";
    public const string CraftedByFallback = "crafted_by_fallback";
    public const string UnlockCostAffordWarning = "unlock_cost_afford_warning";
    public const string UnlockCostLine = "unlock_cost_line";

    // HUD / messages
    public const string MsgItemNotInInventory = "msg_item_not_in_inventory";
    public const string MsgItemNotInInventoryUnlock = "msg_item_not_in_inventory_unlock";
    public const string MsgItemNotInInventoryApply = "msg_item_not_in_inventory_apply";
    public const string MsgItemUnlocked = "msg_item_unlocked";
    public const string MsgCannotEditItem = "msg_cannot_edit_item";
    public const string MsgDescEmpty = "msg_desc_empty";

    // Inventory tooltip hints
    public const string TooltipMenuHint = "tooltip_menu_hint";
    public const string TooltipMenuHintRightClick = "tooltip_menu_hint_right_click";
    public const string TooltipElevatedOverride = "tooltip_elevated_override";
    public const string MenuKeyFallback = "menu_key_fallback";

    // Permission denials (ShowReason off)
    public const string DenialRenameDisabled = "denial_rename_disabled";
    public const string DenialDescDisabled = "denial_desc_disabled";
    public const string DenialCraftedByDisabled = "denial_crafted_by_disabled";
    public const string DenialNotEnoughUnlock = "denial_not_enough_unlock";
    public const string DenialUnlockFirst = "denial_unlock_first";

    // Permission denials (ShowReason on)
    public const string DenialRenameDisabledConfig = "denial_rename_disabled_config";
    public const string DenialDescDisabledConfig = "denial_desc_disabled_config";
    public const string DenialCraftedByDisabledConfig = "denial_crafted_by_disabled_config";
    public const string DenialNotEnoughUnlockInventory = "denial_not_enough_unlock_inventory";
    public const string DenialPayUnlockFirst = "denial_pay_unlock_first";
    public const string DenialExcludedName = "denial_excluded_name";
    public const string DenialExcludedCategory = "denial_excluded_category";
    public const string DenialExcludedStacks = "denial_excluded_stacks";
    public const string DenialUncrafted = "denial_uncrafted";
    public const string DenialNotOwner = "denial_not_owner";

    public const string DenialCannotRename = "denial_cannot_rename";
    public const string DenialCannotDesc = "denial_cannot_desc";
    public const string DenialCannotCraftedBy = "denial_cannot_crafted_by";
    public const string DenialActionNotAllowed = "denial_action_not_allowed";

    // Tooltip denial hints (ShowReason off)
    public const string TooltipCannotRename = "tooltip_cannot_rename";
    public const string TooltipCannotDesc = "tooltip_cannot_desc";
    public const string TooltipCannotCraftedBy = "tooltip_cannot_crafted_by";
    public const string TooltipNotAllowed = "tooltip_not_allowed";
    public const string TooltipNotEnoughResourcesUnlock = "tooltip_not_enough_resources_unlock";

    // Unlock cost errors
    public const string UnlockErrNoPlayer = "unlock_err_no_player";
    public const string UnlockErrNotConfigured = "unlock_err_not_configured";
    public const string UnlockErrEmpty = "unlock_err_empty";
    public const string UnlockErrNoInventory = "unlock_err_no_inventory";
    public const string UnlockErrNotEnough = "unlock_err_not_enough";
}
