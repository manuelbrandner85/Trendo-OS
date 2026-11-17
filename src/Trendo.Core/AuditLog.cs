using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Trendo.Core;

/// <param name="Mac">T-0726: HMAC-SHA256 über <see cref="Hash"/> mit dem lokalen Protokoll-Schlüssel (null bei Einträgen ohne Schlüssel, z. B. aus der Zeit davor).</param>
/// <param name="Kid">T-0726: Kennung des Schlüssels (die ersten 16 Zeichen seines SHA-256) – der Schlüssel selbst steht nie im Protokoll.</param>
public sealed record AuditEntry(long Seq, DateTimeOffset At, string Actor, string Action, string Detail, string PrevHash, string Hash,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Mac = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Kid = null);

/// <summary>T-0726: Schlüssel des Prüfprotokolls. Liegt im geschützten Speicher (<see cref="ISecretStore"/>, unter Windows die Anmeldeinformationsverwaltung
/// des Benutzers) – nicht im Datenordner. Wer nur den Datenordner beschreiben kann, kann damit keine gültigen Einträge mehr anhängen oder ändern.</summary>
/// <param name="Since">Ab wann der Schlüssel gilt: ältere Einträge ohne Schlüssel bleiben lesbar und prüfbar (nur Hash-Kette), neuere müssen signiert sein.</param>
public sealed record AuditKey(byte[] Key, DateTimeOffset Since)
{
    public const string SecretName = "audit-hmac";
    public string Id => Convert.ToHexString(SHA256.HashData(Key))[..16];

    /// <summary>Schlüssel aus dem Speicher holen oder einmalig anlegen (32 Zufallsbytes). null: Speicher nicht dauerhaft (z. B. Linux-Ausgabe ohne Schlüsselbund)
    /// oder nicht nutzbar – dann bleibt es bei der Hash-Kette wie bisher.</summary>
    public static AuditKey? LoadOrCreate(ISecretStore store)
    {
        if (!store.Persistent) return null;
        // zwei Fenster starten gleichzeitig: nur eines legt den Schlüssel an, das andere liest ihn danach (sonst signiert eines mit einem Schlüssel, der nicht gespeichert ist)
        using var m = new Mutex(false, @"Local\TrendoOS-audit-hmac");
        var have = false;
        try { have = m.WaitOne(TimeSpan.FromSeconds(5)); } catch (AbandonedMutexException) { have = true; }
        try
        {
            if (Parse(store.Get(SecretName)) is { } k) return k;
            var key = new AuditKey(RandomNumberGenerator.GetBytes(32), DateTimeOffset.Now);
            store.Set(SecretName, $"v1|{key.Since.ToUnixTimeMilliseconds()}|{Convert.ToBase64String(key.Key)}");
            return Parse(store.Get(SecretName)); // nur was wirklich gespeichert ist, gilt
        }
        catch (Exception e) when (e is InvalidOperationException or System.ComponentModel.Win32Exception or DllNotFoundException or EntryPointNotFoundException) { return null; }
        finally { if (have) m.ReleaseMutex(); }
    }

    static AuditKey? Parse(string? v)
    {
        var p = (v ?? "").Split('|');
        if (p.Length != 3 || p[0] != "v1" || !long.TryParse(p[1], out var ms)) return null;
        try { var k = Convert.FromBase64String(p[2]); return k.Length >= 32 ? new AuditKey(k, DateTimeOffset.FromUnixTimeMilliseconds(ms)) : null; }
        catch (FormatException) { return null; }
    }
}

/// <summary>Append-only Audit-Log als JSON-Lines mit Hash-Kette. Keine Lösch-/Änderungs-API.
/// T-0726: Mit Schlüssel (<see cref="UseKey"/>) trägt jeder neue Eintrag zusätzlich einen HMAC über seinen Hash. Weil jeder Hash den vorigen einschließt,
/// sichert der HMAC eines Eintrags die ganze Kette davor. Prüfregeln: signierte Einträge müssen zum Schlüssel passen; nach dem ersten signierten
/// Eintrag darf keiner mehr ohne (oder mit fremdem) Schlüssel folgen; Einträge ohne Schlüssel sind nur vor dessen Anlage erlaubt (alte Protokolle bleiben gültig).
/// Grenze: Wird das Protokoll am Ende gekürzt oder ganz ersetzt, fällt das ohne einen Anker außerhalb des Datenordners nicht auf.</summary>
public sealed class AuditLog
{
    private readonly string _path;
    // Eine Sperre pro Datei (nicht pro Instanz): mehrere AuditLog-Objekte auf dieselbe Datei schreiben sonst gleichzeitig.
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, object> Locks = new(StringComparer.OrdinalIgnoreCase);
    // T-0726: Schlüssel je Datei (nicht je Instanz): auch weitere AuditLog-Objekte auf dieselbe Datei in diesem Prozess signieren und prüfen damit.
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, AuditKey> Keys = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _lock;
    private readonly string _full;
    public AuditLog(string path)
    {
        _path = path;
        _full = Path.GetFullPath(path);
        _lock = Locks.GetOrAdd(_full, _ => new object());
        Directory.CreateDirectory(Path.GetDirectoryName(_full)!);
    }
    public string FilePath => _path;

    /// <summary>T-0726: ab jetzt mit diesem Schlüssel signieren und prüfen (gilt für alle AuditLog-Objekte dieser Datei im Prozess).</summary>
    public void UseKey(AuditKey key) => Keys[_full] = key;
    /// <summary>Wird mit Schlüssel signiert und geprüft?</summary>
    public bool Keyed => Keys.ContainsKey(_full);
    AuditKey? Key => Keys.TryGetValue(_full, out var k) ? k : null;

    public AuditEntry Append(string actor, string action, string detail)
    {
        lock (_lock)
        {
            // Lesen und Anhängen in EINEM exklusiv geöffneten Strom: auch ein zweiter Prozess kann die Kette nicht spalten.
            using var fs = Retry(() => new FileStream(_path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read));
            var all = Parse(ReadText(fs));
            var prev = all.Count == 0 ? "GENESIS" : all[^1].Hash;
            var seq = all.Count == 0 ? 1 : all[^1].Seq + 1;
            var at = DateTimeOffset.Now;
            var hash = ComputeHash(seq, at, actor, action, detail, prev);
            var k = Key;
            var e = new AuditEntry(seq, at, actor, action, detail, prev, hash, k is null ? null : Mac(k, hash), k?.Id);
            fs.Seek(0, SeekOrigin.End);
            fs.Write(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(e) + "\n"));
            return e;
        }
    }

    public IReadOnlyList<AuditEntry> ReadAll()
    {
        if (!File.Exists(_path)) return [];
        using var fs = Retry(() => new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite));
        return Parse(ReadText(fs));
    }

    private static string ReadText(FileStream fs) { fs.Seek(0, SeekOrigin.Begin); using var r = new StreamReader(fs, Encoding.UTF8, true, 4096, leaveOpen: true); return r.ReadToEnd(); }

    // Nur vollständige Zeilen (mit \n) zählen – eine halb geschriebene Zeile eines anderen Prozesses wird ignoriert.
    private static List<AuditEntry> Parse(string text)
    {
        var end = text.LastIndexOf('\n');
        if (end < 0) return [];
        return text[..end].Split('\n').Where(l => l.Length > 0).Select(l => JsonSerializer.Deserialize<AuditEntry>(l)!).ToList();
    }

    /// <summary>Windows sperrt Dateien hart (Virenscanner, zweiter Prozess): kurz warten statt abstürzen.</summary>
    private static FileStream Retry(Func<FileStream> open)
    {
        for (var i = 0; ; i++)
        {
            try { return open(); }
            catch (IOException) when (i < 40) { Thread.Sleep(25 + i * 5); }
        }
    }

    /// <summary>Prüft die Hash-Kette (und mit Schlüssel die Signaturen); false bei Manipulation.</summary>
    public bool Verify() => FirstBroken() is null;

    /// <summary>SYS-13-P09: Nummer des ersten veränderten, fehlenden, vertauschten oder (T-0726) nicht korrekt signierten Eintrags; null = Kette unverändert.
    /// Ein Eintrag ohne gültige Signatur nach der Schlüssel-Anlage zählt als Bruch – außer ein späterer signierter Eintrag
    /// „<see cref="UnsignedAck"/>“ nennt ihn (siehe <see cref="AcknowledgeUnsigned"/>).</summary>
    public long? FirstBroken() => Check().Broken;

    /// <summary>T-0726: Aktion des signierten Eintrags, der einen Abschnitt ohne Signatur sichtbar festhält (Detail „von-bis“).</summary>
    public const string UnsignedAck = "audit.unsigniert.erkannt";

    /// <summary>T-0726: Ergebnis der Prüfung: erster Bruch (Kette, falsche Signatur, nicht festgehaltener Eintrag ohne Signatur), alle Einträge ohne Signatur nach der
    /// Schlüssel-Anlage, davon festgehaltene, und der erste Bruch von Kette/Signatur allein.</summary>
    public (long? Broken, IReadOnlyList<long> Unsigned, int Acknowledged, long? Chain) Check()
    {
        var prev = "GENESIS"; long seq = 0;
        var k = Key; var signed = false;
        long? chain = null;
        var unsigned = new List<long>(); var acks = new List<(long From, long To)>();
        foreach (var e in ReadAll())
        {
            if (e.PrevHash != prev || e.Seq != seq + 1 || ComputeHash(e.Seq, e.At, e.Actor, e.Action, e.Detail, e.PrevHash) != e.Hash) { chain = seq + 1; break; }
            if (k is not null)
            {
                if (e.Kid == k.Id)
                {
                    if (e.Mac is not { Length: 64 } mac || !mac.All(Uri.IsHexDigit)
                        || !CryptographicOperations.FixedTimeEquals(Convert.FromHexString(mac), Convert.FromHexString(Mac(k, e.Hash)))) { chain = seq + 1; break; }
                    signed = true;
                    if (e.Action == UnsignedAck && e.Detail.Split('-') is [var a, var b] && long.TryParse(a, out var from) && long.TryParse(b, out var to)) acks.Add((from, to));
                }
                else if (signed || e.At >= k.Since) unsigned.Add(e.Seq); // ohne (oder mit fremdem) Schlüssel nach dessen Anlage: ältere Fassung, kein Tresor – oder jemand ohne Schlüssel
            }
            prev = e.Hash; seq = e.Seq;
        }
        var open = unsigned.Where(s => (chain is null || s < chain) && !acks.Any(r => s >= r.From && s <= r.To)).Cast<long?>().FirstOrDefault();
        var broken = chain is null ? open : open is null ? chain : Math.Min(chain.Value, open.Value);
        return (broken, unsigned, unsigned.Count(s => acks.Any(r => s >= r.From && s <= r.To)), chain);
    }

    /// <summary>T-0726: Einträge ohne Signatur nach der Schlüssel-Anlage (z. B. nach dem Rücksprung auf eine ältere Trendo-Fassung oder wenn der Tresor beim Start
    /// nicht erreichbar war) mit einem signierten Eintrag sichtbar festhalten – sonst meldete das Protokoll für immer „beschädigt“. Nur wenn Kette und alle
    /// Signaturen sonst stimmen. Der Abschnitt bleibt als „ohne Signatur“ erkennbar (<see cref="Check"/>); das Freigabe-Tor übernimmt daraus nichts.
    /// Liefert die Zahl der neu festgehaltenen Einträge.</summary>
    public int AcknowledgeUnsigned(string actor)
    {
        if (Key is null) return 0;
        lock (_lock)
        {
            var (broken, unsigned, _, chain) = Check();
            if (broken is null || chain is not null) return 0; // nur festhalten, wenn Kette und Signaturen sonst stimmen
            var acks = unsigned.Where(s => s >= broken).ToList();
            if (acks.Count == 0 || acks[0] != broken) return 0;
            var all = ReadAll(); var last = all.Count == 0 ? 0 : all[^1].Seq;
            Append(actor, UnsignedAck, $"{broken}-{last}");
            if (Check().Broken is not null) return 0;
            return unsigned.Count(s => s >= broken);
        }
    }

    /// <summary>T-0726: Einträge, denen das Freigabe-Tor beim Neustart trauen darf: mit Schlüssel nur gültig signierte, ohne Schlüssel alle (wie bisher).</summary>
    public IReadOnlyList<AuditEntry> Trusted()
    {
        var (broken, _, _, _) = Check();
        if (broken is not null) return [];
        var all = ReadAll();
        return Key is { } k ? all.Where(e => e.Kid == k.Id).ToList() : all;
    }

    private static string Mac(AuditKey k, string hash) => Convert.ToHexString(HMACSHA256.HashData(k.Key, Encoding.UTF8.GetBytes(hash)));

    private static string ComputeHash(long seq, DateTimeOffset at, string actor, string action, string detail, string prev)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{seq}|{at:O}|{actor}|{action}|{detail}|{prev}")));
}
