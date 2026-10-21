using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Trendo.App;
using Xunit;
namespace Trendo.App.Tests;

public class W45_Tabs_Ui
{
    [AvaloniaFact(DisplayName = "W4/W5 (UI): Jede Seite in Kreativ und Business öffnet sich selbst (keine doppelten Seitennamen)")]
    public void All_tabs_own_their_page()
    {
        var (w, _, _) = Ui.Open();
        foreach (var (k, _, _) in MainWindow.KreativTabs) { w.Show(k); Assert.True(w.TryFind<Button>("kreativ-tab-" + k) is not null, "Seite überschrieben: " + k); }
        foreach (var (k, _, _) in MainWindow.BusinessTabs) { w.Show(k); Assert.True(w.TryFind<Button>("business-tab-" + k) is not null, "Seite überschrieben: " + k); }
        w.Close();
    }
}
