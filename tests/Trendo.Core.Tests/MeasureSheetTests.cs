using System.Text.Json.Nodes;
using Trendo.Core.Messdaten;
using Xunit;
namespace Trendo.Core.Tests;

/// <summary>T-0261: Messdaten & Auswertung – Tabelle, CSV, Ausgleichskurven gegen bekannte Zusammenhänge, Diagramm, Bericht.</summary>
public class T0261_Measure
{
    [Fact(DisplayName = "T-0261: Ausgleich – Pendel T = 2π/√g·L^0,5, Abkühlung exponentiell, Parabel exakt, Gerade mit R² = 1")]
    public void Fits()
    {
        var p = MeasureSheet.Template("pendel"); var f = Fitting.Fit(p, 0, 1, "potenz")!;
        Assert.InRange(f.R2, 0.999, 1); Assert.InRange(f.F(1), 2.0, 2.02); // 2π/√9,81 = 2,006
        var a = MeasureSheet.Template("abkuehlen"); var e = Fitting.Fit(a, 0, 1, "exp")!;
        Assert.InRange(e.R2, 0.995, 1); Assert.Contains("e^(", e.Formula);
        var q = new MeasureSheet(); q.AddColumn("x"); q.AddColumn("y");
        for (var i = 0; i < 6; i++) { q.Set(0, i, i); q.Set(1, i, 2 * i * i - 3 * i + 1); }
        var qf = Fitting.Fit(q, 0, 1, "quadratisch")!; Assert.Equal(1, qf.R2, 9); Assert.Equal(2 * 49 - 21 + 1, qf.F(7), 6);
        var lf = Fitting.Fit(q, 0, 0, "linear")!; Assert.Equal(1, lf.R2, 9);
        Assert.Null(Fitting.Fit(q, 0, 1, "exp")); // y enthält Werte ≤ 0 → nicht möglich
        var two = new MeasureSheet(); two.AddColumn("x"); two.AddColumn("y"); two.Set(0, 0, 1); two.Set(1, 0, 2); two.Set(0, 1, 2); two.Set(1, 1, 3);
        Assert.Null(Fitting.Fit(two, 0, 1, "linear")); // zwei Paare: keine Kurve, aber auch kein Absturz
        Assert.NotEmpty(ChartDrawing.Build(two, 600, 400)); two.Report();
    }

    [Fact(DisplayName = "T-0261: Zellen setzen (deutsches Komma, leer), leere Endzeilen fallen weg; CSV mit Einheiten hin und zurück")]
    public void Cells_and_csv()
    {
        var s = new MeasureSheet(); s.AddColumn("Zeit", "s"); s.AddColumn("Weg", "m");
        s.Set(0, 0, MeasureSheet.Parse("1,5")); s.Set(1, 0, MeasureSheet.Parse("3")); s.Set(0, 3, 9);
        Assert.Equal(4, s.Rows); s.Set(0, 3, null); Assert.Equal(1, s.Rows);
        Assert.Throws<FormatException>(() => MeasureSheet.Parse("abc"));
        var csv = s.ToCsv(); Assert.StartsWith("Zeit [s];Weg [m]\n1,5;3\n", csv);
        var back = MeasureSheet.FromCsv(csv); Assert.Equal("s", back.Columns[0].Unit); Assert.Equal(1.5, back.Get(0, 0));
        var data = new JsonObject(); s.WriteTo(data); Assert.Equal(1.5, MeasureSheet.From(data).Get(0, 0));
        var dup = s.AddColumn("Zeit"); Assert.Equal("Zeit 2", dup.Name);
    }

    [Fact(DisplayName = "T-0261: Diagramm (Punkte, Box, Histogramm) und Bericht mit Kennzahlen und Ausgleich; SVG gültig")]
    public void Chart_and_report()
    {
        var p = MeasureSheet.Template("pendel");
        var shapes = ChartDrawing.Build(p, 800, 500);
        Assert.Contains(shapes, x => x is Trendo.Core.Arch.DText { Text: var t } && t.Contains("R² = 0,99"));
        Assert.Equal(7, shapes.OfType<Trendo.Core.Arch.DPoly>().Count(x => x.Layer == "DATEN"));
        var d = MeasureSheet.Template("duenger"); Assert.Equal(2, ChartDrawing.Build(d, 800, 500).OfType<Trendo.Core.Arch.DText>().Count(x => x.Text is "Dünger A" or "Dünger B")); // Beschriftung unter jeder Box
        d.Chart.Kind = "histogramm"; Assert.NotEmpty(ChartDrawing.Build(d, 800, 500).OfType<Trendo.Core.Arch.DPoly>());
        System.Xml.Linq.XDocument.Parse(ChartDrawing.Svg(p));
        var rep = p.Report(); Assert.Contains("| Länge [m] | 7 |", rep); Assert.Contains("R² =", rep);
    }
}
