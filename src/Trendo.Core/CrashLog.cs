using System.Reflection;
using System.Text.RegularExpressions;

namespace Trendo.Core;

/// <summary>SYS-13: Absturzprotokoll im Benutzerdatenordner (logs/). Pfade mit Benutzernamen werden geschwärzt, max. 20 Dateien.</summary>
public static class CrashLog
{
    public const int Keep = 20;
    public static string Dir(string dataRoot) => Path.Combine(dataRoot, "logs");

    public static string Redact(string s)
    {
        var user = Environment.UserName;
        s = Regex.Replace(s, @"(?i)([A-Z]:\\Users\\|/home/|/Users/)[^\\/\s]+", m => m.Groups[1].Value + "<benutzer>");
        if (user.Length > 2) s = s.Replace(user, "<benutzer>", StringComparison.OrdinalIgnoreCase);
        return s;
    }

    public static string Write(string dataRoot, Exception ex, string where, DateTime? now = null)
    {
        var d = Dir(dataRoot); Directory.CreateDirectory(d);
        var t = now ?? DateTime.Now;
        var f = Path.Combine(d, $"absturz-{t:yyyyMMdd-HHmmss-fff}.txt");
        File.WriteAllText(f, Redact($"Trendo OS {AppVersion()}\nZeit: {t:O}\nOrt: {where}\nSystem: {Environment.OSVersion}\n\n{ex}\n"));
        foreach (var old in List(dataRoot).Skip(Keep)) try { File.Delete(old); } catch { }
        return f;
    }

    public static IReadOnlyList<string> List(string dataRoot) =>
        Directory.Exists(Dir(dataRoot)) ? Directory.GetFiles(Dir(dataRoot), "absturz-*.txt").OrderByDescending(x => x, StringComparer.Ordinal).ToList() : [];

    /// <summary>Version des laufenden Programms (InformationalVersion ohne Commit-Hash) und Commit getrennt.</summary>
    public static string AppVersion(Assembly? a = null)
    {
        var v = (a ?? Assembly.GetEntryAssembly() ?? typeof(CrashLog).Assembly).GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unbekannt";
        var i = v.IndexOf('+'); return i < 0 ? v : $"{v[..i]} (Stand {v[(i + 1)..Math.Min(v.Length, i + 8)]})";
    }
}
