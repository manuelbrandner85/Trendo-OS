using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using Trendo.Core.Arch;
using Trendo.Core.Business;
using Trendo.Core.Cad;
using Trendo.Core.Kreativ;
using Trendo.Core.Office;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>T-0776 (Restliste Q3): Fremdprogramm-Nachläufe mit Werkzeugen, die auf dem PC schon da sind. Trendo schreibt echte Dateien mit seinem eigenen
/// Exportcode; das Fremdprogramm öffnet sie per Kommandozeile und liest sie zurück: LibreOffice (Text DOCX/ODT, Geld-XLSX, Shop-CSV), FreeCAD (CAD-STEP,
/// CAD-DXF, Wohnungs-DXF), Krita (PSD, ORA), Inkscape (Logo-SVG, Animations-SVG), 7-Zip (Sicherungs-ZIP, Erweiterungs-ZIP).
/// Nichts ist ersetzt. Jedes Fremdprogramm läuft mit eigenem Profil im eigenen Temp-Ordner (Umgebung <c>T0776_WORK</c>, sonst Temp).
/// Fehlt ein Werkzeug, meldet der Test „übersprungen“ (BLOCKIERT) – er besteht dann nicht still.
/// Rohlog und SHA-256 je Fall: Ordner aus <c>TRENDO_BELEG_DIR</c> (sonst Temp), abgelegt unter <c>reports/T-0776/</c>.</summary>
public class T0776_Fremdnachlauf
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    static readonly CultureInfo De = CultureInfo.GetCultureInfo("de-DE"); // LibreOffice schreibt Zahlen im CSV mit dem Gebietsschema des Dokuments

    // ---------- Werkzeuge finden (nur lesen: Trendo-Werkzeugordner, Programme-Ordner, PATH) ----------

    /// <summary>Konsolenfassung neben der Programmdatei bevorzugen (soffice.com, inkscape.com geben ihre Ausgabe zuverlässig weiter).</summary>
    static string? Konsole(string? exe) => exe is null ? null : Path.ChangeExtension(exe, ".com") is var c && File.Exists(c) ? c : exe;

    /// <summary>FreeCAD steht nicht im Trendo-Werkzeugkatalog: Umgebung TRENDO_FREECAD, sonst „Programme\FreeCAD*\bin\freecadcmd.exe“, sonst PATH.</summary>
    static string? FreeCad()
    {
        if (Environment.GetEnvironmentVariable("TRENDO_FREECAD") is { Length: > 0 } e && File.Exists(e)) return e;
        foreach (var pf in new[] { Environment.GetEnvironmentVariable("ProgramW6432"), Environment.GetEnvironmentVariable("ProgramFiles") }) // wie %ProgramFiles% im Werkzeugkatalog
            if (pf is { Length: > 0 } && Directory.Exists(pf))
                foreach (var d in Directory.GetDirectories(pf, "FreeCAD*").OrderDescending())
                    if (Path.Combine(d, "bin", "freecadcmd.exe") is var p && File.Exists(p)) return p;
        return Cli.Find("freecadcmd") ?? Cli.Find("FreeCADCmd");
    }

    static string Need(string? path, string was) { if (path is null) Ueberspringen.Weil("BLOCKIERT – fehlt auf diesem PC: " + was); return path; }

    static string Repo()
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d is not null; d = d.Parent)
            if (File.Exists(Path.Combine(d.FullName, "Trendo.sln"))) return d.FullName;
        throw new InvalidOperationException("Projektordner nicht gefunden");
    }

    /// <summary>Eigener Arbeitsordner für Dateien und Fremdprogramm-Profile.</summary>
    static string Work(string name)
    {
        var root = Environment.GetEnvironmentVariable("T0776_WORK") is { Length: > 0 } w ? w : Path.Combine(TempDir.SystemTemp, "trendo-t0776-work");
        var d = Path.Combine(root, name + "-" + Guid.NewGuid().ToString("N")[..6]); Directory.CreateDirectory(d); return d;
    }

    // ---------- Beleg: Rohlog mit Befehl, Exit-Code, Ausgabe; Dateien mit SHA-256; Benutzerpfade unkenntlich ----------

    sealed class Beleg : IDisposable
    {
        readonly StringBuilder _log = new(); readonly string _name;
        public string Dir { get; }
        public Beleg(string name)
        {
            _name = name;
            Dir = Path.Combine(Environment.GetEnvironmentVariable("TRENDO_BELEG_DIR") is { Length: > 0 } d ? d : Path.Combine(TempDir.SystemTemp, "trendo-t0776-beleg"), name);
            Directory.CreateDirectory(Dir);
            Line($"# T-0776 Fremdnachlauf {name} – {DateTime.Now:yyyy-MM-dd HH:mm:ss zzz}");
        }

        /// <summary>Benutzerordner in allen Schreibweisen (\, /, URL-kodiert) durch C:\Users\&lt;user&gt; ersetzen.</summary>
        public static string Clean(string s)
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile).TrimEnd('\\', '/');
            if (home.Length < 4) return s;
            var user = Path.GetFileName(home);
            var baseDir = home[..^user.Length];
            foreach (var form in new[] { home, home.Replace('\\', '/'), Uri.EscapeDataString(home), home.Replace('\\', '/').Replace(" ", "%20") })
                s = s.Replace(form, form[..^user.Length] + "<user>", StringComparison.OrdinalIgnoreCase);
            // übrig gebliebene Kurzformen wie C:\Users\NAME~1
            s = System.Text.RegularExpressions.Regex.Replace(s, System.Text.RegularExpressions.Regex.Escape(baseDir) + @"[^\\/<>""\s]*~\d", baseDir + "<user>", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            return s.Replace(Environment.MachineName, "<pc>", StringComparison.OrdinalIgnoreCase);
        }

        public void Line(string s) => _log.Append(Clean(s)).Append('\n');

        public Cli.Result Run(string label, string exe, IReadOnlyList<string> args, IDictionary<string, string>? env = null, int timeoutMs = 300000, string? cwd = null)
        {
            Line($"$ {label}");
            Line($"  {Path.GetFileName(exe)} {string.Join(' ', args.Select(a => a.Contains(' ') ? $"\"{a}\"" : a))}");
            if (env is not null) foreach (var kv in env) Line($"  Umgebung {kv.Key}={kv.Value}");
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var r = Cli.Run(exe, args, cwd, timeoutMs, env, Encoding.UTF8);
            Line($"Exit {r.Exit} nach {sw.Elapsed.TotalSeconds:0.0} s");
            if (r.Out.Trim().Length > 0) Line(Cut(r.Out));
            if (r.Err.Trim().Length > 0) Line("[stderr] " + Cut(r.Err));
            return r;
        }

        static string Cut(string s) { s = s.Trim(); return s.Length > 8000 ? s[..8000] + " …(gekürzt)" : s; }

        /// <summary>Datei als Beleg ablegen (Kopie) und Prüfsumme protokollieren.</summary>
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

    static string Sha(string f) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(f))).ToLowerInvariant();

    // ---------- Fremdprogramme mit eigenem Profil ----------

    /// <summary>LibreOffice ohne Fenster mit eigenem Profil (-env:UserInstallation) im Arbeitsordner.</summary>
    static string LoConvert(Beleg b, string lo, string work, string file, string format, string ext, string? infilter = null)
    {
        var outDir = Path.Combine(work, "lo-aus-" + Path.GetExtension(file).TrimStart('.') + "-" + ext); Directory.CreateDirectory(outDir);
        var args = new List<string> { "-env:UserInstallation=" + new Uri(Path.Combine(work, "lo-profil")).AbsoluteUri, "--headless", "--norestore", "--nolockcheck" };
        if (infilter is not null) args.Add("--infilter=" + infilter);
        args.AddRange(["--convert-to", format, "--outdir", outDir, file]);
        var r = b.Run($"LibreOffice öffnet {Path.GetFileName(file)} und schreibt {ext}", lo, args);
        var target = Path.Combine(outDir, Path.GetFileNameWithoutExtension(file) + "." + ext);
        Assert.True(r.Ok, $"LibreOffice Exit {r.Exit}: {r.Err}");
        return target;
    }

    static Dictionary<string, string> KritaEnv(string work)
    {
        var home = Path.Combine(work, "krita-profil"); Directory.CreateDirectory(Path.Combine(home, "AppData", "Roaming")); Directory.CreateDirectory(Path.Combine(home, "AppData", "Local"));
        return new() { ["USERPROFILE"] = home, ["APPDATA"] = Path.Combine(home, "AppData", "Roaming"), ["LOCALAPPDATA"] = Path.Combine(home, "AppData", "Local"), ["HOME"] = home };
    }

    static Dictionary<string, string> InkscapeEnv(string work)
    {
        var p = Path.Combine(work, "inkscape-profil"); Directory.CreateDirectory(Path.Combine(p, "AppData", "Roaming")); Directory.CreateDirectory(Path.Combine(p, "AppData", "Local"));
        return new() { ["APPDATA"] = Path.Combine(p, "AppData", "Roaming"), ["LOCALAPPDATA"] = Path.Combine(p, "AppData", "Local"), ["INKSCAPE_PROFILE_DIR"] = Path.Combine(p, "profil"), ["XDG_CONFIG_HOME"] = Path.Combine(p, "config"), ["XDG_CACHE_HOME"] = Path.Combine(p, "cache"), ["XDG_DATA_HOME"] = Path.Combine(p, "data"), ["USERPROFILE"] = p, ["HOME"] = p };
    }

    static Dictionary<string, string> FreeCadEnv(string work, string art, string input, string json)
    {
        var p = Path.Combine(work, "freecad-profil"); foreach (var s in new[] { "home", "daten", "tmp" }) Directory.CreateDirectory(Path.Combine(p, s));
        return new() { ["FREECAD_USER_HOME"] = Path.Combine(p, "home"), ["FREECAD_USER_DATA"] = Path.Combine(p, "daten"), ["FREECAD_USER_TEMP"] = Path.Combine(p, "tmp"), ["USERPROFILE"] = Path.Combine(p, "home"),
            ["T0776_ART"] = art, ["T0776_IN"] = input, ["T0776_OUT"] = json };
    }

    static JsonObject FreeCadRead(Beleg b, string fc, string work, string art, string file)
    {
        var json = Path.Combine(work, Path.GetFileNameWithoutExtension(file) + "-freecad.json");
        var r = b.Run($"FreeCAD liest {Path.GetFileName(file)}", fc, [Path.Combine(Repo(), "tools", "echtlauf", "t0776", "freecad_pruefe.py")], FreeCadEnv(work, art, file, json));
        Assert.True(File.Exists(json), $"FreeCAD hat kein Ergebnis geschrieben (Exit {r.Exit}): {r.Err}");
        b.Keep(json);
        var o = (JsonObject)JsonNode.Parse(File.ReadAllText(json))!;
        Assert.True(o["fehler"] is null, "FreeCAD meldet: " + o["fehler"]);
        return o;
    }

    // ===================== LibreOffice =====================

    [Fact(DisplayName = "T-0776 echt: Texte (SYS-10) – Trendo schreibt DOCX und ODT, LibreOffice 26 öffnet beide und liefert Überschrift, Absätze, Liste und Tabelle als Text zurück")]
    public void Text_docx_odt_in_LibreOffice()
    {
        var lo = Konsole(Need(NeedsFactAttribute.Tool("libreoffice"), "LibreOffice (soffice)"));
        var work = Work("text");
        using var b = new Beleg("libreoffice-text");
        var d = new TextDoc { Title = "Nachlauf" };
        var intro = new Para("Dieser Absatz ist fett und kursiv – Umlaute äöüß bleiben.");
        TextDoc.Format(intro, 18, 4, Fmt.Bold, true); TextDoc.Format(intro, 27, 6, Fmt.Italic, true);
        d.Blocks.AddRange([new Para("Quartalsbericht Nachlauf", ParaStyle.Title), new Para("Kapitel eins", ParaStyle.Heading1), intro,
            new Para("Erster Punkt") { List = ListKind.Bullet }, new Para("Zweiter Punkt") { List = ListKind.Bullet },
            new Table { Rows = [["Posten", "Betrag"], ["Miete", "800 €"], ["Strom", "64,50 €"]] }, new PageBreak(), new Para("Zweite Seite, letzter Satz.")]);
        foreach (var ext in new[] { "docx", "odt" })
        {
            var f = Path.Combine(work, "nachlauf." + ext);
            if (ext == "docx") DocxFormat.Write(d, f); else OdtFormat.Write(d, f);
            b.Keep(f, "trendo-nachlauf." + ext);
            var txt = LoConvert(b, lo, work, f, "txt:Text (encoded):UTF8", "txt");
            b.Keep(txt, "libreoffice-aus-" + ext + ".txt");
            var t = File.ReadAllText(txt, Encoding.UTF8);
            b.Line($"LibreOffice-Text aus {ext}: " + t.Replace("\r", "").Replace("\n", " ⏎ ").Trim());
            foreach (var s in new[] { "Quartalsbericht Nachlauf", "Kapitel eins", "Dieser Absatz ist fett und kursiv – Umlaute äöüß bleiben.", "Erster Punkt", "Zweiter Punkt", "Posten", "Miete", "800 €", "64,50 €", "Zweite Seite, letzter Satz." })
                Assert.True(t.Contains(s, StringComparison.Ordinal), $"LibreOffice liest „{s}“ nicht aus {ext}");
            // Seitenumbruch: LibreOffice zählt 2 Seiten im PDF
            var pdf = LoConvert(b, lo, work, f, "pdf", "pdf");
            var raw = Encoding.Latin1.GetString(File.ReadAllBytes(pdf));
            var pages = System.Text.RegularExpressions.Regex.Matches(raw, @"/Type\s*/Page[^s]").Count;
            b.Keep(pdf, "libreoffice-aus-" + ext + ".pdf"); b.Line($"LibreOffice-PDF aus {ext}: {pages} Seiten");
            Assert.Equal(2, pages);
        }
        b.Line("ERGEBNIS: BESTANDEN");
    }

    [Fact(DisplayName = "T-0776 echt: Geld-Auswertung (SPEC-11) – Trendos XLSX öffnet in LibreOffice Calc mit drei Blättern; Calc rechnet die Formeln selbst nach (Überschuss 1035, Monatssumme 1297)")]
    public void Geld_xlsx_in_LibreOffice()
    {
        var lo = Konsole(Need(NeedsFactAttribute.Tool("libreoffice"), "LibreOffice (soffice)"));
        var work = Work("geld");
        using var b = new Beleg("libreoffice-geld-xlsx");
        List<Entry> buch =
        [
            new(new(2026, 3, 1), "Rechnung RE-2026-001", 1190m, "Einnahmen", 19m), new(new(2026, 3, 9), "Fachbuch verkauft", 107m, "Einnahmen", 7m), new(new(2026, 3, 12), "Papier", -11.90m, "Büro", 19m),
            new(new(2026, 4, 1), "Kontoführung", -5m, "Gebühren"), new(new(2026, 4, 3), "Rewe Markt", -50m, "Lebensmittel"),
        ];
        var a = GeldAuswertung.Rechnen(buch, 2026);
        var x = Path.Combine(work, "auswertung.xlsx"); GeldAuswertung.Xlsx(a, x);
        b.Keep(x, "trendo-auswertung.xlsx");
        // alle Blätter als CSV (Semikolon, UTF-8, Rohwerte statt Anzeige, Blatt -1 = jedes Blatt eine Datei)
        LoConvert(b, lo, work, x, "csv:Text - txt - csv (StarCalc):59,34,76,1,,1031,false,true,false,false,false,-1", "csv");
        var outDir = Path.Combine(work, "lo-aus-xlsx-csv");
        var sheets = Directory.GetFiles(outDir, "*.csv").OrderBy(f => f, StringComparer.Ordinal).ToList();
        foreach (var s in sheets) b.Keep(s, "libreoffice-" + Path.GetFileName(s));
        b.Line("Blätter laut LibreOffice: " + string.Join(", ", sheets.Select(Path.GetFileNameWithoutExtension)));
        Assert.Equal(3, sheets.Count);
        string[][] Rows(string part) => File.ReadAllLines(sheets.Single(f => Path.GetFileNameWithoutExtension(f).EndsWith(part, StringComparison.Ordinal)), Encoding.UTF8).Select(l => l.Split(';').Select(c => c.Trim('"')).ToArray()).ToArray();
        var ueber = Rows("Übersicht"); var monate = Rows("Monate"); var buchungen = Rows("Buchungen");
        var ue = ueber.Single(r => r.Length > 2 && r[1].StartsWith("Überschuss", StringComparison.Ordinal));
        b.Line($"Calc: Überschuss-Zeile = {string.Join(" | ", ue)}");
        Assert.Equal(1035m, decimal.Parse(ue[2], De));
        var summe = monate.Single(r => r[0] == "Summe");
        b.Line($"Calc: Monate/Summe = {string.Join(" | ", summe)}");
        Assert.Equal((1297m, 66.90m), (decimal.Parse(summe[1], De), decimal.Parse(summe[2], De)));
        Assert.Equal(1230.10m, decimal.Parse(summe[3], De));
        Assert.Equal(6, buchungen.Length); // Kopf + 5 Buchungen
        Assert.Contains(buchungen, r => r.Length > 1 && r[1] == "Kontoführung");
        b.Line("ERGEBNIS: BESTANDEN");
    }

    [Fact(DisplayName = "T-0776 echt: Online verkaufen – Trendos Shopify-CSV (Artikel, Kunden, Bestellungen) öffnet in LibreOffice Calc: Spalten, Umlaute, Preise stimmen, Formel-Zelle bleibt Text")]
    public void Shop_csv_in_LibreOffice()
    {
        var lo = Konsole(Need(NeedsFactAttribute.Tool("libreoffice"), "LibreOffice (soffice)"));
        var work = Work("shop");
        using var b = new Beleg("libreoffice-shop-csv");
        var s = new Shop(Path.Combine(work, "daten"), "i");
        s.AddCustomer(new BizContact("Erika Muster", "", "erika@example.org", "Kunde", null, 0, "", "Hauptstraße 1, 10115 Berlin"));
        s.AddProduct(new Product("Shirt Waldlauf", "", 0, 0, 0, 0, 0, "", "Bio-Baumwolle, bedruckt", "", [new("M", 21, 3, "SW-M"), new("L", 22.5, 1, "SW-L")]));
        s.AddProduct(new Product("=1+1", "", 0, 5, 1, 0, 0, "", "Größe „XL“; süß"));
        s.PlaceOrder("Erika Muster", new DateOnly(2026, 11, 26), ("Shirt Waldlauf – M", 2));
        var files = new[] { s.ExportProductsCsv(Path.Combine(work, "artikel.csv")), s.ExportCustomersCsv(Path.Combine(work, "kunden.csv")), s.ExportOrdersCsv(Path.Combine(work, "bestellungen.csv")) };
        var expectCols = new[] { Shop.ProductColumns.Length, Shop.CustomerColumns.Length, Shop.OrderColumns.Length };
        var lists = new List<string[][]>();
        for (var i = 0; i < files.Length; i++)
        {
            b.Keep(files[i], "trendo-" + Path.GetFileName(files[i]));
            // Eingabe: Komma, Anführungszeichen, UTF-8 (76), ab Zeile 1, Sprache Englisch (USA) für „21.00“; Ausgabe: Semikolon, Rohwerte
            var outCsv = LoConvert(b, lo, work, files[i], "csv:Text - txt - csv (StarCalc):59,34,76,1,,1031,false,false,false,false,false", "csv", "CSV:44,34,76,1,,1033,true,false");
            var name = "libreoffice-" + Path.GetFileName(files[i]);
            b.Keep(outCsv, name);
            var rows = File.ReadAllLines(outCsv, Encoding.UTF8).Select(SplitSemicolon).ToArray();
            b.Line($"{Path.GetFileName(files[i])}: LibreOffice liest {rows.Length - 1} Datenzeilen, {rows[0].Length} Spalten; Kopf: {string.Join(" | ", rows[0].Take(6))} …");
            Assert.Equal(expectCols[i], rows[0].Length);
            Assert.All(rows, r => Assert.Equal(expectCols[i], r.Length));
            lists.Add(rows);
        }
        var art = lists[0]; var h = art[0].ToList();
        string C(string[] r, string col) => r[h.IndexOf(col)];
        var m = art.Single(r => C(r, "SKU") == "SW-M"); var l = art.Single(r => C(r, "SKU") == "SW-L");
        b.Line($"Calc: SW-M Preis {C(m, "Price")}, Bestand {C(m, "Inventory quantity")}; SW-L Preis {C(l, "Price")}");
        Assert.Equal((21m, 3m), (decimal.Parse(C(m, "Price"), De), decimal.Parse(C(m, "Inventory quantity"), De)));
        Assert.Equal(22.5m, decimal.Parse(C(l, "Price"), De));
        Assert.Equal("Shirt Waldlauf", C(m, "Title")); Assert.Equal("Bio-Baumwolle, bedruckt", C(m, "Description"));
        var f = art.Single(r => C(r, "Description").Contains("XL", StringComparison.Ordinal));
        b.Line($"Calc: Formel-Artikel Titel = „{C(f, "Title")}“, Beschreibung = „{C(f, "Description")}“");
        Assert.NotEqual("2", C(f, "Title")); Assert.Contains("=1+1", C(f, "Title")); // nicht ausgerechnet: Formel-Einschleusung bleibt entschärft
        Assert.Equal("Größe „XL“; süß", System.Net.WebUtility.HtmlDecode(C(f, "Description"))); // Shopify-Beschreibung ist HTML: Trendo schreibt ö/ß als &#246;/&#223;
        var kunde = lists[1][1]; b.Line($"Calc: Kunde {kunde[0]} {kunde[1]}, {kunde[5]}, {kunde[10]} {kunde[7]}");
        Assert.Equal(("Erika", "Muster", "Hauptstraße 1", "10115", "Berlin"), (kunde[0], kunde[1], kunde[5], kunde[10], kunde[7]));
        var best = lists[2][1]; var oh = lists[2][0].ToList();
        Assert.Equal(("2", "Shirt Waldlauf – M"), (best[oh.IndexOf("Lineitem quantity")], best[oh.IndexOf("Lineitem name")]));
        b.Line("ERGEBNIS: BESTANDEN");
    }

    /// <summary>Semikolon-CSV aus LibreOffice (Anführungszeichen nur bei Bedarf) zerlegen.</summary>
    static string[] SplitSemicolon(string line)
    {
        var res = new List<string>(); var sb = new StringBuilder(); var q = false;
        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (q) { if (c == '"' && i + 1 < line.Length && line[i + 1] == '"') { sb.Append('"'); i++; } else if (c == '"') q = false; else sb.Append(c); }
            else if (c == '"') q = true;
            else if (c == ';') { res.Add(sb.ToString()); sb.Clear(); }
            else sb.Append(c);
        }
        res.Add(sb.ToString()); return [.. res];
    }

    // ===================== FreeCAD =====================

    [Fact(DisplayName = "T-0776 echt: CAD-STEP (CORE-05) – FreeCAD 1.1 liest Trendos STEP als gültigen geschlossenen Körper: Volumen 6000 mm³ bzw. Winkel, Umriss und Name stimmen")]
    public void Cad_step_in_FreeCAD()
    {
        var fc = Need(FreeCad() ?? Cli.Find("freecadcmd"), "FreeCAD (freecadcmd)");
        var work = Work("step");
        using var b = new Beleg("freecad-step");
        var quader = CadShapes.Box(new Vec3(0, 0, 0), 30, 20, 10);
        var winkel = CadShapes.Box(new Vec3(40, 0, 0), 20, 20, 20).Subtract(CadShapes.Box(new Vec3(45, 5, 5), 15.01, 15.01, 15.01));
        var f = Path.Combine(work, "teile.step"); CadExport.WriteStep([("Quader", quader), ("Winkel", winkel)], f);
        b.Keep(f, "trendo-teile.step");
        var o = FreeCadRead(b, fc, work, "step", f);
        b.Line($"FreeCAD {string.Join('.', o["freecad"]!.AsArray().Select(v => (string?)v))}: gültig={o["gueltig"]}, Körper={o["koerper"]}, Volumen={o["volumen"]}, Umriss={o["umriss"]!.ToJsonString()}, Namen={o["namen"]?.ToJsonString()}");
        Assert.True((bool)o["gueltig"]!);
        Assert.Equal(2, (int)o["koerper"]!);
        Assert.True((bool)o["schalen_geschlossen"]!);
        var vols = o["koerper_volumen"]!.AsArray().Select(v => (double)v!).Order().ToList();
        Assert.Equal(20 * 20 * 20 - 15 * 15 * 15, vols[0], 3); Assert.Equal(6000, vols[1], 3); // aufsteigend: Winkel 4625, Quader 6000
        Assert.Equal(new[] { 0.0, 0, 0, 60, 20, 20 }, o["umriss"]!.AsArray().Select(v => (double)v!).ToArray());
        Assert.Equal(["Quader", "Winkel"], o["namen"]!.AsArray().Select(v => (string)v!).Order().ToArray());
        b.Line("ERGEBNIS: BESTANDEN");
    }

    [Fact(DisplayName = "T-0776 echt: CAD-DXF (CORE-05) – fremde DXF in Trendo bearbeitet und als DXF geschrieben; FreeCAD 1.1 liest Ebenen, Linien, Kreise, Bogen und Polylinie mit den Trendo-Maßen")]
    public void Cad_dxf_in_FreeCAD()
    {
        var fc = Need(FreeCad() ?? Cli.Find("freecadcmd"), "FreeCAD (freecadcmd)");
        var work = Work("cad-dxf");
        using var b = new Beleg("freecad-cad-dxf");
        var src = Path.Combine(Repo(), "tests", "fixtures", "t0691", "fremd-halteplatte.dxf");
        var (d, _) = CadDxf.Read(src);
        d.Entities.Single(e => e.Kind == "linie").Points[1][0] = 70; // bearbeiten wie im Programm
        var f = Path.Combine(work, "halteplatte-trendo.dxf"); File.WriteAllText(f, CadDxf.Write(d, [("Umriss", 0, 0, 10, 0)]));
        b.Line($"Vorlage {Path.GetFileName(src)}: SHA-256 {Sha(src)}");
        b.Keep(f, "trendo-halteplatte.dxf");
        var o = FreeCadRead(b, fc, work, "dxf", f);
        var list = o["liste"]!.AsArray().Select(x => x!.AsObject()).ToList();
        b.Line($"FreeCAD: {o["objekte"]} Objekte, {o["kanten"]} Kanten, Umriss {o["umriss"]?.ToJsonString()}");
        foreach (var x in list) b.Line($"  {x["typ"]} „{x["name"]}“ Kanten={x["kanten"]} {x["text"]?.ToJsonString()}");
        var trendoEdges = d.Entities.Count(e => e.Kind != "text");
        Assert.True((int)o["kanten"]! >= trendoEdges, $"FreeCAD liest {o["kanten"]} Kanten, Trendo hat {trendoEdges} Zeichenelemente");
        var bb = o["umriss"]!.AsArray().Select(v => (double)v!).ToArray();
        Assert.True(bb[3] >= 70 - 1e-6, "Bearbeitete Linie (x = 70) fehlt im Umriss von FreeCAD");
        var names = list.Select(x => (string?)x["name"]).ToHashSet();
        foreach (var layer in d.Layers.Select(l => l.Name).Append("Umriss")) Assert.True(names.Contains(layer), $"FreeCAD kennt die Ebene „{layer}“ nicht");
        // Texte: Der DXF-Leser von FreeCAD übernimmt sie ohne Oberfläche (freecadcmd) nicht, auch nicht mit „dxftext“ – ehrlich als nicht geprüft vermerkt
        var texts = list.Count(x => x["text"] is not null);
        b.Line($"Texte in FreeCAD (Kommandozeile): {texts} – Trendo schreibt {d.Entities.Count(e => e.Kind == "text")}; Texte sind mit FreeCAD hier NICHT geprüft");
        b.Line("ERGEBNIS: BESTANDEN");
    }

    [Fact(DisplayName = "T-0776 echt: Wohnung planen (SPEC-01) – fremde Wohnungs-DXF in Trendo bearbeitet und als DXF exportiert; FreeCAD 1.1 liest Wände und Umriss in Plangröße")]
    public void Plan_dxf_in_FreeCAD()
    {
        var fc = Need(FreeCad() ?? Cli.Find("freecadcmd"), "FreeCAD (freecadcmd)");
        var work = Work("plan-dxf");
        using var b = new Beleg("freecad-wohnung-dxf");
        var src = Path.Combine(Repo(), "tests", "fixtures", "t0747", "wohnung-fremd.dxf");
        var p = new FloorPlan(); PlanDxfImport.Import(p, CadDxf.Read(src));
        var wall = p.Walls.Single(w => Math.Abs(w.Thickness - 11.5) < 0.01); wall.Thickness = 17.5; // bearbeiten: Innenwand dicker
        var f = Path.Combine(work, "wohnung-bearbeitet.dxf"); File.WriteAllText(f, PlanDrawing.Dxf(p));
        b.Line($"Vorlage {Path.GetFileName(src)}: SHA-256 {Sha(src)}; Trendo: {p.Walls.Count} Wände, {p.Openings.Count} Öffnungen");
        b.Keep(f, "trendo-wohnung.dxf");
        var back = CadDxf.Read(f).Drawing; // Trendo selbst (Vergleichsgröße)
        var o = FreeCadRead(b, fc, work, "dxf", f);
        b.Line($"FreeCAD: {o["objekte"]} Objekte, {o["kanten"]} Kanten, Umriss {o["umriss"]?.ToJsonString()}");
        var bb = o["umriss"]!.AsArray().Select(v => (double)v!).ToArray();
        var xs = p.Walls.SelectMany(w => new[] { w.X1, w.X2 }).ToList(); var ys = p.Walls.SelectMany(w => new[] { w.Y1, w.Y2 }).ToList();
        var planW = xs.Max() - xs.Min(); var planH = ys.Max() - ys.Min();
        // Linien der Trendo-DXF (Wände, Öffnungen, Maße) in DXF-Einheiten; die Datei sagt $INSUNITS = 5 (Zentimeter), FreeCAD rechnet in Millimeter
        var lines = back.Entities.Where(e => e.Kind == "linie").SelectMany(e => e.Points).ToList();
        double[] mine = [lines.Min(q => q[0]), lines.Min(q => q[1]), lines.Max(q => q[0]), lines.Max(q => q[1])]; // Trendos Leser rechnet $INSUNITS schon in mm um
        b.Line($"Trendo-Plan: Wandachsen {planW:0.##} × {planH:0.##} cm; Linien der Trendo-DXF in mm [{string.Join(", ", mine.Select(v => v.ToString("0.##", Inv)))}]; " +
            $"FreeCAD-Umriss in mm [{bb[0]:0.##}, {bb[1]:0.##}, {bb[3]:0.##}, {bb[4]:0.##}]; Trendo liest seine DXF mit {back.Entities.Count} Elementen");
        Assert.True((int)o["kanten"]! >= p.Walls.Count * 2, "FreeCAD liest zu wenige Kanten für die Wände");
        Assert.True(bb[3] - bb[0] >= planW * 10 - 1 && bb[4] - bb[1] >= planH * 10 - 1, "Umriss in FreeCAD kleiner als der Plan");
        // FreeCAD übernimmt Zentimeter (×10) und die Lage der Trendo-Linien auf 1 mm genau; Bögen der Türen liegen innerhalb
        double[] theirs = [bb[0], bb[1], bb[3], bb[4]];
        for (var k = 0; k < 4; k++) Assert.True(Math.Abs(theirs[k] - mine[k]) <= 1, $"Umriss weicht ab: FreeCAD {theirs[k]:0.##} mm, Trendo {mine[k]:0.##} mm");
        b.Line("ERGEBNIS: BESTANDEN");
    }

    // ===================== Krita =====================

    static PaintDoc ArtSample()
    {
        var d = new PaintDoc(40, 30, "#F0E0D0");
        var a = d.Add("Skizze"); var c = d.Add("Farbe rötlich"); var h = d.Add("Versteckt");
        void Rect(PaintLayer l, int x0, int y0, int x1, int y1, byte r, byte g, byte bl) { for (var y = y0; y < y1; y++) for (var x = x0; x < x1; x++) { var i = (y * 40 + x) * 4; l.Px.Px[i] = r; l.Px.Px[i + 1] = g; l.Px.Px[i + 2] = bl; l.Px.Px[i + 3] = 255; } }
        Rect(a, 2, 2, 12, 12, 0x12, 0x34, 0x56);
        Rect(c, 20, 10, 36, 26, 200, 30, 30);
        h.Px.Fill(0, 200, 0, 255); h.Visible = false;
        return d;
    }

    /// <summary>Pixel bei (x, y) als #RRGGBB.</summary>
    static string Pix(Pixmap p, int x, int y) { var i = (y * p.W + x) * 4; return $"#{p.Px[i]:X2}{p.Px[i + 1]:X2}{p.Px[i + 2]:X2}"; }

    [Fact(DisplayName = "T-0776 echt: Künstlerstudio – Trendos PSD und ORA öffnen in Krita 6: Krita liest Ebenen (Namen, Sichtbarkeit) und zeigt dieselben Farben wie Trendo")]
    public void Art_psd_ora_in_Krita()
    {
        var krita = Need(NeedsFactAttribute.Tool("krita"), "Krita");
        var work = Work("kunst");
        using var b = new Beleg("krita-psd-ora");
        var d = ArtSample();
        var psd = Path.Combine(work, "bild.psd"); File.WriteAllBytes(psd, PaintFiles.Psd(d));
        var ora = Path.Combine(work, "bild.ora"); PaintFiles.SaveOra(d, ora);
        b.Keep(psd, "trendo-bild.psd"); b.Keep(ora, "trendo-bild.ora");
        var mine = d.Composite();
        foreach (var src in new[] { psd, ora })
        {
            var ext = Path.GetExtension(src).TrimStart('.');
            var env = KritaEnv(work);
            var outOra = Path.Combine(work, "krita-aus-" + ext + ".ora"); var outPng = Path.Combine(work, "krita-aus-" + ext + ".png");
            var r1 = b.Run($"Krita öffnet {Path.GetFileName(src)} und speichert ORA", krita, [src, "--export", "--export-filename", outOra], env);
            var r2 = b.Run($"Krita öffnet {Path.GetFileName(src)} und speichert PNG", krita, [src, "--export", "--export-filename", outPng], env);
            Assert.True(File.Exists(outOra), $"Krita hat aus {ext} keine ORA geschrieben (Exit {r1.Exit}): {r1.Err}");
            Assert.True(File.Exists(outPng), $"Krita hat aus {ext} kein PNG geschrieben (Exit {r2.Exit}): {r2.Err}");
            b.Keep(outOra); b.Keep(outPng);
            using (var z = ZipFile.OpenRead(outOra))
            {
                var stackText = new StreamReader(z.GetEntry("stack.xml")!.Open(), Encoding.UTF8).ReadToEnd();
                Assert.False(stackText.Contains('\0'), "Krita liest die Ebenennamen mit Füllbytes – der Name ist nicht sauber in der Datei: " + stackText.Replace("\0", "<NUL>"));
                var stack = XDocument.Parse(stackText);
                var layers = stack.Descendants("layer").Select(l => ((string?)l.Attribute("name") ?? "", (string?)l.Attribute("visibility") ?? "visible")).ToList();
                b.Line($"Krita-Ebenen aus {ext}: " + string.Join(", ", layers.Select(l => $"{l.Item1} ({l.Item2})")));
                // genau die Trendo-Namen (ohne Füllbytes, mit Umlaut) und Sichtbarkeit; Krita listet von oben nach unten
                Assert.Equal([("Versteckt", "hidden"), ("Farbe rötlich", "visible"), ("Skizze", "visible")], layers.Where(l => l.Item1 != "Hintergrund").ToList());
            }
            var theirs = PngCodec.Decode(File.ReadAllBytes(outPng));
            Assert.Equal((40, 30), (theirs.W, theirs.H));
            foreach (var (x, y) in new[] { (5, 5), (25, 15), (38, 2), (15, 20) })
            {
                b.Line($"  Punkt ({x}|{y}): Trendo {Pix(mine, x, y)}, Krita aus {ext} {Pix(theirs, x, y)}");
                Assert.Equal(Pix(mine, x, y), Pix(theirs, x, y));
            }
        }
        b.Line("ERGEBNIS: BESTANDEN");
    }

    // ===================== Inkscape =====================

    [Fact(DisplayName = "T-0776 echt: Logos/Animation – Trendos Logo-SVG und Animations-SVG öffnen in Inkscape 1.4: Formen mit Trendo-Kennung und -Lage, gerenderte Farben wie im Logo")]
    public void Svg_in_Inkscape()
    {
        var ink = Konsole(Need(NeedsFactAttribute.Tool("inkscape"), "Inkscape"));
        var work = Work("svg");
        using var b = new Beleg("inkscape-svg");
        var env = InkscapeEnv(work);
        var r = VectorKit.New("rect", 20, 30, 120, 60, "flaeche", "#1E3A8A");
        var e = VectorKit.New("ellipse", 180, 20, 80, 80, "kreis", "#D4A017");
        var t = VectorKit.New("text", 20, 150, 200, 40, "wort", "#222222"); t.Text = "Bäckerei Muster";
        var logo = new VDoc { W = 300, H = 200, Background = "#FFFFFF", Shapes = [r, e, t] };
        var svg = Path.Combine(work, "logo.svg"); File.WriteAllText(svg, VectorKit.ToSvg(logo, background: "#FFFFFF"), new UTF8Encoding(false));
        var anim = new VDoc { W = 320, H = 180, Background = "#101827", Shapes = [VectorKit.New("ellipse", 10, 60, 40, 40, "ball", "#F5C542")] };
        MotionKit.SetKey(anim.Shapes[0], "x", 0, 10); MotionKit.SetKey(anim.Shapes[0], "x", 2, 260);
        var asvg = Path.Combine(work, "animation.svg"); File.WriteAllText(asvg, MotionKit.Svg(anim), new UTF8Encoding(false));
        b.Keep(svg, "trendo-logo.svg"); b.Keep(asvg, "trendo-animation.svg");

        var q = b.Run("Inkscape liest logo.svg (alle Objekte mit Lage)", ink, ["--query-all", svg], env);
        Assert.True(q.Ok, $"Inkscape Exit {q.Exit}: {q.Err}");
        var objs = q.Out.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(l => l.Split(',')).Where(p => p.Length == 5)
            .ToDictionary(p => p[0], p => p.Skip(1).Select(v => double.Parse(v, Inv)).ToArray());
        foreach (var id in new[] { "flaeche", "kreis", "wort" }) Assert.True(objs.Keys.Any(k => k.Contains(id, StringComparison.Ordinal)), $"Inkscape findet „{id}“ nicht");
        var fl = objs.First(k => k.Key.Contains("flaeche", StringComparison.Ordinal)).Value;
        b.Line($"Inkscape: flaeche bei x={fl[0]} y={fl[1]} b={fl[2]} h={fl[3]} (Trendo 20/30/120/60)");
        Assert.Equal(new[] { 20.0, 30, 120, 60 }, fl.Select(v => Math.Round(v, 1)).ToArray());

        var png = Path.Combine(work, "inkscape-logo.png");
        var x = b.Run("Inkscape rendert logo.svg als PNG (300 px breit)", ink, [svg, "--export-type=png", "--export-filename=" + png, "--export-width=300", "--export-background=#FFFFFF", "--export-background-opacity=1"], env);
        Assert.True(x.Ok && File.Exists(png), $"Inkscape PNG fehlt (Exit {x.Exit}): {x.Err}");
        b.Keep(png);
        var pm = PngCodec.Decode(File.ReadAllBytes(png));
        b.Line($"Inkscape-PNG {pm.W}×{pm.H}: Fläche {Pix(pm, 80, 60)}, Kreis {Pix(pm, 220, 60)}, frei {Pix(pm, 290, 190)}");
        Assert.Equal(("#1E3A8A", "#D4A017", "#FFFFFF"), (Pix(pm, 80, 60), Pix(pm, 220, 60), Pix(pm, 290, 190)));

        var qa = b.Run("Inkscape liest animation.svg", ink, ["--query-all", asvg], env);
        Assert.True(qa.Ok, $"Inkscape Exit {qa.Exit}: {qa.Err}");
        // Inkscape kennt kein SMIL (es meldet animate/animateTransform als unbekannt): geprüft wird, dass es die Datei liest und die Form in Trendo-Größe enthält
        Assert.Contains(qa.Out.Split('\n', StringSplitOptions.TrimEntries), l => l.StartsWith("path", StringComparison.Ordinal) && l.EndsWith(",40,40", StringComparison.Ordinal));
        var apng = Path.Combine(work, "inkscape-animation.png");
        var xa = b.Run("Inkscape rendert animation.svg (Anfangsbild)", ink, [asvg, "--export-type=png", "--export-filename=" + apng, "--export-width=320"], env);
        Assert.True(xa.Ok && File.Exists(apng), $"Inkscape PNG fehlt (Exit {xa.Exit}): {xa.Err}");
        b.Keep(apng);
        var am = PngCodec.Decode(File.ReadAllBytes(apng));
        b.Line($"Inkscape-PNG Animation {am.W}×{am.H}: Hintergrund {Pix(am, 300, 170)}");
        Assert.Equal("#101827", Pix(am, 300, 170));
        b.Line("ERGEBNIS: BESTANDEN");
    }

    // ===================== 7-Zip =====================

    static (List<(string Path, long Size, string Crc)> Entries, Cli.Result Raw) SevenList(Beleg b, string sz, string zip)
    {
        var l = b.Run($"7-Zip listet {Path.GetFileName(zip)}", sz, ["l", "-slt", "-ba", "-sccUTF-8", zip]);
        Assert.True(l.Ok, $"7-Zip Exit {l.Exit}: {l.Err}");
        var res = new List<(string, long, string)>(); string? p = null; long size = 0; string crc = ""; var folder = false;
        foreach (var line in (l.Out + "\n").Split('\n').Select(x => x.TrimEnd('\r')))
        {
            if (line.StartsWith("Path = ", StringComparison.Ordinal)) { if (p is not null && !folder) res.Add((p, size, crc)); p = line[7..]; size = 0; crc = ""; folder = false; }
            else if (line.StartsWith("Size = ", StringComparison.Ordinal)) size = long.Parse(line[7..], Inv);
            else if (line.StartsWith("CRC = ", StringComparison.Ordinal)) crc = line[6..];
            else if (line.StartsWith("Folder = +", StringComparison.Ordinal)) folder = true;
        }
        if (p is not null && !folder) res.Add((p, size, crc));
        return (res, l);
    }

    static string SevenCheck(Beleg b, string sz, string zip, string work, string? sourceDir)
    {
        var t = b.Run($"7-Zip prüft {Path.GetFileName(zip)}", sz, ["t", "-sccUTF-8", zip]);
        Assert.True(t.Ok && t.Out.Contains("Everything is Ok", StringComparison.Ordinal), $"7-Zip meldet Fehler (Exit {t.Exit}): {t.Out} {t.Err}");
        var (entries, raw) = SevenList(b, sz, zip);
        b.Line($"7-Zip: {entries.Count} Dateien in {Path.GetFileName(zip)}");
        Assert.NotEmpty(entries);
        var outDir = Path.Combine(work, "7z-" + Path.GetFileNameWithoutExtension(zip));
        var x = b.Run($"7-Zip entpackt {Path.GetFileName(zip)}", sz, ["x", "-y", "-sccUTF-8", "-o" + outDir, zip]);
        Assert.True(x.Ok, $"7-Zip entpackt nicht (Exit {x.Exit}): {x.Err}");
        foreach (var (path, size, _) in entries)
        {
            var got = Path.Combine(outDir, path);
            Assert.True(File.Exists(got), "7-Zip hat nicht entpackt: " + path);
            Assert.Equal(size, new FileInfo(got).Length);
            if (sourceDir is not null && Path.Combine(sourceDir, path) is var orig && File.Exists(orig))
            {
                var same = Sha(orig) == Sha(got);
                b.Line($"  {path}: {size} Bytes, SHA-256 {Sha(got)} {(same ? "= Original" : "≠ Original")}");
                Assert.True(same, "Inhalt weicht vom Original ab: " + path);
            }
            else b.Line($"  {path}: {size} Bytes, SHA-256 {Sha(got)}");
        }
        return t.Out; // „7z t“ zeigt die Eigenschaften des Archivs samt Kommentar
    }

    [Fact(DisplayName = "T-0776 echt: Daten schützen und Trendo erweitern – Sicherungs-ZIP und Erweiterungs-Export-ZIP bestehen die 7-Zip-Prüfung; entpackt sind alle Dateien bitgleich")]
    public void Zip_in_7zip()
    {
        var sz = Need(NeedsFactAttribute.Tool("7zip") ?? (Path.Combine(Environment.GetEnvironmentVariable("ProgramFiles") ?? "", "7-Zip", "7z.exe") is var p && File.Exists(p) ? p : null), "7-Zip (7z.exe)");
        var work = Work("zip");
        using var b = new Beleg("7zip-zip");
        var v = b.Run("7-Zip Version", sz, ["i"]); b.Line("7-Zip: " + v.Out.Split('\n').FirstOrDefault(l => l.Contains("7-Zip", StringComparison.Ordinal))?.Trim());
        // Sicherung (CORE-11) über einen eigenen Datenordner
        var data = Path.Combine(work, "daten"); Directory.CreateDirectory(Path.Combine(data, "projekte", "Garten"));
        File.WriteAllText(Path.Combine(data, "settings.json"), "{\"sprache\":\"de\"}");
        File.WriteAllText(Path.Combine(data, "projekte", "Garten", "plan.md"), "# Beet\nTomaten, Bohnen, Kürbis\n");
        File.WriteAllBytes(Path.Combine(data, "projekte", "Garten", "foto.bin"), RandomNumberGenerator.GetBytes(200_000));
        var backup = new Backup(data, Path.Combine(data, "backups"), new AuditLog(Path.Combine(work, "audit.jsonl")));
        var res = backup.CreateChecked(new DateTime(2026, 11, 26, 10, 0, 0));
        Assert.True(res.Complete);
        b.Line("Trendo: " + res.Say());
        b.Keep(res.Zip, "trendo-sicherung.zip");
        var listing = SevenCheck(b, sz, res.Zip, work, data);
        // Vermerk (ZIP-Kommentar) mit dem Hinweis „nicht verschlüsselt“: 7-Zip zeigt ihn an. Umlaute stellt 7-Zip falsch dar (ZIP-Kommentare haben keine
        // Kennung für UTF-8, 7-Zip liest sie in der Windows-Codepage) – deshalb wird nur der ASCII-Teil verglichen; siehe Bericht „offene Punkte“.
        var comment = listing.Split('\n').Select(l => l.TrimEnd('\r')).FirstOrDefault(l => l.StartsWith("Comment = trendo-sicherung-v1", StringComparison.Ordinal));
        b.Line("7-Zip zeigt den Vermerk: " + (comment is null ? "nein" : comment.Length > 160 ? comment[..160] + " …" : comment));
        Assert.NotNull(comment); Assert.Contains("\"NotSavedCount\":0", comment); Assert.Contains("Diese Sicherungen sind nicht verschl", comment);
        // Erweiterung (X-04): Werkstatt → bauen → Export-ZIP
        var dev = Path.Combine(work, "werkstatt");
        var dir = ExtensionWorkshop.Create(dev, "Meine Tabelle");
        Assert.Empty(ExtensionWorkshop.Build(dir));
        var (zip, err) = ExtensionWorkshop.Export(dir, ExtensionWorkshop.ExportDir(dev)); Assert.Empty(err);
        b.Keep(zip!, "trendo-erweiterung.zip");
        SevenCheck(b, sz, zip!, work, dir);
        b.Line("ERGEBNIS: BESTANDEN");
    }
}
