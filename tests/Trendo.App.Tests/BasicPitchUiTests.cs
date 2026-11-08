using Trendo.App;
using Trendo.Core;
using Xunit;
using Avalonia.Headless.XUnit;

namespace Trendo.App.Tests;

/// <summary>Alle Studios, Runde 1 – basic-pitch in Trendo Musik: „MIDI“ je Spur erkennt mit basic-pitch auch Akkorde (Programm nachgebildet; echter Lauf in reports/alle-studios/basic-pitch-e2e.md).</summary>
public class BasicPitchUi
{
    [AvaloniaFact(DisplayName = "basic-pitch-Oberfläche: Spur-MIDI mit basic-pitch → Akkord als drei gleichzeitige Noten, Meldung nennt basic-pitch, Bericht geschrieben")]
    public async Task Stem_midi_chord()
    {
        var (w, _, root) = Ui.Open(); Directory.CreateDirectory(root);
        string? seen = null;
        try
        {
            const int rate = 22050; var tone = Enumerable.Range(0, rate * 2).Select(i => (float)(0.2 * Math.Sin(2 * Math.PI * 262 * i / rate))).ToArray();
            var song = Path.Combine(root, "lied.wav"); Trendo.Core.Media.AudioFile.WriteWav(new Trendo.Core.Media.AudioBuf(rate, [tone]), song);
            var s = new SongLibrary(w.Core.DataRoot).Add(new MusicSong { Title = "Akkordlied", File = song, Seconds = 2, Bpm = 120 });
            w.StemSplitOverride = (src, dir) =>
            {
                Directory.CreateDirectory(dir);
                foreach (var id in new[] { "gesang", "schlagzeug", "bass", "rest" }) Trendo.Core.Media.AudioFile.WriteWav(new Trendo.Core.Media.AudioBuf(rate, [tone]), Path.Combine(dir, id + ".wav"));
                Trendo.Core.Media.AudioFile.WriteWav(new Trendo.Core.Media.AudioBuf(rate, [tone]), Path.Combine(dir, "begleitung.wav"));
                return Task.FromResult(true);
            };
            var made = await w.MsSplit(s.Id); var gesang = made.First(m => m.Kind == "gesang");
            MainWindow.NotenRunOverride = f => { seen = f; return "{\"werkzeug\":\"basic-pitch 0.4.0\",\"noten\":[{\"tonhoehe\":60,\"start\":0.01,\"ende\":1.94,\"staerke\":0.6},{\"tonhoehe\":64,\"start\":0.01,\"ende\":1.96,\"staerke\":0.6},{\"tonhoehe\":67,\"start\":0.01,\"ende\":1.95,\"staerke\":0.6}]}"; };
            var mid = await w.MsStemMidi(gesang, "bass");
            Assert.NotNull(mid); Assert.True(File.Exists(mid)); Assert.EndsWith("bass.wav", seen);
            Assert.Contains("basic-pitch, 100 % mehrstimmig", w.MsMessage);
            Assert.Contains("| C4 |", File.ReadAllText(Path.Combine(Path.GetDirectoryName(mid!)!, "bass – Unsicherheitsbericht.md")));
            var back = Trendo.Core.Media.PianoRoll.FromMidi(File.ReadAllBytes(mid!)); Assert.True(back.Ok); Assert.Equal([60, 64, 67], back.Song!.Notes.Select(n => n.Pitch).OrderBy(x => x));
        }
        finally { MainWindow.NotenRunOverride = null; w.Close(); }
    }
}
