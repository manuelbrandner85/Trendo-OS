using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Trendo.App;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>Modernisierung 68 (freigegeben 07.11.2026): globale Punkte G1–G12.</summary>
public class Modern_Ui
{
    static void Pump() { for (var i = 0; i < 5; i++) Dispatcher.UIThread.RunJobs(); }

    [AvaloniaFact(DisplayName = "G2: Befehlsleiste mit Gruppen-Überschriften und Symbolen; ausgeführter Befehl steht danach oben unter „Zuletzt benutzt“")]
    public void Palette_recent_groups()
    {
        var (w, _, _) = Ui.Open();
        try
        {
            w.OpenPalette("Trendo Musik"); Pump();
            Assert.NotNull(w.TryFind<TextBlock>("command-group-0"));
            var i = w.PaletteResults.ToList().FindIndex(r => r.Label == "Trendo Musik" && r.Kind == "Studio öffnen");
            Assert.True(i >= 0);
            w.Find<Button>("command-hit-" + i).Press(); Pump();
            Assert.False(w.CommandPaletteOpen);
            Assert.Equal("Trendo Musik|Studio öffnen", w.PaletteRecent()[0]);
            w.OpenPalette(); Pump();
            Assert.Equal(("Trendo Musik", "Zuletzt benutzt"), w.PaletteResults[0]);
            Assert.Equal("ZULETZT BENUTZT", w.Find<TextBlock>("command-group-0").Text);
            w.Find<Button>("command-hit-0").Press(); Pump();
            Assert.Equal("Trendo Musik|Studio öffnen", Assert.Single(w.PaletteRecent()));
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "G9: Seitenleisten in Trendo Musik mit Griff; Breite gemerkt und beim nächsten Öffnen angewandt; Doppelklick klappt ein und zur letzten Breite aus")]
    public void Panes()
    {
        var (w, _, _) = Ui.Open();
        try
        {
            w.Show("musik-studio"); Pump();
            var grip = w.Find<GridSplitter>("pane-grip-musik-links");
            var grid = (Grid)grip.Parent!;
            Assert.Equal(360, grid.ColumnDefinitions[2].Width.Value);
            w.SavePaneWidth("musik/links", 420);
            w.Show("musik-studio"); Pump();
            grid = (Grid)w.Find<GridSplitter>("pane-grip-musik-links").Parent!;
            Assert.Equal(420, grid.ColumnDefinitions[2].Width.Value);
            w.TogglePane(grid, 2, "musik/links", 360); Assert.Equal(0, grid.ColumnDefinitions[2].Width.Value); Assert.Equal(0, w.PaneWidths()["musik/links"]);
            w.TogglePane(grid, 2, "musik/links", 360); Assert.Equal(420, grid.ColumnDefinitions[2].Width.Value);
            w.SavePaneWidth("musik/rechts", 5000); w.Show("musik-studio"); Pump();
            Assert.Equal(560, ((Grid)w.Find<GridSplitter>("pane-grip-musik-rechts").Parent!).ColumnDefinitions[6].Width.Value); // begrenzt
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "G10: KI-Aufträge der Grafikkarten-Warteschlange als Karten – läuft mit Laufzeit, wartet mit Platz; fertig = Karte weg")]
    public async Task Task_cards()
    {
        var (w, _, _) = Ui.Open();
        try
        {
            var t0 = DateTimeOffset.Now; w.TaskClockOverride = () => t0.AddSeconds(75);
            Assert.Empty(w.Find<StackPanel>("task-cards").Children);
            var a = await w.Gpu.AcquireAsync("musik", "Lied „Hafen“");
            var waiting = w.Gpu.AcquireAsync("bild", "Cover-Bild");
            Pump(); w.PaintTaskCards();
            var cards = w.Find<StackPanel>("task-cards").Children.OfType<Border>().ToList();
            Assert.Equal(2, cards.Count);
            var states = w.Find<StackPanel>("task-cards").GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text).ToList();
            Assert.Contains(states, s => s!.StartsWith("Musik-KI · läuft seit 1:1"));
            Assert.Contains("Bild-KI · wartet · Platz 1", states);
            Assert.Contains("Lied „Hafen“", states);
            a.Dispose(); var b = await waiting; Pump(); w.PaintTaskCards();
            Assert.Single(w.Find<StackPanel>("task-cards").Children);
            b.Dispose(); Pump(); w.PaintTaskCards();
            Assert.Empty(w.Find<StackPanel>("task-cards").Children);
        }
        finally { w.Close(); }
    }
}
