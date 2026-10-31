using Trendo.Core.Statik;
using Xunit;
namespace Trendo.Core.Tests;

/// <summary>T-0275: Nachweis nach Eurocode gegen Handrechnung.</summary>
public class T0275_Eurocode
{
    static (FrameModel F, FMember M) Beam(double L, string sec, string mat = "Baustahl S235")
    { var f = new FrameModel(); var m = f.AddMember(0, 0, L, 0, sec, mat); f.Node(m.A)!.Support = "fest"; f.Node(m.B)!.Support = "los"; return (f, m); }

    [Fact(DisplayName = "T-0275: Einfeldträger 5 m IPE 270, q = 10 kN/m – η = 1,5·qL²/8 / (W·f_y); als ständige Last mit 1,35; Durchbiegung charakteristisch")]
    public void Beam_steel()
    {
        var (f, m) = Beam(5, "IPE 270"); f.AddLineLoad(m.Id, 10);
        var w = Sections.Get("IPE 270").W_mm3; var r = Eurocode.Check(f).Of(m.Id)!;
        Assert.Equal(1.5 * 10 * 25 / 8 * 1e6 / (w * 235), r.EtaStress, 3);
        Assert.Equal(FrameSolver.Solve(f).Members[0].MaxDefl_mm, r.DeflSls_mm, 6); // ohne Teilsicherheitsbeiwerte
        Assert.True(r.Ok); Assert.Contains("EC 3", r.Text);
        f.Loads[0].Permanent = true;
        Assert.Equal(1.35 * 10 * 25 / 8 * 1e6 / (w * 235), Eurocode.Check(f).Of(m.Id)!.EtaStress, 3);
        f.Members[0].Section = "IPE 160"; var bad = Eurocode.Check(f); Assert.False(bad.Ok); Assert.Contains("überlastet", bad.Of(m.Id)!.Text);
    }

    [Fact(DisplayName = "T-0275: Druckstab 4 m IPE 200 mit 200 kN – Biegeknicken nach EC 3, Knicklinie b: χ und η wie von Hand")]
    public void Buckling_steel()
    {
        var (f, m) = Beam(4, "IPE 200"); f.AddNodeLoad(m.B, 0, -200); // Druck nach links
        var s = Sections.Get("IPE 200"); double A = s.A_mm2, I = s.I_mm4, L = 4000;
        var ncr = Math.PI * Math.PI * 210_000 * I / (L * L); var lrel = Math.Sqrt(A * 235 / ncr);
        var phi = 0.5 * (1 + 0.34 * (lrel - 0.2) + lrel * lrel); var chi = 1 / (phi + Math.Sqrt(phi * phi - lrel * lrel));
        var r = Eurocode.Check(f).Of(m.Id)!;
        Assert.Equal(chi, r.Chi, 3);
        Assert.Equal(300_000 / (chi * A * 235 / 1.1), r.EtaBuckle!.Value, 3);
        Assert.Equal(300, r.NEd, 2);
    }

    [Fact(DisplayName = "T-0275: Holzbalken C24 100×200, 4 m, q = 3 kN/m – EC 5 mit k_mod 0,8 und γM 1,3; Beton wird ehrlich abgelehnt; Bericht")]
    public void Timber_concrete_report()
    {
        var (f, m) = Beam(4, "Rechteck 100x200", "Fichte/Nadelholz C24"); f.AddLineLoad(m.Id, 3);
        var r = Eurocode.Check(f).Of(m.Id)!;
        Assert.Equal(1.5 * 3 * 16 / 8 * 1e6 / (100 * 200 * 200 / 6.0) / (0.8 * 24 / 1.3), r.EtaStress, 3);
        Assert.Contains("EC 5", r.Text);
        var (g, n) = Beam(4, "Rechteck 100x200", "Beton C25"); g.AddLineLoad(n.Id, 3);
        var c = Eurocode.Check(g); Assert.False(c.Ok); Assert.Contains("Bewehrung", c.Of(n.Id)!.Text);
        var rep = Eurocode.Report(f, Eurocode.Check(f)); Assert.Contains("1,35·G + 1,5·Q", rep); Assert.Contains("| S1 | Rechteck 100x200 |", rep);
    }
}
