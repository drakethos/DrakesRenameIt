using System;
using System.Collections.Generic;
using Jotunn.Managers;

namespace DrakeRenameit.API;

public static class RenameitPermission
{
    private static readonly HashSet<string> vipList = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    public static bool IsAdminOrVIP()
    {
        Player local = Player.m_localPlayer;
        if (local != null)
            return IsElevatedForOverrides(local);
        return false;
    }

    /// <summary>True when the player may bypass ownership, exclusions, and resource rules (subject to <see cref="RenameitConfig.AllowAdminOverride"/>).</summary>
    public static bool IsAdminOrVIP(Player player)
    {
        return IsElevatedForOverrides(player);
    }

    /// <summary>
    /// VIP list / API first; if <see cref="RenameitConfig.VipOnlyOverride"/> is true, Valheim server admin is not treated as elevated.
    /// Otherwise Valheim admin (<see cref="IsValheimAdmin"/>) also counts.
    /// </summary>
    public static bool IsElevatedForOverrides(Player? player)
    {
        if (!RenameitConfig.AllowAdminOverride || player == null)
            return false;

        string pid = player.GetPlayerID().ToString();
        string name = player.GetPlayerName();

        if (vipList.Contains(name) || vipList.Contains(pid))
            return true;

        if (RenameitConfig.VipOnlyOverride)
            return false;

        return IsValheimAdmin(player);
    }

    /// <summary>Valheim server admin (adminlist / Jotunn-synced), independent of VIP-only mode.</summary>
    public static bool IsValheimAdmin(Player? player)
    {
        return IsAdminSafe(player);
    }

    // Public API for other mods
    public static void AddVIP(string nameOrId)
    {
        if (!string.IsNullOrEmpty(nameOrId))
            vipList.Add(nameOrId);
    }

    public static void AddVIP(List<string> list)
    {
        if (list.Count > 0)
        {
            foreach (var s in list)
            {
                vipList.Add(s);
            }
        }
    }

    public static void RemoveVIP(string nameOrId)
    {
        if (!string.IsNullOrEmpty(nameOrId))
            vipList.Remove(nameOrId);
    }

    public static IEnumerable<string> GetVIPs() => vipList;

    /// <summary>
    /// True when the player is a Valheim server admin (adminlist entries are socket/Steam IDs, not character names).
    /// Local player: uses Jotunn's <see cref="SynchronizationManager.PlayerIsAdmin"/> (synced from server on clients).
    /// </summary>
    public static bool IsAdminSafe(Player? player)
    {
        if (player == null || ZNet.instance == null)
            return false;

        if (player == Player.m_localPlayer)
            return SynchronizationManager.Instance != null && SynchronizationManager.Instance.PlayerIsAdmin;

        return IsValheimAdminForRemotePlayer(player);
    }

    /// <summary>
    /// Dedicated / server: resolve peer by character and test adminlist using <see cref="ISocket.GetHostName"/> (same as vanilla adminlist IDs).
    /// </summary>
    private static bool IsValheimAdminForRemotePlayer(Player player)
    {
        try
        {
            if (ZNet.instance.m_adminList == null)
                return false;

            ZDOID zid = player.GetZDOID();

            foreach (ZNetPeer peer in ZNet.instance.GetPeers())
            {
                if (!peer.IsReady())
                    continue;
                if (peer.m_characterID != zid)
                    continue;

                string? hostId = peer.m_socket != null ? peer.m_socket.GetHostName() : null;
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
