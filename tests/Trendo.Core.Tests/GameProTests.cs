using System.Text.Json.Nodes;
using Trendo.Core;
using Trendo.Core.Programs;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>Spiele bauen wie ein Profi-Studio: Phasen mit Abnahme, Spielbarkeits-Prüfung, Test-Notizen.</summary>
public class GameProTests
{
    static string Last = "";
    static bool SimReaches(string[] level)
    {
        Last = "";
        var s = new PlatformSim(level);
        for (var i = 0; i < 1500 && !s.Over; i++)
        {
            int col = (int)((s.Player.X + 12) / PlatformSim.T), row = (int)((s.Player.Y + 29) / PlatformSim.T);
            var gap = s.At(col + 1, row + 1) is not ('#' or 'F'); // vor einer Lücke abspringen
            var j = s.Player.On && gap && i > 20; if (j) Last += $" sprung@{s.Player.X:0}/{i}";
            s.Step(false, true, j);
        }
        Last += $" x={s.Player.X:0} y={s.Player.Y:0} leben={s.Lives} won={s.Won}";
        return s.Won;
    }

    [Fact(DisplayName = "CORE-06: Spielbarkeits-Prüfung – Lücke von 3 schafft die Figur (wie im Probespiel), Lücke von 7 nicht; Ziel hinter zu hoher Wand nicht erreichbar")]
    public void Fairness_reach()
    {
        string[] easy = ["..........", "..........", "..........", "S........Z", "####...###"];
        string[] far = ["..............", "..............", "..............", "S............Z", "####.......###"];
        string[] low = ["..........", "S........Z", "####....##"]; // nur eine Kachel Platz nach oben: Kopf stößt an, Lücke von 4 zu weit
        Assert.False(GamePro.Fairness(low).GoalReachable); Assert.False(SimReaches(low));
        string[] wall = ["......#...", "......#...", "......#...", "......#...", "S.....#..Z", "##########"];
        Assert.True(GamePro.Fairness(easy).GoalReachable, GamePro.Describe(GamePro.Fairness(easy), 1)); Assert.True(SimReaches(easy), "Probespiel " + Last);
        Assert.False(GamePro.Fairness(far).GoalReachable); Assert.False(SimReaches(far));
        var w = GamePro.Fairness(wall); Assert.False(w.GoalReachable); Assert.Contains(w.Findings, f => f.Severe && f.Text.StartsWith("Ziel nicht erreichbar"));
        Assert.True(GamePro.Fairness(GameStudio.DefaultPlatform).GoalReachable, GamePro.Describe(GamePro.Fairness(GameStudio.DefaultPlatform), 1) + " | " + string.Join("/", GameStudio.DefaultPlatform) + " | Probespiel " + SimReaches(GameStudio.DefaultPlatform) + Last);
        Assert.True(GamePro.MaxDx(1, 5) >= GamePro.MaxDx(3, 5)); Assert.Equal(-1, GamePro.MaxDx(5, 5));
    }

    [Fact(DisplayName = "CORE-06: Spielbarkeits-Prüfung meldet unfaire Stellen – Gegner am Start, Landung neben Stacheln, unerreichbare Münze, Schlüssel öffnen Türen; Sprungfeder hilft über hohe Wände")]
    public void Fairness_findings()
    {
        var r = GamePro.Fairness(["....o.....", "..........", "..........", "..........", "SE.......Z", "##########"]);
        Assert.True(r.GoalReachable);
        Assert.Contains(r.Findings, f => f.Text.StartsWith("Gegner direkt am Start"));
        Assert.Contains(r.Findings, f => f.Text.StartsWith("Münze nicht erreichbar") && f.X == 4 && f.Y == 0);
        var spikes = GamePro.Fairness(["..........", "..........", "..........", "S........Z", "###..^####"]);
        Assert.True(spikes.Findings.Any(f => f.Text.StartsWith("Landung direkt neben Stacheln") || f.Text.StartsWith("Sehr knapper")), GamePro.Describe(spikes, 1));
        string[] door = [".......D..", ".......D..", "S.K....D.Z", "##########"]; // Tür über die ganze Höhe
        Assert.True(GamePro.Fairness(door).GoalReachable);
        Assert.False(GamePro.Fairness([".......D..", ".......D..", "S......D.Z", "##########"]).GoalReachable); // ohne Schlüssel bleibt sie zu
        string[] tall = ["..........", "..........", "..........", "..........", "..........", "......#...", "......#...", "......#...", "......#...", "S...F.#..Z", "##########"];
        var noF = GamePro.Fairness([.. tall.Select(r => r.Replace('F', '.'))]); Assert.False(noF.GoalReachable, GamePro.Describe(noF, 1)); // ohne Feder zu hoch
        Assert.True(GamePro.Fairness(tall).GoalReachable, GamePro.Describe(GamePro.Fairness(tall), 2)); // mit Feder drüber
        Assert.Contains("Spalte", GamePro.Describe(GamePro.Fairness(["S.Z", "#.."]), 1));
    }

    [Fact(DisplayName = "CORE-06: Phasen mit Abnahme – nur der Reihe nach und nur mit erfüllten Kriterien; Zurücknehmen öffnet auch spätere Phasen; Beta braucht Test-Notizen ohne offene Fehler")]
    public void Phases()
    {
        var d = GameDesign.Templates.First(t => t.Id == "retro-plattform").Bible();
        Assert.Equal("konzept", GamePro.Current(d).Id);
        Assert.Throws<InvalidOperationException>(() => GamePro.Accept(d, "prototyp")); // erst Konzept
        d["audience"] = ""; Assert.Throws<InvalidOperationException>(() => GamePro.Accept(d, "konzept"));
        d["audience"] = "Kinder ab 8"; GamePro.Accept(d, "konzept");
        Assert.Equal("prototyp", GamePro.Current(d).Id);
        Assert.Contains(GamePro.Criteria(d, "prototyp"), c => c.Text == "Einmal gebaut" && !c.Ok);
        d["built"] = "x"; GamePro.Accept(d, "prototyp");
        Assert.False(GamePro.Criteria(d, "beta").All(c => c.Ok));
        GamePro.AddNote(d, 1, 3, 4, "Sprung bei Spalte 4 zu knapp", bug: true);
        Assert.Contains(GamePro.Criteria(d, "beta"), c => c.Text == "Keine offenen Fehler" && !c.Ok);
        ((JsonObject)((JsonArray)d["testnotes"]!)[0]!)["done"] = true;
        Assert.True(GamePro.Criteria(d, "beta").All(c => c.Ok));
        GamePro.Reopen(d, "konzept"); Assert.False(GamePro.Accepted(d, "prototyp")); Assert.Equal("konzept", GamePro.Current(d).Id);
        Assert.Throws<ArgumentException>(() => GamePro.AddNote(d, 1, 0, 0, "  ", false));
        Assert.Equal(7, GamePro.Roles.Length); Assert.Equal(6, GamePro.Phases.Length);
    }

    [Fact(DisplayName = "CORE-06: Ton-Spur und Treffer-Animation – Geräusch je Ereignis und Musik landen im Spielordner, config.js nennt sie, game.js spielt sie (sonst Piepton); nach einem Treffer ist der Zustand „treffen“")]
    public void Sounds_and_hit_state()
    {
        var dir = Path.Combine(Path.GetTempPath(), "trendo-ton-" + Guid.NewGuid().ToString("N")[..6]); Directory.CreateDirectory(dir);
        try
        {
            var wav = Path.Combine(dir, "sprung.wav"); File.WriteAllBytes(wav, [0x52, 0x49, 0x46, 0x46, 0, 0, 0, 0]);
            var d = new JsonObject { ["browser"] = "plattform", ["title"] = "Tonprobe", ["level"] = new JsonArray("S....Z", "######"), ["sounds"] = new JsonObject { ["springen"] = wav, ["musik"] = Path.Combine(dir, "fehlt.mp3") } };
            var game = GameDesign.BuildBrowser(d, dir);
            Assert.True(File.Exists(Path.Combine(game, "ton-springen.wav")));
            var cfg = File.ReadAllText(Path.Combine(game, "config.js"));
            Assert.Contains("\"sounds\":{\"springen\":\"ton-springen.wav\"}", cfg); // fehlende Datei fällt weg
            var js = File.ReadAllText(Path.Combine(game, "game.js"));
            Assert.Contains("snd('springen',660)", js); Assert.Contains("'treffen'", js);
            if (Cli.Find("node") is { } node) { var r = Cli.Run(node, ["--check", Path.Combine(game, "game.js")], game, 30000); Assert.True(r.Ok, r.Err); }
            Assert.Throws<ArgumentException>(() => GameStudio.Validate(new GameStudio.GameSettings { Sounds = new() { ["knall"] = "x.wav" } }));
        }
        finally { Directory.Delete(dir, true); }
        var sim = new PlatformSim(["..........", "S.^......Z", "##########"]);
        for (var i = 0; i < 200 && sim.Lives == 3; i++) sim.Step(false, true, false);
        Assert.Equal("treffen", sim.State);
        Assert.Contains(PlatformSim.States, x => x.Id == "treffen");
    }
}
