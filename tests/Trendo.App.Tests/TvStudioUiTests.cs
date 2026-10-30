using System.Text;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Trendo.App;
using Trendo.Core;
using Trendo.Core.Tv;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>Studio „TV & Live“ (27.10.2026): Quellen hinzufügen, Sender schauen, umschalten, Favoriten, gesperrte Gruppen, Programmführer,
/// Aufnehmen, Mehrfachansicht – ohne Netz und ohne mpv (nachgebildet).</summary>
public class TvStudioUiTests
{
    static void Pump() { for (var i = 0; i < 5; i++) Avalonia.Threading.Dispatcher.UIThread.RunJobs(); }

    static string Xml()
    {
        string T(DateTime t) => t.ToString("yyyyMMddHHmmss") + " +0000";
        var n = DateTime.UtcNow; var s = n.AddMinutes(-20); var e = n.AddMinutes(40);
        return $"""<tv><programme start="{T(s)}" stop="{T(e)}" channel="eins.de"><title>Jetzt-Sendung</title><desc>Läuft gerade</desc></programme><programme start="{T(e)}" stop="{T(e.AddHours(1))}" channel="eins.de"><title>Spätere Sendung</title></programme></tv>""";
    }

    const string M3U = "#EXTM3U\n#EXTINF:-1 tvg-id=\"eins.de\" group-title=\"Deutschland\",Eins\nhttps://tv.example/eins.m3u8\n#EXTINF:-1 group-title=\"Deutschland\",Zwei\nhttps://tv.example/zwei.m3u8\n#EXTINF:-1 group-title=\"Kinder\",Kinderkanal\nhttps://tv.example/kika.m3u8\n";

    [AvaloniaFact(DisplayName = "TV & Live: Quelle mit Programmführer laden, Sender schauen und umschalten, Favorit, gesperrte Gruppe nur mit PIN")]
    public async Task Watch_and_lock()
    {
        var (w, _, root) = Ui.Open();
        var played = new List<(string Slot, string Url)>();
        w.TvPlayOverride = (s, u) => played.Add((s, u));
        w.TvFetchOverride = u => Task.FromResult(Encoding.UTF8.GetBytes(u.Contains("epg") ? Xml() : M3U));
        w.Show("tv"); Pump();
        Assert.Equal("TV & Live", StudioCatalog.ById[MainWindow.TvStudio].Name);
        Assert.NotNull(w.TryFind<Button>("tv-start-sources"));
        w.Find<Button>("tv-start-sources").Press();
        Assert.False(w.Find<TextBox>("tv-src-url").IsReadOnly); // Adresse eintippen, nicht nur auswählen
        Assert.False(w.Find<TextBox>("tv-x-server").IsReadOnly);
        Assert.Equal(3, await w.TvAddSource("Mein Anbieter", "url", "https://tv.example/liste.m3u", "https://tv.example/epg.xml"));
        w.Show("tv"); w.Find<Button>("tv-tab-live").Press(); Pump();
        Assert.NotNull(w.TryFind<Button>("tv-ch-"));
        var first = w.Tv.Channels()[0];
        w.Find<Button>("tv-ch-" + first.Id.Replace(':', '-')).Press();
        Assert.Equal(("main", "https://tv.example/eins.m3u8"), played[^1]);
        Assert.Contains(w.GetLogicalTexts(), t => t.Contains("Jetzt: Jetzt-Sendung"));
        w.Find<Button>("tv-next").Press();
        Assert.Equal("https://tv.example/zwei.m3u8", played[^1].Url);
        w.Find<Button>("tv-fav").Press();
        Assert.Contains(w.TvCurrent!, w.Tv.Favorites);

        // Vollbild über ganz Trendo, Esc verkleinert wieder
        w.Find<Button>("tv-full").Press();
        Assert.True(w.TvIsFullscreen);
        Assert.Equal(Avalonia.Controls.WindowState.FullScreen, w.WindowState);
        Assert.NotNull(w.TryFind<Button>("tv-full-exit"));
        w.RaiseEvent(new Avalonia.Input.KeyEventArgs { RoutedEvent = Avalonia.Input.InputElement.KeyDownEvent, Key = Avalonia.Input.Key.Escape }); Pump();
        Assert.False(w.TvIsFullscreen);
        Assert.NotEqual(Avalonia.Controls.WindowState.FullScreen, w.WindowState);

        // Gruppe „Kinder“ sperren → nur mit PIN
        new ProfileLock(root).Set(ProfileLock.Kind.Pin, "4711");
        w.Tv.ToggleLockedGroup("Kinder");
        var kika = w.Tv.Channels().First(c => c.Group == "Kinder");
        Assert.False(w.TvPlay(kika.Id));
        w.Show("tv"); w.Find<Button>("tv-tab-quellen").Press();
        w.Find<TextBox>("tv-unlock-pin").Text = "4711"; w.Find<Button>("tv-unlock").Press();
        Assert.True(w.TvPlay(kika.Id));
        w.Close();
    }

    [AvaloniaFact(DisplayName = "TV & Live: Filme und Serien kategorisiert wählbar (Xtream) – Kategorie, Suche, Film abspielen, Serie → Staffel → Folge")]
    public async Task Films_and_series()
    {
        var (w, _, root) = Ui.Open();
        var played = new List<(string Slot, string Url)>();
        w.TvPlayOverride = (s, u) => played.Add((s, u));
        w.TvFetchOverride = u => Task.FromResult(Encoding.UTF8.GetBytes(
            u.Contains("player_api") && !u.Contains("action") ? """{"user_info":{"auth":1,"status":"Active"}}"""
            : u.Contains("get_live_categories") ? """[{"category_id":"9","category_name":"Deutschland"}]"""
            : u.Contains("get_live_streams") ? """[{"num":1,"name":"Eins","stream_id":1,"category_id":"9","epg_channel_id":"eins.de","tv_archive":1,"tv_archive_duration":3},{"num":2,"name":"Zwei","stream_id":2,"category_id":"9"},{"num":3,"name":"Drei","stream_id":3,"category_id":"9"}]"""
            : u.Contains("get_vod_categories") ? """[{"category_id":"1","category_name":"Action"},{"category_id":"2","category_name":"Familie"}]"""
            : u.Contains("get_vod_streams") ? """[{"stream_id":7,"name":"Großes Abenteuer","category_id":"1","container_extension":"mkv","rating":"8"},{"stream_id":8,"name":"Kleiner Bär","category_id":"2"}]"""
            : u.Contains("get_series_categories") ? """[{"category_id":"5","category_name":"Krimi"}]"""
            : u.Contains("get_series_info") ? """{"episodes":{"1":[{"id":"101","episode_num":1,"title":"Anfang","container_extension":"mp4"}],"2":[{"id":"201","episode_num":1,"title":"Neue Staffel"}]}}"""
            : u.Contains("get_series") ? """[{"series_id":11,"name":"Kommissarin","category_id":"5","plot":"Spannend"}]"""
            : u.Contains("xmltv") ? "<tv/>" : M3U));
        // eine eingefügte get.php-Adresse wird als Xtream-Zugang erkannt; Sender kommen über die Schnittstelle (mit Catch-up)
        var n = await w.TvAddSource("", "url", "http://srv.example:8080/get.php?username=u&password=p&type=m3u_plus&output=ts");
        w.Show("tv"); Pump();
        Assert.True(n == 3, string.Join(" | ", w.GetLogicalTexts().Where(t => t.Contains("✗") || t.Contains("✓") || t.Contains("laden"))));
        Assert.Equal("xtream", w.Tv.Sources[0].Kind);
        // T-0240: gespeichert wird ein Platzhalter, eingesetzt wird das Passwort erst beim Abspielen
        Assert.Equal("http://srv.example:8080/live/u/p/1.ts", w.Tv.Reveal(w.Tv.Channels()[0].Url));
        Assert.True(TvCredentials.HasToken(w.Tv.Channels()[0].Url));
        foreach (var f in Directory.GetFiles(Path.Combine(root, "tv"), "*", SearchOption.AllDirectories))
        {
            var t = File.ReadAllText(f);
            Assert.DoesNotContain("password=p", t);
            Assert.DoesNotContain("/u/p/", t);
        }
        Assert.Equal(3, w.Tv.Channels()[0].CatchupDays);
        w.Show("tv"); Pump();
        w.Find<Button>("tv-tab-film").Press();
        Assert.NotNull(w.TryFind<Button>("tv-film-chip-Action"));
        w.Find<Button>("tv-film-chip-Familie").Press();
        Assert.Contains(w.GetLogicalTexts(), t => t == "Kleiner Bär");
        Assert.DoesNotContain(w.GetLogicalTexts(), t => t == "Großes Abenteuer");
        w.Find<Button>("tv-film-chip-Alle").Press();
        w.Find<TextBox>("tv-film-search").Text = "Abenteuer"; Pump();
        w.TryFind<Button>("tv-film-item-")!.Press();
        Assert.Contains(w.GetLogicalTexts(), t => t == "Großes Abenteuer");
        w.Find<Button>("tv-film-play").Press();
        Assert.Equal("http://srv.example:8080/movie/u/p/7.mkv", played[^1].Url);
        Assert.Equal("live", w.CurrentTvTab);
        // Serien
        w.Find<Button>("tv-tab-serie").Press();
        w.TryFind<Button>("tv-serie-item-")!.Press();
        for (var i = 0; i < 50 && w.TryFind<Button>("tv-ep-1-1") is null; i++) { await Task.Delay(20); Pump(); }
        Assert.NotNull(w.TryFind<Button>("tv-season-2"));
        w.Find<Button>("tv-season-2").Press();
        w.Find<Button>("tv-ep-2-1").Press();
        Assert.Equal("http://srv.example:8080/series/u/p/201.mkv", played[^1].Url);
        w.Close();
    }

    [AvaloniaFact(DisplayName = "TV & Live: Xtream-Zugang abgelehnt → klare Meldung ohne Passwort; Server nicht erreichbar → verständlich")]
    public async Task Clear_errors()
    {
        var (w, _, root) = Ui.Open();
        w.TvFetchOverride = u => Task.FromResult(Encoding.UTF8.GetBytes("""{"user_info":{"auth":0}}"""));
        Assert.Equal(0, await w.TvAddSource("", "url", "http://srv.example:8080/get.php?username=u&password=geheim123&type=m3u_plus&output=ts"));
        w.Show("tv"); Pump();
        Assert.Contains(w.GetLogicalTexts(), t => t.Contains("Benutzername oder Passwort stimmt nicht"));
        Assert.DoesNotContain("geheim123", File.ReadAllText(Path.Combine(root, "tv", "tv.json"))); // T-0240: nie im Klartext
        Assert.DoesNotContain(w.GetLogicalTexts(), t => t.Contains("geheim123"));
        w.TvFetchOverride = u => Task.FromException<byte[]>(new HttpRequestException("x", new System.Net.Sockets.SocketException(11001)));
        Assert.Equal(0, await w.TvAddSource("Liste", "url", "http://gibt-es-nicht.example/liste.m3u"));
        w.Show("tv"); Pump();
        Assert.Contains(w.GetLogicalTexts(), t => t.Contains("Der Server ist nicht erreichbar"));
        Assert.Equal("http://s/live/u/•••/1.ts", Trendo.Core.Tv.Xtream.Mask("http://s/live/u/geheim/1.ts"));
        w.Close();
    }

    [AvaloniaFact(DisplayName = "TV & Live: Programmführer zeigt Sendungen, Aufnahme planen und starten, Mehrfachansicht, Quelle entfernen")]
    public async Task Guide_record_multi()
    {
        var (w, _, root) = Ui.Open();
        var played = new List<(string Slot, string Url)>(); var recorded = new List<string>();
        w.TvPlayOverride = (s, u) => played.Add((s, u));
        w.TvRecordOverride = (u, f, secs) => recorded.Add($"{u}|{secs > 0}");
        w.TvFetchOverride = u => Task.FromResult(Encoding.UTF8.GetBytes(u.Contains("epg") ? Xml() : M3U));
        var file = Path.Combine(root, "liste.m3u"); File.WriteAllText(file, M3U);
        w.PickOverride = _ => Task.FromResult<string[]?>([file]);
        w.Show("tv"); Pump();
        w.Find<Button>("tv-start-sources").Press();
        w.Find<Button>("tv-src-file").Press();
        for (var i = 0; i < 50 && w.Tv.Sources.Count == 0; i++) { await Task.Delay(20); Pump(); }
        Assert.Single(w.Tv.Sources);
        Assert.Equal("datei", w.Tv.Sources[0].Kind);
        // Programmführer nachladen (EPG-Adresse setzen)
        w.Tv.SetEpgUrl(w.Tv.Sources[0].Id, "https://tv.example/epg.xml");
        await w.TvRefreshEpg(w.Tv.Sources[0]);
        w.Show("tv"); w.Find<Button>("tv-tab-guide").Press();
        Assert.Contains(w.GetLogicalTexts(), t => t == "Jetzt-Sendung");
        var later = w.TryFind<Button>("tv-prog-" + w.Tv.Channels()[0].Id.Replace(':', '-') + "-" + w.Tv.Programmes()[1].Start.ToString("HHmm"))!;
        later.Press();
        w.Find<Button>("tv-prog-rec").Press();
        Assert.Single(w.Tv.Recordings);
        // „Jetzt aufnehmen“ im Live-Bild startet sofort
        w.Find<Button>("tv-tab-live").Press();
        w.TvPlay(w.Tv.Channels()[0].Id); w.Show("tv");
        w.Find<Button>("tv-rec-now").Press();
        Assert.Contains("https://tv.example/eins.m3u8|True", recorded);
        Assert.Contains(w.Tv.Recordings, r => r.State == "läuft");
        // Mehrfachansicht
        w.Find<Button>("tv-tab-multi").Press();
        w.Find<ComboBox>("tv-multi-pick-1").SelectedIndex = 1; Pump();
        Assert.Contains(("m1", "https://tv.example/zwei.m3u8"), played);
        w.Find<Button>("tv-multi-sound-1").Press();
        // Quelle entfernen (mit Rückfrage)
        w.Find<Button>("tv-tab-quellen").Press();
        var rm = w.TryFind<Button>("tv-src-remove-")!; var name = rm.Name!;
        rm.Press(); w.Find<Button>(name).Press();
        Assert.Empty(w.Tv.Sources);
        w.Close();
    }

    const string Mixed = "#EXTM3U\n#EXTINF:-1 group-title=\"DE • SPORT\",Sport Eins\nhttps://tv.example/live/1.ts\n#EXTINF:-1 group-title=\"TR • HABER\",Haber\nhttps://tv.example/live/2.ts\n"
        + "#EXTINF:-1 group-title=\"DE • NETFLIX FILME\",Deutscher Film\nhttps://tv.example/movie/u/p/1.mkv\n#EXTINF:-1 group-title=\"TR • DISNEY PLUS FILMLER\",Türk Film\nhttps://tv.example/movie/u/p/2.mkv\n"
        + "#EXTINF:-1 group-title=\"DE • NETFLIX SERIEN\",Kommissarin S01 E01\nhttps://tv.example/series/u/p/1.mkv\n#EXTINF:-1 group-title=\"DE • AMAZON PRIME SERIEN\",Hafenkante S01 E01\nhttps://tv.example/series/u/p/2.mkv\n";

    [AvaloniaFact(DisplayName = "TV & Live: KI-Assistent („Behalte nur deutsche Sender“, „Zeig Netflix-Serien“) und Anbieter-/Sprachwahl bei Filmen und Serien")]
    public async Task Assistant_and_providers()
    {
        var (w, _, _) = Ui.Open();
        w.TvFetchOverride = _ => Task.FromResult(Encoding.UTF8.GetBytes(Mixed));
        Assert.Equal(6, await w.TvAddSource("Liste", "url", "https://tv.example/liste.m3u"));
        w.Show("tv"); Pump();
        Assert.NotNull(w.TryFind<TextBox>("tv-ki"));
        var msg = await w.TvDo("Blende mir alle Sender aus und behalte nur deutsche"); Pump();
        Assert.StartsWith("✓", msg);
        Assert.Contains("TR • HABER", w.Tv.HiddenGroups);
        Assert.DoesNotContain("DE • SPORT", w.Tv.HiddenGroups);
        Assert.DoesNotContain("TR • DISNEY PLUS FILMLER", w.Tv.HiddenGroups); // nur Sender gemeint

        Assert.StartsWith("✓", await w.TvDo("Zeig Netflix-Serien")); Pump();
        Assert.Equal("serie", w.CurrentTvTab);
        Assert.NotNull(w.TryFind<Button>("tv-serie-prov-Netflix"));
        Assert.Contains(w.GetLogicalTexts(), t => t == "Kommissarin");
        Assert.DoesNotContain(w.GetLogicalTexts(), t => t == "Hafenkante");
        w.Find<Button>("tv-serie-prov-Amazon-Prime").Press(); Pump();
        Assert.Contains(w.GetLogicalTexts(), t => t == "Hafenkante");

        Assert.StartsWith("✓", await w.TvDo("Behalte nur deutsche Filme")); Pump();
        Assert.Equal("film", w.CurrentTvTab);
        Assert.Contains("TR • DISNEY PLUS FILMLER", w.Tv.HiddenGroups);
        Assert.Contains(w.GetLogicalTexts(), t => t == "Deutscher Film");
        Assert.DoesNotContain(w.GetLogicalTexts(), t => t == "Türk Film");
        Assert.NotNull(w.TryFind<Button>("tv-film-lang-de") ?? w.TryFind<Button>("tv-film-prov-Netflix"));

        Assert.StartsWith("✓", await w.TvDo("Zeig wieder alles"));
        Assert.Empty(w.Tv.HiddenGroups);
    }
}
