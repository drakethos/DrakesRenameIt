# Vendor/DrakeModsLibs

DrakesRenameItLegacy has no DrakeModsLibs dependency: no `ProjectReference`
(see `mod.package.props` → `EmbedServerSync=true`, which also skips the
`build/DrakeMod.Reference.WorkshopLibs.props` import), no `[BepInDependency]`
on it, no separate Libs plugin to load.

This folder is a private, frozen copy of the slice of `DrakeModsLibs` that
RenameIt's naming/tag/display engine, Paper's Forge-loaded assets, and the
new UI-Toolkit Workshop panel actually call into — compiled directly into
`DrakesRenameItLegacy.dll` under the original `DrakeModsLibs.*` namespaces
so the rest of the mod's source didn't need touching call-site by call-site.
`CustomizeLibsPlugin.cs` (the separate BepInEx plugin bootstrap) was **not**
vendored — its `Awake()` setup (`MenuBindingRegistry`, `HarmonyPatchHub`,
`DrakeIntegrationConfig`, inventory-context localization) is folded into
`DrakeRenameit.Awake()` / `InitVendoredDisplayEngine()` instead.

Known, deliberate differences from upstream Libs:
- `Patches/ItemIconPatches.cs` and `Patches/ItemLookPatches.cs` (icon-override
  rendering and equipped-gear recolor — ReskinIt features) were dropped:
  nothing in RenameIt calls `GetIconOverride`/`SetIconOverride`/
  `GetModelOverride`/`SetModelOverride`, and leaving Harmony patches for a
  feature with no UI to drive it is pure attack surface for no benefit.
  `Display/ItemIconService.cs` and `Display/IconCatalog.cs` are now
  reachable only through the now-dead `CustomizeLibsAPI` icon-override
  methods — still present, not yet pruned.
- `Integration/PieceMenuTabs.cs` was not copied (piece-rename is out of
  scope for Legacy).
- `WardHoverAccess`'s LockSmith probe and `CompatibilityManager`'s LockSmith
  soft-dependency were removed from RenameIt's own code (not from here) —
  Legacy has no other-Drakes-mod integration.
- `Integration/RenameItLibsBridge.cs`'s `DrakePermissionProfiles.RegisterSource`
  and `DrakeItemCategory.RegisterAlias` calls were removed for the same
  reason (they existed only so other Drakes mods, e.g. ReskinIt, could defer
  to RenameIt).

Not yet done: a full reachability trim. This folder is still closer to "the
whole engine, minus the two patch files we know are dead" than a minimal
slice — correct and standalone, but bigger than it needs to be. Pulling
`API/CustomizeLibsAPI.cs` down to just the members RenameIt calls (and
deleting whatever becomes unreachable behind it) is the next pass.
