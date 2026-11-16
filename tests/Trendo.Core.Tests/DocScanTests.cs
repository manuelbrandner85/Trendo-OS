using Trendo.Core.Kreativ;
using Trendo.Core.Office;
using Trendo.Core.PromptAction;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>Dokumente scannen (STUDIO-DOCSCAN-01): Prüfungen aus docs/nachbau/scannen/anforderungen.md. Testbilder werden selbst erzeugt.</summary>
public class DocScanTests
{
    static readonly (double X, double Y)[] Quad = [(130, 45), (465, 75), (505, 410), (85, 380)];
    static Pixmap Photo(bool shadow = true) => DocScan.TestPhoto(600, 450, Quad, shadow);
    static byte G(Pixmap p, int x, int y) => p.At(x, y).R;

    [Fact(DisplayName = "SC-01: Blattkanten finden – Ecken des schiefen Blatts auf wenige Bildpunkte genau, Sicherheit hoch")]
    public void Find_page()
    {
        var c = DocScan.FindPage(Photo(), out var conf);
        Assert.True(conf > 0.8, $"Sicherheit {conf}");
        for (var i = 0; i < 4; i++) Assert.True(Math.Abs(c[i].X - Quad[i].X) < 8 && Math.Abs(c[i].Y - Quad[i].Y) < 8, $"Ecke {i}: {c[i]} statt {Quad[i]}");
        // ohne Blatt (einfarbig): ganzes Bild, Sicherheit 0
        var plain = new Pixmap(200, 100); plain.Fill(120, 120, 120, 255);
        Assert.Equal(DocScan.FullFrame(200, 100), DocScan.FindPage(plain, out var c0)); Assert.Equal(0, c0);
    }

    [Fact(DisplayName = "SC-02: Ecken ordnen und Zielgröße – Reihenfolge egal, DIN-A-Verhältnis wird genau A-Format")]
    public void Order_and_size()
    {
        var o = DocScan.Order([Quad[2], Quad[0], Quad[3], Quad[1]]);
        Assert.Equal(Quad, o);
        var (w, h, din) = DocScan.TargetSize([(0, 0), (1000, 0), (1000, 1400), (0, 1400)]);
        Assert.True(din); Assert.Equal(1000, w); Assert.Equal(1414, h);
        var (w2, h2, din2) = DocScan.TargetSize([(0, 0), (1000, 0), (1000, 1000), (0, 1000)]);
        Assert.False(din2); Assert.Equal((1000, 1000), (w2, h2));
        Assert.Throws<ArgumentException>(() => DocScan.Order([(0, 0), (1, 1)]));
    }

    [Fact(DisplayName = "SC-03: Entzerren – Tisch verschwindet, Textzeilen liegen danach waagrecht (gleiche Zeile über die ganze Breite)")]
    public void Warp_straightens()
    {
        var photo = Photo(shadow: false);
        var c = DocScan.FindPage(photo, out _);
        var flat = DocScan.Warp(photo, c, 400, 520);
        // Ecken sind Papier (hell), nicht Tisch (dunkel)
        foreach (var (x, y) in new[] { (3, 3), (396, 3), (396, 516), (3, 516) }) Assert.True(G(flat, x, y) > 200, $"Tisch bei {x},{y}");
        // Textzeile: in Zeile v = 0,208 (dunkel) bei 20 % und 80 % der Breite gleich dunkel; dazwischen-Zeile hell
        int yLine = (int)(0.208 * 520), yGap = (int)(0.26 * 520);
        Assert.True(G(flat, 100, yLine) < 120 && G(flat, 300, yLine) < 120, "Zeile nicht waagrecht");
        Assert.True(G(flat, 100, yGap) > 200 && G(flat, 300, yGap) > 200);
    }

    [Fact(DisplayName = "SC-04: Modus Dokument – adaptiver Schwellwert macht Schatten weiß und Schrift schwarz, nur zwei Werte")]
    public void Document_mode()
    {
        var photo = Photo(shadow: true);
        var page = DocScan.Process(photo, DocScan.FindPage(photo, out _), 0, "dokument", null, 800);
        var vals = new HashSet<byte>(); for (var i = 0; i < page.Px.Length; i += 4) vals.Add(page.Px[i]);
        Assert.Equal(new byte[] { 0, 255 }, vals.OrderBy(v => v).ToArray());
        // im Schatten (unten rechts) ist Papier weiß, nicht grau
        Assert.Equal(255, G(page, page.W * 95 / 100, page.H * 95 / 100));
        Assert.Equal(255, G(page, page.W / 2, (int)(page.H * 0.26)));
        Assert.Equal(0, G(page, page.W / 2, (int)(page.H * 0.808)));
    }

    [Fact(DisplayName = "SC-05: Modus Whiteboard, Graustufen, Foto – Tafel wird weiß, Stiftfarbe bleibt kräftig; Graustufen ohne Farbe")]
    public void Other_modes()
    {
        var wb = new Pixmap(200, 120);
        for (var y = 0; y < 120; y++) for (var x = 0; x < 200; x++) { var j = (y * 200 + x) * 4; var b = (byte)(150 + x / 4); wb.Px[j] = b; wb.Px[j + 1] = b; wb.Px[j + 2] = (byte)(b - 10); wb.Px[j + 3] = 255; }
        for (var x = 20; x < 180; x++) for (var y = 58; y < 62; y++) { var j = (y * 200 + x) * 4; wb.Px[j] = 40; wb.Px[j + 1] = 60; wb.Px[j + 2] = 200; }
        var w = DocScan.Filter(wb, "whiteboard");
        Assert.Equal((255, 255, 255), (w.At(10, 10).R, w.At(10, 10).G, w.At(190, 110).B));
        var ink = w.At(100, 60); Assert.True(ink.B > ink.R + 80, "Blau nicht kräftig");
        var g = DocScan.Filter(Photo(), "grau"); var p = g.At(300, 200); Assert.Equal(p.R, p.G); Assert.Equal(p.G, p.B);
        var f = DocScan.Filter(Photo(), "foto"); Assert.Equal(600, f.W);
        Assert.Same(typeof(Pixmap), DocScan.Filter(Photo(), "original").GetType());
        Assert.Throws<ArgumentException>(() => DocScan.Filter(wb, "sepia"));
    }

    [Fact(DisplayName = "SC-06: Drehen und Zuschneiden – 90° tauscht Breite und Höhe, viermal drehen ergibt das Original; Rand in Prozent")]
    public void Rotate_crop()
    {
        var p = new Pixmap(4, 2); p.Px[0] = 255; // oben links rot
        var r = DocScan.Rotate(p, 1); Assert.Equal((2, 4), (r.W, r.H)); Assert.Equal(255, r.At(1, 0).R); // oben rechts
        Assert.Equal(p.Px, DocScan.Rotate(DocScan.Rotate(r, 2), 1).Px);
        Assert.Equal(255, DocScan.Rotate(p, -1).At(0, 3).R);
        var big = new Pixmap(100, 200); var c = DocScan.Crop(big, 10, 25, 10, 0);
        Assert.Equal((80, 150), (c.W, c.H));
        Assert.Equal((10, 110), (DocScan.Crop(big, 45, 0, 45, 0).W, 200 - 90));
    }

    [Fact(DisplayName = "SC-07: Mehrseitige PDF – A4-Seiten (595 × 842 pt), Querformat bei breitem Bild, „wie das Bild“ nach dpi")]
    public void Pdf()
    {
        var dir = Path.Combine(Path.GetTempPath(), "trendo-scan-" + Guid.NewGuid().ToString("N")[..8]); Directory.CreateDirectory(dir);
        try
        {
            var a = new Pixmap(210, 297); a.Fill(255, 255, 255, 255); var b = new Pixmap(297, 210); b.Fill(0, 0, 0, 255);
            var f = Path.Combine(dir, "scan.pdf");
            Assert.Equal(2, DocScan.WritePdf([a, b], f, "Rechnung"));
            using (var doc = PdfSharp.Pdf.IO.PdfReader.Open(f, PdfSharp.Pdf.IO.PdfDocumentOpenMode.Import))
            {
                Assert.Equal(2, doc.PageCount);
                Assert.Equal((595, 842), ((int)doc.Pages[0].Width.Point, (int)doc.Pages[0].Height.Point));
                Assert.Equal((842, 595), ((int)doc.Pages[1].Width.Point, (int)doc.Pages[1].Height.Point));
            }
            var g = Path.Combine(dir, "auto.pdf"); DocScan.WritePdf([new Pixmap(200, 100)], g, "", "auto", 100);
            using (var doc = PdfSharp.Pdf.IO.PdfReader.Open(g, PdfSharp.Pdf.IO.PdfDocumentOpenMode.Import)) Assert.Equal(144, (int)Math.Round(doc.Pages[0].Width.Point));
            Assert.Throws<InvalidOperationException>(() => DocScan.WritePdf([], f, ""));
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact(DisplayName = "SC-08: Dateiname aus Datum + Titel, verbotene Zeichen raus, nie überschreiben, Ordner nach Art, leere Seite erkannt")]
    public void Naming_and_blank()
    {
        var d = new DateTime(2026, 11, 15);
        Assert.Equal("2026-11-15 Rechnung Stadtwerke.pdf", DocScan.FileName(d, "Rechnung: Stadtwerke"));
        Assert.Equal("2026-11-15 Scan.pdf", DocScan.FileName(d, "  "));
        Assert.Equal("2026-11-15 a b.pdf", DocScan.FileName(d, "a/b?"));
        Assert.True(DocScan.FileName(d, new string('x', 200)).Length <= 80);
        var dir = Path.Combine(Path.GetTempPath(), "trendo-scan-n-" + Guid.NewGuid().ToString("N")[..8]); Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "x.pdf"), "");
            Assert.Equal(Path.Combine(dir, "x (2).pdf"), DocScan.Unique(dir, "x.pdf"));
        }
        finally { Directory.Delete(dir, true); }
        Assert.Equal("Rechnungen", DocScan.Folder("Rechnung Stadtwerke")); Assert.Equal("Notizen", DocScan.Folder("Whiteboard Teamrunde")); Assert.Equal("Scans", DocScan.Folder("Irgendwas"));
        var white = new Pixmap(300, 400); white.Fill(250, 250, 250, 255); Assert.True(DocScan.IsBlank(white));
        var flat = DocScan.Process(Photo(false), DocScan.FindPage(Photo(false), out _), 0, "original", null, 600);
        Assert.False(DocScan.IsBlank(flat));
    }

    [Fact(DisplayName = "STUDIO-DOCSCAN-01 Adapter: Scan-Auftrag mit Dateiname und Ordner – ohne Nebenwirkung, Pflichtfeld Titel")]
    public void Adapter()
    {
        var all = PromptActions.CreateDefault(); var ad = all.Single(a => a.StudioId == "STUDIO-DOCSCAN-01");
        Assert.False(ad.CanPerformSideEffect); Assert.Equal(SideEffectClass.None, ad.Effect);
        Assert.NotEmpty(ad.Missing(new Dictionary<string, string>()));
        Assert.Equal(CapabilityLevel.Partial, CapabilityCatalog.Level("STUDIO-DOCSCAN-01", all));
        Assert.NotEmpty(ad.Validate(new Dictionary<string, string> { ["titel"] = "x", ["modus"] = "sepia" }));
        Assert.NotEmpty(ad.Validate(new Dictionary<string, string> { ["titel"] = "x", ["datum"] = "gestern" }));
        var root = Path.Combine(Path.GetTempPath(), "trendo-scan-ad-" + Guid.NewGuid().ToString("N")[..8]); Directory.CreateDirectory(root);
        try
        {
            var core = new TrendoCore(Path.Combine(root, "daten"), new InMemorySecretStore(), offline: true);
            var project = core.Projects.Create("probe", "Probe").Id;
            var wf = new StudioWorkflow(core, all);
            var ctx = new StudioExecutionContext(project, Principal.LocalOwner, CancellationToken.None, ResourceBudget.Default);
            var req = new StudioPromptRequest("scan-1", project, "STUDIO-DOCSCAN-01", "Titel: Rechnung Stadtwerke\nDatum: 14.11.2026\nModus: whiteboard\nSeiten: 2", [], "de", null, PrivacyMode.Local, new Provenance("test", null, null));
            var done = wf.Los(project, wf.Start(req, ctx).RunId, ctx);
            Assert.Equal(RunStatus.Verified, done.Status);
            var text = File.ReadAllText(core.Projects.ResolveInProject(project, done.Artifacts.Single(a => a.RelativePath.EndsWith("scan-auftrag.txt")).RelativePath));
            Assert.Contains("2026-11-14 Rechnung Stadtwerke.pdf", text); Assert.Contains("Scans/Rechnungen", text); Assert.Contains("Whiteboard", text);
        }
        finally { Directory.Delete(root, true); }
    }
}
