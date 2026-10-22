using System.Text.RegularExpressions;

namespace Trendo.Core;

public sealed record WebBlick(string Titel, string Ueberschrift, string Absatz, string Knopf, string Hintergrund);

/// <summary>Textdateien eines Projekts lesen und speichern, Vorschau und Überschrift vor dem Übernehmen zeigen.</summary>
public static class WebArbeit
{
    static readonly HashSet<string> Skip = new(StringComparer.OrdinalIgnoreCase) { "bin", "obj", "node_modules", ".git", "__pycache__", "fertig" };
    static readonly string[] Ext = [".html", ".htm", ".css", ".js", ".md", ".json", ".cs", ".py", ".txt", ".csproj"];

    public static IReadOnlyList<string> Dateien(string dir)
    {
        if (!Directory.Exists(dir)) return [];
        var list = new List<string>();
        void Walk(string d)
        {
            foreach (var sub in Directory.GetDirectories(d))
            {
                if (Skip.Contains(Path.GetFileName(sub))) continue;
                Walk(sub);
            }
            foreach (var f in Directory.GetFiles(d))
                if (Ext.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase)) list.Add(f);
        }
        Walk(dir);
        return list.OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public static string Lesen(string path) => File.ReadAllText(path);

    public static void Speichern(string path, string text)
    {
        var folder = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);
        File.WriteAllText(path, text.Replace("\r\n", "\n"));
    }

    public static WebBlick Blick(string dir)
    {
        var htmlPath = Directory.Exists(dir) ? Directory.GetFiles(dir, "index.html", SearchOption.AllDirectories).FirstOrDefault(p => !p.Contains($"{Path.DirectorySeparatorChar}fertig{Path.DirectorySeparatorChar}")) : null;
        var html = htmlPath is null ? "" : File.ReadAllText(htmlPath);
        var cssPath = htmlPath is null ? null : Path.Combine(Path.GetDirectoryName(htmlPath)!, "style.css");
        var css = cssPath is not null && File.Exists(cssPath) ? File.ReadAllText(cssPath) : "";
        string Tag(string name)
        {
            var m = Regex.Match(html, $"<{name}[^>]*>(?<t>.*?)</{name}>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            return m.Success ? Regex.Replace(m.Groups["t"].Value, "<[^>]+>", "").Trim() : "";
        }
        var title = Regex.Match(html, "<title[^>]*>(?<t>.*?)</title>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        var bg = Regex.Match(css, @"background\s*:\s*(?<c>#[0-9a-fA-F]{3,8}|[a-zA-Z]+)", RegexOptions.IgnoreCase);
        return new(title.Success ? title.Groups["t"].Value.Trim() : "", Tag("h1"), Tag("p"), Tag("button"), bg.Success ? bg.Groups["c"].Value : "");
    }

    /// <summary>Erkennt „Ändere die Überschrift in …“. Schreibt nichts. Liefert den alten und den neuen Dateiinhalt.</summary>
    public static (string Datei, string Vorher, string Nachher, string Alt, string Neu)? Ueberschrift(string dir, string satz)
    {
        var m = Regex.Match(satz.Trim(), @"^(?:ändere|aendere|ersetze)\s+die\s+überschrift\s+in\s+[„""']?(?<n>.+?)[“""']?\s*$", RegexOptions.IgnoreCase);
        if (!m.Success) return null;
        var neu = m.Groups["n"].Value.Trim();
        if (neu.Length == 0 || !Directory.Exists(dir)) return null;
        var html = Directory.GetFiles(dir, "*.html", SearchOption.AllDirectories).FirstOrDefault(p => !p.Contains($"{Path.DirectorySeparatorChar}fertig{Path.DirectorySeparatorChar}"));
        if (html is null) return null;
        var text = File.ReadAllText(html);
        var rx = new Regex(@"<h1(\s[^>]*)?>(?<t>.*?)</h1>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        var hit = rx.Match(text);
        if (!hit.Success) return null;
        var alt = Regex.Replace(hit.Groups["t"].Value, "<[^>]+>", "").Trim();
        var encoded = System.Net.WebUtility.HtmlEncode(neu);
        var nachher = rx.Replace(text, _ => $"<h1>{encoded}</h1>", 1);
        return (html, text, nachher, alt, neu);
    }
}
