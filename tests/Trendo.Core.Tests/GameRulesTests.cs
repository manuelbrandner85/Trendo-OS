using Trendo.Core;
using Trendo.Core.Programs;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>Spiele bauen – Vorbild-Funktionen (GDevelop, Construct, Godot; 04.11.2026): Sprungfeder, Speicherpunkt, Schlüssel und Tür, Zeitlimit, Ereignis-Regeln – im Probespiel und im gebauten Spiel.</summary>
public class GameRulesTests
{
    static string[] Lvl(params string[] rows) => [.. Enumerable.Repeat(new string('.', rows[0].Length), 8), .. rows];
    static void Run(PlatformSim s, int frames, bool right = false, bool jump = false) { for (var i = 0; i < frames && !s.Over; i++) s.Step(false, right, jump); }

    [Fact(DisplayName = "Sprungfeder schleudert viel höher als ein normaler Sprung")]
    public void Spring()
    {
        double Peak(string ground) { var s = new PlatformSim(Lvl("..S.....", ground)); var y0 = s.Player.Y; var min = y0; for (var i = 0; i < 90; i++) { s.Step(false, false, i == 5); min = Math.Min(min, s.Player.Y); } return y0 - min; }
        var normal = Peak("########"); var spring = Peak("##F#####");
        Assert.InRange(normal, 90, 130); Assert.True(spring > 180, $"Feder nur {spring:0} px");
    }

    [Fact(DisplayName = "Schlüssel öffnet die Tür: mit Schlüssel geschafft, ohne Schlüssel bleibt die Tür zu; Regel „Sekunden ≥ 1 → Türen öffnen“ öffnet sie auch")]
    public void Key_and_door()
    {
        var with = new PlatformSim(Lvl("S..K...D...Z", "############")); Run(with, 900, right: true);
        Assert.True(with.Won); Assert.Equal((1, 1), (with.Keys, with.KeysTotal)); Assert.True(with.DoorsOpen);
        var without = new PlatformSim(Lvl("S......D...Z", "############")); Run(without, 900, right: true);
        Assert.False(without.Won); Assert.False(without.DoorsOpen); Assert.InRange(without.Player.X, 6 * 32 - 30, 7 * 32);
        var rule = new PlatformSim(Lvl("S......D...Z", "############"), rules: [new("sekunden", 1, "tueren")]); Run(rule, 900, right: true);
        Assert.True(rule.Won); Assert.Contains("Türen öffnen", rule.Note);
    }

    [Fact(DisplayName = "Speicherpunkt: nach einem Treffer geht es dort weiter statt am Start")]
    public void Checkpoint()
    {
        var s = new PlatformSim(Lvl("S..C...^...Z", "############"), lives: 3);
        for (var i = 0; i < 400 && s.Lives == 3; i++) s.Step(false, true, false);
        Assert.Equal(2, s.Lives); Assert.Equal((3, 8), s.Respawn); // acht leere Zeilen darüber
        Assert.InRange(s.Player.X, 3 * 32, 3 * 32 + 8);
    }

    [Fact(DisplayName = "Zeitlimit: nach Ablauf verloren, Restzeit zählt herunter; ohne Limit −1")]
    public void Time_limit()
    {
        var s = new PlatformSim(Lvl("S.......", "########"), timeLimit: 1);
        Run(s, 30); Assert.InRange(s.SecondsLeft, 0.4, 0.6);
        Run(s, 60); Assert.True(s.Lost && s.TimeUp); Assert.Equal("Zeit abgelaufen", s.Note);
        Assert.Equal(-1, new PlatformSim(Lvl("S.", "##")).SecondsLeft);
    }

    [Fact(DisplayName = "Regeln: „Münzen ≥ 2 → +1 Leben“ feuert genau einmal, „Punkte ≥ 3 → Gewonnen“ beendet das Spiel")]
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

    [Fact(DisplayName = "Gebautes Spiel: config.js trägt Zeitlimit und Regeln, neue Kacheln sind erlaubt, game.js ist gültiges JavaScript (Node)")]
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
}
