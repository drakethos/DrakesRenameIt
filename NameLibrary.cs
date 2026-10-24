using System;
using System.Collections.Generic;
using System.Linq;
using DrakeModsLibs.UI.Toolkit;

namespace DrakeRenameit;

/// <summary>
/// The names this player has saved before, offered as suggestions under every Name field (items, pieces, animals, portals, map
/// pins). Per client, kept in <c>RenameitConfig.QuickNames</c> as <c>kind:name</c> entries (newest first). Names keep their rich
/// text tags, so a coloured name comes back coloured.
/// </summary>
internal static class NameLibrary
{
    public const string Item = "items";
    public const string Piece = "pieces";
    public const string Animal = "animals";
    public const string Portal = "portals";
    public const string Pin = "pins";

    const string Separator = "||";
    const int Max = 30;

    /// <summary>Remember a name just saved. Empty names and exact repeats of the same kind are not added twice (a repeat moves to the top).</summary>
    public static void Add(string kind, string? raw)
    {
        if (!RenameitConfig.QuickNamesEnabled || string.IsNullOrWhiteSpace(raw))
            return;
        var name = Clean(raw!);
        if (name.Length == 0)
            return;
        var list = Read();
        list.RemoveAll(e => e.Kind == kind && e.Name == name);
        list.Insert(0, (kind, name));
        if (list.Count > Max)
            list.RemoveRange(Max, list.Count - Max);
        Write(list);
    }

    public static void Remove(string kind, string name)
    {
        var list = Read();
        if (list.RemoveAll(e => e.Kind == kind && e.Name == name) > 0)
            Write(list);
    }

    public static void Clear() => RenameitConfig.QuickNames = "";

    /// <summary>Suggestions for what has been typed so far: names containing it (any kind), this kind's first, newest first. Nothing typed gives the latest few.</summary>
    public static IEnumerable<(string Text, string Note)> Suggest(string kind, string typed)
    {
        if (!RenameitConfig.QuickNamesEnabled)
            return Enumerable.Empty<(string, string)>();
        var needle = (typed ?? "").Trim();
        var list = Read();
        var plain = new Func<string, string>(raw => RichModel.FromRaw(raw).Plain);
        return list
            .Where(e => needle.Length == 0 || plain(e.Name).IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0)
            .Where(e => !string.Equals(plain(e.Name), needle, StringComparison.OrdinalIgnoreCase))
            .Select((e, order) => (e, order))
            .OrderBy(x => x.e.Kind == kind ? 0 : 1)
            .ThenBy(x => x.order)
            .Select(x => (x.e.Name, x.e.Kind == kind ? "" : x.e.Kind))
            .ToList();
    }

    static string Clean(string raw) => raw.Replace(Separator, "").Replace(":", "：").Trim();

    static List<(string Kind, string Name)> Read()
    {
        var result = new List<(string, string)>();
        var text = RenameitConfig.QuickNames;
        if (string.IsNullOrEmpty(text))
            return result;
        foreach (var part in text.Split(new[] { Separator }, StringSplitOptions.RemoveEmptyEntries))
        {
            var colon = part.IndexOf(':');
            if (colon <= 0 || colon >= part.Length - 1)
                continue;
            result.Add((part.Substring(0, colon), part.Substring(colon + 1)));
        }

        return result;
    }

    static void Write(List<(string Kind, string Name)> list) =>
        RenameitConfig.QuickNames = string.Join(Separator, list.Select(e => e.Kind + ":" + e.Name));
}
