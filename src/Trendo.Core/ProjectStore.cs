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
        // Symlink/Junction-Prüfung entlang des Pfades
        var cur = new DirectoryInfo(Path.GetDirectoryName(full)!);
        while (cur is not null && cur.FullName.Length + 1 >= baseDir.Length)
        {
            if (cur.Exists && cur.LinkTarget is not null) throw Deny(projectId, relativePath);
            cur = cur.Parent;
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

    private ProjectIsolationException Deny(string projectId, string rel)
    {
        _audit.Append("core", "isolation.denied", $"{projectId}:{rel}");
        return new ProjectIsolationException("Zugriff außerhalb des Projekts verweigert.");
    }
}

public sealed class ProjectIsolationException(string msg) : Exception(msg);
