using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Trendo.App;
using Trendo.Core;
using Trendo.Core.Programs;
using Xunit;

namespace Trendo.App.Tests;

/// Runde 7: Spiele bauen als Spiel-Editor – Objekte auswählen, Eigenschaften-Leiste (Gegner-Tempo, Münzwert, Spielregeln), Probespielen im Fenster.
public class SpielEditor_Ui
{
    const string GK = "spiele-programm";

    static void Enter(TextBox t, string v) { t.Text = v; t.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter }); Avalonia.Threading.Dispatcher.UIThread.RunJobs(); }

    [AvaloniaFact(DisplayName = "Spiel-Editor: Gegner auswählen und Tempo setzen (Rückgängig), Münzwert, Spielregeln; Probespiel im Fenster läuft mit Tasten, Nochmal, Zurück; Bauen übernimmt die Eigenschaften")]
    public void Spiel_editor()
    {
        var (w, _, _) = Ui.Open();
        w.Show(GK); w.Find<Button>("prog-tpl-retro-plattform").Press();
        w.GameSection = "Level"; w.Show(GK);
        var d = w.ProgramActive(GK)!;
        Assert.Contains("Werkzeug „Auswählen“", string.Join("|", w.GetLogicalTexts()));
        w.Find<Button>("game-tile-auswahl").Press();
        w.GameLevelForTests!.Pick(18, 8); // Gegner im Standardlevel
        Assert.StartsWith("Gegner", w.Find<TextBlock>("game-obj-name").Text);
        Enter(w.Find<TextBox>("game-obj-speed"), "2,5");
        Assert.Equal(2.5, ProgramDocument.ToNumber(d.Data["objects"]!["18,8:speed"]));
        Assert.Equal('E', w.GameLevelForTests!.Rows[8][18]); // Auswählen malt nicht
        d.Undo(); Assert.Null(d.Data["objects"]?["18,8:speed"]); w.Show(GK);
        Enter(w.Find<TextBox>("game-obj-speed"), "9"); Assert.Equal(4, ProgramDocument.ToNumber(d.Data["objects"]!["18,8:speed"])); // begrenzt
        // Münze
        w.GameLevelForTests!.Pick(19, 4); Assert.StartsWith("Münze", w.Find<TextBlock>("game-obj-name").Text);
        Enter(w.Find<TextBox>("game-obj-value"), "5");
        Enter(w.Find<TextBox>("game-set-lives"), "5");
        Assert.Equal(5, d.Number("lives"));
        // Probespiel
        w.Find<Button>("game-level-play").Press();
        var sim = w.GamePlaytest!; Assert.Equal(5, sim.Lives);
        Assert.Equal(5, sim.Coins.Single(c => c.Col == 19 && c.Row == 4).Value);
        Assert.Equal(4, sim.Foes.Single(f => f.Col == 18).Speed);
        var canvas = w.GamePlaytestCanvas!; Assert.NotNull(w.TryFind<PlaytestCanvas>("game-playtest"));
        for (var i = 0; i < 30; i++) canvas.Tick();
        var x0 = sim.Player.X;
        canvas.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Right });
        for (var i = 0; i < 20; i++) canvas.Tick();
        Assert.True(sim.Player.X > x0 + 40, $"{x0} → {sim.Player.X}");
        Assert.Contains("Leben", w.Find<TextBlock>("game-play-status").Text);
        w.Find<Button>("game-play-again").Press(); Assert.NotSame(sim, w.GamePlaytest); Assert.Equal(5, w.GamePlaytest!.Lives);
        w.GamePlaytestCanvas!.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Escape });
        Assert.Null(w.GamePlaytest); Assert.NotNull(w.TryFind<WrapPanel>("game-tiles"));
        Assert.Contains(w.Core.Audit.ReadAll(), a => a.Action == "game.playtest");
        // Objekt entfernen nimmt auch seine Eigenschaften mit
        w.Find<Button>("game-tile-auswahl").Press(); w.GameLevelForTests!.Pick(18, 8); w.Find<Button>("game-obj-del").Press();
        Assert.Equal('.', d.List("level")[8]!.GetValue<string>()[18]); Assert.Null(d.Data["objects"]?["18,8:speed"]);
        w.Close();
    }
}
