using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Trendo.App;
using Trendo.Core;
using Xunit;
namespace Trendo.App.Tests;

public class W4_X06_Ui
{
    [AvaloniaFact(DisplayName = "X-06 (UI): Suche zeigt Treffer mit Lizenz, ohne Lizenz kein Herunterladen")]
    public async Task Search_shows()
    {
        var (w, _, _) = Ui.Open();
        w.OssSearchOverride = _ => Task.FromResult<IReadOnlyList<OssRepo>>([new("a/b", "x", "https://github.com/a/b", "https://github.com/a/b.git", 5, "MIT", "C#", DateTimeOffset.Now), new("c/d", "y", "u", "v", 1, "", "", DateTimeOffset.Now)]);
        w.Show("oss");
        await w.OssSearch("editor");
        Assert.True(w.Find<Button>("oss-get-0").IsEnabled);
        Assert.False(w.Find<Button>("oss-get-1").IsEnabled);
        w.Close();
    }

    [Fact(DisplayName = "X-06: Live-Suche bei GitHub (übersprungen ohne Internet)")]
    public async Task Live()
    {
        try { Assert.NotEmpty(await MainWindow.OssSearchHttp(new HttpClient { Timeout = TimeSpan.FromSeconds(15) }, "avalonia", ct: TestContext.Current.CancellationToken)); }
        catch (HttpRequestException e) { Assert.Skip("Übersprungen – keine Verbindung zu GitHub: " + e.Message); }
        catch (TaskCanceledException) { Assert.Skip("Übersprungen – GitHub antwortet nicht rechtzeitig."); }
        // T-0777: nur ohne Netz überspringen – ist das Netz da (z. B. GitHub bremst, Antwort kaputt), schlägt der Test fehl
        catch (InvalidOperationException) when (!NetworkReachable()) { Assert.Skip("Übersprungen – kein Netz (api.github.com nicht auflösbar)."); }
    }

    static bool NetworkReachable()
    {
        if (!System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable()) return false;
        try { return System.Net.Dns.GetHostAddresses("api.github.com").Length > 0; }
        catch (System.Net.Sockets.SocketException) { return false; }
    }
}
