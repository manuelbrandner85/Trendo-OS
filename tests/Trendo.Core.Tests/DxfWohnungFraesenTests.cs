using System.Globalization;
using Trendo.Core;
using Trendo.Core.Arch;
using Trendo.Core.Cad;
using Trendo.Core.Media;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>T-0747: gemeinsamer DXF-Leser mit Einheiten, DXF-Import in den Grundriss (SPEC-01) und Fräsbahnen aus DXF/SVG (SPEC-14).</summary>
public class T0747_DxfWohnungFraesen
{
    static string Fixture(string name)
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null && !File.Exists(Path.Combine(d.FullName, "Trendo.sln"))) d = d.Parent;
        return Path.Combine(d!.FullName, "tests", "fixtures", "t0747", name);
    }

    static string Dxf(int units, string entities) => $"0\nSECTION\n2\nHEADER\n9\n$INSUNITS\n70\n{units}\n0\nENDSEC\n0\nSECTION\n2\nENTITIES\n{entities}0\nENDSEC\n0\nEOF\n";
    static string Line(string layer, double x1, double y1, double x2, double y2) => FormattableString.Invariant($"0\nLINE\n8\n{layer}\n10\n{x1}\n20\n{y1}\n11\n{x2}\n21\n{y2}\n");

    // ---------- gemeinsamer Leser ----------

    [Fact(DisplayName = "T-0747: DXF-Leser rechnet $INSUNITS in mm um (Zoll, m, cm, ohne Angabe = mm)")]
    public void Dxf_units_to_mm()
    {
        var inch = CadDxf.Parse(Dxf(1, Line("0", 0, 0, 2, 0)));
        Assert.Equal(1, inch.Units); Assert.Equal(25.4, inch.MmPerUnit); Assert.Equal(50.8, inch.Drawing.Entities[0].Points[1][0], 9);
        var m = CadDxf.Parse(Dxf(6, "0\nCIRCLE\n8\n0\n10\n1\n20\n2\n40\n0.5\n"));
        Assert.Equal(1000, m.Drawing.Entities[0].Cx, 9); Assert.Equal(500, m.Drawing.Entities[0].R, 9);
        Assert.Equal(55.0, CadDxf.Parse(Dxf(5, Line("0", 0, 0, 5.5, 0))).Drawing.Entities[0].Points[1][0], 9);
        var none = CadDxf.Parse("0\nSECTION\n2\nENTITIES\n" + Line("0", 0, 0, 7, 0) + "0\nENDSEC\n0\nEOF\n");
        Assert.Equal(0, none.Units); Assert.Equal(7, none.Drawing.Entities[0].Points[1][0]);
        // Trendos eigener CAD-Export (mm) bleibt beim Wiedereinlesen gleich
        var back = CadDxf.Parse(CadDxf.Write(inch.Drawing));
        Assert.Equal(4, back.Units); Assert.Equal(50.8, back.Drawing.Entities[0].Points[1][0], 9);
    }

    [Fact(DisplayName = "T-0747: DXF-Leser: UTF-8 und Windows-Codepage, \\U+-Zeichen und %%c")]
    public void Dxf_text_encoding()
    {
        var f = Path.Combine(Path.GetTempPath(), "t0747-enc-" + Guid.NewGuid().ToString("N")[..6] + ".dxf");
        try
        {
            var body = Dxf(4, "0\nTEXT\n8\n0\n10\n0\n20\n0\n40\n2.5\n1\nKüche 12 m²\n0\nTEXT\n8\n0\n10\n0\n20\n0\n40\n2.5\n1\nBad 4 m\\U+00B2 %%c10\n");
            File.WriteAllText(f, body, new System.Text.UTF8Encoding(false));
            Assert.Equal(["Küche 12 m²", "Bad 4 m² Ø10"], CadDxf.Read(f).Drawing.Entities.Select(e => e.Text));
            File.WriteAllBytes(f, System.Text.Encoding.Latin1.GetBytes(body));
            Assert.Equal("Küche 12 m²", CadDxf.Read(f).Drawing.Entities[0].Text);
        }
        finally { File.Delete(f); }
    }

    [Fact(DisplayName = "T-0747: Konturen: lose Linien und Bogen werden ein geschlossener Umriss, Kreise und Langloch bleiben eigene Konturen")]
    public void Contours_chain()
    {
        var r = CadDxf.Read(Fixture("teil-fremd.dxf"));
        Assert.Equal(25.4, r.MmPerUnit);
        var cs = CncCam.Contours(r.Drawing);
        Assert.Equal(4, cs.Count); Assert.All(cs, c => Assert.True(c.Closed));
        var outline = cs.OrderByDescending(c => Math.Abs(c.SignedArea)).First();
        Assert.Equal(4 * 25.4, outline.Box.MaxX - outline.Box.MinX, 3); Assert.Equal(2.5 * 25.4, outline.Box.MaxY - outline.Box.MinY, 3);
        // Fläche: Rechteck minus Ecke (r² − πr²/4)
        var rr = 0.5 * 25.4; Assert.Equal(4 * 2.5 * 25.4 * 25.4 - (rr * rr - Math.PI * rr * rr / 4), Math.Abs(outline.SignedArea), 0);
        var slot = cs.Single(c => c.Layer == "LANGLOCH"); // 1,0 × 0,3 Zoll mit Halbkreisen r = 0,15 Zoll
        var w = 0.3 * 25.4; Assert.Equal(25.4 * w + Math.PI * w * w / 4, Math.Abs(slot.SignedArea), 0);
    }

    // ---------- SPEC-01: DXF in den Grundriss ----------

    static FloorPlan SamplePlan()
    {
        var p = new FloorPlan();
        p.AddWall(0, 0, 600, 0); p.AddWall(600, 0, 600, 450); p.AddWall(600, 450, 0, 450); p.AddWall(0, 450, 0, 0);
        p.AddWall(350, 0, 350, 450, 11.5);
        Assert.NotNull(p.AddOpening("tuer", 350, 200, 90)); Assert.NotNull(p.AddOpening("fenster", 150, 0, 120));
        p.AddPolygonRoom([(12, 12), (344, 12), (344, 438), (12, 438)], "Küche");
        return p;
    }

    [Fact(DisplayName = "T-0747: Rundreise Grundriss → DXF → Import: gleiche Wände, Stärken, Tür und Fenster, Raum mit Name")]
    public void Plan_dxf_roundtrip()
    {
        var src = SamplePlan();
        var r = CadDxf.Parse(PlanDrawing.Dxf(src));
        Assert.Equal(5, r.Units); Assert.Equal(10, r.MmPerUnit);
        var p = new FloorPlan(); var rep = PlanDxfImport.Import(p, r);
        Assert.Equal(5, p.Walls.Count); Assert.Equal(5, rep.DoubleLineWalls);
        static string Key(Wall w) { var (a, b) = ((Math.Round(w.X1), Math.Round(w.Y1)), (Math.Round(w.X2), Math.Round(w.Y2))); return string.Join("|", new[] { a.ToString(), b.ToString() }.Order()) + "|" + w.Thickness.ToString(CultureInfo.InvariantCulture); }
        // Plan beginnt bei 0/0 an der linken oberen Außenkante: alter Plan um die halbe Wandstärke verschoben
        var expected = src.Walls.Select(w => new Wall { X1 = w.X1 + 12, Y1 = w.Y1 + 12, X2 = w.X2 + 12, Y2 = w.Y2 + 12, Thickness = w.Thickness }).Select(Key).Order().ToList();
        Assert.Equal(expected, p.Walls.Select(Key).Order().ToList());
        Assert.Equal(2, p.Openings.Count);
        var door = p.Openings.Single(o => o.Kind == "tuer"); var win = p.Openings.Single(o => o.Kind == "fenster");
        Assert.Equal(90, door.Width, 1); Assert.Equal(120, win.Width, 1);
        Assert.Equal(11.5, p.WallOf(door.WallId)!.Thickness, 2);
        Assert.Equal(src.Openings.Single(o => o.Kind == "tuer").At, door.At, 1);
        var room = Assert.Single(p.Rooms); Assert.Equal("Küche", room.Name); Assert.Equal("kueche", room.Kind);
        Assert.Equal(src.Rooms[0].Area, room.Area, 0);
        Assert.Empty(p.Labels); // Flächenangabe rechnet Trendo selbst, Wandmaße bleiben weg
        Assert.Contains("MASSE", rep.SkippedLayers.Keys); Assert.Contains("OEFFNUNGEN", rep.SkippedLayers.Keys);
        // zweite Runde: noch einmal exportiert und eingelesen → gleiche Wände
        var p2 = new FloorPlan(); PlanDxfImport.Import(p2, CadDxf.Parse(PlanDrawing.Dxf(p)));
        Assert.Equal(p.Walls.Select(Key).Order(), p2.Walls.Select(Key).Order());
        Assert.Equal(2, p2.Openings.Count);
    }

    [Fact(DisplayName = "T-0747: Fremde DXF (mm, LWPOLYLINE, POLYLINE, ARC, Ebenen): Maßstab stimmt, Ecken verbunden, Tür erkannt, Möbel/Maße ausgelassen")]
    public void Foreign_dxf_into_plan()
    {
        var r = CadDxf.Read(Fixture("wohnung-fremd.dxf"));
        Assert.Equal(4, r.Units); Assert.Equal(1, r.Skipped["DIMENSION"]);
        var p = new FloorPlan(); var rep = PlanDxfImport.Import(p, r);
        Assert.Equal(["A-WALL"], rep.WallLayers);
        Assert.Contains("A-FURN", rep.SkippedLayers.Keys); Assert.Contains("A-DOOR", rep.SkippedLayers.Keys);
        // Außenwände: 8 × 6 m außen, 24 cm stark, Achse 12 cm innen – Ecken auf den Achsschnittpunkt gezogen
        var outer = p.Walls.Where(w => w.Arc is null && Math.Abs(w.Thickness - 24) < 0.01 && w.Length > 500).ToList();
        Assert.Equal(4, outer.Count);
        var dump = string.Join("; ", p.Walls.Select(w => FormattableString.Invariant($"{w.X1:0.##},{w.Y1:0.##}-{w.X2:0.##},{w.Y2:0.##} t{w.Thickness}{(w.Arc is null ? "" : " a")}")));
        Assert.True(outer.Any(w => Same(w, 12, 12, 788, 12)), dump); Assert.Contains(outer, w => Same(w, 788, 12, 788, 588));
        Assert.Contains(outer, w => Same(w, 12, 588, 788, 588)); Assert.Contains(outer, w => Same(w, 12, 12, 12, 588));
        // Innenwand 11,5 cm, durchgehend bis zur Achse der Außenwände, mit Tür 90 cm an der richtigen Stelle
        var inner = Assert.Single(p.Walls, w => Math.Abs(w.Thickness - 11.5) < 0.01);
        Assert.True(Same(inner, 405.75, 12, 405.75, 588), $"{inner.X1} {inner.Y1} {inner.X2} {inner.Y2}");
        var door = Assert.Single(p.Openings); Assert.Equal("tuer", door.Kind); Assert.Equal(90, door.Width, 1);
        var (dx, dy) = p.WallOf(door.WallId)!.PointAt(door.At); Assert.Equal(405.75, dx, 1); Assert.Equal(600 - 245, dy, 1);
        // Erker als Bogenwand (Halbkreis r = 1 m), Brüstung aus der alten POLYLINE als zwei Einzelwände
        Assert.Equal(1, rep.ArcWalls); var arc = p.Walls.Where(w => w.Arc is not null).ToList(); Assert.NotEmpty(arc);
        Assert.All(arc, w => { var rr = Math.Sqrt((w.X1 - 800) * (w.X1 - 800) + (w.Y1 - 300) * (w.Y1 - 300)); Assert.Equal(100, rr, 1); });
        Assert.Equal(2, rep.SingleLineWalls);
        // Räume mit Namen aus der DXF, Text außerhalb als Beschriftung
        Assert.Equal(["Schlafen", "Wohnen"], p.Rooms.Select(x => x.Name).Order());
        Assert.Equal(3.76 * 5.52, p.Rooms.Single(x => x.Name == "Wohnen").Area / 10000, 2);
        var label = Assert.Single(p.Labels); Assert.Equal("Balkon Süd", label.Text);
        Assert.Contains("Einheit mm", rep.Text);
        static bool Same(Wall w, double x1, double y1, double x2, double y2) =>
            Math.Abs(w.X1 - x1) < 0.05 && Math.Abs(w.Y1 - y1) < 0.05 && Math.Abs(w.X2 - x2) < 0.05 && Math.Abs(w.Y2 - y2) < 0.05
            || Math.Abs(w.X1 - x2) < 0.05 && Math.Abs(w.Y1 - y2) < 0.05 && Math.Abs(w.X2 - x1) < 0.05 && Math.Abs(w.Y2 - y1) < 0.05;
    }

    [Fact(DisplayName = "T-0747: DXF-Import neben einen vorhandenen Plan; Einzellinien mit gewählter Stärke; leere Wandebene meldet die Ebenen")]
    public void Import_next_to_existing_and_single_lines()
    {
        var p = new FloorPlan(); p.AddWall(0, 0, 300, 0);
        var r = CadDxf.Parse(Dxf(6, Line("Mauer", 0, 0, 4, 0) + Line("Mauer", 4, 0, 4, 3)));
        var rep = PlanDxfImport.Import(p, r, new PlanDxfOptions { SingleWallCm = 17.5 });
        Assert.Equal(2, rep.SingleLineWalls); Assert.Equal(3, p.Walls.Count);
        var added = p.Walls.Skip(1).ToList();
        Assert.All(added, w => Assert.Equal(17.5, w.Thickness));
        Assert.Contains(added, w => Math.Abs(w.Length - 400) < 0.01); Assert.Contains(added, w => Math.Abs(w.Length - 300) < 0.01);
        Assert.True(added.Min(w => Math.Min(w.X1, w.X2)) >= 300 + 100 - 0.01); // 1 m rechts neben dem vorhandenen Plan
        var e = Assert.Throws<CadError>(() => PlanDxfImport.Import(new FloorPlan(), CadDxf.Parse(Dxf(4, "0\nTEXT\n8\nMoebel\n10\n0\n20\n0\n40\n2\n1\nx\n")), new PlanDxfOptions { WallLayers = ["Wand"] }));
        Assert.Contains("Moebel", e.Message);
    }

    // ---------- SPEC-14: Fräsbahnen ----------

    static (double X, double Y) Rot((double X, double Y) p, double a) => (p.X * Math.Cos(a) - p.Y * Math.Sin(a), p.X * Math.Sin(a) + p.Y * Math.Cos(a));

    [Fact(DisplayName = "T-0747: Versatz: Außenkontur genau um den Radius, runde Außenecken; L-Form innen mit Schnittecke")]
    public void Offset_exact()
    {
        List<(double X, double Y)> l = [(0, 0), (60, 0), (60, 20), (20, 20), (20, 50), (0, 50)];
        var outside = CncCam.Offset(l, 3);
        Assert.Equal(3, CncCam.MinDistance(outside, l, true), 3);
        Assert.All(outside, q => Assert.True(CncCam.MinDistance([q], l, true) <= 3.011)); // Ecken: höchstens 0,01 mm Sehnenfehler nach außen
        var inside = CncCam.Offset(l, -3);
        Assert.Equal(3, CncCam.MinDistance(inside, l, true), 3);
        foreach (var (x, y) in new[] { (3.0, 3.0), (57.0, 3.0), (57.0, 17.0), (17.0, 47.0), (3.0, 47.0) }) // Außenecken des Teils: Schnittpunkt
            Assert.Contains(inside, q => Math.Abs(q.X - x) < 1e-6 && Math.Abs(q.Y - y) < 1e-6);
        Assert.Contains(inside, q => Math.Abs(q.X - 20) < 1e-6 && Math.Abs(q.Y - 17) < 1e-6); Assert.Contains(inside, q => Math.Abs(q.X - 17) < 1e-6 && Math.Abs(q.Y - 20) < 1e-6); // Innenecke: Bogen um (20/20)
        // Loch kleiner als der Fräser → nichts
        Assert.Empty(CncCam.Offset([(0, 0), (4, 0), (4, 4), (0, 4)], -3));
        // enger Schlitz (4 mm) in einer Platte: außen versetzt bleibt keine Schleife übrig, Fräser kommt der Kontur nie zu nah
        List<(double X, double Y)> slot = [(0, 0), (50, 0), (50, 30), (27, 30), (27, 10), (23, 10), (23, 30), (0, 30)];
        var o2 = CncCam.Offset(slot, 3);
        Assert.True(CncCam.MinDistance(o2, slot, true) >= 3 - 1e-6);
    }

    [Fact(DisplayName = "T-0747: Fräsbahnen aus DXF in Zoll: Löcher zuerst, Radiusversatz stimmt, Stufen bis zur Tiefe, Haltestege nur außen")]
    public void Cam_from_dxf()
    {
        var cs = CncCam.Contours(CadDxf.Read(Fixture("teil-fremd.dxf")).Drawing);
        var s = new CamSettings { ToolD = 4, Depth = 6, StepDown = 2, Feed = 800, Plunge = 200, Tabs = 3, TabW = 5, TabH = 1.5, Rpm = 18000 };
        var r = CncCam.Generate(cs, s);
        Assert.Empty(r.Warnings);
        Assert.Equal(["innen", "innen", "innen", "aussen"], r.Paths.Select(p => p.Kind));
        foreach (var p in r.Paths) Assert.Equal(2, CncCam.MinDistance(p.Pts, r.Contours[p.Source].Pts, true), 2);
        // Bahnen gehen innen gegen, außen im Uhrzeigersinn (Gleichlauf)
        Assert.True(CncCam.SignedArea(r.Paths[^1].Pts) < 0); Assert.All(r.Paths.Take(3), p => Assert.True(CncCam.SignedArea(p.Pts) > 0));
        // Teil auf den Nullpunkt geschoben
        Assert.Equal(0, r.Contours.Min(c => c.Box.MinX), 9); Assert.Equal(0, r.Contours.Min(c => c.Box.MinY), 9);
        // G-Code: gelesen mit dem vorhandenen Simulator
        var segs = CncSim.Segments(r.Gcode);
        Assert.Equal(-6, segs.Min(g => Math.Min(g.Z0, g.Z1)), 9);
        Assert.Equal([-2.0, -4.0, -6.0], segs.Where(g => !g.Rapid && g.X0 == g.X1 && g.Y0 == g.Y1 && g.Z1 < g.Z0).Select(g => g.Z1).Take(3));
        Assert.Contains("M3 S18000", r.Gcode); Assert.EndsWith("M5\nM30\n", r.Gcode);
        // Haltestege: im letzten Durchgang der Außenkontur 3 Stücke auf −4,5 mm, in Löchern keine
        var zTab = -6 + 1.5; var lastOuter = r.Gcode[r.Gcode.LastIndexOf("(Kontur", StringComparison.Ordinal)..];
        Assert.Equal(3, System.Text.RegularExpressions.Regex.Matches(lastOuter, $@"\nG1 Z{zTab.ToString(CultureInfo.InvariantCulture)}\n").Count); // je Steg einmal hoch, nur im Durchgang −6
        Assert.Equal(1 + 3, System.Text.RegularExpressions.Regex.Matches(lastOuter, @"\nG1 Z-6 F200\n").Count); // eintauchen + nach jedem Steg wieder hinunter
        Assert.DoesNotContain($"Z{zTab.ToString(CultureInfo.InvariantCulture)}", r.Gcode[..r.Gcode.LastIndexOf("(Kontur", StringComparison.Ordinal)]);
        // Fräser bleibt beim Schneiden außerhalb der Teilekontur bzw. innerhalb der Löcher – mit dem Prüfer wie im Beleg
        var cut = segs.Where(g => !g.Rapid && g.Z1 < 0).ToList();
        Assert.True(cut.Count > 100);
    }

    [Fact(DisplayName = "T-0747: Fräsbahnen-Arten: alles außen, alles auf der Linie, offene Linie fährt hin und zurück, zu kleines Loch wird gemeldet")]
    public void Cam_modes()
    {
        List<CadContour> cs = [new([(0, 0), (40, 0), (40, 30), (0, 30)], true, "0"), new([(18, 13), (22, 13), (22, 17), (18, 17)], true, "0"), new([(5, 5), (35, 5)], false, "gravur")];
        var auto = CncCam.Generate(cs, new CamSettings { ToolD = 6, Depth = 1, StepDown = 0.5 });
        Assert.Contains(auto.Warnings, w => w.Contains("zu klein", StringComparison.Ordinal));
        // T-0747-fix2 (zweite Prüfung): offene Linien werden in „auto“ nicht mehr still auf der Linie gefräst, sondern ausgelassen und gemeldet –
        // auf der Linie nur in der ausdrücklichen Fräsart „auf“ (Gravur)
        Assert.Equal(["aussen"], auto.Paths.Select(p => p.Kind));
        Assert.Contains(auto.Warnings, w => w.StartsWith("Kontur 3 (Ebene gravur) ausgelassen: offene Kontur", StringComparison.Ordinal));
        var gravur = CncCam.Generate(cs, new CamSettings { ToolD = 6, Depth = 1, StepDown = 0.5, Mode = "auf" });
        var engr = Assert.Single(gravur.Paths, p => !p.Closed); Assert.Equal("auf", engr.Kind);
        var segs = CncSim.Segments(gravur.Gcode); var atDepth = segs.Where(g => !g.Rapid && Math.Abs(g.Z0 - -1) < 1e-9 && Math.Abs(g.Z1 - -1) < 1e-9 && g.Y0 == 5).ToList();
        Assert.Contains(atDepth, g => g.X1 < g.X0); // zweite Stufe fährt zurück
        var allOut = CncCam.Generate(cs.Take(2).ToList(), new CamSettings { ToolD = 2, Depth = 1, StepDown = 1, Mode = "aussen" });
        Assert.All(allOut.Paths, p => Assert.Equal("aussen", p.Kind));
        var on = CncCam.Generate(cs.Take(1).ToList(), new CamSettings { ToolD = 2, Depth = 1, StepDown = 1, Mode = "auf" });
        Assert.Equal(cs[0].Pts.Order(), on.Paths[0].Pts.Order()); Assert.True(on.Paths[0].Closed);
        Assert.Throws<ArgumentException>(() => CncCam.Generate(cs, new CamSettings { Depth = 1, Tabs = 2, TabH = 1 }));
    }

    [Fact(DisplayName = "T-0747: CNC-Dokument merkt sich Konturen und CAM-Werte (im Speicher und als Datei), Import einer Zeichnung erzeugt den G-Code")]
    public void CncJob_keeps_contours()
    {
        var j = new CncJob { ToolD = 3, Depth = 2, StepDown = 1, CamTabs = 2, CamTabW = 4, CamTabH = 0.5, CamMode = "aussen" };
        var r = j.ImportDrawing(Fixture("teil-fremd.dxf"));
        Assert.Equal("teil-fremd.dxf", j.CamSource); Assert.Equal(4, j.Contours.Count); Assert.Equal(r.Gcode, j.Gcode);
        Assert.All(r.Paths, p => Assert.Equal("aussen", p.Kind));
        var o = new System.Text.Json.Nodes.JsonObject(); j.WriteTo(o);
        foreach (var back in new[] { CncJob.From(o), CncJob.From(System.Text.Json.Nodes.JsonNode.Parse(o.ToJsonString())!.AsObject()) })
        {
            Assert.Equal(2, back.CamTabs); Assert.Equal(4, back.CamTabW); Assert.Equal(0.5, back.CamTabH); Assert.Equal("aussen", back.CamMode);
            Assert.Equal(j.Contours.Select(c => c.Pts.Count), back.Contours.Select(c => c.Pts.Count));
            Assert.Equal(r.Gcode, back.Cam().Gcode);
        }
        Assert.Throws<InvalidOperationException>(() => new CncJob().Cam());
        Assert.Throws<CadError>(() => new CncJob().ImportDrawing(Fixture("make_fixtures.py")));
    }

    [Fact(DisplayName = "T-0747: Belegdateien – fremde DXF → Plan → bearbeiten → DXF; DXF/SVG → G-Code (Ordner über T0747_BELEG, sonst Temp)")]
    public void Beleg_files()
    {
        var dir = Environment.GetEnvironmentVariable("T0747_BELEG") is { Length: > 0 } e ? e : Path.Combine(Path.GetTempPath(), "t0747-beleg-" + Guid.NewGuid().ToString("N")[..6]);
        Directory.CreateDirectory(dir);
        var p = new FloorPlan(); PlanDxfImport.Import(p, CadDxf.Read(Fixture("wohnung-fremd.dxf")));
        p.Walls.Single(w => Math.Abs(w.Thickness - 11.5) < 0.01).Thickness = 17.5; // bearbeiten: Innenwand dicker
        File.WriteAllText(Path.Combine(dir, "wohnung-bearbeitet.dxf"), PlanDrawing.Dxf(p));
        var s = new CamSettings { ToolD = 4, Depth = 6, StepDown = 2, Feed = 800, Plunge = 200, Tabs = 3, TabW = 5, TabH = 1.5, Rpm = 18000 };
        File.WriteAllText(Path.Combine(dir, "teil-fremd-dxf.nc"), CncCam.Generate(CncCam.Contours(CadDxf.Read(Fixture("teil-fremd.dxf")).Drawing), s).Gcode);
        File.WriteAllText(Path.Combine(dir, "teil-fremd-svg.nc"), CncCam.Generate(CncCam.Contours(CadSvg.Read(Fixture("teil-fremd.svg")).Drawing), s).Gcode);
        foreach (var f in new[] { "wohnung-bearbeitet.dxf", "teil-fremd-dxf.nc", "teil-fremd-svg.nc" }) Assert.True(new FileInfo(Path.Combine(dir, f)).Length > 500, f);
    }

    [Fact(DisplayName = "T-0747: SVG lesen: mm über viewBox, y nach oben, Rundung, Kreis bleibt Kreis, Bogen im Pfad, transform der Gruppe")]
    public void Svg_contours()
    {
        var r = CadSvg.Read(Fixture("teil-fremd.svg"));
        Assert.Equal(1, r.MmPerUnit, 9); Assert.Equal(1, r.Skipped["text"]);
        var circle = Assert.Single(r.Drawing.Entities, e => e.Kind == "kreis");
        Assert.Equal(30, circle.Cx, 9); Assert.Equal(-40, circle.Cy, 9); Assert.Equal(6, circle.R, 9);
        Assert.Equal(["loecher", "umriss"], r.Drawing.Layers.Select(l => l.Name).Order());
        var cs = CncCam.Contours(r.Drawing);
        Assert.Equal(3, cs.Count); Assert.All(cs, c => Assert.True(c.Closed));
        var rect = cs.Single(c => c.Layer == "umriss"); Assert.Equal(100, rect.Box.MaxX - rect.Box.MinX, 6); Assert.Equal(-70, rect.Box.MinY, 6);
        Assert.Equal(100 * 60 - (4 - Math.PI) * 64, Math.Abs(rect.SignedArea), 0);
        var slot = cs.Single(c => c.Layer == "loecher" && c.Pts.Count > 20 && Math.Abs(c.SignedArea) > 200);
        Assert.Equal(10 * 20 + Math.PI * 100, Math.Abs(slot.SignedArea), 0); // Langloch aus zwei Bögen
        Assert.Equal(70, slot.Box.MinX, 6); Assert.Equal(100, slot.Box.MaxX, 6); // translate(30 40): x 40…70 → 70…100
        // ParseSubpaths: zwei Teilpfade getrennt, Bogenpunkte liegen auf dem Kreis
        var subs = ImageVector.ParseSubpaths("M0 0 L10 0 M20 0 A 5 5 0 0 1 30 0 Z");
        Assert.Equal(2, subs.Count); Assert.False(subs[0].Closed); Assert.True(subs[1].Closed);
        Assert.All(subs[1].Points, q => Assert.Equal(5, Math.Sqrt((q.X - 25) * (q.X - 25) + q.Y * q.Y), 6));
        Assert.Throws<CadError>(() => CadSvg.Parse("<svg xmlns=\"http://www.w3.org/2000/svg\"><text>x</text></svg>"));
    }
}
