using System.Globalization;
using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Trendo.Core;
using Trendo.Core.Kreativ;
using Trendo.Core.Office;
using Trendo.Core.Programs;

namespace Trendo.App;

/// <summary>STUDIO-DECKDESIGN-01 „Präsentationen gestalten“ (T-0673): Gestaltungs-Einstieg wie Gamma, Pitch, Beautiful.ai und PowerPoint Designer (nur Bedienkonzept).
/// Links die Gliederung, in der Mitte die gestaltete Folie mit Folienleiste, rechts Design, Folienart, Regeln und Redezeit. Erzeugt wird immer ein
/// <see cref="SlideDoc"/> des vorhandenen Präsentationskerns – Export PPTX/PDF über <see cref="SlideFiles"/>/<see cref="SlidePdf"/>, Weiterbearbeiten in „Trendo Präsentation“.
/// Anforderungen und Prüfungen: docs/nachbau/praesentation-gestalten/anforderungen.md.</summary>
public partial class MainWindow
{
    public const string DeckDesignKey = "praesentation-gestalten-programm";
    const string DdStudio = "STUDIO-DECKDESIGN-01";
    /// <summary>Tests: ersetzt die lokale KI beim Entwerfen (Anweisung → Gliederung).</summary>
    public static Func<string, Task<string>>? DeckAiOverride { get; set; }
    readonly Dictionary<string, int> _ddSel = [];
    readonly Dictionary<string, (string Sig, DeckResult R)> _ddCache = [];
    bool _ddAiBusy;

    const string DdExampleTalk = "Energie sparen im Haushalt\n  - Kleine Schritte, große Wirkung\nWarum es sich lohnt\n  - Strom und Heizung werden teurer\n  - Wer spart, schont Geldbeutel und Klima\n  - Die meisten Tipps kosten nichts\n"
        + "Was wir erreichen können\n  - 30 % weniger Heizkosten mit richtigem Lüften\n  - 10 % weniger Strom ohne Stand-by\n  - 1 Grad weniger spart rund 6 % Heizenergie\n"
        + "Vorher und nachher\n  - Vorher: Geräte im Stand-by | Nachher: Steckerleiste aus\n  - Vorher: Fenster gekippt | Nachher: Stoßlüften\n"
        + "Unser Plan\n  - Januar: Zähler ablesen\n  - Februar: Stand-by aufspüren\n  - März: Thermostate einstellen\n  - April: Ergebnis vergleichen\n"
        + "Gedanke zum Schluss\n  - „Die billigste Energie ist die, die wir nicht verbrauchen.“ – Grundsatz der Energieberatung\nFragen?";
    const string DdExamplePitch = "Nachbarschafts-Werkstatt\n  - Reparieren statt wegwerfen\nDas Problem\n  - Kaputte Geräte landen im Müll, obwohl sie oft leicht zu reparieren sind\n  - Werkzeug ist teuer und wird selten gebraucht\n"
        + "Unsere Idee\n  - Ein Raum mit Werkzeug, Ehrenamtlichen und festen Öffnungszeiten\nZahlen aus dem Probejahr\n  - 240 Reparaturen\n  - 85 % erfolgreich\n  - 1,2 t weniger Müll\n"
        + "Fahrplan\n  - 2026: Probebetrieb\n  - 2027: zweiter Standort\n  - 2028: Kurse für Schulen\nWas wir brauchen\n  - Einen Raum ab 40 m²\n  - Zehn Ehrenamtliche\n  - Startgeld für Werkzeug";
    const string DdExampleReport = "Projektbericht\n  - Stand und nächste Schritte\nZiele [Abschnitt]\nStand\n  - Erledigt: Planung und Angebote\n  - In Arbeit: Umbau Erdgeschoss\n  - Offen: Abnahme\nKennzahlen\n  - 72 % des Budgets verbraucht\n  - 3 Wochen Vorsprung\n"
        + "Risiken\n  - Lieferzeiten für Fenster | Ersatzlieferant angefragt\n  - Wetter im Winter | Innenarbeiten vorziehen\nNächste Schritte\n  - Abnahme vorbereiten\n  - Übergabe planen\n> Am Ende um Freigabe für den zweiten Bauabschnitt bitten.";

    static JsonObject DdNew(string outline, string theme = "gold", double minutes = 0) => new()
    {
        ["outline"] = outline, ["theme"] = theme, ["minutes"] = minutes > 0 ? minutes.ToString(CultureInfo.InvariantCulture) : "", ["maxPoints"] = "6",
        ["autoNotes"] = true, ["fixContrast"] = false, ["layouts"] = new JsonObject(), ["bg"] = "", ["title"] = "", ["text"] = "", ["accent"] = "", ["font"] = "", ["topic"] = "",
    };

    /// <summary>Platzhalter der Vorlage „Leere Gliederung“ – solange er unverändert ist, zeigt die Hülle den ersten Schritt (T-0702).</summary>
    public const string DdBlankOutline = "Titel der Präsentation\n  - Untertitel\nErste Folie\n  - Erster Punkt\n  - Zweiter Punkt";

    static readonly string[] DdFonts = ["Segoe UI", "Calibri", "Arial", "Georgia", "Verdana", "Cambria", "Trebuchet MS"];

    private void InitDeckDesign() => RegisterProgram(FachTabs, new ProgramSpec(DeckDesignKey, DdStudio, "Präsentationen gestalten")
    {
        Sentence = "Stichpunkte rein, fertig gestaltete Folien raus – Design mit Kontrast-Prüfung, passende Folienart je Inhalt, Sprechernotizen und Zeitplan.",
        Folder = "Präsentationen",
        FillHeight = true,
        GroupLevels = new() { ["KI"] = 0, ["Folie"] = 0, ["Weitergeben"] = 0, ["Gestalten"] = 0 },
        Templates =
        [
            new("leer", "Leere Gliederung", "Eigene Stichpunkte schreiben", () => DdNew(DdBlankOutline)),
            new("vortrag", "Vortrag", "Kennzahlen, Gegenüberstellung, Zeitleiste, Zitat", () => DdNew(DdExampleTalk, "gold", 10)),
            new("pitch", "Idee vorstellen (Pitch)", "Problem, Idee, Zahlen, Fahrplan", () => DdNew(DdExamplePitch, "minimal", 5)),
            new("bericht", "Projektbericht", "Stand, Kennzahlen, Risiken", () => DdNew(DdExampleReport, "papier", 8)),
        ],
        Ribbon =
        [
            ("Gestalten", [
                new ProgramCmd("dd-build", "Gestalten", d => DdRefresh(d), Primary: true, Tip: "Gliederung neu gestalten – Folienart, Schriftgröße, Regeln, Notizen, Zeit."),
                new ProgramCmd("dd-next-theme", "Anderes Design", d => DdNextTheme(d), Tip: "Nächstes Design ausprobieren (wie „Shuffle“)."),
                new ProgramCmd("dd-fix-contrast", "Kontrast reparieren", d => DdFixContrast(d), Tip: "Schriftfarben so wenig wie nötig ändern, bis alles gut lesbar ist."),
            ]),
            ("Folie", [
                new ProgramCmd("dd-prev", "◀", d => DdSelect(d, DdSelected(d) - 1), Tip: "Vorige Folie"),
                new ProgramCmd("dd-next", "▶", d => DdSelect(d, DdSelected(d) + 1), Tip: "Nächste Folie"),
                new ProgramCmd("dd-auto-layout", "Folienart automatisch", d => DdSetLayout(d, DdSelected(d), null), Tip: "Eigene Wahl der Folienart zurücknehmen."),
            ]),
            ("KI", [
                new ProgramCmd("dd-ai", "Mit der KI entwerfen", d => _ = DdAi(d, d.Text("topic")), Tip: "Gliederung zum Thema rechts – mit der lokalen KI, nichts geht ins Internet."),
            ]),
            ("Weitergeben", [
                new ProgramCmd("dd-handover", "In Trendo Präsentation bearbeiten", d => DdHandover(d), Tip: "Die gestaltete Präsentation in „Trendo Präsentation“ öffnen – dort frei bearbeiten und vorführen."),
                new ProgramCmd("dd-import", "Aus Trendo Präsentation holen", d => DdImportDeck(d), Tip: "Die offene Präsentation als Gliederung übernehmen und neu gestalten.", Level: 1),
                new ProgramCmd("dd-open", "Präsentation öffnen …", d => _ = DdPickFile(d), Tip: "Vorhandene PowerPoint- oder Impress-Datei (.pptx, .odp) oder eine Textgliederung (.txt, .md) übernehmen und neu gestalten – das Original bleibt.", Level: 1),
            ]),
        ],
        Exports =
        [
            new("pptx", "PowerPoint (.pptx)", ".pptx", (d, f) => { SlideFiles.Save(DdBuild(d).Doc, f); _core.Audit.Append(Principal.LocalOwner.Name, "praesentation-gestalten.pptx", Path.GetFileName(f)); return f; }),
            new("pdf", "PDF", ".pdf", (d, f) => { SlidePdf.Export(DdBuild(d).Doc, f); return f; }),
            new("handzettel", "Handzettel mit Notizen (PDF)", ".pdf", (d, f) => { SlidePdf.Export(DdBuild(d).Doc, f, withNotes: true); return f; }),
            new("plan", "Folienplan mit Zeiten (.txt)", ".txt", (d, f) => { File.WriteAllText(f, DeckDesign.PlanText(DdBuild(d))); return f; }),
        ],
        SimpleExports = 2,
        // T-0702 (V-02): leere Gliederung (nur der Platzhalter) – der erste Schritt holt das Eingabefeld
        FirstStep = new("Stichpunkte schreiben", "Schreib links je Folie eine Zeile und darunter Punkte mit „- “ – Trendo gestaltet daraus fertige Folien.",
            d => ProgramFirstFocus(d, "dd-outline"), d => !ProgramFirstDone(d) && d.Text("outline") == DdBlankOutline),
        Workspace = DdWorkspace,
        Properties = DdProperties,
        Status = d => { try { var r = DdBuild(d); return $"{r.Doc.Slides.Count} Folien · {r.Minutes.Sum():0.#} min · Design „{r.Doc.Theme.Name}“" + (r.Findings.Count > 0 ? $" · {r.Findings.Count} Hinweise" : " · alles in Ordnung"); } catch (InvalidOperationException) { return "Noch keine Gliederung"; } },
        Help = "Links die Gliederung schreiben: eine Zeile je Folie, darunter Punkte mit „- “, Sprechernotiz mit „> “, Bild mit „Bild: Datei.png“. Trendo wählt die Folienart selbst: Zahlen werden Kennzahlen, "
             + "Jahreszahlen eine Zeitleiste, Text in „…“ ein Zitat, „Vorher | Nachher“ zwei Spalten. Mehr als 6 Punkte werden auf mehrere Folien verteilt. Rechts Design, Farben, Schrift (mit Kontrast-Prüfung), Folienart je Folie und Redezeit. "
             + "Fertig: als PowerPoint oder PDF speichern oder in „Trendo Präsentation“ weiterbearbeiten.",
    }, "präsentation gestalten", "folien gestalten", "foliendesign", "gamma", "beautiful.ai", "pitch", "designer", "vortrag gestalten", "foliensatz", "sprechernotizen");

    // ---------- Modell (öffentlich für Tests und die KI) ----------

    public SlideTheme DdTheme(ProgramDocument d)
    {
        var t = DeckDesign.Theme(d.Text("theme", "gold"));
        string Pick(string key, string fallback) { var v = d.Text(key).Trim(); return DeckDesign.IsHex(v) ? v.ToUpperInvariant() : fallback; }
        t = t with { Background = Pick("bg", t.Background), Title = Pick("title", t.Title), Text = Pick("text", t.Text), Accent = Pick("accent", t.Accent), Font = d.Text("font").Trim() is { Length: > 0 } f ? f : t.Font };
        return d.Data["fixContrast"].AsBool() ? DeckDesign.FixContrast(t) : t;
    }

    public DeckOptions DdOptions(ProgramDocument d)
    {
        var max = int.TryParse(d.Text("maxPoints"), out var m) ? Math.Clamp(m, 2, 12) : 6;
        var min = double.TryParse(d.Text("minutes").Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var mm) && mm > 0 ? Math.Min(mm, 240) : 0;
        return new DeckOptions(max, 90, min, d.Data["autoNotes"].AsBool(true));
    }

    Dictionary<int, DeckLayout> DdOverrides(ProgramDocument d)
    {
        var res = new Dictionary<int, DeckLayout>();
        if (d.Data["layouts"] is JsonObject o)
            foreach (var kv in o) if (int.TryParse(kv.Key, out var i) && DeckDesign.ParseLayout(kv.Value.AsText()) is { } l) res[i] = l;
        return res;
    }

    /// <summary>Gestaltete Präsentation zum Dokument (zwischengespeichert, solange sich nichts ändert).</summary>
    public DeckResult DdBuild(ProgramDocument d)
    {
        var sig = d.Data.ToJsonString();
        if (_ddCache.TryGetValue(d.Id, out var c) && c.Sig == sig) return c.R;
        var r = DeckDesign.Build(d.Text("outline"), DdTheme(d), DdOptions(d), DdOverrides(d), f => KreativSafe.LocalFile(f, 25L * 1024 * 1024) ? File.ReadAllBytes(f) : null);
        _ddCache[d.Id] = (sig, r); return r;
    }

    public int DdSelected(ProgramDocument d) => _ddSel.GetValueOrDefault(d.Id);
    public void DdSelect(ProgramDocument d, int i) { var n = 1; try { n = DdBuild(d).Doc.Slides.Count; } catch (InvalidOperationException) { } _ddSel[d.Id] = Math.Clamp(i, 0, Math.Max(0, n - 1)); }

    public void DdSetOutline(ProgramDocument d, string outline) { d.Set("outline", outline, "Gliederung"); DdRefresh(d); }

    void DdRefresh(ProgramDocument d)
    {
        var r = DdBuild(d); DdSelect(d, DdSelected(d));
        var split = r.Findings.Count(f => f.Kind is "aufgeteilt" or "gekürzt"); var bad = r.Findings.Count(f => f.Kind == "kontrast");
        _progMsg[DeckDesignKey] = $"{r.Doc.Slides.Count} Folien gestaltet ({string.Join(", ", r.Layouts.GroupBy(l => l).Select(g => $"{g.Count()}× {DeckDesign.Name(g.Key)}"))}), {r.Minutes.Sum():0.#} min."
            + (split > 0 ? $" {split} Regel-Hinweis(e) – siehe unten." : "") + (bad > 0 ? " Achtung: Farben schlecht lesbar – „Kontrast reparieren“." : "");
    }

    /// <summary>Folienart einer Folie von Hand wählen (null = wieder automatisch).</summary>
    public void DdSetLayout(ProgramDocument d, int index, DeckLayout? layout)
    {
        d.Edit(layout is null ? "Folienart automatisch" : "Folienart", x =>
        {
            if (x["layouts"] is not JsonObject o) x["layouts"] = o = [];
            if (layout is null) o.Remove(index.ToString(CultureInfo.InvariantCulture)); else o[index.ToString(CultureInfo.InvariantCulture)] = layout.Value.ToString();
        });
        _progMsg[DeckDesignKey] = layout is null ? $"Folie {index + 1}: Folienart wieder automatisch." : $"Folie {index + 1}: {DeckDesign.Name(layout.Value)}.";
    }

    public void DdSetTheme(ProgramDocument d, string id)
    {
        if (!DeckDesign.Themes.Any(t => t.Id == id)) throw new ArgumentException("Dieses Design gibt es nicht: " + id);
        // T-0705: jede Eigenschaft bekommt ihren eigenen Wert – ein gemeinsamer JsonNode für alle fünf warf „hat schon ein Elternteil“, das Design wechselte
        // dann ohne Rückgängig-Schritt und ohne „geändert“ (keine Notsicherung nach einem Absturz)
        d.Edit("Design", x => { x["theme"] = id; foreach (var k in new[] { "bg", "title", "text", "accent", "font" }) x[k] = ""; });
        var t = DdTheme(d); var c = DeckDesign.CheckTheme(t);
        _progMsg[DeckDesignKey] = $"Design „{t.Name}“ ({t.Font})" + (c.Count == 0 ? " – alle Farben gut lesbar." : " – " + c[0]);
    }

    void DdNextTheme(ProgramDocument d)
    {
        var i = DeckDesign.Themes.ToList().FindIndex(t => t.Id == d.Text("theme", "gold"));
        DdSetTheme(d, DeckDesign.Themes[(i + 1) % DeckDesign.Themes.Count].Id);
    }

    public void DdFixContrast(ProgramDocument d)
    {
        var before = DeckDesign.CheckTheme(DdTheme(d)).Count;
        if (before == 0) { _progMsg[DeckDesignKey] = "Alle Farben sind schon gut lesbar (Text mindestens 4,5 : 1, Titel und Akzent 3 : 1)."; return; }
        var f = DeckDesign.FixContrast(DdTheme(d));
        d.Edit("Kontrast reparieren", x => { x["title"] = f.Title; x["text"] = f.Text; x["accent"] = f.Accent; });
        _progMsg[DeckDesignKey] = $"{before} Farbe(n) angepasst – jetzt gut lesbar.";
    }

    /// <summary>Gestaltete Präsentation in „Trendo Präsentation“ öffnen (gleiches Format, dort frei bearbeiten).</summary>
    public void DdHandover(ProgramDocument d)
    {
        var r = DdBuild(d);
        Deck = r.Doc.Clone(); DeckPath = null; DeckDirty = true; ResetDeckState();
        _dkMsg = $"{Deck.Slides.Count} gestaltete Folien aus „Präsentationen gestalten“ – hier frei bearbeiten, vorführen und speichern.";
        _core.Audit.Append(Principal.LocalOwner.Name, "praesentation-gestalten.uebergabe", $"{Deck.Slides.Count} Folien");
        _progMsg[DeckDesignKey] = $"{Deck.Slides.Count} Folien an „Trendo Präsentation“ übergeben.";
        _ui.Post(() => Show("praesentation")); // erst nach dem Neuzeichnen des Studios wechseln
    }

    public void DdImportDeck(ProgramDocument d)
    {
        if (DeckStartVisible && Deck.Slides.Count <= 1 && Deck.Slides[0].Title is "Titel der Präsentation" or "") throw new InvalidOperationException("In „Trendo Präsentation“ ist noch keine Präsentation offen.");
        d.Edit("Aus Trendo Präsentation", x => { x["outline"] = DeckDesign.FromDeck(Deck); x["layouts"] = new JsonObject(); });
        DdRefresh(d);
    }

    static readonly string[] DdOpenExt = [".pptx", ".odp", ".txt", ".md"];

    /// <summary>T-0705: vorhandene Präsentation (PowerPoint, Impress) oder Textgliederung als Gliederung übernehmen – Titel, Punkte und Sprechernotizen.
    /// Die Datei selbst wird nicht verändert. Rückgängig holt die alte Gliederung zurück.</summary>
    public void DdOpenFile(ProgramDocument d, string path)
    {
        if (!File.Exists(path)) throw new ArgumentException("Datei nicht gefunden: " + Path.GetFileName(path));
        var ext = Path.GetExtension(path).ToLowerInvariant();
        if (!DdOpenExt.Contains(ext)) throw new ArgumentException($"„{Path.GetFileName(path)}“ ist keine Präsentation (.pptx, .odp) und keine Gliederung (.txt, .md).");
        string outline; int slides;
        if (ext is ".txt" or ".md") { outline = File.ReadAllText(path); slides = DeckDesign.Parse(outline).Count; }
        else { var deck = SlideFiles.Open(path); outline = DeckDesign.FromDeck(deck); slides = deck.Slides.Count; }
        if (DeckDesign.Parse(outline).Count == 0) throw new InvalidDataException($"In „{Path.GetFileName(path)}“ steht nichts, woraus sich Folien machen lassen.");
        d.Edit("Präsentation öffnen", x => { x["outline"] = outline; x["layouts"] = new JsonObject(); });
        if (d.Title is "Unbenannt" or "" or "Leere Gliederung") d.Title = Path.GetFileNameWithoutExtension(path);
        DdRefresh(d);
        _progMsg[DeckDesignKey] = $"„{Path.GetFileName(path)}“ übernommen ({slides} Folien) – neu gestaltet. " + _progMsg[DeckDesignKey];
        _core.Audit.Append(Principal.LocalOwner.Name, "praesentation-gestalten.oeffnen", Path.GetFileName(path));
    }

    async Task DdPickFile(ProgramDocument d)
    {
        var req = new PickRequest("dd-open", false, false, ["pptx", "odp", "txt", "md"]);
        string[]? got;
        if (PickOverride is { } fake) got = await fake(req);
        else got = (await StorageProvider.OpenFilePickerAsync(new Avalonia.Platform.Storage.FilePickerOpenOptions { Title = "Präsentation öffnen",
            FileTypeFilter = [new Avalonia.Platform.Storage.FilePickerFileType("Präsentation oder Gliederung") { Patterns = ["*.pptx", "*.odp", "*.txt", "*.md"] }] }))
            .Select(f => f.TryGetLocalPath()).OfType<string>().ToArray();
        if (got is [var f, ..]) ProgramRun(DeckDesignKey, () => DdOpenFile(d, f));
    }

    /// <summary>T-0705: Folie <paramref name="from"/> an die Stelle von Folie <paramref name="to"/> ziehen. Verschoben wird der ganze Abschnitt der Gliederung
    /// (aufgeteilte Folien bleiben zusammen); von Hand gewählte Folienarten gelten je Folie und werden dabei zurückgesetzt.</summary>
    public void DdMoveSlide(ProgramDocument d, int from, int to)
    {
        var o = DdOptions(d); var outline = d.Text("outline");
        var a = DeckDesign.SectionOfSlide(outline, o, from); var b = DeckDesign.SectionOfSlide(outline, o, to);
        if (a == b) { _progMsg[DeckDesignKey] = "Diese Folien gehören zum selben Abschnitt – er wurde nur auf mehrere Folien verteilt."; return; }
        var hadLayouts = DdOverrides(d).Count > 0;
        d.Edit("Folie verschieben", x => { x["outline"] = DeckDesign.MoveSection(outline, a, b); x["layouts"] = new JsonObject(); });
        var r = DdBuild(d); var first = Enumerable.Range(0, r.Doc.Slides.Count).First(i => DeckDesign.SectionOfSlide(d.Text("outline"), o, i) == b);
        DdSelect(d, first);
        _progMsg[DeckDesignKey] = $"Folie jetzt an Stelle {first + 1} – die Gliederung links ist mitgewandert." + (hadLayouts ? " Von Hand gewählte Folienarten sind wieder automatisch." : "");
    }

    /// <summary>T-0705: Folie (ihren Abschnitt) aus der Gliederung entfernen – Rückgängig holt sie zurück.</summary>
    public void DdRemoveSlide(ProgramDocument d, int slide)
    {
        var outline = d.Text("outline"); var s = DeckDesign.SectionOfSlide(outline, DdOptions(d), slide);
        var title = DeckDesign.Parse(outline).ElementAtOrDefault(s)?.Title ?? "";
        d.Edit("Folie entfernen", x => { x["outline"] = DeckDesign.RemoveSection(outline, s); x["layouts"] = new JsonObject(); });
        DdSelect(d, slide);
        _progMsg[DeckDesignKey] = $"Folie „{title}“ entfernt – Strg+Z holt sie zurück.";
    }

    /// <summary>Gliederung zu einem Thema mit der lokalen KI (im Test ersetzt). Das Ergebnis ist ein Vorschlag – prüfen und anpassen.</summary>
    public async Task DdAi(ProgramDocument d, string topic)
    {
        if (_ddAiBusy) { _progMsg[DeckDesignKey] = "Die KI entwirft schon – einen Moment."; Show(DeckDesignKey); return; }
        if (string.IsNullOrWhiteSpace(topic)) { _progMsg[DeckDesignKey] = "Rechts unter „Mit der KI“ ein Thema eintragen, z. B. „Energiesparen im Haushalt“."; Show(DeckDesignKey); return; }
        _ddAiBusy = true; _progMsg[DeckDesignKey] = "Die KI entwirft die Gliederung …"; Show(DeckDesignKey);
        try
        {
            var prompt = DeckDesign.Prompt(topic.Trim(), 7, DdOptions(d).Minutes);
            string text;
            if (DeckAiOverride is { } o) text = await o(prompt);
            else if (_opt.Isolated) { _progMsg[DeckDesignKey] = "Im abgeschotteten Testmodus startet Trendo keine KI."; return; }
            else { using var kiLabel = KiActivityHub.Label("Folien entwerfen"); text = (await Intel.AskAsync(new KiRequest(prompt, PreferredModel: KiModelName, UseKnowledge: false))).Text ?? ""; }
            if (string.IsNullOrWhiteSpace(text) || DeckDesign.Parse(text).Count == 0) { _progMsg[DeckDesignKey] = "Die KI hat keine brauchbare Gliederung geliefert. Ist ein Sprachmodell in der KI-Bibliothek geladen?"; return; }
            d.Edit("Mit der KI entworfen", x => { x["outline"] = text.Trim(); x["layouts"] = new JsonObject(); });
            if (d.Title is "Unbenannt" or "") d.Title = topic.Trim();
            DdRefresh(d);
            _progMsg[DeckDesignKey] = "Die KI hat einen Entwurf geschrieben – bitte Zahlen und Aussagen prüfen. " + _progMsg[DeckDesignKey];
        }
        catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or TaskCanceledException or IOException) { _progMsg[DeckDesignKey] = "Die KI ist gerade nicht erreichbar: " + FriendlyError.Say(ex); }
        finally { _ddAiBusy = false; Show(DeckDesignKey); }
    }

    // ---------- Oberfläche ----------

    Control DdWorkspace(ProgramDocument d)
    {
        var outline = KrBox(d, "outline", "dd-outline", "Gliederung: je Folie eine Zeile, Punkte mit „- “, Notiz mit „> “", multi: true);
        outline.MinHeight = 300; outline.AcceptsTab = true; outline.FontFamily = new FontFamily("Cascadia Code, Consolas, monospace"); outline.FontSize = 12.5;
        var go = new Button { Name = "dd-build-now", Content = "Gestalten ▶", Padding = new Thickness(14, 4), HorizontalAlignment = HorizontalAlignment.Right }; go.Classes.Add("accent");
        go.Click += (_, _) => ProgramRun(DeckDesignKey, () => { if ((outline.Text ?? "") != d.Text("outline")) d.Set("outline", outline.Text ?? "", "Gliederung"); DdRefresh(d); });
        var left = new StackPanel { Spacing = 8, Children = { new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Children = { H("Gliederung", 14), Pos(go, 1) } }, outline,
            P("Tipps: Zahlen am Anfang → Kennzahl · „2026: …“ → Zeitleiste · „Zitat“ – Quelle → Zitat · „links | rechts“ → zwei Spalten · [Zitat] hinter dem Titel wählt die Art fest.", Muted, 11) } };

        DeckResult? r = null; string? err = null;
        try { r = DdBuild(d); } catch (InvalidOperationException e) { err = e.Message; }
        var center = new StackPanel { Spacing = 8 };
        if (r is null) center.Children.Add(P(err ?? "", Muted, 13));
        else
        {
            var sel = Math.Clamp(DdSelected(d), 0, r.Doc.Slides.Count - 1);
            var clock = DeckDesign.Clock(r.Minutes);
            center.Children.Add(new Border { Name = "dd-preview-frame", CornerRadius = new CornerRadius(8), ClipToBounds = true, BorderBrush = Edge, BorderThickness = new Thickness(1),
                Child = new SlideView { Name = "dd-preview", Doc = r.Doc, Slide = r.Doc.Slides[sel], Height = 340, MinWidth = 400 } });
            center.Children.Add(new TextBlock { Name = "dd-slide-info", Text = $"Folie {sel + 1} von {r.Doc.Slides.Count} · {DeckDesign.Name(r.Layouts[sel])} · ab {clock[sel]} · {r.Minutes[sel]:0.0} min" + (DdOverrides(d).ContainsKey(sel) ? " · von Hand gewählt" : ""), Foreground = Muted, FontSize = 12 });
            if (r.Doc.Slides[sel].Notes.Length > 0)
                center.Children.Add(new Border { Padding = new Thickness(10, 6), CornerRadius = new CornerRadius(6), Background = Brush.Parse("#22FFFFFF"),
                    Child = new TextBlock { Name = "dd-notes", Text = "Sprechernotiz: " + r.Doc.Slides[sel].Notes, Foreground = Text, FontSize = 12, TextWrapping = TextWrapping.Wrap } });
            // Folienleiste
            var strip = new WrapPanel { Name = "dd-strip" };
            for (var i = 0; i < r.Doc.Slides.Count; i++)
            {
                var ii = i;
                var b = new Button { Name = "dd-thumb-" + i, Padding = new Thickness(3), Margin = new Thickness(0, 0, 6, 6), BorderThickness = new Thickness(i == sel ? 2 : 1), BorderBrush = i == sel ? Accent : Edge,
                    Content = new StackPanel { Spacing = 2, Children = {
                        new SlideView { Doc = r.Doc, Slide = r.Doc.Slides[i], Width = 150, Height = 84, IsHitTestVisible = false },
                        new TextBlock { Text = $"{i + 1} · {DeckDesign.Name(r.Layouts[i])} · {r.Minutes[i]:0.0} min", FontSize = 10.5, Foreground = Muted } } } };
                ToolTip.SetTip(b, r.Doc.Slides[i].Title);
                b.Click += (_, _) => { DdSelect(d, ii); Show(DeckDesignKey); };
                // T-0705: Folie in der Leiste ziehen = Abschnitt der Gliederung verschieben; Rechtsklick für vor/zurück/entfernen
                BueroReorder(strip, b, i, DeckDesignKey, (from, to) => DdMoveSlide(d, from, to));
                var n = r.Doc.Slides.Count;
                b.ContextMenu = BueroMenu(DeckDesignKey, "dd-thumb-menu-" + i, () => DdSelect(d, ii),
                    ("dd-ctx-earlier", "Nach vorn", () => DdMoveSlide(d, ii, ii - 1), ii > 0),
                    ("dd-ctx-later", "Nach hinten", () => DdMoveSlide(d, ii, ii + 1), ii < n - 1),
                    ("dd-ctx-auto", "Folienart automatisch", () => DdSetLayout(d, ii, null), DdOverrides(d).ContainsKey(ii)),
                    ("-", "", () => { }, true),
                    ("dd-ctx-remove", "Folie entfernen", () => DdRemoveSlide(d, ii), n > 1));
                strip.Children.Add(b);
            }
            center.Children.Add(strip);
            if (r.Findings.Count > 0)
            {
                var fl = new StackPanel { Name = "dd-findings", Spacing = 2 };
                fl.Children.Add(H("Hinweise", 13));
                foreach (var f in r.Findings) fl.Children.Add(new TextBlock { Text = (f.Kind == "kontrast" || f.Kind == "zu-lang" ? "⚠ " : "ℹ ") + f.Message, Foreground = f.Kind is "kontrast" or "zu-lang" ? K4Warn : Muted, FontSize = 12, TextWrapping = TextWrapping.Wrap });
                center.Children.Add(fl);
            }
        }
        var scroll = new ScrollViewer { Content = center, VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto };
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("340,14,*"), Margin = new Thickness(4, 0, 10, 4), Children = { KrCard(new ScrollViewer { Content = left, VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto }, "dd-outline-card"), Pos(KrCard(scroll, "dd-preview-card"), 2) } };
        // T-0705: PowerPoint/Impress-Datei oder Textgliederung auf die Arbeitsfläche ziehen
        BueroFileDrop(grid, DeckDesignKey, BueroExt(DdOpenExt), "eine Präsentation (.pptx, .odp) oder eine Gliederung (.txt, .md)", (dd, files) => DdOpenFile(dd, files[0]));
        return grid;
    }

    Control DdProperties(ProgramDocument d)
    {
        var sp = new StackPanel { Name = "dd-props", Spacing = 5 };
        ComboBox Combo(string name, IReadOnlyList<(string Id, string Name)> items, string current, Action<string> set)
        {
            var c = new ComboBox { Name = name, ItemsSource = items.Select(i => i.Name).ToList(), SelectedIndex = Math.Max(0, items.ToList().FindIndex(i => i.Id == current)), MinWidth = 190, FontSize = 12 };
            c.SelectionChanged += (_, _) => { if (c.SelectedIndex >= 0 && items[c.SelectedIndex].Id != current) ProgramRun(DeckDesignKey, () => set(items[c.SelectedIndex].Id)); };
            return c;
        }
        var t = DdTheme(d);
        sp.Children.Add(H("Design", 13));
        sp.Children.Add(Combo("dd-theme", [.. DeckDesign.Themes.Select(x => (x.Id, $"{x.Name} ({x.Font})"))], d.Text("theme", "gold"), v => DdSetTheme(d, v)));
        var sw = new WrapPanel();
        foreach (var (key, label, val) in new[] { ("bg", "Hintergrund", t.Background), ("title", "Titel", t.Title), ("text", "Text", t.Text), ("accent", "Akzent", t.Accent) })
        {
            var box = new TextBox { Name = "dd-color-" + key, Text = val, Width = 84, FontSize = 11.5 };
            var k = key; OnEnter(box, () => ProgramRun(DeckDesignKey, () => { var v = (box.Text ?? "").Trim(); if (!DeckDesign.IsHex(v)) throw new FormatException($"„{v}“ ist keine Farbe. Bitte z. B. #1F2937 eingeben."); d.Set(k, v.ToUpperInvariant(), label); }));
            sw.Children.Add(new StackPanel { Margin = new Thickness(0, 0, 6, 4), Children = { new TextBlock { Text = label, Foreground = Muted, FontSize = 11 },
                new StackPanel { Orientation = Orientation.Horizontal, Spacing = 3, Children = { new Border { Width = 16, Height = 16, CornerRadius = new CornerRadius(3), Background = Brush.Parse(val), BorderBrush = Edge, BorderThickness = new Thickness(1) }, box } } } });
        }
        sp.Children.Add(sw);
        sp.Children.Add(Combo("dd-font", [.. DdFonts.Select(f => (f, f))], t.Font, v => d.Set("font", v, "Schrift")));
        var issues = DeckDesign.CheckTheme(t);
        var contrast = new StackPanel { Name = "dd-contrast", Spacing = 1 };
        contrast.Children.Add(new TextBlock { Text = string.Create(CultureInfo.GetCultureInfo("de-DE"), $"Kontrast Text {DeckDesign.Contrast(t.Text, t.Background):0.0} : 1 · Titel {DeckDesign.Contrast(t.Title, t.Background):0.0} : 1"), Foreground = Muted, FontSize = 11 });
        if (issues.Count == 0) contrast.Children.Add(new TextBlock { Text = "✓ Alle Farben gut lesbar", Foreground = K4Good, FontSize = 12 });
        foreach (var i in issues) contrast.Children.Add(new TextBlock { Text = "⚠ " + i, Foreground = K4Warn, FontSize = 11.5, TextWrapping = TextWrapping.Wrap });
        sp.Children.Add(contrast);
        var fix = new CheckBox { Name = "dd-fix-auto", Content = "Lesbarkeit immer sichern", IsChecked = d.Data["fixContrast"].AsBool(), Foreground = Text, FontSize = 12 };
        fix.IsCheckedChanged += (_, _) => { if ((fix.IsChecked == true) != d.Data["fixContrast"].AsBool()) ProgramRun(DeckDesignKey, () => d.Edit("Lesbarkeit", x => x["fixContrast"] = fix.IsChecked == true)); };
        sp.Children.Add(fix);

        // Folienart der gewählten Folie
        sp.Children.Add(H("Folie", 13));
        try
        {
            var r = DdBuild(d); var sel = Math.Clamp(DdSelected(d), 0, r.Doc.Slides.Count - 1); var ov = DdOverrides(d);
            List<(string Id, string Name)> items = [("", "Automatisch (" + DeckDesign.Name(ov.ContainsKey(sel) ? DeckDesign.Build(d.Text("outline"), t, DdOptions(d)).Layouts.ElementAtOrDefault(sel) : r.Layouts[sel]) + ")"),
                .. DeckDesign.LayoutNames.Select(l => (l.Layout.ToString(), l.Name))];
            sp.Children.Add(Combo("dd-slide-layout", items, ov.TryGetValue(sel, out var cur) ? cur.ToString() : "", v => DdSetLayout(d, sel, DeckDesign.ParseLayout(v))));
        }
        catch (InvalidOperationException) { sp.Children.Add(P("Erst eine Gliederung schreiben.", Muted, 11)); }

        // Regeln und Zeit
        sp.Children.Add(H("Regeln und Redezeit", 13));
        sp.Children.Add(P("Höchstens Punkte je Folie", null, 11)); sp.Children.Add(KrBox(d, "maxPoints", "dd-max-points", "6"));
        sp.Children.Add(P("Redezeit gesamt (Minuten, leer = schätzen)", null, 11)); sp.Children.Add(KrBox(d, "minutes", "dd-minutes", "z. B. 10"));
        var notes = new CheckBox { Name = "dd-auto-notes", Content = "Fehlende Sprechernotizen vorschlagen", IsChecked = d.Data["autoNotes"].AsBool(true), Foreground = Text, FontSize = 12 };
        notes.IsCheckedChanged += (_, _) => { if ((notes.IsChecked == true) != d.Data["autoNotes"].AsBool(true)) ProgramRun(DeckDesignKey, () => d.Edit("Notizen", x => x["autoNotes"] = notes.IsChecked == true)); };
        sp.Children.Add(notes);

        // KI
        sp.Children.Add(H("Mit der KI", 13));
        sp.Children.Add(KrBox(d, "topic", "dd-topic", "Thema, z. B. „Energiesparen im Haushalt“"));
        var ai = new Button { Name = "dd-ai-go", Content = _ddAiBusy ? "Die KI entwirft …" : "Gliederung entwerfen", IsEnabled = !_ddAiBusy, FontSize = 12 };
        ToolTip.SetTip(ai, "Lokale KI auf deinem PC – der Entwurf ersetzt die Gliederung links (Rückgängig holt sie zurück).");
        ai.Click += (_, _) => _ = DdAi(d, d.Text("topic"));
        sp.Children.Add(ai);
        return sp;
    }
}
