using System.Globalization;
using System.IO.Compression;
using System.Text;

namespace Trendo.Core.Elektro;

/// <summary>Anschlussfläche eines Gehäuses relativ zur Bauteilmitte (mm). Pin: 1 = Anschluss 1 im Schaltplan, 2 = Anschluss 2, 3 = dritter Anschluss, 0 = nicht verbunden.</summary>
public sealed record PadDef(double X, double Y, int Pin, double Size = 1.8, double Drill = 0.8, bool Square = false);
public sealed record Footprint(string Name, IReadOnlyList<PadDef> Pads, double W, double H, bool Round = false);

/// <summary>T-0277: Durchsteck-Gehäuse (THT) in Normmaßen (Raster 2,54 mm).</summary>
public static class Footprints
{
    public static Footprint? For(string kind) => kind switch
    {
        "widerstand" => new("Widerstand 0207, Raster 10,16", [new(-5.08, 0, 1, Square: true), new(5.08, 0, 2)], 6.3, 2.5),
        "spule" => new("Drossel, Raster 10,16", [new(-5.08, 0, 1, Square: true), new(5.08, 0, 2)], 9, 4),
        "diode" => new("Diode DO-41, Raster 10,16", [new(-5.08, 0, 1, Square: true), new(5.08, 0, 2)], 5.2, 2.7),
        "kondensator" => new("Kondensator radial Ø 6,3, Raster 2,54", [new(-1.27, 0, 1, Square: true), new(1.27, 0, 2)], 6.3, 6.3, true),
        "led" => new("LED 5 mm, Raster 2,54", [new(-1.27, 0, 1, Square: true), new(1.27, 0, 2)], 5.8, 5.8, true),
        "lampe" => new("Lampenfassung, Raster 5,08", [new(-2.54, 0, 1, Square: true), new(2.54, 0, 2)], 8, 8, true),
        "schalter" => new("Taster 6 × 6", [new(-3.25, 0, 1, Square: true), new(3.25, 0, 2)], 6, 6),
        "spannung" or "wechsel" => new("Schraubklemme 2-polig, Raster 5,08", [new(-2.54, 0, 1, 2.4, 1.2, true), new(2.54, 0, 2, 2.4, 1.2)], 10, 7.5),
        "npn" or "pnp" => new("TO-92, Beinchen auf 2,54 gebogen (E B C)", [new(-2.54, 0, 2, 1.6), new(0, 0, 3, 1.6), new(2.54, 0, 1, 1.6)], 5.2, 4, true), // E = 2, B = 3, C = 1
        "opv" => new("DIP-8", [new(-3.81, -3.81, 0, Square: true), new(-1.27, -3.81, 1), new(1.27, -3.81, 3), new(3.81, -3.81, 0), new(3.81, 3.81, 0), new(1.27, 3.81, 2), new(-1.27, 3.81, 0), new(-3.81, 3.81, 0)], 10, 6.4), // 2 = −, 3 = +, 6 = Ausgang; 4/7 Versorgung extern
        _ => null,
    };
}

public sealed class PcbPlace { public double X { get; set; } public double Y { get; set; } public int Rot { get; set; } }
public sealed class PcbTrace { public int Net { get; set; } public bool Bottom { get; set; } public List<double[]> Pts { get; set; } = []; }

/// <summary>T-0277: Platinen-Layout zu einer Schaltung – Platinengröße, Lage der Bauteile, Leiterbahnen (zwei Lagen).</summary>
public sealed class PcbLayout
{
    public double W { get; set; } = 60;
    public double H { get; set; } = 40;
    public double TraceWidth { get; set; } = 0.5;
    public Dictionary<string, PcbPlace> Places { get; set; } = [];
    public List<PcbTrace> Traces { get; set; } = [];
}

public sealed record PcbPad(string Part, string Name, double X, double Y, int Net, PadDef Def);
public sealed record Airwire(int Net, double X1, double Y1, double X2, double Y2);

public static class Pcb
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    public static IEnumerable<Part> Placeable(Circuit c) => c.Parts.Where(p => Footprints.For(p.Kind) is not null);

    /// <summary>Netz je Anschluss aus dem Schaltplan (0 = Masse, −1 = nicht verbunden).</summary>
    public static List<PcbPad> Pads(Circuit c)
    {
        var net = Netlist.Build(c); var res = new List<PcbPad>();
        foreach (var p in Placeable(c))
        {
            var fp = Footprints.For(p.Kind)!; if (!c.Pcb.Places.TryGetValue(p.Id, out var pl)) continue;
            foreach (var (pad, i) in fp.Pads.Select((x, i) => (x, i)))
            {
                var n = pad.Pin switch { 1 => net.NodeOf(p.X1, p.Y1), 2 => net.NodeOf(p.X2, p.Y2), 3 => p.Pin3() is { } q ? net.NodeOf(q.X, q.Y) : -1, _ => -1 };
                var (dx, dy) = Rot(pad.X, pad.Y, pl.Rot);
                res.Add(new(p.Id, $"{p.Name}.{i + 1}", pl.X + dx, pl.Y + dy, n, pad));
            }
        }
        return res;
    }

    static (double, double) Rot(double x, double y, int rot) => (((rot % 360) + 360) % 360) switch { 90 => (-y, x), 180 => (-x, -y), 270 => (y, -x), _ => (x, y) };

    /// <summary>Bauteile reihenweise auf die Platine setzen (Raster 1,27 mm, 7 mm Abstand für Leiterbahnen) und die Platine passend vergrößern.</summary>
    public static void AutoPlace(Circuit c)
    {
        double x = 5, y = 7, rowH = 0; const double gap = 7, maxW = 80; // Abstand lässt Platz für Leiterbahnen
        c.Pcb.Places.Clear(); c.Pcb.Traces.Clear();
        foreach (var p in Placeable(c).OrderByDescending(p => Footprints.For(p.Kind)!.W))
        {
            var fp = Footprints.For(p.Kind)!; var w = Math.Max(fp.W, fp.Pads.Max(q => Math.Abs(q.X)) * 2 + 2); var h = Math.Max(fp.H, fp.Pads.Max(q => Math.Abs(q.Y)) * 2 + 2);
            if (x + w > maxW) { x = 5; y += rowH + gap; rowH = 0; }
            c.Pcb.Places[p.Id] = new PcbPlace { X = Snap(x + w / 2), Y = Snap(y + h / 2) };
            x += w + gap; rowH = Math.Max(rowH, h);
        }
        c.Pcb.W = Math.Ceiling(Math.Max(30, c.Pcb.Places.Values.Select(v => v.X).DefaultIfEmpty(0).Max() + 10));
        c.Pcb.H = Math.Ceiling(Math.Max(20, y + rowH + 7));
    }

    public static double Snap(double v, double grid = 1.27) => Math.Round(v / grid) * grid;

    /// <summary>Luftlinien: je Netz ein minimaler Spannbaum über die Anschlüsse; schon verlegte Bahnen verbinden ihre Enden.</summary>
    public static List<Airwire> Ratsnest(Circuit c, bool skipRouted = true)
    {
        var res = new List<Airwire>();
        foreach (var g in Pads(c).Where(p => p.Net >= 0).GroupBy(p => p.Net))
        {
            var pts = g.ToList(); if (pts.Count < 2) continue;
            var inTree = new bool[pts.Count]; inTree[0] = true;
            for (var k = 1; k < pts.Count; k++)
            {
                (int A, int B, double D) best = (-1, -1, double.MaxValue);
                for (var a = 0; a < pts.Count; a++) if (inTree[a]) for (var b = 0; b < pts.Count; b++) if (!inTree[b]) { var d = Math.Abs(pts[a].X - pts[b].X) + Math.Abs(pts[a].Y - pts[b].Y); if (d < best.D) best = (a, b, d); }
                inTree[best.B] = true;
                var w = new Airwire(g.Key, pts[best.A].X, pts[best.A].Y, pts[best.B].X, pts[best.B].Y);
                if (!skipRouted || !c.Pcb.Traces.Any(t => t.Net == w.Net && Ends(t, w))) res.Add(w);
            }
        }
        return res;
    }

    static bool Ends(PcbTrace t, Airwire w) { var a = t.Pts[0]; var b = t.Pts[^1]; bool Eq(double[] p, double x, double y) => Math.Abs(p[0] - x) < 0.01 && Math.Abs(p[1] - y) < 0.01; return Eq(a, w.X1, w.Y1) && Eq(b, w.X2, w.Y2) || Eq(a, w.X2, w.Y2) && Eq(b, w.X1, w.Y1); }

    /// <summary>Einfacher Autorouter: jede Luftlinie als Winkel (waagerecht, dann senkrecht oder umgekehrt) auf der Oberseite; kreuzt das eine
    /// fremde Bahn oder Fläche, wird die andere Winkelrichtung bzw. die Unterseite versucht. Was nirgends frei ist, bleibt als Luftlinie und wird gemeldet.</summary>
    public static (int Routed, int Open) AutoRoute(Circuit c)
    {
        c.Pcb.Traces.Clear(); var pads = Pads(c); int ok = 0, open = 0;
        foreach (var w in Ratsnest(c, false).OrderBy(w => Math.Abs(w.X1 - w.X2) + Math.Abs(w.Y1 - w.Y2)))
        {
            // Winkel in beide Richtungen, dann Umwege über parallele Spuren (Raster 2,54 mm) – kürzeste freie Variante zuerst
            var options = new List<double[][]> { new[] { new[] { w.X1, w.Y1 }, new[] { w.X2, w.Y1 }, new[] { w.X2, w.Y2 } }, new[] { new[] { w.X1, w.Y1 }, new[] { w.X1, w.Y2 }, new[] { w.X2, w.Y2 } } };
            for (var k = 1; k <= 6; k++)
                foreach (var sg in new[] { -1, 1 })
                {
                    var o = sg * k * 2.54; double ly = w.Y1 + o, lx = w.X1 + o;
                    if (ly > 0.6 && ly < c.Pcb.H - 0.6) options.Add([[w.X1, w.Y1], [w.X1, ly], [w.X2, ly], [w.X2, w.Y2]]);
                    if (lx > 0.6 && lx < c.Pcb.W - 0.6) options.Add([[w.X1, w.Y1], [lx, w.Y1], [lx, w.Y2], [w.X2, w.Y2]]);
                }
            options = options.OrderBy(o => o.Zip(o.Skip(1)).Sum(s => Math.Abs(s.First[0] - s.Second[0]) + Math.Abs(s.First[1] - s.Second[1]))).ToList();
            PcbTrace? placed = null;
            foreach (var bottom in new[] { false, true })
                foreach (var o in options)
                {
                    var t = new PcbTrace { Net = w.Net, Bottom = bottom, Pts = o.Where((p, i) => i == 0 || p[0] != o[i - 1][0] || p[1] != o[i - 1][1]).ToList() };
                    if (Conflicts(c, t, pads).Count == 0) { placed = t; goto done; }
                }
            done:
            if (placed is not null) { c.Pcb.Traces.Add(placed); ok++; } else open++;
        }
        return (ok, open);
    }

    /// <summary>Prüfung (DRC): Bahn zu nah an fremder Bahn derselben Lage oder an einem fremden Anschluss (Mindestabstand 0,3 mm).</summary>
    public static List<string> Conflicts(Circuit c, PcbTrace t, List<PcbPad>? pads = null)
    {
        pads ??= Pads(c); var res = new List<string>(); var half = c.Pcb.TraceWidth / 2; const double clear = 0.3;
        for (var i = 1; i < t.Pts.Count; i++)
        {
            var (a, b) = (t.Pts[i - 1], t.Pts[i]);
            foreach (var p in pads.Where(p => p.Net != t.Net)) if (SegPoint(a, b, p.X, p.Y) < p.Def.Size / 2 + half + clear) res.Add($"Bahn berührt Anschluss {p.Name}");
            foreach (var o in c.Pcb.Traces.Where(o => o != t && o.Net != t.Net && o.Bottom == t.Bottom))
                for (var k = 1; k < o.Pts.Count; k++) if (SegSeg(a, b, o.Pts[k - 1], o.Pts[k]) < 2 * half + clear) res.Add("Bahnen zu nah / gekreuzt");
        }
        return res;
    }

    public static List<string> Check(Circuit c)
    {
        var res = new List<string>(); var pads = Pads(c);
        foreach (var t in c.Pcb.Traces) res.AddRange(Conflicts(c, t, pads));
        foreach (var p in pads) if (p.X < 1 || p.Y < 1 || p.X > c.Pcb.W - 1 || p.Y > c.Pcb.H - 1) res.Add($"{p.Name} liegt außerhalb der Platine");
        var open = Ratsnest(c).Count; if (open > 0) res.Add($"{open} Verbindung(en) noch nicht verlegt");
        return res.Distinct().ToList();
    }

    static double SegPoint(double[] a, double[] b, double x, double y)
    {
        double dx = b[0] - a[0], dy = b[1] - a[1], l = dx * dx + dy * dy; var t = l == 0 ? 0 : Math.Clamp(((x - a[0]) * dx + (y - a[1]) * dy) / l, 0, 1);
        return Math.Sqrt(Math.Pow(a[0] + t * dx - x, 2) + Math.Pow(a[1] + t * dy - y, 2));
    }
    static double SegSeg(double[] a, double[] b, double[] c, double[] d)
    {
        static double Cross(double[] o, double[] p, double[] q) => (p[0] - o[0]) * (q[1] - o[1]) - (p[1] - o[1]) * (q[0] - o[0]);
        double d1 = Cross(c, d, a), d2 = Cross(c, d, b), d3 = Cross(a, b, c), d4 = Cross(a, b, d);
        if ((d1 > 0) != (d2 > 0) && (d3 > 0) != (d4 > 0) && d1 != 0 && d2 != 0 && d3 != 0 && d4 != 0) return 0;
        return new[] { SegPoint(c, d, a[0], a[1]), SegPoint(c, d, b[0], b[1]), SegPoint(a, b, c[0], c[1]), SegPoint(a, b, d[0], d[1]) }.Min();
    }

    // ---------- Fertigungsdaten: Gerber RS-274X und Excellon-Bohrdaten (Ursprung links unten, mm) ----------
    static string G(double mm) => ((long)Math.Round(mm * 1e6)).ToString(Inv);

    public static string Gerber(Circuit c, string layer)
    {
        var sb = new StringBuilder($"G04 Trendo OS – {c.Name} – {layer}*\n%FSLAX46Y46*%\n%MOMM*%\n%LPD*%\n");
        double Y(double y) => c.Pcb.H - y; var pads = Pads(c);
        switch (layer)
        {
            case "F_Cu": case "B_Cu":
                var bottom = layer == "B_Cu";
                sb.Append($"%ADD10C,{c.Pcb.TraceWidth.ToString("0.###", Inv)}*%\n");
                var ap = 11; var apOf = new Dictionary<(double, bool), int>();
                foreach (var p in pads) if (!apOf.ContainsKey((p.Def.Size, p.Def.Square))) { apOf[(p.Def.Size, p.Def.Square)] = ap; sb.Append(p.Def.Square ? $"%ADD{ap}R,{p.Def.Size.ToString("0.###", Inv)}X{p.Def.Size.ToString("0.###", Inv)}*%\n" : $"%ADD{ap}C,{p.Def.Size.ToString("0.###", Inv)}*%\n"); ap++; }
                sb.Append("D10*\n");
                foreach (var t in c.Pcb.Traces.Where(t => t.Bottom == bottom)) { sb.Append($"X{G(t.Pts[0][0])}Y{G(Y(t.Pts[0][1]))}D02*\n"); foreach (var q in t.Pts.Skip(1)) sb.Append($"X{G(q[0])}Y{G(Y(q[1]))}D01*\n"); }
                foreach (var g in pads.GroupBy(p => apOf[(p.Def.Size, p.Def.Square)])) { sb.Append($"D{g.Key}*\n"); foreach (var p in g) sb.Append($"X{G(p.X)}Y{G(Y(p.Y))}D03*\n"); }
                break;
            case "F_SilkS":
                sb.Append("%ADD10C,0.150*%\nD10*\n");
                foreach (var p in Placeable(c))
                {
                    if (!c.Pcb.Places.TryGetValue(p.Id, out var pl)) continue; var fp = Footprints.For(p.Kind)!; var (w, h) = pl.Rot % 180 == 0 ? (fp.W, fp.H) : (fp.H, fp.W);
                    double x0 = pl.X - w / 2, x1 = pl.X + w / 2, y0 = Y(pl.Y - h / 2), y1 = Y(pl.Y + h / 2);
                    sb.Append($"X{G(x0)}Y{G(y0)}D02*\nX{G(x1)}Y{G(y0)}D01*\nX{G(x1)}Y{G(y1)}D01*\nX{G(x0)}Y{G(y1)}D01*\nX{G(x0)}Y{G(y0)}D01*\n");
                }
                break;
            case "Edge_Cuts":
                sb.Append($"%ADD10C,0.100*%\nD10*\nX0Y0D02*\nX{G(c.Pcb.W)}Y0D01*\nX{G(c.Pcb.W)}Y{G(c.Pcb.H)}D01*\nX0Y{G(c.Pcb.H)}D01*\nX0Y0D01*\n");
                break;
        }
        return sb.Append("M02*\n").ToString();
    }

    public static string Drill(Circuit c)
    {
        var pads = Pads(c); var tools = pads.Select(p => p.Def.Drill).Distinct().OrderBy(d => d).ToList();
        var sb = new StringBuilder("M48\n; Trendo OS Bohrdaten\nMETRIC,TZ\n");
        for (var i = 0; i < tools.Count; i++) sb.Append($"T{i + 1}C{tools[i].ToString("0.000", Inv)}\n");
        sb.Append("%\nG90\nG05\n");
        for (var i = 0; i < tools.Count; i++) { sb.Append($"T{i + 1}\n"); foreach (var p in pads.Where(p => p.Def.Drill == tools[i])) sb.Append($"X{p.X.ToString("0.000", Inv)}Y{(c.Pcb.H - p.Y).ToString("0.000", Inv)}\n"); }
        return sb.Append("T0\nM30\n").ToString();
    }

    /// <summary>Fertigungspaket als ZIP: Kupfer oben/unten, Bestückungsdruck, Umriss, Bohrdaten – so nehmen es Platinenhersteller an.</summary>
    public static void WriteFabZip(Circuit c, string zipPath)
    {
        if (File.Exists(zipPath)) File.Delete(zipPath);
        using var z = ZipFile.Open(zipPath, ZipArchiveMode.Create);
        void Add(string name, string text) { using var w = new StreamWriter(z.CreateEntry(name).Open(), new UTF8Encoding(false)); w.Write(text); }
        var b = string.Concat((c.Name.Length > 0 ? c.Name : "platine").Where(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_'));
        Add($"{b}-F_Cu.gbr", Gerber(c, "F_Cu")); Add($"{b}-B_Cu.gbr", Gerber(c, "B_Cu")); Add($"{b}-F_SilkS.gbr", Gerber(c, "F_SilkS")); Add($"{b}-Edge_Cuts.gbr", Gerber(c, "Edge_Cuts")); Add($"{b}.drl", Drill(c));
    }
}
