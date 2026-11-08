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
