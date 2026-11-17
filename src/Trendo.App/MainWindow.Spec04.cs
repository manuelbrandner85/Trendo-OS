using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Trendo.Core;

namespace Trendo.App;

/// <summary>„3D-Modelle ansehen“ (Kennung SPEC-04, früher „VR &amp; AR gestalten“) / T-0269: Programm „Modell-Prüfer“ (Vorbild glTF-Viewer, nur Funktionsumfang):
/// Modell (glTF/GLB, OBJ, STL) in 3D ansehen, Maße und Maßstab, Dreiecke, Teile, Texturen und Grafikspeicher, Fehler im Modell, Prüfbericht,
/// Rechner für Detailstufen und Texturschärfe. Erreichbar aus 3D-Druck („Freie 3D-Modelle suchen“), Architektur („3D-Bibliothek öffnen“),
/// über „Ansehen“ in der 3D-Bibliothek und über die Suche.
/// T-0722 (Nutzerentscheidung 16.11.2026): VR-Teile entfernt – Zielbrillen mit Richtwerten, Brillen-Ansicht, „Für die Brille reparieren“,
/// Komfort-Rechner und Bildzeit.</summary>
public partial class MainWindow
{
    public string XrFile = "";
    private bool _xrWire;
    private string _xrMsg = "";
    private (string Key, Mesh? Mesh, XrModelInfo? Info, XrDetail? Detail, string? Error) _xrCache;

    /// <summary>Name der Seite (Reiter, Seitentitel, Suche) – zugleich Name der Seitengruppe in StudioCatalog.</summary>
    public const string XrTitle = "3D-Modelle ansehen";

    private void InitSpec04() => RegisterTab(FachTabs, "xr", XrTitle, "SPEC-04", XrView, "3d-modelle ansehen", "3d modell ansehen", "modell ansehen", "3d ansehen", "modell-prüfer", "modell prüfen",
        "gltf", "glb", "stl", "maße", "dreiecke", "3d-betrachter");

    void XrRun(Action a) { try { a(); } catch (Exception ex) when (ex is IOException or ArgumentException or InvalidOperationException or FormatException or UnauthorizedAccessException or InvalidDataException) { _xrMsg = "⚠ " + ex.Message; } Show("xr"); }

    (Mesh? Mesh, XrModelInfo? Info, XrDetail? Detail, string? Error) XrLoad()
    {
        if (XrFile.Length == 0) return (null, null, null, null);
        var key = XrFile + "|" + (File.Exists(XrFile) ? File.GetLastWriteTimeUtc(XrFile).Ticks : 0);
        if (_xrCache.Key == key) return (_xrCache.Mesh, _xrCache.Info, _xrCache.Detail, _xrCache.Error);
        Mesh? mesh = null; XrModelInfo? info = null; XrDetail? det = null; string? err = null;
        try
        {
            if (!File.Exists(XrFile)) throw new IOException("Diese Datei gibt es nicht.");
            mesh = Mesh3D.ReadMeshFile(XrFile);
            if (Path.GetExtension(XrFile).ToLowerInvariant() is ".glb" or ".gltf") { info = XrInspector.Inspect(XrFile); det = XrPlus.Detail(XrFile); }
            else info = new XrModelInfo(Path.GetFileName(XrFile), new FileInfo(XrFile).Length, 1, 1, mesh.Triangles, mesh.V.Count, 0, 0, 1, 0, "");
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or InvalidDataException or FormatException or System.Text.Json.JsonException or ArgumentException) { err = ex.Message; }
        _xrCache = (key, mesh, info, det, err);
        return (mesh, info, det, err);
    }

    private Control XrView()
    {
        var (mesh, info, det, err) = XrLoad();
        Button Btn(string name, string label, Action go, bool accent = false) { var b = new Button { Name = name, Content = label, Padding = new Thickness(10, 4), Margin = new Thickness(0, 0, 4, 4) }; if (accent) { b.Background = Accent; b.Foreground = Brushes.White; } b.Click += (_, _) => go(); return b; }
        // Kopf: Datei
        var bar = new WrapPanel { Name = "xr-bar" };
        var file = Field("xr-file-path", "3D-Modell (.glb, .gltf, .obj, .stl)", 320, XrFile); bar.Children.Add(file);
        bar.Children.Add(Btn("xr-open", "Öffnen", () => XrRun(() => { var p = Need(file, "das Modell"); if (!File.Exists(p)) throw new FileNotFoundException("Diese Datei gibt es nicht."); XrFile = p; _xrMsg = ""; }), true));
        bar.Children.Add(Btn("xr-sample", "Beispielmodell", () => XrRun(() => { XrFile = XrSample.Write(Path.Combine(WorkFolder("Modell-Prüfer"), "beispiel-leseecke.glb")); _xrMsg = "Beispiel geladen: Leseecke mit Sessel, Pflanze und Teppich in echten Maßen."; })));
        if (_xrMsg.Length > 0) bar.Children.Add(new TextBlock { Name = "xr-msg", Text = _xrMsg, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 4), FontSize = 12, Foreground = Brush.Parse(_xrMsg.StartsWith('⚠') ? "#F87171" : "#4ADE80") });

        // Mitte: 3D-Ansicht
        Control center;
        if (mesh is null)
            center = new Border { Name = "xr-empty", Background = Brush.Parse("#212121"), CornerRadius = new CornerRadius(10), Child = new TextBlock { Text = err is null ? "Modell oben wählen oder hierher ziehen – oder das Beispielmodell laden." : "⚠ " + err, Foreground = err is null ? Muted : Brush.Parse("#F87171"), FontSize = 15, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(24), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } };
        else
        {
            var view = new MeshView { Name = "xr-view", Model = mesh, Wire = _xrWire, EyeHeightCm = _xrEye ? XrPro.EyeCm : null, EyeDistanceCm = XrPro.ViewDistanceCm, Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.SizeAll) };
            var tools = new WrapPanel { Margin = new Thickness(0, 6, 0, 0) };
            foreach (var (id, label) in new[] { ("iso", "Schräg"), ("vorne", "Vorne"), ("seite", "Seite"), ("oben", "Oben") }) { var v = id; tools.Children.Add(Btn("xr-v-" + id, label, () => view.SetView(v))); }
            tools.Children.Add(Btn("xr-wire", _xrWire ? "Drahtgitter aus" : "Drahtgitter an", () => { _xrWire = !_xrWire; Show("xr"); }));
            tools.Children.Add(Btn("xr-eye", _xrEye ? "Augenhöhe aus" : "Aus Augenhöhe", () => { _xrEye = !_xrEye; Show("xr"); })); // stehend 1,60 m, 2 m Abstand – wie im Raum
            var size = det is null ? "" : XrPlus.ScaleVerdict(det);
            tools.Children.Add(new TextBlock { Name = "xr-size", Text = size, Foreground = Text, FontSize = 12, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 4), TextWrapping = TextWrapping.Wrap, MaxWidth = 520 });
            var dp = new DockPanel(); DockPanel.SetDock(tools, Avalonia.Controls.Dock.Bottom); dp.Children.Add(tools); dp.Children.Add(new Border { CornerRadius = new CornerRadius(10), ClipToBounds = true, Child = view });
            center = dp;
        }
        var mid = new Border { CornerRadius = new CornerRadius(14), Background = TileBg, BorderBrush = EdgeLit, BorderThickness = new Thickness(1), Padding = new Thickness(10), Child = center };
        var right = new Border { Name = "xr-side", Width = 360, CornerRadius = new CornerRadius(14), Background = TileBg, BorderBrush = EdgeLit, BorderThickness = new Thickness(1), Padding = new Thickness(12), Child = new ScrollViewer { Content = XrSide(mesh, info, det) } };
        var body = new Grid { ColumnDefinitions = new ColumnDefinitions("*,12,Auto"), Children = { mid, Pos(right, 2) } };
        var root = new DockPanel { Name = "xr-root" }; DockPanel.SetDock(bar, Avalonia.Controls.Dock.Top); root.Children.Add(bar); root.Children.Add(body);
        return FitPage(XrTitle, new Panel(), root);
    }

    /// <summary>Fehler im Modell: bei glTF/GLB aus den Modelldaten (Material, leere Datei), bei OBJ/STL nur „keine Dreiecke“ – Material gibt es dort nicht.</summary>
    static IReadOnlyList<string> XrProblems(Mesh mesh, XrModelInfo info, XrDetail? det) =>
        det is not null ? XrInspector.Problems(info) : mesh.Triangles == 0 ? ["Keine Dreiecke – die Datei enthält keine sichtbare Form."] : [];

    Control XrSide(Mesh? mesh, XrModelInfo? info, XrDetail? det)
    {
        var sp = new StackPanel { Spacing = 6 };
        if (info is not null && mesh is not null)
        {
            sp.Children.Add(H($"{info.File}", 13));
            void Row(string name, string label, string text) =>
                sp.Children.Add(new Grid { Name = name, Margin = new Thickness(0, 0, 14, 0), ColumnDefinitions = new ColumnDefinitions("*,Auto"), Children = { // rechts Platz für die Bildlaufleiste
                    new TextBlock { Text = label, Foreground = Text, FontSize = 12 }, Pos(new TextBlock { Name = name + "-val", Text = text, Foreground = Text, FontSize = 12, FontWeight = FontWeight.SemiBold }, 1) } });
            if (mesh.V.Count > 0)
            {
                double Span(Func<Vec3, double> f) => mesh.V.Max(f) - mesh.V.Min(f);
                Row("xr-m-dims", "Maße (B × T × H)", $"{Fach.F(Span(v => v.X), 1)} × {Fach.F(Span(v => v.Y), 1)} × {Fach.F(Span(v => v.Z), 1)} cm");
            }
            Row("xr-m-tris", "Dreiecke", Fach.F(info.Triangles, 0));
            Row("xr-m-draw", "Teile (Zeichenaufrufe)", info.Primitives.ToString());
            if (det is not null) { Row("xr-m-mat", "Materialien", info.Materials.ToString()); Row("xr-m-tex", "Texturen", info.Textures.ToString()); }
            Row("xr-m-size", "Dateigröße", $"{Fach.F(info.Bytes / 1048576.0, 2)} MB");
            if (det is { Textures.Count: > 0 }) Row("xr-m-texsize", "Größte Textur", $"{det.Textures.Max(t => Math.Max(t.Width, t.Height))} px");
            // Fehler im Modell – für 3D-Druck und Architektur, unabhängig von einem Zielgerät
            var problems = XrProblems(mesh, info, det);
            sp.Children.Add(H("Fehler im Modell", 12));
            var list = new StackPanel { Name = "xr-problems", Spacing = 2 };
            if (problems.Count == 0) list.Children.Add(new TextBlock { Name = "xr-problem-0", Text = "✓ Keine Fehler gefunden.", Foreground = Brush.Parse("#4ADE80"), FontSize = 11.5, TextWrapping = TextWrapping.Wrap });
            for (var k = 0; k < problems.Count; k++) list.Children.Add(new TextBlock { Name = "xr-problem-" + k, Text = "⚠ " + problems[k], Foreground = Brush.Parse("#FACC15"), FontSize = 11.5, TextWrapping = TextWrapping.Wrap });
            sp.Children.Add(list);
            sp.Children.Add(XrGltfCard()); // Alle Studios, Runde 5: Khronos-Prüfung und Packen für Web-Seiten
            sp.Children.Add(new SelectableTextBlock { Name = "xr-stats", Foreground = Muted, FontSize = 11.5, TextWrapping = TextWrapping.Wrap, Text = $"{Fach.F(info.Vertices, 0)} Punkte · {info.Meshes} Netze · {info.Materials} Materialien · {info.Nodes} Knoten · {info.Animations} Animationen" + (info.Generator.Length > 0 ? $" · erstellt mit {info.Generator}" : "") });
            if (det is not null)
            {
                if (det.Textures.Count > 0)
                {
                    sp.Children.Add(H($"Texturen · ca. {Fach.F(det.Textures.Sum(t => t.VramBytes) / 1048576.0, 1)} MB Grafikspeicher", 12));
                    foreach (var t in det.Textures) sp.Children.Add(P($"{t.Name}: {t.Width} × {t.Height} · {Fach.F(t.VramBytes / 1048576.0, 1)} MB", null, 11));
                }
                foreach (var n in XrPlus.ExtensionNotes(det)) sp.Children.Add(P(n, null, 11));
                sp.Children.Add(new Button { Name = "xr-report", Content = "Prüfbericht speichern", Padding = new Thickness(10, 4) }.Also(bt => bt.Click += (_, _) => XrRun(() => { var p = Path.Combine(WorkFolder("Modell-Prüfer"), Path.GetFileNameWithoutExtension(XrFile) + ".pruefbericht.md"); File.WriteAllText(p, XrPlus.Report(det)); _xrMsg = "Gespeichert: " + p; NotifyFileResult(_xrMsg); })));
            }
        }
        // Rechner (sofort)
        var calcs = new StackPanel { Spacing = 6 };
        LiveCalc(calcs, "xr-lod", "Detailstufen planen", [("tris", "Dreiecke (voll)", (info?.Triangles ?? 200000).ToString(), null), ("size", "Objektgröße (m)", det is null ? "2" : Fach.F(Math.Max(det.SizeX, Math.Max(det.SizeY, det.SizeZ)), 2), null)],
            v => string.Join(" · ", XrPlus.LodPlan((long)Fach.Num(v("tris")), Fach.Num(v("size"))).Select(x => $"Stufe {x.Level}: {Fach.F(x.Triangles, 0)} ab {Fach.F(x.FromM, 1)} m")));
        LiveCalc(calcs, "xr-texel", "Textur scharf genug?", [("px", "Textur (px)", det?.Textures.FirstOrDefault()?.Width.ToString() ?? "2048", ["512", "1024", "2048", "4096"]), ("size", "Objekt (m)", "2", null), ("dist", "Abstand (m)", "1", null)],
            v => { var (d, t) = XrPlus.TexelDensity((int)Fach.Num(v("px")), Fach.Num(v("size")), Fach.Num(v("dist"))); return $"{Fach.F(d, 0)} Pixel pro Meter – {t}"; });
        sp.Children.Add(H("Rechner", 12)); sp.Children.Add(calcs);
        return sp;
    }
}

static class ControlAlso { public static T Also<T>(this T c, Action<T> a) { a(c); return c; } }
