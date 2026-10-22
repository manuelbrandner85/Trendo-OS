using System.Text.Json;
using System.Text.RegularExpressions;

namespace Trendo.Core;

public sealed record MdnsService(string Name, string Type, string Host, int Port);

/// <summary>Einmalige QR-Kopplung. Der Code bleibt im Speicher, bis die Kopplung fertig ist oder verworfen wird.</summary>
public sealed record WlanPairing(string ServiceName, string Password, string QrPayload);

public sealed record KnownPhone(string Id, string Model, string Host, int Port, string? Guid, DateTime LastSeen);

/// <summary>WLAN-Debugging ab Android 11: QR im Format von Android, danach adb pair und adb connect. Es wird kein Port geöffnet.</summary>
public static class WirelessAdb
{
    public const string PairingType = "_adb-tls-pairing._tcp";
    public const string ConnectType = "_adb-tls-connect._tcp";
    public const string LegacyType = "_adb._tcp";

    public static IReadOnlyList<string> PhoneSteps { get; } =
    [
        "Einstellungen öffnen und siebenmal auf „Build-Nummer“ tippen.",
        "Entwickleroptionen öffnen und „Drahtloses Debuggen“ einschalten.",
        "Für den QR-Code: „Gerät mit QR-Code koppeln“ tippen und den Code hier scannen.",
        "Für den Zahlencode: „Gerät mit Kopplungscode koppeln“ tippen. IP, Port und Code hier eintragen.",
        "Handy und dieser PC müssen im selben WLAN sein.",
    ];

    public static WlanPairing CreateSession(Random? random = null)
    {
        var rng = random ?? Random.Shared;
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
        var tail = new char[10];
        for (var i = 0; i < tail.Length; i++) tail[i] = alphabet[rng.Next(alphabet.Length)];
        var name = "studio-" + new string(tail);
        var password = new char[10];
        for (var i = 0; i < password.Length; i++) password[i] = (char)('0' + rng.Next(10));
        var code = new string(password);
        return new WlanPairing(name, code, QrPayload(name, code));
    }

    /// <summary>Android liest WIFI:T:ADB;S:&lt;Name&gt;;P:&lt;Code&gt;;; und startet damit selbst den Kopplungsdienst.</summary>
    public static string QrPayload(string service, string password)
    {
        if (!Regex.IsMatch(service, @"^studio-[A-Za-z0-9]{8,16}$")) throw new ArgumentException("Dienstname ungültig.");
        if (!Regex.IsMatch(password, @"^\d{10}$")) throw new ArgumentException("QR-Code ungültig.");
        return $"WIFI:T:ADB;S:{service};P:{password};;";
    }

    public static IReadOnlyList<MdnsService> ParseMdns(string output)
    {
        var list = new List<MdnsService>();
        foreach (var raw in output.Split('\n'))
        {
            var m = Regex.Match(raw.Trim(), @"^(?<name>\S+)\s+(?<type>_adb(?:-tls-(?:pairing|connect))?\._tcp)\s+(?<host>(?:\d{1,3}\.){3}\d{1,3}):(?<port>\d{1,5})$");
            if (!m.Success) continue;
            if (!int.TryParse(m.Groups["port"].Value, out var port) || port is < 1 or > 65535) continue;
            list.Add(new MdnsService(m.Groups["name"].Value, m.Groups["type"].Value, m.Groups["host"].Value, port));
        }
        return list;
    }

    public static (bool Ok, string Args, string Message) PairArgs(string host, int port, string code, bool consent)
    {
        if (!consent) return (false, "", "Erst zustimmen.");
        if (!FusionStore.IsLanAddress(host)) return (false, "", "Nur Adressen im Heimnetz sind erlaubt.");
        if (port is < 1024 or > 65535) return (false, "", "Port ungültig.");
        if (!Regex.IsMatch(code, @"^\d{6}$") && !Regex.IsMatch(code, @"^\d{10}$"))
            return (false, "", "Der Kopplungscode hat 6 Stellen (Zahlencode am Handy) oder 10 Stellen (QR-Code).");
        return (true, $"pair {host}:{port} {code}", $"Kopple {host}");
    }

    public static (bool Ok, string Args, string Message) ConnectArgs(string host, int port, bool consent)
    {
        if (!consent) return (false, "", "Erst zustimmen.");
        if (!FusionStore.IsLanAddress(host)) return (false, "", "Nur Adressen im Heimnetz sind erlaubt.");
        if (port is < 1024 or > 65535) return (false, "", "Port ungültig.");
        return (true, $"connect {host}:{port}", $"Verbinde {host}");
    }

    public static bool PairOk(int exit, string output) => exit == 0 && output.Contains("Successfully paired", StringComparison.OrdinalIgnoreCase);

    public static bool ConnectOk(int exit, string output) =>
        exit == 0 && output.Contains("connected", StringComparison.OrdinalIgnoreCase) && !output.Contains("failed", StringComparison.OrdinalIgnoreCase);

    public static string? WifiGuid(string name)
    {
        var m = Regex.Match(name, @"^adb-([A-Za-z0-9]{4,40})");
        return m.Success ? m.Groups[1].Value : null;
    }

    /// <summary>Lieber den frischen mDNS-Eintrag als die gemerkte Adresse. Der Port am Handy wechselt.</summary>
    public static (bool Ok, string Host, int Port, string Message) PickReconnect(KnownPhone phone, IReadOnlyList<MdnsService> services, bool consent)
    {
        if (!consent) return (false, "", 0, "Erst zustimmen.");
        var hits = services.Where(s => s.Type is ConnectType or LegacyType && (
            (phone.Guid is { Length: > 0 } g && s.Name.Contains(g, StringComparison.Ordinal)) || s.Host == phone.Host)).ToList();
        var hit = hits.FirstOrDefault(s => s.Type == ConnectType) ?? hits.FirstOrDefault();
        if (hit is not null)
        {
            if (!FusionStore.IsLanAddress(hit.Host) || hit.Port is < 1024 or > 65535)
                return (false, "", 0, "Gefundene Adresse liegt nicht im Heimnetz.");
            return (true, hit.Host, hit.Port, "Handy im WLAN wiedergefunden.");
        }
        if (FusionStore.IsLanAddress(phone.Host) && phone.Port is >= 1024 and <= 65535)
            return (true, phone.Host, phone.Port, "Letzte Adresse. Wenn es nicht klappt, ist der Port am Handy neu – dann noch einmal koppeln.");
        return (false, "", 0, "Handy nicht im WLAN gefunden. Ist „Drahtloses Debuggen“ an?");
    }
}

/// <summary>Gemerkte WLAN-Handys. Ohne Kopplungscode und ohne Schlüssel.</summary>
public sealed class KnownPhones(string dataRoot)
{
    private string FilePath => Path.Combine(dataRoot, "handy", "bekannte.json");
    private static readonly JsonSerializerOptions J = new() { WriteIndented = true };

    public List<KnownPhone> All()
    {
        try { return File.Exists(FilePath) ? JsonSerializer.Deserialize<List<KnownPhone>>(File.ReadAllText(FilePath)) ?? [] : []; }
        catch (JsonException) { return []; }
    }

    public void Remember(string model, string host, int port, string? guid, DateTime now)
    {
        if (!FusionStore.IsLanAddress(host) || port is < 1024 or > 65535) return;
        model = model.Replace('\n', ' ').Trim();
        if (model.Length == 0) model = "Android-Handy";
        if (model.Length > 40) model = model[..40];
        var id = string.IsNullOrEmpty(guid) ? host : guid;
        var all = All();
        all.RemoveAll(x => x.Id == id || x.Host == host);
        all.Add(new KnownPhone(id, model, host, port, string.IsNullOrEmpty(guid) ? null : guid, now));
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(all, J));
    }

    public bool Remove(string id)
    {
        var all = All();
        if (all.RemoveAll(x => x.Id == id) == 0) return false;
        File.WriteAllText(FilePath, JsonSerializer.Serialize(all, J));
        return true;
    }
}
