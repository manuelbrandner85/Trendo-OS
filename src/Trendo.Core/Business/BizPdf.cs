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
    public XColor Accent { get; set; } = XColor.FromArgb(0x1F, 0x5F, 0xA8);
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

    /// <summary>Speichern (atomar über .tmp); gibt die Seitenzahl zurück.</summary>
    public int Save(string path)
    {
        for (var i = 0; i < _pages.Count; i++)
        {
            var g = _pages[i];
            if (Footer.Length > 0) foreach (var (l, k) in Wrap(Footer, F(7), 170).Select((l, k) => (l, k))) g.DrawString(l, F(7), new XSolidBrush(Grey), new XRect(20 * Mm, (279 + k * LineH(7)) * Mm, 170 * Mm, 4 * Mm), XStringFormats.TopCenter);
            if (_pages.Count > 1) g.DrawString($"Seite {i + 1} von {_pages.Count}", F(7.5), new XSolidBrush(Grey), new XRect(150 * Mm, 290 * Mm, 40 * Mm, 4 * Mm), XStringFormats.TopRight);
        }
        foreach (var g in _pages) g.Dispose();
        _pages.Clear();
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var n = _doc.PageCount;
        var tmp = path + ".tmp"; _doc.Save(tmp); SafeFile.Move(tmp, path);
        return n;
    }

    public void Dispose() { foreach (var g in _pages) g.Dispose(); _doc.Dispose(); }
}

/// <summary>Angebot, Auftragsbestätigung, Rechnung oder Stornorechnung als PDF im Aufbau nach DIN 5008 (Anschriftfeld, Infoblock, Positionstabelle, Summen je Steuersatz, Zahlungsbedingungen, Fußzeile mit Bank und Steuernummer).</summary>
public static class RechnungPdf
{
    public static int Write(RechnungsBuch b, Vorgang v, string path)
    {
        var f = b.Firma; var name = Rechnungen.Name(v.Art); var s = Rechnungen.Summe(v);
        using var pdf = new BizPdf($"{name} {(v.Entwurf ? "(Entwurf)" : v.Nummer)}", f.Name)
        {
            Footer = string.Join("  ·  ", new[] { f.Name, f.Strasse, f.PlzOrt, f.Telefon, f.Email }.Where(x => x.Trim().Length > 0)) + "\n" +
                     string.Join("  ·  ", new[] { f.SteuerNr.Trim().Length > 0 ? "Steuernummer " + f.SteuerNr : "", f.UstId.Trim().Length > 0 ? "USt-IdNr. " + f.UstId : "", f.Bank, f.Iban.Trim().Length > 0 ? "IBAN " + f.Iban : "" }.Where(x => x.Length > 0)),
            Bottom = 272,
        };
        pdf.At(f.Name, 20, 15, 120, 16, true, pdf.Accent);
        pdf.At(string.Join(" · ", new[] { f.Strasse, f.PlzOrt }.Where(x => x.Trim().Length > 0)), 20, 23, 120, 9, false, BizPdf.Grey);
        pdf.At(string.Join(" · ", new[] { f.Name, f.Strasse, f.PlzOrt }.Where(x => x.Trim().Length > 0)), 25, 47, 85, 7, false, BizPdf.Grey);
        var y = 52.0; foreach (var l in v.KundeAnschrift.Split('\n')) { pdf.At(l, 25, y, 85, 10.5); y += BizPdf.LineH(10.5); }
        var info = new List<(string, string)> { ($"{name}snummer", v.Entwurf ? "Entwurf" : v.Nummer), ("Datum", BizJson.TextOf(v.Datum)) };
        if (v.Leistung.Trim().Length > 0) info.Add(("Leistungsdatum", v.Leistung.Trim()));
        if (v.KundeNummer.Trim().Length > 0) info.Add(("Kundennummer", v.KundeNummer));
        if (v.Herkunft.Length > 0) info.Add((v.Art == "storno" ? "Zu Rechnung" : "Bezug", v.Herkunft));
        if (v.Art == "angebot" && BizJson.Datum(v.Datum) is { } ad) info.Add(("Gültig bis", BizJson.Text(ad.AddDays(v.GueltigTage))));
        y = 50; foreach (var (k, val) in info) { pdf.At(k, 125, y, 32, 9, false, BizPdf.Grey); pdf.At(val, 157, y, 33, 9, true); y += BizPdf.LineH(9) + 0.4; }
        if (v.Entwurf) pdf.At("ENTWURF – nicht versenden", 125, 40, 65, 9, true, XColor.FromArgb(0xB4, 0x53, 0x09));

        pdf.Y = 100;
        pdf.Write(name + (v.Entwurf ? "" : " " + v.Nummer), 14, true, pdf.Accent); pdf.Y += 3;
        pdf.Write(Mahnwesen.Anrede(v.KundeAnschrift), 10); pdf.Y += 1.5;
        if (v.Kopftext.Trim().Length > 0) { pdf.Write(v.Kopftext.Trim(), 10); pdf.Y += 3; }

        var rabatt = v.Positionen.Any(p => p.Rabatt != 0);
        var ust = !v.Kleinunternehmer;
        var head = new List<string> { "Pos.", "Bezeichnung", "Menge", "Einheit", "Einzelpreis" }; var w = new List<double> { 10, 0, 16, 16, 22 }; var al = "llrlr";
        if (rabatt) { head.Add("Rabatt"); w.Add(14); al += "r"; }
        if (ust) { head.Add("USt"); w.Add(11); al += "r"; }
        head.Add("Gesamt"); w.Add(24); al += "r";
        w[1] = 170 - w.Sum();
        var rows = v.Positionen.Select((p, i) =>
        {
            var r = new List<string> { (i + 1).ToString(), p.Text, BizJson.Num(p.Menge), p.Einheit, BizJson.Euro(p.Preis) };
            if (rabatt) r.Add(p.Rabatt == 0 ? "" : BizJson.Num(p.Rabatt) + " %");
            if (ust) r.Add(BizJson.Num(p.Ust) + " %");
            r.Add(BizJson.Euro(Rechnungen.PosNetto(p))); return r.ToArray();
        });
        pdf.Table([.. head], [.. w], al, rows);
        pdf.Y += 2; pdf.Need(30);
        void Sum(string label, string val, bool bold = false) { pdf.Need(6); pdf.At(label, 110, pdf.Y, 50, 10, bold); pdf.At(val, 160, pdf.Y, 30, 10, bold, null, XStringFormats.TopRight); pdf.Y += BizPdf.LineH(10) + 0.8; }
        Sum("Summe netto", BizJson.Euro(s.Netto));
        if (ust) foreach (var g in s.JeSatz) Sum(g.Satz == 0 ? $"0 % USt auf {BizJson.Euro(g.Netto)}" : $"zzgl. {BizJson.Num(g.Satz)} % USt auf {BizJson.Euro(g.Netto)}", BizJson.Euro(g.Steuer));
        pdf.G.DrawLine(new PdfSharp.Drawing.XPen(BizPdf.Dark, 0.3 * 72 / 25.4), 110 * 72 / 25.4, pdf.Y * 72 / 25.4, 190 * 72 / 25.4, pdf.Y * 72 / 25.4); pdf.Y += 1;
        Sum(v.Art == "angebot" ? "Angebotssumme" : "Gesamtbetrag", BizJson.Euro(s.Brutto), true);
        if (v.Bezahlt > 0 && v.Art == "rechnung") { Sum("Bereits bezahlt", "− " + BizJson.Euro(v.Bezahlt)); Sum("Noch offen", BizJson.Euro(Rechnungen.Offen(v)), true); }
        pdf.Y += 4;
        if (v.Kleinunternehmer) pdf.Write(Rechnungen.KleinunternehmerHinweis, 9.5);
        else if (v.Positionen.Any(p => p.Ust == 0) && v.Befreiung.Trim().Length > 0) pdf.Write(v.Befreiung.Trim(), 9.5);
        if (v.Art == "rechnung" && Rechnungen.Faellig(v) is { } due)
            pdf.Write($"Bitte überweisen Sie den Betrag bis zum {BizJson.Text(due)} ohne Abzug" + (f.Iban.Trim().Length > 0 ? $" auf das Konto IBAN {f.Iban}" + (f.Bank.Trim().Length > 0 ? $" ({f.Bank})" : "") : "") + $" unter Angabe der Rechnungsnummer {v.Nummer}.", 9.5);
        if (v.Art == "angebot") pdf.Write($"Dieses Angebot gilt {v.GueltigTage} Tage. Wir freuen uns auf Ihren Auftrag.", 9.5);
        if (v.Fusstext.Trim().Length > 0) { pdf.Y += 2; pdf.Write(v.Fusstext.Trim(), 9.5); }
        pdf.Y += 4; pdf.Write("Mit freundlichen Grüßen", 10); pdf.Write(f.Name, 10);
        return pdf.Save(path);
    }
}
