using System.IO.Compression;

namespace Trendo.Core;

/// <summary>Sicherung der Benutzerdaten (Projekte, Einstellungen, Audit) als ZIP; Wiederherstellung legt vorher eine Sicherheitskopie an.</summary>
public sealed class Backup(string dataRoot, string backupDir, AuditLog audit)
{
    public string Folder => backupDir;
    private static readonly string[] Skip = ["backups", "settings.json.tmp"];

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
}
