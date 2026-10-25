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

    /// <summary>Beim Deinstallieren auf Wunsch: Trendo-Datenordner löschen (Einstellungen, Protokolle, Sicherungen).
    /// Löscht nie Laufwerks- oder Benutzerordner und nie einen Ordner, in dem eigene Projekte liegen. Gibt zurück, ob gelöscht wurde.</summary>
    public static bool RemoveUserData(string root, params string?[] keep)
    {
        if (string.IsNullOrWhiteSpace(root) || !Path.IsPathFullyQualified(root)) return false;
        var full = Norm(root);
        // Doppelter Schutz: nie eine Laufwerkswurzel oder einen flachen Pfad, und nur ein Ordner, der „Trendo…“ heißt
        if (Path.GetPathRoot(full) is not { } drive || Norm(drive) == full || full.Count(c => c == Path.DirectorySeparatorChar) < 2) return false;
        if (!Path.GetFileName(full).StartsWith("trendo", StringComparison.OrdinalIgnoreCase)) return false;
        if (!Directory.Exists(full)) return false;
        var forbidden = new[] { Environment.SpecialFolder.UserProfile, Environment.SpecialFolder.ApplicationData, Environment.SpecialFolder.LocalApplicationData,
            Environment.SpecialFolder.MyDocuments, Environment.SpecialFolder.Desktop, Environment.SpecialFolder.Windows, Environment.SpecialFolder.ProgramFiles }
            .Select(Environment.GetFolderPath).Where(p => p.Length > 0 && Path.IsPathFullyQualified(p)).Select(Norm);
        if (forbidden.Any(f => string.Equals(f, full, StringComparison.OrdinalIgnoreCase))) return false;
        foreach (var k in keep.Where(k => !string.IsNullOrWhiteSpace(k) && Path.IsPathFullyQualified(k!)).Select(k => Norm(k!)))
            if (k.StartsWith(full + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) || string.Equals(k, full, StringComparison.OrdinalIgnoreCase)) return false;
        foreach (var f in Directory.EnumerateFiles(full, "*", SearchOption.AllDirectories)) File.SetAttributes(f, FileAttributes.Normal);
        Directory.Delete(full, true);
        return true;
    }

    /// <summary>Voller Pfad ohne abschließendes Trennzeichen – außer bei der Laufwerkswurzel („C:\“ bleibt „C:\“, nie „C:“).</summary>
    static string Norm(string p)
    {
        var f = Path.GetFullPath(p);
        var r = Path.GetPathRoot(f) ?? "";
        return f.Length > r.Length ? f.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) : f;
    }
}
