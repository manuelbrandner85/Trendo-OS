using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Trendo.App;
using Xunit;
namespace Trendo.App.Tests;

public class W5_X01_Ui
{
    [AvaloniaFact(DisplayName = "X-01 (UI): Ohne Angaben gesperrt; Plan freigeben → Ja → alle Schritte ✓")]
    public void Run()
    {
        var (w, _, _) = Ui.Open();
        w.Show("produktion");
        Assert.False(w.Find<Button>("fusion-request").IsEnabled);
        w.Find<TextBox>("fusion-in-name").Text = "Radkurier"; w.Find<TextBox>("fusion-in-problem").Text = "Pakete kommen zu spät.";
        w.Show("produktion");
        w.Find<Button>("fusion-request").Press();
        w.Find<Button>("fusion-approve").Press();
        Assert.StartsWith("✓", w.Find<TextBlock>("fusion-result-2").Text);
        w.Close();
    }
}
