using Trendo.Core;
using Xunit;

namespace Trendo.Core.Tests;

public class SPEC01_Rooms
{
    [Fact(DisplayName = "SPEC-01: Raumplaner rechnet Fläche, Farbe, Boden, Leisten und speichert dauerhaft")]
    public void Plan()
    {
        using var f = new Fixture();
        var p = new RoomPlanner(f.Root);
        var r = p.Add("Wohnzimmer", 5, 4, 2.5, 1, 2);
        Assert.Equal(20, r.Floor); Assert.Equal(50, r.Volume);
        Assert.Equal(45 - 2 - 3, r.WallNet, 6); Assert.Equal(17.1, r.Skirting, 6);
        var m = RoomPlanner.Need(p.Project.Rooms, 2.2);
        Assert.Equal(10, m.FloorPacks); // 22 m² / 2,2
        Assert.Equal(12, m.PaintLiters); Assert.Equal(2, m.PaintBuckets);
        Assert.Throws<ArgumentException>(() => p.Add("x", 0, 3));
        Assert.Single(new RoomPlanner(f.Root).Project.Rooms);
        var svg = File.ReadAllText(p.ExportSvg());
        Assert.Contains("<rect", svg); Assert.Contains("Wohnzimmer", svg);
        Assert.Equal(2.5, Fach.Num("2,5")); Assert.Equal(1234.5, Fach.Num("1.234,5"));
    }
}

public class SPEC02_Electronics
{
    [Fact(DisplayName = "SPEC-02: Farbcode, Ohm, LED-Vorwiderstand, Spannungsteiler, Stückliste")]
    public void Calc()
    {
        Assert.Equal((4700, 5), Electronics.DecodeBands("gelb violett rot gold"));
        Assert.Equal(10000, Electronics.DecodeBands("braun schwarz schwarz rot braun").Ohm);
        Assert.Equal("gelb violett rot gold", Electronics.EncodeBands(4700));
        Assert.Equal("braun schwarz gold gold", Electronics.EncodeBands(1));
        Assert.Throws<ArgumentException>(() => Electronics.DecodeBands("lila blau"));
        var o = Electronics.OhmsLaw(12, 0, 240); Assert.Equal(0.05, o.I, 6); Assert.Equal(0.6, o.P, 6);
        var led = Electronics.LedResistor(5, 2, 20); Assert.Equal(150, led.Exact, 6); Assert.Equal(150, led.E12);
        Assert.Equal(180, Electronics.LedResistor(5, 1.9, 18).E12);
        var d = Electronics.Divider(12, 3.3); Assert.True(d.ErrorPct < 3);
        var bom = Electronics.ParseBom("R1, 10k, 0805, 1, 0,02\nR2,10k,0805,2,0,02\n# Kommentar\nC1;100n;0603");
        Assert.Equal(2, bom.Count); Assert.Equal(3, bom[0].Qty); Assert.Equal("R1 R2", bom[0].Ref);
        Assert.Contains("0,06", Electronics.BomCsv(bom));
    }
}

public class SPEC03_Engineering
{
    [Fact(DisplayName = "SPEC-03: Balken (4 Lagerungen), Querschnitte, Wärmedehnung, Einheiten")]
    public void Calc()
    {
        var s = Engineering.Rect(100, 200); Assert.Equal(100.0 * 200 * 200 * 200 / 12, s.I); Assert.Equal(666666.67, s.W, 2);
        var steel = Engineering.Mat("Baustahl");
        // F=10 kN, L=4 m: M = 10 kNm; f = F L³/(48 E I)
        var r = Engineering.Beam(BeamCase.EinfeldMitte, 4, 10, s, steel);
        Assert.Equal(10, r.MaxMoment_kNm); Assert.Equal(15, r.Stress_MPa); Assert.Equal(Math.Round(10000 * Math.Pow(4000, 3) / (48 * 210000 * s.I), 2), r.Deflection_mm);
        Assert.True(r.Ok);
        Assert.Equal(20, Engineering.Beam(BeamCase.EinfeldStrecke, 4, 10, s, steel).MaxMoment_kNm);
        Assert.Equal(40, Engineering.Beam(BeamCase.KragarmEnde, 4, 10, s, steel).MaxMoment_kNm);
        Assert.False(Engineering.Beam(BeamCase.KragarmStrecke, 4, 10, Engineering.Rect(40, 40), Engineering.Mat("Fichte")).Ok);
        var t = Engineering.Tube(50, 5); Assert.True(t.I < Engineering.Tube(50).I);
        Assert.Equal(2.76, Engineering.ThermalExpansion(Engineering.Mat("Aluminium"), 3, 40));
        Assert.Equal(6.894757, Engineering.Convert(100, "psi", "bar"), 5);
        Assert.Throws<ArgumentException>(() => Engineering.Convert(1, "m", "bar"));
    }
}

public static class Glb
{
    /// <summary>Echte, minimale GLB-Datei mit einem Dreiecksnetz (tris Dreiecke).</summary>
    public static string Write(string dir, long tris, int textures = 0)
    {
        var json = $"{{\"asset\":{{\"version\":\"2.0\",\"generator\":\"Test\"}},\"meshes\":[{{\"primitives\":[{{\"attributes\":{{\"POSITION\":0}},\"indices\":1,\"material\":0}}]}}],\"materials\":[{{}}],\"accessors\":[{{\"count\":{tris * 3}}},{{\"count\":{tris * 3}}}],\"textures\":[{string.Join(",", Enumerable.Repeat("{}", textures))}],\"nodes\":[{{\"mesh\":0}}]}}";
        while (json.Length % 4 != 0) json += " ";
        var j = System.Text.Encoding.UTF8.GetBytes(json);
        var p = Path.Combine(dir, $"m{tris}.glb");
        using var w = new BinaryWriter(File.Create(p));
        w.Write(0x46546C67u); w.Write(2u); w.Write((uint)(12 + 8 + j.Length)); w.Write((uint)j.Length); w.Write(0x4E4F534Au); w.Write(j);
        return p;
    }
}

public class SPEC04_Xr
{
    [Fact(DisplayName = "SPEC-04: GLB lesen, Dreiecke zählen, Geräte-Grenzen und Bildzeit prüfen")]
    public void Inspect()
    {
        using var f = new Fixture(); Directory.CreateDirectory(f.Root);
        var small = XrInspector.Inspect(Glb.Write(f.Root, 1000));
        Assert.Equal(1000, small.Triangles); Assert.Equal(1, small.Primitives); Assert.Equal("Test", small.Generator);
        Assert.All(XrInspector.Budgets, b => Assert.True(XrInspector.Check(small, b).Ok));
        var big = XrInspector.Inspect(Glb.Write(f.Root, 1_000_000, 50));
        var q3 = XrInspector.Check(big, XrInspector.Budgets[0]);
        Assert.False(q3.Ok); Assert.Contains(q3.Problems, p => p.Contains("Dreiecke")); Assert.Contains(q3.Problems, p => p.Contains("Texturen"));
        Assert.True(XrInspector.Check(big, XrInspector.Budgets[3]).Ok);
        var bad = Path.Combine(f.Root, "x.glb"); File.WriteAllText(bad, "kaputt");
        Assert.Throws<InvalidOperationException>(() => XrInspector.Inspect(bad));
        Assert.Equal((13.89, true, 13.6), XrInspector.FrameBudget(72, 12));
    }
}

public class SPEC05_Science
{
    [Fact(DisplayName = "SPEC-05: CSV mit deutschen Zahlen, Kennzahlen, Regression, Welch-t-Test mit p-Wert")]
    public void Analyse()
    {
        var t = DataTable.Parse("Zeit;Temp;B\n1;2,0;5\n2;4,1;6\n3;5,9;5\n4;8,0;7\n5;10,0;\nx;y;6");
        Assert.Equal(["Zeit", "Temp", "B"], t.Columns);
        var d = t.Describe(); Assert.Equal(5, d[0].N); Assert.Equal(3, d[0].Mean); Assert.Equal(3, d[0].Median); Assert.Equal(Math.Sqrt(2.5), d[0].StdDev, 9);
        var r = t.Fit("Zeit", "Temp"); Assert.Equal(1.99, r.Slope, 9); Assert.True(r.R2 > 0.99);
        // Referenz (R: t.test(c(1,2,3,4,5), c(6,7,8,9,10))): t = -5, df = 8, p = 0,001053
        var w = Stats.Welch([1, 2, 3, 4, 5], [6, 7, 8, 9, 10]);
        Assert.Equal(-5, w.T, 9); Assert.Equal(8, w.Df, 9); Assert.Equal(0.001053, w.P, 5); Assert.True(w.Significant);
        Assert.False(Stats.Welch([1, 2, 3], [1.5, 2.5, 3.2]).Significant);
        Assert.Contains("| Temp | 5 |", Stats.Report(t));
        Assert.Throws<ArgumentException>(() => t.Values("Gibts"));
    }
}

public class SPEC06_Learning
{
    [Fact(DisplayName = "SPEC-06: Karteikarten mit SM-2-Abständen, Import, Fortschritt, dauerhaft gespeichert")]
    public void Cards()
    {
        using var f = new Fixture(); var now = new DateTime(2026, 10, 1, 9, 0, 0);
        var lb = new LearnBox(f.Root, () => now);
        var c = lb.Add("Englisch", "Haus", "house");
        Assert.Equal(2, lb.Import("Englisch", "Hund;dog\nKatze\tcat\nHaus;house\n# x;y\nkaputt"));
        Assert.Equal(3, lb.DueCards().Count);
        Assert.Equal(1, lb.Answer(c.Id, Grade.Gut).IntervalDays);
        now = now.AddDays(1); Assert.Equal(6, lb.Answer(c.Id, Grade.Gut).IntervalDays);
        now = now.AddDays(6); var c3 = lb.Answer(c.Id, Grade.Leicht); Assert.Equal(15, c3.IntervalDays); Assert.True(c3.Ease > 2.5);
        lb.Answer(c.Id, Grade.Falsch); Assert.Equal(1, c.IntervalDays); Assert.Equal(1, c.Lapses); Assert.Equal(0, c.Reps);
        Assert.DoesNotContain(new LearnBox(f.Root, () => now).DueCards(), x => x.Id == c.Id);
        Assert.Equal(3, new LearnBox(f.Root, () => now).Progress("Englisch").Total);
        Assert.Throws<ArgumentException>(() => lb.Add("x", "", "y"));
    }
}

public class SPEC08_Fashion
{
    [Fact(DisplayName = "SPEC-08: Größen aus Maßen, Schuhgrößen, Stoffbedarf, Varianten, Preiskalkulation")]
    public void Calc()
    {
        Assert.Equal(38, Fashion.FromBody(true, 88, 72, 96).Eu);
        Assert.Equal(40, Fashion.FromBody(true, 90, 72, 96).Eu);
        Assert.Contains("verschiedenen", Fashion.FromBody(true, 84, 90, 92).Note);
        Assert.Equal(52, Fashion.FromBody(false, 103, 90).Eu);
        Assert.Contains("US 6", Fashion.WomenSize(38));
        var s = Fashion.Shoe(42); Assert.Equal(26.5, s.Cm); Assert.Equal(8, s.Uk);
        Assert.Equal(42, Fashion.ShoeFromFoot(26.5));
        var f = Fashion.Fabric([(60, 80, 2)], 140); Assert.Equal(1, f.Rows); Assert.Equal(1.0, f.Meters); // 83 cm * 1,1 = 91,3 → 1,0 m
        Assert.Equal(2, Fashion.Fabric([(60, 80, 3)], 140).Rows);
        Assert.Throws<ArgumentException>(() => Fashion.Fabric([(200, 180, 1)], 140));
        var v = Fashion.Variants("Shirt-01", ["S", "M"], ["Schwarz", "Weiß", "Weiß"]);
        Assert.Equal(4, v.Count); Assert.Equal("SHIRT-01-SCHW-S", v[0].Sku); Assert.Contains(v, x => x.Sku == "SHIRT-01-WEIS-M");
        var p = Fashion.Price(8, 12, 20, 40); Assert.Equal(24, p.Cost); Assert.Equal(39.9, p.Gross); Assert.True(p.MarginPct > 25);
    }
}

public class SPEC09_Scan
{
    [Fact(DisplayName = "SPEC-09: Punktwolke aus PLY (ASCII/binär), XYZ, OBJ; ausdünnen, Ausreißer, PLY speichern")]
    public void Cloud()
    {
        using var f = new Fixture(); Directory.CreateDirectory(f.Root);
        var xyz = Path.Combine(f.Root, "a.xyz");
        var lines = new List<string>(); for (var i = 0; i < 10; i++) for (var j = 0; j < 10; j++) lines.Add(FormattableString.Invariant($"{i * 0.01} {j * 0.01} 0 200 100 50"));
        lines.Add("5 5 5 255 0 0"); File.WriteAllLines(xyz, lines);
        var pc = PointCloud.Load(xyz); var info = pc.Info();
        Assert.Equal(101, info.Points); Assert.True(info.HasColor); Assert.Equal(5, info.SizeZ);
        var clean = pc.RemoveOutliers(0.015, 2); Assert.Equal(100, clean.Points.Count);
        var ds = clean.Downsample(0.02); Assert.Equal(25, ds.Points.Count); Assert.Equal(200, ds.Colors![0].R);
        var ply = Path.Combine(f.Root, "o.ply"); ds.SavePly(ply);
        var back = PointCloud.Load(ply); Assert.Equal(25, back.Points.Count); Assert.Equal(ds.Info().Center.X, back.Info().Center.X, 5);
        // binäres PLY
        var bin = Path.Combine(f.Root, "b.ply");
        using (var w = new BinaryWriter(File.Create(bin)))
        {
            w.Write(System.Text.Encoding.ASCII.GetBytes("ply\nformat binary_little_endian 1.0\nelement vertex 2\nproperty float x\nproperty float y\nproperty float z\nproperty uchar red\nproperty uchar green\nproperty uchar blue\nelement face 0\nproperty list uchar int vertex_indices\nend_header\n"));
            w.Write(1f); w.Write(2f); w.Write(3f); w.Write((byte)9); w.Write((byte)8); w.Write((byte)7);
            w.Write(4f); w.Write(5f); w.Write(6f); w.Write((byte)1); w.Write((byte)2); w.Write((byte)3);
        }
        var b = PointCloud.Load(bin); Assert.Equal(new P3(4, 5, 6), b.Points[1]); Assert.Equal((byte)9, b.Colors![0].R);
        var obj = Path.Combine(f.Root, "m.obj"); File.WriteAllText(obj, "# x\nv 0 0 0\nv 1 0 0\nvn 0 0 1\nv 0 1 0\nf 1 2 3\n");
        Assert.Equal(3, PointCloud.Load(obj).Points.Count);
        Assert.Throws<InvalidOperationException>(() => PointCloud.Load(Path.Combine(f.Root, "a.xyz").Replace("a.xyz", "x.stl").Also()));
    }
}

static class TestExt { public static string Also(this string p) { File.WriteAllText(p, "solid"); return p; } }

public class SPEC10_Twin
{
    [Fact(DisplayName = "SPEC-10: Zwilling mit Sensoren, CSV-Import, Ampel, Trend und Zeit bis Alarm")]
    public void Twin()
    {
        using var f = new Fixture();
        var h = new TwinHub(f.Root);
        h.Define("Heizung", "Vorlauf", "°C", 70, 80);
        var (ok, bad) = h.Import("Zeit;Anlage;Sensor;Wert\n21.10.2026 10:00;Heizung;Vorlauf;60\n21.10.2026 11:00;Heizung;Vorlauf;62\n2026-10-21T12:00;Heizung;Vorlauf;64\nquatsch;Heizung;Vorlauf;1\n21.10.2026 12:00;Pumpe;Druck;1,5");
        Assert.Equal((4, 1), (ok, bad));
        var s = new TwinHub(f.Root).States();
        var v = s.First(x => x.Sensor == "Vorlauf");
        Assert.Equal(64, v.Last); Assert.Equal(TwinLevel.Ok, v.Level); Assert.Equal(2, v.TrendPerHour!.Value, 6); Assert.Equal(8, v.HoursToAlarm!.Value, 6);
        Assert.Equal(TwinLevel.Ok, s.First(x => x.Sensor == "Druck").Level);
        h.Record(new DateTime(2026, 10, 21, 13, 0, 0), "Heizung", "Vorlauf", 81);
        Assert.Equal(TwinLevel.Alarm, h.States().First(x => x.Sensor == "Vorlauf").Level);
        Assert.Equal(TwinLevel.Warnung, TwinHub.Level(h.Model.Assets[0].Sensors[0], 72));
        Assert.Throws<ArgumentException>(() => h.Define("a", "b", "", 80, 70));
    }
}

public class SPEC13_Geo
{
    public const string Gpx = """
<?xml version="1.0"?><gpx version="1.1" creator="t" xmlns="http://www.topografix.com/GPX/1/1">
<wpt lat="47.4210" lon="10.9850"><name>Gipfel</name></wpt>
<trk><trkseg>
<trkpt lat="47.4000" lon="10.9800"><ele>1000</ele><time>2026-10-01T08:00:00Z</time></trkpt>
<trkpt lat="47.4100" lon="10.9800"><ele>1001</ele><time>2026-10-01T08:30:00Z</time></trkpt>
<trkpt lat="47.4200" lon="10.9800"><ele>1200</ele><time>2026-10-01T09:30:00Z</time></trkpt>
<trkpt lat="47.4100" lon="10.9800"><ele>1100</ele><time>2026-10-01T10:00:00Z</time></trkpt>
</trkseg></trk></gpx>
""";

    [Fact(DisplayName = "SPEC-13: GPX lesen, km/Höhenmeter/Dauer, GeoJSON, Koordinaten DMS/UTM, Luftlinie")]
    public void Geo_()
    {
        using var f = new Fixture(); Directory.CreateDirectory(f.Root);
        var p = Path.Combine(f.Root, "t.gpx"); File.WriteAllText(p, Gpx);
        var (trk, wpt) = Geo.ReadGpx(p);
        Assert.Equal(4, trk.Count); Assert.Equal("Gipfel", wpt[0].Name);
        var s = Geo.Analyse(trk);
        Assert.Equal(3.336, s.DistanceKm, 2); Assert.Equal(200, s.AscentM); Assert.Equal(100, s.DescentM); Assert.Equal(TimeSpan.FromHours(2), s.Duration);
        Assert.Contains("\"LineString\"", Geo.GeoJson(trk, wpt, "t")); Assert.Contains("[10.985,47.421]", Geo.GeoJson(trk, wpt, "t"));
        Assert.Contains("<polyline", Geo.Svg(trk));
        Assert.Equal(504.2, Geo.Haversine(new(52.52, 13.405), new(48.1372, 11.5756)), 0);
        var (lat, lon) = Geo.ParseCoord("48°8'13.9\"N 11°34'32.2\"E"); Assert.Equal(48.1372, lat, 3); Assert.Equal(11.5756, lon, 3);
        Assert.Equal((-33.8688, 151.2093), Geo.ParseCoord("-33,8688 151,2093"));
        Assert.Equal("48°8'13.9\"N 11°34'32.2\"E", Geo.Dms(48.1372, 11.5756));
        // Referenz München (Marienplatz) ≈ 32U 691 607 O / 5 334 760 N
        var u = Geo.Utm(48.1372, 11.5756); Assert.Equal(32, u.Zone); Assert.Equal('U', u.Band); Assert.InRange(u.Easting, 691_557, 691_657); Assert.InRange(u.Northing, 5_334_710, 5_334_810);
        Assert.Throws<FormatException>(() => Geo.ParseCoord("95 10"));
    }
}

public class SPEC14_Cnc
{
    [Fact(DisplayName = "SPEC-14: G-Code (G0/G1/G2/G3, G20/G91), Wege, Zeit, Grenzen, Warnungen, Schnittwerte")]
    public void Gcode()
    {
        var g = "%\n(Quadrat) G21 G90\nT1 M6\nS18000 M3\nG0 X0 Y0 Z5\nG1 Z-1 F100\nG1 X100 F1000 ; unten\nY100\nX0\nY0\nG0 Z5\nG2 X20 Y0 I10 J0 F500\nM30\n%";
        var r = Cnc.Analyse(g, 3000);
        Assert.Equal(400 + 6 + Math.PI * 10, r.CutMm, 0);
        Assert.Equal(Math.Round(Math.Sqrt(25) + 6, 1), r.RapidMm);
        var expect = 6 / 100.0 + 400 / 1000.0 + Math.PI * 10 / 500.0 + (5 + 6) / 3000.0; // Minuten (Bogen: Z5 → 5 mm Eilgang-Ansatz 5 + 6 Rückzug)
        Assert.Equal(expect * 60, r.Time.TotalSeconds, 0);
        Assert.Equal((0, 0, -1, 100, 100, 5), (r.Box.X0, Math.Round(r.Box.Y0, 6), r.Box.Z0, r.Box.X1, r.Box.Y1, r.Box.Z1));
        Assert.Equal([1], r.Tools); Assert.Equal(18000, r.MaxSpindle); Assert.Empty(r.Warnings); Assert.Contains("<path", r.Svg);
        var w = Cnc.Analyse("G20 G91\nG1 X1 Y1\nG0 X1 Z-0.5", 3000, (20, 20, 50));
        Assert.Equal(Math.Sqrt(2) * 25.4, w.CutMm, 0);
        Assert.Contains(w.Warnings, x => x.Contains("ohne Vorschub")); Assert.Contains(w.Warnings, x => x.Contains("Eilgang schräg")); Assert.Contains(w.Warnings, x => x.Contains("Arbeitsraum"));
        Assert.Throws<InvalidOperationException>(() => Cnc.Analyse("hallo"));
        var fd = Cnc.Feeds("Aluminium", 6, 2); Assert.Equal(13263, fd.Rpm); Assert.Equal(1061, fd.FeedMmMin); Assert.True(Cnc.Feeds("Holz", 3, 1).Limited);
    }
}

public class SPEC15_Broadcast
{
    [Fact(DisplayName = "SPEC-15: Sendeablauf mit Zeiten, Überziehung, Kapitelmarken, Bitrate, Speicher")]
    public void Rundown()
    {
        using var f = new Fixture();
        var b = new BroadcastDesk(f.Root); b.Plan.Start = new DateTime(2026, 10, 21, 20, 0, 0); b.Plan.SlotMinutes = 10;
        b.Add("Intro", BroadcastDesk.ParseDuration("30")); b.Add("Interview", BroadcastDesk.ParseDuration("8:00")); b.Add("Abschied", BroadcastDesk.ParseDuration("2:00"));
        var r = b.Rows(); Assert.Equal(new DateTime(2026, 10, 21, 20, 8, 30), r[2].Start);
        Assert.Equal(TimeSpan.FromSeconds(30), b.Over);
        Assert.Equal("0:00 Intro\n0:30 Interview\n8:30 Abschied", b.Chapters());
        b.Move(2, -1); Assert.Equal("Abschied", new BroadcastDesk(f.Root).Plan.Segments[1].Title);
        Assert.Contains("10:30;Gesamt", b.Sheet());
        Assert.Equal(3720, BroadcastDesk.ParseDuration("1:02:00")); Assert.Throws<FormatException>(() => BroadcastDesk.ParseDuration("abc"));
        var p = BroadcastDesk.PlanStream("YouTube", "1080p60", 10); Assert.False(p.Fits); Assert.Contains("1080p30", p.Advice);
        Assert.True(BroadcastDesk.PlanStream("twitch", "720p30", 10).Fits);
        Assert.Equal(2.77, BroadcastDesk.StorageGb(6160, 60));
    }
}

public class SPEC16_Vehicle
{
    [Fact(DisplayName = "SPEC-16: Tankbuch (Volltank-Methode), Fahrtenbuch mit Lücken, Erinnerungen, CSV")]
    public void Book()
    {
        using var f = new Fixture(); var d = new DateTime(2026, 10, 1);
        var b = new VehicleBook(f.Root); var v = b.Add("Golf", "m-ab 123");
        b.Refuel("Golf", d, 10000, 40, 1.80);
        b.Refuel("Golf", d.AddDays(7), 10500, 20, 1.70, full: false);
        b.Refuel("Golf", d.AddDays(14), 11000, 15, 1.90);
        var s = VehicleBook.Stats(v); Assert.Equal(3.5, s.AvgPer100); Assert.Equal(1000, s.Km); Assert.Equal(Math.Round((20 * 1.7 + 15 * 1.9) / 1000, 3), s.CostPerKm);
        Assert.Throws<ArgumentException>(() => b.Refuel("Golf", d.AddDays(20), 10900, 10, 1.8));
        b.Drive("Golf", d, 11000, 11100, true, "Kunde Müller, Augsburg");
        b.Drive("Golf", d, 11150, 11200, false);
        Assert.Throws<ArgumentException>(() => b.Drive("Golf", d, 11180, 11190, false));
        Assert.Throws<ArgumentException>(() => b.Drive("Golf", d, 11300, 11400, true));
        Assert.Equal((100, 50, 66.7), VehicleBook.TripShare(v)); Assert.Equal([(11100.0, 11150.0)], VehicleBook.Gaps(v));
        v.InspectionDue = new DateTime(2026, 11, 1); v.ServiceDueKm = 12000;
        var r = VehicleBook.Reminders(v, d); Assert.True(r[0].Urgent); Assert.True(r[1].Urgent); Assert.Contains("800 km", r[1].Text);
        Assert.Contains("dienstlich;Kunde Müller", VehicleBook.LogCsv(v)); Assert.Equal("M-AB 123", new VehicleBook(f.Root).Get("golf").Plate);
    }
}

public static class Img
{
    /// <summary>Echte PNG-Datei (RGB, Filter 0) mit rotem Rechteck auf grauem Grund.</summary>
    public static string Png(string dir, int w, int h, (int X0, int Y0, int X1, int Y1) red)
    {
        var raw = new MemoryStream();
        for (var y = 0; y < h; y++) { raw.WriteByte(0); for (var x = 0; x < w; x++) { var r = x >= red.X0 && x <= red.X1 && y >= red.Y0 && y <= red.Y1; raw.Write(r ? [220, 30, 30] : [120, 120, 120]); } }
        var z = new MemoryStream(); using (var zs = new System.IO.Compression.ZLibStream(z, System.IO.Compression.CompressionLevel.Fastest, true)) raw.WriteTo(zs);
        var o = new MemoryStream(); o.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
        void Chunk(string t, byte[] d) { var be = BitConverter.GetBytes(d.Length); Array.Reverse(be); o.Write(be); o.Write(System.Text.Encoding.ASCII.GetBytes(t)); o.Write(d); o.Write([0, 0, 0, 0]); }
        var ih = new byte[13]; var bw = BitConverter.GetBytes(w); Array.Reverse(bw); var bh = BitConverter.GetBytes(h); Array.Reverse(bh); bw.CopyTo(ih, 0); bh.CopyTo(ih, 4); ih[8] = 8; ih[9] = 2;
        Chunk("IHDR", ih); Chunk("IDAT", z.ToArray()); Chunk("IEND", []);
        var p = Path.Combine(dir, "bild.png"); File.WriteAllBytes(p, o.ToArray()); return p;
    }
}

public class CORE09_Robotics
{
    [Fact(DisplayName = "CORE-09: PNG/BMP lesen, Farbobjekt finden, Kinematik hin und zurück, Fahrprogramm")]
    public void Vision_and_arm()
    {
        using var f = new Fixture(); Directory.CreateDirectory(f.Root);
        var img = Vision.Load(Img.Png(f.Root, 80, 60, (10, 20, 29, 39)));
        Assert.Equal((80, 60), (img.Width, img.Height));
        var b = Vision.Find(img, "rot")!; Assert.Equal(400, b.Pixels); Assert.Equal(19.5, b.CenterX); Assert.Equal(29.5, b.CenterY); Assert.Equal((10, 39), (b.MinX, b.MaxY));
        Assert.Null(Vision.Find(img, "blau"));
        var bmp = Path.Combine(f.Root, "b.bmp");
        using (var w = new BinaryWriter(File.Create(bmp))) { w.Write((byte)'B'); w.Write((byte)'M'); w.Write(54 + 8 * 2); w.Write(0); w.Write(54); w.Write(40); w.Write(2); w.Write(2); w.Write((short)1); w.Write((short)24); w.Write(0); w.Write(16); w.Write(0); w.Write(0); w.Write(0); w.Write(0);
            w.Write([255, 0, 0, 0, 255, 0, 0, 0]); w.Write([0, 0, 255, 255, 255, 255, 0, 0]); }
        var bi = Vision.Load(bmp); Assert.Equal(255, bi.Rgb[0]); Assert.Equal(255, bi.Rgb[3 * 2 + 2]); // oben links rot, unten links blau
        Assert.Equal((100, 75), Vision.PixelToTable(40, 30, 80, 60, 200, 150));
        var c = new ArmConfig();
        var j = RobotArm.Inverse(c, 150, 50, 40); var (x, y, z) = RobotArm.Forward(c, j);
        Assert.Equal(150, x, 2); Assert.Equal(50, y, 2); Assert.Equal(40, z, 2); Assert.Equal(18.435, j.Base, 2);
        Assert.Throws<ArgumentException>(() => RobotArm.Inverse(c, 300, 0, 60));
        var p = RobotArm.Program(c, "150 0 100\n# Kommentar\n0 150 100");
        Assert.Equal(2, p.Count); Assert.Equal(1, p[1].Seconds, 3); // 90° Drehung bei 90°/s
        Assert.Contains("Basis_Grad", RobotArm.Csv(p));
        Assert.Contains("Zeile 1", Assert.Throws<ArgumentException>(() => RobotArm.Program(c, "500 0 0")).Message);
    }
}

public class SPEC01_Plus
{
    private static readonly Room R = new("Bad", 3, 2, 2.5, 1, 1);
    [Fact(DisplayName = "SPEC-01 F08: Fliesenbedarf")] public void F08() { var t = Interior.Tiles(6, 30, 60, 3, 10, 1.44); Assert.Equal(37, t.Tiles); Assert.Equal(5, t.Packs); Assert.True(t.GroutKg > 0); Assert.Throws<ArgumentException>(() => Interior.Tiles(0, 30, 30)); }
    [Fact(DisplayName = "SPEC-01 F09: Tapetenrollen")] public void F09() { var w = Interior.Wallpaper(R); Assert.Equal(3, w.StripsPerRoll); Assert.Equal(17, w.Strips); Assert.Equal(6, w.Rolls); Assert.Equal(2, Interior.Wallpaper(R, repeatCm: 100).StripsPerRoll); }
    [Fact(DisplayName = "SPEC-01 F10: Möbel-Stellprüfung")] public void F10() { Assert.True(Interior.FurnitureFits(R, 2.8, 0.6).Fits); Assert.False(Interior.FurnitureFits(R, 2.8, 1.5).Fits); Assert.Contains("größer", Interior.FurnitureFits(R, 4, 4).Why); }
    [Fact(DisplayName = "SPEC-01 F11: Heizlast-Schätzung")] public void F11() { Assert.Equal(540, Interior.HeatLoad(R, "teilsaniert")); Assert.Equal(90, Interior.HeatLoad(R, "passivhaus")); Assert.Throws<ArgumentException>(() => Interior.HeatLoad(R, "iglu")); }
    [Fact(DisplayName = "SPEC-01 F12: Licht-Bedarf")] public void F12() { var l = Interior.Light(R, "bad"); Assert.Equal(2000, l.Lumen); Assert.Equal(3, l.Bulbs806); }
    [Fact(DisplayName = "SPEC-01 F13: Kostenschätzung Renovierung")] public void F13() { var c = Interior.Cost(new MaterialNeed(12, 2, 10, 17.1, 20, 60), 45, 25, 3, 2, 30); Assert.Equal((90, 250, 52.5, 60, 452.5), (c.Paint, c.Floor, c.Skirting, c.Tiles, c.Total)); }
    [Fact(DisplayName = "SPEC-01 F14: Treppe berechnen")] public void F14() { var s = Interior.Stairs(275); Assert.Equal(16, s.Steps); Assert.Equal(17.2, s.RiseCm); Assert.Equal(28.6, s.TreadCm); Assert.Contains("Bequem", s.Verdict); Assert.Contains("unbequem", Interior.Stairs(275, 300).Verdict); }
    [Fact(DisplayName = "SPEC-01 F15: Wohnfläche nach WoFlV")] public void F15() { Assert.Equal(67, Interior.LivingArea(60, 10, 4, 8)); Assert.Throws<ArgumentException>(() => Interior.LivingArea(-1)); }
    [Fact(DisplayName = "SPEC-01 F16: Raumliste als CSV")] public void F16() { var c = Interior.RoomsCsv([R]); Assert.Contains("Bad;3,00;2,00;2,50;6,00", c); Assert.StartsWith("Raum;", c); }
}

public class SPEC02_Plus
{
    [Fact(DisplayName = "SPEC-02 F07: Reihe & parallel")] public void F07() { Assert.Equal(500, ElectronicsPlus.Combine([1000, 1000], true), 6); Assert.Equal(3000, ElectronicsPlus.Combine([1000, 2000], false)); Assert.Equal(2e-6, ElectronicsPlus.Combine([1e-6, 1e-6], true, cap: true), 12); Assert.Equal(0.5e-6, ElectronicsPlus.Combine([1e-6, 1e-6], false, cap: true), 12); }
    [Fact(DisplayName = "SPEC-02 F08: Kondensator-Code")] public void F08() { Assert.Equal(100e-9, ElectronicsPlus.CapCode("104"), 15); Assert.Equal("104", ElectronicsPlus.ToCapCode(100e-9)); Assert.Equal("472", ElectronicsPlus.ToCapCode(4.7e-9)); Assert.Throws<FormatException>(() => ElectronicsPlus.CapCode("1a4")); }
    [Fact(DisplayName = "SPEC-02 F09: RC-Glied")] public void F09() { var r = ElectronicsPlus.Rc(10000, 100e-9); Assert.Equal(1e-3, r.Tau, 9); Assert.Equal(159.15, r.Cutoff, 2); Assert.Equal(5e-3, r.Full, 9); }
    [Fact(DisplayName = "SPEC-02 F10: 555-Timer")] public void F10() { var t = ElectronicsPlus.Timer555(1000, 10000, 10e-6); Assert.Equal(6.87, t.Hz, 2); Assert.Equal(52.38, t.DutyPct, 2); }
    [Fact(DisplayName = "SPEC-02 F11: Leiterbahnbreite")] public void F11() { var w = ElectronicsPlus.TraceWidthMm(1, 10, 35); Assert.InRange(w, 0.25, 0.32); Assert.True(ElectronicsPlus.TraceWidthMm(1, 10, 35, outer: false) > w); }
    [Fact(DisplayName = "SPEC-02 F12: Akku-Laufzeit")] public void F12() { Assert.Equal(34, ElectronicsPlus.BatteryHours(2000, 50)); Assert.Throws<ArgumentException>(() => ElectronicsPlus.BatteryHours(0, 1)); }
    [Fact(DisplayName = "SPEC-02 F13: Kabel-Spannungsabfall")] public void F13() { var d = ElectronicsPlus.CableDrop(5, 10, 1.5, 12); Assert.Equal(1.187, d.DropV); Assert.Equal(9.89, d.DropPct); }
    [Fact(DisplayName = "SPEC-02 F14: SMD-Widerstandscode")] public void F14() { Assert.Equal(4700, ElectronicsPlus.SmdCode("472")); Assert.Equal(47000, ElectronicsPlus.SmdCode("4702")); Assert.Equal(4.7, ElectronicsPlus.SmdCode("4R7")); Assert.Equal(10000, ElectronicsPlus.SmdCode("01C")); Assert.Equal(4990, ElectronicsPlus.SmdCode("68B")); }
    [Fact(DisplayName = "SPEC-02 F15: Linearregler-Verlust")] public void F15() { var r = ElectronicsPlus.Regulator(12, 5, 0.5); Assert.Equal(3.5, r.Watt); Assert.Equal(200, r.TjNoSink); Assert.True(r.NeedsSink); Assert.Equal(23.1, r.SinkKPerW); Assert.False(ElectronicsPlus.Regulator(5, 3.3, 0.1).NeedsSink); }
    [Fact(DisplayName = "SPEC-02 F16: Einheiten-Präfixe")] public void F16() { Assert.Equal(4700, ElectronicsPlus.Si("4k7")); Assert.Equal(100e-9, ElectronicsPlus.Si("100n"), 15); Assert.Equal(2.2e-6, ElectronicsPlus.Si("2,2µ"), 15); Assert.Equal(3.3, ElectronicsPlus.Si("3R3")); Assert.Equal("4,7 kΩ", ElectronicsPlus.Fmt(4700, "Ω")); }
}

public class SPEC03_Plus
{
    [Fact(DisplayName = "SPEC-03 F08: Hohlprofil")] public void F08() { var s = EngineeringPlus.HollowRect(60, 40, 3); Assert.Equal(564, s.A); Assert.Equal((60.0 * 64000 - 54.0 * 34 * 34 * 34) / 12, s.I, 6); Assert.Throws<ArgumentException>(() => EngineeringPlus.HollowRect(10, 10, 5)); }
    [Fact(DisplayName = "SPEC-03 F09: Knicken (Euler)")] public void F09() { var i = Engineering.Tube(40, 2).I; var f2 = EngineeringPlus.EulerKn(2, 210, i, 2.5); Assert.Equal(Math.Round(Math.PI * Math.PI * 210000 * i / (2500.0 * 2500) / 1000, 2), f2); Assert.Equal(f2 / 4, EngineeringPlus.EulerKn(1, 210, i, 2.5), 1); Assert.Equal(f2 * 4, EngineeringPlus.EulerKn(4, 210, i, 2.5), 1); }
    [Fact(DisplayName = "SPEC-03 F10: Schrauben")] public void F10() { Assert.Equal((25, 17), EngineeringPlus.Bolt("M8")); Assert.Equal(36.8, EngineeringPlus.Bolt("m8", "10.9").Nm); Assert.Throws<ArgumentException>(() => EngineeringPlus.Bolt("M7")); }
    [Fact(DisplayName = "SPEC-03 F11: Zug & Druck")] public void F11() { var r = EngineeringPlus.Axial(20, 200, 210, 1); Assert.Equal(100, r.Sigma); Assert.Equal(0.4762, r.StrainPermille); Assert.Equal(0.476, r.DeltaMm); }
    [Fact(DisplayName = "SPEC-03 F12: Torsion Welle")] public void F12() { var r = EngineeringPlus.Torsion(50, 20, 0.5); Assert.Equal(31.83, r.Tau); Assert.Equal(1.129, r.PhiDeg); }
    [Fact(DisplayName = "SPEC-03 F13: Druckfeder")] public void F13() { var r = EngineeringPlus.Spring(2, 20, 8, 50); Assert.Equal(2.547, r.RateNmm); Assert.Equal(19.63, r.TravelMm); }
    [Fact(DisplayName = "SPEC-03 F14: Zahnräder")] public void F14() { var g = EngineeringPlus.Gears(12, 36, 1500, 2); Assert.Equal((3, 500, 5.88), (g.Ratio, g.Rpm2, g.Torque2)); }
    [Fact(DisplayName = "SPEC-03 F15: Wärmedurchgang Wand")] public void F15() { var r = EngineeringPlus.UValue([(0.24, 0.5)], 10, 30); Assert.Equal(1.538, r.U); Assert.Equal(461.5, r.Watt); Assert.True(EngineeringPlus.UValue([(0.24, 0.5), (0.12, 0.035)], 1, 1).U < 0.28); }
    [Fact(DisplayName = "SPEC-03 F16: Gewicht")] public void F16() { Assert.Equal(2.7, EngineeringPlus.MassKg(Engineering.Mat("Aluminium"), 1e6)); Assert.Equal(7.85, EngineeringPlus.MassKg(Engineering.Mat("Baustahl"), 1e6)); }
}

public static class Gltf
{
    public static byte[] TinyPng(int w, int h) { var b = new byte[33]; new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 13, 0x49, 0x48, 0x44, 0x52 }.CopyTo(b, 0); b[16] = (byte)(w >> 24); b[17] = (byte)(w >> 16); b[18] = (byte)(w >> 8); b[19] = (byte)w; b[20] = (byte)(h >> 24); b[21] = (byte)(h >> 16); b[22] = (byte)(h >> 8); b[23] = (byte)h; return b; }

    /// <summary>GLB mit JSON- und BIN-Teil, eingebetteter PNG-Textur, Namen, Animation, Erweiterungen.</summary>
    public static string Write(string dir, double size = 2, string ext = "KHR_draco_mesh_compression")
    {
        var png = TinyPng(1024, 512); var binLen = (png.Length + 3) / 4 * 4;
        var json = $"{{\"asset\":{{\"version\":\"2.0\"}},\"extensionsUsed\":[\"{ext}\"],\"extensionsRequired\":[\"{ext}\"],\"nodes\":[{{\"name\":\"Stuhl\",\"mesh\":0}},{{\"name\":\"Tisch\"}}],\"meshes\":[{{\"name\":\"StuhlMesh\",\"primitives\":[{{\"attributes\":{{\"POSITION\":0}},\"material\":0}},{{\"attributes\":{{\"POSITION\":0}},\"material\":0}},{{\"attributes\":{{\"POSITION\":0}}}}]}}],\"materials\":[{{\"name\":\"Holz\"}}],\"accessors\":[{{\"count\":30,\"min\":[0,0,0],\"max\":[{size.ToString(System.Globalization.CultureInfo.InvariantCulture)},{(size / 2).ToString(System.Globalization.CultureInfo.InvariantCulture)},{(size / 4).ToString(System.Globalization.CultureInfo.InvariantCulture)}]}}],\"animations\":[{{\"name\":\"Wackeln\"}}],\"bufferViews\":[{{\"buffer\":0,\"byteOffset\":0,\"byteLength\":{png.Length}}}],\"images\":[{{\"name\":\"Holz_Farbe\",\"bufferView\":0,\"mimeType\":\"image/png\"}}],\"textures\":[{{\"source\":0}}]}}";
        while (json.Length % 4 != 0) json += " ";
        var j = System.Text.Encoding.UTF8.GetBytes(json); var p = Path.Combine(dir, "szene.glb");
        using var w = new BinaryWriter(File.Create(p));
        w.Write(0x46546C67u); w.Write(2u); w.Write((uint)(12 + 8 + j.Length + 8 + binLen)); w.Write((uint)j.Length); w.Write(0x4E4F534Au); w.Write(j);
        w.Write((uint)binLen); w.Write(0x004E4942u); w.Write(png); w.Write(new byte[binLen - png.Length]);
        return p;
    }
}

public class SPEC04_Plus
{
    [Fact(DisplayName = "SPEC-04 F02: glTF lesen")] public void F02() { using var f = new Fixture(); Directory.CreateDirectory(f.Root); var p = Path.Combine(f.Root, "a.gltf"); File.WriteAllText(p, "{\"asset\":{\"version\":\"2.0\"},\"meshes\":[{\"primitives\":[{\"attributes\":{\"POSITION\":0},\"indices\":1}]}],\"accessors\":[{\"count\":4},{\"count\":6}]}"); var i = XrInspector.Inspect(p); Assert.Equal(2, i.Triangles); Assert.Equal(4, i.Vertices); }
    [Fact(DisplayName = "SPEC-04 F06: Material-Check")] public void F06() { var i = new XrModelInfo("x", 10, 1, 1, 10, 10, 0, 0, 1, 0, ""); Assert.Contains(XrInspector.Check(i, XrInspector.Budgets[0]).Problems, p => p.Contains("Kein Material")); }
    [Fact(DisplayName = "SPEC-04 F07: Texturgrößen & Grafikspeicher")] public void F07() { using var f = new Fixture(); Directory.CreateDirectory(f.Root); var d = XrPlus.Detail(Gltf.Write(f.Root)); var t = Assert.Single(d.Textures); Assert.Equal((1024, 512, "Holz_Farbe"), (t.Width, t.Height, t.Name)); Assert.Equal(2796202, t.VramBytes); var jpg = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0, 4, 0, 0, 0xFF, 0xC0, 0, 11, 8, 0, 200, 1, 44, 3, 0, 0, 0 }; Assert.Equal((300, 200), XrPlus.ImageSize(jpg)); }
    [Fact(DisplayName = "SPEC-04 F08: Maßstab prüfen")] public void F08() { using var f = new Fixture(); Directory.CreateDirectory(f.Root); Assert.StartsWith("✓", XrPlus.ScaleVerdict(XrPlus.Detail(Gltf.Write(f.Root)))); Assert.Contains("falsche Einheit", XrPlus.ScaleVerdict(XrPlus.Detail(Gltf.Write(f.Root, 0.002)))); Assert.Contains("mm/cm", XrPlus.ScaleVerdict(XrPlus.Detail(Gltf.Write(f.Root, 2000)))); }
    [Fact(DisplayName = "SPEC-04 F09: Texel-Dichte")] public void F09() { Assert.Equal(1024, XrPlus.TexelDensity(2048, 2).PxPerM); Assert.StartsWith("✓", XrPlus.TexelDensity(2048, 2).Verdict); Assert.StartsWith("⚠", XrPlus.TexelDensity(256, 2).Verdict); }
    [Fact(DisplayName = "SPEC-04 F10: Bewegungs-Komfort")] public void F10() { Assert.Equal(100, XrPlus.Comfort(1.4, 0, true, true).Score); var c = XrPlus.Comfort(4, 120, false, false, 2); Assert.Equal(25, c.Score); Assert.Equal(4, c.Tips.Count); }
    [Fact(DisplayName = "SPEC-04 F11: Szenenliste")] public void F11() { using var f = new Fixture(); Directory.CreateDirectory(f.Root); var d = XrPlus.Detail(Gltf.Write(f.Root)); Assert.Equal(["Stuhl", "Tisch"], d.Nodes); Assert.Equal(["StuhlMesh"], d.MeshNames); }
    [Fact(DisplayName = "SPEC-04 F12: Animationen")] public void F12() { using var f = new Fixture(); Directory.CreateDirectory(f.Root); Assert.Equal(["Wackeln"], XrPlus.Detail(Gltf.Write(f.Root)).Animations); }
    [Fact(DisplayName = "SPEC-04 F13: Prüfbericht")] public void F13() { using var f = new Fixture(); Directory.CreateDirectory(f.Root); var r = XrPlus.Report(XrPlus.Detail(Gltf.Write(f.Root))); Assert.Contains("# VR/AR-Prüfbericht: szene.glb", r); Assert.Contains("Holz_Farbe: 1024 × 512", r); Assert.Contains("Meta Quest 3", r); }
    [Fact(DisplayName = "SPEC-04 F14: LOD-Plan")] public void F14() { var l = XrPlus.LodPlan(200000, 2); Assert.Equal(4, l.Count); Assert.Equal((3, 25000L, 40.0), l[3]); Assert.Equal(0, l[0].FromM); }
    [Fact(DisplayName = "SPEC-04 F15: Zeichenaufrufe je Material")] public void F15() { using var f = new Fixture(); Directory.CreateDirectory(f.Root); var d = XrPlus.Detail(Gltf.Write(f.Root)).DrawCallsPerMaterial; Assert.Equal(2, d["Holz"]); Assert.Equal(1, d["(ohne Material)"]); }
    [Fact(DisplayName = "SPEC-04 F16: Erweiterungen")] public void F16() { using var f = new Fixture(); Directory.CreateDirectory(f.Root); var n = XrPlus.ExtensionNotes(XrPlus.Detail(Gltf.Write(f.Root))); Assert.Contains("Pflicht: KHR_draco_mesh_compression", Assert.Single(n)); Assert.Contains("unbekannt", XrPlus.ExtensionNotes(XrPlus.Detail(Gltf.Write(f.Root, 2, "VENDOR_x"))).Single()); }
}

public class SPEC05_Plus
{
    [Fact(DisplayName = "SPEC-05 F05: Zusammenhang deuten")] public void F05() { Assert.StartsWith("sehr starker positiver", StatsPlus.Describe(0.9)); Assert.StartsWith("mittlerer negativer", StatsPlus.Describe(-0.4)); Assert.Equal("kein erkennbarer Zusammenhang", StatsPlus.Describe(0.05)); }
    // Handrechnung: d = −2,−2,−1,−2,−3 → Mittel −2, s = √0,5 → t = −2/(√0,5/√5) = −6,3246; df 4 → p ≈ 0,0032
    [Fact(DisplayName = "SPEC-05 F07: Gepaarter t-Test")] public void F07() { var r = StatsPlus.Paired([10, 12, 9, 11, 13], [12, 14, 10, 13, 16]); Assert.Equal(-2 / (Math.Sqrt(0.5) / Math.Sqrt(5)), r.T, 9); Assert.Equal(4, r.Df); Assert.InRange(r.P, 0.0030, 0.0034); Assert.True(r.Significant); Assert.Throws<ArgumentException>(() => StatsPlus.Paired([1, 2], [1])); }
    [Fact(DisplayName = "SPEC-05 F08: Quartile & Ausreißer")] public void F08() { var q = StatsPlus.Quartiles([1, 2, 3, 4, 5, 6, 7, 8, 100]); Assert.Equal((3, 5, 7, 4), (q.Q1, q.Q2, q.Q3, q.Iqr)); Assert.Equal([100.0], q.Outliers); }
    [Fact(DisplayName = "SPEC-05 F09: Histogramm")] public void F09() { var h = StatsPlus.Histogram(Enumerable.Range(1, 16).Select(i => (double)i).ToArray()); Assert.Equal(5, h.Count); Assert.Equal(16, h.Sum(x => x.Count)); Assert.Contains("<rect", StatsPlus.HistogramSvg(h, "Test")); }
    [Fact(DisplayName = "SPEC-05 F10: Konfidenzintervall")] public void F10() { Assert.Equal(2.776, StatsPlus.TCrit(4), 3); var c = StatsPlus.Confidence([1, 2, 3, 4, 5]); Assert.Equal(1.9632, c.Half, 3); }
    // Referenz R: chisq.test(matrix(c(20,40,30,10),2), correct=FALSE): X² = 16,667, p = 4,456e-05
    [Fact(DisplayName = "SPEC-05 F11: Chi²-Test")] public void F11() { var r = StatsPlus.ChiSquare([[20, 30], [40, 10]]); Assert.Equal(16.667, r.Chi2, 3); Assert.Equal(1, r.Df); Assert.Equal(4.456e-5, r.P, 7); Assert.True(r.LowExpected == false); Assert.Equal(0.3173, StatsPlus.GammaQ(0.5, 0.5), 4); }
    [Fact(DisplayName = "SPEC-05 F12: Fehlerfortpflanzung")] public void F12() { var s = StatsPlus.Propagate('+', (10, 0.3), (5, 0.4)); Assert.Equal((15, 0.5), (s.Value, Math.Round(s.Uncertainty, 9))); var p = StatsPlus.Propagate('*', (10, 0.1), (5, 0.05)); Assert.Equal(50, p.Value); Assert.Equal(50 * Math.Sqrt(0.0002), p.Uncertainty, 9); }
    [Fact(DisplayName = "SPEC-05 F13: Stichprobengröße")] public void F13() { Assert.Equal(97, StatsPlus.SampleSize(10, 2)); Assert.Equal(385, StatsPlus.SampleSizeShare(0.05)); }
    [Fact(DisplayName = "SPEC-05 F14: Streudiagramm")] public void F14() { var s = StatsPlus.ScatterSvg([1, 2, 3], [2, 4, 6.1], "t"); Assert.Equal(3, System.Text.RegularExpressions.Regex.Matches(s, "<circle").Count); Assert.Contains("<line", s); }
    [Fact(DisplayName = "SPEC-05 F15: Glätten")] public void F15() { Assert.Equal([null, 2, 3, 4, null], StatsPlus.MovingAverage([1, 2, 3, 4, 5], 3)); Assert.Throws<ArgumentException>(() => StatsPlus.MovingAverage([1], 2)); Assert.Contains("x;y;y_g\n1;2;\n2;4;4\n3;6;", StatsPlus.AppendColumn("x;y\n1;2\n2;4\n3;6", "y_g", [null, 4, null], 1)); }
    [Fact(DisplayName = "SPEC-05 F16: Standardisieren")] public void F16() { var z = StatsPlus.ZScores([2, 4, 6]); Assert.Equal([-1, 0, 1], z); }
}

public class SPEC06_Plus
{
    private static (Fixture F, LearnBox L, Func<DateTime> Now, Action<int> Add) Box()
    {
        var f = new Fixture(); var now = new DateTime(2026, 10, 1, 9, 0, 0); var l = new LearnBox(f.Root, () => now);
        l.Import("En", "Hund;dog\nKatze;cat\nHaus;house\nBaum;tree"); return (f, l, () => now, d => now = now.AddDays(d));
    }
    [Fact(DisplayName = "SPEC-06 F07: Export")] public void F07() { var (f, l, _, _) = Box(); using var _f = f; var e = l.Export("En"); Assert.Equal(4, new LearnBox(f.Root + "x").Import("En", e)); Assert.Contains("Hund;dog", e); }
    [Fact(DisplayName = "SPEC-06 F08: Bearbeiten & löschen")] public void F08() { var (f, l, _, _) = Box(); using var _f = f; var c = l.Store.Cards[0]; l.Edit(c.Id, "Hündin", "dog (female)"); Assert.Equal("Hündin", new LearnBox(f.Root).Store.Cards[0].Front); l.Delete(c.Id); Assert.Equal(3, l.Store.Cards.Count); Assert.Throws<ArgumentException>(() => l.Edit("x", "a", "b")); }
    [Fact(DisplayName = "SPEC-06 F09: Quiz mit Auswahl")] public void F09() { var (f, l, _, _) = Box(); using var _f = f; var q = l.Quiz(seed: 1); Assert.Equal(4, q.Options.Count); Assert.Equal(q.Card.Back, q.Options[q.Correct]); Assert.Equal(4, q.Options.Distinct().Count()); }
    [Fact(DisplayName = "SPEC-06 F10: Eintippen")] public void F10() { Assert.Equal((true, false), LearnBox.CheckTyped("House", " house ")); Assert.Equal((true, true), LearnBox.CheckTyped("elephant", "elephat")); Assert.False(LearnBox.CheckTyped("dog", "cat").Ok); Assert.Equal(3, LearnBox.Levenshtein("kitten", "sitting")); }
    [Fact(DisplayName = "SPEC-06 F11: Lernserie")] public void F11() { var (f, l, _, add) = Box(); using var _f = f; var id = l.Store.Cards[0].Id; l.Answer(id, Grade.Gut); add(1); l.Answer(id, Grade.Gut); add(1); Assert.Equal((2, false), l.Streak()); l.Answer(id, Grade.Gut); Assert.Equal((3, true), l.Streak()); add(2); Assert.Equal(0, l.Streak().Streak); }
    [Fact(DisplayName = "SPEC-06 F12: Vorschau 7 Tage")] public void F12() { var (f, l, _, _) = Box(); using var _f = f; l.Answer(l.Store.Cards[0].Id, Grade.Gut); var fc = l.Forecast(); Assert.Equal(7, fc.Count); Assert.Equal(3, fc[0].Due); Assert.Equal(1, fc[1].Due); }
    [Fact(DisplayName = "SPEC-06 F13: Stapel umbenennen")] public void F13() { var (f, l, _, _) = Box(); using var _f = f; Assert.Equal(4, l.RenameDeck("en", "Englisch")); Assert.Equal(["Englisch"], l.Decks); Assert.Throws<ArgumentException>(() => l.RenameDeck("xx", "y")); }
    [Fact(DisplayName = "SPEC-06 F14: Problemkarten")] public void F14() { var (f, l, _, _) = Box(); using var _f = f; var id = l.Store.Cards[1].Id; for (var i = 0; i < 3; i++) l.Answer(id, Grade.Falsch); Assert.Equal(id, Assert.Single(l.Leeches()).Id); }
    [Fact(DisplayName = "SPEC-06 F15: Umgekehrte Karten")] public void F15() { var (f, l, _, _) = Box(); using var _f = f; Assert.Equal(4, l.AddReversed("En")); Assert.Equal(0, l.AddReversed("En")); Assert.Contains(l.Store.Cards, c => c.Deck == "En (umgekehrt)" && c.Front == "dog" && c.Back == "Hund"); }
    [Fact(DisplayName = "SPEC-06 F16: Prüfungsplan")] public void F16() { var (f, l, now, _) = Box(); using var _f = f; var p = l.ExamPlan(now().AddDays(5)); Assert.Equal((2, 5, 4), (p.NewPerDay, p.DaysLeft, p.NewCards)); Assert.Throws<ArgumentException>(() => l.ExamPlan(now())); }
}

public class SPEC08_Plus
{
    [Fact(DisplayName = "SPEC-08 F10: Kindergrößen")] public void F10() { Assert.Equal(98, FashionPlus.KidSize(95).Size); Assert.Equal(104, FashionPlus.KidSize(100).Size); Assert.Equal("4–6 Monate", FashionPlus.KidSize(66).Age); Assert.Throws<ArgumentException>(() => FashionPlus.KidSize(200)); }
    [Fact(DisplayName = "SPEC-08 F11: BH-Größe")] public void F11() { Assert.Equal("75B", FashionPlus.BraSize(75, 89)); Assert.Equal("75D", FashionPlus.BraSize(75, 93)); Assert.Equal("80A", FashionPlus.BraSize(78, 90)); }
    [Fact(DisplayName = "SPEC-08 F12: Ringgröße")] public void F12() { var r = FashionPlus.Ring(54); Assert.Equal(54, r.Eu); Assert.Equal(17.2, r.DiameterMm); Assert.Equal(7, r.Us); }
    [Fact(DisplayName = "SPEC-08 F13: Textilkennzeichnung")] public void F13() { var ok = FashionPlus.TextileLabel("5% Elasthan, 95 % Baumwolle"); Assert.True(ok.Ok); Assert.Equal("95 % Baumwolle, 5 % Elasthan", ok.Label); var bad = FashionPlus.TextileLabel("80 % Cotton, 10 % Plastik"); Assert.False(bad.Ok); Assert.Equal(3, bad.Problems.Count); }
    [Fact(DisplayName = "SPEC-08 F14: Gradierung")] public void F14() { var g = FashionPlus.Grade(["S", "M", "L"], "M", 100, 4); Assert.Equal([("S", 96.0), ("M", 100.0), ("L", 104.0)], g); }
    [Fact(DisplayName = "SPEC-08 F15: Garnbedarf")] public void F15() { var y = FashionPlus.Yarn(50, 60, 22, 30, 125); Assert.Equal((110, 180), (y.Stitches, y.Rows)); Assert.Equal(297, y.Meters); Assert.Equal(3, y.Balls); }
    [Fact(DisplayName = "SPEC-08 F16: EAN-13")] public void F16() { Assert.Equal(1, FashionPlus.EanCheckDigit("400638133393")); Assert.True(FashionPlus.EanValid("4006381333931")); Assert.False(FashionPlus.EanValid("4006381333932")); }
    [Fact(DisplayName = "SPEC-08 F17: Staffelpreise")] public void F17() { var r = FashionPlus.Tier(29.9, 50, [(10, 5), (25, 10), (100, 15)]); Assert.Equal((26.91, 1345.5, 10), (r.Unit, r.Total, r.DiscountPct)); Assert.Equal(0, FashionPlus.Tier(10, 1, [(10, 5)]).DiscountPct); }
    [Fact(DisplayName = "SPEC-08 F18: Produktdatenblatt")] public void F18() { var md = FashionPlus.DataSheet("Shirt", "S-1", "Weich.", "100 % Baumwolle", Fashion.Variants("S-1", ["M"], ["Rot"]), Fashion.Price(5, 5, 0, 50)); Assert.Contains("# Shirt", md); Assert.Contains("| S-1-ROT-M | M | Rot |", md); Assert.Contains("inkl. MwSt.", md); }
}

public class SPEC09_Plus
{
    static PointCloud Grid(int n, double step, double z = 0) { var pc = new PointCloud(); for (var i = 0; i < n; i++) for (var j = 0; j < n; j++) pc.Points.Add(new(i * step, j * step, z)); return pc; }
    [Fact(DisplayName = "SPEC-09 F10: Skalieren")] public void F10() { Assert.Equal(0.001, ScanPlus.UnitFactor("mm", "m")); var s = ScanPlus.Scale(Grid(2, 1000), ScanPlus.UnitFactor("mm", "m")); Assert.Equal(1, s.Info().SizeX, 9); Assert.Throws<ArgumentException>(() => ScanPlus.UnitFactor("yard", "m")); }
    [Fact(DisplayName = "SPEC-09 F11: Zentrieren")] public void F11() { var pc = Grid(3, 1, 5); var c = ScanPlus.Center(pc).Info(); Assert.Equal((-1.0, 1.0, 0.0), (c.Min.X, c.Max.X, c.Min.Z)); }
    [Fact(DisplayName = "SPEC-09 F12: Zuschneiden")] public void F12() { var c = ScanPlus.Crop(Grid(10, 1), new(0, 0, -1), new(2, 2, 1)); Assert.Equal(9, c.Points.Count); }
    [Fact(DisplayName = "SPEC-09 F13: Boden finden")] public void F13() { var pc = Grid(10, 0.1); pc.Points.Add(new(0.5, 0.5, 1)); pc.Points.Add(new(0.3, 0.2, 0.7)); var pl = ScanPlus.FindFloor(pc, 0.01); Assert.Equal(100, pl.Inliers); Assert.Equal(0, pl.TiltDeg, 3); }
    [Fact(DisplayName = "SPEC-09 F14: Boden entfernen")] public void F14() { var pc = Grid(10, 0.1); pc.Points.Add(new(0.5, 0.5, 1)); var r = ScanPlus.RemovePlane(pc, ScanPlus.FindFloor(pc, 0.01), 0.01); Assert.Single(r.Points); Assert.Equal(1, r.Points[0].Z); }
    [Fact(DisplayName = "SPEC-09 F15: Scans vergleichen")] public void F15() { var a = Grid(5, 1, 0.1); var b = Grid(5, 1); a.Points.Add(new(2, 2, 3)); var d = ScanPlus.Compare(a, b); Assert.Equal(3, d.Max, 9); Assert.Equal((25 * 0.1 + 3) / 26, d.Mean, 9); }
    [Fact(DisplayName = "SPEC-09 F16: Volumen")] public void F16() { var pc = new PointCloud(); for (var i = 0; i < 10; i++) for (var j = 0; j < 10; j++) { pc.Points.Add(new(i * 0.1 + 0.05, j * 0.1 + 0.05, 0)); pc.Points.Add(new(i * 0.1 + 0.05, j * 0.1 + 0.05, 2)); } Assert.Equal(2, ScanPlus.Volume(pc, 0.1), 9); }
    [Fact(DisplayName = "SPEC-09 F17: XYZ speichern")] public void F17() { var pc = new PointCloud(); pc.Points.Add(new(1.5, -2, 0.25)); Assert.Equal("1.5 -2 0.25\n", ScanPlus.ToXyz(pc)); }
    [Fact(DisplayName = "SPEC-09 F18: Punktdichte")] public void F18() { Assert.Equal(0.01, ScanPlus.Density(Grid(8, 0.01)), 9); }
    [Fact(DisplayName = "SPEC-09 F19: Drehen")] public void F19() { var pc = new PointCloud(); pc.Points.Add(new(1, 0, 3)); var r = ScanPlus.RotateZ(pc, 90).Points[0]; Assert.Equal((0.0, 1.0, 3.0), (r.X, r.Y, r.Z)); }
    [Fact(DisplayName = "SPEC-09 F20: Zusammenführen")] public void F20() { var m = ScanPlus.Merge(Grid(2, 1), Grid(3, 1)); Assert.Equal(13, m.Points.Count); Assert.Null(m.Colors); }
}

public class SPEC10_Plus
{
    static readonly DateTime T = new(2026, 10, 21, 10, 0, 0);
    static TwinHub Hub(Fixture f, params double[] v) { var h = new TwinHub(f.Root); h.Define("Heizung", "Vorlauf", "°C", 70, 80); for (var i = 0; i < v.Length; i++) h.Record(T.AddHours(i), "Heizung", "Vorlauf", v[i], false); h.Save(); return h; }
    [Fact(DisplayName = "SPEC-10 F09: Speichergrenze")] public void F09() { using var f = new Fixture(); var h = Hub(f, 1, 2, 3, 4, 5); Assert.Equal(3, TwinPlus.Trim(h.Model, 2)); Assert.Equal([4.0, 5.0], h.Model.Readings.Select(r => r.Value)); Assert.Equal(0, TwinPlus.Trim(h.Model, 9)); }
    [Fact(DisplayName = "SPEC-10 F10: Ereignisprotokoll")] public void F10() { using var f = new Fixture(); var e = TwinPlus.Events(Hub(f, 60, 72, 85, 65).Model, "Heizung", "Vorlauf"); Assert.Equal(3, e.Count); Assert.Equal((TwinLevel.Ok, TwinLevel.Warnung), (e[0].From, e[0].To)); Assert.Equal(T.AddHours(2), e[1].Time); Assert.Equal(TwinLevel.Ok, e[2].To); }
    [Fact(DisplayName = "SPEC-10 F11: Hysterese")] public void F11() { using var f = new Fixture(); var m = Hub(f, 60, 72, 68, 64).Model; Assert.Equal(T.AddHours(2), TwinPlus.Events(m, "Heizung", "Vorlauf")[1].Time); var e = TwinPlus.Events(m, "Heizung", "Vorlauf", 5); Assert.Equal(2, e.Count); Assert.Equal(T.AddHours(3), e[1].Time); }
    [Fact(DisplayName = "SPEC-10 F12: Zeitraum-Kennzahlen")] public void F12() { using var f = new Fixture(); var s = TwinPlus.Span(Hub(f, 10, 20, 30, 40).Model.Readings, T.AddHours(1), T.AddHours(3)); Assert.Equal((3, 20.0, 40.0, 30.0), (s.Count, s.Min, s.Max, s.Mean)); }
    [Fact(DisplayName = "SPEC-10 F13: Betriebsstunden")] public void F13() { using var f = new Fixture(); var h = Hub(f, 0, 5, 5, 0, 5); Assert.Equal(2, TwinPlus.HoursAbove(TwinPlus.Series(h.Model, "Heizung", "Vorlauf"), 1)); }
    [Fact(DisplayName = "SPEC-10 F14: Veraltete Sensoren")] public void F14() { using var f = new Fixture(); var h = Hub(f, 1, 2); h.Define("Pumpe", "Druck", "bar"); var s = TwinPlus.Stale(h.Model, T.AddHours(1.5), 60); Assert.Single(s); Assert.Equal("Pumpe", s[0].Asset); Assert.Equal(2, TwinPlus.Stale(h.Model, T.AddHours(3), 60).Count); }
    [Fact(DisplayName = "SPEC-10 F15: Abgeleiteter Sensor")] public void F15() { using var f = new Fixture(); var h = Hub(f, 0, 100); Assert.Equal(2, TwinPlus.Derive(h, "Heizung", "Vorlauf", "Vorlauf °F", "°F", 1.8, 32)); Assert.Equal([32.0, 212.0], TwinPlus.Series(new TwinHub(f.Root).Model, "Heizung", "Vorlauf °F").Select(r => r.Value)); }
    [Fact(DisplayName = "SPEC-10 F16: Export")] public void F16() { using var f = new Fixture(); var c = TwinPlus.ExportCsv(Hub(f, 1.5).Model, "Heizung", "Vorlauf"); Assert.Equal("Zeit;Anlage;Sensor;Wert\n2026-10-21T10:00:00;Heizung;Vorlauf;1,5\n", c); var h2 = new TwinHub(new Fixture().Root); Assert.Equal((1, 0), h2.Import(c)); }
    [Fact(DisplayName = "SPEC-10 F17: Sensor löschen/umbenennen")] public void F17() { using var f = new Fixture(); var h = Hub(f, 1, 2); TwinPlus.Rename(h, "Heizung", "Vorlauf", "VL"); Assert.Equal(2, TwinPlus.Series(h.Model, "Heizung", "VL").Count); Assert.Equal(2, TwinPlus.Delete(h, "Heizung", "VL")); Assert.Empty(new TwinHub(f.Root).Model.Assets); Assert.Throws<ArgumentException>(() => TwinPlus.Delete(h, "Heizung", "VL")); }
    [Fact(DisplayName = "SPEC-10 F18: Wartungsplan")] public void F18() { Assert.Equal((1500.0, 300.0, false), TwinPlus.Maintenance(1200, 500, 1000)); Assert.True(TwinPlus.Maintenance(600, 500).Due); }
    [Fact(DisplayName = "SPEC-10 F19: Was-wäre-wenn")] public void F19() { using var f = new Fixture(); var h = Hub(f, 60, 62, 64); Assert.Equal(112, TwinPlus.Forecast(TwinPlus.Series(h.Model, "Heizung", "Vorlauf"), 24), 6); }
    [Fact(DisplayName = "SPEC-10 F20: Energie")] public void F20() { using var f = new Fixture(); var h = Hub(f, 2, 4, 4); Assert.Equal(7, TwinPlus.EnergyKwh(TwinPlus.Series(h.Model, "Heizung", "Vorlauf")), 9); }
}

public class SPEC13_Plus
{
    [Fact(DisplayName = "SPEC-13 F02: GPX-Routen")] public void F02() { using var f = new Fixture(); Directory.CreateDirectory(f.Root); var p = Path.Combine(f.Root, "r.gpx"); File.WriteAllText(p, "<gpx xmlns=\"http://www.topografix.com/GPX/1/1\"><rte><rtept lat=\"48\" lon=\"11\"/><rtept lat=\"48.01\" lon=\"11\"/></rte></gpx>"); var (t, w) = Geo.ReadGpx(p); Assert.Equal(2, t.Count); Assert.Empty(w); Assert.Equal(1.11, Geo.Analyse(t).DistanceKm, 2); }
    [Fact(DisplayName = "SPEC-13 F13: UTM → Grad")] public void F13() { var u = Geo.Utm(48.1372, 11.5756); var (la, lo) = GeoPlus.UtmToLatLon(u.Zone, u.Easting, u.Northing); Assert.Equal(48.1372, la, 5); Assert.Equal(11.5756, lo, 5); var s = Geo.Utm(-33.8688, 151.2093); var b = GeoPlus.UtmToLatLon(s.Zone, s.Easting, s.Northing, false); Assert.Equal(-33.8688, b.Lat, 5); }
    [Fact(DisplayName = "SPEC-13 F14: Richtung")] public void F14() { Assert.Equal(90, GeoPlus.Bearing(new(0, 0), new(0, 1)), 6); Assert.Equal(0, GeoPlus.Bearing(new(48, 11), new(49, 11)), 6); Assert.Equal("SO", GeoPlus.Compass(135)); Assert.Equal("N", GeoPlus.Compass(355)); }
    [Fact(DisplayName = "SPEC-13 F15: Zielpunkt")] public void F15() { var a = new GeoPoint(48, 11); var b = GeoPlus.Destination(a, 45, 10); Assert.Equal(10, Geo.Haversine(a, b), 6); Assert.Equal(45, GeoPlus.Bearing(a, b), 1); }
    [Fact(DisplayName = "SPEC-13 F16: Fläche")] public void F16() { var d = 1 / (Geo.R * 1000 * Math.PI / 180) * 100; var sq = new List<GeoPoint> { new(0, 0), new(0, d), new(d, d), new(d, 0) }; Assert.Equal(10000, GeoPlus.AreaM2(sq), 0); Assert.Throws<ArgumentException>(() => GeoPlus.AreaM2(sq.Take(2).ToList())); }
    [Fact(DisplayName = "SPEC-13 F17: Höhenprofil")] public void F17() { var s = GeoPlus.ProfileSvg([new(48, 11, 500), new(48.01, 11, 700)]); Assert.Contains("<polyline", s); Assert.Contains("700 m", s); Assert.Throws<InvalidOperationException>(() => GeoPlus.ProfileSvg([new(48, 11), new(48.1, 11)])); }
    [Fact(DisplayName = "SPEC-13 F18: Wanderzeit")] public void F18() { Assert.Equal(TimeSpan.FromHours(4), GeoPlus.HikingTime(8, 600, 500)); Assert.Equal(TimeSpan.FromHours(2.5), GeoPlus.HikingTime(8, 300, 0)); }
    [Fact(DisplayName = "SPEC-13 F19: Vereinfachen")] public void F19() { var t = Enumerable.Range(0, 11).Select(i => new GeoPoint(48 + (i % 2) * 0.00001, 11 + i * 0.001)).ToList(); Assert.Equal(2, GeoPlus.Simplify(t, 5).Count); Assert.Equal(11, GeoPlus.Simplify(t, 0.1).Count); t.Add(new GeoPoint(48.01, 11.01)); Assert.Equal(3, GeoPlus.Simplify(t, 5).Count); }
    [Fact(DisplayName = "SPEC-13 F20: GPX schreiben")] public void F20() { using var f = new Fixture(); Directory.CreateDirectory(f.Root); var p = Path.Combine(f.Root, "o.gpx"); File.WriteAllText(p, GeoPlus.WriteGpx([new(48, 11, 500, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)), new(48.01, 11, 510)], [new(48, 11, Name: "A & B")], "Tour")); var (t, w) = Geo.ReadGpx(p); Assert.Equal(2, t.Count); Assert.Equal(510, t[1].Ele); Assert.Equal("A & B", w[0].Name); }
}
