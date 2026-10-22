using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Trendo.App;
using Trendo.Core;
using Xunit;

namespace Trendo.App.Tests;

public static class W6Ui
{
    public static string Run(MainWindow w, string id, params (string Key, string Val)[] vals)
    {
        foreach (var (k, v) in vals) w.Find<TextBox>($"{id}-{k}").Text = v;
        w.Find<Button>($"{id}-run").Press(); W2Ui.Settle();
        return w.Find<SelectableTextBlock>($"{id}-out").Text ?? "";
    }
}

public class SPEC01_Ui
{
    [AvaloniaFact(DisplayName = "SPEC-01 UI: Studio-Kachel öffnet Räume, Raum anlegen, Bedarf, Grundriss")]
    public void Rooms()
    {
        var (w, l, _) = Ui.Open();
        w.Show(MainWindow.StudioPages["SPEC-01"]);
        W6Ui.Run(w, "arch-add", ("name", "Küche"), ("l", "3,5"), ("w", "3"));
        Assert.Single(w.Rooms.Project.Rooms);
        Assert.Contains("Pakete", W6Ui.Run(w, "arch-need"));
        Assert.Contains("⚠", W6Ui.Run(w, "arch-add", ("l", "abc")));
        w.Find<Button>("arch-svg").Press();
        Assert.Contains(l.Opened, x => x.EndsWith("grundriss.svg"));
    }
}

public class SPEC02_Ui
{
    [AvaloniaFact(DisplayName = "SPEC-02 UI: Elektronik-Rechner und Stückliste")]
    public void Electronics()
    {
        var (w, _, _) = Ui.Open();
        w.Show(MainWindow.StudioPages["SPEC-02"]);
        Assert.Contains("4,70 kΩ", W6Ui.Run(w, "el-code"));
        Assert.Contains("150", W6Ui.Run(w, "el-led"));
        Assert.Contains("⚠", W6Ui.Run(w, "el-ohm", ("i", "1")));
        Assert.Contains("Gespeichert", W6Ui.Run(w, "el-bom"));
        w.Find<Button>("fach-tab-architektur").Press();
        Assert.Equal("architektur", w.CurrentView);
    }
}

public class SPEC03_Ui
{
    [AvaloniaFact(DisplayName = "SPEC-03 UI: Balken prüfen, Wärmedehnung, Einheiten")]
    public void Engineering()
    {
        var (w, _, _) = Ui.Open();
        w.Show(MainWindow.StudioPages["SPEC-03"]);
        Assert.Contains("Sicherheit", W6Ui.Run(w, "eng-beam"));
        Assert.Contains("⚠", W6Ui.Run(w, "eng-beam", ("case", "9")));
        Assert.Contains("2,76 mm", W6Ui.Run(w, "eng-heat"));
        Assert.Contains("6,8948 bar", W6Ui.Run(w, "eng-unit"));
    }
}

public class SPEC04_Ui
{
    [AvaloniaFact(DisplayName = "SPEC-04 UI: GLB-Modell prüfen und Bildzeit")]
    public void Xr()
    {
        var (w, _, root) = Ui.Open(); Directory.CreateDirectory(root);
        w.Show(MainWindow.StudioPages["SPEC-04"]);
        var o = W6Ui.Run(w, "xr-check", ("file", Glb.Write(root, 500_000)));
        Assert.Contains("500.000 Dreiecke", o); Assert.Contains("⚠ Meta Quest 2", o); Assert.Contains("✓ PC-VR", o);
        Assert.Contains("⚠", W6Ui.Run(w, "xr-check", ("file", "/gibtsnicht.glb")));
        Assert.Contains("Ruckelt", W6Ui.Run(w, "xr-frame", ("ms", "20")));
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


public class SPEC05_Ui
{
    [AvaloniaFact(DisplayName = "SPEC-05 UI: CSV auswerten, Regression, Gruppenvergleich")]
    public void Science()
    {
        var (w, _, root) = Ui.Open(); Directory.CreateDirectory(root);
        var f = Path.Combine(root, "mess.csv"); File.WriteAllText(f, "x;y\n1;2,1\n2;3,9\n3;6,2\n4;7,8\n5;10,1\n");
        w.Show(MainWindow.StudioPages["SPEC-05"]);
        Assert.Contains("n = 5", W6Ui.Run(w, "sci-desc", ("file", f)));
        Assert.True(File.Exists(Path.Combine(root, "mess.auswertung.md")));
        Assert.Contains("starker", W6Ui.Run(w, "sci-fit", ("file", f)));
        Assert.Contains("signifikant", W6Ui.Run(w, "sci-t", ("file", f)));
        Assert.Contains("⚠", W6Ui.Run(w, "sci-fit", ("x", "zz")));
    }
}

public class SPEC06_Ui
{
    [AvaloniaFact(DisplayName = "SPEC-06 UI: Karten anlegen, lernen, bewerten")]
    public void Learn()
    {
        var (w, _, _) = Ui.Open();
        w.Show(MainWindow.StudioPages["SPEC-06"]);
        W6Ui.Run(w, "learn-import", ("text", "Hund;dog|Katze;cat"));
        Assert.Contains("2 Karten", w.Find<TextBlock>("learn-progress").Text);
        Assert.Equal("Hund", w.Find<TextBlock>("learn-front").Text);
        w.Find<Button>("learn-show").Press();
        Assert.Equal("dog", w.Find<TextBlock>("learn-back").Text);
        w.Find<Button>("learn-gut").Press();
        Assert.Equal("Katze", w.Find<TextBlock>("learn-front").Text);
        Assert.Contains("1 heute fällig", w.Find<TextBlock>("learn-progress").Text);
    }
}

public class SPEC08_Ui
{
    [AvaloniaFact(DisplayName = "SPEC-08 UI: Größe, Schuh, Stoff, Varianten-CSV, Preis")]
    public void Fashion()
    {
        var (w, _, root) = Ui.Open();
        w.Show(MainWindow.StudioPages["SPEC-08"]);
        Assert.Contains("EU 40", W6Ui.Run(w, "fa-body"));
        Assert.Contains("UK 8", W6Ui.Run(w, "fa-shoe"));
        Assert.Contains("EU 42", W6Ui.Run(w, "fa-shoe", ("eu", "26,5 cm")));
        Assert.Contains("m Stoff", W6Ui.Run(w, "fa-fabric"));
        Assert.Contains("8 Varianten", W6Ui.Run(w, "fa-var"));
        Assert.True(File.Exists(Path.Combine(root, "fach", "mode", "varianten.csv")));
        Assert.Contains("39,90 €", W6Ui.Run(w, "fa-price"));
    }
}

public class SPEC09_Ui
{
    [AvaloniaFact(DisplayName = "SPEC-09 UI: Scan lesen und bereinigt speichern")]
    public void Scan()
    {
        var (w, _, root) = Ui.Open(); Directory.CreateDirectory(root);
        var f = Path.Combine(root, "s.xyz");
        File.WriteAllLines(f, Enumerable.Range(0, 400).Select(i => FormattableString.Invariant($"{i % 20 * 0.002} {i / 20 * 0.002} 0")).Append("3 3 3"));
        w.Show(MainWindow.StudioPages["SPEC-09"]);
        Assert.Contains("401 Punkte", W6Ui.Run(w, "scan-info", ("file", f)));
        var o = W6Ui.Run(w, "scan-clean", ("file", f), ("voxel", "0,004"), ("radius", "0,01"));
        Assert.Contains("1 Störpunkte", o); Assert.Contains("100 Punkte", o);
        Assert.True(File.Exists(Path.Combine(root, "s.bereinigt.ply")));
    }
}

public class SPEC10_Ui
{
    static void Messen(MainWindow w, double platte, double ram)
    {
        w.PcSampleSource = () => new MainWindow.PcSample(platte, ram);
        w.Find<Button>("twin-pc").Press();
    }

    [AvaloniaFact(DisplayName = "SPEC-10 UI: diesen PC live messen, Sensor anlegen, CSV einlesen")]
    public void Twin_schwellen()
    {
        var (w, _, root) = Ui.Open(); Directory.CreateDirectory(root);
        w.Show(MainWindow.StudioPages["SPEC-10"]);
        // Grenzen im Produkt: Platte Warnung ab 85 / Alarm ab 95, Arbeitsspeicher 1500 / 3000 MB.
        Messen(w, 10, 100);
        Assert.Contains("Ok", w.Find<TextBlock>("twin-Dieser_PC-Festplatte_belegt").Text);
        Assert.Contains("Ok", w.Find<TextBlock>("twin-Dieser_PC-Trendo-Arbeitsspeicher").Text);
        Messen(w, 85, 1500);
        Assert.Contains("Warnung", w.Find<TextBlock>("twin-Dieser_PC-Festplatte_belegt").Text);
        Assert.Contains("Warnung", w.Find<TextBlock>("twin-Dieser_PC-Trendo-Arbeitsspeicher").Text);
        Messen(w, 95, 3000);
        Assert.Contains("Alarm", w.Find<TextBlock>("twin-Dieser_PC-Festplatte_belegt").Text);
        Assert.Contains("Alarm", w.Find<TextBlock>("twin-Dieser_PC-Trendo-Arbeitsspeicher").Text);
        W6Ui.Run(w, "twin-def");
        var f = Path.Combine(root, "m.csv"); File.WriteAllText(f, "Zeit;Anlage;Sensor;Wert\n21.10.2026 10:00;Heizung;Vorlauf;85\n");
        Assert.Contains("1 Messwerte", W6Ui.Run(w, "twin-import", ("file", f)));
        Assert.Contains("Alarm", w.Find<TextBlock>("twin-Heizung-Vorlauf").Text);
        w.Close();
    }

    [AvaloniaFact(DisplayName = "SPEC-10 UI: Live-Messung liefert eine plausible Zahl und einen gültigen Status")]
    public void Twin_live()
    {
        var (w, _, root) = Ui.Open(); Directory.CreateDirectory(root);
        w.Show(MainWindow.StudioPages["SPEC-10"]);
        Assert.Null(w.PcSampleSource);
        w.Find<Button>("twin-pc").Press();
        var ram = w.Twin.States().Single(s => s.Sensor == "Trendo-Arbeitsspeicher");
        var platte = w.Twin.States().Single(s => s.Sensor == "Festplatte belegt");
        Assert.Contains(ram.Level, new[] { TwinLevel.Ok, TwinLevel.Warnung, TwinLevel.Alarm });
        Assert.Contains(platte.Level, new[] { TwinLevel.Ok, TwinLevel.Warnung, TwinLevel.Alarm });
        Assert.Contains(ram.Level.ToString(), w.Find<TextBlock>("twin-Dieser_PC-Trendo-Arbeitsspeicher").Text);
        Assert.Contains(platte.Level.ToString(), w.Find<TextBlock>("twin-Dieser_PC-Festplatte_belegt").Text);
        Assert.InRange(ram.Last!.Value, 1, 500_000);
        Assert.InRange(platte.Last!.Value, 0, 100);
        Assert.True(w.Twin.Model.Readings.Count >= 2);
        w.Close();
    }
}

public class SPEC13_Ui
{
    [AvaloniaFact(DisplayName = "SPEC-13 UI: GPX auswerten, Koordinaten, Luftlinie")]
    public void Geo()
    {
        var (w, _, root) = Ui.Open(); Directory.CreateDirectory(root);
        var f = Path.Combine(root, "tour.gpx"); File.WriteAllText(f, "<gpx xmlns=\"http://www.topografix.com/GPX/1/1\"><trk><trkseg><trkpt lat=\"47.40\" lon=\"10.98\"><ele>1000</ele></trkpt><trkpt lat=\"47.41\" lon=\"10.98\"><ele>1100</ele></trkpt></trkseg></trk></gpx>");
        w.Show(MainWindow.StudioPages["SPEC-13"]);
        Assert.Contains("↑ 100 m", W6Ui.Run(w, "geo-gpx", ("file", f)));
        Assert.True(File.Exists(Path.Combine(root, "tour.geojson"))); Assert.True(File.Exists(Path.Combine(root, "tour.svg")));
        Assert.Contains("UTM: 32U", W6Ui.Run(w, "geo-coord"));
        Assert.Contains("504", W6Ui.Run(w, "geo-dist"));
    }
}

public class SPEC14_Ui
{
    [AvaloniaFact(DisplayName = "SPEC-14 UI: G-Code prüfen mit Vorschau, Schnittwerte")]
    public void Cnc()
    {
        var (w, _, root) = Ui.Open(); Directory.CreateDirectory(root);
        var f = Path.Combine(root, "teil.nc"); File.WriteAllText(f, "G21 G90\nT2 M6\nG0 X0 Y0 Z5\nG1 Z-1 F200\nG1 X50 Y0 F1200\nX50 Y50\nG0 Z5\n");
        w.Show(MainWindow.StudioPages["SPEC-14"]);
        var o = W6Ui.Run(w, "cnc-check", ("file", f));
        Assert.Contains("✓ Keine Auffälligkeiten", o); Assert.Contains("T2", o); Assert.Contains("50,0 × 50,0 mm", o);
        Assert.True(File.Exists(Path.Combine(root, "teil.vorschau.svg")));
        Assert.Contains("⚠", W6Ui.Run(w, "cnc-check", ("x", "10")));
        Assert.Contains("13.263 U/min", W6Ui.Run(w, "cnc-feeds"));
    }
}

public class SPEC15_Ui
{
    [AvaloniaFact(DisplayName = "SPEC-15 UI: Ablauf bauen, speichern, Leitung prüfen")]
    public void Broadcast()
    {
        var (w, l, root) = Ui.Open();
        w.Show(MainWindow.StudioPages["SPEC-15"]);
        W6Ui.Run(w, "bc-setup", ("start", "21.10.2026 20:00"), ("slot", "5"));
        W6Ui.Run(w, "bc-add", ("title", "Intro"), ("dur", "1:00"));
        W6Ui.Run(w, "bc-add", ("title", "Thema"), ("dur", "3:00"));
        W6Ui.Run(w, "bc-add", ("title", "Ende"), ("dur", "2:00"));
        Assert.Equal("20:04:00  Ende", w.Find<TextBlock>("bc-row-2").Text);
        Assert.Contains("zu lang", w.Find<TextBlock>("bc-total").Text);
        w.Find<Button>("bc-export").Press(); W2Ui.Settle();
        Assert.True(File.Exists(Path.Combine(root, "fach", "sendung", "kapitel.txt")));
        Assert.Contains("⚠", W6Ui.Run(w, "bc-rate"));
        Assert.Contains("✓", W6Ui.Run(w, "bc-rate", ("q", "720p30")));
    }
}

public class SPEC16_Ui
{
    [AvaloniaFact(DisplayName = "SPEC-16 UI: Fahrzeug, Tanken, Fahrt, Fahrtenbuch")]
    public void Vehicle()
    {
        var (w, l, _) = Ui.Open();
        w.Show(MainWindow.StudioPages["SPEC-16"]);
        W6Ui.Run(w, "car-add", ("name", "Golf"));
        W6Ui.Run(w, "car-fuel", ("odo", "1000"), ("amt", "40"));
        W6Ui.Run(w, "car-fuel", ("odo", "1600"), ("amt", "36"));
        Assert.Contains("6,0 l/100 km", w.Find<TextBlock>("car-stats-Golf").Text);
        Assert.Contains("⚠", W6Ui.Run(w, "car-trip", ("s", "1600"), ("e", "1650"), ("why", "")));
        W6Ui.Run(w, "car-trip", ("s", "1600"), ("e", "1650"), ("why", "Baumarkt"));
        Assert.Single(w.Cars.Get("Golf").Trips);
        Assert.Contains("Gespeichert", W6Ui.Run(w, "car-export"));
        Assert.Contains(l.Opened, x => x.EndsWith("fahrtenbuch-Golf.csv"));
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


public class CORE09_Ui
{
    [AvaloniaFact(DisplayName = "CORE-09 UI: Objekt im Bild finden, Arm rechnen, Programm speichern")]
    public void Robot()
    {
        var (w, _, root) = Ui.Open(); Directory.CreateDirectory(root);
        w.Show(MainWindow.StudioPages["CORE-09"]);
        var o = W6Ui.Run(w, "rob-see", ("file", Img.Png(root, 100, 100, (60, 60, 79, 79))));
        Assert.Contains("x = 278,0 mm", o);
        Assert.Contains("Kein", W6Ui.Run(w, "rob-see", ("color", "grün")));
        Assert.Contains("Kontrolle: Greifer landet bei 150,0 / 50,0 / 40,0", W6Ui.Run(w, "rob-ik"));
        Assert.Contains("⚠", W6Ui.Run(w, "rob-ik", ("x", "900")));
        Assert.Contains("Gespeichert", W6Ui.Run(w, "rob-prog"));
    }
}

public class SPEC01_UiPlus
{
    [AvaloniaFact(DisplayName = "SPEC-01 UI Ausbau: Gruppen Material & Planen")]
    public void Plus()
    {
        var (w, _, _) = Ui.Open();
        w.Show("architektur");
        Assert.Contains("⚠", W6Ui.Run(w, "arch-furn"));
        W6Ui.Run(w, "arch-add", ("name", "Wohnzimmer"), ("l", "5"), ("w", "4"));
        Assert.Contains("✓", W6Ui.Run(w, "arch-furn"));
        Assert.Contains("Fliesen", W6Ui.Run(w, "arch-tile"));
        Assert.Contains("Rollen", W6Ui.Run(w, "arch-wall"));
        Assert.Contains("gesamt", W6Ui.Run(w, "arch-cost"));
        Assert.Contains("Gespeichert", W6Ui.Run(w, "arch-csv"));
        Assert.Contains("W Heizleistung", W6Ui.Run(w, "arch-heat"));
        Assert.Contains("lm", W6Ui.Run(w, "arch-light"));
        Assert.Contains("Steigungen", W6Ui.Run(w, "arch-stair"));
        Assert.Contains("67,00 m²", W6Ui.Run(w, "arch-wofl"));
    }
}

public class SPEC02_UiPlus
{
    [AvaloniaFact(DisplayName = "SPEC-02 UI Ausbau: Bauteile, Platine, Versorgung")]
    public void Plus()
    {
        var (w, _, _) = Ui.Open(); w.Show("elektronik");
        Assert.Contains("400 Ω", W6Ui.Run(w, "el-comb"));
        Assert.Contains("100 nF", W6Ui.Run(w, "el-cap"));
        Assert.Contains("4,70 kΩ", W6Ui.Run(w, "el-smd"));
        Assert.Contains("Grenzfrequenz", W6Ui.Run(w, "el-rc"));
        Assert.Contains("Hz", W6Ui.Run(w, "el-555"));
        Assert.Contains("mm breit", W6Ui.Run(w, "el-trace"));
        Assert.Contains("34", W6Ui.Run(w, "el-bat"));
        Assert.Contains("⚠", W6Ui.Run(w, "el-cable"));
        Assert.Contains("Kühlkörper", W6Ui.Run(w, "el-reg"));
        Assert.Contains("4.700", W6Ui.Run(w, "el-si"));
    }
}

public class SPEC03_UiPlus
{
    [AvaloniaFact(DisplayName = "SPEC-03 UI Ausbau: Profile, Maschinenelemente, Wärme")]
    public void Plus()
    {
        var (w, _, _) = Ui.Open(); w.Show("technik");
        Assert.Contains("Durchbiegung", W6Ui.Run(w, "eng-hollow"));
        Assert.Contains("Knicklast", W6Ui.Run(w, "eng-buckle"));
        Assert.Contains("N/mm²", W6Ui.Run(w, "eng-axial"));
        Assert.Contains("Verdrehung", W6Ui.Run(w, "eng-torsion"));
        Assert.Contains("25,0 Nm", W6Ui.Run(w, "eng-bolt"));
        Assert.Contains("Federrate", W6Ui.Run(w, "eng-spring"));
        Assert.Contains("500 U/min", W6Ui.Run(w, "eng-gear"));
        Assert.Contains("U-Wert", W6Ui.Run(w, "eng-u"));
        Assert.Contains("2,70 kg", W6Ui.Run(w, "eng-mass"));
    }
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


public class SPEC04_UiPlus
{
    [AvaloniaFact(DisplayName = "SPEC-04 UI Ausbau: Details, LOD, Texel, Komfort")]
    public void Plus()
    {
        var (w, _, root) = Ui.Open(); Directory.CreateDirectory(root); w.Show("xr");
        var o = W6Ui.Run(w, "xr-detail", ("file", Gltf.Write(root)));
        Assert.Contains("Maßstab plausibel", o); Assert.Contains("Stuhl", o); Assert.Contains("1024 × 512", o); Assert.Contains("Draco", o);
        Assert.True(File.Exists(Path.Combine(root, "szene.pruefbericht.md")));
        Assert.Contains("Stufe 3", W6Ui.Run(w, "xr-lod"));
        Assert.Contains("1.024 Pixel", W6Ui.Run(w, "xr-texel"));
        Assert.Contains("Komfort", W6Ui.Run(w, "xr-move"));
    }
}

public class SPEC05_UiPlus
{
    [AvaloniaFact(DisplayName = "SPEC-05 UI Ausbau: Tests, Darstellung, Aufbereitung")]
    public void Plus()
    {
        var (w, l, root) = Ui.Open(); Directory.CreateDirectory(root);
        var f = Path.Combine(root, "m.csv"); File.WriteAllText(f, "vor;nach\n10;12\n12;14\n9;10\n11;13\n13;16\n");
        w.Show("wissenschaft");
        Assert.Contains("sehr wahrscheinlich echt", W6Ui.Run(w, "sci-paired", ("file", f)));
        Assert.Contains("hängen zusammen", W6Ui.Run(w, "sci-chi"));
        Assert.Contains("95 % Sicherheit", W6Ui.Run(w, "sci-ci", ("file", f)));
        Assert.Contains("97 Messungen", W6Ui.Run(w, "sci-n"));
        Assert.Contains("Ergebnis", W6Ui.Run(w, "sci-err"));
        Assert.Contains("Median", W6Ui.Run(w, "sci-quart", ("file", f)));
        Assert.True(File.Exists(Path.Combine(root, "m.histogramm.svg")));
        W6Ui.Run(w, "sci-scatter", ("file", f)); Assert.Contains(l.Opened, x => x.EndsWith("m.streudiagramm.svg"));
        W6Ui.Run(w, "sci-smooth", ("file", f));
        Assert.Contains("nach_geglaettet;nach_z", File.ReadAllText(Path.Combine(root, "m.aufbereitet.csv")));
    }
}

public class SPEC06_UiPlus
{
    [AvaloniaFact(DisplayName = "SPEC-06 UI Ausbau: Eintippen, Quiz, Fortschritt, Verwalten")]
    public void Plus()
    {
        var (w, _, _) = Ui.Open(); w.Show("lernen");
        W6Ui.Run(w, "learn-import", ("text", "Hund;dog|Katze;cat|Haus;house"));
        Assert.Contains("✓ Richtig", W6Ui.Run(w, "learn-type", ("ans", "dog")));
        var q = W6Ui.Run(w, "learn-quiz"); Assert.Contains("1)", q);
        Assert.Contains("Richtig", W6Ui.Run(w, "learn-quiz", ("pick", "1")));
        Assert.Contains("Nächste 7 Tage", w.Find<TextBlock>("learn-forecast").Text);
        Assert.Contains("pro Tag", W6Ui.Run(w, "learn-exam"));
        Assert.Contains("Geändert", W6Ui.Run(w, "learn-edit", ("front", "Haus"), ("back", "home")));
        Assert.Contains("3 umgekehrte", W6Ui.Run(w, "learn-rev", ("deck", "Englisch")));
        Assert.Contains("3 Karte(n) verschoben", W6Ui.Run(w, "learn-rename", ("from", "Englisch"), ("to", "EN")));
        Assert.Contains("Gespeichert", W6Ui.Run(w, "learn-export"));
    }
}

public class SPEC08_UiPlus
{
    [AvaloniaFact(DisplayName = "SPEC-08 UI Ausbau: Größen, Herstellen, Verkaufen")]
    public void Plus()
    {
        var (w, l, _) = Ui.Open(); w.Show("mode");
        Assert.Contains("Größe 98", W6Ui.Run(w, "fa-kid"));
        Assert.Contains("75C", W6Ui.Run(w, "fa-bra"));
        Assert.Contains("US 7", W6Ui.Run(w, "fa-ring"));
        Assert.Contains("XL: 108,0 cm", W6Ui.Run(w, "fa-grade"));
        Assert.Contains("Knäuel", W6Ui.Run(w, "fa-yarn"));
        Assert.Contains("✓", W6Ui.Run(w, "fa-label"));
        Assert.Contains("4006381333931", W6Ui.Run(w, "fa-ean"));
        Assert.Contains("10 % Rabatt", W6Ui.Run(w, "fa-tier"));
        W6Ui.Run(w, "fa-sheet"); Assert.Contains(l.Opened, x => x.EndsWith("datenblatt.md"));
    }
}

public class SPEC09_UiPlus
{
    [AvaloniaFact(DisplayName = "SPEC-09 UI Ausbau: Umformen, Boden, Prüfen")]
    public void Plus()
    {
        var (w, _, root) = Ui.Open(); Directory.CreateDirectory(root);
        var f = Path.Combine(root, "b.xyz");
        File.WriteAllLines(f, Enumerable.Range(0, 100).Select(i => FormattableString.Invariant($"{i % 10 * 10} {i / 10 * 10} 0")).Append("50 50 500"));
        w.Show(MainWindow.StudioPages["SPEC-09"]);
        Assert.Contains("0,090 × 0,090 × 0,500", W6Ui.Run(w, "scan-scale", ("file", f)));
        Assert.True(File.Exists(Path.Combine(root, "b.skaliert.ply")));
        Assert.Contains("Gespeichert", W6Ui.Run(w, "scan-center", ("file", f)));
        Assert.Contains("90", W6Ui.Run(w, "scan-rot", ("file", f)));
        Assert.Contains("4 Punkte", W6Ui.Run(w, "scan-crop", ("file", f), ("box", "0 0 -1 10 10 1")));
        Assert.Contains("202 Punkte", W6Ui.Run(w, "scan-merge", ("file", f), ("file2", f)));
        Assert.Contains("100 von 101", W6Ui.Run(w, "scan-plane", ("file", f), ("tol", "1")));
        Assert.Contains("100 Bodenpunkte", W6Ui.Run(w, "scan-nofloor", ("file", f), ("tol", "1")));
        Assert.Contains("Volumen", W6Ui.Run(w, "scan-vol", ("file", f), ("cell", "10")));
        Assert.Contains("größte 0,0000", W6Ui.Run(w, "scan-cmp", ("file", f), ("file2", f)));
        Assert.Contains("Punktabstand", W6Ui.Run(w, "scan-dens", ("file", f)));
        W6Ui.Run(w, "scan-xyz", ("file", f)); Assert.True(File.Exists(Path.Combine(root, "b.export.xyz")));
    }
}

public class SPEC10_UiPlus
{
    [AvaloniaFact(DisplayName = "SPEC-10 UI Ausbau: Auswerten und Verwalten")]
    public void Plus()
    {
        var (w, _, _) = Ui.Open(); var t = new DateTime(2026, 10, 21, 10, 0, 0);
        w.Twin.Define("Heizung", "Vorlauf", "°C", 70, 80);
        foreach (var (h, v) in new[] { (0, 60.0), (1, 72), (2, 69), (3, 60) }) w.Twin.Record(t.AddHours(h), "Heizung", "Vorlauf", v);
        w.Show(MainWindow.StudioPages["SPEC-10"]);
        Assert.Equal(2, W6Ui.Run(w, "twin-events").Split('\n').Length);
        Assert.Contains("max 72,00", W6Ui.Run(w, "twin-span", ("from", "21.10.2026"), ("to", "21.10.2026")));
        Assert.Contains("3,0 Stunden", W6Ui.Run(w, "twin-hours"));
        Assert.Contains("etwa", W6Ui.Run(w, "twin-fc"));
        Assert.Contains("⚠ Heizung", W6Ui.Run(w, "twin-stale"));
        Assert.Contains("4 Werte", W6Ui.Run(w, "twin-derive"));
        Assert.True(File.Exists(W6Ui.Run(w, "twin-export").Replace("Gespeichert: ", "")));
        Assert.Contains("Umbenannt", W6Ui.Run(w, "twin-rename", ("sensor", "Vorlauf °F"), ("new", "VL F")));
        Assert.Contains("Bestätigung", W6Ui.Run(w, "twin-del", ("sensor", "VL F")));
        Assert.Contains("4 Werte", W6Ui.Run(w, "twin-del", ("sensor", "VL F"), ("confirm", "vl f")));
        Assert.Contains("noch 300 h", W6Ui.Run(w, "twin-maint"));
        Assert.Contains("2 alte Werte", W6Ui.Run(w, "twin-trim", ("max", "2")));
        w.Twin.Define("Solar", "Leistung", "kW"); w.Twin.Record(t, "Solar", "Leistung", 2); w.Twin.Record(t.AddHours(2), "Solar", "Leistung", 4);
        Assert.Contains("6,00 kWh", W6Ui.Run(w, "twin-kwh"));
    }
}

public class SPEC13_UiPlus
{
    [AvaloniaFact(DisplayName = "SPEC-13 UI Ausbau: Navigieren und Tour planen")]
    public void Plus()
    {
        var (w, _, root) = Ui.Open(); Directory.CreateDirectory(root);
        var f = Path.Combine(root, "t.gpx");
        File.WriteAllText(f, "<gpx xmlns=\"http://www.topografix.com/GPX/1/1\"><trk><trkseg>" + string.Concat(Enumerable.Range(0, 21).Select(i => FormattableString.Invariant($"<trkpt lat=\"{48 + i * 0.001}\" lon=\"11\"><ele>{500 + i * 10}</ele></trkpt>"))) + "</trkseg></trk></gpx>");
        w.Show(MainWindow.StudioPages["SPEC-13"]);
        Assert.Contains("48.1", W6Ui.Run(w, "geo-utm"));
        Assert.Contains("(S", W6Ui.Run(w, "geo-bear"));
        Assert.Contains("Ziel: 48.13", W6Ui.Run(w, "geo-dest"));
        Assert.Contains("ha", W6Ui.Run(w, "geo-area"));
        Assert.Contains("5:46 h", W6Ui.Run(w, "geo-time"));
        Assert.Contains("Gehzeit", W6Ui.Run(w, "geo-prof", ("file", f))); Assert.True(File.Exists(Path.Combine(root, "t.profil.svg")));
        Assert.Contains("21 → 2 Punkte", W6Ui.Run(w, "geo-simp", ("file", f)));
    }
}

public class SPEC14_UiPlus
{
    [AvaloniaFact(DisplayName = "SPEC-14 UI Ausbau: Programme erzeugen und Werkstatt-Rechner")]
    public void Plus()
    {
        var (w, _, root) = Ui.Open(); Directory.CreateDirectory(root);
        var f = Path.Combine(root, "p.nc"); File.WriteAllText(f, "G90\nG0 X1 Y1\nG1 Z-1 F100\n");
        w.Show(MainWindow.StudioPages["SPEC-14"]);
        Assert.Contains("tasche.nc", W6Ui.Run(w, "cnc-pocket"));
        Assert.Contains("kontur.nc", W6Ui.Run(w, "cnc-outline"));
        Assert.Contains("(50,00 | 0,00)", W6Ui.Run(w, "cnc-bolt"));
        W6Ui.Run(w, "cnc-offset", ("file", f), ("dx", "10")); Assert.Contains("X11 Y1", File.ReadAllText(Path.Combine(root, "p.versetzt.nc")));
        Assert.Contains("0,30 kW", W6Ui.Run(w, "cnc-mrr"));
        Assert.Contains("17,50 € je Teil", W6Ui.Run(w, "cnc-cost"));
        Assert.Contains("Ø 6,75 mm", W6Ui.Run(w, "cnc-tap"));
    }
}

public class SPEC15_UiPlus
{
    [AvaloniaFact(DisplayName = "SPEC-15 UI Ausbau: Live, Bild & Ton, Planen")]
    public void Plus()
    {
        var (w, l, root) = Ui.Open(); Directory.CreateDirectory(root);
        w.Broadcast.Plan.Start = new DateTime(2026, 10, 21, 20, 0, 0); w.Broadcast.Add("Intro", 60); w.Broadcast.Add("Talk", 600);
        var srt = Path.Combine(root, "u.srt"); File.WriteAllText(srt, "1\n00:00:05,000 --> 00:00:06,000\nHi\n");
        w.Show(MainWindow.StudioPages["SPEC-15"]);
        Assert.Contains("▶ 2. Talk – noch 9:00", W6Ui.Run(w, "bc-now", ("at", "21.10.2026 20:02")));
        W6Ui.Run(w, "bc-cards"); Assert.Contains(l.Opened, x => x.EndsWith("moderationskarten.md"));
        Assert.Contains("3 Wörter", W6Ui.Run(w, "bc-speak", ("t", "eins zwei drei")));
        Assert.Contains("+4,0 dB", W6Ui.Run(w, "bc-loud"));
        Assert.Contains("16:9", W6Ui.Run(w, "bc-aspect"));
        Assert.Contains("crop=606:1080:657:0", W6Ui.Run(w, "bc-crop"));
        Assert.Contains("01:00:00:12", W6Ui.Run(w, "bc-tc"));
        Assert.Contains("4 Termine ohne", W6Ui.Run(w, "bc-week"));
        Assert.Contains("1 Untertitel", W6Ui.Run(w, "bc-srt", ("file", srt), ("s", "-2")));
        Assert.Contains("00:00:03,000", File.ReadAllText(Path.Combine(root, "u.versetzt.srt")));
    }
}

public class SPEC16_UiPlus
{
    [AvaloniaFact(DisplayName = "SPEC-16 UI Ausbau: Steuer & Erstattung, Rechner")]
    public void Plus()
    {
        var (w, _, _) = Ui.Open();
        w.Cars.Add("Zoe", "", true); w.Cars.Refuel("Zoe", new DateTime(2026, 1, 1), 1000, 40, 0.4); w.Cars.Refuel("Zoe", new DateTime(2026, 1, 9), 1200, 36, 0.4);
        w.Show(MainWindow.StudioPages["SPEC-16"]);
        Assert.Contains("2.508,00 €", W6Ui.Run(w, "car-commute", ("year", "2026")));
        Assert.Contains("günstiger: Fahrtenbuch", W6Ui.Run(w, "car-company"));
        Assert.Contains("37,50 €", W6Ui.Run(w, "car-mile"));
        Assert.Contains("18,0 kWh/100 km", W6Ui.Run(w, "car-ev", ("fp", "1,8")));
        Assert.Contains("46,7 kWh", W6Ui.Run(w, "car-charge"));
        Assert.Contains("Jahre alt", W6Ui.Run(w, "car-tire"));
        Assert.Contains("13,43 € pro Person", W6Ui.Run(w, "car-share"));
        Assert.Contains("im Monat", W6Ui.Run(w, "car-year"));
    }
}

public class CORE09_UiPlus
{
    [AvaloniaFact(DisplayName = "CORE-09 UI Ausbau: Bild prüfen und Arm planen")]
    public void Plus()
    {
        var (w, _, root) = Ui.Open(); Directory.CreateDirectory(root);
        var a = Img.Png(root, 80, 60, (10, 20, 29, 39));
        var b = Path.Combine(root, "leer.png"); var grey = new byte[80 * 60 * 3]; Array.Fill(grey, (byte)120); File.WriteAllBytes(b, VisionPlus.EncodePng(new RgbImage(80, 60, grey)));
        w.Show(MainWindow.StudioPages["CORE-09"]);
        Assert.Contains("1 Objekt(e)", W6Ui.Run(w, "rob-count", ("file", a)));
        Assert.Contains("Schärfe", W6Ui.Run(w, "rob-exp", ("file", a)));
        Assert.Contains("Bewegung: 8,3 %", W6Ui.Run(w, "rob-motion", ("file", a), ("file2", b)));
        W6Ui.Run(w, "rob-mask", ("file", a)); Assert.True(File.Exists(Path.Combine(root, "bild.maske-rot.png")));
        Assert.Contains("x = 0,0 mm, y = 300,0 mm", W6Ui.Run(w, "rob-cal", ("pt", "100 50")));
        var pick = W6Ui.Run(w, "rob-pick"); Assert.Contains("5. loslassen", pick); Assert.Contains("Aktion", File.ReadAllText(Path.Combine(MainWindowData(root), "greifbahn.csv")));
        Assert.Contains("% des Quadrats", W6Ui.Run(w, "rob-reach"));
        Assert.Contains("1500 µs", W6Ui.Run(w, "rob-servo"));
        Assert.Contains("2,50 s", W6Ui.Run(w, "rob-prof"));
    }
    static string MainWindowData(string root) => Directory.GetDirectories(root, "roboter", SearchOption.AllDirectories).First();
}
