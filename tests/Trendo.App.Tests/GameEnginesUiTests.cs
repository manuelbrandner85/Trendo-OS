using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Trendo.App;
using Trendo.Core.Programs;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>Paket 01 / S1-01 Universal Game Engine mit Unity: Engines im Bereich „Bauen“, Plattformer als Unity- und Godot-Projekt, Tests nur mit vorhandener Engine.</summary>
public class GameEngines_Ui
{
    const string GK = "spiele-programm";
    static void Pump() { for (var i = 0; i < 5; i++) Dispatcher.UIThread.RunJobs(); }

    [AvaloniaFact(DisplayName = "Spiele Engines: ohne Unity „blockiert“ und Unity-Tests verweigert; Plattformer bauen → Unity- und Godot-Projekt; mit Engines laufen EditMode/PlayMode bzw. die Godot-Prüfung mit Bericht")]
    public void Engines_flow()
    {
        var (w, _, _) = Ui.Open();
        w.Show(GK); Pump(); w.Find<Button>("prog-tpl-retro-plattform").Press(); Pump();
        w.Find<Button>("game-sec-bauen").Press(); Pump();
        Assert.NotNull(w.TryFind<StackPanel>("game-engines"));
        Assert.NotNull(w.TryFind<Border>("game-engine-unity"));
        Assert.Contains("blockiert", w.Find<TextBlock>("game-unity-caps").Text);
        // vor dem Bauen: klare Meldung
        w.Find<Button>("game-to-unity").Press(); Pump();
        Assert.Contains("zuerst „Bauen“", w.ProgramMessage(GK));
        w.Find<Button>("game-build").Press(); Pump();
        w.Find<Button>("game-to-unity").Press(); Pump();
        var unity = w.ProgramActive(GK)!.Text("unityProject");
        Assert.True(File.Exists(Path.Combine(unity, "Assets", "Resources", "trendo-level.json")));
        Assert.Contains("Unity-Projekt angelegt", w.ProgramMessage(GK));
        // Unity fehlt: Test wird verweigert, nichts gestartet
        var runs = new List<string>();
        w.EngineRunOverride = (exe, args) => { runs.Add(exe + " " + string.Join(' ', args)); return (0, ""); };
        w.Find<Button>("game-unity-test").Press(); Pump();
        Assert.Contains("Blockiert", w.ProgramMessage(GK)); Assert.Empty(runs);
        // Godot-Projekt
        w.Find<Button>("game-to-godot").Press(); Pump();
        var godot = w.ProgramActive(GK)!.Text("godotProject");
        Assert.True(File.Exists(Path.Combine(godot, "project.godot")));
        // Engines vorhanden (Testersatz): Unity schreibt Ergebnis-XML, Godot meldet OK
        w.EngineProbeOverride = () => EngineTargets.Summary([], _ => "", [("6000.0.23f1", "U.exe")], [("4.4-stable", "G.exe")], null, _ => false);
        w.EngineRunOverride = (exe, args) =>
        {
            runs.Add(exe + " " + string.Join(' ', args));
            if (args.Contains("-testResults")) File.WriteAllText(args[args.ToList().IndexOf("-testResults") + 1], "<test-run total=\"3\" passed=\"3\" failed=\"0\">");
            return (0, args.Contains("res://tests/check_level.gd") ? "TRENDO-OK 1 Level" : args.Contains("res://tests/play_check.gd") ? "TRENDO-OK Spiel läuft, Figur steht" : args.Contains("res://tests/save_check.gd") ? "TRENDO-OK Spielstand speichern, fortsetzen und prüfen" : args.Contains("res://tests/gameplay_check.gd") ? "TRENDO-OK Gameplay" : args.Contains("res://tests/menu_check.gd") ? "TRENDO-OK Menü" : "");
        };
        w.Show(GK); Pump();
        w.Find<Button>("game-unity-test").Press(); Pump();
        Assert.Contains(runs, r => r.StartsWith("U.exe") && r.Contains("-testPlatform EditMode"));
        Assert.Contains(runs, r => r.StartsWith("U.exe") && r.Contains("-testPlatform PlayMode"));
        Assert.Contains("PlayMode: 3 von 3 bestanden", w.LastEngineReport);
        Assert.True(File.Exists(Path.Combine(unity, "Trendo-Pruefung", "bericht.txt")));
        Assert.Contains("geprüft", w.Find<TextBlock>("game-unity-caps").Text);
        w.Find<Button>("game-godot-test").Press(); Pump();
        Assert.Contains(runs, r => r.StartsWith("G.exe") && r.Contains("--import"));
        Assert.Contains("Level-Prüfung: 1 Level", w.LastEngineReport);
        Assert.Contains("Spiel-Prüfung: Spiel läuft", w.LastEngineReport);
        Assert.Contains("Spielstand-Prüfung: Spielstand", w.LastEngineReport);
        Assert.Contains("Gameplay-Prüfung: Gameplay", w.LastEngineReport);
        Assert.Contains("Menü-Prüfung: Menü", w.LastEngineReport);
        w.Close();
    }
}
