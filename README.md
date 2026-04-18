# DrakesRenameIt V0.7.0

A much needed mod for Valheim that lets you rename and rewrite descriptions of any items. Great for roleplay or just plain fun! Want to let your friends know, that axe is totally yours? Prank a friend by changing his favorite axe.
### How to use:
Simply Press shift + right click on any item you want to rename.
Press ctrl + right click on any item you want to change the description of! (new!)
A dialog prompting you to change your items name/description will appear
Okay will confirm the dialog with your change, reset button will bring it back to the items original localized string.
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

<p>New now we have descriptions!</p>
<img width="264" height="260" alt="image" src="https://github.com/user-attachments/assets/a33411d4-4e0d-49c2-977a-f19fe03eb83a" />

### Features with rename:
- Fully supports the same sign codes like <color=blue> <b>bold</b> etc for full information check official Valheim documentation
  on sign features.
  - You can even resize using <size=...> Do not recommend over 200, things start to get funky.
- Supports localization strings
- Lets you rename any existing item and renames that instance.
- Fully supports multiplayer play, just ensure each client has the mod.
- recolor the UI tips with configs
- New - Admin override to not apply to the rules
- You can enable and disable parts of the mod now.
- New - API hooks for other mods to track name and description changes
- Doesn't ACTUALLY rename items, so any mod that needs to deal with the items shared: name won't experience any issues! (hopefully...)
#### What this Mod doesn't do:
  - <s>your taxes
  - change every single item that exists
  - makes new instances of an item
  - actually change the name of the item under the hood
  - give you up
  - let you down!</s>
### Configurations:

Settings live in `BepInEx/config/` (e.g. `com.DrakeMods.DrakesRenameit.cfg`). **Almost everything is server-synced** so the host controls rules for the world; the exceptions are **ShiftColor** and **CtrlColor** (per-client UI only).

#### General (server-synced)

- **RenameEnabled** — When on, players can use Shift + right-click to rename items. Turn off to block new renames while keeping descriptions or other features (for example after pre-placing renamed gear).
- **RewriteDescriptionsEnabled** — When on, players can use Ctrl + right-click to edit that item instance’s description. Can be used without rename, or turned off if you only want custom names.
- **LockToOwner** — When on, only the crafter / owner can rename or change description. Stacks with **no** crafter yet (raw picked-up resources: no crafter id and no crafter name) are **not** treated as “someone else’s” until they are crafted or claimed. After **NameClaimsOwner** assigns you as crafter, others are blocked as usual.
- **NameClaimsOwner** — When on, successfully applying a new name or description on an **unowned** stack sets you as crafter (and “crafted by” style ownership) so **LockToOwner** can protect that stack. Works with uncrafted resources when **AllowRenameResources** (and allowlist if needed) permits the edit.
- **AllowRenameResources** — When on, unowned resource-style stacks (no crafter yet) can be renamed or given a description. When off, those stacks are blocked **unless** they are on **RenameAllowlist** (then claim rules can still apply). This is separate from **ExcludedCategory** `Material`, which can still block by item type for crafted gear.
- **ShowReason** — When on, denied rename/description attempts show **why** (ownership, exclusion, resources, etc.) in the center message and inventory tooltip. When off, messages stay generic. **Server-synced** so clients cannot enable detailed reasons against the host’s preference. Denials are still logged to the BepInEx log for admins.

#### Limits (server-synced)

- **NameCharacterLimit** — Max length for custom names. Counts rich text tags (`<color>`, `<size>`, etc.), so leave headroom.
- **DescriptionCharacterLimit** — Same idea for custom descriptions.

#### Admin (server-synced)

- **AllowAdminOverride** — When on, “elevated” players (see below) bypass **LockToOwner**, exclusions, and resource rules, and can still rename or edit descriptions even when **RenameEnabled** / **RewriteDescriptionsEnabled** are off for everyone else (“regardless of ownership or enabled,” per config).
- **VipList** — Comma-separated **player names** or **player IDs** (same strings as the API `AddVIP`). Used when **AllowAdminOverride** is on. Valheim server admins also count as elevated unless **VipOnlyOverride** is on.
- **VipOnlyOverride** — When **true** (and **AllowAdminOverride** is on), **only** VIP list / API VIPs count as elevated; Valheim’s server admin flag is **ignored** for bypassing rules. Useful to test VIP-only behavior. When **false**, either Valheim admin **or** VIP counts as elevated.

#### Exclusions (server-synced)

- **ExcludedNames** — Comma-separated items that **cannot** be renamed or have descriptions changed (for non-elevated players). Each entry can be a [Jotunn item list](https://valheim-modding.github.io/Jotunn/data/objects/item-list.html) **Item** (spawn name, e.g. `AxeStone`), **Token** (`$item_...`, matches internal item name), or **English Name** column. Elevated users ignore this when **AllowAdminOverride** is on.
- **ExcludedCategory** — Comma-separated category tokens, e.g. `Swords`, `Armor`, `Material`, `Bows`, or `Skills.SkillType` / `ItemType` enum names. Alias words like `armor`, `weapons`, `melee` also work. A full reference file is generated at **`BepInEx/config/com.DrakeMods.DrakesRenameit/ExcludedCategoryReference.txt`** on first run or when the mod version changes.
- **RenameAllowlist** — Same entry format as **ExcludedNames**. For normal players, items on this list **skip** excluded-by-name, excluded-by-category, and the unowned-resource check, but still require **RenameEnabled** / **RewriteDescriptionsEnabled** to be on and still obey **LockToOwner** if another player owns the stack. **AllowAdminOverride** elevation bypasses the global toggles and most restrictions as usual.

#### UI (not synced — client only)

- **ShiftColor** — Color for the “Shift + right click to rename” hint in the inventory tooltip (Unity color name or `#rrggbb`).
- **CtrlColor** — Same for “Ctrl + right click” description hint.

#### Permission order (how rules stack)

Roughly: **Admin/VIP override** → **global rename / description toggles** → **LockToOwner** (only matters once an owner exists) → **RenameAllowlist** → then **excluded names**, **excluded category**, and **AllowRenameResources** for raw resources.

### Quirks and Known Issues:
Quirks:
- Item stacks behave a very particular way. When you rename an existing stack, it will rename the whole stack. Any item added to said stack
  will then become absorbed. This is the best way to prevent say picking up a rock, and having your special rock blown away when it mixes into the stack
  this is due to the nature of how the stack holds items.
- This means if you have a special pet rock by itself, if you pick up another rock, it will create a stack and lose your name.
  - Future feature may add the option to prevent stacks from combining with different names automatically.
    Known Issues:
  - ~~Item stands still show the name of the original item~~
    - Fixed!
  - ~~Upgrading an item will replace the custom name with the original name~~
    - Fixed!
#### Wishlist for future
- I may try to add stack splitting feature
- Costs (configurable)
  - To prevent others from renaming things a million times adding some sort of cost so item renaming is more special
- if there is a high demand for this:
- Renamable pieces (that have hover names)
##### Distant crazy features
- Someday if it seems doable, I may add customizations like color changes to the icon or item itself, things like that, However this may require a lot of work since I believe it would require new prefabs of items which may be a mess for valheim.

#### API Docs:
The mod exposes two events for other mods to hook into when an item name or description is changed.
API.RenameEvents
example for logging
```csharp
RenameEvents.OnItemNameChanged += (player, item, oldName, newName) =>
{
   //todo: add your code here
};
RenameEvents.OnItemDescriptionChanged += (player, item, oldName, newName) =>
{
   //todo: add your code here
};
```

Contact me:
- Want to drop a line tell me how I'm doing.
  -Report a bug (THATS NOT IN THE KNOWN ISSUES ALREADY),
  or a request for new features.
- I cannot guarantee the request will be met but if there's a high enough demand and the ask isnt too difficult I may take it into consideration.
  Email: Drakethos@gmail.com
  Discord: Drakethos!

