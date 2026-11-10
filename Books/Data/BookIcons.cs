using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace DrakeRenameit.Books;

/// <summary>
/// Inventory icons drawn from the book's own cover: the cover texture (colour and pattern) on a standing book with a darker spine and
/// the page edges showing, so a book in a chest looks like the book on the shelf. No text (it would be unreadable at icon size).
/// One sprite per look, shared.
/// </summary>
internal static class BookIcons
{
    const int Size = 64;
    static readonly Dictionary<string, Sprite> Cache = new();

    internal static Sprite? For(ItemDrop.ItemData item)
    {
        var data = item.m_customData;
        var cover = BookLook.ColorOf(data, BookData.KeyColor, BookLook.Covers[0]);
        var pattern = BookLook.PatternOf(data);
        var accent = BookLook.ColorOf(data, BookLook.KeyAccent, BookLook.Accents[0]);
        var key = ColorUtility.ToHtmlStringRGB(cover) + (int)pattern + ColorUtility.ToHtmlStringRGB(accent);
        if (Cache.TryGetValue(key, out var cached) && cached != null)
            return cached;
        var sprite = Draw(BookLook.Cover(cover, pattern, accent), cover);
        if (sprite != null)
        {
            sprite.name = "drakes_book_icon_" + key;
            Cache[key] = sprite;
        }
        return sprite;
    }

    static Sprite? Draw(Texture2D coverTex, Color cover)
    {
        Color[] src;
        try
        {
            src = coverTex.GetPixels();
        }
        catch
        {
            return null;
        }

        var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        var px = new Color[Size * Size];
        // Book outline in icon pixels (y up): cover 14..50 across, 5..59 up; the page block peeks out on the right and at the top.
        const int x0 = 13, x1 = 49, y0 = 5, y1 = 58, spine = 6, pages = 4;
        var rim = Color.Lerp(cover, Color.black, 0.65f);
        var paper = new Color(0.90f, 0.84f, 0.70f);
        for (var y = 0; y < Size; y++)
        for (var x = 0; x < Size; x++)
        {
            var c = Color.clear;
            if (x >= x1 && x < x1 + pages && y >= y0 + 2 && y <= y1 - 2)
            {
                // Page edges: light lines.
                c = (y & 1) == 0 ? paper : paper * 0.86f;
                c.a = 1f;
            }
            else if (x >= x0 && x < x1 && y >= y0 && y <= y1)
            {
                var u = (x - x0) / (float)(x1 - x0);
                var v = (y - y0) / (float)(y1 - y0);
                var sx = Mathf.Clamp((int)(u * coverTex.width), 0, coverTex.width - 1);
                var sy = Mathf.Clamp((int)(v * coverTex.height), 0, coverTex.height - 1);
                c = src[sy * coverTex.width + sx];
                if (x < x0 + spine)
                    c = Color.Lerp(c, Color.black, 0.28f); // the spine side is in shadow
                var edge = x == x0 || x == x1 - 1 || y == y0 || y == y1;
                if (edge)
                    c = rim;
                c.a = 1f;
            }

            px[y * Size + x] = c;
        }

        tex.SetPixels(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), 100f);
    }
}

/// <summary>A book's icon (inventory, chests, hotbar, tooltips) is drawn from its cover.</summary>
[HarmonyPatch(typeof(ItemDrop.ItemData), nameof(ItemDrop.ItemData.GetIcon))]
internal static class BookIconPatch
{
    static void Postfix(ItemDrop.ItemData __instance, ref Sprite __result)
    {
        if (!RenameitConfig.BooksEnabled || __instance?.m_shared == null || !BookItem.IsBook(__instance))
            return;
        if (BookIcons.For(__instance) is { } sprite)
            __result = sprite;
    }
}
