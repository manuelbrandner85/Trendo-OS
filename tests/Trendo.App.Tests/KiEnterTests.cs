using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using Trendo.App;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;
namespace Trendo.App.Tests;

public class KiEnter_Ui
{
    [AvaloniaFact(DisplayName = "KI-Eingabe: Enter schickt die Frage genau einmal, auch nach vielen Seitenwechseln")]
    public void EnterOnce()
    {
        var (w, _, _) = Ui.Open();
        var asked = new List<string>(); w.KiChatOverride = q => { asked.Add(q); return Task.FromResult("Antwort"); };
        foreach (var k in new[] { "start", "ki", "settings", "start", "ki", "start", "ki" }) w.Show(k);
        Settle();
        var box = V<TextBox>(w, "ki-input"); box.IsEnabled = true; box.Text = "Hallo";
        RealClick(w, box); w.Key(PhysicalKey.Enter); Settle();
        Assert.Equal(["Hallo"], asked); Assert.Equal(1, w.KiAskCount);
        w.Close();
    }
}
