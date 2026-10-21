using Avalonia.Controls;
using Trendo.Core;

namespace Trendo.App;

/// <summary>SPEC-05: Wissenschaft & Forschung – Messreihen auswerten.</summary>
public partial class MainWindow
{
    private void InitSpec05() => RegisterTab(FachTabs, "wissenschaft", "Messdaten", "SPEC-05", ScienceView, "wissenschaft", "forschung", "statistik", "messwerte", "csv", "mittelwert", "regression", "t-test", "signifikant");

    private static string StatLine(ColumnStats s) => $"{s.Name}: n = {s.N}, Mittelwert {Fach.F(s.Mean, 3)}, Median {Fach.F(s.Median, 3)}, Streuung ± {Fach.F(s.StdDev, 3)}, von {Fach.F(s.Min, 3)} bis {Fach.F(s.Max, 3)}";

    private Control ScienceView()
    {
        var sp = new StackPanel { Spacing = 8 };
        sp.Children.Add(P("Werte deine Messreihen aus: eine CSV-Datei (aus Excel, LibreOffice oder einem Messgerät) mit Kopfzeile. Deutsche Kommazahlen gehen.", Text));
        sp.Children.Add(FachTool("sci-desc", "Überblick", "Kennzahlen jeder Spalte; der Bericht wird als Markdown neben der Datei gespeichert.",
            [("file", "CSV-Datei", "")],
            v => { var f = v("file").Trim().Trim('"'); var t = DataTable.Load(f); var rep = Path.ChangeExtension(f, ".auswertung.md"); File.WriteAllText(rep, Stats.Report(t, Path.GetFileName(f))); return string.Join("\n", t.Describe().Select(StatLine)) + $"\n\nBericht: {rep}"; }, "Auswerten"));
        sp.Children.Add(FachTool("sci-fit", "Zusammenhang zweier Spalten", "Gerade durch die Punkte (lineare Regression) und wie eng der Zusammenhang ist.",
            [("file", "CSV-Datei", ""), ("x", "Spalte x", "1"), ("y", "Spalte y", "2")],
            v => { var r = DataTable.Load(v("file").Trim().Trim('"')).Fit(v("x"), v("y")); var a = Math.Abs(r.R); return $"y = {Fach.F(r.Slope, 4)} · x + {Fach.F(r.Intercept, 4)}\nr = {Fach.F(r.R, 3)}, R² = {Fach.F(r.R2, 3)} ({r.N} Paare) → {StatsPlus.Describe(r.R)}"; }));
        sp.Children.Add(FachTool("sci-t", "Zwei Gruppen vergleichen", "Unterscheiden sich zwei Spalten wirklich oder nur zufällig? (Welch-t-Test, 5 %)",
            [("file", "CSV-Datei", ""), ("a", "Gruppe A", "1"), ("b", "Gruppe B", "2")],
            v => { var t = DataTable.Load(v("file").Trim().Trim('"')); var r = Stats.Welch(t.Values(v("a")), t.Values(v("b"))); return $"Mittelwerte {Fach.F(r.MeanA, 3)} gegen {Fach.F(r.MeanB, 3)} · t = {Fach.F(r.T, 3)}, p = {Fach.F(r.P, 4)}\n{(r.Significant ? "✓ Der Unterschied ist sehr wahrscheinlich echt (signifikant)." : "Der Unterschied kann gut Zufall sein (nicht signifikant).")}"; }, "Vergleichen"));
        sp.Children.Add(FachGroup("sci-tests", "Vergleichen & testen", false,
            FachTool("sci-paired", "Vorher / nachher", "Dieselben Personen oder Proben zweimal gemessen (gepaarter t-Test).",
                [("file", "CSV-Datei", ""), ("a", "Vorher", "1"), ("b", "Nachher", "2")],
                v => { var t = DataTable.Load(v("file").Trim().Trim('"')); var r = StatsPlus.Paired(t.Values(v("a")), t.Values(v("b"))); return $"Mittel vorher {Fach.F(r.MeanA, 3)}, nachher {Fach.F(r.MeanB, 3)} · t = {Fach.F(r.T, 3)}, p = {Fach.F(r.P, 4)}\n{(r.Significant ? "✓ Die Veränderung ist sehr wahrscheinlich echt." : "Die Veränderung kann Zufall sein.")}"; }, "Vergleichen"),
            FachTool("sci-chi", "Häufigkeiten vergleichen (Chi²)", "Kreuztabelle: Zeilen mit | trennen, Zahlen mit Leerzeichen. Beispiel: Raucher ja/nein × krank ja/nein.",
                [("tab", "Tabelle", "20 30 | 40 10")],
                v => { var t = v("tab").Split('|').Select(r => r.Split([' ', ';'], StringSplitOptions.RemoveEmptyEntries).Select(x => Fach.Num(x)).ToArray()).ToArray(); var r = StatsPlus.ChiSquare(t); return $"χ² = {Fach.F(r.Chi2, 3)}, Freiheitsgrade {r.Df}, p = {Fach.F(r.P, 4)}\n{(r.Significant ? "✓ Die Merkmale hängen zusammen." : "Kein Zusammenhang nachweisbar.")}{(r.LowExpected ? "\n⚠ Einige erwartete Werte unter 5 – Ergebnis unsicher." : "")}"; }, "Testen"),
            FachTool("sci-ci", "Wie genau ist der Mittelwert?", "95 %-Vertrauensbereich einer Spalte.",
                [("file", "CSV-Datei", ""), ("col", "Spalte", "1")],
                v => { var c = StatsPlus.Confidence(DataTable.Load(v("file").Trim().Trim('"')).Values(v("col"))); return $"Mittelwert {Fach.F(c.Mean, 3)} ± {Fach.F(c.Half, 3)} → mit 95 % Sicherheit zwischen {Fach.F(c.Low, 3)} und {Fach.F(c.High, 3)}"; }),
            FachTool("sci-n", "Wie viele Messungen brauche ich?", "Streuung aus einer Vorstudie, gewünschte Genauigkeit (±).",
                [("sd", "Streuung", "10"), ("e", "Genauigkeit ±", "2"), ("c", "Sicherheit (90/95/99)", "95")],
                v => $"Mindestens {StatsPlus.SampleSize(Fach.Num(v("sd")), Fach.Num(v("e")), Fach.Num(v("c")) / 100)} Messungen", "Rechnen"),
            FachTool("sci-err", "Messunsicherheit weiterrechnen", "Zwei Messwerte mit Unsicherheit, Rechenart + − * /.",
                [("a", "Wert A", "12,3"), ("ua", "± A", "0,2"), ("op", "Rechenart", "*"), ("b", "Wert B", "4,1"), ("ub", "± B", "0,1")],
                v => { var r = StatsPlus.Propagate(v("op").Trim().Replace("×", "*").Replace("÷", "/").Replace("−", "-").FirstOrDefault(), (Fach.Num(v("a")), Fach.Num(v("ua"))), (Fach.Num(v("b")), Fach.Num(v("ub")))); return $"Ergebnis: {Fach.F(r.Value, 4)} ± {Fach.F(r.Uncertainty, 4)} ({Fach.F(r.Uncertainty / Math.Abs(r.Value) * 100, 2)} %)"; })));
        sp.Children.Add(FachGroup("sci-view", "Darstellen & aufbereiten", false,
            FachTool("sci-quart", "Verteilung & Ausreißer", "Quartile wie in Excel (QUARTILE.INKL), Ausreißer nach der 1,5-IQR-Regel. Speichert ein Histogramm als SVG.",
                [("file", "CSV-Datei", ""), ("col", "Spalte", "1")],
                v => { var f = v("file").Trim().Trim('"'); var t = DataTable.Load(f); var x = t.Values(v("col")); var q = StatsPlus.Quartiles(x); var svg = Path.ChangeExtension(f, ".histogramm.svg"); File.WriteAllText(svg, StatsPlus.HistogramSvg(StatsPlus.Histogram(x), t.Columns[t.Index(v("col"))])); return $"Q1 {Fach.F(q.Q1, 3)} · Median {Fach.F(q.Q2, 3)} · Q3 {Fach.F(q.Q3, 3)} · IQR {Fach.F(q.Iqr, 3)}\nAusreißer: {(q.Outliers.Count == 0 ? "keine" : string.Join(", ", q.Outliers.Select(o => Fach.F(o, 3))))}\nHistogramm: {svg}"; }, "Ansehen"),
            FachTool("sci-scatter", "Streudiagramm", "Punkte mit Ausgleichsgerade als SVG.",
                [("file", "CSV-Datei", ""), ("x", "Spalte x", "1"), ("y", "Spalte y", "2")],
                v => { var f = v("file").Trim().Trim('"'); var t = DataTable.Load(f); int xi = t.Index(v("x")), yi = t.Index(v("y")); var pts = t.Rows.Where(r => r[xi].HasValue && r[yi].HasValue).ToList(); var svg = Path.ChangeExtension(f, ".streudiagramm.svg"); File.WriteAllText(svg, StatsPlus.ScatterSvg(pts.Select(r => r[xi]!.Value).ToArray(), pts.Select(r => r[yi]!.Value).ToArray(), $"{t.Columns[yi]} über {t.Columns[xi]}")); Launch(svg); return "Gespeichert: " + svg; }, "Zeichnen"),
            FachTool("sci-smooth", "Glätten & standardisieren", "Hängt zwei Spalten an eine Kopie der Datei: gleitender Mittelwert und z-Wert.",
                [("file", "CSV-Datei", ""), ("col", "Spalte", "2"), ("win", "Fenster (ungerade)", "3")],
                v => { var f = v("file").Trim().Trim('"'); var t = DataTable.Load(f); var i = t.Index(v("col")); var x = t.Values(v("col")); var csv = File.ReadAllText(f); csv = StatsPlus.AppendColumn(csv, t.Columns[i] + "_geglaettet", StatsPlus.MovingAverage(x, (int)Fach.Num(v("win"))), i); csv = StatsPlus.AppendColumn(csv, t.Columns[i] + "_z", StatsPlus.ZScores(x).Select(z => (double?)z).ToList(), i); var o = Path.ChangeExtension(f, ".aufbereitet.csv"); File.WriteAllText(o, csv); return "Gespeichert: " + o; }, "Aufbereiten")));
        return FachPage("Messdaten", sp);
    }
}
