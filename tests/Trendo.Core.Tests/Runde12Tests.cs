using Trendo.Core;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>Alle Studios, Runde 12 – X-05: kostenlose KI weicht auf ein anderes kostenloses Modell aus.</summary>
public class Runde12Tests
{
    sealed class Free(string[] models, Func<string, string> answer, bool canSwitch = true) : IKiFailoverBackend
    {
        public bool CanSwitchModel => canSwitch;
        public List<string> Asked { get; } = [];
        public string Name => "Frei"; public string BaseUrl => "https://frei.example";
        public Task<bool> IsUpAsync(CancellationToken ct = default) => Task.FromResult(true);
        public Task<IReadOnlyList<string>> ModelsAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<string>>(models);
        public Task<string> ChatAsync(string model, IReadOnlyList<KiMessage> messages, CancellationToken ct = default) { Asked.Add(model); return Task.FromResult(answer(model)); }
    }

    sealed class Paid(Exception error) : IRemoteKiBackend
    {
        public List<string> Asked { get; } = [];
        public string Name => "Bezahlt"; public string BaseUrl => "https://bezahlt.example";
        public Task<bool> IsUpAsync(CancellationToken ct = default) => Task.FromResult(true);
        public Task<IReadOnlyList<string>> ModelsAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<string>>(["teuer-1", "teuer-2"]);
        public Task<string> ChatAsync(string model, IReadOnlyList<KiMessage> messages, CancellationToken ct = default) { Asked.Add(model); throw error; }
    }

    static IntelligenceCore Core(Fixture f, params IKiBackend[] b) => new(f.Root, f.Core.Audit, () => b);
    static KiRequest Frage(string? preferred = null) => new("Hallo?", PreferredModel: preferred, AllowRemote: true);
    static string Busy(string _) => throw new KiModelBusyException("ausgelastet", false);

    [Fact(DisplayName = "Kostenlose KI: ausgelastetes Modell wird übersprungen, das nächste antwortet")]
    public async Task Fails_over()
    {
        using var f = new Fixture();
        var b = new Free(["a", "b", "c"], m => m == "b" ? "Antwort von b" : Busy(m));
        var r = await Core(f, b).AskAsync(Frage());
        Assert.True(r.Ok, r.Text); Assert.Equal("Antwort von b", r.Text); Assert.Equal("b", r.Model); Assert.Equal(["a", "b"], b.Asked);
    }

    [Fact(DisplayName = "Kostenlose KI: gewähltes Modell zuerst; höchstens drei Versuche je Frage; danach nächste Quelle")]
    public async Task Limits_tries()
    {
        using var f = new Fixture();
        var b = new Free(["a", "b", "c", "d", "e"], Busy);
        var ersatz = new Free(["x"], _ => "Ersatz");
        var r = await Core(f, b, ersatz).AskAsync(Frage("c"));
        Assert.Equal("Ersatz", r.Text);
        Assert.Equal(["c", "a", "b"], b.Asked); Assert.Equal(IntelligenceCore.MaxFreeModelTries, b.Asked.Count);
    }

    [Fact(DisplayName = "Kostenlose KI: Grenze des ganzen Kontos beendet die Versuche sofort")]
    public async Task Account_limit_stops()
    {
        using var f = new Fixture();
        var b = new Free(["a", "b", "c"], _ => throw new KiModelBusyException("Grenze", true));
        var r = await Core(f, b).AskAsync(Frage());
        Assert.False(r.Ok); Assert.Equal(["a"], b.Asked);
    }

    [Fact(DisplayName = "Kostenlose KI: andere Fehler und bezahlte Dienste wechseln nie das Modell")]
    public async Task Never_for_paid_or_other_errors()
    {
        using var f = new Fixture();
        var free = new Free(["a", "b"], _ => throw new InvalidOperationException("Schlüssel ungültig"));
        Assert.False((await Core(f, free).AskAsync(Frage())).Ok); Assert.Equal(["a"], free.Asked);
        var paid = new Paid(new KiModelBusyException("ausgelastet", false));
        Assert.False((await Core(f, paid).AskAsync(Frage())).Ok); Assert.Equal(["teuer-1"], paid.Asked);
        var locked = new Free(["a", "b"], Busy, canSwitch: false);
        Assert.False((await Core(f, locked).AskAsync(Frage())).Ok); Assert.Equal(["a"], locked.Asked);
    }
}
