
<img width="256" height="256" alt="icon" src="https://github.com/user-attachments/assets/47b41d4d-e109-480c-9d26-2ad4dadf377e" />

# DrakesRenameIt V1.0

A Valheim mod that lets you rename items, rewrite descriptions and  now even update the **Crafted by** line (display only). Good for roleplay or labeling gear.

### How to use

Hold **Shift** or **Ctrl** (configurable: **MenuOpenModifier** in `UI-NotSynced`) and **right-click** an inventory item to open a small menu: **Rename**, **Description**, or **Crafted by**. Choose an action; only available options are clickable. 

"Okay" will confirm the dialog with your change, while the "reset" button will bring it back to the item's original localized string.
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
- Fully supports the same sign codes like <color=blue> <b>bold</b> etc for full information check official Valheim documentation
  on sign features.
  - You can resize using <size=...> Do not recommend over 200, or things start to get funky.
- Supports localization strings
- Lets you rename any existing item and renames that instance.
- Fully supports multiplayer play, just ensure each client has the mod.
- recolor the UI tips with configs
- Admin override does not apply to the rules
- You can enable and disable parts of the mod now.
- API hooks for other mods to track name and description changes
- Doesn't ACTUALLY rename items, so any mod that needs to deal with the items shared, the name won't experience any issues! (hopefully...)
- *New* Change crafted by name.
- *New* Seperate stacks by customization with configurable options.
- *New* Admin override configurable for auto detection or VIP list only
- *New* Configurable options for showing reasons for denials and more detailed config options for exclusions and allowlists.
- *New* one unified menu for all rename, description, and crafted by edits with a slick new design.
- *New* configurable costs for editing items, making changes more meaningful and preventing abuse.
- *New* Vast array of configurations for what's allowed to be edited, by name and category.
- *New* Allow Item stands to be configured to show name on wards.
#### What this Mod doesn't do:
  - <s>your taxes
  - change every single item that exists
  - makes new instances of an item
  - actually change the name of the item behind the scenes
  - give you up
  - let you down!</s>
### Configurations:

Settings live in `BepInEx/config/` (e.g. `com.DrakeMods.DrakesRenameit.cfg`). **Almost everything is server-synced** so the host controls rules for the world; the exceptions are **ShiftColor** and **CtrlColor** (per-client UI only).

#### General (server-synced)

- **RenameEnabled** — When on, non-elevated players may rename items (via the action menu). Turn off to block new renames while keeping other features.
- **RewriteDescriptionsEnabled** — When on, descriptions may be edited from the menu. Can be used without rename, or turned off if you only want custom names.
- **CraftedByLabelEnabled** — When on, players may set a **display-only** override for the “Crafted by” line in tooltips. Real crafter id/name used by the game (ownership, locks) is unchanged.
- **SeparateStacks** — When on, stacks only merge automatically when the tag matches (custom name, description, and crafted-by display key). Auto pickup and other automatic merges will not combine mismatched stacks.
- **SeparateStacksHardLock** — Only applies when **SeparateStacks** is on. When **true** (default), mismatched stacks cannot be merged. This includes manual drags onto another stack. When **false**, you can drag one stack onto another and they combine immediately. The target stack will keep its custom name, description, and crafted-by display.
- **LockToOwner** — When on, only the crafter / owner can rename or change description. Stacks with **no** crafter yet (raw picked-up resources: no crafter id and no crafter name) are **not** treated as “someone else’s” until they are crafted or claimed. After **NameClaimsOwner** assigns you as crafter, others are blocked as usual.
- **NameClaimsOwner** — When on, successfully applying a new name or description on an **unowned** stack sets you as crafter (and “crafted by” style ownership) so **LockToOwner** can protect that item from edit. Works with uncrafted resources when **AllowRenameResources** (and allowlist if needed) permits the edit.
- **AllowRenameResources** — When on, unowned resource-style items (no crafter yet) can be renamed or given a description. When off, those items are blocked **unless** they are on **RenameAllowlist** (then claim rules can still apply). This is separate from **ExcludedCategory** `Material`, which can still block by item type for crafted gear.
- **ShowReason** — When on, denied rename/description attempts show **why** (ownership, exclusion, resources, etc.) in the center message and inventory tooltip. When off, messages stay generic. **Server-synced** so clients cannot enable detailed reasons against the host’s preference. Denials are still logged to the BepInEx log for admins.

#### Limits (server-synced)

- **NameCharacterLimit** — Max length for custom names. Counts rich text tags (`<color>`, `<size>`, etc.), require adequate headroom.
- **CraftedByCharLimit** — Max length for Crafted By: names. Counts rich text tags (`<color>`, `<size>`, etc.), require adequate headroom.
- **DescriptionCharacterLimit** — Same idea for custom descriptions.

#### Admin (server-synced)

- **AllowAdminOverride** — When on, “elevated” players (see below) bypass **LockToOwner**, exclusions, and resource rules, and can still rename or edit descriptions even when **RenameEnabled** / **RewriteDescriptionsEnabled** are off for everyone else (“regardless of ownership or enabled,” per config).
- **VipList** — Comma-separated **player names** or **player IDs** (same strings as the API `AddVIP`). Used when **AllowAdminOverride** is on. Valheim server admins also count as elevated unless **VipOnlyOverride** is on.
- **VipOnlyOverride** — When **true** (and **AllowAdminOverride** is on), **only** VIP list / API VIPs count as elevated; Valheim’s server admin flag is **ignored** for bypassing rules. Useful to test VIP-only behavior. When **false**, either Valheim admin **or** VIP counts as elevated.

#### Exclusions (server-synced)

- **ExcludedNames** — Comma-separated items that **cannot** be renamed or have descriptions changed (for non-elevated players). Each entry can be a [Jotunn item list](https://valheim-modding.github.io/Jotunn/data/objects/item-list.html) **Item** (spawn name, e.g. `AxeStone`), **Token** (`$item_...`, matches internal item name), or **English Name** column. Elevated users ignore this when **AllowAdminOverride** is on.
- **ExcludedCategory** — Comma-separated category tokens, e.g. `Swords`, `Armor`, `Material`, `Bows`, or `Skills.SkillType` / `ItemType` enum names. Alias words like `armor`, `weapons`, `melee` also work. A full reference file is generated at **`BepInEx/config/com.DrakeMods.DrakesRenameit/ExcludedCategoryReference.txt`** on first run or when the mod version changes.
- **ExcludeStacks** — When **true**, stackable vanilla items (`m_maxStackSize > 1`) cannot be renamed, have descriptions changed, or crafted-by labels edited by non-elevated players. **AllowAdminOverride** still lets admins and VIPs edit those stacks. **RenameAllowlist** can bypass this for specific items. This is separate from **SeparateStacks** / **SeparateStacksHardLock** in General: those control whether differently customized stacks merge; they do not replace admin override for editing.
- **RenameAllowlist** — Same entry format as **ExcludedNames**. For normal players, items on this list **skip** excluded-by-name, excluded-by-category, **ExcludeStacks**, and the unowned-resource check, but still require **RenameEnabled** / **RewriteDescriptionsEnabled** to be on and still obey **LockToOwner** if another player owns the item. **AllowAdminOverride** elevation bypasses the global toggles and most restrictions as usual.

#### UI (not synced — client only)

- **MenuOpenModifier** — `Shift` or `Ctrl` + right-click opens the Drake menu. The other modifier + right-click uses default behavior.
- **ModifierColor** — Tooltip hint color for **MenuOpenModifier** (Unity color name or `#rrggbb`).

#### Permission order (how rules stack)

**Admin/VIP override** → **global toggles** (rename, description, crafted-by label) → **LockToOwner** (only matters once an owner exists) → **RenameAllowlist** → then any of the following: **excluded names**, **excluded category**, **ExcludeStacks** (stackable items), and **AllowRenameResources** for raw resources.

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
Events live in namespace `DrakeRenameit.API` (`RenameEvents`).

```csharp
using DrakeRenameit.API;

RenameEvents.OnItemNameChanged += (player, item, oldName, newName) => { /* ... */ };
RenameEvents.OnItemDescriptionChanged += (player, item, oldDesc, newDesc) => { /* ... */ };
RenameEvents.OnCraftedByDisplayChanged += (player, item, itemPrefabName, oldDisplay, newDisplay) => { /* ... */ };
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
