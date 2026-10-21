using System.Text.RegularExpressions;
using Xunit;

namespace Trendo.Core.Tests;

public class W6_Catalog
{
    private static string Root()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null && !File.Exists(Path.Combine(d.FullName, "Trendo.sln"))) d = d.Parent;
        return d?.FullName ?? throw new DirectoryNotFoundException("Repo nicht gefunden");
    }

    [Fact(DisplayName = "W6-Katalog: je Studio genau die genehmigte Anzahl, PROPOSED, „gebaut“ nur mit vorhandenem Test")]
    public void Catalogs()
    {
        var root = Root();
        var reg = File.ReadAllLines(Path.Combine(root, "docs", "spec", "MODULE_REGISTER.csv")).Skip(1).Select(l => l.Split(",")).Where(c => c.Length > 2 && int.TryParse(c[2], out _)).ToDictionary(c => c[0], c => int.Parse(c[2]));
        var tests = string.Concat(Directory.GetFiles(Path.Combine(root, "tests"), "*.cs", SearchOption.AllDirectories).Select(File.ReadAllText));
        var names = Regex.Matches(tests, "DisplayName = \"([^\"]+)\"").Select(m => m.Groups[1].Value).ToHashSet();
        string[] ids = ["SPEC-01", "SPEC-02", "SPEC-03", "SPEC-04", "SPEC-05", "SPEC-06", "SPEC-08", "SPEC-09", "SPEC-10", "SPEC-13", "SPEC-14", "SPEC-15", "SPEC-16", "CORE-09"];
        foreach (var id in ids)
        {
            var md = File.ReadAllText(Path.Combine(root, "docs", "w6-katalog", id + ".md")).Replace("\r\n", "\n"); // Windows-Checkout: CRLF
            Assert.Contains("**PROPOSED**", md); Assert.DoesNotContain("APPROVED)", md.Replace("(nicht APPROVED)", ""));
            var rows = Regex.Matches(md, @"^\| F(\d\d) \| [^|]+ \|[^|]*\| (gebaut|offen) \| `([^`]+)` \|$", RegexOptions.Multiline).ToList();
            Assert.True(reg[id] == rows.Count, $"{id}: {rows.Count} statt {reg[id]} Funktionen");
            foreach (var r in rows)
            {
                var built = r.Groups[2].Value == "gebaut"; var t = r.Groups[3].Value;
                Assert.True(built == names.Contains(t), $"{id} F{r.Groups[1].Value}: Stand „{r.Groups[2].Value}“ passt nicht zum Test „{t}“");
            }
        }
    }
}
