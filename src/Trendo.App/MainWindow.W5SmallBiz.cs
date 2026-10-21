using Avalonia.Controls;
using Trendo.Core;

namespace Trendo.App;

/// <summary>Kleinunternehmer-Prüfung nach § 19 UStG für die Seiten „Geld“ und „Geschäftsidee“. Grenzen und Quellen: LegalRates.</summary>
public partial class MainWindow
{
    private Control SmallBizCheck(string id)
    {
        var all = Money.All(); var y = DateTime.Today.Year;
        string D(decimal v) => v.ToString("0.##", System.Globalization.CultureInfo.GetCultureInfo("de-DE"));
        return FachTool(id, "Bin ich Kleinunternehmer?", $"Kleinunternehmer stellen Rechnungen ohne Mehrwertsteuer. Das geht, wenn der Umsatz im Vorjahr höchstens 25.000 € war und im laufenden Jahr höchstens 100.000 € wird (§ 19 UStG). Im ersten Geschäftsjahr gilt 25.000 €. Gemeint ist der Umsatz ohne Mehrwertsteuer. Vorausgefüllt aus deinen Buchungen. Stand {LegalRates.Checked}, keine Steuerberatung.",
            [("prev", $"Umsatz {y - 1} (€)", D(Finance.Year(all, y - 1).IncomeNet)), ("cur", $"Umsatz {y} bisher oder erwartet (€)", D(Finance.Year(all, y).IncomeNet)), ("new", "Erstes Geschäftsjahr? (j/n)", "n")],
            v =>
            {
                var (ok, why) = LegalRates.SmallBusiness((decimal)Fach.Num(v("prev")), (decimal)Fach.Num(v("cur")), v("new").Trim().StartsWith("j", StringComparison.OrdinalIgnoreCase));
                return ok ? $"✓ Ja, du kannst Kleinunternehmer sein. {why}\nAuf Rechnungen steht dann: „Gemäß § 19 UStG wird keine Umsatzsteuer berechnet.“"
                          : $"✗ Nein, Kleinunternehmer geht nicht. {why}\nDu musst Mehrwertsteuer auf Rechnungen ausweisen und ans Finanzamt abführen. Sprich am besten mit dem Finanzamt oder einer Steuerberatung.";
            }, "Prüfen");
    }
}
