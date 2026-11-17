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

    public string Describe(string unit = "") =>
        $"{(Above ? "über" : "unter")} {Threshold.ToString("0.##", Fach.De)} {unit}".TrimEnd()
        + (DurationSeconds > 0 ? $" für {DurationSeconds.ToString("0.#", Fach.De)} s" : "")
        + (Hysteresis > 0 ? $", Hysterese {Hysteresis.ToString("0.##", Fach.De)}" : "");
}

/// <summary>T-0750: Eintrag im Alarmprotokoll (ausgelöst oder aufgehoben).</summary>
public sealed record TwinAlarm(DateTime Time, string RuleId, string Rule, string Asset, string Sensor, bool Raised, double Value);

/// <summary>T-0750: Alarmregeln auswerten, prüfen, als Datei aus- und einlesen.</summary>
public static class TwinRules
{
    public const int MaxLog = 2000;
    public const string FileFormat = "trendo-alarmregeln";

    public static void Validate(TwinRule r)
    {
        if (string.IsNullOrWhiteSpace(r.Asset) || string.IsNullOrWhiteSpace(r.Sensor)) throw new ArgumentException("Die Regel braucht Anlage und Sensor.");
        if (!double.IsFinite(r.Threshold)) throw new ArgumentException("Bitte eine Schwelle als Zahl eintragen.");
        if (!double.IsFinite(r.DurationSeconds) || r.DurationSeconds < 0 || r.DurationSeconds > 7 * 86400) throw new ArgumentException("Die Dauer muss zwischen 0 Sekunden und 7 Tagen liegen.");
        if (!double.IsFinite(r.Hysteresis) || r.Hysteresis < 0) throw new ArgumentException("Die Hysterese darf nicht negativ sein.");
    }

    static bool Breach(TwinRule r, double v) => r.Above ? v > r.Threshold : v < r.Threshold;
    static bool Clear(TwinRule r, double v) => r.Above ? v <= r.Threshold - r.Hysteresis : v >= r.Threshold + r.Hysteresis;

    /// <summary>Alle Wechsel (ausgelöst/aufgehoben) einer Regel über die Messwerte seit ihrer Anlage – Zustandsautomat mit Dauer und Hysterese.</summary>
    public static List<TwinAlarm> Evaluate(TwinModel m, TwinRule r)
    {
        var res = new List<TwinAlarm>();
        var rs = m.Readings.Where(x => x.Time >= r.Created && x.Asset.Equals(r.Asset, StringComparison.OrdinalIgnoreCase) && x.Sensor.Equals(r.Sensor, StringComparison.OrdinalIgnoreCase)).OrderBy(x => x.Time);
        var active = false; DateTime? since = null;
        foreach (var x in rs)
        {
            if (!active)
            {
                if (!Breach(r, x.Value)) { since = null; continue; }
                since ??= x.Time;
                if ((x.Time - since.Value).TotalSeconds >= r.DurationSeconds) { active = true; res.Add(new(x.Time, r.Id, r.Name, r.Asset, r.Sensor, true, x.Value)); }
            }
            else if (Clear(r, x.Value)) { active = false; since = null; res.Add(new(x.Time, r.Id, r.Name, r.Asset, r.Sensor, false, x.Value)); }
        }
        return res;
    }

    /// <summary>Ist die Regel gerade ausgelöst?</summary>
    public static bool IsActive(TwinModel m, TwinRule r) => r.Enabled && Evaluate(m, r) is { Count: > 0 } e && e[^1].Raised;

    /// <summary>Neue Wechsel aller eingeschalteten Regeln ins Protokoll schreiben und zurückgeben (für die Benachrichtigung).</summary>
    public static List<TwinAlarm> Check(TwinModel m)
    {
        var fresh = new List<TwinAlarm>();
        foreach (var r in m.Rules.Where(r => r.Enabled))
        {
            var last = m.Alarms.Where(a => a.RuleId == r.Id).Select(a => (DateTime?)a.Time).DefaultIfEmpty(null).Max();
            fresh.AddRange(Evaluate(m, r).Where(e => last is null || e.Time > last));
        }
        if (fresh.Count > 0)
        {
            m.Alarms.AddRange(fresh.OrderBy(a => a.Time));
            if (m.Alarms.Count > MaxLog) m.Alarms.RemoveRange(0, m.Alarms.Count - MaxLog);
        }
        return fresh;
    }

    static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    sealed record RuleFile(string Format, int Version, List<TwinRule> Regeln);

    /// <summary>Regeln als JSON-Datei („trendo-alarmregeln“, Version 1).</summary>
    public static string ExportJson(IEnumerable<TwinRule> rules) => JsonSerializer.Serialize(new RuleFile(FileFormat, 1, rules.ToList()), Json);

    /// <summary>Regeln aus einer Datei lesen: prüft Format und jede Regel, vergibt neue Kennungen; ausgewertet wird ab <paramref name="now"/>.</summary>
    public static List<TwinRule> ImportJson(string json, DateTime now)
    {
        RuleFile? f;
        try { f = JsonSerializer.Deserialize<RuleFile>(json); }
        catch (JsonException e) { throw new FormatException("Das ist keine Alarmregel-Datei von Trendo: " + e.Message); }
        if (f is null || f.Format != FileFormat || f.Regeln is null) throw new FormatException("Das ist keine Alarmregel-Datei von Trendo.");
        if (f.Version > 1) throw new FormatException($"Diese Alarmregel-Datei ist neuer (Version {f.Version}) als dieses Trendo.");
        foreach (var r in f.Regeln) { Validate(r); r.Id = Guid.NewGuid().ToString("N")[..8]; r.Created = now; if (string.IsNullOrWhiteSpace(r.Name)) r.Name = $"{r.Asset} – {r.Sensor}"; }
        return f.Regeln;
    }

    /// <summary>Alarmprotokoll als CSV (Zeit;Regel;Anlage;Sensor;Ereignis;Wert).</summary>
    public static string LogCsv(IEnumerable<TwinAlarm> log) =>
        "Zeit;Regel;Anlage;Sensor;Ereignis;Wert\n" + string.Concat(log.Select(a => $"{a.Time:yyyy-MM-dd HH:mm:ss};{a.Rule};{a.Asset};{a.Sensor};{(a.Raised ? "ausgelöst" : "aufgehoben")};{a.Value.ToString("0.###", Fach.De)}\n"));
}

/// <summary>T-0750: Quellen abfragen – wachsende Datei und HTTP im eigenen Netz. Nur lesen.</summary>
public static class TwinFeed
{
    public const int MaxChunk = 1 << 20;

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

    /// <summary>Neue, vollständige Zeilen einer wachsenden Datei lesen (ab <see cref="TwinSource.Offset"/>), Messwerte in den Zwilling schreiben.
    /// Wird die Datei kürzer (neu begonnen), liest Trendo wieder von vorn. Eine angefangene letzte Zeile bleibt für die nächste Abfrage.</summary>
    public static int PollFile(TwinHub hub, TwinSource s, DateTime now, bool save = true)
    {
        if (!File.Exists(s.Target)) throw new IOException($"Die Datei „{Path.GetFileName(s.Target)}“ gibt es nicht (mehr).");
        using var fs = new FileStream(s.Target, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        if (fs.Length < s.Offset) s.Offset = 0;
        if (s.Offset == 0 && fs.Length > MaxChunk) s.Offset = fs.Length - MaxChunk; // riesige Altdatei: nur das Ende
        fs.Seek(s.Offset, SeekOrigin.Begin);
        var buf = new byte[(int)Math.Min(MaxChunk, fs.Length - s.Offset)];
        var read = 0; while (read < buf.Length) { var n = fs.Read(buf, read, buf.Length - read); if (n == 0) break; read += n; }
        if (read == 0) return 0;
        var end = Array.LastIndexOf(buf, (byte)'\n', read - 1);
        if (end < 0) return 0;
        var text = Encoding.UTF8.GetString(buf, 0, end + 1);
        s.Offset += end + 1;
        var n2 = 0;
        foreach (var line in text.Split('\n'))
            foreach (var r in ParseLine(line, s, now)) { hub.Record(r.Time, r.Asset, r.Sensor, r.Value, save: false); n2++; }
        if (save) hub.Save();
        return n2;
    }

    /// <summary>Nur Adressen im eigenen Netz: localhost, *.local/*.lan/*.home.arpa/*.fritz.box oder private IP (10/8, 172.16/12, 192.168/16, 169.254/16, Loopback, fc00::/7, fe80::/10).</summary>
    public static bool IsLocalUrl(string url, out Uri? uri)
    {
        uri = null;
        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var u) || (u.Scheme != Uri.UriSchemeHttp && u.Scheme != Uri.UriSchemeHttps) || u.UserInfo.Length > 0) return false;
        var h = u.IdnHost.Trim('[', ']').ToLowerInvariant();
        bool ok;
        if (IPAddress.TryParse(h, out var ip))
        {
            if (ip.IsIPv4MappedToIPv6) ip = ip.MapToIPv4();
            var b = ip.GetAddressBytes();
            ok = IPAddress.IsLoopback(ip) || (ip.AddressFamily == AddressFamily.InterNetwork
                ? b[0] == 10 || (b[0] == 172 && b[1] is >= 16 and <= 31) || (b[0] == 192 && b[1] == 168) || (b[0] == 169 && b[1] == 254)
                : (b[0] & 0xFE) == 0xFC || (b[0] == 0xFE && (b[1] & 0xC0) == 0x80));
        }
        else ok = h == "localhost" || h.EndsWith(".local") || h.EndsWith(".lan") || h.EndsWith(".home.arpa") || h.EndsWith(".fritz.box") || h.EndsWith(".localhost");
        if (ok) uri = u;
        return ok;
    }

    /// <summary>Eine HTTP-Quelle einmal lesen (GET, höchstens 64 KB, 5 s). Nur mit Freigabe und nur im eigenen Netz; Weiterleitungen werden nicht verfolgt.</summary>
    public static async Task<double> FetchHttpAsync(HttpClient http, TwinSource s, CancellationToken ct = default)
    {
        if (!s.Approved) throw new InvalidOperationException("Diese Adresse ist noch nicht freigegeben.");
        if (!IsLocalUrl(s.Target, out var u)) throw new ArgumentException("Nur Adressen im eigenen Netz (z. B. http://192.168.1.20/status).");
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct); cts.CancelAfter(TimeSpan.FromSeconds(5));
        using var resp = await http.GetAsync(u, HttpCompletionOption.ResponseHeadersRead, cts.Token);
        if ((int)resp.StatusCode is >= 300 and < 400) throw new IOException("Die Adresse leitet weiter – Trendo folgt keiner Weiterleitung.");
        resp.EnsureSuccessStatusCode();
        await using var st = await resp.Content.ReadAsStreamAsync(cts.Token);
        var buf = new byte[65_536]; var n = 0; int k;
        while (n < buf.Length && (k = await st.ReadAsync(buf.AsMemory(n), cts.Token)) > 0) n += k;
        var body = Encoding.UTF8.GetString(buf, 0, n);
        var v = TwinMqtt.ParseValue(body, s.JsonField) ?? (string.IsNullOrEmpty(s.JsonField) && body.TrimStart().StartsWith('{') ? TwinMqtt.ParseValue(body, "value") ?? TwinMqtt.ParseValue(body, "wert") : null);
        return v ?? throw new FormatException("In der Antwort steht kein Messwert" + (string.IsNullOrEmpty(s.JsonField) ? "." : $" im Feld „{s.JsonField}“."));
    }

    /// <summary>HttpClient ohne Weiterleitungen und ohne Proxy (nur eigenes Netz).</summary>
    public static HttpClient NewHttp() => new(new HttpClientHandler { AllowAutoRedirect = false, UseProxy = false }) { Timeout = TimeSpan.FromSeconds(10) };
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

    public Dictionary<string, double> Read(DateTime now)
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

    public void Dispose() { if (_q != IntPtr.Zero) { PdhCloseQuery(_q); _q = IntPtr.Zero; } }

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
