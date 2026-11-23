using System.Diagnostics;
using Trendo.Core;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>T-0729: eigene Temp-Ordner sicher löschen und verwaiste Reste nur mit eigenen Präfixen wegräumen.</summary>
public class TempDirTests
{
    [Fact(DisplayName = "T-0729: TempDir mit using verschwindet – auch mit schreibgeschützter Datei und nach einer Ausnahme")]
    public void Create_and_dispose()
    {
        string path;
        using (var t = TempDir.Create("trendo-ui-"))
        {
            path = t.Path;
            Assert.StartsWith(Path.Combine(Path.GetTempPath(), "trendo-ui-"), path);
            var f = Path.Combine(path, "a", "b.txt"); Directory.CreateDirectory(Path.GetDirectoryName(f)!); File.WriteAllText(f, "x");
            File.SetAttributes(f, FileAttributes.ReadOnly);
        }
        Assert.False(Directory.Exists(path));
        string? second = null;
        Assert.Throws<InvalidOperationException>((Action)(() =>
        {
            using var t = TempDir.Create("trendo-chrome-"); second = t.Path; File.WriteAllText(Path.Combine(t.Path, "x"), "y");
            throw new InvalidOperationException("Abbruch");
        }));
        Assert.False(Directory.Exists(second));
        Assert.Throws<ArgumentException>(() => TempDir.Create("fremd-"));
        Assert.Throws<ArgumentException>(() => TempDir.Create("trendo-auftrag-"));
    }

    [Fact(DisplayName = "T-0729: Löschen wartet, bis eine gesperrte Datei frei wird (wie bei Chrome/LibreOffice)")]
    public async Task Delete_retries_locked_file()
    {
        if (!OperatingSystem.IsWindows()) return; // Sperren mit FileShare.None gibt es so nur unter Windows
        var t = TempDir.Create("trendo-chrome-");
        var f = Path.Combine(t.Path, "Default", "Cookies"); Directory.CreateDirectory(Path.GetDirectoryName(f)!);
        var lockStream = new FileStream(f, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
        // ein einziger Versuch scheitert – genau das alte Verhalten (Directory.Delete einmal, Fehler verschluckt)
        Assert.False(TempDir.Delete(t.Path, attempts: 1));
        Assert.True(Directory.Exists(t.Path));
        // T-0760: Freigabe auf eigenem Faden statt im Thread-Pool – im Volltest ist der Pool von parallelen Tests belegt, Task.Delay kam dann
        // erst nach den 30 × 100 ms Versuchen dran. Geprüft wird weiter genau dasselbe: Delete wartet, bis die Sperre fällt (Versuche unverändert).
        var release = new Thread(() => { Thread.Sleep(500); lockStream.Dispose(); }) { IsBackground = true };
        release.Start();
        Assert.True(TempDir.Delete(t.Path, attempts: 30, delayMs: 100));
        release.Join(); await Task.CompletedTask;
        Assert.False(Directory.Exists(t.Path));
    }

    [Fact(DisplayName = "T-0729: DeleteAsync wartet auf gesperrte Dateien, ohne den aufrufenden Thread zu blockieren")]
    public async Task DeleteAsync_does_not_block()
    {
        if (!OperatingSystem.IsWindows()) return; // Sperren mit FileShare.None gibt es so nur unter Windows
        var t = TempDir.Create("trendo-chrome-");
        var f = Path.Combine(t.Path, "Default", "Cookies"); Directory.CreateDirectory(Path.GetDirectoryName(f)!);
        var lockStream = new FileStream(f, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var del = TempDir.DeleteAsync(t.Path, attempts: 30, delayMs: 100);
        Assert.True(sw.ElapsedMilliseconds < 400, "DeleteAsync hat den Aufrufer blockiert"); // Rückkehr sofort, gelöscht wird im Hintergrund
        Assert.False(del.IsCompleted);
        await Task.Delay(500); await lockStream.DisposeAsync();
        Assert.True(await del);
        Assert.False(Directory.Exists(t.Path));
        Assert.True(await TempDir.DeleteAsync(t.Path)); // schon weg: sofort true
    }

    [Fact(DisplayName = "T-0729: Startaufräumung löscht nur alte eigene Präfixe – fremde, geschützte, junge und lebende Ordner bleiben")]
    public void Sweep_only_old_own()
    {
        var root = Path.Combine(Path.GetTempPath(), "t0729-sweep-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var old = DateTime.UtcNow.AddHours(-48);
            string Dir(string name, bool aged, string? youngChild = null)
            {
                var d = Path.Combine(root, name); Directory.CreateDirectory(Path.Combine(d, "sub"));
                File.WriteAllText(Path.Combine(d, "sub", "f.txt"), "x");
                if (aged) Age(d, old);
                if (youngChild is not null) File.WriteAllText(Path.Combine(d, "sub", youngChild), "neu");
                return d;
            }
            using var self = Process.GetCurrentProcess();
            var gone = new[]
            {
                Dir("trendo-ui-" + Guid.NewGuid().ToString("N"), aged: true),
                Dir("trendo-chrome-" + Guid.NewGuid().ToString("N"), aged: true),
                Dir("trendo-logo-" + Guid.NewGuid().ToString("N"), aged: true), // unbekannter Präfix, aber Trendo-Guid-Name
                Dir("trendo-testlauf-999999-abc", aged: false), // Testprozess 999999 gibt es nicht (Windows-PIDs sind Vielfache von 4) – sofort weg
            };
            var oldFile = Path.Combine(root, "trendo-pdf-" + Guid.NewGuid().ToString("N") + ".pdf"); File.WriteAllText(oldFile, "%PDF"); Age(oldFile, old);
            var stay = new[]
            {
                Dir("trendo-chrome-" + Guid.NewGuid().ToString("N"), aged: false), // jung: läuft vielleicht gerade
                Dir("trendo-ui-" + Guid.NewGuid().ToString("N"), aged: true, youngChild: "gerade.txt"), // alt, aber darin wurde eben geschrieben
                Dir("fremd-alt", aged: true),
                Dir("claude", aged: true),
                Dir("trendo-auftrag-T-9999", aged: true),
                Dir("trendo-tools-b", aged: true),
                Dir("trendo-ue-e2e", aged: true),
                Dir("trendo-ps2", aged: true),
                Dir("trendo-abbild-latest", aged: true),
                Dir("trendo-abbild-0.9.84", aged: true),
                Dir("trendo-logo-ohne-kennung", aged: true), // weder Liste noch Guid – unbekannt, bleibt
                Dir(TempDir.TestRunName(self), aged: true), // Laufordner eines lebenden Testprozesses
            };
            var n = TempDir.SweepStale(root, ignoreSwitch: true);
            foreach (var g in gone) Assert.False(Directory.Exists(g), "hätte gelöscht werden sollen: " + Path.GetFileName(g));
            Assert.False(File.Exists(oldFile));
            foreach (var s in stay) Assert.True(Directory.Exists(s), "hätte bleiben müssen: " + Path.GetFileName(s));
            Assert.Equal(gone.Length + 1, n);
            // Schalter: TRENDO_TEMP_AUFRAEUMEN=0 → nichts
            var again = Dir("trendo-ui-" + Guid.NewGuid().ToString("N"), aged: true);
            var before = Environment.GetEnvironmentVariable(TempDir.SweepVar);
            Environment.SetEnvironmentVariable(TempDir.SweepVar, "0");
            try { Assert.Equal(0, TempDir.SweepStale(root)); Assert.True(Directory.Exists(again)); }
            finally { Environment.SetEnvironmentVariable(TempDir.SweepVar, before); }
        }
        finally { TempDir.Delete(root); }
    }

    [Fact(DisplayName = "T-0729: Geschützte Namen gelten nie als eigener Wegwerf-Ordner")]
    public void Protected_names()
    {
        foreach (var p in TempDir.Protected) Assert.False(TempDir.IsOwnName(p + "x"), p);
        Assert.False(TempDir.IsOwnName("trendo-auftrag-" + Guid.NewGuid().ToString("N")));
        Assert.True(TempDir.IsOwnName("trendo-ui-" + Guid.NewGuid().ToString("N")));
        Assert.True(TempDir.IsOwnName("trendo-ext-t-1"));
        Assert.False(TempDir.IsOwnName("Trendo"));
        Assert.False(TempDir.IsOwnName("andere-" + Guid.NewGuid().ToString("N")));
    }

    [Fact(DisplayName = "T-0729: Audio-Ausschnitte in trendo-audio-* überleben die Aufräumung – gespeicherte Projekte verweisen darauf")]
    public void Sweep_keeps_audio_clips()
    {
        var root = Path.Combine(Path.GetTempPath(), "t0729-audio-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var audio = Path.Combine(root, "trendo-audio-4242"); Directory.CreateDirectory(audio);
            var clip = Path.Combine(audio, "clip-" + Guid.NewGuid().ToString("N") + ".wav"); File.WriteAllText(clip, "RIFF");
            var audioGuid = Path.Combine(root, "trendo-audio-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(audioGuid); // auch mit Guid-Namen geschützt
            var scratch = Path.Combine(root, "trendo-ui-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(scratch);
            var old = DateTime.UtcNow.AddHours(-48);
            Age(audio, old); Age(audioGuid, old); Age(scratch, old);
            Assert.Equal(1, TempDir.SweepStale(root, ignoreSwitch: true));
            Assert.True(File.Exists(clip));
            Assert.True(Directory.Exists(audioGuid));
            Assert.False(Directory.Exists(scratch));
            Assert.True(TempDir.IsProtected("trendo-audio-4242"));
            Assert.DoesNotContain("trendo-audio-", TempDir.OwnPrefixes);
        }
        finally { TempDir.Delete(root); }
    }

    [Fact(DisplayName = "T-0729: Testlauf schreibt in eigenen Laufordner, feste Prüfordner bleiben im echten Temp")]
    public void Test_run_redirect()
    {
        var run = TempDir.TestRun;
        Assert.NotNull(run);
        Assert.Equal(Path.GetFullPath(run!).TrimEnd(Path.DirectorySeparatorChar), Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar));
        Assert.Equal(Path.GetFullPath(TempDir.SystemTemp).TrimEnd(Path.DirectorySeparatorChar), Path.GetDirectoryName(Path.GetFullPath(run!)));
        Assert.True(TempDir.OwnerAlive(Path.GetFileName(run!)));
    }

    static void Age(string path, DateTime t)
    {
        if (Directory.Exists(path))
            foreach (var e in Directory.EnumerateFileSystemEntries(path, "*", SearchOption.AllDirectories).OrderByDescending(x => x.Length))
                Set(e, t);
        Set(path, t);
        static void Set(string p, DateTime t)
        {
            if (Directory.Exists(p)) { Directory.SetLastWriteTimeUtc(p, t); Directory.SetCreationTimeUtc(p, t); }
            else { File.SetLastWriteTimeUtc(p, t); File.SetCreationTimeUtc(p, t); }
        }
    }
}
