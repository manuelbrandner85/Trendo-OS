using Trendo.Core;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>Trendo Musik wie Suno V13 (07.11.2026): Mein Geschmack.</summary>
public class MusicTasteTests
{
    [Fact(DisplayName = "Suno V13: Geschmack aus Favoriten (×3) und davon abgeleiteten Liedern, ohne Zusätze; leer unter zwei Favoriten; Vorschlag und „Für dich“-Reihenfolge")]
    public void Profile_suggest_sort()
    {
        Assert.Equal(["pop", "gitarre"], MusicTaste.Words("Pop, Gitarre · 110 % Tempo, Sample aus „X“ 1,0–3,0 s, Mashup: Gesang"));
        var a = new MusicSong { Title = "A", Style = "Indie-Pop, Akustikgitarre, warm", Favorite = true };
        var b = new MusicSong { Title = "B", Style = "Indie-Pop, Klavier", Favorite = true };
        var c = new MusicSong { Title = "C", Style = "Techno, hart" };
        var d = new MusicSong { Title = "D", Style = "Akustikgitarre, Regen", ParentId = a.Id };
        var s = new MusicSong { Title = "S", Style = "Indie-Pop", Kind = "sound", Favorite = true };
        Assert.Empty(MusicTaste.Profile([a, c, s]));
        var p = MusicTaste.Profile([a, b, c, d, s]);
        Assert.Equal("indie-pop", p[0].Text); Assert.Equal(6, p[0].Weight); Assert.Equal("akustikgitarre", p[1].Text); Assert.Equal(4, p[1].Weight);
        Assert.DoesNotContain(p, w => w.Text is "techno" or "regen");
        var idea = MusicTaste.Suggest(p, 7); Assert.StartsWith("indie-pop, akustikgitarre, ", idea); Assert.Contains("über", idea);
        Assert.Equal(idea, MusicTaste.Suggest(p, 7));
        Assert.True(MusicTaste.Score(a, p) > MusicTaste.Score(b, p)); Assert.Equal(0, MusicTaste.Score(c, p));
        var col = new MusicCollections(Path.Combine(Path.GetTempPath(), "trendo-taste-" + Guid.NewGuid().ToString("N")[..8]));
        var (page, _, _) = SongQuery.Run([c, b, a], col, "*", null, "alle", "geschmack", "", 0);
        Assert.Equal(["A", "B", "C"], page.Select(x => x.Title));
    }
}
