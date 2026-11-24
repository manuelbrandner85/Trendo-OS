using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Trendo.Core;

/// <summary>T-0767 „Datei senden“ zwischen PC und Handy (ohne App auf dem Handy, nur im Heimnetz).
/// Ankommende Dateien: Name gesäubert, Größe begrenzt, erst vollständig als versteckte Teil-Datei, dann unter dem echten Namen (kein halber Rest nach Abbruch).
/// Dateien für ein bestimmtes Handy liegen in einem versteckten Unterordner von „Zum Handy“, gelten nur für dieses Handy und nur 24 Stunden;
/// Entfernen des Handys löscht sie sofort.</summary>
public static class PhoneTransfer
{
    public const long DefaultMaxBytes = 2L << 30; // 2 GB je Datei
    public static readonly TimeSpan PendingLifetime = TimeSpan.FromHours(24);
    const int MaxNameLength = 120;
    const string DevicePrefix = ".fuer-";

    static readonly HashSet<string> Reserved = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL", "CONIN$", "CONOUT$",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
    };
    static readonly char[] Bad = ['<', '>', ':', '"', '/', '\\', '|', '?', '*'];

    public sealed class TooLargeException(long max) : IOException($"Die Datei ist größer als {Size(max)} und wurde nicht angenommen.")
    {
        public long Max { get; } = max;
    }

    public sealed record Received(string Path, long Bytes, string Sha256);

    /// <summary>Dateiname vom Handy oder für das Handy: nur der Name ohne Pfad, keine Steuer- und Sonderzeichen, keine Windows-Gerätenamen (CON, NUL …),
    /// keine führenden Punkte (versteckt), höchstens 120 Zeichen mit erhaltener Endung.</summary>
    public static string CleanName(string? raw, string fallback)
    {
        var s = (raw ?? "").Replace('\\', '/');
        s = s[(s.LastIndexOf('/') + 1)..];
        s = new string(s.Where(ch => !char.IsControl(ch) && Array.IndexOf(Bad, ch) < 0).ToArray()).Trim().Trim('.', ' ');
        if (s.Length == 0) s = fallback;
        var ext = Path.GetExtension(s);
        var stem = s[..^ext.Length];
        if (ext.Length > 16) { stem = s; ext = ""; }
        var dot = stem.IndexOf('.');
        if (Reserved.Contains(dot < 0 ? stem.TrimEnd() : stem[..dot].TrimEnd())) stem = "_" + stem;
        if (stem.Length + ext.Length > MaxNameLength) stem = stem[..(MaxNameLength - ext.Length)].TrimEnd(' ', '.');
        return stem + ext;
    }

    /// <summary>Freier Zielpfad: gleiche Namen bekommen eine Nummer („Foto (2).jpg“).</summary>
    public static string UniqueTarget(string dir, string name)
    {
        var target = Path.Combine(dir, name);
        for (var i = 2; File.Exists(target) || Directory.Exists(target); i++)
            target = Path.Combine(dir, $"{Path.GetFileNameWithoutExtension(name)} ({i}){Path.GetExtension(name)}");
        return target;
    }

    /// <summary>Nimmt eine Datei an: schreibt erst eine versteckte Teil-Datei, prüft die Größe beim Lesen, bildet SHA-256 und benennt erst am Ende um.
    /// Bei Abbruch oder zu großer Datei bleibt nichts liegen.</summary>
    public static async Task<Received> ReceiveAsync(Stream source, string dir, string? rawName, long maxBytes, CancellationToken ct = default)
    {
        Directory.CreateDirectory(dir);
        var part = Path.Combine(dir, "." + Guid.NewGuid().ToString("N")[..12] + ".teil");
        long total = 0;
        string sha;
        try
        {
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            await using (var dst = new FileStream(part, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true))
            {
                if (OperatingSystem.IsWindows()) File.SetAttributes(part, FileAttributes.Hidden);
                var buf = new byte[81920];
                int n;
                while ((n = await source.ReadAsync(buf, ct)) > 0)
                {
                    total += n;
                    if (total > maxBytes) throw new TooLargeException(maxBytes);
                    hash.AppendData(buf, 0, n);
                    await dst.WriteAsync(buf.AsMemory(0, n), ct);
                }
                await dst.FlushAsync(ct);
            }
            sha = Convert.ToHexStringLower(hash.GetHashAndReset());
            var target = UniqueTarget(dir, CleanName(rawName, $"vom-handy-{DateTime.Now:yyyyMMdd-HHmmss}"));
            if (OperatingSystem.IsWindows()) File.SetAttributes(part, FileAttributes.Normal);
            File.Move(part, target);
            return new(target, total, sha);
        }
        catch
        {
            try { if (File.Exists(part)) { File.SetAttributes(part, FileAttributes.Normal); File.Delete(part); } } catch (IOException) { } catch (UnauthorizedAccessException) { }
            throw;
        }
    }

    public static string Sha256File(string path)
    {
        using var s = File.OpenRead(path);
        return Convert.ToHexStringLower(SHA256.HashData(s));
    }

    /// <summary>Gerätekennung aus <see cref="PhoneAccess"/> (12 Hex-Zeichen) – alles andere wird nie zu einem Ordnernamen.</summary>
    public static bool ValidDeviceId(string? id) => id is { Length: >= 6 and <= 32 } && id.All(char.IsAsciiHexDigit);

    public static string DeviceFolder(string outbox, string deviceId) =>
        ValidDeviceId(deviceId) ? Path.Combine(outbox, DevicePrefix + deviceId.ToLowerInvariant()) : throw new ArgumentException("Gerätekennung ungültig.", nameof(deviceId));

    /// <summary>„Datei senden“ am PC: legt eine Kopie für ein bestimmtes Handy (oder ohne <paramref name="deviceId"/> für alle verbundenen) bereit.</summary>
    public static async Task<Received> OfferAsync(string source, string outbox, string? deviceId, long maxBytes = DefaultMaxBytes, CancellationToken ct = default)
    {
        var info = new FileInfo(source);
        if (!info.Exists) throw new FileNotFoundException("Datei nicht gefunden.", source);
        if (info.Length > maxBytes) throw new TooLargeException(maxBytes);
        var dir = deviceId is null ? outbox : DeviceFolder(outbox, deviceId);
        Directory.CreateDirectory(dir);
        if (deviceId is not null && OperatingSystem.IsWindows()) File.SetAttributes(dir, File.GetAttributes(dir) | FileAttributes.Hidden);
        await using var src = File.OpenRead(source);
        return await ReceiveAsync(src, dir, info.Name, maxBytes, ct);
    }

    /// <summary>Bereitgestellte Dateien für ein Handy, die älter als 24 Stunden sind, verschwinden.</summary>
    public static int PurgeExpired(string outbox, DateTime utcNow)
    {
        if (!Directory.Exists(outbox)) return 0;
        var n = 0;
        foreach (var dir in Directory.EnumerateDirectories(outbox, DevicePrefix + "*"))
            foreach (var f in new DirectoryInfo(dir).EnumerateFiles())
                if (utcNow - f.LastWriteTimeUtc > PendingLifetime && !f.Name.StartsWith('.'))
                    try { f.Delete(); n++; } catch (IOException) { } catch (UnauthorizedAccessException) { }
        return n;
    }

    /// <summary>Handy entfernt: alles, was nur für dieses Handy bereitlag, wird sofort gelöscht.</summary>
    public static void DropDevice(string outbox, string deviceId)
    {
        if (!ValidDeviceId(deviceId)) return;
        var dir = DeviceFolder(outbox, deviceId);
        if (!Directory.Exists(dir)) return;
        foreach (var f in Directory.EnumerateFiles(dir)) File.SetAttributes(f, FileAttributes.Normal);
        Directory.Delete(dir, true);
    }

    /// <summary>Was ein Handy sehen darf: zuerst seine eigenen bereitgestellten Dateien, dann der gemeinsame Ordner „Zum Handy“. Versteckte und Teil-Dateien nie.</summary>
    public static IReadOnlyList<(FileInfo File, bool Personal)> Visible(string outbox, string? deviceId, DateTime utcNow)
    {
        PurgeExpired(outbox, utcNow);
        var list = new List<(FileInfo, bool)>();
        if (deviceId is not null && ValidDeviceId(deviceId) && Directory.Exists(DeviceFolder(outbox, deviceId)))
            list.AddRange(Listed(DeviceFolder(outbox, deviceId)).Select(f => (f, true)));
        if (Directory.Exists(outbox)) list.AddRange(Listed(outbox).Select(f => (f, false)));
        return list;
    }

    static IEnumerable<FileInfo> Listed(string dir) => new DirectoryInfo(dir).EnumerateFiles()
        .Where(f => !f.Name.StartsWith('.') && !f.Attributes.HasFlag(FileAttributes.Hidden)).OrderByDescending(f => f.LastWriteTimeUtc).Take(200);

    /// <summary>Datei zu einem Namen aus der Liste; null bei Pfadteilen, versteckten Namen oder unbekannter Datei.</summary>
    public static string? Resolve(string outbox, string? deviceId, string name, DateTime utcNow)
    {
        if (string.IsNullOrEmpty(name) || Path.GetFileName(name) != name || name.StartsWith('.') || name.IndexOfAny(Bad) >= 0) return null;
        return Visible(outbox, deviceId, utcNow).Select(x => x.File).FirstOrDefault(f => f.Name == name)?.FullName;
    }

    public static string Size(long bytes) => bytes >= 1L << 30 ? $"{bytes / (double)(1L << 30):0.#} GB" : bytes >= 1L << 20 ? $"{bytes / (double)(1L << 20):0.#} MB" : bytes >= 1024 ? $"{bytes / 1024.0:0.#} KB" : $"{bytes} B";
}

/// <summary>T-0767 „Lauf aufzeichnen“: Protokoll für den echten Handy-Lauf (Kopplung, Dateien in beide Richtungen, Entfernen, Abweisung danach)
/// mit Zeit, Größe und SHA-256 – anonymisiert: keine Gerätenamen, keine Adressen, nur „Gerät 1“ und die Netzart.</summary>
public sealed class PhoneRunLog
{
    public static class Steps
    {
        public const string Offer = "QR-Code gezeigt";
        public const string Paired = "Kopplung";
        public const string PhoneToPc = "Handy → PC";
        public const string Provided = "PC → Handy bereitgestellt";
        public const string PcToPhone = "PC → Handy abgeholt";
        public const string CablePush = "Kabel PC → Handy";
        public const string CablePull = "Kabel Handy → PC";
        public const string Removed = "Entfernt";
        public const string Rejected = "Abgewiesen (nicht gekoppelt)";
    }

    public sealed record Entry(DateTime At, double Seconds, string Step, string Device, string Network, long? Bytes, string? Sha256, bool Ok, string Note);

    readonly object _lock = new();
    readonly List<Entry> _entries = [];
    readonly Dictionary<string, string> _alias = [];
    readonly HashSet<string> _removed = [];
    DateTime _start;

    public bool Active { get; private set; }
    public DateTime Started => _start;
    public IReadOnlyList<Entry> Entries { get { lock (_lock) return [.. _entries]; } }

    public void Start(DateTime? utcNow = null)
    {
        lock (_lock) { _entries.Clear(); _alias.Clear(); _removed.Clear(); _start = utcNow ?? DateTime.UtcNow; Active = true; }
    }

    /// <summary>Netzart statt Adresse: „Heimnetz“, „Tailscale“, „Loopback (Test am PC)“, „PC“ (ohne Gegenstelle).</summary>
    public static string Network(IPAddress? ip)
    {
        if (ip is null) return "PC";
        if (IPAddress.IsLoopback(ip)) return "Loopback (Test am PC)";
        if (PhoneAccess.IsTailscale(ip)) return "Tailscale";
        return PhoneAccess.AllowedAddress(ip) ? "Heimnetz" : "fremdes Netz";
    }

    /// <summary>Ein Schritt. <paramref name="deviceKey"/> ist die Gerätekennung (wird zu „Gerät 1“ …), nie der Name.
    /// Ist <paramref name="path"/> angegeben und fehlt die Prüfsumme, bildet das Protokoll sie selbst.</summary>
    public void Record(string step, string? deviceKey, IPAddress? from, string? path = null, long? bytes = null, string? sha = null, bool ok = true, string note = "", DateTime? utcNow = null)
    {
        if (!Active) return;
        if (path is not null && File.Exists(path) && (sha is null || bytes is null))
        {
            try { sha ??= PhoneTransfer.Sha256File(path); bytes ??= new FileInfo(path).Length; } catch (IOException) { }
        }
        var t = utcNow ?? DateTime.UtcNow;
        lock (_lock)
        {
            if (!Active) return;
            var dev = deviceKey is null ? "–" : _alias.TryGetValue(deviceKey, out var a) ? a : _alias[deviceKey] = $"Gerät {_alias.Count + 1}";
            if (step == Steps.Removed && deviceKey is not null) _removed.Add(dev);
            _entries.Add(new(t, Math.Round((t - _start).TotalSeconds, 1), step, dev, Network(from), bytes, sha, ok, Clean(note)));
        }
    }

    /// <summary>Wurde schon ein Gerät entfernt? (Dann zählt eine Abweisung als Nachweis der Sperre.)</summary>
    public bool AnyRemoved { get { lock (_lock) return _removed.Count > 0; } }

    static string Clean(string s) => new string((s ?? "").Where(ch => !char.IsControl(ch)).Take(160).ToArray()).Replace("|", "/");

    public sealed record Check(string Name, bool Ok, string Detail);

    /// <summary>Was der Lauf belegt: Kopplung, beide Richtungen mit gleicher Prüfsumme, Entfernen und Abweisung danach.</summary>
    public static IReadOnlyList<Check> Evaluate(IReadOnlyList<Entry> e)
    {
        var paired = e.Any(x => x.Step == Steps.Paired && x.Ok);
        var up = e.Where(x => x.Step is Steps.PhoneToPc or Steps.CablePull && x.Ok && x.Sha256 is not null).ToList();
        var upMatched = up.Where(x => x.Note.Contains("Prüfsumme gleich")).ToList();
        var provided = e.Where(x => x.Step == Steps.Provided && x.Sha256 is not null).Select(x => x.Sha256).ToHashSet();
        var down = e.Where(x => x.Step is Steps.PcToPhone && x.Ok && x.Sha256 is not null && provided.Contains(x.Sha256)).ToList();
        var cableDown = e.Where(x => x.Step == Steps.CablePush && x.Ok && x.Sha256 is not null).ToList();
        var removedAt = e.FirstOrDefault(x => x.Step == Steps.Removed)?.At;
        var rejected = removedAt is { } r && e.Any(x => x.Step == Steps.Rejected && x.At >= r);
        return
        [
            new("Kopplung per QR-Code", paired, paired ? "Einmal-Code eingelöst" : "keine Kopplung im Lauf"),
            new("Datei Handy → PC", up.Count > 0, up.Count == 0 ? "keine Datei angekommen"
                : $"{up.Count} Datei(en); Prüfsumme am Handy gebildet und gleich: {upMatched.Count}"),
            new("Datei PC → Handy", down.Count + cableDown.Count > 0, down.Count + cableDown.Count == 0 ? "keine Datei vollständig abgeholt"
                : $"{down.Count} über das Heimnetz vollständig gesendet (Prüfsumme wie bereitgestellt), {cableDown.Count} per Kabel"),
            new("Entfernen", removedAt is not null, removedAt is null ? "nicht entfernt" : "Gerät am PC entfernt"),
            new("Sperre nach Entfernen", rejected, rejected ? "Anfrage danach abgewiesen" : "keine Anfrage nach dem Entfernen beobachtet"),
        ];
    }

    /// <summary>Beendet die Aufzeichnung und schreibt Bericht (Markdown), Rohlog (JSON Lines) und Prüfsummen in <paramref name="dir"/>.</summary>
    public (string Report, string Raw, string Sums) Stop(string dir, string version, DateTime? utcNow = null)
    {
        List<Entry> e;
        lock (_lock) { Active = false; e = [.. _entries]; }
        var end = utcNow ?? DateTime.UtcNow;
        Directory.CreateDirectory(dir);
        var stamp = _start.ToString("yyyyMMdd-HHmmss");
        var raw = Path.Combine(dir, $"handy-lauf-{stamp}.jsonl");
        var md = Path.Combine(dir, $"handy-lauf-{stamp}.md");
        var sums = Path.Combine(dir, $"handy-lauf-{stamp}.sha256");
        var jo = new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
        AtomicFile.WriteAllText(raw, string.Concat(e.Select(x => JsonSerializer.Serialize(new
        {
            zeit = x.At.ToString("O"), sekunden = x.Seconds, schritt = x.Step, geraet = x.Device, netz = x.Network, bytes = x.Bytes, sha256 = x.Sha256, ok = x.Ok, hinweis = x.Note,
        }, jo) + "\n")));
        var sb = new StringBuilder();
        sb.AppendLine("# Handy-Lauf – Protokoll").AppendLine();
        sb.AppendLine($"- Trendo OS {version}");
        sb.AppendLine($"- Beginn: {_start:yyyy-MM-dd HH:mm:ss} UTC · Ende: {end:yyyy-MM-dd HH:mm:ss} UTC · Dauer: {(end - _start).TotalSeconds:0} s");
        sb.AppendLine("- Anonymisiert: keine Gerätenamen, keine Adressen, keine Dateinamen – Geräte heißen „Gerät 1“ usw., statt der Adresse steht die Netzart.");
        sb.AppendLine("- Rohlog: `" + Path.GetFileName(raw) + "` (JSON Lines), Prüfsummen der Dateien: `" + Path.GetFileName(sums) + "`").AppendLine();
        sb.AppendLine("## Ergebnis").AppendLine();
        foreach (var c in Evaluate(e)) sb.AppendLine($"- {(c.Ok ? "✓" : "✗")} {c.Name}: {c.Detail}");
        sb.AppendLine().AppendLine("## Schritte").AppendLine();
        sb.AppendLine("| s | Schritt | Gerät | Netz | Größe | SHA-256 | Ergebnis | Hinweis |");
        sb.AppendLine("|---:|---|---|---|---:|---|---|---|");
        foreach (var x in e)
            sb.AppendLine($"| {x.Seconds:0.0} | {x.Step} | {x.Device} | {x.Network} | {(x.Bytes is { } b ? b + " B" : "")} | {(x.Sha256 is { } s ? "`" + s + "`" : "")} | {(x.Step == Steps.Rejected ? "abgewiesen (Sperre wirkt)" : x.Ok ? "ok" : "Fehler")} | {x.Note} |");
        AtomicFile.WriteAllText(md, sb.ToString());
        AtomicFile.WriteAllText(sums, $"{PhoneTransfer.Sha256File(raw)}  {Path.GetFileName(raw)}\n{PhoneTransfer.Sha256File(md)}  {Path.GetFileName(md)}\n");
        return (md, raw, sums);
    }
}
