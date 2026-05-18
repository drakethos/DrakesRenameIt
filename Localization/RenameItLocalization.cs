using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BepInEx.Logging;
using UnityEngine;

namespace DrakeRenameit.ModText;

/// <summary>
/// Loads <c>Assets/Localization/{Language}.json</c> next to the plugin DLL.
/// <c>English.json</c> is always loaded first; then the file for Valheim's selected language (e.g. <c>Spanish.json</c>) overrides matching keys.
/// </summary>
public static class RenameItLocalization
{
    public const string KeyPrefix = "drakesrenameit_";

    public static string TokenFor(string key) => "$" + KeyPrefix + key;

    private static readonly Dictionary<string, string> Strings =
        new Dictionary<string, string>(StringComparer.Ordinal);

    private static ManualLogSource? _log;
    private static string _pluginDir = "";
    private static string _loadedLanguage = "";
    private static bool _ready;

    [Serializable]
    private class LocalizationFile
    {
        public LocalizationEntry[] entries = Array.Empty<LocalizationEntry>();
    }

    [Serializable]
    private class LocalizationEntry
    {
        public string key = "";
        public string value = "";
    }

    public static void Init(BepInEx.BaseUnityPlugin plugin, ManualLogSource log)
    {
        _log = log;
        _pluginDir = Path.GetDirectoryName(plugin.Info.Location) ?? "";
        ReloadForCurrentLanguage();
    }

    public static string T(string key, params object[] args)
    {
        EnsureCurrentLanguage();
        string text = Resolve(key);
        if (args == null || args.Length == 0)
            return text;

        try
        {
            return string.Format(text, args);
        }
        catch (FormatException)
        {
            _log?.LogWarning($"[ModText] Format error for key '{key}' with {args.Length} argument(s).");
            return text;
        }
    }

    public static string GetMenuTooltipHint(string? binding)
    {
        var keys = MenuKeyBinding.FormatForDisplay(binding);
        return string.IsNullOrEmpty(keys)
            ? T(LKeys.TooltipMenuHintRightClick)
            : T(LKeys.TooltipMenuHint, keys);
    }

    private static void EnsureCurrentLanguage()
    {
        string lang = ResolveGameLanguage();
        if (!_ready || !string.Equals(lang, _loadedLanguage, StringComparison.Ordinal))
            ReloadForLanguage(lang);
        else if (Strings.Count == 0)
            ReloadForLanguage(lang);
    }

    private static void ReloadForCurrentLanguage() => ReloadForLanguage(ResolveGameLanguage());

    private static void ReloadForLanguage(string language)
    {
        Strings.Clear();
        _ready = false;
        _loadedLanguage = language;

        try
        {
            LocalizationDefaults.Populate(Strings);

            string locDir = Path.Combine(_pluginDir, "Assets", "Localization");
            int fromEnglish = TryLoadLanguageFile(locDir, "English");
            int fromLang = 0;
            if (!string.Equals(language, "English", StringComparison.OrdinalIgnoreCase))
                fromLang = TryLoadLanguageFile(locDir, language);

            _ready = Strings.Count > 0;
            _log?.LogInfo(
                $"[ModText] {Strings.Count} strings ready (language={language}, English overrides={fromEnglish}, {language} overrides={fromLang}).");
        }
        catch (Exception ex)
        {
            _log?.LogError($"[ModText] Reload failed for '{language}': {ex}");
            Strings.Clear();
            LocalizationDefaults.Populate(Strings);
            _ready = Strings.Count > 0;
        }
    }

    private static int TryLoadLanguageFile(string locDir, string language)
    {
        string fileName = SanitizeLanguageFileName(language) + ".json";
        string jsonPath = Path.Combine(locDir, fileName);
        if (!File.Exists(jsonPath))
        {
            if (!string.Equals(language, "English", StringComparison.OrdinalIgnoreCase))
                _log?.LogDebug($"[ModText] No file for language '{language}' at {jsonPath} (using English/fallback).");
            return 0;
        }

        int before = Strings.Count;
        if (TryLoadJsonFile(jsonPath, Strings))
            return Strings.Count - before;

        _log?.LogWarning($"[ModText] Could not parse {jsonPath}.");
        return 0;
    }

    /// <summary>Valheim language id (e.g. English, Spanish, Portuguese_Brazil).</summary>
    public static string ResolveGameLanguage()
    {
        try
        {
            var loc = Localization.instance;
            if (loc != null)
            {
                var mi = typeof(Localization).GetMethod(
                    "GetSelectedLanguage",
                    BindingFlags.Instance | BindingFlags.Public);
                if (mi != null)
                {
                    if (mi.Invoke(loc, null) is string result && !string.IsNullOrWhiteSpace(result))
                        return SanitizeLanguageFileName(result);
                }
            }
        }
        catch (Exception ex)
        {
            _log?.LogDebug($"[ModText] GetSelectedLanguage failed: {ex.Message}");
        }

        foreach (var prefKey in new[] { "language", "Language" })
        {
            var v = PlayerPrefs.GetString(prefKey, "");
            if (!string.IsNullOrWhiteSpace(v))
                return SanitizeLanguageFileName(v);
        }

        return "English";
    }

    private static string SanitizeLanguageFileName(string language)
    {
        language = language.Trim();
        foreach (char c in Path.GetInvalidFileNameChars())
            language = language.Replace(c, '_');
        return string.IsNullOrEmpty(language) ? "English" : language;
    }

    private static string Resolve(string key)
    {
        if (Strings.TryGetValue(key, out var text) && !string.IsNullOrEmpty(text))
            return text;

        _log?.LogWarning($"[ModText] Missing localization key: {key}");
        return key;
    }

    private static bool TryLoadJsonFile(string path, Dictionary<string, string> target)
    {
        if (!File.Exists(path))
            return false;

        try
        {
            string json = File.ReadAllText(path);
            int loaded = 0;

            var file = JsonUtility.FromJson<LocalizationFile>(json);
            if (file.entries != null && file.entries.Length > 0)
            {
                foreach (var entry in file.entries)
                {
                    if (string.IsNullOrWhiteSpace(entry.key))
                        continue;
                    target[entry.key.Trim()] = entry.value ?? "";
                    loaded++;
                }
            }

            if (loaded == 0)
                loaded = TryLoadJsonFileRegex(json, target);

            return loaded > 0;
        }
        catch (Exception ex)
        {
            _log?.LogError($"[ModText] Failed to parse {path}: {ex.Message}");
            return false;
        }
    }

    private static int TryLoadJsonFileRegex(string json, Dictionary<string, string> target)
    {
        int count = 0;
        var matches = System.Text.RegularExpressions.Regex.Matches(
            json,
            "\"key\"\\s*:\\s*\"([^\"]+)\"\\s*,\\s*\"value\"\\s*:\\s*\"((?:\\\\.|[^\"\\\\])*)\"",
            System.Text.RegularExpressions.RegexOptions.Singleline);

        foreach (System.Text.RegularExpressions.Match m in matches)
        {
            if (!m.Success || m.Groups.Count < 3)
                continue;
            string k = m.Groups[1].Value.Trim();
            string v = UnescapeJsonString(m.Groups[2].Value);
            if (k.Length == 0)
                continue;
            target[k] = v;
            count++;
        }

        return count;
    }

    private static string UnescapeJsonString(string s) =>
        s.Replace("\\\"", "\"")
            .Replace("\\n", "\n")
            .Replace("\\r", "\r")
            .Replace("\\t", "\t")
            .Replace("\\\\", "\\");
}
