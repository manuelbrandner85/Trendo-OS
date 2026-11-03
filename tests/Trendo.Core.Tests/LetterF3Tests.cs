using System.IO.Compression;
using Trendo.Core;
using Trendo.Core.Office;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>F3 Briefe (Vorbild-Analyse „Nächste Schritte“): Unterschrift zeichnen, Rechtschreibung beim Tippen, Falz- und Lochmarken im Druck.</summary>
public class LetterF3Tests
{
    static LetterDraft Draft() => new() { SenderName = "Erika Muster", SenderStreet = "Musterweg 1", SenderCity = "12345 Musterstadt", Recipient = "Firma\nStraße 2\n54321 Ort", Subject = "Test", Body = "Text" };

    [Fact(DisplayName = "Briefe F3: Falz- und Lochmarke liegen als verankerte Linien in der Kopfzeile der DOCX (Form B 105/148,5/210 mm, Form A 87/192 mm) und lassen sich abschalten")]
    public void Print_marks_in_docx()
    {
        var dir = Path.Combine(Path.GetTempPath(), "trendo-brief-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir);
        try
        {
            string Header(LetterDraft d, string name)
            {
                var f = Path.Combine(dir, name); Letters.WriteDocx(d, f);
                using var z = ZipFile.OpenRead(f);
                var doc = new StreamReader(z.GetEntry("word/document.xml")!.Open()).ReadToEnd();
                var h = z.GetEntry("word/header1.xml");
                if (h is null) { Assert.DoesNotContain("headerReference", doc); return ""; }
                Assert.Contains("r:id=\"rIdMarks\"", doc);
                Assert.Contains("header1.xml", new StreamReader(z.GetEntry("word/_rels/document.xml.rels")!.Open()).ReadToEnd());
                Assert.Contains("wordprocessingml.header+xml", new StreamReader(z.GetEntry("[Content_Types].xml")!.Open()).ReadToEnd());
                return new StreamReader(h.Open()).ReadToEnd();
            }
            var b = Header(Draft(), "b.docx");
            Assert.Equal(2, System.Text.RegularExpressions.Regex.Matches(b, "name=\"Falzmarke\"").Count); Assert.Contains("name=\"Lochmarke\"", b);
            Assert.Contains($"<wp:posOffset>{105 * 36000}</wp:posOffset>", b); Assert.Contains($"<wp:posOffset>{(long)(148.5 * 36000)}</wp:posOffset>", b); Assert.Contains($"<wp:posOffset>{210 * 36000}</wp:posOffset>", b);
            Assert.Contains("relativeFrom=\"page\"", b); Assert.Contains("behindDoc=\"1\"", b);
            var a = Draft(); a.Form = "A";
            var ha = Header(a, "a.docx"); Assert.Contains($"<wp:posOffset>{87 * 36000}</wp:posOffset>", ha); Assert.Contains($"<wp:posOffset>{192 * 36000}</wp:posOffset>", ha);
            var off = Draft(); off.PrintMarks = false;
            Assert.Equal("", Header(off, "aus.docx"));
            Assert.Contains("Text", Letters.ReadDocxText(Path.Combine(dir, "b.docx")));
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact(DisplayName = "Briefe F3: gezeichnete Unterschrift wird ein zugeschnittenes PNG mit dunkler Tinte; ein Tupfer oder nichts ergibt kein Bild")]
    public void Drawn_signature_png()
    {
        Assert.Null(LetterSignature.ToPng([]));
        Assert.Null(LetterSignature.ToPng([[(10, 10), (12, 11)]]));
        var png = LetterSignature.ToPng([[(20, 40), (60, 20), (100, 50), (140, 25)], [(150, 30), (170, 45)]])!;
        Assert.Equal(new byte[] { 137, 80, 78, 71 }, png[..4]);
        var (w, h) = XrPlus.ImageSize(png);
        Assert.Equal((int)Math.Ceiling((150 + 12) * 3.0), w); Assert.Equal((int)Math.Ceiling((30 + 12) * 3.0), h); // zugeschnitten + 6 px Rand, dreifach
    }

    [NeedsFact("repo:artifacts/vollpaket/staging/libreoffice/App/libreoffice/share/extensions/dict-de/de_DE_frami.dic", DisplayName = "Briefe F3: Rechtschreibung im Brieftext – Tippfehler mit Stelle und Vorschlag, Platzhalter [Datum] und richtige Wörter nicht angestrichen")]
    public void Spelling_in_letter_text()
    {
        string? dic = null;
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d is not null && dic is null; d = d.Parent)
            if (File.Exists(Path.Combine(d.FullName, "Trendo.sln"))) dic = Path.Combine(d.FullName, "artifacts/vollpaket/staging/libreoffice/App/libreoffice/share/extensions/dict-de/de_DE_frami.dic");
        if (dic is null || !File.Exists(dic)) return;
        var s = Spelling.Load([dic])!;
        var text = "Hiermit kündige ich meinen Vertrag zum [Datumm].\nMit freundlichen Grüssen und Dankee";
        var m = s.CheckText(text);
        Assert.Contains(m, x => x.Word == "Dankee" && text.Substring(x.Start, x.Length) == "Dankee");
        Assert.DoesNotContain(m, x => x.Word is "Datumm" or "kündige" or "Vertrag");
        Assert.Contains("Danke", s.Suggest("Dankee"));
    }
}
