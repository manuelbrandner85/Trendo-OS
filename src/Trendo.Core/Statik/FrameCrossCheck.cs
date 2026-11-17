using System.Globalization;
using System.Text;

namespace Trendo.Core.Statik;

/// <summary>Ein Vergleichswert der Gegenprobe: Hauptrechnung gegen unabhängigen Wert, Abweichung in Prozent.</summary>
public sealed record CrossCheckItem(string What, double Main, double Reference, double DeviationPct, string Source)
{
    public bool Ok => DeviationPct <= FrameCrossCheck.TolerancePct;
}

/// <summary>Ergebnis der Gegenprobe. <see cref="Skipped"/> nennt, was nicht unabhängig nachgerechnet werden konnte (ehrlich, statt still zu fehlen).
/// <see cref="TooLarge"/>: zweite Rechnung wegen der Größe ausgelassen. <see cref="Pending"/>: Gegenprobe läuft noch im Hintergrund.</summary>
public sealed record CrossCheckResult(List<CrossCheckItem> Items, List<string> Methods, string? Skipped, bool TooLarge = false, bool Pending = false)
{
    /// <summary>Platzhalter, solange die Gegenprobe im Hintergrund rechnet.</summary>
    public static CrossCheckResult Running => new([], [], null, Pending: true);
    public bool Ok => !Pending && Items.Count > 0 && Items.All(i => i.Ok);
    public double MaxDeviationPct => Items.Count == 0 ? 0 : Items.Max(i => i.DeviationPct);
    public CrossCheckItem? Worst => Items.OrderByDescending(i => i.DeviationPct).FirstOrDefault();
    /// <summary>Nur das Gleichgewicht ist geprüft (keine Formel, keine zweite Rechnung) – dann kein „✓ Abweichung“, sondern ehrlich „nur Gleichgewicht geprüft“.</summary>
    public bool OnlyEquilibrium => Methods.Count > 0 && Methods.All(m => m == "Gleichgewicht");
    /// <summary>Kurzzeile für die Oberfläche: „Gegenprobe ✓ Abweichung 0,02 %“, „nur Gleichgewicht geprüft“, „läuft …“ oder eine Warnung.</summary>
    public string Text => Pending ? "Gegenprobe läuft …"
        : Items.Count == 0 ? "Gegenprobe nicht möglich" + (Skipped is null ? "" : " – " + Skipped)
        : !Ok ? $"⚠ Gegenprobe weicht ab: {Worst!.What} {FrameCrossCheck.Pct(Worst.DeviationPct)} % (erlaubt {FrameCrossCheck.Pct(FrameCrossCheck.TolerancePct)} %) – Ergebnis nicht verwenden, Eingaben prüfen"
        : TooLarge && OnlyEquilibrium ? "Gegenprobe zu groß – nur Gleichgewicht geprüft"
        : OnlyEquilibrium ? "Gegenprobe: nur Gleichgewicht geprüft" + (Reason is { } why ? $" ({why})" : "")
        : $"Gegenprobe ✓ Abweichung {FrameCrossCheck.Pct(MaxDeviationPct)} %" + (TooLarge ? " – zweite Rechnung zu groß" : "");
    string? Reason => Skipped?.Replace(" – nur Gleichgewicht geprüft", "");
}

/// <summary>T-0748 (SPEC-03): Gegenprobe jeder Tragwerksrechnung gegen unabhängige Werte.
/// 1. Gleichgewicht: Summe der Lagerkräfte und -momente gegen die Summe der Lasten (aus den Eingaben direkt aufsummiert).
/// 2. Formel (wenn das System eine Lehrbuchform hat): Einfeldträger (qL²/8, 5qL⁴/384EI, FL/4, FL³/48EI), Kragarm (qL²/2, qL⁴/8EI, FL, FL³/3EI),
///    Zweifeldträger mit gleichen Feldern und Gleichlast (Stützmoment qL²/8, Lager 3/8·qL und 10/8·qL, Durchbiegung aus der Biegelinie des einseitig eingespannten Trägers).
/// 3. Zweite Rechnung mit anderer Methode (eigener Code): jeder Stab in feste Teilstäbe zerlegt, Streckenlast je Teilstab als konsistenter Lastvektor,
///    Gelenke als eigener Drehfreiheitsgrad (statt statischer Kondensation), Nummerierung nach Reverse Cuthill-McKee, Cholesky im Hüllenspeicher (statt dichter Matrix und Gauß).
///    Zu große Tragwerke (Speicher über <see cref="MaxEnvelope"/>) werden ehrlich als „zu groß – nur Gleichgewicht geprüft“ gemeldet.
/// Ersetzt keine Prüfstatik.</summary>
public static class FrameCrossCheck
{
    /// <summary>Erlaubte Abweichung in Prozent (Abnahmekriterium im Audit: 0,5 %).</summary>
    public const double TolerancePct = 0.5;
    const double G = 9.81;
    static readonly CultureInfo De = CultureInfo.GetCultureInfo("de-DE");
    public static string Pct(double v) => v < 0.005 ? "< 0,01" : v.ToString(v < 10 ? "0.00" : "0.#", De);

    public static CrossCheckResult Check(FrameModel f, CancellationToken ct = default, long maxEnvelope = MaxEnvelope)
    {
        var r = FrameSolver.Solve(f); // T-0748-fix2: genau die Rechnung, die Oberfläche und Exporte zeigen (gleiche Messpunkte inkl. Lastorten und Extremstellen)
        var items = new List<CrossCheckItem>(); var methods = new List<string>(); var skipped = new List<string>();
        Equilibrium(f, r, items); methods.Add("Gleichgewicht");
        if (Formula(f, r, items) is { } name) methods.Add("Formel " + name);
        var why = SecondMethodBlocker(f); var tooLarge = false;
        ct.ThrowIfCancellationRequested();
        if (why is null)
        {
            try { SecondMethod(f, r, items, ct, maxEnvelope); methods.Add("zweite Rechnung (Teilstäbe)"); }
            catch (TooLargeException e) { tooLarge = true; skipped.Add("zweite Rechnung: " + e.Message + " – nur Gleichgewicht geprüft"); }
            catch (InvalidOperationException e) { skipped.Add("zweite Rechnung: " + e.Message); }
        }
        else skipped.Add("zweite Rechnung nicht für " + why + " – nur Gleichgewicht geprüft");
        return new(items, methods, skipped.Count == 0 ? null : string.Join("; ", skipped), tooLarge);
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

    /// <summary>Feste Teilung jedes Stabs in der zweiten Rechnung (dazu ein Schnitt an jeder Einzellast). Mit konsistenten Lastvektoren sind die Knotenwerte
    /// eines Euler-Bernoulli-Balkens exakt – die Teilung dient der Abtastung von Moment und Verschiebung, nicht der Genauigkeit an den Knoten.</summary>
    public const int Parts = 8;
    /// <summary>Obergrenze des Hüllenspeichers (Einträge der zerlegten Steifigkeitsmatrix, je 8 Byte). Darüber wäre die Gegenprobe zu groß – dann nur Gleichgewicht.</summary>
    public const long MaxEnvelope = 25_000_000;

    /// <summary>Das Tragwerk ist für die zweite Rechnung zu groß (Speicher über <see cref="MaxEnvelope"/>).</summary>
    public sealed class TooLargeException(string message) : InvalidOperationException(message);

    sealed class Sub
    {
        public required string Member; public required int[] Map; public double L, C, S, EA, EI;
        public double Pa, Pb, Xa, Xb; // Last am Teilstab im örtlichen System: quer (Pa → Pb) und längs (Xa → Xb), kN/m
        public required double[,] Kg; public required double[] Fl;
    }

    /// <summary>Unabhängige Rechnung: jeder Stab in <see cref="Parts"/> Teilstäbe, Streckenlast je Teilstab als konsistenter Lastvektor
    /// (Volleinspann-Kräfte und -Momente), Gelenk = eigener Drehwinkel (statt statischer Kondensation), Knoten nach Reverse Cuthill-McKee nummeriert,
    /// Lösung mit Cholesky im Hüllenspeicher (statt dichter Matrix und Gauß), Lagerkräfte aus den Teilstab-Endkräften.
    /// Moment und Verschiebung im Teilstab aus der geschlossenen Lösung der Balkengleichung. <paramref name="ct"/> bricht ab (neue Änderung).</summary>
    public static (Dictionary<string, (double Rx, double Ry, double M)> Reactions, Dictionary<string, double> MaxMoment, double MaxDisp) Solve(FrameModel f, CancellationToken ct = default, long maxEnvelope = MaxEnvelope)
    {
        var dofOfNode = new Dictionary<string, int>(); var dofs = 0;
        foreach (var n in f.Nodes) { dofOfNode[n.Id] = dofs; dofs += 3; }
        var subs = new List<Sub>(); var nodal = new List<(int Dof, double V)>();
        foreach (var m in f.Members)
        {
            ct.ThrowIfCancellationRequested();
            var a = f.Node(m.A)!; var b = f.Node(m.B)!; var L = Len(a, b); var c = (b.X - a.X) / L; var s = (b.Y - a.Y) / L;
            var sec = Sections.Get(m.Section); var mat = Engineering.Mat(m.Material); var E = mat.E_GPa * 1e6;
            double EA = E * sec.A_mm2 * 1e-6, EI = E * sec.I_mm4 * 1e-12;
            // Teilungspunkte: gleichmäßig plus jede Einzellast
            var ts = Enumerable.Range(0, Parts + 1).Select(i => (double)i / Parts).ToList();
            var points = f.Loads.Where(l => l.Kind == "einzel" && l.Target == m.Id).ToList();
            foreach (var l in points) ts.Add(Math.Clamp(l.At, 0, 1));
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
                // global senkrecht nach unten → örtlich: längs −q·s, quer −q·c
                double pa = -qa * c, pb = -qb * c, xa = -qa * s, xb = -qb * s;
                // konsistenter Lastvektor (Trapez): Längskraft linear, Querkraft und Einspannmoment mit Hermite-Formfunktionen
                double[] fl = [h * (2 * xa + xb) / 6, h * (7 * pa + 3 * pb) / 20, h * h * (3 * pa + 2 * pb) / 60,
                               h * (xa + 2 * xb) / 6, h * (3 * pa + 7 * pb) / 20, -h * h * (2 * pa + 3 * pb) / 60];
                subs.Add(new Sub { Member = m.Id, Map = [.. pts[i - 1], .. pts[i]], L = h, C = c, S = s, EA = EA, EI = EI, Pa = pa, Pb = pb, Xa = xa, Xb = xb, Kg = GlobalStiffness(EA, EI, h, c, s), Fl = fl });
            }
            foreach (var l in points)
            { var i = ts.FindIndex(t => Math.Abs(t - Math.Clamp(l.At, 0, 1)) < 1e-9); nodal.Add((pts[i][1], -l.Value)); }
        }
        foreach (var l in f.Loads.Where(l => l.Kind == "knoten" && dofOfNode.ContainsKey(l.Target)))
        { var k = dofOfNode[l.Target]; nodal.Add((k, l.Fx)); nodal.Add((k + 1, -l.Value)); nodal.Add((k + 2, l.M)); }

        // Lastvektor und Hauptdiagonale aus den Teilstäben (keine Gesamtmatrix)
        var F = new double[dofs]; var diag = new double[dofs];
        foreach (var (d, v) in nodal) F[d] += v;
        foreach (var e in subs)
        {
            var fg = ToGlobal(e.Fl, e.C, e.S);
            for (var i = 0; i < 6; i++) { F[e.Map[i]] += fg[i]; diag[e.Map[i]] += e.Kg[i, i]; }
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
        for (var i = 0; i < dofs; i++) if (!fixedDof[i] && Math.Abs(diag[i]) < 1e-12) fixedDof[i] = true;

        var uf = SolveEnvelope(subs, fixedDof, F, diag, ct, maxEnvelope, out var newIndex);
        var U = new double[dofs]; for (var i = 0; i < dofs; i++) if (newIndex[i] >= 0) U[i] = uf[newIndex[i]];

        // Lagerkräfte R = Σ (k·d − f) über die Teilstäbe − Knotenlasten
        var R = new double[dofs];
        foreach (var e in subs)
        {
            var fg = ToGlobal(e.Fl, e.C, e.S);
            for (var i = 0; i < 6; i++) { double sum = -fg[i]; for (var j = 0; j < 6; j++) sum += e.Kg[i, j] * U[e.Map[j]]; R[e.Map[i]] += sum; }
        }
        foreach (var (d, v) in nodal) R[d] -= v;
        var reactions = new Dictionary<string, (double, double, double)>();
        foreach (var n in f.Nodes.Where(n => n.Support != ""))
        {
            var k = dofOfNode[n.Id];
            reactions[n.Id] = (fixedDof[k] ? R[k] : 0, fixedDof[k + 1] ? R[k + 1] : 0, fixedDof[k + 2] ? R[k + 2] : 0);
        }

        // Moment und Verschiebung im Teilstab aus der geschlossenen Lösung (Biegelinie = Hermite-Anteil + Lösung der eingespannten Teilstab-Last)
        var maxM = new Dictionary<string, double>(); double disp = 0;
        foreach (var e in subs)
        {
            var d = e.Map.Select(i => U[i]).ToArray();
            double[] dl = [e.C * d[0] + e.S * d[1], -e.S * d[0] + e.C * d[1], d[2], e.C * d[3] + e.S * d[4], -e.S * d[3] + e.C * d[4], d[5]];
            double h = e.L, b12 = 12 * e.EI / (h * h * h), b6 = 6 * e.EI / (h * h), b4 = 4 * e.EI / h, b2 = 2 * e.EI / h;
            var vA = b12 * dl[1] + b6 * dl[2] - b12 * dl[4] + b6 * dl[5] - e.Fl[1]; // Endkräfte am Teilstab (örtlich) = k·d − f
            var mA = b6 * dl[1] + b4 * dl[2] - b6 * dl[4] + b2 * dl[5] - e.Fl[2];
            double dp = e.Pb - e.Pa;
            double Mx(double x) => -mA + vA * x + e.Pa * x * x / 2 + dp * x * x * x / (6 * h); // Biegemoment im Schnitt x
            var best = Math.Max(Math.Abs(Mx(0)), Math.Abs(Mx(h)));
            // Extremwerte: dM/dx = vA + Pa·x + dp·x²/(2h) = 0
            double qa2 = dp / (2 * h), qa1 = e.Pa, qa0 = vA;
            if (Math.Abs(qa2) < 1e-15) { if (Math.Abs(qa1) > 1e-15) { var x = -qa0 / qa1; if (x > 0 && x < h) best = Math.Max(best, Math.Abs(Mx(x))); } }
            else
            {
                var disc = qa1 * qa1 - 4 * qa2 * qa0;
                if (disc >= 0) foreach (var x in new[] { (-qa1 + Math.Sqrt(disc)) / (2 * qa2), (-qa1 - Math.Sqrt(disc)) / (2 * qa2) }) if (x > 0 && x < h) best = Math.Max(best, Math.Abs(Mx(x)));
            }
            maxM[e.Member] = Math.Max(maxM.GetValueOrDefault(e.Member), best);
            for (var k = 0; k <= 8; k++)
            {
                var x = h * k / 8; var xi = x / h;
                var v = (1 - 3 * xi * xi + 2 * xi * xi * xi) * dl[1] + h * (xi - 2 * xi * xi + xi * xi * xi) * dl[2] + (3 * xi * xi - 2 * xi * xi * xi) * dl[4] + h * (-xi * xi + xi * xi * xi) * dl[5]
                    + e.Pa * x * x * (h - x) * (h - x) / (24 * e.EI) + dp * x * x * (h - x) * (h - x) * (x + 2 * h) / (120 * e.EI * h);
                var u = (1 - xi) * dl[0] + xi * dl[3] + e.Xa * x * (h - x) / (2 * e.EA) + (e.Xb - e.Xa) * x * (h * h - x * x) / (6 * e.EA * h);
                double gx = e.C * u - e.S * v, gy = e.S * u + e.C * v;
                disp = Math.Max(disp, Math.Sqrt(gx * gx + gy * gy));
            }
        }
        return (reactions, maxM, disp);
    }

    static double[] ToGlobal(double[] l, double c, double s) => [c * l[0] - s * l[1], s * l[0] + c * l[1], l[2], c * l[3] - s * l[4], s * l[3] + c * l[4], l[5]];

    /// <summary>Freie Freiheitsgrade nach Reverse Cuthill-McKee nummerieren, Steifigkeit im Hüllenspeicher (je Zeile ab der ersten belegten Spalte) aufbauen,
    /// mit Cholesky zerlegen und lösen. <paramref name="newIndex"/>: neue Nummer je Freiheitsgrad, −1 = festgehalten.</summary>
    static double[] SolveEnvelope(List<Sub> subs, bool[] fixedDof, double[] F, double[] diag, CancellationToken ct, long maxEnvelope, out int[] newIndex)
    {
        var dofs = fixedDof.Length; var freeOf = new int[dofs]; var n = 0;
        for (var i = 0; i < dofs; i++) freeOf[i] = fixedDof[i] ? -1 : n++;
        // Nachbarn je freiem Freiheitsgrad
        var adj = new List<int>[n]; for (var i = 0; i < n; i++) adj[i] = [];
        foreach (var e in subs)
            foreach (var a in e.Map) if (freeOf[a] >= 0) foreach (var b in e.Map) if (b != a && freeOf[b] >= 0) adj[freeOf[a]].Add(freeOf[b]);
        for (var i = 0; i < n; i++) { adj[i].Sort(); var w = 0; for (var k = 0; k < adj[i].Count; k++) if (k == 0 || adj[i][k] != adj[i][k - 1]) adj[i][w++] = adj[i][k]; adj[i].RemoveRange(w, adj[i].Count - w); }
        ct.ThrowIfCancellationRequested();
        var order = ReverseCuthillMcKee(adj); var pos = new int[n]; for (var k = 0; k < n; k++) pos[order[k]] = k;
        newIndex = new int[dofs]; for (var i = 0; i < dofs; i++) newIndex[i] = freeOf[i] < 0 ? -1 : pos[freeOf[i]];
        // Hülle: erste belegte Spalte je Zeile
        var first = new int[n]; long total = 0;
        for (var r = 0; r < n; r++) { var v = order[r]; var lo = r; foreach (var nb in adj[v]) lo = Math.Min(lo, pos[nb]); first[r] = lo; total += r - lo + 1; }
        if (total > maxEnvelope) throw new TooLargeException($"das Tragwerk ist zu groß für die zweite Rechnung ({total.ToString("N0", De)} Matrixeinträge, Grenze {maxEnvelope.ToString("N0", De)})");
        var row = new double[n][]; for (var r = 0; r < n; r++) row[r] = new double[r - first[r] + 1];
        foreach (var e in subs)
            for (var a = 0; a < 6; a++)
            {
                var ia = newIndex[e.Map[a]]; if (ia < 0) continue;
                for (var b = 0; b < 6; b++) { var ib = newIndex[e.Map[b]]; if (ib < 0 || ib > ia) continue; row[ia][ib - first[ia]] += e.Kg[a, b]; }
            }
        var scale = 0.0; for (var i = 0; i < dofs; i++) if (!fixedDof[i]) scale = Math.Max(scale, Math.Abs(diag[i]));
        // Cholesky K = L·Lᵀ zeilenweise in der Hülle (K symmetrisch positiv definit, sonst ist das System beweglich)
        for (var i = 0; i < n; i++)
        {
            if ((i & 63) == 0) ct.ThrowIfCancellationRequested();
            var ri = row[i]; var fi = first[i];
            for (var j = fi; j < i; j++)
            {
                var rj = row[j]; var fj = first[j]; var k0 = Math.Max(fi, fj); double sum = ri[j - fi];
                for (var k = k0; k < j; k++) sum -= ri[k - fi] * rj[k - fj];
                ri[j - fi] = sum / rj[j - fj];
            }
            double dd = ri[i - fi]; for (var k = fi; k < i; k++) dd -= ri[k - fi] * ri[k - fi];
            if (dd <= scale * 1e-12) throw new InvalidOperationException("das System ist beweglich");
            ri[i - fi] = Math.Sqrt(dd);
        }
        var x = new double[n];
        for (var i = 0; i < dofs; i++) if (newIndex[i] >= 0) x[newIndex[i]] = F[i];
        for (var i = 0; i < n; i++) { var ri = row[i]; var fi = first[i]; double s = x[i]; for (var k = fi; k < i; k++) s -= ri[k - fi] * x[k]; x[i] = s / ri[i - fi]; }
        for (var i = n - 1; i >= 0; i--) { var ri = row[i]; var fi = first[i]; x[i] /= ri[i - fi]; var xi = x[i]; for (var k = fi; k < i; k++) x[k] -= ri[k - fi] * xi; }
        return x;
    }

    /// <summary>Reverse Cuthill-McKee: Breitensuche ab einem fast peripheren Knoten je Teilgraph (getrennte Tragwerke einzeln), Nachbarn nach Grad;
    /// umgekehrt ergibt das eine kleine Bandbreite. Rückgabe: Reihenfolge der Knoten.</summary>
    static int[] ReverseCuthillMcKee(List<int>[] adj)
    {
        var n = adj.Length; var order = new List<int>(n); var done = new bool[n]; var stamp = new int[n]; var mark = 0;
        (int Far, int Depth) Bfs(int start)
        {
            mark++; var q = new Queue<int>(); q.Enqueue(start); stamp[start] = mark; var depth = new Dictionary<int, int> { [start] = 0 }; var last = start; var lastDepth = 0;
            while (q.Count > 0)
            {
                var u = q.Dequeue(); var du = depth[u];
                if (du > lastDepth || du == lastDepth && adj[u].Count < adj[last].Count) { last = u; lastDepth = du; }
                foreach (var v in adj[u]) if (stamp[v] != mark) { stamp[v] = mark; depth[v] = du + 1; q.Enqueue(v); }
            }
            return (last, lastDepth);
        }
        foreach (var seed in Enumerable.Range(0, n).OrderBy(v => adj[v].Count))
        {
            if (done[seed]) continue;
            var (start, ecc) = (seed, -1);
            for (var it = 0; it < 6; it++) { var (far, d) = Bfs(start); if (d <= ecc) break; ecc = d; start = far; }
            var q = new Queue<int>(); q.Enqueue(start); done[start] = true;
            while (q.Count > 0)
            {
                var u = q.Dequeue(); order.Add(u);
                foreach (var v in adj[u].Where(v => !done[v]).OrderBy(v => adj[v].Count)) { done[v] = true; q.Enqueue(v); }
            }
        }
        order.Reverse();
        return [.. order];
    }

    static void SecondMethod(FrameModel f, FrameResult r, List<CrossCheckItem> items, CancellationToken ct, long maxEnvelope)
    {
        var (reac, maxM, disp) = Solve(f, ct, maxEnvelope);
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
