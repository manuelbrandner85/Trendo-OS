using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Trendo.App;
using Trendo.Core;
using Trendo.Core.Programs;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// <summary>T-0717 (B6 Etappe E5 KI &amp; Wissen, dazu der offene Punkt aus E2). Je Programm echt über die Oberfläche:
/// <list type="bullet">
/// <item>Entwürfe (neuer Ablauf, Angaben der Produktion, neue Lernkarte, Auftrag der KI-Rollen, Nachricht an die KI): ändern → Rückgängig über mehrere Schritte und Wiederholen →
/// Takt der Hülle → zweites Fenster bietet nichts an → Fenster ohne Speichern schließen → neues Fenster: Karte → Wiederherstellen bringt exakt den Stand.</item>
/// <item>Sofort speichernde Programme (Aufgaben, Kalender, Vorhaben, Abläufe, Produktion-Tafel): mehrere Schritte zurück und vor mit den Knöpfen der Hülle,
/// nach dem Schließen ist alles gespeichert und es gibt nichts wiederherzustellen.</item>
/// </list></summary>
public class T0717_HuelleE5_Ui(ITestOutputHelper log)
{
    static T Get<T>(MainWindow w, string name) where T : Control { Settle(); return w.GetVisualDescendants().OfType<T>().Last(c => c.Name == name && c.IsEffectivelyVisible && c.Bounds.Width > 0); }
    static bool Has(MainWindow w, string name) { Settle(); return w.GetVisualDescendants().OfType<Control>().Any(c => c.Name == name && c.IsEffectivelyVisible); }
    static void Press(MainWindow w, string name) { var b = Get<Button>(w, name); b.BringIntoView(); Settle(); RealClick(w, Get<Button>(w, name)); Settle(); }
    static void Type(MainWindow w, string name, string text) { var tb = Get<TextBox>(w, name); tb.BringIntoView(); Settle(); RealClick(w, Get<TextBox>(w, name)); tb = Get<TextBox>(w, name); tb.Focus(); tb.CaretIndex = tb.Text?.Length ?? 0; Settle(); foreach (var ch in text) { w.KeyTextInput(ch.ToString()); Settle(); } }
    static void Pause() { var until = DateTime.Now + MainWindow.LinkTypingDelay + TimeSpan.FromMilliseconds(150); while (DateTime.Now < until) { Dispatcher.UIThread.RunJobs(); Thread.Sleep(20); } Settle(); }

    static MainWindow Reopen(string root)
    {
        var w = new MainWindow(new TrendoCore(root, new InMemorySecretStore(), new FakeLauncher()), new AppOptions(root, null, 0, Isolated: true), new Probe("browser"));
        w.ProgramSetLevel(UserLevel.Erweitert); w.FreigabeConfirm = _ => Task.FromResult(true); w.Width = 1600; w.Height = 1100; w.Show(); Settle(); return w;
    }

    static string[] Backups(string root, string studio) { var d = Path.Combine(root, "programme", "sicherung"); return Directory.Exists(d) ? Directory.GetFiles(d, studio + "-*.tdoc") : []; }

    /// <summary>Entwurf: der ganze Ablauf (wie T-0711).</summary>
    static void Cycle(string page, string prefix, string studio, Action<MainWindow> open, Action<MainWindow> step1, Action<MainWindow> step2, Func<MainWindow, string> state,
        Action<MainWindow> reopen, Action<MainWindow, string>? also = null)
    {
        MainWindow.LinkTypingDelay = TimeSpan.FromMilliseconds(300);
        var (w, _, root) = Ui.Open(); w.Width = 1600; w.Height = 1100;
        try
        {
            open(w); Settle();
            var s0 = state(w);
            Assert.False(Get<Button>(w, prefix + "-undo").IsEnabled, page + ": Rückgängig ist am Anfang schon an");
            step1(w); Pause(); var s1 = state(w); Assert.NotEqual(s0, s1);
            step2(w); Pause(); var s2 = state(w); Assert.NotEqual(s1, s2);
            Press(w, prefix + "-undo"); Assert.Equal(s1, state(w));
            Press(w, prefix + "-undo"); Assert.Equal(s0, state(w)); // mehrere Schritte zurück
            Press(w, prefix + "-redo"); Assert.Equal(s1, state(w));
            Press(w, prefix + "-redo"); Assert.Equal(s2, state(w));
            w.ProgramAutosaveAll(); // Takt der Hülle (alle 20 s)
            var file = Assert.Single(Backups(root, studio));
            Assert.True(ProgramDocument.Readable(file));

            var other = Reopen(root); // zweites Fenster, solange das erste läuft: bietet die Sicherung NICHT an (Sperre)
            try { reopen(other); Settle(); Assert.False(Has(other, prefix + "-recovery"), page + ": zweites Fenster bietet die Sicherung des ersten an"); }
            finally { other.Close(); }

            w.Close(); // ohne Speichern
            var w2 = Reopen(root);
            try
            {
                reopen(w2); Settle();
                Assert.True(Has(w2, prefix + "-recovery"), page + ": keine Wiederherstellen-Karte");
                Press(w2, prefix + "-restore-0");
                Assert.Equal(s2, state(w2)); // exakt der Stand
                Assert.False(Has(w2, prefix + "-recovery"), page + ": zurückgeholte Sicherung wird erneut angeboten");
                Assert.True(File.Exists(file)); // bleibt, bis gespeichert ist
                also?.Invoke(w2, file);
            }
            finally { if (w2.IsVisible) w2.Close(); }
        }
        finally { MainWindow.LinkTypingDelay = TimeSpan.FromSeconds(1); if (w.IsVisible) w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0717 Abläufe: neuer Ablauf (Name, Schritte) – Rückgängig/Wiederholen, Notsicherung, Karte, Zurückholen; Speichern legt den Ablauf an und räumt weg")]
    public void Workflow_draft()
    {
        Cycle("ablaeufe", "wfe", "LINK-ABLAEUFE", w => { w.Show("ablaeufe"); Press(w, "wf-new"); },
            w => Type(w, "wfe-name", "Belege sortieren"), w => Press(w, "wfe-add"),
            w => Has(w, "wfe-name") ? Get<TextBox>(w, "wfe-name").Text + "|" + w.WfDraftSteps : "-",
            w => w.Show("ablaeufe"),
            (w, file) => { Press(w, "wfe-save"); Assert.False(File.Exists(file)); var wf = Assert.Single(w.Workflows.All()); Assert.Equal("Belege sortieren", wf.Name); Assert.Single(wf.Steps); });
    }

    [AvaloniaFact(DisplayName = "T-0717 Produktion: Angaben eines Ablaufs – Rückgängig/Wiederholen, Notsicherung, Karte (auch im Reiter Tafel), Zurückholen")]
    public void Fusion_inputs()
    {
        Cycle("produktion", "fusion", "LINK-PRODUKTION", w => { w.Show("produktion"); Press(w, "prod-tab-ablaeufe"); },
            w => Type(w, "fusion-in-name", "Hofladen"), w => Type(w, "fusion-in-problem", "Weite Wege zum Markt"),
            w => Has(w, "fusion-in-name") ? Get<TextBox>(w, "fusion-in-name").Text + "|" + Get<TextBox>(w, "fusion-in-problem").Text : "-",
            w => w.Show("produktion"));
    }

    [AvaloniaFact(DisplayName = "T-0717 Lernen: neue Karteikarte – Rückgängig/Wiederholen, Notsicherung, Karte, Zurückholen; Anlegen räumt weg")]
    public void Learn_new_card()
    {
        Cycle("lernen", "learn-new", "LINK-LERNEN", w => { w.Show("lernen"); Press(w, "learn-mode-karten"); },
            w => Type(w, "learn-new-front", "Hauptstadt von Frankreich?"), w => Type(w, "learn-new-back", "Paris"),
            w => Has(w, "learn-new-front") ? Get<TextBox>(w, "learn-new-front").Text + "|" + Get<TextBox>(w, "learn-new-back").Text : "-",
            w => w.Show("lernen"),
            (w, file) => { Press(w, "learn-add"); Assert.False(File.Exists(file)); Assert.Contains(w.Learn.Store.Cards, c => c.Front == "Hauptstadt von Frankreich?" && c.Back == "Paris"); });
    }

    [AvaloniaFact(DisplayName = "T-0717 KI-Rollen: Auftrag – Rückgängig/Wiederholen, Notsicherung, Karte, Zurückholen")]
    public void Agent_goal()
    {
        Cycle("ki-rollen", "agent-goal", "LINK-ROLLEN", w => w.Show("ki-rollen"),
            w => Type(w, "agent-goal", "Angebot prüfen"), w => Type(w, "agent-goal", " und kürzen"),
            w => Get<TextBox>(w, "agent-goal").Text ?? "", w => w.Show("ki-rollen"));
    }

    [AvaloniaFact(DisplayName = "T-0717 KI-Rollen: neue eigene Rolle – Rückgängig/Wiederholen, Notsicherung, Karte öffnet das Formular wieder")]
    public void Agent_new_role()
    {
        Cycle("ki-rollen", "agent-role", "LINK-ROLLE", w => { w.Show("ki-rollen"); Press(w, "agent-role-new"); },
            w => Type(w, "agent-role-name", "Steuer-Vorprüfer"), w => Type(w, "agent-role-does", "Prüft Belege vor der Abgabe"),
            w => Has(w, "agent-role-name") ? Get<TextBox>(w, "agent-role-name").Text + "|" + Get<TextBox>(w, "agent-role-does").Text : "-",
            w => w.Show("ki-rollen"));
    }

    [AvaloniaFact(DisplayName = "T-0717 KI fragen: noch nicht gesendete Nachricht – Rückgängig/Wiederholen, Notsicherung, Karte, Zurückholen")]
    public void Ask_draft()
    {
        Cycle("ki-fragen", "ask", "LINK-KIFRAGEN", w => w.Show("ki-fragen"),
            w => Type(w, "ask-input", "Wie lange kocht"), w => Type(w, "ask-input", " ein Ei?"),
            w => Get<TextBox>(w, "ask-input").Text ?? "", w => w.Show("ki-fragen"));
    }

    /// <summary>Sofort speichernde Programme: nach dem Schließen ist alles da, es gibt keine Karte und keine Notsicherung.</summary>
    static void NothingToRecover(string root, string page, string prefix, Action<MainWindow> check, string? undo = null)
    {
        Assert.Empty(Directory.Exists(Path.Combine(root, "programme", "sicherung")) ? Directory.GetFiles(Path.Combine(root, "programme", "sicherung"), "LINK-" + page.ToUpperInvariant() + "-*") : []);
        var w2 = Reopen(root);
        try { w2.Show(page); Settle(); Assert.False(Has(w2, prefix + "-recovery")); check(w2); Assert.False(Get<Button>(w2, undo ?? prefix + "-undo").IsEnabled); } // neues Fenster: eigener Verlauf
        finally { w2.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0717 Aufgaben: Rückgängig/Wiederholen der Hülle (vorher eigener Stapel ohne Wiederholen) – mehrere Schritte, Strg+Y, gespeichert nach dem Schließen")]
    public void Todo_undo_redo()
    {
        var (w, _, root) = Ui.Open(); w.Width = 1600; w.Height = 1100;
        try
        {
            w.Show("aufgaben"); Settle();
            foreach (var t in new[] { "Milch kaufen", "Steuer abgeben", "Reifen wechseln" }) { Type(w, "todo-title", t); Press(w, "todo-add"); }
            string Titles() => string.Join(",", w.Cal.Todos().Select(t => t.Title).Order());
            Assert.Equal("Milch kaufen,Reifen wechseln,Steuer abgeben", Titles());
            Assert.False(Get<Button>(w, "todo-redo").IsEnabled);
            Press(w, "todo-undo"); Press(w, "todo-undo"); Assert.Equal("Milch kaufen", Titles());
            Press(w, "todo-redo"); Assert.Equal("Milch kaufen,Steuer abgeben", Titles());
            Get<Control>(w, "todo-undo").Focus(); w.KeyPress(Key.Y, RawInputModifiers.Control, PhysicalKey.Y, "y"); Settle(); w.KeyRelease(Key.Y, RawInputModifiers.Control, PhysicalKey.Y, "y"); Settle();
            Assert.Equal("Milch kaufen,Reifen wechseln,Steuer abgeben", Titles()); // Strg+Y
            Assert.False(Get<Button>(w, "todo-redo").IsEnabled);
            w.Close();
            NothingToRecover(root, "aufgaben", "todo", w2 => Assert.Equal(3, w2.Cal.Todos().Count));
        }
        finally { if (w.IsVisible) w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0717 Kalender: Eintragen ist rückgängig zu machen, Rückgängig/Wiederholen der Hülle über mehrere Schritte, gespeichert nach dem Schließen")]
    public void Calendar_undo_redo()
    {
        var (w, _, root) = Ui.Open(); w.Width = 1600; w.Height = 1100;
        try
        {
            w.Show("kalender"); Settle();
            foreach (var t in new[] { "Zahnarzt", "Elternabend" })
            {
                Get<TextBox>(w, "cal-new-title").Text = t; Get<TextBox>(w, "cal-new-date").Text = DateTime.Today.AddDays(2).ToString("dd.MM.yyyy"); Press(w, "cal-add");
            }
            Assert.Equal(2, w.Cal.All().Count);
            Press(w, "cal-undo"); Assert.Equal("Zahnarzt", Assert.Single(w.Cal.All()).Title);
            Press(w, "cal-undo"); Assert.Empty(w.Cal.All());
            Press(w, "cal-redo"); Press(w, "cal-redo"); Assert.Equal(2, w.Cal.All().Count);
            w.Close();
            NothingToRecover(root, "kalender", "cal", w2 => Assert.Equal(2, w2.Cal.All().Count));
        }
        finally { if (w.IsVisible) w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0717 Vorhaben: Zeitplan – Rückgängig/Wiederholen der Hülle (vorher eigener Stapel ohne Wiederholen) über mehrere Schritte, gespeichert nach dem Schließen")]
    public void Plan_undo_redo()
    {
        var (w, _, root) = Ui.Open(); w.Width = 1700; w.Height = 1200;
        try
        {
            w.Show("vorhaben"); Settle();
            w.Find<TextBox>("suite-name").Text = "Gartenfest"; w.Find<TextBox>("suite-start").Text = DateTime.Today.ToString("dd.MM.yyyy");
            Press(w, "suite-kit-event"); Press(w, "suite-mode-zeitplan");
            var dir = Path.Combine(w.SuiteFolder, "Gartenfest");
            string Plan() => File.ReadAllText(Path.Combine(dir, CreationSuite.ListFile));
            var p0 = Plan();
            void Move(int row, int days)
            {
                Press(w, "suite-plan-row-" + row);
                var start = DateTime.Today.AddDays(days); w.Find<TextBox>("suite-plan-start").Text = start.ToString("dd.MM.yyyy"); w.Find<TextBox>("suite-plan-end").Text = start.AddDays(1).ToString("dd.MM.yyyy");
                Press(w, "suite-plan-apply");
            }
            Move(1, 30); var p1 = Plan(); Move(2, 40); var p2 = Plan();
            Assert.NotEqual(p0, p1); Assert.NotEqual(p1, p2);
            Assert.Equal("Schritt ändern", w.PlanUndoLabel);
            Press(w, "suite-plan-undo"); Assert.Equal(p1, Plan());
            Press(w, "suite-plan-undo"); Assert.Equal(p0, Plan());
            Press(w, "suite-plan-redo"); Assert.Equal(p1, Plan());
            Press(w, "suite-plan-redo"); Assert.Equal(p2, Plan());
            w.Close();
            Assert.Equal(p2, Plan());
            Assert.Empty(Backups(root, "LINK-VORHABEN"));
        }
        finally { if (w.IsVisible) w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0717 Abläufe: Bausteine ändern – Rückgängig/Wiederholen der Hülle (vorher 50 ohne Wiederholen), gespeichert nach dem Schließen, keine Karte")]
    public void Workflow_edits_undo_redo()
    {
        var (w, _, root) = Ui.Open(); w.Width = 1600; w.Height = 1100;
        try
        {
            w.Show("ablaeufe"); Settle();
            Press(w, "wf-tpl-pdf");
            var wf = Assert.Single(w.Workflows.All()); var n0 = wf.Name;
            w.WfChange(wf, x => x.Name = "Rechnungen ablegen", "Umbenannt."); w.WfChange(wf, x => x.Name = "Rechnungen und Belege ablegen", "Umbenannt.");
            string Name() => w.Workflows.Get(wf.Id)!.Name;
            Press(w, "wf-undo"); Assert.Equal("Rechnungen ablegen", Name());
            Press(w, "wf-undo"); Assert.Equal(n0, Name());
            Press(w, "wf-redo"); Press(w, "wf-redo"); Assert.Equal("Rechnungen und Belege ablegen", Name());
            Assert.False(Get<Button>(w, "wf-redo").IsEnabled);
            w.Close();
            NothingToRecover(root, "ablaeufe", "wfe", w2 => Assert.Equal("Rechnungen und Belege ablegen", Assert.Single(w2.Workflows.All()).Name), "wf-undo");
        }
        finally { if (w.IsVisible) w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0717 Produktion: Tafel – Getipptes wird nach der Tipp-Pause gespeichert (vorher erst beim Verlassen des Felds), Rückgängig/Wiederholen (vorher keins), nichts verloren nach dem Schließen")]
    public void Board_typing_undo_redo()
    {
        MainWindow.LinkTypingDelay = TimeSpan.FromMilliseconds(300);
        var (w, _, root) = Ui.Open(); w.Width = 1700; w.Height = 1100;
        try
        {
            w.Show("produktion"); Settle();
            Type(w, "big-new-name", "Hofladen online"); Press(w, "big-new-create");
            var pid = Assert.Single(w.Core.Projects.List()).Id;
            foreach (var t in new[] { "Fotos", "Preise" }) { Type(w, "big-add-text", t); Press(w, "big-add"); }
            Assert.Equal(2, BigBoards.Load(w.Core.Projects, pid).Cards.Count);
            Type(w, "big-c-detail", "Bilder vom Hof"); Pause(); // nicht verlassen – trotzdem schon gespeichert
            Assert.Equal("Bilder vom Hof", BigBoards.Load(w.Core.Projects, pid).Cards.Single(c => c.Title == "Preise").Detail);
            Press(w, "big-undo"); Assert.Equal("", BigBoards.Load(w.Core.Projects, pid).Cards.Single(c => c.Title == "Preise").Detail);
            Press(w, "big-undo"); Assert.Equal("Fotos", Assert.Single(BigBoards.Load(w.Core.Projects, pid).Cards).Title);
            Press(w, "big-redo"); Press(w, "big-redo"); Assert.Equal("Bilder vom Hof", BigBoards.Load(w.Core.Projects, pid).Cards.Single(c => c.Title == "Preise").Detail);
            Type(w, "big-c-detail", " und Kisten"); Pause();
            w.Close(); // Feld nicht verlassen, Fenster zu
            NothingToRecover(root, "produktion", "big", w2 => Assert.Equal("Bilder vom Hof und Kisten", BigBoards.Load(w2.Core.Projects, pid).Cards.Single(c => c.Title == "Preise").Detail));
        }
        finally { MainWindow.LinkTypingDelay = TimeSpan.FromSeconds(1); if (w.IsVisible) w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0717 Menüleiste: alle neu angeschlossenen Seiten haben „Bearbeiten“ mit Rückgängig/Wiederholen der Hülle; Rückgängig über das Menü wirkt (Aufgaben)")]
    public void Menu_bar_on_new_pages()
    {
        var (w, _, _) = Ui.Open(); w.Width = 3000; w.Height = 1000;
        try
        {
            var missing = new List<string>();
            foreach (var key in new[] { "ablaeufe", "produktion", "lernen", "ki-rollen", "ki-fragen", "aufgaben", "kalender", "vorhaben" })
            {
                foreach (var old in w.Windows.Stack.Reverse().Skip(3).ToList()) w.CloseWindow(old.Id);
                w.Show(key); Settle();
                if (key == "ablaeufe") Press(w, "wf-new");
                if (key == "lernen") Press(w, "learn-mode-karten");
                if (w.ActiveMenuBar() is not { } bar) { missing.Add(key + ": keine Leiste"); continue; }
                var names = bar.GetLogicalDescendants().OfType<MenuItem>().Select(m => m.Name).ToList();
                log.WriteLine($"{key}: {string.Join(" ", names.Where(n => n?.StartsWith("menu-") == true))}");
                foreach (var s in w.LinkSlots.Values.Where(x => x.Page == key))
                    foreach (var n in new[] { "menu-" + s.Prefix + "-undo", "menu-" + s.Prefix + "-redo" }) if (!names.Contains(n)) missing.Add(key + ": " + n + " fehlt");
            }
            Assert.Empty(missing);

            foreach (var old in w.Windows.Stack.Reverse().Skip(3).ToList()) w.CloseWindow(old.Id);
            w.Show("aufgaben"); Settle();
            Type(w, "todo-title", "Blumen gießen"); Press(w, "todo-add"); Assert.Single(w.Cal.Todos());
            var item = w.ActiveMenuBar()!.GetLogicalDescendants().OfType<MenuItem>().First(m => m.Name == "menu-todo-undo");
            item.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(MenuItem.ClickEvent)); Settle();
            Assert.Empty(w.Cal.Todos());
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0717 Forensik: bleibt außerhalb der Notsicherung – keine Sicherung, kein Anschluss; Fälle nur atomar (120-Stunden-Regel unverändert)")]
    public void Forensics_not_backed_up()
    {
        var (w, _, root) = Ui.Open(); w.Width = 1600; w.Height = 1100;
        try
        {
            w.Show("forensik"); Settle();
            Assert.DoesNotContain(w.LinkSlots.Values, s => s.Page == "forensik");
            w.ProgramAutosaveAll();
            var d = Path.Combine(root, "programme", "sicherung");
            Assert.Empty(Directory.Exists(d) ? Directory.GetFiles(d, "*FORENS*") : []);
        }
        finally { w.Close(); }
    }
}
