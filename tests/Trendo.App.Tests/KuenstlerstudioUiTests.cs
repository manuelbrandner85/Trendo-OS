using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Trendo.App;
using Trendo.Core;
using Trendo.Core.Kreativ;
using Trendo.Core.Programs;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// <summary>Künstlerstudio (STUDIO-ART-01, K1–K8): echte Klicks und Striche auf der Malfläche, Ebenen, Farben, Zeichenhilfen, Dateien, Zeitraffer, Kurs.</summary>
public class Kuenstlerstudio_Ui
{
    const string K = MainWindow.ArtKey;
    static void Tap(MainWindow w, string name) { var b = w.Find<Button>(name); b.BringIntoView(); Settle(); b.Press(); Settle(); }
    static string Export(MainWindow w, string name, string root)
    {
        var e = MainWindow.Programs[K].Exports.Single(x => x.Name == name);
        return e.Write(w.ProgramActive(K)!, Path.Combine(root, "export-" + name + e.Extension));
    }
    static ProgramDocument Open(MainWindow w, string tpl = "quer")
    {
        w.ProgramSetLevel(UserLevel.Profi); w.Show(K); Settle();
        RealClick(w, w.Find<Button>("prog-tpl-" + tpl)); Settle();
        return w.ProgramActive(K)!;
    }

    [AvaloniaFact(DisplayName = "Künstlerstudio: eigene Seite im Studio, Vorlage öffnet eine Leinwand mit Ebene „Skizze“")]
    public void Page()
    {
        var (w, _, _) = Ui.Open();
        try
        {
            Assert.Equal(K, MainWindow.StudioPages["STUDIO-ART-01"]);
            var d = Open(w, "skizze");
            var m = w.ArtModel(d);
            Assert.Equal((1240, 1754), (m.Doc.W, m.Doc.H)); Assert.Equal("Skizze", m.Doc.Layers.Single().Name);
            Assert.NotNull(w.Find<PaintCanvas>("art-canvas")); Assert.NotNull(w.Find<HsvWheel>("art-wheel"));
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "K1: Mit der Maus auf der Fläche malen färbt das Bild; Rückgängig nimmt den Strich zurück; gesperrte Ebene meldet sich")]
    public void Paint_with_mouse_and_undo()
    {
        var (w, _, _) = Ui.Open(); w.Width = 1500; w.Height = 950;
        try
        {
            var d = Open(w);
            Tap(w, "prog-cmd-art-brush-tusche"); Assert.Equal("tusche", w.ArtBrush);
            w.ArtColor = "#E01010"; w.ArtSize = 20; w.ArtSmooth = 0; w.Show(K); Settle();
            var c = w.Find<PaintCanvas>("art-canvas"); Settle();
            var a = c.TranslatePoint(new Point(c.Bounds.Width * 0.3, c.Bounds.Height * 0.5), w)!.Value;
            var b = c.TranslatePoint(new Point(c.Bounds.Width * 0.7, c.Bounds.Height * 0.5), w)!.Value;
            w.MouseDown(a, MouseButton.Left); for (var i = 1; i <= 10; i++) w.MouseMove(a + (b - a) * (i / 10.0)); w.MouseUp(b, MouseButton.Left); Settle();
            Assert.Equal(1, (int)d.Number("strokes"));
            var mid = w.ArtModel(d).Comp.At(960, 540); Assert.Equal((224, 16, 16), (mid.R, mid.G, mid.B));
            Assert.Contains("#E01010", w.ArtRecent);
            Assert.True(d.Undo()); Assert.Equal((255, 255, 255), (w.ArtModel(d).Comp.At(960, 540).R, w.ArtModel(d).Comp.At(960, 540).G, w.ArtModel(d).Comp.At(960, 540).B));
            // Pipette nimmt die Farbe des Hintergrunds
            Assert.Equal("#FFFFFF", w.ArtPick(d, 10, 10));
            // gesperrte Ebene
            Tap(w, "art-lock-0"); Assert.True(w.ArtModel(d).Doc.Layers[0].Locked);
            var e = Assert.Throws<InvalidOperationException>(() => w.ArtPaint(d, [(100, 100, 1), (200, 100, 1)]));
            Assert.Contains("gesperrt", e.Message);
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "K1: Ebenen über Menüband und Liste – anlegen, Mischart, Deckkraft sperren, verbinden; Export PNG, ORA, PSD")]
    public void Layers_and_files()
    {
        var (w, _, root) = Ui.Open(); Directory.CreateDirectory(root);
        try
        {
            var d = Open(w);
            w.ArtBrush = "tusche"; w.ArtSize = 30; w.ArtColor = "#000000"; w.ArtPaint(d, [(400, 400, 1), (800, 400, 1)]);
            Tap(w, "prog-cmd-art-layer-add");
            var m = w.ArtModel(d); Assert.Equal(2, m.Doc.Layers.Count); Assert.Equal(m.Doc.Layers[1].Id, d.Text("active"));
            var blend = w.Find<ComboBox>("art-layer-blend"); blend.SelectedIndex = 1; Settle(); // Multiplizieren
            Assert.Equal("multiply", w.ArtModel(d).Doc.Layers[1].Blend);
            w.ArtColor = "#FF0000"; w.ArtPaint(d, [(600, 300, 1), (600, 500, 1)]);
            Assert.Equal((0, 0, 0), (w.ArtModel(d).Comp.At(600, 400).R, w.ArtModel(d).Comp.At(600, 400).G, w.ArtModel(d).Comp.At(600, 400).B)); // Rot × Schwarz = Schwarz
            Assert.Equal(255, w.ArtModel(d).Comp.At(600, 320).R);
            var ora = Export(w, "ora", root); var back = PaintFiles.LoadOra(ora);
            Assert.Equal(["Skizze", "Ebene 2"], back.Layers.Select(l => l.Name)); Assert.Equal("multiply", back.Layers[1].Blend);
            Assert.Equal("8BPS", System.Text.Encoding.ASCII.GetString(File.ReadAllBytes(Export(w, "psd", root)), 0, 4));
            var png = PngCodec.Decode(File.ReadAllBytes(Export(w, "png", root))); Assert.Equal((1920, 1080), (png.W, png.H));
            var a4 = PngCodec.Decode(File.ReadAllBytes(Export(w, "druck-a4", root))); Assert.Equal((2551, 3579), (a4.W, a4.H));
            Tap(w, "prog-cmd-art-layer-merge"); Assert.Single(w.ArtModel(d).Doc.Layers);
            // ORA wieder öffnen → neues Dokument mit Ebenen
            var d2 = w.ArtOpen(ora); Assert.Equal(2, w.ArtModel(d2).Doc.Layers.Count);
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "K2/K3: Farbe per Hex und Palette, Harmonie übernehmen; Spiegeln radial und Perspektive mit Punkten per Klick")]
    public void Colors_and_assists()
    {
        var (w, _, _) = Ui.Open(); w.Width = 1500; w.Height = 950;
        try
        {
            var d = Open(w, "quadrat");
            var hex = w.Find<TextBox>("art-hex"); hex.Text = "#2F86F6"; Tap(w, "art-hex-ok"); Assert.Equal("#2F86F6", w.ArtColor);
            Tap(w, "art-pal-add"); Tap(w, "art-pal-harm");
            Assert.Contains("#2F86F6", d.List("palette").Select(n => (string?)n));
            Assert.True(d.List("palette").Count >= 2);
            Tap(w, "art-pal-" + d.List("palette")[^1]!.ToString().TrimStart('#').ToLowerInvariant()); Assert.NotEqual("#2F86F6", w.ArtColor);
            // Spiegeln radial: ein Strich erscheint 8-mal
            w.Find<ComboBox>("art-sym").SelectedIndex = 4; Settle();
            Assert.Equal("radial", d.Data["sym"]!["mode"]!.ToString());
            w.ArtBrush = "tusche"; w.ArtSize = 10; w.ArtColor = "#000000"; w.ArtSmooth = 0;
            w.ArtPaint(d, [(800, 300, 1), (800, 310, 1)]);
            var comp = w.ArtModel(d).Comp; Assert.True(comp.At(800, 305).R < 50 && comp.At(800, 1295).R < 50 && comp.At(305, 800).R < 50);
            // Perspektive 1 Punkt: Klick setzt den Fluchtpunkt
            w.Find<ComboBox>("art-assist").SelectedIndex = 3; Settle();
            Assert.Equal("points", w.ArtMode);
            var c = w.Find<PaintCanvas>("art-canvas"); Settle();
            var p = c.TranslatePoint(new Point(c.Bounds.Width / 2, c.Bounds.Height / 2), w)!.Value;
            w.MouseDown(p, MouseButton.Left); w.MouseUp(p, MouseButton.Left); Settle();
            Assert.Equal("paint", w.ArtMode);
            Assert.Single(d.Data["assist"]!["pts"]!.AsArray());
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "K4–K6: Referenz mit Farben, Posenpuppe als gesperrte Ebene, Skizze säubern und Vorfärben; Ideenskizze nur als Referenz")]
    public async Task Reference_puppet_sketch()
    {
        var (w, _, root) = Ui.Open(); Directory.CreateDirectory(root);
        MainWindow.ImageAiOverride = (prompt, seed, png, wd, ht) => { File.WriteAllBytes(png, PngCodec.Encode(new Pixmap(wd, ht))); return Task.FromResult(true); };
        try
        {
            var d = Open(w, "quadrat");
            var photo = Path.Combine(root, "foto.png"); var img = new Pixmap(60, 20);
            for (var x = 0; x < 60; x++) for (var y = 0; y < 20; y++) { var i = (y * 60 + x) * 4; img.Px[i] = (byte)(x < 30 ? 200 : 20); img.Px[i + 1] = 40; img.Px[i + 2] = (byte)(x < 30 ? 30 : 180); img.Px[i + 3] = 255; }
            File.WriteAllBytes(photo, PngCodec.Encode(img));
            w.ArtAddReference(d, photo); w.Show(K); Settle();
            Assert.NotNull(w.Find<Image>("art-ref-image"));
            Tap(w, "art-pal-ref"); Assert.Contains("#C8281E", d.List("palette").Select(n => (string?)n)); Assert.Contains("#1428B4", d.List("palette").Select(n => (string?)n));
            Tap(w, "art-puppet-add");
            var m = w.ArtModel(d); Assert.StartsWith("Posenpuppe", m.Doc.Layers[0].Name); Assert.True(m.Doc.Layers[0].Locked); Assert.False(m.Doc.Layers[0].Px.IsEmpty());
            // Skizze: Ring malen, säubern, vorfärben
            var sk = m.Doc.Layers.First(l => l.Name == "Skizze"); d.Data["active"] = sk.Id;
            w.ArtBrush = "tusche"; w.ArtColor = "#555555"; w.ArtSize = 8; w.ArtSmooth = 0;
            w.ArtPaint(d, Enumerable.Range(0, 73).Select(i => (800 + 300 * Math.Cos(i * Math.PI / 36), 800 + 300 * Math.Sin(i * Math.PI / 36), 1.0)));
            Tap(w, "prog-cmd-art-clean");
            m = w.ArtModel(d); var lines = m.Doc.Layers.Single(l => l.Name == "Linien"); Assert.Equal(lines.Id, d.Text("active"));
            Assert.True(m.Doc.Layers.First(l => l.Name == "Skizze").Opacity <= 0.3);
            Tap(w, "prog-cmd-art-flats");
            m = w.ArtModel(d); var flats = m.Doc.Layers.Single(l => l.Name == "Farbflächen");
            Assert.Equal(255, flats.Px.At(800, 800).A); Assert.Equal(0, flats.Px.At(10, 10).A);
            // Ideenskizze landet als Referenz, nicht als Ebene
            var layers = m.Doc.Layers.Count;
            w.Find<TextBox>("art-idea").Text = "Fuchs im Schnee"; Tap(w, "art-idea-go");
            for (var i = 0; i < 200 && d.List("refs").Count < 2; i++) { await Task.Delay(10); Settle(); }
            Assert.Equal(2, d.List("refs").Count); Assert.Equal(layers, w.ArtModel(d).Doc.Layers.Count);
        }
        finally { MainWindow.ImageAiOverride = null; w.Close(); }
    }

    [AvaloniaFact(DisplayName = "K5/K7/K8: Zeitraffer-Video, Portfolio-Webseite, Übergabe an Print on Demand und Malkurs, der mitzählt")]
    public async Task Timelapse_share_course()
    {
        var (w, core, root) = Ui.Open(); Directory.CreateDirectory(root);
        string? pattern = null;
        MainWindow.ArtVideoOverride = (pat, fps, output) => { pattern = pat; File.WriteAllBytes(output, [0, 0, 0, 1]); return true; };
        try
        {
            var d = Open(w);
            w.Find<ComboBox>("art-course").SelectedIndex = 1; Settle(); // „Erste Schritte“
            Assert.StartsWith("Schritt 1 von 4", w.Find<TextBlock>("art-course-step").Text);
            w.ArtBrush = "bleistift"; w.ArtColor = "#000000";
            w.ArtPaint(d, [(100, 100, 1), (300, 120, 0.5)]); w.Show(K); Settle();
            Assert.StartsWith("Schritt 2 von 4", w.Find<TextBlock>("art-course-step").Text);
            Tap(w, "prog-cmd-art-layer-add"); w.ArtPaint(d, [(500, 500, 1), (700, 520, 1)]); w.ArtPaint(d, [(500, 600, 1), (700, 620, 1)]); w.Show(K); Settle();
            Assert.NotNull(w.Find<TextBlock>("art-course-done"));
            // Zeitraffer: 3 Striche = 3 Bilder
            var video = await w.ArtTimelapseVideo(d, false);
            Assert.NotNull(video); Assert.EndsWith("z%05d.png", pattern); Assert.Equal(3, Timelapse.Count(Path.GetDirectoryName(pattern)!));
            // Portfolio
            var html = w.ArtPortfolio(d); Assert.Contains("<img", File.ReadAllText(html));
            // Print on Demand
            var motiv = w.ArtToPod(d); Assert.True(File.Exists(motiv));
            Assert.Contains(Path.GetFileName(motiv), w.ProgramActive("pod-programm")!.Data.ToJsonString());
        }
        finally { MainWindow.ArtVideoOverride = null; w.Close(); }
    }
}
