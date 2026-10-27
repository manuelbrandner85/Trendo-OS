using System.Text.Json.Nodes;
using Trendo.Core.Programs;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>Spiele erstellen – Retro bis AAA+: Vorlagen, Prüfungen, Spielbibel, Bauen.</summary>
public class GameDesignTests
{
    static string Tmp() { var d = Path.Combine(Path.GetTempPath(), "trendo-game-" + Guid.NewGuid().ToString("N")[..8]); Directory.CreateDirectory(d); return d; }

    [Fact(DisplayName = "Spiele: jede Vorlage hat Umfang, Meilensteine und keine Sackgassen; AAA+ mit Unreal")]
    public void Templates()
    {
        foreach (var t in GameDesign.Templates)
        {
            var d = t.Bible();
            Assert.Equal(GameDesign.Milestones((string)d["scale"]!).Length, d["milestones"]!.AsArray().Count);
            Assert.DoesNotContain(GameDesign.Check(d), w => w.Contains("Sackgasse") || w.Contains("Auftraggeber"));
        }
        Assert.Equal("unreal", GameDesign.ScaleOf("aaa").Engine);
        Assert.Contains("Gold Master", GameDesign.Milestones("aaa"));
        Assert.True(GameDesign.Overall(GameDesign.Templates.First(t => t.Id == "openworld").Bible()) >= 70);
    }

    [Fact(DisplayName = "Spiele: Prüfung findet Boss ohne Phasen, Missions-Sackgasse, unbekannten Auftraggeber und fehlende Hauptmission")]
    public void Checks()
    {
        var d = GameDesign.Templates.First(t => t.Id == "topdown").Bible();
        ((JsonObject)d["characters"]!.AsArray()[1]!)["phases"] = 1;
        ((JsonObject)d["quests"]!.AsArray()[1]!)["gives"] = "";
        ((JsonObject)d["quests"]!.AsArray()[0]!)["giver"] = "Unbekannt";
        var w = GameDesign.Check(d);
        Assert.Contains(w, x => x.Contains("Moosgeist") && x.Contains("Phasen"));
        Assert.Contains(w, x => x.Contains("Sackgasse") && x.Contains("Laterne"));
        Assert.Contains(w, x => x.Contains("Unbekannt"));
        d["quests"] = new JsonArray();
        Assert.Contains(GameDesign.Check(d), x => x.Contains("keine Hauptmission"));
    }

    [Fact(DisplayName = "Spiele: Spielbibel als Markdown ohne Programmnamen; Retro baut ein spielbares Browser-Spiel mit eigenem Level")]
    public void Bible_and_browser()
    {
        var root = Tmp();
        try
        {
            var d = GameDesign.Templates.First(t => t.Id == "retro-jump").Bible();
            d["level"] = new JsonArray("..#..#....##..");
            var md = GameDesign.BibleMarkdown(d);
            Assert.Contains("# Pixel-Sprung", md); Assert.Contains("## Meilensteine", md); Assert.DoesNotContain("Trendo", md);
            var dir = GameDesign.BuildBrowser(d, root);
            Assert.True(File.Exists(Path.Combine(dir, "index.html")));
            Assert.Contains("..#..#....##..", File.ReadAllText(Path.Combine(dir, "config.js")));
            Assert.DoesNotContain("Trendo", File.ReadAllText(Path.Combine(dir, "README.md")));
            d["level"] = new JsonArray("..#..##");
            Assert.Equal(dir, GameDesign.BuildBrowser(d, root)); // erneut bauen aktualisiert dasselbe Spiel
            Assert.Contains("..#..##", File.ReadAllText(Path.Combine(dir, "config.js")));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact(DisplayName = "Spiele: AAA+ baut ein Unreal-Projekt mit Spielbibel und Lumen/virtuellen Schatten")]
    public void Unreal()
    {
        var root = Tmp();
        try
        {
            var d = GameDesign.Templates.First(t => t.Id == "openworld").Bible();
            var dir = GameDesign.BuildUnreal(d, root, "5.4");
            Assert.Single(Directory.GetFiles(dir, "*.uproject"));
            Assert.Contains("Erbe der Sterne", File.ReadAllText(Path.Combine(dir, "Design", "Spielbibel.md")));
            var ini = File.ReadAllText(Path.Combine(dir, "Config", "DefaultEngine.ini"));
            Assert.Contains("r.DynamicGlobalIlluminationMethod=1", ini); Assert.Contains("r.Shadow.Virtual.Enable=1", ini);
        }
        finally { Directory.Delete(root, true); }
    }
}
