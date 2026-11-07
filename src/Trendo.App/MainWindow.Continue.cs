using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace Trendo.App;

/// <summary>Modernisierung G4 (freigegeben 07.11.2026): „Weitermachen“ auf dem Desktop – die zuletzt benutzten Programme und Studios als große Karten,
/// ein Klick öffnet genau dort. Quelle ist der vorhandene Verlauf des Startmenüs (<c>RecentPages</c>), nichts wird erfunden.</summary>
public partial class MainWindow
{
    Control ContinuePanel()
    {
        var keys = _settings.RecentPages.Where(k => _showMap.ContainsKey(k) && k is not ("start" or "settings")).Take(6).ToList();
        var row = new WrapPanel { Name = "continue-cards" };
        var i = 0;
        foreach (var k in keys)
        {
            var key = k; var title = PageTitle(k);
            var card = new Button { Name = "continue-" + i++, Width = 196, Height = 72, Margin = new Thickness(0, 0, 10, 10), Padding = new Thickness(12, 10), CornerRadius = new CornerRadius(14),
                HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Center, Background = TileBg, BorderBrush = EdgeLit, BorderThickness = new Thickness(1),
                Content = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), ColumnSpacing = 10, Children = {
                    Chip(Mdi.ArrowRight, "#F5D27A", "#9C7419", 34),
                    Pos(new StackPanel { VerticalAlignment = VerticalAlignment.Center, Children = {
                        new TextBlock { Text = title, Foreground = Text, FontSize = 13, FontWeight = FontWeight.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis },
                        new TextBlock { Text = i == 1 ? "zuletzt geöffnet" : "weitermachen", Foreground = Muted, FontSize = 11 } } }, 1) } } };
            ToolTip.SetTip(card, $"„{title}“ öffnen");
            card.Click += (_, _) => Show(key);
            Hover(card); row.Children.Add(card);
        }
        var panel = Glass2(new StackPanel { Children = { Head("Weitermachen", "Zuletzt benutzt", null, Mdi.Restart), row } }, 10);
        panel.Name = "widget-weiter";
        panel.IsVisible = keys.Count > 0 && !_settings.HiddenTiles.Contains("widget-weiter");
        return panel;
    }
}
