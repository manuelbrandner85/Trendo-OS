using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Trendo.App;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>Gemeinsamer Programmrahmen (Vorschläge G1–G14, freigegeben 27.10.2026).</summary>
public class ProgramFrameTests
{
    static void Pump() { for (var i = 0; i < 5; i++) Dispatcher.UIThread.RunJobs(); }

    [AvaloniaFact(DisplayName = "G3: Fachstudio zeigt keine Reiter fremder Studios; G10: keine Schrift unter 12 pt auf der Seite")]
    public void Own_tabs_and_readable()
    {
        var (w, _, _) = Ui.Open();
        w.Show("elektronik"); Pump();
        Assert.Null(w.TryFind<Button>("fach-tab-architektur"));
        Assert.Null(w.TryFind<Button>("fach-tab-mode"));
        var small = w.ActivePageRoot!.GetLogicalDescendants().OfType<TextBlock>().Where(t => t.FontSize < 12 && !string.IsNullOrEmpty(t.Text)).Select(t => t.Text).ToList();
        Assert.True(small.Count == 0, "zu kleine Schrift: " + string.Join(" | ", small.Take(5)));
        w.Close();
    }

    [AvaloniaFact(DisplayName = "G7: Ergebnis-Karte mit Kopieren und Verlauf früherer Ergebnisse; G12: Benachrichtigung erscheint")]
    public void Result_card_and_toast()
    {
        var (w, _, _) = Ui.Open();
        w.Show("medien");
        w.Toast("Hallo");
        Assert.Contains(w.GetLogicalDescendants().OfType<Border>(), b => b.Name == "toast");
        w.Close();
    }

    [AvaloniaFact(DisplayName = "G8: Strg+Q öffnet „Was möchtest du tun?“, findet Studios und Knöpfe der Seite und führt sie aus; G9/G13: F11 Fokusmodus")]
    public void Palette_and_focus()
    {
        var (w, _, _) = Ui.Open();
        w.Show("start"); Pump();
        w.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Q, KeyModifiers = KeyModifiers.Control });
        Assert.True(w.CommandPaletteOpen);
        w.Find<TextBox>("command-search").Text = "handy verbinden"; Pump();
        Assert.Contains(w.PaletteResults, r => r.Label == "Handy verbinden" && r.Kind == "Studio öffnen");
        Assert.Contains(w.PaletteResults, r => r.Kind == "KI");
        var i = w.PaletteResults.ToList().FindIndex(r => r.Label == "Handy verbinden");
        w.Find<Button>("command-hit-" + i).Press();
        Assert.False(w.CommandPaletteOpen);
        Assert.Equal("handy", w.CurrentView);
        // Knopf der offenen Seite über die Suche auslösen
        w.OpenPalette("QR-Code zum Verbinden"); Pump();
        Assert.Contains(w.PaletteResults, r => r.Kind == "Auf dieser Seite" && r.Label.Contains("QR-Code"));
        w.ClosePalette();

        Assert.NotNull(w.TryFind<Border>("auftrag-leiste"));
        w.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.F11 });
        Assert.True(w.FocusMode);
        Assert.Equal(WindowState.FullScreen, w.WindowState);
        Assert.Null(w.TryFind<Border>("auftrag-leiste"));
        Assert.Null(w.TryFind<Border>("page-hero"));
        w.ToggleFocusMode();
        Assert.False(w.FocusMode);
        w.Close();
    }
}
