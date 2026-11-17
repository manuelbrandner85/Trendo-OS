using System.Globalization;
using System.Text;

namespace Trendo.Core.Statik;

/// <summary>Ein Vergleichswert der Gegenprobe: Hauptrechnung gegen unabhängigen Wert, Abweichung in Prozent.</summary>
public sealed record CrossCheckItem(string What, double Main, double Reference, double DeviationPct, string Source)
{
    public bool Ok => DeviationPct <= FrameCrossCheck.TolerancePct;
}

/// <summary>Ergebnis der Gegenprobe. <see cref="Skipped"/> nennt, was nicht unabhängig nachgerechnet werden konnte (ehrlich, statt still zu fehlen).</summary>
public sealed record CrossCheckResult(List<CrossCheckItem> Items, List<string> Methods, string? Skipped)
{
    public bool Ok => Items.Count > 0 && Items.All(i => i.Ok);
    public double MaxDeviationPct => Items.Count == 0 ? 0 : Items.Max(i => i.DeviationPct);
    public CrossCheckItem? Worst => Items.OrderByDescending(i => i.DeviationPct).FirstOrDefault();
    /// <summary>Kurzzeile für die Oberfläche: „Gegenprobe ✓ Abweichung 0,02 %“ oder eine Warnung.</summary>
    public string Text => Items.Count == 0 ? "Gegenprobe nicht möglich" + (Skipped is null ? "" : " – " + Skipped)
        : Ok ? $"Gegenprobe ✓ Abweichung {FrameCrossCheck.Pct(MaxDeviationPct)} %"
        : $"⚠ Gegenprobe weicht ab: {Worst!.What} {FrameCrossCheck.Pct(Worst.DeviationPct)} % (erlaubt {FrameCrossCheck.Pct(FrameCrossCheck.TolerancePct)} %) – Ergebnis nicht verwenden, Eingaben prüfen";
}

/// <summary>T-0748 (SPEC-03): Gegenprobe jeder Tragwerksrechnung gegen unabhängige Werte.
/// 1. Gleichgewicht: Summe der Lagerkräfte und -momente gegen die Summe der Lasten (aus den Eingaben direkt aufsummiert).
/// 2. Formel (wenn das System eine Lehrbuchform hat): Einfeldträger (qL²/8, 5qL⁴/384EI, FL/4, FL³/48EI), Kragarm (qL²/2, qL⁴/8EI, FL, FL³/3EI),
///    Zweifeldträger mit gleichen Feldern und Gleichlast (Stützmoment qL²/8, Lager 3/8·qL und 10/8·qL, Durchbiegung aus der Biegelinie des einseitig eingespannten Trägers).
/// 3. Zweite Rechnung mit anderer Methode: jeder Stab in viele Teilstäbe zerlegt, Lasten als Knotenkräfte verteilt (statt Volleinspann-Lasten),
///    Gelenke als eigener Drehfreiheitsgrad (statt statischer Kondensation), Lösung mit Cholesky-Zerlegung (statt Gauß).
/// Ersetzt keine Prüfstatik.</summary>
public static class FrameCrossCheck
{
    /// <summary>Erlaubte Abweichung in Prozent (Abnahmekriterium im Audit: 0,5 %).</summary>
    public const double TolerancePct = 0.5;
    const double G = 9.81;
    static readonly CultureInfo De = CultureInfo.GetCultureInfo("de-DE");
    public static string Pct(double v) => v < 0.005 ? "< 0,01" : v.ToString(v < 10 ? "0.00" : "0.#", De);

    public static CrossCheckResult Check(FrameModel f)
    {
        var r = FrameSolver.Solve(f, 201); // Hauptrechnung, fein abgetastet (sonst läge ein Größtwert zwischen zwei Punkten)
        var items = new List<CrossCheckItem>(); var methods = new List<string>(); var skipped = new List<string>();
        Equilibrium(f, r, items); methods.Add("Gleichgewicht");
        if (Formula(f, r, items) is { } name) methods.Add("Formel " + name);
        var why = SecondMethodBlocker(f);
        if (why is null)
        {
            try { SecondMethod(f, r, items); methods.Add("zweite Rechnung (Teilstäbe)"); }
            catch (InvalidOperationException e) { skipped.Add("zweite Rechnung: " + e.Message); }
        }
        else skipped.Add("zweite Rechnung nicht für " + why + " – nur Gleichgewicht geprüft");
        return new(items, methods, skipped.Count == 0 ? null : string.Join("; ", skipped));
    }

    static double Dev(double main, double reference, double scale)
    {
        var d = Math.Abs(main - reference); var basis = Math.Max(Math.Abs(reference), scale);
        return basis < 1e-12 ? 0 : d / basis * 100;
    }

    // ---------- 1. Gleichgewicht ----------
    /// <summary>Lasten direkt aus den Eingaben: (x, y, Fx, Fy, M) je Kraft, Fy nach oben positiv.</summary>
    static List<(double X, double Y, double Fx, double Fy, double M)> Loads(FrameModel f)
    {
        var res = new List<(double, double, double, double, double)>();
        foreach (var l in f.Loads)
        {
            if (l.Kind == "knoten" && f.Node(l.Target) is { } n) res.Add((n.X, n.Y, l.Fx, -l.Value, l.M));
            if (l.Kind is "strecke" or "einzel" && f.Member(l.Target) is { } m)
            {
                var a = f.Node(m.A)!; var b = f.Node(m.B)!; var L = Len(a, b);
                if (l.Kind == "einzel") { res.Add((a.X + (b.X - a.X) * l.At, a.Y + (b.Y - a.Y) * l.At, 0, -l.Value, 0)); continue; }
                double qa = l.Value, qb = l.Value2 ?? l.Value; var total = (qa + qb) / 2 * L; if (Math.Abs(total) < 1e-15) continue;
                var t = Math.Abs(qa + qb) < 1e-15 ? 0.5 : (qa + 2 * qb) / (3 * (qa + qb)); // Schwerpunkt der Trapezfläche
                res.Add((a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t, 0, -total, 0));
            }
        }
        if (f.SelfWeight)
            foreach (var m in f.Members)
            {
                var a = f.Node(m.A)!; var b = f.Node(m.B)!; var w = Engineering.Mat(m.Material).Density * Sections.Get(m.Section).A_mm2 * 1e-6 * G / 1000 * f.SelfWeightFactor * Len(a, b);
                res.Add(((a.X + b.X) / 2, (a.Y + b.Y) / 2, 0, -w, 0));
            }
        return res;
    }

    static void Equilibrium(FrameModel f, FrameResult r, List<CrossCheckItem> items)
    {
        var loads = Loads(f);
        double lx = loads.Sum(l => l.Fx), ly = loads.Sum(l => l.Fy), lm = loads.Sum(l => l.X * l.Fy - l.Y * l.Fx + l.M);
        double rx = 0, ry = 0, rm = 0;
        foreach (var (id, (x, y, m)) in r.Reactions) { var n = f.Node(id)!; rx += x; ry += y; rm += n.X * y - n.Y * x + m; }
        var scaleF = Math.Max(1e-6, loads.Sum(l => Math.Abs(l.Fx) + Math.Abs(l.Fy)));
        var size = f.Bounds() is { } bb ? Math.Max(1, Math.Max(bb.MaxX - bb.MinX, bb.MaxY - bb.MinY)) : 1;
        var scaleM = Math.Max(1e-6, loads.Sum(l => Math.Abs(l.X * l.Fy - l.Y * l.Fx) + Math.Abs(l.M)) + scaleF * size * 0.01);
        items.Add(new("Summe senkrecht", ry, -ly, Dev(ry, -ly, scaleF), "Gleichgewicht ΣV = 0"));
        items.Add(new("Summe waagerecht", rx, -lx, Dev(rx, -lx, scaleF), "Gleichgewicht ΣH = 0"));
        items.Add(new("Summe Momente", rm, -lm, Dev(rm, -lm, scaleM), "Gleichgewicht ΣM = 0"));
    }

    // ---------- 2. Formeln für Lehrbuchsysteme ----------
    static string? Formula(FrameModel f, FrameResult r, List<CrossCheckItem> items)
    {
        if (f.Nodes.Count < 2 || f.Loads.Any(l => l.Kind == "temperatur" || l.Kind == "knoten" && (Math.Abs(l.Fx) > 1e-12 || Math.Abs(l.M) > 1e-12))) return null;
        if (f.Nodes.Any(n => Math.Abs(n.Y - f.Nodes[0].Y) > 1e-9 || Math.Abs(n.Settle) > 1e-12 || Math.Abs(n.Angle) > 1e-9)) return null;
        if (f.Members.Any(m => m.HingeA || m.HingeB) || f.Members.Select(m => (m.Section, Engineering.Mat(m.Material).Name)).Distinct().Count() != 1) return null;
        var xs = f.Nodes.Select(n => n.X).OrderBy(x => x).ToList(); var x0 = xs[0]; var L = xs[^1] - x0;
        // durchgehender Träger: Stäbe decken [x0, x0+L] lückenlos ab, ohne Überlappung
        if (Math.Abs(f.Members.Sum(m => Len(f.Node(m.A)!, f.Node(m.B)!)) - L) > 1e-6) return null;
        var mem = f.Members[0]; var sec = Sections.Get(mem.Section); var mat = Engineering.Mat(mem.Material);
        var EI = mat.E_GPa * 1e6 * sec.I_mm4 * 1e-12;
        // Gleichlast: auf allen Stäben gleich, ohne Trapez
        double q = f.SelfWeight ? mat.Density * sec.A_mm2 * 1e-6 * G / 1000 * f.SelfWeightFactor : 0;
        var line = f.Loads.Where(l => l.Kind == "strecke").ToList();
        if (line.Any(l => l.Value2 is { } v2 && Math.Abs(v2 - l.Value) > 1e-12)) return null;
        var perMember = f.Members.Select(m => line.Where(l => l.Target == m.Id).Sum(l => l.Value)).ToList();
        if (perMember.Any(v => Math.Abs(v - perMember[0]) > 1e-12)) return null;
        q += perMember[0];
        // Einzelkräfte als Lage auf dem Träger
        var points = new List<(double X, double F)>();
        foreach (var l in f.Loads.Where(l => l.Kind is "einzel" or "knoten"))
        {
            if (l.Kind == "knoten") points.Add((f.Node(l.Target)!.X - x0, l.Value));
            else { var m = f.Member(l.Target)!; var a = f.Node(m.A)!; var b = f.Node(m.B)!; points.Add((a.X + (b.X - a.X) * l.At - x0, l.Value)); }
        }
        var sup = f.Nodes.Where(n => n.Support != "").OrderBy(n => n.X).ToList();
        double mainM = r.Members.Max(x => Math.Abs(x.MaxM)), mainW = MaxDisp(r);
        double Ry(FNode n) => r.Reactions[n.Id].Ry;

        // Einfeldträger: Fest- und Loslager an den Enden
        if (sup.Count == 2 && sup[0].X - x0 < 1e-9 && Math.Abs(sup[1].X - x0 - L) < 1e-9 && sup.All(n => n.Support is "fest" or "los") && sup.Any(n => n.Support == "fest")
            && points.All(p => Math.Abs(p.X - L / 2) < 1e-9))
        {
            var F = points.Sum(p => p.F);
            var M = q * L * L / 8 + F * L / 4; var w = 5 * q * Math.Pow(L, 4) / (384 * EI) + F * L * L * L / (48 * EI); var R = q * L / 2 + F / 2;
            items.Add(new("Feldmoment", mainM, M, Dev(mainM, M, 1e-6), "Tabelle: M = qL²/8 + FL/4"));
            items.Add(new("Durchbiegung", mainW * 1000, w * 1000, Dev(mainW, w, 1e-9), "Tabelle: w = 5qL⁴/384EI + FL³/48EI"));
            items.Add(new("Lager links", Ry(sup[0]), R, Dev(Ry(sup[0]), R, 1e-6), "Tabelle: A = qL/2 + F/2"));
            return "Einfeldträger";
        }
        // Kragarm: Einspannung an einem Ende, sonst kein Lager; Einzelkraft nur am freien Ende
        if (sup.Count == 1 && sup[0].Support == "einspannung" && (sup[0].X - x0 < 1e-9 || Math.Abs(sup[0].X - x0 - L) < 1e-9))
        {
            var freeEnd = sup[0].X - x0 < 1e-9 ? L : 0;
            if (!points.All(p => Math.Abs(p.X - freeEnd) < 1e-9)) return null;
            var F = points.Sum(p => p.F);
            var M = q * L * L / 2 + F * L; var w = q * Math.Pow(L, 4) / (8 * EI) + F * L * L * L / (3 * EI); var R = q * L + F;
            items.Add(new("Einspannmoment", mainM, M, Dev(mainM, M, 1e-6), "Tabelle: M = qL²/2 + FL"));
            items.Add(new("Durchbiegung Spitze", mainW * 1000, w * 1000, Dev(mainW, w, 1e-9), "Tabelle: w = qL⁴/8EI + FL³/3EI"));
            items.Add(new("Lagerkraft", Ry(sup[0]), R, Dev(Ry(sup[0]), R, 1e-6), "Tabelle: A = qL + F"));
            return "Kragarm";
        }
        // Zweifeldträger, gleiche Felder, nur Gleichlast
        if (sup.Count == 3 && points.Count == 0 && q > 0 && sup[0].X - x0 < 1e-9 && Math.Abs(sup[2].X - x0 - L) < 1e-9 && Math.Abs(sup[1].X - x0 - L / 2) < 1e-9
            && sup.All(n => n.Support is "fest" or "los") && sup.Count(n => n.Support == "fest") == 1)
        {
            var l = L / 2; var Mb = q * l * l / 8; var Ra = 3 * q * l / 8; var Rb = 10 * q * l / 8;
            var t = (1 + Math.Sqrt(33)) / 16; var w = t * (1 - 3 * t * t + 2 * t * t * t) / 48 * q * Math.Pow(l, 4) / EI; // Nullstelle von w' bei 8t³ − 9t² + 1 = 0
            items.Add(new("Stützmoment", mainM, Mb, Dev(mainM, Mb, 1e-6), "Tabelle: M_B = qL²/8"));
            items.Add(new("Lager außen", Ry(sup[0]), Ra, Dev(Ry(sup[0]), Ra, 1e-6), "Tabelle: A = 3/8·qL"));
            items.Add(new("Lager Mitte", Ry(sup[1]), Rb, Dev(Ry(sup[1]), Rb, 1e-6), "Tabelle: B = 10/8·qL"));
            items.Add(new("Durchbiegung", mainW * 1000, w * 1000, Dev(mainW, w, 1e-9), "Biegelinie: w = qL⁴/185EI"));
            return "Zweifeldträger";
        }
        return null;
    }

    static double MaxDisp(FrameResult r) =>
        r.Members.Max(m => Enumerable.Range(0, m.Dx.Length).Max(i => Math.Sqrt(m.Dx[i] * m.Dx[i] + m.Dy[i] * m.Dy[i])));
    static double Len(FNode a, FNode b) => Math.Sqrt((b.X - a.X) * (b.X - a.X) + (b.Y - a.Y) * (b.Y - a.Y));

    // ---------- 3. Zweite Rechnung: Teilstäbe ----------
    static string? SecondMethodBlocker(FrameModel f)
    {
        if (f.Loads.Any(l => l.Kind == "temperatur")) return "Temperaturlasten";
        if (f.Nodes.Any(n => Math.Abs(n.Settle) > 1e-12)) return "Lagersetzungen";
        if (f.Nodes.Any(n => n.Support == "los" && Math.Abs(n.Angle) > 1e-9)) return "schräge Loslager";
        return null;
    }

    /// <summary>Unabhängige Rechnung: Teilstäbe, Knotenkräfte statt Volleinspann-Lasten, Gelenk = eigener Drehwinkel, Cholesky.</summary>
    public static (Dictionary<string, (double Rx, double Ry, double M)> Reactions, Dictionary<string, double> MaxMoment, double MaxDisp) Solve(FrameModel f)
    {
        var parts = Math.Clamp(240 / Math.Max(1, f.Members.Count), 6, 40);
        var dofOfNode = new Dictionary<string, int>(); var dofs = 0;
        foreach (var n in f.Nodes) { dofOfNode[n.Id] = dofs; dofs += 3; }
        var sub = new List<(string Member, int[] Map, double L, double C, double S, double EA, double EI)>();
        var forces = new List<(int Dof, double V)>();
        foreach (var m in f.Members)
        {
            var a = f.Node(m.A)!; var b = f.Node(m.B)!; var L = Len(a, b); var c = (b.X - a.X) / L; var s = (b.Y - a.Y) / L;
            var sec = Sections.Get(m.Section); var mat = Engineering.Mat(m.Material); var E = mat.E_GPa * 1e6;
            double EA = E * sec.A_mm2 * 1e-6, EI = E * sec.I_mm4 * 1e-12;
            // Teilungspunkte: gleichmäßig plus jede Einzellast
            var ts = Enumerable.Range(0, parts + 1).Select(i => (double)i / parts).ToList();
            foreach (var l in f.Loads.Where(l => l.Kind == "einzel" && l.Target == m.Id)) ts.Add(Math.Clamp(l.At, 0, 1));
            ts = ts.OrderBy(t => t).Aggregate(new List<double>(), (acc, t) => { if (acc.Count == 0 || t - acc[^1] > 1e-9) acc.Add(t); return acc; });
            // Freiheitsgrade der Teilungspunkte: Enden = Knoten des Tragwerks (Gelenk: eigener Drehwinkel), innen neu
            var pts = new int[ts.Count][];
            for (var i = 0; i < ts.Count; i++)
            {
                if (i == 0 || i == ts.Count - 1)
                {
                    var k = dofOfNode[i == 0 ? m.A : m.B]; var hinge = i == 0 ? m.HingeA : m.HingeB;
                    pts[i] = [k, k + 1, hinge ? dofs++ : k + 2];
                }
                else { pts[i] = [dofs, dofs + 1, dofs + 2]; dofs += 3; }
            }
            var own = f.SelfWeight ? mat.Density * sec.A_mm2 * 1e-6 * G / 1000 * f.SelfWeightFactor : 0;
            var lines = f.Loads.Where(l => l.Kind == "strecke" && l.Target == m.Id).ToList();
            double Q(double t) => own + lines.Sum(l => l.Value + ((l.Value2 ?? l.Value) - l.Value) * t); // kN/m nach unten bei t
            for (var i = 1; i < ts.Count; i++)
            {
                var h = (ts[i] - ts[i - 1]) * L; double qa = Q(ts[i - 1]), qb = Q(ts[i]);
                sub.Add((m.Id, [.. pts[i - 1], .. pts[i]], h, c, s, EA, EI));
                // Streckenlast als zwei Knotenkräfte (wie ein Einfeldträger je Teilstab), global nach unten
                forces.Add((pts[i - 1][1], -h * (2 * qa + qb) / 6)); forces.Add((pts[i][1], -h * (qa + 2 * qb) / 6));
            }
            foreach (var l in f.Loads.Where(l => l.Kind == "einzel" && l.Target == m.Id))
            { var i = ts.FindIndex(t => Math.Abs(t - Math.Clamp(l.At, 0, 1)) < 1e-9); forces.Add((pts[i][1], -l.Value)); }
        }
        foreach (var l in f.Loads.Where(l => l.Kind == "knoten")) { var k = dofOfNode[l.Target]; forces.Add((k, l.Fx)); forces.Add((k + 1, -l.Value)); forces.Add((k + 2, l.M)); }

        var K = new double[dofs, dofs]; var F = new double[dofs];
        foreach (var (d, v) in forces) F[d] += v;
        foreach (var e in sub)
        {
            var kg = GlobalStiffness(e.EA, e.EI, e.L, e.C, e.S);
            for (var i = 0; i < 6; i++) for (var j = 0; j < 6; j++) K[e.Map[i], e.Map[j]] += kg[i, j];
        }
        var fixedDof = new bool[dofs];
        foreach (var n in f.Nodes)
        {
            var k = dofOfNode[n.Id];
            if (n.Support is "fest" or "einspannung") fixedDof[k] = true;
            if (n.Support is "fest" or "los" or "einspannung") fixedDof[k + 1] = true;
            if (n.Support == "einspannung") fixedDof[k + 2] = true;
        }
        // Drehwinkel ohne Steifigkeit (Knoten, an dem alle Stäbe gelenkig hängen) haben keinen Einfluss: festhalten
        for (var i = 0; i < dofs; i++) if (!fixedDof[i] && Math.Abs(K[i, i]) < 1e-12) fixedDof[i] = true;
        var free = Enumerable.Range(0, dofs).Where(i => !fixedDof[i]).ToArray();
        var Kff = new double[free.Length, free.Length]; var Ff = new double[free.Length];
        for (var i = 0; i < free.Length; i++) { Ff[i] = F[free[i]]; for (var j = 0; j < free.Length; j++) Kff[i, j] = K[free[i], free[j]]; }
        var uf = Cholesky(Kff, Ff);
        var U = new double[dofs]; for (var i = 0; i < free.Length; i++) U[free[i]] = uf[i];

        var reactions = new Dictionary<string, (double, double, double)>();
        foreach (var n in f.Nodes.Where(n => n.Support != ""))
        {
            var k = dofOfNode[n.Id]; var rr = new double[3];
            for (var c = 0; c < 3; c++) { if (!fixedDof[k + c]) continue; double sum = 0; for (var j = 0; j < dofs; j++) sum += K[k + c, j] * U[j]; rr[c] = sum - F[k + c]; }
            reactions[n.Id] = (rr[0], rr[1], rr[2]);
        }
        var maxM = new Dictionary<string, double>();
        foreach (var e in sub)
        {
            var d = e.Map.Select(i => U[i]).ToArray();
            // Endkräfte am Teilstab im örtlichen System: Drehung der Verschiebungen, dann k_örtlich · d
            double[] dl = [e.C * d[0] + e.S * d[1], -e.S * d[0] + e.C * d[1], d[2], e.C * d[3] + e.S * d[4], -e.S * d[3] + e.C * d[4], d[5]];
            double b12 = 12 * e.EI / (e.L * e.L * e.L), b6 = 6 * e.EI / (e.L * e.L), b4 = 4 * e.EI / e.L, b2 = 2 * e.EI / e.L;
            var mA = b6 * dl[1] + b4 * dl[2] - b6 * dl[4] + b2 * dl[5];
            var mB = b6 * dl[1] + b2 * dl[2] - b6 * dl[4] + b4 * dl[5];
            maxM[e.Member] = Math.Max(maxM.GetValueOrDefault(e.Member), Math.Max(Math.Abs(mA), Math.Abs(mB)));
        }
        double disp = 0; // größte Verschiebung über alle Teilungspunkte
        foreach (var e in sub) { disp = Math.Max(disp, Math.Sqrt(U[e.Map[0]] * U[e.Map[0]] + U[e.Map[1]] * U[e.Map[1]])); disp = Math.Max(disp, Math.Sqrt(U[e.Map[3]] * U[e.Map[3]] + U[e.Map[4]] * U[e.Map[4]])); }
        return (reactions, maxM, disp);
    }

    static void SecondMethod(FrameModel f, FrameResult r, List<CrossCheckItem> items)
    {
        var (reac, maxM, disp) = Solve(f);
        var scaleR = Math.Max(1e-6, r.Reactions.Values.Max(x => Math.Max(Math.Abs(x.Rx), Math.Abs(x.Ry))) * 0.01);
        var scaleMr = Math.Max(1e-6, r.Reactions.Values.Max(x => Math.Abs(x.M)) * 0.01);
        foreach (var (id, (rx, ry, rm)) in r.Reactions)
        {
            var k = $"K{f.Nodes.FindIndex(n => n.Id == id) + 1}"; var o = reac[id];
            items.Add(new($"Lager {k} senkrecht", ry, o.Ry, Dev(ry, o.Ry, scaleR), "zweite Rechnung"));
            if (f.Node(id)!.Support != "los") items.Add(new($"Lager {k} waagerecht", rx, o.Rx, Dev(rx, o.Rx, scaleR), "zweite Rechnung"));
            if (f.Node(id)!.Support == "einspannung") items.Add(new($"Lager {k} Moment", rm, o.M, Dev(rm, o.M, scaleMr), "zweite Rechnung"));
        }
        var scaleM = Math.Max(1e-6, r.Members.Max(x => Math.Abs(x.MaxM)) * 0.01);
        for (var i = 0; i < r.Members.Count; i++)
        {
            var mm = Math.Abs(r.Members[i].MaxM); var o = maxM.GetValueOrDefault(r.Members[i].Id);
            items.Add(new($"größtes Moment S{i + 1}", mm, o, Dev(mm, o, scaleM), "zweite Rechnung"));
        }
        var main = MaxDisp(r);
        items.Add(new("größte Verschiebung", main * 1000, disp * 1000, Dev(main, disp, 1e-9), "zweite Rechnung"));
    }

    static double[,] GlobalStiffness(double EA, double EI, double L, double c, double s)
    {
        double a = EA / L, b = 12 * EI / (L * L * L), d = 6 * EI / (L * L), e = 4 * EI / L, g = 2 * EI / L;
        double[,] k =
        {
            { a, 0, 0, -a, 0, 0 }, { 0, b, d, 0, -b, d }, { 0, d, e, 0, -d, g },
            { -a, 0, 0, a, 0, 0 }, { 0, -b, -d, 0, b, -d }, { 0, d, g, 0, -d, e },
        };
        // K = Tᵀ k T, ausgeschrieben je Block
        double[,] T = { { c, s, 0 }, { -s, c, 0 }, { 0, 0, 1 } };
        var r = new double[6, 6];
        for (var bi = 0; bi < 2; bi++) for (var bj = 0; bj < 2; bj++)
            for (var i = 0; i < 3; i++) for (var j = 0; j < 3; j++)
            {
                double sum = 0;
                for (var p = 0; p < 3; p++) for (var q = 0; q < 3; q++) sum += T[p, i] * k[3 * bi + p, 3 * bj + q] * T[q, j];
                r[3 * bi + i, 3 * bj + j] = sum;
            }
        return r;
    }

    /// <summary>Cholesky-Zerlegung K = L·Lᵀ (K symmetrisch positiv definit, sonst ist das System beweglich).</summary>
    static double[] Cholesky(double[,] A, double[] b)
    {
        var n = b.Length; var L = new double[n, n];
        var scale = 0.0; for (var i = 0; i < n; i++) scale = Math.Max(scale, Math.Abs(A[i, i]));
        for (var j = 0; j < n; j++)
        {
            double sum = A[j, j]; for (var k = 0; k < j; k++) sum -= L[j, k] * L[j, k];
            if (sum <= scale * 1e-12) throw new InvalidOperationException("das System ist beweglich");
            L[j, j] = Math.Sqrt(sum);
            for (var i = j + 1; i < n; i++) { double s = A[i, j]; for (var k = 0; k < j; k++) s -= L[i, k] * L[j, k]; L[i, j] = s / L[j, j]; }
        }
        var y = new double[n];
        for (var i = 0; i < n; i++) { double s = b[i]; for (var k = 0; k < i; k++) s -= L[i, k] * y[k]; y[i] = s / L[i, i]; }
        var x = new double[n];
        for (var i = n - 1; i >= 0; i--) { double s = y[i]; for (var k = i + 1; k < n; k++) s -= L[k, i] * x[k]; x[i] = s / L[i, i]; }
        return x;
    }

    /// <summary>Gegenprobe als Bericht (Markdown) – jede Zeile mit Hauptrechnung, Vergleichswert, Quelle und Abweichung.</summary>
    public static string Report(FrameModel f, CrossCheckResult c)
    {
        var sb = new StringBuilder($"# Gegenprobe – {f.Name}\n\n**{c.Text}**\n\nVerfahren: {string.Join(", ", c.Methods)}. Erlaubte Abweichung: {Pct(TolerancePct)} %.\n\n");
        if (c.Skipped is not null) sb.Append($"Nicht geprüft: {c.Skipped}\n\n");
        sb.Append("| Größe | Hauptrechnung | Vergleich | Quelle | Abweichung |\n|---|---:|---:|---|---:|\n");
        foreach (var i in c.Items) sb.Append($"| {i.What} | {(i.Main + 0.0).ToString("0.####", De)} | {(i.Reference + 0.0).ToString("0.####", De)} | {i.Source} | {(i.Ok ? "" : "⚠ ")}{Pct(i.DeviationPct)} % |\n");
        sb.Append("\nKräfte in kN, Momente in kNm, Verschiebungen in mm. Die Gegenprobe prüft die Rechnung, nicht die Eingaben. Vorbemessung – ersetzt keine Prüfstatik durch eine Fachperson.\n");
        return sb.ToString();
    }
}
