using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Trendo.Core;
using Trendo.Core.Programs;

namespace Trendo.App;

/// <summary>SPEC-14 / T-0266: CNC & Fertigung als echtes Programm „CNC-Werkstatt“ (Vorbild CAMotics/NC Viewer, nur Funktionsumfang):
/// G-Code-Editor, Werkzeugweg-Vorschau mit Simulation (Abspielen, Schieberegler, aktuelle Zeile), Prüfbericht mit Sprung zur Zeile,
/// Generatoren (Tasche, Kontur, Lochkreis), Nullpunkt verschieben und Werkstatt-Rechner, die sofort rechnen. Ersetzt die Formularseite.</summary>
public partial class MainWindow
{
    const string CK = "cnc";
    private double _cncT = -1; // Simulationszeit in Minuten, −1 = Ende
    private bool _cncIso;
    private DispatcherTimer? _cncTimer;
    private CncPathView? _cncView; private Slider? _cncSlider; private TextBlock? _cncNow; private TextBox? _cncEditor;
    private string? _cncSegKey; private List<CncSeg> _cncSegs = [];
    public bool CncPlaying => _cncTimer is { IsEnabled: true };
    public double CncTime => _cncT;

    private void InitSpec14() => RegisterProgram(FachTabs, new ProgramSpec(CK, "SPEC-14", "CNC-Werkstatt")
    {
        GroupLevels = new() { ["Erzeugen"] = 1 },
        Sentence = "G-Code schreiben, erzeugen und prüfen – mit Vorschau und Simulation, bevor die Maschine läuft.",
        Folder = "CNC",
        FillHeight = true,
        Templates =
        [
            new("leer", "Leeres Programm", "G-Code selbst schreiben oder eine Datei laden", () => CncDoc(new CncJob { Gcode = "(Neues Programm)\nG21 G90\nG0 Z5\nG0 X0 Y0\nG1 Z-1 F200\nG1 X40 F600\nG1 Y20\nG1 X0\nG1 Y0\nG0 Z5\nM30\n" })),
            new("tasche", "Rechtecktasche", "40 × 20 × 3 mm, Fräser 6 mm, Bahnen im Kreis", () => { var j = new CncJob(); j.Gcode = j.Pocket(); return CncDoc(j); }),
            new("kontur", "Außenkontur", "50 × 30 mm ausfräsen, 4 mm tief", () => { var j = new CncJob { W = 50, H = 30, Depth = 4, StepDown = 2, Feed = 800 }; j.Gcode = j.Outline(); return CncDoc(j); }),
            new("lochkreis", "Lochkreis", "6 Bohrungen mit Spanbruch", () => { var j = new CncJob { W = 100, H = 100, Depth = 10, ToolD = 5, Feed = 400 }; j.Gcode = j.Bolt(); return CncDoc(j); }),
        ],
        Ribbon =
        [
            ("Simulation", [
                new ProgramCmd("cnc-play", "▶ Abspielen / Pause", _ => CncPlay(), true),
                new ProgramCmd("cnc-start", "⏮ Anfang", _ => { CncStop(); CncSeek(0); }),
                new ProgramCmd("cnc-end", "⏭ Ende", _ => { CncStop(); CncSeek(-1); }),
            ]),
            ("Ansicht", [
                new ProgramCmd("cnc-v-top", "Von oben", _ => { _cncIso = false; Show(CK); }),
                new ProgramCmd("cnc-v-iso", "Schräg (3D)", _ => { _cncIso = true; Show(CK); }),
                new ProgramCmd("cnc-v-stock", "Werkstück in 3D", _ => { _cncStock = !_cncStock; Show(CK); }, false, "Zeigt das Rohteil, das bis zum aktuellen Simulationsschritt abgetragen ist"),
            ]),
            ("Erzeugen", [
                new ProgramCmd("cnc-g-pocket", "Tasche", d => CncGenerate(d, "Tasche erzeugen", j => j.Pocket())),
                new ProgramCmd("cnc-g-outline", "Kontur", d => CncGenerate(d, "Kontur erzeugen", j => j.Outline())),
                new ProgramCmd("cnc-g-bolt", "Lochkreis", d => CncGenerate(d, "Lochkreis erzeugen", j => j.Bolt())),
            ]),
        ],
        Exports =
        [
            new("nc", "G-Code (.nc)", ".nc", (d, f) => { File.WriteAllText(f, CncJob.From(d.Data).Gcode); return f; }),
            new("svg", "Vorschau (SVG)", ".svg", (d, f) => { File.WriteAllText(f, CncReport(d).Svg); return f; }),
            new("report", "Prüfbericht", ".md", (d, f) => { var j = CncJob.From(d.Data); File.WriteAllText(f, CncSim.Report(d.Title, CncReport(d), (j.EnvX, j.EnvY, j.EnvZ))); return f; }),
        ],
        Workspace = CncWorkspace,
        Properties = CncProperties,
        Status = d => { try { var r = CncReport(d); return $"{r.Lines} Zeilen · {r.Moves} Bewegungen · Dauer ca. {(int)r.Time.TotalHours}:{r.Time.Minutes:00}:{r.Time.Seconds:00} · Fräsweg {Fach.F(r.CutMm / 1000, 2)} m · {(r.Warnings.Count == 0 ? "✓ keine Auffälligkeiten" : $"⚠ {r.Warnings.Count} Hinweise")}"; } catch (Exception ex) when (ex is InvalidOperationException or FormatException) { return "⚠ " + ex.Message; } },
        Help = "Links steht der G-Code – tippen und mit F5, Strg+Enter oder einem Klick in die Vorschau prüfen. Rechts zeigt die Vorschau die Bahnen; „Abspielen“ fährt das Programm nach, der Regler springt an jede Stelle, die gerade gefahrene Zeile wird links markiert. Hinweise unten springen per Klick zur Zeile.",
    }, "cnc", "fräse", "fräsen", "g-code", "gcode", "drehzahl", "vorschub", "laser", "fertigung", "camotics", "nc-viewer");

    static JsonObject CncDoc(CncJob j) { var o = new JsonObject(); j.WriteTo(o); return o; }
    void CncEdit(ProgramDocument d, string label, Action<CncJob> change) => d.Edit(label, x => { var j = CncJob.From(x); change(j); j.WriteTo(x); });
    void CncGenerate(ProgramDocument d, string label, Func<CncJob, string> gen) { var j = CncJob.From(d.Data); var g = gen(j); Cnc.Analyse(g); CncEdit(d, label, x => x.Gcode = g); CncStop(); _cncT = -1; }

    GcodeReport CncReport(ProgramDocument d) { var j = CncJob.From(d.Data); return Cnc.Analyse(j.Gcode, j.Rapid, (j.EnvX, j.EnvY, j.EnvZ)); }
    List<CncSeg> CncSegs(ProgramDocument d)
    {
        var j = CncJob.From(d.Data); var key = j.Rapid + "|" + j.Gcode;
        if (key != _cncSegKey) { _cncSegs = CncSim.Segments(j.Gcode, j.Rapid); _cncSegKey = key; }
        return _cncSegs;
    }

    public void CncPlay()
    {
        if (CncPlaying) { CncStop(); return; }
        if (_cncSegs.Count == 0) return;
        var total = _cncSegs.Sum(s => s.Minutes); if (_cncT < 0 || _cncT >= total) _cncT = 0;
        _cncTimer ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(40) };
        _cncTimer.Tick -= CncTick; _cncTimer.Tick += CncTick; _cncTimer.Start();
    }
    void CncStop() => _cncTimer?.Stop();
    void CncTick(object? s, EventArgs e)
    {
        if (CurrentView != CK || _cncView is null) { CncStop(); return; }
        var total = _cncSegs.Sum(x => x.Minutes); _cncT += Math.Max(total / 250, 1e-6); // ganzes Programm in etwa 10 s
        if (_cncT >= total) { _cncT = -1; CncStop(); }
        CncSeek(_cncT);
    }

    /// <summary>Simulation an eine Zeitstelle setzen (Minuten, −1 = Ende) und Vorschau, Regler und markierte Zeile nachziehen.</summary>
    public void CncSeek(double minutes)
    {
        _cncT = minutes;
        if (_cncView is null || _cncSegs.Count == 0) return;
        var idx = minutes < 0 ? _cncSegs.Count - 1 : CncSim.AtTime(_cncSegs, minutes);
        _cncView.Pos = minutes < 0 ? -1 : idx; _cncView.InvalidateVisual();
        var total = _cncSegs.Sum(x => x.Minutes); var t = minutes < 0 ? total : minutes;
        if (_cncSlider is not null && Math.Abs(_cncSlider.Value - t) > 1e-9) { _cncSlider.Tag = "sim"; _cncSlider.Value = t; _cncSlider.Tag = null; }
        var seg = _cncSegs[idx];
        if (_cncNow is not null) _cncNow.Text = $"Zeile {seg.Line} · {TimeSpan.FromMinutes(t):h\\:mm\\:ss} von {TimeSpan.FromMinutes(total):h\\:mm\\:ss} · X {Fach.F(seg.X1, 2)} Y {Fach.F(seg.Y1, 2)} Z {Fach.F(seg.Z1, 2)}{(seg.Rapid ? " · Eilgang" : "")}";
        CncMarkLine(seg.Line);
    }

    /// <summary>Zeile im Editor markieren (Simulation oder Klick auf einen Hinweis).</summary>
    public void CncMarkLine(int line)
    {
        if (_cncEditor?.Text is not { } text) return;
        var start = 0; for (var l = 1; l < line && start >= 0; l++) { start = text.IndexOf('\n', start); if (start >= 0) start++; }
        if (start < 0) return; var end = text.IndexOf('\n', start); if (end < 0) end = text.Length;
        _cncEditor.SelectionStart = start; _cncEditor.SelectionEnd = end; _cncEditor.CaretIndex = end;
    }
    public int CncSelectedLine => _cncEditor?.Text is { } t ? t[..Math.Min(_cncEditor.SelectionStart, t.Length)].Count(c => c == '\n') + 1 : 0;

    private Control CncWorkspace(ProgramDocument d)
    {
        var j = CncJob.From(d.Data);
        var ed = new TextBox { Name = "cnc-editor", Text = j.Gcode, AcceptsReturn = true, FontFamily = new FontFamily("Consolas, Cascadia Mono, DejaVu Sans Mono, monospace"), FontSize = 12.5, TextWrapping = TextWrapping.NoWrap,
            Background = Brush.Parse("#1B1B1B"), Foreground = Brush.Parse("#D6E4FF"), SelectionBrush = Brush.Parse("#F5A52466"), VerticalContentAlignment = VerticalAlignment.Top };
        ScrollViewer.SetHorizontalScrollBarVisibility(ed, Avalonia.Controls.Primitives.ScrollBarVisibility.Auto);
        void Commit() { var t = ed.Text ?? ""; if (t != CncJob.From(d.Data).Gcode) { ProgramRun(CK, () => { CncEdit(d, "G-Code ändern", x => x.Gcode = t); CncStop(); _cncT = -1; }); } }
        ed.LostFocus += (_, _) => Commit();
        ed.KeyDown += (_, e) => { if (e.Key == Avalonia.Input.Key.F5 || (e.Key == Avalonia.Input.Key.Enter && e.KeyModifiers.HasFlag(Avalonia.Input.KeyModifiers.Control))) { Commit(); e.Handled = true; } }; // F5 / Strg+Enter: prüfen
        _cncEditor = ed;
        List<CncSeg> segs; GcodeReport? rep = null; string? err = null;
        try { segs = CncSegs(d); rep = CncReport(d); } catch (Exception ex) when (ex is InvalidOperationException or FormatException) { segs = []; err = ex.Message; }
        var view = new CncPathView { Name = "cnc-view", Segs = segs, Iso = _cncIso, ToolD = j.ToolD, Envelope = (j.EnvX, j.EnvY, j.EnvZ), Cursor = new Avalonia.Input.Cursor(_cncIso ? Avalonia.Input.StandardCursorType.SizeAll : Avalonia.Input.StandardCursorType.Arrow) };
        _cncView = view;
        var total = segs.Sum(s => s.Minutes);
        var slider = new Slider { Name = "cnc-sim", Minimum = 0, Maximum = Math.Max(1e-6, total), Value = _cncT < 0 ? total : _cncT, Margin = new Thickness(8, 0) };
        slider.PropertyChanged += (_, e) => { if (e.Property == Slider.ValueProperty && slider.Tag is null) { CncStop(); CncSeek(slider.Value >= total - 1e-9 ? -1 : slider.Value); } };
        _cncSlider = slider;
        var now = new TextBlock { Name = "cnc-now", Foreground = Brush.Parse("#F5A524"), FontSize = 12, VerticalAlignment = VerticalAlignment.Center };
        _cncNow = now;
        var play = new Button { Name = "cnc-sim-play", Content = CncPlaying ? "⏸" : "▶", Padding = new Thickness(12, 4), Background = Accent, Foreground = Brushes.White };
        play.Click += (_, _) => { CncPlay(); play.Content = CncPlaying ? "⏸" : "▶"; };
        var simBar = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), RowDefinitions = new RowDefinitions("Auto,Auto"), Margin = new Thickness(0, 6, 0, 0), Children = { play, Pos(slider, 1), Grid2(now, 1, 0, 2) } };
        // Hinweise mit Sprung zur Zeile
        var notes = new StackPanel { Name = "cnc-notes", Spacing = 2, Margin = new Thickness(0, 6, 0, 0) };
        if (err is not null) notes.Children.Add(new TextBlock { Name = "cnc-error", Text = "⚠ " + err, Foreground = Brush.Parse("#F87171"), FontSize = 12, TextWrapping = TextWrapping.Wrap });
        else if (rep is not null)
        {
            if (rep.Warnings.Count == 0) notes.Children.Add(new TextBlock { Name = "cnc-ok", Text = "✓ Keine Auffälligkeiten.", Foreground = Brush.Parse("#4ADE80"), FontSize = 12 });
            for (var k = 0; k < rep.Warnings.Count; k++)
            {
                var w = rep.Warnings[k]; var m = System.Text.RegularExpressions.Regex.Match(w, @"^Zeile (\d+)");
                var b = new Button { Name = $"cnc-warn-{k}", Content = "⚠ " + w, Padding = new Thickness(6, 2), Foreground = Brush.Parse("#FACC15"), Background = Brushes.Transparent, HorizontalContentAlignment = HorizontalAlignment.Left, FontSize = 12 };
                if (m.Success) { var line = int.Parse(m.Groups[1].Value); b.Click += (_, _) => { CncStop(); var i = segs.FindIndex(s => s.Line >= line); if (i >= 0) CncSeek(segs.Take(i + 1).Sum(s => s.Minutes)); CncMarkLine(line); ed.Focus(); }; }
                notes.Children.Add(b);
            }
        }
        var right = new DockPanel();
        DockPanel.SetDock(notes, Avalonia.Controls.Dock.Bottom); DockPanel.SetDock(simBar, Avalonia.Controls.Dock.Bottom);
        right.Children.Add(notes); right.Children.Add(simBar); right.Children.Add(new Border { CornerRadius = new CornerRadius(10), ClipToBounds = true, Child = _cncStock ? CncStockView(d, segs, j) : view });
        var left = new DockPanel { Children = { new Border { CornerRadius = new CornerRadius(10), ClipToBounds = true, Child = ed } } };
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("340,10,*"), Children = { left, Pos(right, 2) } };
        _ui.Post(() => CncSeek(_cncT), DispatcherPriority.Background);
        return grid;
    }

    static Control Grid2(Control c, int row, int col, int span) { Grid.SetRow(c, row); Grid.SetColumn(c, col); Grid.SetColumnSpan(c, span); return c; }

    private Control CncProperties(ProgramDocument d)
    {
        var j = CncJob.From(d.Data); var sp = new StackPanel { Spacing = 4 };
        Button Act2(string name, string label, Action go) { var b = new Button { Name = name, Content = label, Padding = new Thickness(10, 4), Margin = new Thickness(0, 4, 4, 0) }; b.Click += (_, _) => go(); return b; }
        void Num(string name, string label, double val, Action<CncJob, double> set)
        {
            var t = new TextBox { Name = name, Text = val.ToString("0.###", De), Width = 80 };
            void Commit() { if (double.TryParse(t.Text, System.Globalization.NumberStyles.Float, De, out var v) && v > 0 && v != val) { CncEdit(d, label, x => set(x, v)); Show(CK); } }
            t.LostFocus += (_, _) => Commit(); OnEnter(t, Commit);
            sp.Children.Add(Row(P(label, Text, 12), t));
        }
        sp.Children.Add(H("Maschine", 12));
        Num("cnc-p-envx", "Arbeitsraum X (mm)", j.EnvX, (x, v) => x.EnvX = v); Num("cnc-p-envy", "Arbeitsraum Y (mm)", j.EnvY, (x, v) => x.EnvY = v); Num("cnc-p-envz", "Arbeitsraum Z (mm)", j.EnvZ, (x, v) => x.EnvZ = v); Num("cnc-p-rapid", "Eilgang (mm/min)", j.Rapid, (x, v) => x.Rapid = v);
        sp.Children.Add(H("Werkstück & Fräser (für „Erzeugen“)", 12));
        Num("cnc-p-w", "Breite (mm)", j.W, (x, v) => x.W = v); Num("cnc-p-h", "Höhe (mm)", j.H, (x, v) => x.H = v); Num("cnc-p-depth", "Tiefe (mm)", j.Depth, (x, v) => x.Depth = v);
        Num("cnc-p-tool", "Fräser-Ø (mm)", j.ToolD, (x, v) => x.ToolD = v); Num("cnc-p-step", "Zustellung (mm)", j.StepDown, (x, v) => x.StepDown = v); Num("cnc-p-feed", "Vorschub (mm/min)", j.Feed, (x, v) => x.Feed = v); Num("cnc-p-plunge", "Eintauchen (mm/min)", j.Plunge, (x, v) => x.Plunge = v);
        // Datei laden und Nullpunkt
        sp.Children.Add(H("G-Code-Datei", 12));
        var file = Field("cnc-file-path", "G-Code-Datei (.nc, .gcode, .tap, .ngc)", 140); sp.Children.Add(file);
        sp.Children.Add(Act2("cnc-load", "Laden", () => ProgramRun(CK, () => { var p = Need(file, "die G-Code-Datei"); if (!File.Exists(p)) throw new FileNotFoundException("Diese Datei gibt es nicht."); var g = File.ReadAllText(p); Cnc.Analyse(g); CncEdit(d, "Datei laden", x => x.Gcode = g); CncStop(); _cncT = -1; })));
        sp.Children.Add(H("Nullpunkt verschieben", 12));
        var off = new TextBox { Name = "cnc-offset", Text = "0 0 0", PlaceholderText = "X Y Z in mm" };
        sp.Children.Add(Row(P("um X Y Z (mm)", Text, 12), off));
        sp.Children.Add(Act2("cnc-offset-run", "Verschieben", () => ProgramRun(CK, () => { var v = (off.Text ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(x => Fach.Num(x)).ToArray(); if (v.Length != 3) throw new FormatException("Bitte drei Zahlen: X Y Z."); CncEdit(d, "Nullpunkt verschieben", x => x.Gcode = CncPlus.Offset(x.Gcode, v[0], v[1], v[2])); })));
        // Werkstatt-Rechner (sofort)
        var calcs = new StackPanel { Spacing = 6 };
        LiveCalc(calcs, "cnc-feeds", "Drehzahl & Vorschub", [("mat", "Werkstoff", j.Material, [.. Cnc.Materials.Keys]), ("d", "Fräser-Ø (mm)", j.ToolD.ToString("0.##", De), null), ("z", "Schneiden", "2", ["1", "2", "3", "4"]), ("max", "max. Drehzahl", "24000", null)],
            v => { var r = Cnc.Feeds(v("mat"), Fach.Num(v("d")), (int)Fach.Num(v("z")), Fach.Num(v("max"))); return $"Drehzahl {Fach.F(r.Rpm, 0)} U/min · Vorschub {Fach.F(r.FeedMmMin, 0)} mm/min (fz {Fach.F(r.Fz, 3)} mm, Vc {Fach.F(r.Vc, 0)} m/min){(r.Limited ? " · Spindel am Limit" : "")}"; });
        LiveCalc(calcs, "cnc-mrr", "Zerspanleistung", [("ae", "Eingriffsbreite (mm)", "3", null), ("ap", "Schnitttiefe (mm)", "6", null), ("vf", "Vorschub (mm/min)", j.Feed.ToString("0", De), null), ("mat", "Werkstoff", j.Material, [.. CncPlus.Kc.Keys])],
            v => { var m = CncPlus.Mrr(Fach.Num(v("ae")), Fach.Num(v("ap")), Fach.Num(v("vf")), v("mat")); return $"Spanvolumen {Fach.F(m.Qcm3, 1)} cm³/min · Schnitt {Fach.F(m.CutKw, 2)} kW · Spindel ca. {Fach.F(m.SpindleKw, 2)} kW"; });
        var cycle = 0.0; try { cycle = CncReport(d).Time.TotalMinutes; } catch (Exception ex) when (ex is InvalidOperationException or FormatException) { }
        LiveCalc(calcs, "cnc-cost", "Teilekosten (Zeit aus dem Programm)", [("qty", "Stückzahl", "10", null), ("rate", "Stundensatz (€)", "60", null), ("setup", "Rüstzeit (min)", "30", null), ("mat", "Material je Teil (€)", "2,50", null)],
            v => { var c = CncPlus.PartCost(TimeSpan.FromMinutes(cycle), (int)Fach.Num(v("qty")), Fach.Num(v("rate")), Fach.Num(v("setup")), Fach.Num(v("mat"))); return $"{Fach.F(c.PerPart)} € je Teil · {Fach.F(c.Total)} € gesamt (Programm {Fach.F(cycle, 1)} min)"; });
        LiveCalc(calcs, "cnc-tap", "Kernloch", [("t", "Gewinde (z. B. M8, M10x1)", "M8", null)], v => { var t = CncPlus.TapDrill(v("t")); return $"M{Fach.F(t.D, 1)} × {Fach.F(t.Pitch, 2)}: Kernlochbohrer Ø {Fach.F(t.Drill, 2)} mm"; });
        sp.Children.Add(H("Werkstatt-Rechner", 12)); sp.Children.Add(calcs);
        return sp;
    }
}
