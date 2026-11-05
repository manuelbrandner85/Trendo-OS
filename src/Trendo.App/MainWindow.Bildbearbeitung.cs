using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Trendo.Core;
using Trendo.Core.Media;
using Trendo.Core.Programs;

namespace Trendo.App;

/// <summary>CORE-08 (Masterprompt 131, Beispiel Bildbearbeitung): Bild öffnen, auf der Arbeitsfläche bearbeiten, Ebenen anlegen, Filter anwenden, speichern.
/// Werkzeuge: Verschieben, Pinsel, Radierer, Text, Rechteck, Ellipse, Zuschneiden. Jede Änderung ist ein Schritt im Rückgängig der Programm-Hülle.</summary>
public partial class MainWindow
{
    const string BK = "bild";
    public string ImgTool { get; set; } = "move";
    public string ImgColor { get; set; } = "#E0475B";
    public double ImgBrush { get; set; } = 14;
    /// <summary>Deckkraft des Pinsels (0,05–1) – wie in Krita.</summary>
    public double ImgBrushAlpha { get; set; } = 1;
    public double ImgZoom { get; set; }
    public ImageCanvas? ImgCanvasForTests { get; private set; }
    static readonly (string Id, string Label, string Tip)[] ImgTools =
    [
        ("move", "✥ Verschieben", "Ebene anklicken wählt sie, Ziehen verschiebt sie"), ("brush", "🖌 Pinsel", "Auf einer Mal-Ebene malen (wird bei Bedarf angelegt)"),
        ("eraser", "◌ Radierer", "Auf der Mal-Ebene radieren"), ("text", "T Text", "Klick setzt einen Text"), ("rect", "▭ Rechteck", "Ziehen zeichnet ein Rechteck"),
        ("ellipse", "◯ Ellipse", "Ziehen zeichnet eine Ellipse"), ("crop", "⛶ Zuschneiden", "Ziehen wählt den Ausschnitt der Leinwand"),
        ("vector", "〰 Vektorpinsel", "Zeichnen ergibt einen glatten Vektorpfad – später verschieb- und umfärbbar, als SVG speicherbar"),
        ("select", "⬚ Auswahl", "Rechteck, Ellipse, Lasso oder Zauberstab (oben umschalten) – dann „Maske aus Auswahl“ oder „Inhaltsbasiert füllen“"),
        ("gradient", "◩ Verlauf", "Ziehen legt einen Verlauf von der gewählten Farbe zu durchsichtig"),
        ("fill", "▣ Füllen", "Klick füllt die zusammenhängende Fläche mit der gewählten Farbe (Toleranz rechts)"),
        ("heal", "✚ Reparieren", "Über Fleck, Kratzer oder Staub malen – wird aus der Umgebung aufgefüllt"),
        ("clone", "⎘ Klonen", "Erst klicken = Quelle, dann übermalen – kopiert von der Quelle"),
    ];
    /// <summary>Auswahl-Rechteck (Leinwand-Pixel) – kein Bearbeitungsschritt, wie in GIMP.</summary>
    public Rect? ImgMarquee { get; set; }
    /// <summary>Lasso-Auswahl (Leinwand-Pixel); schließt Rechteck und Zauberstab aus.</summary>
    public List<(double X, double Y)>? ImgLasso { get; set; }
    /// <summary>Zauberstab-Auswahl (Pixel der Leinwand zum Zeitpunkt des Klicks).</summary>
    public (bool[] Bits, int W, int H)? ImgWand { get; set; }
    /// <summary>Auswahl umkehren (Photoshop „Auswahl umkehren“) – gilt für Maske und Füllen.</summary>
    public bool ImgSelInvert { get; set; }
    /// <summary>Aktuelle Auswahl als Masken-Beschreibung (rect, ellipse, poly, bitmap) oder null.</summary>
    public JsonObject? ImgSelectionSpec(double feather = 0)
    {
        var tmp = new JsonObject();
        if (ImgWand is { } wd) ImageDoc.SetMaskBitmap(tmp, wd.Bits, wd.W, wd.H, feather, ImgSelInvert);
        else if (ImgLasso is { Count: >= 3 } la) ImageDoc.SetMaskPoly(tmp, la, feather, ImgSelInvert);
        else if (ImgMarquee is { } m) ImageDoc.SetMask(tmp, ImgMaskEllipse ? "ellipse" : "rect", m.X, m.Y, m.Width, m.Height, feather, ImgSelInvert);
        else return null;
        return (JsonObject)tmp["mask"]!;
    }
    void ImgClearSelection() { ImgMarquee = null; ImgLasso = null; ImgWand = null; }
    /// <summary>Quelle des Kopierstempels (Leinwand-Pixel).</summary>
    public Point? ImgCloneSrc { get; set; }
    /// <summary>Toleranz des Farbeimers (0–255).</summary>
    public double ImgFillTolerance { get; set; } = 32;
    /// <summary>Art der Auswahl wie die Werkzeuggruppe in Photoshop: rect, ellipse, lasso, wand.</summary>
    public string ImgSelMode { get; set; } = "rect";
    public bool ImgMaskEllipse { get => ImgSelMode == "ellipse"; set => ImgSelMode = value ? "ellipse" : ImgSelMode == "ellipse" ? "rect" : ImgSelMode; }
    /// <summary>Werkzeug, wie es die Leinwand sieht (Auswahl im Lasso-/Zauberstab-Modus).</summary>
    string ImgEff => ImgTool == "select" && ImgSelMode is "lasso" or "wand" ? ImgSelMode : ImgTool;
    public bool ImgGradRadial { get; set; }
    static readonly string[] ImgColors = ["#000000", "#FFFFFF", "#E0475B", "#F59E0B", "#FACC15", "#22C55E", "#2F86F6", "#7C3AED", "#EC4899", "#8B5A2B"];

    private void InitBildbearbeitung() => RegisterProgram(KreativTabs, new ProgramSpec(BK, "CORE-08", "Bildbearbeitung")
    {
        Sentence = "Fotos und Grafiken mit Ebenen bearbeiten – malen, Text, Formen, Filter, zuschneiden.",
        FillHeight = true,
        Folder = "Medien",
        GroupLevels = new() { ["Ebene"] = 1 },
        Templates =
        [
            new("leer", "Leere Leinwand", "1920 × 1080, weiß", () => ImageDoc.New(1920, 1080)),
            new("quadrat", "Beitrag (1080 × 1080)", "Für soziale Netzwerke", () => ImageDoc.New(1080, 1080, "#0F172A")),
            new("hoch", "Story (1080 × 1920)", "Hochformat fürs Handy", () => ImageDoc.New(1080, 1920, "#111827")),
            new("plakat", "Plakat A4 (1240 × 1754)", "Hochformat zum Ausdrucken (150 dpi)", () => ImageDoc.New(1240, 1754)),
        ],
        Ribbon =
        [
            ("Ebene", [
                new ProgramCmd("img-layer-paint", "＋ Mal-Ebene", d => d.Edit("Mal-Ebene", x => ImageDoc.AddPaint(x))),
                new ProgramCmd("img-layer-up", "▲ Nach oben", d => ImgSel(d, (x, id) => ImageDoc.Reorder(x, id, 1), "Nach oben")),
                new ProgramCmd("img-layer-down", "▼ Nach unten", d => ImgSel(d, (x, id) => ImageDoc.Reorder(x, id, -1), "Nach unten")),
                new ProgramCmd("img-layer-del", "Ebene löschen", d => ImgSel(d, (x, id) => ImageDoc.Remove(x, id), "Ebene löschen")),
            ]),
            ("Ansicht", [
                new ProgramCmd("img-zoom-fit", "Einpassen", _ => ImgZoom = 0),
                new ProgramCmd("img-zoom-100", "100 %", _ => ImgZoom = 1),
            ]),
        ],
        Exports =
        [
            new("png", "PNG", ".png", (d, f) => ImageRender.Export(d.Data, f)),
            new("jpg", "JPG", ".jpg", (d, f) => ImageRender.Export(d.Data, f)),
            new("svg", "SVG (Vektor)", ".svg", (d, f) => { File.WriteAllText(f, Trendo.Core.Media.ImageVector.ToSvg(d.Data)); return f; }), // selbst statt Inkscape
        ],
        Tools = [.. ImgTools.Select(t => new ProgramTool("img-tool-" + t.Id, t.Label[..t.Label.IndexOf(' ')], t.Label[(t.Label.IndexOf(' ') + 1)..], t.Tip, _ => ImgTool = t.Id, () => ImgTool == t.Id))],
        Workspace = ImgWorkspace,
        Properties = ImgProperties,
        Status = d => $"{(int)d.Number("width")} × {(int)d.Number("height")} px · {ImageDoc.Layers(d.Data).Count} Ebenen · Werkzeug: {ImgTools.First(t => t.Id == ImgTool).Label[2..]}",
        Help = "Rechts ein Bild hinzufügen. Links ein Werkzeug wählen und auf der Fläche arbeiten. Rechts die Ebenen: anklicken, ein-/ausblenden, Deckkraft, Filter. Speichern als Projekt, Exportieren als PNG oder JPG.",
    }, "bildbearbeitung", "foto bearbeiten", "ebenen", "pinsel", "filter", "zuschneiden", "grafik", "collage", "plakat");

    static string? SelId(ProgramDocument d) => d.Text("selected") is { Length: > 0 } s && ImageDoc.Layer(d.Data, s) is not null ? s : null;
    static void ImgSel(ProgramDocument d, Action<JsonObject, string> a, string label)
    {
        var id = SelId(d) ?? throw new InvalidOperationException("Erst eine Ebene auswählen (rechts oder auf der Fläche).");
        d.Edit(label, x => a(x, id));
    }

    /// <summary>SVG öffnen: Formen, Pfade und Text werden bearbeitbare Ebenen (selbst nachgebaut statt Inkscape).</summary>
    public int ImgOpenSvg(string file)
    {
        if (!File.Exists(file)) throw new FileNotFoundException("Datei nicht gefunden: " + Path.GetFileName(file));
        var (doc, skipped) = Trendo.Core.Media.ImageVector.FromSvg(File.ReadAllText(file));
        var d = ProgramNew(BK);
        d.Edit("SVG öffnen", x => { foreach (var kv in doc.ToList()) x[kv.Key] = kv.Value?.DeepClone(); });
        var n = ImageDoc.Layers(d.Data).Count;
        _progMsg[BK] = $"„{Path.GetFileName(file)}“ geöffnet: {n} Ebenen" + (skipped > 0 ? $", {skipped} unbekannte Teile übersprungen." : ".");
        return n;
    }

    /// <summary>Bild als Ebene hinzufügen (öffentlich für Tests und die KI).</summary>
    public void ImgAddImage(string file)
    {
        if (Path.GetExtension(file).Equals(".svg", StringComparison.OrdinalIgnoreCase)) { ImgOpenSvg(file); return; }
        var d = ProgramActive(BK) ?? ProgramNew(BK);
        if (!File.Exists(file)) throw new FileNotFoundException("Datei nicht gefunden: " + Path.GetFileName(file));
        using var b = SkiaSharp.SKBitmap.Decode(file) ?? throw new InvalidOperationException("Das ist kein Bild, das Trendo öffnen kann.");
        var first = ImageDoc.Layers(d.Data).Count == 0;
        d.Edit("Bild hinzufügen", x =>
        {
            if (first) { x["width"] = b.Width; x["height"] = b.Height; } // erstes Bild bestimmt die Leinwand
            ImageDoc.AddImage(x, file, b.Width, b.Height);
        });
        _progMsg[BK] = $"„{Path.GetFileName(file)}“ ist eine Ebene ({b.Width} × {b.Height}).";
    }

    private Control ImgWorkspace(ProgramDocument d)
    {
        var png = ImageRender.Png(d.Data);
        var canvas = new ImageCanvas { Name = "img-canvas", DocW = (int)d.Number("width", 800), DocH = (int)d.Number("height", 600), Zoom = ImgZoom, Tool = ImgEff, BrushColor = Color.Parse(ImgColor), BrushSize = ImgBrush,
            Image = new Bitmap(new MemoryStream(png)), MinHeight = 240 };
        ImgCanvasForTests = canvas; canvas.Marquee = ImgMarquee; canvas.CloneSource = ImgCloneSrc;
        canvas.LassoPath = ImgLasso?.Select(q => new Point(q.X, q.Y)).ToList();
        if (ImgWand is { } wand && wand.W == canvas.DocW && wand.H == canvas.DocH) canvas.SelectionOverlay = new Bitmap(new MemoryStream(ImageRender.SelectionPng(wand.Bits, wand.W, wand.H)));
        canvas.Lassoed = pts => ProgramRun(BK, () =>
        {
            ImgClearSelection(); ImgLasso = pts;
            double x0 = pts.Min(q => q.X), y0 = pts.Min(q => q.Y); ImgMarquee = new Rect(x0, y0, pts.Max(q => q.X) - x0, pts.Max(q => q.Y) - y0);
            _progMsg[BK] = $"Lasso-Auswahl mit {pts.Count} Punkten – oben „Maske aus Auswahl“ oder „Inhaltsbasiert füllen“.";
        });
        canvas.Line = (a, b) => ProgramRun(BK, () => d.Edit("Verlauf", x => ImageDoc.AddGradient(x, a.X, a.Y, b.X, b.Y, ImgColor, "#00" + ImgColor.TrimStart('#')[^6..], ImgGradRadial)));
        if (SelId(d) is { } sid && ImageDoc.Layer(d.Data, sid) is { } sl && ImageDoc.S(sl["kind"]) != "paint")
            canvas.Selection = new Rect(ImageDoc.N(sl["x"]), ImageDoc.N(sl["y"]), ImageDoc.N(sl["w"]), ImageDoc.N(sl["h"]));
        canvas.Clicked = p => ProgramRun(BK, () =>
        {
            if (ImgEff == "wand") { ImgWandAt(d, p.X, p.Y); return; }
            if (ImgTool == "fill") { d.Edit("Füllen", x => ImageDoc.AddFill(ImageDoc.Retouch(x, SelId(d)), p.X, p.Y, ImgColor, ImgFillTolerance)); return; }
            if (ImgTool == "text") { d.Edit("Text", x => ImageDoc.AddText(x, "Dein Text", p.X, p.Y, Math.Max(24, d.Number("height") / 14), ImgColor)); _progMsg[BK] = "Text gesetzt – rechts ändern."; return; }
            var hit = ImageDoc.HitTest(d.Data, p.X, p.Y); d.Data["selected"] = hit ?? ""; // Auswahl ist kein Bearbeitungsschritt
        });
        canvas.Moved = (p, by) => ProgramRun(BK, () =>
        {
            var id = SelId(d); var hit = ImageDoc.HitTest(d.Data, p.X, p.Y);
            if (hit is not null && hit != id) { id = hit; d.Data["selected"] = hit; }
            if (id is null) return;
            d.Edit("Verschieben", x => ImageDoc.Move(ImageDoc.Layer(x, id)!, by.X, by.Y));
        });
        canvas.Stroke = (pts, erase) => ProgramRun(BK, () =>
        {
            if (ImgTool == "clone" && (ImgCloneSrc is null || pts.Count == 1))
            {
                if (pts.Count == 1) { ImgCloneSrc = new Point(pts[0].X, pts[0].Y); _progMsg[BK] = "Quelle gesetzt – jetzt die Stelle übermalen, die ersetzt werden soll."; return; }
                throw new InvalidOperationException("Erst einmal auf die Quelle klicken, dann übermalen.");
            }
            if (ImgTool is "heal" or "clone")
            {
                d.Edit(ImgTool == "heal" ? "Reparieren" : "Klonen", x =>
                {
                    var r = ImageDoc.Retouch(x, SelId(d));
                    if (ImgTool == "heal") ImageDoc.AddHeal(r, pts, ImgBrush);
                    else ImageDoc.AddClone(r, pts, ImgBrush, ImgCloneSrc!.Value.X - pts[0].X, ImgCloneSrc!.Value.Y - pts[0].Y);
                });
                return;
            }
            ImgStroke(d, pts, erase);
        });
        void ImgStroke(ProgramDocument d, List<(double X, double Y)> pts, bool erase) => d.Edit(erase ? "Radieren" : "Malen", x =>
        {
            var target = SelId(d) is { } s0 && ImageDoc.S(ImageDoc.Layer(x, s0)!["kind"]) == "paint" ? ImageDoc.Layer(x, s0)! : ImageDoc.Layers(x).OfType<JsonObject>().LastOrDefault(l => ImageDoc.S(l["kind"]) == "paint") ?? ImageDoc.AddPaint(x);
            x["selected"] = (string?)target["id"];
            if (ImgTool == "vector") { var simple = Trendo.Core.Media.ImageVector.Simplify(pts, Math.Max(1, ImgBrush / 6)); if (simple.Count >= 2) Trendo.Core.Media.ImageVector.AddPath(x, simple, ImgColor, ImgBrush); return; }
            ImageDoc.AddStroke(target, pts, ImgColor, ImgBrush, erase, ImgBrushAlpha);
        });
        canvas.Dragged = r => ProgramRun(BK, () =>
        {
            switch (ImgTool)
            {
                case "rect" or "ellipse": d.Edit(ImgTool == "rect" ? "Rechteck" : "Ellipse", x => ImageDoc.AddShape(x, ImgTool, r.X, r.Y, r.Width, r.Height, ImgColor)); break;
                case "select": ImgClearSelection(); ImgMarquee = r; _progMsg[BK] = $"Auswahl {r.Width:0} × {r.Height:0} px – oben „Maske aus Auswahl“."; break;
                case "crop": ImgClearSelection(); d.Edit("Zuschneiden", x => ImageDoc.Crop(x, r.X, r.Y, r.Width, r.Height)); _progMsg[BK] = $"Zugeschnitten auf {(int)d.Number("width")} × {(int)d.Number("height")} px."; break;
            }
        });
        // Farbe und Pinselgröße direkt über der Fläche
        var bar = new WrapPanel { Name = "img-toolbar", Margin = new Thickness(0, 0, 0, 6) };
        // Farben nur bei Werkzeugen, die Farbe brauchen – Auswahl, Reparieren, Klonen, Verschieben, Zuschneiden halten die Leiste kurz
        foreach (var c in ImgTool is "select" or "heal" or "clone" or "move" or "crop" or "eraser" ? [] : ImgColors)
        {
            var cc = c; var on = string.Equals(ImgColor, c, StringComparison.OrdinalIgnoreCase);
            var b = new Button { Name = "img-color-" + c.TrimStart('#').ToLowerInvariant(), Width = 24, Height = 24, Padding = new Thickness(0), Margin = new Thickness(0, 0, 4, 0), CornerRadius = new CornerRadius(12), Background = Brush.Parse(c), BorderBrush = Brush.Parse(on ? "#FACC15" : "#55FFFFFF"), BorderThickness = new Thickness(on ? 3 : 1) };
            ToolTip.SetTip(b, "Farbe " + c); b.Click += (_, _) => { ImgColor = cc; Show(BK); };
            bar.Children.Add(b);
        }
        var size = new Slider { Name = "img-brush-size", Minimum = 1, Maximum = 120, Value = ImgBrush, Width = 160, Margin = new Thickness(12, 0, 6, 0) };
        size.PropertyChanged += (_, e) => { if (e.Property == Slider.ValueProperty) { ImgBrush = Math.Round(size.Value); canvas.BrushSize = ImgBrush; } };
        // nur die Regler, die zum Werkzeug passen (Optionsleiste wie in Photoshop) – so bleibt die Leiste einzeilig
        if (ImgTool is "brush" or "eraser" or "vector" or "heal" or "clone" or "select")
        {
            bar.Children.Add(new TextBlock { Text = ImgTool == "select" ? "Weiche Kante" : "Pinsel", Foreground = Muted, FontSize = 12, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 0) });
            bar.Children.Add(size);
        }
        var alpha = new Slider { Name = "img-brush-alpha", Minimum = 5, Maximum = 100, Value = ImgBrushAlpha * 100, Width = 110, Margin = new Thickness(6, 0, 6, 0) };
        alpha.PropertyChanged += (_, e) => { if (e.Property == Slider.ValueProperty) ImgBrushAlpha = Math.Round(alpha.Value / 100, 2); };
        if (ImgTool == "brush")
        {
            bar.Children.Add(new TextBlock { Text = "Deckkraft", Foreground = Muted, FontSize = 12, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) });
            bar.Children.Add(alpha);
        }
        var before = bar.Children.Count;
        // Einstellungen des gewählten Werkzeugs (wie die Optionsleiste in Photoshop)
        if (ImgTool == "fill" || ImgEff == "wand")
        {
            var tol = new Slider { Name = "img-fill-tol", Minimum = 0, Maximum = 160, Value = ImgFillTolerance, Width = 110, Margin = new Thickness(6, 0, 6, 0) };
            tol.PropertyChanged += (_, e) => { if (e.Property == Slider.ValueProperty) ImgFillTolerance = Math.Round(tol.Value); };
            bar.Children.Add(new TextBlock { Text = "Toleranz", Foreground = Muted, FontSize = 12, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) }); bar.Children.Add(tol);
        }
        CheckBox Opt(string name, string label, bool value, Action<bool> set) { var c = new CheckBox { Name = name, Content = label, IsChecked = value, Foreground = Text, Margin = new Thickness(8, 0, 0, 0) }; c.IsCheckedChanged += (_, _) => set(c.IsChecked == true); return c; }
        if (ImgTool == "gradient") bar.Children.Add(Opt("img-grad-radial", "Kreisförmig", ImgGradRadial, v => ImgGradRadial = v));
        Button Bb(string name, string label, Action a) { var b = new Button { Name = name, Content = label, FontSize = 12, Margin = new Thickness(8, 0, 0, 0) }; b.Click += (_, _) => ProgramRun(BK, a); return b; }
        if (ImgTool == "select")
        {
            foreach (var (id, icon, mtip) in new[] { ("rect", "▭", "Rechteck aufziehen"), ("ellipse", "◯", "Ellipse aufziehen"), ("lasso", "➰", "Lasso: freihändig umfahren"), ("wand", "✨", "Zauberstab: Klick wählt ähnliche Farbe") })
            {
                var on = ImgSelMode == id; var mid = id;
                var mb = new Button { Name = "img-sel-mode-" + id, Content = icon, Width = 30, Height = 28, Padding = new Thickness(0), FontSize = 14, Margin = new Thickness(id == "rect" ? 0 : 2, 0, 0, 0), HorizontalContentAlignment = HorizontalAlignment.Center,
                    Background = on ? Accent : Brushes.Transparent, Foreground = on ? Brushes.White : Text, BorderBrush = Edge, BorderThickness = new Thickness(1) };
                ToolTip.SetTip(mb, mtip); mb.Click += (_, _) => { ImgSelMode = mid; Show(BK); }; bar.Children.Add(mb);
            }
            bar.Children.Add(Opt("img-sel-invert", "Umkehren", ImgSelInvert, v => ImgSelInvert = v));
            bar.Children.Add(Bb("img-mask-sel", "Maske aus Auswahl", () =>
            {
                var spec = ImgSelectionSpec(Math.Round(ImgBrush / 2)) ?? throw new InvalidOperationException("Erst einen Bereich auswählen – ziehen, umfahren oder mit dem Zauberstab klicken.");
                ImgSel(d, (x, id) => ImageDoc.Layer(x, id)!["mask"] = spec.DeepClone(), "Maske");
            }));
            bar.Children.Add(Bb("img-content-fill", "Inhaltsbasiert füllen", () =>
            {
                var spec = ImgSelectionSpec() ?? throw new InvalidOperationException("Erst den störenden Bereich auswählen – z. B. mit dem Lasso umfahren.");
                d.Edit("Inhaltsbasiert füllen", x => ImageDoc.AddContentFill(ImageDoc.Retouch(x, SelId(d)), spec));
                _progMsg[BK] = "Bereich aus der Umgebung neu aufgebaut – Strg+Z nimmt es zurück.";
            }));
            if (ImgMarquee is not null || ImgWand is not null) bar.Children.Add(Bb("img-sel-none", "Auswahl aufheben", ImgClearSelection));
        }
        if (ImgTool == "clone" && ImgCloneSrc is not null) { var rs = new Button { Name = "img-clone-reset", Content = "Neue Quelle", FontSize = 12, Margin = new Thickness(8, 0, 0, 0) }; rs.Click += (_, _) => { ImgCloneSrc = null; Show(BK); }; bar.Children.Add(rs); }
        var tip = ImgTools.First(t => t.Id == ImgTool).Tip; ToolTip.SetTip(bar, tip);
        if (bar.Children.Count == before) bar.Children.Add(new TextBlock { Text = tip, Foreground = Muted, FontSize = 12, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) });
        // Leiste fest oben, Leinwand nimmt den Rest – auch bei wenig Platz liegt die Leinwand nie über der Leiste
        canvas.MinHeight = 160;
        var g = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(bar, Avalonia.Controls.Dock.Top); g.Children.Add(bar); g.Children.Add(canvas);
        return g;
    }

    private Control ImgProperties(ProgramDocument d)
    {
        var sp = new StackPanel { Spacing = 6 };
        Button B(string name, string label, Action a, bool primary = false) { var b = new Button { Name = name, Content = label, FontSize = 12, Margin = new Thickness(0, 0, 4, 4) }; if (primary) { b.Background = Accent; b.Foreground = Brushes.White; } b.Click += (_, _) => ProgramRun(BK, a); return b; }
        var path = new TextBox { Name = "img-file", PlaceholderText = "Bilddatei (JPG, PNG, WEBP) oder SVG" };
        sp.Children.Add(H("Bild hinzufügen", 13)); sp.Children.Add(path);
        sp.Children.Add(B("img-add", "Als Ebene hinzufügen", () => ImgAddImage((path.Text ?? "").Trim().Trim('"') is { Length: > 0 } f ? f : throw new InvalidOperationException("Bitte eine Bilddatei wählen.")), true));
        // Ebenen von oben nach unten
        sp.Children.Add(H("Ebenen", 13));
        var layers = ImageDoc.Layers(d.Data).OfType<JsonObject>().ToList(); var sel = SelId(d);
        if (layers.Count == 0) sp.Children.Add(P("Noch keine Ebene – Bild hinzufügen oder ein Werkzeug benutzen.", Muted, 12));
        for (var i = layers.Count - 1; i >= 0; i--)
        {
            var l = layers[i]; var id = l["id"].AsText() ?? ""; var vis = l["visible"].AsBool(true); var on = id == sel;
            var eye = new Button { Name = $"img-vis-{i}", Content = vis ? "👁" : "–", Width = 30, Padding = new Thickness(0), FontSize = 12 };
            ToolTip.SetTip(eye, vis ? "Ausblenden" : "Einblenden");
            eye.Click += (_, _) => ProgramRun(BK, () => d.Edit(vis ? "Ausblenden" : "Einblenden", x => ImageDoc.Layer(x, id)!["visible"] = !vis));
            var kind = ImageDoc.S(l["kind"]) switch { "image" => "🖼", "text" => "T", "shape" => "▭", "gradient" => "◩", "retouch" => "✚", _ => "🖌" };
            if (l["mask"] is not null) kind += "◐";
            var pick = new Button { Name = $"img-layer-{i}", Content = $"{kind}  {ImageDoc.S(l["name"])}", HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left, FontSize = 12, Padding = new Thickness(8, 4) };
            if (on) { pick.Background = Accent; pick.Foreground = Brushes.White; }
            pick.Click += (_, _) => { d.Data["selected"] = id; Show(BK); };
            sp.Children.Add(new DockPanel { Margin = new Thickness(0, 0, 0, 2), Children = { eye, pick } });
            DockPanel.SetDock(eye, Avalonia.Controls.Dock.Left);
        }
        // Gewählte Ebene
        if (sel is not null && ImageDoc.Layer(d.Data, sel) is { } L)
        {
            var k = ImageDoc.S(L["kind"]);
            sp.Children.Add(H("Gewählte Ebene", 13));
            var opacity = new Slider { Name = "img-opacity", Minimum = 0, Maximum = 100, Value = ImageDoc.N(L["opacity"], 1) * 100 };
            sp.Children.Add(new TextBlock { Text = $"Deckkraft {ImageDoc.N(L["opacity"], 1) * 100:0} %", Foreground = Muted, FontSize = 11.5 }); sp.Children.Add(opacity);
            var blend = new ComboBox { Name = "img-blend", ItemsSource = Trendo.Core.Media.ImageVector.BlendModes.Select(b => b.Label).ToList(), HorizontalAlignment = HorizontalAlignment.Stretch,
                SelectedIndex = Math.Max(0, Array.FindIndex(Trendo.Core.Media.ImageVector.BlendModes, b => b.Id == ImageDoc.S(L["blend"], "normal"))) };
            sp.Children.Add(new TextBlock { Text = "Mischmodus", Foreground = Muted, FontSize = 11.5 }); sp.Children.Add(blend);
            var apply = new List<Action<JsonObject>> { x => ImageDoc.Layer(x, sel)!["opacity"] = Math.Round(opacity.Value / 100, 2), x => ImageDoc.Layer(x, sel)!["blend"] = Trendo.Core.Media.ImageVector.BlendModes[Math.Max(0, blend.SelectedIndex)].Id };
            if (L["mask"] is JsonObject lm)
                sp.Children.Add(new WrapPanel { Children = {
                    B("img-mask-invert", lm["invert"]?.GetValue<bool>() == true ? "Maske normal" : "Maske umkehren", () => d.Edit("Maske umkehren", x => { var m = (JsonObject)ImageDoc.Layer(x, sel)!["mask"]!; m["invert"] = m["invert"]?.GetValue<bool>() != true; })),
                    B("img-mask-del", "Maske entfernen", () => d.Edit("Maske entfernen", x => ImageDoc.Layer(x, sel)!.Remove("mask"))) } });
            if (k == "path") apply.Add(x => ImageDoc.Layer(x, sel)!["stroke"] = ImgColor);
            if (k == "text")
            {
                var txt = new TextBox { Name = "img-text", Text = ImageDoc.S(L["text"]), AcceptsReturn = true, MinHeight = 50 };
                var sz = new TextBox { Name = "img-text-size", Text = ImageDoc.N(L["size"], 64).ToString("0"), Width = 70 };
                sp.Children.Add(txt); sp.Children.Add(new WrapPanel { Children = { new TextBlock { Text = "Größe ", Foreground = Muted, VerticalAlignment = VerticalAlignment.Center }, sz, new TextBlock { Text = " px · Farbe = gewählte Farbe oben", Foreground = Muted, FontSize = 11, VerticalAlignment = VerticalAlignment.Center } } });
                apply.Add(x => { var t = ImageDoc.Layer(x, sel)!; var s0 = double.TryParse(sz.Text, out var v) && v >= 6 ? v : 64; t["text"] = txt.Text ?? ""; t["size"] = s0; t["color"] = ImgColor;
                    t["w"] = Math.Round(Math.Max(20, (txt.Text ?? "").Split('\n').Max(r => r.Length) * s0 * 0.55)); t["h"] = Math.Round(s0 * 1.25 * Math.Max(1, (txt.Text ?? "").Split('\n').Length)); });
            }
            if (k == "shape") apply.Add(x => ImageDoc.Layer(x, sel)!["fill"] = ImgColor);
            if (k == "image")
            {
                var f = L["filter"] as JsonObject;
                Slider F(string key, string label, double min, double max)
                {
                    var s = new Slider { Name = "img-f-" + key, Minimum = min, Maximum = max, Value = f is null ? 0 : ImageDoc.N(f[key]) };
                    sp.Children.Add(new TextBlock { Text = label, Foreground = Muted, FontSize = 11.5 }); sp.Children.Add(s);
                    apply.Add(x => ImageDoc.SetFilter(ImageDoc.Layer(x, sel)!, key, Math.Round(s.Value)));
                    return s;
                }
                F("brightness", "Helligkeit", -100, 100); F("contrast", "Kontrast", -100, 100); F("saturation", "Sättigung", -100, 100); F("blur", "Unschärfe", 0, 50);
                CheckBox C(string key, string label) { var c = new CheckBox { Name = "img-f-" + key, Content = label, IsChecked = f is not null && ImageDoc.N(f[key]) > 0, Foreground = Text }; apply.Add(x => ImageDoc.SetFilter(ImageDoc.Layer(x, sel)!, key, c.IsChecked == true ? 1 : 0)); return c; }
                sp.Children.Add(new WrapPanel { Children = { C("grayscale", "Schwarzweiß"), C("sepia", "Sepia"), C("invert", "Invertieren") } });
                // Tonwerte und Kurven (Photoshop/GIMP)
                sp.Children.Add(H("Tonwerte und Kurve", 12));
                Slider T(string key, string label, double min, double max, double def, double step)
                {
                    var s = new Slider { Name = "img-f-" + key, Minimum = min, Maximum = max, Value = f?[key] is null ? def : ImageDoc.N(f[key], def), SmallChange = step };
                    sp.Children.Add(new TextBlock { Text = label, Foreground = Muted, FontSize = 11.5 }); sp.Children.Add(s);
                    apply.Add(x => ImageDoc.SetFilter(ImageDoc.Layer(x, sel)!, key, step < 1 ? Math.Round(s.Value, 2) : Math.Round(s.Value)));
                    return s;
                }
                var blk = T("black", "Schwarzpunkt", 0, 200, 0, 1); var wht = T("white", "Weißpunkt", 55, 255, 255, 1); T("gamma", "Mitteltöne (Gamma)", 0.3, 3, 1, 0.01);
                var file = ImageDoc.S(L["file"]);
                var auto = new Button { Name = "img-auto-levels", Content = "Auto-Tonwerte", FontSize = 12, Margin = new Thickness(0, 2, 4, 4) };
                auto.Click += (_, _) => ProgramRun(BK, () => { var (b0, w0) = ImageRender.AutoLevels(file); d.Edit("Auto-Tonwerte", x => { var t = ImageDoc.Layer(x, sel)!; ImageDoc.SetFilter(t, "black", b0); ImageDoc.SetFilter(t, "white", w0); }); _progMsg[BK] = $"Tonwerte: Schwarz {b0}, Weiß {w0}."; });
                sp.Children.Add(auto);
                var curNow = (f?["curve"] as JsonArray)?.Select(n => ImageDoc.N(n)).ToArray() ?? [];
                var curve = new ComboBox { Name = "img-curve", ItemsSource = ImageOps.CurvePresets.Select(c => c.Label).ToList(), HorizontalAlignment = HorizontalAlignment.Stretch,
                    SelectedIndex = Math.Max(0, Array.FindIndex(ImageOps.CurvePresets, c => c.Points.SequenceEqual(curNow))) };
                sp.Children.Add(new TextBlock { Text = "Kurve – Punkte ziehen, Klick setzt einen Punkt, Rechtsklick entfernt ihn", Foreground = Muted, FontSize = 11.5, TextWrapping = TextWrapping.Wrap }); sp.Children.Add(curve);
                var ce = new CurveEditor { Name = "img-curve-editor", Points = curNow, Histogram = ImageRender.Histogram(file), HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 4, 0, 4) };
                curve.SelectionChanged += (_, _) => { if (curve.SelectedIndex >= 0) ce.Points = ImageOps.CurvePresets[curve.SelectedIndex].Points; };
                sp.Children.Add(ce);
                apply.Add(x => ImageDoc.SetCurve(ImageDoc.Layer(x, sel)!, ce.Points));
            }
            sp.Children.Add(B("img-apply", "Übernehmen", () => d.Edit("Ebene ändern", x => { foreach (var a in apply) a(x); }), true));
        }
        sp.Children.Add(H("Leinwand", 13));
        sp.Children.Add(P($"{(int)d.Number("width")} × {(int)d.Number("height")} px · Hintergrund {d.Text("background")}", Muted, 12));
        return sp;
    }

    /// <summary>Zauberstab an (x, y): zusammenhängende Fläche ähnlicher Farbe im sichtbaren Gesamtbild auswählen.</summary>
    public void ImgWandAt(ProgramDocument d, double x, double y)
    {
        int w = (int)d.Number("width", 800), h = (int)d.Number("height", 600);
        var bits = ImageOps.FloodMask(ImageRender.CompositeRgba(d.Data), w, h, (int)x, (int)y, ImgFillTolerance);
        var n = bits.Count(b => b); if (n == 0) throw new InvalidOperationException("Hier ist nichts auszuwählen – auf das Bild klicken.");
        ImgClearSelection(); ImgWand = (bits, w, h);
        int x0 = w, y0 = h, x1 = 0, y1 = 0;
        for (var i = 0; i < bits.Length; i++) if (bits[i]) { int px = i % w, py = i / w; x0 = Math.Min(x0, px); y0 = Math.Min(y0, py); x1 = Math.Max(x1, px); y1 = Math.Max(y1, py); }
        ImgMarquee = new Rect(x0, y0, x1 - x0 + 1, y1 - y0 + 1);
        _progMsg[BK] = $"Zauberstab: {n:N0} Pixel ausgewählt ({100.0 * n / (w * h):0.#} % des Bildes).";
    }

}
