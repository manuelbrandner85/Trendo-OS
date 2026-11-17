using System.IO.Compression;
using System.Text;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using Trendo.Core.Kreativ;
using Trendo.Core.PromptAction;
using Trendo.Core.Sys;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>PDF-Werkstatt (STUDIO-PDFTOOLS-01, T-0672): Prüfungen aus docs/nachbau/pdf-werkstatt/anforderungen.md.</summary>
public class PdfWerkstattTests : IDisposable
{
    readonly string _dir = Path.Combine(Path.GetTempPath(), "trendo-pdfw-" + Guid.NewGuid().ToString("N")[..8]);
    public PdfWerkstattTests() { Directory.CreateDirectory(_dir); PdfTools.EnsureFonts(); }
    public void Dispose() { try { Directory.Delete(_dir, true); } catch (IOException) { } }

    string F(string name) => Path.Combine(_dir, name);

    /// <summary>Test-PDF mit Text je Seite (A4).</summary>
    internal static string TextPdf(string path, params string[] pages)
    {
        PdfTools.EnsureFonts();
        using var d = new PdfDocument();
        foreach (var t in pages)
        {
            var p = d.AddPage(); p.Size = PdfSharp.PageSize.A4;
            using var g = XGraphics.FromPdfPage(p); var font = new XFont("Arial", 14); var y = 80.0;
            foreach (var line in t.Split('\n')) { g.DrawString(line, font, XBrushes.Black, 60, y); y += 22; }
        }
        d.Save(path); return path;
    }

    static string PngFile(string path, int w, int h, bool noise = true)
    {
        var px = new Pixmap(w, h); var rnd = new Random(7);
        for (var y = 0; y < h; y++) for (var x = 0; x < w; x++)
        {
            var i = (y * w + x) * 4; var n = noise ? rnd.Next(0, 40) : 0;
            px.Px[i] = (byte)Math.Min(255, x * 255 / w + n); px.Px[i + 1] = (byte)Math.Min(255, y * 255 / h + n); px.Px[i + 2] = 120; px.Px[i + 3] = 255;
        }
        File.WriteAllBytes(path, PngCodec.Encode(px, fast: false)); return path;
    }

    static int Pages(string f) { using var d = PdfReader.Open(f, PdfDocumentOpenMode.Import); return d.PageCount; }

    /// <summary>Alle Inhalts-Streams einer Seite entpackt (zum Prüfen, ob Text-Befehle da sind).</summary>
    static string Content(string f, int page)
    {
        using var d = PdfReader.Open(f, PdfDocumentOpenMode.Import);
        var sb = new StringBuilder();
        foreach (var c in d.Pages[page].Contents.Elements.Select(e => (e as PdfSharp.Pdf.Advanced.PdfReference)?.Value as PdfDictionary).OfType<PdfDictionary>())
            sb.Append(Encoding.Latin1.GetString(c.Stream.UnfilteredValue));
        return sb.ToString();
    }

    [Fact(DisplayName = "PW-01: Zusammenfügen und Teilen nach Bereichen „1-2; 3; 4-z“ – drei Dateien, Original unverändert")]
    public void Merge_and_split_ranges()
    {
        var a = TextPdf(F("a.pdf"), "A1", "A2"); var b = TextPdf(F("b.pdf"), "B1", "B2", "B3");
        var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(a)));
        Assert.Equal(5, PdfTools.Merge([a, b], F("ab.pdf")));
        var parts = PdfTools.SplitRanges(F("ab.pdf"), F("teile"), "1-2; 3; 4-z");
        Assert.Equal(3, parts.Count); Assert.Equal([2, 1, 2], parts.Select(Pages));
        Assert.EndsWith("ab-teil1.pdf", parts[0]);
        // zweiter Lauf überschreibt nicht, sondern nimmt freie Namen
        var again = PdfTools.SplitRanges(F("ab.pdf"), F("teile"), "1");
        Assert.EndsWith("ab-teil1 (2).pdf", again[0]);
        Assert.Throws<ArgumentException>(() => PdfTools.SplitRanges(F("ab.pdf"), F("teile"), "1-9"));
        Assert.Equal(hash, Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(a))));
        // alle n Seiten (vorhandener Kern von Trendo PDF)
        Assert.Equal(3, PdfTools.Split(F("ab.pdf"), F("je2"), 2).Count);
    }

    [Fact(DisplayName = "PW-02: Seiten ordnen, drehen, löschen in einem Schritt (Rearrange)")]
    public void Organize()
    {
        var a = TextPdf(F("o.pdf"), "1", "2", "3", "4");
        var n = PdfTools.Rearrange([new(a, 3), new(a, 0, 90), new(a, 2)], F("o2.pdf"));
        Assert.Equal(3, n); Assert.Equal(90, PdfTools.PageRotation(F("o2.pdf"), 1)); Assert.Equal(0, PdfTools.PageRotation(F("o2.pdf"), 0));
    }

    [Fact(DisplayName = "PW-03: Bilder zu PDF – A4 mit Rand (Querbild → Querformat) und Seite in Bildgröße")]
    public void Images_to_pdf()
    {
        var quer = PngFile(F("quer.png"), 400, 200, false); var hoch = PngFile(F("hoch.png"), 200, 400, false);
        Assert.Equal(2, PdfTools.ImagesToPdf([quer, hoch], F("bilder.pdf")));
        var info = PdfTools.Read(F("bilder.pdf"));
        Assert.True(info.PageSizes[0].Width > info.PageSizes[0].Height); Assert.True(info.PageSizes[1].Width < info.PageSizes[1].Height);
        Assert.Equal(1, PdfTools.ImagesToPdf([quer], F("bild.pdf"), "bild"));
        Assert.Equal(300, PdfTools.Read(F("bild.pdf")).PageSizes[0].Width, 0);
        Assert.Throws<ArgumentException>(() => PdfTools.ImagesToPdf([F("fehlt.png")], F("x.pdf")));
    }

    [Fact(DisplayName = "PW-04: Komprimieren verkleinert große Bilder messbar (Bytes vorher/nachher), ohne Bilder ehrlich „nicht kleiner“")]
    public void Compress_measured()
    {
        var png = PngFile(F("gross.png"), 2600, 2600);
        PdfTools.ImagesToPdf([png], F("gross.pdf"));
        var r = PdfTools.Compress(F("gross.pdf"), F("klein.pdf"), CompressLevel.Get("stark"));
        Assert.Equal(new FileInfo(F("gross.pdf")).Length, r.Before);
        Assert.Equal(new FileInfo(F("klein.pdf")).Length, r.After);
        Assert.True(r.After < r.Before / 2, $"{r.Before} → {r.After}");
        Assert.Equal(1, r.Images); Assert.Equal(1, r.Recoded); Assert.False(r.KeptOriginal);
        Assert.Equal(1, Pages(F("klein.pdf")));
        // Bild ist jetzt höchstens 96 dpi bezogen auf A4 (≈ 1123 px)
        using (var d = PdfReader.Open(F("klein.pdf"), PdfDocumentOpenMode.Import))
        {
            var img = d.Internals.GetAllObjects().OfType<PdfDictionary>().Single(o => o.Elements.GetName("/Subtype") == "/Image" && !o.Elements.ContainsKey("/SMask") && o.Elements.GetInteger("/Width") > 100);
            Assert.True(img.Elements.GetInteger("/Width") <= 1123);
        }
        // reine Textdatei: nicht kleiner → Kopie des Originals, ehrlich gemeldet
        var t = TextPdf(F("text.pdf"), "Hallo");
        var r2 = PdfTools.Compress(t, F("text-klein.pdf"), CompressLevel.Get("mittel"));
        Assert.True(r2.After <= r2.Before); Assert.True(File.Exists(F("text-klein.pdf")));
        if (r2.KeptOriginal) Assert.Contains("kleiner", r2.Note);
        // JPEG-Kodierer der App wird benutzt, wenn er kleiner ist
        var calls = 0;
        var r3 = PdfTools.Compress(F("gross.pdf"), F("klein-jpg.pdf"), CompressLevel.Get("mittel"), (px, w, h, c, q) => { calls++; return null; });
        Assert.Equal(1, calls); Assert.True(r3.After < r3.Before);
    }

    [Fact(DisplayName = "PW-05: Wasserzeichen und Stempel landen auf den gewählten Seiten, Original bleibt")]
    public void Watermark_and_stamp()
    {
        var a = TextPdf(F("w.pdf"), "Eins", "Zwei", "Drei");
        var before = Content(a, 1);
        PdfTools.Watermark(a, F("w2.pdf"), "VERTRAULICH", spec: "2-3");
        Assert.Equal(before, Content(a, 1));
        Assert.True(Content(F("w2.pdf"), 1).Length > before.Length);
        Assert.Equal(Content(a, 0), Content(F("w2.pdf"), 0)); // Seite 1 nicht berührt
        Assert.Contains("/ca 0.18", File.ReadAllText(F("w2.pdf"), Encoding.Latin1).Replace("/CA", "/ca")); // halbdurchsichtig (ExtGState)
        PdfTools.Stamp(a, F("s.pdf"), "Geprüft", "unten-rechts", date: new DateTime(2026, 11, 15));
        Assert.True(Content(F("s.pdf"), 0).Length > Content(a, 0).Length);
        Assert.Throws<ArgumentException>(() => PdfTools.Watermark(a, F("x.pdf"), " "));
        Assert.Throws<ArgumentException>(() => PdfTools.Watermark(a, a, "X"));
    }

    [Fact(DisplayName = "PW-06: Seitenzahlen „Seite {n} von {N}“, Deckblatt auslassen, Startzahl")]
    public void Page_numbers()
    {
        Assert.Equal("Seite 2 von 5", PdfTools.PageLabel("Seite {n} von {N}", 2, 5));
        Assert.Equal("7", PdfTools.PageLabel("", 7, 9));
        var a = TextPdf(F("n.pdf"), "Deckblatt", "Inhalt", "Ende");
        Assert.Equal(2, PdfTools.PageNumbers(a, F("n2.pdf"), skipFirst: true));
        Assert.Equal(Content(a, 0), Content(F("n2.pdf"), 0));
        Assert.NotEqual(Content(a, 1), Content(F("n2.pdf"), 1));
        Assert.Equal(3, PdfTools.PageNumbers(a, F("n3.pdf"), "{n}", "oben-rechts", start: 10));
    }

    [Fact(DisplayName = "PW-07: Metadaten bearbeiten und vollständig entfernen (auch „erzeugt von“ und XMP)")]
    public void Metadata()
    {
        var a = TextPdf(F("m.pdf"), "x");
        PdfTools.WriteMeta(a, F("m2.pdf"), new Dictionary<string, string> { ["Title"] = "Jahresbericht", ["Author"] = "Erika Muster", ["Keywords"] = "Bericht; 2026" });
        var (info, _) = PdfTools.ReadMeta(F("m2.pdf"));
        Assert.Equal("Jahresbericht", info["Title"]); Assert.Equal("Erika Muster", info["Author"]);
        PdfTools.RemoveMeta(F("m2.pdf"), F("m3.pdf"));
        var (none, xmp) = PdfTools.ReadMeta(F("m3.pdf"));
        Assert.Empty(none); Assert.False(xmp);
        var raw = File.ReadAllText(F("m3.pdf"), Encoding.Latin1);
        Assert.DoesNotContain("Muster", raw); Assert.DoesNotContain("PDFsharp", raw);
        Assert.Equal(1, Pages(F("m3.pdf"))); // Datei bleibt gültig
    }

    [Fact(DisplayName = "PW-08: Passwort setzen und mit bekanntem Passwort entfernen; falsches Passwort scheitert")]
    public void Password()
    {
        var a = TextPdf(F("p.pdf"), "geheim");
        PdfTools.Encrypt(a, F("p2.pdf"), "Probe-123");
        Assert.True(PdfTools.Read(F("p2.pdf")).NeedsPassword);
        Assert.ThrowsAny<Exception>(() => PdfTools.Decrypt(F("p2.pdf"), F("p3.pdf"), "falsch"));
        PdfTools.Decrypt(F("p2.pdf"), F("p3.pdf"), "Probe-123");
        Assert.False(PdfTools.Read(F("p3.pdf")).NeedsPassword);
    }

    [Fact(DisplayName = "PW-09: Schwärzen ersetzt die Seite durch ein Bild – der Text darunter ist nicht mehr in der Datei, andere Seiten bleiben Text")]
    public void Redact_removes_content()
    {
        var a = TextPdf(F("r.pdf"), "Kontonummer 123456789", "Öffentlich");
        Assert.Contains("Tj", Content(a, 0));
        var asked = new List<(int, int)>();
        byte[] Render(int page, int dpi) { asked.Add((page, dpi)); var px = new Pixmap(1240, 1754); px.Fill(255, 255, 255, 255); return PngCodec.Encode(px); }
        var n = PdfTools.Redact(a, F("r2.pdf"), [new RedactBox(0, 50, 60, 300, 30)], Render);
        Assert.Equal(1, n); Assert.Equal([(0, 150)], asked);
        Assert.Equal(2, Pages(F("r2.pdf")));
        var c0 = Content(F("r2.pdf"), 0);
        Assert.DoesNotContain("Tj", c0); Assert.DoesNotContain("TJ", c0); Assert.Contains("Do", c0); // nur noch ein Bild
        Assert.Contains("Tj", Content(F("r2.pdf"), 1));
        Assert.Throws<ArgumentException>(() => PdfTools.Redact(a, F("r3.pdf"), [], Render));
        Assert.Throws<ArgumentException>(() => PdfTools.Redact(a, F("r3.pdf"), [new RedactBox(5, 0, 0, 10, 10)], Render));
    }

    [Fact(DisplayName = "PW-10: Vergleich zweier PDFs: neue, entfernte und gleiche Zeilen mit Seitenangabe; Leerzeichen zählen nicht")]
    public void Compare()
    {
        var c = PdfTools.Compare(["Angebot\nPreis: 100 Euro\nLieferung", "Seite zwei"], ["Angebot\nPreis:  120 Euro\nLieferung", "Seite zwei", "Anhang"]);
        Assert.Equal(1, c.Removed); Assert.Equal(2, c.Added); Assert.Equal(3, c.Same); Assert.False(c.Identical);
        Assert.Contains(c.Lines, l => l.Op == '−' && l.Text == "Preis: 100 Euro" && l.PageA == 1);
        Assert.Contains(c.Lines, l => l.Op == '+' && l.Text == "Anhang" && l.PageB == 3);
        Assert.True(PdfTools.Compare(["a  b\n\n"], ["a b"]).Identical);
        var txt = PdfTools.CompareText(c, "alt.pdf", "neu.pdf");
        Assert.Contains("− A S.1: Preis: 100 Euro", txt); Assert.Contains("+ B S.3: Anhang", txt);
    }

    [Fact(DisplayName = "STUDIO-PDFTOOLS-01 Adapter: Arbeitsauftrag mit aufgelösten Seiten, Pflichtfeld Aktion, falsche Seitenangabe wird gemeldet")]
    public void Adapter()
    {
        var all = PromptActions.CreateDefault(); var ad = all.Single(a => a.StudioId == "STUDIO-PDFTOOLS-01");
        Assert.False(ad.CanPerformSideEffect); Assert.Equal(SideEffectClass.None, ad.Effect);
        Assert.NotEmpty(ad.Missing(new Dictionary<string, string>()));
        Assert.Equal(CapabilityLevel.Partial, CapabilityCatalog.Level("STUDIO-PDFTOOLS-01", all));
        Assert.NotEmpty(ad.Validate(new Dictionary<string, string> { ["aktion"] = "zaubern" }));
        Assert.NotEmpty(ad.Validate(new Dictionary<string, string> { ["aktion"] = "teilen", ["seiten"] = "1-x", ["seitenzahl"] = "5" }));
        var root = Path.Combine(_dir, "ad");
        var core = new TrendoCore(Path.Combine(root, "daten"), new InMemorySecretStore(), offline: true);
        var project = core.Projects.Create("probe", "Probe").Id;
        var wf = new StudioWorkflow(core, all);
        var ctx = new StudioExecutionContext(project, Principal.LocalOwner, CancellationToken.None, ResourceBudget.Default);
        var req = new StudioPromptRequest("pdfw-1", project, "STUDIO-PDFTOOLS-01", "Aktion: teilen\nSeiten: 1-3; 4-z\nSeitenzahl: 10", [], "de", null, PrivacyMode.Local, new Provenance("test", null, null));
        var done = wf.Los(project, wf.Start(req, ctx).RunId, ctx);
        Assert.Equal(RunStatus.Verified, done.Status);
        var text = File.ReadAllText(core.Projects.ResolveInProject(project, done.Artifacts.Single(a => a.RelativePath.EndsWith("pdf-auftrag.txt")).RelativePath));
        Assert.Contains("Teilen nach Bereichen", text); Assert.Contains("Datei 1: Seiten 1, 2, 3", text); Assert.Contains("Datei 2: Seiten 4, 5, 6, 7, 8, 9, 10", text);
    }
}
