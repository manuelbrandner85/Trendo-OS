using System.Text.Json;
using System.Text.Json.Nodes;

namespace Trendo.Core;

/// <summary>Z8: eigene HTML-Seiten in Bausteine übersetzen. Quelle ist nur eine Datei auf diesem PC; eine Internet-Adresse wird abgelehnt (fremde Websites werden nicht importiert).
/// Das Einlesen selbst macht der GrapesJS-Kern im Browser (App), hier liegen Prüfung der Quelle und das Anlegen der Seite.</summary>
public static class WebDesignImport
{
    public const long MaxBytes = 5 * 1024 * 1024;
    public sealed record Outcome(string? Adresse, string? Error, IReadOnlyList<string> Report, int Mapped, int HtmlBlocks);

    /// <summary>Quelle prüfen: nur eine vorhandene .html/.htm-Datei auf diesem PC, keine Adresse aus dem Internet, höchstens 5 MB.</summary>
    public static string? CheckSource(string? path)
    {
        var p = (path ?? "").Trim().Trim('"');
        if (p.Length == 0) return "Bitte eine HTML-Datei wählen.";
        if (p.Contains("://", StringComparison.Ordinal) || p.StartsWith("www.", StringComparison.OrdinalIgnoreCase) || p.StartsWith("//", StringComparison.Ordinal) || p.StartsWith(@"\\", StringComparison.Ordinal))
            return "Eine Internet-Adresse ist als Quelle nicht erlaubt. Trendo importiert nur eigene Dateien auf diesem PC, keine fremden Websites.";
        if (!Path.IsPathFullyQualified(p)) return "Bitte den vollständigen Pfad einer Datei auf diesem PC angeben.";
        if (LocalPathError(p) is { } lerr) return lerr;
        if (Path.GetExtension(p).ToLowerInvariant() is not (".html" or ".htm")) return "Nur HTML-Dateien (.html, .htm) lassen sich importieren.";
        if (!File.Exists(p)) return "Diese Datei gibt es nicht.";
        if ((File.GetAttributes(p) & FileAttributes.ReparsePoint) != 0) return "Verknüpfungen werden nicht importiert. Bitte die Datei selbst wählen.";
        if (new FileInfo(p).Length > MaxBytes) return "Die Datei ist zu groß (über 5 MB).";
        return null;
    }

    /// <summary>Nur Dateien auf einem lokalen Laufwerk: Netzwerkpfade (\\server, \\?\UNC, /\server …) und Netzlaufwerke werden abgelehnt,
    /// auch wenn sie erst nach dem Auflösen des Pfads als solche erkennbar sind (sonst baute Windows eine Verbindung zum fremden Server auf).</summary>
    public static string? LocalPathError(string path)
    {
        string full;
        try { full = Path.GetFullPath(path); } catch (Exception e) when (e is ArgumentException or NotSupportedException or PathTooLongException) { return "Dieser Pfad ist ungültig."; }
        if (full.StartsWith(@"\\", StringComparison.Ordinal) || full.StartsWith("//", StringComparison.Ordinal) || full.StartsWith(@"\??\", StringComparison.Ordinal) || !OperatingSystem.IsWindows() && !full.StartsWith('/'))
            return "Netzwerkpfade sind nicht erlaubt. Bitte eine Datei auf diesem PC wählen.";
        if (OperatingSystem.IsWindows())
        {
            if (full.Length < 3 || full[1] != ':' || !char.IsLetter(full[0])) return "Bitte eine Datei auf einem Laufwerk dieses PCs wählen.";
            try { if (new DriveInfo(full[..3]).DriveType is DriveType.Network or DriveType.NoRootDirectory) return "Netzlaufwerke sind nicht erlaubt. Bitte die Datei zuerst auf diesen PC kopieren."; }
            catch (Exception e) when (e is ArgumentException or IOException) { return "Dieses Laufwerk ist nicht erreichbar."; }
        }
        return null;
    }

    /// <summary>Gelesenen Baum (JSON von trendoParseHtml) als neue Seite anlegen. Der Titel kommt aus der Datei, wenn sie einen hat.</summary>
    public static Outcome CreatePage(string projectDir, string sourceFile, string parsedJson, string? titel = null)
    {
        if (CheckSource(sourceFile) is { } err) return new(null, err, [], 0, 0);
        JsonArray tree; int cssRules;
        try
        {
            var o = JsonNode.Parse(parsedJson) as JsonObject ?? throw new JsonException();
            tree = o["knoten"] as JsonArray ?? throw new JsonException();
            cssRules = (int)(WebDesign.Number(o["css"]) ?? 0);
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException) { return new(null, "Die Datei ließ sich nicht lesen.", [], 0, 0); }
        var ctx = new WebDesignHtml.Context(projectDir, Path.GetDirectoryName(Path.GetFullPath(sourceFile)));
        var r = WebDesignHtml.FromTree(tree, ctx);
        var report = r.Report.ToList();
        if (cssRules > 0) report.Add($"{cssRules} Stilregel(n) der Datei nicht übernommen: Farben, Schrift und Abstände kommen aus der Gestaltung des Projekts.");
        if (r.Content.Count == 0) return new(null, "In der Datei steht kein übernehmbarer Inhalt.", report, 0, 0);
        var name = titel?.Trim() is { Length: > 0 } t ? t : TitleFrom(File.ReadAllText(sourceFile)) ?? Path.GetFileNameWithoutExtension(sourceFile);
        var slug = WebDesignSite.SlugOf(Path.GetFileNameWithoutExtension(sourceFile));
        if (slug is "" or "index" || WebDesignSite.CheckSlug(slug) is not null) slug = "importierte-seite"; // z. B. index.html, de.html, sitemap.html
        var pages = WebDesignSite.Pages(projectDir);
        var baseSlug = slug; for (var i = 2; pages.Contains(slug) || WebDesignCms.Load(projectDir).Any(c => c.Id == slug); i++) slug = $"{baseSlug}-{i}";
        if (WebDesignSite.Create(projectDir, slug, name) is { } cerr) return new(null, cerr, report, r.Mapped, r.HtmlBlocks);
        var doc = new JsonObject { ["root"] = new JsonObject { ["props"] = new JsonObject { ["title"] = name, ["description"] = "" } }, ["content"] = r.Content, ["zones"] = new JsonObject() };
        if (WebDesign.Save(projectDir, doc.ToJsonString()) is { } serr) return new(null, serr, report, r.Mapped, r.HtmlBlocks);
        return new(slug, null, report, r.Mapped, r.HtmlBlocks);
    }

    /// <summary>Titel aus &lt;title&gt; der Datei (nur Text, kurz).</summary>
    static string? TitleFrom(string html)
    {
        var a = html.IndexOf("<title", StringComparison.OrdinalIgnoreCase); if (a < 0) return null;
        var s = html.IndexOf('>', a); var e = html.IndexOf("</title>", s + 1, StringComparison.OrdinalIgnoreCase);
        if (s < 0 || e < 0) return null;
        var t = System.Net.WebUtility.HtmlDecode(html[(s + 1)..e]).Trim();
        return t.Length is > 0 and <= 120 && !t.Contains('<') ? t : null;
    }
}
