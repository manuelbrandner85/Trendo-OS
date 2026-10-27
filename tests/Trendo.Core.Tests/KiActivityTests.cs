using Trendo.Core;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>Wunsch 27.10.2026: Fortschritt jeder KI-Bearbeitung – Phase, Wörter, Schätzung, Abbruch.</summary>
public class KiActivityTests
{
    sealed class FakeStream(Func<Action<string>, CancellationToken, Task<string>> run) : IStreamingKiBackend
    {
        public string Name => "Test"; public string BaseUrl => "http://127.0.0.1:1";
        public Task<bool> IsUpAsync(CancellationToken ct = default) => Task.FromResult(true);
        public Task<IReadOnlyList<string>> ModelsAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<string>>(["mini"]);
        public Task<string> ChatAsync(string model, IReadOnlyList<KiMessage> messages, CancellationToken ct = default) => run(_ => { }, ct);
        public Task<string> ChatStreamAsync(string model, IReadOnlyList<KiMessage> messages, Action<string> onDelta, CancellationToken ct = default) => run(onDelta, ct);
    }

    static IntelligenceCore Core(IKiBackend b)
    {
        var d = Path.Combine(Path.GetTempPath(), "trendo-kiact-" + Guid.NewGuid().ToString("N")[..8]); Directory.CreateDirectory(d);
        return new IntelligenceCore(d, new AuditLog(Path.Combine(d, "a.jsonl")), () => [b]);
    }

    [Fact(DisplayName = "KI-Fortschritt: Bezeichnung, Phase „schreibt … Wörter“, Schätzung nur beim Streamen, danach leer und Länge gelernt")]
    public async Task Streams_progress()
    {
        var label = "Brief verbessern " + Guid.NewGuid().ToString("N")[..6];
        var seen = new List<(string Label, string Phase, double? Pct)>();
        var core = Core(new FakeStream(async (delta, ct) =>
        {
            foreach (var w in new[] { "Hallo ", "das ", "ist ", "eine ", "Antwort. " }) { delta(w); var a = KiActivityHub.Running.Single(x => x.Label == label); seen.Add((a.Label, a.Phase, a.Percent)); await Task.Yield(); }
            return "Hallo das ist eine Antwort.";
        }));
        using (KiActivityHub.Label(label))
        {
            var r = await core.AskAsync(new KiRequest("Frage", UseKnowledge: false));
            Assert.True(r.Ok);
        }
        Assert.All(seen, s => Assert.Equal(label, s.Label));
        Assert.StartsWith("schreibt …", seen[^1].Phase);
        Assert.Contains("5 Wörter", seen[^1].Phase);
        Assert.All(seen, s => Assert.NotNull(s.Pct));
        Assert.True(seen[^1].Pct > seen[0].Pct);
        Assert.DoesNotContain(KiActivityHub.Running, x => x.Label == label);
        Assert.Equal(120, KiActivityHub.Estimate(label)); // 27 Zeichen → Untergrenze 120
    }

    [Fact(DisplayName = "KI-Fortschritt: Abbrechen beendet die Antwort, die Anzeige ist danach leer")]
    public async Task Cancel()
    {
        var label = "Abbruchtest " + Guid.NewGuid().ToString("N")[..6];
        var started = new TaskCompletionSource();
        var core = Core(new FakeStream(async (delta, ct) => { delta("Teil "); started.SetResult(); await Task.Delay(Timeout.Infinite, ct); return ""; }));
        Task<KiAnswer> t;
        using (KiActivityHub.Label(label)) t = core.AskAsync(new KiRequest("Frage", UseKnowledge: false));
        await started.Task;
        var a = Assert.Single(KiActivityHub.Running, x => x.Label == label);
        a.Cancel();
        var r = await t;
        Assert.False(r.Ok); Assert.Equal("Abgebrochen.", r.Text);
        Assert.DoesNotContain(KiActivityHub.Running, x => x.Label == label);
    }
}
