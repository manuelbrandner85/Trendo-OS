using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Trendo.Core;
using Xunit;

namespace Trendo.App.Tests;

public class CORE11_Ui
{
    [AvaloniaFact(DisplayName = "CORE-11 UI: Schutz-Seite über System erreichbar, Sichern, Ordner prüfen")]
    public void Security_page()
    {
        var (w, _, root) = Ui.Open();
        w.Show("system");
        Assert.Equal("CORE-11", MainWindow.StudioPages.First(kv => kv.Value == "schutz").Key);
        w.Show("schutz");
        var s1 = w.Find<TextBlock>("sec-score").Text;
        w.Find<Button>("sec-fix-backup").Press();
        Assert.NotEqual(s1, w.Find<TextBlock>("sec-score").Text);
        var d = Path.Combine(root, "pr"); Directory.CreateDirectory(d); File.WriteAllText(Path.Combine(d, "a.txt"), "password=geheimnis");
        w.Find<TextBox>("sec-folder").Text = d;
        w.Find<Button>("sec-scan").Press();
        for (var i = 0; i < 50 && w.LastPrivacy.Count == 0; i++) { Thread.Sleep(20); W2Ui.Settle(); }
        Assert.Single(w.LastPrivacy);
        Assert.Contains("1 Fundstelle", w.Find<TextBlock>("sec-msg").Text);
    }
}
