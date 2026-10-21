using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Trendo.App;
using Trendo.Core;
using Xunit;
namespace Trendo.App.Tests;

public class W4_Core06_Ui
{
    [AvaloniaFact(DisplayName = "CORE-06 (UI): Spiel anlegen öffnet es im Browser; ohne Unreal ehrlicher Hinweis, mit Unreal Projekt anlegen")]
    public void Game()
    {
        var (w, l, _) = Ui.Open();
        w.EngineFinder = () => [];
        w.Show("spiele");
        Assert.NotNull(w.TryFind<Button>("game-get-unreal"));
        w.Find<Button>("game-create").Press();
        Assert.True(File.Exists(Path.Combine(w.GameFolder, "Mein-Spiel", "index.html")));
        w.EngineFinder = () => [new("5.5", "/nicht/da")];
        w.Show("spiele");
        w.Find<Button>("game-ue-create").Press();
        Assert.True(File.Exists(Path.Combine(w.GameFolder, "MeinUnrealSpiel", "MeinUnrealSpiel.uproject")));
        w.Close();
    }
}
