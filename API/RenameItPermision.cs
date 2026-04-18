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
            return IsAdminOrVIP(local);
        return false;
    }

    // Core admin check
    public static bool IsAdminOrVIP(Player player)
    {
        //we always do false if admin is not allowed to override then theres no reason to even check.
        if (!RenameitConfig.AllowAdminOverride)
        {
            return false;
        }
        if (player == null) return false;

        string pid = player.GetPlayerID().ToString();
        string name = player.GetPlayerName();

        // Admin list check (Valheim adminlist / Jotunn-synced), OR VIP list / API below
        if (IsAdminSafe(player))
            return true;

        // Custom VIP check
        return vipList.Contains(name) || vipList.Contains(pid);
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
    public static bool IsAdminSafe(Player player)
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
