using System.Buffers.Binary;
using System.IO.Compression;
using Trendo.Core.Karten;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>Alle Studios, Runde 1 – SPEC-13: Vektorkarten (MVT in PMTiles) für die MapLibre-Seite. Synthetische Datei aus einem Test-Schreiber (gzip wie Protomaps);
/// echtes Protomaps-Paket nur mit TRENDO_VEKTOR_PMTILES, sonst übersprungen. Beleg: reports/alle-studios/vektorkarten-e2e.md.</summary>
public class VectorMapTests : IDisposable
{
    readonly string _dir = Path.Combine(Path.GetTempPath(), "trendo-vektor-" + Guid.NewGuid().ToString("N"));
    public VectorMapTests() => Directory.CreateDirectory(_dir);
    public void Dispose() { try { Directory.Delete(_dir, true); } catch (IOException) { } }

    static void Var(List<byte> o, ulong v) { while (v >= 0x80) { o.Add((byte)(v | 0x80)); v >>= 7; } o.Add((byte)v); }
    static byte[] Gz(byte[] b) { using var ms = new MemoryStream(); using (var g = new GZipStream(ms, CompressionLevel.Fastest)) g.Write(b); return ms.ToArray(); }
    /// <summary>Kleine Vektor-PMTiles: Verzeichnis und Kacheln gzip-komprimiert (wie Protomaps), Typ MVT.</summary>
    string Write(byte type = 1)
    {
        var tiles = new[] { (Id: PmTiles.TileId(0, 0, 0), Data: Gz("MVT-0"u8.ToArray())), (Id: PmTiles.TileId(1, 1, 0), Data: Gz("MVT-1"u8.ToArray())) };
        var data = new List<byte>(); var dir = new List<byte>(); Var(dir, 2); Var(dir, tiles[0].Id); Var(dir, tiles[1].Id - tiles[0].Id); Var(dir, 1); Var(dir, 1);
        Var(dir, (ulong)tiles[0].Data.Length); Var(dir, (ulong)tiles[1].Data.Length); Var(dir, 1); Var(dir, 0);
        foreach (var t in tiles) data.AddRange(t.Data);
        var root = Gz([.. dir]); var h = new byte[PmTiles.HeaderSize]; "PMTiles"u8.CopyTo(h); h[7] = 3;
        void U(int o, ulong v) => BinaryPrimitives.WriteUInt64LittleEndian(h.AsSpan(o), v);
        U(8, 127); U(16, (ulong)root.Length); U(24, (ulong)(127 + root.Length)); U(40, (ulong)(127 + root.Length)); U(56, (ulong)(127 + root.Length)); U(64, (ulong)data.Count); U(72, 2); U(80, 2); U(88, 2);
        h[96] = 1; h[97] = 2; h[98] = 2; h[99] = type; h[100] = 0; h[101] = 1;
        BinaryPrimitives.WriteInt32LittleEndian(h.AsSpan(102), 113800000); BinaryPrimitives.WriteInt32LittleEndian(h.AsSpan(106), 472500000);
        BinaryPrimitives.WriteInt32LittleEndian(h.AsSpan(110), 114200000); BinaryPrimitives.WriteInt32LittleEndian(h.AsSpan(114), 472800000);
        var f = Path.Combine(_dir, "ort.pmtiles"); File.WriteAllBytes(f, [.. h, .. root, .. data]); return f;
    }

    [Fact(DisplayName = "Vektorkarte +1: Startaufruf mit Zoomstufen und Gebiet aus dem PMTiles-Kopf")]
    public void Init_script()
    {
        using var m = new VectorMap(Write());
        Assert.Equal("window.trendoKarte({\"name\":\"ort\",\"minzoom\":0,\"maxzoom\":1,\"bounds\":[11.38,47.25,11.42,47.28]})", m.InitScript());
    }

    [Fact(DisplayName = "Vektorkarte +2: Kachelanfrage → entpackte Kachel als Base64; fehlende Kachel → leer")]
    public void Tile_answer()
    {
        using var m = new VectorMap(Write());
        var a = m.Answer(VectorMap.Parse("{\"art\":\"kachel\",\"id\":7,\"z\":1,\"x\":1,\"y\":0}")!);
        Assert.Equal($"window.trendoKachel(7,\"{Convert.ToBase64String("MVT-1"u8.ToArray())}\")", a);
        Assert.Equal("window.trendoKachel(8,\"\")", m.Answer(VectorMap.Parse("{\"art\":\"kachel\",\"id\":8,\"z\":1,\"x\":0,\"y\":1}")!));
    }

    [Fact(DisplayName = "Vektorkarte +3: Nachrichten der Seite – bereit, geladen, Fehlertext gekürzt")]
    public void Parse_kinds()
    {
        Assert.Equal("bereit", VectorMap.Parse("{\"art\":\"bereit\"}")!.Kind);
        Assert.Equal("geladen", VectorMap.Parse("{\"art\":\"geladen\"}")!.Kind);
        Assert.Equal(300, VectorMap.Parse("{\"art\":\"fehler\",\"text\":\"" + new string('x', 500) + "\"}")!.Text.Length);
    }

    [Fact(DisplayName = "Vektorkarte −1: kaputte, fremde oder zu große Nachrichten werden ignoriert")]
    public void Parse_refuses()
    {
        Assert.Null(VectorMap.Parse(null)); Assert.Null(VectorMap.Parse("kein json")); Assert.Null(VectorMap.Parse("[1,2]"));
        Assert.Null(VectorMap.Parse("{\"art\":\"loeschen\"}")); Assert.Null(VectorMap.Parse("{\"art\":\"kachel\",\"x\":\"" + new string('1', 20_000) + "\"}"));
    }

    [Fact(DisplayName = "Vektorkarte −2: unsinnige Kachelanfrage (negativ, Zoom > 30, falsche Art) → keine Antwort")]
    public void Answer_refuses()
    {
        using var m = new VectorMap(Write());
        Assert.Null(m.Answer(VectorMap.Parse("{\"art\":\"kachel\",\"id\":1,\"z\":-1,\"x\":0,\"y\":0}")!));
        Assert.Null(m.Answer(VectorMap.Parse("{\"art\":\"kachel\",\"id\":1,\"z\":31,\"x\":0,\"y\":0}")!));
        Assert.Null(m.Answer(VectorMap.Parse("{\"art\":\"bereit\"}")!));
        Assert.Equal("window.trendoKachel(2,\"\")", m.Answer(VectorMap.Parse("{\"art\":\"kachel\",\"id\":2,\"z\":1,\"x\":5,\"y\":0}")!));
    }

    [Fact(DisplayName = "Vektorkarte −3: Rasterpaket oder andere Datei wird nicht als Vektorkarte geöffnet")]
    public void Raster_refused()
    {
        Assert.Throws<InvalidDataException>(() => new VectorMap(Write(type: 2)));
        Assert.Throws<ArgumentException>(() => new VectorMap(Path.Combine(_dir, "x.mbtiles")));
    }

    [Fact(DisplayName = "Vektorkarte +4: Rasterleser lehnt Vektorkarte weiter ab (Karten-Studio schaltet dann auf MapLibre)")]
    public void TilePack_still_refuses() => Assert.Contains("Vektorkarte", Assert.Throws<InvalidDataException>(() => new TilePack(Write())).Message);

    public sealed class VecFactAttribute : FactAttribute
    {
        public VecFactAttribute() { if (Real() is null) Skip = "Übersprungen – TRENDO_VEKTOR_PMTILES nicht gesetzt (Protomaps-Ausschnitt)."; }
    }
    static string? Real() => EchtWerkzeuge.VektorPmtiles();

    [VecFact(DisplayName = "Vektorkarte echt: Protomaps-Ausschnitt – alle Kacheln lesbar, entpackt als MVT (Protobuf), Gebiet stimmt")]
    public void Real_protomaps()
    {
        using var m = new VectorMap(Real()!); var i = m.Info;
        Assert.Equal(PmTiles.Compression.Gzip, i.TileCompression);
        var found = 0; long bytes = 0;
        for (var z = i.MinZoom; z <= i.MaxZoom; z++)
        {
            double n = 1 << z; int X(double lon) => (int)Math.Floor((lon + 180) / 360 * n);
            int Y(double lat) { var r = lat * Math.PI / 180; return (int)Math.Floor((1 - Math.Log(Math.Tan(r) + 1 / Math.Cos(r)) / Math.PI) / 2 * n); }
            for (var x = X(i.MinLon); x <= X(i.MaxLon); x++) for (var y = Y(i.MaxLat); y <= Y(i.MinLat); y++)
            {
                var a = m.Answer(new VectorMap.Message("kachel", 1, z, x, y, ""))!; var b64 = a[(a.IndexOf('"') + 1)..a.LastIndexOf('"')];
                if (b64.Length == 0) continue; var raw = Convert.FromBase64String(b64); found++; bytes += raw.Length;
                Assert.Equal(0x1A, raw[0]); // MVT: erstes Feld „layers“ (Protobuf-Feld 3, Länge) – entpackt, kein gzip mehr
            }
        }
        Assert.Equal((long)i.AddressedTiles, found); Assert.True(bytes > 100_000);
    }
}
