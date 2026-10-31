using System.Globalization;
using System.Numerics;
using Trendo.Core.Arch;

namespace Trendo.Core.Elektro;

public sealed record BodePoint(double F, double MagDb, double PhaseDeg);

/// <summary>T-0272: Frequenzgang (Kleinsignal-Wechselstromanalyse) – Arbeitspunkt mit ausgeschalteten Wechselquellen, dann komplexe Knotenanalyse
/// je Frequenz: R, C (jωC), L (1/jωL), Dioden und Transistoren linearisiert, OPV als gesteuerte Quelle. Eingang: Wechselspannung mit 1 V.</summary>
public static class AcAnalysis
{
    const double Vt = 0.025852;

    public static List<BodePoint> Sweep(Circuit c, (int X, int Y) probe, double f0 = 10, double f1 = 100_000, int count = 61)
    {
        if (!c.Parts.Any(p => p.Kind == "wechsel")) throw new InvalidOperationException("Für den Frequenzgang braucht die Schaltung eine Wechselspannung als Eingang.");
        var net = Netlist.Build(c); var probeNode = net.NodeOf(probe.X, probe.Y);
        if (probeNode < 0) throw new ArgumentException("Der Messpunkt liegt nicht an der Schaltung.");
        // Arbeitspunkt: Wechselquellen auf 0, so lange rechnen, bis Kondensatoren geladen sind
        var dc = c.Clone(); foreach (var p in dc.Parts.Where(p => p.Kind == "wechsel")) p.Value = 0;
        var sim = new Simulator(dc, 0.005); var op = sim.Run(3);
        var parts = c.Parts.Where(p => p.Kind is not ("draht" or "masse")).ToList();
        var n = net.Count - 1; var srcs = parts.Where(p => p.Kind is "spannung" or "wechsel" or "opv").ToList(); var size = n + srcs.Count;
        double NodeV(int node) => node > 0 && node < op.NodeV.Length ? op.NodeV[node] : 0;
        int Pin(Part p) => p.Pin3() is { } q ? net.NodeOf(q.X, q.Y) : 0;
        var res = new List<BodePoint>();
        for (var k = 0; k < count; k++)
        {
            var f = f0 * Math.Pow(f1 / f0, count == 1 ? 0 : (double)k / (count - 1)); var w = 2 * Math.PI * f;
            var G = new Complex[size, size]; var b = new Complex[size];
            void Y(int a, int cc, Complex y) { if (a > 0) G[a - 1, a - 1] += y; if (cc > 0) G[cc - 1, cc - 1] += y; if (a > 0 && cc > 0) { G[a - 1, cc - 1] -= y; G[cc - 1, a - 1] -= y; } }
            void Gm(int from, int to, int cp, int cn, double gm) { void A(int r, int col, double v) { if (r > 0 && col > 0) G[r - 1, col - 1] += v; } A(from, cp, gm); A(from, cn, -gm); A(to, cp, -gm); A(to, cn, gm); } // Strom from→to = gm·(Vcp−Vcn)
            for (var i = 0; i < n; i++) G[i, i] += 1e-12;
            var si = 0;
            foreach (var p in parts)
            {
                int a = net.NodeOf(p.X1, p.Y1), cc = net.NodeOf(p.X2, p.Y2);
                switch (p.Kind)
                {
                    case "widerstand": case "lampe": Y(a, cc, 1 / Math.Max(1e-6, p.Value)); break;
                    case "schalter": Y(a, cc, p.Closed ? 1e3 : 1e-12); break;
                    case "kondensator": Y(a, cc, new Complex(0, w * Math.Max(1e-15, p.Value))); break;
                    case "spule": Y(a, cc, 1 / new Complex(0, w * Math.Max(1e-12, p.Value))); break;
                    case "diode": case "led":
                        { var (Is, N) = p.Kind == "led" ? (1e-19, 2.0) : (2.52e-9, 1.752); var v = op.Parts.TryGetValue(p.Id, out var r) ? r.V : 0; Y(a, cc, Is / (N * Vt) * Math.Exp(Math.Min(v / (N * Vt), 80)) + 1e-12); break; }
                    case "npn": case "pnp":
                        {
                            var bn = Pin(p); var ic = op.Parts.TryGetValue(p.Id, out var r) ? Math.Abs(r.I) : 0; var gm = ic / Vt; var gpi = gm / Math.Max(1, p.Value) + 1e-12;
                            Y(bn, cc, gpi); Gm(a, cc, bn, cc, gm); break; // Hybrid-π: rπ zwischen B und E, gm·vbe von C nach E (für PNP gleich)
                        }
                    case "opv":
                        {
                            var kk = n + si++; var pin = Pin(p); var vs = Math.Max(0.1, p.Value); var th = Math.Tanh(1e5 * (NodeV(pin) - NodeV(a)) / vs); var g = 1e5 * (1 - th * th);
                            if (cc > 0) { G[cc - 1, kk] += 1; G[kk, cc - 1] += 1; }
                            if (pin > 0) G[kk, pin - 1] -= g; if (a > 0) G[kk, a - 1] += g; break;
                        }
                    case "spannung": case "wechsel":
                        {
                            var kk = n + si++;
                            if (cc > 0) { G[cc - 1, kk] += 1; G[kk, cc - 1] += 1; }
                            if (a > 0) { G[a - 1, kk] -= 1; G[kk, a - 1] -= 1; }
                            b[kk] = p.Kind == "wechsel" ? 1 : 0; break;
                        }
                }
            }
            var x = Solve(G, b); var vp = probeNode > 0 ? x[probeNode - 1] : Complex.Zero;
            res.Add(new(f, 20 * Math.Log10(Math.Max(1e-12, vp.Magnitude)), vp.Phase * 180 / Math.PI));
        }
        return res;
    }

    /// <summary>Grenzfrequenz: erste Frequenz, bei der der Betrag 3 dB unter dem Höchstwert liegt (null, wenn er nirgends so weit fällt).</summary>
    public static double? Cutoff(IReadOnlyList<BodePoint> pts)
    {
        if (pts.Count < 2) return null; var max = pts.Max(p => p.MagDb); var im = pts.ToList().FindIndex(p => p.MagDb == max);
        for (var i = 1; i < pts.Count; i++)
        {
            var (a, b) = (pts[i - 1], pts[i]);
            foreach (var dir in new[] { 1 })
                if ((a.MagDb - (max - 3)) * (b.MagDb - (max - 3)) <= 0 && a.MagDb != b.MagDb)
                { var t = (max - 3 - a.MagDb) / (b.MagDb - a.MagDb); return Math.Exp(Math.Log(a.F) + t * (Math.Log(b.F) - Math.Log(a.F))); }
        }
        return null;
    }

    static Complex[] Solve(Complex[,] A, Complex[] b)
    {
        var n = b.Length; var a = (Complex[,])A.Clone(); var x = (Complex[])b.Clone();
        for (var col = 0; col < n; col++)
        {
            var piv = col; for (var r = col + 1; r < n; r++) if (a[r, col].Magnitude > a[piv, col].Magnitude) piv = r;
            if (a[piv, col].Magnitude < 1e-18) throw new InvalidOperationException("Die Schaltung lässt sich bei dieser Frequenz nicht berechnen (Kurzschluss über eine Quelle?).");
            if (piv != col) { for (var k = 0; k < n; k++) (a[col, k], a[piv, k]) = (a[piv, k], a[col, k]); (x[col], x[piv]) = (x[piv], x[col]); }
            for (var r = col + 1; r < n; r++) { var f = a[r, col] / a[col, col]; if (f == Complex.Zero) continue; for (var k = col; k < n; k++) a[r, k] -= f * a[col, k]; x[r] -= f * x[col]; }
        }
        for (var r = n - 1; r >= 0; r--) { var s = x[r]; for (var k = r + 1; k < n; k++) s -= a[r, k] * x[k]; x[r] = s / a[r, r]; }
        return x;
    }

    /// <summary>Bode-Diagramm: Betrag (dB, blau) und Phase (°, gestrichelt), logarithmische Frequenzachse, Grenzfrequenz markiert.</summary>
    public static List<Shape> Draw(IReadOnlyList<BodePoint> pts, double w, double h)
    {
        var s = new List<Shape>(); if (pts.Count < 2) return s;
        var de = CultureInfo.GetCultureInfo("de-DE");
        const double L = 44, T = 10, R = 40, B = 22; var pw = w - L - R; var ph = h - T - B;
        double lf0 = Math.Log10(pts[0].F), lf1 = Math.Log10(pts[^1].F);
        double mx = Math.Ceiling(pts.Max(p => p.MagDb) / 10) * 10 + 5, mn = Math.Max(mx - 100, Math.Floor(pts.Min(p => p.MagDb) / 10) * 10 - 5);
        double X(double f) => L + (Math.Log10(f) - lf0) / Math.Max(1e-9, lf1 - lf0) * pw; double Ym(double db) => T + (mx - Math.Clamp(db, mn, mx)) / (mx - mn) * ph; double Yp(double deg) => T + (180 - deg) / 360 * ph;
        for (var d = Math.Ceiling(lf0); d <= lf1 + 1e-9; d++) { var x = X(Math.Pow(10, d)); s.Add(new DLine(x, T, x, T + ph, "#FFFFFF18", 1, "RASTER")); s.Add(new DText(x, h - 6, Hz(Math.Pow(10, d)), 10, "#8FA3BF", "ACHSE")); }
        for (var db = Math.Ceiling(mn / 20) * 20; db <= mx; db += 20) { s.Add(new DLine(L, Ym(db), L + pw, Ym(db), "#FFFFFF14", 1, "RASTER")); s.Add(new DText(L - 20, Ym(db) + 4, db.ToString("0", de) + " dB", 9, "#7FB4FF", "ACHSE")); }
        foreach (var deg in new[] { -180, -90, 0, 90, 180 }) s.Add(new DText(L + pw + 18, Yp(deg) + 4, deg + "°", 9, "#C084FC", "ACHSE"));
        s.Add(new DPoly(pts.Select(p => (X(p.F), Yp(p.PhaseDeg))).ToList(), null, "#C084FC", 1.4, "PHASE", Closed: false));
        s.Add(new DPoly(pts.Select(p => (X(p.F), Ym(p.MagDb))).ToList(), null, "#4FA3FF", 2.2, "BETRAG", Closed: false));
        if (Cutoff(pts) is { } fc) { var x = X(fc); s.Add(new DLine(x, T, x, T + ph, "#F5A524", 1.4, "GRENZE", true)); s.Add(new DText(x + 4, T + 12, "fg " + Hz(fc), 10, "#F5A524", "GRENZE", true)); }
        return s;
    }

    public static string Hz(double f) => f >= 1e6 ? $"{f / 1e6:0.##} MHz" : f >= 1e3 ? $"{f / 1e3:0.##} kHz" : $"{f:0.##} Hz";
}
