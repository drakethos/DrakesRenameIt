using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using BepInEx;
using HarmonyLib;
using Jotunn.Managers;

namespace DrakeRenameit.Books;

/// <summary>
/// The names players see for the book, the book pieces, the shelves and the secret door, plus the made-up titles on the secret
/// door, from a plain text file a server owner can edit: <c>BepInEx/config/DrakesRenameIt/BookNames.yml</c>, one
/// <c>key: name</c> per line. Written with the defaults the first time; keys added by a later version are appended, edits are kept.
/// A name that still matches the English default changes nothing, so the Spanish names stay for Spanish players.
/// </summary>
internal static class BookNames
{
    internal static string FilePath => Path.Combine(Paths.ConfigPath, "DrakesRenameIt", "BookNames.yml");

    internal sealed class Entry
    {
        public string Key = "";
        public string Token = "";
        public string Default = "";
    }

    /// <summary>Every name in the file, in file order. Tokens without the leading $.</summary>
    internal static readonly Entry[] Entries =
    {
        new() { Key = "book", Token = "item_drakes_book", Default = "Book" },
        new() { Key = "book_piece", Token = "piece_drakes_book", Default = "Book" },
        new() { Key = "book_piece_standing", Token = BookPiece.UprightToken.TrimStart('$'), Default = "Book, standing" },
        new() { Key = "wall_shelf", Token = "piece_drakes_wall_shelf", Default = "Wall Shelf" },
        new() { Key = "wall_shelf_long", Token = "piece_drakes_wall_shelf_long", Default = "Long Wall Shelf" },
        new() { Key = "bookcase_small", Token = "piece_drakes_bookcase_small", Default = "Small Bookcase" },
        new() { Key = "bookcase_tall", Token = "piece_drakes_bookcase", Default = "Tall Bookcase" },
        new() { Key = "bookcase_wide", Token = "piece_drakes_bookcase_wide", Default = "Wide Bookcase" },
        new() { Key = "secret_door", Token = "piece_drakes_secret_bookcase", Default = "Secret Bookcase Door" },
    };

    const string KeyHandle = "secret_door_handle";
    const string KeyTitles = "secret_door_titles";

    internal const string DefaultHandle = "Sagas of the North";

    internal static readonly string[] DefaultTitles =
    {
        "Old Ledger", "Ravens Notes", "Smithing Primer", "Sagas of the North", "Herbs and Roots", "Tides", "Map Book", "Trolls",
        "Bronze and Tin", "Songs", "The Black Forest", "Recipes", "Runes", "Letters Home", "Swamp Journal", "Stars", "Kings",
        "Hunting", "Old Roads", "Weather",
    };

    static Dictionary<string, string>? _values;

    /// <summary>Tokens this file has renamed, so a name taken out again goes back to its default on a reload.</summary>
    static readonly HashSet<string> Applied = new();

    /// <summary>The handle's title on a new secret door (until its owner names it).</summary>
    internal static string SecretHandle => Value(KeyHandle, DefaultHandle);

    /// <summary>Titles for the made-up books on the secret door.</summary>
    internal static string[] SecretTitles
    {
        get
        {
            var raw = Value(KeyTitles, string.Join(", ", DefaultTitles));
            var list = new List<string>();
            foreach (var part in raw.Split(','))
                if (part.Trim().Length > 0)
                    list.Add(part.Trim());
            return list.Count > 0 ? list.ToArray() : DefaultTitles;
        }
    }

    /// <summary>The name for <paramref name="token"/> (with or without $), as the file says or the default.</summary>
    internal static string NameFor(string token)
    {
        token = token.TrimStart('$');
        foreach (var e in Entries)
            if (e.Token == token)
                return Value(e.Key, e.Default);
        return token;
    }

    static string Value(string key, string fallback)
    {
        var values = _values ??= Load();
        return values.TryGetValue(key, out var v) && v.Length > 0 ? v : fallback;
    }

    /// <summary>Re-reads the file (tests, and after an edit) and applies the names.</summary>
    internal static void Reload()
    {
        _values = null;
        Apply();
    }

    /// <summary>
    /// Adds every changed name as a translation for English, Spanish and the language in use, so it wins over the built-in
    /// names. Run after the pieces registered their own names.
    /// </summary>
    internal static void Apply()
    {
        try
        {
            var values = _values ??= Load();
            var loc = LocalizationManager.Instance.GetLocalization();
            var languages = new List<string> { "English", "Spanish" };
            var current = Localization.instance != null ? Localization.instance.GetSelectedLanguage() : "";
            if (current.Length > 0 && !languages.Contains(current))
                languages.Add(current);
            var changed = 0;
            foreach (var e in Entries)
            {
                if (!values.TryGetValue(e.Key, out var name) || name.Length == 0 || name == e.Default)
                {
                    // Renamed earlier, default again now: English gets its default back (other languages on the next start).
                    if (Applied.Remove(e.Token))
                    {
                        loc.AddTranslation("English", e.Token, e.Default);
                        AddWord(e.Token, e.Default);
                    }

                    continue;
                }

                foreach (var language in languages)
                    loc.AddTranslation(language, e.Token, name);
                AddWord(e.Token, name);
                Applied.Add(e.Token);
                changed++;
            }

            ClearCache();
            if (changed > 0)
                RenameitConfig.Log?.LogInfo($"[Books] {changed} name(s) from {FilePath}.");
        }
        catch (Exception ex)
        {
            RenameitConfig.Log?.LogWarning($"[Books] Book names file skipped: {ex.Message}");
        }
    }

    static readonly MethodInfo? AddWordMethod = AccessTools.Method(typeof(Localization), "AddWord", new[] { typeof(string), typeof(string) });
    static readonly FieldInfo? WordsField = AccessTools.Field(typeof(Localization), "m_translations");
    static readonly FieldInfo? CacheField = AccessTools.Field(typeof(Localization), "m_cache");

    /// <summary>
    /// Valheim 1.0 keeps recently translated text in a cache (Localization.m_cache), so a hover already shown would keep the old
    /// name. Emptied after the names change. Not in the build stubs: read by reflection, and skipped where it isn't there.
    /// </summary>
    static void ClearCache()
    {
        try
        {
            if (Localization.instance == null || CacheField?.GetValue(Localization.instance) is not { } cache)
                return;
            if (AccessTools.Method(cache.GetType(), "Clear", Type.EmptyTypes) is { } clear)
            {
                clear.Invoke(cache, null);
                return;
            }

            // No Clear: a fresh cache of the same kind (its constructor takes the size).
            foreach (var ctor in cache.GetType().GetConstructors())
            {
                var ps = ctor.GetParameters();
                if (ps.Length == 1 && ps[0].ParameterType == typeof(int))
                {
                    CacheField.SetValue(Localization.instance, ctor.Invoke(new object[] { 1000 }));
                    return;
                }

                if (ps.Length == 0)
                {
                    CacheField.SetValue(Localization.instance, ctor.Invoke(null));
                    return;
                }
            }
        }
        catch
        {
            /* the old name shows until the text drops out of the cache */
        }
    }

    /// <summary>Into the game's live word list too, so a name changed after the language loaded shows straight away.</summary>
    static void AddWord(string token, string name)
    {
        try
        {
            if (Localization.instance == null)
                return;
            if (WordsField?.GetValue(Localization.instance) is Dictionary<string, string> words)
                words[token] = name;
            else
                AddWordMethod?.Invoke(Localization.instance, new object[] { token, name });
        }
        catch
        {
            /* the translation above still applies on the next language load */
        }
    }

    static Dictionary<string, string> Load()
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            if (File.Exists(FilePath))
            {
                foreach (var raw in File.ReadAllLines(FilePath, Encoding.UTF8))
                {
                    var line = raw.Trim();
                    if (line.Length == 0 || line.StartsWith("#"))
                        continue;
                    var colon = line.IndexOf(':');
                    if (colon <= 0)
                        continue;
                    var key = line.Substring(0, colon).Trim();
                    var value = line.Substring(colon + 1).Trim();
                    if (value.Length >= 2 && (value[0] == '"' && value[value.Length - 1] == '"' || value[0] == '\'' && value[value.Length - 1] == '\''))
                        value = value.Substring(1, value.Length - 2);
                    values[key] = value;
                }
            }

            WriteMissing(values);
        }
        catch (Exception ex)
        {
            RenameitConfig.Log?.LogWarning($"[Books] Could not read {FilePath}: {ex.Message}");
        }

        return values;
    }

    /// <summary>A new file gets every key with its default; an older file gets the keys it lacks appended at the end.</summary>
    static void WriteMissing(Dictionary<string, string> values)
    {
        var fresh = !File.Exists(FilePath);
        var text = new StringBuilder();
        if (fresh)
        {
            text.AppendLine("# Names for RenameIt's books, shelves and bookcases. Change the text after the colon; keep the key before it.");
            text.AppendLine("# Delete a line (or the whole file) to get that default back. Read when the game starts.");
            text.AppendLine("# A name left as the English default changes nothing, so other languages keep their own names.");
        }

        var added = 0;
        foreach (var e in Entries)
        {
            if (values.ContainsKey(e.Key))
                continue;
            text.AppendLine($"{e.Key}: {e.Default}");
            values[e.Key] = e.Default;
            added++;
        }

        if (!values.ContainsKey(KeyHandle))
        {
            text.AppendLine("# The title on a new secret door's handle book (its owner can change it on the door).");
            text.AppendLine($"{KeyHandle}: {DefaultHandle}");
            values[KeyHandle] = DefaultHandle;
            added++;
        }

        if (!values.ContainsKey(KeyTitles))
        {
            text.AppendLine("# The made-up books on a secret door, comma-separated.");
            text.AppendLine($"{KeyTitles}: {string.Join(", ", DefaultTitles)}");
            values[KeyTitles] = string.Join(", ", DefaultTitles);
            added++;
        }

        if (added == 0)
            return;
        // an edited file may end without a line break: start the appended keys on a line of their own
        if (!fresh && File.ReadAllText(FilePath) is { Length: > 0 } old && !old.EndsWith("\n"))
            text.Insert(0, Environment.NewLine);
        File.AppendAllText(FilePath, text.ToString(), new UTF8Encoding(false));
    }
}
