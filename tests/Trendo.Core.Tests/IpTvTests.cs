using System.Text;
using Trendo.Core.Tv;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>Studio „TV & Live“ (27.10.2026): Wiedergabelisten, Xtream, Programmführer, Bibliothek, Aufnahmen, Catch-up.</summary>
public class IpTvTests
{
    const string M3U = """
        #EXTM3U x-tvg-url="https://epg.example/guide.xml.gz"
        #EXTINF:-1 tvg-id="eins.de" tvg-name="Eins" tvg-logo="https://logo.example/1.png" group-title="Deutschland" tvg-chno="1" catchup="default" catchup-source="https://tv.example/eins/{utc}/{duration}.ts" catchup-days="7",Das Erste, HD
        https://tv.example/eins.m3u8
        #EXTINF:-1 tvg-id="zwei.de" group-title="Deutschland",Zwei
        https://tv.example/zwei.m3u8
        #EXTINF:-1,Ohne Angaben
        #EXTGRP:Musik
        http://radio.example/stream
        #EXTINF:-1,Kaputt
        keine-adresse
        """;

    const string XML = """
        <?xml version="1.0" encoding="UTF-8"?>
        <tv>
          <channel id="eins.de"><display-name>Das Erste</display-name></channel>
          <programme start="20261027180000 +0100" stop="20261027190000 +0100" channel="eins.de"><title lang="de">Nachrichten</title><desc>Aktuelles</desc><category>News</category></programme>
          <programme start="20261027190000 +0100" stop="20261027201500 +0100" channel="eins.de"><title>Krimi</title></programme>
          <programme start="20261027190000 +0100" stop="20261027200000 +0100" channel="andere.de"><title>Nicht gefragt</title></programme>
          <programme start="20251027190000 +0100" stop="20251027200000 +0100" channel="eins.de"><title>Zu alt</title></programme>
        </tv>
        """;

    [Fact(DisplayName = "TV: M3U Plus lesen – Name mit Komma, Gruppen, Nummern, Logo, EPG-Adresse aus dem Kopf, Catch-up; ungültige Zeilen übersprungen")]
    public void Parse_m3u()
    {
        var (ch, epg) = M3u.Parse(M3U, "q1");
        Assert.Equal("https://epg.example/guide.xml.gz", epg);
        Assert.Equal(3, ch.Count);
        Assert.Equal("Das Erste, HD", ch[0].Name);
        Assert.Equal(("Deutschland", "eins.de", 1, 7), (ch[0].Group, ch[0].EpgId!, ch[0].Number, ch[0].CatchupDays));
        Assert.Equal("Musik", ch[2].Group);
        Assert.Equal("q1:1", ch[1].Id);
        var again = M3u.Parse(M3u.Write(ch, epg), "q1").Channels;
        Assert.Equal(ch.Select(c => c.Url), again.Select(c => c.Url));
    }

    [Fact(DisplayName = "TV: Xtream-Adressen, XMLTV mit Zeitzone und Zeitfenster, Jetzt/Danach, Catch-up-Adresse")]
    public void Xtream_epg_catchup()
    {
        Assert.Equal("http://srv.example:8080/get.php?username=a%20b&password=p%26w&type=m3u_plus&output=ts", Xtream.PlaylistUrl("srv.example:8080/", "a b", "p&w"));
        Assert.StartsWith("http://srv.example:8080/xmltv.php?", Xtream.EpgUrl("srv.example:8080", "a", "b"));
        Assert.Equal(new DateTime(2026, 10, 27, 17, 0, 0, DateTimeKind.Utc), Xmltv.ParseTime("20261027180000 +0100"));
        var progs = Xmltv.Parse(new MemoryStream(Encoding.UTF8.GetBytes(XML)), new DateTime(2026, 10, 26), new DateTime(2026, 10, 29), new HashSet<string> { "eins.de" });
        Assert.Equal(["Nachrichten", "Krimi"], progs.Select(p => p.Title));
        Assert.Equal("Aktuelles", progs[0].Description); Assert.Equal("News", progs[0].Category);
        var (ch, _) = M3u.Parse(M3U, "q1");
        var (now, next) = TvLibrary.NowNext(progs, ch[0], new DateTime(2026, 10, 27, 17, 30, 0, DateTimeKind.Utc));
        Assert.Equal("Nachrichten", now!.Title); Assert.Equal("Krimi", next!.Title);
        Assert.Equal(0.5, now.Progress(new DateTime(2026, 10, 27, 17, 30, 0, DateTimeKind.Utc)), 3);
        var cu = Catchup.UrlFor(ch[0], progs[0])!;
        Assert.Equal($"https://tv.example/eins/{new DateTimeOffset(progs[0].Start).ToUnixTimeSeconds()}/3600.ts", cu);
        Assert.Null(Catchup.UrlFor(ch[1], progs[0])); // ohne Catch-up
    }

    [Fact(DisplayName = "TV: Filme und Serien erkennen (Adresse, Videodatei, Gruppe, S01E02) und aus Xtream-Antworten lesen")]
    public void Vod_and_series()
    {
        Assert.Equal("film", M3u.Classify("http://x/movie/u/p/1.mkv", "Film", "VOD | Action"));
        Assert.Equal("serie", M3u.Classify("http://x/series/u/p/9.mp4", "Krimi S01 E02", "Serien"));
        Assert.Equal("serie", M3u.Classify("http://x/a/Krimi.S02E05.mkv", "Krimi S02E05", "Unterhaltung"));
        Assert.Equal("film", M3u.Classify("http://x/a/film.mp4", "Ein Film", "Unterhaltung"));
        Assert.Equal("live", M3u.Classify("http://x/eins.m3u8", "Eins", "Deutschland"));
        Assert.Equal(("Mein Krimi", 2, 5), M3u.SplitEpisode("Mein Krimi S02 E05"));
        const string vod = """[{"stream_id":7,"name":"Abenteuer","category_id":"3","stream_icon":"http://img/7.jpg","rating":"7.5","container_extension":"mkv","added":"2024-01-02"}]""";
        const string cats = """[{"category_id":"3","category_name":"Abenteuer & Action"}]""";
        var v = Assert.Single(Xtream.ParseVod(vod, cats, "http://srv:8080", "u", "p", "q"));
        Assert.Equal(("Abenteuer & Action", "http://srv:8080/movie/u/p/7.mkv", "7.5"), (v.Category, v.Url, v.Rating!));
        const string ser = """[{"series_id":11,"name":"Die Serie","category_id":"5","cover":"http://img/11.jpg","plot":"Worum es geht","releaseDate":"2021-05-01"}]""";
        var s = Assert.Single(Xtream.ParseSeries(ser, """[{"category_id":"5","category_name":"Drama"}]""", "q"));
        Assert.Equal(("Drama", "2021", "11"), (s.Category, s.Year!, s.XtreamId!));
        const string info = """{"episodes":{"1":[{"id":"101","episode_num":1,"title":"Anfang","container_extension":"mp4","info":{"plot":"Los geht's","duration_secs":"2700"}}],"2":[{"id":"201","episode_num":1,"title":"Weiter"}]}}""";
        var eps = Xtream.ParseEpisodes(info, s.Id, "http://srv:8080", "u", "p");
        Assert.Equal([(1, 1), (2, 1)], eps.Select(e => (e.Season, e.Episode)));
        Assert.Equal(("http://srv:8080/series/u/p/101.mp4", 2700), (eps[0].Url, eps[0].Seconds));
        Assert.Equal(("http://srv:8080", "u", "p"), Xtream.FromPlaylistUrl(Xtream.PlaylistUrl("srv:8080", "u", "p"))!.Value);

        // Bibliothek: Filme/Serien aus einer gemischten Liste, Live-Sender getrennt
        var d = Path.Combine(Path.GetTempPath(), "trendo-tvv-" + Guid.NewGuid().ToString("N")[..8]); Directory.CreateDirectory(d);
        try
        {
            var lib = new TvLibrary(d);
            var src = lib.AddSource("Mix", "url", "https://tv.example/mix.m3u");
            lib.StorePlaylist(src.Id, """
                #EXTM3U
                #EXTINF:-1 group-title="News",Eins
                https://tv.example/eins.m3u8
                #EXTINF:-1 group-title="Filme",Ein Film
                https://tv.example/movie/u/p/1.mkv
                #EXTINF:-1 group-title="Serien",Krimi S01 E01
                https://tv.example/series/u/p/2.mkv
                #EXTINF:-1 group-title="Serien",Krimi S01 E02
                https://tv.example/series/u/p/3.mkv
                """);
            Assert.Equal(["Eins"], lib.Channels().Select(c => c.Name));
            Assert.Equal(["Ein Film"], lib.Vod().Select(x => x.Name));
            var krimi = Assert.Single(lib.Series());
            Assert.Equal("Krimi", krimi.Name);
            Assert.Equal([1, 2], lib.Episodes(krimi.Id).Select(e => e.Episode));
        }
        finally { Directory.Delete(d, true); }
    }

    [Fact(DisplayName = "TV: Bibliothek – Quellen, Sender, Favoriten, Zuletzt, Gruppen, Aufnahmen planen und fällig, Quelle entfernen")]
    public void Library()
    {
        var d = Path.Combine(Path.GetTempPath(), "trendo-tv-" + Guid.NewGuid().ToString("N")[..8]); Directory.CreateDirectory(d);
        try
        {
            var lib = new TvLibrary(d);
            var src = lib.AddSource("Mein Anbieter", "url", "https://tv.example/liste.m3u");
            lib.StorePlaylist(src.Id, M3U);
            var ch = lib.Channels();
            Assert.Equal(3, ch.Count);
            Assert.True(lib.ToggleFavorite(ch[1].Id)); Assert.Contains(ch[1].Id, lib.Favorites);
            lib.Watched(ch[0].Id); lib.Watched(ch[1].Id); Assert.Equal([ch[1].Id, ch[0].Id], lib.Recent); Assert.Equal(ch[1].Id, lib.LastChannel);
            lib.ToggleHiddenGroup("Musik"); lib.ToggleLockedGroup("Deutschland");
            Assert.Contains("Musik", lib.HiddenGroups); Assert.Contains("Deutschland", lib.LockedGroups);
            var t = new DateTime(2026, 10, 27, 18, 0, 0, DateTimeKind.Utc);
            Assert.Throws<ArgumentException>(() => lib.Plan(ch[0], "x", t, t));
            var r = lib.Plan(ch[0], "Krimi", t, t.AddHours(1));
            Assert.Empty(lib.Due(t.AddMinutes(-5)));
            Assert.Single(lib.Due(t.AddMinutes(5)));
            lib.UpdateRecording(r.Id, "läuft", "x.ts");
            Assert.Empty(lib.Due(t.AddMinutes(5)));
            Assert.EndsWith(".ts", TvLibrary.RecordingFile(d, r));
            lib.RemoveSource(src.Id);
            Assert.Empty(lib.Channels()); Assert.Empty(lib.Favorites);
        }
        finally { Directory.Delete(d, true); }
    }
}
