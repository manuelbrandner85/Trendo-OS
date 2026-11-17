using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Trendo.App;
using Trendo.Core;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// <summary>T-0746 „Live streamen und TV: echte Läufe mit lokalem Teststream“.
/// Mit <c>TRENDO_ECHT_STREAM=&lt;Belegordner&gt;</c> laufen zwei Echtläufe mit den echten Werkzeugen dieses PCs – nur auf 127.0.0.1, nie zu einem
/// externen Dienst, ohne Konto und ohne echten Schlüssel:
/// <list type="bullet">
/// <item>SPEC-15: Die App sendet über „Live anfragen“ → „Ja, jetzt senden“ an einen lokalen RTMP-Empfänger (FFmpeg, -listen 1), der aufzeichnet.
/// Der erste Empfänger beendet nach 8 s, ein zweiter übernimmt denselben Port – die App verbindet selbst neu; „Stopp“ beendet die Sendung.
/// ffprobe liest beide Aufzeichnungen und die Kopien der App (Dauer, Bitrate, Bildgröße).</item>
/// <item>STUDIO-TV-01: FFmpeg erzeugt einen HLS-Teststream (Testbild + Ton) in einen Temp-Ordner, ein kleiner HTTP-Server dieses Tests liefert ihn auf
/// 127.0.0.1 aus. Die App lädt die Senderliste über „Hinzufügen &amp; laden“, spielt den Sender (mpv mit genau den Einstellungen der App, ohne Fenster)
/// und nimmt ihn über „Aufnehmen“/„Beenden“ auf; ffprobe liest die Aufnahme.</item>
/// </list>
/// Ohne Umgebungsvariable: übersprungen (ehrlich, nicht still bestanden). Fehlt ein Werkzeug: BLOCKIERT im Beleg und Testfehler.
/// Alle gestarteten Prozesse gehören diesem Test und werden am Ende beendet; geprüft wird, dass kein eigener Port mehr offen ist.</summary>
public class T0746_StreamTv_Echt_Ui
{
    const string SkipText = "Übersprungen – Echtlauf nur mit TRENDO_ECHT_STREAM=<Belegordner>.";
    static string? BelegDir => Environment.GetEnvironmentVariable("TRENDO_ECHT_STREAM") is { Length: > 0 } d ? Directory.CreateDirectory(d).FullName : null;

    static string? Tool(string name, params string[] own)
    {
        if (Cli.Find(name) is { } p) return p;
        var f = Path.Combine([DataLocation.ToolsRoot(), .. own]);
        return File.Exists(f) ? f : null;
    }

    /// <summary>Das Testfenster erkennt absichtlich nur Programme im eigenen Datenordner (Isolated). Damit die App dieselben Werkzeuge findet wie installiert,
    /// verweist der Test mit einer Verzeichnisverknüpfung (Junction, ohne Adminrechte) aus ihrem Werkzeugordner auf die echten Ordner – die Programme bleiben unverändert.</summary>
    static List<string> LinkTools(MainWindow w, params (string Rel, string Target)[] links)
    {
        var made = new List<string>();
        foreach (var (rel, target) in links)
        {
            var link = Path.Combine(w.Deps.ToolsRoot, rel);
            Directory.CreateDirectory(Path.GetDirectoryName(link)!);
            var r = Cli.Run("cmd.exe", ["/c", "mklink", "/J", link, target]);
            Assert.True(r.Ok && Directory.Exists(link), "Verknüpfung " + rel + ": " + r.Out + r.Err);
            made.Add(link);
        }
        return made;
    }

    static void Unlink(IEnumerable<string> links) { foreach (var l in links) try { Directory.Delete(l, false); } catch (IOException) { } catch (UnauthorizedAccessException) { } }

    static string Sha(string file) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file))).ToLowerInvariant();

    /// <summary>Beleg mit LF, UTF-8 ohne BOM, anonymisiert (Benutzerordner, Benutzer- und Rechnername) und in SHA256SUMS.</summary>
    static void Beleg(string dir, string name, string text)
    {
        File.WriteAllText(Path.Combine(dir, name), T0738_KiEchtlaeufe_Ui.Anon(T0738_KiEchtlaeufe_Ui.NoAnsi(text)), new UTF8Encoding(false));
        var sums = Path.Combine(dir, "SHA256SUMS");
        var lines = File.Exists(sums) ? File.ReadAllLines(sums).Where(l => l.Length > 0 && !l.EndsWith("  " + name, StringComparison.Ordinal)).ToList() : [];
        lines.Add(Sha(Path.Combine(dir, name)) + "  " + name);
        File.WriteAllText(sums, string.Join("\n", lines.OrderBy(l => l[66..], StringComparer.Ordinal)) + "\n", new UTF8Encoding(false));
    }

    static int FreePort() { var l = new TcpListener(IPAddress.Loopback, 0); l.Start(); var p = ((IPEndPoint)l.LocalEndpoint).Port; l.Stop(); return p; }

    static bool PortOpen(int port)
    {
        try { using var c = new TcpClient(); return c.ConnectAsync(IPAddress.Loopback, port).Wait(500) && c.Connected; }
        catch (Exception e) when (e is SocketException or AggregateException) { return false; }
    }

    static void WaitUntil(Func<bool> done, TimeSpan max, string what)
    {
        var sw = Stopwatch.StartNew();
        while (!done())
        {
            Assert.True(sw.Elapsed < max, what + " dauert länger als " + max);
            Thread.Sleep(200); Settle();
        }
    }

    /// <summary>Eigener Hilfsprozess (Empfänger, Teststream, mpv). Stopp schickt „q“, wartet und beendet sonst hart.</summary>
    sealed class Own : IDisposable
    {
        readonly Process _p; readonly StringBuilder _err = new();
        public string Name { get; }
        public Own(string name, string exe, IEnumerable<string> args)
        {
            Name = name;
            var psi = new ProcessStartInfo(exe) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true, RedirectStandardOutput = true, RedirectStandardInput = true };
            foreach (var a in args) psi.ArgumentList.Add(a);
            _p = Process.Start(psi) ?? throw new InvalidOperationException(name + " startet nicht");
            _p.ErrorDataReceived += (_, e) => { if (e.Data is { } d) lock (_err) _err.Append(d).Append('\n'); };
            _p.OutputDataReceived += (_, e) => { if (e.Data is { } d) lock (_err) _err.Append(d).Append('\n'); };
            _p.BeginErrorReadLine(); _p.BeginOutputReadLine();
        }
        public bool Running => !_p.HasExited;
        public int? Exit => _p.HasExited ? _p.ExitCode : null;
        public int Pid => _p.Id;
        public string Log { get { lock (_err) return _err.ToString(); } }
        public bool WaitExit(TimeSpan t) => _p.WaitForExit((int)t.TotalMilliseconds);
        public void Dispose()
        {
            try
            {
                if (!_p.HasExited) { try { _p.StandardInput.Write("q"); _p.StandardInput.Flush(); } catch (IOException) { } }
                if (!_p.WaitForExit(5000)) _p.Kill(true);
                _p.WaitForExit(5000);
            }
            catch (InvalidOperationException) { }
        }
    }

    /// <summary>Kleiner HTTP-Server nur auf 127.0.0.1 für den HLS-Teststream (GET, ein Ordner, keine Unterordner außerhalb).</summary>
    sealed class MiniHttp : IDisposable
    {
        readonly TcpListener _l = new(IPAddress.Loopback, 0);
        readonly CancellationTokenSource _cts = new();
        readonly string _dir;
        public ConcurrentQueue<string> Requests { get; } = new();
        public int Port { get; }
        public MiniHttp(string dir)
        {
            _dir = Path.GetFullPath(dir).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            _l.Start(); Port = ((IPEndPoint)_l.LocalEndpoint).Port;
            _ = Task.Run(Loop);
        }
        async Task Loop()
        {
            while (!_cts.IsCancellationRequested)
            {
                TcpClient c;
                try { c = await _l.AcceptTcpClientAsync(_cts.Token); } catch (Exception e) when (e is OperationCanceledException or SocketException or ObjectDisposedException) { return; }
                _ = Task.Run(() => Serve(c));
            }
        }
        async Task Serve(TcpClient c)
        {
            using (c)
            {
                try
                {
                    var s = c.GetStream();
                    using var r = new StreamReader(s, Encoding.ASCII, false, 2048, leaveOpen: true);
                    var line = await r.ReadLineAsync() ?? "";
                    while (await r.ReadLineAsync() is { Length: > 0 }) { }
                    var parts = line.Split(' ');
                    var rel = parts.Length > 1 ? Uri.UnescapeDataString(parts[1].Split('?')[0]).TrimStart('/') : "";
                    var full = Path.GetFullPath(Path.Combine(_dir, rel));
                    byte[]? body = null;
                    if (parts[0] == "GET" && full.StartsWith(_dir, StringComparison.OrdinalIgnoreCase))
                        for (var i = 0; i < 5 && body is null; i++)
                            try { if (File.Exists(full)) { using var f = new FileStream(full, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete); using var m = new MemoryStream(); await f.CopyToAsync(m); body = m.ToArray(); } else break; }
                            catch (IOException) { await Task.Delay(50); }
                    var type = Path.GetExtension(full).ToLowerInvariant() switch { ".m3u8" => "application/vnd.apple.mpegurl", ".m3u" => "audio/x-mpegurl", ".ts" => "video/mp2t", _ => "application/octet-stream" };
                    var head = body is null ? "HTTP/1.1 404 Not Found\r\nContent-Length: 0\r\nConnection: close\r\n\r\n"
                        : $"HTTP/1.1 200 OK\r\nContent-Type: {type}\r\nContent-Length: {body.Length}\r\nCache-Control: no-cache\r\nConnection: close\r\n\r\n";
                    await s.WriteAsync(Encoding.ASCII.GetBytes(head));
                    if (body is not null) await s.WriteAsync(body);
                    Requests.Enqueue($"{DateTime.Now:HH:mm:ss.fff} {parts[0]} /{rel} → {(body is null ? "404" : "200 " + body.Length + " Bytes")}");
                }
                catch (Exception e) when (e is IOException or SocketException or ObjectDisposedException) { }
            }
        }
        public void Dispose() { _cts.Cancel(); _l.Stop(); }
    }

    static string Probe(string ffprobe, string file)
    {
        var r = Cli.Run(ffprobe, ["-v", "error", "-show_entries", "format=format_name,duration,bit_rate,size:stream=codec_type,codec_name,width,height,r_frame_rate,sample_rate,channels", "-of", "default=noprint_wrappers=0", file], timeoutMs: 60_000);
        Assert.True(r.Ok, "ffprobe: " + r.Err);
        return r.Out.Replace("\r\n", "\n");
    }

    static double Val(string probe, string key)
    {
        var line = probe.Split('\n').FirstOrDefault(l => l.StartsWith(key + "=", StringComparison.Ordinal));
        return line is not null && double.TryParse(line[(key.Length + 1)..], NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : -1;
    }

    // ---------------------------------------------------------------- SPEC-15 Live streamen

    [AvaloniaFact(DisplayName = "T-0746 ECHT Live streamen: App sendet an lokalen RTMP-Empfänger (127.0.0.1), verbindet nach Abbruch neu, ffprobe liest die Aufzeichnung (nur mit TRENDO_ECHT_STREAM)")]
    public void Live_echt()
    {
        if (BelegDir is not { } beleg) { Assert.Skip(SkipText); return; }
        var log = new StringBuilder();
        log.Append($"T-0746 Echtlauf „Live streamen“ (SPEC-15) {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}\nZiel: nur 127.0.0.1 (lokaler Empfänger), kein externer Dienst, kein Konto, Probe-Schlüssel ohne Bedeutung.\n");
        var ff = Tool("ffmpeg", "ffmpeg", "bin", "ffmpeg.exe"); var fp = Tool("ffprobe", "ffmpeg", "bin", "ffprobe.exe");
        if (ff is null || fp is null)
        {
            Beleg(beleg, "live-lauf.log", log + "BLOCKIERT: FFmpeg/ffprobe fehlen auf diesem PC (PATH oder %LOCALAPPDATA%\\TrendoWerkzeuge\\ffmpeg\\bin).\n");
            Assert.Fail("BLOCKIERT: FFmpeg fehlt");
        }
        log.Append($"FFmpeg (Empfänger und ffprobe): {ff}\n{Cli.Run(ff!, ["-version"]).Out.Split('\n')[0].Trim()}\n");
        var work = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "trendo-t0746-live-" + Guid.NewGuid().ToString("N")[..8])).FullName;
        var port = FreePort();
        const string key = "trendo-probe-0746";
        var target = $"rtmp://127.0.0.1:{port}/live";
        var own = new List<Own>();
        Own Receiver(int n) => new($"Empfänger {n}", ff!, ["-hide_banner", "-y", "-listen", "1", "-i", $"{target}/{key}", .. (n == 1 ? new[] { "-t", "8" } : []), "-c", "copy", Path.Combine(work, $"empfang-{n}.flv")]);
        var (w, _, root) = Ui.Open();
        var links = new List<string>();
        try
        {
            links = LinkTools(w, (Path.Combine("ffmpeg", "bin"), Path.GetDirectoryName(ff!)!)); // App sucht FFmpeg über ihre Werkzeugprüfung – nichts wird eingerichtet
            var r1 = Receiver(1); own.Add(r1);
            Thread.Sleep(1500); // nicht mit einer Probeverbindung prüfen: „-listen 1“ nimmt genau eine Verbindung an
            Assert.True(r1.Running, "Empfänger 1: " + r1.Log);
            log.Append($"Empfänger 1 lauscht auf {target}/… (beendet nach 8 s Material, prüft das Neuverbinden)\n");

            w.Show("sendung"); Settle();
            w.Find<ComboBox>("live-platform").SelectedIndex = 2; Settle(); // „Eigenes RTMP“
            w.Find<TextBox>("live-rtmp").Text = target;
            w.Find<TextBox>("live-key").Text = key;
            w.Find<CheckBox>("live-copy").IsChecked = true;
            w.Find<Button>("live-ask").Press(); Settle();
            Assert.Single(w.Core.Approvals.Pending());
            log.Append($"Klick „Live anfragen …“ → Status: {w.Find<TextBlock>("live-status").Text}\n");
            var sw = Stopwatch.StartNew();
            w.Find<Button>("live-yes").Press();
            WaitUntil(() => w.Live.Mode == LiveMode.Broadcasting, TimeSpan.FromSeconds(30), "Sendung startet");
            var args = w.Live.LastArguments;
            Assert.Contains(args, a => a == $"{target}/{key}");
            Assert.DoesNotContain(args, a => a.StartsWith("rtmp", StringComparison.OrdinalIgnoreCase) && !a.StartsWith("rtmp://127.0.0.1:", StringComparison.Ordinal)); // nie extern
            log.Append($"Klick „Ja, jetzt senden“ → Sendung läuft nach {sw.Elapsed.TotalSeconds:0.0} s\nAufruf der App (FFmpeg): {string.Join(' ', args.Select(a => a.Contains(' ') ? $"\"{a}\"" : a))}\n\n--- Statusanzeige der App (bei jeder Änderung) ---\n");

            var seen = new List<string>();
            void Sample() { w.LiveTick(); var t = w.Find<TextBlock>("live-status").Text ?? ""; if (seen.Count == 0 || seen[^1] != t) { seen.Add(t); log.Append($"{sw.Elapsed.TotalSeconds,6:0.0} s  {t}\n"); } }
            var watch = Stopwatch.StartNew();
            while (r1.Running && watch.Elapsed < TimeSpan.FromSeconds(40)) { Thread.Sleep(250); Settle(); Sample(); }
            Assert.False(r1.Running, "Empfänger 1 endet nicht");
            var r2 = Receiver(2); own.Add(r2); // übernimmt denselben Port – die App muss selbst neu verbinden
            WaitUntil(() => { Sample(); return w.Live.Retries >= 1 && !w.Live.Retrying && w.Live.Stats is not null; }, TimeSpan.FromSeconds(40), "Neuverbinden (zuletzt: " + w.Live.LastLog + ")");
            var until = Stopwatch.StartNew();
            while (until.Elapsed < TimeSpan.FromSeconds(8)) { Thread.Sleep(250); Settle(); Sample(); }
            Assert.Equal(LiveMode.Broadcasting, w.Live.Mode);
            var statsBeforeStop = w.Live.Stats;
            w.Find<Button>("live-stop").Press(); Settle(); Sample();
            Assert.Equal(LiveMode.Idle, w.Live.Mode);
            Assert.True(r2.WaitExit(TimeSpan.FromSeconds(20)), "Empfänger 2 endet nach Stopp nicht");
            sw.Stop();
            Assert.Contains(seen, t => t.Contains("Neuer Versuch in 2 s (1 von 3)", StringComparison.Ordinal));
            Assert.Contains(seen, t => t.Contains("Wieder verbunden (1 von 3)", StringComparison.Ordinal) && t.Contains("Gesendet: ", StringComparison.Ordinal));
            Assert.All(seen, t => Assert.DoesNotContain(key, t));
            log.Append($"\nLetzte Anzeige vor Stopp: {statsBeforeStop}\nNeuverbindungen: {w.Live.Retries}\nFFmpeg-Zeilen beim Abbruch (LastLog der App, ohne Schlüssel):\n{w.Live.LastLog}\n");

            log.Append("\n--- ffprobe der Aufzeichnungen des Empfängers ---\n");
            foreach (var n in new[] { 1, 2 })
            {
                var f = Path.Combine(work, $"empfang-{n}.flv");
                Assert.True(File.Exists(f), $"Aufzeichnung {n} fehlt");
                var pr = Probe(fp!, f);
                log.Append($"empfang-{n}.flv ({new FileInfo(f).Length} Bytes, SHA-256 {Sha(f)}):\n{pr}\n");
                Assert.Contains("codec_name=h264", pr); Assert.Contains("width=1280", pr); Assert.Contains("height=720", pr);
                Assert.True(Val(pr, "duration") >= 4, $"Aufzeichnung {n} zu kurz");
                Assert.True(Val(pr, "bit_rate") > 50_000, $"Aufzeichnung {n} ohne Bitrate");
            }
            log.Append("--- ffprobe der Kopien auf diesem PC (je Verbindung eine Datei) ---\n");
            Assert.Equal(2, w.Live.CopyFiles.Count);
            foreach (var c in w.Live.CopyFiles)
            {
                Assert.True(File.Exists(c), "Kopie fehlt: " + c);
                var pr = Probe(fp!, c);
                log.Append($"{Path.GetFileName(c)} ({new FileInfo(c).Length} Bytes):\n{pr}\n");
                Assert.Contains("codec_name=h264", pr);
                Assert.True(Val(pr, "duration") >= 4, "Kopie zu kurz: " + c);
            }
            Assert.DoesNotContain(key, string.Join("\n", Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).Where(f => !f.EndsWith(".mp4") && !f.EndsWith(".jpg")).Select(File.ReadAllText)));
            log.Append($"Schlüssel steht in keiner Datei der App (Datenordner geprüft).\nGesamtdauer vom Klick bis Stopp: {sw.Elapsed.TotalSeconds:0.0} s\n");
            log.Append("Bildinhalte (Bildschirmaufnahme) werden nicht als Beleg abgelegt – nur ihre ffprobe-Werte.\n");
        }
        finally
        {
            try { if (w.Live.Mode != LiveMode.Idle) w.Live.Stop(); } catch (InvalidOperationException) { }
            w.Close();
            Unlink(links);
            foreach (var o in own) o.Dispose();
            foreach (var o in own) log.Append($"\n--- {o.Name} (FFmpeg, Exit {o.Exit?.ToString(CultureInfo.InvariantCulture) ?? "läuft"}, letzte Zeilen) ---\n{Tail(o.Log)}\n");
            if (w.Live.LastLog.Length > 0) log.Append($"\n--- Sender der App beim letzten Ende (LastLog, ohne Schlüssel) ---\n{w.Live.LastLog}\n");
            var open = PortOpen(port);
            log.Append($"\nAufräumen: eigene Prozesse beendet ({string.Join(", ", own.Select(o => $"{o.Name} Exit {o.Exit?.ToString(CultureInfo.InvariantCulture) ?? "läuft"}"))}); Port {port} offen: {(open ? "JA" : "nein")}\n");
            Beleg(beleg, "live-lauf.log", log.ToString());
            try { Directory.Delete(work, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            Assert.False(open, "Port " + port + " ist noch offen");
        }
    }

    static string Tail(string s) { s = s.Replace("\r", "\n"); var lines = s.Split('\n', StringSplitOptions.RemoveEmptyEntries); return string.Join("\n", lines.TakeLast(14)); }

    // ---------------------------------------------------------------- STUDIO-TV-01 TV & Live

    [AvaloniaFact(DisplayName = "T-0746 ECHT TV & Live: lokaler HLS-Teststream (127.0.0.1) → Senderliste laden, abspielen (mpv), aufnehmen, ffprobe liest die Aufnahme (nur mit TRENDO_ECHT_STREAM)")]
    public void Tv_echt()
    {
        if (BelegDir is not { } beleg) { Assert.Skip(SkipText); return; }
        var log = new StringBuilder();
        log.Append($"T-0746 Echtlauf „TV & Live“ (STUDIO-TV-01) {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}\nQuelle: eigener HLS-Teststream (FFmpeg-Testbild, 440-Hz-Ton) auf 127.0.0.1 – kein Sender, kein Internet.\n");
        var ff = Tool("ffmpeg", "ffmpeg", "bin", "ffmpeg.exe"); var fp = Tool("ffprobe", "ffmpeg", "bin", "ffprobe.exe"); var mpv = Tool("mpv", "mpv", "mpv.exe");
        var missing = new[] { ("FFmpeg", ff), ("ffprobe", fp), ("mpv", mpv) }.Where(x => x.Item2 is null).Select(x => x.Item1).ToList();
        if (missing.Count > 0)
        {
            Beleg(beleg, "tv-lauf.log", log + "BLOCKIERT: fehlt auf diesem PC: " + string.Join(", ", missing) + " (PATH oder %LOCALAPPDATA%\\TrendoWerkzeuge).\n");
            Assert.Fail("BLOCKIERT: " + string.Join(", ", missing));
        }
        log.Append($"FFmpeg: {ff}\nmpv: {mpv}\n{Cli.Run(mpv!, ["--version"]).Out.Split('\n')[0].Trim()}\n");
        var work = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "trendo-t0746-tv-" + Guid.NewGuid().ToString("N")[..8])).FullName;
        var hls = Directory.CreateDirectory(Path.Combine(work, "hls")).FullName;
        var own = new List<Own>();
        using var http = new MiniHttp(hls);
        var url = $"http://127.0.0.1:{http.Port}/index.m3u8";
        File.WriteAllText(Path.Combine(hls, "liste.m3u"), $"#EXTM3U\n#EXTINF:-1 tvg-id=\"probe\" group-title=\"Probe\",Trendo Teststream\n{url}\n", new UTF8Encoding(false));
        var gen = new Own("Teststream (HLS)", ff!, ["-hide_banner", "-loglevel", "warning", "-y", "-re", "-f", "lavfi", "-i", "testsrc2=size=640x360:rate=25", "-f", "lavfi", "-i", "sine=frequency=440:sample_rate=48000",
            "-c:v", "libx264", "-preset", "veryfast", "-g", "50", "-b:v", "800k", "-pix_fmt", "yuv420p", "-c:a", "aac", "-b:a", "96k",
            "-f", "hls", "-hls_time", "2", "-hls_list_size", "6", "-hls_flags", "delete_segments", Path.Combine(hls, "index.m3u8")]);
        own.Add(gen);
        var (w, _, root) = Ui.Open();
        var links = new List<string>();
        try
        {
            WaitUntil(() => Directory.GetFiles(hls, "*.ts").Length >= 2 && File.Exists(Path.Combine(hls, "index.m3u8")), TimeSpan.FromSeconds(30), "HLS-Segmente");
            log.Append($"HTTP-Server dieses Tests: http://127.0.0.1:{http.Port}/ (Ordner mit index.m3u8, liste.m3u)\n\n");
            links = LinkTools(w, (Path.Combine("ffmpeg", "bin"), Path.GetDirectoryName(ff!)!), ("mpv", Path.GetDirectoryName(mpv!)!)); // die App findet FFmpeg und mpv über ihre Werkzeugprüfung

            // Quelle über die Oberfläche anlegen und laden
            w.Show("tv"); Settle();
            w.Find<Button>("tv-start-sources").Press(); Settle();
            w.Find<TextBox>("tv-src-name").Text = "Teststream";
            w.Find<TextBox>("tv-src-url").Text = $"http://127.0.0.1:{http.Port}/liste.m3u";
            w.Find<Button>("tv-src-add").Press();
            WaitUntil(() => w.Tv.Channels().Count == 1, TimeSpan.FromSeconds(20), "Senderliste laden");
            log.Append($"Klick „Hinzufügen & laden“ → Meldung: {w.TvMessage}\n");
            var ch = w.Tv.Channels()[0];
            Assert.Equal(url, ch.Url);

            // Abspielen: Klick auf den Sender
            w.Show("tv"); w.Find<Button>("tv-tab-live").Press(); Settle();
            w.Find<Button>("tv-ch-" + ch.Id.Replace(':', '-')).Press(); Settle();
            Assert.True(url == w.TvPlayerUrl, $"Player spielt nicht: {w.TvPlayerUrl ?? "nichts"} – Meldung: {w.TvMessage} – mpv: {w.Deps.ResolvePath(DependencyCatalog.Get("mpv")) ?? "nicht gefunden"}");
            Assert.DoesNotContain("✗", w.TvMessage);
            log.Append($"Klick auf „{ch.Name}“ → Player der App spielt: {w.TvPlayerUrl}\n");
            // Das Testfenster hat kein echtes Fenster für mpv (--wid). Darum startet der Test mpv mit genau den Einstellungen der App ohne Bildausgabe.
            var mpvLog = Path.Combine(work, "mpv.log");
            var mArgs = MpvView.Args(null, "trendo-t0746-" + Environment.ProcessId, true, w.TvPlayerUrl!)
                .Where(a => a is not ("--force-window=yes" or "--idle=yes" or "--keep-open=yes")).ToList();
            mArgs.InsertRange(0, ["--vo=null", "--ao=null", "--length=5", "--log-file=" + mpvLog]);
            var play = Cli.Run(mpv!, mArgs, timeoutMs: 60_000);
            var mlines = File.ReadAllLines(mpvLog).Where(l => l.Contains("[cplayer] VO:") || l.Contains("[cplayer] AO:") || l.Contains("playback restart complete") || l.Contains("EOF code") || l.Contains("h264: Reinit context") || l.Contains("Opening done")).ToList();
            log.Append($"mpv ({string.Join(' ', mArgs.Select(a => a.StartsWith("--log-file", StringComparison.Ordinal) ? "--log-file=<temp>" : a))}) Exit {play.Exit}\n{string.Join("\n", mlines)}\n");
            Assert.Equal(0, play.Exit);
            Assert.Contains(mlines, l => l.Contains("VO: [null] 640x360", StringComparison.Ordinal));
            Assert.Contains(mlines, l => l.Contains("video=playing", StringComparison.Ordinal));

            // Aufnehmen über die Oberfläche
            w.Show("tv"); Settle();
            var sw = Stopwatch.StartNew();
            w.Find<Button>("tv-rec-now").Press(); Settle();
            WaitUntil(() => w.Tv.Recordings.Any(r => r.State == "läuft"), TimeSpan.FromSeconds(10), "Aufnahme startet");
            var rec = w.Tv.Recordings.Single();
            log.Append($"\nKlick „Aufnehmen“ → Aufnahme „{rec.Title}“: {rec.State}, Datei {Path.GetFileName(rec.File)}\n");
            Thread.Sleep(9000); Settle();
            w.Show("tv"); w.Find<Button>("tv-tab-rec").Press(); Settle();
            w.Find<Button>("tv-rec-stop-" + rec.Id).Press(); Settle();
            WaitUntil(() => w.Tv.Recordings.Single().State is "fertig" or "fehlgeschlagen", TimeSpan.FromSeconds(15), "Aufnahme endet");
            sw.Stop();
            rec = w.Tv.Recordings.Single();
            log.Append($"Klick „Beenden“ nach {sw.Elapsed.TotalSeconds:0.0} s → Zustand: {rec.State}\n");
            Assert.Equal("fertig", rec.State);
            Assert.True(File.Exists(rec.File), "Aufnahmedatei fehlt");
            var pr = Probe(fp!, rec.File!);
            log.Append($"\n--- ffprobe der Aufnahme ({new FileInfo(rec.File!).Length} Bytes, SHA-256 {Sha(rec.File!)}) ---\n{pr}\n");
            Assert.Contains("codec_name=h264", pr); Assert.Contains("codec_name=aac", pr);
            Assert.Contains("width=640", pr); Assert.Contains("height=360", pr);
            Assert.True(Val(pr, "duration") >= 5, "Aufnahme zu kurz");
            var copy = Path.Combine(beleg, "tv-aufnahme-teststream.ts");
            File.Copy(rec.File!, copy, true); // nur Testbild und Sinuston aus FFmpeg – kein fremder Inhalt
            var sums = Path.Combine(beleg, "SHA256SUMS");
            var lines = File.Exists(sums) ? File.ReadAllLines(sums).Where(l => l.Length > 0 && !l.EndsWith("  tv-aufnahme-teststream.ts", StringComparison.Ordinal)).ToList() : [];
            lines.Add(Sha(copy) + "  tv-aufnahme-teststream.ts");
            File.WriteAllText(sums, string.Join("\n", lines.OrderBy(l => l[66..], StringComparer.Ordinal)) + "\n", new UTF8Encoding(false));
        }
        finally
        {
            w.Close();
            Unlink(links);
            foreach (var o in own) o.Dispose();
            http.Dispose();
            var open = PortOpen(http.Port);
            log.Append($"\n--- Anfragen an den HTTP-Server (Auszug) ---\n{string.Join("\n", http.Requests.Take(6))}\n… insgesamt {http.Requests.Count} Anfragen\n");
            log.Append($"\nAufräumen: eigene Prozesse beendet ({string.Join(", ", own.Select(o => $"{o.Name} Exit {o.Exit?.ToString(CultureInfo.InvariantCulture) ?? "läuft"}"))}); Port {http.Port} offen: {(open ? "JA" : "nein")}\n");
            Beleg(beleg, "tv-lauf.log", log.ToString());
            try { Directory.Delete(work, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            Assert.False(open, "Port " + http.Port + " ist noch offen");
        }
    }
}
