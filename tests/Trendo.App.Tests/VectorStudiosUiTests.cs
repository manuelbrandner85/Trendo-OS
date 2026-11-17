using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using SkiaSharp;
using Trendo.App;
using Trendo.Core;
using Trendo.Core.Kreativ;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// <summary>T-0696: Logos &amp; Marken und Animation &amp; Motion mit dem gemeinsamen Vektor-Editor – alles mit echter Maus und Tastatur (Headless),
/// Exporte mit echten Fremdlesern: Chrome (SVG), PDFium (PDF), FFmpeg/ffprobe (MP4, GIF). Fehlt ein Werkzeug, wird der Teil übersprungen und gesagt.</summary>
public class VectorStudios_Ui
{
    const string LK = "logo-programm", AK = "animation-programm";

    /// <summary>Programmfenster groß wie im Alltag.</summary>
    static void Max(MainWindow w, string key) { if (w.Windows.Active is { Maximized: false } pw) { w.Windows.ToggleMaximize(pw.Id); w.Show(key); } Settle(); }
    static VectorEditor Ed(MainWindow w, string key) { Settle(); return w.VecEditors[key]; }
    static Point Win(MainWindow w, VectorEditor ed, double x, double y) { w.UpdateLayout(); Settle(); return ed.Surface.TranslatePoint(new Point(x, y), w) ?? throw new InvalidOperationException("Fläche nicht sichtbar"); }
    static void Drag(MainWindow w, Point a, Point b, RawInputModifiers m = RawInputModifiers.None)
    {
        w.MouseMove(a, m); w.MouseDown(a, MouseButton.Left, m); Settle();
        for (var i = 1; i <= 6; i++) { w.MouseMove(a + (b - a) * (i / 6.0), m); Settle(); }
        w.MouseUp(b, MouseButton.Left, m); Settle();
    }
    static void Click(MainWindow w, Point p, int count = 1) { for (var i = 0; i < count; i++) { w.MouseDown(p, MouseButton.Left); w.MouseUp(p, MouseButton.Left); } Settle(); }
    static void Tool(MainWindow w, string id) => RealClick(w, w.Find<Button>("prog-cmd-vec-tool-" + id));
    static void Ctrl(MainWindow w, PhysicalKey k) { w.KeyPressQwerty(k, RawInputModifiers.Control); w.KeyReleaseQwerty(k, RawInputModifiers.Control); Settle(); }
    static string Export(MainWindow w, string key, string name, string root)
    {
        var e = MainWindow.Programs[key].Exports.Single(x => x.Name == name);
        return e.Write(w.ProgramActive(key)!, Path.Combine(root, "export-" + name + e.Extension));
    }
    /// <summary>Inhalt eines aufgeklappten Menüs (Flyout) – liegt nicht im Fensterbaum.</summary>
    static T InFlyout<T>(MainWindow w, string button, string name) where T : Control =>
        ((Control)((Avalonia.Controls.Flyout)w.Find<Button>(button).Flyout!).Content!).GetLogicalDescendants().OfType<T>().First(c => c.Name == name);
    static string Sha(string f) => Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(f)));
    /// <summary>Belege für reports/T-0696 nur, wenn TRENDO_T0696_BELEG auf einen Ordner zeigt.</summary>
    static string? Beleg => Environment.GetEnvironmentVariable("TRENDO_T0696_BELEG") is { Length: > 0 } b ? Directory.CreateDirectory(b).FullName : null;

    [AvaloniaFact(DisplayName = "Logo aus leerer Vorlage mit der Maus: Stern zeichnen, verschieben, drehen, skalieren, Strg+Z; Zeichenstift-Pfad mit Bézier-Rundung schließen, Punkt ziehen; Entf, Strg+D, Rechtsklick-Menü, Raster")]
    public void LogoDraw()
    {
        var (w, _, _) = Ui.Open();
        try
        {
            w.Show(LK); Settle(); Max(w, LK); RealClick(w, w.Find<Button>("prog-tpl-leer")); Settle();
            var d = w.ProgramActive(LK)!;
            int Count() => MainWindow.LogoVec(d).Shapes.Count;
            var n0 = Count();
            // Stern zeichnen
            Tool(w, "star");
            var ed = Ed(w, LK);
            Drag(w, Win(w, ed, 400, 30), Win(w, ed, 480, 110));
            var v = MainWindow.LogoVec(d);
            Assert.Equal(n0 + 1, v.Shapes.Count);
            var star = v.Shapes[^1];
            Assert.Equal(("star", 400.0, 30.0, 80.0, 80.0), (star.Kind, star.X, star.Y, star.W, star.H));
            Assert.Equal("Stern gezeichnet", d.UndoLabel);
            ed = Ed(w, LK); Assert.Equal("select", ed.State.Tool); Assert.Contains(star.Id, ed.State.Selected);
            // verschieben (Raster 10, Einrasten an)
            Drag(w, Win(w, ed, 440, 70), Win(w, ed, 500, 90));
            star = MainWindow.LogoVec(d).Find(star.Id)!;
            Assert.Equal((460.0, 50.0), (star.X, star.Y));
            // drehen am runden Griff: 90° im Uhrzeigersinn um die Mitte
            ed = Ed(w, LK);
            var h = ed.Handles(ed.Frame()!.Value);
            Drag(w, Win(w, ed, h[8].X, h[8].Y), Win(w, ed, star.Cx + 60, star.Cy));
            star = MainWindow.LogoVec(d).Find(star.Id)!;
            Assert.Equal(90, star.Rot, 3);
            // Größe am Griff unten rechts (gedreht – der Griff liegt jetzt unten links)
            ed = Ed(w, LK); h = ed.Handles(ed.Frame()!.Value);
            var wBefore = star.W;
            Drag(w, Win(w, ed, h[4].X, h[4].Y), Win(w, ed, h[4].X - 20, h[4].Y + 20));
            star = MainWindow.LogoVec(d).Find(star.Id)!;
            Assert.True(star.W > wBefore + 10, $"Breite {star.W}");
            // Strg+Z: Größe zurück, Drehung bleibt
            Ed(w, LK).Surface.Focus(); Ctrl(w, PhysicalKey.Z);
            star = MainWindow.LogoVec(d).Find(star.Id)!;
            Assert.Equal(wBefore, star.W, 3); Assert.Equal(90, star.Rot, 3);

            // Zeichenstift: Ecke, Rundung (gezogen), Ecke, Klick auf den ersten Punkt schließt
            Tool(w, "pen"); ed = Ed(w, LK);
            Click(w, Win(w, ed, 200, 40));
            Drag(w, Win(w, ed, 260, 150), Win(w, ed, 300, 150));
            Click(w, Win(w, ed, 320, 40));
            Click(w, Win(w, ed, 200, 40));
            v = MainWindow.LogoVec(d);
            var path = v.Shapes[^1];
            Assert.Equal(("path", true, 3), (path.Kind, path.Closed, path.Nodes.Count));
            Assert.False(path.Nodes[1].IsCorner); // Bézier-Griffe aus dem Ziehen
            Assert.Contains(" C", VectorKit.PathData(path.Nodes, true));
            // Punkt ziehen (Werkzeug „Punkte“)
            Tool(w, "nodes"); ed = Ed(w, LK);
            var a = VectorKit.ToDoc(path, path.Nodes[2].X, path.Nodes[2].Y);
            Drag(w, Win(w, ed, a.X, a.Y), Win(w, ed, a.X + 30, a.Y + 30));
            var moved = MainWindow.LogoVec(d).Find(path.Id)!;
            var a2 = VectorKit.ToDoc(moved, moved.Nodes[2].X, moved.Nodes[2].Y);
            Assert.Equal((a.X + 30, a.Y + 30), (Math.Round(a2.X), Math.Round(a2.Y)));
            Assert.Equal("Punkt verschoben", d.UndoLabel);

            // Tastatur: Auswahl, Strg+D, Entf
            Tool(w, "select"); ed = Ed(w, LK);
            Click(w, Win(w, ed, star.Cx, star.Cy));
            ed = Ed(w, LK); ed.Surface.Focus(); var before = Count();
            Ctrl(w, PhysicalKey.D);
            Assert.Equal(before + 1, Count());
            Ed(w, LK).Surface.Focus(); w.KeyPressQwerty(PhysicalKey.Delete, RawInputModifiers.None); w.KeyReleaseQwerty(PhysicalKey.Delete, RawInputModifiers.None); Settle();
            Assert.Equal(before, Count());
            // Taste R wählt das Rechteck-Werkzeug
            Ed(w, LK).Surface.Focus(); w.KeyPressQwerty(PhysicalKey.R, RawInputModifiers.None); w.KeyReleaseQwerty(PhysicalKey.R, RawInputModifiers.None); Settle();
            Assert.Equal("rect", Ed(w, LK).State.Tool);
            // Rechtsklick auf eine Form: Menü mit Gruppieren/Anordnen/Ausrichten
            Tool(w, "select"); ed = Ed(w, LK);
            var rp = Win(w, ed, star.Cx, star.Cy); w.MouseDown(rp, MouseButton.Right); w.MouseUp(rp, MouseButton.Right); Settle();
            var menu = Ed(w, LK).Surface.ContextMenu;
            Assert.NotNull(menu);
            Assert.Contains(menu!.Items.OfType<MenuItem>(), i => (string?)i.Header == "Gruppieren");
            Assert.Contains(menu.Items.OfType<MenuItem>(), i => (string?)i.Header == "Ausrichten");
            menu.Close();
            // Ausrichten über die Leiste: allein an der Fläche mittig
            RealClick(w, w.Find<Button>("logo-vec-arrange")); Settle();
            InFlyout<Button>(w, "logo-vec-arrange", "logo-vec-align-mitte-h").Press(); Settle();
            Assert.Equal(320, MainWindow.LogoVec(d).Find(star.Id)!.Cx, 3);
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "Logo: fremdes SVG hineinziehen und umfärben, PNG einsetzen; Vorschlag erzeugen ersetzt nur mit Rückgängig; Namensfeld ändert den Text in der Zeichnung")]
    public void LogoImport()
    {
        var (w, _, root) = Ui.Open(); Directory.CreateDirectory(root);
        try
        {
            w.Show(LK); Settle(); Max(w, LK); RealClick(w, w.Find<Button>("prog-tpl-cafe")); Settle();
            var d = w.ProgramActive(LK)!;
            var svg = Path.Combine(root, "zeichen.svg");
            File.WriteAllText(svg, """<svg xmlns="http://www.w3.org/2000/svg" width="100" height="100"><circle cx="50" cy="50" r="45" fill="#00AA00"/><path d="M20,50 C20,10 80,10 80,50 Z" fill="white"/></svg>""");
            var ed = Ed(w, LK); var n = MainWindow.LogoVec(d).Shapes.Count;
            ed.DropFiles!([svg], 560, 90); // derselbe Weg wie Ziehen-und-Ablegen auf die Fläche
            Settle();
            var v = MainWindow.LogoVec(d);
            Assert.Equal(n + 2, v.Shapes.Count);
            var placed = v.Shapes.Skip(n).ToList();
            Assert.Single(placed.Select(s => s.Group).Distinct());
            Assert.Equal(560, VectorKit.Bounds(placed).X + VectorKit.Bounds(placed).W / 2, 3);
            ed = Ed(w, LK); Assert.True(placed.All(s => ed.State.Selected.Contains(s.Id)));
            // umfärben: Füllung über die Leiste (Farbfeld + Übernehmen)
            RealClick(w, w.Find<Button>("logo-vec-fill")); Settle();
            InFlyout<TextBox>(w, "logo-vec-fill", "logo-vec-fill-hex").Text = "#123456"; InFlyout<Button>(w, "logo-vec-fill", "logo-vec-fill-ok").Press(); Settle();
            Assert.All(MainWindow.LogoVec(d).Shapes.Skip(n), s => Assert.Equal("#123456", s.Fill));
            // PNG über die Dateiauswahl einsetzen
            var png = Path.Combine(root, "bild.png");
            File.WriteAllBytes(png, VectorRender.Png(new VDoc { W = 40, H = 20, Background = "#FF0000" }, 40));
            w.PickOverride = _ => Task.FromResult<string[]?>([png]);
            RealClick(w, w.Find<Button>("logo-vec-import")); Settle();
            var img = MainWindow.LogoVec(d).Shapes[^1];
            Assert.Equal(("image", 40.0, 20.0), (img.Kind, img.W, img.H)); Assert.StartsWith("data:image/png;base64,", img.Image);
            // Namensfeld: Text in der Zeichnung folgt, Handarbeit bleibt
            w.ProgramShowProperties(LK); w.Show(LK); Settle();
            var name = w.Find<TextBox>("logo-p-name"); name.Focus(); name.Text = "Café Abendrot"; w.Find<TextBox>("logo-p-tagline").Focus(); Settle();
            v = MainWindow.LogoVec(d);
            Assert.Equal("Café Abendrot", v.Find("name")!.Text); Assert.Equal("CA", v.Find("zeichen-text")!.Text);
            Assert.Contains(v.Shapes, s => s.Kind == "image");
            // Vorschlag erzeugen ersetzt die Zeichnung, Strg+Z holt sie zurück
            RealClick(w, w.Find<Button>("prog-cmd-logo-suggest")); Settle();
            Assert.DoesNotContain(MainWindow.LogoVec(d).Shapes, s => s.Kind == "image");
            Assert.True(d.Undo()); Settle();
            Assert.Contains(MainWindow.LogoVec(d).Shapes, s => s.Kind == "image");
        }
        finally { w.PickOverride = null; w.Close(); }
    }

    static double[] Pixels(byte[] png, int w, int h, string bg)
    {
        var tmp = Path.Combine(Path.GetTempPath(), "trendo-t0696-" + Guid.NewGuid().ToString("N") + ".png"); File.WriteAllBytes(tmp, png);
        using var src = SKBitmap.Decode(tmp); File.Delete(tmp); using var bmp = new SKBitmap(w, h);
        using (var c = new SKCanvas(bmp)) { c.Clear(SKColor.Parse(bg)); c.DrawBitmap(src, new SKRect(0, 0, w, h)); }
        var a = new double[w * h * 3];
        for (var y = 0; y < h; y++) for (var x = 0; x < w; x++) { var p = bmp.GetPixel(x, y); var i = (y * w + x) * 3; a[i] = p.Red; a[i + 1] = p.Green; a[i + 2] = p.Blue; }
        return a;
    }

    [AvaloniaFact(DisplayName = "Logo-Exporte echt geprüft: SVG in Chrome gerendert und Pixel mit der Vorschau verglichen, PDF mit PDFium gezeichnet, PNG 512/1024/2048, Favicon, Paket mit hell/dunkel/einfarbig/negativ")]
    public async Task LogoExports()
    {
        var (w, _, root) = Ui.Open(); Directory.CreateDirectory(root);
        var log = new StringBuilder();
        try
        {
            w.Show(LK); Settle(); Max(w, LK); RealClick(w, w.Find<Button>("prog-tpl-cafe")); Settle();
            var d = w.ProgramActive(LK)!;
            // eigene Handarbeit dazu: Stern mit Verlauf, gedreht
            Tool(w, "star"); var ed = Ed(w, LK);
            Drag(w, Win(w, ed, 540, 20), Win(w, ed, 620, 100));
            var vv = MainWindow.LogoVec(d); var st = vv.Shapes[^1]; st.Fill = "#E0A458"; st.Fill2 = "#7A2E1F"; st.Rot = 15;
            d.Edit("Testvorbereitung", x => x["vector"] = VectorKit.ToJson(vv)); w.Show(LK); Settle();
            var v = MainWindow.LogoVec(d);

            var svg = Export(w, LK, "svg", root);
            Assert.NotNull(XElement.Parse(File.ReadAllText(svg)));
            Assert.Equal(VectorKit.ToJson(v).ToJsonString(), VectorKit.ToJson(VectorKit.FromSvg(File.ReadAllText(svg)).Doc).ToJsonString()); // Rundreise
            log.AppendLine($"SVG {Path.GetFileName(svg)} {new FileInfo(svg).Length} B sha256={Sha(svg)}");

            // Fremdleser Chrome: SVG als Bild in einer Seite, Bildschirmfoto in Originalgröße
            var browser = WebsitePreview.FindBrowser();
            if (browser is null) log.AppendLine("Chrome/Edge: NICHT VORHANDEN – Pixelvergleich übersprungen");
            else
            {
                var site = Path.Combine(root, "svgseite"); Directory.CreateDirectory(site);
                File.Copy(svg, Path.Combine(site, "logo.svg"));
                File.WriteAllText(Path.Combine(site, "index.html"), "<!doctype html><html data-fertig=\"1\"><body style=\"margin:0;background:#FFFFFF\"><img src=\"logo.svg\" width=\"1280\" height=\"360\" style=\"display:block\"></body></html>");
                var shot = Path.Combine(root, "chrome.png");
                var r = await WebsitePreview.CaptureAsync(site, 1280, 360, shot); // Seite in doppelter Größe – Chrome rechnet die Vektoren selbst
                Assert.True(r.Ok, r.Note);
                var preview = VectorRender.Png(v, 1280, "#FFFFFF");
                File.WriteAllBytes(Path.Combine(root, "vorschau.png"), preview);
                var a = Pixels(File.ReadAllBytes(shot), 1280, 360, "#FFFFFF"); var b = Pixels(preview, 1280, 360, "#FFFFFF");
                var diff = a.Zip(b, (x, y) => Math.Abs(x - y)).ToArray();
                var mean = diff.Average(); var far = diff.Count(x => x > 60) / (double)diff.Length;
                log.AppendLine($"Chrome {Path.GetFileName(browser)}: Bild {new FileInfo(shot).Length} B sha256={Sha(shot)}; Vorschau sha256={Convert.ToHexStringLower(SHA256.HashData(preview))}");
                log.AppendLine($"Pixelvergleich 1280×360: mittlere Abweichung {mean:0.00} von 255, Anteil Kanäle > 60 abweichend {far:P2}");
                Assert.True(mean < 6, $"mittlere Abweichung {mean:0.00}"); Assert.True(far < 0.03, $"stark abweichend {far:P2}");
                // dort, wo der Kreis/Schild sitzt, ist es in beiden Bildern dunkelrot
                var i = (80 * 1280 + 100) * 3; // Schild oben links (Dokument 50|40) Assert.True(a[i] > a[i + 2] + 40 && b[i] > b[i + 2] + 40, "Zeichen fehlt");
                if (Beleg is { } bl) { File.Copy(shot, Path.Combine(bl, "logo-chrome.png"), true); File.WriteAllBytes(Path.Combine(bl, "logo-vorschau.png"), preview); File.Copy(svg, Path.Combine(bl, "logo.svg"), true); }
            }

            // PDF: Vektor, mit PDFium (PDFtoImage) gezeichnet – das Zeichen ist sichtbar
            var pdf = Export(w, LK, "pdf", root);
            Assert.Equal(1, PdfView.PageCount(pdf));
            using (var page = PdfView.RenderSk(pdf, 0, 96))
            {
                Assert.Equal((640, 180), (page.Width, page.Height));
                var px = page.GetPixel(50, 40); Assert.True(px.Red > px.Blue + 40, $"PDF an der Stelle des Zeichens: {px}");
                Assert.Contains("Morgenrot", PdfView.Text(pdf, 0));
            }
            log.AppendLine($"PDF {new FileInfo(pdf).Length} B sha256={Sha(pdf)} – PDFium: 1 Seite 640×180 px bei 96 dpi, Text gefunden");

            var png = Export(w, LK, "png", root);
            using (var b = SKBitmap.Decode(png)) Assert.Equal((1024, 288), (b.Width, b.Height));
            var ico = Export(w, LK, "ico", root);
            Assert.Equal([16, 32, 48, 64, 128, 256], VectorKit.IcoSizes(File.ReadAllBytes(ico)));
            var zip = Export(w, LK, "paket", root);
            using (var z = ZipFile.OpenRead(zip))
            {
                var names = z.Entries.Select(e => e.FullName).ToHashSet();
                foreach (var f in new[] { "logo.svg", "logo-hell.svg", "logo-dunkel.svg", "logo-einfarbig.svg", "logo-negativ.svg", "logo-512.png", "logo-1024.png", "logo-2048.png", "logo.pdf", "favicon.ico", "markenpaket.md" }) Assert.Contains(f, names);
                using var s = z.GetEntry("logo-2048.png")!.Open(); using var ms = new MemoryStream(); s.CopyTo(ms);
                var bigFile = Path.Combine(root, "gross.png"); File.WriteAllBytes(bigFile, ms.ToArray());
                using var big = SKBitmap.Decode(bigFile); Assert.Equal(2048, big.Width);
                using var md = new StreamReader(z.GetEntry("markenpaket.md")!.Open()); Assert.Contains("#7A2E1F", md.ReadToEnd());
            }
            log.AppendLine($"ZIP {new FileInfo(zip).Length} B sha256={Sha(zip)}; ICO sha256={Sha(ico)}; PNG1024 sha256={Sha(png)}");
            if (Beleg is { } bl2) File.AppendAllText(Path.Combine(bl2, "logo-exporte.log"), log.ToString());
        }
        finally { w.Close(); }
    }

    static (double X, double Y, int N) Centroid(string png, Func<SKColor, bool> match)
    {
        using var b = SKBitmap.Decode(png); double sx = 0, sy = 0; var n = 0;
        for (var y = 0; y < b.Height; y++) for (var x = 0; x < b.Width; x++) if (match(b.GetPixel(x, y))) { sx += x; sy += y; n++; }
        return n == 0 ? (double.NaN, double.NaN, 0) : (sx / n, sy / n, n);
    }

    [AvaloniaFact(DisplayName = "Animation mit echtem FFmpeg, vom Klick bis zur Datei: erste Form, auf der Bühne ziehen setzt Schlüssel bei 0 s und 2 s, Raute in der Zeitleiste ziehen und Strg+Z, Kurve per Rechtsklick; MP4 (ffprobe) zeigt die Form bei t=0 links und am Ende rechts; GIF und animiertes SVG")]
    public async Task AnimRealVideo()
    {
        var (w, _, root) = Ui.Open(); Directory.CreateDirectory(root);
        var log = new StringBuilder();
        try
        {
            w.Show(AK); Settle(); Max(w, AK); RealClick(w, w.Find<Button>("prog-tpl-leer")); Settle();
            RealClick(w, w.Find<Button>("prog-first-step")); Settle();
            var d = w.ProgramActive(AK)!;
            var v = MainWindow.AnimVec(d);
            var s = Assert.Single(v.Shapes);
            Assert.Equal(("rect", 640.0, 360.0), (s.Kind, s.Cx, s.Cy));
            // t=0: nach links ziehen → Schlüssel (Schlüssel automatisch ist an)
            var ed = Ed(w, AK);
            Drag(w, Win(w, ed, 640, 360), Win(w, ed, 300, 360));
            s = MainWindow.AnimVec(d).Shapes[0];
            Assert.Equal(300, s.Tracks["x"].Single().V, 3);
            // Zeit 2 s über das Lineal der Zeitleiste
            var tl = w.Find<KeyTimeline>("anim-timeline"); w.UpdateLayout(); Settle();
            Click(w, tl.TranslatePoint(new Point(tl.X(2), 6), w)!.Value);
            Assert.Equal("2.00 s", w.Find<TextBlock>("anim-time").Text?.Replace(',', '.'));
            ed = Ed(w, AK);
            Drag(w, Win(w, ed, 300, 360), Win(w, ed, 980, 360));
            s = MainWindow.AnimVec(d).Shapes[0];
            Assert.Equal([(0.0, 300.0), (2.0, 980.0)], s.Tracks["x"].Select(k => (k.T, Math.Round(k.V))));
            Assert.Equal(640, MotionKit.Value(s, "x", 1), 0); // weich: Mitte nach 1 s genau in der Mitte
            // Raute (Position, 2 s) nach 1,5 s ziehen, dann Strg+Z
            tl = w.Find<KeyTimeline>("anim-timeline"); w.UpdateLayout(); Settle();
            var kp = tl.PointOf(s.Id, "position", 2)!.Value;
            Drag(w, tl.TranslatePoint(kp, w)!.Value, tl.TranslatePoint(new Point(tl.X(1.5), kp.Y), w)!.Value);
            Assert.Equal([0.0, 1.5], MainWindow.AnimVec(d).Shapes[0].Tracks["x"].Select(k => Math.Round(k.T, 2)));
            Assert.True(d.Undo()); w.Show(AK); Settle();
            Assert.Equal([0.0, 2.0], MainWindow.AnimVec(d).Shapes[0].Tracks["x"].Select(k => k.T));
            // Rechtsklick auf die Raute bei 0 s: Kurve „Gleichmäßig“
            tl = w.Find<KeyTimeline>("anim-timeline"); w.UpdateLayout(); Settle();
            var k0 = tl.TranslatePoint(tl.PointOf(s.Id, "position", 0)!.Value, w)!.Value;
            w.MouseDown(k0, MouseButton.Right); w.MouseUp(k0, MouseButton.Right); Settle();
            var item = tl.ContextMenu!.Items.OfType<MenuItem>().Single(i => i.Name == "anim-key-ease-linear");
            item.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(MenuItem.ClickEvent)); Settle();
            Assert.Equal("linear", MainWindow.AnimVec(d).Shapes[0].Tracks["x"][0].Ease);
            Assert.Equal(640, MotionKit.Value(MainWindow.AnimVec(d).Shapes[0], "x", 1), 3);

            var svg = Export(w, AK, "svg", root);
            Assert.NotNull(XElement.Parse(File.ReadAllText(svg)));

            var ff = Cli.Find("ffmpeg"); var probe = Cli.Find("ffprobe");
            if (ff is null || probe is null) { Assert.Skip("Video übersprungen – FFmpeg oder ffprobe fehlt auf diesem PC."); return; }
            w.AnimFfmpegPath = ff; // echter FFmpeg statt der Sperre des abgeschotteten Testmodus – kein Ersatz für das Werkzeug
            var mp4 = Path.Combine(root, "ball.mp4");
            var t = w.AnimVideo(d, mp4);
            var sw = Stopwatch.StartNew(); while (!t.IsCompleted && sw.Elapsed < TimeSpan.FromSeconds(120)) { Dispatcher.UIThread.RunJobs(); await Task.Delay(20); }
            var res = await t; w.Show(AK); Settle();
            Assert.True(res == mp4, "Video: " + string.Join(" | ", w.GetLogicalTexts().Where(x => x.Contains("Video") || x.Contains("FFmpeg") || x.Contains("ideo"))));
            string Probe(params string[] a) { var r = Cli.Run(probe, ["-v", "error", .. a], root, 60000); Assert.True(r.Ok, r.Err); return r.Out.Trim(); }
            var info = Probe("-select_streams", "v:0", "-show_entries", "stream=codec_name,width,height,nb_frames:format=duration", "-of", "default=nw=1", mp4);
            log.AppendLine("ffprobe MP4:\n" + info);
            Assert.Contains("codec_name=h264", info); Assert.Contains("width=1280", info); Assert.Contains("height=720", info);
            var dur = double.Parse(info.Split('\n').First(l => l.StartsWith("duration=")).Split('=')[1], System.Globalization.CultureInfo.InvariantCulture);
            Assert.InRange(dur, 1.9, 2.2);
            // Standbild am Anfang und am Ende mit FFmpeg herausholen
            var f0 = Path.Combine(root, "t0.png"); var f1 = Path.Combine(root, "tende.png");
            Assert.True(Cli.Run(ff, ["-v", "error", "-y", "-i", mp4, "-vf", "select=eq(n\\,0)", "-frames:v", "1", f0], root, 60000).Ok);
            Assert.True(Cli.Run(ff, ["-v", "error", "-y", "-sseof", "-0.2", "-i", mp4, "-update", "1", f1], root, 60000).Ok);
            static bool Gold(SKColor c) => c.Red > 200 && c.Green > 150 && c.Blue < 120;
            var c0 = Centroid(f0, Gold); var c1 = Centroid(f1, Gold);
            log.AppendLine($"Standbild t=0: Mitte der Form ({c0.X:0.0}, {c0.Y:0.0}), {c0.N} Bildpunkte; erwartet (300, 360)");
            log.AppendLine($"Standbild Ende: Mitte der Form ({c1.X:0.0}, {c1.Y:0.0}), {c1.N} Bildpunkte; erwartet (980, 360)");
            Assert.InRange(c0.X, 294, 306); Assert.InRange(c0.Y, 354, 366); Assert.InRange(c1.X, 974, 986); Assert.InRange(c1.Y, 354, 366);
            log.AppendLine($"MP4 {new FileInfo(mp4).Length} B sha256={Sha(mp4)}");
            // GIF
            var gif = Path.Combine(root, "ball.gif");
            var tg = w.AnimVideo(d, gif, gif: true);
            sw.Restart(); while (!tg.IsCompleted && sw.Elapsed < TimeSpan.FromSeconds(120)) { Dispatcher.UIThread.RunJobs(); await Task.Delay(20); }
            Assert.Equal(gif, await tg);
            var gi = Probe("-select_streams", "v:0", "-show_entries", "stream=codec_name,width,height", "-of", "default=nw=1", gif);
            Assert.Contains("codec_name=gif", gi); Assert.Contains("width=1280", gi);
            log.AppendLine("ffprobe GIF:\n" + gi + $"\nGIF {new FileInfo(gif).Length} B sha256={Sha(gif)}");
            if (Beleg is { } bl)
            {
                File.Copy(f0, Path.Combine(bl, "anim-t0.png"), true); File.Copy(f1, Path.Combine(bl, "anim-ende.png"), true);
                File.Copy(svg, Path.Combine(bl, "anim.svg"), true);
                File.AppendAllText(Path.Combine(bl, "anim-video.log"), log.ToString());
            }
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "Animation: alte Ebenen-Dokumente öffnen weiter, Hochformat 1080×1920, Text und SVG auf der Bühne, Farbe als Schlüssel")]
    public void AnimLegacyAndFormat()
    {
        var (w, _, root) = Ui.Open(); Directory.CreateDirectory(root);
        try
        {
            w.Show(AK); Settle(); Max(w, AK); RealClick(w, w.Find<Button>("prog-tpl-ball")); Settle();
            var d = w.ProgramActive(AK)!;
            Assert.Equal("kreis", (string?)d.List("layers")[0]!["shape"]); // Vorlage im alten Format
            var v = MainWindow.AnimVec(d);
            Assert.Equal(200, MotionKit.Value(v.Shapes[0], "x", 0)); Assert.Equal(1080, MotionKit.Value(v.Shapes[0], "x", 2));
            // Hochformat: Formen bleiben mittig
            var fmt = w.Find<ComboBox>("anim-p-format"); fmt.SelectedIndex = 2; Settle();
            v = MainWindow.AnimVec(d);
            Assert.Equal((1080.0, 1920.0), (v.W, v.H));
            Assert.Equal(200 + (1080 - 1280) / 2.0, MotionKit.Value(v.Shapes[0], "x", 0), 3);
            Assert.Equal("ellipse", (string?)d.List("layers")[0]!["kind"]); // jetzt im neuen Format gespeichert
            // SVG auf die Bühne
            var svg = Path.Combine(root, "stern.svg");
            File.WriteAllText(svg, """<svg xmlns="http://www.w3.org/2000/svg" width="50" height="50"><polygon points="25,0 50,50 0,50" fill="#00FF00"/></svg>""");
            Ed(w, AK).DropFiles!([svg], 540, 400); Settle();
            Assert.Equal(2, MainWindow.AnimVec(d).Shapes.Count);
            // Farbe als Schlüssel über die Leiste (Schlüssel automatisch)
            RealClick(w, w.Find<Button>("anim-vec-fill")); Settle();
            InFlyout<TextBox>(w, "anim-vec-fill", "anim-vec-fill-hex").Text = "#FF0000"; InFlyout<Button>(w, "anim-vec-fill", "anim-vec-fill-ok").Press(); Settle();
            Assert.True(MainWindow.AnimVec(d).Shapes[^1].Tracks.ContainsKey("farbe"));
            // Text-Ebene über den Befehl
            RealClick(w, w.Find<Button>("prog-cmd-anim-add-text")); Settle();
            var t = MainWindow.AnimVec(d).Shapes[^1];
            Assert.Equal(("text", "Titel", "middle"), (t.Kind, t.Text, t.Anchor)); Assert.Equal(540, t.Cx, 3);
        }
        finally { w.Close(); }
    }
}
