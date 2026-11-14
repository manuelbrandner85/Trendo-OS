using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Trendo.Core.Programs;

/// <summary>T-0650, D5: ähnliche, real existierende Spiele als Vergleich für Designentscheidungen. Die KI schlägt nur Titel vor; gezeigt wird ein Titel erst,
/// wenn ein Wikipedia-Artikel ihn als Computer- oder Videospiel belegt. Lässt sich nichts belegen, steht dort „keine Referenz gefunden“ – nie ein erfundener Titel.
/// Übernommen werden nur Titel, Kurzbeschreibung und Link, keine Inhalte oder Figuren.</summary>
public static class GameReferences
{
    public const string None = "keine Referenz gefunden";
    public sealed record Ref(string Title, string Url, string Note, string Source);

    public static string Instructions() =>
        "Du nennst real existierende, veröffentlichte Computer- oder Videospiele, die einer Spielidee ähneln. Nur Titel, die es wirklich gibt. " +
        "Antworte mit höchstens 6 Zeilen, je Zeile nur der offizielle Titel. Keine Erklärungen, keine Nummern. Kennst du keines sicher, antworte mit „keine“.";

    public static string Prompt(JsonObject d)
    {
        string S(string k) => d[k] is JsonValue v && v.TryGetValue<string>(out var s) ? s.Trim() : "";
        var parts = new[] { ("Titel", S("title")), ("Genre", S("genre")), ("Stil", S("style")), ("Idee", S("logline")), ("Spielablauf", S("loop")), ("Perspektive", S("perspective")) }
            .Where(p => p.Item2.Length > 0).Select(p => $"{p.Item1}: {p.Item2}");
        return "Welche veröffentlichten Spiele ähneln dieser Idee am meisten?\n" + string.Join("\n", parts);
    }

    /// <summary>Titel aus der KI-Antwort: Aufzählungszeichen und Jahreszahlen entfernt, ohne Doppelte, höchstens 6.</summary>
    public static List<string> Candidates(string answer)
    {
        var list = new List<string>();
        foreach (var raw in (answer ?? "").Split('\n'))
        {
            var t = raw.Trim().TrimStart('-', '*', '•', ' ').Trim();
            t = System.Text.RegularExpressions.Regex.Replace(t, @"^\d+[.)]\s*", "");
            t = System.Text.RegularExpressions.Regex.Replace(t, @"\s*[\(\[]\d{4}[\)\]]\s*$", "").Trim().Trim('"', '„', '“', '*');
            var cut = t.IndexOf('|'); if (cut > 0) t = t[..cut].Trim(); // kein Gedankenstrich: er gehört oft zum Titel
            if (t.Length is < 2 or > 80 || t.Equals("keine", StringComparison.OrdinalIgnoreCase)) continue;
            if (!list.Contains(t, StringComparer.OrdinalIgnoreCase)) list.Add(t);
            if (list.Count == 6) break;
        }
        return list;
    }

    /// <summary>Nur eindeutige Wörter für Computer- und Videospiele; ausgeschlossen wird, was ein Film, eine Serie, ein Buch oder ein Brett- oder Kartenspiel ist.</summary>
    static readonly string[] GameWords = ["computerspiel", "videospiel", "video game", "videogame", "browserspiel", "handyspiel", "mobile game"];
    static readonly string[] NotGame = ["film", "fernsehserie", "television", "tv series", "roman", "novel", "brettspiel", "board game", "kartenspiel", "card game", "tabletop", "pen-and-paper", "comic", "spielereihe", "spielreihe", "video game series", "franchise", "begriffsklärung"];

    static string Norm(string s) => new([.. s.ToLowerInvariant().Normalize(System.Text.NormalizationForm.FormD).Where(char.IsLetterOrDigit)]);

    public static string SummaryUrl(string lang, string title) =>
        $"https://{lang}.wikipedia.org/api/rest_v1/page/summary/{Uri.EscapeDataString(title.Trim().Replace(' ', '_'))}?redirect=true";

    /// <summary>Wikipedia-Zusammenfassung prüfen: kein Begriffsklärungs-Artikel, als Spiel beschrieben, Titel passt zum Vorschlag.</summary>
    public static Ref? FromSummary(string asked, string json, string lang)
    {
        try
        {
            var o = JsonNode.Parse(json)!.AsObject();
            if ((string?)o["type"] == "disambiguation") return null;
            var title = (string?)o["title"] ?? ""; var desc = (string?)o["description"] ?? ""; var extract = (string?)o["extract"] ?? "";
            var url = (string?)o["content_urls"]?["desktop"]?["page"] ?? "";
            if (title.Length == 0 || !Uri.TryCreate(url, UriKind.Absolute, out var u) || u.Scheme != "https" || u.Host != $"{lang}.wikipedia.org") return null;
            // Erst die Kurzbeschreibung; nur wenn sie fehlt, der erste Satz des Artikels
            var text = (desc.Length > 0 ? desc : extract.Split(". ")[0]).ToLowerInvariant();
            if (!GameWords.Any(text.Contains) || NotGame.Any(text.Contains)) return null;
            var a = Norm(asked); var t = Norm(title);
            if (a.Length == 0 || !(t == a || t.StartsWith(a, StringComparison.Ordinal) || a.StartsWith(t, StringComparison.Ordinal))) return null;
            var note = desc.Length > 0 ? desc : extract.Split(". ")[0];
            return new(title, u.AbsoluteUri, note.Length > 160 ? note[..160] + "…" : note, "Wikipedia");
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException or NullReferenceException) { return null; }
    }

    /// <summary>Ergebnis der Prüfung. Reached: jede Anfrage bekam eine Antwort (sonst ist die Prüfung unvollständig und nichts darf überschrieben werden).</summary>
    public sealed record Checked(List<Ref> Refs, bool Reached);

    /// <summary>Jeden Titel belegen (erst deutsche, dann englische Wikipedia). Was sich nicht belegen lässt, fällt weg.</summary>
    public static async Task<Checked> Verify(IWebFetch http, IEnumerable<string> titles, CancellationToken ct = default)
    {
        var found = new List<Ref>(); var failed = false;
        foreach (var title in titles.Take(6))
            foreach (var lang in new[] { "de", "en" })
            {
                try
                {
                    var r = await http.GetAsync(SummaryUrl(lang, title), ct);
                    if (!r.Ok && r.Status != 404) failed = true;
                    if (r.Ok && FromSummary(title, r.Text, lang) is { } hit)
                    {
                        if (!found.Any(f => Norm(f.Title) == Norm(hit.Title))) found.Add(hit);
                        break;
                    }
                }
                catch (Exception e) when (e is HttpRequestException or IOException or TaskCanceledException) { failed = true; }
            }
        return new(found, !failed); // schon ein Ausfall: nicht vollständig geprüft, nichts überschreiben
    }

    /// <summary>Belegte Referenzen ins Dokument (ersetzt die alten).</summary>
    public static void Store(JsonObject d, IEnumerable<Ref> refs, DateTime when)
    {
        d["references"] = new JsonArray([.. refs.Select(r => (JsonNode)new JsonObject { ["title"] = r.Title, ["url"] = r.Url, ["note"] = r.Note, ["source"] = r.Source })]);
        d["referencesChecked"] = when.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    /// <summary>Gespeicherte Referenzen; Einträge ohne https-Beleg werden nie gezeigt.</summary>
    public static List<Ref> Read(JsonObject d) =>
        [.. (d["references"] as JsonArray ?? []).OfType<JsonObject>()
            .Select(o => new Ref((string?)o["title"] ?? "", (string?)o["url"] ?? "", (string?)o["note"] ?? "", (string?)o["source"] ?? ""))
            .Where(r => r.Title.Length > 0 && IsWiki(r.Url))];

    /// <summary>Nur Wikipedia-Seiten über https gelten als Beleg (auch beim Laden fremder Spielstände).</summary>
    public static bool IsWiki(string url) => Uri.TryCreate(url, UriKind.Absolute, out var u) && u.Scheme == "https" && u.Host.EndsWith(".wikipedia.org", StringComparison.OrdinalIgnoreCase);
}
