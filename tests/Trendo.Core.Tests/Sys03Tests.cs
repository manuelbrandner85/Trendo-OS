using Trendo.Core;
using Trendo.Core.Sys;
using Xunit;
namespace Trendo.Core.Tests;

public class Sys03_Netz
{
    static string Tmp() { var d = Path.Combine(Path.GetTempPath(), "trendo-s03-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(d); return d; }

    [Fact(DisplayName = "SYS-03-P06: Prüfsumme – Algorithmus erkannt, Abweichung gemeldet")]
    public void Hash()
    {
        var f = Path.Combine(Tmp(), "a.txt"); File.WriteAllText(f, "abc");
        Assert.True(Web.VerifyHash(f, "BA7816BF 8F01CFEA414140DE5DAE2223B00361A396177A9CB410FF61F20015AD").Match);
        Assert.True(Web.VerifyHash(f, "900150983cd24fb0d6963f7d28e17f72").Match);
        var (m, algo, _) = Web.VerifyHash(f, new string('0', 64)); Assert.False(m); Assert.Equal("SHA-256", algo);
        Assert.Throws<ArgumentException>(() => Web.VerifyHash(f, "123"));
    }

    [Fact(DisplayName = "SYS-03-P08: Lesezeichen – Chrome-HTML-Import mit Ordnern, Export/Import verlustfrei")]
    public void BookmarksTest()
    {
        var html = """
        <!DOCTYPE NETSCAPE-Bookmark-file-1>
        <DL><p>
        <DT><H3 ADD_DATE="1">Lesezeichenleiste</H3>
        <DL><p>
          <DT><A HREF="https://tagesschau.de/" ADD_DATE="1">Tagesschau &amp; Co</A>
          <DT><H3>Arbeit</H3>
          <DL><p><DT><A HREF="https://github.com/">GitHub</A></DL><p>
          <DT><A HREF="javascript:alert(1)">Böse</A>
        </DL><p>
        <DT><A HREF="https://example.org/">Ohne Ordner</A>
        </DL><p>
        """;
        var l = Bookmarks.ImportHtml(html);
        Assert.Equal(3, l.Count); Assert.Equal(new Bookmark("Tagesschau & Co", "https://tagesschau.de/", "Lesezeichenleiste"), l[0]);
        Assert.Equal("Lesezeichenleiste/Arbeit", l[1].Folder); Assert.Equal("", l[2].Folder);
        Assert.Equal(l, Bookmarks.ImportHtml(Bookmarks.ExportHtml(l)));
        var root = Tmp(); Bookmarks.Add(root, l[0]); Bookmarks.Add(root, l[0]); Assert.Single(Bookmarks.Load(root));
        Assert.Throws<ArgumentException>(() => Bookmarks.Add(root, new("x", "file:///c:/", "")));
    }

    [Fact(DisplayName = "SYS-03-P09/P22: Lesemodus ohne Skripte; Offline-Kopie ohne Skripte, mit Basis-Adresse")]
    public void Reader()
    {
        var html = "<html><head><title>Titel &amp; mehr</title><script>alert('x')</script></head><body><nav><p>Menü</p></nav><article><h1>Überschrift</h1><p>Erster <b>Absatz</b>.</p><script>document.write('<p>böse</p>')</script><ul><li>Punkt</li></ul></article><p onclick=\"x()\">Fuß</p></body></html>";
        Assert.Equal("Titel & mehr", Web.Title(html));
        var t = Web.ReaderText(html);
        Assert.Equal("## Überschrift\n\nErster Absatz.\n\n• Punkt", t); Assert.DoesNotContain("böse", t);
        var off = Web.OfflineCopy(html, new Uri("https://beispiel.de/artikel"));
        Assert.DoesNotContain("<script", off, StringComparison.OrdinalIgnoreCase); Assert.DoesNotContain("onclick", off);
        Assert.Contains("<base href=\"https://beispiel.de/artikel\">", off); Assert.Contains("script-src 'none'", off);
    }

    [Fact(DisplayName = "SYS-03-P10/P11/P12/P25: Tor nur offiziell, Privat-Parameter je Browser, Standardbrowser lesen, Verlauf-Anleitung")]
    public void Browser()
    {
        Assert.StartsWith("https://www.torproject.org/", Web.TorOfficial);
        Assert.Equal("--incognito", Web.PrivateArg("chrome")); Assert.Equal("--inprivate", Web.PrivateArg("edge")); Assert.Equal("--private-window", Web.PrivateArg("firefox")); Assert.Null(Web.PrivateArg("unbekannt"));
        Assert.Equal("Microsoft Edge", Web.BrowserForProgId("MSEdgeHTM")); Assert.Equal("Mozilla Firefox", Web.BrowserForProgId("FirefoxURL-308046B0AF4A39CB")); Assert.Equal("nicht festgelegt", Web.BrowserForProgId(null));
        Assert.All(Web.ClearHistory, c => Assert.True(c.Steps.Length >= 4));
        foreach (var (id, _, _) in Web.ClearHistory) Assert.NotNull(Web.PrivateArg(id));
    }

    [Fact(DisplayName = "SYS-03-P13: Suchmaschine gespeichert, Standard DuckDuckGo, wirkt auf die Suchadresse")]
    public void Engine()
    {
        var r = Tmp(); Assert.Equal("duckduckgo", Web.Engine(r));
        Web.SetEngine(r, "startpage"); Assert.StartsWith("https://www.startpage.com/", Web.SearchUrl(r));
        Assert.StartsWith("https://www.startpage.com/", Browsers.Normalize("kuchen rezept", Web.SearchUrl(r))!.AbsoluteUri);
        Assert.Throws<ArgumentException>(() => Web.SetEngine(r, "bing"));
    }

    [Fact(DisplayName = "SYS-03-P14: PDF-Befehl für Chrome/Edge (A4-Prüfung über pdfinfo)")]
    public void Pdf()
    {
        var a = Web.PrintToPdfArgs(new Uri("https://example.org/"), "/tmp/x.pdf");
        Assert.Contains("--print-to-pdf=/tmp/x.pdf", a); Assert.Contains(a, x => x.StartsWith("--headless"));
        Assert.True(Web.IsA4("Pages: 1\nPage size:      595.92 x 842.88 pts (A4)\n")); Assert.False(Web.IsA4("Page size: 612 x 792 pts (letter)"));
    }

    [Theory(DisplayName = "SYS-03-P15/P16: Warnzeichen bei Phishing-Adressen")]
    [InlineData("https://paypa1.com/login", "Ähnelt")]
    [InlineData("https://xn--pypal-4ve.com/", "Punycode")]
    [InlineData("http://sparkasse.de.konto-sicher.ru/", "gehört aber zu")]
    [InlineData("https://www.sparkasse.de@evil.example/", "„@“")]
    [InlineData("https://192.168.1.10/bank", "IP-Adresse")]
    [InlineData("https://bit.ly/abc", "Kurzlink")]
    public void Phishing(string url, string expect) => Assert.Contains(Web.PhishingHints(url), h => h.Contains(expect));

    [Fact(DisplayName = "SYS-03-P15/P16: Echte Adressen bleiben ohne Warnung; Ziel wird erklärt")]
    public void Clean()
    {
        Assert.Empty(Web.PhishingHints("https://www.paypal.com/de/home")); Assert.Empty(Web.PhishingHints("https://www.sparkasse.de/"));
        Assert.Equal("Ziel: www.dhl.de/de/privatkunden · verschlüsselt", Web.Explain(new Uri("https://www.dhl.de/de/privatkunden")));
    }

    [Fact(DisplayName = "SYS-03-P17: Formular-Hinweise (IBAN, Karte, Passwort, http) – nie Absenden")]
    public void Form()
    {
        var h = Web.FormHints("http://shop.example/kasse", "IBAN DE89 3704 0044 0532 0130 00, Karte 4111 1111 1111 1111, Passwort: x");
        Assert.Contains(h, x => x.Contains("unverschlüsselt")); Assert.Contains(h, x => x.Contains("IBAN")); Assert.Contains(h, x => x.Contains("Kartennummer")); Assert.Contains(h, x => x.Contains("Passwort"));
        Assert.Empty(Web.FormHints("https://www.example.org/", "Hallo, ich hätte gern einen Termin."));
        Assert.DoesNotContain(Web.FormHints("https://x.org", "Nummer 4111 1111 1111 1112"), x => x.Contains("Kartennummer"));
    }

    [Fact(DisplayName = "SYS-03-P18/P19/P20/P21/P24: Downloads sortieren + rückgängig, Quarantäne, Defender-Auswertung, Webnotiz, Proxy")]
    public void Misc()
    {
        var d = Tmp(); foreach (var n in new[] { "a.pdf", "b.jpg", "c.exe", "d.part", "e.xyz" }) File.WriteAllText(Path.Combine(d, n), n);
        var p = Web.SortPlan(d); Assert.Equal(4, p.Count); Assert.DoesNotContain(p, m => m.From.EndsWith(".part"));
        var u = Renamer.Apply(p, Path.Combine(d, "..", Path.GetFileName(d) + "-u"));
        Assert.True(File.Exists(Path.Combine(d, "Programme", "c.exe"))); Assert.True(File.Exists(Path.Combine(d, "Sonstiges", "e.xyz")));
        Renamer.Undo(u); Assert.True(File.Exists(Path.Combine(d, "c.exe")));
        var root = Tmp(); var q = Web.Quarantine(root, Path.Combine(d, "c.exe"));
        Assert.False(File.Exists(Path.Combine(d, "c.exe"))); Assert.EndsWith(".gesperrt", q.Stored); Assert.Equal(64, q.Sha256.Length);
        Web.Restore(root, q.Id); Assert.True(File.Exists(Path.Combine(d, "c.exe"))); Assert.Empty(Web.QuarantineList(root));
        Assert.Contains("-DisableRemediation", Web.DefenderArgs("x")); Assert.StartsWith("Keine", Web.DefenderResult(0, "")); Assert.Contains("Bedrohung gefunden", Web.DefenderResult(2, ""));
        Assert.StartsWith("🔗 https://a.de/", Web.WebNote(new Uri("https://a.de/"), " Merken "));
        Assert.StartsWith("Kein Proxy", Web.ProxyInfo(false, "x:1", null, null)); Assert.Contains("proxy:8080", Web.ProxyInfo(true, "proxy:8080", null, null));
    }

    [Fact(DisplayName = "SYS-03-P07: Fortsetzen-Zustand nur für dieselbe Adresse")]
    public void Resume()
    {
        var d = Tmp(); var t = Path.Combine(d, "f.bin");
        Assert.Equal(0, Web.ResumeFrom(t, "https://a/f.bin"));
        File.WriteAllBytes(Web.PartFile(t), new byte[100]); Web.SaveState(new("https://a/f.bin", t, 1000, null));
        Assert.Equal(100, Web.ResumeFrom(t, "https://a/f.bin")); Assert.Equal(0, Web.ResumeFrom(t, "https://b/f.bin"));
        Assert.Equal("datei name.zip", Web.SafeFileName(new Uri("https://x.de/pfad/datei%20name.zip?x=1")));
    }
}
