using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Trendo.App;
using Trendo.Core.Programs;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>T-0231 V2/V3/V9: Programmrahmen am Beispiel „Kleidung & Produkte“.</summary>
public class ProgramShellUiTests
{
    static void Pump() { for (var i = 0; i < 5; i++) Dispatcher.UIThread.RunJobs(); }

    [AvaloniaFact(DisplayName = "Programm: Startseite → Vorlage → bearbeiten → Rückgängig → Speichern → Zuletzt → wieder öffnen; Reiter für zwei Dokumente")]
    public void Program_flow()
    {
        var (w, _, _) = Ui.Open();
        Assert.Equal("mode-programm", MainWindow.StudioPages["SPEC-08"]);
        w.Show("mode-programm"); Pump();
        Assert.NotNull(w.TryFind<WrapPanel>("prog-templates"));
        w.Find<Button>("prog-tpl-tshirt").Press(); Pump();
        Assert.NotNull(w.TryFind<WrapPanel>("prog-ribbon"));
        Assert.Contains("Passende Größe", w.Find<TextBlock>("mode-size").Text);
        // Eigenschaft ändern (Fokusverlust) und rückgängig machen
        var name = w.Find<TextBox>("mode-p-name"); name.Text = "Shirt Premium";
        name.RaiseEvent(new Avalonia.Input.KeyEventArgs { RoutedEvent = Avalonia.Input.InputElement.KeyDownEvent, Key = Avalonia.Input.Key.Enter }); Pump();
        Assert.Equal("Shirt Premium", w.Find<TextBlock>("mode-card-name").Text);
        w.Find<Button>("prog-undo").Press(); Pump();
        Assert.Equal("T-Shirt Basic", w.Find<TextBlock>("mode-card-name").Text);
        w.Find<Button>("prog-redo").Press(); Pump();
        w.Find<Button>("prog-cmd-variants").Press(); Pump();
        Assert.Equal(12, w.ProgramActive("mode-programm")!.List("variants").Count);
        // Speichern
        w.Find<Button>("prog-save").Press(); Pump();
        var file = w.ProgramActive("mode-programm")!.Path!;
        Assert.True(File.Exists(file)); Assert.EndsWith(".tdoc", file);
        Assert.Equal("Shirt Premium", ProgramDocument.Load(file).Text("name"));
        // Export
        w.Find<Button>("prog-export-sheet").Press(); Pump();
        Assert.Contains("Exportiert", w.ProgramMessage("mode-programm"));
        // zweites Dokument im Reiter
        w.Find<Button>("prog-tab-new").Press(); Pump();
        Assert.Equal(2, w.ProgramDocs("mode-programm").Count);
        w.Find<Button>("prog-tab-0").Press(); Pump();
        Assert.Equal("T-Shirt", w.ProgramActive("mode-programm")!.Title);
        // schließen und über „Zuletzt“ wieder öffnen
        w.Find<Button>("prog-close").Press(); Pump();
        w.Find<Button>("prog-close").Press(); Pump();
        Assert.Empty(w.ProgramDocs("mode-programm"));
        w.Find<Button>("prog-recent-0").Press(); Pump();
        Assert.Equal(file, w.ProgramActive("mode-programm")!.Path);
        w.Close();
    }

    [AvaloniaFact(DisplayName = "Programm: ungespeichertes Dokument wird automatisch gesichert und auf der Startseite wiederhergestellt")]
    public void Autosave_restore()
    {
        var (w, _, _) = Ui.Open();
        w.Show("mode-programm"); Pump();
        w.Find<Button>("prog-tpl-rock").Press(); Pump();
        w.Find<Button>("prog-cmd-piece-add").Press(); Pump();
        w.ProgramAutosaveAll();
        w.Find<Button>("prog-close").Press(); Pump();
        w.Find<Button>("prog-restore-0").Press(); Pump();
        var d = w.ProgramActive("mode-programm")!;
        Assert.True(d.Dirty); Assert.Equal(4, d.List("pieces").Count);
        w.Close();
    }
}
