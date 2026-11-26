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

    /// T-0783: Diese Bilder entstanden zuerst mit SDXL Turbo (Stability AI Community License) und sind jetzt mit FLUX.2 [klein] 4B (Apache-2.0) neu erzeugt.
    public static readonly string[] ErsetztT0783 =
    {
        "STUDIO-BOOK-01", "STUDIO-ANIM-01", "STUDIO-PHOTOS-01", "STUDIO-IMAGEAI-01", "STUDIO-VIDEOAI-01", "STUDIO-AGENTS-01", "STUDIO-MODELS-01",
        "STUDIO-TRAIN-01", "STUDIO-CODEAI-01", "STUDIO-VISION-01", "STUDIO-CITE-01", "STUDIO-DAYPLAN-01", "STUDIO-TUTOR-01", "STUDIO-CODELEARN-01",
    };

    [Fact(DisplayName = "Studio-Bilder T-0783: früher mit SDXL Turbo erzeugte Bilder stammen jetzt aus FLUX.2 [klein] 4B (Apache-2.0) – Herkunft mit Prüfsumme, Lizenznachweis, Generator ohne SDXL")]
    public void Former_sdxl_images_are_flux_apache()
    {
        using var reg = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Repo("docs/design/STUDIOBILDER.json")));
        var third = File.ReadAllText(Repo("THIRD_PARTY.md"));
        var gen = File.ReadAllText(Repo("tools/studio_bilder.py"));
        Assert.Contains("'unet': 'flux-2-klein-4b.safetensors'", gen);
        Assert.Contains("'unet_name': MODELL['unet']", gen);
        Assert.DoesNotContain("sd-cli", gen);
        Assert.DoesNotContain("sd_xl_turbo", gen);
        Assert.Contains("Studio-Bilder", third);
        Assert.Contains("FLUX.2 [klein] 4B", third);
        Assert.Contains("Apache-2.0", third);
        foreach (var id in ErsetztT0783)
        {
            Assert.True(reg.RootElement.TryGetProperty(id, out var e), $"{id}: keine Herkunft in docs/design/STUDIOBILDER.json");
            Assert.Contains("FLUX.2 [klein] 4B", e.GetProperty("modell").GetString());
            Assert.Contains("Apache-2.0", e.GetProperty("lizenz").GetString());
            var sha = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(Repo($"src/Trendo.App/Assets/studios/{id}.jpg")))).ToLowerInvariant();
            Assert.True(e.TryGetProperty("sha256", out var s) && s.GetString() == sha, $"{id}: Prüfsumme der Herkunft passt nicht zum Bild");
            Assert.Contains(id, third);
        }
    }

    [Fact(DisplayName = "Studio-Bilder: jedes JPG ist 768 × 432 und trägt keine Metadaten mit Pfaden, Rechner- oder Benutzernamen (kein EXIF/XMP)")]
    public void Studio_jpgs_size_and_clean_metadata()
    {
        var bad = new List<string>();
        foreach (var f in Directory.GetFiles(Repo("src/Trendo.App/Assets/studios"), "*.jpg"))
        {
            var b = File.ReadAllBytes(f); var name = Path.GetFileName(f);
            if (b.Length < 4 || b[0] != 0xFF || b[1] != 0xD8) { bad.Add(name + ": kein JPG"); continue; }
            int i = 2, w = 0, h = 0;
            while (i + 4 <= b.Length && b[i] == 0xFF)
            {
                var m = b[i + 1];
                if (m == 0xDA) break;
                var len = (b[i + 2] << 8) | b[i + 3];
                if (i + 2 + len > b.Length) break;
                var seg = b.AsSpan(i + 4, len - 2);
                if (m is 0xC0 or 0xC1 or 0xC2) { h = (seg[1] << 8) | seg[2]; w = (seg[3] << 8) | seg[4]; }
                if (m is 0xE1 or 0xED) bad.Add(name + ": EXIF/XMP/IPTC-Block");
                if (m is 0xFE or (>= 0xE0 and <= 0xEF))
                {
                    var text = System.Text.Encoding.Latin1.GetString(seg);
                    if (System.Text.RegularExpressions.Regex.IsMatch(text, @"(?i)users[\\/]|[a-z]:\\|desktop-|@")) bad.Add(name + ": Metadaten mit Pfad/Name");
                }
                i += 2 + len;
            }
            if (w != 768 || h != 432) bad.Add($"{name}: {w} × {h} statt 768 × 432");
        }
        Assert.True(bad.Count == 0, string.Join("; ", bad));
    }
}
