using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Trendo.Core;

namespace Trendo.App;

/// <summary>Welle 6: Fach-Studios – eigene Reiterleiste (max. 2 Ebenen), einheitliche Rechen-Werkzeuge.</summary>
public partial class MainWindow
{
    public static readonly List<(string Key, string Label, string Module)> FachTabs = [];
    private readonly Dictionary<string, string> _fachOut = [];
    private readonly Dictionary<string, string> _fachIn = [];

    private void InitWave6()
    {
        InitSpec01(); InitSpec02();
    }

    private Control FachPage(string title, Control body) => W45Page(title, FachTabs, "fach", body);

    /// <summary>Ein Werkzeug: Eingabefelder, ein Knopf, ein Ergebnis. Fehler erscheinen als verständlicher Satz.</summary>
    private Control FachTool(string id, string title, string hint, (string Key, string Label, string Default)[] fields, Func<Func<string, string>, string> run, string button = "Berechnen")
    {
        var boxes = new Dictionary<string, TextBox>();
        var wrap = new WrapPanel();
        foreach (var (k, l, d) in fields)
        {
            var tb = new TextBox { Name = $"{id}-{k}", Text = _fachIn.TryGetValue($"{id}-{k}", out var v) ? v : d, Width = fields.Length > 3 ? 150 : 220, Watermark = l };
            boxes[k] = tb;
            wrap.Children.Add(MSp(new StackPanel { Children = { new TextBlock { Text = l, Foreground = Muted, FontSize = 11 }, tb } }));
        }
        var outp = new SelectableTextBlock { Name = $"{id}-out", Text = _fachOut.GetValueOrDefault(id, ""), Foreground = Text, TextWrapping = TextWrapping.Wrap };
        var go = new Button { Name = $"{id}-run", Content = button, Background = Accent, VerticalAlignment = VerticalAlignment.Bottom };
        go.Click += (_, _) =>
        {
            foreach (var (k, b) in boxes) _fachIn[$"{id}-{k}"] = b.Text ?? "";
            string res;
            try { res = run(k => boxes.TryGetValue(k, out var b) ? b.Text ?? "" : ""); }
            catch (Exception ex) when (ex is FormatException or ArgumentException or InvalidOperationException or IOException) { res = "⚠ " + ex.Message; }
            _fachOut[id] = res; outp.Text = res;
        };
        wrap.Children.Add(MSp(go));
        return WCard(new StackPanel { Spacing = 4, Children = { H(title, 14), P(hint, null, 11), wrap, outp } });
    }
}
