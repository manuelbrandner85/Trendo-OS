using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.Json.Nodes;
using System.Xml.Linq;

namespace Trendo.Core.Kreativ;

/// <summary>Künstlerstudio (K1–K8, Freigabe 15.11.2026): eigener Mal-Kern – Clean-Room nach dem Verhalten von Krita/Procreate, kein fremder Code.
/// Pixel liegen als RGBA (nicht vormultipliziert, 8 Bit) vor. Siehe docs/nachbau/kuenstlerstudio/anforderungen.md.</summary>
public sealed class Pixmap
{
    public int W { get; }
    public int H { get; }
    public byte[] Px { get; }
    public Pixmap(int w, int h, byte[]? px = null)
    {
        if (w < 1 || h < 1 || (long)w * h > 64_000_000) throw new ArgumentException("Die Leinwand muss zwischen 1 × 1 und 64 Millionen Bildpunkten groß sein.");
        W = w; H = h; Px = px ?? new byte[w * h * 4];
        if (Px.Length != w * h * 4) throw new ArgumentException("Die Bilddaten passen nicht zur Größe.");
    }
    public Pixmap Clone() => new(W, H, (byte[])Px.Clone());
    public (byte R, byte G, byte B, byte A) At(int x, int y) { var i = (y * W + x) * 4; return (Px[i], Px[i + 1], Px[i + 2], Px[i + 3]); }
    public void Fill(byte r, byte g, byte b, byte a) { for (var i = 0; i < Px.Length; i += 4) { Px[i] = r; Px[i + 1] = g; Px[i + 2] = b; Px[i + 3] = a; } }
    public bool IsEmpty() { for (var i = 3; i < Px.Length; i += 4) if (Px[i] != 0) return false; return true; }
    /// <summary>Verkleinern/Vergrößern (bilinear, Rand mit Alpha gewichtet).</summary>
    public Pixmap Resize(int w, int h)
    {
        var o = new Pixmap(w, h);
        for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++)
            {
                double sx = (x + 0.5) * W / w - 0.5, sy = (y + 0.5) * H / h - 0.5;
                int x0 = Math.Clamp((int)Math.Floor(sx), 0, W - 1), y0 = Math.Clamp((int)Math.Floor(sy), 0, H - 1), x1 = Math.Min(x0 + 1, W - 1), y1 = Math.Min(y0 + 1, H - 1);
                double fx = Math.Clamp(sx - x0, 0, 1), fy = Math.Clamp(sy - y0, 0, 1);
                double a = 0, r = 0, g = 0, b = 0;
                void Add(int px, int py, double wgt) { var i = (py * W + px) * 4; var al = Px[i + 3] * wgt; a += al; r += Px[i] * al; g += Px[i + 1] * al; b += Px[i + 2] * al; }
                Add(x0, y0, (1 - fx) * (1 - fy)); Add(x1, y0, fx * (1 - fy)); Add(x0, y1, (1 - fx) * fy); Add(x1, y1, fx * fy);
                var j = (y * w + x) * 4;
                if (a > 0) { o.Px[j] = (byte)Math.Round(r / a); o.Px[j + 1] = (byte)Math.Round(g / a); o.Px[j + 2] = (byte)Math.Round(b / a); }
                o.Px[j + 3] = (byte)Math.Round(Math.Clamp(a, 0, 255));
            }
        return o;
    }
}

/// <summary>PNG lesen und schreiben (8 Bit, ohne Zeilensprung) – damit der Kern ohne Grafikbibliothek auskommt und testbar bleibt.</summary>
public static class PngCodec
{
    static readonly uint[] CrcTable = Enumerable.Range(0, 256).Select(n => { var c = (uint)n; for (var k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1; return c; }).ToArray();
    static uint Crc(ReadOnlySpan<byte> d) { var c = 0xFFFFFFFF; foreach (var b in d) c = CrcTable[(c ^ b) & 0xFF] ^ (c >> 8); return c ^ 0xFFFFFFFF; }
    static void U32(Stream s, uint v) { s.WriteByte((byte)(v >> 24)); s.WriteByte((byte)(v >> 16)); s.WriteByte((byte)(v >> 8)); s.WriteByte((byte)v); }
    static void Chunk(Stream s, string type, byte[] data)
    {
        var buf = new byte[data.Length + 4]; Encoding.ASCII.GetBytes(type).CopyTo(buf, 0); data.CopyTo(buf, 4);
        U32(s, (uint)data.Length); s.Write(buf); U32(s, Crc(buf));
    }

    /// <summary>RGBA als PNG; <paramref name="dpi"/> &gt; 0 schreibt die Auflösung (pHYs) für die Druckerei.</summary>
    public static byte[] Encode(Pixmap r, bool fast = true, double dpi = 0)
    {
        using var ms = new MemoryStream(); ms.Write([137, 80, 78, 71, 13, 10, 26, 10]);
        var ihdr = new byte[13]; Be(ihdr, 0, (uint)r.W); Be(ihdr, 4, (uint)r.H); ihdr[8] = 8; ihdr[9] = 6; Chunk(ms, "IHDR", ihdr);
        if (dpi > 0) { var p = new byte[9]; var ppm = (uint)Math.Round(dpi / 0.0254); Be(p, 0, ppm); Be(p, 4, ppm); p[8] = 1; Chunk(ms, "pHYs", p); }
        using var raw = new MemoryStream();
        using (var z = new ZLibStream(raw, fast ? CompressionLevel.Fastest : CompressionLevel.Optimal, true))
        {
            var stride = r.W * 4; var line = new byte[stride + 1];
            for (var y = 0; y < r.H; y++)
            {   // Filter „Sub“: kleiner als ungefiltert, schnell
                line[0] = 1; var o = y * stride;
                for (var i = 0; i < stride; i++) line[i + 1] = (byte)(r.Px[o + i] - (i >= 4 ? r.Px[o + i - 4] : 0));
                z.Write(line);
            }
        }
        Chunk(ms, "IDAT", raw.ToArray()); Chunk(ms, "IEND", []); return ms.ToArray();
    }
    static void Be(byte[] b, int o, uint v) { b[o] = (byte)(v >> 24); b[o + 1] = (byte)(v >> 16); b[o + 2] = (byte)(v >> 8); b[o + 3] = (byte)v; }
    static uint Be(ReadOnlySpan<byte> b, int o) => (uint)(b[o] << 24 | b[o + 1] << 16 | b[o + 2] << 8 | b[o + 3]);

    /// <summary>PNG (Graustufen, RGB, RGBA, Grau+Alpha, Palette; 8 Bit) zu RGBA. Andere Varianten werden mit einer klaren Meldung abgelehnt.</summary>
    public static Pixmap Decode(byte[] png)
    {
        if (png.Length < 33 || png[0] != 137 || png[1] != 80 || png[2] != 78 || png[3] != 71) throw new InvalidDataException("Das ist keine PNG-Datei.");
        int pos = 8, w = 0, h = 0, type = -1, depth = 0, inter = 0; byte[]? pal = null, trns = null; using var idat = new MemoryStream();
        while (pos + 8 <= png.Length)
        {
            var len = (int)Be(png, pos); var t = Encoding.ASCII.GetString(png, pos + 4, 4);
            if (len < 0 || pos + 12 + len > png.Length) throw new InvalidDataException("Die PNG-Datei ist beschädigt.");
            var data = png.AsSpan(pos + 8, len);
            switch (t)
            {
                case "IHDR": w = (int)Be(data, 0); h = (int)Be(data, 4); depth = data[8]; type = data[9]; inter = data[12]; break;
                case "PLTE": pal = data.ToArray(); break;
                case "tRNS": trns = data.ToArray(); break;
                case "IDAT": idat.Write(data); break;
            }
            pos += 12 + len; if (t == "IEND") break;
        }
        if (depth != 8 || inter != 0 || type is not (0 or 2 or 3 or 4 or 6)) throw new InvalidDataException("Diese PNG-Variante (16 Bit oder Zeilensprung) kann das Künstlerstudio nicht lesen – bitte als normale 8-Bit-PNG speichern.");
        int ch = type switch { 0 => 1, 2 => 3, 3 => 1, 4 => 2, _ => 4 }, stride = w * ch;
        var r = new Pixmap(w, h); idat.Position = 0;
        using var z = new ZLibStream(idat, CompressionMode.Decompress);
        var prev = new byte[stride]; var cur = new byte[stride]; var f = new byte[1];
        for (var y = 0; y < h; y++)
        {
            if (z.ReadAtLeast(f, 1, false) < 1 || z.ReadAtLeast(cur, stride, false) < stride) throw new InvalidDataException("Die PNG-Datei ist unvollständig.");
            for (var i = 0; i < stride; i++)
            {
                int a = i >= ch ? cur[i - ch] : 0, b = prev[i], c = i >= ch ? prev[i - ch] : 0;
                cur[i] = (byte)(cur[i] + f[0] switch { 0 => 0, 1 => a, 2 => b, 3 => (a + b) / 2, 4 => Paeth(a, b, c), _ => throw new InvalidDataException("Unbekannter PNG-Filter.") });
            }
            for (var x = 0; x < w; x++)
            {
                var o = (y * w + x) * 4; var s = x * ch;
                switch (type)
                {
                    case 0: r.Px[o] = r.Px[o + 1] = r.Px[o + 2] = cur[s]; r.Px[o + 3] = trns is { Length: >= 2 } && trns[1] == cur[s] ? (byte)0 : (byte)255; break;
                    case 2: r.Px[o] = cur[s]; r.Px[o + 1] = cur[s + 1]; r.Px[o + 2] = cur[s + 2]; r.Px[o + 3] = 255; break;
                    case 3: var k = cur[s] * 3; if (pal is null || k + 2 >= pal.Length) throw new InvalidDataException("PNG-Palette fehlt."); r.Px[o] = pal[k]; r.Px[o + 1] = pal[k + 1]; r.Px[o + 2] = pal[k + 2]; r.Px[o + 3] = trns is not null && cur[s] < trns.Length ? trns[cur[s]] : (byte)255; break;
                    case 4: r.Px[o] = r.Px[o + 1] = r.Px[o + 2] = cur[s]; r.Px[o + 3] = cur[s + 1]; break;
                    default: Buffer.BlockCopy(cur, s, r.Px, o, 4); break;
                }
            }
            (prev, cur) = (cur, prev);
        }
        return r;
    }
    static int Paeth(int a, int b, int c) { int p = a + b - c, pa = Math.Abs(p - a), pb = Math.Abs(p - b), pc = Math.Abs(p - c); return pa <= pb && pa <= pc ? a : pb <= pc ? b : c; }
}

/// <summary>K1-05: Mischarten nach den offenen W3C-Compositing-Formeln (nicht vormultipliziert).</summary>
public static class PaintBlend
{
    public static readonly (string Id, string Name)[] Modes =
    [
        ("normal", "Normal"), ("multiply", "Multiplizieren"), ("screen", "Negativ multiplizieren"), ("overlay", "Überlagern"),
        ("darken", "Abdunkeln"), ("lighten", "Aufhellen"), ("add", "Addieren"), ("difference", "Differenz"), ("color-dodge", "Farbig abwedeln"), ("soft-light", "Weiches Licht"),
    ];
    public static string Norm(string? id) => Modes.Any(m => m.Id == id) ? id! : "normal";

    static double B(string mode, double cb, double cs) => mode switch
    {
        "multiply" => cb * cs,
        "screen" => cb + cs - cb * cs,
        "overlay" => cb <= 0.5 ? 2 * cs * cb : 1 - 2 * (1 - cs) * (1 - cb),
        "darken" => Math.Min(cb, cs),
        "lighten" => Math.Max(cb, cs),
        "add" => Math.Min(1, cb + cs),
        "difference" => Math.Abs(cb - cs),
        "color-dodge" => cb == 0 ? 0 : cs >= 1 ? 1 : Math.Min(1, cb / (1 - cs)),
        "soft-light" => cs <= 0.5 ? cb - (1 - 2 * cs) * cb * (1 - cb) : cb + (2 * cs - 1) * ((cb <= 0.25 ? ((16 * cb - 12) * cb + 4) * cb : Math.Sqrt(cb)) - cb),
        _ => cs,
    };

    /// <summary>Quelle (Ebene) mit Deckkraft und Mischart über das Ziel legen, nur im Rechteck <paramref name="x0"/>…<paramref name="x1"/> (exklusiv).</summary>
    public static void Over(Pixmap dst, Pixmap src, double opacity, string mode, int x0 = 0, int y0 = 0, int x1 = int.MaxValue, int y1 = int.MaxValue)
    {
        x0 = Math.Max(0, x0); y0 = Math.Max(0, y0); x1 = Math.Min(dst.W, x1); y1 = Math.Min(dst.H, y1);
        mode = Norm(mode); var plain = mode == "normal";
        for (var y = y0; y < y1; y++)
            for (var x = x0; x < x1; x++)
            {
                var i = (y * dst.W + x) * 4; var a_s = src.Px[i + 3] / 255.0 * opacity; if (a_s <= 0) continue;
                var a_b = dst.Px[i + 3] / 255.0; var a_o = a_s + a_b * (1 - a_s);
                for (var c = 0; c < 3; c++)
                {
                    double cs = src.Px[i + c] / 255.0, cb = dst.Px[i + c] / 255.0;
                    var mix = plain ? cs : (1 - a_b) * cs + a_b * B(mode, cb, cs);
                    var co = (a_s * mix + a_b * cb * (1 - a_s)) / a_o;
                    dst.Px[i + c] = (byte)Math.Round(Math.Clamp(co, 0, 1) * 255);
                }
                dst.Px[i + 3] = (byte)Math.Round(a_o * 255);
            }
    }
}

/// <summary>K1-03: Pinsel-Voreinstellung. Größe in Bildpunkten, Härte 0 (weich) … 1 (hart), Abstand als Anteil der Größe, Fluss = Deckkraft je Tupfer.</summary>
public sealed record BrushPreset(string Id, string Name, string Icon, double Size, double Opacity, double Hardness, double Spacing, double Flow,
    bool PressureSize, bool PressureOpacity, bool Erase = false, bool BuildUp = false, double Grain = 0, double MinSize = 0.15)
{
    public static readonly BrushPreset[] All =
    [
        new("bleistift", "Bleistift", "✏", 4, 0.85, 0.75, 0.15, 1, true, true, Grain: 0.35, MinSize: 0.4),
        new("tusche", "Tusche", "🖋", 6, 1, 0.95, 0.08, 1, true, false, MinSize: 0.1),
        new("pinsel", "Malpinsel", "🖌", 22, 1, 0.55, 0.12, 0.6, true, true, BuildUp: true, MinSize: 0.35),
        new("airbrush", "Airbrush", "💨", 60, 1, 0, 0.1, 0.06, false, true, BuildUp: true),
        new("marker", "Marker", "🖍", 20, 0.55, 0.85, 0.1, 1, false, false, MinSize: 1),
        new("aquarell", "Aquarell", "💧", 40, 0.7, 0.15, 0.1, 0.12, true, true, BuildUp: true, Grain: 0.15, MinSize: 0.5),
        new("kreide", "Kreide", "▤", 16, 0.9, 0.6, 0.1, 0.8, true, true, Grain: 0.6, MinSize: 0.5),
        new("radierer", "Radierer", "◌", 30, 1, 0.7, 0.1, 1, true, false, Erase: true, MinSize: 0.4),
    ];
    public static BrushPreset Get(string? id) => All.FirstOrDefault(b => b.Id == id) ?? All[1];
}

/// <summary>K3-01: Spiegeln – senkrecht, waagrecht, beides oder radial in <see cref="Parts"/> Teile um die Mitte.</summary>
public sealed record PaintSymmetry(string Mode = "aus", double Cx = 0, double Cy = 0, int Parts = 6)
{
    public static readonly (string Id, string Name)[] Modes = [("aus", "Aus"), ("senkrecht", "Senkrecht (links ↔ rechts)"), ("waagrecht", "Waagrecht (oben ↕ unten)"), ("beide", "Beide Achsen"), ("radial", "Radial (Mandala)")];
    public IEnumerable<(double X, double Y)> Mirror(double x, double y)
    {
        yield return (x, y);
        switch (Mode)
        {
            case "senkrecht": yield return (2 * Cx - x, y); break;
            case "waagrecht": yield return (x, 2 * Cy - y); break;
            case "beide": yield return (2 * Cx - x, y); yield return (x, 2 * Cy - y); yield return (2 * Cx - x, 2 * Cy - y); break;
            case "radial":
                var n = Math.Clamp(Parts, 2, 32); double dx = x - Cx, dy = y - Cy;
                for (var k = 1; k < n; k++) { var a = 2 * Math.PI * k / n; yield return (Cx + dx * Math.Cos(a) - dy * Math.Sin(a), Cy + dx * Math.Sin(a) + dy * Math.Cos(a)); }
                break;
        }
    }
}

/// <summary>K3-02/K3-03: Zeichenhilfen. Lineal = feste Gerade; Perspektive = Linien rasten auf den passenden Fluchtpunkt ein
/// (bei einem Fluchtpunkt zusätzlich waagrecht/senkrecht). Die Entscheidung fällt, sobald der Strich ein paar Bildpunkte lang ist.</summary>
public sealed record DrawAssist(string Mode = "aus", IReadOnlyList<(double X, double Y)>? Points = null)
{
    public static readonly (string Id, string Name)[] Modes = [("aus", "Aus"), ("gerade", "Gerade Linie"), ("lineal", "Lineal"), ("perspektive1", "Perspektive 1 Punkt"), ("perspektive2", "Perspektive 2 Punkte"), ("perspektive3", "Perspektive 3 Punkte")];
    public const double Decide = 6;
    public IReadOnlyList<(double X, double Y)> Pts => Points ?? [];

    /// <summary>Richtungen, auf die vom Startpunkt aus eingerastet werden kann (Einheitsvektoren).</summary>
    public List<(double X, double Y)> Directions((double X, double Y) start)
    {
        var res = new List<(double X, double Y)>();
        static (double, double) U(double x, double y) { var l = Math.Sqrt(x * x + y * y); return l < 1e-9 ? (1, 0) : (x / l, y / l); }
        switch (Mode)
        {
            case "lineal" when Pts.Count >= 2: res.Add(U(Pts[1].X - Pts[0].X, Pts[1].Y - Pts[0].Y)); break;
            case "perspektive1" or "perspektive2" or "perspektive3":
                var need = Mode[^1] - '0';
                foreach (var p in Pts.Take(need)) res.Add(U(p.X - start.X, p.Y - start.Y));
                if (need == 1) { res.Add((1, 0)); res.Add((0, 1)); }
                if (need == 2) res.Add((0, 1)); // Senkrechte bleiben senkrecht
                break;
        }
        return res;
    }

    /// <summary>Punkt <paramref name="p"/> auf die gewählte Gerade durch den Startpunkt legen. <paramref name="dir"/> wird beim ersten Aufruf mit genug Weg festgelegt.</summary>
    public (double X, double Y) Snap((double X, double Y) start, (double X, double Y) p, ref (double X, double Y)? dir)
    {
        if (Mode == "aus") return p;
        double dx = p.X - start.X, dy = p.Y - start.Y;
        if (dir is null)
        {
            if (Math.Sqrt(dx * dx + dy * dy) < Decide) return p;
            List<(double X, double Y)> dirs = Mode == "gerade" ? [(dx / Math.Sqrt(dx * dx + dy * dy), dy / Math.Sqrt(dx * dx + dy * dy))] : Directions(start);
            if (dirs.Count == 0) return p;
            var len = Math.Sqrt(dx * dx + dy * dy);
            dir = dirs.OrderByDescending(d => Math.Abs((d.X * dx + d.Y * dy) / len)).First();
        }
        if (Mode == "gerade") return p; // gerade Linie: nur Anfang und Ende zählen
        var (ux, uy) = dir.Value; var t = dx * ux + dy * uy;
        return (start.X + t * ux, start.Y + t * uy);
    }
}

/// <summary>K3-04: Glätten wie „StreamLine“ – der gezeichnete Punkt folgt dem Stift verzögert. Stärke 0 = aus, 1 = sehr ruhig.</summary>
public sealed class Smoother(double strength)
{
    readonly double _k = 1 - Math.Clamp(strength, 0, 1) * 0.92;
    (double X, double Y, double P)? _s;
    public (double X, double Y, double P) Push(double x, double y, double p)
    {
        if (_s is not { } s) { _s = (x, y, p); return (x, y, p); }
        var n = (s.X + (x - s.X) * _k, s.Y + (y - s.Y) * _k, s.P + (p - s.P) * _k); _s = n; return n;
    }
}

/// <summary>K1-01/K1-02: Strich aus Tupfern. Arbeitet „indirekt“: Der Strich sammelt seine Deckung in einer eigenen Maske und wird
/// erst darüber auf den Stand vor dem Strich gelegt – so baut sich ein Marker nicht auf, Airbrush/Aquarell aber schon.</summary>
public sealed class StrokeEngine
{
    readonly Pixmap _layer, _before; readonly float[] _mask; readonly BrushPreset _b; readonly double _size, _opacity;
    readonly byte _r, _g, _bb; readonly PaintSymmetry _sym; readonly DrawAssist _assist; readonly Smoother _smooth; readonly bool _alphaLock;
    (double X, double Y, double P)? _last; (double X, double Y)? _start; (double X, double Y)? _dir; double _carry; uint _seed = 2463534242;
    public int DirtyX0 { get; private set; } = int.MaxValue; public int DirtyY0 { get; private set; } = int.MaxValue;
    public int DirtyX1 { get; private set; } = -1; public int DirtyY1 { get; private set; } = -1;
    public int Dabs { get; private set; }
    int _px0 = int.MaxValue, _py0 = int.MaxValue, _px1 = -1, _py1 = -1;
    /// <summary>Geänderter Bereich seit dem letzten Aufruf (x0, y0, x1, y1 einschließlich) – für schnelles Neuzeichnen; null = nichts geändert.</summary>
    public (int X0, int Y0, int X1, int Y1)? TakeDirty()
    {
        if (_px1 < 0) return null;
        var r = (_px0, _py0, _px1, _py1); _px0 = _py0 = int.MaxValue; _px1 = _py1 = -1; return r;
    }
    void Mark(int x0, int y0, int x1, int y1) { _px0 = Math.Min(_px0, x0); _py0 = Math.Min(_py0, y0); _px1 = Math.Max(_px1, x1); _py1 = Math.Max(_py1, y1); }
    /// <summary>Ebene, auf der gemalt wird.</summary>
    public Pixmap Layer => _layer;

    public StrokeEngine(Pixmap layer, BrushPreset brush, string color, double? size = null, double? opacity = null, PaintSymmetry? sym = null, DrawAssist? assist = null, double smoothing = 0, bool alphaLock = false)
    {
        _layer = layer; _before = layer.Clone(); _mask = new float[layer.W * layer.H]; _b = brush;
        _size = Math.Clamp(size ?? brush.Size, 0.5, 1000); _opacity = Math.Clamp(opacity ?? brush.Opacity, 0.01, 1);
        (_r, _g, _bb) = PaintColors.Rgb(color); _sym = sym ?? new(); _assist = assist ?? new(); _smooth = new(smoothing); _alphaLock = alphaLock;
    }

    /// <summary>Stiftpunkt mit Druck 0…1 (Maus: 1).</summary>
    public void Add(double x, double y, double pressure = 1)
    {
        var (sx, sy, sp) = _smooth.Push(x, y, Math.Clamp(pressure, 0, 1));
        _start ??= (sx, sy);
        var (ax, ay) = _assist.Mode == "gerade" ? (sx, sy) : _assist.Snap(_start.Value, (sx, sy), ref _dir);
        if (_assist.Mode == "gerade") { ReStraight(ax, ay, sp); return; }
        Seg(ax, ay, sp);
    }

    void ReStraight(double x, double y, double p)
    {   // gerade Linie: bei jeder Bewegung neu vom Startpunkt aus zeichnen
        Array.Clear(_mask); Buffer.BlockCopy(_before.Px, 0, _layer.Px, 0, _before.Px.Length); _last = null; _carry = 0;
        if (DirtyX1 >= 0) Mark(DirtyX0, DirtyY0, DirtyX1, DirtyY1); // alte Linie verschwindet
        Seg(_start!.Value.X, _start.Value.Y, p); Seg(x, y, p);
    }

    void Seg(double x, double y, double p)
    {
        if (_last is not { } l) { Dab(x, y, p); _last = (x, y, p); return; }
        double dx = x - l.X, dy = y - l.Y, dist = Math.Sqrt(dx * dx + dy * dy);
        var step = Math.Max(0.5, Radius(Math.Max(p, l.P)) * 2 * _b.Spacing);
        var t = step - _carry;
        while (t <= dist) { var f = t / dist; Dab(l.X + dx * f, l.Y + dy * f, l.P + (p - l.P) * f); t += step; }
        _carry = dist - (t - step); _last = (x, y, p);
    }

    double Radius(double p) => Math.Max(0.5, _size / 2 * (_b.PressureSize ? _b.MinSize + (1 - _b.MinSize) * p : 1));

    void Dab(double cx, double cy, double p)
    {
        foreach (var (mx, my) in _sym.Mirror(cx, cy)) Stamp(mx, my, p);
    }

    float Rnd() { _seed ^= _seed << 13; _seed ^= _seed >> 17; _seed ^= _seed << 5; return (_seed & 0xFFFFFF) / (float)0xFFFFFF; }

    void Stamp(double cx, double cy, double p)
    {
        Dabs++;
        var rad = Radius(p); var flow = _b.Flow * (_b.PressureOpacity ? Math.Max(0.05, p) : 1);
        int x0 = Math.Max(0, (int)Math.Floor(cx - rad - 1)), x1 = Math.Min(_layer.W - 1, (int)Math.Ceiling(cx + rad + 1));
        int y0 = Math.Max(0, (int)Math.Floor(cy - rad - 1)), y1 = Math.Min(_layer.H - 1, (int)Math.Ceiling(cy + rad + 1));
        if (x0 > x1 || y0 > y1) return;
        var hard = Math.Clamp(_b.Hardness, 0, 0.999);
        for (var y = y0; y <= y1; y++)
            for (var x = x0; x <= x1; x++)
            {
                double ddx = x + 0.5 - cx, ddy = y + 0.5 - cy, d = Math.Sqrt(ddx * ddx + ddy * ddy) / rad;
                if (d >= 1 + 0.5 / rad) continue;
                double a = d <= hard ? 1 : 1 - Smooth((d - hard) / (1 - hard));
                if (rad < 3) a = Math.Clamp(1 + 0.5 / rad - d, 0, 1) * (d <= hard ? 1 : a); // kleine Spitzen: Kante glätten
                a *= flow; if (_b.Grain > 0) a *= 1 - _b.Grain * Rnd();
                if (a <= 0) continue;
                var k = y * _layer.W + x; var m = _mask[k];
                _mask[k] = (float)(_b.BuildUp ? m + a * (1 - m) : Math.Max(m, a));
                Apply(k);
            }
        DirtyX0 = Math.Min(DirtyX0, x0); DirtyY0 = Math.Min(DirtyY0, y0); DirtyX1 = Math.Max(DirtyX1, x1); DirtyY1 = Math.Max(DirtyY1, y1); Mark(x0, y0, x1, y1);
    }
    static double Smooth(double t) { t = Math.Clamp(t, 0, 1); return t * t * (3 - 2 * t); }

    void Apply(int k)
    {
        var i = k * 4; var a = _mask[k] * _opacity; var ba = _before.Px[i + 3] / 255.0;
        if (_b.Erase) { _layer.Px[i + 3] = (byte)Math.Round(_before.Px[i + 3] * (1 - a)); return; }
        if (_alphaLock)
        {   // Deckkraft sperren: nur dort färben, wo schon etwas ist
            for (var c = 0; c < 3; c++) _layer.Px[i + c] = (byte)Math.Round(_before.Px[i + c] + ((c == 0 ? _r : c == 1 ? _g : _bb) - _before.Px[i + c]) * a);
            return;
        }
        var ao = a + ba * (1 - a);
        if (ao <= 0) return;
        for (var c = 0; c < 3; c++) { var s = c == 0 ? _r : c == 1 ? _g : _bb; _layer.Px[i + c] = (byte)Math.Round((s * a + _before.Px[i + c] * ba * (1 - a)) / ao); }
        _layer.Px[i + 3] = (byte)Math.Round(ao * 255);
    }
}

/// <summary>Farbumrechnung und K2-04 Harmonien (eigene, einfache Formeln: HSV und Farbtonwinkel).</summary>
public static class PaintColors
{
    public static (byte R, byte G, byte B) Rgb(string hex)
    {
        var h = (hex ?? "").Trim().TrimStart('#');
        if (h.Length == 3) h = string.Concat(h.Select(c => $"{c}{c}"));
        if (h.Length == 8) h = h[2..];
        if (h.Length != 6 || !int.TryParse(h, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var v)) throw new FormatException($"„{hex}“ ist keine Farbe. Bitte z. B. #E0475B eingeben.");
        return ((byte)(v >> 16), (byte)(v >> 8), (byte)v);
    }
    public static bool IsHex(string? hex) { try { Rgb(hex ?? ""); return true; } catch (FormatException) { return false; } }
    public static string Hex(byte r, byte g, byte b) => $"#{r:X2}{g:X2}{b:X2}";

    public static (double H, double S, double V) Hsv(string hex)
    {
        var (r, g, b) = Rgb(hex); double rf = r / 255.0, gf = g / 255.0, bf = b / 255.0, max = Math.Max(rf, Math.Max(gf, bf)), min = Math.Min(rf, Math.Min(gf, bf)), d = max - min;
        double h = d == 0 ? 0 : max == rf ? 60 * (((gf - bf) / d) % 6) : max == gf ? 60 * ((bf - rf) / d + 2) : 60 * ((rf - gf) / d + 4);
        return ((h + 360) % 360, max == 0 ? 0 : d / max, max);
    }
    public static string FromHsv(double h, double s, double v)
    {
        h = ((h % 360) + 360) % 360; s = Math.Clamp(s, 0, 1); v = Math.Clamp(v, 0, 1);
        double c = v * s, x = c * (1 - Math.Abs(h / 60 % 2 - 1)), m = v - c;
        var (r, g, b) = h < 60 ? (c, x, 0d) : h < 120 ? (x, c, 0d) : h < 180 ? (0d, c, x) : h < 240 ? (0d, x, c) : h < 300 ? (x, 0d, c) : (c, 0d, x);
        return Hex((byte)Math.Round((r + m) * 255), (byte)Math.Round((g + m) * 255), (byte)Math.Round((b + m) * 255));
    }

    public static readonly (string Id, string Name, double[] Angles)[] Harmonies =
    [
        ("komplementaer", "Komplementär", [180]), ("analog", "Analog", [-30, 30]), ("triade", "Triade", [120, 240]),
        ("teiltriade", "Teil-Komplementär", [150, 210]), ("quadrat", "Quadrat", [90, 180, 270]),
    ];
    /// <summary>Grundfarbe plus die Farben der Harmonie (gleiche Sättigung und Helligkeit, Farbton gedreht).</summary>
    public static List<string> Harmony(string hex, string id)
    {
        var hm = Harmonies.FirstOrDefault(x => x.Id == id); if (hm.Angles is null) throw new ArgumentException("Unbekannte Harmonie: " + id);
        var (h, s, v) = Hsv(hex); return [Hex(Rgb(hex).R, Rgb(hex).G, Rgb(hex).B), .. hm.Angles.Select(a => FromHsv(h + a, s, v))];
    }
}

/// <summary>K2-02: Paletten im offenen GIMP-Palettenformat (.gpl), das auch Krita und Inkscape lesen.</summary>
public static class Palettes
{
    public static string ToGpl(string name, IEnumerable<string> colors)
    {
        var sb = new StringBuilder("GIMP Palette\n").Append("Name: ").Append(name.Replace('\n', ' ')).Append("\nColumns: 8\n#\n");
        foreach (var c in colors) { var (r, g, b) = PaintColors.Rgb(c); sb.Append(CultureInfo.InvariantCulture, $"{r,3} {g,3} {b,3}\t{PaintColors.Hex(r, g, b)}\n"); }
        return sb.ToString();
    }
    public static (string Name, List<string> Colors) FromGpl(string text)
    {
        var lines = text.Replace("\r", "").Split('\n');
        if (lines.Length == 0 || !lines[0].Trim().Equals("GIMP Palette", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Das ist keine Palette im GIMP-Format (.gpl).");
        var name = "Palette"; var res = new List<string>();
        foreach (var l in lines.Skip(1))
        {
            var t = l.Trim(); if (t.Length == 0 || t.StartsWith('#')) continue;
            if (t.StartsWith("Name:", StringComparison.OrdinalIgnoreCase)) { name = t[5..].Trim(); continue; }
            if (t.StartsWith("Columns:", StringComparison.OrdinalIgnoreCase)) continue;
            var p = t.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
            if (p.Length >= 3 && byte.TryParse(p[0], out var r) && byte.TryParse(p[1], out var g) && byte.TryParse(p[2], out var b)) res.Add(PaintColors.Hex(r, g, b));
        }
        return (name, res);
    }
    /// <summary>K2-03: Hauptfarben aus einem Foto (k-means, wiederholbar), häufigste zuerst.</summary>
    public static List<string> FromPhoto(Pixmap img, int k = 6) => [.. VisionKit.MainColors(img.Px, img.W, img.H, Math.Clamp(k, 2, 12)).Select(c => c.Hex.ToUpperInvariant())];
}

/// <summary>Eine Mal-Ebene.</summary>
public sealed class PaintLayer(string id, string name, Pixmap px)
{
    public string Id { get; set; } = id;
    public string Name { get; set; } = name;
    public Pixmap Px { get; set; } = px;
    public double Opacity { get; set; } = 1;
    public bool Visible { get; set; } = true;
    public bool Locked { get; set; }
    public bool AlphaLock { get; set; }
    public string Blend { get; set; } = "normal";
}

/// <summary>Bild aus Ebenen. Im Programm-Dokument (JSON) liegt jede Ebene als PNG (Base64) – so funktionieren Speichern, Versionen und Rückgängig der Programm-Hülle unverändert.</summary>
public sealed class PaintDoc
{
    public int W { get; }
    public int H { get; }
    public string Background { get; set; } = "#FFFFFF";
    public List<PaintLayer> Layers { get; } = [];
    public PaintDoc(int w, int h, string background = "#FFFFFF") { if (w < 1 || h < 1 || (long)w * h > 64_000_000) throw new ArgumentException("Die Leinwand muss zwischen 1 × 1 und 64 Millionen Bildpunkten groß sein."); W = w; H = h; Background = background; }

    public PaintLayer Add(string? name = null, int? index = null)
    {
        var l = new PaintLayer(NewId(), name ?? $"Ebene {Layers.Count + 1}", new Pixmap(W, H));
        Layers.Insert(Math.Clamp(index ?? Layers.Count, 0, Layers.Count), l); return l;
    }
    string NewId() { var n = Layers.Count + 1; while (Layers.Any(l => l.Id == "e" + n)) n++; return "e" + n; }
    public PaintLayer Get(string id) => Layers.FirstOrDefault(l => l.Id == id) ?? throw new InvalidOperationException("Diese Ebene gibt es nicht mehr.");
    public void Remove(string id) { if (Layers.Count <= 1) throw new InvalidOperationException("Die letzte Ebene bleibt – lösche lieber ihren Inhalt."); Layers.Remove(Get(id)); }
    public void Move(string id, int by) { var l = Get(id); var i = Layers.IndexOf(l); var j = Math.Clamp(i + by, 0, Layers.Count - 1); Layers.RemoveAt(i); Layers.Insert(j, l); }
    public PaintLayer Duplicate(string id) { var s = Get(id); var l = Add(s.Name + " Kopie", Layers.IndexOf(s) + 1); l.Px = s.Px.Clone(); l.Opacity = s.Opacity; l.Blend = s.Blend; return l; }
    /// <summary>Ebene mit der darunter verbinden (Mischart und Deckkraft werden eingerechnet).</summary>
    public void MergeDown(string id)
    {
        var l = Get(id); var i = Layers.IndexOf(l); if (i == 0) throw new InvalidOperationException("Unter dieser Ebene liegt keine weitere.");
        var below = Layers[i - 1]; var tmp = below.Px.Clone(); PaintBlend.Over(tmp, l.Px, l.Opacity, l.Blend); below.Px = tmp; Layers.RemoveAt(i);
    }

    /// <summary>Alles zusammen (mit Hintergrund, wenn <paramref name="withBackground"/>); nur im Rechteck neu rechnen, wenn angegeben.</summary>
    public Pixmap Composite(bool withBackground = true, Pixmap? into = null, int x0 = 0, int y0 = 0, int x1 = int.MaxValue, int y1 = int.MaxValue)
    {
        var r = into ?? new Pixmap(W, H);
        x0 = Math.Max(0, x0); y0 = Math.Max(0, y0); x1 = Math.Min(W, x1); y1 = Math.Min(H, y1);
        var bg = withBackground && PaintColors.IsHex(Background) ? PaintColors.Rgb(Background) : ((byte)0, (byte)0, (byte)0);
        for (var y = y0; y < y1; y++)
            for (var x = x0; x < x1; x++) { var i = (y * W + x) * 4; r.Px[i] = bg.Item1; r.Px[i + 1] = bg.Item2; r.Px[i + 2] = bg.Item3; r.Px[i + 3] = withBackground ? (byte)255 : (byte)0; }
        foreach (var l in Layers.Where(l => l.Visible && l.Opacity > 0)) PaintBlend.Over(r, l.Px, l.Opacity, l.Blend, x0, y0, x1, y1);
        return r;
    }

    // ---------- Programm-Dokument (JSON) ----------
    public static JsonObject NewJson(int w, int h, string background = "#FFFFFF", string firstLayer = "Skizze")
    {
        var d = new PaintDoc(w, h, background); d.Add(firstLayer); var j = d.ToJson(); j["active"] = d.Layers[0].Id; return j;
    }
    public JsonObject ToJson(JsonObject? into = null)
    {
        var j = into ?? new JsonObject();
        j["width"] = W; j["height"] = H; j["background"] = Background;
        var old = (j["layers"] as JsonArray)?.OfType<JsonObject>().ToDictionary(o => (string?)o["id"] ?? "", o => o) ?? [];
        j["layers"] = new JsonArray([.. Layers.Select(l => (JsonNode)new JsonObject
        {
            ["id"] = l.Id, ["name"] = l.Name, ["opacity"] = Math.Round(l.Opacity, 3), ["visible"] = l.Visible, ["locked"] = l.Locked, ["alphaLock"] = l.AlphaLock, ["blend"] = l.Blend,
            ["png"] = Convert.ToBase64String(PngCodec.Encode(l.Px)),
        })]);
        return j;
    }
    public static PaintDoc FromJson(JsonObject j)
    {
        var d = new PaintDoc(Int(j["width"], 1600), Int(j["height"], 1200), (string?)j["background"] ?? "#FFFFFF");
        foreach (var o in (j["layers"] as JsonArray)?.OfType<JsonObject>() ?? [])
        {
            var png = (string?)o["png"]; var r = string.IsNullOrEmpty(png) ? new Pixmap(d.W, d.H) : PngCodec.Decode(Convert.FromBase64String(png));
            if (r.W != d.W || r.H != d.H) r = r.Resize(d.W, d.H);
            d.Layers.Add(new PaintLayer((string?)o["id"] ?? $"e{d.Layers.Count + 1}", (string?)o["name"] ?? "Ebene", r)
            { Opacity = Num(o["opacity"], 1), Visible = (bool?)o["visible"] ?? true, Locked = (bool?)o["locked"] ?? false, AlphaLock = (bool?)o["alphaLock"] ?? false, Blend = PaintBlend.Norm((string?)o["blend"]) });
        }
        if (d.Layers.Count == 0) d.Add("Ebene 1");
        return d;
    }
    static int Int(JsonNode? n, int def) => (int)Programs.ProgramDocument.ToNumber(n, def);
    static double Num(JsonNode? n, double def) => Programs.ProgramDocument.ToNumber(n, def);

    /// <summary>Nur die PNG einer Ebene im JSON ersetzen (nach einem Strich) – schneller als alles neu zu schreiben.</summary>
    public static void StoreLayer(JsonObject j, PaintLayer l)
    {
        var o = (j["layers"] as JsonArray)?.OfType<JsonObject>().FirstOrDefault(x => (string?)x["id"] == l.Id) ?? throw new InvalidOperationException("Diese Ebene gibt es nicht mehr.");
        o["png"] = Convert.ToBase64String(PngCodec.Encode(l.Px));
    }
}

/// <summary>K1-07: Speichern und Laden im offenen OpenRaster-Format (.ora) und Export als Photoshop-Datei (.psd, mit Ebenen).</summary>
public static class PaintFiles
{
    static string OraMode(string b) => b switch
    {
        "multiply" => "svg:multiply", "screen" => "svg:screen", "overlay" => "svg:overlay", "darken" => "svg:darken", "lighten" => "svg:lighten",
        "add" => "svg:plus", "difference" => "svg:difference", "color-dodge" => "svg:color-dodge", "soft-light" => "svg:soft-light", _ => "svg:src-over",
    };
    static string FromOra(string? m) => m switch
    {
        "svg:multiply" => "multiply", "svg:screen" => "screen", "svg:overlay" => "overlay", "svg:darken" => "darken", "svg:lighten" => "lighten",
        "svg:plus" => "add", "svg:difference" => "difference", "svg:color-dodge" => "color-dodge", "svg:soft-light" => "soft-light", _ => "normal",
    };

    public static void SaveOra(PaintDoc d, string path)
    {
        using var fs = File.Create(path);
        using var z = new ZipArchive(fs, ZipArchiveMode.Create);
        var mt = z.CreateEntry("mimetype", CompressionLevel.NoCompression); // muss zuerst und unkomprimiert stehen
        using (var s = mt.Open()) s.Write(Encoding.ASCII.GetBytes("image/openraster"));
        var stack = new XElement("stack");
        var n = 0;
        foreach (var l in Enumerable.Reverse(d.Layers)) // in stack.xml steht die oberste Ebene zuerst
        {
            var src = $"data/ebene{++n}.png";
            stack.Add(new XElement("layer", new XAttribute("name", l.Name), new XAttribute("src", src), new XAttribute("x", 0), new XAttribute("y", 0),
                new XAttribute("opacity", l.Opacity.ToString("0.###", CultureInfo.InvariantCulture)), new XAttribute("visibility", l.Visible ? "visible" : "hidden"),
                new XAttribute("composite-op", OraMode(l.Blend)), new XAttribute("edit-locked", l.Locked ? "true" : "false")));
            Put(z, src, PngCodec.Encode(l.Px, fast: false));
        }
        stack.Add(new XElement("layer", new XAttribute("name", "Hintergrund"), new XAttribute("src", "data/hintergrund.png"), new XAttribute("x", 0), new XAttribute("y", 0), new XAttribute("opacity", "1"), new XAttribute("visibility", "visible")));
        var bg = new Pixmap(d.W, d.H); var (r, g, b) = PaintColors.IsHex(d.Background) ? PaintColors.Rgb(d.Background) : ((byte)255, (byte)255, (byte)255); bg.Fill(r, g, b, 255);
        Put(z, "data/hintergrund.png", PngCodec.Encode(bg, fast: false));
        var xml = new XDocument(new XElement("image", new XAttribute("version", "0.0.5"), new XAttribute("w", d.W), new XAttribute("h", d.H), stack));
        Put(z, "stack.xml", Encoding.UTF8.GetBytes(xml.ToString()));
        var merged = d.Composite(); Put(z, "mergedimage.png", PngCodec.Encode(merged, fast: false));
        var tw = Math.Min(256, d.W); var th = Math.Max(1, (int)Math.Round((double)d.H * tw / d.W)); if (th > 256) { th = 256; tw = Math.Max(1, (int)Math.Round((double)d.W * th / d.H)); }
        Put(z, "Thumbnails/thumbnail.png", PngCodec.Encode(merged.Resize(tw, th)));
    }
    static void Put(ZipArchive z, string name, byte[] data) { using var s = z.CreateEntry(name, CompressionLevel.Fastest).Open(); s.Write(data); }

    /// <summary>ORA laden. Eine Ebene namens „Hintergrund“ ganz unten in einer Farbe wird wieder zur Hintergrundfarbe. Gruppen werden flach gelesen.</summary>
    public static PaintDoc LoadOra(string path)
    {
        using var z = ZipFile.OpenRead(path);
        var se = z.GetEntry("stack.xml") ?? throw new InvalidDataException("Das ist keine OpenRaster-Datei (stack.xml fehlt).");
        XDocument x; using (var s = se.Open()) x = XDocument.Load(s);
        var img = x.Root ?? throw new InvalidDataException("stack.xml ist leer.");
        int w = (int?)img.Attribute("w") ?? 0, h = (int?)img.Attribute("h") ?? 0;
        var d = new PaintDoc(w, h);
        var layers = img.Descendants("layer").Reverse().ToList(); // unterste zuerst
        foreach (var le in layers)
        {
            var src = (string?)le.Attribute("src") ?? ""; var e = z.GetEntry(src); if (e is null) continue;
            if (src.Contains("..")) continue;
            Pixmap r; using (var s = e.Open()) using (var ms = new MemoryStream()) { s.CopyTo(ms); r = PngCodec.Decode(ms.ToArray()); }
            var full = new Pixmap(w, h); int ox = (int?)le.Attribute("x") ?? 0, oy = (int?)le.Attribute("y") ?? 0;
            for (var yy = 0; yy < r.H; yy++) for (var xx = 0; xx < r.W; xx++) { int tx = xx + ox, ty = yy + oy; if (tx < 0 || ty < 0 || tx >= w || ty >= h) continue; Buffer.BlockCopy(r.Px, (yy * r.W + xx) * 4, full.Px, (ty * w + tx) * 4, 4); }
            var name = (string?)le.Attribute("name") ?? "Ebene";
            if (d.Layers.Count == 0 && name == "Hintergrund" && Uniform(full) is { } bgc) { d.Background = bgc; continue; }
            d.Layers.Add(new PaintLayer($"e{d.Layers.Count + 1}", name, full)
            {
                Opacity = double.TryParse((string?)le.Attribute("opacity"), NumberStyles.Float, CultureInfo.InvariantCulture, out var o) ? Math.Clamp(o, 0, 1) : 1,
                Visible = (string?)le.Attribute("visibility") != "hidden", Blend = FromOra((string?)le.Attribute("composite-op")), Locked = (string?)le.Attribute("edit-locked") == "true",
            });
        }
        if (d.Layers.Count == 0) d.Add("Ebene 1");
        return d;
    }
    static string? Uniform(Pixmap r)
    {
        var (a, b, c, al) = r.At(0, 0); if (al != 255) return null;
        for (var i = 0; i < r.Px.Length; i += 4) if (r.Px[i] != a || r.Px[i + 1] != b || r.Px[i + 2] != c || r.Px[i + 3] != 255) return null;
        return PaintColors.Hex(a, b, c);
    }

    static string PsdMode(string b) => b switch
    {
        "multiply" => "mul ", "screen" => "scrn", "overlay" => "over", "darken" => "dark", "lighten" => "lite", "add" => "lddg",
        "difference" => "diff", "color-dodge" => "div ", "soft-light" => "sLit", _ => "norm",
    };

    /// <summary>Photoshop-Datei (Version 1, 8 Bit, RGB) mit allen Ebenen und dem Gesamtbild, ohne Kompression – nach der öffentlichen Formatbeschreibung.</summary>
    public static byte[] Psd(PaintDoc d)
    {
        using var ms = new MemoryStream(); var w = new BinaryWriterBe(ms);
        w.Ascii("8BPS"); w.U16(1); w.Bytes(new byte[6]); w.U16(4); w.U32((uint)d.H); w.U32((uint)d.W); w.U16(8); w.U16(3);
        w.U32(0); // Farbmodus-Daten
        w.U32(0); // Bildressourcen
        using var lm = new MemoryStream(); var l = new BinaryWriterBe(lm);
        using var li = new MemoryStream(); var i = new BinaryWriterBe(li);
        i.I16((short)d.Layers.Count);
        var chanLen = (uint)(2 + d.W * d.H);
        foreach (var layer in d.Layers)
        {
            i.I32(0); i.I32(0); i.I32(d.H); i.I32(d.W); i.U16(4);
            foreach (var id in new short[] { -1, 0, 1, 2 }) { i.I16(id); i.U32(chanLen); }
            i.Ascii("8BIM"); i.Ascii(PsdMode(layer.Blend)); i.U8((byte)Math.Round(layer.Opacity * 255)); i.U8(0); i.U8((byte)((layer.Visible ? 0 : 2) | (layer.Locked ? 1 : 0))); i.U8(0);
            var ascii = string.Concat(layer.Name.Select(c => c is >= ' ' and < (char)128 ? c : '_'));
            var name = Encoding.ASCII.GetBytes(ascii[..Math.Min(255, ascii.Length)]);
            var nameLen = 1 + name.Length; var pad = (4 - nameLen % 4) % 4;
            i.U32((uint)(4 + 4 + nameLen + pad)); i.U32(0); i.U32(0); i.U8((byte)name.Length); i.Bytes(name); i.Bytes(new byte[pad]);
        }
        foreach (var layer in d.Layers)
            foreach (var c in new[] { 3, 0, 1, 2 })
            {
                i.U16(0); var px = layer.Px.Px; var buf = new byte[d.W * d.H];
                for (var p = 0; p < buf.Length; p++) buf[p] = px[p * 4 + c];
                i.Bytes(buf);
            }
        if (li.Length % 2 == 1) i.U8(0);
        l.U32((uint)li.Length); l.Bytes(li.ToArray()); l.U32(0); // globale Maske
        w.U32((uint)lm.Length); w.Bytes(lm.ToArray());
        w.U16(0); var merged = d.Composite();
        foreach (var c in new[] { 0, 1, 2, 3 }) { var buf = new byte[d.W * d.H]; for (var p = 0; p < buf.Length; p++) buf[p] = merged.Px[p * 4 + c]; w.Bytes(buf); }
        return ms.ToArray();
    }

    sealed class BinaryWriterBe(Stream s)
    {
        public void U8(byte v) => s.WriteByte(v);
        public void U16(ushort v) { s.WriteByte((byte)(v >> 8)); s.WriteByte((byte)v); }
        public void I16(short v) => U16((ushort)v);
        public void U32(uint v) { s.WriteByte((byte)(v >> 24)); s.WriteByte((byte)(v >> 16)); s.WriteByte((byte)(v >> 8)); s.WriteByte((byte)v); }
        public void I32(int v) => U32((uint)v);
        public void Ascii(string t) => s.Write(Encoding.ASCII.GetBytes(t));
        public void Bytes(byte[] b) => s.Write(b);
    }
}
