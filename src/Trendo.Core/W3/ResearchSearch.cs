using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Trendo.Core;

/// <summary>Ein Suchtreffer mit Herkunft: welcher Dienst, welche Suchadresse, wann.</summary>
public sealed record WebSearchHit(string Title, string Url, string Snippet, string Engine, string QueryUrl, DateTimeOffset At);
/// <summary>Eine ausgeführte Suche (für Sammlung und Bericht): Dienst, Suchwort, Adresse, Zeit, Trefferzahl, SHA-256 der Antwort.</summary>
public sealed record ResearchSearchLog(string Engine, string Query, string QueryUrl, DateTimeOffset At, int Hits, string Sha256);

/// <summary>T-0735 (SYS-08): Suche über öffentliche Suchdienste ohne Konto. Der Kern baut nur die Adresse und liest die Antwort;
/// den Abruf macht die App über ihren vorhandenen Lese-Weg (nur öffentliche Adressen, höchstens 2 MB). Treffer, die der Lesemodus
/// nicht öffnen darf (.onion, Heimnetz, andere Schemata), fallen weg – Trendo ruft keine Darknet-Inhalte ab.</summary>
public static partial class ResearchSearch
{
    public static readonly (string Id, string Name, string Why)[] Engines =
    [
        ("wikipedia", "Wikipedia (Deutsch)", "Artikel der deutschen Wikipedia – Suche über die offene Schnittstelle"),
        ("duckduckgo", "DuckDuckGo (ganzes Netz)", "Webseiten aus dem ganzen Netz – ohne Konto, ohne Verfolgung"),
    ];

    public const int MaxQuery = 300;

    public static string EngineName(string id) => Engines.FirstOrDefault(e => e.Id == id).Name ?? id;

    public static Uri QueryUrl(string engine, string query)
    {
        var q = (query ?? "").Trim();
        if (q.Length == 0) throw new ArgumentException("Bitte einen Suchbegriff eingeben.");
        if (q.Length > MaxQuery) throw new ArgumentException("Der Suchbegriff ist zu lang (höchstens 300 Zeichen).");
        return engine switch
        {
            "wikipedia" => new Uri("https://de.wikipedia.org/w/api.php?action=query&list=search&format=json&formatversion=2&utf8=1&srlimit=20&srsearch=" + Uri.EscapeDataString(q)),
            "duckduckgo" => new Uri("https://html.duckduckgo.com/html/?kl=de-de&q=" + Uri.EscapeDataString(q)),
            _ => throw new ArgumentException("Diesen Suchdienst gibt es nicht."),
        };
    }

    [GeneratedRegex(@"<a\b[^>]*class=""[^""]*\bresult__a\b[^""]*""[^>]*>(.*?)</a>", RegexOptions.Singleline | RegexOptions.IgnoreCase)] private static partial Regex DdgA();
    [GeneratedRegex(@"href=""([^""]+)""", RegexOptions.IgnoreCase)] private static partial Regex Href();
    [GeneratedRegex(@"class=""[^""]*\bresult__snippet\b[^""]*""[^>]*>(.*?)</(?:a|div|td)>", RegexOptions.Singleline | RegexOptions.IgnoreCase)] private static partial Regex DdgSnippet();

    static string Clean(string s) => Regex.Replace(System.Web.HttpUtility.HtmlDecode(Regex.Replace(s ?? "", "<[^>]+>", "")), @"\s+", " ").Trim();

    /// <summary>Liest die Antwort des Suchdienstes. Nur Treffer, die der Lesemodus öffnen darf; höchstens 20, ohne doppelte Adressen.</summary>
    public static IReadOnlyList<WebSearchHit> Parse(string engine, string body, Uri queryUrl, DateTimeOffset at)
    {
        var hits = new List<WebSearchHit>(); var name = EngineName(engine);
        void Add(string title, string url, string snippet)
        {
            if (title.Length == 0 || ResearchDesk.Classify(url) is not { Kind: UrlKind.Lesemodus, Uri: { } u }) return;
            var s = u.ToString();
            if (hits.Any(h => h.Url == s) || hits.Count >= 20) return;
            hits.Add(new(title, s, snippet.Length > 300 ? snippet[..300] + " …" : snippet, name, queryUrl.ToString(), at));
        }
        if (engine == "wikipedia")
        {
            using var d = JsonDocument.Parse(body);
            if (!d.RootElement.TryGetProperty("query", out var q) || !q.TryGetProperty("search", out var list) || list.ValueKind != JsonValueKind.Array)
                throw new InvalidDataException("Wikipedia hat keine Trefferliste geschickt.");
            foreach (var e in list.EnumerateArray())
            {
                var title = e.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "";
                var snip = e.TryGetProperty("snippet", out var sn) ? Clean(sn.GetString() ?? "") : "";
                Add(title, "https://de.wikipedia.org/wiki/" + Uri.EscapeDataString(title.Replace(' ', '_')).Replace("%28", "(").Replace("%29", ")"), snip);
            }
        }
        else if (engine == "duckduckgo")
        {
            var anchors = DdgA().Matches(body);
            var snippets = DdgSnippet().Matches(body).Select(m => Clean(m.Groups[1].Value)).ToList();
            var i = 0;
            foreach (Match m in anchors)
            {
                var href = System.Web.HttpUtility.HtmlDecode(Href().Match(m.Value).Groups[1].Value);
                var target = Unwrap(href);
                var snip = i < snippets.Count ? snippets[i] : ""; i++;
                if (target is null) continue;
                Add(Clean(m.Groups[1].Value), target, snip);
            }
            if (anchors.Count == 0 && body.Contains("anomaly", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("DuckDuckGo hat die Suche gerade abgelehnt (Prüfseite). Bitte später noch einmal oder Wikipedia wählen.");
        }
        else throw new ArgumentException("Diesen Suchdienst gibt es nicht.");
        return hits;
    }

    /// <summary>DuckDuckGo verpackt Ziele als „//duckduckgo.com/l/?uddg=…“. Werbung („y.js“) und Links auf DuckDuckGo selbst fallen weg.</summary>
    public static string? Unwrap(string href)
    {
        if (href.StartsWith("//", StringComparison.Ordinal)) href = "https:" + href;
        if (!Uri.TryCreate(href, UriKind.Absolute, out var u)) return null;
        if (u.Host.EndsWith("duckduckgo.com", StringComparison.OrdinalIgnoreCase))
        {
            var target = System.Web.HttpUtility.ParseQueryString(u.Query)["uddg"];
            if (string.IsNullOrEmpty(target) || !Uri.TryCreate(target, UriKind.Absolute, out var t) || t.Host.EndsWith("duckduckgo.com", StringComparison.OrdinalIgnoreCase)) return null;
            return t.ToString();
        }
        return u.ToString();
    }
}

public sealed partial class ResearchDesk
{
    string SearchFile => Path.Combine(_dir, "suchen.jsonl");
    string HitsFile => Path.Combine(_dir, "treffer.json");

    /// <summary>Hält eine Suche mit Herkunft fest (Sammlung + Prüfprotokoll) und merkt sich die letzte Trefferliste.</summary>
    public ResearchSearchLog RecordSearch(string engine, string query, Uri queryUrl, byte[] body, IReadOnlyList<WebSearchHit> hits)
    {
        var log = new ResearchSearchLog(ResearchSearch.EngineName(engine), query.Trim(), queryUrl.ToString(), hits.FirstOrDefault()?.At ?? DateTimeOffset.Now, hits.Count,
            Convert.ToHexString(SHA256.HashData(body)).ToLowerInvariant());
        Directory.CreateDirectory(_dir);
        File.AppendAllText(SearchFile, JsonSerializer.Serialize(log) + "\n");
        AtomicFile.WriteAllText(HitsFile, JsonSerializer.Serialize(hits.ToList()));
        _audit.Append(Principal.LocalOwner.Name, "research.search", $"{engine} {hits.Count} Treffer");
        return log;
    }

    public IReadOnlyList<ResearchSearchLog> Searches() => !File.Exists(SearchFile) ? [] :
        File.ReadAllLines(SearchFile).Where(l => l.Length > 0).Select(l => { try { return JsonSerializer.Deserialize<ResearchSearchLog>(l); } catch (JsonException) { return null; } }).OfType<ResearchSearchLog>().ToList();

    /// <summary>Letzte Trefferliste (bleibt mit „Sammlung behalten“ auch nach einem Neustart).</summary>
    public IReadOnlyList<WebSearchHit> LastHits()
    {
        try { return File.Exists(HitsFile) ? JsonSerializer.Deserialize<List<WebSearchHit>>(File.ReadAllText(HitsFile)) ?? [] : []; }
        catch (JsonException) { return []; }
    }
}
