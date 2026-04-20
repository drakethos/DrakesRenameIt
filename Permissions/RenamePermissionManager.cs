using System;
using System.Text;
using BepInEx.Logging;
using RenameitPermission = global::DrakeRenameit.API.RenameitPermission;

namespace DrakeRenameit.Permissions;

[Flags]
public enum RenameDenialReason
{
    None = 0,
    GlobalRenameDisabled = 1 << 0,
    GlobalDescDisabled = 1 << 1,
    NotOwner = 1 << 2,
    UncraftedResourceBlocked = 1 << 3,
    ExcludedByName = 1 << 4,
    ExcludedByCategory = 1 << 5,
    GlobalCraftedByDisabled = 1 << 6,
    UnlockCostRequired = 1 << 7
}

public enum RenamePermissionOperation
{
    RenameItemName,
    RewriteDescription,
    EditCraftedByLabel
}

public readonly struct RenamePermissionResult
{
    public readonly bool Allowed;
    public readonly RenameDenialReason Reasons;

    public RenamePermissionResult(bool allowed, RenameDenialReason reasons)
    {
        Allowed = allowed;
        Reasons = reasons;
    }
}

/// <summary>Single place for rename / description / crafted-by display permission checks, logging, and player-facing messages.</summary>
/// <remarks>
/// Evaluation order: Admin/VIP override → global feature toggles → LockToOwner (only if item has a crafter) →
/// RenameAllowlist → (excluded name, excluded category, uncrafted/resource rule) → optional one-time unlock cost (stack flag).
/// </remarks>
public static class RenamePermissionManager
{
    private static ManualLogSource? _log;
    private static int _ignoreUnlockRequirementDepth;

    internal static void Init(ManualLogSource logSource)
    {
        _log = logSource;
    }

    /// <summary>While &gt; 0, unlock-cost gate is skipped (used to test whether any edit would be allowed if the stack were unlocked).</summary>
    internal static void BeginIgnoreUnlockRequirement() => _ignoreUnlockRequirementDepth++;

    internal static void EndIgnoreUnlockRequirement()
    {
        if (_ignoreUnlockRequirementDepth > 0)
            _ignoreUnlockRequirementDepth--;
    }

    /// <summary>
    /// Computes allow/deny without sending <see cref="MessageHud"/>. Use <paramref name="logDenied"/> true for
    /// player actions; false for tooltips to avoid duplicate log spam.
    /// </summary>
    public static RenamePermissionResult TryGetDenial(
        RenamePermissionOperation op,
        ItemDrop.ItemData? item,
        Player? local,
        bool logDenied = false)
    {
        if (local == null)
        {
            if (logDenied)
                LogDenied(op, item, RenameDenialReason.None, "No local player.");
            return new RenamePermissionResult(false, RenameDenialReason.None);
        }

        bool elevated = RenameitPermission.IsElevatedForOverrides(local);

        // 1. Admin / VIP override — skips everything below
        if (elevated)
        {
            LogAllowed(op, item, "Elevated (admin/VIP per config).");
            return new RenamePermissionResult(true, RenameDenialReason.None);
        }

        // 2. Global feature toggles (allowlist does not bypass)
        switch (op)
        {
            case RenamePermissionOperation.RenameItemName:
                if (!RenameitConfig.RenameEnabled)
                {
                    var r = RenameDenialReason.GlobalRenameDisabled;
                    if (logDenied)
                        LogDenied(op, item, r, "RenameEnabled is false and player is not elevated.");
                    return new RenamePermissionResult(false, r);
                }
                break;
            case RenamePermissionOperation.RewriteDescription:
                if (!RenameitConfig.RewriteDescriptionsEnabled)
                {
                    var r = RenameDenialReason.GlobalDescDisabled;
                    if (logDenied)
                        LogDenied(op, item, r, "RewriteDescriptionsEnabled is false and player is not elevated.");
                    return new RenamePermissionResult(false, r);
                }
                break;
            case RenamePermissionOperation.EditCraftedByLabel:
                if (!RenameitConfig.CraftedByLabelEnabled)
                {
                    var r = RenameDenialReason.GlobalCraftedByDisabled;
                    if (logDenied)
                        LogDenied(op, item, r, "CraftedByLabelEnabled is false and player is not elevated.");
                    return new RenamePermissionResult(false, r);
                }
                break;
        }

        return EvaluateItemRules(op, item, local, logDenied);
    }

    public static RenamePermissionResult Evaluate(
        RenamePermissionOperation op,
        ItemDrop.ItemData? item,
        Player? local,
        bool showErrorToPlayer)
    {
        var result = TryGetDenial(op, item, local, logDenied: showErrorToPlayer);

        if (!result.Allowed && showErrorToPlayer && local != null)
            local.Message(MessageHud.MessageType.Center, FormatDenialForPlayer(op, result.Reasons));

        return result;
    }

    /// <summary>3–5: owner lock, then allowlist, then exclusions / uncrafted rule.</summary>
    private static RenamePermissionResult EvaluateItemRules(
        RenamePermissionOperation op,
        ItemDrop.ItemData? item,
        Player local,
        bool logDenied)
    {
        if (item?.m_shared == null)
        {
            if (logDenied)
                LogDenied(op, item, RenameDenialReason.None, "Item or shared data null.");
            return new RenamePermissionResult(false, RenameDenialReason.None);
        }

        // 3. Lock to owner — only when the stack has an assigned crafter (crafted or claimed via NameClaimsOwner).
        if (RenameitConfig.LockToOwner && !PassesOwnerLock(item, local))
        {
            var r = RenameDenialReason.NotOwner;
            if (logDenied)
                LogDenied(op, item, r,
                    $"Owner lock: crafterID={item.m_crafterID} crafterName={item.m_crafterName} local={local.GetPlayerID()}.");
            return new RenamePermissionResult(false, r);
        }

        // 4. Allowlist — skips tier 5
        if (RenameExclusionRules.MatchesRenameAllowlist(item))
        {
            LogAllowed(op, item, "On RenameAllowlist; skipping exclusions / resource rule.");
            return new RenamePermissionResult(true, RenameDenialReason.None);
        }

        // 5. Same tier: name, category, uncrafted/resource
        if (RenameExclusionRules.MatchesExcludedName(item))
        {
            var r = RenameDenialReason.ExcludedByName;
            if (logDenied)
                LogDenied(op, item, r, $"Excluded by name. Item={item.m_shared.m_name}");
            return new RenamePermissionResult(false, r);
        }

        if (RenameExclusionRules.MatchesExcludedCategory(item))
        {
            var r = RenameDenialReason.ExcludedByCategory;
            if (logDenied)
                LogDenied(op, item, r, $"Excluded by category. Item={item.m_shared.m_name}");
            return new RenamePermissionResult(false, r);
        }

        if (!RenameitConfig.AllowRenameResources && IsUnownedResourceStack(item))
        {
            var r = RenameDenialReason.UncraftedResourceBlocked;
            if (logDenied)
                LogDenied(op, item, r, "Uncrafted/resource blocked (AllowRenameResources false).");
            return new RenamePermissionResult(false, r);
        }

        if (_ignoreUnlockRequirementDepth == 0 &&
            RenameUnlockCost.UnlockCostApplies() &&
            !DrakeRenameit.IsRenameUnlocked(item))
        {
            var r = RenameDenialReason.UnlockCostRequired;
            if (logDenied)
                LogDenied(op, item, r, "Stack not unlocked for editing (UnlockCost).");
            return new RenamePermissionResult(false, r);
        }

        LogAllowed(op, item, "Item rules passed.");
        return new RenamePermissionResult(true, RenameDenialReason.None);
    }

    private static bool IsUnownedResourceStack(ItemDrop.ItemData item) =>
        item.m_crafterID == 0L && string.IsNullOrEmpty(item.m_crafterName);

    private static bool PassesOwnerLock(ItemDrop.ItemData item, Player local)
    {
        if (item.m_crafterID != 0L)
            return item.m_crafterID == local.GetPlayerID();
        if (!string.IsNullOrEmpty(item.m_crafterName))
            return item.m_crafterName.Equals(local.GetPlayerName(), StringComparison.OrdinalIgnoreCase);
        return true;
    }

    private static void LogAllowed(RenamePermissionOperation op, ItemDrop.ItemData? item, string detail)
    {
        string id = item?.m_shared?.m_name ?? "?";
        _log?.LogDebug($"[Permission] ALLOW {op} item={id} — {detail}");
    }

    private static void LogDenied(
        RenamePermissionOperation op,
        ItemDrop.ItemData? item,
        RenameDenialReason reasons,
        string detail)
    {
        string id = item?.m_shared?.m_name ?? "?";
        _log?.LogInfo($"[Permission] DENY {op} item={id} reasons={reasons} — {detail}");
    }

    /// <summary>Player-facing denial text (respects ShowReason). Use for MessageHud and tooltips.</summary>
    public static string FormatDenialForPlayer(RenamePermissionOperation op, RenameDenialReason reasons)
    {
        if (!RenameitConfig.ShowReason)
        {
            if ((reasons & RenameDenialReason.GlobalRenameDisabled) != 0)
                return "Renaming is disabled.";
            if ((reasons & RenameDenialReason.GlobalDescDisabled) != 0)
                return "Description editing is disabled.";
            if ((reasons & RenameDenialReason.GlobalCraftedByDisabled) != 0)
                return "Crafted-by label editing is disabled.";
            if ((reasons & RenameDenialReason.UnlockCostRequired) != 0)
            {
                if (Player.m_localPlayer != null &&
                    RenameUnlockCost.UnlockCostApplies() &&
                    !RenameUnlockCost.CanPlayerAfford(Player.m_localPlayer))
                    return "Not enough items to unlock.";
                return "Unlock this stack first (action menu).";
            }

            return GenericDeniedLine(op);
        }

        if ((reasons & RenameDenialReason.GlobalRenameDisabled) != 0)
            return "Renaming is disabled for this world (config).";
        if ((reasons & RenameDenialReason.GlobalDescDisabled) != 0)
            return "Description editing is disabled for this world (config).";
        if ((reasons & RenameDenialReason.GlobalCraftedByDisabled) != 0)
            return "Crafted-by label editing is disabled for this world (config).";
        if ((reasons & RenameDenialReason.UnlockCostRequired) != 0)
        {
            if (Player.m_localPlayer != null &&
                RenameUnlockCost.UnlockCostApplies() &&
                !RenameUnlockCost.CanPlayerAfford(Player.m_localPlayer))
                return "Not enough items in inventory to unlock this stack for editing.";
            return "Pay the unlock cost in the action menu (Unlock) before editing this stack.";
        }

        var parts = new StringBuilder();
        if ((reasons & RenameDenialReason.ExcludedByName) != 0)
            parts.Append("Excluded by name. ");
        if ((reasons & RenameDenialReason.ExcludedByCategory) != 0)
            parts.Append("Excluded by category. ");
        if ((reasons & RenameDenialReason.UncraftedResourceBlocked) != 0)
            parts.Append("Uncrafted resources cannot be changed (config). ");
        if ((reasons & RenameDenialReason.NotOwner) != 0)
            parts.Append("You don't own this item. ");

        if (parts.Length > 0)
            return parts.ToString().Trim();

        return GenericDeniedLine(op);
    }

    private static string GenericDeniedLine(RenamePermissionOperation op) =>
        op switch
        {
            RenamePermissionOperation.RenameItemName => "This item cannot be renamed.",
            RenamePermissionOperation.RewriteDescription => "This item's description cannot be changed.",
            RenamePermissionOperation.EditCraftedByLabel => "This item's crafted-by label cannot be changed.",
            _ => "Action not allowed."
        };

    public static string BuildPlayerMessage(RenamePermissionOperation op, RenameDenialReason reasons) =>
        FormatDenialForPlayer(op, reasons);

    /// <summary>Short line for tooltips when action is blocked (no duplicate logging).</summary>
    public static string GetTooltipDisabledHint(RenamePermissionOperation op, ItemDrop.ItemData? item)
    {
        var res = TryGetDenial(op, item, Player.m_localPlayer, logDenied: false);
        if (res.Allowed)
            return "";

        if (!RenameitConfig.ShowReason)
        {
            if ((res.Reasons & RenameDenialReason.UnlockCostRequired) != 0)
            {
                if (Player.m_localPlayer != null &&
                    RenameUnlockCost.UnlockCostApplies() &&
                    !RenameUnlockCost.CanPlayerAfford(Player.m_localPlayer))
                    return "Not enough resources to unlock.";
                return "Unlock this stack first (action menu).";
            }

            return op switch
            {
                RenamePermissionOperation.RenameItemName => "Cannot rename this item.",
                RenamePermissionOperation.RewriteDescription => "Cannot edit description.",
                RenamePermissionOperation.EditCraftedByLabel => "Cannot edit crafted-by label.",
                _ => "Not allowed."
            };
        }

        return FormatDenialForPlayer(op, res.Reasons);
    }
}
