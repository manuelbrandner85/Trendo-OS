using System.IO.Compression;
using System.Text.RegularExpressions;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>Vorgabe 2026-10-27: In Briefen, Dokumenten, Webseiten und Projekten der Nutzer steht kein Trendo-Logo und kein „Erstellt mit Trendo OS“.
/// Trendo ist das Werkzeug, nicht der Absender.</summary>
public class NoBrandingInDocumentsTests
{
    static string Root([System.Runtime.CompilerServices.CallerFilePath] string f = "") => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(f)!, "..", ".."));

    [Fact(DisplayName = "Nutzer-Dokumente: kein „Erstellt mit Trendo OS“ und kein Trendo als Autor in den Generatoren")]
    public void Generators_have_no_branding()
    {
        var bad = new Regex(@"(Erstellt|Angelegt|Gemacht|Made|Created) mit Trendo|Start mit Trendo|<dc:creator>Trendo|cp:lastModifiedBy>Trendo", RegexOptions.IgnoreCase);
        var hits = new List<string>();
        foreach (var f in Directory.EnumerateFiles(Path.Combine(Root(), "src"), "*.cs", SearchOption.AllDirectories))
        {
            if (f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") || f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")) continue;
            var i = 0;
            foreach (var line in File.ReadLines(f)) { i++; if (bad.IsMatch(line)) hits.Add($"{Path.GetFileName(f)}:{i}"); }
        }
        Assert.True(hits.Count == 0, "Trendo-Branding in Nutzer-Inhalten: " + string.Join(", ", hits));
    }

    [Fact(DisplayName = "Briefvorlagen: Autor und Bearbeiter sind leer, nirgends „Trendo“")]
    public void Templates_have_no_branding()
    {
        foreach (var f in Directory.EnumerateFiles(Path.Combine(Root(), "src", "Trendo.App", "Assets", "templates"), "*.docx"))
        {
            using var z = ZipFile.OpenRead(f);
            foreach (var e in z.Entries.Where(e => e.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)))
            {
                using var r = new StreamReader(e.Open());
                Assert.DoesNotContain("Trendo", r.ReadToEnd());
            }
        }
    }
}
