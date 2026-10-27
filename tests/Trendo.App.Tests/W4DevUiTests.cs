using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Trendo.App;
using Trendo.Core;
using Xunit;
namespace Trendo.App.Tests;

public class W4_Core07_Ui
{
    [AvaloniaFact(DisplayName = "CORE-07 (UI): Software-Studio legt Projekt an und zeigt es mit Git-Stand")]
    public void Create_project()
    {
        var (w, _, _) = Ui.Open();
        w.Show("dev");
        w.Find<TextBox>("dev-name").Text = "Hallo Web";
        w.Find<Button>("dev-create").Press();
        Assert.True(Directory.Exists(Path.Combine(w.DevFolder, "Hallo-Web")));
        Assert.NotNull(w.TryFind<Button>("dev-open-0"));
        Assert.True(w.TryFind<Button>("kreativ-tab-dev") is not null || w.TryFind<Panel>("tabs-none") is not null); // G3: allein im Studio ohne Reiterleiste
        w.Close();
    }

    [AvaloniaFact(DisplayName = "CORE-07 (UI): Suche findet das Software-Studio")]
    public void Search_finds()
    {
        var (w, _, _) = Ui.Open();
        Assert.Contains(w.BuildSearch().Find("software"), h => h.Target == "page:dev");
        w.Close();
    }
}
