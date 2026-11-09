using System.Text.Json.Nodes;
using Avalonia.Headless.XUnit;
using Trendo.Core;
using Trendo.Core.PromptAction;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// <summary>Phase E, letztes Paket und Gesamtblick: jedes Studio hat Fachwissen; heikle Seiten sind geschützt.</summary>
public class StudiosAlle_Ui
{
    static async Task<T> Run<T>(Task<T> t) { for (var i = 0; i < 3000 && !t.IsCompleted; i++) { Settle(); await Task.Delay(2); } Settle(); return await t; }
    static async Task<JsonObject> Call(TrendoMcp m, string tool, JsonObject? args = null) =>
        (await Run(m.HandleAsync(new JsonObject { ["jsonrpc"] = "2.0", ["id"] = 1, ["method"] = "tools/call", ["params"] = new JsonObject { ["name"] = tool, ["arguments"] = args ?? [] } })))!["result"]!.AsObject();
    static string Text(JsonObject r) => (string)r["content"]![0]!["text"]!;
    static bool Failed(JsonObject r) => (bool)r["isError"]!;
    static string Folder() { var d = Path.Combine(Path.GetTempPath(), "trendo-alle-" + Guid.NewGuid().ToString("N")[..8]); Directory.CreateDirectory(d); return d; }

    public static TheoryData<string> AllStudios { get { var d = new TheoryData<string>(); foreach (var id in CapabilityCatalog.Ids.Where(i => i != "SPEC-07")) d.Add(id); return d; } }

    [Theory(DisplayName = "Alle Studios: ausführliches Fachwissen, gültige Helfer und Programme, mindestens eine Aufgabe")]
    [MemberData(nameof(AllStudios))]
    public void Knowledge_complete(string id)
    {
        var k = StudioKnowledgeBase.For(id)!;
        Assert.True(k.Detailed, id + " hat kein ausführliches Fachwissen");
        Assert.InRange(k.Tasks.Count, 1, 5);
        Assert.NotEmpty(k.Purpose); Assert.NotEmpty(k.Inputs); Assert.NotEmpty(k.Outputs); Assert.NotEmpty(k.Workflows); Assert.NotEmpty(k.Quality);
        Assert.All(k.Helpers, h => { Assert.NotNull(CapabilityCatalog.Find(h)); Assert.NotEqual(id, h); });
        Assert.All(k.Programs, p => Assert.True(DependencyCatalog.All.Any(c => c.Id == p) || Trendo.Core.Programs.EngineTargets.Engines.Any(e => e.Id == p), p));
        Assert.All(k.Tasks, t => { Assert.NotEmpty(t.Keywords); Assert.NotEmpty(t.Starter); });
    }

    [Fact(DisplayName = "Abgelehnt bleibt abgelehnt: SPEC-07 bekommt keine ausführliche Beschreibung")]
    public void Rejected_stays() => Assert.False(StudioKnowledgeBase.For("SPEC-07")!.Detailed);

    [Theory(DisplayName = "Letztes Paket: Auftrag in Alltagssprache führt zur passenden Aufgabe")]
    [InlineData("SPEC-08", "Zeig mir das in 3D auf der Puppe", "anprobe")]
    [InlineData("SPEC-15", "Richte eine Szene mit Kamera ein", "szene")]
    [InlineData("STUDIO-TV-01", "Nimm den Film heute Abend auf", "aufnahme")]
    [InlineData("CORE-02", "Fasse diesen Text zusammen", "zusammenfassen")]
    [InlineData("SYS-05", "Jeden Morgen Wetter und Termine zeigen", "ablauf")]
    [InlineData("X-03", "App für Einkaufslisten", "app")]
    [InlineData("SYS-04", "Bring die Programme auf den neuesten Stand per Update", "aktualisieren")]
    [InlineData("CORE-11", "Mach ein Backup", "sicherung")]
    public void Intent(string studio, string request, string task) => Assert.Equal(task, StudioKnowledgeBase.Match(StudioKnowledgeBase.For(studio)!, request)[0].Id);

    [AvaloniaFact(DisplayName = "Schutz: Die Freigaben-Seite bleibt für Claude gesperrt, auch mit Zustimmung")]
    public async Task Approvals_blocked()
    {
        var (w, _, _) = Ui.Open();
        w.ClaudeConsentOverride = _ => Task.FromResult(true);
        var m = w.ClaudeBridgeForTest(Folder());
        var before = w.CurrentView;
        var r = await Call(m, "programm_oeffnen", new JsonObject { ["programm"] = "approvals" });
        Assert.True(Failed(r)); Assert.Contains("nur der Nutzer", Text(r));
        Assert.Equal(before, w.CurrentView);
        w.Show("approvals"); Settle(); // auch wenn die Person sie selbst geöffnet hat
        Assert.True(Failed(await Call(m, "seite_lesen")));
        Assert.True(Failed(await Call(m, "bild_ansehen")));
        w.Close();
    }

    [AvaloniaTheory(DisplayName = "Schutz: Private Seiten öffnen nur nach Zustimmung; Nein lässt die Seite zu")]
    [InlineData("keys")]
    [InlineData("forensik")]
    [InlineData("posteingang")]
    [InlineData("finanzen")]
    public async Task Sensitive_refused(string page)
    {
        var (w, _, _) = Ui.Open();
        var asked = 0; w.ClaudeConsentOverride = _ => { asked++; return Task.FromResult(false); };
        var m = w.ClaudeBridgeForTest(Folder());
        var before = w.CurrentView;
        var r = await Call(m, "programm_oeffnen", new JsonObject { ["programm"] = page });
        Assert.True(Failed(r)); Assert.Contains("Nicht bestätigt", Text(r));
        Assert.Equal(1, asked); Assert.Equal(before, w.CurrentView);
        w.Close();
    }

    [AvaloniaFact(DisplayName = "Schutz: Zustimmung gilt je Lauf einmal, ein neuer Lauf fragt wieder")]
    public async Task Consent_once_per_run()
    {
        var (w, _, _) = Ui.Open();
        var asked = 0; w.ClaudeConsentOverride = _ => { asked++; return Task.FromResult(true); };
        var m = w.ClaudeBridgeForTest(Folder());
        Assert.False(Failed(await Call(m, "programm_oeffnen", new JsonObject { ["programm"] = "finanzen" })));
        Assert.False(Failed(await Call(m, "seite_lesen")));
        Assert.False(Failed(await Call(m, "seite_lesen")));
        Assert.Equal(1, asked);
        var next = w.ClaudeBridgeForTest(Folder());
        Assert.False(Failed(await Call(next, "seite_lesen")));
        Assert.Equal(2, asked);
        w.Close();
    }

    [AvaloniaFact(DisplayName = "Schutz: Claude kann seine eigenen Rechte nicht ändern, auch nicht mit Zustimmung")]
    public async Task Rights_switch_refused()
    {
        var (w, _, _) = Ui.Open();
        w.ClaudeConsentOverride = _ => Task.FromResult(true);
        var m = w.ClaudeBridgeForTest(Folder());
        await Call(m, "programm_oeffnen", new JsonObject { ["programm"] = "technik", ["vorlage"] = "einfeld" }); Settle();
        var root = Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(w).OfType<Avalonia.Controls.StackPanel>().First(p => p.IsEffectivelyVisible && p.Bounds.Width > 0 && Avalonia.VisualTree.VisualExtensions.FindAncestorOfType<Avalonia.Controls.Border>(p)?.Name == "prog-workspace");
        var toggle = new Avalonia.Controls.CheckBox { Name = "claude-always-full", Content = "Internet", IsChecked = false }; root.Children.Add(toggle); Settle();
        var r = await Call(m, "feld_setzen", new JsonObject { ["ziel"] = "claude-always-full", ["wert"] = true });
        Assert.True(Failed(r)); Assert.Contains("nur der Nutzer", Text(r)); Assert.False(toggle.IsChecked);
        Assert.True(Failed(await Call(m, "klicken", new JsonObject { ["ziel"] = "claude-always-full" }))); Assert.False(toggle.IsChecked);
        w.Close();
    }

    [AvaloniaFact(DisplayName = "Schutz: Einwilligungen im Dokument setzt nur der Nutzer")]
    public async Task Consent_field_refused()
    {
        var (w, _, _) = Ui.Open();
        var m = w.ClaudeBridgeForTest(Folder());
        await Call(m, "programm_oeffnen", new JsonObject { ["programm"] = "technik", ["vorlage"] = "einfeld" });
        var r = await Call(m, "dokument_setzen", new JsonObject { ["pfad"] = "quelle/consent", ["wert"] = true });
        Assert.True(Failed(r)); Assert.Contains("nur der Nutzer", Text(r));
        Assert.Null(w.ProgramActive("technik")!.Data["quelle"]);
        w.Close();
    }

    [AvaloniaFact(DisplayName = "Not-Stopp: Bricht laufende Claude-Arbeit ab und lässt keinen neuen Durchgang starten")]
    public async Task Halt_stops_claude()
    {
        var w = await Chat("CORE-06", "spiele-programm");
        var started = 0; var cancelled = new TaskCompletionSource();
        MainWindow.ClaudeRunOverride = async (_, _, _, ct) =>
        {
            started++;
            try { await Task.Delay(-1, ct); } catch (OperationCanceledException) { cancelled.TrySetResult(); throw; }
            return 0;
        };
        try
        {
            var send = w.ClaudeChatSend("Baue ein Spiel");
            for (var i = 0; i < 500 && started == 0; i++) { Settle(); await Task.Delay(5); }
            Assert.Equal(1, started);
            await Run(Done(w.HaltAll("Prüfung")));
            await Run(Done(cancelled.Task.WaitAsync(TimeSpan.FromSeconds(10))));
            await Run(Done(send));
            Assert.Equal(1, started); // kein weiterer Durchgang
            await Run(Done(w.ClaudeChatSend("Nochmal")));
            Assert.Equal(1, started); // kein neuer Lauf während des Not-Stopps
            w.Core.Approvals.Resume(Principal.LocalOwner);
        }
        finally { MainWindow.ClaudeRunOverride = null; MainWindow.ClaudeStatusOverride = null; }
        w.Close();
    }
    static async Task<bool> Done(Task t) { await t; return true; }

    static Action<string> Emit(Action<string> line) => line;
    static void Say(Action<string> line, string text)
    {
        line(System.Text.Json.JsonSerializer.Serialize(new { type = "system", subtype = "init", session_id = "s1", model = "claude" }));
        line(System.Text.Json.JsonSerializer.Serialize(new { type = "assistant", message = new { content = new object[] { new { type = "text", text } } } }));
        line(System.Text.Json.JsonSerializer.Serialize(new { type = "result", subtype = "success", is_error = false, result = text, session_id = "s1", num_turns = 1 }));
    }

    static async Task<MainWindow> Chat(string studio, string page)
    {
        var (w, _, _) = Ui.Open();
        w.ClaudeNoClicks = true; MainWindow.ClaudeStepDelay = 0;
        MainWindow.ClaudeStatusOverride = () => Task.FromResult(new ClaudeCode.Status(true, true, "claude.ai", "max", @"C:\x\claude.exe"));
        await Run(Done(w.ClaudeCheck(true)));
        w.Show(page); Settle(); w.ClaudeDockToggle(studio); w.Show(page); Settle();
        return w;
    }

    [AvaloniaFact(DisplayName = "Nutzerweg: Claude bekommt den Zugang samt Freigabe und ist bei „fertig“ am Ende, ohne das Wort in der Antwort")]
    public async Task Chat_finishes()
    {
        var w = await Chat("CORE-06", "spiele-programm");
        var calls = new List<IReadOnlyList<string>>();
        MainWindow.ClaudeRunOverride = (a, _, line, _) => { calls.Add(a); Say(line, "Alles gebaut.\nTRENDO_FERTIG"); return Task.FromResult(0); };
        try
        {
            await Run(Done(w.ClaudeChatSend("Baue ein Spiel")));
            var a = Assert.Single(calls);
            Assert.Contains("--mcp-config", a); Assert.Contains(TrendoMcp.AllowedTools, a);
            Assert.Contains(a, x => x.Contains("DU ARBEITEST SELBSTÄNDIG"));
            Assert.DoesNotContain("TRENDO_FERTIG", w.ClaudeChatCurrent()!.Messages[^1].Text);
            var config = a[a.ToList().IndexOf("--mcp-config") + 1];
            Assert.False(File.Exists(config)); // Zugangsdaten sind nach dem Lauf weg
        }
        finally { MainWindow.ClaudeRunOverride = null; MainWindow.ClaudeStatusOverride = null; }
        w.Close();
    }

    [AvaloniaFact(DisplayName = "Nutzerweg: Ohne „fertig“ arbeitet Claude weiter, aber höchstens acht Durchgänge")]
    public async Task Chat_continues_limited()
    {
        var w = await Chat("CORE-06", "spiele-programm");
        var prompts = new List<string>();
        MainWindow.ClaudeRunOverride = (a, _, line, _) => { prompts.Add(a[a.ToList().IndexOf("-p") + 1]); Say(line, "Noch nicht fertig."); return Task.FromResult(0); };
        try
        {
            await Run(Done(w.ClaudeChatSend("Baue ein Spiel")));
            Assert.Equal(8, prompts.Count);
            Assert.Contains(TrendoMcp.ContinuePrompt, prompts[1]);
        }
        finally { MainWindow.ClaudeRunOverride = null; MainWindow.ClaudeStatusOverride = null; }
        w.Close();
    }

    [AvaloniaFact(DisplayName = "Nutzerweg: Ein harmloser Plan läuft von selbst, bei offener Freigabe bleibt er stehen")]
    public async Task Plan_guard()
    {
        var w = await Chat("SPEC-03", "technik");
        MainWindow.ClaudeRunOverride = (_, _, line, _) => { Say(line, "Ich schalte die Ansicht um.\n[[trendo:klick | prog-cmd-tk-m]]\nTRENDO_FERTIG"); return Task.FromResult(0); };
        try
        {
            w.ProgramNew("technik", "einfeld"); w.Show("technik"); Settle();
            await Run(Done(w.ClaudeChatSend("Zeige die Momente")));
            Assert.False(w.ClaudePlanPending());
            var open = w.Core.Approvals.Request(Principal.LocalOwner, "test", "offen", "ziel");
            await Run(Done(w.ClaudeChatSend("Noch einmal")));
            Assert.True(w.ClaudePlanPending());
            w.Core.Approvals.Deny(open.Id, Principal.LocalOwner);
        }
        finally { MainWindow.ClaudeRunOverride = null; MainWindow.ClaudeStatusOverride = null; }
        w.Close();
    }

    [AvaloniaFact(DisplayName = "Nutzerweg: Mit ausgeschaltetem „ohne Klicks“ bleiben Plan, Vergleich und Behalten wie zuvor")]
    public async Task Switch_restores()
    {
        var w = await Chat("SPEC-03", "technik");
        w.ClaudeNoClicks = false;
        MainWindow.ClaudeRunOverride = (a, _, line, _) => { Assert.DoesNotContain("--mcp-config", a); Say(line, "Plan.\n[[trendo:klick | prog-cmd-tk-m]]"); return Task.FromResult(0); };
        try
        {
            w.ProgramNew("technik", "einfeld"); w.Show("technik"); Settle();
            await Run(Done(w.ClaudeChatSend("Zeige die Momente")));
            Assert.True(w.ClaudePlanPending());
        }
        finally { MainWindow.ClaudeRunOverride = null; MainWindow.ClaudeStatusOverride = null; }
        w.Close();
    }

    [AvaloniaFact(DisplayName = "Schutz: Fachwissen bleibt ohne Zustimmung abrufbar, es enthält keine privaten Daten")]
    public async Task Knowledge_without_consent()
    {
        var (w, _, _) = Ui.Open();
        var asked = 0; w.ClaudeConsentOverride = _ => { asked++; return Task.FromResult(false); };
        var m = w.ClaudeBridgeForTest(Folder());
        Assert.False(Failed(await Call(m, "studio_wissen", new JsonObject { ["studio"] = "CORE-11" })));
        Assert.Equal(0, asked);
        w.Close();
    }
}
