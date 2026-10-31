using Trendo.Core.Arch;
using Xunit;
namespace Trendo.Core.Tests;

/// <summary>T-0273: Grundriss – Bogenwände (Kette kurzer Stücke, gemeinsam bearbeitbar) und Nordpfeil.</summary>
public class T0273_Arc
{
    [Fact(DisplayName = "T-0273: Halbkreis-Bogen 4 m Sehne, Stich 2 m – Länge π·r, Stücke schließen lückenlos an, Stich ändern und als Ganzes löschen")]
    public void Arc_wall()
    {
        var p = new FloorPlan();
        var g = p.AddArcWall(0, 0, 400, 0, 200);
        var segs = p.ArcWalls(g);
        Assert.InRange(segs.Count, 6, 48);
        Assert.Equal((0.0, 0.0), (segs[0].X1, segs[0].Y1)); Assert.Equal((400.0, 0.0), (segs[^1].X2, segs[^1].Y2));
        for (var i = 1; i < segs.Count; i++) { Assert.Equal(segs[i - 1].X2, segs[i].X1, 6); Assert.Equal(segs[i - 1].Y2, segs[i].Y1, 6); }
        Assert.Equal(200, FloorPlan.ArcRadius(segs[0]), 6);
        Assert.InRange(p.ArcLength(g), Math.PI * 200 * 0.99, Math.PI * 200); // Sehnenzug etwas kürzer als der Bogen
        Assert.All(segs, w => Assert.InRange(Math.Sqrt((w.X2 - 200) * (w.X2 - 200) + w.Y2 * w.Y2), 199.99, 200.01)); // alle Punkte auf dem Kreis
        Assert.True(segs.Max(w => -w.Y2) > 199); // Stich nach links (im Plan nach oben)
        p.SetArcBulge(g, 50);
        var flat = p.ArcWalls(g); Assert.Equal(425, FloorPlan.ArcRadius(flat[0]), 6); // r = (c²/4 + s²)/(2s)
        Assert.Equal((400.0, 0.0), (flat[^1].X2, flat[^1].Y2));
        Assert.Throws<ArgumentException>(() => p.SetArcBulge(g, 250)); // mehr als ein Halbkreis
        Assert.True(p.Remove(flat[2].Id)); Assert.Empty(p.Walls);
        var q = new FloorPlan(); var g2 = q.AddArcWall(0, 0, 300, 0, -60); Assert.True(q.ArcWalls(g2).Max(w => w.Y2) > 59, string.Join(" ", q.ArcWalls(g2).Select(w => $"{w.X2:0}/{w.Y2:0}"))); // nach rechts gewölbt
    }

    [Fact(DisplayName = "T-0273: Nordpfeil steht im Plan und dreht sich mit der Nordrichtung; Bogen bleibt beim Speichern erhalten")]
    public void North_arrow_and_roundtrip()
    {
        var p = FloorPlan.Template("wohnung"); p.North = 90;
        var s = PlanDrawing.Build(p, PlanTheme.Paper);
        var arrow = s.OfType<DPoly>().Where(x => x.Layer == "NORDPFEIL").ToList(); Assert.Equal(2, arrow.Count);
        Assert.Contains(s.OfType<DText>(), t => t.Layer == "NORDPFEIL" && t.Text == "N");
        var tip = arrow[1].Pts[0]; var center = arrow[0].Pts.Aggregate((0.0, 0.0), (a, b) => (a.Item1 + b.X / arrow[0].Pts.Count, a.Item2 + b.Y / arrow[0].Pts.Count));
        Assert.True(tip.X > center.Item1 + 10, "bei 90° zeigt Norden nach rechts");
        var g = p.AddArcWall(0, -300, 300, -300, 80);
        var data = new System.Text.Json.Nodes.JsonObject(); p.WriteTo(data);
        var back = FloorPlan.From(data); Assert.Equal(90, back.North); Assert.Equal(p.ArcWalls(g).Count, back.ArcWalls(g).Count); Assert.Equal(80, back.ArcWalls(g)[0].ArcBulge);
    }
}
