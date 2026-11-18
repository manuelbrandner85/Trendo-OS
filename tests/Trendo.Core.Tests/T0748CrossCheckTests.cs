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
    // ---------- Gegenprüfung behoben (T-0748-fix) ----------
    /// <summary>Rahmen: Stiele HEA 200 (3,5 m), Riegel IPE 300 (6 m) mit 12 kN/m, Fußpunkte fest, 5 kN waagerecht oben links.</summary>
    internal static FrameModel Frame(int storeys, int bays)
    {
        var m = new FrameModel(); double h = 3.5, l = 6;
        for (var s = 0; s < storeys; s++) for (var b = 0; b <= bays; b++) m.AddMember(b * l, s * h, b * l, (s + 1) * h, "HEA 200");
        for (var s = 1; s <= storeys; s++) for (var b = 0; b < bays; b++) { var x = m.AddMember(b * l, s * h, (b + 1) * l, s * h, "IPE 300"); m.AddLineLoad(x.Id, 12); }
        foreach (var n in m.Nodes.Where(n => Math.Abs(n.Y) < 1e-9)) n.Support = "fest";
        m.AddNodeLoad(m.Nodes.First(n => Math.Abs(n.Y - storeys * h) < 1e-9 && Math.Abs(n.X) < 1e-9).Id, 0, 5);
        return m;
    }
    /// <summary>Durchlaufträger aus Stäben à 1 m, IPE 200, 8 kN/m, Lager alle <paramref name="perSpan"/> Stäbe.</summary>
    internal static FrameModel Beam(int members, int perSpan)
    {
        var m = new FrameModel();
        for (var i = 0; i < members; i++) { var x = m.AddMember(i, 0, i + 1, 0, "IPE 200"); m.AddLineLoad(x.Id, 8); }
        foreach (var n in m.Nodes) { var k = (int)Math.Round(n.X); if (k % perSpan == 0) n.Support = k == 0 ? "fest" : "los"; }
        return m;
    }

    [Theory(DisplayName = "T-0748-fix: Rahmen mit 6, 10, 15 und 35 Stäben – zweite Rechnung (konsistente Lastvektoren) stimmt unter 0,5 % (vorher 1,29 %, 0,60 %, 1,08 %, 5,21 %)")]
    [InlineData(2, 1, 6)] [InlineData(2, 2, 10)] [InlineData(3, 2, 15)] [InlineData(5, 3, 35)]
    public void Rahmen_mehrgeschossig(int storeys, int bays, int members)
    {
        var f = Frame(storeys, bays); Assert.Equal(members, f.Members.Count);
        var c = FrameCrossCheck.Check(f);
        Assert.Contains("zweite Rechnung (Teilstäbe)", c.Methods);
        Assert.True(c.Ok, c.Text); Assert.InRange(c.MaxDeviationPct, 0, 0.5);
        Assert.StartsWith("Gegenprobe ✓ Abweichung", c.Text);
    }

    [Fact(DisplayName = "T-0748-fix: Durchlaufträger mit 200 Stäben (jeder Stab ein Feld und alle 4 Stäbe ein Feld) und 8 getrennte Einfeldträger – Abweichung unter 0,5 %")]
    public void Durchlauf_200_und_getrennte_Traeger()
    {
        foreach (var f in new[] { Beam(200, 1), Beam(200, 4) })
        { var c = FrameCrossCheck.Check(f); Assert.Contains("zweite Rechnung (Teilstäbe)", c.Methods); Assert.True(c.Ok, c.Text); Assert.InRange(c.MaxDeviationPct, 0, 0.5); }
        var sep = new FrameModel();
        for (var i = 0; i < 8; i++) { var x = sep.AddMember(i * 6, 0, i * 6 + 5, 0, "IPE 200"); sep.AddLineLoad(x.Id, 10 + i); sep.Node(x.A)!.Support = "fest"; sep.Node(x.B)!.Support = "los"; }
        var s = FrameCrossCheck.Check(sep);
        Assert.Contains("zweite Rechnung (Teilstäbe)", s.Methods); Assert.True(s.Ok, s.Text); Assert.InRange(s.MaxDeviationPct, 0, 0.5);
        // jeder getrennte Träger: Feldmoment q·L²/8 aus der zweiten Rechnung
        var (_, mm, _) = FrameCrossCheck.Solve(sep);
        for (var i = 0; i < 8; i++) Assert.Equal((10 + i) * 25 / 8.0, mm[sep.Members[i].Id], 6);
    }

    [Fact(DisplayName = "T-0748-fix: Leistung – Gegenprobe für 200 Stäbe deutlich unter 1 s und unter 100 MB (vorher 9,8–11 s und 300–370 MB)")]
    public void Leistung_200_Staebe()
    {
        FrameCrossCheck.Check(FrameModel.Template("rahmen")); // Aufwärmen (JIT)
        foreach (var f in new[] { Beam(200, 4), Frame(10, 10) })
        {
            var before = GC.GetAllocatedBytesForCurrentThread(); var sw = System.Diagnostics.Stopwatch.StartNew();
            var c = FrameCrossCheck.Check(f);
            var secs = sw.Elapsed.TotalSeconds; var mb = (GC.GetAllocatedBytesForCurrentThread() - before) / 1048576.0;
            Assert.True(c.Ok, c.Text); Assert.Contains("zweite Rechnung (Teilstäbe)", c.Methods);
            Assert.True(secs < 1, $"{f.Members.Count} Stäbe: {secs:0.000} s");
            Assert.True(mb < 100, $"{f.Members.Count} Stäbe: {mb:0} MB angelegt");
        }
    }

    [Fact(DisplayName = "T-0748-fix: über der Speichergrenze ehrlich „Gegenprobe zu groß – nur Gleichgewicht geprüft“; Abbruch bei neuer Änderung")]
    public void Zu_gross_und_Abbruch()
    {
        var c = FrameCrossCheck.Check(Frame(3, 2), maxEnvelope: 100);
        Assert.True(c.TooLarge); Assert.Equal("Gegenprobe zu groß – nur Gleichgewicht geprüft", c.Text);
        Assert.DoesNotContain("zweite Rechnung (Teilstäbe)", c.Methods); Assert.Contains("zu groß", c.Skipped);
        using var cts = new CancellationTokenSource(); cts.Cancel();
        Assert.ThrowsAny<OperationCanceledException>(() => FrameCrossCheck.Check(Frame(3, 2), cts.Token));
        Assert.Equal("Gegenprobe läuft …", CrossCheckResult.Running.Text); Assert.False(CrossCheckResult.Running.Ok);
    }

    [Fact(DisplayName = "T-0748-fix: Temperatur, Setzung, Schräglager – Statuszeile sagt „nur Gleichgewicht geprüft“ statt „✓ < 0,01 %“")]
    public void Nur_Gleichgewicht()
    {
        var t = FrameModel.Template("einfeld"); t.AddTemperature(t.Members[0].Id, 20);
        var s = FrameModel.Template("einfeld"); s.Nodes.First(n => n.Support == "los").Settle = 10;
        var a = FrameModel.Template("einfeld"); a.Nodes.First(n => n.Support == "los").Angle = 30;
        foreach (var (f, why) in new[] { (t, "Temperaturlasten"), (s, "Lagersetzungen"), (a, "schräge Loslager") })
        {
            var c = FrameCrossCheck.Check(f);
            Assert.True(c.OnlyEquilibrium, why);
            Assert.StartsWith("Gegenprobe: nur Gleichgewicht geprüft", c.Text); Assert.Contains(why, c.Text);
            Assert.DoesNotContain("✓", c.Text); Assert.DoesNotContain("< 0,01", c.Text);
        }
    }
    // ---------- Zweite Prüfung behoben (T-0748-fix2) ----------
    static double Rel(double a, double b) => Math.Abs(a - b) / Math.Abs(b) * 100;

    [Theory(DisplayName = "T-0748-fix2: Einfeldträger 5 m, Einzellast 20 kN bei 1/3, 1/6, 0,5025, 0,0125 – angezeigtes max M = F·a·b/L (unter 0,01 %), Gegenprobe ✓")]
    [InlineData(1.0 / 3)] [InlineData(1.0 / 6)] [InlineData(0.5025)] [InlineData(0.0125)]
    public void Einfeld_Einzellast_genau(double at)
    {
        var f = new FrameModel(); var m = f.AddMember(0, 0, 5, 0, "IPE 200"); f.Node(m.A)!.Support = "fest"; f.Node(m.B)!.Support = "los"; f.AddPointLoad(m.Id, 20, at);
        double a = at * 5, b = 5 - a, exact = 20 * a * b / 5;
        var shown = Math.Abs(FrameSolver.Solve(f).Members[0].MaxM); // genau das, was Oberfläche und Exporte zeigen
        Assert.True(Rel(shown, exact) < 0.01, $"Anzeige {shown} statt {exact}");
        var c = FrameCrossCheck.Check(f); Assert.True(c.Ok, c.Text); Assert.StartsWith("Gegenprobe ✓", c.Text);
    }

    [Theory(DisplayName = "T-0748-fix2: beidseitig eingespannter Träger 6 m, 50 kN bei 1/3, 1/6, 0,5025, 0,0125 – max M = max(F·a·b²/L², F·a²·b/L², 2F·a²·b²/L³)")]
    [InlineData(1.0 / 3)] [InlineData(1.0 / 6)] [InlineData(0.5025)] [InlineData(0.0125)]
    public void Eingespannt_Einzellast_genau(double at)
    {
        var f = new FrameModel(); var m = f.AddMember(0, 0, 6, 0, "IPE 300"); f.Node(m.A)!.Support = "einspannung"; f.Node(m.B)!.Support = "einspannung"; f.AddPointLoad(m.Id, 50, at);
        double L = 6, a = at * L, b = L - a, F = 50;
        var exact = new[] { F * a * b * b / (L * L), F * a * a * b / (L * L), 2 * F * a * a * b * b / (L * L * L) }.Max();
        var shown = Math.Abs(FrameSolver.Solve(f).Members[0].MaxM);
        Assert.True(Rel(shown, exact) < 0.01, $"Anzeige {shown} statt {exact}");
        var c = FrameCrossCheck.Check(f); Assert.True(c.Ok, c.Text); Assert.StartsWith("Gegenprobe ✓", c.Text);
    }

    [Theory(DisplayName = "T-0748-fix2: Rahmen mit eingespannten Füßen (HEA 200/300, Riegel IPE 300, 6 m), 50 kN bei 1/3, 1/6, 0,5025, 0,0125 – angezeigtes max M = zweite Rechnung (unter 0,01 %), Gegenprobe ✓")]
    [InlineData(1.0 / 3, "HEA 200")] [InlineData(1.0 / 6, "HEA 200")] [InlineData(0.5025, "HEA 200")] [InlineData(0.5025, "HEA 300")] [InlineData(0.0125, "HEA 200")]
    public void Rahmen_Einzellast_genau(double at, string column)
    {
        var f = new FrameModel(); f.AddMember(0, 0, 0, 4, column); var r = f.AddMember(0, 4, 6, 4, "IPE 300"); f.AddMember(6, 4, 6, 0, column);
        f.NodeAt(0, 0).Support = "einspannung"; f.NodeAt(6, 0).Support = "einspannung"; f.AddPointLoad(r.Id, 50, at);
        var shown = FrameSolver.Solve(f).Members.Select(x => Math.Abs(x.MaxM)).ToList();
        var (_, second, _) = FrameCrossCheck.Solve(f);
        for (var i = 0; i < 3; i++) Assert.True(Rel(shown[i], second[f.Members[i].Id]) < 0.01, $"S{i + 1}: Anzeige {shown[i]} statt {second[f.Members[i].Id]}");
        var c = FrameCrossCheck.Check(f); Assert.True(c.Ok, c.Text); Assert.StartsWith("Gegenprobe ✓", c.Text);
    }

    [Fact(DisplayName = "T-0748-fix2: Trapezlast 5 → 15 kN/m auf 5 m – Größtwert liegt nicht auf einem Messpunkt (Querkraft-Nullstelle), Anzeige trifft ihn genau")]
    public void Trapez_Extremstelle()
    {
        var f = FrameModel.Template("einfeld"); f.Loads.Clear(); f.AddTrapezLoad(f.Members[0].Id, 5, 15);
        double L = 5, qa = 5, dq = 10, RA = qa * L / 2 + dq * L / 6; // Lager links
        double M(double x) => RA * x - qa * x * x / 2 - dq * x * x * x / (6 * L);
        var x0 = (-qa + Math.Sqrt(qa * qa + 2 * dq / L * RA)) / (dq / L); // M'(x) = 0
        var shown = Math.Abs(FrameSolver.Solve(f).Members[0].MaxM);
        Assert.True(Rel(shown, M(x0)) < 0.01, $"Anzeige {shown} statt {M(x0)} bei x = {x0:0.###} m");
    }
}
