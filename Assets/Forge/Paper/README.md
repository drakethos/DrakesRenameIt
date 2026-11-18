# Drakes Paper (Asset Forge pack)

The look of RenameIt's paper, as [Drakes Asset Forge](https://github.com/drakethos/DrakesAssetForge) recipes.
This pack is the source of truth: open it in Asset Forge to preview or edit, then regenerate the code:

```
DrakesAssetForge export-code Assets/Forge/Paper . --look-only --libs --embed --into --namespace DrakeRenameit.Paper.Generated --folder src/Paper/Generated
```

The generated `src/Paper/Generated/Items/*.g.cs` add the sheet sprites (`Build(parent, scale)`) and expose the icons;
RenameIt registers the items/pieces itself and keeps all paper gameplay. Images are embedded in the DLL.
This folder is authoring source. The Thunderstore zip does not copy it. Gale only keeps folders under `plugins/`, and the sheets that ship are already inside the DLL.
Sizes are US Letter at scale 1; RenameIt passes its `PaperScale` setting.
