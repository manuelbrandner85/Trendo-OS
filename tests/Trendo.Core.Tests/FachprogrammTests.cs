using Trendo.Core;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>Paket 44: Fachprogramme nur bei Bedarf. Nichts wird beim Anlegen gestartet oder installiert.</summary>
public class Paket44_Fachprogramme
{
    sealed class Runner : IProcessRunner
    {
        public List<(string File, string Args)> Calls { get; } = [];
        public int Exit { get; set; }
        public string Output { get; set; } = "Blender 4.2.0\nmore";
        public Task<(int Exit, string Output)> RunAsync(string file, string args, Action<string>? line = null, CancellationToken ct = default)
        {
            Calls.Add((file, args));
            ct.ThrowIfCancellationRequested();
            return Task.FromResult((Exit, Output));
        }
    }

    sealed class Secrets : ISecretStore
    {
        private readonly Dictionary<string, string> _d = new();
        public int Reads, Writes;
        public string BackendName => "Zähler";
        public void Set(string key, string value) { Writes++; _d[key] = value; }
        public string? Get(string key) { Reads++; return _d.TryGetValue(key, out var v) ? v : null; }
        public bool Delete(string key) => _d.Remove(key);
    }

    static FachprogrammAdapter Neu(Fixture f, IProcessRunner runner, Secrets secrets, bool offline = false, IReadOnlyList<Fachprogramm>? katalog = null) =>
        new(new DependencyManager(f.Core.Audit, runner, p => p, _ => false)
        {
            ConsentFile = Path.Combine(f.Core.DataRoot, "c.txt"),
            ToolsFile = Path.Combine(f.Core.DataRoot, "tools.json")
        }, f.Core.Approvals, secrets, f.Core.Audit, f.Launcher, offline, katalog);

    static string Frei(Fixture f, string action, string id)
    {
        var r = f.Core.Approvals.Request(Principal.LocalOwner, action, id, id);
        f.Core.Approvals.Approve(r.Id, Principal.LocalOwner);
        return r.Id;
    }

    [Fact(DisplayName = "Paket 44: Katalog zeigt auf vorhandene Programme, nicht auf KI oder das Basispaket")]
    public void Katalog_bleibt_bei_den_Fachprogrammen()
    {
        Assert.Equal(11, FachprogrammKatalog.Alle.Select(p => p.Id).Distinct().Count());
        Assert.Contains(FachprogrammKatalog.Alle, p => p.Id == "blender");
        Assert.Contains(FachprogrammKatalog.Alle, p => p.Id == "unreal");
        foreach (var p in FachprogrammKatalog.Alle)
        {
            var c = DependencyCatalog.Get(p.Id);
            Assert.False(p.BrauchtGeheimnis);
            Assert.DoesNotContain(p.Id, new[] { "ollama", "model", "model-3b", "sumatra", "mpv", "7zip" });
            Assert.NotEqual(DeliveryMode.Bundled, c.Mode);
            Assert.NotEqual(DeliveryMode.NotNeeded, c.Mode);
        }
    }

    [Fact(DisplayName = "Paket 44: Anlegen und Erkennen starten nichts und laden nichts")]
    public void Nichts_beim_Start()
    {
        using var f = new Fixture();
        var runner = new Runner();
        var secrets = new Secrets();
        var a = Neu(f, runner, secrets);
        foreach (var p in FachprogrammKatalog.Alle)
        {
            var b = a.Erkennen(p.Id);
            Assert.Equal(FachprogrammStand.NichtGefunden, b.Stand);
            Assert.False(b.Angebunden);
            Assert.Contains("nichts installiert", b.Hinweis);
        }
        Assert.Empty(runner.Calls);
        Assert.Empty(f.Launcher.Opened);
        Assert.Equal(0, secrets.Reads);
        Assert.Equal(0, secrets.Writes);
        Assert.False(a.IstAngebunden("blender"));
    }

    [Fact(DisplayName = "Paket 44: Blender antwortet nur in der Prüfung, bleibt danach nicht geladen")]
    public async Task Probe_blender_ehrlich()
    {
        using var f = new Fixture();
        var runner = new Runner();
        var a = Neu(f, runner, new Secrets());
        a.PfadSuche = p => p.Id == "blender" ? "/opt/blender" : null;
        var vorher = a.Erkennen("blender");
        Assert.Equal(FachprogrammStand.GefundenNichtGeprueft, vorher.Stand);
        Assert.Empty(runner.Calls);

        var ok = await a.ProbeAsync("blender");
        Assert.Equal(FachprogrammStand.Erreichbar, ok.Stand);
        Assert.Equal("Blender 4.2.0", ok.Ausgabe);
        Assert.False(ok.Angebunden);
        Assert.Equal(("/opt/blender", "--version"), runner.Calls.Single());
        Assert.Empty(f.Launcher.Opened);

        var verbunden = a.Anbinden("blender");
        Assert.True(verbunden.Angebunden);
        Assert.True(a.IstAngebunden("blender"));
        Assert.Empty(f.Launcher.Opened);

        a.Trennen("blender");
        Assert.False(a.IstAngebunden("blender"));
        var frisch = Neu(f, runner, new Secrets());
        frisch.PfadSuche = a.PfadSuche;
        Assert.False(frisch.IstAngebunden("blender"));
        Assert.NotEqual(FachprogrammStand.Erreichbar, frisch.Erkennen("blender").Stand);
        Assert.Single(runner.Calls);
    }

    [Fact(DisplayName = "Paket 44: kaputte Prüfung ist nicht bereit, Unreal wird ohne Fenster nicht als bereit gemeldet")]
    public async Task Probe_fehlt_oder_ohne_Argument()
    {
        using var f = new Fixture();
        var runner = new Runner { Exit = 1, Output = "Fehler" };
        var a = Neu(f, runner, new Secrets());
        a.PfadSuche = p => p.Id is "blender" or "unreal" ? "/opt/" + p.Id : null;
        var kaputt = await a.ProbeAsync("blender");
        Assert.Equal(FachprogrammStand.NichtErreichbar, kaputt.Stand);
        Assert.Contains("nicht geklappt", kaputt.Hinweis);
        Assert.False(a.Anbinden("blender").Angebunden);

        var unreal = await a.ProbeAsync("unreal");
        Assert.Equal(FachprogrammStand.GefundenNichtGeprueft, unreal.Stand);
        Assert.Contains("offen", unreal.Hinweis);
        Assert.DoesNotContain(runner.Calls, c => c.File.Contains("unreal"));
        Assert.False(a.Anbinden("unreal").Angebunden);
    }

    sealed class Haenger : IProcessRunner
    {
        public List<(string File, string Args)> Calls { get; } = [];
        public async Task<(int Exit, string Output)> RunAsync(string file, string args, Action<string>? line = null, CancellationToken ct = default)
        {
            Calls.Add((file, args));
            await Task.Delay(Timeout.InfiniteTimeSpan, ct);
            return (0, "");
        }
    }

    [Fact(DisplayName = "Paket 44: LibreOffice und VS Code starten nach --version und Freigabe, sonst nicht")]
    public async Task Versionsabfrage_startet_danach()
    {
        using var f = new Fixture();
        var runner = new Runner { Output = "LibreOffice 24.8.2\n" };
        var a = Neu(f, runner, new Secrets());
        var fehlt = await a.ProbeAsync("libreoffice");
        Assert.Equal(FachprogrammStand.NichtGefunden, fehlt.Stand);
        Assert.Contains("nichts installiert", fehlt.Hinweis);
        Assert.Empty(runner.Calls);
        Assert.Empty(f.Launcher.Opened);

        a.PfadSuche = p => p.Id is "libreoffice" or "vscode" ? "/opt/" + p.Id : null;
        var ok = await a.ProbeAsync("libreoffice");
        Assert.Equal(FachprogrammStand.Erreichbar, ok.Stand);
        Assert.Equal("LibreOffice 24.8.2", ok.Ausgabe);
        Assert.Equal(("/opt/libreoffice", "--version"), runner.Calls.Single());
        Assert.Empty(f.Launcher.Opened);
        a.Anbinden("libreoffice");
        var auf = a.Aufrufen(Frei(f, FachprogrammAdapter.AufrufAktion, "libreoffice"), "libreoffice", ["brief.docx"]);
        Assert.Contains("Geöffnet", auf.Hinweis);
        Assert.Contains("brief.docx", f.Launcher.Opened.Single());

        runner.Output = "1.90.0\n";
        runner.Exit = 0;
        var code = await a.ProbeAsync("vscode");
        Assert.Equal(FachprogrammStand.Erreichbar, code.Stand);
        Assert.Contains(runner.Calls, c => c == ("/opt/vscode", "--version"));
        Assert.Single(f.Launcher.Opened);

        runner.Exit = 2;
        runner.Output = "nein";
        var neu = Neu(f, runner, new Secrets());
        neu.PfadSuche = a.PfadSuche;
        var schlecht = await neu.ProbeAsync("vscode");
        Assert.Equal(FachprogrammStand.NichtErreichbar, schlecht.Stand);
        Assert.Contains("nicht geklappt", schlecht.Hinweis);
        Assert.False(neu.Anbinden("vscode").Angebunden);
        Assert.Single(f.Launcher.Opened);
    }

    [Fact(DisplayName = "Paket 44: läuft die Versionsabfrage zu lange, startet LibreOffice nicht")]
    public async Task Versionsabfrage_laeuft_ab()
    {
        using var f = new Fixture();
        var runner = new Haenger();
        var a = Neu(f, runner, new Secrets());
        a.Versionszeit = TimeSpan.FromMilliseconds(80);
        a.PfadSuche = p => p.Id == "libreoffice" ? "/opt/soffice" : null;
        var zeit = await a.ProbeAsync("libreoffice");
        Assert.Equal(FachprogrammStand.NichtErreichbar, zeit.Stand);
        Assert.Contains("zu lange gedauert", zeit.Hinweis);
        Assert.Contains("nicht gestartet", zeit.Hinweis);
        Assert.Equal(("/opt/soffice", "--version"), runner.Calls.Single());
        Assert.False(a.Anbinden("libreoffice").Angebunden);
        Assert.Empty(f.Launcher.Opened);
    }

    [Fact(DisplayName = "Paket 44: OBS liest unter Windows die Dateiversion und startet sonst mit --version")]
    public async Task Obs_dateiversion_oder_version()
    {
        using var f = new Fixture();
        var runner = new Runner { Output = "OBS Studio 30.2.3\n" };
        var windows = Neu(f, runner, new Secrets());
        windows.ObsWieWindows = true;
        windows.DateiversionLesen = _ => "30.2.3.0";
        windows.PfadSuche = p => p.Id == "obs" ? "/opt/obs" : null;
        var datei = await windows.ProbeAsync("obs");
        Assert.Equal(FachprogrammStand.Erreichbar, datei.Stand);
        Assert.Equal("30.2.3.0", datei.Ausgabe);
        Assert.Empty(runner.Calls);
        Assert.Empty(f.Launcher.Opened);
        windows.Anbinden("obs");
        var auf = windows.Aufrufen(Frei(f, FachprogrammAdapter.AufrufAktion, "obs"), "obs", ["--collection", "Trendo"]);
        Assert.Contains("Geöffnet", auf.Hinweis);
        Assert.Contains("--collection", f.Launcher.Opened.Single());

        var leer = Neu(f, runner, new Secrets());
        leer.ObsWieWindows = true;
        leer.DateiversionLesen = _ => "  ";
        leer.PfadSuche = windows.PfadSuche;
        var unlesbar = await leer.ProbeAsync("obs");
        Assert.Equal(FachprogrammStand.NichtErreichbar, unlesbar.Stand);
        Assert.Contains("nicht lesbar", unlesbar.Hinweis);
        Assert.Contains("nicht gestartet", unlesbar.Hinweis);
        Assert.Empty(runner.Calls);
        Assert.False(leer.Anbinden("obs").Angebunden);

        var linux = Neu(f, runner, new Secrets());
        linux.ObsWieWindows = false;
        linux.PfadSuche = windows.PfadSuche;
        var arg = await linux.ProbeAsync("obs");
        Assert.Equal(FachprogrammStand.Erreichbar, arg.Stand);
        Assert.Equal("OBS Studio 30.2.3", arg.Ausgabe);
        Assert.Equal(("/opt/obs", "--version"), runner.Calls.Single());
        Assert.Single(f.Launcher.Opened);
    }

    [Fact(DisplayName = "Paket 44: Öffnen nur nach Prüfung und Freigabe, dieselbe Freigabe nur einmal")]
    public async Task Aufruf_braucht_Freigabe()
    {
        using var f = new Fixture();
        var runner = new Runner();
        var a = Neu(f, runner, new Secrets());
        a.PfadSuche = p => p.Id == "blender" ? "/opt/blender" : null;
        var ohne = a.Aufrufen("fehlt", "blender");
        Assert.Contains("Verbindung prüfen", ohne.Hinweis);
        Assert.Empty(f.Launcher.Opened);

        await a.ProbeAsync("blender");
        a.Anbinden("blender");
        var gesperrt = a.Aufrufen("fehlt", "blender");
        Assert.Equal(FachprogrammStand.FreigabeNoetig, gesperrt.Stand);
        Assert.Empty(f.Launcher.Opened);
        Assert.True(a.IstAngebunden("blender"));

        var id = Frei(f, FachprogrammAdapter.AufrufAktion, "blender");
        var auf = a.Aufrufen(id, "blender");
        Assert.Contains("Geöffnet", auf.Hinweis);
        Assert.Contains("/opt/blender", f.Launcher.Opened.Single());
        var nochmal = a.Aufrufen(id, "blender");
        Assert.Equal(FachprogrammStand.FreigabeNoetig, nochmal.Stand);
        Assert.Single(f.Launcher.Opened);
    }

    [Fact(DisplayName = "Paket 44: Installation nur nach Freigabe, lokal zuerst, Unreal nie automatisch, offline nichts")]
    public async Task Installation_nur_nach_Freigabe()
    {
        using var f = new Fixture();
        var runner = new Runner();
        var secrets = new Secrets();
        var a = Neu(f, runner, secrets);
        a.PfadSuche = p => p.Id == "krita" ? "/opt/krita" : null;
        var da = await a.InstallierenAsync(Principal.LocalOwner, "egal", "krita");
        Assert.Contains("nichts heruntergeladen", da.Hinweis);
        Assert.Empty(runner.Calls);

        var deps = new DependencyManager(f.Core.Audit, runner, p => p, _ => false) { ConsentFile = Path.Combine(f.Core.DataRoot, "c.txt") };
        deps.GiveConsent(Principal.LocalOwner);
        var mit = new FachprogrammAdapter(deps, f.Core.Approvals, secrets, f.Core.Audit, f.Launcher);
        var nein = await mit.InstallierenAsync(Principal.LocalOwner, "fehlt", "blender");
        Assert.Equal(FachprogrammStand.FreigabeNoetig, nein.Stand);
        Assert.Contains("Freigabe", nein.Hinweis);
        Assert.Empty(runner.Calls);

        var unreal = Frei(f, FachprogrammAdapter.InstallAktion, "unreal");
        var gefuehrt = await a.InstallierenAsync(Principal.LocalOwner, unreal, "unreal");
        Assert.Contains("nicht automatisch", gefuehrt.Hinweis);
        Assert.Empty(runner.Calls);
        Assert.Empty(f.Launcher.Opened);

        var offline = new FachprogrammAdapter(deps, f.Core.Approvals, secrets, f.Core.Audit, f.Launcher, offline: true);
        var weg = await offline.InstallierenAsync(Principal.LocalOwner, Frei(f, FachprogrammAdapter.InstallAktion, "blender"), "blender");
        Assert.Contains("Offline", weg.Hinweis);
        Assert.Empty(runner.Calls);

        var ja = Frei(f, FachprogrammAdapter.InstallAktion, "inkscape");
        var versuch = await mit.InstallierenAsync(Principal.LocalOwner, ja, "inkscape");
        Assert.Equal(FachprogrammStand.NichtGefunden, versuch.Stand);
        Assert.Contains(runner.Calls, c => c.Args.Contains("Inkscape.Inkscape"));
        Assert.DoesNotContain(f.Core.Audit.ReadAll(), e => e.Detail.Contains("Inkscape.Inkscape") && e.Detail.Contains("geheim"));
    }

    [Fact(DisplayName = "Paket 44: Schlüssel nur im Secret-Store, nie im Protokoll, und nur wenn nötig")]
    public async Task Schluessel_nicht_im_Protokoll()
    {
        using var f = new Fixture();
        var runner = new Runner();
        var secrets = new Secrets();
        var normal = Neu(f, runner, secrets);
        Assert.Throws<InvalidOperationException>(() => normal.ZugangMerken("blender", "nicht-speichern"));
        Assert.Equal(0, secrets.Writes);

        const string geheim = "zugang-nur-im-speicher";
        var katalog = new List<Fachprogramm> { new("blender", "Blender", "3D", true) };
        var a = Neu(f, runner, secrets, katalog: katalog);
        var ohne = await a.ProbeAsync("blender");
        Assert.Equal(FachprogrammStand.NichtErreichbar, ohne.Stand);
        Assert.Contains("Zugang fehlt", ohne.Hinweis);
        Assert.Empty(runner.Calls);
        a.ZugangMerken("blender", geheim);
        Assert.Equal(geheim, secrets.Get("fachprogramm/blender"));
        Assert.DoesNotContain(f.Core.Audit.ReadAll(), e => e.Detail.Contains(geheim) || e.Action.Contains(geheim));
    }
}
