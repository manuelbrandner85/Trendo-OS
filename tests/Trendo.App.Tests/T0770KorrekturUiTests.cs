using Avalonia.Headless.XUnit;
using Trendo.App;
using Trendo.Core;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>T-0770 Korrekturrunde: M3 (lokale Update-Quelle nur in einer Prüf-Instanz, sichtbar und protokolliert, keine Netzpfade)
/// und M5 (Sicherung atomar, mit Platzprüfung, gute Sicherung bleibt bei ungeprüfter Version).</summary>
public class T0770KorrekturUiTests
{
    sealed class Fake : ITrendoUpdater
    {
        public string? Source; public int Backups, Downloads;
        public bool IsInstalled => true;
        public string Current { get; set; } = "1.1";
        public string? TestSource => Source;
        public Task<(string Version, string? Notes)?> CheckAsync() => Task.FromResult<(string, string?)?>(("1.2", "## 1.2\n- Test\n"));
        public Task DownloadAsync(Action<int> progress, CancellationToken ct) { Downloads++; return Task.CompletedTask; }
        public void ApplyAndRestart() { }
        public void ApplyOnExit() { }
        public bool BackupCurrent(string dir) { Backups++; return true; }
        public bool RollbackTo(string dir, string version) => false;
    }

    static void Pump() { for (var i = 0; i < 6; i++) Avalonia.Threading.Dispatcher.UIThread.RunJobs(); }

    [Fact(DisplayName = "T-0770 M3: lokale Update-Quelle nur zusammen mit einer Prüf-Instanz, nie als Netzpfad")]
    public void Local_source_needs_instance()
    {
        var feed = Directory.CreateTempSubdirectory("trendo-t0770-feed-").FullName;
        var inst = Path.Combine(Path.GetTempPath(), "trendo-t0770-instanz");
        try
        {
            Assert.Null(VelopackUpdater.LocalSourceDir(feed, instance: null)); // normale Installation ignoriert die Variable
            Assert.Equal(Path.GetFullPath(feed), VelopackUpdater.LocalSourceDir(feed, instance: inst));
            Assert.Null(VelopackUpdater.LocalSourceDir(@"\\server\feed", instance: inst));
            Assert.Null(VelopackUpdater.LocalSourceDir("//server/feed", instance: inst));
        }
        finally { Directory.Delete(feed, true); }
    }

    [AvaloniaFact(DisplayName = "T-0770 M3: aktive Testquelle steht sichtbar in der Updates-Ansicht und im Protokoll")]
    public async Task Test_source_visible_and_logged()
    {
        var (w, _, _) = Ui.Open();
        var up = new Fake { Source = @"C:\Abnahme\feed" }; w.Updater = up;
        w.Settings.UpdateWhen = "start";
        w.Show("updates"); Pump();
        var texts = w.GetLogicalTexts().ToList();
        Assert.Contains(texts, t => t.Contains("Testquelle") && t.Contains(@"C:\Abnahme\feed"));
        Assert.DoesNotContain(texts, t => t.Contains("nur aus den offiziellen"));
        await w.UpdateCheckAsync(userAsked: true);
        Assert.Contains(w.Core.Audit.ReadAll(), a => a.Action == "update.quelle.test");
        w.Close();

        var (w2, _, _) = Ui.Open();
        w2.Updater = new Fake(); w2.Show("updates"); Pump();
        Assert.Contains(w2.GetLogicalTexts(), t => t.Contains("nur aus den offiziellen"));
        w2.Close();
    }

    [AvaloniaFact(DisplayName = "T-0770 M5c: ungeprüfte Version ersetzt die Sicherung der letzten guten Version nicht")]
    public async Task Unverified_keeps_good_backup()
    {
        var (w, _, _) = Ui.Open();
        var up = new Fake { Current = "1.1" }; w.Updater = up;
        w.Settings.UpdateWhen = "start";
        Directory.CreateDirectory(w.Boot.BackupDir);
        File.WriteAllText(Path.Combine(w.Boot.BackupDir, "TrendoOS-1.0-full.nupkg"), "gute Version");
        w.Boot.Begin("1.1"); // 1.1 läuft, noch nicht als gut markiert
        Assert.True(await w.UpdateCheckAsync(userAsked: true));
        w.InstallUpdateNow(); Pump();
        Assert.Equal(0, up.Backups);
        Assert.Equal("gute Version", File.ReadAllText(Path.Combine(w.Boot.BackupDir, "TrendoOS-1.0-full.nupkg")));
        w.Close();
    }

    [Fact(DisplayName = "T-0770 M5a: neue Sicherung erst vollständig kopieren, dann tauschen – alte Version erst danach weg")]
    public void Replace_backup_atomic()
    {
        var root = Directory.CreateTempSubdirectory("trendo-t0770-sich-").FullName;
        try
        {
            var dir = Path.Combine(root, "vorherige-version"); Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "TrendoOS-1.0-full.nupkg"), "alt");
            var src = Path.Combine(root, "TrendoOS-1.1-full.nupkg"); File.WriteAllText(src, "neu");
            // Kopieren scheitert (Quelle fehlt): alte Sicherung bleibt, keine Reste
            Assert.False(VelopackUpdater.ReplaceBackup(Path.Combine(root, "fehlt.nupkg"), dir, _ => long.MaxValue, out _));
            Assert.Equal("alt", File.ReadAllText(Path.Combine(dir, "TrendoOS-1.0-full.nupkg")));
            Assert.Single(Directory.GetFiles(dir));
            // Erfolg: neue da, alte weg, keine Zwischendatei
            Assert.True(VelopackUpdater.ReplaceBackup(src, dir, _ => long.MaxValue, out var note));
            Assert.Null(note);
            Assert.Equal(["TrendoOS-1.1-full.nupkg"], Directory.GetFiles(dir).Select(Path.GetFileName).ToArray());
            Assert.Equal("neu", File.ReadAllText(Path.Combine(dir, "TrendoOS-1.1-full.nupkg")));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact(DisplayName = "T-0770 M5b: zu wenig Speicher – alte Sicherung bleibt, ehrliche Meldung")]
    public void Replace_backup_low_space()
    {
        var root = Directory.CreateTempSubdirectory("trendo-t0770-platz-").FullName;
        try
        {
            var dir = Path.Combine(root, "vorherige-version"); Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "TrendoOS-1.0-full.nupkg"), "alt");
            var src = Path.Combine(root, "TrendoOS-1.1-full.nupkg"); File.WriteAllText(src, new string('x', 4096));
            Assert.False(VelopackUpdater.ReplaceBackup(src, dir, _ => 1000, out var note));
            Assert.Contains("Speicher", note);
            Assert.Equal(["TrendoOS-1.0-full.nupkg"], Directory.GetFiles(dir).Select(Path.GetFileName).ToArray());
            Assert.Equal("alt", File.ReadAllText(Path.Combine(dir, "TrendoOS-1.0-full.nupkg")));
        }
        finally { Directory.Delete(root, true); }
    }
}
