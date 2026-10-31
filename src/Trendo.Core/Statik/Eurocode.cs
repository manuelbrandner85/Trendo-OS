using System.Globalization;
using System.Text;

namespace Trendo.Core.Statik;

public sealed record EcMember(string Id, double EtaStress, double? EtaBuckle, double Chi, double DeflSls_mm, double DeflLimit_mm, double NEd, double MEd, double FdMPa, bool Ok, string Text);
public sealed record EcResult(List<EcMember> Members, string Combination, string Note, bool Ok)
{
    public EcMember? Of(string id) => Members.FirstOrDefault(m => m.Id == id);
}

/// <summary>T-0275: Vereinfachter Nachweis nach Eurocode für Stabwerke.
/// Einwirkungen (EC 0, Grundkombination): 1,35·G + 1,5·Q – alle veränderlichen Lasten als Leiteinwirkung zusammengefasst (auf der sicheren Seite).
/// Stahl, Edelstahl, Aluminium (EC 3 vereinfacht): f_d = f_y/γM0 (1,0); Biegeknicken mit Knicklinie b (α = 0,34), γM1 = 1,1; Knicklänge = Stablänge.
/// Holz C24 (EC 5): f_m,d = k_mod·f_m,k/γM (0,8 · 24 / 1,3), Druck f_c,0,k = 21, Knickbeiwert k_c (β_c = 0,2), E_0,05 = 7400 N/mm².
/// Interaktion linear N/N_Rd + M/M_Rd ≤ 1 (ohne k-Beiwerte, auf der sicheren Seite). Durchbiegung charakteristisch ≤ L/300.
/// Beton: Nachweis nur mit Bewehrung – hier nicht enthalten.</summary>
public static class Eurocode
{
    public const double GammaG = 1.35, GammaQ = 1.5;

    /// <summary>Bemessungsmodell: Lasten mit γG bzw. γQ multipliziert, Eigengewicht mit γG.</summary>
    public static FrameModel DesignModel(FrameModel f)
    {
        var d = f.Clone(); d.SelfWeightFactor = GammaG;
        foreach (var l in d.Loads)
        {
            var g = l.Permanent ? GammaG : GammaQ;
            l.Value *= g; if (l.Value2 is { } v2) l.Value2 = v2 * g; l.Fx *= g; l.M *= g; // Temperatur (M = Unterschied) ebenfalls mit γQ
        }
        foreach (var n in d.Nodes) n.Settle *= GammaG; // Setzung als ständige Einwirkung
        return d;
    }

    public static EcResult Check(FrameModel f)
    {
        var sls = FrameSolver.Solve(f); var uls = FrameSolver.Solve(DesignModel(f));
        var res = new List<EcMember>();
        foreach (var m in f.Members)
        {
            var mat = Engineering.Mat(m.Material); var sec = Sections.Get(m.Section);
            var u = uls.Of(m.Id)!; var s = sls.Of(m.Id)!;
            double A = sec.A_mm2, W = sec.W_mm3, I = sec.I_mm4, L = u.L * 1000;
            double nEd = u.N.Min() < 0 ? -u.N.Min() : 0, nTension = Math.Max(0, u.N.Max()), mEd = Math.Abs(u.MaxM) * 1e6; // N in kN → N, M in kNm → Nmm
            nEd *= 1000; nTension *= 1000;
            double fd, eta, chi = 1; double? etaB = null; string kind;
            if (mat.Name.StartsWith("Beton"))
            {
                res.Add(new(m.Id, double.NaN, null, 1, s.MaxDefl_mm, u.L * 1000 / 300, nEd / 1000, mEd / 1e6, 0, false, "⚠ Beton: Nachweis nur mit Bewehrung – nicht enthalten, bitte Tragwerksplanung")); continue;
            }
            if (mat.Name.Contains("C24"))
            {
                kind = "EC 5"; const double kmod = 0.8, gM = 1.3, fmk = 24, fc0k = 21, ft0k = 14.5, E005 = 7400;
                fd = kmod * fmk / gM; var fcd = kmod * fc0k / gM; var ftd = kmod * ft0k / gM;
                var i = Math.Sqrt(I / A); var lam = L / i; var lrel = lam / Math.PI * Math.Sqrt(fc0k / E005);
                var k = 0.5 * (1 + 0.2 * (lrel - 0.3) + lrel * lrel); chi = lrel <= 0.3 ? 1 : 1 / (k + Math.Sqrt(k * k - lrel * lrel));
                eta = (nTension > 0 ? nTension / (A * ftd) : nEd / (A * fcd)) + mEd / (W * fd);
                if (nEd > 0) etaB = nEd / (chi * A * fcd) + mEd / (W * fd);
            }
            else
            {
                kind = "EC 3"; const double gM0 = 1.0, gM1 = 1.1, alpha = 0.34;
                fd = mat.Yield_MPa / gM0;
                eta = Math.Max(nEd, nTension) / (A * fd) + mEd / (W * fd);
                var ncr = Math.PI * Math.PI * mat.E_GPa * 1000 * I / (L * L); var lrel = Math.Sqrt(A * mat.Yield_MPa / ncr);
                var phi = 0.5 * (1 + alpha * (lrel - 0.2) + lrel * lrel); chi = lrel <= 0.2 ? 1 : Math.Min(1, 1 / (phi + Math.Sqrt(phi * phi - lrel * lrel)));
                if (nEd > 0) etaB = nEd / (chi * A * mat.Yield_MPa / gM1) + mEd / (W * mat.Yield_MPa / gM1);
            }
            var limit = u.L * 1000 / 300; var problems = new List<string>();
            if (eta > 1) problems.Add($"Querschnitt überlastet (η = {eta:0.00})");
            if (etaB > 1) problems.Add($"Knicken (η = {etaB:0.00}, χ = {chi:0.00})");
            if (s.MaxDefl_mm > limit) problems.Add($"Durchbiegung {s.MaxDefl_mm:0.0} mm > L/300 = {limit:0.0} mm");
            var ok = problems.Count == 0;
            res.Add(new(m.Id, Math.Round(eta, 3), etaB is { } b ? Math.Round(b, 3) : null, Math.Round(chi, 3), s.MaxDefl_mm, Math.Round(limit, 1), Math.Round(nEd / 1000, 2), Math.Round(mEd / 1e6, 2), Math.Round(fd, 1), ok,
                ok ? $"✓ {kind}: Ausnutzung {Math.Max(eta, etaB ?? 0) * 100:0} %" : $"⚠ {kind}: " + string.Join(", ", problems)));
        }
        return new(res, "Grundkombination 1,35·G + 1,5·Q (alle veränderlichen Lasten als Leiteinwirkung)",
            "Vereinfachter Nachweis zur Vorbemessung – ersetzt keine geprüfte Statik eines Tragwerksplaners.", res.All(r => r.Ok));
    }

    public static string Report(FrameModel f, EcResult r)
    {
        var de = CultureInfo.GetCultureInfo("de-DE"); string N(double v, int d = 2) => double.IsNaN(v) ? "–" : v.ToString("N" + d, de);
        var sb = new StringBuilder($"# Nachweis nach Eurocode – {f.Name}\n\n{r.Combination}  \n{r.Note}\n\n| Stab | Querschnitt | Werkstoff | N_Ed (kN) | M_Ed (kNm) | f_d (N/mm²) | η Querschnitt | η Knicken | χ | f (mm) | L/300 (mm) | Ergebnis |\n|---|---|---|---|---|---|---|---|---|---|---|---|\n");
        for (var i = 0; i < f.Members.Count; i++)
        {
            var m = f.Members[i]; var e = r.Of(m.Id)!;
            sb.Append($"| S{i + 1} | {m.Section} | {m.Material} | {N(e.NEd)} | {N(e.MEd)} | {N(e.FdMPa, 1)} | {N(e.EtaStress)} | {(e.EtaBuckle is { } b ? N(b) : "–")} | {N(e.Chi)} | {N(e.DeflSls_mm, 1)} | {N(e.DeflLimit_mm, 1)} | {e.Text} |\n");
        }
        sb.Append("\nLasten: ").Append(string.Join("; ", f.Loads.Select(l => $"{(l.Permanent ? "G" : "Q")} {l.Kind} {N(l.Value)}"))).Append(f.SelfWeight ? "; Eigengewicht als G" : "").Append('\n');
        return sb.ToString();
    }
}
