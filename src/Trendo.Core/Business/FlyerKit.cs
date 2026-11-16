using System.Globalization;
using PdfSharp.Drawing;
using PdfSharp.Drawing.Layout;
using PdfSharp.Pdf;

namespace Trendo.Core.Business;

/// <summary>STUDIO-FLYER-01: Endformat in Millimetern. <paramref name="Panels"/> &gt; 1 = Faltflyer (Falzlinien senkrecht, gleich breit).</summary>
public sealed record FlyerFormat(string Id, string Name, double WMm, double HMm, int Panels = 1, double SafeMm = 4);

/// <summary>Ein Element auf einer Seite (Maße in mm ab der linken oberen Ecke des Endformats). Arten: text, rect, ellipse, line, image, qr.</summary>
public sealed record FlyerItem(string Id, int Side, string Kind, double X, double Y, double W, double H, string Text = "", double SizePt = 12, string Color = "#111111",
    string Fill = "", bool Bold = false, string Align = "links", string Src = "");

public sealed record FlyerDoc(string Format, string Background, string BackBackground, IReadOnlyList<FlyerItem> Items, double BleedMm = 3);

public sealed record FlyerFinding(string Text, bool Error, string? ItemId = null);

/// <summary>Flyer und Visitenkarten wie in Canva/VistaPrint (Clean-Room): Formate, Beschnitt 3 mm, Sicherheitsabstand, Schnitt- und Falzmarken,
/// Prüfung (Bildauflösung, Text im Randbereich, Flächen bis in den Beschnitt, QR-Größe) und Druck-PDF mit Endformat- und Beschnittrahmen.</summary>
public static class FlyerKit
{
    public static readonly IReadOnlyList<FlyerFormat> Formats =
    [
        new("visitenkarte", "Visitenkarte 85 × 55 mm", 85, 55, 1, 3),
        new("dinlang", "DIN lang 99 × 210 mm", 99, 210),
        new("a6", "A6 105 × 148 mm", 105, 148),
        new("a5", "A5 148 × 210 mm", 148, 210, 1, 5),
        new("a4", "A4 210 × 297 mm", 210, 297, 1, 5),
        new("falt-a4", "Faltflyer A4 auf DIN lang (Wickelfalz, 6 Seiten)", 297, 210, 3, 5),
    ];
    public const double MmPt = 72 / 25.4;
    const double Slug = 10; // Rand für Schnittmarken

    public static FlyerFormat Format(string id) => Formats.FirstOrDefault(f => f.Id == id) ?? throw new ArgumentException("Unbekanntes Format: " + id);

    /// <summary>Druckdatenformat = Endformat + Beschnitt rundum, z. B. DIN lang 105 × 216 mm.</summary>
    public static (double W, double H) DataSize(FlyerDoc d) { var f = Format(d.Format); return (f.WMm + 2 * d.BleedMm, f.HMm + 2 * d.BleedMm); }

    public static List<double> Folds(FlyerFormat f) => [.. Enumerable.Range(1, f.Panels - 1).Select(i => Math.Round(f.WMm * i / f.Panels, 2))];

    /// <summary>Wirksame Auflösung eines Bildes auf dem Papier (dpi) aus Pixelbreite und Breite in mm.</summary>
    public static double EffectiveDpi(int px, double mm) => mm <= 0 ? 0 : Math.Round(px / (mm / 25.4));

    /// <summary>Prüfung vor dem Druck. <paramref name="imagePx"/> liefert die Pixelgröße eines Bildes (null = nicht lesbar).</summary>
    public static List<FlyerFinding> Check(FlyerDoc d, Func<string, (int W, int H)?>? imagePx = null)
    {
        var f = Format(d.Format); var res = new List<FlyerFinding>();
        if (d.BleedMm < 3) res.Add(new($"Beschnitt {d.BleedMm:0.#} mm – Druckereien verlangen meist 3 mm.", true));
        var s = f.SafeMm; var folds = Folds(f);
        foreach (var it in d.Items)
        {
            var name = it.Kind switch { "text" => $"Text „{Short(it.Text)}“", "image" => "Bild", "qr" => "QR-Code", "rect" => "Fläche", "ellipse" => "Kreis", _ => "Linie" };
            var side = d.Items.Any(x => x.Side == 1) ? (it.Side == 0 ? " (vorne)" : " (hinten)") : "";
            if (it.Kind is "text" or "qr")
            {
                if (it.X < s - 0.01 || it.Y < s - 0.01 || it.X + it.W > f.WMm - s + 0.01 || it.Y + it.H > f.HMm - s + 0.01)
                    res.Add(new($"{name}{side} liegt im Randbereich – mindestens {s:0.#} mm Abstand zum Schnitt, sonst kann er angeschnitten werden.", true, it.Id));
                foreach (var x in folds)
                    if (it.X < x - 2 && it.X + it.W > x + 2) res.Add(new($"{name}{side} läuft über die Falz bei {x:0.#} mm.", false, it.Id));
            }
            if (it.Kind == "text")
            {
                if (it.Text.Trim().Length == 0) res.Add(new($"Leeres Textfeld{side}.", false, it.Id));
                if (it.SizePt < 6) res.Add(new($"{name}{side}: {it.SizePt:0.#} pt ist zu klein zum Lesen (mindestens 6 pt).", true, it.Id));
                else if (TextOverflows(it)) res.Add(new($"{name}{side} passt vermutlich nicht in sein Feld – Feld größer oder Schrift kleiner.", false, it.Id));
            }
            if (it.Kind is "rect" or "image")
            {
                var right = it.X + it.W; var bottom = it.Y + it.H;
                var gapEdge = (it.X >= 0 && it.X < 1.5) || (it.Y >= 0 && it.Y < 1.5) || (right <= f.WMm && right > f.WMm - 1.5) || (bottom <= f.HMm && bottom > f.HMm - 1.5);
                if (gapEdge) res.Add(new($"{name}{side} endet knapp am Schnitt – randabfallend bis {d.BleedMm:0.#} mm in den Beschnitt ziehen, sonst bleibt ein weißer Blitzer.", false, it.Id));
            }
            if (it.Kind == "image")
            {
                if (it.Src.Trim().Length == 0 || imagePx?.Invoke(it.Src) is not { } px) res.Add(new($"Bild{side} fehlt oder lässt sich nicht lesen.", true, it.Id));
                else
                {
                    var dpi = Math.Min(EffectiveDpi(px.W, it.W), EffectiveDpi(px.H, it.H));
                    if (dpi < 150) res.Add(new($"Bild{side} hat nur {dpi:0} dpi – für den Druck mindestens 300 dpi, unter 150 dpi wirkt es pixelig.", true, it.Id));
                    else if (dpi < 300) res.Add(new($"Bild{side} hat {dpi:0} dpi – 300 dpi wären besser.", false, it.Id));
                }
            }
            if (it.Kind == "qr")
            {
                if (Math.Min(it.W, it.H) < 15) res.Add(new($"QR-Code{side} ist {Math.Min(it.W, it.H):0.#} mm groß – unter 15 mm scannen ihn viele Handys schlecht.", false, it.Id));
                try { QrMatrix.Encode(it.Text); }
                catch (InvalidOperationException) { res.Add(new($"Der QR-Inhalt ist zu lang ({it.Text.Length} Zeichen) – höchstens etwa 100 Zeichen, z. B. nur die Webadresse.", true, it.Id)); }
                if (it.Text.Trim().Length == 0) res.Add(new("Der QR-Code hat keinen Inhalt.", true, it.Id));
            }
            if (it.X + it.W < -d.BleedMm || it.Y + it.H < -d.BleedMm || it.X > f.WMm + d.BleedMm || it.Y > f.HMm + d.BleedMm)
                res.Add(new($"{name}{side} liegt ganz außerhalb und wird nicht gedruckt.", false, it.Id));
        }
        return res;
    }

    static string Short(string t) { t = (t ?? "").Replace("\n", " ").Trim(); return t.Length > 24 ? t[..24] + "…" : t; }

    /// <summary>Grobe Schätzung: Zeichen je Zeile aus Breite und Schriftgröße (Durchschnitt 0,5 em), Zeilenhöhe 1,25.</summary>
    public static bool TextOverflows(FlyerItem it)
    {
        var em = it.SizePt / MmPt; var perLine = Math.Max(1, (int)(it.W / (em * 0.5)));
        var lines = (it.Text ?? "").Replace("\r", "").Split('\n').Sum(l => Math.Max(1, (int)Math.Ceiling(l.Length / (double)perLine)));
        return lines * em * 1.25 > it.H + 0.5;
    }

    static XColor Col(string hex, XColor fallback)
    {
        var h = (hex ?? "").TrimStart('#'); if (h.Length != 6 || !uint.TryParse(h, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var v)) return fallback;
        return XColor.FromArgb(255, (int)(v >> 16) & 255, (int)(v >> 8) & 255, (int)v & 255);
    }

    /// <summary>Druck-PDF: je Seite (vorne, hinten falls belegt) Endformat + Beschnitt; mit Schnittmarken zusätzlich 10 mm Rand mit Marken und Falzmarken.
    /// TrimBox und BleedBox sind gesetzt. Gibt die Seitenzahl zurück.</summary>
    public static int Pdf(FlyerDoc d, string path, bool cropMarks, Func<string, byte[]?>? readImage = null)
    {
        Sys.PdfTools.EnsureFonts();
        var f = Format(d.Format); var b = d.BleedMm; var m = cropMarks ? Slug : 0;
        using var pdf = new PdfDocument(); pdf.Info.Title = f.Name;
        var sides = d.Items.Any(i => i.Side == 1) || Col(d.BackBackground, XColors.White) != XColors.White ? 2 : 1;
        for (var side = 0; side < sides; side++)
        {
            var page = pdf.AddPage();
            page.Width = XUnit.FromMillimeter(f.WMm + 2 * (b + m)); page.Height = XUnit.FromMillimeter(f.HMm + 2 * (b + m));
            double ox = (b + m) * MmPt, oy = (b + m) * MmPt; // Ursprung = Endformat oben links
            page.TrimBox = new PdfRectangle(new XRect(ox, oy, f.WMm * MmPt, f.HMm * MmPt));
            page.BleedBox = new PdfRectangle(new XRect(m * MmPt, m * MmPt, (f.WMm + 2 * b) * MmPt, (f.HMm + 2 * b) * MmPt));
            using var g = XGraphics.FromPdfPage(page);
            g.DrawRectangle(new XSolidBrush(Col(side == 0 ? d.Background : d.BackBackground, XColors.White)), new XRect(m * MmPt, m * MmPt, (f.WMm + 2 * b) * MmPt, (f.HMm + 2 * b) * MmPt));
            var st = g.Save();
            g.IntersectClip(new XRect(m * MmPt, m * MmPt, (f.WMm + 2 * b) * MmPt, (f.HMm + 2 * b) * MmPt));
            foreach (var it in d.Items.Where(i => i.Side == side)) DrawItem(g, it, ox, oy, readImage);
            g.Restore(st);
            if (cropMarks)
            {
                var pen = new XPen(XColors.Black, 0.25);
                double L = ox, T = oy, R = ox + f.WMm * MmPt, B = oy + f.HMm * MmPt, gap = (b + 1) * MmPt, len = 6 * MmPt;
                foreach (var x in new[] { L, R }) { g.DrawLine(pen, x, T - gap, x, T - gap - len); g.DrawLine(pen, x, B + gap, x, B + gap + len); }
                foreach (var y in new[] { T, B }) { g.DrawLine(pen, L - gap, y, L - gap - len, y); g.DrawLine(pen, R + gap, y, R + gap + len, y); }
                var dash = new XPen(XColors.Black, 0.25) { DashStyle = XDashStyle.Dash };
                foreach (var fx in Folds(f).Select(x => ox + (side == 0 ? x : f.WMm - x) * MmPt)) { g.DrawLine(dash, fx, T - gap, fx, T - gap - len); g.DrawLine(dash, fx, B + gap, fx, B + gap + len); }
            }
        }
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        pdf.Save(path);
        return sides;
    }

    static void DrawItem(XGraphics g, FlyerItem it, double ox, double oy, Func<string, byte[]?>? readImage)
    {
        var r = new XRect(ox + it.X * MmPt, oy + it.Y * MmPt, Math.Max(0.1, it.W * MmPt), Math.Max(0.1, it.H * MmPt));
        switch (it.Kind)
        {
            case "rect": g.DrawRectangle(new XSolidBrush(Col(it.Fill.Length > 0 ? it.Fill : it.Color, XColors.LightGray)), r); break;
            case "ellipse": g.DrawEllipse(new XSolidBrush(Col(it.Fill.Length > 0 ? it.Fill : it.Color, XColors.LightGray)), r); break;
            case "line": g.DrawLine(new XPen(Col(it.Color, XColors.Black), Math.Max(0.25, it.H * MmPt)), r.Left, r.Top, r.Right, r.Top); break;
            case "text":
            {
                if (it.Fill.Length > 0) g.DrawRectangle(new XSolidBrush(Col(it.Fill, XColors.White)), r);
                var font = new XFont("Arial", it.SizePt, it.Bold ? XFontStyleEx.Bold : XFontStyleEx.Regular);
                var tf = new XTextFormatter(g) { Alignment = it.Align switch { "mitte" => XParagraphAlignment.Center, "rechts" => XParagraphAlignment.Right, _ => XParagraphAlignment.Left } };
                tf.DrawString(it.Text ?? "", font, new XSolidBrush(Col(it.Color, XColors.Black)), r, XStringFormats.TopLeft);
                break;
            }
            case "image":
            {
                var data = it.Src.Length > 0 ? readImage?.Invoke(it.Src) : null;
                if (data is null) { g.DrawRectangle(XPens.LightGray, r); break; }
                try
                {
                    using var ms = new MemoryStream(data); using var img = XImage.FromStream(ms);
                    // füllend einpassen, Überstand abschneiden (wie „Bild füllen“)
                    var s = Math.Max(r.Width / img.PointWidth, r.Height / img.PointHeight);
                    var iw = img.PointWidth * s; var ih = img.PointHeight * s;
                    var st = g.Save(); g.IntersectClip(r); g.DrawImage(img, r.X + (r.Width - iw) / 2, r.Y + (r.Height - ih) / 2, iw, ih); g.Restore(st);
                }
                catch (Exception e) when (e is InvalidOperationException or ArgumentException or NotSupportedException) { g.DrawRectangle(XPens.LightGray, r); }
                break;
            }
            case "qr":
            {
                bool[,] q;
                try { q = QrMatrix.Encode(it.Text ?? ""); } catch (InvalidOperationException) { g.DrawRectangle(XPens.Red, r); break; }
                var n = q.GetLength(0); var side = Math.Min(r.Width, r.Height); var cell = side / (n + 8); // 4 Module Ruhezone
                g.DrawRectangle(XBrushes.White, new XRect(r.X, r.Y, side, side));
                var brush = new XSolidBrush(Col(it.Color, XColors.Black));
                for (var y = 0; y < n; y++) for (var x = 0; x < n; x++) if (q[y, x]) g.DrawRectangle(brush, new XRect(r.X + (x + 4) * cell, r.Y + (y + 4) * cell, cell + 0.01, cell + 0.01));
                break;
            }
        }
    }

    /// <summary>Inhalt für einen Kontakt-QR im kurzen MECARD-Format (passt in einen kleinen Code; Handy-Kameras lesen es als Kontakt).</summary>
    public static string ContactQr(string name, string phone, string email, string web)
    {
        static string Esc(string v) => v.Trim().Replace("\\", "\\\\").Replace(";", "\\;").Replace(":", "\\:").Replace(",", "\\,");
        var sb = new System.Text.StringBuilder("MECARD:N:").Append(Esc(name)).Append(';');
        if (phone.Trim().Length > 0) sb.Append("TEL:").Append(Esc(phone)).Append(';');
        if (email.Trim().Length > 0) sb.Append("EMAIL:").Append(Esc(email)).Append(';');
        if (web.Trim().Length > 0) sb.Append("URL:").Append(Esc(web)).Append(';');
        return sb.Append(';').ToString();
    }
}
