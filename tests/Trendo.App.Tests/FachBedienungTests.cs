using System.Text.RegularExpressions;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Trendo.App;
using Trendo.Core;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>T-0207: Fach-Studios – Auswahllisten statt Freitext, Zahlenprüfung, Stand bleibt erhalten.</summary>
public class FachBedienungTests
{
    [AvaloniaFact(DisplayName = "T-0207: Felder mit fester Wertemenge sind Auswahllisten; kein Hinweistext zählt Werte auf")]
    public void Choices_instead_of_free_text()
    {
        var (w, _, _) = Ui.Open();
        var aufzaehlung = new Regex(@":\s*[a-zäöü]+(, [a-zäöü]+){2,}\.?$");
        foreach (var (page, _, _) in MainWindow.FachTabs)
        {
            w.Show(page); Dispatcher.UIThread.RunJobs();
            foreach (var ex in w.GetLogicalDescendants().OfType<Expander>().ToList()) ex.IsExpanded = true;
            Dispatcher.UIThread.RunJobs();
            var hints = w.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text ?? "").Where(s => aufzaehlung.IsMatch(s)).ToList();
            Assert.True(hints.Count == 0, page + ": Aufzählung im Hinweis – " + string.Join(" | ", hints));
        }
        // SPEC-01/03 sind seit T-0256/T-0258 Programme ohne Formular – Wahlfelder hier am Beispiel Fertigung & CNC
        w.Show(MainWindow.StudioPages["SPEC-14"]);
        foreach (var ex in w.GetLogicalDescendants().OfType<Expander>().ToList()) ex.IsExpanded = true;
        Dispatcher.UIThread.RunJobs();
        var mat = w.Find<ComboBox>("cnc-feeds-mat");
        Assert.Contains("Aluminium", mat.ItemsSource!.Cast<string>());
        w.Close();
    }

    // AvaloniaFact: berührt MainWindow-Statics (Pinsel) – nur auf dem UI-Thread, sonst gehören sie einem fremden Thread
    [AvaloniaFact(DisplayName = "T-0207: Ja/Nein- und Kurzwahl-Felder werden über die Beschriftung erkannt, Maßeinheiten nicht")]
    public void Choice_detection()
    {
        Assert.Equal(["j", "n"], MainWindow.ChoicesFor("x", "ev", "Elektro? (j/n)")!);
        Assert.Equal(["90", "95", "99"], MainWindow.ChoicesFor("x", "c", "Sicherheit (90/95/99)")!);
        Assert.Null(MainWindow.ChoicesFor("x", "move", "Gehtempo (m/s)"));
        Assert.Null(MainWindow.ChoicesFor("x", "n", "Drehzahl (U/min)"));
        Assert.True(MainWindow.LooksNumeric("12,5")); Assert.False(MainWindow.LooksNumeric("zwölf"));
    }

    [AvaloniaFact(DisplayName = "T-0207: Zahlenfeld zeigt Fehler sofort; Eingaben und Ergebnis bleiben nach Neustart erhalten")]
    public void Number_check_and_state_survives()
    {
        var (w, l, root) = Ui.Open();
        w.Show(MainWindow.StudioPages["SPEC-14"]);
        foreach (var ex in w.GetLogicalDescendants().OfType<Expander>().ToList()) ex.IsExpanded = true;
        Dispatcher.UIThread.RunJobs();
        var v = w.Find<TextBox>("cnc-feeds-d");
        v.Text = "zwölf"; Dispatcher.UIThread.RunJobs(); Assert.NotNull(ToolTip.GetTip(v));
        v.Text = "8"; Dispatcher.UIThread.RunJobs(); Assert.Null(ToolTip.GetTip(v));
        var other = w.Find<ComboBox>("cnc-feeds-mat").ItemsSource!.Cast<string>().First(x => x != "Aluminium");
        w.Find<ComboBox>("cnc-feeds-mat").SelectedItem = other;
        w.Find<Button>("cnc-feeds-run").Press();
        var res = w.Find<SelectableTextBlock>("cnc-feeds-out").Text;
        Assert.StartsWith("Drehzahl", res);
        w.Close();
        // gleicher Datenordner, neues Fenster: Stand ist wieder da
        var core = new TrendoCore(root, new InMemorySecretStore(), l);
        var w2 = new MainWindow(core, new AppOptions(root, null, 0, false, Isolated: true), new Probe("browser")); w2.Show(); Dispatcher.UIThread.RunJobs();
        w2.Show(MainWindow.StudioPages["SPEC-14"]);
        foreach (var ex in w2.GetLogicalDescendants().OfType<Expander>().ToList()) ex.IsExpanded = true;
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(other, w2.Find<ComboBox>("cnc-feeds-mat").SelectedItem);
        Assert.Equal(res, w2.Find<SelectableTextBlock>("cnc-feeds-out").Text);
        w2.Close();
    }

    [AvaloniaFact(DisplayName = "T-0207: Grafiken der Generatoren lassen sich im Studio zeichnen (Grundriss, Strecke, Diagramme, Balken)")]
    public void Svg_generators_render()
    {
        var beam = Engineering.Beam(BeamCase.EinfeldMitte, 2, 1, Engineering.Rect(60, 120), Engineering.Materials[0]);
        string[] svgs =
        [
            RoomPlanner.Svg([new Room("Küche", 4, 3), new Room("Bad", 2.5, 2)]),
            Geo.Svg([new GeoPoint(47.40, 10.98, 1000), new GeoPoint(47.41, 10.99, 1100), new GeoPoint(47.42, 10.97, 1050)]),
            StatsPlus.HistogramSvg(StatsPlus.Histogram([1, 2, 2, 3, 3, 3, 4]), "Test"),
            StatsPlus.ScatterSvg([1, 2, 3, 4], [2, 4, 5, 8], "y über x"),
            Engineering.BeamSvg(BeamCase.EinfeldMitte, 2, beam),
            Engineering.BeamSvg(BeamCase.KragarmStrecke, 2, beam),
        ];
        foreach (var s in svgs)
        {
            var c = SvgLite.Render(s);
            Assert.NotEmpty(((Canvas)((Viewbox)c).Child!).Children);
        }
    }
}
