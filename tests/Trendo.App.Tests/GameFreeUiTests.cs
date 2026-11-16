using System.Security.Cryptography;
using System.Text.Json.Nodes;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Trendo.App;
using Trendo.Core;
using Trendo.Core.Programs;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>T-0690: das frei gestaltbare Kachelspiel in „Spiele bauen“ – über die Oberfläche gebaut und in einem ECHTEN Browser (Chrome/Edge ohne Fenster)
/// mit echten Tastendrücken durchgespielt. Ohne Browser werden die Browser-Tests ehrlich übersprungen, nichts wird nachgebildet.</summary>
public class GameFree_Ui
{
    const string GK = "spiele-programm";
    static void Pump() { for (var i = 0; i < 5; i++) Dispatcher.UIThread.RunJobs(); }
    static void Enter(TextBox t, string v) { t.Text = v; t.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter }); Dispatcher.UIThread.RunJobs(); }
    static readonly byte[] Png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==");
    static WebsitePreview.PlayStep S(string kind, string arg = "", int ms = 0) => new(kind, arg, ms);

    /// <summary>Neues Kachelspiel aus der Vorlage; das Level kommt über die Mal-Fläche (derselbe Weg wie ein Strich mit der Maus).</summary>
    static (MainWindow W, ProgramDocument D) Game(string template, string[] level, string view, int lives = 3, bool menu = true)
    {
        var (w, _, _) = Ui.Open();
        w.Show(GK); Pump(); w.Find<Button>("prog-tpl-" + template).Press(); Pump();
        w.GameSection = "Level"; w.Show(GK); Pump();
        var d = w.ProgramActive(GK)!;
        w.Find<Button>("game-view-" + view).Press(); Pump();
        w.GameLevelForTests!.LevelChanged!([.. level]); Pump();
        if (lives != 3) Enter(w.Find<TextBox>("game-set-lives"), lives.ToString());
        if (!menu) { w.Find<CheckBox>("game-set-menu").IsChecked = false; Pump(); }
        return (w, d);
    }

    static string Build(MainWindow w, ProgramDocument d)
    {
        w.Find<Button>("game-sec-bauen").Press(); Pump();
        w.Find<Button>("game-build").Press(); Pump();
        var built = d.Text("built");
        Assert.True(File.Exists(Path.Combine(built, "index.html")), w.ProgramMessage(GK));
        return built;
    }

    static readonly string[] Maze = ["#######", "#S.o..#", "#####.#", "#Z....#", "#######"];

    [AvaloniaFact(DisplayName = "Kachelspiel frei: Vorlage „Abenteuer von oben“ → Blick umschalten (Rückgängig), eigenes Level malen, Gegner verfolgt, eigenes Bild, Regel „alle Münzen“; Spielen im Fenster läuft in vier Richtungen bis zum Sieg; Bauen schreibt alles ins Spiel")]
    public void Editor_top_down()
    {
        var (w, _, _) = Ui.Open();
        w.Show(GK); Pump(); w.Find<Button>("prog-tpl-topdown").Press(); Pump();
        var d = w.ProgramActive(GK)!;
        Assert.Equal("oben", d.Text("view")); Assert.Equal("plattform", d.Text("browser"));
        w.GameSection = "Level"; w.Show(GK); Pump();
        Assert.NotNull(w.TryFind<WrapPanel>("game-views"));
        Assert.Contains("✓ Start und Ziel sind da", string.Join("|", w.GetLogicalTexts())); // von oben braucht es keinen Boden unter dem Start
        // Blick umschalten ist ein Rückgängig-Schritt
        w.Find<Button>("game-view-seite").Press(); Pump();
        Assert.Equal("seite", d.Text("view")); Assert.Contains("✓ Start, Ziel und Boden sind da", string.Join("|", w.GetLogicalTexts()));
        d.Undo(); w.Show(GK); Pump(); Assert.Equal("oben", GameDesign.View(d.Data));
        // eigenes Level malen (Strich auf der Mal-Fläche), Gegner auswählen und auf „verfolgt“ stellen
        string[] own = ["###########", "#S..o.....#", "#.........#", "###########", "#......E.Z#", "###########"]; // der Gegner läuft in seinem eigenen Gang
        w.GameLevelForTests!.LevelChanged!([.. own]); Pump();
        Assert.Equal(own, GameDesignRows(d));
        w.Find<Button>("game-tile-auswahl").Press(); w.GameLevelForTests!.Pick(7, 4); Pump();
        Assert.Contains("besiegen geht nur mit einer Waffe", string.Join("|", w.GetLogicalTexts()));
        w.Find<CheckBox>("game-obj-chase").IsChecked = true; Pump();
        Assert.Equal(1, ProgramDocument.ToNumber(d.Data["objects"]!["7,4:chase"]));
        // Regel: alle Münzen → +10 Punkte
        w.Find<ComboBox>("game-rule-when").SelectedIndex = Array.FindIndex(GameStudio.RuleWhens, x => x.Id == "alle_muenzen");
        w.Find<ComboBox>("game-rule-do").SelectedIndex = Array.FindIndex(GameStudio.RuleDos, x => x.Id == "punkte");
        w.Find<Button>("game-rule-add").Press(); Pump();
        Assert.Contains("Wenn Alle Münzen des Levels gesammelt, dann +10 Punkte", string.Join("|", w.GetLogicalTexts()));
        // eigenes Bild für alle Gegner
        var img = Path.Combine(Path.GetTempPath(), "trendo-frei-" + Guid.NewGuid().ToString("N")[..8] + ".png"); File.WriteAllBytes(img, Png);
        try
        {
            w.PickOverride = _ => Task.FromResult<string[]?>([img]);
            w.Find<Button>("game-image-pick-gegner").Press(); Pump();
            Assert.Equal(img, d.Data["images"]!["gegner"]!.ToString());
            Assert.NotNull(w.TryFind<Button>("game-image-del-gegner"));
            Assert.Throws<ArgumentException>(() => w.GameSetImage(d, "drache", img));
            // Spielen im Fenster: ohne Schwerkraft, nach rechts die Münze, der Gegner kommt näher
            w.Find<Button>("game-level-play").Press(); Pump();
            var sim = w.GamePlaytest!; var canvas = w.GamePlaytestCanvas!;
            Assert.True(sim.TopDown); Assert.True(sim.Foes.Single().Chase);
            var foe = sim.Foes.Single(); var gap0 = Math.Abs(foe.X - sim.Player.X);
            var y0 = sim.Player.Y; for (var i = 0; i < 20; i++) canvas.Tick();
            Assert.Equal(y0, sim.Player.Y);
            Assert.True(Math.Abs(foe.X - sim.Player.X) < gap0 - 10); // der Gegner kommt näher
            canvas.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Right });
            for (var i = 0; i < 40; i++) canvas.Tick();
            canvas.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyUpEvent, Key = Key.Right });
            Assert.Single(sim.Coins, c => c.Taken); Assert.Equal(11, sim.Score); // 1 Münze + 10 aus der Regel
            canvas.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Down });
            for (var i = 0; i < 12; i++) canvas.Tick();
            Assert.True(sim.Player.Y > y0 + 20); // „runter“ gibt es nur von oben
            Assert.Contains("vier Richtungen", string.Join("|", w.GetLogicalTexts()));
            canvas.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Escape }); Pump();
            Assert.Null(w.GamePlaytest);
            // Bauen: Blick, Regel, Verfolgen und Bild stehen im Spiel
            var built = Build(w, d);
            var s = GameStudio.ReadSettings(built);
            Assert.Equal("oben", s.View); Assert.Equal(own, s.Level); Assert.Equal(1, s.Props["7,4:chase"]);
            Assert.Contains(s.Rules, r => r.When == "alle_muenzen" && r.Do == "punkte" && r.N == 1);
            Assert.Equal("bild-gegner.png", s.Images["gegner"]); Assert.True(File.Exists(Path.Combine(built, "bild-gegner.png")));
            // Engine-Projekt: Godot nimmt den Blick von oben, Unity sagt ehrlich nein
            w.Find<Button>("game-to-unity").Press(); Pump();
            Assert.Contains("von oben", w.ProgramMessage(GK)); Assert.Equal("", d.Text("unityProject"));
            w.Find<Button>("game-to-godot").Press(); Pump();
            var godot = d.Text("godotProject");
            Assert.Contains("\"view\": \"oben\"", File.ReadAllText(Path.Combine(godot, "level.json")));
            Assert.True(File.Exists(Path.Combine(godot, "tests", "selfplay_check.gd")));
        }
        finally { w.PickOverride = null; File.Delete(img); }
    }

    static string[] GameDesignRows(ProgramDocument d) => [.. d.List("level").Select(x => x!.GetValue<string>())];

    [AvaloniaFact(DisplayName = "Spiel aus einem Satz: Übernehmen legt ein sofort spielbares, im Level-Editor änderbares Spiel an (Blick von oben) – Spielen läuft, Rückgängig nimmt den Auftrag zurück")]
    public async Task Prompt_playable()
    {
        var (w, _, _) = Ui.Open();
        try
        {
            MainWindow.GamePromptOverride = (_, _) => Task.FromResult("```json\n{\"title\":\"Kristallgarten\",\"scale\":\"retro\",\"genre\":\"Labyrinth\",\"browser\":\"plattform\",\"view\":\"oben\",\"level\":[\"#######\",\"#S.o.Z#\",\"#######\"]}\n```");
            w.Show(GK); Pump();
            await w.GamePromptRun("Ein Labyrinth mit Kristallen", false); Pump();
            Assert.Contains("Blickrichtung", string.Join("|", w.GamePromptPreview));
            w.GamePromptAccept(); Pump();
            var d = w.ProgramActive(GK)!;
            Assert.Equal("Kristallgarten", d.Text("title")); Assert.Equal("oben", d.Text("view"));
            Assert.Equal("Level", w.GameSection); // bei einem Kachelspiel landet man direkt auf der Mal-Fläche
            Assert.NotNull(w.GameLevelForTests); Assert.Equal("#S.o.Z#", w.GameLevelForTests!.Rows[1]);
            Assert.Contains("Spielen", w.ProgramMessage(GK));
            w.Find<Button>("game-level-play").Press(); Pump();
            var sim = w.GamePlaytest!; Assert.True(sim.TopDown);
            for (var i = 0; i < 120 && !sim.Over; i++) w.GamePlaytestCanvas!.Tick(right: true);
            Assert.True(sim.Won); Assert.Single(sim.Coins, c => c.Taken);
            w.GamePlaytestCanvas!.Stop!(); Pump();
            // weiter bearbeiten und zurücknehmen
            w.GameLevelForTests!.LevelChanged!(["#######", "#S...Z#", "#######"]); Pump();
            Assert.Equal("#S...Z#", GameDesignRows(d)[1]);
            d.Undo(); Assert.Equal("#S.o.Z#", GameDesignRows(d)[1]);
            d.Undo(); Assert.NotEqual("Kristallgarten", d.Text("title")); // der ganze Auftrag ist ein Rückgängig-Schritt
        }
        finally { MainWindow.GamePromptOverride = null; }
    }

    [AvaloniaFact(DisplayName = "Browser ECHT (ohne Ersatz): Spiel von oben über die Oberfläche bauen und in Chrome mit echten Tasten durchspielen – Menü starten, laufen, Münze sammeln, Wand hält auf, Regel zählt, Ziel = gewonnen; „Im echten Browser prüfen“ meldet ✓")]
    public async Task Browser_top_down_win()
    {
        if (WebsitePreview.FindBrowser() is null) { Assert.Skip("Übersprungen – kein Chrome/Edge auf diesem PC."); return; }
        var (w, d) = Game("retro-plattform", Maze, "oben");
        w.Find<ComboBox>("game-rule-when").SelectedIndex = Array.FindIndex(GameStudio.RuleWhens, x => x.Id == "alle_muenzen");
        w.Find<ComboBox>("game-rule-do").SelectedIndex = Array.FindIndex(GameStudio.RuleDos, x => x.Id == "punkte");
        w.Find<Button>("game-rule-add").Press(); Pump();
        var built = Build(w, d);
        var errors = new List<string>(); var beleg = Environment.GetEnvironmentVariable("TRENDO_BELEG_DIR");
        var steps = new List<WebsitePreview.PlayStep>
        {
            S("until", "typeof pl==='object'&&typeof mode==='string'", 8000), S("eval", "mode+'|'+TD+'|'+Math.round(pl.x)+','+Math.round(pl.y)"),
            S("wait", ms: 400), S("eval", "Math.round(pl.x)+','+Math.round(pl.y)"), // im Menü steht das Spiel, von oben fällt nichts
            S("down", "Enter", 60), S("up", "Enter", 200), S("eval", "mode"),
            S("down", "ArrowRight"), S("until", "cg===1", 4000), S("until", "pl.x>=167", 4000), S("wait", ms: 300), S("eval", "Math.round(pl.x)+'|'+p+'|'+over"), S("up", "ArrowRight", 60),
            S("down", "ArrowDown"), S("until", "pl.y>=97", 4000), S("up", "ArrowDown", 60),
        };
        if (beleg is { Length: > 0 }) steps.Add(S("shot", Path.Combine(beleg, "browser-von-oben-mitte.png")));
        steps.AddRange([S("down", "ArrowLeft"), S("until", "over", 6000), S("up", "ArrowLeft", 200), S("eval", "over+'|'+lives+'|'+p+'|'+cg+'|'+document.getElementById('s').textContent")]);
        if (beleg is { Length: > 0 }) steps.Add(S("shot", Path.Combine(beleg, "browser-von-oben-gewonnen.png")));
        var r = await WebsitePreview.PlayAsync(built, steps, errors: errors);
        Assert.True(r.Ok, r.Note); Assert.Empty(errors);
        var v = r.Values;
        Assert.Equal("true", v[0]); Assert.Equal("menu|true|36,32", v[1]); Assert.Equal("36,32", v[2]);
        Assert.Equal("play", v[3]);
        Assert.Equal("true", v[4]); Assert.Equal("true", v[5]);            // Münze gesammelt, bis zur Wand gelaufen
        Assert.Equal("168|11|false", v[6]);                                // Wand hält bei x = 168; 1 Münze + 10 aus der Regel
        Assert.Equal("true", v[7]); Assert.Equal("true", v[8]);            // runter gelaufen, Ziel erreicht
        Assert.StartsWith("true|3|21|1|Gewonnen! Punkte: 21", v[9]);
        // der Knopf im Programm prüft dasselbe Spiel im echten Browser
        Assert.True(await w.GameBrowserCheck(d), w.LastBrowserCheck);
        Assert.StartsWith("✓ Im echten Browser geprüft", w.LastBrowserCheck);
        w.Show(GK); Pump(); Assert.NotNull(w.TryFind<TextBlock>("game-browser-result"));
        if (beleg is { Length: > 0 })
            File.WriteAllLines(Path.Combine(beleg, "browser-von-oben-rohlog.txt"), [$"Browser: {r.Note}", .. v.Select((x, i) => $"Wert {i}: {x}"), "Fehler der Seite: " + (errors.Count == 0 ? "keine" : string.Join(" | ", errors)), w.LastBrowserCheck,
                .. new[] { "index.html", "game.js", "config.js", "spiel.json" }.Select(f => $"SHA-256 {f}: {Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(built, f)))).ToLowerInvariant()}")]);
    }

    [AvaloniaFact(DisplayName = "Browser ECHT (ohne Ersatz): Spiel von der Seite in Chrome – laufen, Münze, Wand, mit der Leertaste darüber springen, Ziel = gewonnen; zweites Spiel: Stacheln mit einem Leben = verloren, Leertaste startet neu; eigenes Bild wird geladen")]
    public async Task Browser_side_win_and_lose()
    {
        if (WebsitePreview.FindBrowser() is null) { Assert.Skip("Übersprungen – kein Chrome/Edge auf diesem PC."); return; }
        var beleg = Environment.GetEnvironmentVariable("TRENDO_BELEG_DIR");
        var (w, d) = Game("retro-plattform", [".............", ".............", "S.o.#.......Z", "#############"], "seite", menu: false);
        var img = Path.Combine(Path.GetTempPath(), "trendo-frei-" + Guid.NewGuid().ToString("N")[..8] + ".png"); File.WriteAllBytes(img, Png);
        try
        {
            w.GameSetImage(d, "muenze", img);
            var built = Build(w, d);
            var errors = new List<string>();
            var steps = new List<WebsitePreview.PlayStep>
            {
                S("until", "typeof pl==='object'&&pl.on===true", 8000), S("until", "TI.muenze&&TI.muenze.naturalWidth>0", 4000), S("eval", "mode+'|'+TD"),
                S("down", "ArrowRight"), S("until", "cg===1", 4000), S("until", "pl.x>=103", 4000), S("wait", ms: 300), S("eval", "Math.round(pl.x)+'|'+over+'|'+pl.on"),
                S("down", "Space", 60), S("up", "Space"), S("until", "pl.y<40", 2000), S("until", "over", 6000), S("up", "ArrowRight", 200),
                S("eval", "over+'|'+lives+'|'+p+'|'+document.getElementById('s').textContent"),
            };
            if (beleg is { Length: > 0 }) steps.Add(S("shot", Path.Combine(beleg, "browser-von-der-seite-gewonnen.png")));
            var r = await WebsitePreview.PlayAsync(built, steps, errors: errors);
            Assert.True(r.Ok, r.Note); Assert.Empty(errors);
            var v = r.Values;
            Assert.Equal("true", v[0]); Assert.Equal("true", v[1]); Assert.Equal("play|false", v[2]); // das eigene Münzbild ist geladen; ohne Menü sofort im Spiel
            Assert.Equal("true", v[3]); Assert.Equal("true", v[4]);
            Assert.Equal("104|false|true", v[5]);                             // steht vor der Wand auf dem Boden
            Assert.Equal("true", v[6]); Assert.Equal("true", v[7]);           // gesprungen (über Wandhöhe), dann Ziel
            Assert.StartsWith("true|3|11|Gewonnen! Punkte: 11", v[8]);
            List<string> log = [$"Browser: {r.Note}", .. v.Select((x, i) => $"Seite Wert {i}: {x}")];

            // zweites Spiel: verlieren
            var (w2, d2) = Game("retro-plattform", ["S.^..Z", "######"], "seite", lives: 1, menu: false);
            var built2 = Build(w2, d2);
            var r2 = await WebsitePreview.PlayAsync(built2,
            [
                S("until", "typeof pl==='object'&&pl.on===true", 8000), S("eval", "lives+'|'+over"),
                S("down", "ArrowRight"), S("until", "over", 4000), S("up", "ArrowRight", 100), S("eval", "over+'|'+lives+'|'+document.getElementById('s').textContent"),
                S("down", "Space", 60), S("up", "Space", 200), S("eval", "over+'|'+lives"),
            ], errors: errors);
            Assert.True(r2.Ok, r2.Note); Assert.Empty(errors);
            Assert.Equal("1|false", r2.Values[1]);
            Assert.Equal("true", r2.Values[2]); Assert.StartsWith("true|0|Vorbei", r2.Values[3]);
            Assert.Equal("false|1", r2.Values[4]); // Leertaste: neues Spiel
            if (beleg is { Length: > 0 })
                File.WriteAllLines(Path.Combine(beleg, "browser-von-der-seite-rohlog.txt"), [.. log, .. r2.Values.Select((x, i) => $"Verlieren Wert {i}: {x}"), "Fehler der Seite: keine",
                    .. new[] { "index.html", "game.js", "config.js", "spiel.json" }.Select(f => $"SHA-256 {f}: {Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(built, f)))).ToLowerInvariant()}")]);
        }
        finally { File.Delete(img); }
    }

    [AvaloniaFact(DisplayName = "Godot ECHT über die Oberfläche (ohne Ersatz): Spiel von oben bauen → „Als Godot-Projekt“ → „Godot prüfen“ startet die echte Engine; Bericht nennt jede Prüfung mit ✓ und die Durchspiel-Prüfung")]
    public void Godot_real_from_ui()
    {
        var (w, d) = Game("topdown", GameDesign.DefaultTopDown, "oben");
        var engines = w.EngineDetect();
        if (engines.FirstOrDefault(e => e.Id == "godot") is not { Installed: true, Exe: { } exe }) { Assert.Skip("Übersprungen – Godot 4 ist auf diesem PC nicht vorhanden."); return; }
        Build(w, d);
        w.Find<Button>("game-to-godot").Press(); Pump();
        var godot = d.Text("godotProject"); Assert.True(File.Exists(Path.Combine(godot, "project.godot")));
        // der Testmodus startet von sich aus nichts: hier wird die echte Engine ausdrücklich zugelassen (echter Prozess, keine nachgebildete Ausgabe)
        w.EngineProbeOverride = () => engines;
        var runs = new List<string>();
        w.EngineRunOverride = (e, args) => { var r = Cli.Run(e, args, godot, 300_000); runs.Add($"{Path.GetFileName(e)} {string.Join(' ', args.Select(a => a == godot ? "<projekt>" : a))} → Exitcode {r.Exit}"); return (r.Exit, r.Out + "\n" + r.Err); };
        try
        {
            w.GameEngineTest(d, "godot");
            var rep = w.LastEngineReport;
            foreach (var label in new[] { "Level-Prüfung", "Spiel-Prüfung", "Spielstand-Prüfung", "Gameplay-Prüfung", "Menü-Prüfung", "Durchspiel-Prüfung" }) Assert.Contains($"Godot {label}: ✓", rep);
            Assert.Contains("Blick von oben", rep); Assert.Contains("Durchgespielt", rep); Assert.DoesNotContain("⚠", rep);
            Assert.Equal(7, runs.Count); Assert.All(runs, x => Assert.EndsWith("Exitcode 0", x));
            Assert.True(File.Exists(Path.Combine(godot, "Trendo-Pruefung", "godot-durchspiel.txt")));
            if (Environment.GetEnvironmentVariable("TRENDO_BELEG_DIR") is { Length: > 0 } beleg)
                File.WriteAllLines(Path.Combine(beleg, "godot-ueber-oberflaeche-rohlog.txt"), [$"Engine: {engines.First(e => e.Id == "godot").Name} {engines.First(e => e.Id == "godot").Version}", .. runs, rep]);
        }
        finally { w.EngineRunOverride = null; w.EngineProbeOverride = null; }
    }
}
