using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Trendo.App;

/// <summary>T-0258/T-0259: kleine Rechner in den Programmen – rechnen bei jeder Eingabe sofort (kein Formular mit „Ausführen“).
/// Eingaben bleiben für die Sitzung erhalten; Fehler erscheinen als ⚠ direkt unter den Feldern.</summary>
public partial class MainWindow
{
    private readonly Dictionary<string, string> _calcIn = [];

    private void LiveCalc(StackPanel sp, string id, string title, (string Key, string Label, string Def, string[]? Options)[] fields, Func<Func<string, string>, string> run)
    {
        var box = new StackPanel { Spacing = 3 };
        box.Children.Add(new TextBlock { Text = title, Foreground = Text, FontSize = 12.5, FontWeight = FontWeight.SemiBold });
        var output = new SelectableTextBlock { Name = id + "-out", Foreground = Brush.Parse("#7FD3FF"), FontSize = 12, TextWrapping = TextWrapping.Wrap };
        string V(string k) => _calcIn.TryGetValue(id + "-" + k, out var v) ? v : fields.First(f => f.Key == k).Def;
        void Update() { try { output.Text = run(V); } catch (Exception ex) when (ex is ArgumentException or FormatException or InvalidOperationException or KeyNotFoundException or DivideByZeroException or OverflowException) { output.Text = "⚠ " + ex.Message; } }
        foreach (var (key, label, def, options) in fields)
        {
            var k = id + "-" + key; Control input;
            if (options is not null)
            {
                var c = new ComboBox { Name = k, ItemsSource = options, SelectedItem = options.FirstOrDefault(o => o == V(key)) ?? options.FirstOrDefault(o => o.Contains(V(key), StringComparison.OrdinalIgnoreCase)), Width = 150 };
                c.SelectionChanged += (_, _) => { if (c.SelectedItem is string s) { _calcIn[k] = s; Update(); } }; input = c;
            }
            else { var t = new TextBox { Name = k, Text = V(key), Width = 150 }; t.TextChanged += (_, _) => { _calcIn[k] = t.Text ?? ""; Update(); }; input = t; }
            box.Children.Add(Row(P(label, null, 11), input));
        }
        box.Children.Add(output); Update();
        sp.Children.Add(new Border { Name = id, Background = TileBg, CornerRadius = new CornerRadius(10), Padding = new Thickness(8), BorderBrush = Edge, BorderThickness = new Thickness(1), Child = box });
    }
}
