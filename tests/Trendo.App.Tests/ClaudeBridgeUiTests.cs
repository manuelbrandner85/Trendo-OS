using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Trendo.Api;
using Trendo.Core;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// <summary>Freigabe 09.11.2026: Claude bedient jedes Programm selbst, ohne Klicks des Nutzers.</summary>
public class ClaudeBridge_Ui
{
    static async Task<T> Run<T>(Task<T> t) { for (var i = 0; i < 3000 && !t.IsCompleted; i++) { Settle(); await Task.Delay(2); } Settle(); return await t; }

    static async Task<JsonObject> Call(TrendoMcp m, string tool, JsonObject? args = null) =>
        (await Run(m.HandleAsync(new JsonObject { ["jsonrpc"] = "2.0", ["id"] = 1, ["method"] = "tools/call", ["params"] = new JsonObject { ["name"] = tool, ["arguments"] = args ?? [] } })))!["result"]!.AsObject();
    static string Text(JsonObject r) => (string)r["content"]![0]!["text"]!;
    static bool Failed(JsonObject r) => (bool)r["isError"]!;
    static string Folder() { var d = Path.Combine(Path.GetTempPath(), "trendo-bruecke-" + Guid.NewGuid().ToString("N")[..8]); Directory.CreateDirectory(d); return d; }

    [AvaloniaFact(DisplayName = "Claude-Zugang: Programm öffnen, Befehle auch versteckte, Dokument lesen und ändern, Export, Speichern, Rückgängig")]
    public async Task Works_in_program()
    {
        var (w, _, _) = Ui.Open(); var folder = Folder();
        var m = w.ClaudeBridgeForTest(folder);
        var all = JsonNode.Parse(Text(await Call(m, "programme")))!;
        Assert.True(all["programme"]!.AsArray().Count >= 12);
        Assert.True(all["seiten"]!.AsArray().Count >= 20);
        Assert.False(Failed(await Call(m, "programm_oeffnen", new JsonObject { ["programm"] = "technik", ["vorlage"] = "einfeld" })));
        Assert.Equal("technik", w.CurrentView);
        var cmds = JsonNode.Parse(Text(await Call(m, "befehle")))!;
        Assert.Contains(cmds["befehle"]!.AsArray(), c => (string)c!["name"]! == "tk-3d");
        Assert.False(Failed(await Call(m, "befehl", new JsonObject { ["name"] = "tk-3d" })));
        Settle(); Assert.Contains(w.GetVisualDescendants().OfType<Control>(), c => c.Name == "tk-3d-view");

        var data = await Call(m, "dokument_lesen");
        Assert.False(Failed(data));
        var doc = w.ProgramActive("technik")!;
        Assert.False(Failed(await Call(m, "dokument_setzen", new JsonObject { ["pfad"] = "claude/notiz", ["wert"] = "Hallo" })));
        Assert.Equal("Hallo", (string)doc.Data["claude"]!["notiz"]!);
        Assert.Equal("Hallo", Text(await Call(m, "dokument_lesen", new JsonObject { ["pfad"] = "claude/notiz" })).Trim('"'));
        Assert.False(Failed(await Call(m, "rueckgaengig")));
        Assert.Null(doc.Data["claude"]);
        Assert.False(Failed(await Call(m, "speichern")));
        Assert.StartsWith(Path.GetFullPath(folder), doc.Path!);
        w.ProgramSave("technik", "NurEinName.tdoc");
        Assert.Equal(Path.Combine(Path.GetFullPath(folder), "NurEinName.tdoc"), doc.Path); // nie ins Arbeitsverzeichnis des Prozesses
        Assert.True(Failed(await Call(m, "befehl", new JsonObject { ["name"] = "gibtsnicht" })));
        Assert.True(Failed(await Call(m, "programm_oeffnen", new JsonObject { ["programm"] = "technik", ["vorlage"] = "gibtsnicht", ["neu"] = true })));
        w.Close();
    }

    [AvaloniaFact(DisplayName = "Claude-Zugang: Seite lesen, Feld füllen, auswählen, Knopf drücken über Name oder Beschriftung, Geheimnisse bleiben draußen")]
    public async Task Works_on_page()
    {
        var (w, _, _) = Ui.Open(); var folder = Folder();
        w.Show("cnc"); Settle(); RealClick(w, w.GetVisualDescendants().OfType<Button>().Last(b => b.Name == "prog-tpl-leer" && b.IsEffectivelyVisible)); Settle();
        var m = w.ClaudeBridgeForTest(folder, "cnc");
        var page = JsonNode.Parse(Text(await Call(m, "seite_lesen")))!;
        Assert.Contains(page["elemente"]!.AsArray(), e => (string?)e!["name"] == "cnc-editor" && (string)e["art"]! == "feld");
        Assert.False(Failed(await Call(m, "feld_setzen", new JsonObject { ["ziel"] = "cnc-editor", ["wert"] = "G0 X1" })));
        Assert.Equal("G0 X1", w.GetVisualDescendants().OfType<TextBox>().Last(t => t.Name == "cnc-editor").Text);
        Assert.False(Failed(await Call(m, "klicken", new JsonObject { ["ziel"] = "prog-cmd-cnc-v-stock" })));
        Assert.True(Failed(await Call(m, "klicken", new JsonObject { ["ziel"] = "gibtsnicht" })));
        w.Close();
    }

    [AvaloniaFact(DisplayName = "Nachfrage: Wirkung nach außen fragt, Nein bricht ab und nennt den Grund; Bearbeiten fragt nicht")]
    public async Task Consent()
    {
        var (w, _, _) = Ui.Open(); var folder = Folder();
        var asked = new List<string>(); var answer = false;
        w.ClaudeConsentOverride = q => { asked.Add(q); return Task.FromResult(answer); };
        var pressed = 0;
        w.Show("technik"); Settle(); RealClick(w, w.GetVisualDescendants().OfType<Button>().Last(b => b.Name == "prog-tpl-einfeld" && b.IsEffectivelyVisible)); Settle();
        var root = w.GetVisualDescendants().OfType<StackPanel>().First(p => p.IsEffectivelyVisible && p.Bounds.Width > 0 && p.FindAncestorOfType<Border>()?.Name == "prog-workspace");
        var send = new Button { Name = "test-send", Content = "Jetzt versenden" }; send.Click += (_, _) => pressed++; root.Children.Add(send); Settle();
        var m = w.ClaudeBridgeForTest(folder, "technik");
        var no = await Call(m, "klicken", new JsonObject { ["ziel"] = "test-send" });
        Assert.True(Failed(no)); Assert.Contains("Nicht bestätigt", Text(no)); Assert.Equal(0, pressed); Assert.Single(asked);
        answer = true;
        Assert.False(Failed(await Call(m, "klicken", new JsonObject { ["ziel"] = "test-send" })));
        Assert.Equal(1, pressed);
        asked.Clear();
        Assert.False(Failed(await Call(m, "befehl", new JsonObject { ["name"] = "tk-3d" }))); Assert.Empty(asked);
        Assert.Empty(w.Core.Approvals.Pending());
        // Offene Freigabe: Claude darf sie nicht selbst erteilen, auch nicht über einen harmlos benannten Knopf
        var open = w.Core.Approvals.Request(Principal.LocalOwner, "test.bewegung", "Roboter bewegen", "ziel");
        answer = false; asked.Clear();
        var harmless = new Button { Name = "test-weiter", Content = "Weiter" }; harmless.Click += (_, _) => pressed += 100;
        w.GetVisualDescendants().OfType<StackPanel>().First(p => p.IsEffectivelyVisible && p.Bounds.Width > 0 && p.FindAncestorOfType<Border>()?.Name == "prog-workspace").Children.Add(harmless); Settle(); // die Seite wurde inzwischen neu aufgebaut
        Assert.True(Failed(await Call(m, "klicken", new JsonObject { ["ziel"] = "test-weiter" })));
        Assert.True(Failed(await Call(m, "befehl", new JsonObject { ["name"] = "tk-3d" })));
        Assert.Equal(2, asked.Count); Assert.True(pressed < 100);
        w.Core.Approvals.Deny(open.Id, Principal.LocalOwner);
        answer = true;
        w.Core.Approvals.Halt(Principal.LocalOwner, "Test");
        var halted = await Call(m, "befehl", new JsonObject { ["name"] = "tk-3d" });
        Assert.True(Failed(halted)); Assert.Contains("Not-Stopp", Text(halted));
        w.Core.Approvals.Resume(Principal.LocalOwner);
        w.Close();
    }

    [AvaloniaFact(DisplayName = "Ohne Klicks: Änderung bleibt, keine Behalten-Frage, nur Zurücknehmen; das holt den alten Stand zurück")]
    public async Task No_clicks()
    {
        var (w, _, _) = Ui.Open(); var folder = Folder();
        w.ClaudeNoClicks = true;
        var m = w.ClaudeBridgeForTest(folder);
        await Call(m, "programm_oeffnen", new JsonObject { ["programm"] = "technik", ["vorlage"] = "einfeld" });
        await Call(m, "dokument_setzen", new JsonObject { ["pfad"] = "claude/notiz", ["wert"] = "Hallo" });
        Settle();
        var doc = w.ProgramActive("technik")!;
        Assert.True(w.ClaudeChangePending("technik"));
        Button? Named(string n) => w.GetVisualDescendants().OfType<Button>().LastOrDefault(b => b.Name == n && b.IsEffectivelyVisible);
        Assert.Null(Named("claude-change-keep"));
        Assert.NotNull(Named("claude-change-drop"));
        Assert.Equal("Hallo", (string)doc.Data["claude"]!["notiz"]!);
        RealClick(w, Named("claude-change-drop")!); Settle();
        Assert.Null(doc.Data["claude"]);
        w.Close();
    }

    [AvaloniaFact(DisplayName = "Jedes Programm: Claude sieht alle Befehle aller Stufen, die Seite und die Daten")]
    public async Task Every_program()
    {
        var (w, _, _) = Ui.Open(); var folder = Folder();
        var m = w.ClaudeBridgeForTest(folder);
        foreach (var key in MainWindow.Programs.Keys.ToList())
        {
            var spec = MainWindow.Programs[key];
            var opened = await Call(m, "programm_oeffnen", new JsonObject { ["programm"] = key, ["neu"] = true });
            Assert.False(Failed(opened), key + ": " + Text(opened));
            var listed = JsonNode.Parse(Text(await Call(m, "befehle", new JsonObject { ["programm"] = key })))!;
            Assert.Equal(spec.Ribbon.Sum(g => g.Cmds.Count), listed["befehle"]!.AsArray().Count);
            var names = listed["befehle"]!.AsArray().Select(c => (string)c!["name"]!).ToList();
            Assert.True(names.Distinct().Count() == names.Count, key + ": doppelte Befehlsnamen " + string.Join(", ", names.GroupBy(n => n).Where(g => g.Count() > 1).Select(g => g.Key)));
            Assert.False(Failed(await Call(m, "dokument_lesen", new JsonObject { ["programm"] = key })), key);
            Assert.False(Failed(await Call(m, "seite_lesen")), key);
        }
        w.Close();
    }

    [AvaloniaFact(DisplayName = "Jede Studio-Seite: Claude kann sie öffnen und lesen")]
    public async Task Every_page()
    {
        var (w, _, _) = Ui.Open(); var folder = Folder();
        var m = w.ClaudeBridgeForTest(folder);
        foreach (var page in MainWindow.StudioPages.Values.Distinct().ToList())
        {
            var opened = await Call(m, "programm_oeffnen", new JsonObject { ["programm"] = page });
            Assert.False(Failed(opened), page + ": " + Text(opened));
            Assert.Equal(page, w.CurrentView);
        }
        w.Close();
    }

    [Fact(DisplayName = "Claude-Zugang: nur mit Zufalls-Kennzeichen, nur auf diesem PC, antwortet nach MCP-Art")]
    public async Task Host()
    {
        var mcp = new TrendoMcp([new McpTool("eins", "x", TrendoMcp.Schema(), _ => Task.FromResult(new McpResult("gut")))]);
        await using var host = await ClaudeBridgeHost.StartAsync(mcp);
        var cfg = JsonNode.Parse(host.ConfigJson())!["mcpServers"]!["trendo"]!;
        var url = (string)cfg["url"]!;
        Assert.StartsWith("http://127.0.0.1:", url);
        Assert.Equal("http", (string)cfg["type"]!);
        using var http = new HttpClient();
        StringContent Body(string s) => new(s, Encoding.UTF8, "application/json");
        const string List = """{"jsonrpc":"2.0","id":1,"method":"tools/list"}""";
        Assert.Equal(401, (int)(await http.PostAsync(url, Body(List), TestContext.Current.CancellationToken)).StatusCode);
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "falsch");
        Assert.Equal(401, (int)(await http.PostAsync(url, Body(List), TestContext.Current.CancellationToken)).StatusCode);
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", host.Token);
        var ok = await http.PostAsync(url, Body(List), TestContext.Current.CancellationToken);
        Assert.Equal(200, (int)ok.StatusCode);
        Assert.Contains("\"eins\"", await ok.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal(202, (int)(await http.PostAsync(url, Body("""{"jsonrpc":"2.0","method":"notifications/initialized"}"""), TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(400, (int)(await http.PostAsync(url, Body("{kaputt"), TestContext.Current.CancellationToken)).StatusCode);
    }
}
