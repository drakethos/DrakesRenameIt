using System;
using System.IO;
using System.Linq;
using System.Text;
using BepInEx;

namespace DrakeRenameit;

/// <summary>Writes <c>ExcludedCategoryReference.txt</c> under <see cref="Paths.ConfigPath"/> on first run (or when outdated).</summary>
internal static class ExcludedCategoryReferenceWriter
{
    private const string FileName = "ExcludedCategoryReference.txt";

    internal static void EnsureGenerated()
    {
        try
        {
            string dir = Path.Combine(Paths.ConfigPath, DrakeRenameit.GUID);
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, FileName);
            string stampLine = $"# DrakeRenameit {DrakeRenameit.Version}";

            if (File.Exists(path))
            {
                try
                {
                    string head = File.ReadAllText(path);
                    if (head.Length > 2048)
                        head = head.Substring(0, 2048);
                    if (head.IndexOf(stampLine, StringComparison.Ordinal) >= 0)
                        return;
                }
                catch
                {
                    // Regenerate if unreadable
                }
            }

            var sb = new StringBuilder();
            sb.AppendLine(stampLine);
            sb.AppendLine("# Auto-generated. Lists valid ExcludedCategory tokens: SkillType, ItemType, and aliases.");
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

            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
        }
        catch
        {
            // Non-fatal; config still works without the file
        }
    }
}
