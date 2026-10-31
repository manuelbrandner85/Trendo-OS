using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Trendo.App;
using Trendo.Core;
using Trendo.Core.Office;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>T-0254 (100): Ungespeicherte Änderungen in Text, Tabelle und Präsentation werden gesichert und nach einem Neustart angeboten.</summary>
public class OfficeRecoveryUiTests
{
    static void Pump() { for (var i = 0; i < 5; i++) Avalonia.Threading.Dispatcher.UIThread.RunJobs(); }

    [AvaloniaFact(DisplayName = "T-0254: Text und Tabelle – ungespeichert gesichert, nach Neustart „Wiederherstellen“; „Verwerfen“ löscht; Speichern räumt die Sicherung weg")]
    public void Recover_after_restart()
    {
        var (w, _, root) = Ui.Open();
        w.TextNew(); w.TextDocument.Blocks[0] = new Para("Ungespeicherter Entwurf"); w.TextSelect(0, 0, 0);
        w.Show("text"); Pump();
        // Änderung über den normalen Weg markieren
        w.TextToggle(Fmt.Bold);
        Assert.True(w.TextDirty);
        w.SheetNew(); w.SheetSetCell("A1", "4711");
        Assert.True(w.SheetDirty);
        w.OfficeAutosave();
        Assert.True(w.RecoveryAvailable("text")); Assert.True(w.RecoveryAvailable("tabelle"));
        Assert.False(w.RecoveryAvailable("praesentation")); // nichts ungespeichert
        w.Close();

        // „Neustart“ (gleicher Datenordner)
        var core = new TrendoCore(root, new InMemorySecretStore(), new FakeLauncher());
        var w2 = new MainWindow(core, new AppOptions(root, null, 0, Isolated: true), new Probe("browser")); w2.Show(); Pump();
        w2.Show("text"); Pump();
        Assert.NotNull(w2.TryFind<Button>("office-restore-text"));
        w2.Find<Button>("office-restore-text").Press(); Pump();
        Assert.Equal("Ungespeicherter Entwurf", ((Para)w2.TextDocument.Blocks[0]).Text);
        Assert.True(w2.TextDirty); Assert.Null(w2.TextPath); // bitte selbst speichern
        Assert.False(w2.RecoveryAvailable("text"));
        w2.Show("tabelle"); Pump();
        w2.Find<Button>("office-discard-tabelle").Press(); Pump();
        Assert.False(w2.RecoveryAvailable("tabelle"));
        // Speichern räumt eine neue Sicherung wieder weg
        w2.OfficeAutosave(); Assert.True(w2.RecoveryAvailable("text"));
        w2.TextSaveAs(Path.Combine(root, "entwurf.docx"));
        Assert.False(w2.RecoveryAvailable("text"));
        w2.Close();
    }
}
