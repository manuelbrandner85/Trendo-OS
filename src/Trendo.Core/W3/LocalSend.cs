using System.Net;
using System.Net.Sockets;
using System.Text.Json.Nodes;

namespace Trendo.Core;

/// <summary>Alle Studios, Runde 9 – SYS-06 „Dateien teilen“: Dateien direkt ans Handy senden über das offene LocalSend-Protokoll (v2, Beschreibung github.com/localsend/protocol;
/// LocalSend selbst Apache-2.0, die App läuft auf dem Handy). Eigener Code nach der Beschreibung, nur die Sendeseite. Hier nur die Protokoll-Logik ohne Netz; das Netz macht die App.</summary>
public static class LocalSend
{
    public const string MulticastAddress = "224.0.0.167", Version = "2.0", Alias = "Trendo OS";
    public const int Port = 53317;

    public sealed record Peer(string Alias, string Address, int Port, string Protocol, string Fingerprint, string DeviceModel, string DeviceType)
    {
        public string BaseUrl => $"{Protocol}://{(Address.Contains(':') ? "[" + Address + "]" : Address)}:{Port}";
        public string Label => DeviceModel.Length > 0 ? $"{Alias} ({DeviceModel})" : Alias;
    }

    public sealed record SendFile(string Id, string Path, string FileName, long Size, string Sha256, string FileType);

    /// <summary>Nur eigenes Netz: private Adressen, Link-Local und dieser PC – nie etwas aus dem Internet.</summary>
    public static bool IsLocal(IPAddress ip)
    {
        if (IPAddress.IsLoopback(ip)) return true;
        if (ip.IsIPv4MappedToIPv6) ip = ip.MapToIPv4();
        if (ip.AddressFamily == AddressFamily.InterNetworkV6) return ip.IsIPv6LinkLocal || ip.IsIPv6UniqueLocal;
        var b = ip.GetAddressBytes();
        return b[0] == 10 || (b[0] == 172 && b[1] is >= 16 and <= 31) || (b[0] == 192 && b[1] == 168) || (b[0] == 169 && b[1] == 254);
    }

    static JsonObject Info(string fingerprint, bool announce) => new()
    {
        ["alias"] = Alias, ["version"] = Version, ["deviceModel"] = "PC", ["deviceType"] = "desktop", ["fingerprint"] = fingerprint, ["port"] = Port, ["protocol"] = "http", ["download"] = false, ["announce"] = announce,
    };

    /// <summary>Ankündigung im Netz; Geräte antworten darauf (per UDP, weil Trendo als reiner Sender keinen Server öffnet).</summary>
    public static string Announcement(string fingerprint) => Info(fingerprint, true).ToJsonString();

    /// <summary>UDP-Nachricht eines Geräts lesen; eigene Nachrichten, fremde Netze und Unsinn ergeben null.</summary>
    public static Peer? ParsePeer(string json, IPAddress from, string ownFingerprint)
    {
        if (!IsLocal(from)) return null;
        JsonNode? j; try { j = JsonNode.Parse(json); } catch (System.Text.Json.JsonException) { return null; }
        if (j is not JsonObject o) return null;
        string S(string k) => o[k] is JsonValue v && v.TryGetValue<string>(out var s) ? s : "";
        var fp = S("fingerprint"); var alias = S("alias"); var protocol = S("protocol").Length == 0 ? "https" : S("protocol");
        var port = o["port"] is JsonValue pv && pv.TryGetValue<int>(out var p) ? p : Port;
        if (fp.Length == 0 || alias.Length == 0 || fp == ownFingerprint || protocol is not ("http" or "https") || port is < 1 or > 65535) return null;
        if (!S("version").StartsWith("2.", StringComparison.Ordinal)) return null;
        return new(alias.Length > 60 ? alias[..60] : alias, (from.IsIPv4MappedToIPv6 ? from.MapToIPv4() : from).ToString(), port, protocol, fp, S("deviceModel"), S("deviceType"));
    }

    /// <summary>Metadaten für /prepare-upload (Dateinamen ohne Pfad).</summary>
    public static string PrepareBody(string fingerprint, IReadOnlyList<SendFile> files)
    {
        var info = Info(fingerprint, false); info.Remove("announce");
        var map = new JsonObject();
        foreach (var f in files) map[f.Id] = new JsonObject { ["id"] = f.Id, ["fileName"] = f.FileName, ["size"] = f.Size, ["fileType"] = f.FileType, ["sha256"] = f.Sha256 };
        return new JsonObject { ["info"] = info, ["files"] = map }.ToJsonString();
    }

    public static string PrepareUrl(Peer p, string? pin) => p.BaseUrl + "/api/localsend/v2/prepare-upload" + (string.IsNullOrWhiteSpace(pin) ? "" : "?pin=" + Uri.EscapeDataString(pin.Trim()));
    public static string UploadUrl(Peer p, string session, string fileId, string token) =>
        $"{p.BaseUrl}/api/localsend/v2/upload?sessionId={Uri.EscapeDataString(session)}&fileId={Uri.EscapeDataString(fileId)}&token={Uri.EscapeDataString(token)}";
    public static string CancelUrl(Peer p, string session) => $"{p.BaseUrl}/api/localsend/v2/cancel?sessionId={Uri.EscapeDataString(session)}";

    /// <summary>Antwort von /prepare-upload: Sitzung und Token je Datei.</summary>
    public static (string Session, Dictionary<string, string> Tokens) ParsePrepare(string json)
    {
        var o = JsonNode.Parse(json)?.AsObject() ?? throw new FormatException("Keine Antwort.");
        var session = o["sessionId"]?.GetValue<string>() ?? throw new FormatException("Keine Sitzung.");
        var t = new Dictionary<string, string>();
        foreach (var (k, v) in o["files"]?.AsObject() ?? []) t[k] = v!.GetValue<string>();
        return (session, t);
    }

    /// <summary>HTTP-Antworten laut Protokoll verständlich.</summary>
    public static string Error(int status) => status switch
    {
        204 => "Das Gerät braucht die Dateien nicht (schon vorhanden).",
        401 => "Das Handy verlangt eine PIN – bitte eingeben.",
        403 => "Das Handy hat die Übertragung abgelehnt.",
        409 => "Das Handy ist gerade mit einer anderen Übertragung beschäftigt.",
        422 => "Die Prüfsumme stimmt nicht – die Datei kam beschädigt an.",
        429 => "Zu viele Versuche – bitte kurz warten.",
        _ => $"Das Gerät meldet einen Fehler (HTTP {status}).",
    };

    /// <summary>HTTPS: Das Zertifikat muss zum im Netz angekündigten Fingerabdruck passen (SHA-256 des Zertifikats).</summary>
    public static bool CertificateMatches(byte[] certificate, string fingerprint) =>
        Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(certificate)).Equals(fingerprint.Replace(":", "").Trim(), StringComparison.OrdinalIgnoreCase);

    public static string FileType(string name) => Path.GetExtension(name).ToLowerInvariant() switch
    {
        ".jpg" or ".jpeg" => "image/jpeg", ".png" => "image/png", ".gif" => "image/gif", ".webp" => "image/webp", ".heic" => "image/heic",
        ".mp4" => "video/mp4", ".mov" => "video/quicktime", ".mkv" => "video/x-matroska", ".mp3" => "audio/mpeg", ".m4a" => "audio/mp4", ".wav" => "audio/wav",
        ".pdf" => "application/pdf", ".txt" => "text/plain", ".zip" => "application/zip", ".apk" => "application/vnd.android.package-archive", _ => "application/octet-stream",
    };
}
