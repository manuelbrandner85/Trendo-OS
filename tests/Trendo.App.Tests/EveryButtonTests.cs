using Avalonia.Headless.XUnit;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>T-0209: Jeder Knopf auf den geänderten Seiten muss eine sichtbare Wirkung haben.</summary>
public class EveryButton_Alltag
{
    static readonly string[] Pages =
    [
        "alltag-dateien", "alltag-bilder", "alltag-zeit", "alltag-helfer",
        "brief", "doku-pdf", "kreativ-medien", "netz-laden", "produktion", "ki-quellen",
    ];

    [AvaloniaFact(DisplayName = "T-0209: Jeder sichtbare Knopf auf Helfer, Brief, PDF, Medien, Laden, Produktion und KI-Quellen wirkt", Timeout = 300000)]
    public void Jeder_Knopf_wirkt()
    {
        var (w, l, root) = Ui.Open();
        var datei = Path.Combine(root, "auswahl.txt");
        File.WriteAllText(datei, "x");
        w.PickOverride = _ => Task.FromResult<string[]?>([datei]);
        var downloads = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        if (!Directory.Exists(downloads)) Directory.CreateDirectory(downloads);
        var fehl = ButtonProbe.Require(w, l, root, Pages);
        w.Close();
        Assert.True(fehl.Count == 0, "Knopf ohne Wirkung oder Absturz:\n" + string.Join("\n", fehl));
    }
}
