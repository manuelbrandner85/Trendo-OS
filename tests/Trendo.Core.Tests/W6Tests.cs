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
