using System.Text.Json.Nodes;
using Trendo.Core.Kreativ;
using Trendo.Core.Media;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>T-0697 (D4b): Kern von Comic-Heft (freie Bildfelder, Blasen mit Dorn, Umbruch, Raster-Vorschlag, Vektor-PDF, CBZ) und Podcast-Schnitt.</summary>
public class T0697ComicPodcastTests
{
    [Fact(DisplayName = "Comic: Raster ist nur Vorschlag – Bilder bleiben, freie Ecken, Treffer im schrägen Feld, Zeilenumbruch")]
    public void Comic_Geometrie()
    {
        var page = ComicPage.Grid("2x2");
        page.Panels[1].Image = "C:/bild.png";
        ComicBook.ApplyLayout(page, "6er");
        Assert.Equal(6, page.Panels.Count); Assert.Equal("C:/bild.png", page.Panels[1].Image);
        ComicBook.ApplyLayout(page, "1");
        Assert.Equal(2, page.Panels.Count); // leere überzählige gehen, das Feld mit Bild bleibt
        var p = ComicPanel.Rect(100, 100, 200, 200);
        p.MoveCorner(1, 400, 60); // obere rechte Ecke weit nach rechts oben
        Assert.True(ComicBook.InQuad(p.P, 350, 90)); Assert.False(ComicBook.InQuad(p.P, 350, 290));
        p.Move(-500, 0); Assert.Equal(0, p.Bounds().X); // bleibt auf der Seite
        var lines = ComicBook.Wrap("Ein Satz mit mehreren Wörtern, der umbrechen muss.", 200, 20);
        Assert.True(lines.Count >= 3); Assert.All(lines, l => Assert.True(l.Length <= (int)(200 / (20 * 0.55))));
        var b = ComicBalloon.Make("sprech", 10, 10, 160, string.Join(' ', Enumerable.Repeat("Wort", 30)));
        var h = b.H; b.Fit(); Assert.True(b.H > h);
        b.Move(100, 50); Assert.Equal((110.0, 60.0), (b.X, b.Y)); Assert.Equal(b.X + b.W * 0.3, b.TailX, 3); // Dorn wandert mit
    }

    [Fact(DisplayName = "Comic: Heft speichern und lesen (Seiten, Blasen, Ebenen), altes Format wird umgewandelt, KI-Geschichte landet in den Feldern")]
    public void Comic_Format()
    {
        var j = ComicBook.NewDoc("Test", "comic", "2x2", 2);
        var book = ComicBook.Read(j);
        book.Pages[1].Balloons.Add(ComicBalloon.Make("gedanke", 50, 50, 200, "Hm …"));
        var paint = book.Pages[1].Paint(); var se = new StrokeEngine(paint.Layers[0].Px, BrushPreset.Get("tusche"), "#FF0000", 10);
        se.Add(100, 100); se.Add(300, 100); book.Pages[1].Art = paint.ToJson();
        book.Write(j);
        var again = ComicBook.Read(JsonNode.Parse(j.ToJsonString())!.AsObject());
        Assert.Equal(2, again.Pages.Count); Assert.Equal("gedanke", again.Pages[1].Balloons[0].Kind);
        Assert.True(again.Pages[1].HasArt); Assert.Equal(255, again.Pages[1].Paint().Composite(false).At(200, 100).A);
        var n = ComicBook.AdoptStory(again.Pages[0], "Bild 1: Hallo! | Morgens.\nBild 2: Tschüss.");
        Assert.Equal(2, n); Assert.Contains(again.Pages[0].Balloons, x => x.Text == "Morgens." && x.Kind == "kasten");
    }

    [Fact(DisplayName = "Comic: Vektor-PDF (DIN A4 je Seite, Text als Text) und CBZ mit ComicInfo.xml")]
    public void Comic_Pdf_und_Cbz()
    {
        var dir = Directory.CreateTempSubdirectory("trendo-t0697-").FullName;
        try
        {
            var book = ComicBook.Read(ComicBook.NewDoc("Heft", "storyboard", "sb-4", 3));
            book.Pages[0].Panels[0].Shot = "Totale: Hof.";
            book.Pages[2].Balloons.Add(ComicBalloon.Make("schrei", 100, 100, 300, "BUMM!"));
            var pdf = Path.Combine(dir, "heft.pdf");
            Assert.Equal(3, ComicExport.Pdf(book, pdf));
            using (var doc = PdfSharp.Pdf.IO.PdfReader.Open(pdf, PdfSharp.Pdf.IO.PdfDocumentOpenMode.Import))
            { Assert.Equal(3, doc.PageCount); Assert.InRange(doc.Pages[0].Height.Millimeter, 296, 298); }
            var png = PngCodec.Encode(new Pixmap(10, 14));
            var cbz = Path.Combine(dir, "heft.cbz");
            ComicExport.Cbz([png, png], cbz, "Heft & Co");
            using var zip = System.IO.Compression.ZipFile.OpenRead(cbz);
            Assert.Equal(["001.png", "002.png", "ComicInfo.xml"], zip.Entries.Select(e => e.FullName).ToArray());
            using var r = new StreamReader(zip.GetEntry("ComicInfo.xml")!.Open());
            var info = System.Xml.Linq.XDocument.Parse(r.ReadToEnd());
            Assert.Equal("Heft & Co", info.Root!.Element("Title")!.Value); Assert.Equal("2", info.Root.Element("PageCount")!.Value);
            Assert.Equal((10, 14), ComicExport.PngSize(png));
        }
        finally { Directory.Delete(dir, true); }
    }

    static string Wav(string dir, string name, double seconds, Func<double, double> amp)
    {
        var n = (int)(seconds * 44100); var c = new float[n];
        for (var i = 0; i < n; i++) c[i] = (float)(amp(i / 44100.0) * Math.Sin(2 * Math.PI * 300 * i / 44100.0));
        var f = Path.Combine(dir, name); AudioFile.WriteWav(new AudioBuf(44100, [c]), f); return f;
    }

    [Fact(DisplayName = "Podcast: Spuren mischen, Stelle löschen (Kapitel rücken mit), Pausen finden, Intro, Kapiteldatei, loudnorm-Messung und Export-Argumente")]
    public void Podcast_Schnitt()
    {
        var dir = Directory.CreateTempSubdirectory("trendo-t0697-").FullName;
        try
        {
            var d = new JsonObject { ["title"] = "F1", ["chapters"] = "00:00 Start\n00:05 Mitte\n00:09 Ende\nkaputt", ["audio"] = "" };
            PodcastEdit.AddTrack(d, "Sprecher 1", Wav(dir, "a.wav", 10, t => t is >= 4 and < 7 ? 0 : 0.2));
            PodcastEdit.AddTrack(d, "Sprecher 2", Wav(dir, "b.wav", 5, _ => 0.1));
            Assert.Equal(2, PodcastEdit.Tracks(d).Count); Assert.Equal("Sprecher 3", PodcastEdit.NextSpeaker(d));
            Assert.Equal(10, PodcastEdit.Render(d).Seconds, 2);
            PodcastEdit.ApplyCut(d, 1, 2);
            Assert.Equal(9, PodcastEdit.Render(d).Seconds, 2);
            Assert.Equal("00:00 Start\n00:04 Mitte\n00:08 Ende\nkaputt", (string)d["chapters"]!); // fehlerhafte Zeile bleibt stehen
            Assert.Throws<InvalidOperationException>(() => PodcastEdit.ApplyCut(d, 3, 3));
            // Pause 4–7 s (nach dem Schnitt 3–6 s) – Sprecher 2 spricht bis 4 s (nach dem Schnitt), danach ist 4–6 s still
            var pauses = PodcastEdit.FindPauses(PodcastEdit.Render(d));
            var p = Assert.Single(pauses); Assert.InRange(p.A, 4.1, 4.4); Assert.InRange(p.B, 5.6, 5.9);
            var (count, secs) = PodcastEdit.TrimPauses(d, PodcastEdit.Render(d));
            Assert.Equal(1, count); Assert.Equal(9 - secs, PodcastEdit.Render(d).Seconds, 1);
            // stumm geschaltete Spur fehlt in der Mischung
            PodcastEdit.SetTrack(d, PodcastEdit.Tracks(d)[1].Id, o => o["muted"] = true);
            Assert.DoesNotContain(PodcastEdit.Tracks(d), t => t.Name == "Sprecher 2" && !t.Muted);
            var (all, intro) = PodcastEdit.WithMusic(PodcastEdit.Render(d), AudioBuf.Silence(2, 44100, 2), null);
            Assert.Equal(2, intro, 2); Assert.Equal(PodcastEdit.Render(d).Seconds + 2, all.Seconds, 2);
            var meta = PodcastEdit.Metadata("F1", PodcastKit.ParseChapters("00:00 A\n00:04 B").Chapters, TimeSpan.FromSeconds(12), 2);
            Assert.Contains("START=0\n", meta); Assert.Contains("START=6000", meta); // Kapitel nach dem Intro rücken um 2 s
            Assert.Equal(-23.4, PodcastEdit.ParseMeasure("[Parsed_loudnorm_0]\n{\n\t\"input_i\" : \"-23.40\",\n\t\"input_tp\" : \"-6.10\",\n\t\"input_lra\" : \"2.30\",\n\t\"input_thresh\" : \"-33.50\",\n\t\"target_offset\" : \"0.20\"\n}\n")!.I, 2);
            Assert.Null(PodcastEdit.ParseMeasure("kein json"));
            var args = PodcastEdit.ExportArgs("in.wav", "m.txt", "out.mp3", new(-23.4, -6.1, 2.3, -33.5, 0.2), new("Titel", "Autorin", "Show", "2026", "", "3"));
            Assert.Contains(args, a => a.Contains("measured_I=-23.4") && a.Contains("linear=true") && a.StartsWith("loudnorm=I=-16"));
            Assert.Contains("title=Titel", args); Assert.Contains("artist=Autorin", args); Assert.Contains("genre=Podcast", args); Assert.Contains("track=3", args);
            Assert.DoesNotContain("comment=", args);
        }
        finally { Directory.Delete(dir, true); }
    }
}
