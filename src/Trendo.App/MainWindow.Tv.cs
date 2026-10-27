using System.Diagnostics;
using System.IO.Compression;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Trendo.Core;
using Trendo.Core.Tv;

namespace Trendo.App;

/// <summary>Video im Trendo-Fenster: ein eingebettetes mpv-Fenster (--wid). Steuerung über mpv-IPC (Pause, Lautstärke, Spulen).
/// Zeitversetzt: großer Rückwärts-Puffer, damit man live pausieren und zurückspulen kann.</summary>
public sealed class MpvView : NativeControlHost
{
    public string Slot { get; }
    public string? MpvPath { get; set; }
    public string? Url { get; private set; }
    public bool Muted { get; set; }
    Process? _proc;
    IntPtr _hwnd;
    string Pipe => $"trendo-tv-{Slot}-{Environment.ProcessId}";

    public MpvView(string slot) { Slot = slot; }

    protected override IPlatformHandle CreateNativeControlCore(IPlatformHandle parent)
    {
        var h = base.CreateNativeControlCore(parent);
        _hwnd = h.Handle;
        if (Url is { } u) Start(u); // nach dem Neuaufbau der Seite weiterlaufen lassen
        return h;
    }

    protected override void DestroyNativeControlCore(IPlatformHandle control)
    {
        Kill();
        _hwnd = IntPtr.Zero;
        base.DestroyNativeControlCore(control);
    }

    public void Play(string url)
    {
        Url = url;
        if (_proc is { HasExited: false } && Send("loadfile", url)) return; // schnelles Umschalten ohne Neustart
        Start(url);
    }

    void Start(string url)
    {
        Kill();
        if (_hwnd == IntPtr.Zero || MpvPath is null || !File.Exists(MpvPath)) return;
        var psi = new ProcessStartInfo(MpvPath) { UseShellExecute = false, CreateNoWindow = true };
        foreach (var a in new[] { $"--wid={_hwnd}", "--no-terminal", "--force-window=yes", "--keep-open=yes", "--idle=yes", "--osc=yes", "--input-vo-keyboard=no", "--cache=yes",
                     "--demuxer-max-back-bytes=1GiB", "--demuxer-max-bytes=150MiB", "--hwdec=auto-safe", $"--input-ipc-server=\\\\.\\pipe\\{Pipe}", Muted ? "--mute=yes" : "--mute=no", url })
            psi.ArgumentList.Add(a);
        try { _proc = Process.Start(psi); } catch (System.ComponentModel.Win32Exception) { _proc = null; }
    }

    /// <summary>Befehl an mpv (JSON-IPC). false, wenn mpv nicht läuft.</summary>
    public bool Send(params object[] command)
    {
        if (_proc is not { HasExited: false }) return false;
        try
        {
            using var pipe = new NamedPipeClientStream(".", Pipe, PipeDirection.Out);
            pipe.Connect(400);
            var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { command }) + "\n");
            pipe.Write(bytes); pipe.Flush();
            return true;
        }
        catch (Exception e) when (e is IOException or TimeoutException or UnauthorizedAccessException) { return false; }
    }

    public void Stop() { Url = null; Kill(); }

    void Kill()
    {
        try { if (_proc is { HasExited: false }) _proc.Kill(true); } catch (Exception e) when (e is InvalidOperationException or System.ComponentModel.Win32Exception) { }
        _proc?.Dispose(); _proc = null;
    }
}

/// <summary>Studio „TV & Live“ (Wunsch 27.10.2026): IPTV schauen – frei, vollständig, eigene Trendo-Gestaltung. Senderliste mit „Jetzt läuft“,
/// Bild im Fenster, Programmführer, Favoriten, Zuletzt gesehen, Gruppen (ausblenden, mit PIN sperren), Aufnahmen (ffmpeg), zeitversetzt
/// (Puffer und Catch-up des Anbieters), Mehrfachansicht mit bis zu 4 Sendern. Trendo bringt keine Sender mit – nur eigene, legale Quellen.</summary>
public partial class MainWindow
{
    public const string TvStudio = "STUDIO-TV-01";
    string _tvTab = "live", _tvGroup = "Alle", _tvQuery = "", _tvMsg = "";
    string? _tvChannel, _tvProgSel;
    bool _tvUnlocked;
    List<TvChannel>? _tvChannels;
    List<TvProgramme>? _tvProgs;
    MpvView? _tvPlayer;
    readonly MpvView?[] _tvMulti = new MpvView?[4];
    readonly string?[] _tvMultiCh = new string?[4];
    readonly Dictionary<string, Process> _tvRecProcs = [];
    DispatcherTimer? _tvRecTimer;
    TextBlock? _tvNowTitle, _tvNowInfo, _tvNextInfo;
    ProgressBar? _tvNowBar;
    Button? _tvFavBtn;
    static readonly HttpClient TvHttp = new(new SocketsHttpHandler { AutomaticDecompression = System.Net.DecompressionMethods.All }) { Timeout = TimeSpan.FromMinutes(3) };
    /// <summary>Für Tests: Abspielen nachbilden (Platz, Adresse).</summary>
    public Action<string, string>? TvPlayOverride { get; set; }
    /// <summary>Für Tests: Inhalt einer Adresse (Wiedergabeliste, Programmführer) statt Netz.</summary>
    public Func<string, Task<byte[]>>? TvFetchOverride { get; set; }
    /// <summary>Für Tests: Aufnahme starten (Adresse, Datei, Sekunden) statt ffmpeg.</summary>
    public Action<string, string, int>? TvRecordOverride { get; set; }
    public TvLibrary Tv => new(_core.DataRoot);
    public string? TvCurrent => _tvChannel;

    void InitTv()
    {
        RegisterTab(FachTabs, "tv", "TV & Live", TvStudio, TvView, "iptv", "fernsehen", "tv", "sender", "live tv", "m3u", "xtream", "epg", "programmführer", "aufnahme", "playlist");
        if (Tv.Recordings.Any(r => r.State == "geplant")) TvRecWatch();
    }

    List<TvChannel> TvChannels() => _tvChannels ??= Tv.Channels();
    List<TvProgramme> TvProgs() => _tvProgs ??= Tv.Programmes();
    void TvReload() { _tvChannels = null; _tvProgs = null; }
    string? MpvExe => _deps.ResolvePath(DependencyCatalog.Get("mpv"));
    string? FfmpegExe => _deps.ResolvePath(DependencyCatalog.Get("ffmpeg"));

    async Task<byte[]> TvFetch(string url)
    {
        if (TvFetchOverride is { } o) return await o(url);
        if (!Uri.TryCreate(url, UriKind.Absolute, out var u) || u.Scheme is not ("http" or "https")) throw new InvalidOperationException("Bitte eine Adresse mit http:// oder https:// angeben.");
        using var req = new HttpRequestMessage(HttpMethod.Get, u);
        req.Headers.UserAgent.ParseAdd("TrendoOS/1.0");
        using var res = await TvHttp.SendAsync(req, HttpCompletionOption.ResponseHeadersRead);
        res.EnsureSuccessStatusCode();
        if (res.Content.Headers.ContentLength > 400L << 20) throw new InvalidOperationException("Die Datei ist zu groß.");
        return await res.Content.ReadAsByteArrayAsync();
    }

    /// <summary>Quelle hinzufügen und gleich laden: M3U-Adresse, M3U-Datei oder Xtream-Zugang.</summary>
    public async Task<int> TvAddSource(string name, string kind, string urlOrFile, string? epg = null, string? user = null, string? pass = null)
    {
        if (_core.Offline && kind != "datei") { _tvMsg = "Trendo ist offline – zum Laden braucht es Internet."; return 0; }
        var url = kind == "xtream" ? Xtream.PlaylistUrl(urlOrFile, user ?? "", pass ?? "") : urlOrFile.Trim();
        if (kind == "xtream") epg ??= Xtream.EpgUrl(urlOrFile, user ?? "", pass ?? "");
        var src = Tv.AddSource(name, kind, kind == "datei" ? Path.GetFullPath(url) : url, string.IsNullOrWhiteSpace(epg) ? null : epg.Trim(), user);
        _core.Audit.Append(Principal.LocalOwner.Name, "tv.source.add", $"{kind} {src.Name}");
        return await TvRefreshSource(src);
    }

    public async Task<int> TvRefreshSource(TvSource src)
    {
        try
        {
            var text = src.Kind == "datei" ? await File.ReadAllTextAsync(src.Url) : Encoding.UTF8.GetString(await TvFetch(src.Url));
            var (ch, epgHead) = M3u.Parse(text, src.Id);
            var films = ch.Count(c => c.Kind == "film"); var series = ch.Count(c => c.Kind == "serie");
            if (ch.Count == 0) { _tvMsg = $"In „{src.Name}“ wurden keine Sender gefunden. Ist es eine M3U-Wiedergabeliste?"; return 0; }
            Tv.StorePlaylist(src.Id, text);
            if (src.EpgUrl is null && epgHead is not null) { Tv.SetEpgUrl(src.Id, epgHead); src = src with { EpgUrl = epgHead }; }
            TvReload();
            _tvMsg = $"✓ „{src.Name}“ geladen: {ch.Count - films - series} Sender{(films > 0 ? $", {films} Filme" : "")}{(series > 0 ? $", {series} Serienfolgen" : "")}.";
            if (src.EpgUrl is not null) await TvRefreshEpg(src);
            if (src.Kind == "xtream") await TvRefreshXtreamVod(src);
            _tvVod = null; _tvSeries = null;
            return ch.Count;
        }
        catch (Exception e) when (e is HttpRequestException or IOException or InvalidOperationException or TaskCanceledException or UnauthorizedAccessException)
        {
            _tvMsg = $"„{src.Name}“ ließ sich nicht laden: {e.Message}";
            return 0;
        }
    }

    public async Task TvRefreshEpg(TvSource src)
    {
        if (src.EpgUrl is null) return;
        try
        {
            var bytes = await TvFetch(src.EpgUrl);
            Stream s = new MemoryStream(bytes);
            if (bytes.Length > 2 && bytes[0] == 0x1F && bytes[1] == 0x8B) s = new GZipStream(s, CompressionMode.Decompress);
            var ids = TvChannels().Where(c => c.Id.StartsWith(src.Id + ":") && c.EpgId is not null).Select(c => c.EpgId!).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var progs = await Task.Run(() => Xmltv.Parse(s, DateTime.UtcNow.AddDays(-7), DateTime.UtcNow.AddDays(4), ids.Count > 0 ? ids : null));
            s.Dispose();
            Tv.StoreEpg(src.Id, progs);
            _tvProgs = null;
            _tvMsg += $" Programmführer: {progs.Count} Sendungen.";
        }
        catch (Exception e) when (e is HttpRequestException or IOException or InvalidOperationException or System.Xml.XmlException or TaskCanceledException or InvalidDataException)
        {
            _tvMsg += " Der Programmführer ließ sich nicht laden: " + e.Message;
        }
    }

    bool TvGroupLocked(string group) => Tv.LockedGroups.Contains(group) && !_tvUnlocked;

    /// <summary>Sender abspielen (im Fenster). Gesperrte Gruppen brauchen erst die Anmeldung (PIN/Passwort).</summary>
    public bool TvPlay(string channelId, string slot = "main")
    {
        if (TvChannels().FirstOrDefault(c => c.Id == channelId) is not { } c) return false;
        if (TvGroupLocked(c.Group)) { _tvMsg = $"„{c.Group}“ ist gesperrt. Oben „Gesperrte Gruppen öffnen“ und mit deiner PIN bestätigen."; return false; }
        return TvPlayUrl(c.Url, slot, c);
    }

    bool TvPlayUrl(string url, string slot, TvChannel? c = null)
    {
        if (slot == "main") { _tvChannel = c?.Id ?? _tvChannel; if (c is not null) { Tv.Watched(c.Id); _tvPlayingTitle = null; } }
        if (TvPlayOverride is { } o) { o(slot, url); TvUpdateNow(); return true; }
        if (MpvExe is null) { _tvMsg = "Zum Abspielen braucht Trendo den Player mpv (im Vollpaket enthalten). Unten „mpv hinzufügen“."; return false; }
        var view = slot == "main" ? _tvPlayer : _tvMulti[int.Parse(slot[1..])];
        if (view is null) return false;
        view.MpvPath = MpvExe;
        view.Play(url);
        TvUpdateNow();
        return true;
    }

    void TvUpdateNow()
    {
        if (_tvPlayingTitle is { } film) { if (_tvNowTitle is not null) _tvNowTitle.Text = "🎬 " + film; return; }
        if (_tvChannel is null || TvChannels().FirstOrDefault(x => x.Id == _tvChannel) is not { } c) return;
        var (now, next) = TvLibrary.NowNext(TvProgs(), c, DateTime.UtcNow);
        if (_tvNowTitle is not null) _tvNowTitle.Text = $"{c.Number}  {c.Name}";
        if (_tvNowInfo is not null) _tvNowInfo.Text = now is null ? "Keine Programminfo" : $"Jetzt: {now.Title} ({now.Start.ToLocalTime():HH:mm}–{now.Stop.ToLocalTime():HH:mm})";
        if (_tvNextInfo is not null) _tvNextInfo.Text = next is null ? "" : $"Danach: {next.Title} ({next.Start.ToLocalTime():HH:mm})";
        if (_tvNowBar is not null) _tvNowBar.Value = now?.Progress(DateTime.UtcNow) * 100 ?? 0;
        if (_tvFavBtn is not null) _tvFavBtn.Content = Tv.Favorites.Contains(c.Id) ? "★ Favorit" : "☆ Favorit";
    }

    /// <summary>Nächster/vorheriger Sender in der aktuellen Liste (Pfeiltasten, Kanal +/−).</summary>
    public void TvZap(int delta)
    {
        var list = TvFiltered();
        if (list.Count == 0) return;
        var i = list.FindIndex(c => c.Id == _tvChannel);
        var n = list[((i < 0 ? 0 : i + delta) % list.Count + list.Count) % list.Count];
        TvPlay(n.Id);
    }

    List<TvChannel> TvFiltered()
    {
        var all = TvChannels();
        var hidden = Tv.HiddenGroups;
        IEnumerable<TvChannel> q = _tvGroup switch
        {
            "Favoriten" => Tv.Favorites.Select(f => all.FirstOrDefault(c => c.Id == f)).OfType<TvChannel>(),
            "Zuletzt" => Tv.Recent.Select(f => all.FirstOrDefault(c => c.Id == f)).OfType<TvChannel>(),
            "Alle" => all.Where(c => !hidden.Contains(c.Group)),
            var g => all.Where(c => c.Group == g),
        };
        if (_tvQuery.Trim() is { Length: > 0 } s) q = q.Where(c => c.Name.Contains(s, StringComparison.OrdinalIgnoreCase) || c.Number.ToString() == s);
        return q.Where(c => !TvGroupLocked(c.Group) || _tvGroup == c.Group).ToList();
    }

    // ---------- Aufnahmen ----------
    /// <summary>Aufnahme planen (oder sofort starten, wenn die Zeit schon läuft).</summary>
    public TvRecording TvPlanRecording(TvChannel c, string title, DateTime startUtc, DateTime stopUtc)
    {
        var r = Tv.Plan(c, title, startUtc, stopUtc);
        _core.Audit.Append(Principal.LocalOwner.Name, "tv.record.plan", $"{c.Name} {startUtc:O}");
        TvRecWatch(); TvRecTick();
        return r;
    }

    void TvRecWatch()
    {
        if (_tvRecTimer is not null) return;
        _tvRecTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(20) };
        _tvRecTimer.Tick += (_, _) => TvRecTick();
        _tvRecTimer.Start();
        Closed += (_, _) => { _tvRecTimer?.Stop(); foreach (var p in _tvRecProcs.Values) try { if (!p.HasExited) p.Kill(true); } catch (InvalidOperationException) { } };
    }

    /// <summary>Prüft, ob eine geplante Aufnahme starten soll, und startet sie (ffmpeg kopiert den Stream ohne Umrechnen).</summary>
    public void TvRecTick()
    {
        foreach (var r in Tv.Due(DateTime.UtcNow))
        {
            if (TvChannels().FirstOrDefault(c => c.Id == r.ChannelId) is not { } c) { Tv.UpdateRecording(r.Id, "Sender fehlt"); continue; }
            var folder = WorkFolder("TV-Aufnahmen"); Directory.CreateDirectory(folder);
            var file = TvLibrary.RecordingFile(folder, r);
            var secs = (int)Math.Max(10, (r.Stop - DateTime.UtcNow).TotalSeconds);
            if (TvRecordOverride is { } o) { o(c.Url, file, secs); Tv.UpdateRecording(r.Id, "läuft", file); continue; }
            if (FfmpegExe is null) { Tv.UpdateRecording(r.Id, "ffmpeg fehlt"); continue; }
            var psi = new ProcessStartInfo(FfmpegExe) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = false };
            foreach (var a in new[] { "-hide_banner", "-loglevel", "error", "-y", "-i", c.Url, "-c", "copy", "-t", secs.ToString(), file }) psi.ArgumentList.Add(a);
            try
            {
                var p = Process.Start(psi)!; p.EnableRaisingEvents = true;
                _tvRecProcs[r.Id] = p; Tv.UpdateRecording(r.Id, "läuft", file);
                var id = r.Id;
                p.Exited += (_, _) => Dispatcher.UIThread.Post(() =>
                {
                    _tvRecProcs.Remove(id);
                    var ok = File.Exists(file) && new FileInfo(file).Length > 0;
                    Tv.UpdateRecording(id, ok ? "fertig" : "fehlgeschlagen");
                    Toast(ok ? $"⏺ Aufnahme fertig: {r.Title}" : $"Aufnahme „{r.Title}“ hat nicht geklappt.", ("Ansehen", new Action(() => { _tvTab = "rec"; Show("tv"); })));
                });
                _core.Audit.Append(Principal.LocalOwner.Name, "tv.record.start", c.Name);
            }
            catch (System.ComponentModel.Win32Exception) { Tv.UpdateRecording(r.Id, "fehlgeschlagen"); }
        }
    }

    public void TvStopRecording(string id)
    {
        if (_tvRecProcs.Remove(id, out var p)) try { if (!p.HasExited) p.Kill(true); } catch (InvalidOperationException) { }
        Tv.UpdateRecording(id, "fertig");
    }

    // ---------- Oberfläche ----------
    static readonly (string Id, string Label)[] TvTabs = [("live", "📺 Live-TV"), ("film", "🎬 Filme"), ("serie", "🍿 Serien"), ("guide", "🗓 Programmführer"), ("rec", "⏺ Aufnahmen"), ("multi", "▦ Mehrfachansicht"), ("quellen", "⚙ Quellen")];

    Control TvView()
    {
        TvRecWatch();
        var sp = new StackPanel { Spacing = 10 };
        var tabs = new WrapPanel { Name = "tv-tabs" };
        foreach (var (id, label) in TvTabs)
        {
            var on = _tvTab == id;
            var b = new Button { Name = "tv-tab-" + id, Content = label, Margin = new Thickness(0, 0, 6, 6), Padding = new Thickness(12, 6), CornerRadius = new CornerRadius(14), Background = on ? Accent : Brushes.Transparent, BorderBrush = EdgeLit, BorderThickness = new Thickness(on ? 0 : 1) };
            var tid = id; b.Click += (_, _) => { _tvTab = tid; _tvMsg = ""; Show("tv"); };
            tabs.Children.Add(b);
        }
        sp.Children.Add(tabs);
        if (_tvMsg.Length > 0) sp.Children.Add(P(_tvMsg, _tvMsg.StartsWith('✓') ? Brush.Parse("#4ADE80") : Brush.Parse("#FACC15"), 12.5));
        if (Tv.Sources.Count == 0 && _tvTab != "quellen")
        {
            sp.Children.Add(WCard(new StackPanel { Spacing = 8, Children = {
                H("Willkommen bei TV & Live", 16),
                P("Füge deine Senderliste hinzu – eine M3U-Adresse oder -Datei oder einen Xtream-Zugang deines Anbieters. Trendo bringt keine Sender mit; bitte nur legale Quellen nutzen.", Text, 13),
                TvButton("tv-start-sources", "Senderliste hinzufügen", () => { _tvTab = "quellen"; Show("tv"); }, true) } }));
            return Page("TV & Live", sp);
        }
        sp.Children.Add(_tvTab switch { "film" => TvFilms(), "serie" => TvSeriesView(), "guide" => TvGuide(), "rec" => TvRecordings(), "multi" => TvMultiView(), "quellen" => TvSources(), _ => TvLive() });
        return Page("TV & Live", sp);
    }

    Button TvButton(string name, string text, Action click, bool primary = false)
    {
        var b = new Button { Name = name, Content = text, Background = primary ? Accent : Brushes.Transparent, Padding = new Thickness(10, 5), Margin = new Thickness(0, 0, 6, 6) };
        b.Click += (_, _) => click();
        return b;
    }

    Control TvLive()
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("320,*"), Name = "tv-live" };
        // Links: Gruppen, Suche, Sender
        var left = new StackPanel { Spacing = 6, Margin = new Thickness(0, 0, 10, 0) };
        var groups = new List<string> { "Alle", "Favoriten", "Zuletzt" };
        groups.AddRange(TvChannels().Select(c => c.Group).Distinct().OrderBy(g => g));
        var gsel = new ComboBox { Name = "tv-group", ItemsSource = groups, SelectedItem = groups.Contains(_tvGroup) ? _tvGroup : "Alle", HorizontalAlignment = HorizontalAlignment.Stretch };
        gsel.SelectionChanged += (_, _) => { if (gsel.SelectedItem is string g && g != _tvGroup) { _tvGroup = g; Show("tv"); } };
        var search = new TextBox { Name = "tv-search", Text = _tvQuery, PlaceholderText = "Sender suchen oder Nummer …" };
        var list = new StackPanel { Name = "tv-channels", Spacing = 2 };
        void Fill()
        {
            list.Children.Clear();
            var now = DateTime.UtcNow; var progs = TvProgs();
            foreach (var c in TvFiltered().Take(400))
            {
                var (np, _) = TvLibrary.NowNext(progs, c, now);
                var on = c.Id == _tvChannel;
                var row = new StackPanel { Spacing = 2, Children = {
                    new TextBlock { Text = $"{c.Number,4}  {c.Name}{(Tv.Favorites.Contains(c.Id) ? "  ★" : "")}", Foreground = Text, FontSize = 13, FontWeight = on ? FontWeight.SemiBold : FontWeight.Normal, TextTrimming = TextTrimming.CharacterEllipsis } } };
                if (np is not null)
                {
                    row.Children.Add(new TextBlock { Text = np.Title, Foreground = Muted, FontSize = 11, TextTrimming = TextTrimming.CharacterEllipsis });
                    row.Children.Add(new ProgressBar { Value = np.Progress(now) * 100, Height = 3, MinHeight = 3, Foreground = Brush.Parse("#D4A84B") });
                }
                var b = new Button { Name = "tv-ch-" + c.Id.Replace(':', '-'), Content = row, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch, Padding = new Thickness(8, 5), CornerRadius = new CornerRadius(8), Background = on ? Brush.Parse("#332F86F6") : Brushes.Transparent };
                var id = c.Id;
                b.Click += (_, _) => { if (!TvPlay(id)) Show("tv"); else Fill(); };
                list.Children.Add(b);
            }
            if (list.Children.Count == 0) list.Children.Add(P(_tvGroup == "Favoriten" ? "Noch keine Favoriten – beim Sender „☆ Favorit“ antippen." : "Keine Sender gefunden.", null, 12));
        }
        search.TextChanged += (_, _) => { _tvQuery = search.Text ?? ""; Fill(); };
        Fill();
        left.Children.Add(gsel); left.Children.Add(search);
        left.Children.Add(new ScrollViewer { Content = list, MaxHeight = 640, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        grid.Children.Add(left);

        // Rechts: Bild, Steuerung, Jetzt/Danach
        var right = new StackPanel { Spacing = 8 };
        _tvPlayer ??= new MpvView("main");
        if (!TvIsFullscreen) Detach(_tvPlayer); // im Vollbild bleibt das Bild dort
        var screen = new Border { Name = "tv-screen", Background = Brushes.Black, CornerRadius = new CornerRadius(12), ClipToBounds = true, MinHeight = 260, Height = 380,
            Child = TvIsFullscreen ? P("Läuft im Vollbild – Esc zum Verkleinern", Brushes.White, 13) : _tvPlayer };
        if (MpvExe is null && TvPlayOverride is null)
            screen.Child = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center, Spacing = 8, Children = {
                P("Für das Bild braucht Trendo den freien Player mpv (im Vollpaket enthalten).", Brushes.White, 13),
                TvButton("tv-get-mpv", "mpv hinzufügen", () => OpenAddHub("alles", "mpv"), true) } };
        right.Children.Add(screen);
        var vol = new Slider { Name = "tv-volume", Minimum = 0, Maximum = 130, Value = 100, Width = 140, VerticalAlignment = VerticalAlignment.Center };
        vol.ValueChanged += (_, _) => _tvPlayer?.Send("set_property", "volume", Math.Round(vol.Value));
        void Ctrl(params object[] cmd) { if (_tvPlayer?.Send(cmd) != true && TvPlayOverride is null) { _tvMsg = "Erst einen Sender wählen."; Show("tv"); } }
        _tvFavBtn = TvButton("tv-fav", "☆ Favorit", () =>
        {
            if (_tvChannel is null) { _tvMsg = "Erst einen Sender wählen."; Show("tv"); return; }
            var on = Tv.ToggleFavorite(_tvChannel); _tvFavBtn!.Content = on ? "★ Favorit" : "☆ Favorit"; Fill();
        });
        var controls = new WrapPanel { Name = "tv-controls", Children = {
            TvButton("tv-prev", "◀ Sender", () => TvZap(-1)), TvButton("tv-next", "Sender ▶", () => TvZap(1)),
            TvButton("tv-pause", "⏯ Pause", () => Ctrl("cycle", "pause")),
            TvButton("tv-back", "⏪ 30 s", () => Ctrl("seek", -30)), TvButton("tv-fwd", "⏩ 30 s", () => Ctrl("seek", 30)),
            TvButton("tv-live-now", "● Live", () => Ctrl("seek", 100, "absolute-percent")),
            _tvFavBtn,
            TvButton("tv-rec-now", "⏺ Aufnehmen", () =>
            {
                if (_tvChannel is null || TvChannels().FirstOrDefault(x => x.Id == _tvChannel) is not { } c) { _tvMsg = "Erst einen Sender wählen."; Show("tv"); return; }
                var (np, _) = TvLibrary.NowNext(TvProgs(), c, DateTime.UtcNow);
                TvPlanRecording(c, np?.Title ?? c.Name, DateTime.UtcNow, np?.Stop ?? DateTime.UtcNow.AddHours(1));
                _tvMsg = $"✓ Aufnahme läuft: {np?.Title ?? c.Name} (bis {(np?.Stop ?? DateTime.UtcNow.AddHours(1)).ToLocalTime():HH:mm})."; Show("tv");
            }),
            TvButton("tv-full", "⛶ Vollbild", () => TvFullscreen(true)),
            new TextBlock { Text = "🔊", Foreground = Text, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 4, 6) }, vol } };
        right.Children.Add(controls);
        _tvNowTitle = new TextBlock { Name = "tv-now-title", Foreground = Text, FontSize = 16, FontWeight = FontWeight.SemiBold, Text = "Kein Sender gewählt" };
        _tvNowInfo = new TextBlock { Name = "tv-now-info", Foreground = Text, FontSize = 12.5, TextWrapping = TextWrapping.Wrap };
        _tvNextInfo = new TextBlock { Name = "tv-next-info", Foreground = Muted, FontSize = 12, TextWrapping = TextWrapping.Wrap };
        _tvNowBar = new ProgressBar { Name = "tv-now-bar", Height = 4, MinHeight = 4, Foreground = Brush.Parse("#D4A84B") };
        right.Children.Add(WCard(new StackPanel { Spacing = 4, Children = { _tvNowTitle, _tvNowInfo, _tvNowBar, _tvNextInfo,
            P("Zeitversetzt: Pause und ⏪ spulen bis etwa 30 Minuten zurück. Sendungen von früher: im Programmführer, wenn dein Anbieter das anbietet.", null, 11) } }));
        Grid.SetColumn(right, 1); grid.Children.Add(right);
        TvUpdateNow();
        grid.KeyDown += (_, e) => { if (e.Key is Key.Up or Key.PageUp) { TvZap(-1); e.Handled = true; } else if (e.Key is Key.Down or Key.PageDown) { TvZap(1); e.Handled = true; } };
        return grid;
    }

    Control TvGuide()
    {
        var sp = new StackPanel { Spacing = 8, Name = "tv-guide" };
        var now = DateTime.UtcNow; var from = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute < 30 ? 0 : 30, 0, DateTimeKind.Utc); var to = from.AddHours(4);
        const double perMin = 4.5;
        var progs = TvProgs();
        if (progs.Count == 0) sp.Children.Add(P("Noch kein Programmführer geladen. Unter „Quellen“ eine EPG-Adresse (XMLTV) eintragen – bei Xtream-Zugängen geht das von selbst.", Text, 12.5));
        var rows = new StackPanel { Spacing = 3 };
        var ruler = new Canvas { Height = 22, Width = 160 + (to - from).TotalMinutes * perMin };
        for (var t = from; t < to; t = t.AddMinutes(30))
        {
            var tb = new TextBlock { Text = t.ToLocalTime().ToString("HH:mm"), Foreground = Muted, FontSize = 11 };
            Canvas.SetLeft(tb, 160 + (t - from).TotalMinutes * perMin); ruler.Children.Add(tb);
        }
        var nowLine = new Border { Width = 2, Background = Brush.Parse("#D4A84B"), Height = 22 };
        Canvas.SetLeft(nowLine, 160 + (now - from).TotalMinutes * perMin); ruler.Children.Add(nowLine);
        rows.Children.Add(ruler);
        foreach (var c in TvFiltered().Where(c => c.EpgId is not null).Take(60))
        {
            var row = new Canvas { Height = 40, Width = ruler.Width };
            var name = new TextBlock { Text = $"{c.Number} {c.Name}", Foreground = Text, FontSize = 12, Width = 150, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };
            Canvas.SetTop(name, 11); row.Children.Add(name);
            foreach (var p in progs.Where(p => string.Equals(p.Channel, c.EpgId, StringComparison.OrdinalIgnoreCase) && p.Stop > from && p.Start < to))
            {
                var s = p.Start < from ? from : p.Start; var e = p.Stop > to ? to : p.Stop;
                var on = p.IsOn(now);
                var b = new Button { Name = "tv-prog-" + c.Id.Replace(':', '-') + "-" + p.Start.ToString("HHmm"), Width = Math.Max(8, (e - s).TotalMinutes * perMin - 2), Height = 36, Padding = new Thickness(6, 2), CornerRadius = new CornerRadius(6),
                    Background = on ? Brush.Parse("#402F86F6") : Brush.Parse("#1AFFFFFF"), BorderBrush = _tvProgSel == $"{c.Id}|{p.Start:O}" ? Brush.Parse("#D4A84B") : Brushes.Transparent, BorderThickness = new Thickness(1),
                    Content = new TextBlock { Text = p.Title, Foreground = Text, FontSize = 11.5, TextTrimming = TextTrimming.CharacterEllipsis } };
                ToolTip.SetTip(b, $"{p.Title}\n{p.Start.ToLocalTime():HH:mm}–{p.Stop.ToLocalTime():HH:mm}");
                var key = $"{c.Id}|{p.Start:O}";
                b.Click += (_, _) => { _tvProgSel = key; Show("tv"); };
                Canvas.SetLeft(b, 160 + (s - from).TotalMinutes * perMin); Canvas.SetTop(b, 2); row.Children.Add(b);
            }
            rows.Children.Add(row);
        }
        sp.Children.Add(new ScrollViewer { Content = rows, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, MaxHeight = 520 });
        // Details zur gewählten Sendung
        if (_tvProgSel?.Split('|') is [var cid, var st] && TvChannels().FirstOrDefault(x => x.Id == cid) is { } ch
            && progs.FirstOrDefault(p => string.Equals(p.Channel, ch.EpgId, StringComparison.OrdinalIgnoreCase) && p.Start.ToString("O") == st) is { } sel)
        {
            var d = new StackPanel { Name = "tv-prog-detail", Spacing = 4 };
            d.Children.Add(H(sel.Title, 15));
            d.Children.Add(P($"{ch.Name} · {sel.Start.ToLocalTime():ddd dd.MM. HH:mm}–{sel.Stop.ToLocalTime():HH:mm}{(sel.Category.Length > 0 ? " · " + sel.Category : "")}", null, 12));
            if (sel.Description.Length > 0) d.Children.Add(P(sel.Description, Text, 12.5));
            var acts = new WrapPanel();
            if (sel.IsOn(now)) acts.Children.Add(TvButton("tv-prog-watch", "▶ Ansehen", () => { _tvTab = "live"; TvPlay(ch.Id); Show("tv"); }, true));
            if (sel.Stop > now) acts.Children.Add(TvButton("tv-prog-rec", "⏺ Aufnehmen", () => { TvPlanRecording(ch, sel.Title, sel.Start, sel.Stop); _tvMsg = $"✓ Aufnahme geplant: {sel.Title} ({sel.Start.ToLocalTime():dd.MM. HH:mm})."; Show("tv"); }));
            if (sel.Stop <= now)
            {
                if (Catchup.UrlFor(ch, sel) is { } cu) acts.Children.Add(TvButton("tv-prog-catchup", "⟲ Zeitversetzt ansehen", () => { _tvTab = "live"; Show("tv"); TvPlayUrl(cu, "main", ch); }, true));
                else acts.Children.Add(P("Diese Sendung ist vorbei. Dein Anbieter bietet für diesen Sender kein Nachschauen an.", null, 11.5));
            }
            d.Children.Add(acts);
            sp.Children.Add(WCard(d));
        }
        return sp;
    }

    Control TvRecordings()
    {
        var sp = new StackPanel { Spacing = 6, Name = "tv-recordings" };
        sp.Children.Add(P($"Aufnahmen landen im Ordner „TV-Aufnahmen“. Aufnehmen braucht ffmpeg{(FfmpegExe is null ? " – fehlt noch (unten hinzufügen)" : " ✓")}.", Text, 12.5));
        if (FfmpegExe is null && TvRecordOverride is null) sp.Children.Add(TvButton("tv-get-ffmpeg", "ffmpeg hinzufügen", () => OpenAddHub("alles", "ffmpeg"), true));
        var recs = Tv.Recordings.OrderByDescending(r => r.Start).ToList();
        if (recs.Count == 0) sp.Children.Add(P("Noch keine Aufnahmen. Im Programmführer eine Sendung antippen und „⏺ Aufnehmen“.", null, 12));
        foreach (var r in recs)
        {
            var acts = new WrapPanel { VerticalAlignment = VerticalAlignment.Center };
            var id = r.Id;
            if (r.State == "läuft") acts.Children.Add(TvButton("tv-rec-stop-" + id, "⏹ Beenden", () => { TvStopRecording(id); Show("tv"); }));
            if (r.File is { } f && File.Exists(f)) { acts.Children.Add(TvButton("tv-rec-play-" + id, "▶ Abspielen", () => { _tvTab = "live"; Show("tv"); TvPlayUrl(f, "main"); }, true)); acts.Children.Add(TvButton("tv-rec-folder-" + id, "Ordner", () => Launch(Path.GetDirectoryName(f)!))); }
            acts.Children.Add(ConfirmRemoveButton("tvrec:" + id, "tv-rec-remove-" + id, "tv", () => { TvStopRecording(id); Tv.RemoveRecording(id); return Task.CompletedTask; }));
            sp.Children.Add(WCard(new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Children = {
                new StackPanel { Children = { new TextBlock { Text = r.Title, Foreground = Text, FontSize = 13.5, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap },
                    P($"{r.ChannelName} · {r.Start.ToLocalTime():dd.MM. HH:mm}–{r.Stop.ToLocalTime():HH:mm} · {r.State}", null, 11.5) } },
                Pos(acts, 1) } }));
        }
        return sp;
    }

    Control TvMultiView()
    {
        var sp = new StackPanel { Spacing = 8, Name = "tv-multi" };
        sp.Children.Add(P("Bis zu 4 Sender gleichzeitig. Ton kommt vom ersten Bild – tippe auf „🔊“ bei einem anderen, um umzuschalten.", Text, 12.5));
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*"), RowDefinitions = new RowDefinitions("Auto,Auto") };
        var names = TvChannels().Select(c => $"{c.Number} {c.Name}").ToList();
        for (var i = 0; i < 4; i++)
        {
            var slot = i;
            _tvMulti[i] ??= new MpvView("m" + i) { Muted = i > 0 };
            Detach(_tvMulti[i]!);
            var pick = new ComboBox { Name = "tv-multi-pick-" + i, ItemsSource = names, PlaceholderText = "Sender wählen …", HorizontalAlignment = HorizontalAlignment.Stretch,
                SelectedIndex = _tvMultiCh[i] is { } sel ? TvChannels().FindIndex(c => c.Id == sel) : -1 };
            pick.SelectionChanged += (_, _) =>
            {
                if (pick.SelectedIndex < 0) return;
                var c = TvChannels()[pick.SelectedIndex];
                if (TvGroupLocked(c.Group)) { _tvMsg = $"„{c.Group}“ ist gesperrt."; Show("tv"); return; }
                _tvMultiCh[slot] = c.Id; TvPlayUrl(c.Url, "m" + slot);
            };
            var sound = TvButton("tv-multi-sound-" + i, "🔊", () =>
            {
                for (var k = 0; k < 4; k++) { if (_tvMulti[k] is { } v) { v.Muted = k != slot; v.Send("set_property", "mute", k != slot); } }
                _tvMsg = $"Ton von Bild {slot + 1}."; Show("tv");
            });
            var cell = new StackPanel { Spacing = 4, Margin = new Thickness(0, 0, 8, 8), Children = {
                new Border { Background = Brushes.Black, CornerRadius = new CornerRadius(10), ClipToBounds = true, Height = 220, Child = MpvExe is null && TvPlayOverride is null ? P("mpv fehlt", Brushes.White, 12) : _tvMulti[i] },
                new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Children = { pick, Pos(sound, 1) } } } };
            Grid.SetColumn(cell, i % 2); Grid.SetRow(cell, i / 2);
            grid.Children.Add(cell);
        }
        sp.Children.Add(grid);
        return sp;
    }

    string _tvName = "", _tvUrl = "", _tvEpg = "", _tvXServer = "", _tvXUser = "", _tvXPass = "";

    Control TvSources()
    {
        var sp = new StackPanel { Spacing = 10, Name = "tv-sources" };
        sp.Children.Add(P("Trendo bringt keine Sender mit. Füge die Senderliste deines Anbieters hinzu – bitte nur legale Quellen (z. B. frei empfangbare Sender oder dein eigenes Abo).", Text, 12.5));
        // M3U per Adresse oder Datei
        var m3u = new StackPanel { Spacing = 6 };
        m3u.Children.Add(H("Wiedergabeliste (M3U)", 14));
        var name = new TextBox { Name = "tv-src-name", Text = _tvName, PlaceholderText = "Name, z. B. „Mein Anbieter“", Width = 240 };
        var url = new TextBox { Name = "tv-src-url", Text = _tvUrl, PlaceholderText = "Adresse der M3U-Liste (https://…)", MinWidth = 320 };
        var epg = new TextBox { Name = "tv-src-epg", Text = _tvEpg, PlaceholderText = "Programmführer-Adresse (XMLTV, freiwillig)", MinWidth = 320 };
        name.TextChanged += (_, _) => _tvName = name.Text ?? ""; url.TextChanged += (_, _) => _tvUrl = url.Text ?? ""; epg.TextChanged += (_, _) => _tvEpg = epg.Text ?? "";
        var add = TvButton("tv-src-add", "Hinzufügen & laden", async () => { await TvAddSource(name.Text ?? "", "url", url.Text ?? "", epg.Text); _tvUrl = _tvEpg = ""; Show("tv"); }, true);
        var file = TvButton("tv-src-file", "M3U-Datei wählen …", async () =>
        {
            var req = new PickRequest("tv-src-file", false, false, ["m3u", "m3u8"]);
            string[]? got = PickOverride is { } f ? await f(req) : (await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { Title = "Senderliste wählen", FileTypeFilter = [new FilePickerFileType("Senderliste") { Patterns = ["*.m3u", "*.m3u8"] }] })).Select(x => x.TryGetLocalPath()).OfType<string>().ToArray();
            if (got is { Length: > 0 }) { await TvAddSource(name.Text is { Length: > 0 } n ? n : Path.GetFileNameWithoutExtension(got[0]), "datei", got[0], epg.Text); Show("tv"); }
        });
        // Adressen kann man eintippen oder aus der Zwischenablage einfügen (Wunsch 27.10.2026) – „Datei wählen“ ist nur der zusätzliche Weg
        var paste = TvButton("tv-src-paste", "📋 Einfügen", async () => { if (Clipboard is { } cb && await cb.TryGetTextAsync() is { Length: > 0 } t) { url.Text = t.Trim(); _tvUrl = url.Text; } });
        m3u.Children.Add(new WrapPanel { Children = { Sp(name), Sp(url), paste } }); m3u.Children.Add(epg); m3u.Children.Add(new WrapPanel { Children = { add, file } });
        sp.Children.Add(WCard(m3u));
        // Xtream
        var xt = new StackPanel { Spacing = 6 };
        xt.Children.Add(H("Xtream-Zugang", 14));
        var server = new TextBox { Name = "tv-x-server", Text = _tvXServer, PlaceholderText = "Server (z. B. http://anbieter.example:8080)", MinWidth = 300 };
        var user = new TextBox { Name = "tv-x-user", Text = _tvXUser, PlaceholderText = "Benutzername", Width = 180 };
        var pass = new TextBox { Name = "tv-x-pass", PasswordChar = '•', Text = _tvXPass, PlaceholderText = "Passwort", Width = 180 };
        server.TextChanged += (_, _) => _tvXServer = server.Text ?? ""; user.TextChanged += (_, _) => _tvXUser = user.Text ?? ""; pass.TextChanged += (_, _) => _tvXPass = pass.Text ?? "";
        var xadd = TvButton("tv-x-add", "Verbinden & laden", async () => { await TvAddSource(name.Text is { Length: > 0 } n ? n : "Xtream", "xtream", server.Text ?? "", null, user.Text, pass.Text); _tvXPass = ""; Show("tv"); }, true);
        xt.Children.Add(server); xt.Children.Add(new WrapPanel { Children = { Sp(user), Sp(pass) } }); xt.Children.Add(xadd);
        xt.Children.Add(P("Programmführer kommt bei Xtream automatisch mit. Die Zugangsdaten bleiben auf diesem PC.", null, 11));
        sp.Children.Add(WCard(xt));
        // Vorhandene Quellen
        foreach (var src in Tv.Sources)
        {
            var count = TvChannels().Count(c => c.Id.StartsWith(src.Id + ":"));
            var s = src;
            var acts = new WrapPanel { VerticalAlignment = VerticalAlignment.Center, Children = {
                TvButton("tv-src-refresh-" + src.Id, "Aktualisieren", async () => { await TvRefreshSource(s); Show("tv"); }),
                ConfirmRemoveButton("tvsrc:" + src.Id, "tv-src-remove-" + src.Id, "tv", () => { Tv.RemoveSource(s.Id); TvReload(); return Task.CompletedTask; }) } };
            sp.Children.Add(WCard(new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Children = {
                new StackPanel { Children = { new TextBlock { Text = src.Name, Foreground = Text, FontSize = 14, FontWeight = FontWeight.SemiBold },
                    P($"{count} Sender · {(src.Kind == "xtream" ? "Xtream" : src.Kind == "datei" ? "Datei" : "M3U-Adresse")}{(src.EpgUrl is null ? " · ohne Programmführer" : " · mit Programmführer")}", null, 11.5) } },
                Pos(acts, 1) } }));
        }
        // Gruppen: ausblenden, sperren
        var groups = TvChannels().Select(c => c.Group).Distinct().OrderBy(g => g).ToList();
        if (groups.Count > 0)
        {
            var gp = new StackPanel { Spacing = 4, Name = "tv-groups" };
            gp.Children.Add(H("Gruppen", 14));
            gp.Children.Add(P("Ausblenden: erscheint nicht unter „Alle“. Sperren: nur nach Eingabe deiner Trendo-PIN (z. B. für Kinder).", null, 11.5));
            if (Tv.LockedGroups.Count > 0)
            {
                var pin = new TextBox { Name = "tv-unlock-pin", PasswordChar = '•', PlaceholderText = "PIN oder Passwort", Width = 180 };
                gp.Children.Add(new WrapPanel { Children = { Sp(pin), TvButton("tv-unlock", _tvUnlocked ? "Gesperrte Gruppen schließen" : "Gesperrte Gruppen öffnen", () =>
                {
                    if (_tvUnlocked) { _tvUnlocked = false; _tvMsg = "Gesperrte Gruppen sind wieder zu."; }
                    else if (!Lock.Enabled || Lock.Verify(pin.Text ?? "")) { _tvUnlocked = true; _tvMsg = "✓ Gesperrte Gruppen sind bis zum Schließen von Trendo offen."; }
                    else _tvMsg = "Die PIN stimmt nicht.";
                    Show("tv");
                }) } });
            }
            foreach (var g in groups.Take(80))
            {
                var hidden = Tv.HiddenGroups.Contains(g); var locked = Tv.LockedGroups.Contains(g); var gg = g;
                var safe = new string(g.Where(char.IsLetterOrDigit).ToArray());
                gp.Children.Add(new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"), Children = {
                    new TextBlock { Text = g, Foreground = Text, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis },
                    Pos(TvButton("tv-group-hide-" + safe, hidden ? "Einblenden" : "Ausblenden", () => { Tv.ToggleHiddenGroup(gg); Show("tv"); }), 1),
                    Pos(TvButton("tv-group-lock-" + safe, locked ? "🔓 Entsperren" : "🔒 Sperren", () =>
                    {
                        if (locked && Lock.Enabled && !_tvUnlocked) { _tvMsg = "Zum Entsperren erst oben „Gesperrte Gruppen öffnen“."; Show("tv"); return; }
                        Tv.ToggleLockedGroup(gg); Show("tv");
                    }), 2) } });
            }
            sp.Children.Add(WCard(gp));
        }
        return sp;
    }
}
