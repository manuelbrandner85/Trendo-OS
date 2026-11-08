using System.Text.Json.Nodes;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Trendo.Core.Programs;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>Stufe „Einfach“ (Wunsch 08.11.2026): weniger auf einen Blick, keine Funktion geht verloren.</summary>
public class SimpleMode_Ui
{
    static IEnumerable<Button> Buttons(Control root) => root.GetLogicalDescendants().OfType<Button>();
    static HashSet<string> Names(Control root) => Buttons(root).Where(b => b.IsEffectivelyVisible).Select(b => b.Name ?? "").Where(n => n.Length > 0).ToHashSet();
    static HashSet<string> MoreNames(MainWindow w)
    {
        var more = w.TryFind<Button>("prog-more"); if (more?.Flyout is not Flyout f) return [];
        var root = f.Content is ScrollViewer sv ? (Control)sv.Content! : (Control)f.Content!;
        return root.GetLogicalDescendants().OfType<Button>().Select(b => b.Name ?? "").ToHashSet();
    }

    [AvaloniaFact(DisplayName = "Einfach: In jedem Programm bleibt jeder Befehl der Stufe erreichbar, direkt oder unter „Mehr“; direkt sichtbar sind höchstens vier Programmbefehle")]
    public void Programs_keep_all_commands()
    {
        var (w, _, _) = Ui.Open();
        try
        {
            foreach (var (key, spec) in MainWindow.Programs)
            {
                if (spec.Templates.Count == 0) continue;
                w.ProgramSetLevel(UserLevel.Einfach); w.Show(key); W2Ui.Settle();
                var tpl = Buttons(w.ActivePageRoot!).FirstOrDefault(b => (b.Name ?? "").StartsWith("prog-tpl-"));
                if (tpl is null) continue;
                tpl.Press(); W2Ui.Settle();
                var direct = Names(w.ActivePageRoot!); var more = MoreNames(w);
                var all = direct.Union(more).ToHashSet();
                foreach (var (_, cmds) in spec.Ribbon)
                    foreach (var c in cmds.Where(c => UserLevel.Shows(UserLevel.Einfach, spec.LevelOf(c))))
                        if (UserLevel.Shows(UserLevel.Einfach, spec.GroupLevels.GetValueOrDefault(spec.Ribbon.First(g => g.Item2.Contains(c)).Item1)))
                            Assert.True(all.Contains("prog-cmd-" + c.Name), $"{key}: {c.Name} nicht erreichbar");
                Assert.Contains("prog-save", direct); Assert.Contains("prog-undo", direct);
                foreach (var name in new[] { "prog-new", "prog-saveas", "prog-close" }) if (w.TryFind<Button>("prog-more") is not null) Assert.True(all.Contains(name) || all.Contains("prog-saveas-open"), $"{key}: {name}");
                var ribbonCmds = spec.Ribbon.SelectMany(g => g.Item2).Select(c => "prog-cmd-" + c.Name).Count(direct.Contains);
                Assert.InRange(ribbonCmds, 0, MainWindow.SimpleCommands);
            }
        }
        finally { w.ProgramSetLevel(UserLevel.Erweitert); w.Close(); }
    }

    [AvaloniaFact(DisplayName = "Spiele bauen, Einfach: Auftrag und nächster Schritt; „Alle Bereiche“ zeigt alles; Erweitert unverändert")]
    public void Game()
    {
        var (w, _, _) = Ui.Open();
        try
        {
            var key = MainWindow.StudioPages["CORE-06"];
            w.ProgramSetLevel(UserLevel.Einfach); w.Show(key); W2Ui.Settle();
            Assert.Null(w.TryFind<Button>("game-prompt-team-run") is { IsVisible: true } ? new object() : null);
            Buttons(w.ActivePageRoot!).First(b => (b.Name ?? "").StartsWith("prog-tpl-")).Press(); W2Ui.Settle();
            Assert.NotNull(w.TryFind<Control>("game-next"));
            Assert.Null(w.TryFind<WrapPanel>("game-sections"));
            w.Find<Button>("game-show-all").Press(); W2Ui.Settle();
            Assert.NotNull(w.TryFind<WrapPanel>("game-sections"));
            w.Find<Button>("game-show-less").Press(); W2Ui.Settle();
            Assert.Null(w.TryFind<WrapPanel>("game-sections"));
            w.ProgramSetLevel(UserLevel.Erweitert); w.Show(key); W2Ui.Settle();
            Assert.NotNull(w.TryFind<WrapPanel>("game-sections")); Assert.Null(w.TryFind<Control>("game-next")); Assert.Null(w.TryFind<Button>("prog-more"));
        }
        finally { w.ProgramSetLevel(UserLevel.Erweitert); w.Close(); }
    }

    [AvaloniaFact(DisplayName = "Spiele bauen: Nächster Schritt nennt den schwächsten Bereich in Alltagssprache; Alltagswörter nur in Einfach")]
    public void Next_step_and_words()
    {
        var d = GameDesign.Templates.First(t => t.Scale == "aa");
        Assert.Equal("Spielkonzept", PlainWords.For(UserLevel.Einfach, "Spielbibel"));
        Assert.Equal("Spielbibel", PlainWords.For(UserLevel.Erweitert, "Spielbibel"));
        Assert.Equal("Eigener Text", PlainWords.For(UserLevel.Einfach, "Eigener Text"));
        Assert.Contains("fehlen noch", MainWindow.GameNextStep(new JsonObject())!.Value.Text);
        Assert.NotNull(d);
    }

    [AvaloniaFact(DisplayName = "Übrige Seiten, Einfach: Rest hinter „Mehr anzeigen“ und nach einem Klick wieder da; Erweitert zeigt alles sofort")]
    public void Pages()
    {
        var (w, _, _) = Ui.Open();
        try
        {
            w.Settings.SetupDone = true; // im Einrichtungs-Assistenten wird nichts eingeklappt
            w.ProgramSetLevel(UserLevel.Erweitert); w.Show("ki-quellen"); W2Ui.Settle();
            var full = Names(w.ActivePageRoot!); Assert.DoesNotContain("page-more", full);
            w.ProgramSetLevel(UserLevel.Einfach); w.Show("ki-quellen"); W2Ui.Settle();
            var few = Names(w.ActivePageRoot!); Assert.Contains("page-more", few); Assert.True(few.Count < full.Count);
            w.Find<Button>("page-more").Press(); W2Ui.Settle();
            var opened = Names(w.ActivePageRoot!);
            Assert.Empty(full.Except(opened).Where(n => !n.StartsWith("page-team") && !n.StartsWith("kipy")));
        }
        finally { w.ProgramSetLevel(UserLevel.Erweitert); w.Close(); }
    }

    [AvaloniaFact(DisplayName = "3D-Modelle, Einfach: Grundfunktionen sichtbar, Export und Fachwerkzeuge nach einem Klick; Erweitert zeigt alles")]
    public void Studio3D()
    {
        var (w, _, _) = Ui.Open();
        try
        {
            w.ProgramSetLevel(UserLevel.Einfach); w.Show("3d"); W2Ui.Settle();
            var few = Names(w.ActivePageRoot!);
            Assert.Contains("3d-add", few); Assert.Contains("3d-save", few); Assert.DoesNotContain("3d-export-glb", few); Assert.DoesNotContain("3d-bevel", few);
            w.Find<Button>("3d-show-more").Press(); W2Ui.Settle();
            var more = Names(w.ActivePageRoot!); Assert.Contains("3d-export-glb", more); Assert.Contains("3d-bevel", more);
            w.Find<Button>("3d-show-more").Press(); W2Ui.Settle();
            Assert.Contains("3d-to-print", few); Assert.Contains("3d-printer", few); Assert.Contains("3d-find-models", few);
            w.Find<Button>("3d-to-print").Press(); W2Ui.Settle(); Assert.Equal("3d", w.CurrentView); // noch kein Teil
            w.Find<Button>("3d-add").Press(); W2Ui.Settle();
            w.Find<Button>("3d-to-print").Press(); W2Ui.Settle();
            Assert.Equal("3d-druck", w.CurrentView); Assert.EndsWith(".stl", w.PrintFile); Assert.True(File.Exists(w.PrintFile));
            w.Show("3d"); W2Ui.Settle(); w.Find<Button>("3d-printer").Press(); W2Ui.Settle(); Assert.Equal("3d-druck", w.CurrentView);
            w.Show("3d"); W2Ui.Settle(); w.Find<Button>("3d-find-models").Press(); W2Ui.Settle(); Assert.Equal("bib3d", w.CurrentView);
            w.ProgramSetLevel(UserLevel.Erweitert); w.Show("3d"); W2Ui.Settle();
            var erw = Names(w.ActivePageRoot!); Assert.Contains("3d-to-print", erw); Assert.Contains("3d-export-glb", erw); Assert.DoesNotContain("3d-show-more", erw);
        }
        finally { w.ProgramSetLevel(UserLevel.Erweitert); w.Close(); }
    }
}
