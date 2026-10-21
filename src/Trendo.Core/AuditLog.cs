using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Trendo.Core;

public sealed record AuditEntry(long Seq, DateTimeOffset At, string Actor, string Action, string Detail, string PrevHash, string Hash);

/// <summary>Append-only Audit-Log als JSON-Lines mit Hash-Kette. Keine Lösch-/Änderungs-API.</summary>
public sealed class AuditLog
{
    private readonly string _path;
    // Eine Sperre pro Datei (nicht pro Instanz): mehrere AuditLog-Objekte auf dieselbe Datei schreiben sonst gleichzeitig.
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, object> Locks = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _lock;
    public AuditLog(string path)
    {
        _path = path;
        _lock = Locks.GetOrAdd(Path.GetFullPath(path), _ => new object());
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
    }
    public string FilePath => _path;

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
            var e = new AuditEntry(seq, at, actor, action, detail, prev, hash);
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

    /// <summary>Prüft die Hash-Kette; false bei Manipulation.</summary>
    public bool Verify()
    {
        var prev = "GENESIS"; long seq = 0;
        foreach (var e in ReadAll())
        {
            if (e.PrevHash != prev || e.Seq != seq + 1) return false;
            if (ComputeHash(e.Seq, e.At, e.Actor, e.Action, e.Detail, e.PrevHash) != e.Hash) return false;
            prev = e.Hash; seq = e.Seq;
        }
        return true;
    }

    private static string ComputeHash(long seq, DateTimeOffset at, string actor, string action, string detail, string prev)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{seq}|{at:O}|{actor}|{action}|{detail}|{prev}")));
}
