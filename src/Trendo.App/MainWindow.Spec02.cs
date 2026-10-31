using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Trendo.Core;
using Trendo.Core.Elektro;
using Trendo.Core.Programs;

namespace Trendo.App;

/// <summary>SPEC-02 / T-0259: Elektronik & Platinen als echtes Programm „Schaltung & Simulation“ – Schaltplan zeichnen (DIN-Schaltzeichen),
/// live simulieren (MNA: R, C, L, Quellen, Schalter, Dioden, LEDs), Spannungen farbig, Strompunkte, Oszilloskop, Prüfung, Export SPICE/Stückliste/SVG.
/// Die früheren Rechner (Farbcode, LED-Vorwiderstand …) rechnen live in der rechten Spalte. Ersetzt die Formularseite.</summary>
public partial class MainWindow
{
    const string EK = "elektronik";
    public readonly CircuitViewState CircuitState = new();
    private Simulator? _sim; private string _simKey = "";
    private DispatcherTimer? _simTimer;

    static readonly (string Id, string Label, string Glyph)[] CircuitTools =
    [
        ("auswahl", "Auswahl", "M4,2 L4,20 L9,15 L12,22 L15,21 L12,14 L19,14 Z"),
        ("draht", "Draht", "M2,12 L22,12 M2,12 m-1.5,0 a1.5,1.5 0 1,0 3,0 a1.5,1.5 0 1,0 -3,0 M22,12 m-1.5,0 a1.5,1.5 0 1,0 3,0 a1.5,1.5 0 1,0 -3,0"),
        ("widerstand", "Widerst.", "M1,12 L6,12 M6,8 L18,8 L18,16 L6,16 Z M18,12 L23,12"),
        ("kondensator", "Kondens.", "M1,12 L10,12 M10,5 L10,19 M14,5 L14,19 M14,12 L23,12"),
        ("spule", "Spule", "M1,12 L5,12 M5,9 L19,9 L19,15 L5,15 Z M19,12 L23,12"),
        ("spannung", "Batterie", "M1,12 L10,12 M10,4 L10,20 M14,8 L14,16 M14,12 L23,12"),
        ("wechsel", "Wechsel", "M1,12 L5,12 M12,12 m-7,0 a7,7 0 1,0 14,0 a7,7 0 1,0 -14,0 M8,12 Q10,7 12,12 T16,12 M19,12 L23,12"),
        ("masse", "Masse", "M12,2 L12,12 M5,12 L19,12 M8,16 L16,16 M11,20 L13,20"),
        ("schalter", "Schalter", "M1,14 L7,14 L17,6 M17,14 L23,14"),
        ("diode", "Diode", "M1,12 L7,12 M7,6 L17,12 L7,18 Z M17,6 L17,18 M17,12 L23,12"),
        ("led", "LED", "M1,12 L7,12 M7,6 L17,12 L7,18 Z M17,6 L17,18 M17,12 L23,12 M12,4 L16,0 M15,5 L19,1"),
        ("lampe", "Lampe", "M1,12 L5,12 M12,12 m-7,0 a7,7 0 1,0 14,0 a7,7 0 1,0 -14,0 M7,7 L17,17 M17,7 L7,17 M19,12 L23,12"),
    ];

    private void InitSpec02() => RegisterProgram(FachTabs, new ProgramSpec(EK, "SPEC-02", "Schaltung & Simulation")
    {
        Sentence = "Schaltplan zeichnen und live simulieren – Spannungen, Ströme, Oszilloskop, Prüfung und Stückliste.",
        Folder = "Schaltungen",
        FillHeight = true,
        Templates =
        [
            new("leer", "Leere Schaltung", "Selbst Bauteile setzen und verdrahten", () => CircuitDoc(new Circuit())),
            new("led", "LED mit Vorwiderstand", "9-V-Batterie, 330 Ω, rote LED", () => CircuitDoc(Circuit.Template("led"))),
            new("teiler", "Spannungsteiler", "12 V an 10 kΩ / 4,7 kΩ", () => CircuitDoc(Circuit.Template("teiler"))),
            new("rc", "RC-Glied", "Kondensator über 1 kΩ laden – Schalter umlegen", () => CircuitDoc(Circuit.Template("rc"))),
            new("gleichrichter", "Gleichrichter", "12 V Wechselspannung, Diode, Glättung", () => CircuitDoc(Circuit.Template("gleichrichter"))),
            new("schwingkreis", "Schwingkreis", "1 H und 15 µF – schwingt mit 41 Hz", () => CircuitDoc(Circuit.Template("schwingkreis"))),
        ],
        Ribbon =
        [
            ("Simulation", [
                new ProgramCmd("ek-run", "▶ / ❚❚", _ => CircuitState.Running = !CircuitState.Running, true, "Simulation starten/anhalten"),
                new ProgramCmd("ek-reset", "Neu starten", _ => _simKey = "", false, "Alle Kondensatoren entladen, Zeit auf 0"),
                new ProgramCmd("ek-speed", "Tempo", _ => CircuitState.Speed = CircuitState.Speed >= 1 ? 0.1 : CircuitState.Speed >= 0.1 ? 0.01 : 1, false, "Echtzeit → 10× → 100× langsamer"),
            ]),
            ("Ansicht", [new ProgramCmd("ek-fit", "Einpassen", _ => { CircuitState.Fitted = false; CircuitState.UserView = false; })]),
        ],
        Exports =
        [
            new("spice", "SPICE-Netzliste", ".cir", (d, f) => { File.WriteAllText(f, Circuit.From(d.Data).Spice()); return f; }),
            new("bom", "Stückliste (CSV)", ".csv", (d, f) => { File.WriteAllText(f, Circuit.From(d.Data).BomCsv(), new System.Text.UTF8Encoding(true)); return f; }),
            new("svg", "Schaltplan (SVG)", ".svg", (d, f) => { File.WriteAllText(f, CircuitDrawing.Svg(Circuit.From(d.Data))); return f; }),
        ],
        Workspace = CircuitWorkspace,
        Properties = CircuitProperties,
        Status = d => { var c = Circuit.From(d.Data); var parts = c.Parts.Count(p => p.Kind is not ("draht" or "masse")); return $"{CircuitTools.FirstOrDefault(t => t.Id == CircuitState.Tool).Label} · {parts} Bauteile · {(CircuitState.Running ? "Simulation läuft" : "angehalten")}" + (_sim?.Last is { } s ? $" · t = {s.Time:0.000} s" : ""); },
        Help = "Links ein Bauteil wählen und auf der Fläche von Punkt zu Punkt ziehen (Klick = 4 Raster lang). Draht verbindet Punkte; was sich trifft, ist verbunden. "
             + "Auswahl: anklicken, ziehen verschiebt, R dreht, Entf löscht, ein ausgewählter Schalter schaltet per Klick um. Die Simulation läuft sofort mit – grün = Plus, rot = Minus, gelbe Punkte = Strom.",
    }, "elektronik", "widerstand", "led", "platine", "pcb", "farbcode", "ohm", "spannung", "stückliste", "schaltung", "schaltplan", "simulation");

    static System.Text.Json.Nodes.JsonObject CircuitDoc(Circuit c) { var o = new System.Text.Json.Nodes.JsonObject(); c.WriteTo(o); return o; }

    public void CircuitEdit(string label, Action<Circuit> change)
    {
        var d = ProgramActive(EK) ?? throw new InvalidOperationException("Es ist keine Schaltung offen.");
        CircuitState.Hint = "";
        ProgramRun(EK, () => d.Edit(label, x => { var c = Circuit.From(x); change(c); c.WriteTo(x); }));
    }

    public void CircuitTool(string tool) { CircuitState.Tool = tool; CircuitState.Hint = ""; if (tool != "auswahl") CircuitState.Selected = null; Show(EK); }
    public void CircuitSelect(string? id) { CircuitState.Selected = id; Show(EK); }

    /// <summary>Simulator zur aktuellen Schaltung (neu bei jeder Änderung – Kondensatorladungen beginnen dann bei 0).</summary>
    public Simulator? CircuitSim()
    {
        if (ProgramActive(EK) is not { } d) return null;
        var c = Circuit.From(d.Data); var key = d.Data.ToJsonString();
        if (_sim is null || key != _simKey) { _sim = new Simulator(c, 5e-5); _simKey = key; CircuitState.Scope.Clear(); CircuitState.Phase.Clear(); _sim.Step(); }
        return _sim;
    }

    /// <summary>Simulation um <paramref name="seconds"/> Simulationszeit weiterrechnen (für den Zeitgeber und für Tests).</summary>
    public void CircuitAdvance(double seconds)
    {
        if (CircuitSim() is not { } sim || sim.Problem is not null) return;
        var steps = Math.Clamp((int)Math.Round(seconds / sim.Dt), 1, 4000); SimState? st = null;
        var sel = CircuitState.Selected; var every = Math.Max(1, steps / 20);
        for (var k = 0; k < steps; k++)
        {
            st = sim.Step();
            if (sel is not null && k % every == 0 && st.Parts.TryGetValue(sel, out var r)) { CircuitState.Scope.Add((st.Time, r.V, r.I)); if (CircuitState.Scope.Count > 600) CircuitState.Scope.RemoveAt(0); }
        }
        if (st is null) return;
        // Strompunkte: Geschwindigkeit mit dem Strom (logarithmisch, damit µA und A beide sichtbar sind)
        foreach (var (id, (_, i)) in st.Parts)
            CircuitState.Phase[id] = CircuitState.Phase.GetValueOrDefault(id) + Math.Sign(i) * Math.Clamp(Math.Log10(1 + Math.Abs(i) * 1e4) * 3, 0, 12);
    }

    void EnsureSimTimer()
    {
        if (_simTimer is not null) return;
        _simTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(40) };
        _simTimer.Tick += (_, _) =>
        {
            if (!CircuitState.Running || ProgramActive(EK) is null) return;
            var canvas = this.VisibleCircuit(); if (canvas is null) return; // nur wenn die Schaltung sichtbar ist
            CircuitAdvance(0.04 * CircuitState.Speed);
            canvas.InvalidateVisual();
            if (_simLive is { } live && CircuitState.Selected is { } sel && _sim?.Last?.Parts.TryGetValue(sel, out var r) == true)
                live.Text = $"U {ElectronicsPlus.Fmt(Math.Abs(r.V), "V")} · I {ElectronicsPlus.Fmt(Math.Abs(r.I), "A")} · P {ElectronicsPlus.Fmt(Math.Abs(r.V * r.I), "W")}";
        };
        _simTimer.Start();
    }
    private TextBlock? _simLive;

    private Control CircuitWorkspace(ProgramDocument d)
    {
        EnsureSimTimer();
        var c = Circuit.From(d.Data); var sim = CircuitSim();
        var tools = new WrapPanel { Name = "ek-tools", Width = 112 };
        foreach (var (id, label, glyph) in CircuitTools)
        {
            var on = CircuitState.Tool == id; var tid = id;
            var b = new Button { Name = "ek-tool-" + id, Width = 53, Height = 44, Padding = new Thickness(1), Margin = new Thickness(0, 0, 3, 3), CornerRadius = new CornerRadius(10), Focusable = false,
                Background = on ? Accent : Brush.Parse(C("#1AFFFFFF", "#0D000000")), BorderBrush = on ? Brush.Parse("#F5A524") : Edge, BorderThickness = new Thickness(on ? 1.5 : 1),
                Content = new StackPanel { Spacing = 1, HorizontalAlignment = HorizontalAlignment.Center, Children = {
                    new Avalonia.Controls.Shapes.Path { Data = Geometry.Parse(glyph), Stroke = on ? Brushes.White : Text, StrokeThickness = 1.5, Fill = Brushes.Transparent, Stretch = Stretch.Uniform, Width = 22, Height = 16, HorizontalAlignment = HorizontalAlignment.Center },
                    new TextBlock { Text = label, FontSize = 10, Foreground = on ? Brushes.White : Text, HorizontalAlignment = HorizontalAlignment.Center } } } };
            ToolTip.SetTip(b, id == "auswahl" ? "Auswählen, verschieben, Schalter umlegen (Esc)" : PartTypes.Get(id).Label);
            b.Click += (_, _) => CircuitTool(tid); tools.Children.Add(b);
        }
        var canvas = new CircuitCanvas(c, () => _sim?.Last, sim?.Problem, CircuitState, CircuitEdit, CircuitSelect) { Name = "ek-canvas", MinHeight = 160 };
        var frame = new Border { CornerRadius = new CornerRadius(12), ClipToBounds = true, BorderBrush = EdgeLit, BorderThickness = new Thickness(1), Child = canvas };
        return new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,10,*"), Children = { new ScrollViewer { Content = tools }, Pos(frame, 2) } };
    }

    private Control CircuitProperties(ProgramDocument d)
    {
        var c = Circuit.From(d.Data); var sp = new StackPanel { Spacing = 5 }; var sel = CircuitState.Selected; var st = _sim?.Last; _simLive = null;
        Button Act2(string name, string label, Action go) { var b = new Button { Name = name, Content = label, Padding = new Thickness(10, 4), Margin = new Thickness(0, 4, 4, 0) }; b.Click += (_, _) => go(); return b; }
        void Fact(string label, string value, IBrush? color = null) => sp.Children.Add(Row(P(label, null, 12), new TextBlock { Text = value, Foreground = color ?? Text, FontSize = 12, FontWeight = FontWeight.SemiBold }));
        void Val(string name, string label, string text, Action<Part, string> set)
        {
            var t = new TextBox { Name = "ek-p-" + name, Text = text, Width = 110 };
            void Commit() { var v = t.Text?.Trim() ?? ""; if (v != text) CircuitEdit(label, x => set(x.Part(sel!)!, v)); }
            t.LostFocus += (_, _) => Commit(); OnEnter(t, Commit);
            sp.Children.Add(Row(P(label, Text, 12), t));
        }
        static double Parse(string v, string unit) => ElectronicsPlus.Si(v.Replace("H", "").Replace(unit, ""));

        if (sel is not null && c.Part(sel) is { } p)
        {
            var info = PartTypes.Get(p.Kind);
            sp.Children.Add(H(info.Label + (p.Name.Length > 0 ? " " + p.Name : ""), 13));
            switch (p.Kind)
            {
                case "widerstand": case "lampe": Val("value", "Widerstand (Ω)", ElectronicsPlus.Fmt(p.Value, "").Trim(), (x, v) => x.Value = Math.Max(1e-3, Parse(v, "Ω"))); if (p.Kind == "widerstand") Fact("Farbringe", Electronics.EncodeBands(p.Value)); break;
                case "kondensator": Val("value", "Kapazität (F)", ElectronicsPlus.Fmt(p.Value, "").Trim(), (x, v) => x.Value = Math.Max(1e-15, Parse(v, "F"))); break;
                case "spule": Val("value", "Induktivität (H)", ElectronicsPlus.Fmt(p.Value, "").Trim(), (x, v) => x.Value = Math.Max(1e-12, Parse(v, "H"))); break;
                case "spannung": Val("value", "Spannung (V)", ElectronicsPlus.Fmt(p.Value, "").Trim(), (x, v) => x.Value = Parse(v, "V")); break;
                case "wechsel":
                    Val("value", "Scheitelspannung (V)", ElectronicsPlus.Fmt(p.Value, "").Trim(), (x, v) => x.Value = Parse(v, "V"));
                    Val("freq", "Frequenz (Hz)", ElectronicsPlus.Fmt(p.Freq, "").Trim(), (x, v) => x.Freq = Math.Clamp(Parse(v, "Hz"), 0.01, 1e5)); Fact("Effektivwert", ElectronicsPlus.Fmt(p.Value / Math.Sqrt(2), "V")); break;
                case "schalter": sp.Children.Add(Act2("ek-toggle", p.Closed ? "Schalter öffnen" : "Schalter schließen", () => CircuitEdit("Schalter umlegen", x => { var q = x.Part(sel)!; q.Closed = !q.Closed; }))); break;
                case "led":
                    var colors = new[] { "rot", "gruen", "gelb", "blau", "weiss" };
                    var cb = new ComboBox { Name = "ek-p-color", Width = 110, ItemsSource = new[] { "Rot", "Grün", "Gelb", "Blau", "Weiß" }, SelectedIndex = Math.Max(0, Array.IndexOf(colors, p.Color)) };
                    cb.SelectionChanged += (_, _) => { var v = colors[Math.Max(0, cb.SelectedIndex)]; if (v != p.Color) CircuitEdit("LED-Farbe", x => x.Part(sel)!.Color = v); };
                    sp.Children.Add(Row(P("Farbe", Text, 12), cb)); break;
            }
            if (st is not null && st.Parts.TryGetValue(p.Id, out var r))
            {
                sp.Children.Add(H("Messung (live)", 12));
                _simLive = new TextBlock { Name = "ek-live", Foreground = Brush.Parse("#7FD3FF"), FontSize = 12.5, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap,
                    Text = $"U {ElectronicsPlus.Fmt(Math.Abs(r.V), "V")} · I {ElectronicsPlus.Fmt(Math.Abs(r.I), "A")} · P {ElectronicsPlus.Fmt(Math.Abs(r.V * r.I), "W")}" };
                sp.Children.Add(_simLive);
                sp.Children.Add(P("Unten auf der Fläche läuft das Oszilloskop: grün Spannung, gelb Strom.", null, 11));
            }
            sp.Children.Add(new WrapPanel { Children = {
                Act2("ek-rotate", "↻ Drehen (R)", () => CircuitEdit("Drehen", x => { var q = x.Part(sel)!; var (dx, dy) = (q.X2 - q.X1, q.Y2 - q.Y1); (q.X2, q.Y2) = (q.X1 - dy, q.Y1 + dx); })),
                Act2("ek-del", "Löschen", () => { CircuitState.Selected = null; CircuitEdit("Löschen", x => x.Remove(sel)); }) } });
        }
        else
        {
            sp.Children.Add(H("Schaltung", 13));
            Fact("Bauteile", c.Parts.Count(x => x.Kind is not ("draht" or "masse")).ToString());
            Fact("Simulation", (CircuitState.Running ? "läuft" : "angehalten") + (CircuitState.Speed < 1 ? $", {1 / CircuitState.Speed:0}× langsamer" : ", Echtzeit"));
            if (_sim?.Problem is { } pr) sp.Children.Add(new Border { Name = "ek-problem", Background = Brush.Parse("#33EF4444"), CornerRadius = new CornerRadius(8), Padding = new Thickness(8, 6), Child = new TextBlock { Text = "⚠ " + pr, Foreground = Text, FontSize = 12, TextWrapping = TextWrapping.Wrap } });
            if (st is not null)
            {
                var checks = Simulator.Check(c, st);
                sp.Children.Add(H("Prüfung", 12));
                if (checks.Count == 0) sp.Children.Add(new TextBlock { Name = "ek-check", Text = "✓ Keine Überlastung gefunden.", Foreground = Brush.Parse("#4ADE80"), FontSize = 12 });
                else foreach (var m in checks) sp.Children.Add(new TextBlock { Name = "ek-check", Text = "⚠ " + m, Foreground = Brush.Parse("#F87171"), FontSize = 12, TextWrapping = TextWrapping.Wrap });
            }
            var k = 0;
            foreach (var part in c.Parts.Where(x => x.Kind is not ("draht" or "masse")))
            {
                var id = part.Id; var b = new Button { Name = $"ek-part-{k++}", HorizontalAlignment = HorizontalAlignment.Stretch, Background = Brushes.Transparent, Padding = new Thickness(6, 3),
                    Content = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Children = { new TextBlock { Text = $"{part.Name} · {PartTypes.Get(part.Kind).Label}", Foreground = Text, FontSize = 12 }, Pos(P(PartTypes.Format(part), null, 11), 1) } } };
                b.Click += (_, _) => CircuitSelect(id); sp.Children.Add(b);
            }
            var calcs = new StackPanel { Spacing = 10 };
            LiveCalc(calcs, "el-code", "Widerstand: Farbringe", [("bands", "Farben oder Wert (Ω)", "gelb violett rot gold", null)],
                v => { var s = v("bands"); if (s.Any(char.IsDigit)) { var o = Fach.Num(s, "Wert"); return $"{Electronics.Ohm(o)} = {Electronics.EncodeBands(o)}"; } var (ohm, t) = Electronics.DecodeBands(s); return $"{Electronics.Ohm(ohm)} ± {Fach.F(t, 2)} %"; });
            LiveCalc(calcs, "el-led", "LED-Vorwiderstand", [("vs", "Versorgung (V)", "5", null), ("vf", "LED-Spannung (V)", "2", null), ("ma", "Strom (mA)", "20", null), ("n", "LEDs in Reihe", "1", null)],
                v => { var x = Electronics.LedResistor(Fach.Num(v("vs")), Fach.Num(v("vf")), Fach.Num(v("ma")), (int)Fach.Num(v("n"))); return $"Rechnerisch {Electronics.Ohm(x.Exact)} → nimm {Electronics.Ohm(x.E12)} ({Electronics.EncodeBands(x.E12)}), {Fach.F(x.Power * 1000, 0)} mW"; });
            LiveCalc(calcs, "el-div", "Spannungsteiler (E24)", [("vin", "Eingang (V)", "12", null), ("vout", "Wunsch (V)", "3,3", null), ("r2", "R2 (Ω)", "10000", null)],
                v => { var x = Electronics.Divider(Fach.Num(v("vin")), Fach.Num(v("vout")), Fach.Num(v("r2"))); return $"R1 = {Electronics.Ohm(x.R1)}, R2 = {Electronics.Ohm(x.R2)} → {Fach.F(x.Vout, 3)} V ({Fach.F(x.ErrorPct, 2)} %)"; });
            LiveCalc(calcs, "el-comb", "Reihe & parallel", [("vals", "Werte", "1k 1k 2k", null), ("mode", "Schaltung", "parallel", ["reihe", "parallel"]), ("kind", "Bauteil", "Widerstände", ["Widerstände", "Kondensatoren"])],
                v => { var vals = v("vals").Split([' ', ';'], StringSplitOptions.RemoveEmptyEntries).Select(ElectronicsPlus.Si); var cap = v("kind").StartsWith("K"); return "Gesamt: " + ElectronicsPlus.Fmt(ElectronicsPlus.Combine(vals, v("mode") == "parallel", cap), cap ? "F" : "Ω"); });
            LiveCalc(calcs, "el-cap", "Kondensator-Aufdruck", [("c", "Code oder Wert", "104", null)],
                v => { var t = v("c").Trim(); return t.Length == 3 && t.All(char.IsDigit) ? $"{t} = {ElectronicsPlus.Fmt(ElectronicsPlus.CapCode(t), "F")}" : $"{ElectronicsPlus.Fmt(ElectronicsPlus.Si(t), "F")} = Code {ElectronicsPlus.ToCapCode(ElectronicsPlus.Si(t))}"; });
            LiveCalc(calcs, "el-smd", "SMD-Widerstand", [("c", "Aufdruck", "472", null)], v => $"{v("c")} = {Electronics.Ohm(ElectronicsPlus.SmdCode(v("c")))}");
            LiveCalc(calcs, "el-555", "Blinker mit NE555", [("r1", "R1", "1k", null), ("r2", "R2", "10k", null), ("c", "C", "10u", null)],
                v => { var x = ElectronicsPlus.Timer555(ElectronicsPlus.Si(v("r1")), ElectronicsPlus.Si(v("r2")), ElectronicsPlus.Si(v("c"))); return $"{ElectronicsPlus.Fmt(x.Hz, "Hz")} · an {Fach.F(x.DutyPct, 1)} %"; });
            LiveCalc(calcs, "el-trace", "Leiterbahnbreite (IPC-2221)", [("a", "Strom (A)", "2", null), ("dt", "Erwärmung (°C)", "10", null), ("cu", "Kupfer (µm)", "35", null)],
                v => $"Mindestens {Fach.F(ElectronicsPlus.TraceWidthMm(Fach.Num(v("a")), Fach.Num(v("dt")), Fach.Num(v("cu"))), 2)} mm breit (Außenlage)");
            LiveCalc(calcs, "el-bat", "Wie lange hält der Akku?", [("mah", "Kapazität (mAh)", "2000", null), ("ma", "Verbrauch (mA)", "50", null)],
                v => { var h = ElectronicsPlus.BatteryHours(Fach.Num(v("mah")), Fach.Num(v("ma"))); return $"ca. {Fach.F(h, 1)} Stunden ({Fach.F(h / 24, 1)} Tage)"; });
            LiveCalc(calcs, "el-cable", "Kabel: Spannungsverlust", [("l", "Länge (m)", "5", null), ("a", "Strom (A)", "10", null), ("q", "Querschnitt (mm²)", "1,5", null), ("u", "Spannung (V)", "12", null)],
                v => { var x = ElectronicsPlus.CableDrop(Fach.Num(v("l")), Fach.Num(v("a")), Fach.Num(v("q")), Fach.Num(v("u"))); return $"{(x.DropPct > 3 ? "⚠" : "✓")} Verlust {Fach.F(x.DropV, 2)} V ({Fach.F(x.DropPct, 1)} %), {Fach.F(x.LossW, 1)} W"; });
            LiveCalc(calcs, "el-reg", "Spannungsregler (z. B. 7805)", [("vin", "Eingang (V)", "12", null), ("vout", "Ausgang (V)", "5", null), ("a", "Strom (A)", "0,5", null)],
                v => { var x = ElectronicsPlus.Regulator(Fach.Num(v("vin")), Fach.Num(v("vout")), Fach.Num(v("a"))); return x.NeedsSink ? $"⚠ {Fach.F(x.Watt, 2)} W – Kühlkörper ≤ {Fach.F(x.SinkKPerW, 1)} K/W nötig" : $"✓ {Fach.F(x.Watt, 2)} W, ca. {Fach.F(x.TjNoSink, 0)} °C – kein Kühlkörper nötig"; });
            LiveCalc(calcs, "el-si", "Einheiten-Präfixe", [("v", "Wert", "4k7", null)], v => $"{v("v")} = {Fach.F(ElectronicsPlus.Si(v("v")), 12).TrimEnd('0').TrimEnd(',')}");
            sp.Children.Add(new Expander { Name = "ek-calcs", Header = "Rechner (Farbcode, LED, Teiler …)", IsExpanded = false, Content = calcs });
        }
        return sp;
    }
}

static class VisualFind
{
    /// <summary>Erste sichtbare Zeichenfläche der Schaltung im Fenster (für den Simulationstakt).</summary>
    public static CircuitCanvas? VisibleCircuit(this Avalonia.Visual root) =>
        Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(root).OfType<CircuitCanvas>().LastOrDefault(c => c.IsEffectivelyVisible && c.Bounds.Width > 0);
}
