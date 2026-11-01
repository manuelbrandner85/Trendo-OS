using System.Text.Json.Nodes;
using Trendo.Core;
using Trendo.Core.Programs;
using Xunit;

namespace Trendo.Core.Tests;

/// Spiel-Editor: Probespielen im Fenster mit denselben Regeln wie das Browser-Spiel; Objekt-Eigenschaften (Gegner-Tempo, Münzwert) auch im gebauten Spiel.
public class PlatformSimTests
{
    static PlatformSim Run(PlatformSim s, int frames, bool left = false, bool right = false, bool jump = false)
    { for (var i = 0; i < frames && !s.Over; i++) s.Step(left, right, jump); return s; }

    [Fact(DisplayName = "Probespielen: Figur landet auf dem Boden, läuft nach rechts, sammelt Münzen (mit Wert) und erreicht das Ziel (+10)")]
    public void Walk_to_goal()
    {
        string[] level = ["..........", "S..o....Z.", "##########"];
        var s = new PlatformSim(level, props: new Dictionary<string, double> { ["3,1:value"] = 5 });
        Run(s, 30); Assert.True(s.Player.On); Assert.InRange(s.Player.Y, PlatformSim.T * 2 - 30, PlatformSim.T * 2 - 29);
        Run(s, 400, right: true);
        Assert.True(s.Won); Assert.Equal(15, s.Score); Assert.True(s.Coins[0].Taken);
        var again = s.Restart(); Assert.False(again.Won); Assert.Equal(0, again.Score); Assert.Equal(5, again.Coins[0].Value);
    }

    [Fact(DisplayName = "Probespielen: Stacheln und Abgrund kosten ein Leben (zurück zum Start), nach drei Treffern verloren; Sprung nur vom Boden")]
    public void Spikes_and_lives()
    {
        var s = new PlatformSim(["S.^......Z", "##########"], lives: 3);
        Run(s, 20);
        for (var i = 0; i < 100 && s.Lives == 3; i++) s.Step(false, true, false);
        Assert.Equal(2, s.Lives); Assert.True(s.Player.X < PlatformSim.T); // zurück am Start
        Run(s, 400, right: true); Assert.True(s.Lost); Assert.True(s.Over);
        var j = new PlatformSim(["....", "....", "S...", "####"]); Run(j, 20);
        j.Step(false, false, true); Assert.True(j.Player.Vy < 0);
        var vy = j.Player.Vy; j.Step(false, false, true); Assert.True(j.Player.Vy > vy); // in der Luft kein zweiter Sprung
        var hole = new PlatformSim(["S.........", "##....####"]); Run(hole, 200, right: true); Assert.True(hole.Lives < 3);
    }

    [Fact(DisplayName = "Probespielen: Gegner laufen hin und her (Tempo je Gegner), kehren an der Kante um, Draufspringen besiegt sie (+2)")]
    public void Foes()
    {
        string[] level = ["...........", "......E....", "....#####..", "S.........Z", "###########"];
        var slow = new PlatformSim(level); var fast = new PlatformSim(level, props: new Dictionary<string, double> { ["6,1:speed"] = 3 });
        Run(slow, 10); Run(fast, 10);
        Assert.True(fast.Foes[0].X - fast.Foes[0].Col * PlatformSim.T > slow.Foes[0].X - slow.Foes[0].Col * PlatformSim.T);
        Run(fast, 200); Assert.InRange(fast.Foes[0].X, 4 * PlatformSim.T, 9 * PlatformSim.T); // bleibt auf der Plattform
        // Stampfen: Figur fällt von oben auf den Gegner
        var stomp = new PlatformSim(["...S....", "........", "........", "...E....", "########"]);
        Run(stomp, 60); Assert.False(stomp.Foes[0].Alive); Assert.Equal(2, stomp.Score); Assert.Equal(3, stomp.Lives);
    }

    [Fact(DisplayName = "Spiel bauen: Objekt-Eigenschaften, Tempo und Leben landen begrenzt in config.js; game.js nutzt Münzwert und Gegner-Tempo")]
    public void Build_with_props()
    {
        var root = Path.Combine(Path.GetTempPath(), "trendo-sim-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        var d = GameDesign.Templates.First(t => t.Id == "retro-plattform").Bible();
        d["objects"] = new JsonObject { ["18,8:speed"] = 9, ["19,4:value"] = 3, ["1,1:fremd"] = 5 };
        d["speed"] = 8; d["lives"] = 5;
        var dir = GameDesign.BuildBrowser(d, root);
        var cfg = File.ReadAllText(Path.Combine(dir, "config.js"));
        Assert.Contains("\"18,8:speed\":4", cfg); Assert.Contains("\"19,4:value\":3", cfg); Assert.DoesNotContain("fremd", cfg);
        Assert.Contains("\"speed\":8", cfg); Assert.Contains("\"lives\":5", cfg);
        var js = File.ReadAllText(Path.Combine(dir, "game.js"));
        Assert.Contains("p+=q.v||1", js); Assert.Contains("1.2*(f.s||1)", js);
    }
}
