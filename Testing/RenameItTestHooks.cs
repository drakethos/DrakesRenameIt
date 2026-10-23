using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using DrakeModsLibs.API;
using DrakeRenameit.Paper.Functionality;
using DrakeRenameit.Paper.Items;
using DrakeRenameit.Paper.Pieces;
using DrakeRenameit.Permissions;
using HarmonyLib;
using UnityEngine;
using RenameitPermission = global::DrakeRenameit.API.RenameitPermission;

namespace DrakeRenameit.Testing;

/// <summary>
/// DrakesTestLab finds methods marked with an attribute named <c>TestHookAttribute</c> by name.
/// No reference to DrakesTestLab: without it installed these methods are never called.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
internal sealed class TestHookAttribute : Attribute
{
    public TestHookAttribute(string name) => Name = name;
    public string Name { get; }
}

/// <summary>
/// Test hooks for automated RenameIt tests. They pick an inventory stack and run the same permission checks and
/// apply code the menus use, so tests cover the rules without clicking through the UI.
/// Item args: <c>prefab</c> (e.g. AxeStone), optional <c>index</c> (nth stack of it, 0 = first) or
/// <c>match</c> (the display name contains this).
/// </summary>
internal static class RenameItTestHooks
{
    // ---------- helpers

    static Player P => Player.m_localPlayer ?? throw new InvalidOperationException("no local player");

    static string S(Dictionary<string, object?> a, string key, string fallback = "") =>
        a.TryGetValue(key, out var v) && v != null ? Convert.ToString(v, CultureInfo.InvariantCulture) ?? fallback : fallback;

    static int I(Dictionary<string, object?> a, string key, int fallback = 0) =>
        a.TryGetValue(key, out var v) && v != null ? Convert.ToInt32(v, CultureInfo.InvariantCulture) : fallback;

    static float F(Dictionary<string, object?> a, string key, float fallback = 0f) =>
        a.TryGetValue(key, out var v) && v != null ? Convert.ToSingle(v, CultureInfo.InvariantCulture) : fallback;

    static bool B(Dictionary<string, object?> a, string key, bool fallback = false)
    {
        if (!a.TryGetValue(key, out var v) || v == null)
            return fallback;
        if (v is bool b)
            return b;
        var s = Convert.ToString(v, CultureInfo.InvariantCulture) ?? "";
        return s == "1" || s.Equals("true", StringComparison.OrdinalIgnoreCase) || s.Equals("on", StringComparison.OrdinalIgnoreCase);
    }

    static string PrefabOf(ItemDrop.ItemData item) =>
        item.m_dropPrefab != null ? item.m_dropPrefab.name : item.m_shared?.m_name ?? "";

    static List<ItemDrop.ItemData> Stacks(string prefab) =>
        P.GetInventory().GetAllItems()
            .Where(i => i?.m_shared != null &&
                        (PrefabOf(i).Equals(prefab, StringComparison.OrdinalIgnoreCase) ||
                         i.m_shared.m_name.Equals(prefab, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(i => i.m_gridPos.y).ThenBy(i => i.m_gridPos.x)
            .ToList();

    static string Shown(ItemDrop.ItemData item) => DrakeRenameit.GetDisplayNameForUi(item, true) ?? "";

    /// <summary>The stack a hook acts on: prefab + index, or prefab + match (display name contains).</summary>
    static ItemDrop.ItemData Item(Dictionary<string, object?> a)
    {
        var prefab = S(a, "prefab", S(a, "item"));
        var match = S(a, "match");
        var all = prefab.Length > 0 ? Stacks(prefab) : P.GetInventory().GetAllItems().ToList();
        if (match.Length > 0)
            all = all.Where(i => Shown(i).IndexOf(match, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
        var index = I(a, "index");
        if (all.Count <= index)
            throw new InvalidOperationException($"no inventory stack for prefab '{prefab}' match '{match}' index {index} (have {all.Count})");
        return all[index];
    }

    static RenamePermissionOperation Op(string op) => op.Trim().ToLowerInvariant() switch
    {
        "name" or "rename" => RenamePermissionOperation.RenameItemName,
        "desc" or "description" => RenamePermissionOperation.RewriteDescription,
        "craftedby" or "crafter" or "crafted" => RenamePermissionOperation.EditCraftedByLabel,
        _ => throw new InvalidOperationException($"op must be name, desc or craftedBy (got '{op}')")
    };

    static bool Can(RenamePermissionOperation op, ItemDrop.ItemData item, bool showError) => op switch
    {
        RenamePermissionOperation.RenameItemName => DrakeRenameit.CanChangeName(item, showError),
        RenamePermissionOperation.RewriteDescription => DrakeRenameit.CanChangeDesc(item, showError),
        _ => DrakeRenameit.CanChangeCraftedByLabel(item, showError)
    };

    /// <summary>The menus cap typed text at the configured limit; tests get the same cap.</summary>
    static string Cap(RenamePermissionOperation op, string text)
    {
        var max = op switch
        {
            RenamePermissionOperation.RenameItemName => RenameitConfig.NameCharLimit,
            RenamePermissionOperation.RewriteDescription => RenameitConfig.DescCharLimit,
            _ => RenameitConfig.CraftedByCharLimit
        };
        return max > 0 && text.Length > max ? text.Substring(0, max) : text;
    }

    static string Loc(string s) => Localization.instance != null ? Localization.instance.Localize(s) : s;

    // ---------- permissions

    /// <summary>May the current player edit it? op = name | desc | craftedBy (the same gate the menus use).</summary>
    [TestHook("renameit.can")]
    static bool CanEdit(Dictionary<string, object?> args) => Can(Op(S(args, "op", "name")), Item(args), false);

    /// <summary>Why not: None, NotOwner, ExcludedByName, ExcludedByCategory, ExcludedStacks, UncraftedResourceBlocked, UnlockCostRequired, Global...Disabled.</summary>
    [TestHook("renameit.denial")]
    static string Denial(Dictionary<string, object?> args) =>
        RenamePermissionManager.TryGetDenial(Op(S(args, "op", "name")), Item(args), P).Reasons.ToString();

    /// <summary>The message a player sees when the menu will not open ("" when ShowDenialUi is off).</summary>
    [TestHook("renameit.blockedMessage")]
    static string BlockedMessage(Dictionary<string, object?> args) => Loc(DrakeRenameit.GetMenuBlockedReason(Item(args)));

    /// <summary>Would modifier + right-click open the action menu for this stack?</summary>
    [TestHook("renameit.menuOpens")]
    static bool MenuOpens(Dictionary<string, object?> args) => DrakeRenameit.AnyInventoryActionAvailable(Item(args));

    /// <summary>The inventory tooltip text RenameIt adds to (hint line, lock emoji, denial line), localized.</summary>
    [TestHook("renameit.tooltip")]
    static string Tooltip(Dictionary<string, object?> args)
    {
        var item = Item(args);
        foreach (var m in typeof(ItemDrop.ItemData).GetMethods(BindingFlags.Public | BindingFlags.Static))
        {
            if (m.Name != "GetTooltip")
                continue;
            var ps = m.GetParameters();
            if (ps.Length == 0 || ps[0].ParameterType != typeof(ItemDrop.ItemData))
                continue;
            var call = new object?[ps.Length];
            call[0] = item;
            for (var i = 1; i < ps.Length; i++)
            {
                var t = ps[i].ParameterType;
                call[i] = ps[i].HasDefaultValue ? ps[i].DefaultValue
                    : ps[i].Name.IndexOf("quality", StringComparison.OrdinalIgnoreCase) >= 0 ? item.m_quality
                    : t == typeof(int) ? (object)-1 : t == typeof(float) ? 1f : t == typeof(bool) ? false : null;
            }

            return Loc(m.Invoke(null, call) as string ?? "") + "\n" + DrakeRenameit.GetMenuTooltipLockSuffix(item);
        }

        throw new InvalidOperationException("ItemData.GetTooltip not found");
    }

    /// <summary>Is the Item Workshop window (or the Paper tab) open?</summary>
    [TestHook("renameit.windowOpen")]
    static bool WindowOpen() =>
        global::DrakeRenameit.UI.Toolkit.WorkshopPanel.IsOpen || PaperWallSession.IsActive;

    /// <summary>Close every RenameIt window (Workshop, tab host, wood popups) without saving.</summary>
    [TestHook("renameit.closeWindows")]
    static bool CloseWindows()
    {
        global::DrakeRenameit.UI.Toolkit.WorkshopPanel.HideFromHost();
        DrakeModsLibs.UI.DrakeTabHost.Close();
        global::DrakeRenameit.UI.UIPanels.CloseAllRenameEditingUi();
        if (PaperWallSession.IsActive)
            PaperWallSession.End();
        return true;
    }

    [TestHook("renameit.elevated")]
    static bool Elevated() => RenameitPermission.IsElevatedForOverrides(P);

    // ---------- editing (same apply code as the menus)

    /// <summary>
    /// Edit as the current player. op = name | desc | craftedBy, text ("" resets that field), label (crafted-by line label).
    /// Returns false (and shows the denial message, like the menu) when not allowed. Blank paper turns into a Written Page.
    /// </summary>
    [TestHook("renameit.edit")]
    static bool Edit(Dictionary<string, object?> args)
    {
        var item = Item(args);
        var op = Op(S(args, "op", "name"));
        var text = Cap(op, S(args, "text"));
        if (!Can(op, item, showError: true))
            return false;

        if (PaperItem.IsBlankPaper(item) && op != RenamePermissionOperation.EditCraftedByLabel)
        {
            if (text.Trim().Length == 0)
                return false;
            var written = PaperWriteConverter.PeelBlankToWritten(item,
                op == RenamePermissionOperation.RenameItemName ? text : null,
                op == RenamePermissionOperation.RewriteDescription ? text : null, P);
            return written != null;
        }

        DrakeRenameit.CurrentItem = item;
        try
        {
            switch (op)
            {
                case RenamePermissionOperation.RenameItemName:
                    DrakeRenameit.RenameItem(text);
                    break;
                case RenamePermissionOperation.RewriteDescription:
                    DrakeRenameit.RewriteItemDesc(text);
                    break;
                default:
                    if (item.m_crafterID == 0L && string.IsNullOrEmpty(item.m_crafterName))
                    {
                        item.m_crafterID = P.GetPlayerID();
                        item.m_crafterName = P.GetPlayerName();
                    }

                    CustomizeLibsAPI.SetCraftedByDisplay(item, text);
                    var label = S(args, "lineLabel");
                    if (label.Length > 0 && (RenameitConfig.CraftedByLabelCustomizable || RenameitPermission.IsElevatedForOverrides(P)))
                    {
                        var allowed = RenameitConfig.GetCraftedByAllowedLabelsList();
                        if (!allowed.Skip(1).Contains(label))
                            return false;
                        CustomizeLibsAPI.SetCraftedByLineLabel(item, label);
                    }

                    break;
            }
        }
        finally
        {
            DrakeRenameit.CurrentItem = null;
        }

        return true;
    }

    /// <summary>Reset: op = all (the Reset All button) or name / desc / craftedBy.</summary>
    [TestHook("renameit.reset")]
    static bool Reset(Dictionary<string, object?> args)
    {
        var item = Item(args);
        var op = S(args, "op", "all").ToLowerInvariant();
        if (op == "all")
        {
            if (!DrakeRenameit.CanResetAnyCustomization(item))
                return false;
            DrakeRenameit.ResetAllCustomizations(item);
            return true;
        }

        var o = Op(op);
        if (o != RenamePermissionOperation.EditCraftedByLabel && !Can(o, item, true))
            return false;
        switch (o)
        {
            case RenamePermissionOperation.RenameItemName: DrakeRenameit.resetName(item); break;
            case RenamePermissionOperation.RewriteDescription: DrakeRenameit.resetDesc(item); break;
            default: CustomizeLibsAPI.ClearCraftedByOverrides(item); break;
        }

        return true;
    }

    /// <summary>Name as shown in the inventory (with durability label, localized).</summary>
    [TestHook("renameit.name")]
    static string Name(Dictionary<string, object?> args) => Shown(Item(args));

    [TestHook("renameit.desc")]
    static string Desc(Dictionary<string, object?> args) => Loc(DrakeRenameit.getPropperDesc(Item(args)));

    /// <summary>Crafted-by line as shown: "Label: name".</summary>
    [TestHook("renameit.craftedBy")]
    static string CraftedBy(Dictionary<string, object?> args)
    {
        var item = Item(args);
        var label = item.m_customData != null && item.m_customData.TryGetValue(DrakeRenameit.DrakeCraftedByLineLabel, out var l) && l.Length > 0
            ? l
            : Loc("$item_crafter");
        return label + ": " + DrakeRenameit.getCraftedByDisplay(item);
    }

    /// <summary>The real crafter (owner) name, or "" when unowned.</summary>
    [TestHook("renameit.owner")]
    static string Owner(Dictionary<string, object?> args) => Item(args).m_crafterName ?? "";

    /// <summary>Make a stack someone else's (owner = name, id optional) or unowned (owner = "").</summary>
    [TestHook("renameit.setOwner")]
    static bool SetOwner(Dictionary<string, object?> args)
    {
        var item = Item(args);
        var owner = S(args, "owner");
        if (owner.Equals("me", StringComparison.OrdinalIgnoreCase) || owner.Equals(P.GetPlayerName(), StringComparison.OrdinalIgnoreCase))
        {
            item.m_crafterName = P.GetPlayerName();
            item.m_crafterID = P.GetPlayerID();
            return true;
        }

        item.m_crafterName = owner;
        item.m_crafterID = owner.Length == 0 ? 0L : I(args, "id", 300000 + Math.Abs(owner.GetStableHashCode() % 100000));
        return true;
    }

    [TestHook("renameit.quality")]
    static int Quality(Dictionary<string, object?> args) => Item(args).m_quality;

    /// <summary>Custom data value on the stack (e.g. Drake_Rename), "" when missing.</summary>
    [TestHook("renameit.data")]
    static string Data(Dictionary<string, object?> args)
    {
        var item = Item(args);
        return item.m_customData != null && item.m_customData.TryGetValue(S(args, "key"), out var v) ? v : "";
    }

    // ---------- unlock cost

    [TestHook("renameit.unlock")]
    static bool Unlock(Dictionary<string, object?> args) => DrakeRenameit.TryPayRenameUnlock(Item(args));

    [TestHook("renameit.isUnlocked")]
    static bool IsUnlocked(Dictionary<string, object?> args) => DrakeRenameit.IsRenameUnlocked(Item(args));

    [TestHook("renameit.showsUnlock")]
    static bool ShowsUnlock(Dictionary<string, object?> args) => DrakeRenameit.ShowUnlockButton(Item(args));

    // ---------- shared (public rewrite)

    /// <summary>Turn Shared on/off as the current player; false when they may not.</summary>
    [TestHook("renameit.share")]
    static bool Share(Dictionary<string, object?> args)
    {
        var item = Item(args);
        if (!RenamePermissionManager.CanTogglePublicRewriteFlag(item, P))
            return false;
        RenamePermissionManager.SetPublicRewriteFlag(item, B(args, "on", true));
        return true;
    }

    [TestHook("renameit.isShared")]
    static bool IsShared(Dictionary<string, object?> args) => RenamePermissionManager.HasPublicRewriteFlag(Item(args));

    // ---------- stacks

    [TestHook("renameit.stackCount")]
    static int StackCount(Dictionary<string, object?> args) => Stacks(S(args, "prefab")).Count;

    /// <summary>Amounts of each stack, in inventory order, e.g. "5,5".</summary>
    [TestHook("renameit.stackSizes")]
    static string StackSizes(Dictionary<string, object?> args) =>
        string.Join(",", Stacks(S(args, "prefab")).Select(i => i.m_stack));

    /// <summary>Split <c>amount</c> off a stack into a free slot (like shift-dragging).</summary>
    [TestHook("renameit.split")]
    static bool Split(Dictionary<string, object?> args)
    {
        var item = Item(args);
        var amount = I(args, "amount", item.m_stack / 2);
        if (amount <= 0 || amount >= item.m_stack)
            return false;
        var inv = P.GetInventory();
        // a truly empty cell (AddItem onto an occupied cell would merge right back)
        var free = new Vector2i(-1, -1);
        for (var y = 0; y < inv.GetHeight() && free.x < 0; y++)
        for (var x = 0; x < inv.GetWidth(); x++)
        {
            if (inv.GetItemAt(x, y) != null)
                continue;
            free = new Vector2i(x, y);
            break;
        }

        if (free.x < 0)
            return false;
        var clone = item.Clone();
        clone.m_stack = amount;
        item.m_stack -= amount;
        clone.m_gridPos = free;
        // straight into the grid like the split dialog: AddItem would stack it back onto the source first
        inv.GetAllItems().Add(clone);
        // Changed(bool success = false, bool cheatedStateChanged = false) on 1.0; pass defaults for whatever it takes
        var changed = AccessTools.Method(typeof(Inventory), "Changed");
        if (changed != null)
            changed.Invoke(inv, changed.GetParameters().Select(p => p.HasDefaultValue ? p.DefaultValue : null).ToArray());
        return true;
    }

    /// <summary>Drag stack <c>from</c> onto stack <c>to</c> (indexes of the same prefab). True when they merged.</summary>
    [TestHook("renameit.dragMerge")]
    static bool DragMerge(Dictionary<string, object?> args)
    {
        var prefab = S(args, "prefab");
        var stacks = Stacks(prefab);
        var from = I(args, "from", 1);
        var to = I(args, "to", 0);
        if (stacks.Count <= Math.Max(from, to))
            throw new InvalidOperationException($"need at least {Math.Max(from, to) + 1} stacks of {prefab}");
        var src = stacks[from];
        var dst = stacks[to];
        var inv = P.GetInventory();
        var before = stacks.Count;
        var move = AccessTools.Method(typeof(Inventory), "MoveItemToThis",
            new[] { typeof(Inventory), typeof(ItemDrop.ItemData), typeof(int), typeof(int), typeof(int) });
        if (move == null)
            throw new InvalidOperationException("Inventory.MoveItemToThis not found");
        move.Invoke(inv, new object[] { inv, src, src.m_stack, dst.m_gridPos.x, dst.m_gridPos.y });
        return Stacks(prefab).Count < before;
    }

    /// <summary>Drop every stack of the prefab on the ground and pick them all up again (the auto-pickup merge path).</summary>
    [TestHook("renameit.dropAndPickup")]
    static int DropAndPickup(Dictionary<string, object?> args)
    {
        var prefab = S(args, "prefab");
        var player = P;
        var inv = player.GetInventory();
        var drops = new List<ItemDrop>();
        foreach (var item in Stacks(prefab))
        {
            inv.RemoveItem(item);
            var pos = player.transform.position + player.transform.forward * 1.2f + Vector3.up * 0.3f;
            var drop = ItemDrop.DropItem(item, item.m_stack, pos, Quaternion.identity);
            if (drop != null)
                drops.Add(drop);
        }

        var pickup = typeof(Humanoid).GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .FirstOrDefault(m => m.Name == "Pickup" && m.GetParameters().Length >= 1 && m.GetParameters()[0].ParameterType == typeof(GameObject))
                     ?? throw new InvalidOperationException("Humanoid.Pickup not found");
        foreach (var drop in drops)
        {
            var ps = pickup.GetParameters();
            var call = new object[ps.Length];
            call[0] = drop.gameObject;
            for (var i = 1; i < ps.Length; i++)
                call[i] = ps[i].ParameterType == typeof(bool) ? false : (ps[i].HasDefaultValue ? ps[i].DefaultValue! : null!);
            pickup.Invoke(player, call);
        }

        return Stacks(prefab).Count;
    }

    // ---------- durability, upgrade

    /// <summary>Set durability to a fraction of max (0 = broken, 1 = pristine).</summary>
    [TestHook("renameit.durability")]
    static bool Durability(Dictionary<string, object?> args)
    {
        var item = Item(args);
        if (!item.m_shared.m_useDurability)
            return false;
        item.m_durability = item.GetMaxDurability() * Mathf.Clamp01(F(args, "fraction", 1f));
        return true;
    }

    /// <summary>Upgrade the stack one level through the game's own crafting code (no materials or station needed).</summary>
    [TestHook("renameit.upgrade")]
    static bool Upgrade(Dictionary<string, object?> args)
    {
        var item = Item(args);
        var gui = InventoryGui.instance ?? throw new InvalidOperationException("no InventoryGui");
        var recipe = ObjectDB.instance.GetRecipe(item) ?? throw new InvalidOperationException("no recipe for that item");
        if (item.m_quality >= item.m_shared.m_maxQuality)
            return false;

        var before = item.m_quality;
        var noCost = AccessTools.Field(typeof(Player), "m_noPlacementCost");
        var oldNoCost = noCost?.GetValue(P);
        AccessTools.Field(typeof(InventoryGui), "m_craftRecipe")?.SetValue(gui, recipe);
        AccessTools.Field(typeof(InventoryGui), "m_craftUpgradeItem")?.SetValue(gui, item);
        try
        {
            noCost?.SetValue(P, true);
            AccessTools.Method(typeof(InventoryGui), "DoCrafting")?.Invoke(gui, new object[] { P });
        }
        finally
        {
            noCost?.SetValue(P, oldNoCost);
            AccessTools.Field(typeof(InventoryGui), "m_craftUpgradeItem")?.SetValue(gui, null);
        }

        return P.GetInventory().GetAllItems().Any(i => i.m_shared.m_name == item.m_shared.m_name && i.m_quality > before);
    }

    // ---------- item stands

    static ItemStand Stand(Dictionary<string, object?> args, GameObject? target)
    {
        var stand = target != null ? target.GetComponentInChildren<ItemStand>() : null;
        if (stand == null)
        {
            var best = float.MaxValue;
            foreach (var s in UnityEngine.Object.FindObjectsOfType<ItemStand>())
            {
                var d = Vector3.Distance(s.transform.position, P.transform.position);
                if (d < best)
                {
                    best = d;
                    stand = s;
                }
            }
        }

        return stand ?? throw new InvalidOperationException("no item stand nearby");
    }

    /// <summary>Put the stack on an item stand (target = tagged stand, else the nearest).</summary>
    [TestHook("renameit.standPut")]
    static bool StandPut(Dictionary<string, object?> args, GameObject? target = null)
    {
        var stand = Stand(args, target);
        return stand.UseItem(P, Item(args));
    }

    /// <summary>Take the item off the stand into the inventory (as if pressing E and picking it up).</summary>
    [TestHook("renameit.standTake")]
    static bool StandTake(Dictionary<string, object?> args, GameObject? target = null)
    {
        var stand = Stand(args, target);
        var before = new HashSet<ItemDrop>(UnityEngine.Object.FindObjectsOfType<ItemDrop>());
        // vanilla only takes from a stand on hold-E (and silently refuses without ward access)
        if (!stand.Interact(P, true, false))
            return false;

        foreach (var drop in UnityEngine.Object.FindObjectsOfType<ItemDrop>())
        {
            if (before.Contains(drop) || Vector3.Distance(drop.transform.position, stand.transform.position) > 4f)
                continue;
            return P.Pickup(drop.gameObject, false, false);
        }

        return false;
    }

    /// <summary>The stand's hover text (what a player looking at it sees), localized.</summary>
    [TestHook("renameit.standHover")]
    static string StandHover(Dictionary<string, object?> args, GameObject? target = null) => Loc(Stand(args, target).GetHoverText() ?? "");

    // ---------- paper

    [TestHook("renameit.isWritten")]
    static bool IsWritten(Dictionary<string, object?> args) => PaperItem.IsWrittenPaper(Item(args));

    [TestHook("renameit.isPrinted")]
    static bool IsPrinted(Dictionary<string, object?> args) => PaperItem.IsPrintedPaper(Item(args));

    [TestHook("renameit.isCopy")]
    static bool IsCopy(Dictionary<string, object?> args) => PaperCopyMark.IsCopy(Item(args));

    /// <summary>copies = N separate copies; immutable = true makes one Printed stack of N instead. Uses blank paper.</summary>
    [TestHook("renameit.copy")]
    static bool Copy(Dictionary<string, object?> args)
    {
        var source = Item(args);
        var n = I(args, "copies", 1);
        string err;
        var ok = B(args, "immutable")
            ? PaperCopyService.TryPrintStack(source, n, P, out err)
            : PaperCopyService.TryCopyPages(source, n, P, out err);
        if (!ok)
            RenameitConfig.Log?.LogInfo($"[TestHook] copy refused: {Loc(err)}");
        return ok;
    }

    [TestHook("renameit.canRecycle")]
    static bool CanRecycle(Dictionary<string, object?> args) => PaperRecycleService.CanRecycle(Item(args), P);

    [TestHook("renameit.recycle")]
    static bool Recycle(Dictionary<string, object?> args)
    {
        var ok = PaperRecycleService.TryRecycleToBlank(Item(args), P, out var err);
        if (!ok)
            RenameitConfig.Log?.LogInfo($"[TestHook] recycle refused: {Loc(err)}");
        return ok;
    }

    /// <summary>The paper recipe as "enabled=True station=... cost=Wood:1,Coal:1".</summary>
    [TestHook("renameit.paperRecipe")]
    static string PaperRecipe()
    {
        foreach (var r in ObjectDB.instance.m_recipes)
        {
            if (r == null || r.m_item == null || r.m_item.name != PaperItem.PrefabName)
                continue;
            var cost = string.Join(",", r.m_resources.Where(x => x?.m_resItem != null).Select(x => x.m_resItem.name + ":" + x.m_amount));
            var station = r.m_craftingStation != null ? r.m_craftingStation.name : "";
            return $"enabled={r.m_enabled} station={station} cost={cost}";
        }

        return "missing";
    }

    /// <summary>Max stack size of an item prefab (e.g. Drakes_PieceOfPaper).</summary>
    [TestHook("renameit.maxStack")]
    static int MaxStack(Dictionary<string, object?> args)
    {
        var go = ObjectDB.instance.GetItemPrefab(S(args, "prefab")) ?? throw new InvalidOperationException("unknown prefab");
        return go.GetComponent<ItemDrop>().m_itemData.m_shared.m_maxStackSize;
    }

    /// <summary>Item type of an item prefab (Material, Misc...).</summary>
    [TestHook("renameit.itemType")]
    static string ItemType(Dictionary<string, object?> args)
    {
        var go = ObjectDB.instance.GetItemPrefab(S(args, "prefab")) ?? throw new InvalidOperationException("unknown prefab");
        return go.GetComponent<ItemDrop>().m_itemData.m_shared.m_itemType.ToString();
    }

    /// <summary>Is the hammer piece placeable (Drakes_PaperBlank_Upright, Drakes_PaperBlank_Laying, Drakes_PaperStack)?</summary>
    [TestHook("renameit.pieceEnabled")]
    static bool PieceEnabled(Dictionary<string, object?> args)
    {
        var go = ZNetScene.instance.GetPrefab(S(args, "prefab")) ?? throw new InvalidOperationException("unknown piece prefab");
        var piece = go.GetComponent<Piece>() ?? throw new InvalidOperationException("not a piece");
        return piece.m_enabled;
    }

    // ---------- pinned pages

    static PaperWrittenVessel Page()
    {
        PaperWrittenVessel? best = null;
        var bestDist = 30f;
        foreach (var v in UnityEngine.Object.FindObjectsOfType<PaperWrittenVessel>())
        {
            if (v == null || PaperWrittenPlace.IsPlacementGhost(v.gameObject))
                continue;
            var d = Vector3.Distance(v.transform.position, P.transform.position);
            if (d < bestDist)
            {
                bestDist = d;
                best = v;
            }
        }

        return best ?? throw new InvalidOperationException("no pinned page within 30 m");
    }

    /// <summary>
    /// Pin a Written Page from the inventory flat on the ground <c>dz</c> metres in front (through the real place-mode code).
    /// Set BlankPaperPlaceOrientation to "Horizontal Only" first so it lies flat.
    /// </summary>
    [TestHook("renameit.pin")]
    static bool Pin(Dictionary<string, object?> args)
    {
        var item = Item(args);
        var player = P;
        PaperWrittenPlace.BeginPlace(item);
        var table = item.m_shared?.m_buildPieces;
        var piece = table != null ? table.GetSelectedPiece() : null;
        if (piece == null)
            throw new InvalidOperationException("place mode did not start (is it a Written Page on the hotbar, and PaperPlaceEnabled on?)");

        Vector3 pos;
        Quaternion rot;
        if (B(args, "wall"))
        {
            // onto the wall in front of the camera, like aiming the placement ghost at it (orientation: Vertical Only)
            var cam = GameCamera.instance != null ? GameCamera.instance.transform : player.transform;
            var mask = LayerMask.GetMask("piece", "piece_nonsolid", "Default", "static_solid", "terrain");
            if (!Physics.Raycast(cam.position, cam.forward, out var hit, 8f, mask))
                throw new InvalidOperationException("no wall in front of the camera (use lookAt on a wall first)");
            pos = hit.point + hit.normal * 0.01f;
            rot = Quaternion.LookRotation(-hit.normal, Vector3.up);
        }
        else
        {
            pos = player.transform.position + player.transform.forward * F(args, "dz", 2f);
            if (ZoneSystem.instance != null && ZoneSystem.instance.GetGroundHeight(pos, out var ground))
                pos.y = ground + 0.02f;
            rot = Quaternion.Euler(0f, player.transform.eulerAngles.y + 180f, 0f);
        }

        var place = typeof(Player).GetMethods(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
            .FirstOrDefault(m => m.Name == "PlacePiece" && m.GetParameters().Length >= 3 && m.GetParameters()[0].ParameterType == typeof(Piece))
                    ?? throw new InvalidOperationException("Player.PlacePiece not found");
        var ps = place.GetParameters();
        var call = new object[ps.Length];
        call[0] = piece;
        call[1] = pos;
        call[2] = rot;
        for (var i = 3; i < ps.Length; i++)
            call[i] = ps[i].ParameterType == typeof(bool) ? false : (ps[i].HasDefaultValue ? ps[i].DefaultValue! : null!);
        var result = place.Invoke(player, call);
        return result is not bool b || b;
    }

    /// <summary>The text actually visible on the pinned page (cut where the page overflows), e.g. to check landscape + big font.</summary>
    [TestHook("renameit.pageVisibleText")]
    static string PageVisibleText()
    {
        var widget = PaperNotePageText.FindPageWidget(Page().gameObject) ?? throw new InvalidOperationException("the page shows no text");
        widget.ForceMeshUpdate();
        var parsed = widget.GetParsedText() ?? "";
        var cut = widget.isTextOverflowing && widget.firstOverflowCharacterIndex >= 0 && widget.firstOverflowCharacterIndex < parsed.Length
            ? parsed.Substring(0, widget.firstOverflowCharacterIndex)
            : parsed;
        return cut.Replace("\n", " / ");
    }

    /// <summary>Remove every pinned page within 40 m (the world keeps pages between tests after a relog).</summary>
    [TestHook("renameit.clearPages")]
    static int ClearPages()
    {
        var removed = 0;
        foreach (var v in UnityEngine.Object.FindObjectsOfType<PaperWrittenVessel>())
        {
            if (v == null || PaperWrittenPlace.IsPlacementGhost(v.gameObject) || Vector3.Distance(v.transform.position, P.transform.position) > 40f)
                continue;
            var nv = v.GetComponent<ZNetView>();
            if (nv != null && nv.IsValid())
            {
                if (!nv.IsOwner())
                    nv.ClaimOwnership();
                nv.Destroy();
            }
            else
            {
                UnityEngine.Object.Destroy(v.gameObject);
            }

            removed++;
        }

        return removed;
    }

    [TestHook("renameit.pageHover")]
    static string PageHover() => Loc(Page().GetHoverText() ?? "");

    /// <summary>Take the nearest pinned page (hold = true is hold-E).</summary>
    [TestHook("renameit.pageTake")]
    static bool PageTake(Dictionary<string, object?> args)
    {
        var page = Page();
        // count sheets, not stacks: a taken print joins its stack
        int Sheets() => P.GetInventory().GetAllItems().Where(PaperItem.IsWrittenLike).Sum(i => i.m_stack);
        var before = Sheets();
        page.Interact(P, B(args, "hold", true), false);
        return Sheets() > before;
    }

    /// <summary>The nearest pinned page as an item (name / desc / owner / flags), for the read hooks below.</summary>
    static ItemDrop.ItemData PageItem() => Page().BuildItemFromZdoPublic() ?? throw new InvalidOperationException("page has no item data");

    [TestHook("renameit.pageName")]
    static string PageName() => Shown(PageItem());

    [TestHook("renameit.pageDesc")]
    static string PageDesc() => Loc(DrakeRenameit.getPropperDesc(PageItem()));

    /// <summary>May the current player open the wall editor for the nearest page (ward permit, creator or override)?</summary>
    [TestHook("renameit.pageMayEdit")]
    static bool PageMayEdit() =>
        AccessTools.Method(typeof(PaperWrittenVessel), "LocalMayEditWallPaper")?.Invoke(Page(), null) is true;

    /// <summary>Edit the pinned page in place (Shift+E editor) as the current player: op, text. False when not allowed.</summary>
    [TestHook("renameit.pageEdit")]
    static bool PageEdit(Dictionary<string, object?> args)
    {
        var page = Page();
        if (!PageMayEdit())
            return false;
        var item = page.BuildItemFromZdoPublic();
        if (item == null)
            return false;
        PaperWallSession.Begin(page, item);
        try
        {
            var op = Op(S(args, "op", "name"));
            if (!Can(op, item, true))
                return false;
            var text = Cap(op, S(args, "text"));
            if (op == RenamePermissionOperation.RenameItemName)
                DrakeRenameit.RenameItem(text);
            else if (op == RenamePermissionOperation.RewriteDescription)
                DrakeRenameit.RewriteItemDesc(text);
            else
                CustomizeLibsAPI.SetCraftedByDisplay(item, text);
            PaperWallSession.FlushCurrentItemToVessel();
            return true;
        }
        finally
        {
            PaperWallSession.End();
        }
    }

    /// <summary>Shared (others may rewrite) on the pinned page, as the current player. False when they may not change it.</summary>
    [TestHook("renameit.pageShare")]
    static bool PageShare(Dictionary<string, object?> args)
    {
        var page = Page();
        var item = page.BuildItemFromZdoPublic();
        if (item == null || !RenamePermissionManager.CanTogglePublicRewriteFlag(item, P))
            return false;
        RenamePermissionManager.SetPublicRewriteFlag(item, B(args, "on", true));
        page.ApplyCustomizationFromItem(item);
        return true;
    }

    [TestHook("renameit.pageIsShared")]
    static bool PageIsShared() => RenamePermissionManager.HasPublicRewriteFlag(PageItem());

    /// <summary>Make public (anyone may take it off the wall), as the current player. False when they may not.</summary>
    [TestHook("renameit.pageTakePublic")]
    static bool PageTakePublic(Dictionary<string, object?> args)
    {
        var page = Page();
        if (!page.LocalMayToggleTakePublicPublic())
            return false;
        page.RequestSetTakePublic(B(args, "on", true));
        return true;
    }

    [TestHook("renameit.pageIsTakePublic")]
    static bool PageIsTakePublic() => Page().ReadTakePublicPublic();

    /// <summary>Copy the pinned page into the inventory (Paper tab on the wall): copies, immutable.</summary>
    [TestHook("renameit.pageCopy")]
    static bool PageCopy(Dictionary<string, object?> args)
    {
        var item = PageItem();
        var n = I(args, "copies", 1);
        string err;
        var ok = B(args, "immutable")
            ? PaperCopyService.TryPrintStack(item, n, P, out err)
            : PaperCopyService.TryCopyPages(item, n, P, out err);
        if (!ok)
            RenameitConfig.Log?.LogInfo($"[TestHook] page copy refused: {Loc(err)}");
        return ok;
    }

    /// <summary>Page style on the wall: font (level 1-7), landscape.</summary>
    [TestHook("renameit.pageStyle")]
    static string PageStyle(Dictionary<string, object?> args)
    {
        var page = Page();
        if (args.ContainsKey("font") || args.ContainsKey("landscape"))
        {
            var size = args.ContainsKey("font") ? PaperFontScale.LevelToSize(I(args, "font", 3)) : page.ReadFontSize();
            page.ApplyPaperStyle(size, args.ContainsKey("landscape") ? B(args, "landscape") : page.ReadLandscape());
        }

        return $"font={page.ReadFontSize().ToString("0.###", CultureInfo.InvariantCulture)} landscape={page.ReadLandscape()}";
    }
}
