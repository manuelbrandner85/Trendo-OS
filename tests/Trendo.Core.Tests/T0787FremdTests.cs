using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Trendo.Core.Office;
using Trendo.Core.Wissen;
using Xunit;
using Xunit.Abstractions;

namespace Trendo.Core.Tests;

/// <summary>T-0787: Fremdprogramm-Nachlauf für „Rezepte &amp; Ernährung“ mit Werkzeugen, die auf diesem PC schon liegen (nichts installiert, nichts ersetzt):
/// Google Chrome (headless, eigenes Profil) öffnet die Rezeptseite und liest das eingebettete schema.org-JSON-LD mit seinem eigenen JSON-Leser,
/// LibreOffice (nur aus der Kopie von <see cref="T0776_LoKopie"/>, eigenes Profil) öffnet DOCX, ODT und die CSV-Einkaufsliste, pdftotext liest die PDFs, qpdf prüft deren Aufbau.
/// Nur mit <c>TRENDO_T0787_FREMD=&lt;Belegordner&gt;</c> (Rohlog und SHA-256 je Fall, abgelegt unter reports/T-0787/).</summary>
public class T0787_Fremdnachlauf(T0776_LoKopie loKopie, ITestOutputHelper output) : IClassFixture<T0776_LoKopie>, IDisposable
{
    readonly string _work = Path.Combine(TempDir.SystemTemp, "trendo-t0787-work", Guid.NewGuid().ToString("N")[..8]);
    public void Dispose()
    {
        try { if (Directory.Exists(_work)) Directory.Delete(_work, true); var parent = Path.GetDirectoryName(_work)!; if (Directory.Exists(parent) && !Directory.EnumerateFileSystemEntries(parent).Any()) Directory.Delete(parent); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        GC.SuppressFinalize(this);
    }

    static string? BelegRoot() => Environment.GetEnvironmentVariable("TRENDO_T0787_FREMD") is { Length: > 0 } d ? Directory.CreateDirectory(d).FullName : null;
    static string Need(string? path, string was) { if (path is null || !File.Exists(path)) Ueberspringen.Weil("BLOCKIERT – fehlt auf diesem PC: " + was); return path!; }
    static readonly string Chrome = @"C:\Program Files\Google\Chrome\Application\chrome.exe";
    static readonly string Qpdf = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TrendoWerkzeuge", "qpdf", "bin", "qpdf.exe");
    static string? PdfToText() => Cli.Find("pdftotext") ?? new[] { Environment.GetEnvironmentVariable("ProgramW6432"), Environment.GetEnvironmentVariable("ProgramFiles") }
        .Where(p => p is { Length: > 0 }).Select(p => Path.Combine(p!, "Git", "mingw64", "bin", "pdftotext.exe")).FirstOrDefault(File.Exists);
    string Work(string name) { var d = Path.Combine(_work, name); Directory.CreateDirectory(d); return d; }
    static string Sha(string f) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(f))).ToLowerInvariant();

    sealed class Beleg : IDisposable
    {
        readonly StringBuilder _log = new(); readonly string _name;
        public string Dir { get; }
        public Beleg(string root, string name) { _name = name; Dir = Path.Combine(root, name); Directory.CreateDirectory(Dir); Line($"# T-0787 Fremdnachlauf {name} – {DateTime.Now:yyyy-MM-dd HH:mm:ss zzz}"); }
        public static string Clean(string s)
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile).TrimEnd('\\', '/');
            if (home.Length < 4) return s;
            var user = Path.GetFileName(home);
            foreach (var form in new[] { home, home.Replace('\\', '/'), Uri.EscapeDataString(home), home.Replace('\\', '/').Replace(" ", "%20"), new Uri(home).AbsoluteUri })
                s = s.Replace(form, form[..^user.Length] + "<user>", StringComparison.OrdinalIgnoreCase);
            s = Regex.Replace(s, Regex.Escape(home[..^user.Length]) + @"[^\\/<>""\s]*~\d", home[..^user.Length] + "<user>", RegexOptions.IgnoreCase);
            return s.Replace(Environment.MachineName, "<pc>", StringComparison.OrdinalIgnoreCase);
        }
        public void Line(string s) => _log.Append(Clean(s)).Append('\n');
        public Cli.Result Run(string label, string exe, IReadOnlyList<string> args)
        {
            Line($"$ {label}"); Line($"  {Path.GetFileName(exe)} {string.Join(' ', args.Select(a => a.Contains(' ') ? $"\"{a}\"" : a))}");
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var r = Cli.Run(exe, args, null, 300000, null, Encoding.UTF8);
            Line($"Exit {r.Exit} nach {sw.Elapsed.TotalSeconds:0.0} s");
            if (r.Out.Trim().Length > 0) Line(r.Out.Trim().Length > 6000 ? r.Out.Trim()[..6000] + " …(gekürzt)" : r.Out.Trim());
            if (r.Err.Trim().Length > 0) Line("[stderr] " + r.Err.Trim());
            return r;
        }
        public string Keep(string file, string? name = null)
        {
            Assert.True(File.Exists(file), "Datei fehlt: " + file);
            var to = Path.Combine(Dir, name ?? Path.GetFileName(file));
            if (!string.Equals(Path.GetFullPath(to), Path.GetFullPath(file), StringComparison.OrdinalIgnoreCase)) File.Copy(file, to, true);
            Line($"Datei {Path.GetFileName(to)}: {new FileInfo(to).Length} Bytes, SHA-256 {Sha(to)}");
            return to;
        }
        public void Dispose() => File.WriteAllText(Path.Combine(Dir, _name + ".log"), _log.ToString(), new UTF8Encoding(false));
    }

    static Rezept Pfannkuchen() => new("r-pfannkuchen", "Pfannkuchen", 4,
        [.. new[] { "250 g Weizenmehl", "500 ml Milch", "3 Eier", "1 Prise Salz", "2 EL Butter, zum Ausbacken" }.Select(RezeptKit.LiesZutat)],
        ["Mehl, Milch, Eier und Salz zu einem glatten Teig verrühren.", "In heißer Butter von beiden Seiten goldbraun backen."]) { Beschreibung = "Dünne Pfannkuchen.", Vorbereitung = 15, Garzeit = 20, Kategorie = "Hauptgericht" };
    static Rezept Suppe() => new("r-suppe", "Linsensuppe", 4,
        [.. new[] { "250 g Linsen", "2 Karotten", "1 Zwiebel", "1 l Gemüsebrühe", "1 EL Olivenöl", "Salz und Pfeffer nach Geschmack" }.Select(RezeptKit.LiesZutat)],
        ["Zwiebel und Karotten würfeln und im Öl andünsten.", "Linsen und Brühe dazugeben und 30 Minuten köcheln."]) { Garzeit = 35, Kategorie = "Suppe" };

    [Fact(DisplayName = "T-0787 FREMD: Chrome öffnet die Rezeptseite und liest das eingebettete schema.org-JSON-LD (Recipe, Zutaten, Nährwerte); JSON-LD-Datei zurück in Trendo (nur mit TRENDO_T0787_FREMD)")]
    public void Chrome_liest_jsonld()
    {
        if (BelegRoot() is not { } root) { Ueberspringen.Weil("Nur mit TRENDO_T0787_FREMD=<Belegordner> (Chrome, LibreOffice, pdftotext, qpdf dieses PCs)."); return; }
        Need(Chrome, "Google Chrome");
        var work = Work("chrome");
        using var b = new Beleg(root, "chrome-jsonld");
        var html = Path.Combine(work, "rezepte.html"); File.WriteAllText(html, RezeptKit.Html([Pfannkuchen(), Suppe()]), new UTF8Encoding(false)); b.Keep(html, "trendo-rezepte.html");
        var json = Path.Combine(work, "rezepte.json"); File.WriteAllText(json, RezeptKit.JsonLd([Pfannkuchen(), Suppe()]), new UTF8Encoding(false)); b.Keep(json, "trendo-rezepte.json");
        var prof = Work("chrome-profil");
        var r = b.Run("Chrome (headless, eigenes Profil) öffnet die Rezeptseite mit #pruefen und gibt das DOM aus", Chrome,
            ["--headless=new", "--disable-gpu", "--no-first-run", "--no-default-browser-check", "--user-data-dir=" + prof, "--virtual-time-budget=8000", "--dump-dom", new Uri(html).AbsoluteUri + "#pruefen"]);
        Assert.True(r.Ok, r.Err);
        var body = Regex.Match(r.Out, @"<body[^>]*>");
        b.Line("body-Element laut Chrome: " + body.Value);
        Assert.Contains("data-ld-ok=\"ja\"", body.Value);
        Assert.Contains("data-ld-rezepte=\"2\"", body.Value);
        Assert.Contains($"data-ld-zutaten=\"{Pfannkuchen().Zutaten.Count + Suppe().Zutaten.Count}\"", body.Value); // 5 + 6
        var kcal = Regex.Match(body.Value, "data-ld-kcal=\"([^\"]*)\"").Groups[1].Value;
        var erwartet = string.Join("|", new[] { Pfannkuchen(), Suppe() }.Select(x => $"{Math.Round(RezeptKit.JePortion(x).Kcal)} kcal"));
        Assert.Equal(erwartet, kcal);
        Assert.Contains("<h1>Pfannkuchen</h1>", r.Out); Assert.Contains("Nährwerte je Portion (Richtwerte)", r.Out);
        b.Line($"Chrome liest im JSON-LD 2 Rezepte vom Typ Recipe mit 11 Zutaten, Energie je Portion {kcal} – gleich den Werten von Trendo.");
        var (back, notes) = RezeptKit.Import(File.ReadAllText(json));
        Assert.Empty(notes); Assert.Equal(2, back.Count);
        Assert.Equal(Pfannkuchen().Zutaten.Select(z => RezeptKit.Zeile(z)), back[0].Zutaten.Select(z => RezeptKit.Zeile(z)));
        b.Line("Trendo liest die JSON-LD-Datei wieder: 2 Rezepte, Zutaten gleich.");
        b.Line("ERGEBNIS: BESTANDEN");
        output.WriteLine("Chrome: " + body.Value);
    }

    [Fact(DisplayName = "T-0787 FREMD: LibreOffice öffnet Rezept als DOCX und ODT sowie die Einkaufsliste (CSV); pdftotext und qpdf lesen die PDFs von Trendo (nur mit TRENDO_T0787_FREMD)")]
    public void LibreOffice_und_pdf()
    {
        if (BelegRoot() is not { } root) { Ueberspringen.Weil("Nur mit TRENDO_T0787_FREMD=<Belegordner> (Chrome, LibreOffice, pdftotext, qpdf dieses PCs)."); return; }
        var lo = loKopie.Exe(Need(NeedsFactAttribute.Tool("libreoffice"), "LibreOffice (soffice)")); loKopie.BeginCase();
        var pdftotext = Need(PdfToText(), "pdftotext (Xpdf aus Git für Windows)");
        Need(Qpdf, "qpdf (TrendoWerkzeuge)");
        var work = Work("lo");
        using var b = new Beleg(root, "libreoffice-pdf");
        var r = Pfannkuchen();
        // DOCX und ODT → LibreOffice liest den Text
        foreach (var ext in new[] { "docx", "odt" })
        {
            var f = Path.Combine(work, "pfannkuchen." + ext);
            if (ext == "docx") DocxFormat.Write(RezeptKit.Druck(r), f); else OdtFormat.Write(RezeptKit.Druck(r), f);
            b.Keep(f, "trendo-pfannkuchen." + ext);
            var txt = LoConvert(b, lo, work, f, "txt:Text (encoded):UTF8", "txt");
            b.Keep(txt, "libreoffice-aus-" + ext + ".txt");
            var t = File.ReadAllText(txt, Encoding.UTF8);
            foreach (var s in new[] { "Pfannkuchen", "Zutaten", "250 g", "Weizenmehl", "3", "Eier", "Zubereitung", "Mehl, Milch, Eier und Salz", "Nährwerte je Portion (Richtwerte)", "Energie", "kcal", "FoodData Central" })
                Assert.True(t.Contains(s, StringComparison.Ordinal), $"LibreOffice liest „{s}“ nicht aus {ext}");
            b.Line($"LibreOffice-Text aus {ext}: alle 12 Prüfstellen gefunden");
        }
        // Einkaufsliste als CSV → LibreOffice Calc (Trennzeichen ;) → PDF
        var d = RezeptKit.Neu(); d["rezepte"] = RezeptKit.RezepteJson([r, Suppe()]);
        d["plan"] = RezeptKit.PlanJson(new DateTime(2026, 11, 30), [new("p1", 0, "Mittag", "r-suppe", 4), new("p2", 2, "Abend", "r-pfannkuchen", 8)]);
        var csv = Path.Combine(work, "einkaufsliste.csv"); File.WriteAllText(csv, RezeptKit.EinkaufCsv(d), new UTF8Encoding(false)); b.Keep(csv, "trendo-einkaufsliste.csv");
        var csvPdf = LoConvert(b, lo, work, csv, "pdf", "pdf", "CSV:59,34,76,1");
        b.Keep(csvPdf, "libreoffice-einkaufsliste.pdf");
        var ct = b.Run("pdftotext liest das Calc-PDF der Einkaufsliste", pdftotext, ["-enc", "UTF-8", csvPdf, "-"]);
        Assert.True(ct.Ok, ct.Err);
        foreach (var s in new[] { "Abteilung", "Weizenmehl", "500", "Linsen", "Milch, Käse & Eier" }) Assert.True(ct.Out.Contains(s, StringComparison.Ordinal), $"Calc-PDF ohne „{s}“");
        OriginalStatus(b);
        // PDFs von Trendo: pdftotext liest, qpdf prüft
        var pdfs = new[] { ("rezept", RezeptKit.Druck(r)), ("kochbuch", RezeptKit.Kochbuch([r, Suppe()], "Unser Kochbuch")), ("woche", RezeptKit.WochenDruck(d)) };
        foreach (var (name, doc) in pdfs)
        {
            var pdf = Path.Combine(work, name + ".pdf"); TextPdf.Export(doc, pdf); b.Keep(pdf, "trendo-" + name + ".pdf");
            var pt = b.Run($"pdftotext liest {name}.pdf", pdftotext, ["-enc", "UTF-8", pdf, "-"]);
            Assert.True(pt.Ok, pt.Err);
            var must = name switch { "rezept" => new[] { "Pfannkuchen", "250 g", "Richtwerte" }, "kochbuch" => ["Unser Kochbuch", "Inhalt", "Linsensuppe", "Pfannkuchen"], _ => ["Wochenplan", "Montag", "Einkaufsliste", "Linsen"] };
            foreach (var s in must) Assert.True(pt.Out.Contains(s, StringComparison.Ordinal), $"pdftotext findet „{s}“ nicht in {name}.pdf");
            var q = b.Run($"qpdf prüft den Aufbau von {name}.pdf", Qpdf, ["--check", pdf]);
            // qpdf meldet bei den Flate-Datenströmen von PDFsharp Warnungen (Exit 3 = Erfolg mit Warnungen), wie schon in T-0779 – keine Fehler.
            Assert.True(q.Exit == 0 || q.Exit == 3 && (q.Out + q.Err).Contains("operation succeeded with warnings"), "qpdf: " + q.Out + q.Err);
            b.Line(q.Exit == 0 ? $"→ qpdf {name}: keine Fehler" : $"→ qpdf {name}: lesbar, keine Fehler; Warnungen zu den Datenströmen (PDFsharp-Flate)");
        }
        b.Line("ERGEBNIS: BESTANDEN");
    }

    string LoConvert(Beleg b, string lo, string work, string file, string format, string ext, string? infilter = null)
    {
        var outDir = Path.Combine(work, "lo-aus-" + Path.GetExtension(file).TrimStart('.') + "-" + ext); Directory.CreateDirectory(outDir);
        var args = new List<string> { "-env:UserInstallation=" + new Uri(Path.Combine(work, "lo-profil")).AbsoluteUri, "--headless", "--norestore", "--nolockcheck" };
        if (infilter is not null) args.Add("--infilter=" + infilter);
        args.AddRange(["--convert-to", format, "--outdir", outDir, file]);
        b.Line($"$ LibreOffice öffnet {Path.GetFileName(file)} und schreibt {ext}");
        b.Line($"  {Path.GetFileName(lo)} {string.Join(' ', args.Select(a => a.Contains(' ') ? $"\"{a}\"" : a))}");
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var run = loKopie.Run(lo, args); var r = run.Result;
        b.Line($"Exit {r.Exit} nach {sw.Elapsed.TotalSeconds:0.0} s");
        if (r.Out.Trim().Length > 0) b.Line(r.Out.Trim());
        if (r.Err.Trim().Length > 0) b.Line("[stderr] " + r.Err.Trim());
        b.Line($"  eigene Prozesse ({run.Own.Count}): {string.Join("; ", run.Own)}");
        if (run.Foreign.Count > 0) b.Line($"  HINWEIS fremde soffice aus dem Original: {string.Join("; ", run.Foreign)}");
        Assert.True(run.Own.Count >= 1, "Kein eigener LibreOffice-Prozess beobachtet.");
        Assert.True(run.OwnOutside.Count == 0, "Ein eigener LibreOffice-Prozess lief nicht aus der Kopie: " + string.Join("; ", run.OwnOutside));
        Assert.True(r.Ok, $"LibreOffice Exit {r.Exit}: {r.Err}");
        var target = Path.Combine(outDir, Path.GetFileNameWithoutExtension(file) + "." + ext);
        Assert.True(File.Exists(target), "LibreOffice hat nichts geschrieben: " + target);
        return target;
    }

    void OriginalStatus(Beleg b)
    {
        var (status, bericht) = loKopie.EndCase();
        b.Line(loKopie.Note); b.Line($"Original: {status}. {bericht}");
        output.WriteLine($"Original im Trendo-Werkzeugordner: {status}. {bericht}");
    }
}
