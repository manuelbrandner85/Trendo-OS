using System.Globalization;
using System.Text.Json;

namespace Trendo.Core.Sys;

/// <summary>Ein Foto mit Aufnahmezeit (EXIF, sonst Änderungsdatum).</summary>
public sealed record Photo(string Path, DateTime Taken, long Bytes, bool FromExif);

/// <summary>Fotoalbum (Masterprompt 131): Fotos aus Ordnern einlesen, nach Monaten gruppieren, Alben und Favoriten, Doppelte finden. Nur lesend, außer den Album-Listen.</summary>
public sealed class PhotoAlbum(string dataRoot)
{
    public const string Favorites = "Favoriten";
    static readonly CultureInfo De = CultureInfo.GetCultureInfo("de-DE");
    string F => System.IO.Path.Combine(dataRoot, "fotos", "alben.json");

    /// <summary>Fotos aus den Ordnern (rekursiv, ohne versteckte), neueste zuerst; höchstens <paramref name="max"/>.</summary>
    public static List<Photo> Scan(IEnumerable<string> folders, int max = 5000)
    {
        var res = new List<Photo>(); var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var opt = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, MaxRecursionDepth = 8, AttributesToSkip = FileAttributes.Hidden | FileAttributes.System };
        foreach (var d in folders.Where(Directory.Exists))
            foreach (var f in Directory.EnumerateFiles(d, "*", opt))
            {
                if (!MediaLibrary.Pictures.Contains(System.IO.Path.GetExtension(f).ToLowerInvariant()) || !seen.Add(System.IO.Path.GetFullPath(f))) continue;
                var fi = new FileInfo(f); var exif = fi.Extension.ToLowerInvariant() is ".jpg" or ".jpeg" ? PhotoSorter.ExifDate(f) : null;
                res.Add(new(fi.FullName, exif ?? fi.LastWriteTime, fi.Length, exif is not null));
                if (res.Count >= max) return Sort(res);
            }
        return Sort(res);
    }
    static List<Photo> Sort(List<Photo> l) => l.OrderByDescending(p => p.Taken).ThenBy(p => p.Path, StringComparer.OrdinalIgnoreCase).ToList();

    /// <summary>Gruppen nach Monat („November 2026“), neueste zuerst.</summary>
    public static List<(string Title, List<Photo> Photos)> ByMonth(IEnumerable<Photo> photos) =>
        photos.GroupBy(p => new DateTime(p.Taken.Year, p.Taken.Month, 1)).OrderByDescending(g => g.Key).Select(g => (g.Key.ToString("MMMM yyyy", De), g.ToList())).ToList();

    /// <summary>Gleiche Fotos (gleiche Größe, dann gleicher SHA-256), je Gruppe das älteste zuerst (das bleibt).</summary>
    public static List<List<Photo>> Duplicates(IEnumerable<Photo> photos) =>
        photos.GroupBy(p => p.Bytes).Where(g => g.Count() > 1)
            .SelectMany(g => g.GroupBy(p => { try { return J.Sha(p.Path); } catch (IOException) { return Guid.NewGuid().ToString(); } }))
            .Where(g => g.Count() > 1).Select(g => g.OrderBy(p => File.GetCreationTime(p.Path)).ThenBy(p => p.Path.Length).ToList()).ToList();

    // ---------- Alben (Name → Pfade) ----------
    Dictionary<string, List<string>> Load() { try { return File.Exists(F) ? JsonSerializer.Deserialize<Dictionary<string, List<string>>>(File.ReadAllText(F)) ?? [] : []; } catch (JsonException) { return []; } }
    void Save(Dictionary<string, List<string>> d) { Directory.CreateDirectory(System.IO.Path.GetDirectoryName(F)!); File.WriteAllText(F, JsonSerializer.Serialize(d)); }

    /// <summary>Alben mit Anzahl; „Favoriten“ immer zuerst.</summary>
    public List<(string Name, int Count)> Albums() { var d = Load(); d.TryAdd(Favorites, []); return d.OrderBy(p => p.Key == Favorites ? 0 : 1).ThenBy(p => p.Key, StringComparer.CurrentCultureIgnoreCase).Select(p => (p.Key, p.Value.Count)).ToList(); }
    public IReadOnlyList<string> In(string album) => Load().GetValueOrDefault(album) ?? [];
    public bool IsFavorite(string path) => In(Favorites).Contains(path, StringComparer.OrdinalIgnoreCase);

    public void Create(string name)
    {
        name = name.Trim(); if (name.Length == 0) throw new ArgumentException("Bitte einen Namen für das Album.");
        var d = Load(); if (d.Keys.Any(k => k.Equals(name, StringComparison.CurrentCultureIgnoreCase))) throw new ArgumentException($"Das Album „{name}“ gibt es schon.");
        d[name] = []; Save(d);
    }
    /// <summary>Fotos aufnehmen (doppelte werden übergangen); legt das Album bei Bedarf an. Gibt die Zahl neu aufgenommener zurück.</summary>
    public int Add(string album, IEnumerable<string> paths)
    {
        var d = Load(); var l = d.TryGetValue(album, out var x) ? x : d[album] = []; var n = 0;
        foreach (var p in paths) if (!l.Contains(p, StringComparer.OrdinalIgnoreCase)) { l.Add(p); n++; }
        Save(d); return n;
    }
    public void Remove(string album, IEnumerable<string> paths) { var d = Load(); if (d.TryGetValue(album, out var l)) { l.RemoveAll(p => paths.Contains(p, StringComparer.OrdinalIgnoreCase)); Save(d); } }
    public bool ToggleFavorite(string path) { if (IsFavorite(path)) { Remove(Favorites, [path]); return false; } Add(Favorites, [path]); return true; }
    public void Delete(string album) { if (album == Favorites) throw new ArgumentException("Die Favoriten bleiben."); var d = Load(); if (d.Remove(album)) Save(d); }
    /// <summary>Gelöschte oder verschobene Dateien aus allen Alben entfernen.</summary>
    public int Forget(IEnumerable<string> paths) { var d = Load(); var n = d.Values.Sum(l => l.RemoveAll(p => paths.Contains(p, StringComparer.OrdinalIgnoreCase))); if (n > 0) Save(d); return n; }
}
