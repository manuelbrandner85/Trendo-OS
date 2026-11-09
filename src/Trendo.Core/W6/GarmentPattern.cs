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
    public static readonly (string Id, string Label)[] Kinds = [("tshirt", "T-Shirt"), ("pullover", "Pullover"), ("hemd", "Hemd"), ("kleid", "Kleid"), ("rock", "A-Linien-Rock"), ("hose", "Hose")];

    public static Garment Build(string kind, BodyMeasures b)
    {
        if (b.Chest is < 50 or > 180 || b.Waist is < 40 or > 180 || b.Hip is < 50 or > 200 || b.Height is < 120 or > 230)
            throw new ArgumentException("Die Maße passen nicht zu einem Menschen. Bitte Brust, Taille, Hüfte und Körpergröße in Zentimetern prüfen.");
        return kind switch
        {
            "tshirt" => TShirt(b),
            "rock" => Skirt(b),
            "kleid" => Dress(b),
            "pullover" => Top(b, "pullover", "Pullover", 12, b.Height * 0.38, ArmLength(b) - CuffHeight, "Halsbündchen", 3.5),
            "hemd" => Top(b, "hemd", "Hemd (ohne Knopfleiste in 3D)", 10, b.Height * 0.43, ArmLength(b) - CuffHeight, "Stehkragen", 3.5),
            "hose" => Trousers(b),
            _ => throw new ArgumentException("Diesen Schnitt gibt es nicht."),
        };
    }

    const double CuffHeight = 5;

    /// <summary>Schulter bis Handgelenk: etwa 33 % der Körpergröße.</summary>
    public static double ArmLength(BodyMeasures b) => b.Height * 0.33;

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

    static PatPart Bodice(string name, string role, BodyMeasures b, double neckDrop, double armIn, double ease = 8, double? bodyLength = null)
    {
        var half = (b.Chest + ease) / 4;
        var length = bodyLength ?? b.Height * 0.39;
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

    /// <summary>Ärmel. <paramref name="shoulderToHem"/>: Länge von der Schulter bis zur Ärmelkante in cm; die Armkugel zählt mit, sonst wird der Ärmel zu lang.</summary>
    static PatPart Sleeve(string name, string role, double capLength, BodyMeasures b, double? shoulderToHem = null)
    {
        var capHeight = b.Chest / 10 + 3;
        var length = Math.Max(4, (shoulderToHem ?? b.Height * 0.145) - capHeight);
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
        var hemHalf = length > 25 ? Math.Max(hw * 0.5, b.Chest * 0.1) : hw * 0.92;
        return new(name, role, [Line("hem", L(-hemHalf, 0), L(hemHalf, 0)), Line("underF", L(hemHalf, 0), capF[0]), new("capF", capF), new("capB", capB), Line("underB", capB[^1], L(-hemHalf, 0))]);
    }

    /// <summary>Ring aus Stoffstreifen, der an zwei Kanten angenäht wird (Halsbündchen, Rockbund). Die Enden werden zusammengenäht.</summary>
    static PatPart Band(string name, string role, double frontLength, double backLength, double height, double stretch)
    {
        var total = (frontLength + backLength) * stretch; var split = -total / 2 + frontLength * stretch;
        return new(name, role, [Line("seamF", new(-total / 2, 0), new(split, 0)), Line("seamB", new(split, 0), new(total / 2, 0)), Line("endR", new(total / 2, 0), new(total / 2, height)),
            Line("free", new(total / 2, height), new(-total / 2, height)), Line("endL", new(-total / 2, height), new(-total / 2, 0))]);
    }

    static PatPart Cuff(string name, string role, double length, double height) =>
        new(name, role, [Line("seam", new(-length / 2, 0), new(length / 2, 0)), Line("endR", new(length / 2, 0), new(length / 2, height)), Line("free", new(length / 2, height), new(-length / 2, height)), Line("endL", new(-length / 2, height), new(-length / 2, 0))]);

    static List<PatSeam> BodySeams(string f, string b) =>
        [new(f, "shR", b, "shR", false), new(f, "shL", b, "shL", false), new(f, "sideR", b, "sideR", false), new(f, "sideL", b, "sideL", false)];

    static List<PatSeam> SleeveSeams(string f, string b) =>
    [
        new("Ärmel rechts", "capF", f, "armR", false), new("Ärmel rechts", "capB", b, "armR", true), new("Ärmel rechts", "underF", "Ärmel rechts", "underB", true),
        new("Ärmel links", "capF", f, "armL", true), new("Ärmel links", "capB", b, "armL", false), new("Ärmel links", "underF", "Ärmel links", "underB", true),
    ];

    static List<PatSeam> BandSeams(string band, string f, string b, string edge) => [new(band, "seamF", f, edge, true), new(band, "seamB", b, edge, false), new(band, "endR", band, "endL", true)];

    static Garment TShirt(BodyMeasures b)
    {
        var front = Bodice("Vorderteil", "front", b, b.Chest / 16 + 3.5, 3.6);
        var back = Bodice("Rückenteil", "back", b, 2, 2.2);
        var armAvg = (front.Edge("armR").Length + back.Edge("armR").Length) / 2;
        var rib = Band("Halsbündchen", "neckRib", front.Edge("neck").Length, back.Edge("neck").Length, 2.2, 0.96);
        return new("tshirt", "T-Shirt", [front, back, Sleeve("Ärmel rechts", "sleeveR", armAvg, b), Sleeve("Ärmel links", "sleeveL", armAvg, b), rib],
            [.. BodySeams("Vorderteil", "Rückenteil"), .. SleeveSeams("Vorderteil", "Rückenteil"), .. BandSeams("Halsbündchen", "Vorderteil", "Rückenteil", "neck")]);
    }

    /// <summary>Oberteil mit langen Ärmeln, Manschetten und Halsabschluss.</summary>
    static Garment Top(BodyMeasures b, string kind, string label, double ease, double length, double sleeve, string collar, double collarHeight)
    {
        var front = Bodice("Vorderteil", "front", b, b.Chest / 16 + 3, 3.6, ease, length);
        var back = Bodice("Rückenteil", "back", b, 2, 2.2, ease, length);
        var armAvg = (front.Edge("armR").Length + back.Edge("armR").Length) / 2;
        var right = Sleeve("Ärmel rechts", "sleeveR", armAvg, b, sleeve); var left = Sleeve("Ärmel links", "sleeveL", armAvg, b, sleeve);
        var cuffLen = right.Edge("hem").Length;
        var band = Band(collar, "neckRib", front.Edge("neck").Length, back.Edge("neck").Length, collarHeight, 0.96);
        return new(kind, label, [front, back, right, left, band, Cuff("Manschette rechts", "cuffR", cuffLen, CuffHeight), Cuff("Manschette links", "cuffL", cuffLen, CuffHeight)],
        [
            .. BodySeams("Vorderteil", "Rückenteil"), .. SleeveSeams("Vorderteil", "Rückenteil"), .. BandSeams(collar, "Vorderteil", "Rückenteil", "neck"),
            new("Manschette rechts", "seam", "Ärmel rechts", "hem", false), new("Manschette rechts", "endR", "Manschette rechts", "endL", true),
            new("Manschette links", "seam", "Ärmel links", "hem", false), new("Manschette links", "endR", "Manschette links", "endL", true),
        ]);
    }

    /// <summary>Glatte Kurve durch Stützpunkte (Catmull-Rom).</summary>
    static List<PatPt> Smooth(IReadOnlyList<PatPt> k, int perSpan = 8)
    {
        var r = new List<PatPt> { k[0] };
        for (var i = 0; i < k.Count - 1; i++)
        {
            PatPt p0 = k[Math.Max(i - 1, 0)], p1 = k[i], p2 = k[i + 1], p3 = k[Math.Min(i + 2, k.Count - 1)];
            for (var s = 1; s <= perSpan; s++)
            {
                var t = (double)s / perSpan;
                double F(double a, double b2, double c, double d) => 0.5 * (2 * b2 + (-a + c) * t + (2 * a - 5 * b2 + 4 * c - d) * t * t + (-a + 3 * b2 - 3 * c + d) * t * t * t);
                r.Add(new(F(p0.X, p1.X, p2.X, p3.X), F(p0.Y, p1.Y, p2.Y, p3.Y)));
            }
        }
        return r;
    }

    static PatPart DressPanel(string name, string role, BodyMeasures b, double neckDrop, double armIn)
    {
        var total = b.Height * 0.60; const double ease = 6;
        var plain = Bodice(name, role, b, neckDrop, armIn, ease, total);
        var underY = plain.Edge("sideR").Points[^1].Y;
        double waistY = total - b.Height * 0.235, hipY = total - b.Height * 0.33;
        double chest = (b.Chest + ease) / 4, waist = (b.Waist + 4) / 4, hip = (b.Hip + 4) / 4, hem = hip + hipY * 0.14;
        var side = Smooth([new(hem, 0), new(hip, hipY), new(waist, waistY), new(chest, underY)]);
        var edges = plain.Edges.ToList();
        edges[0] = Line("hem", new(-hem, 0), new(hem, 0));
        edges[1] = new("sideR", side);
        edges[^1] = Mirror("sideL", new("", side));
        return plain with { Edges = edges };
    }

    static Garment Dress(BodyMeasures b) => new("kleid", "Kleid", [DressPanel("Vorderteil", "front", b, b.Chest / 16 + 5, 3.8), DressPanel("Rückenteil", "back", b, 2.5, 2.4)], BodySeams("Vorderteil", "Rückenteil"));

    /// <summary>Hose aus vier Teilen: je Bein vorn und hinten, Bund als eigener Ring.</summary>
    static PatPart TrouserPanel(string name, string role, BodyMeasures b, double sign, bool backPart)
    {
        var waistToFloor = b.Height * 0.60; var crotchDepth = b.Height * 0.155; var inseam = waistToFloor - crotchDepth - 2;
        var y0 = 0.0; var yCrotch = inseam; var yHip = yCrotch + crotchDepth * 0.42; var yTop = waistToFloor - 2;
        var kneeHalf = b.Hip * 0.105; var hemHalf = b.Hip * 0.095;
        var hipSide = (b.Hip + 4) / 4; var waistSide = (b.Waist + 2) / 4;
        var crotchExt = (b.Hip / 4) * (backPart ? 0.42 : 0.2);
        // x: + = aussen; Beinmitte bei x = 0, Schrittkante nach innen (-)
        var outerK = Smooth([new(hemHalf, y0), new(kneeHalf, y0 + inseam * 0.48), new(hipSide * 0.62, yCrotch), new(hipSide * 0.66, yHip), new(waistSide * 0.62, yTop)]);
        var innerK = Smooth([new(-hemHalf, y0), new(-kneeHalf, y0 + inseam * 0.48), new(-hipSide * 0.42 - crotchExt, yCrotch)]);
        var crotchK = BezierInner(new(-hipSide * 0.42 - crotchExt, yCrotch), new(-hipSide * 0.42, yHip), new(-waistSide * 0.38, yTop));
        PatPt M(PatPt p) => new(p.X * sign, p.Y);
        List<PatPt> Mm(IEnumerable<PatPt> l) => [.. l.Select(M)];
        // gegen den Uhrzeigersinn; bei sign < 0 ist die Lage gespiegelt, darum Kanten umdrehen
        if (sign > 0)
            return new(name, role, [Line("hem", M(new(-hemHalf, 0)), M(new(hemHalf, 0))), new("outer", Mm(outerK)), Line("waist", M(new(waistSide * 0.62, yTop)), M(new(-waistSide * 0.38, yTop))), new("crotch", Mm(crotchK.AsEnumerable().Reverse())), new("inner", Mm(innerK.AsEnumerable().Reverse()))]);
        return new(name, role, [Line("hem", M(new(hemHalf, 0)), M(new(-hemHalf, 0))), new("inner", Mm(innerK)), new("crotch", Mm(crotchK)), Line("waist", M(new(-waistSide * 0.38, yTop)), M(new(waistSide * 0.62, yTop))), new("outer", Mm(outerK.AsEnumerable().Reverse()))]);
    }

    static List<PatPt> BezierInner(PatPt a, PatPt c, PatPt b) => Bezier(a, new(c.X, a.Y + (c.Y - a.Y) * 0.25), new(c.X, c.Y), b, 14);

    static Garment Trousers(BodyMeasures b)
    {
        const string fr = "Vorn rechts", fl = "Vorn links", br = "Hinten rechts", bl = "Hinten links", band = "Bund";
        PatPart FR = TrouserPanel(fr, "legFrontR", b, 1, false), FL = TrouserPanel(fl, "legFrontL", b, -1, false), BR = TrouserPanel(br, "legBackR", b, 1, true), BL = TrouserPanel(bl, "legBackL", b, -1, true);
        // Bund als Ring: links beginnend über vorn links, vorn rechts, hinten rechts, hinten links
        double fl1 = FL.Edge("waist").Length * 0.98, fr1 = FR.Edge("waist").Length * 0.98, br1 = BR.Edge("waist").Length * 0.98, bl1 = BL.Edge("waist").Length * 0.98, tot = fl1 + fr1 + br1 + bl1;
        PatPt B(double x, double y) => new(x - tot / 2, y);
        var w = new PatPart(band, "band", [Line("seamFL", B(0, 0), B(fl1, 0)), Line("seamFR", B(fl1, 0), B(fl1 + fr1, 0)), Line("seamBR", B(fl1 + fr1, 0), B(fl1 + fr1 + br1, 0)), Line("seamBL", B(fl1 + fr1 + br1, 0), B(tot, 0)),
            Line("endR", B(tot, 0), B(tot, 4)), Line("free", B(tot, 4), B(0, 4)), Line("endL", B(0, 4), B(0, 0))]);
        return new("hose", "Hose", [FR, FL, BR, BL, w],
        [
            new(fr, "outer", br, "outer", false), new(fr, "inner", br, "inner", false), new(fl, "outer", bl, "outer", false), new(fl, "inner", bl, "inner", false),
            new(fr, "crotch", fl, "crotch", false), new(br, "crotch", bl, "crotch", false), new(band, "endR", band, "endL", true),
            new(band, "seamFL", fl, "waist", false), new(band, "seamFR", fr, "waist", false), new(band, "seamBR", br, "waist", false), new(band, "seamBL", bl, "waist", false),
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
        var front = SkirtPanel("Vorderteil", "skirtFront", b, length); var back = SkirtPanel("Rückenteil", "skirtBack", b, length);
        var band = Band("Bund", "band", front.Edge("waist").Length, back.Edge("waist").Length, 3.5, 0.98);
        return new("rock", "A-Linien-Rock", [front, back, band],
            [new("Vorderteil", "sideR", "Rückenteil", "sideR", false), new("Vorderteil", "sideL", "Rückenteil", "sideL", false), .. BandSeams("Bund", "Vorderteil", "Rückenteil", "waist")]);
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
