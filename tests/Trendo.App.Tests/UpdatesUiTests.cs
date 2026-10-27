using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Trendo.App;
using Trendo.Core;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>Updates wie bei Windows (U1–U6, 27.10.2026) – mit nachgebildetem Update-Weg, ohne Netz.</summary>
public class UpdatesUiTests
{
    sealed class FakeUpdater : ITrendoUpdater
    {
        public string? Offer = "0.9.13"; public int Downloads, Applied, OnExit, Backups;
        public bool IsInstalled => true;
        public string Current { get; set; } = "0.9.12";
        public Task<(string Version, string? Notes)?> CheckAsync() => Task.FromResult<(string, string?)?>(Offer is null ? null : (Offer, "## " + Offer + "\n- TV & Live mit Serien\n- Schnellere Updates\n"));
        public Task DownloadAsync(Action<int> progress, CancellationToken ct) { Downloads++; progress(50); progress(100); return Task.CompletedTask; }
        public void ApplyAndRestart() => Applied++;
        public void ApplyOnExit() => OnExit++;
        public bool BackupCurrent(string dir) { Backups++; return true; }
        public bool RollbackTo(string dir, string version) => false;
    }

    static void Pump() { for (var i = 0; i < 6; i++) Avalonia.Threading.Dispatcher.UIThread.RunJobs(); }

    [AvaloniaFact(DisplayName = "Updates: suchen und im Hintergrund laden; „beim nächsten Start“ installiert beim Beenden, „sofort“ mit Einrichtungsbildschirm, Sicherung und Fenstern")]
    public async Task Check_download_apply()
    {
        var (w, _, root) = Ui.Open();
        var up = new FakeUpdater(); w.Updater = up;
        w.Show("updates"); Pump();
        Assert.NotNull(w.TryFind<RadioButton>("upd-when-nacht"));
        w.Find<RadioButton>("upd-when-start").IsChecked = true; Pump();
        Assert.Equal("start", w.Settings.UpdateWhen);
        Assert.True(await w.UpdateCheckAsync(userAsked: true));
        Assert.Equal((1, 1, 0), (up.Downloads, up.OnExit, up.Applied));
        Assert.Equal("0.9.13", w.UpdateReady);
        Assert.Contains("TV & Live mit Serien", ReleaseNotes.For(root, "0.9.13"));
        w.Show("updates"); Pump();
        w.Find<Button>("upd-install").Press(); Pump();
        Assert.True(w.UpdateApplying);
        Assert.Equal(1, up.Backups);
        Assert.True(File.Exists(Path.Combine(root, "updates", "fenster.json")));
        Assert.True(w.SetupOverlayOpen); // „Trendo wird aktualisiert“
        Assert.Equal(1, up.Applied);
        w.Close();
    }

    [AvaloniaFact(DisplayName = "Updates: nach dem Neustart kommen die Fenster zurück und „Was ist neu?“ erscheint einmal")]
    public void After_update()
    {
        var (w, _, root) = Ui.Open();
        var up = new FakeUpdater { Current = "0.9.13" }; w.Updater = up;
        ReleaseNotes.Save(root, "0.9.13", "## 0.9.13\n- TV & Live mit Serien\n");
        w.Settings.LastSeenVersion = "0.9.12";
        w.Show("handy"); w.Show("tv");
        var snap = w.Windows.Snapshot();
        Directory.CreateDirectory(Path.Combine(root, "updates"));
        File.WriteAllText(Path.Combine(root, "updates", "fenster.json"), System.Text.Json.JsonSerializer.Serialize(new MainWindow.SavedLayout(snap.Windows, snap.Stack, "tv")));
        foreach (var x in w.Windows.Stack.ToList()) w.Windows.Close(x.Id);
        w.UpdatesOnStartForTests(); Pump();
        Assert.Contains(w.Windows.Stack, x => x.Page == "handy");
        Assert.Equal("neu", w.CurrentView);
        Assert.Contains(w.GetLogicalTexts(), t => t.Contains("TV & Live mit Serien"));
        Assert.Equal("0.9.13", w.Settings.LastSeenVersion);
        Assert.False(File.Exists(Path.Combine(root, "updates", "fenster.json")));
        w.Close();
    }
}
