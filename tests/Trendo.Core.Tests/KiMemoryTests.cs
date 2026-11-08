using Trendo.Core;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>CORE-02: Gedächtnis mit Kontrolle.</summary>
public class KiMemoryTests
{
    sealed class Spy(string name, bool remote) : IKiBackend
    {
        public List<IReadOnlyList<KiMessage>> Calls { get; } = [];
        public bool Remote => remote;
        public string Name => name; public string BaseUrl => "x";
        public Task<bool> IsUpAsync(CancellationToken ct = default) => Task.FromResult(true);
        public Task<IReadOnlyList<string>> ModelsAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<string>>(["m"]);
        public Task<string> ChatAsync(string model, IReadOnlyList<KiMessage> messages, CancellationToken ct = default) { Calls.Add(messages); return Task.FromResult("ok"); }
    }

    sealed class RemoteSpy : IRemoteKiBackend
    {
        public List<IReadOnlyList<KiMessage>> Calls { get; } = [];
        public string Name => "Internet"; public string BaseUrl => "x";
        public Task<bool> IsUpAsync(CancellationToken ct = default) => Task.FromResult(true);
        public Task<IReadOnlyList<string>> ModelsAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<string>>(["m"]);
        public Task<string> ChatAsync(string model, IReadOnlyList<KiMessage> messages, CancellationToken ct = default) { Calls.Add(messages); return Task.FromResult("ok"); }
    }

    [Theory(DisplayName = "Gedächtnis: erkennt eigene Aussagen über die Person")]
    [InlineData("Hallo, ich heiße Erika Muster.", "Heißt Erika Muster")]
    [InlineData("Mein Name ist Erika", "Heißt Erika")]
    [InlineData("Ich wohne in Berlin.", "Wohnt in Berlin")]
    [InlineData("Ich arbeite als Tischlerin", "Arbeitet als Tischlerin")]
    [InlineData("ich arbeite bei den Stadtwerken!", "Arbeitet bei den Stadtwerken")]
    [InlineData("Ich bin von Beruf Lehrerin.", "Arbeitet als Lehrerin")]
    [InlineData("Meine Hobbys sind Lesen und Wandern", "Hobbys: Lesen und Wandern")]
    [InlineData("Ich mag Katzen.", "Mag Katzen")]
    [InlineData("Antworte mir immer kurz.", "Wunsch: immer kurz")]
    [InlineData("Merk dir, dass der Hund Bello heißt.", "Der Hund Bello heißt")]
    [InlineData("Bitte merk dir: Kuchen am Freitag", "Kuchen am Freitag")]
    public void Recognizes(string text, string expected) => Assert.Equal([expected], KiMemoryStore.Extract(text));

    [Theory(DisplayName = "Gedächtnis: Fragen, fremde Sätze und Allerweltssätze werden nicht gemerkt")]
    [InlineData("Wie heiße ich?")]
    [InlineData("Ich wohne in Berlin?")]
    [InlineData("Stell dir vor, ich heiße Erika.")]
    [InlineData("Mein Freund sagt, ich arbeite als Koch.")]
    [InlineData("Ich mag das nicht.")]
    [InlineData("Ich mag es, wenn du kurz schreibst.")]
    [InlineData("Schreibe einen Brief an meine Bank.")]
    [InlineData("")]
    public void Ignores(string text) => Assert.Empty(KiMemoryStore.Extract(text));

    [Theory(DisplayName = "Gedächtnis: Geheimnisse nie, auch nicht auf ausdrücklichen Wunsch")]
    [InlineData("Merk dir: mein Passwort ist Sonne123")]
    [InlineData("Merk dir meine PIN 1234")]
    [InlineData("Merk dir DE89 3704 0044 0532 0130 00")]
    [InlineData("Merk dir 4111 1111 1111 1111")]
    [InlineData("Merk dir den API-Key abc")]
    public void Never_secrets(string text) => Assert.Empty(KiMemoryStore.Extract(text));

    [Theory(DisplayName = "Gedächtnis: besonders Persönliches nur auf ausdrückliches „merk dir“")]
    [InlineData("Ich mag die CDU.", "Merk dir: ich wähle die CDU")]
    [InlineData("Ich arbeite als Therapie-Helferin.", "Merk dir: ich mache eine Therapie")]
    public void Special_only_explicit(string auto, string asked)
    {
        Assert.Empty(KiMemoryStore.Extract(auto));
        Assert.Single(KiMemoryStore.Extract(asked));
    }

    [Fact(DisplayName = "Gedächtnis: von Haus aus aus; lernt erst nach dem Einschalten; ausgeschaltet bleibt alles sichtbar und wird nicht genutzt")]
    public void Off_by_default()
    {
        using var f = new Fixture();
        var m = new KiMemoryStore(f.Root, f.Core.Audit);
        Assert.False(m.Enabled); Assert.Empty(m.Learn("Ich wohne in Berlin.")); Assert.Empty(m.All());
        m.SetEnabled(true);
        Assert.Single(m.Learn("Ich wohne in Berlin."));
        Assert.Contains("Wohnt in Berlin", m.PromptBlock());
        m.SetEnabled(false);
        Assert.Equal("", m.PromptBlock()); Assert.Single(m.All());
        Assert.Empty(new KiMemoryStore(f.Root).Learn("Ich mag Katzen."));
    }

    [Fact(DisplayName = "Gedächtnis: doppelt nicht; Name und Wohnort ersetzen; löschen und alles vergessen; übersteht einen Neustart")]
    public void Control()
    {
        using var f = new Fixture();
        var m = new KiMemoryStore(f.Root); m.SetEnabled(true);
        m.Learn("Ich wohne in Berlin."); Assert.Empty(m.Learn("ich wohne in berlin"));
        m.Learn("Ich wohne in Köln."); Assert.Equal(["Wohnt in Köln"], m.All().Select(x => x.Text));
        var k = m.Add("Mag Tee")!; Assert.Equal(2, new KiMemoryStore(f.Root).All().Count);
        Assert.True(m.Remove(k.Id)); Assert.False(m.Remove(k.Id));
        Assert.Null(m.Add("Passwort: x")); Assert.Null(m.Add("  "));
        m.Clear(); Assert.Empty(new KiMemoryStore(f.Root).All());
    }

    [Fact(DisplayName = "Gedächtnis: höchstens 100 Einträge, die neuesten bleiben; kaputte Datei stört nicht; Inhalt steht nicht im Protokoll")]
    public void Limits_and_privacy()
    {
        using var f = new Fixture();
        var m = new KiMemoryStore(f.Root, f.Core.Audit); m.SetEnabled(true);
        var t0 = DateTimeOffset.Now;
        for (var i = 0; i < KiMemoryStore.MaxFacts + 5; i++) m.Add("Notiz Nummer " + i, t0.AddMinutes(i));
        var all = m.All(); Assert.Equal(KiMemoryStore.MaxFacts, all.Count); Assert.Equal("Notiz Nummer 104", all[0].Text); Assert.DoesNotContain(all, x => x.Text == "Notiz Nummer 0");
        Assert.True(m.PromptBlock().Length < KiMemoryStore.MaxPrompt + 400);
        Assert.DoesNotContain("Notiz Nummer", File.ReadAllText(f.Core.Audit.FilePath));
        File.WriteAllText(Path.Combine(f.Root, "ki", "gedaechtnis.json"), "{kaputt");
        Assert.False(m.Enabled); Assert.Empty(m.All());
    }

    [Fact(DisplayName = "Gedächtnis: geht an die KI auf dem PC, nie an eine KI im Internet und nur bei persönlichen Anfragen")]
    public async Task Stays_local()
    {
        using var f = new Fixture();
        var m = new KiMemoryStore(f.Root); m.SetEnabled(true); m.Add("Wohnt in Köln");
        var local = new Spy("Lokal", false);
        var i = new IntelligenceCore(f.Root, f.Core.Audit, () => [local]) { Memory = m };
        await i.AskAsync(new KiRequest("Wetter?", Personal: true));
        Assert.Contains("Wohnt in Köln", local.Calls[0][0].Content);
        await i.AskAsync(new KiRequest("Wetter?"));
        Assert.DoesNotContain("Wohnt in Köln", local.Calls[1][0].Content);
        var net = new RemoteSpy();
        var r = new IntelligenceCore(f.Root, f.Core.Audit, () => [net]) { Memory = m };
        var a = await r.AskAsync(new KiRequest("Wetter?", AllowRemote: true, Personal: true));
        Assert.True(a.Ok); Assert.DoesNotContain(net.Calls[0], x => x.Content.Contains("Köln"));
    }
}
