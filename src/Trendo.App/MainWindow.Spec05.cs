using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Trendo.Core;
using Trendo.Core.Messdaten;
using Trendo.Core.Programs;

namespace Trendo.App;

/// <summary>SPEC-05 / T-0261: Wissenschaft & Forschung als echtes Programm „Messdaten & Auswertung“ – Datentabelle zum Eintippen,
/// Diagramm (Streu, Linie, Balken, Histogramm, Boxplot) mit Ausgleichskurve und R², Statistik und Tests zur gewählten Spalte.
/// Die früheren Rechner (Chi², Stichprobengröße, Messunsicherheit) rechnen live. Ersetzt die Formularseite.</summary>
public partial class MainWindow
{
    const string SK = "wissenschaft";
    private int _msCol, _msRow;

    private void InitSpec05() => RegisterProgram(FachTabs, new ProgramSpec(SK, "SPEC-05", "Messdaten & Auswertung")
    {
        GroupLevels = new() { ["Ausgleich"] = 1 }, CmdLevels = new() { ["ms-k-histogramm"] = 1, ["ms-k-box"] = 1, ["ms-f-exp"] = 1, ["ms-f-potenz"] = 1 },
        Sentence = "Messwerte eintippen oder laden – Diagramm, Ausgleichskurve, Kennzahlen und Tests sofort.",
        Folder = "Messdaten",
        FillHeight = true,
        Templates =
        [
            new("leer", "Leere Messreihe", "Zwei Spalten x und y zum Eintippen", () => SheetDoc(MeasureSheet.Template("leer"))),
            new("pendel", "Fadenpendel", "Länge und Schwingungsdauer – Potenzgesetz T ~ √L", () => SheetDoc(MeasureSheet.Template("pendel"))),
            new("abkuehlen", "Abkühlung", "Tee kühlt ab – exponentieller Verlauf", () => SheetDoc(MeasureSheet.Template("abkuehlen"))),
            new("duenger", "Zwei Gruppen vergleichen", "Pflanzenhöhe mit Dünger A und B – Boxplot und t-Test", () => SheetDoc(MeasureSheet.Template("duenger"))),
        ],
        Ribbon =
        [
            ("Daten", [
                new ProgramCmd("ms-addcol", "Spalte +", d => SheetEdit("Spalte hinzufügen", s => { s.AddColumn("Spalte"); _msCol = s.Columns.Count - 1; })),
                new ProgramCmd("ms-addrow", "Zeile +", d => SheetEdit("Zeile hinzufügen", s => { foreach (var c in s.Columns) c.Values.Insert(Math.Min(_msRow + 1, c.Values.Count), null); })),
            ]),
            ("Diagramm", [
                new ProgramCmd("ms-k-streu", "Punkte", _ => SheetEdit("Diagramm", s => s.Chart.Kind = "streu")),
                new ProgramCmd("ms-k-linie", "Linie", _ => SheetEdit("Diagramm", s => s.Chart.Kind = "linie")),
                new ProgramCmd("ms-k-balken", "Balken", _ => SheetEdit("Diagramm", s => s.Chart.Kind = "balken")),
                new ProgramCmd("ms-k-histogramm", "Histogramm", _ => SheetEdit("Diagramm", s => s.Chart.Kind = "histogramm")),
                new ProgramCmd("ms-k-box", "Boxplot", _ => SheetEdit("Diagramm", s => s.Chart.Kind = "box")),
            ]),
            ("Ausgleich", [
                new ProgramCmd("ms-f-keine", "keine", _ => SheetEdit("Ausgleich", s => s.Chart.Fit = "keine")),
                new ProgramCmd("ms-f-linear", "Gerade", _ => SheetEdit("Ausgleich", s => s.Chart.Fit = "linear")),
                new ProgramCmd("ms-f-quadratisch", "Parabel", _ => SheetEdit("Ausgleich", s => s.Chart.Fit = "quadratisch")),
                new ProgramCmd("ms-f-exp", "Exponential", _ => SheetEdit("Ausgleich", s => s.Chart.Fit = "exp")),
                new ProgramCmd("ms-f-potenz", "Potenz", _ => SheetEdit("Ausgleich", s => s.Chart.Fit = "potenz")),
            ]),
        ],
        Exports =
        [
            new("csv", "CSV (Excel)", ".csv", (d, f) => { File.WriteAllText(f, MeasureSheet.From(d.Data).ToCsv(), new System.Text.UTF8Encoding(true)); return f; }),
            new("report", "Auswertung", ".md", (d, f) => { File.WriteAllText(f, MeasureSheet.From(d.Data).Report()); return f; }),
            new("svg", "Diagramm (SVG)", ".svg", (d, f) => { File.WriteAllText(f, ChartDrawing.Svg(MeasureSheet.From(d.Data))); return f; }),
        ],
        Workspace = SheetWorkspace,
        Properties = SheetProperties,
        Status = d => { var s = MeasureSheet.From(d.Data); return $"{s.Columns.Count} Spalten · {s.Rows} Zeilen · Diagramm: {s.Chart.Kind} · Ausgleich: {s.Chart.Fit}"; },
        Help = "Links in die Tabelle tippen (Enter = nächste Zeile, Tab = nächste Spalte, Entf leert). Klick auf einen Spaltenkopf wählt die Spalte – rechts stehen Kennzahlen und Tests, dort legst du auch fest, was ins Diagramm kommt.",
    }, "wissenschaft", "forschung", "statistik", "messwerte", "csv", "mittelwert", "regression", "t-test", "signifikant", "diagramm", "messdaten");

    static System.Text.Json.Nodes.JsonObject SheetDoc(MeasureSheet s) { var o = new System.Text.Json.Nodes.JsonObject(); s.WriteTo(o); return o; }

    public void SheetEdit(string label, Action<MeasureSheet> change)
    {
        var d = ProgramActive(SK) ?? throw new InvalidOperationException("Es ist keine Messreihe offen.");
        ProgramRun(SK, () => d.Edit(label, x => { var s = MeasureSheet.From(x); change(s); s.WriteTo(x); }));
    }

    public void SheetCell(int col, int row, string text) => SheetEdit("Wert", s => s.Set(col, row, MeasureSheet.Parse(text)));
    public (int Col, int Row) SheetCursor => (_msCol, _msRow);

    private Control SheetWorkspace(ProgramDocument d)
    {
        var s = MeasureSheet.From(d.Data); _msCol = Math.Clamp(_msCol, 0, Math.Max(0, s.Columns.Count - 1));
        var grid = new MeasureGrid(s, _msCol, Math.Max(0, _msRow), (c, r, t) => { _msCol = c; _msRow = r + 1; SheetCell(c, r, t); }, (c, r) => { var changed = c != _msCol || r < 0 != _msRow < 0; _msCol = c; _msRow = r; if (changed) Show(SK); }) { Name = "ms-grid", Top = Math.Max(0, _msRow - 12) };
        grid.AttachedToVisualTree += (_, _) => Avalonia.Threading.Dispatcher.UIThread.Post(() => { if (grid.IsAttached()) grid.Focus(); }, Avalonia.Threading.DispatcherPriority.Input);
        var chart = new ShapeView((w, h) => ChartDrawing.Build(s, w, h)) { Name = "ms-chart" };
        Border Box(Control c) => new() { CornerRadius = new CornerRadius(12), ClipToBounds = true, BorderBrush = EdgeLit, BorderThickness = new Thickness(1), Child = c };
        return new Grid { ColumnDefinitions = new ColumnDefinitions("5*,10,6*"), Children = { Box(grid), Pos(Box(chart), 2) } };
    }

    private Control SheetProperties(ProgramDocument d)
    {
        var s = MeasureSheet.From(d.Data); var sp = new StackPanel { Spacing = 5 };
        Button Act2(string name, string label, Action go) { var b = new Button { Name = name, Content = label, Padding = new Thickness(10, 4), Margin = new Thickness(0, 4, 4, 0) }; b.Click += (_, _) => go(); return b; }
        void Fact(string label, string value, IBrush? c = null) => sp.Children.Add(Row(P(label, null, 12), new TextBlock { Text = value, Foreground = c ?? Text, FontSize = 12, FontWeight = FontWeight.SemiBold }));
        string F(double v) => Fach.F(v, Math.Abs(v) >= 100 ? 1 : 3);
        if (s.Columns.Count == 0) { sp.Children.Add(P("Keine Spalten.", null, 12)); return sp; }
        var ci = Math.Clamp(_msCol, 0, s.Columns.Count - 1); var col = s.Columns[ci];
        sp.Children.Add(H("Spalte", 13));
        var name = new TextBox { Name = "ms-p-name", Text = col.Name }; var unit = new TextBox { Name = "ms-p-unit", Text = col.Unit, Width = 90 };
        void Rename() { if (!string.IsNullOrWhiteSpace(name.Text) && (name.Text != col.Name || unit.Text != col.Unit)) SheetEdit("Spalte benennen", x => { x.Columns[ci].Name = name.Text!.Trim(); x.Columns[ci].Unit = unit.Text?.Trim() ?? ""; }); }
        name.LostFocus += (_, _) => Rename(); OnEnter(name, Rename); unit.LostFocus += (_, _) => Rename(); OnEnter(unit, Rename);
        sp.Children.Add(name); sp.Children.Add(Row(P("Einheit", Text, 12), unit));
        sp.Children.Add(new WrapPanel { Children = {
            Act2("ms-as-x", s.Chart.X == ci ? "✓ ist x-Achse" : "als x-Achse", () => SheetEdit("x-Achse", x => { x.Chart.X = ci; x.Chart.Ys.Remove(ci); })),
            Act2("ms-as-y", s.Chart.Ys.Contains(ci) ? "✓ im Diagramm" : "ins Diagramm", () => SheetEdit("Diagramm", x => { if (!x.Chart.Ys.Remove(ci)) { x.Chart.Ys.Add(ci); if (x.Chart.X == ci) x.Chart.X = -1; } })),
            Act2("ms-delcol", "Spalte löschen", () => { SheetEdit("Spalte löschen", x => { x.Columns.RemoveAt(ci); x.Chart.Ys = x.Chart.Ys.Where(i => i != ci).Select(i => i > ci ? i - 1 : i).ToList(); x.Chart.X = x.Chart.X == ci ? -1 : x.Chart.X > ci ? x.Chart.X - 1 : x.Chart.X; }); _msCol = Math.Max(0, ci - 1); }) } });
        var v = s.Values(ci);
        if (v.Length > 0)
        {
            var st = Stats.Describe(col.Name, v);
            sp.Children.Add(H("Kennzahlen", 12));
            Fact("Anzahl n", st.N.ToString()); Fact("Mittelwert", F(st.Mean)); Fact("Median", F(st.Median)); Fact("Streuung (s)", F(st.StdDev)); Fact("Bereich", $"{F(st.Min)} … {F(st.Max)}");
            if (v.Length >= 2) { var cf = StatsPlus.Confidence(v); Fact("95 %-Bereich des Mittelwerts", $"{F(cf.Low)} … {F(cf.High)}"); }
            if (v.Length >= 4) { var q = StatsPlus.Quartiles(v); Fact("Quartile", $"{F(q.Q1)} · {F(q.Q2)} · {F(q.Q3)}"); Fact("Ausreißer (1,5·IQR)", q.Outliers.Count == 0 ? "keine" : string.Join(", ", q.Outliers.Select(F))); }
            // Vergleich mit einer anderen Spalte
            var others = s.Columns.Select((c, i) => (c, i)).Where(x => x.i != ci && s.Values(x.i).Length >= 2).ToList();
            if (others.Count > 0 && v.Length >= 2)
            {
                sp.Children.Add(H("Vergleich", 12));
                var pick = new ComboBox { Name = "ms-cmp", Width = 150, ItemsSource = others.Select(o => o.c.Name).ToList(), SelectedIndex = Math.Max(0, others.FindIndex(o => o.i == (s.Chart.X == ci ? -1 : s.Chart.X))) };
                var res = new StackPanel { Name = "ms-cmp-res", Spacing = 3 };
                void Run()
                {
                    res.Children.Clear(); if (pick.SelectedIndex < 0) return; var oi = others[pick.SelectedIndex].i; var w = s.Values(oi);
                    var welch = Stats.Welch(v, w);
                    res.Children.Add(new TextBlock { Text = $"Gruppen (Welch-t-Test): t = {Fach.F(welch.T, 2)}, p = {Fach.F(welch.P, 4)} → {(welch.Significant ? "✓ Unterschied ist signifikant (5 %)" : "kein gesicherter Unterschied")}", Foreground = Text, FontSize = 12, TextWrapping = TextWrapping.Wrap });
                    var (x, y) = s.Pairs(oi, ci);
                    if (x.Length >= 3)
                    {
                        var r = Stats.Linear(x, y);
                        res.Children.Add(new TextBlock { Text = $"Zusammenhang mit „{s.Columns[oi].Name}“: r = {Fach.F(r.R, 3)} ({StatsPlus.Describe(r.R)}), R² = {Fach.F(r.R2, 3)}", Foreground = Text, FontSize = 12, TextWrapping = TextWrapping.Wrap });
                        var pt = StatsPlus.Paired(x, y);
                        res.Children.Add(new TextBlock { Text = $"Vorher/nachher (gepaart, n = {x.Length}): p = {Fach.F(pt.P, 4)} → {(pt.Significant ? "✓ signifikant" : "nicht signifikant")}", Foreground = Muted, FontSize = 11.5, TextWrapping = TextWrapping.Wrap });
                    }
                }
                pick.SelectionChanged += (_, _) => Run(); Run();
                sp.Children.Add(Row(P("mit", Text, 12), pick)); sp.Children.Add(res);
            }
            sp.Children.Add(H("Daraus neue Spalte", 12));
            sp.Children.Add(new WrapPanel { Children = {
                Act2("ms-smooth", "Gleitender Mittelwert (3)", () => SheetEdit("Glätten", x => { var src = x.Columns[ci]; var vals = src.Values.Select(q => q ?? double.NaN).ToArray(); var nc = x.AddColumn(src.Name + " geglättet", src.Unit); for (var i = 0; i < vals.Length; i++) { var win = Enumerable.Range(i - 1, 3).Where(k => k >= 0 && k < vals.Length && !double.IsNaN(vals[k])).Select(k => vals[k]).ToList(); nc.Values[i] = win.Count == 3 ? win.Average() : null; } })),
                Act2("ms-z", "z-Werte", () => SheetEdit("z-Werte", x => { var src = x.Columns[ci]; var stt = Stats.Describe(src.Name, x.Values(ci)); if (stt.StdDev == 0) throw new ArgumentException("Alle Werte gleich – keine z-Werte."); var nc = x.AddColumn(src.Name + " (z)"); for (var i = 0; i < src.Values.Count; i++) nc.Values[i] = src.Values[i] is { } q ? (q - stt.Mean) / stt.StdDev : null; })) } });
        }
        // Ausgleichskurve
        if (s.Chart.X >= 0 && s.Chart.X < s.Columns.Count && s.Chart.Fit != "keine")
            foreach (var y in s.Chart.Ys.Where(y => y < s.Columns.Count))
                if (Fitting.Fit(s, s.Chart.X, y, s.Chart.Fit) is { } f) { sp.Children.Add(H("Ausgleich", 12)); sp.Children.Add(new SelectableTextBlock { Name = "ms-fit", Text = $"{s.Columns[y].Name}: {f.Formula}\nR² = {Fach.F(f.R2, 4)}", Foreground = Brush.Parse("#7FD3FF"), FontSize = 12.5, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap }); }
        // Import + Rechner
        sp.Children.Add(H("CSV laden", 12));
        var csv = Field("ms-csv-path", "CSV-Datei (Excel, Messgerät)", 140); sp.Children.Add(csv);
        sp.Children.Add(Act2("ms-csv-load", "Laden", () => ProgramRun(SK, () => { var p = Need(csv, "die CSV-Datei"); if (!File.Exists(p)) throw new FileNotFoundException("Diese Datei gibt es nicht."); var ns = MeasureSheet.FromCsv(File.ReadAllText(p), Path.GetFileNameWithoutExtension(p)); SheetEdit("CSV laden", x => { x.Columns = ns.Columns; x.Chart = ns.Chart; x.Name = ns.Name; }); _msCol = 0; _msRow = 0; _progMsg[SK] = $"„{Path.GetFileName(p)}“ geladen – {ns.Columns.Count} Spalten, {ns.Rows} Zeilen."; })));
        var calcs = new StackPanel { Spacing = 10 };
        LiveCalc(calcs, "sci-chi", "Häufigkeiten vergleichen (Chi²)", [("t", "Kreuztabelle (Zeilen mit |)", "20 30 | 40 10", null)],
            v2 => { var rows = v2("t").Split('|').Select(r => r.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(x => Fach.Num(x)).ToArray()).ToArray(); var c = StatsPlus.ChiSquare(rows); return $"χ² = {Fach.F(c.Chi2, 3)}, df = {c.Df}, p = {Fach.F(c.P, 4)} → {(c.Significant ? "✓ Zusammenhang signifikant" : "kein gesicherter Zusammenhang")}{(c.LowExpected ? " ⚠ erwartete Häufigkeit < 5" : "")}"; });
        LiveCalc(calcs, "sci-n", "Wie viele Messungen brauche ich?", [("s", "Streuung (Vorstudie)", "2,5", null), ("m", "Genauigkeit ±", "0,5", null), ("c", "Sicherheit", "95", ["90", "95", "99"])],
            v2 => $"mindestens {StatsPlus.SampleSize(Fach.Num(v2("s")), Fach.Num(v2("m")), Fach.Num(v2("c")) / 100)} Messungen");
        LiveCalc(calcs, "sci-err", "Messunsicherheit weiterrechnen", [("a", "Wert a ± u", "12,3 0,2", null), ("op", "Rechenart", "*", ["+", "-", "*", "/"]), ("b", "Wert b ± u", "4,1 0,1", null)],
            v2 => { var a = v2("a").Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(x => Fach.Num(x)).ToArray(); var b = v2("b").Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(x => Fach.Num(x)).ToArray(); if (a.Length < 2 || b.Length < 2) throw new FormatException("Bitte „Wert Unsicherheit“, z. B. „12,3 0,2“."); var r = StatsPlus.Propagate(v2("op")[0], (a[0], a[1]), (b[0], b[1])); return $"Ergebnis {Fach.F(r.Value, 4)} ± {Fach.F(r.Uncertainty, 4)}"; });
        sp.Children.Add(new Expander { Name = "ms-calcs", Header = "Rechner (Chi², Stichprobe, Unsicherheit)", IsExpanded = false, Content = calcs });
        return sp;
    }
}
