using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Trendo.App;
using Trendo.Core;
using Trendo.Core.Cad;
using Trendo.Core.Office;
using Trendo.Core.Programs;
using Trendo.Core.Sys;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// <summary>T-0714 (B6 „Programm-Hülle für alle“, Etappe E2): Text, Tabelle, Präsentation, CAD und 3D haben nur noch EINE Notsicherung – die der Hülle (ProgramLink).
/// Je Programm über die Oberfläche: ändern → Takt der Hülle → zweites Fenster bietet nichts an (Sperre) → Fenster ohne Speichern schließen → neues Fenster zeigt die Karte →
/// „Wiederherstellen“ bringt den Stand → Speichern räumt die Sicherung weg. Dazu: alte Sicherungen aus „wiederherstellen/“ werden angeboten (Umzug),
/// PDF-Formularwerte hängen an Rückgängig und Notsicherung, Text und Präsentation haben 100 Rückgängig-Schritte wie die Hülle.</summary>
public class T0714_HuelleE2_Ui
{
    static T Get<T>(MainWindow w, string name) where T : Control { Settle(); return w.GetVisualDescendants().OfType<T>().Last(c => c.Name == name && c.IsEffectivelyVisible && c.Bounds.Width > 0); }
    static bool Has(MainWindow w, string name) { Settle(); return w.GetVisualDescendants().OfType<Control>().Any(c => c.Name == name && c.IsEffectivelyVisible); }
    static void Press(MainWindow w, string name) { var b = Get<Button>(w, name); b.BringIntoView(); Settle(); RealClick(w, Get<Button>(w, name)); Settle(); }
    static void Pause() { var until = DateTime.Now + MainWindow.LinkTypingDelay + TimeSpan.FromMilliseconds(150); while (DateTime.Now < until) { Avalonia.Threading.Dispatcher.UIThread.RunJobs(); Thread.Sleep(20); } Settle(); }

    static MainWindow Reopen(string root)
    {
        var w = new MainWindow(new TrendoCore(root, new InMemorySecretStore(), new FakeLauncher()), new AppOptions(root, null, 0, Isolated: true), new Probe("browser"));
        w.ProgramSetLevel(UserLevel.Erweitert); w.FreigabeConfirm = _ => Task.FromResult(true); w.Width = 1600; w.Height = 1100; w.Show(); Settle(); return w;
    }

    static string[] Backups(string root, string studio) { var d = Path.Combine(root, "programme", "sicherung"); return Directory.Exists(d) ? Directory.GetFiles(d, studio + "-*.tdoc") : []; }

    static void Cycle(string kind, Action<MainWindow> open, Action<MainWindow> change, Func<MainWindow, string> state, Action<MainWindow, string> save)
    {
        var (w, _, root) = Ui.Open(); w.Width = 1600; w.Height = 1100;
        try
        {
            open(w); Settle();
            var s0 = state(w);
            change(w); Settle();
            var s1 = state(w); Assert.NotEqual(s0, s1);
            w.ProgramAutosaveAll(); // Takt der Hülle (alle 20 s)
            var file = Assert.Single(Backups(root, "LINK-" + kind.ToUpperInvariant()));
            Assert.True(ProgramDocument.Readable(file));
            Assert.Equal(kind, ProgramDocument.Load(file).Data["format"]!.GetValue<string>());
            Assert.False(Directory.Exists(Path.Combine(root, "wiederherstellen")), kind + ": der alte zweite Weg schreibt noch");

            var other = Reopen(root); // zweites Fenster, solange das erste läuft: bietet die Sicherung NICHT an
            try { open(other); Settle(); Assert.False(Has(other, $"office-recovery-{kind}"), kind + ": zweites Fenster bietet die Sicherung des ersten an"); }
            finally { other.Close(); }

            w.Close(); // ohne Speichern
            var w2 = Reopen(root);
            try
            {
                open(w2); Settle();
                Assert.True(Has(w2, $"office-recovery-{kind}"), kind + ": keine Wiederherstellen-Karte");
                Press(w2, $"office-restore-{kind}-0");
                Assert.Equal(s1, state(w2));
                Assert.False(Has(w2, $"office-recovery-{kind}"), kind + ": zurückgeholte Sicherung wird erneut angeboten");
                Assert.True(File.Exists(file)); // bleibt, bis gespeichert ist
                save(w2, root); Settle();
                Assert.False(File.Exists(file), kind + ": nach dem Speichern liegt die Sicherung noch");
            }
            finally { if (w2.IsVisible) w2.Close(); }
        }
        finally { if (w.IsVisible) w.Close(); }
    }

    static string TextState(MainWindow w) => string.Join("|", w.TextDocument.Paras.Select(p => p.Text));

    [AvaloniaFact(DisplayName = "T-0714 Text: über die Tastatur getippt, Fenster ohne Speichern geschlossen → Karte im neuen Fenster → Wiederherstellen bringt den Text; Speichern räumt weg")]
    public void Text_cycle() => Cycle("text",
        w => { w.TextNew(); w.Show("text"); },
        w => { w.Find<TextPageView>("text-edit").Focus(); Settle(); w.KeyTextInput("Kündigung zum 31.12."); Settle(); w.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None); w.KeyReleaseQwerty(PhysicalKey.Enter, RawInputModifiers.None); w.KeyTextInput("Mit freundlichen Grüßen"); Settle(); },
        TextState, (w, root) => Assert.True(w.TextSaveAs(Path.Combine(root, "kuendigung.docx"))));

    static string SheetState(MainWindow w) => string.Join("|", w.SheetDocument.Sheets[0].Cells.OrderBy(c => c.Key).Select(c => $"{c.Key.Row},{c.Key.Col}={c.Value.Raw}"));

    [AvaloniaFact(DisplayName = "T-0714 Tabelle: Werte und Formel eingetippt, Fenster ohne Speichern geschlossen → Karte → Wiederherstellen bringt die Mappe; Speichern räumt weg")]
    public void Sheet_cycle() => Cycle("tabelle",
        w => { w.SheetNew(); w.Show("tabelle"); },
        w => { w.SheetType("12,5"); w.SheetType("7,5"); w.SheetType("=SUMME(A1:A2)"); },
        SheetState, (w, root) => Assert.True(w.SheetSaveAs(Path.Combine(root, "mappe.xlsx"))));

    static string DeckState(MainWindow w) => string.Join("|", w.Deck.Slides.SelectMany(s => s.Shapes).Select(s => s.Text)) + "#" + w.Deck.Slides.Count;

    [AvaloniaFact(DisplayName = "T-0714 Präsentation: Titel geschrieben und Folie dupliziert, Fenster ohne Speichern geschlossen → Karte → Wiederherstellen bringt die Folien; Speichern räumt weg")]
    public void Deck_cycle() => Cycle("praesentation",
        w => { w.DeckNew(); w.Show("praesentation"); },
        w => { w.DeckType(0, "Quartalszahlen 2026"); w.DeckDuplicate(); w.Show("praesentation"); },
        DeckState, (w, root) => Assert.True(w.DeckSaveAs(Path.Combine(root, "vortrag.pptx"))));

    [AvaloniaFact(DisplayName = "T-0714 CAD: Quader und Maß geändert, Fenster ohne Speichern geschlossen → Karte → Wiederherstellen bringt das Bauteil (mit Versionsfeld); Speichern räumt weg")]
    public void Cad_cycle() => Cycle("cad",
        w => { w.CadNew(); w.Show("cad"); },
        w => { w.CadAdd(CadOp.Quader); w.CadSet("breite", "55"); w.Show("cad"); },
        w => w.Cad.ToJson(), (w, root) => { w.CadSave(Path.Combine(root, "halter.tcad")); Assert.Contains("\"Version\": 1", File.ReadAllText(Path.Combine(root, "halter.tcad"))); });

    [AvaloniaFact(DisplayName = "T-0714 3D: zwei Grundformen hinzugefügt, Fenster ohne Speichern geschlossen → Karte → Wiederherstellen bringt die Szene vollständig; Speichern im Projekt räumt weg")]
    public void Scene_cycle() => Cycle("3d",
        w => w.Show("3d"),
        w => { w.Studio3DAddShape("cube"); w.Studio3DAddShape("sphere"); w.Show("3d"); },
        w => w.Studio3DScene.ToJson(), (w, root) => { Press(w, "3d-save"); Assert.False(w.Scene3DDirty); });

    [AvaloniaFact(DisplayName = "T-0714 Umzug: Sicherungen der früheren Notsicherung (wiederherstellen/text.docx, cad.tcad, tabelle.xlsx) werden beim ersten Start angeboten, mit Zeitpunkt; Zurückholen und Verwerfen gehen")]
    public void Old_recovery_files_offered()
    {
        var (w0, _, root) = Ui.Open(); w0.Close();
        var old = Path.Combine(root, "wiederherstellen"); Directory.CreateDirectory(old);
        TextFiles.Save(new TextDoc { Blocks = [new Para("Alter Entwurf vor dem Umzug")] }, Path.Combine(old, "text.docx"));
        File.WriteAllText(Path.Combine(old, "cad.tcad"), "{\"Params\":{\"Breite\":40},\"Bodies\":[{\"Name\":\"Teil 1\",\"Color\":\"#2F86F6\",\"Features\":[{\"Op\":\"Quader\",\"Name\":\"Quader\",\"P\":{\"breite\":\"Breite\",\"tiefe\":\"30\",\"hoehe\":\"10\"}}]}],\"Segments\":48}");
        var sheet = new SheetDoc(); sheet.Sheets[0].Set("A1", "4711"); SheetFiles.Save(sheet, Path.Combine(old, "tabelle.xlsx"));
        var when = new DateTime(2026, 11, 10, 9, 30, 0);
        foreach (var f in Directory.GetFiles(old)) File.SetLastWriteTime(f, when);

        var w = Reopen(root);
        try
        {
            w.TextNew(); w.Show("text"); Settle();
            Assert.True(Has(w, "office-recovery-text"), "alte Text-Sicherung wird nicht angeboten");
            var row = w.GetVisualDescendants().OfType<TextBlock>().First(t => t.Text?.Contains("ältere Sicherung") == true);
            Assert.Contains("10.11.2026, 09:30", row.Text); // Zeitpunkt der alten Sicherung bleibt
            Assert.False(Directory.Exists(old)); // alles übernommen, alter Ordner leer und weg
            Assert.Single(Backups(root, "LINK-TEXT")); Assert.Single(Backups(root, "LINK-CAD")); Assert.Single(Backups(root, "LINK-TABELLE"));
            Press(w, "office-restore-text-0");
            Assert.Equal("Alter Entwurf vor dem Umzug", TextState(w)); Assert.True(w.TextDirty); Assert.Null(w.TextPath);

            w.CadNew(); w.Show("cad"); Settle();
            Press(w, "office-restore-cad-0");
            Assert.Equal(40, w.Cad.Params["Breite"]); Assert.Equal(CadDoc.FormatVersion, w.Cad.Version); Assert.True(w.CadDirty);

            w.SheetNew(); w.Show("tabelle"); Settle();
            Press(w, "office-discard-tabelle-0");
            Assert.Empty(Backups(root, "LINK-TABELLE")); Assert.False(Has(w, "office-recovery-tabelle"));
        }
        finally { w.Close(); }
        // im nächsten Fenster: der zurückgeholte, nicht gespeicherte Text wird wieder angeboten (nichts geht verloren), der alte Ordner bleibt weg
        var w2 = Reopen(root);
        try { w2.TextNew(); w2.Show("text"); Settle(); Assert.True(Has(w2, "office-recovery-text")); Assert.False(Directory.Exists(old)); }
        finally { w2.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0714 Text und Präsentation: 100 Rückgängig-Schritte wie die Hülle (vorher 80)")]
    public void Undo_depth_100()
    {
        var (w, _, _) = Ui.Open();
        try
        {
            w.TextNew(); w.Show("text"); Settle();
            for (var i = 1; i <= 120; i++) w.TextSetParagraph(firstLineCm: i / 100.0);
            for (var i = 0; i < 130; i++) w.TextUndo();
            Assert.Equal(0.2, ((Para)w.TextDocument.Blocks[0]).FirstLineCm, 6); // Stand nach Änderung 20 – mit 80 Schritten wäre es 0,4

            w.DeckNew(); w.Show("praesentation"); Settle();
            for (var i = 1; i <= 120; i++) w.DeckSetText(0, "Fassung " + i);
            for (var i = 0; i < 130; i++) w.DeckUndo();
            Assert.Equal("Fassung 20", w.Deck.Slides[0].Shapes[0].Text);
        }
        finally { w.Close(); }
    }

    static void MakeForm(string file)
    {
        var blank = file + ".leer.pdf";
        PdfTools.NewBlank(blank);
        PdfFormular.AddFields(blank, file, [new("Name", "text", 0, 60, 100, 220, 20), new("Zustimmung", "haken", 0, 60, 140, 12, 12)]);
        File.Delete(blank);
    }

    [AvaloniaFact(DisplayName = "T-0714 PDF-Formular: Werte tippen und ankreuzen → Rückgängig/Wiederholen; Fenster ohne Speichern geschlossen → Karte öffnet die PDF mit den Werten; „Ausgefüllt speichern“ schreibt sie und räumt die Sicherung weg")]
    public void Pdf_form_values()
    {
        MainWindow.LinkTypingDelay = TimeSpan.FromMilliseconds(300);
        var (w, _, root) = Ui.Open(); w.Width = 1600; w.Height = 1100;
        try
        {
            var pdf = Path.Combine(root, "Antrag.pdf"); MakeForm(pdf);
            w.OpenPdf(pdf); w.Show("pdf"); Settle();
            Assert.False(Get<Button>(w, "pdf-form-undo").IsEnabled);
            var box = Get<TextBox>(w, "pdf-field-Name"); box.BringIntoView(); Settle(); RealClick(w, Get<TextBox>(w, "pdf-field-Name")); Get<TextBox>(w, "pdf-field-Name").Focus(); Settle();
            foreach (var ch in "Erika Muster") { w.KeyTextInput(ch.ToString()); Settle(); }
            Pause();
            var cb = Get<CheckBox>(w, "pdf-field-Zustimmung"); cb.BringIntoView(); Settle(); RealClick(w, Get<CheckBox>(w, "pdf-field-Zustimmung")); Pause();
            Assert.True(Get<CheckBox>(w, "pdf-field-Zustimmung").IsChecked);
            Press(w, "pdf-form-undo"); Assert.False(Get<CheckBox>(w, "pdf-field-Zustimmung").IsChecked == true); Assert.Equal("Erika Muster", Get<TextBox>(w, "pdf-field-Name").Text);
            Press(w, "pdf-form-undo"); Assert.Equal("", Get<TextBox>(w, "pdf-field-Name").Text ?? "");
            Press(w, "pdf-form-redo"); Press(w, "pdf-form-redo");
            Assert.Equal("Erika Muster", Get<TextBox>(w, "pdf-field-Name").Text); Assert.True(Get<CheckBox>(w, "pdf-field-Zustimmung").IsChecked);
            w.ProgramAutosaveAll();
            var file = Assert.Single(Backups(root, "LINK-PDFFORM"));
            w.Close(); // ohne „Ausgefüllt speichern“

            var w2 = Reopen(root);
            try
            {
                w2.Show("pdf"); Settle();
                Assert.True(Has(w2, "pdf-form-recovery"), "keine Wiederherstellen-Karte für das Formular");
                Press(w2, "pdf-form-restore-0");
                Assert.True(ProgramDocument.SameFile(pdf, w2.PdfPath!)); // die PDF ist wieder offen
                Assert.Equal("Erika Muster", Get<TextBox>(w2, "pdf-field-Name").Text); Assert.True(Get<CheckBox>(w2, "pdf-field-Zustimmung").IsChecked);
                Assert.True(File.Exists(file));
                Press(w2, "pdf-fill-go");
                var filled = Assert.Single(Directory.GetFiles(root, "Antrag-ausgefuellt*.pdf"));
                var values = PdfTools.Fields(filled).ToDictionary(f => f.Name, f => f.Value);
                Assert.Equal("Erika Muster", values["Name"]); Assert.Equal("ja", values["Zustimmung"]);
                Assert.False(File.Exists(file), "nach „Ausgefüllt speichern“ liegt die Sicherung noch");
            }
            finally { w2.Close(); }
        }
        finally { MainWindow.LinkTypingDelay = TimeSpan.FromSeconds(1); if (w.IsVisible) w.Close(); }
    }
}
