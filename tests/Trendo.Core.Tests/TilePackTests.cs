using System.IO.Compression;
using Trendo.Core.Karten;
using Xunit;
namespace Trendo.Core.Tests;

/// <summary>T-0274b: Offline-Kartenpaket aus Ordner oder ZIP (z/x/y.png).</summary>
public class T0274b_TilePack
{
    [Fact(DisplayName = "T-0274b: Kachel-Ordner, ZIP und MBTiles – Zoomstufen, Kachel lesen, Gebiet; leerer Ordner und falsche Datei werden verständlich abgelehnt")]
    public void Folder_and_zip()
    {
        var dir = Path.Combine(Path.GetTempPath(), "trendo-pack-" + Guid.NewGuid().ToString("N")[..6]);
        try
        {
            var tiles = Path.Combine(dir, "kacheln");
            foreach (var (z, x, y) in new[] { (12, 2177, 1420), (12, 2178, 1420), (13, 4355, 2841) })
            { var f = Path.Combine(tiles, z.ToString(), x.ToString(), y + ".png"); Directory.CreateDirectory(Path.GetDirectoryName(f)!); File.WriteAllBytes(f, [1, 2, 3, (byte)z]); }
            File.WriteAllText(Path.Combine(tiles, "liesmich.txt"), "kein Kachelbild");
            using (var p = new TilePack(tiles))
            {
                Assert.Equal(3, p.Count); Assert.Equal(12, p.MinZoom); Assert.Equal(13, p.MaxZoom);
                Assert.Equal(new byte[] { 1, 2, 3, 12 }, p.Get(12, 2178, 1420)); Assert.Null(p.Get(12, 1, 1));
                var (s, w, n, e) = p.Bounds(); Assert.InRange((s + n) / 2, 48.1, 48.2); Assert.InRange((w + e) / 2, 11.3, 11.5); // München
            }
            var zip = Path.Combine(dir, "paket.zip"); ZipFile.CreateFromDirectory(tiles, zip);
            using (var z = new TilePack(zip)) { Assert.Equal(3, z.Count); Assert.Equal(new byte[] { 1, 2, 3, 13 }, z.Get(13, 4355, 2841)); }
            var empty = Path.Combine(dir, "leer"); Directory.CreateDirectory(empty);
            Assert.Throws<InvalidDataException>(() => new TilePack(empty));
            Assert.Throws<ArgumentException>(() => new TilePack(Path.Combine(dir, "fehlt.mbtiles")));
            // MBTiles: Zeilen von unten (TMS), schreibgeschützt geöffnet
            var mb = Path.Combine(dir, "paket.mbtiles");
            using (var con = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=" + mb))
            {
                con.Open(); using var c = con.CreateCommand();
                c.CommandText = "CREATE TABLE metadata (name text, value text); CREATE TABLE tiles (zoom_level integer, tile_column integer, tile_row integer, tile_data blob);"; c.ExecuteNonQuery();
                foreach (var (z, x, y) in new[] { (12, 2177, 1420), (12, 2178, 1420) })
                { using var ins = con.CreateCommand(); ins.CommandText = "INSERT INTO tiles VALUES ($z,$x,$r,$d)"; ins.Parameters.AddWithValue("$z", z); ins.Parameters.AddWithValue("$x", x); ins.Parameters.AddWithValue("$r", (1 << z) - 1 - y); ins.Parameters.AddWithValue("$d", new byte[] { 9, (byte)(x % 256) }); ins.ExecuteNonQuery(); }
            }
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            using (var m = new TilePack(mb))
            {
                Assert.Equal(2, m.Count); Assert.Equal(12, m.MaxZoom); Assert.Equal(new byte[] { 9, 2178 % 256 }, m.Get(12, 2178, 1420)); Assert.Null(m.Get(12, 2178, 1421));
                var (s2, w2, n2, e2) = m.Bounds(); Assert.InRange((s2 + n2) / 2, 48.1, 48.2); Assert.InRange((w2 + e2) / 2, 11.3, 11.5);
            }
            File.WriteAllText(Path.Combine(dir, "kaputt.mbtiles"), "kein sqlite");
            Assert.Throws<InvalidDataException>(() => new TilePack(Path.Combine(dir, "kaputt.mbtiles")));
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }
}
