using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Trendo.App;
using Xunit;
namespace Trendo.App.Tests;

public class W5_X04_CatalogUi
{
    /// <summary>T-0766 (Befund M2): Installieren läuft außerhalb des Oberflächen-Threads – kurz warten, bis es fertig ist.</summary>
    static void Until(Func<bool> ok, int ms = 20000)
    {
        var end = Environment.TickCount64 + ms;
        while (!ok() && Environment.TickCount64 < end) { Avalonia.Threading.Dispatcher.UIThread.RunJobs(); Thread.Sleep(20); }
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact(DisplayName = "X-04 Katalog (UI): mitgelieferten Katalog zeigen und geprüft installieren")]
    public void Install()
    {
        var (w, _, _) = Ui.Open();
        w.Show("erweiterungen");
        w.Find<Button>("ext-cat-local").Press();
        Assert.Equal(4, w.ExtCatalog.Count); // T-0766: plus Funktions-Erweiterung
        w.Find<Button>("ext-cat-install-0").Press(); Dispatcher.UIThread.RunJobs();
        Assert.Empty(w.Exts.All()); // T-0241: erst Rückfrage mit Prüfsumme
        Assert.NotNull(w.TryFind<Button>("ext-ask-yes"));
        w.Find<Button>("ext-ask-yes").Press(); Until(() => w.Exts.All().Count == 1);
        Assert.Single(w.Exts.All());
        w.Close();
    }
}
