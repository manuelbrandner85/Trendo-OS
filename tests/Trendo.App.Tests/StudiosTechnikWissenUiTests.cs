using System.Text.Json.Nodes;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Trendo.Core;
using Trendo.Core.PromptAction;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// <summary>Phase E, Paket Technik und Wissen.</summary>
public class StudiosTechnikWissen_Ui
{
    static async Task<T> Run<T>(Task<T> t) { for (var i = 0; i < 3000 && !t.IsCompleted; i++) { Settle(); await Task.Delay(2); } Settle(); return await t; }
    static async Task<JsonObject> Call(TrendoMcp m, string tool, JsonObject? args = null) =>
        (await Run(m.HandleAsync(new JsonObject { ["jsonrpc"] = "2.0", ["id"] = 1, ["method"] = "tools/call", ["params"] = new JsonObject { ["name"] = tool, ["arguments"] = args ?? [] } })))!["result"]!.AsObject();
    static string Text(JsonObject r) => (string)r["content"]![0]!["text"]!;
    static bool Failed(JsonObject r) => (bool)r["isError"]!;
    static string Folder() { var d = Path.Combine(Path.GetTempPath(), "trendo-technik-" + Guid.NewGuid().ToString("N")[..8]); Directory.CreateDirectory(d); return d; }

    public static TheoryData<string> Studios => new() { "SPEC-01", "SPEC-02", "SPEC-03", "SPEC-10", "SPEC-13", "SPEC-14", "SPEC-16", "CORE-09", "SPEC-05", "SPEC-06", "CORE-10", "SYS-08" };

    [Theory(DisplayName = "Technik und Wissen: Fachwissen ist vollständig und verweist nur auf Vorhandenes")]
    [MemberData(nameof(Studios))]
    public void Knowledge_complete(string id)
    {
        var k = StudioKnowledgeBase.For(id)!;
        Assert.True(k.Detailed); Assert.InRange(k.Tasks.Count, 2, 5);
        Assert.NotEmpty(k.Purpose); Assert.NotEmpty(k.Inputs); Assert.NotEmpty(k.Outputs); Assert.NotEmpty(k.Formats); Assert.NotEmpty(k.Workflows); Assert.NotEmpty(k.Quality);
        Assert.All(k.Helpers, h => { Assert.NotNull(CapabilityCatalog.Find(h)); Assert.NotEqual(id, h); });
        Assert.All(k.Programs, p => Assert.Contains(DependencyCatalog.All, c => c.Id == p));
    }

    [Theory(DisplayName = "Technik und Wissen: Auftrag in Alltagssprache führt zur passenden Aufgabe")]
    [InlineData("SPEC-01", "Wie viel Farbe brauche ich für das Wohnzimmer?", "material")]
    [InlineData("SPEC-01", "Stell ein Sofa in den Raum", "einrichten")]
    [InlineData("SPEC-02", "Simuliere die Spannung am Kondensator", "simulieren")]
    [InlineData("SPEC-02", "Mach daraus eine Platine", "platine")]
    [InlineData("SPEC-03", "Berechne ein Fachwerk", "rahmen")]
    [InlineData("SPEC-03", "Hält ein IPE 200?", "nachweis")]
    [InlineData("SPEC-13", "Gib mir das als GPX fürs Navi", "gpx")]
    [InlineData("SPEC-14", "Prüfe den G-Code auf Kollisionen", "pruefen")]
    [InlineData("SPEC-16", "Ich habe 40 Liter getankt", "tanken")]
    [InlineData("SPEC-05", "Zeichne ein Histogramm", "diagramm")]
    [InlineData("SPEC-05", "Leg eine Gerade durch die Punkte", "kurve")]
    [InlineData("SPEC-06", "Frag mich die fälligen Karten ab", "ueben")]
    [InlineData("SYS-08", "Vergleiche, ob sich die beiden widersprechen", "vergleich")]
    public void Intent(string studio, string request, string task) => Assert.Equal(task, StudioKnowledgeBase.Match(StudioKnowledgeBase.For(studio)!, request)[0].Id);

    [AvaloniaTheory(DisplayName = "Technik und Wissen: Claude erreicht jedes Studio und bekommt das richtige Fachwissen")]
    [InlineData("architektur", "SPEC-01")]
    [InlineData("elektronik", "SPEC-02")]
    [InlineData("technik", "SPEC-03")]
    [InlineData("zwilling", "SPEC-10")]
    [InlineData("karten", "SPEC-13")]
    [InlineData("cnc", "SPEC-14")]
    [InlineData("fahrzeug", "SPEC-16")]
    [InlineData("roboter", "CORE-09")]
    [InlineData("wissenschaft", "SPEC-05")]
    [InlineData("lernen", "SPEC-06")]
    [InlineData("ki-wissen", "CORE-10")]
    [InlineData("recherche", "SYS-08")]
    public async Task Claude_reaches(string page, string studio)
    {
        var (w, _, _) = Ui.Open();
        var m = w.ClaudeBridgeForTest(Folder(), page);
        Assert.False(Failed(await Call(m, "programm_oeffnen", new JsonObject { ["programm"] = page })));
        Assert.Equal(page, w.CurrentView);
        Assert.False(Failed(await Call(m, "seite_lesen")));
        Assert.True((bool)JsonNode.Parse(Text(await Call(m, "studio_wissen", new JsonObject { ["studio"] = studio })))!["ausfuehrlich"]!);
        w.Close();
    }

    [AvaloniaTheory(DisplayName = "Technik und Wissen: Dokumentprogramme zeigen ihre Aufgaben auf der Startseite")]
    [InlineData("architektur", "grundriss")]
    [InlineData("elektronik", "schaltung")]
    [InlineData("technik", "balken")]
    [InlineData("karten", "tour")]
    [InlineData("cnc", "programm")]
    [InlineData("wissenschaft", "auswerten")]
    public void Start_tasks(string page, string task)
    {
        var (w, _, _) = Ui.Open();
        w.Show(page); Settle();
        Assert.Contains(w.GetVisualDescendants().OfType<Button>(), b => b.Name == "studio-task-" + task && b.IsEffectivelyVisible);
        w.Close();
    }

    [Fact(DisplayName = "Maschinen: Echte Bewegung und Fertigung sind in den Grenzen der Studios genannt")]
    public void Machine_limits()
    {
        Assert.Contains("Keine Fräs- oder Maschinenbefehle", CapabilityCatalog.Find("SPEC-14")!.Blocker);
        Assert.Contains("Freigabe", string.Join(" ", StudioKnowledgeBase.For("CORE-09")!.Quality));
        Assert.Contains("keine Maschine", string.Join(" ", StudioKnowledgeBase.For("SPEC-14")!.Quality));
    }
}
