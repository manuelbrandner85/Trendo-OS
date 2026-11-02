using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
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

    // ---------- Scan-Arbeitsplatz (Masterprompt 131, Runde 7) ----------
    PhotoSetReport? _scanReport;
    List<string> _scanSteps = []; int _scanStepIdx = -1; bool _scanFailed; DateTime _scanStart;
    bool _scanWire;
    public PhotoSetReport? PhotoReport => _scanReport;
    public IReadOnlyList<string> ScanSteps => _scanSteps;
    public int ScanStepIndex => _scanStepIdx;
    public string? ScanModelPath => _scanModel;

    /// <summary>Fotos einlesen und prüfen (öffentlich für Tests).</summary>
    public void ScanLoad(string folder) { _photoFolder = folder.Trim().Trim('"'); _scanReport = _photoFolder.Length == 0 ? null : PhotoSet.Check(_photoFolder); Show("scan3d"); }

    private Control PhotoScanCard()
    {
        var folder = new TextBox { Name = "scan-photos-folder", PlaceholderText = "Ordner mit Fotos", Text = _photoFolder, Width = 300 };
        var video = new TextBox { Name = "scan-video-file", PlaceholderText = "Videodatei mp4, mov, mkv", Text = _scanVideo, Width = 300 };
        folder.KeyDown += (_, e) => { if (e.Key == Avalonia.Input.Key.Enter) { ScanLoad(folder.Text ?? ""); e.Handled = true; } };
        var status = new TextBlock { Name = "scan-status", Text = _scanStatus, Foreground = Text, TextWrapping = Avalonia.Media.TextWrapping.Wrap };
        var which = new TextBlock { Name = "scan-engine", Text = _scanEngine == PhotoEngine.Meshroom ? "Gewählt: Meshroom" : "Gewählt: COLMAP", Foreground = Muted, FontSize = 11, VerticalAlignment = VerticalAlignment.Center };
        var check = new Button { Name = "scan-check", Content = "Werkzeug prüfen" };
        check.Click += async (_, _) => await CheckPhotoTool();
        var run = new Button { Name = "scan-run", Content = _scanBusy ? "Rechnet …" : "Modell rechnen", Background = Accent, IsEnabled = !_scanBusy };
        run.Click += async (_, _) =>
        {
            _photoFolder = folder.Text?.Trim().Trim('"') ?? "";
            _scanVideo = video.Text?.Trim().Trim('"') ?? "";
            await RunPhotoScan();
        };

        // ── links: Fotos ──
        var photos = new StackPanel { Name = "scan-photos", Spacing = 6, Children = { H("1 · Fotos", 14), folder, Act("scan-load", "Einlesen und prüfen", () => ScanLoad(folder.Text ?? ""), _scanReport is null), P("oder ein Video, in dem du um den Gegenstand herumgehst:", Muted, 11), video } };
        if (_scanReport is { } rep)
        {
            photos.Children.Add(new TextBlock { Name = "scan-count", Text = $"{rep.Count} Fotos · {rep.Bytes / 1048576.0:0.#} MB" + (rep.Largest is { } lg ? $" · bis {lg.W} × {lg.H} Pixel" : ""), Foreground = rep.Ready ? Brush.Parse("#22B35E") : Brush.Parse("#F97316"), FontWeight = FontWeight.SemiBold });
            var wi = 0;
            foreach (var w in rep.Warnings) photos.Children.Add(new TextBlock { Name = $"scan-warn-{wi++}", Text = "⚠ " + w, Foreground = Brush.Parse("#F5A524"), FontSize = 11.5, TextWrapping = TextWrapping.Wrap });
            if (rep.Count > 0 && rep.Warnings.Count == 0) photos.Children.Add(P("✓ Gute Serie – los geht's mit „Modell rechnen“.", Brush.Parse("#22B35E"), 11.5));
            var thumbs = new WrapPanel { Name = "scan-thumbs" }; var ti = 0;
            foreach (var f in rep.Files.Take(24))
            {
                Control pic;
                try { using var st = File.OpenRead(f); pic = new Image { Source = Avalonia.Media.Imaging.Bitmap.DecodeToWidth(st, 120), Width = 60, Height = 45, Stretch = Stretch.UniformToFill }; }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or NotSupportedException or NullReferenceException) { pic = new Border { Width = 60, Height = 45, Background = Brush.Parse("#22000000"), Child = new TextBlock { Text = "▧", Foreground = Muted, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } }; }
                var tile = new Border { Name = $"scan-thumb-{ti++}", Margin = new Thickness(0, 0, 4, 4), CornerRadius = new CornerRadius(4), ClipToBounds = true, Child = pic };
                ToolTip.SetTip(tile, Path.GetFileName(f)); thumbs.Children.Add(tile);
            }
            if (rep.Count > 24) thumbs.Children.Add(new TextBlock { Text = $"+ {rep.Count - 24}", Foreground = Muted, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0) });
            photos.Children.Add(thumbs);
        }
        else photos.Children.Add(P("Mindestens 3 Fotos aus verschiedenen Richtungen – besser 20 bis 80 mit viel Überlappung, gleichmäßiges Licht, ohne Spiegelungen.", Muted, 11.5));

        // ── Mitte: Rechnung ──
        var steps = new StackPanel { Name = "scan-steps", Spacing = 2 };
        for (var i = 0; i < _scanSteps.Count; i++)
        {
            var (mark, col) = i < _scanStepIdx || (i == _scanStepIdx && _scanModel is not null && !_scanBusy) ? ("✓", "#22B35E") : i == _scanStepIdx ? (_scanFailed ? ("✕", "#F87171") : ("◐", "#E0A82E")) : ("○", "#8A8F98");
            steps.Children.Add(new TextBlock { Name = $"scan-step-{i}", Text = $"{mark}  {_scanSteps[i]}", Foreground = Brush.Parse(col), FontSize = 12, FontWeight = i == _scanStepIdx ? FontWeight.SemiBold : FontWeight.Normal });
        }
        var done = _scanSteps.Count == 0 ? 0 : _scanModel is not null && !_scanBusy && !_scanFailed ? _scanSteps.Count : Math.Max(0, _scanStepIdx);
        var calc = new StackPanel { Name = "scan-calc", Spacing = 6, Children = {
            H("2 · Rechnung", 14),
            new WrapPanel { Children = { MSp(Act("scan-use-colmap", "COLMAP", () => { _scanEngine = PhotoEngine.Colmap; Show("scan3d"); }, _scanEngine == PhotoEngine.Colmap)), MSp(Act("scan-use-meshroom", "Meshroom", () => { _scanEngine = PhotoEngine.Meshroom; Show("scan3d"); }, _scanEngine == PhotoEngine.Meshroom)), MSp(which), MSp(check) } },
            new WrapPanel { Children = { MSp(run), MSp(Act("scan-stop", "Stopp", StopScan)) } },
            new ProgressBar { Name = "scan-progress", Maximum = Math.Max(1, _scanSteps.Count), Value = done, Height = 6 },
            new TextBlock { Name = "scan-elapsed", Text = _scanSteps.Count == 0 ? "Noch nicht gerechnet. Die Rechnung läuft auf deinem PC und kann je nach Fotos von Minuten bis Stunden dauern." : $"Schritt {Math.Min(_scanSteps.Count, Math.Max(1, _scanStepIdx + 1))} von {_scanSteps.Count}" + (_scanBusy ? $" · seit {(DateTime.Now - _scanStart):m\\:ss} min" : ""), Foreground = Muted, FontSize = 11, TextWrapping = TextWrapping.Wrap },
            steps, status } };

        // ── rechts: Ergebnis ──
        var result = new StackPanel { Name = "scan-result", Spacing = 6, Children = { H("3 · Ergebnis", 14) } };
        Mesh? mesh = null;
        if (_scanModel is not null && File.Exists(_scanModel)) try { mesh = Mesh3D.ReadMeshFile(_scanModel); } catch (Exception e) when (e is IOException or InvalidDataException or FormatException) { }
        if (mesh is not null)
        {
            var view = new MeshView { Name = "scan-view", Model = mesh, Wire = _scanWire, Height = 260, Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.SizeAll) };
            result.Children.Add(new Border { CornerRadius = new CornerRadius(10), ClipToBounds = true, Background = Brush.Parse("#FF101826"), Child = view });
            result.Children.Add(new TextBlock { Name = "scan-facts", Text = $"{mesh.Triangles:N0} Dreiecke · Maus ziehen dreht, Rad zoomt", Foreground = Muted, FontSize = 11 });
            var views = new WrapPanel();
            foreach (var (v, l) in new[] { ("schraeg", "Schräg"), ("vorne", "Vorne"), ("oben", "Oben"), ("seite", "Seite") }) { var vv = v; views.Children.Add(MSp(Act("scan-view-" + v, l, () => view.SetView(vv)))); }
            views.Children.Add(MSp(Act("scan-wire", _scanWire ? "Fläche" : "Drahtgitter", () => { _scanWire = !_scanWire; Show("scan3d"); })));
            result.Children.Add(views);
            var exports = new WrapPanel();
            foreach (var (ext, label) in new[] { ("obj", "OBJ"), ("stl", "STL (3D-Druck)"), ("glb", "GLB (Web, AR)") }) { var e = ext; exports.Children.Add(MSp(Act("scan-export-" + ext, label, () => Try("scan3d", () => ScanExport(e))))); }
            result.Children.Add(P("Speichern als:", Muted, 11)); result.Children.Add(exports);
        }
        else result.Children.Add(P("Nach der Rechnung siehst du das Modell hier in 3D und kannst es als OBJ, STL oder GLB speichern.", Muted, 11.5));
        result.Children.Add(Act("scan-open", "Im 3D-Studio öffnen", OpenScanInStudio));

        Border Col(Control c) => new() { CornerRadius = new CornerRadius(14), Background = TileBg, BorderBrush = EdgeLit, BorderThickness = new Thickness(1), Padding = new Thickness(12), VerticalAlignment = VerticalAlignment.Top, Child = c };
        return new Grid { Name = "scan-workbench", ColumnDefinitions = new ColumnDefinitions("*,12,*,12,*"), Children = { Col(photos), Pos(Col(calc), 2), Pos(Col(result), 4) } };
    }

    /// <summary>Modell speichern (OBJ, STL oder GLB) im Ordner „Scan“; vorhandene Dateien bleiben (Nummer dahinter).</summary>
    public string ScanExport(string ext)
    {
        if (_scanModel is null || !File.Exists(_scanModel)) throw new InvalidOperationException("Noch kein Modell.");
        var mesh = Mesh3D.ReadMeshFile(_scanModel);
        var stem = Path.Combine(WorkFolder("Scan"), "Scan " + DateTime.Now.ToString("yyyy-MM-dd")); var f = stem + "." + ext;
        for (var n = 2; File.Exists(f); n++) f = $"{stem} ({n}).{ext}";
        switch (ext) { case "stl": Mesh3D.WriteStl(mesh, f); break; case "glb": Mesh3D.WriteGlb(mesh, f); break; default: Mesh3D.WriteObj(mesh, f); break; }
        _core.Audit.Append(Principal.LocalOwner.Name, "scan.export", Path.GetFileName(f));
        ScanStatus("Gespeichert: " + f); NotifyFileResult("Gespeichert: " + f);
        return f;
    }

    /// <summary>Fortschritt der Rechnung: Schritt-Titel schalten die Liste weiter, alles andere steht unter den Schritten.</summary>
    void ScanProgress(string text)
    {
        var idx = _scanSteps.IndexOf(text);
        if (idx < 0) { ScanStatus(text); return; }
        void Apply() { _scanStepIdx = idx; _scanStatus = text; if (CurrentView == "scan3d") Show("scan3d"); }
        if (Dispatcher.UIThread.CheckAccess()) Apply(); else _ui.Post(Apply);
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
            _scanBusy = true; _scanFailed = false; _scanModel = null; _scanStart = DateTime.Now;
            _scanSteps = [.. PhotoScan.Steps(req, tool, ffmpeg).Select(x => x.Title)]; _scanStepIdx = 0; Show("scan3d");
            _scanCts = new CancellationTokenSource();
            var result = await PhotoScan.RunAsync(req, tool, ffmpeg, ScanRunner ?? _deps.Runner, ScanProgress, _scanCts.Token);
            _scanModel = result.ObjPath; _scanStepIdx = _scanSteps.Count - 1; _scanBusy = false;
            _scanStatus = result.Note + " Rechts siehst du es in 3D – oder mit „Im 3D-Studio öffnen“."; Show("scan3d");
            _core.Audit.Append(Principal.LocalOwner.Name, "scan.photo", _scanEngine + " dreiecke=" + result.Triangles);
        }
        catch (OperationCanceledException) { _scanFailed = true; _scanBusy = false; _scanStatus = "Abgebrochen. Es liegt kein neues Modell vor."; Show("scan3d"); }
        catch (Exception ex) { _scanFailed = _scanSteps.Count > 0; _scanBusy = false; _scanStatus = ex.Message; Show("scan3d"); }
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
        else _ui.Post(Apply);
    }
}
