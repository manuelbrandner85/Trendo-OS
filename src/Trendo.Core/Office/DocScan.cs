using System.Globalization;
using System.Text;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using Trendo.Core.Kreativ;

namespace Trendo.Core.Office;

/// <summary>STUDIO-DOCSCAN-01 „Dokumente scannen“: Fotos von Blättern zu sauberen Scan-Seiten – Blatt finden, entzerren, Modus Dokument/Graustufen/
/// Foto/Whiteboard, drehen, zuschneiden, mehrseitige PDF. Clean-Room nach dem Verhalten von Microsoft Lens, Adobe Scan und NAPS2 (GPL – nur Vorbild).
/// Entzerren nutzt die vorhandene Homographie <see cref="VisionPlus.Homography"/>; Bilder liegen als <see cref="Pixmap"/> (RGBA) vor.</summary>
public static class DocScan
{
    public static readonly IReadOnlyList<(string Id, string Name, string Hint)> Modes =
    [
        ("dokument", "Dokument", "Schwarz auf Weiß – Schatten und Papierfarbe verschwinden"),
        ("grau", "Graustufen", "Ohne Farbe, Kontrast angehoben"),
        ("foto", "Foto", "Farben bleiben, nur Helligkeit und Kontrast angepasst"),
        ("whiteboard", "Whiteboard", "Tafel wird weiß, Stiftfarben kräftig"),
        ("original", "Original", "Unverändert (nur entzerrt)"),
    ];
    public static string ModeName(string id) => Modes.FirstOrDefault(m => m.Id == id).Name ?? "Dokument";

    /// <summary>Ecken im Uhrzeigersinn ab oben links (Bildpunkte im Originalbild).</summary>
    public static (double X, double Y)[] FullFrame(int w, int h) => [(0, 0), (w - 1, 0), (w - 1, h - 1), (0, h - 1)];

    /// <summary>Vier Punkte in die Reihenfolge oben links, oben rechts, unten rechts, unten links bringen.</summary>
    public static (double X, double Y)[] Order(IReadOnlyList<(double X, double Y)> p)
    {
        if (p.Count != 4) throw new ArgumentException("Für das Entzerren braucht es genau 4 Ecken.");
        var tl = p.MinBy(q => q.X + q.Y); var br = p.MaxBy(q => q.X + q.Y);
        var rest = p.Where(q => q != tl && q != br).ToList();
        if (rest.Count != 2) rest = [.. p.Except([tl, br]).Concat(p).Take(2)];
        var tr = rest.MaxBy(q => q.X - q.Y); var bl = rest.First(q => q != tr);
        return [tl, tr, br, bl];
    }

    static byte Gray(byte[] px, int i) => (byte)((px[i] * 299 + px[i + 1] * 587 + px[i + 2] * 114) / 1000);

    /// <summary>Otsu-Schwelle eines Graustufen-Histogramms.</summary>
    public static int Otsu(int[] hist)
    {
        long total = hist.Sum(x => (long)x), sum = 0; for (var i = 0; i < 256; i++) sum += (long)i * hist[i];
        long sumB = 0, wB = 0; double best = -1; var t = 128;
        for (var i = 0; i < 256; i++)
        {
            wB += hist[i]; if (wB == 0) continue; var wF = total - wB; if (wF == 0) break;
            sumB += (long)i * hist[i]; double mB = sumB / (double)wB, mF = (sum - sumB) / (double)wF;
            var between = wB * (double)wF * (mB - mF) * (mB - mF);
            if (between > best) { best = between; t = i; }
        }
        return t;
    }

    /// <summary>Blatt im Foto finden: Bild verkleinern, hell/dunkel nach Otsu trennen, größte zusammenhängende Fläche, die nicht der Rand (Tisch) ist,
    /// Ecken aus den Extrempunkten. Ohne klares Blatt das ganze Bild (Sicherheit 0). <paramref name="confidence"/>: Anteil der Fläche am Viereck (0–1).</summary>
    public static (double X, double Y)[] FindPage(Pixmap img, out double confidence)
    {
        confidence = 0;
        var s = Math.Min(1.0, 360.0 / Math.Max(img.W, img.H));
        var small = s < 1 ? img.Resize(Math.Max(8, (int)(img.W * s)), Math.Max(8, (int)(img.H * s))) : img;
        int w = small.W, h = small.H; var g = new byte[w * h]; var hist = new int[256];
        for (var i = 0; i < g.Length; i++) { g[i] = Gray(small.Px, i * 4); hist[g[i]]++; }
        var t = Otsu(hist);
        (double X, double Y)[]? best = null; var bestScore = 0.0;
        foreach (var bright in new[] { true, false })
        {
            var seen = new bool[w * h]; var stack = new Stack<int>();
            for (var start = 0; start < g.Length; start++)
            {
                if (seen[start] || (g[start] > t) != bright) continue;
                var pts = new List<int>(); stack.Push(start); seen[start] = true; var borders = 0; bool L = false, R = false, T = false, B = false;
                while (stack.Count > 0)
                {
                    var k = stack.Pop(); pts.Add(k); int x = k % w, y = k / w;
                    if (x == 0) L = true; if (x == w - 1) R = true; if (y == 0) T = true; if (y == h - 1) B = true;
                    void Try(int nx, int ny) { if (nx < 0 || ny < 0 || nx >= w || ny >= h) return; var n = ny * w + nx; if (seen[n] || (g[n] > t) != bright) return; seen[n] = true; stack.Push(n); }
                    Try(x + 1, y); Try(x - 1, y); Try(x, y + 1); Try(x, y - 1);
                }
                borders = (L ? 1 : 0) + (R ? 1 : 0) + (T ? 1 : 0) + (B ? 1 : 0);
                var area = pts.Count / (double)g.Length;
                if (area < 0.08 || area > 0.985 || borders >= 3) continue;
                (double X, double Y) P(int k) => (k % w, k / w);
                var tl = P(pts.MinBy(k => k % w + k / w)); var br = P(pts.MaxBy(k => k % w + k / w));
                var tr = P(pts.MaxBy(k => k % w - k / w)); var bl = P(pts.MaxBy(k => k / w - k % w));
                var quad = QuadArea([tl, tr, br, bl]); if (quad <= 0) continue;
                var fill = Math.Min(1, pts.Count / quad); var score = area * fill * (borders == 0 ? 1 : 0.7);
                if (fill > 0.8 && score > bestScore) { bestScore = score; best = [tl, tr, br, bl]; confidence = Math.Round(fill * (borders == 0 ? 1 : 0.8), 2); }
            }
        }
        if (best is null) return FullFrame(img.W, img.H);
        // zurück auf Originalgröße, Punkte eine halbe Zelle nach außen (Kante liegt zwischen den Rasterpunkten)
        var cx = best.Average(p => p.X); var cy = best.Average(p => p.Y);
        return [.. best.Select(p => (Math.Clamp((p.X + Math.Sign(p.X - cx) * 0.5) / s, 0, img.W - 1), Math.Clamp((p.Y + Math.Sign(p.Y - cy) * 0.5) / s, 0, img.H - 1)))];
    }

    public static double QuadArea(IReadOnlyList<(double X, double Y)> q)
    {
        double a = 0; for (var i = 0; i < q.Count; i++) { var (x1, y1) = q[i]; var (x2, y2) = q[(i + 1) % q.Count]; a += x1 * y2 - x2 * y1; }
        return Math.Abs(a) / 2;
    }

    static double Dist((double X, double Y) a, (double X, double Y) b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

    /// <summary>Zielgröße nach dem Entzerren: mittlere Kantenlängen; liegt das Verhältnis nahe an DIN A (1 : 1,414), wird es genau A-Format. Längste Seite höchstens <paramref name="maxSide"/>.</summary>
    public static (int W, int H, bool DinA) TargetSize(IReadOnlyList<(double X, double Y)> c, int maxSide = 3508)
    {
        var o = Order(c);
        double w = (Dist(o[0], o[1]) + Dist(o[3], o[2])) / 2, h = (Dist(o[0], o[3]) + Dist(o[1], o[2])) / 2;
        w = Math.Max(w, 8); h = Math.Max(h, 8);
        var ratio = Math.Max(w, h) / Math.Min(w, h); var din = Math.Abs(ratio - Math.Sqrt(2)) < 0.09;
        if (din) { if (h >= w) h = w * Math.Sqrt(2); else w = h * Math.Sqrt(2); }
        var k = Math.Min(1.0, maxSide / Math.Max(w, h));
        return ((int)Math.Round(w * k), (int)Math.Round(h * k), din);
    }

    /// <summary>Perspektive entzerren: das Viereck <paramref name="corners"/> im Foto wird zu einem geraden Rechteck <paramref name="outW"/> × <paramref name="outH"/> (bilinear).</summary>
    public static Pixmap Warp(Pixmap src, IReadOnlyList<(double X, double Y)> corners, int outW, int outH)
    {
        var c = Order(corners);
        var h = VisionPlus.Homography([(0, 0), (outW - 1, 0), (outW - 1, outH - 1), (0, outH - 1)], c);
        var o = new Pixmap(outW, outH); var sp = src.Px; int sw = src.W, sh = src.H;
        for (var y = 0; y < outH; y++)
            for (var x = 0; x < outW; x++)
            {
                var d = h[6] * x + h[7] * y + h[8];
                var fx = (h[0] * x + h[1] * y + h[2]) / d; var fy = (h[3] * x + h[4] * y + h[5]) / d;
                fx = Math.Clamp(fx, 0, sw - 1); fy = Math.Clamp(fy, 0, sh - 1);
                int x0 = (int)fx, y0 = (int)fy, x1 = Math.Min(x0 + 1, sw - 1), y1 = Math.Min(y0 + 1, sh - 1); double ax = fx - x0, ay = fy - y0;
                var j = (y * outW + x) * 4;
                for (var ch = 0; ch < 4; ch++)
                {
                    double v = sp[(y0 * sw + x0) * 4 + ch] * (1 - ax) * (1 - ay) + sp[(y0 * sw + x1) * 4 + ch] * ax * (1 - ay) + sp[(y1 * sw + x0) * 4 + ch] * (1 - ax) * ay + sp[(y1 * sw + x1) * 4 + ch] * ax * ay;
                    o.Px[j + ch] = (byte)Math.Round(v);
                }
            }
        return o;
    }

    /// <summary>Um 90°-Schritte im Uhrzeigersinn drehen.</summary>
    public static Pixmap Rotate(Pixmap p, int quarters)
    {
        var q = ((quarters % 4) + 4) % 4; if (q == 0) return p.Clone();
        var o = q == 2 ? new Pixmap(p.W, p.H) : new Pixmap(p.H, p.W);
        for (var y = 0; y < p.H; y++)
            for (var x = 0; x < p.W; x++)
            {
                var (nx, ny) = q switch { 1 => (p.H - 1 - y, x), 2 => (p.W - 1 - x, p.H - 1 - y), _ => (y, p.W - 1 - x) };
                Buffer.BlockCopy(p.Px, (y * p.W + x) * 4, o.Px, (ny * o.W + nx) * 4, 4);
            }
        return o;
    }

    /// <summary>Zuschneiden; Ränder in Prozent (links, oben, rechts, unten, je 0–45).</summary>
    public static Pixmap Crop(Pixmap p, double left, double top, double right, double bottom)
    {
        double C(double v) => Math.Clamp(v, 0, 45) / 100;
        int x0 = (int)Math.Round(p.W * C(left)), y0 = (int)Math.Round(p.H * C(top)), x1 = p.W - (int)Math.Round(p.W * C(right)), y1 = p.H - (int)Math.Round(p.H * C(bottom));
        int w = Math.Max(1, x1 - x0), h = Math.Max(1, y1 - y0); var o = new Pixmap(w, h);
        for (var y = 0; y < h; y++) Buffer.BlockCopy(p.Px, ((y0 + y) * p.W + x0) * 4, o.Px, y * w * 4, w * 4);
        return o;
    }

    /// <summary>Summenbild für schnelle Mittelwerte in Fenstern.</summary>
    static long[] Integral(byte[] v, int w, int h)
    {
        var s = new long[(w + 1) * (h + 1)];
        for (var y = 0; y < h; y++) { long row = 0; for (var x = 0; x < w; x++) { row += v[y * w + x]; s[(y + 1) * (w + 1) + x + 1] = s[y * (w + 1) + x + 1] + row; } }
        return s;
    }
    static double Mean(long[] s, int w, int h, int x, int y, int r)
    {
        int x0 = Math.Max(0, x - r), y0 = Math.Max(0, y - r), x1 = Math.Min(w, x + r + 1), y1 = Math.Min(h, y + r + 1); var W1 = w + 1;
        return (s[y1 * W1 + x1] - s[y0 * W1 + x1] - s[y1 * W1 + x0] + s[y0 * W1 + x0]) / (double)((x1 - x0) * (y1 - y0));
    }

    static void Stretch(byte[] v, double lowPct = 0.01, double highPct = 0.99)
    {
        var hist = new int[256]; foreach (var b in v) hist[b]++;
        int lo = 0, hi = 255; long acc = 0;
        for (var i = 0; i < 256; i++) { acc += hist[i]; if (acc >= v.Length * lowPct) { lo = i; break; } }
        acc = 0; for (var i = 255; i >= 0; i--) { acc += hist[i]; if (acc >= v.Length * (1 - highPct)) { hi = i; break; } }
        if (hi - lo < 10) return;
        for (var i = 0; i < v.Length; i++) v[i] = (byte)Math.Clamp((v[i] - lo) * 255 / (hi - lo), 0, 255);
    }

    /// <summary>Scan-Modus anwenden. Dokument: adaptiver Schwellwert (Mittel im Fenster, Bradley-Verfahren) – Schatten und Papierfarbe verschwinden.
    /// Graustufen: ohne Farbe, Kontrast gestreckt. Foto: Kontrast je Kanal gestreckt. Whiteboard: Hintergrund herausgerechnet (Teilen durch die geschätzte Tafelhelligkeit), Farben kräftiger.</summary>
    public static Pixmap Filter(Pixmap p, string mode)
    {
        int w = p.W, h = p.H, n = w * h; var o = p.Clone();
        switch (mode)
        {
            case "dokument":
            {
                var g = new byte[n]; for (var i = 0; i < n; i++) g[i] = Gray(p.Px, i * 4);
                var s = Integral(g, w, h); var r = Math.Max(4, Math.Max(w, h) / 32);
                for (var y = 0; y < h; y++)
                    for (var x = 0; x < w; x++)
                    {
                        var i = y * w + x; var v = g[i] < Mean(s, w, h, x, y, r) * 0.85 ? (byte)0 : (byte)255;
                        o.Px[i * 4] = o.Px[i * 4 + 1] = o.Px[i * 4 + 2] = v; o.Px[i * 4 + 3] = 255;
                    }
                break;
            }
            case "grau":
            {
                var g = new byte[n]; for (var i = 0; i < n; i++) g[i] = Gray(p.Px, i * 4);
                Stretch(g);
                for (var i = 0; i < n; i++) { o.Px[i * 4] = o.Px[i * 4 + 1] = o.Px[i * 4 + 2] = g[i]; o.Px[i * 4 + 3] = 255; }
                break;
            }
            case "foto":
                for (var ch = 0; ch < 3; ch++)
                {
                    var v = new byte[n]; for (var i = 0; i < n; i++) v[i] = p.Px[i * 4 + ch];
                    Stretch(v, 0.005, 0.995); for (var i = 0; i < n; i++) { o.Px[i * 4 + ch] = v[i]; o.Px[i * 4 + 3] = 255; }
                }
                break;
            case "whiteboard":
            {
                var r = Math.Max(6, Math.Max(w, h) / 16);
                for (var ch = 0; ch < 3; ch++)
                {
                    var v = new byte[n]; for (var i = 0; i < n; i++) v[i] = p.Px[i * 4 + ch];
                    var s = Integral(v, w, h);
                    for (var y = 0; y < h; y++)
                        for (var x = 0; x < w; x++) { var i = y * w + x; var bg = Math.Max(1, Mean(s, w, h, x, y, r)); o.Px[i * 4 + ch] = (byte)Math.Clamp(v[i] / bg * 255 * 1.08, 0, 255); }
                }
                for (var i = 0; i < n; i++)
                {   // Sättigung anheben, fast Weißes ganz weiß
                    var j = i * 4; double R = o.Px[j], G = o.Px[j + 1], B = o.Px[j + 2]; var m = (R + G + B) / 3;
                    if (m > 235 && Math.Max(R, Math.Max(G, B)) - Math.Min(R, Math.Min(G, B)) < 25) { o.Px[j] = o.Px[j + 1] = o.Px[j + 2] = 255; }
                    else { o.Px[j] = (byte)Math.Clamp(m + (R - m) * 1.5, 0, 255); o.Px[j + 1] = (byte)Math.Clamp(m + (G - m) * 1.5, 0, 255); o.Px[j + 2] = (byte)Math.Clamp(m + (B - m) * 1.5, 0, 255); }
                    o.Px[j + 3] = 255;
                }
                break;
            }
            case "original": break;
            default: throw new ArgumentException($"Unbekannter Modus „{mode}“. Möglich: {string.Join(", ", Modes.Select(m => m.Id))}.");
        }
        return o;
    }

    /// <summary>Leere Seite (z. B. Rückseite): weniger als <paramref name="inkPct"/> % dunkle Bildpunkte nach dem Dokument-Modus.</summary>
    public static bool IsBlank(Pixmap p, double inkPct = 0.3)
    {
        var s = Math.Min(1.0, 600.0 / Math.Max(p.W, p.H)); var q = s < 1 ? p.Resize(Math.Max(1, (int)(p.W * s)), Math.Max(1, (int)(p.H * s))) : p;
        var d = Filter(q, "dokument"); var dark = 0; var n = d.W * d.H;
        for (var i = 0; i < n; i++) if (d.Px[i * 4] < 128) dark++;
        return dark * 100.0 / n < inkPct;
    }

    /// <summary>Eine Seite fertig machen: entzerren (Ecken), drehen, zuschneiden, Modus.</summary>
    public static Pixmap Process(Pixmap src, IReadOnlyList<(double X, double Y)>? corners, int rotate, string mode, (double L, double T, double R, double B)? crop = null, int maxSide = 2480)
    {
        var c = corners ?? FullFrame(src.W, src.H);
        var (w, h, _) = TargetSize(c, maxSide);
        var p = Warp(src, c, w, h);
        if (rotate % 4 != 0) p = Rotate(p, rotate);
        if (crop is { } cr && (cr.L > 0 || cr.T > 0 || cr.R > 0 || cr.B > 0)) p = Crop(p, cr.L, cr.T, cr.R, cr.B);
        return Filter(p, mode);
    }

    /// <summary>Mehrseitige PDF: je Bild eine Seite. <paramref name="pageSize"/> „a4“ (Hoch- oder Querformat nach Bild, Bild eingepasst) oder „auto“ (Seite = Bildgröße bei <paramref name="dpi"/>).</summary>
    public static int WritePdf(IReadOnlyList<Pixmap> pages, string path, string title, string pageSize = "a4", double dpi = 200)
    {
        if (pages.Count == 0) throw new InvalidOperationException("Es gibt noch keine Seite – erst Fotos oder Bilder hinzufügen.");
        System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(Sys.PdfTools).TypeHandle);
        using var pdf = new PdfDocument();
        pdf.Info.Title = title.Length > 0 ? title : "Scan"; pdf.Info.Subject = "Scan";
        foreach (var p in pages)
        {
            var page = pdf.AddPage(); double pw, ph;
            if (pageSize == "auto") { pw = p.W / dpi * 72; ph = p.H / dpi * 72; }
            else { var land = p.W > p.H; pw = land ? 842 : 595; ph = land ? 595 : 842; }
            page.Width = XUnit.FromPoint(pw); page.Height = XUnit.FromPoint(ph);
            using var g = XGraphics.FromPdfPage(page);
            using var ms = new MemoryStream(PngCodec.Encode(p, fast: false)); using var img = XImage.FromStream(ms);
            var k = Math.Min(pw / p.W, ph / p.H); double iw = p.W * k, ih = p.H * k;
            g.DrawImage(img, (pw - iw) / 2, (ph - ih) / 2, iw, ih);
        }
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var n = pdf.PageCount; pdf.Save(path); return n;
    }

    /// <summary>Dateiname aus Datum und Titel, z. B. „2026-11-15 Rechnung Stadtwerke.pdf“ – ohne verbotene Zeichen, höchstens 80 Zeichen.</summary>
    public static string FileName(DateTime date, string title, string ext = ".pdf")
    {
        var bad = Path.GetInvalidFileNameChars().Concat(['/', '\\', ':', '*', '?', '"', '<', '>', '|']).ToHashSet();
        var t = string.Concat((title ?? "").Select(c => bad.Contains(c) || char.IsControl(c) ? ' ' : c));
        t = string.Join(' ', t.Split(' ', StringSplitOptions.RemoveEmptyEntries)).Trim('.', ' ');
        if (t.Length == 0) t = "Scan";
        if (t.Length > 64) t = t[..64].TrimEnd('.', ' ');
        return date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + " " + t + ext;
    }

    /// <summary>Freier Name im Ordner: vorhandene Dateien werden nie überschrieben („… (2).pdf“).</summary>
    public static string Unique(string dir, string name)
    {
        var f = Path.Combine(dir, name); var stem = Path.GetFileNameWithoutExtension(name); var ext = Path.GetExtension(name);
        for (var i = 2; File.Exists(f); i++) f = Path.Combine(dir, $"{stem} ({i}){ext}");
        return f;
    }

    /// <summary>Ablage-Ordner nach Art des Dokuments (wie die Ordner-Regeln in Scan-Apps): Rechnungen, Verträge, Briefe, Quittungen, Notizen, sonst „Scans“.</summary>
    public static string Folder(string title)
    {
        var t = (title ?? "").ToLowerInvariant();
        (string[] Words, string Dir)[] rules = [(["rechnung", "invoice"], "Rechnungen"), (["vertrag", "vereinbarung"], "Verträge"), (["quittung", "beleg", "kassenbon", "bon"], "Belege"),
            (["brief", "schreiben", "bescheid", "mahnung"], "Briefe"), (["whiteboard", "tafel", "notiz", "mitschrift"], "Notizen"), (["ausweis", "zeugnis", "urkunde"], "Urkunden")];
        foreach (var (words, dir) in rules) if (words.Any(w => t.Contains(w, StringComparison.Ordinal))) return dir;
        return "Scans";
    }

    /// <summary>Testbild: Blatt (weiß, mit Textzeilen) als verzerrtes Viereck auf dunklem Tisch; Schatten-Verlauf auf dem Blatt. Für Tests und die Vorführung.</summary>
    public static Pixmap TestPhoto(int w, int h, IReadOnlyList<(double X, double Y)> corners, bool shadow = true)
    {
        var o = new Pixmap(w, h); o.Fill(70, 62, 55, 255);
        var c = Order(corners);
        var inv = VisionPlus.Homography(c, [(0, 0), (1, 0), (1, 1), (0, 1)]);
        for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++)
            {
                var d = inv[6] * x + inv[7] * y + inv[8]; var u = (inv[0] * x + inv[1] * y + inv[2]) / d; var v = (inv[3] * x + inv[4] * y + inv[5]) / d;
                if (u < 0 || u > 1 || v < 0 || v > 1) continue;
                var j = (y * w + x) * 4; double paper = shadow ? 245 - 70 * u * v : 245;
                var line = v > 0.15 && v < 0.85 && u > 0.1 && u < 0.9 && ((int)(v * 20)) % 2 == 0 && (v * 20 % 1) < 0.35;
                var val = line ? paper * 0.25 : paper;
                o.Px[j] = (byte)val; o.Px[j + 1] = (byte)val; o.Px[j + 2] = (byte)(val * 0.97);
            }
        return o;
    }

    /// <summary>Kurzer Bericht für den Ablage-Plan (KI-Adapter, Statuszeile).</summary>
    public static string Plan(DateTime date, string title, string mode, int pages, string pageSize)
    {
        var sb = new StringBuilder();
        var name = FileName(date, title);
        sb.Append("Scan-Auftrag: ").Append(title.Length > 0 ? title : "Scan").Append('\n');
        sb.Append("Dateiname: ").Append(name).Append('\n');
        sb.Append("Ordner: Dokumente/Scans/").Append(Folder(title)).Append('\n');
        sb.Append("Modus: ").Append(ModeName(mode)).Append(" – ").Append(Modes.First(m => m.Id == mode).Hint).Append('\n');
        sb.Append("Seiten: ").Append(pages > 0 ? pages.ToString(CultureInfo.InvariantCulture) : "beliebig").Append(" · Seitenformat: ").Append(pageSize == "auto" ? "wie das Bild" : "A4").Append('\n');
        sb.Append("\nSo geht es:\n1. Blatt flach auf eine dunkle Unterlage legen, von oben fotografieren (Handy reicht).\n2. Fotos oder den Ordner im Studio „Dokumente scannen“ öffnen – Trendo findet die Blattkanten.\n");
        sb.Append("3. Ecken prüfen, falls nötig ziehen; drehen, Reihenfolge ändern.\n4. „Als PDF speichern“ – mit Texterkennung, wenn Tesseract eingerichtet ist.\n");
        return sb.ToString();
    }
}
