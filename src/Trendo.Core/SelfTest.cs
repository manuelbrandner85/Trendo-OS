namespace Trendo.Core;

/// <summary>Kommandozeilen-Selbsttest: öffnet einen Projektordner ECHT im Explorer, aber nur über das Approval-Gate mit protokollierter Operator-Freigabe.</summary>
public static class SelfTest
{
    public static int OpenExplorer(TrendoCore core, string approver, TextWriter output)
    {
        const string id = "selftest-explorer";
        if (!core.Projects.List().Any(p => p.Id == id)) core.Projects.Create(id, "Selbsttest Explorer");
        var r = core.Integrations.PlanOpenProjectFolder(id);
        output.WriteLine($"Freigabe angefordert: {r.Id} {r.Action}");
        core.Approvals.Approve(r.Id, approver);
        core.Integrations.Execute(r);
        output.WriteLine("Explorer geöffnet.");
        core.Shutdown();
        output.WriteLine($"Audit-Kette intakt: {core.Audit.Verify()}");
        return core.Audit.Verify() ? 0 : 2;
    }
}
