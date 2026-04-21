using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace DrakeRenameit.AgentDebug;

/// <summary>NDJSON one-liner log for debug mode (workspace + plugin folder).</summary>
internal static class AgentDebugLog
{
    private const string SessionId = "a20b7d";
    private const string FileName = "debug-a20b7d.log";
    private static readonly object Sync = new();
    private static string[]? _paths;

    private static void TryAdd(HashSet<string> seen, List<string> list, string? p)
    {
        if (string.IsNullOrWhiteSpace(p))
            return;
        try
        {
            var full = Path.GetFullPath(p);
            if (seen.Add(full))
                list.Add(full);
        }
        catch
        {
            /* ignore */
        }
    }

    private static string[] ResolvePaths()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var list = new List<string>();

        TryAdd(seen, list, Environment.GetEnvironmentVariable("DRAKESRENAMEIT_DEBUG_LOG"));

        try
        {
            var dllDir = Path.GetDirectoryName(typeof(AgentDebugLog).Assembly.Location);
            TryAdd(seen, list, Path.Combine(dllDir ?? "", FileName));

            var asmDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            if (!string.IsNullOrEmpty(asmDir))
            {
                var dir = new DirectoryInfo(asmDir);
                for (var i = 0; i < 8 && dir != null; i++, dir = dir.Parent)
                {
                    var marker = Path.Combine(dir.FullName, "DrakeRenameit.csproj");
                    if (File.Exists(marker))
                    {
                        TryAdd(seen, list, Path.Combine(dir.FullName, FileName));
                        break;
                    }
                }
            }
        }
        catch
        {
            /* ignore */
        }

        TryAdd(seen, list, Path.Combine(Path.GetTempPath(), "DrakesRenameIt-" + FileName));
        return list.ToArray();
    }

    private static string[] Paths => _paths ??= ResolvePaths();

    internal static void Write(string runId, string hypothesisId, string location, string message, string? data = null)
    {
        try
        {
            var payload =
                "{\"sessionId\":\"" + SessionId + "\"," +
                "\"runId\":\"" + Escape(runId) + "\"," +
                "\"hypothesisId\":\"" + Escape(hypothesisId) + "\"," +
                "\"location\":\"" + Escape(location) + "\"," +
                "\"message\":\"" + Escape(message) + "\"," +
                "\"data\":\"" + Escape(data ?? "") + "\"," +
                "\"timestamp\":" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() +
                "}";

            lock (Sync)
            {
                foreach (var path in Paths)
                {
                    try
                    {
                        var dir = Path.GetDirectoryName(path);
                        if (!string.IsNullOrEmpty(dir))
                            Directory.CreateDirectory(dir);
                        File.AppendAllText(path, payload + Environment.NewLine);
                    }
                    catch
                    {
                        /* ignore per path */
                    }
                }
            }
        }
        catch
        {
            /* never throw from debug log */
        }
    }

    private static string Escape(string s) =>
        (s ?? "")
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\r", "\\r")
            .Replace("\n", "\\n");
}
