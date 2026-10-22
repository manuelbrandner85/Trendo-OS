using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Trendo.App;
using Trendo.Core;
using Xunit;

namespace Trendo.App.Tests;

public class WebsiteStudioUiTests
{
    [AvaloniaFact(DisplayName = "CORE-07 (UI): Seite aus Beschreibung, Note, Stil, Paket erst nach Bestätigung")]
    public async Task Build_score_style_and_pack()
    {
        var (w, _, _) = Ui.Open();
        w.Show("dev");
        w.Find<TextBox>("web-prompt").Text = "Eine kleine Landingpage für eine Bücherei im Ort.";
        w.Find<TextBox>("web-name").Text = "Seiten Test";
        w.Find<ComboBox>("web-kind").SelectedIndex = 1;
        w.Find<ComboBox>("web-technique").SelectedIndex = 6;
        w.Find<Button>("web-build").Press();
        var dir = Path.Combine(w.DevFolder, "Seiten-Test");
        await Until(() => File.Exists(Path.Combine(dir, "index.html")) && (w.TryFind<TextBlock>("web-score")?.Text?.Contains("von 100") ?? false));
        Assert.True(File.Exists(Path.Combine(dir, "stil.css")));
        Assert.Contains("von 100", w.Find<TextBlock>("web-score").Text!);
        w.Find<Button>("web-style-luxus").Press();
        await Until(() => File.ReadAllText(Path.Combine(dir, "stil.css")).Contains("#14110e"));
        w.Find<Button>("web-pack").Press();
        Assert.False(Directory.GetFiles(w.DevFolder, "*.zip").Any());
        Assert.NotNull(w.TryFind<Button>("web-pack-yes"));
        w.Find<Button>("web-pack-yes").Press();
        await Until(() => Directory.GetFiles(w.DevFolder, "*.zip").Any());
        w.Close();
    }

    static async Task Until(Func<bool> done)
    {
        for (var i = 0; i < 600 && !done(); i++)
        {
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(50);
        }
        Dispatcher.UIThread.RunJobs();
        Assert.True(done());
    }
}
