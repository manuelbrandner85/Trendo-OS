using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Trendo.Core;

namespace Trendo.App;

/// <summary>CORE-08: Kreativ- & Medien-Studio. T-0205: Datei per Dialog, Vorschau und Länge, Zeitleiste mit Start/Ende,
/// Ergebnis abspielen, Bilder bearbeiten, Clips aneinanderhängen.</summary>
public partial class MainWindow
{
    private string? _mediaFile;
    private double? _mediaDur;
    private double _mediaStart, _mediaEnd;
    private string? _mediaLast;
    private readonly List<string> _clips = [];
    private ImageTools.Edit _imgEdit = new();
    public string MediaFolder => WorkFolder("Medien");
    public string? FfmpegPath => ToolPath("ffmpeg") is { } p && File.Exists(p) ? p : Cli.Find("ffmpeg");
    public IReadOnlyList<string> MediaClips => _clips;
    public string? MediaLastOutput => _mediaLast;

    private void InitW4Media() => RegisterTab(KreativTabs, "kreativ-medien", "Medien bearbeiten", "CORE-08", MediaStudioView, "video", "umwandeln", "mp3", "mp4", "gif", "ffmpeg", "schneiden");

    public void MediaPick(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) { Say("Bitte zuerst den Pfad zu einer Video- oder Audiodatei eintragen.", "kreativ-medien"); return; }
        if (!File.Exists(path)) { Say("Datei nicht gefunden: " + Path.GetFileName(path), "kreativ-medien"); return; }
        _mediaFile = path; _imgEdit = new();
        _mediaDur = !MediaStudio.IsImage(path) && FfmpegPath is { } ff ? MediaStudio.Duration(ff, path) : null;
        _mediaStart = 0; _mediaEnd = _mediaDur ?? 0;
        Say("Übernommen: " + Path.GetFileName(path) + (_mediaDur is { } d ? $" · Länge {Clock(d)}" : ""), "kreativ-medien");
    }

    public (bool Ok, string Output) MediaRun(string jobId, double start = 0, double length = 10)
    {
        if (FfmpegPath is not { } ff) { Say("Dafür fehlt FFmpeg. Unter Zusatzprogramme kannst du es installieren.", "kreativ-medien"); return (false, ""); }
        if (_mediaFile is null) { Say("Bitte zuerst eine Datei wählen.", "kreativ-medien"); return (false, ""); }
        var (r, o) = MediaStudio.Run(ff, _mediaFile, jobId, start, length);
        _core.Audit.Append(Principal.LocalOwner.Name, "media.job", $"{jobId} exit={r.Exit}");
        if (r.Ok) _mediaLast = o;
        Say(r.Ok ? $"Fertig: {Path.GetFileName(o)}" : "Hat nicht geklappt: " + Short(r.Err), "kreativ-medien");
        return (r.Ok, o);
    }

    /// <summary>Ausschnitt zwischen den Markern der Zeitleiste speichern.</summary>
    public (bool Ok, string Output) MediaCut()
    {
        if (FfmpegPath is not { } ff) { Say("Dafür fehlt FFmpeg. Unter Zusatzprogramme kannst du es installieren.", "kreativ-medien"); return (false, ""); }
        if (_mediaFile is null) { Say("Bitte zuerst eine Datei wählen.", "kreativ-medien"); return (false, ""); }
        var (r, o) = MediaStudio.Cut(ff, _mediaFile, _mediaStart, _mediaEnd);
        _core.Audit.Append(Principal.LocalOwner.Name, "media.cut", $"{Clock(_mediaStart)}-{Clock(_mediaEnd)} exit={r.Exit}");
        if (r.Ok) _mediaLast = o;
        Say(r.Ok ? $"Ausschnitt {Clock(_mediaStart)}–{Clock(_mediaEnd)} gespeichert: {Path.GetFileName(o)}" : "Hat nicht geklappt: " + Short(r.Err), "kreativ-medien");
        return (r.Ok, o);
    }

    public void MediaSetRange(double start, double end) { _mediaStart = Math.Max(0, start); _mediaEnd = Math.Max(_mediaStart, end); }

    public (bool Ok, string Output) MediaConcat()
    {
        if (FfmpegPath is not { } ff) { Say("Dafür fehlt FFmpeg. Unter Zusatzprogramme kannst du es installieren.", "kreativ-medien"); return (false, ""); }
        var (r, o) = MediaStudio.Concat(ff, _clips, MediaFolder);
        if (r.Ok) _mediaLast = o;
        Say(r.Ok ? $"{_clips.Count} Clips aneinandergehängt: {Path.GetFileName(o)}" : "Hat nicht geklappt: " + Short(r.Err), "kreativ-medien");
        return (r.Ok, o);
    }

    public string? MediaSaveImage(ImageTools.Edit e)
    {
        if (_mediaFile is null || !MediaStudio.IsImage(_mediaFile)) { Say("Bitte zuerst ein Bild wählen.", "kreativ-medien"); return null; }
        try
        {
            _imgEdit = e; var o = ImageTools.Apply(_mediaFile, e); _mediaLast = o;
            Say("Bild gespeichert: " + Path.GetFileName(o), "kreativ-medien"); return o;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or IOException) { Say("Hat nicht geklappt: " + ex.Message, "kreativ-medien"); return null; }
    }

    static string Clock(double s) => TimeSpan.FromSeconds(Math.Max(0, s)).ToString(s >= 3600 ? @"h\:mm\:ss" : @"m\:ss");

    static Control Thumb(string file, double height)
    {
        try { using var s = File.OpenRead(file); return new Image { Source = new Bitmap(s), Height = height, Stretch = Stretch.Uniform }; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException) { return new TextBlock { Text = "(keine Vorschau)" }; }
    }

    private Control MediaStudioView()
    {
        var sp = new StackPanel { Spacing = 8, Children = { P("Wähle eine Datei und was du damit machen willst. Das Original bleibt immer erhalten.") } };
        if (FfmpegPath is null) sp.Children.Add(Card(Row(P("FFmpeg fehlt noch. Videos und Ton gehen erst damit; Bilder kannst du trotzdem bearbeiten.", Brush.Parse("#FACC15")), Nav2("Einrichten", "components"))));
        var files = FileShelf.Recent([MediaFolder, Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), Environment.GetFolderPath(Environment.SpecialFolder.MyMusic), Environment.GetFolderPath(Environment.SpecialFolder.MyPictures)], MediaStudio.Inputs);
        sp.Children.Add(H("1. Datei wählen", 14));
        var path = new TextBox { Name = "media-path", Text = _mediaFile ?? "", PlaceholderText = "Pfad zur Datei", Width = 380 };
        sp.Children.Add(new WrapPanel { Children = { path, Act("media-use", "Übernehmen", () => MediaPick(path.Text?.Trim('"', ' ') ?? "")), Act("media-folder", "Medienordner öffnen", () => Launch(MediaFolder)) } });
        // Zuletzt benutzte Dateien als Reihe kleiner Karten statt einer langen Knopf-Säule
        var recent = new WrapPanel { Name = "media-recent" };
        var i = 0;
        foreach (var f in files.Take(8))
        {
            var fp = f.FullName; var b = Act($"media-file-{i++}", (fp == _mediaFile ? "● " : "") + f.Name, () => MediaPick(fp));
            b.Margin = new Thickness(0, 0, 6, 6); b.MaxWidth = 260; ToolTip.SetTip(b, fp); recent.Children.Add(b);
        }
        if (recent.Children.Count > 0) { sp.Children.Add(P("Zuletzt benutzt:", null, 11.5)); sp.Children.Add(recent); }
        if (_mediaFile is { } mf && File.Exists(mf))
        {
            if (MediaStudio.IsImage(mf)) sp.Children.Add(ImageEditor(mf));
            else sp.Children.Add(VideoEditor(mf));
        }
        if (_mediaLast is { } last && File.Exists(last))
            sp.Children.Add(Card(Row(P("Ergebnis: " + Path.GetFileName(last), Text), Act("media-play", "Abspielen / Öffnen", () => Launch(last)), Act("media-show", "Im Ordner zeigen", () => Launch(Path.GetDirectoryName(last)!)))));
        sp.Children.Add(ClipList());
        return KreativPage("Medien-Studio", sp);
    }

    private Control VideoEditor(string mf)
    {
        var box = new StackPanel { Spacing = 6, Name = "media-video" };
        var dur = _mediaDur ?? 0;
        box.Children.Add(P($"Datei: {Path.GetFileName(mf)}" + (dur > 0 ? $" · Länge {Clock(dur)}" : MediaStudio.IsAudio(mf) ? " · Ton" : ""), Text));
        if (FfmpegPath is { } ff && !MediaStudio.IsAudio(mf) && dur > 0)
        {
            // Zeitleiste: Vorschaubilder + Start/Ende-Marker
            var frames = MediaStudio.Frames(ff, mf, dur, 6, Path.Combine(MediaFolder, ".vorschau", Convert.ToHexString(System.Security.Cryptography.SHA1.HashData(System.Text.Encoding.UTF8.GetBytes(mf + File.GetLastWriteTimeUtc(mf).Ticks)))[..12]));
            var strip = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2, Name = "media-timeline" };
            foreach (var fr in frames) strip.Children.Add(Thumb(fr, 72));
            box.Children.Add(strip);
            var label = new TextBlock { Name = "media-range", Foreground = Text, Text = $"Ausschnitt {Clock(_mediaStart)} – {Clock(_mediaEnd)} ({Clock(_mediaEnd - _mediaStart)})" };
            var s = new Slider { Name = "media-from", Minimum = 0, Maximum = dur, Value = _mediaStart, Width = 480 };
            var e = new Slider { Name = "media-to", Minimum = 0, Maximum = dur, Value = _mediaEnd, Width = 480 };
            void Upd() { MediaSetRange(s.Value, Math.Max(s.Value, e.Value)); label.Text = $"Ausschnitt {Clock(_mediaStart)} – {Clock(_mediaEnd)} ({Clock(_mediaEnd - _mediaStart)})"; }
            s.ValueChanged += (_, _) => Upd(); e.ValueChanged += (_, _) => Upd();
            box.Children.Add(new StackPanel { Spacing = 2, Children = { Row(P("Anfang"), s), Row(P("Ende"), e), label } });
            box.Children.Add(Row(Act("media-cut", "Ausschnitt speichern", () => MediaCut(), true), Act("media-clip-add", "Zur Clipliste", () => { _clips.Add(mf); Say($"In der Clipliste: {_clips.Count} Clips.", "kreativ-medien"); })));
        }
        else if (FfmpegPath is null) box.Children.Add(P("Vorschau und Zeitleiste brauchen FFmpeg.", null, 11));
        var wrap = new WrapPanel();
        foreach (var j in MediaStudio.Jobs.Where(j => j.Id is not "trim" and not "image"))
        {
            var job = j; var b = Act("media-job-" + j.Id, j.Label, () => MediaRun(job.Id, _mediaStart, Math.Max(0.1, _mediaEnd - _mediaStart)));
            ToolTip.SetTip(b, j.Help); b.Margin = new Avalonia.Thickness(0, 0, 6, 6); wrap.Children.Add(b);
        }
        box.Children.Add(H("Schnell erledigt", 13)); box.Children.Add(wrap);
        return Card(box);
    }

    private Control ImageEditor(string mf)
    {
        var (w, h) = ImageTools.Size(mf);
        var box = new StackPanel { Spacing = 6, Name = "media-image" };
        box.Children.Add(P($"Bild: {Path.GetFileName(mf)} · {w}×{h} Pixel", Text));
        box.Children.Add(Thumb(mf, 220));
        NumericUpDown Num(string n, double v, double max) => new() { Name = n, Minimum = 0, Maximum = (decimal)max, Value = (decimal)v, Increment = 10, Width = 110, FormatString = "0" };
        var cx = Num("img-crop-x", _imgEdit.CropX ?? 0, w - 1); var cy = Num("img-crop-y", _imgEdit.CropY ?? 0, h - 1);
        var cw = Num("img-crop-w", _imgEdit.CropW ?? w, w); var ch = Num("img-crop-h", _imgEdit.CropH ?? h, h);
        var rot = new ComboBox { Name = "img-rotate-deg", ItemsSource = new[] { "nicht drehen", "90° rechts", "180°", "90° links" }, SelectedIndex = (_imgEdit.Rotate / 90) % 4 };
        var bright = new Slider { Name = "img-bright", Minimum = -100, Maximum = 100, Value = _imgEdit.Brightness, Width = 260 };
        var contrast = new Slider { Name = "img-contrast", Minimum = -100, Maximum = 100, Value = _imgEdit.Contrast, Width = 260 };
        var text = new TextBox { Name = "img-text", Text = _imgEdit.Text ?? "", PlaceholderText = "Text ins Bild (optional)", Width = 300 };
        var size = Num("img-text-size", _imgEdit.TextSize, 400);
        var fmt = new ComboBox { Name = "img-format", ItemsSource = new[] { "JPG", "PNG" }, SelectedIndex = _imgEdit.Format == ".png" ? 1 : 0 };
        box.Children.Add(Row(P("Zuschneiden x/y/Breite/Höhe"), cx, cy, cw, ch));
        box.Children.Add(Row(P("Drehen"), rot, P("Helligkeit"), bright));
        box.Children.Add(Row(P("Kontrast"), contrast));
        box.Children.Add(Row(P("Text"), text, P("Größe"), size));
        box.Children.Add(Row(P("Speichern als"), fmt, Act("img-save", "Bild speichern", () => MediaSaveImage(new ImageTools.Edit(
            (int)(cx.Value ?? 0), (int)(cy.Value ?? 0), (int)(cw.Value ?? w), (int)(ch.Value ?? h), (rot.SelectedIndex < 0 ? 0 : rot.SelectedIndex) * 90,
            (int)bright.Value, (int)contrast.Value, string.IsNullOrWhiteSpace(text.Text) ? null : text.Text, (int)(size.Value ?? 48), fmt.SelectedIndex == 1 ? ".png" : ".jpg")), true)));
        if (_mediaLast is { } last && File.Exists(last) && MediaStudio.IsImage(last)) box.Children.Add(new StackPanel { Name = "img-result", Children = { P("Ergebnis:", null, 11), Thumb(last, 220) } });
        return Card(box);
    }

    private Control ClipList()
    {
        var box = new StackPanel { Spacing = 4, Name = "media-clips" };
        box.Children.Add(H("Clips aneinanderhängen", 13));
        if (_clips.Count == 0) box.Children.Add(P("Noch keine Clips. Wähle ein Video und tippe „Zur Clipliste“.", null, 11));
        var n = 0;
        foreach (var c in _clips) box.Children.Add(P($"{++n}. {Path.GetFileName(c)}", Text, 12));
        box.Children.Add(Row(Act("media-concat", "Aneinanderhängen", () => MediaConcat(), true), Act("media-clips-clear", "Liste leeren", () => { _clips.Clear(); Say("Clipliste geleert.", "kreativ-medien"); })));
        return Card(box);
    }
}
