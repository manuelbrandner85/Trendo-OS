using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Trendo.Core;
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

    // ---------- G8: Befehlssuche „Was möchtest du tun?“ (Strg+Q) · G9: Tastenkürzel · G13: Fokusmodus ----------
    private readonly Grid _palette = new() { Name = "command-palette", IsVisible = false, ZIndex = 4500, Background = Brush.Parse("#99000000") };
    private readonly TextBox _paletteBox = new() { Name = "command-search", PlaceholderText = "Was möchtest du tun? z. B. „PDF zusammenfügen“, „Handy verbinden“, „Rechnung“", FontSize = 16, Height = 44 };
    private readonly StackPanel _paletteList = new() { Name = "command-results", Spacing = 2 };
    private List<(string Label, string Kind, Action Run)> _paletteHits = [];
    private int _paletteSel;
    WindowState _beforeFocus = WindowState.Normal;
    public bool FocusMode { get; private set; }
    public bool CommandPaletteOpen => _palette.IsVisible;

    private void InitCommandPalette(Grid root)
    {
        Grid.SetRowSpan(_palette, 2); root.Children.Add(_palette);
        var card = new Border
        {
            Width = 720, MaxHeight = 560, Margin = new Thickness(0, 90, 0, 0), VerticalAlignment = VerticalAlignment.Top, HorizontalAlignment = HorizontalAlignment.Center,
            Background = GlassStrong, BorderBrush = Edge, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(16), Padding = new Thickness(14),
            BoxShadow = BoxShadows.Parse("0 24 60 0 #88000000"),
            Child = new DockPanel { Children = { Dock(_paletteBox), new ScrollViewer { Content = _paletteList, Margin = new Thickness(0, 10, 0, 0) } } },
        };
        _palette.Children.Add(card);
        _palette.PointerPressed += (_, e) => { if (e.Source == _palette) ClosePalette(); };
        _paletteBox.TextChanged += (_, _) => FillPalette();
        _paletteBox.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) { ClosePalette(); e.Handled = true; }
            else if (e.Key == Key.Down) { _paletteSel = Math.Min(_paletteSel + 1, _paletteHits.Count - 1); PaintPalette(); e.Handled = true; }
            else if (e.Key == Key.Up) { _paletteSel = Math.Max(_paletteSel - 1, 0); PaintPalette(); e.Handled = true; }
            else if (e.Key == Key.Enter && _paletteHits.Count > 0) { RunPalette(_paletteSel); e.Handled = true; }
        };
        AddHandler(KeyDownEvent, (_, e) =>
        {
            if (SetupOverlayOpen) return;
            var ctrl = e.KeyModifiers.HasFlag(KeyModifiers.Control);
            if (ctrl && e.Key == Key.Q) { OpenPalette(); e.Handled = true; }
            else if (ctrl && e.Key == Key.K) { ShowShortcuts(); e.Handled = true; }
            else if (e.Key == Key.F11) { ToggleFocusMode(); e.Handled = true; }
            else if (e.Key == Key.F1 && CurrentView.Length > 0) { ShowHelp(CurrentView, force: true); e.Handled = true; }
        }, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        static Control Dock(Control c) { DockPanel.SetDock(c, Avalonia.Controls.Dock.Top); return c; }
    }

    public void OpenPalette(string text = "")
    {
        _paletteBox.Text = text; _palette.IsVisible = true; FillPalette();
        Dispatcher.UIThread.Post(() => _paletteBox.Focus());
    }

    public void ClosePalette() => _palette.IsVisible = false;

    /// <summary>Treffer: Knöpfe der offenen Seite (auch versteckte Profi-Befehle), Studios, Seiten; zuletzt „Die KI fragen“.</summary>
    public IReadOnlyList<(string Label, string Kind)> PaletteResults => _paletteHits.Select(h => (h.Label, h.Kind)).ToList();

    void FillPalette()
    {
        var q = (_paletteBox.Text ?? "").Trim();
        var hits = new List<(string, string, Action, int)>();
        static int Score(string label, string q)
        {
            if (q.Length == 0) return 1;
            var l = label.ToLowerInvariant(); var words = q.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (!words.All(l.Contains)) return 0;
            return l.StartsWith(words[0]) ? 3 : 2;
        }
        // 1) Befehle der offenen Seite
        if (ActivePageRoot is { } page)
            foreach (var b in page.GetLogicalDescendants().OfType<Button>().Where(b => b.IsEffectivelyEnabled && b.Name is not ("btn-help" or "result-copy")))
            {
                var label = ButtonText(b);
                if (label.Length < 2 || label.Length > 60) continue;
                var s = Score(label, q); if (s == 0) continue;
                var btn = b;
                hits.Add((label, "Auf dieser Seite", () => btn.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent)), s + 1));
            }
        // 2) Studios und Seiten
        foreach (var (id, info) in StudioCatalog.ById)
        {
            if (PageForStudio(id) is not { } key) continue;
            var s = Score(info.Name + " " + info.Sentence, q); if (s == 0) continue;
            hits.Add((info.Name, "Studio öffnen", () => Show(key), s));
        }
        foreach (var (title, key, words) in ExtraPages)
        {
            if (!_showMap.ContainsKey(key)) continue;
            var s = Math.Max(Score(title, q), words.Any(w => Score(w, q) > 0) && q.Length > 0 ? 2 : 0); if (s == 0) continue;
            hits.Add((title, "Seite öffnen", () => Show(key), s));
        }
        var list = hits.GroupBy(h => h.Item1 + "|" + h.Item2).Select(g => g.First()).OrderByDescending(h => h.Item4).ThenBy(h => h.Item1, StringComparer.CurrentCultureIgnoreCase).Take(q.Length == 0 ? 12 : 30)
            .Select(h => (h.Item1, h.Item2, h.Item3)).ToList();
        if (q.Length > 0) { var ask = q; list.Add(($"„{q}“ – die Trendo KI fragen", "KI", () => { Show("ki-fragen"); _ = AskKi(ask); })); }
        _paletteHits = list; _paletteSel = 0; PaintPalette();
    }

    static string ButtonText(Button b) => b.Content switch
    {
        string s => s,
        TextBlock t => t.Text ?? "",
        Control c => string.Join(" ", c.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text).Where(x => !string.IsNullOrWhiteSpace(x))),
        _ => "",
    };

    void PaintPalette()
    {
        _paletteList.Children.Clear();
        for (var i = 0; i < _paletteHits.Count; i++)
        {
            var (label, kind, _) = _paletteHits[i]; var idx = i;
            var row = new Button
            {
                Name = "command-hit-" + i, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch, Padding = new Thickness(12, 8),
                Background = i == _paletteSel ? Accent : Brushes.Transparent, BorderThickness = new Thickness(0), CornerRadius = new CornerRadius(8),
                Content = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Children = { new TextBlock { Text = label, Foreground = Text, FontSize = 14 }, Pos(new TextBlock { Text = kind, Foreground = Muted, FontSize = 12 }, 1) } },
            };
            row.Click += (_, _) => RunPalette(idx);
            _paletteList.Children.Add(row);
        }
    }

    void RunPalette(int i)
    {
        if (i < 0 || i >= _paletteHits.Count) return;
        var run = _paletteHits[i].Run; ClosePalette();
        _core.Audit.Append(Principal.LocalOwner.Name, "palette.run", _paletteHits[i].Label);
        run();
    }

    /// <summary>G9: Übersicht der Tastenkürzel (Strg+K).</summary>
    public void ShowShortcuts() => Toast("Tastenkürzel: Strg+Q Was möchtest du tun? · F1 Hilfe · F11 Fokusmodus · Strg+K diese Übersicht · Strg+1…5 Start, Studios, Projekte, Freigaben, System · Strg+Tab Fenster wechseln · Win Startmenü · Strg+S Speichern · Strg+Z/Y Rückgängig/Wiederholen · Strg+F Suchen");

    /// <summary>G13: Fokusmodus – Vollbild, ohne Kopfkarte und KI-Zeile, nur der Inhalt. F11 schaltet zurück.</summary>
    public void ToggleFocusMode()
    {
        FocusMode = !FocusMode;
        if (FocusMode) { _beforeFocus = WindowState; WindowState = WindowState.FullScreen; Toast("Fokusmodus an – F11 beendet ihn."); }
        else if (WindowState == WindowState.FullScreen) WindowState = _beforeFocus;
        if (CurrentView.Length > 0) Show(CurrentView);
    }
}
