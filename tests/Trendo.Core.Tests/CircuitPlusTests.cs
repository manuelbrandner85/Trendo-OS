using Trendo.Core.Elektro;
using Xunit;
namespace Trendo.Core.Tests;

/// <summary>T-0272: Schaltung – Transistor (NPN/PNP), Operationsverstärker, Frequenzgang gegen bekannte Formeln.</summary>
public class T0272_CircuitPlus
{
    [Fact(DisplayName = "T-0272: RC-Tiefpass 1 kΩ / 1 µF – Grenzfrequenz 1/(2πRC) ≈ 159 Hz, −20 dB je Dekade, Phase −45° an fg")]
    public void Rc_lowpass_bode()
    {
        var c = new Circuit();
        c.Add("masse", 0, 4, 0, 4); c.Add("wechsel", 0, 4, 0, 0, 1); c.Add("widerstand", 0, 0, 4, 0, 1000); c.Add("kondensator", 4, 0, 4, 4, 1e-6); c.Add("draht", 4, 4, 0, 4);
        var pts = AcAnalysis.Sweep(c, (4, 0), 1, 100_000, 101);
        var fc = AcAnalysis.Cutoff(pts)!.Value; Assert.InRange(fc, 150, 168); // 159,15 Hz
        Assert.InRange(pts[0].MagDb, -0.01, 0.01);
        var at = pts.MinBy(p => Math.Abs(p.F - 1_591.5))!; Assert.InRange(at.MagDb, -21, -19); Assert.InRange(pts.MinBy(p => Math.Abs(p.F - 15_915))!.MagDb, -41, -39); // −20 dB je Dekade
        Assert.InRange(pts.MinBy(p => Math.Abs(p.F - 159))!.PhaseDeg, -50, -40);
        Assert.NotEmpty(AcAnalysis.Draw(pts, 600, 300));
        Assert.Throws<InvalidOperationException>(() => AcAnalysis.Sweep(new Circuit { Parts = { new Part { Kind = "spannung", X1 = 0, Y1 = 0, X2 = 0, Y2 = 2, Value = 5 } } }, (0, 2)));
    }

    static Circuit NonInverting(double vin)
    {
        var c = new Circuit();
        c.Add("masse", 6, 6, 6, 6); c.Add("spannung", 6, 6, 6, -2, vin); c.Add("draht", 6, -2, 10, -2);
        var op = c.Add("opv", 10, 0, 14, 0, 12); Assert.Equal((10, -2), op.Pin3());
        c.Add("widerstand", 10, 0, 10, 6, 1000); c.Add("draht", 10, 6, 6, 6); c.Add("widerstand", 14, 0, 10, 0, 9000);
        return c;
    }

    [Fact(DisplayName = "T-0272: OPV nicht-invertierend, Verstärkung 1 + 9k/1k = 10 – 0,5 V → 5 V; 2 V → begrenzt auf die Versorgung ±12 V")]
    public void Opamp_gain_and_rail()
    {
        var s = new Simulator(NonInverting(0.5)).Step(); var c = NonInverting(0.5); var op = c.Parts.Single(p => p.Kind == "opv");
        Assert.Equal(5.0, new Simulator(c).Step().Parts[op.Id].V, 2);
        var c2 = NonInverting(2); var op2 = c2.Parts.Single(p => p.Kind == "opv");
        Assert.InRange(new Simulator(c2).Step().Parts[op2.Id].V, 11.5, 12.0);
    }

    static (Circuit C, Part T) CommonEmitter(double rb)
    {
        var c = new Circuit();
        c.Add("masse", 0, 10, 0, 10); c.Add("spannung", 0, 10, 0, 0, 5); c.Add("draht", 0, 0, 14, 0);
        c.Add("widerstand", 10, 0, 10, 4, 1000); var t = c.Add("npn", 10, 4, 10, 8, 100); Assert.Equal((12, 6), t.Pin3());
        c.Add("widerstand", 14, 0, 12, 6, rb); c.Add("draht", 10, 8, 10, 10); c.Add("draht", 10, 10, 0, 10);
        return (c, t);
    }

    [Fact(DisplayName = "T-0272: NPN – mit 1 MΩ Basiswiderstand Ic = β·Ib ≈ 0,43 mA (Verstärkerbetrieb), mit 10 kΩ gesättigt (Uce < 0,3 V, Ic ≈ 4,9 mA)")]
    public void Npn_active_and_saturated()
    {
        var (a, t) = CommonEmitter(1e6); var ra = new Simulator(a).Step().Parts[t.Id];
        Assert.InRange(ra.I, 0.40e-3, 0.46e-3); Assert.InRange(ra.V, 4.4, 4.7);
        var (b, u) = CommonEmitter(10_000); var rb = new Simulator(b).Step().Parts[u.Id];
        Assert.InRange(rb.V, 0, 0.3); Assert.InRange(rb.I, 4.6e-3, 5e-3);
    }

    [Fact(DisplayName = "T-0272: PNP mit Emitter an +5 V, Basis über 1 MΩ an Masse – Kollektorstrom ≈ 0,43 mA durch den Lastwiderstand")]
    public void Pnp_active()
    {
        var c = new Circuit();
        c.Add("masse", 0, 10, 0, 10); c.Add("spannung", 0, 10, 0, 0, 5); c.Add("draht", 0, 0, 10, 0);
        var t = c.Add("pnp", 10, 6, 10, 0, 100); Assert.Equal((7, 3), t.Pin3());
        c.Add("widerstand", 7, 3, 7, 10, 1e6); var rc = c.Add("widerstand", 10, 6, 10, 10, 1000); c.Add("draht", 10, 10, 0, 10);
        var s = new Simulator(c).Step();
        Assert.InRange(Math.Abs(s.Parts[rc.Id].I), 0.40e-3, 0.46e-3);
        Assert.InRange(Math.Abs(s.Parts[t.Id].I), 0.40e-3, 0.46e-3);
    }
}
