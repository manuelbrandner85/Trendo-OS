using Trendo.Core.Media;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>Paket 01 – Studio 4, S4-19 (07.11.2026): Melodie → MIDI mit Sicherheit und Mehrstimmigkeits-Erkennung.</summary>
public class AudioToMidiTests
{
    static double Hz(int midi) => 440 * Math.Pow(2, (midi - 69) / 12.0);

    /// <summary>Töne nacheinander (mit Obertönen wie ein Instrument), je 0,4 s, kurze Pausen.</summary>
    static AudioBuf Melody(params int[] pitches)
    {
        const int rate = 44100; var seg = (int)(0.4 * rate); var gap = (int)(0.05 * rate);
        var x = new float[pitches.Length * (seg + gap)];
        for (var k = 0; k < pitches.Length; k++)
            for (var i = 0; i < seg; i++)
            {
                var t = i / (double)rate; var f = Hz(pitches[k]); var env = Math.Min(1, i / 400.0) * Math.Min(1, (seg - i) / 400.0);
                x[k * (seg + gap) + i] = (float)(env * (0.5 * Math.Sin(2 * Math.PI * f * t) + 0.2 * Math.Sin(4 * Math.PI * f * t) + 0.1 * Math.Sin(6 * Math.PI * f * t)));
            }
        return new AudioBuf(rate, [x]);
    }

    static AudioBuf Chord(double seconds, params int[] pitches)
    {
        const int rate = 44100; var n = (int)(seconds * rate); var x = new float[n];
        for (var i = 0; i < n; i++) { var t = i / (double)rate; x[i] = (float)(pitches.Sum(p => 0.25 * Math.Sin(2 * Math.PI * Hz(p) * t))); }
        return new AudioBuf(rate, [x]);
    }

    [Fact(DisplayName = "S4-19-P: einstimmige Melodie (A3 C4 E4 A4 G4) richtig erkannt, Sicherheit je Note hoch, als exakt markiert; MIDI über die Piano-Roll")]
    public void Monophonic_melody()
    {
        var r = AudioToMidi.Transcribe(Melody(57, 60, 64, 69, 67));
        Assert.Equal([57, 60, 64, 69, 67], r.Notes.Select(n => n.Pitch));
        Assert.All(r.Notes, n => Assert.True(n.Confidence >= 0.8, $"{n.Pitch}: {n.Confidence}"));
        Assert.All(r.Notes, n => Assert.InRange(n.Length, 0.3, 0.45));
        Assert.True(r.Exact, r.Verdict); Assert.Contains("Einstimmig", r.Verdict);
        var song = AudioToMidi.ToSong(r, 120, "Melodie");
        var back = PianoRoll.FromMidi(PianoRoll.ToMidi(song));
        Assert.Equal([57, 60, 64, 69, 67], back.Song!.Notes.Select(n => n.Pitch));
        Assert.Contains("| A3 |", AudioToMidi.Report("Melodie", r));
    }

    [Fact(DisplayName = "S4-19-N: Akkord (mehrstimmig) und Rauschen gelten nie als exakt – Bericht sagt „NICHT exakt“ bzw. „keine Melodie“")]
    public void Polyphonic_not_exact()
    {
        var chord = AudioToMidi.Transcribe(Chord(1.5, 57, 60, 64)); // Am
        Assert.False(chord.Exact); Assert.True(chord.PolyphonicShare >= 0.1, chord.Verdict); Assert.Contains("NICHT exakt", chord.Verdict);
        Assert.Contains("NICHT exakt", AudioToMidi.Report("Akkord", chord));
        var rng = new Random(3); var noise = new AudioBuf(44100, [Enumerable.Range(0, 44100).Select(_ => (float)(rng.NextDouble() * 0.2 - 0.1)).ToArray()]);
        var nr = AudioToMidi.Transcribe(noise); Assert.False(nr.Exact);
        var silence = AudioToMidi.Transcribe(new AudioBuf(44100, [new float[44100]]));
        Assert.Empty(silence.Notes); Assert.Contains("Keine Melodie", silence.Verdict); Assert.False(silence.Exact);
    }
}
