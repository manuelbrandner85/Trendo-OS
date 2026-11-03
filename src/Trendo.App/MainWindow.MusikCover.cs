using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using SkiaSharp;
using Trendo.Core;

namespace Trendo.App;

/// <summary>Wunsch 03.11.2026: Cover-Bilder in Trendo Musik „perfekt wie ein Song-Cover“. Motiv aus der eingebauten Bild-KI (SDXL Turbo bzw. SD Turbo,
/// lokal über stable-diffusion.cpp) passend zu Genre und Stimmung, Titel und Name mit echter Schrift in einem Profi-Layout (SkiaSharp), 1400 × 1400 als JPEG.
/// Ohne Bild-KI: eigenes Foto oder ein gestalteter Farbhintergrund. Beim MP3-Export kommt das Cover in die Datei.</summary>
public partial class MainWindow
{
    /// <summary>Tests: Motiv erzeugen statt der Bild-KI (Beschreibung, Startwert, Ziel-PNG → erfolgreich?).</summary>
    public static Func<string, int, string, Task<bool>>? CoverImageOverride { get; set; }
    bool _msArtBusy;

    /// <summary>Bild-KI für Motive: zuerst SDXL Turbo (schöner), sonst SD Turbo – nur wenn Modell und Programm da sind.</summary>
    (KiLibraryModel Model, string Exe)? CoverModel()
    {
        var store = KiStore;
        foreach (var id in new[] { "sdxl-turbo", "sd-turbo" })
            if (KiLibrary.Models.FirstOrDefault(m => m.Id == id) is { } m && store.ModelReady(m) && store.RuntimeReady(m.Runtime)) return (m, store.RuntimeExe(m.Runtime));
        return null;
    }

    /// <summary>Motiv mit der Bild-KI malen (spart Grafikspeicher, damit es auch neben der Musik-KI läuft).</summary>
    async Task<string?> CoverMotif(string prompt, int seed, bool mayPauseMusicAi = true)
    {
        var png = Path.Combine(Path.GetTempPath(), $"trendo-cover-{Guid.NewGuid():N}.png");
        if (CoverImageOverride is { } o) return await o(prompt, seed, png) && File.Exists(png) ? png : null;
        if (_opt.Isolated || CoverModel() is not { } cm) return null;
        var xl = cm.Model.Id == "sdxl-turbo"; var size = xl ? "1024" : "512";
        IReadOnlyList<string> args = ["-m", KiStore.ModelFile(cm.Model, cm.Model.Files[0]), "-p", prompt, "-o", png, "--steps", xl ? "4" : "1", "--cfg-scale", "1.0", "-W", size, "-H", size, "--seed", seed.ToString(System.Globalization.CultureInfo.InvariantCulture), "--vae-tiling"];
        var r = await Task.Run(() => RunWithInput(cm.Exe, args, null));
        if (!r.Ok || !File.Exists(png)) r = await Task.Run(() => RunWithInput(cm.Exe, [.. args, "--offload-to-cpu"], null)); // wenig Grafikspeicher: Teile in den Arbeitsspeicher
        // Grafikkarte voll, weil die Musik-KI geladen ist (am 03.11.2026 auf der RTX 5070 gemessen: Vulkan sieht dann nur ~0,2 GB frei):
        // ist gerade kein Lied in Arbeit, pausiert Trendo seinen eigenen Musik-Server – er startet beim nächsten Lied von selbst wieder.
        if ((!r.Ok || !File.Exists(png)) && mayPauseMusicAi && _msRunning.Count == 0 && _aceServer is { HasExited: false } ace)
        {
            _msMsg = "Die Grafikkarte ist von der Musik-KI belegt – Trendo pausiert sie kurz für das Cover …"; if (CurrentView == MS) Show(MS);
            try { ace.Kill(true); await ace.WaitForExitAsync(); } catch (InvalidOperationException) { }
            _core.Audit.Append(Principal.LocalOwner.Name, "musik.cover.ace-pause", "");
            await Task.Delay(1500);
            r = await Task.Run(() => RunWithInput(cm.Exe, args, null));
        }
        if (!r.Ok || !File.Exists(png))
        {
            // letzter Weg: auf dem Prozessor (langsam, einige Minuten – aber es klappt)
            _msMsg = "Das Motiv entsteht auf dem Prozessor – das dauert einige Minuten …"; if (CurrentView == MS) Show(MS);
            r = await Task.Run(() => RunWithInput(cm.Exe, [.. args, "--backend", "cpu"], null));
        }
        _core.Audit.Append(Principal.LocalOwner.Name, "musik.cover.motiv", $"{cm.Model.Id} exit={r.Exit}");
        return r.Ok && File.Exists(png) ? png : null;
    }

    /// <summary>Cover für ein Lied erstellen: Motiv (KI, eigenes Foto oder Farbfläche) + Schrift im Layout. Gibt die Datei zurück.</summary>
    /// <param name="auto">Automatisch nach einem neuen Lied: pausiert die Musik-KI nie (dann notfalls auf dem Prozessor).</param>
    public async Task<string?> MsMakeCover(string songId, int? seed = null, string? layout = null, string? photo = null, string? artist = null, bool auto = false)
    {
        if (Songs.Get(songId) is not { } s) return null;
        var sd = seed ?? (s.CoverSeed == 0 ? Random.Shared.Next(1, 1_000_000) : s.CoverSeed);
        var lay = layout ?? s.CoverLayout; var name = artist ?? s.Artist;
        _msArtBusy = true; _msMsg = "Das Cover entsteht …"; if (CurrentView == MS) Show(MS);
        string? motif = null; var ownPhoto = photo is not null && File.Exists(photo);
        try
        {
            motif = ownPhoto ? photo : await CoverMotif(SongCover.Prompt(s.Style, s.Title, sd), sd, !auto);
            var dir = Path.Combine(Path.GetDirectoryName(s.File)!, "Cover"); Directory.CreateDirectory(dir);
            var file = Path.Combine(dir, $"{DevStudio.SafeName(s.Title)}-{sd}.jpg");
            await Task.Run(() => File.WriteAllBytes(file, ComposeCover(motif, s.Title, name, lay, s.Id)));
            var old = s.CoverFile;
            Songs.Update(s.Id, x => { x.CoverFile = file; x.CoverLayout = lay; x.CoverSeed = sd; x.Artist = name; });
            if (old.Length > 0 && old != file && File.Exists(old) && Path.GetDirectoryName(old) == dir) try { File.Delete(old); } catch (IOException) { } // altes Cover desselben Lieds (von Trendo erzeugt)
            _msMsg = motif is null && !ownPhoto
                ? "✓ Cover mit Farbfläche erstellt. Für ein gemaltes Motiv in der KI-Bibliothek unter „Bilder erzeugen“ SDXL Turbo einrichten – oder ein eigenes Foto wählen."
                : "✓ Cover fertig – gefällt es nicht, „Neu würfeln“.";
            _core.Audit.Append(Principal.LocalOwner.Name, "musik.cover", $"{s.Id} {lay} {(ownPhoto ? "foto" : motif is null ? "farbe" : "ki")}");
            return file;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException) { _msMsg = "Cover ging nicht: " + e.Message; return null; }
        finally
        {
            _msArtBusy = false;
            if (motif is not null && !ownPhoto) try { File.Delete(motif); } catch (IOException) { }
            if (CurrentView == MS) Show(MS);
        }
    }

    /// <summary>Cover zusammensetzen (1400 × 1400 JPEG): Motiv füllend, Verlauf für Lesbarkeit, Titel groß, Name in Versalien mit Laufweite.</summary>
    static byte[] ComposeCover(string? motif, string title, string artist, string layout, string id)
    {
        const int N = SongCover.Size;
        using var surface = SKSurface.Create(new SKImageInfo(N, N, SKColorType.Rgba8888, SKAlphaType.Premul));
        var c = surface.Canvas;
        using (var bmp = motif is null ? null : SKBitmap.Decode(motif))
        {
            if (bmp is not null)
            {
                var scale = Math.Max((float)N / bmp.Width, (float)N / bmp.Height);
                var w = bmp.Width * scale; var h = bmp.Height * scale;
                using var img = SKImage.FromBitmap(bmp);
                c.DrawImage(img, new SKRect((N - w) / 2, (N - h) / 2, (N + w) / 2, (N + h) / 2), new SKSamplingOptions(SKCubicResampler.Mitchell));
            }
            else
            {
                // gestaltete Farbfläche: zwei Lichtflecken auf dunklem Grund (Farben je Lied fest)
                var pal = new[] { ("#C9A227", "#5B3A00"), ("#2F86F6", "#0B1E46"), ("#E0475B", "#3B0A1A"), ("#22C55E", "#08321A"), ("#7C3AED", "#1E0B46"), ("#F59E0B", "#3D1E00"), ("#14B8A6", "#05302C"), ("#EC4899", "#3D0726") };
                var (a, b) = pal[Math.Abs(id.GetHashCode() % pal.Length)];
                c.Clear(SKColor.Parse(b));
                using var p1 = new SKPaint { Shader = SKShader.CreateRadialGradient(new SKPoint(N * 0.3f, N * 0.28f), N * 0.75f, [SKColor.Parse(a), SKColor.Parse(a).WithAlpha(0)], SKShaderTileMode.Clamp) };
                c.DrawRect(0, 0, N, N, p1);
                using var p2 = new SKPaint { Shader = SKShader.CreateRadialGradient(new SKPoint(N * 0.85f, N * 0.9f), N * 0.6f, [SKColor.Parse("#88FFFFFF").WithAlpha(60), SKColors.Transparent], SKShaderTileMode.Clamp) };
                c.DrawRect(0, 0, N, N, p2);
            }
        }
        if (layout != "ohne")
        {
            var bold = SKTypeface.FromFamilyName("Segoe UI", SKFontStyleWeight.Black, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright) ?? SKTypeface.Default;
            var light = SKTypeface.FromFamilyName("Segoe UI", SKFontStyleWeight.SemiBold, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright) ?? SKTypeface.Default;
            // Lesbarkeit: weicher Verlauf hinter der Schrift
            using (var shade = new SKPaint())
            {
                var top = layout == "oben";
                shade.Shader = layout == "mittig"
                    ? SKShader.CreateRadialGradient(new SKPoint(N / 2f, N / 2f), N * 0.6f, [new SKColor(0, 0, 0, 150), new SKColor(0, 0, 0, 30)], SKShaderTileMode.Clamp)
                    : SKShader.CreateLinearGradient(new SKPoint(0, top ? 0 : N), new SKPoint(0, top ? N * 0.5f : N * 0.45f), [new SKColor(0, 0, 0, 210), new SKColor(0, 0, 0, 0)], SKShaderTileMode.Clamp);
                c.DrawRect(0, 0, N, N, shade);
            }
            const float M = 96; // Rand
            // Titel: so groß wie möglich, höchstens zwei Zeilen
            var lines = WrapTitle(title.Trim().Length > 0 ? title.Trim() : "Ohne Titel", bold, N - 2 * M, out var size);
            using var tf = new SKFont(bold, size); using var nf = new SKFont(light, 46);
            using var white = new SKPaint { Color = SKColors.White, IsAntialias = true };
            using var soft = new SKPaint { Color = new SKColor(255, 255, 255, 220), IsAntialias = true };
            using var shadow = new SKPaint { Color = new SKColor(0, 0, 0, 140), IsAntialias = true, ImageFilter = SKImageFilter.CreateBlur(10, 10) };
            var lh = size * 1.04f; var name = artist.Trim().ToUpperInvariant();
            void DrawSpaced(string t, float x, float y, SKTextAlign align)
            {
                const float Track = 9; // Laufweite der Versalien
                var w = t.Sum(ch => nf.MeasureText(ch.ToString())) + Track * Math.Max(0, t.Length - 1);
                var cx = align == SKTextAlign.Center ? x - w / 2 : x;
                foreach (var ch in t) { c.DrawText(ch.ToString(), cx, y, nf, soft); cx += nf.MeasureText(ch.ToString()) + Track; }
            }
            void Title(float x, float y, SKTextAlign align)
            {
                foreach (var l in lines)
                {
                    c.DrawText(l, x + 4, y + 6, align, tf, shadow);
                    c.DrawText(l, x, y, align, tf, white); y += lh;
                }
            }
            switch (layout)
            {
                case "oben":
                    if (name.Length > 0) DrawSpaced(name, M, M + 40, SKTextAlign.Left);
                    Title(M, M + 70 + size, SKTextAlign.Left);
                    break;
                case "mittig":
                    using (var frame = new SKPaint { Color = new SKColor(255, 255, 255, 200), Style = SKPaintStyle.Stroke, StrokeWidth = 5, IsAntialias = true }) c.DrawRect(60, 60, N - 120, N - 120, frame);
                    var block = lines.Count * lh; var y0 = N / 2f - block / 2 + size * 0.8f;
                    Title(N / 2f, y0, SKTextAlign.Center);
                    if (name.Length > 0) DrawSpaced(name, N / 2f, y0 + block + 30, SKTextAlign.Center);
                    break;
                default: // unten
                    var yt = N - M - (name.Length > 0 ? 80 : 0) - (lines.Count - 1) * lh;
                    Title(M, yt, SKTextAlign.Left);
                    if (name.Length > 0) DrawSpaced(name, M, N - M, SKTextAlign.Left);
                    break;
            }
        }
        using var snap = surface.Snapshot();
        using var data = snap.Encode(SKEncodedImageFormat.Jpeg, 92);
        return data.ToArray();
    }

    /// <summary>Titel auf höchstens zwei Zeilen umbrechen und die größte passende Schriftgröße wählen.</summary>
    static List<string> WrapTitle(string title, SKTypeface face, float width, out float size)
    {
        for (size = 190; size > 60; size -= 6)
        {
            using var f = new SKFont(face, size);
            var words = title.Split(' ', StringSplitOptions.RemoveEmptyEntries); var lines = new List<string>(); var cur = "";
            foreach (var w in words)
            {
                var t = cur.Length == 0 ? w : cur + " " + w;
                if (f.MeasureText(t) <= width || cur.Length == 0) cur = t; else { lines.Add(cur); cur = w; }
            }
            if (cur.Length > 0) lines.Add(cur);
            if (lines.Count <= 2 && lines.All(l => f.MeasureText(l) <= width)) return lines;
        }
        size = 60; return [title];
    }

    /// <summary>Bereich „Cover-Bild“ im Lied: Vorschau, Layouts, Name, Neu würfeln, eigenes Foto.</summary>
    Control MsCoverPanel(MusicSong s)
    {
        var sp = new StackPanel { Name = "ms-art", Spacing = 6 };
        sp.Children.Add(H("Cover-Bild", 13));
        var layouts = new WrapPanel();
        foreach (var (id, label) in SongCover.Layouts)
        {
            var on = s.CoverLayout == id;
            var b = new Button { Name = "ms-art-layout-" + id, Content = label, FontSize = 12, Padding = new Thickness(9, 3), Margin = new Thickness(0, 0, 5, 5), Background = on ? Accent : Brushes.Transparent, BorderBrush = Edge, BorderThickness = new Thickness(1), IsEnabled = !_msArtBusy };
            var lid = id; b.Click += (_, _) => { if (s.CoverFile.Length > 0) _ = MsMakeCover(s.Id, s.CoverSeed, lid); else { Songs.Update(s.Id, x => x.CoverLayout = lid); Show(MS); } };
            layouts.Children.Add(b);
        }
        var artist = new TextBox { Name = "ms-art-artist", Text = s.Artist, Watermark = "Name auf dem Cover (z. B. dein Künstlername)", MinWidth = 220 };
        artist.LostFocus += (_, _) => { if ((artist.Text ?? "") != s.Artist) Songs.Update(s.Id, x => x.Artist = (artist.Text ?? "").Trim()); };
        var make = Act("ms-art-make", s.CoverFile.Length > 0 ? "🎲 Neu würfeln" : "🎨 Cover-Bild erstellen", () =>
        {
            Songs.Update(s.Id, x => x.Artist = (artist.Text ?? "").Trim());
            _ = MsMakeCover(s.Id, s.CoverFile.Length > 0 ? Random.Shared.Next(1, 1_000_000) : null, null, null, (artist.Text ?? "").Trim());
        });
        make.IsEnabled = !_msArtBusy;
        var own = Act("ms-art-own", "Eigenes Foto …", async () =>
        {
            if (await PickMedia("ms-art-photo", ["*.png", "*.jpg", "*.jpeg", "*.webp"]) is { } f) await MsMakeCover(s.Id, null, null, f, (artist.Text ?? "").Trim());
        });
        own.IsEnabled = !_msArtBusy;
        var hint = CoverModel() is null && CoverImageOverride is null
            ? P("Für gemalte Motive: in der KI-Bibliothek unter „Bilder erzeugen“ SDXL Turbo einrichten. Bis dahin: eigenes Foto oder Farbfläche.", Muted, 11)
            : P("Die Bild-KI malt ein Motiv zu Stil und Stimmung, Trendo setzt Titel und Name mit echter Schrift darauf (1400 × 1400).", Muted, 11);
        sp.Children.Add(layouts); sp.Children.Add(artist); sp.Children.Add(new WrapPanel { Children = { make, Spacer(own) } }); sp.Children.Add(hint);
        return sp;

        static Control Spacer(Control c) { c.Margin = new Thickness(6, 0, 0, 0); return c; }
    }

    /// <summary>MP3 mit eingebettetem Cover neben dem Lied speichern.</summary>
    void MsExportMp3WithCover(MusicSong s)
    {
        if (!File.Exists(s.CoverFile)) { _msMsg = "Erst ein Cover-Bild erstellen."; Show(MS); return; }
        if (!s.File.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase)) { _msMsg = "Das Lied ist keine MP3."; Show(MS); return; }
        var target = Path.Combine(Path.GetDirectoryName(s.File)!, Path.GetFileNameWithoutExtension(s.File) + " (mit Cover).mp3");
        try
        {
            File.WriteAllBytes(target, SongCover.EmbedInMp3(File.ReadAllBytes(s.File), File.ReadAllBytes(s.CoverFile), s.Title, s.Artist));
            _msMsg = "✓ MP3 mit Cover gespeichert: " + target; _core.Audit.Append(Principal.LocalOwner.Name, "musik.studio.export", "mp3+cover");
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException) { _msMsg = "Speichern ging nicht: " + e.Message; }
        Show(MS);
    }
}
