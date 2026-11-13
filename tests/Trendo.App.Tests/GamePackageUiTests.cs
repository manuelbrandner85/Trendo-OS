using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Trendo.App;
using Trendo.Core.Programs;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>Vorschlag 12: Ein-Klick-Veröffentlichung – „Pakete bauen“ ruft für ein Unreal-Spiel Unreals Paketierer auf und legt ein Windows-ZIP ab.</summary>
public class GamePackage_Ui
{
    const string GK = "spiele-programm";
    static void Pump() { for (var i = 0; i < 8; i++) Dispatcher.UIThread.RunJobs(); }

    [AvaloniaFact(DisplayName = "Unreal-Spiel veröffentlichen: Paketierer (Shipping) → Windows-ZIP; Fehlschlag nennt den Grund")]
    public async Task Unreal_windows_package()
    {
        var (w, _, _) = Ui.Open();
        w.Show(GK); Pump(); w.Find<Button>("prog-tpl-openworld").Press(); Pump();
        var d = w.ProgramActive(GK)!;
        w.Find<Button>("game-sec-bauen").Press(); Pump();
        w.Find<Button>("game-build").Press(); Pump();
        Assert.True(Directory.GetFiles(d.Text("built"), "*.uproject").Length == 1);
        var outDir = Path.Combine(Path.GetTempPath(), "trendo-veroeff-" + Guid.NewGuid().ToString("N"));
        var calls = new List<(string Exe, IReadOnlyList<string> Args)>();
        w.UnrealEngineOverride = "C:/UE";
        try
        {
            w.UnrealRunOverride = (exe, args) =>
            {
                calls.Add((exe, args));
                var arch = args.First(a => a.StartsWith("-archivedirectory=", StringComparison.Ordinal))["-archivedirectory=".Length..];
                Directory.CreateDirectory(Path.Combine(arch, "Windows")); File.WriteAllBytes(Path.Combine(arch, "Windows", "Waldlaeufer.exe"), [1]);
                return (0, "BUILD SUCCESSFUL\nAutomationTool exiting with ExitCode=0 (Success)");
            };
            await w.GameUnrealPackage(d, outDir); Pump();
            Assert.EndsWith("RunUAT.bat", calls.Single().Exe); Assert.Contains("-clientconfig=Shipping", calls.Single().Args);
            Assert.Contains(w.GamePackages, f => f.EndsWith("-windows.zip") && File.Exists(f));
            Assert.Contains("Windows-Spiel fertig", w.ProgramMessage(GK)); Assert.Contains("Waldlaeufer.exe", w.ProgramMessage(GK));
            w.UnrealRunOverride = (_, _) => (6, "Error: Cook failed for /Game/Trendo/Welt\nBUILD FAILED");
            await w.GameUnrealPackage(d, outDir); Pump();
            Assert.Contains("Windows-Spiel nicht fertig (Exitcode 6)", w.ProgramMessage(GK)); Assert.Contains("Cook failed", w.ProgramMessage(GK));
        }
        finally { w.UnrealRunOverride = null; w.UnrealEngineOverride = null; if (Directory.Exists(outDir)) Directory.Delete(outDir, true); }
    }
}
