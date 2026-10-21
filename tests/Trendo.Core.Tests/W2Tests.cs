using Trendo.Core;
using Xunit;

namespace Trendo.Core.Tests;

public sealed class FakeKi(string name, bool up = true, string[]? models = null, Func<IReadOnlyList<KiMessage>, string>? reply = null) : IKiBackend
{
    public string Name => name;
    public string BaseUrl => "http://127.0.0.1:1";
    public List<IReadOnlyList<KiMessage>> Calls { get; } = [];
    public Task<bool> IsUpAsync(CancellationToken ct = default) => Task.FromResult(up);
    public Task<IReadOnlyList<string>> ModelsAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<string>>(models ?? ["test-modell"]);
    public Task<string> ChatAsync(string model, IReadOnlyList<KiMessage> m, CancellationToken ct = default) { Calls.Add(m); return Task.FromResult(reply?.Invoke(m) ?? $"Antwort von {name}"); }
}

public class CORE02_IntelligenceCore
{
    [Fact(DisplayName = "CORE-02: Rückfall auf die nächste Quelle, Wissen wird eingebaut, Inhalt nicht ins Protokoll")]
    public async Task Fallback_knowledge_privacy()
    {
        using var f = new Fixture();
        var down = new FakeKi("A", up: false); var ok = new FakeKi("B");
        var ic = new IntelligenceCore(f.Root, f.Core.Audit, () => [down, ok], (q, n) => [("Rezept Apfelkuchen", "200 g Mehl")]);
        var a = await ic.AskAsync(new KiRequest("Geheimfrage Apfelkuchen?"));
        Assert.True(a.Ok); Assert.Equal("B", a.Backend); Assert.Equal(["Rezept Apfelkuchen"], a.Sources);
        Assert.Contains("200 g Mehl", ok.Calls[0][0].Content);
        Assert.DoesNotContain(f.Core.Audit.ReadAll(), e => e.Detail.Contains("Geheimfrage"));
        Assert.Contains(f.Core.Audit.ReadAll(), e => e.Action == "ki.ask");
        Assert.Single(ic.History());
        ic.ClearHistory(); Assert.Empty(ic.History());
    }

    [Fact(DisplayName = "CORE-02: Ohne erreichbare KI ehrliche Meldung statt Fantasieantwort")]
    public async Task No_backend()
    {
        using var f = new Fixture();
        var ic = new IntelligenceCore(f.Root, f.Core.Audit, () => [new FakeKi("A", up: false)]);
        var a = await ic.AskAsync(new KiRequest("Hallo"));
        Assert.False(a.Ok); Assert.Contains("keine KI", a.Text);
        Assert.False((await ic.AskAsync(new KiRequest("  "))).Ok);
    }
}
