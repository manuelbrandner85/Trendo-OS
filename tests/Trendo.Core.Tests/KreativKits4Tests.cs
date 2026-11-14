using System.Text;
using Trendo.Core;
using Trendo.Core.Kreativ;
using Trendo.Core.PromptAction;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>Etappe „Kreativ &amp; KI“ Teil 4 (T-0615): Code mit KI, Bilder erkennen, Recherche mit Quellen, Tagesplaner.</summary>
public class KreativKits4Tests
{
    static string Temp() { var d = Path.Combine(Path.GetTempPath(), "trendo-k4-" + Guid.NewGuid().ToString("N")[..8]); Directory.CreateDirectory(d); return d; }

    // ---------------- Code mit KI ----------------

    [Fact(DisplayName = "Code mit KI: Projektart und Testbefehl je Art (dotnet, npm, Python, Cargo, Go), mehrere Lösungen, ungültiger Ordner")]
    public void Code()
    {
        var d = Temp();
        try
        {
            Assert.Empty(CodeAiKit.DetectProject(d));
            Assert.Null(CodeAiKit.TestCommand(d, "dotnet"));
            Assert.Empty(CodeAiKit.DetectProject(@"\\rechner\freigabe"));
            File.WriteAllText(Path.Combine(d, "App.csproj"), "<Project/>");
            Assert.Equal(["test", "--nologo"], CodeAiKit.TestCommand(d, "dotnet")!.Args);
            File.WriteAllText(Path.Combine(d, "B.sln"), ""); File.WriteAllText(Path.Combine(d, "A.sln"), "");
            var dn = CodeAiKit.TestCommand(d, "dotnet")!;
            Assert.Equal("dotnet test A.sln --nologo", dn.Display); // mehrere Lösungen: die erste, sonst bricht dotnet ab
            File.WriteAllText(Path.Combine(d, "package.json"), "{\"scripts\":{\"test\":\"echo \\\"Error: no test specified\\\" && exit 1\"}}");
            Assert.Equal("node --test", CodeAiKit.TestCommand(d, "npm")!.Display);
            File.WriteAllText(Path.Combine(d, "package.json"), "{\"scripts\":{\"test\":\"vitest run\"}}");
            Assert.Equal("npm test", CodeAiKit.TestCommand(d, "npm")!.Display);
            File.WriteAllText(Path.Combine(d, "package.json"), "{ kaputt");
            Assert.Equal("node --test", CodeAiKit.TestCommand(d, "npm")!.Display);
            File.WriteAllText(Path.Combine(d, "requirements.txt"), "requests\npytest>=8\n");
            Assert.Equal(["-m", "pytest", "-q"], CodeAiKit.TestCommand(d, "python")!.Args);
            File.WriteAllText(Path.Combine(d, "requirements.txt"), "requests\n");
            Directory.CreateDirectory(Path.Combine(d, "tests"));
            Assert.Equal(["-m", "unittest", "discover", "-s", "tests", "-q"], CodeAiKit.TestCommand(d, "python")!.Args);
            File.WriteAllText(Path.Combine(d, "Cargo.toml"), "[package]\nname = \"x\"");
            File.WriteAllText(Path.Combine(d, "go.mod"), "module x");
            Assert.Equal(["dotnet", "npm", "python", "cargo", "go"], CodeAiKit.DetectProject(d).Select(k => k.Id));
            Assert.Equal("cargo test", CodeAiKit.TestCommand(d, "cargo")!.Display);
            Assert.Equal("go test ./...", CodeAiKit.TestCommand(d, "go")!.Display);
            Assert.Null(CodeAiKit.TestCommand(d, "cobol"));
        }
        finally { Directory.Delete(d, true); }
    }

    [Fact(DisplayName = "Code mit KI: Schnappschuss mit Prüfsummen, neu/geändert/gelöscht, Bauordner bleiben draußen, Binärdateien ohne Text, Speichern/Laden")]
    public void Snapshot()
    {
        var d = Temp();
        try
        {
            File.WriteAllText(Path.Combine(d, "a.cs"), "class A {}\n");
            File.WriteAllText(Path.Combine(d, "weg.txt"), "alt");
            Directory.CreateDirectory(Path.Combine(d, "bin")); File.WriteAllText(Path.Combine(d, "bin", "x.dll"), "x");
            Directory.CreateDirectory(Path.Combine(d, "src")); File.WriteAllBytes(Path.Combine(d, "src", "bild.png"), [137, 80, 0, 1, 2]);
            var s1 = CodeAiKit.Take(d, new DateTime(2026, 11, 14, 8, 0, 0, DateTimeKind.Utc));
            Assert.Equal(["a.cs", "src/bild.png", "weg.txt"], s1.Files.Select(f => f.Path).Order(StringComparer.Ordinal));
            Assert.Null(s1.Files.Single(f => f.Path == "src/bild.png").Text);
            Assert.Equal("class A {}\n", s1.Files.Single(f => f.Path == "a.cs").Text);
            var back = CodeAiKit.Load(CodeAiKit.Save(s1))!;
            Assert.Equal(s1.Files.Count, back.Files.Count); Assert.Equal(s1.TakenUtc, back.TakenUtc);
            Assert.Null(CodeAiKit.Load("{ kaputt")); Assert.Null(CodeAiKit.Load("{}"));
            File.WriteAllText(Path.Combine(d, "a.cs"), "class A { int x; }\n");
            File.Delete(Path.Combine(d, "weg.txt"));
            File.WriteAllText(Path.Combine(d, "src", "neu.cs"), "class N {}");
            File.WriteAllText(Path.Combine(d, "bin", "x.dll"), "anders");
            var ch = CodeAiKit.Changed(s1, CodeAiKit.Take(d));
            Assert.Equal([("a.cs", CodeAiKit.ChangeKind.Geaendert), ("src/neu.cs", CodeAiKit.ChangeKind.Neu), ("weg.txt", CodeAiKit.ChangeKind.Geloescht)], ch.Select(c => (c.Path, c.Kind)));
            Assert.Empty(CodeAiKit.Changed(s1, s1));
            Assert.Throws<ArgumentException>(() => CodeAiKit.Take(Path.Combine(d, "gibt-es-nicht")));
        }
        finally { Directory.Delete(d, true); }
    }

    [Fact(DisplayName = "Code mit KI: LCS-Zeilen-Diff (gleich, eingefügt, gelöscht, ersetzt, leer, CRLF) und einheitliches Diff-Format mit richtigen Kopfzeilen")]
    public void Diff()
    {
        static string Ops(IReadOnlyList<CodeAiKit.Line> d) => string.Concat(d.Select(l => l.Op switch { CodeAiKit.Op.Neu => '+', CodeAiKit.Op.Weg => '-', _ => '=' }));
        Assert.Empty(CodeAiKit.Diff("", ""));
        Assert.Equal("===", Ops(CodeAiKit.Diff("a\nb\nc\n", "a\r\nb\r\nc")));
        Assert.Equal("++", Ops(CodeAiKit.Diff(null, "x\ny")));
        Assert.Equal("--", Ops(CodeAiKit.Diff("x\ny", "")));
        var d = CodeAiKit.Diff("a\nb\nc\nd", "a\nx\nc\nd\ne");
        Assert.Equal("=-+==+", Ops(d));
        Assert.Equal((2, 1), CodeAiKit.Count(d));
        Assert.Equal(new CodeAiKit.Line(CodeAiKit.Op.Neu, "x", 0, 2), d[2]);
        Assert.Equal(new CodeAiKit.Line(CodeAiKit.Op.Gleich, "d", 4, 4), d[4]);
        // LCS findet die längste gemeinsame Folge: B C bleiben
        Assert.Equal(2, CodeAiKit.Diff("A\nB\nC", "B\nC\nA").Count(l => l.Op == CodeAiKit.Op.Gleich));
        // zu groß: ehrlich als ersetzt
        var big = CodeAiKit.Diff("a\nb\nc", "x\ny\nz", maxCells: 4);
        Assert.Equal("---+++", Ops(big));

        var lines = Enumerable.Range(1, 20).Select(i => "Zeile " + i).ToList();
        var changed = lines.ToList(); changed[1] = "Zeile zwei"; changed.Insert(15, "Neu");
        var u = CodeAiKit.Unified(CodeAiKit.Diff(string.Join("\n", lines), string.Join("\n", changed)), "src/a.txt");
        Assert.StartsWith("--- a/src/a.txt\n+++ b/src/a.txt\n@@ -1,5 +1,5 @@\n Zeile 1\n-Zeile 2\n+Zeile zwei\n Zeile 3\n", u);
        Assert.Contains("@@ -13,6 +13,7 @@\n Zeile 13\n Zeile 14\n Zeile 15\n+Neu\n Zeile 16\n", u);
        Assert.Equal("", CodeAiKit.Unified(CodeAiKit.Diff("a", "a"), "x"));
        Assert.StartsWith("--- a/n\n+++ b/n\n@@ -0,0 +1,2 @@\n+x\n+y\n", CodeAiKit.Unified(CodeAiKit.Diff("", "x\ny"), "n"));
        Assert.StartsWith("--- /dev/null\n+++ b/n\n", CodeAiKit.Unified(CodeAiKit.Diff("", "x"), "n", added: true));
        Assert.StartsWith("--- a/n\n+++ /dev/null\n@@ -1,1 +0,0 @@\n-x\n", CodeAiKit.Unified(CodeAiKit.Diff("x", ""), "n", removed: true));
        var root = Path.Combine(Path.GetTempPath(), "proj");
        Assert.True(CodeAiKit.InsideNoLinks(root, Path.Combine(root, "a", "b.cs")));
        Assert.False(CodeAiKit.InsideNoLinks(root, Path.Combine(root, "..", "fremd.cs")));
        Assert.False(CodeAiKit.InsideNoLinks(root, root + "x" + Path.DirectorySeparatorChar + "a.cs"));
    }

    // ---------------- Bilder erkennen ----------------

    static byte[] Solid(int w, int h, byte r, byte g, byte b)
    {
        var a = new byte[w * h * 4];
        for (var i = 0; i < w * h; i++) { a[i * 4] = r; a[i * 4 + 1] = g; a[i * 4 + 2] = b; a[i * 4 + 3] = 255; }
        return a;
    }

    static byte[] Checker(int w, int h, int cell)
    {
        var a = new byte[w * h * 4];
        for (var y = 0; y < h; y++) for (var x = 0; x < w; x++) { var v = (byte)(((x / cell) + (y / cell)) % 2 == 0 ? 255 : 0); var p = (y * w + x) * 4; a[p] = a[p + 1] = a[p + 2] = v; a[p + 3] = 255; }
        return a;
    }

    static byte[] Blur(byte[] src, int w, int h, int r, int times)
    {
        var a = (byte[])src.Clone();
        for (var t = 0; t < times; t++)
        {
            var b = new byte[a.Length];
            for (var y = 0; y < h; y++)
                for (var x = 0; x < w; x++)
                {
                    int sum = 0, n = 0;
                    for (var dy = -r; dy <= r; dy++) for (var dx = -r; dx <= r; dx++) { int xx = Math.Clamp(x + dx, 0, w - 1), yy = Math.Clamp(y + dy, 0, h - 1); sum += a[(yy * w + xx) * 4]; n++; }
                    var p = (y * w + x) * 4; b[p] = b[p + 1] = b[p + 2] = (byte)(sum / n); b[p + 3] = 255;
                }
            a = b;
        }
        return a;
    }

    [Fact(DisplayName = "Bilder erkennen: einfarbig, Schachbrett scharf gegen weich, dunkel/hell, Hauptfarben mit festem Startwert, Grenzfälle")]
    public void Pixels()
    {
        var red = Solid(40, 30, 200, 30, 30);
        var c = VisionKit.MainColors(red, 40, 30);
        Assert.Single(c); Assert.Equal("#C81E1E", c[0].Hex); Assert.Equal(1, c[0].Share);
        Assert.Equal(VisionKit.Flat, VisionKit.Sharp(red, 40, 30).Verdict);
        Assert.Equal(0, VisionKit.Sharp(red, 40, 30).Variance);

        var sharp = Checker(64, 64, 16); var soft = Blur(sharp, 64, 64, 4, 3);
        var vs = VisionKit.Sharp(sharp, 64, 64); var vb = VisionKit.Sharp(soft, 64, 64);
        Assert.Equal(VisionKit.SharpOk, vs.Verdict); Assert.Equal(VisionKit.Blurry, vb.Verdict);
        Assert.True(vs.Variance > 20 * vb.Variance, $"{vs.Variance} gegen {vb.Variance}");
        Assert.Equal(VisionKit.Tiny, VisionKit.Sharp(Solid(2, 2, 0, 0, 0), 2, 2).Verdict);

        var dark = Solid(20, 20, 20, 25, 30);
        Assert.Equal(VisionKit.TooDark, VisionKit.Exposure(dark, 20, 20).Verdict);
        Assert.Equal(VisionKit.TooBright, VisionKit.Exposure(Solid(20, 20, 250, 250, 250), 20, 20).Verdict);
        var mid = VisionKit.Exposure(Checker(20, 20, 2), 20, 20);
        Assert.Equal(VisionKit.Ok, mid.Verdict); Assert.Equal(127.5, mid.Mean, 0); Assert.Equal(0.5, mid.DarkShare);

        // zwei Farben 3:1, durchsichtige Bildpunkte zählen nicht, gleiches Ergebnis bei gleichem Startwert
        var two = new byte[100 * 4 * 4];
        for (var i = 0; i < 400; i++) { var p = i * 4; var blue = i % 4 == 0; two[p] = (byte)(blue ? 0 : 255); two[p + 1] = (byte)(blue ? 0 : 255); two[p + 2] = 255; two[p + 3] = 255; }
        two[3] = 0; // ein durchsichtiger
        var k = VisionKit.MainColors(two, 100, 4, 3);
        Assert.Equal(2, k.Count); Assert.Equal("#FFFFFF", k[0].Hex); Assert.Equal("#0000FF", k[1].Hex);
        Assert.Equal(0.752, k[0].Share, 2);
        Assert.Equal(k, VisionKit.MainColors(two, 100, 4, 3));
        var photo = Blur(Checker(48, 48, 6), 48, 48, 1, 1);
        Assert.Equal(VisionKit.MainColors(photo, 48, 48, 5, seed: 7), VisionKit.MainColors(photo, 48, 48, 5, seed: 7));
        Assert.Empty(VisionKit.MainColors(new byte[16], 2, 2)); // alles durchsichtig
        Assert.Throws<ArgumentException>(() => VisionKit.Sharp(new byte[3], 2, 2));
    }

    /// <summary>JPEG mit APP1-EXIF (Big Endian): Hersteller, Modell, Belichtung, Blende, ISO, Brennweite, Aufnahmedatum.</summary>
    static byte[] ExifJpeg()
    {
        var t = new List<byte>();
        void U16(int v) { t.Add((byte)(v >> 8)); t.Add((byte)v); }
        void U32(long v) { t.Add((byte)(v >> 24)); t.Add((byte)(v >> 16)); t.Add((byte)(v >> 8)); t.Add((byte)v); }
        var make = Encoding.ASCII.GetBytes("Beispiel\0"); var model = Encoding.ASCII.GetBytes("Beispiel K10\0"); var date = Encoding.ASCII.GetBytes("2026:07:15 18:30:05\0");
        // Aufbau: Kopf 8, IFD0 mit 3 Einträgen (2+36+4=42) ab 8 → Daten ab 50
        const int ifd0 = 8, ifd0Len = 2 + 3 * 12 + 4, data0 = ifd0 + ifd0Len;
        var makeAt = data0; var modelAt = makeAt + make.Length; var exifIfd = modelAt + model.Length;
        const int exifLen = 2 + 5 * 12 + 4; var exifData = exifIfd + exifLen;
        var dateAt = exifData; var expAt = dateAt + date.Length; var fAt = expAt + 8; var focAt = fAt + 8;
        t.AddRange("MM"u8.ToArray()); U16(42); U32(ifd0);
        U16(3);
        U16(0x010F); U16(2); U32(make.Length); U32(makeAt);
        U16(0x0110); U16(2); U32(model.Length); U32(modelAt);
        U16(0x8769); U16(4); U32(1); U32(exifIfd);
        U32(0);
        t.AddRange(make); t.AddRange(model);
        U16(5);
        U16(0x829A); U16(5); U32(1); U32(expAt);
        U16(0x829D); U16(5); U32(1); U32(fAt);
        U16(0x8827); U16(3); U32(1); U16(200); U16(0);
        U16(0x9003); U16(2); U32(date.Length); U32(dateAt);
        U16(0x920A); U16(5); U32(1); U32(focAt);
        U32(0);
        t.AddRange(date); U32(1); U32(250); U32(28); U32(10); U32(35); U32(1);
        var app1 = new List<byte> { 0xFF, 0xD8, 0xFF, 0xE1 };
        var len = 2 + 6 + t.Count; app1.Add((byte)(len >> 8)); app1.Add((byte)len);
        app1.AddRange("Exif\0\0"u8.ToArray()); app1.AddRange(t);
        app1.AddRange(new byte[] { 0xFF, 0xDA, 0x00, 0x02, 0xFF, 0xD9 });
        return [.. app1];
    }

    [Fact(DisplayName = "Bilder erkennen: Format aus den ersten Bytes, EXIF mit Kamera/Belichtung/Datum, kaputte Daten ohne Absturz, Bericht als Markdown und CSV")]
    public void Facts()
    {
        Assert.Equal("PNG", VisionKit.Format(Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==")));
        Assert.Equal("GIF", VisionKit.Format("GIF89a......"u8));
        Assert.Equal("WebP", VisionKit.Format("RIFF\0\0\0\0WEBPVP8 "u8));
        Assert.Equal("HEIC", VisionKit.Format("\0\0\0\u0018ftypheic"u8));
        Assert.Equal("unbekannt", VisionKit.Format("hallo"u8));

        var jpg = ExifJpeg();
        Assert.Equal("JPEG", VisionKit.Format(jpg));
        var e = VisionKit.Exif(jpg)!;
        Assert.Equal(new DateTime(2026, 7, 15, 18, 30, 5), e.Taken);
        Assert.Equal("Beispiel K10", e.Camera); // Hersteller steht schon im Modell
        Assert.Equal("1/250 s", e.Exposure); Assert.Equal("f/2.8", e.FNumber); Assert.Equal(200, e.Iso); Assert.Equal("35 mm", e.Focal);
        Assert.Null(VisionKit.Exif(Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==")));
        // abgeschnitten und verfälscht: nie Absturz
        for (var cut = 0; cut < jpg.Length; cut += 7) _ = VisionKit.Exif(jpg[..cut]);
        var broken = (byte[])jpg.Clone(); for (var i = 30; i < broken.Length; i += 5) broken[i] ^= 0xA5; _ = VisionKit.Exif(broken);

        var px = Checker(32, 32, 4);
        var r = VisionKit.Analyze(@"C:\Bilder\=Urlaub; Strand.jpg", jpg, 2048, e, px, 32, 32, 4000, 3000);
        Assert.Equal("JPEG", r.Format); Assert.Equal(4000, r.Width);
        var dim = VisionKit.Analyze(@"C:\Bilder\dunkel.jpg", jpg, 1024, null, Solid(10, 10, 10, 10, 10), 10, 10, 10, 10);
        Assert.True(VisionKit.Problem(dim)); Assert.False(VisionKit.Problem(r));
        var md = VisionKit.Markdown([r, dim], "Bildbericht");
        Assert.Contains("2 Bilder geprüft, 1 auffällig", md);
        Assert.Contains("- Aufgenommen: 15.07.2026 18:30", md);
        Assert.Contains("- Kamera: Beispiel K10", md);
        Assert.Contains("zu dunkel", md);
        var csv = VisionKit.Csv([r, dim]).Split("\r\n");
        Assert.StartsWith("Datei;Format;Breite;Höhe", csv[0]);
        Assert.StartsWith("\"'=Urlaub; Strand.jpg\";JPEG;4000;3000;2;2026-07-15 18:30;Beispiel K10;", csv[1]); // Formel entschärft, Semikolon in Anführungszeichen
    }

    // ---------------- Recherche mit Quellen ----------------

    static readonly CiteKit.Source Book = new() { Id = "b", Kind = "buch", Authors = "Muster, Erika; Beispiel, Max", Title = "Gärten für Einsteiger", Year = "2024", Publisher = "Grünverlag", Place = "München", Edition = "2" };
    static readonly CiteKit.Source Article = new() { Id = "a", Kind = "artikel", Authors = "Erika Muster", Title = "Boden und Klima", Year = "2023", Journal = "Zeitschrift für Gartenbau", Volume = "12", Issue = "3", Pages = "45-67", Doi = "https://doi.org/10.1234/zfg.2023.45" };
    static readonly CiteKit.Source Web = new() { Id = "w", Kind = "webseite", Authors = "{Umweltbundesamt}", Title = "Wasser sparen im Garten", Year = "2025", Journal = "umweltbundesamt.de", Url = "https://www.umweltbundesamt.de/wasser", Accessed = "2026-11-03" };

    [Fact(DisplayName = "Quellen: APA 7, Harvard und DIN ISO 690 für Buch, Artikel und Webseite mit erwartetem Text")]
    public void Styles()
    {
        Assert.Equal("Muster, E., & Beispiel, M. (2024). *Gärten für Einsteiger* (2. Aufl.). Grünverlag.", CiteKit.Format(Book, "apa"));
        Assert.Equal("Muster, E. und Beispiel, M. (2024) *Gärten für Einsteiger*. 2. Aufl. München: Grünverlag.", CiteKit.Format(Book, "harvard"));
        Assert.Equal("MUSTER, Erika und Max BEISPIEL, 2024. *Gärten für Einsteiger*. 2. Aufl. München: Grünverlag.", CiteKit.Format(Book, "din"));

        Assert.Equal("Muster, E. (2023). Boden und Klima. *Zeitschrift für Gartenbau*, *12*(3), 45–67. https://doi.org/10.1234/zfg.2023.45", CiteKit.Format(Article, "apa"));
        Assert.Equal("Muster, E. (2023) „Boden und Klima“, *Zeitschrift für Gartenbau*, 12(3), S. 45–67. doi: 10.1234/zfg.2023.45.", CiteKit.Format(Article, "harvard"));
        Assert.Equal("MUSTER, Erika, 2023. Boden und Klima. *Zeitschrift für Gartenbau*. 12(3), S. 45–67. DOI: 10.1234/zfg.2023.45.", CiteKit.Format(Article, "din"));

        Assert.Equal("Umweltbundesamt (2025). *Wasser sparen im Garten*. umweltbundesamt.de. Abgerufen am 3. November 2026, von https://www.umweltbundesamt.de/wasser", CiteKit.Format(Web, "apa"));
        Assert.Equal("Umweltbundesamt (2025) *Wasser sparen im Garten*. Verfügbar unter: https://www.umweltbundesamt.de/wasser (Zugriff: 3. November 2026).", CiteKit.Format(Web, "harvard"));
        Assert.Equal("Umweltbundesamt, 2025. *Wasser sparen im Garten* [online]. umweltbundesamt.de. [Zugriff am: 3. November 2026]. Verfügbar unter: https://www.umweltbundesamt.de/wasser", CiteKit.Format(Web, "din"));

        // ohne Jahr, ohne Person, drei und mehr Personen, Titel mit Fragezeichen
        var noYear = Book with { Year = "", Authors = "", Title = "Wohin mit dem Laub?" };
        Assert.Equal("*Wohin mit dem Laub?* (2. Aufl.). (o. J.). Grünverlag.", CiteKit.Format(noYear, "apa"));
        var three = Book with { Authors = "Muster, Erika; Beispiel, Max; Probe, Karla Maria", Edition = "" };
        Assert.Equal("Muster, E., Beispiel, M., & Probe, K. M. (2024). *Gärten für Einsteiger*. Grünverlag.", CiteKit.Format(three, "apa"));
        Assert.Equal("MUSTER, Erika, Max BEISPIEL und Karla Maria PROBE, 2024. *Gärten für Einsteiger*. München: Grünverlag.", CiteKit.Format(three, "din"));
        Assert.StartsWith("Muster, E. et al. (2024)", CiteKit.Format(three with { Authors = three.Authors + "; Test, Tom" }, "harvard"));
        Assert.Equal("H.-P. J.", CiteKit.Initials("Hans-Peter Jan"));
        Assert.Equal("Muster, E., & Beispiel, M. (2024). Gärten für Einsteiger (2. Aufl.). Grünverlag.", CiteKit.Plain(CiteKit.Format(Book, "apa")));

        Assert.Equal("(Muster & Beispiel, 2024, S. 12–14)", CiteKit.InText(Book, "apa", "12-14"));
        Assert.Equal("(Muster und Beispiel 2024, S. 12)", CiteKit.InText(Book, "harvard", "12"));
        Assert.Equal("(MUSTER und BEISPIEL 2024)", CiteKit.InText(Book, "din"));
        Assert.Equal("(Muster et al., 2024)", CiteKit.InText(three, "apa"));
        Assert.Equal("(Umweltbundesamt, 2025)", CiteKit.InText(Web, "apa"));
        Assert.Equal("„Wasser ist knapp.“ (Umweltbundesamt, 2025, S. 3)", CiteKit.QuoteLine(new("z", "w", " Wasser ist knapp. ", "3"), Web, "apa"));
    }

    [Fact(DisplayName = "Quellen: Personen mit Komma, ohne Komma, mit „von“ und als Einrichtung; Verzeichnis deutsch sortiert; fehlende Pflichtfelder je Stil")]
    public void People_sort_missing()
    {
        Assert.Equal([new CiteKit.Person("von Goethe", "Johann Wolfgang"), new CiteKit.Person("Muster", "Erika")], CiteKit.People("Johann Wolfgang von Goethe und Erika Muster"));
        Assert.Equal([new CiteKit.Person("Bundesamt für Bau und Raum", "")], CiteKit.People("{Bundesamt für Bau und Raum}"));
        Assert.Equal(3, CiteKit.People("Muster, Erika; {Amt A und B}; Platon").Count);

        var aebel = Book with { Id = "x", Authors = "Äbel, Anna", Title = "Zwiebeln" };
        Assert.Equal(["x", "a", "b", "w"], CiteKit.Sorted([Web, Book, aebel, Article]).Select(s => s.Id));
        var md = CiteKit.Markdown([Book, Web], [new("q1", "b", "Erde braucht Zeit.", "12", "Kapitel 2"), new("q2", "fremd", "ohne Quelle", "1")], "apa");
        Assert.Contains("Zitierstil: APA 7", md);
        Assert.Contains("- Muster, E., & Beispiel, M. (2024).", md);
        Assert.Contains("> „Erde braucht Zeit.“ (Muster & Beispiel, 2024, S. 12)\n\nKapitel 2", md);
        Assert.DoesNotContain("ohne Quelle", md);

        Assert.Empty(CiteKit.Missing(Book, "din"));
        Assert.Contains("Ort", CiteKit.Missing(Book with { Place = "" }, "din"));
        Assert.DoesNotContain("Ort", CiteKit.Missing(Book with { Place = "" }, "apa"));
        Assert.Contains("Abrufdatum", CiteKit.Missing(Web with { Accessed = "" }, "harvard"));
        Assert.DoesNotContain("Abrufdatum", CiteKit.Missing(Web with { Accessed = "" }, "apa"));
        Assert.Contains("Jahrgang (Band)", CiteKit.Missing(Article with { Volume = "" }, "apa"));
        var bad = CiteKit.Missing(new CiteKit.Source { Kind = "webseite", Year = "24", Doi = "abc", Url = "www.example.org", Accessed = "gestern" }, "apa");
        Assert.Equal(["Titel", "Jahr als vierstellige Zahl", "Abrufdatum als TT.MM.JJJJ", "DOI im Format 10.xxxx/…", "Adresse mit https://"], bad);
        Assert.Empty(CiteKit.Missing(Web with { Accessed = "03.11.2026" }, "din"));
    }

    [Fact(DisplayName = "Quellen: Doppelte über DOI, Adresse (www, Schrägstrich, Anker, utm) und Titel mit Umlauten")]
    public void Duplicates()
    {
        CiteKit.Source S(string id, string title = "", string year = "", string doi = "", string url = "") => new() { Id = id, Title = title, Year = year, Doi = doi, Url = url };
        var list = new[]
        {
            S("1", "Eins", doi: "10.1234/ABC"), S("2", "Zwei", doi: "https://doi.org/10.1234/abc"),
            S("3", "Drei", url: "http://www.example.org/seite/?utm_source=x&id=5#oben"), S("4", "Vier", url: "https://example.org/seite?id=5"),
            S("5", "Über Gärten!", "2020"), S("6", "uber garten", ""), S("7", "Über Gärten", "2021"),
            S("8", "Ganz anders", "2020"),
        };
        var g = CiteKit.Duplicates(list);
        Assert.Equal(3, g.Count);
        Assert.Equal(["1", "2"], g[0].Select(s => s.Id));
        Assert.Equal(["3", "4"], g[1].Select(s => s.Id));
        Assert.Equal(["5", "6", "7"], g[2].Select(s => s.Id)); // 6 ohne Jahr verbindet 5 und 7
        Assert.Empty(CiteKit.Duplicates([S("a", "Titel A", "2020"), S("b", "Titel A", "2021")]));
        Assert.Equal("example.org/a?b=2&c=1", CiteKit.NormUrl("HTTPS://WWW.Example.org/a/?c=1&b=2&fbclid=x"));
    }

    [Fact(DisplayName = "Quellen: BibTeX-Rundlauf mit Umlauten, Sonderzeichen und Einrichtung; Adressen bleiben wörtlich")]
    public void BibTex_roundtrip()
    {
        var book = Book with { Authors = "Müller, Jürgen; {Bundesamt für Bau und Raum}", Title = "Straßen & Wege: 100 % sicher {neu} $5 #1 a_b \\ c" };
        var web = Web with { Url = "https://example.org/~erika/seite_1?x=1&y=2--3", Authors = "Muster, Erika" };
        var bib = CiteKit.BibTex([book, Article, web]);
        Assert.Contains("@book{mueller2024strassen,", bib);
        Assert.Contains("author = {Müller, Jürgen and {Bundesamt für Bau und Raum}}", bib);
        Assert.Contains("title = {Straßen \\& Wege: 100 \\% sicher \\{neu\\} \\$5 \\#1 a\\_b \\textbackslash{} c}", bib);
        Assert.Contains("pages = {45--67}", bib);
        Assert.Contains("url = {https://example.org/~erika/seite_1?x=1&y=2--3}", bib);
        Assert.Contains("urldate = {2026-11-03}", bib);
        var (back, warn) = CiteKit.ParseBibTex(bib);
        Assert.Empty(warn);
        Assert.Equal(3, back.Count);
        foreach (var (orig, got) in new[] { book, Article, web }.Zip(back))
        {
            Assert.Equal(orig.Kind, got.Kind); Assert.Equal(CiteKit.People(orig.Authors), CiteKit.People(got.Authors)); Assert.Equal(orig.Title, got.Title); Assert.Equal(orig.Year, got.Year);
            Assert.Equal(orig.Publisher, got.Publisher); Assert.Equal(orig.Place, got.Place); Assert.Equal(orig.Edition, got.Edition); Assert.Equal(orig.Journal, got.Journal);
            Assert.Equal(orig.Volume, got.Volume); Assert.Equal(orig.Issue, got.Issue); Assert.Equal(CiteKit.PageRange(orig.Pages), got.Pages);
            Assert.Equal(orig.Url, got.Url); Assert.Equal(CiteKit.NormDoi(orig.Doi), got.Doi); Assert.Equal(orig.Accessed, got.Accessed);
        }
        Assert.Equal(CiteKit.Format(book, "apa"), CiteKit.Format(back[0], "apa"));
        // gleiche Schlüssel bekommen Buchstaben
        var keys = System.Text.RegularExpressions.Regex.Matches(CiteKit.BibTex([Book, Book, Book]), @"@book\{([^,]+),").Select(m => m.Groups[1].Value).ToList();
        Assert.Equal(3, keys.Distinct().Count());
    }

    [Fact(DisplayName = "Quellen: BibTeX-Import robust – @string, #, LaTeX-Akzente, Anführungszeichen, kaputter Eintrag wird übersprungen, misc mit Adresse")]
    public void BibTex_import()
    {
        const string bib = """
            Ein Kommentar außerhalb mit mail@example.org
            @comment{ignorieren {auch das}}
            @string{verlag = "Grünverlag"}
            @Book{a1,
              author = {M{\"u}ller, J{\"o}rg and Anna Stra{\ss}e},
              title = "{\"U}ber die {DNA} der Stadt",
              year = 2019,
              publisher = verlag # " GmbH",
              address = {K{\"o}ln},
            }
            @article{kaputt, title = {Ohne Ende, year = 2020
            @article{b2, author={Beispiel, Max and Fran\c{c}ois Dupont}, title={Kurz \& b\"undig}, journal={Hefte}, year={2021}, pages={1--10}, volume = 4}
            @misc{c3, title={Startseite}, howpublished={\url{https://example.org/start}}, urldate={2026-11-01}}
            @book{leer}
            """;
        var (s, warn) = CiteKit.ParseBibTex(bib);
        Assert.Single(warn); Assert.Contains("Eintrag 2 übersprungen", warn[0]);
        Assert.Equal(4, s.Count);
        Assert.Equal("Müller, Jörg; Straße, Anna", s[0].Authors);
        Assert.Equal("Über die DNA der Stadt", s[0].Title);
        Assert.Equal(("2019", "Grünverlag GmbH", "Köln", "buch"), (s[0].Year, s[0].Publisher, s[0].Place, s[0].Kind));
        Assert.Equal(("artikel", "Kurz & bündig", "Hefte", "1–10", "4"), (s[1].Kind, s[1].Title, s[1].Journal, s[1].Pages, s[1].Volume));
        Assert.Equal("Beispiel, Max; Dupont, François", s[1].Authors);
        Assert.Equal(("webseite", "https://example.org/start", "2026-11-01"), (s[2].Kind, s[2].Url, s[2].Accessed));
        Assert.Equal("", s[3].Title);
        Assert.Equal("ä é ß ø – x", CiteKit.Unlatex(@"\""a \'{e} {\ss} \o{} -- {x}"));
        Assert.Single(CiteKit.ParseBibTex(new string('x', CiteKit.MaxBibChars + 1)).Warnings);
        Assert.Empty(CiteKit.ParseBibTex("").Sources);
    }

    // ---------------- Tagesplaner ----------------

    static readonly DateTime Mon = new(2026, 11, 16);
    static readonly DayPlanner.Options Std = new(TimeSpan.FromHours(8), TimeSpan.FromHours(17));
    static DayPlanner.Appointment A(string t, int h1, int m1, int h2, int m2) => new(t, t, Mon.AddHours(h1).AddMinutes(m1), Mon.AddHours(h2).AddMinutes(m2));
    static string Show(DayPlanner.Plan p) => string.Join(" | ", p.Blocks.Select(b => $"{b.Start:HH:mm}-{b.End:HH:mm} {b.Title}"));

    [Fact(DisplayName = "Tagesplaner: Reihenfolge nach Frist und Wichtigkeit, Lücken füllen, Pause nach 90 min, überlappende Termine, nichts über einem Termin")]
    public void Plan()
    {
        var items = new[]
        {
            new DayPlanner.Item("b", "Steuerunterlagen", 120, 2),
            new DayPlanner.Item("c", "Einkaufsliste", 15, 3),
            new DayPlanner.Item("d", "Telefonat", 30, 1),
            new DayPlanner.Item("a", "Angebot schreiben", 90, 2, Mon),
            new DayPlanner.Item("x", "Schon erledigt", 60, 1, Done: true),
        };
        Assert.Equal(["a", "d", "b", "c"], DayPlanner.Order(items).Select(i => i.Id));
        var p = DayPlanner.Make(Mon, items, [A("Teamrunde", 10, 0, 10, 30), A("Zahnarzt", 14, 0, 15, 0), A("Rückruf", 14, 30, 15, 15)], Std);
        Assert.Equal("08:00-09:30 Angebot schreiben | 09:30-09:40 Pause | 09:40-09:55 Einkaufsliste | 10:00-10:30 Teamrunde | 10:30-11:00 Telefonat | 11:00-11:10 Pause | 11:10-13:10 Steuerunterlagen | 14:00-15:00 Zahnarzt | 14:30-15:15 Rückruf", Show(p));
        Assert.Empty(p.Tomorrow);
        var (planned, breaks, free) = DayPlanner.Summary(p, Std);
        Assert.Equal((255, 20, 540 - 30 - 75 - 255 - 20), (planned, breaks, free));
        var text = DayPlanner.Text(p, Std);
        Assert.Contains("Tagesplan für Montag, 16. November 2026", text);
        Assert.Contains("10:00–10:30  Termin: Teamrunde", text);
        Assert.Contains("09:30–09:40  Pause", text);
    }

    [Fact(DisplayName = "Tagesplaner: Grenzfälle – Tagesende, Termin über das Ende, ganzer Tag belegt, zu lange Aufgabe, ab jetzt, keine Pausen, falsche Arbeitszeit")]
    public void Edges()
    {
        var late = new DayPlanner.Options(TimeSpan.FromHours(16), TimeSpan.FromHours(17));
        var p = DayPlanner.Make(Mon, [new("a", "Halbe Stunde", 30), new("b", "Zehn Minuten", 10)], [A("Abendtermin", 16, 30, 18, 0)], late);
        Assert.Equal("16:00-16:30 Halbe Stunde | 16:30-18:00 Abendtermin", Show(p)); // genau bis zum Termin, nichts danach
        Assert.Equal("b", Assert.Single(p.Tomorrow).Item.Id);
        Assert.Equal("passt heute nicht mehr", p.Tomorrow[0].Why);

        var exact = DayPlanner.Make(Mon, [new("a", "Bis Feierabend", 60)], [], late);
        Assert.Equal(Mon.AddHours(17), exact.Blocks.Single().End);

        var full = DayPlanner.Make(Mon, [new("a", "A", 15)], [A("Seminar", 7, 0, 18, 0)], Std);
        Assert.Equal("länger als jede freie Zeit heute", Assert.Single(full.Tomorrow).Why);
        var tooLong = DayPlanner.Make(Mon, [new("a", "Riesig", 600), new("b", "Ohne Dauer", 0)], [], Std);
        Assert.Equal(["b", "a"], tooLong.Tomorrow.Select(t => t.Item.Id));
        Assert.Equal("Dauer fehlt oder ist zu lang", tooLong.Tomorrow[0].Why);

        var now = DayPlanner.Make(Mon, [new("a", "Jetzt", 20)], [], Std, notBefore: Mon.AddHours(9).AddMinutes(7));
        Assert.Equal(Mon.AddHours(9).AddMinutes(10), now.Blocks.Single().Start);
        Assert.Empty(DayPlanner.Make(Mon, [new("a", "x", 20)], [], Std, notBefore: Mon.AddDays(1)).Blocks);
        Assert.Equal(Mon.AddHours(8), DayPlanner.Make(Mon, [new("a", "x", 20)], [], Std, notBefore: Mon.AddDays(-1)).Blocks.Single().Start);

        var noBreak = DayPlanner.Make(Mon, [new("a", "A", 60), new("b", "B", 60), new("c", "C", 60)], [], Std with { BreakMinutes = 0 });
        Assert.Equal("08:00-09:00 A | 09:00-10:00 B | 10:00-11:00 C", Show(noBreak));
        // kurzer Termin (5 min) ist keine Pause: danach trotzdem Pause, wenn die 90 min voll sind
        var shortAppt = DayPlanner.Make(Mon, [new("a", "A", 60), new("b", "B", 60)], [A("Kurz", 9, 0, 9, 5)], Std);
        Assert.Equal("08:00-09:00 A | 09:00-09:05 Kurz | 09:05-09:15 Pause | 09:15-10:15 B", Show(shortAppt));
        // Termin über Mitternacht am Vortag wird abgeschnitten
        var night = DayPlanner.Make(Mon, [], [new("n", "Nacht", Mon.AddHours(-2), Mon.AddHours(9))], Std);
        Assert.Equal(Mon, night.Blocks.Single().Start);

        Assert.Throws<ArgumentException>(() => DayPlanner.Make(Mon, [], [], new(TimeSpan.FromHours(17), TimeSpan.FromHours(8))));
        Assert.Throws<ArgumentException>(() => DayPlanner.Make(Mon, [], [], Std with { WorkBeforeBreak = 5 }));
        Assert.Equal([new DayPlanner.Item("a", "x", 5, 2, null, false)], DayPlanner.Carry([new("a", "x", 5), new("b", "y", 5, Done: true)]));
    }

    [Fact(DisplayName = "Tagesplaner: 300 zufällige Tage – kein Block überlappt einen Termin oder einen anderen Block, alles im Arbeitsfenster, jede Aufgabe genau einmal, Pausenregel")]
    public void Plan_properties()
    {
        var rnd = new Random(7);
        for (var run = 0; run < 300; run++)
        {
            var o = new DayPlanner.Options(TimeSpan.FromMinutes(rnd.Next(6 * 60, 10 * 60)), TimeSpan.FromMinutes(rnd.Next(14 * 60, 20 * 60)), rnd.Next(30, 120), rnd.Next(0, 3) * 10);
            var items = Enumerable.Range(0, rnd.Next(0, 12)).Select(i => new DayPlanner.Item("t" + i, "T" + i, rnd.Next(5, 200), rnd.Next(1, 4), rnd.Next(3) == 0 ? Mon.AddDays(rnd.Next(-2, 5)) : null)).ToList();
            var appts = Enumerable.Range(0, rnd.Next(0, 6)).Select(i => { var s = rnd.Next(5 * 60, 21 * 60); return new DayPlanner.Appointment("a" + i, "A" + i, Mon.AddMinutes(s), Mon.AddMinutes(s + rnd.Next(5, 150))); }).ToList();
            var p = DayPlanner.Make(Mon, items, appts, o);
            var own = p.Blocks.Where(b => b.Kind != DayPlanner.Fixed).OrderBy(b => b.Start).ToList();
            foreach (var b in own)
            {
                Assert.True(b.Start >= Mon + o.DayStart && b.End <= Mon + o.DayEnd, $"Lauf {run}: {b} außerhalb");
                Assert.DoesNotContain(appts, a => a.Start < b.End && a.End > b.Start);
            }
            for (var i = 1; i < own.Count; i++) Assert.True(own[i].Start >= own[i - 1].End, $"Lauf {run}: Überlappung");
            var placed = own.Where(b => b.Kind == DayPlanner.Work).Select(b => b.ItemId!).ToList();
            Assert.Equal(items.Select(i => i.Id).Order(), placed.Concat(p.Tomorrow.Select(t => t.Item.Id)).Order());
            foreach (var b in own.Where(b => b.Kind == DayPlanner.Work)) Assert.Equal(items.Single(i => i.Id == b.ItemId).Minutes, (int)(b.End - b.Start).TotalMinutes);
            if (o.BreakMinutes > 0)
            {
                // Arbeit am Stück (Lücken unter Pausenlänge zählen mit) überschreitet die Grenze nur mit einer einzelnen langen Aufgabe
                var work = own.Where(b => b.Kind == DayPlanner.Work).ToList(); var runMin = 0; var count = 0; DateTime? last = null;
                foreach (var b in work)
                {
                    var gapBlocks = own.Any(x => x.Kind == DayPlanner.Pause && last is not null && x.Start >= last && x.End <= b.Start);
                    var gapAppt = last is not null && appts.Any(a => a.Start >= last && a.End <= b.Start && (a.End - a.Start).TotalMinutes >= o.BreakMinutes);
                    if (last is null || gapBlocks || (b.Start - last.Value).TotalMinutes >= o.BreakMinutes || gapAppt) { runMin = 0; count = 0; }
                    runMin += (int)(b.End - b.Start).TotalMinutes; count++;
                    Assert.True(runMin <= o.WorkBeforeBreak || count == 1, $"Lauf {run}: {runMin} min am Stück");
                    last = b.End;
                }
            }
        }
    }

    [Fact(DisplayName = "Tagesplaner: ICS nach RFC 5545 – CRLF, Zeilen ≤ 75 Byte gefaltet, Sonderzeichen maskiert, von Ical.Net und dem Kalender lesbar")]
    public void Ics()
    {
        const string longTitle = "Präsentation für die Geschäftsführung vorbereiten; Zahlen, Diagramme \\ Anhänge – sehr ausführlich und gründlich überarbeiten";
        var p = DayPlanner.Make(Mon, [new("a", longTitle, 60), new("b", "Zweite", 60)], [A("Teamrunde", 11, 0, 11, 30)], Std with { WorkBeforeBreak = 60 });
        var ics = DayPlanner.Ics(p, new DateTime(2026, 11, 14, 7, 0, 0, DateTimeKind.Utc));
        Assert.StartsWith("BEGIN:VCALENDAR\r\nVERSION:2.0\r\nPRODID:-//Tagesplaner//DE\r\n", ics);
        Assert.EndsWith("END:VCALENDAR\r\n", ics);
        Assert.DoesNotContain("\n", ics.Replace("\r\n", ""));
        var phys = ics.Split("\r\n")[..^1];
        Assert.All(phys, l => Assert.True(Encoding.UTF8.GetByteCount(l) <= 75, l));
        Assert.Contains(phys, l => l.StartsWith(' ')); // gefaltet
        Assert.Equal(3, phys.Count(l => l == "BEGIN:VEVENT")); // zwei Aufgaben und eine Pause, Termin nur auf Wunsch
        Assert.Contains("DTSTART:20261116T080000", ics);
        Assert.Contains("DTSTAMP:20261114T070000Z", ics);
        var unfolded = ics.Replace("\r\n ", "");
        Assert.Contains("SUMMARY:Präsentation für die Geschäftsführung vorbereiten\\; Zahlen\\, Diagramme \\\\ Anhänge", unfolded);
        // Mehrbyte-Zeichen bleiben ganz
        Assert.DoesNotContain('\uFFFD', Encoding.UTF8.GetString(Encoding.UTF8.GetBytes(ics)));

        var cal = Ical.Net.Calendar.Load(ics)!;
        Assert.Equal(3, cal.Events.Count);
        Assert.Contains(cal.Events, e => e.Summary == longTitle);
        var own = Trendo.Core.Sys.Ics.Import(ics);
        Assert.Equal(longTitle, own.First(e => e.Start == Mon.AddHours(8)).Title);
        Assert.Equal(4, Ical.Net.Calendar.Load(DayPlanner.Ics(p, DateTime.UtcNow, withAppointments: true))!.Events.Count);
        Assert.Equal("a\\\\b\\;c\\,d\\ne", DayPlanner.Escape("a\\b;c,d\r\ne"));
        Assert.Equal("x", DayPlanner.Fold("x"));
    }

    [Fact(DisplayName = "Tagesplaner: Aufgaben und Uhrzeiten aus Text lesen")]
    public void Parse()
    {
        var l = DayPlanner.ParseItems("- Angebot schreiben, 90 min, hoch, bis 20.11.\nEinkauf 1,5 h niedrig\n\nAnruf\n,,,", Mon);
        Assert.Equal(3, l.Count);
        Assert.Equal(("Angebot schreiben", 90, 1, (DateTime?)new DateTime(2026, 11, 20)), (l[0].Title, l[0].Minutes, l[0].Importance, l[0].Due));
        Assert.Equal(("Einkauf", 90, 3), (l[1].Title, l[1].Minutes, l[1].Importance));
        Assert.Equal(("Anruf", 30, 2), (l[2].Title, l[2].Minutes, l[2].Importance));
        Assert.Equal(new TimeSpan(8, 30, 0), DayPlanner.ParseTime("8.30"));
        Assert.Equal(new TimeSpan(24, 0, 0), DayPlanner.ParseTime("24:00"));
        Assert.Null(DayPlanner.ParseTime("24:30")); Assert.Null(DayPlanner.ParseTime("9:75")); Assert.Null(DayPlanner.ParseTime("acht"));
    }

    // ---------------- Adapter ----------------

    [Fact(DisplayName = "Kreativ-Adapter Teil 4: ohne Nebenwirkung, Pflichtfelder, Prüfungen und echte Dateien im Projekt")]
    public void Adapter()
    {
        var all = PromptActions.CreateDefault();
        foreach (var id in new[] { "STUDIO-CODEAI-01", "STUDIO-VISION-01", "STUDIO-CITE-01", "STUDIO-DAYPLAN-01" })
        {
            var ad = all.Single(a => a.StudioId == id);
            Assert.False(ad.CanPerformSideEffect);
            Assert.Equal(SideEffectClass.None, ad.Effect);
            Assert.NotEmpty(ad.Missing(new Dictionary<string, string>()));
            Assert.Equal(CapabilityLevel.Partial, CapabilityCatalog.Level(id, all));
        }
        Assert.NotEmpty(all.Single(a => a.StudioId == "STUDIO-CODEAI-01").Validate(new Dictionary<string, string> { ["ordner"] = @"\\rechner\code" }));
        Assert.NotEmpty(all.Single(a => a.StudioId == "STUDIO-CITE-01").Validate(new Dictionary<string, string> { ["titel"] = "x", ["stil"] = "mla" }));
        Assert.NotEmpty(all.Single(a => a.StudioId == "STUDIO-DAYPLAN-01").Validate(new Dictionary<string, string> { ["aufgaben"] = "x", ["beginn"] = "acht" }));
        Assert.NotEmpty(all.Single(a => a.StudioId == "STUDIO-DAYPLAN-01").Validate(new Dictionary<string, string> { ["aufgaben"] = "x", ["termine"] = "irgendwann Zahnarzt" }));

        var root = Temp(); var code = Path.Combine(root, "projekt"); Directory.CreateDirectory(code); File.WriteAllText(Path.Combine(code, "go.mod"), "module x");
        var pics = Path.Combine(root, "bilder"); Directory.CreateDirectory(pics); File.WriteAllBytes(Path.Combine(pics, "foto.jpg"), ExifJpeg());
        var core = new TrendoCore(Path.Combine(root, "daten"), new InMemorySecretStore(), offline: true);
        var project = core.Projects.Create("probe", "Probe").Id;
        var wf = new StudioWorkflow(core, all);
        var ctx = new StudioExecutionContext(project, Principal.LocalOwner, CancellationToken.None, ResourceBudget.Default);
        string Run(string studio, string prompt, string file, string id)
        {
            var req = new StudioPromptRequest(id, project, studio, prompt, [], "de", null, PrivacyMode.Local, new Provenance("test", null, null));
            var done = wf.Los(project, wf.Start(req, ctx).RunId, ctx);
            Assert.Equal(RunStatus.Verified, done.Status);
            return File.ReadAllText(core.Projects.ResolveInProject(project, done.Artifacts.Single(a => a.RelativePath.EndsWith(file)).RelativePath));
        }
        try
        {
            Assert.Contains("go test ./...", Run("STUDIO-CODEAI-01", "Ordner: " + code, "projekt.md", "code-1"));
            var facts = Run("STUDIO-VISION-01", "Ordner: " + pics, "bildfakten.md", "vis-1");
            Assert.Contains("| foto.jpg | JPEG |", facts); Assert.Contains("15.07.2026 18:30", facts); Assert.Contains("Beispiel K10", facts);
            var cite = Run("STUDIO-CITE-01", "Titel: Gärten für Einsteiger\nAutorin: Muster, Erika\nJahr: 2024\nVerlag: Grünverlag\nStil: Harvard", "literatur.md", "cite-1");
            Assert.Contains("Muster, E. (2024) *Gärten für Einsteiger*. Grünverlag.", cite);
            Assert.Contains("Fehlt noch: Ort", cite);
            Assert.Contains("@book{", cite);
            var day = Run("STUDIO-DAYPLAN-01", "Tag: 16.11.2026\nAufgaben: Angebot schreiben 90 min hoch; Einkauf 30 min\nTermine: 10:00-10:30 Teamrunde\nBeginn: 08:00\nEnde: 12:00", "tagesplan.txt", "day-1");
            Assert.Contains("08:00–09:30  Angebot schreiben", day);
            Assert.Contains("10:00–10:30  Termin: Teamrunde", day);
            Assert.Contains("10:30–11:00  Einkauf", day);
        }
        finally
        {
            try { Directory.Delete(root, true); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        }
    }
}
