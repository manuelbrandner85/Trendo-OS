using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Trendo.App;
using Trendo.Core;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>27.10.2026: WhatsApp & Co. als Apps (S1–S5), Ersteinrichtung fragt nach einem Neustart nicht erneut, TV-Vollbild mit „Verkleinern“-Fenster.</summary>
public class AppsAndSetupResumeTests
{
    static void Pump() { for (var i = 0; i < 5; i++) Dispatcher.UIThread.RunJobs(); }

    [AvaloniaFact(DisplayName = "Apps: WhatsApp installieren → Desktop und Startmenü, öffnet als App-Fenster mit eigenem Speicher, Entfernen löscht die Anmeldung")]
    public void Web_apps()
    {
        var (w, _, root) = Ui.Open();
        var launched = new List<(string Exe, IReadOnlyList<string> Args)>();
        w.WebAppLaunchOverride = (e, a) => launched.Add((e, a));
        w.BrowsersOverride = () => [new BrowserInfo("edge", "Microsoft Edge", @"C:\Edge\msedge.exe")];
        w.OpenAddHub("apps"); Pump();
        foreach (var n in new[] { "WhatsApp", "Telegram", "TikTok", "Instagram", "Facebook", "Telegram (Windows-Programm)" })
            Assert.Contains(w.GetLogicalTexts(), t => t == n);
        var item = w.AddItems().Single(i => i.Id == "app:whatsapp");
        Assert.False(item.Installed);
        item.Install!().GetAwaiter().GetResult();
        Assert.True(w.WebAppInstalled("whatsapp"));
        Assert.True(w.OnDesktop("app:whatsapp"));
        Assert.True(Directory.Exists(WebApps.ProfileDir(root, "whatsapp")));
        w.Show("start"); Pump();
        w.Find<Button>("app-whatsapp").Press();
        var (exe, args) = Assert.Single(launched);
        Assert.Equal(@"C:\Edge\msedge.exe", exe);
        Assert.Contains("--app=https://web.whatsapp.com/", args);
        Assert.Contains("--user-data-dir=" + WebApps.ProfileDir(root, "whatsapp"), args);
        w.RemoveWebApp("whatsapp");
        Assert.False(w.WebAppInstalled("whatsapp"));
        Assert.False(w.OnDesktop("app:whatsapp"));
        Assert.False(Directory.Exists(WebApps.ProfileDir(root, "whatsapp")));
        Assert.False(w.OpenWebApp("whatsapp")); // nicht installiert → „Hinzufügen“
        Assert.Equal("hinzufuegen", w.CurrentView);
        w.Close();
    }

    [AvaloniaFact(DisplayName = "Ersteinrichtung: nach Neustart geht es beim letzten Schritt weiter, eine schon festgelegte PIN wird nicht erneut abgefragt")]
    public void Setup_resumes()
    {
        var root = Path.Combine(Path.GetTempPath(), "trendo-ui-" + Guid.NewGuid().ToString("N"));
        var core = new TrendoCore(root, new InMemorySecretStore(), new FakeLauncher());
        var w = new MainWindow(core, new AppOptions(root, null, 0, FirstRun: true, Isolated: true), new Probe());
        w.Show(); Pump();
        Assert.Equal("einrichtung-start", w.CurrentView); // zuerst Begrüßung im Vollbild mit Claude und Nutzungsart
        Assert.True(w.SetupFullscreen);
        w.Find<Button>("wiz-start-next").Press(); Pump();
        Assert.Equal("einrichtung", w.CurrentView);
        new ProfileLock(root).Set(ProfileLock.Kind.Pin, "4711"); // PIN wurde vor dem Absturz schon festgelegt
        w.Find<TextBox>("wiz-name").Text = "Erika Muster"; w.Find<Button>("wiz-next").Press(); Pump();
        Assert.Contains(w.GetLogicalTexts(), t => t.Contains("Deine Anmeldung ist eingerichtet"));
        w.Find<Button>("wiz-next").Press(); Pump(); // Anmeldung ist schon eingerichtet → nur „Weiter“
        w.Close();

        // „Neustart“ mitten in der Einrichtung
        var core2 = new TrendoCore(root, new InMemorySecretStore(), new FakeLauncher());
        var w2 = new MainWindow(core2, new AppOptions(root, null, 0, FirstRun: true, Isolated: true), new Probe());
        w2.Show(); Pump();
        if (w2.IsLocked) Assert.True(w2.TryUnlock("4711")); // PIN ist schon festgelegt: erst entsperren, dann weiter
        Pump();
        Assert.Equal("einrichtung", w2.CurrentView);
        Assert.True(w2.GetLogicalTexts().Any(t => t.Contains("Schritt 3 von")), string.Join(" | ", w2.GetLogicalTexts().Where(t => t.Contains("Schritt") || t.Contains("Willkommen") || t.Contains("Ordner"))));
        Assert.Null(w2.TryFind<TextBox>("wiz-name"));
        w2.Find<Button>("wiz-back").Press(); Pump();
        Assert.True(w2.GetLogicalTexts().Any(t => t.Contains("Deine Anmeldung ist eingerichtet")), string.Join(" | ", w2.GetLogicalTexts().Where(t => t.Contains("Schritt") || t.Contains("Anmeld") || t.Contains("PIN"))));
        Assert.Null(w2.TryFind<TextBox>("wiz-secret"));
        w2.Close();
    }

    [AvaloniaFact(DisplayName = "Anmeldung: „Beim Start nach PIN fragen“ lässt sich abschalten")]
    public void Lock_at_start_optional()
    {
        var (w, _, root) = Ui.Open();
        new ProfileLock(root).Set(ProfileLock.Kind.Pin, "4711");
        w.Show("personen"); Pump();
        w.Find<CheckBox>("person-lock-at-start").IsChecked = false; Pump();
        Assert.False(UserSettings.Load(root).LockOnStart);
        w.LockIfNeeded();
        Assert.False(w.IsLocked);
        w.Close();
    }

    [AvaloniaFact(DisplayName = "TV-Vollbild: kleines Fenster „Verkleinern“ beim Start und bei Mausbewegung, ein Klick verkleinert")]
    public void Tv_mini_window()
    {
        var (w, _, _) = Ui.Open();
        w.TvPlayOverride = (_, _) => { };
        w.Show("tv"); Pump();
        w.TvFullscreen(true); Pump();
        Assert.True(w.TvIsFullscreen);
        Assert.True(w.TvMiniVisible);
        w.TvPointerMoved();
        Assert.True(w.TvMiniVisible);
        w.TvFullscreen(false); Pump();
        Assert.False(w.TvIsFullscreen);
        Assert.False(w.TvMiniVisible);
        w.Close();
    }

    [AvaloniaFact(DisplayName = "Ersteinrichtung im Vollbild: Claude freiwillig zuerst; Stick/SSD zeigen echte Laufwerke mit GB, zu kleine gesperrt, Windows-Laufwerk nie; ehrlicher Hinweis solange das Abbild fehlt")]
    public void Setup_start_fullscreen()
    {
        var root = Path.Combine(Path.GetTempPath(), "trendo-ui-" + Guid.NewGuid().ToString("N"));
        var core = new TrendoCore(root, new InMemorySecretStore(), new FakeLauncher());
        var w = new MainWindow(core, new AppOptions(root, null, 0, FirstRun: true, Isolated: true), new Probe());
        w.DrivesOverride = () => [("E:", "SanDisk", 64.0, true), ("F:", "Alt", 16.0, true), ("G:", "Samsung T7", 500.0, false)];
        w.Show(); Pump();
        Assert.True(w.SetupFullscreen); Assert.True(w.Find<Grid>("wiz-fullscreen").IsVisible);
        Assert.Contains(w.GetLogicalTexts(), t => t.Contains("Claude und Claude Code (freiwillig)"));
        Assert.NotNull(w.TryFind<Button>("wiz-claude"));
        w.Find<Button>("wiz-use-stick").Press(); Pump();
        Assert.Contains("64 GB · passt", w.Find<TextBlock>("wiz-drive-E").Text);
        Assert.Contains("zu klein", w.Find<TextBlock>("wiz-drive-F").Text);
        Assert.Null(w.TryFind<TextBlock>("wiz-drive-G")); // SSD erscheint nicht bei „Stick“
        Assert.Contains(w.GetLogicalTexts(), t => t.Contains("Abbild für Stick und SSD wird gerade gebaut"));
        w.Find<Button>("wiz-use-ssd").Press(); Pump();
        Assert.Contains("500 GB · passt", w.Find<TextBlock>("wiz-drive-G").Text);
        w.Find<Button>("wiz-start-next").Press(); Pump();
        Assert.Equal("einrichtung", w.CurrentView); Assert.True(w.SetupFullscreen);
        w.Close();
    }

    [AvaloniaFact(DisplayName = "Stick-Schreiber: Abbild im Release erkannt, Laufwerk wählen, Löschen erst nach Eintippen des Buchstabens, laden, prüfen, schreiben (virtuelle Festplatte), Boot-Tasten")]
    public async Task Setup_write_stick()
    {
        var root = Path.Combine(Path.GetTempPath(), "trendo-ui-" + Guid.NewGuid().ToString("N"));
        var core = new TrendoCore(root, new InMemorySecretStore(), new FakeLauncher());
        var w = new MainWindow(core, new AppOptions(root, null, 0, FirstRun: true, Isolated: true), new Probe());
        var img = new byte[3_000_017]; new Random(3).NextBytes(img);
        var sha = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(img));
        var manifest = $$"""{"schema":1,"version":"0.9.71","bytes":{{img.Length}},"sha256":"{{sha}}","min_gb":32,"parts":[{"file":"TrendoOS-Abbild.part00","sha256":"{{sha}}","bytes":{{img.Length}}}]}""";
        var asked = new List<string>();
        w.VollpaketOnlineOverride = u => { lock (asked) asked.Add(u); return Task.FromResult(u.EndsWith(".json") ? System.Text.Encoding.UTF8.GetBytes(manifest) : img); };
        var disk = Path.Combine(root, "virtuell.disk");
        w.ImageWriteOverride = async (file, letter, m, ssd) =>
        {
            Assert.Equal("E:", letter); Assert.False(ssd);
            await using var src = File.OpenRead(file);
            await using var dst = new FileStream(disk, FileMode.Create, FileAccess.ReadWrite);
            var err = await TrendoImage.WriteAndVerifyAsync(src, dst, m.Bytes, m.Sha256);
            return (err is null, err ?? "Trendo OS steht auf „SanDisk“ und ist geprüft.");
        };
        w.DrivesOverride = () => [("E:", "SanDisk", 64.0, true), ("F:", "Alt", 16.0, true)];
        try
        {
            w.Show(); Pump();
            Assert.Null(w.ImageManifest); // Konstruktor prüft vor dem Netz-Ersatz: Tests gehen nie ins Internet
            await w.CheckImageAsync(); Pump();
            Assert.Equal("0.9.71", w.ImageManifest!.Version);
            Assert.StartsWith(TrendoImage.LatestBase, asked[0]);
            w.Find<Button>("wiz-use-stick").Press(); Pump();
            Assert.Contains(w.GetLogicalTexts(), t => t.Contains("Trendo OS 0.9.71 liegt bereit"));
            Assert.Null(w.TryFind<Button>("wiz-pick-F")); // zu klein: nicht wählbar
            w.Find<Button>("wiz-pick-E").Press(); Pump();
            w.Find<Button>("wiz-start-next").Press(); Pump();
            Assert.Equal("einrichtung-abbild", w.CurrentView);
            Assert.Contains(w.GetLogicalTexts(), t => t.Contains("Alles auf SanDisk (E:, 64 GB) wird gelöscht"));
            var write = w.Find<Button>("wiz-img-write");
            Assert.False(write.IsEnabled);
            w.Find<TextBox>("wiz-img-confirm").Text = "F"; Pump(); Assert.False(write.IsEnabled);
            w.Find<TextBox>("wiz-img-confirm").Text = "e"; Pump(); Assert.True(write.IsEnabled);
            write.Press();
            for (var i = 0; i < 500 && w.TryFind<TextBlock>("wiz-img-done") is null; i++) { await Task.Delay(10); Pump(); }
            Assert.Contains("ist geprüft", w.Find<TextBlock>("wiz-img-done").Text);
            Assert.Equal(img, (await File.ReadAllBytesAsync(disk))[..img.Length]);
            Assert.Contains(w.GetLogicalTexts(), t => t.Contains("Lenovo: F12"));
            Assert.False(File.Exists(Path.Combine(root, "abbild", "0.9.71", "TrendoOS-Abbild.part00"))); // Teile nach der Prüfung gelöscht
            w.Find<Button>("wiz-img-next").Press(); Pump();
            Assert.Equal("einrichtung", w.CurrentView);
        }
        finally { w.Close(); }
    }

    [Fact(DisplayName = "Stick-Schreiber schützt: Start-/Systemlaufwerk, Windows-Laufwerk, interne Platte als „Stick“ und zu kleine Laufwerke werden verweigert")]
    public void Writer_refuses()
    {
        var gb = 1_000_000_000L;
        Assert.Empty(ImageWriter.Refuse(new(2, false, false, 64 * gb, "USB", "SanDisk", ["E"]), 2 * gb, false, "C:\\"));
        Assert.Contains(ImageWriter.Refuse(new(0, true, true, 1000 * gb, "NVMe", "Samsung", ["C"]), 2 * gb, true, "C:\\"), t => t.Contains("Systemlaufwerk"));
        Assert.Contains(ImageWriter.Refuse(new(1, false, false, 1000 * gb, "USB", "x", ["D", "C"]), 2 * gb, true, "C:\\"), t => t.Contains("Windows"));
        Assert.Contains(ImageWriter.Refuse(new(1, false, false, 1000 * gb, "SATA", "x", ["D"]), 2 * gb, false, "C:\\"), t => t.Contains("kein USB"));
        Assert.Contains(ImageWriter.Refuse(new(1, false, false, 1000 * gb, "SATA", "x", ["D"]), 2 * gb, true, "C:\\"), t => t.Contains("kein externes"));
        Assert.Contains(ImageWriter.Refuse(new(3, false, false, 16 * gb, "USB", "x", ["F"]), 2 * gb, false, "C:\\"), t => t.Contains("zu klein"));
    }
}
