using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Trendo.Core.Kreativ;
using Trendo.Core.Office;
using Trendo.Core.Wissen;
using Xunit;
using Xunit.Abstractions;

namespace Trendo.Core.Tests;

/// <summary>T-0780: Fremdprogramm-Nachlauf der beiden neuen Studios mit Werkzeugen, die auf diesem PC schon liegen (nichts installiert, nichts ersetzt).
/// Fremde Eingangsdateien: RIS in Zotero-Form (Referate) und eine Tabelle im Format von TimelineJS (Zeitleisten). Trendo schreibt mit seinem eigenen Exportcode;
/// LibreOffice (nur aus der Kopie von <see cref="T0776_LoKopie"/>, eigenes Profil) öffnet DOCX, ODT, PPTX und ODP, Inkscape (eigenes Profil) die SVG,
/// pdftotext (Xpdf aus Git für Windows) liest die PDFs. Fehlt ein Werkzeug: „übersprungen“ mit Grund (BLOCKIERT).
/// Rohlog und SHA-256 je Fall im Ordner aus <c>TRENDO_BELEG_DIR</c> (sonst Temp) – abgelegt unter <c>reports/T-0780/</c>.</summary>
public class T0780_Fremdnachlauf(T0776_LoKopie loKopie, ITestOutputHelper output) : IClassFixture<T0776_LoKopie>
{
    static string Need(string? path, string was) { if (path is null) Ueberspringen.Weil("BLOCKIERT – fehlt auf diesem PC: " + was); return path; }
    static string? Konsole(string? exe) => exe is null ? null : Path.ChangeExtension(exe, ".com") is var c && File.Exists(c) ? c : exe;
    static string Work(string name) { var d = Path.Combine(TempDir.SystemTemp, "trendo-t0780-work", name + "-" + Guid.NewGuid().ToString("N")[..6]); Directory.CreateDirectory(d); return d; }
    static string Sha(string f) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(f))).ToLowerInvariant();
    static string? PdfToText() => Cli.Find("pdftotext") ?? new[] { Environment.GetEnvironmentVariable("ProgramW6432"), Environment.GetEnvironmentVariable("ProgramFiles") }
        .Where(p => p is { Length: > 0 }).Select(p => Path.Combine(p!, "Git", "mingw64", "bin", "pdftotext.exe")).FirstOrDefault(File.Exists);

    sealed class Beleg : IDisposable
    {
        readonly StringBuilder _log = new(); readonly string _name;
        public string Dir { get; }
        public Beleg(string name)
        {
            _name = name;
            Dir = Path.Combine(Environment.GetEnvironmentVariable("TRENDO_BELEG_DIR") is { Length: > 0 } d ? d : Path.Combine(TempDir.SystemTemp, "trendo-t0780-beleg"), name);
            Directory.CreateDirectory(Dir);
            Line($"# T-0780 Fremdnachlauf {name} – {DateTime.Now:yyyy-MM-dd HH:mm:ss zzz}");
        }
        public static string Clean(string s)
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile).TrimEnd('\\', '/');
            if (home.Length < 4) return s;
            var user = Path.GetFileName(home);
            foreach (var form in new[] { home, home.Replace('\\', '/'), Uri.EscapeDataString(home), home.Replace('\\', '/').Replace(" ", "%20") })
                s = s.Replace(form, form[..^user.Length] + "<user>", StringComparison.OrdinalIgnoreCase);
            s = System.Text.RegularExpressions.Regex.Replace(s, System.Text.RegularExpressions.Regex.Escape(home[..^user.Length]) + @"[^\\/<>""\s]*~\d", home[..^user.Length] + "<user>", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            return s.Replace(Environment.MachineName, "<pc>", StringComparison.OrdinalIgnoreCase);
        }
        public void Line(string s) => _log.Append(Clean(s)).Append('\n');
        public Cli.Result Run(string label, string exe, IReadOnlyList<string> args, IDictionary<string, string>? env = null)
        {
            Line($"$ {label}"); Line($"  {Path.GetFileName(exe)} {string.Join(' ', args.Select(a => a.Contains(' ') ? $"\"{a}\"" : a))}");
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var r = Cli.Run(exe, args, null, 300000, env, Encoding.UTF8);
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

    static int PdfPages(string pdf) => System.Text.RegularExpressions.Regex.Matches(Encoding.Latin1.GetString(File.ReadAllBytes(pdf)), @"/Type\s*/Page[^s]").Count;

    /// <summary>Fremde Quellen wie aus Zotero exportiert (RIS, CRLF, Zeilen mit „A1/T1/PY“).</summary>
    const string ZoteroRis = "TY  - BOOK\r\nA1  - Muster, Erika\r\nA1  - Beispiel, Max\r\nT1  - Gärten für Einsteiger\r\nPY  - 2024\r\nPB  - Grünverlag\r\nCY  - Musterstadt\r\nER  - \r\n\r\n"
                           + "TY  - JOUR\r\nAU  - Muster, Erika\r\nTI  - Boden und Klima\r\nT2  - Zeitschrift für Gartenbau\r\nPY  - 2023\r\nVL  - 12\r\nIS  - 3\r\nSP  - 45\r\nEP  - 67\r\nER  - \r\n";

    static JsonObject Referat(List<CiteKit.Source> sources)
    {
        var buch = sources.First(s => s.Kind == "buch").Id; var art = sources.First(s => s.Kind == "artikel").Id;
        return new JsonObject
        {
            ["title"] = "Wasser sparen im eigenen Garten", ["subtitle"] = "Möglichkeiten und Grenzen", ["kind"] = "hausarbeit", ["author"] = "Erika Muster", ["school"] = "Musterhochschule", ["course"] = "Seminar Gartenbau",
            ["teacher"] = "Prof. Max Beispiel", ["place"] = "Musterstadt", ["date"] = "26. November 2026", ["style"] = "din", ["theme"] = "sonne",
            ["sources"] = new JsonArray([.. sources.Select(s => (JsonNode)ReferatKit.SourceJson(s))]),
            ["sections"] = ReferatKit.SectionsJson(
            [
                new("s1", 1, "Einleitung", $"Gärten brauchen im Sommer viel Wasser [@{buch}, S. 12].\n\nDiese Arbeit zeigt Wege zum Sparen."),
                new("s2", 1, "Wege zum Wassersparen", ""),
                new("s3", 2, "Regenwasser sammeln", "Eine Regentonne fängt das Wasser vom Dach auf."),
                new("s4", 2, "Boden verbessern", $"Humusreicher Boden hält Wasser länger [@{art}, S. 50]."),
                new("s5", 1, "Fazit", "Mit wenigen Mitteln lässt sich viel Wasser sparen."),
            ]),
        };
    }

    [Fact(DisplayName = "T-0780 echt: Referate – RIS aus Zotero eingelesen, Trendo schreibt DOCX und ODT; LibreOffice öffnet beide (Kapitel, Kurzbelege, Literaturverzeichnis) und setzt ein PDF mit Seitenzahlen")]
    public void Referat_docx_odt_in_LibreOffice()
    {
        var lo = loKopie.Exe(Need(NeedsFactAttribute.Tool("libreoffice"), "LibreOffice (soffice)")); loKopie.BeginCase();
        var pdftotext = PdfToText();
        var work = Work("referat");
        using var b = new Beleg("libreoffice-referat");
        var ris = Path.Combine(work, "zotero-export.ris"); File.WriteAllText(ris, ZoteroRis, new UTF8Encoding(false)); b.Keep(ris, "fremd-zotero-export.ris");
        var (sources, warn) = CiteKit.ParseRis(File.ReadAllText(ris));
        Assert.Empty(warn); Assert.Equal(2, sources.Count);
        b.Line($"RIS gelesen: {sources.Count} Quellen ({string.Join(", ", sources.Select(s => s.Title))})");
        var data = Referat(sources);
        var doc = ReferatKit.BuildText(data);
        foreach (var ext in new[] { "docx", "odt" })
        {
            var f = Path.Combine(work, "hausarbeit." + ext);
            if (ext == "docx") DocxFormat.Write(doc, f); else OdtFormat.Write(doc, f);
            b.Keep(f, "trendo-hausarbeit." + ext);
            var txt = LoConvert(b, lo, work, f, "txt:Text (encoded):UTF8", "txt");
            b.Keep(txt, "libreoffice-aus-" + ext + ".txt");
            var t = File.ReadAllText(txt, Encoding.UTF8);
            foreach (var s in new[] { "Wasser sparen im eigenen Garten", "vorgelegt von Erika Muster", "Inhaltsverzeichnis", "1 Einleitung", "2.1 Regenwasser sammeln", "3 Fazit",
                                       "(MUSTER und BEISPIEL 2024, S. 12)", "(MUSTER 2023, S. 50)", "Literaturverzeichnis", "Gärten für Einsteiger", "Boden und Klima" })
                Assert.True(t.Contains(s, StringComparison.Ordinal), $"LibreOffice liest „{s}“ nicht aus {ext}");
            b.Line($"LibreOffice-Text aus {ext}: alle 11 Prüfstellen gefunden");
            var pdf = LoConvert(b, lo, work, f, "pdf", "pdf");
            var pages = PdfPages(pdf);
            b.Keep(pdf, "libreoffice-aus-" + ext + ".pdf"); b.Line($"LibreOffice-PDF aus {ext}: {pages} Seiten");
            Assert.True(pages >= 4, $"Deckblatt, Verzeichnis, Text und Literatur: nur {pages} Seiten");
            if (pdftotext is not null)
            {
                var r = b.Run($"pdftotext liest das LibreOffice-PDF aus {ext}", pdftotext, ["-enc", "UTF-8", pdf, "-"]);
                Assert.True(r.Ok, r.Err);
                Assert.Contains($"Seite 2 von {pages}", r.Out);
            }
        }
        OriginalStatus(b);
        b.Line("ERGEBNIS: BESTANDEN");
    }

    [Fact(DisplayName = "T-0780 echt: Referate-Folien – Trendo schreibt PPTX und ODP; LibreOffice Impress öffnet beide, jede Folie wird eine PDF-Seite mit den Folientiteln")]
    public void Folien_pptx_odp_in_LibreOffice()
    {
        var lo = loKopie.Exe(Need(NeedsFactAttribute.Tool("libreoffice"), "LibreOffice (soffice)")); loKopie.BeginCase();
        var pdftotext = Need(PdfToText(), "pdftotext (Xpdf, z. B. aus Git für Windows)");
        var work = Work("folien");
        using var b = new Beleg("libreoffice-folien");
        var (sources, _) = CiteKit.ParseRis(ZoteroRis);
        var deck = ReferatKit.BuildSlides(Referat(sources));
        b.Line($"Trendo-Folien: {deck.Slides.Count} – {string.Join(" | ", deck.Slides.Select(s => s.Title))}");
        foreach (var ext in new[] { "pptx", "odp" })
        {
            var f = Path.Combine(work, "vortrag." + ext);
            if (ext == "pptx") SlideFiles.WritePptx(deck, f); else SlideFiles.WriteOdp(deck, f);
            b.Keep(f, "trendo-vortrag." + ext);
            var pdf = LoConvert(b, lo, work, f, "pdf", "pdf");
            b.Keep(pdf, "libreoffice-aus-" + ext + ".pdf");
            var pages = PdfPages(pdf);
            b.Line($"LibreOffice-PDF aus {ext}: {pages} Seiten");
            Assert.Equal(deck.Slides.Count, pages);
            var r = b.Run($"pdftotext liest das Impress-PDF aus {ext}", pdftotext, ["-enc", "UTF-8", pdf, "-"]);
            Assert.True(r.Ok, r.Err);
            foreach (var s in deck.Slides) Assert.True(r.Out.Contains(s.Title, StringComparison.Ordinal), $"Folientitel „{s.Title}“ fehlt im PDF aus {ext}");
            Assert.Contains("Regenwasser sammeln", r.Out); // Punkte aus den Unterabschnitten
            // Rundreise: Impress speichert als PPTX bzw. ODP, Trendo liest es wieder
            var back = LoConvert(b, lo, work, f, ext == "pptx" ? "odp" : "pptx", ext == "pptx" ? "odp" : "pptx");
            b.Keep(back, $"libreoffice-{ext}-als-{Path.GetExtension(back).TrimStart('.')}{Path.GetExtension(back)}");
            var reread = SlideFiles.Open(back);
            b.Line($"Trendo liest die Impress-Fassung ({Path.GetExtension(back)}): {reread.Slides.Count} Folien – {string.Join(" | ", reread.Slides.Select(s => s.Title))}");
            Assert.Equal(deck.Slides.Select(s => s.Title), reread.Slides.Select(s => s.Title));
            Assert.Contains("Diese Arbeit zeigt Wege zum Sparen.", reread.Slides.First(s => s.Title == "1 Einleitung").Notes);
        }
        OriginalStatus(b);
        b.Line("ERGEBNIS: BESTANDEN");
    }

    /// <summary>Tabelle im Format der TimelineJS-Vorlage (öffentliche Spaltennamen), wie sie aus einer Tabellenkalkulation als CSV kommt.</summary>
    const string TimelineJsCsv = "Year,Month,Day,Time,End Year,End Month,End Day,End Time,Display Date,Headline,Text,Media,Media Credit,Media Caption,Media Thumbnail,Type,Group,Background\r\n"
                               + ",,,,,,,,,Antike Welt,Überblick für den Unterricht,,,,,title,,\r\n"
                               + "-753,,,,,,,,,Gründung Roms,Sagenhaftes Datum,,,,,,Politik,\r\n"
                               + "-44,3,15,,,,,,,Tod Caesars,Iden des März,,,,,,Politik,\r\n"
                               + "-27,,,,476,,,,,Römisches Kaiserreich,,,,,,,Politik,\r\n"
                               + "-490,,,,,,,,,Schlacht bei Marathon,,,,,,,Krieg,\r\n"
                               + "-447,,,,-432,,,,,Bau des Parthenon,,,,,,,Kultur,\r\n";

    [Fact(DisplayName = "T-0780 echt: Zeitleisten – TimelineJS-Tabelle eingelesen, Trendo schreibt SVG und PDF; Inkscape öffnet die SVG (Texte, PNG-Bild), pdftotext liest das PDF, LibreOffice liest die CSV")]
    public void Zeitleiste_svg_in_Inkscape()
    {
        var ink = Konsole(Need(NeedsFactAttribute.Tool("inkscape"), "Inkscape"));
        var pdftotext = Need(PdfToText(), "pdftotext (Xpdf, z. B. aus Git für Windows)");
        var work = Work("zeitleiste");
        using var b = new Beleg("inkscape-zeitleiste");
        var src = Path.Combine(work, "timelinejs.csv"); File.WriteAllText(src, TimelineJsCsv, new UTF8Encoding(false)); b.Keep(src, "fremd-timelinejs.csv");
        var (t, probs) = TimelineKit.FromCsv(File.ReadAllText(src));
        Assert.Empty(probs); Assert.Equal(5, t.Items.Count);
        b.Line($"TimelineJS-Tabelle gelesen: „{t.Title}“, {t.Items.Count} Einträge, Spuren {string.Join(", ", t.Tracks.Select(x => x.Name))}");
        var (f, to) = TimelineKit.Fit(t);
        var scene = TimelineKit.Layout(t, f, to, 1400, paper: true);
        var svg = Path.Combine(work, "zeitleiste.svg"); File.WriteAllText(svg, TimelineKit.ToSvg(scene), new UTF8Encoding(false)); b.Keep(svg, "trendo-zeitleiste.svg");
        var p = Path.Combine(work, "inkscape-profil"); foreach (var s in new[] { "AppData/Roaming", "AppData/Local" }) Directory.CreateDirectory(Path.Combine(p, s));
        var env = new Dictionary<string, string> { ["APPDATA"] = Path.Combine(p, "AppData", "Roaming"), ["LOCALAPPDATA"] = Path.Combine(p, "AppData", "Local"), ["INKSCAPE_PROFILE_DIR"] = Path.Combine(p, "profil"), ["XDG_CONFIG_HOME"] = Path.Combine(p, "config"), ["XDG_CACHE_HOME"] = Path.Combine(p, "cache"), ["XDG_DATA_HOME"] = Path.Combine(p, "data"), ["USERPROFILE"] = p, ["HOME"] = p };
        var q = b.Run("Inkscape liest zeitleiste.svg (alle Objekte)", ink, ["--query-all", svg], env);
        Assert.True(q.Ok, $"Inkscape Exit {q.Exit}: {q.Err}");
        var lines = q.Out.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Where(l => l.Split(',').Length == 5).ToList();
        var texts = lines.Count(l => l.StartsWith("text", StringComparison.Ordinal));
        b.Line($"Inkscape: {lines.Count} Objekte, davon {texts} Texte (Trendo: {scene.Prims.OfType<TimelineKit.PText>().Count()})");
        Assert.Equal(scene.Prims.OfType<TimelineKit.PText>().Count(), texts);
        var plain = Path.Combine(work, "inkscape-text.svg");
        var x = b.Run("Inkscape speichert als einfaches SVG (Texte bleiben Text)", ink, [svg, "--export-type=svg", "--export-plain-svg", "--export-filename=" + plain], env);
        Assert.True(x.Ok && File.Exists(plain), x.Err);
        var plainText = File.ReadAllText(plain);
        foreach (var s in new[] { "Gründung Roms", "Tod Caesars", "Römisches Kaiserreich", "753 v. Chr.", "15.03.44 v. Chr." }) Assert.True(plainText.Contains(s, StringComparison.Ordinal), $"Inkscape-SVG ohne „{s}“");
        b.Keep(plain, "inkscape-plain.svg");
        var png = Path.Combine(work, "inkscape-zeitleiste.png");
        var r = b.Run("Inkscape rendert die Zeitleiste als PNG", ink, [svg, "--export-type=png", "--export-filename=" + png, "--export-width=1400"], env);
        Assert.True(r.Ok && File.Exists(png), $"Inkscape PNG fehlt (Exit {r.Exit}): {r.Err}");
        var pm = PngCodec.Decode(File.ReadAllBytes(png));
        b.Keep(png); b.Line($"Inkscape-PNG {pm.W}×{pm.H}");
        Assert.Equal(1400, pm.W);
        var pdf = Path.Combine(work, "zeitleiste.pdf"); TimelineKit.ToPdf(scene, pdf); b.Keep(pdf, "trendo-zeitleiste.pdf");
        var pt = b.Run("pdftotext liest Trendos Zeitleisten-PDF", pdftotext, ["-enc", "UTF-8", pdf, "-"]);
        Assert.True(pt.Ok, pt.Err);
        foreach (var s in new[] { "Antike Welt", "Gründung Roms", "Schlacht bei Marathon", "Bau des Parthenon", "15.03.44 v. Chr.", "753 v. Chr." }) Assert.True(pt.Out.Contains(s, StringComparison.Ordinal), $"PDF ohne „{s}“");

        // CSV-Ausgabe in LibreOffice Calc
        var lo = NeedsFactAttribute.Tool("libreoffice");
        if (lo is not null)
        {
            var calc = loKopie.Exe(lo); loKopie.BeginCase();
            var csv = Path.Combine(work, "zeitleiste.csv"); File.WriteAllText(csv, TimelineKit.ToCsv(t), new UTF8Encoding(true)); b.Keep(csv, "trendo-zeitleiste.csv");
            var ods = LoConvert(b, calc, work, csv, "ods", "ods", "CSV:59,34,76,1");
            var back = LoConvert(b, calc, work, ods, "csv:Text - txt - csv (StarCalc):59,34,76,1", "csv");
            var rows = File.ReadAllLines(back, Encoding.UTF8).Select(l => l.Replace("\"", "")).ToArray(); // Calc setzt Text in Anführungszeichen
            b.Keep(back, "libreoffice-csv-zurueck.csv");
            b.Line($"LibreOffice Calc: {rows.Length} Zeilen, Kopf „{rows[0]}“");
            Assert.Equal(t.Items.Count + 1, rows.Length);
            Assert.StartsWith("Art;Titel;Beginn;Ende;Spur", rows[0]);
            Assert.Contains(rows, l => l.StartsWith("Ereignis;Tod Caesars;15. März 44 v. Chr.;;Politik", StringComparison.Ordinal));
            OriginalStatus(b);
        }
        b.Line("ERGEBNIS: BESTANDEN");
    }
}
