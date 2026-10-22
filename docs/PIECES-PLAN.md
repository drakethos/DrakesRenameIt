# Rename It: pieces plan (1.3 or 2.0, undecided)

Maps the pieces handoff onto the current code. Nothing here is implemented yet.
The Valheim DLLs in `libs/` are links to the PC's Steam folder, so every "verify" item below still needs a decompile on the PC.

## Where pieces plug into the current code

| Handoff item | Existing code to extend | Gap |
|---|---|---|
| Name / description storage | `DrakeCustomDataKeys` (item custom data) | Pieces have no item custom data. Use ZDO strings (`zdo.Set(string, string)`) under the same key names, so they survive ownership handoffs. |
| Permission checks | `RenamePermissionManager`, `RenamePermissionOperation` | Add `RenamePieceName` and `RenamePieceDescription`. Pieces skip the item rules (exclusions, stacks, unlock cost). |
| Ward status line | `Compat/IAreaCompatModule`, `WardHoverAccess` | Reuse `QueryLocalAccess` for "You can build here · <ward>". Ward access must not override a placer lock. |
| Dialog | `UI/UIPanels.cs` (`OpenActionMenu`) | Add a `Piece` section and a piece-mode entry point. The item dialog stays as is. |
| Hover text | `Patches/PaperItemStandPatches.cs`, `Compat/ItemLibs/ItemStandHoverModule.cs` | Postfix on each first-wave type's `GetHoverText`. Item stands already have a patch, so extend it and avoid a second path. |
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

Pieces change the data model (ZDO instead of item data) and add a new section to the dialog, but nothing existing breaks. That argues for **1.3** unless the dialog is restructured. Take 2.0 only if the item dialog layout has to change for everyone.

## Real dialog vs. the mockup (from the README screenshots, wiki `paper-rename-tab.png`, `paper-tab.png`, `paper-hover.png`)

The shipped UI is not the parchment mockup. Match the shipped one, or decide to restyle it for everyone (that would push toward 2.0).

- Frame: Valheim wood panel, title "Drake's Renameit", orange Valheim-style text.
- Tabs sit above the panel as small buttons: **Rename | Paper**. A piece section would most naturally be a third tab or replace "Paper" for placed pieces. The mockup's single-dialog "Piece section card" would need the tab bar dropped.
- Rename tab: two buttons, **Rename** and **Description**, which open their own editors (not inline fields). Below them a hover hint ("Anyone can rewrite this item's name and description."), a **Shared** toggle, then **Reset All** and **OK**. There is no Cancel.
- Paper tab: toggles (Make public, Landscape text, Immutable stack), +/- steppers, **Make Copy**, **Close**.
- Hover: "[Hold E] Take item" and "**[Shift + E] Edit**". The shipped modifier is Shift + E, not Shift + Right click. That resolves open item 6 for paper: pieces should reuse `MenuOpenModifier`.
- Mockup items that have no equivalent today: per-field labels, the footer Cancel, the amber locked state, Cinzel/Source Sans fonts. Each is new work or a deliberate redesign.

Not retrievable here: valheim.hexium.gg (host not reachable from this environment) and the `github.com/user-attachments` images (403). Only the wiki images were fetched.

## Open items (all need the PC decompile)

1. Real dialog layout: see the section above; the other README screenshots (user-attachments) could not be fetched.
2. `Piece.m_creator`, `SetCreator`, and whether `GetCreator` exists in the current Valheim build.
3. Hover-name methods on `Container`, `ItemStand`, `Sign`, `Bed`, `Vagon` (and `ArmorStand`, which is an `ItemStand`-like type; confirm).
4. Item stands still show the original item name: decide on a separate hover path for pieces.
5. Carts: does the ZDO string survive `Vagon` ownership changes (expected yes, since it is a plain string)?
6. Does Shift + Right click collide with other mods (the item dialog already uses `MenuOpenModifier`)?
7. Hover sync for other clients.

## Out of scope

Portals, creatures, wild monsters, prefab-wide names, ship hulls (Shipwright's Touch). Ship Guard is a separate mod that depends on Locksmith.
