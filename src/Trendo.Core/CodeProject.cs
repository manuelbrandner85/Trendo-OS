using System.Text;

namespace Trendo.Core;

/// <summary>Code-Editor – selbst nachgebaut statt VSCodium (Nachbau-Plan 04.11.2026): Projektbaum, Sprache je Datei, Suche in allen Dateien,
/// Ausführen je Dateityp. Nur Dateien im gewählten Projektordner; Bau-, Paket- und Git-Ordner werden ausgeblendet.</summary>
public static class CodeProject
{
    public static readonly HashSet<string> Hidden = new(StringComparer.OrdinalIgnoreCase) { ".git", "bin", "obj", "node_modules", ".vs", ".idea", "__pycache__", ".venv", "venv", "dist", "build", ".next" };
    public const long MaxEdit = 5_000_000; // größere Dateien öffnet der Editor nicht (Absturzschutz)

    public sealed record Entry(string Path, string Name, int Depth, bool IsDir);

    /// <summary>Baum als flache Liste (Ordner zuerst, alphabetisch). collapsed: zugeklappte Ordner.</summary>
    public static List<Entry> Tree(string root, ISet<string>? collapsed = null, int max = 4000)
    {
        var list = new List<Entry>();
        void Walk(string dir, int depth)
        {
            if (list.Count >= max) return;
            IEnumerable<string> dirs, files;
            try { dirs = Directory.GetDirectories(dir).Where(d => !Hidden.Contains(System.IO.Path.GetFileName(d))).Order(StringComparer.OrdinalIgnoreCase); files = Directory.GetFiles(dir).Order(StringComparer.OrdinalIgnoreCase); }
            catch (Exception e) when (e is UnauthorizedAccessException or IOException) { return; }
            foreach (var d in dirs)
            {
                list.Add(new(d, System.IO.Path.GetFileName(d), depth, true));
                if (collapsed is null || !collapsed.Contains(d)) Walk(d, depth + 1);
            }
            foreach (var f in files) { if (list.Count >= max) return; list.Add(new(f, System.IO.Path.GetFileName(f), depth, false)); }
        }
        if (Directory.Exists(root)) Walk(root, 0);
        return list;
    }

    /// <summary>Name der Hervorhebung (wie in AvaloniaEdit) und deutscher Anzeigename.</summary>
    public static (string? Highlight, string Label) Language(string file) => System.IO.Path.GetExtension(file).ToLowerInvariant() switch
    {
        ".cs" => ("C#", "C#"), ".ts" or ".tsx" => ("JavaScript", "TypeScript"), ".js" or ".mjs" or ".cjs" or ".jsx" => ("JavaScript", "JavaScript"),
        ".py" => ("Python", "Python"), ".html" or ".htm" => ("HTML", "HTML"), ".css" or ".scss" => ("CSS", "CSS"), ".json" => ("Json", "JSON"),
        ".xml" or ".csproj" or ".axaml" or ".xaml" or ".svg" or ".props" or ".targets" => ("XML", "XML"), ".md" => ("MarkDown", "Markdown"),
        ".java" => ("Java", "Java"), ".cpp" or ".c" or ".h" or ".hpp" => ("C++", "C/C++"), ".php" => ("PHP", "PHP"), ".ps1" => ("PowerShell", "PowerShell"),
        ".sql" => ("TSQL", "SQL"), ".vb" => ("VB", "Visual Basic"), ".patch" or ".diff" => ("Patch", "Änderung"),
        ".sh" => (null, "Shell"), ".yaml" or ".yml" => (null, "YAML"), ".toml" => (null, "TOML"), _ => (null, "Text"),
    };

    /// <summary>Datei zum Bearbeiten lesen; Binärdateien und sehr große Dateien werden abgelehnt.</summary>
    public static string ReadText(string file)
    {
        var fi = new FileInfo(file);
        if (!fi.Exists) throw new FileNotFoundException("Datei nicht gefunden: " + fi.Name);
        if (fi.Length > MaxEdit) throw new InvalidOperationException($"„{fi.Name}“ ist zu groß für den Editor ({fi.Length / 1_000_000} MB).");
        var bytes = File.ReadAllBytes(file);
        if (Array.IndexOf(bytes, (byte)0, 0, Math.Min(bytes.Length, 8000)) >= 0) throw new InvalidOperationException($"„{fi.Name}“ ist keine Textdatei.");
        return new UTF8Encoding(false).GetString(bytes).TrimStart('﻿');
    }

    /// <summary>Speichern: Zeilenenden der Datei behalten (CRLF bleibt CRLF), UTF-8 ohne BOM.</summary>
    public static void WriteText(string file, string text)
    {
        var crlf = File.Exists(file) && File.ReadAllText(file).Contains("\r\n");
        var norm = text.Replace("\r\n", "\n");
        File.WriteAllText(file, crlf ? norm.Replace("\n", "\r\n") : norm, new UTF8Encoding(false));
    }

    public sealed record Hit(string File, int Line, string Text);

    /// <summary>In allen Textdateien des Projekts suchen (ohne Groß/Klein), höchstens max Treffer.</summary>
    public static List<Hit> Search(string root, string query, int max = 300)
    {
        var hits = new List<Hit>();
        if (string.IsNullOrWhiteSpace(query)) return hits;
        foreach (var e in Tree(root).Where(e => !e.IsDir))
        {
            if (hits.Count >= max) break;
            try
            {
                if (new FileInfo(e.Path).Length > MaxEdit) continue;
                var lines = File.ReadAllLines(e.Path);
                if (lines.Any(l => l.Contains('\0'))) continue;
                for (var i = 0; i < lines.Length && hits.Count < max; i++)
                    if (lines[i].Contains(query, StringComparison.OrdinalIgnoreCase)) hits.Add(new(e.Path, i + 1, lines[i].Trim()));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
        return hits;
    }

    /// <summary>„Ausführen“ für eine Datei: Python, Node, PowerShell; bei einem .NET-Projekt dotnet run im Projektordner.</summary>
    public static (string Exe, string[] Args, string Cwd)? RunCommand(string root, string file) => System.IO.Path.GetExtension(file).ToLowerInvariant() switch
    {
        ".py" => (OperatingSystem.IsWindows() ? "python" : "python3", [file], System.IO.Path.GetDirectoryName(file)!),
        ".js" or ".mjs" => ("node", [file], System.IO.Path.GetDirectoryName(file)!),
        ".ps1" when OperatingSystem.IsWindows() => ("powershell", ["-NoProfile", "-ExecutionPolicy", "Bypass", "-File", file], System.IO.Path.GetDirectoryName(file)!),
        _ when Directory.GetFiles(root, "*.csproj").Length > 0 => ("dotnet", ["run"], root),
        _ => null,
    };

    /// <summary>Pfad im Projekt halten: neue Dateien nur unterhalb des Projektordners.</summary>
    public static string Inside(string root, string relative)
    {
        var full = System.IO.Path.GetFullPath(System.IO.Path.Combine(root, relative.Trim().TrimStart('/', '\\')));
        var r = System.IO.Path.GetFullPath(root).TrimEnd(System.IO.Path.DirectorySeparatorChar) + System.IO.Path.DirectorySeparatorChar;
        if (!full.StartsWith(r, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Neue Dateien nur innerhalb des Projektordners.");
        return full;
    }
}
