using System.Text.Json.Nodes;
using Avalonia.Headless.XUnit;
using Trendo.Core;
using Trendo.Core.Programs;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// <summary>Phase D: echter Ablauf über zwei Studios, Bildbearbeitung → Spiele bauen.</summary>
public class StudioUebergabe_Ui
{
    const string GK = "spiele-programm";
    static async Task<T> Run<T>(Task<T> t) { for (var i = 0; i < 3000 && !t.IsCompleted; i++) { Settle(); await Task.Delay(2); } Settle(); return await t; }
    static async Task<JsonObject> Call(TrendoMcp m, string tool, JsonObject? args = null) =>
        (await Run(m.HandleAsync(new JsonObject { ["jsonrpc"] = "2.0", ["id"] = 1, ["method"] = "tools/call", ["params"] = new JsonObject { ["name"] = tool, ["arguments"] = args ?? [] } })))!["result"]!.AsObject();
    static string Text(JsonObject r) => (string)r["content"]![0]!["text"]!;
    static bool Failed(JsonObject r) => (bool)r["isError"]!;
    static string Folder() { var d = Path.Combine(Path.GetTempPath(), "trendo-kette-" + Guid.NewGuid().ToString("N")[..8]); Directory.CreateDirectory(d); return d; }
    static string BK => MainWindow.Programs.Values.First(p => p.Title.Contains("Bild", StringComparison.OrdinalIgnoreCase) && p.Exports.Any(x => x.Name == "png")).Key;

    static async Task<(MainWindow W, TrendoMcp M, string Id, string Folder)> Handed()
    {
        var (w, _, _) = Ui.Open(); var folder = Folder();
        var m = w.ClaudeBridgeForTest(folder, BK, "kette-1");
        Assert.False(Failed(await Call(m, "programm_oeffnen", new JsonObject { ["programm"] = BK, ["vorlage"] = "quadrat", ["neu"] = true })));
        var ex = await Call(m, "exportieren", new JsonObject { ["format"] = "png", ["datei"] = "figur.png" });
        Assert.False(Failed(ex), Text(ex));
        Assert.True(new FileInfo(Path.Combine(folder, "figur.png")).Length > 100);
        var h = await Call(m, "uebergeben", new JsonObject { ["datei"] = "figur.png", ["an_studio"] = "CORE-06", ["art"] = "figur", ["notiz"] = "Held" });
        Assert.False(Failed(h), Text(h));
        return (w, m, (string)JsonNode.Parse(Text(h))!["uebergabe"]!, folder);
    }

    [AvaloniaFact(DisplayName = "Kette: Das Bildprogramm exportiert eine Figur, Spiele bauen übernimmt sie mit Projektbezug und baut sie ins Spiel")]
    public async Task Image_to_game()
    {
        var (w, m, id, _) = await Handed();
        Assert.False(Failed(await Call(m, "programm_oeffnen", new JsonObject { ["programm"] = GK, ["vorlage"] = "retro-plattform", ["neu"] = true })));
        var open = JsonNode.Parse(Text(await Call(m, "uebergaben")))!.AsArray();
        Assert.Equal(id, (string)Assert.Single(open)!["uebergabe"]!);
        var take = await Call(m, "uebernehmen", new JsonObject { ["uebergabe"] = id, ["verwendung"] = "figur" });
        Assert.False(Failed(take), Text(take));
        var d = w.ProgramActive(GK)!;
        Assert.True(File.Exists(d.Text("playerImage")));
        Assert.False(Failed(await Call(m, "befehl", new JsonObject { ["name"] = "build" })));
        var built = d.Text("built");
        Assert.True(File.Exists(Path.Combine(built, "index.html")));
        var images = Directory.GetFiles(built, "*.png", SearchOption.AllDirectories);
        Assert.NotEmpty(images);
        Assert.Contains(images, f => new FileInfo(f).Length == new FileInfo(d.Text("playerImage")).Length);
        Assert.Empty(JsonNode.Parse(Text(await Call(m, "uebergaben")))!.AsArray());
        Assert.True(d.CanUndo);
        w.Close();
    }

    [AvaloniaFact(DisplayName = "Kette: Falsche Verwendung, falsches Studio und doppelte Annahme werden abgelehnt, nichts wird verbraucht")]
    public async Task Refuses()
    {
        var (w, m, id, _) = await Handed();
        var wrongStudio = await Call(m, "uebernehmen", new JsonObject { ["uebergabe"] = id });
        Assert.True(Failed(wrongStudio)); Assert.Contains("bestimmt", Text(wrongStudio)); // noch im Bildprogramm
        await Call(m, "programm_oeffnen", new JsonObject { ["programm"] = GK, ["vorlage"] = "retro-plattform", ["neu"] = true });
        var wrongUse = await Call(m, "uebernehmen", new JsonObject { ["uebergabe"] = id, ["verwendung"] = "ton-sprung" });
        Assert.True(Failed(wrongUse)); Assert.Contains("passt nicht", Text(wrongUse));
        Assert.Single(JsonNode.Parse(Text(await Call(m, "uebergaben")))!.AsArray());
        Assert.False(Failed(await Call(m, "uebernehmen", new JsonObject { ["uebergabe"] = id })));
        Assert.True(Failed(await Call(m, "uebernehmen", new JsonObject { ["uebergabe"] = id })));
        Assert.True(Failed(await Call(m, "uebergeben", new JsonObject { ["datei"] = "figur.png", ["an_studio"] = "GIBTSNICHT", ["art"] = "x" })));
        w.Close();
    }

    [AvaloniaFact(DisplayName = "Kette: Der Ablauf „Spielfigur → fertiges Spiel“ läuft nur nach Freigabe und liefert Bild, Dokument und spielbares Spiel")]
    public async Task Recipe_runs()
    {
        var (w, _, _) = Ui.Open();
        var recipe = Fusion.Recipes.First(r => r.Id == "figur-spiel");
        Assert.Equal(["Bildbearbeitung", "Spiele bauen", "Spiele bauen", "Qualität"], recipe.Steps.Select(s => s.Studio));
        w.FusionSelect("figur-spiel"); w.FusionSetInput("text", "Sternenspringer");
        Assert.Empty(w.FusionResult);
        w.FusionRequest(); Settle();
        Assert.Single(w.Core.Approvals.Pending());
        Assert.Empty(w.FusionResult); // ohne Freigabe passiert nichts
        w.FusionApproveAndRun(); Settle();
        Assert.All(w.FusionResult, r => Assert.True(r.Ok, r.Label + ": " + r.Detail));
        Assert.Equal(4, w.FusionResult.Count);
        await Task.CompletedTask;
        w.Close();
    }

    [AvaloniaFact(DisplayName = "Kette: Ohne Namen hält der Ablauf mit klarer Meldung an und läuft nicht weiter")]
    public void Recipe_stops()
    {
        var (w, _, _) = Ui.Open();
        w.FusionSelect("figur-spiel"); w.FusionSetInput("text", "  ");
        w.FusionRequest(); w.FusionApproveAndRun(); Settle();
        var only = Assert.Single(w.FusionResult);
        Assert.False(only.Ok); Assert.Contains("Namen", only.Detail);
        w.Close();
    }
}
