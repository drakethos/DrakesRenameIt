---
name: drakes-renameit
description: >-
  DrakeRenameIt (DrakesRenameit) Valheim mod: rename/description custom data,
  permissions, VIP API, and Harmony patches. Use when editing this repository.
---

# DrakeRenameIt

## Mod identity

- **BepInEx** plugin: `DrakeRenameit` in `DrakeRenameit.cs`, Harmony id `drakesmod.DrakeRenameit`.
- Depends on **Jotunn** (and ServerSync for config). Uses **`RenameitConfig`** (synced entries via ServerSync).

## Permissions (`API/RenameItPermision.cs`)

- **`RenameitConfig.AllowAdminOverride`** must be **true** for any admin/VIP bypass paths.
- **`IsAdminOrVIP` / `IsAdminOrVIP(Player)`**  
  - **Valheim admin**: `IsAdminSafe` — local player uses **`Jotunn.Managers.SynchronizationManager.Instance.PlayerIsAdmin`**; remote players use **ZNet peer socket host id** + **`ListContainsId`** on **`m_adminList`** (not character name).  
  - **VIP**: config list + **`AddVIP` / `RemoveVIP` / `GetVIPs`** (name or `GetPlayerID().ToString()`).
- Admin **or** VIP is enough; do not break VIP when changing admin detection.

## Exclusions (`RenameitConfig`, `RenameExclusionRules.cs`)

- **`ExcludedNames`**: comma-separated `m_shared.m_name` ids blocked for non-admins.
- **`ExcludedCategory`**: comma-separated `Skills.SkillType` and/or `ItemDrop.ItemData.ItemType` names, plus aliases (Armor, Weapons, Tools, Ranged, Melee, Shields, Ammo, Fish).
- **`RenameAllowlist`**: comma-separated `m_shared.m_name` ids that bypass name/category exclusions and the uncrafted/resource block (not global rename disable or ownership).
- Admins/VIP still bypass exclusions when **`AllowAdminOverride`** is on.

## Custom data keys

- Names: `DrakeRenameit.DrakeNewName`; descriptions: `DrakeNewDesc` (see `DrakeRenameit.cs` constants).

## Patches

- Main file: **`Patches.cs`** — `ItemDrop` hover, `InventoryGui`, `InventoryGrid` tooltip, **`ItemStand`** (UseItem, SetVisualItem, GetHoverText for container stands).

## Build / deploy

- **`DrakeRenameit.csproj`**: ILRepack merges **ServerSync**; output DLL is the merged plugin. **`environment.props`** sets BepInEx / Valheim paths for local builds.
