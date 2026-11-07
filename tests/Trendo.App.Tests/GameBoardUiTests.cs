using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Trendo.App;
using Trendo.Core.Programs;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>S2: Spiele-Projekt-Dashboard – Spielvorschau (vor dem Bauen ein klarer nächster Schritt) und Aufgaben-Board mit drei Spalten.</summary>
public class GameBoard_Ui
{
    const string GK = "spiele-programm";
    static void Pump() { for (var i = 0; i < 8; i++) Dispatcher.UIThread.RunJobs(); }

    [AvaloniaFact(DisplayName = "Spiele-Dashboard: Vorschau-Karte, Aufgabe anlegen, weiter/zurück schieben, Hinweise übernehmen, rückgängig")]
    public void Board_in_overview()
    {
        var (w, _, _) = Ui.Open();
        var d = w.ProgramNew(GK, GameDesign.Templates[0].Id);
        w.GameSection = "Übersicht"; w.Show(GK); Pump();
        Assert.NotNull(w.Find<Border>("game-preview-empty")); // noch nicht gebaut: kein Bild, sondern nächster Schritt
        Assert.Equal("Bauen", w.Find<Button>("game-preview-play").Content);
        w.Find<TextBox>("game-task-new").Text = "Boss-Arena bauen";
        w.Find<TextBox>("game-task-new").RaiseEvent(new Avalonia.Input.KeyEventArgs { RoutedEvent = Avalonia.Input.InputElement.KeyDownEvent, Key = Avalonia.Input.Key.Enter }); Pump();
        Assert.Single(GameBoard.Tasks(d.Data)); Assert.Equal("Offen", GameBoard.StateOf(GameBoard.Tasks(d.Data)[0]));
        w.Find<Button>("game-task-0-next").Press(); Pump();
        Assert.Equal("In Arbeit", GameBoard.StateOf(GameBoard.Tasks(d.Data)[0]));
        w.Find<Button>("game-task-0-next").Press(); Pump();
        Assert.Equal("Fertig", GameBoard.StateOf(GameBoard.Tasks(d.Data)[0]));
        w.Find<Button>("game-task-0-back").Press(); Pump();
        Assert.Equal("In Arbeit", GameBoard.StateOf(GameBoard.Tasks(d.Data)[0]));
        w.Find<Button>("game-task-seed").Press(); Pump();
        Assert.True(GameBoard.Tasks(d.Data).Count > 1); // offene Meilensteine kommen als Aufgaben dazu
        Assert.Contains(GameBoard.Tasks(d.Data), t => t["source"]!.GetValue<string>() == "Meilenstein");
        var before = GameBoard.Tasks(d.Data).Count;
        w.Find<Button>("prog-undo").Press(); Pump();
        Assert.Equal(1, GameBoard.Tasks(d.Data).Count); Assert.NotEqual(before, GameBoard.Tasks(d.Data).Count); // Übernahme rückgängig
    }
}
