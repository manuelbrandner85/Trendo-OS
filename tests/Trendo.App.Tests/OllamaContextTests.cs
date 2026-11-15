using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Trendo.App;
using Trendo.Core;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>T-0662: Ollama bekommt ein ausreichendes Kontextfenster mit, sonst bricht ein langer Aktionsplan mitten im JSON ab (Ollama nimmt ohne Angabe nur 4096 Token).</summary>
public class OllamaContextTests
{
    sealed class Capture : HttpMessageHandler
    {
        public readonly List<JsonNode> Bodies = [];
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Bodies.Add(JsonNode.Parse(await request.Content!.ReadAsStringAsync(ct))!);
            var answer = """{"message":{"role":"assistant","content":"ok"},"done":true}""";
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(answer, Encoding.UTF8, "application/json") };
        }
    }

    [Fact(DisplayName = "Ollama: Kontextfenster 8192 Token, bei sehr langen Anweisungen 16384; gilt für normale und gestreamte Antworten")]
    public async Task Sends_context_window()
    {
        var h = new Capture(); var b = new OllamaBackend("http://127.0.0.1:11434", h);
        await b.ChatAsync("m", [new("system", WebDesignKi.Instructions(null)), new("user", WebDesignKi.NewPagePrompt("Bäckerei"))]);
        await b.ChatStreamAsync("m", [new("user", "kurz")], _ => { });
        await b.ChatAsync("m", [new("user", new string('x', 30000))]);
        Assert.Equal([8192, 8192, 16384], h.Bodies.Select(x => (int)x["options"]!["num_ctx"]!));
        Assert.True(WebDesignKi.Instructions(null).Length / 3 + 4096 <= 8192); // die Web-Anweisungen passen in die kleine Stufe
    }
}
