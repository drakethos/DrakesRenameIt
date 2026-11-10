using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;

namespace DrakeRenameit.Books;

/// <summary>Shelf rules: only books go in (unless the server and the shelf's owner both allow more), and aiming at a spine reads it.</summary>
internal static class BookShelfRules
{
    internal static bool Allows(Inventory inventory, ItemDrop.ItemData? item)
    {
        if (item == null || !BookShelfBehaviour.ByInventory.TryGetValue(inventory, out var shelf))
            return true;
        if (BookItem.IsBook(item) || shelf.AllowsNonBooks())
            return true;
        ValheimHudMessage.Show(Player.m_localPlayer, MessageHud.MessageType.Center,
            RenameitConfig.ShelfAllowNonBooks ? "This shelf is for books only. Its owner can allow other things (Shift+Use)." : "Shelves on this server hold books only.");
        return false;
    }

    /// <summary>Inventory methods that put an existing item into an inventory (a parameter named "item"), by return type.</summary>
    internal static IEnumerable<MethodBase> Targets(bool returnsBool) =>
        AccessTools.GetDeclaredMethods(typeof(Inventory))
            .Where(m => (m.Name == "AddItem" || m.Name == "MoveItemToThis")
                        && m.GetParameters().Any(p => p.Name == "item" && p.ParameterType == typeof(ItemDrop.ItemData))
                        && (m.ReturnType == typeof(bool)) == returnsBool)
            .Cast<MethodBase>();
}

[HarmonyPatch]
internal static class BookShelfAddBool
{
    static IEnumerable<MethodBase> TargetMethods() => BookShelfRules.Targets(returnsBool: true);

    static bool Prefix(Inventory __instance, ItemDrop.ItemData item, ref bool __result)
    {
        if (BookShelfRules.Allows(__instance, item))
            return true;
        __result = false;
        return false;
    }
}

[HarmonyPatch]
internal static class BookShelfAddVoid
{
    static IEnumerable<MethodBase> TargetMethods() => BookShelfRules.Targets(returnsBool: false);

    static bool Prefix(Inventory __instance, ItemDrop.ItemData item) => BookShelfRules.Allows(__instance, item);
}

[HarmonyPatch]
internal static class BookShelfInteract
{
    [HarmonyPostfix]
    [HarmonyPatch(typeof(Container), nameof(Container.GetHoverText))]
    static void HoverText(Container __instance, ref string __result)
    {
        var shelf = __instance.GetComponent<BookShelfBehaviour>();
        if (shelf == null)
            return;
        var book = shelf.AimedBook();
        if (book == null)
        {
            // The shelf's own name (RenameIt's piece rename) on the title line, as the chest window shows it.
            if (Pieces.PieceRename.DisplayName(__instance) is { } custom && Localization.instance != null)
            {
                var plain = Localization.instance.Localize(__instance.m_name);
                var at = __result.IndexOf(plain, StringComparison.Ordinal);
                if (at >= 0)
                    __result = __result.Substring(0, at) + custom + __result.Substring(at + plain.Length);
            }

            // Inside a ward LockSmith's Shift+E menu opens instead, with the Shelf tab in it (its own hover line says so).
            if (!LockSmithMenuOpens(__instance))
                __result += Localize("\n[<color=yellow><b>Shift</b></color>+<color=yellow><b>$KEY_Use</b></color>] Shelf settings");
            return;
        }

        // Like a shop case: the thing you point at, then what it stands in.
        // GetHoverName carries the shelf's own name when it has one (RenameIt's piece rename).
        var where = Localization.instance != null ? Localization.instance.Localize(__instance.GetHoverName()) : __instance.m_name;
        var keys = "\n[<color=yellow><b>$KEY_Use</b></color>] Read";
        if (CanEdit(__instance, book))
            keys += "\n[<color=yellow><b>Shift</b></color>+<color=yellow><b>$KEY_Use</b></color>] Edit";
        __result = BookData.DisplayTitle(book) + "\n<color=#b9ab91>" + where + "</color>" + Localize(keys);
    }

    /// <summary>
    /// E / Shift+E on a book spine: read, or the full editor. Handled before the container sees the key, so the chest does not open
    /// and LockSmith's Shift+E menu does not open on top of the book.
    /// </summary>
    [HarmonyPrefix]
    [HarmonyPriority(Priority.High)]
    [HarmonyPatch(typeof(Player), "Interact", new[] { typeof(UnityEngine.GameObject), typeof(bool), typeof(bool) })]
    static bool PlayerInteract(Player __instance, UnityEngine.GameObject go, bool hold, bool alt)
    {
        if (__instance != Player.m_localPlayer || go == null || !RenameitConfig.BooksEnabled)
            return true;
        var shelf = go.GetComponentInParent<BookShelfBehaviour>();
        var book = shelf != null ? shelf.AimedBook() : null;
        if (shelf == null || book == null)
            return true;
        if (hold || __instance.InAttack() || __instance.InDodge())
            return false;
        var container = shelf.GetComponent<Container>();
        if (container == null)
            return true;

        // Nothing leaves the shelf; edits are written back onto the shelved book.
        var canEdit = CanEdit(container, book);
        var copy = book.Clone();
        copy.m_customData = new Dictionary<string, string>(book.m_customData ?? new Dictionary<string, string>());
        Action<ItemDrop.ItemData>? save = canEdit ? edited => WriteBack(container, book, edited) : null;
        if (alt && canEdit)
            BookReaderPanel.ShowFull(copy, canEdit: true, onSave: save);
        else
            BookReaderPanel.Show(copy, canEdit, onSave: save);
        return false;
    }

    /// <summary>
    /// Shift+E on the shelf itself (not a spine). Runs after other mods: with LockSmith installed its menu opens with our Shelf tab;
    /// when nothing else took the key, the shelf's own layout window opens.
    /// </summary>
    [HarmonyPrefix]
    [HarmonyPriority(Priority.Low)]
    [HarmonyPatch(typeof(Container), nameof(Container.Interact))]
    static bool Interact(Container __instance, Humanoid character, bool hold, bool alt, ref bool __result, bool __runOriginal)
    {
        var shelf = __instance.GetComponent<BookShelfBehaviour>();
        if (shelf == null || character != Player.m_localPlayer)
            return true;
        if (!__runOriginal)
            return false; // another mod (LockSmith) already handled this press
        if (hold)
        {
            __result = false;
            return false;
        }

        if (!alt)
            return true; // E opens it like a chest
        ShelfSettingsPanel.Show(shelf, (Player)character);
        __result = true;
        return false;
    }

    /// <summary>The hover text reaches us already localized, so lines added here must be localized themselves (key names).</summary>
    static string Localize(string text) => Localization.instance != null ? Localization.instance.Localize(text) : text;

    static readonly System.Reflection.MethodInfo? HasLockMenu =
        AccessTools.TypeByName("LockSmith.UI.PieceAccessMenu") is { } menu ? AccessTools.Method(menu, "HasLockMenu", new[] { typeof(Container) }) : null;

    /// <summary>True when LockSmith is installed and its Shift+E menu would open here for this player (an active ward with access).</summary>
    static bool LockSmithMenuOpens(Container container)
    {
        try
        {
            return HasLockMenu != null && (bool)HasLockMenu.Invoke(null, new object[] { container })!;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>The book's maker (or an admin / VIP) may edit it on the shelf, unless someone has the shelf open as a chest.</summary>
    static bool CanEdit(Container shelf, ItemDrop.ItemData book) =>
        BookAccess.CanEditItem(book) && !(shelf.IsInUse() && !InventoryGui.IsVisible());

    /// <summary>Copy the edited text and cover onto the book that is on the shelf, and let the shelf save its contents.</summary>
    static void WriteBack(Container shelf, ItemDrop.ItemData original, ItemDrop.ItemData edited)
    {
        if (shelf == null || shelf.GetComponent<ZNetView>() is not { } nview || !nview.IsValid())
            return;
        var inventory = shelf.GetInventory();
        var live = inventory?.GetItemAt(original.m_gridPos.x, original.m_gridPos.y);
        if (live == null || !BookItem.IsBook(live))
            return;
        nview.ClaimOwnership();
        live.m_customData = new Dictionary<string, string>(edited.m_customData);
        BookShelfBehaviour.NotifyInventoryChanged(inventory!);
    }
}
