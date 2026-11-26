using Trendo.App;
using Trendo.Core;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>T-0770 Korrekturrunde 3: automatische Rückkehr beim Start (Program.cs, M-B) – nach drei Fehlstarts zur tatsächlich gesicherten Version.</summary>
public class T0770Korrektur3RueckkehrTests
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

    [Fact(DisplayName = "T-0770 M-B: automatische Rückkehr nach drei Fehlstarts geht zur gesicherten v2 und meldet die Abweichung zu „zuletzt gut“ v1")]
    public void Auto_rollback_uses_backup()
    {
        var root = Directory.CreateTempSubdirectory("trendo-t0770-auto-").FullName;
        try
        {
            var g = new BootGuard(root);
            g.Begin("1.0"); g.MarkOk("1.0"); g.Begin("2.0");
            Directory.CreateDirectory(g.BackupDir); File.WriteAllText(Path.Combine(g.BackupDir, "TrendoOS-2.0-full.nupkg"), "v2");
            var up = new Fake { Current = "3.0" }; var notes = new List<string>();
            Assert.False(AppStartup.TryAutoRollback(g, up, notes.Add)); // 1. Start von 3.0
            Assert.False(AppStartup.TryAutoRollback(g, up, notes.Add)); // 2. Start
            Assert.True(AppStartup.TryAutoRollback(g, up, notes.Add));  // 3. Fehlstart → Rückkehr
            Assert.Equal(["2.0"], up.Rollbacks);
            Assert.Single(notes); Assert.Contains("gesichert ist 2.0", notes[0]);
            // ohne Sicherung keine Rückkehr
            var root2 = Path.Combine(root, "b"); var g2 = new BootGuard(root2); g2.Begin("1.0"); g2.MarkOk("1.0");
            var up2 = new Fake { Current = "2.0" };
            for (var i = 0; i < 3; i++) Assert.False(AppStartup.TryAutoRollback(g2, up2, null));
            Assert.Empty(up2.Rollbacks);
        }
        finally { Directory.Delete(root, true); }
    }
}
