using System.Net.Sockets;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Trendo.Core;

namespace Trendo.App;

public sealed class SavedCamera
{
    public string Name { get; set; } = "";
    public string Source { get; set; } = "";
    public string Kind { get; set; } = "";
}

/// <summary>Zustand von Kameras und Arm, bleibt beim Neuzeichnen der Seite erhalten.</summary>
public sealed class RobotBench
{
    public List<CameraDevice> Cameras { get; } = [];
    public int Selected { get; set; } = -1;
    public byte[]? Frame { get; set; }
    public bool Live { get; set; }
    public RobotDrive Drive { get; } = new();
    public string? PendingId { get; set; }
    public string? PendingCommand { get; set; }
    public Joints? PendingPose { get; set; }
    public string X { get; set; } = "150";
    public string Y { get; set; } = "50";
    public string Z { get; set; } = "40";
    public string Host { get; set; } = "";
    public string Rtsp { get; set; } = "";
    public string Points { get; set; } = "150 0 100 | 150 0 40 | 0 150 40";
    public int ModeIndex { get; set; }
    public string CamStatus { get; set; } = "Noch kein Bild. Kamera suchen oder eine rtsp-Adresse eintragen.";
    public string DriveStatus { get; set; } = "Die Steuerung steht auf Simulation. Es bewegt sich nichts Echtes.";
}

public partial class MainWindow
{
    public RobotBench Bench { get; } = new();
    public IWebcamProbe Webcams { get; set; } = new SystemWebcamProbe();
    public IOnvifTransport? Onvif { get; set; }
    public Func<string, IMotionLink>? OpenRobotLink { get; set; }
    public Func<CameraDevice, string, IDisposable>? StartCameraRecord { get; set; }

    private IFrameSource? _camFrames;
    public IFrameSource CameraFrames
    {
        get => _camFrames ??= new FfmpegFrameSource(() => FfmpegPath);
        set => _camFrames = value;
    }

    private bool _camsLoaded;
    private CancellationTokenSource? _liveCts;
    private IDisposable? _recHandle;
    private string? _recPath;
    private Bitmap? _camBmp;

    private void LoadSavedCams()
    {
        if (_camsLoaded) return;
        _camsLoaded = true;
        foreach (var s in Fach.Load<List<SavedCamera>>(_core.DataRoot, "roboter", "kameras"))
        {
            try
            {
                if (s.Kind == nameof(CameraKind.Rtsp)) Bench.Cameras.Add(WebcamScan.FromRtsp(s.Source, s.Name));
                else if (s.Kind == nameof(CameraKind.Onvif) && s.Source.StartsWith("rtsp", StringComparison.OrdinalIgnoreCase))
                    Bench.Cameras.Add(new CameraDevice("onvif:" + s.Source, s.Name, CameraKind.Onvif, s.Source));
            }
            catch (ArgumentException) { }
        }
    }

    private void SaveCams()
    {
        var list = Bench.Cameras.Where(c => c.Kind != CameraKind.Webcam)
            .Select(c => new SavedCamera { Name = c.Name, Source = c.Source, Kind = c.Kind.ToString() }).ToList();
        Fach.Save(_core.DataRoot, "roboter", "kameras", list);
    }

    private CameraDevice? SelectedCam()
    {
        if (Bench.Cameras.Count == 0) return null;
        if (Bench.Selected < 0 || Bench.Selected >= Bench.Cameras.Count) Bench.Selected = 0;
        return Bench.Cameras[Bench.Selected];
    }

    private void PaintCam()
    {
        var img = this.GetLogicalDescendants().OfType<Image>().FirstOrDefault(i => i.Name == "cam-preview");
        if (img is not null && Bench.Frame is { } frame)
        {
            var bmp = new Bitmap(new MemoryStream(frame));
            img.Source = bmp;
            _camBmp?.Dispose();
            _camBmp = bmp;
        }
        if (this.GetLogicalDescendants().OfType<TextBlock>().FirstOrDefault(t => t.Name == "cam-status") is { } status)
            status.Text = Bench.CamStatus;
        if (this.GetLogicalDescendants().OfType<Button>().FirstOrDefault(b => b.Name == "cam-live") is { } live)
            live.Content = Bench.Live ? "Livebild aus" : "Livebild an";
    }

    private Control EstopBar()
    {
        var stop = new Button
        {
            Name = "rob-estop", Content = "Not-Aus", Background = Brush.Parse("#E11D48"), Foreground = Brushes.White,
            FontWeight = FontWeight.Bold, Padding = new Thickness(18, 8), FontSize = 16,
        };
        stop.Click += (_, _) => DoEstop();
        var release = new Button { Name = "rob-estop-reset", Content = "Not-Aus lösen", Padding = new Thickness(12, 8) };
        release.Click += (_, _) => DoRelease();
        var state = new TextBlock
        {
            Name = "rob-estop-state",
            Text = Bench.Drive.Emergency ? "Not-Aus ist aktiv. Keine Fahrt." : "Not-Aus ist bereit.",
            Foreground = Text, VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap,
        };
        return new Border
        {
            Background = Brush.Parse("#33E11D48"), CornerRadius = new CornerRadius(10), Padding = new Thickness(10, 8),
            Child = new StackPanel
            {
                Orientation = Orientation.Horizontal, Spacing = 10,
                Children = { stop, release, state },
            },
        };
    }

    private Control CameraPanel()
    {
        LoadSavedCams();
        var sp = new StackPanel { Spacing = 6 };
        sp.Children.Add(H("Kameras", 14));
        sp.Children.Add(P("Webcams kommen vom Rechner. IP-Kameras trägst du als rtsp:// ein oder suchst sie per ONVIF im Netz.", null, 11));
        var tools = new WrapPanel();
        tools.Children.Add(MSp(Act("cam-scan", "Kameras suchen", SearchCams, true)));
        tools.Children.Add(MSp(Act("cam-onvif", "Kameras im Netzwerk suchen", () => _ = SearchNet())));
        tools.Children.Add(MSp(Act("cam-ffmpeg", "FFmpeg einrichten", () => _ = AskFfmpeg())));
        sp.Children.Add(tools);
        if (Bench.Cameras.Count == 0) sp.Children.Add(P("Keine Kamera in der Liste."));
        else
        {
            var list = new WrapPanel();
            for (var i = 0; i < Bench.Cameras.Count; i++)
            {
                var cam = Bench.Cameras[i]; var n = i;
                var mark = n == Bench.Selected ? "● " : "";
                list.Children.Add(MSp(Act("cam-pick-" + n, mark + cam.KindLabel + ": " + cam.Name, () => { Bench.Selected = n; Show("roboter"); })));
            }
            sp.Children.Add(list);
        }
        var rtsp = new TextBox { Name = "cam-rtsp", Text = Bench.Rtsp, PlaceholderText = "rtsp://192.168.1.20:554/stream", Width = 280 };
        var add = Act("cam-add", "IP-Kamera hinzufügen", () => AddRtsp(rtsp), true);
        sp.Children.Add(new WrapPanel { Children = { MSp(rtsp), MSp(add) } });
        var img = new Image { Name = "cam-preview", Width = 320, Height = 240, Stretch = Stretch.Uniform };
        if (Bench.Frame is { } frame) img.Source = new Bitmap(new MemoryStream(frame));
        sp.Children.Add(img);
        var status = new TextBlock { Name = "cam-status", Text = Bench.CamStatus, Foreground = Text, TextWrapping = TextWrapping.Wrap };
        sp.Children.Add(status);
        var actions = new WrapPanel();
        actions.Children.Add(MSp(Act("cam-live", Bench.Live ? "Livebild aus" : "Livebild an", () => _ = ToggleLive(), true)));
        actions.Children.Add(MSp(Act("cam-snap", "Foto speichern", () => _ = Snap())));
        actions.Children.Add(MSp(Act("cam-rec", "Aufnahme starten", RecStart)));
        actions.Children.Add(MSp(Act("cam-stop", "Aufnahme stoppen", RecStop)));
        sp.Children.Add(actions);
        return WCard(sp);
    }

    private void SearchCams()
    {
        var found = Webcams.List();
        Bench.Cameras.RemoveAll(c => c.Kind == CameraKind.Webcam);
        Bench.Cameras.InsertRange(0, found);
        if (Bench.Cameras.Count == 0) Bench.Selected = -1;
        else if (Bench.Selected < 0 || Bench.Selected >= Bench.Cameras.Count) Bench.Selected = 0;
        Bench.CamStatus = found.Count == 0
            ? "Keine Webcam gefunden. Eine IP-Kamera trägst du als rtsp://-Adresse ein."
            : found.Count + " Webcam gefunden.";
        Show("roboter");
    }

    private async Task SearchNet()
    {
        Bench.CamStatus = "Suche Kameras im Netzwerk …";
        PaintCam();
        try
        {
            var found = await OnvifCameras.DiscoverAsync(Onvif ?? new OnvifUdp());
            foreach (var c in found)
                if (Bench.Cameras.All(x => x.Id != c.Id)) Bench.Cameras.Add(c);
            SaveCams();
            Bench.CamStatus = found.Count == 0
                ? "Keine ONVIF-Kamera hat geantwortet. Wenn du die Adresse kennst, trag sie als rtsp:// ein."
                : found.Count + " Netzwerk-Kamera eingetragen.";
        }
        catch (Exception ex) { Bench.CamStatus = "⚠ Die Suche ist fehlgeschlagen: " + ex.Message; }
        Show("roboter");
    }

    private void AddRtsp(TextBox box)
    {
        Bench.Rtsp = box.Text ?? "";
        try
        {
            var cam = WebcamScan.FromRtsp(Bench.Rtsp);
            if (Bench.Cameras.Any(c => c.Id == cam.Id)) { Bench.CamStatus = "Die Kamera steht schon in der Liste."; Show("roboter"); return; }
            Bench.Cameras.Add(cam);
            Bench.Selected = Bench.Cameras.Count - 1;
            SaveCams();
            Bench.CamStatus = "Eingetragen: " + cam.Name;
        }
        catch (Exception ex) when (ex is ArgumentException or UriFormatException) { Bench.CamStatus = "⚠ " + ex.Message; }
        Show("roboter");
    }

    private async Task AskFfmpeg()
    {
        if (FfmpegPath is { } have && File.Exists(have)) { Bench.CamStatus = "FFmpeg ist bereit."; Show("roboter"); return; }
        var check = await _deps.CheckAsync(DependencyCatalog.Get("ffmpeg"));
        if (check.Ok && check.Path is { } found && File.Exists(found)) { Bench.CamStatus = "FFmpeg ist bereit."; Show("roboter"); return; }
        _depStudio = "CORE-09";
        _depStudioTitle = "Kameras & Roboter steuern";
        _depChecks = [check];
        _depReturn = "roboter";
        _depLog.Text = "Für Livebild, Foto und Aufnahme brauche ich FFmpeg. Trendo fragt einmal, bevor etwas installiert wird.";
        Show("studio-deps");
    }

    private async Task ToggleLive()
    {
        if (Bench.Live)
        {
            Bench.Live = false;
            _liveCts?.Cancel();
            Bench.CamStatus = "Livebild aus.";
            PaintCam();
            return;
        }
        if (SelectedCam() is null) { Bench.CamStatus = "Zuerst eine Kamera wählen."; PaintCam(); return; }
        Bench.Live = true;
        _liveCts?.Cancel();
        _liveCts = new CancellationTokenSource();
        var ct = _liveCts.Token;
        Bench.CamStatus = "Livebild an.";
        PaintCam();
        while (!ct.IsCancellationRequested && Bench.Live)
        {
            if (CurrentView != "roboter") { Bench.Live = false; break; }
            try { await GrabOnce(false, ct); }
            catch (OperationCanceledException) { break; }
            try { await Task.Delay(800, ct); } catch (OperationCanceledException) { break; }
        }
    }

    private async Task Snap() => await GrabOnce(true, CancellationToken.None);

    private async Task GrabOnce(bool save, CancellationToken ct)
    {
        var cam = SelectedCam();
        if (cam is null) { Bench.CamStatus = "Zuerst eine Kamera wählen."; PaintCam(); return; }
        try
        {
            var bytes = await CameraFrames.GrabAsync(cam, ct);
            Bench.Frame = bytes;
            if (save)
            {
                var ext = bytes.Length > 2 && bytes[0] == 0xFF && bytes[1] == 0xD8 ? ".jpg" : ".png";
                var path = Path.Combine(Fach.StoreDir(_core.DataRoot, "roboter"), $"foto-{DateTime.Now:yyyyMMdd-HHmmss-fff}{ext}");
                await File.WriteAllBytesAsync(path, bytes, ct);
                Bench.CamStatus = "Foto gespeichert: " + path;
                _core.Audit.Append(Principal.LocalOwner.Name, "camera.snap", Path.GetFileName(path));
            }
            else Bench.CamStatus = "Livebild von " + cam.Name + ".";
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            Bench.Live = false;
            Bench.CamStatus = "⚠ " + ex.Message;
        }
        PaintCam();
    }

    private void RecStart()
    {
        if (_recHandle is not null) { Bench.CamStatus = "Die Aufnahme läuft schon. Mit „Aufnahme stoppen“ beenden."; PaintCam(); return; }
        var cam = SelectedCam();
        if (cam is null) { Bench.CamStatus = "Zuerst eine Kamera wählen."; PaintCam(); return; }
        var path = Path.Combine(Fach.StoreDir(_core.DataRoot, "roboter"), $"aufnahme-{DateTime.Now:yyyyMMdd-HHmmss-fff}.mp4");
        try
        {
            _recHandle = StartRec(cam, path);
            _recPath = path;
            Bench.CamStatus = "Aufnahme läuft: " + path;
            _core.Audit.Append(Principal.LocalOwner.Name, "camera.record", Path.GetFileName(path));
        }
        catch (Exception ex) { Bench.CamStatus = "⚠ " + ex.Message; }
        PaintCam();
    }

    private void RecStop()
    {
        if (_recHandle is null) { Bench.CamStatus = "Es läuft keine Aufnahme."; PaintCam(); return; }
        try { _recHandle.Dispose(); }
        catch (Exception ex) { Bench.CamStatus = "⚠ " + ex.Message; PaintCam(); return; }
        _recHandle = null;
        Bench.CamStatus = _recPath is not null && File.Exists(_recPath) ? "Aufnahme gespeichert: " + _recPath : "Aufnahme beendet, die Datei ist nicht angekommen.";
        PaintCam();
    }

    private IDisposable StartRec(CameraDevice cam, string path)
    {
        if (StartCameraRecord is { } hook) return hook(cam, path);
        var ff = FfmpegPath ?? throw new InvalidOperationException("Dafür fehlt FFmpeg. Mit „FFmpeg einrichten“ holst du es.");
        var rec = new CameraRecorder();
        rec.Start(ff, CameraFfmpeg.RecordArgs(cam, path, OperatingSystem.IsWindows()), path);
        return rec;
    }

    private Control DrivePanel()
    {
        var sp = new StackPanel { Spacing = 6 };
        sp.Children.Add(H("Roboterarm", 14));
        sp.Children.Add(P("Simulation rechnet nur. Trockenlauf zeigt den Befehl und sendet ihn nicht. Echt sendet ihn erst nach deiner Freigabe an das verbundene Gerät.", null, 11));
        var mode = new ComboBox { Name = "rob-mode", ItemsSource = new[] { "Simulation", "Trockenlauf", "Echt" }, SelectedIndex = Bench.ModeIndex, Width = 160 };
        mode.SelectionChanged += (_, _) =>
        {
            if (mode.SelectedIndex < 0) return;
            Bench.ModeIndex = mode.SelectedIndex;
            Bench.Drive.Mode = ModeFrom(Bench.ModeIndex);
            Bench.DriveStatus = Bench.Drive.Mode switch
            {
                DriveMode.DryRun => "Trockenlauf: Befehle werden gezeigt und nicht gesendet.",
                DriveMode.Live => "Echt: jede Fahrt fragt dich vorher. Not-Aus bleibt oben.",
                _ => "Simulation: der Arm rechnet die Stellung, es bewegt sich kein Gerät.",
            };
            if (this.GetLogicalDescendants().OfType<TextBlock>().FirstOrDefault(t => t.Name == "rob-drive-out") is { } o) o.Text = Bench.DriveStatus;
        };
        var x = Box("rob-go-x", Bench.X); var y = Box("rob-go-y", Bench.Y); var z = Box("rob-go-z", Bench.Z);
        sp.Children.Add(new WrapPanel
        {
            Children =
            {
                MSp(Labeled("Steuerung", mode)), MSp(Labeled("x mm", x)), MSp(Labeled("y mm", y)), MSp(Labeled("z mm", z)),
                MSp(Act("rob-go", "Fahren", () => Go(x, y, z, mode), true)),
                MSp(Act("rob-grip-close", "Greifer schließen", () => Grip(true, mode))),
                MSp(Act("rob-grip-open", "Greifer öffnen", () => Grip(false, mode))),
            },
        });
        var pts = new TextBox { Name = "rob-live-pts", Text = Bench.Points, Width = 360, PlaceholderText = "Wegpunkte x y z, mit | getrennt" };
        sp.Children.Add(new WrapPanel { Children = { MSp(pts), MSp(Act("rob-run", "Weg abfahren", () => RunPath(pts, mode))) } });
        var host = new TextBox { Name = "rob-host", Text = Bench.Host, Width = 220, PlaceholderText = "192.168.1.50:9000" };
        sp.Children.Add(new WrapPanel
        {
            Children =
            {
                MSp(Labeled("Gerät", host)),
                MSp(Act("rob-connect", "Verbinden", () => Connect(host))),
                MSp(Act("rob-disconnect", "Trennen", Disconnect)),
            },
        });
        sp.Children.Add(new TextBlock { Name = "rob-pose", Text = PoseLine(), Foreground = Text, TextWrapping = TextWrapping.Wrap });
        sp.Children.Add(new TextBlock { Name = "rob-drive-out", Text = Bench.DriveStatus, Foreground = Text, TextWrapping = TextWrapping.Wrap });
        if (Bench.PendingId is not null && Bench.PendingCommand is { } cmd)
        {
            var preview = cmd.Contains('\n') ? cmd.Split('\n').Length + " Befehle an das Gerät" : cmd;
            sp.Children.Add(P("Diese Bewegung wirklich an das Gerät senden? " + preview));
            sp.Children.Add(new WrapPanel
            {
                Children =
                {
                    MSp(Act("rob-yes", "Ja, wirklich bewegen", ApproveMove, true)),
                    MSp(Act("rob-no", "Nein, abbrechen", DenyMove)),
                },
            });
        }
        return WCard(sp);
    }

    private static TextBox Box(string name, string text) => new() { Name = name, Text = text, Width = 80 };
    private static DriveMode ModeFrom(int index) => index switch { 1 => DriveMode.DryRun, 2 => DriveMode.Live, _ => DriveMode.Simulation };

    private void Remember(TextBox x, TextBox y, TextBox z, ComboBox mode)
    {
        Bench.X = x.Text ?? ""; Bench.Y = y.Text ?? ""; Bench.Z = z.Text ?? "";
        Bench.ModeIndex = Math.Max(0, mode.SelectedIndex);
        Bench.Drive.Mode = ModeFrom(Bench.ModeIndex);
    }

    private string PoseLine()
    {
        if (Bench.Drive.Pose is not { } j) return "Noch keine Stellung.";
        var (x, y, z) = RobotArm.Forward(new ArmConfig(), j);
        return $"Sockel {Fach.F(j.Base, 1)}° · Schulter {Fach.F(j.Shoulder, 1)}° · Ellbogen {Fach.F(j.Elbow, 1)}° · Greifer {Fach.F(x, 1)} / {Fach.F(y, 1)} / {Fach.F(z, 1)} mm";
    }

    private void Go(TextBox x, TextBox y, TextBox z, ComboBox mode)
    {
        Remember(x, y, z, mode);
        try
        {
            var j = RobotArm.Inverse(new ArmConfig(), Fach.Num(Bench.X, "x"), Fach.Num(Bench.Y, "y"), Fach.Num(Bench.Z, "z"));
            AskOrRun(MotionText.Move(j), j, $"Arm fährt nach {Bench.X} / {Bench.Y} / {Bench.Z} mm");
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException or InvalidOperationException or ApprovalException or IOException)
        { Bench.DriveStatus = "⚠ " + ex.Message; Show("roboter"); }
    }

    private void Grip(bool close, ComboBox mode)
    {
        Bench.ModeIndex = Math.Max(0, mode.SelectedIndex);
        Bench.Drive.Mode = ModeFrom(Bench.ModeIndex);
        try { AskOrRun(MotionText.Grip(close), Bench.Drive.Pose, close ? "Greifer schließen" : "Greifer öffnen"); }
        catch (Exception ex) when (ex is InvalidOperationException or ApprovalException or IOException)
        { Bench.DriveStatus = "⚠ " + ex.Message; Show("roboter"); }
    }

    private void RunPath(TextBox pts, ComboBox mode)
    {
        Bench.Points = pts.Text ?? "";
        Bench.ModeIndex = Math.Max(0, mode.SelectedIndex);
        Bench.Drive.Mode = ModeFrom(Bench.ModeIndex);
        try
        {
            var steps = RobotArm.Program(new ArmConfig(), Bench.Points.Replace('|', '\n'));
            var cmd = string.Join("\n", steps.Select(s => MotionText.Move(s.J)));
            AskOrRun(cmd, steps[^1].J, "Weg mit " + steps.Count + " Punkten fahren");
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException or InvalidOperationException or ApprovalException or IOException)
        { Bench.DriveStatus = "⚠ " + ex.Message; Show("roboter"); }
    }

    private void AskOrRun(string command, Joints? pose, string description)
    {
        if (Bench.Drive.Emergency) throw new InvalidOperationException("Not-Aus ist aktiv. Erst lösen, dann wieder fahren.");
        if (Bench.PendingId is not null) { Bench.DriveStatus = "Es liegt schon eine Freigabe vor. Erst entscheiden."; Show("roboter"); return; }
        if (Bench.Drive.Mode == DriveMode.Live)
        {
            if (Bench.Drive.Link is not { IsOpen: true }) throw new InvalidOperationException("Kein Gerät verbunden. Adresse eintragen und verbinden, oder bei Simulation bleiben.");
            var req = _core.Approvals.Request(Principal.LocalOwner, MotionText.Action, description, command);
            Bench.PendingId = req.Id;
            Bench.PendingCommand = command;
            Bench.PendingPose = pose;
            Bench.DriveStatus = "Freigabe nötig, bevor sich etwas bewegt.";
        }
        else
        {
            var report = Bench.Drive.Execute(command, pose, null, null);
            Bench.DriveStatus = report.Detail;
        }
        Show("roboter");
    }

    private void ApproveMove()
    {
        if (Bench.PendingId is null || Bench.PendingCommand is null) { Bench.DriveStatus = "Keine offene Freigabe."; Show("roboter"); return; }
        var id = Bench.PendingId; var cmd = Bench.PendingCommand; var pose = Bench.PendingPose;
        try
        {
            _core.Approvals.Approve(id, Principal.LocalOwner);
            var report = Bench.Drive.Execute(cmd, pose, _core.Approvals, id);
            Bench.DriveStatus = report.Detail;
            _core.Audit.Append(Principal.LocalOwner.Name, "robot.move", cmd.Replace('\n', ' '));
        }
        catch (Exception ex) when (ex is InvalidOperationException or ApprovalException or IOException)
        { Bench.DriveStatus = "⚠ " + ex.Message; }
        Bench.PendingId = null; Bench.PendingCommand = null; Bench.PendingPose = null;
        Show("roboter");
    }

    private void DenyMove()
    {
        if (Bench.PendingId is null) { Bench.DriveStatus = "Keine offene Freigabe."; Show("roboter"); return; }
        _core.Approvals.Deny(Bench.PendingId, Principal.LocalOwner);
        Bench.PendingId = null; Bench.PendingCommand = null; Bench.PendingPose = null;
        Bench.DriveStatus = "Abgelehnt. Es wurde nichts gesendet.";
        Show("roboter");
    }

    private void DoEstop()
    {
        Bench.Drive.Mode = ModeFrom(Bench.ModeIndex);
        Bench.Drive.EmergencyStop();
        if (Bench.PendingId is { } id)
        {
            try { _core.Approvals.Deny(id, Principal.LocalOwner); } catch (ApprovalException) { }
            Bench.PendingId = null; Bench.PendingCommand = null; Bench.PendingPose = null;
        }
        _core.Audit.Append(Principal.LocalOwner.Name, "robot.estop", Bench.Drive.Link?.Endpoint ?? "ohne Gerät");
        Bench.DriveStatus = "Not-Aus. Es geht kein Fahrbefehl mehr raus.";
        Show("roboter");
    }

    private void DoRelease()
    {
        try
        {
            Bench.Drive.ReleaseEmergency();
            Bench.DriveStatus = "Not-Aus gelöst. Echte Fahrt fragt erneut nach.";
        }
        catch (InvalidOperationException ex) { Bench.DriveStatus = ex.Message; }
        Show("roboter");
    }

    private void Connect(TextBox host)
    {
        Bench.Host = host.Text ?? "";
        try
        {
            if (string.IsNullOrWhiteSpace(Bench.Host)) throw new ArgumentException("Trag die Adresse des Geräts ein, zum Beispiel 192.168.1.50:9000.");
            Bench.Drive.Link?.Dispose();
            var link = OpenRobotLink is { } open ? open(Bench.Host) : TcpMotionLink.Open(Bench.Host);
            Bench.Drive.Link = link;
            Bench.DriveStatus = "Verbunden mit " + link.Endpoint + ". Echte Fahrt bleibt gesperrt, bis du sie freigibst.";
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or SocketException) { Bench.DriveStatus = "⚠ " + ex.Message; }
        Show("roboter");
    }

    private void Disconnect()
    {
        if (Bench.Drive.Link is null) { Bench.DriveStatus = "Es ist kein Gerät verbunden."; Show("roboter"); return; }
        var ep = Bench.Drive.Link.Endpoint;
        Bench.Drive.Link.Dispose();
        Bench.Drive.Link = null;
        Bench.DriveStatus = "Getrennt: " + ep;
        Show("roboter");
    }
}
