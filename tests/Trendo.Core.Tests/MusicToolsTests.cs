using Trendo.Core;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>Trendo Musik M1/M4/M5/M7/M8/M9 (freigegeben 03.11.2026).</summary>
public class MusicToolsTests
{
    [Fact(DisplayName = "M1 Genres: über 150 in 11 Gruppen, eindeutige Namen, sinnvolle Tempi, Begriffe aus ACE-Steps eigener Genre-Liste (wenn eingerichtet); Suche; M8 Stimmen")]
    public void Genres()
    {
        Assert.True(MusicGenres.All.Length >= 150);
        Assert.Equal(MusicGenres.All.Length, MusicGenres.All.Select(g => g.Name).Distinct().Count());
        Assert.All(MusicGenres.All, g => { Assert.Contains(g.Group, MusicGenres.Groups); Assert.InRange(g.BpmLow, 40, 300); Assert.True(g.BpmHigh >= g.BpmLow); Assert.False(string.IsNullOrWhiteSpace(g.Tag)); });
        Assert.All(MusicGenres.Groups, gr => Assert.NotEmpty(MusicGenres.InGroup(gr)));
        Assert.Contains(MusicGenres.Search("jazz"), g => g.Name == "Bossa Nova" || g.Name == "Jazz");
        Assert.Equal("jazz ballad, smooth jazz, female vocals", MusicGenres.Style([MusicGenres.Find("Jazz-Ballade")!, MusicGenres.Find("Smooth Jazz")!], "female vocals"));
        Assert.True(MusicGenres.Voices.Length >= 12); Assert.Contains(MusicGenres.Voices, v => v.Name.StartsWith("Duett"));
        var vocab = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), DataLocation.ToolsFolderName, "ki", "laufzeit", "acestep", "acestep", "genres_vocab.txt");
        if (!File.Exists(vocab)) return; // ACE-Step nicht eingerichtet – Begriffe sind am 03.11.2026 gegen die Liste geprüft
        var known = File.ReadLines(vocab).Select(l => l.Trim().ToLowerInvariant()).ToHashSet();
        Assert.All(MusicGenres.All, g => Assert.Contains(g.Tag.ToLowerInvariant(), known));
    }

    [Fact(DisplayName = "M4/M9 Liedtext: eingefügten Text gliedern (Wiederholung = Refrain, Bridge vor dem letzten Refrain), Silben je Zeile, Reime, Refrain wiederholen")]
    public void Lyrics_tools()
    {
        var text = "Die Straße glänzt im Abendlicht\nwir fahren los\n\nSommer nimm uns mit\nweit hinaus\n\nDer Wind ist warm\ndie Nacht ist jung\n\nSommer nimm uns mit\nweit hinaus\n\nNur noch ein Stück\n\nSommer nimm uns mit\nweit hinaus";
        var s = SongTools.Structure(text);
        Assert.StartsWith("[Strophe]\nDie Straße", s);
        Assert.Equal(3, System.Text.RegularExpressions.Regex.Matches(s, @"\[Refrain\]").Count);
        Assert.Contains("[Bridge]\nNur noch ein Stück", s);
        Assert.Equal("[Strophe]\nschon", SongTools.Structure("[Strophe]\nschon")); // schon gegliedert
        Assert.Equal(8, SongTools.Syllables("Die Straße glänzt im Abendlicht")); // Die Stra-ße glänzt im A-bend-licht
        Assert.Equal(0, SongTools.Syllables("[Refrain]"));
        Assert.Equal(3, SongTools.Syllables("Eule Meier"));
        Assert.Equal("acht", SongTools.RhymeKey("Nacht")); Assert.Equal("acht", SongTools.RhymeKey("gemacht!"));
        Assert.Equal(["Macht", "Pracht", "gedacht"], SongTools.Rhymes("Nacht", ["Pracht", "Macht/S", "Nacht", "Licht", "gedacht", "9ten"])); // kurze zuerst, ohne das Wort selbst
        Assert.EndsWith("[Refrain]\nSommer nimm uns mit\nweit hinaus", SongTools.RepeatChorus("[Strophe]\na\n[Refrain]\nSommer nimm uns mit\nweit hinaus"));
        Assert.Throws<ArgumentException>(() => SongTools.RepeatChorus("[Strophe]\nnur Strophe"));
    }

    [Fact(DisplayName = "M5 Einrichten: Song-Analyse nur mit PyPI-Paketen (librosa fest 0.11.0) – kein Git, kein PyTorch; als Musik-Werkzeug in der KI-Bibliothek")]
    public void Analyse_setup()
    {
        var app = KiPyApps.For(KiRuntime.MusikAnalyse)!;
        Assert.True(app.PackagesOnly); Assert.Contains("librosa==0.11.0", app.Packages);
        var steps = KiPyApps.Steps(app, new KiPyApps.Paths("uv.exe", "git.exe", "d", "uvhome"), nvidia: true);
        Assert.DoesNotContain(steps, s => s.Exe == "git.exe"); Assert.DoesNotContain(steps, s => s.Text.Contains("PyTorch"));
        Assert.Contains(steps, s => s.Args.Contains("librosa==0.11.0"));
        Assert.True(KiPyApps.CommitOk(app, app.Commit));
        Assert.Contains(KiLibrary.OfKind(KiKind.Musik), m => m.Id == "musik-analyse" && m.Runtime == KiRuntime.MusikAnalyse);
    }

    [Fact(DisplayName = "Spuren trennen: Demucs 4.0.1 von PyPI mit PyTorch (CUDA 12.8 bei NVIDIA), kein Git; als Musik-Werkzeug (MIT) in der KI-Bibliothek")]
    public void Stems_setup()
    {
        var app = KiPyApps.For(KiRuntime.Spuren)!;
        Assert.True(app.PackagesOnly); Assert.Contains("demucs==4.0.1", app.Packages); Assert.Equal("trendo_spuren.py", app.Script);
        var steps = KiPyApps.Steps(app, new KiPyApps.Paths("uv.exe", "git.exe", "d", "uvhome"), nvidia: true);
        Assert.DoesNotContain(steps, s => s.Exe == "git.exe");
        Assert.Contains(steps, s => s.Text.Contains("PyTorch") && s.Args.Contains("https://download.pytorch.org/whl/cu128"));
        Assert.Contains(KiLibrary.OfKind(KiKind.Musik), m => m.Id == "musik-spuren" && m.Runtime == KiRuntime.Spuren && m.License == "MIT");
    }

    [Fact(DisplayName = "M5/M7: Analyse-Ergebnis lesen (Tonart deutsch, Takt-Wert); MusicBrainz-Treffer und AcousticBrainz-Messung (Kreuz-Tonarten wie bei ACE-Step) lesen, kaputte Antworten = nichts")]
    public void Analysis_and_facts()
    {
        var a = SongAnalysis.Parse("""{"duration": 40.0, "bpm": 92.3, "key": "A minor", "key_confidence": 0.67, "meter": "3/4", "chords": ["Am", "G"], "range": {"low": "E2", "high": "A3", "semitones": 17, "voice": "Bass"}}""")!;
        Assert.Equal(("A-Moll", "3", 92.3, 17), (a.KeyDe, a.MeterValue, a.Bpm, a.Range!.Semitones));
        Assert.Null(SongAnalysis.Parse("kaputt"));
        var mb = SongTools.ParseMusicBrainz("""{"recordings":[{"id":"96685213-a25c","title":"Bohemian Rhapsody","artist-credit":[{"name":"Queen"}],"first-release-date":"1975-10-31"}]}""");
        Assert.Equal(new SongTools.Recording("96685213-a25c", "Bohemian Rhapsody", "Queen", 1975), mb.Single());
        Assert.Empty(SongTools.ParseMusicBrainz("<html>"));
        var f = SongTools.ParseAcousticBrainz("""{"tonal":{"key_key":"A#","key_scale":"major"},"rhythm":{"bpm":71.83}}""")!;
        Assert.Equal(("Bb major", 71.8), (f.Key, f.Bpm)); Assert.Contains("CC0", f.Source);
        Assert.Null(SongTools.ParseAcousticBrainz("""{"message":"Not found"}"""));
        Assert.Equal("Es-Dur", SongTools.KeyDe("Eb major"));
    }
}
