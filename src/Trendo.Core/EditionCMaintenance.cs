namespace Trendo.Core;

/// <summary>Edition C: Programm und Benutzerdaten bleiben getrennte Ordner. Update legt die vorige Fassung beiseite, Rollback holt sie zurück.</summary>
public sealed record UninstallPlan(string ProgramDir, string UserDataDir, IReadOnlyList<string> Remove, IReadOnlyList<string> Keep);

public static class EditionCMaintenance
{
    public static UninstallPlan PlanUninstall(string programDir, string userDataDir)
    {
        var program = Path.GetFullPath(programDir);
        var data = Path.GetFullPath(userDataDir);
        if (SameOrInside(program, data) || SameOrInside(data, program))
            throw new InvalidOperationException("Programm und Benutzerdaten liegen im selben Ordner. Es wurde nichts gelöscht.");
        static List<string> Names(string dir) => Directory.Exists(dir)
            ? Directory.EnumerateFileSystemEntries(dir).Select(Path.GetFileName).OfType<string>().OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList()
            : [];
        return new(program, data, Names(program), Names(data));
    }

    /// <summary>Löscht nur den Programmordner. Der Benutzerdatenordner muss vorher und nachher da sein.</summary>
    public static void ApplyUninstall(UninstallPlan plan)
    {
        if (SameOrInside(plan.ProgramDir, plan.UserDataDir) || SameOrInside(plan.UserDataDir, plan.ProgramDir))
            throw new InvalidOperationException("Programm und Benutzerdaten liegen im selben Ordner. Es wurde nichts gelöscht.");
        if (!Directory.Exists(plan.UserDataDir))
            throw new InvalidOperationException("Der Ordner mit deinen Dateien fehlt. Es wurde nichts gelöscht.");
        if (Directory.Exists(plan.ProgramDir)) Directory.Delete(plan.ProgramDir, true);
        if (!Directory.Exists(plan.UserDataDir))
            throw new InvalidOperationException("Deine Dateien sind weg. Das darf bei der Deinstallation nicht passieren.");
    }

    /// <summary>Legt die bisherige Fassung nach „previous“ und schreibt die neue nach „current“.</summary>
    public static void StageUpdate(string installRoot, string version, IReadOnlyDictionary<string, string> files)
    {
        if (string.IsNullOrWhiteSpace(version) || version.Contains('/') || version.Contains('\\') || version.Contains("..", StringComparison.Ordinal))
            throw new ArgumentException("Versionsname ungültig.");
        foreach (var name in files.Keys)
        {
            if (name == "version.txt") continue;
            if (name.Length == 0 || name.Contains("..", StringComparison.Ordinal) || name.IndexOfAny(['/', '\\']) >= 0 || Path.IsPathRooted(name))
                throw new ArgumentException("Dateiname ungültig.");
        }
        var current = Path.Combine(installRoot, "current");
        var previous = Path.Combine(installRoot, "previous");
        Directory.CreateDirectory(installRoot);
        if (Directory.Exists(previous)) Directory.Delete(previous, true);
        if (Directory.Exists(current)) Directory.Move(current, previous);
        Directory.CreateDirectory(current);
        foreach (var (name, text) in files)
            if (name != "version.txt") File.WriteAllText(Path.Combine(current, name), text);
        File.WriteAllText(Path.Combine(current, "version.txt"), version.Trim() + "\n");
    }

    public static string CurrentVersion(string installRoot) =>
        File.ReadAllText(Path.Combine(installRoot, "current", "version.txt")).Trim();

    /// <summary>Holt die vorige Fassung zurück. Ohne „previous“ passiert nichts.</summary>
    public static string Rollback(string installRoot)
    {
        var current = Path.Combine(installRoot, "current");
        var previous = Path.Combine(installRoot, "previous");
        if (!Directory.Exists(previous)) throw new InvalidOperationException("Keine ältere Fassung zum Zurückholen.");
        var failed = Path.Combine(installRoot, "failed");
        if (Directory.Exists(failed)) Directory.Delete(failed, true);
        if (Directory.Exists(current)) Directory.Move(current, failed);
        Directory.Move(previous, current);
        return CurrentVersion(installRoot);
    }

    private static bool SameOrInside(string parent, string child)
    {
        var p = Path.GetFullPath(parent).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var c = Path.GetFullPath(child).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return c.StartsWith(p, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
    }
}
