using System.Net.Http.Json;
using System.Text.Json;
using Trendo.Core;

namespace Trendo.App;

public static class LocalOnly
{
    /// <summary>Wirft, wenn die Adresse nicht auf diesem Gerät liegt (CORE-02: keine Cloud-Exfiltration).</summary>
    public static Uri Require(string url)
    {
        var u = new Uri(url);
        if (!(u.IsLoopback || u.Host is "localhost" or "127.0.0.1" or "[::1]"))
            throw new InvalidOperationException($"Nur Quellen auf diesem Gerät erlaubt, nicht {u.Host}.");
        return u;
    }
}

public sealed class OllamaBackend(string baseUrl = "http://127.0.0.1:11434", HttpMessageHandler? handler = null) : IKiBackend
{
    private readonly HttpClient _http = new(handler ?? new HttpClientHandler()) { BaseAddress = LocalOnly.Require(baseUrl), Timeout = TimeSpan.FromMinutes(30) };
    public string Name => "Ollama";
    public string BaseUrl => baseUrl;
    public bool Serves(KiJob job) => true;

    public async Task<bool> IsUpAsync(CancellationToken ct = default)
    {
        try { using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct); cts.CancelAfter(1200); using var r = await _http.GetAsync("/api/version", cts.Token); return r.IsSuccessStatusCode; }
        catch { return false; }
    }

    public async Task<IReadOnlyList<string>> ModelsAsync(CancellationToken ct = default)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct); cts.CancelAfter(2000);
            var doc = await _http.GetFromJsonAsync<JsonElement>("/api/tags", cts.Token);
            return doc.GetProperty("models").EnumerateArray().Select(m => m.GetProperty("name").GetString() ?? "").Where(n => n != "").ToList();
        }
        catch { return []; }
    }

    /// <summary>X-05: freies Modell über Ollama laden (wartet bis fertig).</summary>
    public async Task<bool> PullAsync(string model, CancellationToken ct = default)
    {
        try { using var r = await _http.PostAsJsonAsync("/api/pull", new { model, stream = false }, ct); return r.IsSuccessStatusCode; }
        catch { return false; }
    }

    /// <summary>Dasselbe Laden, aber mit Fortschritt. Abbruch über den Token. Ollama setzt einen abgebrochenen Ladevorgang beim nächsten Mal fort.</summary>
    public async Task<bool> PullAsync(string model, IProgress<FetchTick>? progress, CancellationToken ct = default)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, "/api/pull") { Content = JsonContent.Create(new { model, stream = true }) };
            using var r = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
            if (!r.IsSuccessStatusCode) return false;
            using var stream = await r.Content.ReadAsStreamAsync(ct);
            using var reader = new StreamReader(stream);
            var saw = false;
            while (await reader.ReadLineAsync(ct) is { } line)
            {
                if (line.Length == 0) continue;
                saw = true;
                using var doc = JsonDocument.Parse(line);
                var status = doc.RootElement.TryGetProperty("status", out var st) ? st.GetString() ?? "" : "";
                double frac = 0;
                if (doc.RootElement.TryGetProperty("total", out var tot) && tot.TryGetInt64(out var total) && total > 0
                    && doc.RootElement.TryGetProperty("completed", out var doneEl) && doneEl.TryGetInt64(out var done))
                    frac = (double)done / total;
                progress?.Report(new FetchTick(frac, status.Length == 0 ? "Lädt …" : status));
                if (doc.RootElement.TryGetProperty("error", out var err) && err.GetString() is { Length: > 0 }) return false;
            }
            return saw;
        }
        catch (OperationCanceledException) { throw; }
        catch { return false; }
    }

    public async Task<bool> DeleteAsync(string model, CancellationToken ct = default)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Delete, "/api/delete") { Content = JsonContent.Create(new { name = model }) };
            using var r = await _http.SendAsync(req, ct);
            return r.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    public async Task<string> SeeAsync(string model, string question, byte[] image, CancellationToken ct = default)
    {
        var body = new
        {
            model,
            stream = false,
            messages = new[] { new { role = "user", content = question, images = new[] { Convert.ToBase64String(image) } } },
        };
        using var r = await _http.PostAsJsonAsync("/api/chat", body, ct);
        r.EnsureSuccessStatusCode();
        var doc = await r.Content.ReadFromJsonAsync<JsonElement>(ct);
        return doc.GetProperty("message").GetProperty("content").GetString()?.Trim() ?? "";
    }

    public async Task<float[]?> EmbedAsync(string model, string text, CancellationToken ct = default)
    {
        using var r = await _http.PostAsJsonAsync("/api/embed", new { model, input = text }, ct);
        if (!r.IsSuccessStatusCode) return null;
        var doc = await r.Content.ReadFromJsonAsync<JsonElement>(ct);
        if (!doc.TryGetProperty("embeddings", out var emb) || emb.GetArrayLength() == 0) return null;
        return emb[0].EnumerateArray().Select(x => x.GetSingle()).ToArray();
    }

    public async Task<string> ChatAsync(string model, IReadOnlyList<KiMessage> messages, CancellationToken ct = default)
    {
        var body = new { model, stream = false, messages = messages.Select(m => new { role = m.Role, content = m.Content }).ToArray() };
        using var r = await _http.PostAsJsonAsync("/api/chat", body, ct);
        r.EnsureSuccessStatusCode();
        var doc = await r.Content.ReadFromJsonAsync<JsonElement>(ct);
        return doc.GetProperty("message").GetProperty("content").GetString()?.Trim() ?? "";
    }
}

/// <summary>LM Studio, llama.cpp-Server, Jan, KoboldCpp … sprechen das OpenAI-Format unter /v1 – hier nur lokal.</summary>
public sealed class OpenAiCompatBackend(string name, string baseUrl, HttpMessageHandler? handler = null) : IKiBackend
{
    private readonly HttpClient _http = new(handler ?? new HttpClientHandler()) { BaseAddress = LocalOnly.Require(baseUrl), Timeout = TimeSpan.FromSeconds(180) };
    public string Name => name;
    public string BaseUrl => baseUrl;
    public bool Serves(KiJob job) => true;
    public async Task<bool> IsUpAsync(CancellationToken ct = default) => (await ModelsAsync(ct)).Count > 0;

    public async Task<IReadOnlyList<string>> ModelsAsync(CancellationToken ct = default)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct); cts.CancelAfter(1200);
            var doc = await _http.GetFromJsonAsync<JsonElement>("/v1/models", cts.Token);
            return doc.GetProperty("data").EnumerateArray().Select(m => m.GetProperty("id").GetString() ?? "").Where(n => n != "").ToList();
        }
        catch { return []; }
    }

    public async Task<string> ChatAsync(string model, IReadOnlyList<KiMessage> messages, CancellationToken ct = default)
    {
        var body = new { model, stream = false, messages = messages.Select(m => new { role = m.Role, content = m.Content }).ToArray() };
        using var r = await _http.PostAsJsonAsync("/v1/chat/completions", body, ct);
        r.EnsureSuccessStatusCode();
        var doc = await r.Content.ReadFromJsonAsync<JsonElement>(ct);
        return doc.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString()?.Trim() ?? "";
    }
}

/// <summary>Kostenloser Online-Dienst. Nur nach ausdrücklicher Erlaubnis und nur mit einem Schlüssel aus dem Speicher.</summary>
public sealed class OnlineBackend : IKiBackend
{
    private readonly FreeProvider _p;
    private readonly HttpClient _http;
    public OnlineBackend(FreeProvider provider, string key, HttpMessageHandler? handler = null)
    {
        _p = provider;
        _http = new HttpClient(handler ?? new HttpClientHandler()) { BaseAddress = new Uri(provider.ApiBase), Timeout = TimeSpan.FromMinutes(2) };
        _http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", key);
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("TrendoOS");
    }
    public string Name => _p.Name;
    public string BaseUrl => _p.ApiBase;
    public bool Local => false;
    public int Quality => _p.Quality;
    public bool Serves(KiJob job) => _p.Jobs.Contains(job);
    public Task<bool> IsUpAsync(CancellationToken ct = default) => Task.FromResult(true);
    public Task<IReadOnlyList<string>> ModelsAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<string>>(_p.Models);

    public async Task<string> ChatAsync(string model, IReadOnlyList<KiMessage> messages, CancellationToken ct = default)
    {
        var body = new { model, stream = false, messages = messages.Select(m => new { role = m.Role, content = m.Content }).ToArray() };
        return await CompleteAsync(body, ct);
    }

    public async Task<string> SeeAsync(string model, string question, byte[] image, CancellationToken ct = default)
    {
        var body = new
        {
            model,
            stream = false,
            messages = new[]
            {
                new
                {
                    role = "user",
                    content = new object[]
                    {
                        new { type = "text", text = question },
                        new { type = "image_url", image_url = new { url = "data:image/png;base64," + Convert.ToBase64String(image) } },
                    },
                },
            },
        };
        return await CompleteAsync(body, ct);
    }

    private async Task<string> CompleteAsync(object body, CancellationToken ct)
    {
        using var r = await _http.PostAsJsonAsync("chat/completions", body, ct);
        var text = await r.Content.ReadAsStringAsync(ct);
        if (Limited(r.StatusCode, text)) throw new KiLimitException($"{_p.Name}: Kontingent");
        if (!r.IsSuccessStatusCode) throw new InvalidOperationException($"{_p.Name}: {(int)r.StatusCode}");
        using var doc = JsonDocument.Parse(text);
        return doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString()?.Trim() ?? "";
    }

    internal static bool Limited(System.Net.HttpStatusCode code, string body)
    {
        if (code is System.Net.HttpStatusCode.TooManyRequests or System.Net.HttpStatusCode.PaymentRequired) return true;
        var b = body.ToLowerInvariant();
        return b.Contains("rate limit") || b.Contains("rate_limit") || b.Contains("quota") || b.Contains("insufficient_quota") || b.Contains("resource_exhausted") || b.Contains("billing");
    }
}

