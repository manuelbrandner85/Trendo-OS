using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Trendo.Core;
using Trendo.Core.Media;

namespace Trendo.App;

/// <summary>Trendo Musik V1/V2/V7 (freigegeben 07.11.2026, „Alle ja“): nach dem Hochladen gleich trennen, Spuren-Mischer im Lied (Gesang, Schlagzeug, Bass, Rest
/// mit Pegel, Stumm, Solo – abgespielt in Trendo selbst), Trennung schnell (htdemucs) oder genau (htdemucs_ft) mit Qualitätsmessung.</summary>
public partial class MainWindow
{
    /// <summary>Für Tests: basic-pitch nachbilden (Eingabedatei → JSON wie trendo_noten.py).</summary>
    public static Func<string, string>? NotenRunOverride { get; set; }
    bool NotenReady => NotenRunOverride is not null || KiStore.RuntimeReady(KiRuntime.Noten);
    public string MsMessage => _msMsg;
    /// <summary>V1: nach „＋ Eigene Aufnahme“ gleich in Gesang und Begleitung trennen.</summary>
    public bool MsAutoSplit { get; set; } = true;
    /// <summary>V7: „htdemucs“ (schnell) oder „htdemucs_ft“ (genauer, etwa 4× langsamer).</summary>
    public string MsStemModel { get; set; } = "htdemucs";
    readonly Dictionary<string, Dictionary<string, StemTools.Channel>> _msStemMix = [];
    /// <summary>Ergebnis der letzten Qualitätsmessung (Rekonstruktion dB, Übersprechen).</summary>
    public (double Reconstruction, double Bleed)? MsStemQuality { get; private set; }

    /// <summary>Spuren-Ordner eines Lieds (oder seiner Spur), falls getrennt.</summary>
    public string? MsStemDir(MusicSong s)
    {
        var (_, voc) = StemsOf(s);
        return voc is not null && File.Exists(voc.File) ? Path.GetDirectoryName(voc.File) : null;
    }

    static string MsStemRoot(MusicSong s) => s.Kind is "begleitung" or "gesang" && s.ParentId is { } p ? p : s.Id;

    Dictionary<string, StemTools.Channel> MsMixOf(MusicSong s) =>
        _msStemMix.TryGetValue(MsStemRoot(s), out var m) ? m : _msStemMix[MsStemRoot(s)] = [];

    public void MsStemSet(MusicSong s, string stem, StemTools.Channel ch) { MsMixOf(s)[stem] = ch; if (CurrentView == MS) Show(MS); }

    /// <summary>Mischung rendern (Trendo selbst, ohne fremden Player-Fenster) und abspielen.</summary>
    public string? MsStemPlay(MusicSong s)
    {
        var dir = MsStemDir(s); if (dir is null) { _msMsg = "Erst „Spuren trennen“."; return null; }
        var doc = StemTools.Project(StemTools.Files(dir), MsMixOf(s));
        var mixDir = Path.Combine(WorkFolder("Musik"), ".mischung"); Directory.CreateDirectory(mixDir);
        var wav = Path.Combine(mixDir, $"{MsStemRoot(s)}-{DateTime.Now:HHmmssfff}.wav");
        AudioFile.WriteWav(AudioMulti.Mix(doc), wav, 16);
        MusicPlay([wav], 0, MS);
        _msMsg = "Spielt deine Mischung: " + string.Join(", ", StemTools.Stems.Where(x => StemTools.Files(dir).ContainsKey(x.Id)).Select(x =>
        {
            var c = MsMixOf(s).GetValueOrDefault(x.Id) ?? new StemTools.Channel();
            return $"{x.Label} {(c.Mute ? "stumm" : c.Solo ? "Solo" : $"{c.GainDb:+0;-0;0} dB")}";
        })) + ".";
        return wav;
    }

    /// <summary>Mischung als eigenes Lied speichern.</summary>
    public MusicSong? MsStemSave(MusicSong s)
    {
        var dir = MsStemDir(s); if (dir is null) return null;
        var root = Songs.Get(MsStemRoot(s)) ?? s;
        var a = AudioMulti.Mix(StemTools.Project(StemTools.Files(dir), MsMixOf(s)));
        var f = Path.Combine(dir, $"mischung-{DateTime.Now:HHmmss}.wav"); AudioFile.WriteWav(a, f, 24);
        var song = Songs.Add(new MusicSong { Title = root.Title + " – eigene Mischung", Style = root.Style, Lyrics = root.Lyrics, Language = root.Language, Seconds = Math.Round(a.Seconds, 2),
            Bpm = root.Bpm, Key = root.Key, TimeSignature = root.TimeSignature, File = f, Kind = "mischung", ParentId = root.Id });
        _msSel = song.Id; _msMsg = $"„{song.Title}“ gespeichert.";
        return song;
    }

    /// <summary>V7: Qualität messen – ergeben die Spuren wieder das Original, spricht der Gesang über?</summary>
    void MsMeasureStems(MusicSong s, string dir)
    {
        try
        {
            var orig = AudioFile.Read(s.File); var files = StemTools.Files(dir);
            if (files.Count < 4) return;
            var stems = files.Values.Select(AudioFile.Read).ToList();
            var rec = StemTools.Reconstruction(orig, stems);
            var bleed = File.Exists(Path.Combine(dir, "begleitung.wav")) ? StemTools.Bleed(AudioFile.Read(files["gesang"]), AudioFile.Read(Path.Combine(dir, "begleitung.wav"))) : 0;
            MsStemQuality = (rec, bleed);
            _msMsg += $" Qualität: Spuren ergeben das Original zu {rec:0.0} dB{(rec >= 20 ? " (sauber)" : " (Verluste – „Genau“ probieren)")}, Übersprechen {bleed:0.00}.";
        }
        catch (Exception e) when (e is InvalidOperationException or IOException or ArgumentException or InvalidDataException or IndexOutOfRangeException) { } // z. B. FLAC oder kaputte Datei: Messung übersprungen
    }

    Control MsStemMixer(MusicSong s)
    {
        var sp = new StackPanel { Name = "ms-stem-mixer", Spacing = 3, Margin = new Thickness(0, 6, 0, 0) };
        var dir = MsStemDir(s); if (dir is null) return sp;
        var files = StemTools.Files(dir); var mix = MsMixOf(s);
        sp.Children.Add(new TextBlock { Text = "SPUREN-MISCHER", Foreground = Muted, FontSize = 10, FontWeight = FontWeight.Bold });
        foreach (var (id, label) in StemTools.Stems)
        {
            if (!files.ContainsKey(id)) continue;
            var ch = mix.GetValueOrDefault(id) ?? new StemTools.Channel(); var stem = id;
            Button T(string name, string text, bool on, Func<StemTools.Channel> next) { var b = new Button { Name = name, Content = text, Width = 26, Height = 22, Padding = new Thickness(0), FontSize = 10.5, FontWeight = FontWeight.Bold, HorizontalContentAlignment = HorizontalAlignment.Center, Background = on ? Brush.Parse(text == "S" ? "#FACC15" : "#EF4444") : DawButton, Foreground = on ? Brushes.Black : Text, BorderThickness = new Thickness(0) }; b.Click += (_, _) => MsStemSet(s, stem, next()); return b; }
            var vol = new Slider { Name = $"ms-stem-vol-{id}", Minimum = -30, Maximum = 6, Value = ch.GainDb, Width = 110, Margin = new Thickness(4, -8, 0, -8) };
            vol.PointerCaptureLost += (_, _) => MsStemSet(s, stem, (mix.GetValueOrDefault(stem) ?? new StemTools.Channel()) with { GainDb = Math.Round(vol.Value) });
            var get = new Button { Name = $"ms-stem-get-{id}", Content = "Holen", FontSize = 10.5, Padding = new Thickness(6, 1), Height = 22 };
            ToolTip.SetTip(get, $"{label} allein und „ohne {label}“ als zwei eigene Lieder (wie Sunos „Split from Mix“)");
            get.Click += (_, _) => MsIsolate(s, stem);
            var midi = new Button { Name = $"ms-stem-midi-{id}", Content = "MIDI", FontSize = 10.5, Padding = new Thickness(6, 1), Height = 22 };
            ToolTip.SetTip(midi, $"Noten der Spur „{label}“ als MIDI-Datei (mit Unsicherheitsbericht)");
            midi.Click += (_, _) => _ = MsStemMidi(s, stem);
            sp.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 3, Children = {
                new TextBlock { Text = label, Width = 110, Foreground = ch.Mute ? Muted : Text, FontSize = 11.5, VerticalAlignment = VerticalAlignment.Center },
                T($"ms-stem-mute-{id}", "M", ch.Mute, () => ch with { Mute = !ch.Mute }), T($"ms-stem-solo-{id}", "S", ch.Solo, () => ch with { Solo = !ch.Solo }), vol, get, midi } });
        }
        var play = Act("ms-stem-play", "Mischung anhören", () => MsStemPlay(s));
        var save = Act("ms-stem-save", "Als Lied speichern", () => MsStemSave(s));
        var karaoke = Act("ms-karaoke", "Karaoke (LRC)", () => _ = MsKaraoke(s)); // V8
        sp.Children.Add(new WrapPanel { Children = { play, new Border { Width = 4 }, save, new Border { Width = 4 }, karaoke } });
        if (MsStemQuality is { } q) sp.Children.Add(P($"Letzte Trennung: {q.Reconstruction:0.0} dB Rekonstruktion, Übersprechen {q.Bleed:0.00}.", Muted, 10.5));
        return sp;
    }

    /// <summary>Suno V9 „Split from Mix“: eine Spur allein und alles andere („ohne …“) als zwei eigene Lieder.</summary>
    public List<MusicSong> MsIsolate(MusicSong s, string stemId)
    {
        var dir = MsStemDir(s); if (dir is null) { _msMsg = "Erst „Spuren trennen“."; Show(MS); return []; }
        var files = StemTools.Files(dir);
        if (!files.ContainsKey(stemId)) { _msMsg = "Diese Spur gibt es in der Trennung nicht – für Gitarre und Klavier „6 Spuren“ wählen."; Show(MS); return []; }
        var root = Songs.Get(MsStemRoot(s)) ?? s; var label = StemTools.Stems.First(x => x.Id == stemId).Label;
        var (alone, rest) = StemTools.Isolate(files, stemId);
        var fa = Path.Combine(dir, $"nur-{stemId}.wav"); var fr = Path.Combine(dir, $"ohne-{stemId}.wav");
        AudioFile.WriteWav(alone, fa, 24); AudioFile.WriteWav(rest, fr, 24);
        MusicSong Add(string file, string title, string kind, bool voice) => Songs.Add(new MusicSong { Title = $"{root.Title} – {title}", Style = root.Style, Lyrics = voice ? root.Lyrics : "[Instrumental]",
            Language = root.Language, Seconds = Math.Round(alone.Seconds, 2), Bpm = root.Bpm, Key = root.Key, TimeSignature = root.TimeSignature, File = file, Kind = kind, ParentId = root.Id });
        var made = new List<MusicSong> { Add(fa, label, "spur-" + stemId, stemId == "gesang"), Add(fr, "ohne " + label, "ohne-" + stemId, stemId != "gesang" && !string.IsNullOrWhiteSpace(root.Lyrics)) };
        _msSel = made[0].Id; _msMsg = $"„{label}“ allein und „ohne {label}“ sind jetzt eigene Lieder.";
        _core.Audit.Append(Principal.LocalOwner.Name, "musik.spur.geholt", $"{root.Id} {stemId}");
        Show(MS);
        return made;
    }

    /// <summary>Suno V9 „Get MIDI“ je Spur: Noten der Spur (Melodie → Noten mit Unsicherheitsbericht, siehe AudioToMidi).</summary>
    public async Task<string?> MsStemMidi(MusicSong s, string stemId)
    {
        var dir = MsStemDir(s); if (dir is null || !StemTools.Files(dir).TryGetValue(stemId, out var f)) { _msMsg = "Erst „Spuren trennen“."; Show(MS); return null; }
        var root = Songs.Get(MsStemRoot(s)) ?? s; var label = StemTools.Stems.First(x => x.Id == stemId).Label;
        var mid = Path.Combine(dir, $"{stemId}.mid");
        AudioToMidi.Result r;
        if (NotenReady) // Alle Studios, Runde 1: basic-pitch erkennt auch Akkorde; ohne Einrichtung die einstimmige Erkennung
        {
            var json = Path.Combine(dir, $"{stemId}.noten.json");
            if (NotenRunOverride is { } o) await File.WriteAllTextAsync(json, o(f));
            else { var k = await KiPyRun(KiRuntime.Noten, [f, mid, json], 600_000); if (!k.Ok || !File.Exists(json)) { _msMsg = "Noten erkennen (basic-pitch) hat nicht geklappt: " + (k.Err.Length > 0 ? k.Err : k.Out).Trim().Split('\n')[^1]; Show(MS); return null; } }
            r = AudioToMidi.FromBasicPitch(await File.ReadAllTextAsync(json));
        }
        else r = await Task.Run(() => AudioToMidi.Transcribe(AudioFile.Read(f)));
        if (r.Notes.Count == 0) { _msMsg = $"In der Spur „{label}“ wurden keine Noten erkannt."; Show(MS); return null; }
        await File.WriteAllBytesAsync(mid, PianoRoll.ToMidi(AudioToMidi.ToSong(r, root.Bpm ?? 120, $"{root.Title} – {label}")));
        await File.WriteAllTextAsync(Path.Combine(dir, $"{stemId} – Unsicherheitsbericht.md"), AudioToMidi.Report($"{root.Title} – {label}", r));
        _msMsg = $"MIDI „{label}“: {r.Notes.Count} Noten{(NotenReady ? $" (basic-pitch, {r.PolyphonicShare:P0} mehrstimmig)" : r.Exact ? "" : " (geschätzt – mehrstimmig, siehe Unsicherheitsbericht)")} – {Path.GetFileName(mid)} im Spuren-Ordner.";
        Show(MS);
        return mid;
    }

    /// <summary>V8 Karaoke: Liedtext-Zeilen auf die Gesangsphrasen legen (aus der Gesangsspur), als LRC neben die Begleitung schreiben und die Begleitung abspielen.</summary>
    public async Task<string?> MsKaraoke(MusicSong s, bool play = true)
    {
        var root = Songs.Get(MsStemRoot(s)) ?? s; var (acc, voc) = StemsOf(s);
        if (acc is null || voc is null) { _msMsg = "Für Karaoke erst „Spuren trennen“ – dann gibt es Begleitung und Gesang."; Show(MS); return null; }
        var lines = LyricTiming.Lines(root.Lyrics);
        if (lines.Count == 0) { _msMsg = "Für Karaoke fehlt der Liedtext – unter „Lied“ deinen eigenen Text einfügen."; Show(MS); return null; }
        var mel = await MelodyOf(voc);
        if (mel is null) { _msMsg = "Die Gesangsmelodie ließ sich nicht bestimmen (Analyse einrichten)."; Show(MS); return null; }
        var timed = LyricTiming.Align(lines, LyricTiming.Phrases(mel));
        if (timed.Count == 0) { _msMsg = "Im Gesang wurden keine gesungenen Stellen gefunden."; Show(MS); return null; }
        // Ende einer Zeile nie nach dem Beginn der nächsten (sonst überlappen die Zeilen im Player)
        var cues = timed.Select((l, i) => new TimedText.Cue(l.Start, i + 1 < timed.Count ? Math.Min(l.End, timed[i + 1].Start) : l.End, l.Text)).Where(c => c.End > c.Start).ToList();
        var lrc = Path.ChangeExtension(acc.File, ".lrc");
        await File.WriteAllTextAsync(lrc, TimedText.ToLrc(cues, root.Title, root.Language.Length > 0 ? root.Language : "de", acc.Seconds));
        _core.Audit.Append(Principal.LocalOwner.Name, "musik.karaoke", Path.GetFileName(lrc));
        if (play) MusicPlay([acc.File], 0, MS);
        _msMsg = $"Karaoke: {cues.Count} Zeilen mit Zeiten – „{Path.GetFileName(lrc)}“ liegt neben der Begleitung (für jeden Karaoke-Player)." + (play ? " Die Begleitung läuft." : "");
        Show(MS);
        return lrc;
    }

    Control MsStemModelBox()
    {
        var models = new[] { ("htdemucs", "Schnell (Standard)"), ("htdemucs_ft", "Genau – etwa 4× langsamer, lädt einmal ca. 320 MB"), ("htdemucs_6s", "6 Spuren – zusätzlich Gitarre und Klavier") };
        var box = new ComboBox { Name = "ms-stem-model", ItemsSource = models.Select(m => "Trennung: " + m.Item2).ToList(), SelectedIndex = Math.Max(0, Array.FindIndex(models, m => m.Item1 == MsStemModel)), FontSize = 11.5, HorizontalAlignment = HorizontalAlignment.Stretch };
        box.SelectionChanged += (_, _) => { if (box.SelectedIndex >= 0) MsStemModel = models[box.SelectedIndex].Item1; };
        return box;
    }
}
