using Trendo.Core.Tv;
using Xunit;
namespace Trendo.Core.Tests;

/// Wunsch 02.11.2026: in TV & Live alles Ausländische ausblenden – nur deutschsprachig; Erwachsene nach Wahl.
public class TvNurDeutsch
{
    [Theory(DisplayName = "TV-Filter „nur deutschsprachig“: DE/AT/CH und Einträge ohne Länderkennung bleiben, Ausländisches fällt weg, Erwachsene nach Einstellung")]
    [InlineData("DE • DRAMA", null, true, true, true)]
    [InlineData("Germany Sport", null, true, true, true)]
    [InlineData("AT | ORF", null, true, true, true)]
    [InlineData("CH: SRF", null, true, true, true)]
    [InlineData("Dokumentationen", "Terra X", true, true, true)]          // ohne Kennung bleibt
    [InlineData("TR • NETFLIX DIZILER", null, true, true, false)]
    [InlineData("UK | Sky Sports", null, true, true, false)]
    [InlineData("FR: Cinéma", null, true, true, false)]
    [InlineData("Sport", "UK: Sky Sports 1", true, true, false)]          // Kennung im Sendernamen
    [InlineData("XXX | Adult", null, true, true, true)]                   // Erwachsene bleiben, wenn erlaubt
    [InlineData("XXX | Adult", null, true, false, false)]
    [InlineData("DE: 18+ Erotik", null, false, false, false)]
    [InlineData("TR • NETFLIX DIZILER", null, false, true, true)]         // Filter aus: alles sichtbar
    public void Allowed(string group, string? name, bool onlyGerman, bool adult, bool expected) =>
        Assert.Equal(expected, TvCommands.Allowed(group, name, onlyGerman, adult));

    [Fact(DisplayName = "TV-Filter: Einstellung wird gespeichert und gilt für die Bibliothek")]
    public void Stored()
    {
        var dir = Path.Combine(Path.GetTempPath(), "trendo-tvfilter-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir);
        try
        {
            var lib = new TvLibrary(dir);
            Assert.False(lib.OnlyGerman); Assert.True(lib.ShowAdult);
            lib.SetOnlyGerman(true); lib.SetShowAdult(false);
            var again = new TvLibrary(dir);
            Assert.True(again.OnlyGerman); Assert.False(again.ShowAdult);
            Assert.False(again.Allowed("PL | Polsat")); Assert.True(again.Allowed("DE | ARD")); Assert.False(again.Allowed("Adult XXX"));
        }
        finally { Directory.Delete(dir, true); }
    }
}
