using System.Security.Cryptography;
using System.Text;
using Trendo.Core;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>T-0712: vorhandene Ollama-Modelle robust finden – erst das laufende Ollama (/api/tags), sonst Manifeste an allen bekannten Orten
/// (OLLAMA_MODELS Prozess + Benutzer, ~/.ollama/models, Trendos Werkzeugordner), auch im neueren Manifest-Format.
/// Echtlauf nur mit TRENDO_ECHT_MODELLE=&lt;Belegordner&gt; (echtes Ollama und echte Manifeste dieses PCs).</summary>
public class KiModelT0712Tests
{
    const long G = 1024L * 1024 * 1024;

    static string NewRoot() => Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "trendo-t0712-" + Guid.NewGuid().ToString("N")[..8])).FullName;

    static void Man(string root, string name, string tag, long size, bool v2 = false)
    {
        var d = v2 ? Path.Combine(root, "manifests-v2", "ollama.com", "library", name) : Path.Combine(root, "manifests", "registry.ollama.ai", "library", name);
        Directory.CreateDirectory(d);
        File.WriteAllText(Path.Combine(d, tag), "{\"schemaVersion\":2,\"layers\":[{\"mediaType\":\"application/vnd.ollama.image.model\",\"size\":" + size + "}]}");
    }

    [Fact(DisplayName = "T-0712: läuft Ollama und meldet Modelle, gilt seine Liste (/api/tags) – Manifeste werden dann nicht gebraucht")]
    public void Api_first()
    {
        var root = NewRoot();
        try
        {
            Man(root, "qwen2.5", "0.5b", 397_807_936);
            var scan = KiModel.Discover(() => [new("qwen3:14b", 9_276_198_993), new("qwen2.5:0.5b", 397_821_319)], [root]);
            Assert.Equal("ollama-api", scan.Source);
            Assert.Equal(["qwen2.5:0.5b", "qwen3:14b"], scan.Models.Select(m => m.Tag));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact(DisplayName = "T-0712: Ollama nicht erreichbar (null) oder ohne Modelle → Manifeste aus allen Ordnern, auch wenn der erste Ordner fehlt")]
    public void Manifests_when_api_unavailable()
    {
        var root = NewRoot();
        try
        {
            Man(root, "qwen3", "14b", 9_276_184_896);
            string[] roots = [Path.Combine(root, "gibt-es-nicht"), root];
            foreach (var api in new Func<IReadOnlyList<InstalledModel>?>?[] { null, () => null, () => [], () => throw new HttpRequestException("verweigert") })
            {
                var scan = KiModel.Discover(api, roots);
                Assert.Equal("manifeste", scan.Source);
                Assert.Equal(["qwen3:14b"], scan.Models.Select(m => m.Tag));
                Assert.Contains(root, scan.Detail);
            }
            var none = KiModel.Discover(() => null, [Path.Combine(root, "gibt-es-nicht")]);
            Assert.Equal("keine", none.Source); Assert.Empty(none.Models);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact(DisplayName = "T-0712: Ordnerreihenfolge – OLLAMA_MODELS (Prozess), OLLAMA_MODELS (Benutzer), ~/.ollama/models, <Werkzeuge>/model; Doppelte nur einmal")]
    public void Root_order()
    {
        var tools = Path.Combine(Path.GetTempPath(), "TrendoWerkzeuge-t0712");
        var home = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".ollama", "models");
        string? Env(string n, EnvironmentVariableTarget t) => n != "OLLAMA_MODELS" ? null : t switch
        {
            EnvironmentVariableTarget.Process => @"D:\KI\prozess",
            EnvironmentVariableTarget.User => @"D:\KI\benutzer\",
            _ => null,
        };
        var roots = KiModel.ModelRoots(tools, Env);
        var expectUser = OperatingSystem.IsWindows() ? new[] { Path.GetFullPath(@"D:\KI\benutzer\") } : [];
        Assert.Equal([Path.GetFullPath(@"D:\KI\prozess"), .. expectUser, home, Path.Combine(tools, "model")], roots);

        // Benutzer-Variable zeigt auf Trendos Ordner → nicht doppelt; leere Variablen zählen nicht
        var same = KiModel.ModelRoots(tools, (n, t) => t == EnvironmentVariableTarget.User ? Path.Combine(tools, "model") + "\\" : "  ");
        Assert.Equal(same.Count, same.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Equal(2, same.Count);
    }

    [Fact(DisplayName = "T-0712: neueres Manifest-Format (manifests-v2/ollama.com) wird gelesen, gleiche Modelle zählen einmal")]
    public void Manifests_v2()
    {
        var root = NewRoot();
        try
        {
            Man(root, "qwen3", "14b", 9_276_184_896, v2: true);
            Man(root, "qwen2.5", "0.5b", 397_807_936, v2: true);
            Man(root, "qwen2.5", "0.5b", 397_807_936);
            var have = KiModel.ScanManifests([root]);
            Assert.Equal(["qwen2.5:0.5b", "qwen3:14b"], have.Select(h => h.Tag));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact(DisplayName = "T-0712: Antwort von /api/tags lesen (Name, Größe; ohne Tag → :latest; kaputte Antwort → null)")]
    public void Parse_tags()
    {
        var list = KiModel.ParseTags("{\"models\":[{\"name\":\"qwen3:14b\",\"model\":\"qwen3:14b\",\"size\":9276198993},{\"name\":\"llama3\",\"size\":5},{\"name\":\"ohne-groesse:1b\"}]}");
        Assert.NotNull(list);
        Assert.Equal(["llama3:latest", "qwen3:14b"], list!.Select(m => m.Tag));
        Assert.Equal(9_276_198_993, list.Single(m => m.Tag == "qwen3:14b").Bytes);
        Assert.Null(KiModel.ParseTags("<html>"));
        Assert.Null(KiModel.ParseTags("{\"fehler\":1}"));
    }

    [Fact(DisplayName = "T-0712: Liste wie Ollama sie auf dem Prüf-PC meldet (9 Modelle) + 32 GB / 12 GB Grafikkarte → qwen3:14b, nicht qwen2.5:0.5b")]
    public void Discovered_list_picks_qwen3()
    {
        IReadOnlyList<InstalledModel> api = KiModel.ParseTags(
            "{\"models\":[{\"name\":\"qwen2.5vl:7b\",\"size\":5969245856},{\"name\":\"qwen3:14b\",\"size\":9276198993},{\"name\":\"qwen2.5-coder:14b\",\"size\":8988124069}," +
            "{\"name\":\"qwen2.5:1.5b\",\"size\":986061892},{\"name\":\"qwen2.5:3b\",\"size\":1929912432},{\"name\":\"qwen2.5-coder:7b\",\"size\":4683087561}," +
            "{\"name\":\"qwen2.5:7b\",\"size\":4683087332},{\"name\":\"mistral:7b\",\"size\":4372824384},{\"name\":\"qwen2.5:0.5b\",\"size\":397821319}]}")!;
        var scan = KiModel.Discover(() => api, []);
        Assert.Equal(9, scan.Models.Count);
        Assert.Equal("qwen3:14b", KiModel.Best(scan.Models, 32 * G, 12).Model);
        Assert.Equal("qwen2.5:0.5b", KiModel.Best(KiModel.Discover(() => null, []).Models, 32 * G, 12).Model); // so wählte die App ohne gefundene Modelle (Befund T-0707)
    }

    // ---------- Echtlauf auf diesem PC ----------

    [Fact(DisplayName = "T-0712 ECHT: Modelle dieses PCs über das laufende Ollama und (getrennt) über die Manifeste finden, Standard = qwen3:14b (nur mit TRENDO_ECHT_MODELLE)")]
    public async Task Real_this_pc()
    {
        if (Environment.GetEnvironmentVariable("TRENDO_ECHT_MODELLE") is not { Length: > 0 } dirVar) { Ueberspringen.Weil("Echtlauf nur mit TRENDO_ECHT_MODELLE=<Belegordner> und laufendem Ollama."); return; }
        var dir = Directory.CreateDirectory(dirVar).FullName;
        var log = new StringBuilder();
        string Anon(string s) => s.Replace(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), @"C:\Users\<user>", StringComparison.OrdinalIgnoreCase);
        log.AppendLine($"T-0712 Echtlauf {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}");
        log.AppendLine($"OLLAMA_MODELS Prozess=[{Environment.GetEnvironmentVariable("OLLAMA_MODELS")}] Benutzer=[{(OperatingSystem.IsWindows() ? Environment.GetEnvironmentVariable("OLLAMA_MODELS", EnvironmentVariableTarget.User) : "-")}]");
        var roots = KiModel.ModelRoots(DataLocation.ToolsRoot());
        foreach (var r in roots) log.AppendLine($"Ordner: {Anon(r)} · vorhanden={Directory.Exists(r)} · Manifeste={KiModel.ScanManifests([r]).Count}");

        using var http = new HttpClient { BaseAddress = new Uri("http://127.0.0.1:11434"), Timeout = TimeSpan.FromSeconds(5) };
        string? raw = null;
        try { raw = await http.GetStringAsync("/api/tags"); } catch (Exception e) when (e is HttpRequestException or TaskCanceledException) { log.AppendLine("GET /api/tags: " + e.GetType().Name); }
        if (raw is not null) log.AppendLine("GET http://127.0.0.1:11434/api/tags (Rohantwort):").AppendLine(raw);
        var ram = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
        var vram = SystemMetrics.GpuMemory()?.TotalGb ?? 0;
        log.AppendLine($"Arbeitsspeicher {ram / (double)G:0.0} GB · Grafikspeicher {vram:0.0} GB");

        var viaApi = KiModel.Discover(() => raw is null ? null : KiModel.ParseTags(raw), roots);
        var viaDisk = KiModel.Discover(null, roots);
        foreach (var (name, s) in new[] { ("1 · laufendes Ollama zuerst (wie die App)", viaApi), ("2 · nur Manifeste (Ollama aus)", viaDisk) })
        {
            var c = KiModel.Best(s.Models, ram, vram);
            log.AppendLine().AppendLine($"## {name}: Quelle={s.Source} · {Anon(s.Detail)}");
            foreach (var m in s.Models.OrderByDescending(m => m.Bytes)) log.AppendLine($"- {m.Tag} {m.Bytes / (double)G:0.00} GB{(KiModel.IsGeneralChat(m.Tag) ? "" : " (Spezialmodell)")}");
            log.AppendLine($"Standard: {c.Model} – {c.Reason}");
        }
        var txt = Anon(log.ToString());
        File.WriteAllText(Path.Combine(dir, "modelle-dieser-pc.log"), txt);
        File.WriteAllText(Path.Combine(dir, "SHA256SUMS"), Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(txt))).ToLowerInvariant() + "  modelle-dieser-pc.log\n");

        Assert.True(raw is not null, "BLOCKIERT – Ollama läuft nicht auf 127.0.0.1:11434.");
        Assert.Equal("ollama-api", viaApi.Source);
        Assert.Equal("qwen3:14b", KiModel.Best(viaApi.Models, ram, vram).Model);
        Assert.Equal("manifeste", viaDisk.Source);
        Assert.Equal("qwen3:14b", KiModel.Best(viaDisk.Models, ram, vram).Model);
    }
}
