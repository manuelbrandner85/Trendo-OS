using Avalonia.Controls;
using Avalonia;
using Avalonia.Headless;
using Avalonia.VisualTree;
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
    [AvaloniaFact(DisplayName = "Spiel-Editor wie GDevelop/Construct: neue Kacheln malen (Feder, Speicherpunkt, Schlüssel, Tür), Regel „Wenn … dann …“ anlegen und löschen, Zeitlimit; Probespiel zeigt Schlüssel und Restzeit; Bauen schreibt Regeln")]
    public void Rules_and_tiles()
    {
        var (w, _, root) = Ui.Open(); w.Width = 1600; w.Height = 1000;
        try
        {
            w.Show(GK); w.Find<Button>("prog-tpl-retro-plattform").Press();
            w.GameSection = "Level"; w.Show(GK);
            var d = w.ProgramActive(GK)!;
            foreach (var t in new[] { "feder", "speicher", "schluessel", "tuer" }) Assert.NotNull(w.TryFind<Button>("game-tile-" + t));
            // Schlüssel und Tür in das Standardlevel malen (Zeile 8 ist die Laufebene)
            void PaintAt(string tile, int col, int row)
            {
                w.Find<Button>("game-tile-" + tile).Press(); StartMenu_Ui.Settle(); var lc = w.GameLevelForTests!;
                lc.BringIntoView(new Rect(lc.CenterOf(col, row), new Size(1, 1))); StartMenu_Ui.Settle(); var pt = lc.TranslatePoint(lc.CenterOf(col, row), w)!.Value;
                w.MouseDown(pt, MouseButton.Left); w.MouseUp(pt, MouseButton.Left); StartMenu_Ui.Settle();
            }
            PaintAt("schluessel", 5, 8); PaintAt("tuer", 7, 8);
            Assert.Equal('K', d.List("level")[8]!.GetValue<string>()[5]); Assert.Equal('D', d.List("level")[8]!.GetValue<string>()[7]);
            w.Find<Button>("game-tile-auswahl").Press(); w.GameLevelForTests!.Pick(7, 8);
            Assert.StartsWith("Tür", w.Find<TextBlock>("game-obj-name").Text);
            // Regel anlegen: Wenn Münzen ≥ 3, dann +1 Leben
            w.Find<ComboBox>("game-rule-when").SelectedIndex = 0; w.Find<TextBox>("game-rule-n").Text = "3"; w.Find<ComboBox>("game-rule-do").SelectedIndex = 0;
            w.Find<Button>("game-rule-add").Press();
            Assert.Equal("Regel", d.UndoLabel);
            Assert.Contains("Wenn Münzen gesammelt ≥ 3, dann +1 Leben", string.Join("|", w.GetLogicalTexts()));
            w.Find<TextBox>("game-rule-n").Text = "x"; w.Find<Button>("game-rule-add").Press();
            Assert.Single(GameDesign.Rules(d.Data)); // falsche Zahl: keine Regel
            Enter(w.Find<TextBox>("game-set-time"), "120");
            Assert.Equal(120, d.Number("timeLimit"));
            // Probespiel: Schlüssel und Restzeit in der Anzeige
            w.Find<Button>("game-level-play").Press();
            var sim = w.GamePlaytest!; Assert.Equal(1, sim.KeysTotal);
            for (var i = 0; i < 10; i++) w.GamePlaytestCanvas!.Tick();
            var st = w.Find<TextBlock>("game-play-status").Text!;
            Assert.Contains("Schlüssel 0/1", st); Assert.Contains("noch 120 s", st);
            w.GamePlaytestCanvas!.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Escape });
            // Regel löschen und wieder herstellen
            w.Find<Button>("game-rule-del-0").Press(); Assert.Empty(GameDesign.Rules(d.Data));
            d.Undo(); Assert.Single(GameDesign.Rules(d.Data));
            // Bauen: config.js mit Regeln und Zeitlimit
            var dir = GameDesign.BuildBrowser(d.Data, root);
            var cfg = File.ReadAllText(Path.Combine(dir, "config.js"));
            Assert.Contains("\"timeLimit\":120", cfg); Assert.Contains("\"when\":\"muenzen\",\"n\":3,\"act\":\"leben\"", cfg);
        }
        finally { w.Close(); }
    }
}
