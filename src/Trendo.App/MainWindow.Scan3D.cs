using Avalonia.Controls;
using Avalonia.Layout;
using Trendo.Core;

namespace Trendo.App;

/// <summary>SPEC-09: Fotos oder ein Video werden mit Meshroom oder COLMAP zu einem 3D-Modell. Installation nur nach Nachfrage.</summary>
public partial class MainWindow
{
    private string? _photoFolder;
    private bool _scanBusy;
    /// <summary>Für Tests: ersetzt Meshroom und COLMAP. Muss die OBJ-Datei selbst schreiben.</summary>
    public Func<IReadOnlyList<string>, string, (int Exit, string Err, string? Obj)>? ScanRun { get; set; }
    /// <summary>Für Tests: ersetzt FFmpeg beim Zerlegen eines Videos.</summary>
    public Func<string, string[], (int Exit, string Err)>? ScanFrames { get; set; }
    /// <summary>Für Tests: ersetzt die Suche nach Meshroom und COLMAP. null heißt: Programm fehlt.</summary>
    public Func<string, string?>? ScanToolOverride { get; set; }

    private Control PhotoScanPanel()
    {
        var box = new StackPanel { Spacing = 6 };
        box.Children.Add(H("Aus Fotos ein Modell", 14));
        box.Children.Add(P("Mindestens drei Fotos aus verschiedenen Richtungen, oder ein Video. Meshroom oder COLMAP rechnet das Modell. Fehlt das Programm, öffne ich nur die offizielle Seite."));
        var folder = new TextBox { Name = "scan-photos-folder", PlaceholderText = "Ordner mit Fotos", Text = _photoFolder ?? "", Width = 360 };
        var video = new TextBox { Name = "scan-video", PlaceholderText = "Videodatei", Width = 280 };
        var every = new TextBox { Name = "scan-every", Text = "1", Width = 50 };
        box.Children.Add(new WrapPanel { Children = { ScanLabel("Fotos", folder), Act("scan-count", "Fotos zählen", CountPhotos) } });
        box.Children.Add(new WrapPanel { Children = { ScanLabel("Video", video), ScanLabel("Alle … Sekunden ein Bild", every), Act("scan-frames", "Bilder aus dem Video", Frames) } });
        box.Children.Add(new WrapPanel { Children = { Act("scan-tools", "Werkzeuge prüfen", CheckScanTools), Act("scan-go", "Modell rechnen", ComputeModel, true) } });
        return Card(box);
    }

    private static StackPanel ScanLabel(string label, Control box) => new() { Orientation = Orientation.Horizontal, Spacing = 4, Margin = new Avalonia.Thickness(0, 0, 12, 4), Children = { new TextBlock { Text = label, Foreground = Muted, FontSize = 12, VerticalAlignment = VerticalAlignment.Center }, box } };

    private string? FieldText(string name) => Avalonia.LogicalTree.LogicalExtensions.GetLogicalDescendants(this).OfType<TextBox>().FirstOrDefault(t => t.Name == name)?.Text;

    private string PhotoFolder()
    {
        var t = FieldText("scan-photos-folder");
        if (!string.IsNullOrWhiteSpace(t)) return t.Trim().Trim('"');
        if (!string.IsNullOrWhiteSpace(_photoFolder)) return _photoFolder;
        throw new ArgumentException("Bitte den Ordner mit den Fotos angeben.");
    }

    private void CountPhotos()
    {
        try
        {
            var folder = PhotoFolder();
            _photoFolder = folder;
            var n = PhotoScan.ListPhotos(folder).Count;
            Say(n == 1 ? "1 Foto." : n + " Fotos. Das reicht für ein Modell.", "scan3d");
        }
        catch (ArgumentException ex) { Say(ex.Message, "scan3d"); }
    }

    private async Task Frames()
    {
        var video = (FieldText("scan-video") ?? "").Trim().Trim('"');
        if (!File.Exists(video)) { Say("Die Videodatei gibt es nicht.", "scan3d"); return; }
        var raw = (FieldText("scan-every") ?? "1").Trim().Replace(',', '.');
        if (!int.TryParse(raw, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var sec) || sec < 1 || sec > 30)
        { Say("Bitte eine ganze Zahl von 1 bis 30 Sekunden.", "scan3d"); return; }
        var dir = Path.Combine(Fach.StoreDir(_core.DataRoot, "scan3d"), "bilder-" + DateTime.Now.ToString("yyyyMMdd-HHmmss"));
        Directory.CreateDirectory(dir);
        var args = PhotoScan.FrameArgs(video, dir, sec);
        if (ScanFrames is { } hook)
        {
            var (exit, err) = hook(video, args);
            if (exit != 0) { Say(string.IsNullOrWhiteSpace(err) ? "Aus dem Video wurden keine Bilder." : err, "scan3d"); return; }
        }
        else
        {
            if (_opt.Isolated || FfmpegPath is not { } ff)
            { Say("Dafür fehlt FFmpeg. Unter Zusatzprogramme kannst du es einrichten. Es wurden keine Bilder erzeugt.", "scan3d"); return; }
            var r = await Task.Run(() => Cli.Run(ff, args, dir, 120_000));
            if (!r.Ok) { Say(string.IsNullOrWhiteSpace(r.Err) ? "FFmpeg hat abgebrochen. Es wurden keine Bilder erzeugt." : r.Err.Trim(), "scan3d"); return; }
        }
        try
        {
            var n = PhotoScan.ListPhotos(dir).Count;
            _photoFolder = dir;
            Say(n + " Fotos aus dem Video. Als Nächstes „Modell rechnen“.", "scan3d");
        }
        catch (ArgumentException ex) { Say(ex.Message, "scan3d"); }
    }

    private string? ScanExe(string id)
    {
        if (ScanToolOverride is { } over) return over(id);
        var saved = ToolPath(id);
        if (saved is not null && File.Exists(saved)) return saved;
        if (_opt.Isolated) return null;
        return Cli.Find(id == "meshroom" ? "meshroom_batch" : "colmap");
    }

    private void CheckScanTools()
    {
        var found = new List<string>();
        if (ScanExe("meshroom") is not null) found.Add("Meshroom");
        if (ScanExe("colmap") is not null) found.Add("COLMAP");
        if (found.Count > 0) { Say("Gefunden: " + string.Join(" und ", found) + ". Es wird nichts installiert.", "scan3d"); return; }
        AskScanTools();
    }

    private void AskScanTools()
    {
        _depReturn = "scan3d";
        _depStudio = "SPEC-09";
        _depStudioTitle = StudioCatalog.NameOf("SPEC-09", "Gegenstände einscannen");
        _depChecks = [];
        foreach (var id in new[] { "meshroom", "colmap" })
        {
            if (ScanExe(id) is not null) continue;
            _depChecks.Add(new ToolCheck(DependencyCatalog.Get(id), ToolState.Missing, null, null, "nicht gefunden"));
        }
        if (_depChecks.Count == 0) { Say("Meshroom oder COLMAP ist da. Es wird nichts installiert.", "scan3d"); return; }
        Show("studio-deps");
    }

    private async Task ComputeModel()
    {
        if (_scanBusy) { Say("Die Rechnung läuft schon.", "scan3d"); return; }
        string folder;
        IReadOnlyList<string> photos;
        try { folder = PhotoFolder(); _photoFolder = folder; photos = PhotoScan.ListPhotos(folder); }
        catch (ArgumentException ex) { Say(ex.Message, "scan3d"); return; }
        var work = Path.Combine(Fach.StoreDir(_core.DataRoot, "scan3d"), "lauf-" + DateTime.Now.ToString("yyyyMMdd-HHmmss"));
        Directory.CreateDirectory(work);
        if (ScanRun is { } hook)
        {
            _scanBusy = true;
            try
            {
                var (exit, err, obj) = await Task.Run(() => hook(photos, work));
                if (exit != 0 || string.IsNullOrWhiteSpace(obj) || !File.Exists(obj))
                { Say(string.IsNullOrWhiteSpace(err) ? "Es ist kein Modell entstanden." : err, "scan3d"); return; }
                OpenScan(obj, hull: false);
                _core.Audit.Append(Principal.LocalOwner.Name, "scan.model", "netz " + photos.Count);
            }
            finally { _scanBusy = false; }
            return;
        }
        var meshroom = ScanExe("meshroom");
        var colmap = ScanExe("colmap");
        if (meshroom is null && colmap is null) { AskScanTools(); return; }
        _scanBusy = true;
        var tool = meshroom is not null ? "Meshroom" : "COLMAP";
        Say(tool + " rechnet. Das kann einige Minuten dauern.", "scan3d");
        try
        {
            var (ok, msg, obj, hull) = await Task.Run(() => meshroom is not null ? RunMeshroom(meshroom, folder, work) : RunColmap(colmap!, folder, work));
            if (!ok || obj is null) { Say(msg, "scan3d"); return; }
            OpenScan(obj, hull);
            _core.Audit.Append(Principal.LocalOwner.Name, "scan.model", (hull ? "huelle " : "netz ") + photos.Count);
        }
        finally { _scanBusy = false; }
    }

    private (bool Ok, string Msg, string? Obj, bool Hull) RunMeshroom(string exe, string input, string work)
    {
        var r = Cli.Run(exe, PhotoScan.MeshroomArgs(input, work), work, 1_200_000);
        if (!r.Ok) return (false, Fail("Meshroom", r.Err), null, false);
        try { var (obj, hull) = PhotoScan.BuildObj(work); return (true, "", obj, hull); }
        catch (Exception ex) when (ex is InvalidOperationException or IOException) { return (false, ex.Message, null, false); }
    }

    private (bool Ok, string Msg, string? Obj, bool Hull) RunColmap(string exe, string input, string work)
    {
        Directory.CreateDirectory(Path.Combine(work, "sparse"));
        foreach (var step in PhotoScan.ColmapSteps(input, work))
        {
            var r = Cli.Run(exe, step, work, 600_000);
            if (r.Ok) continue;
            if (step[0] == "poisson_mesher") break;
            return (false, Fail("COLMAP " + step[0], r.Err), null, false);
        }
        try { var (obj, hull) = PhotoScan.BuildObj(work); return (true, "", obj, hull); }
        catch (Exception ex) when (ex is InvalidOperationException or IOException) { return (false, ex.Message, null, false); }
    }

    private static string Fail(string tool, string err)
    {
        var t = err.Trim();
        if (t.Length > 280) t = t[..280];
        return t.Length == 0 ? tool + " hat abgebrochen. Es ist kein Modell entstanden." : tool + " hat abgebrochen: " + t;
    }

    private void OpenScan(string path, bool hull)
    {
        Import3DFile(path);
        if (CurrentView != "3d") return;
        if (!hull) return;
        _w45Msg += " Es gab nur Punkte, keine Flächen. Das ist die äußere Hülle, kein vollständiges Foto-Modell.";
        Show("3d");
    }
}
