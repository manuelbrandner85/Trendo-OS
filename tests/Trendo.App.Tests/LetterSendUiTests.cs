using System.Text;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Trendo.App;
using Trendo.Core;
using Xunit;
using Path = System.IO.Path;

namespace Trendo.App.Tests;

/// <summary>Wunsch 03.11.2026: In Briefe gingen Senden, Exportieren und „Als PDF“ nicht – und es soll viel mehr Farben und Designs wie bei Canva geben.</summary>
[Collection("Claude")]
public class LetterSendUiTests
{
    static async Task Pump() { for (var i = 0; i < 8; i++) { Dispatcher.UIThread.RunJobs(); await Task.Delay(15); } }

    static void Fill(MainWindow w)
    {
        var d = w.Letter;
        d.SenderName = "Erika Muster"; d.SenderStreet = "Musterweg 1"; d.SenderCity = "12345 Musterstadt";
        d.Recipient = "Beispiel GmbH\nHauptstraße 2\n54321 Beispielort"; d.Subject = "Kündigung Vertrag 4711"; d.Body = "hiermit kündige ich fristgerecht.";
        w.Show("brief");
    }

    [AvaloniaFact(DisplayName = "Briefe: „Als PDF“ ohne LibreOffice (Trendo setzt das PDF selbst), fehlender Punkt wird genannt und angesprungen")]
    public async Task Pdf_without_libreoffice()
    {
        var (w, _, root) = Ui.Open();
        try
        {
            w.Show("brief"); await Pump();
            Assert.True(w.Find<Button>("letter-pdf").IsEnabled); // kein LibreOffice im Testordner – trotzdem an
            w.Find<Button>("letter-pdf").Press(); await Pump();
            Assert.Contains("Noch nicht fertig – Absender: Dein Name fehlt.", w.Find<TextBlock>("letter-msg").Text);
            Assert.Equal(2, w.LetterStep); // zum Absender gesprungen
            Fill(w); await Pump();
            w.Find<Button>("letter-pdf").Press(); await Pump();
            var pdf = w.LetterPdfFile!;
            Assert.True(File.Exists(pdf), w.Find<TextBlock>("letter-msg").Text);
            Assert.StartsWith("%PDF", Encoding.ASCII.GetString(File.ReadAllBytes(pdf), 0, 4));
            Assert.Contains("Kündigung Vertrag 4711", string.Join(" ", PdfView.Text(pdf, 0).Split('\n')));
            Assert.True(File.Exists(Path.ChangeExtension(pdf, ".docx")));
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "Briefe: „Exportieren“ fragt nach Ort und Format – PDF oder Word landen genau dort")]
    public async Task Export_to_chosen_place()
    {
        var (w, _, root) = Ui.Open();
        try
        {
            Fill(w); await Pump();
            var dir = Path.Combine(root, "export"); Directory.CreateDirectory(dir);
            foreach (var name in new[] { "mein-brief.pdf", "mein-brief.docx" })
            {
                var target = Path.Combine(dir, name);
                w.PickOverride = req => { Assert.Equal("letter-export", req.Field); return Task.FromResult<string[]?>([target]); };
                w.Find<Button>("letter-export").Press(); await Pump();
                Assert.True(File.Exists(target), w.Find<TextBlock>("letter-msg").Text);
                Assert.Contains("Exportiert", w.Find<TextBlock>("letter-msg").Text);
            }
            Assert.Contains("Kündigung Vertrag 4711", Letters.ReadDocxText(Path.Combine(dir, "mein-brief.docx")));
            w.PickOverride = _ => Task.FromResult<string[]?>(null);
            w.Find<Button>("letter-export").Press(); await Pump();
            Assert.Contains("abgebrochen", w.Find<TextBlock>("letter-msg").Text);
        }
        finally { w.PickOverride = null; w.Close(); }
    }

    [AvaloniaFact(DisplayName = "Briefe: „Senden“ – E-Mail-Entwurf mit dem Brief als PDF-Anhang öffnet sich im Mailprogramm (abschicken tut man selbst) oder Drucken")]
    public async Task Send_as_mail_or_print()
    {
        var (w, l, root) = Ui.Open();
        var oldDrafts = MainWindow.MailDraftFolder;
        MainWindow.MailDraftFolder = Path.Combine(root, "entwuerfe");
        try
        {
            Fill(w); await Pump();
            w.Find<Button>("letter-send").Press(); await Pump();
            Assert.NotNull(w.TryFind<Border>("letter-send-card"));
            w.Find<TextBox>("letter-send-to").Text = "kundenservice@example.org";
            w.Find<Button>("letter-send-mail").Press(); await Pump();
            var eml = File.ReadAllText(w.LastDraftFile!);
            Assert.Contains("X-Unsent: 1", eml); Assert.Contains("To: kundenservice@example.org", eml);
            Assert.Contains("multipart/mixed", eml); Assert.Contains("Content-Type: application/pdf", eml);
            Assert.Contains(w.LastDraftFile!, l.Opened); // Mailprogramm geöffnet, nichts selbst verschickt
            Assert.Null(w.TryFind<Border>("letter-send-card"));
            w.Find<Button>("letter-send").Press(); await Pump();
            w.Find<Button>("letter-send-print").Press(); await Pump();
            Assert.Contains(w.LetterPdfFile!, l.Opened);
        }
        finally { MainWindow.MailDraftFolder = oldDrafts; w.Close(); }
    }

    [AvaloniaFact(DisplayName = "Briefe-Designs wie Canva: Galerie mit Vorschaubildern (8, „Alle“ zeigt 20), große Farbpalette, zweite Farbe, neue Kopf-Layouts in Vorschau und PDF")]
    public async Task Design_gallery()
    {
        var (w, _, root) = Ui.Open();
        try
        {
            Fill(w); w.LetterGoStep(3); await Pump();
            var gallery = w.Find<WrapPanel>("letter-design-gallery");
            Assert.Equal(8, gallery.Children.Count);
            Assert.Null(w.TryFind<Rectangle>("letter-design-band")); // klassisch – die Vorschaubilder (mit Band) tragen keine Namen
            w.Find<Button>("letter-design-more").Press(); await Pump();
            Assert.Equal(LetterDesigns.Styles.Length, w.Find<WrapPanel>("letter-design-gallery").Children.Count);
            Assert.True(LetterDesigns.Styles.Length >= 20);
            w.Find<Button>("letter-design-style-navy-gold").Press(); await Pump();
            Assert.Equal(("navy-gold", "zweifarbig", "#1B2A4A", "#C9A227"), (w.Letter.Design, w.Letter.Header, w.Letter.Accent, w.Letter.Accent2));
            Assert.NotNull(w.TryFind<Rectangle>("letter-design-stripe"));
            Assert.True(w.GetLogicalDescendants().OfType<Button>().Count(b => b.Name?.StartsWith("letter-design-color-") == true && b.Name != "letter-design-color-eigen") >= 24);
            w.Find<Button>("letter-design-color2-e4572e").Press(); await Pump();
            Assert.Equal("#E4572E", w.Letter.Accent2);
            w.Find<Button>("letter-design-head-fussband").Press(); await Pump();
            Assert.NotNull(w.TryFind<Rectangle>("letter-design-foot"));
            w.Find<Button>("letter-design-head-rahmen").Press(); await Pump();
            Assert.NotNull(w.TryFind<Rectangle>("letter-design-frame"));
            w.Find<Button>("letter-design-head-mittig").Press(); await Pump();
            Assert.Equal(Avalonia.Media.TextAlignment.Center, w.Find<TextBlock>("letter-preview-sender-name").TextAlignment);
            w.Find<Button>("letter-pdf").Press(); await Pump();
            Assert.True(File.Exists(w.LetterPdfFile));
        }
        finally { w.Close(); }
    }
}
