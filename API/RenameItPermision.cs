using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Configuration;
using HarmonyLib;
using Jotunn.Managers;
using DrakeModsLibs.Sync;

namespace DrakeRenameit.API;

public static class RenameitPermission
{
    // ZNet.m_adminList / ListContainsId are private in the real game: reflection only (direct access throws at runtime).
    private static readonly FieldInfo? AdminListField = AccessTools.Field(typeof(ZNet), "m_adminList");
    private static readonly MethodInfo? ListContainsIdMethod = AccessTools.Method(typeof(ZNet), "ListContainsId");

    /// <summary>From synced <see cref="RenameitConfig.VipList"/> cfg only.</summary>
    private static readonly HashSet<string> configVipList = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>From VIP list API / runtime <see cref="AddVIP"/> — never written to synced cfg.</summary>
    private static readonly HashSet<string> apiVipList = new(StringComparer.OrdinalIgnoreCase);
    public static bool IsAdminOrVIP()
    {
        Player? local = Player.m_localPlayer;
        return local != null && IsElevatedForOverrides(local);
    }

    /// <summary>True when the player may bypass ownership, exclusions, and resource rules (subject to <see cref="RenameitConfig.AllowAdminOverride"/>).</summary>
    public static bool IsAdminOrVIP(Player player) => IsElevatedForOverrides(player);

    /// <summary>True when the player is on cfg <see cref="RenameitConfig.VipList"/> and/or the in-memory VIP API list. Does not include Valheim admin.</summary>
    public static bool IsModVip(Player? player)
    {
        if (player == null)
            return false;

        bool trustLocal = LocalVipListsTrusted();
        bool trustHostApi = HostApiListActive();
        foreach (string key in GetVipIdentityKeys(player))
        {
            if (trustLocal && configVipList.Contains(key))
                return true;
            if ((trustLocal || trustHostApi) && apiVipList.Contains(key))
                return true;
        }

        return false;
    }

    static DrakeConfigSync? _vipConfigSync;
    static bool _apiListFromHost;

    /// <summary>
    /// A remote client may only trust VIP entries the host pushed. If we are connected to someone else's
    /// server and ConfigSync never made the host the source of truth (server lacks this mod, or sync not
    /// finished), the local cfg VipList is just a file the player can edit — ignore it.
    /// </summary>
    static bool LocalVipListsTrusted()
    {
        var znet = ZNet.instance;
        if (znet == null || znet.IsServer())
            return true;
        return _vipConfigSync != null && _vipConfigSync.IsSourceOfTruth;
    }

    /// <summary>
    /// The host-pushed VIP list only counts while we are connected to that host. Once we are no longer
    /// connected (left the server, or the host is gone), drop it so the next server does not inherit it.
    /// </summary>
    static bool HostApiListActive()
    {
        var znet = ZNet.instance;
        bool connected = znet != null && !znet.IsServer() && znet.GetServerPeer() != null;
        if (!connected && _apiListFromHost)
        {
            _apiListFromHost = false;
            apiVipList.Clear();
        }
        return connected && _apiListFromHost;
    }

    /// <summary>
    /// Mod VIP first; if <see cref="RenameitConfig.VipOnlyOverride"/> is true, Valheim server admin is not treated as elevated.
    /// Otherwise Valheim admin (<see cref="IsValheimAdmin"/>) also counts. Never grants Valheim admin powers.
    /// </summary>
    public static bool IsElevatedForOverrides(Player? player)
    {
        if (!RenameitConfig.AllowAdminOverride || player == null)
            return false;

        if (IsModVip(player))
            return true;

        if (RenameitConfig.VipOnlyOverride)
            return false;

        return IsValheimAdmin(player);
    }

    /// <summary>Valheim server admin (adminlist / Jotunn-synced), independent of VIP-only mode.</summary>
    public static bool IsValheimAdmin(Player? player) => IsAdminSafe(player);

    /// <summary>Subscribe to ServerSync config updates; call once from <see cref="RenameitConfig.Bind"/>.</summary>
    internal static void WireVipListSync(ConfigEntry<string> vipListEntry, DrakeConfigSync configSync)
    {
        _vipConfigSync = configSync;
        vipListEntry.SettingChanged += (_, _) => ReloadVipsFromSyncedConfig();
        configSync.SourceOfTruthChanged += _ => ReloadVipsFromSyncedConfig();
        ReloadVipsFromSyncedConfig();
    }

    /// <summary>Replace cfg-backed VIP entries from the current synced <see cref="RenameitConfig.VipList"/> value.</summary>
    public static void ReloadVipsFromSyncedConfig()
    {
        configVipList.Clear();
        foreach (string entry in ParseVipListEntries(RenameitConfig.VipList))
            configVipList.Add(entry);
    }

    internal static IEnumerable<string> ParseVipListEntries(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            yield break;

        foreach (string part in raw.Split(',', ';'))
        {
            string trimmed = part.Trim();
            if (trimmed.Length > 0)
                yield return trimmed;
        }
    }

    // Public API for other mods (server / offline only when connected to a remote server)
    public static void AddVIP(string nameOrId)
    {
        if (!CanMutateVipListAtRuntime())
            return;
        if (!string.IsNullOrWhiteSpace(nameOrId))
            apiVipList.Add(nameOrId.Trim());
    }

    public static void AddVIP(List<string> list)
    {
        if (!CanMutateVipListAtRuntime())
            return;

        foreach (string s in list)
        {
            if (!string.IsNullOrWhiteSpace(s))
                apiVipList.Add(s.Trim());
        }
    }

    public static void RemoveVIP(string nameOrId)
    {
        if (!CanMutateVipListAtRuntime())
            return;
        if (!string.IsNullOrEmpty(nameOrId))
            apiVipList.Remove(nameOrId.Trim());
    }

    public static IEnumerable<string> GetVIPs()
    {
        var merged = new HashSet<string>(configVipList, StringComparer.OrdinalIgnoreCase);
        foreach (string entry in apiVipList)
            merged.Add(entry);
        return merged;
    }

    /// <summary>
    /// Server/host/offline only: replace VIP entries from an external source and persist to synced
    /// <see cref="RenameitConfig.VipList"/> (visible in cfg). Prefer
    /// <see cref="ApplyVipListFromExternalInMemoryOnly"/> when hiding names from config.
    /// </summary>
    public static void ApplyVipListFromExternal(IEnumerable<string> entries)
    {
        if (!CanMutateVipListAtRuntime() || entries == null)
            return;

        var names = new List<string>();
        foreach (string entry in entries)
        {
            if (!string.IsNullOrWhiteSpace(entry))
                names.Add(entry.Trim());
        }

        if (names.Count == 0)
            return;

        RenameitConfig.SetSyncedVipList(string.Join(", ", names));
    }

    /// <summary>
    /// Server/host/offline only: replace VIP list API entries in memory (does not update synced <see cref="RenameitConfig.VipList"/> cfg).
    /// Use <see cref="ReceiveApiVipListFromHost"/> on clients when the server pushes the same list over the network.
    /// </summary>
    public static void ApplyVipListFromExternalInMemoryOnly(IEnumerable<string> entries)
    {
        if (!CanMutateVipListAtRuntime() || entries == null)
            return;

        ReplaceApiVipList(entries);
    }

    /// <summary>
    /// Apply host-authoritative API VIP names on this machine (clients receive via VRP/RPC; not written to cfg).
    /// </summary>
    public static void ReceiveApiVipListFromHost(IEnumerable<string>? entries)
    {
        if (entries == null)
            return;

        _apiListFromHost = true;
        ReplaceApiVipList(entries);
    }

    static void ReplaceApiVipList(IEnumerable<string> entries)
    {
        apiVipList.Clear();
        foreach (string entry in entries)
        {
            if (!string.IsNullOrWhiteSpace(entry))
                apiVipList.Add(entry.Trim());
        }
    }

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

    private static IEnumerable<string> GetVipIdentityKeys(Player player)
    {
        string name = player.GetPlayerName();
        if (!string.IsNullOrEmpty(name))
            yield return name;

        yield return player.GetPlayerID().ToString();

        string? hostId = TryGetPeerHostId(player);
        if (!string.IsNullOrEmpty(hostId))
            yield return hostId;
    }

    private static string? TryGetPeerHostId(Player player)
    {
        if (ZNet.instance == null)
            return null;

        try
        {
            ZDOID zid = player.GetZDOID();
            foreach (ZNetPeer peer in ZNet.instance.GetPeers())
            {
                if (!peer.IsReady() || peer.m_characterID != zid)
                    continue;

                string? hostId = peer.m_socket?.GetHostName();
                return string.IsNullOrEmpty(hostId) ? null : hostId;
            }
        }
        catch
        {
            // ignored
        }

        return null;
    }

    /// <summary>Runtime VIP API is server-authoritative; clients use synced VipList from the host.</summary>
    private static bool CanMutateVipListAtRuntime()
    {
        if (ZNet.instance == null)
            return true;

        if (ZNet.GetConnectionStatus() == ZNet.ConnectionStatus.None)
            return true;

        return ZNet.instance.IsServer();
    }

    /// <summary>
    /// Dedicated / server: resolve peer by character and test adminlist using <see cref="ISocket.GetHostName"/> (same as vanilla adminlist IDs).
    /// </summary>
    private static bool IsValheimAdminForRemotePlayer(Player player)
    {
        try
        {
            object? adminList = AdminListField?.GetValue(ZNet.instance!);
            if (adminList == null)
                return false;

            string? hostId = TryGetPeerHostId(player);
            if (string.IsNullOrEmpty(hostId))
                return false;

            return ListContainsIdMethod?.Invoke(ZNet.instance, new object[] { adminList, hostId! }) is true;
        }
        catch
        {
            return false;
        }
    }
}
