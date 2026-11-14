using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using Trendo.Core.Kreativ;
using Trendo.Core.PromptAction;
using Trendo.Core.Sys;
using Xunit;

namespace Trendo.Core.Tests;

public class KreativKits2Tests
{
    [Fact(DisplayName = "Bücher: Kapitel und Vorwort, Wortzahl, Seiten, Ziel, EPUB 3 gültig (mimetype zuerst, unkomprimiert, XHTML lesbar)")]
    public void Buch()
    {
        const string M = "Ein kurzer Anfang.\n\n# Erstes Kapitel\n\nOle stieg die Stufen hinauf. **Laut** und *leise*.\n\nZweiter Absatz.\n\n# Zweites & <Letztes>\n\nEnde.";
        var ch = BookKit.Chapters(M);
        Assert.Equal(["Vorwort", "Erstes Kapitel", "Zweites & <Letztes>"], ch.Select(c => c.Title));
        Assert.Equal(3, ch[0].Words);
        var s = BookKit.Of(M, 100);
        Assert.Equal(ch.Sum(c => c.Words), s.Words);
        Assert.Equal(1, s.Pages);
        Assert.Equal(Math.Round(100.0 * s.Words / 100, 1), s.GoalPercent);
        Assert.Equal(0, BookKit.Of("", 0).Words);
        Assert.EndsWith("# Kapitel 3\n\n", BookKit.NextChapter(M));
        Assert.Equal(BookKit.BookId("A", "B"), BookKit.BookId("A", "B"));
        Assert.NotEqual(BookKit.BookId("A", "B"), BookKit.BookId("A", "C"));

        var epub = BookKit.Epub("Mein Buch", "Erika Muster", M, new DateTimeOffset(2026, 11, 11, 10, 0, 0, TimeSpan.Zero));
        Assert.Equal("mimetype", Encoding.ASCII.GetString(epub, 30, 8)); // erster Eintrag im ZIP
        Assert.Equal("application/epub+zip", Encoding.ASCII.GetString(epub, 38, 20)); // unkomprimiert direkt dahinter
        using var zip = new ZipArchive(new MemoryStream(epub));
        string Read(string n) { using var r = new StreamReader(zip.GetEntry(n)!.Open()); return r.ReadToEnd(); }
        var opf = XDocument.Parse(Read("OEBPS/content.opf"));
        Assert.Contains("Mein Buch", opf.ToString());
        Assert.Contains("2026-11-11T10:00:00Z", opf.ToString());
        Assert.Equal(3, opf.Descendants().Count(e => e.Name.LocalName == "itemref"));
        XDocument.Parse(Read("OEBPS/nav.xhtml"));
        var k2 = XDocument.Parse(Read("OEBPS/k002.xhtml"));
        Assert.Contains(k2.Descendants().Where(e => e.Name.LocalName == "strong"), e => e.Value == "Laut");
        Assert.Contains(k2.Descendants().Where(e => e.Name.LocalName == "em"), e => e.Value == "leise");
        Assert.Contains("Zweites & <Letztes>", XDocument.Parse(Read("OEBPS/k003.xhtml")).ToString(SaveOptions.DisableFormatting).Replace("&amp;", "&").Replace("&lt;", "<").Replace("&gt;", ">"));
        // verschachtelte Sternchen: Kapitel bleibt gültiges XHTML
        var tricky = BookKit.Epub("T", "", "# K\n\n***Wichtig*** bleibt.\n\n**a *b** c*", DateTimeOffset.UtcNow);
        using (var tz = new ZipArchive(new MemoryStream(tricky)))
        using (var r = new StreamReader(tz.GetEntry("OEBPS/k001.xhtml")!.Open()))
        {
            var doc = XDocument.Parse(r.ReadToEnd());
            Assert.Contains(doc.Descendants().Where(e => e.Name.LocalName == "em"), e => e.Value == "Wichtig");
            Assert.Contains(doc.Descendants().Where(e => e.Name.LocalName == "p"), e => e.Value == "**a *b** c*"); // ungültig verschachtelt: als Text
        }
        Assert.Throws<ArgumentException>(() => BookKit.Epub(" ", "", M, DateTimeOffset.UtcNow));
        Assert.Throws<InvalidOperationException>(() => BookKit.Epub("T", "", "", DateTimeOffset.UtcNow));
    }

    [Fact(DisplayName = "Animation: Übergänge 0→1, Werte vor/zwischen/nach Schlüsseln, Schlüssel ersetzen, animiertes SVG gültig")]
    public void Animation()
    {
        foreach (var (id, _) in AnimKit.Eases) { Assert.Equal(0, AnimKit.Ease(id, 0), 6); Assert.Equal(1, AnimKit.Ease(id, 1), 6); }
        Assert.Equal(0.25, AnimKit.Ease("ein", 0.5), 6);
        var l = new AnimKit.Layer("a", "rechteck", "", "#112233", 100, 50, []);
        Assert.Equal(1, AnimKit.ValueOf(l, "deckkraft", 3));
        AnimKit.SetKey(l, "x", 2, 300, "linear"); AnimKit.SetKey(l, "x", 0, 100, "linear");
        Assert.Equal(100, AnimKit.ValueOf(l, "x", -1)); Assert.Equal(200, AnimKit.ValueOf(l, "x", 1), 6); Assert.Equal(300, AnimKit.ValueOf(l, "x", 9));
        AnimKit.SetKey(l, "x", 2.001, 500);
        Assert.Equal(2, l.Tracks["x"].Count); // gleiche Zeit ersetzt
        Assert.Equal(500, AnimKit.ValueOf(l, "x", 2));
        Assert.Throws<ArgumentException>(() => AnimKit.SetKey(l, "farbe", 0, 1));
        Assert.Equal(2, AnimKit.Duration([l]));
        Assert.Equal(1, AnimKit.Duration([]));
        var t = AnimTitleAdapter.Title("Hallo & <Welt>", "einfliegen", "#F5C542");
        var svg = AnimKit.Svg([l, t], "#000000", 10);
        var x = XElement.Parse(svg);
        Assert.Contains("Hallo & <Welt>", x.Value);
        Assert.All(x.Descendants().Where(e => e.Name.LocalName is "animate" or "animateTransform"), e => Assert.Equal(e.Attribute("keyTimes")!.Value.Split(';').Length, e.Attribute("values")!.Value.Split(';').Length));
        Assert.Throws<ArgumentException>(() => AnimKit.Svg([l with { Color = "rot" }], "#000000"));
        Assert.Contains("yuv420p", AnimKit.VideoArgs("f%05d.png", 30, "a.mp4"));
    }

    [Fact(DisplayName = "Fotos: Plan nach Jahr/Monat, Umbenennen, Namenskonflikte, Doppelte übersprungen, Kopieren ohne Überschreiben und Rückgängig")]
    public void Fotos()
    {
        var root = Path.Combine(Path.GetTempPath(), "trendo-fotos-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            var src = Path.Combine(root, "quelle"); var dst = Path.Combine(root, "ziel"); Directory.CreateDirectory(src);
            string F(string n, byte b, DateTime when) { var f = Path.Combine(src, n); File.WriteAllBytes(f, [b, b, b, 1]); File.SetLastWriteTime(f, when); return f; }
            F("a.jpg", 1, new DateTime(2025, 3, 4, 10, 0, 0)); F("b.jpg", 2, new DateTime(2026, 11, 11, 14, 30, 5));
            F("c.jpg", 2, new DateTime(2026, 11, 11, 14, 30, 5)); // gleicher Inhalt wie b
            Directory.CreateDirectory(Path.Combine(src, "unter")); File.WriteAllBytes(Path.Combine(src, "unter", "a.jpg"), [9, 9, 9]); File.SetLastWriteTime(Path.Combine(src, "unter", "a.jpg"), new DateTime(2025, 3, 20));
            var photos = PhotoAlbum.Scan([src]);
            Assert.Equal(4, photos.Count);
            var plan = PhotoOrganizer.Plan(photos, dst, "jahr-monat", rename: false);
            Assert.Equal(3, plan.Count); // ein Doppel weniger
            Assert.Contains(plan, m => m.To == Path.Combine(dst, "2025", "03 März", "a.jpg"));
            Assert.Contains(plan, m => m.To == Path.Combine(dst, "2025", "03 März", "a (2).jpg")); // Namenskonflikt
            var renamed = PhotoOrganizer.Plan(photos, dst, "tag", rename: true, skipDuplicates: false);
            Assert.Equal(4, renamed.Count);
            Assert.Contains(renamed, m => m.To == Path.Combine(dst, "2026", "2026-11-11", "2026-11-11 14-30-05 (2).jpg"));
            Assert.Throws<ArgumentException>(() => PhotoOrganizer.Plan(photos, "relativ", "jahr", false));

            var undo = Path.Combine(root, "undo");
            File.WriteAllBytes(Path.Combine(Directory.CreateDirectory(Path.Combine(dst, "2026", "11 November")).FullName, "b.jpg"), [7]); // liegt schon da
            plan = PhotoOrganizer.Plan(photos, dst, "jahr-monat", false);
            Assert.Contains(plan, m => m.To.EndsWith("b (2).jpg"));
            var log = PhotoOrganizer.Run(plan, undo, copy: true);
            Assert.All(plan, m => Assert.True(File.Exists(m.To)));
            Assert.Equal([7], File.ReadAllBytes(Path.Combine(dst, "2026", "11 November", "b.jpg"))); // nicht überschrieben
            Assert.Equal(4, Directory.GetFiles(src, "*", SearchOption.AllDirectories).Length); // Originale bleiben
            Assert.Empty(PhotoOrganizer.Plan(photos, dst, "jahr-monat", false)); // zweiter Lauf: alles schon da, nichts doppelt
            var changed = plan[0].To; File.WriteAllBytes(changed, [5, 5, 5, 5]); // Kopie inzwischen bearbeitet, gleiche Größe
            Assert.Equal((2, 1), PhotoOrganizer.Undo(log));
            Assert.True(File.Exists(changed)); // bearbeitete Kopie bleibt
            File.Delete(changed);
            Assert.All(plan, m => Assert.False(File.Exists(m.To)));

            // Verschieben und zurück, ohne Zwischennamen
            var moveLog = PhotoOrganizer.Run(PhotoOrganizer.Plan(photos, dst, "jahr", false), undo, copy: false);
            Assert.Single(Directory.GetFiles(src, "*", SearchOption.AllDirectories)); // das Doppel bleibt in der Quelle
            Assert.Empty(Directory.GetFiles(root, "*.trendo-*", SearchOption.AllDirectories));
            Assert.Equal((3, 0), PhotoOrganizer.Undo(moveLog));
            Assert.Equal(4, Directory.GetFiles(src, "*", SearchOption.AllDirectories).Length);
            Assert.Throws<ArgumentException>(() => PhotoOrganizer.Plan(photos, "//rechner/freigabe", "jahr", false));
            var (years, noExif, dups) = PhotoOrganizer.Summary(photos);
            Assert.Equal([(2026, 2), (2025, 2)], years);
            Assert.Equal(4, noExif); Assert.Equal(1, dups);
        }
        finally { try { Directory.Delete(root, true); } catch (IOException) { } }
    }

    [Fact(DisplayName = "Bild-KI: Größen in 64er-Schritten, Sperre für Anstößiges, Stil im Prompt, Startwerte, Herkunftsnachweis mit Prüfsumme")]
    public void BildKi()
    {
        Assert.Equal((1024, 1024), ImageAiKit.Size("quadrat", 1024));
        var (w, h) = ImageAiKit.Size("quer", 1024);
        Assert.True(w > h && w % 64 == 0 && h % 64 == 0);
        Assert.Equal((512, 512), ImageAiKit.Size("gibtsnicht", 512));
        Assert.NotNull(ImageAiKit.Blocked(""));
        Assert.NotNull(ImageAiKit.Blocked("eine nackte Person"));
        Assert.Null(ImageAiKit.Blocked("ein Leuchtturm"));
        foreach (var bad in new[] { "ein nackter Mann", "Nacktfoto", "nudity", "topless am Strand", "unbekleidete Figur" }) Assert.NotNull(ImageAiKit.Blocked(bad));
        Assert.True(ImageAiKit.Seed(int.MinValue, 0) >= 0);
        Assert.Equal(ImageAiKit.Seed(42, 1), ImageAiKit.Seed(42, 1));
        Assert.EndsWith("watercolor painting on textured paper, soft edges, gentle colors", ImageAiKit.FullPrompt("Haus", "aquarell"));
        Assert.Equal("Haus", ImageAiKit.FullPrompt(" Haus ", "ohne"));
        Assert.NotEqual(ImageAiKit.Seed(42, 0), ImageAiKit.Seed(42, 1));
        Assert.True(ImageAiKit.Seed(int.MinValue + 1, 3) >= 0);
        var args = ImageAiKit.Args("m.gguf", "Haus", "foto", "", "o.png", 512, 512, 7, xl: false);
        Assert.Equal(ImageAiKit.Negative, args[args.ToList().IndexOf("-n") + 1]);
        Assert.Equal("1", args[args.ToList().IndexOf("--steps") + 1]);
        var png = Path.GetTempFileName();
        try
        {
            File.WriteAllBytes(png, [1, 2, 3]);
            var f = ImageAiKit.WriteProvenance(png, new("stable-diffusion.cpp", "sd-turbo", "Haus", "foto", "", 7, 512, 512, DateTime.UtcNow, ""));
            var j = File.ReadAllText(f);
            Assert.Contains("\"AiGenerated\": true", j);
            Assert.Contains(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(png))).ToLowerInvariant(), j);
            File.Delete(f);
        }
        finally { File.Delete(png); }
    }

    [Fact(DisplayName = "Kreativ-Adapter Teil 2: ohne Nebenwirkung, Pflichtfelder, Prüfungen, Gliederung und Animation als Datei")]
    public void Adapter()
    {
        var all = PromptActions.CreateDefault();
        foreach (var id in new[] { "STUDIO-BOOK-01", "STUDIO-ANIM-01", "STUDIO-PHOTOS-01", "STUDIO-IMAGEAI-01" })
        {
            var ad = all.Single(a => a.StudioId == id);
            Assert.False(ad.CanPerformSideEffect);
            Assert.Equal(SideEffectClass.None, ad.Effect);
            Assert.NotEmpty(ad.Missing(new Dictionary<string, string>()));
            Assert.Equal(CapabilityLevel.Partial, CapabilityCatalog.Level(id, all));
        }
        Assert.NotEmpty(all.Single(a => a.StudioId == "STUDIO-BOOK-01").Validate(new Dictionary<string, string> { ["titel"] = "x", ["kapitel"] = "999" }));
        Assert.NotEmpty(all.Single(a => a.StudioId == "STUDIO-ANIM-01").Validate(new Dictionary<string, string> { ["text"] = "x", ["bewegung"] = "tanzen" }));
        Assert.NotEmpty(all.Single(a => a.StudioId == "STUDIO-PHOTOS-01").Validate(new Dictionary<string, string> { ["ordner"] = @"\\rechner\fotos", ["ziel"] = "relativ" }));
        Assert.NotEmpty(all.Single(a => a.StudioId == "STUDIO-IMAGEAI-01").Validate(new Dictionary<string, string> { ["bild"] = "porn" }));
    }
}
