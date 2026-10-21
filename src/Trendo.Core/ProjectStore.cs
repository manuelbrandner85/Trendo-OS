using System.Text.Json;
using System.Text.RegularExpressions;

namespace Trendo.Core;

public sealed record TrendoProject(string Id, string Name, DateTimeOffset CreatedAt);

/// <summary>Projekte mit eigener Pfadwurzel. Pfade werden serverseitig geprüft; Ausbrüche (../, absolute Pfade, Symlinks nach außen) werden abgewiesen.</summary>
public sealed partial class ProjectStore
{
    private readonly string _root;
    private readonly AuditLog _audit;
    public ProjectStore(string root, AuditLog audit)
    {
        _root = Path.GetFullPath(root);
        _audit = audit;
        Directory.CreateDirectory(_root);
    }

    [GeneratedRegex("^[a-z0-9][a-z0-9-]{1,47}$")] private static partial Regex IdPattern();

    public TrendoProject Create(string id, string name)
    {
        if (!IdPattern().IsMatch(id)) throw new ArgumentException("Ungültige Projekt-ID (a-z, 0-9, '-', 2–48 Zeichen).");
        var dir = Path.Combine(_root, id);
        if (Directory.Exists(dir)) throw new InvalidOperationException("Projekt existiert bereits.");
        Directory.CreateDirectory(Path.Combine(dir, "files"));
        var p = new TrendoProject(id, name, DateTimeOffset.Now);
        File.WriteAllText(Path.Combine(dir, "project.json"), JsonSerializer.Serialize(p));
        _audit.Append("user", "project.create", id);
        return p;
    }

    public IReadOnlyList<TrendoProject> List() =>
        Directory.EnumerateDirectories(_root)
            .Select(d => Path.Combine(d, "project.json")).Where(File.Exists)
            .Select(f => JsonSerializer.Deserialize<TrendoProject>(File.ReadAllText(f))!)
            .OrderBy(p => p.Id).ToList();

    public string ProjectFilesRoot(string projectId)
    {
        if (!IdPattern().IsMatch(projectId)) throw new ProjectIsolationException("Ungültige Projekt-ID.");
        var dir = Path.Combine(_root, projectId, "files");
        if (!Directory.Exists(dir)) throw new ProjectIsolationException("Projekt unbekannt.");
        return dir;
    }

    /// <summary>Löst einen relativen Pfad innerhalb des Projekts auf. Wirft bei jedem Ausbruchsversuch.</summary>
    public string ResolveInProject(string projectId, string relativePath)
    {
        relativePath = (relativePath ?? "").Replace('\\', '/'); // Backslash plattformunabhängig als Trenner behandeln
        var baseDir = Path.GetFullPath(ProjectFilesRoot(projectId)) + Path.DirectorySeparatorChar;
        if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath) || relativePath.Contains(':'))
            throw Deny(projectId, relativePath);
        var full = Path.GetFullPath(Path.Combine(baseDir, relativePath));
        if (!full.StartsWith(baseDir, StringComparison.OrdinalIgnoreCase)) throw Deny(projectId, relativePath);
        // Symlink/Junction-Prüfung entlang des Pfades und am Ziel selbst
        var cur = new DirectoryInfo(Path.GetDirectoryName(full)!);
        while (cur is not null && cur.FullName.Length + 1 >= baseDir.Length)
        {
            if (cur.Exists && cur.LinkTarget is not null) throw Deny(projectId, relativePath);
            cur = cur.Parent;
        }
        if (File.Exists(full) || Directory.Exists(full))
        {
            FileSystemInfo leaf = (File.GetAttributes(full) & FileAttributes.Directory) != 0 ? new DirectoryInfo(full) : new FileInfo(full);
            if (leaf.LinkTarget is not null || (leaf.Attributes & FileAttributes.ReparsePoint) != 0)
                throw Deny(projectId, relativePath);
        }
        return full;
    }

    public void WriteFile(string projectId, string relativePath, string content)
    {
        var p = ResolveInProject(projectId, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(p)!);
        File.WriteAllText(p, content);
        _audit.Append("user", "file.write", $"{projectId}:{relativePath}");
    }

    public string ReadFile(string projectId, string relativePath) => File.ReadAllText(ResolveInProject(projectId, relativePath));

    public sealed record ProjectNode(string Name, string RelativePath, bool IsDirectory, long Bytes);

    /// <summary>Listet nur den Projektordner (oder einen Unterordner darin). Folgt keinen Verknüpfungen.</summary>
    public IReadOnlyList<ProjectNode> List(string projectId, string relativeDirectory = "")
    {
        var root = Path.GetFullPath(ProjectFilesRoot(projectId));
        var dir = string.IsNullOrWhiteSpace(relativeDirectory) ? root : ResolveInProject(projectId, relativeDirectory);
        if (!Directory.Exists(dir) || (File.GetAttributes(dir) & FileAttributes.Directory) == 0)
            throw new ProjectIsolationException("Ordner unbekannt.");
        var prefix = string.IsNullOrWhiteSpace(relativeDirectory) ? "" : relativeDirectory.Replace('\\', '/').Trim('/') + "/";
        var list = new List<ProjectNode>();
        foreach (var path in Directory.EnumerateFileSystemEntries(dir))
        {
            var name = Path.GetFileName(path);
            var isDirectory = (File.GetAttributes(path) & FileAttributes.Directory) != 0;
            long bytes = 0;
            if (!isDirectory)
            {
                try { bytes = new FileInfo(path).Length; } catch { bytes = 0; }
            }
            list.Add(new ProjectNode(name, prefix + name, isDirectory, bytes));
        }
        return list.OrderByDescending(n => n.IsDirectory).ThenBy(n => n.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public void CreateFolder(string projectId, string relativePath)
    {
        var p = ResolveInProject(projectId, relativePath);
        if (File.Exists(p)) throw new InvalidOperationException("Eine Datei mit diesem Namen existiert bereits.");
        Directory.CreateDirectory(p);
        _audit.Append("user", "file.mkdir", $"{projectId}:{relativePath}");
    }

    /// <summary>Löscht eine Datei oder einen Ordner im Projekt. Die Projektwurzel selbst bleibt.</summary>
    public void Delete(string projectId, string relativePath)
    {
        var p = ResolveInProject(projectId, relativePath);
        var root = Path.GetFullPath(ProjectFilesRoot(projectId));
        if (string.Equals(Path.GetFullPath(p).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
            throw Deny(projectId, relativePath);
        if (ContainsReparse(p)) throw Deny(projectId, relativePath);
        if (Directory.Exists(p))
        {
            Directory.Delete(p, true);
            _audit.Append("user", "file.rmdir", $"{projectId}:{relativePath}");
        }
        else if (File.Exists(p))
        {
            File.Delete(p);
            _audit.Append("user", "file.delete", $"{projectId}:{relativePath}");
        }
        else throw new FileNotFoundException("Nicht gefunden.");
    }

    /// <summary>Textvorschau bis 1 MB. Binärdateien werden nicht als Text ausgegeben.</summary>
    public string ReadPreview(string projectId, string relativePath, int maxChars = 8000)
    {
        var p = ResolveInProject(projectId, relativePath);
        if ((File.GetAttributes(p) & FileAttributes.Directory) != 0)
            throw new InvalidOperationException("Das ist ein Ordner.");
        var info = new FileInfo(p);
        if (info.Length > 1_048_576) return "(Datei größer als 1 MB – keine Vorschau)";
        var bytes = File.ReadAllBytes(p);
        if (Array.IndexOf(bytes, (byte)0) >= 0) return "(Binärdatei – keine Textvorschau)";
        _audit.Append("user", "file.read", $"{projectId}:{relativePath}");
        var text = System.Text.Encoding.UTF8.GetString(bytes);
        return text.Length <= maxChars ? text : text[..maxChars] + "\n…";
    }

    private static bool ContainsReparse(string path)
    {
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) return true;
        if ((File.GetAttributes(path) & FileAttributes.Directory) == 0) return false;
        foreach (var entry in Directory.EnumerateFileSystemEntries(path))
            if (ContainsReparse(entry)) return true;
        return false;
    }

    private ProjectIsolationException Deny(string projectId, string rel)
    {
        _audit.Append("core", "isolation.denied", $"{projectId}:{rel}");
        return new ProjectIsolationException("Zugriff außerhalb des Projekts verweigert.");
    }
}

public sealed class ProjectIsolationException(string msg) : Exception(msg);
