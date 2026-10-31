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
        __result = book != null
            ? BookData.DisplayTitle(book) + "\n[<color=yellow><b>$KEY_Use</b></color>] Read\nShelf settings: Shift+E"
            : __result + "\nShelf settings: Shift+E";
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Container), nameof(Container.Interact))]
    static bool Interact(Container __instance, Humanoid character, bool hold, bool alt, ref bool __result)
    {
        var shelf = __instance.GetComponent<BookShelfBehaviour>();
        if (shelf == null || character != Player.m_localPlayer)
            return true;
        if (hold)
        {
            __result = false;
            return false;
        }

        if (alt)
        {
            ShelfSettingsPanel.Show(shelf, (Player)character);
            __result = true;
            return false;
        }

        var book = shelf.AimedBook();
        if (book == null)
            return true;
        // Read in place: a shelf copy is read-only, nothing leaves the shelf.
        var copy = book.Clone();
        copy.m_customData = new Dictionary<string, string>(book.m_customData ?? new Dictionary<string, string>());
        BookReaderPanel.Show(copy, canEdit: false);
        __result = true;
        return false;
    }
}
