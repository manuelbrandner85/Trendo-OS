using System.Text.RegularExpressions;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>Trendo OS ist für jeden Nutzer da (Vorgabe 2026-10-26): kein fester Personenname im Programm, im Setup oder im Linux-Abbild.</summary>
public class NoFixedUserTests
{
    static string Root([System.Runtime.CompilerServices.CallerFilePath] string f = "") => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(f)!, "..", ".."));

    [Fact(DisplayName = "Kein fester Nutzername in src, installer und linux-image")]
    public void No_fixed_person_name()
    {
        var name = new Regex(@"\bManuel\b|\bmanue\b", RegexOptions.IgnoreCase);
        var hits = new List<string>();
        foreach (var dir in new[] { "src", "installer", "linux-image" })
        {
            var d = Path.Combine(Root(), dir);
            if (!Directory.Exists(d)) continue;
            foreach (var f in Directory.EnumerateFiles(d, "*", SearchOption.AllDirectories))
            {
                if (f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}") || f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")) continue;
                if (Path.GetExtension(f).ToLowerInvariant() is not (".cs" or ".axaml" or ".sh" or ".ps1" or ".py" or ".json" or ".txt" or ".rules" or ".service" or ".md" or ".xml" or ".svg")) continue;
                var i = 0;
                foreach (var line in File.ReadLines(f)) { i++; if (name.IsMatch(line)) hits.Add($"{Path.GetRelativePath(Root(), f)}:{i}"); }
            }
        }
        Assert.True(hits.Count == 0, "Fester Name gefunden: " + string.Join(", ", hits));
    }
}
