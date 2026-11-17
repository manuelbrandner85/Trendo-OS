using System.Security.Cryptography;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using PdfSharp.Drawing;
using Trendo.App;
using Trendo.Core.Kreativ;
using Trendo.Core.Programs;
using Trendo.Core.Sys;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// <summary>PDF-Werkstatt (STUDIO-PDFTOOLS-01, T-0672): echte Klicks – Dateien hinzufügen, Werkzeug wählen, Ausführen; Ergebnis prüfen, Original unverändert.</summary>
public class PdfWerkstatt_Ui
{
    const string K = MainWindow.PwKey;

    internal static string TextPdf(string path, params string[] pages)
    {
        PdfTools.EnsureFonts();
        using var d = new PdfSharp.Pdf.PdfDocument();
        foreach (var t in pages)
        {
            var p = d.AddPage(); p.Size = PdfSharp.PageSize.A4;
            using var g = XGraphics.FromPdfPage(p); var font = new XFont("Arial", 14); var y = 80.0;
            foreach (var line in t.Split('\n')) { g.DrawString(line, font, XBrushes.Black, 60, y); y += 22; }
        }
        d.Save(path); return path;
    }

    static string Hash(string f) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(f)));
    static void Tap(MainWindow w, string name) { var b = w.Find<Button>(name); b.BringIntoView(); Settle(); b.Press(); Settle(); }
    static void Click(MainWindow w, string name) { var c = V<Control>(w, name); c.BringIntoView(); Settle(); RealClick(w, V<Control>(w, name)); Settle(); }

    static ProgramDocument Open(MainWindow w, string tpl, params string[] files)
    {
        w.Width = 1600; w.Height = 1100;
        w.ProgramSetLevel(UserLevel.Profi);
        if (w.ProgramActive(K) is null) { w.Show(K); Settle(); RealClick(w, w.Find<Button>("prog-tpl-" + tpl)); }
        else { w.ProgramNew(K, tpl); w.Show(K); } // zweites Dokument: Vorlage direkt
        Settle();
        var d = w.ProgramActive(K)!;
        if (files.Length > 0)
        {
            w.PickOverride = _ => Task.FromResult<string[]?>(files);
            Tap(w, "prog-cmd-pw-add");
            Assert.Equal(files.Length, d.List("files").Count);
        }
        return d;
    }

    static List<string> Results(ProgramDocument d) => [.. d.List("results").Select(r => r!["file"].AsText()!)];

    [AvaloniaFact(DisplayName = "PDF-Werkstatt: eigene Seite im Studio, Werkzeugleiste mit allen Werkzeugen, Vorlage wählt das Werkzeug")]
    public void Page()
    {
        var (w, _, _) = Ui.Open();
        try
        {
            Assert.Equal(K, MainWindow.StudioPages["STUDIO-PDFTOOLS-01"]);
            var d = Open(w, "teilen");
            Assert.Equal("teilen", d.Text("tool"));
            Assert.Contains("Teilen", w.Find<TextBlock>("pw-tool-title").Text);
            foreach (var t in new[] { "zusammenfuegen", "teilen", "ordnen", "bilder", "komprimieren", "wasserzeichen", "stempel", "seitenzahlen", "metadaten", "passwort", "schwaerzen", "vergleichen" })
                Assert.NotNull(w.Find<Button>("prog-cmd-pw-tool-" + t));
            Tap(w, "prog-cmd-pw-tool-komprimieren"); Assert.Equal("komprimieren", d.Text("tool"));
            Assert.NotNull(w.Find<ComboBox>("pw-level"));
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "PW-01 UI: zwei PDFs hinzufügen, Reihenfolge tauschen, „Ausführen“ fügt zusammen; Ergebnis öffnet in Trendo PDF; Originale unverändert")]
    public void Merge_by_clicks()
    {
        var (w, _, root) = Ui.Open();
        try
        {
            var a = TextPdf(Path.Combine(root, "Rechnung.pdf"), "R1", "R2"); var b = TextPdf(Path.Combine(root, "Anlage.pdf"), "A1", "A2", "A3");
            var ha = Hash(a); var hb = Hash(b);
            var d = Open(w, "zusammenfuegen", a, b);
            Assert.Equal(5, d.List("pages").Count); // Seitenübersicht mit Vorschaubildern
            Assert.NotNull(w.Find<Button>("pw-page-4"));
            Click(w, "pw-file-up-1"); // Anlage nach vorn
            Assert.EndsWith("Anlage.pdf", d.List("files")[0].AsText());
            Click(w, "pw-run-now");
            var res = Results(d).Single();
            Assert.True(File.Exists(res)); Assert.Equal(5, PdfView.PageCount(res));
            Assert.StartsWith("A1", PdfView.Text(res, 0).Trim());
            Assert.Equal(ha, Hash(a)); Assert.Equal(hb, Hash(b));
            Assert.Contains("Fertig", w.ProgramMessage(K));
            Click(w, "pw-res-open-0"); Assert.Equal("pdf", w.CurrentView); Assert.Equal(res, w.PdfPath);
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "PW-02 UI: Seiten ordnen – Seite anklicken, drehen, Seite löschen, nach vorn; Ergebnis hat die neue Folge")]
    public void Organize_by_clicks()
    {
        var (w, _, root) = Ui.Open();
        try
        {
            var a = TextPdf(Path.Combine(root, "Vier.pdf"), "S1", "S2", "S3", "S4");
            var d = Open(w, "ordnen", a);
            Click(w, "pw-page-3"); Assert.Equal([3], w.PwSelection);
            Tap(w, "prog-cmd-pw-rot-right");
            Tap(w, "prog-cmd-pw-earlier"); Assert.Equal([2], w.PwSelection);
            Click(w, "pw-page-0"); Tap(w, "prog-cmd-pw-del");
            Assert.Equal(3, d.List("pages").Count);
            Click(w, "pw-run-now");
            var res = Results(d).Single();
            Assert.Equal(["S2", "S4", "S3"], Enumerable.Range(0, 3).Select(i => PdfView.Text(res, i).Trim()));
            Assert.Equal(90, PdfTools.PageRotation(res, 1));
            // ohne Auswahl meldet „Drehen“ einen Hinweis statt still nichts zu tun
            w.PwSelect(); Tap(w, "prog-cmd-pw-rot-left"); Assert.Contains("Seiten", w.ProgramMessage(K));
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "PW-04 UI: Komprimieren mit JPEG (SkiaSharp) – gemessen kleiner, Bild als JPEG gespeichert, Meldung nennt vorher/nachher")]
    public void Compress_by_clicks()
    {
        var (w, _, root) = Ui.Open();
        try
        {
            var px = new Pixmap(2400, 2400); var rnd = new Random(3);
            for (var i = 0; i < px.Px.Length; i += 4) { px.Px[i] = (byte)rnd.Next(80, 120); px.Px[i + 1] = (byte)(i / 4 % 2400 / 10); px.Px[i + 2] = 150; px.Px[i + 3] = 255; }
            var png = Path.Combine(root, "scan.png"); File.WriteAllBytes(png, PngCodec.Encode(px));
            var big = Path.Combine(root, "Scan.pdf"); PdfTools.ImagesToPdf([png], big);
            var d = Open(w, "komprimieren", big);
            Click(w, "pw-run-now");
            var res = Results(d).Single();
            Assert.True(new FileInfo(res).Length < new FileInfo(big).Length / 3, $"{new FileInfo(big).Length} → {new FileInfo(res).Length}");
            Assert.Contains("/DCTDecode", File.ReadAllText(res, Encoding.Latin1));
            Assert.Matches(@"MB|KB", d.List("results")[0]!["note"].AsText()!);
            Assert.Contains("kleiner", d.List("results")[0]!["note"].AsText()!);
            Assert.Equal(1, PdfView.PageCount(res));
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "PW-09 UI: Schwärzen per Maus – Balken ziehen, Ausführen; der Text unter dem Balken ist in der Ergebnisdatei nicht mehr lesbar")]
    public void Redact_by_mouse()
    {
        var (w, _, root) = Ui.Open();
        try
        {
            var a = TextPdf(Path.Combine(root, "Brief.pdf"), "Kontonummer 98765\nGruss", "Seite zwei bleibt");
            Assert.Contains("98765", PdfView.Text(a, 0));
            var d = Open(w, "schwaerzen", a);
            var c = V<Canvas>(w, "pw-redact-canvas"); c.BringIntoView(); Settle();
            // Text steht bei x=60, y≈66–84 Punkt; Rahmen großzügig darüber ziehen (Maßstab 1,1)
            var from = c.TranslatePoint(new Point(50 * 1.1, 58 * 1.1), w)!.Value; var to = c.TranslatePoint(new Point(320 * 1.1, 88 * 1.1), w)!.Value;
            w.MouseDown(from, MouseButton.Left); for (var i = 1; i <= 6; i++) { w.MouseMove(from + (to - from) * (i / 6.0)); Settle(); } w.MouseUp(to, MouseButton.Left); Settle();
            Assert.Single(d.List("boxes"));
            Click(w, "pw-run-now");
            var res = Results(d).Single();
            Assert.DoesNotContain("98765", PdfView.Text(res, 0));
            Assert.DoesNotContain("Gruss", PdfView.Text(res, 0)); // ganze Seite ist Bild – kein Text mehr
            Assert.Contains("Seite zwei bleibt", PdfView.Text(res, 1));
            Assert.Contains("98765", PdfView.Text(a, 0)); // Original unverändert
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "PW-10 UI: Zwei PDFs vergleichen – Unterschiede mit Seitenangabe in der Arbeitsfläche und als Textdatei")]
    public void Compare_by_clicks()
    {
        var (w, _, root) = Ui.Open();
        try
        {
            var a = TextPdf(Path.Combine(root, "alt.pdf"), "Miete 800 Euro\nKaution 3 Monate");
            var b = TextPdf(Path.Combine(root, "neu.pdf"), "Miete 850 Euro\nKaution 3 Monate", "Anhang Hausordnung");
            var d = Open(w, "vergleichen", a, b);
            Click(w, "pw-run-now");
            var c = w.PwLastCompare!;
            Assert.Equal(1, c.Removed); Assert.Equal(2, c.Added);
            Assert.NotNull(w.Find<StackPanel>("pw-compare"));
            var txt = File.ReadAllText(Results(d).Single());
            Assert.Contains("− A S.1: Miete 800 Euro", txt); Assert.Contains("+ B S.2: Anhang Hausordnung", txt);
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "PW-05/06/07/08 UI: Stapel – Wasserzeichen und Seitenzahlen für jede Datei, Metadaten entfernen, Passwort setzen und mit Passwort entfernen")]
    public void Batch_tools()
    {
        var (w, _, root) = Ui.Open();
        try
        {
            var a = TextPdf(Path.Combine(root, "eins.pdf"), "Eins", "Zwei"); var b = TextPdf(Path.Combine(root, "zwei.pdf"), "Drei");
            var d = Open(w, "zusammenfuegen", a, b);
            Tap(w, "prog-cmd-pw-tool-wasserzeichen"); d.Set("wmText", "ENTWURF"); Settle();
            Click(w, "pw-run-now");
            Assert.Equal(2, Results(d).Count);
            Assert.All(Results(d), f => Assert.Contains("ENTWURF", PdfView.Text(f, 0)));
            Tap(w, "prog-cmd-pw-tool-seitenzahlen");
            Click(w, "pw-run-now");
            Assert.Contains("Seite 2 von 2", PdfView.Text(Results(d)[2], 1));
            Tap(w, "prog-cmd-pw-tool-metadaten"); d.Set("metaMode", "entfernen"); Settle();
            Click(w, "pw-run-now");
            Assert.Empty(PdfTools.ReadMeta(Results(d)[4]).Info);
            // Passwort: getippt, nie im Dokument
            Tap(w, "prog-cmd-pw-tool-passwort");
            var pw = w.Find<TextBox>("pw-password"); pw.Text = "Probe-42"; Settle();
            Click(w, "pw-run-now");
            var locked = Results(d)[6];
            Assert.True(PdfTools.Read(locked).NeedsPassword);
            Assert.DoesNotContain("Probe-42", d.Serialize());
            Tap(w, "prog-cmd-pw-clear"); w.PickOverride = _ => Task.FromResult<string[]?>([locked]); Tap(w, "prog-cmd-pw-add");
            d.Set("pwMode", "entfernen"); Settle();
            w.Find<TextBox>("pw-password").Text = "Probe-42"; Settle();
            Click(w, "pw-run-now");
            Assert.False(PdfTools.Read(Results(d)[^1]).NeedsPassword);
            // falsches Passwort: verständliche Meldung, keine Datei
            var before = Results(d).Count; w.Find<TextBox>("pw-password").Text = "falsch"; Settle();
            Click(w, "pw-run-now"); Assert.Equal(before, Results(d).Count); Assert.NotEqual("", w.ProgramMessage(K));
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "PW-01/03 UI: Teilen nach Bereichen und Bilder zu PDF über die Werkstatt")]
    public void Split_and_images()
    {
        var (w, _, root) = Ui.Open();
        try
        {
            var a = TextPdf(Path.Combine(root, "Buch.pdf"), "K1", "K1b", "K2", "K3");
            var d = Open(w, "teilen", a);
            d.Set("ranges", "1-2; 3; 4"); Settle();
            Click(w, "pw-run-now");
            Assert.Equal([2, 1, 1], Results(d).Select(f => PdfView.PageCount(f)));
            var img = Path.Combine(root, "foto.png"); var px = new Pixmap(300, 200); px.Fill(200, 30, 30, 255); File.WriteAllBytes(img, PngCodec.Encode(px));
            var d2 = Open(w, "bilder", img);
            Click(w, "pw-run-now");
            var res = Results(d2).Single(); Assert.Equal(1, PdfView.PageCount(res));
            var (pw, ph) = PdfView.Sizes(res)[0]; Assert.True(pw > ph); // Querbild → Querformat
            Assert.Throws<ArgumentException>(() => w.PwAddFiles(d2, [Path.Combine(root, "x.docx")]));
        }
        finally { w.Close(); }
    }
}
