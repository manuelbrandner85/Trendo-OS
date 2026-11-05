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
    static void Paint(LevelCanvas c, int x, int y, char ch)
    {
        var l = c.Rows.ToList(); var r = l[y].ToCharArray(); r[x] = ch; l[y] = new string(r);
        c.LevelChanged!(l); Avalonia.Threading.Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact(DisplayName = "CORE-06: Spiel-Editor mit mehreren Leveln – Level hinzufügen, umbenennen, wechseln, früher schieben, löschen (Eigenschaften rücken nach); Probespiel läuft durch alle Level")]
    public void Mehrere_level()
    {
        var (w, _, _) = Ui.Open();
        try
        {
            w.Show(GK); w.Find<Button>("prog-tpl-retro-plattform").Press();
            w.GameSection = "Level"; w.Show(GK);
            var d = w.ProgramActive(GK)!;
            Assert.NotNull(w.TryFind<Button>("game-lv-1")); Assert.Null(w.TryFind<Button>("game-lv-del")); // erstes Level bleibt
            w.Find<Button>("game-lv-add").Press();
            Assert.Equal(2, Trendo.App.MainWindow.GameLevelCount(d)); Assert.Equal(1, w.GameLevelIndex);
            Enter(w.Find<TextBox>("game-lv-name"), "Höhle");
            Assert.Equal("Höhle", w.Find<Button>("game-lv-2").Content);
            // Objekt in Level 2: Münzwert landet unter „2:x,y:value“
            w.Find<Button>("game-tile-auswahl").Press();
            var rows = w.GameLevelForTests!.Rows; var cy = rows.FindIndex(r => r.Contains('o')); var cx = rows[cy].IndexOf('o');
            w.GameLevelForTests!.Pick(cx, cy); Enter(w.Find<TextBox>("game-obj-value"), "9");
            Assert.Equal(9, ProgramDocument.ToNumber(d.Data["objects"]![$"2:{cx},{cy}:value"]));
            w.Find<Button>("game-lv-add").Press(); Assert.Equal(3, Trendo.App.MainWindow.GameLevelCount(d));
            w.Find<Button>("game-lv-up").Press(); // Level 3 vor „Höhle“
            Assert.Equal("Höhle", Trendo.App.MainWindow.GameLevelName(d, 2)); Assert.Equal(1, w.GameLevelIndex);
            d.Undo(); Assert.Equal("Höhle", Trendo.App.MainWindow.GameLevelName(d, 1)); w.Show(GK);
            // Probespiel kennt alle Level
            w.Find<Button>("game-lv-1").Press(); w.Find<Button>("game-level-play").Press();
            Assert.Equal(3, w.GamePlaytest!.LevelCount);
            w.Find<Button>("game-play-stop").Press();
            // Level 2 löschen: Eigenschaften von Level 2 verschwinden
            w.Find<Button>("game-lv-2").Press(); w.Find<Button>("game-lv-del").Press();
            Assert.Equal(2, Trendo.App.MainWindow.GameLevelCount(d)); Assert.Null(d.Data["objects"]?[$"2:{cx},{cy}:value"]);
            Assert.DoesNotContain("Höhle", Trendo.App.MainWindow.GameLevelName(d, 1));
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "CORE-06: Spiel-Editor – Hintergrund-Ebene nur mit Deko (Wolke, Baum …), Spiel-Ebene bleibt unverändert, andere Ebene blass sichtbar; Bauen übernimmt den Hintergrund")]
    public void Hintergrund_ebene()
    {
        var (w, _, _) = Ui.Open();
        try
        {
            w.Show(GK); w.Find<Button>("prog-tpl-retro-plattform").Press();
            w.GameSection = "Level"; w.Show(GK);
            var d = w.ProgramActive(GK)!;
            var before = string.Join("|", Trendo.App.MainWindow.GameLevelRows(d, 0));
            Assert.NotNull(w.GameLevelForTests!.Ghost);
            w.Find<Button>("game-layer-hintergrund").Press();
            Assert.NotNull(w.TryFind<Button>("game-tile-wolke")); Assert.Null(w.TryFind<Button>("game-tile-gegner"));
            Assert.Equal(before, string.Join("|", w.GameLevelForTests!.Ghost!)); // Spiel-Ebene blass darunter
            w.Find<Button>("game-tile-baum").Press();
            Paint(w.GameLevelForTests!, 2, 1, 't');
            Assert.Equal('t', Trendo.App.MainWindow.GameDecorRows(d, 0)[1][2]);
            Assert.Equal(before, string.Join("|", Trendo.App.MainWindow.GameLevelRows(d, 0)));
            w.Find<Button>("game-layer-spiel").Press(); Assert.NotNull(w.TryFind<Button>("game-tile-gegner"));
            var cfg = File.ReadAllText(Path.Combine(GameDesign.BuildBrowser(d.Data, Path.Combine(w.Core.DataRoot, "spiele-test")), "config.js"));
            Assert.Contains("\"decor\":[[", cfg); Assert.Contains("..t", cfg);
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "CORE-06: Spiel-Editor – Animation je Zustand: Bildstreifen wählen, Bilder und Tempo setzen (begrenzt), Probespiel zeigt Laufen-Bilder nacheinander, Entfernen nimmt sie raus; Bauen kopiert den Streifen ins Spiel")]
    public void Animationen()
    {
        var (w, _, root) = Ui.Open();
        try
        {
            var strip = Path.Combine(root, "lauf.png");
            using (var bmp = new Avalonia.Media.Imaging.RenderTargetBitmap(new PixelSize(96, 32))) bmp.Save(strip);
            w.PickOverride = _ => Task.FromResult<string[]?>([strip]);
            w.Show(GK); w.Find<Button>("prog-tpl-retro-plattform").Press();
            w.GameSection = "Level"; w.Show(GK);
            var d = w.ProgramActive(GK)!;
            Assert.NotNull(w.TryFind<StackPanel>("game-anim-panel"));
            w.Find<Button>("game-anim-pick-laufen").Press(); Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            Assert.Equal(strip, d.Data["anims"]!["laufen"]!["file"]!.GetValue<string>());
            Enter(w.Find<TextBox>("game-anim-frames-laufen"), "3");
            Enter(w.Find<TextBox>("game-anim-fps-laufen"), "99");
            Assert.Equal((3, 30), ((int)ProgramDocument.ToNumber(d.Data["anims"]!["laufen"]!["frames"]), (int)ProgramDocument.ToNumber(d.Data["anims"]!["laufen"]!["fps"])));
            Assert.ThrowsAny<Exception>(() => w.GameSetAnim(d, "tanzen", strip, 1, 8));
            // Probespiel: beim Laufen wechseln die Bilder
            w.Find<Button>("game-level-play").Press();
            var c = w.GamePlaytestCanvas!; Assert.Contains("laufen", c.Anims.Keys);
            for (var i = 0; i < 30; i++) c.Tick();
            var seen = new HashSet<int>();
            for (var i = 0; i < 12; i++) { c.Tick(right: true); var f = c.CurrentFrame(); if (f.State == "laufen") seen.Add(f.Frame); }
            Assert.True(seen.Count >= 2, "Laufen-Animation wechselt die Bilder: " + string.Join(",", seen));
            w.Find<Button>("game-play-stop").Press();
            Assert.True(File.Exists(Path.Combine(GameDesign.BuildBrowser(d.Data, Path.Combine(root, "spiele-test")), "figur-laufen.png")));
            w.Find<Button>("game-anim-del-laufen").Press();
            Assert.Null(d.Data["anims"]?["laufen"]);
        }
        finally { w.Close(); }
    }
    [AvaloniaFact(DisplayName = "CORE-06: Profi-Ablauf – Phasen-Leiste mit Abnahme (gesperrt, bis alles erfüllt ist), Rollen mit Arbeitsplatz, Test-Bereich prüft jedes Level und zeigt die Stelle, Notiz und Fehler aus dem Probespiel, Ton-Spur; Claude bekommt den Prüfbericht")]
    public void Profi_ablauf()
    {
        var (w, _, root) = Ui.Open();
        try
        {
            w.Show(GK); w.Find<Button>("prog-tpl-retro-plattform").Press();
            var d = w.ProgramActive(GK)!;
            // Phasen
            w.GameSection = "Produktion"; w.Show(GK);
            Assert.NotNull(w.TryFind<WrapPanel>("game-phases"));
            d.Edit("Zielgruppe", x => x["audience"] = ""); w.Show(GK);
            Assert.False(w.Find<Button>("game-phase-accept").IsEnabled);
            Enter(w.Find<TextBox>("game-role-level"), "Erika Muster");
            Assert.Equal("Erika Muster", d.Data["roles"]!["level"]!.GetValue<string>());
            d.Edit("Zielgruppe", x => x["audience"] = "Kinder ab 8"); w.Show(GK);
            Assert.True(w.Find<Button>("game-phase-accept").IsEnabled);
            w.Find<Button>("game-phase-accept").Press();
            Assert.True(GamePro.Accepted(d.Data, "konzept")); Assert.Equal("prototyp", GamePro.Current(d.Data).Id);
            w.Find<Button>("game-phase-reopen").Press(); Assert.False(GamePro.Accepted(d.Data, "konzept"));
            w.Find<Button>("game-role-open-level").Press(); Assert.Equal("Level", w.GameSection);
            // Test-Bereich: Prüfung je Level, „Zeigen“ springt zur Stelle
            d.Edit("Level", x => x["level"] = new System.Text.Json.Nodes.JsonArray("..........", "..........", "..........", "S....E...Z", "###.......###"[..10]));
            w.GameSection = "Test"; w.Show(GK);
            Assert.Contains("nicht erreichbar", w.Find<TextBlock>("game-fair-1").Text);
            w.Find<Button>("game-fair-show-1-0").Press();
            Assert.Equal("Level", w.GameSection); Assert.NotNull(w.GameObjSel);
            // Notiz und Fehler aus dem Probespiel
            d.Edit("Level", x => x["level"] = new System.Text.Json.Nodes.JsonArray([.. GameStudio.DefaultPlatform.Select(r => (System.Text.Json.Nodes.JsonNode)r)])); w.Show(GK);
            w.Find<Button>("game-level-play").Press();
            Enter(w.Find<TextBox>("game-play-note"), "Sprung hier zu weit");
            w.Find<Button>("game-play-bug-add").Press();
            var note = (System.Text.Json.Nodes.JsonObject)d.List("testnotes")[0]!;
            Assert.Equal(("fehler", 1), (note["kind"]!.GetValue<string>(), (int)ProgramDocument.ToNumber(note["level"])));
            w.Find<Button>("game-play-stop").Press();
            w.GameSection = "Test"; w.Show(GK);
            Assert.Contains("offene", string.Join("|", w.GetLogicalTexts()));
            w.Find<CheckBox>("game-note-done-0").IsChecked = true; Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            Assert.True(d.List("testnotes")[0]!["done"]!.GetValue<bool>());
            // Ton-Spur
            var wav = Path.Combine(root, "sprung.wav"); File.WriteAllBytes(wav, [0x52, 0x49, 0x46, 0x46]);
            w.PickOverride = _ => Task.FromResult<string[]?>([wav]);
            w.GameSection = "Level"; w.Show(GK);
            w.Find<Button>("game-sound-pick-springen").Press(); Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            Assert.Equal(wav, d.Data["sounds"]!["springen"]!.GetValue<string>());
            Assert.Throws<ArgumentException>(() => w.GameSetSound(d, "knall", wav));
            w.Find<Button>("game-sound-del-springen").Press(); Assert.Null(d.Data["sounds"]?["springen"]);
            // Claude sieht den Prüfbericht
            Assert.Contains("Spielbarkeits-Prüfung", w.ClaudeFocus(GK));
        }
        finally { w.Close(); }
    }
}
