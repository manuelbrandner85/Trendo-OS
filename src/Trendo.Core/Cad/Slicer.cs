namespace Trendo.Core.Cad;

/// <summary>Eigenes Slicen wie PrusaSlicer, Cura und Bambu Studio (nur Funktionen, Clean-Room, 04.11.2026): Schnittkonturen zu Umrissen verbinden,
/// Füllung als Linienraster (Winkel wechselt je Schicht), Material mit Dichte und Preis, Zeit und Filament aus den Bahnen,
/// Stützen-Schätzung, Skalieren, Kopien und Anordnen auf der Platte. G-Code schreibt weiterhin der Fachslicer.</summary>
public static class Slicer
{
    public sealed record Material(string Id, string Name, double Density, double EuroPerKg, int Nozzle, int Bed);
    public static readonly Material[] Materials =
    [
        new("pla", "PLA", 1.24, 20, 220, 60), new("petg", "PETG", 1.27, 22, 245, 75), new("abs", "ABS", 1.04, 25, 255, 100),
        new("tpu", "TPU (weich)", 1.21, 32, 225, 40), new("asa", "ASA", 1.07, 28, 260, 100),
    ];
    public static readonly double[] LayerHeights = [0.12, 0.16, 0.2, 0.24, 0.28];

    /// <summary>Einstellungen wie im Slicer: Schichthöhe, Wände, Füllung in Prozent, Material, Linienbreite, massive Ober-/Unterschichten.</summary>
    public sealed record Settings(double Layer = 0.2, int Walls = 2, int Infill = 15, string Material = "pla", double LineWidth = 0.42, int Solid = 3,
        double WallSpeed = 150, double InfillSpeed = 200, double Travel = 300)
    {
        public Material Mat => Materials.FirstOrDefault(m => m.Id == Material) ?? Materials[0];
    }

    public sealed record Result(int Layers, double WallMm, double InfillMm, double FilamentM, double Grams, double Euro, TimeSpan Time, double SupportGrams);

    /// <summary>Schnittsegmente zu geschlossenen Umrissen verbinden (Endpunkte innerhalb <paramref name="eps"/> mm gelten als gleich).</summary>
    public static List<List<(double X, double Y)>> Loops(IReadOnlyList<(double X1, double Y1, double X2, double Y2)> segs, double eps = 1e-3)
    {
        var left = segs.Select(s => ((s.X1, s.Y1), (s.X2, s.Y2))).ToList();
        var loops = new List<List<(double X, double Y)>>();
        static bool Near((double X, double Y) a, (double X, double Y) b, double e) => Math.Abs(a.X - b.X) <= e && Math.Abs(a.Y - b.Y) <= e;
        while (left.Count > 0)
        {
            var (a, b) = left[^1]; left.RemoveAt(left.Count - 1);
            var loop = new List<(double X, double Y)> { a, b };
            for (var grew = true; grew && !Near(loop[0], loop[^1], eps);)
            {
                grew = false;
                for (var i = left.Count - 1; i >= 0; i--)
                {
                    var (p, q) = left[i];
                    if (Near(p, loop[^1], eps)) { loop.Add(q); left.RemoveAt(i); grew = true; break; }
                    if (Near(q, loop[^1], eps)) { loop.Add(p); left.RemoveAt(i); grew = true; break; }
                }
            }
            if (loop.Count > 3 && Near(loop[0], loop[^1], eps)) { loop.RemoveAt(loop.Count - 1); loops.Add(loop); }
        }
        return loops;
    }

    public static double Perimeter(IReadOnlyList<(double X, double Y)> loop)
    {
        double s = 0; for (var i = 0; i < loop.Count; i++) { var a = loop[i]; var b = loop[(i + 1) % loop.Count]; s += Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y)); }
        return s;
    }

    /// <summary>Fläche nach der Schuhbandformel (Betrag, mm²).</summary>
    public static double Area(IReadOnlyList<(double X, double Y)> loop)
    {
        double s = 0; for (var i = 0; i < loop.Count; i++) { var a = loop[i]; var b = loop[(i + 1) % loop.Count]; s += a.X * b.Y - b.X * a.Y; }
        return Math.Abs(s) / 2;
    }

    /// <summary>Füllung als Linienraster im Abstand <paramref name="spacing"/> unter <paramref name="angleDeg"/>; innen/außen nach der Gerade-Ungerade-Regel,
    /// damit Löcher frei bleiben.</summary>
    public static List<(double X1, double Y1, double X2, double Y2)> Infill(IReadOnlyList<IReadOnlyList<(double X, double Y)>> loops, double spacing, double angleDeg)
    {
        var res = new List<(double, double, double, double)>();
        if (loops.Count == 0 || spacing <= 0) return res;
        double c = Math.Cos(angleDeg * Math.PI / 180), s = Math.Sin(angleDeg * Math.PI / 180);
        (double X, double Y) Rot((double X, double Y) p) => (p.X * c + p.Y * s, -p.X * s + p.Y * c);
        (double X, double Y) Back(double x, double y) => (x * c - y * s, x * s + y * c);
        var edges = new List<((double X, double Y) A, (double X, double Y) B)>();
        foreach (var l in loops) for (var i = 0; i < l.Count; i++) edges.Add((Rot(l[i]), Rot(l[(i + 1) % l.Count])));
        var y0 = edges.Min(e => Math.Min(e.A.Y, e.B.Y)); var y1 = edges.Max(e => Math.Max(e.A.Y, e.B.Y));
        for (var y = Math.Ceiling(y0 / spacing) * spacing + spacing / 2; y < y1; y += spacing)
        {
            var xs = new List<double>();
            foreach (var (a, b) in edges)
                if ((a.Y <= y && b.Y > y) || (b.Y <= y && a.Y > y)) xs.Add(a.X + (y - a.Y) / (b.Y - a.Y) * (b.X - a.X));
            xs.Sort();
            for (var i = 0; i + 1 < xs.Count; i += 2)
            {
                var p = Back(xs[i], y); var q = Back(xs[i + 1], y);
                res.Add((p.X, p.Y, q.X, q.Y));
            }
        }
        return res;
    }

    static double Len((double X1, double Y1, double X2, double Y2) s) => Math.Sqrt((s.X2 - s.X1) * (s.X2 - s.X1) + (s.Y2 - s.Y1) * (s.Y2 - s.Y1));

    /// <summary>Eine Schicht: Umrisse und Füllung (unten und oben massiv, sonst Füllgrad), Winkel 45°/135° im Wechsel.</summary>
    public static (List<List<(double X, double Y)>> Loops, List<(double X1, double Y1, double X2, double Y2)> Fill) Layer(Mesh m, int index, int layers, Settings st)
    {
        var z = Math.Max(0.001, (index - 0.5) * st.Layer) + 1e-5; // nie genau auf einer Ecke schneiden
        var loops = Loops(PrintPrep.Slice(m, z));
        var solid = index <= st.Solid || index > layers - st.Solid;
        var pct = solid ? 100 : Math.Clamp(st.Infill, 0, 100);
        var fill = pct <= 0 ? [] : Infill([.. loops], st.LineWidth * 100.0 / pct, index % 2 == 0 ? 45 : 135);
        return (loops, fill);
    }

    /// <summary>Zeit, Filament, Gewicht und Kosten aus den eigenen Bahnen. Bei vielen Schichten wird jede k-te gerechnet und hochgerechnet.</summary>
    public static Result Estimate(Mesh placed, Settings st)
    {
        var (lo, hi) = PrintPrep.Bounds(placed);
        var layers = Math.Max(1, (int)Math.Ceiling((hi.Z - lo.Z) / st.Layer));
        var step = Math.Max(1, layers / 300);
        double wall = 0, fill = 0; int moves = 0;
        for (var i = 1; i <= layers; i += step)
        {
            var (loops, f) = Layer(placed, i, layers, st);
            var w = loops.Sum(Perimeter) * st.Walls; var fl = f.Sum(Len);
            wall += w * step; fill += fl * step; moves += (loops.Count + f.Count) * step;
        }
        var volume = (wall + fill) * st.LineWidth * st.Layer; // mm³
        var support = SupportVolume(placed) * 0.15; // Stützen mit 15 % Dichte
        var mat = st.Mat; var grams = (volume + support) / 1000 * mat.Density;
        var meters = (volume + support) / (Math.PI * 0.875 * 0.875) / 1000;
        var seconds = wall / st.WallSpeed + fill / st.InfillSpeed + support / (st.LineWidth * st.Layer) / st.InfillSpeed + moves * 5.0 / st.Travel + layers * 1.5;
        return new(layers, Math.Round(wall), Math.Round(fill), Math.Round(meters, 2), Math.Round(grams, 1), Math.Round(grams / 1000 * mat.EuroPerKg, 2), TimeSpan.FromSeconds(Math.Round(seconds)), Math.Round(support / 1000 * mat.Density, 1));
    }

    /// <summary>Stützvolumen (mm³): Fläche jedes steiler als 45° überhängenden Dreiecks, senkrecht auf die Platte projiziert, mal Höhe.</summary>
    public static double SupportVolume(Mesh m)
    {
        if (m.V.Count == 0) return 0;
        var minZ = m.V.Min(v => v.Z); double vol = 0;
        for (var t = 0; t + 2 < m.I.Count; t += 3)
        {
            Vec3 a = m.V[m.I[t]], b = m.V[m.I[t + 1]], c = m.V[m.I[t + 2]];
            var n = Vec3.Cross(b - a, c - a); var len = n.Len; if (len < 1e-12 || n.Z / len >= -0.7071) continue;
            var h = (a.Z + b.Z + c.Z) / 3 - minZ; if (h < 0.3) continue;
            vol += Math.Abs(n.Z) / 2 * h;
        }
        return vol;
    }

    /// <summary>Gleichmäßig skalieren (Prozent) – um den Mittelpunkt der Grundfläche.</summary>
    public static Mesh Scale(Mesh m, double percent)
    {
        if (percent is < 1 or > 2000) throw new ArgumentException("Bitte zwischen 1 und 2000 %.");
        var k = percent / 100; var r = Copy(m); for (var i = 0; i < r.V.Count; i++) r.V[i] = r.V[i] * k; return r;
    }

    static Mesh Copy(Mesh m) { var r = new Mesh(); r.V.AddRange(m.V); r.I.AddRange(m.I); r.R.AddRange(m.R); r.G.AddRange(m.G); r.B.AddRange(m.B); return r; }

    /// <summary>Rechtecke (Breite, Tiefe) reihenweise auf die Platte legen (Abstand <paramref name="gap"/> mm), alles zusammen mittig.
    /// Gibt die linke untere Ecke je Teil zurück; wirft, wenn nicht alles passt.</summary>
    public static List<(double X, double Y)> Arrange(IReadOnlyList<(double W, double D)> parts, PrintPrep.Plate p, double gap = 6)
    {
        var order = parts.Select((s, i) => (s, i)).OrderByDescending(x => x.s.D).ToList();
        var pos = new (double X, double Y)[parts.Count]; double x = 0, y = 0, row = 0, maxX = 0;
        foreach (var (s, i) in order)
        {
            if (s.W > p.W || s.D > p.D) throw new InvalidOperationException("Ein Teil ist größer als die Druckplatte.");
            if (x > 0 && x + s.W > p.W) { x = 0; y += row + gap; row = 0; }
            if (y + s.D > p.D) throw new InvalidOperationException($"Nicht alles passt auf die Platte – höchstens {i} Teile, oder kleiner skalieren.");
            pos[i] = (x, y); x += s.W + gap; row = Math.Max(row, s.D); maxX = Math.Max(maxX, x - gap);
        }
        var totalD = y + row; double ox = (p.W - maxX) / 2, oy = (p.D - totalD) / 2;
        return [.. pos.Select(q => (q.X + ox, q.Y + oy))];
    }

    /// <summary>Anzahl Kopien des Modells angeordnet auf der Platte (PrusaSlicer „Instanzen“ + „Anordnen“).</summary>
    public static Mesh Copies(Mesh m, int count, PrintPrep.Plate p, double gap = 6)
    {
        if (count is < 1 or > 100) throw new ArgumentException("Bitte 1 bis 100 Kopien.");
        var (lo, hi) = PrintPrep.Bounds(m); var w = hi.X - lo.X; var d = hi.Y - lo.Y;
        var places = Arrange([.. Enumerable.Repeat((w, d), count)], p, gap);
        var r = new Mesh();
        foreach (var (x, y) in places)
        {
            var c = new Mesh(); var off = new Vec3(x - lo.X, y - lo.Y, -lo.Z);
            c.V.AddRange(m.V.Select(v => v + off)); c.I.AddRange(m.I); c.R.AddRange(m.R); c.G.AddRange(m.G); c.B.AddRange(m.B);
            r.Append(c);
        }
        return r;
    }
}
