namespace Trendo.Core;

/// <summary>Benutzerdaten liegen getrennt vom Programmordner, damit Update/Deinstallation sie nicht löschen.</summary>
public static class DataLocation
{
    /// <summary>Einträge im alten Ordner, die zu Velopack gehören und NICHT migriert werden.</summary>
    public static readonly string[] InstallerEntries = ["current", "packages", "Update.exe", ".portable", "sq.version", "app.ico", "velopack.log"];

    public static string Legacy() => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TrendoOS");
    public static string Default() => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TrendoOS");

    /// <summary>Verschiebt alte Benutzerdaten (v0.3–v0.5) in den neuen Ordner. Überschreibt nichts. Gibt Anzahl verschobener Einträge zurück.</summary>
    public static int MigrateLegacy(string legacyRoot, string newRoot)
    {
        if (!Directory.Exists(legacyRoot) || Path.GetFullPath(legacyRoot) == Path.GetFullPath(newRoot)) return 0;
        Directory.CreateDirectory(newRoot);
        var n = 0;
        foreach (var e in Directory.EnumerateFileSystemEntries(legacyRoot))
        {
            var name = Path.GetFileName(e);
            if (InstallerEntries.Any(x => string.Equals(x, name, StringComparison.OrdinalIgnoreCase)) || name.StartsWith("velopack", StringComparison.OrdinalIgnoreCase)
                || (File.Exists(e) && name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))) continue; // Velopack-Starter (z. B. „Trendo OS.exe“) bleibt
            var dest = Path.Combine(newRoot, name);
            if (File.Exists(dest) || Directory.Exists(dest)) continue;
            if (Directory.Exists(e)) Directory.Move(e, dest); else File.Move(e, dest);
            n++;
        }
        if (n > 0) File.WriteAllText(Path.Combine(newRoot, "MIGRATION.txt"), $"{n} Einträge aus dem alten Programmordner übernommen ({DateTime.Now:yyyy-MM-dd HH:mm}).\n");
        return n;
    }

    /// <summary>Unterordner mit eigenen Projekten: bleiben beim Löschen der Daten immer erhalten.</summary>
    public static readonly string[] ProjectFolders = ["projects", "projekte"];

    /// <summary>Beim Deinstallieren auf Wunsch: Einstellungen, Protokolle, Sicherungen usw. im Trendo-Datenordner löschen.
    /// Nie Laufwerks-, Benutzer- oder Systemordner, nie wenn der Projektordner darin liegt, nie Projekt-Unterordner,
    /// nie über Verknüpfungen (Junction/Symlink) hinaus. Gibt die Zahl gelöschter Einträge zurück, -1 = abgelehnt.</summary>
    public static int RemoveUserData(string root, params string?[] keep)
    {
        if (string.IsNullOrWhiteSpace(root) || !Path.IsPathFullyQualified(root)) return -1;
        var full = Norm(root);
        // Doppelter Schutz: nie eine Laufwerkswurzel oder einen flachen Pfad, und nur ein Ordner, der „Trendo…“ heißt
        if (Path.GetPathRoot(full) is not { } drive || Norm(drive) == full || full.Count(c => c == Path.DirectorySeparatorChar) < 2) return -1;
        if (!Path.GetFileName(full).StartsWith("trendo", StringComparison.OrdinalIgnoreCase)) return -1;
        var dir = new DirectoryInfo(full);
        if (!dir.Exists || dir.Attributes.HasFlag(FileAttributes.ReparsePoint) || dir.LinkTarget is not null) return -1;
        var forbidden = new[] { Environment.SpecialFolder.UserProfile, Environment.SpecialFolder.ApplicationData, Environment.SpecialFolder.LocalApplicationData,
            Environment.SpecialFolder.MyDocuments, Environment.SpecialFolder.Desktop, Environment.SpecialFolder.Windows, Environment.SpecialFolder.ProgramFiles }
            .Select(Environment.GetFolderPath).Where(p => p.Length > 0 && Path.IsPathFullyQualified(p)).Select(Norm);
        if (forbidden.Any(f => string.Equals(f, full, StringComparison.OrdinalIgnoreCase))) return -1;
        foreach (var k in keep.Where(k => !string.IsNullOrWhiteSpace(k) && Path.IsPathFullyQualified(k!)).Select(k => Norm(k!)))
            if (k.StartsWith(full + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) || string.Equals(k, full, StringComparison.OrdinalIgnoreCase)) return -1;
        var n = 0;
        foreach (var e in dir.EnumerateFileSystemInfos())
        {
            if (e is DirectoryInfo && ProjectFolders.Any(p => string.Equals(p, e.Name, StringComparison.OrdinalIgnoreCase))) continue; // Projekte bleiben
            if (e.Attributes.HasFlag(FileAttributes.ReparsePoint)) { e.Delete(); n++; continue; } // nur die Verknüpfung, nie ihr Ziel
            if (e is DirectoryInfo d) { ClearReadOnly(d); d.Delete(true); } else { ClearReadOnly(e); e.Delete(); }
            n++;
        }
        if (!dir.EnumerateFileSystemInfos().Any()) dir.Delete();
        return n;
    }

    /// <summary>Nur „schreibgeschützt“ entfernen, nur innerhalb des Ordners, keine Verknüpfungen verfolgen.</summary>
    static void ClearReadOnly(FileSystemInfo e)
    {
        if (e.Attributes.HasFlag(FileAttributes.ReadOnly)) e.Attributes &= ~FileAttributes.ReadOnly;
        if (e is not DirectoryInfo d) return;
        var opt = new EnumerationOptions { RecurseSubdirectories = true, AttributesToSkip = FileAttributes.ReparsePoint, IgnoreInaccessible = false };
        foreach (var f in d.EnumerateFileSystemInfos("*", opt))
            if (f.Attributes.HasFlag(FileAttributes.ReadOnly)) f.Attributes &= ~FileAttributes.ReadOnly;
    }

    /// <summary>Voller Pfad ohne abschließendes Trennzeichen – außer bei der Laufwerkswurzel („C:\“ bleibt „C:\“, nie „C:“).</summary>
    static string Norm(string p)
    {
        var f = Path.GetFullPath(p);
        var r = Path.GetPathRoot(f) ?? "";
        return f.Length > r.Length ? f.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) : f;
    }
}
