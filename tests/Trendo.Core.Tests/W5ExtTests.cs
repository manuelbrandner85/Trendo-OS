using System.IO.Compression;
using System.Text.Json;
using Trendo.Core;
using Xunit;

public class W5_X04_Extensions
{
    static string Tmp() { var d = Path.Combine(Path.GetTempPath(), "trendo-ext-t-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(d); return d; }

    [Fact(DisplayName = "X-04: Beispiel-Erweiterung installieren (Ordner und ZIP), ein/aus, Prüfsumme stabil, entfernen")]
    public void Install_cycle()
    {
        var src = ExtensionSamples.WriteSample(Tmp());
        var s = new ExtensionStore(Tmp());
        var (e, err) = s.Install(src); Assert.Empty(err); Assert.NotNull(e);
        Assert.True(e!.Enabled); var h = e.Sha256;
        s.SetEnabled(e.Manifest.Id, false); Assert.False(s.Get(e.Manifest.Id)!.Enabled); Assert.Equal(h, s.Get(e.Manifest.Id)!.Sha256);
        s.Remove(e.Manifest.Id); Assert.Empty(s.All());
        var zip = Path.Combine(Tmp(), "v.zip"); ZipFile.CreateFromDirectory(src, zip, CompressionLevel.Fastest, includeBaseDirectory: true);
        Assert.Empty(s.Install(zip).Errors); Assert.Single(s.All());
        Assert.Equal(4, SitePage.Load(Path.Combine(s.All()[0].Dir, "seite.json")).Blocks.Count);
    }

    [Fact(DisplayName = "X-04: Programmcode, falsche Rechte, kaputte Beschreibung und ZIP-Ausbruch werden abgewiesen")]
    public void Rejects()
    {
        var s = new ExtensionStore(Tmp());
        var a = ExtensionSamples.WriteSample(Tmp()); File.WriteAllText(Path.Combine(a, "boese.exe"), "MZ");
        Assert.Contains(s.Install(a).Errors, x => x.Contains("kein Programmcode"));
        var b = ExtensionSamples.WriteSample(Tmp()); var mf = Path.Combine(b, Extensions.ManifestFile);
        File.WriteAllText(mf, File.ReadAllText(mf).Replace("\"Permissions\": []", "\"Permissions\": [\"alles\"]").Replace("1.0.0", "eins"));
        var err = s.Install(b).Errors; Assert.Contains(err, x => x.Contains("Recht")); Assert.Contains(err, x => x.Contains("Version"));
        var c = ExtensionSamples.WriteSample(Tmp()); File.WriteAllText(Path.Combine(c, Extensions.ManifestFile), "{kaputt");
        Assert.Contains(s.Install(c).Errors, x => x.Contains("JSON"));
        var zip = Path.Combine(Tmp(), "evil.zip");
        using (var z = ZipFile.Open(zip, ZipArchiveMode.Create)) { using var w = new StreamWriter(z.CreateEntry("../../evil.json").Open()); w.Write("{}"); }
        Assert.Contains(s.Install(zip).Errors, x => x.Contains("Unsicherer Pfad"));
        Assert.Empty(s.All());
    }
}
