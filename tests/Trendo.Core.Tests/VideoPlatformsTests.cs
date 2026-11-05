using Trendo.Core.Tv;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>STUDIO-TV-01 – Video-Plattformen (Wunsch 05.11.2026): YouTube und ähnliche Dienste legal als eigene Kategorie in Trendo TV.</summary>
public class VideoPlatformsTests
{
    const string PeerTube = """
        {"total":2,"data":[
          {"uuid":"11111111-2222-3333-4444-555555555555","name":"Gitarre lernen – Teil 1","description":"<p>Kurz &amp; gut</p>","duration":337,"views":8696,"publishedAt":"2024-07-14T19:55:42.155Z",
           "thumbnailUrl":"https://tube.example.org/lazy-static/thumbnails/a.jpg","url":"https://tube.example.org/videos/watch/11111111-2222-3333-4444-555555555555","isLive":false,
           "account":{"host":"tube.example.org","displayName":"erika"},"channel":{"displayName":"Erika Muster"}},
          {"uuid":"x","name":"Live","url":"https://tube.example.org/w/x","isLive":true,"account":{"host":"tube.example.org"}},
          {"uuid":"y","name":"ohne https","url":"http://tube.example.org/w/y","account":{"host":"tube.example.org"}}
        ]}
        """;

    [Fact(DisplayName = "PeerTube: Treffer lesen (Live und http übergangen), Wiedergabe über die eigene Instanz, beste Datei bis 1080p, sonst HLS")]
    public void Peertube()
    {
        var v = Assert.Single(VideoPlatforms.ParsePeerTube(PeerTube));
        Assert.Equal(("Gitarre lernen – Teil 1", "Erika Muster", "Kurz & gut", 8696L), (v.Title, v.Author, v.Description, v.Views));
        Assert.Equal(TimeSpan.FromSeconds(337), v.Duration);
        Assert.Equal("https://tube.example.org/api/v1/videos/11111111-2222-3333-4444-555555555555", v.ResolveUrl);
        var files = """{"files":[{"resolution":{"id":2160},"fileUrl":"https://tube.example.org/v-2160.mp4"},{"resolution":{"id":720},"fileUrl":"https://tube.example.org/v-720.mp4"},{"resolution":{"id":480},"fileUrl":"https://tube.example.org/v-480.mp4"}],"streamingPlaylists":[]}""";
        Assert.Equal("https://tube.example.org/v-720.mp4", VideoPlatforms.PeerTubePlayUrl(files));
        var hls = """{"files":[],"streamingPlaylists":[{"playlistUrl":"https://tube.example.org/hls/master.m3u8","files":[]}]}""";
        Assert.Equal("https://tube.example.org/hls/master.m3u8", VideoPlatforms.PeerTubePlayUrl(hls));
        Assert.Throws<InvalidOperationException>(() => VideoPlatforms.ParsePeerTube("kein json"));
        Assert.Contains("search=trending", VideoPlatforms.PeerTubeSearchUrl("")); Assert.Contains("nsfw=false", VideoPlatforms.PeerTubeSearchUrl("x"));
    }

    [Fact(DisplayName = "Dailymotion und Internet Archive: Treffer lesen; Archiv spielt die h.264-MP4, ohne abspielbare Datei null")]
    public void Dailymotion_and_archive()
    {
        var dm = VideoPlatforms.ParseDailymotion("""{"list":[{"id":"x1","title":"Kochen mit Erika","description":"","duration":178,"created_time":1421268216,"views_total":1500,"thumbnail_360_url":"https://s2.example/x360","url":"https://www.dailymotion.com/video/x1","owner.screenname":"Erika Muster"}]}""");
        Assert.Equal(("Kochen mit Erika", "Erika Muster", "https://www.dailymotion.com/video/x1"), (dm[0].Title, dm[0].Author, dm[0].PageUrl));
        Assert.Contains("family_filter=true", VideoPlatforms.DailymotionSearchUrl("x"));
        var ar = VideoPlatforms.ParseArchive("""{"response":{"docs":[{"identifier":"alter_film_1938","title":"Alter Film","date":"1938-01-01T00:00:00Z","downloads":3722203},{"identifier":"../böse","title":"nein"}]}}""");
        var a = Assert.Single(ar);
        Assert.Equal(("https://archive.org/details/alter_film_1938", "https://archive.org/metadata/alter_film_1938"), (a.PageUrl, a.ResolveUrl));
        Assert.Equal("3,7 Mio. Aufrufe", VideoPlatforms.ViewsText(a.Views));
        var meta = """{"metadata":{"identifier":"alter_film_1938"},"files":[{"name":"alter_film_1938.mpeg","format":"MPEG2"},{"name":"alter_film_1938_512kb.mp4","format":"512Kb MPEG4"},{"name":"alter_film_1938.mp4","format":"h.264"}]}""";
        Assert.Equal("https://archive.org/download/alter_film_1938/alter_film_1938.mp4", VideoPlatforms.ArchivePlayUrl(meta));
        Assert.Null(VideoPlatforms.ArchivePlayUrl("""{"metadata":{"identifier":"leer"},"files":[{"name":"leer.torrent","format":"Archive BitTorrent"}]}"""));
    }

    [Fact(DisplayName = "YouTube: Links erkennen (watch, youtu.be, shorts, embed), offizieller Einbett-Player datensparsam; Suche öffnet die offizielle Seite; andere Plattformen nicht in Trendo durchsuchbar")]
    public async Task Youtube()
    {
        Assert.Equal("dQw4w9WgXcQ", VideoPlatforms.YouTubeId("https://www.youtube.com/watch?v=dQw4w9WgXcQ&t=10"));
        Assert.Equal("dQw4w9WgXcQ", VideoPlatforms.YouTubeId("https://youtu.be/dQw4w9WgXcQ?si=abc"));
        Assert.Equal("dQw4w9WgXcQ", VideoPlatforms.YouTubeId("https://youtube.com/shorts/dQw4w9WgXcQ"));
        Assert.Null(VideoPlatforms.YouTubeId("https://example.org/watch?v=dQw4w9WgXcQ"));
        Assert.Null(VideoPlatforms.YouTubeId("Tatort"));
        Assert.Null(VideoPlatforms.YouTubeId("https://www.youtube.com/watch?v=<script>"));
        Assert.StartsWith("https://www.youtube-nocookie.com/embed/dQw4w9WgXcQ", VideoPlatforms.YouTubeEmbed("dQw4w9WgXcQ"));
        Assert.Equal("https://www.youtube.com/results?search_query=Terra%20X", VideoPlatforms.SearchPage("youtube", "Terra X"));
        Assert.StartsWith("https://www.twitch.tv/search?term=", VideoPlatforms.SearchPage("twitch", "musik"));
        Assert.Equal("musik", VideoPlatforms.CategoryQuery("musik"));
        Assert.All(VideoPlatforms.All, p => Assert.StartsWith("https://", p.Home));
        var asked = 0;
        await Assert.ThrowsAsync<InvalidOperationException>(() => VideoPlatforms.SearchAsync((_, _) => { asked++; return Task.FromResult(""); }, "youtube", "x"));
        Assert.Equal(0, asked); // YouTube wird nie abgefragt
        var got = await VideoPlatforms.SearchAsync((u, _) => Task.FromResult(u.Contains("sepiasearch") ? PeerTube : ""), "peertube", "", "musik");
        Assert.Single(got);
        Assert.Equal("https://tube.example.org/v-720.mp4", await VideoPlatforms.PlayUrlAsync((u, _) => Task.FromResult("""{"files":[{"resolution":{"id":720},"fileUrl":"https://tube.example.org/v-720.mp4"}]}"""), got[0]));
        Assert.Throws<ArgumentException>(() => VideoPlatforms.Get("kinox"));
    }
}
