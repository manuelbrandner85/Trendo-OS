using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Trendo.Core;

public sealed record PairRequest(string Code, string PcName);
public sealed record PairResponse(string Token, string Name, string Kind, List<string> Caps, string Version);
public sealed record CompanionHealth(string App, string Version, string Name, string Kind);
public sealed record CompanionInfo(string Name, string Kind, string Model, string Os, int Cpus, long MemTotalMb, long MemFreeMb, double? TempC, double Uptime, List<string> Caps);
public sealed record OcrResult(string Text, double Confidence, List<string> Unreadable);

/// <summary>Zustand der Gegenstelle (Pi/Linux): Kopplungscode, gekoppelte PCs (nur Token-Hashes).</summary>
public sealed class CompanionState
{
    public string Name { get; set; } = Environment.MachineName;
    public string? Code { get; set; }
    public DateTime CodeExpires { get; set; }
    public int FailedTries { get; set; }
    public List<PairedPc> Pcs { get; set; } = [];
    public sealed record PairedPc(string Name, string TokenSha256, DateTime At);

    private static readonly JsonSerializerOptions J = new() { WriteIndented = true };
    public static CompanionState Load(string file) { try { return File.Exists(file) ? JsonSerializer.Deserialize<CompanionState>(File.ReadAllText(file)) ?? new() : new(); } catch (JsonException) { return new(); } }
    public void Save(string file)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(file))!);
        var tmp = file + ".tmp"; File.WriteAllText(tmp, JsonSerializer.Serialize(this, J));
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(tmp, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        File.Move(tmp, file, true);
    }

    public string NewCode(DateTime now) { Code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6"); CodeExpires = now.AddMinutes(10); FailedTries = 0; return Code; }

    public static string Sha(string s) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(s)));

    /// <summary>Einmal-Code prüfen; nach 5 Fehlversuchen ist der Code verbrannt. Liefert Token (nur einmal sichtbar).</summary>
    public (string? Token, string Error) TryPair(string code, string pcName, DateTime now)
    {
        if (Code is null || now > CodeExpires) return (null, "kein gültiger Code – auf dem Gerät „trendo-companion pair“ ausführen");
        if (!CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(Code), Encoding.ASCII.GetBytes((code ?? "").Trim().PadRight(6)[..6])))
        { if (++FailedTries >= 5) Code = null; return (null, "Code falsch"); }
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var n = string.IsNullOrWhiteSpace(pcName) ? "PC" : pcName.Trim()[..Math.Min(40, pcName.Trim().Length)];
        Pcs.Add(new(n, Sha(token), now)); Code = null; FailedTries = 0;
        return (token, "");
    }

    public bool Authorized(string? authHeader)
    {
        if (authHeader is null || !authHeader.StartsWith("Bearer ")) return false;
        var h = Encoding.ASCII.GetBytes(Sha(authHeader[7..].Trim()));
        return Pcs.Any(p => CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(p.TokenSha256), h));
    }

    public bool Unpair(string authHeader) { var h = Sha(authHeader[7..].Trim()); return Pcs.RemoveAll(p => p.TokenSha256 == h) > 0; }
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
