using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using Trendo.App;
using Trendo.Core.Sys;
using Xunit;
namespace Trendo.App.Tests;

public class T0223_TrendoPdfUi
{
    /// <summary>Drei Seiten mit echtem Text und zwei Lesezeichen.</summary>
    static string Sample(string dir)
    {
        System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(PdfTools).TypeHandle); // Schriften für PDFsharp einrichten
        var f = Path.Combine(dir, "beispiel.pdf");
        using var d = new PdfDocument();
        var font = new XFont("Arial", 18);
        foreach (var (i, text) in new[] { (1, "Seite eins Einleitung"), (2, "Seite zwei Rechnung 2026"), (3, "Seite drei Anhang Rechnung") })
        {
            var p = d.AddPage(); p.Size = PdfSharp.PageSize.A4;
            using (var g = XGraphics.FromPdfPage(p)) g.DrawString(text, font, XBrushes.Black, new XPoint(60, 100));
            if (i != 2) d.Outlines.Add(i == 1 ? "Einleitung" : "Anhang", p);
        }
        d.Save(f);
        return f;
    }

    [AvaloniaFact(DisplayName = "T-0223 P1/P2 UI: PDF öffnen, blättern, Gehe zu, suchen, Lesezeichen – Seite wird gezeichnet")]
    public void View_search_bookmarks()
    {
        var (w, _, root) = Ui.Open();
        var f = Sample(root);
        w.OpenPdf(f); w.Show("pdf");
        Assert.Equal("von 3", w.Find<TextBlock>("pdf-pages-label").Text);
        var img = w.Find<Image>("pdf-image-0");
        Assert.True(img.Width > 100 && img.Height > 100, $"Bild {img.Width}x{img.Height}");
        using (var sk = PdfView.RenderSk(f, 0, 72))
        {
            Assert.Equal((595, 842), (sk.Width, sk.Height)); // A4 bei 72 dpi
            var dark = 0;
            for (var y = 80; y < 110; y++) for (var x = 60; x < 260; x++) if (sk.GetPixel(x, y).Red < 128) dark++;
            Assert.True(dark > 100, $"Text nicht gezeichnet ({dark} dunkle Punkte)");
        }
        w.Find<Button>("pdf-next").Press(); Assert.Equal(1, w.PdfPage);
        w.Find<Button>("pdf-last").Press(); Assert.Equal(2, w.PdfPage);
        w.Find<Button>("pdf-first").Press(); Assert.Equal(0, w.PdfPage);
        var box = w.Find<TextBox>("pdf-page"); box.Text = "3";
        box.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter });
        Assert.Equal(2, w.PdfPage);
        var s = w.Find<TextBox>("pdf-search"); s.Text = "rechnung";
        s.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter });
        Assert.Equal(1, w.PdfPage); // erster Treffer auf Seite 2
        Assert.NotNull(w.TryFind<Button>("pdf-hit-1"));
        Assert.Null(w.TryFind<Button>("pdf-hit-2"));
        w.Find<Button>("pdf-bm-1").Press(); Assert.Equal(2, w.PdfPage); // Lesezeichen „Anhang“
        Assert.Equal(["Einleitung", "Anhang"], PdfView.Bookmarks(f).Select(b => b.Title));
        Assert.Contains("Rechnung 2026", PdfView.Text(f, 1));
        w.Find<Button>("pdf-double").Press(); w.Find<Button>("pdf-first").Press();
        Assert.NotNull(w.TryFind<Image>("pdf-image-1"));
        w.Close();
    }

    [AvaloniaFact(DisplayName = "T-0223 P3–P5 UI: Markieren in einer Kopie (Original bleibt), Seiten auswählen, Passwort setzen und wieder öffnen")]
    public void Edit_in_copy()
    {
        var (w, _, root) = Ui.Open();
        var f = Sample(root);
        var before = File.ReadAllBytes(f);
        w.OpenPdf(f); w.Show("pdf");
        w.PdfSetMode("Highlight");
        w.PdfMarkAt(1, new Point(50, 80), new Point(300, 110), 72, "prüfen");
        Assert.Equal(before, File.ReadAllBytes(f));
        var copy = Path.Combine(root, "beispiel (Trendo).pdf");
        Assert.Equal(copy, w.PdfPath);
        Assert.Equal([("Highlight", (string?)"prüfen")], PdfTools.Annotations(copy, 1));
        w.PdfSetMode("Note");
        w.PdfMarkAt(0, new Point(60, 60), new Point(60, 60), 72, "Notiz eins");
        Assert.Equal(copy, w.PdfPath); // dieselbe Kopie wird weiter bearbeitet
        Assert.Contains(PdfTools.Annotations(copy, 0), a => a.Kind == "FreeText");

        w.Show("pdf");
        w.Find<TextBox>("pdf-select-spec").Text = "z,1";
        w.Find<Button>("pdf-select-go").Press();
        var sel = Path.Combine(root, "beispiel (Trendo)-seiten.pdf");
        Assert.Equal(2, PdfTools.Read(sel).Pages);
        Assert.Contains("2 Seiten gespeichert", w.Find<TextBlock>("pdf-msg").Text);

        w.Find<TextBox>("pdf-pw-new").Text = "Geheim-1";
        w.Find<Button>("pdf-encrypt").Press();
        var enc = Path.Combine(root, "beispiel (Trendo)-geschuetzt.pdf");
        w.OpenPdf(enc); w.Show("pdf");
        Assert.Null(w.TryFind<Image>("pdf-image-0"));
        w.Find<TextBox>("pdf-unlock-pw").Text = "Geheim-1";
        w.Find<Button>("pdf-unlock").Press();
        Assert.Equal(3, w.PdfPages);
        Assert.NotNull(w.TryFind<Image>("pdf-image-0"));
        Assert.NotNull(w.TryFind<Button>("pdf-decrypt"));
        Assert.DoesNotContain("Geheim-1", File.ReadAllText(Path.Combine(root, "audit", "audit.jsonl")));
        w.Close();
    }
}

public class T0222_VollpaketMerken
{
    [Fact(DisplayName = "T-0222: Ordner des Vollpakets wird bei der Installation gemerkt und beim ersten Start gefunden")]
    public void Remember()
    {
        var root = Path.Combine(Path.GetTempPath(), "trendo-vpm-" + Guid.NewGuid().ToString("N"));
        try
        {
            var prog = Path.Combine(root, "medium", Trendo.Core.Vollpaket.FolderName); Directory.CreateDirectory(prog);
            File.WriteAllText(Path.Combine(prog, Trendo.Core.Vollpaket.ManifestName), """{"programs":[{"id":"qpdf","name":"qpdf","version":"1","license":"MIT","file":"qpdf.zip","sha256":"0000000000000000000000000000000000000000000000000000000000000000","bytes":1,"exe":"bin/qpdf.exe","source_url":null}]}""");
            var file = Path.Combine(root, "daten", "vollpaket-quelle.txt");
            Assert.Null(VollpaketStart.RememberFromInstaller([Path.Combine(root, "anderswo")], file));
            Assert.False(File.Exists(file));
            Assert.Equal(Path.GetFullPath(prog), VollpaketStart.RememberFromInstaller([Path.Combine(root, "medium")], file));
            Assert.Equal([Path.GetFullPath(prog)], VollpaketStart.Remembered(file));
            Assert.Equal(Path.GetFullPath(prog), Trendo.Core.Vollpaket.Locate(VollpaketStart.Remembered(file)));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
