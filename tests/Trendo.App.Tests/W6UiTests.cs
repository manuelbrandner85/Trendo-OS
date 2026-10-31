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


