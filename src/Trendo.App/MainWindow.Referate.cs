using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Trendo.Core;
using Trendo.Core.Kreativ;
using Trendo.Core.Office;
using Trendo.Core.Programs;
using Trendo.Core.Wissen;
using Path = System.IO.Path;

namespace Trendo.App;

/// <summary>T-0780 (Etappe 0.9.113 „Wissen &amp; Lernen“, Freigabe 26.11.2026): Studio „Referate &amp; Hausarbeiten“ (STUDIO-REFERAT-01).
/// Ein Arbeitsplatz als Kette: Thema (Deckblatt) → Gliederung → Text je Abschnitt → Literatur → Folien. Links die Gliederung (wie der Ordnerbaum in Schreibprogrammen),
/// in der Mitte der Schritt, rechts die Formvorgaben. Nutzt Vorhandenes: Quellen und Zitierstile aus „Recherche mit Quellen“ (<see cref="CiteKit"/>, Übernahme aus einem
/// offenen oder gespeicherten Quellen-Dokument, BibTeX/RIS), das Textdokument von Trendo Text (DOCX/ODT/PDF) und Trendo Präsentation (PPTX; ODP neu).
/// Die KI schreibt über die eine Kette ins offene Dokument (Gliederung oder Abschnitt, Vorschau → „Ja, behalten“ → ein Rückgängig-Schritt) und darf nur Quellen der Liste zitieren.</summary>
public partial class MainWindow
{
    public const string RefKey = "referate-programm";
    public const string RefStudio = "STUDIO-REFERAT-01";

    /// <summary>Gewählter Schritt der Kette (0 Thema … 4 Folien) und Abschnitt – je Dokument, nur in dieser Sitzung.</summary>
    readonly Dictionary<string, int> _refStep = [];
    readonly Dictionary<string, string> _refSel = [];

    public static readonly string[] RefSteps = ["Thema", "Gliederung", "Text", "Literatur", "Folien"];

    private void InitWissenStudios() { InitReferate(); InitZeitleisten(); }

    static JsonObject RefDoc(string title = "") => new()
    {
        ["title"] = title, ["subtitle"] = "", ["kind"] = "hausarbeit", ["author"] = "", ["matrikel"] = "", ["school"] = "", ["course"] = "", ["teacher"] = "", ["place"] = "", ["date"] = "",
        ["style"] = "apa", ["theme"] = "klar", ["sections"] = new JsonArray(), ["sources"] = new JsonArray(),
    };

    static JsonObject RefSample()
    {
        var d = RefDoc("Wasser sparen im eigenen Garten");
        d["subtitle"] = "Möglichkeiten und Grenzen für Hobbygärten"; d["author"] = "Erika Muster"; d["school"] = "Musterschule Musterstadt"; d["course"] = "Biologie, Klasse 10b";
        d["teacher"] = "Max Beispiel"; d["kind"] = "referat";
        d["sources"] = new JsonArray(
            ReferatKit.SourceJson(new CiteKit.Source { Id = "q-buch", Kind = "buch", Authors = "Muster, Erika; Beispiel, Max", Title = "Gärten für Einsteiger", Year = "2024", Publisher = "Grünverlag", Place = "Musterstadt", Edition = "2" }),
            ReferatKit.SourceJson(new CiteKit.Source { Id = "q-web", Kind = "webseite", Authors = "{Gartenverband Musterstadt}", Title = "Wasser sparen im Garten", Year = "2025", Journal = "example.org", Url = "https://example.org/wasser-sparen", Accessed = "2026-11-03" }));
        d["sections"] = ReferatKit.SectionsJson(
        [
            new("s1", 1, "Einleitung", "Viele Gärten brauchen im Sommer viel Wasser. Dieses Referat zeigt, wie man im eigenen Garten Wasser sparen kann [@q-web]."),
            new("s2", 1, "Wege zum Wassersparen", ""),
            new("s3", 2, "Regenwasser sammeln", "Eine Regentonne fängt das Wasser vom Dach auf. Es ist weich und eignet sich gut zum Gießen [@q-buch, S. 12]."),
            new("s4", 2, "Mulchen", "Eine Schicht aus Rasenschnitt oder Laub hält den Boden feucht."),
            new("s5", 1, "Fazit", "Mit wenigen Mitteln lässt sich viel Wasser sparen."),
        ]);
        return d;
    }

    public static List<ReferatKit.Section> RefSections(ProgramDocument d) => ReferatKit.Sections(d.Data);
    public static List<CiteKit.Source> RefSources(ProgramDocument d) => ReferatKit.Sources(d.Data);
    static void RefSaveSections(ProgramDocument d, IEnumerable<ReferatKit.Section> list, string label) => d.Edit(label, x => x["sections"] = ReferatKit.SectionsJson(list));
    static void RefSaveSources(ProgramDocument d, IEnumerable<CiteKit.Source> list, string label) => d.Edit(label, x => x["sources"] = new JsonArray([.. list.Select(s => (JsonNode)ReferatKit.SourceJson(s))]));

    public int RefStepOf(ProgramDocument d) => _refStep.GetValueOrDefault(d.Id, RefSections(d).Count == 0 && d.Text("title").Trim().Length == 0 ? 0 : 2);
    public void RefSetStep(ProgramDocument d, int step) { _refStep[d.Id] = Math.Clamp(step, 0, RefSteps.Length - 1); }
    public string? RefSelected(ProgramDocument d)
    {
        var list = RefSections(d);
        if (_refSel.TryGetValue(d.Id, out var id) && list.Any(s => s.Id == id)) return id;
        return list.FirstOrDefault()?.Id;
    }
    public void RefSelect(ProgramDocument d, string id) => _refSel[d.Id] = id;

    // ---------------- Gliederung ----------------

    public ReferatKit.Section RefAddSection(ProgramDocument d, int level = 1, string title = "")
    {
        var list = RefSections(d);
        if (list.Count >= ReferatKit.MaxSections) throw new InvalidOperationException($"Höchstens {ReferatKit.MaxSections} Abschnitte.");
        var sel = RefSelected(d); var at = sel is null ? list.Count : list.FindIndex(s => s.Id == sel) + 1;
        // hinter dem gewählten Abschnitt samt seinen Unterabschnitten einfügen
        if (sel is not null && at > 0) { var lvl = list[at - 1].Level; while (at < list.Count && list[at].Level > Math.Min(lvl, level)) at++; }
        var s = new ReferatKit.Section(ReferatKit.NewId("s"), Math.Clamp(level, 1, 3), title.Length > 0 ? title : level == 1 ? "Neues Kapitel" : "Neuer Unterabschnitt", "");
        list.Insert(Math.Clamp(at, 0, list.Count), s);
        RefSaveSections(d, list, level == 1 ? "Neuer Abschnitt" : "Neuer Unterabschnitt");
        _refSel[d.Id] = s.Id; _refStep[d.Id] = 2;
        _progMsg[RefKey] = "Abschnitt angelegt – Überschrift und Text in der Mitte eintragen.";
        return s;
    }

    public void RefSetSection(ProgramDocument d, string id, string? title = null, string? text = null, int? level = null, string? points = null)
    {
        var list = RefSections(d); var i = list.FindIndex(s => s.Id == id);
        if (i < 0) throw new InvalidOperationException("Diesen Abschnitt gibt es nicht mehr.");
        var s = list[i];
        list[i] = s with
        {
            Title = title is null ? s.Title : KreativSafe.Cut(title.Trim(), 200),
            Text = text is null ? s.Text : KreativSafe.Cut(text, ReferatKit.MaxSectionChars),
            Level = level is { } l ? Math.Clamp(l, 1, 3) : s.Level,
            Points = points is null ? s.Points : KreativSafe.Cut(points, 4000),
        };
        if (list[i] == s) return;
        RefSaveSections(d, list, title is not null ? "Überschrift" : text is not null ? "Text" : level is not null ? "Ebene" : "Folienpunkte");
    }

    public void RefRemoveSection(ProgramDocument d, string id)
    {
        var list = RefSections(d); var i = list.FindIndex(s => s.Id == id); if (i < 0) return;
        list.RemoveAt(i);
        RefSaveSections(d, list, "Abschnitt gelöscht");
        if (list.Count > 0) _refSel[d.Id] = list[Math.Min(i, list.Count - 1)].Id; else _refSel.Remove(d.Id);
    }

    /// <summary>Abschnitt an eine andere Stelle ziehen (Gliederung links).</summary>
    public void RefMove(ProgramDocument d, int from, int to)
    {
        var list = RefSections(d);
        if (from < 0 || from >= list.Count || to < 0 || to >= list.Count || from == to) return;
        var s = list[from]; list.RemoveAt(from); list.Insert(to, s);
        RefSaveSections(d, list, "Abschnitt verschoben");
        _refSel[d.Id] = s.Id;
    }

    void RefShift(ProgramDocument d, int delta)
    {
        if (RefSelected(d) is not { } id) throw new InvalidOperationException("Wähle links zuerst einen Abschnitt.");
        var list = RefSections(d); var i = list.FindIndex(s => s.Id == id);
        RefMove(d, i, Math.Clamp(i + delta, 0, list.Count - 1));
    }

    void RefIndent(ProgramDocument d, int delta)
    {
        if (RefSelected(d) is not { } id) throw new InvalidOperationException("Wähle links zuerst einen Abschnitt.");
        var s = RefSections(d).First(x => x.Id == id);
        RefSetSection(d, id, level: s.Level + delta);
    }

    /// <summary>Gliederung aus Zeilen übernehmen („1 Einleitung“, „1.1 …“). Vorhandene Abschnitte mit gleicher Überschrift behalten ihren Text.</summary>
    public int RefApplyOutline(ProgramDocument d, string outline, string label = "Gliederung übernommen")
    {
        var parsed = ReferatKit.ParseOutline(outline);
        if (parsed.Count == 0) throw new ArgumentException("Trag die Gliederung Zeile für Zeile ein, z. B. „1 Einleitung“, „1.1 Fragestellung“, „2 Hauptteil“.");
        var old = RefSections(d);
        var used = new HashSet<string>();
        var list = parsed.Select(p =>
        {
            var keep = old.FirstOrDefault(o => !used.Contains(o.Id) && string.Equals(o.Title.Trim(), p.Title.Trim(), StringComparison.OrdinalIgnoreCase));
            if (keep is not null) { used.Add(keep.Id); return keep with { Level = p.Level }; }
            return new ReferatKit.Section(ReferatKit.NewId("s"), p.Level, p.Title, "");
        }).ToList();
        // Abschnitte mit Text, die in der neuen Gliederung fehlen, gehen nicht verloren: sie stehen hinten
        var lost = old.Where(o => !used.Contains(o.Id) && o.Text.Trim().Length > 0).ToList();
        list.AddRange(lost);
        RefSaveSections(d, list, label);
        _refSel[d.Id] = list[0].Id;
        _progMsg[RefKey] = $"Gliederung mit {parsed.Count} Punkten übernommen" + (lost.Count > 0 ? $" – {lost.Count} Abschnitt(e) mit Text stehen weiter hinten." : ".");
        return parsed.Count;
    }

    // ---------------- Quellen ----------------

    public CiteKit.Source RefAddSource(ProgramDocument d, string kind)
    {
        if (!CiteKit.Kinds.Any(k => k.Id == kind)) throw new ArgumentException("Art: Buch, Artikel oder Webseite.");
        var s = new CiteKit.Source { Id = ReferatKit.NewId("q"), Kind = kind, Accessed = kind == "webseite" ? DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : "" };
        RefSaveSources(d, RefSources(d).Append(s), "Neue Quelle");
        _refSrcSel = s.Id; _refStep[d.Id] = 3;
        _progMsg[RefKey] = "Neue Quelle – Angaben unten eintragen.";
        return s;
    }

    string? _refSrcSel;

    public void RefSetSource(ProgramDocument d, string id, string key, string value)
    {
        var list = RefSources(d); var i = list.FindIndex(s => s.Id == id);
        if (i < 0) throw new InvalidOperationException("Diese Quelle gibt es nicht mehr.");
        var o = ReferatKit.SourceJson(list[i]); if (!o.ContainsKey(key)) throw new ArgumentException("Unbekanntes Feld.");
        var v = KreativSafe.Cut((value ?? "").Trim(), 2000);
        if (key == "accessed" && v.Length > 0 && CiteKit.Date(v) is { } dt) v = dt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        if (Js(o[key]) == v) return;
        o[key] = v; list[i] = ReferatKit.SourceOf(o);
        RefSaveSources(d, list, "Quelle geändert");
    }

    public void RefRemoveSource(ProgramDocument d, string id)
    {
        var used = RefSections(d).Any(s => ReferatKit.Citations(s.Text).Any(c => c.Id == id));
        RefSaveSources(d, RefSources(d).Where(s => s.Id != id), "Quelle entfernt");
        _progMsg[RefKey] = used ? "⚠ Quelle entfernt – sie wird im Text noch zitiert. „Prüfen“ zeigt die Stellen." : "Quelle entfernt.";
    }

    /// <summary>Quellen dazunehmen; gleiche DOI, Adresse oder Titel+Jahr werden nicht doppelt übernommen. Kennungen bleiben, wenn frei (Verweise im Text passen dann weiter).</summary>
    int RefMergeSources(ProgramDocument d, IEnumerable<CiteKit.Source> incoming, string label)
    {
        var all = RefSources(d); var added = 0;
        static string Key(CiteKit.Source s) => CiteKit.ValidDoi(s.Doi) ? "doi:" + s.Doi.Trim().ToLowerInvariant() : CiteKit.NormUrl(s.Url) is { Length: > 0 } u ? "url:" + u : "t:" + s.Title.Trim().ToLowerInvariant() + "|" + s.Year.Trim();
        var known = all.Select(Key).ToHashSet();
        foreach (var s in incoming)
        {
            if (s.Title.Trim().Length == 0 || !known.Add(Key(s))) continue;
            var id = s.Id.Length > 0 && all.All(x => x.Id != s.Id) ? s.Id : ReferatKit.NewId("q");
            all.Add(s with { Id = id }); added++;
        }
        RefSaveSources(d, all, label);
        return added;
    }

    /// <summary>Quellen aus „Recherche mit Quellen“ übernehmen: aus dem dort offenen Dokument, sonst aus einer gespeicherten Quellen-Datei (.tdoc).</summary>
    public int RefFromCite(ProgramDocument d, string? file = null)
    {
        List<CiteKit.Source> got;
        if (file is not null)
        {
            if (!KreativSafe.LocalFile(file, 20_000_000)) throw new InvalidOperationException("Wähle eine Quellen-Datei auf diesem Rechner.");
            var src = ProgramDocument.Load(file, "STUDIO-CITE-01");
            got = ReferatKit.Sources(src.Data);
        }
        else if (ProgramActive(CiteKey) is { } cite) got = CiteSources(cite);
        else throw new InvalidOperationException("In „Recherche mit Quellen“ ist gerade keine Quellenliste offen – öffne sie dort oder wähle die gespeicherte Datei.");
        var n = RefMergeSources(d, got, "Aus „Recherche mit Quellen“");
        _progMsg[RefKey] = got.Count == 0 ? "Die Quellenliste ist leer." : $"{n} von {got.Count} Quellen übernommen" + (n < got.Count ? " (die übrigen waren schon da)." : ".");
        _refStep[d.Id] = 3;
        return n;
    }

    /// <summary>BibTeX oder RIS (z. B. aus Zotero oder Citavi) einlesen.</summary>
    public int RefImportSources(ProgramDocument d, string file)
    {
        if (!KreativSafe.LocalFile(file, CiteKit.MaxBibChars)) throw new InvalidOperationException("Wähle eine BibTeX- oder RIS-Datei auf diesem Rechner (höchstens 5 MB).");
        var text = File.ReadAllText(file);
        var ris = Path.GetExtension(file).Equals(".ris", StringComparison.OrdinalIgnoreCase) || Regex.IsMatch(text, @"(?m)^TY  - ");
        var (got, warn) = ris ? CiteKit.ParseRis(text) : CiteKit.ParseBibTex(text);
        var n = RefMergeSources(d, got, ris ? "RIS eingelesen" : "BibTeX eingelesen");
        _progMsg[RefKey] = $"{n} Quellen eingelesen" + (warn.Count > 0 ? $", {warn.Count} Einträge übersprungen ({warn[0]})." : ".");
        _refStep[d.Id] = 3;
        return n;
    }

    /// <summary>Verweis auf eine Quelle ans Ende des gewählten Abschnitts setzen.</summary>
    public void RefInsertCitation(ProgramDocument d, string sourceId, string page = "")
    {
        if (RefSources(d).All(s => s.Id != sourceId)) throw new InvalidOperationException("Diese Quelle steht nicht in der Liste.");
        if (RefSelected(d) is not { } id) throw new InvalidOperationException("Wähle links zuerst einen Abschnitt.");
        var s = RefSections(d).First(x => x.Id == id);
        var mark = page.Trim().Length > 0 ? $"[@{sourceId}, S. {page.Trim()}]" : $"[@{sourceId}]";
        var text = s.Text.TrimEnd();
        text = text.Length == 0 ? mark : text.EndsWith('.') ? text[..^1] + " " + mark + "." : text + " " + mark;
        RefSetSection(d, id, text: text);
        _progMsg[RefKey] = "Verweis eingefügt – im Export steht dort der Kurzbeleg im gewählten Stil.";
    }

    async Task RefPickImport(ProgramDocument d)
    {
        if (await PickMediaFile("ref-bib", "BibTeX- oder RIS-Datei", ["*.bib", "*.bibtex", "*.ris", "*.txt"]) is { } f) ProgramRun(RefKey, () => RefImportSources(d, f));
    }

    async Task RefPickCite(ProgramDocument d)
    {
        if (ProgramActive(CiteKey) is not null) { ProgramRun(RefKey, () => RefFromCite(d)); return; }
        if (await PickMediaFile("ref-cite", "Quellen-Datei aus „Recherche mit Quellen“", ["*.tdoc"]) is { } f) ProgramRun(RefKey, () => RefFromCite(d, f));
    }

    // ---------------- Ausgabe ----------------

    static string RefWriteText(ProgramDocument d, string file, string ext)
    {
        var doc = ReferatKit.BuildText(d.Data);
        switch (ext)
        {
            case ".docx": DocxFormat.Write(doc, file); break;
            case ".odt": OdtFormat.Write(doc, file); break;
            case ".pdf": TextPdf.Export(doc, file); break;
        }
        return file;
    }

    static string RefWriteSlides(ProgramDocument d, string file, string ext)
    {
        var deck = ReferatKit.BuildSlides(d.Data);
        if (ext == ".odp") SlideFiles.WriteOdp(deck, file); else SlideFiles.WritePptx(deck, file);
        return file;
    }

    /// <summary>Weiter in Trendo Text bzw. Trendo Präsentation: die Ausgabe als Datei im Studio-Ordner, dort geöffnet (Original bleibt hier).</summary>
    public string RefOpenIn(ProgramDocument d, string target)
    {
        var dir = WorkFolder(Path.Combine("Referate", "Ausgaben")); var stem = DevStudio.SafeName(d.Text("title").Trim().Length > 0 ? d.Text("title") : "Referat");
        string Free(string ext) { var f = Path.Combine(dir, stem + ext); for (var n = 2; File.Exists(f); n++) f = Path.Combine(dir, $"{stem} ({n}){ext}"); return f; }
        if (target == "text") { var f = RefWriteText(d, Free(".docx"), ".docx"); TextOpen(f); Show("text"); return f; }
        var p = RefWriteSlides(d, Free(".pptx"), ".pptx"); DeckOpen(p); Show("praesentation"); return p;
    }

    void RefCheck(ProgramDocument d)
    {
        var f = ReferatKit.Check(d.Data);
        _progMsg[RefKey] = f.Count == 0 ? $"✓ Formvorgaben erfüllt – {ReferatKit.Words(d.Data)} Wörter, {RefSources(d).Count} Quellen." : string.Join("\n", f.Take(8).Select(x => (x.Blocks ? "✗ " : "⚠ ") + x.Text)) + (f.Count > 8 ? $"\n… und {f.Count - 8} weitere" : "");
    }

    // ---------------- KI-Fachaktionen ----------------

    /// <summary>Die KI darf nur Quellen der Liste zitieren – gemeinsame Prüfung (<see cref="ReferatKit.KiTextProblem"/>), dieselbe wie für „dokument.setzen“ und die Claude-Brücke.</summary>
    static void RefKiCitesOnlyKnown(string text, IReadOnlyList<CiteKit.Source> sources)
    {
        if (ReferatKit.KiTextProblem(text, sources) is { } why) throw new InvalidOperationException(why);
    }

    ProgramKiAction RefKiOutline => new(new AssistAction("referat.gliederung", "Gliederung erzeugen", ["gliederung", "gliedern", "kapitel", "aufbau", "inhaltsverzeichnis", "struktur"],
        [new AssistParam("gliederung", "Gliederung (eine Zeile je Punkt, z. B. „1 Einleitung“, „1.1 Fragestellung“)", Max: 20_000, Multiline: true)]),
        (d, a) =>
        {
            var text = a.GetValueOrDefault("gliederung") ?? "";
            var parsed = ReferatKit.ParseOutline(text);
            if (parsed.Count < 2) throw new InvalidOperationException("Die KI hat keine brauchbare Gliederung geliefert (mindestens zwei Punkte). Nichts übernommen.");
            RefApplyOutline(d, text, "Gliederung");
        },
        (_, _) => [])
    { OnApplyNote = "Erst bei „Ja, behalten“ kommt es ins Dokument – ein Rückgängig-Schritt nimmt es wieder heraus." };

    ProgramKiAction RefKiSection => new(new AssistAction("referat.abschnitt", "Abschnitt schreiben", ["abschnitt", "schreiben", "schreib", "text", "einleitung", "fazit", "kapitel", "formuliere", "ausformulieren"],
        [new AssistParam("abschnitt", "Abschnitt (Nummer wie „2.1“ oder Überschrift)"), new AssistParam("text", "Fertiger Text des Abschnitts", Max: ReferatKit.MaxSectionChars, Multiline: true)]),
        (d, a) =>
        {
            var which = (a.GetValueOrDefault("abschnitt") ?? "").Trim(); var text = (a.GetValueOrDefault("text") ?? "").Trim();
            if (text.Length < 20) throw new InvalidOperationException("Die KI hat keinen Text für den Abschnitt geliefert. Nichts übernommen.");
            RefKiCitesOnlyKnown(text, RefSources(d));
            var list = RefSections(d);
            var numbered = ReferatKit.Numbered(list);
            // Nummer („2.2“), Überschrift oder – wie qwen3:14b im Echtlauf – die Kennung aus dem Dokument („s4“)
            var hit = numbered.FirstOrDefault(x => x.Number == which.TrimEnd('.') || x.Section.Id == which || string.Equals(x.Section.Title.Trim(), which, StringComparison.OrdinalIgnoreCase)
                || which.StartsWith(x.Number + " ", StringComparison.Ordinal) && which[(x.Number.Length + 1)..].Trim().Equals(x.Section.Title.Trim(), StringComparison.OrdinalIgnoreCase)).Section;
            if (hit is null)
            {
                if (which.Length == 0) hit = RefSelected(d) is { } sel ? list.First(s => s.Id == sel) : throw new InvalidOperationException("Die KI hat nicht gesagt, welcher Abschnitt gemeint ist.");
                else { var title = Regex.Replace(which, @"^\d+(\.\d+)*\.?\s*", ""); hit = new ReferatKit.Section(ReferatKit.NewId("s"), 1, KreativSafe.Cut(title, 200), ""); list.Add(hit); RefSaveSections(d, list, "Abschnitt"); }
            }
            RefSetSection(d, hit.Id, text: text);
            _refSel[d.Id] = hit.Id; _refStep[d.Id] = 2;
        },
        (_, _) => [])
    { OnApplyNote = "Erst bei „Ja, behalten“ kommt es ins Dokument – ein Rückgängig-Schritt nimmt es wieder heraus." };

    private void InitReferate() => RegisterProgram(FachTabs, new ProgramSpec(RefKey, RefStudio, "Referate & Hausarbeiten")
    {
        Sentence = "Vom Thema zur fertigen Arbeit: Deckblatt, Gliederung, Text je Abschnitt, Zitate aus deinen Quellen, Literaturverzeichnis (APA, Harvard, DIN ISO 690) und Folien zum Vortrag.",
        Folder = "Referate",
        FillHeight = true,
        Templates =
        [
            new("leer", "Leere Arbeit", "Mit dem Thema beginnen", () => RefDoc()),
            new("hausarbeit", "Hausarbeit (Gliederung)", "Einleitung, Hauptteil, Fazit – zum Ausfüllen", () =>
            {
                var o = RefDoc(); o["kind"] = "hausarbeit";
                o["sections"] = ReferatKit.SectionsJson([new("s1", 1, "Einleitung", ""), new("s2", 2, "Fragestellung", ""), new("s3", 1, "Hauptteil", ""), new("s4", 1, "Fazit", "")]);
                return o;
            }),
            new("beispiel", "Beispiel: Referat Garten", "Mit Quellen, Zitaten und Folien", RefSample),
        ],
        Ribbon =
        [
            ("Gliederung", [
                new ProgramCmd("ref-add", "Abschnitt", d => RefAddSection(d, 1), Primary: true, Tip: "Neues Kapitel hinter dem gewählten") { NoKi = true },
                new ProgramCmd("ref-sub", "Unterabschnitt", d => RefAddSection(d, Math.Min(3, (RefSections(d).FirstOrDefault(s => s.Id == RefSelected(d))?.Level ?? 1) + 1))) { NoKi = true },
                new ProgramCmd("ref-up", "Nach oben", d => RefShift(d, -1), Level: 1) { NoKi = true },
                new ProgramCmd("ref-down", "Nach unten", d => RefShift(d, 1), Level: 1) { NoKi = true },
                new ProgramCmd("ref-outdent", "Ebene höher", d => RefIndent(d, -1), Level: 1) { NoKi = true },
                new ProgramCmd("ref-indent", "Ebene tiefer", d => RefIndent(d, 1), Level: 1) { NoKi = true },
                new ProgramCmd("ref-del", "Abschnitt löschen", d => { if (RefSelected(d) is { } id) RefRemoveSection(d, id); }, Level: 1) { NoKi = true },
            ]),
            ("Quellen", [
                new ProgramCmd("ref-src-buch", "Buch", d => RefAddSource(d, "buch")) { NoKi = true },
                new ProgramCmd("ref-src-artikel", "Artikel", d => RefAddSource(d, "artikel"), Level: 1) { NoKi = true },
                new ProgramCmd("ref-src-webseite", "Webseite", d => RefAddSource(d, "webseite")) { NoKi = true },
                new ProgramCmd("ref-from-cite", "Aus „Recherche mit Quellen“", d => _ = RefPickCite(d), Tip: "Quellen aus dem Studio „Recherche mit Quellen“ übernehmen (offen oder gespeichert)") { NoKi = true },
                new ProgramCmd("ref-import", "BibTeX/RIS einlesen …", d => _ = RefPickImport(d), Tip: "Aus Zotero, Citavi oder JabRef", Level: 1) { NoKi = true },
            ]),
            ("Stil", CiteKit.Styles.Select(s => new ProgramCmd("ref-style-" + s.Id, s.Label, d => d.Set("style", s.Id, "Stil " + s.Label), Level: 1)).ToList()),
            ("Prüfen", [
                new ProgramCmd("ref-check", "Formvorgaben prüfen", d => RefCheck(d), Tip: "Thema, Gliederung, leere Abschnitte, Verweise ohne Quelle, unvollständige Quellen") { NoKi = true },
                new ProgramCmd("ref-to-text", "In Trendo Text öffnen", d => RefOpenIn(d, "text"), Tip: "Als DOCX ausgeben und in Trendo Text weiterbearbeiten", Level: 1) { NoKi = true },
                new ProgramCmd("ref-to-deck", "In Trendo Präsentation öffnen", d => RefOpenIn(d, "deck"), Tip: "Folien als PPTX ausgeben und gestalten", Level: 1) { NoKi = true },
            ]),
        ],
        Exports =
        [
            new("docx", "Word-Dokument (.docx)", ".docx", (d, f) => RefWriteText(d, f, ".docx")),
            new("pdf", "PDF zum Abgeben (.pdf)", ".pdf", (d, f) => RefWriteText(d, f, ".pdf")),
            new("pptx", "Folien für PowerPoint (.pptx)", ".pptx", (d, f) => RefWriteSlides(d, f, ".pptx")),
            new("odt", "OpenDocument-Text (.odt)", ".odt", (d, f) => RefWriteText(d, f, ".odt")),
            new("odp", "Folien für Impress (.odp)", ".odp", (d, f) => RefWriteSlides(d, f, ".odp")),
            new("md", "Markdown für Notion/Obsidian (.md)", ".md", (d, f) => { File.WriteAllText(f, ReferatKit.Markdown(d.Data), new UTF8Encoding(false)); return f; }),
            new("bib", "Quellen als BibTeX (.bib)", ".bib", (d, f) => { File.WriteAllText(f, CiteKit.BibTex(RefSources(d)), new UTF8Encoding(false)); return f; }),
        ],
        SimpleExports = 3,
        KiActions = [RefKiOutline, RefKiSection],
        KiLocked = ["sources"],
        KiCheck = ReferatKit.KiCheck, // T-0780 (Gegenprüfung H1/H2): jeder KI-Schreibweg in Abschnitte und Quellen
        KiHint = "Eine Gliederung entsteht mit „referat.gliederung“ (eine Zeile je Punkt, nummeriert wie „1 Einleitung“, „1.1 …“); den Text eines Abschnitts schreibst du mit „referat.abschnitt“ "
               + "(abschnitt = Nummer oder Überschrift, text = fertiger Text in ganzen Sätzen, Absätze durch eine Leerzeile). Je Auftrag genau eine dieser Aktionen, danach nichts mehr. Zitiere nur Quellen aus „sources“ und nur als Verweis [@kennung] oder [@kennung, S. 12] "
               + "mit der Kennung („id“) aus der Liste. Erfinde keine Quellen, Autorinnen, Jahre oder Seiten – gibt es keine passende Quelle, schreib ohne Verweis. Quellen legt nur die Person an.",
        FirstStep = new("Thema eintragen", "Worum geht es? Trag das Thema ein – danach kommen Gliederung, Text, Quellen und Folien Schritt für Schritt.",
            d => { _refStep[d.Id] = 0; ProgramFirstFocus(d, "ref-f-title"); }, d => d.Text("title").Trim().Length == 0 && RefSections(d).Count == 0 && !ProgramFirstDone(d)),
        Workspace = RefWorkspace,
        Properties = RefProperties,
        Status = d => { var f = ReferatKit.Check(d.Data); var bl = f.Count(x => x.Blocks); return $"{RefSections(d).Count} Abschnitte · {ReferatKit.Words(d.Data)} Wörter · {RefSources(d).Count} Quellen · {CiteKit.Styles.First(s => s.Id == ReferatKit.Style(d.Data)).Label}" + (bl > 0 ? $" · ✗ {bl} Verweis(e) ohne Quelle" : f.Count > 0 ? $" · ⚠ {f.Count} Hinweise" : " · ✓ bereit"); },
        Help = "Oben die Schritte: Thema (Deckblatt), Gliederung, Text, Literatur, Folien. Links steht die Gliederung – Abschnitte anklicken, mit der Maus an eine andere Stelle ziehen, Rechtsklick für mehr. "
             + "Zitieren: unter „Text“ eine Quelle anklicken, dann steht ein Verweis wie [@q-buch, S. 12] im Text; beim Ausgeben wird daraus der Kurzbeleg im gewählten Stil, und das Literaturverzeichnis entsteht von selbst. "
             + "Quellen übernimmst du aus „Recherche mit Quellen“ oder liest BibTeX/RIS aus Zotero und Citavi ein (auch per Ziehen auf die Arbeitsfläche). Ausgabe als Word, PDF, OpenDocument und Folien für PowerPoint oder Impress. "
             + "KI und Quellen: Das Literaturverzeichnis enthält nur Quellen, die du selbst angelegt hast – eine KI legt keine an. Eindeutige Belegformen ohne Quelle (z. B. mit Komma und Jahr, Seitenangabe, „et al.“, „vgl.“, „ebd.“) "
             + "nimmt Trendo aus KI-Text nicht an. Lose Klammern mit Jahr wie „(Mauerfall 1989)“ markiert Trendo nur als Hinweis – ob dort ein Beleg gemeint ist, erkennt Trendo nicht sicher. Lies solche Stellen selbst nach.",
    }, "referat", "hausarbeit", "facharbeit", "seminararbeit", "gliederung", "deckblatt", "inhaltsverzeichnis", "literaturverzeichnis", "zitieren", "citavi", "scrivener", "vortrag");


    // ---------------- Arbeitsfläche ----------------

    Control RefWorkspace(ProgramDocument d)
    {
        var step = RefStepOf(d);
        var list = RefSections(d); var numbered = ReferatKit.Numbered(list); var sel = RefSelected(d);
        // Kette oben
        var chain = new WrapPanel { Name = "ref-chain", Margin = new Thickness(0, 0, 0, 8) };
        var done = new[] { d.Text("title").Trim().Length > 0, list.Count > 0, list.Count > 0 && list.All(s => s.Text.Trim().Length > 0 || list.SkipWhile(x => x != s).Skip(1).TakeWhile(x => x.Level > s.Level).Any()), RefSources(d).Count > 0, list.Any(s => s.Level == 1) };
        for (var i = 0; i < RefSteps.Length; i++)
        {
            var ii = i;
            var b = new Button { Name = "ref-step-" + i, Content = $"{(done[i] ? "✓" : (i + 1).ToString(CultureInfo.InvariantCulture))}  {RefSteps[i]}", Padding = new Thickness(12, 5), Margin = new Thickness(0, 0, 6, 0),
                Background = i == step ? Brush.Parse("#3AD4A017") : Brush.Parse("#151A22"), BorderBrush = i == step ? Accent : Brush.Parse("#22FFFFFF"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(14) };
            b.Click += (_, _) => { _refStep[d.Id] = ii; Show(RefKey); };
            chain.Children.Add(b);
            if (i < RefSteps.Length - 1) chain.Children.Add(new TextBlock { Text = "→", Foreground = Muted, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) });
        }
        // Gliederung links
        var binder = new StackPanel { Name = "ref-binder", Spacing = 2 };
        binder.Children.Add(H("Gliederung", 13));
        if (list.Count == 0) binder.Children.Add(P("Noch leer – „Abschnitt“ oben oder unter „Gliederung“ Zeilen eintragen.", Muted, 11.5));
        var rows = new StackPanel { Name = "ref-binder-rows", Spacing = 2 };
        for (var i = 0; i < numbered.Count; i++)
        {
            var (num, s) = numbered[i]; var ss = s; var idx = i;
            var warn = ReferatKit.UnknownCitations(s.Text, RefSources(d)).Count > 0;
            var empty = s.Text.Trim().Length == 0 && !numbered.Skip(i + 1).TakeWhile(x => x.Section.Level > s.Level).Any();
            var row = new Button
            {
                Name = "ref-sec-" + i, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left, Padding = new Thickness(8 + (s.Level - 1) * 14, 4, 8, 4),
                Background = s.Id == sel ? Brush.Parse("#33D4A017") : Brushes.Transparent, BorderBrush = s.Id == sel ? Accent : Brushes.Transparent, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6),
                Content = new TextBlock { Text = $"{num}  {(s.Title.Length > 0 ? s.Title : "(ohne Überschrift)")}" + (warn ? "  ✗" : empty ? "  ·" : ""), Foreground = warn ? K4Bad : s.Level == 1 ? Text : Muted, FontSize = s.Level == 1 ? 12.5 : 12, TextTrimming = TextTrimming.CharacterEllipsis },
            };
            ToolTip.SetTip(row, warn ? "Verweis ohne Quelle" : empty ? "noch ohne Text" : $"{ReferatKit.Words(new JsonObject { ["sections"] = ReferatKit.SectionsJson([s]) })} Wörter");
            row.Click += (_, _) => { _refSel[d.Id] = ss.Id; if (_refStep.GetValueOrDefault(d.Id, 2) is 0 or 3) _refStep[d.Id] = 2; Show(RefKey); };
            row.ContextMenu = BueroMenu(RefKey, "ref-sec-menu-" + i, () => _refSel[d.Id] = ss.Id,
                ("ref-m-sub", "Unterabschnitt dahinter", () => RefAddSection(d, Math.Min(3, ss.Level + 1)), ss.Level < 3),
                ("ref-m-up", "Nach oben", () => RefMove(d, idx, idx - 1), idx > 0),
                ("ref-m-down", "Nach unten", () => RefMove(d, idx, idx + 1), idx < numbered.Count - 1),
                ("ref-m-out", "Ebene höher", () => RefSetSection(d, ss.Id, level: ss.Level - 1), ss.Level > 1),
                ("ref-m-in", "Ebene tiefer", () => RefSetSection(d, ss.Id, level: ss.Level + 1), ss.Level < 3),
                ("-", "", () => { }, true),
                ("ref-m-del", "Abschnitt löschen", () => RefRemoveSection(d, ss.Id), true));
            BueroReorder(rows, row, i, RefKey, (from, to) => RefMove(d, from, to));
            rows.Children.Add(row);
        }
        binder.Children.Add(rows);
        var center = step switch
        {
            0 => RefStepTopic(d),
            1 => RefStepOutline(d, numbered),
            3 => RefStepSources(d),
            4 => RefStepSlides(d),
            _ => RefStepText(d, list, sel),
        };
        var left = KrCard(new ScrollViewer { Content = binder, VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto }, "ref-binder-card");
        var mid = KrCard(new ScrollViewer { Content = center, VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto }, "ref-step-card");
        var grid = new Grid { Name = "ref-work", ColumnDefinitions = new ColumnDefinitions("250,12,*"), RowDefinitions = new RowDefinitions("Auto,*"), Margin = new Thickness(4, 0, 10, 4) };
        Grid.SetColumnSpan(chain, 3); grid.Children.Add(chain);
        Grid.SetRow(left, 1); grid.Children.Add(left);
        Grid.SetRow(mid, 1); Grid.SetColumn(mid, 2); grid.Children.Add(mid);
        BueroFileDrop(grid, RefKey, BueroExt(".bib", ".bibtex", ".ris", ".tdoc"), "BibTeX-, RIS- oder Quellen-Dateien (.tdoc)", (doc, files) =>
        {
            var n = 0;
            foreach (var f in files) n += f.EndsWith(".tdoc", StringComparison.OrdinalIgnoreCase) ? RefFromCite(doc, f) : RefImportSources(doc, f);
            _progMsg[RefKey] = $"{n} Quellen aus {files.Count} Datei(en) übernommen.";
        });
        return grid;
    }

    TextBox RefField(ProgramDocument d, string key, string name, string label, bool multi = false) { var t = KrBox(d, key, name, label, multi); return t; }

    Control RefStepTopic(ProgramDocument d)
    {
        var sp = new StackPanel { Name = "ref-topic", Spacing = 6, MaxWidth = 760, HorizontalAlignment = HorizontalAlignment.Left };
        sp.Children.Add(H("Thema und Deckblatt", 16));
        sp.Children.Add(P("Das steht auf dem Deckblatt. Leere Felder fallen weg.", Muted, 12));
        var kinds = new WrapPanel();
        foreach (var (id, label) in ReferatKit.Kinds)
        {
            var k = id; var b = new Button { Name = "ref-kind-" + id, Content = label, Padding = new Thickness(10, 3), Margin = new Thickness(0, 0, 6, 6) };
            if (d.Text("kind") == id) b.Classes.Add("accent");
            b.Click += (_, _) => ProgramRun(RefKey, () => d.Set("kind", k, "Art: " + label));
            kinds.Children.Add(b);
        }
        sp.Children.Add(kinds);
        foreach (var (key, label) in new[] { ("title", "Thema (Titel)"), ("subtitle", "Untertitel"), ("author", "Dein Name"), ("matrikel", "Klasse oder Matrikelnummer"), ("school", "Schule oder Hochschule"), ("course", "Fach oder Kurs"), ("teacher", "Lehrkraft oder Betreuung"), ("place", "Ort"), ("date", "Datum (leer = heute)") })
        {
            sp.Children.Add(P(label, null, 11)); sp.Children.Add(RefField(d, key, "ref-f-" + key, label));
        }
        var next = new Button { Name = "ref-topic-next", Content = "Weiter zur Gliederung →", Padding = new Thickness(12, 6), Margin = new Thickness(0, 8, 0, 0) };
        next.Click += (_, _) => { _refStep[d.Id] = 1; Show(RefKey); };
        sp.Children.Add(next);
        return sp;
    }

    Control RefStepOutline(ProgramDocument d, List<(string Number, ReferatKit.Section Section)> numbered)
    {
        var sp = new StackPanel { Name = "ref-outline", Spacing = 6, MaxWidth = 760, HorizontalAlignment = HorizontalAlignment.Left };
        sp.Children.Add(H("Gliederung", 16));
        sp.Children.Add(P("Eine Zeile je Punkt: „1 Einleitung“, „1.1 Fragestellung“, „2 Hauptteil“ … – oder mit „#“/„##“. Vorhandene Abschnitte mit gleicher Überschrift behalten ihren Text. Die KI oben in der Auftragszeile kann die Gliederung auch vorschlagen.", Muted, 12));
        var box = new TextBox { Name = "ref-outline-text", AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 220, FontFamily = new FontFamily("Cascadia Code, Consolas, monospace"),
            Text = string.Join("\n", numbered.Select(x => $"{x.Number} {x.Section.Title}")) };
        sp.Children.Add(box);
        var apply = new Button { Name = "ref-outline-apply", Content = "Gliederung übernehmen", Padding = new Thickness(12, 6) };
        apply.Click += (_, _) => ProgramRun(RefKey, () => RefApplyOutline(d, box.Text ?? ""));
        sp.Children.Add(apply);
        return sp;
    }

    Control RefStepText(ProgramDocument d, List<ReferatKit.Section> list, string? sel)
    {
        var sp = new StackPanel { Name = "ref-text", Spacing = 6 };
        var s = list.FirstOrDefault(x => x.Id == sel);
        if (s is null) { sp.Children.Add(H("Text", 16)); sp.Children.Add(P("Leg zuerst einen Abschnitt an („Abschnitt“ oben).", Muted, 12)); return sp; }
        var num = ReferatKit.Numbered(list).First(x => x.Section.Id == s.Id).Number;
        sp.Children.Add(H($"Abschnitt {num}", 13));
        var title = new TextBox { Name = "ref-sec-title", Text = s.Title, PlaceholderText = "Überschrift", FontSize = 16, FontWeight = FontWeight.SemiBold };
        void CommitTitle() { if ((title.Text ?? "") != s.Title) ProgramRun(RefKey, () => RefSetSection(d, s.Id, title: title.Text ?? "")); }
        title.LostFocus += (_, _) => CommitTitle(); OnEnter(title, CommitTitle);
        sp.Children.Add(title);
        var shown = s.Text;
        var text = new TextBox { Name = "ref-sec-text", Text = s.Text, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 300, PlaceholderText = "Text des Abschnitts – Absätze durch eine Leerzeile. Zitat: rechts eine Quelle anklicken." };
        var words = new TextBlock { Name = "ref-sec-words", Foreground = Muted, FontSize = 11 };
        void Count() => words.Text = $"{Regex.Matches(Regex.Replace(text.Text ?? "", @"\[@[^\]]*\]", ""), @"[\p{L}\p{N}]+").Count} Wörter in diesem Abschnitt";
        var timer = new Avalonia.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        void CommitText() { timer.Stop(); var now = text.Text ?? ""; if (now != shown) { RefSetSection(d, s.Id, text: now); shown = now; } }
        timer.Tick += (_, _) => CommitText();
        text.TextChanged += (_, _) => { Count(); timer.Stop(); if ((text.Text ?? "") != shown) timer.Start(); };
        text.LostFocus += (_, _) => CommitText(); text.DetachedFromVisualTree += (_, _) => CommitText();
        Count();
        sp.Children.Add(text); sp.Children.Add(words);
        var unknown = ReferatKit.UnknownCitations(s.Text, RefSources(d));
        if (unknown.Count > 0) sp.Children.Add(new TextBlock { Name = "ref-sec-unknown", Text = "✗ Verweis ohne Quelle: " + string.Join(", ", unknown.Select(u => "[@" + u + "]")) + " – Quelle anlegen oder Verweis löschen.", Foreground = K4Bad, FontSize = 11.5, TextWrapping = TextWrapping.Wrap });
        // Zitieren
        var sources = RefSources(d); var style = ReferatKit.Style(d.Data);
        sp.Children.Add(H("Zitieren", 13));
        if (sources.Count == 0) sp.Children.Add(P("Noch keine Quellen – unter „Literatur“ anlegen oder aus „Recherche mit Quellen“ übernehmen.", Muted, 11.5));
        var page = new TextBox { Name = "ref-cite-page", PlaceholderText = "Seite (optional)", Width = 140, HorizontalAlignment = HorizontalAlignment.Left };
        if (sources.Count > 0) sp.Children.Add(page);
        var chips = new WrapPanel { Name = "ref-cite-chips" };
        var n = 0;
        foreach (var src in CiteKit.Sorted(sources))
        {
            var id = src.Id;
            var b = new Button { Name = "ref-cite-" + n++, Content = CiteKit.Plain(CiteKit.InText(src, style)), Padding = new Thickness(10, 3), Margin = new Thickness(0, 0, 6, 6) };
            ToolTip.SetTip(b, CiteKit.Plain(CiteKit.Format(src, style)));
            b.Click += (_, _) => { CommitText(); ProgramRun(RefKey, () => RefInsertCitation(d, id, page.Text ?? "")); };
            chips.Children.Add(b);
        }
        sp.Children.Add(chips);
        // Vorschau mit Kurzbelegen
        if (s.Text.Trim().Length > 0)
        {
            sp.Children.Add(H("So steht es in der Arbeit", 13));
            sp.Children.Add(new Border { Padding = new Thickness(12, 10), CornerRadius = new CornerRadius(6), Background = Brush.Parse("#0E1116"),
                Child = new SelectableTextBlock { Name = "ref-sec-preview", Text = string.Join("\n\n", ReferatKit.Paragraphs(ReferatKit.Resolve(s.Text, sources, style))), Foreground = Text, FontSize = 12.5, TextWrapping = TextWrapping.Wrap } });
        }
        return sp;
    }

    static readonly (string Key, string Label, string[] Kinds)[] RefSourceFields =
    [
        ("authors", "Autorin / Autor (Nachname, Vorname; …)", ["buch", "artikel", "webseite"]), ("title", "Titel", ["buch", "artikel", "webseite"]), ("year", "Jahr", ["buch", "artikel", "webseite"]),
        ("publisher", "Verlag", ["buch"]), ("place", "Ort", ["buch"]), ("edition", "Auflage", ["buch"]), ("journal", "Zeitschrift bzw. Name der Webseite", ["artikel", "webseite"]),
        ("volume", "Jahrgang (Band)", ["artikel"]), ("issue", "Heft", ["artikel"]), ("pages", "Seiten", ["artikel"]), ("url", "Adresse (URL)", ["webseite", "artikel"]), ("doi", "DOI", ["artikel", "buch"]), ("accessed", "Abgerufen am", ["webseite"]),
    ];

    Control RefStepSources(ProgramDocument d)
    {
        var sp = new StackPanel { Name = "ref-sources", Spacing = 6, MaxWidth = 860, HorizontalAlignment = HorizontalAlignment.Left };
        var sources = RefSources(d); var style = ReferatKit.Style(d.Data);
        sp.Children.Add(H($"Literatur ({CiteKit.Styles.First(x => x.Id == style).Label})", 16));
        var add = new WrapPanel();
        foreach (var (k, label) in CiteKit.Kinds) { var kk = k; var b = new Button { Name = "ref-srcadd-" + k, Content = "+ " + label, Padding = new Thickness(10, 3), Margin = new Thickness(0, 0, 6, 6) }; b.Click += (_, _) => ProgramRun(RefKey, () => RefAddSource(d, kk)); add.Children.Add(b); }
        var fromCite = new Button { Name = "ref-srcadd-cite", Content = "Aus „Recherche mit Quellen“", Padding = new Thickness(10, 3), Margin = new Thickness(0, 0, 6, 6) };
        fromCite.Click += (_, _) => _ = RefPickCite(d); add.Children.Add(fromCite);
        var styles = new WrapPanel();
        foreach (var (id, label) in CiteKit.Styles) { var ii = id; var b = new Button { Name = "ref-stylebtn-" + id, Content = label, Padding = new Thickness(10, 3), Margin = new Thickness(0, 0, 6, 6) }; if (id == style) b.Classes.Add("accent"); b.Click += (_, _) => ProgramRun(RefKey, () => d.Set("style", ii, "Stil " + label)); styles.Children.Add(b); }
        sp.Children.Add(add); sp.Children.Add(styles);
        if (sources.Count == 0) sp.Children.Add(P("Noch keine Quellen. Oben anlegen, aus „Recherche mit Quellen“ übernehmen oder eine BibTeX-/RIS-Datei auf die Arbeitsfläche ziehen.", Muted, 12));
        var cited = RefSections(d).SelectMany(s => ReferatKit.Citations(s.Text)).GroupBy(c => c.Id).ToDictionary(g => g.Key, g => g.Count());
        if (_refSrcSel is null || sources.All(s => s.Id != _refSrcSel)) _refSrcSel = sources.FirstOrDefault()?.Id;
        var n = 0;
        foreach (var s in CiteKit.Sorted(sources))
        {
            var ss = s; var on = s.Id == _refSrcSel; var miss = CiteKit.Missing(s, style);
            var card = new StackPanel { Spacing = 2 };
            card.Children.Add(new TextBlock { Text = $"{CiteKit.Kinds.First(k => k.Id == s.Kind).Label} · Kennung {s.Id} · " + (cited.TryGetValue(s.Id, out var c) ? $"{c}× zitiert" : "nicht zitiert"), Foreground = Muted, FontSize = 11 });
            card.Children.Add(CiRich(CiteKit.Format(s, style), "ref-src-entry-" + n));
            if (miss.Count > 0) card.Children.Add(new TextBlock { Text = "⚠ Fehlt: " + string.Join(", ", miss), Foreground = K4Warn, FontSize = 11, TextWrapping = TextWrapping.Wrap });
            var pick = new Button { Name = "ref-src-" + n, Content = card, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left, Padding = new Thickness(10, 6),
                Background = on ? Brush.Parse("#2AD4A017") : Brush.Parse("#151A22"), BorderBrush = on ? Accent : Brush.Parse("#22FFFFFF"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8) };
            pick.Click += (_, _) => { _refSrcSel = ss.Id; Show(RefKey); };
            pick.ContextMenu = BueroMenu(RefKey, "ref-src-menu-" + n, () => _refSrcSel = ss.Id,
                ("ref-sm-cite", "Im gewählten Abschnitt zitieren", () => RefInsertCitation(d, ss.Id), RefSelected(d) is not null),
                ("ref-sm-del", "Quelle entfernen", () => RefRemoveSource(d, ss.Id), true));
            n++;
            sp.Children.Add(pick);
        }
        if (sources.FirstOrDefault(s => s.Id == _refSrcSel) is { } sel)
        {
            sp.Children.Add(H("Angaben", 13));
            var kinds = new WrapPanel();
            foreach (var (k, label) in CiteKit.Kinds) { var kk = k; var b = new Button { Name = "ref-srckind-" + k, Content = label, Padding = new Thickness(10, 3), Margin = new Thickness(0, 0, 6, 6) }; if (sel.Kind == k) b.Classes.Add("accent"); b.Click += (_, _) => ProgramRun(RefKey, () => RefSetSource(d, sel.Id, "kind", kk)); kinds.Children.Add(b); }
            sp.Children.Add(kinds);
            var o = ReferatKit.SourceJson(sel);
            foreach (var (key, label, ks) in RefSourceFields.Where(f => f.Kinds.Contains(sel.Kind)))
            {
                var k = key; var shown = Js(o[k]);
                var box = new TextBox { Name = "ref-srcf-" + k, Text = shown, PlaceholderText = label };
                void Commit() { var now = box.Text ?? ""; if (now.Trim() == shown.Trim()) return; try { RefSetSource(d, sel.Id, k, now); shown = now; } catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { _progMsg[RefKey] = ex.Message; } }
                box.LostFocus += (_, _) => Commit(); OnEnter(box, Commit);
                sp.Children.Add(P(label, null, 11)); sp.Children.Add(box);
            }
            var del = new Button { Name = "ref-src-remove", Content = "Quelle entfernen", Padding = new Thickness(10, 4), Margin = new Thickness(0, 6, 0, 0) };
            del.Click += (_, _) => ProgramRun(RefKey, () => RefRemoveSource(d, sel.Id));
            sp.Children.Add(del);
        }
        return sp;
    }

    Control RefStepSlides(ProgramDocument d)
    {
        var sp = new StackPanel { Name = "ref-slides", Spacing = 8 };
        sp.Children.Add(H("Folien zum Vortrag", 16));
        sp.Children.Add(P("Je Kapitel eine Folie. Punkte kommen aus den Unterabschnitten oder den ersten Sätzen – oder du schreibst sie hier selbst. Der Text des Kapitels steht als Sprechernotiz dabei. Ausgabe über „Exportieren“ als PPTX oder ODP.", Muted, 12));
        var themes = new WrapPanel();
        foreach (var th in SlideThemes.All) { var thId = th.Id; var b = new Button { Name = "ref-theme-" + thId, Content = th.Name, Padding = new Thickness(10, 3), Margin = new Thickness(0, 0, 6, 6) }; if ((d.Text("theme") is { Length: > 0 } t ? t : "klar") == thId) b.Classes.Add("accent"); b.Click += (_, _) => ProgramRun(RefKey, () => d.Set("theme", thId, "Folien-Design " + th.Name)); themes.Children.Add(b); }
        sp.Children.Add(themes);
        SlideDoc? deck = null; string? why = null;
        try { deck = ReferatKit.BuildSlides(d.Data); } catch (InvalidOperationException e) { why = e.Message; }
        if (deck is null) { sp.Children.Add(new TextBlock { Name = "ref-slides-why", Text = why, Foreground = K4Warn, TextWrapping = TextWrapping.Wrap }); return sp; }
        var wrap = new WrapPanel { Name = "ref-slide-list" };
        var i = 0;
        foreach (var s in deck.Slides)
        {
            var pc = new StackPanel { Spacing = 3 };
            pc.Children.Add(new TextBlock { Text = s.Title, FontWeight = FontWeight.SemiBold, Foreground = Brush.Parse(deck.Theme.Title), FontSize = 12, TextWrapping = TextWrapping.Wrap });
            if (s.ByRole("body") is { } body) pc.Children.Add(new TextBlock { Text = string.Join("\n", body.Text.Split('\n').Take(6).Select(x => "• " + x.Trim())), Foreground = Brush.Parse(deck.Theme.Text), FontSize = 10, TextWrapping = TextWrapping.Wrap });
            if (s.ByRole("sub") is { } sub) pc.Children.Add(new TextBlock { Text = sub.Text, Foreground = Brush.Parse(deck.Theme.Text), FontSize = 10, TextWrapping = TextWrapping.Wrap });
            wrap.Children.Add(new Border { Name = "ref-slide-" + i++, Width = 240, Height = 135, Margin = new Thickness(0, 0, 10, 10), Padding = new Thickness(10, 8), CornerRadius = new CornerRadius(4),
                Background = Brush.Parse(deck.Theme.Background), BorderBrush = Brush.Parse("#44FFFFFF"), BorderThickness = new Thickness(1), Child = pc, ClipToBounds = true });
        }
        sp.Children.Add(wrap);
        // Folienpunkte des gewählten Kapitels
        if (RefSelected(d) is { } id && RefSections(d).FirstOrDefault(x => x.Id == id) is { Level: 1 } sec)
        {
            sp.Children.Add(H($"Eigene Folienpunkte für „{sec.Title}“", 13));
            var box = new TextBox { Name = "ref-points", Text = sec.Points, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 90, PlaceholderText = "Ein Punkt je Zeile – leer = automatisch" };
            var shown = sec.Points;
            void Commit() { if ((box.Text ?? "") != shown) { ProgramRun(RefKey, () => RefSetSection(d, sec.Id, points: box.Text ?? "")); shown = box.Text ?? ""; } }
            box.LostFocus += (_, _) => Commit();
            sp.Children.Add(box);
        }
        return sp;
    }

    Control RefProperties(ProgramDocument d)
    {
        var sp = new StackPanel { Spacing = 6 };
        sp.Children.Add(H("Formvorgaben", 13));
        var f = ReferatKit.Check(d.Data);
        sp.Children.Add(new TextBlock { Name = "ref-p-words", Text = $"{ReferatKit.Words(d.Data)} Wörter · {RefSections(d).Count} Abschnitte · {RefSources(d).Count} Quellen", Foreground = Text, FontSize = 12 });
        if (f.Count == 0) sp.Children.Add(new TextBlock { Name = "ref-p-ok", Text = "✓ Alles da: Deckblatt, Gliederung, Text und Quellen.", Foreground = K4Good, FontSize = 11.5, TextWrapping = TextWrapping.Wrap });
        foreach (var x in f.Take(10)) sp.Children.Add(new TextBlock { Text = (x.Blocks ? "✗ " : "⚠ ") + x.Text, Foreground = x.Blocks ? K4Bad : K4Warn, FontSize = 11, TextWrapping = TextWrapping.Wrap });
        sp.Children.Add(H("Ausgabe enthält", 13));
        sp.Children.Add(P("Deckblatt · Inhaltsverzeichnis · nummerierte Kapitel · Kurzbelege im Text · Literaturverzeichnis nach Stil · Seitenzahlen „Seite n von m“ · Folien mit Sprechernotizen.", Muted, 11));
        sp.Children.Add(H("Zitierstil", 13));
        sp.Children.Add(P(CiteKit.Styles.First(s => s.Id == ReferatKit.Style(d.Data)).Label + " – „DIN ISO 690 (nah)“ folgt der Norm in den Grundzügen; prüfe die Vorgaben deiner Schule oder Hochschule.", Muted, 11));
        return sp;
    }
}
