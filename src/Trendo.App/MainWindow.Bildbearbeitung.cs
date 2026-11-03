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
    public double ImgZoom { get; set; }
    public ImageCanvas? ImgCanvasForTests { get; private set; }
    static readonly (string Id, string Label, string Tip)[] ImgTools =
    [
        ("move", "✥ Verschieben", "Ebene anklicken wählt sie, Ziehen verschiebt sie"), ("brush", "🖌 Pinsel", "Auf einer Mal-Ebene malen (wird bei Bedarf angelegt)"),
        ("eraser", "◌ Radierer", "Auf der Mal-Ebene radieren"), ("text", "T Text", "Klick setzt einen Text"), ("rect", "▭ Rechteck", "Ziehen zeichnet ein Rechteck"),
        ("ellipse", "◯ Ellipse", "Ziehen zeichnet eine Ellipse"), ("crop", "⛶ Zuschneiden", "Ziehen wählt den Ausschnitt der Leinwand"),
    ];
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

    /// <summary>Bild als Ebene hinzufügen (öffentlich für Tests und die KI).</summary>
    public void ImgAddImage(string file)
    {
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
        var canvas = new ImageCanvas { Name = "img-canvas", DocW = (int)d.Number("width", 800), DocH = (int)d.Number("height", 600), Zoom = ImgZoom, Tool = ImgTool, BrushColor = Color.Parse(ImgColor), BrushSize = ImgBrush,
            Image = new Bitmap(new MemoryStream(png)), MinHeight = 240 };
        ImgCanvasForTests = canvas;
        if (SelId(d) is { } sid && ImageDoc.Layer(d.Data, sid) is { } sl && ImageDoc.S(sl["kind"]) != "paint")
            canvas.Selection = new Rect(ImageDoc.N(sl["x"]), ImageDoc.N(sl["y"]), ImageDoc.N(sl["w"]), ImageDoc.N(sl["h"]));
        canvas.Clicked = p => ProgramRun(BK, () =>
        {
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
        canvas.Stroke = (pts, erase) => ProgramRun(BK, () => d.Edit(erase ? "Radieren" : "Malen", x =>
        {
            var target = SelId(d) is { } s0 && ImageDoc.S(ImageDoc.Layer(x, s0)!["kind"]) == "paint" ? ImageDoc.Layer(x, s0)! : ImageDoc.Layers(x).OfType<JsonObject>().LastOrDefault(l => ImageDoc.S(l["kind"]) == "paint") ?? ImageDoc.AddPaint(x);
            x["selected"] = (string?)target["id"];
            ImageDoc.AddStroke(target, pts, ImgColor, ImgBrush, erase);
        }));
        canvas.Dragged = r => ProgramRun(BK, () =>
        {
            switch (ImgTool)
            {
                case "rect" or "ellipse": d.Edit(ImgTool == "rect" ? "Rechteck" : "Ellipse", x => ImageDoc.AddShape(x, ImgTool, r.X, r.Y, r.Width, r.Height, ImgColor)); break;
                case "crop": d.Edit("Zuschneiden", x => ImageDoc.Crop(x, r.X, r.Y, r.Width, r.Height)); _progMsg[BK] = $"Zugeschnitten auf {(int)d.Number("width")} × {(int)d.Number("height")} px."; break;
            }
        });
        // Farbe und Pinselgröße direkt über der Fläche
        var bar = new WrapPanel { Name = "img-toolbar", Margin = new Thickness(0, 0, 0, 6) };
        foreach (var c in ImgColors)
        {
            var cc = c; var on = string.Equals(ImgColor, c, StringComparison.OrdinalIgnoreCase);
            var b = new Button { Name = "img-color-" + c.TrimStart('#').ToLowerInvariant(), Width = 24, Height = 24, Padding = new Thickness(0), Margin = new Thickness(0, 0, 4, 0), CornerRadius = new CornerRadius(12), Background = Brush.Parse(c), BorderBrush = Brush.Parse(on ? "#FACC15" : "#55FFFFFF"), BorderThickness = new Thickness(on ? 3 : 1) };
            ToolTip.SetTip(b, "Farbe " + c); b.Click += (_, _) => { ImgColor = cc; Show(BK); };
            bar.Children.Add(b);
        }
        var size = new Slider { Name = "img-brush-size", Minimum = 1, Maximum = 120, Value = ImgBrush, Width = 160, Margin = new Thickness(12, 0, 6, 0) };
        size.PropertyChanged += (_, e) => { if (e.Property == Slider.ValueProperty) { ImgBrush = Math.Round(size.Value); canvas.BrushSize = ImgBrush; } };
        bar.Children.Add(new TextBlock { Text = "Pinsel", Foreground = Muted, FontSize = 12, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 0) });
        bar.Children.Add(size);
        bar.Children.Add(new TextBlock { Text = ImgTools.First(t => t.Id == ImgTool).Tip, Foreground = Muted, FontSize = 12, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) });
        var g = new Grid { RowDefinitions = new RowDefinitions("Auto,*") };
        g.Children.Add(bar); Grid.SetRow(canvas, 1); g.Children.Add(canvas);
        return g;
    }

    private Control ImgProperties(ProgramDocument d)
    {
        var sp = new StackPanel { Spacing = 6 };
        Button B(string name, string label, Action a, bool primary = false) { var b = new Button { Name = name, Content = label, FontSize = 12, Margin = new Thickness(0, 0, 4, 4) }; if (primary) { b.Background = Accent; b.Foreground = Brushes.White; } b.Click += (_, _) => ProgramRun(BK, a); return b; }
        var path = new TextBox { Name = "img-file", PlaceholderText = "Bilddatei (JPG, PNG, WEBP)" };
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
            var kind = ImageDoc.S(l["kind"]) switch { "image" => "🖼", "text" => "T", "shape" => "▭", _ => "🖌" };
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
            var apply = new List<Action<JsonObject>> { x => ImageDoc.Layer(x, sel)!["opacity"] = Math.Round(opacity.Value / 100, 2) };
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
            }
            sp.Children.Add(B("img-apply", "Übernehmen", () => d.Edit("Ebene ändern", x => { foreach (var a in apply) a(x); }), true));
        }
        sp.Children.Add(H("Leinwand", 13));
        sp.Children.Add(P($"{(int)d.Number("width")} × {(int)d.Number("height")} px · Hintergrund {d.Text("background")}", Muted, 12));
        return sp;
    }
}
