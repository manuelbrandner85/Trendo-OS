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

    [GeneratedRegex(@"\[@(?<id>[A-Za-z0-9_\-]+)(?:\s*[,;]?\s*(?:S\.|s\.|p\.)?\s*(?<page>[^\]]*))?\]")] private static partial Regex CiteRx();

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

    // ---------------- Prüfen ----------------

    public sealed record Finding(string Text, bool Blocks);

    /// <summary>Formvorgaben prüfen: Thema, Gliederung, leere Abschnitte, Verweise ohne Quelle (blockiert), unvollständige Quellen, nie zitierte Quellen, Wortzahl.</summary>
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
        var sections = Sections(o); var sources = Sources(o); var style = Style(o);
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
        var sections = Sections(o); var sources = Sources(o); var style = Style(o);
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
        var sources = Sources(o); var style = Style(o);
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
