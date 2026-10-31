using System.IO.Compression;
using System.Text.RegularExpressions;

namespace Trendo.Core.Karten;

/// <summary>T-0274b: Offline-Kartenpaket – MBTiles (SQLite, schreibgeschützt geöffnet) oder Ordner/ZIP mit Kacheln im Schema z/x/y.png (oder .jpg/.webp), wie es Kachel-Werkzeuge
/// (z. B. MOBAC „OSMAnd/Ordner“) oder ein eigener Kachelserver liefern. Trendo lädt dafür selbst nichts massenhaft herunter
/// (die OSM-Kachelrichtlinie verbietet das); das Paket bringt der Nutzer mit.</summary>
public sealed partial class TilePack : IDisposable
{
    [GeneratedRegex(@"(?:^|/)(\d{1,2})/(\d+)/(\d+)\.(png|jpe?g|webp)$", RegexOptions.IgnoreCase)] private static partial Regex TilePath();
    readonly Dictionary<(int Z, int X, int Y), string> _index = [];
    readonly ZipArchive? _zip; readonly string? _dir; readonly Microsoft.Data.Sqlite.SqliteConnection? _db;
    int _dbMin, _dbMax, _dbCount;
    public string Source { get; }
    public int MinZoom => _db is not null ? _dbMin : _index.Count == 0 ? 0 : _index.Keys.Min(k => k.Z);
    public int MaxZoom => _db is not null ? _dbMax : _index.Count == 0 ? 0 : _index.Keys.Max(k => k.Z);
    public int Count => _db is not null ? _dbCount : _index.Count;

    public TilePack(string path)
    {
        Source = path;
        if (Directory.Exists(path))
        {
            _dir = path;
            foreach (var f in Directory.EnumerateFiles(path, "*.*", SearchOption.AllDirectories)) Add(Path.GetRelativePath(path, f).Replace('\\', '/'));
        }
        else if (File.Exists(path) && path.EndsWith(".mbtiles", StringComparison.OrdinalIgnoreCase))
        {
            _db = new Microsoft.Data.Sqlite.SqliteConnection(new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder { DataSource = path, Mode = Microsoft.Data.Sqlite.SqliteOpenMode.ReadOnly }.ToString());
            try
            {
                _db.Open(); using var c = _db.CreateCommand(); c.CommandText = "SELECT COUNT(*), MIN(zoom_level), MAX(zoom_level) FROM tiles";
                using var r = c.ExecuteReader(); r.Read(); _dbCount = r.GetInt32(0); if (_dbCount > 0) { _dbMin = r.GetInt32(1); _dbMax = r.GetInt32(2); }
            }
            catch (Microsoft.Data.Sqlite.SqliteException) { Dispose(); throw new InvalidDataException("Diese Datei ist kein gültiges MBTiles-Kartenpaket."); }
            if (_dbCount == 0) { Dispose(); throw new InvalidDataException("Das MBTiles-Paket enthält keine Kacheln."); }
            return;
        }
        else if (File.Exists(path) && path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            _zip = ZipFile.OpenRead(path);
            foreach (var e in _zip.Entries) Add(e.FullName.Replace('\\', '/'));
        }
        else throw new ArgumentException("Bitte ein MBTiles-Paket, einen Ordner oder eine ZIP-Datei mit Kacheln (z/x/y.png) wählen.");
        if (_index.Count == 0) { Dispose(); throw new InvalidDataException("Darin sind keine Kacheln im Schema z/x/y.png."); }
    }

    void Add(string rel) { var m = TilePath().Match(rel); if (m.Success && int.TryParse(m.Groups[1].Value, out var z) && z <= 22) _index.TryAdd((z, int.Parse(m.Groups[2].Value), int.Parse(m.Groups[3].Value)), rel); }

    public byte[]? Get(int z, int x, int y)
    {
        if (_db is not null)
        {
            lock (_db)
            {
                using var c = _db.CreateCommand(); c.CommandText = "SELECT tile_data FROM tiles WHERE zoom_level=$z AND tile_column=$x AND tile_row=$y";
                c.Parameters.AddWithValue("$z", z); c.Parameters.AddWithValue("$x", x); c.Parameters.AddWithValue("$y", (1 << z) - 1 - y); // MBTiles zählt die Zeilen von unten (TMS)
                return c.ExecuteScalar() as byte[];
            }
        }
        if (!_index.TryGetValue((z, x, y), out var rel)) return null;
        if (_dir is not null) return File.ReadAllBytes(Path.Combine(_dir, rel));
        lock (_zip!) { using var s = _zip.GetEntry(rel)!.Open(); using var ms = new MemoryStream(); s.CopyTo(ms); return ms.ToArray(); }
    }

    /// <summary>Abgedecktes Gebiet (Breite/Länge) der höchsten Zoomstufe – zum Hinspringen.</summary>
    public (double South, double West, double North, double East) Bounds()
    {
        var z = MaxZoom; double n = 1 << z;
        var ks = _index.Keys.Where(k => k.Z == z).ToList();
        if (_db is not null)
        {
            lock (_db) { using var c = _db.CreateCommand(); c.CommandText = "SELECT MIN(tile_column), MAX(tile_column), MIN(tile_row), MAX(tile_row) FROM tiles WHERE zoom_level=$z"; c.Parameters.AddWithValue("$z", z);
                using var r = c.ExecuteReader(); r.Read(); var top = (int)n - 1 - r.GetInt32(3); var bottom = (int)n - 1 - r.GetInt32(2); ks = [(z, r.GetInt32(0), top), (z, r.GetInt32(1), bottom)]; }
        }
        double Lon(double x) => x / n * 360 - 180; double Lat(double y) => Math.Atan(Math.Sinh(Math.PI * (1 - 2 * y / n))) * 180 / Math.PI;
        return (Lat(ks.Max(k => k.Y) + 1), Lon(ks.Min(k => k.X)), Lat(ks.Min(k => k.Y)), Lon(ks.Max(k => k.X) + 1));
    }

    public void Dispose() { _zip?.Dispose(); _db?.Dispose(); }
}
