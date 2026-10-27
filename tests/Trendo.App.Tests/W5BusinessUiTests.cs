using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Trendo.App;
using Xunit;
namespace Trendo.App.Tests;

public class W5_Business_Ui
{
    [AvaloniaFact(DisplayName = "BUS-001/005/010/011 (UI): Idee eingeben, bewerten, Bauplan speichern; Studio-Kachel öffnet Business")]
    public async Task Evaluate()
    {
        var (w, _, _) = Ui.Open();
        await w.OpenStudio("STUDIO-ONLINE-BUSINESS-01", "Business");
        Assert.Equal("verkaufen", w.CurrentView); // Teil 4 U1: das Studio startet mit der Übersicht
        w.Show("business");
        w.Find<TextBox>("biz-name").Text = "Kaffee-Abo";
        w.Find<NumericUpDown>("biz-price").Value = 15; w.Find<NumericUpDown>("biz-varcost").Value = 20;
        w.Find<Button>("biz-evaluate").Press();
        Assert.Contains("Nicht starten", w.Find<TextBlock>("biz-verdict").Text);
        Assert.Equal("Kaffee-Abo", w.Ideas.All()[0].Name);
        w.Find<Button>("biz-blueprint").Press();
        Assert.True(File.Exists(Path.Combine(w.BusinessFolder, "Kaffee-Abo-Bauplan.md")));
        w.Close();
    }
}
