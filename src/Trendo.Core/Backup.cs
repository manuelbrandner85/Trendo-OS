using System.IO.Compression;

namespace Trendo.Core;

/// <summary>Sicherung der Benutzerdaten (Projekte, Einstellungen, Audit) als ZIP; Wiederherstellung legt vorher eine Sicherheitskopie an.</summary>
public sealed class Backup(string dataRoot, string backupDir, AuditLog audit)
{
    public string Folder => backupDir;
    private static readonly string[] Skip = ["backups", "settings.json.tmp", "web-mixamo"]; // T-0660: Anmeldesitzung bei Mixamo nicht mitsichern

    public IReadOnlyList<FileInfo> List() => Directory.Exists(backupDir)
        ? new DirectoryInfo(backupDir).GetFiles("trendo-sicherung-*.zip").OrderByDescending(f => f.Name).ToList() : [];

    public bool Due(DateTime now, int everyHours) => everyHours > 0 && (List().FirstOrDefault() is not { } last || (now - last.LastWriteTime).TotalHours >= everyHours);

    public string Create(DateTime now, int keep = 10)
    {
        Directory.CreateDirectory(backupDir);
        var zip = Path.Combine(backupDir, $"trendo-sicherung-{now:yyyyMMdd-HHmmss}.zip");
        for (var n = 2; File.Exists(zip); n++) zip = Path.Combine(backupDir, $"trendo-sicherung-{now:yyyyMMdd-HHmmss}-{n}.zip"); // nie eine vorhandene Sicherung überschreiben
        using (var z = ZipFile.Open(zip, ZipArchiveMode.Create))
            foreach (var f in Directory.EnumerateFiles(dataRoot, "*", SearchOption.AllDirectories))
            {
                var rel = Path.GetRelativePath(dataRoot, f);
                if (Skip.Any(s => rel.StartsWith(s, StringComparison.OrdinalIgnoreCase)) || Path.GetFullPath(f).StartsWith(Path.GetFullPath(backupDir))) continue;
                using var src = new FileStream(f, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using var dst = z.CreateEntry(rel.Replace('\\', '/'), CompressionLevel.Optimal).Open();
                src.CopyTo(dst);
            }
        foreach (var old in List().Skip(keep)) old.Delete();
        audit.Append("app", "backup.created", Path.GetFileName(zip));
        return zip;
    }

    public sealed record RestorePlan(int Replaced, int Added, IReadOnlyList<string> Examples);

    /// <summary>Was ein Wiederherstellen ändern würde: ersetzte und neue Dateien (ohne Audit, ohne Zip-Slip), nichts wird geschrieben.</summary>
    public RestorePlan Preview(string zip)
    {
        if (!File.Exists(zip)) throw new FileNotFoundException(zip);
        using var z = ZipFile.OpenRead(zip);
        int replaced = 0, added = 0; var examples = new List<string>();
        foreach (var e in z.Entries)
        {
            if (e.FullName.EndsWith('/') || e.FullName.StartsWith("audit/")) continue;
            var dest = Path.GetFullPath(Path.Combine(dataRoot, e.FullName));
            if (!dest.StartsWith(Path.GetFullPath(dataRoot) + Path.DirectorySeparatorChar)) continue;
            if (File.Exists(dest)) { replaced++; if (examples.Count < 5) examples.Add(e.FullName); }
            else added++;
        }
        return new(replaced, added, examples);
    }

    /// <summary>Stellt eine Sicherung wieder her. Vorher wird der aktuelle Stand als eigene Sicherung gespeichert.</summary>
    public int Restore(string zip, DateTime now)
    {
        if (!File.Exists(zip)) throw new FileNotFoundException(zip);
        var before = Create(now.AddSeconds(-1), keep: 20);
        using var z = ZipFile.OpenRead(zip);
        var count = 0;
        foreach (var e in z.Entries)
        {
            var dest = Path.GetFullPath(Path.Combine(dataRoot, e.FullName));
            if (!dest.StartsWith(Path.GetFullPath(dataRoot) + Path.DirectorySeparatorChar)) continue; // kein Zip-Slip
            if (e.FullName.StartsWith("audit/")) continue; // Audit-Kette wird nie überschrieben
            if (e.FullName.EndsWith('/')) { Directory.CreateDirectory(dest); continue; }
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            if (File.Exists(dest)) { File.SetAttributes(dest, FileAttributes.Normal); File.Delete(dest); } // z. B. schreibgeschützte .git-Objekte
            e.ExtractToFile(dest, true);
            count++;
        }
        audit.Append("owner", "backup.restored", $"{Path.GetFileName(zip)} (vorher gesichert: {Path.GetFileName(before)})");
        return count;
    }

    // ---------- Zeitleiste: Inhalt, Vergleich, Einzeldatei (Masterprompt 131) ----------
    public sealed record Entry(string Path, long Bytes, DateTimeOffset Changed);
    public enum Change { Gleich, Geaendert, Geloescht, Neu }
    public sealed record Diff(string Path, Change Kind, long Bytes);

    /// <summary>Dateien einer Sicherung (ohne Protokoll), alphabetisch.</summary>
    public IReadOnlyList<Entry> Entries(string zip)
    {
        using var z = ZipFile.OpenRead(zip);
        return z.Entries.Where(e => !e.FullName.EndsWith('/') && !e.FullName.StartsWith("audit/")).Select(e => new Entry(e.FullName, e.Length, e.LastWriteTime)).OrderBy(e => e.Path, StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>Vergleich Sicherung ↔ heute: geändert (Inhalt anders), seither gelöscht, seither neu (ohne Protokoll und Sicherungen).</summary>
    public IReadOnlyList<Diff> CompareWithNow(string zip)
    {
        using var z = ZipFile.OpenRead(zip);
        var res = new List<Diff>(); var inZip = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var e in z.Entries.Where(e => !e.FullName.EndsWith('/') && !e.FullName.StartsWith("audit/")))
        {
            inZip.Add(e.FullName);
            var cur = Path.Combine(dataRoot, e.FullName);
            if (!File.Exists(cur)) { res.Add(new(e.FullName, Change.Geloescht, e.Length)); continue; }
            res.Add(new(e.FullName, SameContent(e, cur) ? Change.Gleich : Change.Geaendert, e.Length));
        }
        foreach (var f in Directory.EnumerateFiles(dataRoot, "*", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(dataRoot, f).Replace(Path.DirectorySeparatorChar, '/');
            if (rel.StartsWith("audit/") || Skip.Any(s => rel.StartsWith(s, StringComparison.OrdinalIgnoreCase)) || Path.GetFullPath(f).StartsWith(Path.GetFullPath(backupDir))) continue;
            if (!inZip.Contains(rel)) res.Add(new(rel, Change.Neu, new FileInfo(f).Length));
        }
        return res.OrderBy(d => d.Path, StringComparer.OrdinalIgnoreCase).ToList();
    }

    static bool SameContent(ZipArchiveEntry e, string file)
    {
        var fi = new FileInfo(file); if (fi.Length != e.Length) return false;
        using var a = e.Open(); using var b = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        var ba = new byte[81920]; var bb = new byte[81920];
        while (true)
        {
            var na = a.ReadAtLeast(ba, ba.Length, false); var nb = b.ReadAtLeast(bb, bb.Length, false);
            if (na != nb || !ba.AsSpan(0, na).SequenceEqual(bb.AsSpan(0, nb))) return false;
            if (na == 0) return true;
        }
    }

    /// <summary>Inhalt einer Datei aus der Sicherung (für die Vorschau), höchstens <paramref name="max"/> Bytes.</summary>
    public byte[] Read(string zip, string path, int max = 2_000_000)
    {
        using var z = ZipFile.OpenRead(zip);
        var e = z.GetEntry(path) ?? throw new FileNotFoundException("Nicht in der Sicherung: " + path);
        using var s = e.Open(); using var ms = new MemoryStream(); var buf = new byte[81920]; int n;
        while ((n = s.Read(buf)) > 0 && ms.Length < max) ms.Write(buf, 0, n);
        return ms.ToArray();
    }

    /// <summary>Eine einzelne Datei zurückholen. Die heutige Fassung wird vorher unter backups/einzeln/&lt;Zeit&gt;/ aufgehoben. Gibt den Ablageort der alten Fassung zurück (oder null).</summary>
    public string? RestoreOne(string zip, string path, DateTime now)
    {
        if (path.StartsWith("audit/")) throw new InvalidOperationException("Das Protokoll wird nie überschrieben.");
        var dest = Path.GetFullPath(Path.Combine(dataRoot, path));
        if (!dest.StartsWith(Path.GetFullPath(dataRoot) + Path.DirectorySeparatorChar)) throw new InvalidOperationException("Ungültiger Pfad.");
        using var z = ZipFile.OpenRead(zip);
        var e = z.GetEntry(path) ?? throw new FileNotFoundException("Nicht in der Sicherung: " + path);
        string? kept = null;
        if (File.Exists(dest))
        {
            kept = Path.Combine(backupDir, "einzeln", now.ToString("yyyyMMdd-HHmmss"), path.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(kept)!); File.Copy(dest, kept, true);
            File.SetAttributes(dest, FileAttributes.Normal);
        }
        Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
        e.ExtractToFile(dest, true);
        audit.Append("owner", "backup.restore.one", $"{Path.GetFileName(zip)}: {path}");
        return kept;
    }
}
