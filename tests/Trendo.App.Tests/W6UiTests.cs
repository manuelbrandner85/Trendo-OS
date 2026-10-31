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
        foreach (var (k, v) in vals)
        {
            // T-0207: Felder mit fester Wertemenge sind Auswahllisten
            if (w.TryFind<ComboBox>($"{id}-{k}") is { } cb) { Assert.Contains(v, cb.ItemsSource!.Cast<object>().Select(o => o.ToString())); cb.SelectedItem = v; }
            else w.Find<TextBox>($"{id}-{k}").Text = v;
        }
        w.Find<Button>($"{id}-run").Press(); W2Ui.Settle();
        return w.Find<SelectableTextBlock>($"{id}-out").Text ?? "";
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
        Assert.Contains("Laut deiner Messung zu langsam", W6Ui.Run(w, "xr-frame", ("ms", "20"))); // T-0244: Trendo misst nicht selbst
        Assert.Contains("auf dem Gerät nicht geprüft", o);
        Assert.DoesNotContain("läuft flüssig", o);
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
        // T-0251: Bild im Seitenverhältnis des Tischs (400 × 300 mm → 4:3); ein quadratisches Bild wird als falsche Kalibrierung abgelehnt
        Assert.Contains("Kalibrierung passt nicht", W6Ui.Run(w, "rob-see", ("file", Img.Png(root, 100, 100, (60, 60, 79, 79)))));
        var o = W6Ui.Run(w, "rob-see", ("file", Img.Png(root, 100, 75, (60, 50, 79, 69))));
        Assert.Contains("x = 278,0 mm", o);
        Assert.Contains("Kein", W6Ui.Run(w, "rob-see", ("color", "grün")));
        Assert.Contains("Kontrolle: Greifer landet bei 150,0 / 50,0 / 40,0", W6Ui.Run(w, "rob-ik"));
        Assert.Contains("⚠", W6Ui.Run(w, "rob-ik", ("x", "900")));
        Assert.Contains("Gespeichert", W6Ui.Run(w, "rob-prog"));
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
