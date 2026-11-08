using System.Buffers.Binary;
using System.IO.Compression;

namespace Trendo.Core.Karten;

/// <summary>Alle Studios, Runde 1 – SPEC-13 „Routen & Karten“: Leser für PMTiles v3 (offenes Einzeldatei-Format von Protomaps, Spezifikation BSD-3-Clause),
/// eigener Code ohne Fremdbibliothek. Liest Kopf, Wurzel- und Blattverzeichnisse (gzip/brotli oder unkomprimiert) und einzelne Kacheln per Hilbert-Kachelnummer.
/// Trendo zeigt Rasterkacheln (PNG/JPEG/WebP); Vektorkacheln (MVT) werden erkannt und klar abgelehnt. Geprüft gegen Dateien von go-pmtiles 1.31.2.</summary>
public sealed class PmTiles : IDisposable
{
    public enum Compression : byte { Unknown = 0, None = 1, Gzip = 2, Brotli = 3, Zstd = 4 }
    public enum TileType : byte { Unknown = 0, Mvt = 1, Png = 2, Jpeg = 3, Webp = 4, Avif = 5 }
    public sealed record Header(ulong RootOffset, ulong RootLength, ulong MetadataOffset, ulong MetadataLength, ulong LeafOffset, ulong LeafLength,
        ulong DataOffset, ulong DataLength, ulong AddressedTiles, ulong TileEntries, ulong TileContents, bool Clustered, Compression Internal, Compression TileCompression,
        TileType Type, int MinZoom, int MaxZoom, double MinLon, double MinLat, double MaxLon, double MaxLat);
    public readonly record struct Entry(ulong TileId, ulong Offset, uint Length, uint RunLength);

    public const int HeaderSize = 127, MaxDepth = 4;
    const int MaxDirBytes = 64 * 1024 * 1024, MaxTileBytes = 32 * 1024 * 1024;
    readonly FileStream _f; readonly Entry[] _root; readonly Dictionary<(ulong, ulong), Entry[]> _leaves = [];
    public Header Info { get; }

    public PmTiles(string path)
    {
        _f = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        try
        {
            if (_f.Length < HeaderSize) throw new InvalidDataException("Diese Datei ist kein PMTiles-Kartenpaket (zu kurz).");
            Info = ParseHeader(Read(0, HeaderSize), _f.Length);
            if (Info.Internal == Compression.Zstd) throw new InvalidDataException("Dieses PMTiles-Paket nutzt eine Kompression (zstd), die Trendo nicht liest.");
            _root = Directory(Decompress(Read(Info.RootOffset, Info.RootLength), Info.Internal));
        }
        catch { _f.Dispose(); throw; }
    }

    public static Header ParseHeader(byte[] b, long fileLength)
    {
        if (b.Length < HeaderSize || !b.AsSpan(0, 7).SequenceEqual("PMTiles"u8)) throw new InvalidDataException("Diese Datei ist kein PMTiles-Kartenpaket.");
        if (b[7] != 3) throw new InvalidDataException($"PMTiles-Fassung {b[7]} wird nicht unterstützt (nur 3).");
        ulong U(int o) => BinaryPrimitives.ReadUInt64LittleEndian(b.AsSpan(o)); double E7(int o) => BinaryPrimitives.ReadInt32LittleEndian(b.AsSpan(o)) / 1e7;
        var h = new Header(U(8), U(16), U(24), U(32), U(40), U(48), U(56), U(64), U(72), U(80), U(88), b[96] == 1, (Compression)b[97], (Compression)b[98], (TileType)b[99],
            b[100], b[101], E7(102), E7(106), E7(110), E7(114));
        foreach (var (o, l) in new[] { (h.RootOffset, h.RootLength), (h.MetadataOffset, h.MetadataLength), (h.LeafOffset, h.LeafLength), (h.DataOffset, h.DataLength) })
            if (o > (ulong)fileLength || l > (ulong)fileLength - o) throw new InvalidDataException("PMTiles-Paket ist beschädigt (Abschnitt außerhalb der Datei).");
        if (h.RootLength == 0 || h.RootLength > MaxDirBytes) throw new InvalidDataException("PMTiles-Paket ist beschädigt (Wurzelverzeichnis).");
        if (h.MinZoom > h.MaxZoom || h.MaxZoom > 30) throw new InvalidDataException("PMTiles-Paket ist beschädigt (Zoomstufen).");
        return h;
    }

    /// <summary>Kachelnummer nach PMTiles: alle Kacheln niedrigerer Zoomstufen davor, innerhalb der Stufe entlang der Hilbert-Kurve.</summary>
    public static ulong TileId(int z, long x, long y)
    {
        if (z is < 0 or > 26) throw new ArgumentOutOfRangeException(nameof(z));
        long n = 1L << z; if (x < 0 || y < 0 || x >= n || y >= n) throw new ArgumentOutOfRangeException(nameof(x), "Kachel außerhalb der Zoomstufe.");
        ulong acc = 0; for (var t = 0; t < z; t++) acc += 1UL << (2 * t);
        ulong d = 0;
        for (var s = n / 2; s > 0; s /= 2)
        {
            long rx = (x & s) > 0 ? 1 : 0, ry = (y & s) > 0 ? 1 : 0;
            d += (ulong)(s * s * ((3 * rx) ^ ry));
            if (ry == 0) { if (rx == 1) { x = n - 1 - x; y = n - 1 - y; } (x, y) = (y, x); }
        }
        return acc + d;
    }

    /// <summary>Verzeichnis (bereits entpackt) lesen: Anzahl, Kachelnummern als Differenzen, Lauflängen, Längen, Versätze (0 = direkt nach dem vorigen).</summary>
    public static Entry[] Directory(ReadOnlySpan<byte> b)
    {
        var pos = 0;
        ulong V(ReadOnlySpan<byte> s)
        {
            ulong v = 0; for (var shift = 0; shift < 64; shift += 7) { if (pos >= s.Length) throw new InvalidDataException("PMTiles-Verzeichnis ist beschädigt."); var c = s[pos++]; v |= (ulong)(c & 0x7F) << shift; if ((c & 0x80) == 0) return v; }
            throw new InvalidDataException("PMTiles-Verzeichnis ist beschädigt.");
        }
        var count = V(b); if (count > (ulong)b.Length) throw new InvalidDataException("PMTiles-Verzeichnis ist beschädigt.");
        var e = new Entry[(int)count]; ulong id = 0;
        for (var i = 0; i < e.Length; i++) { id += V(b); e[i] = e[i] with { TileId = id }; }
        for (var i = 0; i < e.Length; i++) e[i] = e[i] with { RunLength = checked((uint)V(b)) };
        for (var i = 0; i < e.Length; i++) e[i] = e[i] with { Length = checked((uint)V(b)) };
        for (var i = 0; i < e.Length; i++) { var v = V(b); e[i] = e[i] with { Offset = v == 0 && i > 0 ? e[i - 1].Offset + e[i - 1].Length : v - 1 }; }
        return e;
    }

    /// <summary>Kachel als Bilddaten (z/x/y wie im Web, y von oben) oder null, wenn es sie nicht gibt.</summary>
    public byte[]? Get(int z, int x, int y)
    {
        if (z < Info.MinZoom || z > Info.MaxZoom || x < 0 || y < 0 || x >= 1L << z || y >= 1L << z) return null;
        var id = TileId(z, x, y); var dir = _root;
        for (var depth = 0; depth < MaxDepth; depth++)
        {
            var e = Find(dir, id); if (e is not { } en) return null;
            if (en.RunLength > 0)
            {
                if (en.Length > MaxTileBytes) throw new InvalidDataException("Kachel zu groß – Paket beschädigt?");
                return Decompress(Read(Info.DataOffset + en.Offset, en.Length), Info.TileCompression);
            }
            var key = (en.Offset, (ulong)en.Length);
            lock (_leaves) if (!_leaves.TryGetValue(key, out dir!)) _leaves[key] = dir = Directory(Decompress(Read(Info.LeafOffset + en.Offset, en.Length), Info.Internal));
        }
        throw new InvalidDataException("PMTiles-Verzeichnisse zu tief verschachtelt – Paket beschädigt?");
    }

    static Entry? Find(Entry[] dir, ulong id)
    {
        int lo = 0, hi = dir.Length - 1;
        while (lo <= hi) { var mid = (lo + hi) / 2; var c = dir[mid].TileId; if (c == id) return dir[mid]; if (c < id) lo = mid + 1; else hi = mid - 1; }
        if (hi < 0) return null; var e = dir[hi];
        if (e.RunLength == 0) return e; // Blattverzeichnis deckt den Bereich ab
        return id - e.TileId < e.RunLength ? e : null;
    }

    byte[] Read(ulong offset, ulong length)
    {
        if (length > MaxDirBytes || offset + length > (ulong)_f.Length) throw new InvalidDataException("PMTiles-Paket ist beschädigt (Bereich außerhalb der Datei).");
        var buf = new byte[length];
        lock (_f) { _f.Position = (long)offset; _f.ReadExactly(buf); }
        return buf;
    }

    static byte[] Decompress(byte[] data, Compression c)
    {
        if (c is Compression.None or Compression.Unknown) return data;
        if (c == Compression.Zstd) throw new InvalidDataException("zstd-komprimierte PMTiles werden nicht gelesen.");
        using var src = new MemoryStream(data);
        using Stream z = c == Compression.Gzip ? new GZipStream(src, CompressionMode.Decompress) : new BrotliStream(src, CompressionMode.Decompress);
        using var o = new MemoryStream(); var buf = new byte[81920]; int n;
        while ((n = z.Read(buf)) > 0) { o.Write(buf, 0, n); if (o.Length > MaxDirBytes) throw new InvalidDataException("Entpackte Daten zu groß – Paket beschädigt?"); }
        return o.ToArray();
    }

    public bool IsRaster => Info.Type is TileType.Png or TileType.Jpeg or TileType.Webp;
    public void Dispose() => _f.Dispose();
}
