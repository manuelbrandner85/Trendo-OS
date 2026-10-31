using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Trendo.App;
using Trendo.Core;
using Trendo.Core.Programs;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// <summary>CORE-06 / Masterprompt 131: Spiele-Werkstatt mit Level-Editor – Kacheln mit der Maus malen, radieren, Rückgängig, Prüfung, Testen baut ein spielbares Spiel.</summary>
public class LevelEditor_Ui
{
    const string GK = "spiele-programm";
    static Point Cell(MainWindow w, int col, int row) { Settle(); var c = w.GameLevelForTests!; c.BringIntoView(new Rect(c.CenterOf(col, row), new Size(1, 1))); Settle(); return c.TranslatePoint(c.CenterOf(col, row), w)!.Value; }
    static List<string> Level(MainWindow w) => w.ProgramActive(GK)!.List("level").Select(x => x!.GetValue<string>()).ToList();
    static void Click(MainWindow w, string name) { var b = V<Button>(w, name); b.BringIntoView(); Settle(); RealClick(w, V<Button>(w, name)); }

    [AvaloniaFact(DisplayName = "Level-Editor: Münzen ziehend malen, Start versetzen (nur einer), rechts radieren, Rückgängig je Strich, Prüfung meldet fehlendes Ziel, Testen baut das Spiel")]
    public void Paint_level()
    {
        var (w, l, _) = Ui.Open(); w.Width = 1600; w.Height = 1400;
        w.Show(GK); Settle(); RealClick(w, V<Button>(w, "prog-tpl-retro-plattform"));
        w.GameSection = "Level"; w.Show(GK); Settle();
        Assert.Equal(GameStudio.DefaultPlatform, Level(w));
        // Münzen in Zeile 2, Spalten 2–6 ziehend malen
        Click(w, "game-tile-muenze");
        var a = Cell(w, 2, 2); var b = Cell(w, 6, 2);
        w.MouseDown(a, MouseButton.Left); for (var i = 1; i <= 8; i++) { w.MouseMove(a + (b - a) * (i / 8.0)); Settle(); } w.MouseUp(b, MouseButton.Left); Settle();
        Assert.Equal("..ooooo", Level(w)[2][..7]);
        // Start auf Spalte 3, Zeile 8: alter Start verschwindet
        Click(w, "game-tile-start"); var s = Cell(w, 3, 8); w.MouseDown(s, MouseButton.Left); w.MouseUp(s, MouseButton.Left); Settle();
        Assert.Equal(1, string.Concat(Level(w)).Count(ch => ch == 'S')); Assert.Equal('S', Level(w)[8][3]);
        // Ziel rechts radieren → Prüfung meldet es
        var z = Level(w).FindIndex(r => r.Contains('Z')); var zc = Level(w)[z].IndexOf('Z');
        var zp = Cell(w, zc, z); w.MouseDown(zp, MouseButton.Right); w.MouseUp(zp, MouseButton.Right); Settle();
        Assert.DoesNotContain('Z', string.Concat(Level(w)));
        Assert.Contains("Kein Ziel", string.Join(" ", w.Find<StackPanel>("game-level-hints").Children.OfType<TextBlock>().Select(t => t.Text)));
        // Rückgängig: je Strich ein Schritt
        var d = w.ProgramActive(GK)!;
        Assert.Equal("Level malen", d.UndoLabel); d.Undo(); w.Show(GK); Settle(); Assert.Contains('Z', string.Concat(Level(w)));
        // Testen: baut ein Browser-Spiel mit diesem Level und öffnet es
        Click(w, "game-level-test");
        var built = d.Text("built"); Assert.True(File.Exists(Path.Combine(built, "index.html")));
        Assert.Contains("..ooooo", File.ReadAllText(Path.Combine(built, "config.js")));
        Assert.Contains("const T=32", File.ReadAllText(Path.Combine(built, "game.js")));
        Assert.Contains(l.Opened, o => o.EndsWith("index.html"));
        // breiter machen
        var cols = Level(w)[0].Length; Click(w, "game-level-wider"); Assert.Equal(cols + 10, Level(w)[0].Length);
    }

    [Fact(DisplayName = "Plattformer-Prüfung: kein Start, zwei Starts, kein Ziel, kein Boden unter dem Start; erlaubte Kacheln")]
    public void Platform_checks()
    {
        Assert.Empty(GameStudio.CheckPlatform(GameStudio.DefaultPlatform));
        Assert.Contains(GameStudio.CheckPlatform(["......Z", "#######"]), x => x.Contains("Kein Start"));
        Assert.Contains(GameStudio.CheckPlatform(["S.S...Z", "#######"]), x => x.Contains("2 Startpunkte"));
        Assert.Contains(GameStudio.CheckPlatform(["S......", "#######"]), x => x.Contains("Kein Ziel"));
        Assert.Contains(GameStudio.CheckPlatform(["S.....Z", ".######"]), x => x.Contains("kein Boden"));
        Assert.Throws<ArgumentException>(() => GameStudio.Validate(new GameStudio.GameSettings { Level = ["S..X..Z"] }));
        GameStudio.Validate(new GameStudio.GameSettings { Level = [.. GameStudio.DefaultPlatform] });
        var doc = GameDesign.Templates.First(t => t.Id == "retro-plattform").Bible();
        Assert.Empty(GameDesign.Check(doc).Where(x => x.Contains("Level") || x.Contains("Start") || x.Contains("Ziel")));
    }
}
