using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Trendo.Core;

namespace Trendo.App;

/// <summary>CORE-09 / T-0270: Kameras & Roboter als Programm „Roboter-Werkstatt“ (Vorbild RoboDK/MoveIt-Planungsansicht, nur Funktionsumfang):
/// Arm in Draufsicht und Seitenansicht (Klick setzt das Ziel), Wegpunkte als Liste, Weg simulieren, Pick & Place, Programm-CSV;
/// Bild prüfen mit eingeblendeten Fundstellen, Maske, Belichtung/Schärfe, Bewegung, Kalibrierung. Kameras, Steuerung (Simulation,
/// Trockenlauf, echt nur nach Freigabe) und Not-Aus bleiben. Ersetzt die Formularseite.</summary>
public partial class MainWindow
{
    public string RobMode = "arm";
    private string _robMsg = "";
    public string RobImage = "", RobImage2 = "", RobColor = "rot";
    private bool _robMask;
    private DispatcherTimer? _robSim; private double _robSimT; private ArmView? _robView;
    public bool RobSimulating => _robSim is { IsEnabled: true };

    private void InitCore09() => RegisterTab(FachTabs, "roboter", "Roboter-Werkstatt", "CORE-09", RobotView, "roboter", "robotik", "roboterarm", "kamera", "bilderkennung", "vision", "servo", "kinematik", "greifer", "pick and place");

    void RobRun(Action a) { try { a(); } catch (Exception ex) when (ex is IOException or ArgumentException or InvalidOperationException or FormatException or UnauthorizedAccessException or InvalidDataException) { _robMsg = "⚠ " + ex.Message; } Show("roboter"); }

    List<(double X, double Y, double Z)> RobPoints() => Bench.Points.Split('|', StringSplitOptions.RemoveEmptyEntries).Select(p => p.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries)).Where(p => p.Length == 3)
        .Select(p => (Fach.Num(p[0], "x"), Fach.Num(p[1], "y"), Fach.Num(p[2], "z"))).ToList();
    void RobSetPoints(IEnumerable<(double X, double Y, double Z)> pts) => Bench.Points = string.Join(" | ", pts.Select(p => $"{p.X:0.#} {p.Y:0.#} {p.Z:0.#}".Replace(',', '.')));

    /// <summary>Weg in der Simulation abfahren (Gelenke werden zwischen den Wegpunkten gleichmäßig gedreht).</summary>
    public void RobSimulate()
    {
        if (RobSimulating) { _robSim!.Stop(); return; }
        var steps = RobotArm.Program(new ArmConfig(), Bench.Points.Replace('|', '\n'));
        _robSimT = 0; _robSim ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(40) };
        _robSim.Tick -= RobSimTick; _robSim.Tick += RobSimTick; _robSim.Start();
        RobSimAt(0, steps);
    }
    void RobSimTick(object? s, EventArgs e)
    {
        if (CurrentView != "roboter" || _robView is null) { _robSim?.Stop(); return; }
        IReadOnlyList<ProgramStep> steps; try { steps = RobotArm.Program(new ArmConfig(), Bench.Points.Replace('|', '\n')); } catch (Exception ex) when (ex is ArgumentException or FormatException) { _robSim?.Stop(); return; }
        var total = Math.Max(0.5, steps.Sum(x => x.Seconds)); _robSimT += 0.04;
        if (_robSimT >= total) { _robSim?.Stop(); _robSimT = total; }
        RobSimAt(_robSimT, steps);
    }
    void RobSimAt(double t, IReadOnlyList<ProgramStep> steps)
    {
        if (_robView is null || steps.Count == 0) return;
        var j = steps[0].J; var acc = 0.0;
        for (var k = 1; k < steps.Count; k++)
        {
            var d = Math.Max(1e-6, steps[k].Seconds);
            if (t <= acc + d) { var f = (t - acc) / d; var a = steps[k - 1].J; var b = steps[k].J; j = new(a.Base + (b.Base - a.Base) * f, a.Shoulder + (b.Shoulder - a.Shoulder) * f, a.Elbow + (b.Elbow - a.Elbow) * f); break; }
            acc += d; j = steps[k].J;
        }
        _robView.J = j; _robView.InvalidateVisual();
    }

    private Control RobotView()
    {
        Button Btn(string name, string label, Action go, bool accent = false) { var b = new Button { Name = name, Content = label, Padding = new Thickness(10, 4), Margin = new Thickness(0, 0, 4, 4) }; if (accent) { b.Background = Accent; b.Foreground = Brushes.White; } b.Click += (_, _) => go(); return b; }
        var bar = new WrapPanel { Name = "rob-bar" };
        foreach (var (id, label) in new[] { ("arm", "Arm planen"), ("bild", "Bild prüfen") })
        {
            var mid = id; var b = Btn("rob-mode-" + id, label, () => { RobMode = mid; _robMsg = ""; Show("roboter"); }); if (RobMode == id) { b.Background = Accent; b.Foreground = Brushes.White; }
            bar.Children.Add(b);
        }
        if (_robMsg.Length > 0) bar.Children.Add(new TextBlock { Name = "rob-msg", Text = _robMsg, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 4), TextWrapping = TextWrapping.Wrap, MaxWidth = 640, FontSize = 12, Foreground = Brush.Parse(_robMsg.StartsWith('⚠') ? "#F87171" : "#4ADE80") });
        var top = new StackPanel { Spacing = 6, Children = { EstopBar(), bar } };
        Control main = RobMode == "bild" ? RobImagePane() : RobArmPane();
        var mid2 = new Border { CornerRadius = new CornerRadius(14), Background = TileBg, BorderBrush = EdgeLit, BorderThickness = new Thickness(1), Padding = new Thickness(12), Child = main };
        var side = new StackPanel { Spacing = 10, Children = { DrivePanel(), CameraPanel(), RobCalcs() } };
        var right = new Border { Name = "rob-side", Width = 420, CornerRadius = new CornerRadius(14), Background = TileBg, BorderBrush = EdgeLit, BorderThickness = new Thickness(1), Padding = new Thickness(12), Child = new ScrollViewer { Content = side } };
        var body = new Grid { ColumnDefinitions = new ColumnDefinitions("*,12,Auto"), Children = { mid2, Pos(right, 2) } };
        var root = new DockPanel { Name = "rob-root" }; DockPanel.SetDock(top, Avalonia.Controls.Dock.Top); root.Children.Add(top); root.Children.Add(body);
        return FitPage("Roboter-Werkstatt", new Panel(), root);
    }

    Control RobArmPane()
    {
        var arm = new ArmConfig();
        (double X, double Y, double Z)? target = null; Joints j = Bench.Drive.Pose ?? new(0, 90, -90);
        try { var t = (Fach.Num(Bench.X, "x"), Fach.Num(Bench.Y, "y"), Fach.Num(Bench.Z, "z")); target = t; j = RobotArm.Inverse(arm, t.Item1, t.Item2, t.Item3); } // geplante Stellung zum Ziel; die echte Stellung steht in der Steuerung
        catch (Exception ex) when (ex is ArgumentException or FormatException) { if (_robMsg.Length == 0) _robMsg = "⚠ " + ex.Message; }
        List<(double X, double Y, double Z)> pts; try { pts = RobPoints(); } catch (FormatException) { pts = []; }
        var view = new ArmView { Name = "rob-arm-view", Arm = arm, J = j, Target = target, Points = pts, MinHeight = 260 };
        view.Picked = (x, y) => RobRun(() => { RobotArm.Inverse(arm, x, y, Fach.Num(Bench.Z, "z")); Bench.X = x.ToString("0"); Bench.Y = y.ToString("0"); _robMsg = $"Ziel {x:0} / {y:0} / {Bench.Z} mm – mit „Fahren“ anfahren oder als Wegpunkt übernehmen."; });
        _robView = view;
        // Wegpunkte
        var list = new StackPanel { Name = "rob-points", Spacing = 2 };
        Button Small(string name, string label, Action go) { var b = new Button { Name = name, Content = label, Padding = new Thickness(6, 1) }; b.Click += (_, _) => RobRun(go); return b; }
        for (var k = 0; k < pts.Count; k++)
        {
            var i = k; var p = pts[k]; string joints;
            try { var jj = RobotArm.Inverse(arm, p.X, p.Y, p.Z); joints = $"{jj.Base:0}° / {jj.Shoulder:0}° / {jj.Elbow:0}°"; } catch (ArgumentException ex) { joints = "⚠ " + ex.Message; }
            list.Children.Add(new Grid { ColumnDefinitions = new ColumnDefinitions("26,150,*,Auto"), Children = {
                new TextBlock { Text = (i + 1) + ".", Foreground = Muted, VerticalAlignment = VerticalAlignment.Center },
                Pos(new TextBlock { Name = $"rob-pt-{i}", Text = $"{p.X:0} / {p.Y:0} / {p.Z:0} mm", Foreground = Text, FontSize = 12, VerticalAlignment = VerticalAlignment.Center }, 1),
                Pos(new TextBlock { Text = joints, Foreground = joints.StartsWith('⚠') ? Brush.Parse("#FACC15") : Muted, FontSize = 11, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis }, 2),
                Pos(new StackPanel { Orientation = Orientation.Horizontal, Children = { Small($"rob-pt-up-{i}", "↑", () => { var l = RobPoints(); if (i > 0) (l[i - 1], l[i]) = (l[i], l[i - 1]); RobSetPoints(l); }), Small($"rob-pt-del-{i}", "✕", () => { var l = RobPoints(); l.RemoveAt(i); RobSetPoints(l); }) } }, 3) } });
        }
        Button Btn(string name, string label, Action go, bool accent = false) { var b = new Button { Name = name, Content = label, Padding = new Thickness(10, 4), Margin = new Thickness(0, 0, 4, 4) }; if (accent) { b.Background = Accent; b.Foreground = Brushes.White; } b.Click += (_, _) => go(); return b; }
        var pick = new TextBox { Name = "rob-pick-from", Text = "150 0 20", Width = 110 }; var place = new TextBox { Name = "rob-pick-to", Text = "0 150 20", Width = 110 };
        var actions = new WrapPanel { Children = {
            Btn("rob-pt-add", "＋ Ziel als Wegpunkt", () => RobRun(() => { var l = RobPoints(); var p = (Fach.Num(Bench.X, "x"), Fach.Num(Bench.Y, "y"), Fach.Num(Bench.Z, "z")); RobotArm.Inverse(arm, p.Item1, p.Item2, p.Item3); l.Add(p); RobSetPoints(l); _robMsg = ""; })),
            Btn("rob-sim", RobSimulating ? "■ Simulation stoppen" : "▶ Weg simulieren", () => RobRun(() => RobSimulate()), true),
            Btn("rob-csv", "Programm (CSV)", () => RobRun(() => { var p = RobotArm.Program(arm, Bench.Points.Replace('|', '\n')); var f = System.IO.Path.Combine(WorkFolder("Roboter"), "programm.csv"); File.WriteAllText(f, RobotArm.Csv(p)); _robMsg = $"Gesamt {Fach.F(p.Sum(s => s.Seconds), 2)} s – gespeichert: {f}"; NotifyFileResult(_robMsg); })),
            new TextBlock { Text = "Pick & Place von", Foreground = Muted, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 4, 4) }, pick,
            new TextBlock { Text = "nach", Foreground = Muted, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 4, 4) }, place,
            Btn("rob-pick", "Greifbahn erzeugen", () => RobRun(() => { (double, double, double) T(string s) { var p = s.Split(' ', StringSplitOptions.RemoveEmptyEntries); return p.Length == 3 ? (Fach.Num(p[0]), Fach.Num(p[1]), Fach.Num(p[2])) : throw new FormatException("Bitte „x y z“."); } var path = ArmPlus.PickPlace(T(pick.Text ?? ""), T(place.Text ?? ""), 40); var text = ArmPlus.ProgramText(path); RobotArm.Program(arm, text); Bench.Points = string.Join(" | ", text.Replace("\r", "").Split('\n').Where(l => l.Trim().Length > 0 && !l.TrimStart().StartsWith('#')).Select(l => string.Join(' ', l.Split([' ', ';', '\t'], StringSplitOptions.RemoveEmptyEntries).Take(3)))); _robMsg = "Greifbahn mit Anfahrhöhe 40 mm erzeugt."; })),
        } };
        var bottom = new StackPanel { Spacing = 6, Children = { H($"Wegpunkte ({pts.Count})", 12), list, actions } };
        var dp = new DockPanel(); var sv = new ScrollViewer { Content = bottom, MaxHeight = 280 }; DockPanel.SetDock(sv, Avalonia.Controls.Dock.Bottom); dp.Children.Add(sv); dp.Children.Add(new Border { CornerRadius = new CornerRadius(10), ClipToBounds = true, Child = view });
        return dp;
    }

    Control RobImagePane()
    {
        var sp = new DockPanel();
        var head = new WrapPanel();
        var file = Field("rob-img-path", "Kamerabild (.png, .bmp)", 300, RobImage); head.Children.Add(file);
        var color = new ComboBox { Name = "rob-img-color", ItemsSource = Vision.Hues.Keys.ToList(), SelectedItem = RobColor, Width = 110, Margin = new Thickness(6, 0) };
        color.SelectionChanged += (_, _) => { if (color.SelectedItem is string c && c != RobColor) { RobColor = c; Show("roboter"); } };
        head.Children.Add(color);
        var open = new Button { Name = "rob-img-open", Content = "Prüfen", Padding = new Thickness(10, 4), Background = Accent, Foreground = Brushes.White };
        open.Click += (_, _) => RobRun(() => { var p = Need(file, "das Bild"); if (!File.Exists(p)) throw new FileNotFoundException("Diese Datei gibt es nicht."); Vision.Load(p); RobImage = p; _robMsg = ""; });
        var mask = new CheckBox { Name = "rob-img-mask", Content = "Maske zeigen", IsChecked = _robMask, Margin = new Thickness(8, 0) }; mask.IsCheckedChanged += (_, _) => { _robMask = mask.IsChecked == true; Show("roboter"); };
        head.Children.Add(open); head.Children.Add(mask);
        DockPanel.SetDock(head, Avalonia.Controls.Dock.Top); sp.Children.Add(head);
        if (RobImage.Length == 0 || !File.Exists(RobImage)) { sp.Children.Add(new TextBlock { Text = "Ein Kamerabild wählen – Trendo zeigt, wo es Objekte der Farbe findet, ob das Licht passt und wie scharf das Bild ist.", Foreground = Muted, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 20) }); return sp; }
        var img = Vision.Load(RobImage); var blobs = VisionPlus.FindAll(img, RobColor, 20); var exp = VisionPlus.CheckExposure(img); var sharp = VisionPlus.Sharpness(img);
        Bitmap bmp; using (var ms = new MemoryStream(_robMask ? VisionPlus.EncodePng(VisionPlus.MaskImage(img, RobColor)) : File.ReadAllBytes(RobImage))) bmp = new Bitmap(ms);
        var canvas = new Canvas { Width = img.Width, Height = img.Height, Children = { new Image { Source = bmp, Width = img.Width, Height = img.Height } } };
        for (var k = 0; k < blobs.Count; k++)
        {
            var b = blobs[k]; var r = new Rectangle { Width = b.MaxX - b.MinX + 1, Height = b.MaxY - b.MinY + 1, Stroke = Brush.Parse("#F5A524"), StrokeThickness = Math.Max(2, img.Width / 300.0) };
            Canvas.SetLeft(r, b.MinX); Canvas.SetTop(r, b.MinY); canvas.Children.Add(r);
            var t = new TextBlock { Text = (k + 1).ToString(), Foreground = Brushes.White, Background = Brush.Parse("#CCF5A524"), FontSize = Math.Max(12, img.Width / 50.0), Padding = new Thickness(3, 0) };
            Canvas.SetLeft(t, b.MinX); Canvas.SetTop(t, Math.Max(0, b.MinY - Math.Max(16, img.Width / 40.0))); canvas.Children.Add(t);
        }
        var info = new StackPanel { Spacing = 3, Margin = new Thickness(0, 6, 0, 0) };
        info.Children.Add(new TextBlock { Name = "rob-img-found", Text = blobs.Count == 0 ? $"Keine {RobColor}en Objekte gefunden ({img.Width} × {img.Height} Pixel)." : $"{blobs.Count} {RobColor}e(s) Objekt(e): " + string.Join(" · ", blobs.Take(6).Select((b, i) => $"{i + 1}. Mitte {b.CenterX:0}/{b.CenterY:0}, {b.Pixels} px")), Foreground = Text, FontSize = 12.5, TextWrapping = TextWrapping.Wrap });
        info.Children.Add(new TextBlock { Name = "rob-img-light", Text = $"{exp.Verdict} · Helligkeit {Fach.F(exp.MeanPct, 0)} % · Schärfe {Fach.F(sharp, 0)}{(sharp < 100 ? " – wahrscheinlich unscharf" : "")}", Foreground = Muted, FontSize = 12, TextWrapping = TextWrapping.Wrap });
        var tw = new TextBox { Name = "rob-img-tw", Text = "400", Width = 70 }; var th = new TextBox { Name = "rob-img-th", Text = "300", Width = 70 };
        var take = new Button { Name = "rob-img-target", Content = "Größtes Objekt als Arm-Ziel", Padding = new Thickness(10, 4), IsEnabled = blobs.Count > 0 };
        take.Click += (_, _) => RobRun(() => { var b = blobs.OrderByDescending(x => x.Pixels).First(); var (x, y) = Vision.PixelToTable(b.CenterX, b.CenterY, img.Width, img.Height, Fach.Num(tw.Text ?? "", "Breite"), Fach.Num(th.Text ?? "", "Höhe")); Bench.X = x.ToString("0"); Bench.Y = y.ToString("0"); RobMode = "arm"; _robMsg = $"Ziel aus dem Bild: {x:0} / {y:0} mm (Tisch {tw.Text} × {th.Text} mm, Kamera senkrecht)."; });
        info.Children.Add(new WrapPanel { Children = { new TextBlock { Text = "Tisch im Bild (mm)", Foreground = Muted, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) }, tw, new TextBlock { Text = "×", Foreground = Muted, Margin = new Thickness(4, 0), VerticalAlignment = VerticalAlignment.Center }, th, new Border { Width = 8 }, take } });
        var f2 = Field("rob-img2-path", "Zweites Bild derselben Kamera (Bewegung)", 260, RobImage2);
        var cmp = new Button { Name = "rob-img-motion", Content = "Bewegung vergleichen", Padding = new Thickness(10, 4) };
        cmp.Click += (_, _) => RobRun(() => { var p = Need(f2, "das zweite Bild"); RobImage2 = p; var m = VisionPlus.Motion(img, Vision.Load(p), 30); _robMsg = m.Moved ? $"Bewegung: {Fach.F(m.ChangedPct, 1)} % geändert, Bereich {m.MinX}/{m.MinY} bis {m.MaxX}/{m.MaxY}" : $"Keine nennenswerte Bewegung ({Fach.F(m.ChangedPct, 2)} % geändert)."; });
        info.Children.Add(new WrapPanel { Children = { f2, cmp } });
        DockPanel.SetDock(info, Avalonia.Controls.Dock.Bottom); sp.Children.Add(info);
        sp.Children.Add(new Border { Name = "rob-img-view", CornerRadius = new CornerRadius(10), ClipToBounds = true, Background = Brush.Parse("#212121"), Child = new Viewbox { Stretch = Stretch.Uniform, Child = canvas } });
        return sp;
    }

    Control RobCalcs()
    {
        var calcs = new StackPanel { Spacing = 6 };
        LiveCalc(calcs, "rob-servo", "Servo-Pulse", [("a", "Winkel (°)", "90", null), ("amin", "Winkel min", "0", null), ("amax", "Winkel max", "180", null), ("umin", "Puls min (µs)", "500", null), ("umax", "Puls max (µs)", "2500", null)],
            v => $"{ArmPlus.ServoPulse(Fach.Num(v("a")), Fach.Num(v("amin")), Fach.Num(v("amax")), (int)Fach.Num(v("umin")), (int)Fach.Num(v("umax")))} µs");
        LiveCalc(calcs, "rob-prof", "Fahrprofil", [("d", "Weg (mm oder °)", "100", null), ("v", "Höchstgeschwindigkeit (je s)", "50", null), ("a", "Beschleunigung (je s²)", "100", null)],
            v => { var p = ArmPlus.Profile(Fach.Num(v("d")), Fach.Num(v("v")), Fach.Num(v("a"))); return p.Triangle ? $"Kurzer Weg – Spitze {Fach.F(p.PeakSpeed, 1)}, Dauer {Fach.F(p.Total, 2)} s" : $"Beschleunigen {Fach.F(p.AccelTime, 2)} s · gleichmäßig {Fach.F(p.CruiseTime, 2)} s · Bremsen {Fach.F(p.AccelTime, 2)} s → {Fach.F(p.Total, 2)} s"; });
        LiveCalc(calcs, "rob-reach", "Reichweite in einer Höhe", [("z", "Höhe z (mm)", "40", null)],
            v => { var r = ArmPlus.Reach(new ArmConfig(), Fach.Num(v("z")), 20); return $"{Fach.F(r.ReachablePct, 1)} % des Quadrats erreichbar, bis {Fach.F(r.MaxRadius, 0)} mm vom Sockel"; });
        LiveCalc(calcs, "rob-cal", "Kalibrieren (schräge Kamera)", [("src", "Ecken im Bild „x y / …“", "100 50 / 500 60 / 520 400 / 80 380", null), ("dst", "Ecken auf dem Tisch (mm)", "0 300 / 400 300 / 400 0 / 0 0", null), ("pt", "Bildpunkt", "300 220", null)],
            v => { (double, double)[] P(string k) => v(k).Split('/').Select(x => x.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries)).Select(x => x.Length == 2 ? (Fach.Num(x[0]), Fach.Num(x[1])) : throw new FormatException("Jeder Punkt als „x y“.")).ToArray(); var h = VisionPlus.Homography(P("src"), P("dst")); var q = P("pt"); var (x, y) = VisionPlus.Apply(h, q[0].Item1, q[0].Item2); return $"Auf dem Tisch: x = {Fach.F(x, 1)} mm, y = {Fach.F(y, 1)} mm"; });
        return new StackPanel { Spacing = 6, Children = { H("Rechner", 13), calcs } };
    }
}
