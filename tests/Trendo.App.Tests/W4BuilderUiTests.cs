using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Trendo.App;
using Xunit;
namespace Trendo.App.Tests;

public class W4_X03_Ui
{
    [AvaloniaFact(DisplayName = "X-03 (UI): Baustein hinzufügen, verschieben, entfernen, veröffentlichen")]
    public void Build()
    {
        var (w, l, _) = Ui.Open();
        w.Show("baukasten");
        w.Find<Button>("builder-add-button").Press();
        Assert.Equal(3, w.BuilderPage.Blocks.Count);
        w.Find<Button>("builder-up-2").Press();
        Assert.Equal("button", w.BuilderPage.Blocks[1].Kind);
        w.Find<Button>("builder-del-0").Press();
        Assert.Equal(2, w.BuilderPage.Blocks.Count);
        Assert.True(File.Exists(w.BuilderFile));
        w.Find<Button>("builder-preview").Press();
        Assert.Contains("Mehr erfahren", File.ReadAllText(Path.Combine(w.BuilderFolder, "fertig", "index.html")));
        w.Close();
    }
}
