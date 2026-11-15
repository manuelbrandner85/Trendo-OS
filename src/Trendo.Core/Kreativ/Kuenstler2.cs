using System.Globalization;
using System.Net;
using System.Text;

namespace Trendo.Core.Kreativ;

/// <summary>K4-02: Posenpuppe – Gliederpuppe mit echten Proportionen (8 Kopfhöhen), Gelenkwinkel in 3D, von vorne oder schräg gesehen.
/// Wird als halbdurchsichtige Skizzenebene gezeichnet, damit man darüber malt.</summary>
public static class Mannequin
{
    /// <summary>Gelenkwinkel in Grad: Schulter (seitlich anheben / nach vorne), Ellbogen, Hüfte (vorne), Knie; Rumpfneigung; Kopfneigung.</summary>
    public sealed record Pose(string Id, string Name,
        double ShoulderLSide, double ShoulderLFront, double ElbowL, double ShoulderRSide, double ShoulderRFront, double ElbowR,
        double HipLFront, double KneeL, double HipRFront, double KneeR, double Lean = 0, double Head = 0, double HipLSide = 4, double HipRSide = 4);

    public static readonly Pose[] Poses =
    [
        new("stehen", "Stehen", 8, 0, 5, 8, 0, 5, 0, 0, 0, 0),
        new("gehen", "Gehen", 6, 25, 20, 6, -25, 10, -25, 10, 20, 35, 3),
        new("laufen", "Laufen", 10, 50, 90, 10, -45, 90, -45, 30, 40, 100, 12, 5),
        new("sitzen", "Sitzen", 10, 30, 70, 10, 30, 70, 90, 90, 90, 90, 5, 0, 6, 6),
        new("winken", "Winken", 8, 0, 5, 160, 0, 40, 0, 0, 0, 0),
        new("zeigen", "Zeigen", 8, 0, 5, 20, 85, 0, 0, 0, 0, 0, 0, -5),
        new("hocken", "Hocken", 15, 40, 60, 15, 40, 60, 110, 140, 110, 140, 25, 10, 15, 15),
        new("arme-hoch", "Jubeln", 150, 0, 15, 150, 0, 15, 0, 0, 0, 0, -3),
    ];
    public static Pose Get(string? id) => Poses.FirstOrDefault(p => p.Id == id) ?? Poses[0];

    public sealed record Bone(string Name, (double X, double Y) A, (double X, double Y) B, double Thickness);

    /// <summary>Knochen in Bildkoordinaten. <paramref name="height"/> = Körperhöhe in Bildpunkten, <paramref name="yaw"/> = Drehung um die Hochachse (0 = von vorne).</summary>
    public static List<Bone> Bones(Pose p, double cx, double footY, double height, double yaw = 0)
    {
        var u = height / 8.0; // Kopfhöhe
        double Rad(double d) => d * Math.PI / 180;
        var cy = Math.Cos(Rad(yaw)); var sy = Math.Sin(Rad(yaw));
        (double X, double Y) Pr((double X, double Y, double Z) v) => (cx + v.X * cy + v.Z * sy, footY - v.Y);
        // Gelenke im Körperraum (x rechts, y oben, z nach vorne), Hüfte auf 4 Kopfhöhen
        var lean = Rad(p.Lean);
        var hip = (X: 0.0, Y: 4 * u, Z: 0.0);
        if (p.HipLFront >= 80 && p.HipRFront >= 80) hip.Y = 4 * u - 0.1 * u; // sitzen/hocken: Becken bleibt auf Höhe
        (double X, double Y, double Z) Up(double len) => (0, len * Math.Cos(lean), len * Math.Sin(lean));
        (double X, double Y, double Z) Add((double X, double Y, double Z) a, (double X, double Y, double Z) b) => (a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        var chest = Add(hip, Up(2.2 * u)); var neck = Add(hip, Up(2.9 * u)); var headC = Add(neck, (0, 0.55 * u * Math.Cos(Rad(p.Head)), 0.55 * u * Math.Sin(Rad(p.Head))));
        // Gliedmaße: Richtung aus seitlichem und vorderem Winkel, nach unten hängend bei 0
        (double X, double Y, double Z) Limb(double side, double front, double len, int s)
        {
            double a = Rad(side), f = Rad(front);
            var dir = (X: s * Math.Sin(a) * Math.Cos(f), Y: -Math.Cos(a) * Math.Cos(f), Z: Math.Sin(f));
            return (dir.X * len, dir.Y * len, dir.Z * len);
        }
        (double X, double Y, double Z) Bend((double X, double Y, double Z) upper, double angle, double len, bool forward)
        {   // Ellbogen/Knie: weiter in der Ebene aus Gliedrichtung und Vorne/Hinten
            var l = Math.Sqrt(upper.X * upper.X + upper.Y * upper.Y + upper.Z * upper.Z); var d = (X: upper.X / l, Y: upper.Y / l, Z: upper.Z / l);
            var a = Rad(angle) * (forward ? 1 : -1);
            var perp = (X: 0.0, Y: d.Z, Z: -d.Y); var pl = Math.Sqrt(perp.Y * perp.Y + perp.Z * perp.Z); if (pl < 1e-6) perp = (0, 0, 1); else perp = (0, perp.Y / pl, perp.Z / pl);
            if (!forward) perp = (perp.X, -perp.Y, -perp.Z);
            var nd = (X: d.X * Math.Cos(a), Y: d.Y * Math.Cos(a) + perp.Y * Math.Sin(Math.Abs(a)), Z: d.Z * Math.Cos(a) + perp.Z * Math.Sin(Math.Abs(a)));
            return (nd.X * len, nd.Y * len, nd.Z * len);
        }
        var shL = Add(chest, (-0.95 * u, 0.25 * u, 0)); var shR = Add(chest, (0.95 * u, 0.25 * u, 0));
        var hpL = Add(hip, (-0.45 * u, 0, 0)); var hpR = Add(hip, (0.45 * u, 0, 0));
        var uaL = Limb(p.ShoulderLSide, p.ShoulderLFront, 1.45 * u, -1); var elL = Add(shL, uaL); var haL = Add(elL, Bend(uaL, p.ElbowL, 1.3 * u, true));
        var uaR = Limb(p.ShoulderRSide, p.ShoulderRFront, 1.45 * u, 1); var elR = Add(shR, uaR); var haR = Add(elR, Bend(uaR, p.ElbowR, 1.3 * u, true));
        var thL = Limb(p.HipLSide, p.HipLFront, 2.0 * u, -1); var knL = Add(hpL, thL); var ftL = Add(knL, Bend(thL, p.KneeL, 2.0 * u, false));
        var thR = Limb(p.HipRSide, p.HipRFront, 2.0 * u, 1); var knR = Add(hpR, thR); var ftR = Add(knR, Bend(thR, p.KneeR, 2.0 * u, false));
        var t = u * 0.32;
        return
        [
            new("Rumpf", Pr(hip), Pr(neck), t * 2.4), new("Becken", Pr(hpL), Pr(hpR), t * 1.6), new("Schultern", Pr(shL), Pr(shR), t * 1.2),
            new("Hals", Pr(neck), Pr(headC), t * 0.9), new("Kopf", Pr(headC), Pr(Add(headC, (0, 0.01, 0))), u * 0.95),
            new("Oberarm links", Pr(shL), Pr(elL), t), new("Unterarm links", Pr(elL), Pr(haL), t * 0.8),
            new("Oberarm rechts", Pr(shR), Pr(elR), t), new("Unterarm rechts", Pr(elR), Pr(haR), t * 0.8),
            new("Oberschenkel links", Pr(hpL), Pr(knL), t * 1.3), new("Unterschenkel links", Pr(knL), Pr(ftL), t),
            new("Oberschenkel rechts", Pr(hpR), Pr(knR), t * 1.3), new("Unterschenkel rechts", Pr(knR), Pr(ftR), t),
        ];
    }

    /// <summary>Puppe als Skizzenebene zeichnen (Kapseln, halbdurchsichtig, Farbe wählbar).</summary>
    public static Pixmap Render(int w, int h, Pose p, double yaw = 0, string color = "#4A90D9", double alpha = 0.55)
    {
        var r = new Pixmap(w, h); var (cr, cg, cb) = PaintColors.Rgb(color);
        var height = h * 0.86; var bones = Bones(p, w / 2.0, h * 0.95, height, yaw);
        foreach (var b in bones)
        {
            var rad = Math.Max(1.5, b.Thickness / 2);
            int x0 = Math.Max(0, (int)(Math.Min(b.A.X, b.B.X) - rad - 1)), x1 = Math.Min(w - 1, (int)(Math.Max(b.A.X, b.B.X) + rad + 1));
            int y0 = Math.Max(0, (int)(Math.Min(b.A.Y, b.B.Y) - rad - 1)), y1 = Math.Min(h - 1, (int)(Math.Max(b.A.Y, b.B.Y) + rad + 1));
            double vx = b.B.X - b.A.X, vy = b.B.Y - b.A.Y, l2 = vx * vx + vy * vy;
            for (var y = y0; y <= y1; y++)
                for (var x = x0; x <= x1; x++)
                {
                    double px = x + 0.5 - b.A.X, py = y + 0.5 - b.A.Y, tt = l2 < 1e-9 ? 0 : Math.Clamp((px * vx + py * vy) / l2, 0, 1);
                    double dx = px - tt * vx, dy = py - tt * vy, d = Math.Sqrt(dx * dx + dy * dy);
                    var cov = Math.Clamp(rad + 0.5 - d, 0, 1); if (cov <= 0) continue;
                    var edge = d > rad - 2 ? 1.0 : 0.55; // Umriss kräftiger, Fläche leichter
                    var i = (y * w + x) * 4; var a = (byte)Math.Round(255 * alpha * cov * edge);
                    if (a > r.Px[i + 3]) { r.Px[i] = cr; r.Px[i + 1] = cg; r.Px[i + 2] = cb; r.Px[i + 3] = a; }
                }
        }
        return r;
    }
}

/// <summary>K5-01: Zeitraffer – nach jedem Strich ein verkleinertes Bild der ganzen Leinwand; daraus wird mit FFmpeg ein Video.</summary>
public static class Timelapse
{
    public const int MaxSide = 1280;
    public static string FrameName(int n) => $"z{n:00000}.png";
    public static int Count(string dir) => Directory.Exists(dir) ? Directory.GetFiles(dir, "z?????.png").Length : 0;
    /// <summary>Nächstes Bild ablegen (höchstens <see cref="MaxSide"/> Bildpunkte an der langen Seite, gerade Maße für das Video).</summary>
    public static string Record(string dir, Pixmap composite)
    {
        Directory.CreateDirectory(dir);
        var s = Math.Min(1.0, (double)MaxSide / Math.Max(composite.W, composite.H));
        int w = Math.Max(2, (int)Math.Round(composite.W * s) / 2 * 2), h = Math.Max(2, (int)Math.Round(composite.H * s) / 2 * 2);
        var img = composite.W == w && composite.H == h ? composite : composite.Resize(w, h);
        var f = Path.Combine(dir, FrameName(Count(dir) + 1)); File.WriteAllBytes(f, PngCodec.Encode(img)); return f;
    }
    /// <summary>Bilder pro Sekunde so, dass das Video zwischen 5 und 30 Sekunden lang ist.</summary>
    public static int Fps(int frames) => Math.Clamp((int)Math.Ceiling(frames / 30.0), 2, 30);
    public static string Pattern(string dir) => Path.Combine(dir, "z%05d.png");
}

/// <summary>K6: Skizze säubern und vorfärben – eigene, einfache Bildverfahren, Ergebnis immer als neue Ebene, das Original bleibt.</summary>
public static class Sketch
{
    /// <summary>Linien aus einer Skizze: dunkle Stellen werden schwarze Linie (Deckkraft nach Dunkelheit), helles Papier wird durchsichtig,
    /// kleine Krümel unter <paramref name="minSpeck"/> Bildpunkten fallen weg. <paramref name="threshold"/> 0…1: wie dunkel eine Linie mindestens ist.</summary>
    public static Pixmap LineArt(Pixmap sketch, double threshold = 0.35, int minSpeck = 12, string color = "#111111")
    {
        int w = sketch.W, h = sketch.H; var dark = new double[w * h];
        for (var i = 0; i < w * h; i++)
        {
            var o = i * 4; var lum = (0.299 * sketch.Px[o] + 0.587 * sketch.Px[o + 1] + 0.114 * sketch.Px[o + 2]) / 255.0;
            dark[i] = (1 - lum) * sketch.Px[o + 3] / 255.0;
        }
        var t = Math.Clamp(threshold, 0.02, 0.98); var on = new bool[w * h];
        for (var i = 0; i < on.Length; i++) on[i] = dark[i] >= t;
        // Krümel entfernen (zusammenhängende Flächen, 8er-Nachbarschaft)
        var seen = new bool[w * h]; var stack = new Stack<int>(); var comp = new List<int>();
        for (var s = 0; s < on.Length; s++)
        {
            if (!on[s] || seen[s]) continue;
            comp.Clear(); stack.Push(s); seen[s] = true;
            while (stack.Count > 0)
            {
                var c = stack.Pop(); comp.Add(c); int cx = c % w, cy = c / w;
                for (var dy = -1; dy <= 1; dy++) for (var dx = -1; dx <= 1; dx++)
                {
                    int nx = cx + dx, ny = cy + dy; if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                    var n = ny * w + nx; if (on[n] && !seen[n]) { seen[n] = true; stack.Push(n); }
                }
            }
            if (comp.Count < minSpeck) foreach (var c in comp) on[c] = false;
        }
        var (r, g, b) = PaintColors.Rgb(color); var res = new Pixmap(w, h);
        for (var i = 0; i < on.Length; i++)
        {
            if (!on[i]) continue;
            var a = Math.Clamp((dark[i] - t) / Math.Max(0.05, 1 - t) * 1.6 + 0.45, 0, 1); var o = i * 4;
            res.Px[o] = r; res.Px[o + 1] = g; res.Px[o + 2] = b; res.Px[o + 3] = (byte)Math.Round(a * 255);
        }
        return res;
    }

    /// <summary>Vorfärben: jede von Linien umschlossene Fläche bekommt eine eigene Farbe auf einer neuen Ebene (wie „Flats“ im Comic).
    /// Flächen, die den Bildrand berühren, gelten als Hintergrund und bleiben leer. Liefert Ebene und Anzahl der Flächen.</summary>
    public static (Pixmap Flats, int Regions) Flats(Pixmap lines, IReadOnlyList<string>? palette = null, int minArea = 30, byte lineAlpha = 64)
    {
        int w = lines.W, h = lines.H; var wall = new bool[w * h];
        for (var i = 0; i < wall.Length; i++) wall[i] = lines.Px[i * 4 + 3] >= lineAlpha;
        var label = new int[w * h]; var res = new Pixmap(w, h); var n = 0; var q = new Queue<int>(); var comp = new List<int>();
        var pal = palette is { Count: > 0 } ? palette : ["#F4C7A1", "#9EC5E8", "#B8D8A8", "#F2E394", "#D9B3E6", "#F5A9A9", "#A9E0D9", "#E8C39E"];
        for (var s = 0; s < wall.Length; s++)
        {
            if (wall[s] || label[s] != 0) continue;
            comp.Clear(); q.Enqueue(s); label[s] = -1; var border = false;
            while (q.Count > 0)
            {
                var c = q.Dequeue(); comp.Add(c); int cx = c % w, cy = c / w;
                if (cx == 0 || cy == 0 || cx == w - 1 || cy == h - 1) border = true;
                foreach (var (nx, ny) in new[] { (cx + 1, cy), (cx - 1, cy), (cx, cy + 1), (cx, cy - 1) })
                {
                    if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                    var k = ny * w + nx; if (!wall[k] && label[k] == 0) { label[k] = -1; q.Enqueue(k); }
                }
            }
            if (border || comp.Count < minArea) continue;
            var (r, g, b) = PaintColors.Rgb(pal[n % pal.Count]); n++;
            foreach (var c in comp) { var o = c * 4; res.Px[o] = r; res.Px[o + 1] = g; res.Px[o + 2] = b; res.Px[o + 3] = 255; }
        }
        // Farbe unter die Linie ziehen, damit keine weißen Säume bleiben (1 Bildpunkt)
        var grow = res.Clone();
        for (var y = 0; y < h; y++) for (var x = 0; x < w; x++)
        {
            var i = y * w + x; if (res.Px[i * 4 + 3] != 0 || !wall[i]) continue;
            foreach (var (nx, ny) in new[] { (x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1) })
            {
                if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                var k = (ny * w + nx) * 4; if (res.Px[k + 3] == 255) { Buffer.BlockCopy(res.Px, k, grow.Px, i * 4, 4); break; }
            }
        }
        return (grow, n);
    }
}

/// <summary>K7-02: Druckdatei – Bild auf ein Papierformat bei gewählter Auflösung, mit Beschnitt (Bild läuft über den Rand, wird beim Schneiden entfernt).</summary>
public static class PrintFile
{
    public static readonly (string Id, string Name, double WMm, double HMm)[] Formats =
    [
        ("a5", "A5 (148 × 210 mm)", 148, 210), ("a4", "A4 (210 × 297 mm)", 210, 297), ("a3", "A3 (297 × 420 mm)", 297, 420),
        ("postkarte", "Postkarte (148 × 105 mm)", 148, 105), ("quadrat30", "Quadrat 30 × 30 cm", 300, 300), ("poster50", "Poster 50 × 70 cm", 500, 700),
    ];
    public static (int W, int H) Pixels(string format, int dpi = 300, double bleedMm = 3, bool landscape = false)
    {
        var f = Formats.FirstOrDefault(x => x.Id == format); if (f.Name is null) throw new ArgumentException("Unbekanntes Format: " + format);
        if (dpi is < 72 or > 1200) throw new ArgumentException("Die Auflösung muss zwischen 72 und 1200 dpi liegen.");
        if (bleedMm is < 0 or > 20) throw new ArgumentException("Der Beschnitt muss zwischen 0 und 20 mm liegen.");
        double wm = f.WMm, hm = f.HMm;
        if (landscape && wm < hm) (wm, hm) = (hm, wm);
        wm += 2 * bleedMm; hm += 2 * bleedMm;
        return ((int)Math.Round(wm / 25.4 * dpi), (int)Math.Round(hm / 25.4 * dpi));
    }
    /// <summary>Bild füllend einpassen (beschnitten, nicht verzerrt) – Ergebnis als PNG mit Auflösung im Kopf.
    /// <paramref name="sourceDpiWarning"/> sagt, ob das Bild für diese Größe zu klein ist (unter 150 dpi echte Auflösung).</summary>
    public static (byte[] Png, int W, int H, bool SourceDpiWarning) Make(Pixmap img, string format, int dpi = 300, double bleedMm = 3, bool landscape = false, string background = "#FFFFFF")
    {
        var (w, h) = Pixels(format, dpi, bleedMm, landscape);
        var s = Math.Max((double)w / img.W, (double)h / img.H);
        var sw = Math.Max(1, (int)Math.Round(img.W * s)); var sh = Math.Max(1, (int)Math.Round(img.H * s));
        var scaled = img.Resize(sw, sh); var res = new Pixmap(w, h); var (r, g, b) = PaintColors.Rgb(background); res.Fill(r, g, b, 255);
        int ox = (sw - w) / 2, oy = (sh - h) / 2;
        var tmp = new Pixmap(w, h);
        for (var y = 0; y < h; y++) Buffer.BlockCopy(scaled.Px, ((y + oy) * sw + ox) * 4, tmp.Px, y * w * 4, w * 4);
        PaintBlend.Over(res, tmp, 1, "normal");
        return (PngCodec.Encode(res, fast: false, dpi: dpi), w, h, dpi / s < 150);
    }
}

/// <summary>K7-01: Portfolio-Webseite – eine schlichte, eigene Galerie (eine HTML-Datei, Bilder daneben), ohne fremde Skripte, offline lesbar.</summary>
public static class Portfolio
{
    public sealed record Item(string Title, byte[] Png, string Note = "");
    public static string Write(string dir, string title, string artist, IReadOnlyList<Item> items, string accent = "#C9A227")
    {
        if (items.Count == 0) throw new InvalidOperationException("Erst mindestens ein Bild für das Portfolio auswählen.");
        Directory.CreateDirectory(Path.Combine(dir, "bilder"));
        static string E(string s) => WebUtility.HtmlEncode(s ?? "");
        var sb = new StringBuilder();
        sb.Append("<!doctype html>\n<html lang=\"de\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">")
          .Append("<title>").Append(E(title)).Append("</title><style>")
          .Append(":root{--a:").Append(PaintColors.IsHex(accent) ? accent : "#C9A227").Append(";--bg:#111214;--fg:#ECEDEE;--mut:#9A9DA3}")
          .Append("*{box-sizing:border-box}body{margin:0;background:var(--bg);color:var(--fg);font:16px/1.5 system-ui,sans-serif}")
          .Append("header{padding:48px 24px 16px;max-width:1200px;margin:auto}h1{margin:0;font-size:2.2rem;font-weight:600}header p{color:var(--mut);margin:.25rem 0 0}")
          .Append("main{max-width:1200px;margin:auto;padding:16px 24px 64px;display:grid;gap:24px;grid-template-columns:repeat(auto-fill,minmax(280px,1fr))}")
          .Append("figure{margin:0}figure img{width:100%;height:auto;display:block;border-radius:4px;background:#1b1c1f}figcaption{margin-top:8px}figcaption small{display:block;color:var(--mut)}")
          .Append("a{color:var(--a)}footer{text-align:center;color:var(--mut);padding:24px;font-size:.85rem}</style></head><body>\n")
          .Append("<header><h1>").Append(E(title)).Append("</h1><p>").Append(E(artist)).Append("</p></header>\n<main>\n");
        var used = new HashSet<string>();
        for (var i = 0; i < items.Count; i++)
        {
            var it = items[i]; var baseName = Slug(it.Title); var name = baseName; var k = 2; while (!used.Add(name)) name = $"{baseName}-{k++}";
            File.WriteAllBytes(Path.Combine(dir, "bilder", name + ".png"), it.Png);
            sb.Append("<figure><a href=\"bilder/").Append(name).Append(".png\"><img loading=\"lazy\" src=\"bilder/").Append(name).Append(".png\" alt=\"").Append(E(it.Title)).Append("\"></a><figcaption>")
              .Append(E(it.Title)).Append(it.Note.Length > 0 ? "<small>" + E(it.Note) + "</small>" : "").Append("</figcaption></figure>\n");
        }
        sb.Append("</main>\n<footer>© ").Append(DateTime.Now.Year.ToString(CultureInfo.InvariantCulture)).Append(' ').Append(E(artist)).Append("</footer>\n</body></html>\n");
        var f = Path.Combine(dir, "index.html"); File.WriteAllText(f, sb.ToString(), new UTF8Encoding(false)); return f;
    }
    public static string Slug(string s)
    {
        var t = (s ?? "").ToLowerInvariant().Replace("ä", "ae").Replace("ö", "oe").Replace("ü", "ue").Replace("ß", "ss");
        var r = new StringBuilder(); foreach (var c in t) r.Append(char.IsAsciiLetterOrDigit(c) ? c : '-');
        var x = string.Join('-', r.ToString().Split('-', StringSplitOptions.RemoveEmptyEntries)); return x.Length == 0 ? "bild" : x[..Math.Min(60, x.Length)];
    }
}

/// <summary>K8-01: Geführte Malkurse. Jeder Schritt hat eine prüfbare Bedingung auf dem Bild – der Kurs geht erst weiter, wenn sie erfüllt ist.</summary>
public static class Courses
{
    /// <summary>Was ein Schritt prüfen kann: Ebenen, gemalte Ebenen, Spiegel, Zeichenhilfe, Palette, Mischart, Ebenen mit Namen.</summary>
    public sealed record State(int Layers, int PaintedLayers, string Symmetry, string Assist, int PaletteColors, IReadOnlyList<string> Blends, IReadOnlyList<string> LayerNames, int Strokes, bool HasReference, bool HasFlats);
    public sealed record Step(string Text, string Hint, Func<State, bool> Done);
    public sealed record Course(string Id, string Title, string Level, string Goal, IReadOnlyList<Step> Steps);

    public static State Of(PaintDoc d, string symmetry, string assist, int paletteColors, int strokes, bool hasReference) =>
        new(d.Layers.Count, d.Layers.Count(l => !l.Px.IsEmpty()), symmetry, assist, paletteColors, [.. d.Layers.Select(l => l.Blend)], [.. d.Layers.Select(l => l.Name)], strokes, hasReference,
            d.Layers.Any(l => l.Name.StartsWith("Farbflächen", StringComparison.Ordinal)));

    public static readonly Course[] All =
    [
        new("erste-schritte", "Erste Schritte", "Einsteiger", "Pinsel, Farbe und Ebenen kennenlernen",
        [
            new("Wähle links den Bleistift und zeichne eine Linie.", "Ein Klick auf ✏ wählt den Bleistift.", s => s.Strokes >= 1),
            new("Lege mit „＋ Ebene“ eine zweite Ebene an.", "Ebenen sind wie Folien übereinander.", s => s.Layers >= 2),
            new("Wähle eine andere Farbe im Farbrad und male auf der neuen Ebene.", "Rechts im Farbrad die Farbe antippen.", s => s.PaintedLayers >= 2),
            new("Blende die untere Ebene mit dem Auge aus und wieder ein.", "So prüfst du, was auf welcher Ebene liegt.", s => s.Strokes >= 2),
        ]),
        new("mandala", "Mandala mit Spiegel", "Einsteiger", "Symmetrisch zeichnen",
        [
            new("Stelle unter „Spiegeln“ den Modus „Radial (Mandala)“ ein.", "Die Mitte ist die Bildmitte.", s => s.Symmetry == "radial"),
            new("Zeichne von der Mitte aus Blätter – jeder Strich erscheint mehrfach.", "Langsam ziehen, Glätten hilft.", s => s.Strokes >= 3),
            new("Lege eine Ebene für Farbe an und stelle sie auf „Multiplizieren“.", "So bleiben die Linien darüber sichtbar.", s => s.Blends.Contains("multiply")),
        ]),
        new("wuerfel", "Würfel in Perspektive", "Fortgeschritten", "Räumlich zeichnen mit zwei Fluchtpunkten",
        [
            new("Wähle unter „Zeichenhilfe“ die Perspektive mit 2 Punkten.", "Die Punkte liegen auf der Horizontlinie.", s => s.Assist == "perspektive2"),
            new("Zeichne die vordere senkrechte Kante und die Kanten zu beiden Fluchtpunkten.", "Striche rasten von selbst ein.", s => s.Strokes >= 3),
            new("Schalte die Hilfe aus und schattiere eine Seite auf einer eigenen Ebene.", "Licht von einer Seite – die andere dunkler.", s => s.Assist == "aus" && s.PaintedLayers >= 2),
        ]),
        new("comic", "Von der Skizze zur Farbe", "Fortgeschritten", "Skizze säubern, vorfärben, schattieren",
        [
            new("Zeichne eine grobe Skizze mit dem Bleistift.", "Ruhig locker – das wird später sauber.", s => s.Strokes >= 3),
            new("Drücke „Skizze säubern“ – die Linien kommen auf eine eigene Ebene.", "Die Skizze bleibt darunter erhalten.", s => s.LayerNames.Any(n => n.StartsWith("Linien", StringComparison.Ordinal))),
            new("Drücke „Vorfärben“ – jede geschlossene Fläche bekommt eine Farbe.", "Lücken in Linien vorher schließen.", s => s.HasFlats),
            new("Lege eine Schatten-Ebene mit „Multiplizieren“ an.", "Schatten mit einem kühlen Grau.", s => s.Blends.Contains("multiply")),
        ]),
        new("farben", "Farben aus einem Foto", "Einsteiger", "Eine stimmige Palette finden",
        [
            new("Lade ein Referenzfoto unter „Referenz“.", "Es bleibt neben dem Bild und wird nicht mitgespeichert.", s => s.HasReference),
            new("Drücke „Farben aus Referenz“ – die Hauptfarben kommen in die Palette.", "Meist reichen 5–6 Farben.", s => s.PaletteColors >= 4),
            new("Male ein kleines Bild nur mit diesen Farben.", "Weniger Farben wirken ruhiger.", s => s.Strokes >= 5),
        ]),
    ];
    public static Course Get(string id) => All.FirstOrDefault(c => c.Id == id) ?? throw new ArgumentException("Unbekannter Kurs: " + id);

    /// <summary>Erster noch nicht erfüllter Schritt ab <paramref name="from"/> (Schritte gelten der Reihe nach). Gleich der Anzahl = Kurs geschafft.</summary>
    public static int Progress(Course c, State s, int from = 0)
    {
        var i = Math.Clamp(from, 0, c.Steps.Count);
        while (i < c.Steps.Count && c.Steps[i].Done(s)) i++;
        return i;
    }
}
