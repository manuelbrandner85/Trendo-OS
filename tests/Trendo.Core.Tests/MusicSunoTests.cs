using Trendo.Core;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>Trendo Musik wie Suno V2/V3/V4/V17 (07.11.2026).</summary>
public class MusicSunoTests
{
    [Fact(DisplayName = "Suno V2/V17: Liedidee je Zufallswert gleich, verschieden je Wert; Idee des Tages wechselt täglich")]
    public void Ideas()
    {
        Assert.Equal(MusicSuno.Idea(7), MusicSuno.Idea(7));
        Assert.True(Enumerable.Range(0, 20).Select(MusicSuno.Idea).Distinct().Count() >= 15);
        Assert.Contains(" über ", MusicSuno.Idea(1));
        var d = new DateOnly(2026, 11, 7);
        Assert.Equal(MusicSuno.DailyIdea(d), MusicSuno.DailyIdea(d)); Assert.NotEqual(MusicSuno.DailyIdea(d), MusicSuno.DailyIdea(d.AddDays(1)));
    }

    [Fact(DisplayName = "Suno V2 „+ Bild“: Farbton, Helligkeit, Sättigung und Kontrast werden zu nachvollziehbaren Stimmungswörtern")]
    public void Image_mood()
    {
        Assert.Contains("hell und sonnig", MusicSuno.ImageMood(new(35, 0.6, 0.75, 0.25))); // Sonnenuntergang hell, warm
        Assert.Contains("lebhaft", MusicSuno.ImageMood(new(35, 0.6, 0.75, 0.25)));
        Assert.Contains("düster", MusicSuno.ImageMood(new(220, 0.3, 0.15, 0.1)));             // Nacht, blau
        Assert.Contains("zurückhaltend", MusicSuno.ImageMood(new(0, 0.05, 0.5, 0.1)));         // grau
        Assert.Contains("naturnah", MusicSuno.ImageMood(new(120, 0.5, 0.5, 0.2)));             // Wald
    }

    [Fact(DisplayName = "Suno V3: Stile ausschließen streicht Begriffe und setzt den Ausschluss-Prompt; Stimme w/m; Regler auf ACE-Step-Werte; Grundeinstellung sendet nichts")]
    public void Options_to_ace()
    {
        var o = new MusicSuno.Options(Exclude: "Schlagzeug; Rap", VoiceGender: MusicSuno.Voice.Female);
        Assert.Equal("Pop, Akustikgitarre, female vocals", MusicSuno.ApplyToStyle("Pop, Schlagzeug kräftig, Akustikgitarre, Rap-Teil, Männerstimme", o));
        Assert.Empty(MusicSuno.AceFields(new(), hasReference: true));
        Assert.True(new MusicSuno.Options().IsDefault);
        var f = MusicSuno.AceFields(o with { Weirdness = 100, StyleInfluence = 0, Variety = 0, AudioInfluence = 100, Max = true }, hasReference: true);
        Assert.Equal("1.35", f["lm_temperature"]); Assert.Equal("1", f["lm_cfg_scale"]); Assert.Equal("0.6", f["lm_top_p"]); Assert.Equal("1", f["audio_cover_strength"]);
        Assert.Equal("Schlagzeug, Rap", f["lm_negative_prompt"]); Assert.Equal("64", f["inference_steps"]);
        Assert.Equal("0.55", MusicSuno.AceFields(new(Weirdness: 0), false)["lm_temperature"]);
        Assert.Equal("5", MusicSuno.AceFields(new(StyleInfluence: 100), false)["lm_cfg_scale"]);
        Assert.False(MusicSuno.AceFields(new(AudioInfluence: 90), hasReference: false).ContainsKey("audio_cover_strength")); // ohne Klangvorbild sinnlos
        Assert.Equal("ohne Schlagzeug/Rap · Frauenstimme · 2:30 min · Verrücktheit 80", MusicSuno.Describe(o with { Seconds = 150, Weirdness = 80 }));
    }

    [Fact(DisplayName = "Suno V4: Playlists, Alben, Arbeitsbereiche anlegen, doppelt abgelehnt, Lieder hinzufügen/umsortieren/entfernen; Löschen lässt Lieder in Ruhe")]
    public void Collections()
    {
        var root = Path.Combine(Path.GetTempPath(), "trendo-mc-" + Guid.NewGuid().ToString("N")[..6]);
        try
        {
            var c = new MusicCollections(root);
            var p = c.Create(MusicCollections.Playlist, "Sommer 2026");
            Assert.Throws<ArgumentException>(() => c.Create(MusicCollections.Playlist, "sommer 2026"));
            Assert.Throws<ArgumentException>(() => c.Create(MusicCollections.Workspace, MusicCollections.DefaultWorkspace));
            Assert.Throws<ArgumentException>(() => c.Create("ordner", "x")); Assert.Throws<ArgumentException>(() => c.Create(MusicCollections.Album, "  "));
            c.AddSong(p.Id, "a"); c.AddSong(p.Id, "b"); c.AddSong(p.Id, "c"); c.AddSong(p.Id, "a");
            Assert.Equal(["a", "b", "c"], c.Get(p.Id)!.SongIds);
            c.Move(p.Id, "c", -2); Assert.Equal(["c", "a", "b"], c.Get(p.Id)!.SongIds);
            c.RemoveSong(p.Id, "a"); Assert.Equal(["c", "b"], c.Get(p.Id)!.SongIds);
            var w1 = c.Create(MusicCollections.Workspace, "Filmmusik"); var w2 = c.Create(MusicCollections.Workspace, "Demos");
            c.MoveToWorkspace("x", w1.Id); c.MoveToWorkspace("x", w2.Id);
            Assert.Equal(w2.Id, c.WorkspaceOf("x")); Assert.DoesNotContain("x", c.Get(w1.Id)!.SongIds);
            c.MoveToWorkspace("x", null); Assert.Null(c.WorkspaceOf("x"));
            c.Rename(p.Id, "Sommerhits"); Assert.Equal("Sommerhits", c.Get(p.Id)!.Name);
            c.Delete(p.Id); Assert.Null(c.Get(p.Id));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact(DisplayName = "Suno V4: Liste nach Arbeitsbereich, Playlist-Reihenfolge, Filter, Suche, Sortierung und Seiten")]
    public void Query()
    {
        var root = Path.Combine(Path.GetTempPath(), "trendo-mq-" + Guid.NewGuid().ToString("N")[..6]);
        try
        {
            var t0 = new DateTimeOffset(2026, 11, 1, 10, 0, 0, TimeSpan.Zero);
            MusicSong S(string id, string title, string kind = "neu", string lyrics = "[Strophe]\nla", bool fav = false, double sec = 120, int min = 0) =>
                new() { Id = id, Title = title, Kind = kind, Lyrics = lyrics, Favorite = fav, Seconds = sec, Created = t0.AddMinutes(min), File = "x" };
            var songs = new List<MusicSong> { S("a", "Zebra", fav: true, min: 1), S("b", "Apfel", "cover", min: 2), S("c", "Mond", lyrics: "[Instrumental]", sec: 300, min: 3),
                S("d", "Gesang", "gesang", min: 4), S("e", "Meine Aufnahme", "eigen", min: 5) };
            var c = new MusicCollections(root);
            var ws = c.Create(MusicCollections.Workspace, "Projekt"); c.MoveToWorkspace("c", ws.Id);
            var pl = c.Create(MusicCollections.Playlist, "Liste"); c.AddSong(pl.Id, "e"); c.AddSong(pl.Id, "a");
            Assert.Equal(["e", "d", "b", "a"], SongQuery.Run(songs, c, null, null, "alle", "neu", "", 0).Page.Select(s => s.Id)); // „Mein Arbeitsbereich“ ohne „c“
            Assert.Equal(["c"], SongQuery.Run(songs, c, ws.Id, null, "alle", "neu", "", 0).Page.Select(s => s.Id));
            Assert.Equal(5, SongQuery.Run(songs, c, "*", null, "alle", "neu", "", 0).Total);
            Assert.Equal(["e", "a"], SongQuery.Run(songs, c, null, pl.Id, "alle", "neu", "", 0).Page.Select(s => s.Id)); // Reihenfolge der Playlist
            Assert.Equal(["a", "e"], SongQuery.Run(songs, c, null, pl.Id, "alle", "titel", "", 0).Page.Select(s => s.Id).Reverse());
            Assert.Equal(["a"], SongQuery.Run(songs, c, "*", null, "favoriten", "neu", "", 0).Page.Select(s => s.Id));
            Assert.Equal(["c"], SongQuery.Run(songs, c, "*", null, "instrumental", "neu", "", 0).Page.Select(s => s.Id));
            Assert.Equal(["b"], SongQuery.Run(songs, c, "*", null, "fassungen", "neu", "", 0).Page.Select(s => s.Id));
            Assert.Equal(["d"], SongQuery.Run(songs, c, "*", null, "spuren", "neu", "", 0).Page.Select(s => s.Id));
            Assert.Equal(["e"], SongQuery.Run(songs, c, "*", null, "eigene", "neu", "", 0).Page.Select(s => s.Id));
            Assert.Equal(["b", "d", "e", "c", "a"], SongQuery.Run(songs, c, "*", null, "alle", "titel", "", 0).Page.Select(s => s.Id));
            Assert.Equal("c", SongQuery.Run(songs, c, "*", null, "alle", "laenge", "", 0).Page[0].Id);
            Assert.Equal(["a"], SongQuery.Run(songs, c, "*", null, "alle", "neu", "zeb", 0).Page.Select(s => s.Id));
            var many = Enumerable.Range(0, 65).Select(i => S("s" + i, "Lied " + i, min: i)).ToList();
            var (page, total, pages) = SongQuery.Run(many, new MusicCollections(root + "-leer"), null, null, "alle", "neu", "", 2);
            Assert.Equal((5, 65, 3), (page.Count, total, pages)); Assert.Equal("s4", page[0].Id);
            Assert.Equal(3, SongQuery.Run(many, new MusicCollections(root + "-leer"), null, null, "alle", "neu", "", 99).Pages); // zu große Seite → letzte
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
