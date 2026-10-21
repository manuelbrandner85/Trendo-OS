using System.Security.Cryptography;
using System.Text.Json;

namespace Trendo.Core;

public sealed record EvidenceItem(string Name, string OriginalPath, string Sha256, long Bytes, DateTimeOffset AddedAt);
public sealed class EvidenceCase
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public DateTimeOffset Created { get; set; }
    public DateTimeOffset Expires { get; set; }
    public List<EvidenceItem> Items { get; set; } = [];
}

public enum CaseState { Aktiv, LaeuftBaldAb, Abgelaufen }

/// <summary>SPEC-12 Cybersecurity & Forensics: Beweis-Fälle nur lokal, Kopien mit SHA-256 und lückenloser Kette (eigenes Protokoll),
/// schreibgeschützt; Fälle werden nach höchstens 120 h gelöscht, 24 h vorher Warnung (AGENTS.md Regel 5). Dateien prüfen per Prüfsumme.</summary>
public sealed class ForensicsLab
{
    public static readonly TimeSpan MaxLife = TimeSpan.FromHours(120), WarnBefore = TimeSpan.FromHours(24);
    private readonly string _root; private readonly AuditLog _audit; private readonly TimeProvider _time;

    public ForensicsLab(string dataRoot, AuditLog audit, TimeProvider? time = null)
    {
        _root = Path.Combine(dataRoot, "forensik"); _audit = audit; _time = time ?? TimeProvider.System;
        Directory.CreateDirectory(_root);
    }

    public static string Sha256(string file) { using var s = File.OpenRead(file); return Convert.ToHexString(SHA256.HashData(s)).ToLowerInvariant(); }

    public static bool Matches(string file, string expected) => string.Equals(Sha256(file), expected.Trim().Replace(" ", ""), StringComparison.OrdinalIgnoreCase);

    private string Dir(string id) => Path.Combine(_root, id);
    private string Meta(string id) => Path.Combine(Dir(id), "fall.json");
    public AuditLog Custody(string id) => new(Path.Combine(Dir(id), "kette.jsonl"));

    public EvidenceCase Create(string title, TimeSpan? life = null)
    {
        var l = life ?? MaxLife; if (l > MaxLife || l <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(life), "Höchstens 120 Stunden.");
        var now = _time.GetLocalNow();
        var c = new EvidenceCase { Id = now.ToString("yyyyMMdd-HHmmss") + "-" + Convert.ToHexString(RandomNumberGenerator.GetBytes(2)).ToLowerInvariant(), Title = title.Trim() == "" ? "Fall" : title.Trim(), Created = now, Expires = now + l };
        Directory.CreateDirectory(Path.Combine(Dir(c.Id), "beweise")); Save(c);
        Custody(c.Id).Append(Principal.LocalOwner.Name, "fall.angelegt", $"{c.Title}, löscht sich am {c.Expires:dd.MM.yyyy HH:mm}");
        _audit.Append(Principal.LocalOwner.Name, "forensik.case", c.Id);
        return c;
    }

    public IReadOnlyList<EvidenceCase> Cases() => Directory.GetDirectories(_root).Select(d => Path.Combine(d, "fall.json")).Where(File.Exists)
        .Select(f => { try { return JsonSerializer.Deserialize<EvidenceCase>(File.ReadAllText(f)); } catch (JsonException) { return null; } }).OfType<EvidenceCase>().OrderByDescending(c => c.Created).ToList();

    public CaseState State(EvidenceCase c) { var now = _time.GetLocalNow(); return now >= c.Expires ? CaseState.Abgelaufen : c.Expires - now <= WarnBefore ? CaseState.LaeuftBaldAb : CaseState.Aktiv; }

    /// <summary>Kopiert eine Datei unverändert in den Fall, prüft Original- gegen Kopie-Prüfsumme, setzt Schreibschutz.</summary>
    public EvidenceItem Add(EvidenceCase c, string file)
    {
        if (State(c) == CaseState.Abgelaufen) throw new InvalidOperationException("Der Fall ist abgelaufen.");
        if (!File.Exists(file)) throw new FileNotFoundException(file);
        var h = Sha256(file);
        var name = Path.GetFileName(file); var dest = Path.Combine(Dir(c.Id), "beweise", name);
        for (var i = 2; File.Exists(dest); i++) dest = Path.Combine(Dir(c.Id), "beweise", $"{Path.GetFileNameWithoutExtension(name)}_{i}{Path.GetExtension(name)}");
        File.Copy(file, dest);
        if (Sha256(dest) != h) { File.Delete(dest); throw new IOException("Kopie weicht vom Original ab."); }
        File.SetAttributes(dest, File.GetAttributes(dest) | FileAttributes.ReadOnly);
        var it = new EvidenceItem(Path.GetFileName(dest), Path.GetFileName(file), h, new FileInfo(dest).Length, _time.GetLocalNow());
        c.Items.Add(it); Save(c);
        Custody(c.Id).Append(Principal.LocalOwner.Name, "beweis.gesichert", $"{it.Name} sha256={h} {it.Bytes}B");
        return it;
    }

    /// <summary>Prüft alle Kopien gegen die gespeicherte Prüfsumme und die Kette auf Veränderung.</summary>
    public (bool ChainOk, IReadOnlyList<(EvidenceItem Item, bool Ok)> Items) Verify(EvidenceCase c)
    {
        var res = c.Items.Select(i => { var p = Path.Combine(Dir(c.Id), "beweise", i.Name); return (i, File.Exists(p) && Sha256(p) == i.Sha256); }).ToList();
        var chain = Custody(c.Id);
        chain.Append(Principal.LocalOwner.Name, "fall.geprueft", $"{res.Count(r => r.Item2)}/{res.Count} unverändert");
        return (chain.Verify(), res);
    }

    public string Report(EvidenceCase c)
    {
        var (ok, items) = Verify(c);
        var lines = new List<string> { $"# Fallbericht: {c.Title}", "", $"- Fall-ID: {c.Id}", $"- Angelegt: {c.Created:dd.MM.yyyy HH:mm}", $"- Löscht sich automatisch: {c.Expires:dd.MM.yyyy HH:mm}", $"- Kette unverändert: {(ok ? "ja" : "NEIN")}", "", "| Datei | SHA-256 | Größe | Zustand |", "|---|---|---|---|" };
        lines.AddRange(items.Select(i => $"| {i.Item.Name} | {i.Item.Sha256} | {i.Item.Bytes} B | {(i.Ok ? "unverändert" : "VERÄNDERT/FEHLT")} |"));
        lines.Add(""); lines.Add("## Kette"); lines.AddRange(Custody(c.Id).ReadAll().Select(e => $"- {e.At:dd.MM.yyyy HH:mm:ss} {e.Action}: {e.Detail}"));
        var f = Path.Combine(Dir(c.Id), "bericht.md"); File.WriteAllLines(f, lines);
        return f;
    }

    /// <summary>Löscht abgelaufene Fälle vollständig (inkl. Kopien). Gibt die Anzahl zurück.</summary>
    public int PurgeExpired()
    {
        var n = 0;
        foreach (var c in Cases().Where(c => State(c) == CaseState.Abgelaufen)) { Delete(c.Id); n++; }
        return n;
    }

    public void Delete(string id)
    {
        var d = Dir(id); if (!Directory.Exists(d)) return;
        foreach (var f in Directory.GetFiles(d, "*", SearchOption.AllDirectories)) File.SetAttributes(f, FileAttributes.Normal);
        Directory.Delete(d, true);
        _audit.Append(Principal.LocalOwner.Name, "forensik.deleted", id);
    }

    private void Save(EvidenceCase c) => File.WriteAllText(Meta(c.Id), JsonSerializer.Serialize(c));
}
