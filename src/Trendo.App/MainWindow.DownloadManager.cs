using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Trendo.Core;
using Trendo.Core.Sys;
using S = Trendo.Core.Sys.DownloadQueue.State;

namespace Trendo.App;

/// <summary>Download-Manager (Masterprompt 131, Runde 6): Warteschlange mit mehreren gleichzeitigen Downloads, Anhalten/Fortsetzen je Eintrag,
/// Reihenfolge, Prüfsumme, Geschwindigkeit und Restzeit. Lädt über <see cref="WebTools.DownloadAsync"/> (fortsetzbar per .part-Datei).</summary>
public partial class MainWindow
{
    DownloadQueue? _dlq;
    public DownloadQueue Downloads => _dlq ??= new DownloadQueue(_core.DataRoot);
    readonly Dictionary<string, CancellationTokenSource> _dlRunning = [];
    readonly Dictionary<string, (ProgressBar Bar, TextBlock Info)> _dlViews = [];
    DispatcherTimer? _dlTimer; string _dlFilter = "alle"; string _dlMsg = "";

    /// <summary>Für Tests: statt echtem Netz (Eintrag, Fortschritt, Abbruch).</summary>
    public static Func<DownloadQueue.Job, IProgress<(long Done, long Total)>, CancellationToken, Task>? DownloadRunOverride { get; set; }

    /// <summary>Zielordner: der Download-Ordner des Nutzers – im Testmodus (eigener Datenordner) nie, damit Prüfungen keine Dateien in echte Ordner legen.</summary>
    string DownloadManagerFolder => _opt.Isolated ? Directory.CreateDirectory(Path.Combine(_core.DataRoot, "test-downloads")).FullName : DownloadTarget;

    /// <summary>Einträge hinzufügen (eingefügter Text mit einem oder mehreren Links) und Warteschlange anstoßen.</summary>
    public int DownloadAdd(string text, string? hash = null)
    {
        if (_core.Offline) { _dlMsg = "Offline-Modus: Downloads sind ausgeschaltet."; return 0; }
        var links = DownloadQueue.ParseLinks(text);
        if (links.Count == 0) { _dlMsg = "Keine http- oder https-Adresse gefunden."; return 0; }
        var n = 0;
        foreach (var u in links)
        {
            try { Downloads.Add(u, DownloadManagerFolder, links.Count == 1 ? hash : null); n++; }
            catch (ArgumentException e) { _dlMsg = e.Message; return n; }
        }
        _core.Audit.Append(Principal.LocalOwner.Name, "download.add", $"{n} Eintrag/Einträge");
        _dlMsg = n == 1 ? "1 Download in der Warteschlange." : $"{n} Downloads in der Warteschlange.";
        DownloadPump();
        return n;
    }

    /// <summary>Freie Plätze mit wartenden Einträgen füllen (höchstens „gleichzeitig“ viele laufen).</summary>
    public void DownloadPump()
    {
        foreach (var j in Downloads.ToStart()) _ = DownloadRun(j);
        if (_dlRunning.Count > 0 && _dlTimer is null)
        {
            _dlTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _dlTimer.Tick += (_, _) => { DownloadRefreshViews(); if (_dlRunning.Count == 0) { _dlTimer?.Stop(); _dlTimer = null; } };
            _dlTimer.Start();
        }
    }

    async Task DownloadRun(DownloadQueue.Job j)
    {
        var cts = new CancellationTokenSource(); _dlRunning[j.Id] = cts; Downloads.Started(j);
        var last = DateTime.UtcNow; var lastDone = j.Done;
        var prog = new Progress<(long Done, long Total)>(p =>
        {
            var now = DateTime.UtcNow; var sec = (now - last).TotalSeconds;
            if (sec >= 0.5 || p.Done == p.Total) { DownloadQueue.Progress(j, p.Done, p.Total, sec); last = now; }
            else { j.Done = p.Done; if (p.Total > 0) j.Total = p.Total; }
        });
        try
        {
            if (DownloadRunOverride is { } o) await o(j, prog, cts.Token);
            else if (_opt.Isolated) throw new IOException("Testmodus – es wird nichts aus dem Internet geladen.");
            else await WebTools.DownloadAsync(j.Url, j.Target, prog, cts.Token);
            Downloads.Completed(j);
            _core.Audit.Append(Principal.LocalOwner.Name, j.Status == S.Fertig ? "download.ok" : "download.hash_fail", $"{new Uri(j.Url).Host} {j.Name}");
        }
        catch (OperationCanceledException) { if (j.Status == S.Lädt) { j.Status = S.Angehalten; j.Message = "Angehalten."; } j.BytesPerSecond = 0; Downloads.Save(); }
        catch (Exception e) when (e is HttpRequestException or IOException or UnauthorizedAccessException or ArgumentException or TaskCanceledException)
        { Downloads.Failed(j, $"Abgebrochen: {e.Message} – „Erneut versuchen“ macht dort weiter, wo er aufgehört hat."); }
        finally { _dlRunning.Remove(j.Id); cts.Dispose(); }
        _ui.Post(() => { if (CurrentView == "downloads") Show("downloads"); DownloadPump(); });
    }

    public void DownloadPause(string id) { if (_dlRunning.TryGetValue(id, out var c)) c.Cancel(); Downloads.Pause(id); if (CurrentView == "downloads") Show("downloads"); }
    public void DownloadResume(string id) { Downloads.Resume(id); DownloadPump(); if (CurrentView == "downloads") Show("downloads"); }
    public void DownloadRemove(string id) { if (_dlRunning.TryGetValue(id, out var c)) c.Cancel(); Downloads.Remove(id); if (CurrentView == "downloads") Show("downloads"); }

    void DownloadRefreshViews()
    {
        foreach (var j in Downloads.Jobs)
            if (_dlViews.TryGetValue(j.Id, out var v)) { v.Bar.Value = j.Fraction * 100; v.Info.Text = DownloadInfo(j); }
    }

    static string DownloadInfo(DownloadQueue.Job j)
    {
        static string Mb(long b) => b < 0 ? "?" : b >= 1 << 30 ? $"{b / (double)(1 << 30):0.00} GB" : $"{b / (double)(1 << 20):0.0} MB";
        return j.Status switch
        {
            S.Lädt => $"{Mb(j.Done)} von {Mb(j.Total)} · {(j.BytesPerSecond > 0 ? $"{j.BytesPerSecond / (1 << 20):0.0} MB/s" : "verbindet …")} · noch {DownloadQueue.Duration(j.SecondsLeft)}",
            S.Wartet => "Wartet auf einen freien Platz …",
            S.Angehalten => $"Angehalten bei {Mb(j.Done)}{(j.Total > 0 ? $" von {Mb(j.Total)}" : "")}",
            S.Fertig => $"Fertig · {Mb(j.Total)} · {j.Finished:dd.MM. HH:mm}{(j.HashResult is { } h ? " · Prüfsumme " + h : "")}",
            _ => j.Message,
        };
    }

    /// <summary>Die Warteschlange als Karten (oben auf der Downloads-Seite).</summary>
    Control DownloadManagerPanel()
    {
        _dlViews.Clear();
        var sp = new StackPanel { Name = "dlm", Spacing = 6 };
        var input = new TextBox { Name = "dlm-url", PlaceholderText = "Link einfügen – auch mehrere auf einmal (z. B. eine Liste)", AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 34, MaxHeight = 90, Width = 520, IsEnabled = !_core.Offline };
        var hash = new TextBox { Name = "dlm-hash", PlaceholderText = "Prüfsumme (optional, bei einem Link)", Width = 300, IsEnabled = !_core.Offline };
        var add = Act("dlm-add", "Herunterladen", () => { if (DownloadAdd(input.Text ?? "", hash.Text) > 0) { input.Text = ""; hash.Text = ""; } Show("downloads"); }, true);
        add.IsEnabled = !_core.Offline;
        var par = new ComboBox { Name = "dlm-parallel", ItemsSource = Enumerable.Range(1, DownloadQueue.MaxParallelLimit).Select(i => $"{i} gleichzeitig").ToList(), SelectedIndex = Downloads.MaxParallel - 1 };
        par.SelectionChanged += (_, _) => { if (par.SelectedIndex >= 0) { Downloads.MaxParallel = par.SelectedIndex + 1; Downloads.Save(); DownloadPump(); } };
        sp.Children.Add(new WrapPanel { Children = { input, new Border { Width = 6 }, new StackPanel { Spacing = 4, Children = { hash, new WrapPanel { Children = { add, new Border { Width = 6 }, par } } } } } });
        if (_dlMsg.Length > 0) sp.Children.Add(new TextBlock { Name = "dlm-msg", Text = _dlMsg, Foreground = Text, FontSize = 12, TextWrapping = TextWrapping.Wrap });

        var (run, wait, done, fail) = Downloads.Counts;
        var tabs = new WrapPanel();
        foreach (var (id, label) in new[] { ("alle", $"Alle ({Downloads.Jobs.Count})"), ("laeuft", $"Läuft/wartet ({run + wait})"), ("fertig", $"Fertig ({done})"), ("fehler", $"Fehler ({fail})") })
        {
            var b = Act("dlm-tab-" + id, label, () => { _dlFilter = id; Show("downloads"); }, _dlFilter == id); b.Margin = new Thickness(0, 0, 6, 0); tabs.Children.Add(b);
        }
        if (done > 0) tabs.Children.Add(Act("dlm-clear", "Fertige aus der Liste nehmen", () => { Downloads.ClearFinished(); Show("downloads"); }));
        if (Downloads.Jobs.Count > 0) sp.Children.Add(tabs);

        var list = Downloads.Jobs.Where(j => _dlFilter switch { "laeuft" => j.Status is S.Lädt or S.Wartet or S.Angehalten, "fertig" => j.Status == S.Fertig, "fehler" => j.Status == S.Fehler, _ => true }).ToList();
        if (Downloads.Jobs.Count == 0) sp.Children.Add(P("Noch keine Downloads. Link oben einfügen – Trendo lädt im Hintergrund, auch mehrere gleichzeitig, und macht nach einer Unterbrechung dort weiter.", Muted, 11.5));
        var i = 0;
        foreach (var j in list)
        {
            var id = j.Id; var k = i++;
            var bar = new ProgressBar { Name = $"dlm-bar-{k}", Minimum = 0, Maximum = 100, Value = j.Fraction * 100, Height = 6, IsIndeterminate = j.Status == S.Lädt && j.Total <= 0,
                Foreground = Brush.Parse(j.Status switch { S.Fertig => "#4ADE80", S.Fehler => "#F87171", S.Angehalten => "#FACC15", _ => "#60A5FA" }) };
            var info = new TextBlock { Name = $"dlm-info-{k}", Text = DownloadInfo(j), Foreground = j.Status == S.Fehler ? Brush.Parse("#F87171") : Muted, FontSize = 11, TextWrapping = TextWrapping.Wrap };
            _dlViews[id] = (bar, info);
            var btn = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right };
            void B(string n, string t, Action a, bool primary = false) { var b = Act($"dlm-{n}-{k}", t, a, primary); b.FontSize = 11; b.Margin = new Thickness(0, 0, 4, 0); btn.Children.Add(b); }
            switch (j.Status)
            {
                case S.Lädt or S.Wartet: B("pause", "❚❚ Anhalten", () => DownloadPause(id)); break;
                case S.Angehalten: B("resume", "▶ Fortsetzen", () => DownloadResume(id), true); break;
                case S.Fehler: B("retry", "↻ Erneut versuchen", () => DownloadResume(id), true); break;
                case S.Fertig:
                    B("open", "Öffnen", () => Launch(j.Target), true);
                    B("folder", "Ordner", () => Launch(Path.GetDirectoryName(j.Target)!)); break;
            }
            if (j.Status is S.Wartet or S.Angehalten) { B("up", "▲", () => { Downloads.Move(id, -1); Show("downloads"); }); B("down", "▼", () => { Downloads.Move(id, 1); Show("downloads"); }); }
            B("remove", "✕", () => DownloadRemove(id));
            var risky = j.Status == S.Fertig && Browsers.Risky(j.Name);
            sp.Children.Add(Card(new StackPanel { Name = $"dlm-job-{k}", Spacing = 4, Children = {
                Row(new StackPanel { Children = { new TextBlock { Text = j.Name, Foreground = Text, FontWeight = FontWeight.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis }, P(new Uri(j.Url).Host, Muted, 10.5) } }, btn),
                bar, info,
                risky ? P("⚠ Programmdatei – nur öffnen, wenn du der Quelle vertraust.", Brush.Parse("#FACC15"), 10.5) : new Panel() } }));
        }
        return sp;
    }
}
