using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace Trendo.App;

/// <summary>Modernisierung Y4/Y7/Y11/Y14 (07.11.2026): einheitliche Werkzeug-Karte für schlichte Formularseiten – farbiges Symbol, Titel, ein Satz
/// Erklärung, darunter die Bedienelemente. Die Elemente selbst (Namen, Verhalten) bleiben unverändert.</summary>
public partial class MainWindow
{
    internal Border ToolCard(string name, string icon, string c1, string c2, string title, string sub, params Control[] body)
    {
        var content = new StackPanel { Spacing = 8 };
        content.Children.Add(new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), ColumnSpacing = 12, Children = { Chip(icon, c1, c2, 40),
            Pos(new StackPanel { VerticalAlignment = VerticalAlignment.Center, Spacing = 2, Children = {
                new TextBlock { Text = title, Foreground = Text, FontSize = 15, FontWeight = FontWeight.SemiBold },
                new TextBlock { Text = sub, Foreground = Muted, FontSize = 12, TextWrapping = TextWrapping.Wrap } } }, 1) } });
        foreach (var b in body) content.Children.Add(b);
        return new Border { Name = name, Child = content, Padding = new Thickness(16, 14), Margin = new Thickness(0, 0, 0, 10), CornerRadius = new CornerRadius(14),
            Background = TileBg, BorderBrush = EdgeLit, BorderThickness = new Thickness(1), BoxShadow = CardShadow };
    }
}
