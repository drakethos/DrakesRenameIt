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
- **`UI/TooltipRichText.cs`**: **`EnsureRichTextTagsClosedForTooltip`** auto-closes unmatched **`</color>`** and **`</size>`** (LIFO). Crafted-by display: **`WrapCraftedByDisplayWithDefaultStatColorIfNeeded`** wraps text with no `<color` / `<#hex>` markup in **`GUIManager.ValheimOrange`** (fallback **`#ff8800`**). Order: ensure → wrap. Rename/description tooltips use **`EnsureRichTextTagsClosedForTooltip`** only (`Patches.cs`).

## Separate stacks (`RenameitConfig.SeparateStacks`, `SeparateStacksHardLock`)

- **`InventoryStackPatches`**: `Inventory.AddItem(ItemData)` tracks incoming item for `FindFreeStackItem`; `AddItemAtCell` blocks mismatched Drake fingerprints when splits are enforced.
- **`StackIdentity.GetFingerprint`**: includes **`DrakeNewName`**, **`DrakeNewDesc`**, **`DrakeCraftedByDisplay`**, **`DrakeCraftedByLineLabel`**.
- **`SeparateStacksHardLock`**: when `SeparateStacks` is on, `true` blocks manual drag-merges; `false` allows vanilla **`AddItemAtCell`** in one call (no dialog) so mismatched stacks can be combined by drag.

## Reset / API

- **`CanResetAnyCustomization`** / **`ResetAllCustomizations`**: crafted-by reset covers **display** and **line label** when permitted.
- **`API/RenameEvents.cs`**: **`OnCraftedByDisplayChanged`** fires on display changes (line label changes piggyback the same apply flow from the editor).

## Patches

- Main file: **`Patches/Patches.cs`** — `ItemDrop` hover, `InventoryGui`, `InventoryGrid` tooltip, **`ItemStand`** (UseItem, SetVisualItem, GetHoverText for container stands).
- **`ItemTooltipPatches`**: Harmony postfix on **`ItemDrop.ItemData.GetTooltip`** (resolved in **`Apply`**).

## Build / deploy

- **`DrakeRenameit.csproj`**: ILRepack merges **ServerSync**; output DLL is the merged plugin. **`environment.props`** sets BepInEx / Valheim paths for local builds.
