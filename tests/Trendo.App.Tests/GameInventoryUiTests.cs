using System.Text.Json.Nodes;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Trendo.App;
using Trendo.Core.Programs;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>Paket 01 / S1-05 (Teil 2): Gegenstand und sprechende Figur im Editor – Werkzeuge, Texte, Wunsch und Belohnung, Probespiel mit Inventar.</summary>
public class GameInventory_Ui
{
    const string GK = "spiele-programm";
    static void Pump() { for (var i = 0; i < 5; i++) Dispatcher.UIThread.RunJobs(); }
    static void Type(MainWindow w, string name, string text) { var t = w.Find<TextBox>(name); t.Text = text; t.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter }); Pump(); }

    [AvaloniaFact(DisplayName = "Inventar Editor: Werkzeuge Gegenstand/Figur; Gegenstand „Apfel“ benennen; Figur sagt Text, wünscht Apfel, gibt +1 Leben; Rückgängig; Probespiel zeigt Inventar")]
    public void Item_and_npc()
    {
        var (w, _, _) = Ui.Open();
        w.Show(GK); Pump(); w.Find<Button>("prog-tpl-retro-plattform").Press(); Pump();
        var d = w.ProgramActive(GK)!;
        w.GameSection = "Level"; w.Show(GK); Pump();
        Assert.NotNull(w.TryFind<Button>("game-tile-gegenstand")); Assert.NotNull(w.TryFind<Button>("game-tile-figur"));
        // Start-Zeile suchen und rechts daneben Gegenstand und Figur setzen
        var rows = MainWindow.GameLevelRows(d, 0); var sy = rows.FindIndex(r => r.Contains('S')); var sx = rows[sy].IndexOf('S');
        var row = rows[sy].ToCharArray(); row[sx + 1] = 'G'; row[sx + 3] = 'N';
        d.Set("level", new JsonArray([.. rows.Select((r, i) => (JsonNode)(i == sy ? new string(row) : r))]), "Level malen"); Pump();
        w.GameObjSel = (sx + 1, sy); w.Show(GK); Pump();
        Type(w, "game-obj-item", "Apfel");
        Assert.Equal("Apfel", GameDesign.Texts(d.Data)[$"{sx + 1},{sy}:name"]);
        w.GameObjSel = (sx + 3, sy); w.Show(GK); Pump();
        Type(w, "game-obj-say", "Ich habe Hunger!");
        var need = w.Find<ComboBox>("game-obj-need"); need.SelectedIndex = ((List<string>)need.ItemsSource!).IndexOf("Apfel"); Pump();
        Assert.Equal("Apfel", GameDesign.Texts(d.Data)[$"{sx + 3},{sy}:need"]);
        w.Show(GK); Pump();
        Type(w, "game-obj-thanks", "Lecker!");
        var give = w.Find<ComboBox>("game-obj-give"); give.SelectedIndex = 0; Pump(); // +1 Leben
        Assert.Equal("leben", GameDesign.Texts(d.Data)[$"{sx + 3},{sy}:give"]);
        w.Find<Button>("prog-undo").Press(); Pump();
        Assert.False(GameDesign.Texts(d.Data).ContainsKey($"{sx + 3},{sy}:give"));
        w.GamePlayHere(d); w.Show(GK); Pump();
        var sim = w.GamePlaytestCanvas!.Sim;
        for (var i = 0; i < 300 && sim.Inventory.Count == 0; i++) w.GamePlaytestCanvas.Tick(false, true, false);
        Assert.Contains("Inventar: Apfel", w.Find<TextBlock>("game-play-status").Text);
        w.Close();
    }
}
