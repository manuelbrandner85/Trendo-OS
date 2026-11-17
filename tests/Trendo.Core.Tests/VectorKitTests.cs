using System.Text;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using PdfSharp.Pdf.IO;
using Trendo.Core.Kreativ;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>T-0696: gemeinsamer Vektor-Kern für Logos und Animation.</summary>
public class VectorKitTests
{
    static VShape Rect(string id, double x, double y, double w, double h, string fill = "#FF0000") => VectorKit.New("rect", x, y, w, h, id, fill);

    [Fact(DisplayName = "Pfaddaten nur aus M/L/C/Z; Ellipse aus vier Bézierkurven, Rechteck mit Rundung aus Linien und Kurven")]
    public void PathData()
    {
        var e = VectorKit.New("ellipse", 0, 0, 100, 50, "e");
        var (n, closed) = VectorKit.ToNodes(e);
        Assert.True(closed); Assert.Equal(4, n.Count);
        var d = VectorKit.PathData(n, true);
        Assert.Equal(4, d.Count(c => c == 'C')); Assert.EndsWith("Z", d);
        var r = Rect("r", 0, 0, 100, 60); r.Radius = 10;
        var rd = VectorKit.PathData(VectorKit.ToNodes(r).Nodes, true);
        Assert.Equal(4, rd.Count(c => c == 'C')); Assert.Equal(4, rd.Count(c => c == 'L'));
        var star = VectorKit.New("star", 0, 0, 100, 100, "s"); star.Sides = 5;
        Assert.Equal(10, VectorKit.ToNodes(star).Nodes.Count);
    }

    [Fact(DisplayName = "Gedrehte Form: Rahmen, Treffer und Umrechnung lokal ↔ Dokument stimmen")]
    public void RotationAndHit()
    {
        var s = Rect("r", 100, 100, 200, 20); s.Rot = 90;
        var b = VectorKit.Bounds(s);
        Assert.Equal(190, b.X, 6); Assert.Equal(10, b.Y, 6); Assert.Equal(20, b.W, 6); Assert.Equal(200, b.H, 6);
        Assert.True(VectorKit.Hit(s, 200, 30)); // oben im gedrehten Balken
        Assert.False(VectorKit.Hit(s, 120, 110)); // dort lag er ungedreht
        var (lx, ly) = VectorKit.ToLocal(s, 200, 30); var back = VectorKit.ToDoc(s, lx, ly);
        Assert.Equal(200, back.X, 6); Assert.Equal(30, back.Y, 6);
        var line = VectorKit.New("line", 0, 0, 100, 100, "l");
        Assert.True(VectorKit.Hit(line, 50, 51, 4)); Assert.False(VectorKit.Hit(line, 90, 10, 4));
    }

    [Fact(DisplayName = "Pfad bearbeiten: Punkt ziehen und Kasten neu anpassen verschiebt nichts sichtbar, auch gedreht; Ecke ↔ Rundung")]
    public void NormalizeKeepsPlace()
    {
        var s = new VShape { Id = "p", Kind = "path", X = 50, Y = 40, W = 100, H = 100, Rot = 30, Nodes = [VNode.Corner(0, 0), VNode.Corner(100, 0), VNode.Corner(100, 100), VNode.Corner(0, 100)] };
        var fixedPoint = VectorKit.ToDoc(s, 100, 100);
        s.Nodes[0] = VNode.Corner(-40, -20); // Punkt nach außen gezogen
        VectorKit.Normalize(s);
        Assert.Equal(140, s.W, 6); Assert.Equal(120, s.H, 6);
        var i = s.Nodes.FindIndex(n => Math.Abs(n.X - 140) < 1e-6 && Math.Abs(n.Y - 120) < 1e-6);
        var now = VectorKit.ToDoc(s, s.Nodes[i].X, s.Nodes[i].Y);
        Assert.Equal(fixedPoint.X, now.X, 6); Assert.Equal(fixedPoint.Y, now.Y, 6);
        var smooth = VectorKit.ToggleSmooth(s.Nodes, 1, true);
        Assert.False(smooth.IsCorner);
        Assert.True(VectorKit.ToggleSmooth([.. s.Nodes.Take(1), smooth], 1, false).IsCorner);
        var rect = Rect("r", 0, 0, 10, 10); VectorKit.ToPath(rect);
        Assert.Equal("path", rect.Kind); Assert.Equal(4, rect.Nodes.Count);
    }

    [Fact(DisplayName = "Ausrichten, Verteilen, Gruppieren, Reihenfolge und Duplizieren")]
    public void Arrange()
    {
        var d = new VDoc { W = 400, H = 200, Shapes = [Rect("a", 10, 10, 20, 20), Rect("b", 100, 50, 40, 40), Rect("c", 300, 120, 20, 20)] };
        VectorKit.Align(d, ["a", "b", "c"], "links");
        Assert.All(d.Shapes, s => Assert.Equal(10, s.X, 6));
        VectorKit.Align(d, ["b"], "mitte-h"); // allein: an der Zeichenfläche
        Assert.Equal(200, d.Find("b")!.Cx, 6);
        d.Find("a")!.X = 0; d.Find("b")!.X = 100; d.Find("c")!.X = 380;
        VectorKit.Distribute(d, ["a", "b", "c"], "h");
        Assert.Equal(d.Find("b")!.X - 20, d.Find("c")!.X - (d.Find("b")!.X + 40), 6);
        var g = VectorKit.Group(d, ["a", "c"]);
        Assert.Equal(["a", "c"], d.Shapes.Where(s => s.Group == g).Select(s => s.Id));
        Assert.Equal(["a", "c"], VectorKit.WithGroups(d, ["a"]).Order());
        VectorKit.Order(d, ["b"], "ganz-vorne"); Assert.Equal("b", d.Shapes[^1].Id);
        VectorKit.Order(d, ["b"], "hinten"); Assert.Equal("b", d.Shapes[^2].Id);
        VectorKit.Order(d, ["b"], "ganz-hinten"); Assert.Equal("b", d.Shapes[0].Id);
        var copies = VectorKit.Duplicate(d, d.Shapes.Where(s => s.Group == g).ToList());
        Assert.Equal(2, copies.Count);
        Assert.NotEqual(g, d.Find(copies[0])!.Group); Assert.Equal(d.Find(copies[0])!.Group, d.Find(copies[1])!.Group);
        VectorKit.Ungroup(d, ["a", "c"]); Assert.Equal("", d.Find("a")!.Group);
        Assert.Throws<InvalidOperationException>(() => VectorKit.Group(d, ["a"]));
    }

    [Fact(DisplayName = "JSON: alle Eigenschaften überstehen Speichern und Laden; kaputte Werte fallen auf sichere Standards")]
    public void Json()
    {
        var s = new VShape { Id = "p", Kind = "path", X = 1, Y = 2, W = 3, H = 4, Rot = 15, Fill = "#112233", Fill2 = "#445566", GradAngle = 45, Stroke = "#778899", StrokeW = 2, Closed = false, Group = "g", Opacity = 0.5, Nodes = [new VNode(0, 0, -1, -1, 1, 1), VNode.Corner(3, 4)] };
        MotionKit.SetKey(s, "x", 0, 10); MotionKit.SetKey(s, "farbe", 1, VectorKit.Pack("#ABCDEF"), "federnd");
        var t = new VShape { Id = "t", Kind = "text", Text = "Hallo", Font = "Georgia", FontSize = 30, Bold = true, Anchor = "middle" };
        var d = new VDoc { W = 300, H = 200, Background = "#000000", Shapes = [s, t] };
        var back = VectorKit.FromJson(JsonNode.Parse(VectorKit.ToJson(d).ToJsonString()));
        Assert.Equal(VectorKit.ToJson(d).ToJsonString(), VectorKit.ToJson(back).ToJsonString());
        Assert.Equal("#ABCDEF", VectorKit.Unpack(back.Find("p")!.Tracks["farbe"][0].V));
        var bad = VectorKit.FromJson(JsonNode.Parse("""{"w":"x","shapes":[{"id":"a","kind":"bogus","fill":"rot","x":1e300,"image":"C:/Windows/win.ini"},{"id":"a","kind":"image","image":"file:///etc/passwd"}]}"""));
        Assert.Equal("rect", bad.Shapes[0].Kind); Assert.Equal("#D4A017", bad.Shapes[0].Fill); Assert.Equal(1e5, bad.Shapes[0].X);
        Assert.NotEqual(bad.Shapes[0].Id, bad.Shapes[1].Id); Assert.Equal("", bad.Shapes[1].Image);
    }

    [Fact(DisplayName = "SVG: eigenes SVG ist gültiges XML mit Verlauf, Drehung, Text und kommt verlustfrei zurück")]
    public void SvgRoundTrip()
    {
        var r = Rect("r", 10, 20, 100, 50, "#1E3A8A"); r.Fill2 = "#D4A017"; r.Rot = 20;
        var t = VectorKit.New("text", 10, 100, 10, 40, "t", "#000000"); t.Text = "Café & <Bar>";
        var d = new VDoc { W = 640, H = 180, Shapes = [r, t] };
        var svg = VectorKit.ToSvg(d);
        var x = XElement.Parse(svg);
        XNamespace ns = "http://www.w3.org/2000/svg";
        Assert.Single(x.Descendants(ns + "linearGradient"));
        Assert.Contains("rotate(20", svg);
        Assert.Contains("Café &amp; &lt;Bar&gt;", svg);
        var (back, skipped) = VectorKit.FromSvg(svg);
        Assert.Equal(0, skipped);
        Assert.Equal(VectorKit.ToJson(d).ToJsonString(), VectorKit.ToJson(back).ToJsonString());
    }

    [Fact(DisplayName = "Fremdes SVG (z. B. aus Inkscape) wird gelesen: Kreis, Pfad mit Kurven, Polygon, Text, benannte Farben")]
    public void ForeignSvg()
    {
        const string svg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="200" height="100" viewBox="0 0 200 100">
              <circle cx="50" cy="50" r="40" fill="red" stroke="#000" stroke-width="2"/>
              <path d="M100,10 C150,10 150,90 100,90 Z" fill="#00FF00"/>
              <polygon points="160,10 190,90 130,90" style="fill:blue"/>
              <text x="10" y="95" font-size="12" fill="#333333">Hallo</text>
              <foreignObject/>
            </svg>
            """;
        var (d, skipped) = VectorKit.FromSvg(svg);
        Assert.Equal(200, d.W); Assert.Equal(4, d.Shapes.Count); Assert.Equal(1, skipped);
        Assert.Equal(("ellipse", "#FF0000", "#000000"), (d.Shapes[0].Kind, d.Shapes[0].Fill, d.Shapes[0].Stroke));
        Assert.Equal(10, d.Shapes[0].X, 3); Assert.Equal(80, d.Shapes[0].W, 3);
        Assert.Equal("path", d.Shapes[1].Kind); Assert.True(d.Shapes[1].Nodes.Count > 4); Assert.Equal(137.5, d.Shapes[1].X + d.Shapes[1].W, 0);
        Assert.Equal("#0000FF", d.Shapes[2].Fill);
        Assert.Equal("Hallo", d.Shapes[3].Text);
        Assert.Throws<InvalidDataException>(() => VectorKit.FromSvg("<svg"));
    }

    [Fact(DisplayName = "Eingesetztes SVG wird eingepasst, als Gruppe angelegt und lässt sich umfärben")]
    public void PlaceRecolor()
    {
        var src = new VDoc { W = 1000, H = 1000, Shapes = [Rect("a", 0, 0, 1000, 500), Rect("b", 0, 500, 1000, 500, "#00FF00")] };
        var target = new VDoc { W = 400, H = 200 };
        var ids = VectorKit.Place(target, src, 200, 100, 100, 100);
        Assert.Equal(2, ids.Count);
        var b = VectorKit.Bounds(target.Shapes);
        Assert.Equal(100, b.W, 6); Assert.Equal(200, b.X + b.W / 2, 6);
        Assert.Single(target.Shapes.Select(s => s.Group).Distinct()); Assert.NotEqual("", target.Shapes[0].Group);
        VectorKit.Recolor(target.Shapes, "#123456");
        Assert.All(target.Shapes, s => Assert.Equal("#123456", s.Fill));
        Assert.Throws<ArgumentException>(() => VectorKit.Recolor(target.Shapes, "blau"));
    }

    [Fact(DisplayName = "Logo-Vorschlag: bearbeitbare Formen in der Geometrie des alten Generators; Varianten hell/dunkel halten 3:1; Markenpaket aus dem echten Logo")]
    public void Logo()
    {
        var spec = new LogoKit.Spec("Café Morgenrot", "Röstet seit heute", "#7A2E1F", "#E0A458", "kreis", "Georgia");
        var d = VectorKit.FromLogo(spec);
        Assert.Equal((640.0, 180.0), (d.W, d.H));
        var circle = d.Find("zeichen-form")!;
        Assert.Equal(("ellipse", 90.0, 90.0, 124.0), (circle.Kind, circle.Cx, circle.Cy, circle.W));
        Assert.Equal("CM", d.Find("zeichen-text")!.Text); Assert.Equal("middle", d.Find("zeichen-text")!.Anchor);
        Assert.Equal(92, d.Find("name")!.Y + 44 * VectorKit.TextBase, 6); // Grundlinie wie im alten SVG
        Assert.Equal("Georgia", d.Find("name")!.Font);
        var dark = VectorKit.ForBackground(d, "#111827");
        foreach (var s in dark.Shapes.Where(s => s.Group == "")) Assert.True(LogoKit.Contrast(s.Fill, "#111827") >= 3, s.Id);
        Assert.True(LogoKit.Contrast(dark.Find("zeichen-text")!.Fill, dark.Find("zeichen-form")!.Fill) >= 3);
        Assert.Equal("#7A2E1F", VectorKit.Palette(d)[0]);
        var md = VectorKit.BrandMarkdown(d, spec.Name);
        Assert.Contains("| Hauptfarbe | #7A2E1F |", md); Assert.Contains("- Georgia", md); Assert.Contains("favicon.ico", md);
        var schild = VectorKit.FromLogo(spec with { Layout = "schild" });
        Assert.Equal("path", schild.Find("zeichen-form")!.Kind);
        Assert.Contains(" C", VectorKit.PathData(schild.Find("zeichen-form")!.Nodes, true));
        Assert.DoesNotContain(VectorKit.FromLogo(spec with { Layout = "wort" }).Shapes, s => s.Id.StartsWith("zeichen"));
        Assert.All(VectorKit.Mono(d, "#000000").Shapes, s => Assert.True(s.Fill is "#000000" or ""));
    }

    [Fact(DisplayName = "PDF ist ein Vektor-PDF in der Größe der Zeichenfläche (von PDFsharp wieder lesbar, mit Pfaden und Text)")]
    public void Pdf()
    {
        var d = VectorKit.FromLogo(new LogoKit.Spec("Nordlicht Labs", "Software", "#0F4C5C", "#5FA8D3", "kreis", "Segoe UI"));
        d.Shapes[0].Fill2 = "#FFFFFF"; d.Shapes[0].Rot = 10;
        var bytes = VectorKit.Pdf(d);
        Assert.Equal("%PDF", Encoding.ASCII.GetString(bytes, 0, 4));
        using var ms = new MemoryStream(bytes);
        using var pdf = PdfReader.Open(ms, PdfDocumentOpenMode.Import);
        Assert.Equal(1, pdf.PageCount);
        Assert.Equal(640 * 0.75, pdf.Pages[0].Width.Point, 1); Assert.Equal(180 * 0.75, pdf.Pages[0].Height.Point, 1);
    }

    [Fact(DisplayName = "Favicon: ICO mit PNG-Einträgen 16 bis 256 Pixel")]
    public void Ico()
    {
        var png = new byte[] { 0x89, 0x50, 0x4E, 0x47, 1, 2, 3 };
        var ico = VectorKit.Ico([(16, png), (32, png), (256, png)]);
        Assert.Equal([16, 32, 256], VectorKit.IcoSizes(ico));
        Assert.Equal(6 + 16 * 3, BitConverter.ToInt32(ico, 6 + 12)); // erster Bildanfang
        Assert.Throws<ArgumentException>(() => VectorKit.Ico([(512, png)]));
    }

    [Fact(DisplayName = "Bewegung: Wert und Farbe zwischen Schlüsseln mit Kurve, Pose, Schlüssel ziehen, Kurve ändern, löschen")]
    public void Motion()
    {
        var s = Rect("r", 0, 0, 100, 100);
        Assert.Equal(50, MotionKit.Value(s, "x", 3)); // ohne Schlüssel: aus der Form
        MotionKit.SetKey(s, "x", 0, 100, "linear"); MotionKit.SetKey(s, "x", 2, 300);
        Assert.Equal(200, MotionKit.Value(s, "x", 1), 6);
        MotionKit.SetKey(s, "farbe", 0, VectorKit.Pack("#000000"), "linear"); MotionKit.SetKey(s, "farbe", 1, VectorKit.Pack("#FF0000"));
        Assert.Equal("#800000", MotionKit.Color(s, 0.5));
        var p = MotionKit.Pose(s, 1);
        Assert.Equal(200, p.Cx, 6); Assert.Equal("#FF0000", p.Fill);
        MotionKit.MoveKeys(s, ["x", "y"], 2, 1);
        Assert.Equal([0.0, 1.0], MotionKit.KeyTimes(s, ["x"]));
        Assert.Equal(300, MotionKit.Value(s, "x", 1), 6);
        MotionKit.SetEase(s, ["x"], 0, "federnd"); Assert.Equal("federnd", s.Tracks["x"][0].Ease);
        Assert.Throws<ArgumentException>(() => MotionKit.SetEase(s, ["x"], 0, "zappelig"));
        MotionKit.DeleteKeys(s, ["x"], 1); MotionKit.DeleteKeys(s, ["x"], 0);
        Assert.False(s.Tracks.ContainsKey("x"));
        Assert.Equal(1, MotionKit.Duration(new VDoc { Shapes = [s] }));
    }

    [Fact(DisplayName = "Animiertes SVG ist gültiges XML mit SMIL-Bewegung und Farbe; alte Ebenen (rechteck/kreis/text) werden zu Formen")]
    public void MotionSvgAndLegacy()
    {
        var l = new AnimKit.Layer("ball", "kreis", "", "#F5C542", 120, 120, []);
        AnimKit.SetKey(l, "x", 0, 200, "linear"); AnimKit.SetKey(l, "x", 2, 1080);
        var s = MotionKit.FromLayer(l);
        Assert.Equal("ellipse", s.Kind); Assert.Equal(200, MotionKit.Value(s, "x", 0)); Assert.Equal(1080, MotionKit.Value(s, "x", 2));
        var t = MotionKit.FromLayer(new AnimKit.Layer("t", "text", "Hallo", "#FFFFFF", 0, 90, []));
        Assert.Equal(("text", 90.0, "middle"), (t.Kind, t.FontSize, t.Anchor)); Assert.Equal(AnimKit.StageW / 2, t.Cx, 6);
        MotionKit.SetKey(s, "farbe", 0, VectorKit.Pack("#FF0000")); MotionKit.SetKey(s, "farbe", 2, VectorKit.Pack("#0000FF"));
        var d = new VDoc { W = 1280, H = 720, Background = "#101827", Shapes = [s, t] };
        var x = XElement.Parse(MotionKit.Svg(d));
        XNamespace ns = "http://www.w3.org/2000/svg";
        Assert.Equal(6, x.Descendants(ns + "animateTransform").Count());
        Assert.Contains(x.Descendants(ns + "animate"), a => (string?)a.Attribute("attributeName") == "fill");
        Assert.Contains("1080 ", x.ToString());
    }
}
