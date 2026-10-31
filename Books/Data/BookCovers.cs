using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using UnityEngine;

namespace DrakeRenameit.Books;

/// <summary>
/// Optional cover pictures: PNG or JPG files in <c>BepInEx/config/DrakesRenameIt/BookCovers</c>. A server (or a modpack) puts the
/// same files in that folder on every machine; a book remembers only the file name, so nothing is sent over the network.
/// A name with no file here simply falls back to the plain leather cover.
/// </summary>
internal static class BookCovers
{
    static readonly Dictionary<string, Texture2D?> Cache = new();
    static string[]? _names;

    internal static string Folder => Path.Combine(Paths.ConfigPath, "DrakesRenameIt", "BookCovers");

    /// <summary>The file names (without extension) found in the folder, sorted. Read once per game start; <see cref="Rescan"/> re-reads.</summary>
    internal static IReadOnlyList<string> Names()
    {
        if (_names != null)
            return _names;
        var list = new List<string>();
        try
        {
            Directory.CreateDirectory(Folder);
            foreach (var file in Directory.GetFiles(Folder))
            {
                var ext = Path.GetExtension(file).ToLowerInvariant();
                if (ext == ".png" || ext == ".jpg" || ext == ".jpeg")
                    list.Add(Path.GetFileNameWithoutExtension(file));
            }
        }
        catch (Exception)
        {
            // an unreadable folder just means no cover pictures
        }

        list.Sort(StringComparer.OrdinalIgnoreCase);
        return _names = list.ToArray();
    }

    // UnityEngine.ImageConversion lives in a module this project does not reference; call it by name.
    static readonly System.Reflection.MethodInfo? LoadImageMethod =
        Type.GetType("UnityEngine.ImageConversion, UnityEngine.ImageConversionModule")?.GetMethod("LoadImage", new[] { typeof(Texture2D), typeof(byte[]) });

    static bool Load(Texture2D texture, byte[] bytes) => LoadImageMethod != null && LoadImageMethod.Invoke(null, new object[] { texture, bytes }) is true;

    internal static void Rescan()
    {
        _names = null;
        Cache.Clear();
    }

    internal static Texture2D? Texture(string? name)
    {
        if (string.IsNullOrEmpty(name))
            return null;
        if (Cache.TryGetValue(name!, out var cached))
            return cached;
        Texture2D? texture = null;
        try
        {
            foreach (var ext in new[] { ".png", ".jpg", ".jpeg" })
            {
                var path = Path.Combine(Folder, name + ext);
                if (!File.Exists(path))
                    continue;
                texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!Load(texture, File.ReadAllBytes(path)))
                    texture = null;
                break;
            }
        }
        catch (Exception)
        {
            texture = null;
        }

        Cache[name!] = texture;
        return texture;
    }
}
