using Trendo.Core;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>T-0703: bestes vorhandenes Modell als Standard (Auswahlregel mit nachgebildeten Modelllisten) und Ausweichen auf kleinere Modelle.</summary>
public class KiModelT0703Tests
{
    const long G = 1024L * 1024 * 1024;
    const long M = 1024L * 1024;

    /// <summary>Die Liste dieses Prüf-PCs (Größen aus den Ollama-Manifesten, 16.11.2026) plus ein Bild- und ein Suchmodell.</summary>
    static readonly InstalledModel[] PcList =
    [
        new("qwen3:14b", 9_276_184_896), new("qwen2.5-coder:14b", 8_988_110_656), new("qwen2.5:7b", 4_683_073_184), new("qwen2.5-coder:7b", 4_683_073_408),
        new("mistral:7b", 4_372_812_000), new("qwen2.5:3b", 1_929_900_000), new("qwen2.5:1.5b", 986_046_272), new("qwen2.5:0.5b", 397_807_936),
        new("qwen2.5vl:7b", 5_969_233_408), new("nomic-embed-text:latest", 274_290_656),
    ];

    [Fact(DisplayName = "T-0703: 32 GB + 12 GB Grafikkarte → qwen3:14b (größtes allgemeines Modell, das passt), nicht Coder, Bild- oder Suchmodell")]
    public void Best_on_this_pc()
    {
        var c = KiModel.Best(PcList, 32 * G, 12);
        Assert.Equal("qwen3:14b", c.Model);
        Assert.False(c.ByUser);
        Assert.Contains("Grafikspeicher (12 GB)", c.Reason);
        // Spezialmodelle werden nie Standard, auch wenn sie größer sind
        var withBigCoder = PcList.Append(new InstalledModel("qwen2.5-coder:32b", 10 * G)).ToArray();
        Assert.Equal("qwen3:14b", KiModel.Best(withBigCoder, 64 * G, 24).Model);
        Assert.False(KiModel.IsGeneralChat("qwen2.5vl:7b"));
        Assert.False(KiModel.IsGeneralChat("qwen2.5-coder:7b"));
        Assert.False(KiModel.IsGeneralChat("nomic-embed-text:latest"));
        Assert.False(KiModel.IsGeneralChat("llama3.2-vision:latest"));
        Assert.False(KiModel.IsGeneralChat("translategemma:4b"));
        Assert.True(KiModel.IsGeneralChat("gemma3:4b")); // steht in der Bibliothek auch unter Chat
        Assert.True(KiModel.IsGeneralChat("deepseek-r1:8b"));
        Assert.True(KiModel.IsGeneralChat("qwen3:14b")); // nicht im Katalog → nach Namen allgemein
    }

    [Fact(DisplayName = "T-0703: Grafik- und Arbeitsspeicher begrenzen – 6 GB Grafik → 7B, 16 GB ohne Grafik → 3B, 8 GB ohne Grafik → kleines Modell bleibt")]
    public void Fits_hardware()
    {
        Assert.Equal("qwen2.5:7b", KiModel.Best(PcList, 32 * G, 6).Model);
        Assert.Equal("qwen2.5:3b", KiModel.Best(PcList, 16 * G, 0).Model);
        Assert.Equal("qwen2.5:7b", KiModel.Best(PcList, 32 * G, 0).Model); // ohne Grafikkarte höchstens 5 GB
        var weak = KiModel.Best(PcList, 8 * G, 0);
        Assert.Equal("qwen2.5:0.5b", weak.Model);
        Assert.Contains("wenig Arbeitsspeicher", weak.Reason);
        Assert.Equal("qwen2.5:0.5b", KiModel.Best(PcList, 8 * G, 2).Model); // kleine Grafikkarte zählt nicht
        // Schwacher PC ohne 0.5B: das kleinste vorhandene
        Assert.Equal("qwen2.5:1.5b", KiModel.Best(PcList.Where(m => m.Tag != "qwen2.5:0.5b").ToArray(), 8 * G, 0).Model);
        // Nichts passt: kleinstes allgemeines Modell
        Assert.Equal("qwen2.5:7b", KiModel.Best([new("qwen3:14b", 9 * G), new("qwen2.5:7b", 4_683_073_184)], 16 * G, 0).Model);
        // Kein allgemeines Modell: das kleine Standardmodell (wird nachgeladen)
        Assert.Equal(DependencyCatalog.LocalModel, KiModel.Best([new("qwen2.5vl:7b", 6 * G)], 32 * G, 12).Model);
    }

    [Fact(DisplayName = "T-0703: eigene Wahl hat Vorrang, „Automatisch“ hebt sie auf; unbekanntes Modell wird abgelehnt")]
    public void User_choice_wins()
    {
        using var f = new Fixture();
        Assert.Equal("qwen3:14b", KiModel.Choose(f.Core.DataRoot, 32 * G, 12, PcList).Model);
        KiModel.SelectInstalled(f.Core.DataRoot, "mistral:7b", PcList, f.Core.Audit);
        var c = KiModel.Choose(f.Core.DataRoot, 32 * G, 12, PcList);
        Assert.Equal("mistral:7b", c.Model); Assert.True(c.ByUser); Assert.Contains("Von dir gewählt", c.Reason);
        Assert.Throws<ArgumentException>(() => KiModel.SelectInstalled(f.Core.DataRoot, "llama3:70b", PcList, f.Core.Audit));
        Assert.Throws<ArgumentException>(() => KiModel.SelectInstalled(f.Core.DataRoot, "../böse", PcList, f.Core.Audit));
        // Alte Wahl (0.5B aus „Hardware“-Ratschlag) gilt weiter
        KiModel.Select(f.Core.DataRoot, DependencyCatalog.LocalModel, f.Core.Audit);
        Assert.Equal(DependencyCatalog.LocalModel, KiModel.Choose(f.Core.DataRoot, 32 * G, 12, PcList).Model);
        KiModel.SelectAuto(f.Core.DataRoot, f.Core.Audit);
        Assert.Equal("qwen3:14b", KiModel.Choose(f.Core.DataRoot, 32 * G, 12, PcList).Model);
        // Gewähltes Modell nicht mehr da → automatisch
        KiModel.SelectInstalled(f.Core.DataRoot, "mistral:7b", PcList, f.Core.Audit);
        Assert.Equal("qwen3:14b", KiModel.Choose(f.Core.DataRoot, 32 * G, 12, PcList.Where(m => m.Tag != "mistral:7b").ToArray()).Model);
        Assert.Contains(f.Core.Audit.ReadAll(), e => e.Action == "ki.model_selected" && e.Detail == "automatisch");
    }

    [Fact(DisplayName = "T-0703: Modell, das nicht lädt oder zu langsam ist, wird übersprungen; Ausweichliste größtes kleineres + kleinstes")]
    public void Demote_and_fallbacks()
    {
        using var f = new Fixture();
        Assert.Equal(["qwen2.5:7b", "qwen2.5:0.5b"], KiModel.Fallbacks("qwen3:14b", PcList));
        Assert.Equal(["qwen2.5:0.5b"], KiModel.Fallbacks("qwen2.5:1.5b", PcList));
        Assert.Empty(KiModel.Fallbacks("qwen2.5:0.5b", PcList));
        KiModel.Demote(f.Core.DataRoot, "qwen3:14b", "keine Antwort nach 90 s", f.Core.Audit);
        var c = KiModel.Choose(f.Core.DataRoot, 32 * G, 12, PcList);
        Assert.Equal("qwen2.5:7b", c.Model);
        Assert.Contains("qwen3:14b wird übersprungen", c.Reason);
        Assert.Contains(f.Core.Audit.ReadAll(), e => e.Action == "ki.model.fallback");
    }

    [Fact(DisplayName = "T-0703: vorhandene Modelle aus Ollama-Manifesten lesen (Größe der Gewichte, auch Projektor)")]
    public void Scan_manifests()
    {
        var root = Path.Combine(Path.GetTempPath(), "trendo-t0703-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            void Man(string name, string tag, string layers)
            {
                var d = Path.Combine(root, "manifests", "registry.ollama.ai", "library", name); Directory.CreateDirectory(d);
                File.WriteAllText(Path.Combine(d, tag), "{\"schemaVersion\":2,\"layers\":[" + layers + "]}");
            }
            Man("qwen3", "14b", "{\"mediaType\":\"application/vnd.ollama.image.model\",\"size\":9276184896},{\"mediaType\":\"application/vnd.ollama.image.license\",\"size\":11338}");
            Man("llava", "7b", "{\"mediaType\":\"application/vnd.ollama.image.model\",\"size\":4000000000},{\"mediaType\":\"application/vnd.ollama.image.projector\",\"size\":600000000}");
            Man("kaputt", "x", "nicht json");
            var have = KiModel.ScanManifests([root, Path.Combine(root, "gibt-es-nicht")]);
            Assert.Equal(["llava:7b", "qwen3:14b"], have.Select(h => h.Tag));
            Assert.Equal(9_276_184_896, have.Single(h => h.Tag == "qwen3:14b").Bytes);
            Assert.Equal(4_600_000_000, have.Single(h => h.Tag == "llava:7b").Bytes);
        }
        finally { try { Directory.Delete(root, true); } catch (IOException) { } }
    }

    sealed class Fake(Dictionary<string, Func<Action<string>, CancellationToken, Task<string>>> runs) : IStreamingKiBackend
    {
        public List<string> Asked { get; } = [];
        public string Name => "Ollama"; public string BaseUrl => "http://127.0.0.1:1";
        public Task<bool> IsUpAsync(CancellationToken ct = default) => Task.FromResult(true);
        public Task<IReadOnlyList<string>> ModelsAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<string>>(runs.Keys.ToList());
        public Task<string> ChatAsync(string model, IReadOnlyList<KiMessage> messages, CancellationToken ct = default) => ChatStreamAsync(model, messages, _ => { }, ct);
        public Task<string> ChatStreamAsync(string model, IReadOnlyList<KiMessage> messages, Action<string> onDelta, CancellationToken ct = default) { Asked.Add(model); return runs[model](onDelta, ct); }
    }

    static (IntelligenceCore Core, List<(string, string)> Failed) Core(Fake b, TimeSpan timeout)
    {
        var d = Path.Combine(Path.GetTempPath(), "trendo-t0703-" + Guid.NewGuid().ToString("N")[..8]); Directory.CreateDirectory(d);
        var failed = new List<(string, string)>();
        var core = new IntelligenceCore(d, new AuditLog(Path.Combine(d, "a.jsonl")), () => [b])
        {
            LocalFallbacks = m => m == "gross" ? ["mittel", "klein"] : m == "mittel" ? ["klein"] : [],
            LocalFirstAnswerTimeout = timeout,
            LocalModelFailed = (m, why) => failed.Add((m, why)),
        };
        return (core, failed);
    }

    [Fact(DisplayName = "T-0703: großes Modell lädt nicht → Antwort vom nächstkleineren, Fehlschlag wird gemeldet")]
    public async Task Fallback_when_load_fails()
    {
        var b = new Fake(new()
        {
            ["gross"] = (_, _) => throw new InvalidOperationException("model requires more system memory (10.2 GiB) than is available"),
            ["mittel"] = (d, _) => { d("42"); return Task.FromResult("42"); },
            ["klein"] = (d, _) => { d("32"); return Task.FromResult("32"); },
        });
        var (core, failed) = Core(b, TimeSpan.FromSeconds(30));
        var a = await core.AskAsync(new KiRequest("Was ist 17 + 25?", PreferredModel: "gross", UseKnowledge: false));
        Assert.True(a.Ok); Assert.Equal("42", a.Text); Assert.Equal("mittel", a.Model);
        Assert.Equal(["gross", "mittel"], b.Asked);
        Assert.Equal("gross", Assert.Single(failed).Item1);
    }

    [Fact(DisplayName = "T-0703: großes Modell schweigt bis zur Zeitgrenze → Ausweichen; Nachdenken zählt als Lebenszeichen")]
    public async Task Fallback_on_timeout_but_thinking_keeps_alive()
    {
        var hang = new Fake(new()
        {
            ["gross"] = async (_, ct) => { await Task.Delay(Timeout.Infinite, ct); return ""; },
            ["klein"] = (d, _) => { d("42"); return Task.FromResult("42"); },
        });
        var (core, failed) = Core(hang, TimeSpan.FromMilliseconds(300));
        var a = await core.AskAsync(new KiRequest("Frage", PreferredModel: "gross", UseKnowledge: false));
        Assert.True(a.Ok); Assert.Equal("klein", a.Model);
        Assert.StartsWith("keine Antwort nach", Assert.Single(failed).Item2);

        var thinker = new Fake(new()
        {
            // Gesamtdauer ca. 2,3 s – mehr als die Zeitgrenze (1 s); nur die Lebenszeichen alle 150 ms verhindern das Ausweichen.
            // (vorher 300 ms Grenze / 100 ms Takt: im vollen Testlauf unter Last wurde ein Takt länger als die Grenze)
            // T-0771: Das erste Lebenszeichen hebt die Zeitgrenze auf (IntelligenceCore). Es kam bisher nach „await Task.Delay(150)“ – unter Volllast
            // ist der Thread-Pool ausgelastet, die Fortsetzung lief erst nach über 1 s, also nach der Grenze. Jetzt wartet der erste Takt auf dem
            // aufrufenden Thread selbst (Thread.Sleep, ohne Thread-Pool) – gleiche 150 ms, gleiche Grenze, gleiche Aussage.
            ["gross"] = async (d, ct) => { Thread.Sleep(150); d(""); for (var i = 0; i < 15; i++) { await Task.Delay(150, ct); d(""); } d("42"); return "42"; },
            ["klein"] = (d, _) => { d("32"); return Task.FromResult("32"); },
        });
        var (core2, failed2) = Core(thinker, TimeSpan.FromMilliseconds(1000));
        var b2 = await core2.AskAsync(new KiRequest("Frage", PreferredModel: "gross", UseKnowledge: false));
        Assert.True(b2.Ok); Assert.Equal("gross", b2.Model); Assert.Equal("42", b2.Text);
        Assert.Empty(failed2);
    }

    [Fact(DisplayName = "T-0703: ohne Ausweichliste bleibt alles wie bisher (keine Zeitgrenze, Fehler bricht ab); Abbruch durch die Person weicht nicht aus")]
    public async Task No_fallback_without_list_and_user_cancel()
    {
        var b = new Fake(new()
        {
            ["gross"] = async (_, ct) => { await Task.Delay(Timeout.Infinite, ct); return ""; },
            ["klein"] = (d, _) => { d("x"); return Task.FromResult("x"); },
        });
        // Abbruch durch die Person (0,5 s) kommt vor der Zeitgrenze (2 s) – gleiche Aussage wie vorher 100/200 ms, aber im vollen Testlauf unter Last stabil
        var (core, failed) = Core(b, TimeSpan.FromMilliseconds(2000));
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => core.AskAsync(new KiRequest("Frage", PreferredModel: "gross", UseKnowledge: false), cts.Token));
        Assert.Empty(failed);
        Assert.Equal(["gross"], b.Asked);

        var d = Path.Combine(Path.GetTempPath(), "trendo-t0703-" + Guid.NewGuid().ToString("N")[..8]); Directory.CreateDirectory(d);
        var plain = new IntelligenceCore(d, new AuditLog(Path.Combine(d, "a.jsonl")), () => [new Fake(new() { ["gross"] = (_, _) => throw new InvalidOperationException("kaputt"), ["klein"] = (x, _) => Task.FromResult("x") })]);
        var r = await plain.AskAsync(new KiRequest("Frage", PreferredModel: "gross", UseKnowledge: false));
        Assert.False(r.Ok);
    }

    [Fact(DisplayName = "T-0703: Schlagworte hinter dem letzten Satz (echte Antwort von qwen2.5vl:7b) werden erkannt; „Tags:“ mitten im Satz nicht")]
    public void Inline_tags_from_real_answer()
    {
        // wörtlich aus dem Echtlauf reports/T-0703/vlm-beschreibung.md (vor der Korrektur: keine Schlagworte erkannt)
        var (d, t) = Trendo.Core.Kreativ.VisionTags.ParseDescription("Im Bild ist ein Hund auf einer Veranda zu sehen, der neben einem roten Fahrrad sitzt. Im Hintergrund ist ein geparktes Auto und Bäume sichtbar. Die Szene wirkt friedlich und alltäglich. Schlagworte: Hund, Veranda, Fahrrad, Auto, Bäume, Frieden, Alltag");
        Assert.Equal(["Hund", "Veranda", "Fahrrad", "Auto", "Bäume", "Frieden", "Alltag"], t);
        Assert.EndsWith("friedlich und alltäglich.", d);
        var (d2, t2) = Trendo.Core.Kreativ.VisionTags.ParseDescription("Ein Schild mit der Aufschrift tags: offen bis 18 Uhr");
        Assert.Empty(t2); Assert.Contains("tags: offen", d2);
    }
}
