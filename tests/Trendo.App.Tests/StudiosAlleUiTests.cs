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
