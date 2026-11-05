using System.Text.Json.Nodes;
using Trendo.Core.Tv;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>STUDIO-TV-01 – legale Quellen (05.11.2026): Mediatheken, kostenlos mit Werbung, Abo-Dienste; Mediathek-Suche lesen.</summary>
public class LegalSourcesTests
{
    [Fact(DisplayName = "STUDIO-TV-01 Legale Quellen: drei Arten, jede Kategorie hat Anbieter, nur https, keine doppelten Kennungen, keine Piraterie-Seiten")]
    public void Catalog()
    {
        Assert.Equal(3, LegalSources.All.Select(p => p.Kind).Distinct().Count());
        Assert.All(LegalSources.All, p => Assert.StartsWith("https://", p.Url));
        Assert.Equal(LegalSources.All.Count, LegalSources.All.Select(p => p.Id).Distinct().Count());
        foreach (var c in LegalSources.Categories) Assert.NotEmpty(LegalSources.All.Where(p => p.Categories.Contains(c)));
        Assert.All(LegalSources.Of(LegalSources.Kind.Mediathek), p => Assert.False(string.IsNullOrEmpty(p.Channel)));
        Assert.DoesNotContain(LegalSources.All, p => p.Url.Contains("kinox", StringComparison.OrdinalIgnoreCase) || p.Url.Contains("burning", StringComparison.OrdinalIgnoreCase) || p.Url.Contains("s.to", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(LegalSources.Of(LegalSources.Kind.Abo, "Sport"), p => p.Id == "dazn");
        Assert.DoesNotContain(LegalSources.Of(LegalSources.Kind.Abo, "Sport"), p => p.Id == "netflix");
    }

    [Fact(DisplayName = "STUDIO-TV-01 Mediathek-Suche: Suchtext vor Kategorie, Filme ab 60 Minuten, Kinder nur KiKA, Sender als zweite Bedingung")]
    public void Query()
    {
        var q = JsonNode.Parse(LegalSources.QueryJson("Tatort", "Filme", null))!;
        Assert.Equal("Tatort", (string?)q["queries"]![0]!["query"]); Assert.Null(q["duration_min"]); // eigener Suchtext: keine Mindestlänge
        var f = JsonNode.Parse(LegalSources.QueryJson("", "Filme", "ARD"))!;
        Assert.Equal("film", (string?)f["queries"]![0]!["query"]); Assert.Equal("ARD", (string?)f["queries"]![1]!["query"]); Assert.Equal(3600, (int)f["duration_min"]!);
        var k = JsonNode.Parse(LegalSources.QueryJson("", "Kinder", null))!;
        Assert.Contains(k["queries"]!.AsArray(), x => (string?)x!["query"] == "KiKA");
        Assert.False((bool)f["future"]!); Assert.Equal("desc", (string?)f["sortOrder"]);
    }

    [Fact(DisplayName = "STUDIO-TV-01 Mediathek-Antwort lesen: Titel, Sender, Dauer, Datum, HD-Adresse; ohne https übergangen; Fehler verständlich")]
    public void Parse()
    {
        const string json = """
        {"result":{"results":[
          {"channel":"ARD","topic":"Filme in der ARD","title":"Ein Film","description":"Beschreibung","timestamp":1793842454,"duration":5400,"url_website":"https://www.ardmediathek.de/x","url_video":"https://cdn.example/a.mp4","url_video_hd":"https://cdn.example/a_hd.mp4"},
          {"channel":"ZDF","topic":"Doku","title":"Ohne https","description":"","timestamp":1793842454,"duration":600,"url_website":"","url_video":"http://cdn.example/b.mp4","url_video_hd":""}
        ],"queryInfo":{"totalResults":2}},"err":null}
        """;
        var r = LegalSources.Parse(json);
        var v = Assert.Single(r);
        Assert.Equal(("ARD", "Ein Film"), (v.Channel, v.Title)); Assert.Equal(TimeSpan.FromMinutes(90), v.Duration);
        Assert.Equal("https://cdn.example/a_hd.mp4", v.UrlHd); Assert.Equal(2026, v.Date.Year);
        Assert.Equal("1 Std. 30 Min.", LegalSources.DurationText(v.Duration)); Assert.Equal("10 Min.", LegalSources.DurationText(TimeSpan.FromMinutes(10)));
        Assert.Throws<InvalidOperationException>(() => LegalSources.Parse("kein json"));
        Assert.Throws<InvalidOperationException>(() => LegalSources.Parse("""{"err":["kaputt"],"result":null}"""));
    }
}
