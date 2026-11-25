using System.Diagnostics;
using System.IO.Compression;
using System.Text;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Trendo.App;
using Trendo.Core;
using Trendo.Core.Programs;
using Xunit;

namespace Trendo.App.Tests;

/// T-0763: „Alles prüfen“ (SYS-13), Einrichten & Reparieren mit Installation, Zusatzprogrammen und Quarantäne (X-02),
/// hängendes Trendo-Werkzeug beenden (X-09), klare Angebotsaussage im Betriebsmodus (SYS-01).
public class T0763SystemAlltagUi
{
    static async Task Until(Func<bool> ok, int ms = 15000)
    {
        var sw = Stopwatch.StartNew();
        while (!ok() && sw.ElapsedMilliseconds < ms) { Dispatcher.UIThread.RunJobs(); await Task.Delay(20); }
        Dispatcher.UIThread.RunJobs();
    }

    static string FakeInstall(string root)
    {
        var app = Path.Combine(root, "current"); Directory.CreateDirectory(app);
        var files = new Dictionary<string, string> { ["TrendoOS.exe"] = "PROGRAMM", ["Trendo.Core.dll"] = "KERN" };
        foreach (var (k, v) in files) File.WriteAllText(Path.Combine(app, k), v);
        File.WriteAllText(Path.Combine(app, "sq.version"), "<?xml version=\"1.0\"?><package><metadata><id>TrendoOS</id><version>9.9.9</version></metadata></package>");
        Directory.CreateDirectory(Path.Combine(root, "packages"));
        using var z = ZipFile.Open(Path.Combine(root, "packages", "TrendoOS-9.9.9-full.nupkg"), ZipArchiveMode.Create);
        using (var n = z.CreateEntry("TrendoOS.nuspec").Open()) n.Write("<?xml version=\"1.0\"?><package><metadata><id>TrendoOS</id><version>9.9.9</version></metadata></package>"u8);
        foreach (var (k, v) in files) { using var s = z.CreateEntry("lib/app/" + k).Open(); s.Write(Encoding.UTF8.GetBytes(v)); }
        return app;
    }

    [AvaloniaFact(DisplayName = "SYS-13 (T-0763): Studio öffnet „Alles prüfen“ – goldener Knopf, Ampel je Bereich mit nächstem Schritt; veränderte Protokollzeile rot mit Stelle")]
    public async Task Check_everything()
    {
        var (w, _, root) = Ui.Open();
        Assert.Equal("qual-check", w.PageForStudio("SYS-13"));
        w.Show("qual-check");
        Assert.NotNull(w.TryFind<TextBlock>("check-empty")); // erster Schritt im Leerzustand sichtbar
        var run = w.Find<Button>("check-run");
        Assert.True(MainWindow.IsGold(run)); // die eine goldene Hauptaktion
        run.Press();
        await w.CheckupTask!; await Until(() => w.TryFind<TextBlock>("check-summary") is not null);
        foreach (var id in new[] { "installation", "programme", "daten", "protokoll", "sicherung", "speicher", "abstuerze" })
        {
            Assert.NotNull(w.TryFind<Grid>("check-area-" + id));
            Assert.False(string.IsNullOrWhiteSpace(w.Find<TextBlock>("check-finding-" + id).Text), id); // keine leeren Karten
            Assert.StartsWith("Nächster Schritt: ", w.Find<TextBlock>("check-next-" + id).Text);
        }
        Assert.Contains("Nur für Entwickler", w.Find<TextBlock>("check-finding-installation").Text); // Testlauf: aus dem Quelltext, kein Paket
        Assert.Equal(Trendo.Core.Sys.Checkup.Light.Gruen, w.CheckupAreas.Single(a => a.Id == "protokoll").Light);
        Assert.Contains(w.Core.Audit.ReadAll(), e => e.Action == "pruefung.alles");

        // Protokollzeile verändern → rot, mit Nummer
        var f = Path.Combine(root, "audit", "audit.jsonl");
        var lines = File.ReadAllLines(f); Assert.True(lines.Length >= 2);
        lines[1] = lines[1].Replace("\"Detail\":\"", "\"Detail\":\"verändert ");
        File.WriteAllLines(f, lines);
        w.Find<Button>("check-run").Press();
        await w.CheckupTask!; await Until(() => w.CheckupAreas.Single(a => a.Id == "protokoll").Light == Trendo.Core.Sys.Checkup.Light.Rot);
        Assert.Contains("Eintrag 2", w.Find<TextBlock>("check-finding-protokoll").Text);
        Assert.NotNull(w.TryFind<Button>("check-go-protokoll"));
        Assert.Contains("braucht jetzt deine Hilfe", w.Find<TextBlock>("check-summary").Text);
        w.Close();
    }

    [AvaloniaFact(DisplayName = "SYS-13 (T-0763): Entwickler-Prüfungen nur in „Professionell“ und ehrlich beschriftet; Telemetrie-Karte mit echtem Ergebnis")]
    public void Developer_split()
    {
        var (w, _, root) = Ui.Open();
        w.ProgramSetLevel(UserLevel.Einfach);
        w.Show("qual-check");
        Assert.NotNull(w.TryFind<TextBlock>("check-dev-hint")); Assert.Null(w.TryFind<Button>("check-dev-ampel"));
        w.ProgramSetLevel(UserLevel.Profi);
        w.Show("qual-check");
        Assert.NotNull(w.TryFind<Button>("check-dev-ampel")); Assert.Contains(w.GetLogicalTexts(), t => t == "Nur für Entwickler");
        w.Show("qualitaet");
        Assert.StartsWith("Nur für Entwickler", w.Find<TextBlock>("ql-dev-note").Text);
        // Paketliste des Produkts (TrendoOS.deps.json) – der Testordner selbst enthält die Telemetrie des Test-Werkzeugs (Microsoft.ApplicationInsights), das zeigt die Karte auch ehrlich an
        w.Show("qual-rueckfall");
        Assert.StartsWith("✕ Gefunden: Trendo.App.Tests.deps.json: ApplicationInsights", w.Find<TextBlock>("telemetry-result").Text);
        var prod = Path.Combine(root, "programm"); Directory.CreateDirectory(prod);
        File.Copy(Path.Combine(AppContext.BaseDirectory, "TrendoOS.deps.json"), Path.Combine(prod, "TrendoOS.deps.json"));
        w.InstallDirOverride = prod; w.Show("qual-rueckfall");
        Assert.StartsWith("✓ Geprüft", w.Find<TextBlock>("telemetry-result").Text);
        w.Close();
    }

    [AvaloniaFact(DisplayName = "X-02 (T-0763): gelöschte Programmdatei erkannt und nach Rückfrage aus dem Installationspaket ersetzt")]
    public async Task Repair_installation()
    {
        var (w, _, root) = Ui.Open();
        var app = FakeInstall(Path.Combine(root, "inst")); w.InstallDirOverride = app;
        w.Show("heilung");
        // Fix N6: beim Öffnen läuft die Prüfung schon (Ergebnis ohne Klick)
        Assert.NotNull(w.HealInstallTask); await w.HealInstallTask!; await Until(() => w.TryFind<TextBlock>("heal-install-result") is not null);
        Assert.StartsWith("✓", w.Find<TextBlock>("heal-install-result").Text);
        Assert.Contains(w.GetLogicalTexts(), t => t.Contains("nicht code-signiert")); // Fix M3
        File.WriteAllText(Path.Combine(app, "Fremd.dll"), "x");
        w.Find<Button>("heal-install-check").Press(); await w.HealInstallTask!; await Until(() => w.TryFind<TextBlock>("heal-install-extra") is not null);
        Assert.Contains("Fremd.dll", w.Find<TextBlock>("heal-install-extra").Text);
        File.Delete(Path.Combine(app, "Fremd.dll"));
        File.Delete(Path.Combine(app, "Trendo.Core.dll"));
        w.Find<Button>("heal-install-check").Press(); await w.HealInstallTask!; await Until(() => w.TryFind<Button>("heal-install-repair") is not null);
        Assert.Contains("Trendo.Core.dll – fehlt", w.GetLogicalTexts().First(t => t.Contains("Trendo.Core.dll")));
        Assert.True(MainWindow.IsGold(w.Find<Button>("heal-run"))); // Hauptaktion bleibt „Jetzt prüfen und reparieren“
        Assert.False(MainWindow.IsGold(w.Find<Button>("heal-install-repair"))); Assert.False(MainWindow.IsGold(w.Find<Button>("heal-install-check")));
        w.Find<Button>("heal-install-repair").Press();
        Assert.False(File.Exists(Path.Combine(app, "Trendo.Core.dll"))); // erst Rückfrage
        Assert.StartsWith("Wirklich", (string)w.Find<Button>("heal-install-repair").Content!);
        w.Find<Button>("heal-install-repair").Press();
        await Until(() => w.HealMessage.StartsWith('✓'));
        Assert.Equal("KERN", File.ReadAllText(Path.Combine(app, "Trendo.Core.dll")));
        Assert.True(w.HealInstallReport!.Ok);
        // ohne Paket: ehrlicher nächster Schritt
        File.Delete(Directory.GetFiles(Path.Combine(root, "inst", "packages"))[0]);
        w.Find<Button>("heal-install-check").Press(); await w.HealInstallTask!; await Until(() => w.TryFind<TextBlock>("heal-install-next") is not null);
        Assert.Contains("Setup erneut", w.Find<TextBlock>("heal-install-next").Text);
        w.Close();
    }

    [AvaloniaFact(DisplayName = "X-02 (T-0763): fehlendes Zusatzprogramm mit einem Klick nachholen – erst eigene Rückfrage, dann Einrichtung; Nein ändert nichts")]
    public async Task Fetch_missing_program()
    {
        var (w, _, _) = Ui.Open();
        var order = new List<string>();
        w.SammelConfirm = comps => { order.Add("frage:" + comps[0].Id); return Task.FromResult<IReadOnlyCollection<string>?>(null); };
        w.InstallOverride = c => { order.Add("einrichten:" + c.Id); return Task.FromResult(true); };
        w.Show("heilung");
        w.Find<Button>("heal-programs-check").Press(); await w.HealProgramsTask!; await Until(() => w.TryFind<Button>("heal-fetch-") is not null);
        var b = w.TryFind<Button>("heal-fetch-")!; var id = b.Name!["heal-fetch-".Length..];
        b.Press(); await Until(() => w.HealMessage.Contains("nicht nachgeholt"));
        Assert.Equal(["frage:" + id], order); // Nein: nichts eingerichtet
        w.SammelConfirm = comps => { order.Add("frage:" + comps[0].Id); return Task.FromResult<IReadOnlyCollection<string>?>(comps.Select(c => c.Id).ToList()); };
        await w.HealProgramsTask!; await Until(() => w.TryFind<Button>("heal-fetch-" + id) is not null);
        w.Find<Button>("heal-fetch-" + id).Press(); await Until(() => w.HealMessage.Contains("nachgeholt ✓"));
        Assert.Equal(["frage:" + id, "frage:" + id, "einrichten:" + id], order); // Freigabe nie im selben Klick: erst Frage, dann Einrichtung
        w.Close();
    }

    [AvaloniaFact(DisplayName = "X-02 (T-0763): kaputte Einstellungsdatei wird beiseitegelegt und lässt sich per Knopf (mit Rückfrage) zurückholen")]
    public async Task Restore_quarantine()
    {
        var (w, _, root) = Ui.Open();
        await w.StartupHeal!;
        var f = Path.Combine(root, "ki", "kaputt.json"); Directory.CreateDirectory(Path.GetDirectoryName(f)!); File.WriteAllText(f, "{ nicht lesbar");
        w.Show("heilung");
        Assert.NotNull(w.TryFind<TextBlock>("heal-quarantine-empty"));
        w.Find<Button>("heal-run").Press();
        await Until(() => w.TryFind<Button>("heal-restore-0") is not null);
        Assert.False(File.Exists(f));
        Assert.Contains("ki/kaputt.json", w.Find<TextBlock>("heal-q-0").Text);
        w.Find<Button>("heal-restore-0").Press();
        Assert.False(File.Exists(f)); // erst Rückfrage
        Assert.Equal("Ja, zurückholen", w.Find<Button>("heal-restore-0").Content);
        w.Find<Button>("heal-restore-0").Press();
        Assert.Equal("{ nicht lesbar", File.ReadAllText(f));
        Assert.StartsWith("✓", w.Find<TextBlock>("heal-msg").Text);
        Assert.NotNull(w.TryFind<TextBlock>("heal-quarantine-empty"));
        w.Close();
    }

    [AvaloniaFact(DisplayName = "X-09 (T-0763, echt): hängendes Trendo-Werkzeug nach Rückfrage beenden (echter Prozess, per Kennung); fremdes Programm bleibt unberührt")]
    public async Task Kill_hanging_own_tool()
    {
        if (!OperatingSystem.IsWindows()) return;
        var (w, _, root) = Ui.Open();
        var tool = Path.Combine(root, "werkzeuge", "haengt", "ping.exe"); Directory.CreateDirectory(Path.GetDirectoryName(tool)!);
        File.Copy(Path.Combine(Environment.SystemDirectory, "PING.EXE"), tool);
        using var own = Process.Start(new ProcessStartInfo(tool, "-n 120 127.0.0.1") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true })!;
        using var foreign = Process.Start(new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "PING.EXE"), "-n 120 127.0.0.1") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true })!;
        try
        {
            await Task.Delay(300);
            w.Show("ressourcen"); w.TaskTick();
            w.Find<Button>("res-tab-prozesse").Press();
            var search = w.Find<TextBox>("tm-search"); search.Text = "ping"; search.RaiseEvent(new Avalonia.Input.KeyEventArgs { RoutedEvent = Avalonia.Input.InputElement.KeyUpEvent, Key = Avalonia.Input.Key.G });
            Assert.True(w.TaskRows.Single(r => r.Pid == own.Id).Own);
            Assert.False(w.TaskRows.Single(r => r.Pid == foreign.Id).Own);
            Assert.Null(w.TryFind<Button>($"tm-kill-{foreign.Id}")); // fremd: kein Knopf
            Assert.Contains(w.GetLogicalTexts(), t => t.Contains("beendet sie nie"));
            w.Find<Button>($"tm-kill-{own.Id}").Press();
            Assert.False(own.HasExited); // erst Rückfrage
            w.Find<Button>($"tm-kill-{own.Id}").Press();
            Assert.True(own.WaitForExit(5000), "Trendo-Werkzeug läuft noch");
            Assert.False(w.TaskKill(foreign.Id)); Assert.False(foreign.HasExited);
            Assert.Contains(w.Core.Audit.ReadAll(), e => e.Action == "taskmanager.beendet" && e.Detail.Contains($"pid={own.Id}"));
            Assert.NotNull(w.TryFind<TextBlock>("tm-gpu-note"));
        }
        finally
        {
            foreach (var p in new[] { own, foreign }) try { if (!p.HasExited) p.Kill(); } catch (InvalidOperationException) { }
            w.Close();
        }
    }

    [AvaloniaFact(DisplayName = "X-09 (T-0763): Spalte Grafik je Programm, sortierbar; Netzlast je Programm ehrlich als nicht vorhanden gekennzeichnet")]
    public void Gpu_column()
    {
        var (w, _, root) = Ui.Open();
        var t = TimeSpan.Zero;
        w.TaskMeasureOverride = () => { t += TimeSpan.FromMilliseconds(1); return [new(5001, "spiel", t, 100, null), new(5002, "editor", t, 100, null)]; };
        w.TaskGpuOverride = () => new Dictionary<int, double> { [5001] = 72.5, [5002] = 3 };
        w.Show("ressourcen"); w.TaskTick();
        w.Find<Button>("res-tab-prozesse").Press();
        Assert.Equal("72.5 %".Replace(".", System.Globalization.CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator), w.Find<TextBlock>("tm-gpu-5001").Text);
        w.Find<Button>("tm-sort-gpu").Press();
        Assert.StartsWith("spiel", ((TextBlock)w.Find<Grid>("tm-row-0").Children[0]).Text);
        Assert.Contains("Netzlast je Programm", w.Find<TextBlock>("tm-gpu-note").Text);
        w.TaskGpuOverride = () => null; w.TaskTick(); w.Show("ressourcen");
        Assert.Equal("–", w.Find<TextBlock>("tm-gpu-5001").Text);
        Assert.Contains("keinen Messwert", w.Find<TextBlock>("tm-gpu-note").Text);
        w.Close();
    }

    [AvaloniaFact(DisplayName = "X-02 (T-0763 Fix F4): Verknüpfung im Programmordner erscheint als eigener Hinweis in der Karte und gelb in „Alles prüfen“")]
    public async Task Links_in_app_folder()
    {
        if (!OperatingSystem.IsWindows()) return;
        var (w, _, root) = Ui.Open();
        var app = FakeInstall(Path.Combine(root, "inst")); w.InstallDirOverride = app;
        var outside = Path.Combine(root, "draussen"); Directory.CreateDirectory(outside);
        using (var p = Process.Start(new ProcessStartInfo("cmd.exe", $"/c mklink /J \"{Path.Combine(app, "plugins")}\" \"{outside}\"") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true })!) p.WaitForExit(10000);
        Assert.True(new DirectoryInfo(Path.Combine(app, "plugins")).Attributes.HasFlag(FileAttributes.ReparsePoint));
        w.Show("heilung");
        await w.HealInstallTask!; await Until(() => w.TryFind<TextBlock>("heal-install-links") is not null);
        Assert.Contains("plugins", w.Find<TextBlock>("heal-install-links").Text);
        var areas = await w.RunCheckupAsync();
        Assert.Equal(Trendo.Core.Sys.Checkup.Light.Gelb, areas.Single(a => a.Id == "installation").Light);
        Directory.Delete(Path.Combine(app, "plugins")); // nur die Verknüpfung
        w.Close();
    }

    [AvaloniaFact(DisplayName = "SYS-01 (T-0763): Betriebsmodus sagt klar „Desktop Edition für Windows 11, Linux/Pi nicht im Angebot“; Stick-Schreiber bleibt erreichbar")]
    public void Mode_offer()
    {
        var (w, _, _) = Ui.Open();
        w.Show("modus");
        Assert.Contains("Desktop Edition für Windows 11", w.Find<TextBlock>("mode-offer-head").Text);
        Assert.Contains("Linux und Raspberry Pi sind derzeit nicht im Angebot", w.Find<TextBlock>("mode-offer-text").Text);
        Assert.DoesNotContain(w.GetLogicalTexts(), x => x.Contains("von einem Linux-USB-Stick oder auf dem Raspberry Pi"));
        Assert.Contains("Nicht Teil der Desktop Edition", w.Find<TextBlock>("mode-stick-note").Text);
        // Fix M4: kein Widerspruch – Vorschau ist keine goldene Hauptaktion, Pi-Zeile ehrlich, Untertitel ohne Modus-Versprechen
        Assert.Contains("kein unterstütztes Setup", w.Find<TextBlock>("mode-offer-text").Text);
        Assert.False(MainWindow.IsGold(w.Find<Button>("mode-write-stick")));
        Assert.Contains("Vorschau", (string)w.Find<Button>("mode-write-stick").Content!);
        Assert.DoesNotContain(w.GetLogicalTexts(), x => x.Contains("BLOCKIERT – nur auf dem Raspberry Pi"));
        Assert.Contains(w.GetLogicalTexts(), x => x.Contains("Raspberry Pi, der derzeit nicht im Angebot ist"));
        Assert.DoesNotContain("startet Trendo passend", StudioCatalog.ById["SYS-01"].Sentence);
        w.Close();
    }
}
