using System.Text;
using System.Text.Json;

namespace Trendo.Core;

/// <summary>Einscannen wie in Meshroom und RealityCapture (Profi 4 + Claude 4, 06.11.2026): Kamerapositionen, Beschneiden, kleine Reste entfernen,
/// Löcher schließen, echte Größe (auch über einen bekannten Gegenstand), Texturqualität, gute Fotos auswählen. Nur Funktionsideen, eigener Code.</summary>
public static class ScanPro
{
    // ───────────── Kameras ─────────────
    public sealed record Camera(string Name, Vec3 Center);

    /// <summary>COLMAP images.bin: je Bild Drehung (qw,qx,qy,qz) und Verschiebung t; Kameramitte C = −Rᵀ·t.</summary>
    public static List<Camera> ReadColmapBin(string path)
    {
        var res = new List<Camera>();
        using var r = new BinaryReader(File.OpenRead(path));
        var n = r.ReadUInt64();
        for (ulong i = 0; i < n && i < 100_000; i++)
        {
            r.ReadUInt32();
            double qw = r.ReadDouble(), qx = r.ReadDouble(), qy = r.ReadDouble(), qz = r.ReadDouble(), tx = r.ReadDouble(), ty = r.ReadDouble(), tz = r.ReadDouble();
            r.ReadUInt32();
            var name = new StringBuilder(); for (int c; (c = r.ReadByte()) != 0;) name.Append((char)c);
            var pts = r.ReadUInt64(); r.BaseStream.Seek((long)pts * 24, SeekOrigin.Current);
            res.Add(new(name.ToString(), Center(qw, qx, qy, qz, tx, ty, tz)));
        }
        return res;
    }

    /// <summary>COLMAP images.txt (Textform): Zeilen „ID QW QX QY QZ TX TY TZ KAMERA NAME“, je zweite Zeile Punkte.</summary>
    public static List<Camera> ReadColmapTxt(string path)
    {
        var res = new List<Camera>(); var lines = File.ReadAllLines(path).Where(l => !l.StartsWith('#')).ToList(); // leere Punktzeilen gehören dazu
        for (var i = 0; i < lines.Count; i++)
        {
            var p = lines[i].Split(' ', StringSplitOptions.RemoveEmptyEntries); if (p.Length < 10) continue;
            i++; // die Zeile danach enthält die Bildpunkte
            double D(int k) => double.Parse(p[k], System.Globalization.CultureInfo.InvariantCulture);
            res.Add(new(p[9], Center(D(1), D(2), D(3), D(4), D(5), D(6), D(7))));
        }
        return res;
    }

    static Vec3 Center(double w, double x, double y, double z, double tx, double ty, double tz)
    {
        // Drehmatrix aus dem Quaternion; C = −Rᵀ t
        double r00 = 1 - 2 * (y * y + z * z), r01 = 2 * (x * y - z * w), r02 = 2 * (x * z + y * w);
        double r10 = 2 * (x * y + z * w), r11 = 1 - 2 * (x * x + z * z), r12 = 2 * (y * z - x * w);
        double r20 = 2 * (x * z - y * w), r21 = 2 * (y * z + x * w), r22 = 1 - 2 * (x * x + y * y);
        return new(-(r00 * tx + r10 * ty + r20 * tz), -(r01 * tx + r11 * ty + r21 * tz), -(r02 * tx + r12 * ty + r22 * tz));
    }

    /// <summary>Meshroom cameras.sfm (JSON): „poses“ mit „transform.center“, „views“ verbinden Bild und Lage.</summary>
    public static List<Camera> ReadMeshroomSfm(string path)
    {
        using var d = JsonDocument.Parse(File.ReadAllText(path));
        var names = new Dictionary<string, string>();
        if (d.RootElement.TryGetProperty("views", out var views))
            foreach (var v in views.EnumerateArray()) if (v.TryGetProperty("poseId", out var pid)) names[pid.GetString() ?? ""] = Path.GetFileName(v.TryGetProperty("path", out var pa) ? pa.GetString() ?? "" : "");
        var res = new List<Camera>();
        if (d.RootElement.TryGetProperty("poses", out var poses))
            foreach (var p in poses.EnumerateArray())
            {
                var c = p.GetProperty("pose").GetProperty("transform").GetProperty("center").EnumerateArray().Select(x => double.Parse(x.GetString() ?? x.GetRawText(), System.Globalization.CultureInfo.InvariantCulture)).ToArray();
                if (c.Length == 3) res.Add(new(names.GetValueOrDefault(p.GetProperty("poseId").GetString() ?? "", ""), new(c[0], c[1], c[2])));
            }
        return res;
    }

    /// <summary>Kameras im Arbeitsordner finden (COLMAP oder Meshroom, Rohkoordinaten).</summary>
    public static List<Camera> FindCameras(string workDir)
    {
        if (!Directory.Exists(workDir)) return [];
        if (PhotoScan.SparseModel(workDir) is { } sp)
        {
            if (File.Exists(Path.Combine(sp, "images.bin"))) return ReadColmapBin(Path.Combine(sp, "images.bin"));
            if (File.Exists(Path.Combine(sp, "images.txt"))) return ReadColmapTxt(Path.Combine(sp, "images.txt"));
        }
        var sfm = Directory.EnumerateFiles(workDir, "cameras.sfm", SearchOption.AllDirectories).OrderByDescending(f => new FileInfo(f).Length).FirstOrDefault();
        return sfm is null ? [] : ReadMeshroomSfm(sfm);
    }

    /// <summary>Kameras in die Lage des Modells im 3D-Studio bringen (gleiche Mitte und Skalierung wie das Modell) und als kameras.json speichern.</summary>
    public static int SaveCameras(string workDir, Vec3 center, double scale)
    {
        var cams = FindCameras(workDir).Select(c => new { name = c.Name, x = (c.Center.X - center.X) * scale, y = (c.Center.Y - center.Y) * scale, z = (c.Center.Z - center.Z) * scale }).ToList();
        File.WriteAllText(Path.Combine(workDir, "kameras.json"), JsonSerializer.Serialize(cams));
        return cams.Count;
    }
    public static List<Camera> LoadCameras(string workDir)
    {
        var f = Path.Combine(workDir, "kameras.json"); if (!File.Exists(f)) return [];
        try { using var d = JsonDocument.Parse(File.ReadAllText(f)); return [.. d.RootElement.EnumerateArray().Select(e => new Camera(e.GetProperty("name").GetString() ?? "", new(e.GetProperty("x").GetDouble(), e.GetProperty("y").GetDouble(), e.GetProperty("z").GetDouble())))]; }
        catch (JsonException) { return []; }
    }

    // ───────────── Netz bearbeiten ─────────────
    static Mesh Copy(Mesh m, Func<int, bool> keep)
    {
        var o = new Mesh();
        for (var t = 0; t < m.Triangles; t++)
        {
            if (!keep(t)) continue;
            o.CR = t < m.R.Count ? m.R[t] : m.CR; o.CG = t < m.G.Count ? m.G[t] : m.CG; o.CB = t < m.B.Count ? m.B[t] : m.CB;
            o.Add(m.V[m.I[3 * t]], m.V[m.I[3 * t + 1]], m.V[m.I[3 * t + 2]]);
        }
        return o;
    }
    public static (Vec3 Min, Vec3 Max) Bounds(Mesh m) => (new(m.V.Min(v => v.X), m.V.Min(v => v.Y), m.V.Min(v => v.Z)), new(m.V.Max(v => v.X), m.V.Max(v => v.Y), m.V.Max(v => v.Z)));

    /// <summary>Beschneiden: nur Dreiecke behalten, deren Mitte im Kasten liegt (Anteile 0–1 der Ausdehnung je Achse) – z. B. den Tisch unter dem Gegenstand weg.</summary>
    public static Mesh Crop(Mesh m, (double X0, double X1) x, (double Y0, double Y1) y, (double Z0, double Z1) z)
    {
        var (lo, hi) = Bounds(m); var size = hi - lo;
        bool In(Vec3 p) => p.X >= lo.X + size.X * x.X0 - 1e-9 && p.X <= lo.X + size.X * x.X1 + 1e-9 && p.Y >= lo.Y + size.Y * y.Y0 - 1e-9 && p.Y <= lo.Y + size.Y * y.Y1 + 1e-9 && p.Z >= lo.Z + size.Z * z.Z0 - 1e-9 && p.Z <= lo.Z + size.Z * z.Z1 + 1e-9;
        var o = Copy(m, t => In((m.V[m.I[3 * t]] + m.V[m.I[3 * t + 1]] + m.V[m.I[3 * t + 2]]) * (1.0 / 3)));
        if (o.Triangles == 0) throw new InvalidOperationException("So bliebe nichts vom Modell übrig – weniger abschneiden.");
        return o;
    }

    static (Dictionary<(long, long, long), int> Ids, int[] Vid) Weld(Mesh m)
    {
        var ids = new Dictionary<(long, long, long), int>(); var vid = new int[m.V.Count];
        for (var i = 0; i < m.V.Count; i++) { var v = m.V[i]; var k = ((long)Math.Round(v.X * 1e4), (long)Math.Round(v.Y * 1e4), (long)Math.Round(v.Z * 1e4)); if (!ids.TryGetValue(k, out var id)) ids[k] = id = ids.Count; vid[i] = id; }
        return (ids, vid);
    }

    /// <summary>Kleine Reste entfernen: nur das größte zusammenhängende Teil behalten (schwebende Krümel aus dem Hintergrund fallen weg).</summary>
    public static Mesh KeepLargest(Mesh m)
    {
        var (ids, vid) = Weld(m); var parent = Enumerable.Range(0, ids.Count).ToArray();
        int Find(int a) { while (parent[a] != a) a = parent[a] = parent[parent[a]]; return a; }
        for (var t = 0; t < m.Triangles; t++) { var a = Find(vid[m.I[3 * t]]); parent[Find(vid[m.I[3 * t + 1]])] = a; parent[Find(vid[m.I[3 * t + 2]])] = a; }
        var count = new Dictionary<int, int>(); for (var t = 0; t < m.Triangles; t++) { var r = Find(vid[m.I[3 * t]]); count[r] = count.GetValueOrDefault(r) + 1; }
        var best = count.MaxBy(kv => kv.Value).Key;
        return Copy(m, t => Find(vid[m.I[3 * t]]) == best);
    }

    /// <summary>Löcher schließen: offene Ränder (Kanten mit nur einem Dreieck) zu Schleifen verbinden und mit einem Fächer zur Lochmitte füllen (bis <paramref name="maxEdges"/> Kanten je Loch).</summary>
    public static (Mesh Mesh, int Holes) FillHoles(Mesh m, int maxEdges = 400)
    {
        var (ids, vid) = Weld(m); var pos = new Vec3[ids.Count]; for (var i = 0; i < m.V.Count; i++) pos[vid[i]] = m.V[i];
        var edges = new Dictionary<(int, int), int>();
        for (var t = 0; t < m.Triangles; t++)
            for (var k = 0; k < 3; k++)
            {
                int a = vid[m.I[3 * t + k]], b = vid[m.I[3 * t + (k + 1) % 3]];
                var key = (Math.Min(a, b), Math.Max(a, b)); edges[key] = edges.GetValueOrDefault(key) + 1;
            }
        // gerichtete Randkanten (Richtung wie im Dreieck), damit das neue Dreieck richtig herum zeigt
        var next = new Dictionary<int, int>();
        for (var t = 0; t < m.Triangles; t++)
            for (var k = 0; k < 3; k++)
            {
                int a = vid[m.I[3 * t + k]], b = vid[m.I[3 * t + (k + 1) % 3]];
                if (edges[(Math.Min(a, b), Math.Max(a, b))] == 1) next[b] = a; // umgekehrt laufen: Füllung schließt die Fläche
            }
        var o = Copy(m, _ => true); var holes = 0; var used = new HashSet<int>();
        foreach (var start in next.Keys.ToList())
        {
            if (used.Contains(start)) continue;
            var loop = new List<int> { start }; used.Add(start); var cur = start;
            while (next.TryGetValue(cur, out var nx) && nx != start && loop.Count <= maxEdges && used.Add(nx)) { loop.Add(nx); cur = nx; }
            if (!next.TryGetValue(cur, out var close) || close != start || loop.Count < 3 || loop.Count > maxEdges) continue;
            var mid = loop.Aggregate(new Vec3(0, 0, 0), (s, i) => s + pos[i]) * (1.0 / loop.Count);
            o.CR = m.CR; o.CG = m.CG; o.CB = m.CB;
            for (var i = 0; i < loop.Count; i++) o.Add(pos[loop[i]], pos[loop[(i + 1) % loop.Count]], mid);
            holes++;
        }
        return (o, holes);
    }

    /// <summary>Bekannte Gegenstände für die echte Größe: einen davon mitfotografieren und seine Länge im Modell angeben.</summary>
    public static readonly (string Name, double Cm)[] References =
    [
        ("Kreditkarte (lange Seite)", 8.56), ("DIN-A4-Blatt (lange Seite)", 29.7), ("1-Euro-Münze (Durchmesser)", 2.325), ("Getränkedose 0,33 l (Höhe)", 11.5), ("Smartphone (typisch, Höhe)", 15.0),
    ];

    /// <summary>Echte Größe setzen: eine Achse (x, y, z) oder die längste Seite (m) auf <paramref name="cm"/> bringen; alles gleichmäßig skaliert.</summary>
    public static Mesh ScaleTo(Mesh m, char axis, double cm)
    {
        if (cm is <= 0 or > 100_000) throw new ArgumentException("Bitte eine Größe zwischen 0 und 1 000 m.");
        var (lo, hi) = Bounds(m); var size = hi - lo;
        var cur = axis switch { 'x' => size.X, 'y' => size.Y, 'z' => size.Z, _ => Math.Max(size.X, Math.Max(size.Y, size.Z)) };
        if (cur < 1e-9) throw new InvalidOperationException("Das Modell hat in dieser Richtung keine Ausdehnung.");
        var s = cm / cur; var c = (lo + hi) * 0.5;
        var o = new Mesh();
        for (var t = 0; t < m.Triangles; t++)
        {
            o.CR = t < m.R.Count ? m.R[t] : m.CR; o.CG = t < m.G.Count ? m.G[t] : m.CG; o.CB = t < m.B.Count ? m.B[t] : m.CB;
            Vec3 P(int k) { var v = m.V[m.I[3 * t + k]]; return new Vec3((v.X - c.X) * s, (v.Y - c.Y) * s, (v.Z - lo.Z) * s); } // Boden bleibt bei z = 0
            o.Add(P(0), P(1), P(2));
        }
        return o;
    }

    /// <summary>Echte Größe über einen mitgescannten Gegenstand: gemessene Länge im Modell (cm) und echte Länge (cm) → Faktor auf das ganze Modell.</summary>
    public static Mesh ScaleByReference(Mesh m, double measuredInModelCm, double realCm)
    {
        if (measuredInModelCm <= 0 || realCm <= 0) throw new ArgumentException("Beide Längen müssen größer als 0 sein.");
        var (lo, hi) = Bounds(m); var longest = Math.Max(hi.X - lo.X, Math.Max(hi.Y - lo.Y, hi.Z - lo.Z));
        return ScaleTo(m, 'm', longest * realCm / measuredInModelCm);
    }

    // ───────────── Texturqualität und Fotoauswahl ─────────────
    public static readonly int[] TextureSides = [1024, 2048, 4096, 8192];

    /// <summary>Gute Fotos auswählen: wahrscheinlich unscharfe (deutlich weniger Bilddetails pro Pixel als die übrigen – JPEG wird bei Unschärfe klein)
    /// und fast gleiche Fotos direkt hintereinander (Doppelte) aussortieren. Liefert je Foto: behalten? und den Grund.</summary>
    public static List<(string File, bool Keep, string Why)> RankPhotos(IReadOnlyList<string> files, Func<string, (int W, int H)?> size)
    {
        var info = files.Select(f => (File: f, Bytes: new FileInfo(f).Length, Px: size(f) is { } s ? (double)s.W * s.H : 0)).ToList();
        var dens = info.Where(i => i.Px > 0).Select(i => i.Bytes / i.Px).OrderBy(x => x).ToList();
        var median = dens.Count == 0 ? 0 : dens[dens.Count / 2];
        var res = new List<(string, bool, string)>();
        for (var i = 0; i < info.Count; i++)
        {
            var (f, b, px) = info[i];
            if (px > 0 && median > 0 && b / px < median * 0.55) { res.Add((f, false, "wahrscheinlich unscharf oder verwackelt (wenig Bilddetails)")); continue; }
            if (i > 0 && Math.Abs(info[i - 1].Bytes - b) <= Math.Max(64, b / 400) && info[i - 1].Px == px) { res.Add((f, false, "fast gleich wie das Foto davor (doppelt)")); continue; }
            res.Add((f, true, "gut"));
        }
        return res;
    }
}
