using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Trendo.Core.Licensing;

/// <summary>Lizenzarten laut Commercial-Masterplan (Entscheidungen E1–E9 vom 13.11.2026).</summary>
public enum LicenseType { Free, Complete, Trial, OwnerGrant, Partner, Team, Enterprise }

/// <summary>Vom Lizenzserver signierte Berechtigung. Der Client prüft nur mit öffentlichen Schlüsseln; private Schlüssel liegen nie im Client.</summary>
public sealed record Entitlement(
    int Format,
    string LicenseId,
    LicenseType Type,
    string Licensee,
    string? DeviceId,
    int Seats,
    DateTimeOffset IssuedAt,
    DateTimeOffset? ValidUntil,
    DateTimeOffset? UpdatesUntil,
    string[] Features,
    string KeyId)
{
    public bool AllFeatures => Features.Contains("*");
}

/// <summary>Öffentliche Schlüssel des Lizenzservers (SubjectPublicKeyInfo, Base64), je Schlüsselkennung.
/// Leer, solange der Inhaber keinen Server mit eigenem Schlüssel betreibt. Dann bleibt die Editionsprüfung aus (Vorabversion).</summary>
public static class LicenseKeys
{
    public static readonly IReadOnlyDictionary<string, string> Production = new Dictionary<string, string>();
}

/// <summary>Format „TRENDO1.&lt;nutzdaten&gt;.&lt;signatur&gt;“, beide Teile Base64url. Signatur: ECDSA P-256 mit SHA-256 (IEEE P1363) über „TRENDO1.&lt;nutzdaten&gt;“.</summary>
public sealed class LicenseVerifier(IReadOnlyDictionary<string, string> trustedKeys)
{
    public const string Prefix = "TRENDO1";
    public const int MaxTokenLength = 8192;

    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseUpper, allowIntegerValues: false) },
    };

    public bool HasKeys => trustedKeys.Count > 0;

    /// <summary>Nutzdaten im gemeinsamen Format von Client und Lizenzserver (Base64url-JSON). Enthält keine Signatur.</summary>
    public static string EncodePayload(Entitlement e) => B64Url.Encode(JsonSerializer.SerializeToUtf8Bytes(e, Json));

    public (Entitlement? Value, string Error) Verify(string token)
    {
        token = (token ?? "").Trim();
        if (token.Length == 0) return (null, "Kein Freischaltcode eingegeben.");
        if (token.Length > MaxTokenLength) return (null, "Der Freischaltcode ist zu lang.");
        var parts = token.Split('.');
        if (parts.Length != 3 || parts[0] != Prefix) return (null, "Das ist kein Trendo-Freischaltcode.");
        byte[] payload, sig;
        try { payload = B64Url.Decode(parts[1]); sig = B64Url.Decode(parts[2]); }
        catch (FormatException) { return (null, "Der Freischaltcode ist beschädigt."); }

        Entitlement? e;
        try { e = JsonSerializer.Deserialize<Entitlement>(payload, Json); }
        catch (JsonException) { return (null, "Der Freischaltcode ist beschädigt."); }
        if (e is null || e.Format != 1 || string.IsNullOrWhiteSpace(e.LicenseId) || e.Features is null || string.IsNullOrWhiteSpace(e.KeyId))
            return (null, "Der Freischaltcode hat ein unbekanntes Format.");
        if (!trustedKeys.TryGetValue(e.KeyId, out var spki)) return (null, "Der Freischaltcode stammt nicht vom Trendo-Lizenzserver.");

        using var ec = ECDsa.Create();
        try { ec.ImportSubjectPublicKeyInfo(Convert.FromBase64String(spki), out _); }
        catch (Exception ex) when (ex is FormatException or CryptographicException) { return (null, "Der hinterlegte Prüfschlüssel ist ungültig."); }
        if (ec.KeySize != 256) return (null, "Der hinterlegte Prüfschlüssel ist ungültig.");
        var signed = Encoding.ASCII.GetBytes(Prefix + "." + parts[1]);
        if (!ec.VerifyData(signed, sig, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation))
            return (null, "Die Signatur des Freischaltcodes stimmt nicht.");
        return (e, "");
    }
}

public static class B64Url
{
    public static string Encode(byte[] b) => Convert.ToBase64String(b).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public static byte[] Decode(string s)
    {
        if (s.Length == 0 || s.Any(c => !(char.IsAsciiLetterOrDigit(c) || c is '-' or '_'))) throw new FormatException();
        var t = s.Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(t.PadRight(t.Length + (4 - t.Length % 4) % 4, '='));
    }
}

/// <summary>Welche Studios ohne Kauf nutzbar sind (Entscheidung E2: ausgewählte Studios frei, Rest Complete).
/// Die Liste ist ein Vorschlag; der Inhaber legt sie vor dem Verkauf fest. System- und Einstellungsseiten sind keine Studios und bleiben immer frei.</summary>
public static class EditionCatalog
{
    public static readonly IReadOnlySet<string> FreeStudios = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "STUDIO-LETTER-01", "SYS-10", "SYS-09", "X-11", "SYS-02", "SPEC-06", "SPEC-13", "SPEC-16", "CORE-10", "STUDIO-SUMMARY-01", "X-07",
    };

    public static string Label(LicenseType t) => t switch
    {
        LicenseType.Free => "Free",
        LicenseType.Complete => "Complete",
        LicenseType.Trial => "Complete (Test)",
        LicenseType.OwnerGrant => "Complete (geschenkt)",
        LicenseType.Partner => "Partner",
        LicenseType.Team => "Team",
        LicenseType.Enterprise => "Enterprise",
        _ => t.ToString(),
    };
}

/// <summary>Ergebnis der Editionsprüfung für dieses Gerät.</summary>
public sealed record EditionStatus(LicenseType Type, bool Enforced, bool Valid, string Headline, string Detail, Entitlement? Entitlement)
{
    public bool Allows(string studioId)
    {
        if (!Enforced) return true;
        if (EditionCatalog.FreeStudios.Contains(studioId)) return true;
        if (!Valid || Entitlement is null || Type == LicenseType.Free) return false;
        return Entitlement.AllFeatures || Entitlement.Features.Contains(studioId, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Deckt die Berechtigung ein Update mit diesem Veröffentlichungsdatum ab? Ohne Enddatum: ja.</summary>
    public bool UpdatesCover(DateTimeOffset releaseDate) =>
        !Enforced || (Valid && Entitlement is { } e && (e.UpdatesUntil is null || releaseDate <= e.UpdatesUntil));
}

public static class EditionPolicy
{
    /// <summary>Uhr gilt als zurückgestellt, wenn sie mehr als einen Tag vor dem zuletzt gesehenen Zeitpunkt steht.</summary>
    public static readonly TimeSpan ClockTolerance = TimeSpan.FromDays(1);

    public static EditionStatus Evaluate(Entitlement? e, string deviceId, DateTimeOffset now, DateTimeOffset? lastSeen, bool enforced)
    {
        if (!enforced)
            return new(e?.Type ?? LicenseType.Free, false, true, "Vorabversion: alle Studios frei",
                "Der Verkauf ist noch nicht gestartet. Bis dahin sind alle Studios ohne Lizenz nutzbar.", e);
        if (e is null)
            return Free("Ohne Lizenz sind die freien Studios nutzbar. Mit der Vollversion werden alle Studios freigeschaltet.");
        if (e.DeviceId is { } d && !string.Equals(d, deviceId, StringComparison.Ordinal))
            return Free("Diese Lizenz gehört zu einem anderen Gerät. Bitte im Konto dieses Gerät freischalten.", e);
        if (e.ValidUntil is not null && lastSeen is { } seen && now < seen - ClockTolerance)
            return Free("Die Uhr dieses Geräts steht zurück. Zeitlich begrenzte Lizenzen gelten erst wieder mit richtiger Uhrzeit.", e);
        if (e.ValidUntil is { } end && now > end)
            return Free($"Die Lizenz ist am {end.LocalDateTime:dd.MM.yyyy} abgelaufen.", e);
        if (e.Type == LicenseType.Free) return Free("Free Edition.", e);
        var until2 = e.ValidUntil is { } v ? $" bis {v.LocalDateTime:dd.MM.yyyy}" : "";
        var upd = e.UpdatesUntil is { } u ? $" Updates bis {u.LocalDateTime:dd.MM.yyyy}." : "";
        return new(e.Type, true, true, $"Trendo OS {EditionCatalog.Label(e.Type)}", $"Gültig{until2}.{upd}", e);
    }

    static EditionStatus Free(string detail, Entitlement? e = null) => new(LicenseType.Free, true, e is null || e.Type == LicenseType.Free, "Trendo OS Free", detail, e);
}

/// <summary>Speichert den Freischaltcode dieses Geräts unter &lt;DataRoot&gt;/lizenz. Zufällige Geräte-Kennung, kein Hardware-Fingerabdruck.</summary>
public sealed class LicenseStore
{
    private readonly string _dir;
    private readonly LicenseVerifier _verifier;
    private readonly AuditLog _audit;

    public LicenseStore(string dataRoot, LicenseVerifier verifier, AuditLog audit)
    {
        _dir = Path.Combine(dataRoot, "lizenz");
        _verifier = verifier;
        _audit = audit;
    }

    public bool Enforced => _verifier.HasKeys;
    private string TokenFile => Path.Combine(_dir, "freischaltcode.txt");
    private string DeviceFile => Path.Combine(_dir, "geraet.txt");
    private string SeenFile => Path.Combine(_dir, "zuletzt.txt");

    public string DeviceId
    {
        get
        {
            if (File.Exists(DeviceFile) && File.ReadAllText(DeviceFile).Trim() is { Length: 32 } id && id.All(char.IsAsciiHexDigitLower)) return id;
            Directory.CreateDirectory(_dir);
            var fresh = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16));
            File.WriteAllText(DeviceFile, fresh);
            return fresh;
        }
    }

    public Entitlement? Current()
    {
        if (!File.Exists(TokenFile)) return null;
        var (e, _) = _verifier.Verify(File.ReadAllText(TokenFile));
        return e;
    }

    public EditionStatus Status(DateTimeOffset now)
    {
        DateTimeOffset? seen = File.Exists(SeenFile) && DateTimeOffset.TryParse(File.ReadAllText(SeenFile).Trim(), System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var s) ? s : null;
        var st = EditionPolicy.Evaluate(Current(), DeviceId, now, seen, Enforced);
        if (seen is null || now > seen) { Directory.CreateDirectory(_dir); File.WriteAllText(SeenFile, now.ToString("O", System.Globalization.CultureInfo.InvariantCulture)); }
        return st;
    }

    /// <summary>Prüft und speichert einen Freischaltcode. Gespeichert wird nur, was die Signaturprüfung besteht und zu diesem Gerät passt.</summary>
    public (bool Ok, string Message) Install(string token, string by)
    {
        var (e, err) = _verifier.Verify(token);
        if (e is null) { _audit.Append(by, "license.rejected", err); return (false, err); }
        if (e.DeviceId is { } d && d != DeviceId) { _audit.Append(by, "license.rejected", "anderes Gerät"); return (false, "Dieser Freischaltcode gehört zu einem anderen Gerät."); }
        Directory.CreateDirectory(_dir);
        File.WriteAllText(TokenFile, token.Trim());
        _audit.Append(by, "license.installed", $"{e.LicenseId} {e.Type}");
        return (true, $"Freigeschaltet: Trendo OS {EditionCatalog.Label(e.Type)}.");
    }

    public void Remove(string by)
    {
        if (!File.Exists(TokenFile)) return;
        File.Delete(TokenFile);
        _audit.Append(by, "license.removed", "");
    }
}
