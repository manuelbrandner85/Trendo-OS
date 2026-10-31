using Trendo.Core;
using Xunit;

/// <summary>T-0244: Test, der ein Programm braucht. Fehlt es, meldet xUnit den Test als „übersprungen“ – statt ihn still bestehen zu lassen.
/// „a|b“ = eines davon genügt; Einträge mit / oder \ sind Dateipfade, „repo:…“ ist ein Pfad relativ zum Projektordner,
/// „dep:…“ sucht wie Trendo selbst (PATH, bekannte Installationsorte, Werkzeugordner) über den DependencyCatalog.</summary>
public sealed class NeedsFactAttribute : FactAttribute
{
    public NeedsFactAttribute(params string[] needs)
    {
        var missing = needs.Where(n => !n.Split('|').Any(Available)).ToList();
        if (missing.Count > 0) Skip = "Übersprungen – fehlt auf diesem PC: " + string.Join(", ", missing);
    }

    /// <summary>Programm so finden, wie Trendo es findet (nicht nur über PATH).</summary>
    public static string? Tool(string id) =>
        Cli.Find(id) ?? new DependencyManager(new AuditLog(Path.Combine(Path.GetTempPath(), "trendo-needs-" + Environment.ProcessId + ".jsonl")), new RealProcessRunner()).ResolvePath(DependencyCatalog.Get(id));

    static bool Available(string n)
    {
        if (n.StartsWith("repo:", StringComparison.Ordinal))
        {
            for (var d = new DirectoryInfo(AppContext.BaseDirectory); d is not null; d = d.Parent)
                if (File.Exists(Path.Combine(d.FullName, "Trendo.sln"))) return File.Exists(Path.Combine(d.FullName, n[5..]));
            return false;
        }
        if (n.StartsWith("dep:", StringComparison.Ordinal)) return Tool(n[4..]) is not null;
        return n.Contains('/') || n.Contains('\\') ? File.Exists(n) : Cli.Find(n) is not null;
    }
}
