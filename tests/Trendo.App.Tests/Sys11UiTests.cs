using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Trendo.App;
using Trendo.Core.Sys;
using Xunit;
namespace Trendo.App.Tests;

public class Sys11_Programme_Ui
{
    [AvaloniaFact(DisplayName = "SYS-11 (UI): Tabs; Verknüpfung anlegen; Zurücksetzen erst nach Bestätigung; Voraussetzungen prüfen")]
    public void Pages()
    {
        var (w, l, root) = Ui.Open();
        foreach (var k in new[] { "prog-pflege", "prog-start", "prog-pruefen" }) { w.Show(k); Assert.Equal(k, w.CurrentView); }
        w.Show("prog-start"); var exe = Path.Combine(root, "p.exe"); File.WriteAllText(exe, "");
        w.Find<TextBox>("sc-name").Text = "Test"; w.Find<TextBox>("sc-exe").Text = exe; w.Find<TextBox>("sc-args").Text = "-a \"b c\""; w.Find<Button>("sc-add").Press();
        Assert.NotNull(w.TryFind<Button>("sc-run-0"));
        w.Show("prog-pflege"); w.Find<Button>("reset-ask").Press(); Assert.NotNull(w.TryFind<Button>("reset-yes"));
        w.Find<Button>("reset-no").Press(); Assert.Null(w.TryFind<Button>("reset-yes"));
        Assert.Empty(Directory.Exists(Path.Combine(root, "sicherungen")) ? Directory.GetFiles(Path.Combine(root, "sicherungen")) : []);
        w.Show("prog-pruefen"); w.Find<Button>("req-check").Press();
        Assert.Contains("Hinweis:", w.Find<TextBlock>("prog-out").Text);
        w.Close();
    }
}
