using DrakeRenameit.Permissions;
using DrakesWorkshopLibs.API;
using DrakesWorkshopLibs.Input;

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
}

internal static class RenameItLibsBridge
{
    public const string InventoryMenuBindingId = "renameit.inventory";
    static bool _registered;

    internal static void Register()
    {
        if (_registered)
            return;
        _registered = true;

        CustomizeLibsAPI.RegisterDisplayNameModifier(new RenameItDurabilityDisplayModifier());
        CustomizeLibsAPI.RegisterStackMergePolicy(new RenameItStackMergePolicy());
        CustomizeLibsAPI.SetShowItemStandItemNameWhenNoAccess(RenameitConfig.ShowItemStandItemNameWhenNoAccess);

        MenuBindingRegistry.Register(
            InventoryMenuBindingId,
            MenuBindingRegistry.InventoryContextScope,
            priority: 100,
            getBindingString: () => RenameitConfig.MenuOpenModifier,
            modLabel: "DrakesRenameit");

        RegisterPermissionValidators();
    }

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
