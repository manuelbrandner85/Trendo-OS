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
    [AvaloniaFact(DisplayName = "CORE-02 UI: Studio-Kachel führt zu „KI fragen“, Frage wird beantwortet")]
    public void Ask_page()
    {
        var (w, _, _) = Ui.Open();
        w.KiBackendsOverride = () => [new FakeKi()];
        _ = w.OpenStudio("CORE-02", "Trendo Intelligence Core"); W2Ui.Settle();
        Assert.Equal("ki-fragen", w.CurrentView);
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
