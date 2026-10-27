using Trendo.Core;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>Wunsch 27.10.2026: KI-Programme von GitHub oder Herstellerseite automatisch laden und einrichten.</summary>
public class KiSourceSetupTests
{
    const string Releases = """
    [
      { "tag_name": "b11929", "name": "b11929", "prerelease": true, "assets": [
        { "name": "llama-b11929-bin-ubuntu-x64.zip", "browser_download_url": "https://github.com/ggml-org/llama.cpp/releases/download/b11929/llama-b11929-bin-ubuntu-x64.zip", "size": 10 },
        { "name": "llama-b11929-bin-win-cuda-12.4-x64.zip", "browser_download_url": "https://github.com/ggml-org/llama.cpp/releases/download/b11929/llama-b11929-bin-win-cuda-12.4-x64.zip", "size": 10 },
        { "name": "llama-b11929-bin-win-cpu-x64.zip", "browser_download_url": "https://github.com/ggml-org/llama.cpp/releases/download/b11929/llama-b11929-bin-win-cpu-x64.zip", "size": 10, "digest": "sha256:AB" } ] },
      { "tag_name": "b11928", "assets": [] }
    ]
    """;

    [Fact(DisplayName = "KI-Quellen: richtige Windows-Datei aus allen Versionen (auch Vorabversion), nur github.com; jede Quelle hat einen Weg")]
    public void Pick()
    {
        var s = KiSourceSetups.For("llamacpp")!;
        var (rel, asset) = KiSourceSetups.Pick(s, KiSourceSetups.ParseReleases(Releases))!.Value;
        Assert.Equal(("b11929", "llama-b11929-bin-win-cpu-x64.zip", "ab"), (rel.Tag, asset.Name, asset.Sha256));
        Assert.Null(KiSourceSetups.Pick(KiSourceSetups.For("koboldcpp")!, KiSourceSetups.ParseReleases(Releases)));
        Assert.All(KiCatalog.Sources, src => Assert.NotNull(KiSourceSetups.For(src.Id)));
        Assert.All(KiSourceSetups.All, x => Assert.True(x.Repo is not null || x.VendorUrl!.StartsWith("https://", StringComparison.Ordinal)));
        Assert.Contains(new Uri(KiSourceSetups.For("lmstudio")!.VendorUrl!).Host, KiSourceSetups.VendorHosts);
    }

    [Fact(DisplayName = "KI-Quellen: Startbefehl mit Sprachmodell, ohne Modell klare Meldung; größtes GGUF wird gewählt; Installer werden unter %LOCALAPPDATA% erkannt")]
    public void Args_model_installed()
    {
        var k = KiSourceSetups.For("koboldcpp")!;
        Assert.Contains("--model \"C:\\m\\q.gguf\"", KiSourceSetups.Args(k, @"C:\m\q.gguf"));
        Assert.Contains("127.0.0.1", KiSourceSetups.Args(k, @"C:\m\q.gguf"));
        Assert.Throws<InvalidOperationException>(() => KiSourceSetups.Args(k, null));
        Assert.Equal("serve", KiSourceSetups.Args(KiSourceSetups.For("ollama")!, null));
        var d = Path.Combine(Path.GetTempPath(), "trendo-kisrc-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            Directory.CreateDirectory(Path.Combine(d, "ki", "a")); File.WriteAllBytes(Path.Combine(d, "ki", "a", "klein.gguf"), new byte[10]); File.WriteAllBytes(Path.Combine(d, "ki", "a", "gross.gguf"), new byte[99]);
            File.WriteAllBytes(Path.Combine(d, "ki", "a", "mmproj-gross.gguf"), new byte[500]);
            Assert.EndsWith("gross.gguf", KiSourceSetups.BestModel(Path.Combine(d, "ki")));
            Assert.DoesNotContain("mmproj", KiSourceSetups.BestModel(Path.Combine(d, "ki")));
            var lm = KiSourceSetups.For("lmstudio")!;
            Assert.Null(KiSourceSetups.FindInstalled(lm, d));
            Directory.CreateDirectory(Path.Combine(d, "Programs", "LM Studio")); File.WriteAllText(Path.Combine(d, "Programs", "LM Studio", "LM Studio.exe"), "x");
            Assert.NotNull(KiSourceSetups.FindInstalled(lm, d));
        }
        finally { Directory.Delete(d, true); }
    }
}
