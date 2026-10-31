using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Trendo.Core;
using Trendo.Core.Programs;
using Trendo.Core.Statik;

namespace Trendo.App;

/// <summary>SPEC-03 / T-0258: Technik & Simulation als echtes Programm „Tragwerk & Mechanik“ – Stabwerk zeichnen (Balken, Rahmen, Fachwerk),
/// Lager, Gelenke und Lasten setzen; Schnittkräfte, Biegelinie, Reaktionen und Nachweise rechnen sofort mit (Weggrößenverfahren).
/// Die früheren Einzelrechner (Wärmedehnung, Schrauben, Federn …) rechnen live in der rechten Spalte. Ersetzt die Formularseite.</summary>
public partial class MainWindow
{
    const string TK = "technik";
    public readonly FrameViewState FrameState = new();
    private readonly Dictionary<string, string> _calcIn = [];

    static readonly (string Id, string Label, string Glyph, string Tip)[] FrameTools =
    [
        ("auswahl", "Auswahl", "M4,2 L4,20 L9,15 L12,22 L15,21 L12,14 L19,14 Z", "Auswählen, Knoten ziehen (Esc)"),
        ("stab", "Stab", "M3,19 L21,5 M3,19 m-2,0 a2,2 0 1,0 4,0 a2,2 0 1,0 -4,0 M21,5 m-2,0 a2,2 0 1,0 4,0 a2,2 0 1,0 -4,0", "Stäbe zeichnen – Punkt für Punkt, Länge in m tippen + Enter"),
        ("lager", "Lager", "M12,4 L4,16 L20,16 Z M2,20 L22,20", "Lager auf Knoten setzen"),
        ("last", "Last", "M12,2 L12,18 M7,13 L12,19 L17,13", "Lasten auf Stäbe oder Knoten setzen"),
        ("gelenk", "Gelenk", "M3,12 L9,12 M15,12 L21,12 M12,12 m-3,0 a3,3 0 1,0 6,0 a3,3 0 1,0 -6,0", "Gelenk an einem Stabende"),
    ];

    private void InitSpec03() => RegisterProgram(FachTabs, new ProgramSpec(TK, "SPEC-03", "Tragwerk & Mechanik")
    {
        Sentence = "Träger, Rahmen und Fachwerke zeichnen – Schnittkräfte, Durchbiegung und Nachweis rechnen sofort mit.",
        Folder = "Statik",
        FillHeight = true,
        Templates =
        [
            new("leer", "Leeres Tragwerk", "Selbst Stäbe zeichnen, Lager und Lasten setzen", () => FrameDoc(new FrameModel())),
            new("einfeld", "Einfeldträger", "5 m, IPE 200, 10 kN/m Gleichlast", () => FrameDoc(FrameModel.Template("einfeld"))),
            new("kragarm", "Kragarm (Holz)", "2 m, Balken 10/20, 3 kN am Ende", () => FrameDoc(FrameModel.Template("kragarm"))),
            new("durchlauf", "Durchlaufträger", "2 Felder à 4 m, 8 kN/m", () => FrameDoc(FrameModel.Template("durchlauf"))),
            new("rahmen", "Rahmen", "Zweigelenkrahmen 6 × 4 m, Dach- und Windlast", () => FrameDoc(FrameModel.Template("rahmen"))),
            new("fachwerk", "Fachwerkbinder", "8 m, Rohre, Knotenlasten", () => FrameDoc(FrameModel.Template("fachwerk"))),
        ],
        Ribbon =
        [
            ("Ergebnisse", [
                new ProgramCmd("tk-m", "Momente M", _ => FrameState.Show = FrameState.Show with { Moment = !FrameState.Show.Moment }, false, "Momentenlinie (Zugseite) an/aus"),
                new ProgramCmd("tk-v", "Querkraft V", _ => FrameState.Show = FrameState.Show with { Shear = !FrameState.Show.Shear }),
                new ProgramCmd("tk-n", "Normalkraft N", _ => FrameState.Show = FrameState.Show with { Normal = !FrameState.Show.Normal }),
                new ProgramCmd("tk-def", "Biegelinie", _ => FrameState.Show = FrameState.Show with { Deformed = !FrameState.Show.Deformed }, false, "Verformung (überhöht dargestellt)"),
                new ProgramCmd("tk-react", "Reaktionen", _ => FrameState.Show = FrameState.Show with { Reactions = !FrameState.Show.Reactions }),
            ]),
            ("Ansicht", [
                new ProgramCmd("tk-fit", "Einpassen", _ => { FrameState.Fitted = false; FrameState.UserView = false; }),
                new ProgramCmd("tk-grid", "Raster", _ => FrameState.Grid = !FrameState.Grid, false, "10-cm-Raster und Fang an/aus"),
                new ProgramCmd("tk-self", "Eigengewicht", d => d.Edit("Eigengewicht", x => { var f = FrameModel.From(x); f.SelfWeight = !f.SelfWeight; f.WriteTo(x); }), false, "Gewicht der Stäbe mitrechnen"),
            ]),
        ],
        Exports =
        [
            new("pdf", "PDF-Bericht", ".pdf", (d, f) => { var m = FrameModel.From(d.Data); return FrameDrawing.Pdf(m, FrameSolver.Solve(m), f); }),
            new("svg", "SVG (Bild)", ".svg", (d, f) => { var m = FrameModel.From(d.Data); File.WriteAllText(f, FrameDrawing.Svg(m, FrameSolver.Solve(m), FrameState.Show)); return f; }),
            new("csv", "Ergebnisse (CSV)", ".csv", (d, f) => { var m = FrameModel.From(d.Data); File.WriteAllText(f, FrameDrawing.Csv(m, FrameSolver.Solve(m)), new System.Text.UTF8Encoding(true)); return f; }),
        ],
        Workspace = FrameWorkspace,
        Properties = FrameProperties,
        Status = d => { var m = FrameModel.From(d.Data); var (r, err) = FrameSolve(m); return $"{FrameTools.FirstOrDefault(t => t.Id == FrameState.Tool).Label} · {m.Members.Count} Stäbe · {m.Nodes.Count(n => n.Support != "")} Lager · {m.Loads.Count} Lasten · " + (err is not null ? "⚠ " + err : r is null ? "" : r.Ok ? "✓ alle Stäbe halten" : $"⚠ {r.Members.Count(x => !x.Ok)} Stab/Stäbe reichen nicht"); },
        Help = "Links das Werkzeug wählen. Stab: Punkt für Punkt klicken, Länge in m tippen und Enter. Lager/Last/Gelenk: auf Knoten oder Stab klicken – Art und Wert rechts einstellen. "
             + "Auswahl: Knoten ziehen, Entf löscht. Ergebnisse rechnen sofort mit. Vorbemessung – ersetzt keine geprüfte Statik.",
    }, "technik", "statik", "balken", "träger", "simulation", "biegung", "festigkeit", "einheiten", "umrechnen", "rahmen", "fachwerk", "tragwerk");

    static System.Text.Json.Nodes.JsonObject FrameDoc(FrameModel m) { var o = new System.Text.Json.Nodes.JsonObject(); m.WriteTo(o); return o; }

    static (FrameResult? R, string? Error) FrameSolve(FrameModel m)
    {
        if (m.Members.Count == 0) return (null, null);
        try { return (FrameSolver.Solve(m), null); }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException) { return (null, ex.Message); }
    }

    public void FrameEdit(string label, Action<FrameModel> change)
    {
        var d = ProgramActive(TK) ?? throw new InvalidOperationException("Es ist kein Tragwerk offen.");
        FrameState.Hint = "";
        ProgramRun(TK, () => d.Edit(label, x => { var m = FrameModel.From(x); change(m); m.WriteTo(x); }));
    }

    public void FrameTool(string tool) { FrameState.Tool = tool; FrameState.Chain = null; FrameState.Hint = ""; if (tool != "auswahl") FrameState.Selected = null; Show(TK); }
    public void FrameSelect(string? id) { FrameState.Selected = id; Show(TK); }

    private Control FrameWorkspace(ProgramDocument d)
    {
        var m = FrameModel.From(d.Data); var (r, err) = FrameSolve(m);
        var tools = new WrapPanel { Name = "tk-tools", Width = 112 };
        foreach (var (id, label, glyph, tip) in FrameTools)
        {
            var on = FrameState.Tool == id; var tid = id;
            var b = new Button { Name = "tk-tool-" + id, Width = 53, Height = 46, Padding = new Thickness(1), Margin = new Thickness(0, 0, 3, 3), CornerRadius = new CornerRadius(10), Focusable = false,
                Background = on ? Accent : Brush.Parse(C("#1AFFFFFF", "#0D000000")), BorderBrush = on ? Brush.Parse("#F5A524") : Edge, BorderThickness = new Thickness(on ? 1.5 : 1),
                Content = new StackPanel { Spacing = 2, HorizontalAlignment = HorizontalAlignment.Center, Children = {
                    new Avalonia.Controls.Shapes.Path { Data = Geometry.Parse(glyph), Stroke = on ? Brushes.White : Text, StrokeThickness = 1.6, Fill = Brushes.Transparent, Stretch = Stretch.Uniform, Width = 18, Height = 18, HorizontalAlignment = HorizontalAlignment.Center },
                    new TextBlock { Text = label, FontSize = 10, Foreground = on ? Brushes.White : Text, HorizontalAlignment = HorizontalAlignment.Center } } } };
            ToolTip.SetTip(b, tip); b.Click += (_, _) => FrameTool(tid); tools.Children.Add(b);
        }
        // Werkzeug-Optionen gleich unter den Werkzeugen
        var opts = new StackPanel { Spacing = 4, Width = 110, Margin = new Thickness(0, 6, 0, 0) };
        if (FrameState.Tool == "stab")
        {
            opts.Children.Add(new TextBlock { Text = "Querschnitt", FontSize = 10, Foreground = Muted });
            var sec = new AutoCompleteBox { Name = "tk-opt-section", Text = FrameState.Section, ItemsSource = Sections.Suggestions, FilterMode = AutoCompleteFilterMode.ContainsOrdinal, MinimumPrefixLength = 0, FontSize = 11 };
            sec.LostFocus += (_, _) => { try { Sections.Get(sec.Text ?? ""); FrameState.Section = sec.Text!.Trim(); } catch (ArgumentException ex) { _progMsg[TK] = ex.Message; Show(TK); } };
            opts.Children.Add(sec);
            opts.Children.Add(new TextBlock { Text = "Werkstoff", FontSize = 10, Foreground = Muted });
            var mat = new ComboBox { Name = "tk-opt-material", FontSize = 11, Width = 109, ItemsSource = Engineering.Materials.Select(x => x.Name).ToList(), SelectedItem = FrameState.Material };
            mat.SelectionChanged += (_, _) => { if (mat.SelectedItem is string s) FrameState.Material = s; };
            opts.Children.Add(mat);
        }
        else if (FrameState.Tool == "lager")
        {
            foreach (var (t, l) in new[] { ("fest", "Festlager"), ("los", "Loslager"), ("einspannung", "Einspannung") })
            {
                var tt = t; var on = FrameState.SupportType == t;
                var b = new Button { Name = "tk-opt-support-" + t, Content = l, FontSize = 11, Width = 109, Padding = new Thickness(6, 3), Background = on ? Accent : Brushes.Transparent, Foreground = on ? Brushes.White : Text, BorderBrush = Edge, BorderThickness = new Thickness(1), Focusable = false };
                b.Click += (_, _) => { FrameState.SupportType = tt; Show(TK); }; opts.Children.Add(b);
            }
        }
        else if (FrameState.Tool == "last")
        {
            foreach (var (t, l) in new[] { ("strecke", "Streckenlast"), ("einzel", "Einzellast") })
            {
                var tt = t; var on = FrameState.LoadKind == t;
                var b = new Button { Name = "tk-opt-load-" + t, Content = l, FontSize = 11, Width = 109, Padding = new Thickness(6, 3), Background = on ? Accent : Brushes.Transparent, Foreground = on ? Brushes.White : Text, BorderBrush = Edge, BorderThickness = new Thickness(1), Focusable = false };
                b.Click += (_, _) => { FrameState.LoadKind = tt; Show(TK); }; opts.Children.Add(b);
            }
            opts.Children.Add(new TextBlock { Text = FrameState.LoadKind == "strecke" ? "Wert (kN/m)" : "Wert (kN)", FontSize = 10, Foreground = Muted });
            var val = new TextBox { Name = "tk-opt-value", Text = FrameState.LoadValue.ToString("0.##", De), FontSize = 11 };
            val.TextChanged += (_, _) => { if (double.TryParse(val.Text, System.Globalization.NumberStyles.Float, De, out var v)) FrameState.LoadValue = v; };
            opts.Children.Add(val);
        }
        tools.Children.Add(opts);
        var canvas = new FrameCanvas(m, r, err, FrameState, FrameEdit, FrameSelect) { Name = "tk-canvas", MinHeight = 160 };
        var frame = new Border { CornerRadius = new CornerRadius(12), ClipToBounds = true, BorderBrush = EdgeLit, BorderThickness = new Thickness(1), Child = canvas };
        return new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,10,*"), Children = { new ScrollViewer { Content = tools }, Pos(frame, 2) } };
    }

    private Control FrameProperties(ProgramDocument d)
    {
        var m = FrameModel.From(d.Data); var (r, err) = FrameSolve(m); var sp = new StackPanel { Spacing = 5 }; var sel = FrameState.Selected;
        void Fact(string label, string value, IBrush? color = null) => sp.Children.Add(Row(P(label, null, 12), new TextBlock { Text = value, Foreground = color ?? Text, FontSize = 12, FontWeight = FontWeight.SemiBold }));
        Button Act2(string name, string label, Action go) { var b = new Button { Name = name, Content = label, Padding = new Thickness(10, 4), Margin = new Thickness(0, 4, 4, 0) }; b.Click += (_, _) => go(); return b; }
        void Num(string name, string label, double value, Action<FrameModel, double> set, string unit)
        {
            var t = new TextBox { Name = "tk-p-" + name, Text = value.ToString("0.###", De), Width = 90 };
            void Commit() { if (double.TryParse(t.Text, System.Globalization.NumberStyles.Float, De, out var v) && Math.Abs(v - value) > 1e-9) FrameEdit(label, f => set(f, v)); }
            t.LostFocus += (_, _) => Commit(); OnEnter(t, Commit);
            sp.Children.Add(Row(P($"{label} ({unit})", Text, 12), t));
        }
        if (err is not null) sp.Children.Add(new Border { Name = "tk-error", Background = Brush.Parse("#33EF4444"), CornerRadius = new CornerRadius(8), Padding = new Thickness(8, 6), Child = new TextBlock { Text = "⚠ " + err, Foreground = Text, FontSize = 12, TextWrapping = TextWrapping.Wrap } });

        if (sel is not null && m.Node(sel) is { } n)
        {
            sp.Children.Add(H($"Knoten K{m.Nodes.IndexOf(n) + 1}", 13));
            Num("x", "x", n.X, (f, v) => f.Node(sel)!.X = v, "m"); Num("y", "y", n.Y, (f, v) => f.Node(sel)!.Y = v, "m");
            var sup = new ComboBox { Name = "tk-p-support", Width = 150, ItemsSource = new[] { "frei", "Festlager", "Loslager", "Einspannung" }, SelectedIndex = Array.IndexOf(new[] { "", "fest", "los", "einspannung" }, n.Support) };
            sup.SelectionChanged += (_, _) => { var v = new[] { "", "fest", "los", "einspannung" }[Math.Max(0, sup.SelectedIndex)]; if (v != n.Support) FrameEdit("Lager", f => f.Node(sel)!.Support = v); };
            sp.Children.Add(Row(P("Lager", Text, 12), sup));
            if (r is not null && r.Reactions.TryGetValue(n.Id, out var re)) { sp.Children.Add(H("Lagerkräfte", 12)); Fact("waagrecht Rx", $"{re.Rx:0.##} kN"); Fact("senkrecht Ry", $"{re.Ry:0.##} kN"); if (n.Support == "einspannung") Fact("Moment", $"{re.M:0.##} kNm"); }
            if (r is not null && r.Displacements.TryGetValue(n.Id, out var u)) Fact("Verschiebung", $"{Math.Sqrt(u.Ux * u.Ux + u.Uy * u.Uy) * 1000:0.##} mm");
            sp.Children.Add(new WrapPanel { Children = {
                Act2("tk-nodeload", "＋ Last hier", () => FrameEdit("Knotenlast", f => FrameState.Selected = f.AddNodeLoad(sel, FrameState.LoadValue).Id)),
                Act2("tk-del", "Knoten löschen", () => { FrameState.Selected = null; FrameEdit("Knoten löschen", f => f.Remove(sel)); }) } });
        }
        else if (sel is not null && m.Member(sel) is { } mem)
        {
            var i = m.Members.IndexOf(mem) + 1; var a = m.Node(mem.A)!; var b = m.Node(mem.B)!;
            sp.Children.Add(H($"Stab S{i} · {Math.Sqrt((b.X - a.X) * (b.X - a.X) + (b.Y - a.Y) * (b.Y - a.Y)):0.00} m", 13));
            var sec = new AutoCompleteBox { Name = "tk-p-section", Text = mem.Section, ItemsSource = Sections.Suggestions, FilterMode = AutoCompleteFilterMode.ContainsOrdinal, MinimumPrefixLength = 0, Width = 150 };
            void SetSec() { var v = sec.Text?.Trim() ?? ""; if (v != mem.Section) FrameEdit("Querschnitt", f => { Sections.Get(v); f.Member(sel)!.Section = v; }); }
            sec.LostFocus += (_, _) => SetSec(); sec.KeyDown += (_, e) => { if (e.Key == Avalonia.Input.Key.Enter) SetSec(); };
            sp.Children.Add(Row(P("Querschnitt", Text, 12), sec));
            var mat = new ComboBox { Name = "tk-p-material", Width = 150, ItemsSource = Engineering.Materials.Select(x => x.Name).ToList(), SelectedItem = Engineering.Mat(mem.Material).Name };
            mat.SelectionChanged += (_, _) => { if (mat.SelectedItem is string s && s != mem.Material) FrameEdit("Werkstoff", f => f.Member(sel)!.Material = s); };
            sp.Children.Add(Row(P("Werkstoff", Text, 12), mat));
            var ha = new CheckBox { Name = "tk-p-hingea", Content = "Gelenk am Anfang", IsChecked = mem.HingeA }; ha.IsCheckedChanged += (_, _) => { if ((ha.IsChecked == true) != mem.HingeA) FrameEdit("Gelenk", f => f.Member(sel)!.HingeA = ha.IsChecked == true); };
            var hb = new CheckBox { Name = "tk-p-hingeb", Content = "Gelenk am Ende", IsChecked = mem.HingeB }; hb.IsCheckedChanged += (_, _) => { if ((hb.IsChecked == true) != mem.HingeB) FrameEdit("Gelenk", f => f.Member(sel)!.HingeB = hb.IsChecked == true); };
            sp.Children.Add(ha); sp.Children.Add(hb);
            var si = Sections.Get(mem.Section);
            Fact("Fläche / W", $"{si.A_mm2 / 100:0.##} cm² · {si.W_mm3 / 1000:0.#} cm³");
            if (r?.Of(mem.Id) is { } x)
            {
                sp.Children.Add(H("Ergebnis", 12));
                sp.Children.Add(new TextBlock { Name = "tk-verdict", Text = x.Verdict, Foreground = Brush.Parse(x.Ok ? "#4ADE80" : "#F87171"), FontSize = 12.5, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap });
                Fact("max. Moment", $"{x.MaxM:0.##} kNm"); Fact("max. Querkraft", $"{x.MaxV:0.##} kN"); Fact("Normalkraft", $"{x.MaxN:0.##} kN" + (x.MaxN < 0 ? " (Druck)" : x.MaxN > 0 ? " (Zug)" : ""));
                Fact("Spannung", $"{x.Stress_MPa:0.#} N/mm²"); Fact("Sicherheit", $"{x.Safety:0.##}", Brush.Parse(x.Safety >= 1.5 ? "#4ADE80" : "#F87171"));
                Fact("Durchbiegung", $"{x.MaxDefl_mm:0.#} mm (L/300 = {x.L * 1000 / 300:0.#} mm)", Brush.Parse(x.MaxDefl_mm <= x.L * 1000 / 300 ? "#4ADE80" : "#F87171"));
                if (x.BuckleSafety is { } bk) Fact("Knicksicherheit", $"{bk:0.##}", Brush.Parse(bk >= 2 ? "#4ADE80" : "#F87171"));
                // Vorschlag: kleinstes IPE, das hält
                if (!x.Ok && mem.Section.StartsWith("IPE", StringComparison.OrdinalIgnoreCase))
                {
                    var fit = Sections.SteelNames.Where(s => s.StartsWith("IPE")).FirstOrDefault(s => { var t = m.Clone(); t.Member(sel)!.Section = s; try { return FrameSolver.Solve(t).Of(sel)!.Ok; } catch (InvalidOperationException) { return false; } });
                    if (fit is not null) sp.Children.Add(Act2("tk-suggest", $"Vorschlag: {fit} nehmen", () => FrameEdit("Querschnitt", f => f.Member(sel)!.Section = fit)));
                }
            }
            sp.Children.Add(new WrapPanel { Children = {
                Act2("tk-lineload", "＋ Streckenlast", () => FrameEdit("Streckenlast", f => FrameState.Selected = f.AddLineLoad(sel, FrameState.LoadValue).Id)),
                Act2("tk-split", "Stab teilen", () => FrameEdit("Stab teilen", f => FrameState.Selected = f.SplitMember(sel).Id)),
                Act2("tk-del", "Stab löschen", () => { FrameState.Selected = null; FrameEdit("Stab löschen", f => f.Remove(sel)); }) } });
        }
        else if (sel is not null && m.Loads.FirstOrDefault(l => l.Id == sel) is { } ld)
        {
            sp.Children.Add(H(ld.Kind switch { "strecke" => "Streckenlast", "einzel" => "Einzellast am Stab", _ => "Knotenlast" }, 13));
            Num("value", ld.Kind == "strecke" ? "Last nach unten" : "Kraft nach unten", ld.Value, (f, v) => f.Loads.First(x => x.Id == sel).Value = v, ld.Kind == "strecke" ? "kN/m" : "kN");
            if (ld.Kind == "einzel") Num("at", "Lage", ld.At * 100, (f, v) => f.Loads.First(x => x.Id == sel).At = Math.Clamp(v / 100, 0, 1), "% der Länge");
            if (ld.Kind == "knoten") { Num("fx", "Kraft nach rechts", ld.Fx, (f, v) => f.Loads.First(x => x.Id == sel).Fx = v, "kN"); Num("m", "Moment", ld.M, (f, v) => f.Loads.First(x => x.Id == sel).M = v, "kNm"); }
            sp.Children.Add(Act2("tk-del", "Last löschen", () => { FrameState.Selected = null; FrameEdit("Last löschen", f => f.Remove(sel)); }));
        }
        else
        {
            sp.Children.Add(H("Tragwerk", 13));
            Fact("Stäbe / Lager / Lasten", $"{m.Members.Count} / {m.Nodes.Count(n => n.Support != "")} / {m.Loads.Count}");
            Fact("Eigengewicht", m.SelfWeight ? "mitgerechnet" : "nicht mitgerechnet");
            if (r is not null)
            {
                sp.Children.Add(new TextBlock { Name = "tk-verdict", Text = r.Ok ? "✓ Alle Stäbe halten." : $"⚠ {r.Members.Count(x => !x.Ok)} Stab/Stäbe reichen nicht – rot markiert.", Foreground = Brush.Parse(r.Ok ? "#4ADE80" : "#F87171"), FontSize = 12.5, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap });
                var k = 0;
                foreach (var x in r.Members)
                {
                    var id = x.Id; var idx = ++k;
                    var b = new Button { Name = $"tk-member-{idx - 1}", HorizontalAlignment = HorizontalAlignment.Stretch, Background = Brushes.Transparent, Padding = new Thickness(6, 3),
                        Content = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Children = { new TextBlock { Text = $"S{idx} · {m.Member(id)!.Section}", Foreground = Text, FontSize = 12 }, Pos(new TextBlock { Text = $"M {x.MaxM:0.#} · η {(x.Safety > 0 ? 1.5 / x.Safety : 0):0.00}", Foreground = Brush.Parse(x.Ok ? "#4ADE80" : "#F87171"), FontSize = 11 }, 1) } } };
                    b.Click += (_, _) => FrameSelect(id); sp.Children.Add(b);
                }
                sp.Children.Add(H("Lagerkräfte", 12));
                foreach (var (nid, (rx, ry, rm)) in r.Reactions) { var nn = m.Node(nid)!; Fact($"K{m.Nodes.IndexOf(nn) + 1} {FrameDrawing.SupportName(nn.Support)}", $"{ry:0.##} kN ↑{(Math.Abs(rx) > 1e-6 ? $" · {rx:0.##} kN →" : "")}{(Math.Abs(rm) > 1e-6 ? $" · {rm:0.##} kNm" : "")}"); }
            }
            sp.Children.Add(P("Vorbemessung nach einfachen Regeln (Sicherheit 1,5 gegen Fließen, L/300, Knicken nach Euler) – ersetzt keine geprüfte Statik.", null, 11));
            sp.Children.Add(new Expander { Name = "tk-calcs", Header = "Rechner (Wärme, Schrauben, Federn …)", IsExpanded = false, Content = FrameCalculators() });
        }
        return sp;
    }

    /// <summary>Die früheren Einzelrechner – rechnen bei jeder Eingabe sofort (kein Formular mit „Ausführen“).</summary>
    private Control FrameCalculators()
    {
        var sp = new StackPanel { Spacing = 10 };
        var mats = Engineering.Materials.Select(x => x.Name).ToArray();
        void Calc(string id, string title, (string Key, string Label, string Def, string[]? Options)[] fields, Func<Func<string, string>, string> run)
        {
            var box = new StackPanel { Spacing = 3 };
            box.Children.Add(new TextBlock { Text = title, Foreground = Text, FontSize = 12.5, FontWeight = FontWeight.SemiBold });
            var output = new SelectableTextBlock { Name = id + "-out", Foreground = Brush.Parse("#7FD3FF"), FontSize = 12, TextWrapping = TextWrapping.Wrap };
            string V(string k) => _calcIn.TryGetValue(id + "-" + k, out var v) ? v : fields.First(f => f.Key == k).Def;
            void Update() { try { output.Text = run(V); } catch (Exception ex) when (ex is ArgumentException or FormatException or InvalidOperationException or KeyNotFoundException or DivideByZeroException) { output.Text = "⚠ " + ex.Message; } }
            foreach (var (key, label, def, options) in fields)
            {
                var k = id + "-" + key; Control input;
                if (options is not null) { var c = new ComboBox { Name = k, ItemsSource = options, SelectedItem = options.FirstOrDefault(o => o == V(key)) ?? options.FirstOrDefault(o => o.Contains(V(key), StringComparison.OrdinalIgnoreCase)), Width = 150 }; c.SelectionChanged += (_, _) => { if (c.SelectedItem is string s) { _calcIn[k] = s; Update(); } }; input = c; }
                else { var t = new TextBox { Name = k, Text = V(key), Width = 150 }; t.TextChanged += (_, _) => { _calcIn[k] = t.Text ?? ""; Update(); }; input = t; }
                box.Children.Add(Row(P(label, null, 11), input));
            }
            box.Children.Add(output); Update();
            sp.Children.Add(new Border { Name = id, Background = TileBg, CornerRadius = new CornerRadius(10), Padding = new Thickness(8), BorderBrush = Edge, BorderThickness = new Thickness(1), Child = box });
        }
        Calc("eng-heat", "Wärmedehnung", [("mat", "Werkstoff", "Aluminium 6060", mats), ("len", "Länge (m)", "3", null), ("dt", "Erwärmung (°C)", "40", null)],
            v => { var m = Engineering.Mat(v("mat")); return $"{Fach.F(Engineering.ThermalExpansion(m, Fach.Num(v("len")), Fach.Num(v("dt"))), 2)} mm länger"; });
        Calc("eng-unit", "Einheiten umrechnen", [("v", "Wert", "100", null), ("from", "von", "psi", MainWindow.FachChoices["eng-unit-from"]), ("to", "nach", "bar", MainWindow.FachChoices["eng-unit-to"])],
            v => $"{v("v")} {v("from")} = {Fach.F(Engineering.Convert(Fach.Num(v("v")), v("from"), v("to")), 4)} {v("to")}");
        Calc("eng-bolt", "Schraube anziehen", [("size", "Größe", "M8", MainWindow.FachChoices["eng-bolt-size"]), ("grade", "Klasse", "8.8", MainWindow.FachChoices["eng-bolt-grade"])],
            v => { var b = EngineeringPlus.Bolt(v("size"), v("grade")); return $"Anzugsmoment {Fach.F(b.Nm, 1)} Nm · Vorspannkraft ca. {Fach.F(b.KN, 1)} kN"; });
        Calc("eng-spring", "Druckfeder (mm, N)", [("d", "Draht-Ø", "2", null), ("dm", "mittlerer Ø", "20", null), ("n", "Windungen", "8", null), ("f", "Kraft (N)", "50", null)],
            v => { var r = EngineeringPlus.Spring(Fach.Num(v("d")), Fach.Num(v("dm")), Fach.Num(v("n")), Fach.Num(v("f"))); return $"Federrate {Fach.F(r.RateNmm, 2)} N/mm → Weg {Fach.F(r.TravelMm, 1)} mm"; });
        Calc("eng-gear", "Zahnräder", [("z1", "Zähne Antrieb", "12", null), ("z2", "Zähne Abtrieb", "36", null), ("n", "Drehzahl (U/min)", "1500", null), ("m", "Drehmoment (Nm)", "2", null)],
            v => { var g = EngineeringPlus.Gears((int)Fach.Num(v("z1")), (int)Fach.Num(v("z2")), Fach.Num(v("n")), Fach.Num(v("m"))); return $"Übersetzung {Fach.F(g.Ratio, 2)} : 1 → {Fach.F(g.Rpm2, 0)} U/min, {Fach.F(g.Torque2, 2)} Nm"; });
        Calc("eng-torsion", "Welle verdrehen (Stahl)", [("t", "Drehmoment (Nm)", "50", null), ("d", "Ø (mm)", "20", null), ("len", "Länge (m)", "0,5", null)],
            v => { var r = EngineeringPlus.Torsion(Fach.Num(v("t")), Fach.Num(v("d")), Fach.Num(v("len"))); return $"Schubspannung {Fach.F(r.Tau, 1)} N/mm² · Verdrehung {Fach.F(r.PhiDeg, 2)}°"; });
        Calc("eng-u", "Wärmeverlust einer Wand", [("layers", "Schichten (m×λ | …)", "0,015x0,7 | 0,24x0,5 | 0,12x0,035", null), ("a", "Fläche (m²)", "10", null), ("dt", "Temperaturunterschied (K)", "30", null)],
            v => { var l = v("layers").Split('|').Select(x => x.ToLowerInvariant().Replace('×', 'x').Split('x')).Select(x => x.Length == 2 ? (Fach.Num(x[0], "Dicke"), Fach.Num(x[1], "λ")) : throw new FormatException("Schicht bitte als „0,24x0,5“.")); var r = EngineeringPlus.UValue(l, Fach.Num(v("a")), Fach.Num(v("dt"))); return $"U-Wert {Fach.F(r.U, 3)} W/(m²K) → {Fach.F(r.Watt, 0)} W{(r.U <= 0.24 ? " ✓ Neubau-Niveau" : "")}"; });
        Calc("eng-mass", "Wie schwer? (Quader, mm)", [("x", "Länge", "1000", null), ("y", "Breite", "100", null), ("z", "Dicke", "10", null), ("mat", "Werkstoff", "Aluminium 6060", mats)],
            v => $"{Fach.F(EngineeringPlus.MassKg(Engineering.Mat(v("mat")), Fach.Num(v("x")) * Fach.Num(v("y")) * Fach.Num(v("z"))), 2)} kg");
        return sp;
    }
}
