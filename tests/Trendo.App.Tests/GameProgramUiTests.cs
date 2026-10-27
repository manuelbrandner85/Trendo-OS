using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Trendo.App;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>Spiele erstellen als Programm (Retro bis AAA+).</summary>
public class GameProgramUiTests
{
    static void Pump() { for (var i = 0; i < 5; i++) Dispatcher.UIThread.RunJobs(); }

    [AvaloniaFact(DisplayName = "Spiele: Retro-Vorlage, Level im Raster bearbeiten, Figur hinzufügen, prüfen, bauen und spielen")]
    public void Retro_flow()
    {
        var (w, l, _) = Ui.Open();
        Assert.Equal("spiele-programm", MainWindow.StudioPages["CORE-06"]);
        w.Show("spiele-programm"); Pump();
        w.Find<Button>("prog-tpl-retro-jump").Press(); Pump();
        Assert.NotNull(w.TryFind<WrapPanel>("game-rings"));
        w.Find<Button>("game-sec-welt").Press(); Pump();
        var before = w.ProgramActive("spiele-programm")!.List("level")[0]!.GetValue<string>();
        w.Find<Button>("game-cell-0-0").Press(); Pump();
        Assert.NotEqual(before, w.ProgramActive("spiele-programm")!.List("level")[0]!.GetValue<string>());
        w.Find<Button>("prog-undo").Press(); Pump();
        Assert.Equal(before, w.ProgramActive("spiele-programm")!.List("level")[0]!.GetValue<string>());
        w.Find<Button>("prog-cmd-add-char").Press(); Pump();
        Assert.Equal("Figuren", w.GameSection);
        var name = w.Find<TextBox>("game-characters-1-name"); name.Text = "Dr. Pixel";
        name.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter }); Pump();
        Assert.Equal("Dr. Pixel", w.ProgramActive("spiele-programm")!.List("characters")[1]!["name"]!.GetValue<string>());
        w.Find<Button>("prog-cmd-check").Press(); Pump();
        Assert.NotEmpty(w.ProgramMessage("spiele-programm"));
        w.Find<Button>("game-sec-bauen").Press(); Pump();
        w.Find<Button>("game-build").Press(); Pump();
        Assert.Contains("Spielbar gebaut", w.ProgramMessage("spiele-programm"));
        w.Find<Button>("game-play").Press(); Pump();
        Assert.Contains(l.Opened, o => o.EndsWith("index.html"));
        w.Close();
    }

    [AvaloniaFact(DisplayName = "Spiele: Umfang AAA+ wählen setzt AAA-Meilensteine; Bauen legt ein Unreal-Projekt mit Spielbibel an")]
    public void Aaa_flow()
    {
        var (w, _, _) = Ui.Open();
        w.Show("spiele-programm"); Pump();
        w.Find<Button>("prog-tpl-openworld").Press(); Pump();
        w.Find<Button>("game-sec-produktion").Press(); Pump();
        Assert.NotNull(w.TryFind<CheckBox>("game-ms-8"));
        w.Find<Button>("game-sec-bauen").Press(); Pump();
        w.Find<Button>("game-build").Press(); Pump();
        var built = w.ProgramActive("spiele-programm")!.Text("built");
        Assert.True(File.Exists(Path.Combine(built, "Design", "Spielbibel.md")));
        Assert.Contains("Unreal-Projekt angelegt", w.ProgramMessage("spiele-programm"));
        w.Close();
    }
}
