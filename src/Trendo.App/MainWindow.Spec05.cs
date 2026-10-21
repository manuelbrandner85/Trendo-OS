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
            v => { var r = DataTable.Load(v("file").Trim().Trim('"')).Fit(v("x"), v("y")); var a = Math.Abs(r.R); return $"y = {Fach.F(r.Slope, 4)} · x + {Fach.F(r.Intercept, 4)}\nr = {Fach.F(r.R, 3)}, R² = {Fach.F(r.R2, 3)} ({r.N} Paare) → {(a > 0.8 ? "starker" : a > 0.5 ? "mittlerer" : a > 0.2 ? "schwacher" : "kein erkennbarer")} Zusammenhang"; }));
        sp.Children.Add(FachTool("sci-t", "Zwei Gruppen vergleichen", "Unterscheiden sich zwei Spalten wirklich oder nur zufällig? (Welch-t-Test, 5 %)",
            [("file", "CSV-Datei", ""), ("a", "Gruppe A", "1"), ("b", "Gruppe B", "2")],
            v => { var t = DataTable.Load(v("file").Trim().Trim('"')); var r = Stats.Welch(t.Values(v("a")), t.Values(v("b"))); return $"Mittelwerte {Fach.F(r.MeanA, 3)} gegen {Fach.F(r.MeanB, 3)} · t = {Fach.F(r.T, 3)}, p = {Fach.F(r.P, 4)}\n{(r.Significant ? "✓ Der Unterschied ist sehr wahrscheinlich echt (signifikant)." : "Der Unterschied kann gut Zufall sein (nicht signifikant).")}"; }, "Vergleichen"));
        return FachPage("Messdaten", sp);
    }
}
