using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Trendo.App;
using Trendo.Core;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>Trendo Musik S1: eigene Stimme per Mikrofon analysieren und fürs nächste Lied übernehmen.</summary>
[Collection("Claude")]
public class MusikStimmeUiTests
{
    static async Task Pump() { for (var i = 0; i < 8; i++) { Dispatcher.UIThread.RunJobs(); await Task.Delay(10); } }

    const string Bariton = """{"stimme": {"low": "Bb2", "high": "D4", "comfy_low": "C3", "comfy_high": "C4", "semitones": 16, "voice": "Bariton", "color": "warm", "breath": "klar", "keys": ["D", "Eb", "C#"], "tags": "baritone male vocals, warm voice, clear", "seconds": 12.0}}""";

    [AvaloniaFact(DisplayName = "S1: 20 s aufnehmen → Analyse → Profil „Erika“ mit Zusammenfassung, Aufnahme wird gelöscht; „Für mein nächstes Lied“ setzt Stimme, Tonlage, Tonart und Stimmklang")]
    public async Task Record_and_use()
    {
        var (w, _, root) = Ui.Open();
        string? recorded = null; int? secs = null;
        w.MicRecordOverride = s => { secs = s; recorded = Path.Combine(root, "aufnahme.wav"); File.WriteAllBytes(recorded, [1, 2, 3]); return Task.FromResult<string?>(recorded); };
        w.VoiceAnalyseOverride = f => Task.FromResult<string?>(f == recorded ? Bariton : null);
        try
        {
            w.MusicStudioProMode(true); w.Show("musik-studio"); await Pump();
            w.Find<TextBox>("ms-voice-name").Text = "Erika"; await Pump();
            w.Find<Button>("ms-voice-record").Press();
            for (var i = 0; i < 50 && w.Voices.All().Count == 0; i++) await Pump();
            Assert.Equal(20, secs);
            var p = Assert.Single(w.Voices.All());
            Assert.Equal(("Erika", "Bariton", 16), (p.Name, p.Analysis!.Voice, p.Analysis.Semitones));
            Assert.False(File.Exists(recorded)); // nur das Ergebnis bleibt
            await Pump();
            Assert.Contains("bequem C3–C4", w.Find<TextBlock>("ms-voice-summary-" + p.Id).Text);
            Assert.Contains("passende Tonarten D, Es, Cis", w.Find<TextBlock>("ms-voice-summary-" + p.Id).Text);
            w.Find<Button>("ms-voice-use-" + p.Id).Press(); await Pump();
            Assert.Equal("Männerstimme", w.Find<ComboBox>("ms-voice").SelectedItem);
            Assert.Equal("Tonlage mittel", w.Find<ComboBox>("ms-range").SelectedItem);
            Assert.Contains("baritone male vocals, warm voice", w.Find<TextBox>("ms-style").Text);
            Assert.Contains("D-Dur", w.Find<TextBlock>("ms-msg").Text);
            // noch einmal aufnehmen ersetzt das Profil, Löschen entfernt es
            await w.MsAnalyseVoice("erika");
            Assert.Single(w.Voices.All());
            w.Find<Button>("ms-voice-del-" + p.Id).Press(); await Pump();
            Assert.Empty(w.Voices.All());
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "S1: zu wenig gesungen → ehrlicher Hinweis, kein Profil")]
    public async Task Too_quiet()
    {
        var (w, _, root) = Ui.Open();
        w.MicRecordOverride = _ => { var f = Path.Combine(root, "leise.wav"); File.WriteAllBytes(f, [1]); return Task.FromResult<string?>(f); };
        w.VoiceAnalyseOverride = _ => Task.FromResult<string?>("""{"stimme": null, "grund": "zu wenig gesungen – bitte lauter oder länger singen"}""");
        try
        {
            w.MusicStudioProMode(true); w.Show("musik-studio"); await Pump();
            Assert.Null(await w.MsAnalyseVoice("Ich")); await Pump();
            Assert.Contains("zu wenig gesungen", w.Find<TextBlock>("ms-msg").Text);
            Assert.Empty(w.Voices.All());
        }
        finally { w.Close(); }
    }
}
