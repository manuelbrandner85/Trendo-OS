using System.Text.Json;
using System.Text.Json.Serialization;

namespace Trendo.Core.Sys;

/// <summary>T-0764 (SYS-11): Programme, die Trendo selbst eingerichtet hat – an einem Ort mit Starten, Aktualisieren und Entfernen.
/// Entfernen geht ohne Windows-Einstellungen, weil nur Trendos eigene Ordner betroffen sind (Portable-Ordner, Werkzeugordner).
/// Nichts wird sofort gelöscht: der Programmordner wandert in die Sicherung „.entfernt“ im selben Ordner (gleiches Laufwerk, ein Umbenennen –
/// ganz oder gar nicht) und lässt sich von dort wiederherstellen; erst „Endgültig löschen“ gibt den Platz frei.
/// Fremde Programme (über ein Windows-Setup installiert) entfernt Trendo hier nicht – dafür bleibt der ehrliche Weg über Windows bzw. das App-Center (winget).</summary>
public static class TrendoPrograms
{
    public enum Herkunft { Portable, Vollpaket, GitHub }

    /// <summary>Ein von Trendo eingerichtetes Programm. <paramref name="Id"/> ist der Ordnername im jeweiligen Wurzelordner.</summary>
    public sealed record Eintrag(string Id, Herkunft Art, string Name, string Ordner, string? Exe, string Version, string Quelle, long Bytes);

    /// <summary>Ein entferntes Programm in der Sicherung.</summary>
    public sealed record Sicherung(string Ordner, string Id, Herkunft Art, string Name, string Version, DateTime Am, long Bytes);

    /// <summary>Name der Sicherung in jedem Wurzelordner.</summary>
    public const string BackupDir = ".entfernt";

    sealed record Meta(string Id, Herkunft Art, string Name, string Version, DateTime Am, GitHubApps.Installed? GitHub);
    static readonly JsonSerializerOptions J = new() { WriteIndented = true, Converters = { new JsonStringEnumConverter() } };

    /// <summary>Alle von Trendo eingerichteten Programme: Portable (eigener Ordner), mitgelieferte Programme aus dem Vollpaket und Programme von GitHub.</summary>
    public static IReadOnlyList<Eintrag> List(string portableRoot, string toolsRoot)
    {
        var res = new List<Eintrag>();
        var packages = Vollpaket.Included.Values.Select(v => v.Package).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var pkg in packages.Order(StringComparer.OrdinalIgnoreCase))
        {
            var dir = Path.Combine(toolsRoot, pkg);
            if (!Directory.Exists(dir)) continue;
            var exe = Vollpaket.Included.Values.Where(v => v.Package.Equals(pkg, StringComparison.OrdinalIgnoreCase)).Select(v => Path.Combine(toolsRoot, v.Exe.Replace('/', Path.DirectorySeparatorChar))).FirstOrDefault(File.Exists);
            var guide = ProgramGuide.Included.TryGetValue(pkg, out var g) ? g.Name : pkg;
            res.Add(new(pkg, Herkunft.Vollpaket, guide, dir, exe, "", "Trendo-Vollpaket (Prüfsumme kontrolliert)", Size(dir)));
        }
        foreach (var e in GitHubApps.List(toolsRoot))
        {
            var dir = Path.Combine(toolsRoot, e.Id);
            if (!Directory.Exists(dir)) continue;
            var exe = GitHubApps.ExePath(toolsRoot, e);
            res.Add(new(e.Id, Herkunft.GitHub, e.Repo, dir, File.Exists(exe) ? exe : null, e.Version, $"GitHub {e.Repo} · Lizenz {e.License}", Size(dir)));
        }
        if (Directory.Exists(portableRoot))
            foreach (var dir in Directory.GetDirectories(portableRoot).Order(StringComparer.OrdinalIgnoreCase))
            {
                var name = Path.GetFileName(dir);
                // derselbe Ordner wie der Werkzeugordner (Testmodus): dessen eigene Einträge nicht doppelt zeigen
                if (name.StartsWith('.') || packages.Contains(name) || name.StartsWith(GitHubApps.Prefix, StringComparison.Ordinal) || name.StartsWith(SourceApps.Prefix, StringComparison.Ordinal)
                    || SamePath(portableRoot, toolsRoot) && name is "laufzeiten" or "ki-bibliothek" or "model") continue;
                if (new DirectoryInfo(dir).Attributes.HasFlag(FileAttributes.ReparsePoint)) continue;
                // T-0764 (Gegenprüfung N1): nur Ordner, die InstallPortable eingerichtet hat (Kennungsdatei). Ältere Stände ohne Kennung nur, wenn
                // darin ein Programm liegt und der Name kein Trendo-Arbeitsordner ist (z. B. „rueckgaengig“ der Fotos).
                var marked = File.Exists(Path.Combine(dir, Software.PortableMarker));
                var exe = MainExe(dir);
                if (!marked && (Reserved.Contains(name) || exe is null)) continue;
                res.Add(new(name, Herkunft.Portable, name, dir, exe, "", marked ? "Portable-ZIP (Prüfsumme beim Entpacken kontrolliert)" : "Portable-Programm (älterer Stand ohne Kennung)", Size(dir)));
            }
        return res;
    }

    /// <summary>Hauptprogramm eines Portable-Ordners: gleichnamige .exe, sonst die größte (höchstens zwei Ebenen tief, ohne Deinstaller).</summary>
    static string? MainExe(string dir)
    {
        var opt = new EnumerationOptions { RecurseSubdirectories = true, MaxRecursionDepth = 2, IgnoreInaccessible = true, AttributesToSkip = FileAttributes.ReparsePoint };
        var exes = Directory.EnumerateFiles(dir, "*.exe", opt).Where(f => !Path.GetFileName(f).StartsWith("unins", StringComparison.OrdinalIgnoreCase)).Take(200).ToList();
        var name = Path.GetFileName(dir);
        return exes.FirstOrDefault(f => Path.GetFileNameWithoutExtension(f).Equals(name, StringComparison.OrdinalIgnoreCase)) ?? exes.OrderByDescending(f => new FileInfo(f).Length).FirstOrDefault();
    }

    /// <summary>Arbeitsordner von Trendo im Portable-Ordner – nie ein Programm.</summary>
    static readonly HashSet<string> Reserved = new(StringComparer.OrdinalIgnoreCase) { "rueckgaengig", "laufzeiten", "ki-bibliothek", "model", "temp", "tmp", "cache", "protokolle" };

    static long Size(string dir)
    {
        try
        {
            var opt = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = FileAttributes.ReparsePoint };
            return new DirectoryInfo(dir).EnumerateFiles("*", opt).Sum(f => f.Length);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return 0; }
    }

    static bool SamePath(string a, string b) => string.Equals(Path.TrimEndingDirectorySeparator(Path.GetFullPath(a)), Path.TrimEndingDirectorySeparator(Path.GetFullPath(b)), StringComparison.OrdinalIgnoreCase);

    static string RootOf(Herkunft art, string portableRoot, string toolsRoot) => art == Herkunft.Portable ? portableRoot : toolsRoot;

    /// <summary>Entfernen: nur Erwachsene, nur ein Ordner direkt im eigenen Wurzelordner (keine Pfadtricks, keine Verknüpfung nach draußen).
    /// Der Ordner wird in die Sicherung verschoben (ein Umbenennen – ist ein Programm noch geöffnet, scheitert es ganz und es ändert sich nichts).</summary>
    public static Sicherung Remove(Principal by, Eintrag e, string portableRoot, string toolsRoot, AuditLog audit, DateTime? now = null)
    {
        Rbac.DemandSoftware(by, "programm.entfernen " + e.Id, audit);
        var root = Path.GetFullPath(RootOf(e.Art, portableRoot, toolsRoot));
        var dir = Path.GetFullPath(Path.Combine(root, e.Id));
        if (e.Id.Length == 0 || e.Id.StartsWith('.') || e.Id.IndexOfAny(['/', '\\', ':']) >= 0 || !SamePath(Path.GetDirectoryName(dir)!, root))
            throw new ArgumentException("Dieser Ordner gehört nicht zu Trendo – es wird nichts entfernt.");
        var info = new DirectoryInfo(dir);
        if (!info.Exists) throw new InvalidOperationException($"{e.Name} ist nicht (mehr) eingerichtet.");
        if (info.Attributes.HasFlag(FileAttributes.ReparsePoint)) throw new InvalidOperationException($"{e.Name} ist nur ein verknüpfter Ordner – bitte unter „Zusatzprogramme“ entfernen.");
        var gh = e.Art == Herkunft.GitHub ? GitHubApps.List(toolsRoot).FirstOrDefault(x => x.Id == e.Id) : null;
        if (e.Art == Herkunft.GitHub && gh is null) throw new InvalidOperationException($"{e.Name} steht nicht in der Liste der GitHub-Programme.");
        if (e.Art == Herkunft.Vollpaket && !Vollpaket.Included.Values.Any(v => v.Package.Equals(e.Id, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"{e.Name} ist kein mitgeliefertes Programm.");
        var at = now ?? DateTime.Now;
        var backups = Path.Combine(root, BackupDir);
        Directory.CreateDirectory(backups);
        var target = Path.Combine(backups, $"{at:yyyyMMdd-HHmmss}-{e.Id}");
        for (var i = 2; Directory.Exists(target) || File.Exists(target + ".json"); i++) target = Path.Combine(backups, $"{at:yyyyMMdd-HHmmss}-{i}-{e.Id}");
        Directory.Move(dir, target); // ein Schritt: alles oder nichts
        AtomicFile.WriteAllText(target + ".json", JsonSerializer.Serialize(new Meta(e.Id, e.Art, e.Name, e.Version, at, gh), J));
        if (gh is not null) GitHubApps.Forget(toolsRoot, gh.Id);
        audit.Append(by.Name, "programm.entfernt", $"{e.Art} {e.Id} → Sicherung {Path.GetFileName(target)}");
        return new(target, e.Id, e.Art, e.Name, e.Version, at, e.Bytes);
    }

    /// <summary>Alle Sicherungen entfernter Programme (neueste zuerst).</summary>
    public static IReadOnlyList<Sicherung> Backups(string portableRoot, string toolsRoot)
    {
        var res = new List<Sicherung>();
        foreach (var root in SamePath(portableRoot, toolsRoot) ? [portableRoot] : new[] { portableRoot, toolsRoot })
        {
            var b = Path.Combine(root, BackupDir);
            if (!Directory.Exists(b)) continue;
            foreach (var json in Directory.GetFiles(b, "*.json"))
            {
                var dir = json[..^5];
                if (!Directory.Exists(dir) || Read(json) is not { } m) continue;
                res.Add(new(dir, m.Id, m.Art, m.Name, m.Version, m.Am, Size(dir)));
            }
        }
        return res.OrderByDescending(s => s.Am).ToList();
    }

    static Meta? Read(string json)
    {
        try { return JsonSerializer.Deserialize<Meta>(File.ReadAllText(json), J); }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException) { return null; }
    }

    /// <summary>Die Sicherung muss genau in „.entfernt“ eines der beiden Wurzelordner liegen.</summary>
    static string CheckBackup(Sicherung s, string portableRoot, string toolsRoot)
    {
        var dir = Path.GetFullPath(s.Ordner);
        var root = Path.GetFullPath(RootOf(s.Art, portableRoot, toolsRoot));
        if (!SamePath(Path.GetDirectoryName(dir)!, Path.Combine(root, BackupDir)) || !Directory.Exists(dir) || !File.Exists(dir + ".json"))
            throw new ArgumentException("Diese Sicherung gehört nicht zu Trendo.");
        return root;
    }

    /// <summary>Wiederherstellen: nur Erwachsene; das Programm kommt an seinen alten Platz zurück (gibt es dort schon etwas, wird nichts überschrieben).</summary>
    public static Eintrag Restore(Principal by, Sicherung s, string portableRoot, string toolsRoot, AuditLog audit)
    {
        Rbac.DemandSoftware(by, "programm.wiederherstellen " + s.Id, audit);
        var root = CheckBackup(s, portableRoot, toolsRoot);
        var meta = Read(s.Ordner + ".json") ?? throw new InvalidOperationException("Die Sicherung ist unvollständig.");
        if (meta.Id != s.Id || meta.Id.StartsWith('.') || meta.Id.IndexOfAny(['/', '\\', ':']) >= 0) throw new ArgumentException("Diese Sicherung gehört nicht zu Trendo.");
        // T-0764 (Gegenprüfung N2): ein GitHub-Eintrag aus der Sicherung muss genau zu diesem Ordner passen – gültiges Projekt, gleiche Kennung,
        // Programmdatei relativ im Ordner (eine veränderte Beschreibung kann so keinen fremden Pfad in die Liste schmuggeln)
        if (meta.GitHub is { } g && (meta.Art != Herkunft.GitHub || g.Id != meta.Id || !GitHubApps.ValidRepo(g.Repo) || GitHubApps.IdFor(g.Repo) != g.Id
            || string.IsNullOrWhiteSpace(g.Exe) || Path.IsPathRooted(g.Exe) || g.Exe.Replace('\\', '/').Split('/').Any(x => x is ".." or ".")))
            throw new ArgumentException("Die Sicherung ist verändert – es wird nichts wiederhergestellt.");
        if (meta.Art == Herkunft.GitHub && meta.GitHub is null) throw new ArgumentException("Die Sicherung ist unvollständig – es wird nichts wiederhergestellt.");
        var dest = Path.Combine(root, meta.Id);
        if (Directory.Exists(dest)) throw new InvalidOperationException($"{meta.Name} ist schon wieder eingerichtet – die Sicherung bleibt liegen, es wird nichts überschrieben.");
        Directory.Move(s.Ordner, dest);
        File.Delete(s.Ordner + ".json");
        if (meta.GitHub is { } gh) GitHubApps.Remember(toolsRoot, gh);
        audit.Append(by.Name, "programm.wiederhergestellt", $"{meta.Art} {meta.Id}");
        return List(portableRoot, toolsRoot).FirstOrDefault(e => e.Id == meta.Id && e.Art == meta.Art)
            ?? new(meta.Id, meta.Art, meta.Name, dest, null, meta.Version, "", Size(dest));
    }

    /// <summary>Endgültig löschen: nur Erwachsene; danach ist der Platz frei und nichts mehr wiederherstellbar.</summary>
    public static void Purge(Principal by, Sicherung s, string portableRoot, string toolsRoot, AuditLog audit)
    {
        Rbac.DemandSoftware(by, "programm.loeschen " + s.Id, audit);
        CheckBackup(s, portableRoot, toolsRoot);
        var info = new DirectoryInfo(s.Ordner);
        foreach (var f in info.EnumerateFiles("*", new EnumerationOptions { RecurseSubdirectories = true, AttributesToSkip = FileAttributes.ReparsePoint }))
            if (f.IsReadOnly) f.IsReadOnly = false;
        info.Delete(true);
        File.Delete(s.Ordner + ".json");
        audit.Append(by.Name, "programm.geloescht", $"{s.Art} {s.Id}");
    }
}
