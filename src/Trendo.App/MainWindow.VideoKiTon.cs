using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Trendo.Core;
using Trendo.Core.Kreativ;
using Trendo.Core.Media;
using Trendo.Core.Programs;
using Path = System.IO.Path;

namespace Trendo.App;

/// <summary>T-0699 – „Videos mit KI“ erzeugt wirklich Video: Schlüsselbilder der lokalen Bild-KI mit echter Kamerafahrt und Überblendung,
/// auf Wunsch je Szene ein echter bewegter KI-Clip aus dem Schlüsselbild (Wan 2.2 TI2V-5B, lokal, über <see cref="GenVideo"/>),
/// Sprecher mit der lokalen Stimme (Piper), Musik darunter und Untertitel als eigene Spur. Ohne Stimme oder Video-Modell: klarer Hinweis, nichts wird vorgetäuscht.</summary>
public partial class MainWindow
{
    /// <summary>Tests: Sprachausgabe ersetzen (Text, Ziel-WAV → erfolgreich?).</summary>
    public static Func<string, string, bool>? VideoAiTtsOverride { get; set; }

    KiLibraryModel? VideoVoiceModel() => KiLibrary.Models.FirstOrDefault(m => m.Kind == KiKind.TextZuSprache && m.Runtime == KiRuntime.Piper && KiStore.ModelReady(m));
    bool VideoVoiceReady => VideoAiTtsOverride is not null || (!_opt.Isolated && VideoVoiceModel() is not null && KiStore.RuntimeReady(KiRuntime.Piper));
    bool VideoMoveReady => VideoKiOverride is not null || EditGenModelOverride is not null || (!_opt.Isolated && EditGenModel() is not null);

    /// <summary>Sprecher je Szene als WAV (lokale Stimme). Gleicher Text → gleiche Datei (kein zweites Einsprechen). Ohne Stimme: keine Spur und ein Hinweis.</summary>
    async Task<(List<VideoAiKit.Voice> Voices, string Note)> VideoVoices(ProgramDocument d, IReadOnlyList<VideoAiKit.Scene> scenes)
    {
        var list = new List<VideoAiKit.Voice>();
        if (scenes.All(s => s.Speech.Length == 0)) return (list, "");
        if (!VideoVoiceReady) return (list, " ⚠ Ohne Sprecher gebaut: Es ist keine Stimme eingerichtet (KI-Bibliothek → „Vorlesen“ → „Thorsten“). Die Untertitel sind drin.");
        var starts = VideoAiKit.Starts(scenes.Select(s => s.Seconds).ToList());
        var dir = VideoDir(d); Directory.CreateDirectory(dir);
        for (var i = 0; i < scenes.Count; i++)
        {
            var text = scenes[i].Speech.Trim();
            if (text.Length == 0) continue;
            var wav = Path.Combine(dir, $"sprecher-{Sha256Text(text)[..12].ToLowerInvariant()}.wav");
            if (!File.Exists(wav) || new FileInfo(wav).Length < 100)
            {
                _progMsg[VideoAiKey] = $"Sprecher spricht Szene {i + 1} ein …"; Show(VideoAiKey);
                bool ok;
                if (VideoAiTtsOverride is { } o) ok = o(text, wav);
                else
                {
                    var m = VideoVoiceModel()!;
                    var r = await Task.Run(() => RunWithInput(KiStore.RuntimeExe(KiRuntime.Piper), ["--model", KiStore.ModelFile(m, m.Files[0]), "--output_file", wav], text));
                    ok = r.Ok;
                }
                if (!ok || !File.Exists(wav)) return (new(), $" ⚠ Ohne Sprecher gebaut: Die Stimme hat Szene {i + 1} nicht eingesprochen.");
            }
            var start = starts[i] + (i > 0 ? VideoAiKit.Fade : 0) + 0.2;
            var end = starts[i] + scenes[i].Seconds - (i < scenes.Count - 1 ? VideoAiKit.Fade : 0);
            list.Add(new(wav, start, Math.Max(0.5, end - start)));
        }
        return (list, "");
    }

    /// <summary>Szene mit der Video-KI bewegen: aus dem Schlüsselbild entsteht ein echter KI-Clip (Wan 2.2, lokal) mit Herkunftsnachweis. Ersetzt im Video die Kamerafahrt.</summary>
    public async Task<string?> VideoMoveScene(ProgramDocument d, int i, GenVideo.Quality? quality = null)
    {
        if (_videoBusy) { _progMsg[VideoAiKey] = "Es wird schon gearbeitet – einen Moment."; Show(VideoAiKey); return null; }
        var scenes = VideoScenes(d);
        if (i < 0 || i >= scenes.Count) return null;
        var s = scenes[i];
        if (s.Image.Length == 0) { _progMsg[VideoAiKey] = $"Szene {i + 1}: zuerst ein Schlüsselbild malen oder wählen – daraus entsteht die Bewegung."; Show(VideoAiKey); return null; }
        var m = EditGenModel();
        var q = quality ?? GenVideo.Preview;
        // Wan rechnet lange (gemessen 16.11.2026, RTX 5070: viele Minuten je Sekunde Clip) – darum höchstens 1,5 s erzeugen und auf Szenenlänge verlangsamen
        var secs = Math.Clamp(Math.Round(s.Seconds * 2) / 2, 1, VideoAiKit.MaxKiClip);
        var hint = VideoAiKit.MotionLabel(s.Motion) switch { "Zoom rein" => "slow camera push in", "Zoom raus" => "slow camera pull back", "Schwenk nach links" => "slow camera pan left", "Schwenk nach rechts" => "slow camera pan right", _ => "subtle natural motion, static camera" };
        var req = new GenVideo.Request($"{s.Prompt}, {hint}, cinematic, natural movement", ImageAiKit.Seed(int.TryParse(d.Text("seed"), out var sd) ? sd : 42, i), secs, q, s.Image);
        if (GenVideo.Problem(req, m) is { } p) { _progMsg[VideoAiKey] = p; Show(VideoAiKey); return null; }
        if (_opt.Isolated && VideoKiOverride is null) { _progMsg[VideoAiKey] = "Im abgeschotteten Testmodus startet Trendo keine Video-KI."; Show(VideoAiKey); return null; }
        var ffmpeg = VideoAiFfmpegOverride is null ? FfmpegPath : "ffmpeg";
        if (ffmpeg is null) { _progMsg[VideoAiKey] = "Für den Clip fehlt FFmpeg. Du richtest es im Studio „Videos schneiden“ mit einem Klick ein."; Show(VideoAiKey); return null; }
        var dir = VideoDir(d); Directory.CreateDirectory(dir);
        var name = $"ki-clip-{i + 1}-{DateTime.Now:yyyyMMdd-HHmmss}-s{req.Seed}";
        // Startbild genau in Clipgröße (Wan rechnet in Vielfachen von 32): mittig zuschneiden, Schlüsselbild bleibt unverändert
        var startPng = Path.Combine(dir, name + ".start.png");
        try { VideoFitImage(s.Image, q.Width, q.Height, startPng); req = req with { StartImage = startPng }; }
        catch (Exception ex) when (ex is IOException or InvalidDataException or ArgumentException) { _progMsg[VideoAiKey] = "Das Schlüsselbild ließ sich nicht lesen: " + ex.Message; Show(VideoAiKey); return null; }
        var raw = Path.Combine(dir, name + ".roh.webm"); var clip = Path.Combine(dir, name + ".mp4");
        _videoBusy = true; _progMsg[VideoAiKey] = $"Szene {i + 1} wird mit der Video-KI bewegt ({m!.Name}, {q.Label}, {secs:0.#} s) – das dauert etwa 5 bis 15 Minuten …"; Show(VideoAiKey);
        try
        {
            bool ok;
            using (await GpuSlot("video", "KI-Clip für ein Video"))
                ok = await Tool(KiStore.RuntimeExe(KiRuntime.StableDiffusionCpp), GenVideo.Args(req, m, raw), CancellationToken.None);
            if (!ok || !File.Exists(raw) || new FileInfo(raw).Length == 0) { _progMsg[VideoAiKey] = $"Die Video-KI hat für Szene {i + 1} keinen Clip geliefert – die Szene bleibt ein Bild mit Kamerafahrt."; return null; }
            var (w, h) = VideoAiKit.Size(d.Text("format"));
            var nargs = VideoAiKit.ClipFitArgs(raw, secs, s.Seconds, w, h, clip);
            var norm = VideoAiFfmpegOverride is { } fo ? new Cli.Result(fo(nargs, clip) ? 0 : 1, "", "") : await Task.Run(() => Cli.Run(ffmpeg, nargs, timeoutMs: 600_000));
            if (!norm.Ok || !File.Exists(clip)) { _progMsg[VideoAiKey] = "Der KI-Clip ließ sich nicht umwandeln: " + KreativSafe.Cut(norm.Err.Trim().Split('\n').LastOrDefault() ?? "", 200); return null; }
            GenVideo.WriteProvenance(clip, GenVideo.Provenance(req, m, "stable-diffusion.cpp", KiLibrary.RuntimeInfo(KiRuntime.StableDiffusionCpp).Tag, clip, raw, DateTime.UtcNow));
            TryDelete(raw); TryDelete(startPng);
            VideoEditScene(d, i, "KI-Video", o => { if (Js(o["prompt"]) == s.Prompt) o["clip"] = clip; });
            _core.Audit.Append(Principal.LocalOwner.Name, "video-ki.clip", $"Szene {i + 1} {m.Id}");
            _progMsg[VideoAiKey] = $"✓ Szene {i + 1} ist jetzt ein KI-Video ({secs:0.#} s, {m.Name}). Herkunftsnachweis liegt daneben.";
            return clip;
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or UnauthorizedAccessException or System.ComponentModel.Win32Exception) { _progMsg[VideoAiKey] = ex.Message; return null; }
        finally { _videoBusy = false; Show(VideoAiKey); }
    }

    /// <summary>Bild mittig auf w × h zuschneiden und skalieren (PNG).</summary>
    public static void VideoFitImage(string src, int w, int h, string dst)
    {
        using var bmp = SkiaSharp.SKBitmap.Decode(src) ?? throw new InvalidDataException("Bild nicht lesbar.");
        var scale = Math.Max(w / (double)bmp.Width, h / (double)bmp.Height);
        double sw = w / scale, sh = h / scale, sx = (bmp.Width - sw) / 2, sy = (bmp.Height - sh) / 2;
        using var outBmp = new SkiaSharp.SKBitmap(w, h);
        using (var c = new SkiaSharp.SKCanvas(outBmp))
        using (var img = SkiaSharp.SKImage.FromBitmap(bmp))
            c.DrawImage(img, new SkiaSharp.SKRect((float)sx, (float)sy, (float)(sx + sw), (float)(sy + sh)), new SkiaSharp.SKRect(0, 0, w, h), new SkiaSharp.SKSamplingOptions(SkiaSharp.SKCubicResampler.Mitchell));
        using var png = SkiaSharp.SKImage.FromBitmap(outBmp).Encode(SkiaSharp.SKEncodedImageFormat.Png, 100);
        using var fs = File.Create(dst); png.SaveTo(fs);
    }

    /// <summary>Eigene Musik: wird in den Ordner des Dokuments kopiert (Original bleibt), läuft im Video leise unter dem Sprecher.</summary>
    public async Task VideoMusicPick(ProgramDocument d)
    {
        if (await PickMediaFile("video-musik", "Musik (MP3, WAV, M4A, OGG, FLAC)", ["*.mp3", "*.wav", "*.m4a", "*.ogg", "*.flac"]) is not { } f) return;
        ProgramRun(VideoAiKey, () =>
        {
            if (!KreativSafe.LocalFile(f, 200L * 1024 * 1024)) throw new ArgumentException("Bitte eine Musikdatei auf diesem Rechner wählen (höchstens 200 MB).");
            var dir = VideoDir(d); Directory.CreateDirectory(dir);
            var dst = Path.Combine(dir, "musik-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff") + Path.GetExtension(f).ToLowerInvariant());
            File.Copy(f, dst, overwrite: false);
            d.Set("music", dst, "Musik");
            _progMsg[VideoAiKey] = "Musik übernommen: " + Path.GetFileName(f) + ". Sie läuft unter dem Sprecher leise mit.";
        });
    }

    /// <summary>Vorschau der Kamerafahrt ohne FFmpeg: Anfang, Mitte und Ende des sichtbaren Ausschnitts (gleiche Rechnung wie im Video).</summary>
    static Control VideoMotionStrip(string image, string motion, int idx)
    {
        var sp = new StackPanel { Name = $"video-s{idx}-motion-preview", Orientation = Orientation.Horizontal, Spacing = 4 };
        Bitmap bmp;
        try { using var fs = File.OpenRead(image); bmp = Bitmap.DecodeToWidth(fs, 480); }
        catch (Exception ex) when (ex is IOException or ArgumentException or InvalidOperationException or UnauthorizedAccessException) { return sp; }
        foreach (var t in new[] { 0.0, 0.5, 1.0 })
        {
            var (x, y, w, h) = VideoAiKit.View(motion, t);
            var px = bmp.PixelSize;
            var rect = new PixelRect((int)(x * px.Width), (int)(y * px.Height), Math.Max(1, (int)(w * px.Width)), Math.Max(1, (int)(h * px.Height)));
            sp.Children.Add(new Border { CornerRadius = new CornerRadius(4), ClipToBounds = true, Child = new Image { Width = 64, Height = 36, Stretch = Stretch.UniformToFill, Source = new CroppedBitmap(bmp, rect) } });
        }
        sp.Children.Add(new TextBlock { Text = "Anfang → Ende", Foreground = Muted, FontSize = 10.5, VerticalAlignment = VerticalAlignment.Center });
        return sp;
    }
}
