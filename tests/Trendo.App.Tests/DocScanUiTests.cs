using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Trendo.App;
using Trendo.Core;
using Trendo.Core.Kreativ;
using Trendo.Core.Office;
using Trendo.Core.Programs;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// <summary>Dokumente scannen (STUDIO-DOCSCAN-01): Testfotos werden selbst erzeugt (schiefes Blatt auf dunklem Tisch); echte Klicks auf Menüband, Seitenleiste und Ecken.</summary>
public class DocScan_Ui
{
    const string K = MainWindow.ScanKey;
    static readonly (double X, double Y)[] Quad = [(130, 45), (465, 75), (505, 410), (85, 380)];
    static void Tap(MainWindow w, string name) { var b = w.Find<Button>(name); b.BringIntoView(); Settle(); RealClick(w, b); Settle(); }
    static ProgramDocument Open(MainWindow w, string tpl = "leer")
    {
        w.ProgramSetLevel(UserLevel.Profi); w.Show(K); Settle();
        RealClick(w, w.Find<Button>("prog-tpl-" + tpl)); Settle();
        return w.ProgramActive(K)!;
    }
    static string Photo(string dir, string name, bool blank = false)
    {
        Directory.CreateDirectory(dir); var f = Path.Combine(dir, name);
        Pixmap p; if (blank) { p = new Pixmap(600, 450); p.Fill(250, 250, 250, 255); } else p = DocScan.TestPhoto(600, 450, Quad);
        File.WriteAllBytes(f, PngCodec.Encode(p)); return f;
    }

    [AvaloniaFact(DisplayName = "Dokumente scannen: eigene Seite; Fotos über „Fotos hinzufügen“ – Blatt erkannt, Ergebnis entzerrt; als PDF mit Datum + Titel abgelegt, nichts überschrieben")]
    public void Add_and_save_pdf()
    {
        var (w, _, root) = Ui.Open(); w.Width = 1500; w.Height = 950;
        var dir = Path.Combine(root, "fotos");
        var a = Photo(dir, "seite1.png"); var b = Photo(dir, "seite2.png"); var blank = Photo(dir, "rueckseite.png", blank: true);
        w.PickOverride = _ => Task.FromResult<string[]?>([a, blank, b]);
        try
        {
            Assert.Equal(K, MainWindow.StudioPages["STUDIO-DOCSCAN-01"]);
            var d = Open(w);
            Assert.NotNull(w.Find<Button>("scan-empty-add"));
            Tap(w, "prog-cmd-scan-add");
            var pages = w.ScanPages(d); Assert.Equal(3, pages.Count);
            Assert.True(pages[0].Confidence > 0.8); Assert.NotNull(pages[0].Corners);
            Assert.Contains("Blattkanten bei 2 von 3", w.ProgramMessage(K));
            Assert.NotNull(w.Find<ScanCornerView>("scan-corners")); Assert.NotNull(w.Find<Image>("scan-result"));
            var img = w.ScanPageImage(d, 0, 600);
            Assert.True(img.W > img.H); Assert.Equal(255, img.At(3, 3).R); // Ecke ist Papier, nicht Tisch
            d.Set("title", "Rechnung Stadtwerke", "Titel"); d.Set("date", "2026-11-14", "Datum");
            Tap(w, "prog-cmd-scan-save");
            var pdf = Path.Combine(w.ScanFolder(d), "2026-11-14 Rechnung Stadtwerke.pdf");
            Assert.True(File.Exists(pdf)); Assert.EndsWith(Path.Combine("Scans", "Rechnungen"), w.ScanFolder(d));
            using (var doc = PdfSharp.Pdf.IO.PdfReader.Open(pdf, PdfSharp.Pdf.IO.PdfDocumentOpenMode.Import)) Assert.Equal(2, doc.PageCount); // leere Rückseite weggelassen
            Assert.Contains("1 leere Seite", w.ProgramMessage(K));
            Tap(w, "prog-cmd-scan-save");
            Assert.True(File.Exists(Path.Combine(w.ScanFolder(d), "2026-11-14 Rechnung Stadtwerke (2).pdf")));
            Assert.True(File.Exists(a)); // Foto unverändert da
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "SC-06: Seiten drehen, ordnen, entfernen und Modus je Seite/für alle – über Menüband, Werkzeugleiste und Seitenleiste")]
    public void Pages_rotate_order()
    {
        var (w, _, root) = Ui.Open(); w.Width = 1500; w.Height = 950;
        try
        {
            var d = Open(w);
            var dir = Path.Combine(root, "f"); w.ScanAddFiles(d, [Photo(dir, "a.png"), Photo(dir, "b.png")]); w.Show(K); Settle();
            var first = w.ScanPages(d)[0].Id;
            Tap(w, "scan-thumb-0");
            var before = w.ScanPageImage(d, 0, 600);
            Tap(w, "prog-cmd-scan-rotate"); Assert.Equal(1, w.ScanPages(d)[0].Rotate);
            var after = w.ScanPageImage(d, 0, 600); Assert.Equal((before.H, before.W), (after.W, after.H));
            Tap(w, "prog-cmd-scan-down"); Assert.Equal(first, w.ScanPages(d)[1].Id); Assert.Equal(1, w.ScanSelected(d));
            Tap(w, "prog-cmd-scan-mode-whiteboard"); Assert.Equal("whiteboard", w.ScanPages(d)[1].Mode); Assert.Null(w.ScanPages(d)[0].Mode);
            Tap(w, "scan-all-grau"); Assert.Equal("grau", d.Text("mode")); Assert.All(w.ScanPages(d), p => Assert.Null(p.Mode));
            var g = w.ScanPageImage(d, 0, 400).At(200, 100); Assert.Equal(g.R, g.B);
            w.ScanCrop(d, 0, 10, 0, 10, 0); var cropped = w.ScanPageImage(d, 0, 600); Assert.True(cropped.W < w.ScanPageImage(d, 1, 600).H);
            Tap(w, "prog-cmd-scan-del"); Assert.Single(w.ScanPages(d));
            Assert.True(d.Undo()); Assert.Equal(2, w.ScanPages(d).Count);
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "SC-09: Ecke im Original mit der Maus ziehen ändert das Viereck; „Ganzes Bild“ und „Kanten finden“")]
    public void Corners_by_hand()
    {
        var (w, _, root) = Ui.Open(); w.Width = 1500; w.Height = 950;
        try
        {
            var d = Open(w);
            w.ScanAddFiles(d, [Photo(Path.Combine(root, "c"), "a.png")]); w.Show(K); Settle();
            var v = w.Find<ScanCornerView>("scan-corners"); v.BringIntoView(); Settle(); w.UpdateLayout(); Settle();
            var tl = w.ScanPages(d)[0].Corners![0];
            var from = v.TranslatePoint(v.ToScreen(tl), w)!.Value;
            var r = v.ImageRect(); var to = v.TranslatePoint(new Point(r.X + 0.02 * r.Width, r.Y + 0.03 * r.Height), w)!.Value;
            var hit = w.InputHitTest(from) as Visual;
            Assert.True(hit == v || (hit is not null && Avalonia.VisualTree.VisualExtensions.GetVisualAncestors(hit).Contains(v)), $"Ecke liegt unter {hit?.GetType().Name} {(hit as Control)?.Name}");
            w.MouseDown(from, MouseButton.Left); w.MouseMove(from + (to - from) * 0.5); w.MouseMove(to); w.MouseUp(to, MouseButton.Left); Settle();
            var c = w.ScanPages(d)[0].Corners!;
            Assert.True(Math.Abs(c[0].X - 0.02) < 0.02 && Math.Abs(c[0].Y - 0.03) < 0.02, $"Ecke {c[0]}");
            Tap(w, "prog-cmd-scan-full"); Assert.Null(w.ScanPages(d)[0].Corners);
            Tap(w, "prog-cmd-scan-auto"); Assert.NotNull(w.ScanPages(d)[0].Corners); Assert.Contains("Blatt gefunden", w.ProgramMessage(K));
            Assert.Throws<InvalidOperationException>(() => w.ScanSetCorners(d, 0, [(0.5, 0.5), (0.51, 0.5), (0.51, 0.51), (0.5, 0.51)]));
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "SC-10/11/12: Ordner einlesen (natürlich sortiert), Texterkennung ohne Tesseract mit Einrichtungs-Hinweis, mit Tesseract durchsuchbare PDF; Scanner-Hinweis")]
    public async Task Folder_and_ocr_hint()
    {
        var (w, _, root) = Ui.Open(); w.Width = 1500; w.Height = 950;
        try
        {
            var d = Open(w);
            var dir = Path.Combine(root, "ordner"); Photo(dir, "seite10.png"); Photo(dir, "seite2.png"); File.WriteAllText(Path.Combine(dir, "notiz.txt"), "x");
            MainWindow.FolderPickOverride = _ => dir;
            Tap(w, "prog-cmd-scan-folder");
            Assert.Equal(["seite2.png", "seite10.png"], w.ScanPages(d).Select(p => Path.GetFileName(p.Src)));
            MainWindow.ScanTesseractOverride = () => null;
            Assert.Null(await w.ScanSearchablePdf(d));
            Assert.Contains("Tesseract", w.ProgramMessage(K)); Assert.Contains("einrichten", w.ProgramMessage(K));
            // mit (ersetztem) Tesseract: je Seite eine PDF, zusammengefügt
            MainWindow.ScanTesseractOverride = () => "tesseract";
            MainWindow.ScanOcrRunner = new FakeTesseract();
            d.Set("title", "Brief", "Titel");
            var f = await w.ScanSearchablePdf(d);
            Assert.NotNull(f); Assert.True(File.Exists(f));
            using (var doc = PdfSharp.Pdf.IO.PdfReader.Open(f!, PdfSharp.Pdf.IO.PdfDocumentOpenMode.Import)) Assert.Equal(2, doc.PageCount);
            Tap(w, "prog-cmd-scan-scanner"); Assert.Contains("WIA/TWAIN", w.ProgramMessage(K));
        }
        finally { MainWindow.FolderPickOverride = null; MainWindow.ScanTesseractOverride = null; MainWindow.ScanOcrRunner = null; w.Close(); }
    }

    /// <summary>Tut so, als wäre es Tesseract: „--list-langs“ nennt deu, sonst entsteht aus dem Bild eine einseitige PDF.</summary>
    sealed class FakeTesseract : IProcessRunner
    {
        public Task<(int Exit, string Output)> RunAsync(string file, string args, Action<string>? line = null, CancellationToken ct = default)
        {
            if (args.Contains("--list-langs")) return Task.FromResult((0, "List of available languages (2):\ndeu\neng"));
            var m = System.Text.RegularExpressions.Regex.Matches(args, "\"([^\"]+)\"");
            var outBase = m[1].Groups[1].Value;
            using var pdf = new PdfSharp.Pdf.PdfDocument(); pdf.AddPage(); pdf.Save(outBase + ".pdf");
            return Task.FromResult((0, ""));
        }
    }
}
