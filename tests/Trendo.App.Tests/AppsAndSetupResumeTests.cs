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
}
