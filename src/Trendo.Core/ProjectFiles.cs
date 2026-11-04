using System.Globalization;
using System.Text;

namespace Trendo.Core;

/// <summary>Projektmappe (Umbau 04.11.2026): Dateien eines Projekts auflisten, hinzufügen, Ordner anlegen, Notiz – alles innerhalb der Projektgrenze
/// (jede Aktion läuft über <see cref="ResolveInProject"/>, Ausbrüche werden abgewiesen).</summary>
public sealed partial class ProjectStore
{
    public sealed record ProjectFile(string Relative, string Name, bool IsDir, long Bytes, DateTime Changed, int Depth);
    public sealed record ProjectInfo(int Files, long Bytes, DateTime? LastChange, IReadOnlyList<string> Kinds);

    /// <summary>Lesbarer Name → gültige Projekt-ID („Urlaub 2026!“ → „urlaub-2026“). Umlaute werden umschrieben.</summary>
    public static string Slug(string name)
    {
        var s = name.Trim().ToLowerInvariant().Replace("ä", "ae").Replace("ö", "oe").Replace("ü", "ue").Replace("ß", "ss");
        var sb = new StringBuilder();
        foreach (var c in s.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
            sb.Append(c is >= 'a' and <= 'z' or >= '0' and <= '9' ? c : '-');
        }
        var slug = System.Text.RegularExpressions.Regex.Replace(sb.ToString(), "-+", "-").Trim('-');
        if (slug.Length < 2) slug = "projekt-" + slug;
        return slug.Length > 48 ? slug[..48].TrimEnd('-') : slug;
    }

    /// <summary>Neues Projekt aus einem lesbaren Namen; belegte IDs bekommen eine Nummer.</summary>
    public TrendoProject CreateNamed(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Bitte einen Namen für das Projekt eingeben.");
        var baseId = Slug(name); var id = baseId;
        for (var n = 2; Directory.Exists(Path.Combine(_root, id)); n++) id = $"{baseId}-{n}";
        return Create(id, name.Trim());
    }

    /// <summary>Dateien und Ordner des Projekts (Ordner zuerst), höchstens <paramref name="max"/> Einträge.</summary>
    public List<ProjectFile> Files(string projectId, int max = 2000)
    {
        var root = ProjectFilesRoot(projectId); var list = new List<ProjectFile>();
        void Walk(string dir, int depth)
        {
            foreach (var d in Directory.GetDirectories(dir).Order(StringComparer.CurrentCultureIgnoreCase))
            {
                if (list.Count >= max) return;
                var di = new DirectoryInfo(d); if (di.LinkTarget is not null) continue;
                list.Add(new(Path.GetRelativePath(root, d).Replace('\\', '/'), di.Name, true, 0, di.LastWriteTime, depth));
                Walk(d, depth + 1);
            }
            foreach (var f in Directory.GetFiles(dir).Order(StringComparer.CurrentCultureIgnoreCase))
            {
                if (list.Count >= max) return;
                var fi = new FileInfo(f);
                list.Add(new(Path.GetRelativePath(root, f).Replace('\\', '/'), fi.Name, false, fi.Length, fi.LastWriteTime, depth));
            }
        }
        Walk(root, 0);
        return list;
    }

    public ProjectInfo Info(string projectId)
    {
        var files = Files(projectId).Where(f => !f.IsDir).ToList();
        var kinds = files.Select(f => Path.GetExtension(f.Name).ToLowerInvariant()).Where(e => e.Length > 1).GroupBy(e => e).OrderByDescending(g => g.Count()).Take(5).Select(g => g.Key).ToList();
        return new(files.Count, files.Sum(f => f.Bytes), files.Count == 0 ? null : files.Max(f => f.Changed), kinds);
    }

    /// <summary>Datei von außen ins Projekt kopieren (Ziel: Ordner im Projekt oder oberste Ebene). Gleiche Namen bekommen eine Nummer.</summary>
    public string AddFile(string projectId, string source, string? folder = null)
    {
        if (!File.Exists(source)) throw new FileNotFoundException("Datei nicht gefunden: " + Path.GetFileName(source));
        var name = Path.GetFileName(source); var rel = string.IsNullOrWhiteSpace(folder) ? name : folder.TrimEnd('/') + "/" + name;
        var target = ResolveInProject(projectId, rel);
        for (var n = 2; File.Exists(target); n++) { rel = (string.IsNullOrWhiteSpace(folder) ? "" : folder.TrimEnd('/') + "/") + $"{Path.GetFileNameWithoutExtension(name)} ({n}){Path.GetExtension(name)}"; target = ResolveInProject(projectId, rel); }
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        File.Copy(source, target);
        _audit.Append("user", "project.file.add", $"{projectId}:{rel}");
        return rel;
    }

    public string NewFolder(string projectId, string relative)
    {
        var p = ResolveInProject(projectId, relative.Trim().Trim('/'));
        if (Directory.Exists(p) || File.Exists(p)) throw new InvalidOperationException($"„{relative}“ gibt es schon.");
        Directory.CreateDirectory(p);
        _audit.Append("user", "project.folder.add", $"{projectId}:{relative}");
        return p;
    }

    string NotePath(string projectId) { ProjectFilesRoot(projectId); return Path.Combine(_root, projectId, "notiz.txt"); }
    public string Note(string projectId) => File.Exists(NotePath(projectId)) ? File.ReadAllText(NotePath(projectId)) : "";
    public void SetNote(string projectId, string text) { File.WriteAllText(NotePath(projectId), text); _audit.Append("user", "project.note", projectId); }

    /// <summary>Projekt umbenennen (nur der lesbare Name, der Ordner bleibt).</summary>
    public TrendoProject Rename(string projectId, string name)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Bitte einen Namen eingeben.");
        var p = List().FirstOrDefault(x => x.Id == projectId) ?? throw new ProjectIsolationException("Projekt unbekannt.");
        var n = p with { Name = name.Trim() };
        File.WriteAllText(Path.Combine(_root, projectId, "project.json"), System.Text.Json.JsonSerializer.Serialize(n));
        _audit.Append("user", "project.rename", projectId);
        return n;
    }
}
