using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Trendo.Core;

namespace Trendo.App;

/// <summary>SPEC-04 / T-0269: VR/AR/MR als Programm „Modell-Prüfer“ (Vorbild glTF-Viewer und Leistungsanzeigen der Brillen, nur Funktionsumfang):
/// Modell (glTF/GLB, OBJ, STL) in 3D ansehen, Richtwerte je Gerät als Ampel und Balken, Maße, Texturen und Grafikspeicher, Prüfbericht,
/// Rechner für Bildzeit, Detailstufen, Texturschärfe und Bewegungskomfort, die sofort rechnen. Ersetzt die Formularseite.</summary>
public partial class MainWindow
{
    public string XrFile = "";
    public int XrDevice;
    private bool _xrWire;
    private string _xrMsg = "";
    private (string Key, Mesh? Mesh, XrModelInfo? Info, XrDetail? Detail, string? Error) _xrCache;

    private void InitSpec04() => RegisterTab(FachTabs, "xr", "Modell-Prüfer", "SPEC-04", XrView, "vr", "ar", "mr", "xr", "brille", "quest", "gltf", "glb", "virtuelle realität", "modell prüfen");

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
        // Kopf: Datei und Zielgerät
        var bar = new WrapPanel { Name = "xr-bar" };
        var file = Field("xr-file-path", "3D-Modell (.glb, .gltf, .obj, .stl)", 320, XrFile); bar.Children.Add(file);
        bar.Children.Add(Btn("xr-open", "Öffnen", () => XrRun(() => { var p = Need(file, "das Modell"); if (!File.Exists(p)) throw new FileNotFoundException("Diese Datei gibt es nicht."); XrFile = p; _xrMsg = ""; }), true));
        bar.Children.Add(Btn("xr-sample", "Beispielmodell", () => XrRun(() => { XrFile = XrSample.Write(Path.Combine(WorkFolder("Modell-Prüfer"), "beispiel-leseecke.glb")); _xrMsg = "Beispiel geladen: Leseecke mit Sessel, Pflanze und Teppich in echten Maßen."; })));
        bar.Children.Add(new TextBlock { Text = "Zielgerät", Foreground = Muted, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 6, 4) });
        for (var k = 0; k < XrInspector.Budgets.Count; k++)
        {
            var i = k; var b = Btn($"xr-dev-{k}", XrInspector.Budgets[k].Device.Split(" (")[0], () => { XrDevice = i; Show("xr"); }); if (XrDevice == k) { b.Background = Brush.Parse("#C9A227"); b.Foreground = Brushes.White; }
            bar.Children.Add(b);
        }
        if (_xrMsg.Length > 0) bar.Children.Add(new TextBlock { Name = "xr-msg", Text = _xrMsg, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 4), FontSize = 12, Foreground = Brush.Parse(_xrMsg.StartsWith('⚠') ? "#F87171" : "#4ADE80") });

        // Mitte: 3D-Ansicht
        Control center;
        if (mesh is null)
            center = new Border { Name = "xr-empty", Background = Brush.Parse("#212121"), CornerRadius = new CornerRadius(10), Child = new TextBlock { Text = err is null ? "Modell oben wählen oder hierher ziehen – oder das Beispielmodell laden." : "⚠ " + err, Foreground = err is null ? Muted : Brush.Parse("#F87171"), FontSize = 15, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(24), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } };
        else
        {
            var view = new MeshView { Name = "xr-view", Model = mesh, Wire = _xrWire, EyeHeightCm = _xrEye ? XrPro.EyeCm : null, EyeDistanceCm = XrPro.ViewDistanceCm, Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.SizeAll) };
            Control? headset = null;
            var tools = new WrapPanel { Margin = new Thickness(0, 6, 0, 0) };
            foreach (var (id, label) in new[] { ("iso", "Schräg"), ("vorne", "Vorne"), ("seite", "Seite"), ("oben", "Oben") }) { var v = id; tools.Children.Add(Btn("xr-v-" + id, label, () => view.SetView(v))); }
            tools.Children.Add(Btn("xr-wire", _xrWire ? "Drahtgitter aus" : "Drahtgitter an", () => { _xrWire = !_xrWire; Show("xr"); }));
            tools.Children.Add(Btn("xr-eye", _xrEye ? "👁 Augenhöhe aus" : "👁 Aus Augenhöhe", () => { _xrEye = !_xrEye; Show("xr"); }, _xrEye)); // wie mit der Brille: 1,60 m, 2 m Abstand
            if (XrPro.HeadsetOf(XrDevice) is { } hs) // S3: beide Augen wie in der Brille (Sichtfeld und Augenabstand des Zielgeräts)
            {
                tools.Children.Add(Btn("xr-hmd", _xrHmd ? "🥽 Brillen-Ansicht aus" : "🥽 Brillen-Ansicht", () => { _xrHmd = !_xrHmd; Show("xr"); }, _xrHmd));
                if (_xrHmd) { var hv = XrHeadsetView(mesh, hs, view); view = hv.Left; headset = hv.Panel; }
            }
            var size = det is null ? "" : XrPlus.ScaleVerdict(det);
            tools.Children.Add(new TextBlock { Name = "xr-size", Text = size, Foreground = Text, FontSize = 12, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 4), TextWrapping = TextWrapping.Wrap, MaxWidth = 520 });
            var dp = new DockPanel(); DockPanel.SetDock(tools, Avalonia.Controls.Dock.Bottom); dp.Children.Add(tools); dp.Children.Add(headset ?? new Border { CornerRadius = new CornerRadius(10), ClipToBounds = true, Child = view });
            center = dp;
        }
        var mid = new Border { CornerRadius = new CornerRadius(14), Background = TileBg, BorderBrush = EdgeLit, BorderThickness = new Thickness(1), Padding = new Thickness(10), Child = center };
        var right = new Border { Name = "xr-side", Width = 360, CornerRadius = new CornerRadius(14), Background = TileBg, BorderBrush = EdgeLit, BorderThickness = new Thickness(1), Padding = new Thickness(12), Child = new ScrollViewer { Content = XrSide(info, det) } };
        var body = new Grid { ColumnDefinitions = new ColumnDefinitions("*,12,Auto"), Children = { mid, Pos(right, 2) } };
        var root = new DockPanel { Name = "xr-root" }; DockPanel.SetDock(bar, Avalonia.Controls.Dock.Top); root.Children.Add(bar); root.Children.Add(body);
        return FitPage("Modell-Prüfer", new Panel(), root);
    }

    Control XrSide(XrModelInfo? info, XrDetail? det)
    {
        var sp = new StackPanel { Spacing = 6 };
        if (info is not null)
        {
            var b = XrInspector.Budgets[Math.Clamp(XrDevice, 0, XrInspector.Budgets.Count - 1)];
            sp.Children.Add(H($"{info.File}", 13));
            void Meter(string name, string label, double val, double max, string text)
            {
                var pct = max <= 0 ? 0 : val / max; var col = pct > 1 ? "#F87171" : pct > 0.8 ? "#FACC15" : "#4ADE80";
                sp.Children.Add(new StackPanel { Name = name, Spacing = 2, Children = {
                    new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Children = { new TextBlock { Text = label, Foreground = Text, FontSize = 12 }, Pos(new TextBlock { Text = text, Foreground = Brush.Parse(col), FontSize = 12, FontWeight = FontWeight.SemiBold }, 1) } },
                    new ProgressBar { Minimum = 0, Maximum = 100, Value = Math.Min(100, pct * 100), Height = 6, Foreground = Brush.Parse(col) } } });
            }
            Meter("xr-m-tris", "Dreiecke", info.Triangles, b.MaxTriangles, $"{Fach.F(info.Triangles, 0)} / {Fach.F(b.MaxTriangles, 0)}");
            Meter("xr-m-draw", "Zeichenaufrufe", info.Primitives, b.MaxDrawCalls, $"{info.Primitives} / {b.MaxDrawCalls}");
            Meter("xr-m-tex", "Texturen", info.Textures, b.MaxTextures, $"{info.Textures} / {b.MaxTextures}");
            Meter("xr-m-size", "Dateigröße", info.Bytes, b.MaxBytes, $"{Fach.F(info.Bytes / 1048576.0, 2)} / {Fach.F(b.MaxBytes / 1048576.0, 0)} MB");
            if (det is { Textures.Count: > 0 }) { var big = det.Textures.Max(t => Math.Max(t.Width, t.Height)); var lim = XrPro.MaxTextureSide(XrDevice); Meter("xr-m-texsize", "Größte Textur", big, lim, $"{big} / {lim} px"); }
            sp.Children.Add(XrFixCard(info, det));
            sp.Children.Add(XrGltfCard()); // Alle Studios, Runde 5: Khronos-Prüfung und Packen für Web & Brille
            sp.Children.Add(H("Alle Geräte", 12));
            foreach (var bb in XrInspector.Budgets)
            {
                var c = XrInspector.Check(info, bb);
                sp.Children.Add(new TextBlock { Name = "xr-check-" + XrInspector.Budgets.ToList().IndexOf(bb), Text = $"{(c.Ok ? "✓" : "⚠")} {bb.Device}" + (c.Ok ? "" : ": " + string.Join(" ", c.Problems)), Foreground = Brush.Parse(c.Ok ? "#4ADE80" : "#FACC15"), FontSize = 11.5, TextWrapping = TextWrapping.Wrap });
            }
            sp.Children.Add(P("Richtwerte – ob es wirklich flüssig läuft, zeigt nur ein Test auf dem Gerät.", null, 10.5));
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
        var fps = XrInspector.Budgets[Math.Clamp(XrDevice, 0, XrInspector.Budgets.Count - 1)].Fps.ToString();
        LiveCalc(calcs, "xr-frame", "Bildzeit", [("fps", "Bildwiederholrate (Hz)", fps, ["60", "72", "90", "120"]), ("ms", "Gemessen (ms, aus der Leistungsanzeige)", "12", null)],
            v => { var (bud, ok, h) = XrInspector.FrameBudget((int)Fach.Num(v("fps")), Fach.Num(v("ms"))); return ok ? $"✓ Im Budget: {Fach.F(bud)} ms je Bild, {Fach.F(h, 1)} % Reserve" : $"⚠ Zu langsam: Budget nur {Fach.F(bud)} ms – Szene vereinfachen"; });
        LiveCalc(calcs, "xr-lod", "Detailstufen planen", [("tris", "Dreiecke (voll)", (info?.Triangles ?? 200000).ToString(), null), ("size", "Objektgröße (m)", det is null ? "2" : Fach.F(Math.Max(det.SizeX, Math.Max(det.SizeY, det.SizeZ)), 2), null)],
            v => string.Join(" · ", XrPlus.LodPlan((long)Fach.Num(v("tris")), Fach.Num(v("size"))).Select(x => $"Stufe {x.Level}: {Fach.F(x.Triangles, 0)} ab {Fach.F(x.FromM, 1)} m")));
        LiveCalc(calcs, "xr-texel", "Textur scharf genug?", [("px", "Textur (px)", det?.Textures.FirstOrDefault()?.Width.ToString() ?? "2048", ["512", "1024", "2048", "4096"]), ("size", "Objekt (m)", "2", null), ("dist", "Abstand (m)", "1", null)],
            v => { var (d, t) = XrPlus.TexelDensity((int)Fach.Num(v("px")), Fach.Num(v("size")), Fach.Num(v("dist"))); return $"{Fach.F(d, 0)} Pixel pro Meter – {t}"; });
        LiveCalc(calcs, "xr-move", "Wird jemandem übel?", [("move", "Gehtempo (m/s)", "2", null), ("turn", "Drehen (°/s)", "120", null), ("snap", "Schritt-Drehen", "nein", ["nein", "ja"]), ("vig", "Tunnelblick", "nein", ["nein", "ja"]), ("acc", "Beschleunigung (m/s²)", "0", null)],
            v => { var (sc, tips) = XrPlus.Comfort(Fach.Num(v("move")), Fach.Num(v("turn")), v("snap") == "ja", v("vig") == "ja", Fach.Num(v("acc"))); return $"Komfort {sc}/100" + (tips.Count == 0 ? " – angenehm" : " · " + string.Join(" · ", tips)); });
        sp.Children.Add(H("Rechner", 12)); sp.Children.Add(calcs);
        return sp;
    }
}

static class ControlAlso { public static T Also<T>(this T c, Action<T> a) { a(c); return c; } }
