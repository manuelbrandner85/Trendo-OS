using System.IO.Compression;
using Trendo.Core;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>Briefe (Teil 4): Vorlagen-Gruppen, Fristenrechner, Serienbrief, Unterschrift im DOCX.</summary>
public class LetterProTests
{
    [Fact(DisplayName = "Briefe F1: jede Vorlage gehört zu einer Galerie-Gruppe; neue Vorlagen Widerruf und Adressänderung")]
    public void Gallery_groups()
    {
        Assert.All(Letters.Categories, c => Assert.Contains(Letters.GroupOf(c), Letters.Groups));
        Assert.True(Letters.Category("widerruf").Legal);
        Assert.Equal("Verträge & Ämter", Letters.GroupOf(Letters.Category("kuendigung")));
    }

    [Fact(DisplayName = "Briefe F2: Frist in Tagen/Wochen/Monaten, Wochenende → nächster Werktag, [Datum] einsetzen")]
    public void Deadline()
    {
        Assert.Equal((new DateTime(2026, 11, 10), false), Letters.Deadline(new DateTime(2026, 10, 27), 14, "Tage"));
        Assert.Equal((new DateTime(2026, 11, 30), true), Letters.Deadline(new DateTime(2026, 10, 28), 1, "Monate")); // 28.11. ist Samstag
        Assert.Equal(new DateTime(2026, 11, 24), Letters.Deadline(new DateTime(2026, 10, 27), 4, "Wochen").Date);
        var d = new LetterDraft { Body = "bis zum [Datum] bitte." };
        Assert.True(Letters.FillDate(d, new DateTime(2026, 11, 10)));
        Assert.Equal("bis zum 10.11.2026 bitte.", d.Body);
        Assert.False(Letters.FillDate(d, DateTime.Today));
    }

    [Fact(DisplayName = "Briefe F3: Serienbrief – Felder ersetzen, Anschrift aus Spalten zusammensetzen, leere Zeilen auslassen")]
    public void Serial()
    {
        var tpl = new LetterDraft { Subject = "Einladung für {Vorname}", Salutation = "Liebe «Vorname»,", Body = "wir sehen uns am Freitag." };
        Assert.Equal(["Vorname"], Letters.SerialFields(tpl));
        string[] h = ["Vorname", "Nachname", "Straße", "PLZ", "Ort"];
        var rows = new List<IReadOnlyList<string>> { new[] { "Erika", "Muster", "Hauptstraße 1", "10115", "Berlin" }, new[] { "", "", "", "", "" }, new[] { "Max", "Beispiel", "Am Park 2", "50667", "Köln" } };
        var l = Letters.Serial(tpl, h, rows);
        Assert.Equal(2, l.Count);
        Assert.Equal("Einladung für Erika", l[0].Subject);
        Assert.Equal("Liebe Max,", l[1].Salutation);
        Assert.Equal("Erika Muster\nHauptstraße 1\n10115 Berlin", l[0].Recipient);
        Assert.Equal("Einladung für {Vorname}", tpl.Subject); // Vorlage bleibt unverändert
    }

    [Fact(DisplayName = "Briefe F4: Unterschrift als Bild im DOCX (4,5 cm breit, eigene Beziehung und Inhaltstyp); ohne Bild wie bisher")]
    public void Signature_docx()
    {
        var dir = Path.Combine(Path.GetTempPath(), "trendo-letter-" + Guid.NewGuid().ToString("N")[..8]); Directory.CreateDirectory(dir);
        try
        {
            var png = Path.Combine(dir, "sig.png");
            byte[] head = [0x89, (byte)'P', (byte)'N', (byte)'G', 13, 10, 26, 10, 0, 0, 0, 13, (byte)'I', (byte)'H', (byte)'D', (byte)'R', 0, 0, 0, 200, 0, 0, 0, 80, 8, 6, 0, 0, 0];
            File.WriteAllBytes(png, head);
            var d = new LetterDraft { SenderName = "Erika Muster", Subject = "Test", Body = "Text", Recipient = "A\nB\n12345 C", SignatureFile = png };
            var f = Path.Combine(dir, "brief.docx"); Letters.WriteDocx(d, f);
            using (var z = ZipFile.OpenRead(f))
            {
                Assert.NotNull(z.GetEntry("word/media/unterschrift.png"));
                Assert.Contains("rIdSig", new StreamReader(z.GetEntry("word/_rels/document.xml.rels")!.Open()).ReadToEnd());
                Assert.Contains("image/png", new StreamReader(z.GetEntry("[Content_Types].xml")!.Open()).ReadToEnd());
                var xml = new StreamReader(z.GetEntry("word/document.xml")!.Open()).ReadToEnd();
                Assert.Contains("cx=\"1620000\" cy=\"648000\"", xml);
            }
            Assert.Contains("Erika Muster", Letters.ReadDocxText(f));
            d.SignatureFile = ""; Letters.WriteDocx(d, f);
            using (var z = ZipFile.OpenRead(f)) Assert.Null(z.GetEntry("word/media/unterschrift.png"));
        }
        finally { Directory.Delete(dir, true); }
    }
}
