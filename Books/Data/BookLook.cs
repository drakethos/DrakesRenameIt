using System.Collections.Generic;
using System.Globalization;
using DrakeModsLibs.Forge;
using UnityEngine;

namespace DrakeRenameit.Books;

/// <summary>Cover patterns, drawn in code (like the cards' and teams' banners): simple shapes in the accent colour over the leather.</summary>
internal enum BookPattern
{
    Plain = 0,
    Bands = 1,
    Border = 2,
    Corners = 3,
    Stripes = 4,
    Diamond = 5,
    Split = 6,
    Studs = 7,
}

/// <summary>
/// How a book looks: cover colour, a pattern in an accent colour, and the colour of the title on its spine. Stored in the item's custom
/// data next to the other <c>DrakeBook_*</c> keys. Textures are generated once per look and shared by every book that has it.
/// </summary>
internal static class BookLook
{
    internal const string KeyPattern = "DrakeBook_Pattern";
    internal const string KeyAccent = "DrakeBook_Accent";
    internal const string KeyInk = "DrakeBook_Ink";

    /// <summary>Cover colours: leathers and cloths a book could be bound in. No white: a white book reads as a sheet of paper.</summary>
    internal static readonly Color[] Covers =
    {
        Hex("7a2a24"), Hex("5a1c1c"), Hex("9c4a22"), Hex("b07a2a"), Hex("6e6a2a"), Hex("2f5a2c"), Hex("1f4a3e"), Hex("2a5a66"),
        Hex("24456e"), Hex("1c2a4a"), Hex("3c2c6a"), Hex("5a2c5a"), Hex("6a4228"), Hex("3a2616"), Hex("2c2c2e"), Hex("8a7a5a"),
    };

    /// <summary>Accent colours for the pattern: gilt, silver, bone and a few inks.</summary>
    internal static readonly Color[] Accents =
    {
        Hex("d8b04a"), Hex("c0c4c8"), Hex("e8dcc0"), Hex("1a1410"), Hex("a83a2a"), Hex("2a4e86"), Hex("3a6a34"), Hex("b06a2a"),
    };

    /// <summary>Spine title colours.</summary>
    internal static readonly Color[] Inks = { Hex("f0d890"), Hex("efe6d2"), Hex("c0c4c8"), Hex("1a1410"), Hex("d8584a"), Hex("9fc0e8") };

    internal static Color Hex(string hex)
    {
        ColorUtility.TryParseHtmlString("#" + hex, out var c);
        return c;
    }

    internal static BookPattern GetPattern(ItemDrop.ItemData item) => PatternOf(item.m_customData);

    internal static BookPattern PatternOf(IDictionary<string, string>? data) =>
        data != null && data.TryGetValue(KeyPattern, out var raw) && int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n)
        && n >= 0 && n <= (int)BookPattern.Studs
            ? (BookPattern)n
            : BookPattern.Plain;

    internal static Color GetAccent(ItemDrop.ItemData item) => ColorOf(item.m_customData, KeyAccent, Accents[0]);

    internal static Color GetInk(ItemDrop.ItemData item) => ColorOf(item.m_customData, KeyInk, Inks[0]);

    internal static Color ColorOf(IDictionary<string, string>? data, string key, Color fallback) =>
        data != null && data.TryGetValue(key, out var raw) && raw.Length > 0 && ColorUtility.TryParseHtmlString("#" + raw, out var c) ? c : fallback;

    internal static void SetPattern(ItemDrop.ItemData item, BookPattern pattern) =>
        BookData.Set(item, KeyPattern, ((int)pattern).ToString(CultureInfo.InvariantCulture));

    internal static void SetAccent(ItemDrop.ItemData item, Color c) => BookData.Set(item, KeyAccent, ColorUtility.ToHtmlStringRGB(c));

    internal static void SetInk(ItemDrop.ItemData item, Color c) => BookData.Set(item, KeyInk, ColorUtility.ToHtmlStringRGB(c));

    // ------------------------------------------------------------------ textures

    const int CoverW = 128, CoverH = 176;
    const int SpineW = 32, SpineH = 256;
    static readonly Dictionary<string, Texture2D> CoverCache = new();
    static readonly Dictionary<string, Texture2D> SpineCache = new();
    static Texture2D? _grain;
    static Color[]? _grainPixels;

    static string Key(Color cover, BookPattern pattern, Color accent) =>
        ColorUtility.ToHtmlStringRGB(cover) + (int)pattern + ColorUtility.ToHtmlStringRGB(accent);

    /// <summary>The front (and back) cover: leather grain in the cover colour with the pattern on top. U runs from the spine edge.</summary>
    internal static Texture2D Cover(Color cover, BookPattern pattern, Color accent)
    {
        var key = Key(cover, pattern, accent);
        if (CoverCache.TryGetValue(key, out var cached) && cached != null)
            return cached;
        _accent = accent;
        var tex = Paint(CoverW, CoverH, cover, (x, y) => CoverMask(pattern, x, y));
        tex.name = "drakes_book_cover_" + key;
        tex.Apply(true);
        return CoverCache[key] = tex;
    }

    /// <summary>The spine: same leather, with the pattern's spine version (bands, caps) at the head and foot, leaving the middle for the title.</summary>
    internal static Texture2D Spine(Color cover, BookPattern pattern, Color accent)
    {
        var key = Key(cover, pattern, accent);
        if (SpineCache.TryGetValue(key, out var cached) && cached != null)
            return cached;
        _accent = accent;
        var tex = Paint(SpineW, SpineH, Color.Lerp(cover, Color.black, 0.08f), (x, y) => SpineMask(pattern, x, y));
        tex.name = "drakes_book_spine_" + key;
        tex.Apply(true);
        return SpineCache[key] = tex;
    }

    /// <summary>The accent the masks paint with (set by <see cref="Cover"/> / <see cref="Spine"/> just before painting).</summary>
    static Color _accent;

    static Texture2D Paint(int w, int h, Color baseColor, System.Func<float, float, float> mask)
    {
        var grain = Grain();
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        var pixels = new Color[w * h];
        for (var y = 0; y < h; y++)
        for (var x = 0; x < w; x++)
        {
            var g = grain != null ? grain[(y % 64) * 64 + x % 64].grayscale : 0.75f;
            var shade = 0.82f + (g - 0.7f) * 0.5f;
            var u = (x + 0.5f) / w;
            var v = (y + 0.5f) / h;
            var leather = baseColor * shade;
            var a = Mathf.Clamp01(mask(u, v));
            var accent = _accent * (0.9f + (g - 0.7f) * 0.25f);
            var c = Color.Lerp(leather, accent, a);
            c.a = 1f;
            pixels[y * w + x] = c;
        }

        tex.SetPixels(pixels);
        return tex;
    }

    static Color[]? Grain()
    {
        if (_grainPixels != null)
            return _grainPixels;
        _grain = ForgeTextures.Load(typeof(BookLook).Assembly, "textures/book_cover.png");
        if (_grain == null || _grain.width < 64 || _grain.height < 64)
            return null;
        try
        {
            _grainPixels = _grain.GetPixels(0, 0, 64, 64);
        }
        catch
        {
            _grainPixels = null;
        }

        return _grainPixels;
    }

    /// <summary>Soft edge for a shape: 1 inside, 0 outside, a pixel or so of blend.</summary>
    static float Edge(float distanceInside, float soft = 0.006f) => Mathf.Clamp01(distanceInside / soft + 0.5f);

    static float Band(float v, float from, float to) => Mathf.Min(Edge(v - from), Edge(to - v));

    static float CoverMask(BookPattern pattern, float u, float v)
    {
        switch (pattern)
        {
            case BookPattern.Bands:
                return Mathf.Max(Band(v, 0.10f, 0.15f), Band(v, 0.85f, 0.90f));
            case BookPattern.Border:
            {
                // A ruled frame: outer line, gap, thin inner line.
                var d = Mathf.Min(Mathf.Min(u - 0.08f, 0.92f - u), Mathf.Min(v - 0.06f, 0.94f - v));
                var inner = Mathf.Min(Mathf.Min(u - 0.13f, 0.87f - u), Mathf.Min(v - 0.10f, 0.90f - v));
                return Mathf.Max(Mathf.Min(Edge(d), Edge(0.025f - d)), Mathf.Min(Edge(inner), Edge(0.008f - inner)));
            }
            case BookPattern.Corners:
            {
                // Triangular metal corner guards on the fore-edge corners.
                var cu = 1f - u;
                var top = Edge(0.22f - (cu + (1f - v) * 0.72f));
                var bottom = Edge(0.22f - (cu + v * 0.72f));
                return Mathf.Max(top, bottom);
            }
            case BookPattern.Stripes:
                return Edge(0.035f - Mathf.Abs(Mathf.Repeat(u + v * 0.7f, 0.18f) - 0.09f));
            case BookPattern.Diamond:
            {
                var d = Mathf.Abs(u - 0.5f) / 0.30f + Mathf.Abs(v - 0.5f) / 0.22f;
                var ring = Mathf.Min(Edge(1f - d, 0.02f), Edge(d - 0.78f, 0.02f));
                var dot = Edge(0.3f - d, 0.02f);
                return Mathf.Max(ring, dot);
            }
            case BookPattern.Split:
                // Two-tone: the lower third in the accent, with a fine rule above it.
                return Mathf.Max(Edge(0.33f - v), Band(v, 0.36f, 0.375f));
            case BookPattern.Studs:
            {
                var best = 0f;
                foreach (var (cu, cv) in new[] { (0.22f, 0.12f), (0.78f, 0.12f), (0.22f, 0.88f), (0.78f, 0.88f), (0.5f, 0.5f) })
                {
                    var dx = (u - cu) * CoverW;
                    var dy = (v - cv) * CoverH;
                    best = Mathf.Max(best, Edge(7f - Mathf.Sqrt(dx * dx + dy * dy), 1.2f));
                }

                return best;
            }
            default:
                return 0f;
        }
    }

    static float SpineMask(BookPattern pattern, float u, float v)
    {
        switch (pattern)
        {
            case BookPattern.Plain:
                return 0f;
            case BookPattern.Split:
                return Mathf.Max(Edge(0.12f - v), Band(v, 0.14f, 0.15f));
            case BookPattern.Stripes:
                return Mathf.Max(Band(v, 0.04f, 0.07f), Band(v, 0.09f, 0.11f)) + Mathf.Max(Band(v, 0.89f, 0.91f), Band(v, 0.93f, 0.96f));
            case BookPattern.Corners:
            case BookPattern.Studs:
                // Metal caps at head and foot.
                return Mathf.Max(Edge(0.05f - v), Edge(v - 0.95f));
            default:
                // Raised bands near the head and foot, as on a bound spine.
                return Mathf.Max(Mathf.Max(Band(v, 0.06f, 0.09f), Band(v, 0.12f, 0.14f)), Mathf.Max(Band(v, 0.86f, 0.88f), Band(v, 0.91f, 0.94f)));
        }
    }

    // ------------------------------------------------------------------ materials

    static readonly Dictionary<string, Material> CoverMats = new();
    static readonly Dictionary<string, Material> SpineMats = new();

    internal static Material? CoverMaterial(Color cover, BookPattern pattern, Color accent) =>
        Mat(CoverMats, "cover", Key(cover, pattern, accent), () => Cover(cover, pattern, accent));

    internal static Material? SpineMaterial(Color cover, BookPattern pattern, Color accent) =>
        Mat(SpineMats, "spine", Key(cover, pattern, accent), () => Spine(cover, pattern, accent));

    static Material? Mat(Dictionary<string, Material> cache, string kind, string key, System.Func<Texture2D> texture)
    {
        if (cache.TryGetValue(key, out var cached) && cached != null)
            return cached;
        var tex = texture();
        // ForgeSprites gives a vanilla Standard material whose shader variant is in the build; copy it, then set the texture.
        var material = new Material(ForgeSprites.Material(tex)) { name = $"drakes_book_{kind}_{key}", color = Color.white };
        return cache[key] = material;
    }
}
