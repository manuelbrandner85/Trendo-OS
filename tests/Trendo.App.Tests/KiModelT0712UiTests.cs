using System.Security.Cryptography;
using System.Text;
using Avalonia.Headless.XUnit;
using Trendo.App;
using Trendo.Core;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>T-0712: die App findet die vorhandenen Modelle über das laufende Ollama (/api/tags) und sonst über die Manifeste.
/// Echtlauf nur mit TRENDO_ECHT_MODELLE=&lt;Belegordner&gt; (echtes Ollama dieses PCs über <see cref="OllamaBackend.InstalledAsync"/>).</summary>
public class KiModelT0712_Ui
{
    [AvaloniaFact(DisplayName = "T-0712: Ollama meldet Modelle → Standard daraus (qwen3:14b), Quelle „ollama-api“, 30 s zwischengespeichert")]
    public void Api_list_used()
    {
        var (w, _, _) = Ui.Open();
        try
        {
            var calls = 0;
            w.VramOverride = () => 12;
            w.OllamaTagsOverride = () => { calls++; return [new("qwen3:14b", 9_276_198_993), new("qwen2.5:0.5b", 397_821_319), new("qwen2.5-coder:14b", 8_988_124_069)]; };
            Assert.Equal("qwen3:14b", w.KiModelChoice.Model);
            Assert.Equal("ollama-api", w.LastModelScan!.Source);
            _ = w.KiModelChoice; _ = w.KiModelChoice;
            Assert.Equal(1, calls);
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0712: Ollama aus → Manifeste in Trendos Werkzeugordner (auch neues Format manifests-v2) werden gefunden")]
    public void Manifests_when_ollama_off()
    {
        var (w, _, _) = Ui.Open();
        try
        {
            w.VramOverride = () => 12;
            w.OllamaTagsOverride = () => null;
            var d = Path.Combine(w.ToolsRootForTests, "model", "manifests-v2", "ollama.com", "library", "qwen3");
            Directory.CreateDirectory(d);
            File.WriteAllText(Path.Combine(d, "14b"), "{\"schemaVersion\":2,\"layers\":[{\"mediaType\":\"application/vnd.ollama.image.model\",\"size\":9276184896}]}");
            Assert.Equal("qwen3:14b", w.KiModelChoice.Model);
            Assert.Equal("manifeste", w.LastModelScan!.Source);
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0712 ECHT: App-Weg mit dem echten Ollama dieses PCs wählt qwen3:14b (nur mit TRENDO_ECHT_MODELLE)")]
    public void Real_app_path()
    {
        if (Environment.GetEnvironmentVariable("TRENDO_ECHT_MODELLE") is not { Length: > 0 } dirVar) return;
        var dir = Directory.CreateDirectory(dirVar).FullName;
        var (w, _, _) = Ui.Open();
        try
        {
            var vram = SystemMetrics.GpuMemory()?.TotalGb ?? 0;
            w.VramOverride = () => vram; // im Testfenster ist die Hardware-Abfrage sonst aus
            w.OllamaTagsOverride = () => Task.Run(() => new OllamaBackend().InstalledAsync()).Result; // echte Anfrage GET 127.0.0.1:11434/api/tags
            var c = w.KiModelChoice;
            var scan = w.LastModelScan!;
            var txt = $"T-0712 App-Weg {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}\nMainWindow.InstalledModels → KiModel.Discover → OllamaBackend.InstalledAsync (echt)\n" +
                $"Quelle: {scan.Source} · {scan.Detail}\nModelle: {string.Join(", ", scan.Models.Select(m => m.Tag))}\nGrafikspeicher: {vram:0.0} GB\nStandard: {c.Model} – {c.Reason}\n";
            txt = txt.Replace(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), @"C:\Users\<user>", StringComparison.OrdinalIgnoreCase);
            File.WriteAllText(Path.Combine(dir, "app-weg.log"), txt);
            File.AppendAllText(Path.Combine(dir, "SHA256SUMS"), Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(txt))).ToLowerInvariant() + "  app-weg.log\n");
            Assert.Equal("ollama-api", scan.Source);
            Assert.Equal("qwen3:14b", c.Model);
        }
        finally { w.Close(); }
    }
}
