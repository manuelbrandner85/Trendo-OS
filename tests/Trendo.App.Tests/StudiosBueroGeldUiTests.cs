using System.Text.Json.Nodes;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Trendo.Core;
using Trendo.Core.Programs;
using Trendo.Core.PromptAction;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// <summary>Phase E, Paket Büro und Geld.</summary>
public class StudiosBueroGeld_Ui
{
    static async Task<T> Run<T>(Task<T> t) { for (var i = 0; i < 3000 && !t.IsCompleted; i++) { Settle(); await Task.Delay(2); } Settle(); return await t; }
    static async Task<JsonObject> Call(TrendoMcp m, string tool, JsonObject? args = null) =>
        (await Run(m.HandleAsync(new JsonObject { ["jsonrpc"] = "2.0", ["id"] = 1, ["method"] = "tools/call", ["params"] = new JsonObject { ["name"] = tool, ["arguments"] = args ?? [] } })))!["result"]!.AsObject();
    static string Text(JsonObject r) => (string)r["content"]![0]!["text"]!;
    static bool Failed(JsonObject r) => (bool)r["isError"]!;
    static string Folder() { var d = Path.Combine(Path.GetTempPath(), "trendo-buero-" + Guid.NewGuid().ToString("N")[..8]); Directory.CreateDirectory(d); return d; }

    public static TheoryData<string> Studios => new() { "STUDIO-LETTER-01", "SYS-10", "SYS-09", "SPEC-11", "STUDIO-ONLINE-BUSINESS-01", "STUDIO-POD-01", "SYS-02" };

    [Theory(DisplayName = "Büro und Geld: Fachwissen ist vollständig und verweist nur auf Vorhandenes")]
    [MemberData(nameof(Studios))]
    public void Knowledge_complete(string id)
    {
        var k = StudioKnowledgeBase.For(id)!;
        Assert.True(k.Detailed); Assert.InRange(k.Tasks.Count, 3, 5);
        Assert.NotEmpty(k.Purpose); Assert.NotEmpty(k.Inputs); Assert.NotEmpty(k.Outputs); Assert.NotEmpty(k.Formats); Assert.NotEmpty(k.Workflows); Assert.NotEmpty(k.Quality);
        Assert.All(k.Helpers, h => { Assert.NotNull(CapabilityCatalog.Find(h)); Assert.NotEqual(id, h); });
        Assert.All(k.Programs, p => Assert.Contains(DependencyCatalog.All, c => c.Id == p));
        Assert.NotNull(CapabilityCatalog.Find(id)!.Blocker);
    }

    [Theory(DisplayName = "Büro und Geld: Auftrag in Alltagssprache führt zur passenden Aufgabe")]
    [InlineData("STUDIO-LETTER-01", "Kündigung meines Fitnessstudios", "kuendigung")]
    [InlineData("STUDIO-LETTER-01", "Widerspruch gegen einen Bescheid", "widerspruch")]
    [InlineData("STUDIO-LETTER-01", "Mietminderung wegen Heizungsausfall", "beschwerde")]
    [InlineData("SYS-10", "Haushaltsplan als Tabelle", "tabelle")]
    [InlineData("SYS-10", "Präsentation über unser Projekt, 8 Folien", "praesentation")]
    [InlineData("SYS-10", "Erstelle ein professionelles Angebot", "text")]
    [InlineData("SYS-09", "Termin nächste Woche Dienstag 10 Uhr", "termin")]
    [InlineData("SYS-09", "Antwort auf die letzte E-Mail entwerfen", "mail")]
    [InlineData("SPEC-11", "Rechnung für Erika Muster schreiben", "rechnung")]
    [InlineData("SPEC-11", "Sparplan für 5.000 €", "sparen")]
    [InlineData("STUDIO-ONLINE-BUSINESS-01", "Preise kalkulieren", "preis")]
    [InlineData("STUDIO-POD-01", "Hat mein Motiv genug dpi?", "motiv")]
    public void Intent(string studio, string request, string task) => Assert.Equal(task, StudioKnowledgeBase.Match(StudioKnowledgeBase.For(studio)!, request)[0].Id);

    [AvaloniaTheory(DisplayName = "Büro und Geld: Claude erreicht jedes Studio und bekommt das richtige Fachwissen samt Grenzen")]
    [InlineData("brief", "STUDIO-LETTER-01")]
    [InlineData("text", "SYS-10")]
    [InlineData("tabelle", "SYS-10")]
    [InlineData("post", "SYS-09")]
    [InlineData("finanzen", "SPEC-11")]
    [InlineData("verkaufen", "STUDIO-ONLINE-BUSINESS-01")]
    [InlineData("pod-programm", "STUDIO-POD-01")]
    [InlineData("werkzeuge", "SYS-02")]
    public async Task Claude_reaches(string page, string studio)
    {
        var (w, _, _) = Ui.Open();
        w.ClaudeConsentOverride = _ => Task.FromResult(true); // private Seiten fragen, hier zugestimmt
        var m = w.ClaudeBridgeForTest(Folder(), page);
        Assert.False(Failed(await Call(m, "programm_oeffnen", new JsonObject { ["programm"] = page })));
        Assert.Equal(page, w.CurrentView);
        Assert.False(Failed(await Call(m, "seite_lesen")));
        var k = JsonNode.Parse(Text(await Call(m, "studio_wissen", new JsonObject { ["studio"] = studio })))!;
        Assert.True((bool)k["ausfuehrlich"]!); Assert.False(string.IsNullOrEmpty((string?)k["grenzen"]));
        w.Close();
    }

    [AvaloniaFact(DisplayName = "Büro und Geld: Print on Demand zeigt seine Aufgaben auf der Startseite")]
    public void Pod_start()
    {
        var (w, _, _) = Ui.Open();
        w.Show("pod-programm"); Settle();
        foreach (var t in StudioKnowledgeBase.For("STUDIO-POD-01")!.Tasks)
            Assert.Contains(w.GetVisualDescendants().OfType<Button>(), b => b.Name == "studio-task-" + t.Id && b.IsEffectivelyVisible);
        w.Close();
    }

    [Theory(DisplayName = "Büro und Geld: Senden, Bezahlen und Bestellen fragen immer, Entwerfen nicht")]
    [InlineData("E-Mail senden", true)]
    [InlineData("Rechnung versenden", true)]
    [InlineData("Jetzt bezahlen", true)]
    [InlineData("Bei Anbieter bestellen", true)]
    [InlineData("Entwurf speichern", false)]
    [InlineData("Termin eintragen", false)]
    [InlineData("Rechnung erstellen", false)]
    public void Consent(string label, bool asks) => Assert.Equal(asks, ClaudeActions.NeedsConsent(label, "x", true));

    [AvaloniaFact(DisplayName = "Büro und Geld: Fehlendes LibreOffice wird gemeldet, nicht verschwiegen")]
    public async Task Missing_office()
    {
        var (w, _, _) = Ui.Open();
        w.ProgramFindOverride = _ => null;
        var m = w.ClaudeBridgeForTest(Folder(), "brief");
        await Call(m, "programm_oeffnen", new JsonObject { ["programm"] = "brief" });
        var p = JsonNode.Parse(Text(await Call(m, "programme_pruefen", new JsonObject { ["programme"] = "libreoffice" })))!.AsArray()[0]!;
        Assert.False((bool)p["installiert"]!); Assert.False((bool)p["startet"]!);
        w.Close();
    }
}
