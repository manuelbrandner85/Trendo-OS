using Trendo.Core.Statik;
using Xunit;
namespace Trendo.Core.Tests;

/// <summary>T-0271: Tragwerk – Trapez-/Dreieckslast, Temperatur, Lagersetzung, schräges Loslager gegen Lehrbuchformeln.</summary>
public class T0271_FramePlus
{
    static double EI(string sec) => 210e6 * Sections.Get(sec).I_mm4 * 1e-12;
    static double EA(string sec) => 210e6 * Sections.Get(sec).A_mm2 * 1e-6;
    static (FrameModel F, FMember M) Beam(double L, string a, string b, string sec = "IPE 200")
    { var f = new FrameModel(); var m = f.AddMember(0, 0, L, 0, sec); f.Node(m.A)!.Support = a; f.Node(m.B)!.Support = b; return (f, m); }

    [Fact(DisplayName = "T-0271: Dreieckslast 0 → 12 kN/m auf 6 m – Auflager qL/6 und qL/3, max M = qL²/(9√3); Trapez = gleichmäßig + Dreieck")]
    public void Triangle_and_trapez()
    {
        var (f, m) = Beam(6, "fest", "los"); f.AddTrapezLoad(m.Id, 0, 12);
        var r = FrameSolver.Solve(f, 201);
        Assert.Equal(12, r.Reactions[m.A].Ry, 3); Assert.Equal(24, r.Reactions[m.B].Ry, 3);
        Assert.Equal(12 * 36 / (9 * Math.Sqrt(3)), r.Members[0].MaxM, 2);
        var (g, n) = Beam(6, "fest", "los"); g.AddTrapezLoad(n.Id, 4, 10); // = 4 gleichmäßig + 0→6 Dreieck
        var rg = FrameSolver.Solve(g);
        Assert.Equal(4 * 3 + 6, rg.Reactions[n.A].Ry, 3); Assert.Equal(4 * 3 + 12, rg.Reactions[n.B].Ry, 3);
        var (h, k) = Beam(6, "einspannung", "einspannung"); h.AddTrapezLoad(k.Id, 0, 12); // eingespannt: MA = qL²/30, MB = qL²/20
        var rh = FrameSolver.Solve(h);
        Assert.Equal(12 * 36 / 30.0, Math.Abs(rh.Reactions[k.A].M), 3); Assert.Equal(12 * 36 / 20.0, Math.Abs(rh.Reactions[k.B].M), 3);
    }

    [Fact(DisplayName = "T-0271: Temperatur – eingespannt N = EAαΔT, frei verschieblich ΔL = αΔT·L; Unterschied oben/unten: eingespannt M = EIαΔT/h, Einfeld f = κL²/8")]
    public void Temperature()
    {
        const double a = 12e-6;
        var (f, m) = Beam(5, "einspannung", "einspannung"); f.AddTemperature(m.Id, 30);
        Assert.Equal(-EA("IPE 200") * a * 30, FrameSolver.Solve(f).Members[0].MaxN, 1); // Druck
        var (g, n) = Beam(5, "fest", "los"); g.AddTemperature(n.Id, 30);
        var rg = FrameSolver.Solve(g); Assert.Equal(a * 30 * 5, rg.Displacements[n.B].Ux, 9); Assert.Equal(0, rg.Members[0].MaxN, 6);
        var (h, k) = Beam(5, "einspannung", "einspannung"); h.AddTemperature(k.Id, 0, 20);
        var kap = a * 20 / 0.2; var rh = FrameSolver.Solve(h);
        Assert.Equal(EI("IPE 200") * kap, Math.Abs(rh.Members[0].MaxM), 2); Assert.True(rh.Members[0].MaxDefl_mm < 0.01);
        var (s, t) = Beam(5, "fest", "los"); s.AddTemperature(t.Id, 0, 20);
        var rs = FrameSolver.Solve(s); Assert.Equal(0, rs.Members[0].MaxM, 4); Assert.Equal(kap * 25 / 8 * 1000, rs.Members[0].MaxDefl_mm, 1);
    }

    [Fact(DisplayName = "T-0271: Setzung 10 mm – beidseitig eingespannt M = 6EIδ/L², Einfeldträger zwängungsfrei")]
    public void Settlement()
    {
        var (f, m) = Beam(4, "einspannung", "einspannung"); f.Node(m.B)!.Settle = 10;
        var r = FrameSolver.Solve(f);
        Assert.Equal(6 * EI("IPE 200") * 0.01 / 16, Math.Abs(r.Reactions[m.A].M), 2);
        Assert.Equal(-0.01, r.Displacements[m.B].Uy, 9);
        var (g, n) = Beam(4, "fest", "los"); g.Node(n.B)!.Settle = 10;
        var rg = FrameSolver.Solve(g); Assert.Equal(0, rg.Members[0].MaxM, 6); Assert.Equal(0, rg.Reactions[n.A].Ry, 6);
    }

    [Fact(DisplayName = "T-0271: Loslager auf 30° geneigter Ebene – Auflagerkraft senkrecht zur Ebene, Festlager nimmt die waagerechte Kraft")]
    public void Inclined_roller()
    {
        var (f, m) = Beam(4, "fest", "los"); f.Node(m.B)!.Angle = 30; f.AddPointLoad(m.Id, 20, 0.5);
        var r = FrameSolver.Solve(f); var (bx, by, _) = r.Reactions[m.B]; var (ax, ay, _) = r.Reactions[m.A];
        Assert.Equal(10, by, 3); Assert.Equal(-10 * Math.Tan(Math.PI / 6), bx, 3); // senkrecht zur Ebene (−sin30, cos30)
        Assert.Equal(-bx, ax, 3); Assert.Equal(10, ay, 3);
    }
}
