using System.Text.Json.Nodes;
using Trendo.Core.Statik;
using Xunit;
namespace Trendo.Core.Tests;

/// <summary>T-0258: Stabwerksrechnung gegen bekannte Lösungen (Schneider Bautabellen / Lehrbuchformeln).</summary>
public class T0258_Frame
{
    static double EI(string sec, double eGpa = 210) => eGpa * 1e6 * Sections.Get(sec).I_mm4 * 1e-12;

    [Fact(DisplayName = "T-0258: Einfeldträger 5 m, q = 10 kN/m – Auflager je 25 kN, M = qL²/8, f = 5qL⁴/384EI")]
    public void Simple_beam()
    {
        var f = FrameModel.Template("einfeld"); var r = FrameSolver.Solve(f);
        var m = Assert.Single(r.Members);
        Assert.All(r.Reactions.Values, x => Assert.Equal(25, x.Ry, 3));
        Assert.Equal(31.25, m.MaxM, 2);
        Assert.Equal(25, Math.Abs(m.MaxV), 3);
        Assert.Equal(5 * 10 * Math.Pow(5, 4) / (384 * EI("IPE 200")) * 1000, m.MaxDefl_mm, 1);
        Assert.Equal(31.25 / 194e-6 / 1000, m.Stress_MPa, 1); // M / W
        Assert.False(m.Ok); Assert.Contains("biegt sich zu stark", m.Verdict); // IPE 200 ist für 5 m und 10 kN/m zu weich
        f.Members[0].Section = "IPE 270";
        Assert.True(FrameSolver.Solve(f).Members[0].Ok);
    }

    [Fact(DisplayName = "T-0258: Kragarm 2 m mit 3 kN am Ende – Einspannmoment 6 kNm, f = PL³/3EI; Einzellast in Feldmitte M = PL/4")]
    public void Cantilever_and_point_load()
    {
        var f = FrameModel.Template("kragarm"); var r = FrameSolver.Solve(f);
        var (rx, ry, mm) = r.Reactions.Values.Single();
        Assert.Equal(0, rx, 6); Assert.Equal(3, ry, 4); Assert.Equal(6, Math.Abs(mm), 4);
        var ei = 11 * 1e6 * Sections.Get("Rechteck 100x200").I_mm4 * 1e-12;
        Assert.Equal(3 * 8 / (3 * ei) * 1000, r.Members[0].MaxDefl_mm, 1);
        Assert.Equal(-6, r.Members[0].MaxM, 3);
        var g = new FrameModel(); var s = g.AddMember(0, 0, 4, 0, "IPE 160"); g.Node(s.A)!.Support = "fest"; g.Node(s.B)!.Support = "los"; g.AddPointLoad(s.Id, 20, 0.5);
        Assert.Equal(20 * 4 / 4.0, FrameSolver.Solve(g).Members[0].MaxM, 2);
    }

    [Fact(DisplayName = "T-0258: Durchlaufträger 2 × 4 m, q = 8 kN/m – Mittelauflager 1,25·qL = 40 kN, Stützmoment −qL²/8 = −16 kNm")]
    public void Continuous_beam()
    {
        var f = FrameModel.Template("durchlauf"); var r = FrameSolver.Solve(f);
        var mid = f.NodeAt(4, 0).Id; var left = f.NodeAt(0, 0).Id;
        Assert.Equal(40, r.Reactions[mid].Ry, 2);
        Assert.Equal(12, r.Reactions[left].Ry, 2);
        Assert.Equal(-16, r.Members[0].M[^1], 2);
    }

    [Fact(DisplayName = "T-0258: Rahmen im Gleichgewicht (Summe Reaktionen = Lasten); Fachwerk mit Gelenken trägt nur Normalkraft")]
    public void Frame_and_truss()
    {
        var f = FrameModel.Template("rahmen"); var r = FrameSolver.Solve(f);
        Assert.Equal(12 * 6, r.Reactions.Values.Sum(x => x.Ry), 3);
        Assert.Equal(-5, r.Reactions.Values.Sum(x => x.Rx), 3);
        var t = FrameModel.Template("fachwerk"); var tr = FrameSolver.Solve(t);
        Assert.Equal(15, tr.Reactions[t.NodeAt(0, 0).Id].Ry, 3); Assert.Equal(15, tr.Reactions[t.NodeAt(8, 0).Id].Ry, 3);
        Assert.All(tr.Members, m => Assert.True(Math.Abs(m.MaxM) < 1e-3, $"Moment {m.MaxM} im Fachwerkstab"));
        Assert.Contains(tr.Members, m => m.MaxN < -1); Assert.Contains(tr.Members, m => m.MaxN > 1); // Druck- und Zugstäbe
        Assert.Contains(tr.Members, m => m.BuckleSafety is not null);
    }

    [Fact(DisplayName = "T-0258: bewegliches Tragwerk und fehlende Lager werden erkannt; Stab teilen, löschen, Querschnitte, Speichern")]
    public void Mechanism_and_editing()
    {
        var f = new FrameModel(); var m = f.AddMember(0, 0, 5, 0);
        Assert.Contains("keine Lager", Assert.Throws<InvalidOperationException>(() => FrameSolver.Solve(f)).Message);
        f.Node(m.A)!.Support = "los"; f.Node(m.B)!.Support = "los"; f.AddLineLoad(m.Id, 5);
        Assert.Contains("beweglich", Assert.Throws<InvalidOperationException>(() => FrameSolver.Solve(f)).Message);
        f.Node(m.A)!.Support = "fest";
        var before = FrameSolver.Solve(f).Members[0].MaxM;
        var k = f.SplitMember(m.Id, 0.5);
        Assert.Equal(2, f.Members.Count); Assert.Equal(2.5, k.X, 6);
        Assert.Equal(before, FrameSolver.Solve(f).Members.Max(x => x.MaxM), 1); // Teilen ändert das Ergebnis nicht
        Assert.Equal(2, f.Loads.Count(l => l.Kind == "strecke"));
        Assert.True(f.Remove(f.Members[1].Id)); Assert.Single(f.Members);
        Assert.Equal(1943e4, Sections.Get("ipe200").I_mm4, 0);
        Assert.Equal(60 * 120, Sections.Get("Rechteck 60x120").A_mm2, 3);
        Assert.Throws<ArgumentException>(() => Sections.Get("Brett"));
        var data = new JsonObject(); FrameModel.Template("rahmen").WriteTo(data);
        var back = FrameModel.From(data); Assert.Equal(3, back.Members.Count); Assert.Equal(2, back.Loads.Count);
    }
}
