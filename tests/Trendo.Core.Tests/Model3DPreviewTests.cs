using Trendo.Core;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>Alle Studios, Runde 1 – f3d-Vorschaubilder (CORE-05): 5 positive, 5 negative Prüfungen. Der echte f3d-Lauf ist hier nachgebildet (Prozess-Funktion), siehe Beleg.</summary>
public class Model3DPreviewTests
{
    static (string Dir, string Model, string Exe) Setup(string ext = ".glb")
    {
        var d = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "trendo-f3dtest-" + Guid.NewGuid().ToString("N"))).FullName;
        var m = Path.Combine(d, "wuerfel" + ext); File.WriteAllBytes(m, [1, 2, 3, 4]);
        var e = Path.Combine(d, "f3d-console.exe"); File.WriteAllBytes(e, [0]);
        return (d, m, e);
    }
    static Cli.Result Fake(string exe, IReadOnlyList<string> a, int t) { File.WriteAllBytes(a.First(x => x.StartsWith("--output=", StringComparison.Ordinal))["--output=".Length..], [137, 80, 78, 71]); return new(0, "", ""); }

    [Fact(DisplayName = "f3d +1: gültiges Modell → PNG im Projekt")]
    public void Renders()
    {
        var (d, m, e) = Setup(); try { var (ok, msg) = Model3DPreview.Render(e, m, Path.Combine(d, "vorschau", "wuerfel.png"), 512, run: Fake); Assert.True(ok); Assert.True(File.Exists(msg)); }
        finally { Directory.Delete(d, true); }
    }

    [Fact(DisplayName = "f3d +2: Argumente als Liste – Bild, Größe, Kantenglättung, Tone-Mapping, Umgebungsverdeckung, Datei zuletzt")]
    public void Arguments()
    {
        var a = Model3DPreview.Args(@"C:\p\haus & hof.glb", @"C:\p\v.png", 800);
        Assert.Equal(["--no-config", "--output=C:\\p\\v.png", "--resolution=800,800", "--anti-aliasing", "--tone-mapping", "--ambient-occlusion", "--hdri-ambient", @"C:\p\haus & hof.glb"], a);
    }

    [Fact(DisplayName = "f3d +3: gängige Formate werden erkannt (Groß/Klein egal)")]
    public void Formats() { foreach (var f in new[] { "a.GLB", "b.gltf", "c.obj", "d.STL", "e.fbx", "f.usdz", "g.3mf", "h.step" }) Assert.True(Model3DPreview.Supports(f), f); }

    [Fact(DisplayName = "f3d +4: Katalog – f3d ist offiziell, portabel, BSD-3, mit fester Prüfsumme, ohne Admin")]
    public void Catalog()
    {
        var c = DependencyCatalog.Get("f3d");
        Assert.Equal("BSD-3-Clause", c.License); Assert.Equal(DeliveryMode.AutoUser, c.Mode);
        Assert.StartsWith("https://github.com/f3d-app/f3d/releases/download/v4.0.1/", c.PortableZip); Assert.Equal(64, c.PortableSha256!.Length);
    }

    [Fact(DisplayName = "f3d +5: Grenzen 64 und 4096 Pixel sind erlaubt")]
    public void Size_limits()
    {
        var (d, m, _) = Setup(); try { Assert.Null(Model3DPreview.Check(m, "x.png", 64)); Assert.Null(Model3DPreview.Check(m, "x.png", 4096)); }
        finally { Directory.Delete(d, true); }
    }

    [Fact(DisplayName = "f3d −1: fehlende, leere oder fremde Datei wird abgewiesen")]
    public void Bad_input()
    {
        var (d, m, e) = Setup(".txt"); try
        {
            Assert.Contains("gibt es nicht", Model3DPreview.Check(Path.Combine(d, "fehlt.glb"), "x.png", 512));
            Assert.Contains("nicht unterstützt", Model3DPreview.Check(m, "x.png", 512));
            var leer = Path.Combine(d, "leer.obj"); File.WriteAllBytes(leer, []); Assert.Contains("leer", Model3DPreview.Check(leer, "x.png", 512));
        }
        finally { Directory.Delete(d, true); }
    }

    [Fact(DisplayName = "f3d −2: Ziel muss PNG sein, Größe außerhalb 64–4096 wird abgewiesen")]
    public void Bad_output()
    {
        var (d, m, _) = Setup(); try { Assert.Contains("PNG", Model3DPreview.Check(m, "x.jpg", 512)); Assert.Contains("außerhalb", Model3DPreview.Check(m, "x.png", 63)); Assert.Contains("außerhalb", Model3DPreview.Check(m, "x.png", 5000)); }
        finally { Directory.Delete(d, true); }
    }

    [Fact(DisplayName = "f3d −3: f3d nicht eingerichtet → klarer Hinweis, kein Prozessstart")]
    public void Not_installed()
    {
        var (d, m, _) = Setup(); var started = false; try
        {
            var (ok, msg) = Model3DPreview.Render(Path.Combine(d, "fehlt.exe"), m, Path.Combine(d, "v.png"), run: (e, a, t) => { started = true; return new(0, "", ""); });
            Assert.False(ok); Assert.Contains("noch nicht eingerichtet", msg); Assert.False(started);
        }
        finally { Directory.Delete(d, true); }
    }

    [Fact(DisplayName = "f3d −4: Fehler von f3d wird mit seiner ersten Zeile gemeldet")]
    public void Tool_error()
    {
        var (d, m, e) = Setup(); try { var (ok, msg) = Model3DPreview.Render(e, m, Path.Combine(d, "v.png"), run: (x, a, t) => new(1, "", "Reader failed\nmehr")); Assert.False(ok); Assert.EndsWith("Reader failed", msg); }
        finally { Directory.Delete(d, true); }
    }

    [Fact(DisplayName = "f3d −5: Erfolg gemeldet, aber kein Bild geschrieben → kein falsches Erfolgssignal")]
    public void No_image()
    {
        var (d, m, e) = Setup(); try { var (ok, msg) = Model3DPreview.Render(e, m, Path.Combine(d, "v.png"), run: (x, a, t) => new(0, "", "")); Assert.False(ok); Assert.Contains("kein Bild", msg); }
        finally { Directory.Delete(d, true); }
    }
}
