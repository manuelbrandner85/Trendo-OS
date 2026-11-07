using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Trendo.App;
using Trendo.Core.Media;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// <summary>Paket 01 – Studio 4, S4-20 (06.11.2026): Prüfen und Freigeben im Audioeditor.</summary>
public class AudioQa_Ui
{
    static string Tone(string dir, string name, double seconds, double amp, bool clamp = false)
    {
        const int rate = 48000; var n = (int)(seconds * rate);
        var d = Enumerable.Range(0, 2).Select(_ => Enumerable.Range(0, n).Select(i => (float)Math.Clamp(amp * Math.Sin(2 * Math.PI * 440 * i / rate), clamp ? -1 : -9, clamp ? 1 : 9)).ToArray()).ToArray();
        var p = Path.Combine(dir, name); AudioFile.WriteWav(new AudioBuf(rate, d), p, 24); return p;
    }

    [AvaloniaFact(DisplayName = "S4-20 Audioeditor: ohne Lizenz und bei Übersteuerung keine Freigabe (Grund steht da, kein Ordner); mit Angabe Freigabe mit WAV 24 Bit und Prüfbericht")]
    public void Qa_blocks_then_releases()
    {
        var (w, _, root) = Ui.Open(); w.Width = 1600; w.Height = 1000; Directory.CreateDirectory(root);
        try
        {
            w.Show("ton"); Settle(); RealClick(w, V<Button>(w, "prog-tpl-leer")); Settle();
            w.AuOpen(Tone(root, "laut.wav", 2, 1.6, clamp: true)); w.Show("ton"); Settle();
            w.Find<TextBox>("au-license").Text = "eigene Aufnahme";
            var rel = w.Find<Button>("au-release-run"); rel.BringIntoView(); Settle(); rel.Press(); Settle();
            Assert.True(w.AuQaLast!.Blocked); Assert.Contains("Übersteuert", w.ProgramMessage("ton"));
            Assert.Empty(Directory.GetDirectories(root, "Freigabe*", SearchOption.AllDirectories));
            // saubere Aufnahme, aber ohne Lizenz
            w.AuOpen(Tone(root, "sauber.wav", 3, 0.25)); w.Show("ton"); Settle();
            var d = w.ProgramActive("ton")!; d.Edit("leer", x => x["license"] = ""); w.Show("ton"); Settle();
            rel = w.Find<Button>("au-release-run"); rel.BringIntoView(); Settle(); rel.Press(); Settle();
            Assert.Contains("Lizenz fehlen", w.ProgramMessage("ton"));
            // KI-Stimme ohne Einwilligung
            w.Find<TextBox>("au-license").Text = "eigene Aufnahme"; w.Find<CheckBox>("au-voice").IsChecked = true;
            rel = w.Find<Button>("au-release-run"); rel.Press(); Settle();
            Assert.Contains("Einwilligung", w.ProgramMessage("ton"));
            w.Find<CheckBox>("au-consent").IsChecked = true; w.Find<ComboBox>("au-qa-target").SelectedIndex = 1;
            rel = w.Find<Button>("au-release-run"); rel.Press(); Settle();
            Assert.Contains("Freigegeben", w.ProgramMessage("ton"));
            var folder = Directory.GetDirectories(root, "Freigabe*", SearchOption.AllDirectories).Single();
            var wav = Directory.GetFiles(folder, "*.wav").Single();
            Assert.Equal(24, AudioQa.WavBits(File.ReadAllBytes(wav)));
            var rep = File.ReadAllText(Path.Combine(folder, "Prüfbericht.md"));
            Assert.Contains("Sprache / Podcast", rep); Assert.Contains(FilmDelivery.Sha256(wav), rep); Assert.Contains("Einwilligung liegt vor", rep);
            Assert.Equal("podcast", w.ProgramActive("ton")!.Text("qaTarget"));
        }
        finally { w.Close(); }
    }
}

/// <summary>Paket 01 – Studio 4, S4-06 (06.11.2026): Plugins prüfen ohne Laden.</summary>
public class AudioPlugins_Ui
{
    [AvaloniaFact(DisplayName = "S4-06 Audioeditor: „Plugins prüfen“ findet VST3 und schreibt einen Bericht – ohne Plugin zu laden; leerer Ordner: klare Meldung")]
    public void Scan()
    {
        var (w, _, root) = Ui.Open(); Directory.CreateDirectory(root);
        try
        {
            var vst = Path.Combine(root, "VST3", "Hall.vst3", "Contents", "Resources"); Directory.CreateDirectory(vst);
            File.WriteAllText(Path.Combine(vst, "moduleinfo.json"), """{ "Name": "Hall", "Version": "1.0", "Factory Info": { "Vendor": "Beispiel Audio" } }""");
            Directory.CreateDirectory(Path.Combine(root, "VST3", "Hall.vst3", "Contents", "x86_64-win"));
            w.PluginRootsOverride = [Path.Combine(root, "VST3")];
            w.Show("ton"); Settle(); RealClick(w, V<Button>(w, "prog-tpl-leer")); Settle();
            var f = w.AuPluginScan(); Settle();
            Assert.Single(w.AuPluginsLast!); Assert.Contains("gesperrt", w.ProgramMessage("ton"));
            Assert.Contains("| VST3 | Hall | Beispiel Audio | 1.0 |", File.ReadAllText(f));
            w.PluginRootsOverride = [Path.Combine(root, "leer")]; w.AuPluginScan();
            Assert.Contains("Keine VST3", w.ProgramMessage("ton"));
        }
        finally { w.Close(); }
    }
}

/// <summary>Paket 01 – Studio 4, S4-19 (07.11.2026): Melodie → Noten im Audioeditor.</summary>
public class AudioMidi_Ui
{
    static string Wav(string dir, string name, int[] pitches, bool chord)
    {
        const int rate = 44100; var seg = (int)(0.4 * rate); var n = chord ? rate : pitches.Length * seg; var x = new float[n];
        for (var i = 0; i < n; i++)
        {
            var t = i / (double)rate;
            x[i] = chord ? (float)pitches.Sum(p => 0.25 * Math.Sin(2 * Math.PI * 440 * Math.Pow(2, (p - 69) / 12.0) * t))
                : (float)(0.5 * Math.Sin(2 * Math.PI * 440 * Math.Pow(2, (pitches[Math.Min(pitches.Length - 1, i / seg)] - 69) / 12.0) * t) * Math.Min(1, (i % seg) / 400.0) * Math.Min(1, (seg - i % seg) / 400.0));
        }
        var f = Path.Combine(dir, name); AudioFile.WriteWav(new AudioBuf(rate, [x, (float[])x.Clone()]), f); return f;
    }

    [AvaloniaFact(DisplayName = "S4-19 Audioeditor: Melodie → Noten schreibt MIDI und Unsicherheitsbericht, legt die Noten in Trendo Musik; Akkord wird als NICHT exakt gemeldet")]
    public void Melody_to_midi()
    {
        var (w, _, root) = Ui.Open(); Directory.CreateDirectory(root);
        try
        {
            w.Show("ton"); Settle(); RealClick(w, V<Button>(w, "prog-tpl-leer")); Settle();
            w.AuOpen(Wav(root, "melodie.wav", [60, 62, 64, 65], false)); w.Show("ton"); Settle();
            var mid = w.AuMelodyToMidi(w.ProgramActive("ton")!);
            Assert.True(File.Exists(mid)); Assert.True(w.AuMidiLast!.Exact, w.AuMidiLast.Verdict);
            Assert.Equal([60, 62, 64, 65], PianoRoll.FromMidi(File.ReadAllBytes(mid)).Song!.Notes.Select(n => n.Pitch));
            Assert.Equal([60, 62, 64, 65], w.MsRoll.Notes.Select(n => n.Pitch)); // liegt zum Prüfen in Trendo Musik
            Assert.True(File.Exists(Path.Combine(Path.GetDirectoryName(mid)!, "melodie – Unsicherheitsbericht.md")));
            w.AuOpen(Wav(root, "akkord.wav", [57, 60, 64], true)); w.Show("ton"); Settle();
            w.AuMelodyToMidi(w.ProgramActive("ton")!);
            Assert.False(w.AuMidiLast!.Exact); Assert.Contains("NICHT exakt", w.ProgramMessage("ton"));
        }
        finally { w.Close(); }
    }
}

/// <summary>Paket 01 – Studio 4, S4-09 (07.11.2026): Busse im Mehrspur-Mischpult.</summary>
public class AudioBuses_Ui
{
    [AvaloniaFact(DisplayName = "S4-09 Mischpult: Bus anlegen, Spur darauf leiten, EQ übernehmen (rückgängig machbar); Routing-Kreis über die Auswahl wird abgewiesen")]
    public void Buses()
    {
        var (w, _, root) = Ui.Open(); w.Width = 1600; w.Height = 1000; Directory.CreateDirectory(root);
        try
        {
            string Wav(string n, double hz) { var x = Enumerable.Range(0, 44100).Select(i => (float)(0.3 * Math.Sin(2 * Math.PI * hz * i / 44100))).ToArray(); var f = Path.Combine(root, n); AudioFile.WriteWav(new AudioBuf(44100, [x, (float[])x.Clone()]), f); return f; }
            w.Show("ton"); Settle(); RealClick(w, V<Button>(w, "prog-tpl-mehrspur")); Settle();
            w.Find<TextBox>("au-file").Text = Wav("musik.wav", 110); w.Find<Button>("au-add-track").Press(); Settle();
            w.Find<TextBox>("au-file").Text = Wav("stimme.wav", 800); w.Find<Button>("au-add-track").Press(); Settle();
            w.Find<TextBox>("au-bus-name").Text = "Musik"; w.Find<Button>("au-bus-add").Press(); Settle();
            w.Find<TextBox>("au-bus-name").Text = "Hall"; w.Find<Button>("au-bus-add").Press(); Settle();
            var d = w.ProgramActive("ton")!; var buses = MixRouting.Buses(d.Data); Assert.Equal(["Musik", "Hall"], buses.Select(b => b.Name));
            w.Find<ComboBox>("au-track-out-0").SelectedIndex = 1; Settle(); // Musik-Spur → Bus „Musik“
            Assert.Equal(buses[0].Id, MixRouting.TrackOut(w.ProgramActive("ton")!.Data, AudioMulti.Tracks(d.Data)[0].Id));
            w.Find<Slider>("au-bus-low-0").Value = -12; w.Find<Button>("au-bus-apply-0").Press(); Settle();
            d = w.ProgramActive("ton")!;
            Assert.Equal(-12, MixRouting.Buses(d.Data)[0].LowDb); Assert.Equal("Bus einstellen", d.UndoLabel);
            // Bus „Musik“ → „Hall“, dann „Hall“ → „Musik“: Kreis
            w.Find<ComboBox>("au-bus-out-0").SelectedIndex = 1; w.Find<Button>("au-bus-apply-0").Press(); Settle();
            Assert.Equal(buses[1].Id, MixRouting.Buses(w.ProgramActive("ton")!.Data)[0].Out);
            var before = w.ProgramActive("ton")!.Data.ToJsonString();
            w.Find<ComboBox>("au-bus-out-1").SelectedIndex = 1; w.Find<Button>("au-bus-apply-1").Press(); Settle();
            Assert.Contains("Routing-Kreis", w.ProgramMessage("ton"));
            Assert.Equal(before, w.ProgramActive("ton")!.Data.ToJsonString());
        }
        finally { w.Close(); }
    }
}

/// <summary>Paket 01 – Studio 4, S4-01 (07.11.2026): Mehrspur – Aufnahme als neue Spur, Automation, ohne Gerät keine Scheinaufnahme.</summary>
public class AudioMultiRecord_Ui
{
    [AvaloniaFact(DisplayName = "S4-01 Mehrspur: Aufnahme landet als neue Spur, Automation per Feld (rückgängig machbar, Unsinn abgelehnt); ohne Mikrofon keine Scheinaufnahme")]
    public async Task Record_and_automate()
    {
        var (w, _, root) = Ui.Open(); w.Width = 1600; w.Height = 1000; Directory.CreateDirectory(root);
        try
        {
            string Wav(string n) { var x = Enumerable.Range(0, 44100 * 2).Select(i => (float)(0.3 * Math.Sin(2 * Math.PI * 300 * i / 44100.0))).ToArray(); var f = Path.Combine(root, n); AudioFile.WriteWav(new AudioBuf(44100, [x, (float[])x.Clone()]), f); return f; }
            w.Show("ton"); Settle(); RealClick(w, V<Button>(w, "prog-tpl-mehrspur")); Settle();
            w.Find<TextBox>("au-file").Text = Wav("musik.wav"); w.Find<Button>("au-add-track").Press(); Settle();
            // ohne Mikrofon: keine Spur, klare Meldung
            w.MicRecordOverride = _ => Task.FromResult<string?>(null);
            await w.AuRecord(w.ProgramActive("ton")!, 3); Settle();
            Assert.Single(AudioMulti.Tracks(w.ProgramActive("ton")!.Data));
            // mit Aufnahme: neue Spur
            w.MicRecordOverride = _ => Task.FromResult<string?>(Wav("mic.wav"));
            await w.AuRecord(w.ProgramActive("ton")!, 2); Settle();
            Assert.Equal(2, AudioMulti.Tracks(w.ProgramActive("ton")!.Data).Count);
            // Automation
            w.Show("ton"); Settle();
            w.Find<TextBox>("au-auto-0").Text = "0:0, 1:-12"; var ok = w.Find<Button>("au-auto-apply-0"); ok.BringIntoView(); Settle(); ok.Press(); Settle();
            var d = w.ProgramActive("ton")!; var id = AudioMulti.Tracks(d.Data)[0].Id;
            Assert.Equal([(0.0, 0.0), (1.0, -12.0)], AudioMulti.Automation(d.Data, id)); Assert.Equal("Automation", d.UndoLabel);
            w.Find<TextBox>("au-auto-0").Text = "lauter bitte"; w.Find<Button>("au-auto-apply-0").Press(); Settle();
            Assert.Contains("verstehe ich nicht", w.ProgramMessage("ton"));
            Assert.Equal(2, AudioMulti.Automation(w.ProgramActive("ton")!.Data, id).Count);
            w.ProgramActive("ton")!.Undo(); Assert.Empty(AudioMulti.Automation(w.ProgramActive("ton")!.Data, id));
        }
        finally { w.Close(); }
    }
}
