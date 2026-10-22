using System.Text.RegularExpressions;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Trendo.App.Tests;
using Xunit;

public class BauplanUi
{
    [AvaloniaFact(DisplayName = "T-0005 UI: Not-Stopp sitzt in der Leiste und schaltet um")]
    public void NotStopp_button()
    {
        var (w, _, _) = Ui.Open();
        var b = w.Find<Button>("btn-notstopp");
        Assert.Equal("Not-Stopp", b.Content);
        b.Press();
        Assert.Equal("Automatik wieder erlauben", b.Content);
        Assert.True(w.Core.NotStopp.IsStopped);
        b.Press();
        Assert.Equal("Not-Stopp", b.Content);
        Assert.False(w.Core.NotStopp.IsStopped);
        w.Close();
    }

    [AvaloniaFact(DisplayName = "T-0012 UI: Handy-Seite zeigt Code und QR, der Code steht nicht in der Seite")]
    public async Task Handy_page_shows_code()
    {
        var (w, _, _) = Ui.Open();
        w.Show("approvals");
        w.Find<Button>("btn-handy-start").Press();
        var code = w.Find<TextBlock>("handy-code").Text ?? "";
        Assert.Matches(new Regex("^[0-9]{6}$"), code);
        Assert.NotNull(w.TryFind<Control>("handy-qr"));
        Assert.NotNull(w.HandyServer);
        using var http = new HttpClient();
        var page = await http.GetStringAsync(w.HandyServer!.PageUrl);
        Assert.DoesNotContain(code, page);
        w.Close();
    }

    [AvaloniaFact(DisplayName = "T-0008 UI: Pflege zeigt den Deinstallationsplan und löscht nichts")]
    public void Uninstall_plan_visible()
    {
        var (w, _, root) = Ui.Open();
        var marker = Path.Combine(root, "behalten.txt");
        File.WriteAllText(marker, "bleibt");
        w.Show("prog-pflege");
        var text = w.Find<TextBlock>("uninstall-plan").Text ?? "";
        Assert.Contains("löscht nichts", text);
        Assert.True(File.Exists(marker));
        w.Close();
    }
}
