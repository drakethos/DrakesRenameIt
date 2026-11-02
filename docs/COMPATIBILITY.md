# Making RenameIt work with another mod

RenameIt changes what things are *called*. Most mods leave names alone, so nothing needs doing. The exception is a mod
that **draws its own item UI**, such as equipment slots, a replacement inventory or a custom tooltip. If it writes the
item's vanilla name straight into its tooltip, your renamed item will show its old name there.

We cannot chase every mod, so we look after the big ones first (see [Which mods we support](#which-mods-we-support)).
If yours is not on the list, you can add support yourself in about an hour. This page shows how.

## Which mods we support

- **Supported on purpose:** mods with a large audience (roughly **1,000+ downloads**), or mods a running server depends
  on and has asked for. Current list in the README under *Compatibility*.
- **Everything else:** welcome to be supported by a **module you write**. We will happily merge a pull request, or you can
  ship the module as its own small mod.
- If you need something looked at, open a thread on the Hexium page or the GitHub issues and tell us which mod and what
  you see. We read everything, but we pick by how many people use the mod.

## How a compat module works

RenameIt has a **compat host** (from DrakeModsLibs) that loads small *modules*, one per foreign mod. A module:

1. checks whether its mod is installed (`TryActivate`),
2. if so, applies a few Harmony patches (`ApplyHarmonyPatches`).

Nothing runs when the other mod is absent, so a module costs a player who does not have that mod nothing.

```csharp
public interface ICompatModule
{
    string Id { get; }                    // stable id for logs
    string? SoftDependencyGuid { get; }   // the other mod's BepInEx GUID (load order only)
    int Priority { get; }                 // CompatPriority.BuiltIn (100) for ours, ThirdParty (200) for yours
    bool IsActive { get; }
    bool TryActivate();                   // is the other mod here? must never throw
    void ApplyHarmonyPatches(Harmony harmony);
}
```

## Worked example: ExtraSlots

[`Compat/ExtraSlots/ExtraSlotsTooltipModule.cs`](../Compat/ExtraSlots/ExtraSlotsTooltipModule.cs) is the model to copy.

**The problem.** ExtraSlots rebuilds the tooltip of each equipment slot on every update. It checks whether the tooltip
title contains the item's *vanilla* name; a renamed item's title does not, so ExtraSlots writes the vanilla name back.

**The fix.** After ExtraSlots has set the slot, ask the game for the normal grid tooltip again. RenameIt and
DrakeModsLibs already patch that, so the new name and the *Crafted by* line apply:

```csharp
internal sealed class ExtraSlotsTooltipModule : ICompatModule
{
    public string Id => "ExtraSlotsTooltip";
    public string? SoftDependencyGuid => "shudnal.ExtraSlots";
    public int Priority => CompatPriority.BuiltIn;
    public bool IsActive { get; private set; }

    public bool TryActivate()
    {
        try { IsActive = Chainloader.PluginInfos.ContainsKey("shudnal.ExtraSlots") && ResolveTarget() != null; }
        catch { IsActive = false; }
        return IsActive;
    }

    public void ApplyHarmonyPatches(Harmony harmony)
    {
        var target = ResolveTarget();            // ExtraSlots.EquipmentPanel.SetSlotBackgroundImage
        if (target != null)
            harmony.Patch(target, postfix: new HarmonyMethod(typeof(ExtraSlotsTooltipModule), nameof(Postfix)));
    }
    // Postfix(object element, object slot): find the item and its UITooltip, then re-run InventoryGrid.CreateItemTooltip.
}
```

Four things worth copying:

- **Use `object` for the other mod's types** in your patch and read members with `AccessTools`. Then your module never
  needs the other mod's DLL, and a version bump on their side cannot stop yours from loading.
- **Never throw.** Wrap patch bodies in `try/catch`; a broken tooltip must not break the inventory.
- **Reflect on game members that are not public.** `InventoryGrid.CreateItemTooltip` is non-public in the live game even
  though it compiles against the old stubs. The release check (below) catches this.
- **Add a soft dependency** so the other mod loads first:
  `[BepInDependency("shudnal.ExtraSlots", BepInDependency.DependencyFlags.SoftDependency)]` on the RenameIt plugin.

## Option A: pull request into RenameIt

1. Add `Compat/<ModName>/<ModName>Module.cs` next to the ExtraSlots one.
2. Add `<Compile Include="Compat\<ModName>\<ModName>Module.cs" />` to `DrakeRenameit.csproj` (the project lists its files).
3. Register it in `Compat/CompatibilityManager.cs`: `Host.Register(new MyModule());`.
4. Add the soft dependency attribute in `DrakeRenameit.cs` and a GUID constant in `CompatibilityManager.SoftGuids`.
5. Run the checks below and open the pull request. Tell us which mod and version you tested.

## Option B: your own small mod

You do not have to touch RenameIt. Reference `DrakeModsLibs` and register on RenameIt's host:

```csharp
[BepInDependency("com.drakemods.renameit")]
[BepInDependency("author.TheOtherMod", BepInDependency.DependencyFlags.SoftDependency)]
[BepInPlugin("you.RenameItTheOtherModCompat", "RenameIt + TheOtherMod", "1.0.0")]
public class Plugin : BaseUnityPlugin
{
    void Awake() =>
        DrakeModsLibs.Compat.CompatHosts.GetOrCreate("com.drakemods.renameit", Logger).Register(new MyModule());
}
```

Registering after RenameIt has started is fine: late modules get their patches applied straight away. Use
`CompatPriority.ThirdParty` for `Priority`.

## Checking it works

1. **Find the problem.** Open the other mod's DLL in a decompiler (ILSpy). Search for `tooltip`, `.Set(`,
   `m_shared.m_name`, `GetTooltip`. A line that sets the tooltip title from `m_shared.m_name` is the usual cause.
2. **Try it in game.** Craft or find an item, rename it (Shift + right-click), put it in the other mod's slot and hover it.
   The tooltip title must be your new name, with the *Crafted by* line.
3. **Automated check (optional).** The RenameIt repo has a test for this, `X-EQ-03`, for DrakesTestLab: it equips a renamed
   cape, opens the inventory, reads every tooltip topic and takes a screenshot. `M-EQ-01` is the manual version.
4. **Release check.** Builds use old reference stubs, so a method that is not public in the live game compiles and then
   fails at runtime. Run `Verify-ValheimApiRefs.ps1` on the built DLL before you send it.

## Known limits

- Mods that draw an entirely separate UI with no Valheim tooltips (some full UI replacements) need their own
  hook; a module can patch their label code the same way, but expect more work.
- A tooltip the other mod only fills on a real mouse hover cannot be read by the automated test; use the manual check.
