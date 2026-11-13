using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Trendo.Core;

namespace Trendo.App;

/// <summary>Trendo Musik – Cover-Werkstatt M1–M8 (freigegeben 04.11.2026, „ja alles“): Lied suchen → eigenes Original untersuchen → Text einfügen →
/// Cover mit gleichem Rhythmus und gleicher Melodie (ACE-Step) → mit dem Original vergleichen (Ampel, bis zu 3 Versuche) → Klang der Epoche.
/// Dazu Karaoke aus der eigenen Datei. Feste Grenzen: keine Stimme einer echten Person, Liedtext nur vom Nutzer, Original nur aus eigener Datei.</summary>
public partial class MainWindow
{
    string _cwQuery = "", _cwLyrics = "", _cwGenre = "", _cwVoice = "frei", _cwSound = "heute", _cwMsg = "";
    double _cwCloseness = 0.9; bool _cwBusy, _cwOpen; // gemessen 04.11.2026: erst ab Nähe 0,9 folgt die Melodie messbar dem Original
    List<CoverStudio.Hit> _cwHits = []; CoverStudio.Hit? _cwHit;
    string? _cwOriginal; CoverStudio.Blueprint? _cwPlan;
    readonly List<(string SongId, CoverStudio.Report Report, double Strength)> _cwResults = [];

    public IReadOnlyList<CoverStudio.Hit> CoverHits => _cwHits;
    public CoverStudio.Blueprint? CoverPlan => _cwPlan;
    public IReadOnlyList<(string SongId, CoverStudio.Report Report, double Strength)> CoverResults => _cwResults;
    public string CoverMessage => _cwMsg;
    public string CoverLyrics { get => _cwLyrics; set => _cwLyrics = value; }
    public string CoverVoice { get => _cwVoice; set => _cwVoice = value == "eigen" ? "eigen" : CoverStudio.Voice(value).Id; }
    public string CoverSound { get => _cwSound; set => _cwSound = CoverStudio.Sound(value).Id; }
    public string CoverGenre { get => _cwGenre; set => _cwGenre = value; }
    public double CoverCloseness { get => _cwCloseness; set => _cwCloseness = Math.Clamp(value, 0, 1); }

    void CwSay(string t) { _cwMsg = t; if (CurrentView == MS) Show(MS); }

    /// <summary>M1: Lied suchen (MusicBrainz) – Titel, Künstler, Jahr, Album, Länge. Kein Liedtext.</summary>
    public async Task<IReadOnlyList<CoverStudio.Hit>> MsCoverSearch(string query)
    {
        _cwQuery = query.Trim(); _cwHits = []; _cwHit = null; _cwOpen = true;
        if (_cwQuery.Length < 3) { CwSay("Gib Künstler und Titel ein, z. B. „Erika Muster – Mein Lied“."); return _cwHits; }
        if (_core.Offline && SongFactsFetchOverride is null) { CwSay("Trendo ist offline – fürs Suchen braucht es Internet."); return _cwHits; }
        CwSay("Suche …");
        using var http = MsWebClient();
        var j = await MsWebGet(http, CoverStudio.SearchUri(_cwQuery));
        _cwHits = j is null ? [] : CoverStudio.ParseHits(j);
        CwSay(j is null ? "MusicBrainz antwortet gerade nicht – gleich noch einmal versuchen." : _cwHits.Count == 0 ? "Nichts gefunden – Schreibweise prüfen." : $"{_cwHits.Count} Treffer – den richtigen anklicken.");
        return _cwHits;
    }

    public void MsCoverPick(int i)
    {
        if (i < 0 || i >= _cwHits.Count) return;
        _cwHit = _cwHits[i];
        _core.Audit.Append(Principal.LocalOwner.Name, "musik.cover.lied", _cwHit.Mbid);
        CwSay($"„{_cwHit.Title}“ – {_cwHit.Artist}{(_cwHit.Year is { } y ? $" ({y})" : "")}. {CoverStudio.LyricsHint}");
    }

    /// <summary>M2: eigenes Original übernehmen und untersuchen (Tempo, Takt, Tonart, Akkorde, Aufbau, Melodie).</summary>
    public async Task<CoverStudio.Blueprint?> MsCoverOriginal(string? file = null)
    {
        var s = await MsImport(file);
        if (s is null) return null;
        _cwOriginal = s.Id; _cwResults.Clear(); _cwOpen = true;
        if (_cwHit is { } h) Songs.Update(s.Id, x => x.Title = $"{h.Title} – Original");
        return _cwPlan = await CwBlueprint(s.File, s.Id, "Das Original wird untersucht – Tempo, Takt, Tonart, Akkorde, Aufbau und Melodie …");
    }

    async Task<CoverStudio.Blueprint?> CwBlueprint(string file, string? songId, string say)
    {
        CwSay(say);
        var a = await MsAnalyse(file, songId);
        if (a is null) { CwSay("Die Untersuchung ging nicht: " + _msMsg); return null; }
        string? sections = null, melody = null;
        try
        {
            if (SectionsOverride is { } so) sections = await so(file);
            else if (AnalyseReady)
            {
                var o = Path.Combine(Path.GetTempPath(), $"trendo-abschnitte-{Guid.NewGuid():N}.json");
                var r = await KiPyRun(KiRuntime.MusikAnalyse, ["--abschnitte", file, o]);
                if (r.Ok && File.Exists(o)) { sections = await File.ReadAllTextAsync(o); TryDelete(o); }
            }
            if (MelodyOverride is { } mo) melody = await mo(file);
            else if (AnalyseReady)
            {
                // Melodie auf der Gesangsspur messen (gemessen 04.11.2026: aus dem ganzen Mix ist zu wenig Gesang erkennbar für einen fairen Vergleich)
                string? vocal = null; var tmp = Path.Combine(Path.GetTempPath(), $"trendo-cwspuren-{Guid.NewGuid():N}");
                if (KiStore.RuntimeReady(KiRuntime.Spuren))
                {
                    CwSay(say + " (Gesang wird dafür kurz herausgelöst)");
                    var sr = await KiPyRun(KiRuntime.Spuren, [file, tmp]);
                    if (sr.Ok && File.Exists(Path.Combine(tmp, "gesang.wav"))) vocal = Path.Combine(tmp, "gesang.wav");
                }
                var o = Path.Combine(Path.GetTempPath(), $"trendo-melodie-{Guid.NewGuid():N}.json");
                var r = await KiPyRun(KiRuntime.MusikAnalyse, vocal is null ? ["--melodie", file, o] : ["--melodie", "--gesangsspur", vocal, o]);
                if (r.Ok && File.Exists(o)) { melody = await File.ReadAllTextAsync(o); TryDelete(o); }
                try { if (Directory.Exists(tmp)) Directory.Delete(tmp, true); } catch (IOException) { }
            }
        }
        catch (Exception e) when (e is IOException or InvalidOperationException) { }
        var b = CoverStudio.Build(a, sections, melody);
        CwSay($"Bauplan: {b.Bpm:0} BPM · {b.Meter} · {b.KeyDe} · {b.Sections.Count} Teile" + (b.Melody.Count > 0 ? $" · Melodie {CoverStudio.Note(b.MelodyLow)}–{CoverStudio.Note(b.MelodyHigh)}" : ""));
        return b;
    }

    /// <summary>M3 + M4 (+ M8): Cover erzeugen, mit dem Original vergleichen, bei gelb/rot näher am Original neu (höchstens 3 Versuche), dann Klang der Epoche.</summary>
    public async Task<MusicSong?> MsCoverMake()
    {
        if (_cwBusy) return null;
        if (_cwOriginal is not { } oid || Songs.Get(oid) is not { } orig || _cwPlan is not { } plan) { CwSay("Zuerst unter Schritt 2 dein Original wählen."); return null; }
        if (_cwLyrics.Trim().Length == 0) { CwSay("Bitte unter Schritt 3 den Liedtext einfügen – Trendo holt ihn nicht aus dem Internet."); return null; }
        _cwBusy = true; _cwResults.Clear();
        try
        {
            var own = _cwVoice == "eigen";
            if (own && !OwnVoiceReady) { CwSay("Deine Stimme ist noch nicht gelernt – unter Schritt 4 „Meine eigene Stimme“."); return null; }
            var voice = CoverStudio.Voice(own ? "frei" : _cwVoice); var strength = CoverStudio.Strength(_cwCloseness);
            var title = (_cwHit?.Title ?? orig.Title.Replace(" – Original", "")) + " (Cover)";
            MusicSong? best = null; CoverStudio.Report? bestR = null;
            for (var attempt = 1; attempt <= 3; attempt++)
            {
                CwSay($"Cover entsteht (Versuch {attempt}, Nähe zum Original {strength:0.00}) …");
                var job = new AceStepClient.Job(CoverStudio.CoverStyle(plan, _cwGenre, voice), _cwLyrics.Trim(), (int)Math.Round(Math.Max(10, plan.Duration)),
                    AceStepClient.Languages[Math.Clamp(_msLang, 0, AceStepClient.Languages.Length - 1)].Code, 1, (int)Math.Round(plan.Bpm), plan.Key.Length > 0 ? plan.Key : null,
                    plan.Meter.Split('/')[0], Task: "cover", CoverStrength: strength);
                var made = await MusicStudioCreate(job, title + (attempt > 1 ? $" {attempt}" : ""), oid);
                if (made.Count == 0) { CwSay("Das Cover ist nicht entstanden: " + _msMsg); break; }
                var song = made[0];
                var cp = await CwBlueprint(song.File, song.Id, "Das Cover wird mit dem Original verglichen …");
                if (cp is null) { best ??= song; break; }
                var rep = CoverStudio.Compare(plan, cp);
                _cwResults.Add((song.Id, rep, strength));
                if (bestR is null || rep.Score > bestR.Score) { best = song; bestR = rep; }
                _core.Audit.Append(Principal.LocalOwner.Name, "musik.cover.vergleich", $"{song.Id} {rep.Score:0.00} {rep.Light}");
                if (CoverStudio.NextTry(rep, strength, attempt) is not { } next) break;
                strength = next.Strength;
                CwSay($"Ampel {LightDe(rep.Light)} – neuer Versuch {next.Why}.");
            }
            if (best is null) return null;
            if (own) best = await MsOwnVoiceSing(best) ?? best; // M6: Gesang in der eigenen Stimme
            // M8: Klang der Epoche (Stimme bringt ihren Klang mit, eigene Wahl hat Vorrang)
            var soundId = _cwSound != "heute" ? _cwSound : voice.Sound;
            if (soundId != "heute") best = await CwApplySound(best, soundId) ?? best;
            CwSay($"Cover fertig: „{best.Title}“" + (bestR is null ? "." : $" – Ampel {LightDe(bestR.Light)} ({bestR.Score * 100:0} % wie das Original)."));
            return best;
        }
        finally { _cwBusy = false; if (CurrentView == MS) Show(MS); }
    }

    static string LightDe(string l) => l switch { "gruen" => "grün", "gelb" => "gelb", _ => "rot" };

    /// <summary>Für Tests: Ersatz für FFmpeg beim Epochen-Klang (Eingang, Ausgang).</summary>
    public Func<string, string, Task<bool>>? EraSoundOverride { get; set; }

    /// <summary>M8: Klang wie damals – als neue Fassung neben dem Cover (das Cover selbst bleibt).</summary>
    public async Task<MusicSong?> CwApplySound(MusicSong s, string soundId)
    {
        var snd = CoverStudio.Sound(soundId);
        var target = Path.Combine(Path.GetDirectoryName(s.File)!, Path.GetFileNameWithoutExtension(s.File) + $" ({snd.Id}).mp3");
        bool ok;
        if (EraSoundOverride is { } f) ok = await f(s.File, target);
        else
        {
            if (_opt.Isolated) { CwSay("Testmodus: Trendo startet kein FFmpeg."); return null; }
            var ff = FfmpegPath; if (ff is null) { CwSay("Für den Klang der Epoche braucht Trendo FFmpeg – unter Zusatzprogramme einrichten."); return null; }
            ok = (await Task.Run(() => Cli.Run(ff, CoverStudio.SoundArgs(s.File, target, snd.Id), timeoutMs: 600_000))).Ok;
        }
        if (!ok || !File.Exists(target)) return null;
        var v = await MsImport(target);
        if (v is not null) { Songs.Update(v.Id, x => { x.Title = $"{s.Title} – Klang {snd.Label.Split(' ')[0]}"; x.Lyrics = s.Lyrics; x.Style = s.Style; }); TryDelete(target); }
        return v is null ? null : Songs.Get(v.Id);
    }

    /// <summary>M7: Karaoke aus der eigenen Datei – Spuren trennen, dann über die Begleitung mitsingen (Text und Melodie laufen mit).</summary>
    public async Task<bool> MsCoverKaraoke()
    {
        if (_cwOriginal is not { } oid || Songs.Get(oid) is null) { CwSay("Zuerst unter Schritt 2 dein Original wählen."); return false; }
        if (_cwLyrics.Trim().Length > 0) Songs.Update(oid, x => x.Lyrics = _cwLyrics.Trim());
        CwSay("Gesang und Begleitung werden getrennt …");
        try { var st = await MsSplit(oid); if (st.Count == 0) { CwSay("Trennen ging nicht: " + _msMsg); return false; } }
        catch (InvalidOperationException e) { CwSay(e.Message); return false; }
        var ok = await MsSingStart(oid);
        CwSay(ok ? "Karaoke läuft – singe über die Begleitung." : _msMsg);
        return ok;
    }

    // ───────────── Oberfläche ─────────────
    Control MsCoverWorkshopBox()
    {
        var sp = new StackPanel { Name = "cw-body", Spacing = 10 };
        Button B(string name, string label, Func<Task> a, bool primary = false)
        {
            var b = new Button { Name = name, Content = label, FontSize = 12, Padding = new Thickness(10, 4), CornerRadius = new CornerRadius(8), IsEnabled = !_cwBusy };
            if (primary) { b.Background = Accent; b.Foreground = Brushes.White; }
            b.Click += async (_, _) => await a(); return b;
        }
        TextBlock Step(string t) => new() { Text = t, Foreground = Brush.Parse("#F5B83D"), FontSize = 12.5, FontWeight = FontWeight.SemiBold };

        // 1 Lied suchen
        var q = new TextBox { Name = "cw-query", Text = _cwQuery, Watermark = "Künstler – Titel", MinWidth = 220 };
        sp.Children.Add(new StackPanel { Spacing = 4, Children = { Step("1. Lied suchen"), new WrapPanel { Children = { q, new Border { Width = 6 }, B("cw-search", "Suchen", () => MsCoverSearch(q.Text ?? "")) } } } });
        for (var i = 0; i < _cwHits.Count && i < 6; i++)
        {
            var ii = i; var h = _cwHits[i]; var on = _cwHit?.Mbid == h.Mbid;
            var b = new Button { Name = $"cw-hit-{i}", HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left, Padding = new Thickness(8, 4), FontSize = 12,
                Background = on ? Brush.Parse("#33F5B83D") : Brushes.Transparent, BorderBrush = on ? Brush.Parse("#F5B83D") : Brushes.Transparent, BorderThickness = new Thickness(1),
                Content = new StackPanel { Children = { new TextBlock { Text = $"{h.Title} – {h.Artist}", FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap },
                    new TextBlock { Text = string.Join(" · ", new[] { h.Year?.ToString(), h.Album, h.Seconds > 0 ? CoverStudio.Time(h.Seconds) : null }.Where(x => !string.IsNullOrEmpty(x))), Foreground = Muted, FontSize = 11, TextWrapping = TextWrapping.Wrap } } } };
            ToolTip.SetTip(b, "Anklicken übernimmt Titel, Länge, Stil, Tonart und Tempo in „Erstellen“");
            b.Click += (_, _) => _ = MsCoverUse(ii); // Wunsch 05.11.2026: Klick übernimmt alle Einstellungen
            sp.Children.Add(b);
        }
        if (_cwHit is { } hit)
        {
            if (MsCoverTaken.Count > 0) sp.Children.Add(new TextBlock { Name = "cw-taken", Text = "Übernommen: " + string.Join(", ", MsCoverTaken), Foreground = Brush.Parse("#4ADE80"), FontSize = 11.5, TextWrapping = TextWrapping.Wrap });
            var link = new Button { Name = "cw-page", Content = "Infos bei MusicBrainz ansehen", FontSize = 11.5, Padding = new Thickness(8, 2) };
            link.Click += (_, _) => Launch(hit.Page);
            sp.Children.Add(link);
        }

        // 2 Original
        var orig = _cwOriginal is { } oid ? Songs.Get(oid) : null;
        sp.Children.Add(new StackPanel { Spacing = 4, Children = { Step("2. Dein Original (eigene Datei: MP3, CD-Kopie …)"),
            B("cw-original", orig is null ? "Datei wählen und untersuchen" : "Andere Datei wählen", async () => { await MsCoverOriginal(); }) } });
        if (_cwPlan is { } p)
        {
            var g = new WrapPanel();
            foreach (var (k, v) in new[] { ("Tempo", $"{p.Bpm:0} BPM"), ("Takt", p.Meter), ("Tonart", p.KeyDe), ("Länge", CoverStudio.Time(p.Duration)), ("Aufbau", $"{p.Sections.Count} Teile"),
                         ("Melodie", p.Melody.Count > 0 ? $"{CoverStudio.Note(p.MelodyLow)}–{CoverStudio.Note(p.MelodyHigh)}" : "–"), ("Akkorde", string.Join(" ", p.Chords.Take(6))) })
                g.Children.Add(new Border { Background = TileBg, BorderBrush = Edge, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8), Padding = new Thickness(8, 4), Margin = new Thickness(0, 0, 6, 6),
                    Child = new StackPanel { Children = { new TextBlock { Text = k, Foreground = Muted, FontSize = 10.5 }, new TextBlock { Text = v, Foreground = Text, FontSize = 12.5, FontWeight = FontWeight.SemiBold } } } });
            sp.Children.Add(new Border { Name = "cw-plan", Child = g });
            if (p.Melody.Count > 20) sp.Children.Add(CwMelodyCurve(p));
        }

        // 3 Text
        var lyr = new TextBox { Name = "cw-lyrics", Text = _cwLyrics, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 110, MaxHeight = 220, Watermark = "Liedtext hier einfügen (mit [Strophe], [Refrain] …)" };
        lyr.TextChanged += (_, _) => _cwLyrics = lyr.Text ?? "";
        sp.Children.Add(new StackPanel { Spacing = 4, Children = { Step("3. Liedtext"), P(CoverStudio.LyricsHint, Muted, 11), lyr } });

        // 4 Stil, Stimme, Nähe, Klang
        var genre = new TextBox { Name = "cw-genre", Text = _cwGenre, Watermark = "Genre, z. B. Rockabilly, Akustik, Jazz (leer = wie das Original)", MinWidth = 260 };
        genre.TextChanged += (_, _) => _cwGenre = genre.Text ?? "";
        var voiceIds = CoverStudio.Voices.Select(v => v.Id).Concat(OwnVoiceReady ? ["eigen"] : []).ToList();
        var voiceLabels = CoverStudio.Voices.Select(v => v.Label).Concat(OwnVoiceReady ? ["Meine eigene Stimme"] : []).ToList();
        var voice = new ComboBox { Name = "cw-voice", ItemsSource = voiceLabels, SelectedIndex = Math.Max(0, voiceIds.IndexOf(_cwVoice)), MinWidth = 230 };
        voice.SelectionChanged += (_, _) => { _cwVoice = voiceIds[Math.Max(0, voice.SelectedIndex)]; Show(MS); };
        var sound = new ComboBox { Name = "cw-sound", ItemsSource = CoverStudio.Sounds.Select(v => v.Label).ToList(), SelectedIndex = Math.Max(0, Array.FindIndex(CoverStudio.Sounds, v => v.Id == _cwSound)), MinWidth = 230 };
        sound.SelectionChanged += (_, _) => _cwSound = CoverStudio.Sounds[Math.Max(0, sound.SelectedIndex)].Id;
        var near = new Slider { Name = "cw-closeness", Minimum = 0, Maximum = 1, Value = _cwCloseness, Width = 220 };
        near.ValueChanged += (_, _) => _cwCloseness = near.Value;
        var vInfo = CoverStudio.Voice(_cwVoice == "eigen" ? "frei" : _cwVoice);
        sp.Children.Add(new StackPanel { Spacing = 4, Children = {
            Step("4. Stil, Stimme und Klang"), genre,
            new WrapPanel { Children = { new StackPanel { Children = { P("Stimme (erfunden, keine echte Person)", Muted, 11), voice } }, new Border { Width = 10 }, new StackPanel { Children = { P("Klang wie damals", Muted, 11), sound } } } },
            P(_cwVoice == "eigen" ? "Das Cover wird in deiner gelernten Stimme gesungen." : vInfo.Id == "frei" ? "Eigene Wahl: die Stimme folgt deinem Genre-Text." : vInfo.Hint + " – Klang passt automatisch, außer du wählst oben einen anderen.", Muted, 11),
            MsOwnVoicePanel(),
            new StackPanel { Children = { P("Nähe zum Original – links freier, rechts genauer (Rhythmus, Höhen und Tiefen)", Muted, 11), near } } } });

        // 5 Erzeugen
        sp.Children.Add(new StackPanel { Spacing = 6, Children = { Step("5. Cover erzeugen und prüfen"),
            new WrapPanel { Children = { B("cw-make", _cwBusy ? "Cover entsteht …" : "Cover erzeugen", async () => { await MsCoverMake(); }, true), new Border { Width = 6 }, B("cw-karaoke", "Karaoke aus dem Original", async () => { await MsCoverKaraoke(); }) } } } });
        if (_cwMsg.Length > 0) sp.Children.Add(new TextBlock { Name = "cw-msg", Text = _cwMsg, Foreground = Text, FontSize = 12, TextWrapping = TextWrapping.Wrap });

        // Ampel je Versuch
        foreach (var (sid, rep, st) in _cwResults)
        {
            var song = Songs.Get(sid);
            var rows = new StackPanel { Spacing = 2 };
            rows.Children.Add(new TextBlock { Text = $"{LightDe(rep.Light)}  {song?.Title ?? sid} – {rep.Score * 100:0} % wie das Original (Nähe {st:0.00})", Foreground = Text, FontSize = 12, FontWeight = FontWeight.SemiBold });
            foreach (var pt in rep.Points)
                rows.Children.Add(new TextBlock { Text = $"{(pt.Light == "gruen" ? "🟢" : pt.Light == "gelb" ? "🟡" : "🔴")} {pt.Name}: {pt.Original} → {pt.Cover} · {pt.Text}", Foreground = Muted, FontSize = 11.5 });
            sp.Children.Add(new Border { Name = "cw-report", Background = TileBg, BorderBrush = Edge, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8), Padding = new Thickness(10, 6), Child = rows });
        }

        var box = new Expander { Name = "cw-box", IsExpanded = _cwOpen, HorizontalAlignment = HorizontalAlignment.Stretch, Padding = new Thickness(0, 6, 0, 0), Content = sp,
            Header = new StackPanel { Children = { new TextBlock { Text = "Cover-Werkstatt", Foreground = Text, FontSize = 13, FontWeight = FontWeight.SemiBold },
                new TextBlock { Text = "Nach deinem Original: gleicher Rhythmus, gleiche Höhen und Tiefen, Stimme und Klang einer Epoche, Karaoke", Foreground = Muted, FontSize = 11, TextWrapping = TextWrapping.Wrap } } } };
        box.PropertyChanged += (_, e) => { if (e.Property == Expander.IsExpandedProperty) _cwOpen = box.IsExpanded; };
        return box;
    }

    /// <summary>Melodieverlauf des Originals als kleine goldene Linie (Höhen und Tiefen sichtbar).</summary>
    static Control CwMelodyCurve(CoverStudio.Blueprint p)
    {
        const double W = 420, H = 64; // Zeichenmaß – die Linie wird auf die Breite der Spalte gestreckt
        var lo = p.MelodyLow; var hi = Math.Max(lo + 1, p.MelodyHigh);
        var geo = new StreamGeometry();
        using (var g = geo.Open())
        {
            var open = false;
            for (var i = 0; i < p.Melody.Count; i++)
            {
                if (p.Melody[i] is not { } m) { open = false; continue; }
                var pt = new Point(i * W / p.Melody.Count, H - 4 - (m - lo) / (hi - lo) * (H - 8));
                if (!open) { if (i > 0) g.EndFigure(false); g.BeginFigure(pt, false); open = true; } else g.LineTo(pt);
            }
            if (open) g.EndFigure(false);
        }
        return new Border { Name = "cw-melody", Height = H, Background = Brush.Parse("#0E1117"), CornerRadius = new CornerRadius(8), Padding = new Thickness(6, 4), HorizontalAlignment = HorizontalAlignment.Stretch,
            Child = new Avalonia.Controls.Shapes.Path { Data = geo, Stroke = Brush.Parse("#F5B83D"), StrokeThickness = 1.5, Stretch = Stretch.Fill } };
    }
}
