using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Trendo.Core;
using Trendo.Core.Media;

namespace Trendo.App;

/// <summary>Trendo Musik – Vorbild-Funktionen (Recherche 04.11.2026, docs/programme/MUSIK-KI-RECHERCHE.md) in der Oberfläche:
/// Reiter „Profi“ am Lied (Mastern wie LANDR, Üben wie Moises, MIDI, Loop, Kurzclip) und im Erstellen-Bereich Klang-Referenz/Persona und Ablaufplan.</summary>
public partial class MainWindow
{
    string _mpStyle = "ausgewogen", _mpTarget = "streaming", _mpRefFile = "", _mpMsg = "";
    double _mpTempo = 1; int _mpSemis; bool _mpClick, _mpBusy;
    string? _msRefFile; string _msPlanText = "";
    public string MusicProMessage => _mpMsg;
    /// <summary>Klang-Referenz für neue Lieder: eigene Datei oder eigenes Lied (Persona).</summary>
    public string? MusicReference { get => _msRefFile; set => _msRefFile = value; }

    void MpSay(string t) { _mpMsg = t; if (CurrentView == MS) Show(MS); }

    async Task<IReadOnlyList<CoverStudio.Section>> MpSections(MusicSong s)
    {
        var cache = s.File + ".abschnitte.json";
        string? json = File.Exists(cache) ? await File.ReadAllTextAsync(cache) : null;
        if (json is null && SectionsOverride is { } o) json = await o(s.File);
        else if (json is null && AnalyseReady) { var r = await KiPyRun(KiRuntime.MusikAnalyse, ["--abschnitte", s.File, cache]); json = r.Ok && File.Exists(cache) ? await File.ReadAllTextAsync(cache) : null; }
        if (json is null) return [];
        try { var (_, _, _, parts) = SongUnderstanding.ParseSections(json); return [.. parts.Select(p => new CoverStudio.Section(p.Start, p.End, p.Energy))]; }
        catch (Exception e) when (e is System.Text.Json.JsonException or KeyNotFoundException or InvalidOperationException) { return []; }
    }

    /// <summary>Neue Fassung als eigenes Lied (Original bleibt).</summary>
    async Task<MusicSong?> MpAddVersion(MusicSong s, string file, string suffix)
    {
        var v = await MsImport(file); if (v is null) return null;
        Songs.Update(v.Id, x => { x.Title = $"{s.Title} – {suffix}"; x.Lyrics = s.Lyrics; x.Style = s.Style; x.Language = s.Language; x.ParentId = s.Id; });
        TryDelete(file); return Songs.Get(v.Id);
    }

    /// <summary>Mastern mit Stil und Plattform-Ziel, wahlweise nach Referenz (LANDR).</summary>
    public async Task<MusicSong?> MsMasterPro(string songId, string style, string target, string? reference = null)
    {
        if (Songs.Get(songId) is not { } s || !File.Exists(s.File)) return null;
        var ff = await MasterFfmpeg(); if (ff is null) { MpSay("Zum Mastern braucht Trendo FFmpeg – unter „Programme“ einrichten."); return null; }
        _mpBusy = true; MpSay("Wird gemastert …");
        try
        {
            string? eq = null;
            if (reference is { Length: > 0 } && File.Exists(reference))
            {
                try { var (f, g) = MusicPro.MatchEq(MusicPro.BandLevels(AudioFile.Read(s.File)), MusicPro.BandLevels(AudioFile.Read(reference))); eq = f; }
                catch (Exception e) when (e is InvalidOperationException or IOException) { MpSay("Referenz ließ sich nicht lesen (WAV oder MP3): " + e.Message); return null; }
            }
            var t = MusicPro.TargetOf(target); var st = MusicPro.Style(style);
            var outFile = Path.Combine(Path.GetDirectoryName(s.File)!, $".trendo-master-{Guid.NewGuid():N}.mp3");
            var r = await MasterRun(ff, MusicPro.MasterArgs(s.File, outFile, style, target, eq));
            if (!r.Ok || !File.Exists(outFile)) { MpSay("Mastern ging nicht: " + Short(r.Err)); return null; }
            var v = await MpAddVersion(s, outFile, $"Master {st.Label}, {t.Lufs:0} LUFS" + (eq is { Length: > 0 } ? " nach Referenz" : ""));
            _core.Audit.Append(Principal.LocalOwner.Name, "musik.master.pro", $"{st.Id} {t.Id} ref={(eq is null ? "nein" : "ja")}");
            MpSay(v is null ? "Mastern ging nicht." : $"Gemastert ({st.Label}, Ziel {t.Label} {t.Lufs:0} LUFS{(eq is { Length: > 0 } ? ", Klang an die Referenz angeglichen" : "")}).");
            return v;
        }
        finally { _mpBusy = false; if (CurrentView == MS) Show(MS); }
    }

    /// <summary>Übe-Fassung: langsamer/schneller ohne Tonhöhe, andere Tonart, wahlweise mit Klick (Moises).</summary>
    public async Task<MusicSong?> MsPractice(string songId, double tempo, int semitones, bool click)
    {
        if (Songs.Get(songId) is not { } s || !File.Exists(s.File)) return null;
        // Paket 01 S4-12: nicht still begrenzen – extreme Werte ablehnen, starke mit Artefakt-Warnung
        var check = TimePitch.Assess(tempo, semitones);
        if (check.Light == "rot") { MpSay(check.Text); return null; }
        var ff = await MasterFfmpeg(); if (ff is null) { MpSay("Zum Üben braucht Trendo FFmpeg – unter „Programme“ einrichten."); return null; }
        _mpBusy = true; MpSay("Übe-Fassung entsteht …");
        try
        {
            var dir = Path.GetDirectoryName(s.File)!; var outFile = Path.Combine(dir, $".trendo-ueben-{Guid.NewGuid():N}.wav");
            var r = await MasterRun(ff, TimePitch.Args(s.File, outFile, tempo, semitones));
            if (!r.Ok || !File.Exists(outFile)) { MpSay("Ging nicht: " + Short(r.Err)); return null; }
            string quality = "";
            try { var m = TimePitch.Compare(AudioFile.Read(s.File), AudioFile.Read(outFile)); quality = " " + TimePitch.Verify(m, tempo, semitones).Text; }
            catch (Exception e) when (e is InvalidOperationException or IOException or InvalidDataException or ArgumentException) { } // Messung nur, wenn lesbar
            if (click)
            {
                // Takt der neuen Fassung selbst erkennen (ohne Python) und eine Klickspur dazumischen
                var a = AudioFile.Read(outFile); var bt = BeatDetect.Find(a);
                var meter = s.TimeSignature is { Length: > 0 } ts && int.TryParse(ts, out var bpb) ? bpb : 4;
                var ck = MusicPro.ClickTrack(bt.Beats, a.Seconds, meter, a.Rate);
                for (var c = 0; c < a.Channels; c++) for (var i = 0; i < a.Length; i++) a.Data[c][i] = Math.Clamp(a.Data[c][i] * 0.85f + ck.Data[Math.Min(c, 1)][i] * 0.6f, -1f, 1f);
                AudioFile.WriteWav(a, outFile);
            }
            var label = $"Üben {tempo * 100:0} %" + (semitones != 0 ? $", {(semitones > 0 ? "+" : "")}{semitones} Halbtöne" : "") + (click ? ", mit Klick" : "");
            var v = await MpAddVersion(s, outFile, label);
            _core.Audit.Append(Principal.LocalOwner.Name, "musik.ueben", label);
            MpSay(v is null ? "Ging nicht." : $"Übe-Fassung „{label}“ ist fertig." + quality + (check.Light == "gelb" ? "" + check.Text : ""));
            return v;
        }
        finally { _mpBusy = false; if (CurrentView == MS) Show(MS); }
    }

    /// <summary>MIDI-Datei: Melodie und Akkorde (AIVA). Melodie aus der Analyse (Gesangsspur, wenn getrennt).</summary>
    public async Task<string?> MsMidi(string songId)
    {
        if (Songs.Get(songId) is not { } s) return null;
        if (s.Analysis is not { } an) { MpSay("Zuerst unter „Lied“ analysieren (Tonart, Tempo, Akkorde)."); return null; }
        var (_, vocal) = StemsOf(s); var src = vocal?.File ?? s.File; var cache = src + ".melodie.json";
        string? json = File.Exists(cache) ? await File.ReadAllTextAsync(cache) : null;
        if (json is null && MelodyOverride is { } o) json = await o(src);
        else if (json is null && AnalyseReady) { var r = await KiPyRun(KiRuntime.MusikAnalyse, vocal is null ? ["--melodie", src, cache] : ["--melodie", "--gesangsspur", src, cache]); json = r.Ok && File.Exists(cache) ? await File.ReadAllTextAsync(cache) : null; }
        var bp = CoverStudio.Build(an, null, json);
        var notes = MusicPro.NotesFromMelody(bp.Melody, bp.Hop);
        var bytes = MusicPro.Midi(notes, an.Chords, an.Bpm, int.TryParse(an.MeterValue, out var m) ? m : 4, an.Duration);
        var file = Path.Combine(WorkFolder("Musik"), DevStudio.SafeName(s.Title) + ".mid"); await File.WriteAllBytesAsync(file, bytes);
        _core.Audit.Append(Principal.LocalOwner.Name, "musik.midi", Path.GetFileName(file));
        MpSay($"MIDI gespeichert: {Path.GetFileName(file)} – {notes.Count} Noten, {an.Chords.Count} Akkorde (zum Weiterbearbeiten in jedem Notenprogramm).");
        return file;
    }

    /// <summary>Nahtloser Loop aus dem Lied (Stable Audio): Takte im Tempo der Analyse.</summary>
    public async Task<MusicSong?> MsLoop(string songId, int bars)
    {
        if (Songs.Get(songId) is not { } s || !File.Exists(s.File)) return null;
        try
        {
            var a = AudioFile.Read(s.File); var bpm = s.Analysis?.Bpm ?? BeatDetect.Find(a).Bpm; var bpb = s.Analysis is { } an && int.TryParse(an.MeterValue, out var m) ? m : 4;
            var loop = MusicPro.MakeLoop(a, bpm, bpb, bars);
            var f = Path.Combine(Path.GetDirectoryName(s.File)!, $".trendo-loop-{Guid.NewGuid():N}.wav"); AudioFile.WriteWav(loop, f, 24);
            var v = await MpAddVersion(s, f, $"Loop {bars} Takte ({bpm:0} BPM)");
            MpSay(v is null ? "Ging nicht." : $"Loop mit {bars} Takten – läuft ohne Knacken endlos weiter.");
            return v;
        }
        catch (InvalidOperationException e) { MpSay(e.Message); return null; }
    }

    /// <summary>Kurzclip für soziale Netzwerke aus dem stärksten Abschnitt (Suno Hooks): Hochkant-Video mit Titelbild und Wellenform.</summary>
    public async Task<string?> MsHookClip(string songId, double seconds)
    {
        if (Songs.Get(songId) is not { } s || !File.Exists(s.File)) return null;
        var ff = await MasterFfmpeg(); if (ff is null) { MpSay("Für den Kurzclip braucht Trendo FFmpeg – unter „Programme“ einrichten."); return null; }
        _mpBusy = true; MpSay("Kurzclip entsteht …");
        try
        {
            var dur = s.Seconds > 0 ? s.Seconds : s.Analysis?.Duration ?? 60;
            var start = MusicPro.HookStart(await MpSections(s), dur, seconds);
            var cover = s.CoverFile is { Length: > 0 } c && File.Exists(c) ? c : null;
            var dir = Path.Combine(WorkFolder("Musik"), "Kurzclips"); Directory.CreateDirectory(dir);
            var file = Path.Combine(dir, $"{DevStudio.SafeName(s.Title)} – {seconds:0} s.mp4");
            var r = await MasterRun(ff, MusicPro.HookArgs(s.File, cover, file, start, seconds, s.Title));
            if (!r.Ok || !File.Exists(file)) { MpSay("Kurzclip ging nicht: " + Short(r.Err)); return null; }
            _core.Audit.Append(Principal.LocalOwner.Name, "musik.kurzclip", Path.GetFileName(file));
            MpSay($"Kurzclip ({seconds:0} s ab {CoverStudio.Time(start)}) gespeichert: {Path.GetFileName(file)}");
            return file;
        }
        finally { _mpBusy = false; if (CurrentView == MS) Show(MS); }
    }

    Control MsProPanel(MusicSong s)
    {
        var sp = new StackPanel { Name = "mp-panel", Spacing = 10 };
        Button B(string name, string label, Func<Task> a, bool primary = false) { var b = new Button { Name = name, Content = label, FontSize = 12, IsEnabled = !_mpBusy, Padding = new Thickness(10, 4) }; if (primary) { b.Background = Accent; b.Foreground = Brushes.White; } b.Click += async (_, _) => await a(); return b; }
        ComboBox Combo<T>(string name, T[] items, Func<T, string> label, int sel, Action<int> set) { var c = new ComboBox { Name = name, ItemsSource = items.Select(label).ToList(), SelectedIndex = sel, MinWidth = 200 }; c.SelectionChanged += (_, _) => set(Math.Max(0, c.SelectedIndex)); return c; }

        // Mastern
        var style = Combo("mp-style", MusicPro.Styles, x => $"{x.Label} – {x.Hint}", Math.Max(0, Array.FindIndex(MusicPro.Styles, x => x.Id == _mpStyle)), i => _mpStyle = MusicPro.Styles[i].Id);
        var target = Combo("mp-target", MusicPro.Targets, x => $"{x.Label} ({x.Lufs:0} LUFS)", Math.Max(0, Array.FindIndex(MusicPro.Targets, x => x.Id == _mpTarget)), i => _mpTarget = MusicPro.Targets[i].Id);
        var refBox = new TextBox { Name = "mp-ref-file", Text = _mpRefFile, PlaceholderText = "Referenz-Lied (WAV/MP3), optional" };
        refBox.TextChanged += (_, _) => _mpRefFile = (refBox.Text ?? "").Trim().Trim('"');
        sp.Children.Add(new StackPanel { Spacing = 4, Children = { H("Mastern wie im Studio", 13), P("Stil und Ziel je Plattform – mit Referenz klingt dein Lied wie ein Lied, das du magst (Klangbild, nicht die Musik).", Muted, 11.5), style, target, refBox,
            B("mp-master", "Mastern (neue Fassung)", () => MsMasterPro(s.Id, _mpStyle, _mpTarget, _mpRefFile.Length > 0 ? _mpRefFile : null), true) } });

        // Üben
        var tempo = new Slider { Name = "mp-tempo", Minimum = 0.5, Maximum = 1.5, Value = _mpTempo, Width = 200 };
        var tempoLbl = new TextBlock { Text = $"Tempo {_mpTempo * 100:0} %", Foreground = Muted, FontSize = 11.5 };
        tempo.ValueChanged += (_, _) => { _mpTempo = Math.Round(tempo.Value, 2); tempoLbl.Text = $"Tempo {_mpTempo * 100:0} %"; };
        var semis = new NumericUpDown { Name = "mp-semis", Minimum = -6, Maximum = 6, Value = _mpSemis, Increment = 1, FormatString = "0", Width = 110 };
        semis.ValueChanged += (_, _) => _mpSemis = (int)(semis.Value ?? 0);
        var click = new CheckBox { Name = "mp-click", Content = "Klick (Metronom) dazu", IsChecked = _mpClick, FontSize = 12 }; click.IsCheckedChanged += (_, _) => _mpClick = click.IsChecked == true;
        var capo = s.Analysis is { } an ? MusicPro.Capo(an.Key) : (0, "");
        sp.Children.Add(new StackPanel { Spacing = 4, Children = { H("Üben", 13), P("Langsamer üben ohne Mickymaus-Stimme, in deine Tonlage verschieben, mit Klick." + (s.Analysis is not null ? $" Gitarre: {(capo.Item1 == 0 ? "ohne Kapo" : $"Kapo im {capo.Item1}. Bund")}, Griffe wie {capo.Item2}." : ""), Muted, 11.5),
            tempoLbl, tempo, new WrapPanel { Children = { new TextBlock { Text = "Tonart ± Halbtöne", Foreground = Muted, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) }, semis } }, click,
            B("mp-practice", "Übe-Fassung erzeugen", () => MsPractice(s.Id, _mpTempo, _mpSemis, _mpClick)) } });

        // Weitergeben
        sp.Children.Add(new StackPanel { Spacing = 4, Children = { H("Weitergeben", 13),
            new WrapPanel { Children = { B("mp-midi", "MIDI (Melodie + Akkorde)", async () => { await MsMidi(s.Id); }), new Border { Width = 6 },
                B("mp-loop-4", "Loop 4 Takte", async () => { await MsLoop(s.Id, 4); }), new Border { Width = 6 }, B("mp-loop-8", "Loop 8 Takte", async () => { await MsLoop(s.Id, 8); }) } },
            new WrapPanel { Children = { B("mp-hook-15", "Kurzclip 15 s", async () => { await MsHookClip(s.Id, 15); }), new Border { Width = 6 }, B("mp-hook-30", "Kurzclip 30 s", async () => { await MsHookClip(s.Id, 30); }) } },
            P("Kurzclip: Hochkant-Video aus dem stärksten Teil (meist der Refrain) mit Titelbild und Wellenform – für Reels, Shorts, Status.", Muted, 11) } });
        if (_mpMsg.Length > 0) sp.Children.Add(new TextBlock { Name = "mp-msg", Text = _mpMsg, Foreground = Text, FontSize = 12, TextWrapping = TextWrapping.Wrap });
        return sp;
    }

    /// <summary>Im Erstellen-Bereich: Klang-Referenz/Persona (Suno Inspo) und Ablaufplan (ElevenLabs composition plan).</summary>
    Control MsRefPlanPanel(Action sync)
    {
        var sp = new StackPanel { Name = "ms-refplan", Spacing = 6 };
        sp.Children.Add(H("Klang wie …", 13));
        sp.Children.Add(P("Neues Lied klingt wie eine Datei oder eines deiner Lieder (Klang und Stimmung, keine Melodie).", Muted, 11));
        var mine = Songs.All().Where(x => File.Exists(x.File) && x.Kind is not ("gesang" or "begleitung")).Take(30).ToList();
        var pick = new ComboBox { Name = "ms-ref-song", ItemsSource = new[] { "– keine Referenz –" }.Concat(mine.Select(x => "" + x.Title)).ToList(), MinWidth = 220,
            SelectedIndex = _msRefFile is null ? 0 : Math.Max(0, mine.FindIndex(x => x.File == _msRefFile) + 1) };
        pick.SelectionChanged += (_, _) => { _msRefFile = pick.SelectedIndex <= 0 ? null : mine[pick.SelectedIndex - 1].File; };
        var file = new TextBox { Name = "ms-ref-file", PlaceholderText = "… oder eigene Tondatei (WAV/MP3)", Text = _msRefFile is { } r && mine.All(x => x.File != r) ? r : "" };
        file.TextChanged += (_, _) => { var t = (file.Text ?? "").Trim().Trim('"'); if (t.Length > 0) _msRefFile = t; };
        sp.Children.Add(pick); sp.Children.Add(file);
        sp.Children.Add(H("Ablaufplan", 13));
        sp.Children.Add(P("Teile mit Länge in Takten und Stimmung – Trendo baut daraus das Textgerüst und die Länge.", Muted, 11));
        var plan = new TextBox { Name = "ms-plan", AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 90, FontSize = 12,
            Text = _msPlanText.Length > 0 ? _msPlanText : string.Join("\n", MusicPro.DefaultPlan.Select(p => $"{p.Name}; {p.Bars}; {p.Mood}{(p.Vocals ? "" : "; instrumental")}")) };
        plan.TextChanged += (_, _) => _msPlanText = plan.Text ?? "";
        var apply = Act("ms-plan-apply", "Als Gerüst in den Liedtext", () => { sync(); MsApplyPlan(plan.Text ?? ""); });
        sp.Children.Add(plan); sp.Children.Add(apply);
        return sp;
    }

    /// <summary>Plan-Zeilen „Name; Takte; Stimmung[; instrumental]“ → Liedtext-Gerüst (vorhandene Abschnittstexte bleiben), Länge und Tempo passend.</summary>
    public string MsApplyPlan(string text)
    {
        var parts = new List<MusicPro.PlanPart>();
        foreach (var line in text.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0))
        {
            var c = line.Split(';').Select(x => x.Trim()).ToArray();
            parts.Add(new(c[0], c.Length > 1 && int.TryParse(c[1], out var b) ? Math.Clamp(b, 1, 64) : 8, c.Length > 2 ? c[2] : "", !(c.Length > 3 && c[3].StartsWith("instr", StringComparison.OrdinalIgnoreCase))));
        }
        if (parts.Count == 0) { _msMsg = "Der Plan ist leer."; Show(MS); return _msLyrics; }
        var existing = MusicPro.SectionTexts(_msLyrics);
        _msLyrics = MusicPro.PlanLyrics(parts, existing);
        var bpm = int.TryParse(_msBpm, out var bp) ? bp : 110; var sec = MusicPro.PlanSeconds(parts, bpm, _msTime is { Length: > 0 } t && int.TryParse(t, out var bb) ? bb : 4);
        _msPro = true; _msPlanText = text;
        _msMsg = $"Gerüst mit {parts.Count} Teilen – etwa {CoverStudio.Time(sec)} bei {bpm} BPM. Texte unter den Marken eintragen.";
        Show(MS); return _msLyrics;
    }
}
