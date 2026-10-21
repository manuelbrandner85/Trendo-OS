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
}
