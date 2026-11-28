using System.Security.Cryptography;
using System.Text;
using Trendo.Core.Kreativ;
using Trendo.Core.Office;
using Trendo.Core.Wissen;
using Xunit;
using Xunit.Abstractions;

namespace Trendo.Core.Tests;

/// <summary>T-0788: Fremdprogramm-Nachlauf des Studios „Stammbaum“ mit Werkzeugen, die auf diesem PC schon liegen (nichts installiert, nichts ersetzt).
/// Eingang: GEDCOM in der Form verbreiteter Genealogie-Programme (<see cref="T0788Fixtures"/>). Trendo schreibt mit seinem eigenen Exportcode;
/// LibreOffice (nur aus der Kopie von <see cref="T0776_LoKopie"/>, eigenes Profil) öffnet den Bericht (DOCX, ODT) und die Personenliste (CSV),
/// Inkscape (eigenes Profil) den Baum als SVG, pdftotext liest Trendos PDFs. Ein Genealogie-Programm liegt auf diesem PC nicht – der GEDCOM-Export
/// ist nur mit Trendos eigenem Leser und den Regeln des Formats geprüft (für ein Fremdprogramm BLOCKIERT).
/// Rohlog und SHA-256 je Fall im Ordner aus <c>TRENDO_BELEG_DIR</c> (sonst Temp) – abgelegt unter <c>reports/T-0788/</c>.</summary>
public class T0788_Fremdnachlauf(T0776_LoKopie loKopie, ITestOutputHelper output) : IClassFixture<T0776_LoKopie>, IDisposable
{
    /// <summary>Gegenprüfung: eigener Arbeitsordner je Testlauf, am Ende wieder entfernt – ebenso der Belegordner, wenn kein TRENDO_BELEG_DIR gesetzt ist.</summary>
    readonly string _work = Path.Combine(TempDir.SystemTemp, "trendo-t0788-work", Guid.NewGuid().ToString("N")[..8]);
    static readonly string DefaultBeleg = Path.Combine(TempDir.SystemTemp, "trendo-t0788-beleg");
    public void Dispose()
    {
        foreach (var d in new[] { _work, Path.GetDirectoryName(_work)! }.Concat(Environment.GetEnvironmentVariable("TRENDO_BELEG_DIR") is { Length: > 0 } ? [] : new[] { DefaultBeleg }))
            try { if (Directory.Exists(d) && (d != Path.GetDirectoryName(_work) || !Directory.EnumerateFileSystemEntries(d).Any())) Directory.Delete(d, true); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
    }

    static string Need(string? path, string was) { if (path is null) Ueberspringen.Weil("BLOCKIERT – fehlt auf diesem PC: " + was); return path; }
    static string? Konsole(string? exe) => exe is null ? null : Path.ChangeExtension(exe, ".com") is var c && File.Exists(c) ? c : exe;
    string Work(string name) { var d = Path.Combine(_work, name + "-" + Guid.NewGuid().ToString("N")[..6]); Directory.CreateDirectory(d); return d; }
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
            Dir = Path.Combine(Environment.GetEnvironmentVariable("TRENDO_BELEG_DIR") is { Length: > 0 } d ? d : DefaultBeleg, name);
            Directory.CreateDirectory(Dir);
            Line($"# T-0788 Fremdnachlauf {name} – {DateTime.Now:yyyy-MM-dd HH:mm:ss zzz}");
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

    static FamilyTree Foreign(Beleg b, string work)
    {
        var ged = Path.Combine(work, "fremd.ged"); File.WriteAllBytes(ged, T0788Fixtures.Bytes); b.Keep(ged, "fremd-eingang.ged");
        var t = FamilyKit.FromGedcom(File.ReadAllBytes(ged)); t.HideLiving = false; t.Title = "Familie Größer"; t.Root = "I3";
        b.Line($"GEDCOM gelesen: {t.Persons.Count} Personen, {t.Families.Count} Familien, {t.Sources.Count} Quellen – {t.ImportNote}");
        var back = Path.Combine(work, "trendo.ged"); File.WriteAllText(back, FamilyKit.ToGedcom(t, false, T0788Fixtures.Now), new UTF8Encoding(false)); b.Keep(back, "trendo-ausgang.ged");
        var again = FamilyKit.FromGedcom(File.ReadAllBytes(back));
        b.Line($"Rundreise (Trendo liest seinen eigenen Export): Inhalt gleich = {FamilyKit.Content(t) == FamilyKit.Content(again)}");
        Assert.Equal(FamilyKit.Content(t), FamilyKit.Content(again));
        return t;
    }

    [Fact(DisplayName = "T-0788 echt: Stammbaum-Bericht – GEDCOM eingelesen, Trendo schreibt DOCX und ODT; LibreOffice öffnet beide (Personen, Umlaute, Quellen, Prüfung) und setzt ein PDF")]
    public void Bericht_in_LibreOffice()
    {
        var lo = loKopie.Exe(Need(NeedsFactAttribute.Tool("libreoffice"), "LibreOffice (soffice)")); loKopie.BeginCase();
        var pdftotext = PdfToText();
        var work = Work("bericht");
        using var b = new Beleg("libreoffice-stammbaum-bericht");
        var t = Foreign(b, work);
        var doc = FamilyKit.BuildReport(t, T0788Fixtures.Now);
        foreach (var ext in new[] { "docx", "odt" })
        {
            var f = Path.Combine(work, "bericht." + ext);
            if (ext == "docx") DocxFormat.Write(doc, f); else OdtFormat.Write(doc, f);
            b.Keep(f, "trendo-bericht." + ext);
            var txt = LoConvert(b, lo, work, f, "txt:Text (encoded):UTF8", "txt");
            b.Keep(txt, "libreoffice-aus-" + ext + ".txt");
            var text = File.ReadAllText(txt, Encoding.UTF8);
            foreach (var s in new[] { "Familie Größer", "Jürgen Größer (männlich)", "Geburt: 12. März 1850 in Köln, Nordrhein-Westfalen, Deutschland", "Taufbuch 1850, Nr. 34",
                                       "Partner: Anna Muster, Heirat 5. Mai 1878 in Köln; Kinder: Erika Größer", "Prüfung", "Keine Widersprüche gefunden.", "Kirchenbuch St. Beispiel 1840–1860", "Düsseldorf" })
                Assert.True(text.Contains(s, StringComparison.Ordinal), $"LibreOffice liest „{s}“ nicht aus {ext}");
            b.Line($"LibreOffice-Text aus {ext}: alle 9 Prüfstellen gefunden");
            var pdf = LoConvert(b, lo, work, f, "pdf", "pdf");
            b.Keep(pdf, "libreoffice-aus-" + ext + ".pdf"); b.Line($"LibreOffice-PDF aus {ext}: {PdfPages(pdf)} Seite(n)");
            Assert.True(PdfPages(pdf) >= 1);
        }
        if (pdftotext is not null)
        {
            var pdf = Path.Combine(work, "bericht.pdf"); TextPdf.Export(doc, pdf); b.Keep(pdf, "trendo-bericht.pdf");
            var r = b.Run("pdftotext liest Trendos Bericht-PDF", pdftotext, ["-enc", "UTF-8", pdf, "-"]);
            Assert.True(r.Ok, r.Err);
            foreach (var s in new[] { "Jürgen Größer", "Anna Muster", "Erika Größer", "Prüfung" }) Assert.True(r.Out.Contains(s, StringComparison.Ordinal), $"PDF ohne „{s}“");
        }
        else b.Line("pdftotext fehlt – Trendos PDF nicht mit einem Fremdwerkzeug gelesen");
        OriginalStatus(b);
        b.Line("ERGEBNIS: BESTANDEN");
    }

    [Fact(DisplayName = "T-0788 echt: Stammbaum als SVG – Inkscape liest alle Texte (mit Umlauten), speichert einfaches SVG und rendert PNG; pdftotext liest das Baum-PDF; LibreOffice Calc liest die Personenliste")]
    public void Baum_in_Inkscape_und_Calc()
    {
        var ink = Konsole(Need(NeedsFactAttribute.Tool("inkscape"), "Inkscape"));
        var pdftotext = Need(PdfToText(), "pdftotext (Xpdf, z. B. aus Git für Windows)");
        var work = Work("baum");
        using var b = new Beleg("inkscape-stammbaum");
        var t = Foreign(b, work);
        var scene = FamilyKit.Ancestors(t, "I3", 3, paper: true);
        var svg = Path.Combine(work, "stammbaum.svg"); File.WriteAllText(svg, TimelineKit.ToSvg(scene, "Stammbaum"), new UTF8Encoding(false)); b.Keep(svg, "trendo-stammbaum.svg");
        var p = Path.Combine(work, "inkscape-profil"); foreach (var s in new[] { "AppData/Roaming", "AppData/Local" }) Directory.CreateDirectory(Path.Combine(p, s));
        var env = new Dictionary<string, string> { ["APPDATA"] = Path.Combine(p, "AppData", "Roaming"), ["LOCALAPPDATA"] = Path.Combine(p, "AppData", "Local"), ["INKSCAPE_PROFILE_DIR"] = Path.Combine(p, "profil"), ["XDG_CONFIG_HOME"] = Path.Combine(p, "config"), ["XDG_CACHE_HOME"] = Path.Combine(p, "cache"), ["XDG_DATA_HOME"] = Path.Combine(p, "data"), ["USERPROFILE"] = p, ["HOME"] = p };
        var q = b.Run("Inkscape liest stammbaum.svg (alle Objekte)", ink, ["--query-all", svg], env);
        Assert.True(q.Ok, $"Inkscape Exit {q.Exit}: {q.Err}");
        var lines = q.Out.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Where(l => l.Split(',').Length == 5).ToList();
        var texts = lines.Count(l => l.StartsWith("text", StringComparison.Ordinal));
        b.Line($"Inkscape: {lines.Count} Objekte, davon {texts} Texte (Trendo: {scene.Prims.OfType<TimelineKit.PText>().Count()})");
        Assert.Equal(scene.Prims.OfType<TimelineKit.PText>().Count(), texts);
        var plain = Path.Combine(work, "inkscape-plain.svg");
        var x = b.Run("Inkscape speichert als einfaches SVG", ink, [svg, "--export-type=svg", "--export-plain-svg", "--export-filename=" + plain], env);
        Assert.True(x.Ok && File.Exists(plain), x.Err);
        var plainText = File.ReadAllText(plain);
        foreach (var s in new[] { "Jürgen Größer", "Anna Muster", "Erika Größer", "Familie Größer" }) Assert.True(plainText.Contains(s, StringComparison.Ordinal), $"Inkscape-SVG ohne „{s}“");
        b.Keep(plain, "inkscape-plain.svg");
        var png = Path.Combine(work, "inkscape-stammbaum.png");
        var r = b.Run("Inkscape rendert den Baum als PNG", ink, [svg, "--export-type=png", "--export-filename=" + png, "--export-width=1200"], env);
        Assert.True(r.Ok && File.Exists(png), $"Inkscape PNG fehlt (Exit {r.Exit}): {r.Err}");
        var pm = PngCodec.Decode(File.ReadAllBytes(png)); b.Keep(png); b.Line($"Inkscape-PNG {pm.W}×{pm.H}");
        Assert.Equal(1200, pm.W);
        var pdf = Path.Combine(work, "stammbaum.pdf"); TimelineKit.ToPdf(scene, pdf, "Stammbaum"); b.Keep(pdf, "trendo-stammbaum.pdf");
        var pt = b.Run("pdftotext liest Trendos Baum-PDF", pdftotext, ["-enc", "UTF-8", pdf, "-"]);
        Assert.True(pt.Ok, pt.Err);
        foreach (var s in new[] { "Familie Größer", "Jürgen Größer", "Anna Muster", "Erika Größer" }) Assert.True(pt.Out.Contains(s, StringComparison.Ordinal), $"PDF ohne „{s}“");

        var lo = NeedsFactAttribute.Tool("libreoffice");
        if (lo is not null)
        {
            var calc = loKopie.Exe(lo); loKopie.BeginCase();
            var csv = Path.Combine(work, "personen.csv"); File.WriteAllText(csv, FamilyKit.ToCsv(t, T0788Fixtures.Now), new UTF8Encoding(true)); b.Keep(csv, "trendo-personen.csv");
            var ods = LoConvert(b, calc, work, csv, "ods", "ods", "CSV:59,34,76,1");
            var back = LoConvert(b, calc, work, ods, "csv:Text - txt - csv (StarCalc):59,34,76,1", "csv");
            var rows = File.ReadAllLines(back, Encoding.UTF8).Select(l => l.Replace("\"", "")).ToArray();
            b.Keep(back, "libreoffice-csv-zurueck.csv");
            b.Line($"LibreOffice Calc: {rows.Length} Zeilen, Kopf „{rows[0]}“");
            Assert.Equal(t.Persons.Count + 1, rows.Length);
            Assert.StartsWith("Kennung;Vorname;Nachname;Geschlecht;Geburt", rows[0]);
            Assert.Contains(rows, l => l.StartsWith("I1;Jürgen;Größer;männlich;", StringComparison.Ordinal) && l.Contains(";Köln, Nordrhein-Westfalen, Deutschland;um 1920;Düsseldorf;", StringComparison.Ordinal)); // Calc macht aus „12. März 1850“ eine Datumszelle (12.03.50)
            OriginalStatus(b);
        }
        b.Line("ERGEBNIS: BESTANDEN");
    }
}
