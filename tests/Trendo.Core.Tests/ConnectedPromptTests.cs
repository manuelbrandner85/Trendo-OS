using System.Text;
using Trendo.Core.PromptAction;
using Xunit;

namespace Trendo.Core.Tests;

public class ConnectedPromptTests
{
    [Fact(DisplayName = "Kamera nennt Geräte und nimmt nichts auf")]
    public void Kamera_nennt_geraete_ohne_aufnahme()
    {
        using var fx = new Box();
        var root = Path.Combine(fx.Root, "v4l");
        Directory.CreateDirectory(Path.Combine(root, "video0"));
        File.WriteAllText(Path.Combine(root, "video0", "name"), "Hofkamera\n");
        var wf = fx.Flow(new PromptServices { VideoSys = root });
        var done = fx.Go(wf, "CORE-09", "Lesen: jetzt");
        Assert.Equal(RunStatus.Verified, done.Status);
        var text = fx.Read(done);
        Assert.Contains("Hofkamera", text);
        Assert.Contains("keine Kamera geöffnet", text);
        var blocked = fx.Go(fx.Flow(new PromptServices { VideoSys = root }), "CORE-09", "Foto: ja", "kamera-foto");
        Assert.Equal(RunStatus.Unsupported, blocked.Status);
        Assert.Empty(blocked.Artifacts);
        Assert.Contains("nicht angeschlossen", string.Join(" ", blocked.Errors));
    }

    [Fact(DisplayName = "Übersetzung kommt nur von der lokalen KI und nur für Deutsch oder Englisch")]
    public void Text_wird_nur_mit_lokaler_ki_uebersetzt()
    {
        using var fx = new Box();
        var calls = 0;
        string? system = null;
        var wf = fx.Flow(new PromptServices
        {
            Ask = (question, prompt, _) =>
            {
                calls++;
                system = prompt;
                return Task.FromResult(new KiAnswer(true, "Hello yard", "test", "lokal", [], TimeSpan.Zero));
            }
        });
        var done = fx.Go(wf, "SPEC-07", "Text: Hof\nZiel: Englisch");
        Assert.Equal(RunStatus.Verified, done.Status);
        Assert.Equal(1, calls);
        Assert.Contains("Englisch", system);
        Assert.Equal("Hello yard\n", fx.Read(done));
        calls = 0;
        var foreign = fx.Go(fx.Flow(new PromptServices { Ask = (_, _, _) => { calls++; return Task.FromResult(new KiAnswer(true, "erfunden", null, null, [], TimeSpan.Zero)); } }), "SPEC-07", "Text: Hof\nZiel: Französisch", "sprache-fr");
        Assert.Equal(RunStatus.Unsupported, foreign.Status);
        Assert.Equal(0, calls);
        var none = fx.Go(fx.Flow(), "SPEC-07", "Text: Hof", "sprache-ohne");
        Assert.Equal(RunStatus.Failed, none.Status);
        Assert.Contains("keine", string.Join(" ", none.Errors), StringComparison.OrdinalIgnoreCase);
        Assert.Empty(foreign.Artifacts);
        Assert.Empty(none.Artifacts);
    }

    [Fact(DisplayName = "Recherche liest nur eine erlaubte Seite und offline nichts")]
    public void Recherche_liest_nur_die_erlaubte_seite()
    {
        using var fx = new Box();
        var reads = 0;
        PromptServices Online() => new()
        {
            Offline = false,
            ReadBytes = (uri, _) =>
            {
                reads++;
                var html = Encoding.UTF8.GetBytes("<html><title>Hof</title><p>Apfel</p></html>");
                return Task.FromResult(("text/html", html, uri));
            }
        };
        var offline = fx.Go(fx.Flow(new PromptServices { ReadBytes = (_, _) => { reads++; throw new InvalidOperationException("Netz"); } }), "SYS-08", "Adresse: https://example.com/hof");
        Assert.Equal(RunStatus.Verified, offline.Status);
        Assert.Equal(0, reads);
        Assert.Contains("nicht gelesen", fx.Read(offline));
        reads = 0;
        var done = fx.Go(fx.Flow(Online()), "SYS-08", "Adresse: https://example.com/hof", "lese-1");
        Assert.Equal(RunStatus.Verified, done.Status);
        Assert.Equal(1, reads);
        var text = fx.Read(done);
        Assert.Contains("Hof", text);
        Assert.Contains("Apfel", text);
        reads = 0;
        var onion = fx.Go(fx.Flow(Online()), "SYS-08", "Adresse: http://example.onion/a", "lese-onion");
        Assert.Equal(RunStatus.Unsupported, onion.Status);
        Assert.Equal(0, reads);
    }

    [Fact(DisplayName = "KI-Frage schreibt nur eine echte Antwort")]
    public void Ki_fragt_die_angegebene_quelle()
    {
        using var fx = new Box();
        var wf = fx.Flow(new PromptServices { Ask = (q, _, _) => Task.FromResult(new KiAnswer(true, "Die Antwort lautet Hof.", "test", "lokal", [], TimeSpan.Zero)) });
        var done = fx.Go(wf, "CORE-02", "Frage: Wie heißt der Hof?");
        Assert.Equal(RunStatus.Verified, done.Status);
        Assert.Contains("Die Antwort lautet Hof.", fx.Read(done));
        var none = fx.Go(fx.Flow(), "CORE-02", "Frage: Wie heißt der Hof?", "ki-ohne");
        Assert.Equal(RunStatus.Failed, none.Status);
        Assert.Contains("keine", string.Join(" ", none.Errors), StringComparison.OrdinalIgnoreCase);
        Assert.Empty(none.Artifacts);
    }

    [Fact(DisplayName = "Team fragt die Rollen und erfindet ohne KI nichts")]
    public void Team_fragt_die_rollen()
    {
        using var fx = new Box();
        var calls = 0;
        var wf = fx.Flow(new PromptServices { Ask = (_, _, _) => { calls++; return Task.FromResult(new KiAnswer(true, "Fenster zu.", "test", "lokal", [], TimeSpan.Zero)); } });
        var done = fx.Go(wf, "CORE-03", "Aufgabe: Hof prüfen\nTeam: text");
        Assert.Equal(RunStatus.Verified, done.Status);
        Assert.Equal(3, calls);
        var text = fx.Read(done);
        Assert.Contains("Fenster zu.", text);
        Assert.Contains("Planer", text);
        var none = fx.Go(fx.Flow(), "CORE-03", "Aufgabe: Hof prüfen", "team-ohne");
        Assert.Equal(RunStatus.Failed, none.Status);
        Assert.Empty(none.Artifacts);
    }

    [Fact(DisplayName = "Kostenlose KI prüft nur Loopback und fragt keine Online-Dienste")]
    public void Quelle_prueft_nur_loopback()
    {
        using var fx = new Box();
        var calls = 0;
        PromptServices Online() => new()
        {
            Offline = false,
            Probe = (sources, _) =>
            {
                calls++;
                Assert.NotEmpty(sources);
                Assert.All(sources, s => Assert.True(IntelligencePromptModel.Loopback(s.BaseUrl)));
                return Task.FromResult<IReadOnlyList<(string Name, bool Open)>>([("Ollama", true)]);
            }
        };
        var quiet = fx.Go(fx.Flow(new PromptServices { Probe = (_, _) => { calls++; throw new InvalidOperationException("Netz"); } }), "X-05", "Prüfen: jetzt");
        Assert.Equal(RunStatus.Verified, quiet.Status);
        Assert.Equal(0, calls);
        Assert.Contains("nicht kontaktiert", fx.Read(quiet));
        Assert.Contains("nicht gefragt", fx.Read(quiet));
        calls = 0;
        var done = fx.Go(fx.Flow(Online()), "X-05", "Prüfen: jetzt", "quelle-1");
        Assert.Equal(RunStatus.Verified, done.Status);
        Assert.Equal(1, calls);
        Assert.Contains("erreichbar", fx.Read(done));
        Assert.Contains("nicht gefragt", fx.Read(done));
        calls = 0;
        var online = fx.Go(fx.Flow(Online()), "X-05", "Online: ja", "quelle-online");
        Assert.Equal(RunStatus.Unsupported, online.Status);
        Assert.Equal(0, calls);
    }

    [Fact(DisplayName = "Desktop und Darstellung zeigen die Werte und ändern sie nicht")]
    public void Desktop_zeigt_und_aendert_nichts()
    {
        using var fx = new Box();
        Assert.False(File.Exists(Path.Combine(fx.Root, "settings.json")));
        var done = fx.Go(fx.Flow(), "CORE-01", "Zeigen: jetzt");
        Assert.Equal(RunStatus.Verified, done.Status);
        Assert.Contains("Design: dunkel", fx.Read(done));
        Assert.Contains("nichts geändert", fx.Read(done));
        Assert.False(File.Exists(Path.Combine(fx.Root, "settings.json")));
        var blocked = fx.Go(fx.Flow(), "CORE-01", "Ändern: ja\nTheme: hell", "desktop-nein");
        Assert.Equal(RunStatus.Unsupported, blocked.Status);
        Assert.False(File.Exists(Path.Combine(fx.Root, "settings.json")));
        var theme = fx.Go(fx.Flow(), "SYS-12", "Zeigen: jetzt", "theme-1");
        Assert.Contains("Schrift-Minimum: 0", fx.Read(theme));
        var themeNo = fx.Go(fx.Flow(), "SYS-12", "Farbe: #112233", "theme-nein");
        Assert.Equal(RunStatus.Unsupported, themeNo.Status);
        Assert.False(File.Exists(Path.Combine(fx.Root, "settings.json")));
    }

    [Fact(DisplayName = "Geheimnis zeigt nur den Speicher und nie den Wert")]
    public void Geheimnis_zeigt_nur_den_speicher()
    {
        using var fx = new Box();
        fx.Core.Secrets.Set("post", "super-geheim-wert");
        var done = fx.Go(fx.Flow(), "CORE-11", "Zeigen: jetzt");
        Assert.Equal(RunStatus.Verified, done.Status);
        var text = fx.Read(done);
        Assert.Contains(fx.Core.Secrets.BackendName, text);
        Assert.DoesNotContain("super-geheim-wert", text);
        Assert.DoesNotContain("super-geheim-wert", done.Preview);
        var blocked = fx.Go(fx.Flow(), "CORE-11", "Wert: super-geheim-wert", "geheim-nein");
        Assert.Equal(RunStatus.Unsupported, blocked.Status);
        Assert.Empty(blocked.Artifacts);
        Assert.DoesNotContain("super-geheim-wert", blocked.Preview ?? "");
    }

    [Fact(DisplayName = "Download wartet auf die Freigabe und holt vorher nichts")]
    public void Download_wartet_auf_freigabe()
    {
        using var fx = new Box();
        var reads = 0;
        var bytes = Encoding.UTF8.GetBytes("hof-datei");
        PromptServices Net() => new()
        {
            Offline = false,
            ReadBytes = (uri, _) => { reads++; return Task.FromResult(("application/octet-stream", bytes, uri)); }
        };
        var note = fx.Go(fx.Flow(Net()), "SYS-03", "Adresse: https://example.com/hof.txt");
        Assert.Equal(RunStatus.Verified, note.Status);
        Assert.Equal(0, reads);
        Assert.Contains("nichts heruntergeladen", fx.Read(note));
        var wf = fx.Flow(Net());
        var planned = wf.Los(fx.Project, wf.Start(fx.Req("SYS-03", "Laden: ja\nAdresse: https://example.com/hof.txt", "laden-1"), fx.Ctx()).RunId, fx.Ctx());
        Assert.Equal(RunStatus.AwaitingApproval, planned.Status);
        Assert.Equal(0, reads);
        Assert.Contains("Noch nichts geladen", fx.Read(planned));
        var done = wf.Approve(fx.Project, planned.RunId, Principal.LocalOwner, fx.Ctx());
        Assert.Equal(RunStatus.Verified, done.Status);
        Assert.Equal(1, reads);
        Assert.Contains("Geladen: hof.txt, 9 Bytes.", done.Preview);
        var file = Directory.GetFiles(fx.Core.Projects.ProjectFilesRoot(fx.Project), "hof.txt", SearchOption.AllDirectories).Single();
        Assert.Equal(bytes, File.ReadAllBytes(file));
        var again = fx.Flow(Net());
        var second = again.Approve(fx.Project, again.Los(fx.Project, again.Start(fx.Req("SYS-03", "Laden: ja\nAdresse: https://example.com/hof.txt", "laden-2"), fx.Ctx()).RunId, fx.Ctx()).RunId, Principal.LocalOwner, fx.Ctx());
        Assert.Equal(1, reads);
        Assert.Contains("Geladen", second.Preview);
        var onion = fx.Go(fx.Flow(Net()), "SYS-03", "Laden: ja\nAdresse: http://example.onion/a", "laden-onion");
        Assert.Equal(RunStatus.Unsupported, onion.Status);
        Assert.Equal(1, reads);
    }

    [Fact(DisplayName = "Download ohne Wunsch und offline holt nichts")]
    public void Download_ohne_wunsch_holt_nichts()
    {
        using var fx = new Box();
        var reads = 0;
        var done = fx.Go(fx.Flow(new PromptServices { Offline = false, ReadBytes = (_, _) => { reads++; throw new InvalidOperationException("Netz"); } }), "SYS-03", "Freigabe: erteilt");
        Assert.Equal(RunStatus.Verified, done.Status);
        Assert.Equal(0, reads);
        var offline = fx.Go(fx.Flow(new PromptServices { Offline = true, ReadBytes = (_, _) => { reads++; throw new InvalidOperationException("Netz"); } }), "SYS-03", "Laden: ja\nAdresse: https://example.com/a.bin", "laden-offline");
        Assert.Equal(RunStatus.Unsupported, offline.Status);
        Assert.Equal(0, reads);
    }

    [Fact(DisplayName = "Installation läuft nur nach Zustimmung und Freigabe")]
    public void Installation_nur_nach_zustimmung_und_freigabe()
    {
        using var fx = new Box();
        var calls = new List<string>();
        var installed = false;
        var runner = new ScriptRunner((file, args) =>
        {
            calls.Add(file + " " + args);
            if (file == "winget") installed = true;
            return (0, "ok");
        });
        var consent = Path.Combine(fx.Root, "deps-consent.txt");
        PromptServices Ready() => new()
        {
            Runner = runner,
            FileExists = path => installed && path.Contains("ffmpeg", StringComparison.OrdinalIgnoreCase),
            ConsentFile = consent
        };
        var check = fx.Go(fx.Flow(Ready()), "SYS-04", "Programm: ffmpeg");
        Assert.Equal(RunStatus.Verified, check.Status);
        Assert.Empty(calls);
        Assert.Contains("nichts installiert", fx.Read(check));
        var evil = fx.Go(fx.Flow(Ready()), "SYS-04", "Installieren: ja\nProgramm: Evil.Tool", "inst-evil");
        Assert.Equal(RunStatus.Unsupported, evil.Status);
        Assert.Empty(calls);
        var wf = fx.Flow(Ready());
        var planned = wf.Los(fx.Project, wf.Start(fx.Req("SYS-04", "Freigabe: erteilt\nInstallieren: ja\nProgramm: ffmpeg", "inst-1"), fx.Ctx()).RunId, fx.Ctx());
        Assert.Equal(RunStatus.AwaitingApproval, planned.Status);
        Assert.Empty(calls);
        var missing = wf.Approve(fx.Project, planned.RunId, Principal.LocalOwner, fx.Ctx());
        Assert.NotEqual(RunStatus.Verified, missing.Status);
        Assert.Empty(calls);
        Assert.Contains("Zustimmung", string.Join(" ", missing.Errors));
        File.WriteAllText(consent, "Zustimmung\n");
        var next = fx.Flow(Ready());
        var wait = next.Los(fx.Project, next.Start(fx.Req("SYS-04", "Installieren: ja\nProgramm: ffmpeg", "inst-2"), fx.Ctx()).RunId, fx.Ctx());
        var done = next.Approve(fx.Project, wait.RunId, Principal.LocalOwner, fx.Ctx());
        Assert.Equal(RunStatus.Verified, done.Status);
        Assert.Contains("Installiert: FFmpeg", done.Preview);
        var winget = Assert.Single(calls);
        Assert.Contains("Gyan.FFmpeg", winget);
        Assert.Contains("--scope user", winget);
        Assert.DoesNotContain("Evil", winget);
    }

    [Fact(DisplayName = "Programm prüfen installiert nichts")]
    public void Programm_pruefen_installiert_nichts()
    {
        using var fx = new Box();
        var runner = new ScriptRunner((_, _) => throw new InvalidOperationException("winget"));
        var done = fx.Go(fx.Flow(new PromptServices { Runner = runner }), "SYS-04", "Freigabe: erteilt\nProgramm: chrome");
        Assert.Equal(RunStatus.Verified, done.Status);
        Assert.Contains("nichts installiert", fx.Read(done));
    }

    [Fact(DisplayName = "Teilen startet den Server nur nach der Freigabe")]
    public void Teilen_startet_nur_nach_freigabe()
    {
        using var fx = new Box();
        var shares = new List<string>();
        PromptServices Host() => new() { StartShare = (folder, duration) => { shares.Add(folder + "|" + duration.TotalHours); return "http://127.0.0.1:9/tok/"; } };
        var note = fx.Go(fx.Flow(Host()), "SYS-06", "Freigabe: erteilt");
        Assert.Equal(RunStatus.Verified, note.Status);
        Assert.Empty(shares);
        var wf = fx.Flow(Host());
        var planned = wf.Los(fx.Project, wf.Start(fx.Req("SYS-06", "Teilen: ja\nStunden: 2", "teil-1"), fx.Ctx()).RunId, fx.Ctx());
        Assert.Equal(RunStatus.AwaitingApproval, planned.Status);
        Assert.Empty(shares);
        var done = wf.Approve(fx.Project, planned.RunId, Principal.LocalOwner, fx.Ctx());
        Assert.Equal(RunStatus.Verified, done.Status);
        Assert.Equal("http://127.0.0.1:9/tok/", done.Preview);
        var share = Assert.Single(shares);
        Assert.StartsWith(fx.Core.Projects.ProjectFilesRoot(fx.Project), share);
        Assert.EndsWith("|2", share);
        var outside = fx.Go(fx.Flow(Host()), "SYS-06", "Teilen: ja\nOrdner: ../geheim", "teil-weg");
        Assert.NotEqual(RunStatus.Verified, outside.Status);
        Assert.Single(shares);
    }

    [Fact(DisplayName = "Teilen ohne Wunsch startet nichts")]
    public void Teilen_ohne_wunsch_startet_nichts()
    {
        using var fx = new Box();
        var shares = 0;
        var done = fx.Go(fx.Flow(new PromptServices { StartShare = (_, _) => { shares++; return "nein"; } }), "SYS-06", "Ordner: teilen");
        Assert.Equal(RunStatus.Verified, done.Status);
        Assert.Equal(0, shares);
    }

    [Fact(DisplayName = "Geräte werden gelesen und nicht gestartet")]
    public void Geraete_werden_gelesen()
    {
        using var fx = new Box();
        var done = fx.Go(fx.Flow(new PromptServices { Devices = () => [new DeviceInfo("Anzeige", "Hofschirm", "OK")] }), "SYS-07", "Lesen: jetzt");
        Assert.Equal(RunStatus.Verified, done.Status);
        Assert.Contains("Hofschirm", fx.Read(done));
        Assert.Contains("nichts gestartet", fx.Read(done));
        Assert.False(File.Exists(Path.Combine(fx.Root, "geraete.json")));
    }

    [Fact(DisplayName = "Programm startet nur nach der Freigabe")]
    public void Programm_startet_nur_nach_freigabe()
    {
        using var fx = new Box();
        fx.Core.Projects.WriteFile(fx.Project, "notiz.txt", "hallo");
        var launches = new List<string>();
        PromptServices Host() => new() { Launch = (exe, args) => { launches.Add(exe + " " + string.Join("|", args)); return "Gestartet: " + exe; } };
        var plan = fx.Go(fx.Flow(Host()), "SYS-11", "Freigabe: erteilt\nProgramm: vscode\nDatei: notiz.txt");
        Assert.Equal(RunStatus.Verified, plan.Status);
        Assert.Empty(launches);
        Assert.Contains("nichts gestartet", fx.Read(plan));
        var wf = fx.Flow(Host());
        var wait = wf.Los(fx.Project, wf.Start(fx.Req("SYS-11", "Starten: ja\nProgramm: vscode\nDatei: notiz.txt", "start-1"), fx.Ctx()).RunId, fx.Ctx());
        Assert.Equal(RunStatus.AwaitingApproval, wait.Status);
        Assert.Empty(launches);
        var done = wf.Approve(fx.Project, wait.RunId, Principal.LocalOwner, fx.Ctx());
        Assert.Equal(RunStatus.Verified, done.Status);
        var launch = Assert.Single(launches);
        Assert.StartsWith("code ", launch);
        Assert.Contains("notiz.txt", launch);
    }

    [Fact(DisplayName = "Programm planen startet nichts")]
    public void Programm_planen_startet_nichts()
    {
        using var fx = new Box();
        var launches = 0;
        var done = fx.Go(fx.Flow(new PromptServices { Launch = (_, _) => { launches++; return "nein"; } }), "SYS-11", "Programm: blender");
        Assert.Equal(RunStatus.AwaitingInput, done.Status);
        Assert.Equal(0, launches);
    }

    [Fact(DisplayName = "Prüfung schreibt das Protokoll und öffnet den Explorer nicht")]
    public void Pruefung_schreibt_protokoll()
    {
        using var fx = new Box();
        var done = fx.Go(fx.Flow(), "SYS-13", "Prüfen: jetzt");
        Assert.Equal(RunStatus.Verified, done.Status);
        var text = fx.Read(done);
        Assert.Contains("Protokoll:", text);
        Assert.Contains("Explorer wurde nicht geöffnet", text);
        var blocked = fx.Go(fx.Flow(), "SYS-13", "Explorer: ja", "explorer-1");
        Assert.Equal(RunStatus.Unsupported, blocked.Status);
        Assert.Equal(TimeSpan.FromHours(120), ForensicsLab.MaxLife);
    }

    [Fact(DisplayName = "Reparatur prüft und ändert nichts")]
    public void Reparatur_prueft_und_aendert_nichts()
    {
        using var fx = new Box();
        var broken = Path.Combine(fx.Root, "kaputt.json");
        File.WriteAllText(broken, "{");
        var done = fx.Go(fx.Flow(), "X-02", "Prüfen: jetzt");
        Assert.Equal(RunStatus.Verified, done.Status);
        Assert.Contains("nichts geändert", fx.Read(done));
        Assert.Equal("{", File.ReadAllText(broken));
        var blocked = fx.Go(fx.Flow(), "X-02", "Reparieren: ja", "heil-nein");
        Assert.Equal(RunStatus.Unsupported, blocked.Status);
        Assert.Equal("{", File.ReadAllText(broken));
        Assert.False(Directory.Exists(Path.Combine(fx.Root, "quarantaene")));
    }

    [Fact(DisplayName = "Handy listet nur adb und verbindet nichts")]
    public void Handy_listet_ohne_verbindung()
    {
        using var fx = new Box();
        var calls = new List<string>();
        var missing = fx.Go(fx.Flow(new PromptServices { FindAdb = () => null, Runner = new ScriptRunner((file, args) => { calls.Add(args); return (0, ""); }) }), "X-08", "Lesen: jetzt");
        Assert.Contains("installiert es aus diesem Auftrag nicht", fx.Read(missing));
        Assert.Empty(calls);
        var runner = new ScriptRunner((file, args) =>
        {
            calls.Add(args);
            return (0, "List of devices attached\nABC123 device usb:1-1 model:Pixel_7\n");
        });
        var done = fx.Go(fx.Flow(new PromptServices { FindAdb = () => "/tmp/adb", Runner = runner }), "X-08", "Lesen: jetzt", "handy-1");
        Assert.Equal(RunStatus.Verified, done.Status);
        Assert.Equal("devices -l", Assert.Single(calls));
        Assert.Contains("Pixel 7", fx.Read(done));
        calls.Clear();
        var blocked = fx.Go(fx.Flow(new PromptServices { FindAdb = () => "/tmp/adb", Runner = runner }), "X-08", "Verbinden: ja", "handy-nein");
        Assert.Equal(RunStatus.Unsupported, blocked.Status);
        Assert.Empty(calls);
    }

    [Fact(DisplayName = "Verbund zeigt gespeicherte Geräte und koppelt nichts")]
    public void Verbund_zeigt_ohne_kopplung()
    {
        using var fx = new Box();
        var done = fx.Go(fx.Flow(), "CORE-04", "Zeigen: jetzt");
        Assert.Equal(RunStatus.Verified, done.Status);
        Assert.Contains("nichts gekoppelt", fx.Read(done));
        Assert.False(File.Exists(Path.Combine(fx.Root, "verbund", "geraete.json")));
        var blocked = fx.Go(fx.Flow(), "CORE-04", "Koppeln: ja", "verbund-nein");
        Assert.Equal(RunStatus.Unsupported, blocked.Status);
        Assert.False(File.Exists(Path.Combine(fx.Root, "verbund", "geraete.json")));
    }

    sealed class ScriptRunner(Func<string, string, (int, string)> run) : IProcessRunner
    {
        public Task<(int Exit, string Output)> RunAsync(string file, string args, Action<string>? line = null, CancellationToken ct = default) =>
            Task.FromResult(run(file, args));
    }

    sealed class Box : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "trendo-anschluss-" + Guid.NewGuid().ToString("N"));
        public TrendoCore Core { get; }
        public string Project { get; }
        public Box()
        {
            Core = new TrendoCore(Root, new InMemorySecretStore(), offline: true);
            Project = Core.Projects.Create("probe", "Probe").Id;
        }
        public StudioWorkflow Flow(PromptServices? services = null) => new(Core, PromptActions.CreateDefault(), services: services);
        public StudioExecutionContext Ctx() => new(Project, Principal.LocalOwner, CancellationToken.None, ResourceBudget.Default);
        public StudioPromptRequest Req(string studio, string prompt, string? id = null) =>
            new(id ?? "lauf-" + studio.ToLowerInvariant(), Project, studio, prompt, [], "de", null, PrivacyMode.Local, new Provenance("test", null, null));
        public StudioExecutionResult Go(StudioWorkflow wf, string studio, string prompt, string? id = null) =>
            wf.Los(Project, wf.Start(Req(studio, prompt, id), Ctx()).RunId, Ctx());
        public string Read(StudioExecutionResult result) =>
            File.ReadAllText(Core.Projects.ResolveInProject(Project, result.Artifacts.Single(a => a.RelativePath.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)).RelativePath));
        public IEnumerable<string> Files() => Directory.GetFiles(Core.Projects.ProjectFilesRoot(Project), "*", SearchOption.AllDirectories);
        public void Dispose() { try { Directory.Delete(Root, true); } catch (IOException) { } }
    }
}
