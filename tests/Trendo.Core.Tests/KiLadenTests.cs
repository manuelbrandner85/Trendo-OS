using System.IO.Compression;
using System.Runtime.InteropServices;
using Trendo.Core;
using Trendo.Core.PromptAction;
using Xunit;

namespace Trendo.Core.Tests;

public class KiLaden_Plan
{
    const long G = 1024L * 1024 * 1024;
    static HardwareInfo Pc(long ram, long vram = 0) => new(ram, vram > 0 ? "GPU" : null, vram, 4);

    [Fact(DisplayName = "KI-Laden: schwacher PC bekommt kleine Modelle, starker PC größere")]
    public void Tiers()
    {
        var weak = LocalAiPlan.Recommend(Pc(6 * G));
        Assert.Equal("klein", LocalAiPlan.TierOf(Pc(6 * G)).Tier);
        Assert.Equal("llama3.2:1b", weak.First(m => m.Job == KiJob.Text).Tag);
        Assert.True(weak.First(m => m.Job == KiJob.Text).SizeGb <= 4);

        var mid = LocalAiPlan.Recommend(Pc(16 * G));
        Assert.Equal("mittel", LocalAiPlan.TierOf(Pc(16 * G)).Tier);
        Assert.Equal("gemma3:4b", mid.First(m => m.Job == KiJob.Text).Tag);

        var strong = LocalAiPlan.Recommend(Pc(32 * G, 12 * G));
        Assert.Equal("groß", LocalAiPlan.TierOf(Pc(32 * G, 12 * G)).Tier);
        Assert.Equal("qwen2.5:14b", strong.First(m => m.Job == KiJob.Text).Tag);
        Assert.Equal(4, strong.Select(m => m.Job).Distinct().Count());
    }

    [Fact(DisplayName = "KI-Laden: nur frei nutzbare Lizenzen, Grafikspeicher zählt mit")]
    public void Licenses_and_vram()
    {
        string[] allowed = ["Apache-2.0", "MIT", "Llama 3.2 Community", "Gemma Terms"];
        Assert.All(KiCatalog.Models, m => Assert.Contains(m.License, allowed));
        Assert.All(KiCatalog.Models, m => Assert.True(m.FreeToUse));
        Assert.Equal("22.10.2026", KiCatalog.CatalogDate);
        var viaVram = LocalAiPlan.FitsPc(KiCatalog.Models.First(m => m.Tag == "qwen2.5:7b"), Pc(4 * G, 8 * G));
        Assert.True(viaVram);
        Assert.False(LocalAiPlan.FitsPc(KiCatalog.Models.First(m => m.Tag == "qwen2.5:14b"), Pc(4 * G, 8 * G)));
    }

    [Fact(DisplayName = "KI-Laden: zu wenig Platz lädt nichts, Abbruch lässt sich fortsetzen")]
    public async Task Disk_cancel_resume()
    {
        using var f = new Fixture();
        var hw = Pc(8 * G);
        var pulls = new List<string>();
        var msg = await ModelFetch.Run(f.Root, hw, 1024, _ => false, (tag, _, _) => { pulls.Add(tag); return Task.FromResult(true); }, CancellationToken.None);
        Assert.Contains("Zu wenig Platz", msg);
        Assert.Empty(pulls);

        var have = new HashSet<string>();
        var n = 0;
        Task<bool> Pull(string tag, IProgress<FetchTick> p, CancellationToken ct)
        {
            n++;
            p.Report(new FetchTick(0.4, "pulling"));
            if (n == 2) throw new OperationCanceledException();
            have.Add(tag);
            return Task.FromResult(true);
        }
        var stopped = await ModelFetch.Run(f.Root, hw, 80 * G, have.Contains, Pull, CancellationToken.None);
        Assert.Contains("Angehalten", stopped);
        var book = DownloadBook.Load(f.Root);
        Assert.Contains(book.Items, i => i.Phase == "fertig");
        Assert.Contains(book.Items, i => i.Phase == "pause");

        var rest = await ModelFetch.Run(f.Root, hw, 80 * G, have.Contains, (tag, p, _) => { p.Report(new FetchTick(1, "success")); have.Add(tag); return Task.FromResult(true); }, CancellationToken.None);
        Assert.Contains("bereit", rest);
        Assert.Equal(4, have.Count);
        Assert.All(DownloadBook.Load(f.Root).Items, i => Assert.Equal("fertig", i.Phase));
    }

    [Fact(DisplayName = "KI-Wahl: lokal zuerst, bei Limit der nächste, Prompt bleibt auf diesem PC")]
    public async Task Router_and_prompt()
    {
        using var f = new Fixture();
        var down = new FakeKi("Lokal", up: false);
        var limited = new NetKi("Gross", 80, limit: true);
        var next = new NetKi("Klein", 20);
        Assert.Equal(["Lokal", "Gross", "Klein"], FreeAiRouter.Order([next, down, limited]).Select(b => b.Name));
        var ic = new IntelligenceCore(f.Root, f.Core.Audit, () => [next, down, limited]);
        var a = await ic.AskAsync(new KiRequest("Hallo"));
        Assert.True(a.Ok); Assert.Equal("Klein", a.Backend); Assert.False(a.OnThisPc);
        Assert.Equal(1, limited.Hits); Assert.Equal(1, next.Hits);
        var off = await ic.AskAsync(new KiRequest("Hallo", AllowOnline: false));
        Assert.False(off.Ok); Assert.Contains("keine KI", off.Text);
        Assert.Equal(1, next.Hits);

        var local = new FakeKi("Daheim");
        var net = new NetKi("Netz", 90);
        var prompt = new IntelligencePromptModel(new IntelligenceCore(f.Root, f.Core.Audit, () => [net, local]), () => true);
        var text = await prompt.CompleteAsync("System", "Frage", CancellationToken.None);
        Assert.Contains("Daheim", text);
        Assert.Equal(0, net.Hits);
    }

    [Fact(DisplayName = "KI-Wahl: Bild ohne Text, Online nur mit beiden Häkchen")]
    public async Task Vision_and_optin()
    {
        using var f = new Fixture();
        var eye = new EyeKi();
        var ic = new IntelligenceCore(f.Root, f.Core.Audit, () => [eye]);
        var a = await ic.AskAsync(new KiRequest("  ", Task: KiJob.Vision, Image: [1, 2, 3]));
        Assert.True(a.Ok); Assert.Equal("Was siehst du?", eye.Seen); Assert.Contains("Apfel", a.Text);
        var opt = new OnlineOptIn();
        opt.Set("groq", true);
        Assert.False(opt.Uses("groq"));
        opt.Allow = true;
        opt.Save(f.Root);
        Assert.True(OnlineOptIn.Load(f.Root).Uses("groq"));
        Assert.DoesNotContain("sk-", File.ReadAllText(Path.Combine(f.Root, "ki", "online.json")));
        Assert.Contains("GitHub Models", FreeOnline.LeftOut.Select(x => x.Name));
        Assert.DoesNotContain(FreeOnline.Providers, p => p.Id is "cerebras" or "huggingface" or "github");
    }

    [Fact(DisplayName = "Ollama: nur 127.0.0.1, Linux-Archiv ohne winget")]
    public async Task Loopback_and_linux_install()
    {
        Assert.Equal("127.0.0.1:11434", LoopbackAi.OllamaHost);
        Assert.DoesNotContain("0.0.0.0", LoopbackAi.OllamaHost);
        var env = new Dictionary<string, string?>();
        LoopbackAi.BindLocal(env);
        Assert.Equal(LoopbackAi.OllamaHost, env["OLLAMA_HOST"]);
        Assert.Equal("1", env["OLLAMA_NO_CLOUD"]);
        Assert.StartsWith("--host 127.0.0.1 --port 8080", LoopbackAi.LlamaArgs("modell.gguf"));
        Assert.False(LoopbackAi.IsLoopback("0.0.0.0:11434"));
        var json = """{"assets":[{"name":"ollama-linux-amd64.tgz","browser_download_url":"https://example.invalid/ollama.tgz"}]}""";
        Assert.Equal("https://example.invalid/ollama.tgz", OllamaRelease.LinuxTar(json, Architecture.X64));
        Assert.Null(OllamaRelease.LinuxTar("""{"assets":[{"name":"ollama-linux-amd64.tgz","browser_download_url":"http://nein"}]}""", Architecture.X64));
        if (OperatingSystem.IsWindows()) return;

        using var f = new Fixture();
        var files = new HashSet<string>();
        var r = new Rec();
        var m = new DependencyManager(f.Core.Audit, r, p => p, files.Contains) { ConsentFile = Path.Combine(f.Root, "c.txt"), ToolsRoot = Path.Combine(f.Root, "werkzeuge") };
        m.GiveConsent(Principal.LocalOwner);
        m.OllamaLinuxUrl = "http://insecure/ollama.tgz";
        Assert.False(await m.InstallAsync(Principal.LocalOwner, DependencyCatalog.Get("ollama")));
        Assert.Empty(r.Calls);
        m.OllamaLinuxUrl = "https://example.invalid/ollama-linux-amd64.tgz";
        r.OnRun = () => { if (r.Calls[^1].F == "tar") files.Add(m.OllamaUserBin); };
        Assert.True(await m.InstallAsync(Principal.LocalOwner, DependencyCatalog.Get("ollama")));
        Assert.Equal("curl", r.Calls[0].F);
        Assert.Contains("https://example.invalid/ollama-linux-amd64.tgz", r.Calls[0].A);
        Assert.Equal("tar", r.Calls[1].F);
        Assert.Contains("-xzf", r.Calls[1].A);
        Assert.DoesNotContain(r.Calls, c => c.A.Contains("0.0.0.0") || c.A.Contains("winget"));

        var live = "{\"assets\":["
            + "{\"name\":\"ollama-linux-amd64-rocm.tar.zst\",\"browser_download_url\":\"https://example.invalid/rocm.tar.zst\"},"
            + "{\"name\":\"ollama-linux-amd64.tar.zst\",\"browser_download_url\":\"https://example.invalid/ollama.tar.zst\"},"
            + "{\"name\":\"ollama-linux-amd64.tgz\",\"browser_download_url\":\"https://example.invalid/old.tgz\"}]}";
        Assert.Equal("https://example.invalid/ollama.tar.zst", OllamaRelease.LinuxTar(live, Architecture.X64));
        Assert.Equal("https://example.invalid/arm.tar.zst", OllamaRelease.LinuxTar(
            """{"assets":[{"name":"ollama-linux-arm64.tar.zst","browser_download_url":"https://example.invalid/arm.tar.zst"}]}""", Architecture.Arm64));

        var miss = new Rec { FailZstd = true };
        var mm = new DependencyManager(f.Core.Audit, miss, p => p, _ => false) { ConsentFile = Path.Combine(f.Root, "c.txt"), ToolsRoot = Path.Combine(f.Root, "werkzeuge-fehlt") };
        mm.GiveConsent(Principal.LocalOwner);
        mm.OllamaLinuxUrl = "https://example.invalid/ollama-linux-amd64.tar.zst";
        Assert.False(await mm.InstallAsync(Principal.LocalOwner, DependencyCatalog.Get("ollama")));
        Assert.Equal("zstd", Assert.Single(miss.Calls).F);

        var filesZ = new HashSet<string>();
        var z = new Rec();
        var mz = new DependencyManager(f.Core.Audit, z, p => p, filesZ.Contains) { ConsentFile = Path.Combine(f.Root, "c.txt"), ToolsRoot = Path.Combine(f.Root, "werkzeuge-z") };
        mz.GiveConsent(Principal.LocalOwner);
        mz.OllamaLinuxUrl = "https://example.invalid/ollama-linux-amd64.tar.zst";
        z.OnRun = () => { if (z.Calls[^1].F == "tar") filesZ.Add(mz.OllamaUserBin); };
        Assert.True(await mz.InstallAsync(Principal.LocalOwner, DependencyCatalog.Get("ollama")));
        Assert.Equal("zstd", z.Calls[0].F);
        Assert.Equal("curl", z.Calls[1].F);
        Assert.Contains("--zstd", z.Calls[2].A);
    }

    [Fact(DisplayName = "Modellsuche findet ein geladenes Modell auch unter OLLAMA_MODELS")]
    public void HasModel_honours_models_dir()
    {
        using var f = new Fixture();
        var models = Path.Combine(f.Root, "models");
        var manifest = Path.Combine(models, "manifests", "registry.ollama.ai", "library", "qwen2.5", "0.5b");
        Directory.CreateDirectory(Path.GetDirectoryName(manifest)!);
        File.WriteAllText(manifest, "{}");
        var prev = Environment.GetEnvironmentVariable("OLLAMA_MODELS");
        try
        {
            Environment.SetEnvironmentVariable("OLLAMA_MODELS", models);
            var m = new DependencyManager(f.Core.Audit, new Rec());
            Assert.True(m.HasModel("qwen2.5:0.5b"));
            Assert.False(m.HasModel("qwen2.5"));
            Assert.False(m.HasModel("qwen2.5:3b"));
        }
        finally { Environment.SetEnvironmentVariable("OLLAMA_MODELS", prev); }
    }

    [Fact(DisplayName = "Schlüsseldatei bleibt aus der Sicherung und ist nur für den Benutzer lesbar")]
    public void Secret_file_and_backup()
    {
        using var f = new Fixture();
        var path = Path.Combine(f.Root, ".schluessel");
        var store = new FileSecretStore(path);
        store.Set("TrendoOS:key-groq", "geheim-key");
        Assert.Equal("geheim-key", new FileSecretStore(path).Get("TrendoOS:key-groq"));
        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(path));
        File.WriteAllText(Path.Combine(f.Root, "sichtbar.txt"), "hallo");
        var zip = new Backup(f.Root, Path.Combine(f.Root, "backups"), f.Core.Audit).Create(new DateTime(2026, 10, 22, 12, 0, 0));
        using var z = ZipFile.OpenRead(zip);
        Assert.Contains(z.Entries, e => e.FullName == "sichtbar.txt");
        Assert.DoesNotContain(z.Entries, e => e.FullName.Contains("schluessel", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(f.Core.Audit.ReadAll(), e => e.Detail.Contains("geheim-key"));
    }

    sealed class Rec : IProcessRunner
    {
        public List<(string F, string A)> Calls = [];
        public Action? OnRun;
        public bool FailZstd;
        public Task<(int Exit, string Output)> RunAsync(string file, string args, Action<string>? line = null, CancellationToken ct = default)
        { Calls.Add((file, args)); OnRun?.Invoke(); return Task.FromResult((FailZstd && file == "zstd" ? 1 : 0, "")); }
    }

    sealed class NetKi(string name, int quality, bool limit = false) : IKiBackend
    {
        public string Name => name;
        public string BaseUrl => "https://example.test";
        public bool Local => false;
        public int Quality => quality;
        public int Hits { get; private set; }
        public Task<bool> IsUpAsync(CancellationToken ct = default) => Task.FromResult(true);
        public Task<IReadOnlyList<string>> ModelsAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<string>>(["m"]);
        public Task<string> ChatAsync(string model, IReadOnlyList<KiMessage> messages, CancellationToken ct = default)
        {
            Hits++;
            if (limit) throw new KiLimitException("leer");
            return Task.FromResult("online");
        }
    }

    sealed class EyeKi : IKiBackend
    {
        public string Name => "Auge";
        public string BaseUrl => "http://127.0.0.1:1";
        public string Seen { get; private set; } = "";
        public bool Serves(KiJob job) => job == KiJob.Vision;
        public Task<bool> IsUpAsync(CancellationToken ct = default) => Task.FromResult(true);
        public Task<IReadOnlyList<string>> ModelsAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<string>>(["moondream"]);
        public Task<string> ChatAsync(string model, IReadOnlyList<KiMessage> messages, CancellationToken ct = default) => Task.FromResult("text");
        public Task<string> SeeAsync(string model, string question, byte[] image, CancellationToken ct = default)
        { Seen = question; return Task.FromResult("ein Apfel"); }
    }
}
