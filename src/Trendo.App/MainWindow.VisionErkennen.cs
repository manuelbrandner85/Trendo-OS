using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using SkiaSharp;
using Trendo.Core;
using Trendo.Core.Kreativ;
using Trendo.Core.Programs;
using Path = System.IO.Path;

namespace Trendo.App;

/// <summary>T-0699 – „Bilder erkennen“ erkennt wirklich: Objekte mit Rahmen (YOLOX-S, lokal), Bildbeschreibung mit Schlagworten
/// (Bildverstehens-Modell in Ollama; auf ausdrücklichen Wunsch Claude über das eigene Abo), Text im Bild (Tesseract) – für ein Bild oder einen ganzen Ordner.
/// Ergebnisse stehen im Dokument (überstehen Neustart), als Seitendatei „foto.jpg.xmp“ neben dem Foto (das Foto bleibt unverändert) und in der CSV-Liste.
/// Fehlt ein Werkzeug, läuft der Rest; was fehlt, steht in der Werkzeugzeile mit dem Einrichten-Weg.</summary>
public partial class MainWindow
{
    /// <summary>Tests: Bildbeschreibung ersetzen (Bilddatei → Antwort des Modells und wer geantwortet hat; null = keine Beschreibung).</summary>
    public static Func<string, Task<(string Answer, string Who)?>>? VisionDescribeOverride { get; set; }
    /// <summary>Tests: tatsächlich vorhandene Bildverstehens-Modelle in Ollama ersetzen.</summary>
    public static Func<Task<string?>>? VisionLocalModelOverride { get; set; }

    CancellationTokenSource? _viCancel;
    string? _viVlm; bool _viVlmChecked;
    public const int VisionMaxBatch = 500;

    /// <summary>Gespeicherte Erkennungen des Dokuments (nur Bilder auf diesem Rechner).</summary>
    public List<VisionTags.Recognition> VisionResults(ProgramDocument d) =>
        d.List("recog").Select(VisionTags.FromJson).OfType<VisionTags.Recognition>().Where(r => KreativSafe.LocalPath(r.File)).ToList();

    static void ViStore(ProgramDocument d, VisionTags.Recognition r) => d.Edit("Erkannt", x =>
    {
        if (x["recog"] is not JsonArray a) x["recog"] = a = [];
        for (var i = a.Count - 1; i >= 0; i--) if (string.Equals((a[i] as JsonObject)?["file"]?.ToString(), r.File, StringComparison.OrdinalIgnoreCase)) a.RemoveAt(i);
        a.Add(VisionTags.ToJson(r));
    });

    /// <summary>Erstes eingerichtetes Bildverstehens-Modell aus der KI-Bibliothek, das Ollama wirklich hat (qwen2.5vl, gemma3, llama3.2-vision).</summary>
    async Task<string?> VisionLocalModel()
    {
        if (VisionLocalModelOverride is { } o) return await o();
        if (_viVlmChecked) return _viVlm;
        _viVlmChecked = true;
        if (_opt.Isolated) return _viVlm = null;
        foreach (var m in KiLibrary.Models.Where(m => m.Kind == KiKind.BildVerstehen && m.Runtime == KiRuntime.Ollama))
            if (await _ollama.HasModelAsync(m.Tag) || await _ollama.HasModelAsync(m.Tag + ":latest")) return _viVlm = m.Tag;
        return _viVlm = null;
    }

    bool ViClaudeAllowed(ProgramDocument d) => d.Text("claude") == "ja";

    /// <summary>Bild für die Beschreibung auf höchstens 1024 Bildpunkte verkleinern (schneller, gleiche Aussage).</summary>
    static byte[] ViSmallJpeg(string file)
    {
        using var src = SKBitmap.Decode(file) ?? throw new InvalidDataException("Bild nicht lesbar.");
        var s = Math.Min(1.0, 1024.0 / Math.Max(src.Width, src.Height));
        using var small = s < 1 ? src.Resize(new SKImageInfo(Math.Max(1, (int)(src.Width * s)), Math.Max(1, (int)(src.Height * s))), new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear)) : null;
        using var img = SKImage.FromBitmap(small ?? src); using var data = img.Encode(SKEncodedImageFormat.Jpeg, 88);
        return data.ToArray();
    }

    /// <summary>Beschreibung: lokales Bildverstehens-Modell; sonst – nur wenn im Dokument erlaubt – Claude über das eigene Abo. Null: keine Quelle bereit.</summary>
    async Task<(string Answer, string Who)?> VisionDescribe(ProgramDocument d, string file, CancellationToken ct)
    {
        if (VisionDescribeOverride is { } o) return await o(file);
        if (await VisionLocalModel() is { } vlm)
        {
            var bytes = await Task.Run(() => ViSmallJpeg(file), ct);
            using (await GpuSlot("bild", "Bild beschreiben", ct))
                if (await _ollama.AskImagesAsync(vlm, VisionTags.DescribeQuestion, [bytes], ct) is { Length: > 0 } a) return (a, vlm + " (lokal)");
            return null;
        }
        if (!ViClaudeAllowed(d) || _opt.Isolated) return null;
        if (DefaultKiBackends().OfType<ClaudeKiBackend>().FirstOrDefault() is not { } cb || !await cb.IsUpAsync(ct)) return null;
        var dir = Path.Combine(Path.GetTempPath(), "trendo-claude-frage"); Directory.CreateDirectory(dir); // Arbeitsordner von Claude Code im Frage-Modus
        var name = "bild-" + Guid.NewGuid().ToString("N")[..8] + ".jpg"; var copy = Path.Combine(dir, name);
        try
        {
            await File.WriteAllBytesAsync(copy, ViSmallJpeg(file), ct);
            var answer = await cb.ChatAsync("claude", [new KiMessage("user", $"Lies zuerst die Bilddatei „{name}“ im Arbeitsordner mit dem Read-Werkzeug. {VisionTags.DescribeQuestion}")], ct);
            return answer.Trim().Length > 0 ? (answer, "Claude (dein Abo)") : null;
        }
        catch (Exception e) when (e is InvalidOperationException or KiModelBusyException or IOException) { return null; }
        finally { try { File.Delete(copy); } catch (IOException) { } }
    }

    async Task<string> VisionOcrText(string file, CancellationToken ct)
    {
        if (VisionTesseract() is not { } tess) return "";
        var run = new RealProcessRunner(); string? data = null; var lang = "eng";
        if (Ocr.HasUserGerman(_core.DataRoot)) { data = Ocr.UserTessdata(_core.DataRoot); lang = "deu"; }
        else if ((await Ocr.LanguagesAsync(run, tess)).Contains("deu")) lang = "deu";
        var r = await Ocr.RunAsync(run, tess, file, ct, data, lang);
        var t = r.Ok ? r.Text.Trim() : "";
        return System.Text.RegularExpressions.Regex.Matches(t, @"\p{L}{3,}").Count >= 1 ? t : ""; // Rauschen (einzelne Zeichen) ist kein Text
    }

    /// <summary>Ein Bild erkennen (öffentlich für Tests). Läuft jede bereite Quelle; ohne jede Quelle: null mit Hinweis.</summary>
    public async Task<VisionTags.Recognition?> VisionRecognizeFile(ProgramDocument d, string file, CancellationToken ct = default)
    {
        if (!KreativSafe.LocalFile(file, VisionMaxBytes) || !VisionExt.Contains(Path.GetExtension(file).ToLowerInvariant())) throw new InvalidOperationException("Wähle ein Bild auf diesem Rechner (höchstens 60 MB).");
        var bytes = await File.ReadAllBytesAsync(file, ct);
        var sha = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(bytes));
        int w = 0, h = 0;
        using (var codec = SKCodec.Create(new MemoryStream(bytes)))
        {
            if (codec is null) throw new InvalidDataException($"„{Path.GetFileName(file)}“ lässt sich nicht als Bild lesen.");
            (w, h) = (codec.Info.Width, codec.Info.Height);
            if ((long)w * h > VisionMaxPixels) throw new InvalidDataException($"„{Path.GetFileName(file)}“ ist zu groß (höchstens 100 Megapixel).");
        }
        var sources = new List<string>(); ObjectDetect.Result? objects = null;
        if (ObjectsReady)
        {
            var args = ObjectDetect.Args(ObjectsModelPath, file, null, ObjectsThreshold);
            var r = ObjectsRunOverride is { } o ? o(args) : await KiPyRun(KiRuntime.Objekte, args, 300_000);
            if (r.Ok) { objects = ObjectDetect.Parse(r.Out.Trim().Split('\n')[^1]); sources.Add("YOLOX-S"); }
        }
        ct.ThrowIfCancellationRequested();
        var desc = ""; IReadOnlyList<string> descTags = [];
        if (await VisionDescribe(d, file, ct) is { } ans) { (desc, descTags) = VisionTags.ParseDescription(ans.Answer); if (desc.Length > 0 || descTags.Count > 0) sources.Add(ans.Who); }
        ct.ThrowIfCancellationRequested();
        var text = await VisionOcrText(file, ct);
        if (VisionTesseract() is not null) sources.Add("Tesseract");
        if (sources.Count == 0) return null;
        var rec = VisionTags.Build(file, sha, w, h, objects, desc, descTags, text, sources, DateTime.UtcNow);
        ViStore(d, rec);
        // T-0764 (13. Prüfrunde): Seitendatei entsteht neben dem Bild – im Kinderprofil nur in der Freigabe (sonst ohne Seitendatei, die Erkennung bleibt)
        if (d.Text("sidecar") != "nein" && Trendo.Core.Sys.ChildWrite.Problem(CurrentPrincipal, Path.GetDirectoryName(Path.GetFullPath(file)) ?? file, checkName: false) is null) VisionTags.WriteSidecar(rec);
        return rec;
    }

    string ViNothingReady() => "Für die Erkennung ist noch nichts eingerichtet: „Objekterkennung einrichten“ (YOLOX, kostenlos) oder in der KI-Bibliothek ein Modell unter „Bilder verstehen“ laden. Text im Bild braucht Tesseract (Zusatzprogramme).";

    /// <summary>Das geöffnete Bild erkennen.</summary>
    public async Task VisionRecognizeCurrent(ProgramDocument d)
    {
        var img = d.Text("image");
        if (!KreativSafe.LocalFile(img, VisionMaxBytes)) { _progMsg[VisionKey] = "Öffne zuerst ein Bild – oder erkenne einen ganzen Ordner."; Show(VisionKey); return; }
        if (_viBusy) return;
        _viBusy = true; _progMsg[VisionKey] = "Bild wird erkannt …"; Show(VisionKey);
        try
        {
            var r = await VisionRecognizeFile(d, img);
            _progMsg[VisionKey] = r is null ? "⚠ " + ViNothingReady() : $"✓ Erkannt: {(r.Tags.Count > 0 ? string.Join(", ", r.Tags.Take(8)) : "keine Schlagworte")} ({string.Join(", ", r.Sources)}).";
            if (r is not null) _core.Audit.Append(Principal.LocalOwner.Name, "vision.erkennen", Path.GetFileName(img));
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or InvalidOperationException or UnauthorizedAccessException or FormatException or System.Text.Json.JsonException or System.ComponentModel.Win32Exception)
        { _progMsg[VisionKey] = "Erkennen ging nicht: " + ex.Message; }
        finally { _viBusy = false; Show(VisionKey); }
    }

    /// <summary>Stapel: alle Bilder eines Ordners (mit Unterordnern, höchstens 500) erkennen. Schon erkannte, unveränderte Fotos werden übersprungen. Anhalten jederzeit.</summary>
    public async Task<int> VisionRecognizeFolder(ProgramDocument d, string dir)
    {
        if (!KreativSafe.LocalDir(dir)) { _progMsg[VisionKey] = "Wähle einen Bildordner auf diesem Rechner."; Show(VisionKey); return 0; }
        if (_viBusy) return 0;
        d.Set("folder", dir, "Ordner");
        var files = Directory.EnumerateFiles(dir, "*", new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true })
            .Where(f => VisionExt.Contains(Path.GetExtension(f).ToLowerInvariant())).Order(StringComparer.OrdinalIgnoreCase).Take(VisionMaxBatch).ToList();
        if (files.Count == 0) { _progMsg[VisionKey] = "In diesem Ordner liegen keine Bilder."; Show(VisionKey); return 0; }
        var known = VisionResults(d).ToDictionary(r => r.File, r => r.Sha256, StringComparer.OrdinalIgnoreCase);
        _viBusy = true; _viCancel = new CancellationTokenSource(); var done = 0; var same = 0; var bad = 0; var none = false;
        try
        {
            for (var i = 0; i < files.Count; i++)
            {
                if (_viCancel.IsCancellationRequested) break;
                var f = files[i];
                _progMsg[VisionKey] = $"Erkenne Bild {i + 1} von {files.Count}: {Path.GetFileName(f)} …"; Show(VisionKey);
                try
                {
                    if (known.TryGetValue(f, out var sha) && sha == Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(await File.ReadAllBytesAsync(f)))) { same++; continue; }
                    if (await VisionRecognizeFile(d, f, _viCancel.Token) is null) { none = true; break; }
                    done++;
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex) when (ex is IOException or InvalidDataException or InvalidOperationException or UnauthorizedAccessException or FormatException or System.Text.Json.JsonException or System.ComponentModel.Win32Exception) { bad++; }
            }
            var stopped = _viCancel.IsCancellationRequested;
            _progMsg[VisionKey] = none ? "⚠ " + ViNothingReady()
                : $"{(stopped ? "Angehalten: " : "✓ ")}{done} Bilder erkannt" + (same > 0 ? $", {same} unverändert übernommen" : "") + (bad > 0 ? $", {bad} nicht lesbar" : "") + ". Suche oben, z. B. „zeig alle Fotos mit Hund“.";
            if (done > 0) _core.Audit.Append(Principal.LocalOwner.Name, "vision.ordner", $"{done} Bilder");
            return done;
        }
        finally { _viBusy = false; _viCancel.Dispose(); _viCancel = null; Show(VisionKey); }
    }

    async Task ViPickRecognizeFolder(ProgramDocument d)
    {
        if (await PickFolderPath("Bildordner erkennen") is { } f) await VisionRecognizeFolder(d, f);
    }

    /// <summary>Objekterkennung einrichten: KI-Laufzeit „Objekte“ (onnxruntime) und das YOLOX-S-Modell aus dem offiziellen GitHub-Release, nur mit passender Prüfsumme – erst nach Klick.</summary>
    async Task VisionObjectsSetup()
    {
        if (_viBusy) return;
        _viBusy = true; _progMsg[VisionKey] = "Objekterkennung wird eingerichtet (einmalig, ca. 120 MB) …"; Show(VisionKey);
        try
        {
            if (!KiStore.RuntimeReady(KiRuntime.Objekte)) await KiPyInstall(ObjectsModel, KiPyApps.For(KiRuntime.Objekte)!);
            var (ok, msg) = File.Exists(ObjectsModelPath) ? (true, "Modell ist schon da.")
                : await ObjectDetect.DownloadModelAsync(new HttpClient { Timeout = TimeSpan.FromMinutes(5) }.GetStreamAsync, Path.GetDirectoryName(ObjectsModelPath)!);
            _core.Audit.Append(Principal.LocalOwner.Name, "deps.model", "yolox_s.onnx " + (ok ? "ok" : "verworfen"));
            _progMsg[VisionKey] = ok && ObjectsReady ? "✓ Objekterkennung ist eingerichtet." : "⚠ " + msg;
        }
        catch (Exception e) when (e is InvalidOperationException or IOException or HttpRequestException or OperationCanceledException) { _progMsg[VisionKey] = "Einrichten ging nicht: " + Trendo.Core.FriendlyError.Say(e); }
        finally { _viBusy = false; Show(VisionKey); }
    }

    /// <summary>Welche Erkennung bereit ist – ehrlich, mit dem Einrichten-Weg für das, was fehlt.</summary>
    Control ViToolsCard(ProgramDocument d)
    {
        var sp = new StackPanel { Name = "vi-tools", Spacing = 5 };
        sp.Children.Add(H("Erkennung auf diesem PC", 13));
        var objects = ObjectsReady; var tess = VisionTesseract() is not null;
        var vlmTask = VisionLocalModel(); var vlm = vlmTask.IsCompleted ? vlmTask.Result : null;
        if (!vlmTask.IsCompleted) _ = vlmTask.ContinueWith(_ => Trendo.App.UiThread.Post(() => Show(VisionKey)), TaskScheduler.Default);
        void Line(string name, bool ready, string text, Control? fix = null)
        {
            var row = new DockPanel { Name = name };
            if (fix is not null) { DockPanel.SetDock(fix, Avalonia.Controls.Dock.Right); row.Children.Add(fix); }
            row.Children.Add(new TextBlock { Text = (ready ? "✓ " : "– ") + text, Foreground = ready ? K4Good : Muted, FontSize = 12, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center });
            sp.Children.Add(row);
        }
        Button Small(string name, string label, Action a) { var b = new Button { Name = name, Content = label, Padding = new Thickness(8, 3), FontSize = 11.5, IsEnabled = !_viBusy, Margin = new Thickness(8, 0, 0, 0) }; b.Click += (_, _) => a(); return b; }
        Line("vi-tool-objects", objects, objects ? "Objekte und Personen mit Rahmen (YOLOX-S, lokal)" : "Objekte mit Rahmen: noch nicht eingerichtet",
            objects ? null : Small("vi-objects-setup", "Objekterkennung einrichten (YOLOX, kostenlos, ca. 120 MB)", () => _ = VisionObjectsSetup()));
        var claude = ViClaudeAllowed(d);
        Line("vi-tool-describe", vlm is not null || claude, vlm is not null ? $"Bildbeschreibung und Schlagworte ({vlm}, lokal)" : claude ? "Bildbeschreibung über Claude (dein Abo – das Bild geht an deinen Claude-Zugang)" : "Bildbeschreibung: kein Bildverstehens-Modell eingerichtet",
            vlm is not null ? null : Small("vi-describe-setup", "In der KI-Bibliothek laden …", () => Show("ki-bibliothek")));
        Line("vi-tool-text", tess, tess ? "Text im Bild (Tesseract, lokal)" : "Text im Bild: Tesseract fehlt", tess ? null : Small("vi-tess-setup", "Zusatzprogramme …", () => Show("components")));
        if (vlm is null)
        {
            var cb = new CheckBox { Name = "vi-claude-allow", IsChecked = claude, Content = "Ohne lokales Modell: Beschreibung mit Claude erlauben (das Bild geht an deinen Claude-Zugang)", FontSize = 11.5, Foreground = Text };
            cb.IsCheckedChanged += (_, _) => d.Set("claude", cb.IsChecked == true ? "ja" : "", cb.IsChecked == true ? "Claude erlaubt" : "Claude aus");
            sp.Children.Add(cb);
        }
        var side = new CheckBox { Name = "vi-sidecar", IsChecked = d.Text("sidecar") != "nein", Content = "Schlagworte als Seitendatei neben dem Foto speichern (foto.jpg.xmp – das Foto bleibt unverändert)", FontSize = 11.5, Foreground = Text };
        side.IsCheckedChanged += (_, _) => d.Set("sidecar", side.IsChecked == true ? "" : "nein", "Seitendatei");
        sp.Children.Add(side);
        return KrCard(sp, "vi-tools-card");
    }

    /// <summary>Vorschau mit den Rahmen der erkannten Objekte (in Bildkoordinaten, mit dem Bild skaliert).</summary>
    static Control ViBoxedPreview(string file, VisionTags.Recognition? r)
    {
        var pic = new Image { Name = "vi-preview", Stretch = Stretch.Fill };
        Bitmap? bmp = null;
        try { using var fs = File.OpenRead(file); bmp = Bitmap.DecodeToWidth(fs, 1200); pic.Source = bmp; }
        catch (Exception ex) when (ex is IOException or ArgumentException or InvalidOperationException or UnauthorizedAccessException) { return new Border(); }
        int w = r is { Width: > 0 } ? r.Width : (int)bmp.Size.Width, h = r is { Height: > 0 } ? r.Height : (int)bmp.Size.Height;
        var canvas = new Canvas { Name = "vi-boxes", Width = w, Height = h };
        var gold = Brush.Parse("#E5B53A"); var stroke = Math.Max(2, Math.Max(w, h) / 300.0); var font = Math.Max(12, Math.Max(w, h) / 45.0);
        foreach (var o in r?.Objects ?? [])
        {
            var rect = new Rectangle { Width = Math.Max(1, o.X2 - o.X1), Height = Math.Max(1, o.Y2 - o.Y1), Stroke = gold, StrokeThickness = stroke };
            Canvas.SetLeft(rect, o.X1); Canvas.SetTop(rect, o.Y1); canvas.Children.Add(rect);
            var label = new Border { Background = gold, Padding = new Thickness(font / 3, 0), Child = new TextBlock { Text = $"{o.Label} {o.Score * 100:0} %", Foreground = Brushes.Black, FontSize = font, FontWeight = FontWeight.SemiBold } };
            Canvas.SetLeft(label, o.X1); Canvas.SetTop(label, Math.Max(0, o.Y1 - font * 1.4)); canvas.Children.Add(label);
        }
        var grid = new Grid { Width = w, Height = h, Children = { pic, canvas } };
        return new Viewbox { Stretch = Stretch.Uniform, MaxHeight = 380, Child = grid };
    }

    static WrapPanel ViChips(IEnumerable<string> tags, string name)
    {
        var wp = new WrapPanel { Name = name };
        foreach (var t in tags)
            wp.Children.Add(new Border { Background = Brush.Parse("#2A2410"), BorderBrush = Brush.Parse("#6B5520"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10), Padding = new Thickness(8, 1), Margin = new Thickness(0, 2, 5, 2),
                Child = new TextBlock { Text = t, FontSize = 11.5, Foreground = Brush.Parse("#F2D27A") } });
        return wp;
    }

    /// <summary>Erkennung des geöffneten Bildes: Schlagworte, Beschreibung, Objekte, Text, Quellen.</summary>
    static Control ViRecognitionPanel(VisionTags.Recognition r)
    {
        var sp = new StackPanel { Name = "vi-recog", Spacing = 5 };
        sp.Children.Add(H("Erkannt", 13));
        sp.Children.Add(ViChips(r.Tags, "vi-tags"));
        if (r.Description.Length > 0) sp.Children.Add(new TextBlock { Name = "vi-desc", Text = r.Description, Foreground = Text, FontSize = 12, TextWrapping = TextWrapping.Wrap });
        if (r.Objects.Count > 0) sp.Children.Add(new TextBlock { Name = "vi-objects", Text = "Objekte: " + string.Join(", ", r.Objects.GroupBy(o => o.Label).Select(g => $"{g.Count()} × {g.Key}")), Foreground = Muted, FontSize = 12, TextWrapping = TextWrapping.Wrap });
        if (r.Text.Length > 0) sp.Children.Add(new TextBlock { Name = "vi-recog-text", Text = "Text im Bild: " + KreativSafe.Cut(r.Text.Replace('\n', ' '), 300), Foreground = Muted, FontSize = 12, TextWrapping = TextWrapping.Wrap });
        sp.Children.Add(new TextBlock { Text = "Erkannt mit: " + string.Join(", ", r.Sources) + (File.Exists(VisionTags.SidecarPath(r.File)) ? " · Seitendatei " + Path.GetFileName(VisionTags.SidecarPath(r.File)) : ""), Foreground = Muted, FontSize = 11 });
        return sp;
    }

    /// <summary>Galerie aller erkannten Bilder, gefiltert durch die Suche. Klick öffnet das Bild.</summary>
    Control ViGallery(ProgramDocument d, List<VisionTags.Recognition> all)
    {
        var q = d.Text("query");
        var hits = VisionTags.Search(all, q);
        var sp = new StackPanel { Name = "vi-gallery-card-body", Spacing = 6 };
        sp.Children.Add(H(q.Trim().Length == 0 ? $"{all.Count} erkannte Bilder" : $"{hits.Count} von {all.Count} Bildern passen zu „{q.Trim()}“", 14));
        var wp = new WrapPanel { Name = "vi-gallery" };
        foreach (var (r, n) in hits.Take(120).Select((r, n) => (r, n)))
        {
            var thumb = new Image { Stretch = Stretch.UniformToFill, Width = 168, Height = 104 };
            try { using var fs = File.OpenRead(r.File); thumb.Source = Bitmap.DecodeToWidth(fs, 240); }
            catch (Exception ex) when (ex is IOException or ArgumentException or InvalidOperationException or UnauthorizedAccessException) { }
            var card = new StackPanel { Width = 168, Spacing = 2, Children = {
                new Border { CornerRadius = new CornerRadius(6), ClipToBounds = true, Background = Brush.Parse("#0A0F1A"), Child = thumb },
                new TextBlock { Text = Path.GetFileName(r.File), Foreground = Text, FontSize = 11.5, TextTrimming = TextTrimming.CharacterEllipsis },
                new TextBlock { Text = string.Join(", ", r.Tags.Take(4)), Foreground = Muted, FontSize = 11, TextTrimming = TextTrimming.CharacterEllipsis } } };
            var b = new Button { Name = $"vi-hit-{n}", Content = card, Background = Brushes.Transparent, Padding = new Thickness(4), Margin = new Thickness(0, 0, 6, 6) };
            var file = r.File; b.Click += (_, _) => _ = VisionOpen(d, file);
            ToolTip.SetTip(b, r.Description.Length > 0 ? r.Description : string.Join(", ", r.Tags));
            wp.Children.Add(b);
        }
        if (hits.Count == 0) sp.Children.Add(P("Kein Bild passt. Andere Wörter versuchen, z. B. „Hund“, „Auto“ oder „Strand“.", Muted, 12));
        sp.Children.Add(wp);
        var dups = VisionTags.Duplicates(all);
        if (dups.Count > 0) sp.Children.Add(P($"Doppelte Fotos: {dups.Count} Gruppe(n), z. B. " + string.Join(" = ", dups[0].Select(Path.GetFileName)), K4Warn, 12));
        return KrCard(sp, "vi-gallery-card");
    }

    /// <summary>Suchzeile: Alltagsfrage oder Schlagwort.</summary>
    Control ViSearchBox(ProgramDocument d)
    {
        var box = new TextBox { Name = "vi-search", Text = d.Text("query"), PlaceholderText = "Suchen, z. B. „zeig alle Fotos mit Hund“", MinWidth = 320 };
        void Go() { if ((box.Text ?? "") != d.Text("query")) { d.Set("query", box.Text ?? "", "Suche"); Show(VisionKey); } }
        OnEnter(box, Go); box.LostFocus += (_, _) => Go();
        return box;
    }
}
