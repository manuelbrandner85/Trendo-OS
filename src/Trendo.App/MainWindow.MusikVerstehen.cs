using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Trendo.Core;

namespace Trendo.App;

/// <summary>KI-Musikvideo, Baustein A (freigegeben 04.11.2026): „Lied verstehen“. Trendo trennt den Gesang ab, erkennt Takt und Aufbau,
/// wer wann singt (Sängerin, Sänger, beide), jede Liedzeile mit Zeit (Whisper, falls eingerichtet) und worum es geht (KI).
/// Ergebnis liegt neben dem Lied (<c>.verstehen.json</c>) und ist die Grundlage für Drehbuch, Lippen und Schnitt.</summary>
public partial class MainWindow
{
    /// <summary>Tests: Stimmen-Erkennung ersetzen (Gesangsspur → JSON).</summary>
    public Func<string, Task<string?>>? SingersOverride { get; set; }
    /// <summary>Tests: Wortzeiten ersetzen (Gesangsspur → whisper.cpp-JSON).</summary>
    public Func<string, Task<string?>>? WordsOverride { get; set; }
    /// <summary>Tests: Thema ersetzen (Anfrage → Antwort der KI).</summary>
    public Func<string, Task<string?>>? ThemeOverride { get; set; }
    bool _msUnderstanding;

    static string UnderstandFile(MusicSong s) => s.File + ".verstehen.json";
    public SongUnderstanding.Result? Understanding(MusicSong s) => File.Exists(UnderstandFile(s)) ? SongUnderstanding.Load(File.ReadAllText(UnderstandFile(s))) : null;

    MusicSong? VocalStemOf(MusicSong s) => s.Kind == "gesang" ? s : Songs.All().FirstOrDefault(x => x.ParentId == s.Id && x.Kind == "gesang" && File.Exists(x.File));

    /// <summary>Lied verstehen (A1–A4). Gibt das Ergebnis zurück oder null mit Meldung.</summary>
    public async Task<SongUnderstanding.Result?> MsUnderstand(string songId)
    {
        if (Songs.Get(songId) is not { } s || !File.Exists(s.File)) return null;
        if (_msUnderstanding) return null;
        _msUnderstanding = true;
        void Say(string t) { _msMsg = t; if (CurrentView == MS) Show(MS); }
        try
        {
            // A3: Takt und Aufbau
            Say($"„{s.Title}“ wird verstanden: Takt und Aufbau …");
            var secCache = s.File + ".abschnitte.json";
            if (await SectionsOf(s) is null) { Say("Zum Verstehen braucht Trendo die Song-Analyse (rechts unter „Lied“ einrichten, einmalig ca. 300 MB)."); return null; }
            var sections = File.Exists(secCache) ? await File.ReadAllTextAsync(secCache) : SectionsOverride is { } so ? await so(s.File) : null;
            if (sections is null) { Say("Takt und Aufbau konnten nicht bestimmt werden."); return null; }

            // A1: wer singt – auf der Gesangsspur
            string? singers = null; string? vocal = null;
            if (s.Lyrics.Trim() != "[Instrumental]")
            {
                var stem = VocalStemOf(s);
                if (stem is null)
                {
                    Say("Gesang wird abgetrennt (für die Stimmen-Erkennung) …");
                    var keep = _msSel;
                    var made = await MsSplit(s.Id);
                    _msSel = keep;
                    stem = made.FirstOrDefault(x => x.Kind == "gesang");
                }
                vocal = stem?.File;
                if (vocal is not null)
                {
                    Say("Stimmen werden erkannt: Sängerin, Sänger, Duett …");
                    var cache = vocal + ".saenger.json";
                    if (File.Exists(cache)) singers = await File.ReadAllTextAsync(cache);
                    else if (SingersOverride is { } o) singers = await o(vocal);
                    else if (AnalyseReady) { var r = await KiPyRun(KiRuntime.MusikAnalyse, ["--saenger", vocal, cache]); singers = r.Ok && File.Exists(cache) ? await File.ReadAllTextAsync(cache) : null; }
                }
            }

            // A2: Wort für Wort (Whisper auf der Gesangsspur)
            string? words = null;
            if (vocal is not null)
            {
                var cache = vocal + ".woerter.json";
                if (File.Exists(cache)) words = await File.ReadAllTextAsync(cache);
                else if (WordsOverride is { } o) words = await o(vocal);
                else if (WhisperModel() is { } wm && FfmpegPath is { } ff && !_opt.Isolated)
                {
                    Say("Liedzeilen werden Wort für Wort gehört (Whisper) …");
                    var wav = Path.Combine(Path.GetTempPath(), "trendo-whisper-" + Guid.NewGuid().ToString("N") + ".wav");
                    try
                    {
                        var c = await Task.Run(() => Cli.Run(ff, ["-y", "-i", vocal, "-ar", "16000", "-ac", "1", "-c:a", "pcm_s16le", wav], timeoutMs: 600_000));
                        if (c.Ok)
                        {
                            using var gpu = await GpuSlot("sprache", "Liedzeilen hören");
                            var outBase = vocal + ".woerter";
                            var r = await Task.Run(() => Cli.Run(KiStore.RuntimeExe(KiRuntime.WhisperCpp), ["-m", KiStore.ModelFile(wm, wm.Files[0]), "-f", wav, "-l", s.Language is { Length: 2 } l ? l : "auto", "-ml", "1", "-sow", "-oj", "-of", outBase], timeoutMs: 1_800_000));
                            if (r.Ok && File.Exists(outBase + ".json")) { File.Move(outBase + ".json", cache, true); words = await File.ReadAllTextAsync(cache); }
                        }
                    }
                    finally { TryDelete(wav); }
                }
            }

            // A4: worum es geht
            SongUnderstanding.Theme? theme = null;
            if (s.Lyrics.Trim().Length > 0 && s.Lyrics.Trim() != "[Instrumental]")
            {
                Say("Worum es geht: Thema, Stimmung, Orte …");
                var prompt = SongUnderstanding.ThemePrompt(s.Title, s.Style, s.Lyrics);
                try { theme = SongUnderstanding.ParseTheme(ThemeOverride is { } t ? await t(prompt) : await Intel.AskAsync(new KiRequest(prompt, PreferredModel: KiModelName, UseKnowledge: false, AllowRemote: true)) is { Ok: true } a ? a.Text : null); }
                catch (Exception e) when (e is HttpRequestException or TaskCanceledException or InvalidOperationException) { }
            }

            var res = SongUnderstanding.Build(s.Lyrics, sections, singers, words, theme);
            await File.WriteAllTextAsync(UnderstandFile(s), SongUnderstanding.Save(res));
            _core.Audit.Append(Principal.LocalOwner.Name, "musik.verstanden", $"{s.Id}: {res.Source}");
            Say("✓ Verstanden: " + SongUnderstanding.Summary(res) + (words is null && vocal is not null ? " – genauer mit einem Whisper-Modell aus der KI-Bibliothek." : ""));
            return res;
        }
        catch (Exception e) when (e is IOException or System.Text.Json.JsonException or InvalidOperationException) { Say("Verstehen ging nicht: " + e.Message); return null; }
        finally { _msUnderstanding = false; if (CurrentView == MS) Show(MS); }
    }

    KiLibraryModel? WhisperModel() =>
        KiStore.RuntimeReady(KiRuntime.WhisperCpp) ? KiLibrary.Models.Where(x => x.Kind == KiKind.SpracheZuText && KiStore.ModelReady(x)).OrderByDescending(x => x.SizeGb).FirstOrDefault() : null;

    /// <summary>Anzeige im Reiter „Musikvideo“: Aufbau, wer singt, Liedzeilen mit Zeit, Thema.</summary>
    Control MsUnderstandPanel(MusicSong s)
    {
        var sp = new StackPanel { Name = "ms-understand-panel", Spacing = 6 };
        sp.Children.Add(H("Lied verstehen", 13));
        var u = Understanding(s);
        sp.Children.Add(P(u is null ? "Damit das Musikvideo zum Lied passt, hört Trendo es genau an: Takt und Aufbau, wer wann singt (Sängerin, Sänger, beide) und jede Zeile mit Zeit – und liest, worum es geht."
            : SongUnderstanding.Summary(u) + $" · Zeiten aus: {u.Source}", u is null ? Muted : Text, 11.5));
        var go = Act("ms-understand", _msUnderstanding ? "Wird verstanden …" : u is null ? "Lied verstehen" : "Neu verstehen", () => _ = MsUnderstand(s.Id), u is null);
        go.IsEnabled = !_msUnderstanding;
        sp.Children.Add(go);
        if (u is null) return sp;
        if (u.Theme is { } t)
            sp.Children.Add(new Border { Background = Brush.Parse("#14FFFFFF"), CornerRadius = new CornerRadius(10), Padding = new Thickness(10, 8), Child = new StackPanel { Spacing = 2, Children = {
                new TextBlock { Name = "ms-understand-theme", Text = $"Thema: {t.Topic}" + (t.Mood.Length > 0 ? $" · {t.Mood}" : ""), Foreground = Text, FontSize = 12, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap },
                P(t.Story, Muted, 11.5),
                P(string.Join(" · ", new[] { t.Places.Count > 0 ? "Orte: " + string.Join(", ", t.Places) : "", t.TimeOfDay.Length > 0 ? "Zeit: " + t.TimeOfDay : "", t.Colors.Count > 0 ? "Farben: " + string.Join(", ", t.Colors) : "" }.Where(x => x.Length > 0)), Muted, 11) } } });
        var list = new StackPanel { Name = "ms-understand-lines", Spacing = 1 };
        foreach (var l in u.Lines.Take(60))
        {
            var who = l.Who switch { "beide" => "beide", var w when w.StartsWith("Sängerin") => "" + w, var w when w.StartsWith("Sänger") => "" + w, var w => w };
            list.Children.Add(new Grid { ColumnDefinitions = new ColumnDefinitions("44,78,*"), Children = {
                new TextBlock { Text = l.Timed ? SongTime(l.Start) : "–", Foreground = Muted, FontSize = 11 },
                Pos(new TextBlock { Text = who, Foreground = l.Who == "beide" ? Accent : Text, FontSize = 11, TextTrimming = TextTrimming.CharacterEllipsis }, 1),
                Pos(new TextBlock { Text = l.Text, Foreground = Text, FontSize = 11, TextTrimming = TextTrimming.CharacterEllipsis }, 2) } });
        }
        sp.Children.Add(new Border { Background = Brush.Parse("#0FFFFFFF"), CornerRadius = new CornerRadius(10), Padding = new Thickness(8, 6), MaxHeight = 200, Child = new ScrollViewer { Content = list } });
        return sp;
    }
}
