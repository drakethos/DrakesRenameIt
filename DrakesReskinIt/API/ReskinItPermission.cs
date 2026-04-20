using System;
using System.Collections.Generic;
using Jotunn.Managers;

namespace DrakesReskinIt.API;

/// <summary>
/// Public API for admin/VIP elevation checks and VIP list management.
/// Mirrors DrakeRenameIt's permission model so the two mods share the same admin/VIP semantics.
/// </summary>
public static class ReskinItPermission
{
    private static readonly HashSet<string> vipList = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    public static bool IsAdminOrVIP()
    {
        Player local = Player.m_localPlayer;
        if (local != null)
            return IsElevatedForOverrides(local);
        return false;
    }

    /// <summary>True when the player may bypass ownership and exclusion rules (subject to <see cref="ReskinItConfig.AllowAdminOverride"/>).</summary>
    public static bool IsAdminOrVIP(Player player) => IsElevatedForOverrides(player);

    /// <summary>
    /// VIP list / API first; if <see cref="ReskinItConfig.VipOnlyOverride"/> is true, Valheim server admin is not elevated.
    /// Otherwise Valheim admin (<see cref="IsValheimAdmin"/>) also counts.
    /// </summary>
    public static bool IsElevatedForOverrides(Player? player)
    {
        if (!ReskinItConfig.AllowAdminOverride || player == null)
            return false;

        string pid = player.GetPlayerID().ToString();
        string name = player.GetPlayerName();

        if (vipList.Contains(name) || vipList.Contains(pid))
            return true;

        if (ReskinItConfig.VipOnlyOverride)
            return false;

        return IsValheimAdmin(player);
    }

    /// <summary>Valheim server admin (adminlist / Jotunn-synced), independent of VIP-only mode.</summary>
    public static bool IsValheimAdmin(Player? player) => IsAdminSafe(player);

    // ─── Public API ───────────────────────────────────────────────────────────

    public static void AddVIP(string nameOrId)
    {
        if (!string.IsNullOrEmpty(nameOrId))
            vipList.Add(nameOrId);
    }

    public static void AddVIP(List<string> list)
    {
        foreach (var s in list)
            vipList.Add(s);
    }

    public static void RemoveVIP(string nameOrId)
    {
        if (!string.IsNullOrEmpty(nameOrId))
            vipList.Remove(nameOrId);
    }

    public static IEnumerable<string> GetVIPs() => vipList;

    // ─── Admin detection (matches DrakeRenameIt pattern) ─────────────────────

    /// <summary>
    /// Local player: uses Jotunn's PlayerIsAdmin (synced from server).
    /// Remote player: resolves ZNetPeer by character ZDOID, reads socket host id, checks adminlist.
    /// </summary>
    public static bool IsAdminSafe(Player? player)
    {
        if (player == null || ZNet.instance == null)
            return false;

        if (player == Player.m_localPlayer)
            return SynchronizationManager.Instance != null && SynchronizationManager.Instance.PlayerIsAdmin;

        return IsValheimAdminForRemotePlayer(player);
    }

    private static bool IsValheimAdminForRemotePlayer(Player player)
    {
        try
        {
            if (ZNet.instance.m_adminList == null)
                return false;

            ZDOID zid = player.GetZDOID();
            foreach (ZNetPeer peer in ZNet.instance.GetPeers())
            {
                if (!peer.IsReady()) continue;
                if (peer.m_characterID != zid) continue;

                string? hostId = peer.m_socket?.GetHostName();
                if (string.IsNullOrEmpty(hostId))
                    return false;

                return ZNet.instance.ListContainsId(ZNet.instance.m_adminList, hostId);
            }
        }
        catch
        {
            return false;
        }
        return false;
    }
}
