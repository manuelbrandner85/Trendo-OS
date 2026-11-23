using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Trendo.App;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// <summary>T-0753 Drittprüfung N-e: Import nennt in der Oberfläche die Anzahl „nicht übernommen“ und alle Hinweise.</summary>
public class SellEditFix3_Ui
{
    [AvaloniaFact(DisplayName = "N-e (UI): Import mit 8 gesperrten/ungültigen Zeilen – Meldung nennt „8 nicht übernommen“ und alle 8 Hinweise")]
    public void Import_shows_all_warnings()
    {
        var (w, _, root) = Ui.Open(); w.Width = 1600; w.Height = 1100;
        try
        {
            var f = Path.Combine(root, "a.csv");
            File.WriteAllText(f, "Title,Price\nGut,3.00\n" + string.Concat(Enumerable.Range(1, 8).Select(i => $"Minus{i},-{i}.00\n")));
            w.PickOverride = r => Task.FromResult<string[]?>(r.Field == "sell-csv-prod" ? [f] : null);
            w.Show("verkaufen"); Settle();
            var tab = V<Control>(w, "sell-tab-online"); RealClick(w, tab); Settle();
            var b = V<Control>(w, "sell-csv-prod-in"); b.BringIntoView(); Settle(); RealClick(w, V<Control>(w, "sell-csv-prod-in")); Settle();
            var text = string.Join("\n", w.GetLogicalTexts());
            Assert.Contains("1 neu, 0 aktualisiert, 8 nicht übernommen", text);
            Assert.Contains("Hinweise (8)", text);
            for (var i = 1; i <= 8; i++) Assert.Contains($"„Minus{i}“ nicht übernommen", text);
        }
        finally { w.Close(); }
    }
}
