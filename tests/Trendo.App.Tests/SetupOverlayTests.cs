using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>Einrichtung im Vollbild (Vorgabe 2026-10-26): deckt alles ab, zeigt Fortschritt, nicht wegklickbar bis zum Ende.</summary>
public class SetupOverlayTests
{
    [AvaloniaFact(DisplayName = "Einrichtung: Vollbild-Fläche mit Schritt und Fortschritt, Tasten gesperrt, danach wieder frei")]
    public void Overlay_blocks_until_done()
    {
        var (w, _, _) = Ui.Open();
        var overlay = w.GetLogicalDescendants().OfType<Grid>().Single(g => g.Name == "setup-overlay");
        Assert.False(overlay.IsVisible);
        w.ShowSetupOverlay("Trendo OS richtet deine Programme ein", 12, 20, "Krita wird eingerichtet …");
        Assert.True(w.SetupOverlayOpen);
        Assert.Equal(WindowState.FullScreen, w.WindowState);
        Assert.Equal("Krita wird eingerichtet …", w.GetLogicalDescendants().OfType<TextBlock>().Single(t => t.Name == "setup-overlay-step").Text);
        Assert.StartsWith("12 von 20", w.GetLogicalDescendants().OfType<TextBlock>().Single(t => t.Name == "setup-overlay-percent").Text);
        Assert.Equal(0.6, w.GetLogicalDescendants().OfType<ProgressBar>().Single(b => b.Name == "setup-overlay-bar").Value, 3);
        var key = new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Escape };
        w.RaiseEvent(key);
        Assert.True(key.Handled, "Tasten dürfen die Einrichtung nicht umgehen");
        Assert.True(w.SetupOverlayOpen);
        w.HideSetupOverlay();
        Assert.False(w.SetupOverlayOpen);
        Assert.NotEqual(WindowState.FullScreen, w.WindowState);
        w.Close();
    }

    [AvaloniaFact(DisplayName = "Einrichtung: Logo B im Vollbild, Schließen während der Einrichtung wird abgefangen")]
    public void Close_is_blocked()
    {
        var (w, _, _) = Ui.Open();
        w.ShowSetupOverlay("Test", 0, 0, "Vorbereitung …");
        var overlay = w.GetLogicalDescendants().OfType<Grid>().Single(g => g.Name == "setup-overlay");
        Assert.Contains(overlay.GetLogicalDescendants(), c => c is Viewbox { Name: "trendo-logo" });
        Assert.False(w.MayClose(programmatic: false));
        Assert.True(w.MayClose(programmatic: true));
        w.HideSetupOverlay();
        w.Close();
    }
}
