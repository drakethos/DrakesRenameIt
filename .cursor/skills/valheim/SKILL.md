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
- **Method drift**: if `AccessTools.Method`/`DeclaredMethod` fails across game versions, scan `GetMethods` for matching name + parameter signature (e.g. static `GetTooltip(ItemData, int, bool)` on `ItemDrop.ItemData`).

## Networking

- **`ZNet.instance`** may be null off-server or during menus; guard calls.
- Peers: **`ZNet.instance.GetPeers()`** / **`m_peers`**; IDs on sockets differ from character names.

## Item tooltips and crafted-by line

- Vanilla builds the crafter segment as **`\n$item_crafter: {m_crafterName}`** in the tooltip pipeline, then **localizes** `$item_crafter` to the visible label (e.g. “Crafted by”).
- The UI often wraps **`m_crafterName`** in **`<color=…>`** tags, so a naive `string.Replace(oldName, newName)` on the final tooltip string **misses** matches.
- The localized label comes from **`Localization.instance.Localize("$item_crafter")`** (guard for null `Localization.instance`).
- Postfixes on **`ItemDrop.ItemData.GetTooltip`** (static overload with `ItemData, int, bool` when present) are a common place to rewrite the result string while preserving color and locale behavior.
- Injected TMP-style rich text should auto-close **`<color>`** / **`<#RRGGBB>`** and **`<size>`** so tags do not bleed into the next line; use a small stack (LIFO) to append **`</size>`** / **`</color>`** as needed. Stat-value orange in mod UI often matches **`GUIManager.Instance.ValheimOrange`** → **`ColorUtility.ToHtmlStringRGB`** for `<color=#…>` tags.

## Per-item persistent data

- **`ItemDrop.ItemData.m_customData`** is a `Dictionary<string, string>` suitable for mod keys (rename text, display overrides, stack identity). Treat keys as a stable contract (constants on plugin class).
- Anything that changes **merge/stack** behavior should be folded into a **fingerprint** compared in **`Inventory.AddItem` / `FindFreeStackItem`**-style logic if the mod enforces separate stacks.

## Jotunn UI (`GUIManager`)

- **`GUIManager.Instance`**: **`CreateWoodpanel`**, **`CreateButton`**, **`CreateInputField`**, **`CreateText`** under **`GUIManager.CustomGUIFront`** for overlay UI.
- **`GUIManager.BlockInput(true/false)`**: pair carefully so the game does not stay input-locked after closing panels.
- **Dropdowns**: Jotunn exposes **`GUIManager.ApplyDropdownStyle(Dropdown, …)`** for styled **`UnityEngine.UI.Dropdown`**. Building a full Dropdown template in code is heavy; a **button that toggles a small wood-panel list of buttons** is a practical pattern for short option lists.

## Server-authoritative config (ServerSync pattern)

- Use **`DrakeConfigSync`** from **DrakesWorkshopLibs** (`CustomizeLibsAPI.CreateConfigSync`): **`BindSynced`**, **`AddLockingConfigEntry`**, **`FinalizeBinding`**. Only Libs embeds ServerSync. Keep client-only keys via **`BindClientOnly`**.
- Helper **`Bind(section, key, default, desc)`** + **`configSync.AddConfigEntry(entry)`** reduces duplication.
