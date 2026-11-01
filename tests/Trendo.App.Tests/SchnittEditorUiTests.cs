using System.Xml.Linq;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Trendo.App;
using Trendo.Core;
using Xunit;

namespace Trendo.App.Tests;

/// Runde 7: Kleidung als Schnitt-Editor – Größe der Schnittlage wählen (abgeleitet aus der Basis), Stoffbedarf je Größe, Export als SVG im Maßstab 1:1.
public class SchnittEditor_Ui
{
    [AvaloniaFact(DisplayName = "Schnitt-Editor: Basisgröße bearbeitbar, andere Größe abgeleitet und automatisch gelegt (größer bei XL), Stoffbedarf je Größe, SVG speichern und als Export")]
    public void Schnitt_editor()
    {
        var (w, _, _) = Ui.Open();
        w.Show("mode-programm"); w.Find<Button>("prog-tpl-tshirt").Press();
        var d = w.ProgramActive("mode-programm")!;
        Assert.Contains("(Basis)", (string)w.Find<Button>("mode-marker-size-L").Content!);
        Assert.NotNull(w.TryFind<Button>("mode-marker-auto")); // Basis: bearbeiten
        Assert.NotNull(w.MarkerForTests!.Moved);
        var basis = w.MarkerForTests.Pieces[0];
        Assert.Equal(5, w.Find<Grid>("mode-marker-run").ColumnDefinitions.Count);
        // XL: abgeleitet, nicht verschiebbar
        w.Find<Button>("mode-marker-size-XL").Press();
        Assert.Equal("XL", w.MarkerSize);
        Assert.Null(w.TryFind<Button>("mode-marker-auto")); Assert.Null(w.MarkerForTests!.Moved);
        Assert.Equal(basis.W + 1, w.MarkerForTests.Pieces[0].W); Assert.Equal(basis.H + 1, w.MarkerForTests.Pieces[0].H); // 4 cm Sprung / 4, 1 cm Länge
        Assert.Contains("abgeleitet", w.Find<TextBlock>("mode-marker-hint").Text);
        // Längensprung ändern wirkt sofort
        var len = w.Find<TextBox>("mode-p-gradeLen"); Assert.Equal("1", len.Text);
        len.Text = "3"; len.RaiseEvent(new Avalonia.Input.KeyEventArgs { RoutedEvent = Avalonia.Input.InputElement.KeyDownEvent, Key = Avalonia.Input.Key.Enter }); Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.Equal(basis.H + 3, w.MarkerForTests!.Pieces[0].H);
        // SVG der XL-Lage
        w.Find<Button>("mode-marker-svg").Press();
        Assert.StartsWith("Gespeichert: ", w.ProgramMessage("mode-programm"));
        var svg = w.ProgramMessage("mode-programm")["Gespeichert: ".Length..];
        Assert.EndsWith("XL – Schnittlage.svg", svg); Assert.True(File.Exists(svg));
        var x = XDocument.Parse(File.ReadAllText(svg)); Assert.Equal("140cm", x.Root!.Attribute("width")!.Value);
        Assert.Contains("(XL)", x.Root.Elements().First().Value);
        // zurück zur Basis
        w.Find<Button>("mode-marker-size-L").Press(); Assert.Equal("", w.MarkerSize); Assert.NotNull(w.MarkerForTests!.Moved);
        Assert.Contains(MainWindow.Programs["mode-programm"].Exports, e => e.Extension == ".svg");
        w.Close();
    }
}
