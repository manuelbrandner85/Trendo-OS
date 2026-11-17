using System.Security.Cryptography;
using System.Text;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Trendo.App;
using Trendo.Core;
using Trendo.Core.Office;
using Trendo.Core.Programs;
using Trendo.Core.Sys;
using Xunit;
using static Trendo.App.Tests.BueroAusbau_Ui;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// <summary>T-0705 Echtlauf Büro: Fremddateien, die LibreOffice selbst erzeugt (PDF-Dokument, PDF-Formular mit AcroForm-Feldern, PowerPoint, Foto einer Seite),
/// werden über die Oberfläche in die Büro-Studios geholt, bearbeitet und exportiert; die Ergebnisse prüfen echte Fremdprogramme auf diesem PC:
/// qpdf (Struktur, Seiten, Formularfelder), LibreOffice (öffnet PDF/DOCX/PPTX und wandelt um), Tesseract (Texterkennung), PDFium (Text je Seite).
/// Nichts ist ersetzt außer der Dateiauswahl des Systems. Fehlt ein Werkzeug, wird der Test übersprungen (BLOCKIERT) – nichts wird simuliert.
/// Protokoll und Dateien: Ordner aus TRENDO_BELEG_DIR (sonst Temp); <c>tools/echtlauf/buero/t0705.py</c> sammelt sie nach reports/T-0705/.</summary>
public class BueroEchtlauf_Ui
{
    static string? Tool(string id) => Cli.Find(id) ?? new DependencyManager(new AuditLog(Path.Combine(Path.GetTempPath(), "trendo-t0705-needs.jsonl")), new RealProcessRunner()).ResolvePath(DependencyCatalog.Get(id));

    /// <summary>Protokoll eines Echtlaufs: jeder Befehl mit Exit-Code und Ausgabe, jede Datei mit SHA-256.</summary>
    sealed class Beleg : IDisposable
    {
        readonly StringBuilder _log = new(); readonly string _name;
        public string Dir { get; }
        public Beleg(string name)
        {
            _name = name;
            Dir = Path.Combine(Environment.GetEnvironmentVariable("TRENDO_BELEG_DIR") is { Length: > 0 } d ? d : Path.Combine(global::Trendo.Core.TempDir.SystemTemp, "trendo-t0705-beleg"), name);
            Directory.CreateDirectory(Dir);
            Line($"# Echtlauf {name} – {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        }
        public void Line(string s) => _log.Append(s).Append('\n');
        public Cli.Result Run(string label, string exe, params string[] args)
        {
            Line($"$ {label}: {Path.GetFileName(exe)} {string.Join(' ', args.Select(a => a.Contains(' ') ? $"\"{a}\"" : a))}");
            var r = Cli.Run(exe, args, timeoutMs: 240000);
            Line($"Exit {r.Exit}");
            if (r.Out.Trim().Length > 0) Line(Cut(r.Out));
            if (r.Err.Trim().Length > 0) Line("[stderr] " + Cut(r.Err));
            return r;
        }
        static string Cut(string s) { s = s.Trim(); return s.Length > 6000 ? s[..6000] + " …(gekürzt)" : s; }
        /// <summary>Datei als Beleg ablegen (Kopie) und Prüfsumme protokollieren.</summary>
        public string Keep(string file, string? name = null)
        {
            Assert.True(File.Exists(file), "Datei fehlt: " + file);
            var to = Path.Combine(Dir, name ?? Path.GetFileName(file)); File.Copy(file, to, true);
            Line($"Datei {Path.GetFileName(to)}: {new FileInfo(to).Length} Bytes, SHA-256 {Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(to))).ToLowerInvariant()}");
            return to;
        }
        public void Dispose() => File.WriteAllText(Path.Combine(Dir, _name + ".log"), _log.ToString(), new UTF8Encoding(false));
    }

    static (string Lo, string Qpdf) Need(params string[] more)
    {
        var lo = Tool("libreoffice"); var qpdf = Tool("qpdf");
        var missing = new List<string>();
        if (lo is null) missing.Add("LibreOffice (soffice)");
        if (qpdf is null) missing.Add("qpdf");
        foreach (var m in more) if (Tool(m) is null) missing.Add(m);
        if (missing.Count > 0) Assert.Skip("BLOCKIERT – fehlt auf diesem PC: " + string.Join(", ", missing));
        return (lo!, qpdf!);
    }

    static string Repo()
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d is not null; d = d.Parent)
            if (File.Exists(Path.Combine(d.FullName, "Trendo.sln"))) return d.FullName;
        throw new InvalidOperationException("Projektordner nicht gefunden");
    }

    /// <summary>LibreOffice wandelt um (eigenes Profil, ohne Fenster). Gibt die Zieldatei zurück.</summary>
    static string LoConvert(Beleg b, string lo, string file, string format, string ext)
    {
        var outDir = Path.Combine(Path.GetTempPath(), "trendo-t0705-out-" + Guid.NewGuid().ToString("N")[..8]); Directory.CreateDirectory(outDir);
        var prof = Path.Combine(Path.GetTempPath(), "trendo-t0705-lo-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            var r = b.Run("LibreOffice öffnet " + Path.GetFileName(file), lo, "-env:UserInstallation=" + new Uri(prof).AbsoluteUri, "--headless", "--norestore", "--convert-to", format, "--outdir", outDir, file);
            var target = Path.Combine(outDir, Path.GetFileNameWithoutExtension(file) + "." + ext);
            Assert.True(r.Ok && File.Exists(target), $"LibreOffice konnte {Path.GetFileName(file)} nicht öffnen/umwandeln (Exit {r.Exit}).");
            return target;
        }
        finally { try { Directory.Delete(prof, true); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
    }

    /// <summary>LibreOffice erzeugt eine Fremddatei über UNO (Skript tools/echtlauf/buero/lo_fremd.py, Python von LibreOffice).</summary>
    static string LoMake(Beleg b, string lo, string mode, string target)
    {
        var py = Path.Combine(Path.GetDirectoryName(lo)!, "python.exe");
        if (!File.Exists(py)) Assert.Skip("BLOCKIERT – LibreOffice ohne eigenes Python (program\\python.exe) – Fremddatei lässt sich nicht erzeugen.");
        var prof = Path.Combine(Path.GetTempPath(), "trendo-t0705-uno-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            var r = b.Run("LibreOffice erzeugt " + Path.GetFileName(target), py, Path.Combine(Repo(), "tools", "echtlauf", "buero", "lo_fremd.py"), mode, lo, target, prof);
            Assert.True(r.Ok && File.Exists(target), "LibreOffice hat die Fremddatei nicht geschrieben: " + r.Err);
            return target;
        }
        finally { try { Directory.Delete(prof, true); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
    }

    /// <summary>Kleines Textdokument (Flat-ODF) – LibreOffice macht daraus PDF oder PNG; jede Zeile eine Seite.</summary>
    static string Fodt(string path, double pt, params string[] pages)
    {
        static string X(string s) => System.Security.SecurityElement.Escape(s);
        var sb = new StringBuilder("<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<office:document xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" xmlns:style=\"urn:oasis:names:tc:opendocument:xmlns:style:1.0\" "
            + "xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\" xmlns:fo=\"urn:oasis:names:tc:opendocument:xmlns:xsl-fo-compatible:1.0\" office:version=\"1.3\" office:mimetype=\"application/vnd.oasis.opendocument.text\">\n");
        sb.Append(System.Globalization.CultureInfo.InvariantCulture, $"<office:automatic-styles><style:style style:name=\"P1\" style:family=\"paragraph\"><style:text-properties fo:font-size=\"{pt}pt\" fo:font-weight=\"bold\"/></style:style>");
        sb.Append(System.Globalization.CultureInfo.InvariantCulture, $"<style:style style:name=\"P2\" style:family=\"paragraph\"><style:paragraph-properties fo:break-before=\"page\"/><style:text-properties fo:font-size=\"{pt}pt\" fo:font-weight=\"bold\"/></style:style></office:automatic-styles>\n<office:body><office:text>\n");
        for (var i = 0; i < pages.Length; i++)
        {
            var lines = pages[i].Split('\n');
            for (var k = 0; k < lines.Length; k++) sb.Append($"<text:p text:style-name=\"{(i > 0 && k == 0 ? "P2" : "P1")}\">{X(lines[k])}</text:p>\n");
        }
        sb.Append("</office:text></office:body></office:document>\n");
        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false)); return path;
    }

    static void QpdfCheck(Beleg b, string qpdf, string pdf, int? pages = null)
    {
        var r = b.Run("qpdf prüft " + Path.GetFileName(pdf), qpdf, "--check", pdf);
        // Exit 3 = „mit Warnungen erfolgreich“. Erlaubt ist nur die bekannte Warnung zu Flate-Strömen aus PDFsharp 6.2.4 („input stream is complete but output may
        // still be valid“: der Datenstrom endet ohne Abschluss-Prüfsumme, der Inhalt ist vollständig) – jede andere Warnung oder ein Fehler lässt den Test scheitern.
        var warnings = (r.Out + "\n" + r.Err).Split('\n').Where(l => l.Contains("WARNING", StringComparison.Ordinal)).ToList();
        var onlyFlate = r.Exit == 3 && warnings.Count > 0 && warnings.All(l => l.Contains("input stream is complete but output may still be valid", StringComparison.Ordinal));
        if (onlyFlate) b.Line($"qpdf: gültig, {warnings.Count} Warnung(en) „Flate-Strom ohne Abschluss“ (PDFsharp) – keine Fehler.");
        Assert.True(r.Exit == 0 || onlyFlate, $"qpdf meldet Fehler in {Path.GetFileName(pdf)} (Exit {r.Exit}): {r.Out} {r.Err}");
        if (pages is { } n)
        {
            var c = b.Run("qpdf zählt Seiten", qpdf, "--show-npages", pdf);
            Assert.Equal(n.ToString(System.Globalization.CultureInfo.InvariantCulture), c.Out.Trim());
        }
    }

    static string PageText(Beleg b, string pdf, int page)
    {
        var t = PdfView.Text(pdf, page);
        b.Line($"PDFium Text {Path.GetFileName(pdf)} Seite {page + 1}: {System.Text.RegularExpressions.Regex.Replace(t, @"\s+", " ").Trim()}");
        return System.Text.RegularExpressions.Regex.Replace(t, @"\s+", " ");
    }

    static void PdfToText(Beleg b, string pdf, string expect)
    {
        if (Cli.Find("pdftotext") is not { } p) { b.Line("pdftotext: nicht auf diesem PC – nur PDFium und qpdf geprüft."); return; }
        var r = b.Run("pdftotext (Poppler) liest " + Path.GetFileName(pdf), p, "-layout", pdf, "-");
        Assert.True(r.Ok, "pdftotext Exit " + r.Exit);
        Assert.Contains(expect, System.Text.RegularExpressions.Regex.Replace(r.Out, @"\s+", " "));
    }

    static (MainWindow W, string Root) Window()
    {
        var (w, _, root) = Ui.Open(); w.Width = 1600; w.Height = 1200;
        Directory.CreateDirectory(root);
        return (w, root);
    }

    // ---------- PDF-Werkstatt ----------

    [AvaloniaFact(DisplayName = "T-0705 Echtlauf PDF-Werkstatt: LibreOffice-PDF (3 Seiten) ablegen, Seite 3 nach vorn ziehen, drehen, Seite löschen, Ausführen – qpdf prüft Struktur, Seitenzahl und Drehung, LibreOffice öffnet das Ergebnis")]
    public void Pdf_real()
    {
        var (lo, qpdf) = Need();
        var (w, root) = Window();
        using var b = new Beleg("pdf-werkstatt");
        try
        {
            b.Line("Werkzeuge: LibreOffice " + Path.GetFileName(lo) + ", qpdf " + b.Run("qpdf Version", qpdf, "--version").Out.Split('\n')[0]);
            var src = LoConvert(b, lo, Fodt(Path.Combine(root, "amtsbrief.fodt"), 20, "Seite eins vom Amt", "Seite zwei vom Amt", "Seite drei vom Amt"), "pdf", "pdf");
            var foreign = b.Keep(src, "fremd-amtsbrief-libreoffice.pdf");
            QpdfCheck(b, qpdf, src, 3);
            var before = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(src)));

            StartEmpty(w, MainWindow.PwKey, "ordnen");
            Drop(w, "prog-first", src); b.Line("Oberfläche: PDF auf den ersten Schritt gezogen (Ablegen-Ereignis des Fensters).");
            Drag(w, "pw-page-2", "pw-page-0"); b.Line("Oberfläche: Seite 3 mit der Maus an den Anfang gezogen.");
            Menu(w, "pw-page-0", "pw-ctx-rot-right"); b.Line("Oberfläche: Rechtsklick Seite 1 → Nach rechts drehen.");
            Menu(w, "pw-page-2", "pw-ctx-del"); b.Line("Oberfläche: Rechtsklick Seite 3 → Seite löschen.");
            Click(w, "pw-run-now");
            var d = w.ProgramActive(MainWindow.PwKey)!;
            var res = b.Keep(d.List("results").Select(r => r!["file"].AsText()!).Last(), "ergebnis-geordnet.pdf");
            QpdfCheck(b, qpdf, res, 2);
            var json = b.Run("qpdf liest die Seitenobjekte (Drehung)", qpdf, "--json", "--json-key=qpdf", res).Out;
            Assert.Contains("\"/Rotate\": 90", json);
            Assert.Contains("Seite drei", PageText(b, res, 0)); Assert.Contains("Seite eins", PageText(b, res, 1));
            PdfToText(b, res, "Seite drei");
            var png = LoConvert(b, lo, res, "png", "png"); b.Keep(png, "ergebnis-geordnet-libreoffice.png");
            Assert.Equal(before, Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(src)))); b.Line("Original unverändert (SHA-256 gleich).");
            _ = foreign; b.Line("ERGEBNIS: bestanden");
        }
        catch (Exception e) when (!e.Message.Contains("BLOCKIERT", StringComparison.Ordinal)) { b.Line("ERGEBNIS: FEHLER – " + e.Message); throw; }
        finally { w.Close(); }
    }

    // ---------- Formulare ----------

    [AvaloniaFact(DisplayName = "T-0705 Echtlauf Formulare: AcroForm-PDF von LibreOffice öffnen, Felder auf der Oberfläche ausfüllen, speichern und geglättet speichern – qpdf liest die Feldwerte bzw. findet keine Felder mehr, LibreOffice öffnet beide")]
    public void Forms_real()
    {
        var (lo, qpdf) = Need();
        var (w, root) = Window();
        using var b = new Beleg("formulare");
        try
        {
            var form = LoMake(b, lo, "formular", Path.Combine(root, "antrag-libreoffice.pdf"));
            b.Keep(form, "fremd-antrag-libreoffice.pdf");
            var fieldsJson = b.Run("qpdf liest die Formularfelder des Originals", qpdf, "--json", "--json-key=acroform", form).Out;
            Assert.Contains("\"hasacroform\": true", fieldsJson); Assert.Contains("Nachname", fieldsJson);

            StartEmpty(w, MainWindow.FoKey, "oeffnen");
            w.PickOverride = _ => Task.FromResult<string[]?>([form]);
            Click(w, "prog-first-step"); b.Line("Oberfläche: „Formular öffnen …“ → " + w.ProgramMessage(MainWindow.FoKey));
            var d = w.ProgramActive(MainWindow.FoKey)!;
            var names = PdfFormular.Fields(form).Select(f => f.Name).ToList();
            b.Line("Trendo erkennt Felder: " + string.Join(", ", names));
            TypeInto(w, "fo-field-" + names.IndexOf("Nachname"), "Muster");
            TypeInto(w, "fo-field-" + names.IndexOf("Wohnort"), "Musterstadt");
            RealClick(w, V<CheckBox>(w, "fo-field-" + names.IndexOf("Zustimmung"))); Settle();
            b.Line("Oberfläche: Nachname, Wohnort getippt, Ankreuzfeld angeklickt.");
            Click(w, "fo-save-now");
            var saved = b.Keep(d.Text("lastSaved"), "ausgefuellt.pdf");
            QpdfCheck(b, qpdf, saved, 1);
            var filled = b.Run("qpdf liest die Feldwerte", qpdf, "--json", "--json-key=acroform", saved).Out;
            // qpdf-JSON v2 schreibt Textwerte als „u:…“ (Unicode), Ankreuzfelder als Namen („/Yes“)
            Assert.Contains("\"value\": \"u:Muster\"", filled); Assert.Contains("\"value\": \"u:Musterstadt\"", filled);
            Assert.Matches("\"fullname\": \"Zustimmung\"[\\s\\S]*?\"value\": \"/Yes\"", filled);
            Click(w, "fo-save-flat-now");
            var flat = b.Keep(d.Text("lastSaved"), "fertig-geglaettet.pdf");
            QpdfCheck(b, qpdf, flat, 1);
            var flatJson = b.Run("qpdf sucht Formularfelder im geglätteten PDF", qpdf, "--json", "--json-key=acroform", flat).Out;
            Assert.DoesNotContain("Nachname", flatJson);
            var text = PageText(b, flat, 0); Assert.Contains("Musterstadt", text);
            PdfToText(b, flat, "Musterstadt");
            b.Keep(LoConvert(b, lo, saved, "png", "png"), "ausgefuellt-libreoffice.png");
            b.Keep(LoConvert(b, lo, flat, "png", "png"), "fertig-libreoffice.png");
            b.Line("ERGEBNIS: bestanden");
        }
        catch (Exception e) when (!e.Message.Contains("BLOCKIERT", StringComparison.Ordinal)) { b.Line("ERGEBNIS: FEHLER – " + e.Message); throw; }
        finally { w.PickOverride = null; w.Close(); }
    }

    // ---------- Scannen ----------

    [AvaloniaFact(DisplayName = "T-0705 Echtlauf Scannen: Seitenbild von LibreOffice ablegen, „Durchsuchbare PDF“ mit dem echten Tesseract – qpdf prüft, die Textebene enthält den Brieftext")]
    public async Task Scan_real()
    {
        var (lo, qpdf) = Need("tesseract");
        var tess = Tool("tesseract")!;
        var (w, root) = Window();
        using var b = new Beleg("scannen");
        try
        {
            b.Line("Tesseract: " + b.Run("Tesseract Version", tess, "--version").Out.Split('\n')[0]);
            var png = LoConvert(b, lo, Fodt(Path.Combine(root, "rechnung.fodt"), 30, "Stadtwerke Musterstadt\nRechnung Nummer 4711\nBetrag 128,40 Euro"), "png", "png");
            b.Keep(png, "fremd-seite-libreoffice.png");
            StartEmpty(w, MainWindow.ScanKey, "leer");
            Drop(w, "prog-first", png); b.Line("Oberfläche: Seitenbild auf den ersten Schritt gezogen → " + w.ProgramMessage(MainWindow.ScanKey));
            MainWindow.ScanTesseractOverride = () => tess; // nur der Fundort – es läuft das echte Tesseract
            Cmd(w, "prog-cmd-scan-ocr");
            var d = w.ProgramActive(MainWindow.ScanKey)!;
            for (var i = 0; i < 1800 && !(d.Text("last").EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) && File.Exists(d.Text("last"))); i++) { await Task.Delay(100); Settle(); }
            b.Line("Meldung: " + w.ProgramMessage(MainWindow.ScanKey));
            var pdf = b.Keep(d.Text("last"), "durchsuchbar.pdf");
            QpdfCheck(b, qpdf, pdf, 1);
            var text = PageText(b, pdf, 0);
            Assert.Contains("4711", text); Assert.Contains("Rechnung", text);
            PdfToText(b, pdf, "4711");
            b.Keep(LoConvert(b, lo, pdf, "png", "png"), "durchsuchbar-libreoffice.png");
            b.Line("ERGEBNIS: bestanden");
        }
        catch (Exception e) when (!e.Message.Contains("BLOCKIERT", StringComparison.Ordinal)) { b.Line("ERGEBNIS: FEHLER – " + e.Message); throw; }
        finally { MainWindow.ScanTesseractOverride = null; w.Close(); }
    }

    // ---------- Präsentation ----------

    [AvaloniaFact(DisplayName = "T-0705 Echtlauf Präsentation gestalten: PowerPoint von LibreOffice ablegen (Titel, Punkte, Notizen übernommen), Folie umsortieren, als PPTX exportieren – LibreOffice öffnet sie, qpdf zählt die Folien")]
    public void Deck_real()
    {
        var (lo, qpdf) = Need();
        var (w, root) = Window();
        using var b = new Beleg("praesentation");
        try
        {
            var pptx = LoMake(b, lo, "praesentation", Path.Combine(root, "verein-libreoffice.pptx"));
            b.Keep(pptx, "fremd-verein-libreoffice.pptx");
            StartEmpty(w, MainWindow.DeckDesignKey, "leer");
            Drop(w, "prog-first", pptx); b.Line("Oberfläche: PPTX auf den ersten Schritt gezogen → " + w.ProgramMessage(MainWindow.DeckDesignKey));
            var d = w.ProgramActive(MainWindow.DeckDesignKey)!;
            var secs = DeckDesign.Parse(d.Text("outline"));
            b.Line("Übernommene Gliederung:\n" + d.Text("outline"));
            Assert.Equal(["Vereinsjahr 2026", "Was wir geschafft haben", "Was wir vorhaben"], secs.Select(s => s.Title));
            Assert.Contains("120 neue Mitglieder", secs[1].Points); Assert.Contains("Den Helfern danken.", secs[1].Notes);
            Drag(w, "dd-thumb-2", "dd-thumb-1"); b.Line("Oberfläche: Folie 3 vor Folie 2 gezogen.");
            Assert.Equal(["Vereinsjahr 2026", "Was wir vorhaben", "Was wir geschafft haben"], DeckDesign.Parse(d.Text("outline")).Select(s => s.Title));
            Cmd(w, "prog-export-pptx");
            var mine = b.Keep(Exported(w, MainWindow.DeckDesignKey), "gestaltet.pptx");
            var pdf = LoConvert(b, lo, mine, "pdf", "pdf"); b.Keep(pdf, "gestaltet-libreoffice.pdf");
            QpdfCheck(b, qpdf, pdf, 3);
            Assert.Contains("Was wir vorhaben", PageText(b, pdf, 1));
            Assert.Contains("Was wir geschafft haben", PageText(b, pdf, 2));
            b.Line("ERGEBNIS: bestanden");
        }
        catch (Exception e) when (!e.Message.Contains("BLOCKIERT", StringComparison.Ordinal)) { b.Line("ERGEBNIS: FEHLER – " + e.Message); throw; }
        finally { w.Close(); }
    }

    // ---------- Bewerbung ----------

    [AvaloniaFact(DisplayName = "T-0705 Echtlauf Bewerbung: Zeugnis-PDF von LibreOffice als Anlage ablegen, Lebenslauf als Word und PDF, Mappe als PDF – LibreOffice öffnet das DOCX, qpdf prüft PDF und Mappe (Anlage hinten)")]
    public void Cv_real()
    {
        var (lo, qpdf) = Need();
        var (w, root) = Window();
        using var b = new Beleg("bewerbung");
        MainWindow.CvNowOverride = () => new DateTime(2026, 11, 16);
        try
        {
            var zeugnis = LoConvert(b, lo, Fodt(Path.Combine(root, "zeugnis.fodt"), 16, "Arbeitszeugnis\nFrau Erika Muster war bei uns als Bürokauffrau tätig."), "pdf", "pdf");
            b.Keep(zeugnis, "fremd-zeugnis-libreoffice.pdf");
            w.ProgramSetLevel(UserLevel.Erweitert); w.Show(MainWindow.CvKey); Settle();
            RealClick(w, V<Button>(w, "prog-tpl-beispiel")); Settle();
            var d = w.ProgramActive(MainWindow.CvKey)!;
            var name = MainWindow.CvOf(d).Cv.Person.Name;
            Drop(w, "cv-tabs", zeugnis); b.Line("Oberfläche: Zeugnis auf die Arbeitsfläche gezogen → " + w.ProgramMessage(MainWindow.CvKey));
            Assert.Single(MainWindow.CvOf(d).Anlagen);
            Cmd(w, "prog-export-docx");
            var docx = b.Keep(Exported(w, MainWindow.CvKey), "lebenslauf.docx");
            var fromWord = LoConvert(b, lo, docx, "pdf", "pdf"); b.Keep(fromWord, "lebenslauf-docx-libreoffice.pdf");
            QpdfCheck(b, qpdf, fromWord);
            Assert.Contains(name, PageText(b, fromWord, 0));
            Cmd(w, "prog-export-pdf");
            var pdf = b.Keep(Exported(w, MainWindow.CvKey), "lebenslauf.pdf");
            QpdfCheck(b, qpdf, pdf); Assert.Contains(name, PageText(b, pdf, 0));
            Cmd(w, "prog-export-mappe");
            var mappe = b.Keep(Exported(w, MainWindow.CvKey), "mappe.pdf");
            QpdfCheck(b, qpdf, mappe);
            var n = PdfView.PageCount(mappe); b.Line($"Mappe: {n} Seiten");
            Assert.Contains("Arbeitszeugnis", PageText(b, mappe, n - 1));
            PdfToText(b, mappe, "Arbeitszeugnis");
            b.Keep(LoConvert(b, lo, mappe, "png", "png"), "mappe-libreoffice.png");
            b.Line("ERGEBNIS: bestanden");
        }
        catch (Exception e) when (!e.Message.Contains("BLOCKIERT", StringComparison.Ordinal)) { b.Line("ERGEBNIS: FEHLER – " + e.Message); throw; }
        finally { MainWindow.CvNowOverride = null; w.Close(); }
    }

    // ---------- Karten und Verträge ----------

    [AvaloniaFact(DisplayName = "T-0705 Echtlauf Karten und Verträge: Druck-PDF der Karte und Kündigungsschreiben – qpdf prüft, LibreOffice öffnet beide; Kalenderdatei mit Erinnerung protokolliert")]
    public void Cards_contracts_real()
    {
        var (lo, qpdf) = Need();
        var (w, _) = Window();
        using var b = new Beleg("karten-vertraege");
        MainWindow.ConNowOverride = () => new DateTime(2026, 11, 16);
        try
        {
            w.ProgramSetLevel(UserLevel.Erweitert);
            w.Show(MainWindow.CardKey); Settle(); RealClick(w, V<Button>(w, "prog-tpl-hochzeit")); Settle();
            Cmd(w, "prog-export-pdf");
            var card = b.Keep(Exported(w, MainWindow.CardKey), "karte-druck.pdf");
            QpdfCheck(b, qpdf, card);
            b.Keep(LoConvert(b, lo, card, "png", "png"), "karte-libreoffice.png");

            w.Show(MainWindow.ConKey); Settle(); RealClick(w, V<Button>(w, "prog-tpl-beispiel")); Settle();
            Menu(w, "con-row-0", "con-ctx-letter-pdf");
            var letter = w.ProgramMessage(MainWindow.ConKey).Split("PDF: ")[1].Split(" – ")[0];
            var kept = b.Keep(letter, "kuendigung.pdf");
            QpdfCheck(b, qpdf, kept, 1);
            Assert.Contains("Kündigung", PageText(b, kept, 0));
            b.Keep(LoConvert(b, lo, kept, "png", "png"), "kuendigung-libreoffice.png");
            Cmd(w, "prog-export-ics");
            var ics = b.Keep(Exported(w, MainWindow.ConKey), "erinnerungen.ics");
            var txt = File.ReadAllText(ics);
            Assert.Contains("BEGIN:VALARM", txt);
            b.Line($"Kalenderdatei: {txt.Split("BEGIN:VEVENT").Length - 1} Termine mit Erinnerung (VALARM). Kein Kalenderprogramm auf diesem PC – Import in einen Kalender NICHT GEPRÜFT.");
            b.Line("ERGEBNIS: bestanden");
        }
        catch (Exception e) when (!e.Message.Contains("BLOCKIERT", StringComparison.Ordinal)) { b.Line("ERGEBNIS: FEHLER – " + e.Message); throw; }
        finally { MainWindow.ConNowOverride = null; w.Close(); }
    }
}
