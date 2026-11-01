using System;
using System.Collections.Generic;
using BepInEx.Logging;
using DrakeRenameit.Paper.Items;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DrakeRenameit.Books;

/// <summary>
/// The Book item: crafted from paper and leather, one item type whose title, cover colour, size, and pages live in item custom
/// data (<see cref="BookData"/>). Use reads it. The dropped / inventory model is the default medium book; the placed piece and
/// shelf spines draw each book's own colour and size.
/// </summary>
internal static class BookItem
{
    internal const string PrefabName = "Drakes_Book";
    internal const string TokenName = "$item_drakes_book";
    const string TokenDesc = "$item_drakes_book_desc";
    /// <summary>Donor for ItemDrop / physics / networking only; its visuals are discarded.</summary>
    const string CloneSource = "LeatherScraps";

    static ManualLogSource? _log;

    internal static void Register(ManualLogSource log)
    {
        _log = log;
        PrefabManager.OnVanillaPrefabsAvailable -= AddBook;
        PrefabManager.OnVanillaPrefabsAvailable += AddBook;
    }

    static void AddBook()
    {
        PrefabManager.OnVanillaPrefabsAvailable -= AddBook;
        try
        {
            var loc = LocalizationManager.Instance.GetLocalization();
            loc.AddTranslation("English", "item_drakes_book", "Book");
            // Hover shows the title only, so the description stays blank.
            loc.AddTranslation("English", "item_drakes_book_desc", " ");
            loc.AddTranslation("Spanish", "item_drakes_book", "Libro");
            loc.AddTranslation("Spanish", "item_drakes_book_desc", " ");

            var config = new ItemConfig
            {
                Name = TokenName,
                Description = TokenDesc,
                Enabled = RenameitConfig.BooksEnabled,
                Amount = 1,
            };
            var icon = BookAssets.Icon;
            if (icon != null)
                config.Icon = icon;
            var station = RenameitConfig.BookCraftingStation?.Trim() ?? "";
            config.CraftingStation = string.IsNullOrEmpty(station) ? null : station;
            foreach (var (prefab, amount) in PaperItem.ParseCost(RenameitConfig.BookCost))
                config.AddRequirement(prefab, amount);

            var book = new CustomItem(PrefabName, CloneSource, config);
            var shared = book.ItemDrop?.m_itemData?.m_shared;
            if (shared != null)
            {
                shared.m_name = TokenName;
                shared.m_description = TokenDesc;
                shared.m_maxStackSize = 1;
                shared.m_itemType = ItemDrop.ItemData.ItemType.Misc;
                shared.m_weight = 1f;
                shared.m_value = 0;
                shared.m_teleportable = true;
                shared.m_questItem = false;
                shared.m_maxQuality = 1;
                shared.m_useDurability = false;
            }

            BuildVisual(book.ItemPrefab);
            ItemManager.Instance.AddItem(book);
            if (book.ItemDrop?.m_itemData != null)
            {
                book.ItemDrop.m_itemData.m_dropPrefab = book.ItemPrefab;
                BookData.SetColor(book.ItemDrop.m_itemData, BookData.CoverColors[0]);
            }
            _log?.LogInfo($"[Books] Registered {PrefabName} (enabled={RenameitConfig.BooksEnabled}, cost='{RenameitConfig.BookCost}').");
        }
        catch (Exception ex)
        {
            _log?.LogError($"[Books] Failed to register the book item: {ex}");
        }
    }

    static void BuildVisual(GameObject? prefab)
    {
        if (prefab == null)
            return;
        prefab.transform.localScale = Vector3.one;
        PaperItem.StripRootAndChildDonorMeshes(prefab);
        var doomed = new List<GameObject>();
        foreach (Transform child in prefab.transform)
            doomed.Add(child.gameObject);
        foreach (var go in doomed)
            Object.DestroyImmediate(go);
        foreach (var col in prefab.GetComponents<Collider>())
            Object.DestroyImmediate(col);

        // A medium book lying on its back: collider is the cover footprint.
        var d = BookMesh.Dimensions(BookSize.Medium);
        var box = prefab.AddComponent<BoxCollider>();
        box.center = new Vector3(0f, d.z / 2f, 0f);
        box.size = new Vector3(d.y, d.z, d.x);

        var attach = new GameObject("attach").transform;
        attach.SetParent(prefab.transform, false);
        // The mesh stands (spine -Z, up +Y); lay it so the front cover faces up.
        attach.localRotation = Quaternion.Euler(0f, 0f, 90f);
        attach.localPosition = new Vector3(d.y / 2f, d.z / 2f, 0f);
        BookAssets.Build(attach, BookSize.Medium, BookData.CoverColors[0]);
    }

    internal static bool IsBook(ItemDrop.ItemData? item)
    {
        if (item?.m_shared == null)
            return false;
        if (item.m_dropPrefab != null && item.m_dropPrefab.name.StartsWith(PrefabName, StringComparison.OrdinalIgnoreCase))
            return true;
        return item.m_shared.m_name.Equals(TokenName, StringComparison.OrdinalIgnoreCase);
    }
}
