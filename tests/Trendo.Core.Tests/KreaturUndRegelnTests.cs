using System.Text.Json;
using Trendo.Core;
using Xunit;
namespace Trendo.Core.Tests;

/// 0.9.47: Claude-Regeln für Web und 3D, lokale Web-Bibliotheken im Projektordner, Kreatur-Baukasten (Drache) in echtem Blender.
public class KreaturUndRegeln
{
    static string Repo(string rel)
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d is not null; d = d.Parent)
            if (File.Exists(Path.Combine(d.FullName, "Trendo.sln"))) return Path.Combine(d.FullName, rel);
        throw new FileNotFoundException(rel);
    }

    [Fact(DisplayName = "Claude-Regeln: Web nutzt lokale Bibliotheken (ohne CDN, ohne GSAP), 3D arbeitet sichtbar in Blender, Hunyuan3D wird in der EU nicht verwendet")]
    public void Rules()
    {
        var dir = Path.Combine(Path.GetTempPath(), "trendo-webregel-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir);
        try
        {
            Assert.Equal(4, WebRules.Prepare(dir));
            Assert.True(File.Exists(Path.Combine(dir, ".trendo", "web-vendor", "trendo-motion.js")));
            Assert.Contains("immersiv", File.ReadAllText(Path.Combine(dir, ".trendo", "web-vendor", "trendo-three.js")));
            File.WriteAllText(Path.Combine(dir, ".trendo", "web-vendor", "trendo-motion.js"), "// eigene Änderung");
            Assert.Equal(0, WebRules.Prepare(dir)); // Vorhandenes bleibt
            Assert.Equal("// eigene Änderung", File.ReadAllText(Path.Combine(dir, ".trendo", "web-vendor", "trendo-motion.js")));
        }
        finally { Directory.Delete(dir, true); }
        Assert.Contains("CDN", WebRules.Text); Assert.Contains("GSAP", WebRules.Text); Assert.Contains("Bewegung reduzieren", WebRules.Text);
        Assert.Contains("sichtbar", Model3DRules.Text); Assert.Contains("TRELLIS", Model3DRules.Text);
        Assert.Contains("Hunyuan3D ist in der EU nicht lizenziert", Model3DRules.Text);
    }

    static string? BlenderExe() => Cli.Find("blender") ?? (Directory.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Blender Foundation"))
        ? Directory.EnumerateDirectories(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Blender Foundation")).Select(d => Path.Combine(d, "blender.exe")).FirstOrDefault(File.Exists) : null);

    public sealed class BlenderFactAttribute : FactAttribute { public BlenderFactAttribute() { if (BlenderExe() is null) Skip = "Übersprungen – Blender ist nicht installiert."; } }

    [BlenderFact(DisplayName = "Kreatur-Baukasten: echter Blender baut einen Drachen mit Rig und schreibt .blend und .glb (übersprungen ohne Blender)")]
    public void Dragon_in_blender()
    {
        var bl = BlenderExe()!;
        var dir = Path.Combine(Path.GetTempPath(), "trendo-kreatur-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir);
        try
        {
            var basis = Path.Combine(dir, "drache").Replace('\\', '/');
            var r = Cli.Run(bl, ["-b", "--factory-startup", "--python", Repo("src/Trendo.App/Assets/blender/trendo_kreatur.py"), "--", JsonSerializer.Serialize(new { art = "drache", ausgabe = basis, bild = false })], dir, 300_000);
            var line = r.Out.Split('\n').FirstOrDefault(l => l.StartsWith("TRENDO_KREATUR ")) ?? throw new Xunit.Sdk.XunitException("Keine Ausgabe von Blender:\n" + r.Err);
            using var stats = JsonDocument.Parse(line["TRENDO_KREATUR ".Length..]);
            Assert.True(stats.RootElement.GetProperty("dreiecke").GetInt32() > 20_000);
            Assert.True(stats.RootElement.GetProperty("knochen").GetInt32() >= 30);
            Assert.True(new FileInfo(basis + ".glb").Length > 500_000);
            Assert.True(File.Exists(basis + ".blend"));
            var glb = File.ReadAllBytes(basis + ".glb");
            Assert.Equal("glTF", System.Text.Encoding.ASCII.GetString(glb, 0, 4));
            Assert.Contains("\"skins\"", System.Text.Encoding.UTF8.GetString(glb, 20, Math.Min(glb.Length - 20, 400_000))); // Knochengerüst im GLB
        }
        finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
    }
}
