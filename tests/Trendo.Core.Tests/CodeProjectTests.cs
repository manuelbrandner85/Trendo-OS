using Trendo.Core;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>Code-Editor – selbst nachgebaut statt VSCodium (04.11.2026): Baum, Sprache, Suche, Speichern, Projektgrenze.</summary>
public class CodeProjectTests
{
    static string Project()
    {
        var d = Path.Combine(Path.GetTempPath(), "trendo-codetest-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(Path.Combine(d, "src")); Directory.CreateDirectory(Path.Combine(d, "bin")); Directory.CreateDirectory(Path.Combine(d, ".git"));
        File.WriteAllText(Path.Combine(d, "main.py"), "print('Hallo')\n# TODO aufräumen\n");
        File.WriteAllText(Path.Combine(d, "src", "app.js"), "const x = 1;\r\n// todo: mehr\r\n");
        File.WriteAllText(Path.Combine(d, "bin", "x.dll"), "todo");
        File.WriteAllBytes(Path.Combine(d, "bild.png"), [0x89, 0x50, 0, 0, 1, 2]);
        return d;
    }

    [Fact(DisplayName = "Baum: Ordner zuerst, Bau- und Git-Ordner ausgeblendet, zugeklappte Ordner ohne Inhalt")]
    public void Tree()
    {
        var d = Project();
        try
        {
            var t = CodeProject.Tree(d);
            Assert.Equal("src", t[0].Name); Assert.True(t[0].IsDir); Assert.Equal("app.js", t[1].Name); Assert.Equal(1, t[1].Depth);
            Assert.DoesNotContain(t, e => e.Name is "bin" or ".git" or "x.dll");
            var closed = CodeProject.Tree(d, new HashSet<string> { Path.Combine(d, "src") });
            Assert.DoesNotContain(closed, e => e.Name == "app.js");
        }
        finally { Directory.Delete(d, true); }
    }

    [Fact(DisplayName = "Sprache, Lesen (Binärdatei abgelehnt), Speichern behält CRLF, Suche ohne Groß/Klein, neue Datei nur im Projekt")]
    public void Files()
    {
        var d = Project();
        try
        {
            Assert.Equal(("Python", "Python"), CodeProject.Language("a.py")); Assert.Equal("TypeScript", CodeProject.Language("a.tsx").Label); Assert.Equal("JavaScript", CodeProject.Language("a.jsx").Label);
            Assert.Null(CodeProject.Language("x.yaml").Highlight);
            Assert.Contains("keine Textdatei", Assert.Throws<InvalidOperationException>(() => CodeProject.ReadText(Path.Combine(d, "bild.png"))).Message);
            var js = Path.Combine(d, "src", "app.js");
            var text = CodeProject.ReadText(js).Replace("\r\n", "\n") + "export default x;\n";
            CodeProject.WriteText(js, text);
            Assert.Contains("export default x;\r\n", File.ReadAllText(js));
            var hits = CodeProject.Search(d, "TODO");
            Assert.Equal(2, hits.Count); Assert.Contains(hits, h => h.File.EndsWith("main.py") && h.Line == 2);
            Assert.Equal(Path.Combine(d, "src", "neu.py"), CodeProject.Inside(d, "src/neu.py"));
            Assert.Throws<InvalidOperationException>(() => CodeProject.Inside(d, "../draussen.py"));
            Assert.Equal("python", CodeProject.RunCommand(d, Path.Combine(d, "main.py"))!.Value.Exe.Replace("3", ""));
            Assert.Null(CodeProject.RunCommand(d, Path.Combine(d, "x.yaml")));
        }
        finally { Directory.Delete(d, true); }
    }
}
