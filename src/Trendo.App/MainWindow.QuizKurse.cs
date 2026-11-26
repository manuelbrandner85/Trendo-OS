using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Trendo.Core;
using Trendo.Core.Buero;
using Trendo.Core.Kreativ;
using Trendo.Core.Programs;
using Trendo.Core.Wissen;
using Path = System.IO.Path;

namespace Trendo.App;

/// <summary>T-0779 (Etappe 0.9.113 „Wissen &amp; Lernen“, Freigabe 26.11.2026): STUDIO-QUIZ-01 „Quiz erstellen“ und STUDIO-COURSE-01 „Kurse erstellen“.
/// Quiz: Editor für fünf Fragetypen, Spielmodus mit Zeit, Mischen, Punkten und Auswertung; Ausgabe als eigenständiges HTML-Quiz, Druck-PDF mit Lösungen und Moodle-GIFT.
/// Kurs: Kapitel und Lektionen aus Bausteinen (Text, Überschrift, Liste, Hinweis, Bild, Quiz aus „Quiz erstellen“), Kursansicht zum Durcharbeiten mit Fortschritt je Person,
/// Ausgabe als Webseite und SCORM-1.2-Paket. Kerne in <c>Trendo.Core.Wissen</c>; Anforderungen in docs/nachbau/quiz|kurse/anforderungen.md.</summary>
public partial class MainWindow
{
    public const string QuizKey = "quiz-programm", CourseKey = "kurse-programm";
    private void InitWissenStudios() { InitQuiz(); InitKurse(); }

    static readonly IBrush WkCardBg = Brush.Parse("#1C1813"), WkGold = Brush.Parse("#C9A227"), WkLine = Brush.Parse("#3A3024");
    static Border WkCard(Control child, string? name = null, bool on = false) => new()
    {
        Name = name, Padding = new Thickness(14, 12), CornerRadius = new CornerRadius(12), Child = child, Background = WkCardBg,
        BorderBrush = on ? WkGold : WkLine, BorderThickness = new Thickness(on ? 2 : 1), Margin = new Thickness(0, 0, 0, 10),
    };
    Button WkBtn(string name, string label, string key, Action go, string? tip = null)
    {
        var b = new Button { Name = name, Content = label, Padding = new Thickness(8, 3), Margin = new Thickness(0, 0, 4, 4), FontSize = 12, Background = Brushes.Transparent, BorderBrush = WkLine, BorderThickness = new Thickness(1) };
        if (tip is not null) ToolTip.SetTip(b, tip);
        b.Click += (_, _) => ProgramRun(key, go);
        return b;
    }

    /// <summary>Tests: Uhr des Spielmodus (Sekunden je Frage).</summary>
    public static Func<DateTime>? QuizNowOverride { get; set; }
    static DateTime QuizNow => QuizNowOverride?.Invoke() ?? DateTime.Now;

    // ======================================================================
    // Quiz erstellen
    // ======================================================================

    public static Quiz QuizOf(ProgramDocument d) => Quiz.FromJson(d.Data["quiz"]);
    public void QuizChange(ProgramDocument d, string label, Action<Quiz> change)
    {
        var q = QuizOf(d); change(q); var j = q.ToJson();
        d.Edit(label, x => x["quiz"] = j);
    }
    /// <summary>„quelle“ steht vorn, damit die KI den Text auch bei langen Quizzen sieht.</summary>
    static JsonObject QuizDoc(Quiz q, string quelle = "") => new() { ["quelle"] = quelle, ["quiz"] = q.ToJson(), ["sel"] = 0 };
    public static int QuizSel(ProgramDocument d) { var n = QuizOf(d).Fragen.Count; return n == 0 ? -1 : Math.Clamp((int)d.Number("sel"), 0, n - 1); }

    static Quiz QuizBeispiel(bool spiel = false) => new()
    {
        Titel = spiel ? "Europa-Quiz mit Zeit" : "Beispiel: Europa",
        Beschreibung = "Fünf Fragen – je eine von jeder Art.",
        Sekunden = spiel ? 20 : 0, TempoBonus = spiel, Bestehen = 60,
        Fragen =
        [
            new() { Typ = "auswahl", Text = "Welche Stadt ist die Hauptstadt von Frankreich?", Antworten = [new() { Text = "Paris", Richtig = true }, new() { Text = "Lyon" }, new() { Text = "Marseille" }, new() { Text = "Nizza" }], Erklaerung = "Paris ist seit dem Mittelalter die Hauptstadt." },
            new() { Typ = "mehrfach", Text = "Welche dieser Länder grenzen an Deutschland?", Antworten = [new() { Text = "Polen", Richtig = true }, new() { Text = "Österreich", Richtig = true }, new() { Text = "Dänemark", Richtig = true }, new() { Text = "Spanien" }, new() { Text = "Italien" }], Punkte = 2 },
            new() { Typ = "wahrfalsch", Text = "Die Donau mündet ins Schwarze Meer.", Antworten = QuizKit.WahrFalsch(true) },
            new() { Typ = "luecke", Text = "Der höchste Berg Deutschlands ist die [Zugspitze]." },
            new() { Typ = "zuordnen", Text = "Ordne die Hauptstädte zu.", Paare = [new() { Links = "Italien", Rechts = "Rom" }, new() { Links = "Spanien", Rechts = "Madrid" }, new() { Links = "Polen", Rechts = "Warschau" }], Punkte = 3 },
        ],
    };

    private void InitQuiz() => RegisterProgram(FachTabs, new ProgramSpec(QuizKey, "STUDIO-QUIZ-01", "Quiz erstellen")
    {
        Sentence = "Quiz mit Auswahl, Mehrfachauswahl, Wahr/Falsch, Lückentext und Zuordnen – mit Punkten, Zeit und Spielmodus; zum Weitergeben als HTML, zum Drucken als PDF, für Moodle als GIFT.",
        Folder = "Quiz",
        ScrollWorkspace = true,
        GroupLevels = new() { ["Fragen"] = 0, ["Spielen"] = 0, ["Laden"] = 1 },
        Templates =
        [
            new("leer", "Leeres Quiz", "Fragen selbst anlegen oder einen Text für die KI laden", () => QuizDoc(new Quiz())),
            new("beispiel", "Beispiel: Europa", "Je eine Frage jeder Art zum Ausprobieren", () => QuizDoc(QuizBeispiel())),
            new("spiel", "Spiel-Quiz mit Zeit", "20 Sekunden je Frage, Tempo-Bonus, gemischt", () => QuizDoc(QuizBeispiel(true))),
        ],
        Ribbon =
        [
            ("Fragen", [
                new ProgramCmd("quiz-add", "＋ Frage", d => QuizAdd(d, "auswahl"), Primary: true, Tip: "Neue Auswahlfrage – die Art lässt sich danach ändern."),
                new ProgramCmd("quiz-add-mehrfach", "＋ Mehrfachauswahl", d => QuizAdd(d, "mehrfach"), Tip: "Mehrere Antworten können richtig sein."),
                new ProgramCmd("quiz-add-wahr", "＋ Wahr/Falsch", d => QuizAdd(d, "wahrfalsch"), Tip: "Eine Aussage bewerten."),
                new ProgramCmd("quiz-add-luecke", "＋ Lückentext", d => QuizAdd(d, "luecke"), Tip: "Wörter in [eckigen Klammern] werden zu Lücken."),
                new ProgramCmd("quiz-add-zuordnen", "＋ Zuordnen", d => QuizAdd(d, "zuordnen"), Tip: "Begriffe und Bedeutungen verbinden."),
                new ProgramCmd("quiz-dup", "Duplizieren", d => QuizDuplicate(d), Tip: "Gewählte Frage verdoppeln (Strg+D).", Level: 1),
                new ProgramCmd("quiz-up", "Nach oben", d => QuizMove(d, -1), Tip: "Gewählte Frage eins nach oben.", Level: 1),
                new ProgramCmd("quiz-down", "Nach unten", d => QuizMove(d, 1), Tip: "Gewählte Frage eins nach unten.", Level: 1),
                new ProgramCmd("quiz-del", "Frage löschen", d => QuizDelete(d), Tip: "Gewählte Frage löschen (Rückgängig holt sie zurück).", Level: 1),
            ]),
            ("Spielen", [
                new ProgramCmd("quiz-play", "▶ Quiz spielen", d => QuizStartPlay(d), Tip: "Spielmodus mit Zeit, Punkten und Auswertung (F5)."),
                new ProgramCmd("quiz-check", "Prüfen", d => QuizCheckNow(d), Tip: "Fehlt eine richtige Antwort? Eine Lücke? Ist eine Zuordnung doppelt?"),
            ]),
            ("Laden", [
                new ProgramCmd("quiz-import", "Fragen laden (Text, GIFT) …", d => _ = QuizPickImport(d), Tip: "Moodle-GIFT oder das einfache Fragen-Textformat einlesen."),
                new ProgramCmd("quiz-from-cards", "Aus Karteikarten", d => QuizFromCards(d), Tip: "Karten eines Stapels aus „Lernen mit Karteikarten“ als Auswahlfragen."),
                new ProgramCmd("quiz-to-cards", "In Karteikarten übernehmen", d => QuizToCards(d), Tip: "Jede Frage wird eine Karteikarte (Frage → Lösung).", Level: 1),
                new ProgramCmd("quiz-open-source", "Text für die KI laden …", d => _ = QuizPickSource(d), Tip: "Text- oder Markdown-Datei, aus der die KI Fragen machen soll.", Level: 1),
            ]),
        ],
        Menus = d =>
        [
            new("Fragen",
            [
                new("quiz-key-new", "Neue Frage", () => ProgramRun(QuizKey, () => QuizAdd(d, "auswahl")), "Strg+Umschalt+N"),
                new("quiz-key-dup", "Frage duplizieren", () => ProgramRun(QuizKey, () => QuizDuplicate(d)), "Strg+D", () => QuizSel(d) >= 0),
                new("quiz-key-play", "Quiz spielen", () => ProgramRun(QuizKey, () => QuizStartPlay(d)), "F5", () => QuizOf(d).Fragen.Count > 0),
            ]),
        ],
        KiActions = [QuizKiQuestions],
        KiHint = "Fragen schreibst du NUR mit EINEM Schritt „quiz.fragen“ – nie mit „quiz-add“ oder anderen Menübefehlen (die legen nur leere Musterfragen an) und ohne weitere Schritte davor oder danach: Parameter „fragen“ ist ein Text im Fragen-Format, je Frage ein Block, Blöcke durch eine Leerzeile getrennt. "
               + "Auswahl: Zeile „? Frage“, darunter „+ richtige Antwort“ und „- falsche Antwort“ (mehrere „+“ = Mehrfachauswahl). Wahr/Falsch: „W: Aussage“ und „+ Wahr“ oder „+ Falsch“. "
               + "Lückentext: „L: Satz mit [Lösung] in eckigen Klammern“. Zuordnen: „Z: Aufgabe“ und darunter Zeilen „Begriff = Bedeutung“. Optional „E: Erklärung“. "
               + "Grundlage ist der Text im Feld „quelle“ des Dokuments oder im Auftrag – nimm nur, was dort steht, und erfinde keine Fakten. Höchstens 12 Fragen je Auftrag.",
        Exports =
        [
            new("html", "Quiz zum Weitergeben (HTML)", ".html", (d, f) => { QuizHtml.Write(QuizReady(d), f); return f; }),
            new("pdf", "Zum Ausdrucken mit Lösungen (PDF)", ".pdf", (d, f) => { QuizPdf.Write(QuizReady(d), f); return f; }),
            new("gift", "Für Moodle (GIFT)", ".txt", (d, f) => { AtomicFile.WriteAllText(f, QuizGift.Write(QuizReady(d))); return f; }),
            new("text", "Fragen als Text", ".txt", (d, f) => { AtomicFile.WriteAllText(f, QuizText.Write(QuizOf(d).Fragen)); return f; }),
        ],
        SimpleExports = 2,
        FirstStep = new("Erste Frage anlegen", "Leg eine Frage an – oder zieh einen Text hierher und lass die KI Fragen daraus machen.", d => ProgramRun(QuizKey, () => QuizAdd(d, "auswahl")), d => QuizOf(d).Fragen.Count == 0),
        Workspace = QuizWorkspace,
        Properties = QuizProperties,
        Status = d =>
        {
            var q = QuizOf(d); var err = QuizKit.Pruefen(q).Count(x => x.Fehler);
            return $"{q.Fragen.Count} Fragen · {q.MaxPunkte:0.##} Punkte · " + (q.Sekunden > 0 ? $"{q.Sekunden} s je Frage" : "ohne Zeitlimit") + $" · bestanden ab {q.Bestehen} %" + (err > 0 ? $" · {err} Fehler" : " · bereit");
        },
        Help = "Mit „＋ Frage“ oder den anderen Fragearten anlegen. Bei Auswahl den Kreis vor der richtigen Antwort anklicken; bei Lückentext das gesuchte Wort in [eckige Klammern] setzen, "
             + "mehrere richtige Schreibweisen mit | trennen ([Rom|Roma]). Rechtsklick auf eine Frage: duplizieren, verschieben, Art ändern, löschen. „▶ Quiz spielen“ (F5) startet den Spielmodus – "
             + "Tasten 1–4 wählen, Eingabe prüft. Ausgabe: HTML zum Weitergeben (läuft in jedem Browser, ohne Internet), PDF mit Lösungsblatt, GIFT für Moodle. "
             + "KI: Text ins Feld „Text für die KI“ legen und z. B. „Mach 5 Fragen aus dem Text“ schreiben – die Vorschau zeigt die Fragen, „Ja, behalten“ übernimmt sie als einen Schritt.",
    }, "quiz", "quiz erstellen", "test", "prüfungsfragen", "multiple choice", "lückentext", "zuordnen", "kahoot", "quizlet", "moodle", "gift", "h5p", "abfrage", "klassenarbeit");

    /// <summary>Für Ausgaben: Quiz ohne Fehler (sonst eine Meldung mit dem ersten Fehler).</summary>
    static Quiz QuizReady(ProgramDocument d)
    {
        var q = QuizOf(d);
        if (QuizKit.Pruefen(q).FirstOrDefault(x => x.Fehler) is { } e) throw new InvalidOperationException($"Vor der Ausgabe: {e.Feld} – {e.Text}");
        return q;
    }

    // ---------- Befehle ----------

    public void QuizAdd(ProgramDocument d, string typ)
    {
        var n = 0; var at = QuizSel(d);
        QuizChange(d, "Frage: " + QuizKit.TypName(typ), q => { var i = at < 0 ? q.Fragen.Count : at + 1; q.Fragen.Insert(i, QuizKit.Neu(typ)); n = i; });
        d.Data["sel"] = n;
        _progMsg[QuizKey] = $"Neue Frage ({QuizKit.TypName(typ)}) – Text und Antworten direkt überschreiben.";
    }

    public void QuizDuplicate(ProgramDocument d)
    {
        var i = QuizSel(d); if (i < 0) throw new InvalidOperationException("Es ist keine Frage gewählt.");
        QuizChange(d, "Frage dupliziert", q => { var c = q.Copy().Fragen[i]; c.Id = Guid.NewGuid().ToString("N")[..8]; q.Fragen.Insert(i + 1, c); });
        d.Data["sel"] = i + 1;
    }

    public void QuizMove(ProgramDocument d, int by)
    {
        var i = QuizSel(d); var n = QuizOf(d).Fragen.Count; var j = i + by;
        if (i < 0 || j < 0 || j >= n) return;
        QuizChange(d, by < 0 ? "Frage nach oben" : "Frage nach unten", q => (q.Fragen[i], q.Fragen[j]) = (q.Fragen[j], q.Fragen[i]));
        d.Data["sel"] = j;
    }

    public void QuizDelete(ProgramDocument d, int? index = null)
    {
        var i = index ?? QuizSel(d); if (i < 0 || i >= QuizOf(d).Fragen.Count) throw new InvalidOperationException("Es ist keine Frage gewählt.");
        QuizChange(d, "Frage gelöscht", q => q.Fragen.RemoveAt(i));
        d.Data["sel"] = Math.Max(0, i - 1);
        _progMsg[QuizKey] = "Frage gelöscht – „Rückgängig“ (Strg+Z) holt sie zurück.";
    }

    public void QuizSetType(ProgramDocument d, int i, string typ) => QuizChange(d, "Art: " + QuizKit.TypName(typ), q => QuizKit.TypWechseln(q.Fragen[i], typ));

    public List<CvFinding> QuizCheckNow(ProgramDocument d)
    {
        var f = QuizKit.Pruefen(QuizOf(d)); var err = f.Count(x => x.Fehler);
        _progMsg[QuizKey] = f.Count == 0 ? "✓ Das Quiz ist bereit zum Spielen und Weitergeben." : $"Prüfung: {err} Fehler, {f.Count - err} Hinweise – rechts unter „Prüfung“.";
        return f;
    }

    /// <summary>Fragen aus einer Datei: GIFT (Moodle) oder das Fragen-Textformat. Eine Datei ohne Fragen wird zum Text für die KI.</summary>
    public int QuizImportFile(ProgramDocument d, string file)
    {
        if (!File.Exists(file)) throw new FileNotFoundException("Die Datei gibt es nicht.", file);
        if (new FileInfo(file).Length > 2 * 1024 * 1024) throw new InvalidDataException("Die Datei ist zu groß (höchstens 2 MB).");
        var text = File.ReadAllText(file);
        if (Path.GetExtension(file).Equals(".csv", StringComparison.OrdinalIgnoreCase)) return QuizFromPairs(d, ParsePairs(text), Path.GetFileNameWithoutExtension(file));
        List<QuizFrage> got; List<string> skipped; string how;
        if (text.Contains('{') && text.Contains('}')) { var (q, s) = QuizGift.Read(text, Path.GetFileNameWithoutExtension(file)); got = q.Fragen; skipped = s; how = "GIFT"; }
        else { (got, skipped) = QuizText.Parse(text); how = "Text"; }
        if (got.Count == 0)
        {
            if (Path.GetExtension(file).ToLowerInvariant() is ".txt" or ".md") { QuizSetSource(d, text, Path.GetFileName(file)); return 0; }
            throw new InvalidDataException("In der Datei stehen keine lesbaren Fragen." + (skipped.Count > 0 ? " Ausgelassen: " + skipped[0] : ""));
        }
        QuizChange(d, $"{got.Count} Fragen geladen", q => { if (q.Fragen.Count == 0 && q.Titel == new Quiz().Titel) q.Titel = Path.GetFileNameWithoutExtension(file); q.Fragen.AddRange(got); });
        _progMsg[QuizKey] = $"{got.Count} Fragen aus „{Path.GetFileName(file)}“ ({how}) übernommen" + (skipped.Count > 0 ? $", {skipped.Count} ausgelassen (z. B. {skipped[0]})." : ".");
        return got.Count;
    }

    static List<(string F, string A)> ParsePairs(string text) =>
        [.. text.Replace("\r", "").Split('\n').Select(l => l.Contains('\t') ? l.Split('\t', 2) : l.Split(';', 2)).Where(p => p.Length == 2 && p[0].Trim().Length > 0 && p[1].Trim().Length > 0 && !p[0].TrimStart().StartsWith('#')).Select(p => (p[0].Trim().Trim('"'), p[1].Trim().Trim('"')))];

    /// <summary>Frage/Antwort-Paare (Karteikarten) als Auswahlfragen: falsche Antworten sind andere Antworten desselben Stapels (wie das Quiz der Karteikarten).</summary>
    int QuizFromPairs(ProgramDocument d, List<(string F, string A)> pairs, string title)
    {
        var answers = pairs.Select(p => p.A).Distinct().ToList();
        if (pairs.Count == 0 || answers.Count < 2) throw new InvalidOperationException("Für Auswahlfragen braucht es mindestens zwei Karten mit verschiedenen Antworten.");
        var rnd = new Random(pairs.Count * 31 + answers.Count);
        var list = pairs.Select(p => new QuizFrage { Typ = "auswahl", Text = p.F, Antworten = [new() { Text = p.A, Richtig = true }, .. answers.Where(a => a != p.A).OrderBy(_ => rnd.Next()).Take(3).Select(a => new QuizAntwort { Text = a })] }).ToList();
        QuizChange(d, $"{list.Count} Fragen aus Karteikarten", q => { if (q.Fragen.Count == 0 && q.Titel == new Quiz().Titel) q.Titel = title; q.Fragen.AddRange(list); });
        _progMsg[QuizKey] = $"{list.Count} Auswahlfragen aus „{title}“ angelegt – je eine richtige und bis zu drei falsche Antworten aus demselben Stapel.";
        return list.Count;
    }

    public int QuizFromCards(ProgramDocument d)
    {
        var deck = d.Text("stapel"); if (deck.Length == 0 || !Learn.Decks.Contains(deck)) deck = Learn.Decks.FirstOrDefault() ?? "";
        if (deck.Length == 0) throw new InvalidOperationException("Es gibt noch keine Karteikarten – erst in „Lernen mit Karteikarten“ einen Stapel anlegen.");
        return QuizFromPairs(d, [.. Learn.Store.Cards.Where(c => c.Deck == deck).Select(c => (c.Front, c.Back))], deck);
    }

    public int QuizToCards(ProgramDocument d)
    {
        var q = QuizOf(d); if (q.Fragen.Count == 0) throw new InvalidOperationException("Das Quiz hat noch keine Frage.");
        var deck = DevStudio.SafeName(q.Titel).Trim(); if (deck.Length == 0) deck = "Quiz";
        var have = Learn.Store.Cards.Where(c => c.Deck == deck).Select(c => c.Front).ToHashSet();
        var n = 0;
        foreach (var f in q.Fragen)
        {
            var front = f.Typ == "luecke" ? QuizKit.LueckenText(f.Text) : f.Text;
            if (have.Contains(front) || QuizKit.Loesung(f).Trim().Length == 0) continue;
            Learn.Add(deck, front, QuizKit.Loesung(f)); n++;
        }
        _progMsg[QuizKey] = n == 0 ? $"Alle Fragen stehen schon im Stapel „{deck}“." : $"{n} Karteikarten im Stapel „{deck}“ angelegt – zu finden in „Lernen mit Karteikarten“.";
        return n;
    }

    public void QuizSetSource(ProgramDocument d, string text, string from = "")
    {
        var t = (text ?? "").Replace("\r", "").Trim();
        if (t.Length > 6000) t = t[..6000];
        d.Edit("Text für die KI", x => x["quelle"] = t);
        _progMsg[QuizKey] = (from.Length > 0 ? $"Text aus „{from}“ geladen" : "Text übernommen") + $" ({t.Length} Zeichen) – jetzt z. B. „Mach 5 Fragen aus dem Text“ in die Auftragszeile schreiben.";
    }

    async Task QuizPickImport(ProgramDocument d)
    {
        var f = await PickMediaFile("quiz-import", "Fragen laden", ["*.txt", "*.gift", "*.md", "*.csv"]);
        if (f is not null) ProgramRun(QuizKey, () => QuizImportFile(d, f));
    }
    async Task QuizPickSource(ProgramDocument d)
    {
        var f = await PickMediaFile("quiz-source", "Text für die KI", ["*.txt", "*.md"]);
        if (f is not null) ProgramRun(QuizKey, () => QuizSetSource(d, File.ReadAllText(f), Path.GetFileName(f)));
    }

    // ---------- KI-Fachaktion (T-0774-Baustein) ----------

    /// <summary>„Fragen ins Quiz schreiben“: Die KI liefert Fragen im Fragen-Textformat; in der Vorschau stehen sie schon im Quiz, „Ja, behalten“ übernimmt sie als EINEN Rückgängig-Schritt.
    /// Es entsteht keine Datei – das Ergebnis ist das offene Quiz selbst (gespeichert wie jede Änderung).</summary>
    ProgramKiAction QuizKiQuestions => new(new AssistAction("quiz.fragen", "Neue Fragen mit Inhalt ins Quiz schreiben (fragen = alle Fragen im Fragen-Format)", ["fragen", "frage", "quiz", "erzeugen", "erstellen", "machen", "generieren", "aus", "text", "datei", "multiple", "choice", "lueckentext"],
        [new AssistParam("fragen", "Fragen im Fragen-Format")]),
        (d, a) =>
        {
            var (got, skipped) = QuizText.Parse(a.GetValueOrDefault("fragen") ?? "");
            if (got.Count == 0) throw new InvalidOperationException("Die KI hat keine lesbaren Fragen geliefert" + (skipped.Count > 0 ? $" (ausgelassen: {skipped[0]})." : "."));
            if (got.Count > 20) got = got[..20];
            var at = QuizOf(d).Fragen.Count;
            QuizChange(d, $"{got.Count} Fragen aus dem Text", q => q.Fragen.AddRange(got));
            d.Data["sel"] = at;
        },
        (_, _) => []) { MakesFile = false };

    // ---------- Spielmodus ----------

    /// <summary>Laufender Durchgang (je Dokument bzw. je Quiz-Baustein eines Kurses) – gehört nicht zum Dokument, nichts davon landet in Rückgängig.</summary>
    public sealed class QuizPlay(QuizSpiel spiel)
    {
        public QuizSpiel Spiel { get; } = spiel;
        public DateTime Start = QuizNow;
        public readonly List<int> Sel = [];
        public readonly Dictionary<int, string> Gaps = [];
        public readonly Dictionary<int, int> Map = [];
        public bool Shown;
        /// <summary>Letzte Rückmeldung gesehen, Auswertung steht.</summary>
        public bool Ended;
        public DispatcherTimer? Timer;
        public Action? Redraw;
        public ProgressBar? Bar;
        public QuizEingabe Eingabe(QuizFrage f) => f.Typ switch
        {
            "luecke" => QuizEingabe.Text([.. Enumerable.Range(0, QuizKit.Luecken(f.Text).Count).Select(i => Gaps.GetValueOrDefault(i, ""))]),
            "zuordnen" => QuizEingabe.Paare([.. Enumerable.Range(0, f.Paare.Count).Select(i => Map.GetValueOrDefault(i, -1))]),
            _ => QuizEingabe.Auswahl([.. Sel]),
        };
        public void Next() { Sel.Clear(); Gaps.Clear(); Map.Clear(); Shown = false; Start = QuizNow; }
    }
    readonly Dictionary<string, QuizPlay> _quizPlay = [];
    public QuizPlay? QuizPlaying(ProgramDocument d) => _quizPlay.GetValueOrDefault(d.Id);

    public QuizPlay QuizStartPlay(ProgramDocument d, int? seed = null)
    {
        var p = new QuizPlay(new QuizSpiel(QuizOf(d), seed ?? Environment.TickCount));
        QuizStopTimer(d.Id); _quizPlay[d.Id] = p;
        _progMsg[QuizKey] = "Spielmodus: Tasten 1–4 wählen, Eingabe prüft. „Beenden“ führt zurück zum Bearbeiten.";
        return p;
    }
    public void QuizStopPlay(ProgramDocument d) { QuizStopTimer(d.Id); _quizPlay.Remove(d.Id); _progMsg[QuizKey] = ""; }
    void QuizStopTimer(string id) { if (_quizPlay.GetValueOrDefault(id)?.Timer is { } t) t.Stop(); }

    /// <summary>Antwort prüfen (Knopf „Antwort prüfen“ oder Eingabe) – Zeit seit Anzeige der Frage zählt.</summary>
    public QuizSpiel.Ergebnis? QuizPlayCheck(QuizPlay p)
    {
        if (p.Shown || p.Spiel.Aktuell is not { } f) return null;
        var r = p.Spiel.Antworten(p.Eingabe(f), (QuizNow - p.Start).TotalSeconds); p.Shown = true; p.Timer?.Stop();
        return r;
    }

    /// <summary>Zeitprüfung der aktuellen Frage – läuft alle 250 ms über den Timer des Spielers: Balken nachführen, bei 0 zählt die Frage 0 Punkte und die Rückmeldung erscheint.
    /// True, wenn die Zeit gerade abgelaufen ist.</summary>
    public static bool QuizPlayTick(QuizPlay p)
    {
        if (p.Shown || p.Spiel.Aktuell is not { } f) return false;
        var limit = p.Spiel.Quiz.ZeitFuer(f); if (limit <= 0) return false;
        var left = limit - (QuizNow - p.Start).TotalSeconds;
        if (p.Bar is { } bar) bar.Value = Math.Max(0, left);
        if (left > 0) return false;
        p.Timer?.Stop(); p.Spiel.ZeitAbgelaufen(); p.Shown = true; p.Redraw?.Invoke();
        return true;
    }

    /// <summary>„Weiter“ bzw. „Zur Auswertung“ nach der Rückmeldung.</summary>
    public static void QuizPlayNext(QuizPlay p, Action<QuizSpiel>? done = null)
    {
        if (!p.Shown) return;
        if (p.Spiel.Fertig) { if (!p.Ended) { p.Ended = true; done?.Invoke(p.Spiel); } }
        else p.Next();
    }

    /// <summary>Spieler als Steuerelement – im Quiz-Studio und als Quiz-Baustein in der Kursansicht. <paramref name="done"/> läuft einmal am Ende.</summary>
    Control QuizPlayView(string key, string stateId, QuizPlay p, string n, Action redraw, Action<QuizSpiel>? done = null, Action? quit = null)
    {
        var s = p.Spiel; var q = s.Quiz;
        var sp = new StackPanel { Name = n, Spacing = 12, MaxWidth = 820, HorizontalAlignment = HorizontalAlignment.Stretch };
        if (s.Fertig && p.Ended)
        {
            var ok = s.Bestanden;
            sp.Children.Add(new TextBlock { Text = "Auswertung", FontSize = 20, FontWeight = FontWeight.SemiBold, Foreground = Text });
            sp.Children.Add(new TextBlock { Name = n + "-pct", Text = $"{s.Prozent} %", FontSize = 46, FontWeight = FontWeight.Bold, Foreground = WkGold });
            sp.Children.Add(new TextBlock { Name = n + "-sum", Text = $"{s.Grundpunkte:0.##} von {s.Max:0.##} Punkten" + (s.Punkte > s.Grundpunkte ? $" + {s.Punkte - s.Grundpunkte:0.##} Tempo-Bonus" : "") + " · " + (ok ? "Bestanden" : $"Nicht bestanden (ab {q.Bestehen} %)"), Foreground = ok ? K4Good : K4Bad, FontSize = 15 });
            var tab = new StackPanel { Name = n + "-table", Spacing = 4 };
            foreach (var r in s.Ergebnisse)
            {
                var f = q.Fragen[r.Frage];
                tab.Children.Add(new Grid { ColumnDefinitions = new ColumnDefinitions("24,*,80,70"), Children = {
                    new TextBlock { Text = r.Anteil >= 1 ? "✓" : r.Anteil > 0 ? "½" : "✗", Foreground = r.Anteil >= 1 ? K4Good : r.Anteil > 0 ? K4Warn : K4Bad },
                    Pos(new TextBlock { Text = (f.Typ == "luecke" ? QuizKit.LueckenText(f.Text, "___") : f.Text) + " – " + QuizKit.Loesung(f), Foreground = Text, FontSize = 12, TextWrapping = TextWrapping.Wrap }, 1),
                    Pos(new TextBlock { Text = $"{r.Punkte:0.##}/{f.Punkte:0.##}", Foreground = Muted, FontSize = 12 }, 2),
                    Pos(new TextBlock { Text = r.ZeitUm ? "Zeit um" : $"{r.Sekunden:0.#} s", Foreground = Muted, FontSize = 12 }, 3) } });
            }
            sp.Children.Add(tab);
            var row = new WrapPanel();
            row.Children.Add(WkBtn(n + "-again", "Nochmal spielen", key, () => { var np = new QuizPlay(new QuizSpiel(q, s.Seed + 1)); _quizPlay[stateId] = np; }));
            if (quit is not null) row.Children.Add(WkBtn(n + "-quit", "Zurück zum Bearbeiten", key, quit));
            sp.Children.Add(row);
            return sp;
        }
        var cur = p.Shown && s.Letztes is { } la ? q.Fragen[la.Frage] : s.Aktuell!; var limit = q.ZeitFuer(cur); var num = p.Shown ? s.Position : s.Position + 1;
        var head = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        head.Children.Add(new TextBlock { Name = n + "-pos", Text = $"Frage {num} von {s.Reihenfolge.Count} · {cur.Punkte:0.##} {(cur.Punkte == 1 ? "Punkt" : "Punkte")} · bisher {s.Punkte:0.##} Punkte", Foreground = Muted, FontSize = 12 });
        if (quit is not null) head.Children.Add(Pos(WkBtn(n + "-stop", "Beenden", key, quit), 1));
        sp.Children.Add(head);
        if (limit > 0 && !p.Shown)
        {
            var bar = new ProgressBar { Name = n + "-time", Minimum = 0, Maximum = limit, Value = Math.Max(0, limit - (QuizNow - p.Start).TotalSeconds), Height = 6, Foreground = WkGold };
            sp.Children.Add(bar);
            p.Timer?.Stop();
            var t = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
            p.Redraw = redraw; p.Bar = bar;
            t.Tick += (_, _) => { if (!bar.IsAttachedToVisualTree() || p.Shown) { t.Stop(); return; } QuizPlayTick(p); };
            p.Timer = t; t.Start();
        }
        if (cur.Typ != "luecke") sp.Children.Add(new TextBlock { Name = n + "-text", Text = cur.Text, FontSize = 24, FontWeight = FontWeight.SemiBold, Foreground = Text, TextWrapping = TextWrapping.Wrap });
        var last = p.Shown ? s.Letztes : null;
        switch (cur.Typ)
        {
            case "auswahl" or "mehrfach" or "wahrfalsch":
            {
                var order = QuizKit.AntwortReihenfolge(q, cur, s.Seed);
                var grid = new UniformGrid { Name = n + "-opts", Columns = order.Count <= 2 ? 2 : 2 };
                for (var k = 0; k < order.Count; k++)
                {
                    var idx = order[k]; var a = cur.Antworten[idx]; var on = p.Sel.Contains(idx);
                    var border = p.Shown ? (a.Richtig ? K4Good : on ? K4Bad : WkLine) : on ? WkGold : WkLine;
                    var b = new Button { Name = $"{n}-opt-{k}", Margin = new Thickness(4), Padding = new Thickness(14, 12), HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left,
                        Background = on ? Brush.Parse("#3A2F1A") : WkCardBg, BorderBrush = border, BorderThickness = new Thickness(2),
                        Content = new TextBlock { Text = $"{k + 1}  " + (cur.Typ == "mehrfach" ? (on ? "☑ " : "☐ ") : "") + a.Text, FontSize = 16, Foreground = Text, TextWrapping = TextWrapping.Wrap } };
                    b.Click += (_, _) => { if (p.Shown) return; if (cur.Typ == "mehrfach") { if (!p.Sel.Remove(idx)) p.Sel.Add(idx); } else { p.Sel.Clear(); p.Sel.Add(idx); } redraw(); };
                    grid.Children.Add(b);
                }
                if (cur.Typ == "mehrfach") sp.Children.Add(P("Mehrere Antworten können richtig sein.", Muted, 12));
                sp.Children.Add(grid);
                break;
            }
            case "luecke":
            {
                var parts = System.Text.RegularExpressions.Regex.Split(cur.Text, @"\[[^\[\]]+\]");
                var wrap = new WrapPanel { Name = n + "-gaps" };
                for (var k = 0; k < parts.Length; k++)
                {
                    foreach (var w in parts[k].Split(' ', StringSplitOptions.RemoveEmptyEntries)) wrap.Children.Add(new TextBlock { Text = w + " ", FontSize = 22, Foreground = Text, VerticalAlignment = VerticalAlignment.Center });
                    if (k < parts.Length - 1)
                    {
                        var kk = k; var box = new TextBox { Name = $"{n}-gap-{k}", Text = p.Gaps.GetValueOrDefault(k, ""), Width = 170, FontSize = 18, Margin = new Thickness(2, 2, 6, 2), IsReadOnly = p.Shown };
                        box.TextChanged += (_, _) => p.Gaps[kk] = box.Text ?? "";
                        wrap.Children.Add(box);
                    }
                }
                sp.Children.Add(wrap);
                break;
            }
            case "zuordnen":
            {
                var right = QuizKit.AntwortReihenfolge(q, cur, s.Seed);
                var list = new StackPanel { Name = n + "-pairs", Spacing = 6 };
                for (var k = 0; k < cur.Paare.Count; k++)
                {
                    var kk = k;
                    var items = new List<string> { "– wählen –" }; items.AddRange(right.Select(r => cur.Paare[r].Rechts));
                    var cb = new ComboBox { Name = $"{n}-pair-{k}", ItemsSource = items, SelectedIndex = p.Map.TryGetValue(k, out var m) && m >= 0 ? right.IndexOf(m) + 1 : 0, MinWidth = 220, IsEnabled = !p.Shown };
                    cb.SelectionChanged += (_, _) => p.Map[kk] = cb.SelectedIndex > 0 ? right[cb.SelectedIndex - 1] : -1;
                    list.Children.Add(new Grid { ColumnDefinitions = new ColumnDefinitions("*,12,Auto"), Children = { new TextBlock { Text = cur.Paare[k].Links, FontSize = 17, Foreground = Text, VerticalAlignment = VerticalAlignment.Center }, Pos(cb, 2) } });
                }
                sp.Children.Add(list);
                break;
            }
        }
        if (last is { } lr)
        {
            var good = lr.Anteil >= 1;
            var fb = new StackPanel { Spacing = 3 };
            fb.Children.Add(new TextBlock { Name = n + "-feedback", Text = lr.ZeitUm ? "Zeit abgelaufen." : good ? $"Richtig! +{lr.Punkte:0.##} Punkte" : lr.Anteil > 0 ? $"Teilweise richtig – {lr.Punkte:0.##} Punkte." : "Leider falsch.", FontSize = 16, FontWeight = FontWeight.SemiBold, Foreground = good ? K4Good : lr.Anteil > 0 ? K4Warn : K4Bad });
            fb.Children.Add(P("Lösung: " + QuizKit.Loesung(cur), Text, 13));
            if (cur.Erklaerung.Trim().Length > 0) fb.Children.Add(P(cur.Erklaerung, Muted, 12));
            sp.Children.Add(WkCard(fb));
            var next = new Button { Name = n + "-next", Content = s.Fertig ? "Zur Auswertung" : "Weiter", Padding = new Thickness(18, 8) }; QuietGold(next); // eine goldene Hauptaktion je Ansicht bleibt „＋ Frage“ im Menüband
            next.Click += (_, _) => ProgramRun(key, () => QuizPlayNext(p, done));
            sp.Children.Add(next);
        }
        else
        {
            var check = new Button { Name = n + "-check", Content = "Antwort prüfen", Padding = new Thickness(18, 8) }; QuietGold(check);
            check.Click += (_, _) => ProgramRun(key, () => { QuizPlayCheck(p); });
            sp.Children.Add(check);
        }
        sp.Focusable = true;
        sp.KeyDown += (_, e) =>
        {
            if (e.Source is TextBox && e.Key != Key.Enter) return;
            if (e.Key == Key.Enter) { e.Handled = true; ProgramRun(key, () => { if (!p.Shown) QuizPlayCheck(p); else QuizPlayNext(p, done); }); }
            else if (!p.Shown && e.Key is >= Key.D1 and <= Key.D9 && cur.Typ is "auswahl" or "mehrfach" or "wahrfalsch")
            {
                var k = e.Key - Key.D1; var order = QuizKit.AntwortReihenfolge(q, cur, s.Seed);
                if (k < order.Count) { var idx = order[k]; if (cur.Typ == "mehrfach") { if (!p.Sel.Remove(idx)) p.Sel.Add(idx); } else { p.Sel.Clear(); p.Sel.Add(idx); } e.Handled = true; redraw(); }
            }
        };
        sp.AttachedToVisualTree += (_, _) => _ui.Post(() => { if (cur.Typ != "luecke") sp.Focus(); }, DispatcherPriority.Input);
        return sp;
    }

    // ---------- Oberfläche ----------

    Control QuizWorkspace(ProgramDocument d)
    {
        var q = QuizOf(d);
        if (QuizPlaying(d) is { } play)
        {
            var host = new Border { Name = "quiz-play", Padding = new Thickness(20), CornerRadius = new CornerRadius(14), Background = WkCardBg, BorderBrush = WkGold, BorderThickness = new Thickness(1) };
            host.Child = QuizPlayView(QuizKey, d.Id, play, "quiz-play", () => Show(QuizKey), sp => _progMsg[QuizKey] = $"Ergebnis: {sp.Prozent} % ({sp.Punkte:0.##} von {sp.Max:0.##} Punkten) – {(sp.Bestanden ? "bestanden" : "nicht bestanden")}.",
                () => QuizStopPlay(d));
            return host;
        }
        var sel = QuizSel(d);
        var sp = new StackPanel { Name = "quiz-list", Spacing = 0, Margin = new Thickness(4, 0, 10, 4), Background = Brushes.Transparent }; // durchgehend Ablagefläche
        var head = new WrapPanel { Name = "quiz-head", Margin = new Thickness(0, 0, 0, 8) };
        head.Children.Add(new TextBlock { Text = q.Titel, FontSize = 20, FontWeight = FontWeight.SemiBold, Foreground = Text, Margin = new Thickness(0, 0, 14, 6), VerticalAlignment = VerticalAlignment.Center });
        head.Children.Add(BueroChip($"{q.Fragen.Count} Fragen", WkCardBg));
        head.Children.Add(BueroChip($"{q.MaxPunkte:0.##} Punkte", WkCardBg));
        head.Children.Add(BueroChip(q.Sekunden > 0 ? $"{q.Sekunden} s je Frage" : "ohne Zeitlimit", WkCardBg));
        head.Children.Add(BueroChip($"bestanden ab {q.Bestehen} %", WkCardBg));
        sp.Children.Add(head);
        if (d.Text("quelle").Length > 0)
            sp.Children.Add(WkCard(new TextBlock { Name = "quiz-source-note", Text = $"Text für die KI: {d.Text("quelle").Length} Zeichen – z. B. „Mach 5 Fragen aus dem Text“ in die Auftragszeile schreiben.", Foreground = Muted, FontSize = 12, TextWrapping = TextWrapping.Wrap }));
        for (var i = 0; i < q.Fragen.Count; i++) sp.Children.Add(QuizCardView(d, q, i, i == sel));
        if (q.Fragen.Count > 0)
        {
            var add = new WrapPanel { Name = "quiz-add-row" };
            foreach (var (id, name, _) in QuizKit.Typen) add.Children.Add(WkBtn("quiz-add-" + id + "-end", "＋ " + name, QuizKey, () => { d.Data["sel"] = QuizOf(d).Fragen.Count - 1; QuizAdd(d, id); }));
            sp.Children.Add(add);
        }
        BueroFileDrop(sp, QuizKey, BueroExt(".txt", ".gift", ".md", ".csv"), "Fragen (GIFT, Fragen-Text, CSV) oder einen Text für die KI (.txt, .md)", (dd, files) => { foreach (var f in files) QuizImportFile(dd, f); });
        return sp;
    }

    Control QuizCardView(ProgramDocument d, Quiz q, int i, bool on)
    {
        var f = q.Fragen[i]; var ii = i; var n = $"quiz-q-{i}";
        void Set(string label, Action<QuizFrage> a) => QuizChange(d, label, x => { if (ii < x.Fragen.Count) a(x.Fragen[ii]); });
        var sp = new StackPanel { Spacing = 6 };
        var top = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,8,Auto,*,Auto,8,Auto") };
        top.Children.Add(new TextBlock { Text = $"{i + 1}.", FontSize = 16, FontWeight = FontWeight.Bold, Foreground = WkGold, VerticalAlignment = VerticalAlignment.Center });
        top.Children.Add(Pos(BueroCombo(QuizKey, n + "-typ", QuizKit.Typen.Select(t => (t.Id, t.Name)), f.Typ, v => QuizSetType(d, ii, v), 150), 2));
        top.Children.Add(Pos(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Children = {
            BueroBox(n + "-pts", f.Punkte.ToString("0.##", CultureInfo.InvariantCulture), "Punkte", v => { if (double.TryParse(v.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var p) && p is > 0 and <= 1000) Set("Punkte", x => x.Punkte = p); }, false, 50),
            new TextBlock { Text = "Punkte", Foreground = Muted, FontSize = 11, VerticalAlignment = VerticalAlignment.Center } } }, 4));
        top.Children.Add(Pos(new StackPanel { Orientation = Orientation.Horizontal, Children = {
            WkBtn(n + "-up", "↑", QuizKey, () => { d.Data["sel"] = ii; QuizMove(d, -1); }, "Nach oben"), WkBtn(n + "-down", "↓", QuizKey, () => { d.Data["sel"] = ii; QuizMove(d, 1); }, "Nach unten"),
            WkBtn(n + "-dup", "⧉", QuizKey, () => { d.Data["sel"] = ii; QuizDuplicate(d); }, "Duplizieren"), WkBtn(n + "-del", "✕", QuizKey, () => QuizDelete(d, ii), "Frage löschen") } }, 6));
        sp.Children.Add(top);
        sp.Children.Add(BueroBox(n + "-text", f.Text, f.Typ == "luecke" ? "Satz mit [Lösung] in eckigen Klammern, mehrere Schreibweisen mit | trennen" : "Fragetext", v => Set("Fragetext", x => x.Text = v), f.Typ == "luecke"));
        switch (f.Typ)
        {
            case "auswahl" or "mehrfach":
                for (var k = 0; k < f.Antworten.Count; k++)
                {
                    var kk = k; var a = f.Antworten[k];
                    var mark = new Button { Name = $"{n}-a-{k}-ok", Content = a.Richtig ? (f.Typ == "mehrfach" ? "☑" : "◉") : (f.Typ == "mehrfach" ? "☐" : "○"), Width = 34, Padding = new Thickness(4, 2), FontSize = 15,
                        Foreground = a.Richtig ? K4Good : Muted, Background = Brushes.Transparent, BorderBrush = WkLine, BorderThickness = new Thickness(1) };
                    ToolTip.SetTip(mark, a.Richtig ? "Richtige Antwort" : "Als richtig markieren");
                    mark.Click += (_, _) => ProgramRun(QuizKey, () => Set("Richtige Antwort", x => { if (x.Typ == "auswahl") for (var j = 0; j < x.Antworten.Count; j++) x.Antworten[j].Richtig = j == kk; else x.Antworten[kk].Richtig = !x.Antworten[kk].Richtig; }));
                    sp.Children.Add(new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,6,*,6,Auto"), Children = { mark,
                        Pos(BueroBox($"{n}-a-{k}", a.Text, $"Antwort {k + 1}", v => Set("Antwort", x => { if (kk < x.Antworten.Count) x.Antworten[kk].Text = v; })), 2),
                        Pos(WkBtn($"{n}-a-{k}-del", "✕", QuizKey, () => Set("Antwort gelöscht", x => { if (x.Antworten.Count > 2) x.Antworten.RemoveAt(kk); else throw new InvalidOperationException("Es braucht mindestens zwei Antworten."); }), "Antwort löschen"), 4) } });
                }
                sp.Children.Add(WkBtn(n + "-a-add", "＋ Antwort", QuizKey, () => Set("Antwort", x => { if (x.Antworten.Count >= 10) throw new InvalidOperationException("Höchstens 10 Antworten je Frage."); x.Antworten.Add(new QuizAntwort { Text = "Neue Antwort" }); })));
                break;
            case "wahrfalsch":
            {
                var wahr = f.Antworten.FirstOrDefault(a => a.Text == "Wahr")?.Richtig ?? true;
                var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
                foreach (var (id, label, val) in new[] { ("wahr", "Wahr", true), ("falsch", "Falsch", false) })
                {
                    var v = val; var b = new Button { Name = $"{n}-{id}", Content = (wahr == val ? "◉ " : "○ ") + label + (wahr == val ? " ist richtig" : ""), Padding = new Thickness(12, 4), Background = Brushes.Transparent, Foreground = wahr == val ? K4Good : Text, BorderBrush = wahr == val ? K4Good : WkLine, BorderThickness = new Thickness(1) };
                    b.Click += (_, _) => ProgramRun(QuizKey, () => Set("Wahr/Falsch", x => x.Antworten = QuizKit.WahrFalsch(v)));
                    row.Children.Add(b);
                }
                sp.Children.Add(row);
                break;
            }
            case "luecke":
            {
                var gaps = QuizKit.Luecken(f.Text);
                sp.Children.Add(P(gaps.Count == 0 ? "Noch keine Lücke – setz das gesuchte Wort in [eckige Klammern]." : $"So sieht es aus: {QuizKit.LueckenText(f.Text)}  ·  {gaps.Count} Lücke(n): {string.Join(", ", gaps.Select(g => string.Join(" / ", g)))}", gaps.Count == 0 ? K4Warn : Muted, 12).Also(t => t.Name = n + "-gaps"));
                break;
            }
            case "zuordnen":
                for (var k = 0; k < f.Paare.Count; k++)
                {
                    var kk = k; var pr = f.Paare[k];
                    sp.Children.Add(new Grid { ColumnDefinitions = new ColumnDefinitions("*,24,*,6,Auto"), Children = {
                        BueroBox($"{n}-p-{k}-l", pr.Links, "Begriff", v => Set("Begriff", x => { if (kk < x.Paare.Count) x.Paare[kk].Links = v; })),
                        Pos(new TextBlock { Text = "→", Foreground = WkGold, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }, 1),
                        Pos(BueroBox($"{n}-p-{k}-r", pr.Rechts, "Gehört dazu", v => Set("Zuordnung", x => { if (kk < x.Paare.Count) x.Paare[kk].Rechts = v; })), 2),
                        Pos(WkBtn($"{n}-p-{k}-del", "✕", QuizKey, () => Set("Paar gelöscht", x => { if (x.Paare.Count > 2) x.Paare.RemoveAt(kk); else throw new InvalidOperationException("Es braucht mindestens zwei Paare."); }), "Paar löschen"), 4) } });
                }
                sp.Children.Add(WkBtn(n + "-p-add", "＋ Paar", QuizKey, () => Set("Paar", x => { if (x.Paare.Count >= 12) throw new InvalidOperationException("Höchstens 12 Paare je Frage."); x.Paare.Add(new QuizPaar { Links = "Begriff", Rechts = $"Bedeutung {x.Paare.Count + 1}" }); })));
                break;
        }
        sp.Children.Add(BueroBox(n + "-erkl", f.Erklaerung, "Erklärung nach der Antwort (freiwillig)", v => Set("Erklärung", x => x.Erklaerung = v)));
        var card = WkCard(sp, n, on);
        card.PointerPressed += (_, e) => { if (QuizSel(d) != ii) { d.Data["sel"] = ii; if (e.Source is not TextBox) Show(QuizKey); } };
        card.ContextMenu = QuizMenu(d, ii, q.Fragen.Count);
        return card;
    }

    /// <summary>Rechtsklick auf eine Frage.</summary>
    ContextMenu QuizMenu(ProgramDocument d, int i, int count)
    {
        var m = new ContextMenu { Name = $"quiz-menu-{i}" };
        void I(string id, string header, Action go, bool on = true, string gesture = "")
        {
            var it = new MenuItem { Name = $"quiz-menu-{i}-{id}", Header = header, IsEnabled = on, InputGesture = gesture.Length > 0 ? KeyGesture.Parse(gesture) : null };
            it.Click += (_, _) => ProgramRun(QuizKey, () => { d.Data["sel"] = i; go(); }); m.Items.Add(it);
        }
        I("dup", "Duplizieren", () => QuizDuplicate(d), gesture: "Ctrl+D");
        I("up", "Nach oben", () => QuizMove(d, -1), i > 0);
        I("down", "Nach unten", () => QuizMove(d, 1), i < count - 1);
        var typ = new MenuItem { Name = $"quiz-menu-{i}-typ", Header = "Art ändern" };
        foreach (var (id, name, _) in QuizKit.Typen) { var tid = id; var it = new MenuItem { Name = $"quiz-menu-{i}-typ-{id}", Header = name }; it.Click += (_, _) => ProgramRun(QuizKey, () => QuizSetType(d, i, tid)); typ.Items.Add(it); }
        m.Items.Add(typ);
        m.Items.Add(new Separator());
        I("del", "Löschen", () => QuizDelete(d, i));
        return m;
    }

    Control QuizProperties(ProgramDocument d)
    {
        var q = QuizOf(d);
        var sp = new StackPanel { Name = "quiz-props", Spacing = 6 };
        void Set(string label, Action<Quiz> a) => QuizChange(d, label, a);
        sp.Children.Add(H("Quiz", 13));
        sp.Children.Add(BueroLabeled("Titel", BueroBox("quiz-title", q.Titel, "Name des Quiz", v => Set("Titel", x => x.Titel = v))));
        sp.Children.Add(BueroLabeled("Beschreibung", BueroBox("quiz-desc", q.Beschreibung, "Worum geht es? (steht auf der Startseite)", v => Set("Beschreibung", x => x.Beschreibung = v), true)));
        sp.Children.Add(BueroLabeled("Zeit je Frage", BueroCombo(QuizKey, "quiz-time", [("0", "Ohne Zeitlimit"), ("10", "10 Sekunden"), ("20", "20 Sekunden"), ("30", "30 Sekunden"), ("60", "1 Minute"), ("120", "2 Minuten")], q.Sekunden.ToString(CultureInfo.InvariantCulture), v => Set("Zeit je Frage", x => x.Sekunden = int.Parse(v, CultureInfo.InvariantCulture)), 160)));
        sp.Children.Add(BueroLabeled("Bestanden ab (%)", BueroBox("quiz-pass", q.Bestehen.ToString(CultureInfo.InvariantCulture), "0 bis 100", v => { if (int.TryParse(v, out var p) && p is >= 0 and <= 100) Set("Bestanden ab", x => x.Bestehen = p); }, false, 60)));
        CheckBox Check(string name, string label, bool value, Action<Quiz, bool> set)
        {
            var c = new CheckBox { Name = name, Content = label, IsChecked = value, Foreground = Text, FontSize = 12 };
            c.IsCheckedChanged += (_, _) => { if ((c.IsChecked == true) != value) ProgramRun(QuizKey, () => Set(label, x => set(x, c.IsChecked == true))); };
            return c;
        }
        sp.Children.Add(Check("quiz-shuffle", "Fragen mischen", q.FragenMischen, (x, v) => x.FragenMischen = v));
        sp.Children.Add(Check("quiz-shuffle-a", "Antworten mischen", q.AntwortenMischen, (x, v) => x.AntwortenMischen = v));
        sp.Children.Add(Check("quiz-bonus", "Tempo-Bonus (schnell = mehr Punkte)", q.TempoBonus, (x, v) => x.TempoBonus = v));
        if (QuizSel(d) is var s and >= 0)
        {
            var f = q.Fragen[s];
            sp.Children.Add(H($"Frage {s + 1}", 13));
            sp.Children.Add(BueroLabeled("Eigene Zeit (Sekunden, 0 = wie das Quiz)", BueroBox("quiz-q-time", f.Sekunden.ToString(CultureInfo.InvariantCulture), "0", v => { if (int.TryParse(v, out var t) && t is >= 0 and <= 3600) QuizChange(d, "Zeit der Frage", x => x.Fragen[s].Sekunden = t); }, false, 60)));
        }
        sp.Children.Add(H("Text für die KI", 13));
        sp.Children.Add(BueroBox("quiz-source-text", d.Text("quelle"), "Text hier einfügen – dann z. B. „Mach 5 Fragen aus dem Text“ in die Auftragszeile schreiben", v => QuizSetSource(d, v), true));
        var decks = Learn.Decks;
        if (decks.Count > 0)
        {
            sp.Children.Add(H("Karteikarten", 13));
            sp.Children.Add(BueroCombo(QuizKey, "quiz-deck", decks.Select(x => (x, x)), d.Text("stapel", decks[0]), v => d.Data["stapel"] = v, 180));
        }
        sp.Children.Add(H("Prüfung", 13));
        sp.Children.Add(BueroFindings("quiz-findings", QuizKit.Pruefen(q), "Bereit zum Spielen und Weitergeben."));
        return sp;
    }

    // ======================================================================
    // Kurse erstellen
    // ======================================================================

    public static Kurs CourseOf(ProgramDocument d) => Kurs.FromJson(d.Data["kurs"]);
    public void CourseChange(ProgramDocument d, string label, Action<Kurs> change)
    {
        var k = CourseOf(d); change(k); var j = k.ToJson();
        d.Edit(label, x => x["kurs"] = j);
    }
    static JsonObject CourseDoc(Kurs k) => new() { ["kurs"] = k.ToJson(), ["lek"] = KursKit.Lektionen(k).FirstOrDefault().Lektion?.Id ?? "", ["modus"] = "bearbeiten", ["wer"] = k.Lernende.FirstOrDefault()?.Id ?? "" };
    public static KursLektion? CourseLesson(ProgramDocument d) { var k = CourseOf(d); return KursKit.Lektion(k, d.Text("lek")) ?? KursKit.Lektionen(k).FirstOrDefault().Lektion; }
    public static bool CourseViewing(ProgramDocument d) => d.Text("modus") == "ansicht";

    static Kurs CourseBeispiel() => new()
    {
        Titel = "Gut präsentieren",
        Beschreibung = "In drei kurzen Lektionen zu einem sicheren Vortrag – mit Übungen und einem Quiz am Ende.",
        Kapitel =
        [
            new() { Titel = "Vorbereiten", Lektionen =
            [
                new() { Titel = "Das Ziel kennen", Minuten = 5, Bloecke = [new() { Art = "text", Text = "Ein guter Vortrag beginnt mit einer Frage: Was sollen die Zuhörenden danach wissen, fühlen oder tun? Schreib das Ziel in einem Satz auf." }, new() { Art = "liste", Text = "Wer hört zu?\nWas wissen sie schon?\nWas sollen sie mitnehmen?" }, new() { Art = "hinweis", Text = "Ein Satz, den du am Ende wiederholst, bleibt am besten hängen." }] },
                new() { Titel = "Aufbau in drei Teilen", Minuten = 6, Bloecke = [new() { Art = "ueberschrift", Text = "Anfang, Mitte, Schluss" }, new() { Art = "text", Text = "Am Anfang weckst du Neugier, in der Mitte bringst du höchstens drei Kernpunkte, am Schluss fasst du zusammen und sagst, was als Nächstes passiert." }] },
            ] },
            new() { Titel = "Vortragen", Lektionen =
            [
                new() { Titel = "Stimme und Haltung", Minuten = 5, Bloecke =
                [
                    new() { Art = "text", Text = "Sprich langsamer, als es dir natürlich vorkommt, und mach nach jedem Kernpunkt eine kurze Pause. Steh fest auf beiden Beinen und schau einzelne Personen an." },
                    new() { Art = "quiz", Quiz = new Quiz { Titel = "Kurz geprüft", FragenMischen = false, Fragen =
                    [
                        new() { Typ = "auswahl", Text = "Wie viele Kernpunkte gehören höchstens in die Mitte?", Antworten = [new() { Text = "Drei", Richtig = true }, new() { Text = "Fünf" }, new() { Text = "Zehn" }] },
                        new() { Typ = "wahrfalsch", Text = "Pausen nach einem Kernpunkt helfen beim Zuhören.", Antworten = QuizKit.WahrFalsch(true) },
                    ] } },
                ] },
            ] },
        ],
    };

    private void InitKurse() => RegisterProgram(FachTabs, new ProgramSpec(CourseKey, "STUDIO-COURSE-01", "Kurse erstellen")
    {
        Sentence = "Online-Kurse aus Kapiteln und Lektionen bauen – mit Texten, Bildern, Hinweisen und Quizzen aus „Quiz erstellen“, Fortschritt je Person und Ausgabe als Webseite oder SCORM-Paket für Lernplattformen.",
        Folder = "Kurse",
        ScrollWorkspace = true,
        GroupLevels = new() { ["Gliederung"] = 0, ["Inhalt"] = 0, ["Lernen"] = 0 },
        Templates =
        [
            new("leer", "Leerer Kurs", "Ein Kapitel mit einer leeren Lektion", () => CourseDoc(new Kurs { Kapitel = [new() { Titel = "Kapitel 1", Lektionen = [new() { Titel = "Lektion 1" }] }] })),
            new("beispiel", "Beispiel: Gut präsentieren", "Zwei Kapitel, drei Lektionen, ein Quiz", () => CourseDoc(CourseBeispiel())),
            new("malkurs", "Malkurs aus dem Künstlerstudio", "Die Schritte des Malkurses „Erste Schritte“ als Lektionen", () => CourseDoc(KursKit.AusMalkurs("erste-schritte"))),
        ],
        Ribbon =
        [
            ("Gliederung", [
                new ProgramCmd("cs-add-lesson", "＋ Lektion", d => CourseAddLesson(d), Primary: true, Tip: "Neue Lektion nach der gewählten."),
                new ProgramCmd("cs-add-chapter", "＋ Kapitel", d => CourseAddChapter(d), Tip: "Neues Kapitel am Ende."),
                new ProgramCmd("cs-lesson-up", "Lektion nach oben", d => CourseMoveLesson(d, -1), Level: 1),
                new ProgramCmd("cs-lesson-down", "Lektion nach unten", d => CourseMoveLesson(d, 1), Level: 1),
                new ProgramCmd("cs-lesson-del", "Lektion löschen", d => CourseDeleteLesson(d), Level: 1),
            ]),
            ("Inhalt", [
                new ProgramCmd("cs-add-text", "＋ Text", d => CourseAddBlock(d, "text"), Tip: "Absatz in die gewählte Lektion."),
                new ProgramCmd("cs-add-head", "＋ Überschrift", d => CourseAddBlock(d, "ueberschrift")),
                new ProgramCmd("cs-add-list", "＋ Liste", d => CourseAddBlock(d, "liste"), Tip: "Eine Zeile je Punkt."),
                new ProgramCmd("cs-add-note", "＋ Hinweis", d => CourseAddBlock(d, "hinweis"), Tip: "Hervorgehobener Merksatz."),
                new ProgramCmd("cs-open-image", "Bild …", d => _ = CoursePickImage(d), Tip: "Foto oder Grafik (PNG, JPG) – wird beim Export eingebettet."),
                new ProgramCmd("cs-open-quiz", "Quiz einbinden …", d => _ = CoursePickQuiz(d), Tip: "Ein Quiz aus „Quiz erstellen“ (Datei .tdoc) oder Moodle-GIFT."),
                new ProgramCmd("cs-add-openquiz", "Offenes Quiz einbinden", d => CourseAddOpenQuiz(d), Tip: "Das Quiz, das gerade in „Quiz erstellen“ offen ist.", Level: 1),
            ]),
            ("Lernen", [
                new ProgramCmd("cs-view", "Kursansicht", d => CourseToggleView(d), Tip: "Den Kurs so durcharbeiten, wie ihn Lernende sehen (F5)."),
                new ProgramCmd("cs-check", "Prüfen", d => CourseCheckNow(d), Tip: "Leere Lektionen, fehlende Bilder, Quiz mit Fehlern?"),
            ]),
        ],
        Menus = d =>
        [
            new("Kurs",
            [
                new("cs-key-lesson", "Neue Lektion", () => ProgramRun(CourseKey, () => CourseAddLesson(d)), "Strg+Umschalt+N"),
                new("cs-key-text", "Neuer Text-Baustein", () => ProgramRun(CourseKey, () => CourseAddBlock(d, "text")), "Strg+Umschalt+T"),
                new("cs-key-view", "Kursansicht ein/aus", () => ProgramRun(CourseKey, () => CourseToggleView(d)), "F5"),
            ]),
        ],
        KiActions = [CourseKiLesson],
        KiHint = "Neue Lektionen schreibst du NUR mit EINEM Schritt „kurs.lektion“ – nie mit „cs-add-lesson“ oder anderen Menübefehlen: „titel“ ist der Titel, „inhalt“ der fertige Lerntext – Absätze durch Leerzeilen getrennt, „## “ am Zeilenanfang für eine Überschrift, "
               + "„- “ für Listenpunkte, „! “ für einen Merksatz. Optional „kapitel“ = Name des Kapitels (sonst das Kapitel der gewählten Lektion). Schreib verständlich, in kurzen Sätzen, erfinde keine Fakten.",
        Exports =
        [
            new("html", "Kurs als Webseite (HTML)", ".html", (d, f) => { KursHtml.Write(CourseReady(d), f); return f; }),
            new("scorm", "SCORM-Paket für Lernplattformen (ZIP)", ".zip", (d, f) => { KursHtml.WriteScorm(CourseReady(d), f); return f; }),
            new("csv", "Fortschritt der Lernenden (CSV)", ".csv", (d, f) => { AtomicFile.WriteAllBytes(f, [.. new UTF8Encoding(true).GetPreamble(), .. Encoding.UTF8.GetBytes(KursKit.Csv(CourseOf(d)))]); return f; }),
        ],
        SimpleExports = 2,
        FirstStep = new("Ersten Text schreiben", "Schreib den ersten Absatz deiner Lektion – Bilder, Listen und ein Quiz kommen danach dazu.", d => ProgramRun(CourseKey, () => CourseAddBlock(d, "text")),
            d => KursKit.Lektionen(CourseOf(d)).All(x => x.Lektion.Bloecke.Count == 0)),
        Workspace = CourseWorkspace,
        Properties = CourseProperties,
        Status = d =>
        {
            var k = CourseOf(d); var all = KursKit.Lektionen(k);
            return $"{k.Kapitel.Count} Kapitel · {all.Count} Lektionen · etwa {KursKit.Minuten(k)} Minuten · {all.Sum(x => KursKit.Quizze(x.Lektion).Count())} Quiz · {k.Lernende.Count} Lernende" + (CourseViewing(d) ? " · Kursansicht" : "");
        },
        Help = "Links die Gliederung: Kapitel und Lektionen anklicken, mit Rechtsklick verschieben oder löschen. Rechts daneben die gewählte Lektion aus Bausteinen – Text, Überschrift, Liste, Hinweis, Bild und Quiz. "
             + "Bilder und Quiz-Dateien kannst du auf die Lektion ziehen. „Kursansicht“ (F5) zeigt den Kurs wie für Lernende: Person wählen, Lektion lesen, Quiz spielen, „Lektion abschließen“. "
             + "Ausgabe: Webseite (eine HTML-Datei, läuft ohne Internet; Fortschritt bleibt im Browser) oder SCORM-Paket (ZIP) zum Hochladen in Moodle, ILIAS und andere Lernplattformen.",
    }, "kurs", "kurse", "online-kurs", "e-learning", "lektion", "lernmodul", "schulung", "scorm", "moodle", "lernplattform", "unterricht", "fortbildung", "rise", "teachable");

    static Kurs CourseReady(ProgramDocument d)
    {
        var k = CourseOf(d);
        if (KursKit.Pruefen(k).FirstOrDefault(x => x.Fehler) is { } e) throw new InvalidOperationException($"Vor der Ausgabe: {e.Feld} – {e.Text}");
        return k;
    }

    (int Chapter, int Lesson) CourseWhere(Kurs k, string lessonId)
    {
        for (var c = 0; c < k.Kapitel.Count; c++) { var i = k.Kapitel[c].Lektionen.FindIndex(l => l.Id == lessonId); if (i >= 0) return (c, i); }
        return (-1, -1);
    }

    public string CourseAddLesson(ProgramDocument d, int? chapter = null)
    {
        var id = ""; var lek = d.Text("lek");
        CourseChange(d, "Lektion", k =>
        {
            if (k.Kapitel.Count == 0) k.Kapitel.Add(new KursKapitel { Titel = "Kapitel 1" });
            var (c, i) = CourseWhere(k, lek);
            if (chapter is { } ch && ch >= 0 && ch < k.Kapitel.Count) { c = ch; i = k.Kapitel[ch].Lektionen.Count - 1; }
            if (c < 0) { c = k.Kapitel.Count - 1; i = k.Kapitel[c].Lektionen.Count - 1; }
            var l = new KursLektion { Titel = $"Lektion {KursKit.Lektionen(k).Count + 1}" }; id = l.Id;
            k.Kapitel[c].Lektionen.Insert(i + 1, l);
        });
        d.Data["lek"] = id;
        _progMsg[CourseKey] = "Neue Lektion – Titel oben eintragen, dann Bausteine hinzufügen.";
        return id;
    }

    public void CourseAddChapter(ProgramDocument d)
    {
        var id = "";
        CourseChange(d, "Kapitel", k => { var l = new KursLektion { Titel = "Lektion 1" }; id = l.Id; k.Kapitel.Add(new KursKapitel { Titel = $"Kapitel {k.Kapitel.Count + 1}", Lektionen = [l] }); });
        d.Data["lek"] = id;
    }

    public void CourseMoveLesson(ProgramDocument d, int by)
    {
        var lek = d.Text("lek");
        CourseChange(d, by < 0 ? "Lektion nach oben" : "Lektion nach unten", k =>
        {
            var all = KursKit.Lektionen(k); var i = all.FindIndex(x => x.Lektion.Id == lek); var j = i + by;
            if (i < 0 || j < 0 || j >= all.Count) return;
            var (c, li) = CourseWhere(k, lek); var l = k.Kapitel[c].Lektionen[li];
            var (tc, tl) = CourseWhere(k, all[j].Lektion.Id);
            if (tc == c) { k.Kapitel[c].Lektionen.RemoveAt(li); k.Kapitel[c].Lektionen.Insert(tl, l); }
            else { k.Kapitel[c].Lektionen.RemoveAt(li); k.Kapitel[tc].Lektionen.Insert(by < 0 ? k.Kapitel[tc].Lektionen.Count : 0, l); } // über die Kapitelgrenze: ans Ende bzw. an den Anfang des Nachbarkapitels
        });
    }

    public void CourseDeleteLesson(ProgramDocument d, string? id = null)
    {
        var lek = id ?? d.Text("lek"); var next = "";
        CourseChange(d, "Lektion gelöscht", k =>
        {
            var (c, i) = CourseWhere(k, lek); if (c < 0) throw new InvalidOperationException("Es ist keine Lektion gewählt.");
            k.Kapitel[c].Lektionen.RemoveAt(i);
            next = KursKit.Lektionen(k).FirstOrDefault().Lektion?.Id ?? "";
        });
        d.Data["lek"] = next;
        _progMsg[CourseKey] = "Lektion gelöscht – „Rückgängig“ (Strg+Z) holt sie zurück.";
    }

    public void CourseDeleteChapter(ProgramDocument d, int c)
    {
        CourseChange(d, "Kapitel gelöscht", k => { if (c >= 0 && c < k.Kapitel.Count) k.Kapitel.RemoveAt(c); });
        d.Data["lek"] = KursKit.Lektionen(CourseOf(d)).FirstOrDefault().Lektion?.Id ?? "";
    }

    void CourseInLesson(ProgramDocument d, string label, Action<KursLektion> a)
    {
        var lek = CourseLesson(d)?.Id ?? throw new InvalidOperationException("Es gibt noch keine Lektion – erst „＋ Lektion“.");
        CourseChange(d, label, k => a(KursKit.Lektion(k, lek)!));
        d.Data["lek"] = lek;
    }

    public void CourseAddBlock(ProgramDocument d, string art, string text = "")
    {
        var t = text.Length > 0 ? text : art switch { "ueberschrift" => "Überschrift", "liste" => "Erster Punkt\nZweiter Punkt", "hinweis" => "Merksatz", _ => "" };
        CourseInLesson(d, KursKit.ArtName(art), l => l.Bloecke.Add(new KursBlock { Art = art, Text = t }));
        if (CourseViewing(d)) d.Data["modus"] = "bearbeiten";
    }

    public void CourseAddImage(ProgramDocument d, string file)
    {
        if (!KreativSafe.LocalFile(file, KreativSafe.MaxImageBytes)) throw new InvalidOperationException("Bitte ein Bild auf diesem Rechner wählen (höchstens 25 MB).");
        if (Path.GetExtension(file).ToLowerInvariant() is not (".png" or ".jpg" or ".jpeg" or ".gif" or ".webp")) throw new InvalidDataException("Als Bild gehen PNG, JPG, GIF und WEBP.");
        CourseInLesson(d, "Bild", l => l.Bloecke.Add(new KursBlock { Art = "bild", Bild = Path.GetFullPath(file), Text = "" }));
        _progMsg[CourseKey] = $"Bild „{Path.GetFileName(file)}“ eingefügt – darunter kannst du eine Bildunterschrift schreiben.";
    }

    /// <summary>Quiz aus einer Datei einbinden: Dokument von „Quiz erstellen“ (.tdoc) oder GIFT/Fragen-Text.</summary>
    public void CourseAddQuizFile(ProgramDocument d, string file)
    {
        if (!File.Exists(file)) throw new FileNotFoundException("Die Datei gibt es nicht.", file);
        Quiz q;
        if (file.EndsWith(ProgramDocument.Extension, StringComparison.OrdinalIgnoreCase))
        {
            var doc = ProgramDocument.Load(file);
            if (doc.Studio != "STUDIO-QUIZ-01" || doc.Data["quiz"] is null) throw new InvalidDataException("Das ist kein Quiz aus „Quiz erstellen“.");
            q = QuizOf(doc);
        }
        else
        {
            var text = File.ReadAllText(file);
            if (text.Contains('{')) q = QuizGift.Read(text, Path.GetFileNameWithoutExtension(file)).Quiz;
            else { var (f, _) = QuizText.Parse(text); q = new Quiz { Titel = Path.GetFileNameWithoutExtension(file), Fragen = f }; }
        }
        CourseAddQuiz(d, q);
    }

    public void CourseAddQuiz(ProgramDocument d, Quiz q)
    {
        if (q.Fragen.Count == 0) throw new InvalidDataException("Das Quiz hat keine Frage.");
        if (QuizKit.Pruefen(q).FirstOrDefault(x => x.Fehler) is { } e) throw new InvalidDataException($"Das Quiz „{q.Titel}“ hat noch einen Fehler: {e.Feld} – {e.Text}");
        CourseInLesson(d, "Quiz", l => l.Bloecke.Add(new KursBlock { Art = "quiz", Quiz = q.Copy() }));
        _progMsg[CourseKey] = $"Quiz „{q.Titel}“ ({q.Fragen.Count} Fragen) eingebunden – in der Kursansicht wird die Lektion erst nach bestandenem Quiz abgeschlossen.";
    }

    public void CourseAddOpenQuiz(ProgramDocument d)
    {
        if (ProgramActive(QuizKey) is not { } qd) throw new InvalidOperationException("In „Quiz erstellen“ ist gerade kein Quiz offen.");
        CourseAddQuiz(d, QuizOf(qd));
    }

    async Task CoursePickImage(ProgramDocument d)
    {
        var f = await PickMediaFile("cs-image", "Bild für die Lektion", ["*.png", "*.jpg", "*.jpeg", "*.gif", "*.webp"]);
        if (f is not null) ProgramRun(CourseKey, () => CourseAddImage(d, f));
    }
    async Task CoursePickQuiz(ProgramDocument d)
    {
        var f = await PickMediaFile("cs-quiz", "Quiz einbinden", ["*" + ProgramDocument.Extension, "*.txt", "*.gift"]);
        if (f is not null) ProgramRun(CourseKey, () => CourseAddQuizFile(d, f));
    }

    public void CourseBlockMove(ProgramDocument d, int i, int by) => CourseInLesson(d, by < 0 ? "Baustein nach oben" : "Baustein nach unten", l => { var j = i + by; if (j >= 0 && j < l.Bloecke.Count) (l.Bloecke[i], l.Bloecke[j]) = (l.Bloecke[j], l.Bloecke[i]); });
    public void CourseBlockDelete(ProgramDocument d, int i) => CourseInLesson(d, "Baustein gelöscht", l => { if (i >= 0 && i < l.Bloecke.Count) l.Bloecke.RemoveAt(i); });

    public void CourseToggleView(ProgramDocument d)
    {
        var view = !CourseViewing(d);
        if (view && CourseOf(d).Lernende.Count == 0) CourseChange(d, "Lernende(r)", k => k.Lernende.Add(new KursLernender { Name = "Ich (Probe)" }));
        d.Data["modus"] = view ? "ansicht" : "bearbeiten";
        if (view && CourseLearner(d) is null) d.Data["wer"] = CourseOf(d).Lernende[0].Id;
        foreach (var key in _quizPlay.Keys.Where(x => x.StartsWith(d.Id + ":", StringComparison.Ordinal)).ToList()) { QuizStopTimer(key); _quizPlay.Remove(key); }
        _progMsg[CourseKey] = view ? "Kursansicht: so sehen Lernende den Kurs. Der Fortschritt wird bei der gewählten Person gespeichert." : "";
    }

    public KursLernender? CourseLearner(ProgramDocument d) { var k = CourseOf(d); return k.Lernende.FirstOrDefault(p => p.Id == d.Text("wer")) ?? k.Lernende.FirstOrDefault(); }

    public void CourseAddLearner(ProgramDocument d, string name)
    {
        var n = (name ?? "").Trim(); if (n.Length == 0) throw new ArgumentException("Bitte einen Namen eintragen.");
        if (CourseOf(d).Lernende.Any(p => p.Name.Equals(n, StringComparison.OrdinalIgnoreCase))) throw new InvalidOperationException($"„{n}“ steht schon auf der Liste.");
        var id = "";
        CourseChange(d, "Lernende(r)", k => { var p = new KursLernender { Name = n }; id = p.Id; k.Lernende.Add(p); });
        d.Data["wer"] = id;
        _progMsg[CourseKey] = $"„{n}“ eingetragen – in der Kursansicht auswählen und loslegen.";
    }

    public void CourseFinishLesson(ProgramDocument d)
    {
        var p = CourseLearner(d) ?? throw new InvalidOperationException("Erst eine lernende Person eintragen.");
        var l = CourseLesson(d) ?? throw new InvalidOperationException("Es gibt keine Lektion.");
        var pid = p.Id; var lid = l.Id;
        CourseChange(d, "Lektion abgeschlossen", k => KursKit.Abschliessen(k, k.Lernende.First(x => x.Id == pid), lid));
        var k2 = CourseOf(d); var all = KursKit.Lektionen(k2); var i = all.FindIndex(x => x.Lektion.Id == lid);
        var st = KursKit.Fortschritt(k2, k2.Lernende.First(x => x.Id == pid));
        if (i + 1 < all.Count) d.Data["lek"] = all[i + 1].Lektion.Id;
        _progMsg[CourseKey] = st.Fertig ? $"✓ Kurs abgeschlossen – {st.Erledigt} von {st.Gesamt} Lektionen" + (st.QuizSchnitt is { } s ? $", Quiz im Schnitt {s} %." : ".") : $"Lektion erledigt – {st.Prozent} % des Kurses geschafft.";
    }

    public List<CvFinding> CourseCheckNow(ProgramDocument d)
    {
        var f = KursKit.Pruefen(CourseOf(d)); var err = f.Count(x => x.Fehler);
        _progMsg[CourseKey] = f.Count == 0 ? "✓ Der Kurs ist bereit zur Ausgabe." : $"Prüfung: {err} Fehler, {f.Count - err} Hinweise – rechts unter „Prüfung“.";
        return f;
    }

    /// <summary>KI-Fachaktion „Lektion schreiben“: neue Lektion aus dem Text der KI (Absätze, Überschriften, Listen, Merksätze) – Vorschau, „Ja, behalten“, EIN Rückgängig-Schritt.</summary>
    ProgramKiAction CourseKiLesson => new(new AssistAction("kurs.lektion", "Lektion schreiben", ["lektion", "kurs", "schreiben", "erstellen", "erklaeren", "lerntext", "kapitel", "inhalt", "neue"],
        [new AssistParam("titel", "Titel der Lektion", Words: ["titel"]), new AssistParam("inhalt", "Lerntext"), new AssistParam("kapitel", "Kapitel", Required: false, Words: ["kapitel"])]),
        (d, a) =>
        {
            var blocks = KursKit.AusText(a.GetValueOrDefault("inhalt") ?? "");
            if (blocks.Count == 0) throw new InvalidOperationException("Die KI hat keinen Lerntext geliefert.");
            var title = (a.GetValueOrDefault("titel") ?? "").Trim(); if (title.Length == 0) title = "Neue Lektion"; if (title.Length > 120) title = title[..120];
            var chap = (a.GetValueOrDefault("kapitel") ?? "").Trim(); var lek = d.Text("lek"); var id = "";
            CourseChange(d, "Lektion: " + title, k =>
            {
                var l = new KursLektion { Titel = title, Minuten = Math.Clamp(blocks.Sum(b => b.Text.Split(' ').Length) / 150 + 2, 2, 60), Bloecke = blocks }; id = l.Id;
                var c = chap.Length > 0 ? k.Kapitel.FindIndex(x => x.Titel.Equals(chap, StringComparison.OrdinalIgnoreCase)) : -1;
                if (c < 0 && chap.Length > 0) { k.Kapitel.Add(new KursKapitel { Titel = chap.Length > 120 ? chap[..120] : chap }); c = k.Kapitel.Count - 1; }
                if (c < 0) { (c, _) = CourseWhere(k, lek); if (c < 0) { if (k.Kapitel.Count == 0) k.Kapitel.Add(new KursKapitel { Titel = "Kapitel 1" }); c = k.Kapitel.Count - 1; } }
                k.Kapitel[c].Lektionen.Add(l);
            });
            d.Data["lek"] = id; d.Data["modus"] = "bearbeiten";
        },
        (_, _) => []) { MakesFile = false };

    // ---------- Oberfläche ----------

    static Bitmap? CourseBitmap(string file)
    {
        if (file.Length == 0 || !File.Exists(file)) return null;
        try { return new Bitmap(file); } catch (Exception e) when (e is IOException or ArgumentException or InvalidOperationException or NotSupportedException) { return null; }
    }

    Control CourseOutline(ProgramDocument d, Kurs k, bool viewing, KursLernender? who)
    {
        var sp = new StackPanel { Name = "cs-outline", Spacing = 3 };
        var cur = CourseLesson(d)?.Id;
        if (viewing && who is not null)
        {
            var st = KursKit.Fortschritt(k, who);
            sp.Children.Add(new TextBlock { Name = "cs-progress", Text = $"{who.Name}: {st.Erledigt} von {st.Gesamt} Lektionen ({st.Prozent} %)" + (st.QuizSchnitt is { } s ? $" · Quiz {s} %" : ""), Foreground = Text, FontSize = 12, TextWrapping = TextWrapping.Wrap });
            sp.Children.Add(new ProgressBar { Minimum = 0, Maximum = 100, Value = st.Prozent, Height = 6, Foreground = WkGold, Margin = new Thickness(0, 2, 0, 6) });
        }
        for (var c = 0; c < k.Kapitel.Count; c++)
        {
            var cap = k.Kapitel[c]; var cc = c;
            var title = new TextBlock { Name = $"cs-k-{c}", Text = cap.Titel, FontSize = 12, FontWeight = FontWeight.SemiBold, Foreground = Muted, Margin = new Thickness(0, 8, 0, 2) };
            if (!viewing)
            {
                var m = new ContextMenu { Name = $"cs-k-{c}-menu" };
                void CI(string id, string header, Action go) { var it = new MenuItem { Name = $"cs-k-{cc}-menu-{id}", Header = header }; it.Click += (_, _) => ProgramRun(CourseKey, go); m.Items.Add(it); }
                CI("add", "Lektion in diesem Kapitel", () => CourseAddLesson(d, cc));
                CI("del", "Kapitel löschen", () => CourseDeleteChapter(d, cc));
                title.ContextMenu = m;
            }
            sp.Children.Add(title);
            for (var i = 0; i < cap.Lektionen.Count; i++)
            {
                var l = cap.Lektionen[i]; var lid = l.Id;
                var done = who?.Erledigt.Contains(l.Id) == true; var open = who is null || KursKit.Offen(k, who, l.Id);
                var b = new Button { Name = $"cs-l-{c}-{i}", HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left, Padding = new Thickness(8, 5),
                    Background = l.Id == cur ? Brush.Parse("#3A2F1A") : Brushes.Transparent, BorderBrush = l.Id == cur ? WkGold : WkLine, BorderThickness = new Thickness(1), Opacity = viewing && !open ? 0.5 : 1,
                    Content = new TextBlock { Text = (viewing ? (done ? "✓ " : open ? "" : "🔒 ") : "") + l.Titel + (KursKit.Quizze(l).Any() ? "  · Quiz" : ""), FontSize = 12.5, Foreground = done ? K4Good : Text, TextWrapping = TextWrapping.Wrap } };
                b.Click += (_, _) => { if (viewing && !open) { _progMsg[CourseKey] = "Diese Lektion öffnet sich, wenn die vorige abgeschlossen ist."; Show(CourseKey); return; } d.Data["lek"] = lid; Show(CourseKey); };
                if (!viewing)
                {
                    var m = new ContextMenu { Name = $"cs-l-{c}-{i}-menu" };
                    void LI(string id, string header, Action go) { var it = new MenuItem { Name = $"cs-l-{cc}-{i}-menu-{id}", Header = header }; it.Click += (_, _) => ProgramRun(CourseKey, () => { d.Data["lek"] = lid; go(); }); m.Items.Add(it); }
                    LI("up", "Nach oben", () => CourseMoveLesson(d, -1)); LI("down", "Nach unten", () => CourseMoveLesson(d, 1));
                    LI("text", "Text-Baustein hinzufügen", () => CourseAddBlock(d, "text"));
                    m.Items.Add(new Separator()); LI("del", "Lektion löschen", () => CourseDeleteLesson(d, lid));
                    b.ContextMenu = m;
                }
                sp.Children.Add(b);
            }
            if (!viewing) sp.Children.Add(WkBtn($"cs-k-{c}-add", "＋ Lektion", CourseKey, () => CourseAddLesson(d, cc)));
        }
        return sp;
    }

    Control CourseWorkspace(ProgramDocument d)
    {
        var k = CourseOf(d); var viewing = CourseViewing(d); var who = viewing ? CourseLearner(d) : null;
        var grid = new Grid { Name = "cs-root", ColumnDefinitions = new ColumnDefinitions("250,14,*"), Margin = new Thickness(4, 0, 10, 4) };
        var nav = new Border { Name = "cs-nav", Padding = new Thickness(10), CornerRadius = new CornerRadius(12), Background = WkCardBg, BorderBrush = WkLine, BorderThickness = new Thickness(1), VerticalAlignment = VerticalAlignment.Top, Child = CourseOutline(d, k, viewing, who) };
        grid.Children.Add(nav);
        var main = new StackPanel { Name = viewing ? "cs-view-main" : "cs-editor", Spacing = 0, Background = Brushes.Transparent, MinHeight = 400 }; // durchgehend Ablagefläche
        var l = CourseLesson(d);
        if (l is null) main.Children.Add(P("Noch keine Lektion – oben „＋ Lektion“.", Muted, 13));
        else if (viewing) CourseViewLesson(d, k, l, who, main);
        else CourseEditLesson(d, k, l, main);
        grid.Children.Add(Pos(main, 2));
        if (!viewing)
            BueroFileDrop(main, CourseKey, BueroExt(".png", ".jpg", ".jpeg", ".gif", ".webp", ProgramDocument.Extension, ".gift", ".txt", ".md"), "Bilder, Quiz-Dateien (.tdoc, GIFT) oder Texte (.txt, .md)", (dd, files) =>
            {
                foreach (var f in files)
                {
                    var ext = Path.GetExtension(f).ToLowerInvariant();
                    if (ext is ".png" or ".jpg" or ".jpeg" or ".gif" or ".webp") CourseAddImage(dd, f);
                    else if (ext is ".md" || ext is ".txt" && QuizText.Parse(File.ReadAllText(f)).Fragen.Count == 0 && !File.ReadAllText(f).Contains('{'))
                    { var blocks = KursKit.AusText(File.ReadAllText(f)); if (blocks.Count == 0) throw new InvalidDataException("In der Datei steht kein Text."); CourseInLesson(dd, "Text aus Datei", x => x.Bloecke.AddRange(blocks)); }
                    else CourseAddQuizFile(dd, f);
                }
            });
        return grid;
    }

    void CourseEditLesson(ProgramDocument d, Kurs k, KursLektion l, StackPanel main)
    {
        var lid = l.Id; var (ci, _) = CourseWhere(k, lid);
        void SetL(string label, Action<KursLektion> a) => CourseChange(d, label, x => { if (KursKit.Lektion(x, lid) is { } y) a(y); });
        var head = new Grid { ColumnDefinitions = new ColumnDefinitions("*,10,Auto,4,Auto") };
        head.Children.Add(BueroBox("cs-lek-title", l.Titel, "Titel der Lektion", v => SetL("Titel der Lektion", y => y.Titel = v)).Also(t => t.FontSize = 18));
        head.Children.Add(Pos(BueroBox("cs-lek-min", l.Minuten.ToString(CultureInfo.InvariantCulture), "Minuten", v => { if (int.TryParse(v, out var m) && m is >= 1 and <= 600) SetL("Dauer", y => y.Minuten = m); }, false, 50), 2));
        head.Children.Add(Pos(new TextBlock { Text = "Min.", Foreground = Muted, FontSize = 11, VerticalAlignment = VerticalAlignment.Center }, 4));
        main.Children.Add(new TextBlock { Text = ci >= 0 ? k.Kapitel[ci].Titel : "", Foreground = Muted, FontSize = 11, Margin = new Thickness(0, 0, 0, 2) });
        main.Children.Add(WkCard(head, "cs-lek-head"));
        for (var i = 0; i < l.Bloecke.Count; i++)
        {
            var b = l.Bloecke[i]; var ii = i; var n = $"cs-b-{i}";
            void SetB(string label, Action<KursBlock> a) => SetL(label, y => { if (ii < y.Bloecke.Count) a(y.Bloecke[ii]); });
            var sp = new StackPanel { Spacing = 6 };
            sp.Children.Add(new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Children = {
                new TextBlock { Text = KursKit.ArtName(b.Art), Foreground = WkGold, FontSize = 11, FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center },
                Pos(new StackPanel { Orientation = Orientation.Horizontal, Children = { WkBtn(n + "-up", "↑", CourseKey, () => CourseBlockMove(d, ii, -1), "Nach oben"), WkBtn(n + "-down", "↓", CourseKey, () => CourseBlockMove(d, ii, 1), "Nach unten"), WkBtn(n + "-del", "✕", CourseKey, () => CourseBlockDelete(d, ii), "Baustein löschen") } }, 1) } });
            switch (b.Art)
            {
                case "bild":
                    if (CourseBitmap(b.Bild) is { } bmp) sp.Children.Add(new Image { Name = n + "-img", Source = bmp, MaxHeight = 260, HorizontalAlignment = HorizontalAlignment.Left });
                    else sp.Children.Add(P("Bild fehlt: " + Path.GetFileName(b.Bild), K4Bad, 12));
                    sp.Children.Add(BueroBox(n + "-text", b.Text, "Bildunterschrift (freiwillig)", v => SetB("Bildunterschrift", y => y.Text = v)));
                    break;
                case "quiz" when b.Quiz is { } q:
                    sp.Children.Add(new TextBlock { Name = n + "-quiz", Text = $"{q.Titel} · {q.Fragen.Count} Fragen · {q.MaxPunkte:0.##} Punkte", Foreground = Text, FontSize = 14, FontWeight = FontWeight.SemiBold });
                    sp.Children.Add(P(string.Join(" · ", q.Fragen.Take(4).Select(f => QuizKit.TypName(f.Typ) + ": " + (f.Text.Length > 40 ? f.Text[..40] + "…" : f.Text))), Muted, 11));
                    sp.Children.Add(P($"In der Kursansicht zählt die Lektion als erledigt, wenn mindestens {k.Bestehen} % erreicht sind.", Muted, 11));
                    break;
                default:
                    sp.Children.Add(BueroBox(n + "-text", b.Text, b.Art switch { "liste" => "Eine Zeile je Punkt", "ueberschrift" => "Überschrift", "hinweis" => "Merksatz", _ => "Text des Absatzes" }, v => SetB(KursKit.ArtName(b.Art), y => y.Text = v), b.Art is "text" or "liste" or "hinweis")
                        .Also(t => { if (b.Art == "ueberschrift") { t.FontSize = 16; t.FontWeight = FontWeight.SemiBold; } }));
                    break;
            }
            var card = WkCard(sp, n);
            var cm = new ContextMenu { Name = n + "-menu" };
            void BI(string id, string header, Action go) { var it = new MenuItem { Name = $"{n}-menu-{id}", Header = header }; it.Click += (_, _) => ProgramRun(CourseKey, go); cm.Items.Add(it); }
            BI("up", "Nach oben", () => CourseBlockMove(d, ii, -1)); BI("down", "Nach unten", () => CourseBlockMove(d, ii, 1)); BI("del", "Löschen", () => CourseBlockDelete(d, ii));
            card.ContextMenu = cm;
            main.Children.Add(card);
        }
        var add = new WrapPanel { Name = "cs-add-row" };
        foreach (var (art, label) in new[] { ("text", "＋ Text"), ("ueberschrift", "＋ Überschrift"), ("liste", "＋ Liste"), ("hinweis", "＋ Hinweis") }) { var a = art; add.Children.Add(WkBtn("cs-add-" + art + "-end", label, CourseKey, () => CourseAddBlock(d, a))); }
        add.Children.Add(WkBtn("cs-add-image-end", "＋ Bild …", CourseKey, () => _ = CoursePickImage(d)));
        add.Children.Add(WkBtn("cs-add-quiz-end", "＋ Quiz …", CourseKey, () => _ = CoursePickQuiz(d)));
        main.Children.Add(add);
        if (l.Bloecke.Count == 0) main.Children.Add(P("Tipp: Bilder, Texte (.md, .txt) und Quiz-Dateien kannst du einfach hierher ziehen.", Muted, 12));
    }

    void CourseViewLesson(ProgramDocument d, Kurs k, KursLektion l, KursLernender? who, StackPanel main)
    {
        var pick = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(0, 0, 0, 8) };
        pick.Children.Add(new TextBlock { Text = "Ich bin", Foreground = Muted, VerticalAlignment = VerticalAlignment.Center, FontSize = 12 });
        pick.Children.Add(BueroCombo(CourseKey, "cs-who", k.Lernende.Select(p => (p.Id, p.Name)), who?.Id ?? "", v => d.Data["wer"] = v, 180));
        main.Children.Add(pick);
        if (who is null) { main.Children.Add(P("Rechts unter „Lernende“ eine Person eintragen.", Muted, 13)); return; }
        if (!KursKit.Offen(k, who, l.Id)) { main.Children.Add(WkCard(P("Diese Lektion öffnet sich, wenn die vorige abgeschlossen ist.", Muted, 13))); return; }
        var (ci, _) = CourseWhere(k, l.Id);
        var sp = new StackPanel { Spacing = 10 };
        sp.Children.Add(new TextBlock { Text = (ci >= 0 ? k.Kapitel[ci].Titel + " · " : "") + $"etwa {l.Minuten} Minuten", Foreground = Muted, FontSize = 12 });
        sp.Children.Add(new TextBlock { Name = "cs-view-title", Text = l.Titel, FontSize = 26, FontWeight = FontWeight.SemiBold, Foreground = Text, TextWrapping = TextWrapping.Wrap });
        foreach (var b in l.Bloecke)
        {
            switch (b.Art)
            {
                case "ueberschrift": sp.Children.Add(new TextBlock { Text = b.Text, FontSize = 19, FontWeight = FontWeight.SemiBold, Foreground = Text, TextWrapping = TextWrapping.Wrap }); break;
                case "liste":
                {
                    var ul = new StackPanel { Spacing = 3 };
                    foreach (var x in b.Text.Split('\n').Where(x => x.Trim().Length > 0)) ul.Children.Add(new TextBlock { Text = "•  " + x.Trim(), FontSize = 15, Foreground = Text, TextWrapping = TextWrapping.Wrap });
                    sp.Children.Add(ul); break;
                }
                case "hinweis": sp.Children.Add(new Border { BorderBrush = WkGold, BorderThickness = new Thickness(4, 0, 0, 0), Background = Brush.Parse("#231C12"), Padding = new Thickness(12, 8), CornerRadius = new CornerRadius(6), Child = new TextBlock { Text = b.Text, FontSize = 15, Foreground = Text, TextWrapping = TextWrapping.Wrap } }); break;
                case "bild":
                    if (CourseBitmap(b.Bild) is { } bmp) sp.Children.Add(new Image { Source = bmp, MaxHeight = 360, HorizontalAlignment = HorizontalAlignment.Left });
                    if (b.Text.Length > 0) sp.Children.Add(P(b.Text, Muted, 12));
                    break;
                case "quiz" when b.Quiz is { } q:
                {
                    var sid = d.Id + ":" + b.Id; var bid = b.Id; var pid = who.Id;
                    var best = who.Quiz.TryGetValue(b.Id, out var pct) ? pct : (int?)null;
                    var box = new StackPanel { Spacing = 6 };
                    box.Children.Add(new TextBlock { Text = "Quiz: " + q.Titel, FontSize = 16, FontWeight = FontWeight.SemiBold, Foreground = WkGold });
                    if (best is { } bb) box.Children.Add(new TextBlock { Name = "cs-quiz-best-" + b.Id, Text = $"Bisher bestes Ergebnis: {bb} %" + (bb >= k.Bestehen ? " – bestanden" : $" – nötig sind {k.Bestehen} %"), Foreground = bb >= k.Bestehen ? K4Good : K4Warn, FontSize = 12 });
                    if (_quizPlay.TryGetValue(sid, out var play))
                        box.Children.Add(QuizPlayView(CourseKey, sid, play, "cs-quiz-" + b.Id, () => Show(CourseKey), s =>
                        {
                            CourseChange(d, "Quiz-Ergebnis", x => KursKit.QuizErgebnis(x.Lernende.First(p => p.Id == pid), bid, s.Prozent));
                            _progMsg[CourseKey] = $"Quiz: {s.Prozent} % – " + (s.Prozent >= k.Bestehen ? "bestanden, die Lektion kann abgeschlossen werden." : $"nötig sind {k.Bestehen} %. Einfach nochmal versuchen.");
                            QuizStopTimer(sid); _quizPlay.Remove(sid);
                        }));
                    else
                        box.Children.Add(WkBtn("cs-quiz-start-" + b.Id, best is null ? "▶ Quiz starten" : "▶ Nochmal", CourseKey, () => _quizPlay[sid] = new QuizPlay(new QuizSpiel(q, Environment.TickCount))));
                    sp.Children.Add(WkCard(box, "cs-quiz-" + b.Id + "-card"));
                    break;
                }
                default: sp.Children.Add(new TextBlock { Text = b.Text, FontSize = 15, Foreground = Text, TextWrapping = TextWrapping.Wrap, LineHeight = 23 }); break;
            }
        }
        main.Children.Add(WkCard(sp, "cs-view-lesson"));
        var done = who.Erledigt.Contains(l.Id); var missing = KursKit.Fehlt(k, who, l);
        var foot = new StackPanel { Spacing = 6 };
        if (done) foot.Children.Add(new TextBlock { Text = "✓ Diese Lektion ist erledigt.", Foreground = K4Good, FontSize = 13 });
        else if (missing is not null) foot.Children.Add(new TextBlock { Name = "cs-missing", Text = missing, Foreground = K4Warn, FontSize = 13, TextWrapping = TextWrapping.Wrap });
        var all = KursKit.Lektionen(k); var idx = all.FindIndex(x => x.Lektion.Id == l.Id);
        var btn = new Button { Name = "cs-done", Content = done ? (idx + 1 < all.Count ? "Nächste Lektion" : "Kurs abgeschlossen") : "Lektion abschließen", Padding = new Thickness(18, 8), IsEnabled = done ? idx + 1 < all.Count : missing is null };
        QuietGold(btn);
        btn.Click += (_, _) => ProgramRun(CourseKey, () => { if (done) d.Data["lek"] = all[idx + 1].Lektion.Id; else CourseFinishLesson(d); });
        foot.Children.Add(btn);
        main.Children.Add(WkCard(foot, "cs-foot"));
    }

    Control CourseProperties(ProgramDocument d)
    {
        var k = CourseOf(d);
        var sp = new StackPanel { Name = "cs-props", Spacing = 6 };
        sp.Children.Add(H("Kurs", 13));
        sp.Children.Add(BueroLabeled("Titel", BueroBox("cs-title", k.Titel, "Name des Kurses", v => CourseChange(d, "Kurstitel", x => x.Titel = v))));
        sp.Children.Add(BueroLabeled("Beschreibung", BueroBox("cs-desc", k.Beschreibung, "Worum geht es, für wen ist der Kurs?", v => CourseChange(d, "Beschreibung", x => x.Beschreibung = v), true)));
        if (CourseLesson(d) is { } l && CourseWhere(k, l.Id) is var (ci, _) && ci >= 0)
            sp.Children.Add(BueroLabeled("Kapitel der gewählten Lektion", BueroBox("cs-chapter-title", k.Kapitel[ci].Titel, "Name des Kapitels", v => CourseChange(d, "Kapitel umbenannt", x => x.Kapitel[ci].Titel = v))));
        sp.Children.Add(BueroLabeled("Quiz bestanden ab (%)", BueroBox("cs-pass", k.Bestehen.ToString(CultureInfo.InvariantCulture), "0 bis 100", v => { if (int.TryParse(v, out var p) && p is >= 0 and <= 100) CourseChange(d, "Bestanden ab", x => x.Bestehen = p); }, false, 60)));
        var order = new CheckBox { Name = "cs-order", Content = "Lektionen der Reihe nach", IsChecked = k.Reihenfolge, Foreground = Text, FontSize = 12 };
        order.IsCheckedChanged += (_, _) => { if ((order.IsChecked == true) != k.Reihenfolge) ProgramRun(CourseKey, () => CourseChange(d, "Reihenfolge", x => x.Reihenfolge = order.IsChecked == true)); };
        sp.Children.Add(order);
        sp.Children.Add(H($"Lernende ({k.Lernende.Count})", 13));
        foreach (var p in k.Lernende)
        {
            var st = KursKit.Fortschritt(k, p); var pid = p.Id;
            sp.Children.Add(new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Children = {
                new StackPanel { Spacing = 2, Children = {
                    new TextBlock { Text = $"{p.Name} – {st.Prozent} %" + (st.QuizSchnitt is { } s ? $", Quiz {s} %" : "") + (st.Fertig ? " ✓" : ""), Foreground = Text, FontSize = 12 },
                    new ProgressBar { Minimum = 0, Maximum = 100, Value = st.Prozent, Height = 4, Foreground = WkGold } } },
                Pos(WkBtn("cs-person-del-" + p.Id, "✕", CourseKey, () => CourseChange(d, "Lernende(r) entfernt", x => x.Lernende.RemoveAll(y => y.Id == pid)), "Entfernen"), 1) } });
        }
        var nb = new TextBox { Name = "cs-person-name", PlaceholderText = "Name, z. B. Erika Muster", FontSize = 12 };
        var add = new Button { Name = "cs-person-add", Content = "Eintragen", Padding = new Thickness(10, 4) };
        add.Click += (_, _) => ProgramRun(CourseKey, () => CourseAddLearner(d, nb.Text ?? ""));
        sp.Children.Add(new Grid { ColumnDefinitions = new ColumnDefinitions("*,6,Auto"), Children = { nb, Pos(add, 2) } });
        sp.Children.Add(H("Prüfung", 13));
        sp.Children.Add(BueroFindings("cs-findings", KursKit.Pruefen(k), "Bereit zur Ausgabe."));
        return sp;
    }
}
