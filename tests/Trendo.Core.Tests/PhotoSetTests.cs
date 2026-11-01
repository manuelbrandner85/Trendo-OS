using Trendo.Core;
using Xunit;

namespace Trendo.Core.Tests;

/// Scan-Arbeitsplatz: Fotoserie vor der Rechnung prüfen – Auflösung aus JPEG/PNG-Kopf, doppelte Fotos, Hinweise zu Anzahl und Größe.
public class PhotoSetTests
{
    static byte[] Png(int w, int h, byte extra = 0) =>
        [0x89, (byte)'P', (byte)'N', (byte)'G', 13, 10, 26, 10, 0, 0, 0, 13, (byte)'I', (byte)'H', (byte)'D', (byte)'R',
         (byte)(w >> 24), (byte)(w >> 16), (byte)(w >> 8), (byte)w, (byte)(h >> 24), (byte)(h >> 16), (byte)(h >> 8), (byte)h, 8, 2, 0, 0, 0, extra];
    static byte[] Jpg(int w, int h, byte extra = 0) =>
        [0xFF, 0xD8, 0xFF, 0xE0, 0, 16, (byte)'J', (byte)'F', (byte)'I', (byte)'F', 0, 1, 1, 0, 0, 1, 0, 1, 0, 0,   // APP0 (16 Bytes)
         0xFF, 0xC0, 0, 17, 8, (byte)(h >> 8), (byte)h, (byte)(w >> 8), (byte)w, 3, 1, 0x22, 0, 2, 0x11, 1, 3, 0x11, 1, extra, 0xFF, 0xD9];

    [Fact(DisplayName = "Scan-Arbeitsplatz: Auflösung aus PNG- und JPEG-Kopf (auch nach APP-Block), Unlesbares ergibt nichts")]
    public void Image_size()
    {
        var d = Path.Combine(Path.GetTempPath(), "trendo-photoset-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(d);
        File.WriteAllBytes(Path.Combine(d, "a.png"), Png(4000, 3000)); File.WriteAllBytes(Path.Combine(d, "b.jpg"), Jpg(6000, 4000));
        File.WriteAllText(Path.Combine(d, "c.jpg"), "kein Bild");
        Assert.Equal((4000, 3000), PhotoSet.ImageSize(Path.Combine(d, "a.png")));
        Assert.Equal((6000, 4000), PhotoSet.ImageSize(Path.Combine(d, "b.jpg")));
        Assert.Null(PhotoSet.ImageSize(Path.Combine(d, "c.jpg")));
        Assert.Null(PhotoSet.ImageSize(Path.Combine(d, "fehlt.jpg")));
    }

    [Fact(DisplayName = "Scan-Arbeitsplatz: Prüfbericht – zu wenige Fotos, wenige Fotos, kleine und gemischte Auflösungen, doppelte Fotos; gute Serie ohne Hinweise")]
    public void Report()
    {
        var root = Path.Combine(Path.GetTempPath(), "trendo-photoset-" + Guid.NewGuid().ToString("N"));
        string Make(string name, Func<int, byte[]> photo, int n) { var d = Path.Combine(root, name); Directory.CreateDirectory(d); for (var i = 0; i < n; i++) File.WriteAllBytes(Path.Combine(d, $"f{i:000}.jpg"), photo(i)); return d; }
        var two = PhotoSet.Check(Make("zwei", i => Jpg(4000, 3000, (byte)i), 2));
        Assert.False(two.Ready); Assert.Contains("3", two.Warnings[0]);
        var few = PhotoSet.Check(Make("wenig", i => Jpg(4000, 3000, (byte)i), 5));
        Assert.True(few.Ready); Assert.Single(few.Warnings); Assert.Contains("20 bis 80", few.Warnings[0]);
        var mixed = PhotoSet.Check(Make("gemischt", i => i == 0 ? Jpg(800, 600) : Jpg(4000, 3000, (byte)i), 25));
        Assert.Contains(mixed.Warnings, w => w.Contains("klein (800 × 600"));
        Assert.Contains(mixed.Warnings, w => w.Contains("verschiedene Größen"));
        Assert.Equal((800, 600), mixed.Smallest); Assert.Equal((4000, 3000), mixed.Largest);
        var dup = PhotoSet.Check(Make("doppelt", i => Jpg(4000, 3000, (byte)(i % 22)), 25)); // 3 Fotos doppelt
        Assert.Equal(3, dup.Duplicates); Assert.Contains(dup.Warnings, w => w.StartsWith("3 doppelte"));
        var good = PhotoSet.Check(Make("gut", i => Jpg(i % 2 == 0 ? 4000 : 3000, i % 2 == 0 ? 3000 : 4000, (byte)i), 30)); // hoch und quer zählt gleich
        Assert.Empty(good.Warnings); Assert.Equal(30, good.Count); Assert.True(good.Bytes > 0);
        Assert.Contains("gibt es nicht", PhotoSet.Check(Path.Combine(root, "fehlt")).Warnings[0]);
    }
}
