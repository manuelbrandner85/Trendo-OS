using System.Text.Json.Nodes;
using Trendo.Core;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>T-0745: Schnittteile frei bearbeiten – Punkte, Kurven, Nahtzugabe, Maße, Speichern im Dokument.</summary>
public class GarmentEditTests
{
    static readonly BodyMeasures Body = new(170, 92, 74, 98, true);
    static Garment Shirt() => GarmentBlocks.Build("tshirt", Body);

    [Fact(DisplayName = "Zerlegen in Punkte und zurück: Umriss, Fläche und jede Kantenlänge bleiben auf 1 mm genau; Kurven werden zu wenigen Bézier-Stücken")]
    public void RoundTrip()
    {
        foreach (var (kind, _) in GarmentBlocks.Kinds)
            foreach (var p in GarmentBlocks.Build(kind, Body).Parts)
            {
                var n = GarmentEdit.ToNodes(p);
                Assert.True(n.Count <= p.Outline.Count, $"{kind}/{p.Name}: {n.Count} Punkte statt höchstens {p.Outline.Count}");
                var back = GarmentEdit.FromNodes(p, n);
                Assert.Equal(p.Edges.Select(e => e.Name), back.Edges.Select(e => e.Name));
                foreach (var e in p.Edges) Assert.True(Math.Abs(e.Length - back.Edge(e.Name).Length) < 0.1, $"{kind}/{p.Name}/{e.Name}: {e.Length} ↔ {back.Edge(e.Name).Length}");
                Assert.True(Math.Abs(p.Area - back.Area) / p.Area < 0.002, $"{kind}/{p.Name}: Fläche {p.Area} ↔ {back.Area}");
            }
        var front = Shirt().Parts[0];
        Assert.True(GarmentEdit.ToNodes(front).Count < front.Outline.Count / 3, "Kurven sollen wenige Punkte haben");
        Assert.Contains(GarmentEdit.ToNodes(front).Select((_, i) => i), i => GarmentEdit.IsCurve(GarmentEdit.ToNodes(front), i)); // Armloch und Hals sind Kurven
    }

    [Fact(DisplayName = "Punkt verschieben ändert Kante, Fläche und Nahtzugabe; Punkt einfügen ändert die Form nicht; Löschen einer Nahtecke wird erklärt")]
    public void EditOps()
    {
        var front = Shirt().Parts[0];
        var n = GarmentEdit.ToNodes(front);
        var hem = n.FindIndex(q => q.Edge == "hem"); var side = n.FindIndex(q => q.Edge == "sideR");
        // Saum 3 cm nach unten verlängern (beide Ecken)
        var moved = GarmentEdit.MoveNode(GarmentEdit.MoveNode(n, hem, n[hem].X, n[hem].Y - 3), side, n[side].X, n[side].Y - 3);
        var p2 = GarmentEdit.FromNodes(front, moved);
        Assert.Equal(front.Edge("sideR").Length + 3, p2.Edge("sideR").Length, 1);
        Assert.Equal(front.Edge("hem").Length, p2.Edge("hem").Length, 3);
        Assert.True(p2.Area > front.Area + 3 * front.Edge("hem").Length * 0.95);
        var cut1 = GarmentProduction.WithAllowance(front); var cut2 = GarmentProduction.WithAllowance(p2);
        Assert.Equal(cut1.Min(q => q.Y) - 3, cut2.Min(q => q.Y), 3); // Saumzugabe folgt der neuen Kante

        // Einfügen auf einer Kurve: gleiche Form, ein Punkt mehr
        var curveSeg = Enumerable.Range(0, n.Count).First(i => GarmentEdit.IsCurve(n, i));
        var (ins, at) = GarmentEdit.Insert(n, curveSeg, 0.4);
        Assert.Equal(n.Count + 1, ins.Count); Assert.Null(ins[at].Edge);
        var p3 = GarmentEdit.FromNodes(front, ins);
        foreach (var e in front.Edges) Assert.Equal(GarmentEdit.FromNodes(front, n).Edge(e.Name).Length, p3.Edge(e.Name).Length, 2);

        // eingefügten Punkt ziehen, dann löschen
        var bent = GarmentEdit.MoveNode(ins, at, ins[at].X + 2, ins[at].Y);
        Assert.NotEqual(p3.Area, GarmentEdit.FromNodes(front, bent).Area);
        var (del, problem) = GarmentEdit.Delete(bent, at);
        Assert.Null(problem); Assert.Equal(n.Count, del!.Count);

        // Ecke mit Naht ohne Zwischenpunkt: nicht löschbar, mit Erklärung
        var (none, why) = GarmentEdit.Delete(n, n.FindIndex(q => q.Edge == "shR"));
        Assert.Null(none); Assert.Contains("Schulter rechts", why);
        // mit Zwischenpunkt: Kante beginnt am nächsten Punkt
        var sh = n.FindIndex(q => q.Edge == "shR");
        var (withMid, _) = GarmentEdit.Insert(n, sh, 0.5);
        var (ok, _) = GarmentEdit.Delete(withMid, sh);
        Assert.NotNull(ok); Assert.Equal("shR", ok![sh].Edge);
        GarmentEdit.FromNodes(front, ok); // bleibt gültig
    }

    [Fact(DisplayName = "Gerade ↔ Kurve: Griffe entstehen und verschwinden; Griff ziehen biegt die Kante, glatte Punkte bleiben glatt")]
    public void CurveOps()
    {
        var front = Shirt().Parts[0];
        var n = GarmentEdit.ToNodes(front);
        var hem = n.FindIndex(q => q.Edge == "hem");
        Assert.False(GarmentEdit.IsCurve(n, hem));
        var c = GarmentEdit.SetCurve(n, hem, true);
        Assert.True(GarmentEdit.IsCurve(c, hem));
        var bent = GarmentEdit.MoveHandle(c, hem, true, c[hem].N.OutX, c[hem].Y - 8);
        var p = GarmentEdit.FromNodes(front, bent);
        Assert.True(p.Edge("hem").Length > front.Edge("hem").Length + 0.3);
        Assert.True(p.Edge("hem").Points.Count > 2);
        var flat = GarmentEdit.SetCurve(bent, hem, false);
        Assert.False(GarmentEdit.IsCurve(flat, hem));
        Assert.Equal(front.Edge("hem").Length, GarmentEdit.FromNodes(front, flat).Edge("hem").Length, 3);
        // glatter Punkt: Gegengriff dreht mit
        var smooth = Enumerable.Range(0, n.Count).First(i => GarmentEdit.IsSmooth(n[i].N));
        var turned = GarmentEdit.MoveHandle(n, smooth, true, n[smooth].N.OutX + 1, n[smooth].N.OutY + 1);
        Assert.True(GarmentEdit.IsSmooth(turned[smooth].N));
    }

    [Fact(DisplayName = "Im Dokument gespeichert: Bearbeitung je Schnittart und Teil, Zurücksetzen, kaputte Daten fallen auf die Vorlage zurück; Nahtlängen sichtbar")]
    public void Storage()
    {
        var g = Shirt(); var front = g.Parts[0];
        var n = GarmentEdit.ToNodes(front); var hem = n.FindIndex(q => q.Edge == "hem");
        var moved = GarmentEdit.MoveNode(n, hem, n[hem].X - 2, n[hem].Y);
        var edits = GarmentEdit.With(null, "tshirt", front.Name, moved);
        var json = JsonNode.Parse(edits.ToJsonString())!.AsObject(); // wie nach Speichern und Laden
        var g2 = GarmentEdit.Apply(g, json);
        Assert.Equal(front.Edge("hem").Length + 2, g2.Parts[0].Edge("hem").Length, 2);
        Assert.Same(g.Parts[1], g2.Parts[1]);
        Assert.Equal(["Vorderteil"], GarmentEdit.Edited(json, "tshirt"));
        Assert.Same(g, GarmentEdit.Apply(g, GarmentEdit.With(json, "tshirt", front.Name, null))); // zurückgesetzt
        var broken = GarmentEdit.Apply(g, new JsonObject { ["tshirt"] = new JsonObject { ["Vorderteil"] = new JsonArray(1, 2) } });
        Assert.Same(g.Parts[0], broken.Parts[0]);
        var wrongEdges = GarmentEdit.Apply(g, GarmentEdit.With(null, "tshirt", "Vorderteil", n.Select(q => q with { Edge = q.Edge == "hem" ? "xyz" : q.Edge }).ToList()));
        Assert.Same(g.Parts[0], wrongEdges.Parts[0]);
        var seams = GarmentEdit.SeamLengths(g2);
        var right = seams.First(s => s.Seam.EdgeA == "sideR"); var left = seams.First(s => s.Seam.EdgeA == "sideL");
        Assert.True(Math.Abs(right.A - right.B) < 0.01); // unberührt
        Assert.True(Math.Abs(left.A - left.B) > 0.01); // die verschobene Saumecke verlängert die linke Seitennaht nur vorn – das zeigt der Nahtvergleich
        var half = front.Edge("hem").Length / 2;
        var crossed = GarmentEdit.FromNodes(front, GarmentEdit.MoveNode(n, hem, half + 10, 30));
        Assert.True(GarmentEdit.SelfIntersects(crossed), string.Join(" ", crossed.Outline.Select(q => $"{q.X:0.#},{q.Y:0.#}")));
        Assert.False(GarmentEdit.SelfIntersects(front));
    }

    [Fact(DisplayName = "Export mit bearbeiteten Teilen: DXF und SVG enthalten den neuen Umriss in mm")]
    public void ExportUsesEdits()
    {
        var g = Shirt(); var front = g.Parts[0];
        var n = GarmentEdit.ToNodes(front); var hem = n.FindIndex(q => q.Edge == "hem"); var side = n.FindIndex(q => q.Edge == "sideR");
        var g2 = GarmentEdit.Apply(g, GarmentEdit.With(null, "tshirt", front.Name, GarmentEdit.MoveNode(GarmentEdit.MoveNode(n, hem, n[hem].X, n[hem].Y - 10), side, n[side].X, n[side].Y - 10)));
        Assert.NotEqual(GarmentProduction.Dxf(g), GarmentProduction.Dxf(g2));
        var svg = GarmentProduction.Svg(g2);
        Assert.StartsWith("<?xml", svg);
        Assert.Contains("mm\"", svg);
        Assert.Contains("Vorderteil", svg);
        var x = System.Xml.Linq.XDocument.Parse(svg); // gültiges XML
        Assert.True(x.Descendants().Count(e => e.Name.LocalName == "polygon") >= 2 * g.Parts.Count);
    }
}
