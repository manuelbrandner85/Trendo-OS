using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Trendo.Core;

namespace Trendo.App;

/// <summary>Trendo Musik V13 wie Suno (07.11.2026): Karte „Mein Geschmack“ beim Erstellen – gelernt nur lokal aus den ★-Favoriten, Wörter sichtbar,
/// „Lied in meinem Geschmack“ füllt Beschreibung bzw. Stil. In der Bibliothek sortiert „Für dich“ nach Geschmack.</summary>
public partial class MainWindow
{
    int _msTasteSeed = Environment.TickCount;

    /// <summary>Vorschlag aus dem Geschmack übernehmen (null = noch kein Geschmack, mindestens zwei Favoriten).</summary>
    public string? MsTakeTaste(int? seed = null)
    {
        var prof = MusicTaste.Profile(Songs.Visible());
        if (prof.Count == 0) { _msMsg = "Markiere mindestens zwei Lieder mit – daraus lernt Trendo deinen Geschmack (bleibt auf deinem PC)."; Show(MS); return null; }
        var idea = MusicTaste.Suggest(prof, seed ?? _msTasteSeed++);
        if (_msPro) _msStyle = idea; else _msDesc = idea;
        _msMsg = "Vorschlag aus deinem Geschmack – anpassen oder gleich „Erstellen“."; Show(MS);
        return idea;
    }

    Control MsTasteCard()
    {
        var prof = MusicTaste.Profile(Songs.Visible());
        var sp = new StackPanel { Spacing = 4 };
        var go = new Button { Name = "ms-taste-go", Content = "Lied in meinem Geschmack", FontSize = 11.5, Padding = new Thickness(10, 3), CornerRadius = new CornerRadius(12), BorderThickness = new Thickness(0), Background = Brush.Parse("#33D4AF37"), IsEnabled = prof.Count > 0 };
        go.Click += (_, _) => MsTakeTaste();
        sp.Children.Add(new DockPanel { Children = { Dk(go, Avalonia.Controls.Dock.Right), new TextBlock { Text = "MEIN GESCHMACK", Foreground = Muted, FontSize = 10, FontWeight = FontWeight.Bold, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center } } });
        if (prof.Count == 0) sp.Children.Add(P("Markiere Lieder mit – Trendo lernt daraus, was dir gefällt. Nur auf deinem PC, nichts wird gesendet.", Muted, 11));
        else
        {
            var chips = new WrapPanel { Name = "ms-taste-words" };
            var top = prof[0].Weight;
            foreach (var w in prof.Take(8))
                chips.Children.Add(new Border { Padding = new Thickness(8, 2), Margin = new Thickness(0, 0, 4, 4), CornerRadius = new CornerRadius(10), Background = Brush.Parse("#1AFFFFFF"),
                    Child = new TextBlock { Text = w.Text, FontSize = 11, Foreground = Text, Opacity = 0.55 + 0.45 * w.Weight / top } });
            sp.Children.Add(chips);
        }
        return new Border { Name = "ms-taste", Padding = new Thickness(10, 8), CornerRadius = new CornerRadius(12), Margin = new Thickness(0, 0, 0, 6), Background = Brush.Parse("#10FFFFFF"), Child = sp };
    }
}
