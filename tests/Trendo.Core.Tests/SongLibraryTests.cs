using Trendo.Core;
using Xunit;

namespace Trendo.Core.Tests;

/// Studio „Trendo Musik“ (03.11.2026): Liederbibliothek und Liedtext-Marken.
public class SongLibraryTests
{
    [Fact(DisplayName = "Liederbibliothek: neueste zuerst, Suche, Favoriten, Umbenennen, Löschen nur über den Papierkorb; fehlende Dateien verschwinden aus der Ansicht")]
    public void Library()
    {
        var root = Path.Combine(Path.GetTempPath(), "trendo-songs-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        try
        {
            var lib = new SongLibrary(root);
            string F(string n) { var f = Path.Combine(root, n); File.WriteAllText(f, "ID3"); return f; }
            var a = lib.Add(new MusicSong { Title = "Sommer", Style = "Pop", File = F("a.mp3"), Created = DateTimeOffset.Now.AddMinutes(-5) });
            var b = lib.Add(new MusicSong { Title = "Nachtfahrt", Style = "Synthwave", Lyrics = "[Instrumental]", File = F("b.mp3") });
            Assert.Equal(["Nachtfahrt", "Sommer"], lib.Visible().Select(s => s.Title));
            Assert.Equal(["Sommer"], lib.Visible(search: "pop").Select(s => s.Title));
            lib.ToggleFavorite(a.Id); Assert.Equal(["Sommer"], lib.Visible(favoritesOnly: true).Select(s => s.Title));
            lib.Rename(b.Id, "  "); Assert.Equal("Nachtfahrt", lib.Get(b.Id)!.Title);
            lib.Rename(b.Id, "Nachtfahrt II"); Assert.Equal("Nachtfahrt II", lib.Get(b.Id)!.Title);
            var bin = new List<string>(); lib.Remove(a.Id, bin.Add);
            Assert.Equal([a.File], bin); Assert.Null(lib.Get(a.Id)); Assert.True(File.Exists(a.File)); // Datei geht an den Papierkorb, nicht direkt gelöscht
            File.Delete(b.File); Assert.Empty(lib.Visible());
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact(DisplayName = "Liedtext: deutsche Marken werden für ACE-Step übersetzt, Titel aus erster Liedzeile oder Beschreibung")]
    public void Lyrics_and_title()
    {
        Assert.Equal("[Verse]\nHallo\n[Chorus]\nJa\n[Pre-Chorus]\n[Instrumental]", SongLibrary.ToAceLyrics("[Strophe]\nHallo\n[Refrain]\nJa\n[Vor-Refrain]\n[Solo]"));
        Assert.Equal("Die Straße glänzt", SongLibrary.TitleFrom("Pop", "[Strophe]\ndie Straße glänzt\n[Refrain]"));
        Assert.Equal("Fröhliches Sommerlied über einen Roadtrip", SongLibrary.TitleFrom("fröhliches Sommerlied über einen Roadtrip mit Gitarre", ""));
        Assert.Equal("Neues Lied", SongLibrary.TitleFrom("", ""));
    }
}
