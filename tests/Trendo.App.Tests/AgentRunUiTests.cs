using System.Text.Json.Nodes;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Trendo.App;
using Trendo.Core;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// <summary>T-0698: „KI-Agenten bauen“ und „KI-Team“ – Agenten laufen wirklich, vom Klick bis zur echten Datei.
/// ERSETZT: das KI-Modell (<see cref="ToolKi"/> gibt vorgegebene Werkzeugaufrufe zurück). Echt: Oberfläche, Lauf-Schleife, Werkzeuge, Projektdateien, Freigabe-Tor, Stopp, Abläufe.</summary>
public class AgentRun_Ui
{
    /// <summary>„Start“ im Menüband: erst in den sichtbaren Bereich holen (wie ein Mensch hochscrollt) – nach einem Lauf steht die Seite unten bei der Freigabe-Karte.</summary>
    static void StartAgent(MainWindow w) { var b = w.Find<Button>("prog-cmd-agent-start"); b.BringIntoView(); Settle(); RealClick(w, w.Find<Button>("prog-cmd-agent-start")); }

    const string K = "agenten-bauen-programm";

    /// <summary>ERSETZTES Modell: liefert Werkzeugaufrufe als JSON.</summary>
    sealed class ToolKi(Func<IReadOnlyList<KiMessage>, int, string> reply) : IKiBackend
    {
        int _n;
        public Func<int, CancellationToken, Task>? Before { get; set; }
        public string Name => "Ersatz-Modell (Test)";
        public string BaseUrl => "http://127.0.0.1:1";
        public Task<bool> IsUpAsync(CancellationToken ct = default) => Task.FromResult(true);
        public Task<IReadOnlyList<string>> ModelsAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<string>>(["ersatz"]);
        public async Task<string> ChatAsync(string model, IReadOnlyList<KiMessage> m, CancellationToken ct = default)
        {
            var n = Interlocked.Increment(ref _n);
            if (Before is not null) await Before(n, ct);
            await Task.Delay(5, ct);
            return reply(m, n);
        }
    }

    static string Call(string tool, object args) => new JsonObject { ["gedanke"] = "weiter mit " + tool, ["werkzeug"] = tool, ["argumente"] = JsonNode.Parse(System.Text.Json.JsonSerializer.Serialize(args)) }.ToJsonString();
    static string Done(string text) => new JsonObject { ["gedanke"] = "fertig", ["fertig"] = text }.ToJsonString();
    static void Tap(MainWindow w, string name) { var b = w.Find<Button>(name); b.BringIntoView(); Settle(); RealClick(w, b); Settle(); }
    static void WaitFor(Func<bool> done, int ms = 8000) { var sw = System.Diagnostics.Stopwatch.StartNew(); while (!done() && sw.ElapsedMilliseconds < ms) { Thread.Sleep(10); Settle(); } Settle(); }

    static ProgramDocumentRef NewAgent(MainWindow w, string name, string goal, params string[] tools)
    {
        w.ProgramNew(K, "leer"); w.Show(K); Settle();
        var d = w.ProgramActive(K)!;
        d.Set("name", name, "Name"); d.Set("goal", goal, "Ziel"); d.Set("runtools", new JsonArray(tools.Select(t => (JsonNode)t).ToArray()), "Werkzeuge");
        w.Show(K); Settle();
        return new(d);
    }
    sealed record ProgramDocumentRef(Trendo.Core.Programs.ProgramDocument D);

    [AvaloniaFact(DisplayName = "T-0698 UI +1: Leerzustand zeigt den ersten Schritt; „Agent starten“ (gold) → Agent legt echte Datei an, liest und verbessert sie, Protokoll live, Agent in der Liste")]
    public void Start_creates_real_file()
    {
        var (w, _, _) = Ui.Open();
        try
        {
            w.Show(K); Settle(); RealClick(w, w.Find<Button>("prog-tpl-leer")); Settle();
            Assert.NotNull(w.TryFind<Border>("agent-first-step")); // erster Schritt im Leerzustand
            Assert.NotNull(w.TryFind<CheckBox>("agent-runtool-lesen")); Assert.NotNull(w.TryFind<ComboBox>("agent-model"));
            StartAgent(w); Settle();
            Assert.Contains("Kurznamen", w.ProgramMessage(K)); // ohne Name kein Lauf

            var ki1 = new ToolKi((m, n) => n switch
            {
                1 => Call("datei.schreiben", new { pfad = "aufgaben.md", inhalt = "- Fenster putzen" }),
                2 => Call("datei.lesen", new { pfad = "aufgaben.md" }),
                3 => Call("datei.schreiben", new { pfad = "aufgaben.md", inhalt = "# Aufgaben\n\n- [ ] Fenster putzen" }),
                _ => Done("Aufgabenliste steht."),
            });
            w.KiBackendsOverride = () => [ki1]; // einmal anlegen – die KI fragt die Liste bei jeder Frage neu ab
            var r = NewAgent(w, "aufgaben-helfer", "Lege eine Aufgabenliste an.", "lesen", "schreiben");
            Assert.Null(w.TryFind<Border>("agent-first-step"));
            StartAgent(w); Settle();
            WaitFor(() => w.AgentTask0?.IsCompleted == true);
            w.Show(K); Settle();
            Assert.StartsWith("✓ Agent fertig nach 4 Schritten", w.ProgramMessage(K));
            var def = w.Agents.Definitions().Single(x => x.Id == "aufgaben-helfer");
            Assert.Equal("# Aufgaben\n\n- [ ] Fenster putzen", w.Core.Projects.ReadFile(def.ProjectId!, "aufgaben.md"));
            Assert.StartsWith("fertig · 4 Schritte", w.Find<TextBlock>("agent-run-state").Text);
            Assert.Contains(w.Find<StackPanel>("agent-log").Children.OfType<SelectableTextBlock>(), t => t.Text!.Contains("datei.lesen"));
            Assert.NotNull(w.TryFind<SelectableTextBlock>("agent-run-result"));
            Assert.NotNull(w.TryFind<Border>("agent-list-0")); // Agenten-Liste
        }
        finally { w.KiBackendsOverride = null; w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0698 UI +2: Wirkung nach außen fragt in der Lauf-Ansicht – „Ablehnen“ blockiert, „Zustimmen“ führt über das Freigabe-Tor aus")]
    public void Approval_in_run_view()
    {
        var (w, _, root) = Ui.Open();
        var home = Path.Combine(Path.GetTempPath(), "trendo-t0698-home-" + Guid.NewGuid().ToString("N")[..6]); var outDir = Path.Combine(home, "Ausgabe"); Directory.CreateDirectory(home); // außerhalb des Trendo-Datenordners
        var oldHome = MainWindow.WorkflowHome; MainWindow.WorkflowHome = home;
        try
        {
            var ki2 = new ToolKi((m, n) => (n % 3) switch { 1 => Call("datei.schreiben", new { pfad = "brief.md", inhalt = "Hallo" }), 2 => Call("datei.exportieren", new { pfad = "brief.md", ziel = outDir }), _ => Done("ok") });
            w.KiBackendsOverride = () => [ki2];
            NewAgent(w, "export-helfer", "Brief schreiben und hinauslegen.", "lesen", "schreiben", "export");
            StartAgent(w); Settle();
            WaitFor(() => w.AgentAsking is not null); w.Show(K); Settle();
            Assert.True(w.TryFind<Border>("agent-approve") is not null, $"running={w.AgentRunning} live={w.AgentLive?.AgentId}/{w.AgentLive?.State} view={string.Join("|", w.Find<StackPanel>("agent-run-view").Children.OfType<TextBlock>().Select(t => t.Text))}");
            Tap(w, "agent-approve-no");
            WaitFor(() => w.AgentTask0?.IsCompleted == true);
            Assert.False(File.Exists(Path.Combine(outDir, "brief.md")));
            Assert.Contains(w.Agents.Runs("export-helfer", 1)[0].Log, e => e.Kind == "blockiert");

            StartAgent(w); Settle();
            WaitFor(() => w.AgentAsking is not null); w.Show(K); Settle();
            Tap(w, "agent-approve-yes");
            WaitFor(() => w.AgentTask0?.IsCompleted == true);
            Assert.Equal("Hallo", File.ReadAllText(Path.Combine(outDir, "brief.md")));
            Assert.Contains(w.Core.Audit.ReadAll(), a => a.Action == "execution.done");
        }
        finally { MainWindow.WorkflowHome = oldHome; w.KiBackendsOverride = null; w.Close(); try { Directory.Delete(home, true); } catch (IOException) { } }
    }

    [AvaloniaFact(DisplayName = "T-0698 UI +3: Stopp-Knopf in der Lauf-Ansicht beendet einen laufenden Agenten")]
    public void Stop_button()
    {
        var (w, _, _) = Ui.Open();
        try
        {
            var ki = new ToolKi((m, n) => Call("dateien.liste", new { })) { Before = (n, ct) => n >= 2 ? Task.Delay(30_000, ct) : Task.CompletedTask };
            w.KiBackendsOverride = () => [ki];
            NewAgent(w, "lang-laeufer", "Läuft lange.", "lesen");
            StartAgent(w); Settle();
            WaitFor(() => w.AgentLive?.Steps >= 2); w.Show(K); Settle();
            Tap(w, "agent-run-stop");
            WaitFor(() => w.AgentTask0?.IsCompleted == true, 5000);
            Assert.True(w.AgentTask0!.IsCompleted);
            Assert.Equal(AgentRunState.Gestoppt, w.Agents.Runs("lang-laeufer", 1)[0].State);
            Assert.False(w.AgentRunning);
        }
        finally { w.KiBackendsOverride = null; w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0698 UI +4: Abläufe – Baustein „Agent ausführen“ anhängen und Agent wählen (Zeitplan/Auslöser ohne zweiten Scheduler)")]
    public void Workflow_agent_step()
    {
        var (w, _, root) = Ui.Open();
        var home = Path.Combine(root, "home"); Directory.CreateDirectory(home);
        var oldHome = MainWindow.WorkflowHome; MainWindow.WorkflowHome = home;
        try
        {
            var def = w.Agents.SaveDefinition(new AgentDefinition { Name = "Bericht", Goal = "Wochenbericht schreiben", Tools = ["lesen", "schreiben"] });
            var wf = w.Workflows.Add(new Workflow { Name = "Freitags", Trigger = TriggerKind.Taeglich, At = "16:00" });
            w.WfSelected = wf.Id; w.Show("ablaeufe"); Settle();
            w.Find<Button>("wf-add-agentausfuehren").Press(); Settle();
            var pick = w.Find<ComboBox>("wf-edit-agent");
            pick.SelectedIndex = 0; Settle();
            w.Find<Button>("wf-edit-save").Press(); Settle();
            var step = w.Workflows.All().Single(x => x.Id == wf.Id).Steps.Single();
            Assert.Equal(StepKind.AgentAusfuehren, step.Kind); Assert.Equal(def.Id, step.Text);
            Assert.True(w.Workflows.All().Single(x => x.Id == wf.Id).NeedsApproval);
        }
        finally { MainWindow.WorkflowHome = oldHome; w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0698 UI +5: KI-Team – Rollen geben Dateien weiter, Prüfer gibt zurück, Ergebnis-Dateien im Projekt")]
    public void Team_with_files()
    {
        var (w, _, _) = Ui.Open();
        try
        {
            var reviews = 0;
            var ki3 = new ToolKi((m, n) =>
            {
                var sys = m[0].Content;
                if (sys.Contains("„Lektor“")) return Interlocked.Increment(ref reviews) == 1 ? Done("verbessert") : Done("verbessert 2");
                if (sys.Contains("„Texter“")) return m.Count == 2 ? Call("datei.schreiben", new { pfad = "flyer.md", inhalt = "Flohmarkt am Samstag" }) : Done("flyer.md geschrieben");
                return Done("1. Text schreiben 2. Prüfen");
            });
            w.KiBackendsOverride = () => [ki3];
            w.Core.Projects.Create("flyer", "Flyer");
            w.Show("ki-rollen"); Settle();
            w.Find<ComboBox>("agent-project").SelectedIndex = 1;
            w.Find<TextBox>("agent-goal").Text = "Flyer für den Flohmarkt";
            w.Find<Button>("team-text").Press(); Settle();
            WaitFor(() => !w.AgentRunning && w.Find<TextBlock>("agent-msg").Text?.StartsWith("Team") == true);
            Assert.StartsWith("Team fertig: 3 Rollen-Läufe", w.Find<TextBlock>("agent-msg").Text);
            Assert.Equal("Flohmarkt am Samstag", w.Core.Projects.ReadFile("flyer", "flyer.md"));
            var teamDir = Path.Combine(w.Core.Projects.ProjectFilesRoot("flyer"), "team");
            Assert.Equal(3, Directory.GetFiles(Directory.GetDirectories(teamDir).Single(), "*.md").Length);
        }
        finally { w.KiBackendsOverride = null; w.Close(); }
    }
}
