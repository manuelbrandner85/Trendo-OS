using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Trendo.Core.Elektro;

/// <summary>T-0259 (SPEC-02): Schaltung auf einem Raster – jedes Bauteil hat zwei Anschlüsse auf Rasterpunkten; Anschlüsse am selben Punkt
/// und Drähte verbinden. Einheiten: Ω, F, H, V, A, Hz; Raster-Einheit = 1 (Anzeige skaliert).</summary>
public sealed class Circuit
{
    public const int FormatVersion = 1;
    public int Version { get; set; } = FormatVersion;
    public string Name { get; set; } = "Schaltung";
    public List<Part> Parts { get; set; } = [];

    static readonly JsonSerializerOptions Opt = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    public static string NewId() => Guid.NewGuid().ToString("N")[..8];
    public static Circuit From(JsonObject data) { var c = data["circuit"] is JsonObject o ? o.Deserialize<Circuit>(Opt) ?? new() : new(); c.Parts.RemoveAll(p => p.X1 == p.X2 && p.Y1 == p.Y2 && p.Kind != "masse"); return c; }
    public void WriteTo(JsonObject data) => data["circuit"] = JsonSerializer.SerializeToNode(this, Opt);
    public Circuit Clone() => JsonSerializer.Deserialize<Circuit>(JsonSerializer.Serialize(this, Opt), Opt)!;
    public Part? Part(string id) => Parts.FirstOrDefault(p => p.Id == id);

    public Part Add(string kind, int x1, int y1, int x2, int y2, double? value = null)
    {
        var info = PartTypes.Get(kind);
        if (kind != "masse" && x1 == x2 && y1 == y2) throw new ArgumentException("Das Bauteil braucht zwei verschiedene Anschlusspunkte.");
        var p = new Part { Kind = kind, X1 = x1, Y1 = y1, X2 = x2, Y2 = y2, Value = value ?? info.Default, Name = NextName(info.Prefix) };
        Parts.Add(p); return p;
    }

    string NextName(string prefix) { if (prefix == "") return ""; var n = 1; while (Parts.Any(p => p.Name == prefix + n)) n++; return prefix + n; }

    public bool Remove(string id) => Parts.RemoveAll(p => p.Id == id) > 0;

    // ---------- Vorlagen ----------
    public static Circuit Template(string id)
    {
        var c = new Circuit();
        switch (id)
        {
            case "led":
                c.Name = "LED mit Vorwiderstand";
                c.Add("spannung", 0, 6, 0, 0, 9); c.Add("draht", 0, 0, 4, 0); c.Add("widerstand", 4, 0, 8, 0, 330); c.Add("draht", 8, 0, 10, 0);
                c.Add("led", 10, 0, 10, 6); c.Add("draht", 10, 6, 0, 6); c.Add("masse", 0, 6, 0, 7); break;
            case "teiler":
                c.Name = "Spannungsteiler";
                c.Add("spannung", 0, 8, 0, 0, 12); c.Add("draht", 0, 0, 6, 0); c.Add("widerstand", 6, 0, 6, 4, 10000); c.Add("widerstand", 6, 4, 6, 8, 4700); c.Add("draht", 6, 8, 0, 8); c.Add("masse", 0, 8, 0, 9); break;
            case "rc":
                c.Name = "RC-Glied (Kondensator laden)";
                c.Add("spannung", 0, 6, 0, 0, 5); c.Add("schalter", 0, 0, 4, 0).Closed = true; c.Add("widerstand", 4, 0, 8, 0, 1000); c.Add("kondensator", 8, 0, 8, 6, 100e-6); c.Add("draht", 8, 6, 0, 6); c.Add("masse", 0, 6, 0, 7); break;
            case "gleichrichter":
                c.Name = "Einweggleichrichter mit Glättung";
                c.Add("wechsel", 0, 6, 0, 0, 12); c.Add("diode", 0, 0, 6, 0); c.Add("kondensator", 6, 0, 6, 6, 470e-6); c.Add("draht", 6, 0, 10, 0); c.Add("widerstand", 10, 0, 10, 6, 1000); c.Add("draht", 10, 6, 0, 6); c.Add("masse", 0, 6, 0, 7); break;
            case "schwingkreis":
                c.Name = "LC-Schwingkreis";
                c.Add("spannung", 0, 6, 0, 0, 10); c.Add("schalter", 0, 0, 4, 0).Closed = true; c.Add("widerstand", 4, 0, 8, 0, 10); c.Add("spule", 8, 0, 8, 6, 1); c.Add("kondensator", 8, 6, 0, 6, 15e-6); c.Add("masse", 0, 6, 0, 7); break;
        }
        return c;
    }

    // ---------- SPICE-Netzliste und Stückliste ----------
    /// <summary>Netzliste für ngspice/LTspice (Masse = Knoten 0).</summary>
    public string Spice(double tranStop = 0.05)
    {
        var net = Netlist.Build(this); var sb = new StringBuilder($"* {Name} – Trendo Schaltung\n");
        string N(double v) => v.ToString("0.######E+0", CultureInfo.InvariantCulture);
        var k = 0;
        foreach (var p in Parts.Where(p => p.Kind is not ("draht" or "masse")))
        {
            var (a, b) = (net.NodeOf(p.X1, p.Y1), net.NodeOf(p.X2, p.Y2)); var nm = p.Name.Length > 0 ? p.Name : "X" + ++k;
            sb.Append(p.Kind switch
            {
                "widerstand" or "lampe" => $"R{nm} {a} {b} {N(p.Value)}\n",
                "kondensator" => $"C{nm} {a} {b} {N(p.Value)}\n",
                "spule" => $"L{nm} {a} {b} {N(p.Value)}\n",
                "spannung" => $"V{nm} {b} {a} DC {N(p.Value)}\n",
                "wechsel" => $"V{nm} {b} {a} SIN(0 {N(p.Value)} {N(p.Freq)})\n",
                "diode" => $"D{nm} {a} {b} DSI\n",
                "led" => $"D{nm} {a} {b} DLED\n",
                "schalter" => p.Closed ? $"R{nm} {a} {b} 1E-3\n" : $"R{nm} {a} {b} 1E+12\n",
                _ => "",
            });
        }
        sb.Append(".model DSI D(IS=2.52E-9 N=1.752)\n.model DLED D(IS=1E-19 N=2)\n");
        sb.Append($".tran 1E-4 {N(tranStop)}\n.end\n");
        return sb.ToString();
    }

    public string BomCsv()
    {
        var sb = new StringBuilder("Bauteil;Wert;Anzahl;Namen\n");
        foreach (var g in Parts.Where(p => PartTypes.Get(p.Kind).Prefix != "").GroupBy(p => (p.Kind, Value: PartTypes.Format(p))).OrderBy(g => g.Key.Kind))
            sb.Append($"{PartTypes.Get(g.Key.Kind).Label};{g.Key.Value};{g.Count()};{string.Join(" ", g.Select(p => p.Name))}\n");
        return sb.ToString();
    }
}

public sealed class Part
{
    public string Id { get; set; } = Circuit.NewId();
    /// <summary>draht, widerstand, kondensator, spule, spannung, wechsel, masse, schalter, diode, led, lampe.</summary>
    public string Kind { get; set; } = "draht";
    public string Name { get; set; } = "";
    public int X1 { get; set; }
    public int Y1 { get; set; }
    public int X2 { get; set; }
    public int Y2 { get; set; }
    /// <summary>Hauptwert: Ω, F, H, V (bei Wechselspannung: Scheitelwert), Lampe: Ω.</summary>
    public double Value { get; set; }
    public double Freq { get; set; } = 50;
    public bool Closed { get; set; } = true;
    /// <summary>Nur LED: Farbe (rot, gruen, blau, weiss, gelb).</summary>
    public string Color { get; set; } = "rot";
}

public sealed record PartType(string Kind, string Label, string Prefix, double Default, string Unit);

public static class PartTypes
{
    public static readonly IReadOnlyList<PartType> All =
    [
        new("draht", "Draht", "", 0, ""), new("widerstand", "Widerstand", "R", 1000, "Ω"), new("kondensator", "Kondensator", "C", 10e-6, "F"),
        new("spule", "Spule", "L", 0.01, "H"), new("spannung", "Batterie / Gleichspannung", "V", 9, "V"), new("wechsel", "Wechselspannung", "V", 12, "V"),
        new("masse", "Masse (0 V)", "", 0, ""), new("schalter", "Schalter", "S", 0, ""), new("diode", "Diode", "D", 0, ""), new("led", "Leuchtdiode", "LED", 0, ""),
        new("lampe", "Glühlampe", "H", 24, "Ω"),
    ];
    public static PartType Get(string kind) => All.FirstOrDefault(t => t.Kind == kind) ?? throw new ArgumentException($"Bauteil „{kind}“ gibt es nicht.");
    public static string Format(Part p) => p.Kind switch
    {
        "widerstand" or "lampe" => Electronics.Ohm(p.Value), "kondensator" => ElectronicsPlus.Fmt(p.Value, "F"), "spule" => ElectronicsPlus.Fmt(p.Value, "H"),
        "spannung" => ElectronicsPlus.Fmt(p.Value, "V"), "wechsel" => ElectronicsPlus.Fmt(p.Value, "V") + " " + ElectronicsPlus.Fmt(p.Freq, "Hz"),
        "schalter" => p.Closed ? "zu" : "offen", "led" => p.Color, _ => "",
    };
}

/// <summary>Knoten aus Anschlusspunkten: Drähte und gemeinsame Punkte verbinden (Union-Find). Masse = Knoten 0.</summary>
public sealed class Netlist
{
    readonly Dictionary<(int, int), int> _node = [];
    public int Count { get; private set; }
    public bool HasGround { get; private set; }
    public int NodeOf(int x, int y) => _node.TryGetValue((x, y), out var n) ? n : -1;

    public static Netlist Build(Circuit c)
    {
        var parent = new Dictionary<(int, int), (int, int)>();
        (int, int) Find((int, int) p) { if (!parent.TryGetValue(p, out var q)) { parent[p] = p; return p; } if (q == p) return p; var r = Find(q); parent[p] = r; return r; }
        void Union((int, int) a, (int, int) b) { var ra = Find(a); var rb = Find(b); if (ra != rb) parent[ra] = rb; }
        foreach (var p in c.Parts) { Find((p.X1, p.Y1)); Find((p.X2, p.Y2)); if (p.Kind == "draht") Union((p.X1, p.Y1), (p.X2, p.Y2)); }
        // Drahtenden, die auf einem anderen Draht liegen (T-Abzweig), verbinden
        foreach (var w in c.Parts.Where(p => p.Kind == "draht"))
            foreach (var pt in parent.Keys.ToList())
                if (OnSegment(pt, (w.X1, w.Y1), (w.X2, w.Y2))) Union(pt, (w.X1, w.Y1));
        var n = new Netlist(); var ids = new Dictionary<(int, int), int>();
        var grounds = c.Parts.Where(p => p.Kind == "masse").Select(p => Find((p.X1, p.Y1))).ToHashSet();
        n.HasGround = grounds.Count > 0;
        if (!n.HasGround) // ohne Masse: Minuspol der ersten Quelle ist 0 V
        {
            var s = c.Parts.FirstOrDefault(p => p.Kind is "spannung" or "wechsel");
            if (s is not null) grounds.Add(Find((s.X1, s.Y1)));
        }
        foreach (var g in grounds) ids[g] = 0;
        var next = 1;
        foreach (var pt in parent.Keys) { var r = Find(pt); if (!ids.TryGetValue(r, out var id)) ids[r] = id = next++; n._node[pt] = id; }
        n.Count = next; return n;
    }

    static bool OnSegment((int X, int Y) p, (int X, int Y) a, (int X, int Y) b)
    {
        if (p == a || p == b) return false;
        var cross = (long)(b.X - a.X) * (p.Y - a.Y) - (long)(b.Y - a.Y) * (p.X - a.X); if (cross != 0) return false;
        return Math.Min(a.X, b.X) <= p.X && p.X <= Math.Max(a.X, b.X) && Math.Min(a.Y, b.Y) <= p.Y && p.Y <= Math.Max(a.Y, b.Y);
    }
}

/// <summary>Ergebnis eines Zeitschritts: Knotenspannungen und Ströme/Spannungen je Bauteil (Strom fließt von Anschluss 1 nach 2 positiv).</summary>
public sealed record SimState(double Time, double[] NodeV, Dictionary<string, (double V, double I)> Parts);

/// <summary>Transiente Simulation mit modifizierter Knotenanalyse (MNA): Kondensator und Spule als Begleitmodelle (implizites Euler-Verfahren),
/// Dioden nach Shockley mit Newton-Iteration und Spannungsbegrenzung. Spannungsquellen bekommen eine eigene Stromgleichung.</summary>
public sealed class Simulator
{
    readonly Circuit _c; readonly Netlist _net; readonly List<Part> _parts;
    readonly Dictionary<string, double> _capV = [], _indI = [], _diodeV = [];
    public double Dt { get; }
    public double Time { get; private set; }
    public SimState? Last { get; private set; }
    public string? Problem { get; private set; }

    public Simulator(Circuit c, double dt = 1e-4)
    {
        _c = c; _net = Netlist.Build(c); Dt = dt;
        _parts = c.Parts.Where(p => p.Kind is not ("draht" or "masse")).ToList();
        if (!_parts.Any(p => p.Kind is "spannung" or "wechsel")) Problem = "Es gibt keine Spannungsquelle – bitte eine Batterie oder Wechselspannung einsetzen.";
    }

    static (double Is, double N) DiodeModel(Part p) => p.Kind == "led" ? (1e-19, 2.0) : (2.52e-9, 1.752);
    const double Vt = 0.025852;

    public SimState Step()
    {
        var n = _net.Count - 1; // ohne Masse
        var sources = _parts.Where(p => p.Kind is "spannung" or "wechsel").ToList();
        var size = n + sources.Count;
        double[] x = new double[size];
        for (var iter = 0; iter < 200; iter++)
        {
            var diodeMoved = false;
            var G = new double[size, size]; var b = new double[size];
            void Stamp(int a, int c, double g) { if (a > 0) G[a - 1, a - 1] += g; if (c > 0) G[c - 1, c - 1] += g; if (a > 0 && c > 0) { G[a - 1, c - 1] -= g; G[c - 1, a - 1] -= g; } }
            void Inject(int a, int c, double i) { if (a > 0) b[a - 1] -= i; if (c > 0) b[c - 1] += i; } // Strom i von a nach c durch das Bauteil
            for (var k = 0; k < n; k++) G[k, k] += 1e-12; // Leck gegen schwebende Knoten
            var si = 0;
            foreach (var p in _parts)
            {
                int a = _net.NodeOf(p.X1, p.Y1), c = _net.NodeOf(p.X2, p.Y2);
                switch (p.Kind)
                {
                    case "widerstand": case "lampe": Stamp(a, c, 1 / Math.Max(1e-6, p.Value)); break;
                    case "schalter": Stamp(a, c, p.Closed ? 1e3 : 1e-12); break;
                    case "kondensator":
                        { var g = Math.Max(1e-15, p.Value) / Dt; var v0 = _capV.GetValueOrDefault(p.Id); Stamp(a, c, g); Inject(a, c, -g * v0); break; }
                    case "spule":
                        { var g = Dt / Math.Max(1e-12, p.Value); var i0 = _indI.GetValueOrDefault(p.Id); Stamp(a, c, g); Inject(a, c, i0); break; }
                    case "diode": case "led":
                        {
                            var (Is, N) = DiodeModel(p); var nvt = N * Vt;
                            var vold = _diodeV.GetValueOrDefault(p.Id);
                            var vd = Limit(V(x, a) - V(x, c), vold, nvt, Is);
                            if (Math.Abs(vd - vold) > 1e-7) diodeMoved = true;
                            _diodeV[p.Id] = vd;
                            var e = Math.Exp(Math.Min(vd / nvt, 80)); var id = Is * (e - 1); var gd = Is / nvt * e + 1e-12;
                            Stamp(a, c, gd); Inject(a, c, id - gd * vd); break;
                        }
                    case "spannung": case "wechsel":
                        {
                            var k = n + si++; var val = p.Kind == "spannung" ? p.Value : p.Value * Math.Sin(2 * Math.PI * p.Freq * (Time + Dt));
                            // Pluspol = Anschluss 2: V(c) − V(a) = val
                            if (c > 0) { G[c - 1, k] += 1; G[k, c - 1] += 1; }
                            if (a > 0) { G[a - 1, k] -= 1; G[k, a - 1] -= 1; }
                            b[k] = val; break;
                        }
                }
            }
            double[] nx;
            try { nx = Solve(G, b); }
            catch (InvalidOperationException) { Problem = "Die Schaltung lässt sich nicht berechnen – vermutlich ein Kurzschluss über eine Spannungsquelle oder zwei Quellen parallel."; nx = new double[size]; }
            // fertig erst, wenn sich weder Knotenspannungen noch Diodenspannungen noch bewegen
            var converged = _parts.All(p => p.Kind is not ("diode" or "led")) || !diodeMoved && Enumerable.Range(0, size).All(i => Math.Abs(nx[i] - x[i]) < 1e-6 + 1e-6 * Math.Abs(nx[i]));
            x = nx;
            if (converged && iter > 0 || _parts.All(p => p.Kind is not ("diode" or "led"))) break;
        }
        Time += Dt;
        // Ergebnisse und Zustände fortschreiben
        var nodeV = new double[_net.Count]; for (var i = 1; i < _net.Count; i++) nodeV[i] = x[i - 1];
        var res = new Dictionary<string, (double, double)>(); var sIdx = 0;
        foreach (var p in _parts)
        {
            int a = _net.NodeOf(p.X1, p.Y1), c = _net.NodeOf(p.X2, p.Y2);
            var v = V(x, a) - V(x, c); double i;
            switch (p.Kind)
            {
                case "widerstand": case "lampe": i = v / Math.Max(1e-6, p.Value); break;
                case "schalter": i = v * (p.Closed ? 1e3 : 1e-12); break;
                case "kondensator": i = Math.Max(1e-15, p.Value) / Dt * (v - _capV.GetValueOrDefault(p.Id)); _capV[p.Id] = v; break;
                case "spule": i = _indI.GetValueOrDefault(p.Id) + Dt / Math.Max(1e-12, p.Value) * v; _indI[p.Id] = i; break;
                case "diode": case "led": { var (Is, N) = DiodeModel(p); i = Is * (Math.Exp(Math.Min(v / (N * Vt), 80)) - 1); break; }
                case "spannung": case "wechsel": i = -x[n + sIdx++]; break; // MNA-Größe ist der Strom in den Pluspol hinein; positiv = Quelle liefert (von − nach + durch die Quelle)
                default: i = 0; break;
            }
            res[p.Id] = (v, i);
        }
        return Last = new SimState(Time, nodeV, res);
    }

    /// <summary>Mehrere Schritte rechnen (z. B. bis sich die Schaltung eingeschwungen hat).</summary>
    public SimState Run(double seconds) { SimState s = Last ?? Step(); var steps = (int)Math.Ceiling(seconds / Dt); for (var k = 0; k < steps; k++) s = Step(); return s; }

    static double V(double[] x, int node) => node > 0 ? x[node - 1] : 0;

    /// <summary>Spannungsbegrenzung für Newton (verhindert Überlauf der e-Funktion).</summary>
    static double Limit(double vnew, double vold, double nvt, double Is)
    {
        var vcrit = nvt * Math.Log(nvt / (Math.Sqrt(2) * Is));
        if (vnew > vcrit && Math.Abs(vnew - vold) > 2 * nvt)
            vnew = vold > 0 ? vold + nvt * Math.Log(1 + (vnew - vold) / nvt) : nvt * Math.Log(vnew / nvt);
        return vnew;
    }

    static double[] Solve(double[,] A, double[] b)
    {
        var n = b.Length; var a = (double[,])A.Clone(); var x = (double[])b.Clone();
        for (var col = 0; col < n; col++)
        {
            var piv = col; for (var r = col + 1; r < n; r++) if (Math.Abs(a[r, col]) > Math.Abs(a[piv, col])) piv = r;
            if (Math.Abs(a[piv, col]) < 1e-18) throw new InvalidOperationException("singulär");
            if (piv != col) { for (var k = 0; k < n; k++) (a[col, k], a[piv, k]) = (a[piv, k], a[col, k]); (x[col], x[piv]) = (x[piv], x[col]); }
            for (var r = col + 1; r < n; r++) { var f = a[r, col] / a[col, col]; if (f == 0) continue; for (var k = col; k < n; k++) a[r, k] -= f * a[col, k]; x[r] -= f * x[col]; }
        }
        for (var r = n - 1; r >= 0; r--) { var s = x[r]; for (var k = r + 1; k < n; k++) s -= a[r, k] * x[k]; x[r] = s / a[r, r]; }
        return x;
    }

    /// <summary>Prüfung nach dem Einschwingen: LED-Strom, Leistung am Widerstand (¼ W), Kurzschluss.</summary>
    public static List<string> Check(Circuit c, SimState s)
    {
        var res = new List<string>();
        foreach (var p in c.Parts)
        {
            if (!s.Parts.TryGetValue(p.Id, out var r)) continue;
            switch (p.Kind)
            {
                case "led" when r.I > 0.03: res.Add($"{p.Name}: {r.I * 1000:0} mA – die LED brennt durch (höchstens etwa 20 mA). Größeren Vorwiderstand nehmen."); break;
                case "led" when r.I < 0.0005 && r.V > 0: res.Add($"{p.Name}: leuchtet kaum ({r.I * 1000:0.0} mA)."); break;
                case "widerstand" when Math.Abs(r.V * r.I) > 0.25: res.Add($"{p.Name}: {Math.Abs(r.V * r.I):0.00} W – mehr als ein normaler ¼-W-Widerstand verträgt."); break;
                case "spannung" when Math.Abs(r.I) > 5: res.Add($"{p.Name}: {Math.Abs(r.I):0.0} A – fast ein Kurzschluss."); break;
            }
        }
        return res;
    }
}
