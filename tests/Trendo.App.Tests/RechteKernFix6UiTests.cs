using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Trendo.App;
using Trendo.Core;
using Trendo.Core.Programs;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>T-0764, 6. Prüfrunde an der Oberfläche (B): Engine-Läufe (Godot, Unity, Unreal), Spiele-Bibliothek (Godot, mGBA) und Kittl über Claude
/// führen im Kinderprofil nichts aus – Rolle zuerst. Ohne statische Ersatz-Hooks (nur Ersatz je Fenster); startet nichts echt.</summary>
public class RechteKernFix6Ui
{
    const string GK = "spiele-programm";
    static void Settle() { for (var i = 0; i < 8; i++) Dispatcher.UIThread.RunJobs(); }

    static (MainWindow W, FakeLauncher L, string Root) Open(bool child)
    {
        var root = Path.Combine(Path.GetTempPath(), "trendo-ui-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        new UserSettings { Child = child, Name = child ? "Kind" : "Erika Muster" }.Save(root);
        var l = new FakeLauncher();
        var w = new MainWindow(new TrendoCore(root, new InMemorySecretStore(), l, offline: false), new AppOptions(root, null, 0, false, Isolated: true), new Probe("browser"));
        w.ProgramSetLevel(UserLevel.Erweitert); // wie Ui.Open: alle Fachwerkzeuge sichtbar
        w.FreigabeConfirm = _ => Task.FromResult(true); w.Show(); Settle();
        return (w, l, root);
    }

    static bool Denied(MainWindow w, string what) => w.Core.Audit.ReadAll().Any(a => a.Action == "rbac.denied" && a.Detail.Contains(what));

    [AvaloniaFact(DisplayName = "T-0764 B: Kind – Godot- und Unity-Prüfung starten keine Engine (Rolle zuerst); Erwachsene: Engine-Lauf wie bisher")]
    public void Engines_Kind()
    {
        foreach (var child in new[] { true, false })
        {
            var (w, _, _) = Open(child);
            try
            {
                w.Show(GK); Settle(); w.Find<Button>("prog-tpl-retro-plattform").Press(); Settle();
                w.Find<Button>("game-sec-bauen").Press(); Settle();
                w.Find<Button>("game-build").Press(); Settle();
                var d = w.ProgramActive(GK)!;
                w.GameToEngine(d, "godot"); w.GameToEngine(d, "unity");
                var runs = new List<string>();
                w.EngineProbeOverride = () => EngineTargets.Summary([], _ => "", [("6000.0.23f1", "U.exe")], [("4.4-stable", "G.exe")], null, _ => false);
                w.EngineRunOverride = (exe, args) => { runs.Add(exe); return (0, "TRENDO-OK"); };
                foreach (var engine in new[] { "godot", "unity" }) try { w.GameEngineTest(d, engine); } catch (InvalidOperationException) { }
                Settle();
                if (child) { Assert.Empty(runs); Assert.True(Denied(w, "spiel")); }
                else Assert.Contains("G.exe", runs);
            }
            finally { w.Close(); }
        }
    }

    [AvaloniaFact(DisplayName = "T-0764 B: Kind – Unreal kompilieren/prüfen und „Spielen (Unreal)“ starten nichts (Build.bat, UnrealEditor-Cmd, Editor)")]
    public async Task Unreal_Kind()
    {
        var (w, _, _) = Open(true);
        try
        {
            w.Show(GK); Settle(); w.Find<Button>("prog-tpl-openworld").Press(); Settle();
            var d = w.ProgramActive(GK)!;
            w.Find<Button>("game-sec-bauen").Press(); Settle();
            w.Find<Button>("game-build").Press(); Settle();
            var runs = new List<string>();
            w.UnrealEngineOverride = "C:/UE";
            w.NatureFetchOverride = _ => 0;
            w.UnrealRunOverride = (exe, _) => { runs.Add(Path.GetFileName(exe)); return (0, "Result: Succeeded"); };
            await w.GameUnrealCheck(d, screenshot: false); Settle();
            try { w.GameUnrealPlay(d); } catch (InvalidOperationException) { }
            Assert.Empty(runs);
            Assert.True(Denied(w, "spiel"));
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0764 B: Kind – Spiele-Bibliothek startet weder Godot noch den Emulator; Kittl über Claude fragt und öffnet nichts")]
    public async Task Bibliothek_und_Kittl_Kind()
    {
        var (w, _, root) = Open(true);
        try
        {
            var started = new List<string>();
            w.LibGodotOverride = "godot.exe"; w.LibStartOverride = (x, _) => { started.Add(x); return true; };
            var proj = Path.Combine(root, "godot-spiel"); Directory.CreateDirectory(proj); File.WriteAllText(Path.Combine(proj, "project.godot"), "x");
            Assert.False(w.LibPlayGodot(proj));
            Assert.Empty(started);
            var calls = new List<string>();
            w.KittlCliOverride = a => { calls.Add(string.Join(' ', a)); return new(0, "  Status: ! Needs authentication", ""); };
            w.KittlConsoleOverride = a => { calls.Add("console " + string.Join(' ', a)); return true; };
            w.PodKittlSet(true);
            await w.PodKittlCheck(); Settle();
            Assert.False(await w.PodKittlLogin()); Settle();
            Assert.Empty(calls);
            Assert.True(Denied(w, "kittl"));
        }
        finally { w.Close(); }
    }
}
