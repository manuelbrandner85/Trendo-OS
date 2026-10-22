using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Xunit;

namespace Trendo.App.Tests;

/// T-0208: Business – jeder Knopf meldet sich, leere Eingaben führen zu klarer Meldung, kein Absturz.
public class BusinessFeedbackTests
{
    [AvaloniaTheory(DisplayName = "T-0208: Leere Eingabe → eigene Meldung statt Stille")]
    [InlineData("biz-betrieb", "op-c-add", "Namen des Kontakts")]
    [InlineData("biz-betrieb", "op-k-add", "Frage oder das Problem")]
    [InlineData("biz-betrieb", "op-m-add", "Tarif")]
    [InlineData("biz-betrieb", "op-p-add", "Produktnamen")]
    [InlineData("biz-betrieb", "op-s-import", "CSV-Datei")]
    [InlineData("biz-betrieb", "op-t-add", "Namen des Tarifs")]
    [InlineData("biz-marke", "br-f-calc", "neu berechnet")]
    [InlineData("biz-marke", "br-p-add", "in diesem Schritt")]
    [InlineData("biz-marke", "br-seo-check", "Suchwort")]
    [InlineData("biz-markt", "mk-c-add", "Mitbewerbers")]
    [InlineData("biz-markt", "mk-e-add", "Vermutung")]
    [InlineData("biz-markt", "mk-p-add", "Problem beschreiben")]
    public void Empty_input_gives_message(string page, string button, string expected)
    {
        var (w, _, _) = Ui.Open();
        w.Show(page);
        w.Find<Button>(button).Press();
        Assert.Contains(w.GetLogicalTexts(), t => t.Contains(expected));
    }

    [AvaloniaFact(DisplayName = "T-0208: Tarif anlegen → Bestätigung sichtbar")]
    public void Tier_add_confirms()
    {
        var (w, _, _) = Ui.Open();
        w.Show("biz-betrieb");
        w.Find<TextBox>("op-t-name").Text = "Gold";
        w.Find<Button>("op-t-add").Press();
        Assert.Contains(w.GetLogicalTexts(), t => t.Contains("Tarif „Gold“ angelegt"));
    }
}
