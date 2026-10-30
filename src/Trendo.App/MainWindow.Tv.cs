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
        foreach (var a in new[] { $"--wid={_hwnd}", "--no-terminal", "--force-window=yes", "--keep-open=yes", "--idle=yes", "--osc=yes", "--input-vo-keyboard=no", $"--user-agent={Xtream.UserAgent}", "--cache=yes",
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
    public TvLibrary Tv => new(_core.DataRoot, _core.Secrets); // T-0240: Passwörter im sicheren Speicher
    Task? _tvMigrate;
    /// <summary>Für Tests: Umzug alter Zugangsdaten (läuft beim Start im Hintergrund).</summary>
    public Task TvMigration => _tvMigrate ?? Task.CompletedTask;
    /// <summary>Für Tests: letzte Meldung im Studio.</summary>
    public string TvMessage => _tvMsg;
    public string? TvCurrent => _tvChannel;

    void InitTv()
    {
        RegisterTab(FachTabs, "tv", "TV & Live", TvStudio, TvView, "iptv", "fernsehen", "tv", "sender", "live tv", "m3u", "xtream", "epg", "programmführer", "aufnahme", "playlist");
        if (Tv.Recordings.Any(r => r.State == "geplant")) TvRecWatch();
        // T-0240: Passwörter aus alten Einträgen (Adresse mit password=) in den sicheren Speicher umziehen – ohne das Fenster aufzuhalten
        var lib = Tv;
        if (!lib.Sources.Any(x => !TvCredentials.HasToken(x.Url) && Xtream.FromPlaylistUrl(x.Url) is { Pass.Length: > 0 })) return; // nichts umzuziehen
        _tvMigrate = Task.Run(() =>
        {
            try { if (lib.MigrateSecrets(_core.Audit) > 0) Dispatcher.UIThread.Post(() => { TvReload(); if (CurrentView == "tv") Show("tv"); }); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Text.Json.JsonException) { _core.Audit.Append("app", "tv.secrets.migrate_failed", e.GetType().Name); }
        });
    }

    List<TvChannel> TvChannels() => _tvChannels ??= Tv.Channels();

    /// <summary>Player mpv einrichten: aus dem Vollpaket – oder ohne Vollpaket aus der offiziellen Quelle (der Klick ist die Zustimmung).</summary>
    async Task TvSetupPlayer()
    {
        var mpv = DependencyCatalog.Get("mpv");
        if (!_deps.HasConsent) _deps.GiveConsent(Principal.LocalOwner);
        _tvMsg = "Player wird eingerichtet …"; Show("tv");
        await InstallOne(mpv);
        _tvMsg = MpvExe is not null ? "✓ Player ist eingerichtet – Sender antippen und los." : "✗ Der Player ließ sich nicht einrichten. Ist Internet da? Sonst das Trendo-Vollpaket anschließen.";
        Show("tv");
    }
    List<TvProgramme> TvProgs() => _tvProgs ??= Tv.Programmes();
    void TvReload() { _tvChannels = null; _tvProgs = null; _tvVod = null; _tvSeries = null; }
    Task? _tvWarm;

    /// <summary>Große Listen (Absturzbericht 27.10.2026: 339 000 Einträge, „Keine Rückmeldung“) im Hintergrund lesen – das Fenster bleibt bedienbar.
    /// Kleine Listen (unter 4 MB) werden wie bisher sofort gelesen.</summary>
    Control? TvLoading()
    {
        if (_tvMigrate is { IsCompleted: false } mig)
        {
            mig.ContinueWith(_ => Dispatcher.UIThread.Post(() => { if (CurrentView == "tv") Show("tv"); }), TaskScheduler.Default);
            return Glass2(new StackPanel { Spacing = 10, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 60), Children = {
                new TextBlock { Name = "tv-migrating", Text = "Zugangsdaten werden sicher abgelegt …", FontSize = 18, FontWeight = FontWeight.SemiBold, Foreground = GoldText(), HorizontalAlignment = HorizontalAlignment.Center },
                new ProgressBar { IsIndeterminate = true, Width = 260, HorizontalAlignment = HorizontalAlignment.Center } } }, 24);
        }
        if (_tvChannels is not null && _tvVod is not null && _tvSeries is not null && _tvProgs is not null) return null;
        var dir = Path.Combine(_core.DataRoot, "tv");
        long size = 0;
        try { if (Directory.Exists(dir)) size = Directory.GetFiles(dir).Where(f => f.EndsWith(".m3u") || f.EndsWith(".json")).Sum(f => new FileInfo(f).Length); } catch (IOException) { }
        if (size < 4_000_000) return null;
        _tvWarm ??= Task.Run(() =>
        {
            var lib = new TvLibrary(_core.DataRoot, _core.Secrets);
            var ch = lib.Channels(); var vod = lib.Vod(); var ser = lib.Series(); var pr = lib.Programmes();
            Dispatcher.UIThread.Post(() => { _tvChannels = ch; _tvVod = vod; _tvSeries = ser; _tvProgs = pr; _tvWarm = null; if (CurrentView == "tv") Show("tv"); });
        });
        return Glass2(new StackPanel { Spacing = 10, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 60), Children = {
            new TextBlock { Text = "📺", FontSize = 48, HorizontalAlignment = HorizontalAlignment.Center },
            new TextBlock { Name = "tv-loading", Text = "Deine Sender, Filme und Serien werden vorbereitet …", FontSize = 18, FontWeight = FontWeight.SemiBold, Foreground = GoldText(), HorizontalAlignment = HorizontalAlignment.Center },
            new TextBlock { Text = $"Große Liste ({size / 1_000_000} MB) – das dauert beim ersten Öffnen ein paar Sekunden. Trendo bleibt währenddessen bedienbar.", Foreground = Muted, HorizontalAlignment = HorizontalAlignment.Center, TextWrapping = TextWrapping.Wrap, MaxWidth = 520, TextAlignment = TextAlignment.Center },
            new ProgressBar { IsIndeterminate = true, Width = 260, HorizontalAlignment = HorizontalAlignment.Center } } }, 24);
    }
    string? MpvExe => _deps.ResolvePath(DependencyCatalog.Get("mpv"));
    string? FfmpegExe => _deps.ResolvePath(DependencyCatalog.Get("ffmpeg"));

    async Task<byte[]> TvFetch(string url)
    {
        // T-0240: gespeicherte Adressen tragen statt des Passworts einen Platzhalter – erst hier einsetzen
        url = Tv.Reveal(url) ?? throw new InvalidOperationException("Das Passwort für diese Quelle fehlt im sicheren Speicher – bitte die Quelle unter „Quellen“ entfernen und neu anlegen.");
        if (TvFetchOverride is { } o) return await o(url);
        if (!Uri.TryCreate(url, UriKind.Absolute, out var u) || u.Scheme is not ("http" or "https")) throw new InvalidOperationException("Bitte eine Adresse mit http:// oder https:// angeben.");
        using var req = new HttpRequestMessage(HttpMethod.Get, u);
        req.Headers.TryAddWithoutValidation("User-Agent", Xtream.UserAgent); // viele Anbieter lehnen unbekannte Programme ab
        req.Headers.TryAddWithoutValidation("Accept", "*/*");
        using var res = await TvHttp.SendAsync(req, HttpCompletionOption.ResponseHeadersRead);
        res.EnsureSuccessStatusCode();
        if (res.Content.Headers.ContentLength > 400L << 20) throw new InvalidOperationException("Die Datei ist zu groß.");
        return await res.Content.ReadAsByteArrayAsync();
    }

    /// <summary>Quelle hinzufügen und gleich laden: M3U-Adresse, M3U-Datei oder Xtream-Zugang.</summary>
    public async Task<int> TvAddSource(string name, string kind, string urlOrFile, string? epg = null, string? user = null, string? pass = null)
    {
        if (_core.Offline && kind != "datei") { _tvMsg = "Trendo ist offline – zum Laden braucht es Internet."; return 0; }
        // Eine eingefügte get.php-Adresse ist ein Xtream-Zugang: dann Sender, Filme, Serien und Programmführer über die Schnittstelle des Anbieters
        if (kind == "url" && Xtream.FromPlaylistUrl(urlOrFile.Trim()) is { } xt) { kind = "xtream"; urlOrFile = xt.Server; user = xt.User; pass = xt.Pass; if (string.IsNullOrWhiteSpace(name)) name = new Uri(xt.Server).Host; }
        if (kind == "xtream" && (string.IsNullOrWhiteSpace(urlOrFile) || string.IsNullOrWhiteSpace(user) || string.IsNullOrWhiteSpace(pass))) { _tvMsg = "Für Xtream bitte Server, Benutzername und Passwort angeben."; return 0; }
        var url = kind == "xtream" ? Xtream.PlaylistUrl(urlOrFile, user ?? "", pass ?? "") : urlOrFile.Trim();
        if (kind == "xtream") epg ??= Xtream.EpgUrl(urlOrFile, user ?? "", pass ?? "");
        var src = Tv.AddSource(name, kind, kind == "datei" ? Path.GetFullPath(url) : url, string.IsNullOrWhiteSpace(epg) ? null : epg.Trim(), user, kind == "xtream" ? pass : null);
        _core.Audit.Append(Principal.LocalOwner.Name, "tv.source.add", $"{kind} {src.Name}"); // ohne Adresse und Passwort
        return await TvRefreshSource(src);
    }

    public async Task<int> TvRefreshSource(TvSource src)
    {
        _tvMsg = "";
        try
        {
            string text;
            // Auch eine früher als Adresse gespeicherte get.php-Quelle ist ein Xtream-Zugang (schneller: Sender über die Schnittstelle)
            if (Tv.Reveal(src.Url) is not { } realUrl) { _tvMsg = "✗ Das Passwort für diese Quelle fehlt im sicheren Speicher – bitte die Quelle unter „Quellen“ entfernen und neu anlegen."; return 0; }
            var xt = src.Kind is "xtream" or "url" ? Xtream.FromPlaylistUrl(realUrl) : null;
            if (xt is { } x && await TvXtreamLive(src, x) is { } fromApi) text = fromApi;
            else if (src.Kind == "xtream" && _tvMsg.StartsWith('✗')) return 0; // Konto abgelehnt: klare Meldung steht schon da
            else text = src.Kind == "datei" ? await File.ReadAllTextAsync(src.Url) : Encoding.UTF8.GetString(await TvFetch(src.Url));
            var (ch, epgHead) = await Task.Run(() => M3u.Parse(text, src.Id));
            var films = ch.Count(c => c.Kind == "film"); var series = ch.Count(c => c.Kind == "serie");
            if (ch.Count == 0) { _tvMsg = $"In „{src.Name}“ wurden keine Sender gefunden. Ist es eine M3U-Wiedergabeliste?"; return 0; }
            await Task.Run(() => Tv.StorePlaylist(src.Id, text));
            if (src.EpgUrl is null && epgHead is not null) { Tv.SetEpgUrl(src.Id, epgHead); src = src with { EpgUrl = epgHead }; }
            TvReload();
            _tvMsg = $"✓ „{src.Name}“ geladen: {ch.Count - films - series} Sender{(films > 0 ? $", {films} Filme" : "")}{(series > 0 ? $", {series} Serienfolgen" : "")}.";
            if (src.EpgUrl is not null) await TvRefreshEpg(src);
            if (xt is not null) await TvRefreshXtreamVod(src);
            _tvVod = null; _tvSeries = null;
            return ch.Count;
        }
        catch (Exception e) when (e is HttpRequestException or IOException or InvalidOperationException or TaskCanceledException or UnauthorizedAccessException)
        {
            _tvMsg = $"✗ „{src.Name}“ ließ sich nicht laden: {TvWhy(e)}";
            return 0;
        }
    }

    /// <summary>Verständlicher Grund statt technischer Meldung (und nie mit Passwort).</summary>
    static string TvWhy(Exception e) => e switch
    {
        HttpRequestException { StatusCode: System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden } => "Der Anbieter lehnt den Zugang ab – Benutzername, Passwort oder Konto prüfen.",
        HttpRequestException { StatusCode: System.Net.HttpStatusCode.NotFound } => "Die Adresse gibt es beim Anbieter nicht – bitte genau prüfen.",
        HttpRequestException { StatusCode: { } c } when (int)c >= 500 => "Der Server des Anbieters hat gerade ein Problem – später erneut versuchen.",
        HttpRequestException { InnerException: System.Net.Sockets.SocketException } => "Der Server ist nicht erreichbar – Adresse prüfen oder später erneut versuchen.",
        HttpRequestException => "Keine Verbindung zum Anbieter – Internet und Adresse prüfen.",
        TaskCanceledException => "Der Anbieter antwortet nicht (Zeitüberschreitung).",
        _ => Xtream.Mask(e.Message),
    };

    /// <summary>Xtream: Konto prüfen, dann Live-Sender über die Schnittstelle (schneller und zuverlässiger als die große Liste). null = Liste stattdessen laden.</summary>
    async Task<string?> TvXtreamLive(TvSource src, (string Server, string User, string Pass) x)
    {
        string Api(string action) => Xtream.Api(x.Server, x.User, x.Pass, action);
        string info;
        try { info = Encoding.UTF8.GetString(await TvFetch(Xtream.Api(x.Server, x.User, x.Pass, "").Replace("&action=", ""))); }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or InvalidOperationException) { _tvMsg = "✗ " + TvWhy(e); return null; }
        if (Xtream.AccountProblem(info) is { } problem) { _tvMsg = "✗ " + problem; return null; }
        try
        {
            var live = Xtream.ParseLive(Encoding.UTF8.GetString(await TvFetch(Api("get_live_streams"))), Encoding.UTF8.GetString(await TvFetch(Api("get_live_categories"))), x.Server, x.User, x.Pass, src.Id);
            if (live.Count == 0) return null;
            return M3u.Write(live, src.EpgUrl);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException or InvalidOperationException) { return null; }
    }

    public async Task TvRefreshEpg(TvSource src)
    {
        if (src.EpgUrl is null) return;
        try
        {
            var bytes = await TvFetch(src.EpgUrl);
            Stream s = new MemoryStream(bytes);
            if (bytes.Length > 2 && bytes[0] == 0x1F && bytes[1] == 0x8B) s = new GZipStream(s, CompressionMode.Decompress);
            var lib = Tv;
            var ids = await Task.Run(() => lib.Channels().Where(c => c.Id.StartsWith(src.Id + ":") && c.EpgId is not null).Select(c => c.EpgId!).ToHashSet(StringComparer.OrdinalIgnoreCase));
            var progs = await Task.Run(() => Xmltv.Parse(s, DateTime.UtcNow.AddDays(-7), DateTime.UtcNow.AddDays(4), ids.Count > 0 ? ids : null));
            s.Dispose();
            await Task.Run(() => Tv.StoreEpg(src.Id, progs));
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
        if (Tv.Reveal(url) is not { } real) { _tvMsg = "✗ Das Passwort für diese Quelle fehlt im sicheren Speicher – bitte die Quelle unter „Quellen“ entfernen und neu anlegen."; return false; }
        url = real;
        if (TvPlayOverride is { } o) { o(slot, url); TvUpdateNow(); return true; }
        if (MpvExe is null) { _tvMsg = "Zum Abspielen braucht Trendo den freien Player mpv – einmal „Player jetzt einrichten“ klicken."; return false; }
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
        var hidden = Tv.HiddenGroups.ToHashSet();
        var locked = _tvUnlocked ? [] : Tv.LockedGroups.ToHashSet();
        IEnumerable<TvChannel> q = _tvGroup switch
        {
            "Favoriten" => Tv.Favorites.Select(f => all.FirstOrDefault(c => c.Id == f)).OfType<TvChannel>(),
            "Zuletzt" => Tv.Recent.Select(f => all.FirstOrDefault(c => c.Id == f)).OfType<TvChannel>(),
            "Alle" => all.Where(c => !hidden.Contains(c.Group)),
            var g => all.Where(c => c.Group == g),
        };
        if (_tvQuery.Trim() is { Length: > 0 } s) q = q.Where(c => c.Name.Contains(s, StringComparison.OrdinalIgnoreCase) || c.Number.ToString() == s);
        return q.Where(c => !locked.Contains(c.Group) || _tvGroup == c.Group).ToList();
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
            if (Tv.Reveal(c.Url) is not { } recUrl) { Tv.UpdateRecording(r.Id, "Passwort fehlt"); continue; }
            if (TvRecordOverride is { } o) { o(recUrl, file, secs); Tv.UpdateRecording(r.Id, "läuft", file); continue; }
            if (FfmpegExe is null) { Tv.UpdateRecording(r.Id, "ffmpeg fehlt"); continue; }
            var psi = new ProcessStartInfo(FfmpegExe) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = false };
            foreach (var a in new[] { "-hide_banner", "-loglevel", "error", "-y", "-user_agent", Xtream.UserAgent, "-i", recUrl, "-c", "copy", "-t", secs.ToString(), file }) psi.ArgumentList.Add(a);
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
        if (TvLoading() is { } wait) return wait;
        var sp = new StackPanel { Spacing = 12 };
        // Kopfbereich: Gold-Blau mit Zählern (Wunsch 27.10.2026: „viel hochwertiger“)
        var films = TvVodList().Count; var series = TvSeriesList().Count; var live = TvChannels().Count;
        Border Stat(string icon, string value, string label) => new()
        {
            Background = Brush.Parse("#1AFFFFFF"), BorderBrush = Brush.Parse("#33D4A84B"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12), Padding = new Thickness(12, 6), Margin = new Thickness(0, 0, 8, 0),
            Child = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = {
                new TextBlock { Text = icon, FontSize = 18, VerticalAlignment = VerticalAlignment.Center },
                new StackPanel { Children = { new TextBlock { Text = value, Foreground = GoldText(), FontSize = 17, FontWeight = FontWeight.Bold }, new TextBlock { Text = label, Foreground = Muted, FontSize = 10.5 } } } } },
        };
        var hero = new Border
        {
            Name = "tv-hero", CornerRadius = new CornerRadius(16), Padding = new Thickness(20, 14), ClipToBounds = true, BoxShadow = CardShadow,
            Background = new LinearGradientBrush { StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                GradientStops = { new GradientStop(Color.Parse("#FF0E2350"), 0), new GradientStop(Color.Parse("#FF0A1630"), 0.55), new GradientStop(Color.Parse("#FF1B1406"), 1) } },
            BorderBrush = EdgeLit, BorderThickness = new Thickness(1),
            Child = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Children = {
                new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center, Children = {
                    new TextBlock { Text = _tvPlayingTitle is { } pt ? "🎬 " + pt : _tvChannel is not null && TvChannels().FirstOrDefault(c => c.Id == _tvChannel) is { } cur ? "Jetzt: " + cur.Name : "Dein Fernsehen", Foreground = GoldText(), FontSize = 24, FontWeight = FontWeight.Bold, TextTrimming = TextTrimming.CharacterEllipsis },
                    new TextBlock { Text = "Live-TV, Filme und Serien – alles an einem Ort", Foreground = Brush.Parse("#C9D6EA"), FontSize = 13 } } },
                Pos(new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Children = { Stat("📺", live.ToString("N0"), "Sender"), Stat("🎬", films.ToString("N0"), "Filme"), Stat("🍿", series.ToString("N0"), "Serien"), Stat("🕘", DateTime.Now.ToString("HH:mm"), DateTime.Now.ToString("ddd, dd.MM.", System.Globalization.CultureInfo.GetCultureInfo("de-DE"))) } }, 1) } },
        };
        sp.Children.Add(hero);
        if (Tv.Sources.Count > 0) sp.Children.Add(TvAssistantBar()); // „Behalte nur deutsche Sender“ & Co.
        // Reiterleiste aus Glas, der aktive Reiter in Gold
        var tabs = new WrapPanel { Name = "tv-tabs" };
        foreach (var (id, label) in TvTabs)
        {
            var on = _tvTab == id;
            var b = new Button
            {
                Name = "tv-tab-" + id, Content = new TextBlock { Text = label, FontSize = 13.5, FontWeight = on ? FontWeight.SemiBold : FontWeight.Normal, Foreground = on ? Brush.Parse("#1B1406") : Text },
                Margin = new Thickness(0, 0, 6, 0), Padding = new Thickness(16, 8), CornerRadius = new CornerRadius(20),
                Background = on ? new LinearGradientBrush { StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative), GradientStops = { new GradientStop(Color.Parse("#FFF1C870"), 0), new GradientStop(Color.Parse("#FFD4A84B"), 1) } } : Brushes.Transparent,
                BorderThickness = new Thickness(0),
            };
            var tid = id; b.Click += (_, _) => { _tvTab = tid; _tvMsg = ""; Show("tv"); };
            Hover(b); tabs.Children.Add(b);
        }
        sp.Children.Add(new Border { Background = Brush.Parse("#990A1428"), BorderBrush = EdgeLit, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(24), Padding = new Thickness(5), HorizontalAlignment = HorizontalAlignment.Left, Child = tabs });
        if (_tvMsg.Length > 0)
        {
            var ok = _tvMsg.StartsWith('✓');
            sp.Children.Add(new Border { Name = "tv-msg", Background = Brush.Parse(ok ? "#1A22C55E" : "#1AFACC15"), BorderBrush = Brush.Parse(ok ? "#664ADE80" : "#66FACC15"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10), Padding = new Thickness(12, 8),
                Child = new TextBlock { Text = _tvMsg, Foreground = Text, FontSize = 13, TextWrapping = TextWrapping.Wrap } });
        }
        if (Tv.Sources.Count == 0 && _tvTab != "quellen")
        {
            sp.Children.Add(Glass2(new StackPanel { Spacing = 10, Children = {
                new TextBlock { Text = "📡", FontSize = 40 },
                new TextBlock { Text = "Willkommen bei TV & Live", Foreground = GoldText(), FontSize = 20, FontWeight = FontWeight.Bold },
                P("Füge deine Senderliste hinzu – eine M3U-Adresse (auch get.php von Xtream), eine M3U-Datei oder deinen Xtream-Zugang. Trendo bringt keine Sender mit; bitte nur legale Quellen nutzen.", Text, 13),
                TvButton("tv-start-sources", "Senderliste hinzufügen", () => { _tvTab = "quellen"; Show("tv"); }, true) } }, 18));
            return Page("TV & Live", sp);
        }
        sp.Children.Add(_tvTab switch { "film" => TvFilms(), "serie" => TvSeriesView(), "guide" => TvGuide(), "rec" => TvRecordings(), "multi" => TvMultiView(), "quellen" => TvSources(), _ => TvLive() });
        return Page("TV & Live", sp);
    }

    Button TvButton(string name, string text, Action click, bool primary = false)
    {
        var b = new Button { Name = name, Content = text, Padding = new Thickness(14, 7), Margin = new Thickness(0, 0, 8, 8), CornerRadius = new CornerRadius(12),
            Background = primary ? Accent : Brush.Parse("#1FFFFFFF"), BorderBrush = primary ? Brushes.Transparent : Brush.Parse("#33FFFFFF"), BorderThickness = new Thickness(1) };
        b.Click += (_, _) => click();
        Hover(b);
        return b;
    }

    /// <summary>Runder Glas-Knopf mit Symbol (Steuerleiste des Players).</summary>
    Button TvRound(string name, string icon, string tip, Action click, bool gold = false, double size = 46)
    {
        var b = new Button
        {
            Name = name, Width = size, Height = size, Padding = new Thickness(0), CornerRadius = new CornerRadius(size / 2), Margin = new Thickness(0, 0, 8, 0),
            HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center,
            Content = new TextBlock { Text = icon, FontSize = size * 0.4, Foreground = gold ? Brush.Parse("#1B1406") : Brushes.White, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center },
            Background = gold ? new LinearGradientBrush { StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative), GradientStops = { new GradientStop(Color.Parse("#FFF6D98C"), 0), new GradientStop(Color.Parse("#FFD4A84B"), 1) } } : Brush.Parse("#26FFFFFF"),
            BorderBrush = gold ? Brushes.Transparent : Brush.Parse("#40FFFFFF"), BorderThickness = new Thickness(1),
        };
        ToolTip.SetTip(b, tip); Avalonia.Automation.AutomationProperties.SetName(b, tip);
        b.Click += (_, _) => click();
        Hover(b);
        return b;
    }

    Control TvLive()
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("340,*"), Name = "tv-live" };
        // Links: Gruppen, Suche, Sender als edle Karten
        var left = new StackPanel { Spacing = 8, Margin = new Thickness(0, 0, 14, 0) };
        var groups = new List<string> { "Alle", "Favoriten", "Zuletzt" };
        groups.AddRange(TvChannels().Select(c => c.Group).Distinct().OrderBy(g => g));
        var gsel = new ComboBox { Name = "tv-group", ItemsSource = groups, SelectedItem = groups.Contains(_tvGroup) ? _tvGroup : "Alle", HorizontalAlignment = HorizontalAlignment.Stretch, CornerRadius = new CornerRadius(12) };
        gsel.SelectionChanged += (_, _) => { if (gsel.SelectedItem is string g && g != _tvGroup) { _tvGroup = g; Show("tv"); } };
        var search = new TextBox { Name = "tv-search", Text = _tvQuery, PlaceholderText = "🔎  Sender suchen oder Nummer …", CornerRadius = new CornerRadius(12) };
        var list = new StackPanel { Name = "tv-channels", Spacing = 6 };
        void Fill()
        {
            list.Children.Clear();
            var now = DateTime.UtcNow; var progs = TvProgs();
            foreach (var c in TvFiltered().Take(400))
            {
                var (np, _) = TvLibrary.NowNext(progs, c, now);
                var on = c.Id == _tvChannel;
                var logo = new Image { Width = 40, Height = 40, Stretch = Stretch.Uniform };
                TvImage(logo, c.Logo);
                var badge = new Grid { Width = 44, Height = 44, Children = {
                    new Border { CornerRadius = new CornerRadius(12), Background = new RadialGradientBrush { GradientStops = { new GradientStop(Color.Parse("#FF2B4C86"), 0), new GradientStop(Color.Parse("#FF0E1B3A"), 1) } }, BorderBrush = Brush.Parse(on ? "#D4A84B" : "#33D4A84B"), BorderThickness = new Thickness(1) },
                    new TextBlock { Text = c.Number.ToString(), Foreground = GoldText(), FontSize = 15, FontWeight = FontWeight.Bold, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center },
                    logo } };
                var info = new StackPanel { Spacing = 3, Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, Children = {
                    new TextBlock { Text = c.Name + (Tv.Favorites.Contains(c.Id) ? "  ★" : ""), Foreground = Text, FontSize = 13.5, FontWeight = FontWeight.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis } } };
                if (np is not null)
                {
                    info.Children.Add(new TextBlock { Text = $"{np.Start.ToLocalTime():HH:mm}  {np.Title}", Foreground = Muted, FontSize = 11.5, TextTrimming = TextTrimming.CharacterEllipsis });
                    info.Children.Add(new ProgressBar { Value = np.Progress(now) * 100, Height = 3, MinHeight = 3, CornerRadius = new CornerRadius(2), Foreground = Brush.Parse("#D4A84B"), Background = Brush.Parse("#22FFFFFF") });
                }
                var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), Children = { badge, Pos(info, 1) } };
                var b = new Button
                {
                    Name = "tv-ch-" + c.Id.Replace(':', '-'), Content = row, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch,
                    Padding = new Thickness(8, 7), CornerRadius = new CornerRadius(14),
                    Background = on ? new LinearGradientBrush { StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative), GradientStops = { new GradientStop(Color.Parse("#402F86F6"), 0), new GradientStop(Color.Parse("#14D4A84B"), 1) } } : Brush.Parse("#0FFFFFFF"),
                    BorderBrush = on ? Brush.Parse("#D4A84B") : Brush.Parse("#14FFFFFF"), BorderThickness = new Thickness(on ? 1.5 : 1),
                };
                var id = c.Id;
                b.Click += (_, _) => { if (!TvPlay(id)) Show("tv"); else Fill(); };
                Hover(b); list.Children.Add(b);
            }
            if (list.Children.Count == 0) list.Children.Add(P(_tvGroup == "Favoriten" ? "Noch keine Favoriten – beim Sender „☆“ antippen." : "Keine Sender gefunden.", null, 12));
        }
        search.TextChanged += (_, _) => { _tvQuery = search.Text ?? ""; Fill(); };
        Fill();
        left.Children.Add(gsel); left.Children.Add(search);
        left.Children.Add(new ScrollViewer { Content = list, MaxHeight = 660, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        grid.Children.Add(Glass2(left, 10));

        // Rechts: Bild mit Goldrahmen, runde Steuerknöpfe, Jetzt/Danach
        var right = new StackPanel { Spacing = 10, Margin = new Thickness(14, 0, 0, 0) };
        _tvPlayer ??= new MpvView("main");
        if (!TvIsFullscreen) Detach(_tvPlayer); // im Vollbild bleibt das Bild dort
        Control idle = new Grid { Children = {
            new Border { Background = new RadialGradientBrush { GradientStops = { new GradientStop(Color.Parse("#FF16305E"), 0), new GradientStop(Color.Parse("#FF02060F"), 1) } } },
            new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center, Spacing = 8, Children = {
                new TextBlock { Text = "📺", FontSize = 46, HorizontalAlignment = HorizontalAlignment.Center },
                new TextBlock { Text = "Wähle links einen Sender", Foreground = GoldText(), FontSize = 18, FontWeight = FontWeight.SemiBold, HorizontalAlignment = HorizontalAlignment.Center },
                new TextBlock { Text = "↑ ↓ Sender · Leertaste Pause · F Vollbild", Foreground = Muted, FontSize = 12, HorizontalAlignment = HorizontalAlignment.Center } } } } };
        var screen = new Border
        {
            Name = "tv-screen", Background = Brushes.Black, CornerRadius = new CornerRadius(16), ClipToBounds = true, MinHeight = 280, Height = 420,
            BorderBrush = new LinearGradientBrush { StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative), GradientStops = { new GradientStop(Color.Parse("#AAF1C870"), 0), new GradientStop(Color.Parse("#552F86F6"), 1) } },
            BorderThickness = new Thickness(1.5), BoxShadow = BoxShadows.Parse("0 18 40 0 #A0000000, 0 0 30 0 #262F86F6"),
            Child = TvIsFullscreen ? P("Läuft im Vollbild – Esc zum Verkleinern", Brushes.White, 13) : _tvChannel is null && _tvPlayingTitle is null ? idle : _tvPlayer,
        };
        if (MpvExe is null && TvPlayOverride is null)
            screen.Child = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center, Spacing = 8, Children = {
                P("Für das Bild braucht Trendo den freien Player mpv – einmal einrichten, dann läuft alles.", Brushes.White, 13),
                TvButton("tv-get-mpv", "▶ Player jetzt einrichten", () => _ = TvSetupPlayer(), true) } };
        right.Children.Add(screen);
        var vol = new Slider { Name = "tv-volume", Minimum = 0, Maximum = 130, Value = 100, Width = 130, VerticalAlignment = VerticalAlignment.Center };
        vol.ValueChanged += (_, _) => _tvPlayer?.Send("set_property", "volume", Math.Round(vol.Value));
        void Ctrl(params object[] cmd) { if (_tvPlayer?.Send(cmd) != true && TvPlayOverride is null) { _tvMsg = "Erst einen Sender wählen."; Show("tv"); } }
        _tvFavBtn = TvButton("tv-fav", "☆ Favorit", () =>
        {
            if (_tvChannel is null) { _tvMsg = "Erst einen Sender wählen."; Show("tv"); return; }
            var on = Tv.ToggleFavorite(_tvChannel); _tvFavBtn!.Content = on ? "★ Favorit" : "☆ Favorit"; Fill();
        });
        _tvFavBtn.Margin = new Thickness(8, 0, 0, 0);
        var rec = TvButton("tv-rec-now", "⏺ Aufnehmen", () =>
        {
            if (_tvChannel is null || TvChannels().FirstOrDefault(x => x.Id == _tvChannel) is not { } c) { _tvMsg = "Erst einen Sender wählen."; Show("tv"); return; }
            var (np, _) = TvLibrary.NowNext(TvProgs(), c, DateTime.UtcNow);
            TvPlanRecording(c, np?.Title ?? c.Name, DateTime.UtcNow, np?.Stop ?? DateTime.UtcNow.AddHours(1));
            _tvMsg = $"✓ Aufnahme läuft: {np?.Title ?? c.Name} (bis {(np?.Stop ?? DateTime.UtcNow.AddHours(1)).ToLocalTime():HH:mm})."; Show("tv");
        });
        rec.Margin = new Thickness(0);
        var bar = new Grid { Name = "tv-controls", ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), Children = {
            new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Children = {
                TvRound("tv-prev", "⏮", "Vorheriger Sender (↑)", () => TvZap(-1)),
                TvRound("tv-back", "⏪", "30 Sekunden zurück", () => Ctrl("seek", -30)),
                TvRound("tv-pause", "⏯", "Pause / Weiter (Leertaste)", () => Ctrl("cycle", "pause"), gold: true, size: 56),
                TvRound("tv-fwd", "⏩", "30 Sekunden vor", () => Ctrl("seek", 30)),
                TvRound("tv-next", "⏭", "Nächster Sender (↓)", () => TvZap(1)),
                TvRound("tv-live-now", "●", "Zurück zu Live", () => Ctrl("seek", 100, "absolute-percent")) } },
            Pos(new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Spacing = 6, Children = {
                new TextBlock { Text = "🔊", Foreground = Text, VerticalAlignment = VerticalAlignment.Center }, vol } }, 1),
            Pos(new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Children = { rec, _tvFavBtn,
                TvRound("tv-full", "⛶", "Vollbild (F)", () => TvFullscreen(true)) } }, 2) } };
        _tvFavBtn.Margin = new Thickness(8, 0, 8, 0);
        right.Children.Add(Glass2(bar, 10));
        _tvNowTitle = new TextBlock { Name = "tv-now-title", Foreground = GoldText(), FontSize = 20, FontWeight = FontWeight.Bold, Text = "Kein Sender gewählt" };
        _tvNowInfo = new TextBlock { Name = "tv-now-info", Foreground = Text, FontSize = 14, TextWrapping = TextWrapping.Wrap };
        _tvNextInfo = new TextBlock { Name = "tv-next-info", Foreground = Muted, FontSize = 12.5, TextWrapping = TextWrapping.Wrap };
        _tvNowBar = new ProgressBar { Name = "tv-now-bar", Height = 5, MinHeight = 5, CornerRadius = new CornerRadius(3), Foreground = Brush.Parse("#D4A84B"), Background = Brush.Parse("#22FFFFFF") };
        right.Children.Add(Glass2(new StackPanel { Spacing = 6, Children = { _tvNowTitle, _tvNowInfo, _tvNowBar, _tvNextInfo,
            P("Zeitversetzt: Pause und ⏪ spulen bis etwa 30 Minuten zurück. Sendungen von früher findest du im Programmführer, wenn dein Anbieter das anbietet.", null, 11) } }, 14));
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
                    P($"{count} Sender · {(src.Kind == "xtream" ? "Xtream · " + (Xtream.FromPlaylistUrl(src.Url)?.Server ?? "") : src.Kind == "datei" ? "Datei" : Xtream.Mask(src.Url))}{(src.EpgUrl is null ? " · ohne Programmführer" : " · mit Programmführer")}", null, 11.5) } },
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
