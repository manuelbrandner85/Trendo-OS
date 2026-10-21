using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Trendo.Core;

public enum UrlKind { Lesemodus, NurTorBrowser, Abgelehnt }
public sealed record UrlVerdict(UrlKind Kind, string Reason, Uri? Uri = null);
public sealed record ReaderPage(string Title, string Text, IReadOnlyList<(string Text, string Href)> Links);
public sealed record ResearchSource(string Url, string Title, DateTimeOffset At, string Sha256, int Chars);

/// <summary>SYS-08 Deep Web & Dark Web READ-ONLY Research: legaler Lesemodus. Normale Seiten werden nur gelesen (GET, keine Formulare/Downloads/Logins),
/// als reiner Text angezeigt; .onion-Adressen öffnet Trendo nie selbst, nur der offizielle Tor Browser. Sitzung ist flüchtig und wird beim Beenden gelöscht.</summary>
public sealed partial class ResearchDesk
{
    public const int MaxBytes = 2 * 1024 * 1024;
    public static readonly (string Name, string Url, string Why)[] Sources =
    [
        ("Wikipedia", "https://de.wikipedia.org", "Überblick zu fast jedem Thema"),
        ("Internet Archive", "https://web.archive.org", "Alte Fassungen von Webseiten"),
        ("BSI für Bürger", "https://www.bsi.bund.de/dok/buerger", "Sicherheitstipps vom Bundesamt"),
        ("Have I Been Pwned", "https://haveibeenpwned.com", "Steckt meine E-Mail in einem Datenleck?"),
        ("Verbraucherzentrale Phishing-Radar", "https://www.verbraucherzentrale.de/phishingradar", "Aktuelle Betrugsmails"),
        ("Tor Project", "https://www.torproject.org/de/download/", "Offizieller Tor Browser für .onion-Seiten"),
    ];

    private readonly string _dir; private readonly AuditLog _audit;
    public ResearchDesk(string dataRoot, AuditLog audit) { _dir = Path.Combine(dataRoot, "tmp", "recherche-sitzung"); _audit = audit; Directory.CreateDirectory(_dir); }

    public static UrlVerdict Classify(string input)
    {
        var s = input.Trim();
        if (s == "") return new(UrlKind.Abgelehnt, "Bitte eine Adresse eingeben.");
        if (!s.Contains("://")) s = "https://" + s;
        if (!Uri.TryCreate(s, UriKind.Absolute, out var u)) return new(UrlKind.Abgelehnt, "Das ist keine gültige Adresse.");
        if (u.Scheme is not ("http" or "https")) return new(UrlKind.Abgelehnt, "Nur Webseiten (http/https) sind erlaubt.");
        if (u.Host.EndsWith(".onion", StringComparison.OrdinalIgnoreCase)) return new(UrlKind.NurTorBrowser, ".onion-Seiten öffnest du nur im offiziellen Tor Browser. Trendo ruft sie nicht selbst ab.", u);
        if (u.Host.EndsWith(".i2p", StringComparison.OrdinalIgnoreCase)) return new(UrlKind.Abgelehnt, "I2P wird nicht unterstützt.");
        if (IPAddress.TryParse(u.Host.Trim('[', ']'), out var ip) && (IPAddress.IsLoopback(ip) || FolderShare.IsPrivateLan(ip) || ip.ToString().StartsWith("169.254.")) || u.Host is "localhost")
            return new(UrlKind.Abgelehnt, "Geräte im eigenen Netz werden im Lesemodus nicht abgerufen.");
        if (u.UserInfo != "") return new(UrlKind.Abgelehnt, "Adressen mit Anmeldedaten sind nicht erlaubt.");
        return new(UrlKind.Lesemodus, "Wird nur gelesen – ohne Formulare, Anmeldung oder Downloads.", u);
    }

    [GeneratedRegex(@"<(script|style|noscript|form|iframe|svg|template|button|select|textarea|object|embed)\b.*?</\1\s*>", RegexOptions.Singleline | RegexOptions.IgnoreCase)] private static partial Regex Drop();
    [GeneratedRegex(@"<a\b[^>]*href\s*=\s*[""']([^""']+)[""'][^>]*>(.*?)</a>", RegexOptions.Singleline | RegexOptions.IgnoreCase)] private static partial Regex A();
    [GeneratedRegex(@"<title[^>]*>(.*?)</title>", RegexOptions.Singleline | RegexOptions.IgnoreCase)] private static partial Regex T();

    /// <summary>Macht aus HTML reinen Lesetext. Skripte, Formulare und eingebettete Inhalte fallen weg.</summary>
    public static ReaderPage ToReader(string html, Uri baseUri)
    {
        var title = System.Web.HttpUtility.HtmlDecode(T().Match(html).Groups[1].Value).Trim();
        var h = Drop().Replace(html, " ");
        h = Regex.Replace(h, @"<!--.*?-->", " ", RegexOptions.Singleline);
        var links = A().Matches(h).Select(m => (Text: Clean(m.Groups[2].Value), Href: m.Groups[1].Value)).Where(l => l.Text.Length > 0)
            .Select(l => Uri.TryCreate(baseUri, l.Href, out var u) && u.Scheme is "http" or "https" ? (l.Text, u.ToString()) : (l.Text, "")).Where(l => l.Item2 != "").Distinct().Take(100).ToList();
        h = Regex.Replace(h, @"<(br|/p|/div|/h[1-6]|/li|/tr|/section|/article)\b[^>]*>", "\n", RegexOptions.IgnoreCase);
        h = Regex.Replace(h, @"<h[1-6][^>]*>", "\n## ", RegexOptions.IgnoreCase);
        h = Regex.Replace(h, @"<li[^>]*>", "\n• ", RegexOptions.IgnoreCase);
        var text = System.Web.HttpUtility.HtmlDecode(Regex.Replace(h, "<[^>]+>", " "));
        text = string.Join("\n", text.Split('\n').Select(l => Regex.Replace(l, @"[ \t\u00a0]+", " ").Trim()).Where(l => l.Length > 0 && l != "##"));
        return new(title == "" ? baseUri.Host : title, text, links);
    }

    private static string Clean(string s) => Regex.Replace(System.Web.HttpUtility.HtmlDecode(Regex.Replace(s, "<[^>]+>", " ")), @"\s+", " ").Trim();

    public ResearchSource Record(Uri u, ReaderPage p, string rawText)
    {
        var src = new ResearchSource(u.ToString(), p.Title, DateTimeOffset.Now, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawText))).ToLowerInvariant(), p.Text.Length);
        File.AppendAllText(Path.Combine(_dir, "quellen.jsonl"), JsonSerializer.Serialize(src) + "\n");
        _audit.Append(Principal.LocalOwner.Name, "research.read", $"{u.Host} {p.Text.Length}Z");
        return src;
    }

    public IReadOnlyList<ResearchSource> SessionSources() { var f = Path.Combine(_dir, "quellen.jsonl"); return !File.Exists(f) ? [] : File.ReadAllLines(f).Where(l => l.Length > 0).Select(l => JsonSerializer.Deserialize<ResearchSource>(l)!).ToList(); }
    public void SaveNote(string text) => File.WriteAllText(Path.Combine(_dir, "notizen.txt"), text);
    public string Note() { var f = Path.Combine(_dir, "notizen.txt"); return File.Exists(f) ? File.ReadAllText(f) : ""; }

    /// <summary>Beendet die Sitzung: Quellenliste und Notizen werden gelöscht (flüchtig). Optional vorher als Bericht exportieren.</summary>
    public void EndSession() { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); Directory.CreateDirectory(_dir); _audit.Append(Principal.LocalOwner.Name, "research.session.end", ""); }

    public string ExportReport(string folder)
    {
        Directory.CreateDirectory(folder);
        var f = Path.Combine(folder, $"recherche-{DateTime.Now:yyyy-MM-dd_HHmm}.md");
        var sb = new StringBuilder("# Recherche (Lesemodus)\n\n## Notizen\n").Append(Note()).Append("\n\n## Quellen\n");
        foreach (var s in SessionSources()) sb.Append($"- {s.At:dd.MM.yyyy HH:mm} [{s.Title}]({s.Url}) · SHA-256 {s.Sha256[..16]}…\n");
        File.WriteAllText(f, sb.ToString()); return f;
    }
}
