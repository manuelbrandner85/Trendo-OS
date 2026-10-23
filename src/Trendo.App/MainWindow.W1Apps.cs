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
        foreach (var (k, l) in AppsTabs.Where(t => _showMap.ContainsKey(t.Key)))
        {
            var b = new Button { Name = "apps-tab-" + k, Content = l, Margin = new Thickness(0, 0, 6, 6), Padding = new Thickness(10, 6), CornerRadius = new CornerRadius(10), Foreground = Text, Background = CurrentView == k ? Accent : Brushes.Transparent, BorderBrush = Edge, BorderThickness = new Thickness(CurrentView == k ? 0 : 1) };
            b.Click += (_, _) => Show(k); row.Children.Add(b);
        }
        return row;
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
        var sp = new StackPanel { Spacing = 5, Children = { P("Gefundene Programme starten direkt. Fehlt etwas, installiert Trendo es nach deiner Zustimmung.") } };
        var i = 0;
        foreach (var c in DependencyCatalog.All.Where(c => c.Mode is DeliveryMode.AutoUser or DeliveryMode.AutoElevated or DeliveryMode.Guided))
        {
            var path = ToolPath(c.Id);
            var exe = path is not null && (path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) || File.Exists(path) && !Directory.Exists(path)) ? path : null;
            var have = _deps.IsInstalled(c);
            var b = new Button { Name = "app-" + c.Id, FontSize = 11, Padding = new Thickness(10, 2), Content = exe is not null ? "Starten" : have ? "Vorhanden" : "Installieren …", IsEnabled = exe is not null || !have };
            b.Click += (_, _) => { if (exe is not null) Launch(exe); else Show("components"); };
            sp.Children.Add(new Border { Background = TileBg, CornerRadius = new CornerRadius(8), Padding = new Thickness(10, 5), Child = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Children = {
                new StackPanel { Children = { new TextBlock { Text = c.Name, Foreground = Text, FontSize = 12.5 }, P((have ? "✓ vorhanden · " : "fehlt · ") + c.Purpose, have ? null : Brush.Parse("#FACC15"), 10.5) } },
                Pos(b, 1) } } });
            i++;
        }
        sp.Children.Add(Nav2("Zusatzprogramme verwalten", "components"));
        return AppsPage("Apps", sp);
    }

    // ---------- SYS-02: Werkzeuge ----------
    private DispatcherTimer? _timer; private TimeSpan _timerLeft;
    private string _noteMsg = "";
    private readonly TextBlock _timerText = new() { Name = "timer-text", FontSize = 26, FontWeight = FontWeight.SemiBold };

    private Control ToolsView()
    {
        var notes = new NoteStore(_core.DataRoot);
        var sp = new StackPanel { Spacing = 10 };
        // Notizen
        var input = new TextBox { Name = "note-input", PlaceholderText = "Neue Notiz …", AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 54 };
        var add = new Button { Name = "note-add", Content = "Notiz speichern", Background = Accent };
        add.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(input.Text)) { _noteMsg = "Bitte zuerst eine Notiz schreiben."; Show("werkzeuge"); return; }
            notes.Add(input.Text); _noteMsg = "Notiz gespeichert."; _core.Audit.Append(Principal.LocalOwner.Name, "tool.note", "neu"); Show("werkzeuge");
        };
        var list = new StackPanel { Spacing = 4 };
        var n = 0;
        foreach (var note in notes.All().Take(20))
        {
            var del = new Button { Name = "note-del-" + n++, Content = "Löschen", FontSize = 11, Padding = new Thickness(8, 2) };
            del.Click += (_, _) => { notes.Delete(note.Id); Show("werkzeuge"); };
            list.Children.Add(new Border { Background = TileBg, CornerRadius = new CornerRadius(8), Padding = new Thickness(10, 5), Child = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"),
                Children = { new StackPanel { Children = { P(note.Text, Text, 12.5), P(note.Changed.ToString("dd.MM.yyyy HH:mm", De), null, 10) } }, Pos(del, 1) } } });
        }
        if (n == 0) list.Children.Add(P("Noch keine Notizen."));
        if (_noteMsg.Length > 0) list.Children.Add(new TextBlock { Name = "note-msg", Text = _noteMsg, Foreground = Text });
        sp.Children.Add(new StackPanel { Spacing = 6, Children = { H("Notizen", 15), input, add, list } });

        // Taschenrechner
        var calc = new TextBox { Name = "calc-input", PlaceholderText = "z. B. (12,5 + 7) × 3 oder 19 % × 250", Width = 320 };
        var result = new TextBlock { Name = "calc-result", Foreground = Text, FontSize = 18, FontWeight = FontWeight.SemiBold };
        void Run() { try { result.Text = "= " + Calc.Format(Calc.Eval(calc.Text ?? "")); } catch (Exception ex) { result.Text = ex is DivideByZeroException ? ex.Message : "Das kann ich nicht ausrechnen – bitte Eingabe prüfen."; } }
        calc.KeyDown += (_, e) => { if (e.Key == Avalonia.Input.Key.Enter) Run(); };
        var eq = new Button { Name = "calc-run", Content = "=", Width = 40, HorizontalContentAlignment = HorizontalAlignment.Center }; eq.Click += (_, _) => Run();
        sp.Children.Add(new StackPanel { Spacing = 6, Children = { H("Rechner", 15), new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { calc, eq } }, result } });

        // Kurzzeitwecker
        _timerText.Foreground = Text; _timerText.Text = _timer?.IsEnabled == true ? _timerLeft.ToString(@"mm\:ss") : "00:00";
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        foreach (var min in new[] { 1, 5, 10, 25 })
        {
            var b = new Button { Name = $"timer-{min}", Content = $"{min} Min." };
            b.Click += (_, _) => StartTimer(TimeSpan.FromMinutes(min));
            buttons.Children.Add(b);
        }
        var stop = new Button { Name = "timer-stop", Content = "Stopp" }; stop.Click += (_, _) => { _timer?.Stop(); _timerText.Text = "00:00"; };
        buttons.Children.Add(stop);
        Detach(_timerText);
        sp.Children.Add(new StackPanel { Spacing = 6, Children = { H("Kurzzeitwecker", 15), _timerText, buttons } });
        return AppsPage("Werkzeuge", sp);
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
        var music = Environment.GetFolderPath(Environment.SpecialFolder.MyMusic); var videos = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);
        var files = FileShelf.Recent([music, videos, ProjectsRoot], FileShelf.Media, 40);
        var mpv = ToolPath("mpv");
        var sp = new StackPanel { Spacing = 4, Children = { P(mpv is not null ? "Musik und Videos aus deinen Ordnern Musik, Videos und dem Projektordner. Abspielen mit mpv." : "Musik und Videos aus deinen Ordnern. Abspielen mit dem Windows-Standardprogramm (mpv ist nicht installiert).") } };
        var i = 0;
        foreach (var f in files) sp.Children.Add(FileRow(f, i++, "media", mpv));
        if (files.Count == 0) sp.Children.Add(P("Keine Musik- oder Videodateien gefunden.", Text));
        return AppsPage("Medien", sp);
    }

    private string ProjectsRoot => string.IsNullOrEmpty(_settings.ProjectsFolder) ? UserSettings.DefaultProjectsFolder() : _settings.ProjectsFolder;

    // ---------- X-09: Speicher & Leistung ----------
    private Control ResourcesView()
    {
        var sp = new StackPanel { Spacing = 8 };
        var m = _metrics.Sample();
        Border Meter(string name, string label, string value)
        {
            var n = new TextBlock { Name = name, Text = value, Foreground = Text, FontSize = 28, FontWeight = FontWeight.SemiBold };
            return new Border { Background = TileBg, BorderBrush = Edge, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(14), Padding = new Thickness(14, 10), Margin = new Thickness(0, 0, 10, 10), MinWidth = 180, Child = new StackPanel { Spacing = 2, Children = { P(label, null, 12), n } } };
        }
        var meters = new WrapPanel();
        meters.Children.Add(Meter("res-cpu", "Prozessor", m.CpuPercent is { } cpu ? $"{cpu:0} %" : "n. v."));
        meters.Children.Add(Meter("res-ram", "Arbeitsspeicher", m.RamPercent is { } ram ? $"{ram:0} %" : "n. v."));
        meters.Children.Add(Meter("res-gpu", "Grafik", m.GpuPercent is { } gpu ? $"{gpu:0} %" : "n. v."));
        sp.Children.Add(meters);
        sp.Children.Add(P($"Prozessor {m.CpuPercent?.ToString("0") ?? "n. v."} % · Arbeitsspeicher {m.RamPercent?.ToString("0") ?? "n. v."} % belegt von {TotalRam / (1024 * 1024 * 1024.0):0} GB · Grafik {m.GpuPercent?.ToString("0") ?? "n. v."} %", Text, 13));
        var ki = new StackPanel { Spacing = 4, Children = { P("KI-Modelle im Speicher: wird geprüft …") } };
        sp.Children.Add(new StackPanel { Spacing = 4, Children = { H("Trendo KI", 15), ki } });
        _ = FillKi();
        async Task FillKi()
        {
            var loaded = await _ollama.LoadedAsync();
            ki.Children.Clear();
            if (loaded.Count == 0) { ki.Children.Add(P("Kein KI-Modell im Speicher – belegt nichts.")); return; }
            foreach (var (name, bytes) in loaded)
            {
                var b = new Button { Name = "ki-unload", Content = "Aus dem Speicher nehmen", FontSize = 11 };
                b.Click += async (_, _) => { var ok = await _ollama.UnloadAsync(name); _core.Audit.Append(Principal.LocalOwner.Name, "ki.unload", $"{name} ok={ok}"); Show("ressourcen"); };
                ki.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { P($"{name}: {ResourceMonitor.Size(bytes)}", Text, 12.5), b } });
            }
            ki.Children.Add(P("Das Modell bleibt installiert und lädt bei der nächsten Frage automatisch neu.", null, 10.5));
        }
        sp.Children.Add(H("Größte Programme gerade", 15));
        foreach (var p in ResourceMonitor.Snapshot())
            sp.Children.Add(P($"{p.Name}{(p.Count > 1 ? $" ({p.Count}×)" : "")}: {ResourceMonitor.Size(p.Bytes)}{(p.Studio is { } s ? $" · {s}" : "")}", p.Studio is null ? null : Text, 12));
        sp.Children.Add(P("Trendo zeigt nur an und beendet keine fremden Programme. Schließe ein Programm selbst, wenn es zu viel belegt.", null, 10.5));
        var refresh = new Button { Name = "res-refresh", Content = "Aktualisieren" }; refresh.Click += (_, _) => Show("ressourcen");
        sp.Children.Add(refresh);
        return AppsPage("Speicher & Leistung", sp);
    }
}
