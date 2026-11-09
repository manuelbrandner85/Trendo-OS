using Trendo.Core;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>Übergabe zwischen Studios innerhalb eines Projekts.</summary>
public class ProjectHandoffTests
{
    static string Folder() { var d = Path.Combine(Path.GetTempPath(), "trendo-uebergabe-" + Guid.NewGuid().ToString("N")[..8]); Directory.CreateDirectory(d); return d; }

    [Fact(DisplayName = "Übergabe: Datei geht an das Ziel-Studio, wird einmal angenommen und bleibt im Protokoll")]
    public void Flow()
    {
        var d = Folder(); File.WriteAllText(Path.Combine(d, "a.png"), "x");
        var h = new ProjectHandoff(d);
        var item = h.Add("CORE-08", "CORE-06", "a.png", "figur", "Held");
        Assert.Single(h.OpenFor("CORE-06")); Assert.Empty(h.OpenFor("SPEC-01"));
        Assert.Equal(Path.GetFullPath(Path.Combine(d, "a.png")), h.Take(item.Id, "CORE-06"));
        Assert.Empty(h.OpenFor("CORE-06"));
        Assert.NotNull(h.All()[0].Received);
        Assert.Throws<ArgumentException>(() => h.Take(item.Id, "CORE-06"));
    }

    [Fact(DisplayName = "Übergabe: falsches Studio, fehlende, fremde und nachträglich geänderte Dateien werden abgelehnt")]
    public void Refuses()
    {
        var d = Folder(); File.WriteAllText(Path.Combine(d, "a.png"), "x");
        var h = new ProjectHandoff(d);
        Assert.Throws<ArgumentException>(() => h.Add("A", "B", "fehlt.png", "x"));
        Assert.Throws<ArgumentException>(() => h.Add("A", "B", @"..\ausserhalb.png", "x"));
        var item = h.Add("A", "CORE-06", "a.png", "figur");
        Assert.Throws<ArgumentException>(() => h.Take(item.Id, "SPEC-01"));
        Assert.Throws<ArgumentException>(() => h.Take("gibtsnicht", "CORE-06"));
        File.WriteAllText(Path.Combine(d, "a.png"), "geändert");
        Assert.Throws<ArgumentException>(() => h.Take(item.Id, "CORE-06"));
    }

    [Fact(DisplayName = "Übergabe: gilt je Projekt, ein anderes Projekt sieht sie nicht")]
    public void Per_project()
    {
        var a = Folder(); var b = Folder(); File.WriteAllText(Path.Combine(a, "a.png"), "x");
        new ProjectHandoff(a).Add("A", "CORE-06", "a.png", "figur");
        Assert.Empty(new ProjectHandoff(b).All());
    }
}
