using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Trendo.Core.Media;

/// <summary>Ein Titel der Bibliothek (Angaben aus den Tags, sonst aus Ordner- und Dateinamen).</summary>
public sealed record Track(string Path, string Title, string Artist, string Album, int No, string Year, bool Video, long Bytes, DateTime Modified);

/// <summary>Musik & Videos (Masterprompt 131): Bibliothek mit Tags (ID3v1/v2.3/v2.4, FLAC/Vorbis), Cover, Alben, Künstler; Wiedergabelisten; Warteschlange.
/// Liest nur – Dateien werden nie verändert.</summary>
public static partial class MusicLibrary
{
    public const string Unknown = "Unbekannt";
    static readonly string[] Audio = [".mp3", ".m4a", ".flac", ".wav", ".ogg", ".opus", ".aac", ".wma"];
    static readonly string[] Videos = [".mp4", ".mkv", ".mov", ".avi", ".webm", ".m4v"];

    public static List<Track> Scan(IEnumerable<string> folders, int max = 20000)
    {
        var res = new List<Track>(); var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var opt = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, MaxRecursionDepth = 8, AttributesToSkip = FileAttributes.Hidden | FileAttributes.System };
        foreach (var d in folders.Where(Directory.Exists))
            foreach (var f in Directory.EnumerateFiles(d, "*", opt))
            {
                var ext = System.IO.Path.GetExtension(f).ToLowerInvariant(); var video = Videos.Contains(ext);
                if (!video && !Audio.Contains(ext) || !seen.Add(System.IO.Path.GetFullPath(f))) continue;
                res.Add(Read(f, video)); if (res.Count >= max) return res;
            }
        return res.OrderBy(t => t.Artist, StringComparer.CurrentCultureIgnoreCase).ThenBy(t => t.Album, StringComparer.CurrentCultureIgnoreCase).ThenBy(t => t.No).ThenBy(t => t.Title, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    /// <summary>Angaben eines Titels: Tags zuerst, dann „Künstler/Album/01 Titel“ bzw. „Künstler - Titel“ aus dem Pfad.</summary>
    public static Track Read(string file, bool video = false)
    {
        var fi = new FileInfo(file); var tags = video ? [] : ReadTags(file);
        var name = System.IO.Path.GetFileNameWithoutExtension(file); int no = 0; string? artistFromName = null;
        var m = NumRx().Match(name); if (m.Success) { no = int.Parse(m.Groups[1].Value); name = m.Groups[2].Value; }
        var dash = name.IndexOf(" - ", StringComparison.Ordinal); if (dash > 0) { artistFromName = name[..dash].Trim(); name = name[(dash + 3)..].Trim(); }
        var dir = fi.Directory; var parent = dir?.Parent;
        string T(string k) => tags.TryGetValue(k, out var v) && v.Trim().Length > 0 ? v.Trim() : "";
        var trackNo = int.TryParse(T("track").Split('/')[0], out var tn) ? tn : no;
        var album = T("album") is { Length: > 0 } a ? a : video ? "Videos" : dir?.Name ?? Unknown;
        var artist = T("artist") is { Length: > 0 } ar ? ar : artistFromName ?? (parent is not null && !IsRootLike(parent.Name) ? parent.Name : Unknown);
        var year = T("year"); if (year.Length > 4) year = year[..4];
        return new(fi.FullName, T("title") is { Length: > 0 } t ? t : name, artist, album, trackNo, year, video, fi.Length, fi.LastWriteTime);
    }
    static bool IsRootLike(string n) => n.Equals("Music", StringComparison.OrdinalIgnoreCase) || n.Equals("Musik", StringComparison.OrdinalIgnoreCase) || n.Equals("Videos", StringComparison.OrdinalIgnoreCase) || n.Length <= 3;
    [GeneratedRegex(@"^(\d{1,3})[\s._-]+(.+)$")] private static partial Regex NumRx();

    /// <summary>Alben (Künstler + Album), Titel in Reihenfolge.</summary>
    public static List<(string Artist, string Album, List<Track> Tracks)> Albums(IEnumerable<Track> tracks) =>
        tracks.Where(t => !t.Video).GroupBy(t => (t.Album, Artist: AlbumArtist(t)))
            .Select(g => (g.Key.Artist, g.Key.Album, g.OrderBy(t => t.No).ThenBy(t => t.Title, StringComparer.CurrentCultureIgnoreCase).ToList()))
            .OrderBy(a => a.Artist, StringComparer.CurrentCultureIgnoreCase).ThenBy(a => a.Album, StringComparer.CurrentCultureIgnoreCase).ToList();
    static string AlbumArtist(Track t) => t.Artist;

    public static List<(string Artist, int Albums, int Tracks)> Artists(IEnumerable<Track> tracks) =>
        tracks.Where(t => !t.Video).GroupBy(t => t.Artist).Select(g => (g.Key, g.Select(t => t.Album).Distinct().Count(), g.Count())).OrderBy(a => a.Key, StringComparer.CurrentCultureIgnoreCase).ToList();

    /// <summary>Suche in Titel, Künstler und Album.</summary>
    public static List<Track> Search(IEnumerable<Track> tracks, string q) => q.Trim().Length == 0 ? tracks.ToList()
        : tracks.Where(t => t.Title.Contains(q, StringComparison.CurrentCultureIgnoreCase) || t.Artist.Contains(q, StringComparison.CurrentCultureIgnoreCase) || t.Album.Contains(q, StringComparison.CurrentCultureIgnoreCase)).ToList();

    public static string Time(double seconds) { if (double.IsNaN(seconds) || seconds < 0) return "–"; var t = TimeSpan.FromSeconds(seconds); return t.TotalHours >= 1 ? t.ToString(@"h\:mm\:ss") : t.ToString(@"m\:ss"); }

    // ---------- Tags ----------
    /// <summary>Tags lesen: title, artist, album, track, year (leer, wenn keine da).</summary>
    public static Dictionary<string, string> ReadTags(string file)
    {
        var res = new Dictionary<string, string>();
        try
        {
            using var fs = File.OpenRead(file);
            var head = new byte[10]; if (fs.Read(head, 0, 10) < 10) return res;
            if (head[0] == 'I' && head[1] == 'D' && head[2] == '3') Id3v2(fs, head, res, null);
            else if (head[0] == 'f' && head[1] == 'L' && head[2] == 'a' && head[3] == 'C') { fs.Position = 4; Flac(fs, res, null); }
            if (res.Count == 0 && fs.Length > 128) // ID3v1 am Ende
            {
                fs.Position = fs.Length - 128; var b = new byte[128]; fs.ReadExactly(b);
                if (b[0] == 'T' && b[1] == 'A' && b[2] == 'G')
                {
                    string S(int o, int n) => Encoding.Latin1.GetString(b, o, n).TrimEnd('\0', ' ');
                    res["title"] = S(3, 30); res["artist"] = S(33, 30); res["album"] = S(63, 30); res["year"] = S(93, 4);
                    if (b[125] == 0 && b[126] != 0) res["track"] = b[126].ToString();
                }
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or EndOfStreamException) { }
        return res;
    }

    /// <summary>Eingebettetes Cover (ID3 APIC / FLAC PICTURE), sonst cover.jpg/folder.jpg im Ordner; null, wenn keins.</summary>
    public static byte[]? Cover(string file)
    {
        try
        {
            using var fs = File.OpenRead(file); var head = new byte[10];
            if (fs.Read(head, 0, 10) == 10)
            {
                var pic = new byte[1][];
                if (head[0] == 'I' && head[1] == 'D' && head[2] == '3') Id3v2(fs, head, [], pic);
                else if (head[0] == 'f' && head[1] == 'L' && head[2] == 'a' && head[3] == 'C') { fs.Position = 4; Flac(fs, [], pic); }
                if (pic[0] is { Length: > 0 } p) return p;
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or EndOfStreamException) { }
        var dir = System.IO.Path.GetDirectoryName(file)!;
        foreach (var n in new[] { "cover.jpg", "cover.png", "folder.jpg", "folder.png", "front.jpg", "album.jpg" })
        { var f = System.IO.Path.Combine(dir, n); if (File.Exists(f)) try { return File.ReadAllBytes(f); } catch (IOException) { } }
        return null;
    }

    static int Synch(byte[] b, int o) => (b[o] & 0x7F) << 21 | (b[o + 1] & 0x7F) << 14 | (b[o + 2] & 0x7F) << 7 | (b[o + 3] & 0x7F);
    static int Be(byte[] b, int o) => b[o] << 24 | b[o + 1] << 16 | b[o + 2] << 8 | b[o + 3];

    static void Id3v2(Stream fs, byte[] head, Dictionary<string, string> res, byte[][]? pic)
    {
        int ver = head[3]; if (ver is not (3 or 4)) return;
        var size = Synch(head, 6); var body = new byte[Math.Min(size, 32 * 1024 * 1024)]; fs.ReadExactly(body);
        var pos = 0;
        if ((head[5] & 0x40) != 0) pos += ver == 4 ? Synch(body, 0) : Be(body, 0) + 4; // erweiterter Kopf
        while (pos + 10 <= body.Length && body[pos] != 0)
        {
            var id = Encoding.ASCII.GetString(body, pos, 4); var len = ver == 4 ? Synch(body, pos + 4) : Be(body, pos + 4); pos += 10;
            if (len <= 0 || pos + len > body.Length) break;
            var key = id switch { "TIT2" => "title", "TPE1" => "artist", "TALB" => "album", "TRCK" => "track", "TYER" or "TDRC" => "year", _ => null };
            if (key is not null && !res.ContainsKey(key)) res[key] = Text(body, pos, len);
            else if (id == "APIC" && pic is not null && pic[0] is null)
            {
                var enc = body[pos]; var p = pos + 1;
                while (p < pos + len && body[p] != 0) p++; p++; // MIME
                p++; // Bildart
                if (enc is 1 or 2) { while (p + 1 < pos + len && !(body[p] == 0 && body[p + 1] == 0)) p += 2; p += 2; } else { while (p < pos + len && body[p] != 0) p++; p++; }
                if (p < pos + len) pic[0] = body[p..(pos + len)];
            }
            pos += len;
        }
    }

    static string Text(byte[] b, int o, int len)
    {
        if (len < 1) return "";
        var enc = b[o] switch { 1 => Encoding.Unicode, 2 => Encoding.BigEndianUnicode, 3 => Encoding.UTF8, _ => Encoding.Latin1 };
        var start = o + 1; var n = len - 1;
        if (b[o] == 1 && n >= 2) { if (b[start] == 0xFE && b[start + 1] == 0xFF) enc = Encoding.BigEndianUnicode; start += 2; n -= 2; }
        return enc.GetString(b, start, Math.Max(0, n)).TrimEnd('\0').Split('\0')[0].Trim();
    }

    static void Flac(Stream fs, Dictionary<string, string> res, byte[][]? pic)
    {
        var h = new byte[4];
        for (var guard = 0; guard < 64; guard++)
        {
            fs.ReadExactly(h); var last = (h[0] & 0x80) != 0; var type = h[0] & 0x7F; var len = h[1] << 16 | h[2] << 8 | h[3];
            if (type == 4 || type == 6 && pic is not null)
            {
                var b = new byte[len]; fs.ReadExactly(b);
                if (type == 4)
                {
                    var p = 4 + BitConverter.ToInt32(b, 0); var count = BitConverter.ToInt32(b, p); p += 4;
                    for (var i = 0; i < count && p + 4 <= b.Length; i++)
                    {
                        var l = BitConverter.ToInt32(b, p); p += 4; var kv = Encoding.UTF8.GetString(b, p, l); p += l;
                        var eq = kv.IndexOf('='); if (eq <= 0) continue;
                        var key = kv[..eq].ToUpperInvariant() switch { "TITLE" => "title", "ARTIST" => "artist", "ALBUM" => "album", "TRACKNUMBER" => "track", "DATE" => "year", _ => null };
                        if (key is not null) res.TryAdd(key, kv[(eq + 1)..]);
                    }
                }
                else if (pic![0] is null)
                {
                    var p = 4; var ml = Be(b, p); p += 4 + ml; var dl = Be(b, p); p += 4 + dl + 16; var n = Be(b, p); p += 4;
                    if (p + n <= b.Length) pic[0] = b[p..(p + n)];
                }
            }
            else fs.Seek(len, SeekOrigin.Current);
            if (last) break;
        }
    }
}

/// <summary>Wiedergabelisten (Name → Pfade), gespeichert unter musik/listen.json.</summary>
public sealed class Playlists(string dataRoot)
{
    string F => System.IO.Path.Combine(dataRoot, "musik", "listen.json");
    Dictionary<string, List<string>> Load() { try { return File.Exists(F) ? JsonSerializer.Deserialize<Dictionary<string, List<string>>>(File.ReadAllText(F)) ?? [] : []; } catch (JsonException) { return []; } }
    void Save(Dictionary<string, List<string>> d) { Directory.CreateDirectory(System.IO.Path.GetDirectoryName(F)!); File.WriteAllText(F, JsonSerializer.Serialize(d)); }
    public List<(string Name, int Count)> All() => Load().OrderBy(p => p.Key, StringComparer.CurrentCultureIgnoreCase).Select(p => (p.Key, p.Value.Count)).ToList();
    public IReadOnlyList<string> Get(string name) => Load().GetValueOrDefault(name) ?? [];
    public void Create(string name) { name = name.Trim(); if (name.Length == 0) throw new ArgumentException("Bitte einen Namen für die Liste."); var d = Load(); if (d.Keys.Any(k => k.Equals(name, StringComparison.CurrentCultureIgnoreCase))) throw new ArgumentException($"Die Liste „{name}“ gibt es schon."); d[name] = []; Save(d); }
    public int Add(string name, IEnumerable<string> paths) { var d = Load(); var l = d.TryGetValue(name, out var x) ? x : d[name] = []; var n = 0; foreach (var p in paths) { l.Add(p); n++; } Save(d); return n; }
    public void RemoveAt(string name, int index) { var d = Load(); if (d.TryGetValue(name, out var l) && index >= 0 && index < l.Count) { l.RemoveAt(index); Save(d); } }
    public void Delete(string name) { var d = Load(); if (d.Remove(name)) Save(d); }
}

/// <summary>Warteschlange: Reihenfolge, aktueller Titel, Zufall (ohne Wiederholung, aktueller bleibt vorn), Wiederholen (aus/alle/eins).</summary>
public sealed class PlayQueue
{
    readonly List<string> _items = []; List<string>? _ordered;
    public IReadOnlyList<string> Items => _items;
    public int Index { get; private set; } = -1;
    public bool Shuffle { get; private set; }
    public string Repeat { get; set; } = "aus"; // aus | alle | eins
    public string? Current => Index >= 0 && Index < _items.Count ? _items[Index] : null;

    /// <summary>Liste abspielen, beginnend bei <paramref name="start"/>.</summary>
    public void Play(IEnumerable<string> list, int start = 0) { _items.Clear(); _items.AddRange(list); _ordered = null; Index = _items.Count == 0 ? -1 : Math.Clamp(start, 0, _items.Count - 1); if (Shuffle) Mix(); }
    public void Enqueue(string path) { _items.Add(path); _ordered?.Add(path); if (Index < 0) Index = 0; }
    public void PlayNext(string path) { if (Index < 0) { Enqueue(path); return; } _items.Insert(Index + 1, path); _ordered?.Add(path); }
    public void RemoveAt(int i) { if (i < 0 || i >= _items.Count) return; _items.RemoveAt(i); if (i < Index) Index--; else if (i == Index && Index >= _items.Count) Index = _items.Count - 1; }
    public void Move(int from, int to)
    {
        if (from < 0 || from >= _items.Count || to < 0 || to >= _items.Count || from == to) return;
        var x = _items[from]; _items.RemoveAt(from); _items.Insert(to, x);
        if (Index == from) Index = to; else if (from < Index && to >= Index) Index--; else if (from > Index && to <= Index) Index++;
    }
    public void Jump(int i) { if (i >= 0 && i < _items.Count) Index = i; }

    /// <summary>Nächster Titel (bei „eins“ derselbe, bei „alle“ wieder von vorn); null am Ende.</summary>
    public string? Next(bool auto = false)
    {
        if (_items.Count == 0) return null;
        if (auto && Repeat == "eins") return Current;
        if (Index + 1 < _items.Count) { Index++; return Current; }
        if (Repeat == "alle") { Index = 0; return Current; }
        return null;
    }
    public string? Prev() { if (_items.Count == 0) return null; Index = Math.Max(0, Index - 1); return Current; }

    public void SetShuffle(bool on, int seed = 0)
    {
        if (on == Shuffle) return; Shuffle = on;
        if (on) Mix(seed);
        else if (_ordered is not null) { var cur = Current; _items.Clear(); _items.AddRange(_ordered); _ordered = null; Index = cur is null ? -1 : _items.IndexOf(cur); }
    }
    void Mix(int seed = 0)
    {
        _ordered = [.. _items]; var cur = Current; var rest = _items.Where((_, i) => i != Index).ToList();
        var rnd = seed == 0 ? new Random() : new Random(seed);
        for (var i = rest.Count - 1; i > 0; i--) { var j = rnd.Next(i + 1); (rest[i], rest[j]) = (rest[j], rest[i]); }
        _items.Clear(); if (cur is not null) _items.Add(cur); _items.AddRange(rest); Index = cur is null ? -1 : 0;
    }
}
