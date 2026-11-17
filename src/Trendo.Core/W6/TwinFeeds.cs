using System.Globalization;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace Trendo.Core;

/// <summary>T-0750: weitere Messquelle des Anlagen-Monitors – eine fortlaufend wachsende Datei (CSV/Text/JSON-Zeilen) oder eine HTTP-Adresse im eigenen Netz.
/// Trendo liest nur. Eine HTTP-Quelle wird erst nach ausdrücklicher Freigabe abgefragt (<see cref="Approved"/>).</summary>
public sealed class TwinSource
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    /// <summary>„datei“ oder „http“.</summary>
    public string Kind { get; set; } = "datei";
    /// <summary>Dateipfad oder URL.</summary>
    public string Target { get; set; } = "";
    public string Asset { get; set; } = "";
    public string Sensor { get; set; } = "";
    /// <summary>Punkt-Pfad in JSON („ENERGY.Power“); leer = „value“/„wert“ bzw. reine Zahl.</summary>
    public string? JsonField { get; set; }
    /// <summary>Datei: bis hierher (Bytes) ist gelesen.</summary>
    public long Offset { get; set; }
    /// <summary>HTTP: Abfrage ausdrücklich freigegeben (Zeitpunkt steht im Protokoll).</summary>
    public bool Approved { get; set; }
    public string LastError { get; set; } = "";
    /// <summary>Gegenprüfung T-0750, Datei: beim nächsten Lesen bis zum nächsten Zeilenende verwerfen (nach Sprung ans Dateiende oder einer übergroßen Zeile).</summary>
    public bool Resync { get; set; }
    /// <summary>Gegenprüfung T-0750, Datei: Kennung der gelesenen Datei – wird sie durch eine andere ersetzt, liest Trendo von vorn.</summary>
    public string FileId { get; set; } = "";
}

/// <summary>T-0750: Alarmregel – Schwellwert über oder unter, Mindestdauer, Hysterese. Wird im Dokument (Modell des Zwillings) gespeichert.</summary>
public sealed class TwinRule
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string Name { get; set; } = "";
    public string Asset { get; set; } = "";
    public string Sensor { get; set; } = "";
    /// <summary>true = Alarm, wenn der Wert über der Schwelle liegt; false = darunter.</summary>
    public bool Above { get; set; } = true;
    public double Threshold { get; set; }
    /// <summary>So lange (Sekunden) muss die Schwelle ununterbrochen verletzt sein, bevor der Alarm auslöst.</summary>
    public double DurationSeconds { get; set; }
    /// <summary>Der Alarm endet erst, wenn der Wert um so viel wieder auf der guten Seite liegt (verhindert Flattern).</summary>
    public double Hysteresis { get; set; }
    public bool Enabled { get; set; } = true;
    /// <summary>Ab hier wird ausgewertet – ältere Messwerte lösen nichts mehr aus.</summary>
    public DateTime Created { get; set; } = DateTime.Now;
    /// <summary>Gegenprüfung T-0750: gespeicherter Zustand der Auswertung (ausgelöst, verletzt seit, zuletzt ausgewertet/gemeldet).
    /// Damit meldet jede Regel jeden Wechsel genau einmal – auch wenn Messwerte oder Protokoll gekürzt werden. Nicht Teil von Regel-Datei und Rückgängig.</summary>
    public TwinRuleState? State { get; set; }

    public string Describe(string unit = "") =>
        $"{(Above ? "über" : "unter")} {Threshold.ToString("0.##", Fach.De)} {unit}".TrimEnd()
        + (DurationSeconds > 0 ? $" für {DurationSeconds.ToString("0.#", Fach.De)} s" : "")
        + (Hysteresis > 0 ? $", Hysterese {Hysteresis.ToString("0.##", Fach.De)}" : "");

    /// <summary>Kopie ohne Auswertungszustand (für Regel-Datei und Rückgängig).</summary>
    public TwinRule WithoutState() { var c = (TwinRule)MemberwiseClone(); c.State = null; return c; }
}

/// <summary>Gegenprüfung T-0750: Zustand einer Regel. <see cref="Seen"/> = Zeit des zuletzt ausgewerteten Messwerts; nur neuere Werte werden ausgewertet.</summary>
public sealed class TwinRuleState
{
    public bool Active { get; set; }
    /// <summary>Seit wann die Schwelle ununterbrochen verletzt ist (noch nicht ausgelöst).</summary>
    public DateTime? Since { get; set; }
    public DateTime? Seen { get; set; }
    public DateTime? Reported { get; set; }
}

/// <summary>T-0750: Eintrag im Alarmprotokoll (ausgelöst oder aufgehoben).</summary>
public sealed record TwinAlarm(DateTime Time, string RuleId, string Rule, string Asset, string Sensor, bool Raised, double Value);

/// <summary>T-0750: Alarmregeln auswerten, prüfen, als Datei aus- und einlesen.</summary>
public static class TwinRules
{
    public const int MaxLog = 2000;
    public const string FileFormat = "trendo-alarmregeln";
    /// <summary>Gegenprüfung: Grenzen für Regel-Dateien.</summary>
    public const int MaxFileBytes = 1 << 20, MaxRules = 500, MaxNameLength = 300, MaxFieldLength = 200;

    public static void Validate(TwinRule r)
    {
        if (string.IsNullOrWhiteSpace(r.Asset) || string.IsNullOrWhiteSpace(r.Sensor)) throw new ArgumentException("Die Regel braucht Anlage und Sensor.");
        if (r.Asset.Length > MaxFieldLength || r.Sensor.Length > MaxFieldLength) throw new ArgumentException($"Anlage und Sensor dürfen höchstens {MaxFieldLength} Zeichen lang sein.");
        if ((r.Name?.Length ?? 0) > MaxNameLength) throw new ArgumentException($"Der Name einer Regel darf höchstens {MaxNameLength} Zeichen lang sein.");
        if (!double.IsFinite(r.Threshold)) throw new ArgumentException("Bitte eine Schwelle als Zahl eintragen.");
        if (!double.IsFinite(r.DurationSeconds) || r.DurationSeconds < 0 || r.DurationSeconds > 7 * 86400) throw new ArgumentException("Die Dauer muss zwischen 0 Sekunden und 7 Tagen liegen.");
        if (!double.IsFinite(r.Hysteresis) || r.Hysteresis < 0) throw new ArgumentException("Die Hysterese darf nicht negativ sein.");
    }

    static bool Breach(TwinRule r, double v) => r.Above ? v > r.Threshold : v < r.Threshold;
    static bool Clear(TwinRule r, double v) => r.Above ? v <= r.Threshold - r.Hysteresis : v >= r.Threshold + r.Hysteresis;
    static bool Match(TwinRule r, Reading x) => x.Asset.Equals(r.Asset, StringComparison.OrdinalIgnoreCase) && x.Sensor.Equals(r.Sensor, StringComparison.OrdinalIgnoreCase);

    /// <summary>Ein Messwert durch den Zustandsautomaten (Dauer, Hysterese); ein Wechsel landet in <paramref name="res"/>.</summary>
    static void Step(TwinRule r, TwinRuleState st, Reading x, List<TwinAlarm> res)
    {
        if (!st.Active)
        {
            if (!Breach(r, x.Value)) { st.Since = null; return; }
            st.Since ??= x.Time;
            if ((x.Time - st.Since.Value).TotalSeconds >= r.DurationSeconds) { st.Active = true; st.Since = null; st.Reported = x.Time; res.Add(new(x.Time, r.Id, r.Name, r.Asset, r.Sensor, true, x.Value)); }
        }
        else if (Clear(r, x.Value)) { st.Active = false; st.Since = null; st.Reported = x.Time; res.Add(new(x.Time, r.Id, r.Name, r.Asset, r.Sensor, false, x.Value)); }
    }

    /// <summary>Alle Wechsel (ausgelöst/aufgehoben) einer Regel über die Messwerte seit ihrer Anlage – vollständiges Nachrechnen (für Prüfungen und Auswertung).</summary>
    public static List<TwinAlarm> Evaluate(TwinModel m, TwinRule r)
    {
        var res = new List<TwinAlarm>(); var st = new TwinRuleState();
        foreach (var x in m.Readings.Where(x => x.Time >= r.Created && Match(r, x)).OrderBy(x => x.Time)) Step(r, st, x, res);
        return res;
    }

    /// <summary>Nachgerechnet: wäre die Regel nach allen Messwerten ausgelöst?</summary>
    public static bool IsActive(TwinModel m, TwinRule r) => r.Enabled && Evaluate(m, r) is { Count: > 0 } e && e[^1].Raised;

    /// <summary>Gegenprüfung: ist die Regel gerade ausgelöst? Liest nur den gespeicherten Zustand (für das Neuzeichnen, rechnet nichts nach).</summary>
    public static bool IsActive(TwinRule r) => r.Enabled && r.State is { Active: true };

    /// <summary>Neue Wechsel aller eingeschalteten Regeln ins Protokoll schreiben und zurückgeben (für die Benachrichtigung).
    /// Ausgewertet werden je Regel nur Messwerte nach dem zuletzt ausgewerteten (<see cref="TwinRuleState.Seen"/>) – Kürzungen von Verlauf oder Protokoll erzeugen keine Doppelmeldung.</summary>
    public static List<TwinAlarm> Check(TwinModel m)
    {
        var fresh = new List<TwinAlarm>();
        foreach (var r in m.Rules.Where(r => r.Enabled))
        {
            var st = r.State ??= new TwinRuleState();
            var seen = st.Seen;
            foreach (var x in m.Readings.Where(x => (seen is { } s ? x.Time > s : x.Time >= r.Created) && Match(r, x)).OrderBy(x => x.Time))
            { Step(r, st, x, fresh); st.Seen = x.Time; }
        }
        if (fresh.Count > 0)
        {
            m.Alarms.AddRange(fresh.OrderBy(a => a.Time));
            if (m.Alarms.Count > MaxLog) m.Alarms.RemoveRange(0, m.Alarms.Count - MaxLog);
        }
        return fresh;
    }

    /// <summary>Zustand für eine Regel, die (wieder) eingeschaltet oder neu übernommen wird: alles, was schon gemessen ist, gilt als gesehen –
    /// gemeldet wird nur, was ab jetzt hereinkommt.</summary>
    public static TwinRuleState Fresh(TwinModel m, TwinRule r)
    {
        DateTime? last = null;
        foreach (var x in m.Readings) if (Match(r, x) && (last is null || x.Time > last)) last = x.Time;
        return new TwinRuleState { Seen = last };
    }

    /// <summary>Regel ein- oder ausschalten. Eine wieder eingeschaltete Regel meldet nichts aus der Zeit, in der sie aus war.</summary>
    public static bool SetEnabled(TwinModel m, TwinRule r, bool on)
    {
        if (r.Enabled == on) return false;
        r.Enabled = on;
        r.State = on ? Fresh(m, r) : r.State;
        return true;
    }

    /// <summary>Regeln aus einem gespeicherten Stand (Rückgängig) übernehmen: der Auswertungszustand vorhandener Regeln bleibt;
    /// neu hinzukommende oder dabei wieder eingeschaltete Regeln beginnen bei den aktuellen Messwerten.</summary>
    public static void Adopt(TwinModel m, List<TwinRule> rules)
    {
        var old = m.Rules.ToDictionary(r => r.Id);
        foreach (var r in rules)
            r.State = old.TryGetValue(r.Id, out var o) && (o.Enabled || !r.Enabled) ? o.State : r.Enabled ? Fresh(m, r) : null;
        m.Rules.Clear(); m.Rules.AddRange(rules);
    }

    static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    sealed record RuleFile(string Format, int Version, List<TwinRule?> Regeln);

    /// <summary>Regeln als JSON-Datei („trendo-alarmregeln“, Version 1), ohne Auswertungszustand.</summary>
    public static string ExportJson(IEnumerable<TwinRule> rules) => JsonSerializer.Serialize(new RuleFile(FileFormat, 1, rules.Select(r => (TwinRule?)r.WithoutState()).ToList()), Json);

    /// <summary>Regeln aus einer Datei lesen: prüft Größe, Format, Version und jede Regel, vergibt neue Kennungen; ausgewertet wird ab <paramref name="now"/>.</summary>
    public static List<TwinRule> ImportJson(string json, DateTime now)
    {
        if (json.Length > MaxFileBytes) throw new FormatException($"Die Regel-Datei ist zu groß (höchstens {MaxFileBytes / 1024} KB).");
        RuleFile? f;
        try { f = JsonSerializer.Deserialize<RuleFile>(json); }
        catch (JsonException e) { throw new FormatException("Das ist keine Alarmregel-Datei von Trendo: " + e.Message); }
        if (f is null || f.Format != FileFormat || f.Regeln is null) throw new FormatException("Das ist keine Alarmregel-Datei von Trendo.");
        if (f.Version < 1) throw new FormatException("Die Alarmregel-Datei hat keine gültige Version.");
        if (f.Version > 1) throw new FormatException($"Diese Alarmregel-Datei ist neuer (Version {f.Version}) als dieses Trendo.");
        if (f.Regeln.Count > MaxRules) throw new FormatException($"Die Datei enthält {f.Regeln.Count} Regeln – höchstens {MaxRules} werden übernommen.");
        var res = new List<TwinRule>();
        for (var i = 0; i < f.Regeln.Count; i++)
        {
            var r = f.Regeln[i] ?? throw new FormatException($"Eintrag {i + 1} der Regel-Datei ist leer.");
            r.Asset ??= ""; r.Sensor ??= ""; r.Name ??= "";
            try { Validate(r); }
            catch (ArgumentException e) { throw new ArgumentException($"Regel {i + 1} der Datei: {e.Message}"); }
            r.Id = Guid.NewGuid().ToString("N")[..8]; r.Created = now; r.State = null;
            if (string.IsNullOrWhiteSpace(r.Name)) r.Name = $"{r.Asset} – {r.Sensor}";
            res.Add(r);
        }
        return res;
    }

    /// <summary>Alarmprotokoll als CSV (Zeit;Regel;Anlage;Sensor;Ereignis;Wert).</summary>
    public static string LogCsv(IEnumerable<TwinAlarm> log) =>
        "Zeit;Regel;Anlage;Sensor;Ereignis;Wert\n" + string.Concat(log.Select(a => $"{a.Time:yyyy-MM-dd HH:mm:ss};{a.Rule};{a.Asset};{a.Sensor};{(a.Raised ? "ausgelöst" : "aufgehoben")};{a.Value.ToString("0.###", Fach.De)}\n"));
}

/// <summary>Gegenprüfung T-0750: Ergebnis eines Lesevorgangs einer wachsenden Datei – ändert den Zwilling nicht und kann daher im Hintergrund laufen.</summary>
public sealed record TwinFileChunk(long Offset, bool Resync, string FileId, List<Reading> Readings, string Warning);

/// <summary>T-0750: Quellen abfragen – wachsende Datei und HTTP im eigenen Netz. Nur lesen. Der eigentliche HTTP-Abruf liegt in der App (QA-019: der Kern hat keine Netz-Abhängigkeit).</summary>
public static class TwinFeed
{
    public const int MaxChunk = 1 << 20;
    public const string TooLongLine = "Eine Zeile ohne Zeilenende war länger als 1 MB und wurde übersprungen.";

    /// <summary>Eine Zeile deuten: JSON-Objekt, „Zeit;Anlage;Sensor;Wert“, „Zeit;Wert“, „…;Wert“ oder eine reine Zahl. Kopfzeilen und Unlesbares → leer.</summary>
    public static List<Reading> ParseLine(string line, TwinSource s, DateTime now)
    {
        var l = line.Trim().TrimStart('﻿');
        if (l.Length == 0) return [];
        if (l.StartsWith('{'))
        {
            var v = TwinMqtt.ParseValue(l, s.JsonField) ?? (string.IsNullOrEmpty(s.JsonField) ? TwinMqtt.ParseValue(l, "value") ?? TwinMqtt.ParseValue(l, "wert") : null);
            if (v is null) return [];
            var t = now;
            try
            {
                using var d = JsonDocument.Parse(l);
                foreach (var k in new[] { "zeit", "time", "timestamp" })
                    if (d.RootElement.TryGetProperty(k, out var e) && e.ValueKind == JsonValueKind.String && TryTime(e.GetString()!, out var tt)) { t = tt; break; }
            }
            catch (JsonException) { }
            return [new(t, s.Asset, s.Sensor, v.Value)];
        }
        var sep = l.Contains(';') ? ';' : l.Contains('\t') ? '\t' : l.Contains(',') && !Num(l, out _) ? ',' : '\0';
        if (sep == '\0') return Num(l, out var only) ? [new(now, s.Asset, s.Sensor, only)] : [];
        var c = l.Split(sep).Select(x => x.Trim()).ToArray();
        var hasTime = TryTime(c[0], out var time);
        if (c.Length == 4 && hasTime && c[1].Length > 0 && c[2].Length > 0 && Num(c[3], out var full)) return [new(time, c[1], c[2], full)];
        return Num(c[^1], out var last) ? [new(hasTime ? time : now, s.Asset, s.Sensor, last)] : [];
    }

    static bool Num(string s, out double v) => double.TryParse(s.Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out v) && double.IsFinite(v);
    static bool TryTime(string s, out DateTime t)
    {
        t = default;
        if (Num(s, out _) || !(s.Contains(':') || s.Contains('-') || s.Count(ch => ch == '.') == 2)) return false;
        return DateTime.TryParse(s, Fach.De, DateTimeStyles.AssumeLocal, out t) || DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out t);
    }

    /// <summary>Netzwerkpfad (\\Rechner\Freigabe\…, //Rechner/…, \\?\UNC\…, Gerätepfade)? Solche Dateien beobachtet Trendo nicht.</summary>
    public static bool IsNetworkPath(string path) => path.StartsWith(@"\\", StringComparison.Ordinal) || path.StartsWith("//", StringComparison.Ordinal);
    public const string NetworkPathRefused = "Dateien auf anderen Rechnern (\\\\Rechner\\…) beobachtet Trendo nicht – bitte eine Datei auf diesem PC wählen.";

    /// <summary>Neue, vollständige Zeilen einer wachsenden Datei lesen (ab <see cref="TwinSource.Offset"/>), Messwerte in den Zwilling schreiben.
    /// Wird die Datei kürzer oder durch eine andere ersetzt (neu begonnen), liest Trendo wieder von vorn. Eine angefangene letzte Zeile bleibt für die nächste Abfrage.</summary>
    public static int PollFile(TwinHub hub, TwinSource s, DateTime now, bool save = true) => Apply(hub, s, ReadFile(s, now), save);

    /// <summary>Ergebnis von <see cref="ReadFile"/> übernehmen: Lesestand der Quelle, Messwerte in den Zwilling, Hinweis an der Quelle.</summary>
    public static int Apply(TwinHub hub, TwinSource s, TwinFileChunk c, bool save = true)
    {
        s.Offset = c.Offset; s.Resync = c.Resync; s.FileId = c.FileId; s.LastError = c.Warning;
        foreach (var r in c.Readings) hub.Record(r.Time, r.Asset, r.Sensor, r.Value, save: false);
        if (save) hub.Save();
        return c.Readings.Count;
    }

    /// <summary>Gegenprüfung T-0750: neue Zeilen lesen, ohne Zwilling oder Quelle zu ändern (läuft im Hintergrund).
    /// UTF-8 (mit/ohne BOM) und UTF-16 mit BOM. Bei einer riesigen Altdatei nur das letzte MB – die dabei angeschnittene erste Zeile wird verworfen.
    /// Eine Zeile über 1 MB ohne Zeilenende wird übersprungen und gemeldet. Ausgetauschte Datei (andere Dateikennung) → von vorn.</summary>
    public static TwinFileChunk ReadFile(TwinSource s, DateTime now)
    {
        if (IsNetworkPath(s.Target)) throw new IOException(NetworkPathRefused);
        if (!File.Exists(s.Target)) throw new IOException($"Die Datei „{Path.GetFileName(s.Target)}“ gibt es nicht (mehr).");
        using var fs = new FileStream(s.Target, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        var id = FileIdOf(fs);
        long offset = s.Offset; var resync = s.Resync;
        if (fs.Length < offset || (s.FileId.Length > 0 && id.Length > 0 && id != s.FileId)) { offset = 0; resync = false; } // neu begonnen oder ausgetauscht
        // Kodierung am Dateianfang erkennen (BOM)
        var head = new byte[3]; var hn = ReadFull(fs, 0, head, head.Length);
        var (enc, bom, unit, be) = hn >= 2 && head[0] == 0xFF && head[1] == 0xFE ? (Encoding.Unicode, 2, 2, false)
            : hn >= 2 && head[0] == 0xFE && head[1] == 0xFF ? (Encoding.BigEndianUnicode, 2, 2, true)
            : hn == 3 && head[0] == 0xEF && head[1] == 0xBB && head[2] == 0xBF ? (Encoding.UTF8, 3, 1, false)
            : (Encoding.UTF8, 0, 1, false);
        if (offset < bom) { offset = bom; resync = false; }
        if ((offset - bom) % unit != 0) offset++;
        if (offset == bom && fs.Length - bom > MaxChunk) { offset = fs.Length - MaxChunk + unit; if ((offset - bom) % unit != 0) offset++; resync = true; } // riesige Altdatei: nur das Ende
        var from = resync && offset - unit >= bom ? offset - unit : offset; // ein Zeichen davor: endet dort eine Zeile, ist nichts angeschnitten
        var len = (int)Math.Min(MaxChunk, fs.Length - from);
        if (len <= 0) return new(offset, resync, id, [], "");
        var buf = new byte[len];
        var read = ReadFull(fs, from, buf, len);
        read -= read % unit;
        var begin = 0;
        if (resync)
        {
            var nl = NextNewline(buf, 0, read, unit, be);
            if (nl < 0) return new(from + read, true, id, [], read == MaxChunk ? TooLongLine : ""); // weiter verwerfen bis zum nächsten Zeilenende
            begin = nl;
        }
        var end = LastNewline(buf, begin, read, unit, be);
        if (end < 0)
            return begin == 0 && read == MaxChunk
                ? new(from + read, true, id, [], TooLongLine) // Zeile über 1 MB: überspringen, Rest bis zum Zeilenende verwerfen
                : new(from + begin, false, id, [], "");      // angefangene Zeile wartet
        var text = enc.GetString(buf, begin, end - begin);
        var list = new List<Reading>();
        foreach (var line in text.Split('\n')) list.AddRange(ParseLine(line, s, now));
        return new(from + end, false, id, list, "");
    }

    static int ReadFull(FileStream fs, long pos, byte[] buf, int count)
    {
        fs.Seek(pos, SeekOrigin.Begin);
        var read = 0; while (read < count) { var n = fs.Read(buf, read, count - read); if (n == 0) break; read += n; }
        return read;
    }

    static bool IsNl(byte[] b, int i, int unit, bool be) => unit == 1 ? b[i] == (byte)'\n' : be ? b[i] == 0 && b[i + 1] == (byte)'\n' : b[i] == (byte)'\n' && b[i + 1] == 0;
    /// <summary>Position direkt nach dem ersten Zeilenende ab <paramref name="from"/>, sonst −1.</summary>
    static int NextNewline(byte[] b, int from, int to, int unit, bool be) { for (var i = from; i + unit <= to; i += unit) if (IsNl(b, i, unit, be)) return i + unit; return -1; }
    /// <summary>Position direkt nach dem letzten Zeilenende ab <paramref name="from"/>, sonst −1.</summary>
    static int LastNewline(byte[] b, int from, int to, int unit, bool be) { for (var i = to - unit; i >= from; i -= unit) if (IsNl(b, i, unit, be)) return i + unit; return -1; }

    /// <summary>Kennung der Datei (Windows: Datenträger + Dateinummer). So fällt auf, wenn eine gleich lange neue Datei die alte ersetzt. Sonst leer.</summary>
    static string FileIdOf(FileStream fs)
    {
        if (!OperatingSystem.IsWindows()) return "";
        return GetFileInformationByHandle(fs.SafeFileHandle, out var i) ? $"{i.VolumeSerialNumber:x8}-{i.FileIndexHigh:x8}{i.FileIndexLow:x8}" : "";
    }

    [StructLayout(LayoutKind.Sequential)]
    struct FileInfoByHandle { public uint Attributes, C1, C2, A1, A2, W1, W2, VolumeSerialNumber, SizeHigh, SizeLow, Links, FileIndexHigh, FileIndexLow; }
#pragma warning disable SYSLIB1054 // klassisches DllImport: Trendo.Core erlaubt keinen unsafe-Code
    [DllImport("kernel32.dll", SetLastError = true)][return: MarshalAs(UnmanagedType.Bool)] static extern bool GetFileInformationByHandle(Microsoft.Win32.SafeHandles.SafeFileHandle h, out FileInfoByHandle info);
#pragma warning restore SYSLIB1054

    /// <summary>Private, Loopback- oder Link-local-Adresse (10/8, 172.16/12, 192.168/16, 169.254/16, 127/8, ::1, fc00::/7, fe80::/10)?</summary>
    public static bool IsLocalAddress(IPAddress ip)
    {
        if (ip.IsIPv4MappedToIPv6) ip = ip.MapToIPv4();
        var b = ip.GetAddressBytes();
        return IPAddress.IsLoopback(ip) || (ip.AddressFamily == AddressFamily.InterNetwork
            ? b[0] == 10 || (b[0] == 172 && b[1] is >= 16 and <= 31) || (b[0] == 192 && b[1] == 168) || (b[0] == 169 && b[1] == 254)
            : ip.AddressFamily == AddressFamily.InterNetworkV6 && ((b[0] & 0xFE) == 0xFC || (b[0] == 0xFE && (b[1] & 0xC0) == 0x80)));
    }

    /// <summary>Nur Adressen im eigenen Netz: localhost, *.local/*.lan/*.home.arpa/*.fritz.box oder private IP (siehe <see cref="IsLocalAddress"/>).
    /// Bei Namen prüft die App beim Verbindungsaufbau zusätzlich die tatsächlich aufgelöste Adresse (Schutz vor DNS-Rebinding).</summary>
    public static bool IsLocalUrl(string url, out Uri? uri)
    {
        uri = null;
        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var u) || (u.Scheme != Uri.UriSchemeHttp && u.Scheme != Uri.UriSchemeHttps) || u.UserInfo.Length > 0) return false;
        var h = u.IdnHost.Trim('[', ']').ToLowerInvariant();
        var ok = IPAddress.TryParse(h, out var ip) ? IsLocalAddress(ip)
            : h == "localhost" || h.EndsWith(".local") || h.EndsWith(".lan") || h.EndsWith(".home.arpa") || h.EndsWith(".fritz.box") || h.EndsWith(".localhost");
        if (ok) uri = u;
        return ok;
    }

    /// <summary>Antwort einer HTTP-Quelle auswerten: Wert aus dem JSON-Feld, „value“/„wert“ oder eine reine Zahl.</summary>
    public static double ParseHttpBody(string body, TwinSource s)
    {
        var v = TwinMqtt.ParseValue(body, s.JsonField) ?? (string.IsNullOrEmpty(s.JsonField) && body.TrimStart().StartsWith('{') ? TwinMqtt.ParseValue(body, "value") ?? TwinMqtt.ParseValue(body, "wert") : null);
        return v ?? throw new FormatException("In der Antwort steht kein Messwert" + (string.IsNullOrEmpty(s.JsonField) ? "." : $" im Feld „{s.JsonField}“."));
    }

    /// <summary>Eine HTTP-Quelle einmal lesen – nur mit Freigabe und nur im eigenen Netz. Den Abruf selbst (<paramref name="get"/>: Adresse → Antworttext)
    /// reicht die App herein; der Kern hat keine Netz-Abhängigkeit (QA-019).</summary>
    public static async Task<double> FetchHttpAsync(TwinSource s, Func<Uri, CancellationToken, Task<string>> get, CancellationToken ct = default)
    {
        if (!s.Approved) throw new InvalidOperationException("Diese Adresse ist noch nicht freigegeben.");
        if (!IsLocalUrl(s.Target, out var u)) throw new ArgumentException("Nur Adressen im eigenen Netz (z. B. http://192.168.1.20/status).");
        return ParseHttpBody(await get(u!, ct), s);
    }
}

/// <summary>T-0750: echte Leistungswerte dieses PCs ohne Admin – Windows-Leistungsindikatoren (PDH: Prozessor, Datenträger), Speicherauslastung des Systems,
/// Netz-Durchsatz aller aktiven Adapter. Unter Linux aus /proc. Raten (CPU, Datenträger, Netz) brauchen zwei Messungen; die erste liefert sie noch nicht.</summary>
public sealed class PcCounters : IDisposable
{
    public const string Cpu = "Prozessor (CPU)", Ram = "Arbeitsspeicher belegt", Disk = "Datenträger aktiv", NetIn = "Netz empfangen", NetOut = "Netz gesendet";
    /// <summary>Sensor, Einheit, Warnung ab, Alarm ab.</summary>
    public static readonly (string Name, string Unit, double Warn, double Alarm)[] Sensors =
        [(Cpu, "%", 85, 95), (Ram, "%", 85, 95), (Disk, "%", 90, double.NaN), (NetIn, "kB/s", double.NaN, double.NaN), (NetOut, "kB/s", double.NaN, double.NaN)];

    IntPtr _q, _cpu, _diskIdle; bool _pdhTried;
    (long Rx, long Tx, DateTime At)? _net;
    (ulong Idle, ulong Total)? _linCpu;

    readonly object _lock = new();
    bool _disposed;
    /// <summary>Gegenprüfung T-0750: freigegeben (Fenster/Seite geschlossen)? Dann liefert <see cref="Read"/> nichts mehr.</summary>
    public bool IsDisposed { get { lock (_lock) return _disposed; } }

    /// <summary>Eine Messung. Die erste Abfrage der Leistungsindikatoren kann spürbar dauern – die App ruft das im Hintergrund auf. Sicher gegen gleichzeitiges <see cref="Dispose"/>.</summary>
    public Dictionary<string, double> Read(DateTime now) { lock (_lock) return _disposed ? [] : ReadCore(now); }

    Dictionary<string, double> ReadCore(DateTime now)
    {
        var res = new Dictionary<string, double>();
        if (OperatingSystem.IsWindows())
        {
            if (!_pdhTried)
            {
                _pdhTried = true;
                if (PdhOpenQueryW(null, IntPtr.Zero, out _q) == 0)
                {
                    if (PdhAddEnglishCounterW(_q, @"\Processor(_Total)\% Processor Time", IntPtr.Zero, out _cpu) != 0) _cpu = IntPtr.Zero;
                    if (PdhAddEnglishCounterW(_q, @"\PhysicalDisk(_Total)\% Idle Time", IntPtr.Zero, out _diskIdle) != 0) _diskIdle = IntPtr.Zero;
                    PdhCollectQueryData(_q); // erste Messung als Bezug
                }
                else _q = IntPtr.Zero;
            }
            else if (_q != IntPtr.Zero && PdhCollectQueryData(_q) == 0)
            {
                if (Val(_cpu) is { } c) res[Cpu] = Math.Round(Math.Clamp(c, 0, 100), 1);
                if (Val(_diskIdle) is { } i) res[Disk] = Math.Round(Math.Clamp(100 - i, 0, 100), 1);
            }
            var ms = new MemoryStatusEx { dwLength = (uint)Marshal.SizeOf<MemoryStatusEx>() };
            if (GlobalMemoryStatusEx(ref ms) && ms.ullTotalPhys > 0) res[Ram] = Math.Round(100.0 * (ms.ullTotalPhys - ms.ullAvailPhys) / ms.ullTotalPhys, 1);
        }
        else if (OperatingSystem.IsLinux())
        {
            try
            {
                var p = File.ReadLines("/proc/stat").First().Split(' ', StringSplitOptions.RemoveEmptyEntries).Skip(1).Select(ulong.Parse).ToArray();
                ulong idle = p[3] + (p.Length > 4 ? p[4] : 0), total = 0; foreach (var x in p.Take(8)) total += x;
                if (_linCpu is { } o && total > o.Total) res[Cpu] = Math.Round(100.0 * (1 - (double)(idle - o.Idle) / (total - o.Total)), 1);
                _linCpu = (idle, total);
                var mi = File.ReadLines("/proc/meminfo").Select(l => l.Split(':')).Where(a => a.Length == 2).ToDictionary(a => a[0], a => double.Parse(a[1].Trim().Split(' ')[0], CultureInfo.InvariantCulture));
                if (mi.TryGetValue("MemTotal", out var mt) && mi.TryGetValue("MemAvailable", out var ma) && mt > 0) res[Ram] = Math.Round(100 * (mt - ma) / mt, 1);
            }
            catch (Exception e) when (e is IOException or FormatException or UnauthorizedAccessException or IndexOutOfRangeException) { }
        }
        long rx = 0, tx = 0;
        try
        {
            foreach (var n in NetworkInterface.GetAllNetworkInterfaces().Where(n => n.OperationalStatus == OperationalStatus.Up && n.NetworkInterfaceType != NetworkInterfaceType.Loopback))
            { var st = n.GetIPStatistics(); rx += st.BytesReceived; tx += st.BytesSent; }
            if (_net is { } o && (now - o.At).TotalSeconds > 0.2 && rx >= o.Rx && tx >= o.Tx)
            { var s = (now - o.At).TotalSeconds; res[NetIn] = Math.Round((rx - o.Rx) / 1024.0 / s, 1); res[NetOut] = Math.Round((tx - o.Tx) / 1024.0 / s, 1); }
            _net = (rx, tx, now);
        }
        catch (NetworkInformationException) { }
        return res;
    }

    double? Val(IntPtr counter)
    {
        if (counter == IntPtr.Zero) return null;
        return PdhGetFormattedCounterValue(counter, 0x00000200 /* PDH_FMT_DOUBLE */ | 0x00008000 /* PDH_FMT_NOCAP100 */, out _, out var v) == 0 && v.CStatus == 0 && double.IsFinite(v.Value) ? v.Value : null;
    }

    public void Dispose() { lock (_lock) { _disposed = true; if (_q != IntPtr.Zero) { PdhCloseQuery(_q); _q = IntPtr.Zero; } } }

    [StructLayout(LayoutKind.Explicit)] struct PdhValue { [FieldOffset(0)] public uint CStatus; [FieldOffset(8)] public double Value; }
    [StructLayout(LayoutKind.Sequential)] struct MemoryStatusEx { public uint dwLength, dwMemoryLoad; public ulong ullTotalPhys, ullAvailPhys, ullTotalPageFile, ullAvailPageFile, ullTotalVirtual, ullAvailVirtual, ullAvailExtendedVirtual; }
#pragma warning disable SYSLIB1054 // klassisches DllImport: Trendo.Core erlaubt keinen unsafe-Code, den LibraryImport erzeugen würde
    [DllImport("pdh.dll", CharSet = CharSet.Unicode)] static extern uint PdhOpenQueryW(string? source, IntPtr user, out IntPtr query);
    [DllImport("pdh.dll", CharSet = CharSet.Unicode)] static extern uint PdhAddEnglishCounterW(IntPtr query, string path, IntPtr user, out IntPtr counter);
    [DllImport("pdh.dll")] static extern uint PdhCollectQueryData(IntPtr query);
    [DllImport("pdh.dll")] static extern uint PdhGetFormattedCounterValue(IntPtr counter, uint format, out uint type, out PdhValue value);
    [DllImport("pdh.dll")] static extern uint PdhCloseQuery(IntPtr query);
    [DllImport("kernel32.dll", SetLastError = true)][return: MarshalAs(UnmanagedType.Bool)] static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx m);
#pragma warning restore SYSLIB1054
}
