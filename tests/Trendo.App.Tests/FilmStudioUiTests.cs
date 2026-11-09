using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// <summary>Film-Studio Teil 1 (08.11.2026): F1 Ablauf, F2 Drehbuch, F3 Auszug und Einstellungen, F4 Mappe.</summary>
public class FilmStudio_Ui
{
    const string K = "film-programm";

    [AvaloniaFact(DisplayName = "Film: Beispiel öffnen, Drehbuch als Seite, Einstellungen vorschlagen, Figuren übernehmen, Beschreibung bleibt, Export und Rückgängig")]
    public void Flow()
    {
        var (w, _, root) = Ui.Open();
        Assert.Equal(K, MainWindow.StudioPages["STUDIO-FILM-01"]);
        w.Show(K); Settle();
        RealClick(w, w.Find<Button>("prog-tpl-kurzfilm")); Settle();
        var d = w.ProgramActive(K)!;
        Assert.Contains("JONA", d.Text("script"));
        Assert.Contains(w.Find<StackPanel>("film-preview").Children.OfType<TextBlock>(), t => t.Text == "BAHNWÄRTER");

        RealClick(w, w.Find<Button>("prog-cmd-film-shots")); Settle();
        var shots = w.Find<StackPanel>("film-shots").GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text).ToList();
        Assert.Contains("SZ01-E01", shots); Assert.Contains("Totale", shots);
        Assert.Contains("- Totale:", d.Text("script"));

        RealClick(w, w.Find<Button>("prog-cmd-film-sync")); Settle();
        Assert.Equal(2, d.List("figures").Count); Assert.Equal(2, d.List("places").Count);
        var look = w.Find<TextBox>("film-figures-look-0");
        look.Text = "Frau, etwa 35, grauer Mantel"; look.Focus(); w.Find<TextBox>("film-places-look-0").Focus(); Settle();
        Assert.Equal("Frau, etwa 35, grauer Mantel", (string?)d.List("figures")[0]!["look"]);

        // erneutes Übernehmen ändert nichts an vorhandenen Beschreibungen
        RealClick(w, w.Find<Button>("prog-cmd-film-sync")); Settle();
        Assert.Equal("Frau, etwa 35, grauer Mantel", (string?)d.List("figures")[0]!["look"]);
        Assert.Equal(2, d.List("figures").Count);

        Assert.True(d.CanUndo);
        // getippter Text kommt an; ein Befehl danach arbeitet auf dem getippten Stand
        RealClick(w, w.Find<Button>("film-step-drehbuch")); Settle();
        var box = w.Find<TextBox>("film-script"); box.Focus(); box.Text = d.Text("script") + "\n\nAUSSEN. DACH – MORGEN\n\nDie Sonne geht auf.\n"; Settle();
        w.Find<Button>("film-step-figuren").Focus(); Settle();
        Assert.Contains("AUSSEN. DACH", d.Text("script"));
        RealClick(w, w.Find<Button>("prog-cmd-film-shots")); Settle();
        Assert.Contains("- Totale: Die Sonne geht auf.", d.Text("script"));
        RealClick(w, w.Find<Button>("film-step-drehbuch")); Settle();
        Assert.NotNull(w.TryFind<TextBox>("film-script"));
        var csv = Path.Combine(root, "shots.csv");
        Assert.Equal(csv, MainWindow.Programs[K].Exports.Single(x => x.Name == "csv").Write(d, csv));
        Assert.Contains("SZ02-E01", File.ReadAllText(csv));
        w.Close();
    }

    [AvaloniaFact(DisplayName = "Film: PDF ohne typst meldet verständlich, was fehlt")]
    public void Pdf_without_typst()
    {
        var (w, _, root) = Ui.Open();
        MainWindow.ToolExeOverride["typst"] = "";
        try
        {
            w.Show(K); Settle();
            RealClick(w, w.Find<Button>("prog-tpl-kurzfilm")); Settle();
            var ex = Assert.Throws<InvalidOperationException>(() => MainWindow.Programs[K].Exports.Single(x => x.Name == "pdf").Write(w.ProgramActive(K)!, Path.Combine(root, "a.pdf")));
            Assert.Contains("typst", ex.Message);
        }
        finally { MainWindow.ToolExeOverride.Remove("typst"); w.Close(); }
    }
}
