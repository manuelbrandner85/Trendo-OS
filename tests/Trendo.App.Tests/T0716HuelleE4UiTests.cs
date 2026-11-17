using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using Trendo.App;
using Trendo.Core;
using Trendo.Core.Programs;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// <summary>T-0716 (B6 „Programm-Hülle für alle“, Etappe E4): Baukasten, Fahrzeugbuch und Anlagen-Monitor (sofort speichernd) hängen über ProgramLink an Rückgängig (100)
/// und Notsicherung der Hülle; das Web Design Studio sichert seinen Entwurf dort. Je Programm über die Oberfläche: speichernde Änderung + Tippen ohne Übernehmen →
/// Rückgängig mehrere Schritte (schreibt den früheren Stand in die Datei des Programms) und Wiederholen → Takt der Hülle → zweites Fenster bietet nichts an →
/// Fenster ohne Übernehmen schließen → neues Fenster zeigt die Karte → „Wiederherstellen“ bringt genau die Eingaben zurück → Enter übernimmt, Sicherung weg.</summary>
public class T0716_HuelleE4_Ui
{
    static T Get<T>(MainWindow w, string name) where T : Control { Settle(); return w.GetVisualDescendants().OfType<T>().Last(c => c.Name == name && c.IsEffectivelyVisible && c.Bounds.Width > 0); }
    static bool Has(MainWindow w, string name) { Settle(); return w.GetVisualDescendants().OfType<Control>().Any(c => c.Name == name && c.IsEffectivelyVisible); }
    static void Press(MainWindow w, string name) { var b = Get<Button>(w, name); b.BringIntoView(); Settle(); RealClick(w, Get<Button>(w, name)); Settle(); }
    static void Type(MainWindow w, string name, string text) { var tb = Get<TextBox>(w, name); tb.BringIntoView(); Settle(); RealClick(w, Get<TextBox>(w, name)); tb = Get<TextBox>(w, name); tb.Focus(); tb.CaretIndex = tb.Text?.Length ?? 0; Settle(); foreach (var ch in text) { w.KeyTextInput(ch.ToString()); Settle(); } }
    static void Enter(MainWindow w, string name) { var tb = Get<TextBox>(w, name); tb.Focus(); Settle(); w.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null); Settle(); w.KeyRelease(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null); Settle(); }
    static string Field(MainWindow w, string name) => Get<TextBox>(w, name).Text ?? "";
    static void Pause() { var until = DateTime.Now + MainWindow.LinkTypingDelay + TimeSpan.FromMilliseconds(150); while (DateTime.Now < until) { Avalonia.Threading.Dispatcher.UIThread.RunJobs(); Thread.Sleep(20); } Settle(); }
    static void Tap(MainWindow w, Control c) { c.BringIntoView(); Settle(); var p = c.TranslatePoint(new Point(c.Bounds.Width / 2, c.Bounds.Height / 2), w)!.Value; w.MouseDown(p, MouseButton.Left); w.MouseUp(p, MouseButton.Left); Settle(); }

    static MainWindow Reopen(string root)
    {
        var w = new MainWindow(new TrendoCore(root, new InMemorySecretStore(), new FakeLauncher()), new AppOptions(root, null, 0, Isolated: true), new Probe("browser"));
        w.ProgramSetLevel(UserLevel.Erweitert); w.FreigabeConfirm = _ => Task.FromResult(true); w.Width = 1600; w.Height = 1100; w.Show(); Settle(); return w;
    }

    static string[] Backups(string root, string studio) { var d = Path.Combine(root, "programme", "sicherung"); return Directory.Exists(d) ? Directory.GetFiles(d, studio + "-*.tdoc") : []; }

    /// <summary>Der ganze Ablauf. <paramref name="saved1"/>/<paramref name="saved2"/> ändern und speichern (wie das Programm: sofort), <paramref name="typed"/> tippt ohne
    /// zu übernehmen (der Cursor bleibt im Feld), <paramref name="state"/> liest gespeicherten Stand + Eingaben + Feldinhalt, <paramref name="commit"/> übernimmt (Enter).</summary>
    static void Cycle(string page, string prefix, string studio, Action<MainWindow> open, Action<MainWindow> saved1, Action<MainWindow> saved2, Action<MainWindow> typed,
        Func<MainWindow, string> state, Action<MainWindow> commit, Func<MainWindow, string> disk, Action<MainWindow>? check = null)
    {
        MainWindow.LinkTypingDelay = TimeSpan.FromMilliseconds(300);
        var (w, _, root) = Ui.Open(); w.Width = 1600; w.Height = 1100;
        try
        {
            open(w); Settle();
            var s0 = state(w); var d0 = disk(w);
            Assert.False(Get<Button>(w, prefix + "-undo").IsEnabled, page + ": Rückgängig ist am Anfang schon an");
            saved1(w); Pause(); var s1 = state(w); var d1 = disk(w); Assert.NotEqual(s0, s1); Assert.NotEqual(d0, d1);
            saved2(w); Pause(); var s2 = state(w); var d2 = disk(w); Assert.NotEqual(s1, s2); Assert.NotEqual(d1, d2);
            Press(w, prefix + "-undo"); Assert.Equal(s1, state(w)); Assert.Equal(d1, disk(w)); // Rückgängig schreibt den früheren Stand in die Datei des Programms
            Press(w, prefix + "-undo"); Assert.Equal(s0, state(w)); if (d0.Length > 0) Assert.Equal(d0, disk(w)); // mehrere Schritte zurück (gab es noch keine Datei, steht jetzt der leere Stand darin)
            Press(w, prefix + "-redo"); Assert.Equal(s1, state(w));
            Press(w, prefix + "-redo"); Assert.Equal(s2, state(w)); Assert.Equal(d2, disk(w));
            Assert.Empty(Backups(root, studio)); // alles gespeichert: keine Notsicherung
            typed(w); Pause(); var s3 = state(w); Assert.NotEqual(s2, s3); Assert.Equal(d2, disk(w)); // getippt, noch nicht übernommen
            w.ProgramAutosaveAll(); // Takt der Hülle (alle 20 s)
            var file = Assert.Single(Backups(root, studio));
            Assert.True(ProgramDocument.Readable(file));
            // Absturz in diesem Moment: der Datenordner, wie er jetzt auf der Platte liegt (samt Sperre dieses Fensters). Das laufende Fenster übernimmt das Feld,
            // sobald es den Fokus verliert (z. B. an ein zweites Fenster) – nach einem Absturz passiert das nicht, darum geht es mit der Kopie weiter.
            var crashed = root + "-absturz"; Copy(root, crashed);

            // zweites Fenster, solange das erste läuft: bietet die Sicherung NICHT an (Sperre)
            var other = Reopen(root);
            try { open(other); Settle(); Assert.False(Has(other, prefix + "-recovery"), page + ": zweites Fenster bietet die Sicherung des ersten an"); }
            finally { other.Close(); }

            w.ProgLocks.Abandon(); // der abgestürzte Prozess lebt nicht mehr – seine Sperre gilt als verwaist
            file = Path.Combine(crashed, Path.GetRelativePath(root, file));
            var w2 = Reopen(crashed);
            try
            {
                open(w2); Settle();
                Assert.Equal(d2, disk(w2)); // gespeichert ist der Stand vor dem Tippen
                Assert.True(Has(w2, prefix + "-recovery"), page + ": keine Wiederherstellen-Karte");
                Press(w2, prefix + "-restore-0");
                Assert.Equal(s3, state(w2)); // genau die Eingaben von vorher, im Feld
                Assert.False(Has(w2, prefix + "-recovery"), page + ": zurückgeholte Sicherung wird erneut angeboten");
                Assert.True(File.Exists(file)); // bleibt, bis übernommen ist
                commit(w2); Pause();
                Assert.NotEqual(d2, disk(w2)); // Enter hat gespeichert
                w2.Show(page); Settle();
                Assert.False(File.Exists(file), page + ": Sicherung bleibt nach dem Übernehmen liegen");
                check?.Invoke(w2);
            }
            finally { if (w2.IsVisible) w2.Close(); try { Directory.Delete(crashed, true); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { } }
        }
        finally { MainWindow.LinkTypingDelay = TimeSpan.FromSeconds(1); if (w.IsVisible) w.Close(); }
    }

    static void Copy(string from, string to)
    {
        foreach (var d in Directory.GetDirectories(from, "*", SearchOption.AllDirectories)) Directory.CreateDirectory(Path.Combine(to, Path.GetRelativePath(from, d)));
        Directory.CreateDirectory(to);
        foreach (var f in Directory.GetFiles(from, "*", SearchOption.AllDirectories))
            try { File.Copy(f, Path.Combine(to, Path.GetRelativePath(from, f)), true); } catch (IOException) { } // gerade offene Protokolle u. ä.
        // Einstellungen mit festen Pfaden (z. B. Projektordner) zeigen in der Kopie auf die Kopie – wie ein an anderer Stelle eingespieltes Abbild des Datenträgers
        var esc = System.Text.Json.JsonSerializer.Serialize(from)[1..^1]; var esc2 = System.Text.Json.JsonSerializer.Serialize(to)[1..^1];
        foreach (var f in Directory.GetFiles(to, "*.json", SearchOption.AllDirectories))
            try { var t = File.ReadAllText(f); if (t.Contains(esc, StringComparison.Ordinal)) File.WriteAllText(f, t.Replace(esc, esc2, StringComparison.Ordinal)); } catch (IOException) { }
    }

    static string Disk(MainWindow w, string studio, string name) { var f = Path.Combine(w.Core.DataRoot, "fach", studio, name + ".json"); return File.Exists(f) ? File.ReadAllText(f) : ""; }

    [AvaloniaFact(DisplayName = "T-0716 Fenster schließen, während im Feld getippt ist: das Programm übernimmt die Eingabe (wie vorher) – keine Karte, nichts geht verloren")]
    public void Close_commits_field()
    {
        MainWindow.LinkTypingDelay = TimeSpan.FromMilliseconds(300);
        var (w, _, root) = Ui.Open(); w.Width = 1600; w.Height = 1100;
        try
        {
            w.Twin.Define("Heizung", "Vorlauf", "°C"); w.Show("zwilling"); Settle();
            Type(w, "twin-p-aa", "85"); Pause();
            w.ProgramAutosaveAll();
            Assert.Single(Backups(root, "LINK-ZWILLING"));
            w.Close();
            var w2 = Reopen(root);
            try
            {
                w2.Show("zwilling"); Settle();
                Assert.Equal(85, w2.Twin.Model.Assets.Single().Sensors.Single().AlarmAbove);
                Assert.False(Has(w2, "twin-recovery"), "Karte für eine schon übernommene Eingabe");
                Assert.Empty(Backups(root, "LINK-ZWILLING"));
            }
            finally { w2.Close(); }
        }
        finally { MainWindow.LinkTypingDelay = TimeSpan.FromSeconds(1); if (w.IsVisible) w.Close(); }
    }

    static string Inputs(MainWindow w, string key) => string.Join(";", w.LiveInputsOf(key).OrderBy(kv => kv.Key).Select(kv => kv.Key + "=" + kv.Value));

    [AvaloniaFact(DisplayName = "T-0716 Baukasten: Farbe (gespeichert) + Text getippt – Rückgängig/Wiederholen der Hülle, Notsicherung, Sperre, Karte, Zurückholen in das Feld, Enter speichert")]
    public void Builder_cycle()
    {
        Cycle("baukasten", "builder", "LINK-BAUKASTEN",
            w => { w.Show("baukasten"); Settle(); },
            w => Press(w, "builder-accent-22B35E"),
            w => Press(w, "builder-add-hr"),
            w => { Tap(w, Get<Border>(w, "builder-block-0")); Type(w, "builder-text-0", " und hallo"); },
            w => $"{w.BuilderPage.Accent}|{string.Join(",", w.BuilderPage.Blocks.Select(b => b.Kind))}|{w.BuilderPage.Blocks[0].Text}|{Inputs(w, "baukasten")}|{(Has(w, "builder-text-0") ? Field(w, "builder-text-0") : "-")}",
            w => Enter(w, "builder-text-0"),
            w => File.Exists(w.BuilderFile) ? File.ReadAllText(w.BuilderFile) : "",
            w => { Assert.Equal("Willkommen und hallo", w.BuilderPage.Blocks[0].Text); Assert.Contains("\"Version\": 1", File.ReadAllText(w.BuilderFile)); });
    }

    [AvaloniaFact(DisplayName = "T-0716 Baukasten: Rückgängig hat jetzt die Grenze der Hülle (100 Schritte) – vorher eigener Stapel ohne Grenze")]
    public void Builder_undo_100()
    {
        var (w, _, _) = Ui.Open(); w.Width = 1600; w.Height = 1100;
        try
        {
            w.Show("baukasten"); Settle();
            for (var i = 0; i < 120; i++) w.BuilderInsert("hr", 0);
            Assert.Equal(122, w.BuilderPage.Blocks.Count);
            Assert.Equal(100, w.BuilderUndoCount);
            for (var i = 0; i < 130; i++) w.BuilderUndo();
            Assert.Equal(22, w.BuilderPage.Blocks.Count); // nach Schritt 20 – mehr hält die Hülle nicht
            Assert.Equal(22, SitePage.Load(w.BuilderFile).Blocks.Count); // auch in der Datei
            Assert.False(Get<Button>(w, "builder-undo").IsEnabled);
            Press(w, "builder-redo"); Assert.Equal(23, w.BuilderPage.Blocks.Count);
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0716 Anlagen-Monitor: Grenze gesetzt (gespeichert) + zweite getippt – Rückgängig/Wiederholen, Notsicherung, Sperre, Karte, Zurückholen; Messwerte bleiben unberührt")]
    public void Twin_cycle()
    {
        Cycle("zwilling", "twin", "LINK-ZWILLING",
            w => { if (w.Twin.Model.Assets.Count == 0) { w.Twin.Define("Heizung", "Vorlauf", "°C"); w.Twin.Record(DateTime.Now, "Heizung", "Vorlauf", 55); } w.Show("zwilling"); Settle(); },
            w => { Type(w, "twin-p-wa", "70"); Enter(w, "twin-p-wa"); },
            w => { Type(w, "twin-p-wb", "5"); Enter(w, "twin-p-wb"); },
            w => Type(w, "twin-p-aa", "85"),
            w => { var s = w.Twin.Model.Assets.Single().Sensors.Single(); return $"{s.WarnAbove}|{s.WarnBelow}|{s.AlarmAbove}|{Inputs(w, "zwilling")}|{(Has(w, "twin-p-aa") ? Field(w, "twin-p-aa") : "-")}|{w.Twin.Model.Readings.Count}"; },
            w => Enter(w, "twin-p-aa"),
            w => Disk(w, "zwilling", "modell").Split("\"Readings\"")[0],
            w => { var s = w.Twin.Model.Assets.Single().Sensors.Single(); Assert.Equal(70, s.WarnAbove); Assert.Equal(5, s.WarnBelow); Assert.Equal(85, s.AlarmAbove); Assert.Single(w.Twin.Model.Readings); });
    }

    const string NewPagePlan = """{"aktionen":[{"aktion":"seite_anlegen","titel":"Café Muster","beschreibung":"Guter Kaffee"},{"aktion":"baustein_einfuegen","typ":"Text","id":"t1","props":{"text":"Unsere Bohnen"}}]}""";

    [AvaloniaFact(DisplayName = "T-0716 Web Design Studio: ungespeicherter Entwurf des Editors – Notsicherung der Hülle, Sperre, Karte, Zurückholen in den Editor; „Speichern“ räumt weg")]
    public async Task WebDesign_draft()
    {
        var (w, _, root) = Ui.Open(); w.Width = 1600; w.Height = 1100;
        try
        {
            MainWindow.WebDesignKiOverride = (_, _) => Task.FromResult(NewPagePlan);
            w.Show("dev"); Settle();
            await w.WebDesignAiRun("Seite"); Settle(); w.WebDesignAccept(); Settle();
            var dir = w.WebDesignDir; var pageFile = Path.Combine(dir, WebDesign.PageFile); var before = File.ReadAllText(pageFile);
            var draft = before.Replace("Unsere Bohnen", "Frisch gemahlen");
            w.WebDesignMessage("{\"art\":\"entwurf\",\"daten\":" + draft + "}"); Settle(); // so meldet der Editor eine ungespeicherte Änderung
            Assert.Equal(before, File.ReadAllText(pageFile));
            w.ProgramAutosaveAll();
            var file = Assert.Single(Backups(root, "LINK-WEBDESIGN"));
            Assert.Equal("webdesign", ProgramDocument.Load(file).Data["format"]!.GetValue<string>());

            var other = Reopen(root);
            try { other.Show("dev"); Settle(); Assert.False(Has(other, "wd-recovery")); }
            finally { other.Close(); }

            w.Close(); // ohne Speichern im Editor
            var w2 = Reopen(root);
            try
            {
                w2.Show("dev"); Settle();
                Assert.True(Has(w2, "wd-recovery"), "keine Wiederherstellen-Karte");
                Press(w2, "wd-restore-0");
                Assert.Contains("Frisch gemahlen", w2.WebDesignDraft);
                Assert.Equal(before, File.ReadAllText(pageFile)); // erst „Speichern“ schreibt
                Assert.False(Has(w2, "wd-recovery"));
                w2.WebDesignMessage("{\"art\":\"speichern\",\"daten\":" + w2.WebDesignDraft + "}"); Settle();
                Assert.Contains("Frisch gemahlen", File.ReadAllText(pageFile));
                w2.ProgramAutosaveAll();
                Assert.Empty(Backups(root, "LINK-WEBDESIGN")); // gespeichert: Sicherung weg
            }
            finally { if (w2.IsVisible) w2.Close(); }
        }
        finally { MainWindow.WebDesignKiOverride = null; if (w.IsVisible) w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0716 Menüleiste (T-0710) auf Baukasten und Anlagen-Monitor (Fahrzeugbuch mit T-0720 entfernt): „Bearbeiten“ ruft Rückgängig/Wiederholen der Hülle")]
    public void Menu_bar_on_new_pages()
    {
        var (w, _, _) = Ui.Open(); w.Width = 1600; w.Height = 1100;
        try
        {
            foreach (var (page, prefix) in new[] { ("baukasten", "builder"), ("zwilling", "twin") })
            {
                w.Show(page); Settle();
                var bar = w.ActiveMenuBar() ?? throw new Xunit.Sdk.XunitException("keine Menüleiste auf " + page);
                var names = bar.GetLogicalDescendants().OfType<MenuItem>().Select(m => m.Name).ToList();
                Assert.Contains("menubar-Bearbeiten", names);
                Assert.Contains("menu-" + prefix + "-undo", names);
                Assert.Contains("menu-" + prefix + "-redo", names);
            }
            // Bearbeiten › Rückgängig wirkt wie der Knopf
            w.Show("baukasten"); Settle();
            Press(w, "builder-accent-22B35E"); Assert.Equal("#22B35E", w.BuilderPage.Accent);
            var undo = w.ActiveMenuBar()!.GetLogicalDescendants().OfType<MenuItem>().First(m => m.Name == "menu-builder-undo");
            undo.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(MenuItem.ClickEvent)); Settle();
            Assert.Equal("#2F86F6", w.BuilderPage.Accent);
        }
        finally { w.Close(); }
    }
}
