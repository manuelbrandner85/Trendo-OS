using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Trendo.Core;
using Trendo.Core.Office;

namespace Trendo.App;

/// <summary>Trendo Musik – Ausbau M3/M4/M5/M7/M9 (freigegeben 03.11.2026): eigene Aufnahme hinzufügen und als Cover neu einspielen (nur privat),
/// eingefügten Liedtext gliedern, Song-Analyse (Tonart, Tempo, Takt, Akkorde, Tonumfang – lokal mit librosa), Tonart/Tempo bekannter Songs aus
/// freien Daten (AcousticBrainz CC0, MusicBrainz) und Text-Werkzeuge (Silben, Reime, Refrain).</summary>
public partial class MainWindow
{
    /// <summary>Tests: Song-Analyse ersetzen (Audiodatei → JSON).</summary>
    public Func<string, Task<string?>>? SongAnalyseOverride { get; set; }
    /// <summary>Tests: Abruf für M7 ersetzen (Adresse → Antwort oder null).</summary>
    public Func<Uri, Task<string?>>? SongFactsFetchOverride { get; set; }
    bool _msShowSyllables; string _msRhymeWord = "", _msFactsQuery = ""; List<string> _msRhymes = []; SongTools.SongFacts? _msFacts; string _msFactsMsg = "";
    HashSet<string>? _dictWords;

    KiLibraryModel AnalyseModel => KiLibrary.Models.First(m => m.Id == "musik-analyse");
    bool AnalyseReady => SongAnalyseOverride is not null || KiStore.RuntimeReady(KiRuntime.MusikAnalyse);

    /// <summary>M5: Lied oder eigene Datei untersuchen. Gibt das Ergebnis zurück (und speichert es beim Lied).</summary>
    public async Task<SongAnalysis?> MsAnalyse(string file, string? songId = null)
    {
        _msMsg = "Das Lied wird untersucht …"; if (CurrentView == MS) Show(MS);
        string? json = null;
        try
        {
            if (SongAnalyseOverride is { } o) json = await o(file);
            else
            {
                var outFile = Path.Combine(Path.GetTempPath(), $"trendo-analyse-{Guid.NewGuid():N}.json");
                var r = await KiPyRun(KiRuntime.MusikAnalyse, [file, outFile]);
                if (r.Ok && File.Exists(outFile)) { json = await File.ReadAllTextAsync(outFile); File.Delete(outFile); }
                else _msMsg = "Analyse ging nicht: " + Short(r.Err.Length > 0 ? r.Err : r.Out);
            }
        }
        catch (Exception e) when (e is IOException or InvalidOperationException) { _msMsg = "Analyse ging nicht: " + e.Message; }
        var a = json is null ? null : SongAnalysis.Parse(json);
        if (a is not null)
        {
            if (songId is not null) Songs.Update(songId, s => s.Analysis = a);
            _msMsg = $"✓ {a.KeyDe} · {a.Bpm:0} BPM · {a.Meter}" + (a.Range is { } rg ? $" · Tonumfang {rg.Low}–{rg.High} ({rg.Voice})" : "");
            _core.Audit.Append(Principal.LocalOwner.Name, "musik.analyse", Path.GetFileName(file));
        }
        if (CurrentView == MS) Show(MS);
        return a;
    }

    /// <summary>Bereich „Analyse“ im Lied.</summary>
    Control MsAnalysePanel(MusicSong s)
    {
        var sp = new StackPanel { Name = "ms-analyse-panel", Spacing = 5 };
        sp.Children.Add(H("Analyse", 13));
        if (s.Analysis is { } a)
        {
            sp.Children.Add(new TextBlock { Name = "ms-analyse-result", Foreground = Text, FontSize = 12.5, TextWrapping = TextWrapping.Wrap,
                Text = $"Tonart {a.KeyDe} · {a.Bpm:0} BPM · {a.Meter}-Takt" + (a.Range is { } r ? $"\nTonumfang {r.Low}–{r.High} ({r.Semitones} Halbtöne) · Stimmlage {r.Voice}" : "") +
                       (a.Chords.Count > 0 ? "\nAkkorde: " + string.Join(" – ", a.Chords.Take(12)) : "") });
            sp.Children.Add(P("Takt und Akkorde sind Schätzungen aus dem Klang.", Muted, 10.5));
            sp.Children.Add(Act("ms-analyse-take", "Als Vorlage für ein neues Lied", () =>
            {
                _msPro = true; _msBpm = Math.Round(a.Bpm).ToString(System.Globalization.CultureInfo.InvariantCulture);
                _msKey = AceStepClient.Keys.Contains(a.Key) ? a.Key : ""; _msTime = a.MeterValue;
                if (_msStyle.Trim().Length == 0) _msStyle = s.Style;
                _msMsg = $"Tonart, Tempo und Takt von „{s.Title}“ übernommen – links im Profi-Modus."; Show(MS);
            }));
        }
        else if (AnalyseReady) sp.Children.Add(Act("ms-analyse", "🔍 Analysieren (Tonart, Tempo, Akkorde, Tonumfang)", () => _ = MsAnalyse(s.File, s.Id)));
        else sp.Children.Add(Act("ms-analyse-setup", "Song-Analyse einrichten (einmalig, ca. 300 MB)", async () =>
        {
            try { await KiPyInstall(AnalyseModel, KiPyApps.For(KiRuntime.MusikAnalyse)!); _msMsg = "✓ Song-Analyse ist eingerichtet."; }
            catch (Exception e) when (e is InvalidOperationException or IOException or HttpRequestException or OperationCanceledException) { _msMsg = "Einrichten ging nicht: " + e.Message; }
            Show(MS);
        }));
        if (s.Kind == "eigen")
            sp.Children.Add(P("Eigene Aufnahme. Ein Cover davon (unten „In anderem Stil“) nur privat nutzen – wer ein Cover fremder Lieder veröffentlicht, braucht die Rechte (in Deutschland über die GEMA).", Brush.Parse("#FACC15"), 11));
        return sp;
    }

    /// <summary>M3: eigene Aufnahme (gekaufte Datei, eigener Mitschnitt) in die Bibliothek – zum Analysieren und für ein Cover in anderem Stil.</summary>
    /// <summary>Alle gängigen Musik- und Videoformate (Wunsch 07.11.2026: „egal welche Art von Musik“). Was Trendo nicht selbst liest, wandelt FFmpeg beim Hochladen in WAV.</summary>
    public static readonly string[] MusicImportExt = ["mp3", "wav", "flac", "m4a", "aac", "ogg", "opus", "wma", "aif", "aiff", "alac", "mp4", "m4v", "mov", "mkv", "webm", "avi"];
    static readonly string[] MusicDirect = [".mp3", ".wav", ".flac"];

    /// <summary>Dateiauswahl für Musik: startet im Musik-Ordner, zeigt alle Musik- und Videoformate (plus „Alle Dateien“).</summary>
    async Task<string?> MsPickMusic()
    {
        var req = new PickRequest("ms-import", false, false, MusicImportExt);
        if (PickOverride is { } fake) return (await fake(req)) is [var f, ..] ? f : null;
        var start = await StorageProvider.TryGetWellKnownFolderAsync(Avalonia.Platform.Storage.WellKnownFolder.Music);
        var got = await StorageProvider.OpenFilePickerAsync(new Avalonia.Platform.Storage.FilePickerOpenOptions
        {
            Title = "Musik oder Video wählen", SuggestedStartLocation = start,
            FileTypeFilter = [new Avalonia.Platform.Storage.FilePickerFileType("Musik und Videos (MP3, WAV, FLAC, M4A, AAC, OGG, OPUS, WMA, MP4 …)") { Patterns = [.. MusicImportExt.Select(e => "*." + e)] },
                              new Avalonia.Platform.Storage.FilePickerFileType("Alle Dateien") { Patterns = ["*"] }],
        });
        return got.Select(f => f.TryGetLocalPath()).OfType<string>().FirstOrDefault();
    }

    public async Task<MusicSong?> MsImport(string? file = null)
    {
        file ??= await MsPickMusic();
        if (file is null || !File.Exists(file)) return null;
        var dir = WorkFolder("Musik");
        var ext = Path.GetExtension(file).ToLowerInvariant(); var convert = !MusicDirect.Contains(ext);
        var target = Path.Combine(dir, Path.GetFileNameWithoutExtension(file) + (convert ? ".wav" : ext));
        for (var n = 2; File.Exists(target); n++) target = Path.Combine(dir, $"{Path.GetFileNameWithoutExtension(file)} ({n}){(convert ? ".wav" : ext)}");
        if (convert)
        {
            // M4A, AAC, OPUS, WMA, Videos …: nur die Tonspur als WAV (44,1 kHz, Stereo) – damit funktionieren Analyse, Spuren trennen und Bearbeiten
            Func<IReadOnlyList<string>, Cli.Result> run;
            if (FfmpegRunOverride is { } fake) run = fake;
            else if (FfmpegPath is not { } ff) { _msMsg = $"„{ext}“ braucht FFmpeg zum Umwandeln – unter Zusatzprogramme einrichten (MP3, WAV und FLAC gehen ohne)."; Show(MS); return null; }
            else if (_opt.Isolated) { _msMsg = "Im abgeschotteten Testmodus startet Trendo kein FFmpeg."; Show(MS); return null; }
            else run = a => Cli.Run(ff, a, dir, 600_000);
            var src = file; var dst = target;
            var r = await Task.Run(() => run(["-hide_banner", "-loglevel", "error", "-y", "-i", src, "-vn", "-ac", "2", "-ar", "44100", "-c:a", "pcm_s16le", dst]));
            if (!r.Ok || !File.Exists(target) || new FileInfo(target).Length < 100)
            {
                try { File.Delete(target); } catch (IOException) { }
                _msMsg = $"„{Path.GetFileName(file)}“ hat keine lesbare Tonspur." + (r.Err.Length > 0 ? " (" + r.Err.Trim().Split('\n')[^1] + ")" : ""); Show(MS); return null;
            }
        }
        else File.Copy(file, target);
        var s = Songs.Add(new MusicSong { Title = Path.GetFileNameWithoutExtension(file), Style = "", File = target, Kind = "eigen", Seconds = 0 });
        _msSel = s.Id; _msMsg = "✓ Aufnahme hinzugefügt – jetzt analysieren oder rechts „In anderem Stil“ ein Cover machen (nur privat).";
        _core.Audit.Append(Principal.LocalOwner.Name, "musik.import", Path.GetFileName(target));
        if (AnalyseReady) _ = MsAnalyse(target, s.Id);
        Show(MS);
        return s;
    }

    /// <summary>Werkzeuge unter dem Liedtext (Profi): gliedern, Refrain wiederholen, Silben, Reime, Refrain eingängiger.</summary>
    Control MsLyricsTools(Action sync)
    {
        var sp = new StackPanel { Name = "ms-lyrics-tools", Spacing = 4 };
        var row = new WrapPanel();
        Button B(string name, string label, string tip, Action go) { var b = new Button { Name = name, Content = label, FontSize = 11.5, Padding = new Thickness(8, 2), Margin = new Thickness(0, 0, 4, 4) }; ToolTip.SetTip(b, tip); b.Click += (_, _) => { sync(); go(); }; return b; }
        row.Children.Add(B("ms-lyrics-structure", "Gliedern", "Eingefügten Text in Strophe, Refrain und Bridge gliedern (Absätze mit Leerzeile)", () => { _msLyrics = SongTools.Structure(_msLyrics); _msMsg = "Text gegliedert – prüfe die Abschnitte."; Show(MS); }));
        row.Children.Add(B("ms-lyrics-repeat", "Refrain wiederholen", "Den letzten Refrain am Ende noch einmal", () =>
        { try { _msLyrics = SongTools.RepeatChorus(_msLyrics); _msMsg = ""; } catch (ArgumentException e) { _msMsg = e.Message; } Show(MS); }));
        row.Children.Add(B("ms-lyrics-syllables", _msShowSyllables ? "Silben aus" : "Silben zählen", "Silben je Zeile – gleich lange Zeilen singen sich besser", () => { _msShowSyllables = !_msShowSyllables; Show(MS); }));
        row.Children.Add(B("ms-lyrics-floskeln", "KI-Floskeln prüfen", "Sucht abgegriffene KI-Phrasen wie „Neonlicht“ oder „Herz in Flammen“", MsCheckFloskeln));
        row.Children.Add(B("ms-lyrics-catchy", "✳ Refrain eingängiger", "Die KI macht den Refrain einprägsamer – der Rest bleibt", () => _ = MsCatchyChorus()));
        sp.Children.Add(row);
        if (_msShowSyllables)
        {
            var list = new StackPanel { Name = "ms-syllables", Spacing = 0 };
            foreach (var (line, n) in SongTools.SyllablesPerLine(_msLyrics).Take(40))
                list.Children.Add(new TextBlock { Text = $"{n,2}  {line}", FontFamily = new FontFamily("Consolas, monospace"), FontSize = 11, Foreground = Muted, TextTrimming = TextTrimming.CharacterEllipsis });
            sp.Children.Add(new ScrollViewer { MaxHeight = 140, Content = list });
        }
        var rw = new TextBox { Name = "ms-rhyme-word", Watermark = "Reim auf …", Text = _msRhymeWord, Width = 130 };
        var rg = new Button { Name = "ms-rhyme-go", Content = "Reime finden", FontSize = 11.5, Padding = new Thickness(8, 2) };
        rg.Click += (_, _) => { _msRhymeWord = rw.Text ?? ""; _msRhymes = MsRhymes(_msRhymeWord); _msMsg = _msRhymes.Count == 0 ? "Keine Reime gefunden (das deutsche Wörterbuch kommt mit LibreOffice)." : ""; Show(MS); };
        sp.Children.Add(new WrapPanel { Children = { rw, new Border { Width = 6 }, rg } });
        if (_msRhymes.Count > 0) sp.Children.Add(new TextBlock { Name = "ms-rhymes", Text = string.Join(" · ", _msRhymes), Foreground = Text, FontSize = 12, TextWrapping = TextWrapping.Wrap });
        sp.Children.Add(P("Fremde Liedtexte nur privat nutzen – wer sie veröffentlicht, braucht die Rechte. Trendo liefert keine geschützten Texte mit.", Muted, 10.5));
        return sp;
    }

    /// <summary>Reime aus dem deutschen Rechtschreib-Wörterbuch (Hunspell, mit LibreOffice).</summary>
    List<string> MsRhymes(string word)
    {
        if (word.Trim().Length < 2) return [];
        if (_dictWords is null)
        {
            var dic = Spelling.Candidates(_deps.ToolsRoot).FirstOrDefault(File.Exists);
            _dictWords = dic is null ? [] : File.ReadLines(dic).Skip(1).Select(l => l.Split('/')[0].Trim()).Where(w => w.Length is > 1 and < 16).ToHashSet(StringComparer.OrdinalIgnoreCase);
        }
        return SongTools.Rhymes(word, _dictWords);
    }

    async Task MsCatchyChorus()
    {
        if (!_msLyrics.Contains("[Refrain]", StringComparison.OrdinalIgnoreCase)) { _msMsg = "Im Text gibt es noch keinen [Refrain]."; Show(MS); return; }
        var prompt = "Mache nur den Refrain dieses Liedtexts eingängiger (kurze Zeilen, Wiederholung, klarer Hook) – alle anderen Abschnitte bleiben Wort für Wort. " +
                     "Keine Zeilen aus bekannten Liedern. Gib den ganzen Liedtext mit den Abschnitts-Marken zurück.\n\n" + _msLyrics;
        _msMsg = "Die KI arbeitet am Refrain …"; Show(MS);
        try
        {
            var t = MusicLyricsOverride is { } o ? await o(prompt) : await Intel.AskAsync(new KiRequest(prompt, PreferredModel: KiModelName, UseKnowledge: false, AllowRemote: true)) is { Ok: true } a ? a.Text : null;
            if (t is { Length: > 20 } && t.Contains('[')) { _msLyrics = t.Trim().Trim('`').Trim(); _msMsg = "✓ Refrain überarbeitet – lies ihn gegen."; }
            else _msMsg = "Dafür braucht es eine Text-KI (unter „KI einsetzen“ einrichten).";
        }
        catch (Exception e) when (e is InvalidOperationException or HttpRequestException or TaskCanceledException) { _msMsg = "Ging nicht: " + e.Message; }
        Show(MS);
    }

    static HttpClient MsWebClient()
    {
        var http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("TrendoOS/0.9 ( https://github.com/manuelbrandner85/Trendo-OS )"); // MusicBrainz und Wikipedia verlangen eine Kennung
        return http;
    }

    async Task<string?> MsWebGet(HttpClient http, Uri u)
    {
        if (SongFactsFetchOverride is { } o) return await o(u);
        try { using var r = await http.GetAsync(u); return r.IsSuccessStatusCode ? await r.Content.ReadAsStringAsync() : null; }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException) { return null; }
    }

    /// <summary>M7: Tonart und Tempo eines bekannten Songs aus freien Daten (MusicBrainz sucht, AcousticBrainz misst – CC0). Kein Liedtext.</summary>
    public async Task<SongTools.SongFacts?> MsSongFacts(string query)
    {
        _msFacts = null;
        if (query.Trim().Length < 3) { _msFactsMsg = "Gib Künstler und Titel ein, z. B. „Queen – Bohemian Rhapsody“."; Show(MS); return null; }
        if (_core.Offline) { _msFactsMsg = "Trendo ist offline – dafür braucht es Internet."; Show(MS); return null; }
        using var http = MsWebClient();
        Task<string?> Get(Uri u) => MsWebGet(http, u);
        var parts = query.Split(['–', '-', '—'], 2, StringSplitOptions.TrimEntries);
        var q = parts.Length == 2 ? $"recording:\"{parts[1]}\" AND artist:\"{parts[0]}\"" : $"recording:\"{query.Trim()}\"";
        _msFactsMsg = "Suche …"; Show(MS);
        var mb = await Get(new Uri("https://musicbrainz.org/ws/2/recording/?fmt=json&limit=5&query=" + Uri.EscapeDataString(q)));
        var recs = mb is null ? [] : SongTools.ParseMusicBrainz(mb);
        if (recs.Count == 0) { _msFactsMsg = mb is null ? "MusicBrainz antwortet gerade nicht – später noch einmal." : "Kein passender Song gefunden."; Show(MS); return null; }
        foreach (var rec in recs.Take(5))
            if (await Get(new Uri($"https://acousticbrainz.org/api/v1/{rec.Mbid}/low-level")) is { } ab && SongTools.ParseAcousticBrainz(ab) is { } f)
            {
                _msFacts = f;
                _msFactsMsg = $"„{rec.Title}“ – {rec.Artist}{(rec.Year is { } y ? $" ({y})" : "")}: {SongTools.KeyDe(f.Key)}, {f.Bpm:0} BPM · Quelle: {f.Source}. Gemessen, nicht immer exakt.";
                _core.Audit.Append(Principal.LocalOwner.Name, "musik.songwissen", rec.Mbid);
                Show(MS); return f;
            }
        _msFactsMsg = $"„{recs[0].Title}“ gefunden, aber AcousticBrainz hat keine Messung (oder antwortet gerade nicht – der Dienst läuft nur noch als Archiv). " +
                      "Hast du die Aufnahme? Dann „＋ Eigene Aufnahme“ und „Analysieren“.";
        Show(MS); return null;
    }

    /// <summary>Bereich M7 im Profi-Modus.</summary>
    string _msYouTube = "";

    /// <summary>Y1 (freigegeben 03.11.2026): YouTube-Link → Interpret und Titel (offizielles oEmbed, kein Download, kein Liedtext) →
    /// Tonart/Tempo aus freien Daten und Stil aus dem Web; das Lied entsteht neu im gewählten Stil, den Text fügt man selbst ein.</summary>
    public async Task<(string Artist, string Song)?> MsFromYouTube(string link)
    {
        if (YouTubeLink.Normalize(link) is not { } video) { _msFactsMsg = "Das ist kein YouTube-Link (z. B. https://www.youtube.com/watch?v=… oder https://youtu.be/…)."; Show(MS); return null; }
        if (_core.Offline) { _msFactsMsg = "Trendo ist offline – dafür braucht es Internet."; Show(MS); return null; }
        _msFactsMsg = "YouTube wird gefragt …"; Show(MS);
        using var http = MsWebClient();
        if (await MsWebGet(http, YouTubeLink.OEmbedUri(video)) is not { } j || YouTubeLink.ParseOEmbed(j) is not { } o)
        { _msFactsMsg = "YouTube kennt dieses Video nicht (oder es ist privat)."; Show(MS); return null; }
        var (artist, song) = YouTubeLink.Split(o.Title, o.Channel);
        _core.Audit.Append(Principal.LocalOwner.Name, "musik.youtube", video.Query);
        _msTitle = song.Length > 0 ? song + " (neu)" : _msTitle;
        _msFactsQuery = artist.Length > 0 ? $"{artist} – {song}" : song;
        _msPro = true;
        if (artist.Length > 1) { _msLike = artist; await MsStyleLike(artist); }
        await MsSongFacts(_msFactsQuery);
        _msMsg = $"„{song}“ von {(artist.Length > 0 ? artist : "unbekannt")} erkannt. Stil und – wenn bekannt – Tonart und Tempo sind eingesetzt. " +
                 "Den Liedtext fügst du selbst ein (die Text-Werkzeuge gliedern ihn) oder lässt die KI einen eigenen schreiben. Von YouTube wird nichts heruntergeladen; ein Cover fremder Lieder nur privat nutzen.";
        Show(MS);
        return (artist, song);
    }

    Control MsFactsPanel(Action sync)
    {
        var yt = new TextBox { Name = "ms-youtube", Watermark = "YouTube-Link eines Songs", Text = _msYouTube, MinWidth = 200 };
        yt.TextChanged += (_, _) => _msYouTube = yt.Text ?? "";
        var ytGo = new Button { Name = "ms-youtube-go", Content = "Als Vorlage", FontSize = 11.5, Padding = new Thickness(8, 2) };
        ToolTip.SetTip(ytGo, "Trendo liest nur Titel und Interpret (offizielle YouTube-Schnittstelle) und holt Stil, Tonart und Tempo aus freien Daten. Es wird nichts heruntergeladen.");
        ytGo.Click += (_, _) => { sync(); _msYouTube = yt.Text ?? ""; _ = MsFromYouTube(_msYouTube); };
        var q = new TextBox { Name = "ms-facts-query", Watermark = "Bekannter Song: Künstler – Titel", Text = _msFactsQuery, MinWidth = 200 };
        q.TextChanged += (_, _) => _msFactsQuery = q.Text ?? "";
        var go = new Button { Name = "ms-facts-go", Content = "Tonart & Tempo", FontSize = 11.5, Padding = new Thickness(8, 2) };
        go.Click += (_, _) => { sync(); _msFactsQuery = q.Text ?? ""; _ = MsSongFacts(_msFactsQuery); };
        var sp = new StackPanel { Name = "ms-facts", Spacing = 4, Children = { new WrapPanel { Children = { yt, new Border { Width = 6 }, ytGo } }, new WrapPanel { Children = { q, new Border { Width = 6 }, go } } } };
        if (_msFactsMsg.Length > 0) sp.Children.Add(new TextBlock { Name = "ms-facts-result", Text = _msFactsMsg, Foreground = Muted, FontSize = 11.5, TextWrapping = TextWrapping.Wrap });
        if (_msFacts is { } f)
            sp.Children.Add(new Button { Name = "ms-facts-take", Content = "Übernehmen", FontSize = 11.5, Padding = new Thickness(8, 2) }.Also(b => b.Click += (_, _) =>
            {
                sync(); _msBpm = Math.Round(f.Bpm).ToString(System.Globalization.CultureInfo.InvariantCulture); _msKey = AceStepClient.Keys.Contains(f.Key) ? f.Key : _msKey;
                _msMsg = "Tonart und Tempo übernommen – Melodie und Text werden natürlich neu."; Show(MS);
            }));
        return sp;
    }
}
