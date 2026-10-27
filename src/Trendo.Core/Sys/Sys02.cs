using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Trendo.Core.Sys;

/// <summary>SYS-02 (Katalog freigegeben 21.10.2026): Alltagswerkzeuge. Alles lokal; Netz nur in der App.</summary>
static class J
{
    public static readonly JsonSerializerOptions O = new() { WriteIndented = true };
    public static List<T> Load<T>(string f) { try { return File.Exists(f) ? JsonSerializer.Deserialize<List<T>>(File.ReadAllText(f)) ?? [] : []; } catch (JsonException) { return []; } }
    public static void Save<T>(string f, List<T> l) { Directory.CreateDirectory(Path.GetDirectoryName(f)!); File.WriteAllText(f + ".tmp", JsonSerializer.Serialize(l, O)); File.Move(f + ".tmp", f, true); }
    public static string Sha(string file) { using var s = File.OpenRead(file); return Convert.ToHexString(SHA256.HashData(s)).ToLowerInvariant(); }
}

// P05 Zwischenablage-Verlauf
public sealed partial class ClipHistory(string dataRoot)
{
    public const int Max = 50;
    string F => Path.Combine(dataRoot, "werkzeuge", "zwischenablage.json");
    [GeneratedRegex(@"^(sk-|ghp_|github_pat_|xox[bp]-|AKIA)|passwor[dt]\s*[:=]|-----BEGIN [A-Z ]*PRIVATE KEY", RegexOptions.IgnoreCase)] private static partial Regex SecretRx();
    /// <summary>Sieht aus wie Passwort oder Schlüssel: bekannte Präfixe oder ein einzelnes Wort ≥ 12 Zeichen mit Klein-, Großbuchstaben, Ziffern und Sonderzeichen.</summary>
    public static bool LooksSecret(string t)
    {
        t = t.Trim(); if (SecretRx().IsMatch(t)) return true;
        if (t.Length < 12 || t.Length > 128 || t.Any(char.IsWhiteSpace)) return false;
        return t.Any(char.IsLower) && t.Any(char.IsUpper) && t.Any(char.IsDigit) && t.Any(c => !char.IsLetterOrDigit(c));
    }
    public IReadOnlyList<string> All() => J.Load<string>(F);
    public bool Add(string text, bool fromPasswordField = false)
    {
        if (fromPasswordField || string.IsNullOrWhiteSpace(text) || LooksSecret(text)) return false;
        var l = J.Load<string>(F); l.Remove(text); l.Insert(0, text); if (l.Count > Max) l.RemoveRange(Max, l.Count - Max); J.Save(F, l); return true;
    }
    public void Delete(int i) { var l = J.Load<string>(F); if (i >= 0 && i < l.Count) { l.RemoveAt(i); J.Save(F, l); } }
    public void Clear() => J.Save(F, new List<string>());
}

// P07 Bildschirmaufnahme, P23 Audio umwandeln (ffmpeg)
public static class FfmpegJobs
{
    public static List<string> RecordArgs(bool windows, string outFile, int seconds, string? audioDevice = null)
    {
        if (seconds is < 1 or > 3600) throw new ArgumentException("Dauer zwischen 1 Sekunde und 1 Stunde.");
        var a = new List<string> { "-y" };
        if (windows) a.AddRange(["-f", "gdigrab", "-framerate", "30", "-i", "desktop"]); else a.AddRange(["-f", "x11grab", "-framerate", "30", "-i", Environment.GetEnvironmentVariable("DISPLAY") ?? ":0"]);
        if (!string.IsNullOrWhiteSpace(audioDevice)) a.AddRange(windows ? ["-f", "dshow", "-i", "audio=" + audioDevice] : ["-f", "pulse", "-i", audioDevice]);
        a.AddRange(["-t", seconds.ToString(CultureInfo.InvariantCulture), "-c:v", "libx264", "-preset", "veryfast", "-pix_fmt", "yuv420p"]);
        if (!string.IsNullOrWhiteSpace(audioDevice)) a.AddRange(["-c:a", "aac"]);
        a.Add(outFile); return a;
    }
    public static readonly string[] AudioFormats = ["mp3", "ogg", "wav"];
    public static List<string> AudioArgs(string input, string outFile)
    {
        var f = Path.GetExtension(outFile).TrimStart('.').ToLowerInvariant();
        if (!AudioFormats.Contains(f)) throw new ArgumentException("Nur MP3, OGG oder WAV.");
        var a = new List<string> { "-y", "-i", input, "-vn" };
        a.AddRange(f switch { "mp3" => ["-c:a", "libmp3lame", "-q:a", "2"], "ogg" => ["-c:a", "libvorbis", "-q:a", "5"], _ => ["-c:a", "pcm_s16le"] });
        a.Add(outFile); return a;
    }
    /// <summary>Wandelt um und liefert die Prüfsumme der neuen Datei (für das Protokoll). Original bleibt.</summary>
    public static (bool Ok, string Message, string? Sha) ConvertAudio(string ffmpeg, string input, string format, AuditLog? audit = null)
    {
        var outFile = Path.Combine(Path.GetDirectoryName(input)!, Path.GetFileNameWithoutExtension(input) + "-umgewandelt." + format);
        if (Path.GetFullPath(outFile) == Path.GetFullPath(input)) return (false, "Ziel wäre das Original.", null);
        var r = Cli.Run(ffmpeg, AudioArgs(input, outFile), timeoutMs: 600000);
        if (!r.Ok || !File.Exists(outFile)) return (false, "Umwandeln hat nicht geklappt.", null);
        var sha = J.Sha(outFile); audit?.Append("app", "audio.convert", $"{Path.GetFileName(outFile)} sha256={sha}");
        return (true, Path.GetFileName(outFile), sha);
    }
}

// P08 Medienbibliothek, P09 Wiedergabe
public sealed record MediaItem(string Path, string Kind, long Bytes, DateTime Changed);
public static class MediaLibrary
{
    public static readonly string[] Music = [".mp3", ".m4a", ".flac", ".wav", ".ogg", ".opus"];
    public static readonly string[] Video = [".mp4", ".mkv", ".mov", ".avi", ".webm"];
    public static readonly string[] Pictures = [".jpg", ".jpeg", ".png", ".gif", ".webp", ".heic", ".bmp"];
    public static string? KindOf(string f) { var e = Path.GetExtension(f).ToLowerInvariant(); return Music.Contains(e) ? "Musik" : Video.Contains(e) ? "Video" : Pictures.Contains(e) ? "Bild" : null; }
    public static List<MediaItem> Index(IEnumerable<string> folders, int max = 2000)
    {
        var res = new List<MediaItem>();
        var opt = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, MaxRecursionDepth = 6, AttributesToSkip = FileAttributes.Hidden | FileAttributes.System };
        foreach (var d in folders.Where(Directory.Exists).Distinct())
            foreach (var f in Directory.EnumerateFiles(d, "*", opt))
            {
                var k = KindOf(f); if (k is null) continue;
                var fi = new FileInfo(f); res.Add(new(f, k, fi.Length, fi.LastWriteTime));
                if (res.Count >= max) return res;
            }
        return res;
    }
    /// <summary>Spieler wählen: mpv, sonst VLC, sonst Standardprogramm (null).</summary>
    public static (string Exe, string[] Args)? Player(string file, string? mpv, string? vlc) =>
        mpv is not null ? (mpv, ["--force-window=yes", file]) : vlc is not null ? (vlc, ["--play-and-exit", file]) : null;
}

// P11 Batch-Umbenennen (mit Rückgängig), P22 Fotos ordnen
public sealed record Move(string From, string To);
public static class Renamer
{
    /// <summary>Muster: {name} {n} {n:000} {ext} {datum}. Optional Suchen/Ersetzen im Namen.</summary>
    public static List<Move> Plan(IEnumerable<string> files, string pattern, string find = "", string replace = "", int start = 1)
    {
        var res = new List<Move>(); var n = start;
        foreach (var f in files.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
        {
            var name = Path.GetFileNameWithoutExtension(f); if (find.Length > 0) name = name.Replace(find, replace, StringComparison.OrdinalIgnoreCase);
            var s = Regex.Replace(pattern, @"\{n(?::(0+))?\}", m => n.ToString(new string('0', Math.Max(1, m.Groups[1].Length)), CultureInfo.InvariantCulture));
            s = s.Replace("{name}", name).Replace("{datum}", File.GetLastWriteTime(f).ToString("yyyy-MM-dd")).Replace("{ext}", Path.GetExtension(f));
            if (!pattern.Contains("{ext}")) s += Path.GetExtension(f);
            if (s.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || s.Trim().Length == 0) throw new ArgumentException("Ungültiger Dateiname: " + s);
            res.Add(new(f, Path.Combine(Path.GetDirectoryName(f)!, s))); n++;
        }
        var dup = res.GroupBy(m => m.To, StringComparer.OrdinalIgnoreCase).FirstOrDefault(g => g.Count() > 1);
        if (dup is not null) throw new ArgumentException("Zwei Dateien bekämen denselben Namen: " + Path.GetFileName(dup.Key));
        return res;
    }
    /// <summary>Führt aus (über Zwischennamen, damit Tausch-Umbenennungen klappen) und schreibt eine Rückgängig-Datei.</summary>
    public static string Apply(List<Move> moves, string undoDir)
    {
        var targets = moves.Select(m => m.To).ToHashSet(StringComparer.OrdinalIgnoreCase); var sources = moves.Select(m => m.From).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var t in targets) if (File.Exists(t) && !sources.Contains(t)) throw new IOException("Gibt es schon: " + Path.GetFileName(t));
        var tmp = moves.Select(m => (m, T: m.From + ".trendo-" + Guid.NewGuid().ToString("N")[..8])).ToList();
        foreach (var (m, t) in tmp) { Directory.CreateDirectory(Path.GetDirectoryName(t)!); File.Move(m.From, t); }
        foreach (var (m, t) in tmp) { Directory.CreateDirectory(Path.GetDirectoryName(m.To)!); File.Move(t, m.To); }
        Directory.CreateDirectory(undoDir);
        var u = Path.Combine(undoDir, $"rueckgaengig-{DateTime.Now:yyyyMMdd-HHmmssfff}.json"); J.Save(u, moves); return u;
    }
    public static void Undo(string undoFile)
    {
        var back = J.Load<Move>(undoFile).Select(m => new Move(m.To, m.From)).ToList();
        foreach (var m in back) if (!File.Exists(m.From)) throw new IOException("Datei fehlt inzwischen: " + Path.GetFileName(m.From));
        var tmp = back.Select(m => (m, T: m.From + ".trendo-" + Guid.NewGuid().ToString("N")[..8])).ToList();
        foreach (var (m, t) in tmp) File.Move(m.From, t);
        foreach (var (m, t) in tmp) { Directory.CreateDirectory(Path.GetDirectoryName(m.To)!); File.Move(t, m.To); }
        File.Delete(undoFile);
    }
}

public static class PhotoSorter
{
    /// <summary>Aufnahmedatum aus EXIF (DateTimeOriginal/DateTime) einer JPEG-Datei; sonst null.</summary>
    public static DateTime? ExifDate(string file)
    {
        try
        {
            using var fs = File.OpenRead(file); using var br = new BinaryReader(fs);
            if (br.ReadByte() != 0xFF || br.ReadByte() != 0xD8) return null;
            while (fs.Position < fs.Length - 4)
            {
                if (br.ReadByte() != 0xFF) return null; var marker = br.ReadByte();
                int len = br.ReadByte() << 8 | br.ReadByte();
                if (marker == 0xE1)
                {
                    var data = br.ReadBytes(len - 2);
                    var s = Encoding.ASCII.GetString(data);
                    var m = Regex.Match(s, @"(\d{4}):(\d{2}):(\d{2}) (\d{2}):(\d{2}):(\d{2})");
                    if (m.Success && DateTime.TryParseExact(m.Value, "yyyy:MM:dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)) return d;
                    return null;
                }
                if (marker == 0xDA) return null;
                fs.Seek(len - 2, SeekOrigin.Current);
            }
        }
        catch (IOException) { }
        return null;
    }
    /// <summary>Simulation: Ziel „JJJJ/JJJJ-MM/Datei“. Ohne EXIF zählt das Änderungsdatum.</summary>
    public static List<Move> Plan(string folder)
    {
        var res = new List<Move>();
        foreach (var f in Directory.GetFiles(folder).Where(f => MediaLibrary.Pictures.Contains(Path.GetExtension(f).ToLowerInvariant())).OrderBy(f => f))
        {
            var d = ExifDate(f) ?? File.GetLastWriteTime(f);
            var to = Path.Combine(folder, d.ToString("yyyy"), d.ToString("yyyy-MM"), Path.GetFileName(f));
            for (var k = 2; File.Exists(to) || res.Any(r => r.To == to); k++) to = Path.Combine(folder, d.ToString("yyyy"), d.ToString("yyyy-MM"), $"{Path.GetFileNameWithoutExtension(f)}-{k}{Path.GetExtension(f)}");
            res.Add(new(f, to));
        }
        return res;
    }
}

// P12 Doppelte Dateien, P13 Speicherplatz
public static class FileAnalysis
{
    public static List<List<string>> Duplicates(string folder, long minBytes = 1)
    {
        var opt = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true };
        return Directory.EnumerateFiles(folder, "*", opt).Select(f => new FileInfo(f)).Where(f => f.Length >= minBytes)
            .GroupBy(f => f.Length).Where(g => g.Count() > 1)
            .SelectMany(g => g.GroupBy(f => J.Sha(f.FullName))).Where(g => g.Count() > 1)
            .Select(g => g.Select(f => f.FullName).OrderBy(x => x).ToList()).OrderByDescending(g => new FileInfo(g[0]).Length).ToList();
    }
    public sealed record Usage(string Path, long Bytes, bool IsFolder);
    public static long FolderSize(string d)
    {
        var opt = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = FileAttributes.ReparsePoint };
        long s = 0; foreach (var f in Directory.EnumerateFiles(d, "*", opt)) try { s += new FileInfo(f).Length; } catch (IOException) { } return s;
    }
    /// <summary>Größte Unterordner und Dateien direkt im Ordner, plus Gesamtsumme.</summary>
    public static (long Total, List<Usage> Top) Analyze(string folder, int top = 15)
    {
        var items = new List<Usage>();
        foreach (var d in Directory.GetDirectories(folder)) try { if (!new DirectoryInfo(d).Attributes.HasFlag(FileAttributes.ReparsePoint)) items.Add(new(d, FolderSize(d), true)); } catch (UnauthorizedAccessException) { }
        foreach (var f in Directory.GetFiles(folder)) items.Add(new(f, new FileInfo(f).Length, false));
        return (items.Sum(i => i.Bytes), items.OrderByDescending(i => i.Bytes).Take(top).ToList());
    }
}

// P14 Archive
public static class Archives
{
    public static void Pack(IEnumerable<string> paths, string zip)
    {
        if (File.Exists(zip)) File.Delete(zip);
        using var z = ZipFile.Open(zip, ZipArchiveMode.Create);
        foreach (var p in paths)
            if (Directory.Exists(p)) { var root = Path.GetDirectoryName(Path.GetFullPath(p).TrimEnd(Path.DirectorySeparatorChar))!; foreach (var f in Directory.EnumerateFiles(p, "*", SearchOption.AllDirectories)) z.CreateEntryFromFile(f, Path.GetRelativePath(root, f).Replace('\\', '/')); }
            else if (File.Exists(p)) z.CreateEntryFromFile(p, Path.GetFileName(p));
    }
    /// <summary>Entpackt; Einträge, die aus dem Zielordner ausbrechen (Zip-Slip), werden abgewiesen – dann wird gar nichts entpackt.</summary>
    public static int Unpack(string zip, string dest)
    {
        var full = Path.GetFullPath(dest) + Path.DirectorySeparatorChar;
        using var z = ZipFile.OpenRead(zip);
        foreach (var e in z.Entries) { var t = Path.GetFullPath(Path.Combine(dest, e.FullName)); if (!t.StartsWith(full, StringComparison.Ordinal) && t + Path.DirectorySeparatorChar != full) throw new IOException("Unsicherer Pfad im Archiv: " + e.FullName); }
        var n = 0;
        foreach (var e in z.Entries)
        {
            var t = Path.GetFullPath(Path.Combine(dest, e.FullName));
            if (e.FullName.EndsWith('/')) { Directory.CreateDirectory(t); continue; }
            Directory.CreateDirectory(Path.GetDirectoryName(t)!); e.ExtractToFile(t, true); n++;
        }
        return n;
    }
}

// P15 Erinnerungen + Stoppuhr, P19 Fokusmodus
public sealed record Erinnerung(string Id, DateTime At, string Text, bool Done);
public sealed class ErinnerungStore(string dataRoot)
{
    string F => Path.Combine(dataRoot, "werkzeuge", "erinnerungen.json");
    public List<Erinnerung> All() => J.Load<Erinnerung>(F).OrderBy(r => r.At).ToList();
    public Erinnerung Add(DateTime at, string text) { var l = J.Load<Erinnerung>(F); var r = new Erinnerung(Guid.NewGuid().ToString("N")[..8], at, text.Trim(), false); l.Add(r); J.Save(F, l); return r; }
    /// <summary>Fällige (±2 s Toleranz nach vorn) – werden als erledigt markiert.</summary>
    public List<Erinnerung> TakeDue(DateTime now)
    {
        var l = J.Load<Erinnerung>(F); var due = l.Where(r => !r.Done && r.At <= now.AddSeconds(2)).ToList();
        if (due.Count > 0) { J.Save(F, l.Select(r => due.Any(d => d.Id == r.Id) ? r with { Done = true } : r).ToList()); }
        return due;
    }
    public void Delete(string id) { var l = J.Load<Erinnerung>(F); l.RemoveAll(r => r.Id == id); J.Save(F, l); }
}

public sealed class FocusMode(string dataRoot)
{
    string F => Path.Combine(dataRoot, "werkzeuge", "fokus.txt");
    public void Start(TimeSpan d, DateTime now) { Directory.CreateDirectory(Path.GetDirectoryName(F)!); File.WriteAllText(F, now.Add(d).ToString("O")); }
    public void Stop() { if (File.Exists(F)) File.Delete(F); }
    /// <summary>Aktiv bis zum Ablauf; danach wird der Zustand automatisch zurückgesetzt.</summary>
    public DateTime? Until(DateTime now)
    {
        if (!File.Exists(F)) return null;
        if (DateTime.TryParse(File.ReadAllText(F), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var u) && u > now) return u;
        Stop(); return null;
    }
}

// P16 Kalender + ICS; O3/O5 (27.10.2026): Wiederholungen, ganztägige Termine, Erinnerungen, Aufgaben (VTODO)
public enum Repeat { Keine, Taeglich, Woechentlich, Monatlich, Jaehrlich }
public sealed record CalEvent(string Uid, DateTime Start, DateTime End, string Title, string Location = "", string Notes = "", bool AllDay = false, int RemindMin = 15, Repeat Repeat = Repeat.Keine);
/// <summary>O5/N19: Aufgabe mit Fälligkeit, Priorität (1 hoch, 2 normal, 3 niedrig) und Liste.</summary>
public sealed record TodoItem(string Uid, string Title, DateTime? Due = null, bool Done = false, int Priority = 2, string List = "Allgemein");

public static class Ics
{
    static string Esc(string s) => s.Replace("\\", "\\\\").Replace(";", "\\;").Replace(",", "\\,").Replace("\r\n", "\\n").Replace("\n", "\\n");
    static string Unesc(string s) => Regex.Replace(s, @"\\([\\;,nN])", m => m.Groups[1].Value is "n" or "N" ? "\n" : m.Groups[1].Value);
    static string Dt(DateTime d) => d.ToString("yyyyMMdd'T'HHmmss", CultureInfo.InvariantCulture);
    static string Day(DateTime d) => d.ToString("yyyyMMdd", CultureInfo.InvariantCulture);

    public static string Export(IEnumerable<CalEvent> events, IEnumerable<TodoItem>? todos = null)
    {
        var sb = new StringBuilder("BEGIN:VCALENDAR\r\nVERSION:2.0\r\nPRODID:-//Kalender//DE\r\n"); // ohne Programmname (Vorgabe 2026-10-27)
        foreach (var e in events)
        {
            sb.Append("BEGIN:VEVENT\r\nUID:").Append(e.Uid).Append("\r\nDTSTAMP:").Append(DateTime.UtcNow.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture)).Append("\r\n");
            if (e.AllDay) sb.Append("DTSTART;VALUE=DATE:").Append(Day(e.Start)).Append("\r\nDTEND;VALUE=DATE:").Append(Day(e.End)).Append("\r\n");
            else sb.Append("DTSTART:").Append(Dt(e.Start)).Append("\r\nDTEND:").Append(Dt(e.End)).Append("\r\n");
            sb.Append("SUMMARY:").Append(Esc(e.Title)).Append("\r\n");
            if (e.Location.Length > 0) sb.Append("LOCATION:").Append(Esc(e.Location)).Append("\r\n");
            if (e.Notes.Length > 0) sb.Append("DESCRIPTION:").Append(Esc(e.Notes)).Append("\r\n");
            if (e.Repeat != Repeat.Keine) sb.Append("RRULE:FREQ=").Append(e.Repeat switch { Repeat.Taeglich => "DAILY", Repeat.Woechentlich => "WEEKLY", Repeat.Monatlich => "MONTHLY", _ => "YEARLY" }).Append("\r\n");
            if (e.RemindMin >= 0) sb.Append("BEGIN:VALARM\r\nACTION:DISPLAY\r\nDESCRIPTION:").Append(Esc(e.Title)).Append("\r\nTRIGGER:-PT").Append(e.RemindMin).Append("M\r\nEND:VALARM\r\n");
            sb.Append("END:VEVENT\r\n");
        }
        foreach (var t in todos ?? [])
        {
            sb.Append("BEGIN:VTODO\r\nUID:").Append(t.Uid).Append("\r\nDTSTAMP:").Append(DateTime.UtcNow.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture)).Append("\r\nSUMMARY:").Append(Esc(t.Title)).Append("\r\n");
            sb.Append("PRIORITY:").Append(t.Priority switch { 1 => 1, 3 => 9, _ => 5 }).Append("\r\nSTATUS:").Append(t.Done ? "COMPLETED" : "NEEDS-ACTION").Append("\r\nCATEGORIES:").Append(Esc(t.List)).Append("\r\n");
            if (t.Due is { } d) sb.Append("DUE;VALUE=DATE:").Append(Day(d)).Append("\r\n");
            sb.Append("END:VTODO\r\n");
        }
        return sb.Append("END:VCALENDAR\r\n").ToString();
    }

    static DateTime ParseDt(string v)
    {
        var utc = v.EndsWith('Z'); v = v.TrimEnd('Z');
        var d = DateTime.ParseExact(v, v.Length == 8 ? "yyyyMMdd" : v.Length == 13 ? "yyyyMMdd'T'HHmm" : "yyyyMMdd'T'HHmmss", CultureInfo.InvariantCulture);
        return utc ? DateTime.SpecifyKind(d, DateTimeKind.Utc).ToLocalTime() : d;
    }

    static List<(string Kind, Dictionary<string, string> Props)> Blocks(string text)
    {
        var lines = Regex.Replace(text, @"\r?\n[ \t]", "").Split('\n').Select(l => l.TrimEnd('\r'));
        var res = new List<(string, Dictionary<string, string>)>(); Dictionary<string, string>? cur = null; string? kind = null; var alarm = false;
        foreach (var l in lines)
        {
            if (l is "BEGIN:VEVENT" or "BEGIN:VTODO") { cur = []; kind = l[6..]; continue; }
            if (l == "BEGIN:VALARM") { alarm = true; continue; }
            if (l == "END:VALARM") { alarm = false; continue; }
            if ((l == "END:VEVENT" || l == "END:VTODO") && cur is not null) { res.Add((kind!, cur)); cur = null; continue; }
            if (cur is null) continue;
            var i = l.IndexOf(':'); if (i < 0) continue;
            var key = l[..i].Split(';')[0].ToUpperInvariant();
            if (alarm) { if (key == "TRIGGER") cur["TRIGGER"] = l[(i + 1)..]; continue; }
            cur[key] = l[(i + 1)..];
        }
        return res;
    }

    public static List<CalEvent> Import(string text)
    {
        var res = new List<CalEvent>();
        foreach (var (kind, cur) in Blocks(text))
        {
            if (kind != "VEVENT" || !cur.TryGetValue("DTSTART", out var s)) continue;
            var allDay = s.Length == 8;
            var st = ParseDt(s); var en = cur.TryGetValue("DTEND", out var e) ? ParseDt(e) : allDay ? st.AddDays(1) : st.AddHours(1);
            var rr = cur.GetValueOrDefault("RRULE") ?? "";
            var rep = rr.Contains("FREQ=DAILY") ? Repeat.Taeglich : rr.Contains("FREQ=WEEKLY") ? Repeat.Woechentlich : rr.Contains("FREQ=MONTHLY") ? Repeat.Monatlich : rr.Contains("FREQ=YEARLY") ? Repeat.Jaehrlich : Repeat.Keine;
            var remind = cur.TryGetValue("TRIGGER", out var tr) && Regex.Match(tr, @"^-P(?:(\d+)D)?(?:T(?:(\d+)H)?(?:(\d+)M)?)?") is { Success: true } m && (m.Groups[1].Success || m.Groups[2].Success || m.Groups[3].Success)
                ? (m.Groups[1].Success ? int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture) * 1440 : 0) + (m.Groups[2].Success ? int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture) * 60 : 0) + (m.Groups[3].Success ? int.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture) : 0)
                : cur.ContainsKey("TRIGGER") ? 0 : 15;
            res.Add(new(cur.GetValueOrDefault("UID") ?? Guid.NewGuid().ToString("N"), st, en, Unesc(cur.GetValueOrDefault("SUMMARY") ?? ""), Unesc(cur.GetValueOrDefault("LOCATION") ?? ""), Unesc(cur.GetValueOrDefault("DESCRIPTION") ?? ""), allDay, remind, rep));
        }
        return res;
    }

    public static List<TodoItem> ImportTodos(string text) =>
        Blocks(text).Where(b => b.Kind == "VTODO").Select(b =>
        {
            var c = b.Props; var pr = int.TryParse(c.GetValueOrDefault("PRIORITY"), out var p) && p > 0 ? p <= 4 ? 1 : p >= 6 ? 3 : 2 : 2;
            return new TodoItem(c.GetValueOrDefault("UID") ?? Guid.NewGuid().ToString("N"), Unesc(c.GetValueOrDefault("SUMMARY") ?? ""), c.TryGetValue("DUE", out var d) ? ParseDt(d) : null, c.GetValueOrDefault("STATUS") == "COMPLETED", pr, Unesc(c.GetValueOrDefault("CATEGORIES") ?? "Allgemein"));
        }).ToList();
}

public sealed class Calendar(string dataRoot)
{
    string F => Path.Combine(dataRoot, "werkzeuge", "kalender.json");
    string T => Path.Combine(dataRoot, "werkzeuge", "aufgaben.json");
    public List<CalEvent> All() => J.Load<CalEvent>(F).OrderBy(e => e.Start).ToList();
    public void Add(CalEvent e) { var l = J.Load<CalEvent>(F); l.RemoveAll(x => x.Uid == e.Uid); l.Add(e); J.Save(F, l); }
    public void Delete(string uid) { var l = J.Load<CalEvent>(F); l.RemoveAll(x => x.Uid == uid); J.Save(F, l); }
    public int ImportFile(string ics) => ImportText(File.ReadAllText(ics)).Events;
    public void ExportFile(string ics) => File.WriteAllText(ics, Ics.Export(All(), Todos()));

    /// <summary>Termine und Aufgaben aus ICS übernehmen (gleiche UID wird ersetzt).</summary>
    public (int Events, int Todos) ImportText(string ics)
    {
        var ev = Ics.Import(ics); foreach (var e in ev) Add(e);
        var td = Ics.ImportTodos(ics); foreach (var t in td) AddTodo(t);
        return (ev.Count, td.Count);
    }

    // O5/N19 Aufgaben
    public List<TodoItem> Todos() => J.Load<TodoItem>(T).OrderBy(t => t.Done).ThenBy(t => t.Priority).ThenBy(t => t.Due ?? DateTime.MaxValue).ToList();
    public void AddTodo(TodoItem t) { var l = J.Load<TodoItem>(T); l.RemoveAll(x => x.Uid == t.Uid); l.Add(t); J.Save(T, l); }
    public void DeleteTodo(string uid) { var l = J.Load<TodoItem>(T); l.RemoveAll(x => x.Uid == uid); J.Save(T, l); }
    public void SetDone(string uid, bool done) { var l = J.Load<TodoItem>(T); var i = l.FindIndex(x => x.Uid == uid); if (i >= 0) { l[i] = l[i] with { Done = done }; J.Save(T, l); } }

    /// <summary>O3: alle Vorkommen im Zeitraum (Wiederholungen aufgelöst), nach Beginn sortiert.</summary>
    public List<(CalEvent Event, DateTime Start, DateTime End)> Occurrences(DateTime from, DateTime to)
    {
        var res = new List<(CalEvent, DateTime, DateTime)>();
        foreach (var e in All())
        {
            var len = e.End - e.Start; var start = e.Start;
            for (var k = 0; k < 5000 && start < to; k++)
            {
                if (start + len > from) res.Add((e, start, start + len));
                if (e.Repeat == Repeat.Keine) break;
                start = e.Repeat switch { Repeat.Taeglich => e.Start.AddDays(k + 1), Repeat.Woechentlich => e.Start.AddDays(7 * (k + 1)), Repeat.Monatlich => e.Start.AddMonths(k + 1), _ => e.Start.AddYears(k + 1) };
            }
        }
        return res.OrderBy(x => x.Item2).ToList();
    }

    /// <summary>O3: Erinnerungen, die zwischen <paramref name="since"/> und <paramref name="now"/> fällig wurden.</summary>
    public List<(CalEvent Event, DateTime Start)> DueReminders(DateTime since, DateTime now) =>
        Occurrences(since.AddDays(-1), now.AddDays(8)).Where(o => o.Event.RemindMin >= 0 && o.Start.AddMinutes(-o.Event.RemindMin) > since && o.Start.AddMinutes(-o.Event.RemindMin) <= now).Select(o => (o.Event, o.Start)).ToList();
}

// P17 Einheiten
public static class Units
{
    static readonly Dictionary<string, (string Group, double Factor)> U = new(StringComparer.OrdinalIgnoreCase)
    {
        ["mm"] = ("Länge", 0.001), ["cm"] = ("Länge", 0.01), ["m"] = ("Länge", 1), ["km"] = ("Länge", 1000), ["zoll"] = ("Länge", 0.0254), ["fuß"] = ("Länge", 0.3048), ["meile"] = ("Länge", 1609.344),
        ["mg"] = ("Gewicht", 0.000001), ["g"] = ("Gewicht", 0.001), ["kg"] = ("Gewicht", 1), ["t"] = ("Gewicht", 1000), ["pfund"] = ("Gewicht", 0.45359237), ["unze"] = ("Gewicht", 0.028349523125),
        ["ml"] = ("Volumen", 0.001), ["l"] = ("Volumen", 1), ["m³"] = ("Volumen", 1000), ["gallone"] = ("Volumen", 3.785411784),
        ["m²"] = ("Fläche", 1), ["km²"] = ("Fläche", 1e6), ["ha"] = ("Fläche", 1e4), ["ar"] = ("Fläche", 100),
        ["km/h"] = ("Tempo", 1 / 3.6), ["m/s"] = ("Tempo", 1), ["mph"] = ("Tempo", 0.44704),
    };
    public static readonly string[] Temps = ["°C", "°F", "K"];
    public static IEnumerable<string> All => U.Keys.Concat(Temps);
    public static double Convert(double v, string from, string to)
    {
        if (Temps.Contains(from) && Temps.Contains(to))
        {
            var c = from switch { "°F" => (v - 32) * 5 / 9, "K" => v - 273.15, _ => v };
            return to switch { "°F" => c * 9 / 5 + 32, "K" => c + 273.15, _ => c };
        }
        if (!U.TryGetValue(from, out var a) || !U.TryGetValue(to, out var b)) throw new ArgumentException("Unbekannte Einheit.");
        if (a.Group != b.Group) throw new ArgumentException($"{a.Group} lässt sich nicht in {b.Group} umrechnen.");
        return v * a.Factor / b.Factor;
    }
    /// <summary>Währung nur mit selbst eingetragenem Kurs – Trendo holt keinen Online-Kurs.</summary>
    public static double Currency(double amount, double rate) => rate > 0 ? amount * rate : throw new ArgumentException("Bitte einen Kurs größer als 0 eintragen.");
}

// P18 Farben
public static class Colors
{
    public static (byte R, byte G, byte B) ParseHex(string hex)
    {
        hex = hex.Trim().TrimStart('#'); if (hex.Length == 3) hex = string.Concat(hex.Select(c => $"{c}{c}"));
        if (hex.Length != 6 || !hex.All(Uri.IsHexDigit)) throw new ArgumentException("Farbe bitte wie #1A2B3C.");
        return (System.Convert.ToByte(hex[..2], 16), System.Convert.ToByte(hex[2..4], 16), System.Convert.ToByte(hex[4..], 16));
    }
    public static string Hex(byte r, byte g, byte b) => $"#{r:X2}{g:X2}{b:X2}";
    public static string Rgb(byte r, byte g, byte b) => $"rgb({r}, {g}, {b})";
    public static (double H, double S, double L) Hsl(byte r, byte g, byte b)
    {
        double R = r / 255.0, G = g / 255.0, B = b / 255.0, max = Math.Max(R, Math.Max(G, B)), min = Math.Min(R, Math.Min(G, B)), l = (max + min) / 2, d = max - min;
        if (d == 0) return (0, 0, Math.Round(l * 100));
        var s = l > 0.5 ? d / (2 - max - min) : d / (max + min);
        var h = max == R ? (G - B) / d + (G < B ? 6 : 0) : max == G ? (B - R) / d + 2 : (R - G) / d + 4;
        return (Math.Round(h * 60), Math.Round(s * 100), Math.Round(l * 100));
    }
}

// P21 Papierkorb
public sealed record TrashItem(string OriginalPath, long Bytes, DateTime Deleted, string DataFile);
public static class Trash
{
    /// <summary>Liest Windows-$I-Dateien (Version 1 und 2) aus einem $Recycle.Bin-Benutzerordner.</summary>
    public static TrashItem? ParseWindowsInfo(string iFile)
    {
        try
        {
            var b = File.ReadAllBytes(iFile); if (b.Length < 24) return null;
            var ver = BitConverter.ToInt64(b, 0); var size = BitConverter.ToInt64(b, 8); var at = DateTime.FromFileTime(BitConverter.ToInt64(b, 16));
            string name = ver == 2 ? Encoding.Unicode.GetString(b, 28, Math.Min(BitConverter.ToInt32(b, 24) * 2, b.Length - 28)) : Encoding.Unicode.GetString(b, 24, Math.Min(520, b.Length - 24));
            name = name.TrimEnd('\0'); var nul = name.IndexOf('\0'); if (nul >= 0) name = name[..nul];
            var data = Path.Combine(Path.GetDirectoryName(iFile)!, "$R" + Path.GetFileName(iFile)[2..]);
            return new(name, size, at, data);
        }
        catch (Exception e) when (e is IOException or ArgumentException or UnauthorizedAccessException) { return null; }
    }
    /// <summary>Linux/freedesktop: ~/.local/share/Trash (info/*.trashinfo + files/).</summary>
    public static TrashItem? ParseTrashInfo(string infoFile)
    {
        var t = File.ReadAllText(infoFile); var p = Regex.Match(t, @"^Path=(.*)$", RegexOptions.Multiline); var d = Regex.Match(t, @"^DeletionDate=(.*)$", RegexOptions.Multiline);
        if (!p.Success) return null;
        var data = Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(infoFile))!, "files", Path.GetFileNameWithoutExtension(infoFile));
        long size = File.Exists(data) ? new FileInfo(data).Length : Directory.Exists(data) ? FileAnalysis.FolderSize(data) : 0;
        DateTime.TryParse(d.Groups[1].Value.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var at);
        return new(Uri.UnescapeDataString(p.Groups[1].Value.Trim()), size, at, data);
    }
    public static List<TrashItem> List(string? windowsBinUserFolder, string? linuxTrash)
    {
        var res = new List<TrashItem>();
        if (windowsBinUserFolder is not null && Directory.Exists(windowsBinUserFolder))
            foreach (var f in Directory.GetFiles(windowsBinUserFolder, "$I*")) if (ParseWindowsInfo(f) is { } x) res.Add(x);
        if (linuxTrash is not null && Directory.Exists(Path.Combine(linuxTrash, "info")))
            foreach (var f in Directory.GetFiles(Path.Combine(linuxTrash, "info"), "*.trashinfo")) if (ParseTrashInfo(f) is { } x) res.Add(x);
        return res.OrderByDescending(r => r.Deleted).ToList();
    }
    /// <summary>Leeren nur mit ausdrücklicher Bestätigung (confirmed=true). Löscht Daten- und Info-Dateien.</summary>
    public static int Empty(IEnumerable<TrashItem> items, bool confirmed, AuditLog? audit = null)
    {
        if (!confirmed) throw new InvalidOperationException("Leeren braucht deine Bestätigung.");
        var n = 0;
        foreach (var i in items)
        {
            if (File.Exists(i.DataFile)) File.Delete(i.DataFile); else if (Directory.Exists(i.DataFile)) Directory.Delete(i.DataFile, true);
            var dir = Path.GetDirectoryName(i.DataFile)!; var nm = Path.GetFileName(i.DataFile);
            var info = nm.StartsWith("$R") ? Path.Combine(dir, "$I" + nm[2..]) : Path.Combine(Path.GetDirectoryName(dir)!, "info", nm + ".trashinfo");
            if (File.Exists(info)) File.Delete(info); n++;
        }
        audit?.Append("app", "trash.empty", $"{n} Einträge"); return n;
    }
}

// P24 Wetter (optional, Netz nur in der App), P25 Ressourcen-Warnung
public static class Weather
{
    public static string Url(double lat, double lon) => FormattableString.Invariant($"https://api.open-meteo.com/v1/forecast?latitude={lat:0.###}&longitude={lon:0.###}&current=temperature_2m,weather_code&timezone=auto");
    public static string Describe(int code) => code switch
    {
        0 => "klar", 1 or 2 => "leicht bewölkt", 3 => "bedeckt", 45 or 48 => "Nebel", >= 51 and <= 57 => "Nieselregen", >= 61 and <= 67 => "Regen", >= 71 and <= 77 => "Schnee", >= 80 and <= 82 => "Regenschauer", >= 85 and <= 86 => "Schneeschauer", >= 95 => "Gewitter", _ => "unbekannt",
    };
    public static string? Parse(string json)
    {
        try
        {
            using var d = JsonDocument.Parse(json); var c = d.RootElement.GetProperty("current");
            return $"{c.GetProperty("temperature_2m").GetDouble().ToString("0", CultureInfo.GetCultureInfo("de-DE"))} °C, {Describe(c.GetProperty("weather_code").GetInt32())}";
        }
        catch (Exception e) when (e is JsonException or KeyNotFoundException or InvalidOperationException) { return null; }
    }
}
public static class ResourceWarn
{
    /// <summary>Nur Hinweise, nie automatische Aktionen.</summary>
    public static List<string> Check(double? ramPercent, long? freeBytes)
    {
        var w = new List<string>();
        if (ramPercent > 90) w.Add($"Der Arbeitsspeicher ist zu {ramPercent:0} % belegt. Schließe Programme, die du nicht brauchst.");
        if (freeBytes is >= 0 and < 5L << 30) w.Add($"Auf der Festplatte sind nur noch {freeBytes / (double)(1L << 30):0.0} GB frei. Unter „Speicherplatz“ siehst du, was viel Platz braucht.");
        return w;
    }
}
