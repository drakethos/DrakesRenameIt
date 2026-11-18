---
name: drakes-renameit
description: >-
  DrakeRenameIt (DrakesRenameit) Valheim mod: rename/description custom data,
  permissions, VIP API, and Harmony patches. Use when editing this repository.
---

# DrakeRenameIt

## Layout

C# lives under `src/`. The project root is the package: csproj, README, `docs/`, `Tests/`, `Assets/`, `build/`. `Assets/Localization` is the only Assets tree copied into `plugins/` in the Thunderstore zip. `Assets/Forge` is the paper authoring pack and stays out of the zip; the sheets ship inside the DLL.

- `src/Patches/Items`, `Pieces`, `Animals`, `Map` — one folder per thing the player can name. `src/Patches/Hover` is the world crosshair patch.
- `src/Paper` — the paper item (gameplay, generated looks, stand patches). Not a rename target.
- `src/Config`, `src/Core` — settings and shared helpers (saved names, unlock cost, durability prefix).
- `src/UI`, `src/Compat`, `src/Permissions`, `src/API` — panels, other mods, who may rename, public events.

Namespaces were not renamed with the folders.

## Mod identity

- **BepInEx** plugin: `DrakeRenameit` in `src/DrakeRenameit.cs`, Harmony id `drakesmod.DrakeRenameit`.
- Depends on **Jotunn** and **DrakeModsLibs** (`[BepInDependency]` + project reference). Config sync via **`DrakeConfigSync`** from Libs (no direct ServerSync reference).
- **`RenameitConfig`** uses **`DrakeConfigSync.BindSynced`** / **`BindClientOnly`** / **`AddLockingConfigEntry`** / **`FinalizeBinding`** (31 synced entries in sections 01–09).
- **`LockSyncedConfig`** (default true) locks **all** sections 01–09 for non–Valheim-admins; only **10 UI-NotSynced** (`MenuHintColor`, `MenuOpenModifier`) are client-only. **`ServerDefaultMenuOpenModifier`** (General, synced, default `Shift`) applies when local **`MenuOpenModifier`** is empty; non-empty local always overrides.
- **`RenameitPermission.WireVipListSync`** reloads VIP hash from **`VipList`** on **`SettingChanged`** / **`SourceOfTruthChanged`** (not one-shot at Awake). Other rules read **`RenameitConfig.*`** live at use time.

## Permissions (`src/API/RenameItPermision.cs`)

- **`RenameitConfig.AllowAdminOverride`** must be **true** for any admin/VIP bypass paths.
- **`IsModVip`** — VIP list only (name, `GetPlayerID()`, peer host id). Does **not** grant Valheim admin.
- **`IsAdminOrVIP` / `IsElevatedForOverrides`**  
  - **Valheim admin**: `IsValheimAdmin` / `IsAdminSafe` — Jotunn **`PlayerIsAdmin`** (local) or **`m_adminList`** + peer **`GetHostName()`** (remote).  
  - **VIP**: synced **`VipList`** + server-only runtime **`AddVIP` / `RemoveVIP`** when connected to a remote host.
- Admin **or** VIP is enough unless **`VipOnlyOverride`**; do not conflate VIP with Valheim admin.
- **`RenamePermissionManager`** gates **`RenamePermissionOperation.EditCraftedByLabel`** with **`CraftedByLabelEnabled`** (General section). **Tooltip line label** picker uses **`CraftedByLabelCustomizable`** OR **`RenameitPermission.IsElevatedForOverrides`** (same elevated rule as elsewhere).

## Exclusions (`RenameitConfig`, `RenameExclusionRules.cs`)

- **`ExcludedNames`**: comma-separated `m_shared.m_name` ids blocked for non-admins.
- **`ExcludedCategory`**: comma-separated `Skills.SkillType` and/or `ItemDrop.ItemData.ItemType` names, plus aliases (Armor, Weapons, Tools, Ranged, Melee, Shields, Ammo, Fish).
- **`ExcludeStacks`**: when on, vanilla stackable items (`m_maxStackSize > 1`) blocked for non-elevated rename/description/crafted-by (separate from **SeparateStacks** merge rules).
- **`RenameAllowlist`**: comma-separated `m_shared.m_name` ids that bypass name/category exclusions, **ExcludeStacks**, and the uncrafted/resource block (not global rename disable or ownership).
- Admins/VIP still bypass exclusions when **`AllowAdminOverride`** is on.

## Custom data keys (`DrakeRenameit.cs` constants)

- Names: **`DrakeNewName`** (`Drake_Rename`).
- Descriptions: **`DrakeNewDesc`** (`Drake_Rename_Desc`).
- Crafted-by **display name** (tooltip only; real crafter id unchanged): **`DrakeCraftedByDisplay`** (`Drake_CraftedByDisplay`).
- Crafted-by **line prefix** (text before `: name`, e.g. “Belongs To”): **`DrakeCraftedByLineLabel`** (`Drake_CraftedByLineLabel`). Empty / absent means use vanilla localized **`$item_crafter`** line.
- Unlock flag: **`DrakeRenameUnlocked`** (`Drake_RenameUnlocked`).

## Unlock cost (`RenameUnlockCost`, `Drake_RenameUnlocked`)

- When **`UnlockCostEnabled`** + valid **`UnlockCost`** apply, **`IsRenameUnlocked`** normally requires the paid flag on the stack.
- **Grandfather rule**: if the gate applies but the stack has **any** Drake customization (**`HasAnyDrakeRenameCustomization`**: custom name, desc, crafted-by display, or crafted-by line label) and **no** unlock flag yet, **`IsRenameUnlocked`** writes **`Drake_RenameUnlocked`** once and returns true (items edited before unlock existed or after config churn). Unlock is **per stack** (item custom data on that stack), not per player.
- **Tooltip suffix** (`GetMenuTooltipLockSuffix`): **🔒** (`TooltipUnlockCostLockedEmoji`) until paid; **🖊️** (`TooltipDrakeEditableEmoji`, U+1F58A) when paid-unlocked or elevated (open-lock emoji looked too similar to closed-lock in-game).

## Config sections (high level)

- **General**: rename/desc/crafted-by feature toggles, stacks, unlock cost, etc. **`CraftedByLabelEnabled`** — players may edit crafted-by **display** at all. **`ShowDenialUi`** / **`ShowReason`** — blocked-item UI on/off vs. detailed denial text (ShowReason only matters when ShowDenialUi is on).
- **`CraftedBy`** (synced): **`LabelCustomizable`** — non-elevated players may pick from **`AllowedLabels`**; if false, picker is greyed but **elevated** users still can. **`AllowedLabels`** — comma/semicolon list; **first entry** is the UI “default” row (clears **`DrakeCraftedByLineLabel`**; tooltip uses game localization for the line). Further entries are stored verbatim on the item when chosen.
- **`RenameitConfig.GetCraftedByAllowedLabelsList()`** parses **`AllowedLabels`** (dedupe, trim).

## Crafted-by UI and apply path

- **`UI/UIPanels.cs`**: crafted-by wood-panel; **“Tooltip line”** row opens a small list (buttons) to set pending line label; **`CraftedByLineLabelPendingToken`** (internal) is read on OK.
- **`DrakeRenameit.OpenCraftedByEditor`** calls **`UIPanels.RefreshCraftedByLineLabelPicker(item)`** after setting the name field.
- **`DrakeRenameit.ApplyCraftedByLabel`**: applies display +, when allowed, line label; validates custom line with **`IsAllowedCustomCraftedByLineLabel`** (must match an entry after index 0).

## Tooltips (`Patches/ItemTooltipPatches.cs`)

- **`ApplyCraftedByDisplayToTooltipText`**: runs when item has **`DrakeCraftedByDisplay`** and/or **`DrakeCraftedByLineLabel`** (and has crafter); display part defaults to **`m_crafterName`** if only line label is set.
- **`ReplaceCraftedBySegment`**: optional **`lineLabelOverride`**; **`TryReplaceCraftedByWithCustomLineLabel`** handles token + localized + regex + line-scan paths (mirrors vanilla label matching, outputs custom prefix).
- **`src/UI/TooltipRichText.cs`**: **`EnsureRichTextTagsClosedForTooltip`** auto-closes unmatched **`</color>`** and **`</size>`** (LIFO). Crafted-by display: **`WrapCraftedByDisplayWithDefaultStatColorIfNeeded`** wraps text with no `<color` / `<#hex>` markup in **`GUIManager.ValheimOrange`** (fallback **`#ff8800`**). Order: ensure → wrap. Rename/description tooltips use **`EnsureRichTextTagsClosedForTooltip`** only (`Patches.cs`).

## Separate stacks (`RenameitConfig.SeparateStacks`, `SeparateStacksHardLock`)

- **`InventoryStackPatches`**: `Inventory.AddItem(ItemData)` tracks incoming item for `FindFreeStackItem`; `AddItemAtCell` blocks mismatched Drake fingerprints when splits are enforced.
- **`StackIdentity.GetFingerprint`**: includes **`DrakeNewName`**, **`DrakeNewDesc`**, **`DrakeCraftedByDisplay`**, **`DrakeCraftedByLineLabel`**.
- **`SeparateStacksHardLock`**: when `SeparateStacks` is on, `true` blocks manual drag-merges; `false` allows vanilla **`AddItemAtCell`** in one call (no dialog) so mismatched stacks can be combined by drag.

## Reset / API

- **`CanResetAnyCustomization`** / **`ResetAllCustomizations`**: crafted-by reset covers **display** and **line label** when permitted.
- **`src/API/RenameEvents.cs`**: **`OnCraftedByDisplayChanged`** fires on display changes (line label changes piggyback the same apply flow from the editor).

## Patches

- **`src/Patches/Items/RenameItInventoryPatches.cs`**: inventory right-click rename and crafting grid tooltip.
- **`src/Patches/Pieces`**: placed-piece names (`PieceRename`) and the LockSmith Name tab (`PieceNameTab`).
- **`src/Patches/Animals/AnimalRename.cs`**: tamed-animal names.
- **`src/Patches/Map`**: pin editor (`PinEdit`) and saved pin colours (`PinStyles`).
- **`src/Patches/Hover/WorldHoverPatches.cs`**: world crosshair.
- **`src/Paper/Patches/PaperItemStandPatches.cs`**: paper on item stands.

## Build / deploy

- **`DrakeRenameit.csproj`**: references **DrakeModsLibs** (Thunderstore `DrakeMods-DrakeModsLibs-0.9.1+`); no ILRepack or ServerSync in this project. **`environment.props`** sets BepInEx / Valheim paths for local builds.
