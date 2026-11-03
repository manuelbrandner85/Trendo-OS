using Trendo.Core;
using Xunit;

namespace Trendo.Core.Tests;

/// Wunsch 03.11.2026: jedes Studio hat ein Profi-Team; Rollen gehören fest zu einem Studio; das Team geht als Claude-Code-Agenten in den Projektordner.
public class StudioTeamsTests
{
    [Fact(DisplayName = "Profi-Teams: eindeutige Rollen mit Studio-Präfix, Auftrag, Schwerpunkt und festen Grenzen; SPEC-07 (abgelehnt) bekommt kein Team")]
    public void Teams_complete()
    {
        Assert.True(StudioTeams.All.Count >= 55);
        Assert.Null(StudioTeams.For("SPEC-07"));
        var ids = StudioTeams.All.SelectMany(t => t.Roles).Select(r => r.Id).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());
        foreach (var t in StudioTeams.All)
        {
            Assert.NotEmpty(t.Roles); Assert.EndsWith("Team", t.Name);
            foreach (var r in t.Roles)
            {
                Assert.StartsWith(t.StudioId.ToLowerInvariant() + ".", r.Id);
                Assert.False(string.IsNullOrWhiteSpace(r.Does)); Assert.False(string.IsNullOrWhiteSpace(r.Focus));
                Assert.Equal(t.StudioId, StudioTeams.StudioOf(r.Id));
            }
        }
        var p = StudioTeams.Prompt(StudioTeams.For("CORE-05")!.Roles[0], "3D-Modelle erstellen");
        Assert.Contains("„3D-Modelle erstellen“", p); Assert.Contains("nur nach ausdrücklichem Ja", p); Assert.Contains("Erfinde nichts", p);
    }

    [Fact(DisplayName = "KI-Team: Studio-Rollen tragen ihr Studio, Studio-Team als Team startbar, eigene Rolle nur mit Studio, erscheint beim Studio, entfernbar")]
    public void Agent_center_studio_roles()
    {
        using var f = new Fixture();
        AgentCenter.StudioName = id => id == "CORE-05" ? "3D-Modelle erstellen" : id == "SPEC-11" ? "Geld & Rechnungen" : null;
        var c = new AgentCenter(f.Root, f.Core.Audit, f.Core.Approvals, f.Core.Projects);
        var role = AgentCenter.Role("core-05.modellierer");
        Assert.Equal("CORE-05", role.Studio); Assert.Contains("„3D-Modelle erstellen“", role.SystemPrompt); Assert.True(role.MayWriteFiles);
        Assert.Null(AgentCenter.Role("planer").Studio); // allgemeine Rollen bleiben
        var team = AgentCenter.Team(AgentCenter.TeamId("CORE-05"))!.Value;
        Assert.Equal("3D-Team", team.Name); Assert.Equal(4, team.Roles.Length);
        Assert.Null(AgentCenter.Team("studio:GIBT-ES-NICHT"));
        Assert.Throws<InvalidOperationException>(() => AgentCenter.Role("gibt.es.nicht"));

        Assert.Throws<ArgumentException>(() => c.AddCustomRole("Steuer-Vorprüfer", "prüft Belege", "", "", false, AgentCenter.StudioName));
        Assert.Throws<ArgumentException>(() => c.AddCustomRole("", "prüft Belege", "", "SPEC-11", false, AgentCenter.StudioName));
        var mine = c.AddCustomRole("Steuer-Vorprüfer", "prüft Belege auf Vollständigkeit", "Fristen", "SPEC-11", false, AgentCenter.StudioName);
        Assert.Equal("SPEC-11", mine.Studio); Assert.StartsWith("eigen.spec-11.", mine.Id); Assert.Contains("„Geld & Rechnungen“", mine.SystemPrompt);
        Assert.Contains(c.RolesFor("SPEC-11"), r => r.Id == mine.Id); Assert.DoesNotContain(c.RolesFor("CORE-05"), r => r.Id == mine.Id);
        Assert.Equal(mine.Name, c.RoleOf(mine.Id).Name);
        var t = c.Create(mine.Id, "Belege März prüfen"); Assert.Equal(mine.Id, t.RoleId);
        Assert.True(c.RemoveCustomRole(mine.Id)); Assert.Empty(c.CustomRoles());
        Assert.Contains(f.Core.Audit.ReadAll(), a => a.Action == "agent.rolle.neu");
    }

    [Fact(DisplayName = "Claude-Code-Agenten: Team landet als .claude/agents/trendo-*.md (name, description), veraltete Trendo-Agenten weg, eigene Agenten der Person bleiben")]
    public void Export_agents()
    {
        var dir = Path.Combine(Path.GetTempPath(), "trendo-agents-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir);
        try
        {
            AgentCenter.StudioName = _ => "3D-Modelle erstellen";
            var roles = StudioTeams.For("CORE-05")!.Roles.Select(r => AgentCenter.StudioRole(r.Id)!).ToList();
            var agents = Path.Combine(dir, ".claude", "agents"); Directory.CreateDirectory(agents);
            File.WriteAllText(Path.Combine(agents, "trendo-alt.md"), "alt"); File.WriteAllText(Path.Combine(agents, "mein-agent.md"), "von der Person");
            Assert.Equal(4, StudioAgentsExport.Write(dir, roles, "3D-Modelle erstellen"));
            var file = Path.Combine(agents, "trendo-core-05-modellierer.md");
            var text = File.ReadAllText(file);
            Assert.StartsWith("---\nname: trendo-core-05-modellierer\ndescription: 3D-Modellierer im Studio „3D-Modelle erstellen“", text);
            Assert.Contains("Antworte auf Deutsch", text);
            Assert.False(File.Exists(Path.Combine(agents, "trendo-alt.md"))); Assert.True(File.Exists(Path.Combine(agents, "mein-agent.md")));
            Assert.Equal("trendo-eigen-spec-11-steuer-vorprufer", StudioAgentsExport.AgentName("eigen.spec-11.steuer-vorprüfer"));
        }
        finally { Directory.Delete(dir, true); }
    }
}
