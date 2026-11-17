using Trendo.Core;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>B4 (T-0706): Freigaben im Kern. Je Wirkungsart: ohne gültige Freigabe verweigert der Kern (ApprovalException) und es passiert nichts;
/// mit Freigabe genau einmal; dieselbe Freigabe ist nicht wiederverwendbar; alles steht im Protokoll. Dazu: Kinderprofil darf nicht freigeben.</summary>
public class FreigabenKern
{
    static DependencyManager Deps(Fixture f, FakeRunner runner)
    {
        var d = new DependencyManager(f.Core.Audit, runner, p => p, _ => false) { ConsentFile = Path.Combine(f.Root, "zustimmung.txt") };
        d.GiveConsent(Principal.LocalOwner);
        return d;
    }

    static string Frei(Fixture f, string action, string target, Principal? by = null)
    {
        var r = f.Core.Approvals.Request(by ?? Principal.LocalOwner, action, "Test", target);
        f.Core.Approvals.Approve(r.Id, Principal.LocalOwner);
        return r.Id;
    }

    [Fact(DisplayName = "B4 Installieren: ohne Freigabe, offen, falsches Ziel oder zweites Mal verweigert der Kern – mit Freigabe läuft es genau einmal, im Protokoll")]
    public async Task Installieren_nur_mit_Freigabe()
    {
        using var f = new Fixture();
        var runner = new FakeRunner((_, _) => (0, ""));
        var deps = Deps(f, runner);
        var gh = DependencyCatalog.Get("gh");

        await Assert.ThrowsAsync<ApprovalException>(() => deps.InstallApprovedAsync(f.Core.Approvals, "gibt-es-nicht", Principal.LocalOwner, gh));
        var offen = f.Core.Approvals.Request(Principal.LocalOwner, DependencyManager.InstallAction, "gh", gh.Id);
        await Assert.ThrowsAsync<ApprovalException>(() => deps.InstallApprovedAsync(f.Core.Approvals, offen.Id, Principal.LocalOwner, gh));
        var anderes = Frei(f, DependencyManager.InstallAction, "ffmpeg");
        await Assert.ThrowsAsync<ApprovalException>(() => deps.InstallApprovedAsync(f.Core.Approvals, anderes, Principal.LocalOwner, gh));
        Assert.Empty(runner.Calls);

        var id = Frei(f, DependencyManager.InstallAction, gh.Id);
        await deps.InstallApprovedAsync(f.Core.Approvals, id, Principal.LocalOwner, gh);
        Assert.Single(runner.Calls);
        Assert.Contains("winget install --id GitHub.cli", runner.Calls[0]);
        await Assert.ThrowsAsync<ApprovalException>(() => deps.InstallApprovedAsync(f.Core.Approvals, id, Principal.LocalOwner, gh));
        Assert.Single(runner.Calls);

        var log = f.Core.Audit.ReadAll();
        Assert.Contains(log, a => a.Action == "approval.approved" && a.Detail == id);
        Assert.Contains(log, a => a.Action == "execution.start" && a.Detail == $"{id} deps.install gh");
        Assert.Contains(log, a => a.Action == "execution.done" && a.Detail == id);
        Assert.Equal(4, log.Count(a => a.Action == "execution.blocked"));
        Assert.True(f.Core.Audit.Verify());
    }

    [Fact(DisplayName = "B4 Online-KI einschalten (Daten verlassen den PC): nur mit Freigabe genau dieses Dienstes, einmalig, im Protokoll")]
    public void KiCloud_nur_mit_Freigabe()
    {
        using var f = new Fixture();
        var dienst = KiCatalog.Cloud[0].Id;
        Assert.Throws<ApprovalException>(() => KiSourceSettings.EnableCloud(f.Root, f.Core.Approvals, "gibt-es-nicht", dienst));
        var fremd = Frei(f, KiSourceSettings.CloudOnAction, KiCatalog.Cloud[^1].Id + "-anders");
        Assert.Throws<ApprovalException>(() => KiSourceSettings.EnableCloud(f.Root, f.Core.Approvals, fremd, dienst));
        Assert.DoesNotContain(dienst, KiSourceSettings.Load(f.Root).Cloud);

        var id = Frei(f, KiSourceSettings.CloudOnAction, dienst);
        KiSourceSettings.EnableCloud(f.Root, f.Core.Approvals, id, dienst);
        Assert.Contains(dienst, KiSourceSettings.Load(f.Root).Cloud);
        // Ausschalten (sicherer Weg) braucht keine Freigabe; wieder einschalten mit der alten Freigabe geht nicht
        KiSourceSettings.Update(f.Root, s => s.ToggleCloud(dienst));
        Assert.Throws<ApprovalException>(() => KiSourceSettings.EnableCloud(f.Root, f.Core.Approvals, id, dienst));
        Assert.DoesNotContain(dienst, KiSourceSettings.Load(f.Root).Cloud);
        Assert.Contains(f.Core.Audit.ReadAll(), a => a.Action == "execution.done" && a.Detail == id);
    }

    [Fact(DisplayName = "B4 Ablauf freigeben (verschiebt Dateien): ohne Freigabe bleibt der Ablauf gesperrt, mit Freigabe genau einmal; Änderung verlangt neue Freigabe")]
    public void Ablauf_nur_mit_Freigabe()
    {
        using var f = new Fixture();
        var allowed = Path.Combine(f.Root, "eingang"); Directory.CreateDirectory(allowed);
        var eng = new WorkflowEngine(f.Root, f.Core.Audit, f.Core.Approvals, [allowed]);
        var w = eng.Add(new Workflow { Id = "wf1", Name = "Sortieren", Steps = [new() { Kind = StepKind.Verschieben, From = allowed, To = Path.Combine(allowed, "ziel") }] });
        Assert.True(w.NeedsApproval); Assert.False(w.IsApproved);
        Assert.Throws<ApprovalException>(() => eng.ApplyApproval(w, "gibt-es-nicht"));
        var offen = eng.RequestApproval(w);
        Assert.Throws<ApprovalException>(() => eng.ApplyApproval(w, offen.Id));
        Assert.False(w.IsApproved);
        f.Core.Approvals.Approve(offen.Id, Principal.LocalOwner);
        eng.ApplyApproval(w, offen.Id);
        Assert.True(w.IsApproved);
        Assert.Throws<ApprovalException>(() => eng.ApplyApproval(w, offen.Id));
    }

    [Fact(DisplayName = "B4 Rollen: Kinderprofil darf anfragen und zurückziehen, aber nie freigeben – der Kern lehnt ab, nichts wird ausgeführt, rbac.denied im Protokoll")]
    public async Task Kinderprofil_darf_nicht_freigeben()
    {
        using var f = new Fixture();
        Assert.Same(Principal.LocalOwner, Principal.ForProfile(false));
        var kind = Principal.ForProfile(true);
        Assert.Equal(Role.User, kind.Role);
        Assert.False(Rbac.Has(kind, Permission.ApproveAction));

        var runner = new FakeRunner((_, _) => (0, ""));
        var deps = Deps(f, runner);
        var gh = DependencyCatalog.Get("gh");
        var r = f.Core.Approvals.Request(kind, DependencyManager.InstallAction, "gh", gh.Id);
        Assert.Throws<UnauthorizedAccessException>(() => f.Core.Approvals.Approve(r.Id, kind));
        Assert.Throws<UnauthorizedAccessException>(() => f.Core.Approvals.Deny(r.Id, kind));
        await Assert.ThrowsAsync<ApprovalException>(() => deps.InstallApprovedAsync(f.Core.Approvals, r.Id, kind, gh));
        // selbst mit einer Freigabe einer erwachsenen Person installiert der Kern nicht im Namen des Kinderprofils
        var id = Frei(f, DependencyManager.InstallAction, gh.Id);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => deps.InstallApprovedAsync(f.Core.Approvals, id, kind, gh));
        Assert.Empty(runner.Calls);
        Assert.Throws<UnauthorizedAccessException>(() => deps.GiveConsent(kind));

        f.Core.Approvals.Withdraw(r.Id, kind);
        Assert.DoesNotContain(f.Core.Approvals.Pending(), x => x.Id == r.Id);
        Assert.Throws<ApprovalException>(() => f.Core.Approvals.Approve(r.Id, Principal.LocalOwner)); // zurückgezogen bleibt zurückgezogen
        var log = f.Core.Audit.ReadAll();
        Assert.Contains(log, a => a.Action == "rbac.denied" && a.Actor == "kinderprofil");
        Assert.Contains(log, a => a.Action == "approval.withdrawn" && a.Detail == r.Id);
        Assert.Throws<UnauthorizedAccessException>(() => f.Core.Approvals.Withdraw(r.Id, new Principal("gast", Role.Guest)));
        // fremde Anfragen kann ein Kinderprofil nicht zurückziehen
        var fremd = f.Core.Approvals.Request(Principal.LocalOwner, DependencyManager.InstallAction, "gh", gh.Id);
        Assert.Throws<UnauthorizedAccessException>(() => f.Core.Approvals.Withdraw(fremd.Id, kind));
        Assert.Contains(f.Core.Approvals.Pending(), x => x.Id == fremd.Id);
    }

    [Fact(DisplayName = "B4 Freigaben-Seite/Handy-API: fremde Wirkungen (Installieren, Ablauf …) werden nie als Startziel geöffnet – auch nicht mit Freigabe")]
    public void Integrationen_oeffnen_nur_eigene_Ziele()
    {
        using var f = new Fixture();
        Assert.True(Integrations.Handles(Integrations.OpenFolder));
        Assert.True(Integrations.Handles(Integrations.OpenUrl));
        Assert.False(Integrations.Handles(DependencyManager.InstallAction));
        var r = f.Core.Approvals.Request(Principal.LocalOwner, DependencyManager.InstallAction, "ffmpeg", "ffmpeg");
        f.Core.Approvals.Approve(r.Id, Principal.LocalOwner);
        Assert.Throws<ApprovalException>(() => f.Core.Integrations.Execute(r));
        Assert.Empty(f.Launcher.Opened);
        // die Freigabe bleibt für ihre eigene Seite gültig (nicht verbraucht)
        Assert.True(f.Core.Approvals.Execute(r.Id, DependencyManager.InstallAction, "ffmpeg", () => true));
    }
}
