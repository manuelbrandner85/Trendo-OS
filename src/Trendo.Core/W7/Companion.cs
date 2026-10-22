using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Trendo.Core;

public sealed record PairRequest(string Code, string PcName);
public sealed record PairResponse(string Token, string Name, string Kind, List<string> Caps, string Version);
public sealed record CompanionHealth(string App, string Version, string Name, string Kind);
public sealed record CompanionInfo(string Name, string Kind, string Model, string Os, int Cpus, long MemTotalMb, long MemFreeMb, double? TempC, double Uptime, List<string> Caps);
public sealed record OcrResult(string Text, double Confidence, List<string> Unreadable);

public enum PairGate { Ok, RateLimited, Locked }

/// <summary>Ergebnis der Prüfung vor einem Kopplungsversuch: frei, zu viele Versuche oder vorübergehend gesperrt.</summary>
public readonly record struct PairDecision(PairGate Gate, string Error, int RetryAfterSeconds);

/// <summary>Zustand der Gegenstelle (Pi/Linux): Kopplungscode, gekoppelte PCs (nur Token-Hashes), Gültigkeit und Sperren.</summary>
public sealed class CompanionState
{
    public const int DefaultCodeMinutes = 10;
    public const int DefaultTokenValidHours = 24 * 30;
    public const int DefaultPairWindowSeconds = 60;
    public const int DefaultPairMaxAttempts = 5;
    public const int DefaultPairFailuresBeforeLock = 5;
    public const int DefaultPairLockMinutes = 15;

    public string Name { get; set; } = Environment.MachineName;
    public string? Code { get; set; }
    public DateTime CodeExpires { get; set; }
    public int FailedTries { get; set; }
    public List<PairedPc> Pcs { get; set; } = [];
    /// <summary>Gespeicherte Freigabe: beim nächsten Start ohne --local auf allen Schnittstellen hören. Standard aus.</summary>
    public bool ListenLan { get; set; }
    /// <summary>Gültigkeit eines neuen Kopplungscodes in Minuten (1–60). Standard 10.</summary>
    public int CodeValidMinutes { get; set; } = DefaultCodeMinutes;
    /// <summary>Gültigkeit eines neuen Zugriffstokens in Stunden (1–8760). Standard 30 Tage.</summary>
    public int TokenValidHours { get; set; } = DefaultTokenValidHours;
    public int PairWindowSeconds { get; set; } = DefaultPairWindowSeconds;
    public int PairMaxAttempts { get; set; } = DefaultPairMaxAttempts;
    public int PairFailuresBeforeLock { get; set; } = DefaultPairFailuresBeforeLock;
    public int PairLockMinutes { get; set; } = DefaultPairLockMinutes;
    public List<PairGuard> PairGuards { get; set; } = [];

    /// <summary>Gekoppelter PC. Expires fehlt bei älteren Dateien – dann gilt At plus die eingestellte Token-Gültigkeit.</summary>
    public sealed record PairedPc(string Name, string TokenSha256, DateTime At, DateTime? Expires = null);

    public sealed class PairGuard
    {
        public string Ip { get; set; } = "";
        public int Failures { get; set; }
        public DateTime? LockedUntil { get; set; }
        public List<DateTime> Attempts { get; set; } = [];
    }

    public int CodeMinutes => CodeValidMinutes is >= 1 and <= 60 ? CodeValidMinutes : DefaultCodeMinutes;
    public TimeSpan TokenLifetime => TimeSpan.FromHours(TokenValidHours is >= 1 and <= 24 * 365 ? TokenValidHours : DefaultTokenValidHours);
    public int WindowSeconds => PairWindowSeconds is >= 10 and <= 3600 ? PairWindowSeconds : DefaultPairWindowSeconds;
    public int MaxAttempts => PairMaxAttempts is >= 1 and <= 30 ? PairMaxAttempts : DefaultPairMaxAttempts;
    public int FailuresBeforeLock => PairFailuresBeforeLock is >= 3 and <= 20 ? PairFailuresBeforeLock : DefaultPairFailuresBeforeLock;
    public int LockMinutes => PairLockMinutes is >= 1 and <= 1440 ? PairLockMinutes : DefaultPairLockMinutes;

    private static readonly JsonSerializerOptions J = new() { WriteIndented = true };
    public static CompanionState Load(string file) { try { return File.Exists(file) ? JsonSerializer.Deserialize<CompanionState>(File.ReadAllText(file)) ?? new() : new(); } catch (JsonException) { return new(); } }
    public void Save(string file)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(file))!);
        var tmp = file + ".tmp"; File.WriteAllText(tmp, JsonSerializer.Serialize(this, J));
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(tmp, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        File.Move(tmp, file, true);
    }

    public string NewCode(DateTime now) { Code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6"); CodeExpires = now.AddMinutes(CodeMinutes); FailedTries = 0; return Code; }

    public static string Sha(string s) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(s)));

    public static string LockText(DateTime until) => $"vorübergehend gesperrt bis {until:dd.MM.yyyy HH:mm}";

    /// <summary>Einmal-Code prüfen; nach 5 Fehlversuchen ist der Code verbrannt. Das Token gilt nur bis zum Ablauf.</summary>
    public (string? Token, string Error) TryPair(string code, string pcName, DateTime now)
    {
        if (Code is null || now > CodeExpires) return (null, "kein gültiger Code – auf dem Gerät „trendo-companion pair“ ausführen");
        if (!CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(Code), Encoding.ASCII.GetBytes((code ?? "").Trim().PadRight(6)[..6])))
        { if (++FailedTries >= 5) Code = null; return (null, "Code falsch"); }
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var n = string.IsNullOrWhiteSpace(pcName) ? "PC" : pcName.Trim()[..Math.Min(40, pcName.Trim().Length)];
        var exp = now.Add(TokenLifetime);
        Pcs.Add(new(n, Sha(token), now, exp)); Code = null; FailedTries = 0;
        return (token, "");
    }

    public DateTime TokenExpiry(PairedPc pc) => pc.Expires ?? pc.At.Add(TokenLifetime);

    /// <summary>null = erlaubt. Sonst „nicht gekoppelt“ oder „Token abgelaufen“.</summary>
    public string? AuthError(string? authHeader, DateTime now)
    {
        if (authHeader is null || !authHeader.StartsWith("Bearer ", StringComparison.Ordinal)) return "nicht gekoppelt";
        var raw = authHeader[7..].Trim();
        if (raw.Length == 0) return "nicht gekoppelt";
        var h = Encoding.ASCII.GetBytes(Sha(raw));
        foreach (var p in Pcs)
        {
            var stored = Encoding.ASCII.GetBytes(p.TokenSha256 ?? "");
            if (stored.Length != h.Length || !CryptographicOperations.FixedTimeEquals(stored, h)) continue;
            return now > TokenExpiry(p) ? "Token abgelaufen" : null;
        }
        return "nicht gekoppelt";
    }

    public bool Authorized(string? authHeader) => AuthError(authHeader, DateTime.Now) is null;
    public bool Authorized(string? authHeader, DateTime now) => AuthError(authHeader, now) is null;

    public bool Unpair(string authHeader) { var h = Sha(authHeader[7..].Trim()); return Pcs.RemoveAll(p => p.TokenSha256 == h) > 0; }

    /// <summary>Rate-Limit und Sperre gelten nur für diese Adresse. Andere Clients bleiben unberührt.</summary>
    public PairDecision BeginPair(string ip, DateTime now)
    {
        var g = Guard(ip);
        if (g.LockedUntil is DateTime until && now < until)
            return new(PairGate.Locked, LockText(until), Math.Max(1, (int)Math.Ceiling((until - now).TotalSeconds)));
        if (g.LockedUntil is not null) { g.LockedUntil = null; g.Failures = 0; }
        var window = TimeSpan.FromSeconds(WindowSeconds);
        g.Attempts.RemoveAll(t => now - t >= window);
        if (g.Attempts.Count >= MaxAttempts)
            return new(PairGate.RateLimited, "zu viele Versuche – bitte kurz warten", WindowSeconds);
        g.Attempts.Add(now);
        Trim(now, g.Ip);
        return new(PairGate.Ok, "", 0);
    }

    public void NotePairFailure(string ip, DateTime now)
    {
        var g = Guard(ip);
        g.Failures++;
        if (g.Failures >= FailuresBeforeLock)
        {
            g.LockedUntil = now.AddMinutes(LockMinutes);
            g.Failures = 0;
        }
    }

    public void NotePairSuccess(string ip)
    {
        var g = Guard(ip);
        g.Failures = 0;
        g.LockedUntil = null;
    }

    public (string? Message, int RetryAfterSeconds) FreshLock(string ip, DateTime now)
    {
        var g = PairGuards.FirstOrDefault(x => string.Equals(x.Ip, Normalize(ip), StringComparison.Ordinal));
        if (g?.LockedUntil is DateTime until && now < until)
            return (LockText(until), Math.Max(1, (int)Math.Ceiling((until - now).TotalSeconds)));
        return (null, 0);
    }

    private PairGuard Guard(string ip)
    {
        ip = Normalize(ip);
        var g = PairGuards.FirstOrDefault(x => string.Equals(x.Ip, ip, StringComparison.Ordinal));
        if (g is not null) return g;
        g = new PairGuard { Ip = ip };
        PairGuards.Add(g);
        return g;
    }

    private static string Normalize(string ip) => string.IsNullOrWhiteSpace(ip) ? "unbekannt" : ip.Trim();

    private void Trim(DateTime now, string keep)
    {
        if (PairGuards.Count < 64) return;
        var window = TimeSpan.FromSeconds(WindowSeconds);
        PairGuards.RemoveAll(g => !string.Equals(g.Ip, keep, StringComparison.Ordinal)
            && g.Failures == 0
            && (g.LockedUntil is null || now >= g.LockedUntil)
            && g.Attempts.TrueForAll(t => now - t >= window));
    }
}

/// <summary>Echte Systemwerte unter Linux (Pi): /proc, /sys – mit eigenem Wurzelpfad testbar.</summary>
public static class LinuxSysInfo
{
    public static string Model(string root = "/")
    {
        foreach (var f in new[] { "proc/device-tree/model", "sys/firmware/devicetree/base/model", "sys/class/dmi/id/product_name" })
        { var p = Path.Combine(root, f); try { if (File.Exists(p)) { var s = File.ReadAllText(p).Trim('\0', ' ', '\n'); if (s.Length > 0) return s; } } catch (IOException) { } catch (UnauthorizedAccessException) { } }
        return "Linux-Rechner";
    }
    public static string Kind(string model) => model.Contains("Raspberry Pi", StringComparison.OrdinalIgnoreCase) ? "Pi" : "Pc";

    public static (long TotalMb, long FreeMb) Mem(string root = "/")
    {
        var p = Path.Combine(root, "proc/meminfo"); if (!File.Exists(p)) return (0, 0);
        long V(string k) => File.ReadLines(p).Where(l => l.StartsWith(k + ":")).Select(l => long.Parse(new string(l.Where(char.IsDigit).ToArray()))).FirstOrDefault() / 1024;
        return (V("MemTotal"), V("MemAvailable"));
    }
    public static double? TempC(string root = "/")
    {
        var p = Path.Combine(root, "sys/class/thermal/thermal_zone0/temp");
        try { return File.Exists(p) && long.TryParse(File.ReadAllText(p).Trim(), out var m) ? Math.Round(m / 1000.0, 1, MidpointRounding.AwayFromZero) : null; } catch (IOException) { return null; }
    }
    public static double Uptime(string root = "/")
    {
        var p = Path.Combine(root, "proc/uptime");
        return File.Exists(p) && double.TryParse(File.ReadAllText(p).Split(' ')[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var u) ? u : 0;
    }
    public static bool GpioAvailable(string root = "/") => Directory.Exists(Path.Combine(root, "sys/class/gpio")) || File.Exists(Path.Combine(root, "dev/gpiochip0"));
}
