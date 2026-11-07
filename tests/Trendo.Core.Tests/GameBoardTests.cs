using System.Text.Json.Nodes;
using Trendo.Core.Programs;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>S2: Aufgaben-Board im Spielprojekt und Spielvorschau aus dem Prüflauf.</summary>
public class GameBoardTests
{
    [Fact(DisplayName = "Spiele-Board: anlegen ohne Doppelte, schieben bis an den Rand, Fortschritt, Übernahme aus Prüfung und Meilensteinen")]
    public void Board_logic()
    {
        var d = new JsonObject { ["milestones"] = new JsonArray(new JsonObject { ["name"] = "Prototyp", ["done"] = false }, new JsonObject { ["name"] = "Alpha", ["done"] = true }) };
        Assert.True(GameBoard.Add(d, "Boss bauen")); Assert.False(GameBoard.Add(d, " boss bauen ")); Assert.False(GameBoard.Add(d, "  "));
        GameBoard.Move(d, 0, -1); Assert.Equal("Offen", GameBoard.StateOf(GameBoard.Tasks(d)[0]));
        GameBoard.Move(d, 0, +1); GameBoard.Move(d, 0, +1); GameBoard.Move(d, 0, +1); Assert.Equal("Fertig", GameBoard.StateOf(GameBoard.Tasks(d)[0]));
        Assert.Equal(100, GameBoard.Progress(d));
        Assert.True(GameBoard.Add(d, "Boss bauen")); // erledigte Aufgabe darf neu kommen
        var n = GameBoard.Seed(d);
        Assert.Contains(GameBoard.Tasks(d), t => t["title"]!.GetValue<string>() == "Prototyp");
        Assert.DoesNotContain(GameBoard.Tasks(d), t => t["title"]!.GetValue<string>() == "Alpha"); // erledigter Meilenstein nicht
        Assert.Equal(0, GameBoard.Seed(d)); Assert.True(n >= 1); // zweimal übernehmen bringt nichts Doppeltes
        GameBoard.Remove(d, 0); Assert.DoesNotContain(GameBoard.Tasks(d), t => GameBoard.StateOf(t) == "Fertig");
    }

    [Fact(DisplayName = "Spiele-Vorschau: Bild aus dem Prüflauf (trendo-start.png bevorzugt), ohne Prüflauf keines")]
    public void Preview_from_check_run()
    {
        var dir = Path.Combine(Path.GetTempPath(), "trendo-board-" + Guid.NewGuid().ToString("N"));
        try
        {
            Assert.Null(GameBoard.Preview("")); Assert.Null(GameBoard.Preview(dir));
            var shots = Directory.CreateDirectory(Path.Combine(dir, "Saved", "Screenshots")).FullName;
            Assert.Null(GameBoard.Preview(dir));
            File.WriteAllBytes(Path.Combine(shots, "trendo-kampf.png"), [1]);
            Assert.EndsWith("trendo-kampf.png", GameBoard.Preview(dir));
            File.WriteAllBytes(Path.Combine(shots, "trendo-start.png"), [1]);
            Assert.EndsWith("trendo-start.png", GameBoard.Preview(dir));
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }
}
