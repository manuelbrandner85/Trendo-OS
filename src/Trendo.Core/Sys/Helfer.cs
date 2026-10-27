using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Trendo.Core.Sys;

// N5, N9, N18, N20 (Freigabe Teil 5, Punkt 4): Notizbuch, Editor, Zettel, Uhr. Alles lokal im Benutzerdatenordner.

/// <summary>N9: Kodierung und Zeilenende einer Textdatei erkennen und beim Speichern genau so wieder schreiben.</summary>
public sealed record TextFileInfo(string Encoding, bool Bom, string Eol);

public static class TextFileCodec
{
    public static readonly string[] Encodings = ["UTF-8", "UTF-16 LE", "UTF-16 BE", "ANSI (Windows-1252)"];
    public static readonly string[] Eols = ["CRLF", "LF", "CR"];

    static TextFileCodec() => System.Text.Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    static Encoding Enc(string name) => name switch
    {
        "UTF-16 LE" => new UnicodeEncoding(false, false),
        "UTF-16 BE" => new UnicodeEncoding(true, false),
        "ANSI (Windows-1252)" => System.Text.Encoding.GetEncoding(1252),
        _ => new UTF8Encoding(false, true),
    };

    public static (string Text, TextFileInfo Info) Decode(byte[] b)
    {
        string enc; var bom = false; var skip = 0;
        if (b.Length >= 3 && b[0] == 0xEF && b[1] == 0xBB && b[2] == 0xBF) { enc = "UTF-8"; bom = true; skip = 3; }
        else if (b.Length >= 2 && b[0] == 0xFF && b[1] == 0xFE) { enc = "UTF-16 LE"; bom = true; skip = 2; }
        else if (b.Length >= 2 && b[0] == 0xFE && b[1] == 0xFF) { enc = "UTF-16 BE"; bom = true; skip = 2; }
        else
        {
            enc = "UTF-8";
            try { _ = new UTF8Encoding(false, true).GetString(b); }
            catch (DecoderFallbackException) { enc = "ANSI (Windows-1252)"; }
        }
        var text = Enc(enc).GetString(b, skip, b.Length - skip);
        return (text, new TextFileInfo(enc, bom, DetectEol(text)));
    }

    public static string DetectEol(string t)
    {
        int crlf = 0, lf = 0, cr = 0;
        for (var i = 0; i < t.Length; i++)
            if (t[i] == '\r') { if (i + 1 < t.Length && t[i + 1] == '\n') { crlf++; i++; } else cr++; }
            else if (t[i] == '\n') lf++;
        return crlf == 0 && lf == 0 && cr == 0 ? (OperatingSystem.IsWindows() ? "CRLF" : "LF") : crlf >= lf && crlf >= cr ? "CRLF" : lf >= cr ? "LF" : "CR";
    }

    public static string Normalize(string t, string eol) => Regex.Replace(t, "\r\n|\r|\n", eol switch { "LF" => "\n", "CR" => "\r", _ => "\r\n" });

    public static byte[] Encode(string text, TextFileInfo info)
    {
        var e = Enc(info.Encoding); var body = Normalize(text, info.Eol);
        if (info.Encoding.StartsWith("ANSI") && !CanAnsi(body)) throw new InvalidDataException("Der Text enthält Zeichen, die es in ANSI nicht gibt. Bitte als UTF-8 speichern.");
        var bytes = e.GetBytes(body);
        if (!info.Bom) return bytes;
        var pre = info.Encoding switch { "UTF-16 LE" => new byte[] { 0xFF, 0xFE }, "UTF-16 BE" => [0xFE, 0xFF], "UTF-8" => [0xEF, 0xBB, 0xBF], _ => [] };
        return [.. pre, .. bytes];
    }

    static bool CanAnsi(string s) { var e = System.Text.Encoding.GetEncoding(1252, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback); try { e.GetBytes(s); return true; } catch (EncoderFallbackException) { return false; } }

    public static (string Text, TextFileInfo Info) Load(string path)
    {
        var fi = new FileInfo(path);
        if (fi.Length > 50L << 20) throw new InvalidDataException("Die Datei ist größer als 50 MB – dafür ist der Editor nicht gedacht.");
        return Decode(File.ReadAllBytes(path));
    }

    public static void Save(string path, string text, TextFileInfo info)
    {
        var bytes = Encode(text, info);
        var tmp = path + ".trendo-tmp"; File.WriteAllBytes(tmp, bytes); File.Move(tmp, path, true);
    }

    /// <summary>Suchen ab Position (mit Umlauf), optional Groß/klein und regulärer Ausdruck. Liefert (Start, Länge) oder null.</summary>
    public static (int Start, int Length)? Find(string text, string what, int from, bool matchCase, bool regex)
    {
        if (what.Length == 0) return null;
        var rx = Pattern(what, matchCase, regex);
        from = Math.Clamp(from, 0, text.Length);
        var m = rx.Match(text, from); if (!m.Success || m.Length == 0) m = rx.Match(text, 0);
        return m.Success && m.Length > 0 ? (m.Index, m.Length) : null;
    }

    public static (string Text, int Count) ReplaceAll(string text, string what, string with, bool matchCase, bool regex)
    {
        if (what.Length == 0) return (text, 0);
        var rx = Pattern(what, matchCase, regex); var n = 0;
        var res = rx.Replace(text, m => { n++; return regex ? m.Result(with) : with; });
        return (res, n);
    }

    static Regex Pattern(string what, bool matchCase, bool regex)
    {
        try { return new Regex(regex ? what : Regex.Escape(what), (matchCase ? RegexOptions.None : RegexOptions.IgnoreCase) | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(2)); }
        catch (ArgumentException) { throw new ArgumentException("Der Suchausdruck ist kein gültiger regulärer Ausdruck."); }
    }

    /// <summary>Zeile und Spalte (beide ab 1) zu einer Zeichenposition.</summary>
    public static (int Line, int Col) LineCol(string text, int pos)
    {
        pos = Math.Clamp(pos, 0, text.Length); int line = 1, start = 0;
        for (var i = 0; i < pos; i++) if (text[i] == '\n') { line++; start = i + 1; }
        return (line, pos - start + 1);
    }

    /// <summary>Zeichenposition des Anfangs von Zeile <paramref name="line"/> (ab 1).</summary>
    public static int LineStart(string text, int line)
    {
        if (line <= 1) return 0;
        var n = 1;
        for (var i = 0; i < text.Length; i++) if (text[i] == '\n' && ++n == line) return i + 1;
        return text.Length;
    }
}

/// <summary>N5: Notizbuch → Abschnitte → Seiten. Jede Seite ist eine Markdown-Datei (bleibt mit jedem Programm lesbar). #Schlagworte werden erkannt.</summary>
public sealed record NotebookHit(string Section, string Page, string Snippet);

public sealed class Notebook(string dataRoot)
{
    public string Root => Path.Combine(dataRoot, "notizbuch");
    static readonly Regex TagRx = new(@"(?<![\w#])#([\p{L}\p{N}_-]{2,40})", RegexOptions.Compiled);

    public static string SafeName(string name)
    {
        var n = new string((name ?? "").Trim().Select(c => Path.GetInvalidFileNameChars().Contains(c) || c is '/' or '\\' or ':' ? '-' : c).ToArray()).Trim('.', ' ');
        if (n.Length == 0) throw new ArgumentException("Bitte einen Namen eintragen.");
        return n.Length > 80 ? n[..80] : n;
    }

    string Dir(string section) => Path.Combine(Root, SafeName(section));
    string FileOf(string section, string page) => Path.Combine(Dir(section), SafeName(page) + ".md");

    public List<string> Sections() => Directory.Exists(Root) ? Directory.GetDirectories(Root).Select(Path.GetFileName).OfType<string>().Where(n => !n.StartsWith('.')).Order(StringComparer.CurrentCultureIgnoreCase).ToList() : [];
    public List<string> Pages(string section) => Directory.Exists(Dir(section)) ? Directory.GetFiles(Dir(section), "*.md").Select(Path.GetFileNameWithoutExtension).OfType<string>().Order(StringComparer.CurrentCultureIgnoreCase).ToList() : [];

    public void AddSection(string section) => Directory.CreateDirectory(Dir(section));
    public string Read(string section, string page) => File.Exists(FileOf(section, page)) ? File.ReadAllText(FileOf(section, page)) : "";
    public void Write(string section, string page, string text) { Directory.CreateDirectory(Dir(section)); var f = FileOf(section, page); File.WriteAllText(f + ".tmp", text); File.Move(f + ".tmp", f, true); }
    public bool Exists(string section, string page) => File.Exists(FileOf(section, page));

    public void RenamePage(string section, string from, string to)
    {
        var a = FileOf(section, from); var b = FileOf(section, to);
        if (File.Exists(b)) throw new InvalidOperationException("Eine Seite mit diesem Namen gibt es in diesem Abschnitt schon.");
        File.Move(a, b);
    }

    /// <summary>Löschen legt die Seite in den Papierkorb des Notizbuchs (wiederherstellbar), nichts wird endgültig entfernt.</summary>
    public void DeletePage(string section, string page)
    {
        var f = FileOf(section, page); if (!File.Exists(f)) return;
        var bin = Path.Combine(Root, ".papierkorb"); Directory.CreateDirectory(bin);
        File.Move(f, Path.Combine(bin, $"{SafeName(section)} - {SafeName(page)} {DateTime.Now:yyyyMMdd-HHmmss}.md"), true);
    }

    public static List<string> Tags(string text) => TagRx.Matches(text).Select(m => m.Groups[1].Value).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

    public Dictionary<string, int> AllTags()
    {
        var d = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var s in Sections()) foreach (var p in Pages(s)) foreach (var t in Tags(Read(s, p))) d[t] = d.GetValueOrDefault(t) + 1;
        return d;
    }

    /// <summary>Volltextsuche über alle Seiten (Titel und Inhalt). „#wort“ sucht nur Schlagworte.</summary>
    public List<NotebookHit> Search(string q, int max = 50)
    {
        q = q.Trim(); var res = new List<NotebookHit>(); if (q.Length == 0) return res;
        var tag = q.StartsWith('#') ? q[1..] : null;
        foreach (var s in Sections())
            foreach (var p in Pages(s))
            {
                var t = Read(s, p);
                if (tag is not null) { if (Tags(t).Contains(tag, StringComparer.OrdinalIgnoreCase)) res.Add(new(s, p, Snip(t, "#" + tag))); }
                else if (p.Contains(q, StringComparison.CurrentCultureIgnoreCase) || t.Contains(q, StringComparison.CurrentCultureIgnoreCase)) res.Add(new(s, p, Snip(t, q)));
                if (res.Count >= max) return res;
            }
        return res;
    }

    static string Snip(string t, string q)
    {
        var i = t.IndexOf(q, StringComparison.CurrentCultureIgnoreCase);
        if (i < 0) return t.Length > 90 ? t[..90].ReplaceLineEndings(" ") + " …" : t.ReplaceLineEndings(" ");
        var a = Math.Max(0, i - 40); var len = Math.Min(t.Length - a, q.Length + 80);
        return (a > 0 ? "… " : "") + t.Substring(a, len).ReplaceLineEndings(" ") + (a + len < t.Length ? " …" : "");
    }
}

/// <summary>N18: Zettel (Haftnotizen). Angeheftete Zettel erscheinen auf der Startseite.</summary>
public sealed record Zettel(string Id, string Text, string Color = "Gelb", bool OnDesktop = true, DateTime Changed = default);

public sealed class ZettelStore(string dataRoot)
{
    public static readonly (string Name, string Hex)[] Colors = [("Gelb", "#FFF3A6"), ("Grün", "#C8F2C2"), ("Blau", "#BFE3FF"), ("Rosa", "#FFC9DE"), ("Lila", "#DCCBFF"), ("Grau", "#E3E6EA")];
    public static string HexOf(string color) => Colors.FirstOrDefault(c => c.Name == color).Hex ?? Colors[0].Hex;
    string F => Path.Combine(dataRoot, "zettel.json");
    public List<Zettel> All() => J.Load<Zettel>(F);
    public Zettel Add(string text = "", string color = "Gelb") { var l = All(); var z = new Zettel(Guid.NewGuid().ToString("N")[..8], text, color, true, DateTime.Now); l.Insert(0, z); J.Save(F, l); return z; }
    public void Update(Zettel z) { var l = All(); var i = l.FindIndex(x => x.Id == z.Id); if (i < 0) return; l[i] = z with { Changed = DateTime.Now }; J.Save(F, l); }
    public void Delete(string id) { var l = All(); l.RemoveAll(z => z.Id == id); J.Save(F, l); }
    public List<Zettel> Pinned() => All().Where(z => z.OnDesktop && z.Text.Trim().Length > 0).ToList();
}

/// <summary>N20: Wecker. Days ist eine Bitmaske Mo=1 … So=64; 0 heißt „einmalig“ (wird nach dem Klingeln ausgeschaltet).</summary>
public sealed record Alarm(string Id, int Hour, int Minute, string Label = "Wecker", int Days = 0, bool Enabled = true, DateTime? LastFired = null);

public sealed class AlarmStore(string dataRoot)
{
    public static readonly string[] DayNames = ["Mo", "Di", "Mi", "Do", "Fr", "Sa", "So"];
    string F => Path.Combine(dataRoot, "werkzeuge", "wecker.json");
    public List<Alarm> All() => J.Load<Alarm>(F).OrderBy(a => a.Hour).ThenBy(a => a.Minute).ToList();
    public Alarm Add(int hour, int minute, string label, int days)
    {
        if (hour is < 0 or > 23 || minute is < 0 or > 59) throw new ArgumentException("Uhrzeit bitte als HH:MM, z. B. 06:30.");
        var l = J.Load<Alarm>(F); var a = new Alarm(Guid.NewGuid().ToString("N")[..8], hour, minute, string.IsNullOrWhiteSpace(label) ? "Wecker" : label.Trim(), days); l.Add(a); J.Save(F, l); return a;
    }
    public void Set(string id, bool enabled) { var l = J.Load<Alarm>(F); var i = l.FindIndex(a => a.Id == id); if (i >= 0) { l[i] = l[i] with { Enabled = enabled }; J.Save(F, l); } }
    public void Delete(string id) { var l = J.Load<Alarm>(F); l.RemoveAll(a => a.Id == id); J.Save(F, l); }

    static int Bit(DayOfWeek d) => 1 << ((int)d + 6) % 7;
    public static string DaysText(int days) => days == 0 ? "einmalig" : days == 127 ? "täglich" : days == 31 ? "Mo–Fr" : days == 96 ? "Wochenende" : string.Join(", ", DayNames.Where((_, k) => (days & (1 << k)) != 0));

    /// <summary>Nächster Termin ab <paramref name="now"/> (oder null, wenn ausgeschaltet).</summary>
    public static DateTime? Next(Alarm a, DateTime now)
    {
        if (!a.Enabled) return null;
        for (var k = 0; k <= 7; k++)
        {
            var t = now.Date.AddDays(k).AddHours(a.Hour).AddMinutes(a.Minute);
            if (t <= now || (a.LastFired is { } lf && t <= lf)) continue;
            if (a.Days == 0 || (a.Days & Bit(t.DayOfWeek)) != 0) return t;
        }
        return null;
    }

    /// <summary>Fällige Wecker (Klingelzeit bis 2 Minuten zurück) – werden als geklingelt vermerkt, einmalige ausgeschaltet.</summary>
    public List<Alarm> TakeDue(DateTime now)
    {
        var l = J.Load<Alarm>(F); var due = new List<Alarm>();
        for (var i = 0; i < l.Count; i++)
        {
            var a = l[i]; if (!a.Enabled) continue;
            var t = now.Date.AddHours(a.Hour).AddMinutes(a.Minute);
            if (t > now.AddSeconds(1) || now - t > TimeSpan.FromMinutes(2) || (a.LastFired is { } lf && lf >= t)) continue;
            if (a.Days != 0 && (a.Days & Bit(t.DayOfWeek)) == 0) continue;
            due.Add(a); l[i] = a with { LastFired = t, Enabled = a.Days != 0 };
        }
        if (due.Count > 0) J.Save(F, l);
        return due;
    }
}

/// <summary>N20: Weltuhr mit eigener Städteliste (IANA-Zeitzonen, auf Windows und Linux gleich).</summary>
public sealed class WorldClock(string dataRoot)
{
    public static readonly (string City, string Zone)[] Choices =
    [
        ("Berlin", "Europe/Berlin"), ("London", "Europe/London"), ("Lissabon", "Europe/Lisbon"), ("Istanbul", "Europe/Istanbul"), ("Moskau", "Europe/Moscow"),
        ("Dubai", "Asia/Dubai"), ("Neu-Delhi", "Asia/Kolkata"), ("Bangkok", "Asia/Bangkok"), ("Peking", "Asia/Shanghai"), ("Tokio", "Asia/Tokyo"),
        ("Sydney", "Australia/Sydney"), ("Auckland", "Pacific/Auckland"), ("New York", "America/New_York"), ("Chicago", "America/Chicago"),
        ("Los Angeles", "America/Los_Angeles"), ("Mexiko-Stadt", "America/Mexico_City"), ("São Paulo", "America/Sao_Paulo"), ("Kairo", "Africa/Cairo"), ("Johannesburg", "Africa/Johannesburg"), ("UTC", "Etc/UTC"),
    ];
    string F => Path.Combine(dataRoot, "werkzeuge", "weltuhr.txt");
    public List<string> Cities() => File.Exists(F) ? File.ReadAllLines(F).Where(c => Choices.Any(x => x.City == c)).ToList() : ["London", "New York", "Tokio"];
    public void Save(IEnumerable<string> cities) { Directory.CreateDirectory(Path.GetDirectoryName(F)!); File.WriteAllLines(F, cities.Distinct()); }
    public void Add(string city) { var l = Cities(); if (!l.Contains(city) && Choices.Any(x => x.City == city)) { l.Add(city); Save(l); } }
    public void Remove(string city) { var l = Cities(); l.Remove(city); Save(l); }

    /// <summary>Ortszeit und Abstand zur eigenen Uhr in Stunden.</summary>
    public static (DateTime Local, double Offset)? At(string city, DateTime utc)
    {
        var zone = Choices.FirstOrDefault(c => c.City == city).Zone; if (zone is null) return null;
        if (!TimeZoneInfo.TryFindSystemTimeZoneById(zone, out var tz)) return null;
        var t = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), tz);
        return (t, (tz.GetUtcOffset(utc) - TimeZoneInfo.Local.GetUtcOffset(utc)).TotalHours);
    }

    public static string OffsetText(double h) => h == 0 ? "gleiche Zeit" : (h > 0 ? "+" : "−") + Math.Abs(h).ToString("0.##", CultureInfo.GetCultureInfo("de-DE")) + " Std.";
}

/// <summary>N20: Weckton als kleine WAV-Datei (drei kurze Töne), damit kein fremder Klang mitgeliefert werden muss.</summary>
public static class AlarmTone
{
    public static string Ensure(string dataRoot)
    {
        var f = Path.Combine(dataRoot, "werkzeuge", "weckton.wav");
        if (File.Exists(f)) return f;
        Directory.CreateDirectory(Path.GetDirectoryName(f)!);
        const int rate = 22050; var samples = new List<short>();
        for (var beep = 0; beep < 3; beep++)
        {
            for (var k = 0; k < rate * 0.25; k++) { var env = Math.Min(1, Math.Min(k, rate * 0.25 - k) / 400.0); samples.Add((short)(Math.Sin(2 * Math.PI * 880 * k / rate) * 12000 * env)); }
            for (var k = 0; k < rate * 0.15; k++) samples.Add(0);
        }
        using var w = new BinaryWriter(File.Create(f));
        w.Write("RIFF"u8); w.Write(36 + samples.Count * 2); w.Write("WAVEfmt "u8); w.Write(16); w.Write((short)1); w.Write((short)1); w.Write(rate); w.Write(rate * 2); w.Write((short)2); w.Write((short)16);
        w.Write("data"u8); w.Write(samples.Count * 2); foreach (var s in samples) w.Write(s);
        return f;
    }
}
