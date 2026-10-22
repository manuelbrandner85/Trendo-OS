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
        var zip = NewZipPath(now);
        using (var z = ZipFile.Open(zip, ZipArchiveMode.Create))
            foreach (var f in Directory.EnumerateFiles(dataRoot, "*", SearchOption.AllDirectories))
            {
                var rel = Path.GetRelativePath(dataRoot, f);
                if (Skip.Any(s => rel.StartsWith(s, StringComparison.OrdinalIgnoreCase)) || Path.GetFullPath(f).StartsWith(Path.GetFullPath(backupDir))) continue;
                using var src = new FileStream(f, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using var dst = z.CreateEntry(rel.Replace('\\', '/'), CompressionLevel.Optimal).Open();
                src.CopyTo(dst);
            }
        foreach (var old in List().Skip(keep)) TryDelete(old.FullName);
        audit.Append("app", "backup.created", Path.GetFileName(zip));
        return zip;
    }

    /// <summary>Dateien der Sicherung, die eine vorhandene Datei überschreiben würden (Audit nie).</summary>
    public IReadOnlyList<string> WillOverwrite(string zip)
    {
        if (!File.Exists(zip)) throw new FileNotFoundException("Diese Sicherung gibt es nicht.", zip);
        var root = Path.GetFullPath(dataRoot) + Path.DirectorySeparatorChar;
        var list = new List<string>();
        using var z = ZipFile.OpenRead(zip);
        foreach (var e in z.Entries)
        {
            if (IsSkippedEntry(e)) continue;
            var dest = Path.GetFullPath(Path.Combine(dataRoot, e.FullName));
            if (!dest.StartsWith(root, StringComparison.Ordinal)) continue;
            if (File.Exists(dest)) list.Add(e.FullName.Replace('\\', '/'));
        }
        return list;
    }

    /// <summary>Stellt eine Sicherung wieder her. Vorher wird der aktuelle Stand als eigene Sicherung gespeichert. Liefert die Anzahl geschriebener Dateien.</summary>
    public int Restore(string zip, DateTime now)
    {
        if (!File.Exists(zip)) throw new FileNotFoundException("Diese Sicherung gibt es nicht.", zip);
        // Kopie, damit die Sicherheitskopie davor die Quelle nicht durch Rotation löscht oder sperrt.
        var temp = Path.Combine(Path.GetTempPath(), "trendo-restore-" + Guid.NewGuid().ToString("N") + ".zip");
        File.Copy(zip, temp, overwrite: true);
        var before = Create(now, keep: 20);
        var n = 0;
        try
        {
            using var z = ZipFile.OpenRead(temp);
            foreach (var e in z.Entries)
            {
                if (IsSkippedEntry(e)) continue;
                var dest = Path.GetFullPath(Path.Combine(dataRoot, e.FullName));
                if (!dest.StartsWith(Path.GetFullPath(dataRoot) + Path.DirectorySeparatorChar, StringComparison.Ordinal)) continue; // kein Zip-Slip
                ExtractOver(e, dest);
                n++;
            }
        }
        finally { try { File.Delete(temp); } catch (IOException) { } }
        audit.Append("owner", "backup.restored", $"{Path.GetFileName(zip)} (vorher gesichert: {Path.GetFileName(before)}, {n} Dateien)");
        return n;
    }

    /// <summary>Eindeutiger Name, auch wenn in derselben Sekunde schon eine Sicherung liegt.</summary>
    private string NewZipPath(DateTime now)
    {
        var stamp = now.ToString("yyyyMMdd-HHmmss");
        for (var n = 0; n < 1000; n++)
        {
            var name = n == 0 ? $"trendo-sicherung-{stamp}.zip" : $"trendo-sicherung-{stamp}-{n + 1}.zip";
            var path = Path.Combine(backupDir, name);
            if (!File.Exists(path)) return path;
        }
        return Path.Combine(backupDir, $"trendo-sicherung-{stamp}-{Guid.NewGuid():N}.zip");
    }

    private static bool IsSkippedEntry(ZipArchiveEntry e) =>
        e.FullName.Length == 0 || e.FullName.EndsWith('/') || e.FullName.Replace('\\', '/').StartsWith("audit/", StringComparison.OrdinalIgnoreCase);

    /// <summary>Überschreibt auch schreibgeschützte Dateien (z. B. .git/objects), ohne abzustürzen.</summary>
    private static void ExtractOver(ZipArchiveEntry entry, string dest)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
        if (File.Exists(dest))
        {
            try
            {
                var attr = File.GetAttributes(dest);
                if (attr.HasFlag(FileAttributes.ReadOnly)) File.SetAttributes(dest, attr & ~FileAttributes.ReadOnly);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
            try { if (File.Exists(dest)) File.Delete(dest); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
        entry.ExtractToFile(dest, overwrite: true);
    }

    private static void TryDelete(string path)
    {
        try { File.Delete(path); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }
}
