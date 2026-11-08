using System.Text.Json.Nodes;

namespace Trendo.Core;

/// <summary>Alle Studios, Runde 8 – CORE-10 „Wissen merken“: Word, PDF, PowerPoint, Excel und HTML mit markitdown (Microsoft, MIT) in Markdown umwandeln,
/// damit sie in der Wissensablage durchsuchbar sind. Läuft in Trendos KI-Laufzeit „Dokumente“ (eigener Python-Prozess, nur lokale Dateien, ohne Plugins).</summary>
public static class DocMarkdown
{
    public static readonly string[] Extensions = [".pdf", ".docx", ".pptx", ".xlsx", ".html", ".htm"];
    public const long MaxBytes = 50L * 1024 * 1024;
    public static bool Supported(string file) => Extensions.Contains(Path.GetExtension(file).ToLowerInvariant());

    /// <summary>Alle passenden Dateien (einzeln oder Ordner mit Unterordnern), höchstens 500, ohne zu große.</summary>
    public static List<string> Files(string path) =>
        File.Exists(path) ? (Supported(path) ? [path] : [])
        : Directory.Exists(path) ? [.. Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories).Where(f => Supported(f) && new FileInfo(f).Length is > 0 and <= MaxBytes).Take(500)]
        : [];

    public static IReadOnlyList<string> Args(string input, string outMd) => [input, outMd];

    public sealed record Result(int Chars, string Title);
    public static Result Parse(string stdout)
    {
        var line = stdout.Replace("\r", "").Split('\n').LastOrDefault(l => l.TrimStart().StartsWith('{')) ?? throw new FormatException("Keine Antwort.");
        var j = JsonNode.Parse(line)!;
        return new(j["zeichen"]?.GetValue<int>() ?? 0, j["titel"]?.GetValue<string>() ?? "");
    }

    /// <summary>Seitentitel: Titel aus dem Dokument, sonst Dateiname.</summary>
    public static string TitleFor(string file, Result r) => r.Title.Trim().Length > 0 ? r.Title.Trim() : Path.GetFileNameWithoutExtension(file);
}
