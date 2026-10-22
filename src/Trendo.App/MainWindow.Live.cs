using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Trendo.Core;

namespace Trendo.App;

/// <summary>SPEC-15: Quellen wählen, Vorschau, lokale Aufnahme, Sendung nur nach Freigabe.</summary>
public partial class MainWindow
{
    private List<StreamSource> _liveSources = [];
    private readonly HashSet<string> _livePick = [];
    private string? _livePreview;
    private string? _liveFile;
    private string _liveKey = "";
    private string _livePlatform = "YouTube";
    private string _liveServer = "rtmp://a.rtmp.youtube.com/live2";
    private string _liveVia = "ffmpeg";
    private string? _liveApproval;
    private string? _liveDisplay;
    private bool _liveScreenOffered;
    private Process? _liveProc;
    /// <summary>Für Tests: ersetzt den echten FFmpeg-Start. Der Befehl selbst bleibt echt.</summary>
    public Func<string, string[], (int Exit, string Err)>? CaptureRun { get; set; }
    public List<string> CaptureLog { get; } = [];
    /// <summary>Für Tests: Geräte statt echter Abfrage.</summary>
    public CaptureFacts? CaptureFactsOverride { get; set; }
    /// <summary>Für Tests: OBS-Ordner mit basic/profiles.</summary>
    public string? ObsConfigOverride { get; set; }

    private Control LiveStudio()
    {
        var sp = new StackPanel { Spacing = 8 };
        sp.Children.Add(P("Wähle Bildschirm, Fenster, Kamera oder Mikrofon. Die Vorschau und die Aufnahme bleiben auf diesem PC. Eine Sendung ins Netz startet erst, nachdem du sie unter Freigaben zugelassen hast. Trendo startet sie nicht von allein."));
        var src = new StackPanel { Spacing = 6 };
        src.Children.Add(H("Quellen", 14));
        src.Children.Add(new WrapPanel { Children = { Act("live-scan", "Quellen suchen", ScanLive, true) } });
        if (_liveSources.Count == 0) src.Children.Add(new TextBlock { Name = "live-sources", Text = "Noch keine Quelle. „Quellen suchen“ schaut nach Bildschirm, Kameras und Mikrofonen.", Foreground = Muted, TextWrapping = TextWrapping.Wrap });
        else
        {
            var row = new WrapPanel();
            for (var i = 0; i < _liveSources.Count; i++)
            {
                var s = _liveSources[i];
                var on = _livePick.Contains(s.Id);
                var n = i;
                row.Children.Add(Act($"live-src-{n}", (on ? "✓ " : "") + s.Label, () => ToggleLive(s.Id)));
            }
            src.Children.Add(row);
            var found = string.Join(", ", _liveSources.Select(s => s.Label));
            var chosen = _livePick.Count == 0 ? "Tippe an, was in die Sendung soll." : "Gewählt: " + string.Join(", ", _liveSources.Where(s => _livePick.Contains(s.Id)).Select(s => s.Label));
            src.Children.Add(new TextBlock { Name = "live-sources", Text = _liveSources.Count + " gefunden: " + found + ". " + chosen, Foreground = Text, TextWrapping = TextWrapping.Wrap });
        }
        if (_liveScreenOffered)
            src.Children.Add(Act("live-screen-guess", "Bildschirm mit 1280×720 hinzufügen", AddGuessedScreen));
        var win = new TextBox { Name = "live-window", PlaceholderText = "Fenstertitel", Width = 220 };
        src.Children.Add(new WrapPanel { Children = { win, Act("live-win-add", "Fenster hinzufügen", () => AddLiveWindow(win.Text ?? "")) } });
        sp.Children.Add(Card(src));

        var prev = new StackPanel { Spacing = 6 };
        prev.Children.Add(H("Vorschau und Aufnahme", 14));
        prev.Children.Add(new WrapPanel { Children = { Act("live-preview", "Vorschau", PreviewLive, true), Act("live-rec", "Lokal aufnehmen", () => RecordLive(true)), Act("live-rec-stop", "Aufnahme stoppen", StopLive) } });
        if (_livePreview is { } pic && File.Exists(pic))
        {
            try
            {
                using var fs = File.OpenRead(pic);
                var ms = new MemoryStream();
                fs.CopyTo(ms);
                ms.Position = 0;
                prev.Children.Add(new Image { Name = "live-preview-image", Source = new Bitmap(ms), MaxHeight = 240, HorizontalAlignment = HorizontalAlignment.Left });
            }
            catch (Exception ex) { prev.Children.Add(P("Die Vorschau-Datei lässt sich nicht anzeigen: " + ex.Message)); }
        }
        if (_liveFile is { } rec) prev.Children.Add(new TextBlock { Name = "live-file", Text = rec, Foreground = Text, TextWrapping = TextWrapping.Wrap });
        sp.Children.Add(Card(prev));

        var go = new StackPanel { Spacing = 6 };
        go.Children.Add(H("Auf Sendung", 14));
        go.Children.Add(P("Den Stream-Schlüssel tippst du selbst ein. Er landet nicht im Protokoll. Ohne Freigabe geht nichts ins Netz.", null, 11));
        var platforms = LiveStream.Platforms.Select(p => p.Name).ToList();
        var pf = new ComboBox { Name = "live-platform", ItemsSource = platforms, SelectedIndex = Math.Max(0, platforms.IndexOf(_livePlatform)), Width = 160 };
        var viaItems = new[] { "FFmpeg", "OBS" };
        var via = new ComboBox { Name = "live-via", ItemsSource = viaItems, SelectedIndex = _liveVia == "obs" ? 1 : 0, Width = 120 };
        var server = new TextBox { Name = "live-server", Text = _liveServer, Width = 320 };
        var key = new TextBox { Name = "live-key", Text = _liveKey, PasswordChar = '•', PlaceholderText = "Stream-Schlüssel", Width = 220 };
        go.Children.Add(new WrapPanel { Children = { LabeledLive("Plattform", pf), LabeledLive("Über", via), LabeledLive("Server", server), LabeledLive("Schlüssel", key) } });
        go.Children.Add(new WrapPanel { Children = { Act("live-ask", "Freigabe anfragen", () => AskLive(pf, via, server, key), true), Act("live-go", "Sendung starten", () => GoLive(pf, via, server, key)), Act("live-stop", "Sendung beenden", StopLive) } });
        sp.Children.Add(Card(go));
        return sp;
    }

    private static StackPanel LabeledLive(string label, Control box) => new() { Orientation = Orientation.Horizontal, Spacing = 4, Margin = new Avalonia.Thickness(0, 0, 12, 4), Children = { new TextBlock { Text = label, Foreground = Muted, FontSize = 12, VerticalAlignment = VerticalAlignment.Center }, box } };

    private void ScanLive()
    {
        var facts = CaptureFactsOverride ?? ReadCaptureFacts();
        _liveDisplay = facts.Display;
        _liveSources = LiveStream.Read(facts).ToList();
        _livePick.RemoveWhere(id => _liveSources.All(s => s.Id != id));
        _liveScreenOffered = !facts.Windows && !string.IsNullOrWhiteSpace(facts.Display) && _liveSources.All(s => s.Kind != StreamKind.Bildschirm);
        var n = _liveSources.Count;
        Say(n == 0
            ? (_liveScreenOffered ? "Kein Gerät mit messbarer Größe. Den Bildschirm kannst du unten ausdrücklich hinzufügen." : "Nichts gefunden: kein Bildschirm, keine Kamera, kein Mikrofon.")
            : $"{n} Quelle{(n == 1 ? "" : "n")} gefunden. Tippe an, was in die Sendung soll.", "sendung");
    }

    private CaptureFacts ReadCaptureFacts()
    {
        var windows = OperatingSystem.IsWindows();
        var display = Environment.GetEnvironmentVariable("DISPLAY");
        var videos = new List<(string, string)>();
        if (!windows && Directory.Exists("/dev"))
        {
            foreach (var dev in Directory.EnumerateFiles("/dev", "video*").OrderBy(x => x))
            {
                var name = dev;
                var sys = "/sys/class/video4linux/" + Path.GetFileName(dev) + "/name";
                if (File.Exists(sys)) { try { name = File.ReadAllText(sys).Trim(); } catch (IOException) { } }
                videos.Add((dev, name));
            }
        }
        string? Run(string exe, params string[] args)
        {
            if (_opt.Isolated) return null;
            var r = Cli.Run(exe, args, null, 4000);
            var text = (r.Out + "\n" + r.Err).Trim();
            return text.Length == 0 ? null : text;
        }
        var dshow = windows && FfmpegPath is { } ff ? Run(ff, "-hide_banner", "-list_devices", "true", "-f", "dshow", "-i", "dummy") : null;
        var info = new Dictionary<string, string>();
        var wm = Run("wmctrl", "-l");
        if (wm is not null)
        {
            foreach (var w in LiveStream.ParseWindows(wm, windows: false))
            {
                var xi = Run("xwininfo", "-id", w.Device);
                if (xi is not null) info[w.Device] = xi;
            }
        }
        return new CaptureFacts
        {
            Windows = windows, Display = display, Videos = videos, Xrandr = Run("xrandr", "--query"),
            Wmctrl = wm, WindowInfo = info, Pulse = Run("pactl", "list", "short", "sources"), DirectShow = dshow,
        };
    }

    private void ToggleLive(string id)
    {
        if (!_livePick.Add(id)) _livePick.Remove(id);
        Say(_livePick.Contains(id) ? "Quelle dazu genommen." : "Quelle abgewählt.", "sendung");
    }

    private void AddLiveWindow(string title)
    {
        if (!OperatingSystem.IsWindows()) { Say("Unter Linux misst „Quellen suchen“ die offenen Fenster. Ein Titel allein reicht zum Aufnehmen nicht.", "sendung"); return; }
        try
        {
            var s = LiveStream.Window(title);
            _liveSources.RemoveAll(x => x.Id == s.Id);
            _liveSources.Add(s);
            _livePick.Add(s.Id);
            Say($"Fenster „{s.Label}“ ist dabei.", "sendung");
        }
        catch (ArgumentException ex) { Say(ex.Message, "sendung"); }
    }

    private void AddGuessedScreen()
    {
        var dev = string.IsNullOrWhiteSpace(_liveDisplay) ? ":0.0" : _liveDisplay!;
        var s = new StreamSource("screen:1280", StreamKind.Bildschirm, "Bildschirm 1280×720", dev, "1280x720");
        _liveSources.RemoveAll(x => x.Id == s.Id);
        _liveSources.Add(s);
        _livePick.Add(s.Id);
        _liveScreenOffered = false;
        Say("Bildschirm mit 1280×720 ist dabei. Das ist die Größe, die du gewählt hast.", "sendung");
    }

    private List<StreamSource> Picked() => _liveSources.Where(s => _livePick.Contains(s.Id)).ToList();

    private void PreviewLive()
    {
        var src = Picked();
        var ext = src.Count == 0 ? ".png" : LiveStream.OutputExt(src, LiveOut.Vorschau);
        var path = Path.Combine(LiveDir(), "vorschau" + ext);
        var msg = RunLocal(src, LiveOut.Vorschau, path);
        if (msg == "fertig") { _livePreview = ext == ".png" ? path : _livePreview; _liveFile = path; Say(ext == ".png" ? "Vorschau ist da." : "Tonprobe gespeichert: " + Path.GetFileName(path), "sendung"); }
        else Say(msg, "sendung");
    }

    private void RecordLive(bool start)
    {
        if (!start) { StopLive(); return; }
        if (_liveProc is { HasExited: false }) { Say("Es läuft schon eine Aufnahme oder Sendung. Bitte zuerst stoppen.", "sendung"); return; }
        var src = Picked();
        var ext = src.Count == 0 ? ".mp4" : LiveStream.OutputExt(src, LiveOut.Aufnahme);
        var path = Path.Combine(LiveDir(), "aufnahme-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ext);
        var msg = RunLocal(src, LiveOut.Aufnahme, path);
        if (msg == "fertig") { _liveFile = path; Say("Aufnahme gespeichert: " + Path.GetFileName(path), "sendung"); }
        else if (msg == "läuft") { _liveFile = path; Say("Aufnahme läuft: " + Path.GetFileName(path) + ". „Aufnahme stoppen“ beendet sie.", "sendung"); }
        else Say(msg, "sendung");
    }

    private string RunLocal(IReadOnlyList<StreamSource> src, LiveOut kind, string output)
    {
        try
        {
            var args = LiveStream.Args(src, kind, output, OperatingSystem.IsWindows());
            return RunFfmpeg(args, output, kind == LiveOut.Vorschau);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return ex.Message; }
    }

    private void AskLive(ComboBox pf, ComboBox via, TextBox server, TextBox key)
    {
        RememberLive(pf, via, server, key);
        try
        {
            var req = LiveStream.Ask(_core.Approvals, Principal.LocalOwner, _liveVia, _livePlatform, _liveServer, Picked().Select(s => s.Id).ToList(), _liveKey);
            _liveApproval = req.Id;
            Say("Die Freigabe liegt unter „Freigaben“. Es wird noch nichts gesendet. Nach dem Zustimmen hier auf „Sendung starten“.", "sendung");
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { Say(ex.Message, "sendung"); }
    }

    private void GoLive(ComboBox pf, ComboBox via, TextBox server, TextBox key)
    {
        RememberLive(pf, via, server, key);
        if (_liveApproval is null) { Say("Zuerst „Freigabe anfragen“. Es wurde nichts gesendet.", "sendung"); return; }
        if (_liveProc is { HasExited: false }) { Say("Es läuft schon etwas. Bitte zuerst beenden.", "sendung"); return; }
        if (_liveVia == "ffmpeg" && FfmpegPath is null && CaptureRun is null) { Say("FFmpeg fehlt. Unter „Zusatzprogramme“ kannst du es einrichten. Es wurde nichts gesendet.", "sendung"); return; }
        if (_liveVia == "obs" && AppPath("obs") is null) { Say("OBS ist nicht eingerichtet. Weiter unten fragt „OBS einrichten“, bevor etwas installiert wird. Es wurde nichts gesendet.", "sendung"); return; }
        var ids = Picked().Select(s => s.Id).ToList();
        try
        {
            LiveStream.Start(_core.Approvals, _liveApproval, _liveVia, _livePlatform, _liveServer, ids, _liveKey, () =>
            {
                if (_liveVia == "obs") StartObs();
                else
                {
                    var url = LiveStream.PublishUrl(_liveServer, _liveKey);
                    var args = LiveStream.Args(Picked(), LiveOut.Sendung, "", OperatingSystem.IsWindows(), url);
                    var msg = RunFfmpeg(args, "", wait: false);
                    if (msg != "läuft" && msg != "fertig") throw new InvalidOperationException(msg);
                }
            });
            _core.Audit.Append(Principal.LocalOwner.Name, "live.started", _liveVia + " " + _livePlatform);
            Say(_liveVia == "obs" ? "OBS startet die Sendung. Der Schlüssel steht nur in deinem OBS-Profil." : "Die Sendung läuft über FFmpeg. „Sendung beenden“ stoppt sie.", "sendung");
        }
        catch (ApprovalException) { Say("Noch nicht freigegeben oder die Quellen, der Server oder der Schlüssel haben sich geändert. Es wurde nichts gesendet.", "sendung"); }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or IOException) { Say(ex.Message + " Es wurde nichts gesendet.", "sendung"); }
    }

    private void StartObs()
    {
        var obs = AppPath("obs");
        if (obs is null) throw new InvalidOperationException("OBS ist nicht eingerichtet. „OBS einrichten“ weiter unten fragt, bevor etwas installiert wird.");
        var root = ObsConfigOverride ?? (OperatingSystem.IsWindows()
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "obs-studio")
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", "obs-studio"));
        var service = LiveStream.NewestService(root) ?? throw new InvalidOperationException("OBS hat noch kein Profil. Öffne OBS einmal und lege eine Szenensammlung an. Es wurde nichts gesendet.");
        LiveStream.WriteService(service, _liveServer, _liveKey);
        string? collection = null;
        try
        {
            var file = ObsCollectionFile();
            if (file is not null)
            {
                var dir = ObsDirOverride ?? ExternalApps.ObsScenesDir();
                Directory.CreateDirectory(dir);
                File.Copy(file, Path.Combine(dir, Path.GetFileName(file)), true);
                collection = Path.GetFileNameWithoutExtension(file);
            }
        }
        catch (InvalidOperationException) { collection = null; }
        var args = new List<string> { "--startstreaming" };
        if (collection is not null) args.AddRange(["--collection", collection]);
        _core.Integrations.LaunchArgs(Principal.LocalOwner, obs, _core.Audit, args);
    }

    private void RememberLive(ComboBox pf, ComboBox via, TextBox server, TextBox key)
    {
        var index = Math.Clamp(pf.SelectedIndex, 0, LiveStream.Platforms.Length - 1);
        _livePlatform = LiveStream.Platforms[index].Name;
        _liveVia = via.SelectedIndex == 1 ? "obs" : "ffmpeg";
        var typed = server.Text?.Trim() ?? "";
        var known = LiveStream.Platforms.Any(p => p.Server == typed);
        _liveServer = typed.Length == 0 || known ? LiveStream.Platforms[index].Server : typed;
        if (_liveServer == "rtmp://" && typed.StartsWith("rtmp", StringComparison.OrdinalIgnoreCase) && typed != "rtmp://") _liveServer = typed;
        _liveKey = key.Text ?? "";
    }

    private void StopLive()
    {
        var p = _liveProc;
        _liveProc = null;
        if (p is null) { Say("Hier läuft kein FFmpeg. Eine OBS-Sendung beendest du in OBS mit „Stream stoppen“.", "sendung"); return; }
        try { if (!p.HasExited) p.Kill(entireProcessTree: true); } catch (Exception) { }
        p.Dispose();
        _core.Audit.Append(Principal.LocalOwner.Name, "live.stopped", _livePlatform);
        Say("Gestoppt.", "sendung");
    }

    private string RunFfmpeg(string[] args, string output, bool wait)
    {
        var ffmpeg = FfmpegPath;
        if (ffmpeg is null && CaptureRun is null) return "FFmpeg fehlt. Unter „Zusatzprogramme“ kannst du es einrichten. Es wurde nichts gestartet.";
        var exe = ffmpeg ?? "ffmpeg";
        CaptureLog.Add(string.Join(" ", args));
        if (CaptureRun is { } hook)
        {
            var (exit, err) = hook(exe, args);
            if (exit != 0) return string.IsNullOrWhiteSpace(err) ? "FFmpeg hat abgebrochen (Exit " + exit + ")." : err.Trim();
            if (output.Length > 0 && !File.Exists(output)) return "FFmpeg ist fertig, aber die Datei fehlt.";
            return "fertig";
        }
        if (wait)
        {
            var r = Cli.Run(exe, args, LiveDir(), 20000);
            if (!r.Ok) return string.IsNullOrWhiteSpace(r.Err) ? "FFmpeg hat abgebrochen." : r.Err.Trim();
            if (output.Length > 0 && !File.Exists(output)) return "FFmpeg ist fertig, aber die Datei fehlt.";
            return "fertig";
        }
        StopQuiet();
        var psi = new ProcessStartInfo(exe) { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = LiveDir(), RedirectStandardError = true, RedirectStandardOutput = true };
        foreach (var arg in args) psi.ArgumentList.Add(arg);
        _liveProc = Process.Start(psi) ?? throw new InvalidOperationException("FFmpeg ließ sich nicht starten.");
        return "läuft";
    }

    private void StopQuiet()
    {
        var p = _liveProc;
        _liveProc = null;
        if (p is null) return;
        try { if (!p.HasExited) p.Kill(entireProcessTree: true); } catch (Exception) { }
        p.Dispose();
    }

    private string LiveDir()
    {
        var d = Path.Combine(Fach.StoreDir(_core.DataRoot, "sendung"), "live");
        Directory.CreateDirectory(d);
        return d;
    }
}
