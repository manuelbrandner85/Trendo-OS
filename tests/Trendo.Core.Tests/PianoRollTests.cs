using Trendo.Core;
using Trendo.Core.Media;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>Paket 01 – Studio 4, S4-04 (06.11.2026): Piano-Roll, Quantisieren, MIDI schreiben und lesen.</summary>
public class PianoRollTests
{
    static PianoRoll.Song Demo() => new(96, 4,
    [
        new(57, 0, 960, 90), new(60, 0, 960, 80), new(64, 0, 960, 80), // Akkord Am
        new(69, 960, 240), new(71, 1200, 240), new(72, 1440, 480, 110), new(72, 1920, 120, 60), // gleicher Ton direkt danach
    ], "Melodie Erika Muster");

    [Fact(DisplayName = "S4-04-P: Piano-Roll bearbeiten (setzen, löschen, quantisieren, transponieren), als MIDI speichern und identisch wieder öffnen – auch Dateien eines anderen Schreibers")]
    public void Edit_save_reopen()
    {
        var s = Demo();
        var notes = PianoRoll.Toggle(s.Notes, 74, 2400, 240); Assert.Equal(8, notes.Count);
        Assert.Equal(7, PianoRoll.Toggle(notes, 74, 2500, 240).Count); // gleiche Note angeklickt → weg
        var q = PianoRoll.Quantize([new(60, 130, 100), new(62, 250, 100)], 120);
        Assert.Equal([120, 240], q.Select(n => n.Start)); Assert.Equal(125, PianoRoll.Quantize([new(60, 130, 100)], 120, 0.5)[0].Start);
        Assert.Equal(120, PianoRoll.Quantize([new(60, 0, 100)], 120, 1, lengths: true)[0].Length);
        Assert.Equal(62, PianoRoll.Transpose([new(60, 0, 1)], 2)[0].Pitch); Assert.Equal(127, PianoRoll.Transpose([new(126, 0, 1)], 5)[0].Pitch);
        var bytes = PianoRoll.ToMidi(s with { Notes = notes });
        var back = PianoRoll.FromMidi(bytes);
        Assert.True(back.Ok); Assert.Empty(back.Warnings);
        Assert.Equal((96.0, 4, "Melodie Erika Muster"), (back.Song!.Bpm, back.Song.Numerator, back.Song.Name));
        Assert.Equal(notes.OrderBy(n => n.Start).ThenBy(n => n.Pitch), back.Song.Notes);
        Assert.Equal(bytes, PianoRoll.ToMidi(back.Song)); // Byte für Byte gleich
        Assert.Equal("A3", PianoRoll.NoteName(57)); Assert.Equal("C4", PianoRoll.NoteName(60));
        // Datei eines anderen Schreibers (Trendo-Melodie-Export, Format 1, zwei Notenspuren)
        var other = MusicPro.Midi([new MusicPro.Note(64, 0.0, 0.5), new MusicPro.Note(67, 0.5, 0.5)], ["Am", "F"], 120, 4, 4);
        var o = PianoRoll.FromMidi(other);
        Assert.True(o.Ok, string.Join("; ", o.Warnings)); Assert.Equal(120, o.Song!.Bpm); Assert.Contains(o.Song.Notes, n => n.Pitch == 64 && n.Start == 0);
    }

    [Fact(DisplayName = "S4-04-N: kaputte MIDI-Daten → Warnung statt Absturz; ohne Kopf kein Ergebnis (offenes Stück bleibt), abgeschnittene Datei liefert, was lesbar ist")]
    public void Broken_midi()
    {
        var junk = PianoRoll.FromMidi("Hallo, ich bin keine MIDI-Datei"u8.ToArray());
        Assert.False(junk.Ok); Assert.Contains("MThd", junk.Warnings[0]);
        var bytes = PianoRoll.ToMidi(Demo());
        var cut = PianoRoll.FromMidi(bytes[..(bytes.Length - 30)]);
        Assert.True(cut.Ok); Assert.NotEmpty(cut.Warnings); Assert.InRange(cut.Song!.Notes.Count, 1, Demo().Notes.Count);
        var noTrack = PianoRoll.FromMidi(bytes[..14]); Assert.True(noTrack.Ok); Assert.Contains(noTrack.Warnings, w => w.Contains("fehlt")); Assert.Empty(noTrack.Song!.Notes);
        var bad = (byte[])bytes.Clone(); bad[12] = 0x80; bad[13] = 0; // SMPTE
        Assert.False(PianoRoll.FromMidi(bad).Ok);
        Assert.Throws<ArgumentException>(() => PianoRoll.ToMidi(Demo() with { Notes = [new(200, 0, 10)] }));
        Assert.Throws<ArgumentException>(() => PianoRoll.Quantize(Demo().Notes, 0));
    }

    [Fact(DisplayName = "S4-04 echt: MIDI-Datei auf Platte, von FFmpeg als MIDI erkannt (wenn vorhanden), SHA-256 gleich nach erneutem Speichern")]
    public void Real_file()
    {
        var dir = Path.Combine(Path.GetTempPath(), "trendo-midi-" + Guid.NewGuid().ToString("N")[..6]); Directory.CreateDirectory(dir);
        try
        {
            var f = Path.Combine(dir, "melodie.mid"); File.WriteAllBytes(f, PianoRoll.ToMidi(Demo()));
            var re = PianoRoll.FromMidi(File.ReadAllBytes(f));
            var f2 = Path.Combine(dir, "melodie-wieder.mid"); File.WriteAllBytes(f2, PianoRoll.ToMidi(re.Song!));
            Assert.Equal(FilmDelivery.Sha256(f), FilmDelivery.Sha256(f2));
            string probe = "";
            if (Cli.Find("ffprobe") is { } fp) probe = Cli.Run(fp, ["-v", "error", "-show_entries", "format=format_name", "-of", "compact=p=0:nk=1", f], dir, 30000).Out.Trim();
            if (Environment.GetEnvironmentVariable("TRENDO_MIDI_BELEG") is { Length: > 0 } keep)
            {
                Directory.CreateDirectory(keep); File.Copy(f, Path.Combine(keep, "melodie.mid"), true); File.Copy(f2, Path.Combine(keep, "melodie-wieder.mid"), true);
                File.WriteAllText(Path.Combine(keep, "messung.txt"), $"Noten: {re.Song!.Notes.Count}, Tempo {re.Song.Bpm} BPM, Takt {re.Song.Numerator}/4, Name „{re.Song.Name}“, Warnungen: {re.Warnings.Count}\nffprobe format_name: {(probe.Length > 0 ? probe : "(nicht erkannt/kein ffprobe)")}\n"
                    + string.Join("\n", re.Song.Notes.Select(n => $"{PianoRoll.NoteName(n.Pitch),-4} Start {n.Start,5}  Länge {n.Length,4}  Anschlag {n.Velocity}")) + "\n");
            }
        }
        finally { Directory.Delete(dir, true); }
    }
}
