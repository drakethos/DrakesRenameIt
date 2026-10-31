using System.Collections.Generic;
using DrakeModsLibs.Forge;
using DrakeRenameit.Paper.Items;
using UnityEngine;

namespace DrakeRenameit.Books;

/// <summary>Materials and a ready-made book visual. Pages reuse the paper sheet material; covers are the grey leather texture tinted per book.</summary>
internal static class BookAssets
{
    static readonly Dictionary<string, Material> Covers = new();
    static Texture2D? _leather;

    internal static Sprite? Icon => ForgeTextures.Sprite(typeof(BookAssets).Assembly, "textures/book_icon.png");
    internal static Sprite? WallShelfIcon => ForgeTextures.Sprite(typeof(BookAssets).Assembly, "textures/wall_shelf_icon.png");
    internal static Sprite? BookcaseIcon => ForgeTextures.Sprite(typeof(BookAssets).Assembly, "textures/bookcase_icon.png");

    internal static Sprite? PieceIcon(string name) => ForgeTextures.Sprite(typeof(BookAssets).Assembly, $"textures/{name}_icon.png");

    static Texture2D? Leather
    {
        get
        {
            if (_leather == null)
            {
                _leather = ForgeTextures.Load(typeof(BookAssets).Assembly, "textures/book_cover.png");
                if (_leather != null)
                    _leather.filterMode = FilterMode.Point;
            }

            return _leather;
        }
    }

    /// <summary>One tinted cover material per colour, built once.</summary>
    internal static Material? Cover(Color color)
    {
        var key = ColorUtility.ToHtmlStringRGB(color);
        if (Covers.TryGetValue(key, out var cached) && cached != null)
            return cached;
        var tex = Leather;
        if (tex == null)
            return null;
        // ForgeSprites gives a vanilla Standard material whose shader variant is in the build; copy it, then tint.
        var material = new Material(ForgeSprites.Material(tex)) { name = "drakes_book_cover_" + key };
        material.color = color;
        Covers[key] = material;
        return material;
    }

    /// <summary>
    /// A standing book under <paramref name="parent"/> (spine facing -Z, foot of the spine at the parent's origin).
    /// Returns null when the textures are missing.
    /// </summary>
    internal static GameObject? Build(Transform parent, BookSize size, Color color)
    {
        var cover = Cover(color);
        var pages = PaperMesh.SheetMaterial(false);
        if (cover == null || pages == null)
            return null;
        var go = new GameObject("drakes_book_mesh");
        go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = BookMesh.Book(size);
        go.AddComponent<MeshRenderer>().sharedMaterials = new[] { cover, pages };
        return go;
    }
}
