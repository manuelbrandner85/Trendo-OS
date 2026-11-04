using Trendo.Core;
using Trendo.Core.Media;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>Trendo Musik – nachgebaute Vorbild-Funktionen (04.11.2026): Mastering-Stile, Referenz, Üben, Kapo, MIDI, Ablaufplan, Loop, Kurzclip.</summary>
public class MusicProTests
{
    static AudioBuf Tone(double hz, double sec, float amp = 0.3f, int rate = 22050) { var a = AudioBuf.Silence(sec, rate, 2); for (var i = 0; i < a.Length; i++) { var v = amp * (float)Math.Sin(2 * Math.PI * hz * i / rate); a.Data[0][i] = v; a.Data[1][i] = v; } return a; }

    [Fact(DisplayName = "Mastering: Stil und Ziel je Plattform in der FFmpeg-Kette; Referenz-Angleichung hebt fehlende Höhen an (höchstens ±6 dB)")]
    public void Mastering()
    {
        var a = string.Join(" ", MusicPro.MasterArgs("in.wav", "out.mp3", "warm", "apple"));
        Assert.Contains("bass=g=2", a); Assert.Contains("loudnorm=I=-16:TP=-1", a); Assert.Contains("libmp3lame", a);
        Assert.Contains("loudnorm=I=-9", string.Join(" ", MusicPro.MasterArgs("in.wav", "out.wav", "gibts", "cd")));
        var dull = MusicPro.BandLevels(Tone(150, 2)); var bright = MusicPro.BandLevels(Tone(6000, 2));
        Assert.True(dull[1] > dull[5] + 20, "150 Hz im Bassband"); Assert.True(bright[5] > bright[1] + 20);
        var (eq, g) = MusicPro.MatchEq(dull, bright);
        Assert.Equal(6, g[5]); Assert.Equal(-6, g[1]); Assert.Contains("equalizer=f=6000", eq);
    }

    [Fact(DisplayName = "Üben: Tempo und Tonart begrenzt (Rubber Band), Klick betont jeden 4. Schlag, Kapodaster-Vorschlag")]
    public void Practice()
    {
        var a = string.Join(" ", MusicPro.PracticeArgs("in.mp3", "out.wav", 0.2, 9));
        Assert.Contains("rubberband=tempo=0.5:pitch=1.414", a);
        var c = MusicPro.ClickTrack([0, 0.5, 1.0, 1.5, 2.0], 3, 4, 8000);
        Assert.True(Math.Abs(c.Data[0][10]) > 0 || Math.Abs(c.Data[0][5]) > 0);
        var peak0 = c.Data[0].Skip(0).Take(300).Max(Math.Abs); var peak1 = c.Data[0].Skip(4000).Take(300).Max(Math.Abs);
        Assert.True(peak0 > peak1, "erster Schlag betont");
        Assert.Equal((2, "A"), MusicPro.Capo("B major")); Assert.Equal((0, "C"), MusicPro.Capo("C major")); Assert.Equal((1, "Am"), MusicPro.Capo("Bb minor"));
    }

    [Fact(DisplayName = "MIDI: Noten aus der Melodie (kurze Ausreißer weg), Akkord-Töne, gültige Datei mit zwei Spuren")]
    public void Midi()
    {
        double?[] mel = [60, 60, 60, 62, null, 64, 64, 64, 64, 65];
        var notes = MusicPro.NotesFromMelody(mel, 0.1);
        Assert.Equal([60, 64], notes.Select(n => n.Pitch)); Assert.Equal(0.3, notes[0].Length, 3); Assert.Equal(0.5, notes[1].Start, 3);
        Assert.Equal([57, 60, 64], MusicPro.ChordNotes("Am")); Assert.Equal([54, 58, 61], MusicPro.ChordNotes("F#")); Assert.Empty(MusicPro.ChordNotes("X"));
        var bytes = MusicPro.Midi(notes, ["Am", "F"], 120, 4, 4);
        Assert.Equal("MThd", System.Text.Encoding.ASCII.GetString(bytes, 0, 4)); Assert.Equal(2, bytes[11]);
        Assert.Equal(2, System.Text.Encoding.ASCII.GetString(bytes).Split("MTrk").Length - 1);
    }

    [Fact(DisplayName = "Ablaufplan: Länge aus Takten und Tempo, Liedtext-Gerüst mit Stimmung, Instrumental-Teile, Texte je Abschnitt")]
    public void Plan()
    {
        Assert.Equal(52 * 4 * 0.5, MusicPro.PlanSeconds(MusicPro.DefaultPlan, 120, 4), 3);
        var t = MusicPro.PlanLyrics(MusicPro.DefaultPlan, new Dictionary<string, string> { ["Strophe 1"] = "Erste Zeile", ["Refrain"] = "Sing mit" });
        Assert.StartsWith("[Intro - ruhig]\n[Instrumental]", t); Assert.Contains("[Strophe - erzählend]\nErste Zeile", t);
        Assert.Equal(3, t.Split("Sing mit").Length - 1);
    }

    [Fact(DisplayName = "Loop: genau 2 Takte, Übergang ohne Sprung (Ende passt zum Anfang)")]
    public void Loop()
    {
        var a = Tone(220, 6, 0.4f, 22050);
        var l = MusicPro.MakeLoop(a, 120, 4, 2);
        Assert.Equal(4.0, l.Seconds, 2);
        var jump = Math.Abs(l.Data[0][^1] - l.Data[0][0]); var step = a.Data[0].Skip(1).Zip(a.Data[0]).Max(p => Math.Abs(p.First - p.Second));
        Assert.True(jump <= step * 1.5, $"Sprung {jump} größer als ein normaler Schritt {step}");
        Assert.Throws<InvalidOperationException>(() => MusicPro.MakeLoop(a, 60, 4, 8));
    }

    [Fact(DisplayName = "Kurzclip: Start am stärksten Abschnitt; echter Export mit FFmpeg ergibt ein Hochkant-Video mit Ton")]
    public void Hook()
    {
        Assert.Equal(30, MusicPro.HookStart([new(0, 30, 0.3), new(30, 60, 0.9), new(60, 90, 0.5)], 90, 20));
        Assert.Equal(70, MusicPro.HookStart([new(80, 90, 0.9)], 90, 20));
        var ff = Cli.Find("ffmpeg"); if (ff is null) return;
        var dir = Path.Combine(Path.GetTempPath(), "trendo-hook-" + Guid.NewGuid().ToString("N")[..8]); Directory.CreateDirectory(dir);
        try
        {
            var wav = Path.Combine(dir, "a.wav"); AudioFile.WriteWav(Tone(440, 12), wav);
            var mp4 = Path.Combine(dir, "clip.mp4");
            var r = Cli.Run(ff, MusicPro.HookArgs(wav, null, mp4, 2, 5, "Mein Lied: Teil 1"), dir, 120000);
            Assert.True(r.Ok, r.Err);
            Assert.InRange(MediaStudio.Duration(ff, mp4)!.Value, 4.7, 5.3);
            var probe = Cli.Run(Path.Combine(Path.GetDirectoryName(ff)!, Path.GetFileName(ff).Replace("ffmpeg", "ffprobe")), ["-v", "error", "-select_streams", "v:0", "-show_entries", "stream=width,height", "-of", "csv=p=0", mp4], dir, 30000);
            Assert.Equal("1080,1920", probe.Out.Trim());
            var m = Cli.Run(ff, MusicPro.MasterArgs(wav, Path.Combine(dir, "m.wav"), "offen", "streaming"), dir, 120000); Assert.True(m.Ok, m.Err);
            var p = Cli.Run(ff, MusicPro.PracticeArgs(wav, Path.Combine(dir, "p.wav"), 0.8, 2), dir, 120000); Assert.True(p.Ok, p.Err);
            Assert.InRange(MediaStudio.Duration(ff, Path.Combine(dir, "p.wav"))!.Value, 14.5, 15.5); // 12 s bei 80 % Tempo
        }
        finally { Directory.Delete(dir, true); }
    }
}
