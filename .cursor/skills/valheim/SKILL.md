---
name: valheim
description: >-
  Valheim modding with BepInEx, Harmony, Jotunn, and publicized game assemblies.
  Use when editing Valheim mods, networking (ZNet), or admin/player identity.
---

# Valheim modding (concise)

## Stack

- **BepInEx** plugin entry (`BaseUnityPlugin`), `Harmony` patches on game types.
- **Jotunn** is often a dependency; use `Jotunn.Managers` APIs where they wrap vanilla correctly.
- Reference **`assembly_valheim`** (often **publicized** for private members). Paths come from `environment.props` / game install.

## Admin detection (important)

- Server **`adminlist.txt`** entries are **platform/socket IDs** (e.g. Steam64 as string), **not** in-game character display names.
- **`Player.GetPlayerName()`** must **not** be passed to admin-list checks that expect those IDs.
- **Jotunn** `SynchronizationManager.Instance.PlayerIsAdmin` is the intended **client-side** flag for whether **the local player** is admin on the current world (synced from server; correct for dedicated + local).
- For **remote** `Player` instances on the server, resolve the matching **`ZNetPeer`** (e.g. by `m_characterID` vs `player.GetZDOID()`), then use **`peer.m_socket.GetHostName()`** with **`ZNet.instance.ListContainsId(ZNet.instance.m_adminList, id)`** (or equivalent vanilla API), matching how Jotunn builds admin sync.

## Patches

- Use `[HarmonyPatch]` with explicit type/method names; set **`HarmonyPriority`** when order vs other mods matters (e.g. run **last** on shared postfix).

## Networking

- **`ZNet.instance`** may be null off-server or during menus; guard calls.
- Peers: **`ZNet.instance.GetPeers()`** / **`m_peers`**; IDs on sockets differ from character names.
