using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.Highlighting;
using Trendo.Core;
using Trendo.Core.Lernen;
using Trendo.Core.Programs;

namespace Trendo.App;

/// <summary>T-0782 STUDIO-CODELEARN-01 „Programmieren lernen“: Lernpfad Python links (Konzepte, Freischaltung), Anleitung + Editor + Testergebnis in der Mitte
/// (wie eine Lernumgebung mit Erklärtext, Editor und Ausgabe nebeneinander), Hinweise rechts. Bewertet wird nur durch die echten Tests im AppContainer
/// (<see cref="LernSandbox"/>: ohne Netz, Zeitgrenze 10 s, nur Temp-Ordner beschreibbar). Die KI gibt nur Hinweise, sie bewertet nie.
/// Vorbilder (nur Arbeitsweise): Exercism, Codecademy, freeCodeCamp, Scratch. Anforderungen: docs/nachbau/programmieren-lernen/anforderungen.md.</summary>
public partial class MainWindow
{
    public const string CodeLearnKey = "programmieren-lernen-programm";
    const string CodeLearnStudio = "STUDIO-CODELEARN-01";
    /// <summary>Tests: Werkzeugordner mit dem mitgelieferten Python (sonst der von Trendo).</summary>
    public static string? CodeLearnToolsRoot { get; set; }
    TextDocument? _clDoc; string? _clFor; bool _clBusy; string _clKi = "";
    DispatcherTimer? _clTimer;
    /// <summary>Tests: wartet auf den letzten Testlauf.</summary>
    public Task? CodeLearnRun { get; private set; }

    (string? Exe, string? Why) CodeLearnPython()
    {
        if (!OperatingSystem.IsWindows()) return (null, "BLOCKIERT: Der abgeschottete Lauf ist bisher nur unter Windows gebaut.");
        return LernSandbox.PrepareRuntime(_core.DataRoot, CodeLearnToolsRoot ?? _deps.ToolsRoot);
    }

    private void InitCodeLearn() => RegisterProgram(FachTabs, new ProgramSpec(CodeLearnKey, CodeLearnStudio, "Programmieren lernen")
    {
        Sentence = "Python Schritt für Schritt: Aufgabe lesen, Code schreiben, echte Tests laufen lassen – abgeschottet, ohne Internet. Hinweise statt fertiger Lösung, Fortschritt je Konzept.",
        Folder = "Programmieren lernen",
        ScrollWorkspace = true,
        GroupLevels = new() { ["Code"] = 0, ["Hilfe"] = 0 },
        Templates =
        [
            new("leer", "Python-Lernpfad", "Neun Aufgaben von „Begrüßung“ bis „Wörter zählen“", () => CodeLernen.Neu()),
        ],
        Ribbon =
        [
            ("Code", [
                new ProgramCmd("cl-test", "Tests ausführen", d => CodeLearnTest(d), Primary: true, Tip: "Die echten Tests der Aufgabe laufen abgeschottet (F5)."),
                new ProgramCmd("cl-reset", "Zurücksetzen", d => { CodeLearnCommit(d); CodeLernen.Zuruecksetzen(d); _clFor = null; _progMsg[CodeLearnKey] = "Startcode wiederhergestellt – Strg+Z holt deinen Code zurück."; }, Tip: "Startcode der Aufgabe – rückgängig machbar."),
                new ProgramCmd("cl-import", "Python-Datei öffnen …", d => _ = CodeLearnPickImport(d), Tip: "Eigene .py-Datei als Code dieser Aufgabe.", Level: 1),
            ]),
            ("Hilfe", [
                new ProgramCmd("cl-hint", "Hinweis", d => { var h = CodeLernen.Hinweis(d); _progMsg[CodeLearnKey] = h is null ? "Alle Hinweise sind schon zu sehen – frag die KI nach einem weiteren." : "Hinweis: " + h; }, Tip: "Ein Denkanstoß, keine Lösung."),
                new ProgramCmd("cl-ki", "KI-Hinweis", d => _ = CodeLearnAskAsync(d, "Gib mir einen kleinen Hinweis für den nächsten Schritt, ohne die Lösung zu verraten."), Tip: "Die KI gibt nur Hinweise; ob es stimmt, sagen die Tests."),
            ]),
        ],
        KiActions = [CodeLearnKiHint],
        KiHint = "Mit „hinweis.geben“ schreibst du GENAU EINEN Hinweis zur aktuellen Aufgabe ins Dokument: ein Denkanstoß in 1–3 Sätzen, KEIN fertiger Code, kein Code-Block, keine Funktionsdefinition, keine Links. "
               + "Du bewertest den Code nie – das machen die echten Tests. Den Code der lernenden Person änderst du nicht.",
        KiLocked = ["code", "stand", "kiHinweise", "aufgabe", "sprache", "begonnen"],
        KiLocalOnly = () => _roleChild,
        KiView = CodeLearnKiView, // M2: keine Programmausgaben und Testmeldungen an die KI-Kette
        Exports =
        [
            new("py", "Lösung mit Tests (.py)", ".py", (d, f) => { CodeLearnCommit(d); File.WriteAllText(f, CodeLernen.Export(d), new UTF8Encoding(false)); return f; }),
        ],
        SimpleExports = 1,
        Menus = d =>
        [
            new("Code",
            [
                new("cl-key-test", "Tests ausführen", () => ProgramRun(CodeLearnKey, () => CodeLearnTest(d)), "F5"),
                new("cl-key-hint", "Hinweis", () => ProgramRun(CodeLearnKey, () => { var h = CodeLernen.Hinweis(d); _progMsg[CodeLearnKey] = h is null ? "Alle Hinweise sind schon zu sehen." : "Hinweis: " + h; }), "Strg+J"),
            ]),
        ],
        Blocker = _ => OperatingSystem.IsWindows() && LernSandbox.BundledPython(CodeLearnToolsRoot ?? _deps.ToolsRoot) is null ? new ProgramBlocker("Das mitgelieferte Python fehlt – ohne es laufen keine Tests", "Zusatzprogramme öffnen", () => Show("components")) : null,
        FirstStep = new("Mit der ersten Aufgabe beginnen", "Lies die Aufgabe, schreib deinen Code in den Editor und starte dann die echten Tests (F5) – abgeschottet, ohne Internet. Hinweise gibt es jederzeit.",
            d => { d.Set("begonnen", true, "Lernpfad begonnen"); _progMsg[CodeLearnKey] = "Los geht’s: Schreib deinen Code in den Editor, dann „Tests ausführen“."; },
            d => !d.Flag("begonnen") && (d.Data["stand"] as System.Text.Json.Nodes.JsonObject)?.Count is null or 0),
        Workspace = CodeLearnWorkspace,
        Properties = CodeLearnProperties,
        Status = d => { var a = CodeLernen.Aktuell(d); var l = CodeLernen.Letzter(d); return $"Python · {a.Titel} ({CodeLernen.KonzeptName(a.Konzept)}) · {CodeLernen.Track.Count(x => CodeLernen.Geloest(d, x.Id))}/{CodeLernen.Track.Count} gelöst" + (l is null ? "" : $" · letzter Lauf: {l.Bestanden}/{l.Tests.Count} Tests"); },
        Help = "Links eine Aufgabe wählen (die nächste schaltet sich frei, wenn die vorige gelöst ist). In der Mitte steht, was zu tun ist; darunter schreibst du den Code. "
             + "„Tests ausführen“ (F5) startet die echten Tests der Aufgabe in einem abgeschotteten Bereich von Windows (App-Container): ohne Internet, mit 10 Sekunden Zeitgrenze, "
             + "ohne Zugriff auf deine Dateien und deinen Bildschirm (eigener, unsichtbarer Desktop). Ehrlich: Rechner- und Benutzername und Teile der Registrierung "
             + "(z. B. zuletzt geöffnete Dateinamen) kann der Code dort trotzdem lesen – das lässt sich in einem App-Container nicht ganz sperren. Was dein Programm ausgibt, geht nicht an Claude. "
             + "Für den abgeschotteten Bereich legt Windows einmalig ein App-Profil „Trendo.Lernen.Code“ an; Trendo entfernt es beim Deinstallieren wieder. "
             + "Grün heißt bestanden. „Hinweis“ gibt Denkanstöße; die KI gibt ebenfalls nur Hinweise und bewertet nie. Export: Lösung mit Tests als .py, läuft in jedem Python.",
    }, "programmieren lernen", "python lernen", "coden lernen", "programmieren", "exercism", "codecademy", "freecodecamp", "scratch", "aufgaben mit tests", "informatik");

    ProgramKiAction CodeLearnKiHint => new(new AssistAction("hinweis.geben", "Hinweis geben", ["hinweis", "tipp", "hilfe", "erklär", "erklaer", "warum", "fehler"],
        [new AssistParam("hinweis", "Hinweis")]),
        (d, a) => CodeLernen.KiHinweisAnlegen(d, a.GetValueOrDefault("hinweis") ?? ""),
        (_, _) => [])
    { ApplyNote = "Der Hinweis steht erst nach „Ja, behalten“ bei der Aufgabe – „Rückgängig“ nimmt ihn wieder heraus. Bewertet wird nur durch die Tests." };

    /// <summary>Getippten Code ins Dokument übernehmen (ein Schritt).</summary>
    void CodeLearnCommit(ProgramDocument d)
    {
        _clTimer?.Stop();
        if (_clFor == d.Id + "|" + CodeLernen.Aktuell(d).Id && _clDoc is not null && _clDoc.Text != CodeLernen.Code(d)) CodeLernen.CodeSetzen(d, _clDoc.Text);
    }

    void CodeLearnTest(ProgramDocument d)
    {
        if (_clBusy) { _progMsg[CodeLearnKey] = "Die Tests laufen schon."; return; }
        CodeLearnCommit(d);
        var (py, why) = CodeLearnPython();
        if (py is null) throw new InvalidOperationException(why ?? "Python fehlt.");
        var a = CodeLernen.Aktuell(d); var code = CodeLernen.Code(d);
        var work = Path.Combine(Path.GetTempPath(), "trendo-lernen", Guid.NewGuid().ToString("N")[..10]);
        _clBusy = true; _progMsg[CodeLearnKey] = "Tests laufen – abgeschottet, ohne Internet …";
        _core.Audit.Append(CurrentPrincipal.Name, "lernen.code.test", $"{a.Id} zeichen={code.Length}");
        CodeLearnRun = Task.Run(() => { try { return CodeLernen.Testen(a, code, py, work); } catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception) { return new CodeLernen.Bericht("error", [], "Der abgeschottete Lauf ging nicht: " + e.Message, 0); } })
            .ContinueWith(t => _ui.Post(() =>
            {
                _clBusy = false;
                var b = t.Result;
                if (CodeLernen.Aktuell(d).Id == a.Id) CodeLernen.Eintragen(d, b);
                _core.Audit.Append(CurrentPrincipal.Name, "lernen.code.ergebnis", $"{a.Id} {b.Status} {b.Bestanden}/{b.Tests.Count}");
                _progMsg[CodeLearnKey] = b.Status switch
                {
                    "pass" => $"✓ Alle {b.Tests.Count} Tests bestanden!" + (CodeLernen.Track.SkipWhile(x => x.Id != a.Id).Skip(1).FirstOrDefault() is { } nx ? $" Weiter mit „{nx.Titel}“." : " Lernpfad geschafft."),
                    "fail" => $"{b.Bestanden} von {b.Tests.Count} Tests bestanden – unten steht, was noch nicht stimmt.",
                    "timeout" => "⏱ " + b.Meldung,
                    _ => "Der Code ließ sich nicht ausführen – unten steht der Fehler.",
                };
                Show(CodeLearnKey);
            }));
    }

    async Task CodeLearnPickImport(ProgramDocument d)
    {
        if (await PickMediaFile("cl-import", "Python-Datei", ["*.py", "*.txt"]) is { } f) ProgramRun(CodeLearnKey, () => CodeLearnImport(d, f));
    }

    public void CodeLearnImport(ProgramDocument d, string file)
    {
        if (new FileInfo(file).Length > 200_000) throw new InvalidDataException("Die Datei ist zu groß (höchstens 200 KB).");
        CodeLernen.CodeSetzen(d, CodeLernen.ImportText(File.ReadAllText(file)), "Python-Datei eingelesen");
        _clFor = null;
        _progMsg[CodeLearnKey] = $"„{Path.GetFileName(file)}“ ist jetzt dein Code für „{CodeLernen.Aktuell(d).Titel}“ – Strg+Z nimmt es zurück.";
    }

    /// <summary>KI um einen Hinweis bitten – im Kinderprofil nur die KI auf diesem PC. Die Antwort ist nur Anzeige; ein Code-Block wird nicht gezeigt.</summary>
    public async Task CodeLearnAskAsync(ProgramDocument d, string frage)
    {
        if (_clBusy) return;
        CodeLearnCommit(d);
        _clKi = "Die KI denkt nach …"; Show(CodeLearnKey);
        IntelligenceCore core; string? pref;
        if (_roleChild) core = AgentKi("lokal", out pref); else { core = Intel; pref = KiModelName; }
        // M2: Programmausgabe und Testmeldungen nur an die KI auf diesem PC (Kinderprofil oder keine Internet-/Claude-Quelle im Spiel)
        var local = _roleChild;
        var a = await core.AskAsync(new KiRequest(CodeLernen.KiFrage(d, frage, mitAusgabe: local), CodeLernen.KiRolle, pref, UseKnowledge: false));
        var text = Nachhilfe.OhneLinks(System.Text.RegularExpressions.Regex.Replace(a.Text, @"<think>[\s\S]*?</think>", "").Trim());
        _clKi = !a.Ok ? "Die KI antwortet gerade nicht: " + a.Text
            : CodeLernen.HinweisProblem(CodeLernen.Aktuell(d), text) is { } p && p.Contains("Code") ? "Die KI hat fertigen Code geschrieben – den zeigt Trendo nicht, du sollst ja selbst darauf kommen. Frag nach einem kleineren Hinweis."
            : text;
        _core.Audit.Append(CurrentPrincipal.Name, "lernen.code.ki", $"{CodeLernen.Aktuell(d).Id} zeichen={a.Text.Length} quelle={a.Backend}");
        Show(CodeLearnKey);
    }

    Control CodeLearnWorkspace(ProgramDocument d)
    {
        var cur = CodeLernen.Aktuell(d);
        var left = new StackPanel { Name = "cl-pfad", Spacing = 4, Width = 250 };
        left.Children.Add(H("Lernpfad Python", 14));
        left.Children.Add(new ProgressBar { Name = "cl-fortschritt", Minimum = 0, Maximum = 100, Value = CodeLernen.Fortschritt(d), Height = 8, Foreground = Accent });
        left.Children.Add(P($"{CodeLernen.Track.Count(x => CodeLernen.Geloest(d, x.Id))} von {CodeLernen.Track.Count} Aufgaben gelöst", Muted, 11));
        var i = 0;
        foreach (var g in CodeLernen.Track.GroupBy(a => a.Konzept))
        {
            left.Children.Add(new TextBlock { Text = CodeLernen.KonzeptName(g.Key).ToUpperInvariant(), Foreground = Muted, FontSize = 10.5, FontWeight = FontWeight.Bold, Margin = new Thickness(0, 6, 0, 0) });
            foreach (var a in g)
            {
                var aa = a; var free = CodeLernen.Frei(d, a); var done = CodeLernen.Geloest(d, a.Id); var on = a.Id == cur.Id;
                var b = new Button { Name = "cl-aufgabe-" + i++, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left, Padding = new Thickness(10, 5), IsEnabled = free,
                    Background = on ? Brush.Parse("#33D4A017") : Brushes.Transparent, BorderBrush = on ? Accent : Brush.Parse("#22FFFFFF"), BorderThickness = new Thickness(1),
                    Content = new TextBlock { Text = (done ? "✓ " : free ? "○ " : "🔒 ") + a.Titel, Foreground = done ? K4Good : free ? Text : Muted, FontSize = 13 } };
                if (!free) ToolTip.SetTip(b, $"Erst „{CodeLernen.ById(a.Braucht)!.Titel}“ lösen.");
                b.Click += (_, _) => ProgramRun(CodeLearnKey, () => { CodeLearnCommit(d); CodeLernen.Waehlen(d, aa.Id); _clFor = null; _clKi = ""; });
                left.Children.Add(b);
            }
        }

        var center = new StackPanel { Name = "cl-mitte", Spacing = 10 };
        center.Children.Add(KrCard(new StackPanel { Spacing = 4, Children = {
            new TextBlock { Text = CodeLernen.KonzeptName(cur.Konzept), Foreground = Muted, FontSize = 11 }, H(cur.Titel, 16),
            new SelectableTextBlock { Name = "cl-anleitung", Text = cur.Anleitung.Replace("`", ""), Foreground = Text, FontSize = 13, TextWrapping = TextWrapping.Wrap } } }, "cl-aufgabe-karte"));

        // Editor: Getipptes wird nach einer Sekunde Pause ein Rückgängig-Schritt im Dokument
        var key = d.Id + "|" + cur.Id;
        if (_clFor != key || _clDoc is null) { _clDoc = new TextDocument(CodeLernen.Code(d)); _clFor = key; }
        else if (_clDoc.Text != CodeLernen.Code(d) && !(_clTimer?.IsEnabled ?? false)) _clDoc.Text = CodeLernen.Code(d); // Rückgängig/Wiederherstellung von außen
        CodeDarkColors();
        var ed = new TextEditor { Name = "cl-editor", Document = _clDoc, ShowLineNumbers = true, FontFamily = new FontFamily("Cascadia Code, Consolas, Menlo, monospace"), FontSize = 14, Height = 300,
            Background = Brush.Parse("#0E1117"), Foreground = Brush.Parse("#D4D4D4"), LineNumbersForeground = Brush.Parse("#5A6475"), Padding = new Thickness(6, 4),
            SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("Python") };
        ed.Options.ConvertTabsToSpaces = true; ed.Options.IndentationSize = 4; ed.Options.EnableHyperlinks = false;
        _clTimer ??= new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        var doc = d;
        _clTimer.Tick -= CodeLearnTick; _clTimer.Tick += CodeLearnTick;
        ed.TextChanged += (_, _) => { _clTimer.Stop(); _clTimer.Start(); };
        ed.KeyDown += (_, e) => { if (e.Key == Key.F5) { e.Handled = true; ProgramRun(CodeLearnKey, () => CodeLearnTest(doc)); } };
        var run = new Button { Name = "cl-run", Content = _clBusy ? "Tests laufen …" : "▶ Tests ausführen", Padding = new Thickness(12, 5), IsEnabled = !_clBusy };
        run.Click += (_, _) => ProgramRun(CodeLearnKey, () => CodeLearnTest(d));
        center.Children.Add(KrCard(new StackPanel { Spacing = 6, Children = {
            new DockPanel { Children = { new Border { [DockPanel.DockProperty] = Avalonia.Controls.Dock.Right, Child = run }, new TextBlock { Text = "loesung.py · Python", Foreground = Muted, FontSize = 12, VerticalAlignment = VerticalAlignment.Center } } },
            new Border { CornerRadius = new CornerRadius(6), ClipToBounds = true, Child = ed } } }, "cl-editor-karte"));

        // Testergebnis (nur echte Tests)
        var res = new StackPanel { Name = "cl-ergebnis", Spacing = 4 };
        res.Children.Add(new TextBlock { Text = "Testergebnis (echte Tests, abgeschottet ohne Internet)", Foreground = Muted, FontSize = 11 });
        if (CodeLernen.Letzter(d) is { } l)
        {
            res.Children.Add(new TextBlock { Name = "cl-status", Text = l.Status switch { "pass" => $"✓ Bestanden – {l.Bestanden}/{l.Tests.Count}", "fail" => $"✕ {l.Bestanden}/{l.Tests.Count} bestanden", "timeout" => "⏱ Zeitgrenze", _ => "✕ Fehler" },
                Foreground = l.Status == "pass" ? K4Good : K4Bad, FontSize = 15, FontWeight = FontWeight.SemiBold });
            if (l.Meldung.Length > 0) res.Children.Add(new SelectableTextBlock { Name = "cl-meldung", Text = l.Meldung, Foreground = Text, FontFamily = new FontFamily("Cascadia Code, Consolas, monospace"), FontSize = 12, TextWrapping = TextWrapping.Wrap });
            var n = 0;
            foreach (var t in l.Tests)
            {
                var row = new StackPanel { Name = "cl-test-" + n++, Spacing = 1 };
                row.Children.Add(new TextBlock { Text = (t.Status == "pass" ? "✓ " : "✕ ") + t.Name.Replace("test_", "").Replace('_', ' '), Foreground = t.Status == "pass" ? K4Good : K4Bad, FontSize = 12.5 });
                if (t.Meldung.Length > 0) row.Children.Add(new SelectableTextBlock { Text = t.Meldung, Foreground = Muted, FontFamily = new FontFamily("Cascadia Code, Consolas, monospace"), FontSize = 11.5, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(16, 0, 0, 0) });
                if (t.Ausgabe.Length > 0) row.Children.Add(new SelectableTextBlock { Text = "Ausgabe: " + t.Ausgabe.TrimEnd(), Foreground = Muted, FontSize = 11.5, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(16, 0, 0, 0) });
                res.Children.Add(row);
            }
        }
        else res.Children.Add(P("Noch nicht getestet. „Tests ausführen“ (F5) prüft deinen Code mit den Tests der Aufgabe.", Muted, 12));
        res.Children.Add(new TextBlock { Name = "cl-hinweis-sicherheit", TextWrapping = TextWrapping.Wrap, FontSize = 11, Foreground = Muted,
            Text = "Abgeschottet: kein Internet, keine deiner Dateien, nicht dein Bildschirm. Lesbar bleiben Rechner- und Benutzername und Teile der Registrierung (z. B. zuletzt geöffnete Dateinamen). Programmausgaben gehen nicht an Claude. "
                 + "Windows legt dafür einmalig das App-Profil „Trendo.Lernen.Code“ an – Trendo entfernt es beim Deinstallieren." });
        center.Children.Add(KrCard(res, "cl-ergebnis-karte"));

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,16,*") };
        grid.Children.Add(left); Grid.SetColumn(left, 0);
        grid.Children.Add(center); Grid.SetColumn(center, 2);
        BueroFileDrop(grid, CodeLearnKey, BueroExt(".py"), "Python-Dateien (.py)", (dd, files) => CodeLearnImport(dd, files[0]));
        return grid;
    }

    /// <summary>M2: Sicht der KI-Kette – ohne Programmausgaben und Testmeldungen (sie enthalten Werte, die der Lerncode erzeugt hat).</summary>
    static System.Text.Json.Nodes.JsonObject CodeLearnKiView(System.Text.Json.Nodes.JsonObject data)
    {
        var c = data.DeepClone().AsObject();
        if (c["stand"] is System.Text.Json.Nodes.JsonObject st)
            foreach (var (_, v) in st)
                if (v?["letzter"] is System.Text.Json.Nodes.JsonObject l)
                {
                    l.Remove("meldung");
                    if (l["tests"] is System.Text.Json.Nodes.JsonArray ts) foreach (var t in ts.OfType<System.Text.Json.Nodes.JsonObject>()) { t.Remove("meldung"); t.Remove("ausgabe"); }
                }
        return c;
    }

    void CodeLearnTick(object? s, EventArgs e) { _clTimer!.Stop(); if (ProgramActive(CodeLearnKey) is { } a && _clDoc is not null && _clFor == a.Id + "|" + CodeLernen.Aktuell(a).Id && _clDoc.Text != CodeLernen.Code(a)) CodeLernen.CodeSetzen(a, _clDoc.Text); }

    Control CodeLearnProperties(ProgramDocument d)
    {
        var a = CodeLernen.Aktuell(d);
        var sp = new StackPanel { Spacing = 8, Width = 290 };
        sp.Children.Add(H("Hinweise", 13));
        var shown = CodeLernen.HinweiseGezeigt(d, a.Id);
        for (var i = 0; i < shown && i < a.Hinweise.Count; i++) sp.Children.Add(new TextBlock { Name = "cl-hinweis-" + i, Text = $"💡 {a.Hinweise[i].Replace("`", "")}", Foreground = Brush.Parse("#FDE68A"), FontSize = 12.5, TextWrapping = TextWrapping.Wrap });
        var hb = new Button { Name = "cl-hinweis", Content = shown < a.Hinweise.Count ? $"Hinweis {shown + 1} von {a.Hinweise.Count} zeigen" : "Alle Hinweise gezeigt", IsEnabled = shown < a.Hinweise.Count, Padding = new Thickness(10, 4) };
        hb.Click += (_, _) => ProgramRun(CodeLearnKey, () => CodeLernen.Hinweis(d));
        sp.Children.Add(hb);
        var ki = CodeLernen.KiHinweise(d, a.Id);
        for (var i = 0; i < ki.Count; i++) sp.Children.Add(new TextBlock { Name = "cl-kihinweis-" + i, Text = "🤖 " + ki[i], Foreground = Brush.Parse("#C4B5FD"), FontSize = 12.5, TextWrapping = TextWrapping.Wrap });
        sp.Children.Add(H("KI fragen", 13));
        var q = new TextBox { Name = "cl-ki-frage", PlaceholderText = "z. B. „Warum schlägt der Test fehl?“", AcceptsReturn = false };
        var ask = new Button { Name = "cl-ki-fragen", Content = "Hinweis von der KI", Padding = new Thickness(10, 4) };
        ask.Click += (_, _) => _ = CodeLearnAskAsync(d, q.Text is { Length: > 0 } t ? t : "Gib mir einen kleinen Hinweis.");
        sp.Children.Add(q); sp.Children.Add(ask);
        if (_clKi.Length > 0) sp.Children.Add(new SelectableTextBlock { Name = "cl-ki-antwort", Text = _clKi, Foreground = Text, FontSize = 12.5, TextWrapping = TextWrapping.Wrap });
        sp.Children.Add(P("Die KI gibt nur Hinweise. Ob dein Code stimmt, entscheiden allein die Tests." + (_roleChild ? " Im Kinderprofil fragt Trendo nur die KI auf diesem PC." : ""), Muted, 11));
        sp.Children.Add(H("Stand", 13));
        sp.Children.Add(P($"Versuche bei dieser Aufgabe: {CodeLernen.Versuche(d, a.Id)}" + (CodeLernen.Geloest(d, a.Id) ? " · ✓ gelöst" : ""), Muted, 11.5));
        return sp;
    }
}
