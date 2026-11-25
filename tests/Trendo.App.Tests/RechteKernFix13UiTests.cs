using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Trendo.App;
using Trendo.Core;
using Trendo.Core.Programs;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>T-0764, 13. Prüfrunde an der Oberfläche: frei getippte Ordner. M1 „Code mit KI“ legt im Kinderprofil keine start.bat in einem
/// Autostart-Testordner an (die KI ist ersetzt, nichts startet echt); M2 „Viele Dateien umbenennen“ macht aus „spiel“ kein „spiel.cmd“ und ändert
/// nichts außerhalb der Freigabe. Erwachsene unverändert. Der „Autostart“ ist ein Testordner im isolierten Datenordner (nicht freigegeben).</summary>
[Collection("Claude")]
public class RechteKernFix13Ui
{
    const string K = "code-ki-programm";

    sealed class FixedKi(string answer) : IKiBackend
    {
        public string Name => "Ersatz-KI"; public string BaseUrl => "http://127.0.0.1:9";
        public Task<bool> IsUpAsync(CancellationToken ct = default) => Task.FromResult(true);
        public Task<IReadOnlyList<string>> ModelsAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<string>>(["ersatz"]);
        public Task<string> ChatAsync(string model, IReadOnlyList<KiMessage> messages, CancellationToken ct = default) => Task.FromResult(answer);
    }

    static void Settle() { for (var i = 0; i < 8; i++) Dispatcher.UIThread.RunJobs(); }
    static async Task Until(Func<bool> ok, int ms = 60000) { for (var i = 0; i < ms / 20 && !ok(); i++) { Settle(); await Task.Delay(20); } Settle(); Assert.True(ok()); }

    static (MainWindow W, string Root) Open(bool child)
    {
        var root = Path.Combine(Path.GetTempPath(), "trendo-ui-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        new UserSettings { Child = child, Name = child ? "Kind" : "Erika Muster" }.Save(root);
        var w = new MainWindow(new TrendoCore(root, new InMemorySecretStore(), new FakeLauncher(), offline: false), new AppOptions(root, null, 0, false, Isolated: true), new Probe("browser"));
        w.ProgramSetLevel(UserLevel.Erweitert); w.FreigabeConfirm = _ => Task.FromResult(true); w.Show(); Settle();
        return (w, root);
    }

    const string BatAnswer = "PLAN:\n- Startdatei anlegen\n\nDATEI: start.bat\n```bat\n@echo off\necho hallo\n```\n";

    [AvaloniaFact(DisplayName = "T-0764 M1: Kind – „Code mit KI“ mit eingetipptem Autostart-Ordner legt keine start.bat an; Erwachsene wie bisher")]
    public async Task CodeKi_Autostart()
    {
        foreach (var child in new[] { true, false })
        {
            var (w, root) = Open(child);
            try
            {
                w.KiBackendsOverride = () => [new FixedKi(BatAnswer)];
                w.Show(K); Settle();
                w.Find<Button>("prog-tpl-beispiel").Press(); Settle();
                var d = w.ProgramActive(K)!;
                var auto = Path.Combine(root, "AppData-Test", "Startmenue", "Autostart"); Directory.CreateDirectory(auto);
                d.Set("root", auto, "Projektordner"); d.Set("file", "", "Datei"); // wie frei eingetippt
                w.Show(K); Settle();
                w.Find<TextBox>("ca-ki-wish").Text = "Leg eine start.bat an";
                w.Find<Button>("ca-ki-propose").Press(); Settle();
                await Until(() => w.CodeAiKiPhase is "vorschau" or "fehler");
                if (w.CodeAiKiPhase == "vorschau")
                {
                    foreach (var f in w.CodeAiKiPreview!.Files) w.CodeAiKiDecide(f.Path, -1, true);
                    await w.CodeAiKiApply(d); Settle();
                }
                Assert.Equal(!child, File.Exists(Path.Combine(auto, "start.bat")));
            }
            finally { w.KiBackendsOverride = null; w.Close(); }
        }
    }

    [AvaloniaFact(DisplayName = "T-0764 M2: Kind – „Viele Dateien umbenennen“: spiel → spiel.cmd nicht (auch nicht in der Vorschau), nichts außerhalb der Freigabe; Muster ohne neue Endung geht; Erwachsene wie bisher")]
    public void Umbenennen()
    {
        foreach (var child in new[] { true, false })
        {
            var (w, root) = Open(child);
            try
            {
                var dir = Path.Combine(w.Files.Home, "spiele"); Directory.CreateDirectory(dir); File.WriteAllText(Path.Combine(dir, "spiel"), "x");
                w.Show("alltag-dateien"); Settle();
                w.Find<TextBox>("ren-folder").Text = dir; w.Find<TextBox>("ren-pattern").Text = "{name}.cmd{ext}";
                w.Find<Button>("ren-preview-btn").Press(); Settle();
                var shown = w.Find<StackPanel>("ren-preview").Children.Count;
                w.Find<Button>("ren-apply").Press(); Settle();
                Assert.Equal(!child, File.Exists(Path.Combine(dir, "spiel.cmd")));
                Assert.Equal(child, File.Exists(Path.Combine(dir, "spiel")));
                Assert.Equal(child ? 0 : 1, shown); // Kinder: schon die Vorschau zeigt „spiel.cmd“ nicht
                if (!child) continue;
                Assert.Contains(w.Core.Audit.ReadAll(), a => a.Action == "rbac.denied" && a.Detail.Contains("spiel.cmd"));
                // erlaubt: ohne neue Endung
                w.Show("alltag-dateien"); Settle();
                w.Find<TextBox>("ren-folder").Text = dir; w.Find<TextBox>("ren-pattern").Text = "Spiel_{n:00}";
                w.Find<Button>("ren-apply").Press(); Settle();
                Assert.True(File.Exists(Path.Combine(dir, "Spiel_01")));
                // außerhalb der Freigabe (Trendos Datenordner steht für AppData): nichts
                var outside = Path.Combine(root, "AppData-Test", "werkzeuge"); Directory.CreateDirectory(outside); File.WriteAllText(Path.Combine(outside, "a.txt"), "x");
                w.Show("alltag-dateien"); Settle();
                w.Find<TextBox>("ren-folder").Text = outside; w.Find<TextBox>("ren-pattern").Text = "b_{n}";
                w.Find<Button>("ren-apply").Press(); Settle();
                Assert.True(File.Exists(Path.Combine(outside, "a.txt"))); Assert.False(File.Exists(Path.Combine(outside, "b_1.txt")));
            }
            finally { w.Close(); }
        }
    }
}
