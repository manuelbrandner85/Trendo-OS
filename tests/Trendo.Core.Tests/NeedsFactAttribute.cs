using Trendo.Core;
using Xunit;

/// <summary>T-0244: Test, der ein Programm braucht. Fehlt es, meldet xUnit den Test als „übersprungen“ – statt ihn still bestehen zu lassen.
/// „a|b“ = eines davon genügt; Einträge mit / oder \ sind Dateipfade, „repo:…“ ist ein Pfad relativ zum Projektordner.</summary>
public sealed class NeedsFactAttribute : FactAttribute
{
    public NeedsFactAttribute(params string[] needs)
    {
        var missing = needs.Where(n => !n.Split('|').Any(Available)).ToList();
        if (missing.Count > 0) Skip = "Übersprungen – fehlt auf diesem PC: " + string.Join(", ", missing);
    }

    static bool Available(string n)
    {
        if (n.StartsWith("repo:", StringComparison.Ordinal))
        {
            for (var d = new DirectoryInfo(AppContext.BaseDirectory); d is not null; d = d.Parent)
                if (File.Exists(Path.Combine(d.FullName, "Trendo.sln"))) return File.Exists(Path.Combine(d.FullName, n[5..]));
            return false;
        }
        return n.Contains('/') || n.Contains('\\') ? File.Exists(n) : Cli.Find(n) is not null;
    }
}
