using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Trendo.Core;

namespace Trendo.App;

/// <summary>SPEC-09: Fotos oder ein Video zu einem 3D-Modell, danach im 3D-Studio öffnen.</summary>
public partial class MainWindow
{
    private string _photoFolder = "";
    private string _scanVideo = "";
    private PhotoEngine _scanEngine = PhotoEngine.Colmap;
    private string _scanStatus = "";
    private string? _scanModel;
    private bool _scanBusy;
    private CancellationTokenSource? _scanCts;

    /// <summary>Für Tests: Programmdatei, ohne die echte Installation zu suchen.</summary>
    public string? ScanToolOverride { get; set; }
    /// <summary>Für Tests: FFmpeg, ohne die echte Installation zu suchen.</summary>
    public string? ScanFfmpegOverride { get; set; }
    /// <summary>Für Tests: führt die Rechnung aus, ohne COLMAP oder Meshroom zu starten.</summary>
    public IProcessRunner? ScanRunner { get; set; }

    private Control PhotoScanCard()
    {
        var folder = new TextBox { Name = "scan-photos-folder", PlaceholderText = "Ordner mit Fotos", Text = _photoFolder, Width = 360 };
        var video = new TextBox { Name = "scan-video-file", PlaceholderText = "Videodatei mp4, mov, mkv", Text = _scanVideo, Width = 360 };
        var status = new TextBlock { Name = "scan-status", Text = _scanStatus, Foreground = Text, TextWrapping = Avalonia.Media.TextWrapping.Wrap };
        var which = new TextBlock
        {
            Name = "scan-engine",
            Text = _scanEngine == PhotoEngine.Meshroom ? "Gewählt: Meshroom" : "Gewählt: COLMAP",
            Foreground = Muted,
            FontSize = 11,
            VerticalAlignment = VerticalAlignment.Center,
        };
        var check = new Button { Name = "scan-check", Content = "Werkzeug prüfen" };
        check.Click += async (_, _) => await CheckPhotoTool();
        var run = new Button { Name = "scan-run", Content = "Modell rechnen", Background = Accent };
        run.Click += async (_, _) =>
        {
            _photoFolder = folder.Text?.Trim().Trim('"') ?? "";
            _scanVideo = video.Text?.Trim().Trim('"') ?? "";
            await RunPhotoScan();
        };
        return WCard(new StackPanel
        {
            Spacing = 6,
            Children =
            {
                H("Aus Fotos ein Modell", 14),
                P("Mindestens 3 Fotos aus verschiedenen Richtungen, oder ein Video, in dem du um den Gegenstand herumgehst. COLMAP richtet Trendo nach deiner Zustimmung ein. Meshroom geht über die offizielle Seite, wenn du es noch nicht hast. Die längste Seite wird auf 20 cm gesetzt, weil Fotos keine echten Maße haben.", null, 11),
                new WrapPanel { Children = { MSp(Act("scan-use-colmap", "COLMAP", () => { _scanEngine = PhotoEngine.Colmap; Show("scan3d"); }, _scanEngine == PhotoEngine.Colmap)), MSp(Act("scan-use-meshroom", "Meshroom", () => { _scanEngine = PhotoEngine.Meshroom; Show("scan3d"); }, _scanEngine == PhotoEngine.Meshroom)), MSp(which), MSp(check) } },
                folder,
                video,
                new WrapPanel { Children = { MSp(run), MSp(Act("scan-stop", "Stopp", StopScan)), MSp(Act("scan-open", "Im 3D-Studio öffnen", OpenScanInStudio)) } },
                status,
            },
        });
    }

    private void StopScan()
    {
        if (_scanCts is null) { ScanStatus("Es läuft gerade keine Rechnung."); return; }
        _scanCts.Cancel();
        ScanStatus("Stoppe …");
    }

    private void OpenScanInStudio()
    {
        if (_scanModel is null || !File.Exists(_scanModel))
        {
            ScanStatus("Noch kein Modell. Lege Fotos oder ein Video an und klicke auf „Modell rechnen“.");
            return;
        }
        Import3DFile(_scanModel);
    }

    private async Task CheckPhotoTool()
    {
        var path = await ReadyPhotoTool();
        if (path is null) return;
        ScanStatus((_scanEngine == PhotoEngine.Meshroom ? "Meshroom" : "COLMAP") + " ist da.");
    }

    private async Task RunPhotoScan()
    {
        if (_scanBusy) { ScanStatus("Die Rechnung läuft schon."); return; }
        var video = string.IsNullOrWhiteSpace(_scanVideo) ? null : _scanVideo;
        var photos = string.IsNullOrWhiteSpace(_photoFolder) ? null : _photoFolder;
        if (video is null && photos is null) { ScanStatus("Wähle einen Ordner mit mindestens 3 Fotos oder ein Video."); return; }
        try
        {
            if (video is null)
            {
                var n = PhotoScan.ListImages(photos!).Count;
                if (n < 3) { ScanStatus(PhotoScan.TooFew(n)); return; }
            }
            else if (!File.Exists(video)) { ScanStatus("Dieses Video gibt es nicht."); return; }

            var tool = await ReadyPhotoTool();
            if (tool is null) return;
            if (!OperatingSystem.IsWindows() && tool.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                ScanStatus("Diese Programmdatei ist für Windows. Auf diesem System brauchst du colmap oder meshroom_batch aus der Paketverwaltung.");
                return;
            }
            string? ffmpeg = null;
            if (video is not null)
            {
                ffmpeg = await ReadyFfmpeg();
                if (ffmpeg is null) return;
            }
            var req = new PhotoRequest(Path.Combine(WorkFolder("Scan"), DateTime.Now.ToString("yyyy-MM-dd-HHmmss") + "-" + Guid.NewGuid().ToString("N")[..4]), photos, video, _scanEngine);
            _scanBusy = true;
            _scanCts = new CancellationTokenSource();
            var result = await PhotoScan.RunAsync(req, tool, ffmpeg, ScanRunner ?? _deps.Runner, ScanStatus, _scanCts.Token);
            _scanModel = result.ObjPath;
            ScanStatus(result.Note + " Mit „Im 3D-Studio öffnen“ siehst du es.");
            _core.Audit.Append(Principal.LocalOwner.Name, "scan.photo", _scanEngine + " dreiecke=" + result.Triangles);
        }
        catch (OperationCanceledException) { ScanStatus("Abgebrochen. Es liegt kein neues Modell vor."); }
        catch (Exception ex) { ScanStatus(ex.Message); }
        finally { _scanBusy = false; _scanCts?.Dispose(); _scanCts = null; }
    }

    private async Task<string?> ReadyPhotoTool()
    {
        var meshroom = _scanEngine == PhotoEngine.Meshroom;
        var id = meshroom ? "meshroom" : "colmap";
        var name = meshroom ? "Meshroom" : "COLMAP";
        if (ScanToolOverride is { } over && File.Exists(over)) return over;
        var check = await _deps.CheckAsync(DependencyCatalog.Get(id));
        if (check.Ok && check.Path is { } p && File.Exists(p)) return p;
        if (!_opt.Isolated && Cli.Find(meshroom ? "meshroom_batch" : "colmap") is { } cli) return cli;
        ShowScanAsk(check, $"{name} fehlt. Trendo fragt einmal, bevor etwas installiert wird.");
        return null;
    }

    private async Task<string?> ReadyFfmpeg()
    {
        if (ScanFfmpegOverride is { } over && File.Exists(over)) return over;
        if (ToolPath("ffmpeg") is { } p && File.Exists(p)) return p;
        if (!_opt.Isolated && Cli.Find("ffmpeg") is { } cli) return cli;
        var check = await _deps.CheckAsync(DependencyCatalog.Get("ffmpeg"));
        if (check.Ok && check.Path is { } found && File.Exists(found)) return found;
        ShowScanAsk(check, "FFmpeg fehlt. Trendo fragt einmal, bevor etwas installiert wird.");
        return null;
    }

    private void ShowScanAsk(ToolCheck check, string log)
    {
        _depStudio = "SPEC-09";
        _depStudioTitle = "Gegenstände einscannen";
        _depChecks = [check];
        _depReturn = "scan3d";
        _depLog.Text = log;
        Show("studio-deps");
    }

    private void ScanStatus(string text)
    {
        _scanStatus = text;
        void Apply()
        {
            foreach (var t in this.GetLogicalDescendants().OfType<TextBlock>())
                if (t.Name == "scan-status") t.Text = text;
        }
        if (Dispatcher.UIThread.CheckAccess()) Apply();
        else Dispatcher.UIThread.Post(Apply);
    }
}
