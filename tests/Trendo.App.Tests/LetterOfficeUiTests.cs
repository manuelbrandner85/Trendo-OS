using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Trendo.App;
using Trendo.Core;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>STUDIO-LETTER-01 Profi 5 + Claude 5: Briefart erkennen, Frist erst nach „Ja“ in den Kalender, Anlagen eintragen, Postausgangsbuch,
/// Wiedervorlage in der Akte, Übersicht „Steht an“ über alle Akten.</summary>
public class LetterOffice_Ui
{
    static void Pump() => Avalonia.Threading.Dispatcher.UIThread.RunJobs();

    [AvaloniaFact(DisplayName = "STUDIO-LETTER-01: Widerspruch erkannt, Pflichtangaben und Anlagen, Frist erst nach „Ja“ im Kalender, „Nein“ trägt nichts ein; Brief ins Postausgangsbuch")]
    public void Kind_and_deadline()
    {
        var (w, _, _) = Ui.Open();
        try
        {
            w.LetterToday = new DateTime(2026, 11, 6);
            w.Show("brief"); var d = w.Letter;
            d.Subject = "Widerspruch gegen den Bescheid vom 02.11.2026"; d.Body = "gegen den oben genannten Bescheid lege ich Widerspruch ein."; d.Date = new DateTime(2026, 11, 6);
            d.Recipient = "Rentenversicherung\nMusterweg 1\n12345 Musterstadt"; d.SenderName = "Erika Muster"; d.SenderCity = "12345 Musterstadt";
            w.Show("brief"); Pump();
            Assert.Equal("Erkannt: Widerspruch gegen einen Bescheid", w.Find<TextBlock>("letter-kind-name").Text);
            Assert.Contains("Aktenzeichen", string.Join("|", w.GetLogicalTexts()));
            Assert.Contains("07.12.2026", w.Find<TextBlock>("letter-kind-deadline").Text);
            w.Find<Button>("letter-kind-attach").Press(); Pump(); Assert.Equal("Kopie des Bescheids", d.Attachments);
            var before = w.Cal.All().Count;
            w.Find<Button>("letter-kind-cal").Press(); Pump();
            Assert.Equal(before, w.Cal.All().Count); // noch nichts – erst fragen
            w.Find<Button>("letter-kind-cal-no").Press(); Pump(); Assert.Equal(before, w.Cal.All().Count);
            w.Find<Button>("letter-kind-cal").Press(); Pump(); w.Find<Button>("letter-kind-cal-yes").Press(); Pump();
            Assert.Contains(w.Cal.All(), e => e.Start.Date == new DateTime(2026, 12, 7) && e.Title.StartsWith("Frist: Widerspruch"));
            w.Find<ComboBox>("letter-post-channel").SelectedIndex = 3; w.Find<Button>("letter-post-add").Press(); Pump();
            var e = Assert.Single(w.PostOutEntries); Assert.Equal(("Rentenversicherung", "fax"), (e.Recipient, e.Channel));
            Assert.Contains(w.Core.Audit.ReadAll(), a => a.Action == "letter.frist.kalender");
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "STUDIO-LETTER-01: Akte – Wiedervorlage setzen und erledigen, Versand mit Nachweis macht die Akte „versendet“ und steht im Postausgangsbuch; Übersicht zeigt fällige Wiedervorlagen und öffnet die Akte")]
    public void Case_resubmit_and_post()
    {
        var (w, _, _) = Ui.Open();
        try
        {
            w.LetterToday = new DateTime(2026, 11, 6);
            w.Show("akten"); Pump();
            w.Find<TextBox>("case-title").Text = "Kündigung Strom"; w.Find<Button>("case-create").Press(); Pump();
            var c = w.CurrentCase!; c.Draft.Recipient = "Stadtwerke\nWeg 1\n12345 Ort"; c.Draft.Subject = "Kündigung";
            w.Show("akte"); Pump();
            w.Find<TextBox>("case-resubmit-date").Text = "05.11.2026"; w.Find<TextBox>("case-resubmit-note").Text = "Bestätigung da?"; w.Find<Button>("case-resubmit-set").Press(); Pump();
            Assert.Equal(new DateTime(2026, 11, 5), w.CurrentCase!.ResubmitOn);
            w.Find<TextBox>("case-post-proof").Text = "RR123456789DE"; w.Find<Button>("case-post-add").Press(); Pump();
            Assert.Equal(DeliveryState.Versendet, w.CurrentCase!.State); Assert.Equal("RR123456789DE", w.CurrentCase.DeliveryProof);
            Assert.Contains(w.PostOutEntries, x => x.CaseId == c.Id && x.Channel == "einschreiben");
            w.Show("akten"); Pump();
            Assert.Contains("überfällig", ((TextBlock)w.Find<Button>("case-agenda-0").Content!).Text);
            w.Find<Button>("case-agenda-0").Press(); Pump(); Assert.Equal(c.Id, w.CurrentCase!.Id);
            w.Find<Button>("case-resubmit-done").Press(); Pump(); Assert.Null(w.CurrentCase!.ResubmitOn);
            w.Show("akten"); Pump(); Assert.Null(w.TryFind<Button>("case-agenda-0"));
            w.Find<Button>("post-book-csv").Press(); Pump();
            Assert.True(Directory.GetFiles(Path.Combine(w.ProjectsRootForTests, "Briefe"), "Postausgangsbuch-*.csv").Length == 1);
        }
        finally { w.Close(); }
    }
}
