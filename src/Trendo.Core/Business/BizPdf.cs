using PdfSharp.Drawing;
using PdfSharp.Pdf;

namespace Trendo.Core.Business;

/// <summary>Kleine Satz-Hilfe für die Business-Studios auf PDFsharp (MIT, schon im Core wie bei Brief, Lebenslauf und Karten): A4, Maße in mm,
/// Text mit Umbruch, Tabellen mit Seitenumbruch und wiederholtem Kopf, Seitenzahlen. Kein zweites Dokumentsystem – nur der Satz für Tabellenbelege.</summary>
public sealed class BizPdf : IDisposable
{
    const double Mm = 72 / 25.4;
    public static readonly XColor Grey = XColor.FromArgb(0x5C, 0x6B, 0x7A), Line = XColor.FromArgb(0xD0, 0xD5, 0xDB), Dark = XColor.FromArgb(0x11, 0x18, 0x27);
    readonly PdfDocument _doc = new();
    readonly List<XGraphics> _pages = [];
    readonly XGraphics _measure = XGraphics.CreateMeasureContext(new XSize(595, 842), XGraphicsUnit.Point, XPageDirection.Downwards);
    public XGraphics G { get; private set; } = null!;
    public double Y { get; set; }
    public double Left { get; set; } = 20;
    public double Right { get; set; } = 190;
    public double Top { get; set; } = 20;
    public double Bottom { get; set; } = 270;
    public XColor Accent { get; set; } = XColor.FromArgb(0x8A, 0x6A, 0x12);
    public string Footer { get; set; } = "";
    public Action<BizPdf>? OnNewPage { get; set; }

    public BizPdf(string title, string author)
    {
        Sys.PdfTools.EnsureFonts();
        _doc.Info.Title = title; _doc.Info.Author = author;
        NewPage();
    }

    public static XFont F(double pt, bool bold = false)
    {
        try { return new XFont("Arial", pt, bold ? XFontStyleEx.Bold : XFontStyleEx.Regular); }
        catch (Exception e) when (e is InvalidOperationException or ArgumentException) { return new XFont("Arial", pt); }
    }
    public static double LineH(double pt) => pt * 0.3528 * 1.25;

    public void NewPage()
    {
        var p = _doc.AddPage(); p.Width = XUnit.FromMillimeter(210); p.Height = XUnit.FromMillimeter(297);
        G = XGraphics.FromPdfPage(p); _pages.Add(G); Y = Top;
        OnNewPage?.Invoke(this);
    }

    public void Need(double mm) { if (Y + mm > Bottom) NewPage(); }

    public List<string> Wrap(string text, XFont f, double widthMm)
    {
        var res = new List<string>(); var max = widthMm * Mm;
        foreach (var para in (text ?? "").Replace("\r", "").Split('\n'))
        {
            if (para.Length == 0) { res.Add(""); continue; }
            var line = "";
            foreach (var word in para.Split(' '))
            {
                var test = line.Length == 0 ? word : line + " " + word;
                if (_measure.MeasureString(test, f).Width <= max || line.Length == 0) { line = test; continue; }
                res.Add(line); line = word;
            }
            res.Add(line);
        }
        return res;
    }

    /// <summary>Text an fester Stelle (ändert <see cref="Y"/> nicht).</summary>
    public void At(string text, double x, double y, double w, double pt = 10, bool bold = false, XColor? c = null, XStringFormat? fmt = null) =>
        G.DrawString(text ?? "", F(pt, bold), new XSolidBrush(c ?? Dark), new XRect(x * Mm, y * Mm, w * Mm, LineH(pt) * Mm * 1.5), fmt ?? XStringFormats.TopLeft);

    /// <summary>Fließtext mit Umbruch ab <see cref="Y"/>.</summary>
    public void Write(string text, double pt = 10, bool bold = false, XColor? c = null, double? x = null, double? w = null)
    {
        var xx = x ?? Left; var ww = w ?? Right - Left;
        foreach (var l in Wrap(text, F(pt, bold), ww)) { Need(LineH(pt)); At(l, xx, Y, ww, pt, bold, c); Y += LineH(pt); }
    }

    public void Rule(XColor? c = null, double width = 0.25) { G.DrawLine(new XPen(c ?? Line, width * Mm), Left * Mm, Y * Mm, Right * Mm, Y * Mm); }
    public void Box(double x, double y, double w, double h, XColor c) => G.DrawRectangle(new XSolidBrush(c), x * Mm, y * Mm, w * Mm, h * Mm);

    /// <summary>Tabelle: Spaltenbreiten in mm (Summe ≤ Breite), Ausrichtung je Spalte ('l' oder 'r'). Kopf wird auf jeder Seite wiederholt.</summary>
    public void Table(string[] head, double[] widths, string align, IEnumerable<string[]> rows, double pt = 9, Func<int, bool>? boldRow = null)
    {
        void Head()
        {
            Need(LineH(pt) + 3);
            Box(Left, Y - 0.5, Right - Left, LineH(pt) + 1.5, XColor.FromArgb(0xEE, 0xF2, 0xF7));
            Cells(head, true); Y += LineH(pt) + 1.5;
        }
        void Cells(string[] cells, bool bold, int lines = 1)
        {
            double x = Left;
            for (var i = 0; i < widths.Length; i++)
            {
                var txt = i < cells.Length ? cells[i] : "";
                var wrapped = align[i] == 'l' ? Wrap(txt, F(pt, bold), widths[i] - 1.5) : [txt];
                for (var k = 0; k < wrapped.Count; k++)
                    At(wrapped[k], x + 0.8, Y + k * LineH(pt), widths[i] - 1.6, pt, bold, null, align[i] == 'r' ? XStringFormats.TopRight : XStringFormats.TopLeft);
                x += widths[i];
            }
        }
        Head(); var idx = 0;
        foreach (var r in rows)
        {
            var bold = boldRow?.Invoke(idx) ?? false;
            var n = Math.Max(1, widths.Select((w, i) => align[i] == 'l' && i < r.Length ? Wrap(r[i], F(pt, bold), w - 1.5).Count : 1).Max());
            var h = n * LineH(pt) + 1.5;
            if (Y + h > Bottom) { NewPage(); Head(); }
            Cells(r, bold, n); Y += h;
            G.DrawLine(new XPen(Line, 0.15 * Mm), Left * Mm, (Y - 0.6) * Mm, Right * Mm, (Y - 0.6) * Mm);
            idx++;
        }
    }

    /// <summary>Fußzeile und Seitenzahlen setzen und das Dokument als Bytes liefern. <paramref name="prepare"/> läuft direkt vor dem Schreiben (z. B. PDF/A einschalten).</summary>
    public byte[] ToBytes(Action<PdfDocument>? prepare = null)
    {
        for (var i = 0; i < _pages.Count; i++)
        {
            var g = _pages[i];
            if (Footer.Length > 0) foreach (var (l, k) in Wrap(Footer, F(7), 170).Select((l, k) => (l, k))) g.DrawString(l, F(7), new XSolidBrush(Grey), new XRect(20 * Mm, (279 + k * LineH(7)) * Mm, 170 * Mm, 4 * Mm), XStringFormats.TopCenter);
            if (_pages.Count > 1) g.DrawString($"Seite {i + 1} von {_pages.Count}", F(7.5), new XSolidBrush(Grey), new XRect(150 * Mm, 290 * Mm, 40 * Mm, 4 * Mm), XStringFormats.TopRight);
        }
        foreach (var g in _pages) g.Dispose();
        _pages.Clear();
        Pages = _doc.PageCount;
        prepare?.Invoke(_doc);
        using var ms = new MemoryStream(); _doc.Save(ms, false);
        return ms.ToArray();
    }
    public int Pages { get; private set; }

    /// <summary>Speichern (absturzsicher über <see cref="AtomicFile"/>); gibt die Seitenzahl zurück.</summary>
    public int Save(string path) { AtomicFile.WriteAllBytes(path, ToBytes()); return Pages; }

    public void Dispose() { foreach (var g in _pages) g.Dispose(); _doc.Dispose(); }
}

