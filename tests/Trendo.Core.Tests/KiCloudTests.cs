using Trendo.Core;
using Xunit;
namespace Trendo.Core.Tests;

public class T0225_Ki
{
    const long G = 1024L * 1024 * 1024;

    [Fact(DisplayName = "T-0225: KI-Dienste mit Schlüssel – nur HTTPS, offizielle Schlüsselseite, kostenlos/kostenpflichtig gekennzeichnet")]
    public void Cloud_catalog()
    {
        Assert.True(KiCatalog.Cloud.Count >= 4);
        Assert.Equal(KiCatalog.Cloud.Count, KiCatalog.Cloud.Select(c => c.Id).Distinct().Count());
        Assert.All(KiCatalog.Cloud, c => { Assert.StartsWith("https://", c.BaseUrl); Assert.StartsWith("https://", c.KeyUrl); Assert.StartsWith("TrendoOS:ki-", c.SecretName); Assert.False(string.IsNullOrWhiteSpace(c.Hint)); });
        Assert.Contains(KiCatalog.Cloud, c => c.FreeTier);
        Assert.Contains(KiCatalog.Cloud, c => !c.FreeTier && c.Hint.Contains("Kostenpflichtig"));
    }

    [Fact(DisplayName = "T-0225: Automatisch geladen werden nur freie Lizenzen (Apache-2.0/MIT), passend zum Arbeitsspeicher, kleinste zuerst, Vorhandenes nicht")]
    public void Auto_models()
    {
        var big = KiCatalog.AutoModels(new HardwareInfo(32 * G, null, 0), ["qwen2.5:0.5b"]);
        Assert.NotEmpty(big);
        Assert.All(big, m => Assert.Contains(m.License, new[] { "Apache-2.0", "MIT" }));
        Assert.DoesNotContain(big, m => m.Tag == "qwen2.5:0.5b");
        Assert.DoesNotContain(big, m => m.Tag is "qwen2.5:3b" or "llama3.2:3b" or "gemma2:2b"); // eigene Lizenzbedingungen: nur mit Bestätigung
        Assert.Equal(big.OrderBy(m => m.SizeGb).Select(m => m.Tag), big.Select(m => m.Tag));
        var small = KiCatalog.AutoModels(new HardwareInfo(8 * G, null, 0), []);
        Assert.All(small, m => Assert.True(m.MinRamGb <= 8));
        Assert.Contains(small, m => m.Tag == "qwen2.5:0.5b");
    }

    [Fact(DisplayName = "T-0225: Ersatz-KI-Dienste werden gespeichert, unbekannte Kennungen verworfen, automatisches Laden ist Standard")]
    public void Settings()
    {
        var d = Path.Combine(Path.GetTempPath(), "trendo-ki-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(d);
        try
        {
            var s = KiSourceSettings.Load(d);
            Assert.True(s.AutoModels); Assert.Empty(s.Cloud);
            s.ToggleCloud("mistral"); s.ToggleCloud("gibt-es-nicht"); s.Save(d);
            Assert.Equal(["mistral"], KiSourceSettings.Load(d).Cloud);
            File.WriteAllText(Path.Combine(d, "ki", "quellen.json"), """{"Cloud":["gemini","fremd"],"AutoModels":false}""");
            var r = KiSourceSettings.Load(d);
            Assert.Equal(["gemini"], r.Cloud); Assert.False(r.AutoModels);
        }
        finally { Directory.Delete(d, true); }
    }
}

public class T0225_KiWissen
{
    sealed class Rec(bool remote) : IKiBackend
    {
        public string Name => remote ? "Dienst" : "Lokal"; public string BaseUrl => "x";
        public List<string> Systems = [];
        public Task<bool> IsUpAsync(CancellationToken ct = default) => Task.FromResult(true);
        public Task<IReadOnlyList<string>> ModelsAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<string>>(["m"]);
        public Task<string> ChatAsync(string model, IReadOnlyList<KiMessage> messages, CancellationToken ct = default) { Systems.Add(messages[0].Content); return Task.FromResult("ok"); }
    }
    sealed class RemoteRec() : IRemoteKiBackend
    {
        readonly Rec _r = new(true); public List<string> Systems => _r.Systems;
        public string Name => _r.Name; public string BaseUrl => _r.BaseUrl;
        public Task<bool> IsUpAsync(CancellationToken ct = default) => _r.IsUpAsync(ct);
        public Task<IReadOnlyList<string>> ModelsAsync(CancellationToken ct = default) => _r.ModelsAsync(ct);
        public Task<string> ChatAsync(string model, IReadOnlyList<KiMessage> messages, CancellationToken ct = default) => _r.ChatAsync(model, messages, ct);
    }

    [Fact(DisplayName = "T-0225 Sicherheit: eigene Dokumente („Wissen“) gehen nie an einen KI-Dienst außerhalb des PCs")]
    public async Task Knowledge_stays_local()
    {
        var d = Path.Combine(Path.GetTempPath(), "trendo-kiw-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(d);
        try
        {
            IReadOnlyList<(string, string)> Wissen(string q, int n) => [("Kontoauszug", "IBAN DE00 1234 geheim")];
            var local = new Rec(false);
            var a = await new IntelligenceCore(d, new AuditLog(Path.Combine(d, "a.jsonl")), () => [local], Wissen).AskAsync(new KiRequest("Was steht im Kontoauszug?"));
            Assert.Contains("geheim", local.Systems[0]); Assert.Equal(["Kontoauszug"], a.Sources);
            var remote = new RemoteRec();
            var blocked = await new IntelligenceCore(d, new AuditLog(Path.Combine(d, "c.jsonl")), () => [remote], Wissen).AskAsync(new KiRequest("Was steht im Kontoauszug?"));
            Assert.False(blocked.Ok); Assert.Empty(remote.Systems); // ohne ausdrückliche Erlaubnis nie nach außen
            var b = await new IntelligenceCore(d, new AuditLog(Path.Combine(d, "b.jsonl")), () => [remote], Wissen).AskAsync(new KiRequest("Was steht im Kontoauszug?", AllowRemote: true));
            Assert.DoesNotContain("geheim", remote.Systems[0]); Assert.DoesNotContain("Kontoauszug", remote.Systems[0]); Assert.Empty(b.Sources);
            Assert.Contains("extern", File.ReadAllText(Path.Combine(d, "b.jsonl")));
        }
        finally { Directory.Delete(d, true); }
    }
}
