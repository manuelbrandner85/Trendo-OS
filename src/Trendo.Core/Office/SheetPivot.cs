using System.Globalization;

namespace Trendo.Core.Office;

/// <summary>F3 Tabelle (Vorbild-Analyse, nächster Schritt 1): Pivot-Tabelle. Legt ein neues Blatt an, das eine Liste nach Zeilen- und optional
/// Spaltenfeld zusammenfasst. Die Zahlen sind Formeln (SUMMEWENNS/ZÄHLENWENNS/MITTELWERTWENNS) auf die Quelle – ändern sich dort Werte, rechnet
/// die Pivot-Tabelle mit. Neue Kategorien kommen erst mit „Neu erstellen“ dazu.</summary>
public static class SheetPivot
{
    public enum Agg { Summe, Anzahl, Mittelwert }
    public static readonly string[] AggLabels = ["Summe", "Anzahl", "Mittelwert"];

    /// <summary>Feldnamen (Kopfzeile) einer Liste.</summary>
    public static List<string> Headers(SheetDoc doc, Sheet src, int top, int left, int right)
    {
        var calc = new SheetCalc(doc);
        return Enumerable.Range(left, right - left + 1).Select(c => calc.Display(src, top, c) is { Length: > 0 } h ? h : "Spalte " + Addr.Col(c)).ToList();
    }

    /// <summary>Pivot bauen. <paramref name="top"/> ist die Kopfzeile, Daten bis <paramref name="bottom"/>; Spalten sind absolute Spaltennummern.</summary>
    public static Sheet Build(SheetDoc doc, Sheet src, int top, int left, int bottom, int right, int rowCol, int? colCol, int valCol, Agg agg, string? name = null)
    {
        if (bottom <= top) throw new ArgumentException("Die Liste braucht eine Kopfzeile und mindestens eine Datenzeile.");
        foreach (var c in new[] { rowCol, valCol }.Concat(colCol is { } cc ? [cc] : []))
            if (c < left || c > right) throw new ArgumentException("Das Feld liegt nicht in der Liste.");
        if (colCol == rowCol) throw new ArgumentException("Zeilen- und Spaltenfeld müssen verschieden sein.");
        var calc = new SheetCalc(doc);
        var heads = Headers(doc, src, top, left, right);
        string Head(int c) => heads[c - left];
        // Schlüssel: angezeigter Text und der Wert, der in die Zelle kommt (Zahlen als Zahl, damit SUMMEWENNS sie findet)
        List<(string Display, string Raw)> Keys(int col)
        {
            var vals = Enumerable.Range(top + 1, bottom - top).Select(r => (V: calc.Value(src, r, col), D: calc.Display(src, r, col))).Where(x => !x.V.IsEmpty).ToList();
            return vals.GroupBy(x => x.D).Select(g => g.First())
                .OrderBy(x => x.V.IsNumber ? 0 : 1).ThenBy(x => x.V.IsNumber ? x.V.Number : 0)
                .ThenBy(x => x.D, StringComparer.Create(CultureInfo.GetCultureInfo("de-DE"), true))
                .Select(x => (x.D, x.V.IsNumber ? x.V.Number.ToString("R", CultureInfo.GetCultureInfo("de-DE")) : x.V.Text)).ToList();
        }
        var rows = Keys(rowCol); var cols = colCol is { } c2 ? Keys(c2) : [];
        if (rows.Count == 0) throw new ArgumentException($"In „{Head(rowCol)}“ steht nichts.");
        if (rows.Count > 500 || cols.Count > 100) throw new ArgumentException("Zu viele verschiedene Werte für eine Pivot-Tabelle (höchstens 500 Zeilen und 100 Spalten).");

        name ??= Enumerable.Range(1, 999).Select(i => "Pivot" + i).First(n => doc.Find(n) is null);
        var p = doc.AddSheet(name);
        var prefix = (src.Name.Any(ch => !char.IsLetterOrDigit(ch)) ? "'" + src.Name.Replace("'", "''") + "'" : src.Name) + "!";
        string Rng(int col) => prefix + $"${Addr.Col(col)}${top + 2}:${Addr.Col(col)}${bottom + 1}";
        var vRng = Rng(valCol); var rRng = Rng(rowCol); var cRng = colCol is { } c3 ? Rng(c3) : "";
        var fn = agg switch { Agg.Summe => "SUMMEWENNS", Agg.Anzahl => "ZÄHLENWENNS", _ => "MITTELWERTWENNS" };
        string F(params (string Range, string Crit)[] crit)
        {
            if (crit.Length == 0) return agg switch { Agg.Summe => $"=SUMME({vRng})", Agg.Anzahl => $"=ANZAHL2({vRng})", _ => $"=MITTELWERT({vRng})" };
            var args = string.Join(";", crit.Select(x => $"{x.Range};{x.Crit}"));
            return agg switch { Agg.Anzahl => $"={fn}({args})", Agg.Summe => $"={fn}({vRng};{args})", _ => $"=WENNFEHLER({fn}({vRng};{args});\"\")" }; // Mittelwert ohne Treffer: leer statt Fehler
        }

        p.Set(0, 0, $"Pivot: {AggLabels[(int)agg]} von {Head(valCol)} nach {Head(rowCol)}{(colCol is { } c4 ? " und " + Head(c4) : "")}");
        p.GetOrAdd(0, 0).Bold = true;
        p.Set(1, 0, $"Quelle: {src.Name}!{Addr.Col(left)}{top + 1}:{Addr.Col(right)}{bottom + 1} – rechnet mit; neue Werte in „{Head(rowCol)}“ kommen mit „Neu erstellen“ dazu");
        const int H = 3; // Kopfzeile der Pivot-Tabelle (Zeile 4)
        p.Set(H, 0, Head(rowCol));
        var fmt = agg == Agg.Anzahl ? NumFmt.Ganzzahl : src.Get(top + 1, valCol)?.Format is { } f0 and not NumFmt.Standard and not NumFmt.Text ? f0 : NumFmt.Zahl;
        for (var j = 0; j < cols.Count; j++) { p.Set(H, 1 + j, cols[j].Raw); p.GetOrAdd(H, 1 + j).Format = colCol is { } cf && src.Get(top + 1, cf)?.Format is { } f1 ? f1 : NumFmt.Standard; }
        var totalCol = 1 + cols.Count; p.Set(H, totalCol, "Gesamt");
        for (var j = 0; j <= totalCol; j++) { var hc = p.GetOrAdd(H, j); hc.Bold = true; hc.Fill = "#E8EEF6"; hc.Border = true; }
        for (var i = 0; i < rows.Count; i++)
        {
            var r = H + 1 + i; var rowRef = $"$A{r + 1}";
            p.Set(r, 0, rows[i].Raw); p.GetOrAdd(r, 0).Border = true; p.GetOrAdd(r, 0).Format = src.Get(top + 1, rowCol)?.Format ?? NumFmt.Standard;
            for (var j = 0; j < cols.Count; j++) Cell(r, 1 + j, F((rRng, rowRef), (cRng, $"{Addr.Col(1 + j)}${H + 1}")));
            Cell(r, totalCol, F((rRng, rowRef)), bold: true);
        }
        var tr = H + 1 + rows.Count;
        p.Set(tr, 0, "Gesamt"); p.GetOrAdd(tr, 0).Bold = true; p.GetOrAdd(tr, 0).Border = true;
        for (var j = 0; j < cols.Count; j++) Cell(tr, 1 + j, F((cRng, $"{Addr.Col(1 + j)}${H + 1}")), bold: true);
        Cell(tr, totalCol, F(), bold: true);
        p.ColWidths[0] = Math.Max(14, Math.Min(40, rows.Max(x => x.Display.Length) + 2));
        p.FreezeRows = H + 1; p.FreezeCols = 1;
        return p;

        void Cell(int r, int c, string raw, bool bold = false) { var x = p.GetOrAdd(r, c); x.Raw = raw; x.Format = fmt; x.Bold = bold; x.Border = true; }
    }

    // ───────────── Autofilter je Spalte (Vorbild-Analyse, nächster Schritt 3) ─────────────

    /// <summary>Verschiedene angezeigte Werte einer Spalte im Datenbereich, mit Anzahl („(leer)“ für leere Zellen).</summary>
    public static List<(string Value, int Count)> Values(SheetDoc doc, Sheet s, int firstRow, int lastRow, int col)
    {
        var calc = new SheetCalc(doc);
        return Enumerable.Range(firstRow, Math.Max(0, lastRow - firstRow + 1))
            .Select(r => (V: calc.Value(s, r, col), D: calc.Display(s, r, col))).GroupBy(x => x.V.IsEmpty ? Empty : x.D)
            .Select(g => (Key: g.Key, First: g.First().V, Count: g.Count()))
            .OrderBy(x => x.Key == Empty ? 2 : x.First.IsNumber ? 0 : 1).ThenBy(x => x.First.IsNumber ? x.First.Number : 0)
            .ThenBy(x => x.Key, StringComparer.Create(CultureInfo.GetCultureInfo("de-DE"), true)).Select(x => (x.Key, x.Count)).ToList();
    }
    public const string Empty = "(leer)";

    /// <summary>Alle Spaltenfilter zusammen anwenden: eine Zeile bleibt sichtbar, wenn sie in jeder gefilterten Spalte einen erlaubten Wert hat.</summary>
    public static int Apply(SheetDoc doc, Sheet s)
    {
        s.HiddenRows.Clear();
        if (s.AutoFilter is not { } af) return 0;
        var calc = new SheetCalc(doc);
        for (var r = af.Header + 1; r <= af.Bottom; r++)
            foreach (var (col, allowed) in af.Columns)
            {
                var v = calc.Value(s, r, col); var key = v.IsEmpty ? Empty : calc.Display(s, r, col);
                if (!allowed.Contains(key)) { s.HiddenRows.Add(r); break; }
            }
        return s.HiddenRows.Count;
    }
}

/// <summary>Autofilter eines Blatts: Kopfzeile, Datenbereich und je Spalte die erlaubten (angezeigten) Werte; fehlt eine Spalte, ist sie ungefiltert.</summary>
public sealed class SheetAutoFilter(int header, int bottom, int left, int right)
{
    public int Header { get; } = header;
    public int Bottom { get; set; } = bottom;
    public int Left { get; } = left;
    public int Right { get; } = right;
    public Dictionary<int, HashSet<string>> Columns { get; } = [];
    public SheetAutoFilter Clone()
    {
        var a = new SheetAutoFilter(Header, Bottom, Left, Right);
        foreach (var (k, v) in Columns) a.Columns[k] = [.. v];
        return a;
    }
}
