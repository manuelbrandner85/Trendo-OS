using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Trendo.App;
using Trendo.Core;
using Xunit;
namespace Trendo.App.Tests;

public class W4_X07_Ui
{
    [AvaloniaFact(DisplayName = "X-07 (UI): Suche und Laden mit Lizenzdatei (Netz simuliert)")]
    public async Task Fetch()
    {
        var (w, _, _) = Ui.Open();
        w.AssetGet = url => Task.FromResult(url.Contains("/files/")
            ? """{"gltf":{"1k":{"gltf":{"url":"https://dl.polyhaven.org/a/r_1k.gltf","md5":"900150983cd24fb0d6963f7d28e17f72","include":{"textures/t.jpg":{"url":"https://dl.polyhaven.org/t.jpg","md5":""}}}}}}"""
            : """{"rock_09":{"type":2,"name":"Rock 09","categories":["rocks"],"download_count":5}}""");
        w.AssetDownload = (_, f) => File.WriteAllTextAsync(f, "abc");
        await w.AssetSearch(2, "rock");
        w.Find<Button>("asset-get-0").Press();
        await Task.Delay(200); Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.True(File.Exists(Path.Combine(w.AssetFolder, "rock_09", "textures", "t.jpg")));
        Assert.Contains("CC0", File.ReadAllText(Path.Combine(w.AssetFolder, "rock_09", "LIZENZ.txt")));
        w.Close();
    }

    [AvaloniaFact(DisplayName = "X-07: Echter Download eines HDRI von Poly Haven mit Prüfsumme (übersprungen ohne Internet)")]
    public async Task Live()
    {
        var (w, _, _) = Ui.Open();
        try
        {
            var list = FreeAssets.ParseList(await w.AssetGet(FreeAssets.ListUrl("hdris")), "");
            Assert.NotEmpty(list);
            var dir = await w.AssetFetch(list.OrderBy(h => h.Id).First());
            Assert.NotNull(dir); Assert.Single(Directory.GetFiles(dir!, "*.hdr"));
        }
        catch (HttpRequestException) { } catch (TaskCanceledException) { }
        w.Close();
    }
}
