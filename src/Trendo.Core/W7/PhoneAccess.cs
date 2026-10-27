using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Trendo.Core;

/// <summary>„Handy verbinden – ganz einfach“ (Vorgabe 27.10.2026): QR-Code mit der normalen Handy-Kamera scannen, im Browser bestätigen, fertig.
/// Kein Entwicklermodus und keine App. Zuhause über das WLAN, unterwegs über Tailscale (verschlüsseltes privates Netz, keine offenen Ports).
/// Hier nur die Schlüssel: einmalige Kopplungscodes (10 Minuten), gekoppelte Geräte mit eigenem Schlüssel (nur als SHA-256 gespeichert).</summary>
public sealed class PhoneAccess(string dataRoot)
{
    public static readonly TimeSpan OfferLifetime = TimeSpan.FromMinutes(10);
    public const int DefaultPort = 47_823;

    public sealed record Offer(string Secret, DateTime Expires);
    public sealed record Device(string Id, string Name, string TokenHash, DateTime Paired, DateTime LastSeen);

    readonly object _lock = new();
    Offer? _offer;
    string FilePath => Path.Combine(dataRoot, "handy", "verbundene-geraete.json");
    static readonly JsonSerializerOptions J = new() { WriteIndented = true };

    /// <summary>Neuer einmaliger Kopplungscode; ein alter verfällt damit sofort.</summary>
    public Offer NewOffer(DateTime? now = null)
    {
        lock (_lock) return _offer = new Offer(Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16)), (now ?? DateTime.UtcNow) + OfferLifetime);
    }

    public Offer? CurrentOffer(DateTime? now = null)
    {
        lock (_lock) return _offer is { } o && o.Expires > (now ?? DateTime.UtcNow) ? o : null;
    }

    /// <summary>Löst den Kopplungscode ein (nur einmal): legt das Gerät an und liefert seinen Schlüssel. Null bei falschem/abgelaufenem Code.</summary>
    public (Device Device, string Token)? Redeem(string secret, string name, DateTime? now = null)
    {
        var t = now ?? DateTime.UtcNow;
        lock (_lock)
        {
            if (_offer is not { } o || o.Expires <= t || !CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(o.Secret), Encoding.ASCII.GetBytes(secret ?? ""))) return null;
            _offer = null;
            var token = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));
            var clean = new string((name ?? "").Where(ch => !char.IsControl(ch)).Take(40).ToArray()).Trim();
            var dev = new Device(Guid.NewGuid().ToString("N")[..12], clean.Length > 0 ? clean : "Handy", Hash(token), t, t);
            var all = Devices(); all.Add(dev); Save(all);
            return (dev, token);
        }
    }

    /// <summary>Prüft einen Geräteschlüssel; merkt sich „zuletzt gesehen“. Null, wenn unbekannt oder entfernt.</summary>
    public Device? Verify(string? token, DateTime? now = null)
    {
        if (string.IsNullOrEmpty(token) || token.Length != 64) return null;
        var h = Hash(token);
        lock (_lock)
        {
            var all = Devices();
            var i = all.FindIndex(d => CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(d.TokenHash), Encoding.ASCII.GetBytes(h)));
            if (i < 0) return null;
            var seen = now ?? DateTime.UtcNow;
            if (seen - all[i].LastSeen > TimeSpan.FromMinutes(1)) { all[i] = all[i] with { LastSeen = seen }; Save(all); }
            return all[i];
        }
    }

    public List<Device> Devices()
    {
        lock (_lock)
        {
            try { return File.Exists(FilePath) ? JsonSerializer.Deserialize<List<Device>>(File.ReadAllText(FilePath)) ?? [] : []; }
            catch (Exception e) when (e is JsonException or IOException) { return []; }
        }
    }

    public bool Remove(string id)
    {
        lock (_lock) { var all = Devices(); var n = all.RemoveAll(d => d.Id == id); if (n > 0) Save(all); return n > 0; }
    }

    void Save(List<Device> all)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        var tmp = FilePath + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(all, J));
        File.Move(tmp, FilePath, true);
    }

    static string Hash(string token) => Convert.ToHexStringLower(SHA256.HashData(Encoding.ASCII.GetBytes(token)));

    /// <summary>Erlaubte Adressen für den Handy-Zugang: privates Heimnetz (10/8, 172.16/12, 192.168/16) oder Tailscale (100.64.0.0/10). Nie öffentlich.</summary>
    public static bool AllowedAddress(IPAddress ip)
    {
        if (IPAddress.IsLoopback(ip)) return true;
        var b = ip.MapToIPv4().GetAddressBytes();
        if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6 && !ip.IsIPv4MappedToIPv6) return false;
        return b[0] == 10 || (b[0] == 172 && b[1] is >= 16 and <= 31) || (b[0] == 192 && b[1] == 168) || IsTailscale(ip);
    }

    public static bool IsTailscale(IPAddress ip)
    {
        var b = ip.MapToIPv4().GetAddressBytes();
        return b[0] == 100 && b[1] is >= 64 and <= 127;
    }

    /// <summary>Adresse für den QR-Code: unterwegs-tauglich (Tailscale) bevorzugt, sonst Heimnetz.</summary>
    public static string PairUrl(IPAddress host, int port, string secret) => $"http://{host}:{port}/p/{secret}";
}
