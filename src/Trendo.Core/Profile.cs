using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Trendo.Core.Sys;

namespace Trendo.Core;

/// <summary>Wunsch 27.10.2026 (B2): Anmeldung an Trendo – PIN, Passwort oder ohne. Alles lokal, kein Online-Konto.
/// Gespeichert werden nur PBKDF2-Prüfwerte (SHA-256, 210 000 Runden, eigenes Salz), nie PIN oder Passwort selbst.
/// PIN vergessen: Wiederherstellungs-Code (bei der Einrichtung einmal gezeigt), Windows-Anmeldung oder ein Erwachsener aus der Familie.
/// Die Sperre schützt den Zugang zu Trendo – Daten gehen beim Zurücksetzen nie verloren.</summary>
public sealed class ProfileLock(string dataRoot)
{
    public enum Kind { None, Pin, Password }
    sealed record Stored(Kind Kind, string Salt, string Hash, string RecoverySalt, string RecoveryHash, int Rounds);
    const int Rounds = 210_000;
    string FileOf => Path.Combine(dataRoot, "profil", "anmeldung.json");
    int _fails;
    DateTime _waitUntil;

    public Kind Current => Load()?.Kind ?? Kind.None;
    public bool Enabled => Current != Kind.None;

    Stored? Load()
    {
        try { return File.Exists(FileOf) ? JsonSerializer.Deserialize<Stored>(File.ReadAllText(FileOf)) : null; }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException) { return null; }
    }

    static string Hash(string secret, byte[] salt, int rounds) => Convert.ToHexStringLower(Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(secret), salt, rounds, HashAlgorithmName.SHA256, 32));

    /// <summary>Prüft, ob eine PIN/ein Passwort den Mindestregeln entspricht (null = in Ordnung, sonst Hinweis).</summary>
    public static string? Problem(Kind kind, string secret) => kind switch
    {
        Kind.Pin when secret.Length is < 4 or > 12 || !secret.All(char.IsAsciiDigit) => "Die PIN braucht 4 bis 12 Ziffern.",
        Kind.Pin when secret.Distinct().Count() == 1 || "0123456789012".Contains(secret) || "9876543210987".Contains(secret) => "Bitte keine ganz einfache PIN wie 1111 oder 1234.",
        Kind.Password when secret.Length < 8 => "Das Passwort braucht mindestens 8 Zeichen.",
        _ => null,
    };

    /// <summary>Anmeldung festlegen. Liefert den neuen Wiederherstellungs-Code (nur jetzt sichtbar) – bei „ohne“ null.</summary>
    public string? Set(Kind kind, string secret = "")
    {
        if (kind == Kind.None) { if (File.Exists(FileOf)) File.Delete(FileOf); return null; }
        if (Problem(kind, secret) is { } p) throw new ArgumentException(p);
        var code = NewRecoveryCode();
        var salt = RandomNumberGenerator.GetBytes(16); var rsalt = RandomNumberGenerator.GetBytes(16);
        Directory.CreateDirectory(Path.GetDirectoryName(FileOf)!);
        File.WriteAllText(FileOf, JsonSerializer.Serialize(new Stored(kind, Convert.ToHexStringLower(salt), Hash(secret, salt, Rounds), Convert.ToHexStringLower(rsalt), Hash(Normalize(code), rsalt, Rounds), Rounds)));
        _fails = 0;
        return code;
    }

    /// <summary>Nach 5 Fehlversuchen kurz warten (30 s, dann länger) – gegen Durchprobieren.</summary>
    public TimeSpan WaitLeft(DateTime? now = null) => _waitUntil > (now ?? DateTime.UtcNow) ? _waitUntil - (now ?? DateTime.UtcNow) : TimeSpan.Zero;

    public bool Verify(string secret, DateTime? now = null)
    {
        if (Load() is not { } s) return true;
        if (WaitLeft(now) > TimeSpan.Zero) return false;
        var ok = CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(Hash(secret, Convert.FromHexString(s.Salt), s.Rounds)), Encoding.ASCII.GetBytes(s.Hash));
        if (ok) { _fails = 0; return true; }
        if (++_fails >= 5) _waitUntil = (now ?? DateTime.UtcNow) + TimeSpan.FromSeconds(30 * Math.Pow(2, Math.Min(6, _fails - 5)));
        return false;
    }

    /// <summary>PIN vergessen: Wiederherstellungs-Code prüfen (Groß/Klein, Leerzeichen und Striche egal).</summary>
    public bool VerifyRecovery(string code, DateTime? now = null)
    {
        if (Load() is not { } s) return false;
        if (WaitLeft(now) > TimeSpan.Zero) return false;
        var ok = CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(Hash(Normalize(code), Convert.FromHexString(s.RecoverySalt), s.Rounds)), Encoding.ASCII.GetBytes(s.RecoveryHash));
        if (!ok && ++_fails >= 5) _waitUntil = (now ?? DateTime.UtcNow) + TimeSpan.FromSeconds(30 * Math.Pow(2, Math.Min(6, _fails - 5)));
        return ok;
    }

    static string Normalize(string code) => new string(code.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();

    /// <summary>16 Zeichen in 4er-Gruppen, ohne verwechselbare Zeichen (0/O, 1/I/L).</summary>
    public static string NewRecoveryCode()
    {
        const string abc = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";
        var b = RandomNumberGenerator.GetBytes(16);
        var s = new string(b.Select(x => abc[x % abc.Length]).ToArray());
        return $"{s[..4]}-{s[4..8]}-{s[8..12]}-{s[12..]}";
    }
}

/// <summary>B7: weitere Personen am selben PC (Familie, Kinder). Jede Person hat einen eigenen Trendo-Datenordner
/// („personen/&lt;id&gt;“ neben dem Hauptordner) mit eigenen Einstellungen, Projekten und eigener Anmeldung.</summary>
public sealed class PersonStore(string baseRoot)
{
    public sealed record Person(string Id, string Name, string Avatar, bool Child, DateTime Created);
    string FileOf => Path.Combine(baseRoot, "personen.json");

    /// <summary>Hauptordner zu einem Datenordner (Personen liegen in „personen/&lt;id&gt;“).</summary>
    public static string BaseOf(string dataRoot)
    {
        var parent = Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(dataRoot));
        return parent is not null && Path.GetFileName(parent) == "personen" ? Path.GetDirectoryName(parent)! : dataRoot;
    }

    public string RootOf(string id) => id == "haupt" ? baseRoot : Path.Combine(baseRoot, "personen", id);

    /// <summary>Kennung der Person zu einem Datenordner („personen/&lt;id&gt;“), null für die Hauptperson.</summary>
    public static string? IdOf(string dataRoot)
    {
        var d = Path.TrimEndingDirectorySeparator(dataRoot);
        return Path.GetFileName(Path.GetDirectoryName(d)) == "personen" ? Path.GetFileName(d) : null;
    }

    public List<Person> All()
    {
        try { return File.Exists(FileOf) ? JsonSerializer.Deserialize<List<Person>>(File.ReadAllText(FileOf)) ?? [] : []; }
        catch (Exception e) when (e is IOException or JsonException) { return []; }
    }

    public Person Add(string name, string avatar, bool child, AuditLog? audit = null)
    {
        name = new string((name ?? "").Where(c => !char.IsControl(c)).Take(40).ToArray()).Trim();
        if (name.Length == 0) throw new ArgumentException("Bitte einen Namen eingeben.");
        var p = new Person(Guid.NewGuid().ToString("N")[..10], name, avatar, child, DateTime.UtcNow);
        var all = All(); all.Add(p); Save(all);
        var root = RootOf(p.Id); Directory.CreateDirectory(root);
        // Kinder: einfache Bedienung und leichte Sprache von Anfang an
        var s = UserSettings.Load(root); s.Name = name; s.Avatar = avatar; s.Child = child;
        if (child) { s.SimpleMode = true; s.EasyLanguage = true; s.Level = "einfach"; }
        s.Save(root);
        audit?.Append("app", "person.add", child ? "Kind" : "Erwachsen");
        return p;
    }

    public bool Remove(string id, AuditLog? audit = null)
    {
        var all = All(); var n = all.RemoveAll(p => p.Id == id);
        if (n == 0) return false;
        Save(all); audit?.Append("app", "person.remove", id);
        return true; // Daten bleiben im Ordner liegen (nichts wird ohne zweite Rückfrage gelöscht)
    }

    void Save(List<Person> all) { Directory.CreateDirectory(baseRoot); File.WriteAllText(FileOf, JsonSerializer.Serialize(all)); }

    /// <summary>T-0764 (14. Prüfrunde): Rolle einer Person ändern – nur über <see cref="ProfileRole.Set"/> (Erwachsenen-Weg).</summary>
    internal void SetChild(string id, bool child)
    {
        var all = All(); var i = all.FindIndex(p => p.Id == id);
        if (i < 0) throw new InvalidOperationException("Diese Person gibt es nicht.");
        all[i] = all[i] with { Child = child }; Save(all);
    }

    /// <summary>T-0713: Erwachsenen-Profile ohne PIN oder Passwort – nur, solange es auch ein Kinderprofil gibt. Dann kann ein Kind über „Wechseln“
    /// in ein solches Profil gehen und dort freigeben; die Rollentrennung wirkt nicht. Die Hauptperson hat die Kennung „haupt“. Nur ein Hinweis, nichts wird erzwungen.</summary>
    public IReadOnlyList<(string Id, string Name)> AdultsWithoutLockBesideChild()
    {
        var main = UserSettings.Load(baseRoot);
        var profiles = new List<(string Id, string Name, bool Child)> { ("haupt", main.Name, ProfileRole.IsChild(baseRoot)) }; // T-0764 (14.): Rolle aus der geschützten Quelle
        foreach (var p in All()) profiles.Add((p.Id, p.Name, p.Child || ProfileRole.IsChild(RootOf(p.Id))));
        if (!profiles.Any(p => p.Child)) return [];
        return profiles.Where(p => !p.Child && !new ProfileLock(RootOf(p.Id)).Enabled).Select(p => (p.Id, p.Name)).ToList();
    }
}

/// <summary>B3: Wofür nutzt du Trendo? Aus den Antworten werden Studio-Pakete und Desktop-/Handy-Symbole.</summary>
public static class Interests
{
    public sealed record Interest(string Id, string Label, string Emoji, string[] Packs, string[] Studios);
    public static readonly IReadOnlyList<Interest> All =
    [
        new("buero", "Büro & Briefe", "📄", ["office"], ["STUDIO-LETTER-01", "SYS-10", "page:handy"]),
        new("kreativ", "Fotos, Videos & Musik", "🎨", ["kreativ"], ["CORE-08"]),
        new("spiele", "Spiele & 3D", "🎮", ["3d"], ["CORE-05", "CORE-06"]),
        new("lernen", "Lernen & Wissen", "📚", [], ["page:ki-wissen", "page:ki"]),
        new("business", "Business & Geld", "💼", ["office"], ["STUDIO-ONLINE-BUSINESS-01"]),
        new("code", "Programmieren & Web", "💻", ["dev"], ["CORE-07"]),
        new("ki", "KI nutzen", "✨", ["ki"], ["page:ki", "page:ki-internet"]),
        new("familie", "Familie & Alltag", "🏡", [], ["page:handy"]),
    ];

    public static IEnumerable<string> PacksFor(IEnumerable<string> chosen) => All.Where(i => chosen.Contains(i.Id)).SelectMany(i => i.Packs).Distinct();
    public static IEnumerable<string> StudiosFor(IEnumerable<string> chosen) => All.Where(i => chosen.Contains(i.Id)).SelectMany(i => i.Studios).Distinct();
}

/// <summary>B8: Lesezeichen aus Edge und Chrome übernehmen (nur lesen; Firefox nutzt eine Datenbank und bleibt außen vor).</summary>
public static class BrowserImport
{
    public static IEnumerable<string> BookmarkFiles(string localAppData) =>
        new[] { Path.Combine(localAppData, "Microsoft", "Edge", "User Data", "Default", "Bookmarks"), Path.Combine(localAppData, "Google", "Chrome", "User Data", "Default", "Bookmarks") }.Where(File.Exists);

    /// <summary>Liest eine Chromium-Lesezeichendatei; Ordnername wird übernommen.</summary>
    public static List<Bookmark> Parse(string json, string browser)
    {
        var list = new List<Bookmark>();
        using var doc = JsonDocument.Parse(json);
        void Walk(JsonElement node, string folder)
        {
            if (node.TryGetProperty("type", out var t) && t.GetString() == "url" && node.TryGetProperty("url", out var u) && u.GetString() is { } url && url.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                list.Add(new(node.TryGetProperty("name", out var n) ? n.GetString() ?? url : url, url, folder));
            if (node.TryGetProperty("children", out var kids))
                foreach (var k in kids.EnumerateArray()) Walk(k, node.TryGetProperty("type", out var tt) && tt.GetString() == "folder" && node.TryGetProperty("name", out var fn) && folder.Length == 0 ? $"{browser}: {fn.GetString()}" : folder);
        }
        if (doc.RootElement.TryGetProperty("roots", out var roots))
            foreach (var r in roots.EnumerateObject()) if (r.Value.ValueKind == JsonValueKind.Object) Walk(r.Value, "");
        return list.Select(b => b with { Folder = b.Folder.Length == 0 ? browser : b.Folder }).ToList();
    }

    /// <summary>Übernimmt Lesezeichen ohne Doppelte (gleiche Adresse). Liefert die Anzahl neuer Einträge.</summary>
    public static int ImportInto(string dataRoot, IEnumerable<Bookmark> found)
    {
        var have = Bookmarks.Load(dataRoot);
        var urls = have.Select(b => b.Url).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var neu = found.Where(b => urls.Add(b.Url)).ToList();
        if (neu.Count > 0) Bookmarks.Save(dataRoot, [.. have, .. neu]);
        return neu.Count;
    }
}

/// <summary>T-0764 (14. Prüfrunde, H1/H2): Rolle eines Profils aus einer GESCHÜTZTEN Quelle – nicht aus settings.json, die das Profil selbst schreiben,
/// ersetzen, einlesen oder zurücksetzen kann. Weitere Personen: Eintrag in <c>personen.json</c> des Hauptordners (<see cref="PersonStore"/>).
/// Hauptprofil: <c>profil/rolle.json</c> im Hauptordner. Beide liegen außerhalb der Kinder-Freigabe (<see cref="ChildWrite"/>: nur Dokumente, Bilder …
/// und „projekte“) und werden nur über den Erwachsenen-Weg geschrieben (Person anlegen, <see cref="Set"/>).
/// Fail-closed: Person ohne Eintrag, unlesbare Quelle oder Widerspruch → Kinderprofil. settings.json ist nur noch Spiegel und kann die Rolle nur
/// verschärfen (Child=true), nie lockern. Einzige Ausnahme ohne Eintrag: das Hauptprofil beim ersten Start – echte Ersteinrichtung (gar nichts da)
/// gilt als Besitzer; ein vorhandener Stand vor diesem Umbau wird einmalig aus settings.json übernommen (danach zählt nur noch rolle.json).</summary>
public static class ProfileRole
{
    public static string FileOf(string baseRoot) => Path.Combine(baseRoot, "profil", "rolle.json");
    sealed record Stored(bool Child, DateTime AtUtc);

    /// <summary>Kinderprofil? true auch, wenn die Quelle fehlt, unlesbar ist oder widerspricht (außer Ersteinrichtung des Hauptprofils).</summary>
    public static bool IsChild(string dataRoot)
    {
        try
        {
            var baseRoot = PersonStore.BaseOf(dataRoot);
            var mirror = MirrorChild(dataRoot);
            if (PersonStore.IdOf(dataRoot) is { } id)
            {
                var p = new PersonStore(baseRoot).All().FirstOrDefault(x => x.Id == id);
                return p is null || p.Child || mirror; // ohne Eintrag: Kind
            }
            var f = FileOf(baseRoot);
            if (File.Exists(f)) return JsonSerializer.Deserialize<Stored>(File.ReadAllText(f)) is not { } s || s.Child || mirror;
            // kein Eintrag: Ersteinrichtung (nichts da) → Besitzer; Stand vor dem Umbau → einmalig aus settings.json; sonst (Personen da, settings.json fehlt) → Kind
            var hasSettings = File.Exists(UserSettings.PathIn(dataRoot));
            var hasPersons = File.Exists(Path.Combine(baseRoot, "personen.json"));
            var child = hasSettings ? mirror : hasPersons;
            Write(baseRoot, child);
            return child;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException or ArgumentException or NotSupportedException) { return true; }
    }

    /// <summary>Child aus settings.json (nur Spiegel). Unlesbar → false (verschärft dann nicht; die Rolle kommt aus der geschützten Quelle).</summary>
    static bool MirrorChild(string dataRoot)
    {
        var f = UserSettings.PathIn(dataRoot);
        if (!File.Exists(f)) return false;
        try { using var doc = JsonDocument.Parse(File.ReadAllText(f)); return doc.RootElement.ValueKind == JsonValueKind.Object && doc.RootElement.TryGetProperty("Child", out var c) && c.ValueKind == JsonValueKind.True; }
        catch (JsonException) { return false; }
    }

    static void Write(string baseRoot, bool child)
    {
        var f = FileOf(baseRoot); Directory.CreateDirectory(Path.GetDirectoryName(f)!);
        AtomicFile.WriteAllText(f, JsonSerializer.Serialize(new Stored(child, DateTime.UtcNow)));
    }

    /// <summary>Der Erwachsenen-Weg: Rolle eines Profils festlegen (Hauptprofil: rolle.json, Person: personen.json) – nur durch ein Erwachsenen-Profil
    /// (<see cref="Rbac.DemandSoftware"/>). settings.json wird als Spiegel nachgezogen.</summary>
    public static void Set(Principal by, string dataRoot, bool child, AuditLog audit)
    {
        Rbac.DemandSoftware(by, "profil.rolle", audit, Rbac.NurErwachseneProfil);
        var baseRoot = PersonStore.BaseOf(dataRoot);
        if (PersonStore.IdOf(dataRoot) is { } id) new PersonStore(baseRoot).SetChild(id, child);
        else Write(baseRoot, child);
        var s = UserSettings.Load(dataRoot); s.Child = child; s.Save(dataRoot);
        audit.Append(by.Name, "profil.rolle", (child ? "Kind " : "Erwachsen ") + (PersonStore.IdOf(dataRoot) ?? "haupt"));
    }
}
