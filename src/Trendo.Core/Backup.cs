using System.IO.Compression;

namespace Trendo.Core;

/// <summary>Was eine Sicherung an vorhandenen Dateien ändern würde. Das Audit-Protokoll steht nie auf der Liste.</summary>
public sealed record BackupPreview(IReadOnlyList<string> Overwrite, int Added);

/// <summary>Sicherung der Benutzerdaten (Projekte, Einstellungen, Audit) als ZIP; Wiederherstellung legt vorher eine Sicherheitskopie an.</summary>
public sealed class Backup(string dataRoot, string backupDir, AuditLog audit)
{
    public string Folder => backupDir;

    public IReadOnlyList<FileInfo> List() => Directory.Exists(backupDir)
        ? new DirectoryInfo(backupDir).GetFiles("trendo-sicherung-*.zip").OrderByDescending(f => f.Name).ToList() : [];

    public bool Due(DateTime now, int everyHours) => everyHours > 0 && (List().FirstOrDefault() is not { } last || (now - last.LastWriteTime).TotalHours >= everyHours);

    public string Create(DateTime now, int keep = 10, string? protectPath = null)
    {
        Directory.CreateDirectory(backupDir);
        var zip = NewZipPath(now);
        var opened = false;
        try
        {
            using (var archive = ZipFile.Open(zip, ZipArchiveMode.Create))
            {
                opened = true;
                foreach (var file in Directory.EnumerateFiles(dataRoot, "*", SearchOption.AllDirectories))
                {
                    if (!Include(file)) continue;
                    var rel = Path.GetRelativePath(dataRoot, file).Replace('\\', '/');
                    using var src = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                    using var dst = archive.CreateEntry(rel, CompressionLevel.Optimal).Open();
                    src.CopyTo(dst);
                }
            }
        }
        catch
        {
            // Handle ist zu, nur die angefangene Datei dieses Laufs löschen.
            if (opened) TryDelete(zip);
            throw;
        }
        Prune(keep, protectPath);
        audit.Append("app", "backup.created", Path.GetFileName(zip));
        return zip;
    }

    /// <summary>Dateien, die ersetzt würden, und wie viele neu dazukommen. Unsichere Pfade und das Audit zählen nicht.</summary>
    public BackupPreview Preview(string zip)
    {
        if (string.IsNullOrWhiteSpace(zip) || !File.Exists(zip)) throw new FileNotFoundException("Diese Sicherung gibt es nicht.", zip);
        var over = new List<string>();
        var added = 0;
        using var archive = ZipFile.OpenRead(zip);
        foreach (var entry in archive.Entries)
        {
            if (!TryFile(entry, out var rel, out var dest)) continue;
            if (File.Exists(dest)) over.Add(rel);
            else added++;
        }
        return new BackupPreview(over, added);
    }

    /// <summary>Stellt eine Sicherung wieder her. Vorher wird der aktuelle Stand als eigene Sicherung gespeichert. Liefert die Anzahl geschriebener Dateien.</summary>
    public int Restore(string zip, DateTime now)
    {
        if (string.IsNullOrWhiteSpace(zip) || !File.Exists(zip)) throw new FileNotFoundException("Diese Sicherung gibt es nicht.", zip);
        var source = Path.GetFullPath(zip);
        // Kopie, damit die Sicherheitskopie die gewählte Datei nicht durch Rotation löscht oder sperrt.
        var temp = Path.Combine(Path.GetTempPath(), "trendo-restore-" + Guid.NewGuid().ToString("N") + ".zip");
        File.Copy(source, temp, overwrite: false);
        try
        {
            using (var check = ZipFile.OpenRead(temp)) { _ = check.Entries.Count; }
            var before = Create(now.AddSeconds(-1), keep: 20, protectPath: source);
            var count = 0;
            using var archive = ZipFile.OpenRead(temp);
            foreach (var entry in archive.Entries)
            {
                if (IsDirectory(entry))
                {
                    if (TryDirectory(entry, out var dir)) Directory.CreateDirectory(dir);
                    continue;
                }
                if (!TryFile(entry, out _, out var dest)) continue;
                ExtractOver(entry, dest);
                count++;
            }
            audit.Append("owner", "backup.restored", $"{Path.GetFileName(source)} (vorher gesichert: {Path.GetFileName(before)}, {count} Dateien)");
            return count;
        }
        finally { TryDelete(temp); }
    }

    /// <summary>Eindeutiger Name, auch wenn in derselben Sekunde schon eine Sicherung liegt.</summary>
    private string NewZipPath(DateTime now)
    {
        var stamp = now.ToString("yyyyMMdd-HHmmss");
        for (var n = 1; n <= 1000; n++)
        {
            var name = n == 1 ? $"trendo-sicherung-{stamp}.zip" : $"trendo-sicherung-{stamp}-{n}.zip";
            var path = Path.Combine(backupDir, name);
            if (!File.Exists(path)) return path;
        }
        return Path.Combine(backupDir, $"trendo-sicherung-{stamp}-{Guid.NewGuid():N}.zip");
    }

    private bool Include(string fullPath)
    {
        var rel = Path.GetRelativePath(dataRoot, fullPath).Replace('\\', '/');
        if (rel.Equals("settings.json.tmp", StringComparison.OrdinalIgnoreCase)) return false;
        if (rel.Equals("backups", StringComparison.OrdinalIgnoreCase) || rel.StartsWith("backups/", StringComparison.OrdinalIgnoreCase)) return false;
        var backupFull = Path.GetFullPath(backupDir).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return !Path.GetFullPath(fullPath).StartsWith(backupFull, StringComparison.OrdinalIgnoreCase);
    }

    private void Prune(int keep, string? protectPath)
    {
        var kept = 0;
        foreach (var file in List())
        {
            if (protectPath is not null && SamePath(file.FullName, protectPath)) continue;
            if (kept < keep) { kept++; continue; }
            TryDelete(file.FullName);
        }
    }

    private static bool IsDirectory(ZipArchiveEntry entry)
    {
        var rel = entry.FullName.Replace('\\', '/');
        return rel.EndsWith('/') || entry.Name.Length == 0;
    }

    private bool TryDirectory(ZipArchiveEntry entry, out string dest)
    {
        dest = "";
        var rel = entry.FullName.Replace('\\', '/').TrimEnd('/');
        if (rel.Equals("audit", StringComparison.Ordinal) || rel.StartsWith("audit/", StringComparison.Ordinal)) return false;
        return TryInside(rel, out dest);
    }

    private bool TryFile(ZipArchiveEntry entry, out string rel, out string dest)
    {
        rel = entry.FullName.Replace('\\', '/');
        dest = "";
        if (rel.StartsWith("audit/", StringComparison.Ordinal)) return false;
        if (!TryInside(rel, out dest)) return false;
        return true;
    }

    /// <summary>Ziel liegt in der Datenwurzel. Zip-Slip, absolute Pfade und das Audit-Protokoll fallen heraus.</summary>
    private bool TryInside(string rel, out string dest)
    {
        dest = "";
        if (!IsSafeRelative(rel)) return false;
        var root = Path.GetFullPath(dataRoot);
        var mapped = Path.GetFullPath(Path.Combine(root, rel.Replace('/', Path.DirectorySeparatorChar)));
        var prefix = root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!mapped.StartsWith(prefix, StringComparison.Ordinal)) return false;
        dest = mapped;
        return true;
    }

    private static bool IsSafeRelative(string rel)
    {
        if (rel.Length == 0 || rel.StartsWith('/') || rel.StartsWith('\\') || Path.IsPathRooted(rel)) return false;
        foreach (var part in rel.Split('/'))
            if (part.Length == 0 || part == "." || part == "..") return false;
        return true;
    }

    /// <summary>Überschreibt auch schreibgeschützte Dateien (z. B. .git/objects), ohne abzustürzen.</summary>
    private static void ExtractOver(ZipArchiveEntry entry, string dest)
    {
        var parent = Path.GetDirectoryName(dest);
        if (!string.IsNullOrEmpty(parent)) Directory.CreateDirectory(parent);
        MakeWritable(dest);
        try { if (File.Exists(dest)) File.Delete(dest); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { MakeWritable(dest); }
        entry.ExtractToFile(dest, overwrite: true);
    }

    private static void MakeWritable(string path)
    {
        if (!File.Exists(path)) return;
        try
        {
            var attr = File.GetAttributes(path);
            if ((attr & FileAttributes.ReadOnly) == 0) return;
            var cleared = attr & ~FileAttributes.ReadOnly;
            File.SetAttributes(path, cleared == 0 ? FileAttributes.Normal : cleared);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }

    private static bool SamePath(string a, string b) =>
        Path.GetFullPath(a).Equals(Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase);

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                MakeWritable(path);
                File.Delete(path);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }
}
