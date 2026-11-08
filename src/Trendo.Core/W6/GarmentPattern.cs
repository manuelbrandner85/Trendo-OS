using System.Text.Json.Nodes;

namespace Trendo.Core;

public sealed record PatPt(double X, double Y);

/// <summary>Ein benannter Abschnitt des Schnittrands, in Laufrichtung gegen den Uhrzeigersinn. Das Ende ist der Anfang des nächsten Abschnitts.</summary>
public sealed record PatEdge(string Name, IReadOnlyList<PatPt> Points)
{
    public double Length => Enumerable.Range(1, Points.Count - 1).Sum(i => Math.Sqrt(Math.Pow(Points[i].X - Points[i - 1].X, 2) + Math.Pow(Points[i].Y - Points[i - 1].Y, 2)));
}

/// <summary>Schnittteil in Zentimetern; Fadenlauf ist die Y-Achse.</summary>
public sealed record PatPart(string Name, string Role, IReadOnlyList<PatEdge> Edges, int Qty = 1)
{
    public IReadOnlyList<PatPt> Outline => Edges.SelectMany(e => e.Points.Take(e.Points.Count - 1)).ToList();
    public PatEdge Edge(string name) => Edges.First(e => e.Name == name);

    /// <summary>Fläche in cm² (Gaußsche Trapezformel).</summary>
    public double Area
    {
        get
        {
            var o = Outline; var a = 0.0;
            for (var i = 0; i < o.Count; i++) { var p = o[i]; var q = o[(i + 1) % o.Count]; a += p.X * q.Y - q.X * p.Y; }
            return Math.Abs(a) / 2;
        }
    }
}

/// <summary>Naht zwischen zwei Abschnitten. <paramref name="Reverse"/>: Der zweite Abschnitt läuft andersherum.</summary>
public sealed record PatSeam(string PartA, string EdgeA, string PartB, string EdgeB, bool Reverse);

public sealed record Garment(string Kind, string Label, IReadOnlyList<PatPart> Parts, IReadOnlyList<PatSeam> Seams);

public sealed record BodyMeasures(double Height, double Chest, double Waist, double Hip, bool Women);

/// <summary>SPEC-08: echte Grundschnitte aus Körpermaßen, mit benannten Kanten und Nähten. Eigene Konstruktion nach den üblichen Formeln der Schnittkonstruktion
/// (Viertelweite, Armloch aus Brustumfang). Dieselben Teile dienen für 2D-Schnitt, Zuschnitt und 3D-Anprobe.</summary>
public static class GarmentBlocks
{
    public static readonly (string Id, string Label)[] Kinds = [("tshirt", "T-Shirt"), ("rock", "A-Linien-Rock")];

    public static Garment Build(string kind, BodyMeasures b)
    {
        if (b.Chest is < 50 or > 180 || b.Waist is < 40 or > 180 || b.Hip is < 50 or > 200 || b.Height is < 120 or > 230)
            throw new ArgumentException("Die Maße passen nicht zu einem Menschen. Bitte Brust, Taille, Hüfte und Körpergröße in Zentimetern prüfen.");
        return kind switch
        {
            "tshirt" => TShirt(b),
            "rock" => Skirt(b),
            _ => throw new ArgumentException("Diesen Schnitt gibt es nicht."),
        };
    }

    /// <summary>Kubische Bézierkurve als Punktliste.</summary>
    public static List<PatPt> Bezier(PatPt a, PatPt c1, PatPt c2, PatPt b, int segments = 16)
    {
        var r = new List<PatPt>(segments + 1);
        for (var i = 0; i <= segments; i++)
        {
            var t = (double)i / segments; var u = 1 - t;
            r.Add(new(u * u * u * a.X + 3 * u * u * t * c1.X + 3 * u * t * t * c2.X + t * t * t * b.X, u * u * u * a.Y + 3 * u * u * t * c1.Y + 3 * u * t * t * c2.Y + t * t * t * b.Y));
        }
        return r;
    }

    static PatEdge Line(string name, PatPt a, PatPt b) => new(name, [a, b]);
    static PatEdge Mirror(string name, PatEdge e) => new(name, e.Points.Reverse().Select(p => new PatPt(-p.X, p.Y)).ToList());

    static PatPart Bodice(string name, string role, BodyMeasures b, double neckDrop, double armIn)
    {
        const double ease = 8;
        var half = (b.Chest + ease) / 4;
        var length = b.Height * 0.39;
        var neckHalf = b.Chest / 16 + 2;
        var shoulderX = Math.Min(half - 0.5, b.Chest / 8 + 8.5);
        var shoulderY = length - 4.5;
        var underY = length - (b.Chest / 6 + 7.5);
        PatPt hemL = new(-half, 0), hemR = new(half, 0), under = new(half, underY), shoulder = new(shoulderX, shoulderY), neck = new(neckHalf, length), cf = new(0, length - neckDrop);
        var armR = new PatEdge("armR", Bezier(under, new(half - armIn, underY + 1.5), new(shoulderX - 1, underY + (shoulderY - underY) * 0.55), shoulder));
        var shR = Line("shR", shoulder, neck);
        var right = Bezier(neck, new(neckHalf, length - neckDrop * 0.7), new(neckHalf * 0.55, length - neckDrop), cf, 10);
        var neckEdge = new PatEdge("neck", [.. right, .. right.AsEnumerable().Reverse().Skip(1).Select(p => new PatPt(-p.X, p.Y))]);
        return new(name, role, [Line("hem", hemL, hemR), Line("sideR", hemR, under), armR, shR, neckEdge, Mirror("shL", shR), Mirror("armL", armR), Line("sideL", new(-half, underY), hemL)]);
    }

    static PatPart Sleeve(string name, string role, double capLength, BodyMeasures b)
    {
        const double length = 22, capHeight0 = 0;
        var capHeight = b.Chest / 10 + 3 + capHeight0;
        // Halbe Armkugelbreite so suchen, dass die Armkugel 2 % länger ist als das Armloch (Mehrweite zum Einhalten).
        double low = 8, high = 40;
        Func<double, List<PatPt>> cap = w => Bezier(new(w, 0), new(w * 0.55, capHeight * 0.12), new(w * 0.45, capHeight), new(0, capHeight));
        double Len(List<PatPt> pts) => new PatEdge("", pts).Length;
        for (var i = 0; i < 40; i++) { var mid = (low + high) / 2; if (Len(cap(mid)) < capLength * 1.02) low = mid; else high = mid; }
        var hw = (low + high) / 2;
        var y0 = length;
        PatPt L(double x, double y) => new(x, y);
        var capF = cap(hw).Select(p => L(p.X, p.Y + y0)).ToList();
        var capB = capF.AsEnumerable().Reverse().Select(p => L(-p.X, p.Y)).ToList();
        var hemHalf = hw * 0.92;
        return new(name, role, [Line("hem", L(-hemHalf, 0), L(hemHalf, 0)), Line("underF", L(hemHalf, 0), capF[0]), new("capF", capF), new("capB", capB), Line("underB", capB[^1], L(-hemHalf, 0))]);
    }

    static Garment TShirt(BodyMeasures b)
    {
        var front = Bodice("Vorderteil", "front", b, b.Chest / 16 + 3.5, 3.6);
        var back = Bodice("Rückenteil", "back", b, 2, 2.2);
        var armAvg = (front.Edge("armR").Length + back.Edge("armR").Length) / 2;
        var right = Sleeve("Ärmel rechts", "sleeveR", armAvg, b);
        var left = Sleeve("Ärmel links", "sleeveL", armAvg, b);
        return new("tshirt", "T-Shirt", [front, back, right, left],
        [
            new("Vorderteil", "shR", "Rückenteil", "shR", false), new("Vorderteil", "shL", "Rückenteil", "shL", false),
            new("Vorderteil", "sideR", "Rückenteil", "sideR", false), new("Vorderteil", "sideL", "Rückenteil", "sideL", false),
            new("Ärmel rechts", "capF", "Vorderteil", "armR", false), new("Ärmel rechts", "capB", "Rückenteil", "armR", true), new("Ärmel rechts", "underF", "Ärmel rechts", "underB", true),
            new("Ärmel links", "capF", "Vorderteil", "armL", true), new("Ärmel links", "capB", "Rückenteil", "armL", false), new("Ärmel links", "underF", "Ärmel links", "underB", true),
        ]);
    }

    static PatPart SkirtPanel(string name, string role, BodyMeasures b, double length)
    {
        var waistHalf = (b.Waist + 2) / 4; var hipHalf = (b.Hip + 4) / 4; var hemHalf = hipHalf + length * 0.16;
        const double hipDepth = 19;
        var hipY = length - hipDepth;
        var sideR = Bezier(new(hemHalf, 0), new(hemHalf - (hemHalf - hipHalf) * 0.5, hipY * 0.5), new(hipHalf + 0.5, hipY + hipDepth * 0.35), new(waistHalf, length));
        return new(name, role, [Line("hem", new(-hemHalf, 0), new(hemHalf, 0)), new("sideR", sideR), Line("waist", new(waistHalf, length), new(-waistHalf, length)), Mirror("sideL", new("", sideR))]);
    }

    static Garment Skirt(BodyMeasures b)
    {
        var length = b.Height * 0.33;
        return new("rock", "A-Linien-Rock", [SkirtPanel("Vorderteil", "skirtFront", b, length), SkirtPanel("Rückenteil", "skirtBack", b, length)],
        [new("Vorderteil", "sideR", "Rückenteil", "sideR", false), new("Vorderteil", "sideL", "Rückenteil", "sideL", false)]);
    }

    /// <summary>Datensatz für die 3D-Anprobe in Blender (cm, Blender rechnet in Meter um).</summary>
    public static JsonObject ToJson(Garment g, BodyMeasures b) => new()
    {
        ["kind"] = g.Kind,
        ["body"] = new JsonObject { ["height"] = b.Height, ["chest"] = b.Chest, ["waist"] = b.Waist, ["hip"] = b.Hip, ["women"] = b.Women },
        ["parts"] = new JsonArray([.. g.Parts.Select(p => (JsonNode)new JsonObject
        {
            ["name"] = p.Name, ["role"] = p.Role,
            ["edges"] = new JsonArray([.. p.Edges.Select(e => (JsonNode)new JsonObject { ["name"] = e.Name, ["pts"] = new JsonArray([.. e.Points.Select(q => (JsonNode)new JsonArray(Math.Round(q.X, 3), Math.Round(q.Y, 3)))]) })]),
        })]),
        ["seams"] = new JsonArray([.. g.Seams.Select(s => (JsonNode)new JsonObject { ["a"] = s.PartA, ["ea"] = s.EdgeA, ["b"] = s.PartB, ["eb"] = s.EdgeB, ["rev"] = s.Reverse })]),
    };
}
