using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Trendo.App;
using Xunit;
namespace Trendo.App.Tests;

public class W5_X04_CatalogUi
{
    [AvaloniaFact(DisplayName = "X-04 Katalog (UI): mitgelieferten Katalog zeigen und geprüft installieren")]
    public void Install()
    {
        var (w, _, _) = Ui.Open();
        w.Show("erweiterungen");
        w.Find<Button>("ext-cat-local").Press();
        Assert.Equal(3, w.ExtCatalog.Count);
        w.Find<Button>("ext-cat-install-0").Press(); Dispatcher.UIThread.RunJobs();
        Assert.Empty(w.Exts.All()); // T-0241: erst Rückfrage mit Prüfsumme
        Assert.NotNull(w.TryFind<Button>("ext-ask-yes"));
        w.Find<Button>("ext-ask-yes").Press(); Dispatcher.UIThread.RunJobs();
        Assert.Single(w.Exts.All());
        w.Close();
    }
}
