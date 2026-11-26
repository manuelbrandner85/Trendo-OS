using Avalonia.Headless.XUnit;
using Trendo.App;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>T-0770 (Abnahme-Befund): Velopack löscht beim Laden eines Updates das Paket der laufenden Version.
/// Die Sicherung für die Rückkehr zur alten Version (U5) muss deshalb vor dem Laden entstehen.</summary>
public class T0770UpdateSicherungUiTests
{
    /// <summary>Bildet das am PC beobachtete Velopack-Verhalten nach: nach DownloadAsync ist das Paket der laufenden Version weg.</summary>
    sealed class VelopackLikeUpdater : ITrendoUpdater
    {
        public readonly List<string> Calls = [];
        bool _currentPackage = true;
        public bool IsInstalled => true;
        public string Current => "0.9.110-abnahme1";
        public Task<(string Version, string? Notes)?> CheckAsync() => Task.FromResult<(string, string?)?>(("0.9.110-abnahme2", "## 0.9.110-abnahme2\n- Abnahme\n"));
        public Task DownloadAsync(Action<int> progress, CancellationToken ct) { Calls.Add("download"); _currentPackage = false; progress(100); return Task.CompletedTask; }
        public void ApplyAndRestart() => Calls.Add("apply");
        public void ApplyOnExit() => Calls.Add("apply-on-exit");
        public bool BackupCurrent(string dir) { Calls.Add(_currentPackage ? "backup-ok" : "backup-fehlt"); return _currentPackage; }
        public bool RollbackTo(string dir, string version) => false;
    }

    static void Pump() { for (var i = 0; i < 6; i++) Avalonia.Threading.Dispatcher.UIThread.RunJobs(); }

    [AvaloniaFact(DisplayName = "T-0770: Sicherung der laufenden Version entsteht vor dem Laden des Updates – beim Installieren ist sie da")]
    public async Task Backup_before_download()
    {
        var (w, _, _) = Ui.Open();
        var up = new VelopackLikeUpdater(); w.Updater = up;
        w.Settings.UpdateWhen = "start";
        Assert.True(await w.UpdateCheckAsync(userAsked: true));
        w.InstallUpdateNow(); Pump();
        Assert.Equal("backup-ok", up.Calls[0]);
        Assert.Equal("download", up.Calls[1]);
        Assert.DoesNotContain("backup-fehlt", up.Calls);
        Assert.Contains("apply", up.Calls);
        w.Close();
    }
}
