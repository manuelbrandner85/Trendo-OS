using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Trendo.Core.Kreativ;
using Trendo.Core.Office;

namespace Trendo.Core.Wissen;

/// <summary>T-0780 (Studio „Referate &amp; Hausarbeiten“): Fachkern der Kette Thema → Gliederung → Text je Abschnitt → Literatur und Zitate → Folien.
/// Baut auf Vorhandenem auf: Quellen und Zitierstile aus <see cref="CiteKit"/> (Studio „Recherche mit Quellen“), das Textdokument aus <see cref="TextDoc"/>
/// (DOCX/ODT/PDF wie in Trendo Text: Deckblatt, Inhaltsverzeichnis als echtes Feld, Seitenzahlen) und Folien aus <see cref="SlideDoc"/> (PPTX wie Trendo Präsentation, ODP neu).
/// Zitate im Text stehen als Verweis <c>[@kennung]</c> oder <c>[@kennung, S. 12]</c> – sie zeigen auf eine echte Quelle der Liste; ein Verweis ohne Quelle wird gemeldet und
/// blockiert die Ausgabe (keine erfundenen Quellen).</summary>
public static partial class ReferatKit
{
    public const int MaxSections = 200, MaxSectionChars = 60_000;

    public static readonly IReadOnlyList<(string Id, string Label)> Kinds =
    [
        ("hausarbeit", "Hausarbeit"), ("referat", "Referat"), ("facharbeit", "Facharbeit"), ("seminararbeit", "Seminararbeit"), ("bachelorarbeit", "Bachelorarbeit"),
    ];

    public sealed record Section(string Id, int Level, string Title, string Text, string Points = "");

    // ---------------- Dokument ----------------

    public static List<Section> Sections(JsonObject o) =>
        (o["sections"] as JsonArray ?? []).OfType<JsonObject>().Take(MaxSections)
            .Select(s => new Section(Str(s["id"]) is { Length: > 0 } id ? id : NewId("s"), Math.Clamp((int)Num(s["level"], 1), 1, 3), Str(s["title"]), Str(s["text"]), Str(s["points"]))).ToList();

    public static JsonObject SectionJson(Section s)
    {
        var o = new JsonObject { ["id"] = s.Id, ["level"] = s.Level, ["title"] = s.Title, ["text"] = s.Text };
        if (s.Points.Length > 0) o["points"] = s.Points;
        return o;
    }

    public static JsonArray SectionsJson(IEnumerable<Section> list) => new([.. list.Select(s => (JsonNode)SectionJson(s))]);

    /// <summary>Quellen im selben Format wie das Studio „Recherche mit Quellen“ (dieselben Feldnamen) – Dokumente lassen sich zwischen beiden austauschen.</summary>
    public static List<CiteKit.Source> Sources(JsonObject o) =>
        (o["sources"] as JsonArray ?? []).OfType<JsonObject>().Select(SourceOf).Where(s => s.Id.Length > 0).ToList();

    public static CiteKit.Source SourceOf(JsonObject o) => new()
    {
        Id = Str(o["id"]), Kind = CiteKit.Kinds.Any(k => k.Id == Str(o["kind"])) ? Str(o["kind"]) : "buch", Authors = Str(o["authors"]), Title = Str(o["title"]), Year = Str(o["year"]),
        Publisher = Str(o["publisher"]), Place = Str(o["place"]), Edition = Str(o["edition"]), Journal = Str(o["journal"]), Volume = Str(o["volume"]), Issue = Str(o["issue"]),
        Pages = Str(o["pages"]), Url = Str(o["url"]), Doi = Str(o["doi"]), Accessed = Str(o["accessed"]), Origin = Str(o["origin"]),
    };

    public static JsonObject SourceJson(CiteKit.Source s)
    {
        var o = new JsonObject
        {
            ["id"] = s.Id, ["kind"] = s.Kind, ["authors"] = s.Authors, ["title"] = s.Title, ["year"] = s.Year, ["publisher"] = s.Publisher, ["place"] = s.Place, ["edition"] = s.Edition,
            ["journal"] = s.Journal, ["volume"] = s.Volume, ["issue"] = s.Issue, ["pages"] = s.Pages, ["url"] = s.Url, ["doi"] = s.Doi, ["accessed"] = s.Accessed,
        };
        if (s.Origin.Length > 0) o["origin"] = s.Origin;
        return o;
    }

    public static string Style(JsonObject o) => CiteKit.Styles.Any(s => s.Id == Str(o["style"])) ? Str(o["style"]) : "apa";

    static string Str(JsonNode? n) => n is JsonValue v && v.GetValueKind() == JsonValueKind.String ? v.GetValue<string>() : "";
    static double Num(JsonNode? n, double f) => Programs.ProgramDocument.ToNumber(n, f);
    public static string NewId(string p) => p + Guid.NewGuid().ToString("N")[..8];

    /// <summary>Gliederungsnummern „1“, „1.1“, „1.1.1“ (eine Ebene tiefer als die vorige wird auf eine Stufe begrenzt).</summary>
    public static List<(string Number, Section Section)> Numbered(IReadOnlyList<Section> list)
    {
        var res = new List<(string, Section)>(); var n = new int[3]; var prev = 0;
        foreach (var s in list)
        {
            var lvl = Math.Min(s.Level, prev + 1); prev = lvl;
            n[lvl - 1]++; for (var i = lvl; i < 3; i++) n[i] = 0;
            res.Add((string.Join('.', n.Take(lvl)), s));
        }
        return res;
    }

    // ---------------- Gliederung lesen ----------------

    [GeneratedRegex(@"^(?<num>\d+(?:\.\d+){0,2})\.?\s+(?<t>.+)$")] private static partial Regex NumberedLine();
    [GeneratedRegex(@"^(?<h>#{1,3})\s*(?<t>.+)$")] private static partial Regex HashLine();

    /// <summary>Gliederung aus Zeilen: „1 Einleitung“, „1.1 Fragestellung“, „## Unterpunkt“ oder eingerückt („  - Punkt“). Leere Zeilen zählen nicht.</summary>
    public static List<(int Level, string Title)> ParseOutline(string text)
    {
        var res = new List<(int, string)>();
        foreach (var raw in (text ?? "").Replace("\r\n", "\n").Split('\n'))
        {
            if (raw.Trim().Length == 0) continue;
            var t = raw.TrimEnd(); var trimmed = t.TrimStart(); var indent = t.Length - trimmed.Length;
            trimmed = trimmed.TrimStart('-', '*', '•', '–').Trim();
            if (trimmed.Length == 0) continue;
            int level; string title;
            if (NumberedLine().Match(trimmed) is { Success: true } m) { level = m.Groups["num"].Value.Split('.').Length; title = m.Groups["t"].Value; }
            else if (HashLine().Match(trimmed) is { Success: true } h) { level = h.Groups["h"].Value.Length; title = h.Groups["t"].Value; }
            else { level = indent >= 4 ? 3 : indent >= 2 ? 2 : 1; title = trimmed; }
            title = title.Trim().TrimEnd(':').Trim();
            if (title.Length == 0) continue;
            res.Add((Math.Clamp(level, 1, 3), title.Length > 200 ? title[..200] : title));
            if (res.Count >= MaxSections) break;
        }
        return res;
    }

    // ---------------- Zitate im Text ----------------

    // T-0780 (Gegenprüfung M3): Kennungen auch mit Umlauten, Punkt oder Doppelpunkt; Leerzeichen nach „[“ („[ @neu]“) zählt ebenfalls als Verweis
    [GeneratedRegex(@"\[\s*@\s*(?<id>[\p{L}\p{N}_\-]+(?:[.:][\p{L}\p{N}_\-]+)*)(?:\s*[,;]?\s*(?:S\.|s\.|p\.|pp\.)?\s*(?<page>[^\]]*))?\]")] private static partial Regex CiteRx();

    public sealed record Citation(string Id, string Page, int Index, int Length);

    public static List<Citation> Citations(string text) =>
        [.. CiteRx().Matches(text ?? "").Select(m => new Citation(m.Groups["id"].Value, m.Groups["page"].Value.Trim(), m.Index, m.Length))];

    /// <summary>Verweise durch den Kurzbeleg des Stils ersetzen („(Muster, 2024, S. 12)“). Unbekannte Verweise bleiben als „[Quelle fehlt: …]“ sichtbar.</summary>
    public static string Resolve(string text, IReadOnlyList<CiteKit.Source> sources, string style) =>
        CiteRx().Replace(text ?? "", m =>
        {
            var s = sources.FirstOrDefault(x => x.Id == m.Groups["id"].Value);
            return s is null ? $"[Quelle fehlt: {m.Groups["id"].Value}]" : CiteKit.Plain(CiteKit.InText(s, style, m.Groups["page"].Value.Trim()));
        });

    /// <summary>Verweise, die auf keine Quelle der Liste zeigen.</summary>
    public static List<string> UnknownCitations(string text, IReadOnlyList<CiteKit.Source> sources) =>
        Citations(text).Select(c => c.Id).Where(id => sources.All(s => s.Id != id)).Distinct().ToList();

    // ---------- T-0780 (2./3. Gegenprüfung): Belege ohne Verweis erkennen – gesperrt eindeutige Formen, sonst Hinweis ----------

    const string Year = @"(?:\d{1,4}\s*v\.\s*Chr\.|\[\s*\d{3,4}[a-z]?\s*\]|\d{3,4}[a-z]?|o\.\s*J\.|n\.\s*d\.)";
    const string Particle = @"(?:(?:van|von|de|der|den|du|la|le|zu|ten|ter|da|di)\s+)";
    const string Name = @"(?:" + Particle + @"?\p{L}[\p{L}'’\-]*\.?)";
    // „und“/„and“ nur als eigenes Wort – sonst zerlegt das Muster Namen wie „Erfunden“ in „Erf“ + „und“ + „en“
    const string Sep = @"(?:\s*[,&]\s*|\s+(?:und|and)\s+)";
    const string Names = Name + @"(?:" + Sep + Name + @")*(?:\s+(?:et\s+al\.|u\.\s*a\.))?";
    /// <summary>Mehrere Personen („A &amp; B“, „A und B“, „A et al.“) – eindeutig ein Beleg, auch ohne Komma.</summary>
    const string NamesMulti = @"(?:" + Name + @"(?:" + Sep + Name + @")+(?:\s+(?:et\s+al\.|u\.\s*a\.))?|" + Name + @"\s+(?:et\s+al\.|u\.\s*a\.))";
    const string Locator = @"(?:\s*[,:]?\s*(?:S\.|s\.|p\.|pp\.|Sp\.)\s*\d+(?:\s*[–\-]\s*\d+)?\s*(?:f{1,2}\.)?|\s*[,:]\s*\d+(?:\s*[–\-]\s*\d+)?)";
    // gesperrt: Komma vor dem Jahr, Seitenangabe, „vgl.“, mehrere Personen oder Buchstabe am Jahr (2021a)
    const string Strict = @"(?:(?:(?:vgl\.|siehe|s\.|zit\.\s*n\.)\s*" + Names + @",?\s+" + Year + Locator + @"?)|(?:" + Names + @"\s*,\s*" + Year + Locator + @"?)|(?:" + Names + @"\s+" + Year + Locator + @")"
        + @"|(?:" + NamesMulti + @"\s+" + Year + Locator + @"?)|(?:" + Names + @"\s+\d{3,4}[a-z]" + Locator + @"?))";

    [GeneratedRegex(@"\(\s*" + Strict + @"(?:\s*;\s*(?:" + Strict + @"|" + Names + @"\s+" + Year + @"))*\s*\)")] private static partial Regex StrictParenRx();
    [GeneratedRegex(@"\(\s*(?:(?:vgl\.\s*)?" + Names + @",?\s+)?(?:o\.\s*J\.|n\.\s*d\.|\[\s*\d{3,4}[a-z]?\s*\])" + Locator + @"?\s*\)")] private static partial Regex NoYearRx();
    [GeneratedRegex(@"\b(?:vgl\.|siehe)\s+" + Names + @"\s*,\s*" + Year + Locator + @"?")] private static partial Regex VglRx();
    /// <summary>Gesperrt: Literaturangabe „Ort: Verlag, Jahr“ (Verlag aus höchstens vier großgeschriebenen Wörtern, danach Satzende) oder „Ort Jahr, S. n“.</summary>
    [GeneratedRegex(@"\b[A-ZÄÖÜ][\p{L}\-]+\s*:\s*[A-ZÄÖÜ][\p{L}\-\.&]*(?:\s+[A-ZÄÖÜ&][\p{L}\-\.]*){0,3},\s*" + Year + @"(?=\s*[.;)]|\s*$)|\b[A-ZÄÖÜ][\p{L}\-]+\s+\d{3,4},\s*S\.\s*\d+")] private static partial Regex FootRx();
    /// <summary>„Name (2021: 5)“, „Name et al. (2021)“, „Name (2021a)“ – erzählend, aber eindeutig.</summary>
    [GeneratedRegex(@"\b(?:" + Names + @"\s*\(\s*\d{3,4}[a-z]?" + Locator + @"\s*\)|" + NamesMulti + @"\s*\(\s*\d{3,4}[a-z]?" + Locator + @"?\s*\)|" + Names + @"\s*\(\s*\d{3,4}[a-z]" + Locator + @"?\s*\))")] private static partial Regex NarrativeStrictRx();
    /// <summary>„(Name 2021)“ – genau die Form, die Trendo bei Harvard/DIN selbst ausgibt; Prüfung gegen die Quellen der Liste.</summary>
    [GeneratedRegex(@"\(\s*(?<n>" + Name + @")\s+\d{3,4}\s*\)")] private static partial Regex LooseParenRx();
    [GeneratedRegex(@"\b" + Names + @"\s*\(\s*\d{3,4}[a-z]?\s*\)")] private static partial Regex NarrativeRx();
    [GeneratedRegex(@"(?m)^\s*(?:\[\d+\]|\d+\)|[¹²³⁴⁵⁶⁷⁸⁹⁰]+)\s*\p{Lu}[^\n]*\b\d{3,4}\b")] private static partial Regex FootLineRx();
    /// <summary>„(ebd.)“, „(ebd., S. 4)“, „(a. a. O., S. 5)“ – nur zulässig, wenn davor ein echter Verweis steht.</summary>
    [GeneratedRegex(@"\(\s*(?:ebd\.|ebenda|a\.\s*a\.\s*O\.)(?:\s*,?\s*(?:S\.|p\.)?\s*\d+(?:\s*[–\-]\s*\d+)?)?\s*\)")] private static partial Regex IbidRx();

    /// <summary>Zeitwörter vor einer Jahreszahl („(Stand 2021)“, „(Juli 2021)“, „Im Sommer (2019)“). T-0780 (4. Gegenprüfung): unterdrücken NUR den Hinweis bei losen Formen –
    /// eine strenge Belegform (Komma, Seite, „et al.“, „vgl.“ …) wird trotzdem gesperrt. Orte und Ereignisse („(Berlin 2019)“, „(Brexit 2016)“) bleiben ein Hinweis.</summary>
    static readonly HashSet<string> NotNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Stand", "Seit", "Ab", "Bis", "Ende", "Anfang", "Mitte", "Jahr", "Jahre", "Frühjahr", "Sommer", "Herbst", "Winter", "Januar", "Februar", "März", "April", "Mai", "Juni", "Juli", "August",
        "September", "Oktober", "November", "Dezember", "Ca", "Etwa", "Rund", "Um", "Im", "In", "Nach", "Vor", "Gegründet", "Geboren", "Gestorben", "Erschienen", "Ausgabe", "Auflage", "Version", "Stichtag", "Quartal",
    };

    public sealed record CiteHit(string Text, bool Blocks);

    /// <summary>Belege im Text, die nicht als Verweis <c>[@kennung]</c> geschrieben sind (T-0780, 4. Gegenprüfung – eine Regel):
    /// strenge Formen (Klammer mit Jahr und Komma, Seitenangabe, „et al.“, „&amp;“/„und“, Buchstabe am Jahr, „vgl.“, „ebd.“/„a. a. O.“ ohne Verweis davor, Fußnote mit Ort oder Verlag)
    /// sind immer gesperrt (<see cref="CiteHit.Blocks"/>) – ohne Ausnahmeliste. Lose Formen „(Wort Jahr)“ und „Wort (Jahr)“ sind nur ein Hinweis; gesperrt nur, wenn das Wort der ganze Nachname
    /// einer vorhandenen Quelle ist, das Jahr aber zu keiner Quelle dieses Namens passt. Die Liste der Zeitwörter unterdrückt nur den Hinweis. Ehrlich: eine Erkennung über Muster –
    /// lose Klammern mit Jahr werden markiert, nicht sicher als Beleg erkannt.</summary>
    public static List<CiteHit> CitationHits(string text, IReadOnlyList<CiteKit.Source>? sources = null)
    {
        var raw = text ?? "";
        var t = CiteRx().Replace(raw, m => new string(' ', m.Length)); // gleiche Länge – Stellen bleiben vergleichbar
        var hits = new List<CiteHit>();
        bool Known(string v) => hits.Any(h => h.Text.Contains(v, StringComparison.Ordinal) || v.Contains(h.Text, StringComparison.Ordinal));
        // streng: immer gesperrt
        foreach (Match m in StrictParenRx().Matches(t)) hits.Add(new(m.Value, true));
        foreach (Match m in NoYearRx().Matches(t)) if (!Known(m.Value)) hits.Add(new(m.Value, true));
        foreach (Match m in VglRx().Matches(t)) if (!Known(m.Value)) hits.Add(new(m.Value.Trim(), true));
        foreach (Match m in FootRx().Matches(t)) if (!Known(m.Value)) hits.Add(new(m.Value, true));
        foreach (Match m in NarrativeStrictRx().Matches(t)) if (!Known(m.Value.Trim())) hits.Add(new(m.Value.Trim(), true));
        foreach (Match m in IbidRx().Matches(t)) if (!CiteRx().IsMatch(raw[..m.Index])) hits.Add(new(m.Value, true)); // „ebd.“ ohne echten Verweis davor
        // lose: Hinweis – außer Nachname einer Quelle mit falschem Jahr
        foreach (Match m in LooseParenRx().Matches(t)) Loose(m.Value, m.Groups["n"].Value, Regex.Match(m.Value, @"\d{3,4}\s*\)$").Value.TrimEnd(')', ' '));
        foreach (Match m in NarrativeRx().Matches(t))
        {
            var v = m.Value.Trim(); var cut = v.IndexOf('(');
            Loose(v, v[..cut], Regex.Match(v[cut..], @"\d{3,4}").Value);
        }
        foreach (Match m in FootLineRx().Matches(t)) if (!Known(m.Value.Trim())) hits.Add(new(m.Value.Trim(), false));
        return hits;

        void Loose(string value, string name, string year)
        {
            var n = Bare(name);
            if (Known(value) || n.Length == 0 || NotNames.Contains(n)) return;
            var same = sources?.Where(s => Surnames(s).Contains(n, StringComparer.OrdinalIgnoreCase)).ToList() ?? [];
            hits.Add(new(value, same.Count > 0 && same.All(s => Regex.Match(s.Year ?? "", @"\d{3,4}").Value != year)));
        }
    }

    /// <summary>Name ohne Namenszusatz („von“, „van“ …) und Schlusspunkt – verglichen wird der ganze Nachname.</summary>
    static string Bare(string name) => Regex.Replace(name.Trim().TrimEnd('.').Trim(), @"^(?:(?:van|von|de|der|den|du|la|le|zu|ten|ter|da|di)\s+)+", "", RegexOptions.IgnoreCase);
    /// <summary>Nachnamen einer Quelle („Muster, Erika; Beispiel, Max“ → Muster, Beispiel; eine Einrichtung ohne Komma ganz).</summary>
    static IEnumerable<string> Surnames(CiteKit.Source s) => (s.Authors ?? "").Split(';').Select(a => Bare(a.Split(',')[0])).Where(a => a.Length > 0);

    /// <summary>Eindeutige Belegformen (für KI-Texte gesperrt); mit <paramref name="sources"/> zusätzlich „(Name Jahr)“ ohne passende Quelle.</summary>
    public static List<string> FreeCitations(string text, IReadOnlyList<CiteKit.Source>? sources = null) => [.. CitationHits(text, sources).Where(h => h.Blocks).Select(h => h.Text).Distinct()];

    /// <summary>Gemeinsame Zitatprüfung für Text, den eine KI schreibt (Assistent, Fachaktion, Claude-Brücke, Plan im Chat): nur Verweise auf Quellen der Liste, keine eindeutigen freien Belege.
    /// <paramref name="before"/>: Text davor – Belege und Verweise, die dort schon standen (von der Person), werden nicht beanstandet. null = in Ordnung, sonst ein Satz für die Person.</summary>
    public static string? KiTextProblem(string text, IReadOnlyList<CiteKit.Source> sources, string before = "")
    {
        var oldIds = Citations(before).Select(c => c.Id).ToHashSet();
        if (UnknownCitations(text, sources).Where(id => !oldIds.Contains(id)).ToList() is { Count: > 0 } unknown)
            return $"Die KI hat eine Quelle zitiert, die nicht in deiner Liste steht ([@{unknown[0]}]) – zitiert wird nur aus deinen eigenen Quellen. Nichts übernommen.";
        var oldFree = FreeCitations(before, sources).ToHashSet();
        if (FreeCitations(text, sources).Where(f => !oldFree.Contains(f)).ToList() is { Count: > 0 } free)
            return $"Die KI hat „{free[0]}“ geschrieben – Belege stehen nur als Verweis auf eine Quelle deiner Liste ([@kennung]). Nichts übernommen.";
        return null;
    }

    /// <summary>Prüfung nach jeder Änderung durch eine KI am Dokument: geänderte oder neue Abschnitte (Überschrift und Text) und die Quellenliste.
    /// Quellen legt nur die Person an; null = in Ordnung.</summary>
    public static string? KiCheck(JsonObject before, JsonObject after)
    {
        var src = Sources(after);
        if ((before["sources"]?.ToJsonString() ?? "[]") != (after["sources"]?.ToJsonString() ?? "[]")) return "Quellen legt nur die Person selbst an – die KI ändert die Quellenliste nicht. Nichts übernommen.";
        var old = Sections(before).ToDictionary(s => s.Id, s => s);
        foreach (var s in Sections(after))
        {
            var had = old.TryGetValue(s.Id, out var o) ? o.Title + "\n" + o.Text + "\n" + o.Points : "";
            if (o is not null && o.Title == s.Title && o.Text == s.Text && o.Points == s.Points) continue;
            if (KiTextProblem(s.Title + "\n" + s.Text + "\n" + s.Points, src, had) is { } why) return why;
        }
        return null;
    }

    // ---------------- Prüfen ----------------

    public sealed record Finding(string Text, bool Blocks);

    [GeneratedRegex(@"\d+")] private static partial Regex NumRx();

    /// <summary>Formvorgaben prüfen: Thema, Gliederung, leere Abschnitte, Verweise ohne Quelle (blockiert), freie Kurzbelege, Seitenangaben außerhalb der bekannten Seiten,
    /// unvollständige Quellen, nie zitierte Quellen.</summary>
    public static List<Finding> Check(JsonObject o)
    {
        var f = new List<Finding>();
        var sections = Sections(o); var sources = Sources(o); var style = Style(o);
        if (Str(o["title"]).Trim().Length == 0) f.Add(new("Das Thema (Titel) fehlt noch.", false));
        if (sections.Count == 0) f.Add(new("Es gibt noch keine Gliederung.", false));
        foreach (var (num, s) in Numbered(sections))
        {
            if (s.Title.Trim().Length == 0) f.Add(new($"Abschnitt {num} hat keine Überschrift.", false));
            if (s.Text.Trim().Length == 0 && !sections.SkipWhile(x => x != s).Skip(1).TakeWhile(x => x.Level > s.Level).Any()) f.Add(new($"Abschnitt {num} „{s.Title}“ hat noch keinen Text.", false));
            foreach (var id in UnknownCitations(s.Text, sources)) f.Add(new($"Abschnitt {num}: Verweis [@{id}] zeigt auf keine Quelle der Liste – Quelle anlegen oder Verweis entfernen.", true));
            foreach (var h in CitationHits(s.Text, sources)) f.Add(new(h.Blocks ? $"Abschnitt {num}: „{h.Text}“ ist ein Beleg ohne Verweis – als [@kennung] aus der Liste zitieren." : $"Abschnitt {num}: „{h.Text}“ sieht aus wie ein Beleg – falls ja, als [@kennung] aus der Liste zitieren.", false));
            foreach (var c in Citations(s.Text))
                if (sources.FirstOrDefault(x => x.Id == c.Id) is { } src && PageOutside(c.Page, src.Pages) is { } p)
                    f.Add(new($"Abschnitt {num}: Seite {p} liegt außerhalb der Seiten der Quelle „{src.Title}“ ({src.Pages}).", false));
        }
        var cited = sections.SelectMany(s => Citations(s.Text)).Select(c => c.Id).ToHashSet();
        foreach (var s in sources)
        {
            var miss = CiteKit.Missing(s, style);
            if (miss.Count > 0) f.Add(new($"Quelle „{(s.Title.Length > 0 ? s.Title : s.Id)}“: es fehlt {string.Join(", ", miss)}.", false));
            if (!cited.Contains(s.Id)) f.Add(new($"Quelle „{(s.Title.Length > 0 ? s.Title : s.Id)}“ wird im Text nicht zitiert.", false));
        }
        return f;
    }

    /// <summary>Seitenangabe eines Verweises außerhalb der bekannten Seiten der Quelle („45–67“ beim Artikel)? Gibt die erste unpassende Seite zurück, sonst null.
    /// Ohne bekannte Seiten (Bücher, Webseiten) wird nichts geprüft.</summary>
    public static int? PageOutside(string page, string known)
    {
        var k = NumRx().Matches(known ?? "").Select(m => int.TryParse(m.Value, out var v) ? v : -1).Where(v => v >= 0).ToList();
        if (k.Count == 0) return null;
        int lo = k.Min(), hi = k.Max();
        if (k.Count == 1) lo = 1; // nur eine Zahl: Seitenzahl des Werks
        foreach (Match m in NumRx().Matches(page ?? "")) if (int.TryParse(m.Value, out var p) && (p < lo || p > hi)) return p;
        return null;
    }

    // ---------- T-0780 (Gegenprüfung M4): 2020a/2020b und „*“ im Titel ----------

    /// <summary>T-0780 (2. Gegenprüfung M3): die ganze Personenliste (Nachnamen in Reihenfolge) – „Muster“ und „Muster; Beispiel“ sind verschiedene Urheber.</summary>
    static string AuthorKey(CiteKit.Source s) => string.Join("|", s.Authors.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(p => { var x = p.Trim('{', '}').Trim(); var c = x.IndexOf(','); return (c > 0 ? x[..c] : x.Split(' ').LastOrDefault() ?? "").Trim().ToLowerInvariant(); }));

    /// <summary>Quellen für die Ausgabe: gleiche Personen (ganze Liste) im gleichen Jahr bekommt „2020a“, „2020b“ (nach Titel sortiert), „*“ im Titel wird nicht zur Kursivmarke.</summary>
    public static List<CiteKit.Source> ForOutput(IReadOnlyList<CiteKit.Source> sources)
    {
        var list = sources.Select(s => s with { Title = s.Title.Replace('*', '∗'), Journal = s.Journal.Replace('*', '∗') }).ToList();
        var cmp = StringComparer.Create(De, true);
        foreach (var g in list.Where(s => s.Year.Trim().Length > 0 && AuthorKey(s).Length > 0).GroupBy(s => (AuthorKey(s), s.Year.Trim())).Where(g => g.Count() > 1))
        {
            var i = 0;
            foreach (var s in g.OrderBy(x => x.Title, cmp).ToList())
            {
                var at = list.IndexOf(s);
                list[at] = s with { Year = s.Year.Trim() + (char)('a' + Math.Min(i++, 25)) };
            }
        }
        return list;
    }

    public static int Words(JsonObject o) => Sections(o).Sum(s => Regex.Matches(CiteRx().Replace(s.Text, ""), @"[\p{L}\p{N}]+").Count);

    /// <summary>Vor jeder Ausgabe: blockierende Befunde (Verweis ohne Quelle) als eine Meldung.</summary>
    public static void EnsureExportable(JsonObject o)
    {
        var blocks = Check(o).Where(x => x.Blocks).ToList();
        if (blocks.Count > 0) throw new InvalidOperationException("Vor der Ausgabe prüfen: " + blocks[0].Text + (blocks.Count > 1 ? $" (und {blocks.Count - 1} weitere)" : ""));
        if (Sections(o).Count == 0) throw new InvalidOperationException("Es gibt noch keine Gliederung – erst Abschnitte anlegen.");
    }

    // ---------------- Textdokument (DOCX/ODT/PDF) ----------------

    static readonly CultureInfo De = CultureInfo.GetCultureInfo("de-DE");

    /// <summary>Hausarbeit als Textdokument: Deckblatt, Inhaltsverzeichnis (echtes Feld in Word, Verzeichnis in LibreOffice), nummerierte Kapitel mit Kurzbelegen,
    /// Literaturverzeichnis nach Stil, Seitenzahlen „Seite n von m“ in der Fußzeile.</summary>
    public static TextDoc BuildText(JsonObject o)
    {
        EnsureExportable(o);
        var sections = Sections(o); var sources = ForOutput(Sources(o)); var style = Style(o);
        var kind = Kinds.FirstOrDefault(k => k.Id == Str(o["kind"])).Label ?? "Hausarbeit";
        var title = Str(o["title"]).Trim();
        var doc = new TextDoc { Title = title.Length > 0 ? title : kind, Page = new PageSetup { PageNumbers = true, Top = 2.5, Bottom = 2, Left = 2.5, Right = 3, Design = "standard" } };
        // Deckblatt
        Para C(string text, ParaStyle st = ParaStyle.Text, double? before = null, double? size = null)
        {
            var p = new Para(text, st) { Align = Align.Center, SpaceBeforePt = before };
            if (size is { } sz && text.Length > 0) p.Spans.Add(new Span(0, text.Length, Fmt.None, sz));
            return p;
        }
        if (Str(o["school"]) is { Length: > 0 } school) doc.Blocks.Add(C(school, size: 13));
        if (Str(o["course"]) is { Length: > 0 } course) doc.Blocks.Add(C(course));
        if (Str(o["teacher"]) is { Length: > 0 } teacher) doc.Blocks.Add(C((kind == "Referat" ? "Lehrkraft: " : "Betreuung: ") + teacher));
        doc.Blocks.Add(C(kind, ParaStyle.Subtitle, 120));
        doc.Blocks.Add(C(title.Length > 0 ? title : "Thema", ParaStyle.Title, 12));
        if (Str(o["subtitle"]) is { Length: > 0 } sub) doc.Blocks.Add(C(sub, ParaStyle.Subtitle));
        var who = new List<string>();
        if (Str(o["author"]) is { Length: > 0 } author) who.Add("vorgelegt von " + author);
        if (Str(o["matrikel"]) is { Length: > 0 } mat) who.Add("Matrikelnummer/Klasse: " + mat);
        var date = Str(o["date"]) is { Length: > 0 } dt ? dt : DateTime.Today.ToString("d. MMMM yyyy", De);
        who.Add((Str(o["place"]) is { Length: > 0 } place ? place + ", " : "") + date);
        for (var i = 0; i < who.Count; i++) doc.Blocks.Add(C(who[i], before: i == 0 ? 160 : null));
        doc.Blocks.Add(new PageBreak());
        doc.Blocks.Add(new TocBlock { Title = "Inhaltsverzeichnis" });
        doc.Blocks.Add(new PageBreak());
        foreach (var (num, s) in Numbered(sections))
        {
            var st = s.Level switch { 1 => ParaStyle.Heading1, 2 => ParaStyle.Heading2, _ => ParaStyle.Heading3 };
            doc.Blocks.Add(new Para($"{num} {s.Title.Trim()}", st));
            foreach (var para in Paragraphs(Resolve(s.Text, sources, style)))
                doc.Blocks.Add(para.StartsWith('>') ? new Para(para.TrimStart('>').Trim(), ParaStyle.Quote) : new Para(para) { Align = Align.Justify, LineSpacing = 1.5 });
        }
        if (sources.Count > 0)
        {
            doc.Blocks.Add(new PageBreak());
            doc.Blocks.Add(new Para("Literaturverzeichnis", ParaStyle.Heading1));
            foreach (var md in CiteKit.Bibliography(sources, style)) doc.Blocks.Add(Italic(md));
        }
        return doc;
    }

    /// <summary>Absätze: durch Leerzeilen getrennt, Zeilenumbrüche innerhalb bleiben als Leerzeichen.</summary>
    public static List<string> Paragraphs(string text) =>
        [.. Regex.Split((text ?? "").Replace("\r\n", "\n").Trim(), @"\n\s*\n").Select(p => Regex.Replace(p.Trim(), @"\s*\n\s*", " ")).Where(p => p.Length > 0)];

    /// <summary>Literaturangabe mit *kursiv* als Absatz mit hängendem Einzug und kursivem Titel.</summary>
    static Para Italic(string md)
    {
        var p = new Para { IndentCm = 1, FirstLineCm = -1, SpaceAfterPt = 6 };
        var sb = new StringBuilder(); var parts = md.Split('*');
        for (var i = 0; i < parts.Length; i++)
        {
            if (parts[i].Length == 0) continue;
            if (i % 2 == 1) p.Spans.Add(new Span(sb.Length, parts[i].Length, Fmt.Italic));
            sb.Append(parts[i]);
        }
        p.Text = sb.ToString();
        return p;
    }

    // ---------------- Folien (PPTX/ODP) ----------------

    /// <summary>Folien zum Vortrag: Titelfolie, Gliederung, je Kapitel eine Folie (Folienpunkte des Abschnitts, sonst die Kernsätze), Notizen aus dem Text, Quellenfolie.</summary>
    public static SlideDoc BuildSlides(JsonObject o)
    {
        EnsureExportable(o);
        var sections = Sections(o); var sources = ForOutput(Sources(o)); var style = Style(o);
        var d = new SlideDoc { Theme = SlideThemes.Get(Str(o["theme"]) is { Length: > 0 } th ? th : "klar") };
        var t = d.AddSlide(SlideLayout.Titel);
        t.ByRole("title")!.Text = Str(o["title"]) is { Length: > 0 } title ? title : "Thema";
        var sub = new List<string>();
        if (Str(o["subtitle"]) is { Length: > 0 } st) sub.Add(st);
        if (Str(o["author"]) is { Length: > 0 } a) sub.Add(a);
        sub.Add(Str(o["date"]) is { Length: > 0 } dt ? dt : DateTime.Today.ToString("d. MMMM yyyy", De));
        t.ByRole("sub")!.Text = string.Join(" · ", sub);
        var numbered = Numbered(sections);
        var tops = numbered.Where(x => x.Section.Level == 1).ToList();
        if (tops.Count > 1)
        {
            var g = d.AddSlide(SlideLayout.TitelInhalt);
            g.ByRole("title")!.Text = "Gliederung";
            g.ByRole("body")!.Text = string.Join('\n', tops.Select(x => $"{x.Number} {x.Section.Title}"));
        }
        for (var i = 0; i < numbered.Count; i++)
        {
            var (num, s) = numbered[i];
            if (s.Level > 1) continue;
            var kids = numbered.Skip(i + 1).TakeWhile(x => x.Section.Level > 1).ToList();
            var points = s.Points.Trim().Length > 0 ? s.Points.Replace("\r\n", "\n").Split('\n').Select(p => p.Trim().TrimStart('-', '*', '•').Trim()).Where(p => p.Length > 0).ToList()
                : kids.Count > 0 ? kids.Select(k => (k.Section.Level > 2 ? "\t" : "") + k.Section.Title).ToList()
                : KeySentences(Resolve(s.Text, sources, style), 4);
            var slide = d.AddSlide(points.Count == 0 ? SlideLayout.NurTitel : SlideLayout.TitelInhalt);
            slide.ByRole("title")!.Text = $"{num} {s.Title}";
            if (slide.ByRole("body") is { } body) { body.Text = string.Join('\n', points.Take(8)); if (points.Count > 6) body.FontSize = 18; }
            var notes = string.Join("\n\n", new[] { s }.Concat(kids.Select(k => k.Section)).Select(x => Resolve(x.Text, sources, style).Trim()).Where(x => x.Length > 0));
            slide.Notes = notes.Length > 3000 ? notes[..3000] + " …" : notes;
        }
        if (sources.Count > 0)
        {
            var q = d.AddSlide(SlideLayout.TitelInhalt);
            q.ByRole("title")!.Text = "Quellen";
            var body = q.ByRole("body")!;
            body.Text = string.Join('\n', CiteKit.Bibliography(sources, style).Select(CiteKit.Plain).Take(10));
            body.FontSize = sources.Count > 5 ? 14 : 16;
        }
        return d;
    }

    /// <summary>Die ersten Sätze eines Textes als Folienpunkte (kurz gehalten).</summary>
    public static List<string> KeySentences(string text, int max)
    {
        var res = new List<string>();
        foreach (var para in Paragraphs(text))
        {
            var first = Regex.Split(para.TrimStart('>').Trim(), @"(?<=[.!?])\s+(?=[A-ZÄÖÜ])").FirstOrDefault()?.Trim() ?? "";
            if (first.Length == 0) continue;
            res.Add(first.Length > 120 ? first[..117].TrimEnd() + " …" : first);
            if (res.Count >= max) break;
        }
        return res;
    }

    // ---------------- Markdown (Notion, Obsidian) ----------------

    public static string Markdown(JsonObject o)
    {
        EnsureExportable(o);
        var sources = ForOutput(Sources(o)); var style = Style(o);
        var sb = new StringBuilder($"# {Str(o["title"]).Trim()}\n\n");
        if (Str(o["subtitle"]) is { Length: > 0 } sub) sb.Append($"*{sub}*\n\n");
        foreach (var (num, s) in Numbered(Sections(o)))
        {
            sb.Append(new string('#', s.Level + 1)).Append($" {num} {s.Title.Trim()}\n\n");
            foreach (var p in Paragraphs(Resolve(s.Text, sources, style))) sb.Append(p).Append("\n\n");
        }
        if (sources.Count > 0) { sb.Append("## Literaturverzeichnis\n\n"); foreach (var md in CiteKit.Bibliography(sources, style)) sb.Append("- ").Append(md).Append('\n'); }
        return sb.ToString();
    }
}
