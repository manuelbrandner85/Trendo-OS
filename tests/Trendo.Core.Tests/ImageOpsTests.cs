using System.Text.Json.Nodes;
using Trendo.Core.Media;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>Bildbearbeitung – Vorbild-Funktionen (Photoshop, GIMP, Krita; 04.11.2026): Tonwerte, Kurven, Auto-Tonwerte, Füllen, Reparieren, Klonen.</summary>
public class ImageOpsTests
{
    static byte[] Solid(int w, int h, byte r, byte g, byte b) { var a = new byte[w * h * 4]; for (var i = 0; i < w * h; i++) { a[i * 4] = r; a[i * 4 + 1] = g; a[i * 4 + 2] = b; a[i * 4 + 3] = 255; } return a; }

    [Fact(DisplayName = "Tonwerte: Schwarz- und Weißpunkt spreizen, Gamma hellt Mitten auf; ohne Angaben bleibt alles gleich")]
    public void Levels()
    {
        Assert.False(ImageOps.HasLut(null)); Assert.False(ImageOps.HasLut(new JsonObject { ["black"] = 0, ["white"] = 255, ["gamma"] = 1 }));
        var t = ImageOps.Lut(new JsonObject { ["black"] = 50, ["white"] = 200 });
        Assert.Equal(0, t[50]); Assert.Equal(0, t[20]); Assert.Equal(255, t[200]); Assert.Equal(255, t[230]); Assert.InRange(t[125], 126, 129);
        var g = ImageOps.Lut(new JsonObject { ["gamma"] = 2 });
        Assert.True(g[128] > 170, $"Gamma 2 hellt nicht auf: {g[128]}"); Assert.Equal(0, g[0]); Assert.Equal(255, g[255]);
        var layer = new JsonObject(); ImageDoc.SetFilter(layer, "gamma", 99); ImageDoc.SetFilter(layer, "white", 0);
        Assert.Equal(5, ImageDoc.N(((JsonObject)layer["filter"]!)["gamma"])); Assert.Equal(1, ImageDoc.N(((JsonObject)layer["filter"]!)["white"]));
    }

    [Fact(DisplayName = "Kurven: S-Kurve dunkelt Schatten ab und hellt Lichter auf, bleibt monoton (kein Überschwingen)")]
    public void Curves()
    {
        var layer = new JsonObject(); ImageDoc.SetCurve(layer, ImageOps.CurvePresets.First(c => c.Id == "s").Points);
        var t = ImageOps.Lut(layer["filter"] as JsonObject);
        Assert.True(t[64] < 56 && t[192] > 200, $"S-Kurve wirkt nicht: {t[64]}/{t[192]}");
        for (var i = 1; i < 256; i++) Assert.True(t[i] >= t[i - 1], $"nicht monoton bei {i}");
        ImageDoc.SetCurve(layer, []); Assert.False(ImageOps.HasLut(layer["filter"] as JsonObject));
    }

    [Fact(DisplayName = "Auto-Tonwerte: flaues Bild (Werte 60–180) bekommt Schwarzpunkt ~60 und Weißpunkt ~180")]
    public void Auto_levels()
    {
        var a = new byte[200 * 4];
        for (var i = 0; i < 200; i++) { var v = (byte)(60 + i * 120 / 199); a[i * 4] = a[i * 4 + 1] = a[i * 4 + 2] = v; a[i * 4 + 3] = 255; }
        var (b, w) = ImageOps.AutoLevels(a);
        Assert.InRange(b, 59, 62); Assert.InRange(w, 178, 181);
        Assert.Equal((0, 255), ImageOps.AutoLevels(new byte[16]));
    }

    [Fact(DisplayName = "Füllen: Farbeimer füllt nur die zusammenhängende Fläche innerhalb der Toleranz, nicht über eine Linie hinweg")]
    public void Flood_fill()
    {
        const int W = 40, H = 20; var a = Solid(W, H, 250, 250, 250);
        for (var y = 0; y < H; y++) { var i = (y * W + 20) * 4; a[i] = a[i + 1] = a[i + 2] = 0; } // schwarze Trennlinie
        for (var x = 0; x < 5; x++) { var i = (5 * W + x) * 4; a[i] = a[i + 1] = a[i + 2] = 230; } // leicht dunklere Stelle links
        var m = ImageOps.FloodMask(a, W, H, 3, 3, 32);
        Assert.Equal(20 * H, m.Count(v => v)); Assert.False(m[3 * W + 25]); Assert.True(m[5 * W + 2]);
        var strict = ImageOps.FloodMask(a, W, H, 3, 3, 5); Assert.False(strict[5 * W + 2]);
        Assert.All(ImageOps.FloodMask(a, W, H, -1, 3, 32), v => Assert.False(v));
    }

    [Fact(DisplayName = "Reparieren: Fleck in einem Verlauf verschwindet und der Verlauf läuft durch; Klonen kopiert von der Quelle")]
    public void Heal_and_clone()
    {
        const int W = 60, H = 30; var a = new byte[W * H * 4];
        for (var y = 0; y < H; y++) for (var x = 0; x < W; x++) { var i = (y * W + x) * 4; a[i] = (byte)(x * 4); a[i + 1] = 100; a[i + 2] = 50; a[i + 3] = 255; }
        var orig = (byte[])a.Clone();
        for (var y = 10; y < 20; y++) for (var x = 25; x < 35; x++) { var i = (y * W + x) * 4; a[i] = 255; a[i + 1] = 0; a[i + 2] = 255; } // magenta Fleck
        var mask = ImageOps.StrokeMask(W, H, [30, 15], 16);
        Assert.True(mask[15 * W + 30] && !mask[2 * W + 2]);
        ImageOps.Heal(a, W, H, mask);
        var c = (15 * W + 30) * 4;
        Assert.InRange(a[c], orig[c] - 8, orig[c] + 8); Assert.InRange(a[c + 1], 92, 108); Assert.InRange(a[c + 2], 42, 58);
        var b = Solid(W, H, 0, 0, 0); var src = Solid(W, H, 0, 0, 0); for (var i = 0; i < 4; i++) src[(5 * W + 50) * 4 + i] = 200;
        ImageOps.Clone(b, src, W, H, ImageOps.StrokeMask(W, H, [10, 5], 2), 40, 0);
        Assert.Equal(200, b[(5 * W + 10) * 4]);
    }

    [Fact(DisplayName = "Bild-Dokument: Verlauf, Retusche und Maske rücken beim Zuschneiden mit; Maske braucht eine Auswahl; Treffer übersieht Verlauf/Retusche")]
    public void Doc_layers()
    {
        var d = ImageDoc.New(200, 100);
        var s = ImageDoc.AddShape(d, "rect", 10, 10, 50, 50);
        var g = ImageDoc.AddGradient(d, 0, 0, 200, 0, "#FF0000", "#000000FF");
        var r = ImageDoc.Retouch(d, null); ImageDoc.AddFill(r, 50, 50, "#00FF00", 400); ImageDoc.AddHeal(r, [(80, 40)], 10);
        Assert.Same(r, ImageDoc.Retouch(d, null)); // oberste Retusche wird weiterbenutzt
        Assert.Equal((string?)s["id"], ImageDoc.HitTest(d, 20, 20));
        Assert.Throws<InvalidOperationException>(() => ImageDoc.SetMask(s, "rect", 0, 0, 1, 1));
        ImageDoc.SetMask(s, "ellipse", 60, 60, -40, -40, 999);
        var mk = (JsonObject)s["mask"]!; Assert.Equal((20.0, 40.0, 200.0), (ImageDoc.N(mk["x"]), ImageDoc.N(mk["w"]), ImageDoc.N(mk["feather"])));
        ImageDoc.Crop(d, 10, 10, 100, 50);
        Assert.Equal(-10, ImageDoc.N(g["x1"])); Assert.Equal(10, ImageDoc.N(mk["x"]));
        var ops = (JsonArray)r["ops"]!; Assert.Equal(40, ImageDoc.N(((JsonObject)ops[0]!)["px"])); Assert.Equal(255, ImageDoc.N(((JsonObject)ops[0]!)["tolerance"]));
        Assert.Equal(70, ImageDoc.N(((JsonArray)((JsonObject)ops[1]!)["points"]!)[0]));
        Assert.Equal((100.0, 50.0), (ImageDoc.N(g["w"]), ImageDoc.N(g["h"])));
    }
}
