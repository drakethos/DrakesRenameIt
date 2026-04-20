# DrakesReskinIt

**DrakesReskinIt** is an addon mod for the DrakeMods suite that lets players customize the **icon** and **tint/recolor** of individual item stacks in their inventory — without touching any other player's copy of the same item.

Built on the same core libraries as **DrakesRenameit**.

---

## Features

- **Change Icon** — swap the displayed icon of any item stack to any registered sprite. Third-party mods can contribute icons at startup via `IconRegistry.Register(name, sprite)`.
- **Recolor / Tint** — apply a color tint to an item's icon. Choose from 12 preset colors or enter any HTML hex value (`#rrggbb`).
- All changes are stored in Valheim's native **`m_customData`** on the item — fully save-safe and server-compatible.
- Shared server-config (ServerSync) so the server controls which features are enabled.
- Full admin / VIP bypass system matching DrakesRenameit.
- Per-item exclusion lists (name, category) and allowlist.

---

## Usage

1. Open your inventory and `Shift + Right Click` (or `Ctrl + Right Click`, configurable) an item.
2. The **DrakesReskinIt** action menu appears.
3. Choose **Change Icon** to pick from registered icons, or **Recolor / Tint** to apply a color.
4. Use **Reset all** to remove all customizations from that stack.

---

## For Modders — Adding Icons

```csharp
// In your BepInEx plugin Awake / Start, after DrakesReskinIt loads:
DrakesReskinIt.IconRegistry.Register("MyMod.FancySword", mySprite);
```

Players can then select `MyMod.FancySword` in the icon picker. Store the name in `m_customData[DrakesReskinIt.DrakeCustomIcon]` to apply it programmatically.

---

## Configuration (BepInEx config / ServerSync)

| Key | Default | Notes |
|-----|---------|-------|
| `ReskinEnabled` | `true` | Master toggle for icon changes |
| `TintEnabled` | `true` | Enable/disable tint sub-feature |
| `LockToOwner` | `true` | Only the crafter/owner can reskin |
| `ReskinClaimsOwner` | `true` | Reskinning unclaimed items claims ownership |
| `AllowAdminOverride` | `true` | Admins/VIPs bypass all rules |
| `VipList` | `""` | Comma-separated player names or IDs |
| `ExcludedNames` | `""` | Item tokens/names blocked from reskinning |
| `ExcludedCategory` | `""` | Item type/category tokens blocked |
| `ReskinAllowlist` | `""` | Items that bypass exclusions |
| `MenuOpenModifier` | `Shift` | `Shift` or `Ctrl` + right-click |
| `MenuHintColor` | `yellow` | Tooltip hint color |

---

## Requirements

- BepInEx 5.4.21+
- Jotunn 2.26.1+
- DrakesRenameit 1.0.0+ (shares ServerSync and core libs)
