using System.Net;
using System.Text;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Trendo.Core;
using Xunit;

namespace Trendo.App.Tests;

public sealed class FakeKi(string answer = "Hallo, ich bin Trendo.") : IKiBackend
{
    public string Name => "Test-KI"; public string BaseUrl => "http://127.0.0.1:1";
    public Task<bool> IsUpAsync(CancellationToken ct = default) => Task.FromResult(true);
    public Task<IReadOnlyList<string>> ModelsAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<string>>(["test"]);
    public Task<string> ChatAsync(string m, IReadOnlyList<KiMessage> x, CancellationToken ct = default) => Task.FromResult(answer);
}

public static class W2Ui
{
    public static void Settle() { for (var i = 0; i < 5; i++) { Dispatcher.UIThread.RunJobs(); Thread.Sleep(10); } }
}

public class W2_Ui
{
    [AvaloniaFact(DisplayName = "CORE-02 UI: Studio verweist auf „KI fragen“, Frage wird beantwortet")]
    public void Ask_page()
    {
        var (w, _, _) = Ui.Open();
        w.KiBackendsOverride = () => [new FakeKi()];
        Assert.Equal("ki-fragen", MainWindow.StudioPages["CORE-02"]);
        w.Show("ki-fragen"); W2Ui.Settle();
        w.Find<TextBox>("ask-input").Text = "Wer bist du?";
        w.Find<Button>("ask-send").Press(); W2Ui.Settle();
        Assert.Equal("Hallo, ich bin Trendo.", w.LastAnswer);
        Assert.Contains("Antwort von test", w.Find<TextBlock>("ask-state").Text);
        Assert.NotNull(w.TryFind<Button>("ki-tab-ki"));
    }
}

public sealed class StubHandler(Func<HttpRequestMessage, (HttpStatusCode, string)> f) : HttpMessageHandler
{
    public List<string> Paths { get; } = [];
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken ct)
    {
        Paths.Add(r.RequestUri!.AbsolutePath);
        var (c, body) = f(r);
        return Task.FromResult(new HttpResponseMessage(c) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
    }
}

public class CORE02_Backends
{
    [Fact(DisplayName = "CORE-02: Nur Quellen auf diesem Gerät – Cloud-Adressen werden abgelehnt")]
    public void Rejects_remote()
    {
        Assert.Throws<InvalidOperationException>(() => new OllamaBackend("https://api.example.com"));
        Assert.Throws<InvalidOperationException>(() => new OpenAiCompatBackend("x", "http://192.168.1.5:1234"));
        _ = new OllamaBackend("http://localhost:11434");
    }

    [Fact(DisplayName = "CORE-02: Ollama-Format (tags/chat) wird korrekt gesprochen")]
    public async Task Ollama_protocol()
    {
        var h = new StubHandler(r => r.RequestUri!.AbsolutePath switch
        {
            "/api/version" => (HttpStatusCode.OK, "{\"version\":\"0.5\"}"),
            "/api/tags" => (HttpStatusCode.OK, "{\"models\":[{\"name\":\"qwen2.5:3b\"}]}"),
            "/api/chat" => (HttpStatusCode.OK, "{\"message\":{\"role\":\"assistant\",\"content\":\" Hallo! \"}}"),
            _ => (HttpStatusCode.NotFound, "{}"),
        });
        var b = new OllamaBackend(handler: h);
        Assert.True(await b.IsUpAsync());
        Assert.Equal(["qwen2.5:3b"], await b.ModelsAsync());
        Assert.Equal("Hallo!", await b.ChatAsync("qwen2.5:3b", [new("user", "hi")]));
    }

}

public sealed class SrcFake(string name, bool up, params string[] models) : IKiBackend
{
    public string Name => name; public string BaseUrl => "http://127.0.0.1:1";
    public Task<bool> IsUpAsync(CancellationToken ct = default) => Task.FromResult(up);
    public Task<IReadOnlyList<string>> ModelsAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<string>>(models);
    public Task<string> ChatAsync(string m, IReadOnlyList<KiMessage> x, CancellationToken ct = default) => Task.FromResult($"{name}:{m}");
}

public class X05_Ui
{
    [AvaloniaFact(DisplayName = "X-05 UI: Suche findet laufende Quelle, Fragen nutzt die Rückfallkette")]
    public async Task Scan_and_fallback()
    {
        var (w, _, _) = Ui.Open();
        w.KiBackendFactory = s => s.Id == "lmstudio" ? new SrcFake("LM Studio", true, "phi") : new SrcFake(s.Name, false);
        w.Show("ki-quellen");
        await w.ScanSourcesAsync(); w.Show("ki-quellen"); W2Ui.Settle();
        Assert.Contains("1 KI-Quelle", w.Find<TextBlock>("src-msg").Text);
        Assert.True(w.LastScan["lmstudio"].Up);
        var a = await w.Intel.AskAsync(new KiRequest("Hallo"));
        Assert.Equal("LM Studio:phi", a.Text);
        w.Find<Button>("src-toggle-lmstudio").Press();
        Assert.False((await w.Intel.AskAsync(new KiRequest("Hallo"))).Ok);
        w.Find<Button>("ki-tab-ki-quellen");
    }
}

public class CORE10_Ui
{
    [AvaloniaFact(DisplayName = "CORE-10 UI: Merken, Suchen, Löschen und Korrektur aus dem Gespräch")]
    public void Knowledge_page()
    {
        var (w, _, _) = Ui.Open();
        w.Show("ki-wissen");
        w.Find<TextBox>("wissen-text").Text = "Der Müll wird dienstags abgeholt.";
        w.Find<Button>("wissen-add").Press();
        Assert.Equal("Gemerkt.", w.Find<TextBlock>("wissen-msg").Text);
        w.Find<TextBox>("wissen-search").Text = "muell dienstag";
        w.Find<Button>("wissen-go").Press();
        Assert.NotNull(w.TryFind<Button>("wissen-del-0"));
        w.KiBackendsOverride = () => [new FakeKi("Montag")];
        w.Show("ki-fragen"); w.Find<TextBox>("ask-input").Text = "Wann wird der Müll abgeholt?"; w.Find<Button>("ask-send").Press(); W2Ui.Settle();
        Assert.Contains("Wissen:", w.Find<TextBlock>("ask-state").Text);
        w.Find<TextBox>("ask-fix").Text = "Dienstags."; w.Find<Button>("ask-bad").Press();
        Assert.Equal(2, w.Wissen.All().Count);
        w.Show("ki-wissen"); w.Find<Button>("wissen-del-0").Press();
        Assert.Single(w.Wissen.All());
    }
}

public class CORE12_Ui
{
    [AvaloniaFact(DisplayName = "CORE-12 UI: Vorlage ausfüllen und senden, neue Vorlage speichern, Masterprompt bauen")]
    public void Prompts_page()
    {
        var (w, _, _) = Ui.Open();
        w.KiBackendsOverride = () => [new FakeKi("Strom fließt.")];
        w.Show("ki-vorlagen");
        w.Find<Button>("prompt-einfach-erklaeren").Press();
        w.Find<Button>("prompt-send").Press(); W2Ui.Settle();
        Assert.Contains("Thema", w.Find<TextBlock>("prompt-msg").Text);
        w.Find<TextBox>("pvar-Thema").Text = "Strom";
        w.Find<Button>("prompt-send").Press(); W2Ui.Settle();
        Assert.Equal("Strom fließt.", w.LastPromptOutput);
        w.Find<Button>("prompt-new").Press();
        w.Find<TextBox>("pedit-name").Text = "Gedicht"; w.Find<TextBox>("pedit-text").Text = "Schreibe ein Gedicht über {{Tier}} in 4 Zeilen.";
        w.Find<Button>("pedit-save").Press();
        Assert.Contains("Version 1", w.Find<TextBlock>("prompt-msg").Text);
        Assert.NotNull(w.TryFind<TextBox>("pvar-Tier"));
        w.Find<Button>("prompt-master").Press();
        w.Find<TextBox>("master-role").Text = "ein Koch"; w.Find<Button>("master-build").Press();
        Assert.Contains("Du bist ein Koch.", w.Find<SelectableTextBlock>("master-out").Text);
        w.Find<Button>("master-save").Press();
        Assert.Contains(w.Prompts.All(), p => p.Category == "Masterprompts");
    }
}
