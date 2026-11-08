using SpiceSharp.Components;
using SpiceSharp.Simulations;
using Trendo.Core.Elektro;
using Xunit;
using Xunit.Abstractions;
using Sp = SpiceSharp.Circuit;

namespace Trendo.Core.Tests;

/// <summary>SPEC-02: Gegenprobe der eigenen Simulation mit SpiceSharp 3.2.3 (MIT, nur im Testprojekt, wird nicht ausgeliefert).
/// Beide rechnen dieselbe Schaltung mit denselben Bauteilwerten bei 300 K.</summary>
public class SpiceCrossCheckTests(ITestOutputHelper log)
{
    const double T = 300.0;

    static DiodeModel Dm(string name, double isat, double n) { var m = new DiodeModel(name); m.SetParameter("is", isat); m.SetParameter("n", n); m.SetParameter("tnom", T - 273.15); return m; }
    static BipolarJunctionTransistorModel Qm(bool npn, double beta)
    {
        var m = new BipolarJunctionTransistorModel("QM");
        m.SetParameter(npn ? "npn" : "pnp", true); m.SetParameter("is", 1e-14); m.SetParameter("bf", beta); m.SetParameter("br", 1.0); m.SetParameter("tnom", T - 273.15);
        return m;
    }

    static double[] Spice(Sp c, params (string P, string N)[] probes)
    {
        var op = new OP("op");
        op.BiasingParameters.Temperature = T; op.BiasingParameters.NominalTemperature = T;
        var ex = probes.Select(p => new RealVoltageExport(op, p.P, p.N)).ToArray();
        var res = new double[probes.Length];
        foreach (var _ in op.Run(c)) for (var i = 0; i < ex.Length; i++) res[i] = ex[i].Value;
        return res;
    }

    void Near(string what, double spice, double own, double absTol, double relTol = 1e-3)
    {
        var tol = Math.Max(absTol, Math.Abs(spice) * relTol);
        log.WriteLine($"{what}: SpiceSharp {spice:0.000000} · Trendo {own:0.000000} · Abweichung {Math.Abs(spice - own):0.000000}");
        Assert.True(Math.Abs(spice - own) <= tol, $"{what}: SpiceSharp {spice}, Trendo {own}");
    }

    static SimState Own(Circuit c) { var s = new Simulator(c); Assert.Null(s.Problem); return s.Run(0.002); }

    [Theory(DisplayName = "Gegenprobe SpiceSharp: Spannungsteiler")]
    [InlineData(12, 10000, 4700)]
    [InlineData(5, 330, 1000000)]
    public void Divider(double u, double r1, double r2)
    {
        var c = new Circuit(); c.Add("spannung", 0, 8, 0, 0, u); c.Add("draht", 0, 0, 6, 0); var a = c.Add("widerstand", 6, 0, 6, 4, r1); var b = c.Add("widerstand", 6, 4, 6, 8, r2); c.Add("draht", 6, 8, 0, 8); c.Add("masse", 0, 8, 0, 9);
        var own = Own(c);
        var sp = Spice(new Sp(new VoltageSource("V1", "in", "0", u), new Resistor("R1", "in", "m", r1), new Resistor("R2", "m", "0", r2)), ("in", "m"), ("m", "0"));
        Near("R1", sp[0], own.Parts[a.Id].V, 1e-5); Near("R2", sp[1], own.Parts[b.Id].V, 1e-5);
    }

    [Theory(DisplayName = "Gegenprobe SpiceSharp: Diode und LED mit Vorwiderstand, Durchlass und Sperrrichtung")]
    [InlineData("diode", 1, 1000)]
    [InlineData("diode", 5, 1000)]
    [InlineData("diode", 12, 220)]
    [InlineData("diode", 0.3, 1000)]
    [InlineData("diode", -5, 1000)]
    [InlineData("led", 9, 330)]
    [InlineData("led", 3, 100)]
    public void Diodes(string kind, double u, double r)
    {
        var c = new Circuit(); c.Add("spannung", 0, 6, 0, 0, u); c.Add("draht", 0, 0, 4, 0); var rp = c.Add("widerstand", 4, 0, 8, 0, r); var d = c.Add(kind, 8, 0, 8, 6); c.Add("draht", 8, 6, 0, 6); c.Add("masse", 0, 6, 0, 7);
        var own = Own(c);
        var (isat, n) = kind == "led" ? (1e-19, 2.0) : (2.52e-9, 1.752);
        var sp = Spice(new Sp(new VoltageSource("V1", "in", "0", u), new Resistor("R1", "in", "a", r), new Diode("D1", "a", "0", "DM"), Dm("DM", isat, n)), ("in", "a"), ("a", "0"));
        Near($"{kind} {u} V: Widerstand", sp[0], own.Parts[rp.Id].V, 2e-4); Near($"{kind} {u} V: Diode", sp[1], own.Parts[d.Id].V, 2e-4);
    }

    [Theory(DisplayName = "Gegenprobe SpiceSharp: NPN gesättigt und im Verstärkerbereich")]
    [InlineData(5, 1000, 10000, 100)]
    [InlineData(9, 470, 22000, 150)]
    [InlineData(12, 1000, 1000000, 100)]
    [InlineData(5, 1000, 470000, 200)]
    public void Npn(double u, double rc, double rb, double beta)
    {
        var c = new Circuit(); c.Add("spannung", 0, 10, 0, 0, u); c.Add("draht", 0, 0, 10, 0); c.Add("draht", 10, 0, 16, 0);
        var rcp = c.Add("widerstand", 10, 0, 10, 6, rc); var q = c.Add("npn", 10, 6, 10, 10, beta); var rbp = c.Add("widerstand", 16, 0, 12, 8, rb);
        c.Add("draht", 10, 10, 0, 10); c.Add("masse", 0, 10, 0, 11);
        var own = Own(c);
        var sp = Spice(new Sp(new VoltageSource("V1", "vcc", "0", u), new Resistor("RC", "vcc", "c", rc), new Resistor("RB", "vcc", "b", rb), new BipolarJunctionTransistor("Q1", "c", "b", "0", "0", "QM"), Qm(true, beta)), ("vcc", "c"), ("vcc", "b"), ("c", "0"));
        Near("RC", sp[0], own.Parts[rcp.Id].V, 5e-4); Near("RB", sp[1], own.Parts[rbp.Id].V, 5e-4); Near("Uce", sp[2], own.Parts[q.Id].V, 5e-4);
    }

    [Theory(DisplayName = "Gegenprobe SpiceSharp: PNP als Schalter nach Plus")]
    [InlineData(5, 1000, 10000, 100)]
    [InlineData(12, 1000, 1000000, 100)]
    public void Pnp(double u, double rc, double rb, double beta)
    {
        var c = new Circuit(); c.Add("spannung", 0, 10, 0, 0, u); c.Add("draht", 0, 0, 10, 0);
        var q = c.Add("pnp", 10, 6, 10, 0, beta); var rbp = c.Add("widerstand", 7, 3, 7, 10, rb); var rcp = c.Add("widerstand", 10, 6, 10, 10, rc);
        c.Add("draht", 0, 10, 7, 10); c.Add("draht", 7, 10, 10, 10); c.Add("masse", 0, 10, 0, 11);
        var own = Own(c);
        var sp = Spice(new Sp(new VoltageSource("V1", "vcc", "0", u), new Resistor("RC", "c", "0", rc), new Resistor("RB", "b", "0", rb), new BipolarJunctionTransistor("Q1", "c", "b", "vcc", "0", "QM"), Qm(false, beta)), ("c", "0"), ("b", "0"), ("c", "vcc"));
        Near("RC", sp[0], own.Parts[rcp.Id].V, 5e-4); Near("RB", sp[1], own.Parts[rbp.Id].V, 5e-4); Near("Uce", sp[2], own.Parts[q.Id].V, 5e-4);
    }

    void CompareAc(Circuit own, (int X, int Y) probe, Sp spice, string node)
    {
        var pts = AcAnalysis.Sweep(own, probe);
        var ac = new AC("ac", new DecadeSweep(10, 100_000, 15));
        ac.BiasingParameters.Temperature = T; ac.BiasingParameters.NominalTemperature = T;
        var ex = new ComplexVoltageExport(ac, node);
        var values = new List<(double F, System.Numerics.Complex V)>();
        foreach (var code in ac.Run(spice)) if (code == AC.ExportSmallSignal) values.Add((ac.Frequency, ex.Value));
        Assert.Equal(pts.Count, values.Count);
        var worstDb = 0.0; var worstDeg = 0.0;
        for (var i = 0; i < pts.Count; i++)
        {
            Assert.Equal(values[i].F, pts[i].F, values[i].F * 1e-6);
            var db = 20 * Math.Log10(values[i].V.Magnitude);
            var dDb = Math.Abs(db - pts[i].MagDb); var dDeg = Math.Abs(Math.IEEERemainder(values[i].V.Phase * 180 / Math.PI - pts[i].PhaseDeg, 360));
            worstDb = Math.Max(worstDb, dDb); worstDeg = Math.Max(worstDeg, dDeg);
        }
        log.WriteLine($"AC {values.Count} Punkte: größte Abweichung {worstDb:0.000000} dB, {worstDeg:0.000000}°");
        Assert.InRange(worstDb, 0, 0.01); Assert.InRange(worstDeg, 0, 0.1);
    }

    static VoltageSource Ac1(string name, string p) { var v = new VoltageSource(name, p, "0", 0); v.SetParameter("acmag", 1.0); return v; }

    [Fact(DisplayName = "Gegenprobe SpiceSharp: Frequenzgang RC-Tiefpass")]
    public void Ac_rc()
    {
        var c = new Circuit(); c.Add("wechsel", 0, 6, 0, 0, 1); c.Add("widerstand", 0, 0, 6, 0, 1000); c.Add("kondensator", 6, 0, 6, 6, 100e-9); c.Add("draht", 6, 6, 0, 6); c.Add("masse", 0, 6, 0, 7);
        CompareAc(c, (6, 0), new Sp(Ac1("V1", "in"), new Resistor("R1", "in", "o", 1000), new Capacitor("C1", "o", "0", 100e-9)), "o");
    }

    [Fact(DisplayName = "Gegenprobe SpiceSharp: Frequenzgang RLC-Reihenschwingkreis")]
    public void Ac_rlc()
    {
        var c = new Circuit(); c.Add("wechsel", 0, 6, 0, 0, 1); c.Add("widerstand", 0, 0, 4, 0, 47); c.Add("spule", 4, 0, 8, 0, 10e-3); c.Add("kondensator", 8, 0, 8, 6, 1e-6); c.Add("draht", 8, 6, 0, 6); c.Add("masse", 0, 6, 0, 7);
        CompareAc(c, (8, 0), new Sp(Ac1("V1", "in"), new Resistor("R1", "in", "a", 47), new Inductor("L1", "a", "o", 10e-3), new Capacitor("C1", "o", "0", 1e-6)), "o");
    }

    [Fact(DisplayName = "Gegenprobe SpiceSharp: Frequenzgang Emitterschaltung mit Koppelkondensator")]
    public void Ac_common_emitter()
    {
        const double rc = 2200, rb = 470000, beta = 100, ck = 10e-6;
        var c = new Circuit();
        c.Add("spannung", 0, 10, 0, 0, 9); c.Add("draht", 0, 0, 10, 0); c.Add("draht", 10, 0, 16, 0);
        c.Add("widerstand", 10, 0, 10, 6, rc); c.Add("npn", 10, 6, 10, 10, beta); c.Add("widerstand", 16, 0, 12, 8, rb);
        c.Add("draht", 10, 10, 0, 10); c.Add("masse", 0, 10, 0, 11);
        c.Add("kondensator", 12, 8, 20, 8, ck); c.Add("wechsel", 20, 10, 20, 8, 0.001); c.Add("draht", 20, 10, 10, 10);
        CompareAc(c, (10, 6), new Sp(new VoltageSource("V1", "vcc", "0", 9), new Resistor("RC", "vcc", "c", rc), new Resistor("RB", "vcc", "b", rb),
            new BipolarJunctionTransistor("Q1", "c", "b", "0", "0", "QM"), Qm(true, beta), new Capacitor("CK", "b", "i", ck), Ac1("VI", "i")), "c");
    }
}
