using Trendo.Core;
using Xunit;

namespace Trendo.Core.Tests;

/// T-0309: Recherche mit Markierungen – Stellen markieren, Abschnitte für die Anzeige, Zitierstile, Literaturliste, Bericht, flüchtige Sitzung.
public class ResearchMarksTests
{
    const string Text = "Tor ist ein Netzwerk zur Anonymisierung. Es leitet Verbindungen über mehrere Knoten.";

    [Fact(DisplayName = "T-0309: Markieren schneidet Leerzeichen ab, gleiche Stelle ersetzt Farbe/Notiz, falsche Auswahl abgewiesen, Abschnitte für die Anzeige")]
    public void Marks()
    {
        using var f = new Fixture(); var d = new ResearchDesk(f.Core.DataRoot, f.Core.Audit);
        var h = d.AddHighlight("https://de.example.org/tor", "Tor", Text, 3, 9, "gelb");
        Assert.Equal(("ist ein", 4, 7), (h.Text, h.Start, h.Length));
        var again = d.AddHighlight("https://de.example.org/tor", "Tor", Text, 4, 7, "grün", "wichtig");
        Assert.Equal(h.Id, again.Id); Assert.Single(d.Highlights()); Assert.Equal(("grün", "wichtig"), (d.Highlights()[0].Color, d.Highlights()[0].Note));
        Assert.Throws<ArgumentException>(() => d.AddHighlight("https://de.example.org/tor", "Tor", Text, 5, 0));
        Assert.Throws<ArgumentException>(() => d.AddHighlight("https://de.example.org/tor", "Tor", Text, 80, 50));
        Assert.Throws<ArgumentException>(() => d.AddHighlight("https://de.example.org/tor", "Tor", Text, 0, 3, "lila"));
        d.AddHighlight("https://de.example.org/tor", "Tor", Text, 41, 10, "blau"); // „Es leitet “ – Leerzeichen am Ende fällt weg
        var seg = ResearchDesk.Segments(Text, d.Highlights("https://de.example.org/tor"));
        Assert.Equal(Text, string.Concat(seg.Select(s => s.Text)));
        Assert.Equal(["ist ein", "Es leitet"], seg.Where(s => s.Mark is not null).Select(s => s.Text));
        d.SetHighlight(h.Id, color: "rot", note: "neu"); Assert.Equal(("rot", "neu"), (d.Highlights()[0].Color, d.Highlights()[0].Note));
        d.RemoveHighlight(h.Id); Assert.Single(d.Highlights());
    }

    [Fact(DisplayName = "T-0309: Zitierstile, Literaturliste ohne Doppelte, Zitat mit Quelle, Bericht mit Zitaten und Literatur; Sitzungsende löscht Markierungen")]
    public void Cite_and_report()
    {
        using var f = new Fixture(); var d = new ResearchDesk(f.Core.DataRoot, f.Core.Audit);
        var page = new ReaderPage("Tor (Netzwerk)", Text, []);
        var src = d.Record(new Uri("https://www.example.org/wiki/Tor"), page, Text); d.Record(new Uri("https://www.example.org/wiki/Tor"), page, Text);
        d.Record(new Uri("https://archiv.example.net/a"), new ReaderPage("Anonym im Netz", "x", []), "x");
        Assert.Equal($"Tor (Netzwerk). In: example.org. Online unter: https://www.example.org/wiki/Tor (Abruf: {src.At:dd.MM.yyyy}).", ResearchDesk.Cite(src));
        Assert.StartsWith("example.org. (o. J.). Tor (Netzwerk). Abgerufen am ", ResearchDesk.Cite(src, "apa"));
        Assert.Equal(2, d.Bibliography().Count); Assert.Equal("Anonym im Netz", d.Bibliography()[0].Split('.')[0]);
        var h = d.AddHighlight(src.Url, page.Title, Text, 0, 40, "gelb", "Kernaussage");
        Assert.Single(d.Bibliography(onlyMarked: true));
        Assert.StartsWith("„Tor ist ein Netzwerk zur Anonymisierung.“ (Tor (Netzwerk).", d.Quote(h)); Assert.EndsWith("Notiz: Kernaussage", d.Quote(h));
        var dir = Path.Combine(f.Core.DataRoot, "bericht"); var file = d.ExportReport(dir, "apa"); var md = File.ReadAllText(file);
        Assert.Contains("## Zitate und Markierungen", md); Assert.Contains("> „Tor ist ein Netzwerk zur Anonymisierung.“ (gelb)", md); Assert.Contains("Kernaussage", md);
        Assert.Contains("## Literaturliste", md); Assert.Contains("Abgerufen am", md);
        d.EndSession(); Assert.Empty(d.Highlights()); Assert.Empty(d.Bibliography());
    }
}
