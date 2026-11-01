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
        Assert.True(w.NewsOpen); Assert.Single(w.NewsSlides); // Vollbild „Das ist neu“
        Assert.Contains(w.GetLogicalTexts(), t => t.Contains("TV & Live mit Serien"));
        Assert.Equal("0.9.13", w.Settings.LastSeenVersion);
        Assert.False(File.Exists(Path.Combine(root, "updates", "fenster.json")));
        w.Close();
    }

    [AvaloniaFact(DisplayName = "Neuerungen im Vollbild: nicht wegklickbar (Esc, Klick daneben), läuft von selbst weiter, Pfeile blättern, „Los geht's“ erst auf der letzten Folie")]
    public void News_fullscreen()
    {
        var (w, _, _) = Ui.Open();
        w.NewsManualClock = true;
        w.ShowNews("0.9.29", "## 0.9.29\n- **Karten & Touren:** echte Karte\n  - GPX laden\n  - Höhenprofil\n- **Posteingang als Mailprogramm:** Ordner und Anhänge\n- Rückgängig wirkt jetzt zuverlässig\n");
        Assert.True(w.NewsOpen); Assert.Equal(3, w.NewsSlides.Count); Assert.Equal(-1, w.NewsIndex);
        Assert.Equal(WindowState.FullScreen, w.WindowState);
        Assert.Equal("Trendo OS 0.9.29", w.Find<TextBlock>("news-title").Text);
        Assert.False(w.Find<Button>("news-done").IsVisible);
        // Esc und Klick schließen nicht
        w.Key(Avalonia.Input.PhysicalKey.Escape); Assert.True(w.NewsOpen);
        w.NewsClose(); Assert.True(w.NewsOpen); // „Los geht's“ erst am Ende
        // läuft von selbst weiter
        w.NewsTick(TimeSpan.FromSeconds(5)); Pump();
        Assert.Equal(0, w.NewsIndex); Assert.Equal("Karten & Touren", w.Find<TextBlock>("news-title").Text);
        Assert.NotNull(w.TryFind<Border>("news-point-1"));
        Assert.Equal("1 / 3", w.Find<TextBlock>("news-count").Text);
        // Pfeile
        w.Key(Avalonia.Input.PhysicalKey.ArrowRight); Assert.Equal(1, w.NewsIndex);
        w.Key(Avalonia.Input.PhysicalKey.ArrowLeft); Assert.Equal(0, w.NewsIndex);
        w.Find<Button>("news-next").Press(); w.Find<Button>("news-next").Press();
        Assert.Equal(2, w.NewsIndex); Assert.True(w.NewsLast);
        Assert.True(w.Find<Button>("news-done").IsVisible); Assert.False(w.Find<Button>("news-next").IsVisible);
        w.NewsTick(TimeSpan.FromSeconds(30)); Assert.Equal(2, w.NewsIndex); // letzte Folie bleibt stehen
        w.Find<Button>("news-done").Press();
        Assert.False(w.NewsOpen); Assert.NotEqual(WindowState.FullScreen, w.WindowState);
        Assert.Contains(w.Core.Audit.ReadAll(), a => a.Action == "news.done");
        w.Close();
    }

    [AvaloniaFact(DisplayName = "Update sofort: geladenes Update startet 30-Sekunden-Countdown (alles gesichert), danach Installation; „Jetzt neu starten“ geht sofort")]
    public async Task Countdown()
    {
        var (w, _, _) = Ui.Open();
        var up = new FakeUpdater(); w.Updater = up;
        Assert.Equal("sofort", w.Settings.UpdateWhen);
        Assert.True(await w.UpdateCheckAsync(userAsked: true)); Pump();
        Assert.Equal(30, w.UpdateCountdownLeft); Assert.Equal(0, up.Applied);
        Assert.True(w.Find<Border>("upd-countdown").IsVisible);
        Assert.Contains("30 Sekunden", w.Find<TextBlock>("upd-countdown-text").Text);
        for (var i = 0; i < 29; i++) w.UpdateCountdownStep();
        Assert.Equal(1, w.UpdateCountdownLeft); Assert.Equal(0, up.Applied);
        w.UpdateCountdownStep(); Pump();
        Assert.Equal(-1, w.UpdateCountdownLeft); Assert.Equal(1, up.Applied); Assert.True(w.UpdateApplying);
        Assert.False(w.Find<Border>("upd-countdown").IsVisible);
        w.Close();

        var (w2, _, _) = Ui.Open(); var up2 = new FakeUpdater(); w2.Updater = up2;
        await w2.UpdateCheckAsync(userAsked: true); Pump();
        w2.Find<Button>("upd-countdown-now").Press(); Pump();
        Assert.Equal(1, up2.Applied); Assert.Equal(-1, w2.UpdateCountdownLeft);
        w2.Close();
    }
}
