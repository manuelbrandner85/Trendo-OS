using Trendo.Core;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>SPEC-08: Stoffe, Zuschnitt mit echten Formen, Produktionsdateien.</summary>
public class GarmentProductionTests
{
    static readonly BodyMeasures M = new(168, 92, 74, 98, true);
    static Garment T => GarmentBlocks.Build("tshirt", M);

    static bool Inside(IReadOnlyList<PatPt> poly, double x, double y)
    {
        var hit = false;
        for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
            if (poly[i].Y > y != poly[j].Y > y && x < (poly[j].X - poly[i].X) * (y - poly[i].Y) / (poly[j].Y - poly[i].Y) + poly[i].X) hit = !hit;
        return hit;
    }

    [Fact(DisplayName = "Stoffe: alle Werte plausibel, unbekannt ergibt den ersten; Farben werden linear umgerechnet")]
    public void Fabric_values()
    {
        Assert.All(Fabrics.All, f => { Assert.InRange(f.Gsm, 40, 2000); Assert.InRange(f.WidthCm, 90, 200); Assert.True(f.PricePerM > 0); Assert.True(f.Mass > 0); });
        Assert.Equal(Fabrics.All.Length, Fabrics.All.Select(f => f.Id).Distinct().Count());
        Assert.Equal("jersey", Fabrics.Of("gibtsnicht").Id);
        var white = Fabrics.Linear("#FFFFFF"); Assert.Equal(1, white[0]);
        Assert.InRange(Fabrics.Linear("#808080")[0], 0.21, 0.22); // sRGB 50 % ist linear etwa 21,6 %
        Assert.Equal(Fabrics.Linear("#1E2F5C"), Fabrics.Linear("navy"));
    }

    [Fact(DisplayName = "Stoffe: Auftrag für Blender enthält Physik und Texturpfade")]
    public void Fabric_job()
    {
        var j = Fabrics.ToJob(Fabrics.Of("denim"), "Navy", "C:/tex");
        Assert.Equal(40d, (double)j["tension"]!);
        Assert.EndsWith("Fabric019_1K-JPG_NormalGL.jpg", (string)j["textures"]!["normal"]!);
        Assert.Null(Fabrics.ToJob(Fabrics.Of("denim"), "Navy", null)["textures"]);
    }

    [Fact(DisplayName = "Nahtzugabe: Schnittkante liegt außerhalb der Nahtlinie, am Saum breiter")]
    public void Allowance()
    {
        foreach (var p in T.Parts)
        {
            var cut = GarmentProduction.WithAllowance(p);
            Assert.Equal(p.Outline.Count, cut.Count);
            foreach (var q in p.Outline) Assert.True(Inside(cut, q.X, q.Y), p.Name);
        }
        var f = T.Parts[0]; var cutF = GarmentProduction.WithAllowance(f);
        Assert.Equal(-GarmentProduction.HemCm, cutF.Min(q => q.Y), 3);
    }

    [Theory(DisplayName = "Zuschnitt: echte Formen überlappen nicht und bleiben auf der Bahn")]
    [InlineData("tshirt", 150)]
    [InlineData("rock", 140)]
    public void Nest_no_overlap(string kind, double width)
    {
        var g = GarmentBlocks.Build(kind, M);
        var n = GarmentProduction.Nest(g, width);
        Assert.Equal(g.Parts.Sum(p => p.Qty), n.Placed.Count);
        Assert.InRange(n.Efficiency, 0.3, 1);
        var placed = n.Placed.Select(p => GarmentProduction.WithAllowance(g.Parts[p.Part]).Select(q => GarmentProduction.Place(q, p)).ToList()).ToList();
        foreach (var poly in placed) { Assert.InRange(poly.Min(q => q.X), -0.01, width); Assert.InRange(poly.Max(q => q.X), 0, width + 0.01); Assert.True(poly.Min(q => q.Y) >= -0.01); }
        for (var i = 0; i < placed.Count; i++)
            for (var j = i + 1; j < placed.Count; j++)
                Assert.DoesNotContain(placed[i], q => Inside(placed[j], q.X, q.Y));
    }

    [Fact(DisplayName = "Zuschnitt: echte Formen sparen Stoff gegenüber Rechtecken; zu schmale Bahn wird abgelehnt")]
    public void Nest_saves_fabric()
    {
        var g = T; var n = GarmentProduction.Nest(g, 150);
        var pieces = g.Parts.Select(p => new Marker.Piece(p.Name, p.Outline.Max(q => q.X) - p.Outline.Min(q => q.X), p.Outline.Max(q => q.Y) - p.Outline.Min(q => q.Y), p.Qty)).ToList();
        var rect = Marker.Length(pieces, Marker.AutoLayout(pieces, 150));
        Assert.True(n.LengthCm <= rect + 1, $"{n.LengthCm} gegen {rect}");
        Assert.Throws<ArgumentException>(() => GarmentProduction.Nest(g, 30));
    }

    [Fact(DisplayName = "Größen: größere Größe braucht nicht weniger Stoff; Kosten folgen dem Meterpreis")]
    public void Size_run()
    {
        var f = Fabrics.Of("jersey");
        var r = GarmentProduction.SizeRun("tshirt", M, ["S", "M", "L", "XL"], f);
        Assert.Equal(4, r.Count); Assert.True(r[^1].Meters >= r[0].Meters);
        Assert.All(r, x => Assert.Equal(Math.Round(x.Meters * f.PricePerM, 2), x.Cost));
    }

    [Fact(DisplayName = "DXF: Ebenen für Schnitt, Naht und Fadenlauf, Maße in Millimetern, gültiges Dateiende")]
    public void Dxf_file()
    {
        var d = GarmentProduction.Dxf(T);
        Assert.Contains("SCHNITT", d); Assert.Contains("NAHT", d); Assert.Contains("FADENLAUF", d);
        Assert.StartsWith("0\nSECTION", d); Assert.EndsWith("0\nEOF\n", d);
        Assert.Equal(5 * 3, System.Text.RegularExpressions.Regex.Matches(d, "POLYLINE").Count);
    }

    [Fact(DisplayName = "PDF: Schnittbogen auf mehreren A4-Seiten; Datenblatt nennt Stoff, Teile, Nähte und Größen")]
    public void Pdf_and_techpack()
    {
        var path = Path.Combine(Path.GetTempPath(), "trendo-garment-" + Guid.NewGuid().ToString("N") + ".pdf");
        try
        {
            var pages = GarmentProduction.PdfTiled(path, T, "Probe");
            Assert.True(pages >= 4, pages.ToString()); Assert.True(new FileInfo(path).Length > 1000);
        }
        finally { File.Delete(path); }
        var t = GarmentProduction.TechPack("Probe", T, M, Fabrics.Of("jersey"), "Navy", ["S", "M", "L"]);
        Assert.Contains("Baumwoll-Jersey", t); Assert.Contains("Vorderteil", t); Assert.Contains("Ärmel rechts (capF)", t); Assert.Contains("| L |", t);
    }
}
