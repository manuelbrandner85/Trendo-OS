using System.IO.Compression;
using Trendo.Core.Elektro;
using Xunit;
namespace Trendo.Core.Tests;

/// <summary>T-0277: Platinen-Layout – Gehäuse, Luftlinien, Autorouter, Prüfung, Gerber/Bohrdaten.</summary>
public class T0277_Pcb
{
    [Fact(DisplayName = "T-0277: LED-Schaltung – 3 Bauteile platziert, 3 Netze ergeben 3 Luftlinien, Autorouter verlegt alle ohne Konflikt")]
    public void Place_ratsnest_route()
    {
        var c = Circuit.Template("led"); Pcb.AutoPlace(c);
        Assert.Equal(3, c.Pcb.Places.Count);
        var pads = Pcb.Pads(c); Assert.Equal(6, pads.Count); Assert.All(pads, p => Assert.True(p.Net >= 0));
        Assert.All(c.Pcb.Places.Values, v => Assert.Equal(0, Math.Abs(v.X / 1.27 - Math.Round(v.X / 1.27)), 6)); // Raster 1,27 mm
        Assert.Equal(3, Pcb.Ratsnest(c).Count);
        var (ok, open) = Pcb.AutoRoute(c); Assert.Equal(3, ok); Assert.Equal(0, open);
        Assert.Empty(Pcb.Ratsnest(c)); Assert.Empty(Pcb.Check(c));
        Assert.All(c.Pcb.Traces, t => Assert.All(t.Pts.Zip(t.Pts.Skip(1)), s => Assert.True(s.First[0] == s.Second[0] || s.First[1] == s.Second[1]))); // nur waagerecht/senkrecht
    }

    [Fact(DisplayName = "T-0277: Prüfung meldet Bahn über fremdem Anschluss und Bauteil außerhalb; Drehen um 90° tauscht die Pad-Lage; Transistor und OPV haben Gehäuse")]
    public void Drc_rotate_footprints()
    {
        var c = Circuit.Template("led"); Pcb.AutoPlace(c); var pads = Pcb.Pads(c);
        var a = pads[0]; var other = pads.First(p => p.Net != a.Net);
        c.Pcb.Traces.Add(new PcbTrace { Net = a.Net, Pts = [[a.X, a.Y], [other.X, other.Y]] });
        Assert.Contains(Pcb.Check(c), m => m.StartsWith("Bahn berührt Anschluss"));
        var r = c.Parts.First(p => p.Kind == "widerstand"); var before = Pcb.Pads(c).Where(p => p.Part == r.Id).ToList();
        c.Pcb.Places[r.Id].Rot = 90; var after = Pcb.Pads(c).Where(p => p.Part == r.Id).ToList();
        Assert.Equal(before[0].X - c.Pcb.Places[r.Id].X, after[0].Y - c.Pcb.Places[r.Id].Y, 6);
        c.Pcb.Places[r.Id].X = 500; Assert.Contains(Pcb.Check(c), m => m.Contains("außerhalb"));
        Assert.Equal(3, Footprints.For("npn")!.Pads.Count); Assert.Equal(8, Footprints.For("opv")!.Pads.Count);
        var amp = Circuit.Template("verstaerker"); Pcb.AutoPlace(amp); var op = amp.Parts.Single(p => p.Kind == "opv");
        Assert.Equal(3, Pcb.Pads(amp).Count(p => p.Part == op.Id && p.Net >= 0)); // Pins 2, 3, 6 verbunden
    }

    [Fact(DisplayName = "T-0277: Fertigungsdaten – Gerber mit Format/Einheit, Pads als Blitz, Bahnen, Umriss; Bohrdatei mit Werkzeugen; ZIP mit 5 Dateien")]
    public void Gerber_drill_zip()
    {
        var c = Circuit.Template("led"); Pcb.AutoPlace(c); Pcb.AutoRoute(c);
        var cu = Pcb.Gerber(c, "F_Cu"); Assert.StartsWith("G04", cu); Assert.Contains("%FSLAX46Y46*%", cu); Assert.Contains("%MOMM*%", cu); Assert.EndsWith("M02*\n", cu);
        Assert.Equal(6, cu.Split('\n').Count(l => l.EndsWith("D03*")));
        Assert.True(cu.Split('\n').Count(l => l.EndsWith("D01*")) + Pcb.Gerber(c, "B_Cu").Split('\n').Count(l => l.EndsWith("D01*")) >= 3);
        Assert.Contains($"X{(long)(c.Pcb.W * 1e6)}Y0D01*", Pcb.Gerber(c, "Edge_Cuts"));
        var drl = Pcb.Drill(c); Assert.StartsWith("M48", drl); Assert.Contains("T1C0.800", drl); Assert.Contains("T2C1.200", drl); Assert.EndsWith("M30\n", drl);
        var zip = Path.Combine(Path.GetTempPath(), "trendo-pcb-" + Guid.NewGuid().ToString("N")[..6] + ".zip");
        try { Pcb.WriteFabZip(c, zip); using var z = ZipFile.OpenRead(zip); Assert.Equal(5, z.Entries.Count); Assert.Contains(z.Entries, e => e.Name.EndsWith(".drl")); }
        finally { File.Delete(zip); }
        var data = new System.Text.Json.Nodes.JsonObject(); c.WriteTo(data); var back = Circuit.From(data);
        Assert.Equal(c.Pcb.Places.Count, back.Pcb.Places.Count); Assert.Equal(c.Pcb.Traces.Count, back.Pcb.Traces.Count);
    }
}
