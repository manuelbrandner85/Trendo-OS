using Trendo.Core.Programs;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>U1–U6 (Freigabe 07.11.2026, nach UltraGameStudio, MIT): Fachrollen, Rat-Modus, Ablaufplaner, Prüfschleife, Sprites, Befehle.</summary>
public class GameStudioUgsTests
{
    [Fact(DisplayName = "U1: über 50 Spiele-Fachrollen, eindeutig, jede mit Auftrag, drei Leitlinien, Grenze und Stichwörtern; deutsche Anweisung")]
    public void Experts_catalog()
    {
        Assert.True(GameExperts.All.Count >= 52);
        Assert.Equal(GameExperts.All.Count, GameExperts.All.Select(e => e.Id).Distinct().Count());
        Assert.All(GameExperts.All, e => { Assert.Equal(3, e.Rules.Length); Assert.NotEmpty(e.Triggers); Assert.False(string.IsNullOrWhiteSpace(e.Limit)); Assert.Contains("Antworte auf Deutsch", e.Prompt); });
        foreach (var eng in new[] { "unreal", "unity", "godot", "web" }) Assert.Contains(GameExperts.All, e => e.Engine == eng);
        Assert.StartsWith("spiele.", GameExperts.ToAgent(GameExperts.All[0]).Id);
        // im KI-Team: Rolle über ihre Id auffindbar, fest dem Spiele-Studio zugeordnet, mit den Grundregeln aller Rollen
        var r = AgentCenter.Role("spiele.ai-programmer");
        Assert.Equal("CORE-06", r.Studio); Assert.Equal("KI-Programmierer", r.Name); Assert.Contains("Senden, Kaufen", r.SystemPrompt);
        Assert.Throws<InvalidOperationException>(() => AgentCenter.Role("spiele.gibt-es-nicht"));
    }

    [Fact(DisplayName = "U2: Rat-Modus wählt passend zum Auftrag (1/3/6), bevorzugt die eigene Engine und lässt fremde Engine-Spezialisten weg")]
    public void Council_pick()
    {
        Assert.Single(GameExperts.Pick("Gegner sollen den Spieler verfolgen", "unreal", "leicht"));
        Assert.Equal("ai-programmer", GameExperts.Pick("Gegner sollen den Spieler verfolgen", "unreal", "leicht")[0].Id);
        var rat = GameExperts.Pick("Mehrspieler mit Fähigkeiten und Abklingzeit, Server entscheidet", "unreal", "rat");
        Assert.InRange(rat.Count, 3, 6); Assert.Contains(rat, e => e.Id == "ue-gas-specialist"); Assert.DoesNotContain(rat, e => e.Engine is "unity" or "godot");
        Assert.Equal(["game-designer", "technical-director"], GameExperts.Pick("xyz", null, "normal").Select(e => e.Id));
    }

    [Fact(DisplayName = "U3: Strategien aus dem Auftrag, Plan endet mit Abnahme; Lauf bessert nach Mängeln nach und protokolliert")]
    public async Task Harness_plan_and_run()
    {
        Assert.Equal(["bis-fertig", "gegenpruefung"], GameHarness.InferStrategies("Das Spiel stürzt beim Laden ab"));
        Assert.Contains("wettbewerb", GameHarness.InferStrategies("Drei Ideen für einen Boss"));
        var plan = GameHarness.Build("Das Spiel stürzt beim Laden ab", "unreal");
        Assert.Equal("abnahme", plan.Steps[^1].Stage); Assert.Contains(plan.Steps, s => s.Stage == "gegenpruefung");
        var dir = Path.Combine(Path.GetTempPath(), "trendo-lauf-" + Guid.NewGuid().ToString("N"));
        try
        {
            int checks = 0;
            Task<string> Ask(string role, string q, CancellationToken _) => Task.FromResult(q.Contains("JSON") ? (++checks <= 2 ? "{\"ok\":false,\"maengel\":[\"Ladepfad fehlt\"]}" : "Urteil: {\"ok\":true,\"maengel\":[]}") : "Lösung: Ladepfad prüfen");
            var (ok, result, log) = await GameHarness.Run(plan, Ask, dir);
            Assert.True(ok); Assert.Contains("Ladepfad", result);
            Assert.Contains("\"runde\":2", File.ReadAllText(Path.Combine(log, "ledger.jsonl"))); Assert.Contains("bestanden", File.ReadAllText(Path.Combine(log, "ergebnis.md")));
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
        Assert.False(GameHarness.ParseCheck("keine Ahnung").Ok);
    }

    [Fact(DisplayName = "U4: Prüfschleife – Urteil lesen, Schwelle, Prompt mit Mängeln nachbessern, Abbruch bei bestanden; ohne Bewerter ehrlich ungeprüft")]
    public async Task Asset_check_loop()
    {
        var v = AssetCheck.Parse("Hier: {\"pass\":true,\"score\":62,\"maengel\":[\"sechs Finger\"],\"prompt_zusatz\":\"fünf Finger\"}");
        Assert.NotNull(v); Assert.False(v!.Pass); Assert.Equal(62, v.Score); // pass ohne genug Punkte gilt nicht
        Assert.Equal("Krieger, fünf Finger. Vermeiden: sechs Finger", AssetCheck.NextPrompt("Krieger", v));
        int n = 0;
        var tries = await AssetCheck.Loop("Krieger", (p, i, _) => Task.FromResult($"bild{i}.png"), (f, q, _) => Task.FromResult(++n < 2 ? "{\"pass\":false,\"score\":40,\"maengel\":[\"unscharf\"]}" : "{\"pass\":true,\"score\":90,\"maengel\":[]}"));
        Assert.Equal(2, tries.Count); Assert.True(tries[^1].Verdict!.Pass); Assert.Contains("unscharf", tries[1].Prompt);
        var blind = await AssetCheck.Loop("Krieger", (p, i, _) => Task.FromResult("a.png"), null);
        Assert.Single(blind); Assert.Null(blind[0].Verdict);
    }

    [Fact(DisplayName = "U5: Spritesheet-Raster, Zellen und Prompt für die lokale Bild-KI")]
    public void Sprite_sheet()
    {
        Assert.Equal((3, 3), SpriteSheet.Grid(8)); Assert.Equal((8, 1), SpriteSheet.Grid(8, 8));
        var cells = SpriteSheet.Cells(6, 64); Assert.Equal(6, cells.Count); Assert.Equal((64, 64, 64, 64), cells[4]);
        var p = SpriteSheet.Prompt("Ritter mit Schild", "laufen", 8); Assert.Contains("3 columns x 3 rows", p); Assert.Contains("Laufen", p); Assert.Contains("no text", p);
    }

    [Fact(DisplayName = "U6: Befehle /bild /sprite /modell /musik /sprache /video, mit Start/Ende-Modus; normaler Text ist kein Befehl")]
    public void Commands()
    {
        Assert.Equal(new GameCommands.Parsed("sprite", "Ritter läuft", null), GameCommands.Parse("/sprite Ritter läuft"));
        Assert.Equal("start", GameCommands.Parse("/musik-start")!.Mode); Assert.Equal("ende", GameCommands.Parse("/musik-ende")!.Mode);
        Assert.Equal(6, GameCommands.All.Length); Assert.Null(GameCommands.Parse("mach den Wald dichter")); Assert.Null(GameCommands.Parse("/unbekannt x"));
    }
}
