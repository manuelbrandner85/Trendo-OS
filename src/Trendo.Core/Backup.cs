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

    /// <summary>Stellt eine Sicherung wieder her. Vorher wird der aktuelle Stand als eigene Sicherung gespeichert.</summary>
    public void Restore(string zip, DateTime now)
    {
        if (!File.Exists(zip)) throw new FileNotFoundException(zip);
        var before = Create(now.AddSeconds(-1), keep: 20);
        using var z = ZipFile.OpenRead(zip);
        foreach (var e in z.Entries)
        {
            var dest = Path.GetFullPath(Path.Combine(dataRoot, e.FullName));
            if (!dest.StartsWith(Path.GetFullPath(dataRoot) + Path.DirectorySeparatorChar)) continue; // kein Zip-Slip
            if (e.FullName.StartsWith("audit/")) continue; // Audit-Kette wird nie überschrieben
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            e.ExtractToFile(dest, true);
        }
        audit.Append("owner", "backup.restored", $"{Path.GetFileName(zip)} (vorher gesichert: {Path.GetFileName(before)})");
    }
}
