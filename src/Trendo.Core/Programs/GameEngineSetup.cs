using System.Text.RegularExpressions;

namespace Trendo.Core.Programs;

/// <summary>T-0650, E1: Stand der Engine-Einrichtung für ein Spiel – erkannt, Projekt angelegt, Öffnen und Bauen geprüft. Ein Schritt gilt nur als
/// „geprüft“, wenn ein Prüfbericht der Engine im Projekt liegt und darin nichts fehlgeschlagen ist. Fehlt etwas, nennt Trendo genau einen nächsten Schritt.
/// Nichts wird heruntergeladen: fehlt die Engine, ist der Schritt die offizielle Seite.</summary>
public static partial class GameEngineSetup
{
    public const string Open = "offen", Done = "erledigt", Checked = "geprüft", Failed = "fehlgeschlagen";
    public sealed record Step(string Id, string Label, string State, string Evidence);
    /// <summary>Next: genau ein Satz, oder null, wenn alles geprüft ist. Action: was der Knopf tut (install, create, build, check) oder leer.</summary>
    public sealed record Status(string Engine, IReadOnlyList<Step> Steps, string? Next, string Action);

    /// <summary>Projektordner der Engine: Unreal und Browser im gebauten Spiel, Unity und Godot als eigenes Engine-Projekt.</summary>
    public static string? ProjectDir(string engine, string built, string unityProject, string godotProject) => engine switch
    {
        "unity" => unityProject.Length > 0 ? unityProject : null,
        "godot" => godotProject.Length > 0 ? godotProject : null,
        _ => built.Length > 0 ? built : null,
    };

    /// <summary>Liegt im Ordner ein Projekt dieser Engine?</summary>
    public static bool HasProject(string engine, string? dir)
    {
        if (dir is null || !Directory.Exists(dir)) return false;
        return engine switch
        {
            "unreal" => Directory.EnumerateFiles(dir, "*.uproject").Any(),
            "unity" => File.Exists(Path.Combine(dir, "Packages", "manifest.json")) && Directory.Exists(Path.Combine(dir, "Assets")),
            "godot" => File.Exists(Path.Combine(dir, "project.godot")),
            _ => File.Exists(Path.Combine(dir, "spiel.json")),
        };
    }

    [GeneratedRegex(@"(\d+) von (\d+) bestanden")] private static partial Regex Passed();

    /// <summary>Prüfbericht lesen (Trendo-Pruefung/bericht.txt). Geprüft ist es nur, wenn der Bericht Erfolge enthält und nichts fehlschlug.</summary>
    public static (bool Ok, string Summary) ReadReport(string engine, string text)
    {
        var lines = (text ?? "").Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).Skip(1).ToList(); // erste Zeile: Datum
        if (lines.Count == 0) return (false, "Bericht ist leer");
        if (engine == "unreal")
        {
            var ok = lines[0].StartsWith("Alles bereit", StringComparison.Ordinal) && !lines.Any(l => l.StartsWith('✗'));
            return (ok, ok ? "kompiliert, getestet und zur Probe gestartet" : lines.FirstOrDefault(l => l.StartsWith('✗'))?.TrimStart('✗', ' ') ?? lines[0]);
        }
        if (engine == "unity")
        {
            var res = lines.Select(l => Passed().Match(l)).Where(m => m.Success).Select(m => (P: int.Parse(m.Groups[1].Value), T: int.Parse(m.Groups[2].Value))).ToList();
            var ok = res.Count >= 2 && res.All(r => r.T > 0 && r.P == r.T) && !lines.Any(l => l.Contains("kein Ergebnis", StringComparison.Ordinal));
            return (ok, ok ? $"EditMode und PlayMode bestanden ({res.Sum(r => r.P)} Tests)" : lines.FirstOrDefault(l => l.Contains("kein Ergebnis") || (Passed().Match(l) is { Success: true } m && m.Groups[1].Value != m.Groups[2].Value)) ?? "Tests nicht vollständig bestanden");
        }
        if (engine == "godot")
        {
            var checks = lines.Where(l => l.StartsWith("Godot ", StringComparison.Ordinal)).ToList();
            var ok = checks.Count > 0 && checks.All(l => l.Contains(": ✓", StringComparison.Ordinal));
            return (ok, ok ? $"{checks.Count} Godot-Prüfungen bestanden" : checks.FirstOrDefault(l => !l.Contains(": ✓")) ?? "keine Godot-Prüfung im Bericht");
        }
        return (false, "Für den Browser gibt es keinen Engine-Bericht");
    }

    /// <summary>Stand für die gewählte Engine. found: Ergebnis der Erkennung; reportText: Inhalt von Trendo-Pruefung/bericht.txt oder null.</summary>
    public static Status Of(string engine, IReadOnlyList<EngineFound> found, string? projectDir, string? reportText)
    {
        var name = GameTools.NameOf(engine);
        var steps = new List<Step>();
        if (engine == "browser")
        {
            var built = HasProject("browser", projectDir);
            steps.Add(new("erkannt", "Engine", Checked, "Browser – immer vorhanden"));
            steps.Add(new("projekt", "Spiel gebaut", built ? Done : Open, built ? "spiel.json liegt im Spielordner" : "noch nicht gebaut"));
            return new(engine, steps, built ? null : "Klicke auf „Bauen“, dann ist das Spiel im Browser spielbar.", built ? "" : "build");
        }
        var e = found.FirstOrDefault(f => f.Id == engine);
        var installed = e is { Installed: true };
        steps.Add(new("erkannt", "Engine erkannt", installed ? Checked : Open, installed ? $"{e!.Name} {e.Version} – {Path.GetFileName(e.Exe ?? "")}".Trim() : "nicht auf diesem PC gefunden"));
        var project = HasProject(engine, projectDir);
        steps.Add(new("projekt", "Projekt angelegt", project ? Done : Open, project ? Path.GetFileName(projectDir!.TrimEnd('/', '\\')) : "noch kein Projekt"));
        var (ok, summary) = project && reportText is { Length: > 0 } ? ReadReport(engine, reportText) : (false, "");
        steps.Add(new("pruefung", "Öffnen und Bauen", !project || reportText is null ? Open : ok ? Checked : Failed,
            !project ? "erst nach dem Projekt" : reportText is null ? "noch nicht geprüft" : summary));

        // Genau ein nächster Schritt, in dieser Reihenfolge
        var source = EngineTargets.Engines.FirstOrDefault(x => x.Id == engine).Source;
        if (!installed) return new(engine, steps, $"Installiere {name} von der offiziellen Seite ({source}); Trendo lädt keine Engine selbst.", "install");
        if (!project) return new(engine, steps, engine == "unreal" ? $"Klicke auf „Bauen“, dann legt Trendo das {name}-Projekt an." : $"Lege das {name}-Projekt an.", engine == "unreal" ? "build" : "create");
        if (reportText is null) return new(engine, steps, $"Lass {name} das Projekt öffnen und bauen.", "check");
        if (!ok) return new(engine, steps, $"Behebe den gemeldeten Fehler und prüfe noch einmal: {summary}", "check");
        return new(engine, steps, null, "");
    }
}
