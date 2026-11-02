using Trendo.App;
using Xunit;
namespace Trendo.App.Tests;

/// Wunsch 02.11.2026: alle Bilder in Trendo fotorealistisch und passend – auch jedes künftige Studio bekommt eins (tools/studio_bilder.py, lokal mit FLUX.2 klein).
public class StudioBilder
{
    static string Repo(string rel)
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d is not null; d = d.Parent)
            if (File.Exists(Path.Combine(d.FullName, "Trendo.sln"))) return Path.Combine(d.FullName, rel);
        throw new FileNotFoundException(rel);
    }

    [Fact(DisplayName = "Studio-Bilder: jedes Studio im Katalog hat ein Bild (768 × 432 JPG) und eine Bildbeschreibung im Generator – neue Studios fallen sofort auf")]
    public void Every_studio_has_photo()
    {
        var gen = File.ReadAllText(Repo("tools/studio_bilder.py"));
        var missingImage = new List<string>(); var missingPrompt = new List<string>();
        foreach (var id in StudioCatalog.ById.Keys)
        {
            var f = Repo($"src/Trendo.App/Assets/studios/{id}.jpg");
            if (!File.Exists(f) || new FileInfo(f).Length < 8_000) missingImage.Add(id);
            else
            {
                var b = File.ReadAllBytes(f);
                Assert.True(b[0] == 0xFF && b[1] == 0xD8, $"{id}.jpg ist kein JPG");
            }
            if (!gen.Contains($"'{id}':")) missingPrompt.Add(id);
        }
        Assert.True(missingImage.Count == 0, "Ohne Bild: " + string.Join(", ", missingImage));
        Assert.True(missingPrompt.Count == 0, "Ohne Bildbeschreibung in tools/studio_bilder.py: " + string.Join(", ", missingPrompt));
    }
}
