using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Trendo.Core;
using Trendo.Core.Programs;

namespace Trendo.App;

/// <summary>Paket 01 / S1-01 „Universal Game Engine“ mit Unity (Freigabe 05.11.2026): Engines ehrlich prüfen (Unreal, Unity, Godot, Blender),
/// das Trendo-Spiel als Unity- oder Godot-Projekt anlegen und – nur wenn die Engine wirklich da ist – deren Tests über die offizielle Kommandozeile laufen lassen.
/// Nichts wird heruntergeladen oder installiert.</summary>
public partial class MainWindow
{
    /// <summary>Für Tests: Engine-Funde statt echter Suche.</summary>
    public Func<List<EngineFound>>? EngineProbeOverride { get; set; }
    /// <summary>Für Tests: Engine-Lauf (Programm, Argumente) → (Exitcode, Ausgabe) statt echtem Prozess.</summary>
    public Func<string, IReadOnlyList<string>, (int Exit, string Output)>? EngineRunOverride { get; set; }
    public string? LastEngineProject { get; private set; }
    public string LastEngineReport { get; private set; } = "";

    public List<EngineFound> EngineCheck()
    {
        if (EngineProbeOverride is { } o) return o();
        if (_opt.Isolated) return EngineTargets.Summary([], _ => "", [], [], null, _ => false);
        return EngineDetect();
    }

    /// <summary>Echte Suche auf diesem PC (nur Dateien lesen, nichts starten).</summary>
    public List<EngineFound> EngineDetect()
    {
        var pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles); var app = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        static IEnumerable<string> Dirs(string d) { try { return Directory.Exists(d) ? Directory.EnumerateDirectories(d) : []; } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return []; } }
        static IEnumerable<string> Files(string d) { try { return Directory.Exists(d) ? Directory.EnumerateFiles(d, "Godot_v4*", SearchOption.TopDirectoryOnly) : []; } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return []; } }
        static string? Read(string f) { try { return File.Exists(f) ? File.ReadAllText(f) : null; } catch (IOException) { return null; } }
        var unity = EngineTargets.FindUnity(EngineTargets.UnityRoots(pf, app, Read), Dirs, File.Exists);
        var godot = EngineTargets.FindGodot([Path.Combine(_deps.ToolsRoot, "godot"), Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Godot"), Path.Combine(local, "Programs", "Godot"), Path.Combine(pf, "Godot")], Files);
        var unreal = GameStudio.FindEngines().Select(e => (e.Version, e.Path)).ToList();
        var blender = Cli.Find("blender") ?? Dirs(Path.Combine(pf, "Blender Foundation")).Select(d => Path.Combine(d, "blender.exe")).FirstOrDefault(File.Exists);
        return EngineTargets.Summary(unreal, p => GameStudio.EditorExe(new GameStudio.Engine("", p)), unity, godot, blender, File.Exists);
    }

    (int Exit, string Output) EngineRun(string exe, IReadOnlyList<string> args, string cwd, int timeoutMs)
    {
        if (EngineRunOverride is { } o) return o(exe, args);
        if (_opt.Isolated) return (-1, "Testmodus: keine Engine gestartet.");
        var r = Cli.Run(exe, args, cwd, timeoutMs);
        return (r.Exit, r.Out + "\n" + r.Err);
    }

    /// <summary>Trendo-Spiel (gebauter Plattformer) als Unity- oder Godot-Projekt anlegen.</summary>
    public string GameToEngine(ProgramDocument d, string engine)
    {
        var built = d.Text("built");
        if (d.Text("browser") != "plattform") throw new InvalidOperationException("Als Engine-Projekt geht bisher der Plattformer (Level, Figur, Regeln).");
        if (built.Length == 0 || !File.Exists(Path.Combine(built, "spiel.json"))) throw new InvalidOperationException("Bitte zuerst „Bauen“ – das Engine-Projekt entsteht aus dem fertigen Spiel.");
        var s = GameStudio.ReadSettings(built); var name = d.Text("title", "Spiel");
        var parent = Path.Combine(WorkFolder("Spiele"), "Engine-Projekte"); Directory.CreateDirectory(parent);
        var unityVersion = engine == "unity" ? EngineCheck().FirstOrDefault(e => e.Id == "unity" && e.Installed)?.Version : null;
        var dir = engine == "unity" ? EngineTargets.CreateUnityProject(parent, name, s, unityVersion) : EngineTargets.CreateGodotProject(parent, name, s);
        LastEngineProject = dir; LastEngineReport = "";
        d.Set(engine + "Project", dir, engine == "unity" ? "Unity-Projekt" : "Godot-Projekt");
        _core.Audit.Append(Principal.LocalOwner.Name, "game.engine." + engine, Path.GetFileName(dir));
        _progMsg[GK] = $"{(engine == "unity" ? "Unity" : "Godot")}-Projekt angelegt: {dir}";
        return dir;
    }

    /// <summary>Tests der Engine über die offizielle Kommandozeile – nur wenn die Engine installiert ist; Ergebnis mit Exitcode und Protokoll im Projektordner.</summary>
    public void GameEngineTest(ProgramDocument d, string engine)
    {
        var dir = d.Text(engine + "Project");
        if (dir.Length == 0 || !Directory.Exists(dir)) throw new InvalidOperationException("Zuerst das Projekt anlegen.");
        var e = EngineCheck().FirstOrDefault(x => x.Id == engine);
        if (e is not { Installed: true, Exe: { } exe }) { _progMsg[GK] = engine == "unity" ? "Blockiert: Unity ist nicht installiert. Unity Hub und Editor installierst du selbst (Anmeldung bei Unity) – Trendo lädt nichts." : "Godot wurde nicht gefunden – Godot_v4…exe in den Werkzeugordner „godot“ legen."; LastEngineReport = "BLOCKED"; return; }
        var logs = Path.Combine(dir, "Trendo-Pruefung"); Directory.CreateDirectory(logs);
        var lines = new List<string>();
        if (engine == "unity")
        {
            foreach (var platform in new[] { "EditMode", "PlayMode" })
            {
                var xml = Path.Combine(logs, platform + "-ergebnis.xml"); var log = Path.Combine(logs, platform + "-protokoll.txt");
                var (exit, _) = EngineRun(exe, EngineTargets.UnityTestArgs(dir, platform, xml, log), dir, 1_800_000);
                var res = File.Exists(xml) ? EngineTargets.ParseUnityResults(File.ReadAllText(xml)) : null;
                lines.Add(res is { } r ? $"{platform}: {r.Passed} von {r.Total} bestanden, {r.Failed} fehlgeschlagen (Exitcode {exit})" : $"{platform}: kein Ergebnis (Exitcode {exit}) – Protokoll {Path.GetFileName(log)}");
            }
        }
        else
        {
            EngineRun(exe, EngineTargets.GodotImportArgs(dir), dir, 600_000);
            foreach (var (label, args, file) in new[] { ("Level-Prüfung", EngineTargets.GodotCheckArgs(dir), "godot-level.txt"), ("Spiel-Prüfung", EngineTargets.GodotPlayArgs(dir), "godot-spiel.txt"), ("Spielstand-Prüfung", EngineTargets.GodotSaveArgs(dir), "godot-spielstand.txt") })
            {
                var (exit, output) = EngineRun(exe, args, dir, 300_000);
                File.WriteAllText(Path.Combine(logs, file), output);
                var (ok, text) = EngineTargets.ParseGodotCheck(output);
                lines.Add($"Godot {label}: {(ok ? "✓ " : "⚠ ")}{text} (Exitcode {exit})");
            }
        }
        LastEngineReport = string.Join("\n", lines);
        File.WriteAllText(Path.Combine(logs, "bericht.txt"), $"{DateTime.Now:yyyy-MM-dd HH:mm} {e.Name} {e.Version}\n{LastEngineReport}\n");
        _core.Audit.Append(Principal.LocalOwner.Name, "game.engine.test", $"{engine}: {LastEngineReport.Replace('\n', ' ')}");
        _progMsg[GK] = LastEngineReport;
    }

    private Control GameEnginesView(ProgramDocument d)
    {
        var sp = new StackPanel { Name = "game-engines", Spacing = 6, Margin = new Thickness(0, 10, 0, 0) };
        sp.Children.Add(H("ENGINES", 12.5));
        sp.Children.Add(P("Dein Spiel auch als echtes Projekt für Unity oder Godot – gleiche Level, Figur und Regeln. Trendo installiert keine Engine; der Zustand kommt aus der Prüfung auf diesem PC.", null, 12));
        var engines = EngineCheck();
        foreach (var e in engines)
        {
            var ok = e.Installed;
            sp.Children.Add(new Border
            {
                Name = "game-engine-" + e.Id, Background = PaneBg, BorderBrush = PaneEdge, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6), Padding = new Thickness(10, 6),
                Child = new DockPanel { Children = {
                    Dk(new TextBlock { Text = ok ? "● " + (e.Version ?? "bereit") : "○ " + e.Status, Foreground = ok ? Brush.Parse("#2FBF71") : Brush.Parse("#F5A524"), FontFamily = new FontFamily("Consolas, monospace"), FontSize = 12, VerticalAlignment = VerticalAlignment.Center }, Avalonia.Controls.Dock.Right),
                    new StackPanel { Children = { new TextBlock { Text = e.Name, Foreground = Text, FontWeight = FontWeight.SemiBold, FontSize = 12.5 }, new TextBlock { Text = e.Hint, Foreground = Muted, FontSize = 11, TextWrapping = TextWrapping.Wrap } } } } },
            });
        }
        if (engines.FirstOrDefault(e => e.Id == "unity") is { Installed: false })
            sp.Children.Add(Act("game-unity-get", "Unity Hub (offizielle Seite)", () => Launch("https://unity.com/download")));
        var unityDir = d.Text("unityProject"); var godotDir = d.Text("godotProject");
        Button Btn(string name, string label, Action go, bool accent = false) { var b = Act(name, label, go, accent); b.Margin = new Thickness(0, 0, 6, 6); return b; }
        var row = new WrapPanel
        {
            Children =
            {
                Btn("game-to-unity", "Als Unity-Projekt", () => ProgramRun(GK, () => GameToEngine(d, "unity")), true),
                Btn("game-to-godot", "Als Godot-Projekt", () => ProgramRun(GK, () => GameToEngine(d, "godot")), true),
            }
        };
        if (unityDir.Length > 0) { row.Children.Add(Btn("game-unity-test", "Unity-Tests (EditMode + PlayMode)", () => ProgramRun(GK, () => GameEngineTest(d, "unity")))); row.Children.Add(Btn("game-unity-open", "Ordner", () => Launch(unityDir))); }
        if (godotDir.Length > 0) { row.Children.Add(Btn("game-godot-test", "Godot prüfen", () => ProgramRun(GK, () => GameEngineTest(d, "godot")))); row.Children.Add(Btn("game-godot-open", "Ordner", () => Launch(godotDir))); }
        sp.Children.Add(row);
        var caps = EngineTargets.UnityCapStates(engines.Any(e => e.Id == "unity" && e.Installed), LastEngineReport.Contains("PlayMode") && !LastEngineReport.Contains("kein Ergebnis") && !LastEngineReport.Contains(" 0 von"));
        var capText = string.Join("  ", caps.Select(kv => $"{kv.Key.Replace("unity.", "")}: {EngineTargets.CapLabel(kv.Value)}"));
        sp.Children.Add(new TextBlock { Name = "game-unity-caps", Text = "Unity-Fähigkeiten – " + capText, Foreground = Muted, FontSize = 10.5, TextWrapping = TextWrapping.Wrap });
        if (LastEngineReport.Length > 0 && LastEngineReport != "BLOCKED") sp.Children.Add(new TextBlock { Name = "game-engine-report", Text = LastEngineReport, Foreground = Text, FontFamily = new FontFamily("Consolas, monospace"), FontSize = 11.5, TextWrapping = TextWrapping.Wrap });
        return sp;
    }
}
