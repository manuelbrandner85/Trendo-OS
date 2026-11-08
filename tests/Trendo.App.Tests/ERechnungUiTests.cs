using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Trendo.Core;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>Alle Studios, Runde 6 – Geld &amp; Rechnungen: „Rechnung + E-Rechnung (XRechnung) erstellen“.</summary>
public class ERechnungUi
{
    [AvaloniaFact(DisplayName = "E-Rechnung-Oberfläche: fehlende Angaben verständlich, nichts gebucht; vollständig → XRechnung neben der Rechnung, Einnahme gebucht, eigene Angaben gemerkt")]
    public void Create()
    {
        var (w, _, _) = Ui.Open();
        try
        {
            w.Show("finanzen"); Assert.NotNull(w.Find<Expander>("er-box"));
            var before = w.Money.All().Count;
            Assert.Null(w.FinanceERechnung("Erika Muster\nMusterweg 1\n12345 Musterstadt", "Kunde", "Beratung", "2", "80", false, "", "", "", "", "", ""));
            Assert.Contains("IBAN", w.ERechnungMessage); Assert.Equal(before, w.Money.All().Count);
            var xml = w.FinanceERechnung("Erika Muster\nMusterweg 1\n12345 Musterstadt", "Max Beispiel GmbH\nHauptstraße 5\n54321 Beispielort", "Beratung", "2", "80", false,
                "DE123456789", "", "rechnung@example.org", "DE02 1203 0000 0000 2020 51", "einkauf@example.com", "Bestellung 4711");
            Assert.NotNull(xml); Assert.StartsWith("xrechnung-", Path.GetFileName(xml)); Assert.True(File.Exists(xml));
            var r = ERechnung.Read(File.ReadAllBytes(xml!)); Assert.Equal(190.40m, r.Gross); Assert.Equal("Bestellung 4711", r.Reference);
            Assert.Equal(before + 1, w.Money.All().Count); Assert.StartsWith("✓ Rechnung", w.ERechnungMessage);
            var saved = File.ReadAllText(Path.Combine(w.Core.DataRoot, "finanzen", "e-rechnung.json")); Assert.Contains("DE123456789", saved); Assert.Contains("rechnung@example.org", saved);
            w.Show("finanzen"); Assert.Equal("DE123456789", w.Find<TextBox>("er-vat").Text);
        }
        finally { w.Close(); }
    }
}
