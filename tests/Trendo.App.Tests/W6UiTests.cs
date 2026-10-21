using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Trendo.Core;
using Xunit;

namespace Trendo.App.Tests;

public static class W6Ui
{
    public static string Run(MainWindow w, string id, params (string Key, string Val)[] vals)
    {
        foreach (var (k, v) in vals) w.Find<TextBox>($"{id}-{k}").Text = v;
        w.Find<Button>($"{id}-run").Press(); W2Ui.Settle();
        return w.Find<SelectableTextBlock>($"{id}-out").Text ?? "";
    }
}

public class SPEC01_Ui
{
    [AvaloniaFact(DisplayName = "SPEC-01 UI: Studio-Kachel öffnet Räume, Raum anlegen, Bedarf, Grundriss")]
    public void Rooms()
    {
        var (w, l, _) = Ui.Open();
        w.Show(MainWindow.StudioPages["SPEC-01"]);
        W6Ui.Run(w, "arch-add", ("name", "Küche"), ("l", "3,5"), ("w", "3"));
        Assert.Single(w.Rooms.Project.Rooms);
        Assert.Contains("Pakete", W6Ui.Run(w, "arch-need"));
        Assert.Contains("⚠", W6Ui.Run(w, "arch-add", ("l", "abc")));
        w.Find<Button>("arch-svg").Press();
        Assert.Contains(l.Opened, x => x.EndsWith("grundriss.svg"));
    }
}
