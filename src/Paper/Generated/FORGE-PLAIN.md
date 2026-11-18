# Drakes Asset Forge items (plain C#)

Regenerated on every export: `Paper\Generated\Items\*.g.cs` (one class per recipe: `Build(parent, scale)` adds its sprites, `Dress(prefab)` the rest, `Icon`) and `Paper\Generated\Assets\` (the pack's images).

1. Your own code registers the items and calls, for example, `Items.PaperSheet.Build(decor.transform, scale)` and `Items.PaperSheet.Icon`.

2. In your `.csproj`, put the images inside the DLL (safe when Hexium/Gale flatten folders):

```xml
<ItemGroup>
  <EmbeddedResource Include="Paper\Generated\Assets\**\*.png;Paper\Generated\Assets\**\*.jpg" />
</ItemGroup>
```

3. References: DrakeModsLibs 0.10+ (its `DrakeModsLibs.Forge` helpers), Jotunn, assembly_valheim, UnityEngine.CoreModule.

No Forge Runtime and no pack files are needed.