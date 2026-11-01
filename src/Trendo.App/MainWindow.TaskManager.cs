using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Trendo.Core;
using Trendo.Core.Arch;
using Trendo.Core.Sys;

namespace Trendo.App;

/// <summary>Leistung als Task-Manager (Masterprompt 131, Runde 7): Reiter Leistung (Kurven der letzten 60 s), Prozesse (sortierbar, Suche,
/// Beenden nur für Trendos eigene Programme mit Rückfrage) und Speicher (bisherige Ansicht). Aktualisiert sich jede Sekunde an Ort und Stelle.</summary>
public partial class MainWindow
{
    readonly History _hCpu = new(), _hRam = new(), _hGpu = new();
    IReadOnlyList<TaskManager.Sample> _tmLast = []; DateTime _tmAt = DateTime.MinValue; IReadOnlyList<TaskManager.Proc> _tmRows = [];
    string _tmTab = "leistung", _tmSort = "cpu", _tmSearch = ""; bool _tmDesc = true; int? _tmConfirm; DispatcherTimer? _tmTimer;
    Action? _tmRefresh;
    /// <summary>Für Tests: Messung und Beenden nachbilden.</summary>
    public Func<IReadOnlyList<TaskManager.Sample>>? TaskMeasureOverride { get; set; }
    public Action<int>? TaskKillOverride { get; set; }
    public IReadOnlyList<TaskManager.Proc> TaskRows => _tmRows;

    IReadOnlyList<string> OwnRoots => [_deps.ToolsRoot, _core.DataRoot];

    /// <summary>Eine Messung: Gesamtwerte in die Verläufe, Prozesse mit Last seit der letzten Messung.</summary>
    public void TaskTick()
    {
        MetricsSnapshot m; lock (_metrics) m = _metrics.Sample();
        _hCpu.Add(m.CpuPercent); _hRam.Add(m.RamPercent); _hGpu.Add(m.GpuPercent);
        var now = TaskMeasureOverride?.Invoke() ?? TaskManager.Measure(); var t = DateTime.UtcNow;
        _tmRows = TaskManager.Rows(_tmLast, now, _tmAt == DateTime.MinValue ? 1 : (t - _tmAt).TotalSeconds, Environment.ProcessorCount, OwnRoots);
        _tmLast = now; _tmAt = t;
    }

    void TaskTimer()
    {
        if (_tmTimer is not null) return;
        _tmTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _tmTimer.Tick += (_, _) =>
        {
            if (CurrentView != "ressourcen") { _tmTimer?.Stop(); _tmTimer = null; return; }
            TaskTick(); _tmRefresh?.Invoke();
        };
        _tmTimer.Start();
    }

    /// <summary>Programm beenden – nur eigene Trendo-Programme (nie Trendo selbst), zweistufig bestätigt.</summary>
    public bool TaskKill(int pid)
    {
        var row = _tmRows.FirstOrDefault(r => r.Pid == pid);
        if (row is null || !row.Own || pid == Environment.ProcessId) { _tmMsg = "Trendo beendet nur Programme, die Trendo selbst eingerichtet hat."; return false; }
        try
        {
            if (TaskKillOverride is { } o) o(pid);
            else using (var p = Process.GetProcessById(pid)) p.Kill(entireProcessTree: true);
            _core.Audit.Append(Principal.LocalOwner.Name, "taskmanager.beendet", $"{row.Name} pid={pid}");
            _tmMsg = $"„{row.Name}“ wurde beendet."; return true;
        }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception) { _tmMsg = $"„{row.Name}“ ließ sich nicht beenden: {e.Message}"; return false; }
    }
    string _tmMsg = "";

    private Control ResourcesView()
    {
        if (_tmAt == DateTime.MinValue) { TaskTick(); }
        TaskTimer();
        var sp = new StackPanel { Spacing = 8 };
        var tabs = new WrapPanel { Name = "res-tabs" };
        foreach (var (id, label) in new[] { ("leistung", "Leistung"), ("prozesse", "Prozesse"), ("speicher", "Speicher") })
        {
            var tid = id; var b = Act("res-tab-" + id, label, () => { _tmTab = tid; _tmConfirm = null; Show("ressourcen"); }, _tmTab == id); b.Margin = new Thickness(0, 0, 6, 6); tabs.Children.Add(b);
        }
        sp.Children.Add(tabs);
        if (_tmMsg.Length > 0) sp.Children.Add(new TextBlock { Name = "tm-msg", Text = _tmMsg, Foreground = Text, TextWrapping = TextWrapping.Wrap });

        if (_tmTab == "leistung")
        {
            var meters = new WrapPanel();
            var labels = new List<(TextBlock Value, Border Chart, History H)>();
            foreach (var (name, label, h, color) in new[] { ("res-cpu", "Prozessor", _hCpu, "#2F86F6"), ("res-ram", "Arbeitsspeicher", _hRam, "#A855F7"), ("res-gpu", "Grafik", _hGpu, "#22C55E") })
            {
                var value = new TextBlock { Name = name, Text = $"{h.Last:0} %", Foreground = Text, FontSize = 26, FontWeight = FontWeight.SemiBold };
                var hh = h; var col = color;
                var chart = new Border { Name = name.Replace("res-", "res-chart-"), Height = 90, Width = 300, CornerRadius = new CornerRadius(8), ClipToBounds = true, Child = Chart(hh, col) };
                labels.Add((value, chart, hh));
                meters.Children.Add(new Border { Background = TileBg, BorderBrush = Edge, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(14), Padding = new Thickness(14, 10), Margin = new Thickness(0, 0, 10, 10),
                    Child = new StackPanel { Spacing = 4, Children = { P(label, Muted, 12), value, chart, P($"Ø {hh.Average:0} % · Spitze {hh.Max:0} % (letzte {hh.Size} s)", Muted, 10.5) } } });
            }
            sp.Children.Add(meters);
            sp.Children.Add(P($"Arbeitsspeicher insgesamt {TotalRam / (1024 * 1024 * 1024.0):0} GB · {Environment.ProcessorCount} Prozessorkerne · {_tmRows.Count} Prozesse", Muted, 11.5));
            _tmRefresh = () => { foreach (var (v, c, h) in labels) { v.Text = $"{h.Last:0} %"; c.Child = Chart(h, c.Name!.EndsWith("cpu") ? "#2F86F6" : c.Name.EndsWith("ram") ? "#A855F7" : "#22C55E"); } };
        }
        else if (_tmTab == "prozesse")
        {
            var search = new TextBox { Name = "tm-search", Text = _tmSearch, PlaceholderText = "Programm oder Kennung suchen", Width = 280 };
            search.KeyUp += (_, _) => { _tmSearch = search.Text ?? ""; _tmRefresh?.Invoke(); };
            var head = new Grid { ColumnDefinitions = new ColumnDefinitions("*,90,110,80,120") };
            var c = 0;
            foreach (var (id, label) in new[] { ("name", "Name"), ("cpu", "Prozessor"), ("ram", "Speicher"), ("pid", "Kennung") })
            {
                var sid = id; var b = Act("tm-sort-" + id, label + (_tmSort == id ? (_tmDesc ? " ▼" : " ▲") : ""), () => { if (_tmSort == sid) _tmDesc = !_tmDesc; else { _tmSort = sid; _tmDesc = sid is "cpu" or "ram"; } Show("ressourcen"); });
                b.FontSize = 11.5; b.Background = Brushes.Transparent; head.Children.Add(Pos(b, c++));
            }
            var rows = new StackPanel { Name = "tm-rows", Spacing = 1 };
            void Fill()
            {
                rows.Children.Clear(); var i = 0;
                foreach (var r in TaskManager.Sort(_tmRows, _tmSort, _tmDesc, _tmSearch).Take(150))
                {
                    var pid = r.Pid;
                    Control action = !r.Own || pid == Environment.ProcessId ? new Panel()
                        : _tmConfirm == pid ? Act($"tm-kill-{pid}", "Wirklich beenden?", () => { _tmConfirm = null; if (TaskKill(pid)) TaskTick(); Show("ressourcen"); }, true)
                        : Act($"tm-kill-{pid}", "Beenden", () => { _tmConfirm = pid; Fill(); });
                    var g = new Grid { Name = $"tm-row-{i++}", ColumnDefinitions = new ColumnDefinitions("*,90,110,80,120"), Background = r.Own ? Brush.Parse("#142F86F6") : Brushes.Transparent, Children = {
                        new TextBlock { Text = r.Name + (r.Own ? "  · Trendo" : ""), Foreground = Text, FontSize = 12, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 3) },
                        Pos(new TextBlock { Text = $"{r.CpuPercent:0.0} %", Foreground = r.CpuPercent >= 50 ? Brush.Parse("#F97316") : Text, FontSize = 12, VerticalAlignment = VerticalAlignment.Center }, 1),
                        Pos(new TextBlock { Text = ResourceMonitor.Size(r.Bytes), Foreground = Text, FontSize = 12, VerticalAlignment = VerticalAlignment.Center }, 2),
                        Pos(new TextBlock { Text = r.Pid.ToString(), Foreground = Muted, FontSize = 11.5, VerticalAlignment = VerticalAlignment.Center }, 3),
                        Pos(action, 4) } };
                    rows.Children.Add(g);
                }
                if (rows.Children.Count == 0) rows.Children.Add(P("Nichts gefunden.", Muted, 12));
            }
            Fill();
            _tmRefresh = () => { if (_tmConfirm is null) Fill(); };
            sp.Children.Add(search); sp.Children.Add(head); sp.Children.Add(Card(rows));
            sp.Children.Add(P("Trendo zeigt alle Programme an, beendet aber nur die, die Trendo selbst eingerichtet hat (blau markiert). Andere Programme schließt du selbst.", Muted, 11));
        }
        else
        {
            _tmRefresh = null;
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
            sp.Children.Add(Card(DiskMapCard()));
        }
        var refresh = new Button { Name = "res-refresh", Content = "Aktualisieren" }; refresh.Click += (_, _) => { TaskTick(); Show("ressourcen"); };
        sp.Children.Add(refresh);
        return AppsPage("Speicher & Leistung", sp);
    }

    /// <summary>Flächenkurve eines Verlaufs (0–100 %).</summary>
    static ShapeView Chart(History h, string color) => new((w, hgt) =>
    {
        var v = h.Values; var s = new List<Shape>();
        for (var y = 1; y < 4; y++) s.Add(new DLine(0, hgt * y / 4, w, hgt * y / 4, "#22FFFFFF", 1, "RASTER"));
        if (v.Count < 2) return s;
        var step = w / (h.Size - 1); var off = (h.Size - v.Count) * step;
        var pts = new List<(double, double)> { (off, hgt) };
        for (var i = 0; i < v.Count; i++) pts.Add((off + i * step, hgt - hgt * Math.Clamp(v[i], 0, 100) / 100));
        pts.Add((off + (v.Count - 1) * step, hgt));
        s.Add(new DPoly(pts, color + "55", color, 1.5, "DATEN"));
        return s;
    }, "#10203A");
}
