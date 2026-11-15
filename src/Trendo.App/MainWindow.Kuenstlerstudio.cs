using System.Diagnostics;
using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Trendo.Core;
using Trendo.Core.Kreativ;
using Trendo.Core.Programs;

namespace Trendo.App;

/// <summary>STUDIO-ART-01 Künstlerstudio (K1–K8, Freigabe 15.11.2026): Zeichnen und Malen wie in einem Profi-Malprogramm – Clean-Room nach dem
/// Verhalten von Krita/Procreate. Anforderungen und Prüfungen: docs/nachbau/kuenstlerstudio/anforderungen.md.</summary>
public partial class MainWindow
{
    public const string ArtKey = "kuenstler-programm";
    const string ArtStudio = "STUDIO-ART-01";
    public string ArtBrush { get; set; } = "bleistift";
    public string ArtColor { get; set; } = "#1F2937";
    public double ArtSize { get; set; } = 6;
    public double ArtOpacity { get; set; } = 1;
    public double ArtSmooth { get; set; } = 0.3;
    /// <summary>Werkzeug neben den Pinseln: Pipette oder Hilfspunkte setzen.</summary>
    public string ArtMode { get; set; } = "paint";
    public double ArtZoom { get; set; }
    public bool ArtGrid { get; set; }
    public List<string> ArtRecent { get; } = [];
    public PaintCanvas? ArtCanvasForTests { get; private set; }
    /// <summary>Tests: ersetzt FFmpeg beim Zeitraffer-Video (Muster, Bilder/s, Ziel → erfolgreich?).</summary>
    public static Func<string, int, string, bool>? ArtVideoOverride { get; set; }
    readonly Dictionary<string, ArtState> _art = [];
    bool _artAiBusy;

    static readonly string[] ArtSwatches = ["#000000", "#FFFFFF", "#1F2937", "#7C2D12", "#B91C1C", "#EA580C", "#F59E0B", "#FDE68A", "#15803D", "#0EA5E9", "#1D4ED8", "#7C3AED", "#DB2777", "#A8A29E"];

    static JsonObject ArtNew(int w, int h, string bg = "#FFFFFF", string sym = "aus", string first = "Skizze")
    {
        var j = PaintDoc.NewJson(w, h, bg, first);
        j["sym"] = new JsonObject { ["mode"] = sym, ["parts"] = 8 }; j["assist"] = new JsonObject { ["mode"] = "aus", ["pts"] = new JsonArray() };
        j["palette"] = new JsonArray(); j["refs"] = new JsonArray(); j["strokes"] = 0; j["tl"] = Guid.NewGuid().ToString("N"); j["timelapse"] = true;
        return j;
    }

    private void InitKuenstlerstudio() => RegisterProgram(KreativTabs, new ProgramSpec(ArtKey, ArtStudio, "Künstlerstudio")
    {
        Sentence = "Zeichnen und Malen wie auf Papier – echte Pinsel mit Stiftdruck, Ebenen, Farbrad, Spiegeln und Perspektive.",
        FillHeight = true,
        Folder = "Kunst",
        GroupLevels = new() { ["Ebene"] = 0, ["Bild"] = 0, ["Teilen"] = 1, ["Zeitraffer"] = 1, ["Ansicht"] = 0 },
        Templates =
        [
            new("skizze", "Skizzenblatt A4", "1240 × 1754, hochkant", () => ArtNew(1240, 1754)),
            new("quer", "Querformat 16:9", "1920 × 1080 – für Bildschirm und Video", () => ArtNew(1920, 1080)),
            new("quadrat", "Quadrat", "1600 × 1600 – für soziale Netzwerke", () => ArtNew(1600, 1600)),
            new("aquarell", "Aquarellpapier", "1600 × 1200, warmes Papier", () => ArtNew(1600, 1200, "#F6F1E7")),
            new("comic", "Comicseite", "1200 × 1700 – Skizze, Linien, Farbe", () => ArtNew(1200, 1700)),
            new("mandala", "Mandala", "1600 × 1600 mit Spiegeln (8 Teile)", () => ArtNew(1600, 1600, "#FFFFFF", "radial")),
        ],
        Ribbon =
        [
            ("Ebene", [
                new ProgramCmd("art-layer-add", "＋ Ebene", d => ArtEdit(d, "Neue Ebene", p => { var l = p.Add(null, p.Layers.IndexOf(ArtActive(d, p)) + 1); d.Data["active"] = l.Id; }), Primary: true),
                new ProgramCmd("art-layer-dup", "Kopieren", d => ArtEdit(d, "Ebene kopieren", p => d.Data["active"] = p.Duplicate(ArtActive(d, p).Id).Id)),
                new ProgramCmd("art-layer-up", "▲", d => ArtEdit(d, "Nach oben", p => p.Move(ArtActive(d, p).Id, 1)), Tip: "Ebene nach oben"),
                new ProgramCmd("art-layer-down", "▼", d => ArtEdit(d, "Nach unten", p => p.Move(ArtActive(d, p).Id, -1)), Tip: "Ebene nach unten"),
                new ProgramCmd("art-layer-merge", "Nach unten verbinden", d => ArtEdit(d, "Verbinden", p => { var l = ArtActive(d, p); var i = p.Layers.IndexOf(l); p.MergeDown(l.Id); d.Data["active"] = p.Layers[Math.Max(0, i - 1)].Id; }), Level: 1),
                new ProgramCmd("art-layer-clear", "Leeren", d => ArtEdit(d, "Ebene leeren", p => { var l = ArtActive(d, p); ArtCheckLock(l); l.Px = new Pixmap(p.W, p.H); }), Level: 1),
                new ProgramCmd("art-layer-del", "Löschen", d => ArtEdit(d, "Ebene löschen", p => { var l = ArtActive(d, p); var i = p.Layers.IndexOf(l); p.Remove(l.Id); d.Data["active"] = p.Layers[Math.Clamp(i - 1, 0, p.Layers.Count - 1)].Id; })),
            ]),
            ("Bild", [
                new ProgramCmd("art-open", "Öffnen …", d => _ = ArtOpenPick(), Tip: "ORA, PNG oder JPG öffnen"),
                new ProgramCmd("art-clean", "Skizze säubern", d => ArtCleanSketch(d), Tip: "Linien der gewählten Ebene auf eine neue, saubere Ebene – die Skizze bleibt."),
                new ProgramCmd("art-flats", "Vorfärben", d => ArtFlats(d), Tip: "Geschlossene Flächen der Linien bekommen je eine Farbe – auf eigener Ebene."),
                new ProgramCmd("art-puppet", "Posenpuppe", d => ArtPuppet(d, d.Text("pose", "stehen"), d.Number("poseYaw")), Tip: "Gliederpuppe als Skizzenebene – Pose rechts wählen."),
            ]),
            ("Zeitraffer", [
                new ProgramCmd("art-tl-video", "Video erstellen", d => _ = ArtTimelapseVideo(d, false), Tip: "Jeder Strich ein Bild – als MP4."),
                new ProgramCmd("art-tl-cut", "In den Videoschnitt", d => _ = ArtTimelapseVideo(d, true)),
            ]),
            ("Teilen", [
                new ProgramCmd("art-portfolio", "Portfolio-Webseite", d => ArtPortfolio(d), Tip: "Alle offenen Bilder als Galerie-Webseite."),
                new ProgramCmd("art-pod", "Auf Produkte (Print on Demand)", d => ArtToPod(d)),
            ]),
            ("Ansicht", [
                new ProgramCmd("art-zoom-fit", "Einpassen", _ => { ArtZoom = 0; ArtPan = default; }),
                new ProgramCmd("art-zoom-100", "100 %", _ => ArtZoom = 1),
                new ProgramCmd("art-grid", "Raster", _ => ArtGrid = !ArtGrid),
            ]),
        ],
        Exports =
        [
            new("png", "PNG", ".png", (d, f) => { File.WriteAllBytes(f, PngCodec.Encode(ArtModel(d).Doc.Composite(), fast: false)); return f; }),
            new("ora", "OpenRaster mit Ebenen (.ora)", ".ora", (d, f) => { PaintFiles.SaveOra(ArtModel(d).Doc, f); return f; }),
            new("psd", "Photoshop mit Ebenen (.psd)", ".psd", (d, f) => { File.WriteAllBytes(f, PaintFiles.Psd(ArtModel(d).Doc)); return f; }),
            new("png-transparent", "PNG ohne Hintergrund", ".png", (d, f) => { File.WriteAllBytes(f, PngCodec.Encode(ArtModel(d).Doc.Composite(false), fast: false)); return f; }),
            new("druck-a4", "Druckdatei A4 (300 dpi, 3 mm Beschnitt)", ".png", (d, f) => ArtPrint(d, f, "a4")),
            new("druck-a3", "Druckdatei A3 (300 dpi, 3 mm Beschnitt)", ".png", (d, f) => ArtPrint(d, f, "a3")),
            new("druck-postkarte", "Druckdatei Postkarte (300 dpi)", ".png", (d, f) => ArtPrint(d, f, "postkarte")),
            new("palette", "Palette (.gpl)", ".gpl", (d, f) => { File.WriteAllText(f, Palettes.ToGpl(d.Title, ArtPalette(d))); return f; }),
        ],
        SimpleExports = 2,
        Tools = [.. BrushPreset.All.Select(b => new ProgramTool("art-brush-" + b.Id, b.Icon, b.Name, ArtBrushTip(b), _ => ArtPickBrush(b.Id), () => ArtMode == "paint" && ArtBrush == b.Id)),
                 new ProgramTool("art-tool-pick", "◉", "Pipette", "Farbe aus dem Bild aufnehmen", _ => ArtMode = "pick", () => ArtMode == "pick")],
        Workspace = ArtWorkspace,
        Properties = ArtProperties,
        Status = d => { var m = ArtModel(d); var b = BrushPreset.Get(ArtBrush); return $"{m.Doc.W} × {m.Doc.H} px · {m.Doc.Layers.Count} Ebenen · {b.Name} {ArtSize:0} px · {(int)d.Number("strokes")} Striche" + (ArtSym(d).Mode != "aus" ? " · Spiegeln an" : ""); },
        Help = "Links einen Pinsel wählen und malen – mit Grafiktablett zählt der Stiftdruck. Rechts Farbe (Farbrad, Palette, Harmonien), Ebenen mit Mischarten, Spiegeln und Perspektive. "
             + "Strg+Mausrad zoomt, mittlere Maustaste oder Leertaste verschiebt. Speichern als Projekt; Export als PNG, ORA oder PSD mit Ebenen, als Druckdatei oder Zeitraffer-Video.",
    }, "künstlerstudio", "zeichnen", "malen", "malprogramm", "pinsel", "skizze", "illustration", "aquarell", "comic", "mandala", "grafiktablett", "krita", "procreate");

    public Vector ArtPan { get; set; }

    static string ArtBrushTip(BrushPreset b) => b.Id switch
    {
        "bleistift" => "Leicht körnig, Druck = dünner/heller", "tusche" => "Saubere Linien, Druck = Strichstärke", "pinsel" => "Deckende Farbe, baut sich auf",
        "airbrush" => "Weich, wird beim Verweilen dichter", "marker" => "Halbdurchsichtig, gleichmäßig – mehrfaches Überfahren dunkelt nicht nach",
        "aquarell" => "Weich und lasierend", "kreide" => "Raue Kante", "radierer" => "Entfernt Farbe auf der gewählten Ebene", _ => b.Name,
    };

    void ArtPickBrush(string id)
    {
        var b = BrushPreset.Get(id); ArtMode = "paint";
        if (ArtBrush != id) { ArtBrush = id; ArtSize = b.Size; ArtOpacity = b.Opacity; }
    }

    // ---------- Modell ----------

    /// <summary>Bild aus dem Dokument entpacken – Ebenen, deren PNG sich nicht geändert hat, werden nicht neu gelesen.</summary>
    public ArtState ArtModel(ProgramDocument d)
    {
        if (!_art.TryGetValue(d.Id, out var st)) _art[d.Id] = st = new ArtState();
        var j = d.Data; var layers = (j["layers"] as JsonArray)?.OfType<JsonObject>().ToList() ?? [];
        int w = (int)d.Number("width", 1600), h = (int)d.Number("height", 1200);
        var sig = $"{w}x{h}|{d.Text("background")}|" + string.Join(";", layers.Select(o => $"{o["id"].AsText()}:{o["opacity"]?.ToJsonString()}:{o["visible"]?.ToJsonString()}:{o["blend"].AsText()}:{o["name"].AsText()}:{o["locked"]?.ToJsonString()}:{o["alphaLock"]?.ToJsonString()}"));
        var changed = sig != st.Sig || st.Doc.W != w || st.Doc.H != h;
        var doc = new PaintDoc(w, h, d.Text("background", "#FFFFFF"));
        foreach (var o in layers)
        {
            var id = o["id"].AsText() ?? ""; var png = o["png"].AsText() ?? "";
            Pixmap px;
            if (st.Cache.TryGetValue(id, out var c) && string.Equals(c.Png, png, StringComparison.Ordinal) && c.Px.W == w && c.Px.H == h) px = c.Px;
            else
            {
                px = png.Length == 0 ? new Pixmap(w, h) : PngCodec.Decode(Convert.FromBase64String(png));
                if (px.W != w || px.H != h) px = px.Resize(w, h);
                st.Cache[id] = (png, px); changed = true;
            }
            doc.Layers.Add(new PaintLayer(id, o["name"].AsText() ?? "Ebene", px)
            { Opacity = ProgramDocument.ToNumber(o["opacity"], 1), Visible = o["visible"].AsBool(true), Locked = o["locked"].AsBool(), AlphaLock = o["alphaLock"].AsBool(), Blend = PaintBlend.Norm(o["blend"].AsText()) });
        }
        if (doc.Layers.Count == 0) { var l = doc.Add("Ebene 1"); st.Cache[l.Id] = ("", l.Px); changed = true; }
        foreach (var gone in st.Cache.Keys.Except(doc.Layers.Select(l => l.Id)).ToList()) st.Cache.Remove(gone);
        st.Doc = doc;
        if (changed || st.Bmp is null) { st.Sig = sig; if (st.Comp.W != w || st.Comp.H != h) st.Comp = new Pixmap(w, h); st.Refresh(); }
        return st;
    }

    PaintLayer ArtActive(ProgramDocument d, PaintDoc p) => p.Layers.FirstOrDefault(l => l.Id == d.Text("active")) ?? p.Layers[^1];
    static void ArtCheckLock(PaintLayer l) { if (l.Locked) throw new InvalidOperationException($"Die Ebene „{l.Name}“ ist gesperrt – erst rechts das Schloss lösen."); }

    /// <summary>Änderung an Ebenen als ein Rückgängig-Schritt. Unveränderte Ebenen behalten ihr PNG (kein Neu-Kodieren).</summary>
    void ArtEdit(ProgramDocument d, string label, Action<PaintDoc> change)
    {
        var st = ArtModel(d); change(st.Doc);
        var layers = new JsonArray();
        foreach (var l in st.Doc.Layers)
        {
            var png = st.Cache.TryGetValue(l.Id, out var c) && ReferenceEquals(c.Px, l.Px) ? c.Png : Convert.ToBase64String(PngCodec.Encode(l.Px));
            st.Cache[l.Id] = (png, l.Px);
            layers.Add(new JsonObject { ["id"] = l.Id, ["name"] = l.Name, ["opacity"] = Math.Round(l.Opacity, 3), ["visible"] = l.Visible, ["locked"] = l.Locked, ["alphaLock"] = l.AlphaLock, ["blend"] = l.Blend, ["png"] = png });
        }
        var active = d.Text("active");
        d.Edit(label, x => { x["layers"] = layers; x["background"] = st.Doc.Background; x["active"] = active; });
    }

    void ArtLayerProp(ProgramDocument d, string id, string label, Action<JsonObject> set) =>
        d.Edit(label, x => set((x["layers"] as JsonArray)?.OfType<JsonObject>().FirstOrDefault(o => o["id"].AsText() == id) ?? throw new InvalidOperationException("Diese Ebene gibt es nicht mehr.")));

    static PaintSymmetry ArtSym(ProgramDocument d)
    {
        var s = d.Data["sym"] as JsonObject; var w = d.Number("width", 1600); var h = d.Number("height", 1200);
        return new(s?["mode"].AsText() ?? "aus", w / 2, h / 2, (int)ProgramDocument.ToNumber(s?["parts"], 8));
    }
    static DrawAssist ArtAssist(ProgramDocument d)
    {
        var a = d.Data["assist"] as JsonObject;
        var pts = (a?["pts"] as JsonArray)?.OfType<JsonArray>().Select(p => (ProgramDocument.ToNumber(p[0]), ProgramDocument.ToNumber(p[1]))).ToList() ?? [];
        return new(a?["mode"].AsText() ?? "aus", pts);
    }
    static int ArtNeedPoints(string mode) => mode switch { "lineal" => 2, "perspektive1" => 1, "perspektive2" => 2, "perspektive3" => 3, _ => 0 };
    static List<string> ArtPalette(ProgramDocument d) => [.. d.List("palette").Select(n => n.AsText()).OfType<string>().Where(PaintColors.IsHex)];

    void ArtUseColor(string hex)
    {
        if (!PaintColors.IsHex(hex)) throw new FormatException($"„{hex}“ ist keine Farbe. Bitte z. B. #E0475B eingeben.");
        var (r, g, b) = PaintColors.Rgb(hex); ArtColor = PaintColors.Hex(r, g, b);
        ArtRecent.Remove(ArtColor); ArtRecent.Insert(0, ArtColor); if (ArtRecent.Count > 12) ArtRecent.RemoveAt(12);
    }

    // ---------- Malen (öffentlich für Tests und die KI) ----------

    StrokeEngine ArtBegin(ProgramDocument d)
    {
        var st = ArtModel(d); var l = ArtActive(d, st.Doc); ArtCheckLock(l);
        if (!l.Visible) throw new InvalidOperationException($"Die Ebene „{l.Name}“ ist ausgeblendet – erst einblenden, dann malen.");
        var b = BrushPreset.Get(ArtBrush);
        return new StrokeEngine(l.Px, b, ArtColor, ArtSize, ArtOpacity, ArtSym(d), ArtAssist(d), ArtSmooth, l.AlphaLock);
    }

    void ArtEnd(ProgramDocument d, StrokeEngine s)
    {
        if (s.Dabs == 0) return;
        var st = ArtModel(d); var l = ArtActive(d, st.Doc);
        if (!ReferenceEquals(l.Px, s.Layer)) return; // Ebene inzwischen gewechselt
        var png = Convert.ToBase64String(PngCodec.Encode(l.Px));
        st.Cache[l.Id] = (png, l.Px);
        var b = BrushPreset.Get(ArtBrush);
        d.Edit(b.Erase ? "Radieren" : b.Name, x =>
        {
            var o = (x["layers"] as JsonArray)?.OfType<JsonObject>().FirstOrDefault(y => y["id"].AsText() == l.Id); if (o is not null) o["png"] = png;
            x["strokes"] = (int)ProgramDocument.ToNumber(x["strokes"]) + 1;
        });
        if (d.Data["timelapse"].AsBool(true)) try { Timelapse.Record(ArtTlDir(d), st.Comp); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        if (!b.Erase) ArtUseColor(ArtColor);
    }

    /// <summary>Strich aus Punkten (x, y, Druck) – wie mit dem Stift auf der Fläche.</summary>
    public void ArtPaint(ProgramDocument d, IEnumerable<(double X, double Y, double P)> pts)
    {
        var s = ArtBegin(d); foreach (var (x, y, p) in pts) s.Add(x, y, p);
        var st = ArtModel(d); if (s.TakeDirty() is { } r) st.Refresh(r.X0, r.Y0, r.X1, r.Y1);
        ArtEnd(d, s);
    }

    /// <summary>Farbe an einer Stelle des Gesamtbilds aufnehmen (Pipette).</summary>
    public string ArtPick(ProgramDocument d, double x, double y)
    {
        var st = ArtModel(d); int px = (int)Math.Clamp(x, 0, st.Doc.W - 1), py = (int)Math.Clamp(y, 0, st.Doc.H - 1);
        var (r, g, b, _) = st.Comp.At(px, py); var hex = PaintColors.Hex(r, g, b); ArtUseColor(hex); return hex;
    }

    string ArtDir(params string[] parts) { var p = Path.Combine([_core.DataRoot, "kuenstler", .. parts]); Directory.CreateDirectory(p); return p; }
    string ArtTlDir(ProgramDocument d)
    {
        var id = d.Text("tl"); if (!System.Text.RegularExpressions.Regex.IsMatch(id, "^[0-9a-f]{32}$")) { id = Guid.NewGuid().ToString("N"); d.Data["tl"] = id; }
        return Path.Combine(_core.DataRoot, "kuenstler", "zeitraffer", id);
    }

    // ---------- Öffnen ----------

    async Task ArtOpenPick()
    {
        var f = await PickMediaFile("art-open", "Bild öffnen", ["*.ora", "*.png", "*.jpg", "*.jpeg", "*.webp"]);
        if (f is not null) ProgramRun(ArtKey, () => ArtOpen(f));
    }

    /// <summary>ORA mit Ebenen oder ein Foto/Bild als unterste Ebene öffnen.</summary>
    public ProgramDocument ArtOpen(string file)
    {
        if (!File.Exists(file)) throw new FileNotFoundException("Datei nicht gefunden: " + Path.GetFileName(file));
        PaintDoc p;
        if (Path.GetExtension(file).Equals(".ora", StringComparison.OrdinalIgnoreCase)) p = PaintFiles.LoadOra(file);
        else
        {
            var img = ArtDecode(file); p = new PaintDoc(img.W, img.H); var bl = p.Add("Bild"); bl.Px = img; p.Add("Malen");
        }
        if ((long)p.W * p.H > 40_000_000) throw new InvalidOperationException("Das Bild ist zu groß (mehr als 40 Millionen Bildpunkte).");
        var j = ArtNew(p.W, p.H, p.Background); p.ToJson(j); j["active"] = p.Layers[^1].Id;
        var d = ProgramNew(ArtKey); d.Title = Path.GetFileNameWithoutExtension(file);
        d.Edit("Öffnen", x => { foreach (var kv in j.ToList()) x[kv.Key] = kv.Value?.DeepClone(); });
        _progMsg[ArtKey] = $"„{Path.GetFileName(file)}“ geöffnet: {p.Layers.Count} Ebenen, {p.W} × {p.H}.";
        return d;
    }

    static Pixmap ArtDecode(string file)
    {
        using var src = SkiaSharp.SKBitmap.Decode(file) ?? throw new InvalidDataException("Das ist kein Bild, das Trendo öffnen kann.");
        using var bmp = src.Copy(SkiaSharp.SKColorType.Rgba8888) ?? throw new InvalidDataException("Das Bild ließ sich nicht umwandeln.");
        var px = new Pixmap(bmp.Width, bmp.Height);
        for (var y = 0; y < bmp.Height; y++)
            for (var x = 0; x < bmp.Width; x++) { var c = bmp.GetPixel(x, y); var i = (y * bmp.Width + x) * 4; px.Px[i] = c.Red; px.Px[i + 1] = c.Green; px.Px[i + 2] = c.Blue; px.Px[i + 3] = c.Alpha; }
        return px;
    }

    // ---------- K4 Referenz, Posenpuppe, Ideenskizze ----------

    public void ArtAddReference(ProgramDocument d, string file)
    {
        if (!File.Exists(file)) throw new FileNotFoundException("Datei nicht gefunden: " + Path.GetFileName(file));
        _ = ArtDecode(file); // prüft, ob es ein Bild ist
        d.Edit("Referenz", x => { if (x["refs"] is not JsonArray a) x["refs"] = a = []; a.Add(file); });
        _progMsg[ArtKey] = "Referenz rechts – zum Farbe aufnehmen auf das Bild klicken. Sie wird nicht mitgemalt.";
    }

    public List<string> ArtColorsFromReference(ProgramDocument d, int k = 6)
    {
        var f = d.List("refs").Select(n => n.AsText()).OfType<string>().LastOrDefault(File.Exists) ?? throw new InvalidOperationException("Erst rechts unter „Referenz“ ein Foto laden.");
        var img = ArtDecode(f); var s = Math.Min(1.0, 400.0 / Math.Max(img.W, img.H));
        var cols = Palettes.FromPhoto(s < 1 ? img.Resize(Math.Max(1, (int)(img.W * s)), Math.Max(1, (int)(img.H * s))) : img, k);
        d.Set("palette", new JsonArray([.. cols.Select(c => (JsonNode)c)]), "Farben aus Referenz");
        _progMsg[ArtKey] = $"{cols.Count} Hauptfarben in die Palette übernommen.";
        return cols;
    }

    public void ArtPuppet(ProgramDocument d, string pose, double yaw)
    {
        var pz = Mannequin.Get(pose);
        ArtEdit(d, "Posenpuppe", p =>
        {
            var l = p.Add($"Posenpuppe ({pz.Name})", 0); l.Px = Mannequin.Render(p.W, p.H, pz, yaw); l.Opacity = 0.7; l.Locked = true;
            var above = p.Layers.Skip(1).FirstOrDefault(x => !x.Locked) ?? p.Add("Zeichnung");
            d.Data["active"] = above.Id;
        });
        _progMsg[ArtKey] = $"Posenpuppe „{pz.Name}“ liegt gesperrt ganz unten – darüber zeichnen. Später ausblenden oder löschen.";
    }

    async Task ArtIdeaSketch(ProgramDocument d, string prompt)
    {
        if (_artAiBusy) { _progMsg[ArtKey] = "Die Ideenskizze wird schon gemalt – einen Moment."; Show(ArtKey); return; }
        if (string.IsNullOrWhiteSpace(prompt)) { _progMsg[ArtKey] = "Beschreibe kurz, was die Skizze zeigen soll."; Show(ArtKey); return; }
        if (ImageAiKit.Blocked(prompt) is { } why) { _progMsg[ArtKey] = why; Show(ArtKey); return; }
        var cm = ImageAiOverride is null ? CoverModel() : null;
        if (ImageAiOverride is null && (cm is null || _opt.Isolated)) { _progMsg[ArtKey] = "Die lokale Bild-KI ist noch nicht eingerichtet – unter „Bilder mit KI“ einrichten (kostenlos, läuft auf deinem PC)."; Show(ArtKey); return; }
        _artAiBusy = true;
        try
        {
            var dir = ArtDir("ideen"); var seed = Random.Shared.Next(1, 1_000_000);
            var png = Path.Combine(dir, $"idee-{DateTime.Now:yyyyMMdd-HHmmss}-{seed}.png");
            var full = prompt.Trim() + ", rough pencil concept sketch, loose lines, monochrome";
            _progMsg[ArtKey] = "Die lokale Bild-KI skizziert eine Idee …"; Show(ArtKey);
            bool ok;
            using (await GpuSlot("bild", "Ideenskizze"))
            {
                if (ImageAiOverride is { } o) ok = await o(full, seed, png, 512, 512);
                else
                {
                    var xl = cm!.Value.Model.Id != "sd-turbo"; var size = xl ? 1024 : 512;
                    var args = ImageAiKit.Args(KiStore.ModelFile(cm.Value.Model, cm.Value.Model.Files[0]), full, "", "", png, size, size, seed, xl);
                    var r = await Task.Run(() => RunWithInput(cm.Value.Exe, args, null)); ok = r.Ok;
                }
            }
            if (!ok || !File.Exists(png)) { _progMsg[ArtKey] = "Die Bild-KI hat keine Skizze geliefert."; return; }
            ImageAiKit.WriteProvenance(png, new("stable-diffusion.cpp", cm?.Model.Id ?? "test", full, "", "", seed, 512, 512, DateTime.UtcNow, ""));
            d.Edit("Ideenskizze", x => { if (x["refs"] is not JsonArray a) x["refs"] = a = []; a.Add(png); });
            _core.Audit.Append(Principal.LocalOwner.Name, "kuenstler.ideenskizze", "lokal");
            _progMsg[ArtKey] = "Ideenskizze liegt rechts als Referenz – nur als Anregung, gemalt wird von dir.";
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or UnauthorizedAccessException or System.ComponentModel.Win32Exception) { _progMsg[ArtKey] = FriendlyError.Say(ex); }
        finally { _artAiBusy = false; Show(ArtKey); }
    }

    // ---------- K6 Skizze säubern, Vorfärben ----------

    public void ArtCleanSketch(ProgramDocument d, double threshold = 0.35)
    {
        var src = ArtActive(d, ArtModel(d).Doc);
        if (src.Px.IsEmpty()) throw new InvalidOperationException($"Auf der Ebene „{src.Name}“ ist noch nichts gezeichnet.");
        ArtEdit(d, "Skizze säubern", p =>
        {
            var s = p.Get(src.Id); var lines = p.Add("Linien", p.Layers.IndexOf(s) + 1); lines.Px = Sketch.LineArt(s.Px, threshold, 12, "#111111");
            s.Opacity = Math.Min(s.Opacity, 0.3); d.Data["active"] = lines.Id;
        });
        _progMsg[ArtKey] = "Saubere Linien liegen auf „Linien“, die Skizze darunter ist blass geschaltet.";
    }

    public int ArtFlats(ProgramDocument d)
    {
        var st = ArtModel(d); var src = st.Doc.Layers.LastOrDefault(l => l.Name.StartsWith("Linien", StringComparison.Ordinal)) ?? ArtActive(d, st.Doc);
        var pal = ArtPalette(d); var n = 0;
        ArtEdit(d, "Vorfärben", p =>
        {
            var lines = p.Get(src.Id); var (flats, regions) = Sketch.Flats(lines.Px, pal.Count >= 3 ? pal : null); n = regions;
            if (regions == 0) throw new InvalidOperationException("Keine geschlossene Fläche gefunden – Lücken in den Linien schließen und noch einmal versuchen.");
            var f = p.Add("Farbflächen", p.Layers.IndexOf(lines)); f.Px = flats; d.Data["active"] = f.Id;
        });
        _progMsg[ArtKey] = $"{n} Flächen vorgefärbt (Ebene „Farbflächen“ unter den Linien). Mit Deckkraft sperren bleibt jede Farbe in ihrer Fläche.";
        return n;
    }

    // ---------- K5 Zeitraffer ----------

    public async Task<string?> ArtTimelapseVideo(ProgramDocument d, bool toCut)
    {
        var dir = ArtTlDir(d); var n = Timelapse.Count(dir);
        if (n < 2) { _progMsg[ArtKey] = "Für einen Zeitraffer erst ein paar Striche malen – jeder Strich wird ein Bild."; Show(ArtKey); return null; }
        var ff = ArtVideoOverride is null ? FfmpegPath : "ffmpeg";
        if (ff is null) { _progMsg[ArtKey] = "Für das Video braucht Trendo FFmpeg – unter Werkzeuge mit einem Klick laden."; Show(ArtKey); return null; }
        var output = Path.Combine(ArtDir("videos"), $"{Portfolio.Slug(d.Title)}-zeitraffer-{DateTime.Now:yyyyMMdd-HHmmss}.mp4");
        var fps = Timelapse.Fps(n); bool ok;
        try
        {
            if (ArtVideoOverride is { } o) ok = o(Timelapse.Pattern(dir), fps, output);
            else
            {
                var psi = new ProcessStartInfo(ff) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true };
                foreach (var a in AnimKit.VideoArgs(Timelapse.Pattern(dir), fps, output)) psi.ArgumentList.Add(a);
                psi.ArgumentList.Insert(psi.ArgumentList.IndexOf("-i") + 2, "-vf"); psi.ArgumentList.Insert(psi.ArgumentList.IndexOf("-vf") + 1, "scale=trunc(iw/2)*2:trunc(ih/2)*2");
                using var p = Process.Start(psi) ?? throw new InvalidOperationException("FFmpeg ließ sich nicht starten.");
                _ = await p.StandardError.ReadToEndAsync(); await p.WaitForExitAsync(); ok = p.ExitCode == 0;
            }
            if (!ok || !File.Exists(output)) { _progMsg[ArtKey] = "FFmpeg konnte das Zeitraffer-Video nicht schreiben."; return null; }
            _progMsg[ArtKey] = $"Zeitraffer fertig ({n} Bilder, {fps} Bilder/s): {output}";
            if (toCut) { EditAdd(output, length: Math.Max(1, (double)n / fps), audio: false); Show("schnitt"); return output; }
            NotifyFileResult(_progMsg[ArtKey]); return output;
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or UnauthorizedAccessException or System.ComponentModel.Win32Exception) { _progMsg[ArtKey] = FriendlyError.Say(ex); return null; }
        finally { if (!toCut) Show(ArtKey); }
    }

    // ---------- K7 Teilen ----------

    string ArtPrint(ProgramDocument d, string f, string format)
    {
        var (png, w, h, warn) = Trendo.Core.Kreativ.PrintFile.Make(ArtModel(d).Doc.Composite(), format, 300, 3, background: d.Text("background", "#FFFFFF"));
        File.WriteAllBytes(f, png);
        _progMsg[ArtKey] = $"Druckdatei {w} × {h} px (300 dpi, 3 mm Beschnitt rundum)." + (warn ? " Achtung: Das Bild ist für dieses Format eigentlich zu klein – im Druck wirkt es weich." : "");
        return f;
    }

    public string ArtPortfolio(ProgramDocument d)
    {
        var docs = ProgramDocs(ArtKey).ToList(); if (!docs.Contains(d)) docs.Insert(0, d);
        var items = docs.Select(x => new Portfolio.Item(x.Title, PngCodec.Encode(ArtModel(x).Doc.Composite(), fast: false))).ToList();
        var dir = ArtDir("portfolio"); foreach (var old in Directory.GetFiles(dir, "*.png", SearchOption.AllDirectories)) File.Delete(old);
        var f = Portfolio.Write(dir, "Portfolio", "", items);
        _core.Audit.Append(Principal.LocalOwner.Name, "kuenstler.portfolio", $"{items.Count} Bilder");
        _progMsg[ArtKey] = $"Portfolio mit {items.Count} Bild(ern): {f} – zum Veröffentlichen den Ordner im Web-Design-Studio oder bei deinem Anbieter hochladen.";
        NotifyFileResult(_progMsg[ArtKey]);
        return f;
    }

    public string ArtToPod(ProgramDocument d)
    {
        var file = Path.Combine(ArtDir("druck"), $"{Portfolio.Slug(d.Title)}-{DateTime.Now:yyyyMMdd-HHmmss}.png");
        File.WriteAllBytes(file, PngCodec.Encode(ArtModel(d).Doc.Composite(false), fast: false, dpi: 300));
        var pod = ProgramActive(PodKey) ?? ProgramNew(PodKey);
        PodAdd(pod, "image", file);
        _progMsg[PodKey] = $"Motiv aus dem Künstlerstudio eingefügt ({ArtModel(d).Doc.W} × {ArtModel(d).Doc.H} px) – Druckqualität wird geprüft.";
        Show(PodKey); return file;
    }

    // ---------- Oberfläche ----------

    private Control ArtWorkspace(ProgramDocument d)
    {
        var st = ArtModel(d);
        var canvas = new PaintCanvas { Name = "art-canvas", State = st, Zoom = ArtZoom, Pan = ArtPan, ShowGrid = ArtGrid, Sym = ArtSym(d), Assist = ArtAssist(d), MinHeight = 300,
            Mode = ArtMode, BrushRadius = ArtSize / 2 };
        ArtCanvasForTests = canvas;
        canvas.BeginStroke = () =>
        {
            try { return ArtBegin(d); }
            catch (InvalidOperationException ex) { _progMsg[ArtKey] = ex.Message; Show(ArtKey); return null; }
        };
        canvas.EndStroke = s => ProgramRun(ArtKey, () => ArtEnd(d, s));
        canvas.Picked = (x, y) => ProgramRun(ArtKey, () => { var c = ArtPick(d, x, y); ArtMode = "paint"; _progMsg[ArtKey] = $"Farbe {c} aufgenommen."; });
        canvas.PointSet = (x, y) => ProgramRun(ArtKey, () => ArtAddPoint(d, x, y));
        canvas.ZoomChanged = z => { ArtZoom = z; ArtPan = canvas.Pan; };
        canvas.PointerReleased += (_, _) => ArtPan = canvas.Pan;

        // Optionsleiste über der Fläche: Größe, Deckkraft, Glätten, Schnellfarben
        var bar = new WrapPanel { Name = "art-toolbar", Margin = new Thickness(0, 0, 0, 6) };
        var b = BrushPreset.Get(ArtBrush);
        bar.Children.Add(new TextBlock { Text = (ArtMode == "pick" ? "Pipette" : ArtMode == "points" ? "Hilfspunkte setzen" : b.Name), Foreground = Text, FontWeight = FontWeight.SemiBold, FontSize = 12.5, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) });
        Slider Sl(string name, string label, double min, double max, double val, Action<double> set, double width = 130)
        {
            var s = new Slider { Name = name, Minimum = min, Maximum = max, Value = val, Width = width, Margin = new Thickness(6, 0, 10, 0) };
            s.PropertyChanged += (_, e) => { if (e.Property == Slider.ValueProperty) { set(s.Value); canvas.BrushRadius = ArtSize / 2; } };
            bar.Children.Add(new TextBlock { Text = label, Foreground = Muted, FontSize = 12, VerticalAlignment = VerticalAlignment.Center }); bar.Children.Add(s); return s;
        }
        Sl("art-size", "Größe", 1, 300, ArtSize, v => ArtSize = Math.Round(v), 150);
        Sl("art-opacity", "Deckkraft", 5, 100, ArtOpacity * 100, v => ArtOpacity = Math.Round(v / 100, 2), 100);
        Sl("art-smooth", "Glätten", 0, 100, ArtSmooth * 100, v => ArtSmooth = Math.Round(v / 100, 2), 100);
        var cur = new Border { Name = "art-current-color", Width = 26, Height = 26, CornerRadius = new CornerRadius(13), Background = Brush.Parse(ArtColor), BorderBrush = Edge, BorderThickness = new Thickness(2), Margin = new Thickness(4, 0, 6, 0) };
        ToolTip.SetTip(cur, "Aktuelle Farbe " + ArtColor); bar.Children.Add(cur);
        foreach (var c in ArtRecent.Take(8))
        {
            var cc = c; var sw = new Button { Name = "art-recent-" + c.TrimStart('#').ToLowerInvariant(), Width = 20, Height = 20, Padding = new Thickness(0), Margin = new Thickness(0, 0, 3, 0), CornerRadius = new CornerRadius(10), Background = Brush.Parse(c), BorderBrush = Edge, BorderThickness = new Thickness(1) };
            ToolTip.SetTip(sw, "Zuletzt benutzt: " + c); sw.Click += (_, _) => { ArtUseColor(cc); Show(ArtKey); }; bar.Children.Add(sw);
        }
        // Schnellfarben: auch in „Einfach“ (Eigenschaften eingeklappt) sofort eine Farbe wählen
        bar.Children.Add(new Border { Width = 1, Height = 20, Background = Edge, Margin = new Thickness(6, 0, 8, 0) });
        foreach (var c in ArtSwatches.Where(x => !ArtRecent.Take(8).Contains(x)).Take(10))
        {
            var cc = c; var qs = new Button { Name = "art-quick-" + c.TrimStart('#').ToLowerInvariant(), Width = 20, Height = 20, Padding = new Thickness(0), Margin = new Thickness(0, 0, 3, 0), CornerRadius = new CornerRadius(4), Background = Brush.Parse(c), BorderBrush = Edge, BorderThickness = new Thickness(1) };
            ToolTip.SetTip(qs, "Farbe " + c + " – mehr Farben unter „Eigenschaften“"); qs.Click += (_, _) => { ArtUseColor(cc); Show(ArtKey); }; bar.Children.Add(qs);
        }
        var grid = new Grid { RowDefinitions = new RowDefinitions("Auto,*") };
        grid.Children.Add(bar); Grid.SetRow(canvas, 1); grid.Children.Add(canvas);
        return grid;
    }

    void ArtAddPoint(ProgramDocument d, double x, double y)
    {
        var a = ArtAssist(d); var need = ArtNeedPoints(a.Mode);
        if (need == 0) { ArtMode = "paint"; return; }
        var pts = a.Pts.Count >= need ? [] : a.Pts.ToList(); pts.Add((Math.Round(x, 1), Math.Round(y, 1)));
        d.Edit("Hilfspunkt", j => j["assist"] = new JsonObject { ["mode"] = a.Mode, ["pts"] = new JsonArray([.. pts.Select(p => (JsonNode)new JsonArray(p.X, p.Y))]) });
        if (pts.Count >= need) { ArtMode = "paint"; _progMsg[ArtKey] = "Zeichenhilfe steht – Striche rasten jetzt ein. Zum Ausschalten rechts „Aus“ wählen."; }
        else _progMsg[ArtKey] = $"Punkt {pts.Count} von {need} gesetzt – noch {need - pts.Count} auf die Fläche klicken.";
    }

    /// <summary>Spiegeln und Zeichenhilfe einstellen (öffentlich für Tests und die KI).</summary>
    public void ArtSetSymmetry(ProgramDocument d, string mode, int parts = 8) =>
        d.Edit("Spiegeln", x => x["sym"] = new JsonObject { ["mode"] = PaintSymmetry.Modes.Any(m => m.Id == mode) ? mode : "aus", ["parts"] = Math.Clamp(parts, 2, 32) });
    public void ArtSetAssist(ProgramDocument d, string mode, IEnumerable<(double X, double Y)>? pts = null)
    {
        if (!DrawAssist.Modes.Any(m => m.Id == mode)) throw new ArgumentException("Unbekannte Zeichenhilfe: " + mode);
        var list = pts?.ToList() ?? [];
        d.Edit("Zeichenhilfe", x => x["assist"] = new JsonObject { ["mode"] = mode, ["pts"] = new JsonArray([.. list.Select(p => (JsonNode)new JsonArray(p.X, p.Y))]) });
        ArtMode = list.Count < ArtNeedPoints(mode) ? "points" : "paint";
        if (ArtMode == "points") _progMsg[ArtKey] = $"Jetzt {ArtNeedPoints(mode)} Punkt(e) auf die Fläche klicken – das sind die Fluchtpunkte bzw. die Enden des Lineals.";
    }

    private Control ArtProperties(ProgramDocument d)
    {
        var st = ArtModel(d); var sp = new StackPanel { Name = "art-props", Spacing = 6 };
        Button B(string name, string label, Action a, bool primary = false)
        {
            var b = new Button { Name = name, Content = label, FontSize = 12, Margin = new Thickness(0, 0, 4, 4) };
            if (primary) { b.Background = Accent; b.Foreground = Brushes.White; }
            b.Click += (_, _) => ProgramRun(ArtKey, a); return b;
        }
        ComboBox Combo(string name, IEnumerable<(string Id, string Name)> items, string current, Action<string> set)
        {
            var list = items.ToList(); var c = new ComboBox { Name = name, ItemsSource = list.Select(i => i.Name).ToList(), SelectedIndex = Math.Max(0, list.FindIndex(i => i.Id == current)), MinWidth = 180, FontSize = 12 };
            c.SelectionChanged += (_, _) => { if (c.SelectedIndex >= 0 && list[c.SelectedIndex].Id != current) ProgramRun(ArtKey, () => set(list[c.SelectedIndex].Id)); };
            return c;
        }

        // Farbe
        sp.Children.Add(H("Farbe", 13));
        var wheel = new HsvWheel { Name = "art-wheel", HorizontalAlignment = HorizontalAlignment.Left }; wheel.Set(ArtColor);
        var hex = new TextBox { Name = "art-hex", Text = ArtColor, Width = 100, FontSize = 12 };
        wheel.Changed = c => { ArtUseColor(c); Show(ArtKey); };
        OnEnter(hex, () => ProgramRun(ArtKey, () => ArtUseColor((hex.Text ?? "").Trim())));
        sp.Children.Add(wheel);
        sp.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { hex, B("art-hex-ok", "Übernehmen", () => ArtUseColor((hex.Text ?? "").Trim())) } });
        var sw = new WrapPanel { Name = "art-swatches" };
        void Swatch(WrapPanel into, string prefix, string c, string tip)
        {
            var cc = c; var bt = new Button { Name = prefix + c.TrimStart('#').ToLowerInvariant(), Width = 22, Height = 22, Padding = new Thickness(0), Margin = new Thickness(0, 0, 3, 3), CornerRadius = new CornerRadius(4), Background = Brush.Parse(c), BorderBrush = Edge, BorderThickness = new Thickness(1) };
            ToolTip.SetTip(bt, tip + " " + c); bt.Click += (_, _) => { ArtUseColor(cc); Show(ArtKey); }; into.Children.Add(bt);
        }
        foreach (var c in ArtSwatches) Swatch(sw, "art-sw-", c, "Farbe");
        sp.Children.Add(sw);
        // Harmonien
        var harm = new WrapPanel { Name = "art-harmony" };
        var hid = d.Text("harmony", "komplementaer");
        foreach (var c in PaintColors.Harmony(ArtColor, PaintColors.Harmonies.Any(h => h.Id == hid) ? hid : "komplementaer").Skip(1)) Swatch(harm, "art-harm-", c, "Passt dazu:");
        sp.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = {
            Combo("art-harmony-kind", PaintColors.Harmonies.Select(h => (h.Id, h.Name)), hid, v => d.Data["harmony"] = v), harm } });
        // Palette
        var pal = ArtPalette(d);
        sp.Children.Add(H("Palette", 12));
        var pw = new WrapPanel { Name = "art-palette" };
        foreach (var c in pal) Swatch(pw, "art-pal-", c, "Palette:");
        if (pal.Count == 0) pw.Children.Add(P("Noch leer – Farbe hinzufügen oder aus einem Foto holen.", Muted, 11));
        sp.Children.Add(pw);
        sp.Children.Add(new WrapPanel { Children = {
            B("art-pal-add", "＋ Farbe", () => d.Set("palette", new JsonArray([.. ArtPalette(d).Append(ArtColor).Distinct().Select(c => (JsonNode)c)]), "Palette")),
            B("art-pal-harm", "Harmonie übernehmen", () => d.Set("palette", new JsonArray([.. ArtPalette(d).Concat(PaintColors.Harmony(ArtColor, d.Text("harmony", "komplementaer"))).Distinct().Select(c => (JsonNode)c)]), "Palette")),
            B("art-pal-ref", "Farben aus Referenz", () => ArtColorsFromReference(d)),
            B("art-pal-load", "Laden (.gpl)", () => _ = ArtLoadPalette(d)),
            B("art-pal-clear", "Leeren", () => d.Set("palette", new JsonArray(), "Palette leeren")),
        } });

        // Ebenen (oben = vorne)
        sp.Children.Add(H("Ebenen", 13));
        var active = ArtActive(d, st.Doc);
        for (var i = st.Doc.Layers.Count - 1; i >= 0; i--)
        {
            var l = st.Doc.Layers[i]; var id = l.Id; var on = id == active.Id; var idx = i;
            var eye = new Button { Name = $"art-vis-{i}", Content = l.Visible ? "👁" : "–", Width = 28, Padding = new Thickness(0), FontSize = 12 };
            ToolTip.SetTip(eye, l.Visible ? "Ausblenden" : "Einblenden");
            eye.Click += (_, _) => ProgramRun(ArtKey, () => ArtLayerProp(d, id, l.Visible ? "Ausblenden" : "Einblenden", o => o["visible"] = !l.Visible));
            var lk = new Button { Name = $"art-lock-{i}", Content = l.Locked ? "🔒" : "🔓", Width = 28, Padding = new Thickness(0), FontSize = 11 };
            ToolTip.SetTip(lk, l.Locked ? "Entsperren" : "Sperren (nicht mehr bemalbar)");
            lk.Click += (_, _) => ProgramRun(ArtKey, () => ArtLayerProp(d, id, l.Locked ? "Entsperren" : "Sperren", o => o["locked"] = !l.Locked));
            var pick = new Button { Name = $"art-layer-{i}", Content = l.Name + (l.Blend != "normal" ? "  · " + PaintBlend.Modes.First(m => m.Id == l.Blend).Name : "") + (l.Opacity < 0.999 ? $"  · {l.Opacity * 100:0} %" : ""),
                HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left, FontSize = 12 };
            if (on) { pick.Background = Accent; pick.Foreground = Brushes.White; }
            pick.Click += (_, _) => { d.Data["active"] = id; Show(ArtKey); }; // Auswahl ist kein Bearbeitungsschritt
            var row = new DockPanel { Margin = new Thickness(0, 0, 0, 2), Children = { eye, lk, pick } };
            DockPanel.SetDock(eye, Avalonia.Controls.Dock.Left); DockPanel.SetDock(lk, Avalonia.Controls.Dock.Left);
            sp.Children.Add(row);
        }
        // gewählte Ebene
        var name = new TextBox { Name = "art-layer-name", Text = active.Name, FontSize = 12 };
        OnEnter(name, () => ProgramRun(ArtKey, () => { var t = (name.Text ?? "").Trim(); if (t.Length == 0) throw new InvalidOperationException("Bitte einen Namen eingeben."); ArtLayerProp(d, active.Id, "Umbenennen", o => o["name"] = t); }));
        var op = new Slider { Name = "art-layer-opacity", Minimum = 0, Maximum = 100, Value = active.Opacity * 100 };
        op.PointerCaptureLost += (_, _) => ProgramRun(ArtKey, () => ArtLayerProp(d, active.Id, "Deckkraft", o => o["opacity"] = Math.Round(op.Value / 100, 2)));
        op.KeyUp += (_, _) => ProgramRun(ArtKey, () => ArtLayerProp(d, active.Id, "Deckkraft", o => o["opacity"] = Math.Round(op.Value / 100, 2)));
        var al = new CheckBox { Name = "art-alpha-lock", Content = "Deckkraft sperren (nur Gemaltes färben)", IsChecked = active.AlphaLock, Foreground = Text, FontSize = 12 };
        al.IsCheckedChanged += (_, _) => { if ((al.IsChecked == true) != active.AlphaLock) ProgramRun(ArtKey, () => ArtLayerProp(d, active.Id, "Deckkraft sperren", o => o["alphaLock"] = al.IsChecked == true)); };
        sp.Children.Add(new StackPanel { Spacing = 4, Children = {
            P("Gewählte Ebene", Muted, 11), name,
            new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { new TextBlock { Text = "Mischart", Foreground = Muted, FontSize = 12, VerticalAlignment = VerticalAlignment.Center },
                Combo("art-layer-blend", PaintBlend.Modes, active.Blend, v => ArtLayerProp(d, active.Id, "Mischart", o => o["blend"] = v)) } },
            new TextBlock { Text = "Deckkraft der Ebene", Foreground = Muted, FontSize = 12 }, op, al } });

        // Zeichenhilfen
        sp.Children.Add(H("Zeichenhilfen", 13));
        var sym = ArtSym(d); var asst = ArtAssist(d);
        sp.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { new TextBlock { Text = "Spiegeln", Foreground = Muted, FontSize = 12, Width = 70, VerticalAlignment = VerticalAlignment.Center },
            Combo("art-sym", PaintSymmetry.Modes, sym.Mode, v => ArtSetSymmetry(d, v, sym.Parts)) } });
        if (sym.Mode == "radial")
        {
            var parts = new NumericUpDown { Name = "art-sym-parts", Minimum = 2, Maximum = 32, Value = sym.Parts, Width = 120, FontSize = 12 };
            parts.ValueChanged += (_, _) => { if (parts.Value is { } v && (int)v != sym.Parts) ProgramRun(ArtKey, () => ArtSetSymmetry(d, "radial", (int)v)); };
            sp.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { new TextBlock { Text = "Teile", Foreground = Muted, FontSize = 12, Width = 70, VerticalAlignment = VerticalAlignment.Center }, parts } });
        }
        sp.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { new TextBlock { Text = "Hilfe", Foreground = Muted, FontSize = 12, Width = 70, VerticalAlignment = VerticalAlignment.Center },
            Combo("art-assist", DrawAssist.Modes, asst.Mode, v => ArtSetAssist(d, v)) } });
        if (ArtNeedPoints(asst.Mode) > 0) sp.Children.Add(B("art-assist-points", $"Punkte neu setzen ({asst.Pts.Count}/{ArtNeedPoints(asst.Mode)})", () => ArtMode = "points"));
        var gridBox = new CheckBox { Name = "art-grid-box", Content = "Raster einblenden", IsChecked = ArtGrid, Foreground = Text, FontSize = 12 };
        gridBox.IsCheckedChanged += (_, _) => { ArtGrid = gridBox.IsChecked == true; Show(ArtKey); };
        sp.Children.Add(gridBox);

        // Referenz und Posenpuppe
        sp.Children.Add(H("Referenz", 13));
        var refs = d.List("refs").Select(n => n.AsText()).OfType<string>().Where(File.Exists).ToList();
        if (refs.LastOrDefault() is { } rf)
        {
            try
            {
                var bmp = new Bitmap(rf); var img = new Image { Name = "art-ref-image", Source = bmp, MaxHeight = 220, Stretch = Stretch.Uniform, HorizontalAlignment = HorizontalAlignment.Left, Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Cross) };
                ToolTip.SetTip(img, "Klicken nimmt die Farbe auf");
                img.PointerPressed += (_, e) =>
                {
                    var p = e.GetPosition(img); var sc = Math.Min(img.Bounds.Width / bmp.PixelSize.Width, img.Bounds.Height / bmp.PixelSize.Height); if (sc <= 0) return;
                    int px = (int)(p.X / sc), py = (int)(p.Y / sc);
                    ProgramRun(ArtKey, () => { var src = ArtDecode(rf); var (r, g, bb, _) = src.At(Math.Clamp(px, 0, src.W - 1), Math.Clamp(py, 0, src.H - 1)); ArtUseColor(PaintColors.Hex(r, g, bb)); _progMsg[ArtKey] = "Farbe aus der Referenz: " + ArtColor; });
                };
                sp.Children.Add(img);
            }
            catch (Exception e) when (e is IOException or ArgumentException or InvalidOperationException) { sp.Children.Add(P("Referenzbild lässt sich nicht anzeigen.", Muted, 11)); }
        }
        else sp.Children.Add(P("Ein Foto oder Bild neben der Arbeit – wird nicht mitgemalt.", Muted, 11));
        sp.Children.Add(new WrapPanel { Children = {
            B("art-ref-load", "Referenz laden …", () => _ = ArtLoadReference(d)),
            B("art-ref-clear", "Entfernen", () => d.Set("refs", new JsonArray(), "Referenz entfernen")),
        } });
        sp.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = {
            Combo("art-pose", Mannequin.Poses.Select(p => (p.Id, p.Name)), d.Text("pose", "stehen"), v => d.Data["pose"] = v),
            B("art-puppet-add", "Posenpuppe einfügen", () => ArtPuppet(d, d.Text("pose", "stehen"), d.Number("poseYaw"))) } });
        var yaw = new Slider { Name = "art-pose-yaw", Minimum = -90, Maximum = 90, Value = d.Number("poseYaw"), Width = 180 };
        yaw.PropertyChanged += (_, e) => { if (e.Property == Slider.ValueProperty) d.Data["poseYaw"] = Math.Round(yaw.Value); };
        sp.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { new TextBlock { Text = "Drehung", Foreground = Muted, FontSize = 12, VerticalAlignment = VerticalAlignment.Center }, yaw } });
        var idea = new TextBox { Name = "art-idea", PlaceholderText = "Ideenskizze, z. B. „Fuchs im Schnee“", FontSize = 12 };
        sp.Children.Add(idea);
        var ideaBtn = new Button { Name = "art-idea-go", Content = _artAiBusy ? "Skizziert …" : "Ideenskizze (lokale Bild-KI)", FontSize = 12, IsEnabled = !_artAiBusy };
        ToolTip.SetTip(ideaBtn, "Nur als Anregung neben dem Bild – läuft auf deinem PC, nichts geht ins Internet.");
        ideaBtn.Click += (_, _) => _ = ArtIdeaSketch(d, idea.Text ?? "");
        sp.Children.Add(ideaBtn);

        // Zeitraffer
        sp.Children.Add(H("Zeitraffer", 13));
        var tl = new CheckBox { Name = "art-timelapse", Content = $"Aufnehmen ({Timelapse.Count(ArtTlDir(d))} Bilder)", IsChecked = d.Data["timelapse"].AsBool(true), Foreground = Text, FontSize = 12 };
        tl.IsCheckedChanged += (_, _) => { d.Data["timelapse"] = tl.IsChecked == true; d.EditMeta(_ => { }); };
        sp.Children.Add(tl);

        // Malkurs
        sp.Children.Add(H("Malkurs", 13));
        var courseObj = d.Data["course"] as JsonObject; var cid = courseObj?["id"].AsText() ?? "";
        sp.Children.Add(Combo("art-course", [("", "Kein Kurs"), .. Courses.All.Select(c => (c.Id, $"{c.Title} ({c.Level})"))], cid,
            v => { d.Data["course"] = v.Length == 0 ? null : new JsonObject { ["id"] = v, ["step"] = 0, ["s0"] = (int)d.Number("strokes") }; d.EditMeta(_ => { }); }));
        if (cid.Length > 0 && Courses.All.FirstOrDefault(c => c.Id == cid) is { } course)
        {
            var step = ArtCourseStep(d, course);
            sp.Children.Add(P(course.Goal, Muted, 11));
            if (step >= course.Steps.Count) sp.Children.Add(new TextBlock { Name = "art-course-done", Text = "✓ Kurs geschafft – stark!", Foreground = Accent, FontWeight = FontWeight.SemiBold, FontSize = 13 });
            else
            {
                sp.Children.Add(new TextBlock { Name = "art-course-step", Text = $"Schritt {step + 1} von {course.Steps.Count}: {course.Steps[step].Text}", TextWrapping = TextWrapping.Wrap, Foreground = Text, FontSize = 12.5, FontWeight = FontWeight.SemiBold });
                sp.Children.Add(P("Tipp: " + course.Steps[step].Hint, Muted, 11));
            }
        }
        return sp;
    }

    /// <summary>Kursfortschritt: der Kurs zählt Striche ab Kursbeginn; ein erledigter Schritt bleibt erledigt.</summary>
    public int ArtCourseStep(ProgramDocument d, Courses.Course course)
    {
        var o = d.Data["course"] as JsonObject; var from = (int)ProgramDocument.ToNumber(o?["step"]); var s0 = (int)ProgramDocument.ToNumber(o?["s0"]);
        var st = ArtModel(d);
        var state = Courses.Of(st.Doc, ArtSym(d).Mode, ArtAssist(d).Mode, ArtPalette(d).Count, (int)d.Number("strokes") - s0, d.List("refs").Count > 0);
        var step = Courses.Progress(course, state, from);
        if (o is not null && step != from) o["step"] = step; // Fortschritt ist kein Bearbeitungsschritt
        return step;
    }

    async Task ArtLoadReference(ProgramDocument d)
    {
        var f = await PickMediaFile("art-ref", "Referenzbild", ["*.png", "*.jpg", "*.jpeg", "*.webp"]);
        if (f is not null) ProgramRun(ArtKey, () => ArtAddReference(d, f));
    }

    async Task ArtLoadPalette(ProgramDocument d)
    {
        var f = await PickMediaFile("art-palette", "Palette laden", ["*.gpl"]);
        if (f is null) return;
        ProgramRun(ArtKey, () =>
        {
            var (name, cols) = Palettes.FromGpl(File.ReadAllText(f));
            d.Set("palette", new JsonArray([.. cols.Select(c => (JsonNode)c)]), "Palette laden");
            _progMsg[ArtKey] = $"Palette „{name}“ mit {cols.Count} Farben geladen.";
        });
    }
}
