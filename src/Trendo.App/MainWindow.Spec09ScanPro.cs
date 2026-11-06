using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Trendo.Core;

namespace Trendo.App;

/// <summary>Einscannen wie in Meshroom und RealityCapture (Profi 4 + Claude 4, 06.11.2026): Kamerapositionen sichtbar, Modell beschneiden, Reste entfernen,
/// Löcher schließen, echte Größe (direkt oder über einen bekannten Gegenstand), Texturqualität, gute Fotos auswählen.</summary>
public partial class MainWindow
{
    bool _scanShowCams = true;
    int _scanTex = 4096;
    List<(string File, bool Keep, string Why)>? _scanRank;
    public int ScanTextureSide { get => _scanTex; set => _scanTex = ScanPro.TextureSides.Contains(value) ? value : 4096; }
    public IReadOnlyList<(string File, bool Keep, string Why)>? ScanPhotoRanking => _scanRank;
    public void ScanSetModel(string objPath) { _scanModel = objPath; Show("scan3d"); }

    /// <summary>Claude 4 / Profi 4: gute Fotos auswählen – unscharfe und doppelte werden markiert.</summary>
    public void ScanRankPhotos()
    {
        if (_scanReport is not { } rep || rep.Files.Count == 0) { _scanStatus = "Erst Fotos einlesen."; Show("scan3d"); return; }
        _scanRank = ScanPro.RankPhotos(rep.Files, f => { try { var head = new byte[65536]; using var s = File.OpenRead(f); var n = s.Read(head, 0, head.Length); var (w, h) = XrPlus.ImageSize(head[..n]); return w > 0 && h > 0 ? (w, h) : null; } catch (Exception e) when (e is IOException or ArgumentException or IndexOutOfRangeException) { return null; } });
        var bad = _scanRank.Count(r => !r.Keep);
        _scanStatus = bad == 0 ? "✓ Alle Fotos sehen brauchbar aus." : $"{bad} von {_scanRank.Count} Fotos aussortieren empfohlen – „Nur gute verwenden“ übernimmt den Rest.";
        Show("scan3d");
    }

    /// <summary>Nur die guten Fotos in einen eigenen Trendo-Ordner kopieren und für die Rechnung verwenden (der Original-Ordner bleibt unberührt).</summary>
    public void ScanUseGoodPhotos()
    {
        if (_scanRank is null) ScanRankPhotos();
        if (_scanRank is null) return;
        var keep = _scanRank.Where(r => r.Keep).Select(r => r.File).ToList();
        if (keep.Count < 3) { _scanStatus = PhotoScan.TooFew(keep.Count); Show("scan3d"); return; }
        var dir = Path.Combine(WorkFolder("Scan"), "auswahl-" + DateTime.Now.ToString("yyyyMMdd-HHmmss")); Directory.CreateDirectory(dir);
        foreach (var f in keep) File.Copy(f, Path.Combine(dir, Path.GetFileName(f)), true);
        _core.Audit.Append(Principal.LocalOwner.Name, "scan.auswahl", $"{keep.Count} von {_scanRank.Count} Fotos");
        _scanRank = null; ScanLoad(dir);
        _scanStatus = $"✓ {keep.Count} gute Fotos übernommen (Kopie) – jetzt „Modell rechnen“.";
        Show("scan3d");
    }

    /// <summary>Modell bearbeiten (beschneiden, Reste, Löcher, Größe): vorher sichern, damit „Rückgängig“ geht.</summary>
    public void ScanEdit(string what, Func<Mesh, Mesh> change)
    {
        if (_scanModel is null || !File.Exists(_scanModel)) { _scanStatus = "Noch kein Modell."; Show("scan3d"); return; }
        try
        {
            var mesh = Mesh3D.ReadMeshFile(_scanModel); var before = mesh.Triangles;
            var o = change(mesh);
            File.Copy(_scanModel, Path.Combine(Path.GetDirectoryName(_scanModel)!, "modell-vorher.obj"), true);
            Mesh3D.WriteObj(o, _scanModel);
            if (what.StartsWith("Größe", StringComparison.Ordinal)) _scanShowCams = false; // Kameras liegen noch im alten Maßstab
            _scanStatus = $"✓ {what}: {before:N0} → {o.Triangles:N0} Dreiecke. „Rückgängig“ holt den vorigen Stand.";
            _core.Audit.Append(Principal.LocalOwner.Name, "scan.bearbeitet", what);
        }
        catch (Exception e) when (e is InvalidOperationException or ArgumentException or IOException) { _scanStatus = "⚠ " + e.Message; }
        Show("scan3d");
    }
    public void ScanUndo()
    {
        var prev = _scanModel is null ? null : Path.Combine(Path.GetDirectoryName(_scanModel)!, "modell-vorher.obj");
        if (prev is null || !File.Exists(prev)) { _scanStatus = "Nichts zum Rückgängigmachen."; Show("scan3d"); return; }
        File.Copy(prev, _scanModel!, true); File.Delete(prev); _scanStatus = "Rückgängig."; Show("scan3d");
    }

    /// <summary>Werkzeuge unter dem Ergebnis: Kameras, Beschneiden, Reste, Löcher, echte Größe.</summary>
    private Control ScanToolsCard(Mesh mesh)
    {
        var sp = new StackPanel { Name = "scan-tools", Spacing = 6 };
        var (lo, hi) = ScanPro.Bounds(mesh);
        sp.Children.Add(new TextBlock { Name = "scan-dims", Text = $"Größe jetzt: {Part3D.Num(hi.X - lo.X)} × {Part3D.Num(hi.Y - lo.Y)} × {Part3D.Num(hi.Z - lo.Z)} cm", Foreground = Text, FontSize = 12 });
        var cams = _scanModel is null ? [] : ScanPro.LoadCameras(Path.GetDirectoryName(_scanModel)!);
        if (cams.Count > 0) sp.Children.Add(Act("scan-cams", _scanShowCams ? $"📷 {cams.Count} Kameras ausblenden" : $"📷 {cams.Count} Kameras zeigen", () => { _scanShowCams = !_scanShowCams; Show("scan3d"); }));
        // Beschneiden: unten (Tisch/Boden) und rundum
        var bottom = new Slider { Name = "scan-crop-bottom", Minimum = 0, Maximum = 40, Value = 0, Width = 160 };
        var side = new Slider { Name = "scan-crop-side", Minimum = 0, Maximum = 40, Value = 0, Width = 160 };
        sp.Children.Add(H("Beschneiden", 12));
        sp.Children.Add(new WrapPanel { Children = { new TextBlock { Text = "unten %", Width = 70, Foreground = Muted, FontSize = 11.5, VerticalAlignment = VerticalAlignment.Center }, bottom } });
        sp.Children.Add(new WrapPanel { Children = { new TextBlock { Text = "rundum %", Width = 70, Foreground = Muted, FontSize = 11.5, VerticalAlignment = VerticalAlignment.Center }, side } });
        sp.Children.Add(new WrapPanel { Children = {
            Act("scan-crop", "✂ Abschneiden", () => { double b = bottom.Value / 100, s = side.Value / 100; ScanEdit("Beschnitten", m => ScanPro.Crop(m, (s, 1 - s), (s, 1 - s), (b, 1))); }),
            new Border { Width = 4 }, Act("scan-largest", "Kleine Reste entfernen", () => ScanEdit("Reste entfernt", ScanPro.KeepLargest)),
            new Border { Width = 4 }, Act("scan-holes", "Löcher schließen", () => ScanEdit("Löcher geschlossen", m => ScanPro.FillHoles(m).Mesh)),
            new Border { Width = 4 }, Act("scan-undo", "Rückgängig", ScanUndo) } });
        // Echte Größe
        sp.Children.Add(H("Echte Größe", 12));
        var axis = new ComboBox { Name = "scan-size-axis", ItemsSource = new[] { "Längste Seite", "Breite (X)", "Tiefe (Y)", "Höhe (Z)" }, SelectedIndex = 0, Width = 130 };
        var cm = new TextBox { Name = "scan-size-cm", PlaceholderText = "cm", Width = 70 };
        sp.Children.Add(new WrapPanel { Children = { axis, new Border { Width = 4 }, cm, new Border { Width = 4 }, Act("scan-size-set", "Setzen", () =>
        {
            if (!double.TryParse((cm.Text ?? "").Replace(',', '.'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v)) { _scanStatus = "Bitte eine Zahl in cm."; Show("scan3d"); return; }
            var ax = axis.SelectedIndex switch { 1 => 'x', 2 => 'y', 3 => 'z', _ => 'm' }; ScanEdit($"Größe {Part3D.Num(v)} cm", m => ScanPro.ScaleTo(m, ax, v));
        }) } });
        var refObj = new ComboBox { Name = "scan-ref", ItemsSource = ScanPro.References.Select(r => $"{r.Name}: {Part3D.Num(r.Cm)} cm").ToList(), SelectedIndex = 0, Width = 250 };
        var measured = new TextBox { Name = "scan-ref-measured", PlaceholderText = "im Modell gemessen (cm)", Width = 150 };
        sp.Children.Add(P("Oder: einen bekannten Gegenstand mitfotografieren und seine Länge im Modell eingeben – Trendo rechnet den Rest.", Muted, 11));
        sp.Children.Add(new WrapPanel { Children = { refObj, new Border { Width = 4 }, measured, new Border { Width = 4 }, Act("scan-ref-set", "Über Gegenstand", () =>
        {
            if (!double.TryParse((measured.Text ?? "").Replace(',', '.'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var mm)) { _scanStatus = "Bitte die im Modell gemessene Länge in cm."; Show("scan3d"); return; }
            var r = ScanPro.References[Math.Max(0, refObj.SelectedIndex)]; ScanEdit($"Größe über {r.Name}", m => ScanPro.ScaleByReference(m, mm, r.Cm));
        }) } });
        return sp;
    }

    /// <summary>Texturqualität (Meshroom) und Fotoauswahl im Rechnen-Bereich.</summary>
    private Control ScanQualityRow()
    {
        var tex = new ComboBox { Name = "scan-texture", ItemsSource = ScanPro.TextureSides.Select(t => $"{t / 1024}K ({t} px)").ToList(), SelectedIndex = Array.IndexOf(ScanPro.TextureSides, _scanTex), Width = 140 };
        tex.SelectionChanged += (_, _) => { if (tex.SelectedIndex >= 0) _scanTex = ScanPro.TextureSides[tex.SelectedIndex]; };
        ToolTip.SetTip(tex, "Größe der Farbtextur bei Meshroom – 8K ist am schärfsten, braucht aber viel Speicher. COLMAP färbt die Punkte direkt (ohne Textur).");
        return new WrapPanel { Children = { new TextBlock { Text = "Texturqualität", Foreground = Muted, FontSize = 11.5, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) }, tex } };
    }

    private Control? ScanRankingList()
    {
        var sp = new StackPanel { Spacing = 2 };
        sp.Children.Add(new WrapPanel { Children = { Act("scan-rank", "Gute Fotos auswählen", ScanRankPhotos), new Border { Width = 4 }, Act("scan-use-good", "Nur gute verwenden", ScanUseGoodPhotos, _scanRank is { } rr && rr.Any(r => !r.Keep)) } });
        if (_scanRank is { } rank)
            foreach (var (f, keep, why) in rank.Where(r => !r.Keep).Take(20))
                sp.Children.Add(P($"✗ {Path.GetFileName(f)}: {why}", Brush.Parse("#F97316"), 11));
        return sp;
    }
}
