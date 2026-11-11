using System.Xml.Linq;
using Trendo.Core.Kreativ;
using Trendo.Core.PromptAction;
using Xunit;

namespace Trendo.Core.Tests;

public class KreativKitsTests
{
    const string Text = "Der Gemeinderat hat den neuen Radweg beschlossen. Der Radweg verbindet den Bahnhof mit der Schule. " +
        "Die Kosten liegen bei einer Million Euro. Das Wetter war am Montag schön. Der Radweg soll im Herbst fertig sein. Viele Kinder fahren dann sicher zur Schule.";

    [Fact(DisplayName = "Zusammenfassen: Kernsätze in Originalreihenfolge, Länge, Stichwörter, Lesezeit")]
    public void Zusammenfassung()
    {
        var all = SummaryKit.Sentences(Text);
        Assert.Equal(6, all.Count);
        var s = SummaryKit.Summarize(Text, SummaryKit.CountFor("kurz"));
        Assert.Equal(3, s.Sentences.Count);
        Assert.Equal(s.Sentences.OrderBy(x => all.ToList().IndexOf(x)), s.Sentences); // Reihenfolge bleibt
        Assert.Contains("radweg", s.Keywords);
        Assert.DoesNotContain("der", s.Keywords);
        Assert.Equal(1, s.ReadMinutes);
        Assert.Equal(5, SummaryKit.CountFor("irgendwas"));
        Assert.Equal(8, SummaryKit.CountFor("lang"));
        Assert.StartsWith("# Radweg", SummaryKit.Markdown("Radweg", s));
        Assert.Empty(SummaryKit.Summarize("", 3).Sentences);
    }

    [Fact(DisplayName = "Logos: SVG für jede Form, Text maskiert, WCAG-Kontrast, lesbare Schriftfarbe")]
    public void Logo()
    {
        Assert.Equal(21, LogoKit.Contrast("#000000", "#FFFFFF"));
        Assert.Equal(1, LogoKit.Contrast("#777777", "#777777"));
        Assert.Equal("#FFFFFF", LogoKit.TextOn("#1E3A8A"));
        Assert.Equal("#111111", LogoKit.TextOn("#FDE68A"));
        Assert.Equal("EM", LogoKit.Initials("erika muster"));
        foreach (var (id, _) in LogoKit.Layouts)
        {
            var svg = LogoKit.Svg(new("A&B <Bau>", "Seit \"heute\"", "#1E3A8A", "#D4A017", id, "Georgia"));
            var x = XElement.Parse(svg); // gültiges XML trotz Sonderzeichen
            Assert.Contains("A&B <Bau>", x.Value);
        }
        Assert.Throws<ArgumentException>(() => LogoKit.Svg(new("", "", "#000000", "#FFFFFF", "kreis", "")));
        Assert.Throws<ArgumentException>(() => LogoKit.Svg(new("X", "", "blau", "#FFFFFF", "kreis", "")));
        Assert.Contains("4,5:1", LogoKit.BrandMarkdown(new("X", "", "#1E3A8A", "#D4A017", "kreis", "")));
    }

    [Fact(DisplayName = "Podcast: Kapitel lesen und prüfen, FFMETADATA mit Enden, Lautheit -16 LUFS, RSS nur mit https")]
    public void Podcast()
    {
        var (ch, problems) = PodcastKit.ParseChapters("02:15 Thema\n00:00 Start\nfalsch\n1:02:03 Fazit\n00:70 kaputt");
        Assert.Equal(["Start", "Thema", "Fazit"], ch.Select(c => c.Title));
        Assert.Equal(new TimeSpan(1, 2, 3), ch[2].Start);
        Assert.Equal(2, problems.Count);
        Assert.Contains("erste Kapitel", PodcastKit.ParseChapters("01:00 Ohne Anfang").Problems.Single());
        var meta = PodcastKit.FfMetadata("Folge = 1", ch, TimeSpan.FromMinutes(70));
        Assert.StartsWith(";FFMETADATA1", meta);
        Assert.Contains("title=Folge \\= 1", meta);
        Assert.Contains("START=135000\nEND=3723000", meta);
        Assert.Contains("END=4200000", meta);
        Assert.Contains("loudnorm=I=-16:TP=-1.5:LRA=11", PodcastKit.LoudnessArgs("a.wav", "b.mp3"));
        Assert.Contains("01:02:03", PodcastKit.ShowNotes("T", "D", ch).Replace("1:02:03", "01:02:03"));
        var ep = new PodcastKit.Episode("Folge 1", "Über & mehr", "folge 1.mp3", 1234, TimeSpan.FromSeconds(95), new DateTimeOffset(2026, 11, 11, 8, 0, 0, TimeSpan.Zero), "g1");
        var rss = PodcastKit.Rss("Mein Podcast", "Erika Muster", "Beschreibung", "https://example.org/pod", [ep]);
        Assert.StartsWith("<?xml", rss);
        var doc = XDocument.Parse(rss);
        XNamespace it = "http://www.itunes.com/dtds/podcast-1.0.dtd";
        Assert.Equal("https://example.org/pod/folge%201.mp3", (string?)doc.Descendants("enclosure").Single().Attribute("url"));
        Assert.Equal("95", doc.Descendants(it + "duration").Single().Value);
        Assert.Throws<ArgumentException>(() => PodcastKit.Rss("x", "y", "z", "http://example.org", [ep]));
    }

    [Fact(DisplayName = "Comics: Aufteilungen innerhalb der Seite, Geschichte in Bilder teilen, SVG gültig und maskiert")]
    public void Comic()
    {
        foreach (var (id, _) in ComicKit.Layouts)
            Assert.All(ComicKit.Boxes(id), b => Assert.True(b.X >= 0 && b.Y >= 0 && b.X + b.W <= ComicKit.PageW + 0.01 && b.Y + b.H <= ComicKit.PageH + 0.01 && b.W > 100 && b.H > 100));
        Assert.Equal(4, ComicKit.Boxes("2x2").Count); Assert.Equal(6, ComicKit.Boxes("6er").Count); Assert.Equal(3, ComicKit.Boxes("1+2").Count);
        Assert.Equal(["Er sucht.", "Sie findet."], ComicKit.SplitStory("Bild 1: Er sucht.\nBild 2: Sie findet."));
        Assert.Equal(2, ComicKit.SplitStory("Erster Absatz.\n\nZweiter Absatz.").Count);
        var svg = ComicKit.Svg("2x2", [new("Hallo <du> & ich, das ist eine längere Sprechblase mit vielen Wörtern", "Später …", "gibt-es-nicht.png")], "Titel & Co");
        var x = XElement.Parse(svg);
        Assert.Contains("Titel & Co", x.Value);
        Assert.DoesNotContain("<image", svg); // fehlende Datei wird übersprungen
    }

    [Fact(DisplayName = "Kreativ-Adapter: alle vier ohne Nebenwirkung, fragen nach Pflichtfeldern, prüfen Eingaben")]
    public void Adapter()
    {
        var all = PromptActions.CreateDefault();
        foreach (var id in new[] { "STUDIO-SUMMARY-01", "STUDIO-LOGO-01", "STUDIO-PODCAST-01", "STUDIO-COMIC-01" })
        {
            var ad = all.Single(a => a.StudioId == id);
            Assert.False(ad.CanPerformSideEffect);
            Assert.Equal(SideEffectClass.None, ad.Effect);
            Assert.NotEmpty(ad.Missing(new Dictionary<string, string>()));
            Assert.Equal(CapabilityLevel.Partial, CapabilityCatalog.Level(id, all));
        }
        Assert.NotEmpty(all.Single(a => a.StudioId == "STUDIO-SUMMARY-01").Validate(new Dictionary<string, string> { ["text"] = "Nur ein Satz" }));
        Assert.NotEmpty(all.Single(a => a.StudioId == "STUDIO-LOGO-01").Validate(new Dictionary<string, string> { ["name"] = "X", ["farbe"] = "blau" }));
        Assert.NotEmpty(all.Single(a => a.StudioId == "STUDIO-PODCAST-01").Validate(new Dictionary<string, string> { ["kapitel"] = "ohne Zeit" }));
    }
}
