using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Trendo.Core;

public sealed record AuditEntry(long Seq, DateTimeOffset At, string Actor, string Action, string Detail, string PrevHash, string Hash);

/// <summary>Append-only Audit-Log als JSON-Lines mit Hash-Kette. Keine Lösch-/Änderungs-API.</summary>
public sealed class AuditLog
{
    private readonly string _path;
    private readonly object _lock = new();
    public AuditLog(string path)
    {
        _path = path;
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
    }
    public string FilePath => _path;

    public AuditEntry Append(string actor, string action, string detail)
    {
        lock (_lock)
        {
            var all = ReadAll();
            var prev = all.Count == 0 ? "GENESIS" : all[^1].Hash;
            var seq = all.Count == 0 ? 1 : all[^1].Seq + 1;
            var at = DateTimeOffset.Now;
            var hash = ComputeHash(seq, at, actor, action, detail, prev);
            var e = new AuditEntry(seq, at, actor, action, detail, prev, hash);
            using var fs = new FileStream(_path, FileMode.Append, FileAccess.Write, FileShare.Read);
            var line = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(e) + "\n");
            fs.Write(line);
            return e;
        }
    }

    public IReadOnlyList<AuditEntry> ReadAll()
    {
        if (!File.Exists(_path)) return [];
        return File.ReadAllLines(_path).Where(l => l.Length > 0)
            .Select(l => JsonSerializer.Deserialize<AuditEntry>(l)!).ToList();
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
