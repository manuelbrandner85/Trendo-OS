using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using Trendo.App;
using Trendo.Core;
using Trendo.Core.Programs;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// <summary>T-0708 (B6 Etappe 1): Briefe, CAD, 3D-Modelle und Code-Editor hängen an Speichern · Rückgängig · Wiederholen · Exportieren (V-14) –
/// Briefe zusätzlich an Rückgängig und Notsicherung der Programm-Hülle (vorher: weder Rückgängig noch Autosave).</summary>
public class T0708_HuelleEtappe1_Ui
{
    static T Get<T>(MainWindow w, string name) where T : Control { Settle(); return w.GetVisualDescendants().OfType<T>().Last(c => c.Name == name && c.IsEffectivelyVisible && c.Bounds.Width > 0); }
    static void Press(MainWindow w, string name) { var b = Get<Button>(w, name); b.BringIntoView(); Settle(); RealClick(w, Get<Button>(w, name)); Settle(); }
    static void Type(MainWindow w, string name, string text) { var tb = Get<TextBox>(w, name); tb.BringIntoView(); RealClick(w, tb); tb.Focus(); tb.CaretIndex = tb.Text?.Length ?? 0; Settle(); foreach (var ch in text) { w.KeyTextInput(ch.ToString()); Settle(); } }
    /// <summary>Tipp-Pause abwarten (so wird aus dem Tippen ein Rückgängig-Schritt).</summary>
    static void Pause() { var until = DateTime.Now + MainWindow.LinkTypingDelay + TimeSpan.FromMilliseconds(150); while (DateTime.Now < until) { Avalonia.Threading.Dispatcher.UIThread.RunJobs(); Thread.Sleep(20); } Settle(); }

    static void AssertDocActionsFirst(MainWindow w, string save, string undo, string redo, string? export)
    {
        Settle();
        var g = w.GetVisualDescendants().OfType<StackPanel>().First(s => s.Name == MainWindow.DocActionsName && s.IsEffectivelyVisible);
        var names = g.Children.Select(c => c.Name).ToList();
        Assert.Equal(new[] { save, undo, redo, export }.Where(x => x is not null).ToList(), names);
    }

    [AvaloniaFact(DisplayName = "T-0708 Briefe: tippen, Rückgängig über mehrere Schritte und Wiederholen über die Knöpfe links oben")]
    public void Letter_undo_redo()
    {
        MainWindow.LinkTypingDelay = TimeSpan.FromMilliseconds(300);
        var (w, _, _) = Ui.Open();
        try
        {
            w.Show("brief"); Settle();
            Press(w, "letter-wiz-tab-3");
            AssertDocActionsFirst(w, "letter-save", "letter-undo", "letter-redo", "letter-export");
            Assert.False(Get<Button>(w, "letter-undo").IsEnabled);
            Type(w, "letter-subject", "Kündigung"); Pause();
            Type(w, "letter-body", "Hiermit kündige ich."); Pause();
            Type(w, "letter-body", " Bitte bestätigen."); Pause();
            Assert.Equal("Hiermit kündige ich. Bitte bestätigen.", w.Letter.Body);
            Assert.True(Get<Button>(w, "letter-undo").IsEnabled);
            Press(w, "letter-undo");
            Assert.Equal("Hiermit kündige ich.", w.Letter.Body); Assert.Equal("Hiermit kündige ich.", Get<TextBox>(w, "letter-body").Text);
            Press(w, "letter-undo");
            Assert.Equal("", w.Letter.Body); Assert.Equal("Kündigung", w.Letter.Subject);
            Press(w, "letter-undo");
            Assert.Equal("", w.Letter.Subject);
            Press(w, "letter-redo"); Press(w, "letter-redo");
            Assert.Equal("Kündigung", w.Letter.Subject); Assert.Equal("Hiermit kündige ich.", w.Letter.Body);
            // eine Aktion über einen Knopf (Vorlage) ist ebenfalls ein Schritt
            Press(w, "letter-wiz-tab-1"); Press(w, "letter-gallery-toggle"); w.Find<Button>("letter-gallery-widerruf").Press(); Settle();
            Assert.NotEqual("Kündigung", w.Letter.Subject);
            Press(w, "letter-undo");
            Assert.Equal("Kündigung", w.Letter.Subject);
        }
        finally { MainWindow.LinkTypingDelay = TimeSpan.FromSeconds(1); w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0708 Briefe: Fenster ohne Speichern geschlossen → nach dem Neustart Wiederherstellen-Karte, Brief zurück; Speichern räumt die Sicherung weg; Verwerfen löscht")]
    public void Letter_recover_after_restart()
    {
        MainWindow.LinkTypingDelay = TimeSpan.FromMilliseconds(300);
        var (w, _, root) = Ui.Open();
        try
        {
            w.Show("brief"); Settle(); Press(w, "letter-wiz-tab-3");
            Type(w, "letter-subject", "Widerspruch"); Type(w, "letter-body", "Gegen den Bescheid lege ich Widerspruch ein."); // ohne Tipp-Pause: der Takt der Hülle nimmt das Getippte mit
            w.ProgramAutosaveAll(); // Takt der Hülle (alle 20 s)
            var bak = Path.Combine(root, "programme", "sicherung");
            var file = Assert.Single(Directory.GetFiles(bak, "STUDIO-LETTER-01-*.tdoc"));
            Assert.True(ProgramDocument.Readable(file));
            Assert.True(MainWindow.ProgramAutosaveEvery <= TimeSpan.FromSeconds(30) - MainWindow.LinkTypingDelay); // höchstens 30 s alter Stand
            w.Close(); // ohne Speichern

            var core = new TrendoCore(root, new InMemorySecretStore(), new FakeLauncher());
            var w2 = new MainWindow(core, new AppOptions(root, null, 0, Isolated: true), new Probe("browser")); w2.Show(); Settle();
            try
            {
                w2.Show("brief"); Settle();
                Assert.NotNull(w2.TryFind<Border>("letter-recovery"));
                Assert.Equal("", w2.Letter.Body);
                if (Environment.GetEnvironmentVariable("TRENDO_T0708_SHOT") is { Length: > 0 } shot) w2.CaptureRenderedFrame()?.Save(shot); // Bild der Wiederherstellen-Karte (nur auf Wunsch)
                Press(w2, "letter-restore-0");
                Assert.Equal("Widerspruch", w2.Letter.Subject); Assert.Equal("Gegen den Bescheid lege ich Widerspruch ein.", w2.Letter.Body);
                Assert.Null(w2.TryFind<Border>("letter-recovery")); // die eigene (zurückgeholte) Sicherung wird nicht erneut angeboten
                Assert.True(File.Exists(file)); // bleibt, bis gespeichert ist
                // weiterarbeiten und speichern
                Press(w2, "letter-wiz-tab-2"); Type(w2, "letter-recipient","Amt für Beispiele\nPostfach 1\n12345 Musterstadt");
                w2.Letter.SenderName = "Erika Muster"; w2.Letter.SenderStreet = "Musterweg 1"; w2.Letter.SenderCity = "12345 Musterstadt";
                w2.Show("brief"); Settle(); Press(w2, "letter-wiz-tab-3");
                Press(w2, "letter-save");
                for (var i = 0; i < 50 && File.Exists(file); i++) { Avalonia.Threading.Dispatcher.UIThread.RunJobs(); Thread.Sleep(20); }
                Assert.False(File.Exists(file)); // gespeichert → Sicherung weg
                Assert.NotEmpty(Directory.GetFiles(Path.Combine(w2.ProjectsRootForTests, "Briefe"), "*.trendo-brief.json"));

                // Verwerfen: neue ungesicherte Änderung, Neustart, verwerfen
                Type(w2, "letter-body", " Nachtrag"); w2.ProgramAutosaveAll();
                var second = Assert.Single(Directory.GetFiles(bak, "STUDIO-LETTER-01-*.tdoc"));
                w2.Close();
                var w3 = new MainWindow(new TrendoCore(root, new InMemorySecretStore(), new FakeLauncher()), new AppOptions(root, null, 0, Isolated: true), new Probe("browser")); w3.Show(); Settle();
                w3.Show("brief"); Settle();
                Press(w3, "letter-discard-0");
                Assert.False(File.Exists(second)); Assert.Null(w3.TryFind<Border>("letter-recovery"));
                w3.Close();
            }
            finally { if (w2.IsVisible) w2.Close(); }
        }
        finally { MainWindow.LinkTypingDelay = TimeSpan.FromSeconds(1); }
    }

    [AvaloniaFact(DisplayName = "T-0708 V-14: CAD, 3D-Modelle und Code-Editor haben Speichern · Rückgängig · Wiederholen (· Exportieren) links oben; Rückgängig wirkt über mehrere Schritte")]
    public void Doc_actions_cad_3d_code()
    {
        var (w, _, root) = Ui.Open();
        try
        {
            // CAD
            w.CadNew(); w.Show("cad"); Settle();
            AssertDocActionsFirst(w, "cad-save", "cad-undo", "cad-redo", "cad-export");
            w.CadAdd(Trendo.Core.Cad.CadOp.Quader); w.CadAdd(Trendo.Core.Cad.CadOp.Bohrung); w.CadAdd(Trendo.Core.Cad.CadOp.Rundung); w.Show("cad"); Settle();
            Assert.Equal(3, w.Cad.Bodies[0].Features.Count);
            Press(w, "cad-undo"); Press(w, "cad-undo");
            Assert.Single(w.Cad.Bodies[0].Features);
            Press(w, "cad-redo");
            Assert.Equal(2, w.Cad.Bodies[0].Features.Count);
            for (var i = 0; i < 120; i++) w.CadSetParam("p", i);
            for (var i = 0; i < ProgramDocument.MaxUndo; i++) w.CadUndo();
            Assert.Equal(119 - ProgramDocument.MaxUndo, w.Cad.Params["p"]); // 100 Schritte wie die Hülle (vorher 80: blieb bei 39 stehen)

            // 3D-Modelle erstellen – in jeder Bedienstufe
            w.Show("3d"); Settle();
            AssertDocActionsFirst(w, "3d-save", "3d-undo", "3d-redo", "3d-export");

            // Code-Editor
            var proj = Path.Combine(root, "code"); Directory.CreateDirectory(proj); File.WriteAllText(Path.Combine(proj, "a.py"), "print(1)\n");
            w.CodeOpenFolder(proj); w.CodeOpenFile(Path.Combine(proj, "a.py")); w.Show("code"); Settle();
            AssertDocActionsFirst(w, "ce-save", "ce-undo", "ce-redo", null);
            var ed = w.CodeEditorForTests!; ed.Document.Insert(ed.Document.TextLength, "x = 2\n"); ed.Document.Insert(ed.Document.TextLength, "y = 3\n");
            w.Show("code"); Settle();
            Press(w, "ce-undo"); Press(w, "ce-undo");
            Assert.Equal("print(1)\n", w.CodeEditorForTests!.Document.Text);
            Press(w, "ce-redo");
            Assert.Equal("print(1)\nx = 2\n", w.CodeEditorForTests!.Document.Text);
        }
        finally { w.Close(); }
    }
}
