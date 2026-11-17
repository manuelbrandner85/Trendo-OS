using System.Security.Cryptography;
using System.Text;
using Avalonia.Headless.XUnit;
using Trendo.App;
using Trendo.Core;
using Trendo.Core.Kreativ;
using Trendo.Core.Office;
using Trendo.Core.Programs;
using Trendo.Core.Sys;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// <summary>T-0725 Echtlauf mit echten Fremdprogrammen (nichts ersetzt außer der Dateiauswahl): LibreOffice schreibt die Fremddateien (DOCX/PDF aus einem
/// OpenDocument-Text) und öffnet die Trendo-Ergebnisse, qpdf prüft PDF-Struktur und Seitenzahl, Ical.Net liest die Kalenderdatei. Fehlt ein Werkzeug,
/// wird der Test mit „BLOCKIERT“ übersprungen. Protokoll und Dateien: TRENDO_BELEG_DIR; <c>tools/echtlauf/fachstudios/t0725.py</c> legt sie unter reports/T-0725/ ab.</summary>
[Collection("Claude")]
public class T0725_Echtlauf_Ui
{
    static string? Tool(string id) => Cli.Find(id) ?? new DependencyManager(new AuditLog(Path.Combine(Path.GetTempPath(), "trendo-t0725-needs.jsonl")), new RealProcessRunner()).ResolvePath(DependencyCatalog.Get(id));

    sealed class Beleg : IDisposable
    {
        readonly StringBuilder _log = new(); readonly string _name;
        public string Dir { get; }
        public Beleg(string name)
        {
            _name = name;
            Dir = Path.Combine(Environment.GetEnvironmentVariable("TRENDO_BELEG_DIR") is { Length: > 0 } d ? d : Path.Combine(global::Trendo.Core.TempDir.SystemTemp, "trendo-t0725-beleg"), name);
            Directory.CreateDirectory(Dir);
            Line($"# Echtlauf {name} – {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        }
        public void Line(string s) => _log.Append(s).Append('\n');
        public Cli.Result Run(string label, string exe, params string[] args)
        {
            Line($"$ {label}: {Path.GetFileName(exe)} {string.Join(' ', args.Select(a => a.Contains(' ') ? $"\"{a}\"" : a))}");
            var r = Cli.Run(exe, args, timeoutMs: 240000);
            Line($"Exit {r.Exit}");
            if (r.Out.Trim().Length > 0) Line(r.Out.Trim().Length > 4000 ? r.Out.Trim()[..4000] + " …" : r.Out.Trim());
            if (r.Err.Trim().Length > 0) Line("[stderr] " + (r.Err.Trim().Length > 4000 ? r.Err.Trim()[..4000] : r.Err.Trim()));
            return r;
        }
        public string Keep(string file, string? name = null)
        {
            Assert.True(File.Exists(file), "Datei fehlt: " + file);
            var to = Path.Combine(Dir, name ?? Path.GetFileName(file)); File.Copy(file, to, true);
            Line($"Datei {Path.GetFileName(to)}: {new FileInfo(to).Length} Bytes, SHA-256 {Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(to))).ToLowerInvariant()}");
            return to;
        }
        public void Dispose() => File.WriteAllText(Path.Combine(Dir, _name + ".log"), _log.ToString(), new UTF8Encoding(false));
    }

    static (string Lo, string Qpdf) Need()
    {
        var lo = Tool("libreoffice"); var qpdf = Tool("qpdf");
        var missing = new List<string>();
        if (lo is null) missing.Add("LibreOffice (soffice)");
        if (qpdf is null) missing.Add("qpdf");
        if (missing.Count > 0) Assert.Skip("BLOCKIERT – fehlt auf diesem PC: " + string.Join(", ", missing));
        return (lo!, qpdf!);
    }

    static string Tmp(string n) { var d = Path.Combine(Path.GetTempPath(), "trendo-t0725-" + n + "-" + Guid.NewGuid().ToString("N")[..8]); Directory.CreateDirectory(d); return d; }

    /// <summary>LibreOffice wandelt um (eigenes Profil, ohne Fenster); optional mit Eingabefilter. Gibt die Zieldatei zurück.</summary>
    static string Lo(Beleg b, string lo, string file, string format, string ext, string? infilter = null)
    {
        var outDir = Tmp("lo-out"); var prof = Tmp("lo-prof");
        try
        {
            var args = new List<string> { "-env:UserInstallation=" + new Uri(prof).AbsoluteUri, "--headless", "--norestore" };
            if (infilter is not null) args.Add("--infilter=" + infilter);
            args.AddRange(["--convert-to", format, "--outdir", outDir, file]);
            var r = b.Run("LibreOffice öffnet " + Path.GetFileName(file), lo, [.. args]);
            var target = Path.Combine(outDir, Path.GetFileNameWithoutExtension(file) + "." + ext);
            Assert.True(r.Ok && File.Exists(target), $"LibreOffice konnte {Path.GetFileName(file)} nicht öffnen/umwandeln (Exit {r.Exit}).");
            return target;
        }
        finally { try { Directory.Delete(prof, true); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
    }

    static void Qpdf(Beleg b, string qpdf, string pdf, int? minPages = null)
    {
        var r = b.Run("qpdf prüft " + Path.GetFileName(pdf), qpdf, "--check", pdf);
        var warnings = (r.Out + "\n" + r.Err).Split('\n').Where(l => l.Contains("WARNING:", StringComparison.Ordinal)).ToList();
        var onlyFlate = r.Exit == 3 && warnings.Count > 0 && warnings.All(w => w.Contains("input stream is complete but output may still be valid"));
        if (onlyFlate) b.Line("qpdf: gültig, nur Warnung „Flate-Strom ohne Abschluss“ (PDFsharp, bekannt aus T-0705) – keine Fehler.");
        Assert.True(r.Exit == 0 || onlyFlate, $"qpdf meldet Fehler in {Path.GetFileName(pdf)} (Exit {r.Exit}): {r.Out} {r.Err}");
        if (minPages is { } n)
        {
            var c = b.Run("qpdf zählt Seiten", qpdf, "--show-npages", pdf);
            Assert.True(int.TryParse(c.Out.Trim(), out var pages) && pages >= n, $"{Path.GetFileName(pdf)}: {c.Out.Trim()} Seiten, erwartet mindestens {n}");
        }
    }

    static string X(string s) => System.Security.SecurityElement.Escape(s);

    /// <summary>OpenDocument-Text (flach): Absätze, Überschriften (Stufe 1), fett/kursiv in einem Absatz – daraus schreibt LibreOffice die Fremddatei.</summary>
    static string Fodt(string path, IEnumerable<(string Kind, string Text)> paras)
    {
        var sb = new StringBuilder("<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<office:document xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" xmlns:style=\"urn:oasis:names:tc:opendocument:xmlns:style:1.0\" "
            + "xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\" xmlns:fo=\"urn:oasis:names:tc:opendocument:xmlns:xsl-fo-compatible:1.0\" office:version=\"1.3\" office:mimetype=\"application/vnd.oasis.opendocument.text\">\n"
            // Formatvorlagen wie in einem normalen Dokument: „Heading 1“ (Gliederungsebene 1) und „Title“ – Writer/Word speichern sie als Heading1/Title
            + "<office:styles><style:style style:name=\"Standard\" style:family=\"paragraph\"/><style:style style:name=\"Heading_20_1\" style:display-name=\"Heading 1\" style:family=\"paragraph\" style:parent-style-name=\"Standard\" style:default-outline-level=\"1\"><style:text-properties fo:font-size=\"18pt\" fo:font-weight=\"bold\"/></style:style>"
            + "<style:style style:name=\"Title\" style:family=\"paragraph\" style:parent-style-name=\"Standard\"><style:text-properties fo:font-size=\"28pt\"/></style:style></office:styles>\n"
            + "<office:automatic-styles><style:style style:name=\"T1\" style:family=\"text\"><style:text-properties fo:font-weight=\"bold\"/></style:style><style:style style:name=\"T2\" style:family=\"text\"><style:text-properties fo:font-style=\"italic\"/></style:style></office:automatic-styles>\n<office:body><office:text>\n");
        foreach (var (kind, text) in paras)
        {
            // **fett** und *kursiv* als echte Zeichenformate
            var inner = System.Text.RegularExpressions.Regex.Replace(X(text), @"\*\*(.+?)\*\*", "<text:span text:style-name=\"T1\">$1</text:span>");
            inner = System.Text.RegularExpressions.Regex.Replace(inner, @"\*(.+?)\*", "<text:span text:style-name=\"T2\">$1</text:span>");
            sb.Append(kind switch
            {
                "title" => $"<text:p text:style-name=\"Title\">{inner}</text:p>\n",
                "h1" => $"<text:h text:style-name=\"Heading_20_1\" text:outline-level=\"1\">{inner}</text:h>\n",
                _ => $"<text:p text:style-name=\"Standard\">{inner}</text:p>\n",
            });
        }
        sb.Append("</office:text></office:body></office:document>\n");
        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false)); return path;
    }

    [AvaloniaFact(DisplayName = "T-0725 Echtlauf Bücher: LibreOffice schreibt ein Word-Manuskript (Titel, Kapitel, fett, kursiv), Trendo übernimmt es per „Manuskript öffnen …“; LibreOffice öffnet das Trendo-DOCX und -ODT, qpdf prüft das Druck-PDF")]
    public void Book_roundtrip_with_libreoffice()
    {
        var (lo, qpdf) = Need();
        using var b = new Beleg("buecher");
        var dir = Tmp("buch");
        var src = Lo(b, lo, Fodt(Path.Combine(dir, "Sturmnacht.fodt"), [("title", "Sturmnacht"), ("h1", "Ankunft"), ("p", "Die Fähre legte spät an. **Niemand** wartete am Steg."), ("h1", "Der Leuchtturm"), ("p", "Oben *brannte* noch Licht.")]), "docx", "docx");
        b.Keep(src, "fremd-Sturmnacht.docx"); var sha = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(src)));
        var (w, _, _) = Ui.Open();
        try
        {
            const string K = "buch-programm";
            w.Width = 1600; w.Height = 1200; w.Show(K); Settle();
            RealClick(w, w.Find<Avalonia.Controls.Button>("prog-tpl-leer")); Settle();
            var d = w.ProgramActive(K)!;
            w.PickOverride = _ => Task.FromResult<string[]?>([src]);
            BueroAusbau_Ui.Cmd(w, "prog-cmd-book-open");
            var text = d.Text("text");
            b.Line("Trendo-Manuskript nach dem Öffnen:\n" + text);
            Assert.Equal(["Ankunft", "Der Leuchtturm"], BookKit.Chapters(text).Select(c => c.Title).ToArray());
            Assert.Contains("**Niemand**", text); Assert.Contains("*brannte*", text);
            Assert.Equal("Sturmnacht", d.Text("title"));
            Assert.Equal(sha, Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(src))));
            BueroAusbau_Ui.Cmd(w, "prog-export-docx"); var docx = b.Keep(BueroAusbau_Ui.Exported(w, K), "trendo-Sturmnacht.docx");
            BueroAusbau_Ui.Cmd(w, "prog-export-odt"); var odt = b.Keep(BueroAusbau_Ui.Exported(w, K), "trendo-Sturmnacht.odt");
            BueroAusbau_Ui.Cmd(w, "prog-export-pdf"); var pdf = b.Keep(BueroAusbau_Ui.Exported(w, K), "trendo-Sturmnacht.pdf");
            Qpdf(b, qpdf, pdf, 3);
            foreach (var f in new[] { docx, odt })
            {
                var lop = b.Keep(Lo(b, lo, f, "pdf", "pdf"), "lo-" + Path.GetFileNameWithoutExtension(f) + Path.GetExtension(f).Replace('.', '-') + ".pdf");
                Qpdf(b, qpdf, lop, 3);
                var all = string.Concat(Enumerable.Range(0, PdfView.PageCount(lop)).Select(i => PdfView.Text(lop, i)));
                Assert.Contains("Der Leuchtturm", all); Assert.Contains("Niemand wartete", all);
                b.Line($"LibreOffice-PDF aus {Path.GetFileName(f)}: {PdfView.PageCount(lop)} Seiten, Kapitel und Text gefunden.");
            }
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0725 Echtlauf Texte zusammenfassen: LibreOffice schreibt eine Meldung als PDF und als Word-Datei, Trendo liest beide über „Datei öffnen …“ ein")]
    public void Summary_reads_libreoffice_files()
    {
        var (lo, qpdf) = Need();
        using var b = new Beleg("zusammenfassen");
        var dir = Tmp("sum");
        var lines = new[] { "Die Stadt baut einen neuen Radweg entlang des Flusses.", "Er soll im Frühjahr fertig sein und kostet zwei Millionen Euro.", "Während der Bauzeit wird die Uferstraße gesperrt.", "Der Radweg verbindet zwei Stadtteile direkt miteinander." };
        var fodt = Fodt(Path.Combine(dir, "Meldung.fodt"), lines.Select(l => ("p", l)));
        var pdf = b.Keep(Lo(b, lo, fodt, "pdf", "pdf"), "fremd-Meldung.pdf"); Qpdf(b, qpdf, pdf, 1);
        var docx = b.Keep(Lo(b, lo, fodt, "docx", "docx"), "fremd-Meldung.docx");
        var (w, _, _) = Ui.Open();
        try
        {
            const string K = "zusammenfassen-programm";
            w.Width = 1600; w.Height = 1200; w.Show(K); Settle();
            RealClick(w, w.Find<Avalonia.Controls.Button>("prog-tpl-leer")); Settle();
            var d = w.ProgramActive(K)!;
            foreach (var f in new[] { pdf, docx })
            {
                w.PickOverride = _ => Task.FromResult<string[]?>([f]);
                BueroAusbau_Ui.Cmd(w, "prog-cmd-sum-open");
                var t = System.Text.RegularExpressions.Regex.Replace(d.Text("text"), @"\s+", " ");
                b.Line($"{Path.GetFileName(f)} → {t.Length} Zeichen: {w.ProgramMessage(K)}");
                Assert.Contains("Uferstraße gesperrt", t); Assert.Contains("zwei Millionen Euro", t);
            }
            BueroAusbau_Ui.Cmd(w, "prog-export-md"); var md = b.Keep(BueroAusbau_Ui.Exported(w, K), "trendo-Zusammenfassung.md");
            Assert.Contains("Radweg", File.ReadAllText(md));
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0725 Echtlauf Briefe: LibreOffice schreibt einen Brief als Word-Datei, Trendo erkennt die Felder, speichert einen neuen Brief; LibreOffice öffnet ihn, qpdf prüft das PDF")]
    public async Task Letter_from_libreoffice_docx()
    {
        var (lo, qpdf) = Need();
        using var b = new Beleg("briefe");
        var dir = Tmp("brief");
        var src = b.Keep(Lo(b, lo, Fodt(Path.Combine(dir, "Kuendigung.fodt"), T0725_Fachstudios_Ui.LetterLines.Select(l => ("p", l))), "docx", "docx"), "fremd-Kuendigung.docx");
        var sha = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(src)));
        var (w, _, _) = Ui.Open();
        try
        {
            await Ui.OpenLetter(w); Settle();
            w.PickOverride = _ => Task.FromResult<string[]?>([src]);
            BueroAusbau_Ui.Click(w, "letter-open-foreign");
            var l = w.Letter;
            b.Line($"Erkannt: Betreff „{l.Subject}“, Anrede „{l.Salutation}“, Datum {l.Date:dd.MM.yyyy}, Ort „{l.Place}“, Anschrift „{l.Recipient.Replace("\n", " / ")}“, Gruß „{l.Closing}“, Anlagen „{l.Attachments}“");
            Assert.Equal("Kündigung meines Stromvertrags 4711", l.Subject);
            Assert.Equal("Stadtwerke Musterstadt\nKundenservice\nHauptstraße 1\n12345 Musterstadt", l.Recipient);
            Assert.Contains("fristgerecht zum 30.06.2026", l.Body);
            BueroAusbau_Ui.Click(w, "letter-wiz-tab-2");
            BueroAusbau_Ui.TypeInto(w, "letter-sender-name", "Erika Muster");
            BueroAusbau_Ui.TypeInto(w, "letter-sender-street", "Lindenweg 3");
            BueroAusbau_Ui.TypeInto(w, "letter-sender-city", "12345 Musterstadt");
            BueroAusbau_Ui.Click(w, "letter-save");
            var sw = System.Diagnostics.Stopwatch.StartNew();
            while (new LetterArchive(w.ProjectsRootForTests).List().Count == 0 && sw.ElapsedMilliseconds < 15000) { await Task.Delay(20); Settle(); }
            var saved = b.Keep(new LetterArchive(w.ProjectsRootForTests).List().First(), "trendo-Brief.docx");
            var lop = b.Keep(Lo(b, lo, saved, "pdf", "pdf"), "lo-trendo-Brief.pdf");
            Qpdf(b, qpdf, lop, 1);
            var all = System.Text.RegularExpressions.Regex.Replace(string.Concat(Enumerable.Range(0, PdfView.PageCount(lop)).Select(i => PdfView.Text(lop, i))), @"\s+", " ");
            Assert.Contains("Kündigung meines Stromvertrags 4711", all); Assert.Contains("Stadtwerke Musterstadt", all);
            Assert.Equal(sha, Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(src))));
            b.Line("Original unverändert (SHA-256 gleich).");
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0725 Echtlauf Neues Projekt: LibreOffice Calc öffnet die Schritte-CSV (Semikolon, UTF-8), Ical.Net liest die Kalenderdatei; Messwerte: LibreOffice öffnet das Diagramm-PNG")]
    public void Project_and_measure_with_foreign_tools()
    {
        var (lo, qpdf) = Need();
        using var b = new Beleg("vorhaben-messwerte");
        var (w, _, _) = Ui.Open();
        try
        {
            w.Show("vorhaben"); Settle();
            BueroAusbau_Ui.Click(w, "suite-demo");
            var dir = w.SuiteOpenDir!; var steps = CreationSuite.Steps(dir);
            BueroAusbau_Ui.Click(w, "suite-export-csv"); var csv = b.Keep(w.SuiteLastExport!, "trendo-Schritte.csv");
            BueroAusbau_Ui.Click(w, "suite-export-ics"); var ics = b.Keep(w.SuiteLastExport!, "trendo-Zeitplan.ics");
            // Calc liest die CSV mit Semikolon (59), Anführungszeichen (34), UTF-8 (76), ab Zeile 1 und speichert sie als Tabelle – Gegenprobe über das PDF
            var ods = b.Keep(Lo(b, lo, csv, "ods", "ods", "CSV:59,34,76,1"), "lo-Schritte.ods");
            var pdf = b.Keep(Lo(b, lo, ods, "pdf", "pdf"), "lo-Schritte.pdf"); Qpdf(b, qpdf, pdf, 1);
            var all = string.Concat(Enumerable.Range(0, PdfView.PageCount(pdf)).Select(i => PdfView.Text(pdf, i)));
            foreach (var s in steps.Take(3)) Assert.Contains(s.Text.Length > 20 ? s.Text[..20] : s.Text, all);
            Assert.Contains("Kritischer Pfad", all);
            var cal = Ical.Net.Calendar.Load(File.ReadAllText(ics))!;
            b.Line($"Ical.Net: {cal.Events.Count} Termine, erster „{cal.Events.OrderBy(e => e.DtStart!.Value).First().Summary}“");
            Assert.Equal(CreationSuite.Bars(steps).Count, cal.Events.Count);
            // Messwerte → Diagramm als PNG → LibreOffice Draw öffnet es
            const string MK = "wissenschaft";
            w.Width = 1600; w.Height = 1100; w.Show(MK); Settle();
            RealClick(w, w.Find<Avalonia.Controls.Button>("prog-tpl-pendel")); Settle();
            BueroAusbau_Ui.Cmd(w, "prog-export-png");
            var png = b.Keep(BueroAusbau_Ui.Exported(w, MK), "trendo-Diagramm.png");
            var lopdf = b.Keep(Lo(b, lo, png, "pdf", "pdf"), "lo-Diagramm.pdf"); Qpdf(b, qpdf, lopdf, 1);
        }
        finally { w.Close(); }
    }
}
