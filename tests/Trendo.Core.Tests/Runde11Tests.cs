using Trendo.Core;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>Alle Studios, Runde 11: Artikel-Erkennung im Lesemodus (SYS-08) und Anweisungs-Test (CORE-12).</summary>
public class Runde11Tests
{
    static readonly Uri Base = new("https://example.org/artikel/");
    const string Satz = "Wildbienen bestäuben viele Pflanzen, sie brauchen offene Böden, trockene Stängel und Blüten über das ganze Jahr hinweg, damit ihre Brut satt wird. ";

    static string Seite(string artikel) => "<html><head><title>Bienen</title></head><body>"
        + "<header class='site-header'><a href='/a'>Start</a><a href='/b'>Themen</a></header>"
        + "<nav><ul><li><a href='/c'>Menüpunkt Eins</a></li><li><a href='/d'>Menüpunkt Zwei</a></li></ul></nav>"
        + "<div class='cookie-banner'>Wir verwenden Cookies, um dieses Angebot zu verbessern, bitte stimmen Sie zu oder lehnen Sie ab.</div>"
        + "<div id='layout'><div class='story-body'>" + artikel + "</div>"
        + "<div class='sidebar'><p>Anzeige: Kaufen Sie jetzt die besten Gartenmöbel, nur heute mit großem Rabatt für alle Kunden.</p></div></div>"
        + "<footer><p>Impressum, Datenschutz und alle Rechte vorbehalten bei der Beispiel GmbH in Musterstadt.</p></footer></body></html>";

    [Fact(DisplayName = "Lesemodus: Artikel ohne Menü, Cookie-Hinweis, Seitenleiste und Fußzeile; Links der ganzen Seite bleiben; ganze Seite weiter abrufbar")]
    public void Article_only()
    {
        var html = Seite($"<h2>Warum Bienen wichtig sind</h2><p>{Satz}</p><p>{Satz}</p><p>{Satz}</p>");
        var p = ResearchDesk.ToReader(html, Base, articleOnly: true);
        Assert.True(p.IsArticle);
        Assert.Contains("Wildbienen", p.Text); Assert.Contains("## Warum Bienen wichtig sind", p.Text);
        foreach (var weg in new[] { "Menüpunkt", "Cookies", "Gartenmöbel", "Impressum" }) Assert.DoesNotContain(weg, p.Text);
        Assert.Contains(p.Links, l => l.Text == "Menüpunkt Eins");
        var ganz = ResearchDesk.ToReader(html, Base);
        Assert.False(ganz.IsArticle); Assert.Contains("Impressum", ganz.Text); Assert.Contains("Wildbienen", ganz.Text);
    }

    [Fact(DisplayName = "Lesemodus: Link-Listen im Artikel fallen weg, Text mit einzelnen Links bleibt; über Geschwister verteilte Absätze bleiben zusammen")]
    public void Link_lists_and_siblings()
    {
        var mitLink = $"<p>{Satz}<a href='/q'>Quelle</a> {Satz}</p>";
        var liste = "<ul><li><a href='/1'>Mehr zum Thema Hummeln im Garten</a></li><li><a href='/2'>Mehr zum Thema Honig und Imkerei</a></li></ul>";
        var p = ResearchDesk.ToReader(Seite(mitLink + liste + mitLink), Base, articleOnly: true);
        Assert.True(p.IsArticle); Assert.Contains("Quelle", p.Text); Assert.DoesNotContain("Mehr zum Thema", p.Text);

        var html = $"<html><body><div id='a'><p>{Satz}{Satz}</p><p>{Satz}</p></div><div id='b'><p>Zweiter Teil. {Satz}{Satz}</p><p>{Satz}</p></div><div class='comments'><p>Mein Kommentar dazu, der ziemlich lang und ganz belanglos ist, wirklich.</p></div></body></html>";
        var t = ResearchDesk.ToReader(html, Base, articleOnly: true);
        Assert.True(t.IsArticle); Assert.Contains("Zweiter Teil", t.Text); Assert.DoesNotContain("Kommentar", t.Text);
    }

    [Theory(DisplayName = "Lesemodus: ohne erkennbaren Artikel die ganze Seite statt Bruchstück")]
    [InlineData("<title>Kurz</title><p>Inhalt</p><a href='/b'>B</a>")]
    [InlineData("<html><body><ul><li><a href='/1'>Eins Eins Eins Eins Eins Eins Eins Eins Eins</a></li><li><a href='/2'>Zwei Zwei Zwei Zwei Zwei Zwei Zwei Zwei</a></li></ul></body></html>")]
    [InlineData("")]
    public void Falls_back(string html)
    {
        var p = ResearchDesk.ToReader(html, Base, articleOnly: true);
        Assert.False(p.IsArticle);
        Assert.Equal(ResearchDesk.ToReader(html, Base).Text, p.Text);
    }

    [Theory(DisplayName = "Lesemodus: kaputtes HTML bleibt bei 2 MB schnell und stürzt nicht ab")]
    [InlineData("<a \"")]
    [InlineData("<div>")]
    [InlineData("</div>")]
    [InlineData("<p>x<")]
    [InlineData("<")]
    public void Hostile_html_is_linear(string unit)
    {
        var html = string.Concat(Enumerable.Repeat(unit, ResearchDesk.MaxBytes / unit.Length));
        var sw = System.Diagnostics.Stopwatch.StartNew();
        ResearchDesk.ExtractArticle(html);
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(5), sw.Elapsed.ToString());
    }

    [Fact(DisplayName = "Lesemodus: „>“ in Attributen und nicht geschlossene Absätze werden richtig gelesen")]
    public void Tolerant_parser()
    {
        var html = Seite($"<p title='a>b'>{Satz}<p>{Satz}<p>{Satz}");
        var p = ResearchDesk.ToReader(html, Base, articleOnly: true);
        Assert.True(p.IsArticle); Assert.DoesNotContain("Impressum", p.Text);
    }

    [Fact(DisplayName = "Anweisungs-Test: Regeln in Alltagssprache; Unverständliches und falsche Muster werden gemeldet")]
    public void Parse_rules()
    {
        var (c, e) = PromptEval.Parse("enthält: Preis\r\nnicht: leider\nhöchstens 5 Wörter\nMindestens 2 Woerter\nist JSON\nMuster: \\d+ Euro\n\nblabla\nMuster: (\nenthält:");
        Assert.Equal(["has", "not", "max", "min", "json", "regex"], c.Select(x => x.Kind));
        Assert.Equal(3, e.Count); Assert.Contains("blabla", e[0]); Assert.Contains("ungültig", e[1]);
    }

    [Theory(DisplayName = "Anweisungs-Test: jede Regel wird richtig ausgewertet")]
    [InlineData("enthält: preis", "Der Preis ist gut", true)]
    [InlineData("enthält: Preis", "Kostet nichts", false)]
    [InlineData("nicht: leider", "Leider nein", false)]
    [InlineData("nicht: leider", "Gern", true)]
    [InlineData("höchstens 3 Wörter", "eins  zwei\ndrei", true)]
    [InlineData("höchstens 2 Wörter", "eins zwei drei", false)]
    [InlineData("mindestens 2 Wörter", "eins", false)]
    [InlineData("ist JSON", " {\"a\":1} ", true)]
    [InlineData("ist JSON", "{a", false)]
    [InlineData("Muster: \\d+ Euro", "kostet 12 Euro", true)]
    [InlineData("Muster: ^(a+)+$", "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa!", false)]
    public void Evaluate_rules(string rule, string answer, bool expected)
    {
        var (c, e) = PromptEval.Parse(rule); Assert.Empty(e);
        Assert.Equal(expected, PromptEval.Evaluate(answer, c).Single().Ok);
    }

    [Fact(DisplayName = "Anweisungs-Test: zählt bestandene Durchläufe je Fassung; ausgefallene KI zählt als nicht bestanden; Durchläufe sind begrenzt")]
    public async Task Run_variants()
    {
        var (c, _) = PromptEval.Parse("enthält: gut");
        var calls = 0;
        Task<(bool, string)> Ask(string p, CancellationToken _)
        {
            calls++;
            return Task.FromResult(p == "neu" ? (true, "sehr gut") : calls % 2 == 0 ? (false, "KI aus") : (true, "schlecht"));
        }
        var r = await PromptEval.RunAsync([("Version 2", "neu"), ("Version 1", "alt")], c, 3, Ask);
        Assert.Equal(3, r[0].Passed); Assert.Equal(0, r[1].Passed);
        Assert.Contains(r[1].Runs, x => !x.Answered); Assert.Contains(r[1].Runs, x => x.Answered && !x.Ok);
        calls = 0; await PromptEval.RunAsync([("x", "neu")], c, 99, Ask); Assert.Equal(PromptEval.MaxRuns, calls);
    }

    [Fact(DisplayName = "Anweisungs-Test: Regeln bleiben je Vorlage gespeichert")]
    public void Checks_persist()
    {
        using var f = new Fixture();
        var a = new PromptLibrary(f.Root, f.Core.Audit); Assert.Equal("", a.ChecksFor("x"));
        a.SaveChecks("x", "enthält: A"); a.SaveChecks("y", "nicht: B");
        var b = new PromptLibrary(f.Root, f.Core.Audit);
        Assert.Equal("enthält: A", b.ChecksFor("x")); Assert.Equal("nicht: B", b.ChecksFor("y"));
    }
}
