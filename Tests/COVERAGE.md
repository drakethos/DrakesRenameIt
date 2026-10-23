# RenameIt test coverage

How "Drake's RenameIt Test Suite 2.0" (the spreadsheet) maps onto the DrakesTestLab suites in `Tests/suites/`.

- **Auto** = `DrakesRenameIt` suite, runs unattended (`Run-TestLabSuites.ps1 -Suites DrakesRenameIt`).
- **Manual** = `DrakesRenameIt-Manual` suite (3 tests: other mods and real multiplayer): it sets the scene, then asks you Pass / Fail. Run it with `Run-TestLabSuites.ps1 -Suites DrakesRenameIt-Manual -Attended` or from F9 > Tests.
- **Gap** = not covered by either yet.

Owned items are always **crafted** through the game's crafting code (`craft` step, `"by": "Alice"` for someone else), so ownership is the real thing. Resources (wood, mushrooms) and loot are given unowned, like picking them up.

Other players are simulated with play-as (Alice, Bob, Nacho-VIP): the same character switches identity (player id and name), so ownership, Shared, VIP and ward rules are checked as another person. What that cannot show: another real client's view, server sync, and network timing.

## Smoke

| Sheet | What | Coverage | Test |
|---|---|---|---|
| SM-001 | Open rename UI | Auto: real Shift + right-click on the slot opens the window; not for someone else's item | SM-001, SM-001-UI |
| SM-002..004 | Name / description / crafted-by | Auto | SM-002 |
| SM-005 | Upgrade keeps changes | Auto (game upgrade code) | SM-005 |
| SM-006 | Item stand | Auto (hover text) | SM-006 |
| SM-007 | Split, rename, drag back | Auto | SM-007 |
| SM-008 | Data after relaunch | Auto: log out and back in (relog step, no game restart): inventory item, item on a stand, pinned page | SM-008 |
| SM-009 | VIP bypass | Auto (play-as Nacho-VIP) | SM-009 |
| SM-010 | Craft paper | Auto: real craft with Wood + Coal, and Piece of Paper in the crafting list | SM-010, SM-010-UI |
| SM-011 | Paper modify | Auto | SM-011 |
| SM-012 | Paper placement | Auto: flat on the ground, and on a real wall aimed with the crosshair | SM-012, SM-012-UI |
| SM-013 | Copy from wall | Auto | SM-012 |
| SM-014 | Grab from wall | Auto | SM-012 |
| SM-015 | Localization | Auto: game switched to Spanish for the test (finding: paper text stays English) | SM-015, SM-015-UI |

## Core

| Sheet | What | Coverage | Test |
|---|---|---|---|
| TC-101 | Unlock cost, edit all | Auto (incl. not enough coins, no double pay) | TC-101 |
| TC-102 | Upgrade keeps all three | Auto | TC-102 |
| TC-103 | Item stand visuals | Auto (hover + taking it back) | TC-103 |
| TC-104 | Reset buttons | Auto (each field + Reset All) | TC-104 |
| TC-105 | Paper basics | Auto (incl. landscape + font 7 shows only the first lines) | TC-105, TC-105-UI |
| TC-106 | Copy and recycle | Auto (copies, printed stack, recycle rules, ISSUE-015/016/017) | TC-106, TC-106b |
| TC-107 | Shared paper | Auto (play-as Bob) | TC-107, TC-107b |
| TC-108 | Shared items | Auto (play-as Bob, via a stand) | TC-108 |
| TC-109 | Ownership with upgrade | Auto (unowned upgrade claims it; someone else's: recorded as informational) | TC-109 |
| TC-110 | Locked ward stand + public paper | Auto (Alice's ward, Bob looks and takes) | TC-110 |

## Config

| Sheet | What | Coverage | Test |
|---|---|---|---|
| TC-201 | Exclusions | Auto | TC-201 |
| TC-202..204 | LockToOwner / NameClaimsOwner | Auto | TC-202, TC-203, TC-204 |
| TC-205 | AllowRenameUnownedItems | Auto (incl. ISSUE-009, allowlist) | TC-205 |
| TC-206..208 | Features off | Auto (+ VIP still may, ISSUE-011) | TC-206, TC-207, TC-208, TC-208b |
| TC-209 | Durability labels | Auto (every band, ISSUE-008) | TC-209 |
| TC-210..212 | SeparateStacks / HardLock | Auto (drop + pickup, drag) | TC-210, TC-211, TC-212 |
| TC-213 | ExcludeStacks | Auto (+ allowlist) | TC-213 |
| TC-214 | Keybinds | Auto: real Shift / Ctrl / Ctrl+Alt + right-click against the configured key | TC-214, TC-214-UI |
| TC-215 | Denial UI | Auto (text, reasons, center message) | TC-215 |
| TC-216 | Hidden UI setting | Auto, **assumed** to mean ShowItemStandItemNameWhenNoAccess (sheet row 48): confirm | TC-216, TC-110 |
| TC-217 | VIP bypass | Auto (VipOnly, list format, override off) | TC-217 |
| TC-218 | Written paper restrictions | Auto | TC-218 |

## Edge / UI

| Sheet | What | Coverage | Test |
|---|---|---|---|
| TC-301 | Character limits | Auto: 16 characters typed into the real Name field, limit 10 | TC-301-UI |
| TC-302 | Colours / markdown | Auto (tags kept, unclosed tag closed) | TC-302 |
| TC-303 | Cancel / Esc | Auto: close button and Esc do not save, Save does | TC-303-UI |
| TC-304 | Grab paper, full inventory | Auto | TC-304 |
| TC-305 | Copy more than you can hold | Auto | TC-305 |

## Integration and multiplayer

| Sheet | What | Coverage |
|---|---|---|
| TC-401..408 | Other mods (VRP, Shrooms, RPChatter, PlanBuild, InfinityHammer, Ward is Love, Protective Wards, Arcane Wards) | Manual (M-TC-4xx). **Gap:** no automated suite until those mods are in a test profile; a suite with `requireMods` per mod would skip cleanly when they are missing. |
| TC-501 | Config on server lock / VIP list client-side (ISSUE-019, open) | Manual, needs a dedicated server. **Gap.** |
| TC-502..504 | Item info / paper / shared between real players | Rules covered by play-as (TC-107, TC-108, TC-110); real two-client view Manual (M-TC-502). |

## Not in the sheet, now covered

| Test | What |
|---|---|
| X-01 | Paper stack sizes, item type, hammer pieces placeable on/off |
| X-02 | Crafted-by line labels, AllowedLabels, LabelCustomizable |
| X-03 | PublicRewriteEnabled off disables Shared |
| X-04 | Unlock cost with a two-item list, paper never gated, invalid cost ignored |
| X-05 | Custom data survives dropping and picking up |

## Findings from the first automated runs (2026-10-22, RenameIt 1.3.0-beta.2)

| Test | Finding | Cause | Status |
|---|---|---|---|
| TC-216, TC-110 | Turning **ShowItemStandItemNameWhenNoAccess** off does not hide the item name on a stand for a player without ward access. | `RenameItLibsBridge.Register` copied the value into Libs once (`CustomizeLibsAPI.SetShowItemStandItemNameWhenNoAccess`) and nothing updated it on `SettingChanged`, so live toggles and server-synced values were ignored. | **Fixed** (uncommitted): `RenameitConfig` pushes the value to Libs on `SettingChanged` (ServerSync sets the entry, so synced values fire it too) and on `SourceOfTruthChanged`. Passes. |
| SM-015 | Changing **PaperName** live does not rename blank paper (still "Piece of Paper"). | Jotunn only copies its translations into the game when the language is set up; after that, the game's `Localization.m_translations` and its `m_cache` keep the old text. | **Fixed** (uncommitted): `PaperItem.AddLocalization` also writes the six paper tokens into `Localization.instance` (via reflection; the fields are private) and evicts its cache. Covers live and server-synced changes; PrintedPaperName/Description now refresh live too. Passes. |
| SM-015-UI | In Spanish, **Piece of Paper kept its English name and description** (the game's own text was translated). | `PaperItem.AddLocalization` wrote the config text (English defaults) into the Spanish translation and pushed English into the game's table whatever the language. | **Fixed** (uncommitted): while a paper text setting is still its default, Spanish uses shipped Spanish text ("Hoja de papel"...); a server's own text applies in every language; the live push uses the selected language. Passes. |
| TC-214-UI | With RenameIt's **MenuOpenModifier = Ctrl**, Shift + right-click still opened the window. | DrakeModsLibs wraps the inventory right-click with its own shared `InventoryOpenModifier` (default Shift) and opened the tab host, RenameIt's tab included, before RenameIt's handler checked its own key. | **Fixed** (uncommitted, needs the new Libs): new Libs API `MenuBindingRegistry.LinkTab`; RenameIt links its Rename and Paper tabs to its binding, so the shared modifier leaves them out unless RenameIt's key is held (or both use the same keys). Passes. |
| (info) | The Workshop window's titles (Name, Description, Crafted by, Save...) are English in every language. | Hard-coded strings in `UI/Toolkit/WorkshopPanel.cs`. | Open. |

Re-run 2026-10-22 (all suites via `scripts/Test-All.ps1`): 103/103 automated passing.

## Known gaps (for a later round)

- **Paper tab buttons and Quick Settings window**: not driven yet (the Workshop window and hover styles are, see the -UI tests).
- **Paper look settings** (PaperScale, PaperMeshLook, PaperShowPageText, PaperDefaultFontSize, PaperDefaultLandscape): screenshots only; applied at registration, so they need a world reload.
- **PaperEnabled / PaperCost / PaperCraftingStation live toggles:** the recipe is built at registration; SM-010 records the state after a live toggle but does not assert it.
- **LockSyncedConfig and ServerSync**: no dedicated server in the loop.
- **Compat modules** (LockSmith, wards from other mods): manual until those mods are in the test profile.
