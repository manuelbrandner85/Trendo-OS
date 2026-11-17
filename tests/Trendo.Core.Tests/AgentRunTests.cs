using System.Text.Json.Nodes;
using Trendo.Core;
using Trendo.Core.PromptAction;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>T-0698: Agenten laufen wirklich (STUDIO-AGENTS-01, CORE-03, SYS-05).
/// ERSETZT: In diesen Tests ist das KI-Modell durch <see cref="ScriptKi"/> ersetzt, das vorgegebene Werkzeugaufrufe als JSON zurückgibt.
/// Echt sind: Lauf-Schleife, Werkzeuge, Dateien im Projekt, Freigabe-Tor, Stopp, Abläufe-Maschine. Der echte Modelllauf steht in reports/T-0698/.</summary>
public class AgentRunTests
{
    /// <summary>ERSETZTES Modell: antwortet je Aufruf mit dem nächsten Werkzeugaufruf aus dem Skript (oder einer Funktion über die Nachrichten).</summary>
    public sealed class ScriptKi(Func<IReadOnlyList<KiMessage>, int, string> reply, int delayMs = 0) : IKiBackend
    {
        int _n, _now; public int MaxParallel; public List<IReadOnlyList<KiMessage>> Calls { get; } = [];
        public Func<int, CancellationToken, Task>? Before { get; set; }
        public string Name => "Ersatz-Modell (Test)";
        public string BaseUrl => "http://127.0.0.1:1";
        public Task<bool> IsUpAsync(CancellationToken ct = default) => Task.FromResult(true);
        public Task<IReadOnlyList<string>> ModelsAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<string>>(["ersatz"]);
        public async Task<string> ChatAsync(string model, IReadOnlyList<KiMessage> m, CancellationToken ct = default)
        {
            var n = Interlocked.Increment(ref _n);
            var now = Interlocked.Increment(ref _now); lock (Calls) { Calls.Add(m); MaxParallel = Math.Max(MaxParallel, now); }
            try
            {
                if (Before is not null) await Before(n, ct);
                if (delayMs > 0) await Task.Delay(delayMs, ct);
                return reply(m, n);
            }
            finally { Interlocked.Decrement(ref _now); }
        }
    }

    static string Call(string tool, object args, string thought = "weiter") => new JsonObject { ["gedanke"] = thought, ["werkzeug"] = tool, ["argumente"] = JsonNode.Parse(System.Text.Json.JsonSerializer.Serialize(args)) }.ToJsonString();
    static string Done(string text) => new JsonObject { ["gedanke"] = "fertig", ["fertig"] = text }.ToJsonString();

    static (AgentCenter C, IntelligenceCore Ki, ScriptKi Fake, Fixture F) Setup(Func<IReadOnlyList<KiMessage>, int, string> reply, int delay = 0)
    {
        var f = new Fixture(); var fake = new ScriptKi(reply, delay);
        return (new AgentCenter(f.Root, f.Core.Audit, f.Core.Approvals, f.Core.Projects), new IntelligenceCore(f.Root, f.Core.Audit, () => [fake]), fake, f);
    }

    static AgentDefinition Def(AgentCenter c, params string[] tools) => c.SaveDefinition(new AgentDefinition { Name = "Notiz-Helfer", Goal = "Lege eine Einkaufsliste an und verbessere sie.", Tools = [.. tools], MaxSteps = 10, MaxMinutes = 2 });

    static string LastToolResult(IReadOnlyList<KiMessage> m) => m[^1].Content;

    [Fact(DisplayName = "T-0698 Agent +1: legt eine echte Datei an, liest sie, verbessert sie – jede Werkzeugnutzung im Protokoll, Ergebnis als Datei im Projekt")]
    public async Task Agent_creates_reads_improves_file()
    {
        string? readBack = null;
        var (c, ki, fake, f) = Setup((m, n) => n switch
        {
            1 => Call("datei.schreiben", new { pfad = "einkauf.md", inhalt = "- Milch\n- Brot" }, "Ich lege die Liste an."),
            2 => Call("datei.lesen", new { pfad = "einkauf.md" }, "Ich lese sie zur Kontrolle."),
            3 => (readBack = LastToolResult(m)) is { } ? Call("datei.schreiben", new { pfad = "einkauf.md", inhalt = "# Einkauf\n\n- Milch (1 l)\n- Brot\n- Äpfel" }, "Ich verbessere sie.") : "",
            _ => Done("Einkaufsliste angelegt und verbessert."),
        });
        using (f)
        {
            var def = Def(c, "lesen", "schreiben");
            Assert.Equal("agent-notiz-helfer", def.ProjectId);
            var run = await c.RunAgentAsync(def, ki);
            Assert.Equal(AgentRunState.Fertig, run.State);
            Assert.Equal(4, run.Steps);
            Assert.Contains("- Milch\n- Brot", readBack); // Lesen lieferte den echten Dateiinhalt an das Modell zurück
            var file = f.Core.Projects.ResolveInProject(def.ProjectId!, "einkauf.md");
            Assert.Equal("# Einkauf\n\n- Milch (1 l)\n- Brot\n- Äpfel", File.ReadAllText(file));
            Assert.Equal(3, run.Log.Count(e => e.Kind == "werkzeug"));
            Assert.Contains(run.Log, e => e.Kind == "ergebnis" && e.Text.StartsWith("Ersetzt: einkauf.md"));
            Assert.NotNull(run.ResultFile);
            var md = f.Core.Projects.ReadFile(def.ProjectId!, run.ResultFile!);
            Assert.Contains("Einkaufsliste angelegt und verbessert.", md); Assert.Contains("datei.lesen", md);
            Assert.Equal(run.Id, c.Runs(def.Id).Single().Id); // Lauf ist gespeichert
            Assert.Contains("datei.schreiben", fake.Calls[0][0].Content); // Werkzeugliste steht im Systemtext
            Assert.DoesNotContain("web.lesen", fake.Calls[0][0].Content);
        }
    }

    [Fact(DisplayName = "T-0698 Agent −1: Agent nur mit „lesen“ kann nicht schreiben – Versuch abgewiesen und protokolliert")]
    public async Task Read_only_agent_cannot_write()
    {
        var (c, ki, _, f) = Setup((m, n) => n == 1 ? Call("datei.schreiben", new { pfad = "x.md", inhalt = "boese" }) : Done("ok"));
        using (f)
        {
            var def = Def(c, "lesen");
            var run = await c.RunAgentAsync(def, ki);
            Assert.False(File.Exists(f.Core.Projects.ResolveInProject(def.ProjectId!, "x.md")));
            Assert.Contains(run.Log, e => e.Kind == "blockiert" && e.Text.Contains("nicht erlaubt"));
            Assert.Contains(f.Core.Audit.ReadAll(), a => a.Action == "agent.werkzeug.verweigert");
        }
    }

    [Fact(DisplayName = "T-0698 Agent −2: Ausbruch aus dem Projektordner wird abgewiesen")]
    public async Task Project_boundary()
    {
        var (c, ki, _, f) = Setup((m, n) => n switch { 1 => Call("datei.schreiben", new { pfad = "../../ausbruch.txt", inhalt = "x" }), 2 => Call("datei.lesen", new { pfad = "C:/Windows/win.ini" }), _ => Done("ok") });
        using (f)
        {
            var def = Def(c, "lesen", "schreiben");
            var run = await c.RunAgentAsync(def, ki);
            Assert.Equal(2, run.Log.Count(e => e.Kind == "blockiert" && e.Text.Contains("außerhalb des Projekts")));
            Assert.False(File.Exists(Path.Combine(f.Root, "projects", "ausbruch.txt")));
        }
    }

    [Fact(DisplayName = "T-0698 Freigabe −1: Wirkung nach außen ohne Zustimmung wird vom Freigabe-Tor blockiert, mit Zustimmung ausgeführt")]
    public async Task Gate_blocks_without_consent()
    {
        var outDir = Path.Combine(Path.GetTempPath(), "trendo-t0698-out-" + Guid.NewGuid().ToString("N")[..6]);
        var (c, ki, _, f) = Setup((m, n) => (n % 3) switch { 1 => Call("datei.schreiben", new { pfad = "bericht.md", inhalt = "Bericht" }), 2 => Call("datei.exportieren", new { pfad = "bericht.md", ziel = outDir }), _ => Done("fertig") });
        using (f)
            try
            {
                var def = Def(c, "lesen", "schreiben", "export");
                // 1) Niemand stimmt zu
                var svcNo = new AgentServices { ExportAllowed = d => d.StartsWith(Path.GetTempPath(), StringComparison.OrdinalIgnoreCase), AskApproval = (_, _) => Task.FromResult(false) };
                var run = await c.RunAgentAsync(def, ki, svcNo);
                Assert.False(File.Exists(Path.Combine(outDir, "bericht.md")));
                Assert.Contains(run.Log, e => e.Kind == "freigabe");
                Assert.Contains(run.Log, e => e.Kind == "blockiert" && e.Text.Contains("nicht zugestimmt"));
                Assert.Contains(f.Core.Audit.ReadAll(), a => a.Action == "execution.blocked" && a.Detail.Contains("Pending"));
                // 2) Lauf ohne Person (z. B. aus einem Ablauf): ebenfalls blockiert
                var run2 = await c.RunAgentAsync(def, ki, new AgentServices { ExportAllowed = _ => true });
                Assert.Contains(run2.Log, e => e.Kind == "blockiert" && e.Text.Contains("Niemand konnte zustimmen"));
                Assert.False(File.Exists(Path.Combine(outDir, "bericht.md")));
                // 3) Person stimmt zu → über das Tor ausgeführt
                var svcYes = new AgentServices { ExportAllowed = _ => true, AskApproval = (req, _) => { f.Core.Approvals.Approve(req.Id, Principal.LocalOwner); return Task.FromResult(true); } };
                var run3 = await c.RunAgentAsync(def, ki, svcYes);
                Assert.Equal("Bericht", File.ReadAllText(Path.Combine(outDir, "bericht.md")));
                Assert.Contains(f.Core.Audit.ReadAll(), a => a.Action == "execution.done");
                Assert.Equal(AgentRunState.Fertig, run3.State);
            }
            finally { try { Directory.Delete(outDir, true); } catch (DirectoryNotFoundException) { } }
    }

    [Fact(DisplayName = "T-0698 Stopp: Stopp-Knopf (Abbruch) beendet den Lauf sofort, Zustand „gestoppt“, Ergebnisdatei bleibt")]
    public async Task Stop_ends_run()
    {
        using var cts = new CancellationTokenSource();
        var (c, ki, fake, f) = Setup((m, n) => Call("datei.schreiben", new { pfad = $"teil{n}.md", inhalt = "x" }));
        fake.Before = async (n, ct) => { if (n == 3) { cts.Cancel(); await Task.Delay(5000, ct); } };
        using (f)
        {
            var def = Def(c, "lesen", "schreiben");
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var run = await c.RunAgentAsync(def, ki, null, null, cts.Token);
            Assert.True(sw.Elapsed < TimeSpan.FromSeconds(4));
            Assert.Equal(AgentRunState.Gestoppt, run.State);
            Assert.Equal(2, run.FilesWritten.Count); // Schritte vor dem Stopp bleiben
            Assert.Contains(run.Log, e => e.Kind == "stopp");
            Assert.True(File.Exists(f.Core.Projects.ResolveInProject(def.ProjectId!, run.ResultFile!)));
        }
    }

    [Fact(DisplayName = "T-0698 Grenze: Schrittgrenze beendet einen Agenten, der nie fertig wird; Not-Stopp hält ihn an")]
    public async Task Step_limit_and_halt()
    {
        var (c, ki, _, f) = Setup((m, n) => Call("dateien.liste", new { }));
        using (f)
        {
            var def = Def(c, "lesen"); def.MaxSteps = 3; c.SaveDefinition(def);
            var run = await c.RunAgentAsync(def, ki);
            Assert.Equal(AgentRunState.Grenze, run.State); Assert.Equal(3, run.Steps);
            f.Core.Approvals.Halt(Principal.LocalOwner, "Test");
            var halted = await c.RunAgentAsync(def, ki);
            Assert.Equal(AgentRunState.Gestoppt, halted.State); Assert.Contains(halted.Log, e => e.Text.StartsWith("Not-Stopp"));
        }
    }

    [Fact(DisplayName = "T-0698 Werkzeuge: E-Mail nur als Entwurf (.eml, nie gesendet), Web nur im Lesemodus – Heimnetz abgewiesen")]
    public async Task Mail_draft_and_web()
    {
        var (c, ki, _, f) = Setup((m, n) => n switch
        {
            1 => Call("web.lesen", new { adresse = "https://example.org/info" }),
            2 => Call("web.lesen", new { adresse = "http://192.168.0.1/admin" }),
            3 => Call("email.entwurf", new { an = "kontakt@example.org", betreff = "Frage zur Info", text = "Hallo,\nkurze Frage." }),
            _ => Done("ok"),
        });
        using (f)
        {
            var def = Def(c, "web", "mail");
            var read = new List<Uri>();
            var svc = new AgentServices { ReadWeb = (u, _) => { read.Add(u); return Task.FromResult(("text/html", System.Text.Encoding.UTF8.GetBytes("<html><head><title>Info</title></head><body><p>Öffnungszeiten 9 bis 17 Uhr.</p></body></html>"), u)); } };
            var run = await c.RunAgentAsync(def, ki, svc);
            Assert.Single(read); // nur die öffentliche Seite wurde gelesen
            Assert.Contains(run.Log, e => e.Kind == "ergebnis" && e.Text.Contains("Öffnungszeiten"));
            Assert.Contains(run.Log, e => e.Kind == "blockiert" && e.Text.StartsWith("Abgewiesen"));
            var eml = Assert.Single(Directory.GetFiles(Path.Combine(f.Core.Projects.ProjectFilesRoot(def.ProjectId!), "entwuerfe"), "*.eml"));
            var text = File.ReadAllText(eml);
            Assert.StartsWith("X-Unsent: 1", text); Assert.Contains("To: kontakt@example.org", text);
            Assert.Contains(run.Log, e => e.Text.Contains("NICHT gesendet"));
        }
    }

    [Fact(DisplayName = "T-0698 Werkzeug Studio: Studio-Aktion über den vorhandenen Studio-Ablauf (echter Adapter SPEC-11) legt eine Rechnung im Projekt an, bucht nichts")]
    public async Task Studio_action_via_workflow()
    {
        const string invoice = "Verkaeufer: Anna Beispiel\nKaeufer: Bruno Beispiel\nPosition: Beratung | 2 | 10\nMwSt: 19\nDatum: 2026-10-22";
        var (c, ki, _, f) = Setup((m, n) => n == 1 ? Call("studio.aktion", new { studio = "SPEC-11", aktion = "rechnung.schreiben", auftrag = invoice }) : Done("Rechnung liegt im Projekt."));
        using (f)
        {
            var def = Def(c, "studio");
            var run = await c.RunAgentAsync(def, ki, new AgentServices { Studio = new StudioWorkflow(f.Core, PromptActions.CreateDefault()) });
            Assert.Equal(AgentRunState.Fertig, run.State);
            var html = Assert.Single(run.FilesWritten, x => x.EndsWith(".html"));
            Assert.Contains("Bruno Beispiel", File.ReadAllText(f.Core.Projects.ResolveInProject(def.ProjectId!, html)));
            Assert.Contains(run.Log, e => e.Kind == "ergebnis" && e.Text.StartsWith("Studio-Aktion: Verified"));
        }
    }

    [Fact(DisplayName = "T-0698 Ablauf +1: Schritt „Agent ausführen“ – Neue-Datei-Auslöser startet den Agenten über die Abläufe-Maschine, nur nach Freigabe")]
    public async Task Workflow_step_runs_agent_on_new_file()
    {
        var (c, ki, _, f) = Setup((m, n) => n switch
        {
            1 => Call("datei.lesen", new { pfad = "eingang/rechnung.txt" }),
            2 => Call("datei.schreiben", new { pfad = "zusammenfassung.md", inhalt = "Betrag: " + (m[^1].Content.Contains("42,00") ? "42,00 EUR" : "?") }),
            _ => Done("Rechnung zusammengefasst."),
        });
        using (f)
        {
            var def = Def(c, "lesen", "schreiben");
            var watch = Path.Combine(f.Root, "home", "Eingang"); Directory.CreateDirectory(watch);
            var wf = new WorkflowEngine(f.Root, f.Core.Audit, f.Core.Approvals, [Path.Combine(f.Root, "home")])
            {
                RunAgent = (id, file, ct) => c.RunForWorkflowAsync(id, file, ki, null, ct),
                AgentName = id => c.Definition(id)?.Name,
            };
            var w = wf.Add(new Workflow { Name = "Rechnungen", Trigger = TriggerKind.NeueDatei, WatchFolder = watch, WatchPattern = "*.txt", Steps = [new() { Kind = StepKind.AgentAusfuehren, Text = def.Id }] });
            Assert.True(w.NeedsApproval); Assert.Equal("Agent ausführen", WorkflowEngine.StepLabel(StepKind.AgentAusfuehren));
            var now = DateTimeOffset.Now;
            Assert.Empty(wf.Due(now)); // erster Blick merkt sich den Ordner
            File.WriteAllText(Path.Combine(watch, "rechnung.txt"), "Rechnung Nr. 7, Betrag 42,00 EUR");
            var blocked = await wf.RunDueAsync(now.AddMinutes(1));
            Assert.False(Assert.Single(blocked).Ok); Assert.Contains("Freigabe", blocked[0].Lines[0]); // ohne Freigabe läuft kein Agent
            var req = wf.RequestApproval(w); f.Core.Approvals.Approve(req.Id, Principal.LocalOwner); wf.ApplyApproval(w, req.Id);
            var runs = await wf.RunDueAsync(now.AddMinutes(2)); // T-0681: fehlgeschlagener Lauf hält die Datei für den nächsten Takt fällig
            var r = Assert.Single(runs);
            Assert.True(r.Ok, string.Join("\n", r.Lines));
            Assert.Contains("Agent ausführen: Notiz-Helfer mit rechnung.txt", r.Lines[0]);
            Assert.Equal("Betrag: 42,00 EUR", f.Core.Projects.ReadFile(def.ProjectId!, "zusammenfassung.md"));
            Assert.Equal(AgentRunState.Fertig, c.Runs(def.Id).Single().State);
        }
    }

    [Fact(DisplayName = "T-0698 Ablauf +2: alte Ablaufdateien bleiben lesbar (Schritt-Arten als Zahl, neue Art hinten)")]
    public void Old_workflow_files_still_load()
    {
        Assert.Equal(6, (int)StepKind.KiZusammenfassen); Assert.Equal(7, (int)StepKind.AgentAusfuehren);
        using var f = new Fixture();
        var dir = Path.Combine(f.Root, "ablaeufe"); Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "ablaeufe.json"), "[{\"Id\":\"a1\",\"Name\":\"Alt\",\"Trigger\":0,\"Steps\":[{\"Kind\":6,\"From\":\"x\",\"To\":\"\",\"Pattern\":\"*\",\"Text\":\"\"}]}]");
        var wf = new WorkflowEngine(f.Root, f.Core.Audit, f.Core.Approvals);
        Assert.Null(wf.LoadProblem); Assert.Equal(StepKind.KiZusammenfassen, wf.All().Single().Steps[0].Kind);
    }

    [Fact(DisplayName = "T-0698 Team +1: Rollen geben Dateien weiter, Prüfer laufen parallel, Prüfer gibt einmal zurück, danach OK")]
    public async Task Team_files_review_loop_parallel()
    {
        var reviews = 0;
        string Reply(IReadOnlyList<KiMessage> m, int n)
        {
            var sys = m[0].Content; var last = m[^1].Content;
            var role = sys.Contains("„Planer“") ? "planer" : sys.Contains("„Programmierer“") ? "programmierer" : sys.Contains("„Prüfer“") ? "pruefer" : "sicherheit";
            var first = m.Count == 2; // nur System + erste Frage
            if (first) return role == "planer" ? Call("datei.schreiben", new { pfad = "plan.md", inhalt = "1. Funktion summe schreiben" })
                : Call("datei.lesen", new { pfad = ExtractFirst(last) });
            return role switch
            {
                "planer" => Done("Plan steht in plan.md."),
                "programmierer" when !m.Any(x => x.Content.Contains("summe.py")) => Call("datei.schreiben", new { pfad = "summe.py", inhalt = m.Any(x => x.Content.Contains("zurückgegeben")) ? "def summe(a, b):\n    return a + b\n" : "def summe(a, b): return a - b\n" }),
                "programmierer" => Done("summe.py geschrieben."),
                "pruefer" => Interlocked.Increment(ref reviews) == 1 ? Done("ZURÜCK: summe rechnet minus statt plus.") : Done("OK: summe passt."),
                _ => Done("OK: keine Geheimnisse im Code."),
            };
        }
        var (c, ki, fake, f) = Setup(Reply, delay: 150);
        using (f)
        {
            f.Core.Projects.Create("p1", "P1");
            var team = await c.RunTeamFilesAsync("code", "Funktion summe(a, b) bauen", ki, "p1");
            Assert.Equal(AgentRunState.Fertig, team.State);
            Assert.Equal(1, team.Returns);
            Assert.Equal(["pruefer", "sicherheit"], team.Stages[2]);
            Assert.True(fake.MaxParallel >= 2, "Prüfer und Sicherheitsprüfer sollten gleichzeitig arbeiten");
            // planer, programmierer, pruefer+sicherheit, programmierer (nach Rückgabe), pruefer+sicherheit
            Assert.Equal(["planer", "programmierer", "pruefer", "sicherheit", "programmierer", "pruefer", "sicherheit"], team.Tasks.Select(t => t.RoleId).ToArray());
            foreach (var h in team.HandoffFiles) Assert.True(File.Exists(f.Core.Projects.ResolveInProject("p1", h)), h);
            Assert.Equal("def summe(a, b):\n    return a + b\n", f.Core.Projects.ReadFile("p1", "summe.py")); // nach der Rückgabe verbessert
            Assert.Contains("ZURÜCK", f.Core.Projects.ReadFile("p1", team.HandoffFiles[2]));
            Assert.All(team.Tasks, t => Assert.Equal(AgentTaskState.Fertig, t.State));
            Assert.False(c.CanResume(team.Tasks[0].TeamId!)); // Datei-Team ist kein Text-Ketten-Lauf
        }

        static string ExtractFirst(string s) { var i = s.IndexOf("team/", StringComparison.Ordinal); if (i < 0) return "plan.md"; var e = s.IndexOfAny([',', '\n', ' '], i); return e < 0 ? s[i..] : s[i..e]; }
    }

    [Fact(DisplayName = "T-0698 Team +2: Prüfer kann höchstens zweimal zurückgeben; Stopp beendet das Team jederzeit")]
    public async Task Team_return_limit_and_stop()
    {
        var (c, ki, _, f) = Setup((m, n) => m[0].Content.Contains("„Lektor“") ? Done("verbessert") : m[0].Content.Contains("„Prüfer“") ? Done("ZURÜCK: noch nicht gut") : Done("Text"));
        using (f)
        {
            f.Core.Projects.Create("p1", "P1");
            var r = await c.RunTeamFilesAsync("code", "x", ki, "p1");
            Assert.Equal(AgentRunState.Grenze, r.State); Assert.Equal(AgentCenter.MaxTeamReturns, r.Returns);
            using var cts = new CancellationTokenSource(); cts.Cancel();
            var stopped = await c.RunTeamFilesAsync("text", "x", ki, "p1", null, cts.Token);
            Assert.Equal(AgentRunState.Gestoppt, stopped.State); Assert.Empty(stopped.Tasks);
        }
    }

    [Fact(DisplayName = "T-0698 Definition −1: Pflichtfelder, Werkzeuge nur aus der festen Liste, Grenzen, keine Schlüssel")]
    public void Definition_checks()
    {
        using var f = new Fixture();
        var c = new AgentCenter(f.Root, f.Core.Audit, f.Core.Approvals, f.Core.Projects);
        Assert.Throws<ArgumentException>(() => c.SaveDefinition(new AgentDefinition { Name = "", Goal = "x", Tools = ["lesen"] }));
        Assert.Throws<ArgumentException>(() => c.SaveDefinition(new AgentDefinition { Name = "A", Goal = "x", Tools = ["befehle"] })); // unbekannt → keine Werkzeuge übrig
        Assert.Throws<ArgumentException>(() => c.SaveDefinition(new AgentDefinition { Name = "A", Goal = "x", Tools = ["lesen"], MaxSteps = 99 }));
        Assert.Throws<ArgumentException>(() => c.SaveDefinition(new AgentDefinition { Name = "A", Goal = "api_key: abcd1234efgh", Tools = ["lesen"] }));
        var d = c.SaveDefinition(new AgentDefinition { Name = "Wochen Bericht", Goal = "Bericht", Tools = ["lesen", "befehle"] });
        Assert.Equal("wochen-bericht", d.Id); Assert.Equal(["lesen"], d.Tools);
        Assert.Single(c.Definitions()); Assert.True(c.RemoveDefinition(d.Id)); Assert.Empty(c.Definitions());
    }
}
