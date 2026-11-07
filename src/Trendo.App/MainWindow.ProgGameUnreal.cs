using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Trendo.Core;
using Trendo.Core.Programs;

namespace Trendo.App;

/// <summary>3D-Spiele in Unreal ohne Fachwissen (Nutzerwunsch 07.11.2026): Welt wählen (3D-Level oder Open World), Trendo kompiliert das C++-Spiel,
/// lässt die Unreal-Tests laufen, startet das Spiel zur Probe (mit Bildschirmfoto) und zeigt einen verständlichen Bericht. Alles über die offiziellen Kommandozeilen.</summary>
public partial class MainWindow
{
    /// <summary>Für Tests: Unreal-Lauf (Programm, Argumente) → (Exitcode, Protokoll) statt echtem Prozess.</summary>
    public Func<string, IReadOnlyList<string>, (int Exit, string Log)>? UnrealRunOverride { get; set; }
    /// <summary>Für Tests: Unreal-Ordner statt Suche.</summary>
    public string? UnrealEngineOverride { get; set; }
    public string LastUnrealReport { get; private set; } = "";
    public bool UnrealBusy { get; private set; }

    string? UnrealEngineDir() => UnrealEngineOverride ?? (_opt.Isolated ? null : GameStudio.FindEngines().FirstOrDefault(e => File.Exists(GameStudio.EditorExe(e)))?.Path);

    (int Exit, string Log) UnrealRun(string exe, IReadOnlyList<string> args, string logFile, int timeoutMs)
    {
        if (UnrealRunOverride is { } o) { var r = o(exe, args); try { File.WriteAllText(logFile, r.Log); } catch (IOException) { } return r; }
        var run = Cli.Run(exe, args, Path.GetDirectoryName(logFile), timeoutMs);
        var log = File.Exists(logFile) ? File.ReadAllText(logFile) : "";
        if (log.Length == 0) { log = run.Out + "\n" + run.Err; File.WriteAllText(logFile, log); }
        return (run.Exit, log);
    }

    /// <summary>Kompilieren, Unreal-Tests, Startlauf – Schritt für Schritt mit Meldung; Bericht im Projektordner „Trendo-Pruefung“.</summary>
    public async Task GameUnrealCheck(ProgramDocument d, bool screenshot = true)
    {
        var built = d.Text("built");
        var uproject = built.Length > 0 && Directory.Exists(built) ? Directory.GetFiles(built, "*.uproject").FirstOrDefault() : null;
        if (uproject is null) { _progMsg[GK] = "Bitte zuerst „Bauen“ – dann prüft Trendo das Unreal-Spiel."; Show(GK); return; }
        if (UnrealEngineDir() is not { } engine) { _progMsg[GK] = "Unreal Engine 5 fehlt. Sie ist kostenlos und kommt über den Epic Games Launcher – danach hier noch einmal „Kompilieren und prüfen“."; Show(GK); return; }
        if (UnrealBusy) return;
        UnrealBusy = true;
        var logs = Path.Combine(built, "Trendo-Pruefung"); Directory.CreateDirectory(logs);
        var lines = new List<string>(); var ok = true;
        void Step(string t) { _progMsg[GK] = t; _ui.Post(() => { if (CurrentView == GK) Show(GK); }); }
        try
        {
            Step("1/3 Unreal kompiliert das Spiel (C++) … beim ersten Mal einige Minuten.");
            var (ce, clog) = await Task.Run(() => UnrealRun(UnrealGame.BuildBat(engine), UnrealGame.CompileArgs(uproject), Path.Combine(logs, "kompilieren.txt"), 1_800_000));
            if (!UnrealGame.CompileOk(clog)) { ok = false; lines.Add($"✗ Kompilieren fehlgeschlagen (Exitcode {ce}) – Protokoll kompilieren.txt"); }
            else
            {
                lines.Add($"✓ Kompiliert (Exitcode {ce})");
                Step("2/3 Unreal-Tests laufen (Daten, Regeln, Spielstand) …");
                var (_, tlog) = await Task.Run(() => UnrealRun(UnrealGame.EditorCmd(engine), UnrealGame.TestArgs(uproject, Path.Combine(logs, "tests.txt")), Path.Combine(logs, "tests.txt"), 900_000));
                var tests = UnrealGame.ParseTests(tlog);
                ok &= tests.Count > 0 && tests.All(t => t.Ok);
                lines.Add(tests.Count == 0 ? "✗ Keine Unreal-Tests gefunden – Protokoll tests.txt" : $"{(tests.All(t => t.Ok) ? "✓" : "✗")} Unreal-Tests: {tests.Count(t => t.Ok)} von {tests.Count} bestanden ({string.Join(", ", tests.Select(t => t.Test.Replace("Trendo.", "") + (t.Ok ? "" : " ✗")))})");
                Step("3/3 Das Spiel startet zur Probe" + (screenshot ? " und macht ein Bild …" : " …"));
                var (_, glog) = await Task.Run(() => UnrealRun(UnrealGame.Editor(engine), UnrealGame.CheckArgs(uproject, Path.Combine(logs, "start.txt"), screenshot && !_opt.Isolated), Path.Combine(logs, "start.txt"), 900_000));
                var (gok, gtext) = UnrealGame.ParseCheck(glog);
                ok &= gok; lines.Add((gok ? "✓ Spiel läuft: " : "✗ Spiel: ") + gtext);
            }
        }
        catch (Exception e) when (e is IOException or InvalidOperationException or System.ComponentModel.Win32Exception) { ok = false; lines.Add("✗ " + e.Message); }
        finally { UnrealBusy = false; }
        LastUnrealReport = (ok ? "Alles bereit – „Spielen“ startet dein 3D-Spiel." : "Nicht alles ging – Details unten.") + "\n" + string.Join("\n", lines);
        File.WriteAllText(Path.Combine(logs, "bericht.txt"), $"{DateTime.Now:yyyy-MM-dd HH:mm} {UnrealGame.WorldOf(d.Data)}\n{LastUnrealReport}\n");
        _core.Audit.Append(Principal.LocalOwner.Name, "game.unreal.check", (ok ? "ok" : "fehler") + " " + Path.GetFileName(built));
        _progMsg[GK] = LastUnrealReport; _ui.Post(() => { if (CurrentView == GK) Show(GK); });
    }

    /// <summary>Spiel in Unreal starten (ohne Editor).</summary>
    public void GameUnrealPlay(ProgramDocument d)
    {
        var built = d.Text("built");
        var uproject = built.Length > 0 && Directory.Exists(built) ? Directory.GetFiles(built, "*.uproject").FirstOrDefault() : null;
        if (uproject is null) throw new InvalidOperationException("Bitte zuerst „Bauen“.");
        if (UnrealEngineDir() is not { } engine) throw new InvalidOperationException("Unreal Engine 5 fehlt – kostenlos über den Epic Games Launcher.");
        if (UnrealRunOverride is { } o) { o(UnrealGame.Editor(engine), UnrealGame.PlayArgs(uproject)); _progMsg[GK] = "Spiel gestartet."; return; }
        if (_opt.Isolated) { _progMsg[GK] = "Testmodus: Spiel nicht gestartet."; return; }
        var psi = new System.Diagnostics.ProcessStartInfo(UnrealGame.Editor(engine)) { UseShellExecute = false };
        foreach (var a in UnrealGame.PlayArgs(uproject)) psi.ArgumentList.Add(a);
        System.Diagnostics.Process.Start(psi);
        _progMsg[GK] = "Dein 3D-Spiel startet – Esc öffnet die Pause.";
    }

    private Control GameUnrealView(ProgramDocument d)
    {
        var sp = new StackPanel { Name = "game-unreal", Spacing = 6, Margin = new Thickness(0, 10, 0, 0) };
        sp.Children.Add(H("3D-SPIEL IN UNREAL", 12.5));
        var world = UnrealGame.WorldOf(d.Data);
        var pick = new ComboBox { Name = "game-world", ItemsSource = UnrealGame.Worlds.Select(w => w.Label).ToList(), SelectedIndex = Math.Max(0, Array.FindIndex(UnrealGame.Worlds, w => w.Id == world)), MinWidth = 320 };
        pick.SelectionChanged += (_, _) => { var v = UnrealGame.Worlds[Math.Max(0, pick.SelectedIndex)].Id; if (v != world) ProgramRun(GK, () => d.Set("world", v, "Welt")); };
        sp.Children.Add(Row(P("Welt", Muted, 11.5), pick));
        sp.Children.Add(P("„Bauen“ schreibt ein spielbares C++-Spiel mit deinen Leveln, Regeln, Gegnern, Gegenständen, Fahrzeugen, Spielstand und Menü. „Kompilieren und prüfen“ erledigt Unreal für dich: übersetzen, testen, zur Probe starten.", null, 12));
        var check = Act("game-ue-check", UnrealBusy ? "Prüfung läuft …" : "Kompilieren und prüfen", () => _ = GameUnrealCheck(d), true); check.IsEnabled = !UnrealBusy;
        var play = Act("game-ue-play", "Spielen (Unreal)", () => ProgramRun(GK, () => GameUnrealPlay(d)));
        sp.Children.Add(new WrapPanel { Children = { check, new Border { Width = 6 }, play } });
        var built = d.Text("built");
        if (LastUnrealReport.Length > 0) sp.Children.Add(new TextBlock { Name = "game-ue-report", Text = LastUnrealReport, Foreground = Text, FontFamily = new FontFamily("Consolas, monospace"), FontSize = 11.5, TextWrapping = TextWrapping.Wrap });
        var shot = built.Length > 0 ? Path.Combine(built, "Saved", "Screenshots", "trendo-start.png") : "";
        if (shot.Length > 0 && File.Exists(shot))
            try { sp.Children.Add(new Border { CornerRadius = new CornerRadius(8), ClipToBounds = true, MaxWidth = 560, HorizontalAlignment = HorizontalAlignment.Left, Child = new Image { Name = "game-ue-shot", Source = new Bitmap(shot), Stretch = Stretch.Uniform } }); }
            catch (Exception e) when (e is IOException or ArgumentException) { }
        return sp;
    }
}
