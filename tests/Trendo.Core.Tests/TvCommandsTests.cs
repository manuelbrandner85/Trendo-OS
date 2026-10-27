using System.Diagnostics;
using System.Text;
using Trendo.Core.Tv;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>TV-Assistent (27.10.2026): Wünsche in Aktionen übersetzen, Anbieter und Sprache erkennen, große Listen ohne Hänger.</summary>
public class TvCommandsTests
{
    static TvCommands.Action One(string text) => Assert.Single(TvCommands.Rules(text));

    [Fact(DisplayName = "TV-Assistent: feste Regeln für Sender, Filme und Serien")]
    public void Rules()
    {
        Assert.Equal(new TvCommands.Action("only-country", "de", "live"), One("Blende mir alle Sender aus und behalte nur deutsche"));
        Assert.Equal(new TvCommands.Action("only-country", "de", "film"), One("Behalte nur deutsche Filme"));
        Assert.Equal(new TvCommands.Action("only-country", "tr", "serie"), One("nur türkische Serien"));
        Assert.Equal(new TvCommands.Action("only-country", "de", ""), One("Behalte nur deutsche"));
        Assert.Equal("show-all", One("Zeig wieder alles").Kind);
        Assert.Equal(new TvCommands.Action("provider", "Netflix", "serie"), One("Zeig Netflix-Serien"));
        Assert.Equal(new TvCommands.Action("provider", "Disney+", "film"), One("Disney+ Filme"));
        Assert.Equal(new TvCommands.Action("provider", "Amazon Prime", "serie"), One("was gibt es bei amazon prime"));
        var two = TvCommands.Rules("zeig deutsche netflix serien");
        Assert.Equal(["provider", "lang"], two.Select(a => a.Kind));
        Assert.Equal("play", One("Spiel ZDF").Kind); // Sender, nicht Anbieter
        Assert.Equal("play", One("Schalt auf Sky Sport").Kind);
        Assert.Equal(new TvCommands.Action("films", "", "action"), One("Zeig Action-Filme"));
        Assert.Equal("series", One("Zeig Krimi-Serien").Kind);
        Assert.Equal("lock-groups", One("Sperre Erwachsenen-Gruppen").Kind);
        Assert.Equal("guide", One("Was läuft gerade?").Kind);
        Assert.Empty(TvCommands.Rules("Wie wird das Wetter?"));
    }

    [Fact(DisplayName = "TV-Assistent: Anbieter und Sprache aus Kategorien, KI-Antwort nur mit erlaubten Aktionen")]
    public void Providers_and_languages()
    {
        Assert.True(TvCommands.HasProvider("DE • NETFLIX SERIEN", "Netflix"));
        Assert.True(TvCommands.HasProvider("TR • DISNEY PLUS  DIZILER", "Disney+"));
        Assert.True(TvCommands.HasProvider("DE • AMAZON PRIME SERIEN", "Amazon Prime"));
        Assert.True(TvCommands.HasProvider("DE • APPLETV + SERIEN", "Apple TV+"));
        Assert.False(TvCommands.HasProvider("DE • DRAMA", "Netflix"));
        Assert.Equal(["Netflix", "Amazon Prime", "Disney+"], TvCommands.ProvidersIn(["EU • MULTI  AMAZON • DISNEY • NETFLIX SERIES", "DE • KRIMI"]));
        Assert.Equal("de", TvCommands.CountryOf("DE • DRAMA"));
        Assert.Equal("tr", TvCommands.CountryOf("TR • NETFLIX DIZILER"));
        Assert.Equal("eu", TvCommands.CountryOf("EU • MULTI  AMAZON"));
        Assert.Equal("de", TvCommands.CountryOf("Germany Sport"));
        Assert.Null(TvCommands.CountryOf("XXX • FOR ADULTS"));
        var ki = TvCommands.ParseKi("""Klar: [{"kind":"provider","arg":"Netflix","extra":"serie"},{"kind":"format-c","arg":"x"}]""");
        Assert.Equal(new TvCommands.Action("provider", "Netflix", "serie"), Assert.Single(ki));
    }

    [Fact(DisplayName = "TV & Live: Liste mit 120 000 Einträgen wird einmal gelesen, Serien in einem Durchgang (vorher „Keine Rückmeldung“)")]
    public void Big_playlist()
    {
        var root = Path.Combine(Path.GetTempPath(), "trendo-tvbig-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            var lib = new TvLibrary(root);
            var src = lib.AddSource("Groß", "url", "https://tv.example/liste.m3u");
            var sb = new StringBuilder("#EXTM3U\n");
            for (var i = 0; i < 20_000; i++) sb.Append($"#EXTINF:-1 group-title=\"DE • SPORT\",Sender {i}\nhttps://tv.example/live/{i}.ts\n");
            for (var i = 0; i < 40_000; i++) sb.Append($"#EXTINF:-1 group-title=\"DE • NETFLIX FILME\",Film {i}\nhttps://tv.example/movie/u/p/{i}.mkv\n");
            for (var s = 0; s < 3_000; s++) for (var e = 1; e <= 20; e++) sb.Append($"#EXTINF:-1 group-title=\"TR • DISNEY PLUS DIZILER\",Serie {s} S01 E{e:00}\nhttps://tv.example/series/u/p/{s}-{e}.mkv\n");
            lib.StorePlaylist(src.Id, sb.ToString());
            var sw = Stopwatch.StartNew();
            Assert.Equal(20_000, lib.Channels().Count);
            Assert.Equal(40_000, lib.Vod().Count);
            Assert.Equal(3_000, lib.Series().Count);
            Assert.Equal(20, lib.Episodes($"{src.Id}:s-serie 42").Count);
            for (var i = 0; i < 50; i++) _ = lib.Favorites; // Zustand aus dem Zwischenspeicher
            Assert.True(sw.Elapsed < TimeSpan.FromSeconds(20), $"zu langsam: {sw.Elapsed}");
            sw.Restart();
            _ = lib.Channels(); _ = lib.Series();
            Assert.True(sw.Elapsed < TimeSpan.FromSeconds(2), $"zweites Lesen zu langsam: {sw.Elapsed}");
            lib.ToggleFavorite($"{src.Id}:1");
            Assert.Contains($"{src.Id}:1", lib.Favorites); // Zwischenspeicher sieht Änderungen sofort
        }
        finally { try { Directory.Delete(root, true); } catch (IOException) { } }
    }
}
