using PdfSharp.Drawing;
using PdfSharp.Pdf;

namespace Trendo.Core.Office;

/// <summary>T-0228 E2: Daten eines Diagramms aus einem Bereich – erste Spalte Beschriftungen, weitere Spalten Reihen; Kopfzeile = Reihennamen.</summary>
public sealed record ChartData(IReadOnlyList<string> Labels, IReadOnlyList<(string Name, double[] Values)> Series)
{
    public static readonly string[] Palette = ["#2F86F6", "#F59E0B", "#10B981", "#EF4444", "#8B5CF6", "#06B6D4", "#EC4899", "#84CC16"];

    /// <summary>Aufbau eines Bereichs: Kopfzeile? Beschriftungsspalte? Welche Spalten sind Zahlenreihen?</summary>
    public sealed record Layout(int Top, int Left, int Bottom, int Right, bool Header, bool LabelCol, IReadOnlyList<int> SeriesCols)
    {
        public int First => Header ? Top + 1 : Top;
    }

    public static Layout Analyze(Sheet s, SheetCalc calc, string range)
    {
        var (t, l, b, r) = Addr.Range(range.Contains(':') ? range : range + ":" + range);
        var header = Enumerable.Range(l, r - l + 1).Any(c => calc.Value(s, t, c).Kind == CellValue.K.Text) && Enumerable.Range(l + 1, Math.Max(0, r - l)).All(c => !calc.Value(s, t, c).IsNumber);
        var first = header ? t + 1 : t;
        var rows = Enumerable.Range(first, Math.Max(0, b - first + 1)).ToList();
        var labelCol = r > l && rows.Any(row => calc.Value(s, row, l).Kind == CellValue.K.Text);
        var cols = Enumerable.Range(labelCol ? l + 1 : l, Math.Max(0, r - (labelCol ? l + 1 : l) + 1))
            .Where(c => rows.Any(row => calc.Value(s, row, c).IsNumber)).ToList(); // Textspalten (z. B. „Art“) sind keine Reihe
        return new Layout(t, l, b, r, header, labelCol, cols);
    }

    public static ChartData From(Sheet s, SheetCalc calc, string range)
    {
        var lay = Analyze(s, calc, range);
        var rows = Enumerable.Range(lay.First, Math.Max(0, lay.Bottom - lay.First + 1)).Where(row => !s.HiddenRows.Contains(row)).ToList();
        var labels = rows.Select(row => lay.LabelCol ? calc.Display(s, row, lay.Left) : (row + 1).ToString(System.Globalization.CultureInfo.InvariantCulture)).ToList();
        var series = lay.SeriesCols.Select(c => (lay.Header ? calc.Display(s, lay.Top, c) : "Reihe " + Addr.Col(c), rows.Select(row => calc.Value(s, row, c) is { IsNumber: true } v ? v.Number : 0).ToArray())).ToList();
        return new ChartData(labels, series);
    }

    public double Max => Series.Count == 0 ? 0 : Series.SelectMany(x => x.Values).DefaultIfEmpty(0).Max();
    public double Min => Series.Count == 0 ? 0 : Math.Min(0, Series.SelectMany(x => x.Values).DefaultIfEmpty(0).Min());

    /// <summary>Achsenbeschriftung deutsch: 2.500 · 0,5</summary>
    public static string AxisLabel(double v) => Math.Round(v, 6).ToString("#,##0.######", System.Globalization.CultureInfo.GetCultureInfo("de-DE"));

    /// <summary>Gut lesbare Achsenschritte (1, 2, 5 × 10ⁿ).</summary>
    public static double NiceStep(double span, int ticks = 5)
    {
        if (span <= 0) return 1;
        var raw = span / ticks; var mag = Math.Pow(10, Math.Floor(Math.Log10(raw))); var n = raw / mag;
        return (n <= 1 ? 1 : n <= 2 ? 2 : n <= 5 ? 5 : 10) * mag;
    }
}

/// <summary>T-0228 E3: Tabelle als PDF – A4, Hoch- oder Querformat, „Auf Seitenbreite anpassen“, Kopfzeile auf jeder Seite wiederholt,
/// Zellformate, Füllfarben, Rahmen und Diagramme. Ohne Programmname oder Logo im Dokument.</summary>
public static class SheetPdf
{
    const double Cm = 72 / 2.54, Margin = 1.5 * Cm, CharW = 5.2, RowH = 16;

    public sealed record Options(bool Landscape = false, bool FitWidth = true, bool RepeatHeader = true, bool AllSheets = false, bool Gridlines = true);

    public static int Export(SheetDoc doc, Sheet active, string path, Options? o = null)
    {
        o ??= new Options();
        System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(Sys.PdfTools).TypeHandle);
        var calc = new SheetCalc(doc);
        using var pdf = new PdfDocument();
        pdf.Info.Title = Path.GetFileNameWithoutExtension(path);
        double pw = (o.Landscape ? 29.7 : 21) * Cm, ph = (o.Landscape ? 21 : 29.7) * Cm;
        foreach (var s in o.AllSheets ? doc.Sheets : [active])
        {
            if (s.MaxRow < 0 && s.Charts.Count == 0) continue;
            var cols = Math.Max(0, s.MaxCol); var rows = Math.Max(0, s.MaxRow);
            var widths = Enumerable.Range(0, cols + 1).Select(c => Math.Max(3, s.ColWidth(c)) * CharW + 8).ToArray();
            var total = widths.Sum(); var avail = pw - 2 * Margin;
            var scale = o.FitWidth && total > avail ? avail / total : 1;
            // Spalten, die nicht auf die Seite passen, kommen auf Folgeseiten (wie in Calc „erst nach unten, dann nach rechts“)
            var bands = new List<(int From, int To)>(); var start = 0; var acc = 0.0;
            for (var c = 0; c <= cols; c++) { if (acc + widths[c] * scale > avail && c > start) { bands.Add((start, c - 1)); start = c; acc = 0; } acc += widths[c] * scale; }
            bands.Add((start, cols));
            var headerRow = o.RepeatHeader && Enumerable.Range(0, cols + 1).Any(c => s.Get(0, c)?.Bold == true);
            foreach (var (from, to) in bands)
            {
                PdfPage page = null!; XGraphics g = null!; var y = 0.0;
                void NewPage(bool withHeader)
                {
                    g?.Dispose(); page = pdf.AddPage(); page.Width = XUnit.FromPoint(pw); page.Height = XUnit.FromPoint(ph);
                    g = XGraphics.FromPdfPage(page); y = Margin;
                    if (o.AllSheets) { g.DrawString(s.Name, new XFont("Arial", 9, XFontStyleEx.Bold), XBrushes.DimGray, Margin, y - 6); }
                    if (withHeader && headerRow) { DrawRow(g, s, calc, 0, from, to, widths, scale, y); y += RowH * scale; }
                }
                NewPage(false);
                for (var r = 0; r <= rows; r++)
                {
                    if (s.HiddenRows.Contains(r)) continue;
                    if (y + RowH * scale > ph - Margin) NewPage(r > 0);
                    DrawRow(g, s, calc, r, from, to, widths, scale, y); y += RowH * scale;
                }
                if (from == 0)
                    foreach (var ch in s.Charts)
                    {
                        var h = 7 * Cm; if (y + h + 12 > ph - Margin) NewPage(false);
                        y += 12; DrawChart(g, ch, ChartData.From(s, calc, ch.Range), new XRect(Margin, y, Math.Min(avail, 16 * Cm), h)); y += h;
                    }
                g.Dispose();
            }
        }
        if (pdf.PageCount == 0) { var p = pdf.AddPage(); p.Width = XUnit.FromPoint(pw); p.Height = XUnit.FromPoint(ph); }
        var pages = pdf.PageCount;
        for (var i = 0; i < pages; i++)
            using (var pg = XGraphics.FromPdfPage(pdf.Pages[i], XGraphicsPdfPageOptions.Append))
                pg.DrawString($"{i + 1} / {pages}", new XFont("Arial", 8), XBrushes.Gray, new XRect(0, ph - 1 * Cm, pw, 12), XStringFormats.Center);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        pdf.Save(path);
        return pages;
    }

    static void DrawRow(XGraphics g, Sheet s, SheetCalc calc, int r, int from, int to, double[] widths, double scale, double y)
    {
        var x = Margin; var h = RowH * scale; var grid = new XPen(XColor.FromArgb(255, 200, 205, 214), 0.4);
        for (var c = from; c <= to; c++)
        {
            var w = widths[c] * scale; var cell = s.Get(r, c);
            if (cell?.Fill is { } fill && TryColor(fill, out var fc)) g.DrawRectangle(new XSolidBrush(fc), x, y, w, h);
            g.DrawRectangle(cell?.Border == true ? new XPen(XColors.Black, 0.8) : grid, x, y, w, h);
            if (cell is { Raw.Length: > 0 })
            {
                var v = calc.Value(s, r, c); var text = SheetFormat.Display(v, cell.Format);
                var font = new XFont("Arial", 9 * scale, cell.Bold ? XFontStyleEx.Bold : XFontStyleEx.Regular);
                while (text.Length > 1 && g.MeasureString(text, font).Width > w - 4) text = text[..^1];
                var right = v.IsNumber || v.Kind == CellValue.K.Bool;
                g.DrawString(text, font, v.IsError ? XBrushes.Firebrick : XBrushes.Black, new XRect(x + 2, y, w - 4, h), right ? XStringFormats.CenterRight : XStringFormats.CenterLeft);
            }
            x += w;
        }
    }

    static bool TryColor(string hex, out XColor c)
    {
        c = XColors.Transparent; var h = hex.TrimStart('#');
        if (h.Length == 6) h = "FF" + h;
        if (h.Length != 8 || !uint.TryParse(h, System.Globalization.NumberStyles.HexNumber, null, out var v)) return false;
        c = XColor.FromArgb((int)(v >> 24), (int)((v >> 16) & 255), (int)((v >> 8) & 255), (int)(v & 255)); return true;
    }

    /// <summary>Balken-, Linien- oder Kreisdiagramm in ein Rechteck zeichnen.</summary>
    public static void DrawChart(XGraphics g, SheetChart ch, ChartData d, XRect area)
    {
        g.DrawRectangle(new XPen(XColors.LightGray, 0.5), XBrushes.White, area);
        var titleFont = new XFont("Arial", 11, XFontStyleEx.Bold); var small = new XFont("Arial", 7.5);
        g.DrawString(ch.Title, titleFont, XBrushes.Black, new XRect(area.X, area.Y + 4, area.Width, 16), XStringFormats.TopCenter);
        var plot = new XRect(area.X + 40, area.Y + 26, area.Width - 56, area.Height - 52);
        XBrush Brush(int i) => new XSolidBrush(TryColor(ChartData.Palette[i % ChartData.Palette.Length], out var c) ? c : XColors.SteelBlue);
        if (d.Labels.Count == 0 || d.Series.Count == 0) return;
        if (ch.Kind == ChartKind.Kreis)
        {
            var vals = d.Series[0].Values.Select(v => Math.Max(0, v)).ToArray(); var sum = vals.Sum(); if (sum <= 0) return;
            var size = Math.Min(plot.Height, plot.Width / 2); var box = new XRect(plot.X, plot.Y, size, size); var a = -90.0;
            for (var i = 0; i < vals.Length; i++)
            {
                var sweep = vals[i] / sum * 360; if (sweep <= 0) continue;
                g.DrawPie(new XPen(XColors.White, 1), Brush(i), box, a, sweep); a += sweep;
                g.DrawRectangle(Brush(i), plot.X + size + 16, plot.Y + i * 13, 9, 9);
                g.DrawString($"{d.Labels[i]} ({vals[i] / sum:P0})", small, XBrushes.Black, plot.X + size + 30, plot.Y + i * 13 + 8);
            }
            return;
        }
        double min = d.Min, max = Math.Max(d.Max, min + 1e-9); var step = ChartData.NiceStep(max - min); max = Math.Ceiling(max / step) * step; min = Math.Floor(min / step) * step;
        double Y(double v) => plot.Bottom - (v - min) / (max - min) * plot.Height;
        for (var v = min; v <= max + step / 2; v += step)
        {
            g.DrawLine(new XPen(XColors.Gainsboro, 0.4), plot.Left, Y(v), plot.Right, Y(v));
            g.DrawString(ChartData.AxisLabel(v), small, XBrushes.DimGray, new XRect(area.X, Y(v) - 5, 36, 10), XStringFormats.CenterRight);
        }
        var n = d.Labels.Count; var slot = plot.Width / n;
        for (var i = 0; i < n; i++) g.DrawString(d.Labels[i], small, XBrushes.DimGray, new XRect(plot.X + i * slot, plot.Bottom + 2, slot, 10), XStringFormats.TopCenter);
        for (var k = 0; k < d.Series.Count; k++)
        {
            var vals = d.Series[k].Values;
            if (ch.Kind == ChartKind.Linie)
            {
                var pen = new XPen(TryColor(ChartData.Palette[k % ChartData.Palette.Length], out var c) ? c : XColors.SteelBlue, 1.6);
                for (var i = 1; i < n; i++) g.DrawLine(pen, plot.X + (i - 0.5) * slot, Y(vals[i - 1]), plot.X + (i + 0.5) * slot, Y(vals[i]));
            }
            else
            {
                var bw = slot * 0.7 / d.Series.Count;
                for (var i = 0; i < n; i++) { var x = plot.X + i * slot + slot * 0.15 + k * bw; var y0 = Y(Math.Max(0, min)); var y1 = Y(vals[i]); g.DrawRectangle(Brush(k), x, Math.Min(y0, y1), bw, Math.Abs(y0 - y1)); }
            }
        }
        if (d.Series.Count > 1)
            for (var k = 0; k < d.Series.Count; k++) { g.DrawRectangle(Brush(k), plot.X + k * 90, area.Bottom - 12, 8, 8); g.DrawString(d.Series[k].Name, small, XBrushes.Black, plot.X + k * 90 + 11, area.Bottom - 5); }
    }
}
