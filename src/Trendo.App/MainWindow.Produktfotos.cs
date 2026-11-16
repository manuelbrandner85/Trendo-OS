using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Trendo.Core;
using Trendo.Core.Business;
using Trendo.Core.Kreativ;
using Trendo.Core.Programs;
using Path = System.IO.Path;

namespace Trendo.App;

/// <summary>Etappe „Business &amp; Geld“ Teil C (T-0677): Produktfotos für Shops, Kundenmeinungen, Marktanalyse.
/// STUDIO-PRODUCTPHOTO-01: Stapel-Bearbeitung wie in Händler-Fotowerkzeugen (Clean-Room nach dem Bedienkonzept von Photoroom, remove.bg, Pixelcut).
/// Anforderungen: docs/nachbau/produktfotos/anforderungen.md.</summary>
public partial class MainWindow
{
    public const string PpKey = "produktfotos-programm", RvKey = "kundenmeinungen-programm", MkKey = "marktanalyse-programm";
    private void InitShopStudios() { InitProduktfotos(); InitKundenmeinungen(); InitMarktanalyse(); }

    // ---------- gemeinsame Bausteine der drei Studios ----------

    static TextBox ShopBox(string name, string value, string hint, Action<string> commit, bool multi = false, double width = double.NaN)
    {
        var t = new TextBox { Name = name, Text = value, PlaceholderText = hint, AcceptsReturn = multi, TextWrapping = multi ? TextWrapping.Wrap : TextWrapping.NoWrap, MinHeight = multi ? 60 : 0, FontSize = 12.5, Width = width,
            VerticalContentAlignment = multi ? VerticalAlignment.Top : VerticalAlignment.Center };
        ToolTip.SetTip(t, hint);
        var shown = value; var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        void Commit() { timer.Stop(); var now = t.Text ?? ""; if (now != shown) { shown = now; commit(now); } }
        timer.Tick += (_, _) => Commit();
        t.TextChanged += (_, _) => { timer.Stop(); if ((t.Text ?? "") != shown) timer.Start(); };
        t.LostFocus += (_, _) => Commit();
        t.DetachedFromVisualTree += (_, _) => Commit();
        return t;
    }

    ComboBox ShopCombo(string key, string name, IEnumerable<(string Id, string Name)> items, string current, Action<string> set, double minWidth = 140)
    {
        var list = items.ToList();
        var c = new ComboBox { Name = name, ItemsSource = list.Select(i => i.Name).ToList(), SelectedIndex = Math.Max(0, list.FindIndex(i => i.Id == current)), MinWidth = minWidth, FontSize = 12 };
        c.SelectionChanged += (_, _) => { if (c.SelectedIndex >= 0 && list[c.SelectedIndex].Id != current) { var v = list[c.SelectedIndex].Id; ProgramRun(key, () => set(v)); } };
        return c;
    }

    static StackPanel ShopLabeled(string label, Control c) => new() { Spacing = 2, Children = { new TextBlock { Text = label, Foreground = Muted, FontSize = 11 }, c } };

    static Border ShopTile(string name, string big, string small, IBrush? fg = null) => KrCard(new StackPanel { Spacing = 2, Children = {
        new TextBlock { Text = big, Foreground = fg ?? Text, FontSize = 22, FontWeight = FontWeight.Bold }, new TextBlock { Text = small, Foreground = Muted, FontSize = 11.5, TextWrapping = TextWrapping.Wrap } } }, name);

    /// <summary>Freier Dateiname im Arbeitsordner (vorhandene Dateien werden nie überschrieben).</summary>
    string ShopFile(string folder, string stem, string ext)
    {
        var dir = WorkFolder(folder); stem = DevStudio.SafeName(stem);
        var f = Path.Combine(dir, stem + ext); for (var n = 2; File.Exists(f); n++) f = Path.Combine(dir, $"{stem} ({n}){ext}");
        return f;
    }

    /// <summary>Tests: Antwort der KI (Prompt → Text) statt des echten KI-Kerns – für Kundenmeinungen und Marktanalyse.</summary>
    public static Func<string, Task<string?>>? ShopAiOverride { get; set; }
    /// <summary>Tests: „heute“.</summary>
    public static Func<DateTime>? ShopNowOverride { get; set; }
    static DateTime ShopNow => (ShopNowOverride?.Invoke() ?? DateTime.Now).Date;

    /// <summary>Ein Auftrag an den vorhandenen KI-Kern (lokal oder verbunden); null = keine KI erreichbar.</summary>
    async Task<string?> ShopAsk(string prompt)
    {
        try
        {
            if (ShopAiOverride is { } o) return await o(prompt);
            if (!_kiConnected && _opt.Isolated) return null;
            using var label = KiActivityHub.Label("Business-Studio");
            var r = await Intel.AskAsync(new KiRequest(prompt, PreferredModel: KiModelName, UseKnowledge: false, AllowRemote: true));
            return r.Ok ? r.Text : null;
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or HttpRequestException or TaskCanceledException) { return null; }
    }

    static Bitmap ShopBitmap(Pixmap p) { using var ms = new MemoryStream(PngCodec.Encode(p)); return new Bitmap(ms); }

    // ======================================================================
    // Produktfotos für Shops
    // ======================================================================

    sealed class PpCache { public string File = ""; public long Stamp; public Pixmap? Src; public Pixmap? Out; public string Sig = ""; public IReadOnlyList<FotoBefund> Befunde = []; public string Hinweis = ""; }
    readonly Dictionary<string, PpCache> _pp = [];
    /// <summary>Vorschau-Größe (längste Seite) für Arbeitsfläche und Schnellprüfung.</summary>
    public const int PpPreview = 420;

    public static FotoStapel PpOf(ProgramDocument d) => FotoStapel.FromJson(d.Data["stapel"]);
    public void PpChange(ProgramDocument d, string label, Action<FotoStapel> change) { var s = PpOf(d); change(s); var j = s.ToJson(); d.Edit(label, x => x["stapel"] = j); }
    static JsonObject PpDoc(string markt, string bg = "#FFFFFF", string schatten = "weich", double rand = 5, string wz = "") =>
        new() { ["stapel"] = new FotoStapel { Einstellung = new FotoEinstellung { Markt = markt, Hintergrund = bg, Schatten = schatten, RandProzent = rand, Wasserzeichen = wz, Muster = markt == "amazon" ? "{nr}.{variante}" : "{nr}_{n}" } }.ToJson(), ["view"] = "nachher" };

    private void InitProduktfotos() => RegisterProgram(BusinessTabs, new ProgramSpec(PpKey, "STUDIO-PRODUCTPHOTO-01", "Produktfotos für Shops")
    {
        Sentence = "Viele Produktfotos auf einmal: Hintergrund weg, reinweiß oder farbig, Schatten, mittig auf die Vorgabe von Amazon, Etsy, eBay oder Shopify – mit Prüfung und Upload-Paket.",
        Folder = "Produktfotos",
        ScrollWorkspace = true,
        GroupLevels = new() { ["Fotos"] = 0, ["Ansicht"] = 0, ["Export"] = 0 },
        Templates =
        [
            new("amazon", "Amazon-Hauptbilder", "Reinweiß, Produkt ≥ 85 %, 2000 × 2000 px", () => PpDoc("amazon", rand: 4)),
            new("etsy", "Etsy-Angebot", "3000 × 2250 px (4:3), warmer Grund", () => PpDoc("etsy", "#F4EFE6")),
            new("ebay", "eBay", "1600 × 1600 px, weiß, ohne Wasserzeichen", () => PpDoc("ebay")),
            new("shopify", "Shopify-Shop", "2048 × 2048 px, weiß mit Schatten", () => PpDoc("shopify")),
            new("social", "Social Media mit Wasserzeichen", "1600 × 1600 px, farbiger Grund, Spiegelung", () => PpDoc("frei", "#E8EEF4", "spiegel", 10, "© Mein Shop")),
        ],
        Ribbon =
        [
            ("Fotos", [
                new ProgramCmd("pp-add", "＋ Fotos …", d => _ = PpPick(d), Primary: true, Tip: "Mehrere Fotos auf einmal (JPG, PNG, WebP)."),
                new ProgramCmd("pp-remove", "Entfernen", d => PpRemoveSelected(d)),
                new ProgramCmd("pp-check", "Alle prüfen", d => PpRefresh(d, true), Tip: "Alle Fotos neu bearbeiten und gegen die Vorgabe prüfen."),
            ]),
            ("Ansicht", [
                new ProgramCmd("pp-before", "Vorher", d => d.Data["view"] = "vorher"),
                new ProgramCmd("pp-after", "Nachher", d => d.Data["view"] = "nachher"),
            ]),
            ("Export", [
                new ProgramCmd("pp-package", "Upload-Paket (ZIP)", d => PpPackageNow(d), Tip: "Alle Fotos in voller Größe, umbenannt, mit Prüfbericht."),
                new ProgramCmd("pp-folder", "In Ordner speichern", d => PpSaveFolderNow(d)),
            ]),
        ],
        Exports =
        [
            new("zip", "Upload-Paket (ZIP mit Prüfbericht)", ".zip", (d, f) => PpPackage(d, f)),
            new("csv", "Prüfbericht (CSV)", ".csv", (d, f) => { File.WriteAllText(f, PpReport(d, full: true), new UTF8Encoding(true)); return f; }),
        ],
        SimpleExports = 1,
        Workspace = PpWorkspace,
        Properties = PpProperties,
        Status = d =>
        {
            var s = PpOf(d); var v = ProduktFotos.Vorgabe(s.Einstellung.Markt);
            var ok = s.Fotos.Count(f => _pp.TryGetValue(f.Id, out var c) && c.Out is not null && ProduktFotos.Bestanden(c.Befunde));
            return $"{s.Fotos.Count} Fotos · {v.Name} {v.Breite} × {v.Hoehe} px · {ok} von {s.Fotos.Count} bestehen die Prüfung · Vorgaben Stand {ProduktFotos.Stand}";
        },
        Help = "Oben „＋ Fotos“ – dann rechts den Marktplatz wählen. Jedes Foto wird freigestellt (Hintergrund vom Rand her, Toleranz einstellbar), aufgehellt, im Weißabgleich korrigiert und mittig mit Rand auf die Vorgabe gesetzt. "
             + "Die Ampel zeigt, ob Größe, Hintergrund, Füllung und Format passen. Das eigene Freistellen klappt gut bei einfarbigem Hintergrund (Fotokarton, Lichtzelt); für unruhige Hintergründe oben „kie.ai“ → Hintergrund entfernen (eigenes Konto, kostenpflichtig, nur nach Bestätigung). "
             + "Trendo lädt nichts hoch. Vorgaben mit Stand-Datum – vor dem Hochladen in der Hilfe des Marktplatzes gegenprüfen.",
    }, "produktfotos", "produktfoto", "freistellen", "hintergrund entfernen", "weißer hintergrund", "amazon bilder", "etsy fotos", "shop fotos", "photoroom", "remove.bg", "pixelcut", "artikelbilder");

    async Task PpPick(ProgramDocument d)
    {
        string[]? files;
        var req = new PickRequest("pp-add", false, true, [".jpg", ".jpeg", ".png", ".webp"]);
        if (PickOverride is { } fake) files = await fake(req);
        else
        {
            var got = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { Title = "Produktfotos", AllowMultiple = true, FileTypeFilter = [new FilePickerFileType("Fotos") { Patterns = ["*.jpg", "*.jpeg", "*.png", "*.webp"] }] });
            files = got.Select(f => f.TryGetLocalPath()).OfType<string>().ToArray();
        }
        if (files is { Length: > 0 }) ProgramRun(PpKey, () => PpAdd(d, files));
    }

    /// <summary>Fotos hinzufügen; die Artikelnummer wird aus dem Dateinamen vorgeschlagen (Teil vor „_“ oder „-“).</summary>
    public int PpAdd(ProgramDocument d, IEnumerable<string> files)
    {
        var list = files.Where(File.Exists).ToList();
        if (list.Count == 0) throw new FileNotFoundException("Keine der Dateien gibt es.");
        var have = PpOf(d).Fotos.Select(f => Path.GetFullPath(f.Datei)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var neu = list.Where(f => !have.Contains(Path.GetFullPath(f))).Select(f =>
        {
            var stem = Path.GetFileNameWithoutExtension(f); var cut = stem.IndexOfAny(['_', ' ']);
            return new ProduktFoto { Id = NewId("f"), Datei = f, Artikelnummer = cut > 0 ? stem[..cut] : stem };
        }).ToList();
        PpChange(d, "Fotos hinzugefügt", s => s.Fotos.AddRange(neu));
        if (d.Text("sel").Length == 0 && neu.Count > 0) d.Data["sel"] = neu[0].Id;
        _progMsg[PpKey] = $"{neu.Count} Foto(s) hinzugefügt" + (neu.Count < list.Count ? $", {list.Count - neu.Count} waren schon da" : "") + ". Artikelnummer rechts prüfen.";
        return neu.Count;
    }

    void PpRemoveSelected(ProgramDocument d)
    {
        var id = d.Text("sel"); var s = PpOf(d);
        var f = s.Fotos.FirstOrDefault(x => x.Id == id) ?? throw new InvalidOperationException("Erst ein Foto anklicken.");
        PpChange(d, "Foto entfernt", x => x.Fotos.RemoveAll(y => y.Id == id)); _pp.Remove(id);
        d.Data["sel"] = PpOf(d).Fotos.FirstOrDefault()?.Id ?? "";
        _progMsg[PpKey] = $"„{Path.GetFileName(f.Datei)}“ entfernt – Rückgängig holt es zurück.";
    }

    static Pixmap PpLoad(string file, int maxSide = 0)
    {
        var p = ArtDecode(file);
        if (maxSide > 0 && Math.Max(p.W, p.H) > maxSide) { var sc = maxSide / (double)Math.Max(p.W, p.H); p = ProduktFotos.Skalieren(p, Math.Max(1, (int)(p.W * sc)), Math.Max(1, (int)(p.H * sc))); }
        return p;
    }

    /// <summary>Wasserzeichen-Text als Bild (weiß mit dunkler Kante, damit es auf jedem Grund lesbar ist).</summary>
    static Pixmap? PpMark(string text, int canvasW)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var size = Math.Max(10f, canvasW * 0.035f);
        using var font = new SkiaSharp.SKFont(SkiaSharp.SKTypeface.Default, size);
        var w = (int)Math.Ceiling(font.MeasureText(text)) + 8; var h = (int)Math.Ceiling(size * 1.4) + 4;
        using var bmp = new SkiaSharp.SKBitmap(new SkiaSharp.SKImageInfo(w, h, SkiaSharp.SKColorType.Rgba8888, SkiaSharp.SKAlphaType.Unpremul));
        using (var c = new SkiaSharp.SKCanvas(bmp))
        {
            c.Clear(SkiaSharp.SKColors.Transparent);
            using var stroke = new SkiaSharp.SKPaint { Color = new SkiaSharp.SKColor(0, 0, 0, 160), IsAntialias = true, Style = SkiaSharp.SKPaintStyle.Stroke, StrokeWidth = Math.Max(1.5f, size / 12) };
            using var fill = new SkiaSharp.SKPaint { Color = SkiaSharp.SKColors.White, IsAntialias = true };
            c.DrawText(text, 4, size * 1.05f + 2, font, stroke); c.DrawText(text, 4, size * 1.05f + 2, font, fill);
        }
        var px = new Pixmap(w, h);
        for (var y = 0; y < h; y++) for (var x = 0; x < w; x++) { var col = bmp.GetPixel(x, y); var i = (y * w + x) * 4; px.Px[i] = col.Red; px.Px[i + 1] = col.Green; px.Px[i + 2] = col.Blue; px.Px[i + 3] = col.Alpha; }
        return px;
    }

    /// <summary>Bild in das gewählte Format kodieren (JPEG über Skia, PNG über den eigenen Kodierer).</summary>
    static byte[] PpEncode(Pixmap p, string format)
    {
        if (format is "png") return PngCodec.Encode(p, fast: false);
        using var bmp = new SkiaSharp.SKBitmap(new SkiaSharp.SKImageInfo(p.W, p.H, SkiaSharp.SKColorType.Rgba8888, SkiaSharp.SKAlphaType.Unpremul));
        var tmp = (byte[])p.Px.Clone(); // Rgba8888 = gleiche Reihenfolge wie Pixmap
        for (var i = 0; i < tmp.Length; i += 4) tmp[i + 3] = 255; // JPEG kennt keine Transparenz
        System.Runtime.InteropServices.Marshal.Copy(tmp, 0, bmp.GetPixels(), tmp.Length);
        using var img = SkiaSharp.SKImage.FromBitmap(bmp);
        using var data = img.Encode(SkiaSharp.SKEncodedImageFormat.Jpeg, 92);
        return data.ToArray();
    }

    /// <summary>Vorschau aller Fotos (verkleinert) bearbeiten und prüfen. <paramref name="force"/> = auch Unverändertes neu rechnen.</summary>
    public void PpRefresh(ProgramDocument d, bool force = false)
    {
        var s = PpOf(d); var e = s.Einstellung; var v = ProduktFotos.Vorgabe(e.Markt);
        var sig = System.Text.Json.JsonSerializer.Serialize(e);
        double sc = PpPreview / (double)Math.Max(v.Breite, v.Hoehe); int pw = Math.Max(1, (int)(v.Breite * sc)), ph = Math.Max(1, (int)(v.Hoehe * sc));
        var mark = PpMark(e.Wasserzeichen, pw);
        foreach (var f in s.Fotos)
        {
            if (!_pp.TryGetValue(f.Id, out var c)) _pp[f.Id] = c = new PpCache();
            var stamp = File.Exists(f.Datei) ? File.GetLastWriteTimeUtc(f.Datei).Ticks : -1;
            if (stamp < 0) { c.Src = null; c.Out = null; c.Hinweis = "Datei fehlt: " + Path.GetFileName(f.Datei); c.Befunde = [new("Datei", false, "nicht gefunden", true)]; continue; }
            if (c.File != f.Datei || c.Stamp != stamp || c.Src is null) { c.Src = PpLoad(f.Datei, 700); c.File = f.Datei; c.Stamp = stamp; c.Sig = ""; }
            var key = sig + "|" + f.Hauptbild;
            if (!force && c.Sig == key && c.Out is not null) continue;
            try
            {
                var r = ProduktFotos.Bearbeiten(c.Src, e, mark, pw, ph);
                c.Out = r.Bild; c.Sig = key;
                c.Befunde = ProduktFotos.Pruefen(r.Bild, v, e.Format, -1, mark is not null, f.Hauptbild, (v.Breite, v.Hoehe));
                c.Hinweis = r.Frei?.Hinweis ?? "Ohne Freistellen.";
            }
            catch (InvalidOperationException ex) { c.Out = null; c.Hinweis = ex.Message; c.Befunde = [new("Objekt", false, ex.Message, true)]; }
        }
        foreach (var gone in _pp.Keys.Except(s.Fotos.Select(f => f.Id)).ToList()) _pp.Remove(gone);
    }

    public IReadOnlyList<FotoBefund> PpBefunde(ProgramDocument d, string id) { PpRefresh(d); return _pp.TryGetValue(id, out var c) ? c.Befunde : []; }
    public Pixmap? PpPreviewOf(ProgramDocument d, string id) { PpRefresh(d); return _pp.TryGetValue(id, out var c) ? c.Out : null; }

    /// <summary>Alle Fotos in voller Größe bearbeiten, benennen, kodieren und prüfen.</summary>
    public List<(string Name, byte[] Daten, IReadOnlyList<FotoBefund> Befunde)> PpRender(ProgramDocument d)
    {
        var s = PpOf(d); var e = s.Einstellung; var v = ProduktFotos.Vorgabe(e.Markt);
        if (s.Fotos.Count == 0) throw new InvalidOperationException("Noch keine Fotos – oben „＋ Fotos“.");
        var fmt = e.Hintergrund == "transparent" ? "png" : e.Format; var nums = ProduktFotos.Nummern(s.Fotos); var mark = PpMark(e.Wasserzeichen, v.Breite);
        var list = new List<(string, byte[], IReadOnlyList<FotoBefund>)>();
        for (var i = 0; i < s.Fotos.Count; i++)
        {
            var f = s.Fotos[i];
            if (!File.Exists(f.Datei)) throw new FileNotFoundException("Das Foto gibt es nicht mehr: " + Path.GetFileName(f.Datei));
            var src = PpLoad(f.Datei, 6000);
            var r = ProduktFotos.Bearbeiten(src, e, mark);
            var bytes = PpEncode(r.Bild, fmt);
            var bef = ProduktFotos.Pruefen(r.Bild, v, fmt, bytes.Length, mark is not null, f.Hauptbild).ToList();
            if (r.Vergroessert) bef.Add(new("Auflösung", false, "Das Foto war kleiner als die Zielgröße und wurde vergrößert – wirkt im Zoom weich. Besser mit höherer Auflösung fotografieren.", false));
            list.Add((ProduktFotos.Dateiname(e.Muster, f.Artikelnummer, nums[i], e.Markt, fmt), bytes, bef));
        }
        return list;
    }

    string PpReport(ProgramDocument d, bool full)
    {
        var v = ProduktFotos.Vorgabe(PpOf(d).Einstellung.Markt);
        if (full) return ProduktFotos.BerichtCsv(PpRender(d).Select(x => (x.Name, x.Befunde)), v);
        PpRefresh(d); var s = PpOf(d);
        return ProduktFotos.BerichtCsv(s.Fotos.Select(f => (Path.GetFileName(f.Datei), _pp.TryGetValue(f.Id, out var c) ? c.Befunde : (IReadOnlyList<FotoBefund>)[])), v);
    }

    public string PpPackage(ProgramDocument d, string zip)
    {
        var all = PpRender(d); var v = ProduktFotos.Vorgabe(PpOf(d).Einstellung.Markt);
        ProduktFotos.Paket(zip, all.Select(x => (x.Name, x.Daten)), ProduktFotos.BerichtCsv(all.Select(x => (x.Name, x.Befunde)), v));
        var bad = all.Count(x => !ProduktFotos.Bestanden(x.Befunde));
        _progMsg[PpKey] = $"Paket mit {all.Count} Fotos für {v.Name}: {zip}" + (bad > 0 ? $" – ⚠ {bad} bestehen die Prüfung nicht (siehe pruefbericht.csv)." : " – alle bestehen die Prüfung.");
        _core.Audit.Append(Principal.LocalOwner.Name, "produktfotos.paket", $"{all.Count} Fotos {v.Id}");
        return zip;
    }

    public string PpPackageNow(ProgramDocument d)
    {
        var f = PpPackage(d, ShopFile("Produktfotos", $"{d.Title} {ProduktFotos.Vorgabe(PpOf(d).Einstellung.Markt).Id} {ShopNow:yyyy-MM-dd}", ".zip"));
        NotifyFileResult(_progMsg[PpKey]); return f;
    }

    public string PpSaveFolderNow(ProgramDocument d)
    {
        var all = PpRender(d); var dir = Path.Combine(WorkFolder("Produktfotos"), DevStudio.SafeName($"{d.Title} {ShopNow:yyyy-MM-dd HHmm}"));
        Directory.CreateDirectory(dir);
        foreach (var (name, data, _) in all) { var f = Path.Combine(dir, name); for (var n = 2; File.Exists(f); n++) f = Path.Combine(dir, $"{Path.GetFileNameWithoutExtension(name)}-{n}{Path.GetExtension(name)}"); File.WriteAllBytes(f, data); }
        File.WriteAllText(Path.Combine(dir, "pruefbericht.csv"), ProduktFotos.BerichtCsv(all.Select(x => (x.Name, x.Befunde)), ProduktFotos.Vorgabe(PpOf(d).Einstellung.Markt)), new UTF8Encoding(true));
        _progMsg[PpKey] = $"{all.Count} Fotos gespeichert: {dir}"; NotifyFileResult(_progMsg[PpKey]);
        return dir;
    }

    static IBrush PpAmpel(IReadOnlyList<FotoBefund> b) => b.Count == 0 ? Brush.Parse("#6B7280") : !ProduktFotos.Bestanden(b) ? K4Bad : b.All(x => x.Ok) ? K4Good : K4Warn;

    Control PpWorkspace(ProgramDocument d)
    {
        PpRefresh(d);
        var s = PpOf(d); var e = s.Einstellung; var v = ProduktFotos.Vorgabe(e.Markt); var after = d.Text("view", "nachher") != "vorher";
        var sp = new StackPanel { Spacing = 12, Margin = new Thickness(4, 0, 10, 4) };
        var ok = s.Fotos.Count(f => _pp.TryGetValue(f.Id, out var c) && c.Out is not null && ProduktFotos.Bestanden(c.Befunde));
        sp.Children.Add(new Grid { ColumnDefinitions = new ColumnDefinitions("*,10,*,10,*"), Children = {
            ShopTile("pp-kpi-count", s.Fotos.Count.ToString(CultureInfo.InvariantCulture), "Fotos im Stapel"),
            Pos(ShopTile("pp-kpi-ok", $"{ok} / {s.Fotos.Count}", "bestehen die Prüfung", s.Fotos.Count > 0 && ok == s.Fotos.Count ? K4Good : ok < s.Fotos.Count ? K4Warn : null), 2),
            Pos(ShopTile("pp-kpi-target", $"{v.Breite} × {v.Hoehe}", $"{v.Name} · Stand {ProduktFotos.Stand}"), 4) } });

        var grid = new WrapPanel { Name = "pp-grid" };
        if (s.Fotos.Count == 0)
            grid.Children.Add(new StackPanel { Spacing = 8, Children = { P("Noch keine Fotos. Oben „＋ Fotos …“ – am besten auf einfarbigem Hintergrund fotografiert (Fotokarton, Lichtzelt).", Muted, 12.5),
                KrBtn("pp-add-empty", "＋ Fotos …", () => _ = PpPick(d), true, PpKey) } });
        var n = 0; var sel = d.Text("sel");
        foreach (var f in s.Fotos)
        {
            var id = f.Id; var i = n++; _pp.TryGetValue(id, out var c);
            var img = after ? c?.Out : c?.Src;
            Control pic = img is null ? new Border { Width = 180, Height = 180, Background = Brush.Parse("#22000000"), Child = new TextBlock { Text = c?.Hinweis ?? "…", Foreground = K4Bad, FontSize = 11, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(8) } }
                : new Border { Width = 180, Height = 180, Background = new ImageBrush(ShopBitmap(PpChecker())) { TileMode = TileMode.Tile, DestinationRect = new RelativeRect(0, 0, 16, 16, RelativeUnit.Absolute) },
                    Child = new Image { Source = ShopBitmap(img), Stretch = Stretch.Uniform } };
            var bef = c?.Befunde ?? [];
            var card = new Button
            {
                Name = "pp-card-" + i, Padding = new Thickness(8), Margin = new Thickness(0, 0, 10, 10), Background = id == sel ? Brush.Parse("#33C9A227") : Brush.Parse("#14FFFFFF"),
                Content = new StackPanel { Spacing = 4, Width = 180, Children = { pic,
                    new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = {
                        new Border { Name = $"pp-card-{i}-ampel", Width = 10, Height = 10, CornerRadius = new CornerRadius(5), Background = PpAmpel(bef), VerticalAlignment = VerticalAlignment.Center },
                        new TextBlock { Text = f.Artikelnummer.Length > 0 ? f.Artikelnummer : Path.GetFileName(f.Datei), Foreground = Text, FontSize = 12, FontWeight = FontWeight.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = 160 } } },
                    new TextBlock { Name = $"pp-card-{i}-state", Text = bef.Count == 0 ? "" : ProduktFotos.Bestanden(bef) ? (bef.All(x => x.Ok) ? "✓ erfüllt die Vorgabe" : "✓ Pflicht erfüllt, Empfehlung offen") : "✗ " + bef.First(x => !x.Ok && x.Pflicht).Punkt,
                        Foreground = PpAmpel(bef), FontSize = 11, TextTrimming = TextTrimming.CharacterEllipsis } } },
            };
            card.Click += (_, _) => { d.Data["sel"] = id; Show(PpKey); };
            grid.Children.Add(card);
        }
        sp.Children.Add(KrCard(new StackPanel { Spacing = 8, Children = { new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Children = {
            H(after ? "Nachher" : "Vorher (Original)", 14),
            Pos(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = {
                KrBtn("pp-view-before", "Vorher", () => d.Data["view"] = "vorher", !after, PpKey), KrBtn("pp-view-after", "Nachher", () => d.Data["view"] = "nachher", after, PpKey) } }, 1) } }, grid } }, "pp-grid-card"));

        // Prüfung des gewählten Fotos
        if (s.Fotos.FirstOrDefault(f => f.Id == sel) is { } cur && _pp.TryGetValue(cur.Id, out var cc))
        {
            var chk = new StackPanel { Name = "pp-checks", Spacing = 3, Children = { H("Prüfung: " + Path.GetFileName(cur.Datei), 14), P(cc.Hinweis, cc.Hinweis.StartsWith('◐') ? K4Warn : Muted, 11.5) } };
            foreach (var b in cc.Befunde)
                chk.Children.Add(new TextBlock { Text = (b.Ok ? "✓ " : b.Pflicht ? "✗ " : "◐ ") + b.Punkt + ": " + b.Text, Foreground = b.Ok ? K4Good : b.Pflicht ? K4Bad : K4Warn, FontSize = 12, TextWrapping = TextWrapping.Wrap });
            chk.Children.Add(P("Vorschau-Prüfung; Dateigröße und Auflösung prüft das Paket in voller Größe.", Muted, 10.5));
            sp.Children.Add(KrCard(chk, "pp-checks-card"));
        }
        return sp;
    }

    static Pixmap? _ppChecker;
    static Pixmap PpChecker()
    {
        if (_ppChecker is not null) return _ppChecker;
        var p = new Pixmap(16, 16);
        for (var y = 0; y < 16; y++) for (var x = 0; x < 16; x++) { var i = (y * 16 + x) * 4; var c = (byte)(((x / 8 + y / 8) % 2) == 0 ? 0xCC : 0xF2); p.Px[i] = p.Px[i + 1] = p.Px[i + 2] = c; p.Px[i + 3] = 255; }
        return _ppChecker = p;
    }

    Control PpProperties(ProgramDocument d)
    {
        var s = PpOf(d); var e = s.Einstellung; var v = ProduktFotos.Vorgabe(e.Markt);
        var sp = new StackPanel { Name = "pp-props", Spacing = 6 };
        void Set(string label, Action<FotoEinstellung> a) => PpChange(d, label, x => a(x.Einstellung));

        sp.Children.Add(H("Marktplatz", 13));
        sp.Children.Add(ShopCombo(PpKey, "pp-markt", ProduktFotos.Vorgaben.Select(x => (x.Id, x.Name)), e.Markt, t => Set("Marktplatz", x => { x.Markt = t; if (ProduktFotos.Vorgabe(t).WeissPflicht) x.Hintergrund = "#FFFFFF"; }), 220));
        sp.Children.Add(P($"{v.Hinweis} Stand {ProduktFotos.Stand} · {v.Quelle}", Muted, 10.5));

        sp.Children.Add(H("Hintergrund", 13));
        var free = new CheckBox { Name = "pp-free", Content = "Hintergrund entfernen (Freistellen)", IsChecked = e.Freistellen, Foreground = Text, FontSize = 12 };
        free.IsCheckedChanged += (_, _) => { if ((free.IsChecked == true) != e.Freistellen) ProgramRun(PpKey, () => Set("Freistellen", x => x.Freistellen = free.IsChecked == true)); };
        sp.Children.Add(free);
        var tol = new Slider { Name = "pp-tol", Minimum = 0, Maximum = 120, Value = e.Toleranz, Width = 200 };
        tol.PointerCaptureLost += (_, _) => ProgramRun(PpKey, () => Set("Toleranz", x => x.Toleranz = (int)Math.Round(tol.Value)));
        sp.Children.Add(ShopLabeled($"Toleranz ({e.Toleranz}) – höher entfernt mehr, zu hoch frisst Kanten", tol));
        var colors = new WrapPanel { Name = "pp-bg-colors" };
        foreach (var (hex, tip) in new[] { ("#FFFFFF", "Reinweiß"), ("#F4EFE6", "Creme"), ("#E8EEF4", "Hellblau"), ("#EDE7F6", "Flieder"), ("#E8F5E9", "Mint"), ("#1F2937", "Dunkel"), ("transparent", "Ohne (PNG)") })
        {
            var h = hex; var b = new Button { Name = "pp-bg-" + hex.TrimStart('#').ToLowerInvariant(), Width = 26, Height = 26, Padding = new Thickness(0), Margin = new Thickness(0, 0, 4, 4), CornerRadius = new CornerRadius(4),
                Background = hex == "transparent" ? new ImageBrush(ShopBitmap(PpChecker())) { TileMode = TileMode.Tile, DestinationRect = new RelativeRect(0, 0, 8, 8, RelativeUnit.Absolute) } : Brush.Parse(hex),
                BorderBrush = e.Hintergrund.Equals(hex, StringComparison.OrdinalIgnoreCase) ? Accent : Edge, BorderThickness = new Thickness(e.Hintergrund.Equals(hex, StringComparison.OrdinalIgnoreCase) ? 3 : 1) };
            ToolTip.SetTip(b, tip); b.Click += (_, _) => ProgramRun(PpKey, () => PpSetBackground(d, h)); colors.Children.Add(b);
        }
        sp.Children.Add(colors);
        sp.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = {
            ShopBox("pp-bg-hex", e.Hintergrund, "#RRGGBB", t => ProgramRun(PpKey, () => PpSetBackground(d, t.Trim())), false, 100) } });
        if (v.WeissPflicht && !e.Hintergrund.Equals("#FFFFFF", StringComparison.OrdinalIgnoreCase)) sp.Children.Add(P("⚠ " + v.Name + " verlangt für das Hauptbild Reinweiß.", K4Warn, 11));
        sp.Children.Add(ShopLabeled("Schatten", ShopCombo(PpKey, "pp-shadow", [("aus", "Ohne"), ("weich", "Weicher Kontaktschatten"), ("spiegel", "Spiegelung am Boden")], e.Schatten, t => Set("Schatten", x => x.Schatten = t), 200)));

        sp.Children.Add(H("Ausrichten und Licht", 13));
        sp.Children.Add(ShopLabeled("Rand rundum (%)", ShopBox("pp-margin", e.RandProzent.ToString("0.#", CultureInfo.InvariantCulture), "z. B. 5", t => { if (double.TryParse(t.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var r) && r is >= 0 and <= 30) Set("Rand", x => x.RandProzent = r); }, false, 70)));
        foreach (var (name, label, val, set) in new (string, string, bool, Action<FotoEinstellung, bool>)[] { ("pp-light", "Helligkeit automatisch", e.AutoLicht, (x, b) => x.AutoLicht = b), ("pp-wb", "Weißabgleich automatisch", e.Weissabgleich, (x, b) => x.Weissabgleich = b) })
        {
            var cb = new CheckBox { Name = name, Content = label, IsChecked = val, Foreground = Text, FontSize = 12 }; var st = set; var old = val;
            cb.IsCheckedChanged += (_, _) => { if ((cb.IsChecked == true) != old) ProgramRun(PpKey, () => Set(label, x => st(x, cb.IsChecked == true))); };
            sp.Children.Add(cb);
        }

        sp.Children.Add(H("Wasserzeichen", 13));
        sp.Children.Add(ShopBox("pp-wm", e.Wasserzeichen, "z. B. © Mein Shop – leer = keins", t => Set("Wasserzeichen", x => x.Wasserzeichen = t.Trim())));
        sp.Children.Add(ShopCombo(PpKey, "pp-wm-pos", [("unten-rechts", "unten rechts"), ("unten-links", "unten links"), ("oben-rechts", "oben rechts"), ("oben-links", "oben links"), ("mitte", "Mitte")], e.WasserzeichenOrt, t => Set("Ort", x => x.WasserzeichenOrt = t), 160));
        if (!v.WasserzeichenErlaubt && e.Wasserzeichen.Length > 0) sp.Children.Add(P($"⚠ {v.Name} erlaubt kein Wasserzeichen im Bild.", K4Bad, 11));

        sp.Children.Add(H("Dateien", 13));
        sp.Children.Add(ShopLabeled("Dateiname ({nr} Artikelnummer, {n} Nummer, {variante} MAIN/PT01, {markt}, {datum})", ShopBox("pp-pattern", e.Muster, "{nr}_{n}", t => Set("Dateiname", x => x.Muster = t.Trim()))));
        sp.Children.Add(ShopLabeled("Format", ShopCombo(PpKey, "pp-format", [("jpg", "JPEG (klein, Standard)"), ("png", "PNG (verlustfrei)")], e.Format, t => Set("Format", x => x.Format = t), 180)));
        var nums = ProduktFotos.Nummern(s.Fotos);
        if (s.Fotos.FirstOrDefault(f => f.Id == d.Text("sel")) is { } cur)
        {
            var idx = s.Fotos.IndexOf(cur); var cid = cur.Id;
            sp.Children.Add(H("Gewähltes Foto", 13));
            sp.Children.Add(ShopLabeled("Artikelnummer (SKU, ASIN …)", ShopBox("pp-sku", cur.Artikelnummer, "z. B. TASSE-001", t => PpChange(d, "Artikelnummer", x => { if (x.Fotos.FirstOrDefault(y => y.Id == cid) is { } y) y.Artikelnummer = t.Trim(); }))));
            sp.Children.Add(P("→ " + ProduktFotos.Dateiname(e.Muster, cur.Artikelnummer, nums[idx], e.Markt, e.Hintergrund == "transparent" ? "png" : e.Format), Muted, 11));
            var main = new CheckBox { Name = "pp-main", Content = "Hauptbild (strenge Regeln, z. B. Amazon)", IsChecked = cur.Hauptbild, Foreground = Text, FontSize = 12 };
            main.IsCheckedChanged += (_, _) => { if ((main.IsChecked == true) != cur.Hauptbild) ProgramRun(PpKey, () => PpChange(d, "Hauptbild", x => { if (x.Fotos.FirstOrDefault(y => y.Id == cid) is { } y) y.Hauptbild = main.IsChecked == true; })); };
            sp.Children.Add(main);
        }
        sp.Children.Add(KrBtn("pp-package-btn", "Upload-Paket (ZIP)", () => PpPackageNow(d), true, PpKey));
        sp.Children.Add(P("Trendo lädt nichts hoch. Für unruhige Hintergründe oben „kie.ai“ → Hintergrund entfernen (eigenes Konto, kostenpflichtig) und das Ergebnis hier hinzufügen.", Muted, 10.5));
        return sp;
    }

    public void PpSetBackground(ProgramDocument d, string hex)
    {
        if (hex != "transparent" && !ProduktFotos.IsHex(hex)) throw new FormatException($"„{hex}“ ist keine Farbe – bitte z. B. #FFFFFF.");
        PpChange(d, "Hintergrund", s => { s.Einstellung.Hintergrund = hex == "transparent" ? hex : hex.ToUpperInvariant(); if (hex == "transparent") s.Einstellung.Format = "png"; });
    }
}
