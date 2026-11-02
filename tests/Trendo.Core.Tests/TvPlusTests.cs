using Trendo.Core.Tv;
using Xunit;
namespace Trendo.Core.Tests;

/// TV & Live auf Highend (02.11.2026): Weiterschauen, nächste Folge, Kinder-Profil, doppelte Sender, Sprache, Startseite, Aufnahme-Regeln, Film-Infos, Assistent.
public class TvPlusKern
{
    static string Tmp() { var d = Path.Combine(Path.GetTempPath(), "trendo-tvplus-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(d); return d; }

    [Fact(DisplayName = "TV: Weiterschauen merkt die Stelle (nur sinnvolle), neueste zuerst, am Ende fällt der Eintrag weg")]
    public void Resume()
    {
        var dir = Tmp();
        try
        {
            var lib = new TvLibrary(dir);
            lib.SaveResume(new TvResume(TvPlus.ResumeKey("u1"), "Film A", "u1", 600, 6000, DateTime.UtcNow));
            lib.SaveResume(new TvResume(TvPlus.ResumeKey("u2"), "Film B", "u2", 10, 6000, DateTime.UtcNow)); // zu früh – nicht merken
            lib.SaveResume(new TvResume(TvPlus.ResumeKey("u3"), "Film C", "u3", 1200, 6000, DateTime.UtcNow));
            Assert.Equal(["Film C", "Film A"], new TvLibrary(dir).Resume.Select(r => r.Title));
            Assert.Equal(600, lib.ResumeFor("u1")!.Pos);
            lib.SaveResume(new TvResume(TvPlus.ResumeKey("u1"), "Film A", "u1", 5900, 6000, DateTime.UtcNow)); // fast am Ende → raus
            Assert.Null(lib.ResumeFor("u1"));
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact(DisplayName = "TV: nächste Folge (auch über die Staffel hinweg) und die Momente für Intro/Abspann/Countdown")]
    public void Next_episode()
    {
        TvEpisode E(int s, int e) => new("ser", s, e, $"S{s}E{e}", $"u{s}{e}");
        var eps = new[] { E(1, 1), E(1, 2), E(2, 1) };
        Assert.Equal((1, 2), TvPlus.NextEpisode(eps, 1, 1) is { } n ? (n.Season, n.Episode) : default);
        Assert.Equal((2, 1), TvPlus.NextEpisode(eps, 1, 2) is { } m ? (m.Season, m.Episode) : default);
        Assert.Null(TvPlus.NextEpisode(eps, 2, 1));
        Assert.Equal((true, false, false), TvPlus.EpisodeMoments(30, 2700));
        Assert.Equal((false, true, false), TvPlus.EpisodeMoments(2650, 2700));
        Assert.Equal((false, false, true), TvPlus.EpisodeMoments(2695, 2700));
    }

    [Fact(DisplayName = "TV: Kinder-Profil zeigt nur Kinderinhalte (nie Erwachsene) und wird gespeichert")]
    public void Kids()
    {
        Assert.True(TvPlus.IsKids("DE | Kinder")); Assert.True(TvPlus.IsKids("Sender", "KiKA HD")); Assert.True(TvPlus.IsKids("DE • ANIMATION"));
        Assert.False(TvPlus.IsKids("DE | Sport")); Assert.False(TvPlus.IsKids("XXX Kids"));
        var dir = Tmp();
        try
        {
            var lib = new TvLibrary(dir); lib.SetKidsMode(true);
            Assert.True(new TvLibrary(dir).KidsMode);
            Assert.True(lib.Allowed("DE | Kinder")); Assert.False(lib.Allowed("DE | Nachrichten")); Assert.False(lib.Allowed("Adult"));
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact(DisplayName = "TV: doppelte Sender (HD/FHD/SD) werden einer – die beste Qualität läuft, die anderen bleiben wählbar")]
    public void Duplicates()
    {
        TvChannel C(string id, string name, int n) => new(id, name, "u" + id, "DE", null, null, n);
        var list = new[] { C("1", "DE: ZDF HD", 1), C("2", "ZDF FHD", 2), C("3", "ZDF SD", 3), C("4", "ARD", 4) };
        var (best, alt) = TvPlus.MergeDuplicates(list);
        Assert.Equal(["ZDF FHD", "ARD"], best.Select(c => c.Name));
        Assert.Equal(["ZDF FHD", "DE: ZDF HD", "ZDF SD"], alt["2"].Select(c => c.Name));
        Assert.Equal(4, TvPlus.QualityRank("Sky 4K")); Assert.Equal(1, TvPlus.QualityRank("RTL SD"));
    }

    [Fact(DisplayName = "TV: Sprache für Ton und Untertitel (Untertitel nur bei fremdem Ton), Startseite mit Reihen, Aufnahme-Regeln, Film-Infos")]
    public void Prefs_rows_rules_info()
    {
        Assert.Contains("--alang=de,deu,ger", TvPlus.LanguageArgs("de")); Assert.Contains("--subs-with-matching-audio=no", TvPlus.LanguageArgs("de"));
        TvVod V(string id, string cat, string? rating, string? cover = "c") => new(id, "Film " + id, cat, "u" + id, cover, null, null, rating);
        var all = new[] { V("1", "Krimi", "7.1"), V("2", "Krimi", "8.4"), V("3", "Komödie", null), V("4", "Krimi", "6.0", null) };
        var rows = TvPlus.Rows(all, [new TvResume("k", "Film 1", "u1", 100, 6000, DateTime.UtcNow)]);
        Assert.Equal("Film 2", rows.Single(r => r.Id == "beliebt").Items[0].Name);
        Assert.Contains(rows, r => r.Id == "weil" && r.Items.All(v => v.Category == "Krimi" && v.Url != "u1"));
        Assert.Equal("Film 2", TvPlus.Hero(all)!.Name);
        var rule = new TvRecordRule("r", "FC Bayern");
        Assert.True(TvPlus.Matches(rule, "ch", "Bundesliga: FC Bayern – Dortmund")); Assert.False(TvPlus.Matches(rule, "ch", "Tagesschau"));
        Assert.False(TvPlus.Matches(rule with { ChannelId = "anderer" }, "ch", "FC Bayern"));
        var info = TvPlus.ParseVodInfo("""{"info":{"plot":"Spannend","rating":"7.5","director":"Erika Muster","cast":"A, B","youtube_trailer":"dQw4w9WgXcQ","genre":"Krimi"}}""")!;
        Assert.Equal("https://www.youtube.com/watch?v=dQw4w9WgXcQ", info.TrailerUrl); Assert.Equal("Erika Muster", info.Director);
        Assert.Null(TvPlus.ParseVodInfo("""{"info":{"youtube_trailer":"javascript:alert(1)"}}""")!.TrailerUrl); // nur echte YouTube-Adressen
        Assert.Equal("7", TvPlus.StreamIdOf("http://srv.example:8080/movie/u/p/7.mkv"));
    }

    [Fact(DisplayName = "TV-Assistent: „Nimm alle Spiele vom FC Bayern auf“ wird eine Aufnahme-Regel, „Such mir einen Krimi“ sucht Filme")]
    public void Assistant()
    {
        var a = Assert.Single(TvCommands.Rules("Nimm alle Spiele vom FC Bayern auf"));
        Assert.Equal(("record-rule", "fc bayern"), (a.Kind, a.Arg));
        Assert.Equal("record-rule", TvCommands.Rules("Tagesschau immer aufnehmen")[0].Kind);
        var f = Assert.Single(TvCommands.Rules("Such mir einen Krimi für heute Abend"));
        Assert.Equal(("films", "krimi"), (f.Kind, f.Extra));
        Assert.Equal("record", TvCommands.Rules("Nimm heute Abend den Tatort auf")[0].Kind); // einzelne Aufnahme bleibt
    }
}
