using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Trendo.App;
using Trendo.Core;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>T-0770 Korrekturrunde 2: M-B (Rückkehr nimmt die tatsächlich gesicherte Version, Abweichung zu „zuletzt gut“ wird ehrlich gemeldet)
/// und N-a (liegengebliebene Zwischendateien einer abgebrochenen Sicherung werden aufgeräumt).</summary>
public class T0770Korrektur2UiTests
{
    sealed class Fake : ITrendoUpdater
    {
        public readonly List<string> Rollbacks = [];
        public bool IsInstalled => true;
        public string Current { get; set; } = "3.0";
        public Task<(string Version, string? Notes)?> CheckAsync() => Task.FromResult<(string, string?)?>(null);
        public Task DownloadAsync(Action<int> progress, CancellationToken ct) => Task.CompletedTask;
        public void ApplyAndRestart() { }
        public void ApplyOnExit() { }
        public bool BackupCurrent(string dir) => true;
        public bool RollbackTo(string dir, string version) { Rollbacks.Add(version); return true; }
    }

    static void Pump() { for (var i = 0; i < 6; i++) Avalonia.Threading.Dispatcher.UIThread.RunJobs(); }

    [AvaloniaFact(DisplayName = "T-0770 M-B: v2 frisch vor MarkOk gesichert, v3 läuft – Rückkehr geht zur gesicherten v2, Abweichung zu „zuletzt gut“ steht da")]
    public void Rollback_uses_backed_up_version()
    {
        var (w, _, _) = Ui.Open();
        var up = new Fake { Current = "3.0" }; w.Updater = up;
        var g = w.Boot;
        g.Begin("1.0"); g.MarkOk("1.0");          // 1.0 lief gut
        g.Begin("2.0");                            // 2.0 frisch installiert, noch nicht bestätigt
        Directory.CreateDirectory(g.BackupDir);    // vor dem Update auf 3.0 wurde 2.0 gesichert (es gab noch keine Sicherung)
        File.WriteAllText(Path.Combine(g.BackupDir, "TrendoOS-2.0-full.nupkg"), "v2");
        g.Begin("3.0");                            // 3.0 läuft jetzt; „zuletzt gut“ ist 1.0, gesichert ist 2.0
        Assert.Equal("1.0", g.LastGood);
        w.Show("updates"); Pump();
        var btn = w.Find<Button>("upd-rollback");
        Assert.Contains("2.0", btn.Content?.ToString());
        Assert.Contains(w.GetLogicalTexts(), t => t.Contains("gesichert ist 2.0") && t.Contains("1.0"));
        btn.Press(); Pump();
        Assert.Equal(["2.0"], up.Rollbacks);
        w.Close();
    }

    [Fact(DisplayName = "T-0770 N-a: liegengebliebene Zwischendateien (*.nupkg.neu-*) werden vor dem Sichern weggeräumt")]
    public void Leftover_temp_files_cleaned()
    {
        var root = Directory.CreateTempSubdirectory("trendo-t0770-rest-").FullName;
        try
        {
            var dir = Path.Combine(root, "updates", "vorherige-version"); Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "TrendoOS-1.0-full.nupkg"), "alt");
            File.WriteAllText(Path.Combine(dir, "TrendoOS-1.1-full.nupkg.neu-abcd1234"), "halb");
            var src = Path.Combine(root, "TrendoOS-1.1-full.nupkg"); File.WriteAllText(src, "neu");
            Assert.False(VelopackUpdater.ReplaceBackup(src, dir, _ => 0, out _)); // auch wenn die Sicherung selbst scheitert (zu wenig Platz)
            Assert.Equal(["TrendoOS-1.0-full.nupkg"], Directory.GetFiles(dir).Select(Path.GetFileName).ToArray());
        }
        finally { Directory.Delete(root, true); }
    }
}
