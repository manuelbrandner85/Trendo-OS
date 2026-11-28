using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace Trendo.App;

/// <summary>V-09 (T-0700): Was den nächsten Schritt verhindert (fehlendes Werkzeug, Modell, Konto), steht direkt an der Hauptaktion – mit „Jetzt einrichten“.</summary>
/// <param name="Missing">Kurzer Satz, was fehlt („Bild-KI fehlt“).</param>
/// <param name="SetupLabel">Knopftext, meist „Jetzt einrichten“.</param>
/// <param name="Setup">Öffnet den Ort, an dem man es einrichtet (nach Zustimmung dort, nie automatisch).</param>
/// <param name="Replaces">Befehl des Programms, der dasselbe tut (z. B. „Bild-KI einrichten“) – er wandert in „Einfach“ unter „Mehr“, solange der Hinweis sichtbar ist.</param>
public sealed record ProgramBlocker(string Missing, string SetupLabel, Action Setup, string? Replaces = null, string? Tip = null); // T-0784 fix3: Tip = längere Erklärung am Knopf

public partial class MainWindow
{
    /// <summary>Gemeinsamer Baustein: Hinweis mit Knopf, der neben bzw. unter der blockierten Hauptaktion steht. Der Knopf ist dann der nächste sinnvolle Schritt (golden).
    /// Namen: <c>{name}</c> (Rahmen), <c>{name}-text</c>, <c>{name}-setup</c>.</summary>
    public Control BlockerChip(string name, ProgramBlocker b)
    {
        var setup = new Button { Name = name + "-setup", Content = b.SetupLabel, FontSize = 12.5, Padding = new Thickness(10, 2), MinHeight = 0, Background = Accent, Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center };
        setup.Click += (_, _) => b.Setup();
        ToolTip.SetTip(setup, (b.Tip ?? b.Missing) + " – hier einrichten (es wird nichts ohne deine Zustimmung installiert)");
        return new Border
        {
            Name = name, Background = Brush.Parse(C("#26F5A524", "#33F5A524")), BorderBrush = Brush.Parse("#80F5A524"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8),
            Padding = new Thickness(8, 1, 2, 1), Margin = new Thickness(4, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center,
            Child = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = {
                new TextBlock { Name = name + "-text", Text = b.Missing, Foreground = Text, FontSize = 12.5, VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap, MaxWidth = 360 },
                setup } },
        };
    }
}
