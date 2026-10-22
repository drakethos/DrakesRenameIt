# RenameIt test coverage

How "Drake's RenameIt Test Suite 2.0" (the spreadsheet) maps onto the DrakesTestLab suites in `Tests/suites/`.

- **Auto** = `DrakesRenameIt` suite, runs unattended (`Run-TestLabSuites.ps1 -Suites DrakesRenameIt`).
- **Manual** = `DrakesRenameIt-Manual` suite: it sets the scene, then asks you Pass / Fail (run it from F9 > Tests).
- **Gap** = not covered by either yet.

Owned items are always **crafted** through the game's crafting code (`craft` step, `"by": "Alice"` for someone else), so ownership is the real thing. Resources (wood, mushrooms) and loot are given unowned, like picking them up.

Other players are simulated with play-as (Alice, Bob, Nacho-VIP): the same character switches identity (player id and name), so ownership, Shared, VIP and ward rules are checked as another person. What that cannot show: another real client's view, server sync, and network timing.

## Smoke

| Sheet | What | Coverage | Test |
|---|---|---|---|
| SM-001 | Open rename UI | Auto (menu would open; crafted owner) + Manual (real Shift+RMB, window) | SM-001, M-SM-001 |
| SM-002..004 | Name / description / crafted-by | Auto | SM-002 |
| SM-005 | Upgrade keeps changes | Auto (game upgrade code) | SM-005 |
| SM-006 | Item stand | Auto (hover text) | SM-006 |
| SM-007 | Split, rename, drag back | Auto | SM-007 |
| SM-008 | Config / data after relaunch | Partly auto (survives drop + pickup, X-05) + Manual (relaunch) | X-05, M-SM-008 |
| SM-009 | VIP bypass | Auto (play-as Nacho-VIP) | SM-009 |
| SM-010 | Craft paper | Auto (real craft with Wood + Coal) + Manual (crafting list) | SM-010, M-SM-010 |
| SM-011 | Paper modify | Auto | SM-011 |
| SM-012 | Paper placement | Auto (pinned flat on the ground through place mode) + Manual (wall, by hand) | SM-012, M-SM-012 |
| SM-013 | Copy from wall | Auto | SM-012 |
| SM-014 | Grab from wall | Auto | SM-012 |
| SM-015 | Localization | Partly auto (names follow config / English text) + Manual (language switch) | SM-015, M-SM-015 |

## Core

| Sheet | What | Coverage | Test |
|---|---|---|---|
| TC-101 | Unlock cost, edit all | Auto (incl. not enough coins, no double pay) | TC-101 |
| TC-102 | Upgrade keeps all three | Auto | TC-102 |
| TC-103 | Item stand visuals | Auto (hover + taking it back) | TC-103 |
| TC-104 | Reset buttons | Auto (each field + Reset All) | TC-104 |
| TC-105 | Paper basics | Auto + Manual (landscape text looks right) | TC-105, M-TC-105 |
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
| TC-214 | Keybinds | Partly auto (which keys apply) + Manual (pressing them) | TC-214, M-TC-214 |
| TC-215 | Denial UI | Auto (text, reasons, center message) | TC-215 |
| TC-216 | Hidden UI setting | Auto, **assumed** to mean ShowItemStandItemNameWhenNoAccess (sheet row 48): confirm | TC-216, TC-110 |
| TC-217 | VIP bypass | Auto (VipOnly, list format, override off) | TC-217 |
| TC-218 | Written paper restrictions | Auto | TC-218 |

## Edge / UI

| Sheet | What | Coverage | Test |
|---|---|---|---|
| TC-301 | Character limits | Manual (the limit is enforced by the text field) | M-TC-301 |
| TC-302 | Colours / markdown | Auto (tags kept, unclosed tag closed) | TC-302 |
| TC-303 | Cancel / Esc | Manual | M-TC-303 |
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

Re-run 2026-10-22: 49/49.

## Known gaps (for a later round)

- **Real UI:** the Workshop window, Paper tab buttons, Quick Settings and hover-style options are only manual (M-HOVER). DrakesTestLab can click UI Toolkit buttons by name now, so these could be automated once the element names are stable.
- **Wall placement on an actual wall** (vertical): the automated pin lays the page flat on the ground. Vertical is manual.
- **Paper look settings** (PaperScale, PaperMeshLook, PaperShowPageText, PaperDefaultFontSize, PaperDefaultLandscape): screenshots only; applied at registration, so they need a world reload.
- **PaperEnabled / PaperCost / PaperCraftingStation live toggles:** the recipe is built at registration; SM-010 records the state after a live toggle but does not assert it.
- **LockSyncedConfig and ServerSync**: no dedicated server in the loop.
- **Language switch** and **relaunch persistence**: manual.
- **Compat modules** (LockSmith, wards from other mods): manual until those mods are in the test profile.
