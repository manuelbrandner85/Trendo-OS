using System.Text;
using System.Text.Json.Nodes;
using Trendo.Core;
using Xunit;
using static Trendo.Core.Tests.AgentRunTests;

namespace Trendo.Core.Tests;

/// <summary>T-0741 (CORE-03 „KI-Team“): Fortschritt je Rolle, Stopp mit sichtbarem und speicherbarem Zwischenstand, Fortsetzen nach Stopp (auch nach Neustart),
/// Werkzeuge je Rolle, „im Internet nachlesen“ nur nach Freigabe.
/// ERSETZT: das KI-Modell (<see cref="ScriptKi"/>) und der Internet-Abruf (Funktion statt Netz). Echt sind: Team-Schleife, Agenten-Lauf, Werkzeuge, Dateien im Projekt,
/// Freigabe-Tor, Prüfprotokoll, gespeicherter Lauf-Stand. Der Lauf mit echtem lokalem Modell steht in reports/T-0741/.</summary>
public class AgentTeamT0741Tests
{
    static string Call(string tool, object args, string thought = "weiter") => new JsonObject { ["gedanke"] = thought, ["werkzeug"] = tool, ["argumente"] = JsonNode.Parse(System.Text.Json.JsonSerializer.Serialize(args)) }.ToJsonString();
    static string Done(string text) => new JsonObject { ["gedanke"] = "fertig", ["fertig"] = text }.ToJsonString();
    static string RoleOf(IReadOnlyList<KiMessage> m) => m[0].Content is var s && s.Contains("„Planer“") ? "planer" : s.Contains("„Texter“") ? "texter" : s.Contains("„Lektor“") ? "lektor"
        : s.Contains("„Rechercheur“") ? "recherche" : "andere";

    [Fact(DisplayName = "T-0741 Team +1: Fortschritt je Rolle – wartet, arbeitet, fertig mit Zwischenergebnis; Stand liegt gespeichert vor")]
    public async Task Progress_per_role()
    {
        var f = new Fixture(); var seen = new List<string>();
        var fake = new ScriptKi((m, n) => RoleOf(m) switch
        {
            "planer" when m.Count == 2 => Call("datei.schreiben", new { pfad = "plan.md", inhalt = "1. Einladung schreiben" }, "Ich lege den Plan als Notiz ab."),
            "planer" => Done("Plan steht in plan.md."),
            "texter" => Done("Einladung: Sommerfest am Samstag."),
            _ => Done("Einladung verbessert."),
        });
        using (f)
        {
            var c = new AgentCenter(f.Root, f.Core.Audit, f.Core.Approvals, f.Core.Projects); var ki = new IntelligenceCore(f.Root, f.Core.Audit, () => [fake]);
            f.Core.Projects.Create("p1", "P1");
            var r = await c.RunTeamFilesAsync("text", "Einladung zum Sommerfest", ki, "p1",
                onProgress: s => { lock (seen) seen.Add(string.Join("|", s.Roles.Select(x => $"{x.RoleId}:{x.Status}"))); });
            Assert.Equal(AgentRunState.Fertig, r.State);
            // während der Planer arbeitet, warten Texter und Lektor
            Assert.Contains("planer:Arbeitet|texter:Wartet|lektor:Wartet", seen);
            Assert.Contains("planer:Fertig|texter:Arbeitet|lektor:Wartet", seen);
            Assert.Contains("planer:Fertig|texter:Fertig|lektor:Fertig", seen);
            var st = c.TeamRun(r.RunId)!;
            Assert.All(st.Roles, x => { Assert.Equal(TeamRoleStatus.Fertig, x.Status); Assert.NotNull(x.File); Assert.True(File.Exists(f.Core.Projects.ResolveInProject("p1", x.File!))); });
            Assert.Equal("Plan steht in plan.md.", st.Roles[0].Note);
            Assert.Equal(["lesen"], st.Roles[0].Tools); // Planer darf nicht schreiben – die Notiz wurde abgewiesen
            Assert.Equal(["lesen", "schreiben"], st.Roles[1].Tools);
            Assert.False(File.Exists(Path.Combine(f.Core.Projects.ProjectFilesRoot("p1"), "plan.md")));
            Assert.False(c.CanResumeTeam(r.RunId)); // fertig → nichts fortzusetzen
        }
    }

    [Fact(DisplayName = "T-0741 Team +2: Stopp mitten in der zweiten Rolle – Erarbeitetes bleibt sichtbar und speicherbar; nach Neustart an der offenen Rolle fortsetzen")]
    public async Task Stop_snapshot_resume_after_restart()
    {
        var f = new Fixture(); using var cts = new CancellationTokenSource(); var resumed = false; var calls = new List<string>();
        string Reply(IReadOnlyList<KiMessage> m, int n)
        {
            var role = RoleOf(m); lock (calls) calls.Add((resumed ? "nach:" : "vor:") + role);
            switch (role)
            {
                case "planer": return Done("1. Begrüßung 2. Programm 3. Anmeldung");
                case "texter" when m.Count == 2: return Call("datei.schreiben", new { pfad = "entwurf.md", inhalt = "Liebe Nachbarn, am Samstag feiern wir." }, "Ich schreibe den ersten Entwurf.");
                case "texter" when !resumed: cts.Cancel(); return Done("wird nie gelesen"); // Person drückt „Stopp“, während der Texter arbeitet
                case "texter": return Done("Einladung fertig in entwurf.md.");
                default: return m.Any(x => x.Content.Contains("01-planer.md") && x.Content.Contains("-texter.md")) ? Done("Einladung geprüft und verbessert.") : Done("FEHLT: Übergabe");
            }
        }
        var fake = new ScriptKi(Reply);
        using (f)
        {
            var c = new AgentCenter(f.Root, f.Core.Audit, f.Core.Approvals, f.Core.Projects); var ki = new IntelligenceCore(f.Root, f.Core.Audit, () => [fake]);
            f.Core.Projects.Create("p1", "P1");
            var r = await c.RunTeamFilesAsync("text", "Einladung zum Sommerfest", ki, "p1", ct: cts.Token);
            Assert.Equal(AgentRunState.Gestoppt, r.State);
            var st = c.TeamRun(r.RunId)!;
            Assert.Equal([TeamRoleStatus.Fertig, TeamRoleStatus.Gestoppt, TeamRoleStatus.Wartet], st.Roles.Select(x => x.Status).ToArray());
            Assert.Equal("Liebe Nachbarn, am Samstag feiern wir.", f.Core.Projects.ReadFile("p1", "entwurf.md")); // Datei des Texters bleibt
            var texterFile = st.Roles[1].File!;
            var partial = f.Core.Projects.ReadFile("p1", texterFile);
            Assert.Contains("Bisher erarbeitet", partial); Assert.Contains("entwurf.md", partial);
            Assert.True(c.CanResumeTeam(r.RunId));
            var snap = c.SaveTeamSnapshot(r.RunId);
            var md = f.Core.Projects.ReadFile("p1", snap);
            Assert.Contains("Planer – fertig", md); Assert.Contains("Texter – gestoppt", md); Assert.Contains("Lektor – wartet", md); Assert.Contains("1. Begrüßung", md);

            // Neustart: neues AgentCenter liest den gespeicherten Stand
            var c2 = new AgentCenter(f.Root, f.Core.Audit, f.Core.Approvals, f.Core.Projects);
            Assert.True(c2.CanResumeTeam(r.RunId));
            resumed = true;
            var r2 = await c2.ResumeTeamFilesAsync(r.RunId, ki);
            Assert.Equal(AgentRunState.Fertig, r2.State);
            Assert.DoesNotContain("nach:planer", calls); // der fertige Planer läuft nicht noch einmal
            Assert.Contains("nach:texter", calls); Assert.Contains("nach:lektor", calls);
            var st2 = c2.TeamRun(r.RunId)!;
            Assert.All(st2.Roles, x => Assert.Equal(TeamRoleStatus.Fertig, x.Status));
            Assert.Equal(2, st2.Roles[1].Rounds);
            Assert.Equal("Einladung geprüft und verbessert.", st2.Roles[2].Result);
            Assert.Contains(f.Core.Audit.ReadAll(), e => e.Action == "agent.team.fortsetzen" && e.Detail.StartsWith(r.RunId));
            Assert.False(c2.CanResumeTeam(r.RunId));
        }
    }

    [Fact(DisplayName = "T-0741 Team +3: Rechercheur liest im Internet nur nach Freigabe – „Nein“ blockiert ohne Abruf, „Ja“ ruft genau einmal ab")]
    public async Task Researcher_web_only_after_approval()
    {
        foreach (var yes in new[] { false, true })
        {
            var f = new Fixture(); var fetched = 0;
            var fake = new ScriptKi((m, n) => RoleOf(m) == "recherche" && m.Count == 2 ? Call("web.lesen", new { adresse = "https://example.org/sommerfest" }, "Ich lese nach.")
                : Done(RoleOf(m) == "recherche" ? "Fakten: " + (m[^1].Content.Contains("Blockiert") ? "keine (nicht freigegeben)" : "Sommerfest am 12. Juli") : "ok"));
            using (f)
            {
                var c = new AgentCenter(f.Root, f.Core.Audit, f.Core.Approvals, f.Core.Projects); var ki = new IntelligenceCore(f.Root, f.Core.Audit, () => [fake]);
                f.Core.Projects.Create("p1", "P1");
                ApprovalRequest? asked = null;
                var svc = new AgentServices
                {
                    ReadWeb = (u, ct) => { Interlocked.Increment(ref fetched); return Task.FromResult(("text/html", Encoding.UTF8.GetBytes("<html><head><title>Sommerfest</title></head><body><p>Sommerfest am 12. Juli</p></body></html>"), u)); },
                    AskApproval = (req, ct) => { asked = req; if (yes) f.Core.Approvals.Approve(req.Id, Principal.LocalOwner); return Task.FromResult(yes); },
                };
                var r = await c.RunTeamFilesAsync("projekt", "Sommerfest planen", ki, "p1", svc);
                Assert.Equal(AgentRunState.Fertig, r.State);
                Assert.NotNull(asked); Assert.Equal(AgentCenter.TeamWebAction, asked!.Action); Assert.Contains("Rechercheur", asked.Description);
                var st = c.TeamRun(r.RunId)!;
                Assert.Equal(["lesen", "web"], st.Roles[0].Tools);
                Assert.DoesNotContain("web", st.Roles[1].Tools); // nur die Recherche-Rolle darf ins Internet
                var log = c.Runs().First(x => x.AgentName == "Rechercheur").Log;
                if (yes)
                {
                    Assert.Equal(1, fetched);
                    Assert.Contains(log, e => e.Kind == "ergebnis" && e.Text.Contains("Sommerfest am 12. Juli"));
                    Assert.Contains(f.Core.Audit.ReadAll(), e => e.Action == "execution.done");
                    Assert.Contains("12. Juli", st.Roles[0].Result);
                }
                else
                {
                    Assert.Equal(0, fetched); // nichts abgerufen
                    Assert.Contains(log, e => e.Kind == "blockiert" && e.Text.Contains("nichts aus dem Internet"));
                    Assert.Contains(f.Core.Audit.ReadAll(), e => e.Action == "execution.blocked");
                }
            }
        }
    }

    [Fact(DisplayName = "T-0741 Team −1: ohne Person blockiert die Websuche; ein unterbrochener Lauf (Trendo beendet) gilt als gestoppt und lässt sich fortsetzen")]
    public async Task No_person_blocks_web_and_interrupted_run_is_resumable()
    {
        var f = new Fixture(); var fetched = 0;
        var fake = new ScriptKi((m, n) => RoleOf(m) == "recherche" && m.Count == 2 ? Call("web.lesen", new { adresse = "https://example.org/" }) : Done("ok"));
        using (f)
        {
            var c = new AgentCenter(f.Root, f.Core.Audit, f.Core.Approvals, f.Core.Projects); var ki = new IntelligenceCore(f.Root, f.Core.Audit, () => [fake]);
            f.Core.Projects.Create("p1", "P1");
            var svc = new AgentServices { ReadWeb = (u, ct) => { fetched++; return Task.FromResult(("text/html", Encoding.UTF8.GetBytes("x"), u)); } }; // keine AskApproval = niemand da
            var r = await c.RunTeamFilesAsync("projekt", "Sommerfest planen", ki, "p1", svc);
            Assert.Equal(0, fetched);
            Assert.Contains(c.Runs().First(x => x.AgentName == "Rechercheur").Log, e => e.Kind == "blockiert" && e.Text.Contains("Niemand konnte zustimmen"));
            // Lauf-Datei so, als wäre Trendo mitten im Lauf beendet worden
            var file = Path.Combine(f.Root, "ki", "team-laeufe", r.RunId + ".json");
            var json = JsonNode.Parse(File.ReadAllText(file))!.AsObject();
            json["State"] = (int)AgentRunState.Laeuft; json["Stage"] = 2; json["Roles"]![2]!["Status"] = (int)TeamRoleStatus.Arbeitet;
            File.WriteAllText(file, json.ToJsonString());
            var c2 = new AgentCenter(f.Root, f.Core.Audit, f.Core.Approvals, f.Core.Projects);
            var st = c2.TeamRun(r.RunId)!;
            Assert.Equal(AgentRunState.Gestoppt, st.State);
            Assert.Equal(TeamRoleStatus.Gestoppt, st.Roles[2].Status); Assert.StartsWith("Unterbrochen", st.Roles[2].Note);
            Assert.True(c2.CanResumeTeam(r.RunId));
            await Assert.ThrowsAsync<InvalidOperationException>(() => c2.ResumeTeamFilesAsync("gibt-es-nicht", ki));
        }
    }
}
