using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Trendo.Core;

namespace Trendo.App;

/// <summary>Trendo Musik S1 (freigegeben 03.11.2026): eigene Stimme per Mikrofon analysieren (20 s singen) – Umfang, bequemer Bereich, Stimmlage,
/// Klangfarbe, Hauchanteil und passende Tonarten; „Für mein nächstes Lied“ setzt Stimme, Tonlage, Tonart und Stimmklang. Profile liegen nur auf diesem PC.</summary>
public partial class MainWindow
{
    /// <summary>Tests: Mikrofon-Aufnahme ersetzen (Sekunden → WAV-Datei oder null).</summary>
    public Func<int, Task<string?>>? MicRecordOverride { get; set; }
    /// <summary>Tests: Stimmanalyse ersetzen (WAV → JSON).</summary>
    public Func<string, Task<string?>>? VoiceAnalyseOverride { get; set; }
    public VoiceProfiles Voices => new(_core.DataRoot);
    string _msVoiceName = "Ich"; bool _msRecording; const int VoiceSeconds = 20;

    /// <summary>Mikrofon für <paramref name="seconds"/> aufnehmen (FFmpeg, 44,1 kHz mono) – oder null mit Hinweis.</summary>
    async Task<string?> RecordMic(int seconds)
    {
        if (MicRecordOverride is { } o) return await o(seconds);
        if (_opt.Isolated) { _msMsg = "Testmodus: es wird nichts aufgenommen."; return null; }
        if (FfmpegPath is not { } ff || DesktopCapture.Devices(ff).Microphones.FirstOrDefault() is not { } mic) { _msMsg = "Dafür braucht es ein Mikrofon und FFmpeg (unter „Programme“ einrichten)."; return null; }
        var wav = Path.Combine(Path.GetTempPath(), $"trendo-stimme-{Guid.NewGuid():N}.wav");
        var r = await Task.Run(() => Cli.Run(ff, ["-hide_banner", "-nostats", "-y", "-f", "dshow", "-i", "audio=" + mic.Id, "-t", seconds.ToString(System.Globalization.CultureInfo.InvariantCulture),
            "-ac", "1", "-ar", "44100", "-c:a", "pcm_s16le", wav], null, (seconds + 20) * 1000));
        if (!r.Ok || !File.Exists(wav)) { TryDelete(wav); _msMsg = "Aufnahme ging nicht: " + Short(r.Err); return null; }
        return wav;
    }

    /// <summary>S1: Stimme aufnehmen (oder eine Datei nehmen) und analysieren; das Profil wird unter <paramref name="name"/> gespeichert.</summary>
    public async Task<VoiceProfile?> MsAnalyseVoice(string name, string? file = null)
    {
        _msRecording = true; _msMsg = file is null ? $"Aufnahme läuft ({VoiceSeconds} s): sing langsam von deinem tiefsten bis zu deinem höchsten Ton, dann ein Stück eines Lieds …" : "Die Stimme wird untersucht …";
        if (CurrentView == MS) Show(MS);
        var wav = file; var own = false;
        try
        {
            if (wav is null) { wav = await RecordMic(VoiceSeconds); own = true; }
            if (wav is null) return null;
            _msMsg = "Die Stimme wird untersucht …"; if (CurrentView == MS) Show(MS);
            string? json = null;
            if (VoiceAnalyseOverride is { } o) json = await o(wav);
            else if (!AnalyseReady) { _msMsg = "Zuerst die Song-Analyse einrichten (bei einem Lied rechts, einmalig ca. 300 MB)."; return null; }
            else
            {
                var outFile = Path.Combine(Path.GetTempPath(), $"trendo-stimme-{Guid.NewGuid():N}.json");
                var r = await KiPyRun(KiRuntime.MusikAnalyse, ["--stimme", wav, outFile]);
                if (r.Ok && File.Exists(outFile)) { json = await File.ReadAllTextAsync(outFile); TryDelete(outFile); }
                else _msMsg = "Analyse ging nicht: " + Short(r.Err.Length > 0 ? r.Err : r.Out);
            }
            if (json is null) return null;
            var (a, why) = VoiceAnalysis.Parse(json);
            if (a is null) { _msMsg = "Keine Stimme erkannt: " + why + "."; return null; }
            var p = Voices.Upsert(name, a);
            _msMsg = $"Stimme von „{p.Name}“: {a.Summary}.";
            _core.Audit.Append(Principal.LocalOwner.Name, "musik.stimme.analyse", p.Id); // keine Aufnahme gespeichert, nur das Ergebnis
            return p;
        }
        finally
        {
            if (own && wav is not null) TryDelete(wav);
            _msRecording = false; if (CurrentView == MS) Show(MS);
        }
    }

    /// <summary>Stimmprofil fürs nächste Lied übernehmen: Männer-/Frauenstimme, Tonlage, passendste Tonart (Dur) und Stimmklang.</summary>
    public void MsUseVoice(string profileId)
    {
        if (Voices.All().FirstOrDefault(x => x.Id == profileId) is not { Analysis: { } a } p) return;
        _msPro = true;
        _msVoice = a.Female ? "Frauenstimme" : "Männerstimme"; _msRange = a.Range;
        if (a.Keys.Count > 0 && AceStepClient.Keys.Contains(a.Keys[0] + " major")) _msKey = a.Keys[0] + " major";
        var have = _msStyle.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var add = a.Tags.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Where(t => !have.Contains(t)).ToList();
        if (add.Count > 0) _msStyle = (_msStyle.Trim().Length > 0 ? _msStyle.TrimEnd(' ', ',') + ", " : "") + string.Join(", ", add);
        _msMsg = $"Stimme von „{p.Name}“ übernommen: {(a.Female ? "Frauenstimme" : "Männerstimme")}, Tonlage {a.Range}, Tonart {SongTools.KeyDe(_msKey)}. Die Musik-KI singt mit eigener Stimme in dieser Lage – mit deiner echten Stimme geht das später mit dem eigenen Stimmmodell.";
        Show(MS);
    }

    Control MsVoicePanel(Action sync)
    {
        var sp = new StackPanel { Name = "ms-voice-panel", Spacing = 4 };
        sp.Children.Add(H("Meine Stimme", 13));
        sp.Children.Add(P("Sing 20 Sekunden: langsam vom tiefsten bis zum höchsten Ton, dann ein Stück eines Lieds. Trendo findet deine Lage und die Tonarten, die dir liegen. Gespeichert wird nur das Ergebnis, nicht die Aufnahme.", Muted, 11));
        var name = new TextBox { Name = "ms-voice-name", Text = _msVoiceName, Width = 120, Watermark = "Name" };
        name.TextChanged += (_, _) => _msVoiceName = name.Text ?? "";
        var rec = Act("ms-voice-record", _msRecording ? "Aufnahme läuft …" : "Stimme aufnehmen", () => { sync(); _ = MsAnalyseVoice(_msVoiceName); });
        rec.IsEnabled = !_msRecording;
        var fromFile = Act("ms-voice-file", "Aus Datei", async () =>
        {
            sync();
            if (await PickMedia("ms-voice-file", ["*.wav", "*.mp3", "*.flac", "*.m4a"]) is { } f) _ = MsAnalyseVoice(_msVoiceName, f);
        });
        sp.Children.Add(new WrapPanel { Children = { name, new Border { Width = 6 }, rec, new Border { Width = 6 }, fromFile } });
        foreach (var p in Voices.All().Where(x => x.Analysis is not null))
        {
            var id = p.Id;
            var row = new StackPanel { Name = "ms-voice-" + id, Spacing = 2, Margin = new Thickness(0, 4, 0, 0) };
            row.Children.Add(new TextBlock { Text = p.Name, Foreground = Text, FontSize = 12.5, FontWeight = FontWeight.SemiBold });
            row.Children.Add(new TextBlock { Name = "ms-voice-summary-" + id, Text = p.Analysis!.Summary, Foreground = Muted, FontSize = 11, TextWrapping = TextWrapping.Wrap });
            row.Children.Add(new WrapPanel { Children = {
                Act("ms-voice-use-" + id, "Für mein nächstes Lied", () => { sync(); MsUseVoice(id); }),
                new Border { Width = 6 },
                Act("ms-voice-del-" + id, "Löschen", () => { Voices.Remove(id); _core.Audit.Append(Principal.LocalOwner.Name, "musik.stimme.geloescht", id); Show(MS); }) } });
            sp.Children.Add(row);
        }
        return sp;
    }
}
