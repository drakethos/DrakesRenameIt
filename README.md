
<img width="256" height="256" alt="icon" src="https://github.com/user-attachments/assets/47b41d4d-e109-480c-9d26-2ad4dadf377e" />

# DrakesRenameIt V1.0

A Valheim mod that lets you rename items, rewrite descriptions and  now even update the **Crafted by** line (display only). Good for roleplay or labeling gear.

### How to use

Hold your configured keys (**MenuOpenModifier** in `UI-NotSynced` — default **Shift**, or **Ctrl**, **Alt**, combos like **Shift+Alt**, **F1**, or **None** for right-click only) and **right-click** an inventory item to open a small menu: **Rename**, **Description**, or **Crafted by**. Choose an action; only available options are clickable.

**Okay** confirms your change; **Reset** restores the original localized string; **Cancel** closes without saving. **Reset all** asks for confirmation before clearing every Drake customization on that stack. When **Unlock cost** is enabled, use **Unlock** once per stack (from your inventory) before edits apply — stacks you already customized are grandfathered in automatically.
It always appears with the current name including localization. If you would like to maintain
localization with an additional name, simply leave the $string intact and add around it.
<p>Rename anything:</p>
<img width="262" height="137" alt="image" src="https://github.com/user-attachments/assets/0d1634b0-5fff-4518-9bea-3d72f6c19b7d" />

<p>Lock others from changing your names:</p>
<img width="286" height="276" alt="image" src="https://github.com/user-attachments/assets/ec1a8644-b4fa-4534-9bac-30a112d8006d" />

<p>Easy to use Shift + Right click in inventory: You can recolor and even keep localized strings.</p>
<img width="415" height="233" alt="image" src="https://github.com/user-attachments/assets/cf645932-288c-4406-8b3f-de5635bc0bbb" />

<p>Start your own rock collection:</p>
<img width="274" height="137"  alt="image" src="https://github.com/user-attachments/assets/e0fb0e16-db7b-4988-aae3-8343061c395a" />

<p>Edit descriptions!</p>
<img width="264" height="260" alt="image" src="https://github.com/user-attachments/assets/a33411d4-4e0d-49c2-977a-f19fe03eb83a" />

<p>Works on item stands: edit them with font styles</p>
<img width="284" height="149" alt="image" src="https://github.com/user-attachments/assets/aa1b75d4-475d-4772-b18c-0abeb1283d9f" />

<p>Now with customizable "crafted by" names and messages</p>
<img width="249" height="246" alt="image" src="https://github.com/user-attachments/assets/ce1e72e2-0a43-4d77-a9aa-5ea849534628" />

<p>New simplified single modifier. Right click menu to access 3 editible items, with fast reset.</p>
<img width="302" height="271" alt="image" src="https://github.com/user-attachments/assets/d9b2f38c-ae50-40a9-ae05-6f290c60980d" />

<p>Configurable cost mode: set in game prices to make renaming more meaningful.</p>
<img width="346" height="254" alt="image" src="https://github.com/user-attachments/assets/16d5c388-92a7-45bb-87df-fddc64b2d45e" />

<p>Configurable list of alternates to "Crafted By" to update</p>
<img width="348" height="239" alt="image" src="https://github.com/user-attachments/assets/3de2900f-3fe3-499e-b650-3e9774e6eb51" />

<p>Configurable flag warded item stands. Great for showing things off in a shop setting. Able to add price tags on them, etc.</p>
<img width="331" height="247" alt="image" src="https://github.com/user-attachments/assets/6ab2dee8-37cb-45de-80e5-1c69f1e2b93b" />

### Features with rename:
- Fully supports the same sign codes like `<color=blue>`, `<b>bold</b>`, etc. — see official Valheim sign documentation for the full list.
  - You can resize using `<size=...>`. Values over ~200 are not recommended; layout can break.
- Supports localization strings (`$item_...` tokens); leave tokens intact and wrap extra text around them to keep localization.
- Rename, description, and **Crafted by** (display name + optional **tooltip line** prefix such as “Belongs To”) on that item instance only — internal prefab ids are unchanged for other mods.
- Fully supports multiplayer; every client needs the mod.
- **MenuHintColor** and flexible **MenuOpenModifier** key bindings (per-client UI).
- Toggle rename, descriptions, and crafted-by independently; **ShowDenialUi** / **ShowReason** control blocked-item feedback.
- **AllowAdminOverride** + **VipList** / API VIPs bypass ownership and exclusions; **VipOnlyOverride** can require VIP list only (ignore Valheim server admin).
- **Unlock cost** — optional per-stack payment before edits; tooltip shows lock / pen icons; admins and VIPs skip when override applies.
- **Separate stacks** — customized stacks only merge when name, description, and crafted-by data match; optional hard lock blocks manual drag-merges too.
- **ExcludeStacks** — block non-elevated edits on vanilla stackable items (`m_maxStackSize > 1`), separate from merge rules.
- **Durability modifiers** — optional wear labels prepended to display names (Pristine / tiers / Broken).
- Exclusions by item name, category, and **RenameAllowlist** bypasses; reference file generated for category tokens.
- Unified action menu (rename, description, crafted by) with fast reset; **Cancel** and **Reset all** confirmation.
- Item stands: custom names on hover; **ShowItemStandItemNameWhenNoAccess** shows the stand item name even in warded “no access” areas.
- Centralized localization (`Assets/Localization/*.json`); English and Spanish included.
- API hooks for other mods (`RenameEvents`: name, description, crafted-by display changes).
- Tooltip rich-text safety (unclosed tags auto-closed); crafted-by display can get default orange coloring when unstyled.
#### What this Mod doesn't do:
  - <s>your taxes
  - change every single item that exists
  - makes new instances of an item
  - actually change the name of the item behind the scenes
  - give you up
  - let you down!</s>
### Configurations:

Settings live in `BepInEx/config/` (e.g. `com.DrakeMods.DrakesRenameit.cfg`). Sections are numbered in the file so Configuration Manager sorts in tab order. **Almost everything is server-synced**; per-client exceptions are **MenuOpenModifier** and **MenuHintColor** (`UI-NotSynced`). Legacy section names are migrated automatically on load.

#### Features (server-synced)

- **RenameEnabled** — When on, players may edit display names from the action menu (subject to all other rules). Turn off to block new renames while keeping descriptions or crafted-by.
- **RewriteDescriptionsEnabled** — When on, descriptions may be edited from the menu. Can be used without rename, or turned off if you only want custom names.
- **CraftedByLabelEnabled** — When on, players may set a **display-only** override for the crafted-by line in tooltips (name and optional line prefix). Real crafter id/name used by the game (ownership, locks) is unchanged.

#### Admin (server-synced)

- **AllowAdminOverride** — When on, “elevated” players (see below) bypass **LockToOwner**, exclusions, unowned-resource blocks, unlock cost, and most per-item rules. Elevated players can still edit when feature toggles are off for everyone else.
- **VipList** — Comma-separated **player names** or **player IDs** (same strings as the API `AddVIP`). Used when **AllowAdminOverride** is on. Valheim server admins also count as elevated unless **VipOnlyOverride** is on.
- **VipOnlyOverride** — When **true** (and **AllowAdminOverride** is on), **only** VIP list / API VIPs count as elevated; Valheim’s server admin flag is **ignored** for bypassing rules. When **false**, either Valheim admin **or** VIP counts as elevated.

#### Exclusions (server-synced)

- **ExcludedNames** — Comma-separated items that **cannot** be renamed or have descriptions/crafted-by changed (for non-elevated players). Each entry can be a [Jotunn item list](https://valheim-modding.github.io/Jotunn/data/objects/item-list.html) **Item** (spawn name, e.g. `AxeStone`), **Token** (`$item_...`), or **English Name** column. Elevated users ignore this when **AllowAdminOverride** is on.
- **ExcludedCategory** — Comma-separated category tokens, e.g. `Swords`, `Armor`, `Material`, `Bows`, or `Skills.SkillType` / `ItemType` enum names. Alias words like `armor`, `weapons`, `melee` also work. Reference file: **`BepInEx/config/com.DrakeMods.DrakesRenameit/ExcludedCategoryReference.txt`** on first run or version change.
- **RenameAllowlist** — Same entry format as **ExcludedNames**. For normal players, items on this list **skip** excluded-by-name, excluded-by-category, **ExcludeStacks**, and the unowned-resource check, but still require the relevant **Features** toggles to be on and still obey **LockToOwner** if another player owns the item.

#### Crafted by (server-synced)

- **LabelCustomizable** — When on, players who may open the crafted-by editor can pick a **tooltip line** prefix from **AllowedLabels**. When off, the picker is greyed for normal players; elevated players can still change it.
- **AllowedLabels** — Comma- or semicolon-separated prefixes shown before the crafter name (e.g. `Belongs To: Name`). The **first** entry is the default row (uses the game’s localized crafted-by line when selected). Further entries are stored on the item when chosen.

#### General (server-synced)

- **LockToOwner** — When on, only the crafter / owner can rename or change description/crafted-by. Stacks with **no** crafter yet are not locked until claimed or crafted.
- **NameClaimsOwner** — When on, successfully applying a new name or description on an **unowned** stack sets you as crafter so **LockToOwner** can protect it. Requires **AllowRenameUnownedItems** or **RenameAllowlist** for raw resources.
- **AllowRenameUnownedItems** — When on, unowned stacks (no crafter id and no crafter name — picked up resources, loot, etc.) may be edited. When off, blocked **unless** on **RenameAllowlist**. Separate from **ExcludedCategory** `Material`, which blocks by item type even when crafted.
- **ShowDenialUi** — When on, access denials show a red menu hint, tooltip denial lines, and a center message on failed modifier+right-click. When off, those cues are hidden (silent). Unlock-cost, not-in-inventory, and validation errors are unchanged.
- **ShowReason** — When on (and **ShowDenialUi** is on), denial text explains **why** (ownership, exclusion, etc.). When off, generic messages only. No effect when **ShowDenialUi** is off. Denials are still logged to BepInEx for admins.
- **ShowItemStandItemNameWhenNoAccess** — When on, item stands in warded/private areas still show “no access” but also append the stand’s current item name on the hover label (display only; permissions unchanged).

#### Stacks (server-synced)

- **SeparateStacks** — When on, stacks only merge when Drake identity matches (custom name, description, crafted-by display, and crafted-by line label). Auto pickup will not combine mismatched customized stacks.
- **SeparateStacksHardLock** — Only when **SeparateStacks** is on. When **true**, mismatched stacks never merge, including manual drags. When **false**, auto pickup still separates mismatches, but you can drag-merge in one step (target keeps its custom data).
- **ExcludeStacks** — When **true**, non-elevated players cannot rename, edit descriptions, or crafted-by on vanilla stackable items (`m_maxStackSize > 1`). **RenameAllowlist** and elevated override bypass this. Independent of **SeparateStacks** merge behavior.

#### Unlock cost (server-synced)

- **UnlockCostEnabled** — When on (and **UnlockCost** parses to at least one item), each stack must be unlocked once before rename/description/crafted-by edits. Pay from inventory via **Unlock** in the action menu. Elevated players skip when **AllowAdminOverride** applies. Stacks already customized before unlock existed are grandfathered (unlock flag written once).
- **UnlockCost** — Comma- or semicolon-separated `PrefabName:amount` (e.g. `Coins:5`, `Coal:10`) or `$item_token:amount`. Invalid or empty cost is ignored.

#### Limits (server-synced)

- **NameCharacterLimit** — Max length for custom names (rich-text tag codes count toward the limit).
- **CraftedByCharLimit** — Max length for crafted-by display names (rich-text tags count).
- **DescriptionCharacterLimit** — Max length for custom descriptions.

#### Modifiers (server-synced)

- **DurabilityModifierEnabled** — When on, prepends a wear label to display names for items with forge durability (`m_useDurability`), not spoilage timers. Does not change stored rename text.
- **DurabilityUnbrokenLabel** — Label at full durability (~100%). Rich-text allowed; empty = no label when pristine.
- **DurabilityBrokenLabel** — Label only at **0** durability (broken in-game). Not used for merely worn gear.
- **DurabilityTierModifiers** — In-between wear bands: `{Name,fraction}` or `{Name,percent}` (e.g. `{Rusty,0.4},{Worn,0.6}`). Lowest matching threshold wins; gap above highest tier but below full gets no extra label. Skips items with no durability (`m_maxDurability` 0).

#### UI-NotSynced (client only)

- **MenuOpenModifier** — Keys held while right-clicking an inventory item to open the Drake menu. Examples: `Shift`, `Ctrl`, `Alt`, `Shift+Alt`, `F1`. Combine with `+`, `,`, or `&`. Use `None` for right-click only.
- **MenuHintColor** — Tooltip hint color for the menu shortcut (Unity color name or `#rrggbb`).

#### Permission order (how rules stack)

**Admin/VIP override** (and unlock-cost skip when elevated) → **Features toggles** (rename, description, crafted-by) → **LockToOwner** (once an owner exists) → **RenameAllowlist** → **excluded names**, **excluded category**, **ExcludeStacks**, and **AllowRenameUnownedItems** for raw resources → **unlock cost** (if enabled) for the stack.

### Known Issues:
Known Issues:
    - None currently but please report if you find any!
#### Wishlist for future
- if there is a high demand for this:
    - Renamable pieces (that have hover names)
##### Distant crazy features
- Someday if it seems doable, I may add customizations like color changes to the icon or item itself, things like that. However this may require a lot of work since I believe it would require new prefabs of items which may be a mess for Valheim.
-   Probably a seperate mod though!
#### API Docs:
Types live in namespace `DrakeRenameit.API`.

**Events** (`RenameEvents`):

```csharp
using DrakeRenameit.API;

RenameEvents.OnItemNameChanged += (player, item, oldName, newName) => { /* ... */ };
RenameEvents.OnItemDescriptionChanged += (player, item, oldDesc, newDesc) => { /* ... */ };
RenameEvents.OnCraftedByDisplayChanged += (player, item, itemPrefabName, oldDisplay, newDisplay) => { /* ... */ };
```

**VIP / elevated checks** (`RenameitPermission`) — requires **AllowAdminOverride** on the server for bypass rules to apply. Use the player’s **character name** or **`GetPlayerID().ToString()`** (same values as **VipList** in config):

```csharp
using DrakeRenameit.API;

// At mod startup or when your mod grants perks:
RenameitPermission.AddVIP("SomePlayerName");
RenameitPermission.AddVIP(Player.m_localPlayer.GetPlayerID().ToString());

// Or bulk from your own list:
RenameitPermission.AddVIP(new List<string> { "Alice", "76561198000000000" });

RenameitPermission.RemoveVIP("SomePlayerName");

foreach (string vip in RenameitPermission.GetVIPs())
    Logger.LogInfo($"VIP: {vip}");

if (RenameitPermission.IsElevatedForOverrides(Player.m_localPlayer))
    Logger.LogInfo("Local player bypasses ownership/exclusions (VIP or Valheim admin, per config).");
```

Contact me:
- Want to drop a line tell me how I'm doing.
  -Report a bug (THATS NOT IN THE KNOWN ISSUES ALREADY),
  or a request for new features.
- I cannot guarantee the request will be met but if there's a high enough demand and the ask isn't too difficult I may take it into consideration.
  Email: Drakethos@gmail.com
  Discord: Drakethos
- discord server: https://discord.gg/cQegN9fB6r

- buy me a coffee ☕
https://paypal.me/Drakethos?country.x=US&locale.x=en_US

Credits:
Used Cursor AI assitance to complete desired features
Big shout out and thanks to nchnc for the intense testing of this mod!
