using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using SkiaSharp;
using Trendo.App;
using Trendo.Core.Media;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// <summary>Masterprompt 131 (Beispiel Bildbearbeitung): Bild öffnen, auf der Fläche bearbeiten, Ebenen, Filter, speichern – mit Pixelprüfung.</summary>
public class Bildbearbeitung_Ui
{
    const string BK = "bild";
    static Point P(MainWindow w, double x, double y) { Settle(); var c = w.ImgCanvasForTests!; return c.TranslatePoint(c.ToScreen(new Point(x, y)), w)!.Value; }
    static void Drag(MainWindow w, Point a, Point b) { w.MouseDown(a, MouseButton.Left); for (var i = 1; i <= 6; i++) { w.MouseMove(a + (b - a) * (i / 6.0)); Settle(); } w.MouseUp(b, MouseButton.Left); Settle(); }
    static void Click(MainWindow w, string name) { var b = V<Button>(w, name); b.BringIntoView(); Settle(); RealClick(w, V<Button>(w, name)); }
    static SKColor Px(MainWindow w, int x, int y) { using var b = ImageRender.Render(w.ProgramActive(BK)!.Data); return b.GetPixel(x, y); }

    [AvaloniaFact(DisplayName = "Bildbearbeitung UI: Bild als Ebene, Pinsel malt (Pixel), Rechteck aufziehen, Text setzen, Ebene verschieben, Filter Schwarzweiß, Zuschneiden, Rückgängig, Export PNG")]
    public void Edit_image()
    {
        var (w, _, root) = Ui.Open(); w.Width = 1600; w.Height = 1000; Directory.CreateDirectory(root);
        // Testbild 400 × 200: links rot, rechts blau
        var src = Path.Combine(root, "foto.png");
        using (var bmp = new SKBitmap(400, 200)) { using (var c = new SKCanvas(bmp)) { c.Clear(SKColors.Red); c.DrawRect(200, 0, 200, 200, new SKPaint { Color = SKColors.Blue }); } using var img = SKImage.FromBitmap(bmp); using var data = img.Encode(SKEncodedImageFormat.Png, 100); File.WriteAllBytes(src, data.ToArray()); }
        w.Show(BK); Settle(); RealClick(w, V<Button>(w, "prog-tpl-leer"));
        w.ImgAddImage(src); w.Show(BK); Settle();
        var d = w.ProgramActive(BK)!; Assert.Equal((400, 200), ((int)d.Number("width"), (int)d.Number("height"))); // erstes Bild bestimmt die Leinwand
        Assert.Equal(SKColors.Red, Px(w, 50, 100));
        // Pinsel: grüner Strich quer über die rote Hälfte
        Click(w, "prog-cmd-img-tool-brush"); Click(w, "img-color-22c55e");
        Drag(w, P(w, 20, 100), P(w, 180, 100));
        var g = Px(w, 100, 100); Assert.True(g.Green > 150 && g.Red < 100, $"kein Grün: {g}");
        Assert.Equal("Malen", w.ProgramActive(BK)!.UndoLabel);
        // Rechteck oben rechts aufziehen (gelb)
        Click(w, "prog-cmd-img-tool-rect"); Click(w, "img-color-facc15");
        Drag(w, P(w, 300, 20), P(w, 380, 80));
        var y = Px(w, 340, 50); Assert.True(y.Red > 200 && y.Green > 180 && y.Blue < 80, $"kein Gelb: {y}");
        // Text per Klick
        Click(w, "prog-cmd-img-tool-text"); var tp = P(w, 220, 120); w.MouseDown(tp, MouseButton.Left); w.MouseUp(tp, MouseButton.Left); Settle();
        Assert.Contains(ImageDoc.Layers(w.ProgramActive(BK)!.Data).OfType<System.Text.Json.Nodes.JsonObject>(), l => ImageDoc.S(l["kind"]) == "text");
        // Rechteck verschieben (anklicken + ziehen)
        Click(w, "prog-cmd-img-tool-move");
        Drag(w, P(w, 340, 50), P(w, 340, 150));
        var rect = ImageDoc.Layers(w.ProgramActive(BK)!.Data).OfType<System.Text.Json.Nodes.JsonObject>().First(l => ImageDoc.S(l["kind"]) == "shape");
        Assert.InRange(ImageDoc.N(rect["y"]), 115, 125);
        // Bild-Ebene auswählen, Schwarzweiß
        var imgIdx = 0; Click(w, $"img-layer-{imgIdx}");
        w.Find<CheckBox>("img-f-grayscale").IsChecked = true; Click(w, "img-apply");
        var gr = Px(w, 300, 190); Assert.True(Math.Abs(gr.Red - gr.Blue) < 6 && Math.Abs(gr.Green - gr.Blue) < 6, $"nicht grau: {gr}");
        // Zuschneiden: rechte Hälfte
        Click(w, "prog-cmd-img-tool-crop"); Drag(w, P(w, 200, 0), P(w, 400, 200));
        d = w.ProgramActive(BK)!; Assert.Equal((200, 200), ((int)d.Number("width"), (int)d.Number("height")));
        // Rückgängig: Zuschnitt zurück
        RealClick(w, V<Button>(w, "prog-undo")); Assert.Equal(400, (int)w.ProgramActive(BK)!.Number("width"));
        // Export PNG: echte Datei, gleiche Größe
        Click(w, "prog-export-png");
        var outFile = w.ProgramMessage(BK)["Exportiert: ".Length..];
        using var exported = SKBitmap.Decode(outFile); Assert.Equal((400, 200), (exported.Width, exported.Height));
    }

    [Fact(DisplayName = "Bild-Dokument: Ebenen-Reihenfolge, Treffer oberste Ebene, Zuschnitt verschiebt Ebenen und Striche, Filter-Grenzen, Farbmatrix Invertieren")]
    public void Doc_ops()
    {
        var d = ImageDoc.New(100, 100);
        var a = ImageDoc.AddShape(d, "rect", 10, 10, 50, 50); var b = ImageDoc.AddShape(d, "ellipse", 30, 30, 50, 50);
        Assert.Equal((string?)b["id"], ImageDoc.HitTest(d, 40, 40)); Assert.Equal((string?)a["id"], ImageDoc.HitTest(d, 15, 15)); Assert.Null(ImageDoc.HitTest(d, 95, 5));
        ImageDoc.Reorder(d, (string)b["id"]!, -1); Assert.Equal((string?)a["id"], ImageDoc.HitTest(d, 40, 40));
        var p = ImageDoc.AddPaint(d); ImageDoc.AddStroke(p, [(20, 20), (60, 60)], "#000000", 4, false);
        ImageDoc.Crop(d, 10, 10, 60, 60);
        Assert.Equal((60, 60), ((int)ImageDoc.N(d["width"]), (int)ImageDoc.N(d["height"])));
        Assert.Equal((0.0, 0.0), (ImageDoc.N(a["x"]), ImageDoc.N(a["y"]))); Assert.Equal(10, ImageDoc.N(((System.Text.Json.Nodes.JsonArray)((System.Text.Json.Nodes.JsonObject)((System.Text.Json.Nodes.JsonArray)p["strokes"]!)[0]!)["points"]!)[0]));
        ImageDoc.SetFilter(a, "blur", 999); Assert.Equal(50, ImageDoc.N(((System.Text.Json.Nodes.JsonObject)a["filter"]!)["blur"]));
        Assert.Throws<ArgumentException>(() => ImageDoc.SetFilter(a, "zauber", 1));
        Assert.Throws<InvalidOperationException>(() => ImageDoc.Crop(d, 0, 0, 2, 2));
        var inv = ImageDoc.ColorMatrix(new System.Text.Json.Nodes.JsonObject { ["invert"] = 1 });
        Assert.Equal((-1f, 255f), (inv[0], inv[4]));
    }

    [AvaloniaFact(DisplayName = "Bildbearbeitung wie Krita/Inkscape: Vektorpinsel ergibt glatten Pfad, Mischmodus je Ebene, Pinsel-Deckkraft, SVG speichern und wieder öffnen")]
    public void Vector_and_svg()
    {
        var (w, _, root) = Ui.Open(); w.Width = 1600; w.Height = 1000;
        try
        {
            w.Show("bild"); var d = w.ProgramNew("bild"); w.Show("bild"); Settle();
            w.ImgTool = "vector"; w.Show("bild"); Settle();
            var pts = Enumerable.Range(0, 40).Select(i => (X: 50.0 + i * 8, Y: 200 + Math.Sin(i / 4.0) * 40)).ToList();
            w.ImgCanvasForTests!.Stroke!(pts, false); Settle();
            var layer = ImageDoc.Layers(d.Data).OfType<System.Text.Json.Nodes.JsonObject>().Last();
            Assert.Equal("path", ImageDoc.S(layer["kind"]));
            Assert.True(((System.Text.Json.Nodes.JsonArray)layer["points"]!).Count < pts.Count * 2); // vereinfacht
            w.Show("bild"); Settle();
            w.Find<ComboBox>("img-blend").SelectedIndex = 1; // Multiplizieren
            w.Find<Button>("img-apply").Press(); Settle();
            Assert.Equal("multiply", ImageDoc.S(ImageDoc.Layers(w.ProgramActive("bild")!.Data).OfType<System.Text.Json.Nodes.JsonObject>().Last()["blend"]));
            w.ImgTool = "brush"; w.ImgBrushAlpha = 0.4; w.Show("bild"); Settle();
            w.ImgCanvasForTests!.Stroke!([(10, 10), (60, 60)], false); Settle();
            var paint = ImageDoc.Layers(w.ProgramActive("bild")!.Data).OfType<System.Text.Json.Nodes.JsonObject>().First(l => ImageDoc.S(l["kind"]) == "paint");
            Assert.Equal(0.4, ImageDoc.N(((System.Text.Json.Nodes.JsonArray)paint["strokes"]!)[0]!["alpha"]), 2);
            var svg = Path.Combine(root, "zeichnung.svg"); File.WriteAllText(svg, ImageVector.ToSvg(w.ProgramActive("bild")!.Data));
            Assert.Contains("<path", File.ReadAllText(svg));
            Assert.True(w.ImgOpenSvg(svg) >= 2);
            Assert.Contains(ImageDoc.Layers(w.ProgramActive("bild")!.Data).OfType<System.Text.Json.Nodes.JsonObject>(), l => ImageDoc.S(l["kind"]) == "path");
        }
        finally { w.Close(); }
    }
    [AvaloniaFact(DisplayName = "Bildbearbeitung wie Photoshop/GIMP: Füllen mit Toleranz, Reparieren entfernt Fleck, Klonen mit Quelle, Verlauf ziehen, Maske aus Auswahl, Tonwerte, Kurve, Auto-Tonwerte")]
    public void Retouch_and_tones()
    {
        var (w, _, root) = Ui.Open(); w.Width = 1600; w.Height = 1000; Directory.CreateDirectory(root);
        try
        {
            // Foto 300 × 150: grau (120), links ein schwarzer Fleck, rechts eine weiße Fläche
            var src = Path.Combine(root, "foto.png");
            using (var bmp = new SKBitmap(300, 150))
            {
                using (var c = new SKCanvas(bmp)) { c.Clear(new SKColor(120, 120, 120)); c.DrawCircle(50, 75, 8, new SKPaint { Color = SKColors.Black, IsAntialias = false }); c.DrawRect(200, 0, 100, 150, new SKPaint { Color = SKColors.White }); }
                using var img = SKImage.FromBitmap(bmp); using var data = img.Encode(SKEncodedImageFormat.Png, 100); File.WriteAllBytes(src, data.ToArray());
            }
            w.Show(BK); Settle(); RealClick(w, V<Button>(w, "prog-tpl-leer")); w.ImgAddImage(src); w.Show(BK); Settle();
            // Füllen: Klick in die weiße Fläche färbt nur sie blau
            Click(w, "prog-cmd-img-tool-fill"); Click(w, "img-color-2f86f6");
            var fp = P(w, 250, 75); w.MouseDown(fp, MouseButton.Left); w.MouseUp(fp, MouseButton.Left); Settle();
            Assert.Equal("Füllen", w.ProgramActive(BK)!.UndoLabel);
            var b = Px(w, 250, 20); Assert.True(b.Blue > 200 && b.Red < 80, $"nicht blau: {b}");
            Assert.Equal(new SKColor(120, 120, 120), Px(w, 150, 20));
            // Reparieren: über den schwarzen Fleck malen
            Click(w, "prog-cmd-img-tool-heal"); w.ImgBrush = 24; w.Show(BK); Settle();
            Drag(w, P(w, 46, 75), P(w, 54, 75));
            var h = Px(w, 50, 75); Assert.True(Math.Abs(h.Red - 120) < 10, $"Fleck noch da: {h}");
            // Klonen: Quelle in der blauen Fläche, dann oben links übermalen
            Click(w, "prog-cmd-img-tool-clone"); var q = P(w, 260, 40); w.MouseDown(q, MouseButton.Left); w.MouseUp(q, MouseButton.Left); Settle();
            Assert.Contains("Quelle", w.ProgramMessage(BK)); Assert.NotNull(w.ImgCloneSrc);
            Drag(w, P(w, 20, 20), P(w, 40, 20));
            var k = Px(w, 30, 20); Assert.True(k.Blue > 200 && k.Red < 80, $"nicht geklont: {k}");
            Assert.Single(ImageDoc.Layers(w.ProgramActive(BK)!.Data).OfType<System.Text.Json.Nodes.JsonObject>(), l => ImageDoc.S(l["kind"]) == "retouch"); // eine Retusche-Ebene für alle Schritte
            // Verlauf: von links rot nach rechts durchsichtig
            Click(w, "prog-cmd-img-tool-gradient"); Click(w, "img-color-e0475b"); Drag(w, P(w, 0, 110), P(w, 300, 110));
            var g0 = Px(w, 5, 110); var g1 = Px(w, 150, 110); Assert.True(g0.Red > 200 && g0.Green < 100, $"Verlauf links: {g0}"); Assert.True(g1.Red > g1.Green + 20 && g1.Red < g0.Red + 1 && g1.Green > g0.Green, $"Verlauf Mitte: {g1}");
            // Maske: Auswahl links, Verlauf nur dort sichtbar
            Click(w, "prog-cmd-img-tool-select"); Drag(w, P(w, 0, 0), P(w, 100, 150));
            Assert.NotNull(w.ImgMarquee); w.ImgBrush = 0; w.Show(BK); Settle(); Click(w, "img-mask-sel"); Settle();
            Assert.Equal("Maske", w.ProgramActive(BK)!.UndoLabel);
            Assert.True(Px(w, 50, 110).Red > 180); Assert.Equal(new SKColor(120, 120, 120), Px(w, 150, 110));
            Click(w, "img-mask-invert"); Settle();
            Assert.Equal(new SKColor(120, 120, 120), Px(w, 50, 110)); Assert.True(Px(w, 150, 110).Red > 125);
            // Tonwerte auf der Foto-Ebene: (unter der Maske, ohne Verlauf) Gamma hellt das Grau auf, Kurve „Abdunkeln“ dunkelt es
            Click(w, "img-layer-0"); Settle();
            w.Find<Slider>("img-f-gamma").Value = 2; Click(w, "img-apply");
            Assert.True(Px(w, 80, 75).Red > 160, $"Gamma: {Px(w, 80, 75)}");
            w.Find<Slider>("img-f-gamma").Value = 1; w.Find<ComboBox>("img-curve").SelectedIndex = 3; Click(w, "img-apply");
            Assert.True(Px(w, 80, 75).Red < 110, $"Kurve: {Px(w, 80, 75)}");
            Click(w, "img-auto-levels");
            Assert.Contains("Tonwerte: Schwarz", w.ProgramMessage(BK));
            var f = (System.Text.Json.Nodes.JsonObject)ImageDoc.Layers(w.ProgramActive(BK)!.Data)[0]!["filter"]!;
            Assert.InRange(ImageDoc.N(f["black"]), 0, 120); Assert.Equal(255, ImageDoc.N(f["white"])); // der winzige schwarze Fleck liegt unter 0,5 % und wird wie in Photoshop abgeschnitten
            // Rückgängig nimmt Schritt für Schritt zurück, Original bleibt unverändert
            RealClick(w, V<Button>(w, "prog-undo")); Settle();
            using var o = SKBitmap.Decode(src); Assert.Equal(SKColors.Black, o.GetPixel(50, 75));
        }
        finally { w.Close(); }
    }
}
