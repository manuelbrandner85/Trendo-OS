using Trendo.Core.Wissen;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>T-0779 Gegenprüfung: Prüfungen, die neue Hilfen brauchen (übersetzen auf dem alten Stand nicht).</summary>
public class T0779GegenpruefungTests
{
    [Fact(DisplayName = "T-0779 H1: Lückenregel – Erwartung für die Browser-Prüffälle (data-tq-rules) kommt aus derselben Regel wie Trendo")]
    public void H1_rule_cases() => Assert.Equal("00000111", QuizHtml.RuleExpectation);

    [Fact(DisplayName = "T-0779 Niedrig: Quiz im Kurs übernimmt die Bestehensgrenze des Kurses")]
    public void Course_threshold()
    {
        var k = new Kurs { Bestehen = 75 }; var q = new Quiz { Bestehen = 60 };
        Assert.Equal(75, KursKit.MitKursGrenze(q, k).Bestehen); Assert.Equal(60, q.Bestehen);
    }

    [Fact(DisplayName = "T-0779 2. Gegenprüfung: Bilder im Kurs am Inhalt geprüft (PNG/JPG/GIF/WEBP-Kennung, SVG mit Wurzel svg) – falsche Inhalte sind Fehler und werden nicht eingebettet")]
    public void Image_content_checked()
    {
        var root = Path.Combine(Path.GetTempPath(), "trendo-t0779-bild-" + Guid.NewGuid().ToString("N")[..8]); Directory.CreateDirectory(root);
        try
        {
            string F(string name, byte[] b) { var f = Path.Combine(root, name); File.WriteAllBytes(f, b); return f; }
            var png = F("echt.png", Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNkYAAAAAYAAjCB0C8AAAAASUVORK5CYII="));
            var fakePng = F("falsch.png", System.Text.Encoding.UTF8.GetBytes("Kontonummer 123"));
            var jpg = F("echt.jpg", [0xFF, 0xD8, 0xFF, 0xE0, 0, 0x10]); var gif = F("echt.gif", System.Text.Encoding.ASCII.GetBytes("GIF89a......"));
            var webp = F("echt.webp", System.Text.Encoding.ASCII.GetBytes("RIFF\0\0\0\0WEBPVP8 "));
            var svg = F("echt.svg", System.Text.Encoding.UTF8.GetBytes("<?xml version=\"1.0\"?><svg xmlns=\"http://www.w3.org/2000/svg\" width=\"1\" height=\"1\"/>"));
            var fakeSvg = F("falsch.svg", System.Text.Encoding.UTF8.GetBytes("<html><body>nein</body></html>"));
            foreach (var ok in new[] { png, jpg, gif, webp, svg }) Assert.True(KursKit.BildOk(ok), ok);
            foreach (var bad in new[] { fakePng, fakeSvg }) Assert.False(KursKit.BildOk(bad), bad);
            var k = new Kurs { Titel = "K", Kapitel = [new() { Titel = "A", Lektionen = [new() { Titel = "L", Bloecke = [new() { Art = "bild", Bild = fakePng }, new() { Art = "bild", Bild = png }] }] }] };
            Assert.Contains(KursKit.Pruefen(k), x => x.Fehler && x.Text.Contains("falsch.png"));
            var html = KursHtml.Html(k);
            Assert.Single(System.Text.RegularExpressions.Regex.Matches(html, "data:image/"));
            Assert.DoesNotContain(Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("Kontonummer 123")), html);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact(DisplayName = "T-0779 2. Gegenprüfung: CSV entschärft auch Tab und CR am Zellanfang")]
    public void Csv_tab_cr()
    {
        var k = new Kurs { Lernende = [new() { Name = "\t=1+1" }, new() { Name = "\r=2" }] };
        var csv = KursKit.Csv(k);
        Assert.Contains("\"'\t=1+1\"", csv); Assert.Contains("\"'\r=2\"", csv);
    }

    [Fact(DisplayName = "T-0779 M-A: Quiz-Lücken streng, Karteikarten unverändert (dox gilt bei dog weiter als Tippfehler)")]
    public void Quiz_rule_only()
    {
        Assert.False(QuizKit.LueckeOk("dog", "dox")); Assert.True(Trendo.Core.LearnBox.CheckTyped("dog", "dox").Ok);
        Assert.Equal(4, QuizKit.ExactUpTo);
    }
}
