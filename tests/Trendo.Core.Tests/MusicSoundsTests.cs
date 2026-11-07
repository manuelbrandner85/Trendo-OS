using Trendo.Core;
using Trendo.Core.Media;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>Trendo Musik wie Suno V7/V8 (07.11.2026): One-Shot, Loop, Sprache mit Musik.</summary>
public class MusicSoundsTests
{
    [Fact(DisplayName = "Suno V7: One-Shot schneidet vom Einsatz bis zum Abklingen (−50 dB), Spitze −1 dBFS; Stille → Fehler")]
    public void One_shot()
    {
        const int rate = 16000; var x = new float[rate * 6];
        var at = (int)(1.5 * rate); for (var i = 0; i < rate; i++) x[at + i] = (float)(0.6 * Math.Sin(2 * Math.PI * 440 * i / rate) * Math.Exp(-i / (rate * 0.08)));
        var o = MusicSounds.OneShot(new AudioBuf(rate, [x]));
        Assert.InRange(o.Seconds, 0.42, 0.6); // −50 dB nach 0,08 s · ln(316) = 0,46 s
        Assert.InRange(o.Data[0].Max(Math.Abs), 0.88, 0.9); Assert.InRange(Math.Abs(o.Data[0][^1]), 0, 0.001);
        Assert.Throws<InvalidOperationException>(() => MusicSounds.OneShot(new AudioBuf(rate, [new float[rate * 3]])));
    }

    [Fact(DisplayName = "Suno V7: Loop im erkannten Tempo – 2 Takte bei 120 BPM = 4 s, Takte/Tempo-Rechnung, Beschreibung für die Musik-KI")]
    public void Loop()
    {
        const int rate = 22050; var x = new float[rate * 12]; var beat = rate / 2; // 120 BPM
        for (var b = 1; b < 23; b++) { var s = b * beat; for (var i = 0; i < rate / 10 && s + i < x.Length; i++) x[s + i] = (float)(0.7 * Math.Sin(2 * Math.PI * 80 * i / rate) * Math.Exp(-i / (rate * 0.03))); }
        var (l, bpm) = MusicSounds.Loop(new AudioBuf(rate, [x]), 120, 2);
        Assert.InRange(bpm, 118, 122); Assert.InRange(l.Seconds, 3.9, 4.1);
        Assert.Equal(2.0, MusicSounds.BarsSeconds(120, 1), 3); Assert.Equal(16.0, MusicSounds.BarsSeconds(60, 4), 3);
        Assert.Contains("seamless loop, 96 bpm, A minor", MusicSounds.SoundPrompt("funky Bass", true, 96, "A minor"));
        Assert.Contains("one shot", MusicSounds.SoundPrompt("Glas zerbricht", false, 0, null));
    }

    [Fact(DisplayName = "Suno V8 echt mit FFmpeg: Sprache nach 2 s über der Musik; Musik ist beim Sprechen deutlich leiser (Sidechain) und blendet aus")]
    public void Speech_mix()
    {
        var ff = Cli.Find("ffmpeg"); if (ff is null) return;
        var dir = Path.Combine(Path.GetTempPath(), "trendo-sp-" + Guid.NewGuid().ToString("N")[..6]); Directory.CreateDirectory(dir);
        try
        {
            const int rate = 44100;
            var voice = new float[rate * 4]; for (var i = 0; i < voice.Length; i++) voice[i] = (float)(0.5 * Math.Sin(2 * Math.PI * 220 * i / rate) * (0.6 + 0.4 * Math.Sin(2 * Math.PI * 3 * i / rate)));
            var music = new float[rate * 12]; for (var i = 0; i < music.Length; i++) music[i] = (float)(0.5 * Math.Sin(2 * Math.PI * 523 * i / rate));
            string W(string n, float[] d) { var f = Path.Combine(dir, n); AudioFile.WriteWav(new AudioBuf(rate, [d]), f); return f; }
            var o = Path.Combine(dir, "mix.wav");
            var r = Cli.Run(ff, MusicSounds.SpeechMixArgs(W("v.wav", voice), W("m.wav", music), o, 4), dir, 60000);
            Assert.True(r.Ok && File.Exists(o), r.Err);
            var a = AudioFile.Read(o); Assert.InRange(a.Seconds, 8.9, 9.1); // 2 + 4 + 3
            // Musik (523 Hz) vor dem Sprechen vs. während: Goertzel-Energie
            double Tone(double from, double to, double hz)
            {
                int s = (int)(from * a.Rate), e = (int)(to * a.Rate); var k = 2 * Math.Cos(2 * Math.PI * hz / a.Rate); double s1 = 0, s2 = 0;
                for (var i = s; i < e; i++) { var s0 = a.Data[0][i] + k * s1 - s2; s2 = s1; s1 = s0; }
                return Math.Sqrt(s1 * s1 + s2 * s2 - k * s1 * s2) / (e - s);
            }
            double before = Tone(0.5, 1.8, 523), during = Tone(3.0, 5.5, 523);
            Assert.True(during < before * 0.5, $"Musik vorher {before:0.0000}, beim Sprechen {during:0.0000}");
            Assert.True(Tone(3.0, 5.5, 220) > during, "Sprache muss lauter sein als die abgesenkte Musik");
            if (Environment.GetEnvironmentVariable("TRENDO_SPEECH_BELEG") is { Length: > 0 } keep)
            { Directory.CreateDirectory(keep); File.WriteAllText(Path.Combine(keep, "ducking.txt"), $"Musikpegel vor dem Sprechen {before:0.0000}, beim Sprechen {during:0.0000} ({20 * Math.Log10(during / before):0.0} dB)\n"); }
        }
        finally { Directory.Delete(dir, true); }
    }
}
