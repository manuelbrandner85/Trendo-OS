using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Trendo.Core;
using Trendo.Core.Media;
using Trendo.Core.Programs;

namespace Trendo.App;

/// <summary>Paket 01 – Studio 3, S3-09 (07.11.2026): Lippensynchron im Videoschnitt – Sprachaufnahme (+ Text) → Mundbild-Spur (JSON, Moho/Papagayo),
/// Prüfung gegen die Aufnahme, Vergleichsvorschau (optional neben einem Gesichtsvideo, dessen Mund per MediaPipe gemessen wird).
/// FAIL (keine Sprache, Versatz, kein Zusammenhang) wird gemeldet – dann kommt nichts in die Zeitleiste.</summary>
public partial class MainWindow
{
    /// <summary>Tests: Wortzeiten (16-kHz-WAV → whisper.cpp-JSON) ersetzen.</summary>
    public Func<string, Task<string?>>? EditLipWordsOverride { get; set; }
    /// <summary>Tests: Mundöffnung je Bild aus dem Video ersetzen (Video → fps, Werte).</summary>
    public Func<string, Task<(double Fps, double?[] Mouth)?>>? EditLipMouthOverride { get; set; }
    public LipSync.SyncResult? EditLipLast { get; private set; }
    public LipSync.SyncResult? EditLipVideoLast { get; private set; }
    public string? EditLipFolder { get; private set; }
    string _veLipAudio = "", _veLipText = "", _veLipVideo = "";
    bool _veLipBusy;

    public async Task<bool> EditLipSync(ProgramDocument d, string audio, string text, string? video = null)
    {
        audio = audio.Trim().Trim('"'); video = string.IsNullOrWhiteSpace(video) ? null : video.Trim().Trim('"');
        if (audio.Length == 0 || !File.Exists(audio)) { _progMsg[VK] = "FAIL: Sprachaufnahme fehlt – bitte eine Audio- oder Videodatei mit Sprache angeben."; Show(VK); return false; }
        if (video is not null && !File.Exists(video)) { _progMsg[VK] = "Vergleichsvideo fehlt: " + Path.GetFileName(video); Show(VK); return false; }
        var dir = Path.Combine(WorkFolder(Programs[VK].Folder), "Lippensynchron", DevStudio.SafeName(Path.GetFileNameWithoutExtension(audio))); Directory.CreateDirectory(dir);
        if (FfmpegRunner(dir, out var why) is not { } run) { _progMsg[VK] = why!; Show(VK); return false; }
        _veLipBusy = true; EditLipLast = null; EditLipVideoLast = null; _progMsg[VK] = "Mundbilder entstehen …"; Show(VK);
        try
        {
            var wav = Path.Combine(dir, "stimme-16k.wav");
            var c = await Task.Run(() => run(["-hide_banner", "-loglevel", "error", "-y", "-i", audio, "-vn", "-ar", "16000", "-ac", "1", "-c:a", "pcm_s16le", wav]));
            if (!c.Ok || !File.Exists(wav)) { _progMsg[VK] = "FAIL: Die Aufnahme hat keinen lesbaren Ton."; return false; }
            var buf = AudioFile.Read(wav); var env = LipSync.Envelope(buf); var seconds = buf.Length / (double)buf.Rate;
            // Wortzeiten: Whisper (genau) – sonst Text über die Sprechpausen verteilt (ungenauer, so angezeigt)
            List<LipSync.Word> words = []; var source = "";
            string? json = EditLipWordsOverride is { } o ? await o(wav) : null;
            if (json is null && EditLipWordsOverride is null && WhisperModel() is { } wm && !_opt.Isolated)
            {
                using var gpu = await GpuSlot("sprache", "Lippensynchron: Wörter hören");
                var b = Path.Combine(dir, "woerter");
                var r = await Task.Run(() => Cli.Run(KiStore.RuntimeExe(KiRuntime.WhisperCpp), ["-m", KiStore.ModelFile(wm, wm.Files[0]), "-f", wav, "-l", "auto", "-ml", "1", "-sow", "-oj", "-of", b], timeoutMs: 1_800_000));
                if (r.Ok && File.Exists(b + ".json")) json = await File.ReadAllTextAsync(b + ".json");
            }
            if (json is not null) { words = LipSync.SnapToVoice(LipSync.ParseWhisper(json), env); source = "Whisper (Wortzeiten, am Ton ausgerichtet)"; }
            if (words.Count == 0 && text.Trim().Length > 0) { words = LipSync.WordsFromPauses(text, env); source = "Text über Sprechpausen verteilt (ohne Whisper ungenauer)"; }
            if (words.Count == 0 && env.Count(v => v > 0.08) >= LipSync.Rate / 4) { _progMsg[VK] = "Für Mundbilder fehlt der Text – Text eingeben oder ein Whisper-Modell in der KI-Bibliothek laden („Sprache zu Text“)."; return false; }
            var track = LipSync.Track(words);
            var check = LipSync.Check(track, env); EditLipLast = check; EditLipFolder = dir;
            var fps = (int)Math.Max(1, d.Number("fps", 30));
            await File.WriteAllTextAsync(Path.Combine(dir, "mundbilder.json"), LipSync.Json(track, source, check));
            await File.WriteAllTextAsync(Path.Combine(dir, $"mundbilder-{fps}fps.dat"), LipSync.MohoDat(track, fps));
            // Vergleich mit einem Gesichtsvideo: Mund je Bild messen (MediaPipe)
            if (video is not null)
            {
                (double Fps, double?[] Mouth)? m = null;
                if (EditLipMouthOverride is { } mo) m = await mo(video);
                else if (Model("video-pruefung") is { } vm)
                {
                    var outFile = Path.Combine(dir, "mund-im-video.json");
                    var r = await KiPyRun(KiRuntime.VideoPruefung, [video, outFile, "--mund", "--modelle", KiStore.ModelDir(vm)]);
                    if (r.Ok && File.Exists(outFile))
                    {
                        var j = System.Text.Json.Nodes.JsonNode.Parse(await File.ReadAllTextAsync(outFile));
                        m = ((double?)j?["fps"] ?? 25, (j?["mund"]?.AsArray() ?? []).Select(v => v is null ? (double?)null : (double)v).ToArray());
                    }
                }
                if (m is null) EditLipVideoLast = new(0, 0, false, "Mund im Video nicht gemessen – in der KI-Bibliothek „Artefakt-Prüfung für KI-Videos“ einrichten.");
                else
                {
                    var vals = m.Value.Mouth; var found = vals.Count(v => v is not null);
                    EditLipVideoLast = found < vals.Length * 0.6 ? new(0, 0, false, $"FAIL: Im Video ist das Gesicht zu selten zu sehen ({found} von {vals.Length} Bildern).")
                        : LipSync.CompareVideo(vals.Select(v => v ?? 0).ToArray(), m.Value.Fps, env);
                }
            }
            // Vergleichsvorschau
            foreach (var v in LipSync.Shapes.Keys) await Task.Run(() => run(LipSync.MouthImageArgs(v, Path.Combine(dir, "mund-" + v + ".png"))));
            var list = Path.Combine(dir, "mund.ffconcat"); await File.WriteAllTextAsync(list, LipSync.ConcatList(track, seconds, v => Path.Combine(dir, "mund-" + v + ".png")));
            var prev = Path.Combine(dir, "vergleich.mp4");
            var pr = await Task.Run(() => run(LipSync.PreviewArgs(audio, list, prev, seconds, video)));
            var ok = check.Ok && EditLipVideoLast is not { Ok: false };
            if (ok && pr.Ok && File.Exists(prev))
                d.Edit("Lippensynchron", x => { VideoEdit.Add(x, VideoEdit.NewClip(prev, seconds, true)); VideoEdit.SetClip(x, VideoEdit.Clips(x).Count - 1, 0, "Lippensynchron: " + Path.GetFileNameWithoutExtension(audio)); });
            _progMsg[VK] = $"{check.Text} {(EditLipVideoLast is { } vv ? "Video: " + vv.Text + " " : "")}– {track.Count} Mundbilder ({source}). " +
                (!pr.Ok ? "Die Vergleichsvorschau ließ sich nicht erstellen (" + (pr.Err.Trim().Split('\n').LastOrDefault() ?? "") + ") – Spur liegt im Ordner „Lippensynchron“."
                : ok ? "Vergleichsvorschau in der Zeitleiste; Spur als JSON und Moho/Papagayo-Datei im Ordner „Lippensynchron“." : "Nichts in die Zeitleiste übernommen – Spur und Vorschau liegen zur Kontrolle im Ordner „Lippensynchron“.");
            return ok && pr.Ok;
        }
        catch (Exception e) when (e is IOException or InvalidDataException or InvalidOperationException) { _progMsg[VK] = "FAIL: " + e.Message; return false; }
        finally { _veLipBusy = false; Show(VK); }
    }

    Control EditLipSyncPanel(ProgramDocument d)
    {
        var sp = new StackPanel { Name = "ve-lipsync", Spacing = 5, Margin = new Thickness(0, 6, 0, 0) };
        sp.Children.Add(H("Lippensynchron (Mundbilder)", 13));
        TextBox Box(string name, string val, string hint, Action<string> set) { var t = new TextBox { Name = name, Text = val, FontSize = 12, PlaceholderText = hint }; t.TextChanged += (_, _) => set(t.Text ?? ""); return t; }
        sp.Children.Add(Box("ve-lip-audio", _veLipAudio, "Sprachaufnahme (WAV, MP3, Video …)", v => _veLipAudio = v));
        var text = Box("ve-lip-text", _veLipText, "Gesprochener Text (ohne Whisper nötig)", v => _veLipText = v); text.AcceptsReturn = true; text.TextWrapping = TextWrapping.Wrap; text.MinHeight = 40;
        sp.Children.Add(text);
        sp.Children.Add(Box("ve-lip-video", _veLipVideo, "Optional: Video mit Gesicht zum Vergleichen", v => _veLipVideo = v));
        var go = Act("ve-lip-run", _veLipBusy ? "Mundbilder entstehen …" : "Mundbilder erzeugen und prüfen", () => { if (!_veLipBusy) _ = EditLipSync(d, _veLipAudio, _veLipText, _veLipVideo); });
        go.IsEnabled = !_veLipBusy; sp.Children.Add(go);
        foreach (var r in new[] { EditLipLast, EditLipVideoLast }.OfType<LipSync.SyncResult>())
            sp.Children.Add(P((r.Ok ? "• " : "") + r.Text, r.Ok ? Text : Brush.Parse("#F87171"), 11));
        sp.Children.Add(P("Mundbilder nach Papagayo (AI, E, O, U, L, WQ, etc, FV, MBP, rest) – als JSON und Moho-Datei für Blender, Moho, OpenToonz. Erlaubter Versatz ±80 ms.", Muted, 10.5));
        return sp;
    }
}
