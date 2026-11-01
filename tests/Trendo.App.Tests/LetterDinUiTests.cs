using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Trendo.Core;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// T-0310: Brief nach DIN 5008 – Empfänger aus dem Adressbuch, Form A/B, Informationsblock, echte Folgeseiten, Serienbrief an eine Gruppe mit Vorschau.
public class LetterDin_Ui
{
    [AvaloniaFact(DisplayName = "T-0310: Brief – Kontakt einsetzen, Form wechseln, Informationsblock in der Vorschau, langer Text auf Folgeseiten mit „Seite n von N“, Serienbrief an Gruppe ansehen und speichern")]
    public void Ui_letter()
    {
        var (w, _, root) = Ui.Open(); w.Width = 1700; w.Height = 1200;
        void Click(string name) { var b = V<Control>(w, name); b.BringIntoView(); Settle(); RealClick(w, V<Control>(w, name)); Settle(); }
        void Tool(string name) { w.Find<Button>(name).Press(); Settle(); } // Werkzeugkästen unten in der linken Spalte (wie in LetterProUiTests)
        w.Mail.UpdateContact(new Contact("e", "Erika Muster", "erika@example.de", Street: "Hauptstraße 1", City: "10115 Berlin", Groups: ["Verein"]));
        w.Mail.UpdateContact(new Contact("m", "Max Beispiel", "", Street: "Ring 5", City: "80331 München", Groups: ["Verein"]));
        w.Mail.UpdateContact(new Contact("o", "Ohne Anschrift", "o@example.de", Groups: ["Verein"]));
        w.Show("brief"); Settle();
        if (w.TryFind<Button>("letter-gallery-frei") is not null) Click("letter-gallery-frei");
        // Empfänger aus dem Adressbuch
        w.Find<ComboBox>("letter-contact-pick").SelectedIndex = 0; Tool("letter-contact-use");
        Assert.StartsWith("Erika Muster\nHauptstraße 1", w.Letter.Recipient); Assert.Equal("Guten Tag Erika Muster,", w.Letter.Salutation);
        Assert.StartsWith("Erika Muster", w.Find<TextBlock>("letter-preview-recipient").Text);
        // Form wechseln und Informationsblock
        Tool("letter-form-a"); Assert.Equal("A", w.Letter.Form);
        w.Find<TextBox>("letter-our-ref").Text = "em/26"; Settle();
        Assert.Contains("Unser Zeichen: em/26", w.Find<TextBlock>("letter-preview-date").Text);
        Tool("letter-form-b"); Assert.Equal("B", w.Letter.Form);
        // langer Text → zwei Seiten
        w.Letter.Subject = "Langer Brief"; w.Letter.Body = string.Join("\n\n", Enumerable.Range(1, 40).Select(i => $"Absatz {i}: Dies ist ein ausführlicher Satz, damit der Brief über eine Seite hinausgeht."));
        w.Show("brief"); Settle();
        var n = w.Find<StackPanel>("letter-pages").Children.Count; Assert.InRange(n, 2, 6);
        Assert.Equal($"Seite {n} von {n}", w.Find<TextBlock>($"letter-preview-pageno-{n}").Text);
        Assert.Contains("Absatz 40", w.Find<TextBlock>($"letter-preview-body-{n}").Text);
        Assert.Contains("Mit freundlichen Grüßen", w.GetLogicalTexts()); // Gruß auf der letzten Seite
        Assert.DoesNotContain("Absatz 40", w.Find<TextBlock>("letter-preview-body").Text);
        // Serienbrief an die Gruppe „Verein“
        w.Letter.SenderName = "Verein Muster e. V."; w.Letter.SenderStreet = "Vereinsweg 1"; w.Letter.SenderCity = "10115 Berlin";
        w.Letter.Body = "Hallo {Vorname}, wir sehen uns in {Ort}."; w.Letter.Recipient = ""; w.Letter.Salutation = "Sehr geehrte Damen und Herren,"; w.Show("brief"); Settle();
        w.Find<ComboBox>("letter-serial-group").SelectedItem = "Verein"; Tool("letter-serial-group-preview");
        Assert.Equal(2, w.LetterSerialPreview!.Count);
        Assert.StartsWith("Serienbrief 1 von 2", w.Find<TextBlock>("letter-serial-pos").Text);
        Click("letter-serial-next"); Assert.Contains("Max Beispiel", w.Find<TextBlock>("letter-serial-pos").Text);
        Assert.Contains("München", w.Find<TextBlock>("letter-preview-body").Text);
        Tool("letter-serial-group-run");
        Assert.Equal(2, Directory.GetFiles(root, "*.docx", SearchOption.AllDirectories).Count(f => f.Contains("Serienbrief")));
        Assert.Null(w.LetterSerialPreview);
    }
}
