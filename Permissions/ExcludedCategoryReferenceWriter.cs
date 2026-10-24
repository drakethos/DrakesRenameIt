using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using BepInEx;
using DrakeRenameit.Pieces;
using HarmonyLib;
using UnityEngine;

namespace DrakeRenameit.Permissions;

/// <summary>
/// Writes <c>ExcludedCategoryReference.txt</c> under <see cref="Paths.ConfigPath"/> on first run (or when outdated):
/// item category tokens for ExcludedCategory, piece categories for PieceExcludedCategory, and (once a world has loaded and
/// every piece, modded ones included, exists) the list of renamable pieces with the names PieceExcludedNames accepts.
/// </summary>
internal static class ExcludedCategoryReferenceWriter
{
    private const string FileName = "ExcludedCategoryReference.txt";
    private const string PiecesListedLine = "# Pieces listed from a loaded world.";

    static bool _piecesWrittenThisSession;

    static string FilePath => Path.Combine(Paths.ConfigPath, DrakeRenameit.GUID, FileName);
    static string StampLine => $"# DrakeRenameit {DrakeRenameit.Version}";

    /// <summary>At startup: items and piece categories (pieces do not exist yet).</summary>
    internal static void EnsureGenerated()
    {
        try
        {
            var head = ReadHead();
            if (head != null && head.IndexOf(StampLine, StringComparison.Ordinal) >= 0)
                return;
            Write(pieces: null);
        }
        catch
        {
            // Non-fatal; config still works without the file
        }
    }

    /// <summary>
    /// After the local player spawns: adds the piece list once per version (and refreshes it once per session, so newly
    /// installed piece mods show up). Never breaks spawning.
    /// </summary>
    internal static void EnsurePiecesListed()
    {
        if (_piecesWrittenThisSession || ZNetScene.instance == null)
            return;
        _piecesWrittenThisSession = true;
        try
        {
            var pieces = CollectPieces();
            Write(pieces);
            RenameitConfig.Log?.LogInfo($"[PieceRename] {FileName}: listed {pieces.Count} buildable pieces.");
        }
        catch (Exception ex)
        {
            RenameitConfig.Log?.LogWarning($"[PieceRename] Could not list pieces in {FileName}: {ex.Message}");
        }
    }

    static string? ReadHead()
    {
        try
        {
            if (!File.Exists(FilePath))
                return null;
            var head = File.ReadAllText(FilePath);
            return head.Length > 2048 ? head.Substring(0, 2048) : head;
        }
        catch
        {
            return null; // regenerate if unreadable
        }
    }

    sealed class PieceRow
    {
        public string Prefab = "";
        public string Token = "";
        public string Name = "";
        public List<string> Categories = new();
    }

    static List<PieceRow> CollectPieces()
    {
        var rows = new List<PieceRow>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var buildable = BuildMenuPieces();
        foreach (var prefab in ZNetScene.instance.m_prefabs)
        {
            if (prefab == null || prefab.GetComponent<Piece>() is not Piece piece || !seen.Add(prefab.name))
                continue;
            // Buildable only: dungeon loot chests and other world pieces are in no tool's build menu.
            if (!buildable.Contains(prefab.name) || PieceRename.FirstNameableHover(piece) == null)
                continue;
            rows.Add(new PieceRow
            {
                Prefab = prefab.name,
                Token = piece.m_name ?? "",
                Name = Localize(piece.m_name),
                Categories = PieceRename.CategoriesOf(piece).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            });
        }
        return rows;
    }

    /// <summary>Every piece in a build menu: hammer, hoe, cultivator and any tool a mod adds.</summary>
    static HashSet<string> BuildMenuPieces()
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        if (ObjectDB.instance == null)
            return names;
        foreach (var item in ObjectDB.instance.m_items)
        {
            var table = item != null ? item.GetComponent<ItemDrop>()?.m_itemData?.m_shared?.m_buildPieces : null;
            if (table == null || table.m_pieces == null)
                continue;
            foreach (var go in table.m_pieces)
            {
                if (go != null)
                    names.Add(go.name);
            }
        }
        return names;
    }

    static void Write(List<PieceRow>? pieces)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        var sb = new StringBuilder();
        sb.AppendLine(StampLine);
        if (pieces != null)
            sb.AppendLine(PiecesListedLine);
        sb.AppendLine("# Auto-generated. Valid tokens for ExcludedCategory (items) and PieceExcludedCategory / PieceExcludedNames (pieces).");
        sb.AppendLine("# Matching ignores upper/lower case. Separate several with commas.");
        sb.AppendLine();

        sb.AppendLine("# ================================================================ ITEMS: ExcludedCategory");
        sb.AppendLine();
        sb.AppendLine("## Skills.SkillType");
        foreach (var name in Enum.GetNames(typeof(Skills.SkillType)).OrderBy(n => n, StringComparer.Ordinal))
            sb.AppendLine(name);
        sb.AppendLine();

        sb.AppendLine("## ItemDrop.ItemData.ItemType");
        foreach (var name in Enum.GetNames(typeof(ItemDrop.ItemData.ItemType)).OrderBy(n => n, StringComparer.Ordinal))
            sb.AppendLine(name);
        sb.AppendLine();

        sb.AppendLine("## Aliases (see ExcludedCategoryAliases)");
        foreach (var key in ExcludedCategoryAliases.AllKeys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase))
            sb.AppendLine(key);
        sb.AppendLine();

        sb.AppendLine("# ================================================================ PIECES: PieceExcludedCategory");
        sb.AppendLine();
        sb.AppendLine("## What the piece does");
        foreach (var (name, what) in FunctionCategories)
            sb.AppendLine($"{name,-10} {what}");
        sb.AppendLine();

        sb.AppendLine("## Build menu category (Piece.PieceCategory)");
        foreach (var name in Enum.GetNames(typeof(Piece.PieceCategory)).Where(n => n != "Max" && n != "All"))
            sb.AppendLine(name);
        if (pieces != null)
        {
            // Categories other mods add (through Jotunn), as the pieces below use them.
            var vanilla = new HashSet<string>(Enum.GetNames(typeof(Piece.PieceCategory)), StringComparer.OrdinalIgnoreCase);
            var modded = pieces.Select(p => p.Categories.FirstOrDefault() ?? "").Where(c => c.Length > 0 && !vanilla.Contains(c))
                .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(c => c, StringComparer.OrdinalIgnoreCase).ToList();
            if (modded.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("## Build menu categories added by other mods");
                foreach (var name in modded)
                    sb.AppendLine(name);
            }
        }
        sb.AppendLine();

        sb.AppendLine("# ================================================================ PIECES: PieceExcludedNames / PieceRenameAllowlist");
        sb.AppendLine("# Any of the first three columns works: prefab name, $token, or the name players see.");
        sb.AppendLine("# Only pieces that show a name on hover are listed (fires, item stands and signs keep vanilla Shift+Use).");
        sb.AppendLine("# Only pieces players can build are listed. Pieces the world generated (dungeon chests, crypt doors) only admins can rename.");
        sb.AppendLine();
        if (pieces == null)
        {
            sb.AppendLine("(Load into a world once and this list fills in, including pieces from other mods.)");
        }
        else
        {
            var w1 = Math.Max(6, pieces.Select(p => p.Prefab.Length).DefaultIfEmpty(0).Max());
            var w2 = Math.Max(6, pieces.Select(p => p.Token.Length).DefaultIfEmpty(0).Max());
            var w3 = Math.Max(4, pieces.Select(p => p.Name.Length).DefaultIfEmpty(0).Max());
            sb.AppendLine($"{"Prefab".PadRight(w1)}  {"Token".PadRight(w2)}  {"Name".PadRight(w3)}  Categories");
            foreach (var row in pieces.OrderBy(p => p.Categories.Count > 1 ? p.Categories[1] : "~", StringComparer.OrdinalIgnoreCase)
                         .ThenBy(p => p.Prefab, StringComparer.OrdinalIgnoreCase))
                sb.AppendLine($"{row.Prefab.PadRight(w1)}  {row.Token.PadRight(w2)}  {row.Name.PadRight(w3)}  {string.Join(", ", row.Categories)}");
            sb.AppendLine();
            sb.AppendLine($"({pieces.Count} pieces)");
        }

        File.WriteAllText(FilePath, sb.ToString(), Encoding.UTF8);
    }

    /// <summary>Keep in step with <see cref="PieceRename.CategoriesOf"/>.</summary>
    static readonly (string Name, string What)[] FunctionCategories =
    {
        ("Chest", "anything with storage (chests, barrels, carts)"),
        ("Door", "doors and gates"),
        ("Bed", "beds"),
        ("Station", "crafting stations and their upgrades (workbench, forge, chopping block, ...)"),
        ("Smelter", "smelters, kilns, furnaces, windmill, spinning wheel, eitr refinery"),
        ("Cooking", "cooking stations and fermenters"),
        ("Portal", "portals"),
        ("Ward", "wards (guard stone)"),
        ("Seat", "chairs, benches, thrones, the hot tub seat"),
    };

    static string Localize(string? text) =>
        string.IsNullOrEmpty(text) ? "" : Localization.instance != null ? Localization.instance.Localize(text) : text!;
}

/// <summary>Every piece (modded ones too) exists once the local player has spawned.</summary>
[HarmonyPatch(typeof(Player), nameof(Player.OnSpawned))]
internal static class ExcludedCategoryReferencePiecesPatch
{
    [HarmonyPostfix]
    static void Postfix(Player __instance)
    {
        if (__instance != Player.m_localPlayer)
            return;
        RenameitConfig.Log?.LogDebug("[PieceRename] local player spawned: listing pieces for the reference file.");
        ExcludedCategoryReferenceWriter.EnsurePiecesListed();
    }
}
