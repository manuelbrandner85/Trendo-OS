using System.Text.Json.Nodes;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Trendo.App;
using Trendo.Core;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>T-0620: KI-Grundlage in „Webseite bauen“ – Klickwege mit nachgebildeter KI, dazu die Prüfung der echten Seite im echten Browser.</summary>
public class WebDesignKi_Ui : IDisposable
{
    const string NewPagePlan = """{"aktionen":[{"aktion":"seite_anlegen","titel":"Café Muster","beschreibung":"Guter Kaffee"},{"aktion":"baustein_einfuegen","typ":"Kopfbereich","id":"held","props":{"title":"Guter Kaffee","sub":"Täglich frisch"}},{"aktion":"baustein_einfuegen","typ":"Text","id":"t1","props":{"text":"Unsere Bohnen"}},{"aktion":"baustein_einfuegen","typ":"Text","id":"t2","props":{"text":"Öffnungszeiten"}}]}""";
    static void Pump() { for (var i = 0; i < 8; i++) Dispatcher.UIThread.RunJobs(); }
    static async Task Until(Func<bool> ok, int ms = 6000) { for (var i = 0; i < ms / 20 && !ok(); i++) { Dispatcher.UIThread.RunJobs(); await Task.Delay(20); } Assert.True(ok()); }
    static WebAuditResult Ok(string json, string dir) => new(WebDesignCheck.CheckData(json, dir), [], "");

    public void Dispose()
    {
        MainWindow.WebDesignKiOverride = null; MainWindow.WebDesignVisionOverride = null; MainWindow.WebDesignAuditOverride = null; MainWindow.WebDesignKiStateOverride = null; MainWindow.WebDesignEditorDirOverride = null;
        GC.SuppressFinalize(this);
    }

    static string PageFile(MainWindow w) => Path.Combine(w.WebDesignDir, WebDesign.PageFile);
    internal static string Repo()
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d is not null; d = d.Parent) if (File.Exists(Path.Combine(d.FullName, "Trendo.sln"))) return d.FullName;
        throw new DirectoryNotFoundException();
    }

    [AvaloniaFact(DisplayName = "K4: Satz → Vorschau ohne Schreiben → Übernehmen als ein Schritt → Rückgängig; Editor-Daten statt Dateien")]
    public async Task K4_sentence_to_page()
    {
        var (w, _, _) = Ui.Open(); var asked = new List<(string Sys, string Prompt)>();
        const string directions = """{"richtungen":[{"name":"Ruhig","stimmung":"Klar.","schrift":"klar","akzent":"#1f4e79","text":"#1d1d1f","hintergrund":"#ffffff","aufbau":["Hero","Text"]},{"name":"Warm","stimmung":"Herzlich.","schrift":"serif","akzent":"#7a1f2b","text":"#2b1d14","hintergrund":"#fbf6ee","aufbau":["Hero","Galerie"]}]}""";
        MainWindow.WebDesignKiOverride = (s, p) => { if (s.Contains("Gestaltungsrichtungen")) return Task.FromResult(directions); asked.Add((s, p)); return Task.FromResult(NewPagePlan); };
        w.Show("dev"); Pump();
        w.Find<TextBox>("web-ki-wish").Text = "Eine Seite für ein Café";
        w.Find<Button>("web-ki-new").Press(); Pump();
        await Until(() => w.WebDesignDirectionsShown is not null); Pump(); // A8: erst Richtungen, noch nichts gebaut
        Assert.Empty(asked); Assert.Null(w.WebDesignPending);
        w.Find<Button>("web-ki-richtung-0").Press(); Pump();
        await Until(() => w.WebDesignPending is not null);
        Pump();
        Assert.Contains("Ruhig", asked[0].Prompt);
        Assert.Contains("aktionen", asked[0].Sys); Assert.Contains("baustein_einfuegen", asked[0].Sys); Assert.Contains("Eine Seite für ein Café", asked[0].Prompt);
        Assert.False(File.Exists(PageFile(w))); // nur Vorschau
        Assert.NotNull(w.Find<StackPanel>("web-ki-preview")); Assert.Contains(w.GetLogicalTexts(), t => t.Contains("Kopfbereich eingefügt"));
        Assert.NotNull(w.Find<StackPanel>("web-ki-check-result"));
        w.Find<Button>("web-ki-accept").Press(); Pump();
        var doc = JsonNode.Parse(File.ReadAllText(PageFile(w)))!;
        Assert.Equal("Guter Kaffee", doc["content"]![0]!["props"]!["title"]!.ToString());
        Assert.Null(w.WebDesignPending);
        var state = WebDesignStates.List(w.WebDesignDir).Single();
        Assert.True(state.Auto); Assert.StartsWith("Vor KI:", state.Name);
        w.Find<Button>("web-ki-undo").Press(); Pump();
        Assert.Empty(JsonNode.Parse(File.ReadAllText(PageFile(w)))!["content"]!.AsArray());
    }

    [AvaloniaFact(DisplayName = "K4: Ungültige KI-Antwort wird gemeldet, nichts gelangt in die Seite")]
    public async Task K4_invalid_answer()
    {
        var (w, _, _) = Ui.Open();
        MainWindow.WebDesignKiOverride = (_, _) => Task.FromResult("<html><body>fertig</body></html>");
        w.Show("dev"); Pump();
        await w.WebDesignAiRun("Eine Seite"); Pump();
        Assert.Contains("abgelehnt", w.WebDesignNote); Assert.Null(w.WebDesignPending); Assert.False(File.Exists(PageFile(w)));
    }

    [AvaloniaFact(DisplayName = "K4 und A7: Bild ohne bildfähige KI ergibt einen Hinweis statt eines Fehlers, nichts wird gefragt; mit bildfähiger KI fließt das Bild in den Auftrag")]
    public async Task K4_image()
    {
        var (w, _, root) = Ui.Open(); var asked = new List<string>();
        MainWindow.WebDesignKiOverride = (_, p) => { asked.Add(p); return Task.FromResult(NewPagePlan); };
        MainWindow.WebDesignKiStateOverride = (false, true);
        var png = Path.Combine(root, "vorlage.png"); File.WriteAllBytes(png, [137, 80, 78, 71]);
        w.Show("dev"); Pump();
        await w.WebDesignAiRun("So wie auf dem Bild", png); Pump();
        Assert.Contains("Bilder sehen", w.WebDesignNote); Assert.Empty(asked); Assert.Null(w.WebDesignPending);
        Assert.Contains(w.GetLogicalTexts(), t => t.Contains("Bilder sehen"));
        MainWindow.WebDesignVisionOverride = (file, _) => Task.FromResult<string?>(file == png ? "Zwei Abschnitte, Farbe #AA3300" : "falsch");
        await w.WebDesignAiRun("So wie auf dem Bild", png); Pump();
        Assert.Contains("Zwei Abschnitte", asked.Single()); Assert.NotNull(w.WebDesignPending);
    }

    sealed class LocalKi : IKiBackend
    {
        public string Name => "Ollama"; public string BaseUrl => "http://127.0.0.1:11434";
        public Task<bool> IsUpAsync(CancellationToken ct = default) => Task.FromResult(true);
        public Task<IReadOnlyList<string>> ModelsAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<string>>(["klein"]);
        public Task<string> ChatAsync(string model, IReadOnlyList<KiMessage> messages, CancellationToken ct = default) => Task.FromResult(NewPagePlan);
    }
    sealed class DownClaude : IMainKiBackend
    {
        public string Name => "Claude"; public string BaseUrl => "claude-code";
        public Task<bool> IsUpAsync(CancellationToken ct = default) => Task.FromResult(false);
        public Task<IReadOnlyList<string>> ModelsAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<string>>(["claude"]);
        public Task<string> ChatAsync(string model, IReadOnlyList<KiMessage> messages, CancellationToken ct = default) => throw new InvalidOperationException("nicht verbunden");
    }

    sealed class PaidApi : IRemoteKiBackend
    {
        public int Calls;
        public string Name => "Bezahl-API"; public string BaseUrl => "https://api.example";
        public Task<bool> IsUpAsync(CancellationToken ct = default) => Task.FromResult(true);
        public Task<IReadOnlyList<string>> ModelsAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<string>>(["x"]);
        public Task<string> ChatAsync(string model, IReadOnlyList<KiMessage> messages, CancellationToken ct = default) { Calls++; return Task.FromResult("nein"); }
    }

    [AvaloniaFact(DisplayName = "A7: Ohne Claude läuft die Textaufgabe lokal und die Oberfläche nennt die KI; ohne jede KI ein klarer Hinweis")]
    public async Task A7_local_and_none()
    {
        var (w, _, _) = Ui.Open(); var api = new PaidApi();
        w.KiBackendsOverride = () => [new DownClaude(), api, new LocalKi()];
        w.Show("dev"); Pump();
        Assert.Contains("KI auf diesem PC", w.Find<TextBlock>("web-ki-who").Text);
        await w.WebDesignAiRun("Eine Seite"); Pump();
        Assert.Contains(WebDesignKi.LocalLabel, w.WebDesignKiWho); Assert.Contains("klein", w.WebDesignKiWho); Assert.NotNull(w.WebDesignPending); Assert.Equal(0, api.Calls);
        w.KiBackendsOverride = () => [new DownClaude()];
        w.WebDesignDiscard();
        await w.WebDesignAiRun("Eine Seite"); Pump();
        Assert.Contains("keine KI", w.WebDesignNote); Assert.Null(w.WebDesignPending);
    }

    static async Task<(MainWindow W, string Before)> WithPage()
    {
        var (w, _, _) = Ui.Open();
        MainWindow.WebDesignKiOverride = (_, _) => Task.FromResult(NewPagePlan);
        w.Show("dev"); Pump();
        await w.WebDesignAiRun("Seite"); Pump(); w.WebDesignAccept(); Pump();
        return (w, File.ReadAllText(PageFile(w)));
    }

    [AvaloniaFact(DisplayName = "K1: Auswahl aus der Vorschau, Auftrag ändert nur sie, die KI sieht nur sie; Verwerfen lässt die Seite gleich, Übernehmen ist ein Rückgängig-Schritt")]
    public async Task K1_selection()
    {
        var (w, before) = await WithPage(); var asked = "";
        MainWindow.WebDesignKiOverride = (_, p) => { asked = p; return Task.FromResult("""{"aktionen":[{"aktion":"element_aendern","id":"t1","props":{"text":"Beste Bohnen"}}]}"""); };
        w.WebDesignMessage("""{"art":"auswahl","daten":{"ids":["t1"]}}"""); Pump();
        Assert.Equal(["t1"], w.WebDesignSelection);
        Assert.Contains("(1)", w.Find<Button>("web-ki-change").Content?.ToString());
        await w.WebDesignAiChange("Kürzer"); Pump();
        Assert.Contains("\"t1\"", asked); Assert.DoesNotContain("Öffnungszeiten", asked);
        w.Find<Button>("web-ki-discard").Press(); Pump();
        Assert.Equal(before, File.ReadAllText(PageFile(w)));
        await w.WebDesignAiChange("Kürzer"); Pump();
        w.Find<Button>("web-ki-accept").Press(); Pump();
        var c = JsonNode.Parse(File.ReadAllText(PageFile(w)))!["content"]!.AsArray();
        Assert.Equal("Beste Bohnen", c[1]!["props"]!["text"]!.ToString());
        Assert.Equal("Öffnungszeiten", c[2]!["props"]!["text"]!.ToString()); Assert.Equal("Guter Kaffee", c[0]!["props"]!["title"]!.ToString());
        w.WebDesignUndo(); Pump();
        Assert.Equal(before, File.ReadAllText(PageFile(w)));
    }

    [AvaloniaFact(DisplayName = "K1: Auftrag außerhalb der Auswahl wird abgelehnt; ohne Auswahl ein Hinweis")]
    public async Task K1_rejected()
    {
        var (w, before) = await WithPage();
        await w.WebDesignAiChange("Etwas"); Pump();
        Assert.Contains("Wähle zuerst", w.WebDesignNote);
        MainWindow.WebDesignKiOverride = (_, _) => Task.FromResult("""{"aktionen":[{"aktion":"element_aendern","id":"t2","props":{"text":"Fremd"}}]}""");
        w.WebDesignSetSelection("t1");
        await w.WebDesignAiChange("Etwas"); Pump();
        Assert.Contains("nicht zur Auswahl", w.WebDesignNote); Assert.Null(w.WebDesignPending); Assert.Equal(before, File.ReadAllText(PageFile(w)));
    }

    [AvaloniaFact(DisplayName = "K2: Text direkt ändern: gespeicherte Daten enthalten den neuen Text, ohne KI")]
    public async Task K2_direct_text()
    {
        var (w, _) = await WithPage(); var calls = 0;
        MainWindow.WebDesignKiOverride = (_, _) => { calls++; return Task.FromResult(""); };
        w.WebDesignMessage("""{"art":"aktion","daten":{"aktionen":[{"aktion":"element_aendern","id":"t2","props":{"text":"Mo bis Fr 8–18 Uhr"}}]}}"""); Pump();
        Assert.Equal("Mo bis Fr 8–18 Uhr", JsonNode.Parse(WebDesign.Load(w.WebDesignDir))!["content"]![2]!["props"]!["text"]!.ToString());
        Assert.Equal(0, calls); Assert.StartsWith("Gespeichert", w.WebDesignNote);
        w.WebDesignMessage("""{"art":"aktion","daten":{"aktionen":[{"aktion":"datei_loeschen"}]}}"""); Pump();
        Assert.Contains("unbekannte Aktion", w.WebDesignNote);
    }

    [AvaloniaFact(DisplayName = "Editor-Entwurf ist die Grundlage der KI: ungespeicherte Änderungen gehen nicht verloren und werden erst beim Übernehmen geschrieben")]
    public async Task Draft_is_base()
    {
        var (w, before) = await WithPage(); var asked = "";
        var draft = before.Replace("Unsere Bohnen", "Ungespeichert");
        w.WebDesignMessage("{\"art\":\"entwurf\",\"daten\":" + draft + "}"); Pump();
        Assert.Equal(before, File.ReadAllText(PageFile(w)));
        MainWindow.WebDesignKiOverride = (_, p) => { asked = p; return Task.FromResult("""{"aktionen":[{"aktion":"element_aendern","id":"t2","props":{"text":"Neu"}}]}"""); };
        w.WebDesignSetSelection("t1", "t2");
        await w.WebDesignAiChange("Etwas"); Pump();
        Assert.Contains("Ungespeichert", asked);
        w.WebDesignAccept(); Pump();
        Assert.Contains("Ungespeichert", File.ReadAllText(PageFile(w)));
    }

    [AvaloniaFact(DisplayName = "K5: Stand benennen und wiederherstellen; Variante anlegen, öffnen, Original bleibt, übernehmen")]
    public async Task K5_ui()
    {
        var (w, before) = await WithPage();
        w.Find<TextBox>("web-stand-name").Text = "Erster Entwurf";
        w.Find<Button>("web-stand-save").Press(); Pump();
        var named = WebDesignStates.List(w.WebDesignDir).First(s => s.Name == "Erster Entwurf");
        w.WebDesignMessage("{\"art\":\"speichern\",\"daten\":" + before.Replace("Unsere Bohnen", "Anders") + "}"); Pump();
        w.WebDesignStandRestore(named.Id); Pump();
        Assert.Equal(before, File.ReadAllText(PageFile(w)));

        w.Find<TextBox>("web-var-name").Text = "Dunkel";
        w.Find<Button>("web-var-new").Press(); Pump();
        w.Find<Button>("web-var-open-0").Press(); Pump();
        Assert.NotNull(w.WebDesignVariant); Assert.NotNull(w.Find<Button>("web-var-back"));
        w.WebDesignMessage("{\"art\":\"speichern\",\"daten\":" + before.Replace("Unsere Bohnen", "Variante") + "}"); Pump();
        Assert.Equal(before, File.ReadAllText(PageFile(w)));
        w.Find<Button>("web-var-adopt-0").Press(); Pump();
        Assert.Contains("Variante", File.ReadAllText(PageFile(w))); Assert.Null(w.WebDesignVariant);
    }

    [AvaloniaFact(DisplayName = "Kein Datenverlust: Wechsel der Fassung verwirft eine offene KI-Vorschau, die Variante bleibt unberührt")]
    public async Task Preview_bound_to_target()
    {
        var (w, before) = await WithPage();
        w.WebDesignVariantCreate("A"); Pump();
        var id = WebDesignStates.Variants(w.WebDesignDir).Single().Id;
        var variantBefore = WebDesignStates.VariantContent(w.WebDesignDir, id);
        await w.WebDesignAiRun("Neue Fassung"); Pump();
        Assert.NotNull(w.WebDesignPending);
        w.WebDesignVariantOpen(id); Pump();
        Assert.Null(w.WebDesignPending);
        w.WebDesignAccept(); Pump();
        Assert.Equal(variantBefore, WebDesignStates.VariantContent(w.WebDesignDir, id)); Assert.Equal(before, File.ReadAllText(PageFile(w)));

        w.WebDesignVariantOpen(null); Pump();
        await w.WebDesignAiRun("Neue Fassung"); Pump();
        var named = WebDesignStates.SaveContent(w.WebDesignDir, "Alt", before).Stand!;
        w.WebDesignStandRestore(named.Id); Pump();
        Assert.Null(w.WebDesignPending); // Wiederherstellen macht die Vorschau hinfällig
    }

    [AvaloniaFact(DisplayName = "Kein Datenverlust: Solange die KI arbeitet, bleibt das Ziel fest; eine Änderung in der Zwischenzeit verwirft den Vorschlag für die Auswahl")]
    public async Task Busy_keeps_target()
    {
        var (w, before) = await WithPage();
        w.WebDesignVariantCreate("A"); Pump();
        var id = WebDesignStates.Variants(w.WebDesignDir).Single().Id;
        var gate = new TaskCompletionSource<string>();
        MainWindow.WebDesignKiOverride = (_, _) => gate.Task;
        w.WebDesignSetSelection("t1");
        var run = w.WebDesignAiChange("Etwas"); Pump();
        w.WebDesignVariantOpen(id); Pump();
        Assert.Null(w.WebDesignVariant); Assert.Contains("arbeitet noch", w.WebDesignNote);
        var second = 0;
        MainWindow.WebDesignKiOverride = (_, _) => { second++; return gate.Task; };
        await w.WebDesignAiChange("Doppelt"); Pump();
        Assert.Equal(0, second); // kein Doppelstart
        w.WebDesignMessage("{\"art\":\"entwurf\",\"daten\":" + before.Replace("Unsere Bohnen", "Zwischendurch") + "}"); Pump();
        gate.SetResult("""{"aktionen":[{"aktion":"element_aendern","id":"t1","props":{"text":"KI"}}]}""");
        await run; Pump();
        Assert.Null(w.WebDesignPending); Assert.Contains("verworfen", w.WebDesignNote);
    }

    [AvaloniaFact(DisplayName = "Sperre löst sich: Auch wenn die Seite nicht lesbar ist, bleibt die KI nicht für immer blockiert")]
    public async Task Busy_released_after_failure()
    {
        var (w, _) = await WithPage();
        using (new FileStream(PageFile(w), FileMode.Open, FileAccess.Read, FileShare.None))
        {
            await w.WebDesignAiRun("Neue Fassung"); Pump();
            Assert.DoesNotContain("arbeitet", w.WebDesignNote); Assert.Null(w.WebDesignPending);
        }
        await w.WebDesignAiRun("Neue Fassung"); Pump();
        Assert.NotNull(w.WebDesignPending);
    }

    [AvaloniaFact(DisplayName = "Kein Datenverlust: Ungespeicherter Entwurf landet vor Wiederherstellen und Fassungswechsel in einem Stand; benannte Stände bleiben sichtbar")]
    public async Task Draft_is_kept()
    {
        var (w, before) = await WithPage();
        var named = WebDesignStates.SaveContent(w.WebDesignDir, "Mein Stand", before).Stand!;
        w.WebDesignMessage("{\"art\":\"entwurf\",\"daten\":" + before.Replace("Unsere Bohnen", "Entwurf eins") + "}"); Pump();
        w.WebDesignStandRestore(named.Id); Pump();
        Assert.Contains(WebDesignStates.List(w.WebDesignDir), s => s.Auto && WebDesignStates.Content(w.WebDesignDir, s.Id)!.Contains("Entwurf eins"));
        w.WebDesignVariantCreate("A"); Pump();
        w.WebDesignMessage("{\"art\":\"entwurf\",\"daten\":" + before.Replace("Unsere Bohnen", "Entwurf zwei") + "}"); Pump();
        w.WebDesignVariantOpen(WebDesignStates.Variants(w.WebDesignDir).Single().Id); Pump();
        Assert.Contains(WebDesignStates.List(w.WebDesignDir), s => s.Auto && WebDesignStates.Content(w.WebDesignDir, s.Id)!.Contains("Entwurf zwei"));
        w.WebDesignVariantOpen(null); Pump();
        for (var i = 0; i < 12; i++) WebDesignStates.SaveContent(w.WebDesignDir, "Auto", before, true);
        w.Show("dev"); Pump();
        Assert.Contains(w.GetLogicalTexts(), t => t.Contains("Mein Stand"));
    }

    [AvaloniaFact(DisplayName = "A6: Eingebauter Fehler wird gemeldet und gezielt nachgebessert; die Obergrenze gilt; Bildbewertung ist nur ein Hinweis")]
    public async Task A6_loop_in_ui()
    {
        var (w, _, _) = Ui.Open(); var repairs = 0;
        MainWindow.WebDesignKiOverride = (s, p) =>
        {
            if (!p.Contains("Prüfung der fertigen Seite")) return Task.FromResult("""{"aktionen":[{"aktion":"seite_anlegen","titel":"Seite","beschreibung":"Text"},{"aktion":"baustein_einfuegen","typ":"Kopfbereich","id":"k","props":{"title":"Titel","sub":"Echter Text","href":"#fehlt"}}]}""");
            repairs++; return Task.FromResult("{\"aktionen\":[{\"aktion\":\"element_aendern\",\"id\":\"k\",\"props\":{\"href\":\"#\",\"sub\":\"Fassung " + repairs + "\"}}]}");
        };
        var png = Path.Combine(w.WebDesignDir + "-bild.png"); Directory.CreateDirectory(Path.GetDirectoryName(png)!); File.WriteAllBytes(png, Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg=="));
        MainWindow.WebDesignAuditOverride = j => Task.FromResult(Ok(j, w.WebDesignDir) with { Images = [png, png, png] });
        MainWindow.WebDesignVisionOverride = (_, _) => Task.FromResult<string?>("Der Kopf wirkt leer.");
        w.Show("dev"); Pump();
        await w.WebDesignAiRun("Seite"); Pump();
        Assert.Equal(1, repairs); Assert.True(w.WebDesignLoop!.Clean); Assert.Equal(1, w.WebDesignLoop.Repairs);
        Assert.DoesNotContain("#fehlt", w.WebDesignPending!.Json);
        Assert.NotNull(w.Find<WrapPanel>("web-ki-images"));
        Assert.Contains(w.GetLogicalTexts(), t => t.Contains("nur ein Hinweis, kein Nachweis"));

        MainWindow.WebDesignAuditOverride = _ => Task.FromResult(new WebAuditResult([new("Konsole", "Fehler", "Skriptfehler", "console")], [], ""));
        repairs = 0;
        await w.WebDesignAiRun("Seite"); Pump();
        Assert.Equal(WebDesignCheck.MaxRepairs, repairs); Assert.True(w.WebDesignLoop!.LimitReached);
        Assert.Contains(w.GetLogicalTexts(), t => t.Contains("Obergrenze"));
        Assert.Contains(w.GetLogicalTexts(), t => t.Contains("Skriptfehler"));
    }

    [AvaloniaFact(DisplayName = "A6: „Seite prüfen“ meldet Fehler der gespeicherten Seite, ohne etwas zu ändern")]
    public async Task A6_check_now()
    {
        var (w, _, _) = Ui.Open();
        w.WebDesignMessage("""{"art":"speichern","daten":{"root":{"props":{"title":"x","description":"y"}},"content":[{"type":"Knopf","props":{"id":"a","label":"Los","href":"#weg"}}],"zones":{}}}"""); Pump();
        var before = File.ReadAllText(PageFile(w));
        w.Show("dev"); Pump();
        w.Find<Button>("web-ki-check").Press(); Pump();
        await Until(() => w.WebDesignLoop is not null); Pump();
        Assert.Contains(w.GetLogicalTexts(), t => t.Contains("#weg"));
        Assert.Equal(before, File.ReadAllText(PageFile(w)));
    }

}

/// <summary>T-0620: Prüfungen im echten Browser. Eigene Klasse ohne Bezug zum Fenster: Sie laufen nicht auf dem UI-Thread und dürfen <see cref="MainWindow"/> nicht als Erste anfassen.</summary>
public class WebDesignKi_Browser
{
    static string Repo() => WebDesignKi_Ui.Repo();

    [Fact(DisplayName = "K1 und K2: Das echte Editor-Bündel in echtem Chrome: Auswählen, Mehrfachauswahl, Text direkt ändern, KI-Vorschau (übersprungen ohne Chrome oder Node)")]
    public void K1_K2_real_bundle()
    {
        if (WebsitePreview.FindBrowser() is not { } chrome || Cli.Find("node") is not { } node) { Assert.Skip("Übersprungen – Chrome/Edge oder Node.js fehlt auf diesem PC."); return; }
        // Echte Ausgabe der Aktionen, nicht von Hand gebaut: K4 prüft so, dass der Editor sie öffnet
        var plan = WebDesignActions.Apply(WebDesign.Empty(), """{"aktionen":[{"aktion":"seite_anlegen","titel":"Café","beschreibung":"x"},{"aktion":"ansicht_anpassen","ansicht":"handy"},{"aktion":"baustein_einfuegen","typ":"Kopfbereich","id":"held","props":{"title":"Guter Kaffee","sub":"Täglich frisch","motion":"keine"}},{"aktion":"baustein_einfuegen","typ":"Text","id":"t1","props":{"text":"Unsere Bohnen"}},{"aktion":"baustein_einfuegen","typ":"Bereich","id":"b"},{"aktion":"baustein_einfuegen","typ":"Knopf","id":"kn","in":"b","props":{"label":"Innen"}}]}""");
        Assert.Null(plan.Error);
        var file = Path.Combine(Path.GetTempPath(), "trendo-webki-seite-" + Guid.NewGuid().ToString("N") + ".json");
        File.WriteAllText(file, plan.Json!);
        try
        {
            var r = Cli.Run(node, [Path.Combine(Repo(), "tools", "webdesign-editor", "pruefe-auswahl.mjs"), chrome, Path.Combine(Repo(), "src", "Trendo.App", "Assets", "webdesign"), file], timeoutMs: 90_000);
            Assert.True(r.Ok, r.Out + r.Err);
            Assert.Contains("\"problems\":[]", r.Out);
        }
        finally { File.Delete(file); }
    }

    [Fact(DisplayName = "A6: Echte Prüfung auf dem Export: drei Bilder, gezeichnete Seite mit <main>, defekter Link und zu breiter Inhalt werden gefunden (übersprungen ohne Chrome)")]
    public async Task A6_real_browser()
    {
        if (WebsitePreview.FindBrowser() is null) { Assert.Skip("Übersprungen – kein Chrome/Edge auf diesem PC."); return; }
        var ct = TestContext.Current.CancellationToken;
        var d = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "trendo-webki-real-" + Guid.NewGuid().ToString("N"))).FullName;
        try
        {
            var editor = Path.Combine(Repo(), "src", "Trendo.App", "Assets", "webdesign");
            var plan = WebDesignActions.Apply(WebDesign.Empty(), """{"aktionen":[{"aktion":"seite_anlegen","titel":"Prüfseite","beschreibung":"Kurztext"},{"aktion":"baustein_einfuegen","typ":"Kopfbereich","id":"k","props":{"title":"Titel","sub":"Echter Text","href":"#gibtsnicht"}},{"aktion":"baustein_einfuegen","typ":"Text","id":"t","props":{"text":"Absatz"}},{"aktion":"baustein_einfuegen","typ":"Text","id":"lang","props":{"text":"LANGWORT"}}]}""".Replace("LANGWORT", new string('x', 1800))); // ein Wort ohne Umbruch macht die Seite zu breit
            Assert.Null(plan.Error);
            var axeCalls = new List<IReadOnlyList<string>>(); string? snapshot = null;
            var res = await WebDesignAudit.RunAsync(plan.Json!, d, editor, "node", Path.Combine(d, "axe"), (_, args, _) =>
            {
                axeCalls.Add(args); snapshot = File.ReadAllText(args[1]);
                return new Cli.Result(0, """{"axe":"4","results":[{"file":"index.html","passes":3,"violations":[{"id":"image-alt","impact":"critical","help":"x","nodes":1,"target":"img"}]}]}""", "");
            }, ct);
            Assert.Equal(3, res.Images.Count); Assert.All(res.Images, f => Assert.True(new FileInfo(f).Length > 1000));
            Assert.Contains("<main", snapshot); Assert.Contains("Absatz", snapshot); // die wirklich gezeichnete Seite, nicht die leere Hülle
            Assert.Contains(res.Findings, f => f.Code == "anchor" && f.Text.Contains("gibtsnicht"));
            Assert.Contains(res.Findings, f => f.Code == "axe-image-alt" && f.Level == "Fehler");
            Assert.Contains(res.Findings, f => f.Area == "Handy" && f.Text.Contains("Waagerechtes Scrollen bei 390"));
            Assert.DoesNotContain(res.Findings, f => f.Code == "main");
            Assert.DoesNotContain(res.Findings, f => f.Area == "Konsole");
            Assert.False(Directory.Exists(Path.Combine(d, WebDesignAudit.WorkDir, "seite"))); // kein HTML bleibt liegen, das andere Prüfungen mitzählen würden
            Assert.False(File.Exists(Path.Combine(d, WebDesign.PageFile))); // die Seite selbst wurde nicht geschrieben

            // Fehlende Datei fällt in der echten Konsole auf: auf die Festplatte bezogen fehlt sie, im Browser kommt 404
            var broken = WebDesignActions.Apply(plan.Json!, """{"aktionen":[{"aktion":"baustein_einfuegen","typ":"Bild","id":"f","props":{"src":"fehlt.png","alt":"Foto"}}]}""");
            var second = await WebDesignAudit.RunAsync(broken.Json!, d, editor, "node", Path.Combine(d, "axe"), (_, _, _) => new Cli.Result(0, """{"axe":"4","results":[]}""", ""), ct);
            Assert.Contains(second.Findings, f => f.Area == "Konsole" && f.Text.Contains("fehlt.png"));
        }
        finally { try { Directory.Delete(d, true); } catch (IOException) { } }
    }
}
