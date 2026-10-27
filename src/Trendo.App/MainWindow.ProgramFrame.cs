using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.LogicalTree;

namespace Trendo.App;

/// <summary>Gemeinsamer Programmrahmen für alle Studios (Vorschläge G1–G14, freigegeben 27.10.2026).</summary>
public partial class MainWindow
{
    /// <summary>G3: Ein Reiter gehört zur Leiste, wenn er zum selben Studio (Modul) gehört wie die offene Seite. Fremde Studios erreicht man
    /// über Startmenü, Suche und Dock – nicht über eine Reiter-Wolke.</summary>
    internal bool OwnTab(string key, string? module = null)
    {
        var current = ModuleOf(CurrentView);
        return current is null || string.Equals(module ?? ModuleOf(key), current, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>G3: Eine Reiterleiste mit nur einem Eintrag zeigt nichts Neues – dann bleibt sie weg.</summary>
    static Control TabsOrNothing(Panel row) => row.Children.Count > 1 ? row : new Panel { Name = "tabs-none", IsVisible = false };

    /// <summary>G10: Mindestschrift auf Studio-Seiten – Hinweise 12 pt, Eingaben 13 pt, Knöpfe 12 pt. Der Desktop behält seine Gestaltung.</summary>
    internal static T Readable<T>(T root) where T : Control
    {
        static void Fix(ILogical l)
        {
            switch (l)
            {
                case TextBox tb when tb.FontSize < 13: tb.FontSize = 13; break;
                case Button b when b.FontSize < 12: b.FontSize = 12; break;
                case TextBlock t when t.FontSize < 12: t.FontSize = 12; break;
            }
            foreach (var c in l.LogicalChildren) Fix(c);
        }
        Fix(root);
        return root;
    }

    // ---------- G7: Ergebnis-Karte mit Kopieren und Verlauf ----------
    readonly Dictionary<string, List<string>> _resultHistory = [];

    /// <summary>G7: Das Ergebnis einer Seite groß und gut lesbar, mit „Kopieren“ und den letzten Ergebnissen dieser Seite.</summary>
    private Control ResultCard(string msg)
    {
        var page = CurrentView;
        if (!_resultHistory.TryGetValue(page, out var hist)) _resultHistory[page] = hist = [];
        if (hist.Count == 0 || hist[^1] != msg) { hist.Add(msg); if (hist.Count > 6) hist.RemoveAt(0); }
        var text = new TextBlock { Text = msg, Foreground = Text, FontSize = 15, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap };
        var card = new Border { Name = "w45-msg", Background = TileBg, BorderBrush = Accent, BorderThickness = new Thickness(4, 0, 0, 0), CornerRadius = new CornerRadius(8), Padding = new Thickness(14, 10), Child = text };
        var copy = new Button { Name = "result-copy", Content = "Kopieren", Padding = new Thickness(10, 4) };
        copy.Click += async (_, _) => { if (Clipboard is { } cb) { await cb.SetTextAsync(msg); Toast("Ergebnis kopiert."); } };
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { copy } };
        var older = hist.Take(hist.Count - 1).Reverse().ToList();
        var box = new StackPanel { Name = "result-card", Spacing = 4, Children = { card, row } };
        if (older.Count > 0)
        {
            var list = new StackPanel { Spacing = 2 };
            foreach (var o in older) list.Children.Add(new TextBlock { Text = "• " + o, Foreground = Muted, FontSize = 12, TextWrapping = TextWrapping.Wrap });
            box.Children.Add(new Expander { Name = "result-history", Header = new TextBlock { Text = $"Frühere Ergebnisse ({older.Count})", Foreground = Muted, FontSize = 12 }, Content = list });
        }
        return box;
    }

    // ---------- G12: Benachrichtigungen unten rechts ----------
    private readonly StackPanel _toastHost = new() { Name = "toast-host", Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 0, 18, 90), ZIndex = 4000, Width = 380 };

    private void InitToasts(Grid root) { Grid.SetRowSpan(_toastHost, 2); root.Children.Add(_toastHost); }

    /// <summary>G12: kurze Benachrichtigung unten rechts (6 s), optional mit Aktionen wie „Rückgängig“ oder „Ordner öffnen“.</summary>
    public void Toast(string text, params (string Label, Action Run)[] actions)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, HorizontalAlignment = HorizontalAlignment.Right };
        var t = new Border
        {
            Name = "toast", Background = GlassStrong, BorderBrush = Edge, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12), Padding = new Thickness(14, 10),
            BoxShadow = BoxShadows.Parse("0 8 24 0 #66000000"),
            Child = new StackPanel { Spacing = 6, Children = { new TextBlock { Text = text, Foreground = Text, FontSize = 13, TextWrapping = TextWrapping.Wrap }, row } },
        };
        foreach (var (label, run) in actions)
        {
            var b = new Button { Name = "toast-action", Content = label, Padding = new Thickness(10, 4) };
            b.Click += (_, _) => { run(); _toastHost.Children.Remove(t); };
            row.Children.Add(b);
        }
        var close = new Button { Name = "toast-close", Content = "×", Padding = new Thickness(8, 2), Background = Brushes.Transparent };
        close.Click += (_, _) => _toastHost.Children.Remove(t);
        row.Children.Add(close);
        _toastHost.Children.Add(t);
        while (_toastHost.Children.Count > 3) _toastHost.Children.RemoveAt(0);
        DispatcherTimer.RunOnce(() => _toastHost.Children.Remove(t), TimeSpan.FromSeconds(actions.Length > 0 ? 9 : 6));
    }

    /// <summary>Nennt eine Meldung eine vorhandene Datei oder einen Ordner, bietet die Benachrichtigung „Ordner öffnen“ an.</summary>
    private void NotifyFileResult(string msg)
    {
        var m = Regex.Match(msg, @"[A-Za-z]:\\[^\n„“""<>|*?]+");
        if (!m.Success) return;
        var path = m.Value.TrimEnd(' ', '.', ',', ';', ')', '“');
        string? folder = Directory.Exists(path) ? path : File.Exists(path) ? Path.GetDirectoryName(path) : null;
        if (folder is null) return;
        Toast(Path.GetFileName(path) is { Length: > 0 } n ? $"Fertig: {n}" : "Fertig.", ("Ordner öffnen", new Action(() => Launch(folder))));
    }
}
