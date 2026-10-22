# Rename It: pieces plan (1.3 or 2.0, undecided)

Maps the pieces handoff onto the current code. Nothing here is implemented yet.
The Valheim DLLs in `libs/` are links to the PC's Steam folder, so every "verify" item below still needs a decompile on the PC.

## Where pieces plug into the current code

| Handoff item | Existing code to extend | Gap |
|---|---|---|
| Name / description storage | `DrakeCustomDataKeys` (item custom data) | Pieces have no item custom data. Use ZDO strings (`zdo.Set(string, string)`) under the same key names, so they survive ownership handoffs. |
| Permission checks | `RenamePermissionManager`, `RenamePermissionOperation` | Add `RenamePieceName` and `RenamePieceDescription`. Pieces skip the item rules (exclusions, stacks, unlock cost). |
| Ward status line | `Compat/IAreaCompatModule`, `WardHoverAccess` | Reuse `QueryLocalAccess` for "You can build here · <ward>". Ward access must not override a placer lock. |
| Dialog | `UI/Toolkit/WorkshopPanel.cs`, `WorkshopPreview.cs`, Libs `DrakeTabHost` | Add a Piece card to the Rename tab and a piece target next to `ItemData`. `UI/UIPanels.cs` is the old IMGUI path. |
| Hover text | `UI/Toolkit/WorldHover.cs` (draws), `Patches/PaperItemStandPatches.cs`, `Compat/ItemLibs/ItemStandHoverModule.cs` | Postfix on each first-wave type's `GetHoverText`. Item stands already have a patch, so extend it and avoid a second path. |
| Config | `RenameitConfig` (synced via `DrakeConfigSync`) | New synced keys, see below. |
| Locksmith | soft dependency already declared in `DrakeRenameit.cs` | New bridge, see below. |

## Proposed ZDO keys and config

- Keys: `Drake_Rename` and `Drake_Rename_Desc` (same names as items), `Drake_PieceLocked` (placer lock), `Drake_PlacedBy` and `Drake_PlacedById` (our own creator record), `Drake_ShowPlacer`.
- Config (synced, new section "Pieces"): `PieceRenameEnabled`, `PieceLockRenameToPlacer` (server setting), `PieceAllowPlacerLock`.
- Permission order: feature toggle, then placer lock (placer only; ward access and admin/VIP do not override a placer lock unless you decide admin should, which is an open question), then "can build or remove" (Piece `CanBeRemoved` plus ward check).

## Placed-by

- Prefer vanilla `Piece.m_creator` (read via `Piece.GetCreator()`) when present.
- Also stamp our own `Drake_PlacedById` on first rename if no creator is set (pieces placed before the mod).
- Never use `ZDO.GetOwner()`.

## Build order

1. Piece target resolution: raycast hit to ZNetView, in-scope test (persistent, placed, has a hover name).
2. ZDO name read/write plus sync check (second client sees the name).
3. Hover patches, one type at a time: Container, Door, Bed, ItemStand/ArmorStand, Vagon. Shop pieces go through the same path.
4. Permission layer plus the three config keys.
5. Dialog Piece section: normal, locked-to-placer (amber, "Close" only), and Locksmith-not-trusted states.
6. Locksmith bridge: read lock state and trusted count for the status row; "use this name as the lock label" writes through Locksmith's API, so Rename It stays cosmetic.
7. Localization keys (English, Spanish) and the changelog.

## Version call

Pieces change the data model (ZDO instead of item data) and add a card to a dialog that is already redesigned for 1.3. Nothing existing breaks, so ship it as **1.3.x** (`feature/ui-toolkit` is already 1.3.0-beta.2). Choose 2.0 only if the piece target abstraction forces a breaking Libs API.

## The dialog is already the new one (branch `feature/ui-toolkit`, 1.3.0-beta.2)

The first version of this plan was written against `main` (1.2.1, old IMGUI wood panel). The new UI exists on `origin/feature/ui-toolkit`, 8 commits ahead of `main` and not merged. This branch now includes it. The README screenshots on Hexium/GitHub are still the old UI, which is why they did not match the mockup.

What the new UI has (from the user's screenshots and `UI/Toolkit/WorkshopPanel.cs`):
- Dark Toolkit panel: item icon, title, subtitle ("No crafter yet" / "Unsaved changes"), theme toggle, close button.
- Tab strip from `DrakeTabHost` (Libs): **Paper | Rename | Reskin**, only shown when more than one mod offers a tab for the item.
- Rename tab: Name with counter (formatting not counted) and per-field **Reset**, a rich-text bar (B, I, A-, A+, Clear, 10 colours and a custom picker, "Formatting: Name / Description / Crafted-by line" follows focus), Description (1000 chars), Crafted-by line (dropdown plus text), **Shared** switch, footer **Reset all**, "Enter save · Esc close", **Revert**, **Save**.
- Live preview card beside the panel (`WorkshopPreview.cs`), updating as you type.
- `WorldHover.cs` already draws hover text for the new UI (styles, "Minimal" default).

Consequences for pieces:
- The mockup is closer to this UI than to the old one, so the Piece section is a **card on the Rename tab**, in the same style as the **Shared** card. No 2.0-sized restyle is needed; this supports **1.3**.
- `WorkshopPanel.Show` and `DrakeTabHost.GetUsableTabs` are keyed on `ItemDrop.ItemData`. A chest or door has no `ItemData`, so pieces need a target abstraction (item or piece) in Libs and the panel. That is the largest structural change.
- Mockup items not in the shipped UI: footer **Cancel** (it has **Revert**), amber locked state, "Piece" subtitle. The Piece card and the locked state are new.
- The hover prompt for paper is "[Shift + E] Edit", so pieces reuse `MenuOpenModifier` (open item 6 resolved for paper).

## Open items (all need the PC decompile)

1. Done: dialog layout is the 1.3 Toolkit UI, see above.
2. `Piece.m_creator`, `SetCreator`, and whether `GetCreator` exists in the current Valheim build.
3. Hover-name methods on `Container`, `ItemStand`, `Sign`, `Bed`, `Vagon` (and `ArmorStand`, which is an `ItemStand`-like type; confirm).
4. Item stands still show the original item name: decide on a separate hover path for pieces.
5. Carts: does the ZDO string survive `Vagon` ownership changes (expected yes, since it is a plain string)?
6. Does Shift + Right click collide with other mods (the item dialog already uses `MenuOpenModifier`)?
7. Hover sync for other clients.

## Out of scope

Portals, creatures, wild monsters, prefab-wide names, ship hulls (Shipwright's Touch). Ship Guard is a separate mod that depends on Locksmith.
