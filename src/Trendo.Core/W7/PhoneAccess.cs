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
    /// <summary>Gekoppeltes Handy. <paramref name="Remote"/>: darf Trendo vollständig bedienen (nur wenn am PC erlaubt).
    /// <paramref name="More"/>: weitere Schlüssel desselben Handys (z. B. für die Unterwegs-Adresse über Tailscale).</summary>
    public sealed record Device(string Id, string Name, string TokenHash, DateTime Paired, DateTime LastSeen, bool Remote = false, List<string>? More = null);

    /// <summary>Netzwerkkarte des PCs für die Adresswahl im QR-Code.</summary>
    public sealed record Nic(IPAddress Ip, string Name, bool Gateway, bool Virtual, bool Wireless);

    readonly object _lock = new();
    Offer? _offer;
    readonly Dictionary<string, (string DeviceId, DateTime Expires)> _transfers = [];

    /// <summary>Zuletzt hat ein Handy die Kopplungsseite geöffnet (für den Assistenten: „Handy hat den PC erreicht“).</summary>
    public (DateTime At, IPAddress From)? LastReach { get; private set; }
    public void MarkReached(IPAddress from, DateTime? now = null) { lock (_lock) LastReach = (now ?? DateTime.UtcNow, from); }
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
            var i = all.FindIndex(d => Same(d.TokenHash, h) || (d.More?.Any(m => Same(m, h)) ?? false));
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

    static bool Same(string a, string b) => CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(a), Encoding.ASCII.GetBytes(b));

    /// <summary>Darf das Handy Trendo vollständig bedienen (Bildschirm sehen, tippen, schreiben)? Nur der PC-Nutzer schaltet das ein.</summary>
    public bool SetRemote(string id, bool on)
    {
        lock (_lock)
        {
            var all = Devices(); var i = all.FindIndex(d => d.Id == id);
            if (i < 0) return false;
            all[i] = all[i] with { Remote = on }; Save(all); return true;
        }
    }

    /// <summary>Unterwegs-Zugang: ein bereits verbundenes Handy holt sich einen Übergabe-Code (2 Minuten, einmalig) für die Tailscale-Adresse.
    /// Dort ist es eine andere Web-Adresse, deshalb bekommt das Handy dafür einen zweiten eigenen Schlüssel.</summary>
    public string NewTransfer(string deviceId, DateTime? now = null)
    {
        var code = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16));
        lock (_lock)
        {
            var t = now ?? DateTime.UtcNow;
            foreach (var k in _transfers.Where(x => x.Value.Expires <= t).Select(x => x.Key).ToList()) _transfers.Remove(k);
            _transfers[code] = (deviceId, t + TimeSpan.FromMinutes(2));
        }
        return code;
    }

    public (Device Device, string Token)? RedeemTransfer(string code, DateTime? now = null)
    {
        lock (_lock)
        {
            if (code is null || !_transfers.Remove(code, out var tr) || tr.Expires <= (now ?? DateTime.UtcNow)) return null;
            var all = Devices(); var i = all.FindIndex(d => d.Id == tr.DeviceId);
            if (i < 0) return null;
            var token = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));
            all[i] = all[i] with { More = [.. (all[i].More ?? []).TakeLast(3), Hash(token)] };
            Save(all);
            return (all[i], token);
        }
    }

    /// <summary>Beste Heimnetz-Adressen für den QR-Code: echte WLAN-/LAN-Karte mit Router zuerst; virtuelle Karten (Hyper-V, WSL, VPN) und Tailscale zuletzt.
    /// Befund 27.10.2026: Der Code zeigte manchmal die Adresse einer virtuellen Karte oder Tailscale – das Handy im WLAN kam dann nie an.</summary>
    public static IReadOnlyList<IPAddress> HomeAddresses(IEnumerable<Nic> nics) =>
        nics.Where(n => !IsTailscale(n.Ip) && !IPAddress.IsLoopback(n.Ip) && AllowedAddress(n.Ip))
            .OrderBy(n => n.Virtual).ThenByDescending(n => n.Gateway).ThenByDescending(n => n.Wireless)
            .ThenByDescending(n => n.Ip.GetAddressBytes()[0] == 192).Select(n => n.Ip).Distinct().ToList();

    /// <summary>Virtuelle Netzwerkkarten (vom Namen her), die ein Handy nie erreicht.</summary>
    public static bool LooksVirtual(string description) =>
        new[] { "Hyper-V", "vEthernet", "Virtual", "VMware", "VirtualBox", "WSL", "TAP-", "Npcap", "Loopback", "Bluetooth", "Docker", "WireGuard", "Wintun" }
            .Any(v => description.Contains(v, StringComparison.OrdinalIgnoreCase));

    /// <summary>Windows-Freigabe für den Handy-Zugang (eine Regel, nur dieser Anschluss, nur Heimnetz und Tailscale – auch wenn Windows das WLAN „Öffentlich“ nennt).</summary>
    public static string[] FirewallArgs(int port) =>
        ["advfirewall", "firewall", "add", "rule", "name=Trendo Handy", "dir=in", "action=allow", "protocol=TCP", $"localport={port}", "profile=any",
         "remoteip=localsubnet,100.64.0.0/10"];

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
