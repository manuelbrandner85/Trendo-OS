using Trendo.Core.Office;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>F3 Tabelle (Vorbild-Analyse „Nächste Schritte“): Pivot-Tabelle mit Formeln und Autofilter je Spalte.</summary>
public class SheetPivotTests
{
    static (SheetDoc Doc, Sheet S) Data()
    {
        var d = new SheetDoc(); var s = d.Sheets[0]; d.RenameSheet(s, "Ausgaben 2026");
        string[][] rows =
        [
            ["Monat", "Kategorie", "Betrag"],
            ["Jan", "Miete", "800"], ["Jan", "Essen", "250,5"], ["Feb", "Miete", "800"],
            ["Feb", "Essen", "199,5"], ["Feb", "Auto", "120"], ["Mär", "Essen", "300"],
        ];
        for (var r = 0; r < rows.Length; r++) for (var c = 0; c < 3; c++) s.Set(r, c, rows[r][c]);
        return (d, s);
    }

    static string D(SheetCalc c, Sheet s, string a1) { var (r, col) = Addr.Parse(a1); return c.Display(s, r, col); }

    [Fact(DisplayName = "Tabelle F3: Pivot – Summe je Kategorie und Monat als Formeln, Gesamtzeile und -spalte, rechnet bei geänderter Quelle mit")]
    public void Pivot_sum()
    {
        var (d, s) = Data();
        var p = SheetPivot.Build(d, s, 0, 0, 6, 2, rowCol: 1, colCol: 0, valCol: 2, SheetPivot.Agg.Summe);
        Assert.Equal("Pivot1", p.Name);
        var calc = new SheetCalc(d);
        string V(string a1) => D(calc, p, a1);
        Assert.Equal("Kategorie", V("A4"));
        // Spalten: Monate in Reihenfolge des Alphabets (Text) – Feb, Jan, Mär
        Assert.Equal(["Feb", "Jan", "Mär", "Gesamt"], new[] { V("B4"), V("C4"), V("D4"), V("E4") });
        Assert.Equal(["Auto", "Essen", "Miete", "Gesamt"], new[] { V("A5"), V("A6"), V("A7"), V("A8") });
        Assert.Equal(250.5, calc.Value(p, "C6").Number, 3);
        Assert.Equal(199.5, calc.Value(p, "B6").Number, 3);
        Assert.Equal(750.0, calc.Value(p, "E6").Number, 3); // Essen gesamt
        Assert.Equal(1600.0, calc.Value(p, "E7").Number, 3); // Miete
        Assert.Equal(1119.5, calc.Value(p, "B8").Number, 3); // Februar gesamt
        Assert.Equal(2470.0, calc.Value(p, "E8").Number, 3); // alles
        Assert.StartsWith("=SUMMEWENNS('Ausgaben 2026'!$C$2:$C$7;", p.Raw(5, 1));
        s.Set(1, 2, "900"); // Miete Januar geändert
        calc = new SheetCalc(d);
        Assert.Equal(1700.0, calc.Value(p, "E7").Number, 3);
        Assert.Equal(4, p.FreezeRows);
    }

    [Fact(DisplayName = "Tabelle F3: Pivot – Anzahl und Mittelwert (leere Kreuzung ohne Fehler), nur Zeilenfeld, falsche Felder abgelehnt")]
    public void Pivot_count_average()
    {
        var (d, s) = Data();
        var n = SheetPivot.Build(d, s, 0, 0, 6, 2, 1, null, 2, SheetPivot.Agg.Anzahl);
        var calc = new SheetCalc(d);
        Assert.Equal("Gesamt", D(calc, n, "B4"));
        Assert.Equal(3.0, calc.Value(n, "B6").Number); Assert.Equal(6.0, calc.Value(n, "B8").Number);
        var m = SheetPivot.Build(d, s, 0, 0, 6, 2, 1, 0, 2, SheetPivot.Agg.Mittelwert, "Schnitt");
        calc = new SheetCalc(d);
        Assert.Equal(250.0, calc.Value(m, "E6").Number, 3);
        Assert.Equal("", D(calc, m, "C5")); // Auto im Januar: nichts – leer statt #DIV/0!
        Assert.Throws<ArgumentException>(() => SheetPivot.Build(d, s, 0, 0, 6, 2, 1, 1, 2, SheetPivot.Agg.Summe));
        Assert.Throws<ArgumentException>(() => SheetPivot.Build(d, s, 0, 0, 0, 2, 1, null, 2, SheetPivot.Agg.Summe));
    }

    [Fact(DisplayName = "Tabelle F3: Autofilter – Werteliste je Spalte mit Anzahl, Filter in zwei Spalten zusammen, aufheben")]
    public void Auto_filter()
    {
        var (d, s) = Data(); s.Set(6, 1, "");
        Assert.Equal([("Auto", 1), ("Essen", 2), ("Miete", 2), (SheetPivot.Empty, 1)], SheetPivot.Values(d, s, 1, 6, 1));
        s.AutoFilter = new SheetAutoFilter(0, 6, 0, 2);
        s.AutoFilter.Columns[1] = ["Essen", SheetPivot.Empty];
        Assert.Equal(3, SheetPivot.Apply(d, s)); // Miete ×2, Auto aus
        Assert.Equal([1, 3, 5], s.HiddenRows.Order());
        s.AutoFilter.Columns[0] = ["Feb"];
        Assert.Equal(5, SheetPivot.Apply(d, s)); // nur Essen im Februar bleibt
        Assert.DoesNotContain(4, s.HiddenRows);
        var c = s.Clone(); Assert.Equal(["Feb"], c.AutoFilter!.Columns[0]);
        s.ClearFilter(); Assert.Null(s.AutoFilter); Assert.Empty(s.HiddenRows);
    }
}
