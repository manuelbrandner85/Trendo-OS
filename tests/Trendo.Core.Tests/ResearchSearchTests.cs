using System.Text;
using Trendo.Core;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>T-0735 (SYS-08): Suche mit Trefferliste und Herkunft, Darknet-/Heimnetz-Treffer fallen weg, Sammlung wahlweise behalten.
/// Antworten hier als feste Proben; echter Abruf: App-Test „Sys08_Echt“ und reports/T-0735.</summary>
public class ResearchSearchTests
{
    [Fact(DisplayName = "SYS-08 Suche: Adressen der Suchdienste, Wikipedia-Antwort wird Trefferliste mit Herkunft")]
    public void Wikipedia_treffer()
    {
        var url = ResearchSearch.QueryUrl("wikipedia", "Phishing & Co");
        Assert.Equal("de.wikipedia.org", url.Host);
        Assert.Contains("srsearch=Phishing%20%26%20Co", url.AbsoluteUri);
        Assert.StartsWith("https://html.duckduckgo.com/html/", ResearchSearch.QueryUrl("duckduckgo", "x").AbsoluteUri);
        Assert.Throws<ArgumentException>(() => ResearchSearch.QueryUrl("wikipedia", "  "));
        Assert.Throws<ArgumentException>(() => ResearchSearch.QueryUrl("darknet", "x"));
        const string body = """{"batchcomplete":true,"query":{"searchinfo":{"totalhits":2},"search":[{"ns":0,"title":"Phishing","pageid":1,"snippet":"<span class=\"searchmatch\">Phishing</span> ist ein Betrug &amp; mehr"},{"ns":0,"title":"Tor (Netzwerk)","pageid":2,"snippet":"Anonymisierung"}]}}""";
        var at = new DateTimeOffset(2026, 11, 17, 9, 0, 0, TimeSpan.Zero);
        var hits = ResearchSearch.Parse("wikipedia", body, url, at);
        Assert.Equal(2, hits.Count);
        Assert.Equal("Phishing", hits[0].Title);
        Assert.Equal("https://de.wikipedia.org/wiki/Phishing", hits[0].Url);
        Assert.Equal("Phishing ist ein Betrug & mehr", hits[0].Snippet);
        Assert.Equal("Wikipedia (Deutsch)", hits[0].Engine); Assert.Equal(url.ToString(), hits[0].QueryUrl); Assert.Equal(at, hits[0].At);
        Assert.Equal("https://de.wikipedia.org/wiki/Tor_(Netzwerk)", hits[1].Url);
        Assert.Throws<InvalidDataException>(() => ResearchSearch.Parse("wikipedia", "{\"error\":{}}", url, at));
    }

    [Fact(DisplayName = "SYS-08 Suche: DuckDuckGo-HTML – Ziel entpackt, Werbung, .onion und Heimnetz fallen weg")]
    public void Duckduckgo_treffer()
    {
        var url = ResearchSearch.QueryUrl("duckduckgo", "phishing");
        const string html = """
            <div class="result results_links result--ad"><a rel="nofollow" class="result__a" href="https://duckduckgo.com/y.js?ad_domain=shop.example">Werbung</a><a class="result__snippet" href="#">Kaufen!</a></div>
            <div class="result"><h2><a rel="nofollow" class="result__a" href="//duckduckgo.com/l/?uddg=https%3A%2F%2Fwww.bsi.bund.de%2Fphishing&amp;rut=abc">BSI – <b>Phishing</b></a></h2><a class="result__snippet" href="x">So erkennst du <b>Phishing</b>.</a></div>
            <div class="result"><a rel="nofollow" class="result__a" href="//duckduckgo.com/l/?uddg=http%3A%2F%2Fabcdefghijklmnop.onion%2F&amp;rut=1">Versteckter Dienst</a><a class="result__snippet">Darknet</a></div>
            <div class="result"><a rel="nofollow" class="result__a" href="//duckduckgo.com/l/?uddg=http%3A%2F%2F192.168.0.1%2F&amp;rut=2">Router</a><a class="result__snippet">Heimnetz</a></div>
            <div class="result"><a rel="nofollow" class="result__a" href="https://www.verbraucherzentrale.de/phishingradar">Phishing-Radar</a><a class="result__snippet">Aktuelle Warnungen</a></div>
            """;
        var hits = ResearchSearch.Parse("duckduckgo", html, url, DateTimeOffset.Now);
        Assert.Equal(["https://www.bsi.bund.de/phishing", "https://www.verbraucherzentrale.de/phishingradar"], hits.Select(h => h.Url));
        Assert.Equal("BSI – Phishing", hits[0].Title); Assert.Equal("So erkennst du Phishing.", hits[0].Snippet);
        Assert.All(hits, h => Assert.Equal("DuckDuckGo (ganzes Netz)", h.Engine));
        Assert.Throws<InvalidDataException>(() => ResearchSearch.Parse("duckduckgo", "<html>anomaly-modal</html>", url, DateTimeOffset.Now));
    }

    [Fact(DisplayName = "SYS-08: Sammlung behalten übersteht den Neustart, ausgeschaltet beginnt jede Sitzung leer; Suchen stehen im Bericht")]
    public void Sammlung_behalten()
    {
        using var f = new Fixture();
        var d = new ResearchDesk(f.Root, f.Core.Audit);
        Assert.False(d.Keep);
        var page = new ReaderPage("Seite", "Text", []);
        d.Record(new Uri("https://example.com/a"), page, "roh"); d.SaveNote("notiz");
        var url = ResearchSearch.QueryUrl("wikipedia", "garten");
        var hits = ResearchSearch.Parse("wikipedia", """{"query":{"search":[{"title":"Garten","snippet":"x"}]}}""", url, DateTimeOffset.Now);
        var log = d.RecordSearch("wikipedia", "garten", url, Encoding.UTF8.GetBytes("roh"), hits);
        Assert.Equal(1, log.Hits); Assert.Equal(64, log.Sha256.Length);

        d.SetKeep(true); // bisherige Sammlung kommt mit
        Assert.True(d.Keep);
        var again = new ResearchDesk(f.Root, f.Core.Audit); again.StartSession(); // „Neustart“
        Assert.True(again.Keep);
        Assert.Single(again.SessionSources()); Assert.Equal("notiz", again.Note()); Assert.Single(again.Searches()); Assert.Equal("Garten", again.LastHits().Single().Title);
        var rep = File.ReadAllText(again.ExportReport(Path.Combine(f.Root, "out")));
        Assert.Contains("## Suchen", rep); Assert.Contains("„garten“ bei Wikipedia (Deutsch) · 1 Treffer", rep);

        again.SetKeep(false);
        var third = new ResearchDesk(f.Root, f.Core.Audit); third.StartSession();
        Assert.False(third.Keep); Assert.Empty(third.SessionSources()); Assert.Equal("", third.Note()); Assert.Empty(third.Searches()); Assert.Empty(third.LastHits());
        Assert.Contains(File.ReadAllLines(f.Core.Audit.FilePath), l => l.Contains("research.keep") && l.Contains("an"));
    }
}
