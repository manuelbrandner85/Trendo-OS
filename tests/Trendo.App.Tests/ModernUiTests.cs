using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Trendo.App;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>Modernisierung 68 (freigegeben 07.11.2026): globale Punkte G1–G12.</summary>
public class Modern_Ui
{
    static void Pump() { for (var i = 0; i < 5; i++) Dispatcher.UIThread.RunJobs(); }

    [AvaloniaFact(DisplayName = "G2: Befehlsleiste mit Gruppen-Überschriften und Symbolen; ausgeführter Befehl steht danach oben unter „Zuletzt benutzt“")]
    public void Palette_recent_groups()
    {
        var (w, _, _) = Ui.Open();
        try
        {
            w.OpenPalette("Trendo Musik"); Pump();
            Assert.NotNull(w.TryFind<TextBlock>("command-group-0"));
            var i = w.PaletteResults.ToList().FindIndex(r => r.Label == "Trendo Musik" && r.Kind == "Studio öffnen");
            Assert.True(i >= 0);
            w.Find<Button>("command-hit-" + i).Press(); Pump();
            Assert.False(w.CommandPaletteOpen);
            Assert.Equal("Trendo Musik|Studio öffnen", w.PaletteRecent()[0]);
            w.OpenPalette(); Pump();
            Assert.Equal(("Trendo Musik", "Zuletzt benutzt"), w.PaletteResults[0]);
            Assert.Equal("ZULETZT BENUTZT", w.Find<TextBlock>("command-group-0").Text);
            w.Find<Button>("command-hit-0").Press(); Pump();
            Assert.Equal("Trendo Musik|Studio öffnen", Assert.Single(w.PaletteRecent()));
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "G9: Seitenleisten in Trendo Musik mit Griff; Breite gemerkt und beim nächsten Öffnen angewandt; Doppelklick klappt ein und zur letzten Breite aus")]
    public void Panes()
    {
        var (w, _, _) = Ui.Open();
        try
        {
            w.Show("musik-studio"); Pump();
            var grip = w.Find<GridSplitter>("pane-grip-musik-links");
            var grid = (Grid)grip.Parent!;
            Assert.Equal(360, grid.ColumnDefinitions[2].Width.Value);
            w.SavePaneWidth("musik/links", 420);
            w.Show("musik-studio"); Pump();
            grid = (Grid)w.Find<GridSplitter>("pane-grip-musik-links").Parent!;
            Assert.Equal(420, grid.ColumnDefinitions[2].Width.Value);
            w.TogglePane(grid, 2, "musik/links", 360); Assert.Equal(0, grid.ColumnDefinitions[2].Width.Value); Assert.Equal(0, w.PaneWidths()["musik/links"]);
            w.TogglePane(grid, 2, "musik/links", 360); Assert.Equal(420, grid.ColumnDefinitions[2].Width.Value);
            w.SavePaneWidth("musik/rechts", 5000); w.Show("musik-studio"); Pump();
            Assert.Equal(560, ((Grid)w.Find<GridSplitter>("pane-grip-musik-rechts").Parent!).ColumnDefinitions[6].Width.Value); // begrenzt
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "G3/G4: Vorschlags-Chips unter der Studio-KI-Zeile setzen den Auftrag ein (starten nichts) und verschwinden beim Tippen; Desktop „Weitermachen“ mit zuletzt benutzten Programmen")]
    public void Chips_and_continue()
    {
        var (w, _, _) = Ui.Open();
        try
        {
            w.Show("musik-studio"); Pump();
            var chip = w.Find<Button>("auftrag-chip-0");
            Assert.True(w.Find<WrapPanel>("auftrag-chips").IsVisible);
            chip.Press(); Pump();
            Assert.Equal(MainWindow.PromptChips["STUDIO-MUSIK-01"][0], w.Find<TextBox>("auftrag-prompt").Text);
            Assert.False(w.Find<WrapPanel>("auftrag-chips").IsVisible);
            Assert.Equal("Noch nicht gestartet", w.Find<TextBlock>("auftrag-status").Text);
            foreach (var id in MainWindow.PromptChips.Keys) Assert.NotNull(StudioCatalog.For(id));
            w.Show("start"); Pump();
            Assert.True(w.Find<Border>("widget-weiter").IsVisible);
            var first = w.Find<Button>("continue-0");
            Assert.Contains("Trendo Musik", string.Join(" ", first.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text)));
            first.Press(); Pump(); Assert.Equal("musik-studio", w.CurrentView);
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "K1: Musikspur im Videoschnitt zeigt die echte Wellenform der Tondatei (laut/leise erkennbar); ohne lesbare Datei nur Mittellinie statt erfundener Welle")]
    public void Video_real_waveform()
    {
        var (w, _, root) = Ui.Open(); Directory.CreateDirectory(root);
        try
        {
            const int rate = 22050;
            // 2 s leise, 2 s laut
            var wav = Path.Combine(root, "musik.wav");
            Trendo.Core.Media.AudioFile.WriteWav(new Trendo.Core.Media.AudioBuf(rate, [Enumerable.Range(0, rate * 4).Select(i => (float)((i < rate * 2 ? 0.05 : 0.8) * Math.Sin(2 * Math.PI * 220 * i / rate))).ToArray()]), wav);
            var png = Path.Combine(root, "bild.png"); File.WriteAllBytes(png, Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg=="));
            var fake = Path.Combine(root, "kaputt.mp3"); File.WriteAllBytes(fake, [1, 2, 3]);
            w.Show("schnitt"); Pump(); w.Find<Button>("prog-tpl-leer").Press(); Pump();
            w.EditAdd(png); w.EditAdd(wav, music: true, length: 4, audio: true); w.EditAdd(fake, music: true, length: 3, audio: true);
            w.Show("schnitt"); Pump();
            var tl = w.EditTimelineForTests!;
            var pk = tl.Waves[wav];
            Assert.InRange(pk.Length, 195, 205);
            Assert.True(pk[150].Max > 0.6 && pk[50].Max < 0.1, $"leise {pk[50].Max}, laut {pk[150].Max}");
            Assert.False(tl.Waves.ContainsKey(fake));
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "K3: Abspielleiste von Trendo Musik zeigt die echte Wellenform des gewählten Lieds; ohne Lied keine Welle")]
    public void Musik_player_wave()
    {
        var (w, _, root) = Ui.Open(); Directory.CreateDirectory(root);
        try
        {
            w.Show("musik-studio"); Pump();
            Assert.False(w.Find<WaveStrip>("ms-wave").IsVisible);
            const int rate = 22050; var wav = Path.Combine(root, "lied.wav");
            Trendo.Core.Media.AudioFile.WriteWav(new Trendo.Core.Media.AudioBuf(rate, [Enumerable.Range(0, rate * 3).Select(i => (float)(0.5 * Math.Sin(2 * Math.PI * 330 * i / rate))).ToArray()]), wav);
            var s = new Trendo.Core.SongLibrary(w.Core.DataRoot).Add(new Trendo.Core.MusicSong { Title = "Welle", File = wav, Seconds = 3 });
            w.MusicStudioSelect(s.Id); w.Show("musik-studio"); Pump();
            var strip = w.Find<WaveStrip>("ms-wave");
            Assert.True(strip.IsVisible); Assert.InRange(strip.Peaks.Length, 140, 160); Assert.InRange(strip.Peaks.Max(p => p.Max), 0.45, 0.55);
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "A2/A4: Texte & Tabellen mit Neu-Karten (Dokument, Tabelle, Präsentation, PDF, Brief, Word-Vorlagen) und Leerzustand; Neues Projekt mit Ziel-Karten")]
    public void Office_and_project_cards()
    {
        var (w, _, _) = Ui.Open();
        try
        {
            w.Show("office"); Pump();
            foreach (var n in new[] { "office-text", "office-tabelle", "office-deck", "office-pdf", "office-brief", "office-new-privat", "office-new-geschaeft" }) Assert.NotNull(w.TryFind<Button>(n));
            Assert.Equal(7, w.Find<WrapPanel>("office-new-cards").Children.Count);
            Assert.NotNull(w.TryFind<Border>("office-empty"));
            w.Find<Button>("office-tabelle").Press(); Pump(); Assert.Equal("tabelle", w.CurrentView);
            w.Show(w.PageForStudio("X-11")!); Pump();
            var kit = w.Find<Button>("suite-kit-video");
            Assert.Contains("Schritte", string.Join(" ", kit.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text)));
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "I2/I4: KI-Rollen als Karten (Auftrag, Schreibrecht), Wahl markiert; Ablauf-Vorlagen als Karten, ohne Ablauf Leerzustand, Vorlage legt Ablauf an")]
    public void Agents_and_workflow_cards()
    {
        var (w, _, _) = Ui.Open();
        try
        {
            w.Show("ki-rollen"); Pump();
            var role = w.Find<Button>("role-pruefer");
            Assert.Contains("nur Vorschläge", string.Join(" ", role.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text)));
            role.Press(); Pump();
            Assert.Equal(2, w.Find<Button>("role-pruefer").BorderThickness.Left);
            w.Show("ablaeufe"); Pump();
            Assert.NotNull(w.TryFind<Border>("wf-empty"));
            w.Find<Button>("wf-tpl-notiz").Press(); Pump();
            Assert.Null(w.TryFind<Border>("wf-empty"));
            Assert.Contains("Tagesnotiz", w.Find<TextBlock>("wf-msg").Text);
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "Y3/Y6: Sicherheitszentrale mit Schutz-Ring und Ampel-Karten je Prüfung; Programme verwalten mit App-Regal – Klick sucht, installiert nichts ohne „Installieren“")]
    public async Task Security_and_store()
    {
        var (w, _, _) = Ui.Open();
        try
        {
            w.Show("schutz"); Pump();
            var ring = w.Find<DonutChart>("sec-ring");
            Assert.StartsWith("Schutz: " + ring.Center + " von 100", w.Find<TextBlock>("sec-score").Text);
            Assert.True(w.Find<Avalonia.Controls.Primitives.UniformGrid>("sec-checks").Children.Count >= 3);
            var pkg = new FakePkg(); w.AppBackendOverride = pkg;
            w.Show("appcenter"); Pump();
            Assert.NotNull(w.TryFind<StackPanel>("appc-shelf"));
            w.Find<Button>("appc-shelf-vlc").Press();
            for (var i = 0; i < 50 && w.TryFind<Button>("appc-hit-install-0") is null; i++) { Pump(); await Task.Delay(20); }
            Assert.NotNull(w.TryFind<Button>("appc-hit-install-0")); Assert.Null(w.TryFind<StackPanel>("appc-shelf"));
            Assert.Empty(pkg.Done); // nichts installiert
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "Y4/Y7/Y11/Y14: Gefahren prüfen, Teilen, Qualität und Geräteverbund als Werkzeug-Karten mit Symbol; Geräteverbund ohne Geräte mit Leerzustand")]
    public void Tool_cards()
    {
        var (w, _, _) = Ui.Open();
        try
        {
            foreach (var (page, cards) in new[] { ("forensik", new[] { "for-card-file", "for-card-ports", "for-card-case" }), ("qual-leistung", new[] { "perf-card", "audit-card" }), ("verbund", new[] { "fus-card-pair", "fus-empty" }) })
            {
                w.Show(page); Pump();
                foreach (var c in cards) Assert.True(w.TryFind<Border>(c) is not null, $"{page}: {c} fehlt");
            }
            w.Show("teilen"); Pump();
            Assert.True(w.TryFind<Border>("share-card") is not null || w.TryFind<Border>("share-live") is not null || w.TryFind<TextBlock>("share-msg") is not null);
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "W3: Wissensnetz – Seiten als Punkte, [[Verweise]] als Linien (ungerichtet, ohne Doppelte, fehlende Ziele nicht), Klick öffnet die Seite; leer mit Leerzustand")]
    public void Knowledge_graph()
    {
        var (w, _, _) = Ui.Open();
        try
        {
            w.Show("ki-wissen"); Pump();
            w.Find<Button>("wissen-graph-open").Press(); Pump();
            Assert.NotNull(w.TryFind<Border>("wissen-graph-empty"));
            w.Wissen.Add("Stadtwerke", "Kundennummer 12345, siehe [[Strom]] und [[Gas]]");
            w.Wissen.Add("Strom", "Zählerstand im Januar, gehört zu [[Stadtwerke]]");
            w.Wissen.Add("Urlaub", "Packliste, verweist auf [[Fehlt noch]]");
            var edges = MainWindow.WikiEdges(w.Wissen.All(), w.Wissen);
            Assert.Single(edges); // Stadtwerke–Strom einmal, „Gas“ und „Fehlt noch“ gibt es nicht
            w.Show("ki-wissen"); Pump();
            var canvas = w.Find<Canvas>("wissen-graph");
            Assert.Single(canvas.Children.OfType<Avalonia.Controls.Shapes.Line>());
            Assert.Equal(3, canvas.Children.OfType<Button>().Count());
            w.Find<Button>("wissen-node-0").Press(); Pump();
            Assert.Null(w.TryFind<Canvas>("wissen-graph"));
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T4/T7: Geräte überwachen und Auto & Fahrten starten mit Leerzustand; Beispiel-Knopf darin füllt Dashboard bzw. Fahrzeug")]
    public void Tech_empty_states()
    {
        var (w, _, _) = Ui.Open();
        try
        {
            w.Show(w.PageForStudio("SPEC-10")!); Pump();
            Assert.NotNull(w.TryFind<Border>("twin-empty"));
            w.Find<Button>("twin-empty-demo").Press(); Pump();
            Assert.Null(w.TryFind<Border>("twin-empty"));
            w.Show(w.PageForStudio("SPEC-16")!); Pump();
            Assert.NotNull(w.TryFind<Border>("car-empty"));
            w.Find<Button>("car-empty-demo").Press(); Pump();
            Assert.Null(w.TryFind<Border>("car-empty"));
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "C1/C2/C4: Ist Claude verbunden, geht „Los“ der Studio-Zeile an Claude (Studio bleibt sichtbar, volle Rechte mit Rückfrage bei Wirkung nach außen); der zweite Auftrag läuft in derselben Sitzung; ohne Claude plant Trendo selbst")]
    public async Task Claude_takes_studio_prompts()
    {
        var (w, _, _) = Ui.Open();
        var calls = new List<IReadOnlyList<string>>();
        static string J(object o) => System.Text.Json.JsonSerializer.Serialize(o);
        try
        {
            w.Show("musik-studio"); Pump();
            Assert.False(w.ClaudeTakesStudioPrompts); // ohne Claude: Trendo-Planung wie bisher
            MainWindow.ClaudeStatusOverride = () => Task.FromResult(new Trendo.Core.ClaudeCode.Status(true, true, "claude.ai", "max", @"C:\x\claude.exe"));
            MainWindow.ClaudeStepDelay = 0;
            MainWindow.ClaudeRunOverride = (args, _, line, _) =>
            {
                calls.Add(args);
                line(J(new { type = "system", subtype = "init", session_id = "s1", model = "claude" }));
                line(J(new { type = "assistant", message = new { content = new object[] { new { type = "text", text = "Erledigt." } } } }));
                line(J(new { type = "result", subtype = "success", is_error = false, result = "Erledigt.", session_id = "s1", num_turns = 1 }));
                return Task.FromResult(0);
            };
            MainWindow.StudioMcpOverride = (dir, studio) => { File.WriteAllText(Path.Combine(dir, ".mcp.json"), "{\"mcpServers\":{}}"); return new Trendo.Core.StudioMcp.Result(dir, ["trendo-blender"], []); };
            await w.ClaudeCheck(true); Pump();
            Assert.True(w.ClaudeTakesStudioPrompts);
            w.Show("musik-studio"); Pump();
            w.Find<TextBox>("auftrag-prompt").Text = "Mach ein Sommerlied";
            w.Find<Button>("auftrag-pruefen").Press();
            for (var i = 0; i < 100 && calls.Count == 0; i++) { Pump(); await Task.Delay(20); }
            Assert.Single(calls);
            var sys = calls[0][calls[0].ToList().IndexOf("--append-system-prompt") + 1];
            Assert.Contains("Mach ein Sommerlied", string.Join(" ", calls[0]));
            Assert.Contains("fragst du zuerst", sys); // C4: Rückfrage bei Wirkung nach außen
            var mi = calls[0].ToList().IndexOf("--mcp-config"); Assert.True(mi > 0); Assert.EndsWith(".mcp.json", calls[0][mi + 1]); // C5: Projekt-Server werden mitgegeben
            Assert.True(w.ClaudeDockOpen(w.ActiveWindowId!)); Assert.False(w.ClaudeDockIsMax(w.ActiveWindowId!)); // Studio bleibt sichtbar
            for (var i = 0; i < 100 && w.ClaudeChatRunning(); i++) { Pump(); await Task.Delay(20); }
            // zweiter Auftrag: dieselbe Sitzung
            w.Show("musik-studio"); Pump();
            w.Find<TextBox>("auftrag-prompt").Text = "Jetzt etwas schneller";
            w.Find<Button>("auftrag-pruefen").Press();
            for (var i = 0; i < 100 && calls.Count < 2; i++) { Pump(); await Task.Delay(20); }
            Assert.Equal(2, calls.Count); Assert.Contains("--resume", calls[1]); Assert.Contains("s1", calls[1]);
        }
        finally { MainWindow.ClaudeStatusOverride = null; MainWindow.ClaudeRunOverride = null; MainWindow.StudioMcpOverride = null; w.Close(); }
    }

    [AvaloniaFact(DisplayName = "G10: KI-Aufträge der Grafikkarten-Warteschlange als Karten – läuft mit Laufzeit, wartet mit Platz; fertig = Karte weg")]
    public async Task Task_cards()
    {
        var (w, _, _) = Ui.Open();
        try
        {
            var t0 = DateTimeOffset.Now; w.TaskClockOverride = () => t0.AddSeconds(75);
            Assert.Empty(w.Find<StackPanel>("task-cards").Children);
            var a = await w.Gpu.AcquireAsync("musik", "Lied „Hafen“");
            var waiting = w.Gpu.AcquireAsync("bild", "Cover-Bild");
            Pump(); w.PaintTaskCards();
            var cards = w.Find<StackPanel>("task-cards").Children.OfType<Border>().ToList();
            Assert.Equal(2, cards.Count);
            var states = w.Find<StackPanel>("task-cards").GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text).ToList();
            Assert.Contains(states, s => s!.StartsWith("Musik-KI · läuft seit 1:1"));
            Assert.Contains("Bild-KI · wartet · Platz 1", states);
            Assert.Contains("Lied „Hafen“", states);
            a.Dispose(); var b = await waiting; Pump(); w.PaintTaskCards();
            Assert.Single(w.Find<StackPanel>("task-cards").Children);
            b.Dispose(); Pump(); w.PaintTaskCards();
            Assert.Empty(w.Find<StackPanel>("task-cards").Children);
        }
        finally { w.Close(); }
    }
}
