using System.Text.Json.Nodes;
using Trendo.Core.Elektro;
using Xunit;
namespace Trendo.Core.Tests;

/// <summary>T-0259: Schaltungssimulation gegen bekannte Werte (Ohm, Spannungsteiler, RC-Ladekurve, LED, Schwingkreis).</summary>
public class T0259_Circuit
{
    [Fact(DisplayName = "T-0259: Spannungsteiler 12 V an 10 kΩ/4,7 kΩ – Mitte 3,837 V, Strom 0,816 mA")]
    public void Divider()
    {
        var c = Circuit.Template("teiler"); var s = new Simulator(c).Run(0.001);
        var r2 = c.Parts.First(p => p.Name == "R2");
        Assert.Equal(12 * 4700 / 14700.0, Math.Abs(s.Parts[r2.Id].V), 4);
        Assert.Equal(12 / 14700.0, Math.Abs(s.Parts[r2.Id].I), 7);
        var src = c.Parts.First(p => p.Kind == "spannung");
        Assert.Equal(12 / 14700.0, Math.Abs(s.Parts[src.Id].I), 7);
    }

    [Fact(DisplayName = "T-0259: RC-Glied 1 kΩ/100 µF an 5 V – nach τ = 0,1 s etwa 63 %, nach 5τ voll; Schalter offen lädt nicht")]
    public void Rc_charging()
    {
        var c = Circuit.Template("rc"); var cap = c.Parts.First(p => p.Kind == "kondensator");
        var sim = new Simulator(c, 1e-4); var s = sim.Run(0.1);
        Assert.InRange(Math.Abs(s.Parts[cap.Id].V), 5 * 0.62, 5 * 0.645); // 1 − e⁻¹ = 63,2 % (implizites Euler etwas darunter)
        s = sim.Run(0.4); Assert.InRange(Math.Abs(s.Parts[cap.Id].V), 4.95, 5.0);
        c.Parts.First(p => p.Kind == "schalter").Closed = false;
        Assert.True(Math.Abs(new Simulator(c).Run(0.2).Parts[cap.Id].V) < 0.01);
    }

    [Fact(DisplayName = "T-0259: LED rot an 9 V mit 330 Ω – rund 2 V an der LED, ~21 mA; ohne Vorwiderstand meldet die Prüfung „brennt durch“")]
    public void Led()
    {
        var c = Circuit.Template("led"); var led = c.Parts.First(p => p.Kind == "led");
        var s = new Simulator(c).Run(0.002);
        Assert.InRange(s.Parts[led.Id].V, 1.8, 2.3);
        Assert.InRange(s.Parts[led.Id].I, 0.019, 0.023);
        Assert.Empty(Simulator.Check(c, s));
        c.Parts.First(p => p.Kind == "widerstand").Value = 10;
        var hot = new Simulator(c).Run(0.002);
        Assert.Contains(Simulator.Check(c, hot), m => m.Contains("brennt durch"));
    }

    [Fact(DisplayName = "T-0259: LC-Schwingkreis 1 H/15 µF schwingt mit f = 1/(2π√LC) ≈ 41 Hz; Gleichrichter lädt den Kondensator")]
    public void Oscillator_and_rectifier()
    {
        var c = Circuit.Template("schwingkreis"); var cap = c.Parts.First(p => p.Kind == "kondensator");
        var sim = new Simulator(c, 1e-5); var signs = new List<double>();
        double prev = 0; var crossings = new List<double>();
        for (var i = 0; i < 20000; i++) { var s = sim.Step(); var v = s.Parts[cap.Id].V - 10; if (i > 0 && Math.Sign(v) != Math.Sign(prev) && prev != 0) crossings.Add(s.Time); prev = v; }
        Assert.True(crossings.Count >= 6, $"nur {crossings.Count} Nulldurchgänge");
        var period = (crossings[^1] - crossings[1]) / ((crossings.Count - 2) / 2.0);
        Assert.InRange(1 / period, 38, 44);
        var g = Circuit.Template("gleichrichter"); var gc = g.Parts.First(p => p.Kind == "kondensator");
        var gs = new Simulator(g, 1e-4).Run(0.2);
        Assert.InRange(Math.Abs(gs.Parts[gc.Id].V), 9.5, 12); // Scheitel 12 V minus Diode, etwas Brummen
    }

    [Fact(DisplayName = "T-0259: Netz – Drähte und T-Abzweige verbinden; ohne Quelle Hinweis; SPICE-Netzliste und Stückliste; Speichern")]
    public void Netlist_spice_bom()
    {
        var c = new Circuit(); c.Add("draht", 0, 0, 10, 0); c.Add("widerstand", 5, 0, 5, 5, 100); c.Add("draht", 5, 5, 5, 8);
        var n = Netlist.Build(c);
        Assert.Equal(n.NodeOf(0, 0), n.NodeOf(5, 0)); Assert.Equal(n.NodeOf(5, 5), n.NodeOf(5, 8)); Assert.NotEqual(n.NodeOf(5, 0), n.NodeOf(5, 5));
        Assert.NotNull(new Simulator(c).Problem);
        var led = Circuit.Template("led");
        var spice = led.Spice();
        Assert.Contains("RR1 ", spice); Assert.Contains("DLED1 ", spice); Assert.Contains(".model DLED D(", spice); Assert.Contains("VV1 ", spice); Assert.EndsWith(".end\n", spice);
        Assert.Contains(" 0 ", spice); // Masse
        Assert.Contains("Widerstand;330,00 Ω;1;R1", led.BomCsv());
        var data = new JsonObject(); led.WriteTo(data);
        Assert.Equal(led.Parts.Count, Circuit.From(data).Parts.Count);
        Assert.Throws<ArgumentException>(() => led.Add("widerstand", 1, 1, 1, 1));
    }
}
