using System.Buffers.Binary;
using Trendo.Core;
using Trendo.Core.Karten;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>Alle Studios, Runde 1 – SPEC-13: eigener PMTiles-v3-Leser im Offline-Kartenpaket. Synthetische Dateien aus einem kleinen Test-Schreiber;
/// echter Vergleich mit go-pmtiles nur mit TRENDO_PMTILES_EXE (sonst übersprungen). Beleg: reports/alle-studios/pmtiles-e2e.md.</summary>
public class PmTilesTests : IDisposable
{
    readonly string _dir = Path.Combine(Path.GetTempPath(), "trendo-pmtiles-" + Guid.NewGuid().ToString("N"));
    public PmTilesTests() => Directory.CreateDirectory(_dir);
    public void Dispose() { Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools(); try { Directory.Delete(_dir, true); } catch (IOException) { } }

    static void Var(List<byte> o, ulong v) { while (v >= 0x80) { o.Add((byte)(v | 0x80)); v >>= 7; } o.Add((byte)v); }
    /// <summary>Minimaler Schreiber: unkomprimiertes Wurzelverzeichnis, Kacheln je (z,x,y) als eigene Daten, Typ wählbar.</summary>
    string Write(string name, IEnumerable<(int Z, int X, int Y, byte[] Data)> tiles, byte type = 2, byte version = 3, int minZ = 0, int maxZ = 4)
    {
        var list = tiles.Select(t => (Id: PmTiles.TileId(t.Z, t.X, t.Y), t.Data)).OrderBy(t => t.Id).ToList();
        var data = new List<byte>(); var offs = new List<(ulong Off, int Len)>();
        foreach (var t in list) { offs.Add(((ulong)data.Count, t.Data.Length)); data.AddRange(t.Data); }
        var dir = new List<byte>(); Var(dir, (ulong)list.Count); ulong last = 0;
        foreach (var t in list) { Var(dir, t.Id - last); last = t.Id; }
        foreach (var _ in list) Var(dir, 1);
        foreach (var o in offs) Var(dir, (ulong)o.Len);
        for (var i = 0; i < offs.Count; i++) Var(dir, i > 0 && offs[i].Off == offs[i - 1].Off + (ulong)offs[i - 1].Len ? 0 : offs[i].Off + 1);
        var h = new byte[PmTiles.HeaderSize]; "PMTiles"u8.CopyTo(h); h[7] = version;
        void U(int o, ulong v) => BinaryPrimitives.WriteUInt64LittleEndian(h.AsSpan(o), v);
        void I(int o, double v) => BinaryPrimitives.WriteInt32LittleEndian(h.AsSpan(o), (int)Math.Round(v * 1e7));
        U(8, PmTiles.HeaderSize); U(16, (ulong)dir.Count); U(24, (ulong)(PmTiles.HeaderSize + dir.Count)); U(32, 0); U(40, (ulong)(PmTiles.HeaderSize + dir.Count)); U(48, 0);
        U(56, (ulong)(PmTiles.HeaderSize + dir.Count)); U(64, (ulong)data.Count); U(72, (ulong)list.Count); U(80, (ulong)list.Count); U(88, (ulong)list.Count);
        h[96] = 1; h[97] = 1; h[98] = 1; h[99] = type; h[100] = (byte)minZ; h[101] = (byte)maxZ; I(102, 5.8); I(106, 47.2); I(110, 15.1); I(114, 55.1);
        var f = Path.Combine(_dir, name); File.WriteAllBytes(f, [.. h, .. dir, .. data]); return f;
    }
    static byte[] T(int z, int x, int y) => [.. "TILE"u8, (byte)z, (byte)x, (byte)y];

    [Fact(DisplayName = "PMTiles +1: Kachelnummern nach Spezifikation (Hilbert-Kurve je Zoomstufe)")]
    public void Tile_ids()
    {
        Assert.Equal(0UL, PmTiles.TileId(0, 0, 0));
        Assert.Equal([1UL, 2, 3, 4], new[] { PmTiles.TileId(1, 0, 0), PmTiles.TileId(1, 0, 1), PmTiles.TileId(1, 1, 1), PmTiles.TileId(1, 1, 0) });
        Assert.Equal(5UL, PmTiles.TileId(2, 0, 0)); Assert.Equal(19078479UL, PmTiles.TileId(12, 3423, 1763));
        var ids = Enumerable.Range(0, 16).SelectMany(x => Enumerable.Range(0, 16).Select(y => PmTiles.TileId(4, x, y))).ToHashSet();
        Assert.Equal(256, ids.Count); Assert.Equal(85UL, ids.Min()); Assert.Equal(340UL, ids.Max()); // lückenlos
    }

    [Fact(DisplayName = "PMTiles +2: Verzeichnis mit Differenzen, Lauflängen und „0 = direkt danach“ wird richtig gelesen")]
    public void Directory_decode()
    {
        var d = new List<byte>(); Var(d, 3); Var(d, 5); Var(d, 1); Var(d, 10); Var(d, 1); Var(d, 2); Var(d, 0); Var(d, 100); Var(d, 50); Var(d, 7); Var(d, 1); Var(d, 0); Var(d, 501);
        var e = PmTiles.Directory(d.ToArray());
        Assert.Equal([5UL, 6, 16], e.Select(x => x.TileId)); Assert.Equal([1u, 2, 0], e.Select(x => x.RunLength));
        Assert.Equal([0UL, 100, 500], e.Select(x => x.Offset)); Assert.Equal([100u, 50, 7], e.Select(x => x.Length));
    }

    [Fact(DisplayName = "PMTiles +3: Offline-Kartenpaket liest Zoomstufen, Anzahl, Gebiet und jede Kachel (z/x/y wie im Web)")]
    public void Tile_pack_reads()
    {
        var tiles = Enumerable.Range(0, 4).SelectMany(z => Enumerable.Range(0, 1 << z).SelectMany(x => Enumerable.Range(0, 1 << z).Select(y => (z, x, y, T(z, x, y))))).ToList();
        using var p = new TilePack(Write("a.pmtiles", tiles, maxZ: 3));
        Assert.Equal((0, 3, 85), (p.MinZoom, p.MaxZoom, p.Count));
        foreach (var (z, x, y, data) in tiles) Assert.Equal(data, p.Get(z, x, y));
        var b = p.Bounds(); Assert.Equal(47.2, b.South, 5); Assert.Equal(5.8, b.West, 5); Assert.Equal(55.1, b.North, 5); Assert.Equal(15.1, b.East, 5);
    }

    [Fact(DisplayName = "PMTiles +4: fehlende Kachel, Zoom außerhalb und Koordinate außerhalb → null")]
    public void Missing_tiles()
    {
        using var p = new TilePack(Write("b.pmtiles", [(2, 1, 1, T(2, 1, 1))], minZ: 2, maxZ: 2));
        Assert.NotNull(p.Get(2, 1, 1)); Assert.Null(p.Get(2, 1, 2)); Assert.Null(p.Get(3, 2, 2)); Assert.Null(p.Get(2, 9, 0)); Assert.Null(p.Get(2, -1, 0));
    }

    [Fact(DisplayName = "PMTiles −1: Vektorkarte (MVT) wird verständlich abgelehnt")]
    public void Vector_refused()
    {
        var e = Assert.Throws<InvalidDataException>(() => new TilePack(Write("v.pmtiles", [(0, 0, 0, T(0, 0, 0))], type: 1)));
        Assert.Contains("Vektorkarte", e.Message);
    }

    [Fact(DisplayName = "PMTiles −2: falsche Kennung, Fassung 2 und zu kurze Datei werden abgelehnt")]
    public void Bad_header()
    {
        var f = Path.Combine(_dir, "x.pmtiles"); File.WriteAllBytes(f, new byte[200]); Assert.Throws<InvalidDataException>(() => new PmTiles(f));
        Assert.Contains("Fassung 2", Assert.Throws<InvalidDataException>(() => new PmTiles(Write("v2.pmtiles", [(0, 0, 0, T(0, 0, 0))], version: 2))).Message);
        File.WriteAllBytes(f, "PMTiles"u8.ToArray()); Assert.Throws<InvalidDataException>(() => new PmTiles(f));
    }

    [Fact(DisplayName = "PMTiles −3: Abschnitt zeigt hinter das Dateiende → beschädigt, keine Ausnahme anderer Art")]
    public void Out_of_range()
    {
        var f = Write("c.pmtiles", [(0, 0, 0, T(0, 0, 0))]); var b = File.ReadAllBytes(f);
        BinaryPrimitives.WriteUInt64LittleEndian(b.AsSpan(64), 10_000_000); File.WriteAllBytes(f, b);
        Assert.Contains("beschädigt", Assert.Throws<InvalidDataException>(() => new PmTiles(f)).Message);
    }

    [Fact(DisplayName = "PMTiles −4: kaputtes Verzeichnis (abgeschnittene Zahl) wird abgelehnt")]
    public void Broken_directory() => Assert.Throws<InvalidDataException>(() => PmTiles.Directory(new byte[] { 0x85, 0x80 }));

    [Fact(DisplayName = "PMTiles +5: Datei bleibt nach dem Schließen nicht gesperrt")]
    public void Released()
    {
        var f = Write("d.pmtiles", [(0, 0, 0, T(0, 0, 0))]);
        using (var p = new TilePack(f)) Assert.NotNull(p.Get(0, 0, 0));
        File.Delete(f); Assert.False(File.Exists(f));
    }

    [Fact(DisplayName = "PMTiles +6: Hinweis nennt das neue Format")]
    public void Message_names_pmtiles() => Assert.Contains("PMTiles", Assert.Throws<ArgumentException>(() => new TilePack(Path.Combine(_dir, "nichts.txt"))).Message);

    // ---------- echter Vergleich mit go-pmtiles ----------
    public sealed class PmFactAttribute : FactAttribute
    {
        public PmFactAttribute() { if (Exe() is null) Skip = "Übersprungen – TRENDO_PMTILES_EXE nicht gesetzt (go-pmtiles)."; }
    }
    static string? Exe() => Environment.GetEnvironmentVariable("TRENDO_PMTILES_EXE") is { Length: > 0 } p && File.Exists(p) ? p : null;

    string Mbtiles(string name, int maxZ)
    {
        var f = Path.Combine(_dir, name);
        using var db = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=" + f); db.Open();
        using (var c = db.CreateCommand()) { c.CommandText = "CREATE TABLE metadata (name text, value text); CREATE TABLE tiles (zoom_level integer, tile_column integer, tile_row integer, tile_data blob); INSERT INTO metadata VALUES ('format','png'),('minzoom','0'),('maxzoom','" + maxZ + "'),('name','Test');"; c.ExecuteNonQuery(); }
        using var tx = db.BeginTransaction(); using var ins = db.CreateCommand(); ins.Transaction = tx;
        ins.CommandText = "INSERT INTO tiles VALUES ($z,$x,$y,$d)"; var pz = ins.Parameters.Add("$z", Microsoft.Data.Sqlite.SqliteType.Integer); var px = ins.Parameters.Add("$x", Microsoft.Data.Sqlite.SqliteType.Integer); var py = ins.Parameters.Add("$y", Microsoft.Data.Sqlite.SqliteType.Integer); var pd = ins.Parameters.Add("$d", Microsoft.Data.Sqlite.SqliteType.Blob);
        for (var z = 0; z <= maxZ; z++) for (var x = 0; x < 1 << z; x++) for (var y = 0; y < 1 << z; y++)
        { pz.Value = z; px.Value = x; py.Value = (1 << z) - 1 - y; pd.Value = (byte[])[.. "\x89PNG"u8, (byte)z, (byte)(x >> 8), (byte)x, (byte)(y >> 8), (byte)y]; ins.ExecuteNonQuery(); }
        tx.Commit(); return f;
    }

    [PmFact(DisplayName = "PMTiles echt: go-pmtiles wandelt MBTiles um (bis 87 381 Kacheln, mit Blattverzeichnissen) – jede Kachel gleich wie im Original")]
    public void Real_convert_compare()
    {
        foreach (var maxZ in new[] { 4, 8 })
        {
            var mb = Mbtiles($"t{maxZ}.mbtiles", maxZ); var pm = Path.Combine(_dir, $"t{maxZ}.pmtiles");
            var r = Cli.Run(Exe()!, ["convert", mb, pm], _dir, 300_000); Assert.True(r.Ok, r.Err + r.Out);
            using var a = new TilePack(mb); using var b = new TilePack(pm);
            Assert.Equal((a.MinZoom, a.MaxZoom, a.Count), (b.MinZoom, b.MaxZoom, b.Count));
            for (var z = 0; z <= maxZ; z++) for (var x = 0; x < 1 << z; x++) for (var y = 0; y < 1 << z; y++) Assert.Equal(a.Get(z, x, y), b.Get(z, x, y));
            using var raw = new PmTiles(pm);
            Assert.Equal(PmTiles.Compression.Gzip, raw.Info.Internal);
            if (maxZ == 8) Assert.True(raw.Info.LeafLength > 0, "erwartet Blattverzeichnisse bei 87 381 Kacheln");
        }
    }
}
