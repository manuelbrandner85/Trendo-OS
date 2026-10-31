using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Trendo.Core.Statik;

/// <summary>T-0258 (SPEC-03): ebenes Stabwerk – Knoten mit Lagern, Stäbe mit Querschnitt/Werkstoff/Gelenken, Lasten.
/// Koordinaten in m (y nach oben), Kräfte in kN (Lasten nach unten positiv), Momente in kNm (gegen den Uhrzeigersinn positiv).</summary>
public sealed class FrameModel
{
    public const int FormatVersion = 1;
    public int Version { get; set; } = FormatVersion;
    public string Name { get; set; } = "Tragwerk";
    public bool SelfWeight { get; set; }
    public List<FNode> Nodes { get; set; } = [];
    public List<FMember> Members { get; set; } = [];
    public List<FLoad> Loads { get; set; } = [];

    static readonly JsonSerializerOptions Opt = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    public static string NewId() => Guid.NewGuid().ToString("N")[..8];
    public static FrameModel From(JsonObject data)
    {
        var m = data["frame"] is JsonObject o ? o.Deserialize<FrameModel>(Opt) ?? new() : new();
        m.Loads.RemoveAll(l => l.Kind == "knoten" ? m.Node(l.Target) is null : m.Member(l.Target) is null);
        m.Members.RemoveAll(x => m.Node(x.A) is null || m.Node(x.B) is null || x.A == x.B);
        m.Version = FormatVersion; return m;
    }
    public void WriteTo(JsonObject data) => data["frame"] = JsonSerializer.SerializeToNode(this, Opt);
    public FrameModel Clone() => JsonSerializer.Deserialize<FrameModel>(JsonSerializer.Serialize(this, Opt), Opt)!;

    public FNode? Node(string id) => Nodes.FirstOrDefault(n => n.Id == id);
    public FMember? Member(string id) => Members.FirstOrDefault(m => m.Id == id);

    /// <summary>Knoten an der Stelle finden oder anlegen (fängt auf 1 cm).</summary>
    public FNode NodeAt(double x, double y)
    {
        var n = Nodes.FirstOrDefault(k => Math.Abs(k.X - x) < 0.01 && Math.Abs(k.Y - y) < 0.01);
        if (n is not null) return n;
        n = new FNode { X = Math.Round(x, 3), Y = Math.Round(y, 3) }; Nodes.Add(n); return n;
    }

    public FMember AddMember(double x1, double y1, double x2, double y2, string section = "IPE 200", string material = "Baustahl S235")
    {
        if (Math.Sqrt((x2 - x1) * (x2 - x1) + (y2 - y1) * (y2 - y1)) < 0.05) throw new ArgumentException("Der Stab ist zu kurz (mindestens 5 cm).");
        Sections.Get(section); // prüft den Namen
        var a = NodeAt(x1, y1); var b = NodeAt(x2, y2);
        if (Members.Any(m => m.A == a.Id && m.B == b.Id || m.A == b.Id && m.B == a.Id)) throw new ArgumentException("Hier liegt schon ein Stab.");
        var s = new FMember { A = a.Id, B = b.Id, Section = section, Material = material }; Members.Add(s); return s;
    }

    public bool Remove(string id)
    {
        var n = Members.RemoveAll(m => m.Id == id) + Loads.RemoveAll(l => l.Id == id);
        if (Nodes.RemoveAll(k => k.Id == id) > 0) { n++; Members.RemoveAll(m => m.A == id || m.B == id); }
        Loads.RemoveAll(l => l.Kind == "knoten" ? Node(l.Target) is null : Member(l.Target) is null);
        Nodes.RemoveAll(k => k.Support == "" && Members.All(m => m.A != k.Id && m.B != k.Id) && Loads.All(l => l.Target != k.Id)); // lose Knoten aufräumen
        return n > 0;
    }

    /// <summary>Stab teilen (z. B. um dort ein Lager oder eine Knotenlast zu setzen). t = 0…1.</summary>
    public FNode SplitMember(string id, double t = 0.5)
    {
        var m = Member(id) ?? throw new KeyNotFoundException("Den Stab gibt es nicht mehr.");
        if (t is <= 0.01 or >= 0.99) throw new ArgumentException("So nah am Ende lässt sich der Stab nicht teilen.");
        var a = Node(m.A)!; var b = Node(m.B)!;
        var k = NodeAt(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);
        var second = new FMember { A = k.Id, B = m.B, Section = m.Section, Material = m.Material, HingeB = m.HingeB };
        m.B = k.Id; m.HingeB = false; Members.Insert(Members.IndexOf(m) + 1, second);
        foreach (var l in Loads.Where(l => l.Target == id && l.Kind == "einzel").ToList())
            if (l.At > t) { l.Target = second.Id; l.At = (l.At - t) / (1 - t); } else l.At /= t;
        foreach (var l in Loads.Where(l => l.Target == id && l.Kind == "strecke").ToList())
            Loads.Add(new FLoad { Kind = "strecke", Target = second.Id, Value = l.Value });
        return k;
    }

    public FLoad AddNodeLoad(string node, double down, double right = 0, double moment = 0)
    { var l = new FLoad { Kind = "knoten", Target = node, Value = down, Fx = right, M = moment }; Loads.Add(l); return l; }
    public FLoad AddLineLoad(string member, double q) { var l = new FLoad { Kind = "strecke", Target = member, Value = q }; Loads.Add(l); return l; }
    public FLoad AddPointLoad(string member, double f, double at = 0.5) { var l = new FLoad { Kind = "einzel", Target = member, Value = f, At = Math.Clamp(at, 0, 1) }; Loads.Add(l); return l; }

    public (double MinX, double MinY, double MaxX, double MaxY)? Bounds() =>
        Nodes.Count == 0 ? null : (Nodes.Min(n => n.X), Nodes.Min(n => n.Y), Nodes.Max(n => n.X), Nodes.Max(n => n.Y));

    // ---------- Vorlagen ----------
    public static FrameModel Template(string id)
    {
        var f = new FrameModel();
        switch (id)
        {
            case "einfeld":
                f.Name = "Einfeldträger 5 m"; var m = f.AddMember(0, 0, 5, 0, "IPE 200");
                f.Node(m.A)!.Support = "fest"; f.Node(m.B)!.Support = "los"; f.AddLineLoad(m.Id, 10); break;
            case "kragarm":
                f.Name = "Kragarm 2 m"; var k = f.AddMember(0, 0, 2, 0, "Rechteck 100x200", "Fichte/Nadelholz C24");
                f.Node(k.A)!.Support = "einspannung"; f.AddNodeLoad(k.B, 3); break;
            case "durchlauf":
                f.Name = "Durchlaufträger 2 × 4 m"; var d1 = f.AddMember(0, 0, 4, 0, "IPE 180"); var d2 = f.AddMember(4, 0, 8, 0, "IPE 180");
                f.Node(d1.A)!.Support = "fest"; f.Node(d1.B)!.Support = "los"; f.Node(d2.B)!.Support = "los";
                f.AddLineLoad(d1.Id, 8); f.AddLineLoad(d2.Id, 8); break;
            case "rahmen":
                f.Name = "Zweigelenkrahmen 6 × 4 m"; var s1 = f.AddMember(0, 0, 0, 4, "HEA 200"); var r = f.AddMember(0, 4, 6, 4, "IPE 300"); var s2 = f.AddMember(6, 4, 6, 0, "HEA 200");
                f.Node(s1.A)!.Support = "fest"; f.Node(s2.B)!.Support = "fest";
                f.AddLineLoad(r.Id, 12); f.AddNodeLoad(s1.B, 0, 5); break;
            case "fachwerk":
                f.Name = "Fachwerkbinder 8 m";
                (double, double)[] p = [(0, 0), (2, 0), (4, 0), (6, 0), (8, 0), (2, 1), (4, 1.5), (6, 1)];
                int[][] bars = [[0, 1], [1, 2], [2, 3], [3, 4], [0, 5], [5, 6], [6, 7], [7, 4], [1, 5], [2, 6], [3, 7], [5, 2], [7, 2]];
                foreach (var bar in bars) { var mm = f.AddMember(p[bar[0]].Item1, p[bar[0]].Item2, p[bar[1]].Item1, p[bar[1]].Item2, "Rohr 60x4"); mm.HingeA = mm.HingeB = true; }
                f.NodeAt(0, 0).Support = "fest"; f.NodeAt(8, 0).Support = "los";
                foreach (var x in new[] { 2.0, 4.0, 6.0 }) f.AddNodeLoad(f.NodeAt(x, 0).Id, 10);
                break;
        }
        return f;
    }

    public static string Num(double v, int d = 2) => v.ToString("N" + d, CultureInfo.GetCultureInfo("de-DE"));
}

public sealed class FNode
{
    public string Id { get; set; } = FrameModel.NewId();
    public double X { get; set; }
    public double Y { get; set; }
    /// <summary>"" frei · "fest" (Festlager, hält x und y) · "los" (Loslager, hält y) · "einspannung" (hält x, y und Drehung).</summary>
    public string Support { get; set; } = "";
}

public sealed class FMember
{
    public string Id { get; set; } = FrameModel.NewId();
    public string A { get; set; } = "";
    public string B { get; set; } = "";
    public string Section { get; set; } = "IPE 200";
    public string Material { get; set; } = "Baustahl S235";
    /// <summary>Gelenk am Anfang/Ende: dort wird kein Moment übertragen.</summary>
    public bool HingeA { get; set; }
    public bool HingeB { get; set; }
}

public sealed class FLoad
{
    public string Id { get; set; } = FrameModel.NewId();
    /// <summary>"knoten" (Value nach unten, Fx nach rechts, M) · "strecke" (Value kN/m nach unten auf Stablänge) · "einzel" (Value kN nach unten bei At = 0…1).</summary>
    public string Kind { get; set; } = "knoten";
    public string Target { get; set; } = "";
    public double Value { get; set; }
    public double Fx { get; set; }
    public double M { get; set; }
    public double At { get; set; } = 0.5;
}

public sealed record SectionInfo(string Name, double A_mm2, double I_mm4, double W_mm3, double H_mm);

/// <summary>Querschnitte: Stahlprofile IPE/HEA (Tabellenwerte), Rechteck B×H, Rohr D×t, Hohlprofil B×H×t (mm).</summary>
public static class Sections
{
    // h [mm], A [cm²], Iy [cm⁴], Wy [cm³] – übliche Tabellenwerte
    static readonly (string N, double H, double A, double I, double W)[] Steel =
    [
        ("IPE 80", 80, 7.64, 80.1, 20.0), ("IPE 100", 100, 10.3, 171, 34.2), ("IPE 120", 120, 13.2, 318, 53.0), ("IPE 140", 140, 16.4, 541, 77.3),
        ("IPE 160", 160, 20.1, 869, 109), ("IPE 180", 180, 23.9, 1317, 146), ("IPE 200", 200, 28.5, 1943, 194), ("IPE 220", 220, 33.4, 2772, 252),
        ("IPE 240", 240, 39.1, 3892, 324), ("IPE 270", 270, 45.9, 5790, 429), ("IPE 300", 300, 53.8, 8356, 557), ("IPE 330", 330, 62.6, 11770, 713),
        ("IPE 360", 360, 72.7, 16270, 904), ("IPE 400", 400, 84.5, 23130, 1160), ("IPE 450", 450, 98.8, 33740, 1500), ("IPE 500", 500, 116, 48200, 1930), ("IPE 600", 600, 156, 92080, 3070),
        ("HEA 100", 96, 21.2, 349, 72.8), ("HEA 120", 114, 25.3, 606, 106), ("HEA 140", 133, 31.4, 1033, 155), ("HEA 160", 152, 38.8, 1673, 220),
        ("HEA 180", 171, 45.3, 2510, 294), ("HEA 200", 190, 53.8, 3692, 389), ("HEA 220", 210, 64.3, 5410, 515), ("HEA 240", 230, 76.8, 7763, 675),
        ("HEA 260", 250, 86.8, 10450, 836), ("HEA 280", 270, 97.3, 13670, 1010), ("HEA 300", 290, 112, 18260, 1260),
    ];
    public static IEnumerable<string> SteelNames => Steel.Select(s => s.N);
    public static readonly string[] Suggestions = [.. Steel.Select(s => s.N), "Rechteck 60x120", "Rechteck 100x200", "Rechteck 120x240", "Rohr 60x4", "Rohr 100x5", "Hohlprofil 100x100x5"];

    public static SectionInfo Get(string name)
    {
        var n = (name ?? "").Trim();
        foreach (var s in Steel) if (string.Equals(s.N, n, StringComparison.OrdinalIgnoreCase) || string.Equals(s.N.Replace(" ", ""), n.Replace(" ", ""), StringComparison.OrdinalIgnoreCase))
                return new(s.N, s.A * 100, s.I * 1e4, s.W * 1e3, s.H);
        var parts = n.Split(' ', 2);
        double[] Dims(string t) => t.Split('x', '×', 'X').Select(v => double.Parse(v.Trim().Replace(',', '.'), CultureInfo.InvariantCulture)).ToArray();
        try
        {
            switch (parts[0].ToLowerInvariant())
            {
                case "rechteck": { var d = Dims(parts[1]); var (a, i, w) = Engineering.Rect(d[0], d[1]); return new($"Rechteck {d[0]:0.#}x{d[1]:0.#}", a, i, w, d[1]); }
                case "rohr": { var d = Dims(parts[1]); var (a, i, w) = Engineering.Tube(d[0], d[1]); return new($"Rohr {d[0]:0.#}x{d[1]:0.#}", a, i, w, d[0]); }
                case "hohlprofil": { var d = Dims(parts[1]); var (a, i, w) = EngineeringPlus.HollowRect(d[0], d[1], d[2]); return new($"Hohlprofil {d[0]:0.#}x{d[1]:0.#}x{d[2]:0.#}", a, i, w, d[1]); }
            }
        }
        catch (Exception ex) when (ex is FormatException or IndexOutOfRangeException) { }
        throw new ArgumentException($"Querschnitt „{name}“ unbekannt – z. B. IPE 200, HEA 160, Rechteck 60x120, Rohr 60x4, Hohlprofil 100x100x5 (mm).");
    }
}

public sealed record MemberResult(string Id, double L, double[] Xs, double[] N, double[] V, double[] M, double[] Dx, double[] Dy,
    double MaxN, double MaxV, double MaxM, double MaxDefl_mm, double Stress_MPa, double Safety, double? BuckleSafety, bool Ok, string Verdict);
public sealed record FrameResult(Dictionary<string, (double Rx, double Ry, double M)> Reactions, Dictionary<string, (double Ux, double Uy, double Rot)> Displacements, List<MemberResult> Members, bool Ok)
{
    public MemberResult? Of(string id) => Members.FirstOrDefault(m => m.Id == id);
}

/// <summary>Weggrößenverfahren (direkte Steifigkeitsmethode) für ebene Rahmen: 3 Freiheitsgrade je Knoten, Gelenke durch statische Kondensation,
/// Schnittgrößen aus dem Gleichgewicht am Stab, Biegelinie durch zweifache Integration von M/EI.</summary>
public static class FrameSolver
{
    const double G = 9.81;

    sealed record El(FMember M, FNode A, FNode B, double L, double C, double S, double EA, double EI, SectionInfo Sec, Material Mat, double[,] K, double[] P);

    public static FrameResult Solve(FrameModel f, int samples = 41)
    {
        if (f.Members.Count == 0) throw new InvalidOperationException("Es gibt noch keinen Stab – zuerst Stäbe zeichnen.");
        if (f.Nodes.All(n => n.Support == "")) throw new InvalidOperationException("Das Tragwerk hat keine Lager – es würde wegfliegen. Bitte Lager setzen.");
        var idx = f.Nodes.Select((n, i) => (n.Id, i)).ToDictionary(x => x.Id, x => x.i);
        var dof = 3 * f.Nodes.Count; var K = new double[dof, dof]; var F = new double[dof];
        var els = new List<El>();
        foreach (var m in f.Members)
        {
            var a = f.Node(m.A)!; var b = f.Node(m.B)!; var dx = b.X - a.X; var dy = b.Y - a.Y; var L = Math.Sqrt(dx * dx + dy * dy);
            var sec = Sections.Get(m.Section); var mat = Engineering.Mat(m.Material);
            var E = mat.E_GPa * 1e6; var EA = E * sec.A_mm2 * 1e-6; var EI = E * sec.I_mm4 * 1e-12;
            var c = dx / L; var s = dy / L;
            var k = Local(EA, EI, L); var p = new double[6];
            // Lasten am Stab (global nach unten) → örtliche Ersatz-Knotenlasten
            var q = f.Loads.Where(l => l.Kind == "strecke" && l.Target == m.Id).Sum(l => l.Value) + (f.SelfWeight ? mat.Density * sec.A_mm2 * 1e-6 * G / 1000 : 0);
            if (q != 0) { var wx = -q * s; var wy = -q * c; Add(p, [wx * L / 2, wy * L / 2, wy * L * L / 12, wx * L / 2, wy * L / 2, -wy * L * L / 12]); }
            foreach (var l in f.Loads.Where(l => l.Kind == "einzel" && l.Target == m.Id))
            {
                var aa = l.At * L; var bb = L - aa; var px = -l.Value * s; var py = -l.Value * c;
                Add(p, [px * bb / L, py * bb * bb * (3 * aa + bb) / (L * L * L), py * aa * bb * bb / (L * L), px * aa / L, py * aa * aa * (aa + 3 * bb) / (L * L * L), -py * aa * aa * bb / (L * L)]);
            }
            // Gelenke: Drehfreiheitsgrad am Stabende auskondensieren
            var rel = new List<int>(); if (m.HingeA) rel.Add(2); if (m.HingeB) rel.Add(5);
            if (rel.Count > 0) (k, p) = Condense(k, p, rel);
            els.Add(new El(m, a, b, L, c, s, EA, EI, sec, mat, k, p));
            var T = Rot(c, s); var kg = Mul(Transpose(T), Mul(k, T)); var pg = MulV(Transpose(T), p);
            int[] map = [3 * idx[a.Id], 3 * idx[a.Id] + 1, 3 * idx[a.Id] + 2, 3 * idx[b.Id], 3 * idx[b.Id] + 1, 3 * idx[b.Id] + 2];
            for (var i = 0; i < 6; i++) { F[map[i]] += pg[i]; for (var j = 0; j < 6; j++) K[map[i], map[j]] += kg[i, j]; }
        }
        foreach (var l in f.Loads.Where(l => l.Kind == "knoten")) { var i = 3 * idx[l.Target]; F[i] += l.Fx; F[i + 1] -= l.Value; F[i + 2] += l.M; }
        // Knoten, an denen alle Stäbe gelenkig hängen: Drehung mit winziger Feder festhalten (sonst singulär, ohne Einfluss auf das Ergebnis)
        var kmax = Enumerable.Range(0, dof).Max(i => Math.Abs(K[i, i]));
        for (var n = 0; n < f.Nodes.Count; n++) if (Math.Abs(K[3 * n + 2, 3 * n + 2]) < kmax * 1e-12) K[3 * n + 2, 3 * n + 2] = kmax * 1e-9;
        // Lager
        var fixedDof = new bool[dof];
        foreach (var n in f.Nodes)
        {
            var i = 3 * idx[n.Id];
            switch (n.Support) { case "fest": fixedDof[i] = fixedDof[i + 1] = true; break; case "los": fixedDof[i + 1] = true; break; case "einspannung": fixedDof[i] = fixedDof[i + 1] = fixedDof[i + 2] = true; break; }
        }
        var free = Enumerable.Range(0, dof).Where(i => !fixedDof[i]).ToArray();
        var Kff = new double[free.Length, free.Length]; var Ff = new double[free.Length];
        for (var i = 0; i < free.Length; i++) { Ff[i] = F[free[i]]; for (var j = 0; j < free.Length; j++) Kff[i, j] = K[free[i], free[j]]; }
        var uf = SolveLinear(Kff, Ff, kmax);
        var U = new double[dof]; for (var i = 0; i < free.Length; i++) U[free[i]] = uf[i];
        // Lagerreaktionen R = K·U − F
        var reactions = new Dictionary<string, (double, double, double)>();
        foreach (var n in f.Nodes.Where(n => n.Support != ""))
        {
            var i = 3 * idx[n.Id]; var r = new double[3];
            for (var k = 0; k < 3; k++) { if (!fixedDof[i + k]) continue; double sum = 0; for (var j = 0; j < dof; j++) sum += K[i + k, j] * U[j]; r[k] = sum - F[i + k]; }
            reactions[n.Id] = (Math.Round(r[0], 6), Math.Round(r[1], 6), Math.Round(r[2], 6));
        }
        var disp = f.Nodes.ToDictionary(n => n.Id, n => (U[3 * idx[n.Id]], U[3 * idx[n.Id] + 1], U[3 * idx[n.Id] + 2]));
        var results = els.Select(e => Member(f, e, U, idx, samples)).ToList();
        return new(reactions, disp, results, results.All(r => r.Ok));
    }

    static MemberResult Member(FrameModel f, El e, double[] U, Dictionary<string, int> idx, int n)
    {
        var T = Rot(e.C, e.S); int ia = 3 * idx[e.A.Id], ib = 3 * idx[e.B.Id];
        var dg = new[] { U[ia], U[ia + 1], U[ia + 2], U[ib], U[ib + 1], U[ib + 2] };
        var dl = MulV(T, dg);
        var kd = MulV(e.K, dl); var fe = new double[6]; for (var i = 0; i < 6; i++) fe[i] = kd[i] - e.P[i]; // Stabendkräfte (örtlich, am Stab)
        var q = f.Loads.Where(l => l.Kind == "strecke" && l.Target == e.M.Id).Sum(l => l.Value) + (f.SelfWeight ? e.Mat.Density * e.Sec.A_mm2 * 1e-6 * G / 1000 : 0);
        var wx = -q * e.S; var wy = -q * e.C;
        var pts = f.Loads.Where(l => l.Kind == "einzel" && l.Target == e.M.Id).Select(l => (a: l.At * e.L, px: -l.Value * e.S, py: -l.Value * e.C)).ToList();
        var xs = new double[n]; var N = new double[n]; var V = new double[n]; var M = new double[n];
        for (var i = 0; i < n; i++)
        {
            var x = e.L * i / (n - 1); xs[i] = x;
            double sx = 0, sy = 0, sm = 0;
            foreach (var p in pts) if (p.a < x || p.a <= x && i == n - 1) { sx += p.px; sy += p.py; sm += p.py * (x - p.a); }
            N[i] = -(fe[0] + wx * x + sx);
            V[i] = fe[1] + wy * x + sy;
            M[i] = -fe[2] + fe[1] * x + wy * x * x / 2 + sm;
        }
        // Biegelinie: v'' = M/EI, Endwerte aus den Knotenverschiebungen (Sehne) + Krümmungsanteil
        var I1 = new double[n]; var I2 = new double[n];
        for (var i = 1; i < n; i++) { var h = xs[i] - xs[i - 1]; I1[i] = I1[i - 1] + (M[i] + M[i - 1]) / 2 * h / e.EI; I2[i] = I2[i - 1] + (I1[i] + I1[i - 1]) / 2 * h; }
        var dx = new double[n]; var dy = new double[n]; double maxRel = 0;
        for (var i = 0; i < n; i++)
        {
            var t = xs[i] / e.L; var rel = I2[i] - I2[n - 1] * t;
            var u = dl[0] + (dl[3] - dl[0]) * t; var v = dl[1] + (dl[4] - dl[1]) * t + rel;
            maxRel = Math.Max(maxRel, Math.Abs(v)); // Durchbiegung quer zum Stab, absolut (Kragarmspitze zählt mit)
            dx[i] = u * e.C - v * e.S; dy[i] = u * e.S + v * e.C;
        }
        var maxN = N.MaxBy(Math.Abs); var maxV = V.MaxBy(Math.Abs); var maxM = M.MaxBy(Math.Abs);
        var stress = Enumerable.Range(0, n).Max(i => Math.Abs(N[i]) / (e.Sec.A_mm2 * 1e-6) + Math.Abs(M[i]) / (e.Sec.W_mm3 * 1e-9)) / 1000; // kN/m² → N/mm²
        var safety = stress < 1e-9 ? 99 : e.Mat.Yield_MPa / stress;
        double? buckle = null;
        var minN = N.Min(); if (minN < -1e-6) buckle = Math.PI * Math.PI * e.EI / (e.L * e.L) / -minN; // Euler-Fall 2 (gelenkig–gelenkig)
        var defl = maxRel * 1000; var limit = e.L * 1000 / 300;
        var problems = new List<string>();
        if (safety < 1.5) problems.Add($"Spannung zu hoch (Sicherheit {FrameModel.Num(safety)} < 1,5)");
        if (defl > limit) problems.Add($"biegt sich zu stark ({FrameModel.Num(defl, 1)} mm > L/300 = {FrameModel.Num(limit, 1)} mm)");
        if (buckle is < 2) problems.Add($"Knickgefahr (Sicherheit {FrameModel.Num(buckle.Value)} < 2)");
        return new(e.M.Id, e.L, xs, N, V, M, dx, dy, maxN, maxV, maxM, Math.Round(defl, 2), Math.Round(stress, 2), Math.Round(safety, 2), buckle is { } bk ? Math.Round(bk, 2) : null,
            problems.Count == 0, problems.Count == 0 ? "✓ hält – Spannung, Durchbiegung und Knicken im grünen Bereich" : "⚠ " + string.Join(", ", problems));
    }

    static double[,] Local(double EA, double EI, double L)
    {
        double a = EA / L, b = 12 * EI / (L * L * L), c = 6 * EI / (L * L), d = 4 * EI / L, e = 2 * EI / L;
        return new double[,]
        {
            { a, 0, 0, -a, 0, 0 }, { 0, b, c, 0, -b, c }, { 0, c, d, 0, -c, e },
            { -a, 0, 0, a, 0, 0 }, { 0, -b, -c, 0, b, -c }, { 0, c, e, 0, -c, d },
        };
    }

    /// <summary>Statische Kondensation der freigegebenen Freiheitsgrade: k* = k_rr − k_rc·k_cc⁻¹·k_cr, p* = p_r − k_rc·k_cc⁻¹·p_c.</summary>
    static (double[,], double[]) Condense(double[,] k, double[] p, List<int> rel)
    {
        var keep = Enumerable.Range(0, 6).Where(i => !rel.Contains(i)).ToArray();
        var kcc = new double[rel.Count, rel.Count]; for (var i = 0; i < rel.Count; i++) for (var j = 0; j < rel.Count; j++) kcc[i, j] = k[rel[i], rel[j]];
        var inv = Invert(kcc);
        var kn = new double[6, 6]; var pn = new double[6];
        foreach (var i in keep)
        {
            double pc = 0;
            for (var a = 0; a < rel.Count; a++) for (var b = 0; b < rel.Count; b++) pc += k[i, rel[a]] * inv[a, b] * p[rel[b]];
            pn[i] = p[i] - pc;
            foreach (var j in keep)
            {
                double s = 0;
                for (var a = 0; a < rel.Count; a++) for (var b = 0; b < rel.Count; b++) s += k[i, rel[a]] * inv[a, b] * k[rel[b], j];
                kn[i, j] = k[i, j] - s;
            }
        }
        return (kn, pn);
    }

    static double[,] Invert(double[,] m)
    {
        var n = m.GetLength(0); var r = new double[n, n];
        if (n == 1) { r[0, 0] = 1 / m[0, 0]; return r; }
        var det = m[0, 0] * m[1, 1] - m[0, 1] * m[1, 0];
        r[0, 0] = m[1, 1] / det; r[1, 1] = m[0, 0] / det; r[0, 1] = -m[0, 1] / det; r[1, 0] = -m[1, 0] / det; return r;
    }

    static double[,] Rot(double c, double s) => new double[,]
    {
        { c, s, 0, 0, 0, 0 }, { -s, c, 0, 0, 0, 0 }, { 0, 0, 1, 0, 0, 0 },
        { 0, 0, 0, c, s, 0 }, { 0, 0, 0, -s, c, 0 }, { 0, 0, 0, 0, 0, 1 },
    };
    static void Add(double[] a, double[] b) { for (var i = 0; i < a.Length; i++) a[i] += b[i]; }
    static double[,] Transpose(double[,] a) { var r = new double[6, 6]; for (var i = 0; i < 6; i++) for (var j = 0; j < 6; j++) r[i, j] = a[j, i]; return r; }
    static double[,] Mul(double[,] a, double[,] b) { var r = new double[6, 6]; for (var i = 0; i < 6; i++) for (var j = 0; j < 6; j++) { double s = 0; for (var k = 0; k < 6; k++) s += a[i, k] * b[k, j]; r[i, j] = s; } return r; }
    static double[] MulV(double[,] a, double[] v) { var r = new double[6]; for (var i = 0; i < 6; i++) { double s = 0; for (var k = 0; k < 6; k++) s += a[i, k] * v[k]; r[i] = s; } return r; }

    /// <summary>Gauß mit Spaltenpivot; ein fehlender Pivot heißt: das Tragwerk ist beweglich.</summary>
    static double[] SolveLinear(double[,] A, double[] b, double scale)
    {
        var n = b.Length; var a = (double[,])A.Clone(); var x = (double[])b.Clone();
        for (var col = 0; col < n; col++)
        {
            var piv = col; for (var r = col + 1; r < n; r++) if (Math.Abs(a[r, col]) > Math.Abs(a[piv, col])) piv = r;
            if (Math.Abs(a[piv, col]) < scale * 1e-10) throw new InvalidOperationException("Das Tragwerk ist beweglich – es fehlen Lager, oder Gelenke machen es instabil (kinematisch). Bitte Lager oder Einspannung ergänzen.");
            if (piv != col) { for (var k = 0; k < n; k++) (a[col, k], a[piv, k]) = (a[piv, k], a[col, k]); (x[col], x[piv]) = (x[piv], x[col]); }
            for (var r = col + 1; r < n; r++)
            {
                var f = a[r, col] / a[col, col]; if (f == 0) continue;
                for (var k = col; k < n; k++) a[r, k] -= f * a[col, k];
                x[r] -= f * x[col];
            }
        }
        for (var r = n - 1; r >= 0; r--) { var s = x[r]; for (var k = r + 1; k < n; k++) s -= a[r, k] * x[k]; x[r] = s / a[r, r]; }
        return x;
    }
}
