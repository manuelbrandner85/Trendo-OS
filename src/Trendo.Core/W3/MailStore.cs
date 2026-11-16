using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Trendo.Core;

/// <summary>T-0689: Lokaler Zwischenspeicher des Mailprogramms – Ordnerliste, Kopfzeilen je Ordner und geöffnete Nachrichten als Rohdatei (.eml).
/// Damit bleibt das Postfach ohne Netz lesbar und nach einem Absturz steht alles wieder da. Je Konto ein Unterordner; Passwörter liegen hier nie.
/// Alle Dateien werden über <see cref="AtomicFile"/> geschrieben (nie halbe Dateien).</summary>
public sealed class MailStore
{
    /// <summary>Stand eines Ordners: Kennung des Servers, Anzahl dort, bisher geladene Kopfzeilen (neueste zuerst).</summary>
    public sealed record FolderState(long UidValidity, int Exists, List<MailHeader> Headers, DateTimeOffset SyncedAt);

    readonly string _root;

    public MailStore(string dataRoot, string email)
    {
        var safe = string.Concat(email.Trim().ToLowerInvariant().Select(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '-' or '_' ? c : '_'));
        _root = Path.Combine(dataRoot, "post", "cache", safe.Length > 0 ? safe : "konto");
    }

    public string Root => _root;

    string Dir(string folder)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(folder)))[..12].ToLowerInvariant();
        return Path.Combine(_root, hash);
    }

    static T? Read<T>(string file) where T : class
    {
        try { return File.Exists(file) ? JsonSerializer.Deserialize<T>(File.ReadAllText(file)) : null; }
        catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException) { return null; }
    }

    public List<MailFolderInfo> Folders() => Read<List<MailFolderInfo>>(Path.Combine(_root, "ordner.json")) ?? [];
    public void SaveFolders(IEnumerable<MailFolderInfo> folders) => AtomicFile.WriteAllText(Path.Combine(_root, "ordner.json"), JsonSerializer.Serialize(folders.ToList()));

    public FolderState Load(string folder) => Read<FolderState>(Path.Combine(Dir(folder), "liste.json")) ?? new(0, 0, [], DateTimeOffset.MinValue);

    void Save(string folder, FolderState st) => AtomicFile.WriteAllText(Path.Combine(Dir(folder), "liste.json"), JsonSerializer.Serialize(st));

    /// <summary>Frisch geladene Kopfzeilen einarbeiten. <paramref name="firstPage"/>: Das ist die neueste Seite – was dort im Bereich der geladenen Nummern
    /// fehlt, wurde auf dem Server gelöscht oder verschoben. Hat der Server die Ordner-Kennung geändert, wird der Ordner neu aufgebaut.</summary>
    public FolderState Merge(string folder, long uidValidity, int exists, IReadOnlyList<MailHeader> page, bool firstPage)
    {
        var old = Load(folder);
        var reset = uidValidity != 0 && old.UidValidity != 0 && old.UidValidity != uidValidity;
        if (reset) Clear(folder);
        var map = reset ? [] : old.Headers.ToDictionary(h => h.Uid);
        if (firstPage && page.Count > 0)
        {
            var min = page.Min(h => h.Uid); var fresh = page.Select(h => h.Uid).ToHashSet();
            foreach (var gone in map.Keys.Where(u => u >= min && !fresh.Contains(u)).ToList()) { map.Remove(gone); TryDelete(RawFile(folder, gone)); }
        }
        else if (firstPage && exists == 0) { foreach (var u in map.Keys.ToList()) TryDelete(RawFile(folder, u)); map.Clear(); }
        foreach (var h in page) map[h.Uid] = h;
        var st = new FolderState(uidValidity != 0 ? uidValidity : old.UidValidity, exists, map.Values.OrderByDescending(h => h.Uid).ToList(), DateTimeOffset.Now);
        Save(folder, st);
        return st;
    }

    /// <summary>Kopfzeilen lokal ändern (gelesen, Stern) oder entfernen (gelöscht/verschoben).</summary>
    public FolderState Update(string folder, Func<MailHeader, MailHeader?> change)
    {
        var old = Load(folder); var list = new List<MailHeader>();
        foreach (var h in old.Headers) { if (change(h) is { } n) list.Add(n); else TryDelete(RawFile(folder, h.Uid)); }
        var st = old with { Headers = list, Exists = Math.Max(0, old.Exists - (old.Headers.Count - list.Count)) };
        Save(folder, st);
        return st;
    }

    string RawFile(string folder, long uid) => Path.Combine(Dir(folder), uid.ToString(System.Globalization.CultureInfo.InvariantCulture) + ".eml");

    /// <summary>Gespeicherte Nachricht (Rohbytes) oder null, wenn sie noch nie geöffnet wurde.</summary>
    public byte[]? Raw(string folder, long uid) { try { var f = RawFile(folder, uid); return File.Exists(f) ? File.ReadAllBytes(f) : null; } catch (IOException) { return null; } }
    public void SaveRaw(string folder, long uid, byte[] raw) => AtomicFile.WriteAllBytes(RawFile(folder, uid), raw);

    void Clear(string folder) { try { if (Directory.Exists(Dir(folder))) Directory.Delete(Dir(folder), true); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { } }
    static void TryDelete(string f) { try { if (File.Exists(f)) File.Delete(f); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { } }

    /// <summary>Suche ohne Netz: Absender, Empfänger, Betreff – und der Text aller schon geöffneten Nachrichten.</summary>
    public List<MailHeader> Search(string folder, string query)
    {
        var q = query.Trim(); if (q.Length == 0) return Load(folder).Headers;
        bool Hit(string s) => s.Contains(q, StringComparison.CurrentCultureIgnoreCase);
        return Load(folder).Headers.Where(h => Hit(h.Subject) || Hit(h.From) || Hit(h.To) || (Raw(folder, h.Uid) is { } raw && Hit(MailMime.Parse(raw).Body))).ToList();
    }
}
