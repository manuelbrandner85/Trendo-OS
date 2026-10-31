using System.Text;
using Trendo.Core.Sys;
using Xunit;

namespace Trendo.Core.Tests;

/// T-0302: Medien als Fotoalbum – Einlesen mit EXIF-Datum, Monatsgruppen, Doppelte, Alben und Favoriten.
public class PhotoAlbumTests
{
    /// <summary>Minimales JPEG mit EXIF-Aufnahmedatum (APP1) – reicht für die Datumserkennung.</summary>
    public static byte[] JpegWithExif(DateTime taken, string salt = "")
    {
        var exif = Encoding.ASCII.GetBytes("Exif\0\0" + taken.ToString("yyyy:MM:dd HH:mm:ss") + salt);
        var len = exif.Length + 2;
        return [0xFF, 0xD8, 0xFF, 0xE1, (byte)(len >> 8), (byte)len, .. exif, 0xFF, 0xD9];
    }

    [Fact(DisplayName = "T-0302: Einlesen – nur Bilder, EXIF-Datum vor Dateidatum, neueste zuerst, versteckte Ordner nicht, nach Monaten gruppiert")]
    public void Scan_and_months()
    {
        var dir = Path.Combine(Path.GetTempPath(), "trendo-fotos-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(Path.Combine(dir, "Urlaub"));
            File.WriteAllBytes(Path.Combine(dir, "Urlaub", "strand.jpg"), JpegWithExif(new DateTime(2025, 7, 14, 10, 30, 0)));
            var png = Path.Combine(dir, "plan.png"); File.WriteAllBytes(png, [1, 2, 3]); File.SetLastWriteTime(png, new DateTime(2026, 10, 3));
            var png2 = Path.Combine(dir, "skizze.PNG"); File.WriteAllBytes(png2, [4, 5]); File.SetLastWriteTime(png2, new DateTime(2026, 10, 20));
            File.WriteAllText(Path.Combine(dir, "notiz.txt"), "kein Bild");
            var hidden = Directory.CreateDirectory(Path.Combine(dir, "versteckt")); hidden.Attributes |= FileAttributes.Hidden; File.WriteAllBytes(Path.Combine(hidden.FullName, "x.jpg"), [9]);
            var all = PhotoAlbum.Scan([dir, dir]);
            Assert.Equal(["skizze.PNG", "plan.png", "strand.jpg"], all.Select(p => Path.GetFileName(p.Path)));
            Assert.True(all[2].FromExif); Assert.Equal(new DateTime(2025, 7, 14, 10, 30, 0), all[2].Taken); Assert.False(all[0].FromExif);
            var months = PhotoAlbum.ByMonth(all);
            Assert.Equal(["Oktober 2026", "Juli 2025"], months.Select(m => m.Title)); Assert.Equal(2, months[0].Photos.Count);
            Assert.Single(PhotoAlbum.Scan([dir], max: 1));
        }
        finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
    }

    [Fact(DisplayName = "T-0302: Doppelte nach Inhalt (nicht Name), gleiche Größe aber anderer Inhalt ist keine Kopie")]
    public void Duplicates()
    {
        var dir = Path.Combine(Path.GetTempPath(), "trendo-fotos-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(dir);
            File.WriteAllBytes(Path.Combine(dir, "a.jpg"), [1, 2, 3, 4]); File.WriteAllBytes(Path.Combine(dir, "a-kopie.jpg"), [1, 2, 3, 4]);
            File.WriteAllBytes(Path.Combine(dir, "b.jpg"), [1, 2, 3, 5]); File.WriteAllBytes(Path.Combine(dir, "c.png"), [7]);
            var d = PhotoAlbum.Duplicates(PhotoAlbum.Scan([dir]));
            Assert.Single(d); Assert.Equal(2, d[0].Count); Assert.All(d[0], p => Assert.StartsWith("a", Path.GetFileName(p.Path)));
        }
        finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
    }

    [Fact(DisplayName = "T-0302: Alben – Favoriten immer da und zuerst, anlegen (kein doppelter Name), hinzufügen ohne Doppelte, entfernen, vergessen, löschen")]
    public void Albums()
    {
        using var f = new Fixture(); var a = new PhotoAlbum(f.Core.DataRoot);
        Assert.Equal([("Favoriten", 0)], a.Albums());
        a.Create("Urlaub 2025"); Assert.Throws<ArgumentException>(() => a.Create("urlaub 2025")); Assert.Throws<ArgumentException>(() => a.Create("  "));
        Assert.Equal(2, a.Add("Urlaub 2025", [@"C:\x\1.jpg", @"C:\x\2.jpg"])); Assert.Equal(0, a.Add("Urlaub 2025", [@"c:\x\1.JPG"]));
        Assert.True(a.ToggleFavorite(@"C:\x\1.jpg")); Assert.True(a.IsFavorite(@"C:\x\1.jpg"));
        Assert.Equal([("Favoriten", 1), ("Urlaub 2025", 2)], a.Albums());
        Assert.Equal(2, a.Forget([@"C:\x\1.jpg"])); Assert.Equal([@"C:\x\2.jpg"], a.In("Urlaub 2025"));
        a.Remove("Urlaub 2025", [@"C:\x\2.jpg"]); Assert.Empty(a.In("Urlaub 2025"));
        Assert.Throws<ArgumentException>(() => a.Delete("Favoriten")); a.Delete("Urlaub 2025"); Assert.Single(a.Albums());
    }
}
