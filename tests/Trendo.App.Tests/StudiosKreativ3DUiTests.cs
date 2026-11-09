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

/// <summary>Phase E, Paket Kreativ und 3D: Filme, Musik, Videos und Fotos, 3D-Modelle, Webseite.</summary>
public class StudiosKreativ3D_Ui
{
    static async Task<T> Run<T>(Task<T> t) { for (var i = 0; i < 3000 && !t.IsCompleted; i++) { Settle(); await Task.Delay(2); } Settle(); return await t; }
    static async Task<JsonObject> Call(TrendoMcp m, string tool, JsonObject? args = null) =>
        (await Run(m.HandleAsync(new JsonObject { ["jsonrpc"] = "2.0", ["id"] = 1, ["method"] = "tools/call", ["params"] = new JsonObject { ["name"] = tool, ["arguments"] = args ?? [] } })))!["result"]!.AsObject();
    static string Text(JsonObject r) => (string)r["content"]![0]!["text"]!;
    static bool Failed(JsonObject r) => (bool)r["isError"]!;
    static string Folder() { var d = Path.Combine(Path.GetTempPath(), "trendo-paket-" + Guid.NewGuid().ToString("N")[..8]); Directory.CreateDirectory(d); return d; }

    public static TheoryData<string> Studios => new() { "CORE-08", "STUDIO-FILM-01", "STUDIO-MUSIK-01", "CORE-05", "CORE-07" };

    [Theory(DisplayName = "Kreativ und 3D: Fachwissen ist vollständig und verweist nur auf Vorhandenes")]
    [MemberData(nameof(Studios))]
    public void Knowledge_complete(string id)
    {
        var k = StudioKnowledgeBase.For(id)!;
        Assert.True(k.Detailed); Assert.InRange(k.Tasks.Count, 3, 5);
        Assert.NotEmpty(k.Purpose); Assert.NotEmpty(k.Inputs); Assert.NotEmpty(k.Outputs); Assert.NotEmpty(k.Formats); Assert.NotEmpty(k.Workflows); Assert.NotEmpty(k.Quality);
        Assert.All(k.Helpers, h => { Assert.NotNull(CapabilityCatalog.Find(h)); Assert.NotEqual(id, h); });
        Assert.All(k.Programs, p => Assert.Contains(DependencyCatalog.All, c => c.Id == p));
        Assert.Equal(k.Tasks.Count, k.Tasks.Select(t => t.Id).Distinct().Count());
        Assert.All(k.Tasks, t => { Assert.NotEmpty(t.Keywords); Assert.NotEmpty(t.Starter); if (t.Program is { } p) Assert.True(MainWindow.Programs.ContainsKey(p), p); });
    }

    [Theory(DisplayName = "Kreativ und 3D: Auftrag in Alltagssprache führt zur passenden Aufgabe")]
    [InlineData("CORE-08", "Schneide aus meinen Clips ein Urlaubsvideo", "schnitt")]
    [InlineData("CORE-08", "Gestalte ein Plakat für das Stadtfest", "bild")]
    [InlineData("CORE-08", "Mach das Rauschen aus meiner Aufnahme weg", "ton")]
    [InlineData("STUDIO-FILM-01", "Plane die Kamera-Einstellungen", "shots")]
    [InlineData("STUDIO-FILM-01", "Mach mir einen Trailer", "trailer")]
    [InlineData("STUDIO-MUSIK-01", "Trenne Gesang und Begleitung, ich brauche Karaoke", "trennen")]
    [InlineData("STUDIO-MUSIK-01", "Ich brauche ein Cover", "cover")]
    [InlineData("CORE-05", "Baue mir eine realistische mittelalterliche Burg", "modell")]
    [InlineData("CORE-05", "Das soll auf den Bambu Drucker", "druck")]
    [InlineData("CORE-07", "Baue mir eine moderne Firmenwebseite", "seite")]
    [InlineData("CORE-07", "Bau ein Kontaktformular ein", "funktion")]
    public void Intent(string studio, string request, string task) => Assert.Equal(task, StudioKnowledgeBase.Match(StudioKnowledgeBase.For(studio)!, request)[0].Id);

    [AvaloniaTheory(DisplayName = "Kreativ und 3D: Dokumentprogramme zeigen nur ihre eigenen Aufgaben, ein Klick füllt die Eingabe")]
    [InlineData("schnitt", "schnitt", "bild")]
    [InlineData("bild", "bild", "ton")]
    [InlineData("ton", "ton", "schnitt")]
    [InlineData("film-programm", "drehbuch", "schnitt")]
    public void Start_tasks(string page, string shown, string hidden)
    {
        var (w, _, _) = Ui.Open();
        w.Show(page); Settle();
        Button? B(string n) => w.GetVisualDescendants().OfType<Button>().LastOrDefault(b => b.Name == "studio-task-" + n && b.IsEffectivelyVisible);
        Assert.NotNull(B(shown)); Assert.Null(B(hidden));
        RealClick(w, B(shown)!); Settle();
        var box = w.GetVisualDescendants().OfType<TextBox>().Last(t => t.Name == "auftrag-prompt" && t.IsEffectivelyVisible);
        Assert.False(string.IsNullOrEmpty(box.Text));
        w.Close();
    }

    [AvaloniaTheory(DisplayName = "Kreativ und 3D: Claude erreicht jedes Studio und bekommt dort das richtige Fachwissen")]
    [InlineData("schnitt", "CORE-08")]
    [InlineData("film-programm", "STUDIO-FILM-01")]
    [InlineData("musik-studio", "STUDIO-MUSIK-01")]
    [InlineData("3d", "CORE-05")]
    [InlineData("dev", "CORE-07")]
    public async Task Claude_reaches(string page, string studio)
    {
        var (w, _, _) = Ui.Open();
        var m = w.ClaudeBridgeForTest(Folder(), page);
        Assert.False(Failed(await Call(m, "programm_oeffnen", new JsonObject { ["programm"] = page })));
        Assert.False(Failed(await Call(m, "seite_lesen")));
        var k = JsonNode.Parse(Text(await Call(m, "studio_wissen", new JsonObject { ["studio"] = studio })))!;
        Assert.True((bool)k["ausfuehrlich"]!);
        w.Close();
    }

    [AvaloniaTheory(DisplayName = "Kreativ und 3D: Fehlende Fachprogramme werden ehrlich gemeldet")]
    [InlineData("CORE-08", "ffmpeg")]
    [InlineData("CORE-05", "blender")]
    [InlineData("CORE-07", "git")]
    public async Task Missing(string studio, string program)
    {
        var (w, _, _) = Ui.Open();
        w.ProgramFindOverride = _ => null; w.EngineProbeOverride = () => EngineTargets.Engines.Select(e => new EngineFound(e.Id, e.Name, null, null, false, "fehlt", "fehlt")).ToList();
        var m = w.ClaudeBridgeForTest(Folder(), "start");
        var list = JsonNode.Parse(Text(await Call(m, "programme_pruefen", new JsonObject { ["programme"] = string.Join(",", StudioKnowledgeBase.For(studio)!.Programs) })))!.AsArray();
        var p = list.First(x => (string)x!["programm"]! == program)!;
        Assert.False((bool)p["installiert"]!); Assert.False((bool)p["startet"]!);
        Assert.DoesNotContain("verbunden", (string)p["schnittstelle"]!);
        w.Close();
    }

    [AvaloniaFact(DisplayName = "Kreativ und 3D: Vorhandenes ffmpeg gilt erst als startbar, wenn es wirklich startet")]
    public async Task Found_not_started()
    {
        var (w, _, _) = Ui.Open();
        w.ProgramFindOverride = _ => @"C:\x\ffmpeg.exe"; w.ProgramStartsOverride = (_, _) => false;
        var m = w.ClaudeBridgeForTest(Folder(), "schnitt");
        var p = JsonNode.Parse(Text(await Call(m, "programme_pruefen", new JsonObject { ["programme"] = "ffmpeg" })))!.AsArray()[0]!;
        Assert.True((bool)p["installiert"]!); Assert.False((bool)p["startet"]!); Assert.Contains("startet nicht", (string)p["schnittstelle"]!);
        w.Close();
    }

    [AvaloniaFact(DisplayName = "3D: Claude liest und ändert die Szene bis ins Detail, Rückgängig holt sie zurück, ungültige Szenen werden abgelehnt")]
    public async Task Scene_detail()
    {
        var (w, _, _) = Ui.Open();
        var m = w.ClaudeBridgeForTest(Folder(), "3d");
        await Call(m, "programm_oeffnen", new JsonObject { ["programm"] = "3d" });
        w.Studio3DAddShape("cube"); Settle();
        var before = w.Studio3DSceneJson;
        var name = JsonNode.Parse(Text(await Call(m, "dokument_lesen", new JsonObject { ["pfad"] = "parts/0/name" })));
        Assert.NotNull(name);
        Assert.False(Failed(await Call(m, "dokument_setzen", new JsonObject { ["pfad"] = "parts/0/name", ["wert"] = "Tischplatte" })));
        Assert.Equal("Tischplatte", w.Studio3DParts[0].Name);
        Assert.True(Failed(await Call(m, "dokument_setzen", new JsonObject { ["pfad"] = "parts/9/name", ["wert"] = "X" })));
        Assert.Equal("Tischplatte", w.Studio3DParts[0].Name);
        Assert.False(Failed(await Call(m, "rueckgaengig")));
        Assert.Equal(before, w.Studio3DSceneJson);
        w.Close();
    }
}
