using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;
using SkiaSharp;
using Trendo.App;
using Trendo.Core;
using Trendo.Core.Media;
using Trendo.Core.Programs;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>T-0686 Design-Paket 2 (Bild-Audit V-01, V-02, V-03, V-04, V-08): die gemeinsame Programm-Hülle – schmaler Kopf, ein goldener Knopf,
/// Werkzeugleiste mit Wort, erster Schritt im leeren Dokument, Speicherstand an einer Stelle. Geprüft wird über alle Hüllen-Programme.</summary>
public class ProgramHeadDesign_Ui(ITestOutputHelper log)
{
    static void Settle() { for (var i = 0; i < 6; i++) Dispatcher.UIThread.RunJobs(); }

    /// <summary>Suche nur in der offenen Seite – andere Fenster (frühere Programme) zählen nicht.</summary>
    static T Find<T>(MainWindow w, string name) where T : Control => w.ActivePageRoot!.GetLogicalDescendants().OfType<T>().First(c => c.Name == name);
    static T? TryFind<T>(MainWindow w, string name) where T : Control => w.ActivePageRoot!.GetLogicalDescendants().OfType<T>().FirstOrDefault(c => c.Name == name);
    static Panel Root(MainWindow w) => w.ActivePageRoot!.GetLogicalDescendants().OfType<Panel>().First(p => p.Name == "prog-root");
    static List<Button> Gold(Control root) => root.GetLogicalDescendants().OfType<Button>().Where(MainWindow.IsGold).ToList();

    /// <summary>Programm mit seiner ersten Vorlage öffnen (frisches Dokument).</summary>
    static ProgramDocument OpenDoc(MainWindow w, string key)
    {
        foreach (var old in w.Windows.Stack.Reverse().Skip(3).ToList()) w.CloseWindow(old.Id);
        while (w.ProgramDocs(key).Count > 0) w.ProgramClose(key, 0);
        w.Show(key);
        var d = w.ProgramNew(key, MainWindow.Programs[key].Templates.FirstOrDefault()?.Id);
        w.Show(key);
        if (w.Windows.Active is { Maximized: false } win) { w.Windows.ToggleMaximize(win.Id); w.Show(key); } // wie im Bild-Audit: Fenster des Programms füllt den Bildschirm
        Settle(); w.UpdateLayout(); Settle();
        return d;
    }

    static IEnumerable<string> Keys => MainWindow.Programs.Keys.OrderBy(k => k, StringComparer.Ordinal).ToList();

    [AvaloniaFact(DisplayName = "V-03: je Ansicht höchstens ein goldener Knopf – über alle Hüllen-Programme, Startseite und Dokument, „Einfach“ und „Erweitert“; „Speichern“ ist nie golden, kein Knopf trägt mehr die blaue Akzentklasse")]
    public void One_gold_button_per_view()
    {
        var (w, _, _) = Ui.Open(); w.Width = 1600; w.Height = 1000;
        var bad = new List<string>(); var none = new List<string>(); var views = 0;
        try
        {
            Assert.True(MainWindow.Programs.Count >= 30, $"nur {MainWindow.Programs.Count} Hüllen-Programme");
            foreach (var level in new[] { UserLevel.Einfach, UserLevel.Erweitert })
            {
                w.ProgramSetLevel(level);
                foreach (var key in Keys)
                {
                    var spec = MainWindow.Programs[key];
                    while (w.ProgramDocs(key).Count > 0) w.ProgramClose(key, 0);
                    w.Show(key); Settle(); views++;
                    var start = Gold(Root(w));
                    if (start.Count > 1) bad.Add($"{key} Startseite/{UserLevel.Id(level)}: {string.Join(", ", start.Select(b => b.Name))}");

                    var d = OpenDoc(w, key); views++;
                    var root = Root(w);
                    var gold = Gold(root);
                    if (gold.Count > 1) bad.Add($"{key} Dokument/{UserLevel.Id(level)}: {string.Join(", ", gold.Select(b => b.Name))}");
                    if (gold.Any(b => b.Name == "prog-save")) bad.Add($"{key}: Speichern ist golden");
                    if (root.GetLogicalDescendants().OfType<Button>().Any(b => b.Classes.Contains("accent"))) bad.Add($"{key}/{UserLevel.Id(level)}: Knopf mit Akzentklasse (blau)");
                    // Regel: der erste Schritt des leeren Dokuments, sonst der erste sichtbare Hauptbefehl (Primary) des Programms
                    var firstStep = spec.FirstStep is { } fs && fs.Empty(d);
                    var primary = spec.Ribbon.Where(g => UserLevel.Shows(level, spec.GroupLevels.GetValueOrDefault(g.Group))).SelectMany(g => g.Cmds).FirstOrDefault(c => c.Primary && UserLevel.Shows(level, spec.LevelOf(c)));
                    var expect = firstStep ? "prog-first-step" : primary is null ? null : "prog-cmd-" + primary.Name;
                    if (expect is null) { if (gold.Count == 0) none.Add($"{key}/{UserLevel.Id(level)}"); }
                    else if (gold.Count != 1 || gold[0].Name != expect) bad.Add($"{key} Dokument/{UserLevel.Id(level)}: erwartet {expect}, golden ist {string.Join(", ", gold.Select(b => b.Name).DefaultIfEmpty("nichts"))}");
                    w.ProgramClose(key, 0);
                }
            }
        }
        finally { w.ProgramSetLevel(UserLevel.Erweitert); w.Close(); }
        log.WriteLine($"{views} Ansichten geprüft; ohne Hauptbefehl (kein goldener Knopf): {string.Join(", ", none)}");
        Assert.Empty(bad);
    }

    /// <summary>Obergrenze: Die Arbeitsfläche beginnt in „Einfach“ bei 1600 × 1000 spätestens hier (30 % der Fensterhöhe).</summary>
    public const double WorkspaceTopMax = 300;

    [AvaloniaFact(DisplayName = "V-01: schmaler Kopf – in jedem Hüllen-Programm beginnt die Arbeitsfläche in „Einfach“ (1600 × 1000) bei höchstens 30 % der Fensterhöhe; eine Kopfzeile mit Programmname, Titel und Speicherstand, keine Kopfkarte, keine Hinweis-Karte")]
    public void Workspace_starts_in_upper_third()
    {
        var (w, _, _) = Ui.Open(); w.Width = 1600; w.Height = 1000;
        var bad = new List<string>(); var report = new List<string>();
        try
        {
            w.ProgramSetLevel(UserLevel.Einfach);
            foreach (var key in Keys)
            {
                var spec = MainWindow.Programs[key];
                OpenDoc(w, key);
                var root = Root(w);
                var ws = root.GetLogicalDescendants().OfType<Border>().First(b => b.Name == "prog-workspace");
                var top = ws.TranslatePoint(new Point(0, 0), w)?.Y ?? double.NaN;
                report.Add($"{key}\t{top:0}");
                if (!(top <= WorkspaceTopMax)) bad.Add($"{key}: Arbeitsfläche beginnt bei {top:0} px");
                // eine Zeile: Name des Programms, Titel des Dokuments, Speicherstand – auf gleicher Höhe
                var name = root.GetLogicalDescendants().OfType<TextBlock>().Single(t => t.Name == "prog-name");
                var title = root.GetLogicalDescendants().OfType<TextBox>().Single(t => t.Name == "prog-title");
                var state = root.GetLogicalDescendants().OfType<TextBlock>().Single(t => t.Name == "prog-dirty");
                Assert.Equal(spec.Title, name.Text);
                double Mid(Control c) => c.TranslatePoint(new Point(0, c.Bounds.Height / 2), w)!.Value.Y;
                if (Math.Abs(Mid(name) - Mid(title)) > 8 || Math.Abs(Mid(name) - Mid(state)) > 8) bad.Add($"{key}: Name, Titel und Speicherstand stehen nicht in einer Zeile");
                if (state.TranslatePoint(new Point(state.Bounds.Width, 0), w)!.Value.X > w.Width) bad.Add($"{key}: Speicherstand ragt aus dem Fenster");
                // die alten Streifen sind weg: Kopfkarte der Seite und Hinweis-Karte mit „Kopieren“
                if (w.ActivePageRoot!.GetLogicalDescendants().OfType<Control>().Any(c => c.Name is "page-hero" or "result-card")) bad.Add($"{key}: Kopfkarte oder Hinweis-Karte über der Arbeitsfläche");
                w.ProgramClose(key, 0);
            }
        }
        finally { w.ProgramSetLevel(UserLevel.Erweitert); w.Close(); }
        log.WriteLine(string.Join("\n", report));
        File.WriteAllText(Path.Combine(Path.GetTempPath(), "trendo-t0686-arbeitsflaeche.txt"), string.Join(Environment.NewLine, report));
        Assert.Empty(bad);
    }

    [AvaloniaFact(DisplayName = "V-01: der kurze Hinweis („Vorlage … geöffnet.“) steht in der Kopfzeile; lange Ergebnisse und Fehler bleiben eine Karte; „?“ zeigt die Hilfe; ab zwei Dokumenten erscheinen die Reiter")]
    public void Head_line_details()
    {
        var (w, _, _) = Ui.Open(); w.Width = 1600; w.Height = 1000;
        try
        {
            const string key = "tagesplaner-programm"; // Programm ohne volle Fensterhöhe (hatte früher die sechs Streifen)
            OpenDoc(w, key);
            Assert.Contains("Neues Dokument", Find<SelectableTextBlock>(w, "prog-msg").Text);
            Assert.Null(TryFind<StackPanel>(w, "result-card"));
            Assert.NotNull(TryFind<Button>(w, "btn-help"));
            // ein Dokument: der Titel steht im Feld, der Reiter dazu ist ausgeblendet; „×“ und „＋“ bleiben
            Assert.False(Find<Button>(w, "prog-tab-0").IsVisible); Assert.True(Find<Button>(w, "prog-tab-new").IsVisible); Assert.True(Find<Button>(w, "prog-tab-close-0").IsVisible);
            Find<Button>(w, "prog-tab-new").Press(); Settle();
            Assert.Equal(2, w.ProgramDocs(key).Count);
            Assert.True(Find<Button>(w, "prog-tab-0").IsVisible); Assert.True(Find<Button>(w, "prog-tab-1").IsVisible);
            // Titel ändern
            var title = Find<TextBox>(w, "prog-title"); title.Focus(); Settle(); title.Text = "Mein Montag";
            Find<Button>(w, "btn-help").Focus(); Settle(); // Feld verlassen übernimmt den Namen
            Assert.Equal("Mein Montag", w.ProgramActive(key)!.Title);
            // Fehler bleibt gut sichtbar (Karte), nicht nur in der Kopfzeile
            Find<Button>(w, "prog-undo").Press(); Settle(); // es gibt nichts rückgängig zu machen
            Assert.Contains("nichts rückgängig", w.ProgramMessage(key));
            Assert.True(TryFind<StackPanel>(w, "error-card") is not null || TryFind<StackPanel>(w, "result-card") is not null);
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "V-08: Speicherstand nur an einer Stelle und verständlich – „Nicht gespeichert –“ mit Knopf „Speichern“ (ohne automatisches Speichern), „Wird automatisch gespeichert“, „Gespeichert um HH:MM“")]
    public void Save_state_once_and_clear()
    {
        var (w, _, _) = Ui.Open(); w.Width = 1600; w.Height = 1000;
        try
        {
            const string key = "mode-programm";
            // ohne automatisches Speichern (so laufen die abgeschotteten Tests): klar sagen, dass noch nichts gespeichert ist – mit dem Knopf daneben
            w.AutoSaveDocs = false;
            var d = OpenDoc(w, key);
            Assert.Equal("Nicht gespeichert –", Find<TextBlock>(w, "prog-dirty").Text);
            var now = Find<Button>(w, "prog-save-now"); Assert.True(now.IsVisible); Assert.Equal("Speichern", now.Content);
            now.Press(); Settle();
            Assert.NotNull(d.Path); Assert.True(File.Exists(d.Path));
            Assert.Matches(@"^Gespeichert um \d\d:\d\d$", Find<TextBlock>(w, "prog-dirty").Text!);
            Assert.False(Find<Button>(w, "prog-save-now").IsVisible);
            // die Statusleiste nennt nur noch den Dateinamen – kein zweiter Speicherstand, keine unerklärte Uhrzeit
            Assert.Equal(Path.GetFileName(d.Path), Find<TextBlock>(w, "prog-saved").Text);
            var texts = Root(w).GetLogicalDescendants().OfType<TextBlock>().Where(t => t.Name != "prog-dirty" && t.FindLogicalAncestorOfType<Button>() is null && t.FindLogicalAncestorOfType<TextBox>() is null)
                .Select(t => t.Text ?? "").Where(t => t.Contains("gespeichert", StringComparison.OrdinalIgnoreCase)).ToList();
            Assert.True(texts.All(t => t.StartsWith("Gespeichert: ", StringComparison.Ordinal)), string.Join(" | ", texts)); // nur die Meldung des Befehls „Gespeichert: <Datei>“
            w.ProgramClose(key, 0);

            // mit automatischem Speichern: nie „nicht gespeichert“
            w.AutoSaveDocs = true;
            d = OpenDoc(w, key);
            Assert.Equal("Wird automatisch gespeichert", Find<TextBlock>(w, "prog-dirty").Text);
            Assert.False(Find<Button>(w, "prog-save-now").IsVisible);
            Assert.Equal("", Find<TextBlock>(w, "prog-saved").Text);
            Find<Button>(w, "prog-cmd-variants").Press(); Settle();
            Assert.True(d.Dirty); Assert.Equal("Wird automatisch gespeichert", Find<TextBlock>(w, "prog-dirty").Text);
            var shown = Find<TextBlock>(w, "prog-dirty");
            Assert.Equal(1, w.ProgramAutosaveNow());
            Assert.Same(shown, Find<TextBlock>(w, "prog-dirty")); // nachgezogen, nicht neu gezeichnet
            Assert.Matches(@"^Gespeichert um \d\d:\d\d$", shown.Text!);
            Assert.Equal(Path.GetFileName(d.Path), Find<TextBlock>(w, "prog-saved").Text);
            Assert.DoesNotContain(Root(w).GetLogicalDescendants().OfType<TextBlock>(), t => (t.Text ?? "").Contains("nicht gespeichert", StringComparison.OrdinalIgnoreCase));
        }
        finally { w.AutoSaveDocs = false; w.Close(); }
    }

    [AvaloniaFact(DisplayName = "V-04: Werkzeugleiste – in „Einfach“ links mit Symbol und Wort, nichts abgeschnitten; in „Erweitert“ nur das Symbol mit Hinweis; das gewählte Werkzeug ist markiert, aber nicht golden gefüllt; Menüband bricht um")]
    public void Tool_rail_with_words()
    {
        var (w, _, _) = Ui.Open(); w.Width = 1600; w.Height = 1000;
        try
        {
            foreach (var key in Keys.Where(k => MainWindow.Programs[k].Tools.Count > 0))
            {
                var spec = MainWindow.Programs[key];
                w.ProgramSetLevel(UserLevel.Einfach);
                OpenDoc(w, key);
                var rail = Find<Panel>(w, "prog-tools"); var ws = Find<Border>(w, "prog-workspace");
                Assert.Equal(spec.Tools.Count, rail.Children.Count);
                Assert.True(rail.TranslatePoint(new Point(0, 0), w)!.Value.X < ws.TranslatePoint(new Point(0, 0), w)!.Value.X, $"{key}: Werkzeuge stehen nicht links");
                foreach (var (b, t) in rail.Children.OfType<Button>().Zip(spec.Tools))
                {
                    Assert.Equal("prog-cmd-" + t.Name, b.Name);
                    var words = b.GetLogicalDescendants().OfType<TextBlock>().ToList();
                    Assert.Contains(words, x => x.Text == t.Label); // das Wort steht am Knopf
                    Assert.False(SimpleWords.IsSymbolOnly(b));
                    var label = words.First(x => x.Text == t.Label);
                    // nichts abgeschnitten: das Wort passt vollständig in den Knopf und der Knopf in die Leiste
                    label.Measure(Size.Infinity);
                    Assert.True(label.DesiredSize.Width <= label.Bounds.Width + 0.5, $"{key}: „{t.Label}“ ist abgeschnitten ({label.DesiredSize.Width:0} > {label.Bounds.Width:0})");
                    Assert.True(b.Bounds.Right <= rail.Bounds.Width + 0.5, $"{key}: Knopf „{t.Label}“ ragt aus der Leiste");
                    Assert.False(MainWindow.IsGold(b));
                }
                var marks = rail.Children.OfType<Button>().GroupBy(b => (b.BorderBrush as Avalonia.Media.ISolidColorBrush)?.Color).OrderBy(g => g.Count()).ToList();
                Assert.True(marks.Count == 2 && marks[0].Count() == 1, $"{key}: genau ein Werkzeug soll markiert sein (anderer Rand)"); // gewählt = goldener Rand

                w.ProgramSetLevel(UserLevel.Erweitert); w.Show(key); Settle();
                rail = Find<Panel>(w, "prog-tools");
                foreach (var (b, t) in rail.Children.OfType<Button>().Zip(spec.Tools))
                {
                    Assert.True(b.Content as string == t.Icon || GlyphIcons.GetGlyph(b) == t.Icon.Trim(), $"{key}: „{t.Label}“ zeigt nicht nur sein Symbol"); // nur das Symbol (Zeichen oder Linien-Symbol)
                    Assert.DoesNotContain(b.GetLogicalDescendants().OfType<TextBlock>(), x => x.Text == t.Label);
                    Assert.StartsWith(t.Label, ToolTip.GetTip(b) as string); // das Wort steht im Hinweis
                }
                w.ProgramClose(key, 0);
            }

            // Menüband: kein Knopf ragt aus dem Fenster – auch nicht in einem schmalen Fenster (dann bricht es um)
            w.ProgramSetLevel(UserLevel.Erweitert);
            foreach (var width in new[] { 1600, 1000 })
            {
                w.Width = width; OpenDoc(w, "bild"); w.UpdateLayout(); Settle();
                var ribbon = Find<WrapPanel>(w, "prog-ribbon");
                var buttons = ribbon.GetVisualDescendants().OfType<Button>().Where(b => b.IsEffectivelyVisible && b.TemplatedParent is null).ToList();
                Assert.True(buttons.Count > 8);
                foreach (var b in buttons)
                    Assert.True(b.TranslatePoint(new Point(b.Bounds.Width, 0), w)!.Value.X <= width, $"Breite {width}: „{b.Name}“ ragt aus dem Fenster");
                // Abstand zwischen benachbarten Knöpfen einer Zeile
                // (ein schmales Fenster zeigt die Seite verkleinert – deshalb beide Kanten über TranslatePoint, nicht über Bounds.Width)
                var rows = buttons.Select(b => (B: b, P: b.TranslatePoint(new Point(0, 0), w)!.Value, R: b.TranslatePoint(new Point(b.Bounds.Width, 0), w)!.Value.X)).GroupBy(x => Math.Round(x.P.Y)).ToList();
                foreach (var row in rows)
                {
                    var line = row.OrderBy(x => x.P.X).ToList();
                    for (var i = 1; i < line.Count; i++)
                        Assert.True(line[i].P.X - line[i - 1].R >= 3, $"Breite {width}: „{line[i - 1].B.Name}“ (bis {line[i - 1].R:0}) und „{line[i].B.Name}“ (ab {line[i].P.X:0}) kleben aneinander");
                }
                w.ProgramClose("bild", 0);
            }
        }
        finally { w.ProgramSetLevel(UserLevel.Erweitert); w.Close(); }
    }

    static string Png(string root, string name)
    {
        var f = Path.Combine(root, name);
        using var bmp = new SKBitmap(64, 48); using (var c = new SKCanvas(bmp)) c.Clear(SKColors.Teal);
        using var img = SKImage.FromBitmap(bmp); using var data = img.Encode(SKEncodedImageFormat.Png, 100); File.WriteAllBytes(f, data.ToArray());
        return f;
    }

    [AvaloniaFact(DisplayName = "V-02: erster Schritt im leeren Dokument – der große Knopf löst in „Einfach“ die echte Funktion aus, obwohl die rechte Spalte eingeklappt ist (Videoschnitt, Bildbearbeitung, Audio, Punktwolke, Logos, Comics); danach ist er weg")]
    public void First_step_runs_the_real_function()
    {
        var (w, _, root) = Ui.Open(); w.Width = 1600; w.Height = 1000;
        try
        {
            w.ProgramSetLevel(UserLevel.Einfach);
            var asked = new List<MainWindow.PickRequest>();
            string[] pick = [];
            w.PickOverride = r => { asked.Add(r); return Task.FromResult<string[]?>(pick); };
            Button Step(string key)
            {
                var b = Find<Button>(w, "prog-first-step");
                Assert.True(b.IsEffectivelyVisible); Assert.True(MainWindow.IsGold(b));
                Assert.Null(TryFind<Border>(w, "prog-properties")); // die rechte Spalte ist in „Einfach“ eingeklappt
                // mittig auf der Arbeitsfläche
                var ws = Find<Border>(w, "prog-workspace"); var card = Find<Border>(w, "prog-first");
                var mid = card.TranslatePoint(new Point(card.Bounds.Width / 2, card.Bounds.Height / 2), ws)!.Value;
                var dy = mid.Y - ws.Bounds.Height / 2; // waagerecht genau mittig, senkrecht bis zu einem halben FirstStepLift über der Mitte
                Assert.True(Math.Abs(mid.X - ws.Bounds.Width / 2) < 12 && dy is < 12 and > -(MainWindow.FirstStepLift / 2 + 12), $"{key}: nicht mittig ({mid})");
                Assert.Equal(MainWindow.Programs[key].FirstStep!.Label, b.Content);
                return b;
            }

            // Videoschnitt: „Video hinzufügen“ → Dateiauswahl → Stück auf der Zeitleiste
            var d = OpenDoc(w, "schnitt");
            pick = [Png(root, "urlaub.png")];
            Step("schnitt").Press(); Settle();
            Assert.Equal("ve-add-pick", asked[^1].Field); Assert.True(asked[^1].Multiple);
            Assert.Single(VideoEdit.Clips(d.Data));
            Assert.Null(TryFind<Button>(w, "prog-first-step"));
            Assert.DoesNotContain(Root(w).GetLogicalDescendants().OfType<TextBlock>(), t => (t.Text ?? "").Contains("Rechts ", StringComparison.Ordinal)); // kein Hinweis mehr auf die eingeklappte Spalte
            w.ProgramClose("schnitt", 0);

            // Bildbearbeitung: „Foto öffnen“ → Ebene mit dem Foto, Leinwand so groß wie das Foto
            d = OpenDoc(w, "bild");
            pick = [Png(root, "foto.png")];
            Step("bild").Press(); Settle();
            Assert.Equal("img-open-pick", asked[^1].Field);
            Assert.Single(ImageDoc.Layers(d.Data)); Assert.Equal(64, d.Number("width"));
            Assert.Null(TryFind<Button>(w, "prog-first-step"));
            w.ProgramClose("bild", 0);

            // Audio: „Tondatei öffnen“ → Datei als Quelle
            d = OpenDoc(w, "ton");
            var wav = Path.Combine(root, "ton.wav"); AudioFile.WriteWav(AudioBuf.Silence(2, 8000, 1), wav);
            pick = [wav];
            Step("ton").Press(); Settle();
            Assert.Equal(Path.GetFullPath(wav), w.ProgramActive("ton")!.Text("source"));
            Assert.Null(TryFind<Button>(w, "prog-first-step"));
            w.ProgramClose("ton", 0);

            // Punktwolke: „Scan-Datei öffnen“
            d = OpenDoc(w, "punktwolke");
            var xyz = Path.Combine(root, "bauteil.xyz"); File.WriteAllText(xyz, string.Join("\n", Enumerable.Range(0, 50).Select(i => $"{i * 20} {i % 5 * 30} {i % 7 * 10}")));
            pick = [xyz];
            Step("punktwolke").Press(); Settle();
            Assert.Contains("bauteil.xyz", d.Data.ToJsonString());
            Assert.Null(TryFind<Button>(w, "prog-first-step"));
            w.ProgramClose("punktwolke", 0);

            // Abbrechen der Dateiauswahl ändert nichts, der Knopf bleibt
            d = OpenDoc(w, "bild"); pick = [];
            Step("bild").Press(); Settle();
            Assert.Empty(ImageDoc.Layers(d.Data)); Assert.NotNull(TryFind<Button>(w, "prog-first-step"));
            w.ProgramClose("bild", 0);

            // Logos und Comics: der erste Schritt holt die eingeklappten Eigenschaften (dort stehen Name, Sprechblasen, Bilder)
            foreach (var (key, field) in new[] { ("logo-programm", "logo-p-name"), ("comic-programm", "comic-speech-0") })
            {
                OpenDoc(w, key);
                Assert.Null(TryFind<TextBox>(w, field));
                Step(key).Press(); Settle();
                Assert.NotNull(TryFind<Border>(w, "prog-properties")); Assert.NotNull(TryFind<TextBox>(w, field));
                Assert.Null(TryFind<Button>(w, "prog-first-step"));
                Find<Button>(w, "prog-tools-more").Press(); Settle(); // wieder einklappen für das nächste Programm
                w.ProgramClose(key, 0);
            }

            // eine gefüllte Vorlage zeigt den Knopf nicht
            w.Show("comic-programm"); w.ProgramNew("comic-programm", "beispiel"); w.Show("comic-programm"); Settle();
            Assert.Null(TryFind<Button>(w, "prog-first-step"));
        }
        finally { w.ProgramSetLevel(UserLevel.Erweitert); w.Close(); }
    }
}
