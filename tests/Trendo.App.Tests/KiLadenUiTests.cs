using System.Net;
using System.Text;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Trendo.Core;
using Xunit;

namespace Trendo.App.Tests;

public class KiLaden_Ui
{
    const long G = 1024L * 1024 * 1024;

    [AvaloniaFact(DisplayName = "KI-Laden: Passung, Empfehlung, Holen und Entfernen ohne echtes Netz")]
    public async Task Shop_fit_pull_remove()
    {
        var (w, _, _) = Ui.Open();
        w.HardwareOverride = () => new HardwareInfo(8 * G, null, 0, 4);
        w.InstalledModelsOverride = () => [];
        w.FreeBytesOverride = () => 1024;
        var pulled = new List<string>();
        w.ModelPullOverride = (tag, _, _) => { pulled.Add(tag); return Task.FromResult(true); };
        w.Show("ki-laden"); W2Ui.Settle();
        Assert.Contains("Für diesen PC", w.Find<TextBlock>("laden-plan").Text);
        Assert.Contains("zu groß", w.Find<TextBlock>("laden-fit-mistral-7b").Text);
        Assert.Equal("Empfohlen", w.Find<TextBlock>("laden-tip-gemma3-4b").Text);
        Assert.False(w.Find<Button>("laden-holen-empfohlen").IsEnabled);
        w.Find<Button>("laden-holen-gemma3-4b").Press();
        for (var i = 0; i < 40 && !(w.Find<TextBlock>("laden-status").Text ?? "").Contains("Zu wenig"); i++) { await Task.Delay(20); Dispatcher.UIThread.RunJobs(); }
        Assert.Contains("Zu wenig Platz", w.Find<TextBlock>("laden-status").Text);
        Assert.Empty(pulled);

        w.FreeBytesOverride = () => 80 * G;
        w.Show("ki-laden"); W2Ui.Settle();
        w.Find<Button>("laden-holen-empfohlen").Press();
        for (var i = 0; i < 40 && pulled.Count < 4; i++) { await Task.Delay(20); Dispatcher.UIThread.RunJobs(); }
        Assert.Equal(4, pulled.Count);
        Assert.Contains("bereit", w.Find<TextBlock>("laden-status").Text);

        var gone = false;
        w.InstalledModelsOverride = () => gone ? [] : ["moondream"];
        w.ModelRemoveOverride = tag => { gone = tag == "moondream"; return Task.FromResult(true); };
        w.Show("ki-laden"); W2Ui.Settle();
        w.Find<Button>("laden-weg-moondream").Press();
        for (var i = 0; i < 40 && !(w.Find<TextBlock>("laden-status").Text ?? "").Contains("entfernt"); i++) { await Task.Delay(20); Dispatcher.UIThread.RunJobs(); }
        Assert.Contains("entfernt", w.Find<TextBlock>("laden-status").Text);
        Assert.DoesNotContain(w.Core.Audit.ReadAll(), e => e.Detail.Contains("http"));
        w.Close();
    }

    [AvaloniaFact(DisplayName = "Einrichtung: Plan, Platz und Schlüssel holen auf Schritt 4")]
    public void Wizard_shows_plan()
    {
        var (w, l, _) = Ui.Open();
        w.HardwareOverride = () => new HardwareInfo(8 * G, null, 0, 4);
        w.Show("einrichtung-3"); W2Ui.Settle();
        Assert.Contains("Für diesen PC", w.Find<TextBlock>("wiz-ki-plan").Text);
        Assert.False(string.IsNullOrWhiteSpace(w.Find<TextBlock>("wiz-ki-disk").Text));
        Assert.NotNull(w.Find<CheckBox>("wiz-opt-master"));
        Assert.NotNull(w.Find<Button>("wiz-ki-stop"));
        w.Find<Button>("wiz-getkey-gemini").Press();
        Assert.Contains(l.Opened, o => o.Contains("aistudio.google.com"));
        Assert.NotNull(w.Find<CheckBox>("wiz-pack-ki"));
        w.Close();
    }

    [AvaloniaFact(DisplayName = "Online-Dienst: Schlüssel gilt erst mit beiden Häkchen, nie im Protokoll")]
    public async Task Opt_in_gates_the_key()
    {
        var (w, _, _) = Ui.Open();
        w.Show("keys"); W2Ui.Settle();
        Assert.DoesNotContain(w.BackendsNow(), b => b is OnlineBackend);
        w.Find<CheckBox>("opt-groq").IsChecked = true;
        w.Find<CheckBox>("opt-master").IsChecked = true;
        Assert.DoesNotContain(w.BackendsNow(), b => b is OnlineBackend);
        w.KeyValidatorOverride = k => Task.FromResult(k == "frei-123");
        w.Find<TextBox>("txt-key-groq").Text = "frei-123";
        w.Find<Button>("btn-savekey-groq").Press();
        for (var i = 0; i < 40 && w.Core.Secrets.Get(FreeOnline.SecretKey("groq")) is null; i++) { await Task.Delay(20); Dispatcher.UIThread.RunJobs(); }
        Assert.Equal("frei-123", w.Core.Secrets.Get(FreeOnline.SecretKey("groq")));
        Assert.Contains(w.BackendsNow(), b => b.Name == "Groq" && !b.Local);
        Assert.DoesNotContain(w.Core.Audit.ReadAll(), e => e.Detail.Contains("frei-123"));
        w.Find<CheckBox>("opt-master").IsChecked = false; W2Ui.Settle();
        Assert.DoesNotContain(w.BackendsNow(), b => b is OnlineBackend);
        w.Close();

        var (off, _, root) = Ui.Open(offline: true);
        var opt = new OnlineOptIn { Allow = true }; opt.Set("gemini", true); opt.Save(root);
        off.Core.Secrets.Set(FreeOnline.SecretKey("gemini"), "x");
        Assert.DoesNotContain(off.BackendsNow(), b => b is OnlineBackend);
        off.Show("keys"); W2Ui.Settle();
        Assert.False(off.Find<Button>("btn-getkey-gemini").IsEnabled);
        off.Close();
    }
}

public class KiLaden_Protocol
{
    [Fact(DisplayName = "Ollama-Laden meldet Fortschritt, Online-Limit wirft und springt weiter")]
    public async Task Pull_stream_and_limit()
    {
        var h = new CaptureHandler("{\"status\":\"pulling\",\"total\":10,\"completed\":4}\n{\"status\":\"success\",\"total\":10,\"completed\":10}\n");
        var ollama = new OllamaBackend(handler: h);
        var seen = new List<double>();
        Assert.True(await ollama.PullAsync("qwen2.5:0.5b", new InlineProgress(t => seen.Add(t.Fraction)), CancellationToken.None));
        Assert.Contains(seen, x => x > 0.3 && x < 0.5);
        Assert.Contains("\"stream\":true", h.Body);
        Assert.Equal(HttpMethod.Post, h.Method);

        var limit = new OnlineBackend(FreeOnline.Providers.First(p => p.Id == "groq"), "nicht-der-echte", new StubHandler(_ => (HttpStatusCode.TooManyRequests, "{\"error\":\"rate limit\"}")));
        await Assert.ThrowsAsync<KiLimitException>(() => limit.ChatAsync("openai/gpt-oss-20b", [new("user", "hi")]));
        var pay = new OnlineBackend(FreeOnline.Providers.First(p => p.Id == "mistral"), "k", new StubHandler(_ => (HttpStatusCode.PaymentRequired, "{\"error\":\"billing\"}")));
        await Assert.ThrowsAsync<KiLimitException>(() => pay.ChatAsync("mistral-small-latest", [new("user", "hi")]));
    }

    sealed class CaptureHandler(string body) : HttpMessageHandler
    {
        public string Body { get; private set; } = "";
        public HttpMethod? Method { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Method = request.Method;
            Body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/x-ndjson") };
        }
    }
}
