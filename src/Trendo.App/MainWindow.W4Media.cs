using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Trendo.Core;

namespace Trendo.App;

/// <summary>CORE-08: Kreativ- & Medien-Studio. Film schneiden, Bilder bearbeiten, mehrere Clips verbinden.</summary>
public partial class MainWindow
{
    private string? _mediaFile;
    private RgbImage? _bild;
    private string? _bildHinweis;
    private string? _mediaResult;
    private readonly List<(string Datei, double Start, double Ende)> _clips = [];
    public string MediaFolder => WorkFolder("Medien");
    public string? FfmpegPath => ToolPath("ffmpeg") is { } p && File.Exists(p) ? p : Cli.Find("ffmpeg");

    private void InitW4Media() => RegisterTab(KreativTabs, "kreativ-medien", "Medien bearbeiten", "CORE-08", MediaStudioView, "video", "umwandeln", "mp3", "mp4", "gif", "ffmpeg", "schneiden");

    static bool IstBild(string path)
    {
        var e = Path.GetExtension(path).ToLowerInvariant();
        return e is ".png" or ".jpg" or ".jpeg" or ".bmp" or ".webp" or ".gif";
    }

    static bool IstVideo(string path)
    {
        var e = Path.GetExtension(path).ToLowerInvariant();
        return e is ".mp4" or ".mkv" or ".mov" or ".avi" or ".webm";
    }

    public void MediaPick(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) { Say("Bitte zuerst den Pfad zu einer Video- oder Audiodatei eintragen.", "kreativ-medien"); return; }
        if (!File.Exists(path)) { Say("Datei nicht gefunden: " + Path.GetFileName(path), "kreativ-medien"); return; }
        _mediaFile = path;
        _bild = null; _bildHinweis = null;
        if (IstBild(path))
        {
            try { _bild = BildStudio.Oeffnen(path); }
            catch (Exception ex) when (ex is IOException or InvalidOperationException) { _bildHinweis = ex.Message; }
        }
        Say("Übernommen: " + Path.GetFileName(path) + (_bild is { } b ? $" · {b.Width} × {b.Height}" : ""), "kreativ-medien");
    }

    public (bool Ok, string Output) MediaRun(string jobId, double start = 0, double length = 10)
    {
        if (FfmpegPath is not { } ff) { Say("Dafür fehlt FFmpeg. Unter Zusatzprogramme kannst du es installieren.", "kreativ-medien"); return (false, ""); }
        if (_mediaFile is null) { Say("Bitte zuerst eine Datei wählen.", "kreativ-medien"); return (false, ""); }
        var (r, o) = MediaStudio.Run(ff, _mediaFile, jobId, start, length);
        _core.Audit.Append(Principal.LocalOwner.Name, "media.job", $"{jobId} exit={r.Exit}");
        if (r.Ok) _mediaResult = o;
        Say(r.Ok ? $"Fertig: {Path.GetFileName(o)}" : "Hat nicht geklappt: " + Short(r.Err), "kreativ-medien");
        return (r.Ok, o);
    }

    private Control MediaStudioView()
    {
        var sp = new StackPanel { Spacing = 8, Children = { P("Wähle eine Datei. Das Original bleibt immer erhalten. Den Schnitt siehst du an Start, Ende und Vorschaubildern.") } };
        if (FfmpegPath is null) sp.Children.Add(Card(Row(P("FFmpeg fehlt noch. Bilder als PNG oder BMP gehen trotzdem. Film und Ton brauchen FFmpeg.", Brush.Parse("#FACC15")), Nav2("Einrichten", "components"))));
        var files = FileShelf.Recent([MediaFolder, Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), Environment.GetFolderPath(Environment.SpecialFolder.MyMusic), Environment.GetFolderPath(Environment.SpecialFolder.MyPictures)], MediaStudio.Inputs, 15);
        sp.Children.Add(H("1. Datei wählen", 14));
        var path = new TextBox { Name = "media-path", Text = _mediaFile ?? "", PlaceholderText = "Pfad zur Datei", Width = 380 };
        path.TextChanged += (_, _) =>
        {
            var p = path.Text?.Trim('"', ' ') ?? "";
            if (p.Length == 0 || string.Equals(p, _mediaFile, StringComparison.Ordinal) || !File.Exists(p)) return;
            MediaPick(p);
        };
        sp.Children.Add(new WrapPanel { Children = { path, Act("media-use", "Übernehmen", () => MediaPick(path.Text?.Trim('"', ' ') ?? "")), Act("media-folder", "Medienordner öffnen", () => Launch(MediaFolder)) } });
        var i = 0;
        foreach (var f in files.Take(8)) { var fp = f.FullName; sp.Children.Add(Act($"media-file-{i++}", (fp == _mediaFile ? "● " : "") + f.Name, () => MediaPick(fp))); }
        if (_mediaFile is { } mf && File.Exists(mf))
        {
            double? dauer = FfmpegPath is { } ff ? MediaStudio.Duration(ff, mf) : null;
            sp.Children.Add(new TextBlock { Name = "media-duration", Foreground = Text, Text = dauer is { } d ? $"Dauer {d.ToString("0.#", Fach.De)} Sekunden" : "Dauer gerade nicht lesbar" });
            var start = new NumericUpDown { Name = "media-start", Minimum = 0, Maximum = 99999, Value = 0, Increment = 1, Width = 90, FormatString = "0" };
            var ende = new NumericUpDown { Name = "media-end", Minimum = 0, Maximum = 99999, Value = (decimal)Math.Max(1, Math.Min(dauer ?? 10, 99999)), Increment = 1, Width = 90, FormatString = "0" };
            var len = new NumericUpDown { Name = "media-len", Minimum = 1, Value = 10, Increment = 1, Width = 90, FormatString = "0" };
            var vorschau = VorschauBild(mf, dauer);
            if (vorschau is not null) sp.Children.Add(vorschau);
            if (IstVideo(mf) && FfmpegPath is not null && dauer is { } sek) sp.Children.Add(Daumen(mf, sek));
            sp.Children.Add(H("2. Was soll passieren?", 14));
            var wrap = new WrapPanel();
            foreach (var j in MediaStudio.Jobs)
            {
                var job = j;
                var b = Act("media-job-" + j.Id, j.Label, () =>
                {
                    var von = (double)(start.Value ?? 0m);
                    var bis = (double)(ende.Value ?? (decimal)(von + 1));
                    var laenge = job.Id == "trim" && bis > von ? bis - von : (double)(len.Value ?? 10m);
                    MediaRun(job.Id, von, laenge);
                });
                ToolTip.SetTip(b, j.Help); b.Margin = new Avalonia.Thickness(0, 0, 6, 6); wrap.Children.Add(b);
            }
            sp.Children.Add(Card(new StackPanel { Spacing = 6, Children = { P("Datei: " + Path.GetFileName(mf), Text), wrap,
                new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { P("Start (Sekunde)"), start, P("Ende"), ende, P("Länge"), len } } } }));
            sp.Children.Add(Card(new StackPanel { Spacing = 6, Children = {
                H("Mehrere Clips", 13),
                P(_clips.Count == 0 ? "Noch kein Clip in der Liste." : string.Join(" · ", _clips.Select(c => Path.GetFileName(c.Datei))), null, 11),
                new WrapPanel { Children = {
                    Act("media-clip-add", "Ausschnitt merken", () =>
                    {
                        var von = (double)(start.Value ?? 0); var bis = (double)(ende.Value ?? 0);
                        if (bis <= von) { Say("Bitte zuerst ein Ende nach dem Start eintragen.", "kreativ-medien"); return; }
                        _clips.Add((mf, von, bis));
                        Say($"Ausschnitt {von.ToString("0", Fach.De)}–{bis.ToString("0", Fach.De)} s gemerkt. {_clips.Count} in der Liste.", "kreativ-medien");
                    }),
                    Act("media-concat", "Clips aneinanderhängen", () => ClipsHängen()),
                    Act("media-play", "Ergebnis abspielen", () =>
                    {
                        if (_mediaResult is null || !File.Exists(_mediaResult)) { Say("Bitte zuerst eine Aufgabe starten.", "kreativ-medien"); return; }
                        Launch(_mediaResult); Say("Wiedergabe: " + Path.GetFileName(_mediaResult), "kreativ-medien");
                    }),
                } } } }));
            if (_bild is { } bild) sp.Children.Add(BildWerkzeuge(bild));
            else if (_bildHinweis is { } hin) sp.Children.Add(P(hin, Brush.Parse("#FACC15")));
        }
        return KreativPage("Medien-Studio", sp);
    }

    private Control? VorschauBild(string datei, double? dauer)
    {
        try
        {
            RgbImage? img = _bild;
            if (img is null && IstVideo(datei) && FfmpegPath is { } ff)
            {
                var png = Path.Combine(MediaFolder, "vorschau-" + Kurz(datei) + ".png");
                if (!File.Exists(png))
                {
                    var r = Cli.Run(ff, MediaCut.FrameArgs(datei, png, Math.Min(1, Math.Max(0, (dauer ?? 1) / 2))), null, 60000);
                    if (!r.Ok || !File.Exists(png)) return null;
                }
                img = Vision.Load(png);
            }
            if (img is null) return null;
            using var ms = new MemoryStream(VisionPlus.EncodePng(img));
            var bmp = new Bitmap(ms);
            return new Image { Name = "media-preview", Source = bmp, Width = 320, Height = 180, Stretch = Stretch.Uniform };
        }
        catch (Exception) { return null; }
    }

    private Control Daumen(string datei, double sek)
    {
        var zeile = new StackPanel { Spacing = 4, Children = { H("Zeitleiste", 13), P("Fünf Bilder von Anfang bis Ende. Start und Ende stehen in den Feldern darüber.") } };
        var wrap = new WrapPanel();
        var ff = FfmpegPath!;
        for (var k = 0; k < 5; k++)
        {
            var t = sek <= 0 ? 0 : sek * k / 4.0;
            var png = Path.Combine(MediaFolder, $"daumen-{Kurz(datei)}-{k}.png");
            if (!File.Exists(png)) Cli.Run(ff, MediaCut.FrameArgs(datei, png, t), null, 60000);
            if (!File.Exists(png)) continue;
            try
            {
                using var ms = new MemoryStream(File.ReadAllBytes(png));
                var img = new Image { Name = "media-thumb-" + k, Source = new Bitmap(ms), Width = 96, Height = 54, Stretch = Stretch.Uniform };
                var zeit = t;
                wrap.Children.Add(new StackPanel { Margin = new Avalonia.Thickness(0, 0, 6, 6), Children = { img, new TextBlock { Text = zeit.ToString("0.#", Fach.De) + " s", Foreground = Muted, FontSize = 10 } } });
            }
            catch (Exception) { }
        }
        zeile.Children.Add(wrap);
        return Card(zeile);
    }

    private Control BildWerkzeuge(RgbImage bild)
    {
        var x = new NumericUpDown { Name = "media-crop-x", Minimum = 0, Value = 0, Width = 70, FormatString = "0" };
        var y = new NumericUpDown { Name = "media-crop-y", Minimum = 0, Value = 0, Width = 70, FormatString = "0" };
        var w = new NumericUpDown { Name = "media-crop-w", Minimum = 1, Value = bild.Width, Width = 70, FormatString = "0" };
        var h = new NumericUpDown { Name = "media-crop-h", Minimum = 1, Value = bild.Height, Width = 70, FormatString = "0" };
        var hell = new NumericUpDown { Name = "media-hell-wert", Minimum = -100, Maximum = 100, Value = 15, Width = 70, FormatString = "0" };
        var kontrast = new NumericUpDown { Name = "media-kontrast-wert", Minimum = -100, Maximum = 100, Value = 10, Width = 70, FormatString = "0" };
        var text = new TextBox { Name = "media-caption", Text = "Hallo", Width = 160 };
        void Anwenden(RgbImage neu, string satz)
        {
            _bild = neu;
            var p = Path.Combine(MediaFolder, "bild-" + DateTime.Now.ToString("HHmmss") + ".png");
            BildStudio.SavePng(neu, p);
            _mediaResult = p;
            Say(satz + " Gespeichert: " + Path.GetFileName(p), "kreativ-medien");
        }
        return Card(new StackPanel { Spacing = 6, Children = {
            H("Bild bearbeiten", 13),
            P($"{bild.Width} × {bild.Height} Pixel. Drehen, Zuschneiden, Helligkeit und Text. Das Original bleibt liegen."),
            new WrapPanel { Children = { P("x"), x, P("y"), y, P("Breite"), w, P("Höhe"), h,
                Act("media-crop", "Zuschneiden", () => { try { Anwenden(BildStudio.Zuschneiden(bild, (int)(x.Value ?? 0), (int)(y.Value ?? 0), (int)(w.Value ?? 1), (int)(h.Value ?? 1)), "Ausschnitt übernommen."); } catch (Exception ex) { Say(ex.Message, "kreativ-medien"); } }) } },
            new WrapPanel { Children = {
                Act("media-rot", "Nach rechts drehen", () => Anwenden(BildStudio.Drehen90(bild), "Um 90 Grad gedreht.")),
                P("Helligkeit"), hell, Act("media-hell", "Heller oder dunkler", () => Anwenden(BildStudio.Helligkeit(bild, (double)(hell.Value ?? 0), 0), "Helligkeit geändert.")),
                P("Kontrast"), kontrast, Act("media-kontrast", "Kontrast", () => Anwenden(BildStudio.Helligkeit(bild, 0, (double)(kontrast.Value ?? 0)), "Kontrast geändert.")),
            } },
            new WrapPanel { Children = { text, Act("media-text", "Text ins Bild", () => Anwenden(BildStudio.Text(bild, text.Text ?? "", 8, 8), "Text eingefügt.")),
                Act("media-save-png", "Als PNG speichern", () => { var p = Frei("png"); BildStudio.SavePng(bild, p); _mediaResult = p; Say("PNG gespeichert: " + Path.GetFileName(p), "kreativ-medien"); }),
                Act("media-save-jpg", "Als JPG speichern", () => { var p = Frei("jpg"); BildStudio.SaveJpg(bild, p); _mediaResult = p; Say("JPG gespeichert: " + Path.GetFileName(p), "kreativ-medien"); }),
            } },
        } });
    }

    private void ClipsHängen()
    {
        if (FfmpegPath is not { } ff) { Say("Dafür fehlt FFmpeg. Unter Zusatzprogramme kannst du es installieren.", "kreativ-medien"); return; }
        if (_clips.Count < 2) { Say("Bitte zuerst mindestens zwei Ausschnitte merken.", "kreativ-medien"); return; }
        try
        {
            var (r, o) = MediaCut.Aneinander(ff, _clips, MediaFolder);
            if (!r.Ok) { Say("Hat nicht geklappt: " + Short(r.Err), "kreativ-medien"); return; }
            _mediaResult = o;
            Say("Fertiger Film: " + Path.GetFileName(o), "kreativ-medien");
        }
        catch (Exception ex) { Say(ex.Message, "kreativ-medien"); }
    }

    private string Frei(string ext)
    {
        var b = Path.GetFileNameWithoutExtension(_mediaFile ?? "bild") + "-fertig";
        var p = Path.Combine(MediaFolder, b + "." + ext);
        for (var i = 2; File.Exists(p); i++) p = Path.Combine(MediaFolder, $"{b}-{i}.{ext}");
        return p;
    }

    static string Kurz(string path)
    {
        var n = Path.GetFileNameWithoutExtension(path);
        var s = new string(n.Where(char.IsLetterOrDigit).Take(24).ToArray());
        return s.Length == 0 ? "clip" : s;
    }
}
