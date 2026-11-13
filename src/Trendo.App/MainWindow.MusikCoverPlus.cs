using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Trendo.Core;
using Trendo.Core.Media;

namespace Trendo.App;

/// <summary>Trendo Musik V15 wie Suno (07.11.2026): Cover mit eigener Beschreibung (die Text-KI übersetzt sie für die Bild-KI ins Englische, wenn sie da ist)
/// und „Bewegtes Cover“ – Wan 2.2 lässt das Cover-Bild 4 Sekunden lebendig werden, Trendo macht daraus eine nahtlose Schleife (vorwärts + rückwärts, 1080 × 1080).
/// Ohne Video-Modell entsteht nichts Vorgetäuschtes, sondern ein klarer Hinweis.</summary>
public partial class MainWindow
{
    /// <summary>Für Tests: Übersetzung der Cover-Beschreibung ersetzen.</summary>
    public Func<string, Task<string?>>? CoverTranslateOverride { get; set; }
    /// <summary>Quadratisch für Cover (Vielfache von 32), Endfassung mit 30 Schritten.</summary>
    public static readonly GenVideo.Quality CoverMotion = new("cover", "Bewegtes Cover", 704, 704, 30, 24);
    string _msCoverHintDraft = ""; bool _msMotionBusy;

    async Task<string> CoverHintEnglish(string hint)
    {
        var h = hint.Trim(); if (h.Length == 0) return h;
        string? t = null;
        try
        {
            var ask = $"Übersetze diese Bildbeschreibung für ein Album-Cover ins Englische, knapp, ohne Text im Bild, ohne Namen echter Personen. Gib nur die Übersetzung aus: {h}";
            t = CoverTranslateOverride is { } o ? await o(ask)
                : _kiConnected || !_opt.Isolated ? (await Intel.AskAsync(new KiRequest(ask, PreferredModel: KiModelName, UseKnowledge: false, AllowRemote: true)) is { Ok: true } a ? a.Text : null) : null;
        }
        catch (Exception ex) when (ex is InvalidOperationException or HttpRequestException or TaskCanceledException) { t = null; }
        t = t?.Trim().Trim('"', '`', '„', '“').Trim();
        return t is { Length: > 2 and < 600 } ? t : h;
    }

    /// <summary>Cover nach eigener Beschreibung neu malen (Beschreibung bleibt am Lied für „Neu würfeln“).</summary>
    public async Task<string?> MsCoverWithHint(string songId, string hint)
    {
        if (Songs.Get(songId) is null) return null;
        var en = await CoverHintEnglish(hint);
        Songs.Update(songId, x => x.CoverHint = en);
        return await MsMakeCover(songId, Random.Shared.Next(1, 1_000_000));
    }

    /// <summary>Schleife: Clip vorwärts und rückwärts (nahtlos), quadratisch 1080, ohne Ton.</summary>
    public static string[] CoverLoopArgs(string input, string output) =>
        ["-hide_banner", "-loglevel", "error", "-y", "-i", input, "-an", "-filter_complex",
         "[0:v]scale=1080:1080:force_original_aspect_ratio=increase,crop=1080:1080,fps=24,split[a][b];[b]reverse,trim=start_frame=1,setpts=PTS-STARTPTS[r];[a][r]concat=n=2:v=1[v]",
         "-map", "[v]", "-c:v", "libx264", "-preset", "medium", "-crf", "18", "-pix_fmt", "yuv420p", "-movflags", "+faststart", output];

    /// <summary>Bewegtes Cover aus dem Cover-Bild mit der Video-KI.</summary>
    public async Task<string?> MsMotionCover(string songId, string? motion = null)
    {
        if (Songs.Get(songId) is not { } s) return null;
        if (!File.Exists(s.CoverFile)) { _msMsg = "Erst ein Cover-Bild erstellen – daraus entsteht das bewegte Cover."; Show(MS); return null; }
        var m = EditGenModel();
        var prompt = "album cover coming to life, subtle slow motion, gentle camera drift, " + (motion is { Length: > 0 } mo ? await CoverHintEnglish(mo) : SongCover.Prompt(s.Style, s.Title, s.CoverSeed, s.CoverHint));
        var seed = GenVideo.NewSeed(); var req = new GenVideo.Request(prompt.Length > 1500 ? prompt[..1500] : prompt, seed, 4, CoverMotion, s.CoverFile);
        if (GenVideo.Problem(req, m) is { } p) { _msMsg = p; Show(MS); return null; }
        if (_opt.Isolated && VideoKiOverride is null) { _msMsg = "Im abgeschotteten Testmodus startet Trendo keine Video-KI."; Show(MS); return null; }
        var dir = Path.Combine(Path.GetDirectoryName(s.File)!, "Cover"); Directory.CreateDirectory(dir);
        if (FfmpegRunner(dir, out var why) is not { } run) { _msMsg = why!; Show(MS); return null; }
        var raw = Path.Combine(dir, $".bewegt-{Guid.NewGuid():N}.webm"); var clip = Path.Combine(dir, $"{DevStudio.SafeName(s.Title)}-bewegt-{seed}.mp4");
        _msMotionBusy = true; _msMsg = "Das bewegte Cover entsteht mit der Video-KI – das dauert einige Minuten …"; Show(MS);
        try
        {
            bool ok;
            using (await GpuSlot("video", "Bewegtes Cover"))
                ok = await Tool(KiStore.RuntimeExe(KiRuntime.StableDiffusionCpp), GenVideo.Args(req, m!, raw), CancellationToken.None);
            if (!ok || !File.Exists(raw) || new FileInfo(raw).Length == 0) { _msMsg = "Die Video-KI hat keinen Clip geliefert."; return null; }
            var r = await Task.Run(() => run(CoverLoopArgs(raw, clip)));
            if (!r.Ok || !File.Exists(clip)) { _msMsg = "Die Schleife ließ sich nicht bauen: " + (r.Err.Trim().Split('\n').LastOrDefault() ?? ""); return null; }
            GenVideo.WriteProvenance(clip, GenVideo.Provenance(req, m!, "stable-diffusion.cpp", KiLibrary.RuntimeInfo(KiRuntime.StableDiffusionCpp).Tag, clip, raw, DateTime.UtcNow));
            var old = s.CoverVideo;
            Songs.Update(s.Id, x => x.CoverVideo = clip);
            if (old.Length > 0 && old != clip && File.Exists(old) && Path.GetDirectoryName(old) == dir) { TryDelete(old); TryDelete(GenVideo.ProvenancePath(old)); }
            _core.Audit.Append(Principal.LocalOwner.Name, "musik.cover.bewegt", $"{s.Id} s{seed}");
            _msMsg = $"Bewegtes Cover fertig: {Path.GetFileName(clip)} – Schleife 1080 × 1080, Herkunftsnachweis daneben.";
            return clip;
        }
        finally { TryDelete(raw); _msMotionBusy = false; Show(MS); }
    }

    Control MsCoverPlus(MusicSong s)
    {
        var sp = new StackPanel { Name = "ms-art-plus", Spacing = 4, Margin = new Thickness(0, 4, 0, 0) };
        var hint = new TextBox { Name = "ms-art-hint", Text = _msCoverHintDraft.Length > 0 ? _msCoverHintDraft : "", Watermark = s.CoverHint.Length > 0 ? "Jetzt: " + s.CoverHint : "Motiv beschreiben, z. B. „Leuchtturm im Nebel bei Sonnenaufgang“", FontSize = 12, TextWrapping = TextWrapping.Wrap };
        hint.TextChanged += (_, _) => _msCoverHintDraft = hint.Text ?? "";
        var go = Act("ms-art-hint-go", "Cover nach Beschreibung", () => { var t = (hint.Text ?? "").Trim(); if (t.Length < 3) { _msMsg = "Beschreibe kurz, was auf dem Cover sein soll."; Show(MS); return; } _msCoverHintDraft = ""; _ = MsCoverWithHint(s.Id, t); });
        go.IsEnabled = !_msArtBusy;
        var motion = Act("ms-art-motion", _msMotionBusy ? "Bewegtes Cover entsteht …" : "Bewegtes Cover (KI-Video)", () => { if (!_msMotionBusy) _ = MsMotionCover(s.Id); });
        motion.IsEnabled = !_msMotionBusy && File.Exists(s.CoverFile);
        sp.Children.Add(hint); sp.Children.Add(new WrapPanel { Children = { go, new Border { Width = 6 }, motion } });
        sp.Children.Add(P(EditGenModel() is null ? "Bewegtes Cover braucht „Wan 2.2 – Filmszenen“ aus der KI-Bibliothek (Videos erzeugen)." : "Bewegtes Cover: 4 Sekunden Video-KI aus deinem Cover, als nahtlose Schleife – wie ein animiertes Albumcover.", Muted, 11));
        if (s.CoverVideo.Length > 0 && File.Exists(s.CoverVideo)) sp.Children.Add(P("" + Path.GetFileName(s.CoverVideo), Text, 11.5));
        return sp;
    }
}
