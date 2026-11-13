using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Trendo.Core;

namespace Trendo.App;

/// <summary>Welle 1: Apps-Bereich mit einer Unterebene – Programme (SYS-11), Werkzeuge & Medien (SYS-02), Ressourcen (X-09).</summary>
public partial class MainWindow
{
    public static readonly (string Key, string Label)[] AppsTabs =
        [("apps", "Programme"), ("werkzeuge", "Werkzeuge"), ("medien", "Medien"), ("office", "Dokumente"), ("downloads", "Downloads"), ("ressourcen", "Speicher & Leistung")];

    private Control AppsNav()
    {
        var row = new WrapPanel();
        foreach (var (k, l) in AppsTabs.Where(t => _showMap.ContainsKey(t.Key) && OwnTab(t.Key)))
        {
            var b = new Button { Name = "apps-tab-" + k, Content = l, Margin = new Thickness(0, 0, 6, 6), Padding = new Thickness(10, 6), CornerRadius = new CornerRadius(10), Foreground = Text, Background = CurrentView == k ? Accent : Brushes.Transparent, BorderBrush = Edge, BorderThickness = new Thickness(CurrentView == k ? 0 : 1) };
            b.Click += (_, _) => Show(k); row.Children.Add(b);
        }
        return TabsOrNothing(row);
    }

    private Control AppsPage(string title, Control body) => Page(title, new StackPanel { Spacing = 8, Children = { AppsNav(), body } });

    private void Launch(string target) => _core.Integrations.LaunchDirect(Principal.LocalOwner, target, _core.Audit);

    private Control FileRow(FileInfo f, int i, string prefix, string? player = null)
    {
        var open = new Button { Name = $"{prefix}-open-{i}", Content = "Öffnen", FontSize = 11, Padding = new Thickness(10, 2) };
        open.Click += (_, _) => { if (player is not null) _core.Integrations.LaunchDirect(Principal.LocalOwner, player, _core.Audit, f.FullName); else Launch(f.FullName); };
        var folder = new Button { Name = $"{prefix}-folder-{i}", Content = "Ordner", FontSize = 11, Padding = new Thickness(10, 2) };
        folder.Click += (_, _) => Launch(f.DirectoryName ?? "");
        return new Border { Background = TileBg, CornerRadius = new CornerRadius(8), Padding = new Thickness(10, 5), Child = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Children = {
            new StackPanel { Children = { new TextBlock { Text = f.Name, Foreground = Text, FontSize = 12.5, TextTrimming = TextTrimming.CharacterEllipsis },
                P($"{FileShelf.Kind(f.Name)} · {ResourceMonitor.Size(f.Length)} · {f.LastWriteTime:dd.MM.yyyy HH:mm}", null, 10.5) } },
            Pos(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center, Children = { open, folder } }, 1) } } };
    }

    private string? ToolPath(string id) => DependencyCatalog.All.FirstOrDefault(c => c.Id == id) is { } c ? _deps.Integrated(id) ?? _deps.ResolvePath(c) : null;

    // ---------- SYS-11: Programme – was da ist, startet mit einem Klick; was fehlt, ist einen Klick entfernt ----------
    private Control AppsView()
    {
        var sp = new StackPanel { Spacing = 5, Children = { P("Gefundene Programme starten direkt. Fehlt etwas, richtet Trendo es aus dem Vollpaket ein oder installiert es nach deiner Zustimmung.") } };
        var i = 0;
        foreach (var c in DependencyCatalog.All.Where(c => c.Mode is DeliveryMode.AutoUser or DeliveryMode.AutoElevated or DeliveryMode.Guided or DeliveryMode.Included))
        {
            var path = ToolPath(c.Id);
            var exe = path is not null && (path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) || File.Exists(path) && !Directory.Exists(path)) ? path : null;
            var have = _deps.IsInstalled(c);
            var b = new Button { Name = "app-" + c.Id, FontSize = 11, Padding = new Thickness(10, 2), Content = exe is not null ? "Starten" : have ? "Vorhanden" : "Installieren …", IsEnabled = exe is not null || !have };
            b.Click += (_, _) => { if (exe is not null) Launch(exe); else Show("components"); };
            sp.Children.Add(new Border { Background = TileBg, CornerRadius = new CornerRadius(8), Padding = new Thickness(10, 5), Child = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Children = {
                new StackPanel { Children = { new TextBlock { Text = c.Name, Foreground = Text, FontSize = 12.5 }, P((have ? "vorhanden · " : "fehlt · ") + c.Purpose, have ? null : Brush.Parse("#FACC15"), 10.5) } },
                Pos(b, 1) } } });
            i++;
        }
        sp.Children.Add(Nav2("Zusatzprogramme verwalten", "components"));
        return AppsPage("Apps", sp);
    }

    // ---------- SYS-02: Werkzeuge ----------
    private DispatcherTimer? _timer; private TimeSpan _timerLeft;
    private readonly TextBlock _timerText = new() { Name = "timer-text", FontSize = 26, FontWeight = FontWeight.SemiBold };

    readonly List<string> _calcHist = [];

    /// <summary>Kleine Helfer als echte Mini-Programme (Clean-Room nach Windows-Kurznotizen, -Rechner und -Uhr): Haftnotizen als Karten,
    /// Rechner mit Tastenfeld und Verlauf, Kurzzeitwecker mit großer Anzeige.</summary>
    private Control ToolsView()
    {
        var notes = new NoteStore(_core.DataRoot);
        Border Panel(string title, string icon, Control body) => new() { Background = TileBg, BorderBrush = EdgeLit, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(16), BoxShadow = CardShadow, Padding = new Thickness(14),
            Child = new DockPanel { Children = { Dk(new TextBlock { Text = icon + "  " + title, Foreground = Text, FontSize = 16, FontWeight = FontWeight.SemiBold, Margin = new Thickness(0, 0, 0, 10) }, Avalonia.Controls.Dock.Top), body } } };

        // Haftnotizen
        var input = new TextBox { Name = "note-input", PlaceholderText = "Neue Notiz …", AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 54 };
        var add = new Button { Name = "note-add", Content = "＋ Notiz", Background = Accent };
        add.Click += (_, _) => { if (!string.IsNullOrWhiteSpace(input.Text)) { notes.Add(input.Text); _core.Audit.Append(Principal.LocalOwner.Name, "tool.note", "neu"); Show("werkzeuge"); } else Toast("Bitte zuerst etwas schreiben."); };
        var nb = new Button { Name = "note-notebook", Content = "Zum Notizbuch →" }; nb.Click += (_, _) => Show("notizbuch");
        var board = new WrapPanel { Name = "note-board" };
        string[] colors = ["#3DFACC15", "#3D60A5FA", "#3DF472B6", "#3D4ADE80", "#3DC084FC"];
        var n = 0;
        foreach (var note in notes.All().Take(30))
        {
            var del = new Button { Name = "note-del-" + n, Content = "✕", FontSize = 11, Padding = new Thickness(6, 0), Background = Brushes.Transparent, BorderThickness = new Thickness(0), HorizontalAlignment = HorizontalAlignment.Right };
            ToolTip.SetTip(del, "Notiz löschen");
            var id = note.Id; del.Click += (_, _) => { notes.Delete(id); Show("werkzeuge"); };
            board.Children.Add(new Border { Width = 190, MinHeight = 120, Margin = new Thickness(0, 0, 8, 8), CornerRadius = new CornerRadius(10), Padding = new Thickness(10, 6, 10, 8),
                Background = Brush.Parse(colors[n % colors.Length]), BorderBrush = Edge, BorderThickness = new Thickness(1),
                Child = new DockPanel { Children = { Dk(new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Children = { new TextBlock { Text = note.Changed.ToString("dd.MM. HH:mm", De), Foreground = Muted, FontSize = 10.5, VerticalAlignment = VerticalAlignment.Center }, Pos(del, 1) } }, Avalonia.Controls.Dock.Top),
                    new TextBlock { Text = note.Text, Foreground = Text, FontSize = 13, TextWrapping = TextWrapping.Wrap, MaxHeight = 160, TextTrimming = TextTrimming.CharacterEllipsis } } } });
            n++;
        }
        if (n == 0) board.Children.Add(P("Noch keine Notizen – oben schreiben, „＋ Notiz“.", Muted, 12));
        var notesPanel = Panel("Haftnotizen", "🗒", new DockPanel { Children = { Dk(new StackPanel { Spacing = 6, Margin = new Thickness(0, 0, 0, 10), Children = { input, new WrapPanel { Children = { add, new Border { Width = 6 }, nb } } } }, Avalonia.Controls.Dock.Top),
            new ScrollViewer { Content = board } } });

        // Rechner mit Tastenfeld
        var calc = new TextBox { Name = "calc-input", PlaceholderText = "Rechnung, z. B. 19 % × 250", FontSize = 18, HorizontalContentAlignment = HorizontalAlignment.Right };
        var result = new TextBlock { Name = "calc-result", Foreground = Text, FontSize = 30, FontWeight = FontWeight.SemiBold, HorizontalAlignment = HorizontalAlignment.Right, Text = _calcHist.Count > 0 ? _calcHist[0].Split('=').Last().Trim() is var last ? "= " + last : "" : "" };
        var hist = new StackPanel { Name = "calc-history", Spacing = 2 };
        void History() { hist.Children.Clear(); foreach (var h in _calcHist.Take(6)) hist.Children.Add(new TextBlock { Text = h, Foreground = Muted, FontSize = 11.5, HorizontalAlignment = HorizontalAlignment.Right }); }
        void Run()
        {
            try { var v = Calc.Format(Calc.Eval(calc.Text ?? "")); result.Text = "= " + v; _calcHist.Insert(0, $"{calc.Text} = {v}"); if (_calcHist.Count > 20) _calcHist.RemoveAt(20); History(); }
            catch (Exception ex) { result.Text = ex is DivideByZeroException ? ex.Message : "Das kann ich nicht ausrechnen."; }
        }
        History();
        calc.KeyDown += (_, e) => { if (e.Key == Avalonia.Input.Key.Enter) Run(); };
        var keys = new Grid { Name = "calc-keys", ColumnDefinitions = new ColumnDefinitions("*,*,*,*"), RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto,Auto") };
        string[][] layout = [["C", "⌫", "%", "÷"], ["7", "8", "9", "×"], ["4", "5", "6", "−"], ["1", "2", "3", "+"], ["(", "0", ",", "="]];
        for (var r = 0; r < layout.Length; r++)
            for (var c = 0; c < 4; c++)
            {
                var k = layout[r][c];
                var op = k is "÷" or "×" or "−" or "+" or "=" or "%";
                var b = new Button { Name = k == "=" ? "calc-run" : "calc-k-" + (k switch { "÷" => "div", "×" => "mul", "−" => "minus", "+" => "plus", "%" => "pct", "⌫" => "back", "(" => "paren", "," => "comma", _ => k }), Content = k, FontSize = 17,
                    HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Center, Height = 46, Margin = new Thickness(3), CornerRadius = new CornerRadius(10),
                    Background = k == "=" ? Accent : op ? Brush.Parse("#26D4A84B") : Brush.Parse("#14FFFFFF"), Foreground = k == "=" ? Brushes.White : Text };
                var kk = k;
                b.Click += (_, _) =>
                {
                    var t = calc.Text ?? "";
                    switch (kk)
                    {
                        case "=": Run(); return;
                        case "C": calc.Text = ""; result.Text = ""; return;
                        case "⌫": calc.Text = t.Length > 0 ? t[..^1] : ""; return;
                        case "(": calc.Text = t + (t.Count(ch => ch == '(') > t.Count(ch => ch == ')') ? ")" : "("); return;
                        default: calc.Text = t + (kk is "÷" or "×" or "−" or "+" ? $" {kk} " : kk); return;
                    }
                };
                Grid.SetRow(b, r); Grid.SetColumn(b, c); keys.Children.Add(b);
            }
        var calcPanel = Panel("Rechner", "🧮", new StackPanel { Spacing = 6, Children = { hist, calc, result, keys } });

        // Kurzzeitwecker mit großer Anzeige
        _timerText.Foreground = Text; _timerText.FontSize = 44; _timerText.HorizontalAlignment = HorizontalAlignment.Center; _timerText.VerticalAlignment = VerticalAlignment.Center;
        _timerText.Text = _timer?.IsEnabled == true ? _timerLeft.ToString(@"mm\:ss") : "00:00";
        Detach(_timerText);
        var dial = new Border { Width = 190, Height = 190, CornerRadius = new CornerRadius(95), BorderBrush = Accent, BorderThickness = new Thickness(6), HorizontalAlignment = HorizontalAlignment.Center,
            Background = Brush.Parse("#100F1520"), Child = _timerText };
        var presets = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Center };
        foreach (var min in new[] { 1, 5, 10, 25 })
        {
            var b = new Button { Name = $"timer-{min}", Content = $"{min} Min.", Margin = new Thickness(0, 0, 6, 6), CornerRadius = new CornerRadius(12) };
            b.Click += (_, _) => StartTimer(TimeSpan.FromMinutes(min));
            presets.Children.Add(b);
        }
        var stop = new Button { Name = "timer-stop", Content = "Stopp", Margin = new Thickness(0, 0, 6, 6), CornerRadius = new CornerRadius(12) }; stop.Click += (_, _) => { _timer?.Stop(); _timerText.Text = "00:00"; };
        presets.Children.Add(stop);
        var timerPanel = Panel("Kurzzeitwecker", "⏱", new StackPanel { Spacing = 14, Children = { dial, presets, P("Läuft weiter, auch wenn du die Seite wechselst – am Ende meldet sich Trendo.", Muted, 11.5) } });

        var desk = new Grid { Name = "tools-desk", ColumnDefinitions = new ColumnDefinitions("*,14,340,14,300"), Children = { notesPanel, Pos(calcPanel, 2), Pos(timerPanel, 4) } };
        return AppsPage("Werkzeuge", desk);
    }

    public void StartTimer(TimeSpan t)
    {
        _timer?.Stop(); _timerLeft = t; _timerText.Text = t.ToString(@"mm\:ss");
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) =>
        {
            _timerLeft -= TimeSpan.FromSeconds(1);
            _timerText.Text = _timerLeft.ToString(@"mm\:ss");
            if (_timerLeft > TimeSpan.Zero) return;
            _timer!.Stop(); _timerText.Text = "Zeit ist um!";
            _core.Audit.Append(Principal.LocalOwner.Name, "tool.timer", $"{t.TotalMinutes:0} Min. abgelaufen");
            if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
            Activate();
        };
        _timer.Start();
    }

    // ---------- SYS-02: Medien ----------
    private Control MediaView()
    {
        if (_mediaTab == "fotos") return AppsPage("Medien", new StackPanel { Spacing = 8, Children = { MediaTabs(), PhotoView() } });
        return AppsPage("Medien", new StackPanel { Spacing = 8, Children = { MediaTabs(), MusicView() } });
    }

    private string ProjectsRoot => string.IsNullOrEmpty(_settings.ProjectsFolder) ? UserSettings.DefaultProjectsFolder() : _settings.ProjectsFolder;

    // ---------- X-09: Speicher & Leistung ----------
    // ResourcesView: siehe MainWindow.TaskManager.cs (Task-Manager, Runde 7)
}
