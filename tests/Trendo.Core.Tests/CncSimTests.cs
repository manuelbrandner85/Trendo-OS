using System.Text.Json.Nodes;
using Xunit;
namespace Trendo.Core.Tests;

/// <summary>T-0266: Werkzeugweg für Vorschau und Simulation – gleiche Länge und Zeit wie die Prüfung, Zeilenzuordnung, Bögen, Zoll, relativ.</summary>
public class T0266_CncSim
{
    [Fact(DisplayName = "T-0266: Segmente – Länge/Zeit stimmen mit Cnc.Analyse überein, jede Bewegung kennt ihre Zeile, Bogen ≈ Viertelkreis")]
    public void Segments_match_analyse()
    {
        var job = new CncJob(); var g = job.Pocket();
        var segs = CncSim.Segments(g); var r = Cnc.Analyse(g);
        Assert.Equal(r.CutMm, Math.Round(segs.Where(s => !s.Rapid).Sum(s => Math.Sqrt((s.X1 - s.X0) * (s.X1 - s.X0) + (s.Y1 - s.Y0) * (s.Y1 - s.Y0) + (s.Z1 - s.Z0) * (s.Z1 - s.Z0))), 1), 1);
        Assert.Equal(r.Time.TotalMinutes, segs.Sum(s => s.Minutes), 3);
        var lines = g.Replace("\r", "").Split('\n');
        Assert.All(segs, s => Assert.Matches("[XYZ]", lines[s.Line - 1].ToUpperInvariant()));
        // Viertelkreis R10 im Uhrzeigersinn, dann in Zoll relativ
        var arc = CncSim.Segments("G21 G90\nG0 X10 Y0\nG2 X0 Y-10 I-10 J0 F100\nG20 G91\nG1 X1 F10\n");
        var arcLen = arc.Where(s => s.Line == 3).Sum(s => Math.Sqrt((s.X1 - s.X0) * (s.X1 - s.X0) + (s.Y1 - s.Y0) * (s.Y1 - s.Y0)));
        Assert.InRange(arcLen, Math.PI * 5 - 0.1, Math.PI * 5 + 0.01);
        var last = arc[^1]; Assert.Equal(25.4, last.X1 - last.X0, 6); Assert.Equal(25.4 / 254, last.Minutes, 6);
        Assert.Equal(arc.Count - 1, CncSim.AtTime(arc, 1e9)); Assert.Equal(0, CncSim.AtTime(arc, 0));
    }

    [Fact(DisplayName = "T-0266: Auftrag speichern/laden, Generatoren liefern prüfbaren G-Code, Prüfbericht nennt Hinweise")]
    public void Job_and_report()
    {
        var j = new CncJob { W = 60, H = 30, Depth = 2, ToolD = 4, Material = "Holz" };
        var o = new JsonObject(); j.Gcode = j.Outline(); j.WriteTo(o);
        var back = CncJob.From(o); Assert.Equal(60, back.W); Assert.Equal("Holz", back.Material); Assert.Equal(j.Gcode, back.Gcode);
        foreach (var g in new[] { j.Pocket(), j.Outline(), j.Bolt(4) }) Assert.True(Cnc.Analyse(g).Moves > 4);
        var bad = Cnc.Analyse("G21 G90\nG0 X0 Y0 Z5\nG1 Z-1\nG0 X500 Y10 Z-2\n", 3000, (300, 300, 60));
        var rep = CncSim.Report("Test", bad, (300, 300, 60));
        Assert.Contains("ohne Vorschub", rep); Assert.Contains("Kollisionsgefahr", rep); Assert.Contains("Arbeitsraum", rep);
    }
}
