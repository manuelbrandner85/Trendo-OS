using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Trendo.App;
using Trendo.Core;
using Trendo.Core.Office;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>T-0254 (100): Ungespeicherte Änderungen in Text, Tabelle und Präsentation werden gesichert und nach einem Neustart angeboten.
/// T-0714: Die Sicherung ist jetzt die eine Notsicherung der Hülle (ProgramLink, programme/sicherung/LINK-*.tdoc) statt „wiederherstellen/“; Ablauf und Prüfungen bleiben.</summary>
public class OfficeRecoveryUiTests
{
    static void Pump() { for (var i = 0; i < 5; i++) Avalonia.Threading.Dispatcher.UIThread.RunJobs(); }

    [AvaloniaFact(DisplayName = "T-0254: Text und Tabelle – ungespeichert gesichert, nach Neustart „Wiederherstellen“; „Verwerfen“ löscht; Speichern räumt die Sicherung weg")]
    public void Recover_after_restart()
    {
        var (w, _, root) = Ui.Open();
        w.TextNew(); w.TextDocument.Blocks[0] = new Para("Ungespeicherter Entwurf"); w.TextSelect(0, 0, 14); // T-0692: Fett ohne Markierung gilt nur fürs Weitertippen – hier echt markieren
        w.Show("text"); Pump();
        // Änderung über den normalen Weg markieren
        w.TextToggle(Fmt.Bold);
        Assert.True(w.TextDirty);
        w.SheetNew(); w.SheetSetCell("A1", "4711");
        Assert.True(w.SheetDirty);
        w.ProgramAutosaveAll(); // Takt der Hülle (alle 20 s und beim Schließen)
        Assert.True(w.OfficeBackupPending("text")); Assert.True(w.OfficeBackupPending("tabelle"));
        Assert.False(w.OfficeBackupPending("praesentation")); // nichts ungespeichert
        Assert.Single(Directory.GetFiles(Path.Combine(root, "programme", "sicherung"), "LINK-TEXT-*.tdoc"));
        Assert.False(Directory.Exists(Path.Combine(root, "wiederherstellen"))); // der alte zweite Weg schreibt nichts mehr
        w.Close();

        // „Neustart“ (gleicher Datenordner)
        var core = new TrendoCore(root, new InMemorySecretStore(), new FakeLauncher());
        var w2 = new MainWindow(core, new AppOptions(root, null, 0, Isolated: true), new Probe("browser")); w2.Show(); Pump();
        w2.Show("text"); Pump();
        Assert.NotNull(w2.TryFind<Button>("office-restore-text-0"));
        w2.Find<Button>("office-restore-text-0").Press(); Pump();
        Assert.Equal("Ungespeicherter Entwurf", ((Para)w2.TextDocument.Blocks[0]).Text);
        Assert.True(w2.TextDirty); Assert.Null(w2.TextPath); // bitte selbst speichern
        Assert.Empty(w2.OfficeRecoveries("text")); // zurückgeholt – wird nicht erneut angeboten
        w2.Show("tabelle"); Pump();
        w2.Find<Button>("office-discard-tabelle-0").Press(); Pump();
        Assert.Empty(w2.OfficeRecoveries("tabelle"));
        Assert.Empty(Directory.GetFiles(Path.Combine(root, "programme", "sicherung"), "LINK-TABELLE-*.tdoc")); // „Verwerfen“ löscht
        // Speichern räumt die Sicherung wieder weg
        w2.ProgramAutosaveAll(); Assert.True(w2.OfficeBackupPending("text"));
        w2.TextSaveAs(Path.Combine(root, "entwurf.docx"));
        Assert.False(w2.OfficeBackupPending("text"));
        Assert.Empty(Directory.GetFiles(Path.Combine(root, "programme", "sicherung"), "LINK-TEXT-*.tdoc"));
        w2.Close();
    }
}
