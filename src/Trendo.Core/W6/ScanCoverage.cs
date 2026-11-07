namespace Trendo.Core;

/// <summary>S4 (Modernisierung 07.11.2026): Aufnahme-Assistent mit Abdeckungsanzeige beim Einscannen – wie in RealityCapture/Polycam:
/// aus welchen Richtungen und Höhen wurde fotografiert, wo fehlen Fotos. Rundum 12 Richtungen (je 30°, Richtung 0° = erstes Foto, Angaben als Richtungsmitten) in drei Höhen
/// (flach unter 15°, schräg 15–45°, von oben über 45°). Fotogrammetrie kennt kein festes Oben: die Hochachse ist die Achse, entlang der die
/// Kamerapositionen am wenigsten streuen (man geht ums Objekt herum), Vorzeichen so, dass die Kameras im Mittel darüber stehen.</summary>
public static class ScanCoverage
{
    public static readonly string[] Rings = ["flach", "schräg", "von oben"];
    public const int Sectors = 12;

    public sealed record Result(int[,] Counts, int Percent, IReadOnlyList<string> Gaps, int Cameras)
    {
        public int Count(int ring, int sector) => Counts[ring, sector];
    }

    /// <summary>Abdeckung der Kameras um die Objektmitte; null bei weniger als 3 Kameras.</summary>
    public static Result? Compute(IReadOnlyList<Vec3> cams, Vec3 center)
    {
        if (cams.Count < 3) return null;
        var up = UpAxis(cams);
        if (cams.Average(c => Vec3.Dot(c - center, up)) < 0) up = up * -1;
        Vec3 Flat(Vec3 v) => v - up * Vec3.Dot(v, up);
        var e1 = cams.Select(c => Flat(c - center)).FirstOrDefault(v => v.Len > 1e-9).Norm();
        var e2 = Vec3.Cross(up, e1).Norm();
        var counts = new int[3, Sectors];
        foreach (var c in cams)
        {
            var v = c - center; var h = Vec3.Dot(v, up); var r = Flat(v).Len;
            var elev = Math.Atan2(h, r) * 180 / Math.PI;
            var az = (Math.Atan2(Vec3.Dot(v, e2), Vec3.Dot(v, e1)) * 180 / Math.PI + 360 + 15) % 360; // erstes Foto in der Mitte von Richtung 0 (je ±15°)
            counts[elev < 15 ? 0 : elev < 45 ? 1 : 2, (int)(az / 30) % Sectors]++;
        }
        int Covered(int ring) => Enumerable.Range(0, Sectors).Count(s => counts[ring, s] > 0);
        var topQuarters = Enumerable.Range(0, 4).Count(q => Enumerable.Range(q * 3, 3).Any(s => counts[2, s] > 0));
        var percent = (int)Math.Round(100 * (0.4 * Covered(0) / Sectors + 0.4 * Covered(1) / Sectors + 0.2 * topQuarters / 4.0));
        var gaps = new List<string>();
        for (var ring = 0; ring < 2; ring++)
            foreach (var (from, len) in EmptyRuns(counts, ring))
                gaps.Add(len == Sectors ? $"{Rings[ring]}: noch keine Fotos – eine ganze Runde, etwa 12 bis 24 Fotos"
                    : len == 1 ? $"{Rings[ring]}: Richtung {from * 30}° fehlt – dort noch etwa 2 Fotos" : $"{Rings[ring]}: {from * 30}°–{(from + len - 1) * 30 % 360}° fehlt – dort noch etwa {len * 2} Fotos");
        if (topQuarters < 4) gaps.Add($"von oben: {4 - topQuarters} von 4 Seiten fehlen – noch {Math.Max(2, (4 - topQuarters) * 2)} Fotos schräg von oben");
        return new(counts, percent, gaps, cams.Count);
    }

    /// <summary>Zusammenhängende leere Richtungen einer Höhe (rundum, über 0° hinweg).</summary>
    static List<(int From, int Len)> EmptyRuns(int[,] c, int ring)
    {
        var empty = Enumerable.Range(0, Sectors).Select(s => c[ring, s] == 0).ToArray();
        if (empty.All(e => e)) return [(0, Sectors)];
        var start = Array.FindIndex(empty, e => !e); var runs = new List<(int, int)>();
        for (int k = 1, from = -1; k <= Sectors; k++)
        {
            var s = (start + k) % Sectors;
            if (empty[s] && from < 0) from = s;
            if (!empty[s] && from >= 0) { runs.Add((from, (s - from + Sectors) % Sectors)); from = -1; }
        }
        return runs;
    }

    /// <summary>Achse mit der kleinsten Streuung der Kamerapositionen (kleinster Eigenvektor der Kovarianz, Jacobi-Verfahren).</summary>
    public static Vec3 UpAxis(IReadOnlyList<Vec3> cams)
    {
        double mx = cams.Average(c => c.X), my = cams.Average(c => c.Y), mz = cams.Average(c => c.Z);
        var a = new double[3, 3];
        foreach (var c in cams)
        {
            var d = new[] { c.X - mx, c.Y - my, c.Z - mz };
            for (var i = 0; i < 3; i++) for (var j = 0; j < 3; j++) a[i, j] += d[i] * d[j];
        }
        var v = new double[3, 3] { { 1, 0, 0 }, { 0, 1, 0 }, { 0, 0, 1 } };
        for (var sweep = 0; sweep < 50; sweep++)
        {
            var off = Math.Abs(a[0, 1]) + Math.Abs(a[0, 2]) + Math.Abs(a[1, 2]);
            if (off < 1e-12 * (Math.Abs(a[0, 0]) + Math.Abs(a[1, 1]) + Math.Abs(a[2, 2]) + 1e-30)) break;
            foreach (var (p, q) in new[] { (0, 1), (0, 2), (1, 2) })
            {
                if (Math.Abs(a[p, q]) < 1e-30) continue;
                var th = 0.5 * Math.Atan2(2 * a[p, q], a[q, q] - a[p, p]); double cs = Math.Cos(th), sn = Math.Sin(th);
                for (var k = 0; k < 3; k++) { double akp = a[k, p], akq = a[k, q]; a[k, p] = cs * akp - sn * akq; a[k, q] = sn * akp + cs * akq; }
                for (var k = 0; k < 3; k++) { double apk = a[p, k], aqk = a[q, k]; a[p, k] = cs * apk - sn * aqk; a[q, k] = sn * apk + cs * aqk; }
                for (var k = 0; k < 3; k++) { double vkp = v[k, p], vkq = v[k, q]; v[k, p] = cs * vkp - sn * vkq; v[k, q] = sn * vkp + cs * vkq; }
            }
        }
        var min = Enumerable.Range(0, 3).OrderBy(i => a[i, i]).First();
        return new Vec3(v[0, min], v[1, min], v[2, min]).Norm();
    }

    /// <summary>Aufnahme-Plan vor der Rechnung: drei Runden ums Objekt plus Details; Fortschritt nur nach Fotoanzahl (Richtungen kennt erst die Rechnung).</summary>
    public static readonly (string Step, int Photos)[] Plan =
    [
        ("Runde 1 auf Höhe des Objekts: alle 30° ein Foto, Objekt füllt das Bild", 12),
        ("Runde 2 schräg von oben (etwa 30–45°): wieder alle 30°", 12),
        ("Runde 3 von oben: vier Seiten", 4),
        ("Details nah: Kanten, Schrift, Unterseite falls möglich", 6),
    ];
    public static int PlanTotal => Plan.Sum(p => p.Photos);
}
