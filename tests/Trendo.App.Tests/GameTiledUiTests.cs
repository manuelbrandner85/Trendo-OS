using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Trendo.Core;
using Trendo.Core.Programs;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>Alle Studios, Runde 7 – CORE-06: „Als Tiled-Karten ausgeben“ im Spiel-Editor.</summary>
public class GameTiled_Ui
{
    const string GK = "spiele-programm";

    [AvaloniaFact(DisplayName = "Tiled-Ausgabe: Plattform-Spiel mit 2 Leveln → 2 .tmj + Kachelbild + Welt; Level 1 zurückgelesen gleich; Münzwert aus Level 2 steht am Objekt")]
    public void Export()
    {
        var (w, _, _) = Ui.Open();
        try
        {
            w.Show(GK); w.Find<Button>("prog-tpl-retro-plattform").Press();
            w.GameSection = "Level"; w.Show(GK);
            var d = w.ProgramActive(GK)!;
            w.Find<Button>("game-lv-add").Press();
            var rows2 = MainWindow.GameLevelRows(d, 1); var cy = rows2.FindIndex(r => r.Contains('o')); var cx = rows2[cy].IndexOf('o');
            d.Edit("Münzwert", x => { var o = x["objects"] as System.Text.Json.Nodes.JsonObject ?? []; o[$"2:{cx},{cy}:value"] = 7; o["1,1:value"] = 99; x["objects"] = o; });
            w.Show(GK);
            w.Find<Button>("game-tiled").Press();
            Assert.Contains("2 Level als Tiled-Karten", w.ProgramMessage(GK));
            var dir = w.GameExportTiled(d); // gleicher Ordner, gibt den Pfad zurück
            var files = Directory.GetFiles(dir).Select(Path.GetFileName).Order().ToList();
            Assert.Equal(4, files.Count); Assert.Contains("spiel.world", files); Assert.Contains(TiledExport.TilesetImage, files);
            var tmj = Directory.GetFiles(dir, "*.tmj").Order().ToList();
            Assert.Equal(MainWindow.GameLevelRows(d, 0).Select(r => r.TrimEnd('.')), TiledExport.ToRows(File.ReadAllText(tmj[0])));
            var map2 = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(tmj[1]))!;
            var coin = map2["layers"]![2]!["objects"]!.AsArray().First(o => o!["x"]!.GetValue<int>() == cx * 32 && o["y"]!.GetValue<int>() == (cy + 1) * 32)!;
            Assert.Equal(7, coin["properties"]![0]!["value"]!.GetValue<double>());
            var allProps = map2["layers"]![2]!["objects"]!.AsArray().SelectMany(o => o!["properties"]?.AsArray() ?? []).Select(p => p!["value"]!.ToJsonString());
            Assert.DoesNotContain("99", allProps); // Eigenschaft aus Level 1 landet nicht in Level 2
        }
        finally { w.Close(); }
    }
}
