using Xunit;

namespace Trendo.Core.Tests;

/// <summary>Alle Studios, Runde 8 – CORE-10: Dokumente mit markitdown 0.1.9 in Markdown (Antwort in echter Form; Echtlauf erst nach Freigabe).</summary>
public class DocMarkdownTests : IDisposable
{
    readonly string _dir = Path.Combine(Path.GetTempPath(), "trendo-docmd-" + Guid.NewGuid().ToString("N"));
    public DocMarkdownTests() => Directory.CreateDirectory(Path.Combine(_dir, "unter"));
    public void Dispose() { try { Directory.Delete(_dir, true); } catch (IOException) { } }

    [Fact(DisplayName = "Dokumente +1: nur Word/PDF/PowerPoint/Excel/HTML, auch in Unterordnern; leere Dateien und Fremdes übersprungen")]
    public void Files()
    {
        foreach (var n in new[] { "a.pdf", "b.DOCX", "unter/c.pptx", "unter/d.xlsx", "e.html", "f.txt", "g.exe" }) File.WriteAllText(Path.Combine(_dir, n), "x");
        File.WriteAllText(Path.Combine(_dir, "leer.pdf"), "");
        Assert.Equal(["a.pdf", "b.DOCX", "c.pptx", "d.xlsx", "e.html"], DocMarkdown.Files(_dir).Select(Path.GetFileName).Order(StringComparer.OrdinalIgnoreCase));
        Assert.Single(DocMarkdown.Files(Path.Combine(_dir, "a.pdf"))); Assert.Empty(DocMarkdown.Files(Path.Combine(_dir, "f.txt"))); Assert.Empty(DocMarkdown.Files(Path.Combine(_dir, "weg")));
    }

    [Fact(DisplayName = "Dokumente +2: Antwort lesen (letzte JSON-Zeile); Titel aus dem Dokument, sonst Dateiname")]
    public void Parse()
    {
        var r = DocMarkdown.Parse("Warnung: irgendwas\n{\"zeichen\": 1834, \"titel\": \"Mietvertrag 2026\"}\n");
        Assert.Equal((1834, "Mietvertrag 2026"), (r.Chars, r.Title));
        Assert.Equal("Mietvertrag 2026", DocMarkdown.TitleFor("x/vertrag.pdf", r)); Assert.Equal("vertrag", DocMarkdown.TitleFor("x/vertrag.pdf", r with { Title = " " }));
        Assert.Throws<FormatException>(() => DocMarkdown.Parse("kein json"));
    }

    [Fact(DisplayName = "Dokumente +3: KI-Laufzeit „Dokumente“ mit fester Fassung; Startskript nur lokal, ohne Plugins, mit Größengrenze")]
    public void Runtime()
    {
        var app = KiPyApps.For(KiRuntime.Dokumente)!;
        Assert.Equal(["markitdown[pdf,docx,pptx,xlsx]==0.1.9"], app.Packages); Assert.Equal("trendo_markdown.py", app.Script); Assert.Empty(app.Torch);
        var root = new DirectoryInfo(AppContext.BaseDirectory); while (root is not null && !File.Exists(Path.Combine(root.FullName, "Trendo.sln"))) root = root.Parent;
        var script = File.ReadAllText(Path.Combine(root!.FullName, "src", "Trendo.App", "Assets", "ki", "trendo_markdown.py"));
        Assert.Contains("enable_plugins=False", script); Assert.Contains("convert_local", script); Assert.Contains("50 * 1024 * 1024", script);
        Assert.Equal("MIT", KiLibrary.RuntimeInfo(KiRuntime.Dokumente).License);
    }
}

/// <summary>Umlaute aus Python-Werkzeugen: UTF-8-Ausgabe (KI-Laufzeit: PYTHONIOENCODING; hier -X utf8, weil -I die PYTHON*-Variablen ignoriert) und UTF-8-Lesen kommen Meldungen unversehrt an (echter Lauf 08.11.2026: „besch�digt“).</summary>
public class CliEncodingTests
{
    [Fact(DisplayName = "Cli: UTF-8-Ausgabe eines Python-Prozesses kommt mit Umlauten an (stdout und stderr)")]
    public void Utf8_umlauts()
    {
        var py = Cli.Find("python") ?? Cli.Find("python3");
        if (py is null) return; // ohne Python nichts zu prüfen
        var r = Cli.Run(py, ["-I", "-X", "utf8", "-c", "import sys; print('Prüfung äöü'); print('beschädigt ß', file=sys.stderr)"], env: new Dictionary<string, string> { ["PYTHONIOENCODING"] = "utf-8" }, encoding: System.Text.Encoding.UTF8);
        Assert.Equal("Prüfung äöü", r.Out.Trim()); Assert.Equal("beschädigt ß", r.Err.Trim());
    }
}

/// <summary>Echter Lauf: markitdown 0.1.9 auf echten Testdokumenten (Prüfumgebung %TEMP%\trendo-tools-b\markitdown, Freigabe 08.11.2026).</summary>
public class DocMarkdownRealTests
{
    static string Dir => Path.Combine(Path.GetTempPath(), "trendo-tools-b", "markitdown");
    static string Py => Path.Combine(Dir, "venv", OperatingSystem.IsWindows() ? Path.Combine("Scripts", "python.exe") : Path.Combine("bin", "python"));
    public sealed class MdFactAttribute : FactAttribute { public MdFactAttribute() { if (!File.Exists(Py) || !Directory.Exists(Path.Combine(Dir, "dokumente"))) Skip = "Übersprungen – markitdown-Prüfumgebung fehlt."; } }
    static string Script()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory); while (root is not null && !File.Exists(Path.Combine(root.FullName, "Trendo.sln"))) root = root.Parent;
        return Path.Combine(root!.FullName, "src", "Trendo.App", "Assets", "ki", "trendo_markdown.py");
    }

    [MdFact(DisplayName = "Dokumente echt: Word, Excel (Tabelle), PowerPoint, HTML, PDF → Markdown mit Umlauten und Titel; kaputte Excel-Datei abgelehnt statt Müll")]
    public void Real_convert()
    {
        var outDir = Path.Combine(Path.GetTempPath(), "trendo-mdreal-" + Guid.NewGuid().ToString("N"));
        var env = new Dictionary<string, string> { ["PYTHONIOENCODING"] = "utf-8", ["PYTHONUTF8"] = "1" };
        try
        {
            (Cli.Result R, string Md) Run(string name)
            {
                var md = Path.Combine(outDir, name + ".md");
                var r = Cli.Run(Py, ["-I", "-X", "utf8", Script(), "-", .. DocMarkdown.Args(Path.Combine(Dir, "dokumente", name), md)], timeoutMs: 120_000, env: env, encoding: System.Text.Encoding.UTF8);
                return (r, File.Exists(md) ? File.ReadAllText(md) : "");
            }
            var docx = Run("mietvertrag.docx"); Assert.True(docx.R.Ok, docx.R.Err); Assert.Contains("Kündigungsfrist beträgt drei Monate", docx.Md);
            Assert.Equal("Mietvertrag Musterweg 1", DocMarkdown.TitleFor("mietvertrag.docx", DocMarkdown.Parse(docx.R.Out), docx.Md));
            var xlsx = Run("haushalt.xlsx"); Assert.True(xlsx.R.Ok, xlsx.R.Err); Assert.Contains("| Januar | 84 | 31 |", xlsx.Md);
            var pptx = Run("vereinsfest.pptx"); Assert.True(pptx.R.Ok, pptx.R.Err); Assert.Contains("Vereinsfest 2026", pptx.Md); Assert.Contains("Festwiese", pptx.Md);
            var html = Run("seite.html"); Assert.True(html.R.Ok); Assert.Equal("Rezept Apfelkuchen", DocMarkdown.Parse(html.R.Out).Title); Assert.Contains("500 g Äpfel", html.Md);
            var pdf = Run("hausordnung.pdf"); Assert.True(pdf.R.Ok, pdf.R.Err); Assert.Contains("Müll wird dienstags abgeholt", pdf.Md);
            var bad = Run("kaputt.xlsx"); Assert.False(bad.R.Ok); Assert.Contains("beschädigt", bad.R.Err); Assert.Equal("", bad.Md);
        }
        finally { try { Directory.Delete(outDir, true); } catch (IOException) { } }
    }
}
