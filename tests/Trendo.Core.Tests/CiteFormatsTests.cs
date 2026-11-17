using System.Text;
using System.Text.Json;
using Trendo.Core.Kreativ;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>T-0735 (STUDIO-CITE-01): RIS hin und zurück, RIS aus Zotero-Form, CSL-JSON, DOI über Crossref (Antwort hier als feste Probe;
/// echter Abruf: App-Test „Cite_Echt“ und reports/T-0735).</summary>
public class CiteFormatsTests
{
    static readonly CiteKit.Source[] Sample =
    [
        new() { Id = "a", Kind = "buch", Authors = "Muster, Erika; Beispiel, Max", Title = "Gärten für Einsteiger", Year = "2024", Publisher = "Grünverlag", Place = "Musterstadt", Edition = "2", Doi = "10.1234/garten.2024" },
        new() { Id = "b", Kind = "artikel", Authors = "Muster, Erika", Title = "Boden und Klima", Year = "2023", Journal = "Zeitschrift für Gartenbau", Volume = "12", Issue = "3", Pages = "45–67", Origin = "Crossref · https://api.crossref.org/works/x · abgerufen 17.11.2026 10:00 · SHA-256 abc…" },
        new() { Id = "c", Kind = "webseite", Authors = "{Gartenverband Musterstadt}", Title = "Wasser sparen im Garten", Year = "2025", Journal = "example.org", Url = "https://example.org/wasser-sparen", Accessed = "2026-11-03" },
    ];

    static object Key(CiteKit.Source s) => (s.Kind, s.Authors, s.Title, s.Year, s.Publisher, s.Place, s.Edition, s.Journal, s.Volume, s.Issue, s.Pages, s.Url, s.Doi, s.Accessed, s.Origin);

    [Fact(DisplayName = "RIS: Export und Wiedereinlesen ergibt dieselben Quellen (T-0735)")]
    public void Ris_rundreise()
    {
        var ris = CiteKit.Ris(Sample);
        Assert.Contains("TY  - BOOK\r\nAU  - Muster, Erika\r\nAU  - Beispiel, Max\r\n", ris);
        Assert.Contains("SP  - 45\r\nEP  - 67\r\n", ris);
        Assert.Contains("Y2  - 2026/11/03\r\n", ris);
        Assert.Equal(3, System.Text.RegularExpressions.Regex.Matches(ris, "ER  - ").Count);
        var (back, warn) = CiteKit.ParseRis(ris);
        Assert.Empty(warn);
        Assert.Equal(Sample.Select(Key), back.Select(Key));
    }

    [Fact(DisplayName = "RIS: Datei in Zotero-Form mit A1, JF, Y1, DA, Umbruch und unbekannten Feldern wird gelesen; kaputter Block gemeldet")]
    public void Ris_fremdform()
    {
        const string zotero = "﻿TY  - JOUR\nTI  - Deep learning\nAU  - LeCun, Yann\nAU  - Bengio, Yoshua\nAU  - Hinton, Geoffrey\nT2  - Nature\nAB  - Lange Zusammenfassung,\n  die umbricht\nDA  - 2015/05//\nPY  - 2015\nDO  - 10.1038/nature14539\nSP  - 436\nEP  - 444\nVL  - 521\nIS  - 7553\nSN  - 1476-4687\nUR  - https://www.nature.com/articles/nature14539\nKW  - KI\nER  - \n\n"
            + "AU  - Ohne, Anfang\nER  - \n\nTY  - BOOK\nA1  - {Statistisches Amt}\nT1  - Jahrbuch\nY1  - 2020///\nPB  - Amtverlag\nCY  - Musterstadt\n";
        var (s, warn) = CiteKit.ParseRis(zotero);
        Assert.Equal(2, s.Count);
        Assert.Equal("artikel", s[0].Kind); Assert.Equal("LeCun, Yann; Bengio, Yoshua; Hinton, Geoffrey", s[0].Authors);
        Assert.Equal("Nature", s[0].Journal); Assert.Equal("436–444", s[0].Pages); Assert.Equal("10.1038/nature14539", s[0].Doi); Assert.Equal("2015", s[0].Year);
        Assert.Equal("buch", s[1].Kind); Assert.Equal("{Statistisches Amt}", s[1].Authors); Assert.Equal("2020", s[1].Year); Assert.Equal("Musterstadt", s[1].Place);
        Assert.Contains(warn, w => w.Contains("außerhalb eines Eintrags"));
        Assert.Contains(warn, w => w.Contains("ohne „ER“"));
        Assert.Equal("LeCun, Y., Bengio, Y., & Hinton, G. (2015). Deep learning. *Nature*, *521*(7553), 436–444. https://doi.org/10.1038/nature14539", CiteKit.Format(s[0], "apa"));
    }

    [Fact(DisplayName = "CSL-JSON: Arten, Personen, Datum, Seiten, DOI und Abrufdatum nach CSL-Schema")]
    public void Csl_json()
    {
        using var d = JsonDocument.Parse(CiteKit.CslJson(Sample));
        var a = d.RootElement;
        Assert.Equal(3, a.GetArrayLength());
        Assert.Equal("book", a[0].GetProperty("type").GetString());
        Assert.Equal("Muster", a[0].GetProperty("author")[0].GetProperty("family").GetString());
        Assert.Equal("Erika", a[0].GetProperty("author")[0].GetProperty("given").GetString());
        Assert.Equal(2024, a[0].GetProperty("issued").GetProperty("date-parts")[0][0].GetInt32());
        Assert.Equal("10.1234/garten.2024", a[0].GetProperty("DOI").GetString());
        Assert.Equal("article-journal", a[1].GetProperty("type").GetString());
        Assert.Equal("45-67", a[1].GetProperty("page").GetString());
        Assert.Equal("Zeitschrift für Gartenbau", a[1].GetProperty("container-title").GetString());
        Assert.StartsWith("Herkunft: Crossref", a[1].GetProperty("note").GetString());
        Assert.Equal("webpage", a[2].GetProperty("type").GetString());
        Assert.Equal("Gartenverband Musterstadt", a[2].GetProperty("author")[0].GetProperty("literal").GetString());
        Assert.Equal([2026, 11, 3], a[2].GetProperty("accessed").GetProperty("date-parts")[0].EnumerateArray().Select(x => x.GetInt32()));
        Assert.Equal(3, a.EnumerateArray().Select(x => x.GetProperty("id").GetString()).Distinct().Count());
    }

    // Aufbau wie die echte Antwort von api.crossref.org/works/{doi} (gekürzt)
    const string Crossref = """
        {"status":"ok","message-type":"work","message":{"publisher":"Springer Science and Business Media LLC","issue":"7553","DOI":"10.1038/nature14539","type":"journal-article",
         "page":"436-444","title":["Deep learning"],"volume":"521","author":[{"given":"Yann","family":"LeCun","sequence":"first"},{"given":"Yoshua","family":"Bengio"},{"given":"Geoffrey","family":"Hinton"}],
         "container-title":["Nature"],"published-print":{"date-parts":[[2015,5,28]]},"issued":{"date-parts":[[2015,5,27]]},"URL":"https://doi.org/10.1038/nature14539"}}
        """;

    [Fact(DisplayName = "DOI: Crossref-Antwort füllt Autor, Titel, Jahr, Zeitschrift; Herkunft mit Adresse und SHA-256 (T-0735)")]
    public void Doi_aus_crossref()
    {
        var url = CiteKit.CrossrefUrl("https://doi.org/10.1038/NATURE14539");
        Assert.Equal("https://api.crossref.org/works/10.1038%2Fnature14539", url.AbsoluteUri);
        var bytes = Encoding.UTF8.GetBytes(Crossref);
        var s = CiteKit.FromCrossref(bytes, url, new DateTimeOffset(2026, 11, 17, 10, 5, 0, TimeSpan.Zero), "q1");
        Assert.Equal("artikel", s.Kind); Assert.Equal("LeCun, Yann; Bengio, Yoshua; Hinton, Geoffrey", s.Authors);
        Assert.Equal("Deep learning", s.Title); Assert.Equal("2015", s.Year); Assert.Equal("Nature", s.Journal);
        Assert.Equal("521", s.Volume); Assert.Equal("7553", s.Issue); Assert.Equal("436–444", s.Pages); Assert.Equal("10.1038/nature14539", s.Doi);
        var sha = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant();
        Assert.Equal($"Crossref · {url} · abgerufen 17.11.2026 10:05 · SHA-256 {sha[..16]}…", s.Origin);
        Assert.Empty(CiteKit.Missing(s, "apa"));
        Assert.Throws<ArgumentException>(() => CiteKit.CrossrefUrl("keine doi"));
        Assert.Throws<InvalidDataException>(() => CiteKit.FromCrossref(Encoding.UTF8.GetBytes("Resource not found."), url, DateTimeOffset.Now, "q2"));
        Assert.Throws<InvalidDataException>(() => CiteKit.FromCrossref(Encoding.UTF8.GetBytes("{\"message\":{\"type\":\"book\"}}"), url, DateTimeOffset.Now, "q3"));
        var book = CiteKit.FromCrossref(Encoding.UTF8.GetBytes("{\"message\":{\"type\":\"book\",\"title\":[\"Ein Buch\"],\"subtitle\":[\"Mit Untertitel\"],\"publisher\":\"Verlag\",\"publisher-location\":\"Musterstadt\",\"editor\":[{\"given\":\"Erika\",\"family\":\"Muster\"}],\"issued\":{\"date-parts\":[[2020]]}}}"), url, DateTimeOffset.Now, "q4");
        Assert.Equal(("buch", "Ein Buch: Mit Untertitel", "Verlag", "Musterstadt", "Muster, Erika"), (book.Kind, book.Title, book.Publisher, book.Place, book.Authors));
    }
}
