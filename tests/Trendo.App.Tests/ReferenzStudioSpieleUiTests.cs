using System.Diagnostics;
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

/// <summary>Referenzstudio „Spiele bauen“ nach Punkt 14 des Auftrags: positive und negative Fälle über den echten Eingabeweg.</summary>
public class ReferenzStudioSpiele_Ui
{
    const string GK = "spiele-programm";
    static async Task<T> Run<T>(Task<T> t) { for (var i = 0; i < 3000 && !t.IsCompleted; i++) { Settle(); await Task.Delay(2); } Settle(); return await t; }
    static async Task<JsonObject> Call(TrendoMcp m, string tool, JsonObject? args = null) =>
        (await Run(m.HandleAsync(new JsonObject { ["jsonrpc"] = "2.0", ["id"] = 1, ["method"] = "tools/call", ["params"] = new JsonObject { ["name"] = tool, ["arguments"] = args ?? [] } })))!["result"]!.AsObject();
    static string Text(JsonObject r) => (string)r["content"]![0]!["text"]!;
    static bool Failed(JsonObject r) => (bool)r["isError"]!;
    static string Folder() { var d = Path.Combine(Path.GetTempPath(), "trendo-referenz-" + Guid.NewGuid().ToString("N")[..8]); Directory.CreateDirectory(d); return d; }
    static T? Find<T>(MainWindow w, string name) where T : Control { Settle(); return w.GetVisualDescendants().OfType<T>().LastOrDefault(c => c.Name == name && c.IsEffectivelyVisible); }
    static List<EngineFound> Engines(bool installed) => EngineTargets.Engines.Select(e => new EngineFound(e.Id, e.Name, installed ? "1.0" : null, installed ? @"C:\x\" + e.Id + ".exe" : null, installed, installed ? "da" : "fehlt", "Bitte installieren")).ToList();
    static (MainWindow W, TrendoMcp M, string Folder) Studio(string? chat = "chat-1")
    {
        var (w, _, _) = Ui.Open(); var folder = Folder();
        w.Show(GK); Settle();
        return (w, w.ClaudeBridgeForTest(folder, GK, chat), folder);
    }
    static async Task<ProgramDocument> Game(MainWindow w, TrendoMcp m)
    {
        Assert.False(Failed(await Call(m, "programm_oeffnen", new JsonObject { ["programm"] = GK, ["vorlage"] = "retro-plattform", ["neu"] = true })));
        return w.ProgramActive(GK)!;
    }

    [AvaloniaFact(DisplayName = "Referenz: Startseite fragt „Was möchtest du machen?“ und zeigt fünf Aufgaben statt Fachbegriffe")]
    public void Start_view()
    {
        var (w, _, _) = Studio();
        var tasks = StudioKnowledgeBase.For("CORE-06")!.Tasks;
        Assert.InRange(tasks.Count, 3, 5);
        foreach (var t in tasks) Assert.NotNull(Find<Button>(w, "studio-task-" + t.Id));
        w.Close();
    }

    [AvaloniaFact(DisplayName = "Referenz (Messung): Vom Start zur ersten sinnvollen Aktion genügt ein Klick")]
    public void One_click_to_start()
    {
        var (w, _, _) = Studio();
        RealClick(w, Find<Button>(w, "studio-task-figur")!); Settle();
        var box = Find<TextBox>(w, "auftrag-prompt")!;
        Assert.StartsWith("Entwirf für mein Spiel eine Figur", box.Text);
        Assert.Equal(box.Text!.Length, box.CaretIndex);
        w.Close();
    }

    [AvaloniaFact(DisplayName = "Referenz (Messung): Studio öffnet unter 1,5 Sekunden")]
    public void Opens_fast()
    {
        var (w, _, _) = Ui.Open();
        var sw = Stopwatch.StartNew(); w.Show(GK); Settle(); sw.Stop();
        Assert.Equal(GK, w.CurrentView);
        Assert.True(sw.ElapsedMilliseconds < 1500, $"{sw.ElapsedMilliseconds} ms");
        w.Close();
    }

    [AvaloniaFact(DisplayName = "Referenz: Claude bekommt das passende Fachwissen zum Auftrag; unbekanntes Studio wird abgelehnt")]
    public async Task Knowledge()
    {
        var (w, m, _) = Studio();
        var k = JsonNode.Parse(Text(await Call(m, "studio_wissen", new JsonObject { ["auftrag"] = "Erstelle einen spielbaren Charakter" })))!;
        Assert.Equal("CORE-06", (string)k["studio"]!);
        Assert.Equal("figur", (string)k["passend"]![0]!);
        Assert.NotEmpty(k["qualitaet"]!.AsArray()); Assert.NotNull(k["grenzen"]);
        Assert.True(Failed(await Call(m, "studio_wissen", new JsonObject { ["studio"] = "GIBTSNICHT" })));
        w.Close();
    }

    [AvaloniaFact(DisplayName = "Referenz: Claude kennt Projekt, Dokument und frühere Entscheidungen")]
    public async Task Context()
    {
        var (w, m, _) = Studio();
        var d = await Game(w, m);
        Assert.False(Failed(await Call(m, "projekt_merken", new JsonObject { ["art"] = "Entscheidung", ["text"] = "Thema Weltraum" })));
        var c = JsonNode.Parse(Text(await Call(m, "projekt_kontext")))!;
        Assert.Equal("CORE-06", (string)c["studio"]!);
        Assert.Equal(d.Title, (string)c["dokument"]!["titel"]!);
        Assert.Contains("Entscheidung: Thema Weltraum", c["gedaechtnis"]!.AsArray().Select(x => (string)x!));
        w.Close();
    }

    [AvaloniaFact(DisplayName = "Referenz: Gedächtnis nimmt nur erlaubte Arten und braucht ein Projekt")]
    public async Task Memory_rules()
    {
        var (w, m, _) = Studio();
        Assert.True(Failed(await Call(m, "projekt_merken", new JsonObject { ["art"] = "Passwort", ["text"] = "x" })));
        var none = w.ClaudeBridgeForTest(Folder(), GK, null);
        Assert.True(Failed(await Call(none, "projekt_merken", new JsonObject { ["art"] = "Problem", ["text"] = "x" })));
        w.Close();
    }

    [AvaloniaFact(DisplayName = "Referenz: Umschalten zwischen Einfach, Erweitert und Profi verändert keine Projektdaten")]
    public async Task Levels_keep_data()
    {
        var (w, m, _) = Studio();
        var d = await Game(w, m);
        var before = d.Data.ToJsonString();
        foreach (var level in new[] { 0, 2, 1, 0 })
        {
            w.ProgramSetLevel(level, GK); Settle();
            Assert.Equal(before, d.Data.ToJsonString());
        }
        w.Close();
    }

    [AvaloniaFact(DisplayName = "Referenz: Alle Befehle sind in jeder Stufe erreichbar, in Einfach über „Mehr“ und über Claude")]
    public async Task All_commands_reachable()
    {
        var (w, m, _) = Studio();
        await Game(w, m);
        foreach (var level in new[] { 0, 2 })
        {
            w.ProgramSetLevel(level, GK); Settle();
            var listed = JsonNode.Parse(Text(await Call(m, "befehle")))!["befehle"]!.AsArray().Select(c => (string)c!["name"]!).ToList();
            Assert.Equal(MainWindow.Programs[GK].Ribbon.SelectMany(g => g.Cmds).Select(c => c.Name), listed);
        }
        w.ProgramSetLevel(0, GK); Settle();
        Assert.False(Failed(await Call(m, "befehl", new JsonObject { ["name"] = "check" })));
        w.Close();
    }

    [AvaloniaFact(DisplayName = "Referenz: Speichern und Laden geben dasselbe Spiel zurück")]
    public async Task Save_load()
    {
        var (w, m, _) = Studio();
        var d = await Game(w, m);
        await Call(m, "dokument_setzen", new JsonObject { ["pfad"] = "title", ["wert"] = "Sternenspringer" });
        Assert.False(Failed(await Call(m, "speichern")));
        var loaded = ProgramDocument.Load(d.Path!, "CORE-06");
        Assert.Equal(d.Data.ToJsonString(), loaded.Data.ToJsonString());
        w.Close();
    }

    [AvaloniaFact(DisplayName = "Referenz: Bauen erzeugt echte Spieldateien mit Titel und Level des Dokuments")]
    public async Task Real_files()
    {
        var (w, m, _) = Studio();
        var d = await Game(w, m);
        await Call(m, "dokument_setzen", new JsonObject { ["pfad"] = "title", ["wert"] = "Sternenspringer" });
        Assert.False(Failed(await Call(m, "befehl", new JsonObject { ["name"] = "build" })));
        var dir = d.Text("built");
        Assert.True(Directory.Exists(dir), dir);
        var html = Path.Combine(dir, "index.html");
        Assert.True(File.Exists(html));
        Assert.Contains("Sternenspringer", File.ReadAllText(html));
        w.Close();
    }

    [AvaloniaFact(DisplayName = "Referenz: Fehlende Programme werden gemeldet, nichts gilt als verbunden")]
    public async Task Missing_programs()
    {
        var (w, m, _) = Studio();
        w.EngineProbeOverride = () => Engines(false);
        var list = JsonNode.Parse(Text(await Call(m, "programme_pruefen")))!.AsArray();
        Assert.Equal(4, list.Count);
        foreach (var p in list) { Assert.False((bool)p!["installiert"]!); Assert.False((bool)p["startet"]!); Assert.Contains("fehlt", (string)p["schnittstelle"]!); Assert.Equal("Bitte installieren", (string)p["hinweis"]!); }
        w.Close();
    }

    [AvaloniaFact(DisplayName = "Referenz: Installiert heißt nicht startbar, Unreal bleibt ungeprüft, MCP gilt erst nach echtem Lauf")]
    public async Task Program_truth()
    {
        var (w, m, _) = Studio();
        w.EngineProbeOverride = () => Engines(true);
        w.ProgramStartsOverride = (id, _) => id switch { "godot" => false, "unreal" => null, _ => true };
        var list = JsonNode.Parse(Text(await Call(m, "programme_pruefen")))!.AsArray().ToDictionary(x => (string)x!["programm"]!, x => x!);
        Assert.False((bool)list["godot"]["startet"]!); Assert.Contains("startet nicht", (string)list["godot"]["schnittstelle"]!);
        Assert.Null(list["unreal"]["startet"]); Assert.Contains("nicht geprüft", (string)list["unreal"]["schnittstelle"]!);
        Assert.True((bool)list["blender"]["startet"]!); Assert.Contains("MCP nicht bestätigt", (string)list["blender"]["schnittstelle"]!);
        Assert.DoesNotContain("MCP verbunden", Text(await Call(m, "programme_pruefen")));
        Assert.Contains("MCP verbunden", ProgramCheck.InterfaceOf("blender", true, true, ["blender"]));
        w.Close();
    }

    [AvaloniaFact(DisplayName = "Referenz: Bei Not-Stopp arbeitet Claude nicht weiter und bekommt den Grund genannt")]
    public async Task Emergency_stop()
    {
        var (w, m, _) = Studio();
        await Game(w, m);
        w.Core.Approvals.Halt(Principal.LocalOwner, "Prüfung");
        var r = await Call(m, "dokument_setzen", new JsonObject { ["pfad"] = "title", ["wert"] = "X" });
        Assert.True(Failed(r)); Assert.Contains("Not-Stopp", Text(r));
        w.Core.Approvals.Resume(Principal.LocalOwner);
        w.Close();
    }

    [AvaloniaFact(DisplayName = "Referenz: Fehler werden verständlich gemeldet, das Dokument bleibt unverändert")]
    public async Task Errors_reported()
    {
        var (w, m, _) = Studio();
        var d = await Game(w, m);
        var before = d.Data.ToJsonString();
        var bad = await Call(m, "dokument_setzen", new JsonObject { ["pfad"] = "levels/99/name", ["wert"] = "X" });
        Assert.True(Failed(bad)); Assert.Contains("gibt es nicht", Text(bad));
        Assert.Equal(before, d.Data.ToJsonString());
        Assert.True(Failed(await Call(m, "befehl", new JsonObject { ["name"] = "gibtsnicht" })));
        Assert.True(Failed(await Call(m, "klicken", new JsonObject())));
        var view = w.CurrentView;
        Assert.True(Failed(await Call(m, "programm_oeffnen", new JsonObject { ["programm"] = "gibtsnicht" })));
        Assert.Equal(view, w.CurrentView); // kein Fehlerfenster durch einen falschen Namen
        w.Close();
    }

    [AvaloniaFact(DisplayName = "Referenz: Dateien entstehen nur im Projektordner, kein Ausbruch nach außen")]
    public async Task Stays_in_project()
    {
        var (w, m, folder) = Studio();
        await Game(w, m);
        var outside = Path.Combine(Path.GetDirectoryName(folder)!, "ausbruch-" + Guid.NewGuid().ToString("N")[..6] + ".md");
        var r = await Call(m, "exportieren", new JsonObject { ["format"] = "bible", ["datei"] = @"..\" + Path.GetFileName(outside) });
        Assert.True(Failed(r)); Assert.False(File.Exists(outside));
        Assert.False(Failed(await Call(m, "exportieren", new JsonObject { ["format"] = "bible", ["datei"] = "bibel.md" })));
        Assert.Equal(["bibel.md"], Directory.GetFiles(folder).Select(Path.GetFileName));
        w.Close();
    }

    [AvaloniaFact(DisplayName = "Referenz: Veröffentlichen läuft nie ohne Zustimmung; Ablehnen ändert nichts")]
    public async Task No_silent_publish()
    {
        var (w, m, _) = Studio();
        var d = await Game(w, m);
        var asked = 0; w.ClaudeConsentOverride = _ => { asked++; return Task.FromResult(false); };
        var pressed = 0;
        var spec = MainWindow.Programs[GK];
        var group = spec.Ribbon[0].Cmds; var extra = new ProgramCmd("test-pub", "Spiel veröffentlichen", _ => pressed++);
        group.Add(extra);
        try
        {
            var r = await Call(m, "befehl", new JsonObject { ["name"] = "test-pub" });
            Assert.True(Failed(r)); Assert.Equal(1, asked); Assert.Equal(0, pressed);
        }
        finally { group.Remove(extra); }
        Assert.NotNull(d);
        w.Close();
    }

    [Fact(DisplayName = "Referenz: Helfer-Studios und Programme der Beschreibung gibt es wirklich")]
    public void Knowledge_is_real()
    {
        var k = StudioKnowledgeBase.For("CORE-06")!;
        Assert.All(k.Helpers, h => Assert.NotNull(CapabilityCatalog.Find(h)));
        Assert.All(k.Programs, p => Assert.Contains(EngineTargets.Engines, e => e.Id == p));
        Assert.All(k.Tasks, t => Assert.StartsWith(t.Starter, t.Starter.TrimEnd() + " "));
        Assert.Contains(k.Formats, f => f == ProgramDocument.Extension);
    }

    [Theory(DisplayName = "Referenz: Auftrag in Alltagssprache führt zur passenden Aufgabe")]
    [InlineData("Baue mir ein Jump and Run über Piraten", "spiel-neu")]
    [InlineData("Mach ein neues Level mit Lava", "level")]
    [InlineData("Erstelle einen spielbaren Charakter", "figur")]
    [InlineData("Schreib mir eine Quest über einen verlorenen Ring", "mission")]
    [InlineData("Da ist ein Fehler, bitte prüfen", "pruefen")]
    public void Intent(string request, string task) => Assert.Equal(task, StudioKnowledgeBase.Match(StudioKnowledgeBase.For("CORE-06")!, request)[0].Id);

    [Fact(DisplayName = "Referenz: Ohne ausführliche Beschreibung bleibt das Studio ehrlich als nicht ausführlich markiert")]
    public void Derived_is_marked()
    {
        var k = StudioKnowledgeBase.For("SPEC-07")!;
        Assert.False(k.Detailed); Assert.Empty(k.Tasks);
        Assert.Null(StudioKnowledgeBase.For("GIBTSNICHT"));
        Assert.Empty(StudioKnowledgeBase.Match(k, "irgendwas"));
    }
}
