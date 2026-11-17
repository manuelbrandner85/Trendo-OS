using System.Text.Json.Nodes;
using Microsoft.Extensions.Time.Testing;
using Trendo.Core;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>T-0713 (Nachtrag zu B4): Ablaufdatum in Decide/Execute, Verwerfen nicht eingelöster Sammel-Freigaben, offene Anfragen über einen Neustart,
/// Hinweis auf Erwachsenen-Profile ohne PIN neben einem Kinderprofil.</summary>
public class T0713FreigabenNachtrag
{
    sealed class Root : IDisposable
    {
        public string Dir { get; } = Path.Combine(Path.GetTempPath(), "trendo-t0713-" + Guid.NewGuid().ToString("N"));
        public FakeTimeProvider Time { get; } = new(DateTimeOffset.Now);
        public FakeLauncher Launcher { get; } = new();
        public TrendoCore Core() => new(Dir, new InMemorySecretStore(), Launcher, Time);
        public string Store => Path.Combine(Dir, "freigaben", "offen.json");
        public void Dispose() { try { Directory.Delete(Dir, true); } catch { } }
    }

    [Fact(DisplayName = "T-0713 Ablauf: abgelaufene Anfrage kann nicht freigegeben werden (ApprovalException + approval.expired), verschwindet aus „offen“, Ausführung blockiert")]
    public void Abgelaufen_wird_nicht_freigegeben()
    {
        using var f = new Fixture();
        var gate = f.Core.Approvals;
        var r = gate.Request(Principal.LocalOwner, "test.wirkung", "Test", "ziel");
        f.Time.Advance(TimeSpan.FromMinutes(11));
        var ex = Assert.Throws<ApprovalException>(() => gate.Approve(r.Id, Principal.LocalOwner));
        Assert.Contains("abgelaufen", ex.Message);
        Assert.Equal(ApprovalState.Expired, r.State);
        Assert.Throws<ApprovalException>(() => gate.Execute(r.Id, "test.wirkung", "ziel", () => true));
        var log = f.Core.Audit.ReadAll();
        Assert.Contains(log, a => a.Action == "approval.expired" && a.Detail == r.Id);
        Assert.DoesNotContain(log, a => a.Action is "approval.approved" or "execution.start");

        // „offen“ zeigt Abgelaufenes nicht mehr an (und markiert es); Ablehnen einer abgelaufenen Anfrage ist ohne Wirkung, kein Fehler
        var r2 = gate.Request(Principal.LocalOwner, "test.wirkung", "Test", "ziel2");
        var r3 = gate.Request(Principal.LocalOwner, "test.wirkung", "Test", "ziel3");
        f.Time.Advance(TimeSpan.FromMinutes(11));
        Assert.Empty(gate.Pending());
        Assert.Equal(ApprovalState.Expired, r2.State);
        Assert.Throws<ApprovalException>(() => gate.Deny(r2.Id, Principal.LocalOwner)); // schon abgelaufen markiert → nicht mehr offen
        Assert.Throws<ApprovalException>(() => gate.Execute(r3.Id, "test.wirkung", "ziel3", () => true));
    }

    [Fact(DisplayName = "T-0713 Ablauf: erteilt, aber zu spät ausgeführt → blockiert mit approval.expired; Ablehnen einer abgelaufenen offenen Anfrage wirft nicht")]
    public void Erteilt_aber_abgelaufen()
    {
        using var f = new Fixture();
        var gate = f.Core.Approvals;
        var r = gate.Request(Principal.LocalOwner, "test.wirkung", "Test", "ziel");
        gate.Approve(r.Id, Principal.LocalOwner);
        f.Time.Advance(TimeSpan.FromMinutes(11));
        var ex = Assert.Throws<ApprovalException>(() => gate.Execute(r.Id, "test.wirkung", "ziel", () => true));
        Assert.Contains("abgelaufen", ex.Message);
        Assert.Contains(f.Core.Audit.ReadAll(), a => a.Action == "approval.expired" && a.Detail == r.Id);

        var d = gate.Request(Principal.LocalOwner, "test.wirkung", "Test", "z2");
        f.Time.Advance(TimeSpan.FromMinutes(11));
        gate.Deny(d.Id, Principal.LocalOwner); // die Person sagt Nein – die Anfrage ist ohnehin abgelaufen
        Assert.Equal(ApprovalState.Expired, d.State);
        Assert.DoesNotContain(f.Core.Audit.ReadAll(), a => a.Action == "approval.denied" && a.Detail == d.Id);
    }

    [Fact(DisplayName = "T-0713 Sammel-Freigabe: je Komponente genau eine Freigabe, genau einmal installiert; nicht eingelöste werden verworfen und installieren nie")]
    public async Task Sammel_genau_einmal_je_Komponente()
    {
        using var f = new Fixture();
        var runner = new FakeRunner((_, _) => (0, ""));
        var deps = new DependencyManager(f.Core.Audit, runner, p => p, _ => false) { ConsentFile = Path.Combine(f.Root, "zustimmung.txt") };
        deps.GiveConsent(Principal.LocalOwner);
        var comps = new[] { DependencyCatalog.Get("gh"), DependencyCatalog.Get("typst"), DependencyCatalog.Get("streamlink") };
        var ids = comps.Select(c =>
        {
            var r = f.Core.Approvals.Request(Principal.LocalOwner, DependencyManager.InstallAction, c.Name, c.Id, TimeSpan.FromHours(2));
            f.Core.Approvals.Approve(r.Id, Principal.LocalOwner);
            return r.Id;
        }).ToList();
        // die Freigabe der einen Komponente passt nicht auf eine andere – und wird dabei nicht verbraucht (Ziel wird vor dem Einlösen geprüft)
        await Assert.ThrowsAsync<ApprovalException>(() => deps.InstallApprovedAsync(f.Core.Approvals, ids[1], Principal.LocalOwner, comps[0]));
        Assert.Empty(runner.Calls);
        await deps.InstallApprovedAsync(f.Core.Approvals, ids[0], Principal.LocalOwner, comps[0]); // echter Kern-Weg (winget ersetzt; Ergebnis egal, es zählt der eine Aufruf)
        Assert.Single(runner.Calls);
        var ran = new List<string>();
        Assert.True(f.Core.Approvals.Execute(ids[1], DependencyManager.InstallAction, comps[1].Id, () => { ran.Add(comps[1].Id); return true; }));
        // Abbruch: der Rest wird verworfen und kann nie mehr etwas ausführen; eingelöste gehen kein zweites Mal
        f.Core.Approvals.Discard(ids[2], Principal.LocalOwner);
        Assert.Throws<ApprovalException>(() => f.Core.Approvals.Execute(ids[2], DependencyManager.InstallAction, comps[2].Id, () => { ran.Add(comps[2].Id); return true; }));
        Assert.Throws<ApprovalException>(() => f.Core.Approvals.Execute(ids[1], DependencyManager.InstallAction, comps[1].Id, () => { ran.Add(comps[1].Id); return true; }));
        await Assert.ThrowsAsync<ApprovalException>(() => deps.InstallApprovedAsync(f.Core.Approvals, ids[0], Principal.LocalOwner, comps[0]));
        Assert.Single(runner.Calls);
        Assert.Equal([comps[1].Id], ran);
        var log = f.Core.Audit.ReadAll();
        foreach (var (c, id) in comps.Zip(ids).Take(2)) Assert.Single(log, a => a.Action == "execution.start" && a.Detail == $"{id} {DependencyManager.InstallAction} {c.Id}");
        Assert.Contains(log, a => a.Action == "approval.discarded" && a.Detail == ids[2]);
        // ein Kinderprofil kann fremde Freigaben nicht verwerfen
        var other = f.Core.Approvals.Request(Principal.LocalOwner, DependencyManager.InstallAction, "x", "gh");
        Assert.Throws<UnauthorizedAccessException>(() => f.Core.Approvals.Discard(other.Id, Principal.ForProfile(true)));
    }

    [Fact(DisplayName = "T-0713 Persistenz: offene, fortsetzbare Anfrage überlebt einen Neustart (atomar gespeichert), wird danach freigegeben und genau einmal ausgeführt")]
    public void Offene_Anfrage_ueberlebt_Neustart()
    {
        using var root = new Root();
        var core1 = root.Core();
        var r = core1.Integrations.PlanOpenUrl(Principal.LocalOwner, "https://example.org/");
        var nur = core1.Approvals.Request(Principal.LocalOwner, "test.nur.speicher", "nicht fortsetzbar", "x"); // ohne Fortsetzung: nicht gespeichert
        Assert.True(File.Exists(root.Store));
        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(root.Store)!, "*.tmp"));
        Assert.DoesNotContain(nur.Id, File.ReadAllText(root.Store));

        var core2 = root.Core(); // „Neustart“
        var p = Assert.Single(core2.Approvals.Pending());
        Assert.Equal(r.Id, p.Id); Assert.Equal(r.Target, p.Target); Assert.True(p.Resumable);
        Assert.Contains(core2.Audit.ReadAll(), a => a.Action == "approval.restored" && a.Detail == r.Id);
        core2.Approvals.Approve(p.Id, Principal.LocalOwner);
        Assert.False(File.Exists(root.Store)); // erteilt = nie gespeichert
        core2.Integrations.Execute(p);
        Assert.Single(root.Launcher.Opened);
        Assert.Throws<ApprovalException>(() => core2.Integrations.Execute(p));

        var core3 = root.Core();
        Assert.Empty(core3.Approvals.Pending());
        Assert.Single(root.Launcher.Opened);
    }

    [Fact(DisplayName = "T-0713 Persistenz: erteilte Freigaben überleben keinen Neustart, abgelaufene verfallen beim Laden, von Hand eingefügte Anfragen werden nicht übernommen")]
    public void Neustart_nimmt_nur_Gepruftes()
    {
        using var root = new Root();
        var core1 = root.Core();
        var erteilt = core1.Integrations.PlanOpenUrl(Principal.LocalOwner, "https://example.org/a");
        core1.Approvals.Approve(erteilt.Id, Principal.LocalOwner);
        var alt = core1.Integrations.PlanOpenUrl(Principal.LocalOwner, "https://example.org/b");
        var json = JsonNode.Parse(File.ReadAllText(root.Store))!;
        var list = json["Requests"]!.AsArray();
        Assert.Single(list); // nur die offene
        var fake = list[0]!.DeepClone(); fake["Id"] = "DEADBEEFDEADBEEF"; fake["Target"] = "https://evil.example/"; fake["ExpiresAt"] = DateTimeOffset.Now.AddDays(1).ToString("o"); // nicht abgelaufen – fällt nur durch die Prüfung im Protokoll
        list.Add(fake);
        File.WriteAllText(root.Store, json.ToJsonString());

        root.Time.Advance(TimeSpan.FromMinutes(11));
        var core2 = root.Core();
        Assert.Empty(core2.Approvals.Pending());
        Assert.Throws<ApprovalException>(() => core2.Integrations.Execute(erteilt));
        Assert.Contains(core2.Audit.ReadAll(), a => a.Action == "approval.restore.dropped" && a.Detail.Contains("abgelaufen=1") && a.Detail.Contains("ungültig=1"));
        Assert.False(File.Exists(root.Store));
        Assert.Empty(root.Launcher.Opened);
        _ = alt;
    }

    [Fact(DisplayName = "T-0713 Persistenz (Sicherheitsprüfung): gefälschte Ablage mit passendem Protokolleintrag – fremde Aktion wird nicht geladen, Laufzeit begrenzt, Texte und Person aus dem Protokoll, kein Start einer Datei, kaputtes Protokoll ohne Absturz")]
    public void Faelschung_wird_abgefangen()
    {
        using var root = new Root();
        var core1 = root.Core();
        var echt = core1.Integrations.PlanOpenUrl(Principal.LocalOwner, "https://example.org/");
        const string exe = @"C:\temp\x.exe";
        // Angreifer mit Schreibzugriff auf den Datenordner: Protokolleinträge anhängen und die Ablage umschreiben
        core1.Audit.Append("lokaler-besitzer", "approval.requested", "AAAAAAAAAAAAAAAA deps.install gh");
        core1.Audit.Append("lokaler-besitzer", "approval.requested", "BBBBBBBBBBBBBBBB browser.open_url " + exe);
        var json = JsonNode.Parse(File.ReadAllText(root.Store))!;
        var list = json["Requests"]!.AsArray();
        list[0]!["ExpiresAt"] = new DateTimeOffset(2099, 1, 1, 0, 0, 0, TimeSpan.Zero).ToString("o");
        list[0]!["Description"] = "Harmlos"; list[0]!["RequestedBy"] = "jemand";
        list.Add(new JsonObject { ["Id"] = "AAAAAAAAAAAAAAAA", ["Action"] = "deps.install", ["Target"] = "gh", ["ExpiresAt"] = DateTimeOffset.Now.AddDays(1).ToString("o") });
        list.Add(new JsonObject { ["Id"] = "BBBBBBBBBBBBBBBB", ["Action"] = "browser.open_url", ["Target"] = exe, ["ExpiresAt"] = DateTimeOffset.Now.AddDays(1).ToString("o") });
        File.WriteAllText(root.Store, json.ToJsonString());

        var core2 = root.Core();
        var open = core2.Approvals.Pending();
        Assert.DoesNotContain(open, r => r.Id == "AAAAAAAAAAAAAAAA"); // Installieren ist nicht fortsetzbar
        var p = Assert.Single(open, r => r.Id == echt.Id);
        Assert.True(p.ExpiresAt <= DateTimeOffset.Now + ApprovalGate.MaxResumableTtl);
        Assert.Equal("lokaler-besitzer", p.RequestedBy);
        Assert.DoesNotContain("Harmlos", p.Description);
        // eine geladene „URL“, die eine Datei ist, startet nie – auch mit Freigabe
        var evil = Assert.Single(open, r => r.Id == "BBBBBBBBBBBBBBBB");
        core2.Approvals.Approve(evil.Id, Principal.LocalOwner);
        Assert.Throws<ApprovalException>(() => core2.Integrations.Execute(evil));
        Assert.Empty(root.Launcher.Opened);
        Assert.False(core2.Integrations.IsValidTarget(Integrations.OpenUrl, exe));
        Assert.False(core2.Integrations.IsValidTarget(Integrations.OpenFolder, root.Dir));

        // kaputte Protokollzeile + Ablage: Start ohne Absturz, nichts übernommen
        File.AppendAllText(core2.Audit.FilePath, "{kaputt\n");
        Assert.True(File.Exists(root.Store));
        var gate3 = new ApprovalGate(core2.Audit, root.Time, root.Store); // das Laden selbst wirft nicht (der Rest des Kerns braucht ein lesbares Protokoll – unverändert)
        Assert.Empty(gate3.Pending());
    }

    [Fact(DisplayName = "T-0713 Persistenz: zwei Fenster mit demselben Datenordner – eine geladene Anfrage wird nur einmal ausgeführt")]
    public void Zwei_Fenster_einmalig()
    {
        using var root = new Root();
        var core1 = root.Core();
        var r = core1.Integrations.PlanOpenUrl(Principal.LocalOwner, "https://example.org/");
        var a = root.Core(); var b = root.Core(); // beide laden dieselbe offene Anfrage
        var pa = Assert.Single(a.Approvals.Pending()); var pb = Assert.Single(b.Approvals.Pending());
        a.Approvals.Approve(pa.Id, Principal.LocalOwner);
        a.Integrations.Execute(pa);
        Assert.Throws<ApprovalException>(() => b.Approvals.Approve(pb.Id, Principal.LocalOwner));
        Assert.Throws<ApprovalException>(() => b.Integrations.Execute(pb));
        Assert.Single(root.Launcher.Opened);
        _ = r;
    }

    [Fact(DisplayName = "T-0713 PIN-Hinweis: Erwachsenen-Profil ohne PIN neben einem Kinderprofil wird genannt; mit PIN oder ohne Kind kein Hinweis")]
    public void Pin_Hinweis()
    {
        using var root = new Root();
        Directory.CreateDirectory(root.Dir);
        new UserSettings { Name = "Erika Muster" }.Save(root.Dir);
        var store = new PersonStore(root.Dir);
        Assert.Empty(store.AdultsWithoutLockBesideChild()); // nur eine Person
        var adult = store.Add("Max Muster", "emoji:🦊", child: false);
        Assert.Empty(store.AdultsWithoutLockBesideChild()); // kein Kind
        store.Add("Kind", "emoji:🐻", child: true);
        var open = store.AdultsWithoutLockBesideChild();
        Assert.Equal(["haupt", adult.Id], open.Select(o => o.Id).OrderBy(i => i == "haupt" ? 0 : 1));
        new ProfileLock(root.Dir).Set(ProfileLock.Kind.Pin, "4711");
        new ProfileLock(store.RootOf(adult.Id)).Set(ProfileLock.Kind.Password, "langes-passwort");
        Assert.Empty(store.AdultsWithoutLockBesideChild());
    }
}
