using Trendo.Core;
using Trendo.Core.Programs;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>Spiele bauen – Vorbild-Funktionen (GDevelop, Construct, Godot; 04.11.2026): Sprungfeder, Speicherpunkt, Schlüssel und Tür, Zeitlimit, Ereignis-Regeln – im Probespiel und im gebauten Spiel.</summary>
public class GameRulesTests
{
    static string[] Lvl(params string[] rows) => [.. Enumerable.Repeat(new string('.', rows[0].Length), 8), .. rows];
    static void Run(PlatformSim s, int frames, bool right = false, bool jump = false) { for (var i = 0; i < frames && !s.Over; i++) s.Step(false, right, jump); }

    [Fact(DisplayName = "CORE-06: Sprungfeder schleudert viel höher als ein normaler Sprung")]
    public void Spring()
    {
        double Peak(string ground) { var s = new PlatformSim(Lvl("..S.....", ground)); var y0 = s.Player.Y; var min = y0; for (var i = 0; i < 90; i++) { s.Step(false, false, i == 5); min = Math.Min(min, s.Player.Y); } return y0 - min; }
        var normal = Peak("########"); var spring = Peak("##F#####");
        Assert.InRange(normal, 90, 130); Assert.True(spring > 180, $"Feder nur {spring:0} px");
    }

    [Fact(DisplayName = "CORE-06: Schlüssel öffnet die Tür: mit Schlüssel geschafft, ohne Schlüssel bleibt die Tür zu; Regel „Sekunden ≥ 1 → Türen öffnen“ öffnet sie auch")]
    public void Key_and_door()
    {
        var with = new PlatformSim(Lvl("S..K...D...Z", "############")); Run(with, 900, right: true);
        Assert.True(with.Won); Assert.Equal((1, 1), (with.Keys, with.KeysTotal)); Assert.True(with.DoorsOpen);
        var without = new PlatformSim(Lvl("S......D...Z", "############")); Run(without, 900, right: true);
        Assert.False(without.Won); Assert.False(without.DoorsOpen); Assert.InRange(without.Player.X, 6 * 32 - 30, 7 * 32);
        var rule = new PlatformSim(Lvl("S......D...Z", "############"), rules: [new("sekunden", 1, "tueren")]); Run(rule, 900, right: true);
        Assert.True(rule.Won); Assert.Contains("Türen öffnen", rule.Note);
    }

    [Fact(DisplayName = "CORE-06: Speicherpunkt: nach einem Treffer geht es dort weiter statt am Start")]
    public void Checkpoint()
    {
        var s = new PlatformSim(Lvl("S..C...^...Z", "############"), lives: 3);
        for (var i = 0; i < 400 && s.Lives == 3; i++) s.Step(false, true, false);
        Assert.Equal(2, s.Lives); Assert.Equal((3, 8), s.Respawn); // acht leere Zeilen darüber
        Assert.InRange(s.Player.X, 3 * 32, 3 * 32 + 8);
    }

    [Fact(DisplayName = "CORE-06: Zeitlimit: nach Ablauf verloren, Restzeit zählt herunter; ohne Limit −1")]
    public void Time_limit()
    {
        var s = new PlatformSim(Lvl("S.......", "########"), timeLimit: 1);
        Run(s, 30); Assert.InRange(s.SecondsLeft, 0.4, 0.6);
        Run(s, 60); Assert.True(s.Lost && s.TimeUp); Assert.Equal("Zeit abgelaufen", s.Note);
        Assert.Equal(-1, new PlatformSim(Lvl("S.", "##")).SecondsLeft);
    }

    [Fact(DisplayName = "CORE-06: Regeln: „Münzen ≥ 2 → +1 Leben“ feuert genau einmal, „Punkte ≥ 3 → Gewonnen“ beendet das Spiel")]
    public void Rules()
    {
        var s = new PlatformSim(Lvl("S.o.o.o.o.......", "################"), lives: 2, rules: [new("muenzen", 2, "leben"), new("punkte", 3, "sieg")]);
        Run(s, 600, right: true);
        Assert.Equal(3, s.Lives); Assert.True(s.Won); Assert.Equal(3, s.Coins.Count(c => c.Taken));
        Assert.Equal("Wenn Münzen gesammelt ≥ 2, dann +1 Leben", GameStudio.RuleText(new("muenzen", 2, "leben")));
        Assert.Throws<ArgumentException>(() => GameStudio.Validate(new GameStudio.GameSettings { Rules = [new("zauber", 1, "leben")] }));
        Assert.Throws<ArgumentException>(() => GameStudio.Validate(new GameStudio.GameSettings { Rules = [.. Enumerable.Repeat(new GameStudio.GameRule("punkte", 1, "leben"), 21)] }));
        Assert.Equal(3600, GameStudio.Validate(new GameStudio.GameSettings { TimeLimit = 99999 }).TimeLimit);
    }

    [Fact(DisplayName = "CORE-06: Gebautes Spiel: config.js trägt Zeitlimit und Regeln, neue Kacheln sind erlaubt, game.js ist gültiges JavaScript (Node)")]
    public void Built_game()
    {
        var dir = Path.Combine(Path.GetTempPath(), "trendo-spiel-" + Guid.NewGuid().ToString("N")[..6]); Directory.CreateDirectory(dir);
        try
        {
            var game = GameStudio.CreateBrowserGame(dir, "plattform", "Schlüsseljagd", new GameStudio.GameSettings { Level = ["S.FKC.D..Z", "##########"], TimeLimit = 90, Rules = [new("gegner", 1, "punkte")] });
            var cfg = File.ReadAllText(Path.Combine(game, "config.js"));
            Assert.Contains("\"timeLimit\":90", cfg); Assert.Contains("\"rules\":[{\"when\":\"gegner\",\"n\":1,\"act\":\"punkte\"}]", cfg);
            var js = File.ReadAllText(Path.Combine(game, "game.js"));
            Assert.Contains("CFG.rules", js); Assert.Contains("'F'", js);
            if (Cli.Find("node") is { } node)
            {
                var r = Cli.Run(node, ["--check", Path.Combine(game, "game.js")], game, 30000);
                Assert.True(r.Ok, r.Err);
            }
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact(DisplayName = "CORE-06: Mehrere Level – Ziel lädt das nächste Level (+10, Hinweis „Level 2 von 2“), Münzen und Eigenschaften je Level („2:x,y:value“), erst das letzte Ziel gewinnt; Nochmal beginnt bei Level 1")]
    public void Multi_level()
    {
        string[] l1 = ["S..o..Z", "#######"], l2 = ["S.o...Z", "#######"];
        var s = new PlatformSim(l1, props: new Dictionary<string, double> { ["3,0:value"] = 2, ["2:2,0:value"] = 7 }, moreLevels: [l2]);
        Assert.Equal((0, 2), (s.LevelIndex, s.LevelCount));
        Run(s, 600, right: true);
        Assert.True(s.Won); Assert.Equal(1, s.LevelIndex);
        Assert.Equal(2 + 10 + 7 + 10, s.Score);
        var again = s.Restart(); Assert.Equal(0, again.LevelIndex); Assert.Equal(2, again.LevelCount); Assert.False(again.Won);
        // Zwischenstand: nach dem ersten Ziel steht die Figur am Start von Level 2
        var t = new PlatformSim(l1, moreLevels: [l2]);
        for (var i = 0; i < 600 && t.LevelIndex == 0; i++) t.Step(false, true, false);
        Assert.Equal(1, t.LevelIndex); Assert.False(t.Won); Assert.Equal("Level 2 von 2", t.Note); Assert.Equal(1, t.Coins.Count);
    }

    [Fact(DisplayName = "CORE-06: Zustände der Figur – stehen auf dem Boden, laufen mit Tempo, springen nach oben, fallen nach unten; Blickrichtung folgt der Laufrichtung")]
    public void Player_states()
    {
        var s = new PlatformSim(["..........", "..........", "....S.....", "##########"]);
        Run(s, 40); Assert.Equal("stehen", s.State);
        s.Step(false, true, false); Assert.Equal("laufen", s.State); Assert.Equal(1, s.Player.Facing);
        s.Step(true, false, false); s.Step(true, false, false); Assert.Equal(-1, s.Player.Facing);
        Run(s, 5); s.Step(false, false, true); s.Step(false, false, false); Assert.Equal("springen", s.State);
        for (var i = 0; i < 60 && s.Player.Vy <= 0; i++) s.Step(false, false, false);
        Assert.Equal("fallen", s.State);
        Assert.Equal(["stehen", "laufen", "springen", "fallen", "treffen"], PlatformSim.States.Select(x => x.Id));
    }

    [Fact(DisplayName = "CORE-06: Prüfung – Hintergrund nur Deko-Zeichen (w, b, t, m, g), Level 2 ff. nur gültige Kacheln, höchstens 20 Level, Animation nur für bekannte Zustände, Bilder 1–32 und 1–30 je Sekunde")]
    public void Validate_levels_and_anims()
    {
        Assert.Throws<ArgumentException>(() => GameStudio.Validate(new GameStudio.GameSettings { Decor = [["w#"]] }));
        Assert.Throws<ArgumentException>(() => GameStudio.Validate(new GameStudio.GameSettings { MoreLevels = [["S..X"]] }));
        Assert.Throws<ArgumentException>(() => GameStudio.Validate(new GameStudio.GameSettings { MoreLevels = [.. Enumerable.Repeat(new[] { "S.Z", "###" }, 20)] }));
        Assert.Throws<ArgumentException>(() => GameStudio.Validate(new GameStudio.GameSettings { Anims = new() { ["tanzen"] = new("a.png") } }));
        var ok = GameStudio.Validate(new GameStudio.GameSettings { Decor = [["wbtmg."]], Anims = new() { ["laufen"] = new("a.png", 99, 0) } });
        Assert.Equal((32, 1), (ok.Anims["laufen"].Frames, ok.Anims["laufen"].Fps));
    }

    [Fact(DisplayName = "CORE-06: Gebautes Spiel mit 2 Leveln, Hintergrund und Lauf-Animation – config.js trägt alles, Bildstreifen liegt im Spielordner, game.js ist gültiges JavaScript (Node)")]
    public void Built_game_levels()
    {
        var dir = Path.Combine(Path.GetTempPath(), "trendo-spiel-" + Guid.NewGuid().ToString("N")[..6]); Directory.CreateDirectory(dir);
        try
        {
            var strip = Path.Combine(dir, "lauf.png"); File.WriteAllBytes(strip, Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg=="));
            var d = new System.Text.Json.Nodes.JsonObject
            {
                ["browser"] = "plattform", ["title"] = "Zwei Welten", ["level"] = new System.Text.Json.Nodes.JsonArray("S....Z", "######"), ["decor"] = new System.Text.Json.Nodes.JsonArray("w..t.."),
                ["levelName"] = "Wiese",
                ["levels"] = new System.Text.Json.Nodes.JsonArray(new System.Text.Json.Nodes.JsonObject { ["name"] = "Höhle", ["rows"] = new System.Text.Json.Nodes.JsonArray("S.o..Z", "######"), ["decor"] = new System.Text.Json.Nodes.JsonArray("..m...") }),
                ["anims"] = new System.Text.Json.Nodes.JsonObject { ["laufen"] = new System.Text.Json.Nodes.JsonObject { ["file"] = strip, ["frames"] = 4, ["fps"] = 10 } },
            };
            var game = GameDesign.BuildBrowser(d, dir);
            var cfg = File.ReadAllText(Path.Combine(game, "config.js"));
            Assert.Contains("\"more\":[[\"S.o..Z\",\"######\"]]", cfg);
            Assert.Contains("\"decor\":[[\"w..t..\"],[\"..m...\"]]", cfg);
            Assert.Contains("\"names\":[\"Wiese\",\"H", cfg);
            Assert.Contains("\"laufen\":{\"img\":\"figur-laufen.png\",\"frames\":4,\"fps\":10}", cfg);
            Assert.True(File.Exists(Path.Combine(game, "figur-laufen.png")));
            var js = File.ReadAllText(Path.Combine(game, "game.js"));
            Assert.Contains("function load(i)", js); Assert.Contains("CFG.anims", js); Assert.Contains("deco(", js);
            if (Cli.Find("node") is { } node)
            {
                var r = Cli.Run(node, ["--check", Path.Combine(game, "game.js")], game, 30000);
                Assert.True(r.Ok, r.Err);
            }
        }
        finally { Directory.Delete(dir, true); }
    }
}
