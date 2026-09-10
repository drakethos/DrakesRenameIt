# DrakesRenameIt 1.1.0 — Regression Plan (WorkshopLibs port)

**Branch:** `customize-split` (suite monorepo + `DrakeModsLibs`)  
**Baseline QA:** [Drake's RenameIt Test.xlsx](https://github.com/drakethos/DrakesRenameIt) — v1.0.0b4 exploration (54 TCs, 53 pass / 1 open)  
**Release delta:** CHANGELOG 1.1.0 — *Migrated shared code to Libs.*  
**Full human checklist (Valheim 1.0 + Libs):** [Valheim-1.0-Manual-Regression-Checklist.md](../../docs/Valheim-1.0-Manual-Regression-Checklist.md)

This plan extends the v1.0 matrix with **port-specific** checks. Anything that previously lived in RenameIt Harmony/display/stack code now runs in **DrakeModsLibs**; RenameIt keeps UI, permissions, config, unlock cost, and inventory menu wiring.

---

## 1. Architectural change (what moved)

| Area | Was in RenameIt (<=1.0) | Now in DrakeModsLibs | RenameIt 1.1 still owns |
|------|------------------------|---------------------------|-------------------------|
| Display / hover / tooltips | `Patches.cs`, `DecoratedNamePatches.cs`, `ItemTooltipPatches.cs` | `ItemDisplayPatches.cs`, `ItemTooltipPatches.cs`, `DecoratedNamePatches.cs`, `HoverRenameHelper` | — |
| Stack merge / fingerprint | `InventoryStackPatches.cs`, `StackIdentity.cs` | `InventoryStackPatches.cs`, `StackIdentity.cs` | Policy via `RenameItStackMergePolicy` |
| Custom data keys | Local constants | `DrakeCustomDataKeys` (shared) | Aliases on `DrakeRenameit` for API compat |
| Durability name prefix | In display patches | `IDisplayNameModifier` hub | `RenameItDurabilityDisplayModifier` registration |
| ServerSync | Embedded in RenameIt DLL | ILRepack in **Libs only** | `DrakeConfigSync` via `CustomizeLibsAPI` |
| Menu modifier (Shift+RClick) | `MenuKeyBinding.cs` | `MenuBindingRegistry` | `RenameItLibsBridge` + `RenameItInventoryPatches` |
| Edit permission gate | Inline in patches | `CustomizationGatekeeper` + validators | `RegisterPermissionValidators()` |
| Item stand ward name | In `Patches.cs` | `CustomizeLibsRuntime.ShowItemStandItemNameWhenNoAccess` | Config pushes value on register |

**Build regression (CI / local):**

- [ ] `DrakesRenameit.dll` passes `Verify-NoServerSyncInModDll.ps1` (no ServerSync IL in consumer).
- [ ] Thunderstore profile loads **DrakeModsLibs** + **DrakesRenameit** (dependency `DrakeMods-DrakeModsLibs-0.3.0+`).
- [ ] Upgrade from 1.0 cfg: legacy section names migrate; 31 synced entries still audit-clean at startup.

---

## 2. Risk-ranked port regression areas

### P0 — Must pass before 1.1 publish (display & data path)

Libs now owns all player-visible name/description/crafted-by rendering. Re-run **all Group 1** and **SM-001–SM-008** from the spreadsheet.

| ID | Focus | Port risk |
|----|--------|-----------|
| TC-001 | Unlock + name + description + crafted-by apply & persist | Custom data write path must still hit same ZDO keys via Libs API |
| TC-002 | Upgrade retains all Drake fields | `ItemDisplayService` / upgrade hooks unchanged semantically |
| TC-006–TC-008 | SeparateStacks merge rules | Policy registered in `RenameItLibsBridge`; patches in Libs |
| TC-029–TC-030 | Item stand hover + ward visibility | `SetShowItemStandItemNameWhenNoAccess` timing vs config load |
| TC-036 | Durability modifier labels | Modifier hub order / registration once at Awake |
| TC-010 | Claim-via-rename + unlock consistency | Permission validators still wired to RenameIt manager |
| SM-001–SM-008 | Quick smoke | Catches Libs load order / missing dependency |

**Extra P0 port checks (not in v1.0 sheet):**

- [ ] **Libs-only install failure:** Remove Libs → RenameIt logs hard dependency error, no null-ref spam.
- [ ] **Libs + RenameIt load order:** Libs Awake before RenameIt; log shows Libs display patches then RenameIt bridge register.
- [ ] **Cross-mod API:** `GetPropperName` / `GetDisplayNameForUi` / `CustomizeLibsAPI.SetCustomName` behave identically for dependent mods (ItemShop display layer, etc.).

### P1 — Config, permissions, multiplayer sync

v1.0.0b4 scope (sheet2 changelog) — ServerSync now in Libs; behavior must match 1.0.

| ID | Focus | Port risk |
|----|--------|-----------|
| TC-011–TC-015 | SeparateStacks / HardLock matrix | Host cfg sync + Libs stack patches |
| TC-019–TC-025 | Ownership, resource rename, feature toggles | Validators + gatekeeper |
| TC-026–TC-027 | VIP / admin bypass | `VipList` live reload via `DrakeConfigSync` |
| TC-016–TC-018 | Exclusions + allowlist | Unchanged logic; ensure denial UI still in RenameIt |
| TC-038–TC-042 | Menu keys (local + server default) | `MenuBindingRegistry` vs old `MenuKeyBinding` |
| TC-046 | Localization EN/ES | RenameIt-only assets; Libs strings N/A |

**P1 multiplayer session (host + 1 client):**

- [ ] Client cannot change locked synced cfg (sections 01–09) when `LockSyncedConfig=true`.
- [ ] Host changes `VipList` -> client VIP bypass updates without relog (SettingChanged).
- [ ] Client empty `MenuOpenModifier` follows host `ServerDefaultMenuOpenModifier`.
- [ ] Client non-empty `MenuOpenModifier` overrides server default on that machine only.

### P2 — UI / edge cases / integrations

| ID | Focus | Notes |
|----|--------|-------|
| TC-003–TC-005 | Character limits | RenameIt UI |
| TC-028 | Rich text color/size | Libs `TooltipRichText` |
| TC-037 | ExcludeStacks | Permission + display |
| TC-043–TC-045 | Cancel / reset / accidental close | RenameIt UI (ISSUE-011 area) |
| TC-031–TC-035 | Third-party mods | Infinity Hammer, Zen stands, Plan Build, etc. |
| TC-032 | Plan Build duplicate | ISSUE-003 — known; confirm not worse after port |

**Open issues from QA tracker — explicit retest:**

| Issue | Retest for 1.1 |
|-------|----------------|
| ISSUE-011 | Reset on textbox instant apply (v1.0.0b4) |
| ISSUE-003 | Plan Build clone loses custom data |
| ISSUE-002 | Formatting tag edge cases |

---

## 3. Libs-only regression (WorkshopLibs 0.3.0+)

Run with **minimal** profile: BepInEx + Jotunn + DrakeModsLibs + DrakesRenameit.

| Check | Expected |
|-------|----------|
| Item tooltip crafted-by override | Custom line label + display name render |
| Inventory grid tooltip postfix | Custom name in grid hover |
| Auto-pickup stack split | Fingerprint includes all four Drake keys |
| Drag-merge blocked | When HardLock on and fingerprints differ |
| Drop HUD message capture | No regression in pickup messaging (Libs patch) |
| `CustomizeLibsAPI.CanPerform` | Honors RenameIt-registered validators |

---

## 4. Suggested execution order (1.1 sign-off)

1. **Build / deploy** — Package RenameIt 1.1.0 + Libs 0.3.0; verify no ServerSync in RenameIt DLL.  
2. **SM-001–SM-009** — 15 min solo smoke.  
3. **Group 1 (TC-001–010, 029–030, 036–037)** — Core + stands + durability.  
4. **Group 2–4 (TC-011–027)** — Config matrix (use sheet11 preset as baseline).  
5. **Group 5 (TC-003–005, 028, 038–046)** — UI/input/localization.  
6. **Group 6 (TC-031–035)** — Mod integrations.  
7. **Multiplayer P1 session** — Sync + VIP + menu default.  
8. **Consumer mod spot-check** — ItemShop / ReskinIt / QuestItems with Libs present (display layers only).

**Exit criteria:** All P0 pass; no new P0/P1 vs 1.0.0b4; ISSUE-011 disposition documented; Thunderstore dependency chain documented in README.

---

## 5. Traceability to QA workbook

| Workbook sheet | Version | Use for 1.1 |
|----------------|---------|-------------|
| sheet6 | v1.0.0b1 groups | Master TC list — **re-execute all groups** |
| sheet7–sheet8 | v1.0.0b4 steps | Detailed steps / expected results |
| sheet10–sheet9 | Exploration duplicates | Same TCs; pick one sheet as record |
| sheet11 | Config preset | Default host cfg for matrix tests |
| sheet3 | Issue log | Carry forward open issues + port column |

**Record results:** Add workbook row `v1.1.0 | Libs port regression | 54+ | | |` and link to this plan.

---

## 6. Changelog blurb (for 1.1 release notes)

> QA: Full v1.0 regression matrix re-run against DrakeModsLibs 0.3.0. Display, tooltip, stack-merge, and ServerSync paths verified in Libs; RenameIt retains UI, permissions, and config. See `docs/RenameIt-1.1-Regression-Plan.md`.

---

*Generated from branch analysis + Drake's RenameIt Test.xlsx (May 2026).*
