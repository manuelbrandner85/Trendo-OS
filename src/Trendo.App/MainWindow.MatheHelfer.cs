using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Trendo.Core;
using Trendo.Core.Programs;
using Trendo.Core.Wissen;

namespace Trendo.App;

/// <summary>T-0781 STUDIO-MATH-01 „Mathe-Helfer“: Rechenblatt wie ein CAS-Arbeitsblatt (Vorbild in der Bedienung: Eingabezeile mit Aufgabenart, Ergebnis,
/// aufklappbarer Lösungsweg, Probe, Funktionsgraph). Gerechnet wird ausschließlich im eigenen Kern <see cref="MatheKern"/>; die KI übersetzt höchstens eine
/// Textaufgabe in eine Rechnung („mathe.rechnung“) – das Ergebnis rechnet der Kern.</summary>
public partial class MainWindow
{
    public const string MatheKey = "mathe-helfer-programm", MatheStudio = "STUDIO-MATH-01";
    /// <summary>Gewählte Rechnung je Dokument (nur Ansicht, kein Rückgängig-Schritt).</summary>
    readonly Dictionary<string, int> _mhSel = [];
    /// <summary>Getippte, noch nicht gerechnete Eingabe je Dokument.</summary>
    readonly Dictionary<string, string> _mhTyped = [];
    readonly Dictionary<string, (Loesung? L, string? Fehler)> _mhCache = [];

    static readonly (string Id, string Label, string Beispiel)[] MhArten =
    [
        ("auto", "Automatisch", "x² − 5x + 6 = 0"), ("gleichung", "Gleichung", "3x + 7 = 22"), ("system", "Gleichungssystem", "2x + 3y = 12; x − y = 1"),
        ("vereinfachen", "Vereinfachen", "4x²y + 3xy² − x²y"), ("ausmultiplizieren", "Ausmultiplizieren", "(x + 3)²"), ("faktorisieren", "Faktorisieren", "x² + x − 6"),
        ("bruch", "Bruchrechnen", "3/4 + 5/6"), ("ableiten", "Ableiten", "x²·sin(x)"), ("integrieren", "Integrieren", "x·e^x"),
    ];

    static JsonObject MhEintrag(string art, string eingabe, string variable = "", string von = "", string bis = "", string quelle = "") =>
        new() { ["art"] = art, ["eingabe"] = eingabe, ["variable"] = variable, ["von"] = von, ["bis"] = bis, ["quelle"] = quelle };

    static JsonObject MhDoc(string title, params JsonObject[] r) => new() { ["title"] = title, ["art"] = "auto", ["eingabe"] = "", ["rechnungen"] = new JsonArray([.. r]), ["graphVon"] = "-10", ["graphBis"] = "10" };

    private void InitMatheHelfer() => RegisterProgram(FachTabs, new ProgramSpec(MatheKey, MatheStudio, "Mathe-Helfer")
    {
        Sentence = "Rechnen mit Lösungsweg Schritt für Schritt: Gleichungen und Gleichungssysteme, Terme, Brüche, Ableitungen und Integrale – exakt mit Brüchen und mit Probe.",
        Folder = "Mathe",
        ScrollWorkspace = true,
        Templates =
        [
            new("leer", "Leeres Rechenblatt", "Aufgabe eintippen und rechnen", () => MhDoc("")),
            new("gleichungen", "Beispiel: Gleichungen", "Linear, quadratisch und ein Gleichungssystem", () => MhDoc("Gleichungen üben",
                MhEintrag("gleichung", "3x + 7 = 22"), MhEintrag("gleichung", "x^2 - 5x + 6 = 0"), MhEintrag("system", "2x + 3y = 12; x - y = 1"))),
            new("analysis", "Beispiel: Ableiten und Integrieren", "Produktregel, Kettenregel, Stammfunktion und Fläche", () => MhDoc("Analysis",
                MhEintrag("ableiten", "x^2 * sin(x)"), MhEintrag("ableiten", "e^(2x) + ln(x)"), MhEintrag("integrieren", "3x^2 + 2x"), MhEintrag("integrieren", "x^2", "", "0", "3"))),
            new("brueche", "Beispiel: Bruchrechnen", "Hauptnenner, Kürzen, Kehrwert", () => MhDoc("Brüche",
                MhEintrag("bruch", "3/4 + 5/6"), MhEintrag("bruch", "4/9 * 3/8"), MhEintrag("bruch", "(3/4) : (9/10)"))),
        ],
        Ribbon =
        [
            ("Rechnen", [
                new ProgramCmd("mh-run-rechnen", "Rechnen", d => MhRechnen(d), Primary: true, Tip: "Rechnet die Aufgabe aus der Eingabezeile mit Lösungsweg und Probe (Enter)."),
                new ProgramCmd("mh-beispiel", "Beispiel einsetzen", d => MhBeispiel(d), Tip: "Setzt ein Beispiel für die gewählte Aufgabenart in die Eingabezeile.") { OhneKi = true },
            ]),
            ("Bearbeiten", [
                new ProgramCmd("mh-loeschen", "Rechnung löschen", d => MhLoeschen(d, MhAuswahl(d)), Tip: "Entfernt die gewählte Rechnung (Entf). Rückgängig holt sie zurück.") { OhneKi = true },
                new ProgramCmd("mh-kopie", "Rechnung kopieren", d => MhKopie(d, MhAuswahl(d)), Tip: "Legt eine Kopie der gewählten Rechnung an.") { OhneKi = true },
                new ProgramCmd("mh-hoch", "Nach oben", d => MhVerschieben(d, MhAuswahl(d), -1), Level: 1) { OhneKi = true },
                new ProgramCmd("mh-runter", "Nach unten", d => MhVerschieben(d, MhAuswahl(d), 1), Level: 1) { OhneKi = true },
            ]),
            ("Datei", [
                new ProgramCmd("mh-import", "Aufgabenblatt öffnen …", d => _ = MhImportPick(d), Tip: "Liest Aufgaben aus einer Text-, Markdown- oder CSV-Datei (eine Aufgabe je Zeile, optional „gleichung: 3x + 7 = 22“). Du kannst die Datei auch auf das Rechenblatt ziehen."),
            ]),
        ],
        Exports =
        [
            new("pdf", "Lösungswege als PDF", ".pdf", (d, f) => { Rechenblatt.Pdf(f, MhTitel(d), MhBlatt(d)); return f; }),
            new("tex", "LaTeX (.tex)", ".tex", (d, f) => { AtomicFile.WriteAllText(f, Rechenblatt.Latex(MhTitel(d), MhBlatt(d))); return f; }),
            new("md", "Markdown (.md)", ".md", (d, f) => { AtomicFile.WriteAllText(f, Rechenblatt.Markdown(MhTitel(d), MhBlatt(d))); return f; }),
        ],
        SimpleExports = 1,
        KiActions = [MhKiRechnung],
        KiLocked = ["rechnungen"], // Ergebnisse stehen nie im Dokument – die KI kann keine Rechnung „von Hand“ eintragen, nur über „mathe.rechnung“
        KiHint = "Für jede Rechnung nimmst du genau EINEN Schritt „mathe.rechnung“ (keine Menübefehle). Eine Textaufgabe übersetzt du mit „mathe.rechnung“ in eine Rechnung: „art“ (auto, gleichung, system, vereinfachen, ausmultiplizieren, faktorisieren, bruch, ableiten, integrieren) "
               + "und „eingabe“ in der Schreibweise des Rechenblatts, z. B. „3x + 7 = 22“, „2x + 3y = 12; x - y = 1“, „3/4 + 5/6“ oder „x^2 * sin(x)“; bei bestimmten Integralen dazu „von“ und „bis“. "
               + "Gib NUR die Aufgabe an, nie das Ergebnis: Lösen, Rechnen und die Probe übernimmt der Rechenkern des Programms. Eine Gleichung wie „x = 5“ ist schon ein Ergebnis und wird abgelehnt. "
               + "Beispiel für „Ein Rechteck ist 3 cm länger als breit und hat 40 cm² Fläche“: {\"steps\":[{\"action\":\"mathe.rechnung\",\"args\":{\"art\":\"gleichung\",\"eingabe\":\"x*(x+3) = 40\"}}]}",
        Workspace = MhWorkspace,
        Properties = MhProperties,
        Status = d => { var n = d.List("rechnungen").Count; if (n == 0) return "Noch keine Rechnung"; var l = Enumerable.Range(0, n).Select(i => MhLoesung(d, i)).ToList(); var fehl = l.Count(x => x.L is { ProbeOk: false } || x.Fehler is not null); return $"{n} Rechnung(en) · " + (fehl == 0 ? "alle Proben bestanden" : $"{fehl} ohne bestandene Probe"); },
        Help = "Oben die Aufgabenart wählen (oder „Automatisch“) und die Aufgabe eintippen: Komma oder Punkt, ·, :, ², √, π – das Malzeichen darf fehlen (2x, 3(x+1)). Enter oder „Rechnen“ legt die Rechnung an. "
             + "Rechts stehen Ergebnis, Lösungsweg Schritt für Schritt und die Probe; für Funktionen in x zeichnet der Graph mit. Mehrere Gleichungen mit „;“ trennen. "
             + "Entf löscht die gewählte Rechnung, Rechtsklick auf eine Rechnung zeigt weitere Befehle. Gerechnet wird exakt mit Brüchen; Näherungen sind mit ≈ gekennzeichnet.",
    }, "mathe", "mathematik", "rechnen", "gleichung", "gleichungssystem", "lösungsweg", "term", "vereinfachen", "faktorisieren", "ausmultiplizieren", "bruch", "brüche", "ableitung", "ableiten", "integral", "integrieren", "cas", "hausaufgaben");

    static string MhTitel(ProgramDocument d) => d.Text("title") is { Length: > 0 } t ? t : "Rechenblatt";

    public int MhAuswahl(ProgramDocument d) { var n = d.List("rechnungen").Count; return n == 0 ? -1 : Math.Clamp(_mhSel.GetValueOrDefault(d.Id, n - 1), 0, n - 1); }
    public void MhWaehle(ProgramDocument d, int i) { _mhSel[d.Id] = i; Show(MatheKey); }

    static MatheAuftrag MhAuftrag(JsonNode? r) => new(MatheKern.ArtVonId.GetValueOrDefault(Js(r?["art"]), MatheArt.Automatisch), Js(r?["eingabe"]),
        Js(r?["variable"]) is { Length: > 0 } v ? v : null, Js(r?["von"]) is { Length: > 0 } a ? a : null, Js(r?["bis"]) is { Length: > 0 } b ? b : null);

    /// <summary>Lösung der i-ten Rechnung – immer frisch aus dem Kern (zwischengespeichert nach Eingabe), nie aus dem Dokument.</summary>
    public (Loesung? L, string? Fehler) MhLoesung(ProgramDocument d, int i)
    {
        var r = d.List("rechnungen")[i];
        var key = r?.ToJsonString() ?? "";
        if (_mhCache.TryGetValue(key, out var c)) return c;
        return MhMerken(key, MhRechneSicher(MhAuftrag(r)));
    }

    (Loesung? L, string? Fehler) MhMerken(string key, (Loesung? L, string? Fehler) res)
    {
        if (_mhCache.Count > 500) _mhCache.Clear();
        return _mhCache[key] = res;
    }

    /// <summary>Rechnen mit Zeitgrenze (3 s im Kern): Fehler und Abbruch werden zur Meldung, nie zum Absturz.</summary>
    static (Loesung? L, string? Fehler) MhRechneSicher(MatheAuftrag a)
    {
        try { return (MatheKern.Rechne(a), null); }
        catch (Exception e) when (e is FormatException or ArgumentException or DivideByZeroException or OverflowException or InvalidOperationException or OperationCanceledException) { return (null, e.Message); }
    }

    /// <summary>T-0781 (Gegenprüfung: Einfrieren): Rechnung im Hintergrund; kurze Rechnungen (unter ¼ s) kommen sofort, längere melden sich nach dem Ende
    /// und zeichnen das Programm neu – die Oberfläche bleibt bedienbar. Die Rechenzeit ist im Kern auf 3 s begrenzt.</summary>
    readonly HashSet<string> _mhLaeuft = [];
    (Loesung? L, string? Fehler)? MhLoesungOderSpaeter(ProgramDocument d, int i)
    {
        var r = d.List("rechnungen")[i];
        var key = r?.ToJsonString() ?? "";
        if (_mhCache.TryGetValue(key, out var c)) return c;
        if (_mhLaeuft.Contains(key)) return null;
        var auftrag = MhAuftrag(r);
        var task = Task.Run(() => MhRechneSicher(auftrag));
        if (task.Wait(250)) return MhMerken(key, task.Result);
        _mhLaeuft.Add(key);
        task.ContinueWith(t => Avalonia.Threading.Dispatcher.UIThread.Post(() => { _mhLaeuft.Remove(key); MhMerken(key, t.Result); if (CurrentView == MatheKey) Show(MatheKey); }));
        return null;
    }

    IEnumerable<BlattEintrag> MhBlatt(ProgramDocument d)
    {
        var l = d.List("rechnungen");
        for (var i = 0; i < l.Count; i++)
        {
            var (s, err) = MhLoesung(d, i);
            yield return s is not null ? BlattEintrag.Aus(s) : new BlattEintrag(Js(l[i]?["art"]), Js(l[i]?["eingabe"]), [], "", "", [], err);
        }
    }

    /// <summary>„Rechnen“: Eingabezeile prüfen (der Kern muss die Aufgabe verstehen), dann als neue Rechnung anlegen – ein Rückgängig-Schritt.</summary>
    public void MhRechnen(ProgramDocument d)
    {
        var text = (_mhTyped.GetValueOrDefault(d.Id) ?? d.Text("eingabe")).Trim();
        if (text.Length == 0) throw new ArgumentException("Tippe oben eine Aufgabe ein, z. B. 3x + 7 = 22.");
        var art = d.Text("art", "auto");
        var e = MhEintrag(art, text);
        var auftrag = MhAuftrag(e);
        var task = Task.Run(() => MhRechneSicher(auftrag));
        if (task.Wait(250)) { MhUebernehmen(d, e, text, task.Result); return; }
        // länger als ¼ s: im Hintergrund weiterrechnen (höchstens 3 s), die Oberfläche bleibt bedienbar
        _progMsg[MatheKey] = "Rechnet … (höchstens 3 Sekunden)";
        task.ContinueWith(t => Avalonia.Threading.Dispatcher.UIThread.Post(() => ProgramRun(MatheKey, () => MhUebernehmen(d, e, text, t.Result))));
    }

    void MhUebernehmen(ProgramDocument d, JsonObject e, string text, (Loesung? L, string? Fehler) res)
    {
        var (l, err) = res;
        if (l is null) throw new ArgumentException(err!);
        MhMerken(e.ToJsonString(), res);
        d.Edit("Rechnen: " + (text.Length > 30 ? text[..30] + "…" : text), x => { ((JsonArray)(x["rechnungen"] ??= new JsonArray())).Add(e); x["eingabe"] = ""; });
        _mhTyped.Remove(d.Id);
        _mhSel[d.Id] = d.List("rechnungen").Count - 1;
        _progMsg[MatheKey] = $"{MatheKern.ArtName(l.Art)}: {l.Ergebnis}" + (l.ProbeOk ? " – Probe bestanden." : l.Unsicher ? " – Achtung: Probe unsicher (gelb)." : " – Achtung: Probe nicht bestanden.");
    }

    (Loesung? L, string? Fehler) MhPruefen(JsonObject e) => MhRechneSicher(MhAuftrag(e));

    void MhBeispiel(ProgramDocument d)
    {
        var art = d.Text("art", "auto");
        var b = MhArten.FirstOrDefault(a => a.Id == art).Beispiel ?? MhArten[0].Beispiel;
        _mhTyped[d.Id] = b.Replace('−', '-');
        _progMsg[MatheKey] = "Beispiel eingesetzt – mit Enter oder „Rechnen“ ausrechnen.";
    }

    void MhLoeschen(ProgramDocument d, int i)
    {
        if (i < 0) throw new ArgumentException("Es gibt noch keine Rechnung.");
        d.Edit("Rechnung löschen", x => ((JsonArray)x["rechnungen"]!).RemoveAt(i));
        _mhSel[d.Id] = Math.Max(0, i - 1);
        _progMsg[MatheKey] = "Rechnung gelöscht – Rückgängig holt sie zurück.";
    }

    void MhKopie(ProgramDocument d, int i)
    {
        if (i < 0) throw new ArgumentException("Es gibt noch keine Rechnung.");
        d.Edit("Rechnung kopieren", x => { var a = (JsonArray)x["rechnungen"]!; a.Insert(i + 1, a[i]!.DeepClone()); });
        _mhSel[d.Id] = i + 1;
    }

    void MhVerschieben(ProgramDocument d, int i, int richtung)
    {
        var n = d.List("rechnungen").Count; var j = i + richtung;
        if (i < 0 || j < 0 || j >= n) return;
        d.Edit(richtung < 0 ? "Nach oben" : "Nach unten", x => { var a = (JsonArray)x["rechnungen"]!; var e = a[i]!.DeepClone(); a.RemoveAt(i); a.Insert(j, e); });
        _mhSel[d.Id] = j;
    }

    /// <summary>Eine Rechnung ändern (Art, Variable, Grenzen) – ein Rückgängig-Schritt.</summary>
    public void MhAendern(ProgramDocument d, int i, string feld, string wert)
    {
        if (i < 0) return;
        if (Js(d.List("rechnungen")[i]?[feld]) == wert) return;
        d.Edit(feld switch { "art" => "Aufgabenart", "variable" => "Variable", "von" or "bis" => "Grenzen", _ => "Rechnung ändern" }, x => ((JsonArray)x["rechnungen"]!)[i]![feld] = wert);
    }

    // ---------- Aufgabenblatt einlesen (Datei oder Ziehen und Ablegen) ----------

    public static readonly string[] MhImportEndungen = [".txt", ".md", ".csv", ".tex"];

    async Task MhImportPick(ProgramDocument d)
    {
        if (await PickMediaFile("mh-import", "Aufgabenblatt (Text, Markdown, CSV)", [.. MhImportEndungen.Select(e => "*" + e)]) is { } f) ProgramRun(MatheKey, () => MhImport(d, f));
    }

    /// <summary>Aufgaben aus einer Datei: eine je Zeile; „art: aufgabe“ wählt die Art; Aufzählungszeichen, Nummern und Überschriften werden übergangen.</summary>
    public int MhImport(ProgramDocument d, string pfad)
    {
        var fi = new FileInfo(pfad);
        if (!fi.Exists) throw new FileNotFoundException("Die Datei gibt es nicht.");
        if (fi.Length > 2_000_000) throw new InvalidDataException("Die Datei ist zu groß (höchstens 2 MB).");
        var neu = new List<JsonObject>(); var uebergangen = 0;
        foreach (var roh in File.ReadAllLines(pfad))
        {
            var z = roh.Trim().TrimStart('-', '*', '•', '\t', ' ');
            z = System.Text.RegularExpressions.Regex.Replace(z, @"^(\d+[.)]|[a-z][.)])\s+", "");
            if (z.Length == 0 || z.StartsWith('#') || z.StartsWith('%') || z.StartsWith('\\')) continue;
            if (!System.Text.RegularExpressions.Regex.IsMatch(z, @"[\d=+\-*/^²³√:]")) continue; // Überschrift oder Fließtext (keine Zahl, kein Rechenzeichen)
            if (pfad.EndsWith(".csv", StringComparison.OrdinalIgnoreCase) && z.Contains(';')) { var c = z.Split(';', 2); z = c.Length == 2 && MatheKern.ArtVonId.ContainsKey(c[0].Trim()) ? c[0].Trim() + ": " + c[1].Trim() : z; }
            var art = "auto";
            var dp = z.IndexOf(':');
            if (dp > 0 && MatheKern.ArtVonId.ContainsKey(z[..dp].Trim().ToLowerInvariant())) { art = z[..dp].Trim().ToLowerInvariant(); z = z[(dp + 1)..].Trim(); }
            var e = MhEintrag(art, z, quelle: Path.GetFileName(pfad));
            if (MhPruefen(e).L is null) { uebergangen++; continue; }
            neu.Add(e);
            if (neu.Count >= 200) break;
        }
        if (neu.Count == 0) throw new InvalidDataException("In der Datei steht keine Aufgabe, die der Mathe-Helfer rechnen kann (eine Aufgabe je Zeile, z. B. „3x + 7 = 22“).");
        d.Edit("Aufgabenblatt einlesen", x => { var a = (JsonArray)(x["rechnungen"] ??= new JsonArray()); foreach (var e in neu) a.Add(e); if (string.IsNullOrWhiteSpace(Js(x["title"]))) x["title"] = Path.GetFileNameWithoutExtension(pfad); });
        _mhSel[d.Id] = d.List("rechnungen").Count - neu.Count;
        _progMsg[MatheKey] = $"„{Path.GetFileName(pfad)}“: {neu.Count} Aufgabe(n) gerechnet" + (uebergangen > 0 ? $", {uebergangen} Zeile(n) übergangen (nicht verstanden)." : ".") + " Die Datei bleibt unverändert.";
        return neu.Count;
    }

    // ---------- KI-Fachaktion: Textaufgabe → Rechnung (das Ergebnis rechnet der Kern) ----------

    ProgramKiAction MhKiRechnung => new(new AssistAction("mathe.rechnung", "Textaufgabe als Rechnung eintragen", ["textaufgabe"], // nur die KI übersetzt Textaufgaben – der Regel-Planer ohne KI soll diese Aktion nicht wählen (er kann die Rechnung nicht formulieren)
        [new AssistParam("art", "Aufgabenart", AssistKind.Choice, [.. MatheKern.ArtVonId.Keys], Default: "auto", Required: false, Words: ["art"]),
         new AssistParam("eingabe", "Rechnung (nur die Aufgabe, ohne Ergebnis)"),
         new AssistParam("variable", "Variable", Required: false, Words: ["variable"]),
         new AssistParam("von", "untere Grenze", Required: false, Words: ["von"]),
         new AssistParam("bis", "obere Grenze", Required: false, Words: ["bis"])]),
        (d, a) =>
        {
            var eingabe = (a.GetValueOrDefault("eingabe") ?? "").Trim().Replace('−', '-');
            if (eingabe.Length == 0) throw new InvalidOperationException("Die KI hat keine Rechnung genannt.");
            var art = a.GetValueOrDefault("art") is { Length: > 0 } ar && MatheKern.ArtVonId.ContainsKey(ar) ? ar.ToLowerInvariant() : "auto";
            MhKeinErgebnis(eingabe);
            var e = MhEintrag(art, eingabe, a.GetValueOrDefault("variable") ?? "", a.GetValueOrDefault("von") ?? "", a.GetValueOrDefault("bis") ?? "", "KI: Textaufgabe");
            var (l, err) = MhPruefen(e);
            if (l is null) throw new InvalidOperationException($"Die Rechnung „{eingabe}“ versteht der Rechenkern nicht: {err}");
            d.Edit("Rechnung aus Textaufgabe", x => ((JsonArray)(x["rechnungen"] ??= new JsonArray())).Add(e));
            _mhSel[d.Id] = d.List("rechnungen").Count - 1;
        },
        (d, created) => [])
    {
        Hinweis = "Gerechnet hat der Rechenkern, nicht die KI – „Ja, behalten“ übernimmt die Rechnung ins Rechenblatt (ein Rückgängig-Schritt).",
    };

    /// <summary>Hat die KI schon ein Ergebnis eingesetzt („x = 5“, „x = 3; y = 2“)? Dann ablehnen – rechnen soll der Kern.</summary>
    static void MhKeinErgebnis(string eingabe)
    {
        var gl = eingabe.Split([';', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (gl.Length == 0 || !gl.All(g => g.Contains('='))) return;
        bool Fertig(string g)
        {
            var t = g.Split('=');
            if (t.Length != 2) return false;
            try
            {
                var l = TermLeser.Lies(t[0]); var r = TermLeser.Lies(t[1]);
                // Gegenprüfung T-0781: auch „x + 0 = 5“, „1x = 5“, „2x/2 = 5“ sind schon nach x aufgelöst
                l = Vereinfacher.Vereinfache(l); r = Vereinfacher.Vereinfache(r);
                return l is Var && !r.Variablen().Any() || r is Var && !l.Variablen().Any();
            }
            catch (FormatException) { return false; }
        }
        if (gl.All(Fertig)) throw new InvalidOperationException("Die KI hat schon ein Ergebnis eingesetzt („" + eingabe + "“) – gerechnet wird nur im Rechenkern. Bitte die Aufgabe selbst angeben (z. B. „3x + 7 = 22“).");
    }

    // ---------- Arbeitsfläche ----------

    Control MhWorkspace(ProgramDocument d)
    {
        var sp = new StackPanel { Name = "mh-workspace", Spacing = 12, Margin = new Thickness(4, 0, 10, 4) };
        // Aufgabenart
        var arten = new WrapPanel { Name = "mh-arten" };
        var aktArt = d.Text("art", "auto");
        foreach (var (id, label, _) in MhArten)
        {
            var b = new Button { Name = "mh-art-" + id, Content = label, FontSize = 12, Padding = new Thickness(11, 4), Margin = new Thickness(0, 0, 6, 6), CornerRadius = new CornerRadius(13) };
            if (id == aktArt) QuietGold(b);
            var i2 = id;
            b.Click += (_, _) => ProgramRun(MatheKey, () => { if (d.Text("art") != i2) d.Set("art", i2, "Aufgabenart"); });
            arten.Children.Add(b);
        }
        // Eingabezeile
        var box = new TextBox { Name = "mh-eingabe", Text = _mhTyped.GetValueOrDefault(d.Id) ?? d.Text("eingabe"), FontFamily = Mono, FontSize = 18, PlaceholderText = "Aufgabe, z. B. " + (MhArten.FirstOrDefault(a => a.Id == aktArt).Beispiel ?? MhArten[0].Beispiel), AcceptsReturn = false };
        box.TextChanged += (_, _) => _mhTyped[d.Id] = box.Text ?? "";
        box.KeyDown += (_, e) => { if (e.Key == Key.Enter) { e.Handled = true; ProgramRun(MatheKey, () => MhRechnen(d)); } };
        var los = new Button { Name = "mh-rechnen-zeile", Content = "Rechnen", Padding = new Thickness(16, 6), Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Stretch };
        QuietGold(los);
        los.Click += (_, _) => ProgramRun(MatheKey, () => MhRechnen(d));
        var tasten = new WrapPanel { Name = "mh-tasten", Margin = new Thickness(0, 6, 0, 0) };
        foreach (var t in new[] { "²", "³", "^", "√(", "π", "·", ":", "(", ")", "=", ";", "x", "y", "sin(", "cos(", "ln(", "e^" })
        {
            var b = new Button { Name = "mh-taste-" + (t switch { "²" => "hoch2", "³" => "hoch3", "^" => "hoch", "√(" => "wurzel", "π" => "pi", "·" => "mal", ":" => "geteilt", "(" => "auf", ")" => "zu", "=" => "gleich", ";" => "trenner", "e^" => "e-hoch", _ => t.TrimEnd('(') }), Content = t.TrimEnd('(') is { Length: > 0 } c && t.EndsWith('(') && t != "√(" ? c : t, FontSize = 13, MinWidth = 34, Padding = new Thickness(8, 3), Margin = new Thickness(0, 0, 4, 4), FontFamily = Mono };
            var ins = t;
            b.Click += (_, _) => { var c0 = Math.Clamp(box.CaretIndex, 0, box.Text?.Length ?? 0); box.Text = (box.Text ?? "").Insert(c0, ins); box.CaretIndex = c0 + ins.Length; box.Focus(); };
            tasten.Children.Add(b);
        }
        var zeile = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Children = { box, Pos(los, 1) } };
        sp.Children.Add(KrCard(new StackPanel { Spacing = 6, Children = { arten, zeile, tasten } }, "mh-eingabe-karte"));
        BueroFileDrop(sp, MatheKey, BueroExt(MhImportEndungen), "ein Aufgabenblatt (.txt, .md, .csv oder .tex)", (dd, files) => MhImport(dd, files[0]));

        var liste = d.List("rechnungen");
        if (liste.Count == 0)
        {
            sp.Children.Add(KrCard(new StackPanel { Spacing = 8, Children = {
                H("So geht's", 15),
                P("1. Oben die Art wählen – oder „Automatisch“ lassen.  2. Die Aufgabe eintippen, z. B. „x² − 5x + 6 = 0“ oder „3/4 + 5/6“.  3. Enter drücken. Rechts stehen dann Ergebnis, Lösungsweg und Probe.", Text, 13),
                P("Du kannst auch ein Aufgabenblatt (Text- oder Markdown-Datei, eine Aufgabe je Zeile) hierher ziehen oder der KI oben in der Auftragszeile eine Textaufgabe geben – rechnen tut immer der Mathe-Helfer selbst.", Muted, 12),
            } }, "mh-leer"));
            return sp;
        }
        var sel = MhAuswahl(d);
        // links: Rechnungen, rechts: gewählte Rechnung
        var links = new StackPanel { Name = "mh-liste", Spacing = 4 };
        for (var i = 0; i < liste.Count; i++)
        {
            var (l, err) = MhLoesungOderSpaeter(d, i) ?? (null, "Rechnet …");
            var ok = l?.ProbeOk ?? false;
            var zeichen = new TextBlock { Text = err is not null ? "!" : ok ? "✓" : l!.Unsicher ? "?" : "✗", Foreground = err is not null || !ok ? (l is { Unsicher: false, ProbeOk: false } ? K4Bad : K4Warn) : K4Good, FontSize = 14, FontWeight = FontWeight.SemiBold, Width = 18 };
            var inhalt = new StackPanel { Spacing = 1, Children = {
                new TextBlock { Text = Js(liste[i]?["eingabe"]), Foreground = Text, FontFamily = Mono, FontSize = 12.5, TextTrimming = TextTrimming.CharacterEllipsis },
                new TextBlock { Text = err ?? l!.Ergebnis, Foreground = err is null ? Accent : K4Warn, FontSize = 11.5, TextTrimming = TextTrimming.CharacterEllipsis },
            } };
            var b = new Button { Name = $"mh-zeile-{i}", HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch, Padding = new Thickness(8, 5),
                Content = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), Children = { zeichen, Pos(inhalt, 1) } } };
            if (i == sel) QuietGold(b);
            var k = i;
            b.Click += (_, _) => MhWaehle(d, k);
            b.ContextMenu = MhKontext(d, k);
            links.Children.Add(b);
        }
        links.Focusable = true;
        links.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Delete) { e.Handled = true; ProgramRun(MatheKey, () => MhLoeschen(d, MhAuswahl(d))); }
            else if (e.Key is Key.Up or Key.Down) { e.Handled = true; var n = Math.Clamp(MhAuswahl(d) + (e.Key == Key.Up ? -1 : 1), 0, liste.Count - 1); MhWaehle(d, n); }
        };
        var rechts = MhDetail(d, sel);
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("300,14,*") };
        grid.Children.Add(KrCard(new StackPanel { Spacing = 6, Children = { H($"Rechnungen ({liste.Count})", 14), links } }, "mh-liste-karte"));
        grid.Children.Add(Pos(rechts, 2));
        sp.Children.Add(grid);
        return sp;
    }

    ContextMenu MhKontext(ProgramDocument d, int i)
    {
        MenuItem M(string name, string text, Action a) { var m = new MenuItem { Name = name, Header = text }; m.Click += (_, _) => ProgramRun(MatheKey, a); return m; }
        return new ContextMenu { Items = {
            M($"mh-ctx-eingabe-{i}", "In die Eingabezeile holen", () => { _mhTyped[d.Id] = Js(d.List("rechnungen")[i]?["eingabe"]); d.Set("art", Js(d.List("rechnungen")[i]?["art"]) is { Length: > 0 } a ? a : "auto", "Aufgabenart"); }),
            M($"mh-ctx-kopie-{i}", "Kopieren", () => MhKopie(d, i)),
            M($"mh-ctx-hoch-{i}", "Nach oben", () => MhVerschieben(d, i, -1)),
            M($"mh-ctx-loeschen-{i}", "Löschen", () => MhLoeschen(d, i)),
        } };
    }

    Control MhDetail(ProgramDocument d, int i)
    {
        var (l, err) = MhLoesungOderSpaeter(d, i) ?? (null, "Rechnet … (höchstens 3 Sekunden, die Oberfläche bleibt bedienbar)");
        var sp = new StackPanel { Name = "mh-detail", Spacing = 10 };
        if (l is null)
        {
            sp.Children.Add(H(Js(d.List("rechnungen")[i]?["eingabe"]), 15));
            sp.Children.Add(P("Diese Aufgabe kann der Mathe-Helfer nicht rechnen: " + err, K4Warn, 13));
            return KrCard(sp, "mh-detail-karte");
        }
        sp.Children.Add(P(MatheKern.ArtName(l.Art) + (Js(d.List("rechnungen")[i]?["quelle"]) is { Length: > 0 } q ? " · aus " + q : ""), Muted, 11));
        sp.Children.Add(new TextBlock { Name = "mh-aufgabe", Text = l.Aufgabe, Foreground = Text, FontSize = 15, TextWrapping = TextWrapping.Wrap, FontWeight = FontWeight.SemiBold });
        sp.Children.Add(new Border { Padding = new Thickness(12, 8), CornerRadius = new CornerRadius(8), Background = Brush.Parse("#1AD4A017"), BorderBrush = Brush.Parse("#55D4A017"), BorderThickness = new Thickness(1),
            Child = new TextBlock { Name = "mh-ergebnis", Text = l.Ergebnis, Foreground = Accent, FontSize = 20, FontWeight = FontWeight.SemiBold, FontFamily = Mono, TextWrapping = TextWrapping.Wrap } });
        // Lösungsweg
        var weg = new StackPanel { Name = "mh-weg", Spacing = 6 };
        var nr = 0;
        foreach (var s in l.Schritte)
        {
            nr++;
            weg.Children.Add(new Grid { ColumnDefinitions = new ColumnDefinitions("26,*"), Children = {
                new TextBlock { Text = nr + ".", Foreground = Accent, FontSize = 12.5, FontWeight = FontWeight.SemiBold },
                Pos(new StackPanel { Spacing = 2, Children = {
                    new TextBlock { Text = s.Text, Foreground = Muted, FontSize = 12, TextWrapping = TextWrapping.Wrap },
                    new TextBlock { Text = s.Formel, Foreground = Text, FontSize = 13.5, FontFamily = Mono, TextWrapping = TextWrapping.Wrap },
                } }, 1) } });
        }
        sp.Children.Add(new Expander { Name = "mh-weg-auf", Header = H($"Lösungsweg ({l.Schritte.Count} Schritte)", 13), IsExpanded = true, HorizontalAlignment = HorizontalAlignment.Stretch, Content = weg });
        // Probe
        var probe = new StackPanel { Name = "mh-probe", Spacing = 3 };
        foreach (var p in l.Proben) probe.Children.Add(new TextBlock { Text = p.Text, Foreground = p.Unsicher ? K4Warn : p.Ok ? K4Good : K4Bad, FontSize = 12, TextWrapping = TextWrapping.Wrap });
        sp.Children.Add(new StackPanel { Spacing = 4, Children = { H("Probe", 13), probe } });
        if (ProgramLevel >= UserLevel.Profi) sp.Children.Add(new TextBlock { Name = "mh-latex", Text = "LaTeX: " + l.ErgebnisLatex, Foreground = Muted, FontSize = 11, FontFamily = Mono, TextWrapping = TextWrapping.Wrap });
        // Funktionsgraph (vorhandener GraphView des Rechners)
        if (l.Graph.Length > 0 && l.Variable == "x") sp.Children.Add(MhGraph(d, l));
        return KrCard(sp, "mh-detail-karte");
    }

    Control MhGraph(ProgramDocument d, Loesung l)
    {
        double a = -10, b = 10;
        try { a = Calc.Eval(d.Text("graphVon", "-10")); b = Calc.Eval(d.Text("graphBis", "10")); } catch (Exception e) when (e is FormatException or DivideByZeroException or OverflowException) { }
        if (b <= a) (a, b) = (-10, 10);
        var gv = new GraphView { Name = "mh-graph", Height = 280, Background = Brush.Parse("#141B27"), Foreground = Text, Farben = ["#D4A017", "#2BB3A3", "#E5484D", "#F76B15", "#A78BFA"] };
        var legende = new WrapPanel();
        var namen = l.Art switch { MatheArt.Ableiten => new[] { "f(x)", "f'(x)" }, MatheArt.Integrieren => ["f(x)", "F(x)"], MatheArt.Gleichung => ["linke Seite", "rechte Seite"], _ => ["vorher", "nachher"] };
        var k = 0;
        foreach (var f in l.Graph.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Take(2))
        {
            List<(double X, double Y)> pts;
            try { pts = Calc.Plot(f, a, b, 500, false); } catch (Exception e) when (e is FormatException or DivideByZeroException or OverflowException) { continue; }
            gv.Series.Add((namen[Math.Min(k, namen.Length - 1)], pts));
            legende.Children.Add(new TextBlock { Text = "— " + namen[Math.Min(k, namen.Length - 1)], Foreground = Brush.Parse(gv.Farben[k % gv.Farben.Length]), FontSize = 12, Margin = new Thickness(0, 0, 14, 0) });
            k++;
        }
        if (gv.Series.Count == 0) return new TextBlock { Text = "" };
        return new StackPanel { Spacing = 4, Children = { H("Graph", 13), legende, gv } };
    }

    Control MhProperties(ProgramDocument d)
    {
        var sp = new StackPanel { Spacing = 8, Name = "mh-props" };
        sp.Children.Add(H("Rechenblatt", 13));
        sp.Children.Add(KrBox(d, "title", "mh-titel", "Titel (für den Export)"));
        var i = MhAuswahl(d);
        if (i >= 0)
        {
            var r = d.List("rechnungen")[i];
            sp.Children.Add(H("Gewählte Rechnung", 13));
            var art = new ComboBox { Name = "mh-prop-art", HorizontalAlignment = HorizontalAlignment.Stretch, ItemsSource = MhArten.Select(a => a.Label).ToList(), SelectedIndex = Math.Max(0, Array.FindIndex(MhArten, a => a.Id == Js(r?["art"]))) };
            art.SelectionChanged += (_, _) => { if (art.SelectedIndex >= 0) ProgramRun(MatheKey, () => MhAendern(d, i, "art", MhArten[art.SelectedIndex].Id)); };
            sp.Children.Add(P("Aufgabenart", Muted, 11)); sp.Children.Add(art);
            TextBox Feld(string name, string feld, string label)
            {
                var t = new TextBox { Name = name, Text = Js(r?[feld]), PlaceholderText = label };
                void Commit() => ProgramRun(MatheKey, () => MhAendern(d, i, feld, (t.Text ?? "").Trim()));
                t.LostFocus += (_, _) => { if ((t.Text ?? "").Trim() != Js(d.List("rechnungen").ElementAtOrDefault(i)?[feld])) Commit(); };
                t.KeyDown += (_, e) => { if (e.Key == Key.Enter) { e.Handled = true; Commit(); } };
                return t;
            }
            sp.Children.Add(P("Variable (leer = automatisch)", Muted, 11)); sp.Children.Add(Feld("mh-prop-variable", "variable", "z. B. x oder t"));
            sp.Children.Add(P("Grenzen für ein bestimmtes Integral", Muted, 11));
            sp.Children.Add(new Grid { ColumnDefinitions = new ColumnDefinitions("*,8,*"), Children = { Feld("mh-prop-von", "von", "von"), Pos(Feld("mh-prop-bis", "bis", "bis"), 2) } });
        }
        sp.Children.Add(H("Graph", 13));
        sp.Children.Add(new Grid { ColumnDefinitions = new ColumnDefinitions("*,8,*"), Children = { KrBox(d, "graphVon", "mh-graph-von", "x von"), Pos(KrBox(d, "graphBis", "mh-graph-bis", "x bis"), 2) } });
        return sp;
    }
}
