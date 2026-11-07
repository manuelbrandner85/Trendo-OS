using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Trendo.App;
using Trendo.Core.Programs;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>3D-Spiel in Unreal: Welt wählen, Bauen erzeugt das C++-Spiel, „Kompilieren und prüfen“ läuft in drei Schritten mit verständlichem Bericht; ohne Unreal klare Anleitung.</summary>
public class GameUnreal_Ui
{
    const string GK = "spiele-programm";
    static void Pump() { for (var i = 0; i < 8; i++) Dispatcher.UIThread.RunJobs(); }

    [AvaloniaFact(DisplayName = "Unreal-Spiel: Open World wählen (Rückgängig), Bauen schreibt C++-Spiel; ohne Unreal klare Meldung; mit Unreal kompilieren, Tests 3/3, Startlauf → Bericht im Projektordner; Fehler beim Kompilieren wird verständlich gemeldet")]
    public async Task Unreal_check()
    {
        var (w, _, _) = Ui.Open();
        w.Show(GK); Pump(); w.Find<Button>("prog-tpl-openworld").Press(); Pump();
        var d = w.ProgramActive(GK)!;
        w.Find<Button>("game-sec-bauen").Press(); Pump();
        var pick = w.Find<ComboBox>("game-world"); pick.SelectedIndex = 0; Pump();
        Assert.Equal("level", UnrealGame.WorldOf(d.Data));
        w.Find<Button>("prog-undo").Press(); Pump();
        w.Find<ComboBox>("game-world").SelectedIndex = 1; Pump();
        Assert.Equal("openworld", UnrealGame.WorldOf(d.Data));
        w.Find<Button>("game-build").Press(); Pump();
        var built = d.Text("built");
        Assert.True(File.Exists(Path.Combine(built, "Content", "Trendo", "level.json")));
        Assert.Contains("OpenWorld", File.ReadAllText(Path.Combine(built, "Config", "DefaultEngine.ini")));
        // ohne Unreal (Testmodus): klare Anleitung, nichts gestartet
        await w.GameUnrealCheck(d); Pump();
        Assert.Contains("Epic Games Launcher", w.ProgramMessage(GK));
        // mit Unreal (Testersatz für die Läufe)
        var runs = new List<string>();
        w.UnrealEngineOverride = "C:/UE";
        w.UnrealRunOverride = (exe, args) =>
        {
            runs.Add(Path.GetFileName(exe));
            if (exe.EndsWith("Build.bat")) return (0, "Result: Succeeded");
            if (args.Any(a => a.Contains("Automation RunTests"))) return (0, string.Join("\n", new[] { "Daten", "Regeln", "Spielstand" }.Select(t => $"Test Completed. Result={{Success}} Name={{{t}}} Path={{Trendo.{t}}}")));
            return (0, "TRENDO-SPIEL Welt=openworld Level=0 Objekte=7 Gegner=1 Figur=ja Boden=ja Z=92\nTRENDO-OK Spiel läuft, Figur steht");
        };
        await w.GameUnrealCheck(d, screenshot: false); Pump();
        Assert.Equal(["Build.bat", "UnrealEditor-Cmd.exe", "UnrealEditor.exe"], runs);
        Assert.StartsWith("Alles bereit", w.LastUnrealReport);
        Assert.Contains("Unreal-Tests: 3 von 3 bestanden", w.LastUnrealReport);
        Assert.Contains("Spiel läuft: Welt=openworld", w.LastUnrealReport);
        Assert.True(File.Exists(Path.Combine(built, "Trendo-Pruefung", "bericht.txt")));
        w.Show(GK); Pump();
        Assert.Contains("3 von 3", w.Find<TextBlock>("game-ue-report").Text);
        // Kompilierfehler: verständlich, keine weiteren Schritte
        runs.Clear(); w.UnrealRunOverride = (exe, args) => { runs.Add(Path.GetFileName(exe)); return (6, "error C2065: undeclared\nResult: Failed (OtherCompilationError)"); };
        await w.GameUnrealCheck(d, screenshot: false); Pump();
        Assert.Equal(["Build.bat"], runs);
        Assert.Contains("Kompilieren fehlgeschlagen (Exitcode 6)", w.LastUnrealReport);
        w.Find<Button>("game-ue-play").Press(); Pump();
        w.Close();
    }
}
