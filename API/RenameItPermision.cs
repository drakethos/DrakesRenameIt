using System;
using System.Collections.Generic;

namespace DrakeRenameit.API;

public static class RenameitPermission
{
    private static readonly HashSet<string> vipList = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    public static bool IsAdminOrVIP()
    {
        Player local = Player.m_localPlayer;
        if (local != null)
        {
            return IsAdminOrVIP(local);
        }
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

        // Admin list check
        if (ZNet.instance?.IsAdmin(pid) ?? false)
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
}
