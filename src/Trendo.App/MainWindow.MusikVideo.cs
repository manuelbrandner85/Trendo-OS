using Avalonia.Platform.Storage;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Trendo.Core;

namespace Trendo.App;

/// <summary>Trendo Musik – Musikvideo (V1/V3/V4/V6/V7/V8, freigegeben 03.11.2026): Shotliste aus der Song-Analyse, echte Videoclips (eigene Videos
/// oder freies Filmmaterial von Pexels; die Video-KI folgt nach der Messung), Schnitt auf den Takt, Film-Look, Korn, Breitbild-Balken,
/// Liedtext eingeblendet, 16:9 und 9:16, Übergabe an den Videoschnitt. Am 03.11.2026 echt geprüft: 54 Einstellungen für 3:26 Minuten, 10 Clips in 43 s geschnitten.</summary>
public partial class MainWindow
{
    /// <summary>Tests: Abschnitts-Analyse ersetzen (Audiodatei → JSON).</summary>
    public Func<string, Task<string?>>? SectionsOverride { get; set; }
    /// <summary>Tests: Pexels-Abruf ersetzen (Adresse, Schlüssel → Antwort) und Herunterladen (Adresse, Ziel).</summary>
    public Func<Uri, string, Task<string?>>? PexelsFetchOverride { get; set; }
    public Func<Uri, string, Task<bool>>? StockDownloadOverride { get; set; }

    string _mvLook = "kino", _mvFormat = "16:9"; bool _mvBars = true, _mvLyrics = true; List<string> _mvClips = []; List<MusicVideo.Shot>? _mvShots; string? _mvFor;
    string _mvPexelsKey = ""; bool _mvBusy;
    const string PexelsSecret = "pexels-api-key";

    async Task<MusicVideo.Analysis?> SectionsOf(MusicSong s)
    {
        var cache = s.File + ".abschnitte.json";
        string? json = File.Exists(cache) ? await File.ReadAllTextAsync(cache) : null;
        if (json is null && SectionsOverride is { } o) json = await o(s.File);
        else if (json is null && AnalyseReady)
        {
            var r = await KiPyRun(KiRuntime.MusikAnalyse, ["--abschnitte", s.File, cache]);
            json = r.Ok && File.Exists(cache) ? await File.ReadAllTextAsync(cache) : null;
        }
        return json is null ? null : MusicVideo.Parse(json);
    }

    /// <summary>V1: Shotliste für das Lied erstellen.</summary>
    public async Task<List<MusicVideo.Shot>?> MsVideoPlan(string songId)
    {
        if (Songs.Get(songId) is not { } s) return null;
        if (SectionsOverride is null && !AnalyseReady && !File.Exists(s.File + ".abschnitte.json")) { _msMsg = "Fürs Musikvideo braucht Trendo die Song-Analyse (rechts beim Lied einrichten, einmalig ca. 300 MB)."; Show(MS); return null; }
        _msMsg = "Abschnitte und Takt werden erkannt …"; Show(MS);
        var a = await SectionsOf(s);
        if (a is null) { _msMsg = "Die Abschnitte des Lieds ließen sich nicht erkennen."; Show(MS); return null; }
        _mvShots = MusicVideo.Plan(a, s.Style.Length > 0 ? s.Style : "cinematic", s.TimeSignature == "3" ? 3 : 4); _mvFor = songId;
        _msMsg = $"Shotliste: {_mvShots.Count} Einstellungen, Schnitte auf dem Takt ({a.Bpm:0} BPM), {a.Sections.Count} Abschnitte.";
        Show(MS); return _mvShots;
    }

    /// <summary>V3: passende Clips von Pexels holen (eigener kostenloser Schlüssel, im Windows-Anmeldetresor).</summary>
    public async Task<int> MsVideoStock(string songId, int count = 12)
    {
        if (Songs.Get(songId) is not { } s) return 0;
        var key = _mvPexelsKey.Trim().Length > 0 ? _mvPexelsKey.Trim() : _core.Secrets.Get(PexelsSecret) ?? "";
        if (key.Length < 20) { _msMsg = "Für Pexels brauchst du einen eigenen kostenlosen Schlüssel (pexels.com/api) – hier eintragen."; Show(MS); return 0; }
        if (_core.Offline) { _msMsg = "Trendo ist offline."; Show(MS); return 0; }
        if (_mvPexelsKey.Trim().Length > 0) { _core.Secrets.Set(PexelsSecret, key); _mvPexelsKey = ""; }
        var portrait = _mvFormat == "9:16";
        var q = MusicVideo.StockQuery(s.Style);
        _msMsg = $"Pexels wird nach „{q}“ durchsucht …"; Show(MS);
        string? json;
        if (PexelsFetchOverride is { } o) json = await o(MusicVideo.PexelsSearch(q, portrait), key);
        else
        {
            using var http = MsWebClient(); http.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", key);
            try { using var r = await http.GetAsync(MusicVideo.PexelsSearch(q, portrait)); json = r.IsSuccessStatusCode ? await r.Content.ReadAsStringAsync() : null; }
            catch (Exception e) when (e is HttpRequestException or TaskCanceledException) { json = null; }
        }
        var clips = json is null ? [] : MusicVideo.ParsePexels(json, portrait ? 1080 : 1920);
        if (clips.Count == 0) { _msMsg = json is null ? "Pexels antwortet nicht (Schlüssel richtig?)." : "Keine passenden Clips gefunden – anderen Stil versuchen oder eigene Clips wählen."; Show(MS); return 0; }
        var dir = Path.Combine(WorkFolder("Musik"), DevStudio.SafeName(s.Title) + " – Video", "Clips"); Directory.CreateDirectory(dir);
        var got = 0;
        foreach (var c in clips.Take(count))
        {
            var f = Path.Combine(dir, $"pexels-{c.Id}.mp4");
            if (!File.Exists(f))
            {
                _msMsg = $"Clip {got + 1} von {Math.Min(count, clips.Count)} wird geladen …"; if (CurrentView == MS) Show(MS);
                var ok = StockDownloadOverride is { } d ? await d(new Uri(c.Url), f) : await DownloadTo(new Uri(c.Url), f);
                if (!ok) continue;
            }
            if (!_mvClips.Contains(f)) _mvClips.Add(f);
            got++;
        }
        File.WriteAllText(Path.Combine(dir, "QUELLEN.txt"), "Videos von Pexels (pexels.com) – Pexels-Lizenz, kostenlos nutzbar.\n" + string.Join("\n", clips.Take(count).Select(c => $"{c.Page} – {c.Author}")));
        _core.Audit.Append(Principal.LocalOwner.Name, "musik.video.pexels", $"{got} Clips");
        _msMsg = $"{got} Clips von Pexels geladen (Quellen in „QUELLEN.txt“)."; Show(MS);
        return got;
    }

    async Task<bool> DownloadTo(Uri u, string file)
    {
        try
        {
            using var http = MsWebClient(); http.Timeout = TimeSpan.FromMinutes(5);
            using var r = await http.GetAsync(u, HttpCompletionOption.ResponseHeadersRead);
            if (!r.IsSuccessStatusCode) return false;
            var tmp = file + ".part";
            await using (var o = File.Create(tmp)) await (await r.Content.ReadAsStreamAsync()).CopyToAsync(o);
            File.Move(tmp, file, true); return true;
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or IOException) { return false; }
    }

    /// <summary>V4/V6/V8: Video rendern – je Einstellung ein Clip (Ausschnitte wechselnd aus den Quellen), Look, Korn, Balken, Liedtext, Lied darunter.</summary>
    public async Task<List<string>> MsVideoRender(string songId)
    {
        var made = new List<string>();
        if (Songs.Get(songId) is not { } s) return made;
        if (_mvShots is null || _mvFor != songId) { if (await MsVideoPlan(songId) is null) return made; }
        var clips = _mvClips.Where(File.Exists).ToList();
        if (clips.Count == 0) { _msMsg = "Zuerst Videoclips wählen (eigene Videos oder Pexels)."; Show(MS); return made; }
        var ff = await MasterFfmpeg();
        if (ff is null) { _msMsg = "Fürs Musikvideo braucht Trendo FFmpeg – unter „Programme“ einrichten."; Show(MS); return made; }
        _mvBusy = true;
        try
        {
            var dir = Path.Combine(WorkFolder("Musik"), DevStudio.SafeName(s.Title) + " – Video"); Directory.CreateDirectory(dir);
            var lut = Path.Combine(dir, $"look-{_mvLook}.cube"); await File.WriteAllTextAsync(lut, MusicVideo.Cube(_mvLook));
            var formats = _mvFormat switch { "9:16" => new[] { (1080, 1920) }, "beide" => [(1920, 1080), (1080, 1920)], _ => [(1920, 1080)] };
            // Liedtext-Zeiten aus der Gesangsspur (wenn getrennt)
            List<LyricTiming.Line>? lyr = null;
            if (_mvLyrics && LyricTiming.Lines(s.Lyrics) is { Count: > 0 } lines && StemsOf(s).Vocal is { } voc && await MelodyOf(voc) is { } mel)
                lyr = LyricTiming.Align(lines, LyricTiming.Phrases(mel));
            var lengths = new Dictionary<string, double>();
            var rnd = new Random(s.Id.GetHashCode());
            foreach (var (w, h) in formats)
            {
                var tag = w > h ? "16x9" : "9x16"; var shotDir = Path.Combine(dir, "Einstellungen " + tag); Directory.CreateDirectory(shotDir);
                var filter = MusicVideo.Filter(w, h, lut, _mvBars);
                var parts = new List<string>();
                foreach (var shot in _mvShots!)
                {
                    var src = clips[shot.Index % clips.Count];
                    var srcLen = lengths.TryGetValue(src, out var l) ? l : lengths[src] = MasterRunOverride is null ? ProbeSeconds(ff, src) : 0;
                    var inPoint = srcLen > shot.Length + 0.5 ? Math.Round(rnd.NextDouble() * (srcLen - shot.Length - 0.2), 2) : 0;
                    var o = Path.Combine(shotDir, $"{shot.Index + 1:000}.mp4");
                    if (CurrentView == MS && _msRunningText is { } rt) rt.Text = $"Einstellung {shot.Index + 1} von {_mvShots.Count} ({tag}) …";
                    _msMsg = $"Einstellung {shot.Index + 1} von {_mvShots.Count} ({tag}) …";
                    var r = await MasterRun(ff, MusicVideo.ClipArgs(src, inPoint, shot.Length, filter, o));
                    if (!r.Ok || !File.Exists(o)) { _msMsg = "Ein Clip ließ sich nicht schneiden: " + Short(r.Err); Show(MS); return made; }
                    parts.Add(o);
                }
                var list = Path.Combine(shotDir, "liste.txt"); await File.WriteAllTextAsync(list, MusicVideo.ConcatList(parts));
                string? ass = null;
                if (lyr is { Count: > 0 }) { ass = Path.Combine(dir, $"liedtext-{tag}.ass"); await File.WriteAllTextAsync(ass, LyricTiming.Ass(lyr, w, h)); }
                var outFile = Path.Combine(dir, $"{DevStudio.SafeName(s.Title)} – Musikvideo {tag}.mp4");
                var j = await MasterRun(ff, MusicVideo.JoinArgs(list, s.File, outFile, ass));
                if (!j.Ok || !File.Exists(outFile)) { _msMsg = "Zusammenfügen ging nicht: " + Short(j.Err); Show(MS); return made; }
                made.Add(outFile);
            }
            _core.Audit.Append(Principal.LocalOwner.Name, "musik.video", $"{s.Id} {string.Join(",", formats.Select(f => $"{f.Item1}x{f.Item2}"))} {_mvLook}");
            _msMsg = $"Musikvideo fertig ({string.Join(" und ", made.Select(m => m.Contains("9x16") ? "9:16" : "16:9"))})" + (lyr is null && _mvLyrics ? " – Liedtext erst nach „Spuren trennen“ einblendbar." : ".");
            Show(MS);
            return made;
        }
        finally { _mvBusy = false; }
    }

    static double ProbeSeconds(string ffmpeg, string file)
    {
        var r = Cli.Run(ffmpeg, ["-hide_banner", "-i", file], null, 30000);
        var m = System.Text.RegularExpressions.Regex.Match(r.Err, @"Duration: (\d+):(\d+):(\d+(?:\.\d+)?)");
        return m.Success ? int.Parse(m.Groups[1].Value) * 3600 + int.Parse(m.Groups[2].Value) * 60 + double.Parse(m.Groups[3].Value, System.Globalization.CultureInfo.InvariantCulture) : 0;
    }

    /// <summary>V7: fertiges Video zum Feinschliff in den Videoschnitt.</summary>
    void MsVideoToEdit(string file, double seconds)
    {
        Show("schnitt");
        if (ProgramActive("schnitt") is null) ProgramNew("schnitt");
        try { EditAdd(file, length: seconds > 0 ? seconds : null); }
        catch (InvalidOperationException e) { _progMsg["schnitt"] = e.Message; Show("schnitt"); return; }
        _progMsg["schnitt"] = $"„{Path.GetFileNameWithoutExtension(file)}“ liegt auf der Zeitleiste – hier fein schneiden, Titel setzen, exportieren.";
        Show("schnitt");
    }

    Control MsVideoPanel(MusicSong s)
    {
        var sp = new StackPanel { Name = "ms-video-panel", Spacing = 4 };
        sp.Children.Add(H("Schnelles Musikvideo aus Stockvideos (ohne KI-Darsteller)", 13));
        sp.Children.Add(P("Echte Videoclips, geschnitten auf den Takt, mit Kamera-Look wie im Kino. Clips: eigene Videos oder freies Filmmaterial von Pexels.", Muted, 11));
        var look = new ComboBox { Name = "ms-video-look", ItemsSource = MusicVideo.Looks.Select(l => "Look: " + l.Name).ToArray(), SelectedIndex = Math.Max(0, Array.FindIndex(MusicVideo.Looks, l => l.Id == _mvLook)) };
        look.SelectionChanged += (_, _) => _mvLook = MusicVideo.Looks[Math.Max(0, look.SelectedIndex)].Id;
        var fmt = new ComboBox { Name = "ms-video-format", ItemsSource = new[] { "16:9 (YouTube)", "9:16 (Shorts, Reels)", "beide" }, SelectedIndex = _mvFormat switch { "9:16" => 1, "beide" => 2, _ => 0 } };
        fmt.SelectionChanged += (_, _) => _mvFormat = fmt.SelectedIndex switch { 1 => "9:16", 2 => "beide", _ => "16:9" };
        sp.Children.Add(new WrapPanel { Children = { look, new Border { Width = 6 }, fmt } });
        var bars = new CheckBox { Name = "ms-video-bars", Content = "Breitbild-Balken", IsChecked = _mvBars, Foreground = Text, FontSize = 11.5 }; bars.IsCheckedChanged += (_, _) => _mvBars = bars.IsChecked == true;
        var lyr = new CheckBox { Name = "ms-video-lyrics", Content = "Liedtext einblenden", IsChecked = _mvLyrics, Foreground = Text, FontSize = 11.5 }; lyr.IsCheckedChanged += (_, _) => _mvLyrics = lyr.IsChecked == true;
        sp.Children.Add(new WrapPanel { Children = { bars, new Border { Width = 10 }, lyr } });
        var own = Act("ms-video-own", "Eigene Videoclips …", async () =>
        {
            var got = PickOverride is { } fake ? await fake(new PickRequest("ms-video-own", false, true, ["mp4", "mov", "mkv", "webm"]))
                : (await StorageProvider.OpenFilePickerAsync(new Avalonia.Platform.Storage.FilePickerOpenOptions { Title = "Videoclips wählen", AllowMultiple = true,
                    FileTypeFilter = [new Avalonia.Platform.Storage.FilePickerFileType("Videos") { Patterns = ["*.mp4", "*.mov", "*.mkv", "*.webm"] }] })).Select(f => f.TryGetLocalPath()).OfType<string>().ToArray();
            foreach (var f in got ?? []) if (!_mvClips.Contains(f)) _mvClips.Add(f);
            Show(MS);
        });
        var key = new TextBox { Name = "ms-video-pexels-key", Watermark = _core.Secrets.Get(PexelsSecret) is { Length: > 0 } ? "Pexels-Schlüssel gespeichert" : "Pexels-Schlüssel (kostenlos)", Text = _mvPexelsKey, Width = 170, PasswordChar = '•' };
        key.TextChanged += (_, _) => _mvPexelsKey = key.Text ?? "";
        var stock = Act("ms-video-pexels", "Clips von Pexels", () => _ = MsVideoStock(s.Id));
        sp.Children.Add(new WrapPanel { Children = { own, new Border { Width = 6 }, key, new Border { Width = 6 }, stock } });
        if (_mvClips.Count > 0)
            sp.Children.Add(new WrapPanel { Children = { new TextBlock { Name = "ms-video-clips", Text = $"{_mvClips.Count} Clips gewählt", Foreground = Muted, FontSize = 11, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center },
                new Border { Width = 6 }, Act("ms-video-clear", "leeren", () => { _mvClips.Clear(); Show(MS); }) } });
        sp.Children.Add(new WrapPanel { Children = { Act("ms-video-plan", "Shotliste", () => _ = MsVideoPlan(s.Id)), new Border { Width = 6 },
            Act("ms-video-render", _mvBusy ? "Wird gerendert …" : "Video erstellen", () => { if (!_mvBusy) _ = MsVideoRender(s.Id); }) } });
        if (_mvShots is { } shots && _mvFor == s.Id)
        {
            var list = new StackPanel { Name = "ms-video-shots", Spacing = 1 };
            foreach (var sh in shots.Take(8))
                list.Children.Add(new TextBlock { Text = $"{SongTime(sh.Start)}  {sh.Length:0.0} s  {sh.Part} · {sh.Camera}", Foreground = Muted, FontSize = 10.5 });
            if (shots.Count > 8) list.Children.Add(new TextBlock { Text = $"… und {shots.Count - 8} weitere", Foreground = Muted, FontSize = 10.5 });
            sp.Children.Add(list);
        }
        var dirV = Path.Combine(WorkFolder("Musik"), DevStudio.SafeName(s.Title) + " – Video");
        if (Directory.Exists(dirV) && Directory.GetFiles(dirV, "*Musikvideo*.mp4") is { Length: > 0 } vids)
            foreach (var v in vids)
            {
                var vf = v;
                sp.Children.Add(new WrapPanel { Children = { new TextBlock { Text = Path.GetFileName(vf), Foreground = Text, FontSize = 11, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center },
                    new Border { Width = 6 }, Act("ms-video-open-" + (vf.Contains("9x16") ? "9x16" : "16x9"), "Öffnen", () => Launch(vf)), new Border { Width = 4 },
                    Act("ms-video-edit-" + (vf.Contains("9x16") ? "9x16" : "16x9"), "In den Videoschnitt", () => MsVideoToEdit(vf, s.Seconds)) } });
            }
        return sp;
    }
}
