using System.Text;
using Avalonia.Headless.XUnit;
using Trendo.App;
using Trendo.Core;
using Trendo.Core.Tv;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>T-0240: TV-Zugangsdaten liegen im sicheren Speicher, nicht in den TV-Dateien – auch alte Einträge werden umgezogen.</summary>
public class TvCredentialsUiTests
{
    const string Old = "http://srv.example:8080/get.php?username=erika&password=Geheim%2B1&type=m3u_plus&output=ts";
    const string OldM3u = "#EXTM3U\n#EXTINF:-1 group-title=\"DE • SPORT\",Sport Eins\nhttp://srv.example:8080/erika/Geheim+1/11\n#EXTINF:-1 group-title=\"DE • NEWS\",Nachrichten\nhttp://srv.example:8080/live/erika/Geheim+1/12.ts\n";

    static void AllFilesClean(string root, params string[] secrets)
    {
        foreach (var f in Directory.GetFiles(Path.Combine(root, "tv"), "*", SearchOption.AllDirectories))
        {
            var t = File.ReadAllText(f);
            foreach (var s in secrets) Assert.False(t.Contains(s), $"{Path.GetFileName(f)} enthält das Passwort");
        }
    }

    [AvaloniaFact(DisplayName = "T-0240: alter Eintrag mit Passwort in der Adresse wird beim Start umgezogen – Sicherung, Speicher, Wiedergabe unverändert, Entfernen löscht das Passwort")]
    public async Task Migrates_old_entries()
    {
        // Ausgangslage wie bis 0.9.15: Passwort im Klartext in tv.json und in der Senderliste
        var root = Path.Combine(Path.GetTempPath(), "trendo-ui-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "tv"));
        File.WriteAllText(Path.Combine(root, "tv", "tv.json"),
            "{\"Sources\":[{\"Id\":\"a1b2c3d4\",\"Name\":\"Anbieter\",\"Kind\":\"url\",\"Url\":\"" + Old + "\",\"EpgUrl\":\"http://srv.example:8080/xmltv.php?username=erika&password=Geheim%2B1\"}]}");
        File.WriteAllText(Path.Combine(root, "tv", "a1b2c3d4.m3u"), OldM3u);
        var secrets = new InMemorySecretStore();
        var core = new TrendoCore(root, secrets, new FakeLauncher());
        var w = new MainWindow(core, new AppOptions(root, null, 0, Isolated: true), new Probe("browser"));
        w.Show();
        await w.TvMigration;
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.Equal("Geheim+1", secrets.Get(TvCredentials.Key("a1b2c3d4")));
        AllFilesClean(root, "password=Geheim", "Geheim%2B1", "/Geheim+1/");
        Assert.True(File.Exists(Path.Combine(root, "tv", "tv.json.vor-0240")));
        Assert.Contains(core.Audit.ReadAll(), e => e.Action == "tv.secrets.migrated" && !e.Detail.Contains("Geheim"));

        // Wiedergabe: dieselbe Adresse wie vorher
        var played = new List<string>();
        w.TvPlayOverride = (_, u) => played.Add(u);
        var ch = w.Tv.Channels().Single(c => c.Name == "Nachrichten");
        Assert.True(w.TvPlay(ch.Id));
        Assert.Equal("http://srv.example:8080/live/erika/Geheim%2B1/12.ts", played[^1]);
        Assert.Equal(Old, w.Tv.Reveal(w.Tv.Sources[0].Url));

        // Zweiter Start: nichts mehr zu tun
        Assert.Equal(0, w.Tv.MigrateSecrets());

        // Entfernen löscht auch das Passwort
        w.Tv.RemoveSource("a1b2c3d4");
        Assert.Null(secrets.Get(TvCredentials.Key("a1b2c3d4")));
        w.Close();
    }

    [AvaloniaFact(DisplayName = "T-0240: fehlt das Passwort im Speicher, gibt es eine klare Meldung – kein Absturz, kein Abruf mit leerem Passwort")]
    public async Task Missing_secret_is_explained()
    {
        var (w, _, _) = Ui.Open();
        var fetched = new List<string>();
        w.TvFetchOverride = u =>
        {
            fetched.Add(u);
            return Task.FromResult(Encoding.UTF8.GetBytes(
                u.Contains("player_api") && !u.Contains("action") ? """{"user_info":{"auth":1,"status":"Active"}}"""
                : u.Contains("get_live_categories") ? """[{"category_id":"9","category_name":"Deutschland"}]"""
                : u.Contains("get_live_streams") ? """[{"num":1,"name":"Eins","stream_id":1,"category_id":"9"}]""" : "[]"));
        };
        Assert.Equal(1, await w.TvAddSource("", "url", "http://srv.example:8080/get.php?username=u&password=geheim7&type=m3u_plus&output=ts"));
        var src = w.Tv.Sources[0];
        Assert.DoesNotContain("geheim7", src.Url);
        Assert.Equal("geheim7", w.Tv.PasswordOf(src.Id));

        // Passwort verschwindet aus dem Speicher (z. B. Windows-Anmeldeinformationen gelöscht)
        w.Core.Secrets.Delete(TvCredentials.Key(src.Id));
        var played = new List<string>();
        w.TvPlayOverride = (_, u) => played.Add(u);
        Assert.False(w.TvPlay(w.Tv.Channels()[0].Id));
        Assert.Empty(played);
        Assert.Contains("Passwort für diese Quelle fehlt", w.TvMessage);
        var before = fetched.Count;
        Assert.Equal(0, await w.TvRefreshSource(src));
        Assert.Equal(before, fetched.Count); // nichts mit Platzhalter oder leerem Passwort abgerufen
        Assert.Contains("Passwort für diese Quelle fehlt", w.TvMessage);
        w.Close();
    }
}
