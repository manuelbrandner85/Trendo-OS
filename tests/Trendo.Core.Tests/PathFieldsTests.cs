using Trendo.Core.Shell;
using Xunit;
using K = Trendo.Core.Shell.PathFields.Kind;
namespace Trendo.Core.Tests;

public class T0201_PathFields
{
    [Theory(DisplayName = "T-0201: Datei-/Ordnerfelder werden erkannt, Adressen und Muster nicht")]
    [InlineData("sci-t-file", "CSV-Datei", K.File)]
    [InlineData("ren-folder", "Ordner mit den Dateien", K.Folder)]
    [InlineData("offline-folder", "Ordner mit Installationsdateien", K.Folder)]
    [InlineData("zip-src", "Ordner oder Datei zum Packen", K.FileOrFolder)]
    [InlineData("media-path", "Pfad zur Datei", K.File)]
    [InlineData("du-folder", @"Ordner, z. B. C:\Users\Name", K.Folder)]
    [InlineData("dl-url", "Adresse der Datei (https://…)", K.None)]
    [InlineData("ren-pattern", "Muster, z. B. Urlaub_{n:000}", K.None)]
    [InlineData("sc-args", "Parameter, z. B. --neu \"C:\\Mein Ordner\"", K.None)]
    [InlineData("bm-folder", "Ordner (optional)", K.None)]
    [InlineData("name", "Name der Idee", K.None)]
    [InlineData("txt-search", "Studios, Dateien und Einstellungen suchen", K.None)]
    [InlineData("startmenu-search", "Programme, Studios, Dateien und Einstellungen suchen", K.None)]
    [InlineData("txt-studio-search", "Studio suchen", K.None)]
    public void Classify(string name, string hint, K want) => Assert.Equal(want, PathFields.Classify(name, hint));

    [Fact(DisplayName = "T-0201: Dateityp-Filter aus dem Platzhalter")]
    public void Filter()
    {
        Assert.Equal(["csv", "xlsx"], PathFields.Extensions("CSV- oder XLSX-Datei"));
        Assert.Equal(["glb", "gltf"], PathFields.Extensions("Datei (.glb/.gltf)"));
        Assert.Equal(["txt", "md", "csv"], PathFields.Extensions("Ordner oder Datei (.txt, .md, .csv)"));
        Assert.Contains("jpg", PathFields.Extensions("Bilddatei (PNG, JPG …)"));
        Assert.Empty(PathFields.Extensions("Datei"));
        Assert.True(PathFields.Multiple("pdf-merge-files", "PDF-Dateien, eine pro Zeile"));
    }

    [Fact(DisplayName = "T-0201: Zuletzt benutzt – neueste zuerst, ohne Doppelte, begrenzt")]
    public void Recent()
    {
        var l = PathFields.Remember(["a", "b"], "b");
        Assert.Equal(["b", "a"], l);
        for (var i = 0; i < 20; i++) l = PathFields.Remember(l, "x" + i);
        Assert.Equal(8, l.Count); Assert.Equal("x19", l[0]);
    }
}
