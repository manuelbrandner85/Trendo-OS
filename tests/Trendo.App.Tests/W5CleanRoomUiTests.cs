using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Trendo.App;
using Trendo.Core;
using Xunit;
namespace Trendo.App.Tests;

public class W5_X10_Ui
{
    [AvaloniaFact(DisplayName = "X-10 (UI): Projekt anlegen, Tagebuch-Eintrag, Prüfung ohne Treffer schreibt Bericht")]
    public void Flow()
    {
        var (w, _, root) = Ui.Open();
        w.Show("nachbau");
        w.Find<TextBox>("clean-name").Text = "Notizen";
        w.Find<Button>("clean-create").Press();
        var d = Path.Combine(w.CleanFolder, "Notizen");
        w.Find<TextBox>("clean-note").Text = "Bildschirme beschrieben";
        w.Find<Button>("clean-log").Press();
        Assert.Equal(2, CleanRoom.Journal(d).Count);
        var orig = Path.Combine(root, "orig"); Directory.CreateDirectory(orig); File.WriteAllText(Path.Combine(orig, "a.cs"), "class A { void M() { int x = 1; x++; return; } }");
        w.CleanCheck(orig);
        Assert.True(File.Exists(Path.Combine(d, "3-Pruefung", "PRUEFBERICHT.md")));
        w.Close();
    }
}
