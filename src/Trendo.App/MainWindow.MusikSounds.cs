using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Trendo.Core;
using Trendo.Core.Media;

namespace Trendo.App;

/// <summary>Trendo Musik V7/V8 wie Suno (07.11.2026): Reiter „Lieder | Sounds | Sprache“ in der Erstellen-Spalte.
/// Sounds: One-Shot oder Loop (Takte, Tempo, Tonart) aus einem kurzen Instrumentalstück der lokalen Musik-KI, zugeschnitten und nahtlos.
/// Sprache: Text wird mit Piper gesprochen, die Musik-KI macht passende Musik darunter, die beim Sprechen leiser wird.</summary>
public partial class MainWindow
{
    string _msKind = "lieder"; string _msSoundDesc = "", _msSoundKey = "", _msSpeechText = "", _msSpeechStyle = ""; bool _msSoundLoop; int _msSoundBpm = 100, _msSoundBars = 4;
    public void MsCreateKind(string kind) { _msKind = kind; if (CurrentView == MS) Show(MS); }
    /// <summary>Für Tests: Piper ersetzen (Text, Ziel-WAV → geklappt).</summary>
    public Func<string, string, Task<bool>>? MsSpeechTtsOverride { get; set; }
    /// <summary>Für Tests: FFmpeg beim Mischen von Sprache und Musik ersetzen.</summary>
    public Func<IReadOnlyList<string>, Cli.Result>? MsSpeechMixOverride { get; set; }

    Control MsKindTabs()
    {
        var row = new StackPanel { Name = "ms-kind-tabs", Orientation = Orientation.Horizontal, Spacing = 2 };
        foreach (var (id, label) in new[] { ("lieder", "Lieder"), ("sounds", "Sounds"), ("sprache", "Sprache") })
        {
            var on = _msKind == id; var k = id;
            var b = new Button { Name = "ms-kind-" + id, Content = label, Padding = new Thickness(12, 4), FontSize = 13, Background = Brushes.Transparent, BorderThickness = new Thickness(0, 0, 0, 2),
                BorderBrush = on ? Accent : Brushes.Transparent, Foreground = on ? Text : Muted, CornerRadius = new CornerRadius(0) };
            b.Click += (_, _) => MsCreateKind(k);
            row.Children.Add(b);
        }
        return row;
    }

    /// <summary>V7: Sound erzeugen – One-Shot oder Loop.</summary>
    public async Task<MusicSong?> MsMakeSound(string description, bool loop, int bpm = 100, int bars = 4, string? key = null)
    {
        if (description.Trim().Length < 3) { _msMsg = "Beschreibe den Sound – z. B. „Glas zerbricht“ oder „funky Basslinie“."; Show(MS); return null; }
        if (loop && bars is not (1 or 2 or 4 or 8)) throw new ArgumentException("Loops haben 1, 2, 4 oder 8 Takte.");
        var secs = loop ? (int)Math.Ceiling(Math.Max(10, MusicSounds.BarsSeconds(bpm, bars) * 2 + 2)) : 10;
        var lang = AceStepClient.Languages[Math.Clamp(_msLang, 0, AceStepClient.Languages.Length - 1)].Code;
        var title = (loop ? "Loop – " : "Sound – ") + (description.Trim().Length > 40 ? description.Trim()[..40] : description.Trim());
        var made = await MusicStudioCreate(new AceStepClient.Job(MusicSounds.SoundPrompt(description, loop, bpm, key), "[Instrumental]", secs, lang, 1, loop ? bpm : null, key is { Length: > 0 } ? key : null, Format: "wav"), title);
        if (made.FirstOrDefault() is not { } m) return null;
        try
        {
            var a = AudioFile.Read(m.File);
            AudioBuf result; string note;
            if (loop) { var (l, real) = MusicSounds.Loop(a, bpm, bars); result = l; note = $"Loop {bars} Takte, {real:0.#} BPM" + (key is { Length: > 0 } ? ", " + KeyDe(key) : ""); }
            else { result = MusicSounds.OneShot(a); note = $"One-Shot {result.Seconds:0.00} s"; }
            var outFile = Path.ChangeExtension(m.File, null) + (loop ? " (Loop).wav" : " (One-Shot).wav");
            AudioFile.WriteWav(result, outFile, 24);
            var raw = Path.Combine(Path.GetDirectoryName(m.File)!, ".roh"); Directory.CreateDirectory(raw);
            File.Move(m.File, Path.Combine(raw, Path.GetFileName(m.File)), true); // Rohstück aufheben, nicht in der Liste
            Songs.Update(m.Id, x => { x.File = outFile; x.Seconds = Math.Round(result.Seconds, 2); x.Kind = "sound"; x.Style = description.Trim() + " · " + note; x.Bpm = loop ? bpm : null; x.Key = loop && key is { Length: > 0 } ? key : null; });
            _msSel = m.Id; _msMsg = $"{note} – im Studio als Spur nutzbar (Lied → Profi → „Im Mischpult öffnen“).";
            return Songs.Get(m.Id);
        }
        catch (InvalidOperationException e) { _msMsg = "Sound nicht brauchbar: " + e.Message + " Das Rohstück liegt in der Bibliothek."; return Songs.Get(m.Id); }
        finally { Show(MS); }
    }

    Control MsSoundsForm()
    {
        var sp = new StackPanel { Name = "ms-sounds", Spacing = 8 };
        sp.Children.Add(P("Einzelne Klänge oder Loops – z. B. „Glas zerbricht“, „Airhorn“ oder „funky Slap-Bass“.", Muted, 12));
        var desc = new TextBox { Name = "ms-sound-desc", Text = _msSoundDesc, Watermark = "Sound beschreiben", AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 60 };
        desc.TextChanged += (_, _) => _msSoundDesc = desc.Text ?? "";
        sp.Children.Add(desc);
        var type = new ComboBox { Name = "ms-sound-type", ItemsSource = new[] { "One-Shot (einzelner Klang)", "Loop (wiederholt sich nahtlos)" }, SelectedIndex = _msSoundLoop ? 1 : 0 };
        type.SelectionChanged += (_, _) => { _msSoundLoop = type.SelectedIndex == 1; Show(MS); };
        sp.Children.Add(type);
        if (_msSoundLoop)
        {
            var bpm = new NumericUpDown { Name = "ms-sound-bpm", Minimum = 60, Maximum = 200, Value = _msSoundBpm, Increment = 1, FormatString = "0", Width = 110 };
            bpm.ValueChanged += (_, _) => _msSoundBpm = (int)(bpm.Value ?? 100);
            var bars = new ComboBox { Name = "ms-sound-bars", ItemsSource = new[] { "1 Takt", "2 Takte", "4 Takte", "8 Takte" }, SelectedIndex = _msSoundBars switch { 1 => 0, 2 => 1, 8 => 3, _ => 2 } };
            bars.SelectionChanged += (_, _) => _msSoundBars = bars.SelectedIndex switch { 0 => 1, 1 => 2, 3 => 8, _ => 4 };
            var key = new ComboBox { Name = "ms-sound-key", Width = 118, ItemsSource = new[] { "Tonart" }.Concat(AceStepClient.Keys.Select(KeyDe)).ToArray(), SelectedIndex = _msSoundKey.Length == 0 ? 0 : 1 + Array.IndexOf(AceStepClient.Keys, _msSoundKey) };
            key.SelectionChanged += (_, _) => _msSoundKey = key.SelectedIndex <= 0 ? "" : AceStepClient.Keys[key.SelectedIndex - 1];
            sp.Children.Add(new WrapPanel { Children = { new TextBlock { Text = "BPM", Foreground = Muted, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) }, bpm, new Border { Width = 8 }, bars, new Border { Width = 8 }, key } });
        }
        var go = new Button { Name = "ms-sound-create", Content = "Sound erzeugen", FontSize = 15, FontWeight = FontWeight.SemiBold, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Center,
            Padding = new Thickness(0, 10), CornerRadius = new CornerRadius(12), Background = Accent, Foreground = Brushes.White, BorderThickness = new Thickness(0) };
        go.Click += (_, _) => { _msSoundDesc = desc.Text ?? ""; try { _ = MsMakeSound(_msSoundDesc, _msSoundLoop, _msSoundBpm, _msSoundBars, _msSoundKey); } catch (ArgumentException e) { _msMsg = e.Message; Show(MS); } };
        sp.Children.Add(go);
        if (_msMsg.Length > 0) sp.Children.Add(new TextBlock { Name = "ms-msg", Text = _msMsg, Foreground = Text, FontSize = 12.5, TextWrapping = TextWrapping.Wrap });
        return sp;
    }

    /// <summary>V8: Sprache mit Musik – sprechen (Piper), Musik passend lang erzeugen, mischen (Musik wird beim Sprechen leiser).</summary>
    public async Task<MusicSong?> MsMakeSpeech(string text, string style)
    {
        if (text.Trim().Length < 3) { _msMsg = "Schreib den Text, der gesprochen werden soll."; Show(MS); return null; }
        var dir = WorkFolder("Musik"); Directory.CreateDirectory(dir);
        var stem = DevStudio.SafeName("Sprache – " + (text.Trim().Length > 30 ? text.Trim()[..30] : text.Trim()));
        var voice = Path.Combine(dir, $".{stem}-stimme.wav");
        bool spoke;
        if (MsSpeechTtsOverride is { } o) spoke = await o(text, voice);
        else
        {
            var model = KiLibrary.Models.FirstOrDefault(m => m.Kind == KiKind.TextZuSprache && KiStore.ModelReady(m));
            if (model is null || !KiStore.RuntimeReady(KiRuntime.Piper)) { _msMsg = "Zum Sprechen eine Stimme in der KI-Bibliothek laden (Bereich „Text zu Sprache“)."; Show(MS); return null; }
            if (_opt.Isolated) { _msMsg = "Im abgeschotteten Testmodus wird nicht gesprochen."; Show(MS); return null; }
            _msMsg = "Der Text wird gesprochen …"; Show(MS);
            var r = await Task.Run(() => RunWithInput(KiStore.RuntimeExe(KiRuntime.Piper), ["--model", KiStore.ModelFile(model, model.Files[0]), "--output_file", voice], text));
            spoke = r.Ok;
        }
        if (!spoke || !File.Exists(voice)) { _msMsg = "Sprechen ging nicht – nichts erzeugt."; Show(MS); return null; }
        var vsec = AudioFile.Read(voice).Seconds;
        var lang = AceStepClient.Languages[Math.Clamp(_msLang, 0, AceStepClient.Languages.Length - 1)].Code;
        var made = await MusicStudioCreate(new AceStepClient.Job(MusicSounds.SpeechMusicPrompt(style), "[Instrumental]", (int)Math.Ceiling(Math.Max(10, vsec + 5.5)), lang, 1, Format: "wav"), stem);
        if (made.FirstOrDefault() is not { } m) { File.Delete(voice); return null; }
        var outFile = Path.Combine(dir, stem + ".wav"); for (var n = 2; File.Exists(outFile); n++) outFile = Path.Combine(dir, $"{stem} {n}.wav");
        var args = MusicSounds.SpeechMixArgs(voice, m.File, outFile, vsec);
        var mix = MsSpeechMixOverride is { } mo ? mo(args) : FfmpegPath is { } ff ? await Task.Run(() => Cli.Run(ff, args, dir, 600_000)) : new Cli.Result(1, "", "FFmpeg fehlt – unter Zusatzprogramme einrichten.");
        if (!mix.Ok || !File.Exists(outFile)) { _msMsg = "Mischen ging nicht: " + (mix.Err.Trim().Split('\n').LastOrDefault() ?? ""); Show(MS); return null; }
        var raw = Path.Combine(dir, ".roh"); Directory.CreateDirectory(raw);
        File.Move(m.File, Path.Combine(raw, Path.GetFileName(m.File)), true); File.Move(voice, Path.Combine(raw, Path.GetFileName(voice)), true);
        Songs.Update(m.Id, x => { x.File = outFile; x.Seconds = Math.Round(2.0 + vsec + 3.0, 2); x.Kind = "sprache"; x.Lyrics = text.Trim(); x.Style = "Sprache mit Musik · " + (style.Trim().Length > 0 ? style.Trim() : "ruhige Hintergrundmusik"); });
        _msSel = m.Id; _msMsg = $"Sprache mit Musik fertig ({SongTime(2.0 + vsec + 3.0)}) – die Musik wird beim Sprechen leiser."; Show(MS);
        return Songs.Get(m.Id);
    }

    Control MsSpeechForm()
    {
        var sp = new StackPanel { Name = "ms-speech", Spacing = 8 };
        sp.Children.Add(P("Text sprechen lassen – mit passender Musik darunter (Hörbuch, Rede, Gute-Nacht-Geschichte).", Muted, 12));
        var text = new TextBox { Name = "ms-speech-text", Text = _msSpeechText, Watermark = "Was gesprochen werden soll", AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 110 };
        text.TextChanged += (_, _) => _msSpeechText = text.Text ?? "";
        var style = new TextBox { Name = "ms-speech-style", Text = _msSpeechStyle, Watermark = "Musik dazu, z. B. „sanftes Klavier“ oder „epische Trommeln“" };
        style.TextChanged += (_, _) => _msSpeechStyle = style.Text ?? "";
        sp.Children.Add(text); sp.Children.Add(style);
        var go = new Button { Name = "ms-speech-create", Content = "Sprache mit Musik", FontSize = 15, FontWeight = FontWeight.SemiBold, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Center,
            Padding = new Thickness(0, 10), CornerRadius = new CornerRadius(12), Background = Accent, Foreground = Brushes.White, BorderThickness = new Thickness(0) };
        go.Click += (_, _) => { _msSpeechText = text.Text ?? ""; _msSpeechStyle = style.Text ?? ""; _ = MsMakeSpeech(_msSpeechText, _msSpeechStyle); };
        sp.Children.Add(go);
        if (_msMsg.Length > 0) sp.Children.Add(new TextBlock { Name = "ms-msg", Text = _msMsg, Foreground = Text, FontSize = 12.5, TextWrapping = TextWrapping.Wrap });
        return sp;
    }
}
