using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Trendo.Core;

namespace Trendo.App;

/// <summary>CORE-09: Webcams und IP-Kameras zeigen, Roboter nur nach Freigabe bewegen, Not-Aus immer sichtbar.</summary>
public partial class MainWindow
{
    private readonly RobotLink _arm = new();
    private readonly List<KnownCamera> _cams = [];
    private string? _camId;
    private string? _camPreview;
    private string? _camFile;
    private Process? _camProc;
    private ApprovalRequest? _robotReq;
    public CameraFacts? CameraFactsOverride { get; set; }
    /// <summary>Für Tests: ersetzt FFmpeg. Der Befehl bleibt der echte.</summary>
    public Func<string, string[], (int Exit, string Err)>? RigRun { get; set; }
    public List<string> RigLog { get; } = [];
    /// <summary>Für Tests: ersetzt die ONVIF-Suche im Netz.</summary>
    public Func<Task<IReadOnlyList<string>>>? OnvifProbe { get; set; }
    /// <summary>Für Tests: ersetzt die SOAP-Abfrage. Parameter sind Adresse, Benutzer, Kennwort.</summary>
    public Func<string, string?, string?, Task<string>>? OnvifFetch { get; set; }

    private Control CameraRobotPanel()
    {
        LoadSavedCams();
        var sp = new StackPanel { Spacing = 8 };
        var stopRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var estop = new Button
        {
            Name = "rob-estop", Content = "NOT-AUS", Background = Brush.Parse("#DC2626"), Foreground = Brushes.White,
            FontSize = 18, FontWeight = FontWeight.Bold, Padding = new Avalonia.Thickness(18, 8), HorizontalAlignment = HorizontalAlignment.Left,
        };
        estop.Click += (_, _) => { _arm.Estop(); StopCamQuiet(); Say("NOT-AUS. Es bewegt sich nichts, die Aufnahme ist gestoppt.", "roboter"); };
        stopRow.Children.Add(estop);
        stopRow.Children.Add(Act("rob-estop-off", "Not-Aus lösen", () => { _arm.Release(); Say("Not-Aus gelöst. Eine echte Bewegung braucht wieder eine Freigabe.", "roboter"); }));
        sp.Children.Add(stopRow);
        if (_arm.Emergency) sp.Children.Add(new TextBlock { Name = "rob-estop-state", Text = "Not-Aus ist aktiv. Es bewegt sich nichts.", Foreground = Brush.Parse("#F87171"), FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap });

        sp.Children.Add(Card(CameraBlock()));
        sp.Children.Add(Card(RobotBlock()));
        return sp;
    }

    private Control CameraBlock()
    {
        var box = new StackPanel { Spacing = 6 };
        box.Children.Add(H("Kameras", 14));
        box.Children.Add(P("Webcams am PC und IP-Kameras (RTSP oder ONVIF). Das Bild, das Foto und die Aufnahme bleiben hier."));
        box.Children.Add(new WrapPanel { Children = { Act("cam-scan", "Webcams suchen", ScanCams, true), Act("cam-live", "Livebild", () => GrabCam(false)), Act("cam-snap", "Foto", () => GrabCam(true)), Act("cam-rec", "Aufnehmen", RecordCam), Act("cam-rec-stop", "Aufnahme stoppen", StopCam) } });
        if (_cams.Count == 0) box.Children.Add(new TextBlock { Name = "cam-list", Text = "Noch keine Kamera.", Foreground = Muted, TextWrapping = TextWrapping.Wrap });
        else
        {
            var row = new WrapPanel();
            for (var i = 0; i < _cams.Count; i++)
            {
                var c = _cams[i];
                var n = i;
                var mark = c.Id == _camId ? "✓ " : "";
                row.Children.Add(Act($"cam-pick-{n}", mark + c.Name, () => { _camId = c.Id; Say(c.Name + " ist gewählt.", "roboter"); }));
            }
            box.Children.Add(row);
            var cur = _cams.FirstOrDefault(c => c.Id == _camId);
            var names = string.Join(", ", _cams.Select(c => c.Name));
            var head = _cams.Count == 1 ? "1 Kamera: " + names : _cams.Count + " Kameras: " + names;
            var tail = cur is null ? " Tippe eine an." : " Gewählt: " + cur.Name + " · " + CameraCatalog.Redact(cur.Address);
            box.Children.Add(new TextBlock { Name = "cam-list", Text = head + "." + tail, Foreground = Text, TextWrapping = TextWrapping.Wrap });
        }
        if (_camPreview is { } pic && File.Exists(pic))
        {
            try
            {
                using var fs = File.OpenRead(pic);
                var ms = new MemoryStream();
                fs.CopyTo(ms);
                ms.Position = 0;
                box.Children.Add(new Image { Name = "cam-preview-image", Source = new Bitmap(ms), MaxHeight = 240, HorizontalAlignment = HorizontalAlignment.Left });
            }
            catch (Exception ex) { box.Children.Add(P("Das Bild lässt sich nicht anzeigen: " + ex.Message)); }
        }
        var name = new TextBox { Name = "cam-name", PlaceholderText = "Name", Text = "Wohnzimmer", Width = 140 };
        var url = new TextBox { Name = "cam-rtsp", PlaceholderText = "rtsp://…", Width = 280 };
        var seconds = new TextBox { Name = "cam-seconds", Text = "10", Width = 60 };
        box.Children.Add(new WrapPanel { Children = { LabeledCam("Name", name), LabeledCam("RTSP", url), Act("cam-add", "IP-Kamera hinzufügen", () => AddRtsp(name.Text ?? "", url.Text ?? "")) } });
        var host = new TextBox { Name = "cam-onvif-host", PlaceholderText = "192.168.0.20", Width = 160 };
        var user = new TextBox { Name = "cam-onvif-user", PlaceholderText = "Benutzer", Width = 120 };
        var pass = new TextBox { Name = "cam-onvif-pass", PlaceholderText = "Kennwort", PasswordChar = '•', Width = 120 };
        box.Children.Add(new WrapPanel { Children = { LabeledCam("ONVIF", host), user, pass, Act("cam-onvif", "ONVIF lesen", async () => await ReadOnvif(host.Text ?? "", user.Text, pass.Text)), Act("cam-discover", "Im Netz suchen", DiscoverOnvif) } });
        box.Children.Add(new WrapPanel { Children = { LabeledCam("Aufnahme Sekunden", seconds) } });
        return box;
    }

    private Control RobotBlock()
    {
        var box = new StackPanel { Spacing = 6 };
        box.Children.Add(H("Roboter", 14));
        box.Children.Add(P("Simulieren und Probelauf schicken nichts los. „Wirklich senden“ geht erst nach deiner Freigabe, und nie bei Not-Aus."));
        var device = new TextBox { Name = "rob-device", Text = "Arm", Width = 100 };
        var port = new TextBox { Name = "rob-port", PlaceholderText = "COM3 oder /dev/ttyUSB0", Width = 200 };
        var b = NumCam("rob-base", "0");
        var sh = NumCam("rob-shoulder", "45");
        var el = NumCam("rob-elbow", "-30");
        var sec = NumCam("rob-sec", "2");
        var grip = new ComboBox { Name = "rob-grip", ItemsSource = new[] { "halten", "greifen", "loslassen" }, SelectedIndex = 0, Width = 120 };
        var x = NumCam("rob-x", "150");
        var y = NumCam("rob-y", "50");
        var z = NumCam("rob-z", "40");
        box.Children.Add(new WrapPanel { Children = { LabeledCam("Gerät", device), LabeledCam("Anschluss", port), LabeledCam("Sockel °", b), LabeledCam("Schulter °", sh), LabeledCam("Ellbogen °", el), LabeledCam("Sekunden", sec), LabeledCam("Greifer", grip) } });
        box.Children.Add(new WrapPanel { Children = { Act("rob-dry", "Probelauf", () => DryRobot(device, b, sh, el, sec, grip), true), Act("rob-ask", "Freigabe anfragen", () => AskRobot(device, b, sh, el, sec, grip)), Act("rob-send", "Wirklich senden", () => SendRobot(device, port, b, sh, el, sec, grip)) } });
        box.Children.Add(new WrapPanel { Children = { LabeledCam("Ziel x", x), LabeledCam("y", y), LabeledCam("z", z), Act("rob-sim", "Stellung simulieren", () => SimRobot(x, y, z)) } });
        box.Children.Add(new TextBlock { Name = "rob-journal", Text = _arm.Journal.Count == 0 ? "Noch kein Befehl." : string.Join("\n", _arm.Journal.TakeLast(6)), Foreground = Text, TextWrapping = TextWrapping.Wrap });
        return box;
    }

    private static StackPanel LabeledCam(string label, Control box) => new() { Orientation = Orientation.Horizontal, Spacing = 4, Margin = new Avalonia.Thickness(0, 0, 12, 4), Children = { new TextBlock { Text = label, Foreground = Muted, FontSize = 12, VerticalAlignment = VerticalAlignment.Center }, box } };
    private static TextBox NumCam(string name, string value) => new() { Name = name, Text = value, Width = 70 };

    private void ScanCams()
    {
        var facts = CameraFactsOverride ?? ReadCameraFacts();
        var local = CameraCatalog.Local(facts);
        _cams.RemoveAll(c => c.Kind == "webcam");
        _cams.InsertRange(0, local);
        LoadSavedCams();
        Say(local.Count == 0 ? "Keine Webcam gefunden." : local.Count == 1 ? "1 Webcam gefunden." : local.Count + " Webcams gefunden.", "roboter");
    }

    private CameraFacts ReadCameraFacts()
    {
        var windows = OperatingSystem.IsWindows();
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
        string? dshow = null;
        if (windows && FfmpegPath is { } ff && !_opt.Isolated)
        {
            var r = Cli.Run(ff, ["-hide_banner", "-list_devices", "true", "-f", "dshow", "-i", "dummy"], null, 4000);
            dshow = (r.Out + "\n" + r.Err).Trim();
        }
        return new CameraFacts { Windows = windows, Videos = videos, DirectShow = dshow };
    }

    private void LoadSavedCams()
    {
        foreach (var c in CameraBook.Load(CamFile()).Saved)
            if (_cams.All(x => x.Id != c.Id)) _cams.Add(c);
    }

    private void RememberCam(KnownCamera cam)
    {
        _cams.RemoveAll(c => c.Id == cam.Id);
        _cams.Add(cam);
        _camId = cam.Id;
        var book = CameraBook.Load(CamFile());
        book.Saved.RemoveAll(c => c.Id == cam.Id);
        book.Saved.Add(cam);
        book.Save(CamFile());
    }

    private void AddRtsp(string name, string url)
    {
        try
        {
            var cam = CameraCatalog.Rtsp(name, url);
            RememberCam(cam);
            Say(cam.Name + " ist gespeichert. Adresse: " + CameraCatalog.Redact(cam.Address), "roboter");
        }
        catch (ArgumentException ex) { Say(ex.Message, "roboter"); }
    }

    private async Task ReadOnvif(string host, string? user, string? pass)
    {
        try
        {
            string rtsp;
            if (OnvifFetch is { } fake) rtsp = await fake(host, user, pass);
            else rtsp = await Onvif.Resolve(PostSoap, host, user, pass);
            var cam = CameraCatalog.Rtsp(string.IsNullOrWhiteSpace(host) ? "ONVIF" : "ONVIF " + host.Trim(), rtsp);
            RememberCam(cam);
            _core.Audit.Append(Principal.LocalOwner.Name, "camera.onvif", CameraCatalog.Redact(rtsp));
            Say("RTSP-Adresse gelesen: " + CameraCatalog.Redact(rtsp), "roboter");
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or HttpRequestException or TaskCanceledException) { Say(ex.Message, "roboter"); }
    }

    private static async Task<string> PostSoap(string url, string body, CancellationToken ct)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        using var res = await http.PostAsync(url, new StringContent(body, Encoding.UTF8, "application/soap+xml"), ct);
        var text = await res.Content.ReadAsStringAsync(ct);
        if (!res.IsSuccessStatusCode) throw new InvalidOperationException("Die Kamera antwortet mit " + (int)res.StatusCode + ".");
        return text;
    }

    private async Task DiscoverOnvif()
    {
        IReadOnlyList<string> found;
        try
        {
            found = OnvifProbe is { } fake ? await fake() : await ProbeLan();
        }
        catch (Exception ex) when (ex is SocketException or InvalidOperationException) { Say("Die Suche im Netz ist fehlgeschlagen: " + ex.Message, "roboter"); return; }
        if (found.Count == 0) { Say("Keine ONVIF-Kamera hat geantwortet. Trag die Adresse oben ein.", "roboter"); return; }
        var n = 0;
        foreach (var addr in found.Take(8))
        {
            try
            {
                var rtsp = OnvifFetch is { } fake ? await fake(addr, null, null) : await Onvif.Resolve(PostSoap, addr, null, null);
                RememberCam(CameraCatalog.Rtsp("ONVIF " + (++n), rtsp));
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or HttpRequestException or TaskCanceledException)
            {
                _cams.Add(new("onvif:" + addr, "onvif", "ONVIF " + addr, addr));
            }
        }
        Say(found.Count + " Antwort(en) im Netz.", "roboter");
    }

    private static async Task<IReadOnlyList<string>> ProbeLan()
    {
        using var udp = new UdpClient();
        var probe = Onvif.Probe();
        await udp.SendAsync(probe, new IPEndPoint(IPAddress.Parse("239.255.255.250"), 3702));
        var found = new List<string>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        while (!cts.IsCancellationRequested)
        {
            try
            {
                var got = await udp.ReceiveAsync(cts.Token);
                foreach (var x in Onvif.XAddrs(Encoding.UTF8.GetString(got.Buffer))) found.Add(x);
            }
            catch (OperationCanceledException) { break; }
        }
        return found.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private KnownCamera? SelectedCam() => _cams.FirstOrDefault(c => c.Id == _camId);

    private void GrabCam(bool keep)
    {
        var cam = SelectedCam();
        if (cam is null) { Say("Bitte zuerst eine Kamera wählen.", "roboter"); return; }
        var path = Path.Combine(CamDir(), (keep ? "foto-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") : "livebild") + ".png");
        var msg = RunRig(CameraCatalog.Snapshot(cam, path, OperatingSystem.IsWindows()), path, wait: true);
        if (msg == "fertig") { _camPreview = path; _camFile = path; Say(keep ? "Foto gespeichert: " + Path.GetFileName(path) : "Livebild aktualisiert.", "roboter"); }
        else Say(msg, "roboter");
    }

    private void RecordCam()
    {
        var cam = SelectedCam();
        if (cam is null) { Say("Bitte zuerst eine Kamera wählen.", "roboter"); return; }
        if (_camProc is { HasExited: false }) { Say("Die Aufnahme läuft schon. „Aufnahme stoppen“ beendet sie.", "roboter"); return; }
        if (!int.TryParse((FindText("cam-seconds") ?? "10").Trim(), out var sec)) { Say("Die Dauer bitte als ganze Sekunden, oder 0 für eine Aufnahme bis zum Stopp.", "roboter"); return; }
        var path = Path.Combine(CamDir(), "aufnahme-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".mp4");
        string[] args;
        try { args = CameraCatalog.Record(cam, path, sec, OperatingSystem.IsWindows()); }
        catch (ArgumentException ex) { Say(ex.Message, "roboter"); return; }
        var msg = RunRig(args, path, wait: sec > 0 && RigRun is null);
        if (msg == "fertig") { _camFile = path; Say("Aufnahme gespeichert: " + Path.GetFileName(path), "roboter"); }
        else if (msg == "läuft") { _camFile = path; Say("Aufnahme läuft: " + Path.GetFileName(path), "roboter"); }
        else Say(msg, "roboter");
    }

    private string? FindText(string name) => Avalonia.LogicalTree.LogicalExtensions.GetLogicalDescendants(this).OfType<TextBox>().FirstOrDefault(t => t.Name == name)?.Text;

    private void StopCam()
    {
        if (_camProc is null) { Say("Es läuft keine Aufnahme.", "roboter"); return; }
        StopCamQuiet();
        Say("Aufnahme gestoppt.", "roboter");
    }

    private void StopCamQuiet()
    {
        var p = _camProc;
        _camProc = null;
        if (p is null) return;
        try { if (!p.HasExited) p.Kill(entireProcessTree: true); } catch (Exception) { }
        p.Dispose();
    }

    private string RunRig(string[] args, string output, bool wait)
    {
        var ffmpeg = FfmpegPath;
        if (ffmpeg is null && RigRun is null) return "FFmpeg fehlt. Unter „Zusatzprogramme“ kannst du es einrichten. Es wurde nichts aufgenommen.";
        RigLog.Add(string.Join(" ", args));
        if (RigRun is { } hook)
        {
            var (exit, err) = hook(ffmpeg ?? "ffmpeg", args);
            if (exit != 0) return string.IsNullOrWhiteSpace(err) ? "FFmpeg hat abgebrochen." : err.Trim();
            if (!File.Exists(output)) return "FFmpeg ist fertig, aber die Datei fehlt.";
            return "fertig";
        }
        var exe = ffmpeg!;
        if (wait)
        {
            var r = Cli.Run(exe, args, CamDir(), 20000);
            if (!r.Ok) return string.IsNullOrWhiteSpace(r.Err) ? "FFmpeg hat abgebrochen." : r.Err.Trim();
            if (!File.Exists(output)) return "FFmpeg ist fertig, aber die Datei fehlt.";
            return "fertig";
        }
        StopCamQuiet();
        var psi = new ProcessStartInfo(exe) { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = CamDir(), RedirectStandardError = true, RedirectStandardOutput = true };
        foreach (var arg in args) psi.ArgumentList.Add(arg);
        _camProc = Process.Start(psi) ?? throw new InvalidOperationException("FFmpeg ließ sich nicht starten.");
        return "läuft";
    }

    private MotionOrder? Order(TextBox device, TextBox b, TextBox sh, TextBox el, TextBox sec, ComboBox grip)
    {
        try
        {
            var g = grip.SelectedIndex switch { 1 => "greifen", 2 => "loslassen", _ => "halten" };
            return new MotionOrder(device.Text ?? "", Fach.Num(b.Text ?? ""), Fach.Num(sh.Text ?? ""), Fach.Num(el.Text ?? ""), Fach.Num(sec.Text ?? ""), g);
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException) { Say(ex.Message, "roboter"); return null; }
    }

    private void DryRobot(TextBox device, TextBox b, TextBox sh, TextBox el, TextBox sec, ComboBox grip)
    {
        var order = Order(device, b, sh, el, sec, grip);
        if (order is null) return;
        Say(_arm.Dry(order), "roboter");
    }

    private void AskRobot(TextBox device, TextBox b, TextBox sh, TextBox el, TextBox sec, ComboBox grip)
    {
        var order = Order(device, b, sh, el, sec, grip);
        if (order is null) return;
        _robotReq = _arm.Ask(_core.Approvals, Principal.LocalOwner, order);
        Say("Die Freigabe liegt unter „Freigaben“. Es wurde nichts gesendet. Danach hier auf „Wirklich senden“.", "roboter");
    }

    private void SendRobot(TextBox device, TextBox port, TextBox b, TextBox sh, TextBox el, TextBox sec, ComboBox grip)
    {
        if (_arm.Emergency) { Say("Not-Aus ist aktiv. Es wurde nichts gesendet.", "roboter"); return; }
        var order = Order(device, b, sh, el, sec, grip);
        if (order is null) return;
        if (_robotReq is null || _robotReq.Target != order.Target) { Say("Zuerst „Freigabe anfragen“ für genau diesen Befehl. Es wurde nichts gesendet.", "roboter"); return; }
        if (_robotReq.State == ApprovalState.Pending) { Say("Noch nicht freigegeben. Es wurde nichts gesendet.", "roboter"); return; }
        if (_robotReq.State != ApprovalState.Approved) { Say("Diese Freigabe gilt nicht mehr. Bitte neu anfragen. Es wurde nichts gesendet.", "roboter"); return; }
        try
        {
            var line = _arm.Send(_core.Approvals, _robotReq.Id, order, cmd => RobotWire.Send(port.Text ?? "", cmd));
            Say(line, "roboter");
        }
        catch (Exception ex) when (ex is ApprovalException or InvalidOperationException or IOException or UnauthorizedAccessException) { Say(ex.Message, "roboter"); }
    }

    private void SimRobot(TextBox x, TextBox y, TextBox z)
    {
        try
        {
            var arm = new ArmConfig(60, 120, 120);
            Say(_arm.Simulate(arm, Fach.Num(x.Text ?? ""), Fach.Num(y.Text ?? ""), Fach.Num(z.Text ?? "")), "roboter");
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException) { Say(ex.Message, "roboter"); }
    }

    private string CamDir()
    {
        var d = Path.Combine(Fach.StoreDir(_core.DataRoot, "kameras"), "bilder");
        Directory.CreateDirectory(d);
        return d;
    }

    private string CamFile() => Path.Combine(Fach.StoreDir(_core.DataRoot, "kameras"), "kameras.json");
}
