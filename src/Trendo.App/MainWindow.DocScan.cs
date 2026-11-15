using System.Globalization;
using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using SkiaSharp;
using Trendo.Core;
using Trendo.Core.Kreativ;
using Trendo.Core.Office;
using Trendo.Core.Programs;
using Trendo.Core.Sys;

namespace Trendo.App;

/// <summary>STUDIO-DOCSCAN-01 „Dokumente scannen“ (T-0673): Fotos von Blättern zu sauberen Scans wie in Microsoft Lens / Adobe Scan / NAPS2 (nur Vorbild).
/// Links die Seiten, in der Mitte Original mit ziehbaren Ecken und das Ergebnis, rechts Modus, Zuschnitt und Ablage. Kern: <see cref="DocScan"/>.
/// Texterkennung über das vorhandene Tesseract (<see cref="PdfTools.OcrSearchableAsync"/>), Scanner (WIA/TWAIN) und Kamera sind nicht angeschlossen.
/// Anforderungen und Prüfungen: docs/nachbau/scannen/anforderungen.md.</summary>
public partial class MainWindow
{
    public const string ScanKey = "scannen-programm";
    const string ScanStudio = "STUDIO-DOCSCAN-01";
    /// <summary>Tests: Pfad zu Tesseract (null = nicht eingerichtet) statt im Werkzeugordner zu suchen.</summary>
    public static Func<string?>? ScanTesseractOverride { get; set; }
    /// <summary>Tests: ersetzt Tesseract beim Erzeugen der durchsuchbaren PDF.</summary>
    public static IProcessRunner? ScanOcrRunner { get; set; }
    readonly Dictionary<string, int> _scanSel = [];
    readonly Dictionary<string, (DateTime Stamp, Pixmap Px)> _scanSrc = [];
    readonly Dictionary<string, Pixmap> _scanOut = [];
    bool _dsBusy;
    static readonly string[] ScanExt = [".jpg", ".jpeg", ".png", ".webp", ".bmp", ".tif", ".tiff", ".heic"];

    JsonObject ScanNew() => new()
    {
        ["pages"] = new JsonArray(), ["mode"] = "dokument", ["title"] = "", ["date"] = DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        ["folder"] = "", ["pageSize"] = "a4", ["skipBlank"] = true, ["last"] = "",
    };

    private void InitDocScan() => RegisterProgram(FachTabs, new ProgramSpec(ScanKey, ScanStudio, "Dokumente scannen")
    {
        Sentence = "Fotos von Briefen, Rechnungen und Whiteboards zu sauberen Scans – Blatt finden, entzerren, Schwarzweiß oder Farbe, als PDF mit Datum im Namen.",
        Folder = "Scans",
        GroupLevels = new() { ["Seiten"] = 0, ["Ecken"] = 0, ["Ablegen"] = 0 },
        Templates =
        [
            new("leer", "Neuer Scan", "Fotos oder einen Ordner hinzufügen", ScanNew),
            new("beispiel", "Beispiel: schräges Foto", "Ein schief fotografiertes Blatt zum Ausprobieren", () => ScanSample()),
        ],
        Ribbon =
        [
            ("Seiten", [
                new ProgramCmd("scan-add", "Fotos hinzufügen …", d => _ = ScanPickFiles(d), Primary: true, Tip: "Fotos oder Bilder von Dokumenten (JPG, PNG …) – mehrere auf einmal."),
                new ProgramCmd("scan-folder", "Ordner …", d => _ = ScanPickFolder(d), Tip: "Alle Bilder eines Ordners, nach Namen sortiert."),
                new ProgramCmd("scan-rotate", "⟳ Drehen", d => ScanRotate(d, ScanSelected(d), 1), Tip: "Seite um 90° im Uhrzeigersinn"),
                new ProgramCmd("scan-up", "▲", d => ScanMove(d, ScanSelected(d), -1), Tip: "Seite nach vorn"),
                new ProgramCmd("scan-down", "▼", d => ScanMove(d, ScanSelected(d), 1), Tip: "Seite nach hinten"),
                new ProgramCmd("scan-del", "Seite entfernen", d => ScanDelete(d, ScanSelected(d)), Tip: "Nur aus dem Scan – das Foto bleibt erhalten."),
            ]),
            ("Ecken", [
                new ProgramCmd("scan-auto", "Kanten finden", d => ScanAutoCorners(d, ScanSelected(d)), Tip: "Blatt im Foto suchen und die Ecken setzen."),
                new ProgramCmd("scan-full", "Ganzes Bild", d => ScanSetCorners(d, ScanSelected(d), null), Tip: "Nicht entzerren – das ganze Bild nehmen."),
            ]),
            ("Ablegen", [
                new ProgramCmd("scan-save", "Als PDF ablegen", d => ScanSavePdf(d), Primary: true, Tip: "Mehrseitige PDF, Name „Datum Titel.pdf“, im passenden Ordner – nichts wird überschrieben."),
                new ProgramCmd("scan-ocr", "Durchsuchbare PDF", d => _ = ScanSearchablePdf(d), Tip: "Mit Texterkennung (Tesseract, offline) – Suchen und Kopieren gehen dann."),
                new ProgramCmd("scan-open-pdf", "In Trendo PDF öffnen", d => ScanOpenInPdf(d), Tip: "Zuletzt abgelegte PDF in „Trendo PDF“ – unterschreiben, zusammenfügen, schwärzen."),
                new ProgramCmd("scan-scanner", "Scanner …", d => _progMsg[ScanKey] = "Ein Scanner (WIA/TWAIN) ist noch nicht angeschlossen. So geht es trotzdem: mit dem Scanner-Programm als Bild speichern oder das Blatt mit dem Handy fotografieren und hier „Fotos hinzufügen“.", Level: 1),
            ]),
        ],
        Exports =
        [
            new("pdf", "PDF (alle Seiten)", ".pdf", (d, f) => { ScanWritePdf(d, f); return f; }),
            new("png", "Aktuelle Seite als PNG", ".png", (d, f) => { File.WriteAllBytes(f, PngCodec.Encode(ScanPageImage(d, ScanSelected(d)), fast: false, dpi: 300)); return f; }),
        ],
        SimpleExports = 1,
        Tools = [.. DocScan.Modes.Select(m => new ProgramTool("scan-mode-" + m.Id, m.Id switch { "dokument" => "▤", "grau" => "◐", "foto" => "▣", "whiteboard" => "▭", _ => "○" }, m.Name, m.Hint,
            d => ProgramRun(ScanKey, () => ScanSetMode(d, ScanSelected(d), m.Id)), () => ProgramActive(ScanKey) is { } a && ScanPages(a).Count > 0 && ScanModeOf(a, ScanSelected(a)) == m.Id))],
        Workspace = DocScanWorkspace,
        Properties = DocScanProperties,
        Status = d => { var p = ScanPages(d); return p.Count == 0 ? "Noch keine Seite" : $"{p.Count} Seite(n) · Seite {ScanSelected(d) + 1} · {DocScan.ModeName(ScanModeOf(d, ScanSelected(d)))} · {DocScan.FileName(ScanDate(d), d.Text("title"))}"; },
        Help = "Blatt flach auf eine dunkle Unterlage legen und von oben fotografieren. Dann „Fotos hinzufügen“ oder „Ordner …“: Trendo findet die Blattkanten und entzerrt die Seite. Stimmen die Ecken nicht, "
             + "im Original an den Punkten ziehen. Links den Modus wählen (Dokument = Schwarzweiß, Whiteboard, Foto …), drehen und ordnen. Rechts Titel und Datum – daraus wird der Dateiname. „Als PDF ablegen“ speichert, ohne etwas zu überschreiben. Die Fotos selbst bleiben unverändert.",
    }, "scannen", "scanner", "einscannen", "dokument scannen", "lens", "adobe scan", "naps2", "rechnung scannen", "whiteboard", "pdf aus fotos", "entzerren");

    // ---------- Modell (öffentlich für Tests und die KI) ----------

    public sealed record ScanPage(string Id, string Src, (double X, double Y)[]? Corners, double Confidence, int Rotate, string? Mode, double[] Crop);

    public List<ScanPage> ScanPages(ProgramDocument d) => [.. d.List("pages").OfType<JsonObject>().Select(o => new ScanPage(
        o["id"].AsText() ?? "", o["src"].AsText() ?? "",
        o["corners"] is JsonArray c && c.Count == 4 ? [.. c.OfType<JsonArray>().Select(p => (ProgramDocument.ToNumber(p[0]), ProgramDocument.ToNumber(p[1])))] : null,
        ProgramDocument.ToNumber(o["conf"]), (int)ProgramDocument.ToNumber(o["rot"]), o["mode"].AsText() is { Length: > 0 } m ? m : null,
        o["crop"] is JsonArray cr && cr.Count == 4 ? [.. cr.Select(x => ProgramDocument.ToNumber(x))] : [0, 0, 0, 0]))];

    static JsonObject ScanJson(ScanPage p)
    {
        var o = new JsonObject { ["id"] = p.Id, ["src"] = p.Src, ["conf"] = Math.Round(p.Confidence, 2), ["rot"] = ((p.Rotate % 4) + 4) % 4, ["mode"] = p.Mode ?? "",
            ["crop"] = new JsonArray([.. p.Crop.Select(x => (JsonNode)Math.Round(x, 1))]) };
        o["corners"] = p.Corners is null ? null : new JsonArray([.. p.Corners.Select(c => (JsonNode)new JsonArray(Math.Round(c.X, 4), Math.Round(c.Y, 4)))]);
        return o;
    }

    void ScanSave(ProgramDocument d, string label, List<ScanPage> pages) => d.Edit(label, x => x["pages"] = new JsonArray([.. pages.Select(p => (JsonNode)ScanJson(p))]));

    ScanPage ScanAt(ProgramDocument d, int i) { var p = ScanPages(d); if (p.Count == 0) throw new InvalidOperationException("Es gibt noch keine Seite – erst „Fotos hinzufügen“."); return p[Math.Clamp(i, 0, p.Count - 1)]; }
    void ScanUpdate(ProgramDocument d, int i, string label, Func<ScanPage, ScanPage> change)
    {
        var pages = ScanPages(d); if (pages.Count == 0) throw new InvalidOperationException("Es gibt noch keine Seite – erst „Fotos hinzufügen“.");
        i = Math.Clamp(i, 0, pages.Count - 1); pages[i] = change(pages[i]); ScanSave(d, label, pages);
    }

    public int ScanSelected(ProgramDocument d) => Math.Clamp(_scanSel.GetValueOrDefault(d.Id), 0, Math.Max(0, d.List("pages").Count - 1));
    public void ScanSelect(ProgramDocument d, int i) => _scanSel[d.Id] = Math.Clamp(i, 0, Math.Max(0, d.List("pages").Count - 1));
    public string ScanModeOf(ProgramDocument d, int i) { var p = ScanPages(d); return p.Count == 0 ? d.Text("mode", "dokument") : p[Math.Clamp(i, 0, p.Count - 1)].Mode ?? d.Text("mode", "dokument"); }
    static DateTime ScanDateOf(string s) => DateTime.TryParseExact(s, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt) ? dt
        : DateTime.TryParse(s, CultureInfo.GetCultureInfo("de-DE"), DateTimeStyles.None, out var de) ? de : DateTime.Today;
    public DateTime ScanDate(ProgramDocument d) => ScanDateOf(d.Text("date"));

    /// <summary>Foto lesen (EXIF-Drehung beachten), längste Seite höchstens <paramref name="maxSide"/> Bildpunkte.</summary>
    public static Pixmap ScanDecode(string file, int maxSide = 3200)
    {
        if (!KreativSafe.LocalFile(file, 80L * 1024 * 1024)) throw new InvalidOperationException($"„{Path.GetFileName(file)}“ ist kein Bild auf diesem Rechner (höchstens 80 MB).");
        var bytes = File.ReadAllBytes(file);
        using var codec = SKCodec.Create(new MemoryStream(bytes)) ?? throw new InvalidDataException($"„{Path.GetFileName(file)}“ lässt sich nicht als Bild lesen.");
        if ((long)codec.Info.Width * codec.Info.Height > 120_000_000) throw new InvalidDataException($"„{Path.GetFileName(file)}“ ist zu groß (mehr als 120 Megapixel).");
        var origin = codec.EncodedOrigin;
        using var src = SKBitmap.Decode(codec) ?? throw new InvalidDataException($"„{Path.GetFileName(file)}“ lässt sich nicht als Bild lesen.");
        var s = Math.Min(1.0, maxSide / (double)Math.Max(src.Width, src.Height));
        int w = Math.Max(1, (int)Math.Round(src.Width * s)), h = Math.Max(1, (int)Math.Round(src.Height * s));
        using var rgba = new SKBitmap(new SKImageInfo(w, h, SKColorType.Rgba8888, SKAlphaType.Unpremul));
        using (var cv = new SKCanvas(rgba))
        {
            cv.Clear(SKColors.White);
            using var img = SKImage.FromBitmap(src);
            cv.DrawImage(img, new SKRect(0, 0, w, h), new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear));
        }
        var px = new Pixmap(w, h, (byte[])rgba.Bytes.Clone());
        return origin switch { SKEncodedOrigin.RightTop => DocScan.Rotate(px, 1), SKEncodedOrigin.BottomRight => DocScan.Rotate(px, 2), SKEncodedOrigin.LeftBottom => DocScan.Rotate(px, 3), _ => px };
    }

    Pixmap ScanSource(string file)
    {
        var stamp = File.Exists(file) ? File.GetLastWriteTimeUtc(file) : throw new FileNotFoundException("Das Foto gibt es nicht mehr: " + Path.GetFileName(file));
        if (_scanSrc.TryGetValue(file, out var c) && c.Stamp == stamp) return c.Px;
        var px = ScanDecode(file); _scanSrc[file] = (stamp, px);
        if (_scanSrc.Count > 12) _scanSrc.Remove(_scanSrc.Keys.First(k => k != file));
        return px;
    }

    static (double X, double Y)[] ScanToPixels((double X, double Y)[]? norm, Pixmap src) =>
        norm is null ? DocScan.FullFrame(src.W, src.H) : [.. norm.Select(c => (c.X * (src.W - 1), c.Y * (src.H - 1)))];

    /// <summary>Fertige Seite (entzerrt, gedreht, zugeschnitten, im Modus). <paramref name="maxSide"/> 2480 = A4 mit 300 dpi.</summary>
    public Pixmap ScanPageImage(ProgramDocument d, int i, int maxSide = 2480)
    {
        var p = ScanAt(d, i); var mode = p.Mode ?? d.Text("mode", "dokument");
        var key = $"{p.Src}|{File.GetLastWriteTimeUtc(p.Src).Ticks}|{string.Join(";", p.Corners?.Select(c => $"{c.X:0.####},{c.Y:0.####}") ?? [])}|{p.Rotate}|{mode}|{string.Join(",", p.Crop)}|{maxSide}";
        if (_scanOut.TryGetValue(key, out var hit)) return hit;
        var src = ScanSource(p.Src);
        var c = p.Crop; var res = DocScan.Process(src, ScanToPixels(p.Corners, src), p.Rotate, mode, (c[0], c[1], c[2], c[3]), maxSide);
        if (_scanOut.Count > 40) _scanOut.Clear();
        _scanOut[key] = res; return res;
    }

    /// <summary>Fotos als Seiten hinzufügen; das Blatt wird in jedem Foto gesucht. Gibt die Zahl der neuen Seiten zurück.</summary>
    public int ScanAddFiles(ProgramDocument d, IEnumerable<string> files)
    {
        var pages = ScanPages(d); var added = 0; var found = 0; var skipped = new List<string>();
        foreach (var f in files)
        {
            if (!ScanExt.Contains(Path.GetExtension(f).ToLowerInvariant())) { skipped.Add(Path.GetFileName(f)); continue; }
            Pixmap px;
            try { px = ScanSource(f); }
            catch (Exception e) when (e is InvalidDataException or InvalidOperationException or IOException) { skipped.Add(Path.GetFileName(f)); continue; }
            var corners = DocScan.FindPage(px, out var conf);
            (double, double)[]? norm = conf > 0 ? [.. corners.Select(c => (c.X / (px.W - 1), c.Y / (px.H - 1)))] : null;
            if (conf > 0) found++;
            pages.Add(new ScanPage(Guid.NewGuid().ToString("N")[..12], Path.GetFullPath(f), norm, conf, 0, null, [0, 0, 0, 0])); added++;
        }
        if (added == 0) throw new InvalidOperationException(skipped.Count > 0 ? "Keine lesbaren Bilder dabei: " + string.Join(", ", skipped.Take(5)) : "Es wurden keine Bilder gewählt.");
        ScanSave(d, added == 1 ? "Seite hinzugefügt" : "Seiten hinzugefügt", pages);
        ScanSelect(d, pages.Count - added);
        if (d.Title is "Unbenannt" or "" && d.Text("title").Length == 0) d.Title = "Scan " + ScanDate(d).ToString("d. MMMM yyyy", CultureInfo.GetCultureInfo("de-DE"));
        _progMsg[ScanKey] = $"{added} Seite(n) hinzugefügt – Blattkanten bei {found} von {added} gefunden." + (found < added ? " Bei den anderen bitte im Original die Ecken ziehen." : "") + (skipped.Count > 0 ? $" Übersprungen: {string.Join(", ", skipped.Take(3))}." : "");
        return added;
    }

    /// <summary>Alle Bilder eines Ordners (Namen natürlich sortiert, „2“ vor „10“).</summary>
    public int ScanAddFolder(ProgramDocument d, string folder)
    {
        if (!Directory.Exists(folder)) throw new DirectoryNotFoundException("Den Ordner gibt es nicht: " + folder);
        var files = Directory.GetFiles(folder).Where(f => ScanExt.Contains(Path.GetExtension(f).ToLowerInvariant()))
            .OrderBy(f => System.Text.RegularExpressions.Regex.Replace(Path.GetFileName(f).ToLowerInvariant(), @"\d+", m => m.Value.PadLeft(10, '0')), StringComparer.Ordinal).ToList();
        if (files.Count == 0) throw new InvalidOperationException("In diesem Ordner liegen keine Bilder (JPG, PNG, WEBP, BMP, TIFF).");
        return ScanAddFiles(d, files);
    }

    public void ScanSetCorners(ProgramDocument d, int i, IReadOnlyList<(double X, double Y)>? norm)
    {
        (double, double)[]? c = norm is null ? null : DocScan.Order([.. norm.Select(p => (Math.Clamp(p.X, 0, 1), Math.Clamp(p.Y, 0, 1)))]);
        if (c is not null && DocScan.QuadArea(c) < 0.01) throw new InvalidOperationException("Die Ecken liegen zu dicht beieinander – bitte an die vier Blattecken ziehen.");
        ScanUpdate(d, i, norm is null ? "Ganzes Bild" : "Ecken", p => p with { Corners = c });
        _progMsg[ScanKey] = norm is null ? "Seite nimmt das ganze Bild (nicht entzerrt)." : "Ecken gesetzt – die Seite wird entzerrt.";
    }

    public void ScanAutoCorners(ProgramDocument d, int i)
    {
        var p = ScanAt(d, i); var src = ScanSource(p.Src);
        var corners = DocScan.FindPage(src, out var conf);
        if (conf <= 0) { _progMsg[ScanKey] = "Kein Blatt erkannt – bitte die Ecken im Original von Hand ziehen. Tipp: dunkle Unterlage, gleichmäßiges Licht."; return; }
        ScanUpdate(d, i, "Kanten finden", x => x with { Corners = [.. corners.Select(c => (c.X / (src.W - 1), c.Y / (src.H - 1)))], Confidence = conf });
        _progMsg[ScanKey] = string.Create(CultureInfo.GetCultureInfo("de-DE"), $"Blatt gefunden (Sicherheit {conf * 100:0} %).");
    }

    public void ScanRotate(ProgramDocument d, int i, int quarters) => ScanUpdate(d, i, "Drehen", p => p with { Rotate = p.Rotate + quarters });
    public void ScanSetMode(ProgramDocument d, int i, string mode)
    {
        if (!DocScan.Modes.Any(m => m.Id == mode)) throw new ArgumentException("Unbekannter Modus: " + mode);
        ScanUpdate(d, i, "Modus", p => p with { Mode = mode }); _progMsg[ScanKey] = $"Seite {Math.Clamp(i, 0, ScanPages(d).Count - 1) + 1}: {DocScan.ModeName(mode)}.";
    }
    public void ScanSetModeAll(ProgramDocument d, string mode)
    {
        if (!DocScan.Modes.Any(m => m.Id == mode)) throw new ArgumentException("Unbekannter Modus: " + mode);
        d.Edit("Modus für alle", x => { x["mode"] = mode; if (x["pages"] is JsonArray a) foreach (var o in a.OfType<JsonObject>()) o["mode"] = ""; });
        _progMsg[ScanKey] = $"Alle Seiten: {DocScan.ModeName(mode)}.";
    }
    public void ScanCrop(ProgramDocument d, int i, double l, double t, double r, double b) => ScanUpdate(d, i, "Zuschneiden", p => p with { Crop = [Math.Clamp(l, 0, 45), Math.Clamp(t, 0, 45), Math.Clamp(r, 0, 45), Math.Clamp(b, 0, 45)] });

    public void ScanMove(ProgramDocument d, int i, int delta)
    {
        var pages = ScanPages(d); if (pages.Count < 2) return;
        i = Math.Clamp(i, 0, pages.Count - 1); var to = Math.Clamp(i + delta, 0, pages.Count - 1); if (to == i) return;
        var p = pages[i]; pages.RemoveAt(i); pages.Insert(to, p); ScanSave(d, "Reihenfolge", pages); ScanSelect(d, to);
        _progMsg[ScanKey] = $"Seite jetzt an Stelle {to + 1}.";
    }

    public void ScanDelete(ProgramDocument d, int i)
    {
        var pages = ScanPages(d); if (pages.Count == 0) return;
        i = Math.Clamp(i, 0, pages.Count - 1); pages.RemoveAt(i); ScanSave(d, "Seite entfernt", pages); ScanSelect(d, i - 1);
        _progMsg[ScanKey] = "Seite aus dem Scan entfernt – das Foto selbst bleibt erhalten.";
    }

    /// <summary>Ablage-Ordner: eigener Ordner oder „Scans/&lt;Art&gt;“ in den Projekten (Rechnungen, Briefe …).</summary>
    public string ScanFolder(ProgramDocument d) => d.Text("folder").Trim() is { Length: > 0 } f && Path.IsPathRooted(f) ? f : Path.Combine(WorkFolder("Scans"), DocScan.Folder(d.Text("title")));

    /// <summary>Seiten für die PDF – leere Seiten (Rückseiten) bleiben weg, wenn eingeschaltet. Gibt (Seiten, ausgelassen) zurück.</summary>
    (List<Pixmap> Pages, int Skipped) ScanFinalPages(ProgramDocument d)
    {
        var n = ScanPages(d).Count; if (n == 0) throw new InvalidOperationException("Es gibt noch keine Seite – erst „Fotos hinzufügen“.");
        var res = new List<Pixmap>(); var skipped = 0;
        for (var i = 0; i < n; i++) { var p = ScanPageImage(d, i); if (d.Data["skipBlank"].AsBool(true) && n > 1 && DocScan.IsBlank(p)) { skipped++; continue; } res.Add(p); }
        if (res.Count == 0) throw new InvalidOperationException("Alle Seiten sind leer – „Leere Seiten weglassen“ ausschalten oder andere Fotos wählen.");
        return (res, skipped);
    }

    public int ScanWritePdf(ProgramDocument d, string path)
    {
        var (pages, skipped) = ScanFinalPages(d);
        var n = DocScan.WritePdf(pages, path, d.Text("title"), d.Text("pageSize", "a4"), 300);
        _core.Audit.Append(Principal.LocalOwner.Name, "scannen.pdf", $"{n} Seiten {Path.GetFileName(path)}");
        d.Data["last"] = path; d.EditMeta(_ => { });
        _progMsg[ScanKey] = $"PDF mit {n} Seite(n) abgelegt: {path}" + (skipped > 0 ? $" ({skipped} leere Seite(n) weggelassen)" : "");
        return n;
    }

    /// <summary>Als PDF ablegen: Name aus Datum und Titel, Ordner nach Art – vorhandene Dateien werden nie überschrieben.</summary>
    public string ScanSavePdf(ProgramDocument d)
    {
        var dir = ScanFolder(d); Directory.CreateDirectory(dir);
        var f = DocScan.Unique(dir, DocScan.FileName(ScanDate(d), d.Text("title")));
        ScanWritePdf(d, f); NotifyFileResult(_progMsg[ScanKey]);
        return f;
    }

    string? ScanTesseract() => ScanTesseractOverride is { } o ? o() : _opt.Isolated ? null : TesseractOverride ?? ToolPath("tesseract");

    /// <summary>Durchsuchbare PDF mit dem vorhandenen Tesseract. Ohne Tesseract ein Hinweis, wie man es einrichtet – es wird nichts geladen.</summary>
    public async Task<string?> ScanSearchablePdf(ProgramDocument d)
    {
        if (_dsBusy) { _progMsg[ScanKey] = "Es läuft schon eine Texterkennung."; Show(ScanKey); return null; }
        var tess = ScanTesseract();
        if (tess is null) { _progMsg[ScanKey] = "Texterkennung braucht Tesseract (kostenlos, offline). Unter „Programme“ → „Tesseract OCR“ einrichten – danach hier noch einmal klicken. Die normale PDF geht schon jetzt mit „Als PDF ablegen“."; Show(ScanKey); return null; }
        var work = Path.Combine(Path.GetTempPath(), "trendo-scan-ocr-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(work);
        _dsBusy = true; _progMsg[ScanKey] = "Texterkennung läuft …"; Show(ScanKey);
        try
        {
            var (pages, _) = ScanFinalPages(d);
            var imgs = new List<string>(); var dpi = 300;
            for (var i = 0; i < pages.Count; i++)
            {
                var f = Path.Combine(work, $"seite-{i + 1:0000}.png"); File.WriteAllBytes(f, PngCodec.Encode(pages[i], fast: false, dpi: 300)); imgs.Add(f);
                dpi = (int)Math.Round(Math.Min(pages[i].W, pages[i].H) / 8.27);
            }
            var run = ScanOcrRunner ?? new RealProcessRunner(); string? data = null; var lang = "eng";
            if (Ocr.HasUserGerman(_core.DataRoot)) { data = Ocr.UserTessdata(_core.DataRoot); lang = "deu"; }
            else if ((await Ocr.LanguagesAsync(run, tess)).Contains("deu")) lang = "deu";
            var dir = ScanFolder(d); Directory.CreateDirectory(dir);
            var target = DocScan.Unique(dir, DocScan.FileName(ScanDate(d), d.Text("title")));
            var (n, err) = await PdfTools.OcrSearchableAsync(run, tess, imgs, target, data, lang, Math.Clamp(dpi, 72, 600), i => _ui.Post(() => { _progMsg[ScanKey] = $"Texterkennung: Seite {i + 1} von {imgs.Count} …"; }), _core.Audit);
            if (err is not null) { _progMsg[ScanKey] = err; return null; }
            d.Data["last"] = target; d.EditMeta(_ => { });
            _progMsg[ScanKey] = $"Durchsuchbare PDF ({n} Seiten, {(lang == "deu" ? "Deutsch" : "Englisch")}): {target}";
            NotifyFileResult(_progMsg[ScanKey]);
            return target;
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or UnauthorizedAccessException or InvalidDataException) { _progMsg[ScanKey] = "Texterkennung ging nicht: " + FriendlyError.Say(ex); return null; }
        finally { _dsBusy = false; try { Directory.Delete(work, true); } catch (IOException) { } Show(ScanKey); }
    }

    void ScanOpenInPdf(ProgramDocument d)
    {
        var f = d.Text("last");
        if (f.Length == 0 || !File.Exists(f)) f = ScanSavePdf(d);
        OpenPdf(f); Show("pdf");
    }

    JsonObject ScanSample()
    {
        var j = ScanNew(); j["title"] = "Beispiel Brief";
        try
        {
            var dir = Path.Combine(_core.DataRoot, "scannen", "beispiel"); Directory.CreateDirectory(dir);
            var f = Path.Combine(dir, "schraeges-foto.png");
            var w = 1200; var h = 900; (double, double)[] corners = [(260, 90), (930, 150), (1010, 820), (170, 760)];
            if (!File.Exists(f)) File.WriteAllBytes(f, PngCodec.Encode(DocScan.TestPhoto(w, h, corners), fast: false));
            var px = ScanDecode(f); var found = DocScan.FindPage(px, out var conf);
            var page = new ScanPage("beispiel", f, conf > 0 ? [.. found.Select(c => (c.X / (px.W - 1), c.Y / (px.H - 1)))] : null, conf, 0, null, [0, 0, 0, 0]);
            j["pages"] = new JsonArray(ScanJson(page));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException) { }
        return j;
    }

    async Task ScanPickFiles(ProgramDocument d)
    {
        var req = new PickRequest("scan-fotos", false, true, [.. ScanExt]);
        string[]? files;
        if (PickOverride is { } fake) files = await fake(req);
        else
        {
            var got = await StorageProvider.OpenFilePickerAsync(new Avalonia.Platform.Storage.FilePickerOpenOptions { Title = "Fotos von Dokumenten", AllowMultiple = true,
                FileTypeFilter = [new Avalonia.Platform.Storage.FilePickerFileType("Bilder") { Patterns = [.. ScanExt.Select(e => "*" + e)] }] });
            files = [.. got.Select(f => f.TryGetLocalPath()).OfType<string>()];
        }
        if (files is { Length: > 0 }) ProgramRun(ScanKey, () => ScanAddFiles(d, files));
    }

    async Task ScanPickFolder(ProgramDocument d)
    {
        if (await PickFolderPath("Ordner mit Fotos") is { } f) ProgramRun(ScanKey, () => ScanAddFolder(d, f));
    }

    static Bitmap ScanBitmap(Pixmap p) { using var ms = new MemoryStream(PngCodec.Encode(p)); return new Bitmap(ms); }

    // ---------- Oberfläche ----------

    Control DocScanWorkspace(ProgramDocument d)
    {
        var pages = ScanPages(d);
        if (pages.Count == 0)
        {
            var add = new Button { Name = "scan-empty-add", Content = "Fotos hinzufügen …", Padding = new Thickness(16, 8) }; add.Classes.Add("accent");
            add.Click += (_, _) => _ = ScanPickFiles(d);
            var folder = new Button { Name = "scan-empty-folder", Content = "Ordner …", Padding = new Thickness(16, 8) };
            folder.Click += (_, _) => _ = ScanPickFolder(d);
            return KrCard(new StackPanel { Spacing = 10, Children = { H("Noch keine Seite", 15),
                P("Blatt flach auf eine dunkle Unterlage legen, von oben fotografieren (Handy reicht), Fotos hier hinzufügen. Trendo findet die Blattkanten, entzerrt und macht daraus eine saubere PDF.", Muted, 12),
                new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { add, folder } },
                P("Scanner (WIA/TWAIN) und Kamera sind noch nicht angeschlossen – bitte als Bild speichern bzw. fotografieren.", Muted, 11) } }, "scan-empty");
        }
        var sel = ScanSelected(d); var page = pages[sel];

        // Seitenleiste links
        var list = new StackPanel { Name = "scan-pages", Spacing = 6 };
        for (var i = 0; i < pages.Count; i++)
        {
            var ii = i; Control thumb;
            try { thumb = new Image { Source = ScanBitmap(ScanPageImage(d, i, 220)), Height = 120, Stretch = Stretch.Uniform }; }
            catch (Exception e) when (e is IOException or InvalidDataException or InvalidOperationException) { thumb = P("Foto fehlt", K4Warn, 11); }
            var b = new Button { Name = "scan-thumb-" + i, Padding = new Thickness(4), BorderThickness = new Thickness(i == sel ? 2 : 1), BorderBrush = i == sel ? Accent : Edge, HorizontalAlignment = HorizontalAlignment.Stretch,
                Content = new StackPanel { Spacing = 2, Children = { thumb, new TextBlock { Text = $"Seite {i + 1} · {DocScan.ModeName(pages[i].Mode ?? d.Text("mode", "dokument"))}", FontSize = 10.5, Foreground = Muted } } } };
            ToolTip.SetTip(b, Path.GetFileName(pages[i].Src));
            b.Click += (_, _) => { ScanSelect(d, ii); Show(ScanKey); };
            list.Children.Add(b);
        }
        var left = new ScrollViewer { Content = list, Width = 170, VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto };

        // Original mit Ecken und Ergebnis
        Control original, result;
        try
        {
            var src = ScanSource(page.Src);
            var view = new ScanCornerView { Name = "scan-corners", Source = ScanBitmap(src.W > 1400 || src.H > 1400 ? src.Resize(src.W * 1400 / Math.Max(src.W, src.H), src.H * 1400 / Math.Max(src.W, src.H)) : src),
                Corners = page.Corners ?? [(0, 0), (1, 0), (1, 1), (0, 1)], MinHeight = 380, Accent = Accent, SourceW = src.W, SourceH = src.H };
            view.Changed = c => ProgramRun(ScanKey, () => ScanSetCorners(d, sel, c));
            original = view;
            result = new Image { Name = "scan-result", Source = ScanBitmap(ScanPageImage(d, sel, 1200)), Stretch = Stretch.Uniform, MinHeight = 380 };
        }
        catch (Exception e) when (e is IOException or InvalidDataException or InvalidOperationException) { original = P(FriendlyError.Say(e), K4Warn, 12); result = new Border(); }
        var conf = page.Corners is null ? "ganzes Bild" : page.Confidence > 0 ? string.Create(CultureInfo.InvariantCulture, $"Blatt erkannt ({page.Confidence * 100:0} %)") : "Ecken von Hand";
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,12,*"), RowDefinitions = new RowDefinitions("Auto,*"), Children = {
            new TextBlock { Text = $"Original – Ecken ziehen ({conf})", Foreground = Muted, FontSize = 12 },
            Pos(new TextBlock { Text = $"Ergebnis – {DocScan.ModeName(page.Mode ?? d.Text("mode", "dokument"))}", Foreground = Muted, FontSize = 12 }, 2) } };
        Grid.SetRow(original, 1); Grid.SetRow(result, 1); Grid.SetColumn(result, 2);
        grid.Children.Add(new Border { Background = Brush.Parse("#22000000"), CornerRadius = new CornerRadius(6), Child = original, [Grid.RowProperty] = 1 });
        grid.Children.Add(new Border { Background = Brush.Parse("#22000000"), CornerRadius = new CornerRadius(6), Child = result, [Grid.RowProperty] = 1, [Grid.ColumnProperty] = 2 });
        return new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,12,*"), Margin = new Thickness(4, 0, 10, 4), Children = { KrCard(left, "scan-pages-card"), Pos(KrCard(grid, "scan-view-card"), 2) } };
    }

    Control DocScanProperties(ProgramDocument d)
    {
        var sp = new StackPanel { Name = "scan-props", Spacing = 5 };
        sp.Children.Add(H("Modus", 13));
        var modes = new WrapPanel();
        foreach (var m in DocScan.Modes)
        {
            var id = m.Id; var b = new Button { Name = "scan-all-" + id, Content = m.Name, FontSize = 12, Margin = new Thickness(0, 0, 4, 4) };
            if (d.Text("mode", "dokument") == id) b.Classes.Add("accent");
            ToolTip.SetTip(b, m.Hint + " – für alle Seiten");
            b.Click += (_, _) => ProgramRun(ScanKey, () => ScanSetModeAll(d, id));
            modes.Children.Add(b);
        }
        sp.Children.Add(modes);
        sp.Children.Add(P("Links in der Werkzeugleiste gilt der Modus nur für die gewählte Seite.", Muted, 11));

        if (ScanPages(d).Count > 0)
        {
            sp.Children.Add(H("Zuschneiden (Rand in %)", 13));
            var p = ScanAt(d, ScanSelected(d)); var sel = ScanSelected(d);
            var names = new[] { "links", "oben", "rechts", "unten" };
            for (var k = 0; k < 4; k++)
            {
                var kk = k; var s = new Slider { Name = "scan-crop-" + names[k], Minimum = 0, Maximum = 45, Value = p.Crop[k], Width = 150 };
                s.PointerCaptureLost += (_, _) => ProgramRun(ScanKey, () => { var c = ScanAt(d, sel).Crop.ToArray(); c[kk] = Math.Round(s.Value, 1); ScanCrop(d, sel, c[0], c[1], c[2], c[3]); });
                sp.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { new TextBlock { Text = names[k], Width = 50, Foreground = Muted, FontSize = 12, VerticalAlignment = VerticalAlignment.Center }, s } });
            }
        }

        sp.Children.Add(H("Ablage", 13));
        sp.Children.Add(P("Titel (wird Teil des Dateinamens)", null, 11)); sp.Children.Add(KrBox(d, "title", "scan-title", "z. B. Rechnung Stadtwerke"));
        sp.Children.Add(P("Datum (JJJJ-MM-TT)", null, 11)); sp.Children.Add(KrBox(d, "date", "scan-date", DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)));
        sp.Children.Add(P("Ordner (leer = automatisch nach Art)", null, 11)); sp.Children.Add(KrBox(d, "folder", "scan-folder-box", "z. B. D:\\Ablage\\Steuer"));
        sp.Children.Add(new TextBlock { Name = "scan-target", Text = "→ " + Path.Combine(ScanFolder(d), DocScan.FileName(ScanDate(d), d.Text("title"))), Foreground = Muted, FontSize = 11, TextWrapping = TextWrapping.Wrap });
        var size = new ComboBox { Name = "scan-page-size", ItemsSource = new[] { "A4 (Bild eingepasst)", "Wie das Bild" }, SelectedIndex = d.Text("pageSize", "a4") == "auto" ? 1 : 0, FontSize = 12, MinWidth = 180 };
        size.SelectionChanged += (_, _) => { var v = size.SelectedIndex == 1 ? "auto" : "a4"; if (v != d.Text("pageSize", "a4")) ProgramRun(ScanKey, () => d.Set("pageSize", v, "Seitenformat")); };
        sp.Children.Add(size);
        var blank = new CheckBox { Name = "scan-skip-blank", Content = "Leere Seiten weglassen", IsChecked = d.Data["skipBlank"].AsBool(true), Foreground = Text, FontSize = 12 };
        blank.IsCheckedChanged += (_, _) => { if ((blank.IsChecked == true) != d.Data["skipBlank"].AsBool(true)) ProgramRun(ScanKey, () => d.Edit("Leere Seiten", x => x["skipBlank"] = blank.IsChecked == true)); };
        sp.Children.Add(blank);

        sp.Children.Add(H("Texterkennung", 13));
        sp.Children.Add(P(ScanTesseract() is null ? "◐ Tesseract ist nicht eingerichtet – unter „Programme“ → „Tesseract OCR“ (kostenlos, offline). Ohne entsteht eine PDF aus Bildern." : "✓ Tesseract gefunden – „Durchsuchbare PDF“ legt eine Textebene unter jede Seite.", Muted, 11));
        sp.Children.Add(H("Scanner und Kamera", 13));
        sp.Children.Add(P("◐ Ein Scanner (WIA/TWAIN) und die Kamera sind noch nicht angeschlossen. Fotos vom Handy oder Bilder aus dem Scanner-Programm hier einfach hinzufügen.", Muted, 11));
        return sp;
    }
}

/// <summary>Originalfoto mit vier ziehbaren Ecken (wie das Zuschneiden in Scan-Apps). Ecken als Anteil 0–1 der Bildgröße.</summary>
public sealed class ScanCornerView : Control, Avalonia.Rendering.ICustomHitTest
{
    public bool HitTest(Point point) => new Rect(Bounds.Size).Contains(point);
    public Bitmap? Source { get; set; }
    /// <summary>Größe des Originals in Bildpunkten (die Bitmap kann verkleinert sein).</summary>
    public int SourceW { get; set; } = 1;
    public int SourceH { get; set; } = 1;
    public (double X, double Y)[] Corners { get; set; } = [(0, 0), (1, 0), (1, 1), (0, 1)];
    public IBrush Accent { get; set; } = Brushes.Gold;
    /// <summary>Nach dem Loslassen einer Ecke.</summary>
    public Action<(double X, double Y)[]>? Changed { get; set; }
    int _drag = -1;

    public ScanCornerView() { ClipToBounds = true; Cursor = new Cursor(StandardCursorType.Cross); }

    public Rect ImageRect()
    {
        if (Source is null || Bounds.Width <= 0 || Bounds.Height <= 0) return default;
        var k = Math.Min((Bounds.Width - 16) / SourceW, (Bounds.Height - 16) / SourceH);
        double w = SourceW * k, h = SourceH * k;
        return new Rect((Bounds.Width - w) / 2, (Bounds.Height - h) / 2, w, h);
    }
    public Point ToScreen((double X, double Y) c) { var r = ImageRect(); return new Point(r.X + c.X * r.Width, r.Y + c.Y * r.Height); }

    public override void Render(DrawingContext ctx)
    {
        if (Source is null) return;
        var r = ImageRect(); if (r.Width <= 0) return;
        ctx.DrawImage(Source, r);
        var pen = new Pen(Accent, 2);
        var pts = Corners.Select(ToScreen).ToArray();
        for (var i = 0; i < 4; i++) ctx.DrawLine(pen, pts[i], pts[(i + 1) % 4]);
        foreach (var p in pts) ctx.DrawEllipse(Brushes.White, new Pen(Accent, 3), p, 8, 8);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var p = e.GetPosition(this); var pts = Corners.Select(ToScreen).ToArray();
        var best = -1; var bd = 22.0;
        for (var i = 0; i < 4; i++) { var dd = Math.Sqrt(Math.Pow(pts[i].X - p.X, 2) + Math.Pow(pts[i].Y - p.Y, 2)); if (dd < bd) { bd = dd; best = i; } }
        _drag = best; if (best >= 0) { e.Pointer.Capture(this); e.Handled = true; }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_drag < 0) return;
        var r = ImageRect(); if (r.Width <= 0) return; var p = e.GetPosition(this);
        Corners[_drag] = (Math.Clamp((p.X - r.X) / r.Width, 0, 1), Math.Clamp((p.Y - r.Y) / r.Height, 0, 1));
        InvalidateVisual();
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_drag < 0) return;
        _drag = -1; e.Pointer.Capture(null);
        Changed?.Invoke([.. Corners]);
    }
}
