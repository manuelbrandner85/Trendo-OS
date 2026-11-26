using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Trendo.Core;
using Trendo.Core.Wissen;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>T-0779 Beleg mit echten Fremdprogrammen dieses PCs (nur mit TRENDO_T0779_FREMD=&lt;Belegordner&gt;, nichts wird installiert):
/// Google Chrome (headless) spielt das HTML-Quiz und den Kurs selbst durch (Selbsttest „#selbsttest“), 7-Zip prüft und entpackt das SCORM-Paket,
/// Chrome öffnet die entpackte index.html, pdftotext (Xpdf aus Git für Windows) liest das Druck-PDF, qpdf prüft dessen Aufbau.
/// Ehrlich: Moodle oder eine andere Lernplattform ist hier nicht installiert – der GIFT-Import und der SCORM-Lauf in einer Plattform sind nicht geprüft.</summary>
public class T0779_Fremd
{
    static string? Beleg() => Environment.GetEnvironmentVariable("TRENDO_T0779_FREMD") is { Length: > 0 } d ? Directory.CreateDirectory(d).FullName : null;
    static string Hash(string f) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(f))).ToLowerInvariant();
    /// <summary>Google Chrome. Microsoft Edge 159 dieses PCs beendete sich headless sofort ohne Ausgabe (auch ohne Trendo) – darum Chrome.</summary>
    static readonly string Browser = @"C:\Program Files\Google\Chrome\Application\chrome.exe";
    static readonly string SevenZip = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TrendoWerkzeuge", "7zip", "7z.exe");
    static readonly string Qpdf = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TrendoWerkzeuge", "qpdf", "bin", "qpdf.exe");
    static readonly string PdfToText = @"C:\Program Files\Git\mingw64\bin\pdftotext.exe";

    static (int Code, string Out) Run(StringBuilder log, string exe, params string[] args)
    {
        var psi = new ProcessStartInfo(exe) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true, StandardOutputEncoding = Encoding.UTF8 };
        foreach (var a in args) psi.ArgumentList.Add(a);
        using var p = Process.Start(psi)!;
        var o = p.StandardOutput.ReadToEndAsync(); var e = p.StandardError.ReadToEndAsync();
        if (!p.WaitForExit(120_000)) { p.Kill(true); throw new TimeoutException(exe); }
        var output = o.Result + e.Result;
        log.AppendLine($"$ {Path.GetFileName(exe)} {string.Join(' ', args.Select(x => x.Contains(' ') ? "\"" + x + "\"" : x))}");
        log.AppendLine($"Exit {p.ExitCode}");
        log.AppendLine(output.Length > 4000 ? output[..4000] + " …(gekürzt)" : output);
        return (p.ExitCode, output);
    }

    [Fact(DisplayName = "T-0779 FREMD: Chrome spielt HTML-Quiz und Kurs durch, 7-Zip prüft/entpackt SCORM, Chrome öffnet entpackte index.html, pdftotext und qpdf lesen das PDF (nur mit TRENDO_T0779_FREMD)")]
    public void Foreign_programs()
    {
        if (Beleg() is not { } beleg) { Assert.Skip("Nur mit TRENDO_T0779_FREMD=<Belegordner> (Chrome, 7-Zip, qpdf, pdftotext dieses PCs)."); return; }
        foreach (var t in new[] { Browser, SevenZip, Qpdf, PdfToText }) Assert.True(File.Exists(t), "fehlt: " + t);
        var tmp = Path.Combine(Path.GetTempPath(), "trendo-t0779-fremd-" + Guid.NewGuid().ToString("N")[..8]); Directory.CreateDirectory(tmp);
        var log = new StringBuilder();
        log.AppendLine($"# T-0779 Fremdprogramme – {DateTime.Now:yyyy-MM-dd HH:mm}"); log.AppendLine();
        try
        {
            var q = new Quiz
            {
                Titel = "Europa-Quiz", Beschreibung = "Beleg T-0779", Sekunden = 30, TempoBonus = true,
                Fragen =
                [
                    new() { Typ = "auswahl", Text = "Hauptstadt von Frankreich? (<!-- ist hier kein Kommentar)", Antworten = [new() { Text = "Paris", Richtig = true }, new() { Text = "Lyon" }, new() { Text = "Nizza" }] },
                    new() { Typ = "mehrfach", Text = "Nachbarn von Deutschland?", Antworten = [new() { Text = "Polen", Richtig = true }, new() { Text = "Österreich", Richtig = true }, new() { Text = "Spanien" }], Punkte = 2 },
                    new() { Typ = "wahrfalsch", Text = "Die Donau mündet ins Schwarze Meer.", Antworten = QuizKit.WahrFalsch(true) },
                    new() { Typ = "luecke", Text = "Der höchste Berg Deutschlands ist die [Zugspitze]." },
                    new() { Typ = "zuordnen", Text = "Hauptstädte", Paare = [new() { Links = "Italien", Rechts = "Rom" }, new() { Links = "Spanien", Rechts = "Madrid" }, new() { Links = "Polen", Rechts = "Warschau" }], Punkte = 3 },
                ],
            };
            var html = Path.Combine(beleg, "quiz.html"); QuizHtml.Write(q, html);
            var pdf = Path.Combine(beleg, "quiz.pdf"); QuizPdf.Write(q, pdf);
            var gift = Path.Combine(beleg, "quiz-moodle.gift.txt"); AtomicFile.WriteAllText(gift, QuizGift.Write(q));
            var k = new Kurs
            {
                Titel = "Europa kennenlernen", Beschreibung = "Beleg T-0779",
                Kapitel =
                [
                    new() { Titel = "Grundlagen", Lektionen = [new() { Titel = "Länder", Bloecke = [new() { Art = "text", Text = "Europa hat viele Länder." }, new() { Art = "liste", Text = "Frankreich\nPolen" }] }] },
                    new() { Titel = "Prüfung", Lektionen = [new() { Titel = "Quiz", Bloecke = [new() { Art = "hinweis", Text = "Mindestens 60 %." }, new() { Art = "quiz", Quiz = q }] }] },
                ],
                Lernende = [new() { Name = "Erika Muster", Erledigt = [] }],
            };
            var khtml = Path.Combine(beleg, "kurs.html"); KursHtml.Write(k, khtml);
            var zip = Path.Combine(beleg, "kurs-scorm.zip"); KursHtml.WriteScorm(k, zip);
            log.AppendLine("Dateien (von Trendo erzeugt):");
            foreach (var f in new[] { html, pdf, gift, khtml, zip }) log.AppendLine($"- {Path.GetFileName(f)}: {new FileInfo(f).Length} Bytes, SHA-256 {Hash(f)}");
            log.AppendLine();

            string Dom(string file, string hash = "#selbsttest")
            {
                var prof = Path.Combine(tmp, "edge-" + Guid.NewGuid().ToString("N")[..6]);
                var url = new Uri(file).AbsoluteUri + hash;
                var (code, outp) = Run(log, Browser, "--headless=new", "--disable-gpu", "--no-first-run", "--no-default-browser-check", "--user-data-dir=" + prof, "--virtual-time-budget=15000", "--dump-dom", url);
                Assert.Equal(0, code);
                return outp;
            }
            log.AppendLine("## Google Chrome (headless) – HTML-Quiz mit #selbsttest");
            var dom = Dom(html);
            Assert.Contains("data-tq-result=\"100\"", dom); Assert.Contains("Auswertung", dom); Assert.Contains("Bestanden", dom);
            log.AppendLine("→ Ergebnis im DOM: data-tq-result=\"100\", „Auswertung“, „Bestanden“ (Fragetext mit „<!--“ fehlerfrei gelesen)"); log.AppendLine();
            log.AppendLine("## Google Chrome (headless) – Lückenregel des Spielers (#regeln) gegen Trendo (LearnBox.CheckTyped)");
            dom = Dom(html, "#regeln");
            Assert.Contains($"data-tq-rules=\"{QuizHtml.RuleExpectation}\"", dom);
            log.AppendLine($"→ data-tq-rules=\"{QuizHtml.RuleExpectation}\" – gleich wie in Trendo (Fälle: {string.Join(", ", QuizHtml.RuleCases.Select(c => c.Expected + "/" + c.Typed))})"); log.AppendLine();

            log.AppendLine("## Google Chrome (headless) – Kurs-Webseite mit #selbsttest");
            dom = Dom(khtml);
            Assert.Contains("data-k-selftest=\"fertig\"", dom); Assert.Contains("data-k-done=\"2\"", dom);
            log.AppendLine("→ Ergebnis im DOM: data-k-selftest=\"fertig\", data-k-done=\"2\" (beide Lektionen erledigt, Quiz bestanden)"); log.AppendLine();

            log.AppendLine("## 7-Zip – SCORM-Paket prüfen und entpacken");
            var (c1, t1) = Run(log, SevenZip, "t", zip); Assert.Equal(0, c1); Assert.Contains("Everything is Ok", t1);
            var outDir = Path.Combine(tmp, "scorm");
            var (c2, _) = Run(log, SevenZip, "x", zip, "-o" + outDir, "-y"); Assert.Equal(0, c2);
            Assert.True(File.Exists(Path.Combine(outDir, "imsmanifest.xml"))); Assert.True(File.Exists(Path.Combine(outDir, "index.html")));
            log.AppendLine("## Google Chrome (headless) – entpackte SCORM-index.html ohne Lernplattform (Seite läuft auch ohne SCORM-API)");
            dom = Dom(Path.Combine(outDir, "index.html"));
            Assert.Contains("data-k-selftest=\"fertig\"", dom);
            log.AppendLine("→ data-k-selftest=\"fertig\""); log.AppendLine();

            log.AppendLine("## pdftotext (Xpdf aus Git für Windows) – Druck-PDF lesen");
            var txt = Path.Combine(tmp, "quiz.txt");
            var (c3, _) = Run(log, PdfToText, "-enc", "UTF-8", pdf, txt); Assert.Equal(0, c3);
            var text = File.ReadAllText(txt);
            Assert.Contains("Hauptstadt von Frankreich?", text); Assert.Contains("Lösungen", text); Assert.Contains("Zugspitze", text);
            log.AppendLine("→ Text enthält „Hauptstadt von Frankreich?“, „Lösungen“, „Zugspitze“:"); log.AppendLine(text.Length > 1500 ? text[..1500] + " …" : text);
            log.AppendLine("## qpdf – Aufbau des PDF prüfen");
            // qpdf meldet bei den Flate-Datenströmen von PDFsharp Warnungen („input stream is complete but output may still be valid“, Exit 3) – keine Fehler.
            var (c4, t4) = Run(log, Qpdf, "--check", pdf);
            Assert.True(c4 == 0 || c4 == 3 && t4.Contains("operation succeeded with warnings"), "qpdf: " + t4);
            Assert.DoesNotContain("ERROR", t4, StringComparison.OrdinalIgnoreCase); Assert.Contains("PDF Version", t4);
            log.AppendLine(c4 == 0 ? "→ qpdf: keine Fehler" : "→ qpdf: lesbar, keine Fehler; Warnungen zu den Datenströmen (PDFsharp-Flate), siehe oben");
            log.AppendLine(); log.AppendLine("Nicht geprüft (kein Programm auf diesem PC): GIFT-Import in Moodle, SCORM-Lauf in einer Lernplattform (Moodle, ILIAS, SCORM Cloud).");
            log.AppendLine("Ergebnis: alle Fremdprogramm-Prüfungen bestanden.");
        }
        catch (Exception e) { log.AppendLine("FEHLER: " + e.Message); throw; }
        finally
        {
            var clean = log.ToString().Replace(tmp, "<temp>").Replace(beleg, "<beleg>").Replace(Environment.UserName, "<user>");
            File.WriteAllText(Path.Combine(beleg, "fremdprogramme.log"), clean, new UTF8Encoding(false));
            try { Directory.Delete(tmp, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }
}
