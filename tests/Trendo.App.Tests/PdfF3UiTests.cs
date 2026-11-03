using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using Trendo.App;
using Trendo.Core;
using Xunit;
using Path = System.IO.Path;

namespace Trendo.App.Tests;

/// <summary>F3 PDF (Vorbild-Analyse „Nächste Schritte“): Unterschrift einsetzen und Texterkennung im Programm.</summary>
public class PdfF3UiTests
{
    static void Pump() { for (var i = 0; i < 5; i++) Dispatcher.UIThread.RunJobs(); }
    static async Task Run(Task t) { while (!t.IsCompleted) { Dispatcher.UIThread.RunJobs(); await Task.Delay(20); } await t; }

    /// <summary>A4-Seite mit großem Text (Vektorschrift) – Vorlage für Unterschrift und Texterkennung.</summary>
    static string MakePdf(string dir)
    {
        MakeBlank(dir); // PdfTools richtet dabei die Schriften für PDFsharp ein
        var f = Path.Combine(dir, "rechnung.pdf");
        using var d = new PdfDocument(); var p = d.AddPage(); p.Width = XUnit.FromMillimeter(210); p.Height = XUnit.FromMillimeter(297);
        using (var g = XGraphics.FromPdfPage(p))
        {
            g.DrawString("Rechnung Nummer 4711", new XFont("Arial", 28), XBrushes.Black, 60, 120);
            g.DrawString("Bitte bis Freitag bezahlen", new XFont("Arial", 22), XBrushes.Black, 60, 180);
        }
        d.Save(f); return f;
    }
    static string MakeBlank(string dir) { var b = Path.Combine(dir, "leer.pdf"); Trendo.Core.Sys.PdfTools.NewBlank(b); return b; }

    [AvaloniaFact(DisplayName = "PDF F3: Unterschrift im Zeichenfeld zeichnen, dann auf die Seite klicken – fest als Bild in einer Kopie „(Trendo)“, Original unverändert")]
    public void Sign_pdf()
    {
        var (w, _, root) = Ui.Open();
        try
        {
            var src = MakePdf(root); var before = File.ReadAllBytes(src);
            w.OpenPdf(src); w.Show("pdf"); Pump();
            w.Find<Expander>("pdf-sign").IsExpanded = true; Pump();
            var pad = w.Find<Canvas>("pdf-sig-canvas");
            Point At(double x, double y) { pad.BringIntoView(); Pump(); return w.Find<Canvas>("pdf-sig-canvas").TranslatePoint(new Point(x, y), w)!.Value; }
            w.MouseDown(At(20, 60), MouseButton.Left);
            foreach (var (x, y) in new[] { (50.0, 25.0), (90.0, 62.0), (130.0, 28.0), (180.0, 60.0) }) { w.MouseMove(At(x, y)); Pump(); }
            w.MouseUp(At(180, 60), MouseButton.Left); Pump();
            Assert.Equal(1, w.LetterSignatureStrokes);
            Assert.True(w.Find<Button>("pdf-sig-use").IsEnabled);
            w.Find<Button>("pdf-sig-use").Press(); Pump();
            Assert.True(File.Exists(UserSettings.Load(root).LetterSignature)); // dieselbe Unterschrift wie in Briefe
            Assert.Contains("auf die Stelle", string.Join("|", w.GetLogicalTexts()));
            w.PdfMarkAt(0, new Point(300, 500), new Point(300, 500), 72, ""); w.Show("pdf"); Pump(); // Klick bei 72 dpi = PDF-Punkte (wie der Klick auf die Seite)
            Assert.EndsWith("rechnung (Trendo).pdf", w.PdfPath);
            Assert.Equal(before, File.ReadAllBytes(src));
            var text = System.Text.Encoding.Latin1.GetString(File.ReadAllBytes(w.PdfPath!));
            Assert.Matches(@"/Subtype\s*/Image", text);
            Assert.Contains("Unterschrift gesetzt", string.Join("|", w.GetLogicalTexts()));
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "PDF F3: Texterkennung – ohne Tesseract ehrlicher Hinweis; mit Tesseract wird ein Bild-Scan zur durchsuchbaren PDF und die Seite als Text erkannt")]
    public async Task Ocr_pdf()
    {
        var (w, _, root) = Ui.Open(offline: true);
        try
        {
            // Scan nachbauen: Seite als Bild in eine neue PDF (ohne Text)
            var vec = MakePdf(root);
            var png = Path.Combine(root, "scan.png");
            PdfView.SavePng(vec, 0, 150, png);
            var scan = Path.Combine(root, "scan.pdf");
            using (var d = new PdfDocument()) { var p = d.AddPage(); p.Width = XUnit.FromMillimeter(210); p.Height = XUnit.FromMillimeter(297); using (var g = XGraphics.FromPdfPage(p)) using (var img = XImage.FromFile(png)) g.DrawImage(img, 0, 0, p.Width.Point, p.Height.Point); d.Save(scan); }
            Assert.Empty(PdfView.Search(scan, "Rechnung"));

            w.OpenPdf(scan); w.Show("pdf"); Pump();
            MainWindow.TesseractOverride = null;
            await Run(w.PdfOcrPage());
            Assert.Contains("nicht installiert", string.Join("|", w.GetLogicalTexts())); // Werkzeugordner der Tests ist leer

            var tess = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), DataLocation.ToolsFolderName, "tesseract", "tesseract.exe");
            if (!File.Exists(tess)) return; // ohne Tesseract auf diesem PC nur der Hinweis
            MainWindow.TesseractOverride = tess;
            await Run(w.PdfOcrPage());
            Assert.Contains("Rechnung", w.Find<TextBox>("pdf-ocr-text").Text);
            var outFile = await RunR(w.PdfMakeSearchable());
            Assert.NotNull(outFile); Assert.EndsWith("scan-durchsuchbar.pdf", outFile);
            Assert.Equal(outFile, w.PdfPath); Assert.Equal(1, w.PdfPages);
            Assert.NotEmpty(PdfView.Search(outFile!, "Rechnung"));
            Assert.True(File.Exists(scan));
        }
        finally { MainWindow.TesseractOverride = null; w.Close(); }

        static async Task<T> RunR<T>(Task<T> t) { while (!t.IsCompleted) { Dispatcher.UIThread.RunJobs(); await Task.Delay(20); } return await t; }
    }
}
