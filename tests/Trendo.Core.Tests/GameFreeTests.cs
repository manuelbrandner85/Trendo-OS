using System.Security.Cryptography;
using System.Text.Json.Nodes;
using Trendo.Core.Programs;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>T-0690: das frei gestaltbare Kachelspiel – Blick von oben, Regeln für Sieg und Niederlage, verfolgende Gegner, eigene Bilder je Objektart,
/// und der echte Godot-Lauf (ohne Ersatz; übersprungen, wenn Godot auf diesem PC fehlt).</summary>
public class GameFreeTests
{
    static string Tmp() { var d = Path.Combine(Path.GetTempPath(), "trendo-frei-" + Guid.NewGuid().ToString("N")[..8]); Directory.CreateDirectory(d); return d; }
    static readonly byte[] Png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==");

    static readonly string[] Maze = ["#####", "#S.o#", "#.#.#", "#..Z#", "#####"];

    [Fact(DisplayName = "Kachelspiel frei: Blickrichtung und Objektbilder werden geprüft, ins Spiel geschrieben und die Bilder in den Spielordner kopiert")]
    public void Settings_view_and_images()
    {
        Assert.Throws<ArgumentException>(() => GameStudio.Validate(new GameStudio.GameSettings { View = "schraeg" }));
        Assert.Throws<ArgumentException>(() => GameStudio.Validate(new GameStudio.GameSettings { Images = new() { ["drache"] = "x.png" } }));
        Assert.Equal("seite", new GameStudio.GameSettings().View); // alte spiel.json ohne Feld bleibt ein Spiel von der Seite
        var flag = GameStudio.Validate(new GameStudio.GameSettings { Rules = [new("alle_muenzen", 99, "sieg"), new("muenzen", 7, "verloren")] });
        Assert.Equal(1, flag.Rules[0].N); Assert.Equal(7, flag.Rules[1].N);
        Assert.Equal("Wenn Alle Münzen des Levels gesammelt, dann Gewonnen", GameStudio.RuleText(flag.Rules[0]));
        Assert.Equal("Wenn Münzen gesammelt ≥ 7, dann Verloren", GameStudio.RuleText(flag.Rules[1]));
        Assert.Empty(GameStudio.CheckPlatform(Maze, topDown: true));
        Assert.Contains(GameStudio.CheckPlatform(["S..Z"], topDown: false), h => h.Contains("kein Boden"));
        var root = Tmp();
        try
        {
            var img = Path.Combine(root, "monster.png"); File.WriteAllBytes(img, Png);
            var dir = GameStudio.CreateBrowserGame(root, "plattform", "Irrgarten", new GameStudio.GameSettings { Level = Maze, View = "oben", Props = new() { ["3,1:chase"] = 3 } });
            var s = GameStudio.Save(dir, GameStudio.ReadSettings(dir), imageFiles: new Dictionary<string, string> { ["gegner"] = img });
            Assert.Equal("oben", s.View); Assert.Equal("bild-gegner.png", s.Images["gegner"]); Assert.Equal(1, s.Props["3,1:chase"]);
            Assert.True(File.Exists(Path.Combine(dir, "bild-gegner.png")));
            var cfg = File.ReadAllText(Path.Combine(dir, "config.js"));
            Assert.Contains("\"view\":\"oben\"", cfg); Assert.Contains("\"gegner\":\"bild-gegner.png\"", cfg);
            var js = File.ReadAllText(Path.Combine(dir, "game.js"));
            Assert.Contains("const TD=CFG.view==='oben';", js); Assert.Contains("alle_muenzen", js); Assert.Contains("verloren", js);
            Assert.Equal("oben", GameStudio.ReadSettings(dir).View);
            Assert.Throws<ArgumentException>(() => GameStudio.Save(dir, s, imageFiles: new Dictionary<string, string> { ["gegner"] = Path.Combine(root, "ton.mp3") }));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact(DisplayName = "Kachelspiel frei: Probespiel von oben – keine Schwerkraft, vier Richtungen, Wand hält auf, Münze, Ziel gewinnt; von der Seite fällt dieselbe Figur")]
    public void Sim_top_down()
    {
        var sim = new PlatformSim(Maze, view: "oben");
        Assert.True(sim.TopDown);
        double x0 = sim.Player.X, y0 = sim.Player.Y;
        for (var i = 0; i < 40; i++) sim.Step(false, false, true); // Springen gibt es von oben nicht
        Assert.Equal(y0, sim.Player.Y); Assert.Equal(x0, sim.Player.X); Assert.Equal("stehen", sim.State);
        for (var i = 0; i < 60; i++) sim.Step(false, true, false);
        Assert.True(sim.Player.X > x0 + 40); Assert.True(sim.Player.X <= 4 * PlatformSim.T - sim.Player.W, "Wand rechts hält auf");
        Assert.Equal(y0, sim.Player.Y); Assert.Single(sim.Coins, c => c.Taken); Assert.Equal(1, sim.Score);
        for (var i = 0; i < 80 && !sim.Over; i++) sim.Step(false, false, false, down: true);
        Assert.True(sim.Won); Assert.Equal(3, sim.Lives);
        var side = new PlatformSim(["S...", "....", "####"]); // ohne view: wie bisher mit Schwerkraft
        var sy = side.Player.Y; for (var i = 0; i < 30; i++) side.Step(false, false, false);
        Assert.False(side.TopDown); Assert.True(side.Player.Y > sy + 20);
        Assert.True(sim.Restart().TopDown);
    }

    [Fact(DisplayName = "Kachelspiel frei: Regeln „alle Münzen → gewonnen“ und „… → verloren“; Stacheln kosten von oben ein Leben; ein verfolgender Gegner kommt näher, ein normaler läuft hin und her")]
    public void Sim_rules_and_chase()
    {
        string[] room = ["#######", "#S.o.o#", "#.....#", "#######"];
        var win = new PlatformSim(room, rules: [new("alle_muenzen", 1, "sieg")], view: "oben");
        for (var i = 0; i < 200 && !win.Over; i++) win.Step(false, true, false);
        Assert.True(win.Won); Assert.Equal(2, win.Coins.Count(c => c.Taken));
        var lose = new PlatformSim(room, rules: [new("muenzen", 1, "verloren")], view: "oben");
        for (var i = 0; i < 200 && !lose.Over; i++) lose.Step(false, true, false);
        Assert.True(lose.Lost); Assert.False(lose.Won);
        var spikes = new PlatformSim(["#####", "#S^.#", "#####"], lives: 2, view: "oben");
        for (var i = 0; i < 40 && spikes.Lives == 2; i++) spikes.Step(false, true, false);
        Assert.Equal(1, spikes.Lives);
        // Gegner: verfolgt (chase) oder läuft hin und her
        string[] hall = ["#########", "#S......#", "#.......#", "#.....E.#", "#########"];
        var hunt = new PlatformSim(hall, props: new Dictionary<string, double> { ["6,3:chase"] = 1 }, view: "oben");
        var f = hunt.Foes.Single(); Assert.True(f.Chase);
        double Dist() => Math.Abs(f.X - hunt.Player.X) + Math.Abs(f.Y - hunt.Player.Y);
        var d0 = Dist(); for (var i = 0; i < 60; i++) hunt.Step(false, false, false);
        Assert.True(Dist() < d0 - 60, $"{d0} → {Dist()}");
        var calm = new PlatformSim(hall, view: "oben"); var g = calm.Foes.Single(); var gy = g.Y;
        for (var i = 0; i < 200; i++) calm.Step(false, false, false);
        Assert.Equal(gy, g.Y); Assert.InRange(g.X, PlatformSim.T, 7 * PlatformSim.T); // bleibt in seiner Zeile und im Raum
    }

    [Fact(DisplayName = "Kachelspiel frei: Spielbarkeit von oben – Weg zu Schlüssel und Ziel wird gefunden, eine Mauer oder verschlossene Tür ohne Schlüssel gilt als unschaffbar")]
    public void Fairness_top_down()
    {
        Assert.True(GamePro.Fairness(GameDesign.DefaultTopDown, 5, topDown: true).GoalReachable);
        Assert.True(GameDesign.DefaultTopDown.All(r => r.Length == GameDesign.DefaultTopDown[0].Length));
        var walled = GamePro.Fairness(["#####", "#S#Z#", "#####"], 5, topDown: true);
        Assert.False(walled.GoalReachable); Assert.Contains(walled.Findings, x => x.Text.Contains("Ziel nicht erreichbar"));
        var door = GamePro.Fairness(["######", "#S.DZ#", "######"], 5, topDown: true); // Tür ohne Schlüssel im Level bleibt zu
        Assert.False(door.GoalReachable);
        Assert.True(GamePro.Fairness(["######", "#SKDZ#", "######"], 5, topDown: true).GoalReachable);
        var lostKey = GamePro.Fairness(["#######", "#S.DZ##", "###.#K#", "#######"], 5, topDown: true);
        Assert.False(lostKey.GoalReachable); Assert.Contains(lostKey.Findings, x => x.Text.Contains("Schlüssel nicht erreichbar"));
        // Vorlage „Abenteuer von oben“: baut ohne weitere Angabe ein spielbares Spiel
        var t = GameDesign.Templates.Single(x => x.Id == "topdown"); var doc = t.Bible();
        Assert.Equal("plattform", doc["browser"]!.ToString()); Assert.Equal("oben", GameDesign.View(doc)); Assert.Empty(GameDesign.Check(doc).Where(h => h.Contains("Start") || h.Contains("Ziel") || h.Contains("Boden")));
        var root = Tmp();
        try
        {
            var dir = GameDesign.BuildBrowser(doc, root);
            Assert.Equal("oben", GameStudio.ReadSettings(dir).View); Assert.Equal("schwert", GameStudio.ReadSettings(dir).Weapon);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact(DisplayName = "Spiel aus einem Satz: Blick von oben wird übernommen; ein Level ohne Start oder Ziel wird durch ein spielbares ersetzt, ein gültiges eigenes Level bleibt")]
    public void Director_playable()
    {
        var top = GameDirector.Parse("```json\n{\"title\":\"Irrgarten\",\"scale\":\"retro\",\"genre\":\"Labyrinth\",\"browser\":\"plattform\"}\n```").Doc!;
        Assert.Equal("oben", top["view"]!.ToString());
        Assert.Equal(GameDesign.DefaultTopDown, top["level"]!.AsArray().Select(x => x!.ToString()).ToArray());
        var broken = GameDirector.Parse("```json\n{\"title\":\"Kaputt\",\"scale\":\"retro\",\"genre\":\"Plattformer\",\"browser\":\"plattform\",\"level\":[\"....o...\",\"########\"]}\n```").Doc!;
        Assert.Equal("seite", broken["view"]!.ToString());
        Assert.Equal(GameStudio.DefaultPlatform, broken["level"]!.AsArray().Select(x => x!.ToString()).ToArray());
        var own = GameDirector.Parse("```json\n{\"title\":\"Eigen\",\"scale\":\"retro\",\"genre\":\"Abenteuer\",\"browser\":\"plattform\",\"view\":\"oben\",\"level\":[\"#####\",\"#S.Z#\",\"#####\"]}\n```").Doc!;
        Assert.Equal(new[] { "#####", "#S.Z#", "#####" }, own["level"]!.AsArray().Select(x => x!.ToString()).ToArray());
        Assert.Equal("oben", GameDesign.SettingsOf(own).View);
        Assert.Contains("view", GameDirector.Instructions(false));
        var snake = GameDirector.Parse("```json\n{\"title\":\"S\",\"scale\":\"retro\",\"genre\":\"Schlange\",\"view\":\"oben\"}\n```").Doc!;
        Assert.Null(snake["view"]); // die Blickrichtung gibt es nur beim Kachelspiel
    }

    // ───────────── echter Godot-Lauf ─────────────
    static string? Godot()
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData); var pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        static IEnumerable<string> Files(string d) { try { return Directory.Exists(d) ? Directory.EnumerateFiles(d, "Godot_v4*", SearchOption.TopDirectoryOnly) : []; } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return []; } }
        // dieselben Orte, an denen Trendo Godot sucht (MainWindow.EngineDetect)
        var found = EngineTargets.FindGodot([Path.Combine(DataLocation.ToolsRoot(), "godot"), Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Godot"), Path.Combine(local, "Programs", "Godot"), Path.Combine(pf, "Godot")], Files);
        return found.Select(g => g.Exe).FirstOrDefault(e => e.Contains("console", StringComparison.OrdinalIgnoreCase)) ?? found.FirstOrDefault().Exe;
    }

    [Fact(DisplayName = "Godot ECHT (ohne Ersatz): ein Spiel von oben und eines von der Seite als Godot-4-Projekt – Import, Level-, Spiel-, Gameplay- und Durchspiel-Prüfung laufen in der echten Engine; ein kaputtes Skript fällt auf")]
    public void Godot_real_run()
    {
        if (Godot() is not { } exe) { Ueberspringen.Weil("Godot 4 ist auf diesem PC nicht vorhanden (Godot_v4…exe im Ordner „Godot“ des Benutzers oder im Werkzeugordner)."); return; }
        var root = Tmp(); var log = new List<string> { $"Godot: {Path.GetFileName(exe)}" }; var topDir = "";
        try
        {
            foreach (var (name, settings) in new[]
            {
                ("Von oben", new GameStudio.GameSettings { Level = GameDesign.DefaultTopDown, View = "oben", Weapon = "schwert", Rules = [new("alle_muenzen", 1, "leben")] }),
                ("Von der Seite", new GameStudio.GameSettings { Level = GameStudio.DefaultPlatform }),
            })
            {
                var dir = EngineTargets.CreateGodotProject(root, name, GameStudio.Validate(settings)); if (topDir.Length == 0) topDir = dir;
                var imp = Cli.Run(exe, EngineTargets.GodotImportArgs(dir), dir, 300_000);
                log.Add($"[{name}] --import Exitcode {imp.Exit}");
                foreach (var (label, args) in new[] { ("Level", EngineTargets.GodotCheckArgs(dir)), ("Spiel", EngineTargets.GodotPlayArgs(dir)), ("Gameplay", EngineTargets.GodotGameplayArgs(dir)), ("Spielstand", EngineTargets.GodotSaveArgs(dir)), ("Menü", EngineTargets.GodotMenuArgs(dir)), ("Durchspiel", EngineTargets.GodotSelfPlayArgs(dir)) })
                {
                    var r = Cli.Run(exe, args, dir, 180_000);
                    var (ok, text) = EngineTargets.ParseGodotCheck(r.Out + "\n" + r.Err);
                    log.Add($"[{name}] {label}: {(ok ? "OK" : "FEHLER")} {text} (Exitcode {r.Exit})");
                    Assert.True(ok && r.Exit == 0, $"{name} / {label}: {text}\n{r.Out}\n{r.Err}");
                    Assert.DoesNotContain("SCRIPT ERROR", r.Out + r.Err); Assert.DoesNotContain("Parse Error", r.Out + r.Err);
                }
            }
            // Gegenprobe: ein absichtlich kaputtes Spielskript darf nicht als bestanden durchgehen
            var bad = EngineTargets.CreateGodotProject(root, "Kaputt", GameStudio.Validate(new GameStudio.GameSettings { Level = GameDesign.DefaultTopDown, View = "oben" }));
            File.AppendAllText(Path.Combine(bad, "trendo_player.gd"), "\nfunc kaputt(:\n");
            Cli.Run(exe, EngineTargets.GodotImportArgs(bad), bad, 300_000);
            var br = Cli.Run(exe, EngineTargets.GodotSelfPlayArgs(bad), bad, 180_000);
            log.Add($"[Gegenprobe kaputtes Skript] {(EngineTargets.ParseGodotCheck(br.Out + "\n" + br.Err).Ok ? "OK (falsch!)" : "FEHLER erkannt")} (Exitcode {br.Exit})");
            Assert.False(EngineTargets.ParseGodotCheck(br.Out + "\n" + br.Err).Ok);
            if (Environment.GetEnvironmentVariable("TRENDO_BELEG_DIR") is { Length: > 0 } beleg)
            {
                Directory.CreateDirectory(beleg);
                var top = topDir;
                foreach (var f in new[] { "project.godot", "level.json", "trendo_level.gd", "trendo_player.gd", "tests/selfplay_check.gd" })
                    log.Add($"SHA-256 {f}: {Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(top, f.Replace('/', Path.DirectorySeparatorChar))))).ToLowerInvariant()}");
                File.WriteAllLines(Path.Combine(beleg, "godot-echt-rohlog.txt"), log);
            }
        }
        finally { try { Directory.Delete(root, true); } catch (IOException) { } }
    }
}
