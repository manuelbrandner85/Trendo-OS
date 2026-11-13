using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Trendo.Core;
using Trendo.Core.Programs;

namespace Trendo.App;

/// <summary>SPEC-09 / T-0264: 3D-Scan als echtes Programm „Punktwolke“ (Vorbild CloudCompare/MeshLab, nur Funktionsumfang):
/// 3D-Ansicht zum Drehen/Zoomen/Verschieben, Messen per Klick, nicht-destruktive Bearbeitungsschritte (Störpunkte, Ausdünnen, Zentrieren,
/// Drehen, Einheit, Zuschneiden, Boden entfernen, Zusammenführen), Analyse (Boden, Volumen, Dichte, Vergleich), Export PLY/XYZ/Bericht.
/// Der Reiter „Aus Fotos“ (COLMAP/Meshroom) bleibt daneben. Ersetzt die Formularseite.</summary>
public partial class MainWindow
{
    const string PK = "punktwolke";
    private readonly Dictionary<string, PointCloud> _pcCache = [];
    private readonly List<P3> _pcMarks = [];
    private bool _pcMeasure;
    private string _pcAnalysis = "";

    private void InitSpec09()
    {
        RegisterProgram(FachTabs, new ProgramSpec(PK, "SPEC-09", "Punktwolke")
        {
            GroupLevels = new() { ["Umformen"] = 1 }, CmdLevels = new() { ["pc-thin"] = 1, ["pc-scale"] = 1, ["pc-color"] = 1 },
            Sentence = "Scan öffnen, von allen Seiten ansehen, aufräumen, messen und als PLY oder XYZ weitergeben.",
            Folder = "Punktwolken",
            FillHeight = true,
            Templates =
            [
                new("datei", "Scan-Datei öffnen", "PLY, XYZ, PTS, CSV oder OBJ von Handy-LiDAR, Laserscanner oder Fotogrammetrie", () => ScanDocJson(new ScanDoc())),
                new("beispiel", "Beispielraum", "Boden, Wand, Kiste und Kugel mit Störpunkten – zum Ausprobieren", () => ScanDocJson(new ScanDoc { Source = ScanDoc.DemoSource })),
            ],
            Ribbon =
            [
                ("Aufräumen", [
                    new ProgramCmd("pc-outliers", "Störpunkte entfernen", d => ScanAddOp(d, s => new("outliers", [s.Radius, s.MinNeighbors])), true),
                    new ProgramCmd("pc-thin", "Ausdünnen", d => ScanAddOp(d, s => new("thin", [s.Voxel]))),
                    new ProgramCmd("pc-nofloor", "Boden entfernen", d => ScanAddOp(d, s => new("nofloor", [s.FloorTol]))),
                ]),
                ("Umformen", [
                    new ProgramCmd("pc-center", "Zentrieren", d => ScanAddOp(d, _ => new("center", []))),
                    new ProgramCmd("pc-rotate", "Drehen", d => ScanAddOp(d, s => new("rotate", [s.Angle]))),
                    new ProgramCmd("pc-scale", "Einheit umrechnen", d => ScanAddOp(d, s => new("scale", [ScanPlus.UnitFactor(s.UnitFrom, s.UnitTo)]))),
                    new ProgramCmd("pc-crop", "Zuschneiden", d => ScanAddOp(d, s => new("crop", [.. s.Box]))),
                ]),
                ("Ansicht", [
                    new ProgramCmd("pc-v-iso", "Schräg", d => ScanView(d, -35, 28)),
                    new ProgramCmd("pc-v-top", "Oben", d => ScanView(d, 0, 89)),
                    new ProgramCmd("pc-v-front", "Vorne", d => ScanView(d, 0, 0)),
                    new ProgramCmd("pc-v-side", "Seite", d => ScanView(d, -90, 0)),
                    new ProgramCmd("pc-color", "Farbe: Scan/Höhe", d => ScanEditDoc(d, "Farbe", s => s.ColorBy = s.ColorBy == "auto" ? "hoehe" : "auto")),
                    new ProgramCmd("pc-measure", "Messen", _ => { _pcMeasure = !_pcMeasure; _pcMarks.Clear(); Show(PK); }),
                ]),
            ],
            Exports =
            [
                new("ply", "PLY", ".ply", (d, f) => { ScanCloud(d).SavePly(f); return f; }),
                new("xyz", "XYZ-Text", ".xyz", (d, f) => { File.WriteAllText(f, ScanPlus.ToXyz(ScanCloud(d))); return f; }),
                new("report", "Messbericht", ".md", (d, f) => { File.WriteAllText(f, ScanReport(d)); return f; }),
            ],
            Workspace = ScanWorkspace,
            Properties = ScanProperties,
            Status = d => { var s = ScanDoc.From(d.Data); try { var i = ScanCloud(d).Info(); return $"{Fach.F(i.Points, 0)} Punkte · {Fach.F(i.SizeX, 2)} × {Fach.F(i.SizeY, 2)} × {Fach.F(i.SizeZ, 2)} · {s.Ops.Count} Schritte{(_pcMeasure ? " · Messen: Punkte anklicken" : "")}"; } catch (Exception ex) when (ex is IOException or ArgumentException or InvalidOperationException or InvalidDataException or FormatException) { return s.Source.Length == 0 ? "Noch keine Datei" : "" + ex.Message; } },
            Help = "Links ziehen dreht, rechts ziehen (oder Umschalt) verschiebt, Mausrad zoomt. „Messen“ einschalten und zwei Punkte anklicken. Jeder Bearbeitungsschritt steht rechts in der Liste und lässt sich wieder entfernen – die Originaldatei bleibt unverändert.",
        }, "3d-scan", "scan", "punktwolke", "ply", "xyz", "lidar", "laserscan", "pointcloud", "cloudcompare", "meshlab", "volumen");
        RegisterTab(FachTabs, "scan3d", "Aus Fotos", "SPEC-09", () => FachPage("3D-Modell aus Fotos", new StackPanel { Spacing = 8, Children = { P("Mach aus Fotos oder einem Video ein 3D-Modell. Fertige Punktwolken öffnest du im Reiter „Punktwolke“.", Text), PhotoScanCard() } }), "fotogrammetrie", "colmap", "meshroom", "fotos zu 3d");
    }

    static JsonObject ScanDocJson(ScanDoc s) { var o = new JsonObject(); s.WriteTo(o); return o; }

    void ScanEditDoc(ProgramDocument d, string label, Action<ScanDoc> change) => d.Edit(label, x => { var s = ScanDoc.From(x); change(s); s.WriteTo(x); });

    void ScanAddOp(ProgramDocument d, Func<ScanDoc, ScanOp> op)
    {
        var s = ScanDoc.From(d.Data); if (s.Source.Length == 0) throw new ArgumentException("Erst eine Scan-Datei laden.");
        var o = op(s); s.Ops.Add(o); ScanCloudOf(s); // prüft sofort, ob danach noch Punkte übrig sind
        ScanEditDoc(d, o.Describe(), x => x.Ops.Add(o)); _pcMarks.Clear();
    }

    void ScanView(ProgramDocument d, double yaw, double pitch) => ScanEditDoc(d, "Ansicht", s => { s.Yaw = yaw; s.Pitch = pitch; s.Zoom = 1; });

    PointCloud ScanCloudOf(ScanDoc s)
    {
        var key = s.Fingerprint();
        if (_pcCache.TryGetValue(key, out var pc)) return pc;
        pc = s.Build(); if (_pcCache.Count > 4) _pcCache.Clear(); _pcCache[key] = pc; return pc;
    }

    public PointCloud ScanCloud(ProgramDocument d) => ScanCloudOf(ScanDoc.From(d.Data));

    /// <summary>Messpunkt setzen (Klick in der Ansicht oder Test).</summary>
    public void ScanPick(P3 p) { if (_pcMarks.Count >= 2) _pcMarks.Clear(); _pcMarks.Add(p); Show(PK); }
    public IReadOnlyList<P3> ScanMarks => _pcMarks;

    private Control ScanWorkspace(ProgramDocument d)
    {
        var s = ScanDoc.From(d.Data);
        if (s.Source.Length == 0)
            return new Border { Name = "pc-empty", Background = Brush.Parse("#212121"), CornerRadius = new CornerRadius(10), Child = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Spacing = 8, Children = {
                new TextBlock { Text = "Scan-Datei rechts wählen oder hierher ziehen", Foreground = Text, FontSize = 18, HorizontalAlignment = HorizontalAlignment.Center },
                new TextBlock { Text = "PLY, XYZ, PTS, CSV oder OBJ – z. B. vom iPhone-LiDAR, Laserscanner oder aus Fotos", Foreground = Muted, FontSize = 12, HorizontalAlignment = HorizontalAlignment.Center } } } };
        PointCloud pc;
        try { pc = ScanCloudOf(s); }
        catch (Exception ex) when (ex is IOException or ArgumentException or InvalidOperationException or InvalidDataException or FormatException)
        { return new Border { Name = "pc-error", Background = Brush.Parse("#212121"), CornerRadius = new CornerRadius(10), Child = new TextBlock { Text = "" + ex.Message, Foreground = Brush.Parse("#F87171"), FontSize = 15, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(24), VerticalAlignment = VerticalAlignment.Center } }; }
        var view = new PointCloudView { Name = "pc-view", Cloud = pc, Yaw = s.Yaw, Pitch = s.Pitch, Zoom = s.Zoom, ByHeight = s.ColorBy != "auto", MeasureMode = _pcMeasure, Cursor = new Avalonia.Input.Cursor(_pcMeasure ? Avalonia.Input.StandardCursorType.Cross : Avalonia.Input.StandardCursorType.SizeAll) };
        view.Marks.AddRange(_pcMarks);
        view.ViewChanged = (yaw, pitch, zoom) => { var cur = ScanDoc.From(d.Data); cur.Yaw = yaw; cur.Pitch = pitch; cur.Zoom = zoom; cur.WriteTo(d.Data); }; // Ansicht ohne Rückgängig-Eintrag merken
        view.Picked = ScanPick;
        var overlay = new StackPanel { Margin = new Thickness(10), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Spacing = 4, IsHitTestVisible = false };
        if (_pcMeasure) overlay.Children.Add(new Border { Background = Brush.Parse("#CC0A1322"), CornerRadius = new CornerRadius(8), Padding = new Thickness(10, 6), Child = new TextBlock { Name = "pc-measure-out", Foreground = Brush.Parse("#F5A524"), FontSize = 14, FontWeight = FontWeight.SemiBold,
            Text = _pcMarks.Count switch { 0 => "Messen: ersten Punkt anklicken", 1 => $"Punkt 1: {P3s(_pcMarks[0])} – zweiten Punkt anklicken", _ => $"Abstand {Fach.F(Dist(_pcMarks[0], _pcMarks[1]), 3)} · Höhenunterschied {Fach.F(Math.Abs(_pcMarks[1].Z - _pcMarks[0].Z), 3)}" } } });
        return new Panel { Children = { new Border { CornerRadius = new CornerRadius(10), ClipToBounds = true, Child = view }, overlay } };
    }

    static double Dist(P3 a, P3 b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y) + (a.Z - b.Z) * (a.Z - b.Z));
    static string P3s(P3 p) => $"{Fach.F(p.X, 3)} / {Fach.F(p.Y, 3)} / {Fach.F(p.Z, 3)}";

    private Control ScanProperties(ProgramDocument d)
    {
        var s = ScanDoc.From(d.Data); var sp = new StackPanel { Spacing = 4 };
        Button Act2(string name, string label, Action go) { var b = new Button { Name = name, Content = label, Padding = new Thickness(10, 4), Margin = new Thickness(0, 4, 4, 0) }; b.Click += (_, _) => go(); return b; }
        // Quelle
        sp.Children.Add(H("Scan-Datei", 12));
        var file = Field("pc-file-path", "Scan-Datei (.ply, .xyz, .pts, .csv, .obj)", 140, s.Source == ScanDoc.DemoSource ? "" : s.Source); sp.Children.Add(file);
        sp.Children.Add(Act2("pc-load", "Laden", () => ProgramRun(PK, () =>
        {
            var p = Need(file, "die Scan-Datei"); if (!File.Exists(p)) throw new FileNotFoundException("Diese Datei gibt es nicht.");
            var test = new ScanDoc { Source = p }; ScanCloudOf(test); // erst prüfen, dann ins Dokument
            ScanEditDoc(d, "Datei laden", x => { x.Source = p; x.Ops.Clear(); x.Yaw = -35; x.Pitch = 28; x.Zoom = 1; }); _pcMarks.Clear(); _pcAnalysis = "";
        })));
        PointCloud? pc = null; try { if (s.Source.Length > 0) pc = ScanCloudOf(s); } catch (Exception ex) when (ex is IOException or ArgumentException or InvalidOperationException or InvalidDataException or FormatException) { }
        if (pc is not null)
        {
            var i = pc.Info();
            sp.Children.Add(new SelectableTextBlock { Name = "pc-info", Foreground = Text, FontSize = 12, TextWrapping = TextWrapping.Wrap,
                Text = $"{Fach.F(i.Points, 0)} Punkte{(i.HasColor ? " mit Farbe" : "")}\nGröße {Fach.F(i.SizeX, 3)} × {Fach.F(i.SizeY, 3)} × {Fach.F(i.SizeZ, 3)}\nMitte {P3s(i.Center)}" });
        }
        // Bearbeitungsschritte (nicht-destruktiv)
        sp.Children.Add(H($"Schritte ({s.Ops.Count})", 12));
        if (s.Ops.Count == 0) sp.Children.Add(P("Noch keine – oben im Menüband wählen.", null, 11));
        for (var k = 0; k < s.Ops.Count; k++)
        {
            var idx = k; var del = new Button { Name = $"pc-op-del-{k}", Content = "✕", Padding = new Thickness(6, 1) }; ToolTip.SetTip(del, "Schritt entfernen");
            del.Click += (_, _) => ProgramRun(PK, () => { ScanEditDoc(d, "Schritt entfernen", x => x.Ops.RemoveAt(idx)); _pcMarks.Clear(); });
            sp.Children.Add(new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Children = { new TextBlock { Name = $"pc-op-{k}", Text = $"{k + 1}. {s.Ops[k].Describe()}", Foreground = Text, FontSize = 12, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center }, Pos(del, 1) } });
        }
        // Werkzeug-Einstellungen
        sp.Children.Add(H("Einstellungen", 12));
        void Num(string name, string label, double val, Action<ScanDoc, double> set)
        {
            var t = new TextBox { Name = name, Text = val.ToString("0.####", De), Width = 90 };
            void Commit() { if (double.TryParse(t.Text, System.Globalization.NumberStyles.Float, De, out var v) && v != val) ScanEditDoc(d, label, x => set(x, v)); }
            t.LostFocus += (_, _) => Commit(); OnEnter(t, () => { Commit(); Show(PK); });
            sp.Children.Add(Row(P(label, Text, 12), t));
        }
        Num("pc-s-radius", "Störpunkt-Umkreis", s.Radius, (x, v) => x.Radius = v); Num("pc-s-minn", "min. Nachbarn", s.MinNeighbors, (x, v) => x.MinNeighbors = v);
        Num("pc-s-voxel", "Würfelgröße (Ausdünnen)", s.Voxel, (x, v) => x.Voxel = v); Num("pc-s-floor", "Boden-Toleranz", s.FloorTol, (x, v) => x.FloorTol = v); Num("pc-s-angle", "Drehwinkel °", s.Angle, (x, v) => x.Angle = v);
        var units = new[] { "mm", "cm", "m", "in", "ft" };
        ComboBox Unit(string name, string cur, Action<ScanDoc, string> set) { var c = new ComboBox { Name = name, ItemsSource = units, SelectedItem = cur, Width = 70 }; c.SelectionChanged += (_, _) => { if (c.SelectedItem is string u && u != cur) ScanEditDoc(d, "Einheit", x => set(x, u)); }; return c; }
        sp.Children.Add(Row(P("Einheit von → nach", Text, 12), new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Children = { Unit("pc-s-from", s.UnitFrom, (x, u) => x.UnitFrom = u), Unit("pc-s-to", s.UnitTo, (x, u) => x.UnitTo = u) } }));
        var box = new TextBox { Name = "pc-s-box", Text = string.Join(" ", s.Box.Select(b => Fach.F(b, 3))) };
        void BoxCommit() { var b = (box.Text ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(x => double.TryParse(x, System.Globalization.NumberStyles.Float, De, out var v) ? v : double.NaN).ToArray(); if (b.Length == 6 && b.All(double.IsFinite) && !b.SequenceEqual(s.Box)) ScanEditDoc(d, "Box", x => x.Box = b); }
        box.LostFocus += (_, _) => BoxCommit(); OnEnter(box, () => { BoxCommit(); Show(PK); });
        sp.Children.Add(P("Zuschneide-Box „x1 y1 z1 x2 y2 z2“", null, 11)); sp.Children.Add(box);
        if (pc is not null) sp.Children.Add(Act2("pc-box-fit", "Box = ganzer Scan", () => { var i = pc.Info(); ScanEditDoc(d, "Box", x => x.Box = [i.Min.X, i.Min.Y, i.Min.Z, i.Max.X, i.Max.Y, i.Max.Z]); Show(PK); }));
        // Analyse
        sp.Children.Add(H("Analyse", 12));
        var other = Field("pc-cmp-path", "Zweiter Scan (.ply, .xyz) zum Vergleichen oder Zusammenführen", 140); sp.Children.Add(other);
        sp.Children.Add(new WrapPanel { Children = {
            Act2("pc-a-floor", "Boden finden", () => ProgramRun(PK, () => { var c = ScanCloud(d); var pl = ScanPlus.FindFloor(c, s.FloorTol); _pcAnalysis = $"Boden: {Fach.F(pl.Inliers, 0)} von {Fach.F(c.Points.Count, 0)} Punkten, Neigung {Fach.F(pl.TiltDeg, 1)}°"; })),
            Act2("pc-a-vol", "Volumen", () => ProgramRun(PK, () => { var c = ScanCloud(d); var i = c.Info(); var cell = Math.Max(s.Voxel, Math.Max(i.SizeX, i.SizeY) / 200); _pcAnalysis = $"Volumen über dem tiefsten Punkt ca. {Fach.F(ScanPlus.Volume(c, cell, i.Min.Z), 4)} (Einheit³, Raster {Fach.F(cell, 3)})"; })),
            Act2("pc-a-dens", "Punktdichte", () => ProgramRun(PK, () => _pcAnalysis = $"Mittlerer Punktabstand {Fach.F(ScanPlus.Density(ScanCloud(d)), 4)}")),
            Act2("pc-a-cmp", "Vergleichen", () => ProgramRun(PK, () => { var p = Need(other, "den zweiten Scan"); var r = ScanPlus.Compare(ScanCloud(d), PointCloud.Load(p)); _pcAnalysis = $"Abweichung zu {Path.GetFileName(p)}: Mittel {Fach.F(r.Mean, 4)} · größte {Fach.F(r.Max, 4)} · RMS {Fach.F(r.Rms, 4)}"; })),
            Act2("pc-a-merge", "Zusammenführen", () => ProgramRun(PK, () => { var p = Need(other, "den zweiten Scan"); if (!File.Exists(p)) throw new FileNotFoundException("Diese Datei gibt es nicht."); ScanAddOp(d, _ => new("merge", [], p)); })),
        } });
        if (_pcAnalysis.Length > 0) sp.Children.Add(new SelectableTextBlock { Name = "pc-analysis", Text = _pcAnalysis, Foreground = Brush.Parse("#7FD3FF"), FontSize = 12, TextWrapping = TextWrapping.Wrap });
        return sp;
    }

    string ScanReport(ProgramDocument d)
    {
        var s = ScanDoc.From(d.Data); var pc = ScanCloud(d); var i = pc.Info();
        var sb = new System.Text.StringBuilder($"# Messbericht Punktwolke\n\nQuelle: {(s.Source == ScanDoc.DemoSource ? "Beispielraum" : Path.GetFileName(s.Source))}  \nErstellt am {DateTime.Now:dd.MM.yyyy HH:mm}\n\n| Kennzahl | Wert |\n|---|---|\n");
        sb.Append($"| Punkte | {Fach.F(i.Points, 0)} |\n| Größe X × Y × Z | {Fach.F(i.SizeX, 3)} × {Fach.F(i.SizeY, 3)} × {Fach.F(i.SizeZ, 3)} |\n| Mitte | {P3s(i.Center)} |\n| Farbe | {(i.HasColor ? "ja" : "nein")} |\n");
        if (_pcMarks.Count == 2) sb.Append($"| Gemessener Abstand | {Fach.F(Dist(_pcMarks[0], _pcMarks[1]), 3)} |\n");
        sb.Append("\n## Bearbeitungsschritte\n\n");
        if (s.Ops.Count == 0) sb.Append("keine – Originaldaten\n"); else for (var k = 0; k < s.Ops.Count; k++) sb.Append($"{k + 1}. {s.Ops[k].Describe()}\n");
        return sb.ToString();
    }
}
