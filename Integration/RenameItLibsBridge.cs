using System;
using DrakeRenameit.API;
using DrakeRenameit.Permissions;
using DrakeRenameit.UI;
using DrakeModsLibs.Tags;
using DrakeModsLibs.API;
using DrakeModsLibs.Input;
using DrakeModsLibs.UI;

namespace DrakeRenameit.Integration;

internal sealed class RenameItDurabilityDisplayModifier : IDisplayNameModifier
{
    public bool AffectsDisplay(ItemDrop.ItemData? item) => DurabilityNameModifier.AffectsDisplay(item);
    public string GetPrefixRaw(ItemDrop.ItemData? item) => DurabilityNameModifier.GetPrefixRaw(item);
}

internal sealed class RenameItStackMergePolicy : IStackMergePolicy
{
    public bool SeparateStacksEnabled => RenameitConfig.SeparateStacks;
    public bool SeparateStacksHardLock => RenameitConfig.SeparateStacksHardLock;

    public DrakeStackForce GetStackForce(ItemDrop.ItemData? item)
    {
        if (PaperItem.IsPrintedPaper(item) || PaperCopyMark.IsCopy(item))
            return DrakeStackForce.ByIdentity;
        return DrakeStackForce.None;
    }
}

internal static class RenameItLibsBridge
{
    public const string InventoryMenuBindingId = "renameit.inventory";
    public const string TabId = DrakeTabRegistration.RenameItTabId;
    public const string PaperTabId = PaperTabPanel.TabId;
    /// <summary>Just after Rename so Paper sits beside it on the strip.</summary>
    const int PaperTabPriority = DrakeTabRegistration.DefaultRenamePriority + 1;
    static bool _registered;

    internal static void Register()
    {
        if (_registered)
            return;
        _registered = true;

        CustomizeLibsAPI.RegisterDisplayNameModifier(new RenameItDurabilityDisplayModifier());
        CustomizeLibsAPI.RegisterStackMergePolicy(new RenameItStackMergePolicy());
        CustomizeLibsAPI.SetShowItemStandItemNameWhenNoAccess(RenameitConfig.ShowItemStandItemNameWhenNoAccess);
        PaperCopyMark.RegisterSoftEditRule();

        // Binding + validators must succeed even if tab-host API drifts — otherwise Awake aborts before PatchAll.
        MenuBindingRegistry.Register(
            InventoryMenuBindingId,
            MenuBindingRegistry.InventoryContextScope,
            priority: DrakeTabRegistration.DefaultRenamePriority,
            getBindingString: () => RenameitConfig.MenuOpenModifier,
            modLabel: "DrakesRenameit");

        RegisterPermissionValidators();
        CustomizationGatekeeper.TagBypass = RenameitPermission.IsElevatedForOverrides;

        try
        {
            DrakeTabHost.Register(
                id: TabId,
                title: "Rename",
                priority: DrakeTabRegistration.DefaultRenamePriority,
                isAvailable: IsRenameTabAvailable,
                show: ShowRenameTab,
                claimDefault: _ => false,
                hide: HideRenameTab,
                getHintPhrase: () => "open rename options",
                getTitle: () => "Rename");

            DrakeTabHost.Register(
                id: PaperTabId,
                title: "Paper",
                priority: PaperTabPriority,
                isAvailable: IsPaperTabAvailable,
                show: PaperTabPanel.Show,
                claimDefault: ClaimPaperDefault,
                hide: PaperTabPanel.Hide,
                getHintPhrase: () => "open paper options",
                getTitle: () => "Paper");
        }
        catch (Exception ex)
        {
            RenameitConfig.Log?.LogError(
                $"[DrakesRenameit] DrakeTabHost.Register failed (libs API mismatch?). Inventory menu still uses fallback. {ex.GetType().Name}: {ex.Message}");
        }
    }

    static bool ClaimPaperDefault(ItemDrop.ItemData item) =>
        PaperItem.IsPrintedPaper(item) || PaperCopyMark.IsCopy(item);

    static bool IsRenameTabAvailable(ItemDrop.ItemData item)
    {
        if (item == null)
            return false;

        // Printed copies: Rename tab only with override.
        if (PaperCopyMark.IsCopy(item) || PaperItem.IsPrintedPaper(item))
            return RenameitPermission.IsElevatedForOverrides(Player.m_localPlayer);

        if (CustomizeLibsAPI.IsRenameInventorySuppressed(item))
            return false;
        if (!DrakeRenameit.IsEditableItemContext(item))
            return false;
        return DrakeRenameit.ShowUnlockButton(item) || DrakeRenameit.AnyInventoryActionAvailable(item);
    }

    static bool IsPaperTabAvailable(ItemDrop.ItemData item)
    {
        if (item == null || !RenameitConfig.PaperWallRenameEnabled)
            return false;
        if (!PaperItem.IsWrittenLike(item))
            return false;
        // Copies always get Paper (Make Copy / Make public) even when Rename is suppressed.
        if (PaperCopyMark.IsCopy(item) || PaperItem.IsPrintedPaper(item))
            return true;
        return DrakeRenameit.IsEditableItemContext(item);
    }

    static void ShowRenameTab(DrakeTabPageContext ctx)
    {
        var item = ctx.Item;
        if (DrakeRenameit.ShowUnlockButton(item))
        {
            UIPanels.OpenUnlockMenuFromInventory(item);
            return;
        }

        UIPanels.OpenActionMenu(item);
    }

    static void HideRenameTab() => UIPanels.HideForTabHost();

    static void RegisterPermissionValidators()
    {
        CustomizeLibsAPI.RegisterEditValidator(CustomizeOperation.RenameName, (item, player) =>
            item != null && RenamePermissionManager
                .Evaluate(RenamePermissionOperation.RenameItemName, item, player, false).Allowed);

        CustomizeLibsAPI.RegisterEditValidator(CustomizeOperation.RenameDescription, (item, player) =>
            item != null && RenamePermissionManager
                .Evaluate(RenamePermissionOperation.RewriteDescription, item, player, false).Allowed);

        CustomizeLibsAPI.RegisterEditValidator(CustomizeOperation.EditCraftedBy, (item, player) =>
            item != null && RenamePermissionManager
                .Evaluate(RenamePermissionOperation.EditCraftedByLabel, item, player, false).Allowed);
    }
}
