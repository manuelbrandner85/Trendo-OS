using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Trendo.Core;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// T-0307: Adressbuch mit echten Klicks – neu anlegen und bearbeiten, Suche, Gruppe, Favorit, Doppelte zusammenführen nach Ja, Geburtstage in den Kalender, löschen nach Ja, E-Mail schreiben.
public class AddressBook_Ui
{
    [AvaloniaFact(DisplayName = "T-0307: Adressbuch – Neuer Kontakt bearbeiten/speichern, Karte, Gruppe, Favorit, Suche, Doppelte Nein/Ja, Geburtstage in den Kalender, E-Mail schreiben, Löschen Nein/Ja")]
    public void Ui_addressbook()
    {
        var (w, _, root) = Ui.Open(); w.Width = 1600; w.Height = 1100;
        void Click(string name) { var b = V<Control>(w, name); b.BringIntoView(); Settle(); RealClick(w, V<Control>(w, name)); Settle(); }
        w.Mail.UpdateContact(new Contact("b1", "Max Beispiel", "max@example.de", Mobile: "0171 1234567"));
        w.Mail.UpdateContact(new Contact("b2", "Beispiel, Max", "", Phone: "+49 171 1234567", Company: "Beispiel AG"));
        w.Show("kontakte"); Settle();
        // neu anlegen
        Click("ab-new"); Assert.True(w.AddressEditing);
        w.Find<TextBox>("ab-e-name").Text = "Erika Muster"; w.Find<TextBox>("ab-e-email").Text = "erika@example.de"; w.Find<TextBox>("ab-e-mobile").Text = "0160 999";
        var bday = DateTime.Today.AddDays(5); w.Find<TextBox>("ab-e-birthday").Text = new DateTime(1990, bday.Month, bday.Day).ToString("dd.MM.yyyy");
        w.Find<TextBox>("ab-e-groups").Text = "Familie, Verein";
        var photo = Path.Combine(root, "foto.png"); File.WriteAllBytes(photo, [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]); w.Find<TextBox>("ab-e-photo-file").Text = photo;
        Click("ab-e-save");
        Assert.False(w.AddressEditing);
        var e = w.Mail.Contacts().Single(c => c.Name == "Erika Muster");
        Assert.Equal(["Familie", "Verein"], e.Groups); Assert.True(File.Exists(e.Photo)); Assert.Equal("Erika Muster", w.Find<TextBlock>("ab-card-name").Text);
        // Falsche Eingabe wird abgewiesen
        Click("ab-edit"); w.Find<TextBox>("ab-e-email").Text = "kaputt"; Click("ab-e-save"); Assert.True(w.AddressEditing); Assert.Equal("erika@example.de", w.Mail.Get(e.Id).Email); Click("ab-e-cancel");
        // Favorit, Gruppe, Suche
        Click("ab-fav"); Assert.True(w.Mail.Get(e.Id).Favorite);
        Click("ab-g-fav"); Assert.Equal(1, w.Find<StackPanel>("ab-list").Children.OfType<Button>().Count());
        Click("ab-g-0"); Assert.Contains(w.GetLogicalTexts(), t => t == "Erika Muster");
        Click("ab-g-alle"); var find = w.Find<TextBox>("ab-find"); find.Text = "1234567";
        find.RaiseEvent(new Avalonia.Input.KeyEventArgs { RoutedEvent = Avalonia.Input.InputElement.KeyDownEvent, Key = Avalonia.Input.Key.Enter }); Settle();
        Assert.Equal(2, w.Find<StackPanel>("ab-list").Children.OfType<Button>().Count());
        find = w.Find<TextBox>("ab-find"); find.Text = ""; find.RaiseEvent(new Avalonia.Input.KeyEventArgs { RoutedEvent = Avalonia.Input.InputElement.KeyDownEvent, Key = Avalonia.Input.Key.Enter }); Settle();
        // Bald Geburtstag
        Click("ab-g-bday"); Assert.Contains(w.GetLogicalTexts(), t => t.Contains("wird 3") || t.Contains("wird "));
        Click("ab-bday-cal");
        var ev = Assert.Single(w.Cal.All(), x => x.Uid == "geburtstag-" + e.Id); Assert.Equal(Trendo.Core.Sys.Repeat.Jaehrlich, ev.Repeat); Assert.True(ev.AllDay);
        Click("ab-bday-cal"); Assert.Single(w.Cal.All(), x => x.Uid == "geburtstag-" + e.Id); // kein Doppel
        // Doppelte: Nein ändert nichts, Ja führt zusammen
        Click("ab-g-dups"); Click("ab-merge-0"); Click("ab-merge-no-0"); Assert.Equal(3, w.Mail.Contacts().Count);
        Click("ab-merge-0"); Click("ab-merge-yes-0");
        Assert.Equal(2, w.Mail.Contacts().Count); var max = w.Mail.Contacts().Single(c => c.Email == "max@example.de"); Assert.Equal("Beispiel AG", max.Company);
        // E-Mail schreiben → Post mit Empfänger
        Click("ab-g-alle"); Click("ab-c-0");
        Click("ab-mail"); Assert.Equal("post", w.CurrentView); Assert.Contains("@example.de", w.Find<TextBox>("mail-to").Text);
        // Löschen: Nein, dann Ja
        w.Show("kontakte"); Settle(); Click("ab-c-0"); var sel = w.AddressSelected;
        Click("ab-delete"); Click("ab-delete-no"); Assert.Equal(2, w.Mail.Contacts().Count);
        Click("ab-delete"); Click("ab-delete-yes"); Assert.Single(w.Mail.Contacts()); Assert.DoesNotContain(w.Mail.Contacts(), c => c.Id == sel);
    }
    [AvaloniaFact(DisplayName = "Adressbuch wie Google Kontakte/Outlook: CSV einlesen und sichern, Nummern vereinheitlichen, Etiketten der gewählten Gruppe als PDF")]
    public void Ui_csv_phones_labels()
    {
        var (w, _, root) = Ui.Open(); w.Width = 1600; w.Height = 1100; Directory.CreateDirectory(root);
        var oldDrafts = MainWindow.MailDraftFolder; MainWindow.MailDraftFolder = Path.Combine(root, "entwuerfe"); // nie in die echten Dokumente schreiben
        try
        {
            void Click(string name) { var b = V<Control>(w, name); b.BringIntoView(); Settle(); RealClick(w, V<Control>(w, name)); Settle(); }
            w.Show("kontakte"); Settle();
            var csv = Path.Combine(root, "google.csv");
            File.WriteAllText(csv, "First Name,Last Name,E-mail 1 - Value,Phone 1 - Value,Address 1 - Street,Address 1 - City,Group Membership\r\n"
                + "Erika,Muster,erika@example.de,030 1234567,Hauptstraße 1,10115 Berlin,Kunden\r\nMax,Beispiel,,0171 7654321,,,Kunden\r\n");
            w.Find<TextBox>("ab-vcf-file").Text = csv; Click("ab-vcf-import");
            Assert.Equal(2, w.Mail.Contacts().Count);
            Click("ab-fix-phones");
            Assert.Equal("+49 30 1234567", w.Mail.Contacts().Single(c => c.Name == "Erika Muster").Phone);
            Assert.Equal("+49 171 7654321", w.Mail.Contacts().Single(c => c.Name == "Max Beispiel").Phone);
            // Etiketten für die Gruppe „Kunden“: einer ohne Anschrift wird übersprungen
            Click("ab-g-0"); Click("ab-labels");
            Assert.True(File.Exists(Path.Combine(MainWindow.MailDraftFolder, "etiketten.pdf")));
            using (var pdf = PdfSharp.Pdf.IO.PdfReader.Open(Path.Combine(MainWindow.MailDraftFolder, "etiketten.pdf"), PdfSharp.Pdf.IO.PdfDocumentOpenMode.Import)) Assert.Equal(1, pdf.PageCount); // einer ohne Anschrift übersprungen
            Click("ab-csv-export");
            var back = File.ReadAllText(Path.Combine(MainWindow.MailDraftFolder, "kontakte.csv"));
            Assert.Contains("Erika Muster", back); Assert.Contains("+49 30 1234567", back);
        }
        finally { MainWindow.MailDraftFolder = oldDrafts; w.Close(); }
    }
}
