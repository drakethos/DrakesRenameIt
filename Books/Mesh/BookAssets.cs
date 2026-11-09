using System.Collections.Generic;
using DrakeModsLibs.Forge;
using DrakeRenameit.Paper.Items;
using TMPro;
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

    static readonly Dictionary<string, Material> CoverPictures = new();

    /// <summary>
    /// A standing book under <paramref name="parent"/> (spine facing -Z, foot of the spine at the parent's origin) in one of the
    /// three stock sizes, with its look and picture from <paramref name="data"/> (the item's custom data or a placed book's).
    /// </summary>
    internal static GameObject? Build(Transform parent, BookSize size, IDictionary<string, string>? data, string? spineTitle = null)
    {
        var d = BookMesh.Dimensions(size);
        return Build(parent, d.x, d.y, d.z, data, spineTitle);
    }

    /// <summary>Same, at any depth / height / thickness (shelves size their books to the row).</summary>
    internal static GameObject? Build(Transform parent, float depth, float height, float thickness, IDictionary<string, string>? data, string? spineTitle)
    {
        var color = BookLook.ColorOf(data, BookData.KeyColor, BookData.CoverColors[0]);
        var pattern = BookLook.PatternOf(data);
        var accent = BookLook.ColorOf(data, BookLook.KeyAccent, BookLook.Accents[0]);
        var cover = BookLook.CoverMaterial(color, pattern, accent) ?? Cover(color);
        var spine = BookLook.SpineMaterial(color, pattern, accent) ?? cover;
        var pages = PaperMesh.SheetMaterial(false);
        if (cover == null || pages == null)
            return null;
        var go = new GameObject("drakes_book_mesh");
        go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = BookMesh.Custom(depth, height, thickness);
        go.AddComponent<MeshRenderer>().sharedMaterials = new[] { cover, pages, spine };

        var picture = PictureMaterial(data != null && data.TryGetValue(BookData.KeyCover, out var pic) ? pic : null);
        if (picture != null)
        {
            var quad = new GameObject("cover_picture");
            quad.transform.SetParent(go.transform, false);
            quad.AddComponent<MeshFilter>().sharedMesh = BookMesh.CoverQuadFor(depth, height, thickness);
            quad.AddComponent<MeshRenderer>().sharedMaterial = picture;
        }

        if (!string.IsNullOrEmpty(spineTitle))
            SpineText(go.transform, depth, height, thickness, spineTitle!, BookLook.ColorOf(data, BookLook.KeyInk, BookLook.Inks[0]));
        return go;
    }

    static TMP_FontAsset? _font;

    /// <summary>The game's own serif (the one the hover names use), found once.</summary>
    static TMP_FontAsset? Font()
    {
        if (_font != null)
            return _font;
        TMP_FontAsset? fallback = null;
        foreach (var f in Resources.FindObjectsOfTypeAll<TMP_FontAsset>())
        {
            if (f == null)
                continue;
            fallback ??= f;
            if (f.name.IndexOf("Averia", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                _font = f;
                break;
            }
        }

        return _font ??= fallback;
    }

    /// <summary>The title on the spine, reading bottom to top, sized to fit (world-space TextMeshPro, shares the game's font atlas).</summary>
    static void SpineText(Transform book, float depth, float height, float thickness, string title, Color ink)
    {
        var font = Font();
        if (font == null)
            return;
        var go = new GameObject("spine_title");
        go.transform.SetParent(book, false);
        go.transform.localPosition = new Vector3(0f, height / 2f, -depth / 2f - 0.0015f);
        go.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        var text = go.AddComponent<TextMeshPro>();
        text.font = font;
        text.text = title;
        text.color = ink;
        text.alignment = TextAlignmentOptions.Center;
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.enableAutoSizing = true;
        text.fontSizeMin = 0.05f;
        text.fontSizeMax = Mathf.Clamp(thickness * 6f, 0.15f, 0.5f);
        text.fontStyle = FontStyles.Bold;
        text.rectTransform.sizeDelta = new Vector2(height * 0.66f, thickness * 0.72f);
    }

    static Material? PictureMaterial(string? name)
    {
        if (string.IsNullOrEmpty(name))
            return null;
        if (CoverPictures.TryGetValue(name!, out var cached) && cached != null)
            return cached;
        var texture = BookCovers.Texture(name);
        if (texture == null)
            return null;
        var material = new Material(ForgeSprites.Material(texture)) { name = "drakes_book_picture_" + name };
        CoverPictures[name!] = material;
        return material;
    }
}
