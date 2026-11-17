using Trendo.Core.Statik;
using Xunit;
namespace Trendo.Core.Tests;

/// <summary>T-0748: Gegenprobe der Tragwerksrechnung. Die Lehrbuchwerte unten sind selbst nachgerechnet (Formeln der Balkentheorie),
/// nicht aus Tabellenwerken übernommen. IPE 200: I = 1943 cm⁴, E = 210 000 N/mm² → EI = 4080,3 kNm².</summary>
public class T0748_CrossCheck
{
    static double ItemMain(CrossCheckResult c, string what) => c.Items.First(i => i.What == what).Main;
    static double ItemRef(CrossCheckResult c, string what) => c.Items.First(i => i.What == what).Reference;

    [Fact(DisplayName = "T-0748: Einfeldträger 5 m, 10 kN/m – M = 31,25 kNm, w = 19,94 mm, A = 25 kN; Gegenprobe ✓ unter 0,5 %")]
    public void Einfeld()
    {
        var c = FrameCrossCheck.Check(FrameModel.Template("einfeld"));
        Assert.True(c.Ok, c.Text);
        Assert.Contains("Formel Einfeldträger", c.Methods); Assert.Contains("zweite Rechnung (Teilstäbe)", c.Methods);
        Assert.Equal(31.25, ItemRef(c, "Feldmoment"), 6);           // 10 · 5² / 8
        Assert.Equal(19.94, ItemRef(c, "Durchbiegung"), 2);         // 5 · 10 · 5⁴ / (384 · 4080,3) m = 19,94 mm
        Assert.Equal(25, ItemRef(c, "Lager links"), 6);
        Assert.Equal(31.25, ItemMain(c, "Feldmoment"), 2);
        Assert.InRange(c.MaxDeviationPct, 0, FrameCrossCheck.TolerancePct);
        Assert.StartsWith("Gegenprobe ✓ Abweichung", c.Text);
    }

    [Fact(DisplayName = "T-0748: Kragarm Holz 2 m, 3 kN am Ende – M = 6 kNm, w = FL³/3EI = 10,91 mm (C24, 10/20)")]
    public void Kragarm()
    {
        var c = FrameCrossCheck.Check(FrameModel.Template("kragarm"));
        Assert.True(c.Ok, c.Text); Assert.Contains("Formel Kragarm", c.Methods);
        Assert.Equal(6, ItemRef(c, "Einspannmoment"), 6);
        Assert.Equal(3 * 8 / (3 * 11e6 * (0.1 * 0.2 * 0.2 * 0.2 / 12)) * 1000, ItemRef(c, "Durchbiegung Spitze"), 6); // 10,91 mm
        Assert.Equal(10.91, ItemMain(c, "Durchbiegung Spitze"), 2);
    }

    [Fact(DisplayName = "T-0748: Zweifeldträger 2 × 4 m, 8 kN/m – Stützmoment 16 kNm, A = 12 kN, B = 40 kN, w = qL⁴/185EI")]
    public void Durchlauf()
    {
        var c = FrameCrossCheck.Check(FrameModel.Template("durchlauf"));
        Assert.True(c.Ok, c.Text); Assert.Contains("Formel Zweifeldträger", c.Methods);
        Assert.Equal(16, ItemMain(c, "Stützmoment"), 2);   // 8 · 4² / 8
        Assert.Equal(12, ItemMain(c, "Lager außen"), 2);   // 3/8 · 8 · 4
        Assert.Equal(40, ItemMain(c, "Lager Mitte"), 2);   // 10/8 · 8 · 4
        var ei = 210e6 * 1317e-8; Assert.Equal(8 * 256 / (185.0 * ei) * 1000, ItemRef(c, "Durchbiegung"), 1); // ≈ qL⁴/185EI
    }

    [Fact(DisplayName = "T-0748: Zweigelenkrahmen und Fachwerk – Gleichgewicht und zweite Rechnung (Teilstäbe, Gelenke als eigener Drehwinkel) stimmen auf 0,5 %")]
    public void Rahmen_und_Fachwerk()
    {
        foreach (var t in new[] { "rahmen", "fachwerk" })
        {
            var c = FrameCrossCheck.Check(FrameModel.Template(t));
            Assert.True(c.Ok, t + ": " + c.Text); Assert.Null(c.Skipped);
            Assert.Contains("zweite Rechnung (Teilstäbe)", c.Methods);
            Assert.True(c.Items.Count(i => i.Source == "zweite Rechnung") >= 4);
        }
        // Rahmen: Summe der senkrechten Lagerkräfte = 12 kN/m · 6 m = 72 kN, waagerecht = −5 kN
        var r = FrameCrossCheck.Check(FrameModel.Template("rahmen"));
        Assert.Equal(72, ItemMain(r, "Summe senkrecht"), 6); Assert.Equal(-5, ItemMain(r, "Summe waagerecht"), 6);
    }

    [Fact(DisplayName = "T-0748: Zweigelenkrahmen 6 × 4 m, 12 kN/m auf dem Riegel – Horizontalschub H = ql²/(4h(2k+3)), k = I_R·h/(I_S·l) (Kraftgrößenverfahren, selbst hergeleitet)")]
    public void Zweigelenkrahmen_Schub()
    {
        var f = FrameModel.Template("rahmen"); f.Loads.RemoveAll(l => l.Kind == "knoten");
        double l = 6, h = 4, q = 12, k = 8356.0 * h / (3692.0 * l); // IPE 300 Riegel, HEA 200 Stiele
        var H = q * l * l / (4 * h * (2 * k + 3)); // ≈ 4,49 kN; Formel ohne Längsdehnung der Stäbe
        var left = f.Nodes.Single(n => n.X == 0 && n.Y == 0).Id;
        var main = Math.Abs(FrameSolver.Solve(f).Reactions[left].Rx);
        var second = Math.Abs(FrameCrossCheck.Solve(f).Reactions[left].Rx);
        Assert.InRange(Math.Abs(main - H) / H * 100, 0, FrameCrossCheck.TolerancePct);
        Assert.InRange(Math.Abs(second - H) / H * 100, 0, FrameCrossCheck.TolerancePct);
        Assert.Equal(H * h, Math.Abs(FrameSolver.Solve(f).Members[0].MaxM), 1); // Eckmoment = H·h
    }

    [Fact(DisplayName = "T-0748: Trapezlast, Einzellast außermittig, Eigengewicht, beidseitig eingespannt – zweite Rechnung stimmt; Temperatur wird ehrlich als nicht geprüft gemeldet")]
    public void Weitere_Lastfaelle()
    {
        var f = new FrameModel(); var m = f.AddMember(0, 0, 6, 0, "IPE 240"); f.Node(m.A)!.Support = "einspannung"; f.Node(m.B)!.Support = "einspannung";
        f.AddTrapezLoad(m.Id, 2, 9); f.AddPointLoad(m.Id, 15, 0.3); f.SelfWeight = true;
        var c = FrameCrossCheck.Check(f);
        Assert.True(c.Ok, c.Text);
        f.AddTemperature(m.Id, 20);
        var t = FrameCrossCheck.Check(f);
        Assert.Contains("Temperatur", t.Skipped); Assert.DoesNotContain("zweite Rechnung (Teilstäbe)", t.Methods); Assert.True(t.Ok, t.Text); // Gleichgewicht gilt weiter
    }

    [Fact(DisplayName = "T-0748: Abweichung über 0,5 % ergibt eine Warnung statt ✓; Bericht nennt Quelle und „ersetzt keine Prüfstatik“")]
    public void Warnung_und_Bericht()
    {
        var bad = new CrossCheckResult([new("Feldmoment", 31.25, 30, 4.1667, "Tabelle"), new("Lager links", 25, 25, 0, "Tabelle")], ["Formel"], null);
        Assert.False(bad.Ok); Assert.StartsWith("⚠ Gegenprobe weicht ab: Feldmoment 4,17 %", bad.Text);
        var md = FrameCrossCheck.Report(FrameModel.Template("einfeld"), FrameCrossCheck.Check(FrameModel.Template("einfeld")));
        Assert.Contains("| Feldmoment |", md); Assert.Contains("Tabelle: M = qL²/8", md); Assert.Contains("ersetzt keine Prüfstatik", md);
    }

    [Fact(DisplayName = "T-0748: zweite Rechnung allein – Einfeldträger mit Mitteneinzellast 20 kN auf 4 m: M = FL/4 = 20 kNm, w = FL³/48EI")]
    public void Zweite_Rechnung_allein()
    {
        var f = new FrameModel(); var m = f.AddMember(0, 0, 4, 0, "IPE 200"); f.Node(m.A)!.Support = "fest"; f.Node(m.B)!.Support = "los"; f.AddPointLoad(m.Id, 20, 0.5);
        var (re, mm, w) = FrameCrossCheck.Solve(f);
        Assert.Equal(20, mm[m.Id], 6); Assert.Equal(10, re[m.A].Ry, 6);
        Assert.Equal(20 * 64 / (48 * 210e6 * 1943e-8), w, 8);
    }
}
