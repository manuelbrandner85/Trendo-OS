using Avalonia;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Layout;
using Avalonia.Media;
using Trendo.Core;
using Trendo.Core.Sys;
using Path = System.IO.Path;

namespace Trendo.App;

/// <summary>F3 PDF (Vorbild-Analyse „Nächste Schritte“): Unterschrift einsetzen (gezeichnet oder als Bild, gleiche Unterschrift wie in Briefe)
/// und Texterkennung direkt im Programm – Seite erkennen oder ganze PDF durchsuchbar machen.</summary>
public partial class MainWindow
{
    private bool _pdfSignDraw;
    private string _pdfOcrText = "";
    private bool _pdfOcrBusy;
    /// <summary>Breite der Unterschrift auf der Seite in PDF-Punkten (4,5 cm wie im Brief).</summary>
    public const double PdfSignWidth = 45 / 25.4 * 72;
    /// <summary>Tests: Tesseract-Pfad statt Werkzeugordner.</summary>
    public static string? TesseractOverride { get; set; }

    Control PdfSignBox()
    {
        var sp = new StackPanel { Spacing = 5 };
        var have = File.Exists(_settings.LetterSignature);
        if (have && !_pdfSignDraw)
        {
            var img = new Image { Name = "pdf-sign-preview", Width = 180, Stretch = Stretch.Uniform, HorizontalAlignment = HorizontalAlignment.Left };
            try { img.Source = new Avalonia.Media.Imaging.Bitmap(_settings.LetterSignature); } catch (Exception) { img.IsVisible = false; }
            sp.Children.Add(new Border { Background = Brushes.White, CornerRadius = new CornerRadius(6), Padding = new Thickness(6), HorizontalAlignment = HorizontalAlignment.Left, Child = img });
            var on = _pdfMode == "Sign";
            var place = new Button { Name = "pdf-sign-place", Content = on ? "✍ Jetzt auf die Seite klicken …" : "✍ Auf die Seite setzen", Background = on ? Accent : Brushes.Transparent };
            place.Click += (_, _) => { _pdfMode = on ? null : "Sign"; PdfSay(on ? "" : "Klick auf die Stelle, an der die Unterschrift stehen soll."); Show("pdf"); };
            var redraw = new Button { Name = "pdf-sign-redraw", Content = "Neu zeichnen" };
            redraw.Click += (_, _) => { _pdfSignDraw = true; Show("pdf"); };
            sp.Children.Add(new WrapPanel { Children = { place, Sp(redraw) } });
            sp.Children.Add(P("Sie wird fest auf die Seite gezeichnet – in einer Kopie „(Trendo)“, das Original bleibt. Keine qualifizierte elektronische Signatur.", null, 11));
        }
        else
        {
            sp.Children.Add(SignaturePad("pdf-sig", "pdf", _ => { _pdfSignDraw = false; _pdfMode = "Sign"; PdfSay("✓ Unterschrift gespeichert – jetzt auf die Stelle der Seite klicken."); Show("pdf"); },
                m => { PdfSay(m); Show("pdf"); }, 205, 90)); // passt in die Seitenleiste
            if (have) { var back = new Button { Name = "pdf-sign-keep", Content = "Bisherige behalten" }; back.Click += (_, _) => { _pdfSignDraw = false; _sigStrokes.Clear(); Show("pdf"); }; sp.Children.Add(back); }
        }
        return new Expander { Name = "pdf-sign", Header = "✍ Unterschreiben", IsExpanded = _pdfMode == "Sign" || _pdfSignDraw, HorizontalAlignment = HorizontalAlignment.Stretch, Content = sp };
    }

    /// <summary>Unterschrift mittig an die angeklickte Stelle setzen (Punkte in PDF-Koordinaten von oben links).</summary>
    public void PdfSignAt(int pageIndex, double xPt, double yTopPt)
    {
        if (!File.Exists(_settings.LetterSignature)) { PdfSay("Bitte zuerst unterschreiben."); return; }
        var f = _settings.LetterSignature; var (iw, ih) = XrPlus.ImageSize(File.ReadAllBytes(f)); var h = iw > 0 ? PdfSignWidth * ih / iw : 0;
        _pdfMode = null;
        ApplyEdit((i, o) => PdfTools.AddImage(i, o, pageIndex, f, xPt - PdfSignWidth / 2, yTopPt - h / 2, PdfSignWidth, _core.Audit), "Unterschrift gesetzt");
    }

    Control PdfOcrBox()
    {
        var sp = new StackPanel { Spacing = 5 };
        var page = new Button { Name = "pdf-ocr-page", Content = "Text dieser Seite erkennen", IsEnabled = !_pdfOcrBusy };
        page.Click += async (_, _) => await PdfOcrPage();
        var all = new Button { Name = "pdf-ocr-all", Content = "Ganze PDF durchsuchbar machen", IsEnabled = !_pdfOcrBusy };
        all.Click += async (_, _) => await PdfMakeSearchable();
        sp.Children.Add(P("Für Scans und Fotos: Trendo liest den Text mit Tesseract (offline). „Durchsuchbar“ legt eine neue PDF mit unsichtbarer Textebene an – Suchen und Kopieren gehen dann.", null, 11));
        sp.Children.Add(new WrapPanel { Children = { page, Sp(all) } });
        if (_pdfOcrText.Length > 0)
            sp.Children.Add(new TextBox { Name = "pdf-ocr-text", Text = _pdfOcrText, IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MaxHeight = 200 });
        return new Expander { Name = "pdf-ocr", Header = "🔎 Texterkennung", IsExpanded = _pdfOcrText.Length > 0 || _pdfOcrBusy, HorizontalAlignment = HorizontalAlignment.Stretch, Content = sp };
    }

    /// <summary>Tesseract und deutsches Sprachpaket bereitstellen (wie im Briefe-Studio); null mit Meldung, wenn es nicht geht.</summary>
    async Task<(string Tess, string? Dir, string Lang)?> OcrSetup(Action<string> say)
    {
        var tess = TesseractOverride ?? ToolPath("tesseract");
        if (tess is null) { say("Texterkennung (Tesseract) ist nicht installiert – unter Zusatzprogramme nachinstallieren."); return null; }
        var run = new RealProcessRunner(); string? dir = null; var lang = "deu";
        if (Ocr.HasUserGerman(_core.DataRoot)) dir = Ocr.UserTessdata(_core.DataRoot);
        else if (!(await Ocr.LanguagesAsync(run, tess)).Contains("deu"))
        {
            if (_core.Offline) { say("Deutsches Sprachpaket für die Texterkennung fehlt, und Trendo ist offline – es wird Englisch erkannt."); return (tess, null, "eng"); }
            var (ok, m) = await Ocr.DownloadGermanAsync(new HttpClient { Timeout = TimeSpan.FromSeconds(60) }.GetStreamAsync, _core.DataRoot, _core.Audit, Principal.LocalOwner);
            if (!ok) { say(m + " Erkannt wird auf Englisch."); lang = "eng"; } else dir = Ocr.UserTessdata(_core.DataRoot);
        }
        return (tess, dir, lang);
    }

    /// <summary>Seite mit 300 dpi als PNG für die Texterkennung.</summary>
    string PdfPagePng(int page, string dir)
    {
        var f = Path.Combine(dir, $"seite-{page + 1:0000}.png");
        PdfView.SavePng(PdfPath!, page, 300, f, _pdfPassword);
        return f;
    }

    public async Task PdfOcrPage()
    {
        if (PdfPath is null || _pdfOcrBusy) return;
        var dir = Path.Combine(Path.GetTempPath(), "trendo-pdf-ocr-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir);
        _pdfOcrBusy = true; PdfSay($"Erkenne Text auf Seite {PdfPage + 1} …"); Show("pdf");
        try
        {
            if (await OcrSetup(m => PdfSay(m)) is not { } o) return;
            var r = await Ocr.RunAsync(new RealProcessRunner(), o.Tess, PdfPagePng(PdfPage, dir), default, o.Dir, o.Lang);
            if (!r.Ok) { PdfSay(r.Message); return; }
            _pdfOcrText = r.Text;
            if (Clipboard is { } cb && r.Text.Length > 0) _ = cb.SetTextAsync(r.Text);
            PdfSay(r.Text.Trim().Length == 0 ? "Auf dieser Seite wurde kein Text erkannt." : r.Message + " Text ist kopiert.");
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or UnauthorizedAccessException) { PdfSay("Texterkennung fehlgeschlagen: " + ex.Message); }
        finally { _pdfOcrBusy = false; try { Directory.Delete(dir, true); } catch (IOException) { } Show("pdf"); }
    }

    /// <summary>Neue PDF „-durchsuchbar“ mit Textebene; danach wird sie geöffnet.</summary>
    public async Task<string?> PdfMakeSearchable()
    {
        if (PdfPath is null || _pdfOcrBusy) return null;
        var dir = Path.Combine(Path.GetTempPath(), "trendo-pdf-ocr-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir);
        _pdfOcrBusy = true; PdfSay("Texterkennung läuft …"); Show("pdf");
        try
        {
            if (await OcrSetup(m => PdfSay(m)) is not { } o) return null;
            var src = PdfPath; var pages = PdfPages;
            var imgs = Enumerable.Range(0, pages).Select(i => PdfPagePng(i, dir)).ToList();
            var outFile = Beside(src, "-durchsuchbar");
            var (n, err) = await PdfTools.OcrSearchableAsync(new RealProcessRunner(), o.Tess, imgs, outFile, o.Dir, o.Lang, 300,
                i => _ui.Post(() => { PdfSay($"Texterkennung: Seite {i + 1} von {pages} …"); }), _core.Audit);
            if (err is not null) { PdfSay(err); return null; }
            OpenPdf(outFile);
            PdfSay($"✓ Durchsuchbar gespeichert: {Path.GetFileName(outFile)} ({n} Seiten) – Suchen und Kopieren gehen jetzt.");
            return outFile;
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or UnauthorizedAccessException) { PdfSay("Texterkennung fehlgeschlagen: " + ex.Message); return null; }
        finally { _pdfOcrBusy = false; try { Directory.Delete(dir, true); } catch (IOException) { } Show("pdf"); }
    }
}
