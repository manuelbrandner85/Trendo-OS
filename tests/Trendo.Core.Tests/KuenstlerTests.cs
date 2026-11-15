using Trendo.Core.Kreativ;
using Trendo.Core.PromptAction;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>Künstlerstudio K1–K8: Prüfungen aus docs/nachbau/kuenstlerstudio/anforderungen.md.</summary>
public class KuenstlerTests
{
    static Pixmap Blank(int w = 100, int h = 40) => new(w, h);
    static byte A(Pixmap r, int x, int y) => r.At(x, y).A;

    [Fact(DisplayName = "K1-01: Tusche-Strich ist lückenlos, Rand weicher als Mitte beim weichen Pinsel")]
    public void Stroke_continuous_and_soft()
    {
        var r = Blank();
        var s = new StrokeEngine(r, BrushPreset.Get("tusche"), "#000000", size: 6);
        s.Add(10, 20); s.Add(90, 20);
        for (var x = 10; x <= 90; x++) Assert.True(A(r, x, 20) > 200, $"Lücke bei x={x}");
        Assert.Equal(0, A(r, 50, 30));
        var soft = Blank(); var a = new StrokeEngine(soft, BrushPreset.Get("airbrush"), "#000000", size: 30, opacity: 1); a.Add(50, 20); a.Add(51, 20);
        Assert.True(A(soft, 50, 20) > A(soft, 50, 8), "Mitte deckt stärker als der Rand");
    }

    [Fact(DisplayName = "K1-02: Stiftdruck steuert die Strichbreite, Marker baut sich nicht auf, Airbrush schon")]
    public void Pressure_and_buildup()
    {
        int Width(double p) { var r = Blank(100, 60); var s = new StrokeEngine(r, BrushPreset.Get("tusche"), "#000000", size: 20); s.Add(10, 30, p); s.Add(90, 30, p); var n = 0; for (var y = 0; y < 60; y++) if (A(r, 50, y) > 128) n++; return n; }
        Assert.True(Width(0.2) < Width(1.0) / 2.0, $"leicht {Width(0.2)} vs. fest {Width(1.0)}");
        var m = Blank(); var mk = new StrokeEngine(m, BrushPreset.Get("marker"), "#FF0000"); for (var i = 0; i < 6; i++) { mk.Add(20, 20); mk.Add(80, 20); }
        Assert.InRange(A(m, 50, 20), 130, 150); // 55 % Deckkraft, auch nach mehrfachem Überfahren
        var ab = Blank(); var air = new StrokeEngine(ab, BrushPreset.Get("airbrush"), "#000000", size: 30); air.Add(50, 20); var first = A(ab, 50, 20);
        for (var i = 0; i < 20; i++) { air.Add(50.5, 20); air.Add(49.5, 20); }
        Assert.True(A(ab, 50, 20) > first + 40, "Airbrush wird beim Verweilen dichter");
    }

    [Fact(DisplayName = "K1-03: Pinselsatz vollständig, Radierer senkt die Deckkraft, Deckkraft sperren färbt nur Gemaltes")]
    public void Brush_set()
    {
        foreach (var id in new[] { "bleistift", "tusche", "pinsel", "airbrush", "marker", "aquarell", "kreide", "radierer" }) Assert.Equal(id, BrushPreset.Get(id).Id);
        var r = Blank(); r.Fill(0, 0, 255, 255);
        var e = new StrokeEngine(r, BrushPreset.Get("radierer"), "#000000", size: 10); e.Add(20, 20); e.Add(80, 20);
        Assert.Equal(0, A(r, 50, 20)); Assert.Equal(255, A(r, 50, 2));
        var lockR = Blank(); var t = new StrokeEngine(lockR, BrushPreset.Get("tusche"), "#000000", size: 4); t.Add(10, 20); t.Add(90, 20);
        var paint = new StrokeEngine(lockR, BrushPreset.Get("tusche"), "#FF0000", size: 30, alphaLock: true); paint.Add(50, 20); paint.Add(51, 20);
        Assert.Equal((255, 255), (lockR.At(50, 20).R, lockR.At(50, 20).A)); Assert.Equal(0, A(lockR, 50, 8));
    }

    [Fact(DisplayName = "K1-04: Ebenen anlegen, verschieben, kopieren, verbinden, ausblenden; letzte Ebene bleibt")]
    public void Layers()
    {
        var d = new PaintDoc(20, 20); var a = d.Add("A"); var b = d.Add("B");
        Assert.Equal(["A", "B"], d.Layers.Select(l => l.Name));
        d.Move(b.Id, -1); Assert.Equal(["B", "A"], d.Layers.Select(l => l.Name));
        var c = d.Duplicate(a.Id); Assert.Equal("A Kopie", c.Name); Assert.Equal(3, d.Layers.Count);
        a.Px.Fill(255, 0, 0, 255); a.Visible = false;
        Assert.Equal(255, d.Composite().At(5, 5).G); // Hintergrund weiß, A ausgeblendet
        a.Visible = true; Assert.Equal((255, 0), (d.Composite().At(5, 5).R, d.Composite().At(5, 5).G));
        d.MergeDown(c.Id); Assert.Equal(2, d.Layers.Count);
        d.Remove(b.Id); Assert.Throws<InvalidOperationException>(() => d.Remove(d.Layers[0].Id));
    }

    [Theory(DisplayName = "K1-05: Mischarten rechnen nach den Compositing-Formeln")]
    [InlineData("normal", 64)]
    [InlineData("multiply", 32)]   // 128·64/255
    [InlineData("screen", 160)]    // 128+64−32
    [InlineData("darken", 64)]
    [InlineData("lighten", 128)]
    [InlineData("add", 192)]
    [InlineData("difference", 64)]
    [InlineData("overlay", 64)]    // Grund 128 ≈ 0,5 → 2·0,5·0,25
    public void Blend_modes(string mode, int expected)
    {
        var dst = Blank(2, 2); dst.Fill(128, 128, 128, 255); var src = Blank(2, 2); src.Fill(64, 64, 64, 255);
        PaintBlend.Over(dst, src, 1, mode);
        Assert.InRange(dst.At(0, 0).R, expected - 2, expected + 2);
        var half = Blank(2, 2); half.Fill(128, 128, 128, 255); PaintBlend.Over(half, src, 0.5, "normal"); Assert.InRange(half.At(0, 0).R, 94, 98);
    }

    [Fact(DisplayName = "K1-06: Rückgängig über das Programm-Dokument – Strich wird zurückgenommen (bis 100 Schritte)")]
    public void Undo_via_document()
    {
        var doc = new Programs.ProgramDocument("STUDIO-ART-01", "Bild", PaintDoc.NewJson(40, 30)); // leeres Bild
        var before = doc.Data.ToJsonString();
        var p = PaintDoc.FromJson(doc.Data); var l = p.Layers[0];
        new StrokeEngine(l.Px, BrushPreset.Get("tusche"), "#000000").Add(5, 5);
        doc.Edit("Malen", x => PaintDoc.StoreLayer(x, l));
        Assert.NotEqual(before, doc.Data.ToJsonString());
        doc.Undo(); Assert.Equal(before, doc.Data.ToJsonString());
        Assert.True(Programs.ProgramDocument.MaxUndo >= 50);
    }

    [Fact(DisplayName = "K1-07: ORA-Rundreise behält Ebenen, Namen, Deckkraft, Sichtbarkeit, Mischart und Pixel; PNG und PSD sind gültig")]
    public void Files_roundtrip()
    {
        var dir = Path.Combine(Path.GetTempPath(), "trendo-kunst-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir);
        try
        {
            var d = new PaintDoc(30, 20, "#F0E0D0"); var a = d.Add("Skizze"); var b = d.Add("Farbe (Schatten)");
            new StrokeEngine(a.Px, BrushPreset.Get("tusche"), "#123456", size: 4).Add(5, 5);
            b.Px.Fill(200, 100, 50, 128); b.Opacity = 0.6; b.Blend = "multiply"; b.Visible = false; a.Locked = true;
            var f = Path.Combine(dir, "bild.ora"); PaintFiles.SaveOra(d, f);
            using (var z = System.IO.Compression.ZipFile.OpenRead(f))
            {
                Assert.Equal("mimetype", z.Entries[0].FullName);
                using var s = new StreamReader(z.Entries[0].Open()); Assert.Equal("image/openraster", s.ReadToEnd());
                Assert.NotNull(z.GetEntry("mergedimage.png")); Assert.NotNull(z.GetEntry("stack.xml"));
            }
            var back = PaintFiles.LoadOra(f);
            Assert.Equal("#F0E0D0", back.Background);
            Assert.Equal(["Skizze", "Farbe (Schatten)"], back.Layers.Select(l => l.Name));
            Assert.Equal(("multiply", false), (back.Layers[1].Blend, back.Layers[1].Visible)); Assert.Equal(0.6, back.Layers[1].Opacity, 3);
            Assert.True(back.Layers[0].Locked);
            Assert.Equal(a.Px.Px, back.Layers[0].Px.Px); Assert.Equal(b.Px.Px, back.Layers[1].Px.Px);
            // PNG: eigener Kodierer ↔ Dekodierer
            var png = PngCodec.Encode(d.Composite(), dpi: 300); var dec = PngCodec.Decode(png); Assert.Equal(d.Composite().Px, dec.Px);
            // JSON des Programm-Dokuments
            var j = PaintDoc.FromJson(d.ToJson()); Assert.Equal(b.Px.Px, j.Layers[1].Px.Px); Assert.Equal("multiply", j.Layers[1].Blend);
            // PSD-Kopf und Ebenenzahl
            var psd = PaintFiles.Psd(d);
            Assert.Equal("8BPS", System.Text.Encoding.ASCII.GetString(psd, 0, 4));
            Assert.Equal((1, 4, 20, 30), (psd[4] << 8 | psd[5], psd[12] << 8 | psd[13], psd[14] << 24 | psd[15] << 16 | psd[16] << 8 | psd[17], psd[18] << 24 | psd[19] << 16 | psd[20] << 8 | psd[21]));
            var lmLen = psd[26 + 4 + 4] << 24 | psd[26 + 4 + 5] << 16 | psd[26 + 4 + 6] << 8 | psd[26 + 4 + 7]; var count = (short)(psd[26 + 16] << 8 | psd[26 + 17]);
            Assert.Equal(2, count); Assert.True(lmLen > 0);
            Assert.Equal(26 + 4 + 4 + 4 + lmLen + 2 + 4 * 30 * 20, psd.Length);
            Assert.Throws<InvalidDataException>(() => PngCodec.Decode([1, 2, 3]));
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact(DisplayName = "K2-02/K2-04: Paletten als .gpl hin und zurück; Harmonien drehen den Farbton")]
    public void Palettes_and_harmonies()
    {
        var gpl = Palettes.ToGpl("Herbst", ["#C0392B", "#F39C12", "#27AE60"]);
        Assert.StartsWith("GIMP Palette", gpl);
        var (name, cols) = Palettes.FromGpl(gpl); Assert.Equal("Herbst", name); Assert.Equal(["#C0392B", "#F39C12", "#27AE60"], cols);
        Assert.Throws<InvalidDataException>(() => Palettes.FromGpl("kein format"));
        var tri = PaintColors.Harmony("#FF0000", "triade"); Assert.Equal(["#FF0000", "#00FF00", "#0000FF"], tri);
        Assert.Equal(["#FF0000", "#00FFFF"], PaintColors.Harmony("#FF0000", "komplementaer"));
        Assert.Equal(120, PaintColors.Hsv("#00FF00").H, 3);
        Assert.Throws<FormatException>(() => PaintColors.Rgb("blau"));
    }

    [Fact(DisplayName = "K2-03: Farben aus einem Foto aus drei Farbflächen findet genau diese drei")]
    public void Colors_from_photo()
    {
        var img = new Pixmap(90, 30);
        for (var y = 0; y < 30; y++) for (var x = 0; x < 90; x++) { var i = (y * 90 + x) * 4; var c = x < 30 ? (200, 30, 30) : x < 60 ? (30, 160, 60) : (40, 60, 200); img.Px[i] = (byte)c.Item1; img.Px[i + 1] = (byte)c.Item2; img.Px[i + 2] = (byte)c.Item3; img.Px[i + 3] = 255; }
        var cols = Palettes.FromPhoto(img, 3);
        Assert.Equal(3, cols.Count);
        Assert.Contains("#C81E1E", cols); Assert.Contains("#1EA03C", cols); Assert.Contains("#283CC8", cols);
    }

    [Fact(DisplayName = "K3-01: Spiegeln senkrecht und radial setzt die Spiegelpunkte")]
    public void Symmetry()
    {
        var r = Blank(); var s = new StrokeEngine(r, BrushPreset.Get("tusche"), "#000000", size: 4, sym: new PaintSymmetry("senkrecht", 50, 20)); s.Add(10, 10);
        Assert.True(A(r, 10, 10) > 200 && A(r, 89, 10) > 200);
        var pts = new PaintSymmetry("radial", 0, 0, 4).Mirror(10, 0).ToList();
        Assert.Equal(4, pts.Count); Assert.Equal(0, pts[1].X, 6); Assert.Equal(10, pts[1].Y, 6); Assert.Equal(-10, pts[2].X, 6);
    }

    [Fact(DisplayName = "K3-02/K3-03: Perspektive rastet auf den passenden Fluchtpunkt ein, Lineal hält die Richtung, „Gerade Linie“ ist gerade")]
    public void Assist_snap()
    {
        var two = new DrawAssist("perspektive2", [(0, 100), (1000, 100)]);
        (double X, double Y)? dir = null;
        var start = (500.0, 300.0);
        var p = two.Snap(start, (480, 290), ref dir); // grob Richtung linker Fluchtpunkt
        Assert.NotNull(dir);
        var p2 = two.Snap(start, (300, 260), ref dir);
        // liegt auf der Geraden Start → (0,100)
        var cross = (p2.X - 500) * (100 - 300) - (p2.Y - 300) * (0 - 500); Assert.Equal(0, cross, 3);
        var ruler = new DrawAssist("lineal", [(0, 0), (10, 10)]); (double X, double Y)? rd = null;
        ruler.Snap((0, 0), (8, 0), ref rd); var q = ruler.Snap((0, 0), (20, 0), ref rd); Assert.Equal(q.X, q.Y, 6);
        var r = Blank(); var s = new StrokeEngine(r, BrushPreset.Get("tusche"), "#000000", size: 2, assist: new DrawAssist("gerade"));
        s.Add(10, 20); s.Add(40, 5); s.Add(60, 35); s.Add(90, 20);
        Assert.True(A(r, 50, 20) > 200); Assert.Equal(0, A(r, 40, 5)); // Zwischenpunkte zählen nicht
    }

    [Fact(DisplayName = "K3-04: Glätten verringert das Zittern")]
    public void Smoothing()
    {
        double Jitter(double strength)
        {
            var sm = new Smoother(strength); double sum = 0;
            for (var i = 0; i < 200; i++) { var y = 50 + (i % 2 == 0 ? 4 : -4); var (_, sy, _) = sm.Push(i, y, 1); if (i > 20) sum += Math.Abs(sy - 50); }
            return sum;
        }
        Assert.True(Jitter(0.8) < Jitter(0) / 3);
    }

    [Fact(DisplayName = "K4-02: Posenpuppe hat 13 Knochen, steht auf dem Boden und hebt beim Winken den Arm über den Kopf")]
    public void Mannequin_pose()
    {
        var stand = Mannequin.Bones(Mannequin.Get("stehen"), 100, 400, 320);
        Assert.Equal(13, stand.Count);
        var head = stand.Single(b => b.Name == "Kopf").A.Y;
        Assert.InRange(head, 400 - 320, 400 - 280);
        Assert.InRange(stand.Single(b => b.Name == "Unterschenkel links").B.Y, 395, 405);
        var wave = Mannequin.Bones(Mannequin.Get("winken"), 100, 400, 320);
        Assert.True(wave.Single(b => b.Name == "Unterarm rechts").B.Y < head);
        var img = Mannequin.Render(200, 300, Mannequin.Get("gehen"), 30);
        Assert.False(img.IsEmpty()); Assert.Equal(0, img.At(2, 2).A);
    }

    [Fact(DisplayName = "K5-01: Zeitraffer legt nummerierte Bilder ab, das letzte zeigt das Endbild")]
    public void Timelapse_frames()
    {
        var dir = Path.Combine(Path.GetTempPath(), "trendo-zr-" + Guid.NewGuid().ToString("N"));
        try
        {
            var d = new PaintDoc(60, 40); var l = d.Add();
            for (var i = 0; i < 3; i++) { new StrokeEngine(l.Px, BrushPreset.Get("tusche"), "#000000").Add(10 + i * 15, 20); Timelapse.Record(dir, d.Composite()); }
            Assert.Equal(3, Timelapse.Count(dir));
            Assert.Equal(d.Composite().Px, PngCodec.Decode(File.ReadAllBytes(Path.Combine(dir, Timelapse.FrameName(3)))).Px);
            Assert.EndsWith("z%05d.png", Timelapse.Pattern(dir));
            Assert.Equal(2, Timelapse.Fps(10)); Assert.Equal(10, Timelapse.Fps(300));
            var big = Timelapse.Record(dir, new Pixmap(3001, 1000)); var r = PngCodec.Decode(File.ReadAllBytes(big)); Assert.Equal((1280, 426), (r.W, r.H));
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }

    [Fact(DisplayName = "K6-01/K6-02: Skizze säubern ergibt eine Linienebene ohne Krümel; Vorfärben füllt nur geschlossene Flächen")]
    public void Sketch_cleanup_and_flats()
    {
        var sk = new Pixmap(80, 80); sk.Fill(250, 248, 240, 255);
        // grauer Kreis (Ring) + ein Krümel
        for (var y = 0; y < 80; y++) for (var x = 0; x < 80; x++)
        {
            var d = Math.Sqrt((x - 40) * (x - 40) + (y - 40) * (y - 40)); var i = (y * 80 + x) * 4;
            if (Math.Abs(d - 25) < 1.5) { sk.Px[i] = sk.Px[i + 1] = sk.Px[i + 2] = 90; }
        }
        var ki = (5 * 80 + 5) * 4; sk.Px[ki] = sk.Px[ki + 1] = sk.Px[ki + 2] = 40;
        var lines = Sketch.LineArt(sk, 0.35, 8);
        Assert.True(lines.At(65, 40).A > 200); Assert.Equal(0, lines.At(5, 5).A); Assert.Equal(0, lines.At(40, 40).A);
        Assert.Equal(250, sk.At(40, 40).R); // Original unverändert
        var (flats, n) = Sketch.Flats(lines);
        Assert.Equal(1, n); Assert.Equal(255, flats.At(40, 40).A); Assert.Equal(0, flats.At(2, 2).A);
    }

    [Fact(DisplayName = "K7-01/K7-02: Portfolio schreibt index.html mit Bildern; Druckdatei A4 300 dpi mit 3 mm Beschnitt hat die richtigen Maße")]
    public void Portfolio_and_print()
    {
        var dir = Path.Combine(Path.GetTempPath(), "trendo-pf-" + Guid.NewGuid().ToString("N"));
        try
        {
            var png = PngCodec.Encode(new Pixmap(4, 4));
            var f = Portfolio.Write(dir, "Meine Bilder", "Erika Muster", [new("Abendlicht", png, "Aquarell"), new("Abendlicht", png), new("<Katze>", png)]);
            var html = File.ReadAllText(f);
            Assert.Contains("bilder/abendlicht.png", html); Assert.Contains("bilder/abendlicht-2.png", html); Assert.Contains("&lt;Katze&gt;", html);
            Assert.DoesNotContain("<script", html); Assert.Equal(3, Directory.GetFiles(Path.Combine(dir, "bilder")).Length);
            Assert.Throws<InvalidOperationException>(() => Portfolio.Write(dir, "x", "y", []));
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
        Assert.Equal((2551, 3579), PrintFile.Pixels("a4", 300, 3));
        Assert.Equal((3579, 2551), PrintFile.Pixels("a4", 300, 3, landscape: true));
        var src = new Pixmap(400, 300); src.Fill(10, 20, 30, 255);
        var (p, w, h, warn) = PrintFile.Make(src, "a5", 150, 0);
        Assert.Equal((874, 1240), (w, h)); Assert.True(warn);
        var back = PngCodec.Decode(p); Assert.Equal((10, 255), (back.At(0, 0).R, back.At(w - 1, h - 1).A));
        Assert.Throws<ArgumentException>(() => PrintFile.Pixels("a4", 20));
    }

    [Fact(DisplayName = "K8-01: Malkurs geht erst weiter, wenn die Bedingung des Schritts erfüllt ist")]
    public void Courses_progress()
    {
        var c = Courses.Get("erste-schritte");
        var d = new PaintDoc(20, 20); d.Add();
        var s0 = Courses.Of(d, "aus", "aus", 0, 0, false);
        Assert.Equal(0, Courses.Progress(c, s0));
        new StrokeEngine(d.Layers[0].Px, BrushPreset.Get("tusche"), "#000000").Add(5, 5);
        Assert.Equal(1, Courses.Progress(c, Courses.Of(d, "aus", "aus", 0, 1, false)));
        d.Add(); Assert.Equal(2, Courses.Progress(c, Courses.Of(d, "aus", "aus", 0, 1, false)));
        Assert.True(Courses.All.Length >= 5); Assert.All(Courses.All, k => Assert.NotEmpty(k.Steps));
        Assert.Throws<ArgumentException>(() => Courses.Get("gibtsnicht"));
    }

    [Fact(DisplayName = "STUDIO-ART-01 Adapter: Malvorlage mit Format, Spiegeln, Palette und Schritten – ohne Nebenwirkung, Pflichtfeld Motiv")]
    public void Adapter()
    {
        var all = PromptActions.CreateDefault(); var ad = all.Single(a => a.StudioId == "STUDIO-ART-01");
        Assert.False(ad.CanPerformSideEffect); Assert.Equal(SideEffectClass.None, ad.Effect);
        Assert.NotEmpty(ad.Missing(new Dictionary<string, string>()));
        Assert.Equal(CapabilityLevel.Partial, CapabilityCatalog.Level("STUDIO-ART-01", all));
        Assert.NotEmpty(ad.Validate(new Dictionary<string, string> { ["motiv"] = "x", ["farbe"] = "blau" }));
        Assert.NotEmpty(ad.Validate(new Dictionary<string, string> { ["motiv"] = "x", ["teile"] = "99" }));
        var root = Path.Combine(Path.GetTempPath(), "trendo-kunst-ad-" + Guid.NewGuid().ToString("N")[..8]); Directory.CreateDirectory(root);
        try
        {
            var core = new TrendoCore(Path.Combine(root, "daten"), new InMemorySecretStore(), offline: true);
            var project = core.Projects.Create("probe", "Probe").Id;
            var wf = new StudioWorkflow(core, all);
            var ctx = new StudioExecutionContext(project, Principal.LocalOwner, CancellationToken.None, ResourceBudget.Default);
            var req = new StudioPromptRequest("kunst-1", project, "STUDIO-ART-01", "Motiv: Fuchs im Schnee\nFormat: quadrat\nFarbe: #FF0000\nHarmonie: triade\nTeile: 8", [], "de", null, PrivacyMode.Local, new Provenance("test", null, null));
            var done = wf.Los(project, wf.Start(req, ctx).RunId, ctx);
            Assert.Equal(RunStatus.Verified, done.Status);
            var text = File.ReadAllText(core.Projects.ResolveInProject(project, done.Artifacts.Single(a => a.RelativePath.EndsWith("malvorlage.txt")).RelativePath));
            Assert.Contains("Quadrat (1600 × 1600 px)", text); Assert.Contains("radial, 8 Teile", text);
            Assert.Contains("#FF0000, #00FF00, #0000FF", text); Assert.Contains("GIMP Palette", text);
        }
        finally { Directory.Delete(root, true); }
    }
}
