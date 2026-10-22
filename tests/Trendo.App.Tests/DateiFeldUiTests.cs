using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Trendo.App;
using Xunit;

namespace Trendo.App.Tests;

public class DateiFeld_Ui
{
    [AvaloniaFact(DisplayName = "T-0201: jedes Datei- und Ordnerfeld hat „Durchsuchen …“")]
    public void Every_path_field_has_browse()
    {
        var (w, _, _) = Ui.Open();
        var seen = 0;
        foreach (var (key, _, _) in MainWindow.FachTabs)
        {
            w.Show(key);
            var root = w.ActivePageRoot!;
            Assert.True(root.Name != "error-page", key + " zeigt eine Fehlerseite");
            foreach (var box in root.GetLogicalDescendants().OfType<TextBox>())
            {
                if (box.Name is not { } name) continue;
                if (!name.EndsWith("-file", StringComparison.Ordinal) && !name.EndsWith("-file2", StringComparison.Ordinal) && !name.EndsWith("-folder", StringComparison.Ordinal)) continue;
                var browse = root.GetLogicalDescendants().OfType<Button>().FirstOrDefault(b => b.Name == name + "-browse");
                Assert.True(browse is not null, name + " hat keinen Durchsuchen-Knopf");
                Assert.Equal("Durchsuchen …", browse!.Content);
                seen++;
            }
        }
        Assert.True(seen >= 30, "zu wenige Dateifelder: " + seen);

        w.Show("kreativ-medien");
        Assert.Equal("Durchsuchen …", w.Find<Button>("media-path-browse").Content);
        w.Show("finanzen");
        Assert.Equal("Durchsuchen …", w.Find<Button>("fin-csv-browse").Content);
        w.Show("produktion");
        w.Find<Button>("fusion-recipe-video-pack").Press();
        Assert.Equal("Durchsuchen …", w.Find<Button>("fusion-in-file-browse").Content);
        w.Show("einrichtung-1");
        Assert.Equal("Durchsuchen …", w.Find<Button>("wiz-folder-browse").Content);
        w.Close();
    }

    [AvaloniaFact(DisplayName = "T-0201: Durchsuchen setzt den Pfad, merkt ihn sich und filtert den Typ")]
    public async Task Browse_fills_field_and_recent()
    {
        var (w, _, root) = Ui.Open();
        var file = Path.Combine(root, "konto.csv");
        File.WriteAllText(file, "datum;text;betrag\n");
        IReadOnlyList<string>? muster = null;
        string? titel = null;
        w.DateiWahlOverride = (_, t, m) => { titel = t; muster = m; return Task.FromResult<string?>(file); };
        w.Show("finanzen");
        w.Find<Button>("fin-csv-browse").Press();
        await Wait(() => w.Find<TextBox>("fin-csv").Text == file);
        Assert.Equal(file, w.Find<TextBox>("fin-csv").Text);
        Assert.Equal("Bank-CSV", titel);
        Assert.Contains("*.csv", muster!);
        var combo = w.Find<ComboBox>("fin-csv-recent");
        Assert.True(combo.IsVisible);
        Assert.Contains(combo.ItemsSource!.Cast<ZuletztEintrag>(), e => e.Pfad == file);
        Assert.True(DragDrop.GetAllowDrop(w));
        w.Close();
    }

    [AvaloniaFact(DisplayName = "T-0201: Ablegen füllt das leere Feld; Ordnerfeld nimmt den Ordner der Datei")]
    public void Drop_fills_empty_field()
    {
        var (w, _, root) = Ui.Open();
        var file = Path.Combine(root, "scan.gpx");
        File.WriteAllText(file, "<gpx/>");
        w.Show("karten");
        Assert.True(w.DateiAblegen(file));
        Assert.Equal(file, w.Find<TextBox>("geo-gpx-file").Text);
        Assert.Contains(w.ZuletztListe("karten"), p => p == file);

        var ordnerDatei = Path.Combine(root, "notiz.txt");
        File.WriteAllText(ordnerDatei, "x");
        w.Show("einrichtung-1");
        Assert.True(w.DateiAblegen(ordnerDatei));
        Assert.Equal(root, w.Find<TextBox>("wiz-folder").Text);
        w.Close();
    }

    private static async Task Wait(Func<bool> done)
    {
        for (var i = 0; i < 40 && !done(); i++)
        {
            await Task.Delay(20);
            Dispatcher.UIThread.RunJobs();
        }
    }
}
