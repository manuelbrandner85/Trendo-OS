using Trendo.Core.Office;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>T-0228 Stufe A: Trendo Tabelle – Rechnen, Formate, Übersetzen, Zeilen/Spalten, Sortieren, Filtern.</summary>
public class SheetCalcTests
{
    static (SheetDoc D, Sheet S, SheetCalc C) New()
    {
        var d = new SheetDoc(); var s = d.Sheets[0];
        return (d, s, new SheetCalc(d) { Now = new DateTime(2026, 10, 27, 14, 30, 0) });
    }

    [Fact(DisplayName = "Tabelle: Grundrechnen, Vorrang, Prozent, Text verketten, Vergleiche")]
    public void Basics()
    {
        var (_, s, c) = New();
        s.Set("A1", "12,5"); s.Set("A2", "7,5"); s.Set("A3", "=A1+A2*2"); s.Set("A4", "=(A1+A2)*2"); s.Set("A5", "=2^3^2");
        s.Set("A6", "=50%*A1"); s.Set("A7", "=\"Summe: \"&A3"); s.Set("A8", "=A1>A2"); s.Set("A9", "=-A2+1");
        Assert.Equal(27.5, c.Value(s, "A3").Number);
        Assert.Equal(40, c.Value(s, "A4").Number);
        Assert.Equal(64, c.Value(s, "A5").Number);
        Assert.Equal(6.25, c.Value(s, "A6").Number);
        Assert.Equal("Summe: 27,5", c.Value(s, "A7").Text);
        Assert.Equal(CellValue.Bool(true), c.Value(s, "A8"));
        Assert.Equal(-6.5, c.Value(s, "A9").Number);
    }

    [Fact(DisplayName = "Tabelle: Funktionen deutsch und englisch, Bereiche, leere Zellen und Text zählen nicht")]
    public void Functions()
    {
        var (_, s, c) = New();
        for (var r = 1; r <= 5; r++) s.Set($"A{r}", (r * 10).ToString());
        s.Set("A6", "Text"); s.Set("B1", "Miete"); s.Set("B2", "Strom"); s.Set("B3", "Miete");
        string[] f = ["=SUMME(A1:A6)", "=SUM(A1:A6)", "=MITTELWERT(A1:A5)", "=MIN(A1:A5)", "=MAX(A1:A5)", "=ANZAHL(A1:A6)", "=ANZAHL2(A1:A6)",
            "=RUNDEN(2,345;2)", "=AUFRUNDEN(2,341;2)", "=ABRUNDEN(2,349;2)", "=WENN(A1>5;\"groß\";\"klein\")", "=SUMMEWENN(B1:B3;\"Miete\";A1:A3)",
            "=ZÄHLENWENN(A1:A5;\">20\")", "=SVERWEIS(\"Strom\";B1:C3;1;FALSCH)", "=VERKETTEN(\"a\";\"b\";1)", "=LÄNGE(\"Hallo\")", "=GROSS(\"straße\")",
            "=MEDIAN(A1:A4)", "=PRODUKT(2;3;4)", "=REST(-7;3)", "=WURZEL(16)", "=UND(WAHR;A1>0)", "=WENNFEHLER(1/0;\"x\")"];
        object[] want = [150.0, 150.0, 30.0, 10.0, 50.0, 5.0, 6.0, 2.35, 2.35, 2.34, "groß", 40.0, 3.0, "Strom", "ab1", 5.0, "STRASSE", 25.0, 24.0, 2.0, 4.0, true, "x"];
        for (var i = 0; i < f.Length; i++)
        {
            s.Set("D1", f[i]); c.Invalidate();
            var v = c.Value(s, "D1");
            switch (want[i])
            {
                case double d: Assert.True(v.IsNumber && Math.Abs(v.Number - d) < 1e-9, $"{f[i]} → {v}"); break;
                case string t: Assert.Equal(t, v.Text); break;
                case bool b: Assert.Equal(CellValue.Bool(b), v); break;
            }
        }
    }

    [Fact(DisplayName = "Tabelle: Fehlerwerte verständlich (#DIV/0!, #NAME?, #WERT!, #NV, #ZIRKEL!, #BEZUG!)")]
    public void Errors()
    {
        var (d, s, c) = New();
        s.Set("A1", "=1/0"); s.Set("A2", "=GIBTSNICHT(1)"); s.Set("A3", "=\"a\"*2"); s.Set("A4", "=SVERWEIS(9;B1:B2;1;FALSCH)");
        s.Set("A5", "=A6"); s.Set("A6", "=A5"); s.Set("A7", "=Fehlt!A1"); s.Set("A8", "=A1+1"); s.Set("A9", "=SUMME(A1:A2");
        Assert.Equal("#DIV/0!", c.Value(s, "A1").Text);
        Assert.Equal("#NAME?", c.Value(s, "A2").Text);
        Assert.Equal("#WERT!", c.Value(s, "A3").Text);
        Assert.Equal("#NV", c.Value(s, "A4").Text);
        Assert.Equal("#ZIRKEL!", c.Value(s, "A5").Text);
        Assert.Equal("#BEZUG!", c.Value(s, "A7").Text);
        Assert.Equal("#DIV/0!", c.Value(s, "A8").Text); // Fehler schlagen durch
        Assert.Equal("#WERT!", c.Value(s, "A9").Text);
        Assert.NotNull(Formula.Check("SUMME(A1:A2"));
        Assert.Contains("GIBTSNICHT", Formula.Check("GIBTSNICHT(1)"));
        Assert.Null(Formula.Check("SUMME(A1:A2;3,5)"));
        _ = d;
    }

    [Fact(DisplayName = "Tabelle: Eingaben deutsch deuten und anzeigen (Zahl, Euro, Prozent, Datum)")]
    public void Formats()
    {
        Assert.Equal((CellValue.Num(1234.5), (NumFmt?)null), SheetCalc.Literal("1.234,5"));
        Assert.Equal(NumFmt.Euro, SheetCalc.Literal("12,50 €").Suggest);
        Assert.Equal(0.19, SheetCalc.Literal("19 %").Value.Number, 9);
        Assert.Equal(NumFmt.Datum, SheetCalc.Literal("27.10.2026").Suggest);
        Assert.Equal(CellValue.K.Text, SheetCalc.Literal("'0049").Value.Kind);
        Assert.Equal("1.234,50 €", SheetFormat.Display(CellValue.Num(1234.5), NumFmt.Euro));
        Assert.Equal("19 %", SheetFormat.Display(CellValue.Num(0.19), NumFmt.Prozent));
        Assert.Equal("27.10.2026", SheetFormat.Display(CellValue.Num(SheetFormat.ToSerial(new DateTime(2026, 10, 27))), NumFmt.Datum));
        Assert.Equal("0,3333333333", SheetFormat.Display(CellValue.Num(1.0 / 3), NumFmt.Standard));
        Assert.Equal("1.235", SheetFormat.Display(CellValue.Num(1234.5), NumFmt.Ganzzahl));
        var (_, s, c) = New();
        s.Set("A1", "=HEUTE()"); s.Get("A1").Format = NumFmt.Datum; s.Set("A2", "=JAHR(A1)");
        Assert.Equal("27.10.2026", c.Display(s, 0, 0));
        Assert.Equal(2026, c.Value(s, "A2").Number);
    }

    [Fact(DisplayName = "Tabelle: Formeln deutsch ↔ XLSX (englisch) übersetzen, verlustfrei hin und zurück")]
    public void Translate()
    {
        var de = "SUMME(A1:A3;2,5)+WENN(B1>0;\"ja; nein\";FALSCH)&TEIL(\"x\";1;1)";
        var en = Formula.ToExcel(de);
        Assert.Equal("SUM(A1:A3,2.5)+IF(B1>0,\"ja; nein\",FALSE)&MID(\"x\",1,1)", en);
        Assert.Equal(de, Formula.FromExcel(en));
        Assert.Equal("Tabelle2!A1+'Mein Blatt'!$B$2", Formula.ToExcel("Tabelle2!A1+'Mein Blatt'!$B$2"));
    }

    [Fact(DisplayName = "Tabelle: Zeilen/Spalten einfügen und löschen – Formeln wandern mit, gelöschte Bezüge werden #BEZUG!")]
    public void InsertDelete()
    {
        var (_, s, c) = New();
        s.Set("A1", "1"); s.Set("A2", "2"); s.Set("A3", "3"); s.Set("B1", "=SUMME(A1:A3)"); s.Set("B2", "=A2*10"); s.Set("C1", "=$A$3");
        s.InsertRows(1);
        Assert.Equal("=SUMME(A1:A4)", s.Raw(0, 1));
        Assert.Equal("=A3*10", s.Raw(2, 1));
        Assert.Equal("=$A$4", s.Raw(0, 2));
        Assert.Equal(6, c.Value(s, "B1").Number);
        s.DeleteRows(2); c.Invalidate(); // die Zeile mit der „2“ und „=A3*10“
        Assert.Equal("=SUMME(A1:A3)", s.Raw(0, 1));
        Assert.Equal(4, c.Value(s, "B1").Number);
        s.Set("D1", "=A2"); s.DeleteRows(1); c.Invalidate();
        Assert.Equal("=#BEZUG!", s.Raw(0, 3));
        s.InsertCols(0); c.Invalidate();
        Assert.Equal("=SUMME(B1:B2)", s.Raw(0, 2));
    }

    [Fact(DisplayName = "Tabelle: Sortieren (Zahlen, Text deutsch, leer ans Ende, Formeln der Zeile wandern mit) und Filtern")]
    public void SortFilter()
    {
        var (d, s, c) = New();
        string[] names = ["Zoe", "Ärger", "Anna", "", "Bernd"];
        for (var i = 0; i < names.Length; i++) { s.Set(i + 1, 0, names[i]); s.Set(i + 1, 1, ((i + 1) * 3 % 5).ToString()); s.Set(i + 1, 2, $"=B{i + 2}*2"); }
        s.Set("A1", "Name"); s.Set("B1", "Punkte");
        s.SortRows(1, 5, 0, 2, 0, ascending: true, d); c.Invalidate();
        Assert.Equal(["Anna", "Ärger", "Bernd", "Zoe", ""], Enumerable.Range(1, 5).Select(r => s.Raw(r, 0)));
        for (var r = 1; r <= 5; r++) Assert.Equal(c.Value(s, r, 1).Number * 2, c.Value(s, r, 2).Number); // Formeln zeigen weiter auf die eigene Zeile
        s.SortRows(1, 5, 0, 2, 1, ascending: false, d); c.Invalidate();
        Assert.True(c.Value(s, 1, 1).Number >= c.Value(s, 2, 1).Number);
        var hidden = s.Filter(0, 5, 1, v => v.IsNumber && v.Number >= 3, d);
        Assert.Equal(Enumerable.Range(1, 5).Count(r => c.Value(s, r, 1).Number < 3), hidden);
        s.ClearFilter(); Assert.Empty(s.HiddenRows);
    }

    [Fact(DisplayName = "Tabelle: mehrere Blätter, Bezüge zwischen Blättern, Umbenennen, mindestens ein Blatt")]
    public void Sheets()
    {
        var (d, s, c) = New();
        var t2 = d.AddSheet("Kosten"); t2.Set("A1", "100");
        s.Set("A1", "=Kosten!A1*2");
        Assert.Equal(200, c.Value(s, "A1").Number);
        Assert.Throws<ArgumentException>(() => d.AddSheet("kosten"));
        Assert.Throws<ArgumentException>(() => d.RenameSheet(t2, "a/b"));
        d.RemoveSheet(t2);
        Assert.Throws<InvalidOperationException>(() => d.RemoveSheet(s));
        Assert.Equal("Tabelle2", d.AddSheet().Name);
    }
}
