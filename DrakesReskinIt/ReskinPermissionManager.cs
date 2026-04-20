using System;
using System.Text;
using BepInEx.Logging;
using ReskinItPermission = global::DrakesReskinIt.API.ReskinItPermission;

namespace DrakesReskinIt;

[Flags]
public enum ReskinDenialReason
{
    None = 0,
    GlobalReskinDisabled = 1 << 0,
    GlobalTintDisabled = 1 << 1,
    NotOwner = 1 << 2,
    ExcludedByName = 1 << 3,
    ExcludedByCategory = 1 << 4,
}

public enum ReskinPermissionOperation
{
    ChangeIcon,
    ChangeTint,
}

public readonly struct ReskinPermissionResult
{
    public readonly bool Allowed;
    public readonly ReskinDenialReason Reasons;

    public ReskinPermissionResult(bool allowed, ReskinDenialReason reasons)
    {
        Allowed = allowed;
        Reasons = reasons;
    }
}

/// <summary>Single place for icon/tint permission checks, logging, and player-facing messages.</summary>
/// <remarks>
/// Evaluation order: Admin/VIP override → global feature toggles → LockToOwner →
/// ReskinAllowlist → (excluded name, excluded category).
/// </remarks>
public static class ReskinPermissionManager
{
    private static ManualLogSource? _log;

    internal static void Init(ManualLogSource logSource)
    {
        _log = logSource;
    }

    public static ReskinPermissionResult TryGetDenial(
        ReskinPermissionOperation op,
        ItemDrop.ItemData? item,
        Player? local,
        bool logDenied = false)
    {
        if (local == null)
        {
            if (logDenied) LogDenied(op, item, ReskinDenialReason.None, "No local player.");
            return new ReskinPermissionResult(false, ReskinDenialReason.None);
        }

        bool elevated = ReskinItPermission.IsElevatedForOverrides(local);

        if (elevated)
        {
            LogAllowed(op, item, "Elevated (admin/VIP per config).");
            return new ReskinPermissionResult(true, ReskinDenialReason.None);
        }

        switch (op)
        {
            case ReskinPermissionOperation.ChangeIcon:
                if (!ReskinItConfig.ReskinEnabled)
                {
                    var r = ReskinDenialReason.GlobalReskinDisabled;
                    if (logDenied) LogDenied(op, item, r, "ReskinEnabled is false.");
                    return new ReskinPermissionResult(false, r);
                }
                break;
            case ReskinPermissionOperation.ChangeTint:
                if (!ReskinItConfig.TintEnabled)
                {
                    var r = ReskinDenialReason.GlobalTintDisabled;
                    if (logDenied) LogDenied(op, item, r, "TintEnabled is false.");
                    return new ReskinPermissionResult(false, r);
                }
                break;
        }

        return EvaluateItemRules(op, item, local, logDenied);
    }

    public static ReskinPermissionResult Evaluate(
        ReskinPermissionOperation op,
        ItemDrop.ItemData? item,
        Player? local,
        bool showErrorToPlayer)
    {
        var result = TryGetDenial(op, item, local, logDenied: showErrorToPlayer);

        if (!result.Allowed && showErrorToPlayer && local != null)
            local.Message(MessageHud.MessageType.Center, FormatDenialForPlayer(op, result.Reasons));

        return result;
    }

    private static ReskinPermissionResult EvaluateItemRules(
        ReskinPermissionOperation op,
        ItemDrop.ItemData? item,
        Player local,
        bool logDenied)
    {
        if (item?.m_shared == null)
        {
            if (logDenied) LogDenied(op, item, ReskinDenialReason.None, "Item or shared data null.");
            return new ReskinPermissionResult(false, ReskinDenialReason.None);
        }

        if (ReskinItConfig.LockToOwner && !PassesOwnerLock(item, local))
        {
            var r = ReskinDenialReason.NotOwner;
            if (logDenied)
                LogDenied(op, item, r,
                    $"Owner lock: crafterID={item.m_crafterID} crafterName={item.m_crafterName} local={local.GetPlayerID()}.");
            return new ReskinPermissionResult(false, r);
        }

        if (ReskinExclusionRules.MatchesReskinAllowlist(item))
        {
            LogAllowed(op, item, "On ReskinAllowlist; skipping exclusions.");
            return new ReskinPermissionResult(true, ReskinDenialReason.None);
        }

        if (ReskinExclusionRules.MatchesExcludedName(item))
        {
            var r = ReskinDenialReason.ExcludedByName;
            if (logDenied) LogDenied(op, item, r, $"Excluded by name. Item={item.m_shared.m_name}");
            return new ReskinPermissionResult(false, r);
        }

        if (ReskinExclusionRules.MatchesExcludedCategory(item))
        {
            var r = ReskinDenialReason.ExcludedByCategory;
            if (logDenied) LogDenied(op, item, r, $"Excluded by category. Item={item.m_shared.m_name}");
            return new ReskinPermissionResult(false, r);
        }

        LogAllowed(op, item, "Item rules passed.");
        return new ReskinPermissionResult(true, ReskinDenialReason.None);
    }

    private static bool PassesOwnerLock(ItemDrop.ItemData item, Player local)
    {
        if (item.m_crafterID != 0L)
            return item.m_crafterID == local.GetPlayerID();
        if (!string.IsNullOrEmpty(item.m_crafterName))
            return item.m_crafterName.Equals(local.GetPlayerName(), StringComparison.OrdinalIgnoreCase);
        return true;
    }

    private static void LogAllowed(ReskinPermissionOperation op, ItemDrop.ItemData? item, string detail)
    {
        _log?.LogDebug($"[ReskinPerm] ALLOW {op} item={item?.m_shared?.m_name ?? "?"} — {detail}");
    }

    private static void LogDenied(ReskinPermissionOperation op, ItemDrop.ItemData? item, ReskinDenialReason reasons, string detail)
    {
        _log?.LogInfo($"[ReskinPerm] DENY {op} item={item?.m_shared?.m_name ?? "?"} reasons={reasons} — {detail}");
    }

    public static string FormatDenialForPlayer(ReskinPermissionOperation op, ReskinDenialReason reasons)
    {
        if (!ReskinItConfig.ShowReason)
        {
            if ((reasons & ReskinDenialReason.GlobalReskinDisabled) != 0) return "Icon customization is disabled.";
            if ((reasons & ReskinDenialReason.GlobalTintDisabled) != 0) return "Tint/recolor is disabled.";
            return GenericDeniedLine(op);
        }

        if ((reasons & ReskinDenialReason.GlobalReskinDisabled) != 0)
            return "Icon customization is disabled for this world (config).";
        if ((reasons & ReskinDenialReason.GlobalTintDisabled) != 0)
            return "Tint/recolor is disabled for this world (config).";

        var parts = new StringBuilder();
        if ((reasons & ReskinDenialReason.ExcludedByName) != 0) parts.Append("Excluded by name. ");
        if ((reasons & ReskinDenialReason.ExcludedByCategory) != 0) parts.Append("Excluded by category. ");
        if ((reasons & ReskinDenialReason.NotOwner) != 0) parts.Append("You don't own this item. ");
        if (parts.Length > 0) return parts.ToString().Trim();

        return GenericDeniedLine(op);
    }

    private static string GenericDeniedLine(ReskinPermissionOperation op) =>
        op switch
        {
            ReskinPermissionOperation.ChangeIcon => "This item's icon cannot be changed.",
            ReskinPermissionOperation.ChangeTint => "This item's tint cannot be changed.",
            _ => "Action not allowed."
        };

    public static string GetTooltipDisabledHint(ReskinPermissionOperation op, ItemDrop.ItemData? item)
    {
        var res = TryGetDenial(op, item, Player.m_localPlayer, logDenied: false);
        if (res.Allowed) return "";

        return op switch
        {
            ReskinPermissionOperation.ChangeIcon => "Cannot change icon.",
            ReskinPermissionOperation.ChangeTint => "Cannot change tint.",
            _ => "Not allowed."
        };
    }
}
